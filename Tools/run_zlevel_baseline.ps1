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
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-baseline"
}

$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$expectedSnapshots = @(3, 6, 10) | ForEach-Object {
    Join-Path $OutputDirectory "zlevel-baseline-$_-floors.json"
}

foreach ($snapshot in $expectedSnapshots) {
    Remove-Item -LiteralPath $snapshot -Force -ErrorAction SilentlyContinue
}

$arguments = @(
    "test",
    $project,
    "--configuration", $Configuration,
    "--filter", "FullyQualifiedName~Content.IntegrationTests.Tests.ZLevel.ZLevelStressBaselineTest",
    "--logger", "console;verbosity=minimal"
)

if ($NoBuild) {
    $arguments += "--no-build"
}

$previousOutputDirectory = $env:WTZ_ZLEVEL_BASELINE_DIR
try {
    $env:WTZ_ZLEVEL_BASELINE_DIR = $OutputDirectory
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Z-level baseline runner failed with exit code $LASTEXITCODE."
    }
}
finally {
    $env:WTZ_ZLEVEL_BASELINE_DIR = $previousOutputDirectory
}

$missingSnapshots = $expectedSnapshots | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) }
if ($missingSnapshots.Count -ne 0) {
    throw "Missing expected baseline snapshots: $($missingSnapshots -join ', ')."
}

Write-Host "Z-level baselines written to $OutputDirectory"
$expectedSnapshots | Sort-Object | ForEach-Object { Write-Host "  $(Split-Path -Leaf $_)" }
