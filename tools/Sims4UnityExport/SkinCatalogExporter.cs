// SkinCatalogExporter — emits the FULL skin catalog for an Adult-Female character so Unity can
// switch base tones / detail layers / eye colors at RUNTIME. This realizes the shared contract:
//
//   unity/Sims4Creator/Assets/Sims4/<slug>/Skin/
//     base_<toneId>.png    — one per catalog baseTone   (COLOR base, RGBA, UV-aligned, gutters dilated)
//     detail_<detailId>.png— one per catalog detailLayer (GRAYSCALE relief, RGBA, meaningful alpha=coverage)
//     eye_<eyeId>.png      — one per catalog eyeColor    (RGBA overlay, transparent except the iris island)
//
// All skin textures share the SAME EA body-atlas UV layout (the meshes' uv0). They are normalised to
// a single shared resolution (1024x2048) so the runtime shader can sample every layer with one set of
// UVs. The exporter ALSO bakes a preview skin_atlas.png with the EXACT compose formula the Unity
// runtime shader must use (see ComposeAlbedo) so the character renders before the shader is wired.
//
// Compose formula (per-channel, 0..1) — MUST match the Unity runtime shader bit-for-bit:
//   albedo = baseTone.rgb
//   for each ACTIVE detail layer (g = detail.r, a = detail.a):
//       ov = (albedo < 0.5) ? (2*albedo*g) : (1 - 2*(1-albedo)*(1-g))   // 'overlay', g broadcast to rgb
//       albedo = lerp(albedo, ov, a)
//   albedo = lerp(albedo, eye.rgb, eye.a)                                // eye iris source-over
//
// This file does NOT touch the assembled mesh/skeleton/parts — CharacterDefinitionExporter still owns
// that. It is invoked from CharacterDefinitionExporter to fill the new "skinCatalog" section + the
// per-option Skin/ PNGs + the contract-correct preview bake.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

// ---- character.json "skinCatalog" section (per the shared contract) ------------------------------
internal sealed class SkinCatalogBaseTone
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("texture")] public string Texture { get; set; } = string.Empty;
}

internal sealed class SkinCatalogDetailLayer
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("texture")] public string Texture { get; set; } = string.Empty;
    [JsonPropertyName("blend")] public string Blend { get; set; } = "overlay";
}

internal sealed class SkinCatalogEyeColor
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("texture")] public string Texture { get; set; } = string.Empty;
}

internal sealed class SkinCatalogDefault
{
    [JsonPropertyName("baseTone")] public string? BaseTone { get; set; }
    [JsonPropertyName("detailLayers")] public List<string> DetailLayers { get; set; } = new();
    [JsonPropertyName("eyeColor")] public string? EyeColor { get; set; }
}

internal sealed class SkinCatalogSection
{
    [JsonPropertyName("baseTones")] public List<SkinCatalogBaseTone> BaseTones { get; set; } = new();
    [JsonPropertyName("detailLayers")] public List<SkinCatalogDetailLayer> DetailLayers { get; set; } = new();
    [JsonPropertyName("eyeColors")] public List<SkinCatalogEyeColor> EyeColors { get; set; } = new();
    [JsonPropertyName("default")] public SkinCatalogDefault Default { get; set; } = new();
}

internal static class SkinCatalogExporter
{
    // Shared atlas resolution: every skin layer is normalised to this so the runtime shader samples
    // all layers with ONE set of UVs. EA's body atlas authoring is 1024x2048; HD bases come through at
    // 2048x4096 and are downscaled mod-2 to 1024x2048 so the whole set shares one resolution.
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 2048;

    /// <summary>
    /// Resolve + write EVERY catalog option's texture into &lt;assetFolder&gt;/Skin/, bake the
    /// contract-correct preview albedo (skin_atlas.png), and return the populated skinCatalog section.
    /// </summary>
    public static async Task<SkinCatalogSection> ExportAsync(
        Catalog catalog,
        CharacterDefinition def,
        ISyntheticSimService synthetic,
        IResourceCatalogService catalogSvc,
        string assetFolder,
        string modsRoot,
        CancellationToken ct)
    {
        var skinFolder = Path.Combine(assetFolder, "Skin");
        Directory.CreateDirectory(skinFolder);

        var section = new SkinCatalogSection();

        Console.WriteLine("============================================================");
        Console.WriteLine($"skinCatalog: emitting ALL options for {def.Age} {def.Gender} into {skinFolder}");
        Console.WriteLine($"  baseTones={catalog.BaseTone.Count} detailLayers={catalog.DetailLayer.Count} eyeColors={catalog.EyeColor.Count}");
        Console.WriteLine("============================================================");

        // --- 1) base_<toneId>.png — every catalog baseTone's resolved COLOR base. ----------------
        // Keyed by id so the preview bake can pick the default tone's UV-aligned RGBA buffer.
        var baseBuffers = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var tone in catalog.BaseTone)
        {
            var toneInstance = tone.ToneInstanceValue != 0ul ? tone.ToneInstanceValue : 0x0000000000005545ul;
            SimSkintoneRenderSummary? skin = null;
            try
            {
                skin = await synthetic
                    .ResolveSkintoneRenderAsync(def.Age, def.Gender, toneInstance, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[skinCatalog] base '{tone.Id}': ResolveSkintoneRenderAsync FAILED ({ex.GetType().Name}: {ex.Message}); skipped.");
                continue;
            }
            if (skin?.BaseTexturePngBytes is not { Length: > 0 } baseBytes)
            {
                Console.Error.WriteLine($"[skinCatalog] base '{tone.Id}' (0x{toneInstance:X16}): no BaseTexturePngBytes; skipped.");
                continue;
            }

            var img = DecodeToAtlas(baseBytes);
            if (img is null)
            {
                Console.Error.WriteLine($"[skinCatalog] base '{tone.Id}': decode failed; skipped.");
                continue;
            }
            // The base is a COLOR substrate sharing the atlas UV gutters: DILATE the opaque skin color
            // outward into the transparent gutters (same cure the preview uses) so the runtime shader's
            // edge filtering does not pull black/empty across UV islands. Then FORCE alpha opaque so the
            // base is a solid RGBA color plate.
            DilateOpaqueInPlace(img, 16);
            ForceOpaque(img);

            var file = $"base_{Sanitize(tone.Id)}.png";
            WritePng(img, Path.Combine(skinFolder, file));
            var warm = WarmStats(img);
            Console.WriteLine($"[skinCatalog] base '{tone.Id}' (0x{toneInstance:X16}) -> Skin/{file} ({img.Width}x{img.Height}) mean RGB=({warm.R:0},{warm.G:0},{warm.B:0}) {(warm.Warm ? "WARM (R>G>B)" : "NOT warm")}.");

            baseBuffers[tone.Id] = img;
            section.BaseTones.Add(new SkinCatalogBaseTone
            {
                Id = tone.Id, Label = tone.Label, Texture = $"Skin/{file}",
            });
        }

        // --- 2) detail_<detailId>.png — every catalog detailLayer's GRAYSCALE relief. -------------
        // Real alpha/coverage is PRESERVED (meaningful holes = "show clean base"); no opacity forcing.
        var detailBuffers = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var detail in catalog.DetailLayer)
        {
            var png = await ResolveDetailReliefAsync(catalogSvc, detail, modsRoot, ct).ConfigureAwait(false);
            if (png is not { Length: > 0 })
            {
                Console.Error.WriteLine($"[skinCatalog] detail '{detail.Id}': could not resolve any texture TGI; skipped.");
                continue;
            }
            var img = DecodeToAtlas(png);
            if (img is null)
            {
                Console.Error.WriteLine($"[skinCatalog] detail '{detail.Id}': decode failed; skipped.");
                continue;
            }

            var file = $"detail_{Sanitize(detail.Id)}.png";
            WritePng(img, Path.Combine(skinFolder, file));
            var cov = CoverageStats(img);
            Console.WriteLine($"[skinCatalog] detail '{detail.Id}' -> Skin/{file} ({img.Width}x{img.Height}) blend={detail.Blend} coverage={cov:0.0%} (alpha preserved).");

            detailBuffers[detail.Id] = img;
            section.DetailLayers.Add(new SkinCatalogDetailLayer
            {
                Id = detail.Id, Label = detail.Label, Texture = $"Skin/{file}", Blend = detail.Blend,
            });
        }

        // --- 3) eye_<eyeId>.png — every catalog eyeColor CAS overlay (transparent except iris). ----
        var eyeBuffers = new Dictionary<string, RgbaImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var eye in catalog.EyeColor)
        {
            byte[]? png = null;
            if (eye.OverlayInstanceValue != 0ul)
            {
                try
                {
                    png = await synthetic.ResolveCasPartDiffusePngAsync(eye.OverlayInstanceValue, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[skinCatalog] eye '{eye.Id}': ResolveCasPartDiffusePngAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
                }
            }
            if (png is not { Length: > 0 })
            {
                Console.Error.WriteLine($"[skinCatalog] eye '{eye.Id}' (0x{eye.OverlayInstanceValue:X16}): no diffuse resolved; skipped.");
                continue;
            }
            var img = DecodeToAtlas(png);
            if (img is null)
            {
                Console.Error.WriteLine($"[skinCatalog] eye '{eye.Id}': decode failed; skipped.");
                continue;
            }

            var file = $"eye_{Sanitize(eye.Id)}.png";
            WritePng(img, Path.Combine(skinFolder, file));
            var loc = IrisLocalization(img);
            Console.WriteLine($"[skinCatalog] eye '{eye.Id}' (0x{eye.OverlayInstanceValue:X16}) -> Skin/{file} ({img.Width}x{img.Height}) opaque={loc.Coverage:0.00%} bbox={loc.Box} {(loc.Localized ? "LOCALIZED iris island" : "NOT localized (covers >25% — investigate)")}.");

            eyeBuffers[eye.Id] = img;
            section.EyeColors.Add(new SkinCatalogEyeColor
            {
                Id = eye.Id, Label = eye.Label, Texture = $"Skin/{file}",
            });
        }

        // --- 4) default selection (from the CharacterDefinition, clamped to what actually resolved). -
        var defaultBaseId = section.BaseTones.Any(t => string.Equals(t.Id, def.BaseTone, StringComparison.OrdinalIgnoreCase))
            ? def.BaseTone
            : section.BaseTones.FirstOrDefault()?.Id;
        var defaultEyeId = section.EyeColors.Any(e => string.Equals(e.Id, def.EyeColor, StringComparison.OrdinalIgnoreCase))
            ? def.EyeColor
            : section.EyeColors.FirstOrDefault()?.Id;
        var defaultDetailIds = def.DetailLayers
            .Where(id => section.DetailLayers.Any(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        section.Default = new SkinCatalogDefault
        {
            BaseTone = defaultBaseId,
            DetailLayers = defaultDetailIds,
            EyeColor = defaultEyeId,
        };

        // --- 5) preview skin_atlas.png — the EXACT contract compose formula. ----------------------
        // base + default detail layers + default eye, composed with ComposeAlbedo (the same math the
        // Unity runtime shader must use). Written into Skin/ so the character renders before the shader.
        if (defaultBaseId is not null && baseBuffers.TryGetValue(defaultBaseId, out var baseImg))
        {
            var activeDetails = defaultDetailIds
                .Select(id => detailBuffers.TryGetValue(id, out var d) ? d : null)
                .Where(d => d is not null)
                .Select(d => d!)
                .ToList();
            RgbaImage? eyeImg = defaultEyeId is not null && eyeBuffers.TryGetValue(defaultEyeId, out var e) ? e : null;

            var preview = ComposeAlbedo(baseImg, activeDetails, eyeImg);
            var previewPath = Path.Combine(skinFolder, "skin_atlas.png");
            WritePng(preview, previewPath);
            var warm = WarmStats(preview);
            Console.WriteLine(
                $"[skinCatalog] preview bake -> Skin/skin_atlas.png ({preview.Width}x{preview.Height}) " +
                $"base='{defaultBaseId}' details=[{string.Join(",", defaultDetailIds)}] eye='{defaultEyeId}' " +
                $"mean RGB=({warm.R:0},{warm.G:0},{warm.B:0}) {(warm.Warm ? "WARM" : "NOT warm")}.");
        }
        else
        {
            Console.Error.WriteLine("[skinCatalog] preview bake SKIPPED: no default base tone resolved.");
        }

        Console.WriteLine("============================================================");
        Console.WriteLine($"skinCatalog: wrote {section.BaseTones.Count} base, {section.DetailLayers.Count} detail, {section.EyeColors.Count} eye texture(s).");
        Console.WriteLine($"  default: baseTone='{section.Default.BaseTone}' detailLayers=[{string.Join(",", section.Default.DetailLayers)}] eyeColor='{section.Default.EyeColor}'.");
        Console.WriteLine("============================================================");

        return section;
    }

    // ---- the SHARED COMPOSE FORMULA (must match the Unity runtime shader, per-channel, 0..1) ------
    // albedo = baseTone.rgb
    // for each active detail (g = detail.r, a = detail.a):
    //     ov = overlay(albedo, g); albedo = lerp(albedo, ov, a)
    // albedo = lerp(albedo, eye.rgb, eye.a)
    private static RgbaImage ComposeAlbedo(RgbaImage baseImg, IReadOnlyList<RgbaImage> details, RgbaImage? eye)
    {
        var w = baseImg.Width;
        var h = baseImg.Height;
        var outPx = new byte[w * h * 4];
        var bp = baseImg.Pixels; // BGRA byte order in memory (Format32bppArgb little-endian)

        for (var i = 0; i < outPx.Length; i += 4)
        {
            // 0..1 base color, per channel. Memory order: [i]=B [i+1]=G [i+2]=R [i+3]=A.
            var b = bp[i] / 255f;
            var g = bp[i + 1] / 255f;
            var r = bp[i + 2] / 255f;

            foreach (var detail in details)
            {
                var dp = detail.Pixels;
                // detail.r = relief grayscale value (broadcast to rgb), detail.a = coverage.
                var gd = dp[i + 2] / 255f; // R channel = the grayscale relief value
                var a = dp[i + 3] / 255f;
                if (a <= 0f) continue;
                r = r + (Overlay(r, gd) - r) * a;
                g = g + (Overlay(g, gd) - g) * a;
                b = b + (Overlay(b, gd) - b) * a;
            }

            if (eye is not null)
            {
                var ep = eye.Pixels;
                var ea = ep[i + 3] / 255f;
                if (ea > 0f)
                {
                    var er = ep[i + 2] / 255f;
                    var eg = ep[i + 1] / 255f;
                    var eb = ep[i] / 255f;
                    r = r + (er - r) * ea;
                    g = g + (eg - g) * ea;
                    b = b + (eb - b) * ea;
                }
            }

            outPx[i] = ToByte(b);
            outPx[i + 1] = ToByte(g);
            outPx[i + 2] = ToByte(r);
            outPx[i + 3] = 255;
        }
        return new RgbaImage(w, h, outPx);
    }

    // 'overlay' blend, per channel, 0..1: base<0.5 -> 2*base*g ; else 1 - 2*(1-base)*(1-g).
    private static float Overlay(float baseChannel, float g) =>
        baseChannel < 0.5f
            ? 2f * baseChannel * g
            : 1f - (2f * (1f - baseChannel) * (1f - g));

    private static byte ToByte(float v)
    {
        if (v < 0f) v = 0f;
        if (v > 1f) v = 1f;
        return (byte)((v * 255f) + 0.5f);
    }

    // Resolve the FIRST resolvable relief texture for a detail option (TGIs tried in order).
    // (Mirrors CharacterDefinitionExporter.ResolveDetailReliefAsync — kept local so the catalog
    // exporter is self-contained.)
    private static async Task<byte[]?> ResolveDetailReliefAsync(
        IResourceCatalogService catalog, DetailLayerOption opt, string modsRoot, CancellationToken ct)
    {
        var pkg = string.IsNullOrWhiteSpace(opt.Package) ? null : Path.Combine(modsRoot, opt.Package);
        foreach (var tgi in opt.TextureTgis)
        {
            if (!TryParseTgi(tgi, out var type, out var group, out var inst)) continue;
            var key = new ResourceKeyRecord(type, group, inst, TypeNameForType(type));
            var path = pkg ?? string.Empty;
            try
            {
                var png = await catalog.GetTexturePngAsync(path, key, ct).ConfigureAwait(false);
                if (png is { Length: > 0 }) return png;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[skinCatalog] detail {tgi}: GetTexturePngAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            }
        }
        return null;
    }

    // ---- image helpers (tight BGRA buffers, normalised to the shared atlas resolution) ------------
    internal sealed class RgbaImage
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; } // tight BGRA, width*height*4
        public RgbaImage(int width, int height, byte[] pixels)
        {
            Width = width; Height = height; Pixels = pixels;
        }
    }

    // Decode a PNG to a tight BGRA buffer at the shared atlas resolution. EA body atlases are already
    // 1024x2048; HD 2048x4096 bases are downscaled mod-2. Any other size is high-quality bicubic'd to
    // the shared resolution so every layer aligns to the same UVs.
    private static RgbaImage? DecodeToAtlas(byte[] pngBytes)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes, writable: false);
            using var source = new Bitmap(ms);
            using var argb = new Bitmap(AtlasWidth, AtlasHeight, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(argb))
            {
                g.CompositingMode = CompositingMode.SourceCopy; // copy source alpha verbatim (straight)
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                var needScale = source.Width != AtlasWidth || source.Height != AtlasHeight;
                g.InterpolationMode = needScale ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, AtlasWidth, AtlasHeight), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
            }
            return new RgbaImage(AtlasWidth, AtlasHeight, ReadTightBgra(argb));
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ReadTightBgra(Bitmap argb)
    {
        var rect = new Rectangle(0, 0, argb.Width, argb.Height);
        var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var rowBytes = argb.Width * 4;
            var tight = new byte[argb.Width * argb.Height * 4];
            if (stride == rowBytes)
            {
                Marshal.Copy(data.Scan0, tight, 0, tight.Length);
            }
            else
            {
                for (var y = 0; y < argb.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), tight, y * rowBytes, rowBytes);
                }
            }
            return tight;
        }
        finally
        {
            argb.UnlockBits(data);
        }
    }

    private static void WritePng(RgbaImage img, string path)
    {
        using var argb = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
        var rect = new Rectangle(0, 0, img.Width, img.Height);
        var data = argb.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var rowBytes = img.Width * 4;
            if (stride == rowBytes)
            {
                Marshal.Copy(img.Pixels, 0, data.Scan0, img.Pixels.Length);
            }
            else
            {
                for (var y = 0; y < img.Height; y++)
                {
                    Marshal.Copy(img.Pixels, y * rowBytes, IntPtr.Add(data.Scan0, y * stride), rowBytes);
                }
            }
        }
        finally
        {
            argb.UnlockBits(data);
        }
        argb.Save(path, ImageFormat.Png);
    }

    // Dilate the opaque color outward into transparent gutters (iterative 1px grow up to `radius`
    // passes), so the runtime shader's bilinear edge filtering does not pull empty/black across UV
    // seams. Operates in place on the BGRA buffer; only fully-transparent texels are filled.
    private static void DilateOpaqueInPlace(RgbaImage img, int radius)
    {
        var w = img.Width;
        var h = img.Height;
        var px = img.Pixels;
        for (var pass = 0; pass < radius; pass++)
        {
            var filledThisPass = 0;
            var snapshot = (byte[])px.Clone();
            for (var y = 0; y < h; y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var i = (y * w + x) * 4;
                    if (snapshot[i + 3] != 0) continue; // already opaque/partly opaque
                    // Average the nearest opaque 4-neighbours from the snapshot.
                    int sb = 0, sg = 0, sr = 0, n = 0;
                    void Take(int xx, int yy)
                    {
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) return;
                        var j = (yy * w + xx) * 4;
                        if (snapshot[j + 3] == 0) return;
                        sb += snapshot[j]; sg += snapshot[j + 1]; sr += snapshot[j + 2]; n++;
                    }
                    Take(x - 1, y); Take(x + 1, y); Take(x, y - 1); Take(x, y + 1);
                    if (n == 0) continue;
                    px[i] = (byte)(sb / n);
                    px[i + 1] = (byte)(sg / n);
                    px[i + 2] = (byte)(sr / n);
                    px[i + 3] = 255;
                    filledThisPass++;
                }
            }
            if (filledThisPass == 0) break;
        }
    }

    private static void ForceOpaque(RgbaImage img)
    {
        var px = img.Pixels;
        for (var i = 3; i < px.Length; i += 4) px[i] = 255;
    }

    // ---- verification/report helpers --------------------------------------------------------------
    private readonly record struct WarmInfo(double R, double G, double B, bool Warm);

    private static WarmInfo WarmStats(RgbaImage img)
    {
        var px = img.Pixels;
        double sr = 0, sg = 0, sb = 0; long n = 0;
        for (var i = 0; i < px.Length; i += 4 * 8) // coarse grid
        {
            if (px[i + 3] < 250) continue;
            sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; n++;
        }
        if (n == 0) return new WarmInfo(0, 0, 0, false);
        var mr = sr / n; var mg = sg / n; var mb = sb / n;
        return new WarmInfo(mr, mg, mb, mr > mg && mg > mb);
    }

    private static double CoverageStats(RgbaImage img)
    {
        var px = img.Pixels;
        long opaque = 0; long total = 0;
        for (var i = 3; i < px.Length; i += 4) { if (px[i] > 8) opaque++; total++; }
        return total == 0 ? 0 : (double)opaque / total;
    }

    private readonly record struct IrisInfo(double Coverage, string Box, bool Localized);

    private static IrisInfo IrisLocalization(RgbaImage img)
    {
        var w = img.Width;
        var h = img.Height;
        var px = img.Pixels;
        long opaque = 0;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = (y * w + x) * 4;
                if (px[i + 3] < 16) continue;
                opaque++;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
            }
        }
        var cov = (double)opaque / (w * (long)h);
        var box = maxX < 0 ? "(empty)" : $"[{minX},{minY}]-[{maxX},{maxY}]";
        // Localized iff opaque pixels cover a small fraction of the atlas (an iris island, not a body plate).
        return new IrisInfo(cov, box, cov > 0 && cov < 0.25);
    }

    // ---- small parse helpers (shared with CharacterDefinitionExporter's conventions) --------------
    private static bool TryParseTgi(string? tgi, out uint type, out uint group, out ulong instance)
    {
        type = 0; group = 0; instance = 0;
        if (string.IsNullOrWhiteSpace(tgi)) return false;
        var parts = tgi.Split(':');
        if (parts.Length != 3) return false;
        var ci = CultureInfo.InvariantCulture;
        var ns = NumberStyles.HexNumber;
        return uint.TryParse(parts[0], ns, ci, out type)
            && uint.TryParse(parts[1], ns, ci, out group)
            && ulong.TryParse(parts[2], ns, ci, out instance);
    }

    private static string TypeNameForType(uint type) => type switch
    {
        0x2BC04EDFu => "LRLEImage",
        0x3453CF95u => "RLE2Image",
        _ => "Texture",
    };

    private static string Sanitize(string id) =>
        new string(id.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
}
