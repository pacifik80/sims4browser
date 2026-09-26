// NormalGen — generate a tangent-space NORMAL map from a grayscale skin RELIEF (height) map via a
// Sobel gradient. Sims skin detail (WildGuy/EVE/Vanilla relief) is grayscale value; converting it to
// a normal gives the body's pores/anatomy real lighting response instead of a flat/waxy albedo.
//
// Input: any PNG; the RED channel is treated as height (the relief value). Output: an RGB normal map
// (R=x, G=y, B=z in 0..1), opaque. Name the output "*_normal*.png" so the Unity AssetPostprocessor
// imports it as a NormalMap.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Sims4UnityExport;

internal static class NormalGen
{
    /// <summary>
    /// Read <paramref name="inPng"/>'s red channel as a height field and write a tangent-space normal
    /// map to <paramref name="outPng"/>. <paramref name="strength"/> scales the bump (higher = deeper).
    /// </summary>
    public static void Generate(string inPng, string outPng, float strength = 4.0f)
    {
        using var src = new Bitmap(inPng);
        var w = src.Width;
        var h = src.Height;
        var height = ReadRedAsHeight(src, w, h);

        var outBuf = new byte[w * h * 4]; // BGRA
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // Central-difference Sobel-ish gradient (clamped at edges).
                var dx = (Sample(height, w, h, x + 1, y) - Sample(height, w, h, x - 1, y)) * strength;
                var dy = (Sample(height, w, h, x, y + 1) - Sample(height, w, h, x, y - 1)) * strength;

                // Tangent-space normal = normalize(-dx, -dy, 1).
                float nx = -dx, ny = -dy, nz = 1f;
                var len = (float)System.Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));
                if (len <= 1e-6f) { nx = 0; ny = 0; nz = 1; len = 1; }
                nx /= len; ny /= len; nz /= len;

                var i = ((y * w) + x) * 4;
                outBuf[i + 0] = Enc(nz); // B = z
                outBuf[i + 1] = Enc(ny); // G = y
                outBuf[i + 2] = Enc(nx); // R = x
                outBuf[i + 3] = 255;
            }
        }

        WriteBgra(outPng, outBuf, w, h);
    }

    /// <summary>
    /// Convert a decoded EA DST skin normal — a 2-channel tangent normal with X in the ALPHA channel and
    /// Y in the GREEN channel (verified vs TS4SimRipper PreviewControl.ConvertNormalMap: red=alpha, blue=255)
    /// — into a proper Unity tangent-space normal: R=X, G=Y, B=reconstructed Z. NOT a Sobel/height pass.
    /// <paramref name="intensity"/> scales the (subtle) X/Y deviation; 1 = as-authored, higher = deeper.
    /// </summary>
    public static void ConvertDstNormal(string inPng, string outPng, float intensity = 2.0f)
    {
        using var src = new Bitmap(inPng);
        var w = src.Width;
        var h = src.Height;
        using var argb = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(argb)) g.DrawImageUnscaled(src, 0, 0);

        var rect = new Rectangle(0, 0, w, h);
        var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var inBuf = new byte[w * h * 4];
        try
        {
            var rowBytes = w * 4;
            for (var y = 0; y < h; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), inBuf, y * rowBytes, rowBytes);
            }
        }
        finally { argb.UnlockBits(data); }

        var outBuf = new byte[w * h * 4]; // BGRA
        for (var i = 0; i < w * h; i++)
        {
            var p = i * 4; // BGRA in memory: B=p, G=p+1, R=p+2, A=p+3
            var nx = (((inBuf[p + 3] / 255f) * 2f) - 1f) * intensity; // X from ALPHA
            var ny = (((inBuf[p + 1] / 255f) * 2f) - 1f) * intensity; // Y from GREEN
            var nz2 = 1f - (nx * nx) - (ny * ny);
            var nz = nz2 > 0f ? (float)System.Math.Sqrt(nz2) : 0f;
            outBuf[p + 0] = Enc(nz); // B = z
            outBuf[p + 1] = Enc(ny); // G = y
            outBuf[p + 2] = Enc(nx); // R = x
            outBuf[p + 3] = 255;
        }
        WriteBgra(outPng, outBuf, w, h);
    }

    private static byte Enc(float n) // -1..1 -> 0..255
    {
        var v = (n * 0.5f) + 0.5f;
        if (v < 0f) v = 0f;
        if (v > 1f) v = 1f;
        return (byte)((v * 255f) + 0.5f);
    }

    private static float Sample(float[] hgt, int w, int h, int x, int y)
    {
        if (x < 0) x = 0; else if (x >= w) x = w - 1;
        if (y < 0) y = 0; else if (y >= h) y = h - 1;
        return hgt[(y * w) + x];
    }

    private static float[] ReadRedAsHeight(Bitmap src, int w, int h)
    {
        using var argb = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(argb)) g.DrawImageUnscaled(src, 0, 0);
        var rect = new Rectangle(0, 0, w, h);
        var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = w * 4;
            var row = new byte[rowBytes];
            var hgt = new float[w * h];
            for (var y = 0; y < h; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, rowBytes);
                for (var x = 0; x < w; x++)
                {
                    hgt[(y * w) + x] = row[(x * 4) + 2] / 255f; // R channel
                }
            }
            return hgt;
        }
        finally { argb.UnlockBits(data); }
    }

    private static void WriteBgra(string path, byte[] bgra, int w, int h)
    {
        using var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, w, h);
        var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var rowBytes = w * 4;
            for (var y = 0; y < h; y++)
            {
                Marshal.Copy(bgra, y * rowBytes, IntPtr.Add(data.Scan0, y * data.Stride), rowBytes);
            }
        }
        finally { bmp.UnlockBits(data); }
        bmp.Save(path, ImageFormat.Png);
    }
}
