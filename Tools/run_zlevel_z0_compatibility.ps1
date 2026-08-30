[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [string] $MatrixPath,

    [string] $OutputDirectory,

    [switch] $NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$repoPrefix = $repoRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

if ([string]::IsNullOrWhiteSpace($MatrixPath)) {
    $MatrixPath = Join-Path $repoRoot "Docs\ZLevelZZeroCompatibility.json"
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repoRoot "artifacts\zlevel-z0-compatibility"
}

$MatrixPath = [System.IO.Path]::GetFullPath($MatrixPath)
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

if (-not $MatrixPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Compatibility matrix must be inside the repository: $MatrixPath"
}

if (-not (Test-Path -LiteralPath $MatrixPath -PathType Leaf)) {
    throw "Missing Z 0 compatibility matrix: $MatrixPath"
}

$matrixRelativePath = $MatrixPath.Substring($repoPrefix.Length).Replace("\", "/")

function Resolve-RepositoryPath([string] $RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or
        [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Project path must be a non-empty repository-relative path: '$RelativePath'."
    }

    $fullPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $RelativePath))
    if (-not $fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Project path escapes the repository: '$RelativePath'."
    }

    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "Matrix project does not exist: '$RelativePath'."
    }

    return $fullPath
}

$requiredDomains = @(
    "atmosphere",
    "combat",
    "construction",
    "core",
    "engine-map",
    "entity-position",
    "gravity",
    "interaction",
    "mapping",
    "navigation",
    "rendering",
    "serialization",
    "sound",
    "visibility",
    "weather"
)

$matrix = Get-Content -LiteralPath $MatrixPath -Raw | ConvertFrom-Json
if ($matrix.schemaVersion -ne 1) {
    throw "Unsupported Z 0 compatibility matrix schema: $($matrix.schemaVersion)."
}

if ([string]::IsNullOrWhiteSpace([string] $matrix.contractVersion)) {
    throw "Compatibility matrix contractVersion must not be empty."
}

$declaredDomains = @($matrix.requiredDomains | ForEach-Object { [string] $_ } | Sort-Object -Unique)
$expectedDomains = @($requiredDomains | Sort-Object)
$domainDifference = @(Compare-Object -ReferenceObject $expectedDomains -DifferenceObject $declaredDomains)
if ($domainDifference.Count -ne 0) {
    throw "Compatibility matrix requiredDomains does not match the runner's protected domain set."
}

$entries = @($matrix.entries)
if ($entries.Count -eq 0) {
    throw "Compatibility matrix has no test entries."
}

$ids = @{}
$tests = @{}
$validatedEntries = @()
foreach ($entry in $entries) {
    $id = [string] $entry.id
    $domain = [string] $entry.domain
    $repository = [string] $entry.repository
    $contract = [string] $entry.contract
    $project = ([string] $entry.project).Replace("\", "/")
    $fullyQualifiedTest = [string] $entry.fullyQualifiedTest

    if ($id -notmatch '^[a-z0-9]+(?:[.-][a-z0-9]+)*$') {
        throw "Invalid compatibility entry id: '$id'."
    }

    if ($ids.ContainsKey($id)) {
        throw "Duplicate compatibility entry id: '$id'."
    }

    if ($tests.ContainsKey($fullyQualifiedTest)) {
        throw "A test may protect only one matrix entry; duplicate: '$fullyQualifiedTest'."
    }

    if ($expectedDomains -notcontains $domain) {
        throw "Entry '$id' uses unknown domain '$domain'."
    }

    if ($repository -notin @("project", "engine")) {
        throw "Entry '$id' has invalid repository '$repository'."
    }

    if ([string]::IsNullOrWhiteSpace($contract)) {
        throw "Entry '$id' has an empty contract."
    }

    if ($fullyQualifiedTest -notmatch '^[A-Za-z_][A-Za-z0-9_.]+$') {
        throw "Entry '$id' has an invalid fully-qualified test name."
    }

    $isEngineProject = $project.StartsWith("RobustToolbox/", [System.StringComparison]::OrdinalIgnoreCase)
    if (($repository -eq "engine") -ne $isEngineProject) {
        throw "Entry '$id' repository ownership does not match project '$project'."
    }

    $projectPath = Resolve-RepositoryPath $project
    $ids.Add($id, $true)
    $tests.Add($fullyQualifiedTest, $true)
    $validatedEntries += [pscustomobject]@{
        Id = $id
        Domain = $domain
        Repository = $repository
        Contract = $contract
        Project = $project
        ProjectPath = $projectPath
        FullyQualifiedTest = $fullyQualifiedTest
    }
}

foreach ($domain in $expectedDomains) {
    if (-not ($validatedEntries | Where-Object Domain -EQ $domain)) {
        throw "Protected domain '$domain' has no compatibility entry."
    }
}

[System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
Get-ChildItem -LiteralPath $OutputDirectory -Filter "*.trx" -File -ErrorAction SilentlyContinue |
    Remove-Item -Force
$reportPath = Join-Path $OutputDirectory "zlevel-z0-compatibility.json"
Remove-Item -LiteralPath $reportPath -Force -ErrorAction SilentlyContinue

$projectReports = @()
$verifiedTests = @()
$groups = @($validatedEntries | Group-Object Project | Sort-Object Name)
foreach ($group in $groups) {
    $groupEntries = @($group.Group)
    $projectPath = $groupEntries[0].ProjectPath
    $projectName = [System.IO.Path]::GetFileNameWithoutExtension($projectPath)
    $slug = $projectName -replace '[^A-Za-z0-9_.-]', '-'
    $trxName = "zlevel-z0-$slug.trx"
    $trxPath = Join-Path $OutputDirectory $trxName
    $filter = @($groupEntries | ForEach-Object {
        "FullyQualifiedName=$($_.FullyQualifiedTest)"
    }) -join "|"

    $arguments = @(
        "test",
        $projectPath,
        "--configuration", $Configuration,
        "--filter", $filter,
        "--logger", "trx;LogFileName=$trxName",
        "--results-directory", $OutputDirectory,
        "--nologo"
    )

    if ($NoBuild) {
        $arguments += @("--no-build", "--no-restore")
    }

    Write-Host "Running $($groupEntries.Count) Z 0 contracts in $($group.Name)..."
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Z 0 compatibility tests failed for '$($group.Name)' with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        throw "Missing TRX result for '$($group.Name)': $trxPath"
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
            throw "Declared compatibility test was not discovered: '$($entry.FullyQualifiedTest)'."
        }

        if ($outcomes[$entry.FullyQualifiedTest] -ne "Passed") {
            throw "Compatibility test '$($entry.FullyQualifiedTest)' completed as '$($outcomes[$entry.FullyQualifiedTest])'."
        }

        $verifiedTests += [ordered]@{
            id = $entry.Id
            domain = $entry.Domain
            repository = $entry.Repository
            fullyQualifiedTest = $entry.FullyQualifiedTest
            outcome = "Passed"
        }
    }

    foreach ($actualTest in $outcomes.Keys) {
        if (-not $expectedGroupTests.ContainsKey($actualTest)) {
            throw "Compatibility filter executed an undeclared test: '$actualTest'."
        }
    }

    if ($outcomes.Count -ne $groupEntries.Count) {
        throw "TRX result count for '$($group.Name)' is $($outcomes.Count); expected $($groupEntries.Count)."
    }

    $projectReports += [ordered]@{
        project = $group.Name
        expected = $groupEntries.Count
        executed = $outcomes.Count
        passed = @($outcomes.Values | Where-Object { $_ -eq "Passed" }).Count
        trx = $trxName
    }
}

$projectRevision = (& git -C $repoRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve WTZ Project revision."
}

$engineRoot = Join-Path $repoRoot "RobustToolbox"
$engineRevision = (& git -C $engineRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Unable to resolve WTZ Engine revision."
}

$report = [ordered]@{
    schemaVersion = 1
    contractVersion = [string] $matrix.contractVersion
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    configuration = $Configuration
    matrix = [ordered]@{
        path = $matrixRelativePath
        sha256 = (Get-FileHash -LiteralPath $MatrixPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    revisions = [ordered]@{
        project = $projectRevision
        engine = $engineRevision
    }
    summary = [ordered]@{
        domains = $expectedDomains.Count
        projects = $projectReports.Count
        declared = $validatedEntries.Count
        executed = $verifiedTests.Count
        passed = @($verifiedTests | Where-Object outcome -EQ "Passed").Count
    }
    projects = $projectReports
    tests = $verifiedTests
}

$json = $report | ConvertTo-Json -Depth 10
[System.IO.File]::WriteAllText(
    $reportPath,
    $json,
    [System.Text.UTF8Encoding]::new($false))

Write-Host "Z 0 compatibility gate passed."
Write-Host "  contract=$($matrix.contractVersion), domains=$($expectedDomains.Count), tests=$($verifiedTests.Count)"
Write-Host "  report=$reportPath"
