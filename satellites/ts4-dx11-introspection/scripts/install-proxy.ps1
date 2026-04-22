param(
    [string]$GameBin = "C:\Games\The Sims 4\Game\Bin",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$proxyPath = Get-ChildItem (Join-Path $root "native\out\$Configuration") -Recurse -Filter d3d11.dll |
    Select-Object -First 1 -ExpandProperty FullName

if (-not (Test-Path $proxyPath)) {
    throw "Proxy build not found under native\\out\\$Configuration.`nRun build-native.ps1 first."
}

$destination = Join-Path $GameBin "d3d11.dll"
if (Test-Path $destination) {
    throw "Refusing to overwrite existing local d3d11.dll at $destination"
}

Copy-Item -LiteralPath $proxyPath -Destination $destination -Force
Write-Host "Installed proxy to $destination"
