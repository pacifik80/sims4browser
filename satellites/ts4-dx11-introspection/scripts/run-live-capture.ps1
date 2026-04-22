param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [string]$GameBin = "C:\Games\The Sims 4\Game\Bin",

    [ValidateSet("TS4_x64.exe", "TS4_x64_fpb.exe")]
    [string]$Executable = "TS4_x64.exe",

    [int]$CaptureFrames = 120,

    [ValidateSet("ShaderDayNight", "GeneratedLight", "ProjectionReveal")]
    [string]$HelperPreset,

    [ValidateSet("CAS", "BuildBuy", "LiveMode")]
    [string]$WorldMode,

    [string]$SceneLabel,

    [string[]]$FamilyFocus,

    [string[]]$SceneClass,

    [string[]]$ExpectedCandidateClusters,

    [string[]]$TargetAssetsOrEffects,

    [string]$Notes,

    [switch]$SkipManagedBuild,

    [switch]$SkipNativeBuild,

    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$satelliteRoot = Resolve-Path (Join-Path $scriptRoot "..")
$repoRoot = Resolve-Path (Join-Path $satelliteRoot "..\..")
$captureRoot = Join-Path $satelliteRoot "captures\live"
$sessionId = Get-Date -Format "yyyyMMdd-HHmmss"
$sessionDir = Join-Path $captureRoot $sessionId
$tracePath = Join-Path $sessionDir "session-trace.jsonl"
$manifestPath = Join-Path $sessionDir "session-manifest.json"
$contextTagsPath = Join-Path $sessionDir "context-tags.json"
$proxyMarkerPath = Join-Path $sessionDir "proxy-loaded.marker"
$gameExePath = Join-Path $GameBin $Executable
$proxyPath = Join-Path $GameBin "d3d11.dll"
$proxySessionConfigPath = Join-Path $GameBin "ts4-dx11-introspection-session-dir.txt"
$summaryPath = Join-Path $sessionDir "summary.txt"
$managedSolution = Join-Path $satelliteRoot "TS4.DX11.Introspection.sln"
$companionProject = Join-Path $satelliteRoot "companion\Ts4Dx11Companion.csproj"
$buildNativeScript = Join-Path $scriptRoot "build-native.ps1"
$removeProxyScript = Join-Path $scriptRoot "remove-proxy.ps1"
$installProxyScript = Join-Path $scriptRoot "install-proxy.ps1"
$script:ObservedD3D11States = @{}

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-UtcTimestamp {
    [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
}

function ConvertTo-JsonLiteral {
    param([object]$Value)
    return ($Value | ConvertTo-Json -Compress -Depth 8)
}

function Write-SessionTrace {
    param(
        [string]$Component,
        [string]$Action,
        [hashtable]$Fields = @{}
    )

    $payload = [ordered]@{
        schema_version = 1
        event_type = "session_trace"
        timestamp_utc = Get-UtcTimestamp
        pid = $PID
        component = $Component
        action = $Action
        session_id = $sessionId
    }

    foreach ($key in $Fields.Keys) {
        $payload[$key] = $Fields[$key]
    }

    Add-Content -LiteralPath $tracePath -Value (ConvertTo-JsonLiteral $payload) -Encoding utf8
}

function Write-SessionManifest {
    $manifest = [ordered]@{
        schema_version = 1
        session_id = $sessionId
        created_utc = Get-UtcTimestamp
        repo_root = [string]$repoRoot
        satellite_root = [string]$satelliteRoot
        capture_root = $captureRoot
        session_dir = $sessionDir
        trace_path = $tracePath
        context_tags_path = $contextTagsPath
        proxy_marker_path = $proxyMarkerPath
        proxy_session_config_path = $proxySessionConfigPath
        summary_path = $summaryPath
        game_bin = $GameBin
        game_executable = $gameExePath
        proxy_path = $proxyPath
        managed_solution = $managedSolution
        companion_project = $companionProject
        build_native_script = $buildNativeScript
        install_proxy_script = $installProxyScript
        remove_proxy_script = $removeProxyScript
        configuration = $Configuration
        capture_frames = $CaptureFrames
        helper_preset = $HelperPreset
    }

    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $manifestPath -Encoding utf8
}

function Apply-HelperPresetDefaults {
    if ([string]::IsNullOrWhiteSpace($HelperPreset)) {
        return
    }

    $presetDefaults = switch ($HelperPreset) {
        "ShaderDayNight" {
            [ordered]@{
                world_mode = "LiveMode"
                family_focus = @("shader-daynight")
                scene_class = @("lighting-heavy", "reveal-aware")
                expected_candidate_clusters = @("F04-parameter-heavy", "F05-color-aware")
            }
        }
        "GeneratedLight" {
            [ordered]@{
                world_mode = "LiveMode"
                family_focus = @("generated-light")
                scene_class = @("lighting-heavy", "indoor-lit")
                expected_candidate_clusters = @("F03-maptex")
            }
        }
        "ProjectionReveal" {
            [ordered]@{
                world_mode = "LiveMode"
                family_focus = @("projection-reveal")
                scene_class = @("projection-heavy", "reveal-aware")
                expected_candidate_clusters = @("F04-srctex")
            }
        }
    }

    if ([string]::IsNullOrWhiteSpace($WorldMode)) {
        $script:WorldMode = $presetDefaults.world_mode
    }

    if ($FamilyFocus.Count -eq 0) {
        $script:FamilyFocus = @($presetDefaults.family_focus)
    }

    if ($SceneClass.Count -eq 0) {
        $script:SceneClass = @($presetDefaults.scene_class)
    }

    if ($ExpectedCandidateClusters.Count -eq 0) {
        $script:ExpectedCandidateClusters = @($presetDefaults.expected_candidate_clusters)
    }

    Write-SessionTrace -Component "runner" -Action "helper_preset_applied" -Fields @{
        helper_preset = $HelperPreset
        world_mode = $WorldMode
        family_focus = @($FamilyFocus)
        scene_class = @($SceneClass)
        expected_candidate_clusters = @($ExpectedCandidateClusters)
    }
}

function Write-ContextTagsIfRequested {
    $hasAnyContext =
        -not [string]::IsNullOrWhiteSpace($WorldMode) -or
        -not [string]::IsNullOrWhiteSpace($SceneLabel) -or
        $FamilyFocus.Count -gt 0 -or
        $SceneClass.Count -gt 0 -or
        $ExpectedCandidateClusters.Count -gt 0 -or
        $TargetAssetsOrEffects.Count -gt 0 -or
        -not [string]::IsNullOrWhiteSpace($Notes)

    if (-not $hasAnyContext) {
        return
    }

    if ([string]::IsNullOrWhiteSpace($WorldMode)) {
        throw "WorldMode is required when writing context tags."
    }

    if ([string]::IsNullOrWhiteSpace($SceneLabel)) {
        throw "SceneLabel is required when writing context tags."
    }

    if ($FamilyFocus.Count -eq 0) {
        throw "FamilyFocus is required when writing context tags."
    }

    if ($SceneClass.Count -eq 0) {
        throw "SceneClass is required when writing context tags."
    }

    if ($ExpectedCandidateClusters.Count -eq 0) {
        throw "ExpectedCandidateClusters is required when writing context tags."
    }

    if ($TargetAssetsOrEffects.Count -eq 0) {
        throw "TargetAssetsOrEffects is required when writing context tags."
    }

    if ([string]::IsNullOrWhiteSpace($Notes)) {
        throw "Notes is required when writing context tags."
    }

    $payload = [ordered]@{
        schema_version = 1
        session_id = $sessionId
        world_mode = $WorldMode
        scene_label = $SceneLabel
        family_focus = @($FamilyFocus)
        scene_class = @($SceneClass)
        expected_candidate_clusters = @($ExpectedCandidateClusters)
        target_assets_or_effects = @($TargetAssetsOrEffects)
        notes = $Notes
    }

    $payload | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $contextTagsPath -Encoding utf8

    Write-SessionTrace -Component "runner" -Action "context_tags_written" -Fields @{
        context_tags_path = $contextTagsPath
        world_mode = $WorldMode
        scene_label = $SceneLabel
        family_focus = @($FamilyFocus)
        scene_class = @($SceneClass)
        expected_candidate_clusters = @($ExpectedCandidateClusters)
        target_assets_or_effects = @($TargetAssetsOrEffects)
    }
}

function Get-RunningGameProcesses {
    $expectedNames = @("TS4_x64", "TS4_x64_fpb", "TS4_Launcher_x64")
    return @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
        Where-Object {
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
            $expectedNames -contains $baseName -and
            (
                ([string]::IsNullOrWhiteSpace($_.ExecutablePath)) -or
                $_.ExecutablePath.StartsWith($GameBin, [System.StringComparison]::OrdinalIgnoreCase)
            )
        } |
        Select-Object @{ Name = "ProcessName"; Expression = { [System.IO.Path]::GetFileNameWithoutExtension($_.Name) } }, ProcessId, ParentProcessId, ExecutablePath)
}

function Observe-GameProcessModules {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Processes
    )

    foreach ($processInfo in $Processes) {
        $moduleState = "not_loaded"
        $modulePath = $null
        $errorMessage = $null

        try {
            $module = Get-Process -Id $processInfo.ProcessId -Module -ErrorAction Stop |
                Where-Object { $_.ModuleName -ieq "d3d11.dll" } |
                Select-Object -First 1

            if ($null -ne $module) {
                $modulePath = $module.FileName
                if ($modulePath -and [string]::Equals($modulePath, $proxyPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                    $moduleState = "local_proxy"
                }
                elseif ($modulePath -and $modulePath.StartsWith((Join-Path $env:WINDIR "System32"), [System.StringComparison]::OrdinalIgnoreCase)) {
                    $moduleState = "system_d3d11"
                }
                else {
                    $moduleState = "other_path"
                }
            }
        }
        catch {
            $moduleState = "enumeration_failed"
            $errorMessage = $_.Exception.Message
        }

        $signature = "{0}|{1}|{2}" -f $moduleState, $modulePath, $errorMessage
        $stateKey = [string]$processInfo.ProcessId
        if ($script:ObservedD3D11States[$stateKey] -ne $signature) {
            Write-SessionTrace -Component "module_audit" -Action "d3d11_observed" -Fields @{
                process_name = $processInfo.ProcessName
                process_id = $processInfo.ProcessId
                executable_path = $processInfo.ExecutablePath
                state = $moduleState
                module_path = $modulePath
                error = $errorMessage
            }
            $script:ObservedD3D11States[$stateKey] = $signature
        }
    }
}

function Ensure-CleanProxyState {
    if (-not (Test-Path $proxyPath)) {
        Write-SessionTrace -Component "runner" -Action "proxy_already_clean"
        return
    }

    $runningGames = @(Get-RunningGameProcesses)
    if ($runningGames.Count -gt 0) {
        $summary = ($runningGames | ForEach-Object { "$($_.ProcessName)[$($_.ProcessId)]" }) -join ", "
        Write-SessionTrace -Component "runner" -Action "proxy_cleanup_blocked" -Fields @{ running_processes = $summary }
        throw "A local d3d11.dll already exists at $proxyPath and TS4 is still running: $summary. Close the game, then run the same command again."
    }

    Write-Step "Removing stale local proxy from previous run"
    Write-SessionTrace -Component "runner" -Action "proxy_cleanup_begin" -Fields @{ proxy_path = $proxyPath }
    powershell -ExecutionPolicy Bypass -File $removeProxyScript -GameBin $GameBin
    Write-SessionTrace -Component "runner" -Action "proxy_cleanup_complete" -Fields @{ proxy_path = $proxyPath }
}

function Wait-ForGameProcessesToExit {
    param(
        [int]$StartupGraceSeconds = 20,
        [int]$ZeroProcessGraceSeconds = 5,
        [int]$PollMilliseconds = 1000
    )

    $startupDeadline = (Get-Date).AddSeconds($StartupGraceSeconds)
    $sawAtLeastOneProcess = $false
    $zeroSince = $null
    while ($true) {
        $running = @(Get-RunningGameProcesses)
        if ($running.Count -gt 0) {
            $sawAtLeastOneProcess = $true
            $runningNames = ($running | ForEach-Object { "$($_.ProcessName)[$($_.ProcessId)]" }) -join ", "
            Write-Host "Active TS4 processes: $runningNames" -ForegroundColor DarkGray
            Observe-GameProcessModules -Processes $running
        }

        if ($running.Count -eq 0) {
            if (-not $sawAtLeastOneProcess -and (Get-Date) -lt $startupDeadline) {
                Start-Sleep -Milliseconds $PollMilliseconds
                continue
            }

            if ($null -eq $zeroSince) {
                $zeroSince = Get-Date
            }
            elseif (((Get-Date) - $zeroSince).TotalSeconds -ge $ZeroProcessGraceSeconds) {
                Write-SessionTrace -Component "runner" -Action "game_processes_exited"
                return
            }
        }
        else {
            $zeroSince = $null
        }

        Start-Sleep -Milliseconds $PollMilliseconds
    }
}

function Remove-ProxyIfPresent {
    if (-not (Test-Path $proxyPath)) {
        Write-SessionTrace -Component "runner" -Action "proxy_remove_skip_missing"
        return
    }

    $runningGames = @(Get-RunningGameProcesses)
    if ($runningGames.Count -gt 0) {
        $summary = ($runningGames | ForEach-Object { "$($_.ProcessName)[$($_.ProcessId)]" }) -join ", "
        Write-SessionTrace -Component "runner" -Action "proxy_remove_skip_running_game" -Fields @{ running_processes = $summary }
        Write-Warning "Skipping proxy removal because TS4 is still running: $summary"
        return
    }

    Write-Step "Removing local proxy from Game\Bin"
    Write-SessionTrace -Component "runner" -Action "proxy_remove_begin" -Fields @{ proxy_path = $proxyPath }
    try {
        powershell -ExecutionPolicy Bypass -File $removeProxyScript -GameBin $GameBin
        Write-SessionTrace -Component "runner" -Action "proxy_remove_complete" -Fields @{ proxy_path = $proxyPath }
    }
    catch {
        Write-SessionTrace -Component "runner" -Action "proxy_remove_failed" -Fields @{ proxy_path = $proxyPath; error = $_.Exception.Message }
        Write-Warning $_.Exception.Message
    }
}

function Get-SessionCaptureDirectory {
    if (Test-Path (Join-Path $sessionDir "events.jsonl")) {
        return $sessionDir
    }

    return $null
}

if (-not (Test-Path $gameExePath)) {
    throw "Game executable not found: $gameExePath"
}

New-Item -ItemType Directory -Force $sessionDir | Out-Null
Write-SessionManifest
Apply-HelperPresetDefaults
Write-ContextTagsIfRequested
Write-SessionTrace -Component "runner" -Action "session_created" -Fields @{
    session_dir = $sessionDir
    game_executable = $gameExePath
    proxy_path = $proxyPath
    proxy_marker_path = $proxyMarkerPath
    proxy_session_config_path = $proxySessionConfigPath
    configuration = $Configuration
    capture_frames = $CaptureFrames
    helper_preset = $HelperPreset
}

$originalSessionDir = $env:TS4_DX11_INTROSPECTION_SESSION_DIR
$originalFallback = $env:TS4_DX11_INTROSPECTION_FALLBACK_DIR
$originalFrames = $env:TS4_DX11_INTROSPECTION_CAPTURE_FRAMES
$originalProxyMarkerPath = $env:TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH

try {
    Ensure-CleanProxyState

    if (-not $SkipManagedBuild) {
        Write-Step "Building managed solution"
        Write-SessionTrace -Component "runner" -Action "managed_build_begin"
        dotnet build $managedSolution -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            Write-SessionTrace -Component "runner" -Action "managed_build_failed" -Fields @{ exit_code = $LASTEXITCODE }
            throw "Managed build failed."
        }

        Write-SessionTrace -Component "runner" -Action "managed_build_complete"
    }
    else {
        Write-SessionTrace -Component "runner" -Action "managed_build_skipped"
    }

    if (-not $SkipNativeBuild) {
        Write-Step "Building native proxy"
        Write-SessionTrace -Component "runner" -Action "native_build_begin"
        powershell -ExecutionPolicy Bypass -File $buildNativeScript -Configuration $Configuration
        if ($LASTEXITCODE -ne 0) {
            Write-SessionTrace -Component "runner" -Action "native_build_failed" -Fields @{ exit_code = $LASTEXITCODE }
            throw "Native build failed."
        }

        Write-SessionTrace -Component "runner" -Action "native_build_complete"
    }
    else {
        Write-SessionTrace -Component "runner" -Action "native_build_skipped"
    }

    Write-Step "Configuring session environment"
    $env:TS4_DX11_INTROSPECTION_SESSION_DIR = $sessionDir
    $env:TS4_DX11_INTROSPECTION_FALLBACK_DIR = $sessionDir
    $env:TS4_DX11_INTROSPECTION_CAPTURE_FRAMES = $CaptureFrames.ToString()
    $env:TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH = $proxyMarkerPath
    Set-Content -LiteralPath $proxySessionConfigPath -Value $sessionDir -Encoding UTF8
    Write-SessionTrace -Component "runner" -Action "environment_configured" -Fields @{
        TS4_DX11_INTROSPECTION_SESSION_DIR = $env:TS4_DX11_INTROSPECTION_SESSION_DIR
        TS4_DX11_INTROSPECTION_FALLBACK_DIR = $env:TS4_DX11_INTROSPECTION_FALLBACK_DIR
        TS4_DX11_INTROSPECTION_CAPTURE_FRAMES = $env:TS4_DX11_INTROSPECTION_CAPTURE_FRAMES
        TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH = $env:TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH
    }
    Write-SessionTrace -Component "runner" -Action "proxy_session_config_written" -Fields @{
        proxy_session_config_path = $proxySessionConfigPath
        session_dir = $sessionDir
    }

    Write-Step "Installing d3d11 proxy into Game\Bin"
    Write-SessionTrace -Component "runner" -Action "proxy_install_begin" -Fields @{ game_bin = $GameBin }
    powershell -ExecutionPolicy Bypass -File $installProxyScript -GameBin $GameBin -Configuration $Configuration
    Write-SessionTrace -Component "runner" -Action "proxy_install_complete" -Fields @{ proxy_path = $proxyPath }

    if ($NoLaunch) {
        Write-Step "NoLaunch set, stopping after build + proxy install"
        Write-SessionTrace -Component "runner" -Action "no_launch_stop"
        return
    }

    Write-Step "Launching $Executable"
    Write-Host "Continuous logging starts automatically after DX11 runtime hooks go live." -ForegroundColor Yellow
    Write-Host "F10 only adds a bookmark to the log." -ForegroundColor Yellow
    Write-SessionTrace -Component "runner" -Action "game_launch_begin" -Fields @{ executable = $gameExePath }
    $gameProcess = Start-Process -FilePath $gameExePath -WorkingDirectory $GameBin -PassThru
    Write-SessionTrace -Component "runner" -Action "game_launch_complete" -Fields @{ process_id = $gameProcess.Id }
    Start-Sleep -Seconds 2
    Wait-ForGameProcessesToExit

    $exitCode = if ($gameProcess.HasExited) { $gameProcess.ExitCode } else { "<still-running>" }
    Write-Step "Game processes exited (launcher exit code: $exitCode)"
    Write-SessionTrace -Component "runner" -Action "game_exit_observed" -Fields @{ launcher_exit_code = $exitCode }
    Start-Sleep -Seconds 2

    $captureSessionDir = Get-SessionCaptureDirectory
    if ($null -ne $captureSessionDir) {
        Write-Step "Generating summary"
        Write-SessionTrace -Component "runner" -Action "summary_begin" -Fields @{ input_dir = $captureSessionDir }
        $summary = dotnet run --project $companionProject -- summarize --input $captureSessionDir
        $summary | Set-Content -Path $summaryPath -Encoding utf8
        $summary | ForEach-Object { Write-Host $_ }
        Write-SessionTrace -Component "runner" -Action "summary_complete" -Fields @{ summary_path = $summaryPath }
    }
    else {
        Write-SessionTrace -Component "runner" -Action "summary_skipped_no_primary_logs"
        Write-Warning "No primary JSONL logs were produced under $sessionDir"
    }
}
catch {
    Write-SessionTrace -Component "runner" -Action "run_failed" -Fields @{ error = $_.Exception.Message }
    throw
}
finally {
    Remove-ProxyIfPresent

    $env:TS4_DX11_INTROSPECTION_SESSION_DIR = $originalSessionDir
    $env:TS4_DX11_INTROSPECTION_FALLBACK_DIR = $originalFallback
    $env:TS4_DX11_INTROSPECTION_CAPTURE_FRAMES = $originalFrames
    $env:TS4_DX11_INTROSPECTION_PROXY_MARKER_PATH = $originalProxyMarkerPath

    Write-SessionTrace -Component "runner" -Action "environment_restored"

    Write-Host
    Write-Host "Session root: $sessionDir" -ForegroundColor Green
    Write-Host "Session trace: $tracePath" -ForegroundColor Green
}
