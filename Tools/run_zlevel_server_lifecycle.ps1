[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateRange(1, 64)]
    [int] $WarmupCycles = 8,

    [ValidateRange(1, 2048)]
    [int] $Cycles = 128,

    [string] $OutputDirectory,

    [switch] $RequireReleaseEnvelope,

    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Content.IntegrationTests\Content.IntegrationTests.csproj"
$releaseEnvelope = [ordered]@{
    MinimumWarmupCycles = 8
    MinimumMeasuredCycles = 128
    MaximumCycleP95Milliseconds = 30.0
    MaximumCycleP99Milliseconds = 40.0
    MaximumCycleMilliseconds = 66.667
    MaximumAllocatedBytesPerCycle = 1MB
    MaximumRetainedHeapDeltaBytes = 2MB
}

if ($RequireReleaseEnvelope) {
    if ($Configuration -ne "Release") {
        throw "The lifecycle release envelope requires -Configuration Release."
    }

    if ($WarmupCycles -lt $releaseEnvelope.MinimumWarmupCycles -or
        $Cycles -lt $releaseEnvelope.MinimumMeasuredCycles) {
        throw (("The lifecycle release envelope requires warmup>={0} and cycles>={1}.") -f
            $releaseEnvelope.MinimumWarmupCycles,
            $releaseEnvelope.MinimumMeasuredCycles)
    }
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-server-lifecycle"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$reportPath = Join-Path $OutputDirectory "zlevel-server-lifecycle.json"
Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue

$arguments = @(
    "test",
    $project,
    "--configuration", $Configuration,
    "--filter", "Name=RepeatedNativeMapLifecycleReturnsOwnedCachesToBaseline",
    "--logger", "console;verbosity=minimal"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$previous = @{
    Output = $env:WTZ_ZLEVEL_LIFECYCLE_DIR
    Warmup = $env:WTZ_ZLEVEL_LIFECYCLE_WARMUP
    Cycles = $env:WTZ_ZLEVEL_LIFECYCLE_CYCLES
    RequireServerGC = $env:WTZ_ZLEVEL_LIFECYCLE_REQUIRE_SERVER_GC
    ServerGC = $env:DOTNET_gcServer
}

try {
    $env:WTZ_ZLEVEL_LIFECYCLE_DIR = $OutputDirectory
    $env:WTZ_ZLEVEL_LIFECYCLE_WARMUP = $WarmupCycles.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_LIFECYCLE_CYCLES = $Cycles.ToString([System.Globalization.CultureInfo]::InvariantCulture)
    $env:WTZ_ZLEVEL_LIFECYCLE_REQUIRE_SERVER_GC = "1"
    $env:DOTNET_gcServer = "1"

    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Z-level server lifecycle test failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:WTZ_ZLEVEL_LIFECYCLE_DIR = $previous.Output
    $env:WTZ_ZLEVEL_LIFECYCLE_WARMUP = $previous.Warmup
    $env:WTZ_ZLEVEL_LIFECYCLE_CYCLES = $previous.Cycles
    $env:WTZ_ZLEVEL_LIFECYCLE_REQUIRE_SERVER_GC = $previous.RequireServerGC
    $env:DOTNET_gcServer = $previous.ServerGC
}

if (-not (Test-Path -LiteralPath $reportPath -PathType Leaf)) {
    throw "Missing expected server lifecycle report: $reportPath."
}

$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
if ($report.schemaVersion -ne 1) {
    throw "Unsupported server lifecycle report schema: $($report.schemaVersion)."
}

if ($report.settings.warmupCycles -ne $WarmupCycles -or
    $report.settings.measuredCycles -ne $Cycles) {
    throw "The lifecycle report settings do not match the requested profile."
}

if (-not $report.settings.requireServerGarbageCollection -or
    -not $report.host.serverGarbageCollection) {
    throw "The lifecycle report was produced without the required Server GC testhost."
}

if ($report.host.buildConfiguration -ne $Configuration) {
    throw ("The lifecycle report build is '{0}'; expected '{1}'." -f
        $report.host.buildConfiguration,
        $Configuration)
}

$baselineJson = $report.baseline | ConvertTo-Json -Compress
$finalJson = $report.finalState | ConvertTo-Json -Compress
if ($baselineJson -ne $finalJson) {
    throw "The lifecycle final cache state differs from its baseline."
}

if ($report.generationTwoCollections -lt 1) {
    throw "The lifecycle run did not observe a full GC collection."
}

$allocatedBytesPerCycle = [double] $report.allocatedBytes / $report.settings.measuredCycles
$releaseEnvelopeSummary = $null
if ($RequireReleaseEnvelope) {
    $failures = [System.Collections.Generic.List[string]]::new()
    $latency = $report.cycleLatency

    if ($latency.p95Milliseconds -gt $releaseEnvelope.MaximumCycleP95Milliseconds) {
        $failures.Add(("cycle p95 is {0:N3} ms; maximum is {1:N3} ms" -f
            $latency.p95Milliseconds,
            $releaseEnvelope.MaximumCycleP95Milliseconds))
    }
    if ($latency.p99Milliseconds -gt $releaseEnvelope.MaximumCycleP99Milliseconds) {
        $failures.Add(("cycle p99 is {0:N3} ms; maximum is {1:N3} ms" -f
            $latency.p99Milliseconds,
            $releaseEnvelope.MaximumCycleP99Milliseconds))
    }
    if ($latency.maxMilliseconds -gt $releaseEnvelope.MaximumCycleMilliseconds) {
        $failures.Add(("cycle maximum is {0:N3} ms; maximum is {1:N3} ms" -f
            $latency.maxMilliseconds,
            $releaseEnvelope.MaximumCycleMilliseconds))
    }
    if ($allocatedBytesPerCycle -gt $releaseEnvelope.MaximumAllocatedBytesPerCycle) {
        $failures.Add(("allocation is {0:N0} bytes/cycle; maximum is {1:N0}" -f
            $allocatedBytesPerCycle,
            $releaseEnvelope.MaximumAllocatedBytesPerCycle))
    }
    if ($report.retainedHeapDeltaBytes -gt $releaseEnvelope.MaximumRetainedHeapDeltaBytes) {
        $failures.Add(("retained heap delta is {0:N0} bytes; maximum is {1:N0}" -f
            $report.retainedHeapDeltaBytes,
            $releaseEnvelope.MaximumRetainedHeapDeltaBytes))
    }

    if ($failures.Count -gt 0) {
        throw "Z-level lifecycle release envelope failed: $($failures -join '; ')."
    }

    $releaseEnvelopeSummary =
        "p95={0:N3} ms, p99={1:N3} ms, allocated={2:N0} bytes/cycle, retained={3:N0} bytes" -f
        $latency.p95Milliseconds,
        $latency.p99Milliseconds,
        $allocatedBytesPerCycle,
        $report.retainedHeapDeltaBytes
}

Write-Host "Z-level server lifecycle report written to $reportPath"
Write-Host ("  build={0}, server-gc={1}, warmup/cycles={2}/{3}" -f
    $report.host.buildConfiguration,
    $report.host.serverGarbageCollection,
    $report.settings.warmupCycles,
    $report.settings.measuredCycles)
Write-Host ("  cycle p50/p95/p99/max={0:N3}/{1:N3}/{2:N3}/{3:N3} ms" -f
    $report.cycleLatency.p50Milliseconds,
    $report.cycleLatency.p95Milliseconds,
    $report.cycleLatency.p99Milliseconds,
    $report.cycleLatency.maxMilliseconds)
Write-Host ("  allocated={0:N0} bytes ({1:N0}/cycle), retained={2:N0} bytes" -f
    $report.allocatedBytes,
    $allocatedBytesPerCycle,
    $report.retainedHeapDeltaBytes)
Write-Host ("  GC collections gen0/gen1/gen2={0}/{1}/{2}" -f
    $report.generationZeroCollections,
    $report.generationOneCollections,
    $report.generationTwoCollections)
if ($null -ne $releaseEnvelopeSummary) {
    Write-Host "  lifecycle release envelope: PASS ($releaseEnvelopeSummary)"
}
