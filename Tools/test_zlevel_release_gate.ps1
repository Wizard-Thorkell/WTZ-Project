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
$projectPrefix = $ProjectRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $ManifestPath = Join-Path $ProjectRoot "Docs\ZLevelReleaseManifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "artifacts\zlevel-release-gate-tests"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$runnerPath = Join-Path $PSScriptRoot "run_zlevel_release_gate.ps1"
if (-not $OutputDirectory.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Release gate self-test output must be inside the repository: $OutputDirectory"
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing source release manifest: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
    throw "Missing release gate runner: $runnerPath"
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
    return $path
}

function Invoke-Validation([string] $Manifest) {
    $arguments = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-File", $runnerPath,
        "-ManifestPath", $Manifest,
        "-ValidateOnly"
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

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = ($output -join "`n").Trim()
    }
}

function Invoke-AcceptanceCase([string] $Case, [scriptblock] $Mutation) {
    $path = Write-MutatedManifest $Case $Mutation
    $result = Invoke-Validation $path
    $passed = $result.ExitCode -eq 0 -and
        $result.Output -match "WTZ-RELEASE-1 manifest validation passed"
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "Accepted"
        exitCode = $result.ExitCode
        matchedExpectedDiagnostic = $result.Output -match "manifest validation passed"
        passed = $passed
        diagnostic = $result.Output
    }) | Out-Null
}

function Invoke-RejectionCase(
    [string] $Case,
    [scriptblock] $Mutation,
    [string] $ExpectedPattern) {
    $path = Write-MutatedManifest $Case $Mutation
    $result = Invoke-Validation $path
    $matched = $result.Output -match $ExpectedPattern
    $passed = $result.ExitCode -ne 0 -and $matched
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "Rejected"
        exitCode = $result.ExitCode
        matchedExpectedDiagnostic = $matched
        passed = $passed
        diagnostic = $result.Output
    }) | Out-Null
}

Invoke-AcceptanceCase -Case "canonical-manifest" -Mutation { param($manifest) }

Invoke-RejectionCase -Case "missing-domain" `
    -Mutation { param($manifest) $manifest.requiredDomains = @($manifest.requiredDomains | Select-Object -First 18) } `
    -ExpectedPattern "requiredDomains does not match the protected set"

Invoke-RejectionCase -Case "missing-entry" `
    -Mutation { param($manifest) $manifest.entries = @($manifest.entries | Select-Object -First 40) } `
    -ExpectedPattern "has 40 entries; expected 41"

Invoke-RejectionCase -Case "duplicate-test" `
    -Mutation { param($manifest) $manifest.entries[1].fullyQualifiedTest = $manifest.entries[0].fullyQualifiedTest } `
    -ExpectedPattern "A test may protect only one release entry; duplicate"

Invoke-RejectionCase -Case "unprotected-project" `
    -Mutation { param($manifest) $manifest.entries[0].project = "Content.Server/Content.Server.csproj" } `
    -ExpectedPattern "uses unprotected test project"

Invoke-RejectionCase -Case "missing-composite-gate" `
    -Mutation { param($manifest) $manifest.compositeGates = @($manifest.compositeGates | Select-Object -First 2) } `
    -ExpectedPattern "composite gate id set does not match the protected set"

Invoke-RejectionCase -Case "weakened-clean-policy" `
    -Mutation { param($manifest) $manifest.sourcePolicy.requireCleanProject = $false } `
    -ExpectedPattern "must require clean project and engine worktrees"

Invoke-RejectionCase -Case "reduced-visual-checks" `
    -Mutation {
        param($manifest)
        ($manifest.compositeGates | Where-Object id -EQ "real-client-visual").expectedChecks = 23
    } `
    -ExpectedPattern "must require exactly 15 captures and 24 checks"

$passedCases = @($caseReports | Where-Object passed -EQ $true).Count
$summary = [ordered]@{
    schemaVersion = 1
    contractVersion = "WTZ-RELEASE-GATE-TEST-1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = if ($passedCases -eq $caseReports.Count) { "Passed" } else { "Failed" }
    cases = $caseReports.Count
    passed = $passedCases
    results = @($caseReports)
}

$summaryPath = Join-Path $OutputDirectory "zlevel-release-gate-tests.json"
[System.IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 10),
    $utf8)

if ($summary.status -ne "Passed") {
    foreach ($case in @($caseReports | Where-Object passed -NE $true)) {
        Write-Host "Release gate self-test '$($case.case)' failed: $($case.diagnostic)" -ForegroundColor Red
    }

    throw "Z-level release gate self-tests failed ($passedCases/$($caseReports.Count)). Report: $summaryPath"
}

Write-Host "Z-level release gate self-tests passed."
Write-Host "  passed=$passedCases/$($caseReports.Count)"
Write-Host "  report=$summaryPath"
