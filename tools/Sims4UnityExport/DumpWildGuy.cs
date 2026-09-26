// DumpWildGuy — DIAGNOSTIC dumper + classifier for the wild_guy FemaleDefaultNudeSkin LRLEImages.
//
// PURPOSE
// -------
// The HD adult-female export (exportsimhd -> full_af_hd) came out WHITE/grayscale because the
// skin-base REDIRECT in ModOverride force-substituted wild_guy LRLE 53F13B3669333A6A as the skin
// base. That texture turned out to be grayscale/near-white, so the skin lost its warm color.
//
// This command opens "wild_guy FemaleDefaultNudeSkin.package", decodes EVERY LRLEImage to a PNG
// (via IResourceCatalogService.GetTexturePngAsync — the SAME decode the exporter's texture path
// uses), writes each PNG to <UnityAssetsDir>/_wildguy/, and classifies it by mean RGB:
//   - COLORED-WARM-SKIN : saturated, R>G>B, beige/brown (a real skin-color base).
//   - GRAYSCALE/NEAR-WHITE : R≈G≈B, high value (a grayscale detail/spec map, NOT a color base).
//
// The classification table answers the model question: are wild_guy's LRLEs per-tone COLOR bases
// (pick a warm one) or grayscale detail layers (no color base -> revert to EA's warm base)?
//
// Nothing here writes into src/; all output goes to <UnityAssetsDir>/_wildguy/.

using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class WildGuyDumper
{
    // The LRLE the override currently force-substitutes (the one that produced the white atlas).
    private const ulong CurrentlyUsedInstance = 0x53F13B3669333A6Aul;

    public static async Task<bool> RunAsync(
        IResourceCatalogService catalog,
        string unityAssetsDir,
        string packagePath,
        CancellationToken ct)
    {
        if (!File.Exists(packagePath))
        {
            Console.Error.WriteLine($"[dumpwildguy] Package not found: {packagePath}");
            return false;
        }

        var outDir = Path.Combine(unityAssetsDir, "_wildguy");
        Directory.CreateDirectory(outDir);

        Console.WriteLine("============================================================");
        Console.WriteLine($"[dumpwildguy] package: {packagePath}");
        Console.WriteLine($"[dumpwildguy] output : {outDir}");
        Console.WriteLine("============================================================");

        var source = new DataSourceDefinition(Guid.NewGuid(), Path.GetFileName(packagePath), packagePath, SourceKind.Mods);
        PackageScanResult scan;
        try
        {
            scan = await catalog.ScanPackageAsync(source, packagePath, progress: null, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dumpwildguy] ScanPackageAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }

        var lrles = scan.Resources
            .Where(r => string.Equals(r.Key.TypeName, "LRLEImage", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.UncompressedSize ?? r.CompressedSize ?? 0)
            .ToList();

        Console.WriteLine($"[dumpwildguy] LRLEImage resources found: {lrles.Count}");
        Console.WriteLine();

        var rows = new List<ClassRow>();
        var index = 0;
        foreach (var resource in lrles)
        {
            index++;
            byte[]? png;
            try
            {
                png = await catalog.GetTexturePngAsync(packagePath, resource.Key, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[dumpwildguy] #{index} {resource.Key.FullTgi}: decode FAILED ({ex.GetType().Name}: {ex.Message}).");
                rows.Add(new ClassRow(resource.Key.FullInstance, resource.Key.FullTgi, 0, 0, 0, 0, 0, "DECODE-FAILED", false));
                continue;
            }
            if (png is not { Length: > 0 })
            {
                Console.Error.WriteLine($"[dumpwildguy] #{index} {resource.Key.FullTgi}: decode returned no bytes.");
                rows.Add(new ClassRow(resource.Key.FullInstance, resource.Key.FullTgi, 0, 0, 0, 0, 0, "NO-BYTES", false));
                continue;
            }

            var (w, h, meanR, meanG, meanB, sat) = AnalyzePng(png);
            var classification = Classify(meanR, meanG, meanB, sat);
            var isCurrent = resource.Key.FullInstance == CurrentlyUsedInstance;

            // File name: keep the instance hex so the chosen base is unambiguous; mark the current one.
            var marker = isCurrent ? "_CURRENTLY_USED" : string.Empty;
            var safeClass = classification.Replace("/", "-").Replace(" ", "_");
            var fileName = $"lrle_{index:D2}_{resource.Key.FullInstance:X16}_{safeClass}{marker}.png";
            await File.WriteAllBytesAsync(Path.Combine(outDir, fileName), png, ct).ConfigureAwait(false);

            rows.Add(new ClassRow(resource.Key.FullInstance, resource.Key.FullTgi, w, h, meanR, meanG, meanB, classification, isCurrent));

            Console.WriteLine(
                $"  #{index:D2} {resource.Key.FullTgi}  {w}x{h,-9}  mean=({meanR,3},{meanG,3},{meanB,3})  sat={sat,3}  -> {classification}{(isCurrent ? "   <== CURRENTLY USED" : string.Empty)}");
        }

        // --------- Summary + verdict ---------
        Console.WriteLine();
        Console.WriteLine("------------------------------------------------------------");
        var warm = rows.Where(r => r.Classification.StartsWith("COLORED-WARM-SKIN", StringComparison.Ordinal)).ToList();
        var gray = rows.Where(r => r.Classification.StartsWith("GRAYSCALE", StringComparison.Ordinal)).ToList();
        var other = rows.Except(warm).Except(gray).ToList();
        Console.WriteLine($"[dumpwildguy] COUNTS: COLORED-WARM-SKIN={warm.Count}  GRAYSCALE/NEAR-WHITE={gray.Count}  OTHER/FAILED={other.Count}  total={rows.Count}");

        var current = rows.FirstOrDefault(r => r.IsCurrent);
        if (current is not null)
        {
            Console.WriteLine($"[dumpwildguy] CURRENTLY-USED 0x{current.Instance:X16} classified as: {current.Classification} (mean RGB {current.MeanR},{current.MeanG},{current.MeanB}).");
        }
        else
        {
            Console.WriteLine($"[dumpwildguy] CURRENTLY-USED 0x{CurrentlyUsedInstance:X16} not found among LRLEImage resources.");
        }

        if (warm.Count == 0)
        {
            Console.WriteLine("[dumpwildguy] VERDICT: wild_guy has NO warm COLOR base LRLE — all are grayscale detail/spec maps.");
            Console.WriteLine("[dumpwildguy]          => REVERT the base override to EA's warm tone base; treat wild_guy as a DETAIL overlay.");
        }
        else
        {
            var best = warm
                .OrderByDescending(r => (long)r.Width * r.Height)
                .ThenBy(r => Math.Abs(r.MeanR - 175) + Math.Abs(r.MeanG - 135) + Math.Abs(r.MeanB - 110))
                .First();
            Console.WriteLine($"[dumpwildguy] VERDICT: {warm.Count} warm COLOR base candidate(s). Largest/most-natural = 0x{best.Instance:X16} ({best.Width}x{best.Height}, mean {best.MeanR},{best.MeanG},{best.MeanB}).");
        }

        // Write the classification table to a text file for the report.
        var reportPath = Path.Combine(outDir, "_WILDGUY_CLASSIFICATION.txt");
        var lines = new List<string>
        {
            $"WILD_GUY LRLE CLASSIFICATION — {Path.GetFileName(packagePath)}",
            $"counts: COLORED-WARM-SKIN={warm.Count}  GRAYSCALE/NEAR-WHITE={gray.Count}  OTHER/FAILED={other.Count}  total={rows.Count}",
            "",
            $"{"#",-3} {"INSTANCE",-16} {"DIMS",-11} {"meanR",5} {"meanG",5} {"meanB",5}  CLASS",
        };
        var n = 0;
        foreach (var r in rows)
        {
            n++;
            lines.Add($"{n,-3} {r.Instance:X16} {(r.Width + "x" + r.Height),-11} {r.MeanR,5} {r.MeanG,5} {r.MeanB,5}  {r.Classification}{(r.IsCurrent ? "   <== CURRENTLY USED" : string.Empty)}");
        }
        await File.WriteAllTextAsync(reportPath, string.Join(Environment.NewLine, lines), ct).ConfigureAwait(false);
        Console.WriteLine($"[dumpwildguy] wrote table: {reportPath}");
        Console.WriteLine("[dumpwildguy] DONE.");
        return true;
    }

    private sealed record ClassRow(
        ulong Instance, string FullTgi, int Width, int Height,
        int MeanR, int MeanG, int MeanB, string Classification, bool IsCurrent);

    // Classify on mean color. A real warm skin base is saturated and warm (R>G>B, beige/brown).
    // A grayscale/detail/spec map has R≈G≈B (very low saturation) and is usually high-value.
    private static string Classify(int r, int g, int b, int sat)
    {
        // Very low channel spread => grayscale (could be near-white or mid-gray detail).
        var spread = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
        var value = Math.Max(r, Math.Max(g, b));
        if (spread <= 12)
        {
            return value >= 170 ? "GRAYSCALE/NEAR-WHITE" : "GRAYSCALE/MID-GRAY";
        }
        // Warm skin: red is the dominant channel and ordering is R>=G>=B (beige/brown).
        if (r >= g && g >= b && (r - b) >= 15)
        {
            return "COLORED-WARM-SKIN";
        }
        // Anything else colored but not warm-skin ordered.
        return "COLORED-OTHER";
    }

    // Decode a PNG and compute the OPAQUE-pixel mean RGB (alpha==0 gutter pixels are excluded so a
    // green/transparent background does not skew the skin color), plus a saturation proxy.
    private static (int W, int H, int MeanR, int MeanG, int MeanB, int Sat) AnalyzePng(byte[] png)
    {
        try
        {
            using var ms = new MemoryStream(png, writable: false);
            using var src = new Bitmap(ms);
            var w = src.Width;
            var h = src.Height;
            using var argb = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var gfx = Graphics.FromImage(argb))
            {
                gfx.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                gfx.DrawImage(src, 0, 0, w, h);
            }

            var rect = new Rectangle(0, 0, w, h);
            var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var row = new byte[stride];
                // Sample a grid (every Nth pixel) to keep large 2048x4096 atlases fast.
                var stepX = Math.Max(1, w / 256);
                var stepY = Math.Max(1, h / 512);
                long sumR = 0, sumG = 0, sumB = 0, count = 0;
                for (var y = 0; y < h; y += stepY)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, stride);
                    for (var x = 0; x < w; x += stepX)
                    {
                        var o = x * 4;
                        var b = row[o + 0];
                        var g = row[o + 1];
                        var r = row[o + 2];
                        var a = row[o + 3];
                        if (a == 0)
                        {
                            continue; // skip transparent gutter
                        }
                        // Skip pure green RLE transparent-fill leftovers (R~0,G~162,B~0) if any survive.
                        if (r < 30 && g > 120 && b < 30)
                        {
                            continue;
                        }
                        sumR += r; sumG += g; sumB += b; count++;
                    }
                }
                if (count == 0)
                {
                    return (w, h, 0, 0, 0, 0);
                }
                var mr = (int)(sumR / count);
                var mg = (int)(sumG / count);
                var mb = (int)(sumB / count);
                var max = Math.Max(mr, Math.Max(mg, mb));
                var min = Math.Min(mr, Math.Min(mg, mb));
                var sat = max == 0 ? 0 : (int)((max - min) * 255L / max);
                return (w, h, mr, mg, mb, sat);
            }
            finally
            {
                argb.UnlockBits(data);
            }
        }
        catch
        {
            return (0, 0, 0, 0, 0, 0);
        }
    }
}
