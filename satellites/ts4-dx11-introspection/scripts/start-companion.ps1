param(
    [string]$Output = "$PSScriptRoot\..\captures",
    [string]$PipeName = "ts4-dx11-introspection-v1"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\companion\Ts4Dx11Companion.csproj"
dotnet run --project $project -- listen --output $Output --pipe-name $PipeName
