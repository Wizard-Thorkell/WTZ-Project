[CmdletBinding()]
param(
    [string] $ProjectRoot,

    [string] $EngineRoot,

    [string] $ManifestPath,

    [string] $OutputDirectory,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [switch] $SkipBuild,

    [switch] $NoRestore,

    [switch] $KeepWorktrees,

    [switch] $AllowDirtySourceForDevelopment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ProjectRoot "Docs\ZLevelPortingManifest.json"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing Z-level porting manifest: $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($EngineRoot)) {
    $EngineRoot = Join-Path $ProjectRoot ([string] $manifest.project.submodulePath)
}

$EngineRoot = [System.IO.Path]::GetFullPath($EngineRoot)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "artifacts\zlevel-port-rehearsal"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null

$runId = "{0}-{1}-{2}" -f
    [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"),
    $PID,
    [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDirectory = Join-Path $OutputDirectory $runId
[System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null

$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$temporaryPrefix = $temporaryRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$workRoot = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot "wtz-zpr-$runId"))
if (-not $workRoot.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
    -not [System.IO.Path]::GetFileName($workRoot).StartsWith("wtz-zpr-", [System.StringComparison]::Ordinal)) {
    throw "Refusing unsafe rehearsal work root: $workRoot"
}

if (Test-Path -LiteralPath $workRoot) {
    throw "Rehearsal work root already exists: $workRoot"
}

[System.IO.Directory]::CreateDirectory($workRoot) | Out-Null
$ownershipMarker = Join-Path $workRoot ".wtz-port-rehearsal-owner"
[System.IO.File]::WriteAllText(
    $ownershipMarker,
    $runId,
    [System.Text.UTF8Encoding]::new($false))

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$scenarioReports = [System.Collections.Generic.List[object]]::new()
$sourceProjectRevision = "unknown"
$sourceEngineRevision = "unknown"
$sourceProjectStatus = "unknown"
$sourceEngineStatus = "unknown"
$worktreesRemoved = $false
$status = "Failed"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-GitResult([string] $Root, [string[]] $Arguments) {
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& git -c core.longpaths=true -C $Root @Arguments 2>&1)
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

function Invoke-Git([string] $Root, [string[]] $Arguments, [string] $Description) {
    $result = Invoke-GitResult $Root $Arguments
    if ($result.ExitCode -ne 0) {
        throw "$Description failed with exit code $($result.ExitCode): $($result.Output)"
    }

    if (-not [string]::IsNullOrWhiteSpace($result.Output)) {
        Write-Verbose $result.Output
    }

    return $result.Output
}

function Get-GitText([string] $Root, [string[]] $Arguments, [string] $Description) {
    return (Invoke-Git $Root $Arguments $Description).Trim()
}

function Assert-CleanRepository([string] $Root, [string] $Description) {
    $statusText = Get-GitText $Root @("status", "--porcelain") "$Description status lookup"
    if (-not [string]::IsNullOrWhiteSpace($statusText)) {
        throw "$Description is not clean: $statusText"
    }
}

function ConvertTo-FileUri([string] $Path) {
    return [Uri]::new([System.IO.Path]::GetFullPath($Path)).AbsoluteUri
}

function Get-SubmoduleEntries([string] $Root) {
    $gitmodules = Join-Path $Root ".gitmodules"
    if (-not (Test-Path -LiteralPath $gitmodules -PathType Leaf)) {
        return @()
    }

    $keysResult = Invoke-GitResult $Root @(
        "config",
        "-f", $gitmodules,
        "--name-only",
        "--get-regexp", "^submodule\..*\.path$")
    if ($keysResult.ExitCode -eq 1) {
        return @()
    }

    if ($keysResult.ExitCode -ne 0) {
        throw "Submodule inventory failed in '$Root': $($keysResult.Output)"
    }

    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($key in @($keysResult.Output -split "`n")) {
        $trimmedKey = $key.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmedKey)) {
            continue
        }

        if (-not $trimmedKey.StartsWith("submodule.", [System.StringComparison]::Ordinal) -or
            -not $trimmedKey.EndsWith(".path", [System.StringComparison]::Ordinal)) {
            throw "Unexpected submodule path key '$trimmedKey'."
        }

        $name = $trimmedKey.Substring(
            "submodule.".Length,
            $trimmedKey.Length - "submodule.".Length - ".path".Length)
        $path = Get-GitText $Root @("config", "-f", $gitmodules, "--get", $trimmedKey) `
            "Submodule path lookup"
        $entries.Add([pscustomobject]@{
            Name = $name
            Path = $path.Replace("/", [System.IO.Path]::DirectorySeparatorChar)
        }) | Out-Null
    }

    return @($entries)
}

function Initialize-SubmodulesFromSource(
    [string] $DestinationRoot,
    [string] $SourceRoot,
    [bool] $Shallow) {
    foreach ($entry in @(Get-SubmoduleEntries $SourceRoot)) {
        $sourceSubmodule = [System.IO.Path]::GetFullPath((Join-Path $SourceRoot $entry.Path))
        $destinationSubmodule = [System.IO.Path]::GetFullPath((Join-Path $DestinationRoot $entry.Path))
        $sourceCheck = Invoke-GitResult $sourceSubmodule @("rev-parse", "--is-inside-work-tree")
        if ($sourceCheck.ExitCode -ne 0 -or $sourceCheck.Output -ne "true") {
            throw "Source submodule is not initialized: $sourceSubmodule"
        }

        $sourceUrl = if ($Shallow) {
            ConvertTo-FileUri $sourceSubmodule
        }
        else {
            $sourceSubmodule
        }

        $arguments = @(
            "-c", "protocol.file.allow=always",
            "-c", "submodule.$($entry.Name).url=$sourceUrl",
            "submodule", "update",
            "--init",
            "--force",
            "--jobs", "4"
        )
        if ($Shallow) {
            $arguments += @("--depth", "1")
        }

        $arguments += @("--", $entry.Path)
        Write-Host "Initializing $($entry.Path) for $(Split-Path -Leaf $DestinationRoot)..."
        $null = Invoke-Git $DestinationRoot $arguments "Submodule '$($entry.Path)' initialization"
        $null = Invoke-Git $destinationSubmodule @("config", "core.longpaths", "true") `
            "Submodule long-path configuration"
        Initialize-SubmodulesFromSource $destinationSubmodule $sourceSubmodule $Shallow
    }
}

function New-IsolatedClone(
    [string] $SourceRoot,
    [string] $DestinationRoot,
    [string] $Revision,
    [bool] $Shallow,
    [string] $Description) {
    $parent = Split-Path -Parent $DestinationRoot
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    $source = if ($Shallow) { ConvertTo-FileUri $SourceRoot } else { $SourceRoot }
    $arguments = @("clone", "--quiet", "--no-checkout")
    if ($Shallow) {
        $arguments += @("--depth", "1")
    }
    else {
        $arguments += "--shared"
    }

    $arguments += @($source, $DestinationRoot)
    Write-Host "Cloning $Description..."
    $null = Invoke-Git $parent $arguments "$Description clone"
    $null = Invoke-Git $DestinationRoot @("config", "core.longpaths", "true") `
        "$Description long-path configuration"
    $null = Invoke-Git $DestinationRoot @("checkout", "--quiet", "--detach", $Revision) `
        "$Description checkout"
}

function Invoke-PortVerifier(
    [string] $Scenario,
    [string] $Mode,
    [string] $ScenarioProject,
    [string] $ScenarioEngine) {
    $verifier = Join-Path $ScenarioProject "Tools\verify_zlevel_port.ps1"
    $scenarioManifest = Join-Path $ScenarioProject "Docs\ZLevelPortingManifest.json"
    $scenarioOutput = Join-Path $runDirectory $Scenario
    [System.IO.Directory]::CreateDirectory($scenarioOutput) | Out-Null
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $verifier,
        "-Mode", $Mode,
        "-ProjectRoot", $ScenarioProject,
        "-EngineRoot", $ScenarioEngine,
        "-ManifestPath", $scenarioManifest,
        "-OutputDirectory", $scenarioOutput,
        "-Configuration", $Configuration,
        "-RequireClean"
    )
    if ($SkipBuild) {
        $arguments += "-SkipBuild"
    }

    if ($NoRestore) {
        $arguments += "-NoRestore"
    }

    Write-Host "Running $Scenario rehearsal in $Mode mode..."
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & powershell @arguments 2>&1 | ForEach-Object { Write-Host $_ }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    if ($exitCode -ne 0) {
        throw "$Scenario verifier failed with exit code $exitCode."
    }

    $reportPath = Join-Path $scenarioOutput "zlevel-port-compatibility.json"
    if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
        throw "$Scenario verifier did not produce its report."
    }

    return Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
}

function Assert-VerifierReport(
    [object] $Report,
    [string] $Mode,
    [string] $ProjectRevision,
    [string] $EngineRevision) {
    if ($Report.status -ne "Passed" -or $Report.mode -ne $Mode) {
        throw "$Mode verifier report did not pass in the requested mode."
    }

    if ($Report.revisions.project -ne $ProjectRevision -or
        $Report.revisions.engine -ne $EngineRevision) {
        throw "$Mode verifier report revisions do not match the rehearsal checkouts."
    }

    if ($Report.revisions.projectDirty -or $Report.revisions.engineDirty) {
        throw "$Mode verifier report observed a dirty rehearsal checkout."
    }

    if ($Report.summary.capabilities -ne 20 -or
        $Report.summary.probes -ne 50 -or
        $Report.summary.probesPassed -ne 50 -or
        $Report.summary.failures -ne 0) {
        throw "$Mode verifier report did not preserve the complete static contract."
    }

    $expectedBuilds = if ($SkipBuild) { 0 } else { 2 }
    if ($Report.summary.builds -ne $expectedBuilds -or
        $Report.summary.buildsPassed -ne $expectedBuilds) {
        throw "$Mode verifier report has an unexpected build result."
    }
}

function New-ScenarioSummary([string] $Name, [object] $Report, [hashtable] $Details) {
    return [ordered]@{
        name = $Name
        mode = [string] $Report.mode
        status = [string] $Report.status
        projectRevision = [string] $Report.revisions.project
        engineRevision = [string] $Report.revisions.engine
        projectDirty = [bool] $Report.revisions.projectDirty
        engineDirty = [bool] $Report.revisions.engineDirty
        officialSeriesVerified = [bool] $Report.revisions.officialSeriesVerified
        capabilities = [int] $Report.summary.capabilities
        probes = [int] $Report.summary.probes
        probesPassed = [int] $Report.summary.probesPassed
        builds = [int] $Report.summary.builds
        buildsPassed = [int] $Report.summary.buildsPassed
        warnings = @($Report.warnings)
        details = $Details
    }
}

function Remove-OwnedWorkRoot([string] $Target, [string] $ExpectedRunId) {
    $resolvedWorkRoot = [System.IO.Path]::GetFullPath($Target)
    if (-not $resolvedWorkRoot.StartsWith($temporaryPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        -not [System.IO.Path]::GetFileName($resolvedWorkRoot).StartsWith("wtz-zpr-", [System.StringComparison]::Ordinal)) {
        throw "Refusing to remove unsafe rehearsal path: $resolvedWorkRoot"
    }

    $markerPath = Join-Path $resolvedWorkRoot ".wtz-port-rehearsal-owner"
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        throw "Refusing to remove unowned rehearsal path: $resolvedWorkRoot"
    }

    $marker = [System.IO.File]::ReadAllText($markerPath)
    if ($marker -ne $ExpectedRunId) {
        throw "Refusing to remove rehearsal path with a mismatched ownership marker."
    }

    Remove-Item -LiteralPath $resolvedWorkRoot -Recurse -Force
}

try {
    $removedStaleWorktrees = 0
    foreach ($previousRun in @(Get-ChildItem -LiteralPath $OutputDirectory -Directory)) {
        $previousReportPath = Join-Path $previousRun.FullName "zlevel-port-rehearsal.json"
        if (-not (Test-Path -LiteralPath $previousReportPath -PathType Leaf)) {
            continue
        }

        $previousReport = Get-Content -LiteralPath $previousReportPath -Raw | ConvertFrom-Json
        if ($previousReport.status -eq "Failed" -and
            -not $previousReport.work.removed -and
            [string] $previousReport.work.path -ne $workRoot -and
            (Test-Path -LiteralPath ([string] $previousReport.work.path) -PathType Container)) {
            Remove-OwnedWorkRoot ([string] $previousReport.work.path) ([string] $previousReport.runId)
            $removedStaleWorktrees++
        }
    }

    if ($removedStaleWorktrees -gt 0) {
        $warnings.Add("Removed $removedStaleWorktrees previously failed marked rehearsal worktree(s).") | Out-Null
    }

    $sourceProjectRevision = Get-GitText $ProjectRoot @("rev-parse", "HEAD") `
        "Source project revision lookup"
    $sourceEngineRevision = Get-GitText $EngineRoot @("rev-parse", "HEAD") `
        "Source engine revision lookup"
    $sourceProjectStatus = Get-GitText $ProjectRoot @("status", "--porcelain") `
        "Source project status lookup"
    $sourceEngineStatus = Get-GitText $EngineRoot @("status", "--porcelain") `
        "Source engine status lookup"
    if (-not $AllowDirtySourceForDevelopment) {
        Assert-CleanRepository $ProjectRoot "Source WTZ Project"
        Assert-CleanRepository $EngineRoot "Source WTZ Engine"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($sourceProjectStatus) -or
        -not [string]::IsNullOrWhiteSpace($sourceEngineStatus)) {
        $warnings.Add("Dirty source was accepted only because -AllowDirtySourceForDevelopment is active.") | Out-Null
    }

    $preflightOutput = Join-Path $runDirectory "preflight"
    $preflightArguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", (Join-Path $ProjectRoot "Tools\verify_zlevel_port.ps1"),
        "-Mode", "Paired",
        "-ProjectRoot", $ProjectRoot,
        "-EngineRoot", $EngineRoot,
        "-ManifestPath", $ManifestPath,
        "-OutputDirectory", $preflightOutput,
        "-SkipBuild"
    )
    if (-not $AllowDirtySourceForDevelopment) {
        $preflightArguments += "-RequireClean"
    }

    & powershell @preflightArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Official-pair preflight failed with exit code $LASTEXITCODE."
    }

    $pairedProject = Join-Path $workRoot "a"
    $pairedEngine = Join-Path $pairedProject ([string] $manifest.project.submodulePath)
    New-IsolatedClone $ProjectRoot $pairedProject $sourceProjectRevision $false "Paired project"
    New-IsolatedClone $EngineRoot $pairedEngine $sourceEngineRevision $false "Paired engine"
    Initialize-SubmodulesFromSource $pairedEngine $EngineRoot $false
    Assert-CleanRepository $pairedEngine "Paired WTZ Engine"
    Assert-CleanRepository $pairedProject "Paired WTZ Project"

    $pairedReport = Invoke-PortVerifier "paired" "Paired" $pairedProject $pairedEngine
    $pairedProjectRevision = Get-GitText $pairedProject @("rev-parse", "HEAD") `
        "Paired project revision lookup"
    $pairedEngineRevision = Get-GitText $pairedEngine @("rev-parse", "HEAD") `
        "Paired engine revision lookup"
    Assert-VerifierReport $pairedReport "Paired" $pairedProjectRevision $pairedEngineRevision
    if (-not $pairedReport.revisions.officialSeriesVerified) {
        throw "Paired rehearsal did not verify the official engine series."
    }

    Assert-CleanRepository $pairedEngine "Paired WTZ Engine after verification"
    Assert-CleanRepository $pairedProject "Paired WTZ Project after verification"

    $scenarioReports.Add((New-ScenarioSummary "paired-clean-clone" $pairedReport @{
        fullProjectHistory = $true
        fullEngineHistory = $true
        sharedLocalObjectStore = $true
        exactProjectRevision = $pairedProjectRevision -eq $sourceProjectRevision
        exactEngineRevision = $pairedEngineRevision -eq $sourceEngineRevision
    })) | Out-Null

    $portableProject = Join-Path $workRoot "b"
    $portableEngine = Join-Path $portableProject ([string] $manifest.project.submodulePath)
    New-IsolatedClone $ProjectRoot $portableProject $sourceProjectRevision $true "Portable project"
    New-IsolatedClone $EngineRoot $portableEngine $sourceEngineRevision $true "Portable engine"
    Initialize-SubmodulesFromSource $portableEngine $EngineRoot $true

    $null = Invoke-Git $portableEngine @("config", "user.name", "WTZ Port Rehearsal") `
        "Portable engine author configuration"
    $null = Invoke-Git $portableEngine @("config", "user.email", "wtz-port-rehearsal@invalid.local") `
        "Portable engine email configuration"
    $null = Invoke-Git $portableEngine @(
        "commit", "--quiet", "--allow-empty", "--no-gpg-sign",
        "-m", "Create portable WTZ Engine rehearsal head") `
        "Portable engine head creation"
    $portableEngineRevision = Get-GitText $portableEngine @("rev-parse", "HEAD") `
        "Portable engine revision lookup"

    $null = Invoke-Git $portableProject @("config", "user.name", "WTZ Port Rehearsal") `
        "Portable project author configuration"
    $null = Invoke-Git $portableProject @("config", "user.email", "wtz-port-rehearsal@invalid.local") `
        "Portable project email configuration"
    $null = Invoke-Git $portableProject @("add", "--", [string] $manifest.project.submodulePath) `
        "Portable project gitlink staging"
    $null = Invoke-Git $portableProject @(
        "commit", "--quiet", "--no-gpg-sign",
        "-m", "Create portable WTZ Project rehearsal head") `
        "Portable project head creation"
    $portableProjectRevision = Get-GitText $portableProject @("rev-parse", "HEAD") `
        "Portable project revision lookup"

    Assert-CleanRepository $portableEngine "Portable WTZ Engine"
    Assert-CleanRepository $portableProject "Portable WTZ Project"
    if ($portableProjectRevision -eq $sourceProjectRevision -or
        $portableEngineRevision -eq $sourceEngineRevision) {
        throw "Portable rehearsal did not create distinct project and engine heads."
    }

    $minimumRevision = [string] $manifest.project.minimumRevision
    $minimumLookup = Invoke-GitResult $portableProject @("cat-file", "-e", "$minimumRevision`^{commit}")
    if ($minimumLookup.ExitCode -eq 0) {
        throw "Portable shallow project unexpectedly contains the official minimum revision."
    }

    $engineBaseRevision = [string] $manifest.engine.upstreamBase.revision
    $engineBaseLookup = Invoke-GitResult $portableEngine @("cat-file", "-e", "$engineBaseRevision`^{commit}")
    if ($engineBaseLookup.ExitCode -eq 0) {
        throw "Portable shallow engine unexpectedly contains the official base revision."
    }

    $portableReport = Invoke-PortVerifier "portable" "Portable" $portableProject $portableEngine
    Assert-VerifierReport $portableReport "Portable" $portableProjectRevision $portableEngineRevision
    Assert-CleanRepository $portableEngine "Portable WTZ Engine after verification"
    Assert-CleanRepository $portableProject "Portable WTZ Project after verification"
    $engineWarning = @($portableReport.warnings | Where-Object {
        $_ -match "Portable mode accepts engine revision"
    }).Count -eq 1
    $historyWarning = @($portableReport.warnings | Where-Object {
        $_ -match "cannot resolve the official WTZ minimum revision"
    }).Count -eq 1
    if (-not $engineWarning -or -not $historyWarning) {
        throw "Portable rehearsal did not emit both expected history warnings."
    }

    $scenarioReports.Add((New-ScenarioSummary "portable-shallow-heads" $portableReport @{
        shallowProject = Test-Path -LiteralPath (Join-Path $portableProject ".git\shallow") -PathType Leaf
        shallowEngine = Test-Path -LiteralPath (Join-Path $portableEngine ".git\shallow") -PathType Leaf
        officialProjectMinimumAvailable = $false
        officialEngineBaseAvailable = $false
        distinctProjectHead = $true
        distinctEngineHead = $true
        expectedEngineWarning = $engineWarning
        expectedHistoryWarning = $historyWarning
    })) | Out-Null

    if ((Get-GitText $ProjectRoot @("rev-parse", "HEAD") "Source project post-check") -ne
        $sourceProjectRevision) {
        throw "Source WTZ Project revision changed during the rehearsal."
    }

    if ((Get-GitText $EngineRoot @("rev-parse", "HEAD") "Source engine post-check") -ne
        $sourceEngineRevision) {
        throw "Source WTZ Engine revision changed during the rehearsal."
    }

    $postProjectStatus = Get-GitText $ProjectRoot @("status", "--porcelain") `
        "Source project post-status lookup"
    $postEngineStatus = Get-GitText $EngineRoot @("status", "--porcelain") `
        "Source engine post-status lookup"
    if ($postProjectStatus -ne $sourceProjectStatus -or $postEngineStatus -ne $sourceEngineStatus) {
        throw "Source worktree status changed during the rehearsal."
    }

    if (-not $AllowDirtySourceForDevelopment) {
        Assert-CleanRepository $ProjectRoot "Source WTZ Project after rehearsal"
        Assert-CleanRepository $EngineRoot "Source WTZ Engine after rehearsal"
    }

    $status = "Passed"
}
catch {
    $failures.Add($_.Exception.Message) | Out-Null
    Write-Host "Z-level port rehearsal failed: $($_.Exception.Message)" -ForegroundColor Red
}
finally {
    $stopwatch.Stop()
    if (-not $KeepWorktrees) {
        try {
            Remove-OwnedWorkRoot $workRoot $runId
            $worktreesRemoved = $true
        }
        catch {
            $failures.Add("Worktree cleanup failed: $($_.Exception.Message)") | Out-Null
            $status = "Failed"
        }
    }
    elseif ($KeepWorktrees) {
        $warnings.Add("Rehearsal worktrees were retained by request at '$workRoot'.") | Out-Null
    }

    $report = [ordered]@{
        schemaVersion = 1
        contractVersion = "WTZ-PORT-REHEARSAL-1"
        generatedAtUtc = [DateTime]::UtcNow.ToString("O")
        runId = $runId
        status = $status
        configuration = $Configuration
        skipBuild = [bool] $SkipBuild
        noRestore = [bool] $NoRestore
        elapsedMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
        source = [ordered]@{
            projectRoot = $ProjectRoot
            engineRoot = $EngineRoot
            projectRevision = $sourceProjectRevision
            engineRevision = $sourceEngineRevision
            projectDirty = -not [string]::IsNullOrWhiteSpace($sourceProjectStatus)
            engineDirty = -not [string]::IsNullOrWhiteSpace($sourceEngineStatus)
            dirtySourceAllowedForDevelopment = [bool] $AllowDirtySourceForDevelopment
        }
        work = [ordered]@{
            path = $workRoot
            removed = $worktreesRemoved
            retained = -not $worktreesRemoved
        }
        summary = [ordered]@{
            scenarios = $scenarioReports.Count
            scenariosPassed = @($scenarioReports | Where-Object status -EQ "Passed").Count
            warnings = $warnings.Count
            failures = $failures.Count
        }
        scenarios = @($scenarioReports)
        warnings = @($warnings)
        failures = @($failures)
    }

    $json = $report | ConvertTo-Json -Depth 12
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $reportPath = Join-Path $runDirectory "zlevel-port-rehearsal.json"
    [System.IO.File]::WriteAllText($reportPath, $json, $utf8)
    [System.IO.File]::WriteAllText(
        (Join-Path $OutputDirectory "zlevel-port-rehearsal-latest.json"),
        $json,
        $utf8)
}

if ($status -ne "Passed") {
    throw "Z-level port rehearsal failed with $($failures.Count) issue(s). Report: $reportPath"
}

Write-Host "Z-level port rehearsal passed."
Write-Host "  scenarios=$($scenarioReports.Count)/$($scenarioReports.Count), configuration=$Configuration, worktreesRemoved=$worktreesRemoved"
Write-Host "  report=$reportPath"
