param(
    [string[]]$Inputs = @(
        '20260421-212139',
        '20260421-212533',
        '20260421-220041'
    ),

    [string]$OutputMarkdown,

    [string]$OutputJson
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$captureRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\captures\live'
$docsRoot = Join-Path $repoRoot 'satellites\ts4-dx11-introspection\docs'
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

$resolvedInputs = Expand-SessionArgs $Inputs | ForEach-Object { Resolve-CapturePath $_ }

if ([string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = Join-Path $docsRoot 'raw\shader-catalog.md'
}
else {
    $OutputMarkdown = [System.IO.Path]::GetFullPath($OutputMarkdown)
}

if ([string]::IsNullOrWhiteSpace($OutputJson)) {
    $OutputJson = Join-Path $docsRoot 'raw\shader-catalog.json'
}
else {
    $OutputJson = [System.IO.Path]::GetFullPath($OutputJson)
}

New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($OutputMarkdown)) -Force | Out-Null
New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($OutputJson)) -Force | Out-Null

$arguments = @('run', '--project', $companionProject, '--', 'catalog')
foreach ($path in $resolvedInputs) {
    $arguments += @('--input', $path)
}
$arguments += @('--output-md', $OutputMarkdown, '--output-json', $OutputJson)

Write-Host "Building shader catalog from:"
$resolvedInputs | ForEach-Object { Write-Host "  $_" }
Write-Host "Markdown:"
Write-Host "  $OutputMarkdown"
Write-Host "JSON:"
Write-Host "  $OutputJson"

dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Catalog generation failed with exit code $LASTEXITCODE"
}

Write-Host ""
Write-Host "Done."
