param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "src\Sims4ResourceExplorer.App\Sims4ResourceExplorer.App.csproj"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

$arguments = @(
    "run",
    "--project", $projectPath,
    "--configuration", $Configuration
)

if ($NoBuild) {
    $arguments += "--no-build"
}

Write-Host "Starting Sims4 Resource Explorer ($Configuration)..." -ForegroundColor Cyan
& dotnet @arguments
exit $LASTEXITCODE
