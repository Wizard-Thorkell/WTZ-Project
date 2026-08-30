[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string] $OutputDirectory,

    [switch] $AllowDirtySourceForDevelopment,

    [switch] $SkipBuildForDevelopment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repoPrefix = $repoRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$engineRoot = Join-Path $repoRoot "RobustToolbox"
$project = Join-Path $repoRoot "Content.IntegrationTests\Content.IntegrationTests.csproj"
$expectedTest = "Content.IntegrationTests.Tests.ZLevel.ZLevelRecoveryRehearsalTest.ValidatedCheckpointRejectsCorruptionAndRecoversTwice"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-recovery"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Recovery output must be inside the repository: $OutputDirectory"
}

function Invoke-GitResult([string] $Root, [string[]] $Arguments) {
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& git -C $Root @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n").Trim()
    }
}

function Get-GitText([string] $Root, [string[]] $Arguments, [string] $Description) {
    $result = Invoke-GitResult $Root $Arguments
    if ($result.ExitCode -ne 0) {
        throw "$Description failed with exit code $($result.ExitCode): $($result.Output)"
    }

    return $result.Output
}

function Assert-Equal($Actual, $Expected, [string] $Description) {
    if ($Actual -ne $Expected) {
        throw "$Description is '$Actual'; expected '$Expected'."
    }
}

$projectRevision = Get-GitText $repoRoot @("rev-parse", "HEAD") "WTZ Project revision lookup"
$engineRevision = Get-GitText $engineRoot @("rev-parse", "HEAD") "WTZ Engine revision lookup"
$projectStatus = Get-GitText $repoRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Project worktree lookup"
$engineStatus = Get-GitText $engineRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Engine worktree lookup"
$projectClean = [string]::IsNullOrWhiteSpace($projectStatus)
$engineClean = [string]::IsNullOrWhiteSpace($engineStatus)

$gitlinkText = Get-GitText $repoRoot @("ls-tree", "HEAD", "--", "RobustToolbox") `
    "WTZ Engine gitlink lookup"
if ($gitlinkText -notmatch '^160000 commit ([0-9a-f]{40})\s+RobustToolbox$') {
    throw "Unable to parse the RobustToolbox submodule gitlink."
}

$gitlinkRevision = $Matches[1]
if ($gitlinkRevision -ne $engineRevision) {
    throw "Engine gitlink '$gitlinkRevision' and checkout '$engineRevision' are not paired."
}

if ((-not $projectClean -or -not $engineClean) -and -not $AllowDirtySourceForDevelopment) {
    throw "WTZ-RECOVERY-1 requires clean project and engine worktrees. Use -AllowDirtySourceForDevelopment only while developing the gate."
}

$developmentRun = $AllowDirtySourceForDevelopment -or $SkipBuildForDevelopment
$expectedStatus = if ($developmentRun) { "DevelopmentPassed" } else { "Passed" }
$runId = "{0}-{1}-{2}" -f
    [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"),
    $PID,
    [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDirectory = Join-Path $OutputDirectory $runId
[System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$reportPath = Join-Path $runDirectory "zlevel-recovery.json"
$trxName = "zlevel-recovery.trx"
$trxPath = Join-Path $runDirectory $trxName

$environment = [ordered]@{
    WTZ_ZLEVEL_RECOVERY_DIR = $runDirectory
    WTZ_ZLEVEL_RECOVERY_STATUS = $expectedStatus
    WTZ_ZLEVEL_RECOVERY_PROJECT_REVISION = $projectRevision
    WTZ_ZLEVEL_RECOVERY_ENGINE_REVISION = $engineRevision
    WTZ_ZLEVEL_RECOVERY_GITLINK_REVISION = $gitlinkRevision
    WTZ_ZLEVEL_RECOVERY_PROJECT_CLEAN = $projectClean.ToString()
    WTZ_ZLEVEL_RECOVERY_ENGINE_CLEAN = $engineClean.ToString()
}
$previousEnvironment = @{}
foreach ($name in $environment.Keys) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    [Environment]::SetEnvironmentVariable($name, [string] $environment[$name])
}

$arguments = @(
    "test",
    $project,
    "--configuration", $Configuration,
    "--filter", "FullyQualifiedName=$expectedTest",
    "--logger", "trx;LogFileName=$trxName",
    "--results-directory", $runDirectory,
    "--nologo"
)
if ($SkipBuildForDevelopment) {
    $arguments += @("--no-build", "--no-restore")
}

try {
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Recovery rehearsal test failed with exit code $LASTEXITCODE."
    }
}
finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name])
    }
}

if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
    throw "Missing recovery TRX result: $trxPath"
}

[xml] $trx = Get-Content -LiteralPath $trxPath -Raw
$definitionsById = @{}
foreach ($definition in @($trx.SelectNodes("//*[local-name()='TestDefinitions']/*[local-name()='UnitTest']"))) {
    $method = $definition.SelectSingleNode("*[local-name()='TestMethod']")
    if ($null -eq $method) {
        continue
    }

    $definitionsById[$definition.GetAttribute("id")] =
        "$($method.GetAttribute('className')).$($method.GetAttribute('name'))"
}

$outcomes = @{}
foreach ($result in @($trx.SelectNodes("//*[local-name()='Results']/*[local-name()='UnitTestResult']"))) {
    $testId = $result.GetAttribute("testId")
    if (-not $definitionsById.ContainsKey($testId)) {
        throw "Recovery TRX references unknown test id '$testId'."
    }

    $testName = $definitionsById[$testId]
    if ($outcomes.ContainsKey($testName)) {
        throw "Recovery TRX contains duplicate result for '$testName'."
    }

    $outcomes[$testName] = $result.GetAttribute("outcome")
}

if ($outcomes.Count -ne 1 -or -not $outcomes.ContainsKey($expectedTest)) {
    throw "Recovery TRX must contain exactly the protected test '$expectedTest'."
}

Assert-Equal $outcomes[$expectedTest] "Passed" "Protected recovery test outcome"

if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Missing recovery report: $reportPath"
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
Assert-Equal ([int] $report.schemaVersion) 1 "Recovery report schema"
Assert-Equal ([string] $report.contractVersion) "WTZ-RECOVERY-1" "Recovery report contract"
Assert-Equal ([string] $report.status) $expectedStatus "Recovery report status"
Assert-Equal ([string] $report.fullyQualifiedTest) $expectedTest "Recovery report test identity"
Assert-Equal ([string] $report.source.projectRevision) $projectRevision "Project revision"
Assert-Equal ([string] $report.source.engineRevision) $engineRevision "Engine revision"
Assert-Equal ([string] $report.source.gitlinkRevision) $gitlinkRevision "Engine gitlink revision"
Assert-Equal ([bool] $report.source.projectClean) $projectClean "Project clean-source flag"
Assert-Equal ([bool] $report.source.engineClean) $engineClean "Engine clean-source flag"

Assert-Equal ([int] $report.scenario.floorCount) 3 "Recovery floor count"
Assert-Equal ([int] $report.scenario.gridCount) 1 "Recovery grid count"
if ([int] $report.scenario.initialValidatedEntities -le 0) {
    throw "Initial checkpoint did not validate any entities."
}
Assert-Equal ([int] $report.scenario.recoveredValidatedEntities) `
    ([int] $report.scenario.initialValidatedEntities) "Recovered validated entity count"
Assert-Equal ([int] $report.scenario.initialExcludedRoots) 2 "Initial excluded root count"
Assert-Equal ([int] $report.scenario.recoveredExcludedRoots) 0 "Recovered excluded root count"
Assert-Equal ([long] $report.scenario.attempts) 3 "Checkpoint attempt count"
Assert-Equal ([long] $report.scenario.successes) 2 "Checkpoint success count"
Assert-Equal ([long] $report.scenario.failures) 1 "Checkpoint failure count"
Assert-Equal ([string] $report.scenario.finalHealthStatus) "Degraded" "Final health status"
if ([string] $report.scenario.rejectedCheckpointError -notmatch "outside the declared range") {
    throw "Recovery report does not contain the expected rejected-map diagnostic."
}

if ([string] $report.scenario.initialCheckpoint -eq [string] $report.scenario.recoveredCheckpoint) {
    throw "Initial and recovered checkpoint paths must be distinct."
}
foreach ($path in @(
    [string] $report.scenario.initialCheckpoint,
    [string] $report.scenario.recoveredCheckpoint)) {
    if ($path -notmatch '-CHECKPOINT(?:-[0-9]+)?\.yml$') {
        throw "Recovery report contains a non-checkpoint destination: '$path'."
    }
}

foreach ($hash in @(
    [string] $report.scenario.initialCheckpointSha256,
    [string] $report.scenario.recoveredCheckpointSha256)) {
    if ($hash -notmatch '^[0-9a-f]{64}$') {
        throw "Recovery report contains an invalid SHA-256 value: '$hash'."
    }
}

$requiredSteps = @(
    "initialCheckpointCreated",
    "invalidCheckpointRejected",
    "knownGoodBytesPreserved",
    "corruptSourceRemoved",
    "initialCheckpointLoaded",
    "recoveredCheckpointCreated",
    "recoveredCheckpointLoaded",
    "structuralStateMatched",
    "noCriticalHealthFinding"
)
foreach ($step in $requiredSteps) {
    $property = $report.steps.PSObject.Properties[$step]
    if ($null -eq $property -or -not [bool] $property.Value) {
        throw "Recovery step '$step' is missing or did not pass."
    }
}
Assert-Equal ([int] $report.steps.temporaryFilesRemaining) 0 "Temporary recovery files"

$reportHash = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "WTZ-RECOVERY-1 $expectedStatus."
Write-Host "  run=$runId"
Write-Host "  source=$projectRevision, engine=$engineRevision, gitlink=$gitlinkRevision"
Write-Host "  checkpoints=2, rejected=1, loads=2, structural-match=true"
Write-Host "  report=$reportPath"
Write-Host "  report-sha256=$reportHash"
