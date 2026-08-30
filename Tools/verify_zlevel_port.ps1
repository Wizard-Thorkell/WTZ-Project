[CmdletBinding()]
param(
    [ValidateSet("Paired", "Portable")]
    [string] $Mode = "Paired",

    [string] $ProjectRoot,

    [string] $EngineRoot,

    [string] $ManifestPath,

    [string] $OutputDirectory,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $SkipBuild,

    [switch] $NoRestore,

    [switch] $RequireClean
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Split-Path -Parent $PSScriptRoot
}

$ProjectRoot = [System.IO.Path]::GetFullPath($ProjectRoot)
$projectPrefix = $ProjectRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ProjectRoot "Docs\ZLevelPortingManifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "artifacts\zlevel-port-compatibility"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing Z-level porting manifest: $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1) {
    throw "Unsupported Z-level porting manifest schema: $($manifest.schemaVersion)."
}

if ([string] $manifest.contractVersion -ne "WTZ-PORT-1") {
    throw "Unsupported Z-level porting contract: '$($manifest.contractVersion)'."
}

if ([string]::IsNullOrWhiteSpace($EngineRoot)) {
    $EngineRoot = Join-Path $ProjectRoot ([string] $manifest.project.submodulePath)
}

$EngineRoot = [System.IO.Path]::GetFullPath($EngineRoot)
$enginePrefix = $EngineRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

function Resolve-ContainedFile(
    [string] $Root,
    [string] $Prefix,
    [string] $RelativePath,
    [string] $Description) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Description must be a non-empty repository-relative path: '$RelativePath'."
    }

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
    if (-not $fullPath.StartsWith($Prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description escapes its repository: '$RelativePath'."
    }

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Description does not exist: '$RelativePath'."
    }

    return $fullPath
}

function Invoke-GitResult([string] $Root, [string[]] $GitArguments) {
    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& git -C $Root @GitArguments 2>&1)
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

function Get-GitText([string] $Root, [string[]] $GitArguments, [string] $Description) {
    $result = Invoke-GitResult $Root $GitArguments
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

function Get-OptionalInt([object] $Object, [string] $Property, [int] $Default) {
    $value = $Object.PSObject.Properties[$Property]
    if ($null -eq $value) {
        return $Default
    }

    return [int] $value.Value
}

$expectedCapabilities = @(
    "atomic-file-writes",
    "audio-post-processing",
    "audio-recipient-filtering",
    "audio-source-position",
    "chunk-replication",
    "engine-foundation",
    "external-shadow-atlases",
    "filtered-entity-snapshots",
    "imagesharp-pixel-read",
    "invalid-reference-reporting",
    "light-add-blend",
    "moving-grid-frames",
    "physics-contact-flush",
    "pointer-coordinate-layer",
    "pvs-render-hooks",
    "reusable-tree-queries",
    "serialized-yaml-identifiers",
    "sparse-tile-enumeration",
    "tile-index-serialization",
    "world-z-rendering"
) | Sort-Object

$expectedBuildProjects = @(
    "engine:Robust.Server.IntegrationTests/Robust.Server.IntegrationTests.csproj",
    "project:Content.IntegrationTests/Content.IntegrationTests.csproj"
) | Sort-Object
$expectedProbeCount = 50

$shaPattern = '^[0-9a-f]{40}$'
$commits = @($manifest.engine.commits)
$capabilities = @($manifest.capabilities)
$buildProjects = @($manifest.buildProjects)
if ($commits.Count -ne $expectedCapabilities.Count) {
    throw "Porting manifest has $($commits.Count) engine commits; expected $($expectedCapabilities.Count)."
}

if ($capabilities.Count -ne $expectedCapabilities.Count) {
    throw "Porting manifest has $($capabilities.Count) capabilities; expected $($expectedCapabilities.Count)."
}

$declaredCapabilities = @($capabilities | ForEach-Object { [string] $_.id } | Sort-Object -Unique)
$capabilityDifference = @(Compare-Object -ReferenceObject $expectedCapabilities -DifferenceObject $declaredCapabilities)
if ($capabilityDifference.Count -ne 0) {
    throw "Porting manifest capability IDs do not match the verifier's protected capability set."
}

$commitByCapability = @{}
$commitRevisions = @{}
foreach ($commit in $commits) {
    $revision = [string] $commit.revision
    $title = [string] $commit.title
    $capability = [string] $commit.capability
    if ($revision -notmatch $shaPattern) {
        throw "Engine series contains an invalid revision: '$revision'."
    }

    if ([string]::IsNullOrWhiteSpace($title)) {
        throw "Engine commit '$revision' has an empty title."
    }

    if ($expectedCapabilities -notcontains $capability) {
        throw "Engine commit '$revision' names unknown capability '$capability'."
    }

    if ($commitByCapability.ContainsKey($capability)) {
        throw "Engine series contains duplicate capability '$capability'."
    }

    if ($commitRevisions.ContainsKey($revision)) {
        throw "Engine series contains duplicate revision '$revision'."
    }

    $commitByCapability[$capability] = $commit
    $commitRevisions[$revision] = $true
}

$capabilityById = @{}
foreach ($capability in $capabilities) {
    $id = [string] $capability.id
    $phase = [string] $capability.phase
    $engineCommit = [string] $capability.engineCommit
    $summary = [string] $capability.summary
    if ($capabilityById.ContainsKey($id)) {
        throw "Porting manifest contains duplicate capability '$id'."
    }

    if ([string]::IsNullOrWhiteSpace($phase) -or [string]::IsNullOrWhiteSpace($summary)) {
        throw "Capability '$id' must define a phase and summary."
    }

    if ($engineCommit -ne [string] $commitByCapability[$id].revision) {
        throw "Capability '$id' does not point at its engine series revision."
    }

    $engineProbes = @($capability.engineProbes)
    $projectProbes = @($capability.projectProbes)
    if ($engineProbes.Count -eq 0 -or $projectProbes.Count -eq 0) {
        throw "Capability '$id' must define at least one engine and one project probe."
    }

    foreach ($probe in @($engineProbes + $projectProbes)) {
        if ([string]::IsNullOrWhiteSpace([string] $probe.path) -or
            [string]::IsNullOrWhiteSpace([string] $probe.pattern) -or
            [string]::IsNullOrWhiteSpace([string] $probe.description)) {
            throw "Capability '$id' contains an incomplete source probe."
        }

        $minimumMatches = Get-OptionalInt $probe "minimumMatches" 1
        if ($minimumMatches -lt 1) {
            throw "Capability '$id' has a probe with minimumMatches below one."
        }

        try {
            $null = [regex]::new(
                [string] $probe.pattern,
                [System.Text.RegularExpressions.RegexOptions]::Multiline,
                [TimeSpan]::FromSeconds(2))
        }
        catch {
            throw "Capability '$id' has an invalid probe regex '$($probe.pattern)': $($_.Exception.Message)"
        }
    }

    $capabilityById[$id] = $capability
}

$declaredProbeCount = 0
foreach ($capability in $capabilities) {
    $declaredProbeCount += @($capability.engineProbes).Count
    $declaredProbeCount += @($capability.projectProbes).Count
}

if ($declaredProbeCount -ne $expectedProbeCount) {
    throw "Porting manifest has $declaredProbeCount source probes; expected $expectedProbeCount."
}

if ([string] $manifest.project.minimumRevision -notmatch $shaPattern -or
    [string] $manifest.engine.upstreamBase.revision -notmatch $shaPattern -or
    [string] $manifest.engine.officialRevision -notmatch $shaPattern) {
    throw "Project minimum, engine base, and engine official revisions must be full lowercase Git SHAs."
}

if ([string] $manifest.engine.officialRevision -ne [string] $commits[-1].revision) {
    throw "Engine official revision must be the final revision in the ordered engine series."
}

$declaredBuildProjects = @($buildProjects | ForEach-Object {
    "{0}:{1}" -f [string] $_.repository, ([string] $_.path).Replace("\", "/")
} | Sort-Object -Unique)
$buildDifference = @(Compare-Object -ReferenceObject $expectedBuildProjects -DifferenceObject $declaredBuildProjects)
if ($buildProjects.Count -ne $expectedBuildProjects.Count -or $buildDifference.Count -ne 0) {
    throw "Porting manifest build projects do not match the verifier's protected build set."
}

$validatedBuildProjects = @()
foreach ($build in $buildProjects) {
    $repository = [string] $build.repository
    $path = [string] $build.path
    if ($repository -notin @("project", "engine")) {
        throw "Build project '$path' has invalid repository '$repository'."
    }

    $root = if ($repository -eq "engine") { $EngineRoot } else { $ProjectRoot }
    $prefix = if ($repository -eq "engine") { $enginePrefix } else { $projectPrefix }
    $fullPath = Resolve-ContainedFile $root $prefix $path "Build project"
    $validatedBuildProjects += [pscustomobject]@{
        Repository = $repository
        Path = $path.Replace("\", "/")
        FullPath = $fullPath
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$probeReports = [System.Collections.Generic.List[object]]::new()
$buildReports = [System.Collections.Generic.List[object]]::new()

function Add-Failure([string] $Message) {
    $script:failures.Add($Message) | Out-Null
}

function Add-Warning([string] $Message) {
    $script:warnings.Add($Message) | Out-Null
}

function Test-SourceProbes(
    [object] $Capability,
    [object[]] $Probes,
    [string] $Repository,
    [string] $Root,
    [string] $Prefix) {
    foreach ($probe in $Probes) {
        $relativePath = ([string] $probe.path).Replace("\", "/")
        $minimumMatches = Get-OptionalInt $probe "minimumMatches" 1
        $matches = 0
        $passed = $false
        try {
            $fullPath = Resolve-ContainedFile $Root $Prefix $relativePath "Source probe"
            $content = Get-Content -LiteralPath $fullPath -Raw
            $regex = [regex]::new(
                [string] $probe.pattern,
                [System.Text.RegularExpressions.RegexOptions]::Multiline,
                [TimeSpan]::FromSeconds(2))
            $matches = $regex.Matches($content).Count
            $passed = $matches -ge $minimumMatches
            if (-not $passed) {
                Add-Failure "[$($Capability.id)] $Repository probe '$relativePath' matched $matches time(s); expected at least $minimumMatches. $($probe.description)"
            }
        }
        catch {
            Add-Failure "[$($Capability.id)] $Repository probe '$relativePath' failed: $($_.Exception.Message)"
        }

        $script:probeReports.Add([ordered]@{
            capability = [string] $Capability.id
            repository = $Repository
            path = $relativePath
            description = [string] $probe.description
            minimumMatches = $minimumMatches
            matches = $matches
            passed = $passed
        }) | Out-Null
    }
}

$projectRevision = "unknown"
$engineRevision = "unknown"
$projectDirty = $false
$engineDirty = $false
try {
    $projectRevision = Get-GitText $ProjectRoot @("rev-parse", "HEAD") "Project revision lookup"
    $engineRevision = Get-GitText $EngineRoot @("rev-parse", "HEAD") "Engine revision lookup"
    $projectDirty = -not [string]::IsNullOrWhiteSpace(
        (Get-GitText $ProjectRoot @("status", "--porcelain") "Project worktree lookup"))
    $engineDirty = -not [string]::IsNullOrWhiteSpace(
        (Get-GitText $EngineRoot @("status", "--porcelain") "Engine worktree lookup"))
}
catch {
    Add-Failure $_.Exception.Message
}

if ($RequireClean) {
    if ($projectDirty) {
        Add-Failure "WTZ Project worktree is dirty while -RequireClean is active."
    }

    if ($engineDirty) {
        Add-Failure "WTZ Engine worktree is dirty while -RequireClean is active."
    }
}

$officialSeriesVerified = $false
if ($Mode -eq "Paired" -and $projectRevision -ne "unknown" -and $engineRevision -ne "unknown") {
    $minimumProjectRevision = [string] $manifest.project.minimumRevision
    $baseRevision = [string] $manifest.engine.upstreamBase.revision
    $officialRevision = [string] $manifest.engine.officialRevision
    $seriesValid = $true

    if (-not (Test-GitAncestor $ProjectRoot $minimumProjectRevision $projectRevision)) {
        Add-Failure "Project revision '$projectRevision' does not contain minimum contract revision '$minimumProjectRevision'."
        $seriesValid = $false
    }

    if ($engineRevision -ne $officialRevision) {
        Add-Failure "Paired mode requires engine revision '$officialRevision'; checkout is '$engineRevision'."
        $seriesValid = $false
    }

    if (-not (Test-GitAncestor $EngineRoot $baseRevision $engineRevision)) {
        Add-Failure "Engine revision '$engineRevision' does not descend from declared base '$baseRevision'."
        $seriesValid = $false
    }

    $previousRevision = $baseRevision
    foreach ($commit in $commits) {
        $revision = [string] $commit.revision
        $object = Invoke-GitResult $EngineRoot @("cat-file", "-e", "$revision`^{commit}")
        if ($object.ExitCode -ne 0) {
            Add-Failure "Official engine commit '$revision' is missing from the checkout."
            $seriesValid = $false
            continue
        }

        $actualTitle = Get-GitText $EngineRoot @("show", "-s", "--format=%s", $revision) "Engine commit title lookup"
        if ($actualTitle -ne [string] $commit.title) {
            Add-Failure "Engine commit '$revision' title is '$actualTitle'; expected '$($commit.title)'."
            $seriesValid = $false
        }

        if (-not (Test-GitAncestor $EngineRoot $previousRevision $revision)) {
            Add-Failure "Engine series commit '$revision' does not follow '$previousRevision'."
            $seriesValid = $false
        }

        if (-not (Test-GitAncestor $EngineRoot $revision $engineRevision)) {
            Add-Failure "Engine checkout does not contain required commit '$revision'."
            $seriesValid = $false
        }

        $previousRevision = $revision
    }

    $submodulePath = ([string] $manifest.project.submodulePath).Replace("\", "/")
    $gitlink = Get-GitText $ProjectRoot @("ls-files", "--stage", "--", $submodulePath) "Submodule gitlink lookup"
    if ($gitlink -notmatch '^160000 ([0-9a-f]{40}) 0\s+') {
        Add-Failure "Project path '$submodulePath' is not a Git submodule entry."
        $seriesValid = $false
    }
    else {
        $gitlinkRevision = $Matches[1]
        if ($gitlinkRevision -ne $officialRevision -or $gitlinkRevision -ne $engineRevision) {
            Add-Failure "Submodule gitlink '$gitlinkRevision', official engine '$officialRevision', and checkout '$engineRevision' are not paired."
            $seriesValid = $false
        }
    }

    $submoduleUrl = Get-GitText $ProjectRoot @(
        "config",
        "-f",
        ".gitmodules",
        "--get",
        "submodule.$submodulePath.url") "Submodule URL lookup"
    if ($submoduleUrl -ne [string] $manifest.project.submoduleUrl) {
        Add-Failure "Submodule URL '$submoduleUrl' does not match '$($manifest.project.submoduleUrl)'."
        $seriesValid = $false
    }

    $officialSeriesVerified = $seriesValid
}
elseif ($Mode -eq "Portable") {
    if ($engineRevision -ne [string] $manifest.engine.officialRevision) {
        Add-Warning "Portable mode accepts engine revision '$engineRevision'; official WTZ revision is '$($manifest.engine.officialRevision)'."
    }

    if ($projectRevision -ne "unknown") {
        $minimumResult = Invoke-GitResult $ProjectRoot @(
            "merge-base",
            "--is-ancestor",
            [string] $manifest.project.minimumRevision,
            $projectRevision)
        if ($minimumResult.ExitCode -ne 0) {
            Add-Warning "Portable project history does not contain or cannot resolve the official WTZ minimum revision; source probes and builds are authoritative."
        }
    }
}

foreach ($capability in $capabilities) {
    Test-SourceProbes $capability @($capability.engineProbes) "engine" $EngineRoot $enginePrefix
    Test-SourceProbes $capability @($capability.projectProbes) "project" $ProjectRoot $projectPrefix
}

if (-not $SkipBuild -and $failures.Count -eq 0) {
    foreach ($build in $validatedBuildProjects) {
        $arguments = @(
            "build",
            $build.FullPath,
            "--configuration", $Configuration,
            "--nologo"
        )

        if ($NoRestore) {
            $arguments += "--no-restore"
        }

        Write-Host "Building $($build.Repository) contract project $($build.Path)..."
        $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = "Continue"
        try {
            & dotnet @arguments
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorAction
        }

        $stopwatch.Stop()
        $passed = $exitCode -eq 0
        if (-not $passed) {
            Add-Failure "Build failed for '$($build.Path)' with exit code $exitCode."
        }

        $buildReports.Add([ordered]@{
            repository = $build.Repository
            path = $build.Path
            configuration = $Configuration
            elapsedMilliseconds = [Math]::Round($stopwatch.Elapsed.TotalMilliseconds, 3)
            exitCode = $exitCode
            passed = $passed
        }) | Out-Null
    }
}
elseif ($SkipBuild) {
    Add-Warning "Contract builds were skipped by request."
}
else {
    Add-Warning "Contract builds were skipped because static or revision checks failed."
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$reportPath = Join-Path $OutputDirectory "zlevel-port-compatibility.json"
$manifestDisplayPath = $ManifestPath
if ($ManifestPath.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    $manifestDisplayPath = $ManifestPath.Substring($projectPrefix.Length).Replace("\", "/")
}

$passedProbes = @($probeReports | Where-Object passed -EQ $true).Count
$report = [ordered]@{
    schemaVersion = 1
    contractVersion = [string] $manifest.contractVersion
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = if ($failures.Count -eq 0) { "Passed" } else { "Failed" }
    mode = $Mode
    configuration = $Configuration
    manifest = [ordered]@{
        path = $manifestDisplayPath
        sha256 = (Get-FileHash -LiteralPath $ManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    revisions = [ordered]@{
        project = $projectRevision
        engine = $engineRevision
        officialEngine = [string] $manifest.engine.officialRevision
        engineBase = [string] $manifest.engine.upstreamBase.revision
        projectDirty = $projectDirty
        engineDirty = $engineDirty
        officialSeriesVerified = $officialSeriesVerified
    }
    summary = [ordered]@{
        capabilities = $capabilities.Count
        engineCommits = $commits.Count
        probes = $probeReports.Count
        probesPassed = $passedProbes
        builds = $buildReports.Count
        buildsPassed = @($buildReports | Where-Object passed -EQ $true).Count
        warnings = $warnings.Count
        failures = $failures.Count
    }
    probes = @($probeReports)
    builds = @($buildReports)
    warnings = @($warnings)
    failures = @($failures)
}

$json = $report | ConvertTo-Json -Depth 12
[System.IO.File]::WriteAllText(
    $reportPath,
    $json,
    [System.Text.UTF8Encoding]::new($false))

if ($failures.Count -ne 0) {
    Write-Host "Z-level port compatibility failed:" -ForegroundColor Red
    foreach ($failure in $failures) {
        Write-Host "  $failure" -ForegroundColor Red
    }

    Write-Host "  report=$reportPath"
    throw "Z-level port compatibility failed with $($failures.Count) issue(s)."
}

Write-Host "Z-level port compatibility passed."
Write-Host "  contract=$($manifest.contractVersion), mode=$Mode, capabilities=$($capabilities.Count), probes=$passedProbes/$($probeReports.Count)"
Write-Host "  builds=$(@($buildReports | Where-Object passed -EQ $true).Count)/$($buildReports.Count), warnings=$($warnings.Count)"
Write-Host "  report=$reportPath"
