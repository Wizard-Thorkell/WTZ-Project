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
    $ManifestPath = Join-Path $ProjectRoot "Docs\ZLevelFinalManifest.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $ProjectRoot "artifacts\zlevel-final-gate-tests"
}

$ManifestPath = [System.IO.Path]::GetFullPath($ManifestPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$runnerPath = Join-Path $PSScriptRoot "run_zlevel_final_gate.ps1"
if (-not $OutputDirectory.StartsWith($projectPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Final-gate self-test output must be inside the repository: $OutputDirectory"
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "Missing source final manifest: $ManifestPath"
}

if (-not (Test-Path -LiteralPath $runnerPath -PathType Leaf)) {
    throw "Missing final-gate runner: $runnerPath"
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$runId = "{0}-{1}-{2}" -f
    [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ"),
    $PID,
    [Guid]::NewGuid().ToString("N").Substring(0, 8)
$runDirectory = Join-Path $OutputDirectory $runId
[System.IO.Directory]::CreateDirectory($runDirectory) | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
$caseReports = [System.Collections.Generic.List[object]]::new()

function Write-MutatedManifest([string] $Case, [scriptblock] $Mutation) {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    & $Mutation $manifest
    $caseDirectory = Join-Path $runDirectory $Case
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
    $matched = $result.Output -match "WTZ-P0-P8-1 manifest validation passed"
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "Accepted"
        exitCode = $result.ExitCode
        matchedExpectedDiagnostic = $matched
        passed = $result.ExitCode -eq 0 -and $matched
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
    $caseReports.Add([ordered]@{
        case = $Case
        expected = "Rejected"
        exitCode = $result.ExitCode
        matchedExpectedDiagnostic = $matched
        passed = $result.ExitCode -ne 0 -and $matched
        diagnostic = $result.Output
    }) | Out-Null
}

Invoke-AcceptanceCase -Case "canonical-manifest" -Mutation { param($manifest) }

Invoke-RejectionCase -Case "missing-phase" `
    -Mutation { param($manifest) $manifest.requiredPhases = @($manifest.requiredPhases | Select-Object -First 8) } `
    -ExpectedPattern "Required phase set does not match the protected set"

Invoke-RejectionCase -Case "missing-document" `
    -Mutation { param($manifest) $manifest.requiredDocuments = @($manifest.requiredDocuments | Select-Object -First 15) } `
    -ExpectedPattern "Required document set does not match the protected set"

Invoke-RejectionCase -Case "missing-composite" `
    -Mutation { param($manifest) $manifest.compositeGates = @($manifest.compositeGates | Select-Object -First 1) } `
    -ExpectedPattern "Composite gate id set does not match the protected set"

Invoke-RejectionCase -Case "changed-engine" `
    -Mutation { param($manifest) $manifest.sourcePolicy.engineRevision = "0000000000000000000000000000000000000000" } `
    -ExpectedPattern "Engine revision is"

Invoke-RejectionCase -Case "weakened-clean-policy" `
    -Mutation { param($manifest) $manifest.sourcePolicy.requireCleanProject = $false } `
    -ExpectedPattern "must require remote matching and clean project/engine worktrees"

Invoke-RejectionCase -Case "missing-health-test" `
    -Mutation { param($manifest) $manifest.operationalHealthTests = @($manifest.operationalHealthTests | Select-Object -First 3) } `
    -ExpectedPattern "Operational health test set does not match the protected set"

Invoke-RejectionCase -Case "weakened-endurance-limit" `
    -Mutation {
        param($manifest)
        ($manifest.performanceProfiles | Where-Object id -EQ "server-soak-endurance").limits.maximumP95Milliseconds = 31.0
    } `
    -ExpectedPattern "Endurance p95 limit is"

Invoke-RejectionCase -Case "missing-pilot-check" `
    -Mutation { param($manifest) $manifest.pilotContract.requiredChecks = @($manifest.pilotContract.requiredChecks | Select-Object -First 11) } `
    -ExpectedPattern "Pilot check set does not match the protected set"

Invoke-RejectionCase -Case "weakened-pilot-notes-policy" `
    -Mutation { param($manifest) $manifest.pilotContract.requireCheckNotes = $false } `
    -ExpectedPattern "must require evidence notes for every check"

Invoke-RejectionCase -Case "claims-unrestricted-readiness" `
    -Mutation { param($manifest) $manifest.readiness.unrestrictedPublicServer = "Certified" } `
    -ExpectedPattern "Unrestricted-server policy is"

Invoke-RejectionCase -Case "missing-external-condition" `
    -Mutation { param($manifest) $manifest.externalLaunchConditions = @($manifest.externalLaunchConditions | Select-Object -First 4) } `
    -ExpectedPattern "External launch condition set does not match the protected set"

$passedCases = @($caseReports | Where-Object passed -EQ $true).Count
$summary = [ordered]@{
    schemaVersion = 1
    contractVersion = "WTZ-P0-P8-GATE-TEST-1"
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    status = if ($passedCases -eq $caseReports.Count) { "Passed" } else { "Failed" }
    cases = $caseReports.Count
    passed = $passedCases
    results = @($caseReports)
}
$summaryPath = Join-Path $runDirectory "zlevel-final-gate-tests.json"
[System.IO.File]::WriteAllText(
    $summaryPath,
    ($summary | ConvertTo-Json -Depth 10),
    $utf8)

if ($summary.status -ne "Passed") {
    foreach ($case in @($caseReports | Where-Object passed -NE $true)) {
        Write-Host "Final-gate self-test '$($case.case)' failed: $($case.diagnostic)" -ForegroundColor Red
    }

    throw "Z-level final-gate self-tests failed ($passedCases/$($caseReports.Count)). Report: $summaryPath"
}

Write-Host "Z-level final-gate self-tests passed."
Write-Host "  passed=$passedCases/$($caseReports.Count)"
Write-Host "  report=$summaryPath"
