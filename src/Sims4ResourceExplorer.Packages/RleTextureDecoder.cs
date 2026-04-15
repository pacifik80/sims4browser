using System.Buffers.Binary;
using LlamaLogic.Packages.Formats;

namespace Sims4ResourceExplorer.Packages;

internal static class Ts4RleTextureDecoder
{
    private const uint DdsSignature = 0x20534444;
    private const uint Dxt5FourCc = 0x35545844;
    private const uint DdsHeaderSize = 124;
    private const uint DdsPixelFormatSize = 32;
    private const uint DdsPixelFormatFourCc = 0x00000004;
    private const uint DdsCapsTexture = 0x00001000;
    private const uint DdsCapsComplex = 0x00000008;
    private const uint DdsCapsMipMap = 0x00400000;
    private const uint DdsdCaps = 0x00000001;
    private const uint DdsdHeight = 0x00000002;
    private const uint DdsdWidth = 0x00000004;
    private const uint DdsdPixelFormat = 0x00001000;
    private const uint DdsdLinearSize = 0x00080000;
    private const uint DdsdMipMapCount = 0x00020000;
    private static readonly byte[] FullTransparentAlpha = [0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];
    private static readonly byte[] FullOpaqueAlpha = [0x00, 0x05, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

    public static byte[] DecodeToPng(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        var ddsBytes = DecodeToDds(bytes);
        return DirectDrawSurface.GetPngDataFromDdsData(ddsBytes).ToArray();
    }

    private static byte[] DecodeToDds(byte[] bytes)
    {
        using var input = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(input);

        var format = reader.ReadUInt32();
        if (format != Dxt5FourCc)
        {
            throw new InvalidDataException($"Expected DXT5 RLE payload, found 0x{format:X8}.");
        }

        var version = (RleVersion)reader.ReadUInt32();
        if (version is not (RleVersion.Rle2 or RleVersion.Rles))
        {
            throw new InvalidDataException($"Unsupported RLE payload version 0x{(uint)version:X8}.");
        }

        var width = reader.ReadUInt16();
        var height = reader.ReadUInt16();
        var mipCount = reader.ReadUInt16();
        var unknown0E = reader.ReadUInt16();
        if (unknown0E != 0)
        {
            throw new InvalidDataException($"Unexpected RLE texture header value 0x{unknown0E:X4}.");
        }

        var mipHeaders = ReadMipHeaders(reader, version, mipCount, bytes.Length);

        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output);
        WriteDdsHeader(writer, width, height, mipCount);

        if (version == RleVersion.Rle2)
        {
            DecodeRle2Blocks(writer, bytes, mipHeaders, mipCount);
        }
        else
        {
            DecodeRlesBlocks(writer, bytes, mipHeaders, mipCount);
        }

        return output.ToArray();
    }

    private static MipHeader[] ReadMipHeaders(BinaryReader reader, RleVersion version, int mipCount, int totalLength)
    {
        var headers = new MipHeader[mipCount + 1];
        for (var index = 0; index < mipCount; index++)
        {
            headers[index] = new MipHeader(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                version == RleVersion.Rles ? reader.ReadInt32() : 0);
        }

        var first = headers[0];
        headers[mipCount] = version == RleVersion.Rles
            ? new MipHeader(first.Offset2, first.Offset3, first.Offset0, first.Offset1, first.Offset4, totalLength)
            : new MipHeader(first.Offset2, first.Offset3, first.Offset0, first.Offset1, totalLength, 0);
        return headers;
    }

    private static void WriteDdsHeader(BinaryWriter writer, ushort width, ushort height, ushort mipCount)
    {
        var linearSize = Math.Max(16u, (uint)Math.Max(1, (width + 3) / 4) * (uint)Math.Max(1, (height + 3) / 4) * 16u);
        var headerFlags = DdsdCaps | DdsdHeight | DdsdWidth | DdsdPixelFormat | DdsdLinearSize;
        var caps = DdsCapsTexture;
        if (mipCount > 1)
        {
            headerFlags |= DdsdMipMapCount;
            caps |= DdsCapsComplex | DdsCapsMipMap;
        }

        writer.Write(DdsSignature);
        writer.Write(DdsHeaderSize);
        writer.Write(headerFlags);
        writer.Write((int)height);
        writer.Write((int)width);
        writer.Write(linearSize);
        writer.Write(1);
        writer.Write((uint)mipCount);
        writer.Write(new byte[11 * sizeof(uint)]);
        writer.Write(DdsPixelFormatSize);
        writer.Write(DdsPixelFormatFourCc);
        writer.Write(Dxt5FourCc);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(caps);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
        writer.Write(0u);
    }

    private static void DecodeRle2Blocks(BinaryWriter writer, byte[] bytes, MipHeader[] headers, int mipCount)
    {
        for (var mipIndex = 0; mipIndex < mipCount; mipIndex++)
        {
            var header = headers[mipIndex];
            var next = headers[mipIndex + 1];

            var blockOffset2 = header.Offset2;
            var blockOffset3 = header.Offset3;
            var blockOffset0 = header.Offset0;
            var blockOffset1 = header.Offset1;

            for (var commandOffset = header.CommandOffset; commandOffset < next.CommandOffset; commandOffset += sizeof(ushort))
            {
                var command = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(commandOffset, sizeof(ushort)));
                var op = command & 3;
                var count = command >> 2;

                switch (op)
                {
                    case 0:
                        for (var i = 0; i < count; i++)
                        {
                            writer.Write(FullTransparentAlpha);
                            writer.Write(FullTransparentAlpha);
                        }
                        break;
                    case 1:
                        for (var i = 0; i < count; i++)
                        {
                            writer.Write(bytes, blockOffset0, 2);
                            writer.Write(bytes, blockOffset1, 6);
                            writer.Write(bytes, blockOffset2, 4);
                            writer.Write(bytes, blockOffset3, 4);
                            blockOffset2 += 4;
                            blockOffset3 += 4;
                            blockOffset0 += 2;
                            blockOffset1 += 6;
                        }
                        break;
                    case 2:
                        for (var i = 0; i < count; i++)
                        {
                            writer.Write(FullOpaqueAlpha);
                            writer.Write(bytes, blockOffset2, 4);
                            writer.Write(bytes, blockOffset3, 4);
                            blockOffset2 += 4;
                            blockOffset3 += 4;
                        }
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported RLE2 op {op}.");
                }
            }

            if (blockOffset0 != next.Offset0 ||
                blockOffset1 != next.Offset1 ||
                blockOffset2 != next.Offset2 ||
                blockOffset3 != next.Offset3)
            {
                throw new InvalidDataException("RLE2 block streams ended at unexpected offsets.");
            }
        }
    }

    private static void DecodeRlesBlocks(BinaryWriter writer, byte[] bytes, MipHeader[] headers, int mipCount)
    {
        for (var mipIndex = 0; mipIndex < mipCount; mipIndex++)
        {
            var header = headers[mipIndex];
            var next = headers[mipIndex + 1];

            var blockOffset2 = header.Offset2;
            var blockOffset3 = header.Offset3;
            var blockOffset0 = header.Offset0;
            var blockOffset1 = header.Offset1;
            var blockOffset4 = header.Offset4;

            for (var commandOffset = header.CommandOffset; commandOffset < next.CommandOffset; commandOffset += sizeof(ushort))
            {
                var command = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(commandOffset, sizeof(ushort)));
                var op = command & 3;
                var count = command >> 2;

                switch (op)
                {
                    case 0:
                        for (var i = 0; i < count; i++)
                        {
                            writer.Write(FullTransparentAlpha);
                            writer.Write(FullTransparentAlpha);
                        }
                        break;
                    case 1:
                        for (var i = 0; i < count; i++)
                        {
                            blockOffset0 += 2;
                            blockOffset1 += 6;
                            blockOffset2 += 4;
                            blockOffset3 += 4;
                            writer.Write(bytes, blockOffset4, 16);
                            blockOffset4 += 16;
                        }
                        break;
                    case 2:
                        for (var i = 0; i < count; i++)
                        {
                            writer.Write(FullOpaqueAlpha);
                            writer.Write(FullOpaqueAlpha);
                            blockOffset2 += 4;
                            blockOffset3 += 4;
                            blockOffset0 += 2;
                            blockOffset1 += 6;
                        }
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported RLES op {op}.");
                }
            }

            if (blockOffset0 != next.Offset0 ||
                blockOffset1 != next.Offset1 ||
                blockOffset2 != next.Offset2 ||
                blockOffset3 != next.Offset3 ||
                blockOffset4 != next.Offset4)
            {
                throw new InvalidDataException("RLES block streams ended at unexpected offsets.");
            }
        }
    }

    private readonly record struct MipHeader(
        int CommandOffset,
        int Offset2,
        int Offset3,
        int Offset0,
        int Offset1,
        int Offset4);

    private enum RleVersion : uint
    {
        Rle2 = 0x32454C52,
        Rles = 0x53454C52
    }
}
