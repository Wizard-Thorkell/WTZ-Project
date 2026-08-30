[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateRange(3, 32)]
    [int] $Floors = 10,

    [ValidateRange(2, 64)]
    [int] $Sessions = 32,

    [ValidateRange(1, 128)]
    [int] $WarmupIterations = 8,

    [ValidateRange(1, 2048)]
    [int] $Iterations = 128,

    [ValidateRange(1, 64)]
    [int] $CandidateCopies = 8,

    [string] $OutputDirectory,

    [switch] $RequireReleaseEnvelope,

    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Content.IntegrationTests\Content.IntegrationTests.csproj"
$releaseEnvelope = [ordered]@{
    Floors = 10
    Sessions = 32
    MinimumWarmupIterations = 8
    MinimumMeasuredIterations = 128
    CandidateCopies = 8
    MaximumPvsSchedulerFrameP95Milliseconds = 30.0
    MaximumPvsSchedulerFrameP99Milliseconds = 33.333
    MaximumPvsSchedulerFrameMilliseconds = 66.667
    MinimumVisibilityContextCacheHitPercent = 85.0
    MaximumAllocatedBytesPerIteration = 24KB
}

if ($RequireReleaseEnvelope) {
    if ($Configuration -ne "Release") {
        throw "The release envelope requires -Configuration Release."
    }

    if ($Floors -ne $releaseEnvelope.Floors -or
        $Sessions -ne $releaseEnvelope.Sessions -or
        $WarmupIterations -lt $releaseEnvelope.MinimumWarmupIterations -or
        $Iterations -lt $releaseEnvelope.MinimumMeasuredIterations -or
        $CandidateCopies -ne $releaseEnvelope.CandidateCopies) {
        throw (("The release envelope requires floors={0}, sessions={1}, warmup>={2}, " +
            "iterations>={3}, and candidate-copies={4}.") -f
            $releaseEnvelope.Floors,
            $releaseEnvelope.Sessions,
            $releaseEnvelope.MinimumWarmupIterations,
            $releaseEnvelope.MinimumMeasuredIterations,
            $releaseEnvelope.CandidateCopies)
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-server-soak"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$reportPath = Join-Path $OutputDirectory "zlevel-server-soak.json"
Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "test",
    $project,
    "--configuration", $Configuration,
    "--filter", "FullyQualifiedName~Content.IntegrationTests.Tests.ZLevel.ZLevelServerSoakTest",
    "--logger", "console;verbosity=minimal"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$previous = @{
    Output = $env:WTZ_ZLEVEL_SOAK_DIR
    Floors = $env:WTZ_ZLEVEL_SOAK_FLOORS
    Sessions = $env:WTZ_ZLEVEL_SOAK_SESSIONS
    Warmup = $env:WTZ_ZLEVEL_SOAK_WARMUP
    Iterations = $env:WTZ_ZLEVEL_SOAK_ITERATIONS
    CandidateCopies = $env:WTZ_ZLEVEL_SOAK_CANDIDATE_COPIES
}

try {
    $env:WTZ_ZLEVEL_SOAK_DIR = $OutputDirectory
    $env:WTZ_ZLEVEL_SOAK_FLOORS = $Floors.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_SOAK_SESSIONS = $Sessions.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_SOAK_WARMUP = $WarmupIterations.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_SOAK_ITERATIONS = $Iterations.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_SOAK_CANDIDATE_COPIES = $CandidateCopies.ToString([System.Globalization.CultureInfo]::InvariantCulture)

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Z-level server soak failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:WTZ_ZLEVEL_SOAK_DIR = $previous.Output
    $env:WTZ_ZLEVEL_SOAK_FLOORS = $previous.Floors
    $env:WTZ_ZLEVEL_SOAK_SESSIONS = $previous.Sessions
    $env:WTZ_ZLEVEL_SOAK_WARMUP = $previous.Warmup
    $env:WTZ_ZLEVEL_SOAK_ITERATIONS = $previous.Iterations
    $env:WTZ_ZLEVEL_SOAK_CANDIDATE_COPIES = $previous.CandidateCopies
}

if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Missing expected server soak report: $reportPath."
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.schemaVersion -ne 6) {
    throw "Unsupported server soak report schema: $($report.schemaVersion)."
}

$expected = @{
    floorCount = $Floors
    sessionCount = $Sessions
    warmupIterations = $WarmupIterations
    measuredIterations = $Iterations
    candidateCopiesPerTile = $CandidateCopies
}

foreach ($entry in $expected.GetEnumerator()) {
    if ($report.settings.($entry.Key) -ne $entry.Value) {
        throw "Server soak report setting '$($entry.Key)' is $($report.settings.($entry.Key)); expected $($entry.Value)."
    }
}

$releaseEnvelopeSummary = $null
if ($RequireReleaseEnvelope) {
    $failures = [System.Collections.Generic.List[string]]::new()
    $scheduler = $report.measured.pvsScheduler
    $frameLatency = $report.measured.pvsSchedulerFrameLatency
    $allocatedBytesPerIteration =
        [double] $report.measured.allocatedBytes / $report.measured.iterations

    if ($report.host.buildConfiguration -ne "Release") {
        $failures.Add("report build configuration is '$($report.host.buildConfiguration)'")
    }
    if ($frameLatency.p95Milliseconds -gt
        $releaseEnvelope.MaximumPvsSchedulerFrameP95Milliseconds) {
        $failures.Add(("PVS frame p95 is {0:N3} ms; maximum is {1:N3} ms" -f
            $frameLatency.p95Milliseconds,
            $releaseEnvelope.MaximumPvsSchedulerFrameP95Milliseconds))
    }
    if ($frameLatency.p99Milliseconds -gt
        $releaseEnvelope.MaximumPvsSchedulerFrameP99Milliseconds) {
        $failures.Add(("PVS frame p99 is {0:N3} ms; maximum is {1:N3} ms" -f
            $frameLatency.p99Milliseconds,
            $releaseEnvelope.MaximumPvsSchedulerFrameP99Milliseconds))
    }
    if ($frameLatency.maxMilliseconds -gt
        $releaseEnvelope.MaximumPvsSchedulerFrameMilliseconds) {
        $failures.Add(("PVS frame maximum is {0:N3} ms; maximum is {1:N3} ms" -f
            $frameLatency.maxMilliseconds,
            $releaseEnvelope.MaximumPvsSchedulerFrameMilliseconds))
    }
    if ($scheduler.visibilityContextCacheHitPercent -lt
        $releaseEnvelope.MinimumVisibilityContextCacheHitPercent) {
        $failures.Add(("PVS context-cache hit rate is {0:N2}%; minimum is {1:N2}%" -f
            $scheduler.visibilityContextCacheHitPercent,
            $releaseEnvelope.MinimumVisibilityContextCacheHitPercent))
    }
    if ($allocatedBytesPerIteration -gt
        $releaseEnvelope.MaximumAllocatedBytesPerIteration) {
        $failures.Add(("allocation is {0:N0} bytes/iteration; maximum is {1:N0}" -f
            $allocatedBytesPerIteration,
            $releaseEnvelope.MaximumAllocatedBytesPerIteration))
    }
    if ($scheduler.deferredRefreshes -ne 0 -or $scheduler.budgetExhaustions -ne 0) {
        $failures.Add(("PVS scheduler has {0} deferred refreshes and {1} budget exhaustions" -f
            $scheduler.deferredRefreshes,
            $scheduler.budgetExhaustions))
    }

    if ($failures.Count -gt 0) {
        throw "Z-level release envelope failed: $($failures -join '; ')."
    }

    $releaseEnvelopeSummary =
        "p95={0:N3} ms, p99={1:N3} ms, cache={2:N2}%, allocated={3:N0} bytes/iteration" -f
        $frameLatency.p95Milliseconds,
        $frameLatency.p99Milliseconds,
        $scheduler.visibilityContextCacheHitPercent,
        $allocatedBytesPerIteration
}

Write-Host "Z-level server soak report written to $reportPath"
Write-Host ("  sessions={0}, floors={1}, entities={2}, iterations={3}" -f `
    $report.settings.sessionCount,
    $report.settings.floorCount,
    $report.fixture.candidateEntityCount,
    $report.measured.iterations)
Write-Host ("  elapsed={0:N3} ms, per-session-refresh={1:N6} ms, allocated={2:N0} bytes" -f `
    $report.measured.elapsedMilliseconds,
    $report.measured.millisecondsPerSessionRefresh,
    $report.measured.allocatedBytes)
Write-Host ("  PVS candidates={0:N0}, checks={1:N0}, budget-exhaustions={2}" -f `
    $report.measured.sharedMetrics.pvsCandidates,
    $report.measured.sharedMetrics.pvsVisibilityChecks,
    $report.measured.sharedMetrics.pvsBudgetExhaustions)
Write-Host ("  PVS latency p50/p95/p99/max={0:N3}/{1:N3}/{2:N3}/{3:N3} ms" -f `
    $report.measured.pvsRefreshLatency.p50Milliseconds,
    $report.measured.pvsRefreshLatency.p95Milliseconds,
    $report.measured.pvsRefreshLatency.p99Milliseconds,
    $report.measured.pvsRefreshLatency.maxMilliseconds)
Write-Host ("  PVS scheduler-frame p50/p95/p99/max={0:N3}/{1:N3}/{2:N3}/{3:N3} ms" -f `
    $report.measured.pvsSchedulerFrameLatency.p50Milliseconds,
    $report.measured.pvsSchedulerFrameLatency.p95Milliseconds,
    $report.measured.pvsSchedulerFrameLatency.p99Milliseconds,
    $report.measured.pvsSchedulerFrameLatency.maxMilliseconds)
Write-Host ("  PVS scheduler updates/scheduled/deferred/exhausted={0}/{1}/{2}/{3}, max-batch={4}" -f `
    $report.measured.pvsScheduler.updates,
    $report.measured.pvsScheduler.scheduledRefreshes,
    $report.measured.pvsScheduler.deferredRefreshes,
    $report.measured.pvsScheduler.budgetExhaustions,
    $report.measured.pvsScheduler.maxRefreshesPerUpdate)
Write-Host ("  PVS context cache hits/misses/rate/entries={0}/{1}/{2:N2}%/{3}" -f `
    $report.measured.pvsScheduler.visibilityContextCacheHits,
    $report.measured.pvsScheduler.visibilityContextCacheMisses,
    $report.measured.pvsScheduler.visibilityContextCacheHitPercent,
    $report.measured.pvsScheduler.visibilityContextCacheEntries)
Write-Host "  stage attribution:"
foreach ($stage in $report.measured.stages) {
    Write-Host ("    {0}: p50/p95/p99/max={1:N3}/{2:N3}/{3:N3}/{4:N3} ms, allocated={5:N0} bytes" -f `
        $stage.name,
        $stage.latency.p50Milliseconds,
        $stage.latency.p95Milliseconds,
        $stage.latency.p99Milliseconds,
        $stage.latency.maxMilliseconds,
        $stage.allocatedBytes)
}
Write-Host ("  iterations with/without GC collection={0}/{1}" -f `
    $report.measured.collectionCorrelation.iterationsWithCollection,
    $report.measured.collectionCorrelation.iterationsWithoutCollection)
if ($null -ne $releaseEnvelopeSummary) {
    Write-Host "  release envelope: PASS ($releaseEnvelopeSummary)"
}
