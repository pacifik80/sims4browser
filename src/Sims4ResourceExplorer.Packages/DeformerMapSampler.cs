using System.Numerics;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// One resolved deformer-map morph: a parsed sampler plus the modifier weight that should be
/// applied at every vertex sampled through it. <see cref="IsNormalMap"/> distinguishes shape
/// vs normal DMaps — a single SMOD can carry both, and they apply to different mesh attributes.
/// </summary>
public sealed record Ts4SimDeformerMorph(
    Ts4DeformerMapSampler Sampler,
    float Weight,
    bool IsNormalMap,
    uint Region);

/// <summary>
/// Decodes per-pixel shape/normal delta vectors from a parsed <see cref="Ts4DeformerMapResource"/>.
/// Each pixel stores 3 bytes (one per axis); a value of <c>0x80</c> means "no delta on this axis"
/// while any other byte <c>b</c> maps to <c>(b / 255) * SkinTightDelta + SkinTightMinVal</c>.
/// Scan lines may be uncompressed, RLE-compressed, or empty (NoData → all zero).
///
/// Mirrors the relevant decoding from TS4SimRipper <c>DMAP.ToMorphMap</c>. The sampler eagerly
/// decompresses every scan line on construction so that <see cref="SampleSkinDelta(int, int)"/>
/// is O(1) per call. Per-Sim memory is small (a typical face DMap is ~50×50 pixels = 7.5 KB
/// of decoded floats).
/// </summary>
public sealed class Ts4DeformerMapSampler
{
    private readonly Ts4DeformerMapResource map;
    private readonly Vector3[,] skinDeltas;
    private readonly Vector3[,]? robeDeltas;
    private readonly int height;
    private readonly int width;

    /// <summary>Width of the sampler grid: <c>maxCol - minCol + 1</c>.</summary>
    public int Width => width;
    /// <summary>Height of the sampler grid: <c>maxRow - minRow + 1</c>.</summary>
    public int Height => height;
    /// <summary>The DMap resource being sampled.</summary>
    public Ts4DeformerMapResource Map => map;
    /// <summary>True when the DMap carries the optional robe (over-clothing) channel.</summary>
    public bool HasRobeChannel => robeDeltas is not null;

    public Ts4DeformerMapSampler(Ts4DeformerMapResource map)
    {
        ArgumentNullException.ThrowIfNull(map);
        this.map = map;
        if (!map.HasData)
        {
            width = 0;
            height = 0;
            skinDeltas = new Vector3[0, 0];
            return;
        }

        width = (int)(map.MaxCol - map.MinCol + 1);
        height = (int)(map.MaxRow - map.MinRow + 1);
        if (map.ScanLines.Count != height)
        {
            throw new InvalidDataException(
                $"DMap scan-line count {map.ScanLines.Count} does not match expected height {height}.");
        }
        skinDeltas = new Vector3[height, width];
        var hasRobe = map.RobeChannel == Ts4DeformerMapRobeChannel.Present;
        if (hasRobe) robeDeltas = new Vector3[height, width];

        for (var row = 0; row < height; row++)
        {
            DecodeScanLine(map.ScanLines[row], row);
        }
    }

    /// <summary>
    /// Returns the shape delta vector at the given absolute pixel position. <paramref name="x"/>
    /// is a column in the FULL map (use <c>uvU * Map.Width</c> in your sampler before subtracting
    /// <c>Map.MinCol</c>); same for <paramref name="y"/> with rows.
    ///
    /// Out-of-bounds positions return <see cref="Vector3.Zero"/>. Empty maps always return zero.
    /// </summary>
    public Vector3 SampleSkinDelta(int x, int y)
    {
        if (skinDeltas.Length == 0) return Vector3.Zero;
        if (x < 0 || x >= width || y < 0 || y >= height) return Vector3.Zero;
        return skinDeltas[y, x];
    }

    /// <summary>
    /// Returns the robe-channel (over-clothing) delta vector at the given pixel position.
    /// Only meaningful when <see cref="HasRobeChannel"/>; otherwise always zero.
    /// </summary>
    public Vector3 SampleRobeDelta(int x, int y)
    {
        if (robeDeltas is null) return Vector3.Zero;
        if (x < 0 || x >= width || y < 0 || y >= height) return Vector3.Zero;
        return robeDeltas[y, x];
    }

    private void DecodeScanLine(Ts4DeformerMapScanLine scan, int row)
    {
        switch (scan.Compression)
        {
            case Ts4DeformerMapCompression.NoData:
                // All zero — already initialised by Vector3[,] default.
                return;

            case Ts4DeformerMapCompression.None:
                DecodeUncompressedRow(scan, row);
                return;

            case Ts4DeformerMapCompression.Rle:
                DecodeRleRow(scan, row);
                return;

            default:
                throw new InvalidDataException($"Unknown DMap compression {(byte)scan.Compression:X2} on row {row}.");
        }
    }

    private void DecodeUncompressedRow(Ts4DeformerMapScanLine scan, int row)
    {
        var includesRobe = scan.RobeChannel == Ts4DeformerMapRobeChannel.Present;
        var pixelStride = includesRobe ? 6 : 3;
        if (scan.UncompressedPixels.Length < width * pixelStride)
        {
            throw new InvalidDataException(
                $"DMap uncompressed row {row} too short: have {scan.UncompressedPixels.Length}, need {width * pixelStride}.");
        }
        for (var x = 0; x < width; x++)
        {
            var basePos = x * pixelStride;
            skinDeltas[row, x] = DecodeSkinTriplet(
                scan.UncompressedPixels[basePos],
                scan.UncompressedPixels[basePos + 1],
                scan.UncompressedPixels[basePos + 2]);
            if (robeDeltas is not null)
            {
                if (includesRobe)
                {
                    robeDeltas[row, x] = DecodeRobeTriplet(
                        scan.UncompressedPixels[basePos + 3],
                        scan.UncompressedPixels[basePos + 4],
                        scan.UncompressedPixels[basePos + 5]);
                }
                else if (scan.RobeChannel == Ts4DeformerMapRobeChannel.Dropped)
                {
                    robeDeltas[row, x] = Vector3.Zero;
                }
                else
                {
                    // ROBECHANNEL_ISCOPY (rare) — copy the skin triplet as the robe delta.
                    robeDeltas[row, x] = DecodeRobeTriplet(
                        scan.UncompressedPixels[basePos],
                        scan.UncompressedPixels[basePos + 1],
                        scan.UncompressedPixels[basePos + 2]);
                }
            }
        }
    }

    private void DecodeRleRow(Ts4DeformerMapScanLine scan, int row)
    {
        // Mirrors the RLE walk in TS4SimRipper DMAP.ToMorphMap. The pixelPos/dataPos index
        // tables let us seek to the right run for a given column. Each run is:
        //   [runLength byte] [pixelStride bytes for the run's repeated value]
        // The run stride includes robe bytes only when scan.RobeChannel == Present.
        if (scan.NumIndexes == 0) return;
        var includesRobe = scan.RobeChannel == Ts4DeformerMapRobeChannel.Present;
        var pixelStride = includesRobe ? 6 : 3;
        var stepDivisor = scan.NumIndexes - 1;
        if (stepDivisor < 1) stepDivisor = 1;
        var step = 1 + (width / stepDivisor);
        for (var x = 0; x < width; x++)
        {
            var idx = Math.Min(x / step, scan.NumIndexes - 1);
            var pixelPosX = scan.PixelPosIndexes[idx];
            var dataPos = scan.DataPosIndexes[idx] * (pixelStride + 1);
            if (dataPos >= scan.RleArrayOfPixels.Length) break;
            var runLength = scan.RleArrayOfPixels[dataPos];
            while (x >= pixelPosX + runLength)
            {
                pixelPosX += runLength;
                dataPos += 1 + pixelStride;
                if (dataPos >= scan.RleArrayOfPixels.Length) return;
                runLength = scan.RleArrayOfPixels[dataPos];
            }
            var pixelStart = dataPos + 1;
            if (pixelStart + 2 >= scan.RleArrayOfPixels.Length) return;
            skinDeltas[row, x] = DecodeSkinTriplet(
                scan.RleArrayOfPixels[pixelStart],
                scan.RleArrayOfPixels[pixelStart + 1],
                scan.RleArrayOfPixels[pixelStart + 2]);
            if (robeDeltas is not null)
            {
                if (includesRobe && pixelStart + 5 < scan.RleArrayOfPixels.Length)
                {
                    robeDeltas[row, x] = DecodeRobeTriplet(
                        scan.RleArrayOfPixels[pixelStart + 3],
                        scan.RleArrayOfPixels[pixelStart + 4],
                        scan.RleArrayOfPixels[pixelStart + 5]);
                }
                else if (scan.RobeChannel == Ts4DeformerMapRobeChannel.Dropped)
                {
                    robeDeltas[row, x] = Vector3.Zero;
                }
                else
                {
                    robeDeltas[row, x] = DecodeRobeTriplet(
                        scan.RleArrayOfPixels[pixelStart],
                        scan.RleArrayOfPixels[pixelStart + 1],
                        scan.RleArrayOfPixels[pixelStart + 2]);
                }
            }
        }
    }

    private Vector3 DecodeSkinTriplet(byte bx, byte by, byte bz)
    {
        return new Vector3(
            DecodeAxis(bx, map.SkinTightMinVal, map.SkinTightDelta),
            DecodeAxis(by, map.SkinTightMinVal, map.SkinTightDelta),
            DecodeAxis(bz, map.SkinTightMinVal, map.SkinTightDelta));
    }

    private Vector3 DecodeRobeTriplet(byte bx, byte by, byte bz)
    {
        return new Vector3(
            DecodeAxis(bx, map.RobeMinVal, map.RobeDelta),
            DecodeAxis(by, map.RobeMinVal, map.RobeDelta),
            DecodeAxis(bz, map.RobeMinVal, map.RobeDelta));
    }

    private static float DecodeAxis(byte b, float minVal, float delta)
    {
        // TS4SimRipper convention: 0x80 means "no delta" (centre of the [-min, max] range
        // doesn't contribute to morph) — see DMAP.cs:928-933.
        if (b == 0x80) return 0f;
        return (float)(((b * (double)delta) / 255d) + minVal);
    }
}
