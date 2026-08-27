[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string] $OutputDirectory,

    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "Content.IntegrationTests\Content.IntegrationTests.csproj"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-trace-benchmark"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$snapshot = Join-Path $OutputDirectory "zlevel-trace-benchmark.json"
Remove-Item -LiteralPath $snapshot -Force -ErrorAction SilentlyContinue

$arguments = @(
    "test",
    $project,
    "--configuration", $Configuration,
    "--filter", "FullyQualifiedName~Content.IntegrationTests.Tests.ZLevel.ZLevelTraceBenchmarkTest",
    "--logger", "console;verbosity=minimal"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$previousOutputDirectory = $env:WTZ_ZLEVEL_TRACE_BENCHMARK_DIR
try {
    $env:WTZ_ZLEVEL_TRACE_BENCHMARK_DIR = $OutputDirectory
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "ZLevelTrace benchmark runner failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:WTZ_ZLEVEL_TRACE_BENCHMARK_DIR = $previousOutputDirectory
}

if (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) {
    throw "Missing expected ZLevelTrace benchmark snapshot: $snapshot."
}

Write-Host "ZLevelTrace benchmark written to $snapshot"
