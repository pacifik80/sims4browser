using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App;

/// <summary>
/// Builds the skin atlas by TRANSCRIBING the game's own sim pixel-shader albedo chain,
/// decoded from a live RenderDoc capture — see
/// <c>docs/workflows/material-pipeline/live-proof-packets/sim-draw-texture-bindings.md</c>
/// (eid 602 disassembly, yafem_amale_table.rdc):
/// <code>
///   D      = skinDetailComposite.gray            // hardcoded per-(age × gender) detail rows
///   C      = skinColorComposite.rgb              // tone.SkinSets[0] texture (per-tone color)
///   base   = lerp(C, C * ramp(D), 0.5)           // 256×1 ramp ≈ identity for unshifted tones
///   O1     = overlay(base, D)                    // photoshop overlay, branch on base &lt; 0.5
///   O2     = overlay(O1, D)
///   skin   = lerp(O1, O2, k)                     // k = cb0[189].x; tone-driven (0.27 captured)
///   albedo = lerp(skin, outfit.rgb, outfit.a)    // outfit composite alpha-over
/// </code>
/// Mapping to this method: <paramref name="baseSkinPng"/> = C;
/// <paramref name="detailNeutralPng"/> / <paramref name="detailOverlayPng"/> compose D;
/// <paramref name="pass2Opacity"/> = k; the face overlay + face CAS overlays (brows, eye
/// color — equipped bt=34/35 parts) play the outfit-composite role for the face region.
/// The ramp is treated as identity (its runtime source / SkintoneShift relation is a
/// tracked follow-up), so <c>base = C·(1+D)/2</c>.
/// <para/>
/// The game shader samples UNORM (non-sRGB) views and lights in gamma space, so this
/// byte-space CPU compositing matches the in-game color pipeline exactly.
/// <para/>
/// EA's detail maps carry transparent CUT-OUTS at brow/nostril/lash regions (those features
/// arrive via the outfit layer); decoded RGB under that alpha is zero. The detail canvas is
/// therefore composited alpha-aware and holes are filled by nearest-coverage scan-line
/// interpolation (mimicking the full-coverage runtime composite) before the equation runs.
/// <para/>
/// Replaced by this transcription (previous approximations, builds 0230-0312): SkinBlender
/// Pass 1 soft-light ×1.2 + hue-preserving variant, Pass 2 overlay, contrast 1.1@0.75, and
/// the saturation-gated Pass 3 hue shift (alien tones now get their color from C itself).
/// <paramref name="skintoneHue"/>, <paramref name="skintoneSaturation"/> and
/// <paramref name="pass3HueAlpha"/> are accepted for caller compatibility but no longer
/// used by this path.
/// Still pending: per-physique detail rows (heavy/fit/lean/bony), tan/burn skin sets,
/// SkintoneShift via the ramp.
/// </summary>
public static class SimSkinAtlasComposer
{
    public static async Task<byte[]?> BuildAsync(
        byte[]? baseSkinPng,
        byte[]? detailNeutralPng,
        byte[]? detailOverlayPng,
        byte[]? faceOverlayPng,
        IReadOnlyList<byte[]>? faceCasOverlayPngs,
        float pass2Opacity,
        ushort skintoneHue,
        ushort skintoneSaturation,
        CancellationToken cancellationToken,
        // Build 0301 — per-layer alpha multipliers driven by the constructor's Skin
        // textures panel. Defaults are 1f so existing callers behave unchanged.
        float detailNeutralAlpha = 1f,
        float pass3HueAlpha = 1f,
        float faceOverlayAlpha = 1f,
        IReadOnlyList<float>? faceCasOverlayAlphas = null,
        float baseSkinAlpha = 1f)
    {
        var clampedBaseSkinAlpha = System.Math.Clamp(baseSkinAlpha, 0f, 1f);
        var clampedDetailNeutralAlpha = System.Math.Clamp(detailNeutralAlpha, 0f, 1f);
        var clampedPass3HueAlpha = System.Math.Clamp(pass3HueAlpha, 0f, 1f);
        var clampedFaceOverlayAlpha = System.Math.Clamp(faceOverlayAlpha, 0f, 1f);
        if (baseSkinPng is not { Length: > 0 })
        {
            return null;
        }

        var skin = await DecodeBgra8StraightAsync(baseSkinPng, cancellationToken).ConfigureAwait(false);
        if (skin is null)
        {
            return null;
        }

        var width = skin.Value.Width;
        var height = skin.Value.Height;
        var skinPixels = skin.Value.Pixels;

        // Base skin alpha — biases the substrate toward a neutral mid-gray. At 1 the
        // skintone's base PNG is used as-is; at 0 the substrate becomes flat mid-gray
        // (useful as a diagnostic for "is the brown blotch in the base texture?").
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

        // 1. Build the grayscale detail canvas D. Layers are alpha-composited with coverage
        //    tracking (dst alpha accumulates max source alpha), then transparent cut-outs
        //    (brows/nostrils/lashes — see class doc) are filled by nearest-coverage
        //    scan-line interpolation so the equation never consumes the RGB(0) garbage that
        //    sits under alpha-0 in our LRLE/RLE2 decodes (root cause of the build-0303
        //    dark-T-zone blotch).
        byte[]? detailsPixels = null;
        if (detailNeutralPng is { Length: > 0 } || detailOverlayPng is { Length: > 0 })
        {
            detailsPixels = new byte[width * height * 4];
        }
        if (detailsPixels is not null && detailNeutralPng is { Length: > 0 })
        {
            var neutral = await DecodeBgra8StraightAsync(detailNeutralPng, cancellationToken, width, height).ConfigureAwait(false);
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
            var overlay = await DecodeBgra8StraightAsync(detailOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
            if (overlay is not null)
            {
                BlendStraightAlphaOverTrackingCoverage(detailsPixels, overlay.Value.Pixels);
            }
        }
        if (detailsPixels is not null)
        {
            FillUncoveredByScanlineInterpolation(detailsPixels, width, height);
        }

        // 2. The transcribed in-game albedo equation (see class doc; live-proof packet
        //    eid 602): base = C·(1+D)/2 (identity ramp), then overlay twice with D, mixed
        //    by k = pass2Opacity. The game evaluates this per-channel in gamma space on
        //    UNORM views; this loop is byte-space, so the color pipeline matches 1:1.
        if (detailsPixels is not null && detailsPixels.Length == skinPixels.Length)
        {
            var detailMix = Math.Clamp(pass2Opacity, 0f, 1f);
            for (var i = 0; i < skinPixels.Length; i += 4)
            {
                // Detail composite is grayscale — the game shader reads a single channel.
                var d = detailsPixels[i + 1] / 255f;
                var rampScale = (1f + d) * 0.5f;
                for (var c = 0; c < 3; c++)
                {
                    var color = skinPixels[i + c] / 255f;
                    var baseChannel = color * rampScale;
                    var o1 = OverlayBlend(baseChannel, d);
                    var o2 = OverlayBlend(o1, d);
                    var skinChannel = o1 + ((o2 - o1) * detailMix);
                    if (skinChannel < 0f) skinChannel = 0f;
                    if (skinChannel > 1f) skinChannel = 1f;
                    skinPixels[i + c] = (byte)((skinChannel * 255f) + 0.5f);
                }
            }
        }

        // 3. Draw the tone face overlay on top of the composited skin. Straight-alpha blend.
        if (faceOverlayPng is { Length: > 0 } && clampedFaceOverlayAlpha > 0f)
        {
            var faceOverlay = await DecodeBgra8StraightAsync(faceOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
            if (faceOverlay is not null)
            {
                if (clampedFaceOverlayAlpha < 1f)
                {
                    ScaleAlphaInPlace(faceOverlay.Value.Pixels, clampedFaceOverlayAlpha);
                }
                BlendStraightAlphaOver(skinPixels, faceOverlay.Value.Pixels);
            }
        }

        // 4. Draw face CAS overlay textures (EyeColor, Brows, makeup) resolved from the
        //    Sim's equipped CAS parts plus any user-picked overrides from the
        //    constructor's Skin textures panel. Blended in input order; each layer's
        //    alpha is pre-multiplied by the parallel-list entry from
        //    <paramref name="faceCasOverlayAlphas"/> (defaults to 1.0 per slot).
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
                    ? System.Math.Clamp(alphas[slotIndex], 0f, 1f)
                    : 1f;
                if (slotAlpha <= 0f)
                {
                    continue;
                }
                var casOverlay = await DecodeBgra8StraightAsync(casOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
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

        return await EncodeBgra8AsPngAsync(width, height, skinPixels, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Composes the atlas when the supplied <paramref name="preRenderedBasePng"/> is EA's
    /// full-body diffuse for this Sim (the texture that ships on the head CASPart and covers
    /// face, hands, feet, torso with bikini/underwear, and legs via the mesh's UV layout).
    /// Body shell materials AND the head shell material both UV-sample from this single
    /// texture in the EA runtime, so a single atlas serves all four meshes.
    ///
    /// Pass 1 / Pass 2 (soft-light × detail layers) are skipped — the pre-rendered base
    /// already carries all the anatomical shading and the bikini overlay; re-applying the
    /// detail/overlay chain would double-paint shadows that EA's compositor already baked.
    /// Pass 3 (HSL shift) still runs so non-default skintones (saturation >= 100, e.g. alien
    /// tones) retint the texture toward their hue. Face overlay + face CAS overlays (makeup,
    /// brow, eye color) composite on top — they only touch face UV regions.
    /// </summary>
    public static async Task<byte[]?> BuildAtlasFromPreRenderedBaseAsync(
        byte[] preRenderedBasePng,
        ushort skintoneHue,
        ushort skintoneSaturation,
        byte[]? faceOverlayPng,
        IReadOnlyList<byte[]>? faceCasOverlayPngs,
        IReadOnlyList<float>? faceCasOverlayAlphas,
        float faceOverlayAlpha,
        float pass3HueAlpha,
        CancellationToken cancellationToken)
    {
        if (preRenderedBasePng is not { Length: > 0 })
        {
            return null;
        }
        var decoded = await DecodeBgra8StraightAsync(preRenderedBasePng, cancellationToken).ConfigureAwait(false);
        if (decoded is null)
        {
            return null;
        }

        var width = decoded.Value.Width;
        var height = decoded.Value.Height;
        var pixels = decoded.Value.Pixels;

        var clampedPass3Alpha = System.Math.Clamp(pass3HueAlpha, 0f, 1f);
        // Integer division mirrors SkinBlender — Saturation in [0, 99] → no Pass 3 (default
        // human skintones), Saturation in [100, 199] → full hue overlay (aliens etc.).
        var overFactor = (float)(skintoneSaturation / 100) * clampedPass3Alpha;
        if (skintoneSaturation > 0 && overFactor > 0f)
        {
            var rgbOver = HslMidpointToRgb(skintoneHue);
            for (var i = 0; i < pixels.Length; i += 4)
            {
                for (var c = 0; c < 3; c++)
                {
                    float blended = pixels[i + c];
                    // BGRA layout: B=0, G=1, R=2; rgbOver is { R, G, B } so index is 2-c.
                    var overChannel = rgbOver[2 - c];
                    var pass3 = (blended / 255f) * (blended + ((2f * overChannel) / 255f) * (255f - blended));
                    blended = (pass3 * overFactor) + (blended * (1f - overFactor));
                    if (blended < 0f) blended = 0f;
                    if (blended > 255f) blended = 255f;
                    pixels[i + c] = (byte)blended;
                }
            }
        }

        var clampedFaceOverlayAlpha = System.Math.Clamp(faceOverlayAlpha, 0f, 1f);
        if (faceOverlayPng is { Length: > 0 } && clampedFaceOverlayAlpha > 0f)
        {
            var faceOverlay = await DecodeBgra8StraightAsync(faceOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
            if (faceOverlay is not null)
            {
                if (clampedFaceOverlayAlpha < 1f)
                {
                    ScaleAlphaInPlace(faceOverlay.Value.Pixels, clampedFaceOverlayAlpha);
                }
                BlendStraightAlphaOver(pixels, faceOverlay.Value.Pixels);
            }
        }

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
                    ? System.Math.Clamp(alphas[slotIndex], 0f, 1f)
                    : 1f;
                if (slotAlpha <= 0f)
                {
                    continue;
                }
                var casOverlay = await DecodeBgra8StraightAsync(casOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
                if (casOverlay is not null)
                {
                    if (slotAlpha < 1f)
                    {
                        ScaleAlphaInPlace(casOverlay.Value.Pixels, slotAlpha);
                    }
                    BlendStraightAlphaOver(pixels, casOverlay.Value.Pixels);
                }
            }
        }

        return await EncodeBgra8AsPngAsync(width, height, pixels, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Photoshop "overlay" blend for one channel, branch on the BASE value — matches the
    /// in-game shader (eid 602 disassembly lines 6-20): <c>base &lt; 0.5 ? 2·base·d :
    /// 1 − 2·(1−base)·(1−d)</c>.
    /// </summary>
    private static float OverlayBlend(float baseChannel, float detail) =>
        baseChannel < 0.5f
            ? 2f * baseChannel * detail
            : 1f - (2f * (1f - baseChannel) * (1f - detail));

    /// <summary>
    /// Full straight-alpha "over" that weights BOTH alphas:
    /// <c>outA = sa + da·(1−sa); rgb = (src·sa + dst·da·(1−sa)) / outA</c>.
    /// Unlike the opaque-destination over used elsewhere, this keeps a partial-alpha
    /// layer's TRUE color when composited onto an uncovered canvas (plain source-over onto
    /// empty black darkens RGB by the source alpha — that painted the whole soft-shaded
    /// face region dark). Destination alpha becomes the accumulated coverage that the
    /// hole-fill pass keys on.
    /// </summary>
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

    /// <summary>
    /// Fills pixels with no layer coverage (alpha below threshold) by scan-line
    /// interpolation between the nearest covered pixels: horizontal pass first, vertical
    /// pass for what remains, flat mid-gray for anything still uncovered (fully empty
    /// rows/columns). Mimics the full-coverage look of the game's runtime detail composite
    /// across EA's brow/nostril/lash cut-outs.
    /// </summary>
    private static void FillUncoveredByScanlineInterpolation(byte[] bgra, int width, int height)
    {
        const byte coverageThreshold = 8;

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            var x = 0;
            while (x < width)
            {
                if (bgra[((row + x) * 4) + 3] >= coverageThreshold)
                {
                    x++;
                    continue;
                }
                var gapStart = x;
                while (x < width && bgra[((row + x) * 4) + 3] < coverageThreshold)
                {
                    x++;
                }
                var leftIndex = gapStart - 1;
                var rightIndex = x < width ? x : -1;
                if (leftIndex < 0 && rightIndex < 0)
                {
                    continue; // fully empty row — vertical pass handles it
                }
                for (var fillX = gapStart; fillX < (x < width ? x : width); fillX++)
                {
                    var p = (row + fillX) * 4;
                    float t;
                    int leftP = -1, rightP = -1;
                    if (leftIndex >= 0) leftP = (row + leftIndex) * 4;
                    if (rightIndex >= 0) rightP = (row + rightIndex) * 4;
                    if (leftP >= 0 && rightP >= 0)
                    {
                        t = (fillX - leftIndex) / (float)(rightIndex - leftIndex);
                        for (var c = 0; c < 3; c++)
                        {
                            bgra[p + c] = (byte)(bgra[leftP + c] + ((bgra[rightP + c] - bgra[leftP + c]) * t));
                        }
                    }
                    else
                    {
                        var srcP = leftP >= 0 ? leftP : rightP;
                        for (var c = 0; c < 3; c++)
                        {
                            bgra[p + c] = bgra[srcP + c];
                        }
                    }
                    bgra[p + 3] = coverageThreshold; // mark filled (low confidence) but covered
                }
            }
        }

        // Vertical pass + final fallback for anything the horizontal pass could not reach.
        for (var x = 0; x < width; x++)
        {
            var lastCovered = -1;
            for (var y = 0; y < height; y++)
            {
                var p = ((y * width) + x) * 4;
                if (bgra[p + 3] >= coverageThreshold)
                {
                    if (lastCovered >= 0 && y - lastCovered > 1)
                    {
                        var topP = ((lastCovered * width) + x) * 4;
                        for (var fillY = lastCovered + 1; fillY < y; fillY++)
                        {
                            var fp = ((fillY * width) + x) * 4;
                            var t = (fillY - lastCovered) / (float)(y - lastCovered);
                            for (var c = 0; c < 3; c++)
                            {
                                bgra[fp + c] = (byte)(bgra[topP + c] + ((bgra[p + c] - bgra[topP + c]) * t));
                            }
                            bgra[fp + 3] = coverageThreshold;
                        }
                    }
                    else if (lastCovered < 0 && y > 0)
                    {
                        for (var fillY = 0; fillY < y; fillY++)
                        {
                            var fp = ((fillY * width) + x) * 4;
                            for (var c = 0; c < 3; c++)
                            {
                                bgra[fp + c] = bgra[p + c];
                            }
                            bgra[fp + 3] = coverageThreshold;
                        }
                    }
                    lastCovered = y;
                }
            }
            if (lastCovered >= 0 && lastCovered < height - 1)
            {
                var srcP = ((lastCovered * width) + x) * 4;
                for (var fillY = lastCovered + 1; fillY < height; fillY++)
                {
                    var fp = ((fillY * width) + x) * 4;
                    for (var c = 0; c < 3; c++)
                    {
                        bgra[fp + c] = bgra[srcP + c];
                    }
                    bgra[fp + 3] = coverageThreshold;
                }
            }
            else if (lastCovered < 0)
            {
                for (var fillY = 0; fillY < height; fillY++)
                {
                    var fp = ((fillY * width) + x) * 4;
                    bgra[fp] = 128;
                    bgra[fp + 1] = 128;
                    bgra[fp + 2] = 128;
                    bgra[fp + 3] = coverageThreshold;
                }
            }
        }
    }

    /// <summary>
    /// Pre-multiplies every BGRA pixel's alpha by <paramref name="alpha"/> (clamped 0..1).
    /// Used to scale a layer's contribution before the source-over blend.
    /// </summary>
    private static void ScaleAlphaInPlace(byte[] bgra, float alpha)
    {
        var clamped = System.Math.Clamp(alpha, 0f, 1f);
        for (var i = 3; i < bgra.Length; i += 4)
        {
            bgra[i] = (byte)(bgra[i] * clamped);
        }
    }

    /// <summary>
    /// Standard "source-over" alpha composite: <c>dst = src*srcA + dst*(1 - srcA)</c>, per-channel,
    /// preserving the destination alpha channel (the resulting atlas always renders opaque).
    /// </summary>
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

    /// <summary>
    /// Mirrors <c>GetRGB(hue, 127, 127)</c> in
    /// <c>docs/references/external/TS4SimRipper/src/SkinBlender.cs:339-369</c>. Converts a
    /// (hue, saturation=127, luminance=127) HSL triplet to RGB. Hue is in <c>0..239</c>;
    /// saturation/luminance are in <c>0..240</c>. Returned array is <c>{ R, G, B }</c>.
    /// </summary>
    private static byte[] HslMidpointToRgb(ushort hue)
    {
        const ushort saturation = 127;
        const ushort luminance = 127;
        var l = luminance / 240f;
        if (l > 1f) l = 1f;
        var s = saturation / 240f;
        float tmp1;
        if (l < 0.5f) tmp1 = l * (1f + s);
        else tmp1 = (l + s) - (l * s);
        var tmp2 = 2f * l - tmp1;
        var hueNormalized = hue / 239f;
        var r = HslToRgbChannel(hueNormalized + 0.333f, tmp1, tmp2);
        var g = HslToRgbChannel(hueNormalized, tmp1, tmp2);
        var b = HslToRgbChannel(hueNormalized - 0.333f, tmp1, tmp2);
        return new[] { r, g, b };
    }

    private static byte HslToRgbChannel(float value, float adjust1, float adjust2)
    {
        if (value < 0f) value += 1f;
        else if (value > 1f) value -= 1f;
        float channel;
        if ((6f * value) < 1f) channel = adjust2 + ((adjust1 - adjust2) * 6f * value);
        else if ((2f * value) < 1f) channel = adjust1;
        else if ((3f * value) < 2f) channel = adjust2 + ((adjust1 - adjust2) * (0.666f - value) * 6f);
        else channel = adjust2;
        channel *= 255f;
        if (channel < 0f) channel = 0f;
        if (channel > 255f) channel = 255f;
        return (byte)(channel + 0.5f);
    }

    private static async Task<(int Width, int Height, byte[] Pixels)?> DecodeBgra8StraightAsync(
        byte[] pngBytes,
        CancellationToken cancellationToken,
        int scaledWidth = 0,
        int scaledHeight = 0)
    {
        try
        {
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(pngBytes.AsBuffer()).AsTask(cancellationToken).ConfigureAwait(false);
            stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken).ConfigureAwait(false);
            var transform = new BitmapTransform();
            if (scaledWidth > 0 && scaledHeight > 0 &&
                ((uint)scaledWidth != decoder.PixelWidth || (uint)scaledHeight != decoder.PixelHeight))
            {
                transform.ScaledWidth = (uint)scaledWidth;
                transform.ScaledHeight = (uint)scaledHeight;
                transform.InterpolationMode = BitmapInterpolationMode.Linear;
            }
            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask(cancellationToken).ConfigureAwait(false);
            var resolvedWidth = scaledWidth > 0 ? scaledWidth : (int)decoder.PixelWidth;
            var resolvedHeight = scaledHeight > 0 ? scaledHeight : (int)decoder.PixelHeight;
            return (resolvedWidth, resolvedHeight, pixelData.DetachPixelData());
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> EncodeBgra8AsPngAsync(
        int width,
        int height,
        byte[] pixels,
        CancellationToken cancellationToken)
    {
        try
        {
            using var output = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask(cancellationToken).ConfigureAwait(false);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                (uint)width,
                (uint)height,
                96,
                96,
                pixels);
            await encoder.FlushAsync().AsTask(cancellationToken).ConfigureAwait(false);
            output.Seek(0);
            using var ms = new MemoryStream();
            using var input = output.AsStreamForRead();
            await input.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }
        catch
        {
            return null;
        }
    }
}
