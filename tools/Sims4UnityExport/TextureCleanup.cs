// TextureCleanup — shared diffuse-texture post-processing used by every export path.
//
// EA's RLE2 textures fill fully-transparent blocks with green: pixels with alpha==0 decode to
// RGB ~ (0,162,0). The exported diffuse PNG is correct where it matters (the opaque UV island),
// but Unity's opaque material samples RGB and ignores alpha, so the green transparent background
// bleeds. Fix at the source: set RGB to (0,0,0) wherever alpha==0 (alpha left UNTOUCHED).
//
// For the SKIN atlas specifically, blackening is the WRONG cure: it trades a green seam for a
// black one, because Unity's bilinear/mipmap filtering still pulls the gutter color across UV
// island edges. DilateOpaque instead floods the nearest opaque (skin) color OUTWARD into the
// transparent gutters so edge filtering samples skin color — no green AND no black seam.
//
// Uses System.Drawing.Common (available on net8.0-windows) with Bitmap + LockBits.

namespace Sims4UnityExport;

internal static class TextureCleanup
{
    // Edge-pad ("dilate") the opaque region of a PNG OUTWARD into its transparent gutters so that
    // Unity's bilinear/mipmap filtering at UV-island edges samples real skin color instead of the
    // transparent-gutter color (green) or a blackened gutter. For `passes` iterations, every pixel
    // that is still transparent (in the working buffer) but borders at least one opaque pixel takes
    // the AVERAGE RGB of its opaque 4-neighbors and becomes opaque for subsequent passes — so the
    // fill grows one pixel-ring per pass. Only gutter RGB is overwritten; the saved PNG keeps every
    // pixel's ORIGINAL alpha (so any alpha-clip use still sees the true transparency).
    // Re-saves the PNG over the same file. Returns the count of gutter pixels filled.
    public static int DilateOpaque(string pngPath, int passes)
    {
        if (passes < 1) passes = 1;

        using var bitmap = LoadDetachedArgb(pngPath);
        var width = bitmap.Width;
        var height = bitmap.Height;

        var rect = new System.Drawing.Rectangle(0, 0, width, height);
        var data = bitmap.LockBits(
            rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var filled = 0;
        try
        {
            var stride = data.Stride;            // bytes per row (may exceed width*4 due to padding)
            var buffer = new byte[stride * height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            // Format32bppArgb in memory (little-endian) is laid out B, G, R, A per pixel.
            // Working "opaque" mask: pixel currently counts as a flood source. Seeded from the real
            // alpha (alpha>0 == opaque). Filled gutter pixels flip to opaque for the next pass.
            var opaque = new bool[width * height];
            for (var y = 0; y < height; y++)
            {
                var rowStart = y * stride;
                for (var x = 0; x < width; x++)
                {
                    opaque[y * width + x] = buffer[rowStart + x * 4 + 3] != 0;
                }
            }

            for (var pass = 0; pass < passes; pass++)
            {
                // Pixels to flip opaque AFTER this pass (so a pass uses only the previous ring).
                var newlyOpaque = new List<int>();

                for (var y = 0; y < height; y++)
                {
                    var rowStart = y * stride;
                    for (var x = 0; x < width; x++)
                    {
                        var pix = y * width + x;
                        if (opaque[pix])
                        {
                            continue; // already filled / real skin
                        }

                        // Average RGB over opaque 4-neighbors.
                        int sumB = 0, sumG = 0, sumR = 0, n = 0;
                        if (x > 0 && opaque[pix - 1]) { Accumulate(buffer, rowStart + (x - 1) * 4, ref sumB, ref sumG, ref sumR); n++; }
                        if (x + 1 < width && opaque[pix + 1]) { Accumulate(buffer, rowStart + (x + 1) * 4, ref sumB, ref sumG, ref sumR); n++; }
                        if (y > 0 && opaque[pix - width]) { Accumulate(buffer, (y - 1) * stride + x * 4, ref sumB, ref sumG, ref sumR); n++; }
                        if (y + 1 < height && opaque[pix + width]) { Accumulate(buffer, (y + 1) * stride + x * 4, ref sumB, ref sumG, ref sumR); n++; }

                        if (n == 0)
                        {
                            continue; // no opaque neighbor yet — fill on a later pass
                        }

                        var i = rowStart + x * 4;
                        buffer[i + 0] = (byte)(sumB / n); // B
                        buffer[i + 1] = (byte)(sumG / n); // G
                        buffer[i + 2] = (byte)(sumR / n); // R
                        // alpha (i+3) left untouched — original transparency preserved.
                        newlyOpaque.Add(pix);
                        filled++;
                    }
                }

                if (newlyOpaque.Count == 0)
                {
                    break; // nothing left adjacent to fill — done early
                }
                foreach (var pix in newlyOpaque)
                {
                    opaque[pix] = true;
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
        return filled;
    }

    private static void Accumulate(byte[] buffer, int offset, ref int sumB, ref int sumG, ref int sumR)
    {
        sumB += buffer[offset + 0];
        sumG += buffer[offset + 1];
        sumR += buffer[offset + 2];
    }

    // Set RGB=(0,0,0) for every pixel whose alpha==0 in the given PNG (alpha preserved),
    // then re-save the PNG over the same file. Returns the count of pixels modified.
    public static int BlackenTransparentPixels(string pngPath)
    {
        // Load into an explicit 32bpp ARGB bitmap so the pixel layout is known regardless of how
        // the source PNG was encoded. The source Bitmap keeps the PNG file locked for its whole
        // lifetime, so we copy its pixels into a fresh, file-detached ARGB bitmap and dispose the
        // source immediately — otherwise saving back over the same path throws a GDI+ error.
        using var bitmap = LoadDetachedArgb(pngPath);

        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(
            rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var blackened = 0;
        try
        {
            var stride = data.Stride;            // bytes per row (may exceed width*4 due to padding)
            var rowBytes = bitmap.Width * 4;     // valid bytes in a row
            var buffer = new byte[stride * bitmap.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);

            // Format32bppArgb in memory (little-endian) is laid out B, G, R, A per pixel.
            for (var y = 0; y < bitmap.Height; y++)
            {
                var rowStart = y * stride;
                for (var x = 0; x < rowBytes; x += 4)
                {
                    var i = rowStart + x;
                    if (buffer[i + 3] == 0)
                    {
                        buffer[i + 0] = 0; // B
                        buffer[i + 1] = 0; // G
                        buffer[i + 2] = 0; // R
                        blackened++;
                    }
                }
            }

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data.Scan0, buffer.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        bitmap.Save(pngPath, System.Drawing.Imaging.ImageFormat.Png);
        return blackened;
    }

    // True when the PNG has ANY fully-transparent pixel (alpha==0). Used to decide whether the
    // blacken pass is needed at all: an opaque texture (e.g. the skin base atlas) has none, so we
    // skip blackening entirely and never touch its RGB. Cheap single read-only LockBits scan.
    public static bool HasTransparentPixels(string pngPath)
    {
        using var bitmap = LoadDetachedArgb(pngPath);
        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(
            rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var rowBytes = bitmap.Width * 4;
            var buffer = new byte[stride * bitmap.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            for (var y = 0; y < bitmap.Height; y++)
            {
                var rowStart = y * stride;
                for (var x = 0; x < rowBytes; x += 4)
                {
                    if (buffer[rowStart + x + 3] == 0)
                    {
                        return true;
                    }
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return false;
    }

    // Load a PNG into a fresh 32bpp ARGB bitmap and immediately release the on-disk file.
    private static System.Drawing.Bitmap LoadDetachedArgb(string pngPath)
    {
        using var source = new System.Drawing.Bitmap(pngPath);
        var copy = new System.Drawing.Bitmap(
            source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(copy);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }
}
