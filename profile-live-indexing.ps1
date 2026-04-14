param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoBuild,

    [string]$OutputPath,

    [ValidateSet("dotnet-sampled-thread-time", "dotnet-common")]
    [string]$Profile = "dotnet-sampled-thread-time",

    [int]$AttachTimeoutSeconds = 60,

    [int]$TopCount = 40
)

$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$runScriptPath = Join-Path $repoRoot "run.ps1"
$tmpRoot = Join-Path $repoRoot "tmp"

if (-not (Test-Path $runScriptPath)) {
    throw "run.ps1 not found: $runScriptPath"
}

if (-not (Test-Path $tmpRoot)) {
    New-Item -ItemType Directory -Path $tmpRoot | Out-Null
}

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $OutputPath = Join-Path $tmpRoot "live_indexing_$timestamp.nettrace"
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory) -and -not (Test-Path $outputDirectory)) {
    New-Item -ItemType Directory -Path $outputDirectory | Out-Null
}

$existingAppIds = @(
    Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -eq "Sims4ResourceExplorer.App" } |
    ForEach-Object { $_.Id }
)

$runArguments = @(
    "-NoProfile",
    "-ExecutionPolicy", "Bypass",
    "-File", $runScriptPath,
    "-Configuration", $Configuration
)

if ($NoBuild) {
    $runArguments += "-NoBuild"
}

Write-Host "Launching app through run.ps1 ($Configuration)..." -ForegroundColor Cyan
$runHost = Start-Process -FilePath "powershell.exe" -ArgumentList $runArguments -PassThru

Write-Host "Waiting for Sims4ResourceExplorer.App process..." -ForegroundColor Cyan
$deadline = (Get-Date).AddSeconds($AttachTimeoutSeconds)
$appProcess = $null

while ((Get-Date) -lt $deadline) {
    $candidates = @(
        Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessName -eq "Sims4ResourceExplorer.App" -and
            $existingAppIds -notcontains $_.Id
        }
    )

    if ($candidates.Count -gt 0) {
        $appProcess = $candidates | Sort-Object StartTime | Select-Object -Last 1
        break
    }

    Start-Sleep -Milliseconds 250
}

if ($null -eq $appProcess) {
    try {
        if (-not $runHost.HasExited) {
            Stop-Process -Id $runHost.Id -Force -ErrorAction SilentlyContinue
        }
    }
    catch {
    }

    throw "Timed out waiting for Sims4ResourceExplorer.App to start."
}

Write-Host "Attaching dotnet-trace to PID $($appProcess.Id)..." -ForegroundColor Cyan
Write-Host "Trace file: $OutputPath" -ForegroundColor DarkGray
Write-Host "Start indexing in the UI now. Close the app when finished to stop collection." -ForegroundColor Yellow

$traceArguments = @(
    "collect",
    "--process-id", $appProcess.Id,
    "--profile", $Profile,
    "--output", $OutputPath
)

& dotnet-trace @traceArguments
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$topReportPath = "$OutputPath.top$TopCount.txt"
Write-Host "Generating topN report..." -ForegroundColor Cyan
$report = & dotnet-trace report $OutputPath topN --count $TopCount
$report | Set-Content -Path $topReportPath

Write-Host "Trace collection finished." -ForegroundColor Green
Write-Host "Trace: $OutputPath" -ForegroundColor DarkGray
Write-Host "TopN:  $topReportPath" -ForegroundColor DarkGray
