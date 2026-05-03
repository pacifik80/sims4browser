using System.Buffers.Binary;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// Compression mode for a DMap scan line. Mirrors TS4SimRipper <c>CompressionType</c>.
/// </summary>
public enum Ts4DeformerMapCompression : byte
{
    None = 0,
    Rle = 1,
    NoData = 2,
}

/// <summary>
/// Whether a DMap scan line carries the optional robe (over-clothing) channel data.
/// Mirrors TS4SimRipper <c>RobeChannel</c>.
/// </summary>
public enum Ts4DeformerMapRobeChannel : byte
{
    Present = 0,
    Dropped = 1,
    NotApplicable = 2,
}

/// <summary>
/// One row of DMap data. Pixel layout depends on <see cref="Compression"/> and
/// <see cref="RobeChannel"/>:
/// <list type="bullet">
///   <item><c>None</c> + RobeChannel <c>Present</c>: 6 bytes per pixel (skin XYZ + robe XYZ)</item>
///   <item><c>None</c> otherwise: 3 bytes per pixel (skin XYZ)</item>
///   <item><c>Rle</c>: pixel position lookup tables + RLE byte stream (decoded by callers)</item>
///   <item><c>NoData</c>: row is entirely empty; no payload</item>
/// </list>
/// </summary>
public sealed record Ts4DeformerMapScanLine(
    ushort ScanLineDataSize,
    Ts4DeformerMapCompression Compression,
    Ts4DeformerMapRobeChannel RobeChannel,
    byte[] UncompressedPixels,
    byte NumIndexes,
    ushort[] PixelPosIndexes,
    ushort[] DataPosIndexes,
    byte[] RleArrayOfPixels)
{
    public bool HasData => Compression != Ts4DeformerMapCompression.NoData;
}

/// <summary>
/// A parsed DeformerMap (DMap) resource. DMaps drive per-vertex shape and normal deltas via
/// UV1-sampled lookups: each Sim has one DMap per body-shape modifier, and the renderer
/// samples the DMap at each vertex's UV1 position to compute a 3D offset for that vertex.
///
/// Format mirrored from TS4SimRipper <c>DMAP.cs</c>:
/// <list type="number">
///   <item>version (UInt32) — 7 is current</item>
///   <item>doubledWidth (UInt32), height (UInt32) — true map width is doubledWidth/2</item>
///   <item>ageGender (UInt32) — Sim age/gender flags this map applies to</item>
///   <item>species (UInt32) — present when version &gt; 5</item>
///   <item>physique (UInt8), shapeOrNormals (UInt8)</item>
///   <item>minCol, maxCol, minRow, maxRow (UInt32 each) — bounding box of non-zero data</item>
///   <item>robeChannel (UInt8) — whether scan lines carry robe channel</item>
///   <item>skinTightMinVal, skinTightDelta (Single) — present when version &gt; 6</item>
///   <item>robeMinVal, robeDelta (Single) — present when version &gt; 6 and robe channel present</item>
///   <item>totalBytes (Int32) — payload size in bytes; if 0 the map is empty</item>
///   <item>scanLine[height = maxRow - minRow + 1]</item>
/// </list>
/// </summary>
public sealed record Ts4DeformerMapResource(
    uint Version,
    uint Width,
    uint Height,
    uint AgeGender,
    uint Species,
    byte Physique,
    byte ShapeOrNormals,
    uint MinCol,
    uint MaxCol,
    uint MinRow,
    uint MaxRow,
    Ts4DeformerMapRobeChannel RobeChannel,
    float SkinTightMinVal,
    float SkinTightDelta,
    float RobeMinVal,
    float RobeDelta,
    int TotalBytes,
    IReadOnlyList<Ts4DeformerMapScanLine> ScanLines)
{
    public bool HasData => TotalBytes > 0 && ScanLines.Count > 0;

    public static Ts4DeformerMapResource Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length < 32)
        {
            throw new InvalidDataException($"DMap payload too short: {bytes.Length} bytes.");
        }

        var span = (ReadOnlySpan<byte>)bytes;
        var offset = 0;
        var version = ReadUInt32(span, ref offset);
        // v9+ DMaps appear to have a shifted header layout (physique/shapeOrNormals positions
        // diverge from v5/v7). TS4SimRipper doesn't support v9. Until we reverse-engineer the
        // exact layout, return an empty DMap so callers (DeformerMapResolver) skip it cleanly
        // instead of throwing. v9 covers ~9 of 564 EA-shipped DMaps (1.6%).
        if (version > 7)
        {
            return new Ts4DeformerMapResource(
                Version: version,
                Width: 0, Height: 0, AgeGender: 0, Species: 0,
                Physique: 0, ShapeOrNormals: 0,
                MinCol: 0, MaxCol: 0, MinRow: 0, MaxRow: 0,
                RobeChannel: Ts4DeformerMapRobeChannel.NotApplicable,
                SkinTightMinVal: 0, SkinTightDelta: 0,
                RobeMinVal: 0, RobeDelta: 0,
                TotalBytes: 0,
                ScanLines: []);
        }
        var doubledWidth = ReadUInt32(span, ref offset);
        var height = ReadUInt32(span, ref offset);
        var ageGender = ReadUInt32(span, ref offset);
        var species = version > 5 ? ReadUInt32(span, ref offset) : 0u;
        var physique = ReadByte(span, ref offset);
        var shapeOrNormals = ReadByte(span, ref offset);
        var minCol = ReadUInt32(span, ref offset);
        var maxCol = ReadUInt32(span, ref offset);
        var minRow = ReadUInt32(span, ref offset);
        var maxRow = ReadUInt32(span, ref offset);
        var robeChannelByte = ReadByte(span, ref offset);
        var robeChannel = (Ts4DeformerMapRobeChannel)robeChannelByte;

        float skinTightMinVal = 0f, skinTightDelta = 0f, robeMinVal = 0f, robeDelta = 0f;
        if (version > 6)
        {
            skinTightMinVal = ReadSingle(span, ref offset);
            skinTightDelta = ReadSingle(span, ref offset);
            if (robeChannel == Ts4DeformerMapRobeChannel.Present)
            {
                robeMinVal = ReadSingle(span, ref offset);
                robeDelta = ReadSingle(span, ref offset);
            }
            else
            {
                // TS4SimRipper falls back to skin values for the robe defaults.
                robeMinVal = skinTightMinVal;
                robeDelta = skinTightDelta;
            }
        }
        else
        {
            // Pre-v7 hardcoded defaults from TS4SimRipper SkinTightMinVal/Delta accessors.
            skinTightMinVal = shapeOrNormals == 0 ? -0.2f : -0.75f;
            skinTightDelta = shapeOrNormals == 0 ? 0.4f : 1.5f;
            robeMinVal = skinTightMinVal;
            robeDelta = skinTightDelta;
        }

        var totalBytes = (int)ReadUInt32(span, ref offset);
        var scanLines = new List<Ts4DeformerMapScanLine>();
        if (totalBytes > 0)
        {
            var width = (int)(maxCol - minCol + 1);
            var numScanLines = (int)(maxRow - minRow + 1);
            for (var i = 0; i < numScanLines; i++)
            {
                scanLines.Add(ReadScanLine(span, ref offset, width));
            }
        }

        return new Ts4DeformerMapResource(
            Version: version,
            Width: doubledWidth / 2,
            Height: height,
            AgeGender: ageGender,
            Species: species,
            Physique: physique,
            ShapeOrNormals: shapeOrNormals,
            MinCol: minCol,
            MaxCol: maxCol,
            MinRow: minRow,
            MaxRow: maxRow,
            RobeChannel: robeChannel,
            SkinTightMinVal: skinTightMinVal,
            SkinTightDelta: skinTightDelta,
            RobeMinVal: robeMinVal,
            RobeDelta: robeDelta,
            TotalBytes: totalBytes,
            ScanLines: scanLines);
    }

    private static Ts4DeformerMapScanLine ReadScanLine(ReadOnlySpan<byte> span, ref int offset, int width)
    {
        if (offset + 3 > span.Length)
        {
            throw new InvalidDataException("DMap scan line truncated at header.");
        }
        var dataSize = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2)); offset += 2;
        var compression = (Ts4DeformerMapCompression)span[offset]; offset += 1;

        Ts4DeformerMapRobeChannel rowRobeChannel;
        if (compression == Ts4DeformerMapCompression.NoData)
        {
            rowRobeChannel = Ts4DeformerMapRobeChannel.Dropped;
            return new Ts4DeformerMapScanLine(
                dataSize, compression, rowRobeChannel,
                Array.Empty<byte>(), 0, Array.Empty<ushort>(), Array.Empty<ushort>(), Array.Empty<byte>());
        }
        if (offset + 1 > span.Length)
        {
            throw new InvalidDataException("DMap scan line truncated at robe channel byte.");
        }
        rowRobeChannel = (Ts4DeformerMapRobeChannel)span[offset]; offset += 1;

        if (compression == Ts4DeformerMapCompression.None)
        {
            var pixelStride = rowRobeChannel == Ts4DeformerMapRobeChannel.Present ? 6 : 3;
            var byteCount = width * pixelStride;
            if (offset + byteCount > span.Length)
            {
                throw new InvalidDataException($"DMap uncompressed scan line truncated: need {byteCount} bytes.");
            }
            var pixels = span.Slice(offset, byteCount).ToArray();
            offset += byteCount;
            return new Ts4DeformerMapScanLine(
                dataSize, compression, rowRobeChannel,
                pixels, 0, Array.Empty<ushort>(), Array.Empty<ushort>(), Array.Empty<byte>());
        }
        else if (compression == Ts4DeformerMapCompression.Rle)
        {
            if (offset + 1 > span.Length)
            {
                throw new InvalidDataException("DMap RLE scan line truncated at index count.");
            }
            var numIndexes = span[offset]; offset += 1;
            var indexBytes = numIndexes * 2;
            if (offset + indexBytes * 2 > span.Length)
            {
                throw new InvalidDataException($"DMap RLE index tables truncated: need {indexBytes * 2} bytes.");
            }
            var pixelPos = new ushort[numIndexes];
            for (var i = 0; i < numIndexes; i++)
            {
                pixelPos[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
                offset += 2;
            }
            var dataPos = new ushort[numIndexes];
            for (var i = 0; i < numIndexes; i++)
            {
                dataPos[i] = BinaryPrimitives.ReadUInt16LittleEndian(span.Slice(offset, 2));
                offset += 2;
            }
            var headerSize = 4u + 1u + (4u * numIndexes);
            var payloadSize = (int)(dataSize - headerSize);
            if (payloadSize < 0 || offset + payloadSize > span.Length)
            {
                throw new InvalidDataException($"DMap RLE payload truncated: declared {payloadSize} bytes.");
            }
            var rlePixels = span.Slice(offset, payloadSize).ToArray();
            offset += payloadSize;
            return new Ts4DeformerMapScanLine(
                dataSize, compression, rowRobeChannel,
                Array.Empty<byte>(), numIndexes, pixelPos, dataPos, rlePixels);
        }
        else
        {
            throw new InvalidDataException($"DMap scan line has unknown compression byte 0x{(byte)compression:X2}.");
        }
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length) throw new InvalidDataException("DMap header truncated.");
        var v = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(offset, 4));
        offset += 4;
        return v;
    }

    private static byte ReadByte(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 1 > span.Length) throw new InvalidDataException("DMap header truncated.");
        var v = span[offset];
        offset += 1;
        return v;
    }

    private static float ReadSingle(ReadOnlySpan<byte> span, ref int offset)
    {
        if (offset + 4 > span.Length) throw new InvalidDataException("DMap header truncated.");
        var v = BinaryPrimitives.ReadSingleLittleEndian(span.Slice(offset, 4));
        offset += 4;
        return v;
    }
}
