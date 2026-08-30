[CmdletBinding()]
param(
    [string] $ManifestPath,

    [string] $OutputDirectory,

    [switch] $ValidateOnly,

    [switch] $NoRestore,

    [switch] $AllowDirtySourceForDevelopment,

    [switch] $SkipBuildForDevelopment,

    [switch] $SkipVisualCaptureForDevelopment,

    [ValidateRange(30, 600)]
    [int] $VisualTimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repoPrefix = $repoRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot "Docs\ZLevelReleaseManifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-release"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $ManifestPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release manifest must be inside the repository: $ManifestPath"
}

if (-not $OutputDirectory.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release output must be inside the repository: $OutputDirectory"
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing Z-level release manifest: $ManifestPath"
}

function Resolve-RepositoryFile([string] $RelativePath, [string] $Description) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Description must be a non-empty repository-relative path: '$RelativePath'."
    }

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if (-not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description escapes the repository: '$RelativePath'."
    }

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Description does not exist: '$RelativePath'."
    }

    return $fullPath
}

function Assert-ExactSet(
    [string] $Description,
    [string[]] $Expected,
    [string[]] $Actual) {
    $expectedSet = @($Expected | Sort-Object -Unique)
    $actualSet = @($Actual | Sort-Object -Unique)
    $difference = @(Compare-Object -ReferenceObject $expectedSet -DifferenceObject $actualSet)
    if ($difference.Count -ne 0 -or $actualSet.Count -ne $Expected.Count) {
        $details = @($difference | ForEach-Object { "$($_.SideIndicator)$($_.InputObject)" }) -join ", "
        throw "$Description does not match the protected set. Difference: $details"
    }
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

function Test-GitAncestor([string] $Root, [string] $Ancestor, [string] $Descendant) {
    $result = Invoke-GitResult $Root @("merge-base", "--is-ancestor", $Ancestor, $Descendant)
    if ($result.ExitCode -eq 0) {
        return $true
    }

    if ($result.ExitCode -eq 1) {
        return $false
    }

    throw "Unable to compare Git revisions '$Ancestor' and '$Descendant': $($result.Output)"
}

$expectedDomains = @(
    "atmosphere",
    "combat",
    "construction",
    "elevators",
    "flight",
    "interaction",
    "lighting",
    "mapping-lifecycle",
    "mapping-placement",
    "mapping-protocol",
    "movement-gravity",
    "navigation-ai",
    "persistence-autosave",
    "persistence-roundtrip",
    "persistence-snapshot",
    "sound",
    "traversal",
    "visibility-rendering",
    "weather"
)

$expectedEntryIds = @(
    "atmosphere.pipe-network-isolation",
    "atmosphere.vertical-gas-flow",
    "combat.explosion-three-floor",
    "combat.projectile-authority",
    "combat.vertical-hitscan",
    "construction.vertical-surfaces",
    "elevators.ai-call-and-ride",
    "elevators.passenger-move",
    "flight.gravity-restoration",
    "flight.open-boundary-moving-frame",
    "interaction.admin-crowbar-floor",
    "interaction.vertical-boundary-authority",
    "lighting.aperture-moving-frame",
    "lighting.shadow-order",
    "mapping-lifecycle.initialized-floor-mutations",
    "mapping-lifecycle.official-map-load",
    "mapping-placement.entity-floor-authority",
    "mapping-placement.tile-floor-authority",
    "mapping-protocol.correlated-save",
    "mapping-protocol.pending-request",
    "movement-gravity.generator-target",
    "movement-gravity.no-source-no-fall",
    "movement-gravity.opening-fall",
    "navigation-ai.cross-floor-route",
    "navigation-ai.flight-corridor",
    "persistence-autosave.client-utf8",
    "persistence-autosave.initialized-atomic",
    "persistence-autosave.server-file-promotion",
    "persistence-roundtrip.initialized-double-cycle",
    "persistence-roundtrip.versioned-map",
    "persistence-roundtrip.vertical-content",
    "persistence-snapshot.transient-filtering",
    "sound.pressure-aware-route",
    "sound.unauthorized-audio-mute",
    "traversal.adjacent-support",
    "traversal.step-trigger",
    "visibility-rendering.pvs-openings",
    "visibility-rendering.tile-moving-frame",
    "visibility-rendering.viewer-floor-refresh",
    "weather.active-floor-mask",
    "weather.configured-exposure"
)

$expectedProjects = @(
    "Content.IntegrationTests/Content.IntegrationTests.csproj",
    "Content.Tests/Content.Tests.csproj"
)

$expectedEngineRevision = "7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2"
$expectedGateDefinitions = @{
    "port-pairing" = @{
        Contract = "WTZ-PORT-1"
        Script = "Tools/verify_zlevel_port.ps1"
        Report = "zlevel-port-compatibility.json"
    }
    "real-client-visual" = @{
        Contract = "WTZ-VISUAL-1"
        Script = "Tools/run_zlevel_visual_capture.ps1"
        Report = "zlevel-visual-capture-gate.json"
    }
    "z-zero" = @{
        Contract = "WTZ-Z0-1"
        Script = "Tools/run_zlevel_z0_compatibility.ps1"
        Report = "zlevel-z0-compatibility.json"
    }
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) {
    throw "Unsupported Z-level release manifest schema: $($manifest.schemaVersion)."
}

if ([string] $manifest.contractVersion -ne "WTZ-RELEASE-1") {
    throw "Unsupported Z-level release contract: '$($manifest.contractVersion)'."
}

if ([string] $manifest.configuration -ne "Release") {
    throw "WTZ-RELEASE-1 requires configuration 'Release'."
}

$shaPattern = '^[0-9a-f]{40}$'
$minimumProjectRevision = [string] $manifest.sourcePolicy.minimumProjectRevision
$declaredEngineRevision = [string] $manifest.sourcePolicy.engineRevision
if ($minimumProjectRevision -notmatch $shaPattern) {
    throw "Release minimumProjectRevision is not a full lowercase Git revision."
}

if ($declaredEngineRevision -ne $expectedEngineRevision) {
    throw "Release engine revision '$declaredEngineRevision' does not match the protected WTZ Engine revision."
}

if ([string] $manifest.sourcePolicy.engineSubmodulePath -ne "RobustToolbox") {
    throw "Release engineSubmodulePath must be 'RobustToolbox'."
}

if ($manifest.sourcePolicy.requireCleanProject -ne $true -or
    $manifest.sourcePolicy.requireCleanEngine -ne $true) {
    throw "WTZ-RELEASE-1 must require clean project and engine worktrees."
}

Assert-ExactSet "Release build target set" @("SpaceStation14.slnx") @(
    $manifest.buildTargets | ForEach-Object { [string] $_ })
$buildTarget = Resolve-RepositoryFile ([string] $manifest.buildTargets[0]) "Release build target"

$declaredDomains = @($manifest.requiredDomains | ForEach-Object { [string] $_ })
Assert-ExactSet "Release requiredDomains" $expectedDomains $declaredDomains

$entries = @($manifest.entries)
if ($entries.Count -ne $expectedEntryIds.Count) {
    throw "Release manifest has $($entries.Count) entries; expected $($expectedEntryIds.Count)."
}

Assert-ExactSet "Release entry id set" $expectedEntryIds @(
    $entries | ForEach-Object { [string] $_.id })

$ids = @{}
$tests = @{}
$validatedEntries = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $entries) {
    $id = [string] $entry.id
    $domain = [string] $entry.domain
    $contract = [string] $entry.contract
    $project = ([string] $entry.project).Replace("\", "/")
    $fullyQualifiedTest = [string] $entry.fullyQualifiedTest

    if ($id -notmatch '^[a-z0-9]+(?:[.-][a-z0-9]+)*$') {
        throw "Invalid release entry id: '$id'."
    }

    if ($ids.ContainsKey($id)) {
        throw "Duplicate release entry id: '$id'."
    }

    if ($expectedDomains -notcontains $domain) {
        throw "Release entry '$id' uses unknown domain '$domain'."
    }

    if ([string]::IsNullOrWhiteSpace($contract)) {
        throw "Release entry '$id' has an empty contract."
    }

    if ($expectedProjects -notcontains $project) {
        throw "Release entry '$id' uses unprotected test project '$project'."
    }

    if ($fullyQualifiedTest -notmatch '^[A-Za-z_][A-Za-z0-9_.]+$') {
        throw "Release entry '$id' has an invalid fully-qualified test name."
    }

    if ($tests.ContainsKey($fullyQualifiedTest)) {
        throw "A test may protect only one release entry; duplicate: '$fullyQualifiedTest'."
    }

    $projectPath = Resolve-RepositoryFile $project "Release test project"
    $ids.Add($id, $true)
    $tests.Add($fullyQualifiedTest, $true)
    $validatedEntries.Add([pscustomobject]@{
        Id = $id
        Domain = $domain
        Contract = $contract
        Project = $project
        ProjectPath = $projectPath
        FullyQualifiedTest = $fullyQualifiedTest
    }) | Out-Null
}

foreach ($domain in $expectedDomains) {
    if (-not ($validatedEntries | Where-Object Domain -EQ $domain)) {
        throw "Protected release domain '$domain' has no test entry."
    }
}

$compositeGates = @($manifest.compositeGates)
Assert-ExactSet "Release composite gate id set" @($expectedGateDefinitions.Keys) @(
    $compositeGates | ForEach-Object { [string] $_.id })

$validatedGates = @{}
foreach ($gate in $compositeGates) {
    $id = [string] $gate.id
    $expected = $expectedGateDefinitions[$id]
    if ([string] $gate.contractVersion -ne $expected.Contract -or
        ([string] $gate.script).Replace("\", "/") -ne $expected.Script -or
        [string] $gate.report -ne $expected.Report) {
        throw "Composite gate '$id' does not match its protected contract, script, and report definition."
    }

    $scriptPath = Resolve-RepositoryFile ([string] $gate.script) "Composite gate script"
    if ($id -eq "real-client-visual" -and
        ($gate.expectedCaptures -ne 15 -or $gate.expectedChecks -ne 24)) {
        throw "The real-client visual gate must require exactly 15 captures and 24 checks."
    }

    $validatedGates[$id] = [pscustomobject]@{
        Id = $id
        ContractVersion = [string] $gate.contractVersion
        ScriptPath = $scriptPath
        ReportName = [string] $gate.report
        ExpectedCaptures = if ($id -eq "real-client-visual") { [int] $gate.expectedCaptures } else { 0 }
        ExpectedChecks = if ($id -eq "real-client-visual") { [int] $gate.expectedChecks } else { 0 }
    }
}

if ($ValidateOnly) {
    Write-Host "WTZ-RELEASE-1 manifest validation passed."
    Write-Host "  domains=$($expectedDomains.Count), tests=$($validatedEntries.Count), composite-gates=$($validatedGates.Count)"
    return
}

$engineRoot = Join-Path $repoRoot "RobustToolbox"
$projectRevision = Get-GitText $repoRoot @("rev-parse", "HEAD") "WTZ Project revision lookup"
$engineRevision = Get-GitText $engineRoot @("rev-parse", "HEAD") "WTZ Engine revision lookup"
$projectStatus = Get-GitText $repoRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Project worktree lookup"
$engineStatus = Get-GitText $engineRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Engine worktree lookup"
$projectDirty = -not [string]::IsNullOrWhiteSpace($projectStatus)
$engineDirty = -not [string]::IsNullOrWhiteSpace($engineStatus)

if (-not (Test-GitAncestor $repoRoot $minimumProjectRevision $projectRevision)) {
    throw "Project revision '$projectRevision' does not contain release minimum '$minimumProjectRevision'."
}

if ($engineRevision -ne $declaredEngineRevision) {
    throw "Engine checkout '$engineRevision' does not match release revision '$declaredEngineRevision'."
}

$gitlinkText = Get-GitText $repoRoot @("ls-tree", "HEAD", "--", "RobustToolbox") `
    "WTZ Engine gitlink lookup"
if ($gitlinkText -notmatch '^160000 commit ([0-9a-f]{40})\s+RobustToolbox$') {
    throw "Unable to parse the RobustToolbox submodule gitlink."
}

$gitlinkRevision = $Matches[1]
if ($gitlinkRevision -ne $engineRevision) {
    throw "Engine gitlink '$gitlinkRevision' and checkout '$engineRevision' are not paired."
}

if (($projectDirty -or $engineDirty) -and -not $AllowDirtySourceForDevelopment) {
    throw "WTZ-RELEASE-1 requires clean project and engine worktrees. Use -AllowDirtySourceForDevelopment only while developing the gate."
}

$developmentRun = $AllowDirtySourceForDevelopment -or
    $SkipBuildForDevelopment -or
    $SkipVisualCaptureForDevelopment
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$runId = "{0}-{1}-{2}" -f
    [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"),
    $PID,
    [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDirectory = Join-Path $OutputDirectory $runId
[System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$reportPath = Join-Path $runDirectory "zlevel-release.json"
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$buildReports = [System.Collections.Generic.List[object]]::new()
$projectReports = [System.Collections.Generic.List[object]]::new()
$verifiedTests = [System.Collections.Generic.List[object]]::new()
$gateReports = [System.Collections.Generic.List[object]]::new()
$failure = $null

function Invoke-ReleaseBuild {
    if ($SkipBuildForDevelopment) {
        $buildReports.Add([ordered]@{
            target = "SpaceStation14.slnx"
            configuration = "Release"
            status = "SkippedForDevelopment"
            elapsedMilliseconds = 0
        }) | Out-Null
        return
    }

    $timer = [System.Diagnostics.Stopwatch]::StartNew()
    $arguments = @(
        "build",
        $buildTarget,
        "--configuration", "Release",
        "--no-incremental",
        "--nologo",
        "--verbosity", "minimal"
    )
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    Write-Host "Building the complete WTZ solution in Release..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "WTZ Release build failed with exit code $LASTEXITCODE."
    }

    $timer.Stop()
    $buildReports.Add([ordered]@{
        target = "SpaceStation14.slnx"
        configuration = "Release"
        status = "Passed"
        elapsedMilliseconds = $timer.Elapsed.TotalMilliseconds
    }) | Out-Null
}

function Invoke-ExactReleaseTests {
    $testDirectory = Join-Path $runDirectory "exact-tests"
    [System.IO.Directory]::CreateDirectory($testDirectory) | Out-Null
    $groups = @($validatedEntries | Group-Object Project | Sort-Object Name)
    foreach ($group in $groups) {
        $groupEntries = @($group.Group)
        $projectPath = $groupEntries[0].ProjectPath
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
        $trxName = "wtz-release-$projectName.trx"
        $trxPath = Join-Path $testDirectory $trxName
        $filter = @($groupEntries | ForEach-Object {
            "FullyQualifiedName=$($_.FullyQualifiedTest)"
        }) -join "|"

        $arguments = @(
            "test",
            $projectPath,
            "--configuration", "Release",
            "--filter", $filter,
            "--logger", "trx;LogFileName=$trxName",
            "--results-directory", $testDirectory,
            "--no-build",
            "--no-restore",
            "--nologo"
        )

        Write-Host "Running $($groupEntries.Count) exact release contracts in $($group.Name)..."
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Exact release tests failed for '$($group.Name)' with exit code $LASTEXITCODE."
        }

        if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
            throw "Missing exact release TRX for '$($group.Name)': $trxPath"
        }

        [xml] $trx = Get-Content -LiteralPath $trxPath -Raw
        $definitionsById = @{}
        foreach ($definition in @($trx.SelectNodes("//*[local-name()='TestDefinitions']/*[local-name()='UnitTest']"))) {
            $method = $definition.SelectSingleNode("*[local-name()='TestMethod']")
            if ($null -eq $method) {
                continue
            }

            $testId = $definition.GetAttribute("id")
            $testName = "$($method.GetAttribute('className')).$($method.GetAttribute('name'))"
            $definitionsById[$testId] = $testName
        }

        $outcomes = @{}
        foreach ($result in @($trx.SelectNodes("//*[local-name()='Results']/*[local-name()='UnitTestResult']"))) {
            $testId = $result.GetAttribute("testId")
            if (-not $definitionsById.ContainsKey($testId)) {
                throw "TRX result references unknown test id '$testId' in '$trxName'."
            }

            $testName = $definitionsById[$testId]
            if ($outcomes.ContainsKey($testName)) {
                throw "TRX contains duplicate result for '$testName'."
            }

            $outcomes[$testName] = $result.GetAttribute("outcome")
        }

        $expectedGroupTests = @{}
        foreach ($entry in $groupEntries) {
            $expectedGroupTests[$entry.FullyQualifiedTest] = $entry
            if (-not $outcomes.ContainsKey($entry.FullyQualifiedTest)) {
                throw "Declared release test was not discovered: '$($entry.FullyQualifiedTest)'."
            }

            if ($outcomes[$entry.FullyQualifiedTest] -ne "Passed") {
                throw "Release test '$($entry.FullyQualifiedTest)' completed as '$($outcomes[$entry.FullyQualifiedTest])'."
            }

            $verifiedTests.Add([ordered]@{
                id = $entry.Id
                domain = $entry.Domain
                project = $entry.Project
                fullyQualifiedTest = $entry.FullyQualifiedTest
                outcome = "Passed"
            }) | Out-Null
        }

        foreach ($actualTest in $outcomes.Keys) {
            if (-not $expectedGroupTests.ContainsKey($actualTest)) {
                throw "Release filter executed an undeclared test: '$actualTest'."
            }
        }

        if ($outcomes.Count -ne $groupEntries.Count) {
            throw "TRX result count for '$($group.Name)' is $($outcomes.Count); expected $($groupEntries.Count)."
        }

        $projectReports.Add([ordered]@{
            project = $group.Name
            expected = $groupEntries.Count
            executed = $outcomes.Count
            passed = @($outcomes.Values | Where-Object { $_ -eq "Passed" }).Count
            trx = "exact-tests/$trxName"
            trxSha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }) | Out-Null
    }
}

function Invoke-ChildGate([string] $Id, [string[]] $Arguments) {
    $gate = $validatedGates[$Id]
    $gateDirectory = Join-Path $runDirectory $Id
    [System.IO.Directory]::CreateDirectory($gateDirectory) | Out-Null
    $commandArguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $gate.ScriptPath
    ) + $Arguments

    $childOutput = @(& powershell @commandArguments 2>&1)
    $childExitCode = $LASTEXITCODE
    foreach ($line in $childOutput) {
        Write-Host $line
    }

    if ($childExitCode -ne 0) {
        throw "Composite release gate '$Id' failed with exit code $childExitCode."
    }

    $childReportPath = Join-Path $gateDirectory $gate.ReportName
    if (-not (Test-Path -LiteralPath $childReportPath -PathType Leaf)) {
        throw "Composite release gate '$Id' did not produce '$childReportPath'."
    }

    return [pscustomobject]@{
        Gate = $gate
        Directory = $gateDirectory
        ReportPath = $childReportPath
        Report = Get-Content -LiteralPath $childReportPath -Raw | ConvertFrom-Json
    }
}

function Invoke-CompositeReleaseGates {
    $zZeroDirectory = Join-Path $runDirectory "z-zero"
    $zZero = Invoke-ChildGate "z-zero" @(
        "-Configuration", "Release",
        "-OutputDirectory", $zZeroDirectory,
        "-NoBuild"
    )
    if ($zZero.Report.schemaVersion -ne 1 -or
        $zZero.Report.contractVersion -ne "WTZ-Z0-1" -or
        $zZero.Report.configuration -ne "Release" -or
        $zZero.Report.revisions.project -ne $projectRevision -or
        $zZero.Report.revisions.engine -ne $engineRevision -or
        $zZero.Report.summary.declared -ne $zZero.Report.summary.passed -or
        $zZero.Report.summary.executed -ne $zZero.Report.summary.passed) {
        throw "The Z 0 child report does not satisfy WTZ-RELEASE-1."
    }

    $gateReports.Add([ordered]@{
        id = "z-zero"
        contractVersion = "WTZ-Z0-1"
        status = "Passed"
        report = "z-zero/$($zZero.Gate.ReportName)"
        reportSha256 = (Get-FileHash -LiteralPath $zZero.ReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
        checks = [int] $zZero.Report.summary.passed
    }) | Out-Null

    $portDirectory = Join-Path $runDirectory "port-pairing"
    $portArguments = @(
        "-Mode", "Paired",
        "-ProjectRoot", $repoRoot,
        "-EngineRoot", $engineRoot,
        "-OutputDirectory", $portDirectory,
        "-Configuration", "Release",
        "-SkipBuild",
        "-NoRestore"
    )
    if (-not $AllowDirtySourceForDevelopment) {
        $portArguments += "-RequireClean"
    }

    $port = Invoke-ChildGate "port-pairing" $portArguments
    if ($port.Report.schemaVersion -ne 1 -or
        $port.Report.contractVersion -ne "WTZ-PORT-1" -or
        $port.Report.status -ne "Passed" -or
        $port.Report.mode -ne "Paired" -or
        $port.Report.configuration -ne "Release" -or
        $port.Report.revisions.project -ne $projectRevision -or
        $port.Report.revisions.engine -ne $engineRevision -or
        $port.Report.revisions.officialSeriesVerified -ne $true -or
        $port.Report.summary.probes -ne $port.Report.summary.probesPassed) {
        throw "The port-pairing child report does not satisfy WTZ-RELEASE-1."
    }

    if (-not $AllowDirtySourceForDevelopment -and
        ($port.Report.revisions.projectDirty -or $port.Report.revisions.engineDirty)) {
        throw "The strict port-pairing report observed a dirty source tree."
    }

    $gateReports.Add([ordered]@{
        id = "port-pairing"
        contractVersion = "WTZ-PORT-1"
        status = "Passed"
        report = "port-pairing/$($port.Gate.ReportName)"
        reportSha256 = (Get-FileHash -LiteralPath $port.ReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
        checks = [int] $port.Report.summary.probesPassed
    }) | Out-Null

    if ($SkipVisualCaptureForDevelopment) {
        $gateReports.Add([ordered]@{
            id = "real-client-visual"
            contractVersion = "WTZ-VISUAL-1"
            status = "SkippedForDevelopment"
            report = $null
            reportSha256 = $null
            checks = 0
        }) | Out-Null
        return
    }

    $visualDirectory = Join-Path $runDirectory "real-client-visual"
    $visual = Invoke-ChildGate "real-client-visual" @(
        "-Configuration", "Release",
        "-OutputDirectory", $visualDirectory,
        "-TimeoutSeconds", $VisualTimeoutSeconds,
        "-SkipBuild"
    )
    if ($visual.Report.schemaVersion -ne 1 -or
        $visual.Report.contractVersion -ne "WTZ-VISUAL-1" -or
        $visual.Report.status -ne "Passed" -or
        $visual.Report.configuration -ne "Release" -or
        $visual.Report.revisions.project -ne $projectRevision -or
        $visual.Report.revisions.engine -ne $engineRevision -or
        $visual.Report.summary.captures -ne $visual.Gate.ExpectedCaptures -or
        $visual.Report.summary.checks -ne $visual.Gate.ExpectedChecks -or
        $visual.Report.summary.passed -ne $visual.Gate.ExpectedChecks) {
        throw "The real-client visual report does not satisfy WTZ-RELEASE-1."
    }

    $gateReports.Add([ordered]@{
        id = "real-client-visual"
        contractVersion = "WTZ-VISUAL-1"
        status = "Passed"
        report = "real-client-visual/$($visual.Gate.ReportName)"
        reportSha256 = (Get-FileHash -LiteralPath $visual.ReportPath -Algorithm SHA256).Hash.ToLowerInvariant()
        checks = [int] $visual.Report.summary.passed
    }) | Out-Null
}

try {
    Invoke-ReleaseBuild
    Invoke-ExactReleaseTests
    Invoke-CompositeReleaseGates
}
catch {
    $failure = $_.Exception.Message
}

$stopwatch.Stop()
$passedTests = @($verifiedTests | Where-Object outcome -EQ "Passed").Count
$passedGates = @($gateReports | Where-Object status -EQ "Passed").Count
$status = if ($null -ne $failure) {
    "Failed"
}
elseif ($developmentRun) {
    "DevelopmentPassed"
}
else {
    "Passed"
}

$manifestRelativePath = $ManifestPath.Substring($repoPrefix.Length).Replace("\", "/")
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = "WTZ-RELEASE-1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = $status
    configuration = "Release"
    durationMilliseconds = $stopwatch.Elapsed.TotalMilliseconds
    manifest = [ordered]@{
        path = $manifestRelativePath
        sha256 = (Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    revisions = [ordered]@{
        project = $projectRevision
        engine = $engineRevision
        gitlink = $gitlinkRevision
        minimumProject = $minimumProjectRevision
        projectDirty = $projectDirty
        engineDirty = $engineDirty
    }
    development = [ordered]@{
        allowDirtySource = [bool] $AllowDirtySourceForDevelopment
        buildSkipped = [bool] $SkipBuildForDevelopment
        visualCaptureSkipped = [bool] $SkipVisualCaptureForDevelopment
    }
    summary = [ordered]@{
        domains = $expectedDomains.Count
        declaredTests = $validatedEntries.Count
        executedTests = $verifiedTests.Count
        passedTests = $passedTests
        requiredCompositeGates = $validatedGates.Count
        passedCompositeGates = $passedGates
    }
    builds = @($buildReports)
    projects = @($projectReports)
    tests = @($verifiedTests)
    compositeGates = @($gateReports)
    failure = $failure
}

[System.IO.File]::WriteAllText(
    $reportPath,
    ($report | ConvertTo-Json -Depth 12),
    [System.Text.UTF8Encoding]::new($false))

if ($null -ne $failure) {
    Write-Host "WTZ-RELEASE-1 failed: $failure" -ForegroundColor Red
    Write-Host "  report=$reportPath"
    throw "WTZ-RELEASE-1 failed."
}

Write-Host "WTZ-RELEASE-1 $status."
Write-Host "  domains=$($expectedDomains.Count), tests=$passedTests/$($validatedEntries.Count), composite-gates=$passedGates/$($validatedGates.Count)"
Write-Host "  project=$projectRevision"
Write-Host "  engine=$engineRevision"
Write-Host "  report=$reportPath"
