param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [string]$CatalogPath,

    [string]$OutputMarkdown,

    [string]$OutputJson,

    [string]$ExportDir,

    [string]$Filter = '*.package',

    [switch]$Recurse,

    [switch]$MatchOnly
)

$ErrorActionPreference = 'Stop'

$scannerSource = @"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class DxbcStreamingScanner
{
    public static long[] FindMagicOffsets(string path)
    {
        byte[] magic = Encoding.ASCII.GetBytes("DXBC");
        const int chunkSize = 4 * 1024 * 1024;
        byte[] buffer = new byte[chunkSize];
        byte[] carry = new byte[magic.Length - 1];
        int carryLength = 0;
        long absoluteOffset = 0;
        List<long> offsets = new List<long>();

        using (var stream = File.OpenRead(path))
        {
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                int windowLength = carryLength + read;
                byte[] window = new byte[windowLength];
                if (carryLength > 0)
                {
                    Buffer.BlockCopy(carry, 0, window, 0, carryLength);
                }
                Buffer.BlockCopy(buffer, 0, window, carryLength, read);

                long baseOffset = absoluteOffset - carryLength;
                for (int i = 0; i <= windowLength - magic.Length; i++)
                {
                    if (window[i] == magic[0] &&
                        window[i + 1] == magic[1] &&
                        window[i + 2] == magic[2] &&
                        window[i + 3] == magic[3])
                    {
                        offsets.Add(baseOffset + i);
                    }
                }

                carryLength = Math.Min(magic.Length - 1, windowLength);
                if (carryLength > 0)
                {
                    Buffer.BlockCopy(window, windowLength - carryLength, carry, 0, carryLength);
                }

                absoluteOffset += read;
            }
        }

        return offsets.ToArray();
    }

    public static byte[] ReadDxbcBlob(string path, long offset)
    {
        using (var stream = File.OpenRead(path))
        {
            if (offset + 32 > stream.Length)
            {
                return null;
            }

            byte[] header = new byte[32];
            stream.Position = offset;
            if (stream.Read(header, 0, header.Length) != header.Length)
            {
                return null;
            }

            if (header[0] != (byte)'D' || header[1] != (byte)'X' || header[2] != (byte)'B' || header[3] != (byte)'C')
            {
                return null;
            }

            int size = BitConverter.ToInt32(header, 24);
            int chunkCount = BitConverter.ToInt32(header, 28);
            if (size <= 0 || chunkCount <= 0 || chunkCount > 128 || offset + size > stream.Length)
            {
                return null;
            }

            long offsetTableEnd = offset + 32 + (chunkCount * 4L);
            if (offsetTableEnd > offset + size || offsetTableEnd > stream.Length)
            {
                return null;
            }

            byte[] chunkOffsets = new byte[chunkCount * 4];
            if (stream.Read(chunkOffsets, 0, chunkOffsets.Length) != chunkOffsets.Length)
            {
                return null;
            }

            for (int i = 0; i < chunkCount; i++)
            {
                int chunkOffset = BitConverter.ToInt32(chunkOffsets, i * 4);
                if (chunkOffset < 32 + (chunkCount * 4) || chunkOffset + 8 > size)
                {
                    return null;
                }

                byte[] chunkHeader = new byte[8];
                stream.Position = offset + chunkOffset;
                if (stream.Read(chunkHeader, 0, chunkHeader.Length) != chunkHeader.Length)
                {
                    return null;
                }

                int chunkSize = BitConverter.ToInt32(chunkHeader, 4);
                if (chunkSize < 0 || chunkOffset + 8L + chunkSize > size)
                {
                    return null;
                }

                for (int j = 0; j < 4; j++)
                {
                    byte c = chunkHeader[j];
                    bool isAscii =
                        (c >= (byte)'A' && c <= (byte)'Z') ||
                        (c >= (byte)'0' && c <= (byte)'9') ||
                        c == (byte)'_';
                    if (!isAscii)
                    {
                        return null;
                    }
                }
            }

            byte[] blob = new byte[size];
            stream.Position = offset;
            int totalRead = 0;
            while (totalRead < size)
            {
                int read = stream.Read(blob, totalRead, size - totalRead);
                if (read <= 0)
                {
                    return null;
                }

                totalRead += read;
            }

            return blob;
        }
    }
}
"@

Add-Type -TypeDefinition $scannerSource | Out-Null

function Resolve-InputFiles {
    param(
        [string]$Path,
        [string]$Pattern,
        [switch]$Recursive
    )

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $item = Get-Item -LiteralPath $resolved
    if (-not $item.PSIsContainer) {
        return @($item)
    }

    $args = @{
        LiteralPath = $resolved
        File = $true
        Filter = $Pattern
    }
    if ($Recursive) {
        $args.Recurse = $true
    }

    return @(Get-ChildItem @args | Sort-Object FullName)
}

function Load-CatalogMap {
    param([string]$Path)

    $map = @{}
    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $map
    }

    $resolved = (Resolve-Path -LiteralPath $Path).Path
    $catalog = Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json
    foreach ($entry in $catalog.Entries) {
        $map[$entry.Hash] = $entry
    }

    return $map
}

function Get-DxbcCandidateOffsets {
    param([string]$Path)
    return @([DxbcStreamingScanner]::FindMagicOffsets($Path))
}

function Read-DxbcBlobAtOffset {
    param(
        [string]$Path,
        [long]$Offset
    )

    $blob = [DxbcStreamingScanner]::ReadDxbcBlob($Path, $Offset)
    if ($null -eq $blob) {
        return $null
    }

    return [pscustomobject]@{
        Size = [int]$blob.Length
        Blob = $blob
    }
}

function Get-SafeStem {
    param([string]$Value)

    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $builder = New-Object System.Text.StringBuilder
    foreach ($char in $Value.ToCharArray()) {
        if ($invalid -contains $char) {
            [void]$builder.Append('_')
        }
        else {
            [void]$builder.Append($char)
        }
    }

    return $builder.ToString()
}

$files = Resolve-InputFiles -Path $InputPath -Pattern $Filter -Recursive:$Recurse
if ($files.Count -eq 0) {
    throw "No files matched under $InputPath"
}

$catalogEntries = Load-CatalogMap -Path $CatalogPath
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$results = New-Object System.Collections.Generic.List[object]

foreach ($file in $files) {
    Write-Host "Scanning $($file.FullName)"
    $offsets = Get-DxbcCandidateOffsets -Path $file.FullName
    Write-Host ("  candidates: {0}" -f $offsets.Count)

    foreach ($offset in $offsets) {
        $blobInfo = Read-DxbcBlobAtOffset -Path $file.FullName -Offset $offset
        if ($null -eq $blobInfo) {
            continue
        }

        $hashBytes = $sha256.ComputeHash($blobInfo.Blob)
        $hash = -join ($hashBytes | ForEach-Object { $_.ToString('x2') })

        $catalogEntry = $null
        $matchKind = 'none'
        if ($catalogEntries.ContainsKey($hash)) {
            $catalogEntry = $catalogEntries[$hash]
            $matchKind = 'runtime_catalog'
        }

        $results.Add([pscustomobject]@{
                SourcePath = $file.FullName
                SourceName = $file.Name
                Offset = ('0x{0:X}' -f $offset)
                OffsetDecimal = $offset
                DxbcSize = $blobInfo.Size
                Sha256 = $hash
                MatchKind = $matchKind
                Blob = $blobInfo.Blob
                CatalogStage = if ($catalogEntry) { $catalogEntry.Stage } else { $null }
                CatalogReflectionSignature = if ($catalogEntry) { $catalogEntry.ReflectionSignature } else { $null }
                CatalogCaptureSupport = if ($catalogEntry) { $catalogEntry.CaptureSupport } else { $null }
                CatalogInputSemantics = if ($catalogEntry) { $catalogEntry.InputSemanticsSignature } else { $null }
                CatalogOutputSemantics = if ($catalogEntry) { $catalogEntry.OutputSemanticsSignature } else { $null }
            })
    }
}

$matched = @($results | Where-Object MatchKind -eq 'runtime_catalog')

Write-Host ''
Write-Host ("Files scanned: {0}" -f $files.Count)
Write-Host ("DXBC blobs found: {0}" -f $results.Count)
Write-Host ("Runtime catalog matches: {0}" -f $matched.Count)

if (-not [string]::IsNullOrWhiteSpace($ExportDir)) {
    $exportPath = [System.IO.Path]::GetFullPath($ExportDir)
    New-Item -ItemType Directory -Path $exportPath -Force | Out-Null

    foreach ($row in $results) {
        if ($MatchOnly -and $row.MatchKind -ne 'runtime_catalog') {
            continue
        }

        $sourceStem = Get-SafeStem ([System.IO.Path]::GetFileNameWithoutExtension($row.SourceName))
        $offsetStem = $row.Offset.Replace('0x', 'offset-')
        $fileName = if ($row.MatchKind -eq 'runtime_catalog') {
            '{0}-{1}-{2}-{3}.dxbc' -f $row.CatalogStage, $row.Sha256, $sourceStem, $offsetStem
        }
        else {
            'unknown-{0}-{1}-{2}.dxbc' -f $row.Sha256, $sourceStem, $offsetStem
        }

        [System.IO.File]::WriteAllBytes((Join-Path $exportPath $fileName), $row.Blob)
    }
}

if (-not [string]::IsNullOrWhiteSpace($OutputJson)) {
    $jsonPath = [System.IO.Path]::GetFullPath($OutputJson)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($jsonPath)) -Force | Out-Null

    $jsonRows = $results | ForEach-Object {
        [pscustomobject]@{
            SourcePath = $_.SourcePath
            SourceName = $_.SourceName
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

    $jsonRows | ConvertTo-Json -Depth 6 | Set-Content -Path $jsonPath -Encoding utf8
}

if (-not [string]::IsNullOrWhiteSpace($OutputMarkdown)) {
    $markdownPath = [System.IO.Path]::GetFullPath($OutputMarkdown)
    New-Item -ItemType Directory -Path ([System.IO.Path]::GetDirectoryName($markdownPath)) -Force | Out-Null

    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add('# Streaming DXBC Extraction Report')
    $lines.Add('')
    $lines.Add(('- Input path: `{0}`' -f (Resolve-Path -LiteralPath $InputPath).Path))
    $lines.Add(('- Files scanned: `{0}`' -f $files.Count))
    $lines.Add(('- DXBC blobs found: `{0}`' -f $results.Count))
    $lines.Add(('- Runtime catalog matches: `{0}`' -f $matched.Count))
    $lines.Add('')
    $lines.Add('| Source | Offset | Size | SHA-256 | Match | Stage | Reflection | Capture Support |')
    $lines.Add('| --- | --- | ---: | --- | --- | --- | --- | ---: |')
    foreach ($row in $results) {
        $lines.Add(('| `{0}` | `{1}` | {2} | `{3}` | `{4}` | `{5}` | `{6}` | {7} |' -f
                $row.SourceName,
                $row.Offset,
                $row.DxbcSize,
                $row.Sha256,
                $row.MatchKind,
                $(if ($row.CatalogStage) { $row.CatalogStage } else { '-' }),
                $(if ($row.CatalogReflectionSignature) { $row.CatalogReflectionSignature } else { '-' }),
                $(if ($row.CatalogCaptureSupport) { $row.CatalogCaptureSupport } else { '-' })))
    }

    Set-Content -Path $markdownPath -Value $lines -Encoding utf8
}
