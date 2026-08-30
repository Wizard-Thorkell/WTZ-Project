[CmdletBinding()]
param(
    [string] $ManifestPath,

    [string] $OutputDirectory,

    [string] $PilotRecordPath,

    [switch] $ValidateOnly,

    [switch] $NoRestore,

    [switch] $AllowDirtySourceForDevelopment,

    [switch] $SkipBuildForDevelopment,

    [switch] $SkipVisualCaptureForDevelopment,

    [switch] $SkipPerformanceForDevelopment,

    [ValidateRange(30, 600)]
    [int] $VisualTimeoutSeconds = 240
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repoPrefix = $repoRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
$engineRoot = Join-Path $repoRoot "RobustToolbox"
$utf8 = [System.Text.UTF8Encoding]::new($false)

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $repoRoot "Docs\ZLevelFinalManifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-final"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not $ManifestPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Final manifest must be inside the repository: $ManifestPath"
}

if (-not $OutputDirectory.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Final-gate output must be inside the repository: $OutputDirectory"
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing Z-level final manifest: $ManifestPath"
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

function Assert-Equal($Actual, $Expected, [string] $Description) {
    if ($Actual -ne $Expected) {
        throw "$Description is '$Actual'; expected '$Expected'."
    }
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

function Get-RemoteRevision([string] $Remote, [string] $Branch) {
    $result = Invoke-GitResult $repoRoot @("ls-remote", "--heads", $Remote, $Branch)
    if ($result.ExitCode -ne 0) {
        throw "Remote branch lookup failed with exit code $($result.ExitCode): $($result.Output)"
    }

    $lines = @($result.Output -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($lines.Count -ne 1 -or $lines[0] -notmatch '^([0-9a-f]{40})\s+refs/heads/(.+)$') {
        throw "Remote branch lookup returned an unexpected result for '$Remote/$Branch'."
    }

    if ($Matches[2] -ne $Branch) {
        throw "Remote branch lookup returned '$($Matches[2])'; expected '$Branch'."
    }

    return $Matches[1]
}

function Invoke-PowerShellChild(
    [string] $Description,
    [string] $ScriptPath,
    [string[]] $Arguments) {
    $commandArguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $ScriptPath
    ) + $Arguments

    Write-Host "Running $Description..."
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        & powershell @commandArguments 2>&1 | ForEach-Object { Write-Host $_ }
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    if ($exitCode -ne 0) {
        throw "$Description failed with exit code $exitCode."
    }
}

function Get-OnlyReport([string] $Root, [string] $Name, [string] $Description) {
    $reports = @(
        Get-ChildItem -LiteralPath $Root -Directory | ForEach-Object {
            $candidate = Join-Path $_.FullName $Name
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                Get-Item -LiteralPath $candidate
            }
        }
    )
    if ($reports.Count -ne 1) {
        throw "$Description produced $($reports.Count) '$Name' reports; expected exactly one."
    }

    return $reports[0].FullName
}

function Get-RunRelativePath([string] $Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $runPrefix = $runDirectory.TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($runPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Artifact is outside the owned final-gate directory: $fullPath"
    }

    return $fullPath.Substring($runPrefix.Length).Replace("\", "/")
}

function Get-TrxOutcomes([string] $Path) {
    [xml] $trx = Get-Content -LiteralPath $Path -Raw
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
            throw "TRX result references unknown test id '$testId'."
        }

        $testName = $definitionsById[$testId]
        if ($outcomes.ContainsKey($testName)) {
            throw "TRX contains duplicate result for '$testName'."
        }

        $outcomes[$testName] = $result.GetAttribute("outcome")
    }

    return $outcomes
}

$expectedPhases = @("P0", "P1", "P2", "P3", "P4", "P5", "P6", "P7", "P8")
$expectedDocuments = @(
    "Docs/ZLevel.md",
    "Docs/ZLevelElevators.md",
    "Docs/ZLevelFlight.md",
    "Docs/ZLevelImplementationLedger.md",
    "Docs/ZLevelLighting.md",
    "Docs/ZLevelMapSaveLoad.md",
    "Docs/ZLevelOperations.md",
    "Docs/ZLevelPathfinding.md",
    "Docs/ZLevelPorting.md",
    "Docs/ZLevelProjectiles.md",
    "Docs/ZLevelRelease.md",
    "Docs/ZLevelServerHardening.md",
    "Docs/ZLevelSound.md",
    "Docs/ZLevelTrace.md",
    "Docs/ZLevelVerticalContent.md",
    "Docs/ZLevelZZeroCompatibility.md"
)
$expectedHealthTests = @(
    "Content.Tests.Server.ZLevelOperationalHealthTest.HealthySignalsHaveNoFindings",
    "Content.Tests.Server.ZLevelOperationalHealthTest.IntegrityAndFailOpenSignalsAreCritical",
    "Content.Tests.Server.ZLevelOperationalHealthTest.JsonOutputPreservesMachineReadableContract",
    "Content.Tests.Server.ZLevelOperationalHealthTest.RecoverableSignalsAreDegradedWithStableActions"
)
$expectedPilotChecks = @(
    "atmosphere-open-close",
    "combat-projectiles-explosions",
    "construction-deconstruction",
    "elevator-power-navigation",
    "gravity-fall-flight",
    "health-checkpoint-recovery",
    "join-latejoin-observer",
    "lighting-fov-weather",
    "mapping-save-reload",
    "round-start-map-load",
    "sound-vertical-sealed",
    "traversal-grouped-ladders"
)
$expectedExternalConditions = @(
    "dependency-advisory-review",
    "deployment-filesystem-recovery-drill",
    "human-multiplayer-pilot",
    "monitoring-backup-retention",
    "target-host-capacity-calibration"
)
$expectedEngineRevision = "7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2"
$expectedMinimumProjectRevision = "bf673e572cdcd4602c9280356ff2bf6df542bdd1"
$expectedBranch = "zlevel/server-hardening"
$expectedCompositeGates = @{
    release = @{
        Contract = "WTZ-RELEASE-1"
        Script = "Tools/run_zlevel_release_gate.ps1"
        Report = "zlevel-release.json"
    }
    recovery = @{
        Contract = "WTZ-RECOVERY-1"
        Script = "Tools/run_zlevel_recovery_rehearsal.ps1"
        Report = "zlevel-recovery.json"
    }
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
Assert-Equal ([int] $manifest.schemaVersion) 1 "Final manifest schema"
Assert-Equal ([string] $manifest.contractVersion) "WTZ-P0-P8-1" "Final manifest contract"
Assert-Equal ([string] $manifest.configuration) "Release" "Final manifest configuration"
Assert-Equal ([string] $manifest.readiness.roadmapStatus) "Complete" "Roadmap readiness"
Assert-Equal ([string] $manifest.readiness.deploymentClass) "ControlledPublicPilot" "Deployment class"
Assert-Equal ([string] $manifest.readiness.unrestrictedPublicServer) `
    "RequiresExternalOperationalEvidence" "Unrestricted-server policy"

Assert-Equal ([string] $manifest.sourcePolicy.minimumProjectRevision) `
    $expectedMinimumProjectRevision "Minimum project revision"
Assert-Equal ([string] $manifest.sourcePolicy.engineRevision) `
    $expectedEngineRevision "Engine revision"
Assert-Equal ([string] $manifest.sourcePolicy.engineSubmodulePath) "RobustToolbox" "Engine submodule path"
Assert-Equal ([string] $manifest.sourcePolicy.remote) "origin" "Protected remote"
Assert-Equal ([string] $manifest.sourcePolicy.branch) $expectedBranch "Protected branch"
if ($manifest.sourcePolicy.requireRemoteMatch -ne $true -or
    $manifest.sourcePolicy.requireCleanProject -ne $true -or
    $manifest.sourcePolicy.requireCleanEngine -ne $true) {
    throw "WTZ-P0-P8-1 must require remote matching and clean project/engine worktrees."
}

Assert-ExactSet "Required phase set" $expectedPhases @(
    $manifest.requiredPhases | ForEach-Object { [string] $_ })
Assert-ExactSet "Required document set" $expectedDocuments @(
    $manifest.requiredDocuments | ForEach-Object { ([string] $_).Replace("\", "/") })
Assert-ExactSet "Operational health test set" $expectedHealthTests @(
    $manifest.operationalHealthTests | ForEach-Object { [string] $_ })
Assert-ExactSet "External launch condition set" $expectedExternalConditions @(
    $manifest.externalLaunchConditions | ForEach-Object { [string] $_ })

$resolvedDocuments = [System.Collections.Generic.List[object]]::new()
foreach ($document in $expectedDocuments) {
    $resolvedDocuments.Add([pscustomobject]@{
        Relative = $document
        FullPath = Resolve-RepositoryFile $document "Required operations document"
    }) | Out-Null
}

$compositeGates = @($manifest.compositeGates)
Assert-ExactSet "Composite gate id set" @($expectedCompositeGates.Keys) @(
    $compositeGates | ForEach-Object { [string] $_.id })
$validatedCompositeGates = @{}
foreach ($gate in $compositeGates) {
    $id = [string] $gate.id
    $expected = $expectedCompositeGates[$id]
    Assert-Equal ([string] $gate.contractVersion) $expected.Contract "Composite '$id' contract"
    Assert-Equal (([string] $gate.script).Replace("\", "/")) $expected.Script "Composite '$id' script"
    Assert-Equal ([string] $gate.report) $expected.Report "Composite '$id' report"
    $validatedCompositeGates[$id] = [pscustomobject]@{
        Id = $id
        Contract = $expected.Contract
        ScriptPath = Resolve-RepositoryFile $expected.Script "Composite '$id' script"
        ReportName = $expected.Report
    }
}

$profiles = @($manifest.performanceProfiles)
Assert-ExactSet "Performance profile id set" @(
    "server-lifecycle",
    "server-soak-endurance",
    "server-soak-capacity",
    "neutral-baseline") @($profiles | ForEach-Object { [string] $_.id })
$profilesById = @{}
foreach ($profile in $profiles) {
    $profilesById[[string] $profile.id] = $profile
}

$lifecycleProfile = $profilesById["server-lifecycle"]
Assert-Equal (([string] $lifecycleProfile.script).Replace("\", "/")) `
    "Tools/run_zlevel_server_lifecycle.ps1" "Lifecycle script"
Assert-Equal ([string] $lifecycleProfile.report) "zlevel-server-lifecycle.json" "Lifecycle report"
Assert-Equal ([int] $lifecycleProfile.schemaVersion) 1 "Lifecycle schema"
Assert-Equal ([int] $lifecycleProfile.settings.warmupCycles) 8 "Lifecycle warmup"
Assert-Equal ([int] $lifecycleProfile.settings.measuredCycles) 128 "Lifecycle cycles"
Assert-Equal ([double] $lifecycleProfile.limits.maximumP95Milliseconds) 30.0 "Lifecycle p95 limit"
Assert-Equal ([double] $lifecycleProfile.limits.maximumP99Milliseconds) 40.0 "Lifecycle p99 limit"
Assert-Equal ([double] $lifecycleProfile.limits.maximumMilliseconds) 66.667 "Lifecycle max limit"
Assert-Equal ([long] $lifecycleProfile.limits.maximumAllocatedBytesPerCycle) 1048576 "Lifecycle allocation limit"
Assert-Equal ([long] $lifecycleProfile.limits.maximumRetainedHeapDeltaBytes) 2097152 "Lifecycle heap limit"

foreach ($id in @("server-soak-endurance", "server-soak-capacity")) {
    $profile = $profilesById[$id]
    Assert-Equal (([string] $profile.script).Replace("\", "/")) `
        "Tools/run_zlevel_server_soak.ps1" "$id script"
    Assert-Equal ([string] $profile.report) "zlevel-server-soak.json" "$id report"
    Assert-Equal ([int] $profile.schemaVersion) 6 "$id schema"
    Assert-Equal ([int] $profile.settings.floorCount) 10 "$id floor count"
    Assert-Equal ([int] $profile.settings.warmupIterations) 8 "$id warmup"
    Assert-Equal ([int] $profile.settings.candidateCopiesPerTile) 8 "$id candidate copies"
    Assert-Equal ([int] $profile.limits.maximumDeferredRefreshes) 0 "$id deferred limit"
    Assert-Equal ([int] $profile.limits.maximumBudgetExhaustions) 0 "$id exhaustion limit"
}

$enduranceProfile = $profilesById["server-soak-endurance"]
Assert-Equal ([int] $enduranceProfile.settings.sessionCount) 32 "Endurance session count"
Assert-Equal ([int] $enduranceProfile.settings.measuredIterations) 1024 "Endurance iterations"
Assert-Equal ([double] $enduranceProfile.limits.maximumP95Milliseconds) 30.0 "Endurance p95 limit"
Assert-Equal ([double] $enduranceProfile.limits.maximumP99Milliseconds) 33.333 "Endurance p99 limit"
Assert-Equal ([double] $enduranceProfile.limits.maximumMilliseconds) 66.667 "Endurance max limit"
Assert-Equal ([double] $enduranceProfile.limits.minimumContextCacheHitPercent) 85.0 "Endurance cache limit"
Assert-Equal ([long] $enduranceProfile.limits.maximumAllocatedBytesPerIteration) 24576 "Endurance allocation limit"

$capacityProfile = $profilesById["server-soak-capacity"]
Assert-Equal ([int] $capacityProfile.settings.sessionCount) 64 "Capacity session count"
Assert-Equal ([int] $capacityProfile.settings.measuredIterations) 128 "Capacity iterations"
Assert-Equal ([double] $capacityProfile.limits.maximumP95Milliseconds) 55.0 "Capacity p95 limit"
Assert-Equal ([double] $capacityProfile.limits.maximumP99Milliseconds) 66.667 "Capacity p99 limit"
Assert-Equal ([double] $capacityProfile.limits.maximumMilliseconds) 125.0 "Capacity max limit"
Assert-Equal ([double] $capacityProfile.limits.minimumContextCacheHitPercent) 90.0 "Capacity cache limit"
Assert-Equal ([long] $capacityProfile.limits.maximumAllocatedBytesPerIteration) 40960 "Capacity allocation limit"

$baselineProfile = $profilesById["neutral-baseline"]
Assert-Equal (([string] $baselineProfile.script).Replace("\", "/")) `
    "Tools/run_zlevel_baseline.ps1" "Baseline script"
Assert-Equal ([string] $baselineProfile.report) "zlevel-baseline-{floor}-floors.json" "Baseline report pattern"
Assert-Equal ([int] $baselineProfile.schemaVersion) 5 "Baseline schema"
Assert-ExactSet "Baseline floor set" @("3", "6", "10") @(
    $baselineProfile.settings.floorCounts | ForEach-Object { ([int] $_).ToString() })
Assert-Equal ([int] $baselineProfile.settings.measuredIterations) 3 "Baseline iterations"
Assert-Equal ([long] $baselineProfile.limits.maximumAllocatedBytes) 8192 "Baseline allocation limit"
Assert-Equal ([double] $baselineProfile.limits.minimumWarmCacheHitPercent) 100.0 "Baseline cache limit"
foreach ($property in @(
    "maximumPvsBudgetExhaustions",
    "maximumSkyBudgetExhaustions",
    "maximumBoundaryEvictions",
    "maximumSkyEvictions")) {
    Assert-Equal ([int] $baselineProfile.limits.$property) 0 "Baseline $property"
}

Assert-Equal ([string] $manifest.pilotContract.contractVersion) "WTZ-PILOT-1" "Pilot contract"
Assert-Equal ([int] $manifest.pilotContract.minimumConcurrentHumanPlayers) 8 "Pilot player minimum"
Assert-Equal ([int] $manifest.pilotContract.minimumDurationMinutes) 120 "Pilot duration minimum"
if ($manifest.pilotContract.requireCheckNotes -ne $true) {
    throw "WTZ-PILOT-1 must require evidence notes for every check."
}
Assert-ExactSet "Pilot check set" $expectedPilotChecks @(
    $manifest.pilotContract.requiredChecks | ForEach-Object { [string] $_ })

if ($ValidateOnly) {
    Write-Host "WTZ-P0-P8-1 manifest validation passed."
    Write-Host "  phases=$($expectedPhases.Count), documents=$($expectedDocuments.Count), composites=$($compositeGates.Count), health-tests=$($expectedHealthTests.Count), performance-profiles=$($profiles.Count)"
    return
}

$projectRevision = Get-GitText $repoRoot @("rev-parse", "HEAD") "WTZ Project revision lookup"
$engineRevision = Get-GitText $engineRoot @("rev-parse", "HEAD") "WTZ Engine revision lookup"
$branch = Get-GitText $repoRoot @("branch", "--show-current") "WTZ Project branch lookup"
$projectStatusBefore = Get-GitText $repoRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Project status lookup"
$engineStatusBefore = Get-GitText $engineRoot @("status", "--porcelain", "--untracked-files=all") `
    "WTZ Engine status lookup"
$projectCleanBefore = [string]::IsNullOrWhiteSpace($projectStatusBefore)
$engineCleanBefore = [string]::IsNullOrWhiteSpace($engineStatusBefore)
$gitlinkText = Get-GitText $repoRoot @("ls-tree", "HEAD", "--", "RobustToolbox") `
    "WTZ Engine gitlink lookup"
if ($gitlinkText -notmatch '^160000 commit ([0-9a-f]{40})\s+RobustToolbox$') {
    throw "Unable to parse the RobustToolbox submodule gitlink."
}

$gitlinkRevision = $Matches[1]
Assert-Equal $engineRevision $expectedEngineRevision "Engine checkout revision"
Assert-Equal $gitlinkRevision $engineRevision "Engine gitlink revision"
Assert-Equal $branch $expectedBranch "Active final-gate branch"
if (-not (Test-GitAncestor $repoRoot $expectedMinimumProjectRevision $projectRevision)) {
    throw "Project revision '$projectRevision' does not contain final-gate minimum '$expectedMinimumProjectRevision'."
}

if ((-not $projectCleanBefore -or -not $engineCleanBefore) -and
    -not $AllowDirtySourceForDevelopment) {
    throw "WTZ-P0-P8-1 requires clean project and engine worktrees. Use -AllowDirtySourceForDevelopment only while developing the gate."
}

$remoteRevisionBefore = Get-RemoteRevision `
    ([string] $manifest.sourcePolicy.remote) `
    ([string] $manifest.sourcePolicy.branch)
if ($remoteRevisionBefore -ne $projectRevision -and -not $AllowDirtySourceForDevelopment) {
    throw "Published remote revision '$remoteRevisionBefore' does not match project HEAD '$projectRevision'."
}

$developmentRun = $AllowDirtySourceForDevelopment -or
    $SkipBuildForDevelopment -or
    $SkipVisualCaptureForDevelopment -or
    $SkipPerformanceForDevelopment
$expectedParentStatus = if ($developmentRun) { "DevelopmentPassed" } else { "Passed" }
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$runId = "{0}-{1}-{2}" -f
    [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"),
    $PID,
    [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDirectory = Join-Path $OutputDirectory $runId
[System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$reportPath = Join-Path $runDirectory "zlevel-final.json"
$timer = [System.Diagnostics.Stopwatch]::StartNew()
$compositeReports = [System.Collections.Generic.List[object]]::new()
$performanceReports = [System.Collections.Generic.List[object]]::new()
$healthReport = $null
$pilotReport = $null
$failure = $null
$projectStatusAfter = $null
$engineStatusAfter = $null
$remoteRevisionAfter = $null

function Invoke-CompositeGates {
    $releaseGate = $validatedCompositeGates["release"]
    $releaseDirectory = Join-Path $runDirectory "release"
    [System.IO.Directory]::CreateDirectory($releaseDirectory) | Out-Null
    $releaseArguments = @(
        "-OutputDirectory", $releaseDirectory,
        "-VisualTimeoutSeconds", $VisualTimeoutSeconds.ToString()
    )
    if ($NoRestore) {
        $releaseArguments += "-NoRestore"
    }
    if ($AllowDirtySourceForDevelopment) {
        $releaseArguments += "-AllowDirtySourceForDevelopment"
    }
    if ($SkipBuildForDevelopment) {
        $releaseArguments += "-SkipBuildForDevelopment"
    }
    if ($SkipVisualCaptureForDevelopment) {
        $releaseArguments += "-SkipVisualCaptureForDevelopment"
    }

    Invoke-PowerShellChild "WTZ-RELEASE-1" $releaseGate.ScriptPath $releaseArguments
    $releasePath = Get-OnlyReport $releaseDirectory $releaseGate.ReportName "WTZ-RELEASE-1"
    $release = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
    $releaseDevelopment = $AllowDirtySourceForDevelopment -or
        $SkipBuildForDevelopment -or
        $SkipVisualCaptureForDevelopment
    $expectedReleaseStatus = if ($releaseDevelopment) { "DevelopmentPassed" } else { "Passed" }
    Assert-Equal ([int] $release.schemaVersion) 1 "Release report schema"
    Assert-Equal ([string] $release.contractVersion) "WTZ-RELEASE-1" "Release report contract"
    Assert-Equal ([string] $release.status) $expectedReleaseStatus "Release report status"
    Assert-Equal ([string] $release.revisions.project) $projectRevision "Release project revision"
    Assert-Equal ([string] $release.revisions.engine) $engineRevision "Release engine revision"
    Assert-Equal ([string] $release.revisions.gitlink) $gitlinkRevision "Release gitlink revision"
    Assert-Equal ([int] $release.summary.declaredTests) 41 "Release declared tests"
    Assert-Equal ([int] $release.summary.executedTests) 41 "Release executed tests"
    Assert-Equal ([int] $release.summary.passedTests) 41 "Release passed tests"
    Assert-Equal ([int] $release.summary.requiredCompositeGates) 3 "Release child gate count"
    if (-not $SkipVisualCaptureForDevelopment) {
        Assert-Equal ([int] $release.summary.passedCompositeGates) 3 "Release passed child gates"
    }

    $compositeReports.Add([ordered]@{
        id = "release"
        contractVersion = "WTZ-RELEASE-1"
        status = [string] $release.status
        report = Get-RunRelativePath $releasePath
        reportSha256 = (Get-FileHash -LiteralPath $releasePath -Algorithm SHA256).Hash.ToLowerInvariant()
        checks = 41
        nestedGates = 3
    }) | Out-Null

    $recoveryGate = $validatedCompositeGates["recovery"]
    $recoveryDirectory = Join-Path $runDirectory "recovery"
    [System.IO.Directory]::CreateDirectory($recoveryDirectory) | Out-Null
    $recoveryArguments = @(
        "-Configuration", "Release",
        "-OutputDirectory", $recoveryDirectory
    )
    if ($AllowDirtySourceForDevelopment) {
        $recoveryArguments += "-AllowDirtySourceForDevelopment"
    }
    if ($SkipBuildForDevelopment) {
        $recoveryArguments += "-SkipBuildForDevelopment"
    }

    Invoke-PowerShellChild "WTZ-RECOVERY-1" $recoveryGate.ScriptPath $recoveryArguments
    $recoveryPath = Get-OnlyReport $recoveryDirectory $recoveryGate.ReportName "WTZ-RECOVERY-1"
    $recovery = Get-Content -LiteralPath $recoveryPath -Raw | ConvertFrom-Json
    $recoveryDevelopment = $AllowDirtySourceForDevelopment -or $SkipBuildForDevelopment
    $expectedRecoveryStatus = if ($recoveryDevelopment) { "DevelopmentPassed" } else { "Passed" }
    Assert-Equal ([int] $recovery.schemaVersion) 1 "Recovery report schema"
    Assert-Equal ([string] $recovery.contractVersion) "WTZ-RECOVERY-1" "Recovery report contract"
    Assert-Equal ([string] $recovery.status) $expectedRecoveryStatus "Recovery report status"
    Assert-Equal ([string] $recovery.source.projectRevision) $projectRevision "Recovery project revision"
    Assert-Equal ([string] $recovery.source.engineRevision) $engineRevision "Recovery engine revision"
    Assert-Equal ([string] $recovery.source.gitlinkRevision) $gitlinkRevision "Recovery gitlink revision"
    Assert-Equal ([long] $recovery.scenario.attempts) 3 "Recovery attempts"
    Assert-Equal ([long] $recovery.scenario.successes) 2 "Recovery successes"
    Assert-Equal ([long] $recovery.scenario.failures) 1 "Recovery failures"
    Assert-Equal ([int] $recovery.steps.temporaryFilesRemaining) 0 "Recovery temporary files"
    foreach ($property in @(
        "initialCheckpointCreated",
        "invalidCheckpointRejected",
        "knownGoodBytesPreserved",
        "corruptSourceRemoved",
        "initialCheckpointLoaded",
        "recoveredCheckpointCreated",
        "recoveredCheckpointLoaded",
        "structuralStateMatched",
        "noCriticalHealthFinding")) {
        if ($recovery.steps.$property -ne $true) {
            throw "Recovery step '$property' did not pass."
        }
    }

    $compositeReports.Add([ordered]@{
        id = "recovery"
        contractVersion = "WTZ-RECOVERY-1"
        status = [string] $recovery.status
        report = Get-RunRelativePath $recoveryPath
        reportSha256 = (Get-FileHash -LiteralPath $recoveryPath -Algorithm SHA256).Hash.ToLowerInvariant()
        checkpoints = 2
        refused = 1
        loads = 2
    }) | Out-Null
}

function Invoke-OperationalHealthTests {
    $directory = Join-Path $runDirectory "operational-health"
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    $trxName = "zlevel-operational-health.trx"
    $trxPath = Join-Path $directory $trxName
    $filter = @($expectedHealthTests | ForEach-Object { "FullyQualifiedName=$_" }) -join "|"
    $arguments = @(
        "test",
        (Join-Path $repoRoot "Content.Tests\Content.Tests.csproj"),
        "--configuration", "Release",
        "--filter", $filter,
        "--logger", "trx;LogFileName=$trxName",
        "--results-directory", $directory,
        "--no-build",
        "--nologo"
    )
    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    Write-Host "Running four exact WTZ-OPS-HEALTH-1 tests..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Operational health tests failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "Missing operational health TRX: $trxPath"
    }

    $outcomes = Get-TrxOutcomes $trxPath
    Assert-ExactSet "Operational health TRX test set" $expectedHealthTests @($outcomes.Keys)
    foreach ($test in $expectedHealthTests) {
        Assert-Equal ([string] $outcomes[$test]) "Passed" "Operational health test '$test'"
    }

    $script:healthReport = [ordered]@{
        contractVersion = "WTZ-OPS-HEALTH-1"
        status = "Passed"
        tests = $expectedHealthTests.Count
        trx = Get-RunRelativePath $trxPath
        trxSha256 = (Get-FileHash -LiteralPath $trxPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Assert-Maximum([double] $Actual, [double] $Maximum, [string] $Description) {
    if ($Actual -gt $Maximum) {
        throw "$Description is $Actual; maximum is $Maximum."
    }
}

function Assert-Minimum([double] $Actual, [double] $Minimum, [string] $Description) {
    if ($Actual -lt $Minimum) {
        throw "$Description is $Actual; minimum is $Minimum."
    }
}

function Invoke-PerformanceGates {
    if ($SkipPerformanceForDevelopment) {
        foreach ($id in @("server-lifecycle", "server-soak-endurance", "server-soak-capacity", "neutral-baseline")) {
            $performanceReports.Add([ordered]@{
                id = $id
                status = "SkippedForDevelopment"
                report = $null
                reportSha256 = $null
            }) | Out-Null
        }
        return
    }

    $lifecycleDirectory = Join-Path $runDirectory "server-lifecycle"
    [System.IO.Directory]::CreateDirectory($lifecycleDirectory) | Out-Null
    $lifecycleScript = Resolve-RepositoryFile `
        ([string] $lifecycleProfile.script) "Lifecycle runner"
    Invoke-PowerShellChild "Server GC lifecycle envelope" $lifecycleScript @(
        "-Configuration", "Release",
        "-WarmupCycles", ([int] $lifecycleProfile.settings.warmupCycles).ToString(),
        "-Cycles", ([int] $lifecycleProfile.settings.measuredCycles).ToString(),
        "-OutputDirectory", $lifecycleDirectory,
        "-RequireReleaseEnvelope",
        "-NoBuild"
    )
    $lifecyclePath = Join-Path $lifecycleDirectory ([string] $lifecycleProfile.report)
    $lifecycle = Get-Content -LiteralPath $lifecyclePath -Raw | ConvertFrom-Json
    Assert-Equal ([int] $lifecycle.schemaVersion) 1 "Lifecycle report schema"
    Assert-Equal ([string] $lifecycle.host.buildConfiguration) "Release" "Lifecycle configuration"
    if (-not $lifecycle.host.serverGarbageCollection) {
        throw "Lifecycle report did not use Server GC."
    }
    Assert-Equal ([int] $lifecycle.settings.warmupCycles) 8 "Lifecycle report warmup"
    Assert-Equal ([int] $lifecycle.settings.measuredCycles) 128 "Lifecycle report cycles"
    Assert-Equal (($lifecycle.baseline | ConvertTo-Json -Compress)) `
        (($lifecycle.finalState | ConvertTo-Json -Compress)) "Lifecycle final cache state"
    if ([int] $lifecycle.generationTwoCollections -lt 1) {
        throw "Lifecycle report did not observe a generation-two collection."
    }
    $lifecycleAllocatedPerCycle = [double] $lifecycle.allocatedBytes / $lifecycle.settings.measuredCycles
    Assert-Maximum ([double] $lifecycle.cycleLatency.p95Milliseconds) `
        ([double] $lifecycleProfile.limits.maximumP95Milliseconds) "Lifecycle p95"
    Assert-Maximum ([double] $lifecycle.cycleLatency.p99Milliseconds) `
        ([double] $lifecycleProfile.limits.maximumP99Milliseconds) "Lifecycle p99"
    Assert-Maximum ([double] $lifecycle.cycleLatency.maxMilliseconds) `
        ([double] $lifecycleProfile.limits.maximumMilliseconds) "Lifecycle maximum"
    Assert-Maximum $lifecycleAllocatedPerCycle `
        ([double] $lifecycleProfile.limits.maximumAllocatedBytesPerCycle) "Lifecycle allocation per cycle"
    Assert-Maximum ([double] $lifecycle.retainedHeapDeltaBytes) `
        ([double] $lifecycleProfile.limits.maximumRetainedHeapDeltaBytes) "Lifecycle retained heap delta"
    $performanceReports.Add([ordered]@{
        id = "server-lifecycle"
        status = "Passed"
        report = Get-RunRelativePath $lifecyclePath
        reportSha256 = (Get-FileHash -LiteralPath $lifecyclePath -Algorithm SHA256).Hash.ToLowerInvariant()
        p95Milliseconds = [double] $lifecycle.cycleLatency.p95Milliseconds
        p99Milliseconds = [double] $lifecycle.cycleLatency.p99Milliseconds
        allocatedBytesPerCycle = $lifecycleAllocatedPerCycle
        retainedHeapDeltaBytes = [long] $lifecycle.retainedHeapDeltaBytes
    }) | Out-Null

    foreach ($id in @("server-soak-endurance", "server-soak-capacity")) {
        $profile = $profilesById[$id]
        $directory = Join-Path $runDirectory $id
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
        $script = Resolve-RepositoryFile ([string] $profile.script) "$id runner"
        $arguments = @(
            "-Configuration", "Release",
            "-Floors", ([int] $profile.settings.floorCount).ToString(),
            "-Sessions", ([int] $profile.settings.sessionCount).ToString(),
            "-WarmupIterations", ([int] $profile.settings.warmupIterations).ToString(),
            "-Iterations", ([int] $profile.settings.measuredIterations).ToString(),
            "-CandidateCopies", ([int] $profile.settings.candidateCopiesPerTile).ToString(),
            "-OutputDirectory", $directory,
            "-RequireServerGC",
            "-NoBuild"
        )
        if ($id -eq "server-soak-endurance") {
            $arguments += "-RequireReleaseEnvelope"
        }
        else {
            $arguments += "-RequireCapacityEnvelope"
        }

        Invoke-PowerShellChild "$id envelope" $script $arguments
        $path = Join-Path $directory ([string] $profile.report)
        $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        Assert-Equal ([int] $report.schemaVersion) 6 "$id report schema"
        Assert-Equal ([string] $report.host.buildConfiguration) "Release" "$id configuration"
        if (-not $report.host.serverGarbageCollection) {
            throw "$id report did not use Server GC."
        }
        foreach ($setting in @(
            "floorCount",
            "sessionCount",
            "warmupIterations",
            "measuredIterations",
            "candidateCopiesPerTile")) {
            Assert-Equal ([int] $report.settings.$setting) ([int] $profile.settings.$setting) "$id $setting"
        }

        $frame = $report.measured.pvsSchedulerFrameLatency
        $scheduler = $report.measured.pvsScheduler
        $allocatedPerIteration = [double] $report.measured.allocatedBytes / $report.measured.iterations
        Assert-Maximum ([double] $frame.p95Milliseconds) `
            ([double] $profile.limits.maximumP95Milliseconds) "$id p95"
        Assert-Maximum ([double] $frame.p99Milliseconds) `
            ([double] $profile.limits.maximumP99Milliseconds) "$id p99"
        Assert-Maximum ([double] $frame.maxMilliseconds) `
            ([double] $profile.limits.maximumMilliseconds) "$id maximum"
        Assert-Minimum ([double] $scheduler.visibilityContextCacheHitPercent) `
            ([double] $profile.limits.minimumContextCacheHitPercent) "$id context-cache hits"
        Assert-Maximum $allocatedPerIteration `
            ([double] $profile.limits.maximumAllocatedBytesPerIteration) "$id allocation per iteration"
        Assert-Maximum ([double] $scheduler.deferredRefreshes) `
            ([double] $profile.limits.maximumDeferredRefreshes) "$id deferred refreshes"
        Assert-Maximum ([double] $scheduler.budgetExhaustions) `
            ([double] $profile.limits.maximumBudgetExhaustions) "$id scheduler budget exhaustions"
        Assert-Maximum ([double] $report.measured.sharedMetrics.pvsBudgetExhaustions) 0 `
            "$id shared PVS budget exhaustions"
        $performanceReports.Add([ordered]@{
            id = $id
            status = "Passed"
            report = Get-RunRelativePath $path
            reportSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            sessions = [int] $report.settings.sessionCount
            iterations = [int] $report.settings.measuredIterations
            p95Milliseconds = [double] $frame.p95Milliseconds
            p99Milliseconds = [double] $frame.p99Milliseconds
            contextCacheHitPercent = [double] $scheduler.visibilityContextCacheHitPercent
            allocatedBytesPerIteration = $allocatedPerIteration
        }) | Out-Null
    }

    $baselineDirectory = Join-Path $runDirectory "neutral-baseline"
    [System.IO.Directory]::CreateDirectory($baselineDirectory) | Out-Null
    $baselineScript = Resolve-RepositoryFile ([string] $baselineProfile.script) "Baseline runner"
    Invoke-PowerShellChild "3/6/10-floor neutral baseline" $baselineScript @(
        "-Configuration", "Release",
        "-OutputDirectory", $baselineDirectory,
        "-NoBuild"
    )
    $baselineArtifacts = [System.Collections.Generic.List[object]]::new()
    foreach ($floor in @(3, 6, 10)) {
        $name = ([string] $baselineProfile.report).Replace("{floor}", $floor.ToString())
        $path = Join-Path $baselineDirectory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Missing $floor-floor baseline report: $path"
        }
        $baseline = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
        Assert-Equal ([int] $baseline.schemaVersion) 5 "$floor-floor baseline schema"
        Assert-Equal ([int] $baseline.fixture.floorCount) $floor "$floor-floor baseline fixture"
        Assert-Equal ([int] $baseline.workload.measuredIterations) 3 "$floor-floor baseline iterations"
        Assert-Maximum ([double] $baseline.measured.allocatedBytes) `
            ([double] $baselineProfile.limits.maximumAllocatedBytes) "$floor-floor baseline allocation"
        $metrics = $baseline.measured.metrics
        foreach ($cache in @("boundaryCacheHitPercent", "skyExposureCacheHitPercent", "gravityCacheHitPercent")) {
            Assert-Minimum ([double] $metrics.$cache) `
                ([double] $baselineProfile.limits.minimumWarmCacheHitPercent) "$floor-floor $cache"
        }
        Assert-Maximum ([double] $metrics.pvsBudgetExhaustions) 0 "$floor-floor PVS exhaustion"
        Assert-Maximum ([double] $metrics.skyExposureBudgetExhaustions) 0 "$floor-floor sky exhaustion"
        Assert-Maximum ([double] $metrics.boundaryEvictions) 0 "$floor-floor boundary evictions"
        Assert-Maximum ([double] $metrics.skyExposureEvictions) 0 "$floor-floor sky evictions"
        $baselineArtifacts.Add([ordered]@{
            floorCount = $floor
            report = Get-RunRelativePath $path
            reportSha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
            elapsedMilliseconds = [double] $baseline.measured.elapsedMilliseconds
            allocatedBytes = [long] $baseline.measured.allocatedBytes
        }) | Out-Null
    }
    $performanceReports.Add([ordered]@{
        id = "neutral-baseline"
        status = "Passed"
        reports = @($baselineArtifacts)
    }) | Out-Null
}

function Read-PilotRecord {
    if ([string]::IsNullOrWhiteSpace($PilotRecordPath)) {
        return $null
    }

    $fullPath = [System.IO.Path]::GetFullPath($PilotRecordPath)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Pilot record does not exist: $fullPath"
    }

    $record = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    Assert-Equal ([int] $record.schemaVersion) 1 "Pilot record schema"
    Assert-Equal ([string] $record.contractVersion) "WTZ-PILOT-1" "Pilot record contract"
    Assert-Equal ([string] $record.status) "Passed" "Pilot record status"
    Assert-Equal ([string] $record.source.projectRevision) $projectRevision "Pilot project revision"
    Assert-Equal ([string] $record.source.engineRevision) $engineRevision "Pilot engine revision"
    Assert-Equal ([string] $record.source.gitlinkRevision) $gitlinkRevision "Pilot gitlink revision"
    if ([string]::IsNullOrWhiteSpace([string] $record.operator.name) -or
        [string]::IsNullOrWhiteSpace([string] $record.session.targetHost) -or
        [string]::IsNullOrWhiteSpace([string] $record.session.map)) {
        throw "Pilot record requires operator name, target host, and map."
    }
    Assert-Minimum ([double] $record.session.concurrentHumanPlayers) `
        ([double] $manifest.pilotContract.minimumConcurrentHumanPlayers) "Pilot concurrent players"
    Assert-Minimum ([double] $record.session.durationMinutes) `
        ([double] $manifest.pilotContract.minimumDurationMinutes) "Pilot duration"
    $checks = @($record.checks)
    Assert-ExactSet "Pilot record check set" $expectedPilotChecks @(
        $checks | ForEach-Object { [string] $_.id })
    foreach ($check in $checks) {
        if ($check.passed -ne $true) {
            throw "Pilot check '$($check.id)' did not pass."
        }
        if ([string]::IsNullOrWhiteSpace([string] $check.notes)) {
            throw "Pilot check '$($check.id)' requires evidence notes."
        }
    }

    return [ordered]@{
        contractVersion = "WTZ-PILOT-1"
        status = "Passed"
        operator = [string] $record.operator.name
        targetHost = [string] $record.session.targetHost
        map = [string] $record.session.map
        concurrentHumanPlayers = [int] $record.session.concurrentHumanPlayers
        durationMinutes = [int] $record.session.durationMinutes
        checks = $checks.Count
        sourcePath = $fullPath
        sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

try {
    Invoke-CompositeGates
    Invoke-OperationalHealthTests
    Invoke-PerformanceGates
    $pilotReport = Read-PilotRecord
}
catch {
    $failure = $_.Exception.Message
}

try {
    $projectRevisionAfter = Get-GitText $repoRoot @("rev-parse", "HEAD") "Post-gate project revision lookup"
    $engineRevisionAfter = Get-GitText $engineRoot @("rev-parse", "HEAD") "Post-gate engine revision lookup"
    $projectStatusAfter = Get-GitText $repoRoot @("status", "--porcelain", "--untracked-files=all") `
        "Post-gate project status lookup"
    $engineStatusAfter = Get-GitText $engineRoot @("status", "--porcelain", "--untracked-files=all") `
        "Post-gate engine status lookup"
    $remoteRevisionAfter = Get-RemoteRevision `
        ([string] $manifest.sourcePolicy.remote) `
        ([string] $manifest.sourcePolicy.branch)
    Assert-Equal $projectRevisionAfter $projectRevision "Project revision after final gate"
    Assert-Equal $engineRevisionAfter $engineRevision "Engine revision after final gate"
    Assert-Equal $remoteRevisionAfter $remoteRevisionBefore "Remote revision after final gate"
    Assert-Equal $projectStatusAfter $projectStatusBefore "Project worktree state after final gate"
    Assert-Equal $engineStatusAfter $engineStatusBefore "Engine worktree state after final gate"
    if (-not $developmentRun -and
        (-not [string]::IsNullOrWhiteSpace($projectStatusAfter) -or
         -not [string]::IsNullOrWhiteSpace($engineStatusAfter))) {
        throw "Strict final gate changed the project or engine worktree."
    }

    $ownedProcesses = @(Get-CimInstance Win32_Process | Where-Object {
        $command = [string] $_.CommandLine
        -not [string]::IsNullOrWhiteSpace($command) -and
        $command.IndexOf($repoRoot, [System.StringComparison]::OrdinalIgnoreCase) -ge 0 -and
        ($command -match 'Content\.(Client|Server)\.dll')
    })
    if ($ownedProcesses.Count -ne 0) {
        throw "Final gate left $($ownedProcesses.Count) WTZ game process(es) running."
    }
}
catch {
    if ($null -eq $failure) {
        $failure = $_.Exception.Message
    }
    else {
        $failure = "$failure Post-gate check: $($_.Exception.Message)"
    }
}

$timer.Stop()
$status = if ($null -ne $failure) {
    "Failed"
}
else {
    $expectedParentStatus
}
$roadmapStatus = if ($status -eq "Passed") { "Complete" } else { "NotAccepted" }
$deploymentClass = if ($status -ne "Passed") {
    "DevelopmentOnly"
}
elseif ($null -ne $pilotReport) {
    "PublicCandidate"
}
else {
    "ControlledPublicPilot"
}
$externalConditions = @($expectedExternalConditions | ForEach-Object {
    [ordered]@{
        id = $_
        status = if ($_ -eq "human-multiplayer-pilot" -and $null -ne $pilotReport) {
            "Satisfied"
        }
        else {
            "RequiredExternal"
        }
    }
})
$documentReports = @($resolvedDocuments | ForEach-Object {
    [ordered]@{
        path = $_.Relative
        sha256 = (Get-FileHash -LiteralPath $_.FullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
})
$passedCompositeGates = @($compositeReports | Where-Object status -EQ "Passed").Count
$passedPerformanceProfiles = @($performanceReports | Where-Object status -EQ "Passed").Count
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = "WTZ-P0-P8-1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    runId = $runId
    status = $status
    configuration = "Release"
    durationMilliseconds = $timer.Elapsed.TotalMilliseconds
    readiness = [ordered]@{
        roadmapStatus = $roadmapStatus
        deploymentClass = $deploymentClass
        unrestrictedPublicServer = "NotCertified"
        externalConditions = $externalConditions
    }
    source = [ordered]@{
        projectRevision = $projectRevision
        engineRevision = $engineRevision
        gitlinkRevision = $gitlinkRevision
        branch = $branch
        remote = [string] $manifest.sourcePolicy.remote
        remoteRevisionBefore = $remoteRevisionBefore
        remoteRevisionAfter = $remoteRevisionAfter
        projectCleanBefore = $projectCleanBefore
        engineCleanBefore = $engineCleanBefore
        projectCleanAfter = [string]::IsNullOrWhiteSpace([string] $projectStatusAfter)
        engineCleanAfter = [string]::IsNullOrWhiteSpace([string] $engineStatusAfter)
    }
    development = [ordered]@{
        allowDirtySource = [bool] $AllowDirtySourceForDevelopment
        buildSkipped = [bool] $SkipBuildForDevelopment
        visualCaptureSkipped = [bool] $SkipVisualCaptureForDevelopment
        performanceSkipped = [bool] $SkipPerformanceForDevelopment
    }
    manifest = [ordered]@{
        path = $ManifestPath.Substring($repoPrefix.Length).Replace("\", "/")
        sha256 = (Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    summary = [ordered]@{
        phases = $expectedPhases.Count
        documents = $documentReports.Count
        requiredCompositeGates = $compositeGates.Count
        passedCompositeGates = $passedCompositeGates
        operationalHealthTests = if ($null -eq $healthReport) { 0 } else { [int] $healthReport.tests }
        requiredPerformanceProfiles = $profiles.Count
        passedPerformanceProfiles = $passedPerformanceProfiles
        pilotRecordProvided = $null -ne $pilotReport
    }
    documents = $documentReports
    operationalHealth = $healthReport
    compositeGates = @($compositeReports)
    performanceProfiles = @($performanceReports)
    pilot = $pilotReport
    failure = $failure
}

[System.IO.File]::WriteAllText(
    $reportPath,
    ($report | ConvertTo-Json -Depth 20),
    $utf8)
$reportHash = (Get-FileHash -LiteralPath $reportPath -Algorithm SHA256).Hash.ToLowerInvariant()

if ($status -eq "Failed") {
    Write-Host "WTZ-P0-P8-1 failed: $failure" -ForegroundColor Red
    Write-Host "  report=$reportPath"
    throw "WTZ-P0-P8-1 failed."
}

Write-Host "WTZ-P0-P8-1 $status."
Write-Host "  roadmap=$roadmapStatus, deployment=$deploymentClass, unrestricted-public-server=NotCertified"
Write-Host "  composites=$passedCompositeGates/$($compositeGates.Count), health=$($report.summary.operationalHealthTests)/$($expectedHealthTests.Count), performance=$passedPerformanceProfiles/$($profiles.Count)"
Write-Host "  project=$projectRevision, engine=$engineRevision, remote=$remoteRevisionBefore"
Write-Host "  report=$reportPath"
Write-Host "  report-sha256=$reportHash"
