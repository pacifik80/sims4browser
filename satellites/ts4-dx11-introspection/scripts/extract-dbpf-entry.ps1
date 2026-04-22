param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [UInt64]$Offset,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'

function Read-UInt32 {
    param($Reader)
    return $Reader.ReadUInt32()
}

function Read-UInt64 {
    param($Reader)
    return $Reader.ReadUInt64()
}

function Format-Hex32 {
    param([UInt32]$Value)
    return ('0x{0:X8}' -f $Value)
}

function Format-Hex64 {
    param([UInt64]$Value)
    return ('0x{0:X16}' -f $Value)
}

function Get-DbpfEntryForOffset {
    param(
        [string]$ResolvedPath,
        [UInt64]$TargetOffset
    )

    $stream = [System.IO.File]::OpenRead($ResolvedPath)
    $reader = New-Object System.IO.BinaryReader($stream)
    try {
        $fileId = [System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
        if ($fileId -ne 'DBPF') {
            throw "Not a DBPF package: $ResolvedPath"
        }

        [void](Read-UInt32 $reader) # file version major
        [void](Read-UInt32 $reader) # file version minor
        [void](Read-UInt32 $reader) # user version major
        [void](Read-UInt32 $reader) # user version minor
        [void](Read-UInt32 $reader) # unused1
        [void](Read-UInt32 $reader) # creation time
        [void](Read-UInt32 $reader) # updated time
        [void](Read-UInt32 $reader) # unused2
        $entryCount = Read-UInt32 $reader
        $indexPositionLow = Read-UInt32 $reader
        [void](Read-UInt32 $reader) # index size
        [void](Read-UInt32 $reader)
        [void](Read-UInt32 $reader)
        [void](Read-UInt32 $reader)
        [void](Read-UInt32 $reader)
        $indexPosition = Read-UInt64 $reader
        for ($i = 0; $i -lt 6; $i++) {
            [void](Read-UInt32 $reader)
        }

        $effectiveIndexPosition = if ($indexPosition -ne 0) { $indexPosition } else { [UInt64]$indexPositionLow }
        $stream.Position = [Int64]$effectiveIndexPosition

        $flagsValue = Read-UInt32 $reader
        $constantType = (($flagsValue -band 0x1) -ne 0)
        $constantGroup = (($flagsValue -band 0x2) -ne 0)
        $constantInstanceEx = (($flagsValue -band 0x4) -ne 0)

        $constantTypeId = if ($constantType) { Read-UInt32 $reader } else { $null }
        $constantGroupId = if ($constantGroup) { Read-UInt32 $reader } else { $null }
        $constantInstanceExId = if ($constantInstanceEx) { Read-UInt32 $reader } else { $null }

        for ($entryIndex = 0; $entryIndex -lt $entryCount; $entryIndex++) {
            $typeId = if ($constantType) { $constantTypeId } else { Read-UInt32 $reader }
            $groupId = if ($constantGroup) { $constantGroupId } else { Read-UInt32 $reader }
            $instanceEx = if ($constantInstanceEx) { $constantInstanceExId } else { Read-UInt32 $reader }
            $instance = Read-UInt32 $reader
            $position = Read-UInt32 $reader
            $sizeAndFlag = Read-UInt32 $reader
            $size = ($sizeAndFlag -band 0x7FFFFFFF)
            $hasExtendedCompression = (($sizeAndFlag -band 0x80000000) -ne 0)
            $sizeDecompressed = Read-UInt32 $reader
            $compressionType = $null
            $committed = $null
            if ($hasExtendedCompression) {
                $compressionType = $reader.ReadUInt16()
                $committed = $reader.ReadUInt16()
            }

            $entryStart = [UInt64]$position
            $entryEndExclusive = $entryStart + [UInt64]$size
            if ($TargetOffset -ge $entryStart -and $TargetOffset -lt $entryEndExclusive) {
                return [pscustomobject]@{
                    EntryIndex = $entryIndex
                    TypeId = $typeId
                    GroupId = $groupId
                    InstanceEx = $instanceEx
                    Instance = $instance
                    Position = [UInt64]$position
                    Size = [UInt32]$size
                    SizeDecompressed = [UInt32]$sizeDecompressed
                    HasExtendedCompression = $hasExtendedCompression
                    CompressionType = $compressionType
                    Committed = $committed
                    RelativeOffset = [UInt64]($TargetOffset - $entryStart)
                }
            }
        }

        throw ('Offset {0} was not covered by any DBPF entry' -f (Format-Hex64 $TargetOffset))
    }
    finally {
        $reader.Dispose()
        $stream.Dispose()
    }
}

$resolved = (Resolve-Path -LiteralPath $PackagePath).Path
$outputPath = [System.IO.Path]::GetFullPath($OutputDir)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null

$entry = Get-DbpfEntryForOffset -ResolvedPath $resolved -TargetOffset $Offset

$stream = [System.IO.File]::OpenRead($resolved)
try {
    $stream.Position = [Int64]$entry.Position
    $raw = New-Object byte[] $entry.Size
    $totalRead = 0
    while ($totalRead -lt $raw.Length) {
        $read = $stream.Read($raw, $totalRead, $raw.Length - $totalRead)
        if ($read -le 0) {
            throw "Unexpected EOF while reading DBPF entry payload"
        }
        $totalRead += $read
    }
}
finally {
    $stream.Dispose()
}

$stem = '{0}-{1}-{2}-{3}' -f `
    $entry.EntryIndex, `
    (Format-Hex32 $entry.TypeId).Replace('0x', 'type-'), `
    (Format-Hex32 $entry.GroupId).Replace('0x', 'group-'), `
    (Format-Hex32 $entry.Instance).Replace('0x', 'instance-')

$rawPath = Join-Path $outputPath ($stem + '.bin')
[System.IO.File]::WriteAllBytes($rawPath, $raw)

$decompressedPath = $null
if ($entry.HasExtendedCompression -and $entry.CompressionType -eq 0x5A42) {
    $inputStream = New-Object System.IO.MemoryStream(,$raw)
    if ($raw.Length -lt 6) {
        throw 'ZLIB-compressed DBPF record is too short to contain header and checksum.'
    }

    $inputStream.Position = 2
    $deflateLength = $raw.Length - 6
    $deflateBytes = New-Object byte[] $deflateLength
    [Array]::Copy($raw, 2, $deflateBytes, 0, $deflateLength)
    $deflateStreamSource = New-Object System.IO.MemoryStream(,$deflateBytes)
    $zlibStream = New-Object System.IO.Compression.DeflateStream($deflateStreamSource, [System.IO.Compression.CompressionMode]::Decompress)
    $outputStream = New-Object System.IO.MemoryStream
    try {
        $zlibStream.CopyTo($outputStream)
        $decompressed = $outputStream.ToArray()
        $decompressedPath = Join-Path $outputPath ($stem + '.decompressed.bin')
        [System.IO.File]::WriteAllBytes($decompressedPath, $decompressed)
    }
    finally {
        $zlibStream.Dispose()
        $deflateStreamSource.Dispose()
        $inputStream.Dispose()
        $outputStream.Dispose()
    }
}

Write-Host "Package:   $resolved"
Write-Host "Offset:    $(Format-Hex64 $Offset)"
Write-Host "Entry:     $($entry.EntryIndex)"
Write-Host "Type:      $(Format-Hex32 $entry.TypeId)"
Write-Host "Group:     $(Format-Hex32 $entry.GroupId)"
Write-Host "InstanceEx $(Format-Hex32 $entry.InstanceEx)"
Write-Host "Instance:  $(Format-Hex32 $entry.Instance)"
Write-Host "Range:     $((Format-Hex64 $entry.Position)) - $((Format-Hex64 ($entry.Position + $entry.Size - 1)))"
Write-Host "Size:      $($entry.Size)"
Write-Host "RelOff:    $((Format-Hex64 $entry.RelativeOffset))"
Write-Host "CompType:  $(if ($entry.HasExtendedCompression) { Format-Hex32 $entry.CompressionType } else { '-' })"
Write-Host "Raw out:   $rawPath"
if ($decompressedPath) {
    Write-Host "Decomp:    $decompressedPath"
}
