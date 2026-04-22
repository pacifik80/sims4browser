param(
    [Parameter(Mandatory = $true)]
    [string[]]$Left,

    [Parameter(Mandatory = $true)]
    [string[]]$Right,

    [string]$Output
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$captureRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\captures\live'
$reportRoot = Join-Path $captureRoot 'reports'
$companionProject = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\companion\Ts4Dx11Companion.csproj'

function Resolve-CapturePath {
    param([string]$Value)

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return (Resolve-Path $Value).Path
    }

    return (Resolve-Path (Join-Path $captureRoot $Value)).Path
}

function Expand-SessionArgs {
    param([string[]]$Values)

    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $trimmed
            }
        }
    }
}

$leftResolved = Expand-SessionArgs $Left | ForEach-Object { Resolve-CapturePath $_ }
$rightResolved = Expand-SessionArgs $Right | ForEach-Object { Resolve-CapturePath $_ }

if ([string]::IsNullOrWhiteSpace($Output)) {
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
    $leftName = [System.IO.Path]::GetFileName($leftResolved[0])
    $rightName = [System.IO.Path]::GetFileName($rightResolved[0])
    $Output = Join-Path $reportRoot ("group-compare-{0}-vs-{1}.md" -f $leftName, $rightName)
}
else {
    $Output = [System.IO.Path]::GetFullPath($Output)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($Output)) -Force | Out-Null
}

$arguments = @('run', '--project', $companionProject, '--', 'compare-groups')
foreach ($path in $leftResolved) {
    $arguments += @('--left', $path)
}
foreach ($path in $rightResolved) {
    $arguments += @('--right', $path)
}
$arguments += @('--output', $Output)

Write-Host "Left group:"
$leftResolved | ForEach-Object { Write-Host "  $_" }
Write-Host "Right group:"
$rightResolved | ForEach-Object { Write-Host "  $_" }
Write-Host "Writing report:"
Write-Host "  $Output"

dotnet @arguments

Write-Host ""
Write-Host "Done."
