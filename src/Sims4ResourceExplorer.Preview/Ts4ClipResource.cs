// Ts4ClipResource — decoder for the Sims 4 CLIP animation resource (type 0x6B20C4F3, v14)
// and its embedded S3_CLIP keyframe codec (token "_pilC3S_").
//
// The codec was reverse-engineered by INVERTING the *encoder* in thepancake1/_s4animtools
// (the authoritative open-source Sims4 pose/animation tool — s3pi and TS4SimRipper do NOT
// carry the keyframe codec). The decode below is validated without Unity by checking that
// every decoded orientation quaternion is unit-length (|q| ≈ 1.0); see ProbeAsset --clip-decode.
//
// Key facts that make the dequant correct (the parts everyone gets wrong):
//   * Each Frame = u16 startTick, u16 SIGN-BITS mask, then the components.
//     The sign mask is NOT opaque flags: bit i = sign of component i (1 ⇒ negative);
//     bits 4-6 are unused, bit 7 = "snap" frame.
//   * Quaternions quantize at 12-bit  → divide the raw integer by 4095.
//     Translations quantize at 10-bit → divide by 1023.
//     width-10 channels pack three 10-bit values into a u32 (also /1023).
//   * value = offset + sign * (raw / maxV) * scale   (offset/scale are per-channel f32).
//   * Quaternion component order is X, Y, Z, W (the encoder reorders w,x,y,z → x,y,z,w).
//   * channel.target is the FNV-1 32-bit hash of the bone name (lowercased), matching
//     Ts4RigBone.NameHash. Use Fnv32() to map a bone name back to a channel.

using System.Text;

namespace Sims4ResourceExplorer.Preview;

/// <summary>Animation channel target sub-type (which transform component it drives).</summary>
public enum Ts4ClipSubTarget : byte
{
    Unknown = 0,
    Translation = 1,
    Orientation = 2,
    Scale = 3,
}

/// <summary>One decoded keyframe: the tick it starts at, plus up to 4 component values
/// (XYZ for translation/scale; XYZW quaternion for orientation).</summary>
public readonly record struct Ts4ClipKeyframe(int Tick, float X, float Y, float Z, float W);

/// <summary>One animation channel: all keyframes for a single (bone, sub-target) pair.
/// Offset/Scale are the raw per-channel dequant params (exposed for diagnostics).</summary>
public sealed record Ts4ClipChannel(
    uint TargetHash,
    Ts4ClipSubTarget SubTarget,
    int ChannelType,
    IReadOnlyList<Ts4ClipKeyframe> Keyframes,
    float Offset = 0f,
    float Scale = 0f);

/// <summary>A fully decoded CLIP: header metadata + per-channel keyframe tracks.
/// InitialOffset* = the clip's root reference transform from the CLIP_RESOURCE header.</summary>
public sealed record Ts4ClipResource(
    string Name,
    string RigNamespace,
    float TickLength,
    int NumTicks,
    IReadOnlyList<Ts4ClipChannel> Channels,
    System.Numerics.Quaternion InitialOffsetRotation = default,
    System.Numerics.Vector3 InitialOffsetTranslation = default)
{
    /// <summary>Frames per second (1 / tickLength); defaults to 30 if tickLength is 0.</summary>
    public float Fps => TickLength > 1e-6f ? 1f / TickLength : 30f;

    /// <summary>Clip duration in seconds.</summary>
    public float Duration => NumTicks * TickLength;
}

/// <summary>Parses + decodes <see cref="Ts4ClipResource"/> from raw CLIP bytes.</summary>
public static class Ts4ClipDecoder
{
    /// <summary>FNV-1 (NOT FNV-1a) 32-bit hash of a bone name, lowercased + UTF-8 encoded —
    /// matches the s4animtools <c>get_32bit_hash</c> and <see cref="Ts4RigBone.NameHash"/>.</summary>
    public static uint Fnv32(string name)
    {
        var bytes = Encoding.UTF8.GetBytes(name.ToLowerInvariant());
        uint hval = 0x811c9dc5;
        foreach (var b in bytes)
        {
            hval *= 0x01000193;  // unchecked: wraps mod 2^32, which is what we want
            hval ^= b;
        }
        return hval;
    }

    // Component count and storage width (bytes; 10 ⇒ packed 10-bit-triple) per channelType.
    private static int CountFor(int t) => t switch { 1 or 5 => 1, 2 or 6 => 2, 3 or 7 or 18 => 3, 4 or 8 or 19 or 20 or 21 => 4, _ => 0 };
    private static int WidthFor(int t) => t switch { 5 or 6 or 7 or 8 => 1, 18 or 19 or 21 => 10, 1 or 2 or 3 or 4 or 20 => 2, _ => 0 };

    private static string ReadIoString(BinaryReader r)
    {
        var n = r.ReadUInt32();
        return n > 4096 ? string.Empty : Encoding.ASCII.GetString(r.ReadBytes((int)n));
    }

    /// <summary>Walks the CLIP_RESOURCE v14 header and returns the embedded S3_CLIP codec
    /// byte range plus the clip name + rig namespace. Returns null on a non-v14 / malformed clip.</summary>
    private static (long Start, long Len, string Name, string RigNs, System.Numerics.Quaternion InitQ, System.Numerics.Vector3 InitT)? ReadHeader(byte[] b)
    {
        try
        {
            using var ms = new MemoryStream(b, writable: false);
            using var r = new BinaryReader(ms);
            if (r.ReadUInt32() != 14) return null;                     // version
            r.ReadUInt32();                                            // flags
            r.ReadSingle();                                            // duration
            var initQ = new System.Numerics.Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()); // initialOffsetQ (x,y,z,w)
            var initT = new System.Numerics.Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());                     // initialOffsetT
            r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32(); r.ReadUInt32(); // refNs + surface/joint + surfacechild hashes
            var name = ReadIoString(r);
            var rigNs = ReadIoString(r);
            var ec = r.ReadUInt32(); for (var i = 0; i < ec && i < 256; i++) ReadIoString(r);   // explicitNamespaces[]
            var sc = r.ReadUInt32(); for (var i = 0; i < sc && i < 256; i++) { r.ReadUInt16(); r.ReadUInt16(); ReadIoString(r); ReadIoString(r); } // ikSlotAssignments
            var ev = r.ReadUInt32(); for (var i = 0; i < ev && i < 4096; i++) { r.ReadUInt32(); var z = r.ReadUInt32(); r.BaseStream.Position += z; } // events
            var len = r.ReadUInt32();
            return (r.BaseStream.Position, len, name, rigNs, initQ, initT);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Reads ONLY the CLIP header (cheap) — name + rig namespace, without decoding
    /// any keyframes. Used to find a clip by name across tens of thousands of resources.</summary>
    public static (string Name, string RigNamespace)? PeekHeader(byte[] clipBytes)
    {
        var h = ReadHeader(clipBytes);
        return h is null ? null : (h.Value.Name, h.Value.RigNs);
    }

    /// <summary>Fully decodes the CLIP into per-channel keyframe tracks. Returns null if the
    /// bytes are not a v14 CLIP. Constant channels (Zero/One/Identity, width 0) are skipped —
    /// those bones simply hold their bind pose.</summary>
    public static Ts4ClipResource? Decode(byte[] clipBytes)
    {
        var header = ReadHeader(clipBytes);
        if (header is null) return null;
        var (cStart, cLen, name, rigNs, initQ, initT) = header.Value;
        if (cStart < 0 || cLen <= 0 || cStart + cLen > clipBytes.Length) return null;

        var codec = new byte[cLen];
        Array.Copy(clipBytes, cStart, codec, 0, cLen);
        using var cr = new BinaryReader(new MemoryStream(codec, writable: false));

        cr.ReadBytes(8);                                  // token "_pilC3S_"
        cr.ReadUInt32();                                  // version
        cr.ReadUInt32();                                  // flags
        var tickLen = cr.ReadSingle();
        var numTicks = cr.ReadUInt16();
        cr.ReadUInt16();                                  // pad
        var chCount = cr.ReadUInt32();
        cr.ReadUInt32();                                  // f1PaletteSize
        var chDataOff = cr.ReadUInt32();
        // (remaining header offsets — f1PaletteDataOffset, nameOffset, sourceAssetNameOffset — unused here)

        var channels = new List<Ts4ClipChannel>((int)Math.Min(chCount, 4096));
        for (uint i = 0; i < chCount; i++)
        {
            cr.BaseStream.Position = chDataOff + i * 20;  // S3Channel = 4+4+4+4+2+1+1 = 20 bytes
            var dataOff = cr.ReadUInt32();
            var target = cr.ReadUInt32();
            var off = cr.ReadSingle();
            var scl = cr.ReadSingle();
            var numFrames = cr.ReadUInt16();
            int chType = cr.ReadByte();
            int sub = cr.ReadByte();

            var count = CountFor(chType);
            var width = WidthFor(chType);
            if (numFrames == 0 || width == 0 || count == 0) continue;  // constant / empty channel

            var maxV = width == 1 ? 255f : (width == 10 ? 1023f : (sub == 2 ? 4095f : 1023f));
            var keys = new List<Ts4ClipKeyframe>(numFrames);

            cr.BaseStream.Position = dataOff;
            for (var f = 0; f < numFrames; f++)
            {
                int startTick = cr.ReadUInt16();
                var signBits = cr.ReadUInt16();
                var raw = new uint[4];
                if (width == 1) { for (var c = 0; c < count; c++) raw[c] = cr.ReadByte(); cr.BaseStream.Position += (4 - count); }
                else if (width == 2) { for (var c = 0; c < count; c++) raw[c] = cr.ReadUInt16(); if (count % 2 != 0) cr.BaseStream.Position += 2; }
                else /* width == 10 */ { var packed = cr.ReadUInt32(); raw[0] = packed & 1023; raw[1] = (packed >> 10) & 1023; raw[2] = (packed >> 20) & 1023; }

                var v = new float[4];
                for (var c = 0; c < count; c++)
                {
                    var sgn = ((signBits >> c) & 1) != 0 ? -1f : 1f;
                    v[c] = off + sgn * (raw[c] / maxV) * scl;
                }
                keys.Add(new Ts4ClipKeyframe(startTick, v[0], v[1], v[2], v[3]));
            }

            var subTarget = sub switch { 1 => Ts4ClipSubTarget.Translation, 2 => Ts4ClipSubTarget.Orientation, 3 => Ts4ClipSubTarget.Scale, _ => Ts4ClipSubTarget.Unknown };
            channels.Add(new Ts4ClipChannel(target, subTarget, chType, keys, off, scl));
        }

        return new Ts4ClipResource(name, rigNs, tickLen, numTicks, channels, initQ, initT);
    }
}
