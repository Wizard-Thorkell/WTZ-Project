[CmdletBinding()]
param(
    [string] $ProjectRoot,

    [string] $ManifestPath,

    [string] $OutputDirectory
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

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "artifacts\zlevel-port-verifier-tests"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$verifierPath = Join-Path $PSScriptRoot "verify_zlevel_port.ps1"
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing source manifest: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $verifierPath -PathType Leaf)) {
    throw "Missing port verifier: $verifierPath"
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$caseReports = [System.Collections.Generic.List[object]]::new()

function Write-MutatedManifest([string] $Case, [scriptblock] $Mutation) {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    & $Mutation $manifest
    $caseDirectory = Join-Path $OutputDirectory $Case
    [System.IO.Directory]::CreateDirectory($caseDirectory) | Out-Null
    $path = Join-Path $caseDirectory "manifest.json"
    [System.IO.File]::WriteAllText(
        $path,
        ($manifest | ConvertTo-Json -Depth 100),
        $utf8)
    return [pscustomobject]@{
        Directory = $caseDirectory
        Manifest = $path
    }
}

function Invoke-RejectionCase(
    [string] $Case,
    [scriptblock] $Mutation,
    [string] $ExpectedPattern) {
    $paths = Write-MutatedManifest $Case $Mutation
    $reportDirectory = Join-Path $paths.Directory "report"
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $verifierPath,
        "-ProjectRoot", $ProjectRoot,
        "-ManifestPath", $paths.Manifest,
        "-OutputDirectory", $reportDirectory,
        "-Mode", "Paired",
        "-SkipBuild"
    )

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& powershell @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    $outputText = ($output -join "`n").Trim()
    $passed = $exitCode -ne 0 -and $outputText -match $ExpectedPattern
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "Rejected"
        exitCode = $exitCode
        matchedExpectedDiagnostic = $outputText -match $ExpectedPattern
        passed = $passed
        diagnostic = $outputText
    }) | Out-Null

    if (-not $passed) {
        Write-Host "Verifier self-test '$Case' did not fail closed as expected." -ForegroundColor Red
    }
}

function Invoke-AcceptanceCase(
    [string] $Case,
    [scriptblock] $Mutation,
    [string] $ExpectedWarningPattern) {
    $paths = Write-MutatedManifest $Case $Mutation
    $reportDirectory = Join-Path $paths.Directory "report"
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $verifierPath,
        "-ProjectRoot", $ProjectRoot,
        "-ManifestPath", $paths.Manifest,
        "-OutputDirectory", $reportDirectory,
        "-Mode", "Portable",
        "-SkipBuild"
    )

    $previousErrorAction = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = @(& powershell @arguments 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorAction
    }

    $outputText = ($output -join "`n").Trim()
    $reportPath = Join-Path $reportDirectory "zlevel-port-compatibility.json"
    $report = if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
        Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    }
    else {
        $null
    }

    $matchedWarning = $null -ne $report -and
        @($report.warnings | Where-Object { $_ -match $ExpectedWarningPattern }).Count -gt 0
    $passed = $exitCode -eq 0 -and
        $null -ne $report -and
        $report.status -eq "Passed" -and
        $matchedWarning
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "AcceptedWithWarning"
        exitCode = $exitCode
        matchedExpectedDiagnostic = $matchedWarning
        passed = $passed
        diagnostic = $outputText
    }) | Out-Null

    if (-not $passed) {
        Write-Host "Verifier self-test '$Case' was not accepted with the expected warning." -ForegroundColor Red
    }
}

Invoke-RejectionCase -Case "missing-capability" `
    -Mutation { param($manifest) $manifest.capabilities = @($manifest.capabilities | Select-Object -First 19) } `
    -ExpectedPattern "has 19 capabilities; expected 20"

Invoke-RejectionCase -Case "broken-engine-probe" `
    -Mutation { param($manifest) $manifest.capabilities[0].engineProbes[0].pattern = "WTZ_PORT_PROBE_MUST_NOT_EXIST" } `
    -ExpectedPattern "matched 0 time\(s\); expected at least 1"

Invoke-RejectionCase -Case "missing-source-probe" `
    -Mutation { param($manifest) $manifest.capabilities[0].engineProbes = @($manifest.capabilities[0].engineProbes | Select-Object -First 1) } `
    -ExpectedPattern "has 49 source probes; expected 50"

Invoke-RejectionCase -Case "missing-protected-build" `
    -Mutation { param($manifest) $manifest.buildProjects = @($manifest.buildProjects | Select-Object -First 1) } `
    -ExpectedPattern "protected build set"

Invoke-RejectionCase -Case "official-head-outside-series" `
    -Mutation { param($manifest) $manifest.engine.officialRevision = $manifest.engine.upstreamBase.revision } `
    -ExpectedPattern "official revision must be the final revision"

Invoke-AcceptanceCase -Case "portable-rewritten-history" `
    -Mutation { param($manifest) $manifest.project.minimumRevision = "0000000000000000000000000000000000000000" } `
    -ExpectedWarningPattern "cannot resolve the official WTZ minimum revision"

$passedCases = @($caseReports | Where-Object passed -EQ $true).Count
$summary = [ordered]@{
    schemaVersion = 1
    contractVersion = "WTZ-PORT-VERIFIER-TEST-1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = if ($passedCases -eq $caseReports.Count) { "Passed" } else { "Failed" }
    cases = $caseReports.Count
    passed = $passedCases
    results = @($caseReports)
}

$summaryPath = Join-Path $OutputDirectory "zlevel-port-verifier-tests.json"
[System.IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 10),
    $utf8)

if ($summary.status -ne "Passed") {
    throw "Z-level port verifier self-tests failed ($passedCases/$($caseReports.Count)). Report: $summaryPath"
}

Write-Host "Z-level port verifier self-tests passed."
Write-Host "  passed=$passedCases/$($caseReports.Count)"
Write-Host "  report=$summaryPath"
