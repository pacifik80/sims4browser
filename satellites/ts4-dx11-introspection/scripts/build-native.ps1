param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$buildDir = Join-Path $root "native\out\$Configuration"

cmake -S (Join-Path $root "native") -B $buildDir -G "Visual Studio 16 2019" -A x64
if ($LASTEXITCODE -ne 0) {
    throw "CMake configure failed."
}

cmake --build $buildDir --config $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed."
}
