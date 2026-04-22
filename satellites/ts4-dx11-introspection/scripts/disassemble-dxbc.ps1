param(
    [Parameter(Mandatory = $true)]
    [string]$InputDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir,

    [string]$FxcPath,

    [switch]$InstructionNumbers,

    [switch]$InstructionOffsets,

    [switch]$HexLiterals
)

$ErrorActionPreference = 'Stop'

function Resolve-FxcPath {
    param(
        [string]$ExplicitPath
    )

    if ($ExplicitPath) {
        return (Resolve-Path -LiteralPath $ExplicitPath).Path
    }

    $candidates = @(
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64\fxc.exe',
        'C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\fxc.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate) {
            return $candidate
        }
    }

    $command = Get-Command fxc.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $command) {
        return $command.Source
    }

    throw 'Unable to locate fxc.exe. Install the Windows SDK or pass -FxcPath explicitly.'
}

$InputDir = (Resolve-Path -LiteralPath $InputDir).Path
$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$FxcPath = Resolve-FxcPath -ExplicitPath $FxcPath

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$files = Get-ChildItem -LiteralPath $InputDir -Filter *.dxbc -File | Sort-Object Name
if ($files.Count -eq 0) {
    throw "No .dxbc files found under $InputDir"
}

$sharedArgs = @('/dumpbin')
if ($InstructionNumbers) {
    $sharedArgs += '/Ni'
}
if ($InstructionOffsets) {
    $sharedArgs += '/No'
}
if ($HexLiterals) {
    $sharedArgs += '/Lx'
}

Write-Host "Input shaders: $($files.Count)"
Write-Host "Output dir:"
Write-Host "  $OutputDir"
Write-Host "FXC:"
Write-Host "  $FxcPath"

foreach ($file in $files) {
    $outputPath = Join-Path $OutputDir ($file.BaseName + '.asm')
    $args = @($sharedArgs + @('/Fc', $outputPath, $file.FullName))

    $null = & $FxcPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "fxc.exe failed for $($file.Name) with exit code $LASTEXITCODE"
    }

    Write-Host ("Disassembled {0}" -f $file.Name)
}

Write-Host ''
Write-Host 'Done.'
