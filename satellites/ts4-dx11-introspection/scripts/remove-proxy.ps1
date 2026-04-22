param(
    [string]$GameBin = "C:\Games\The Sims 4\Game\Bin",
    [int]$Attempts = 20,
    [int]$DelayMilliseconds = 500
)

$ErrorActionPreference = "Stop"

$proxyPath = Join-Path $GameBin "d3d11.dll"
$sessionConfigPath = Join-Path $GameBin "ts4-dx11-introspection-session-dir.txt"

function Remove-WithRetry {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [switch]$Required
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        if ($Required) {
            Write-Host "No local file found at $Path"
        }
        return
    }

    $lastError = $null
    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
            Write-Host "Removed $Path"
            return
        }
        catch {
            $lastError = $_
            Start-Sleep -Milliseconds $DelayMilliseconds
        }
    }

    throw "Failed to remove $Path after $Attempts attempt(s). Last error: $($lastError.Exception.Message)"
}

Remove-WithRetry -Path $proxyPath -Required
Remove-WithRetry -Path $sessionConfigPath
