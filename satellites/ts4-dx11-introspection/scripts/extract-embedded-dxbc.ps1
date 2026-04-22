param(
    [Parameter(Mandatory = $true)]
    [string]$BinaryPath,

    [string]$CatalogPath,

    [string]$OutputMarkdown,

    [string]$OutputJson,

    [string]$ExportDir,

    [switch]$MatchOnly
)

$ErrorActionPreference = 'Stop'

$BinaryPath = (Resolve-Path $BinaryPath).Path
if (-not [string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = (Resolve-Path $CatalogPath).Path
}

$bytes = [System.IO.File]::ReadAllBytes($BinaryPath)
$magic = [System.Text.Encoding]::ASCII.GetBytes('DXBC')
$offsets = New-Object System.Collections.Generic.List[int]

for ($i = 0; $i -le $bytes.Length - $magic.Length; $i++) {
    if ($bytes[$i] -eq $magic[0] -and
        $bytes[$i + 1] -eq $magic[1] -and
        $bytes[$i + 2] -eq $magic[2] -and
        $bytes[$i + 3] -eq $magic[3]) {
        [void]$offsets.Add($i)
    }
}

$catalogEntries = @{}
if (-not [string]::IsNullOrWhiteSpace($CatalogPath)) {
    $catalog = Get-Content -Raw $CatalogPath | ConvertFrom-Json
    foreach ($entry in $catalog.Entries) {
        $catalogEntries[$entry.Hash] = $entry
    }
}

$sha256 = [System.Security.Cryptography.SHA256]::Create()
$results = @()
foreach ($offset in $offsets) {
    if ($offset + 28 -gt $bytes.Length) {
        continue
    }

    $size = [System.BitConverter]::ToUInt32($bytes, $offset + 24)
    if ($size -le 0 -or ($offset + $size) -gt $bytes.Length) {
        continue
    }

    $blob = New-Object byte[] $size
    [Array]::Copy($bytes, $offset, $blob, 0, $size)
    $hashBytes = $sha256.ComputeHash($blob)
    $hash = -join ($hashBytes | ForEach-Object { $_.ToString('x2') })

    $catalogEntry = $null
    $matchKind = 'none'
    if ($catalogEntries.ContainsKey($hash)) {
        $catalogEntry = $catalogEntries[$hash]
        $matchKind = 'runtime_catalog'
    }

    $results += [pscustomobject]@{
        BinaryPath = $BinaryPath
        Offset = ('0x{0:X}' -f $offset)
        OffsetDecimal = $offset
        DxbcSize = [int]$size
        Sha256 = $hash
        MatchKind = $matchKind
        Blob = $blob
        CatalogStage = if ($catalogEntry) { $catalogEntry.Stage } else { $null }
        CatalogReflectionSignature = if ($catalogEntry) { $catalogEntry.ReflectionSignature } else { $null }
        CatalogCaptureSupport = if ($catalogEntry) { $catalogEntry.CaptureSupport } else { $null }
        CatalogInputSemantics = if ($catalogEntry) { $catalogEntry.InputSemanticsSignature } else { $null }
        CatalogOutputSemantics = if ($catalogEntry) { $catalogEntry.OutputSemanticsSignature } else { $null }
    }
}

Write-Host "Binary: $BinaryPath"
Write-Host "DXBC blobs found: $($results.Count)"
$matched = @($results | Where-Object { $_.MatchKind -ne 'none' })
Write-Host "Catalog matches: $($matched.Count)"
foreach ($row in $results) {
    if ($row.MatchKind -eq 'runtime_catalog') {
        Write-Host ("MATCH  {0}  size={1}  {2}  {3}" -f $row.Offset, $row.DxbcSize, $row.CatalogStage, $row.Sha256)
    }
    else {
        Write-Host ("BLOB   {0}  size={1}  {2}" -f $row.Offset, $row.DxbcSize, $row.Sha256)
    }
}

if (-not [string]::IsNullOrWhiteSpace($ExportDir)) {
    $ExportDir = [System.IO.Path]::GetFullPath($ExportDir)
    New-Item -ItemType Directory -Path $ExportDir -Force | Out-Null

    foreach ($row in $results) {
        if ($MatchOnly -and $row.MatchKind -eq 'none') {
            continue
        }

        $fileName = if ($row.MatchKind -eq 'runtime_catalog') {
            '{0}-{1}-{2}.dxbc' -f $row.CatalogStage, $row.Sha256, $row.Offset.Replace('0x', 'offset-')
        }
        else {
            'unknown-{0}-{1}.dxbc' -f $row.Sha256, $row.Offset.Replace('0x', 'offset-')
        }

        [System.IO.File]::WriteAllBytes((Join-Path $ExportDir $fileName), $row.Blob)
    }
}

if (-not [string]::IsNullOrWhiteSpace($OutputJson)) {
    $OutputJson = [System.IO.Path]::GetFullPath($OutputJson)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($OutputJson)) -Force | Out-Null
    $jsonRows = $results | ForEach-Object {
        [pscustomobject]@{
            BinaryPath = $_.BinaryPath
            Offset = $_.Offset
            OffsetDecimal = $_.OffsetDecimal
            DxbcSize = $_.DxbcSize
            Sha256 = $_.Sha256
            MatchKind = $_.MatchKind
            CatalogStage = $_.CatalogStage
            CatalogReflectionSignature = $_.CatalogReflectionSignature
            CatalogCaptureSupport = $_.CatalogCaptureSupport
            CatalogInputSemantics = $_.CatalogInputSemantics
            CatalogOutputSemantics = $_.CatalogOutputSemantics
        }
    }
    $jsonRows | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputJson -Encoding utf8
}

if (-not [string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $OutputMarkdown = [System.IO.Path]::GetFullPath($OutputMarkdown)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($OutputMarkdown)) -Force | Out-Null

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Embedded DXBC Extraction Report')
    $lines.Add('')
    $lines.Add(('- Binary: `{0}`' -f $BinaryPath))
    $lines.Add(('- DXBC blobs found: `{0}`' -f $results.Count))
    $lines.Add(('- Runtime catalog matches: `{0}`' -f $matched.Count))
    $lines.Add('')
    $lines.Add('| Offset | Size | SHA-256 | Match | Stage | Reflection | Capture Support |')
    $lines.Add('| --- | ---: | --- | --- | --- | --- | ---: |')
    foreach ($row in $results) {
        $lines.Add(('| `{0}` | {1} | `{2}` | `{3}` | `{4}` | `{5}` | {6} |' -f
                $row.Offset,
                $row.DxbcSize,
                $row.Sha256,
                $row.MatchKind,
                $(if ($row.CatalogStage) { $row.CatalogStage } else { '-' }),
                $(if ($row.CatalogReflectionSignature) { $row.CatalogReflectionSignature } else { '-' }),
                $(if ($row.CatalogCaptureSupport) { $row.CatalogCaptureSupport } else { '-' })))
    }

    Set-Content -Path $OutputMarkdown -Value $lines -Encoding utf8
}
