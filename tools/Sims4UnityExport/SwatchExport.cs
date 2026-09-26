// SwatchExport — emits a swatches.json manifest (and per-swatch textures) next to each
// exported asset. Two swatch models are supported, mirroring the App's viewport behaviour:
//
//   1) BUILD/BUY (MTST variants): the CanonicalScene already carries every variant on the
//      primary CanonicalMaterial. Swatch 0 = material.Textures (the default); each entry of
//      material.Variants becomes an additional swatch, written to Swatches/<index>/<slot>.png.
//
//   2) CAS recolor (SwatchColors + color_shift_mask): if the CAS material has a
//      color_shift_mask texture slot AND the CASP carries >1 SwatchColors, each swatch is
//      BAKED — final.rgb = base.rgb * ((1-maskFactor) + maskFactor*tint.rgb), where
//      maskFactor = max(mask.R,G,B,A) * tint.A — reimplemented here with System.Drawing
//      (this is the headless port of ComposeSwatchMaskedPng in SceneViewportRenderer.cs).
//      The baked diffuse is written to Swatches/<index>/diffuse.png; normal/specular/shadow
//      are shared by referencing the existing files under Textures/.
//
// Every diffuse PNG written here is run through the SAME transparent-pixel blackening as the
// main export (BlackenTransparentPixelsInFile) so Unity's opaque material never bleeds the
// acid-green that EA's RLE2 textures leave in fully-transparent blocks.
//
// swatches.json lives at <UnityAssetsDir>/<slug>/swatches.json. Paths inside it are RELATIVE
// to the asset folder and use forward slashes.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

// ---------------------------------------------------------------------------
// swatches.json data contract (serialized verbatim per the brief)
// ---------------------------------------------------------------------------
internal sealed class SwatchesManifest
{
    [JsonPropertyName("asset")] public string Asset { get; set; } = string.Empty;
    [JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty; // "buildbuy" | "cas"
    [JsonPropertyName("defaultIndex")] public int DefaultIndex { get; set; }
    [JsonPropertyName("swatches")] public List<SwatchEntry> Swatches { get; set; } = new();
}

internal sealed class SwatchEntry
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("colorHex")] public string? ColorHex { get; set; }
    [JsonPropertyName("stateHash")] public string? StateHash { get; set; }
    [JsonPropertyName("isDefault")] public bool IsDefault { get; set; }
    [JsonPropertyName("diffuse")] public string Diffuse { get; set; } = string.Empty;
    [JsonPropertyName("normal")] public string? Normal { get; set; }
    [JsonPropertyName("specular")] public string? Specular { get; set; }
    [JsonPropertyName("shadow")] public string? Shadow { get; set; }
}

internal static class SwatchExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    // -----------------------------------------------------------------------
    // BUILD/BUY: swatch 0 = material.Textures; each material.Variants entry is an extra swatch.
    // -----------------------------------------------------------------------
    public static void WriteBuildBuySwatches(string unityAssetsDir, string slug, CanonicalScene scene)
    {
        var assetFolder = Path.Combine(unityAssetsDir, slug);
        Directory.CreateDirectory(assetFolder);

        // Primary material = the one carrying the most MTST variants (the App groups variants
        // per material; in practice a single textured material owns them). Fall back to the
        // first material with any textures, else the first material.
        var primary = scene.Materials
                          .Where(m => m.Variants is { Count: > 0 })
                          .OrderByDescending(m => m.Variants!.Count)
                          .FirstOrDefault()
                      ?? scene.Materials.FirstOrDefault(m => m.Textures.Count > 0)
                      ?? scene.Materials.FirstOrDefault();

        var manifest = new SwatchesManifest
        {
            Asset = slug,
            Kind = "buildbuy",
            DefaultIndex = 0
        };

        if (primary is null)
        {
            Console.WriteLine("[swatches] No materials on the scene; writing a 0-swatch manifest.");
            WriteManifest(assetFolder, manifest);
            return;
        }

        // Collect candidate swatches: index 0 = the default (material.Textures), then one per
        // MTST variant. We DEDUPE by resolved texture content below: the source data routinely
        // has several states resolving to the SAME textures (the synthetic "Default", the variant
        // flagged IsDefault, and the 0x0 state all share the default set), which would otherwise
        // show up as no-op / duplicate dropdown entries.
        var candidates = new List<(string Label, string? StateHash, bool IsDefault, IReadOnlyList<CanonicalTexture> Textures)>
        {
            ("Default", null, true, primary.Textures)
        };

        foreach (var variant in primary.Variants ?? Array.Empty<CanonicalMaterialVariant>())
        {
            var stateHash = $"0x{variant.StateNameHash:X8}";
            var label = variant.IsDefault
                ? "Default"
                : (!string.IsNullOrWhiteSpace(variant.VariantName) ? variant.VariantName! : $"State {stateHash}");
            candidates.Add((label, stateHash, variant.IsDefault, variant.Textures));
        }

        // Keep only swatches whose resolved texture set is unique (first occurrence wins, so the
        // synthetic "Default" is preferred over an identical variant). Uniquify duplicate labels.
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var usedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var distinct = 0;
        var skipped = 0;
        foreach (var candidate in candidates)
        {
            if (!seenKeys.Add(ComputeSwatchKey(candidate.Textures)))
            {
                skipped++;
                continue; // identical appearance to an earlier swatch — drop it
            }

            var label = candidate.Label;
            if (!usedLabels.Add(label))
            {
                label = $"{label} ({distinct})";
                usedLabels.Add(label);
            }

            manifest.Swatches.Add(WriteSwatchFromTextures(
                assetFolder, distinct, candidate.Textures,
                label, colorHex: null, stateHash: candidate.StateHash, isDefault: candidate.IsDefault));
            distinct++;
        }

        // Default index = the first swatch flagged default (else 0).
        var defaultIdx = manifest.Swatches.FindIndex(s => s.IsDefault);
        manifest.DefaultIndex = defaultIdx >= 0 ? defaultIdx : 0;

        WriteManifest(assetFolder, manifest);
        Console.WriteLine(
            $"[swatches] Build/Buy: wrote {manifest.Swatches.Count} unique swatch(es) " +
            $"({skipped} duplicate(s) collapsed) to swatches.json.");
    }

    // Write one Build/Buy swatch's texture set into Swatches/<index>/<slot>.png and return its entry.
    private static SwatchEntry WriteSwatchFromTextures(
        string assetFolder, int index, IReadOnlyList<CanonicalTexture> textures,
        string label, string? colorHex, string? stateHash, bool isDefault)
    {
        var swatchDir = Path.Combine(assetFolder, "Swatches", index.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(swatchDir);

        string? diffuseRel = null;
        string? normalRel = null;
        string? specularRel = null;
        string? shadowRel = null;

        foreach (var slot in new[] { "diffuse", "normal", "specular", "shadow" })
        {
            var tex = PickBySlot(textures, slot);
            if (tex is null)
            {
                continue;
            }

            var fileName = $"{slot}.png";
            var fullPath = Path.Combine(swatchDir, fileName);
            File.WriteAllBytes(fullPath, tex.PngBytes);

            var rel = $"Swatches/{index}/{fileName}";
            switch (slot)
            {
                case "diffuse":
                    // Blacken fully-transparent RGB so the opaque Unity material never bleeds green.
                    BlackenTransparentPixelsInFile(fullPath);
                    diffuseRel = rel;
                    break;
                case "normal": normalRel = rel; break;
                case "specular": specularRel = rel; break;
                case "shadow": shadowRel = rel; break;
            }
        }

        // A swatch must always have a diffuse path; if the variant carried none, fall back to
        // the first texture so the manifest stays valid.
        if (diffuseRel is null && textures.Count > 0)
        {
            var fileName = "diffuse.png";
            var fullPath = Path.Combine(swatchDir, fileName);
            File.WriteAllBytes(fullPath, textures[0].PngBytes);
            BlackenTransparentPixelsInFile(fullPath);
            diffuseRel = $"Swatches/{index}/{fileName}";
        }

        return new SwatchEntry
        {
            Index = index,
            Label = label,
            ColorHex = colorHex,
            StateHash = stateHash,
            IsDefault = isDefault,
            Diffuse = diffuseRel ?? string.Empty,
            Normal = normalRel,
            Specular = specularRel,
            Shadow = shadowRel
        };
    }

    // Content hash of a swatch's resolved texture set (diffuse/normal/specular/shadow), used to
    // collapse swatches that resolve to identical textures into one dropdown entry.
    private static string ComputeSwatchKey(IReadOnlyList<CanonicalTexture> textures)
    {
        using var md5 = MD5.Create();
        using var ms = new MemoryStream();
        foreach (var slot in new[] { "diffuse", "normal", "specular", "shadow" })
        {
            var tex = PickBySlot(textures, slot);
            ms.WriteByte((byte)(tex is null ? 0 : 1));
            if (tex?.PngBytes is { Length: > 0 } bytes)
            {
                ms.Write(bytes, 0, bytes.Length);
            }
        }

        ms.Position = 0;
        return Convert.ToHexString(md5.ComputeHash(ms));
    }

    // -----------------------------------------------------------------------
    // CAS: bake each SwatchColors tint over the base diffuse via the color_shift_mask.
    //
    // diffuseTexturesRel maps slot -> existing "Textures/<file>.png" relative path (already
    // written + blackened by the main export) so normal/specular/shadow can be SHARED. If the
    // part has no mask or <2 swatch colours, a single-swatch manifest is written pointing at
    // the existing Textures/diffuse*.png.
    // -----------------------------------------------------------------------
    public static void WriteCasSwatches(
        string unityAssetsDir,
        string slug,
        CanonicalScene scene,
        IReadOnlyList<uint> swatchColors)
    {
        var assetFolder = Path.Combine(unityAssetsDir, slug);
        Directory.CreateDirectory(assetFolder);

        // The CAS material the App treats as recolorable is the ApproximateCas one; fall back
        // to the first material with a diffuse + mask.
        var material = scene.Materials.FirstOrDefault(m =>
                           m.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
                           PickBySlot(m.Textures, "diffuse") is not null)
                       ?? scene.Materials.FirstOrDefault(m => PickBySlot(m.Textures, "diffuse") is not null)
                       ?? scene.Materials.FirstOrDefault();

        var baseDiffuse = material is null ? null : PickBySlot(material.Textures, "diffuse");
        var mask = material is null ? null : PickColorShiftMask(material.Textures);
        var normal = material is null ? null : PickBySlot(material.Textures, "normal");
        var specular = material is null ? null : PickBySlot(material.Textures, "specular");
        var shadow = material is null ? null : PickBySlot(material.Textures, "shadow");

        // Shared (non-baked) maps live under the existing Textures/ folder by their FileName.
        string? normalRel = normal is null ? null : $"Textures/{normal.FileName}";
        string? specularRel = specular is null ? null : $"Textures/{specular.FileName}";
        string? shadowRel = shadow is null ? null : $"Textures/{shadow.FileName}";

        var manifest = new SwatchesManifest
        {
            Asset = slug,
            Kind = "cas",
            DefaultIndex = 0
        };

        var canBake = baseDiffuse is not null && mask is not null && swatchColors.Count > 1;

        if (!canBake)
        {
            // Single-swatch CAS manifest pointing at the existing Textures/diffuse*.png.
            string diffuseRel;
            if (baseDiffuse is not null)
            {
                diffuseRel = $"Textures/{baseDiffuse.FileName}";
            }
            else
            {
                // Last resort: locate any diffuse*.png already written under Textures/.
                diffuseRel = FindExistingDiffuseRel(assetFolder) ?? "Textures/diffuse.png";
            }

            var single = new SwatchEntry
            {
                Index = 0,
                Label = swatchColors.Count == 1 ? FormatSwatchLabel(swatchColors[0]) : "Default",
                ColorHex = swatchColors.Count == 1 ? FormatSwatchColorHex(swatchColors[0]) : null,
                StateHash = null,
                IsDefault = true,
                Diffuse = diffuseRel,
                Normal = normalRel,
                Specular = specularRel,
                Shadow = shadowRel
            };
            manifest.Swatches.Add(single);
            WriteManifest(assetFolder, manifest);
            Console.WriteLine(
                $"[swatches] CAS: no recolor (mask={(mask is not null)}, swatchColors={swatchColors.Count}); wrote single-swatch swatches.json.");
            return;
        }

        // Bake one diffuse per swatch colour.
        for (var i = 0; i < swatchColors.Count; i++)
        {
            var argb = swatchColors[i];
            var (tr, tg, tb, ta) = ToTintRgba(argb);

            var baked = ComposeSwatchMaskedPng(baseDiffuse!.PngBytes, mask!.PngBytes, tr, tg, tb, ta)
                        ?? baseDiffuse.PngBytes; // fall back to the base diffuse on a compose failure

            var swatchDir = Path.Combine(assetFolder, "Swatches", i.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(swatchDir);
            var fullPath = Path.Combine(swatchDir, "diffuse.png");
            File.WriteAllBytes(fullPath, baked);
            BlackenTransparentPixelsInFile(fullPath);

            manifest.Swatches.Add(new SwatchEntry
            {
                Index = i,
                Label = FormatSwatchLabel(argb),
                ColorHex = FormatSwatchColorHex(argb),
                StateHash = null,
                IsDefault = i == 0,
                Diffuse = $"Swatches/{i}/diffuse.png",
                Normal = normalRel,
                Specular = specularRel,
                Shadow = shadowRel
            });
        }

        WriteManifest(assetFolder, manifest);
        Console.WriteLine($"[swatches] CAS: baked {manifest.Swatches.Count} recolor swatch(es) to swatches.json.");
    }

    // -----------------------------------------------------------------------
    // Slot resolution
    // -----------------------------------------------------------------------

    // Resolve a CanonicalTexture for a logical slot, by semantic first then by slot-name match.
    private static CanonicalTexture? PickBySlot(IReadOnlyList<CanonicalTexture> textures, string slot)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        switch (slot)
        {
            case "diffuse":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.BaseColor)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "diffuse", "albedo", "basecolor", "base color"));
            case "normal":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.Normal)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "normal", "bump"));
            case "specular":
                return textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.Specular)
                       ?? textures.FirstOrDefault(t => SlotContains(t, "specular", "spec", "gloss", "rough", "smooth"));
            case "shadow":
                // CanonicalTextureSemantic has no Shadow/AO member, so resolve by slot name only.
                return textures.FirstOrDefault(t => SlotContains(t, "shadow", "ao", "occlusion", "ambient"));
            default:
                return null;
        }
    }

    private static CanonicalTexture? PickColorShiftMask(IReadOnlyList<CanonicalTexture> textures) =>
        textures.FirstOrDefault(t => string.Equals(t.Slot, "color_shift_mask", StringComparison.OrdinalIgnoreCase))
        ?? textures.FirstOrDefault(t => SlotContains(t, "color_shift", "colorshift"));

    private static bool SlotContains(CanonicalTexture t, params string[] needles)
    {
        if (string.IsNullOrEmpty(t.Slot))
        {
            return false;
        }

        foreach (var needle in needles)
        {
            if (t.Slot.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string? FindExistingDiffuseRel(string assetFolder)
    {
        var texturesFolder = Path.Combine(assetFolder, "Textures");
        if (!Directory.Exists(texturesFolder))
        {
            return null;
        }

        var file = Directory.EnumerateFiles(texturesFolder, "*.png")
            .FirstOrDefault(p => Path.GetFileName(p).StartsWith("diffuse", StringComparison.OrdinalIgnoreCase));
        return file is null ? null : $"Textures/{Path.GetFileName(file)}";
    }

    // -----------------------------------------------------------------------
    // Swatch tint helpers (CASP SwatchColors are packed AARRGGBB uint, per TryParseSwatchTint).
    // -----------------------------------------------------------------------
    private static (float R, float G, float B, float A) ToTintRgba(uint argb)
    {
        var a = ((argb >> 24) & 0xFF) / 255f;
        var r = ((argb >> 16) & 0xFF) / 255f;
        var g = ((argb >> 8) & 0xFF) / 255f;
        var b = (argb & 0xFF) / 255f;
        return (r, g, b, a);
    }

    private static string FormatSwatchColorHex(uint argb)
    {
        var r = (argb >> 16) & 0xFF;
        var g = (argb >> 8) & 0xFF;
        var b = argb & 0xFF;
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static string FormatSwatchLabel(uint argb) => $"#{argb:X8}";

    // -----------------------------------------------------------------------
    // Masked-swatch compose (headless port of SceneViewportRenderer.ComposeSwatchMaskedPng).
    //
    // final.rgb = base.rgb * ((1 - maskFactor) + maskFactor*tint.rgb)
    // maskFactor = clamp( max(mask.R,G,B,A) * tint.A , 0..1 )
    // base alpha is preserved. The mask is scaled to the diffuse's dimensions if they differ.
    // Returns null on failure (caller falls back to the base diffuse).
    // -----------------------------------------------------------------------
    // Internal so the headless self-test can exercise the exact bake math the CAS swatch path
    // uses. (The CAS recolor path is gated on a CASP exposing a resolvable color_shift_mask AND
    // >1 SwatchColors; EA's shipped content in this install never co-occurs both, so the live
    // CAS export legitimately emits single-swatch manifests — see ComposeSelfTest.)
    internal static byte[]? ComposeSwatchMaskedPng(
        byte[] diffusePngBytes, byte[] maskPngBytes,
        float tintR, float tintG, float tintB, float tintA)
    {
        try
        {
            using var diffuse = LoadArgb(diffusePngBytes);
            using var maskSource = LoadArgb(maskPngBytes);

            // Scale mask to match the diffuse if needed.
            System.Drawing.Bitmap mask;
            var scaledMask = false;
            if (maskSource.Width != diffuse.Width || maskSource.Height != diffuse.Height)
            {
                mask = new System.Drawing.Bitmap(diffuse.Width, diffuse.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = System.Drawing.Graphics.FromImage(mask);
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                g.DrawImage(maskSource, 0, 0, diffuse.Width, diffuse.Height);
                scaledMask = true;
            }
            else
            {
                mask = maskSource;
            }

            try
            {
                var rect = new System.Drawing.Rectangle(0, 0, diffuse.Width, diffuse.Height);
                var diffData = diffuse.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var maskData = mask.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var stride = diffData.Stride;
                    var rowBytes = diffuse.Width * 4;
                    var diffBuf = new byte[stride * diffuse.Height];
                    var maskBuf = new byte[maskData.Stride * mask.Height];
                    System.Runtime.InteropServices.Marshal.Copy(diffData.Scan0, diffBuf, 0, diffBuf.Length);
                    System.Runtime.InteropServices.Marshal.Copy(maskData.Scan0, maskBuf, 0, maskBuf.Length);

                    var tr = Math.Clamp(tintR, 0f, 1f);
                    var tg = Math.Clamp(tintG, 0f, 1f);
                    var tb = Math.Clamp(tintB, 0f, 1f);
                    var ta = Math.Clamp(tintA, 0f, 1f);

                    // Format32bppArgb in memory (little-endian) is B, G, R, A per pixel.
                    for (var y = 0; y < diffuse.Height; y++)
                    {
                        var diffRow = y * stride;
                        var maskRow = y * maskData.Stride;
                        for (var x = 0; x < rowBytes; x += 4)
                        {
                            var di = diffRow + x;
                            var mi = maskRow + x;

                            var baseB = diffBuf[di + 0] / 255f;
                            var baseG = diffBuf[di + 1] / 255f;
                            var baseR = diffBuf[di + 2] / 255f;
                            // baseA preserved (diffBuf[di + 3]).

                            var maskB = maskBuf[mi + 0] / 255f;
                            var maskG = maskBuf[mi + 1] / 255f;
                            var maskR = maskBuf[mi + 2] / 255f;
                            var maskA = maskBuf[mi + 3] / 255f;
                            var maskFactor = Math.Clamp(Math.Max(maskA, Math.Max(maskR, Math.Max(maskG, maskB))) * ta, 0f, 1f);

                            var blendedR = baseR * ((1f - maskFactor) + (maskFactor * tr));
                            var blendedG = baseG * ((1f - maskFactor) + (maskFactor * tg));
                            var blendedB = baseB * ((1f - maskFactor) + (maskFactor * tb));

                            diffBuf[di + 0] = (byte)Math.Clamp((int)Math.Round(blendedB * 255f), 0, 255);
                            diffBuf[di + 1] = (byte)Math.Clamp((int)Math.Round(blendedG * 255f), 0, 255);
                            diffBuf[di + 2] = (byte)Math.Clamp((int)Math.Round(blendedR * 255f), 0, 255);
                            // diffBuf[di + 3] (alpha) untouched.
                        }
                    }

                    System.Runtime.InteropServices.Marshal.Copy(diffBuf, 0, diffData.Scan0, diffBuf.Length);
                }
                finally
                {
                    diffuse.UnlockBits(diffData);
                    mask.UnlockBits(maskData);
                }

                using var ms = new MemoryStream();
                diffuse.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            finally
            {
                if (scaledMask)
                {
                    mask.Dispose();
                }
            }
        }
        catch
        {
            return null;
        }
    }

    // Decode PNG bytes into a fresh, file-detached 32bpp ARGB bitmap.
    private static System.Drawing.Bitmap LoadArgb(byte[] pngBytes)
    {
        using var ms = new MemoryStream(pngBytes, writable: false);
        using var source = new System.Drawing.Bitmap(ms);
        var copy = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(copy);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }

    // -----------------------------------------------------------------------
    // Transparent-pixel blackening (shared with the main export's CleanupDiffuseTextures).
    // Sets RGB=(0,0,0) for every pixel whose alpha==0 (alpha preserved), re-saving the PNG.
    // Returns the count of pixels modified.
    // -----------------------------------------------------------------------
    public static int BlackenTransparentPixelsInFile(string pngPath)
    {
        using var bitmap = LoadArgbFromFile(pngPath);

        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        var blackened = 0;
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
                    var i = rowStart + x;
                    if (buffer[i + 3] == 0)
                    {
                        buffer[i + 0] = 0;
                        buffer[i + 1] = 0;
                        buffer[i + 2] = 0;
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

    // Load a PNG file into a detached 32bpp ARGB bitmap, releasing the on-disk file handle so
    // the bitmap can later be saved back over the same path.
    private static System.Drawing.Bitmap LoadArgbFromFile(string pngPath)
    {
        using var source = new System.Drawing.Bitmap(pngPath);
        var copy = new System.Drawing.Bitmap(source.Width, source.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = System.Drawing.Graphics.FromImage(copy);
        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
        g.DrawImage(source, 0, 0, source.Width, source.Height);
        return copy;
    }

    // -----------------------------------------------------------------------
    // Bake self-test: prove the CAS recolor math produces DISTINCT colored diffuses for
    // distinct swatch tints. Composes the given base diffuse with a synthetic full-white mask
    // (maskFactor = tint.A everywhere) under each tint, writes the results, and reports each
    // output's mean RGB so the caller can confirm they differ. This exercises the exact
    // ComposeSwatchMaskedPng path the live CAS export uses; it exists because EA's shipped
    // content never co-locates a resolvable color_shift_mask with >1 SwatchColors.
    // -----------------------------------------------------------------------
    public static void ComposeSelfTest(string baseDiffusePath, string outputDir, IReadOnlyList<uint> tints)
    {
        Directory.CreateDirectory(outputDir);
        var baseBytes = File.ReadAllBytes(baseDiffusePath);

        // Build a fully-white opaque mask matching the base dimensions so maskFactor = tint.A.
        byte[] whiteMaskBytes;
        using (var probe = LoadArgb(baseBytes))
        using (var white = new System.Drawing.Bitmap(probe.Width, probe.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
        using (var g = System.Drawing.Graphics.FromImage(white))
        {
            g.Clear(System.Drawing.Color.FromArgb(255, 255, 255, 255));
            using var ms = new MemoryStream();
            white.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            whiteMaskBytes = ms.ToArray();
        }

        for (var i = 0; i < tints.Count; i++)
        {
            var (r, g, b, a) = ToTintRgba(tints[i]);
            var baked = ComposeSwatchMaskedPng(baseBytes, whiteMaskBytes, r, g, b, a);
            if (baked is null)
            {
                Console.WriteLine($"  [selftest] tint {FormatSwatchColorHex(tints[i])}: compose FAILED");
                continue;
            }

            var outPath = Path.Combine(outputDir, $"selftest_{i}_{FormatSwatchColorHex(tints[i]).TrimStart('#')}.png");
            File.WriteAllBytes(outPath, baked);
            var (mr, mg, mb) = MeanRgb(baked);
            Console.WriteLine($"  [selftest] tint {FormatSwatchColorHex(tints[i])} -> meanRGB=({mr},{mg},{mb})  {Path.GetFileName(outPath)}");
        }
    }

    private static (int R, int G, int B) MeanRgb(byte[] pngBytes)
    {
        using var bmp = LoadArgb(pngBytes);
        var rect = new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height);
        var data = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var rowBytes = bmp.Width * 4;
            var buf = new byte[stride * bmp.Height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, buf, 0, buf.Length);
            long sr = 0, sg = 0, sb = 0, n = 0;
            for (var y = 0; y < bmp.Height; y++)
            {
                var row = y * stride;
                for (var x = 0; x < rowBytes; x += 4)
                {
                    sb += buf[row + x + 0];
                    sg += buf[row + x + 1];
                    sr += buf[row + x + 2];
                    n++;
                }
            }
            return n == 0 ? (0, 0, 0) : ((int)(sr / n), (int)(sg / n), (int)(sb / n));
        }
        finally
        {
            bmp.UnlockBits(data);
        }
    }

    private static void WriteManifest(string assetFolder, SwatchesManifest manifest)
    {
        var path = Path.Combine(assetFolder, "swatches.json");
        File.WriteAllText(path, JsonSerializer.Serialize(manifest, JsonOptions));
        Console.WriteLine($"[swatches] Wrote {path}");
    }
}
