using System.Buffers.Binary;
using LlamaLogic.Packages.Formats;

namespace Sims4ResourceExplorer.Packages;

/// <summary>
/// Decodes TS4 <c>LRLE</c> images (resource type-name <c>LRLEImage</c>, variant of EA's
/// custom run-length-encoded texture container). Ported from
/// <c>docs/references/external/TS4SimRipper/src/LRLE.cs</c> — the only publicly available
/// reference for this format. Decode-side only; we do not encode.
/// <para/>
/// Format outline (per the reference):
/// <list type="bullet">
///   <item>Magic <c>'LRLE'</c> (<c>0x454C524C</c>, little-endian).</item>
///   <item>Version: <c>0</c> (V1) or <c>'V002'</c> = <c>0x32303056</c> (V2). They use
///         distinct instruction streams; only V2 includes a palette table after the offsets.</item>
///   <item>Width/height <c>UInt16</c>; mip count + mip offsets.</item>
///   <item>For V2, a <c>NumPixels</c> count followed by that many 4-byte BGRA palette entries.</item>
///   <item>Per-mip RLE instruction stream. We decode mip 0 only (the highest-resolution image)
///         since the consumer only samples the base mip.</item>
/// </list>
/// Pixels are emitted in 4×4 block-major order (DXT-style block layout) and reordered to
/// linear row-major before the DDS wrapper is constructed.
/// </summary>
internal static class Ts4LrleTextureDecoder
{
    private const uint LrleMagic = 0x454C524C; // 'LRLE'
    private const uint LrleVersion2 = 0x32303056; // 'V002'

    private const uint DdsSignature = 0x20534444;
    private const uint DdsHeaderSize = 124;
    private const uint DdsPixelFormatSize = 32;
    private const uint DdsPfRgb = 0x00000040;
    private const uint DdsPfAlphaPixels = 0x00000001;
    private const uint DdsCapsTexture = 0x00001000;
    private const uint DdsdCaps = 0x00000001;
    private const uint DdsdHeight = 0x00000002;
    private const uint DdsdWidth = 0x00000004;
    private const uint DdsdPitch = 0x00000008;
    private const uint DdsdPixelFormat = 0x00001000;

    public static byte[] DecodeToPng(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var (width, height, pixelsBgra) = DecodeMip0(bytes);
        var ddsBytes = WrapAsUncompressedRgbaDds(width, height, pixelsBgra);
        return DirectDrawSurface.GetPngDataFromDdsData(ddsBytes).ToArray();
    }

    private static (ushort Width, ushort Height, byte[] Pixels) DecodeMip0(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(input);

        var magic = reader.ReadUInt32();
        if (magic != LrleMagic)
        {
            throw new InvalidDataException($"Expected LRLE magic 0x{LrleMagic:X8}, found 0x{magic:X8}.");
        }
        var version = reader.ReadUInt32();
        if (version != 0u && version != LrleVersion2)
        {
            throw new InvalidDataException($"Unsupported LRLE version 0x{version:X8}.");
        }
        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        var numMipMaps = reader.ReadUInt32();
        if (numMipMaps == 0)
        {
            throw new InvalidDataException("LRLE has no mip maps.");
        }
        var mipOffsets = new int[numMipMaps];
        for (var i = 0; i < numMipMaps; i++)
        {
            mipOffsets[i] = reader.ReadInt32();
        }

        byte[][]? palette = null;
        if (version == LrleVersion2)
        {
            var numPixels = reader.ReadUInt32();
            palette = new byte[numPixels][];
            for (var i = 0; i < numPixels; i++)
            {
                palette[i] = reader.ReadBytes(4);
            }
        }

        // Mip 0 starts at the current stream position; its size is offset[1] - offset[0]
        // when more than one mip exists, otherwise we read to the end.
        var mip0Start = (int)input.Position;
        var mip0Length = numMipMaps > 1
            ? mipOffsets[1] - mipOffsets[0]
            : bytes.Length - mip0Start;
        if (mip0Length <= 0 || mip0Start + mip0Length > bytes.Length)
        {
            throw new InvalidDataException($"LRLE mip 0 has invalid bounds (start={mip0Start}, length={mip0Length}).");
        }
        var mip0 = new byte[mip0Length];
        Array.Copy(bytes, mip0Start, mip0, 0, mip0Length);

        var blockPixels = version == LrleVersion2
            ? DecodeMipV2(mip0, width, height, palette ?? Array.Empty<byte[]>())
            : DecodeMipV1(mip0, width, height);
        var linearPixels = ReorderBlocksToLinear(blockPixels, width, height);
        return (width, height, linearPixels);
    }

    /// <summary>
    /// Port of <c>ReadMipV1</c> from the reference. Instruction is <c>byte0 &amp; 3</c>:
    /// 0 = run of zeros, 1 = run of embedded BGRA quads, 2 = run of one repeated BGRA quad,
    /// 3 = embedded sub-RLE producing planar BGRA blocks.
    /// </summary>
    private static byte[] DecodeMipV1(byte[] mip, int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var pointer = 0;
        var pixelPointer = 0;
        while (pointer < mip.Length)
        {
            var instruction = mip[pointer] & 3;
            switch (instruction)
            {
                case 1:
                {
                    var count = mip[pointer] >> 2;
                    pointer++;
                    Array.Copy(mip, pointer, pixels, pixelPointer, 4 * count);
                    pixelPointer += 4 * count;
                    pointer += 4 * count;
                    break;
                }
                case 2:
                {
                    var count = ReadPixelRunLength(mip, ref pointer);
                    pointer++;
                    for (var i = 0; i < count; i++)
                    {
                        Array.Copy(mip, pointer, pixels, pixelPointer, 4);
                        pixelPointer += 4;
                    }
                    pointer += 4;
                    break;
                }
                case 0:
                {
                    var count = ReadPixelRunLength(mip, ref pointer);
                    pointer++;
                    for (var i = 0; i < count * 4; i++)
                    {
                        pixels[pixelPointer++] = 0;
                    }
                    break;
                }
                case 3:
                {
                    var count = mip[pointer] >> 2;
                    pointer++;
                    var planar = ReadEmbeddedRleV1(mip, count, ref pointer);
                    for (var i = 0; i < count; i++)
                    {
                        pixels[pixelPointer + 0] = planar[i];
                        pixels[pixelPointer + 1] = planar[i + count];
                        pixels[pixelPointer + 2] = planar[i + 2 * count];
                        pixels[pixelPointer + 3] = planar[i + 3 * count];
                        pixelPointer += 4;
                    }
                    break;
                }
                default:
                    throw new InvalidDataException(
                        $"LRLE V1: unknown instruction 0x{mip[pointer]:X2} at offset {pointer}.");
            }
        }
        return pixels;
    }

    /// <summary>
    /// Port of <c>ReadEmbeddedRLE</c> from the reference. Used by V1 instruction 3 — emits
    /// a planar block of <c>pixelCount * 4</c> bytes (BBBB...GGGG...RRRR...AAAA...).
    /// </summary>
    private static byte[] ReadEmbeddedRleV1(byte[] data, int pixelCount, ref int pointer)
    {
        var result = new byte[pixelCount * 4];
        var resultPtr = 0;
        while (resultPtr < pixelCount * 4)
        {
            var head = data[pointer];
            if ((head & 1) == 1)
            {
                var count = (head & 0x7F) >> 1;
                if ((head & 0x80) == 0x80)
                {
                    pointer++;
                    count += data[pointer] << 6;
                }
                pointer++;
                Array.Copy(data, pointer, result, resultPtr, count);
                resultPtr += count;
                pointer += count;
            }
            else if ((head & 2) == 2)
            {
                var count = (head & 0x7F) >> 2;
                if ((head & 0x80) == 0x80)
                {
                    pointer++;
                    count += data[pointer] << 5;
                }
                pointer++;
                for (var i = 0; i < count; i++)
                {
                    result[resultPtr++] = data[pointer];
                }
                pointer++;
            }
            else
            {
                var count = (head & 0x7F) >> 2;
                if ((head & 0x80) == 0x80)
                {
                    pointer++;
                    count += data[pointer] << 5;
                }
                for (var i = 0; i < count; i++)
                {
                    result[resultPtr++] = 0;
                }
                pointer++;
            }
        }
        return result;
    }

    /// <summary>
    /// Port of <c>ReadMipV2</c> from the reference. Instructions are decoded from the low
    /// three bits of <c>byte0</c>:
    /// <list type="bullet">
    ///   <item>bits 1+2 (mask 0x03): copy following BGRA pixels inline.</item>
    ///   <item>bits 2+4 (mask 0x06): repeat one following BGRA pixel.</item>
    ///   <item>bit 1 only:           copy palette pixels via following indices (variable length).</item>
    ///   <item>bit 2 only:           repeat palette pixel referenced by single-byte index.</item>
    ///   <item>bit 4 only:           repeat palette pixel referenced by two-byte index.</item>
    /// </list>
    /// </summary>
    private static byte[] DecodeMipV2(byte[] mip, int width, int height, byte[][] palette)
    {
        var pixels = new byte[width * height * 4];
        var pointer = 0;
        var pixelPointer = 0;
        while (pointer < mip.Length && pixelPointer < pixels.Length)
        {
            var head = mip[pointer];
            var bit1 = (head & 0x01) > 0;
            var bit2 = (head & 0x02) > 0;
            var bit4 = (head & 0x04) > 0;
            if (bit1 && bit2)
            {
                var count = ReadPixelRunLength(mip, ref pointer);
                pointer++;
                for (var i = 0; i < count; i++)
                {
                    Array.Copy(mip, pointer, pixels, pixelPointer, 4);
                    pixelPointer += 4;
                    pointer += 4;
                }
            }
            else if (!bit1 && bit2 && bit4)
            {
                var count = ReadRepeatRunLength(mip, ref pointer);
                pointer++;
                for (var i = 0; i < count; i++)
                {
                    Array.Copy(mip, pointer, pixels, pixelPointer, 4);
                    pixelPointer += 4;
                }
                pointer += 4;
            }
            else if (bit1 && !bit2)
            {
                var count = ReadPixelRunLength(mip, ref pointer);
                pointer++;
                for (var i = 0; i < count; i++)
                {
                    var index = ReadColorIndex(mip, ref pointer);
                    Array.Copy(palette[index], 0, pixels, pixelPointer, 4);
                    pixelPointer += 4;
                    pointer++;
                }
            }
            else if (bit2 && !bit1 && !bit4)
            {
                var count = ReadRepeatRunLength(mip, ref pointer);
                pointer++;
                int index = mip[pointer];
                for (var i = 0; i < count; i++)
                {
                    Array.Copy(palette[index], 0, pixels, pixelPointer, 4);
                    pixelPointer += 4;
                }
                pointer++;
            }
            else if (bit4 && !bit1 && !bit2)
            {
                var count = ReadRepeatRunLength(mip, ref pointer);
                pointer++;
                int index = BinaryPrimitives.ReadUInt16LittleEndian(mip.AsSpan(pointer, 2));
                for (var i = 0; i < count; i++)
                {
                    Array.Copy(palette[index], 0, pixels, pixelPointer, 4);
                    pixelPointer += 4;
                }
                pointer += 2;
            }
            else
            {
                throw new InvalidDataException(
                    $"LRLE V2: unknown instruction 0x{head:X2} at offset {pointer}.");
            }
        }
        return pixels;
    }

    /// <summary>
    /// Port of <c>GetPixelRunLength</c>. Count is in bits 2..6 of <c>byte0</c>; if bit 7 is
    /// set, additional bytes contribute their low 7 bits with shift starting at 5 and stepping
    /// by 7. The pointer is advanced through any continuation bytes but stops on the last one
    /// of the length encoding.
    /// </summary>
    private static int ReadPixelRunLength(byte[] mip, ref int pointer)
    {
        var count = (mip[pointer] & 0x7F) >> 2;
        var shift = 5;
        while ((mip[pointer] & 0x80) != 0)
        {
            pointer++;
            count += (mip[pointer] & 0x7F) << shift;
            shift += 7;
        }
        return count;
    }

    /// <summary>Port of <c>GetRepeatRunLength</c>. Count is in bits 3..6, shift starts at 4.</summary>
    private static int ReadRepeatRunLength(byte[] mip, ref int pointer)
    {
        var count = (mip[pointer] & 0x7F) >> 3;
        var shift = 4;
        while ((mip[pointer] & 0x80) != 0)
        {
            pointer++;
            count += (mip[pointer] & 0x7F) << shift;
            shift += 7;
        }
        return count;
    }

    /// <summary>Port of <c>GetColorIndex</c>. Count is bits 0..6, shift starts at 7.</summary>
    private static int ReadColorIndex(byte[] mip, ref int pointer)
    {
        var count = mip[pointer] & 0x7F;
        var shift = 7;
        while ((mip[pointer] & 0x80) != 0)
        {
            pointer++;
            count += (mip[pointer] & 0x7F) << shift;
            shift += 7;
        }
        return count;
    }

    /// <summary>
    /// LRLE emits 4×4 BGRA blocks in DXT-style block-major order. This converts the block
    /// layout to a linear row-major BGRA image. Mirrors the post-decode loop at
    /// <c>LRLE.cs:195-214</c> / <c>LRLE.cs:400-419</c>.
    /// </summary>
    private static byte[] ReorderBlocksToLinear(byte[] blockPixels, int width, int height)
    {
        var linear = new byte[blockPixels.Length];
        var x = 0;
        var y = 0;
        var rowStrideBytes = width * 4;
        for (var i = 0; i < blockPixels.Length; i += 64)
        {
            for (var j = 0; j < 4; j++)
            {
                var dstOffset = y * rowStrideBytes + x;
                if (dstOffset + 16 <= linear.Length)
                {
                    Array.Copy(blockPixels, i + j * 16, linear, dstOffset, 16);
                }
                y++;
            }
            x += 16;
            if (x >= rowStrideBytes)
            {
                x = 0;
            }
            else
            {
                y -= 4;
            }
        }
        return linear;
    }

    /// <summary>
    /// Wraps a raw BGRA8 pixel buffer in a minimal uncompressed A8R8G8B8 DDS. The masks
    /// match Direct3D's standard 32-bit ARGB layout: R=0x00FF0000, G=0x0000FF00, B=0x000000FF,
    /// A=0xFF000000. In little-endian memory this is byte order B, G, R, A — matching the
    /// LRLE-decoded output exactly.
    /// </summary>
    private static byte[] WrapAsUncompressedRgbaDds(int width, int height, byte[] pixelsBgra)
    {
        var pitch = width * 4;
        var ddsSize = 128 + pixelsBgra.Length;
        var dds = new byte[ddsSize];
        var span = dds.AsSpan();

        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], DdsSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(4, 4), DdsHeaderSize);
        var flags = DdsdCaps | DdsdHeight | DdsdWidth | DdsdPixelFormat | DdsdPitch;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(8, 4), flags);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(12, 4), (uint)height);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(16, 4), (uint)width);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(20, 4), (uint)pitch);
        // depth=0, mipMapCount=0, reserved1[11]=0 — span is already zero-initialized.

        // DDS_PIXELFORMAT at offset 76.
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(76, 4), DdsPixelFormatSize);
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(80, 4), DdsPfRgb | DdsPfAlphaPixels);
        // fourCC at 84 = 0 (no FourCC for uncompressed).
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(88, 4), 32u); // RGBBitCount
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(92, 4), 0x00FF0000u); // R mask
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(96, 4), 0x0000FF00u); // G mask
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(100, 4), 0x000000FFu); // B mask
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(104, 4), 0xFF000000u); // A mask

        // dwCaps at 108.
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(108, 4), DdsCapsTexture);
        // caps2..caps4, reserved2 = 0.

        pixelsBgra.AsSpan().CopyTo(span[128..]);
        return dds;
    }
}
