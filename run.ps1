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
$projectXml = [xml](Get-Content $projectPath)
$buildNumber = $projectXml.Project.PropertyGroup.BuildNumber | Select-Object -First 1

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

    $appProcesses = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -eq "Sims4ResourceExplorer.App" })

    if ([string]::IsNullOrWhiteSpace($TargetPath)) {
        return $appProcesses
    }

    $matchingProcesses = @($appProcesses |
        Where-Object {
            $_.Path -and
            ([string]::Equals($_.Path, $TargetPath, [System.StringComparison]::OrdinalIgnoreCase))
        })

    if ($matchingProcesses.Count -gt 0) {
        return $matchingProcesses
    }

    # If the expected path is unavailable or the running process reports a slightly
    # different path, prefer stopping all app instances so the build can refresh DLLs.
    return $appProcesses
}

function Wait-ForProcessExit {
    param(
        [int[]]$ProcessIds,
        [int]$TimeoutMilliseconds = 5000
    )

    if (-not $ProcessIds -or $ProcessIds.Count -eq 0) {
        return
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        $stillRunning = @($ProcessIds | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
        if ($stillRunning.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 200
    }
}

function Remove-PathWithRetries {
    param(
        [string]$TargetPath,
        [int]$Attempts = 10,
        [int]$DelayMilliseconds = 400
    )

    if (-not (Test-Path $TargetPath)) {
        return
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $TargetPath -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }

    $lockingNow = Get-LockingAppProcesses -TargetPath $resolvedExePath
    if ($lockingNow.Count -gt 0) {
        $processSummary = ($lockingNow | ForEach-Object { "$($_.ProcessName)[$($_.Id)]" }) -join ", "
        throw "Failed to remove '$TargetPath' after $Attempts attempt(s). Locking app processes: $processSummary. Last error: $($lastError.Exception.Message)"
    }

    throw "Failed to remove '$TargetPath' after $Attempts attempt(s). Last error: $($lastError.Exception.Message)"
}

$lockingProcesses = Get-LockingAppProcesses -TargetPath $resolvedExePath

if (-not $NoBuild -and $lockingProcesses.Count -gt 0) {
    Write-Host "Stopping running app instance(s) so the latest build can be produced..." -ForegroundColor Yellow
    $stoppedProcessIds = @()
    foreach ($process in $lockingProcesses) {
        try {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            $stoppedProcessIds += $process.Id
            Write-Host "Stopped process $($process.Id)" -ForegroundColor DarkYellow
        }
        catch {
            $message = $_.Exception.Message
            if ($message -like "*Cannot find a process with the process identifier*") {
                continue
            }

            throw "Failed to stop running app process $($process.Id): $message"
        }
    }

    Wait-ForProcessExit -ProcessIds $stoppedProcessIds -TimeoutMilliseconds 7000
    Start-Sleep -Milliseconds 800
}

if (-not $NoBuild) {
    if (Test-Path $outputRoot) {
        Write-Host "Removing previous app output so the next launch is guaranteed to use fresh binaries..." -ForegroundColor Yellow
        Remove-PathWithRetries -TargetPath $outputRoot
    }

    $buildArguments = @(
        "build",
        $projectPath,
        "-c", $Configuration,
        "-p:Platform=x64"
    )

    Write-Host "Building Sims4 Resource Explorer build $buildNumber ($Configuration, x64)..." -ForegroundColor Cyan
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    $resolvedExePath = (Resolve-Path $exePath).Path

    $builtFiles = @(
        (Join-Path $outputRoot "Sims4ResourceExplorer.App.dll"),
        (Join-Path $outputRoot "Sims4ResourceExplorer.Preview.dll")
    )

    foreach ($builtFile in $builtFiles) {
        if (-not (Test-Path $builtFile)) {
            throw "Expected build artifact not found: $builtFile"
        }
    }
}
elseif (-not (Test-Path $exePath)) {
    throw "Built executable not found: $exePath`nRun without -NoBuild first."
}

Write-Host "Starting Sims4 Resource Explorer build $buildNumber ($Configuration, x64)..." -ForegroundColor Cyan
Write-Host "Executable: $resolvedExePath" -ForegroundColor DarkGray
try {
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($resolvedExePath)
    $informationalVersion = $versionInfo.ProductVersion
    if ($informationalVersion) {
        Write-Host "Build version: $informationalVersion" -ForegroundColor DarkGray
    }
}
catch {
}
$startedProcess = Start-Process -FilePath $resolvedExePath -PassThru
$startedProcess.WaitForExit()
exit $startedProcess.ExitCode
