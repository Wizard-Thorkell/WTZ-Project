[CmdletBinding()]
param(
    [switch] $SkipBuild,
    [ValidateRange(30, 600)]
    [int] $TimeoutSeconds = 180,
    [ValidateRange(0, 65535)]
    [int] $Port = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$clientDll = Join-Path $repoRoot 'bin\Content.Client\Content.Client.dll'
$serverDll = Join-Path $repoRoot 'bin\Content.Server\Content.Server.dll'
$captureDir = Join-Path $repoRoot 'bin\Content.Client\user_data\ZLevelVisualCapture'
$runDir = Join-Path $repoRoot 'artifacts\zlevel-visual-capture-run'
$serverStdout = Join-Path $runDir 'server.stdout.log'
$serverStderr = Join-Path $runDir 'server.stderr.log'
$clientStdout = Join-Path $runDir 'client.stdout.log'
$clientStderr = Join-Path $runDir 'client.stderr.log'
$serverProcess = $null
$clientProcess = $null

function Assert-RepoPath([string] $Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $repoPrefix = $repoRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (!$fullPath.StartsWith($repoPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the repository: $fullPath"
    }

    return $fullPath
}

function Invoke-DotNetBuild([string] $Project) {
    & dotnet build $Project --no-restore --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed for $Project with exit code $LASTEXITCODE."
    }
}

function Wait-TcpPort([int] $TargetPort, [System.Diagnostics.Process] $Process) {
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($Process.HasExited) {
            throw "The test server exited before opening port $TargetPort."
        }

        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $connect = $client.ConnectAsync('127.0.0.1', $TargetPort)
            if ($connect.Wait(250) -and $client.Connected) {
                return
            }
        }
        catch {
            # Startup polling deliberately ignores refused connections.
        }
        finally {
            $client.Dispose()
        }

        Start-Sleep -Milliseconds 250
    }

    throw "The test server did not open port $TargetPort within 45 seconds."
}

function Show-LogTail([string] $Path) {
    if (Test-Path -LiteralPath $Path) {
        Write-Host "--- $Path ---"
        Get-Content -LiteralPath $Path -Tail 80
    }
}

try {
    $captureDir = Assert-RepoPath $captureDir
    $runDir = Assert-RepoPath $runDir

    if (!$SkipBuild) {
        Invoke-DotNetBuild (Join-Path $repoRoot 'Content.Client\Content.Client.csproj')
        Invoke-DotNetBuild (Join-Path $repoRoot 'Content.Server\Content.Server.csproj')
    }

    if (!(Test-Path -LiteralPath $clientDll) -or !(Test-Path -LiteralPath $serverDll)) {
        throw 'Client or server output is missing. Run without -SkipBuild first.'
    }

    if (Test-Path -LiteralPath $captureDir) {
        Remove-Item -LiteralPath $captureDir -Recurse -Force
    }

    if (Test-Path -LiteralPath $runDir) {
        Remove-Item -LiteralPath $runDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $runDir | Out-Null

    if ($Port -eq 0) {
        $listener = [System.Net.Sockets.TcpListener]::new(
            [System.Net.IPAddress]::Loopback,
            0)
        $listener.Start()
        $Port = ([System.Net.IPEndPoint] $listener.LocalEndpoint).Port
        $listener.Stop()
    }

    $dotnet = (Get-Command dotnet).Source
    $serverArguments = @(
        $serverDll,
        '--cvar', "net.port=$Port",
        '--cvar', 'game.map=ZLevelMappingStation',
        '+startround'
    )
    $serverStart = @{
        FilePath = $dotnet
        ArgumentList = $serverArguments
        WorkingDirectory = $repoRoot
        RedirectStandardOutput = $serverStdout
        RedirectStandardError = $serverStderr
        WindowStyle = 'Hidden'
        PassThru = $true
    }
    $serverProcess = Start-Process @serverStart

    Wait-TcpPort $Port $serverProcess

    $clientArguments = @(
        $clientDll,
        '--self-contained',
        '--connect',
        '--connect-address', "127.0.0.1:$Port",
        '--username', 'WTZCapture',
        '+zlevellightingcapture'
    )
    $clientStart = @{
        FilePath = $dotnet
        ArgumentList = $clientArguments
        WorkingDirectory = $repoRoot
        RedirectStandardOutput = $clientStdout
        RedirectStandardError = $clientStderr
        WindowStyle = 'Hidden'
        PassThru = $true
    }
    $clientProcess = Start-Process @clientStart

    if (!$clientProcess.WaitForExit($TimeoutSeconds * 1000)) {
        throw "The capture client exceeded the $TimeoutSeconds second process timeout."
    }

    $reportPath = Join-Path $captureDir 'report.json'
    if (!(Test-Path -LiteralPath $reportPath)) {
        throw "The capture client exited without writing $reportPath."
    }

    $report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
    $passed = @($report.checks | Where-Object passed).Count
    $total = @($report.checks).Count
    Write-Host "Z-level visual capture: $passed/$total checks passed in $($report.durationSeconds) seconds."
    Write-Host "Artifacts: $captureDir"

    if (!$report.success) {
        $report.checks |
            Where-Object { !$_.passed } |
            Format-Table name, details -AutoSize
        throw 'The real-client Z-level visual capture failed one or more pixel checks.'
    }
}
catch {
    Show-LogTail $clientStdout
    Show-LogTail $clientStderr
    Show-LogTail $serverStdout
    Show-LogTail $serverStderr
    throw
}
finally {
    if ($null -ne $clientProcess -and !$clientProcess.HasExited) {
        Stop-Process -Id $clientProcess.Id -Force
        $clientProcess.WaitForExit()
    }

    if ($null -ne $serverProcess -and !$serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
        $serverProcess.WaitForExit()
    }
}
