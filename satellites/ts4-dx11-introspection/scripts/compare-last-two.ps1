$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$captureRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\captures\live'
$reportRoot = Join-Path $captureRoot 'reports'
$companionProject = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj'

$sessions = Get-ChildItem -Path $captureRoot -Directory |
    Where-Object { $_.Name -match '^\d{8}-\d{6}$' } |
    Sort-Object LastWriteTime -Descending |
    Where-Object {
        $tracePath = Join-Path $_.FullName 'session-trace.jsonl'
        if (-not (Test-Path $tracePath))
        {
            return $false
        }

        $exitLine = Get-Content -Path $tracePath | Select-String '"action":"game_exit_observed"' | Select-Object -Last 1
        return $null -ne $exitLine -and $exitLine.Line -match '"launcher_exit_code":0'
    }

if ($sessions.Count -lt 2)
{
    throw "Need at least two successful capture sessions under $captureRoot"
}

$right = $sessions[0]
$left = $sessions[1]

New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$reportPath = Join-Path $reportRoot ("compare-{0}-vs-{1}.md" -f $left.Name, $right.Name)

Write-Host "Comparing:"
Write-Host "  Left : $($left.FullName)"
Write-Host "  Right: $($right.FullName)"
Write-Host "Writing report:"
Write-Host "  $reportPath"

dotnet run --project $companionProject -- compare --left $left.FullName --right $right.FullName --output $reportPath

Write-Host ""
Write-Host "Done."
