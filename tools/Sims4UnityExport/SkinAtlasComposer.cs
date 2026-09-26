// SkinAtlasComposer — a FAITHFUL, platform-agnostic (System.Drawing / net8.0-windows) port of
// src/Sims4ResourceExplorer.App/SimSkinAtlasComposer.cs, so the headless exporter composes the
// EXACT same full-fidelity skin atlas the WinUI Sim Constructor renders.
//
// What is replicated verbatim from the App class (do NOT re-derive the math here — it is the
// transcribed in-game sim albedo chain, eid 602):
//   * BuildAsync:
//       - optional base-skin-alpha bias toward mid-gray,
//       - optional CAS SkintoneShift (HSV-Value brightness offset),
//       - grayscale DETAIL canvas D = neutral + overlay + per-physique [heavy,fit,lean,bony]
//         rows, alpha-composited with COVERAGE TRACKING (transparent cut-outs are LEFT
//         transparent — the detail layer's meaningful holes reveal the clean base, they are
//         NOT filled),
//       - the albedo equation: base = C·(1+D)/2 (identity ramp), overlay(base,D) twice,
//         skin = lerp(o1, o2, pass2Opacity), per channel in byte/gamma space — then the whole
//         detailed result is lerp(baseC, result, detailCoverage) so uncovered texels stay base C,
//       - face tone overlay (straight-alpha source-over),
//       - face CAS overlays (eye color / brows / makeup) — SUPPORTED by BuildAtlas (straight-
//         alpha source-over in order, verbatim from the App) but DELIBERATELY NOT SUPPLIED by
//         ComposeAtlas: EA's eye-color CAS texture is not body-atlas-aligned (its iris sits in a
//         far-left corner patch keyed to a separate eye-mesh UV), so source-over compositing it
//         onto the unified skin atlas at (0,0) — what both BuildAsync and the App do — lands the
//         iris off the eye socket and the thigh UV samples it. See the ComposeAtlas note.
//   * DeriveNormalMapPng: luminance height field → central-difference tangent-space normal,
//     gutter pixels forced flat, encoded R=X G=Y B=Z (BGRA byte order so sampled RGB = X,Y,Z).
//
// Channel order: System.Drawing Format32bppArgb is little-endian B,G,R,A in memory — IDENTICAL
// to WinRT Bgra8 — so every [i]=B, [i+1]=G, [i+2]=R, [i+3]=A index in the App ports 1:1. The
// App decodes with BitmapAlphaMode.Straight (non-premultiplied); a Format32bppArgb GDI+ bitmap
// is likewise straight, so no un-premultiply step is needed. Overlay/mask resize-to-base uses
// HighQualityBicubic to match the App's BitmapInterpolationMode.Linear scaled decode.
//
// All pixel buffers below are TIGHT (width*height*4, no stride padding) exactly like the App's
// WinRT pixel arrays; the GDI+ stride padding is unpacked on decode and repacked on encode.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class SkinAtlasComposer
{
    // -----------------------------------------------------------------------------------------
    // Public surface required by the brief.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Compose the full-fidelity skin atlas from a resolved <see cref="SimSkintoneRenderSummary"/>,
    /// returning a PNG byte[]. Maps the summary's fields to the App's BuildAsync parameters exactly
    /// as SimConstructorViewModel does, with the App's DEFAULT SkinLayerSettings (all per-layer
    /// alphas = 1.0). Physique weights are taken from the summary's own PhysiqueWeights when
    /// present (synthesised Sims carry none, so the brief's "full physique" need is satisfied by
    /// whatever the resolver supplies; see ComposeAtlas overload to force weights).
    /// </summary>
    public static byte[]? ComposeAtlas(SimSkintoneRenderSummary summary) =>
        ComposeAtlas(summary, summary.PhysiqueWeights);

    /// <summary>
    /// As <see cref="ComposeAtlas(SimSkintoneRenderSummary)"/> but with explicit physique weights
    /// [heavy, fit, lean, bony] (each in [0,1]) — lets the exporter blend the per-physique detail
    /// rows even for synthesised Sims whose summary carries no weights.
    /// </summary>
    public static byte[]? ComposeAtlas(SimSkintoneRenderSummary summary, IReadOnlyList<float>? physiqueWeights)
    {
        // Default SkinLayerSettings (SkinLayerSettings.cs): every alpha 1.0, no SkintoneShift.
        // pass2Opacity = (OverlayOpacity / 100) * DetailOverlayAlpha(=1).  (ViewModel line 460.)
        var pass2Opacity = (summary.OverlayOpacity / 100f) * 1f;

        // FACE CAS OVERLAYS RE-ENABLED (build 0327).
        //
        // BuildAtlas composites the face CAS overlays (eye color / brows) verbatim from the App —
        // straight-alpha source-over at (0,0), no dest rect / region / scale. The resolver's CAS-part
        // diffuse PNGs are FULL-ATLAS (1024x2048) canvases that are transparent everywhere except a
        // small opaque patch already at its correct UV-island position (e.g. the brown iris sits on
        // the eye island, brows above the eyes). So source-over at (0,0), full size, lands them
        // correctly — which is exactly what BuildAtlas does, identical to the App.
        //
        // These were previously SKIPPED because the iris appeared misplaced; that turned out to be a
        // separate V-axis (UV) bug in the Unity builder, since fixed. The overlay PNGs themselves are
        // correct, so we now pass the summary's FaceCasOverlayPngBytes straight through. faceCasOverlayAlphas
        // stays null (per-slot alpha 1.0, App default).
        IReadOnlyList<byte[]>? faceCasOverlayPngs = summary.FaceCasOverlayPngBytes;

        return BuildAtlas(
            baseSkinPng: summary.BaseTexturePngBytes,
            detailNeutralPng: summary.DetailNeutralPngBytes,
            detailOverlayPng: summary.DetailOverlayPngBytes,
            faceOverlayPng: summary.FaceOverlayPngBytes,
            faceCasOverlayPngs: faceCasOverlayPngs,
            pass2Opacity: pass2Opacity,
            detailNeutralAlpha: 1f,
            faceOverlayAlpha: 1f,
            faceCasOverlayAlphas: null,
            baseSkinAlpha: 1f,
            skintoneShift: summary.SkintoneShift ?? 0f,
            physiqueDetailPngs: summary.PhysiqueDetailPngBytes,
            physiqueOverlayPngs: summary.PhysiqueOverlayPngBytes,
            physiqueWeights: physiqueWeights);
    }

    /// <summary>
    /// Derive a tangent-space normal-map PNG from a composed atlas PNG. Mirrors the App's
    /// DeriveNormalMapPngAsync; <paramref name="strength"/> defaults to 3.0 (the App's live value,
    /// SimConstructorViewModel lines 482/906).
    /// </summary>
    public static byte[]? DeriveNormalMap(byte[] atlasPng, float strength = 3.0f) =>
        DeriveNormalMapInternal(atlasPng, strength);

    // -----------------------------------------------------------------------------------------
    // DEBUG: dump every compositor INPUT and STAGE as its own PNG so the artifact can be
    // localized visually. Mirrors ComposeAtlas's parameter mapping exactly, then runs the SAME
    // BuildAtlas chain with a debug sink that writes each named stage to <debugDir>.
    // -----------------------------------------------------------------------------------------
    public static byte[]? ComposeAtlasDebug(
        SimSkintoneRenderSummary summary,
        IReadOnlyList<float>? physiqueWeights,
        string debugDir)
    {
        Directory.CreateDirectory(debugDir);
        var pass2Opacity = (summary.OverlayOpacity / 100f) * 1f;

        void DumpInput(string name, byte[]? png)
        {
            if (png is not { Length: > 0 })
            {
                Console.WriteLine($"[bakeskindebug]   {name}: (none)");
                return;
            }
            var decoded = DecodeBgra8Straight(png); // NATIVE resolution, no scale
            if (decoded is null)
            {
                Console.WriteLine($"[bakeskindebug]   {name}: (decode failed)");
                return;
            }
            var path = Path.Combine(debugDir, name + ".png");
            var re = EncodeBgra8AsPng(decoded.Value.Width, decoded.Value.Height, decoded.Value.Pixels);
            if (re is not null) File.WriteAllBytes(path, re);
            Console.WriteLine($"[bakeskindebug]   {name}: {decoded.Value.Width}x{decoded.Value.Height} -> {Path.GetFileName(path)}");
        }

        Console.WriteLine("[bakeskindebug] INPUT native resolutions:");
        DumpInput("00_base_skin", summary.BaseTexturePngBytes);
        DumpInput("01_detail_neutral", summary.DetailNeutralPngBytes);
        DumpInput("02_detail_overlay", summary.DetailOverlayPngBytes);
        var physDetail = summary.PhysiqueDetailPngBytes;
        var physOverlay = summary.PhysiqueOverlayPngBytes;
        string[] physNames = { "heavy", "fit", "lean", "bony" };
        for (var i = 0; i < 4; i++)
        {
            DumpInput($"03_physique_detail_{i}_{physNames[i]}",
                physDetail is not null && i < physDetail.Count ? physDetail[i] : null);
            DumpInput($"04_physique_overlay_{i}_{physNames[i]}",
                physOverlay is not null && i < physOverlay.Count ? physOverlay[i] : null);
        }
        DumpInput("05_face_overlay", summary.FaceOverlayPngBytes);

        var sink = new DebugSink(debugDir);
        return BuildAtlas(
            baseSkinPng: summary.BaseTexturePngBytes,
            detailNeutralPng: summary.DetailNeutralPngBytes,
            detailOverlayPng: summary.DetailOverlayPngBytes,
            faceOverlayPng: summary.FaceOverlayPngBytes,
            faceCasOverlayPngs: null,
            pass2Opacity: pass2Opacity,
            detailNeutralAlpha: 1f,
            faceOverlayAlpha: 1f,
            faceCasOverlayAlphas: null,
            baseSkinAlpha: 1f,
            skintoneShift: summary.SkintoneShift ?? 0f,
            physiqueDetailPngs: summary.PhysiqueDetailPngBytes,
            physiqueOverlayPngs: summary.PhysiqueOverlayPngBytes,
            physiqueWeights: physiqueWeights,
            debug: sink);
    }

    /// <summary>
    /// Optional debug sink: BuildAtlas writes each named composite STAGE (already at base
    /// resolution) here so they can be inspected on disk. Null in the production path.
    /// </summary>
    private sealed class DebugSink
    {
        private readonly string _dir;
        public DebugSink(string dir) => _dir = dir;

        public void Dump(string name, int width, int height, byte[] pixels)
        {
            try
            {
                var png = EncodeBgra8AsPng(width, height, pixels);
                if (png is null) return;
                var path = Path.Combine(_dir, name + ".png");
                File.WriteAllBytes(path, png);
                Console.WriteLine($"[bakeskindebug]   stage {name}: {width}x{height} -> {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[bakeskindebug]   stage {name}: FAILED ({ex.GetType().Name}: {ex.Message})");
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // Port of SimSkinAtlasComposer.BuildAsync (the skintone-base SkinBlender chain).
    // -----------------------------------------------------------------------------------------
    private static byte[]? BuildAtlas(
        byte[]? baseSkinPng,
        byte[]? detailNeutralPng,
        byte[]? detailOverlayPng,
        byte[]? faceOverlayPng,
        IReadOnlyList<byte[]>? faceCasOverlayPngs,
        float pass2Opacity,
        float detailNeutralAlpha,
        float faceOverlayAlpha,
        IReadOnlyList<float>? faceCasOverlayAlphas,
        float baseSkinAlpha,
        float skintoneShift,
        IReadOnlyList<byte[]?>? physiqueDetailPngs,
        IReadOnlyList<byte[]?>? physiqueOverlayPngs,
        IReadOnlyList<float>? physiqueWeights,
        DebugSink? debug = null)
    {
        var clampedBaseSkinAlpha = Math.Clamp(baseSkinAlpha, 0f, 1f);
        var clampedDetailNeutralAlpha = Math.Clamp(detailNeutralAlpha, 0f, 1f);
        var clampedFaceOverlayAlpha = Math.Clamp(faceOverlayAlpha, 0f, 1f);
        if (baseSkinPng is not { Length: > 0 })
        {
            return null;
        }

        var skin = DecodeBgra8Straight(baseSkinPng);
        if (skin is null)
        {
            return null;
        }

        var width = skin.Value.Width;
        var height = skin.Value.Height;
        var skinPixels = skin.Value.Pixels;

        // Base skin alpha — biases the substrate toward neutral mid-gray.
        if (clampedBaseSkinAlpha < 1f)
        {
            for (var i = 0; i < skinPixels.Length; i += 4)
            {
                for (var c = 0; c < 3; c++)
                {
                    skinPixels[i + c] = (byte)((skinPixels[i + c] * clampedBaseSkinAlpha) + (128f * (1f - clampedBaseSkinAlpha)));
                }
            }
        }

        // CAS SkintoneShift — brightness offset on the base color C, BEFORE the detail equation.
        if (MathF.Abs(skintoneShift) > 0.001f)
        {
            HsvValueShiftInPlace(skinPixels, skintoneShift);
        }

        // 1. Build the grayscale detail canvas D (coverage-tracked alpha composite; NO hole fill —
        //    transparent cut-outs stay transparent and reveal the clean base in the albedo pass).
        byte[]? detailsPixels = null;
        if (detailNeutralPng is { Length: > 0 } || detailOverlayPng is { Length: > 0 })
        {
            detailsPixels = new byte[width * height * 4];
        }
        if (detailsPixels is not null && detailNeutralPng is { Length: > 0 })
        {
            var neutral = DecodeBgra8Straight(detailNeutralPng, width, height);
            if (neutral is not null)
            {
                if (clampedDetailNeutralAlpha < 1f)
                {
                    ScaleAlphaInPlace(neutral.Value.Pixels, clampedDetailNeutralAlpha);
                }
                BlendStraightAlphaOverTrackingCoverage(detailsPixels, neutral.Value.Pixels);
            }
        }
        if (detailsPixels is not null && detailOverlayPng is { Length: > 0 })
        {
            var overlay = DecodeBgra8Straight(detailOverlayPng, width, height);
            if (overlay is not null)
            {
                BlendStraightAlphaOverTrackingCoverage(detailsPixels, overlay.Value.Pixels);
            }
        }
        // Per-physique rows: for each i in [heavy, fit, lean, bony] with weight > 0, blend detail
        // then overlay at alpha = weight[i] (SkinBlender order).
        if (detailsPixels is not null && physiqueWeights is { Count: > 0 } && physiqueDetailPngs is not null)
        {
            for (var i = 0; i < 4; i++)
            {
                var weight = i < physiqueWeights.Count ? Math.Clamp(physiqueWeights[i], 0f, 1f) : 0f;
                if (weight <= 0.001f)
                {
                    continue;
                }
                if (i < physiqueDetailPngs.Count && physiqueDetailPngs[i] is { Length: > 0 } detailRow)
                {
                    var decoded = DecodeBgra8Straight(detailRow, width, height);
                    if (decoded is not null)
                    {
                        ScaleAlphaInPlace(decoded.Value.Pixels, weight);
                        BlendStraightAlphaOverTrackingCoverage(detailsPixels, decoded.Value.Pixels);
                    }
                }
                if (physiqueOverlayPngs is not null && i < physiqueOverlayPngs.Count && physiqueOverlayPngs[i] is { Length: > 0 } overlayRow)
                {
                    var decoded = DecodeBgra8Straight(overlayRow, width, height);
                    if (decoded is not null)
                    {
                        ScaleAlphaInPlace(decoded.Value.Pixels, weight);
                        BlendStraightAlphaOverTrackingCoverage(detailsPixels, decoded.Value.Pixels);
                    }
                }
            }
        }
        if (detailsPixels is not null)
        {
            // NO HOLE-FILL. The detail layers are deliberately TRANSPARENT in the face cut-outs
            // (brows, glabella/nose-bridge, nostrils, lips, lashes) — "no detail here, show the
            // clean base." Filling those areas (old scan-line interpolation, then an isotropic
            // dilation) smeared/blotched detail into the face. Instead we KEEP the alpha-composited
            // coverage as-is and, in the albedo pass below, lerp the detailed result back toward the
            // untouched base by that coverage. Where coverage == 0 -> exactly base C (clean cut-out);
            // where coverage == 1 -> full detailed result; partial alpha -> smooth blend. This sidesteps
            // any "what neutral D value goes in the hole" ambiguity entirely.
            debug?.Dump("06_detail_canvas_coverage", width, height, GrayToViewable(detailsPixels));
        }

        // 2. The transcribed in-game albedo equation: base = C·(1+D)/2, overlay twice, lerp by k.
        //    Applied ONLY where the detail canvas is OPAQUE; uncovered texels keep the base color
        //    via a final lerp(baseC, albedoResult, detailCoverage).
        if (detailsPixels is not null && detailsPixels.Length == skinPixels.Length)
        {
            var detailMix = Math.Clamp(pass2Opacity, 0f, 1f);
            for (var i = 0; i < skinPixels.Length; i += 4)
            {
                var coverage = detailsPixels[i + 3] / 255f; // accumulated opaque-detail coverage
                if (coverage <= 0f)
                {
                    continue; // fully uncovered cut-out -> leave base C exactly as-is (no smear)
                }
                var d = detailsPixels[i + 1] / 255f; // detail composite is grayscale
                var rampScale = (1f + d) * 0.5f;
                for (var c = 0; c < 3; c++)
                {
                    var color = skinPixels[i + c] / 255f; // untouched base C for this channel
                    var baseChannel = color * rampScale;
                    var o1 = OverlayBlend(baseChannel, d);
                    var o2 = OverlayBlend(o1, d);
                    var albedo = o1 + ((o2 - o1) * detailMix);
                    // lerp(baseC, albedoResult, coverage): partially-covered edges blend smoothly,
                    // fully-covered texels (coverage==1) take the full detailed result.
                    var skinChannel = color + ((albedo - color) * coverage);
                    if (skinChannel < 0f) skinChannel = 0f;
                    if (skinChannel > 1f) skinChannel = 1f;
                    skinPixels[i + c] = (byte)((skinChannel * 255f) + 0.5f);
                }
            }
        }

        // 3. Draw the tone face overlay on top of the composited skin (straight-alpha source-over).
        if (faceOverlayPng is { Length: > 0 } && clampedFaceOverlayAlpha > 0f)
        {
            var faceOverlay = DecodeBgra8Straight(faceOverlayPng, width, height);
            if (faceOverlay is not null)
            {
                if (clampedFaceOverlayAlpha < 1f)
                {
                    ScaleAlphaInPlace(faceOverlay.Value.Pixels, clampedFaceOverlayAlpha);
                }
                BlendStraightAlphaOver(skinPixels, faceOverlay.Value.Pixels);
            }
        }

        // 4. Draw face CAS overlay textures (EyeColor, Brows, makeup) in input order.
        if (faceCasOverlayPngs is not null)
        {
            for (var slotIndex = 0; slotIndex < faceCasOverlayPngs.Count; slotIndex++)
            {
                var casOverlayPng = faceCasOverlayPngs[slotIndex];
                if (casOverlayPng is not { Length: > 0 })
                {
                    continue;
                }
                var slotAlpha = faceCasOverlayAlphas is { } alphas && slotIndex < alphas.Count
                    ? Math.Clamp(alphas[slotIndex], 0f, 1f)
                    : 1f;
                if (slotAlpha <= 0f)
                {
                    continue;
                }
                var casOverlay = DecodeBgra8Straight(casOverlayPng, width, height);
                if (casOverlay is not null)
                {
                    if (slotAlpha < 1f)
                    {
                        ScaleAlphaInPlace(casOverlay.Value.Pixels, slotAlpha);
                    }
                    BlendStraightAlphaOver(skinPixels, casOverlay.Value.Pixels);
                }
            }
        }

        debug?.Dump("08_final_atlas_before_dilate", width, height, skinPixels);

        return EncodeBgra8AsPng(width, height, skinPixels);
    }

    // Render the grayscale DETAIL canvas (gray stored in B/G/R, accumulated coverage in alpha)
    // to a viewable opaque PNG: gray value -> RGB, alpha forced to 255 so transparent/uncovered
    // areas (which carry RGB(0) garbage under alpha-0) are visible as black holes.
    private static byte[] GrayToViewable(byte[] gray)
    {
        var view = new byte[gray.Length];
        for (var i = 0; i < gray.Length; i += 4)
        {
            view[i] = gray[i + 1];
            view[i + 1] = gray[i + 1];
            view[i + 2] = gray[i + 1];
            view[i + 3] = 255;
        }
        return view;
    }

    // -----------------------------------------------------------------------------------------
    // Port of SimSkinAtlasComposer.DeriveNormalMapPngAsync.
    // -----------------------------------------------------------------------------------------
    private static byte[]? DeriveNormalMapInternal(byte[] atlasPng, float strength)
    {
        if (atlasPng is not { Length: > 0 })
        {
            return null;
        }
        var decoded = DecodeBgra8Straight(atlasPng);
        if (decoded is null)
        {
            return null;
        }
        var width = decoded.Value.Width;
        var height = decoded.Value.Height;
        var px = decoded.Value.Pixels;

        // Precompute a luminance height field (0..1). BGRA byte order: B=p, G=p+1, R=p+2.
        var lum = new float[width * height];
        for (var i = 0; i < lum.Length; i++)
        {
            var p = i * 4;
            lum[i] = (0.114f * px[p] + 0.587f * px[p + 1] + 0.299f * px[p + 2]) / 255f;
        }

        var outPx = new byte[width * height * 4];
        const float gutterThreshold = 0.04f;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var idx = y * width + x;
                var p = idx * 4;
                var here = lum[idx];
                if (here < gutterThreshold)
                {
                    outPx[p] = 255; outPx[p + 1] = 128; outPx[p + 2] = 128; outPx[p + 3] = 255;
                    continue;
                }
                var xl = x > 0 ? lum[idx - 1] : here;
                var xr = x < width - 1 ? lum[idx + 1] : here;
                var yt = y > 0 ? lum[idx - width] : here;
                var yb = y < height - 1 ? lum[idx + width] : here;
                if (xl < gutterThreshold || xr < gutterThreshold || yt < gutterThreshold || yb < gutterThreshold)
                {
                    outPx[p] = 255; outPx[p + 1] = 128; outPx[p + 2] = 128; outPx[p + 3] = 255;
                    continue;
                }
                var dx = (xr - xl) * strength;
                var dy = (yb - yt) * strength;
                var nx = -dx;
                var ny = -dy;
                const float nz = 1f;
                var inv = 1f / MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                nx *= inv; ny *= inv;
                var nzn = nz * inv;
                outPx[p] = (byte)Math.Clamp((nzn * 0.5f + 0.5f) * 255f, 0f, 255f);     // B = Z
                outPx[p + 1] = (byte)Math.Clamp((ny * 0.5f + 0.5f) * 255f, 0f, 255f);  // G = Y
                outPx[p + 2] = (byte)Math.Clamp((nx * 0.5f + 0.5f) * 255f, 0f, 255f);  // R = X
                outPx[p + 3] = 255;
            }
        }
        return EncodeBgra8AsPng(width, height, outPx);
    }

    // -----------------------------------------------------------------------------------------
    // Blend / fill helpers — verbatim ports of the App's private methods.
    // -----------------------------------------------------------------------------------------
    private static float OverlayBlend(float baseChannel, float detail) =>
        baseChannel < 0.5f
            ? 2f * baseChannel * detail
            : 1f - (2f * (1f - baseChannel) * (1f - detail));

    private static void BlendStraightAlphaOverTrackingCoverage(byte[] dst, byte[] src)
    {
        if (dst.Length != src.Length)
        {
            return;
        }
        for (var i = 0; i < dst.Length; i += 4)
        {
            var srcAlpha = src[i + 3] / 255f;
            if (srcAlpha <= 0f)
            {
                continue;
            }
            var dstAlpha = dst[i + 3] / 255f;
            var outAlpha = srcAlpha + (dstAlpha * (1f - srcAlpha));
            if (outAlpha <= 0f)
            {
                continue;
            }
            for (var c = 0; c < 3; c++)
            {
                var blended = ((src[i + c] * srcAlpha) + (dst[i + c] * dstAlpha * (1f - srcAlpha))) / outAlpha;
                if (blended < 0f) blended = 0f;
                if (blended > 255f) blended = 255f;
                dst[i + c] = (byte)blended;
            }
            dst[i + 3] = (byte)Math.Clamp(outAlpha * 255f, 0f, 255f);
        }
    }

    // NOTE: the detail-canvas hole-fill (formerly FillUncoveredByScanlineInterpolation, later an
    // isotropic dilation) has been REMOVED. The detail layers are an overlay with meaningful
    // transparency — the face cut-outs (brows, glabella/nose-bridge, nostrils, lips, lashes) mean
    // "no detail here, show the clean base," not "interpolate detail across me." Filling them
    // painted spread/blotchy detail into the face (a vertical smear down the nose-bridge, blotches
    // around nostrils/lips/chin). Instead the albedo pass now lerps lerp(baseC, albedoResult,
    // coverage) where coverage is the alpha-composited opaque-detail union, so uncovered texels keep
    // the base color untouched. The final-atlas gutter dilation (DilateOpaque, for UV-edge bleed) is
    // a separate concern on the OUTPUT atlas and is unaffected.

    private static void HsvValueShiftInPlace(byte[] bgra, float vShift)
    {
        var shift255 = vShift * 255f;
        for (var i = 0; i < bgra.Length; i += 4)
        {
            float b = bgra[i];
            float g = bgra[i + 1];
            float r = bgra[i + 2];
            var max = MathF.Max(r, MathF.Max(g, b));
            if (max <= 0f)
            {
                var gray = Math.Clamp(shift255, 0f, 255f);
                bgra[i] = (byte)gray;
                bgra[i + 1] = (byte)gray;
                bgra[i + 2] = (byte)gray;
                continue;
            }
            var newMax = Math.Clamp(max + shift255, 0f, 255f);
            var scale = newMax / max;
            bgra[i] = (byte)Math.Clamp(b * scale, 0f, 255f);
            bgra[i + 1] = (byte)Math.Clamp(g * scale, 0f, 255f);
            bgra[i + 2] = (byte)Math.Clamp(r * scale, 0f, 255f);
        }
    }

    private static void ScaleAlphaInPlace(byte[] bgra, float alpha)
    {
        var clamped = Math.Clamp(alpha, 0f, 1f);
        for (var i = 3; i < bgra.Length; i += 4)
        {
            bgra[i] = (byte)(bgra[i] * clamped);
        }
    }

    private static void BlendStraightAlphaOver(byte[] dst, byte[] src)
    {
        if (dst.Length != src.Length)
        {
            return;
        }
        for (var i = 0; i < dst.Length; i += 4)
        {
            var srcAlpha = src[i + 3] / 255f;
            if (srcAlpha <= 0f)
            {
                continue;
            }
            for (var c = 0; c < 3; c++)
            {
                var blended = src[i + c] * srcAlpha + dst[i + c] * (1f - srcAlpha);
                if (blended < 0f) blended = 0f;
                if (blended > 255f) blended = 255f;
                dst[i + c] = (byte)blended;
            }
        }
    }

    // -----------------------------------------------------------------------------------------
    // PNG I/O — System.Drawing replacement for the App's WinRT BitmapDecoder/Encoder. Returns
    // TIGHT (no stride padding) Bgra8 straight-alpha pixel buffers, matching the WinRT path.
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Decode <paramref name="pngBytes"/> to a tight Bgra8 straight-alpha buffer, optionally
    /// bilinearly scaled to (<paramref name="scaledWidth"/>, <paramref name="scaledHeight"/>) —
    /// the App's DecodeBgra8StraightAsync with the same scaled-decode contract.
    /// </summary>
    private static (int Width, int Height, byte[] Pixels)? DecodeBgra8Straight(
        byte[] pngBytes,
        int scaledWidth = 0,
        int scaledHeight = 0)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes, writable: false);
            // Decode into a detached 32bpp ARGB bitmap (straight alpha; B,G,R,A in memory).
            using var source = new Bitmap(ms);

            var srcW = source.Width;
            var srcH = source.Height;
            var needScale = scaledWidth > 0 && scaledHeight > 0 &&
                            (scaledWidth != srcW || scaledHeight != srcH);
            var outW = needScale ? scaledWidth : srcW;
            var outH = needScale ? scaledHeight : srcH;

            using var argb = new Bitmap(outW, outH, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(argb))
            {
                // SourceCopy so source alpha is copied verbatim (straight), not alpha-composited.
                g.CompositingMode = CompositingMode.SourceCopy;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.SmoothingMode = SmoothingMode.HighQuality;
                // HIGH-QUALITY resampling for any scaled decode (matches/exceeds the App's WinRT
                // BitmapInterpolationMode.Linear scaled decode). The previous port used
                // NearestNeighbor on the 1:1 path and only HighQualityBilinear when scaling; that
                // (and PixelOffsetMode.Half) injected the stair-stepping/banding the brief reports
                // whenever a detail/overlay/mask row was a different size than the 1024x2048 base.
                // HighQualityBicubic + PixelOffsetMode.HighQuality eliminates the nearest-neighbour
                // blockiness; the 1:1 (needScale==false) case is an exact copy regardless of mode.
                g.InterpolationMode = needScale
                    ? InterpolationMode.HighQualityBicubic
                    : InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawImage(source, new Rectangle(0, 0, outW, outH), 0, 0, srcW, srcH, GraphicsUnit.Pixel);
            }

            var pixels = ReadTightBgra(argb);
            return (outW, outH, pixels);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] ReadTightBgra(Bitmap argb)
    {
        var width = argb.Width;
        var height = argb.Height;
        var rect = new Rectangle(0, 0, width, height);
        var data = argb.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = data.Stride;
            var rowBytes = width * 4;
            var tight = new byte[width * height * 4];
            if (stride == rowBytes)
            {
                Marshal.Copy(data.Scan0, tight, 0, tight.Length);
            }
            else
            {
                // Unpack stride padding row by row into the tight buffer.
                var rowBuf = new byte[stride];
                for (var y = 0; y < height; y++)
                {
                    Marshal.Copy(IntPtr.Add(data.Scan0, y * stride), rowBuf, 0, rowBytes);
                    Buffer.BlockCopy(rowBuf, 0, tight, y * rowBytes, rowBytes);
                }
            }
            return tight;
        }
        finally
        {
            argb.UnlockBits(data);
        }
    }

    private static byte[]? EncodeBgra8AsPng(int width, int height, byte[] pixels)
    {
        try
        {
            using var argb = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, width, height);
            var data = argb.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var stride = data.Stride;
                var rowBytes = width * 4;
                if (stride == rowBytes)
                {
                    Marshal.Copy(pixels, 0, data.Scan0, pixels.Length);
                }
                else
                {
                    for (var y = 0; y < height; y++)
                    {
                        Marshal.Copy(pixels, y * rowBytes, IntPtr.Add(data.Scan0, y * stride), rowBytes);
                    }
                }
            }
            finally
            {
                argb.UnlockBits(data);
            }

            using var ms = new MemoryStream();
            argb.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
