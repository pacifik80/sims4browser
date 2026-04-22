param(
    [string]$GameBin = "C:\Games\The Sims 4\Game\Bin",
    [string]$Output = "$PSScriptRoot\..\docs\reports"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\recon\Ts4Dx11Recon.csproj"
dotnet run --project $project -- --target $GameBin --output $Output
