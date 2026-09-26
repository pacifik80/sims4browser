// DumpSkinLayers — TEMPORARY DIAGNOSTIC (investigation only; does not change any export logic).
//
// Resolves the skin-texture LAYER set for one default Sim EXACTLY as exportsim does (via
// ISyntheticSimService.ResolveSkintoneRenderAsync -> the App's ResolveHumanSkintoneAsync) and
// dumps every input layer that the App's SimSkinAtlasComposer consumes as its own PNG at NATIVE
// resolution, plus a per-layer report of (width x height, has-alpha, fully-opaque?).
//
// It ALSO assembles the full Sim scene (the SimConstructorWindow path) and replicates the App's
// SimConstructorViewModel.TryExtractFullBodyDiffuse — extracting the "Head shell" material's
// region_map / full-body-diffuse texture (the >=200KB Model-B "base WITH underwear" candidate)
// if any such texture survives override resolution. This is the texture the App feeds into
// BuildAtlasFromPreRenderedBaseAsync when present.
//
// Nothing here writes into src/; all output goes to <UnityAssetsDir>/_skinlayers/.

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview.SimRender;

namespace Sims4UnityExport;

internal static class SkinLayerDumper
{
    public static async Task<bool> RunAsync(
        string unityAssetsDir,
        ISyntheticSimService syntheticSimService,
        ISimAssetGraphRenderer simRenderer,
        string ageLabel,
        string genderLabel,
        ulong skintoneInstance,
        CancellationToken ct)
    {
        var outDir = Path.Combine(unityAssetsDir, "_skinlayers");
        Directory.CreateDirectory(outDir);
        Console.WriteLine($"[dumpskinlayers] age='{ageLabel}' gender='{genderLabel}' tone=0x{skintoneInstance:X16}");
        Console.WriteLine($"[dumpskinlayers] output dir: {outDir}");

        var report = new List<string>();
        void Dump(string fileName, byte[]? png, string role, string blend, string alphaSemantics)
        {
            var path = Path.Combine(outDir, fileName);
            if (png is not { Length: > 0 })
            {
                Console.WriteLine($"[dumpskinlayers]   {fileName}: (none)  role={role}");
                report.Add($"{fileName,-34} | MISSING | role={role}");
                return;
            }
            File.WriteAllBytes(path, png);
            var (w, h, hasAlpha, fullyOpaque) = ProbePng(png);
            Console.WriteLine(
                $"[dumpskinlayers]   {fileName}: {w}x{h}  bytes={png.Length:N0}  hasAlpha={hasAlpha} fullyOpaque={fullyOpaque}  role={role}");
            report.Add(
                $"{fileName,-34} | {w}x{h,-9} | bytes={png.Length,9:N0} | alpha={(hasAlpha ? (fullyOpaque ? "opaque" : "MEANINGFUL") : "none"),-10} | role={role} | blend={blend} | {alphaSemantics}");
        }

        // 1) Resolve the skintone render summary — the SAME call exportsim uses. This gives us every
        //    BuildAsync input layer directly as byte[] (Model-A modern tone base + detail rows + face).
        SimSkintoneRenderSummary? skin;
        try
        {
            skin = await syntheticSimService.ResolveSkintoneRenderAsync(ageLabel, genderLabel, skintoneInstance, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dumpskinlayers] ResolveSkintoneRenderAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            return false;
        }
        if (skin is null)
        {
            Console.Error.WriteLine("[dumpskinlayers] No skintone render summary resolved.");
            return false;
        }

        Console.WriteLine();
        Console.WriteLine($"[dumpskinlayers] skintone resource TGI : {skin.SkintoneResourceTgi ?? "(none)"}  pkg={Path.GetFileName(skin.SkintonePackagePath ?? "")}");
        Console.WriteLine($"[dumpskinlayers] base texture TGI (Model A): {skin.BaseTextureResourceTgi ?? "(none)"}  pkg={Path.GetFileName(skin.BaseTexturePackagePath ?? "")}");
        Console.WriteLine($"[dumpskinlayers] hue=0x{skin.SkintoneHue:X4} sat=0x{skin.SkintoneSaturation:X4} overlayOpacity={skin.OverlayOpacity} shift={skin.SkintoneShift?.ToString("0.###") ?? "(none)"}");
        Console.WriteLine();

        // 2) Dump the Model-A modern per-tone base (skinSets[0].TextureInstance). This is BuildAsync's
        //    baseSkinPng and what the headless exporter currently uses as the base.
        Dump("base_modelA_tone.png", skin.BaseTexturePngBytes,
            role: "BASE (Model A: per-tone soft-shaded LRLE; full-body skin color — verify underwear)",
            blend: "substrate (C in albedo eq)",
            alphaSemantics: "alpha = UV-island coverage mask (transparent = off-mesh gutter)");

        // 3) Grayscale skin-detail rows (the SkinBlender hardcoded per age x gender detail composite D).
        Dump("detail_neutral.png", skin.DetailNeutralPngBytes,
            role: "SKIN-DETAIL neutral (grayscale relief; overlay() target D)",
            blend: "overlay() x2, identity ramp",
            alphaSemantics: "alpha = coverage; TRANSPARENT CUT-OUTS at brow/nostril/lash (hole-filled before eq)");
        Dump("detail_overlay.png", skin.DetailOverlayPngBytes,
            role: "SKIN-DETAIL overlay row (grayscale; alpha-over onto detail canvas)",
            blend: "straight-alpha over detail canvas",
            alphaSemantics: "alpha = coverage; meaningful cut-outs (composited alpha-aware then filled)");

        // 4) Per-physique detail + overlay rows [heavy, fit, lean, bony].
        string[] phys = { "heavy", "fit", "lean", "bony" };
        for (var i = 0; i < 4; i++)
        {
            var d = skin.PhysiqueDetailPngBytes is { } pd && i < pd.Count ? pd[i] : null;
            var o = skin.PhysiqueOverlayPngBytes is { } po && i < po.Count ? po[i] : null;
            Dump($"physique_detail_{i}_{phys[i]}.png", d,
                role: $"SKIN-DETAIL physique '{phys[i]}' (grayscale; blended by weight)",
                blend: "straight-alpha over detail canvas @ weight",
                alphaSemantics: "alpha = coverage (hole-filled with neutral canvas)");
            Dump($"physique_overlay_{i}_{phys[i]}.png", o,
                role: $"SKIN-DETAIL physique '{phys[i]}' overlay (grayscale)",
                blend: "straight-alpha over detail canvas @ weight",
                alphaSemantics: "alpha = coverage");
        }

        // 5) Tone face overlay (strict age/gender flag match; often null for adults).
        Dump("face_overlay_tone.png", skin.FaceOverlayPngBytes,
            role: "FACE-OVERLAY (tone accents: lash marks / lip / cheek)",
            blend: "straight-alpha source-over (final)",
            alphaSemantics: "alpha = MEANINGFUL (small opaque accent patches; rest transparent = leave skin)");

        // 6) Face CAS overlays (EyeColor / Brows / makeup) resolved from the Sim's equipped CAS parts.
        if (skin.FaceCasOverlayPngBytes is { Count: > 0 } casList)
        {
            for (var i = 0; i < casList.Count; i++)
            {
                Dump($"face_cas_overlay_{i}.png", casList[i],
                    role: $"FACE-CAS overlay #{i} (EyeColor/Brows/makeup CAS part diffuse)",
                    blend: "straight-alpha source-over (final)",
                    alphaSemantics: "alpha = MEANINGFUL (opaque iris/brow patch on a full atlas canvas; rest transparent)");
            }
        }
        else
        {
            Console.WriteLine("[dumpskinlayers]   face_cas_overlay_*: (none resolved for this Sim)");
            report.Add("face_cas_overlay_*               | NONE    | role=FACE-CAS (no equipped EyeColor/Brow parts resolved)");
        }

        // 7) Build the full Sim scene and replicate TryExtractFullBodyDiffuse to capture the Model-B
        //    head-shell full-body diffuse (the >=200KB "base WITH underwear" candidate), if it survives.
        Console.WriteLine();
        Console.WriteLine("[dumpskinlayers] Assembling full Sim scene to probe the head-shell full-body diffuse (Model-B candidate)...");
        try
        {
            var seed = syntheticSimService.CreateHumanSeed(ageLabel, genderLabel, skintoneInstance);
            var graph = await syntheticSimService.BuildHumanAssetGraphAsync(seed, ct).ConfigureAwait(false);
            if (graph.SimGraph is null)
            {
                Console.Error.WriteLine("[dumpskinlayers] No SimGraph; cannot probe head-shell diffuse.");
            }
            else
            {
                var renderResult = await simRenderer.BuildSimSceneAsync(graph, ct).ConfigureAwait(false);
                var scene = renderResult.Scene;
                if (scene is null)
                {
                    Console.Error.WriteLine("[dumpskinlayers] Render produced no Scene; cannot probe head-shell diffuse.");
                }
                else
                {
                    Console.WriteLine($"[dumpskinlayers] scene materials={scene.Materials.Count}; scanning for 'Head shell' region_map / BaseColor...");
                    var (modelBBytes, modelBNote) = ExtractFullBodyDiffuse(scene);
                    Console.WriteLine($"[dumpskinlayers] head-shell probe: {modelBNote}");
                    Dump("base_modelB_underwear_candidate.png", modelBBytes,
                        role: "BASE candidate (Model B: head-CASP full-body diffuse — the alleged 'WITH underwear' base)",
                        blend: "pre-rendered base (BuildAtlasFromPreRenderedBaseAsync path)",
                        alphaSemantics: "alpha = UV-island coverage");

                    // Also dump EVERY material's textures so we can see exactly what the assembled
                    // scene carries (filename, slot, semantic, TGI) for the layer map.
                    Console.WriteLine();
                    Console.WriteLine("[dumpskinlayers] FULL scene material/texture inventory:");
                    for (var m = 0; m < scene.Materials.Count; m++)
                    {
                        var mat = scene.Materials[m];
                        Console.WriteLine($"  mat[{m}] name='{mat.Name}' approx='{Trunc(mat.Approximation, 80)}'");
                        foreach (var t in mat.Textures)
                        {
                            var (tw, th, ta, to) = t.PngBytes is { Length: > 0 } ? ProbePng(t.PngBytes) : (0, 0, false, false);
                            var tgi = t.SourceKey is { } k ? $"{k.Type:X8}:{k.Group:X8}:{k.FullInstance:X16}" : "(no key)";
                            Console.WriteLine(
                                $"      slot='{t.Slot}' file='{t.FileName}' sem={t.Semantic} {tw}x{th} bytes={t.PngBytes?.Length ?? 0:N0} hasAlpha={ta} opaque={to} tgi={tgi}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[dumpskinlayers] scene-assembly probe FAILED ({ex.GetType().Name}: {ex.Message}).");
        }

        // 8) Write the report + the resolver Notes string (the authoritative provenance trail).
        var reportPath = Path.Combine(outDir, "_LAYER_REPORT.txt");
        var lines = new List<string>
        {
            $"SKIN LAYER DUMP — {ageLabel} {genderLabel} tone 0x{skintoneInstance:X16}",
            $"skintone resource : {skin.SkintoneResourceTgi ?? "(none)"}",
            $"  package         : {skin.SkintonePackagePath ?? "(none)"}",
            $"base texture TGI  : {skin.BaseTextureResourceTgi ?? "(none)"}  (Model A: skinSets[0].TextureInstance)",
            $"  package         : {skin.BaseTexturePackagePath ?? "(none)"}",
            $"hue=0x{skin.SkintoneHue:X4} sat=0x{skin.SkintoneSaturation:X4} overlayOpacity={skin.OverlayOpacity} shift={skin.SkintoneShift?.ToString("0.###") ?? "(none)"}",
            "",
            "LAYERS:",
        };
        lines.AddRange(report);
        lines.Add("");
        lines.Add("RESOLVER NOTES (provenance trail):");
        lines.Add(skin.Notes);
        File.WriteAllText(reportPath, string.Join(Environment.NewLine, lines));
        Console.WriteLine();
        Console.WriteLine($"[dumpskinlayers] wrote report: {reportPath}");
        Console.WriteLine("[dumpskinlayers] DONE.");
        return true;
    }

    private static string Trunc(string? s, int n) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n - 1) + "…");

    // Verbatim replica of SimConstructorViewModel.TryExtractFullBodyDiffuse (src/, build at audit
    // time): scan for a 'Head shell' material, prefer its region_map texture (>=200KB) else its
    // BaseColor texture (>=200KB, not the composed skin_atlas/head_atlas), as the full-body diffuse.
    private const int FullBodyDiffuseMinSizeBytes = 200 * 1024;

    private static (byte[]? Bytes, string Note) ExtractFullBodyDiffuse(CanonicalScene scene)
    {
        foreach (var material in scene.Materials)
        {
            if (string.IsNullOrEmpty(material.Approximation) ||
                !material.Approximation.Contains("Head shell", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            var regionMap = material.Textures.FirstOrDefault(t =>
                string.Equals(t.Slot, "region_map", StringComparison.OrdinalIgnoreCase));
            if (regionMap?.PngBytes is { Length: > 0 } regionBytes)
            {
                if (regionBytes.Length >= FullBodyDiffuseMinSizeBytes)
                {
                    return (regionBytes, $"matched Head-shell region_map ({regionBytes.Length:N0} bytes >= {FullBodyDiffuseMinSizeBytes:N0} threshold) -> USED as Model-B base");
                }
            }
            var baseColor = material.Textures.FirstOrDefault(t => t.Semantic == CanonicalTextureSemantic.BaseColor);
            if (baseColor?.PngBytes is { Length: > 0 } baseBytes &&
                baseBytes.Length >= FullBodyDiffuseMinSizeBytes &&
                !string.Equals(baseColor.FileName, "skin_atlas.png", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(baseColor.FileName, "head_atlas.png", StringComparison.OrdinalIgnoreCase))
            {
                return (baseBytes, $"matched Head-shell BaseColor '{baseColor.FileName}' ({baseBytes.Length:N0} bytes) -> USED as Model-B base");
            }
            var rmLen = regionMap?.PngBytes?.Length ?? 0;
            var bcLen = baseColor?.PngBytes?.Length ?? 0;
            return (null,
                $"found Head-shell material but NO >=200KB full-body diffuse survives (region_map={rmLen:N0}B, BaseColor='{baseColor?.FileName}' {bcLen:N0}B) -> Model B retired; App falls back to BuildAsync");
        }
        return (null, "no 'Head shell' material found in scene");
    }

    // Probe a PNG byte[] for dimensions and alpha presence using System.Drawing.
    private static (int Width, int Height, bool HasAlpha, bool FullyOpaque) ProbePng(byte[] png)
    {
        try
        {
            using var ms = new MemoryStream(png, writable: false);
            using var bmp = new Bitmap(ms);
            var w = bmp.Width;
            var h = bmp.Height;
            // PixelFormat alpha capability is the cheap signal; scan for an actual <255 alpha to
            // distinguish "has alpha channel but fully opaque" from "meaningful transparency".
            var hasAlphaChannel = (bmp.PixelFormat & PixelFormat.Alpha) != 0
                || bmp.PixelFormat == PixelFormat.Format32bppArgb
                || bmp.PixelFormat == PixelFormat.Format32bppPArgb;
            if (!hasAlphaChannel)
            {
                return (w, h, false, true);
            }
            using var argb = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(argb))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                g.DrawImage(bmp, 0, 0, w, h);
            }
            var rect = new Rectangle(0, 0, w, h);
            var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var row = new byte[stride];
                var fullyOpaque = true;
                var anyAlpha = false;
                for (var y = 0; y < h && fullyOpaque; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), row, 0, stride);
                    for (var x = 0; x < w; x++)
                    {
                        if (row[x * 4 + 3] != 255)
                        {
                            fullyOpaque = false;
                            anyAlpha = true;
                            break;
                        }
                    }
                }
                return (w, h, anyAlpha || true, fullyOpaque);
            }
            finally
            {
                argb.UnlockBits(data);
            }
        }
        catch
        {
            return (0, 0, false, false);
        }
    }
}
