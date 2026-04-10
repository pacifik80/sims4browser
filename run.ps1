param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "src\Sims4ResourceExplorer.App\Sims4ResourceExplorer.App.csproj"
$outputRoot = Join-Path $PSScriptRoot "src\Sims4ResourceExplorer.App\bin\x64\$Configuration\net8.0-windows10.0.19041.0\win-x64"
$exePath = Join-Path $outputRoot "Sims4ResourceExplorer.App.exe"
$resolvedExePath = $null

if (Test-Path $exePath) {
    $resolvedExePath = (Resolve-Path $exePath).Path
}

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

function Get-LockingAppProcesses {
    param(
        [string]$TargetPath
    )

    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        return @()
    }

    return @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProcessName -eq "Sims4ResourceExplorer.App" -and
            $_.Path -and
            ([string]::Equals($_.Path, $TargetPath, [System.StringComparison]::OrdinalIgnoreCase))
        })
}

$lockingProcesses = Get-LockingAppProcesses -TargetPath $resolvedExePath

if (-not $NoBuild -and $lockingProcesses.Count -gt 0) {
    Write-Host "Stopping running x64 app instance(s) so the latest build can be produced..." -ForegroundColor Yellow
    foreach ($process in $lockingProcesses) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            Write-Host "Stopped process $($process.Id)" -ForegroundColor DarkYellow
        }
        catch {
            throw "Failed to stop running app process $($process.Id): $($_.Exception.Message)"
        }
    }

    Start-Sleep -Milliseconds 500
}

if (-not $NoBuild) {
    $buildArguments = @(
        "build",
        $projectPath,
        "-c", $Configuration,
        "-p:Platform=x64"
    )

    Write-Host "Building Sims4 Resource Explorer ($Configuration, x64)..." -ForegroundColor Cyan
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resolvedExePath = (Resolve-Path $exePath).Path
}
elseif (-not (Test-Path $exePath)) {
    throw "Built executable not found: $exePath`nRun without -NoBuild first."
}

Write-Host "Starting Sims4 Resource Explorer ($Configuration, x64)..." -ForegroundColor Cyan
Write-Host "Executable: $resolvedExePath" -ForegroundColor DarkGray
& $resolvedExePath
exit $LASTEXITCODE
