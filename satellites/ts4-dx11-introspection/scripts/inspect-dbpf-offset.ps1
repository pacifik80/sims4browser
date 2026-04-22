param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [Parameter(Mandatory = $true)]
    [UInt64]$Offset
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

$resolved = (Resolve-Path -LiteralPath $PackagePath).Path
$stream = [System.IO.File]::OpenRead($resolved)
$reader = New-Object System.IO.BinaryReader($stream)

try {
    $fileId = [System.Text.Encoding]::ASCII.GetString($reader.ReadBytes(4))
    if ($fileId -ne 'DBPF') {
        throw "Not a DBPF package: $resolved"
    }

    $fileVersionMajor = Read-UInt32 $reader
    $fileVersionMinor = Read-UInt32 $reader
    $userVersionMajor = Read-UInt32 $reader
    $userVersionMinor = Read-UInt32 $reader
    [void](Read-UInt32 $reader) # unused1
    [void](Read-UInt32 $reader) # creation time
    [void](Read-UInt32 $reader) # updated time
    [void](Read-UInt32 $reader) # unused2
    $entryCount = Read-UInt32 $reader
    $indexPositionLow = Read-UInt32 $reader
    $indexSize = Read-UInt32 $reader
    [void](Read-UInt32 $reader) # unused3[0]
    [void](Read-UInt32 $reader) # unused3[1]
    [void](Read-UInt32 $reader) # unused3[2]
    [void](Read-UInt32 $reader) # unused4
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

    $hit = $null
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
        if ($Offset -ge $entryStart -and $Offset -lt $entryEndExclusive) {
            $hit = [pscustomobject]@{
                PackagePath = $resolved
                Offset = $Offset
                FileVersion = '{0}.{1}' -f $fileVersionMajor, $fileVersionMinor
                UserVersion = '{0}.{1}' -f $userVersionMajor, $userVersionMinor
                EntryIndex = $entryIndex
                TypeId = $typeId
                GroupId = $groupId
                InstanceEx = $instanceEx
                Instance = $instance
                ResourceKey = ("{0}:{1}:{2}'{3}" -f (Format-Hex32 $typeId), (Format-Hex32 $groupId), (Format-Hex32 $instanceEx), (Format-Hex32 $instance))
                Position = [UInt64]$position
                Size = [UInt32]$size
                SizeDecompressed = [UInt32]$sizeDecompressed
                HasExtendedCompression = $hasExtendedCompression
                CompressionType = $compressionType
                Committed = $committed
                RelativeOffset = [UInt64]($Offset - $entryStart)
            }
            break
        }
    }

    if ($null -eq $hit) {
        throw ('Offset {0} was not covered by any DBPF index entry in {1}' -f (Format-Hex64 $Offset), $resolved)
    }

    $compressionLabel = switch ($hit.CompressionType) {
        0x0000 { 'Uncompressed' }
        0xFFFE { 'Streamable compression' }
        0xFFFF { 'Internal compression' }
        0xFFE0 { 'Deleted record' }
        0x5A42 { 'ZLIB' }
        $null { '-' }
        default { ('Unknown ({0})' -f (Format-Hex32 $hit.CompressionType)) }
    }

    Write-Host "Package: $($hit.PackagePath)"
    Write-Host "Offset:  $(Format-Hex64 $hit.Offset)"
    Write-Host "Entry:   $($hit.EntryIndex) / $entryCount"
    Write-Host "Key:     $($hit.ResourceKey)"
    Write-Host "Range:   $((Format-Hex64 $hit.Position)) - $((Format-Hex64 ($hit.Position + $hit.Size - 1)))"
    Write-Host "Size:    $($hit.Size)"
    Write-Host "RelOff:  $((Format-Hex64 $hit.RelativeOffset))"
    Write-Host "Comp:    $compressionLabel"
    Write-Host "Decomp:  $($hit.SizeDecompressed)"
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}
