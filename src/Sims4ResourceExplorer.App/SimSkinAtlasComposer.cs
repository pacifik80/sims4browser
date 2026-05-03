using System.IO;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App;

/// <summary>
/// Builds the single-skin-atlas that the in-game shader uses for both body and head meshes,
/// per the SkinBlender chain documented in
/// <c>docs/workflows/material-pipeline/skintone-and-overlay-compositor.md</c> and reflected in
/// <c>docs/references/external/TS4SimRipper/src/SkinBlender.cs</c> lines 46-321.
/// <para/>
/// Pipeline implemented in this packet (subset of full SkinBlender chain):
/// <list type="number">
///   <item>Build details bitmap from the hardcoded per-(age × gender) neutral detail texture
///         (and overlay row at <c>+4</c> for adult / elder), drawn at the same canvas size.
///         Per-physique alpha-weighted blend is NOT applied yet — neutral details only.</item>
///   <item>Build skin bitmap from <c>tone.SkinSets[0].TextureInstance</c>.</item>
///   <item>Resize details to skin dimensions (SkinBlender resizes skin to details, but for our
///         atlas the skin is the canonical sampling target).</item>
///   <item>Per-pixel <c>Pass 1</c> soft-light + <c>×1.2</c> brighten + clamp; <c>Pass 2</c>
///         overlay-blend; mix by <paramref name="pass2Opacity"/> (the caller's
///         <c>tone.Opacity / 100</c> approximation); contrast-around-midpoint adjust.</item>
///   <item>Draw <c>tone.OverlayList[ageGender]</c> face overlay on top of the composited skin.
///         </item>
///   <item>Draw face CAS overlay textures (EyeColor, Brows, Lipstick, Eyeshadow, Eyeliner,
///         Blush) resolved from the Sim's equipped CAS parts (bt=4, 14, 29-35).</item>
/// </list>
/// HeadMouthColor was previously bundled per TS4SimRipper SkinBlender.cs:314-316 but is no
/// longer used: the eye iris and mouth interior come from the EyeColor mesh (bt=4) and the
/// in-mouth mesh, so the bundled overlay was redundant. Removed in build 0235 along with
/// the embedded PNG and loader code (see Plan J).
/// Skipped on purpose (future packets):
/// <list type="bullet">
///   <item>Per-physique-weight blends of detail rows 1..4 (heavy/fit/lean/bony).</item>
///   <item>Hue/Saturation Pass 3 from tone.Hue / tone.Saturation.</item>
/// </list>
/// All pixel decoding uses <see cref="BitmapAlphaMode.Straight"/> to match SkinBlender's
/// System.Drawing.Bitmap defaults; using premultiplied alpha here would silently scale RGB
/// down by alpha and produce wrong colours when alpha encodes region masks.
/// </summary>
internal static class SimSkinAtlasComposer
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
        CancellationToken cancellationToken)
    {
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

        // 1. Build the details canvas. Start with the neutral details, then draw the overlay
        //    row on top (when present, e.g. adult / elder). Both are resized to (width × height)
        //    to match the skin's canonical sampling space.
        byte[]? detailsPixels = null;
        if (detailNeutralPng is { Length: > 0 })
        {
            var neutral = await DecodeBgra8StraightAsync(detailNeutralPng, cancellationToken, width, height).ConfigureAwait(false);
            if (neutral is not null)
            {
                detailsPixels = neutral.Value.Pixels;
            }
        }
        if (detailOverlayPng is { Length: > 0 })
        {
            var overlay = await DecodeBgra8StraightAsync(detailOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
            if (overlay is not null)
            {
                if (detailsPixels is null)
                {
                    detailsPixels = overlay.Value.Pixels;
                }
                else
                {
                    BlendStraightAlphaOver(detailsPixels, overlay.Value.Pixels);
                }
            }
        }

        // 2. Pass 1 soft-light + Pass 2 overlay-blend the details onto the skin (per-channel
        //    per-pixel). When details aren't available, skip and use the bare base skin.
        if (detailsPixels is not null && detailsPixels.Length == skinPixels.Length)
        {
            var pass2 = Math.Clamp(pass2Opacity, 0f, 1f);
            const float contrast = 1.1f;
            const float midpoint = 0.75f;
            // Pass 3: per `DisplayableSkintone` in
            // docs/references/external/TS4SimRipper/src/SkinBlender.cs:239-264, the tone's hue
            // is converted to an RGB at fixed mid-saturation (127) and mid-luminance (127),
            // and overFactor is `tone.Saturation / 100` — INTEGER division in the reference,
            // which we replicate literally. For tones with Saturation < 100 this means
            // overFactor = 0 and Pass 3 is effectively a no-op; tones with Saturation >= 100
            // get a soft-light blend toward the hue tint. The `_37` variant of SkinBlender
            // computes Pass 3 differently (radio-button toggle in the TS4SimRipper UI). We
            // mirror the main path; switching variants would be a separate documented choice.
            var rgbOver = HslMidpointToRgb(skintoneHue);
            var overFactor = (float)(skintoneSaturation / 100); // literal int division
            var pass3Active = skintoneSaturation > 0 && overFactor > 0f;
            for (var i = 0; i < skinPixels.Length; i += 4)
            {
                for (var c = 0; c < 3; c++)
                {
                    var color = skinPixels[i + c];
                    var detail = detailsPixels[i + c];
                    var detF = detail / 255f;
                    var colF = color / 255f;
                    var pass1 = ((1f - 2f * detF) * colF * colF + 2f * detF * colF) * 255f;
                    pass1 = Math.Min(pass1 * 1.2f, 255f);
                    float pass2Result;
                    if (pass1 > 128f)
                    {
                        pass2Result = 255f - ((255f - 2f * (detail - 128f)) * (255f - pass1) / 256f);
                    }
                    else
                    {
                        pass2Result = (2f * detail * pass1) / 256f;
                    }
                    var blended = (pass2Result * pass2) + (pass1 * (1f - pass2));
                    if (pass3Active)
                    {
                        // BGRA pixel layout, RGB rgbOver: B(c=0) -> rgbOver[2], G(c=1) -> rgbOver[1], R(c=2) -> rgbOver[0].
                        var overChannel = rgbOver[2 - c];
                        var pass3 = (blended / 255f) * (blended + ((2f * overChannel) / 255f) * (255f - blended));
                        blended = (pass3 * overFactor) + (blended * (1f - overFactor));
                    }
                    blended = (((blended / 255f) - midpoint) * contrast + midpoint) * 255f;
                    if (blended < 0f) blended = 0f;
                    if (blended > 255f) blended = 255f;
                    skinPixels[i + c] = (byte)blended;
                }
            }
        }

        // 3. Draw the tone face overlay on top of the composited skin. Straight-alpha blend.
        if (faceOverlayPng is { Length: > 0 })
        {
            var faceOverlay = await DecodeBgra8StraightAsync(faceOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
            if (faceOverlay is not null)
            {
                BlendStraightAlphaOver(skinPixels, faceOverlay.Value.Pixels);
            }
        }

        // 4. Draw face CAS overlay textures (EyeColor, Brows) resolved from the Sim's equipped
        //    CAS parts. Blended in body-type order on top of the tone face overlay.
        if (faceCasOverlayPngs is not null)
        {
            foreach (var casOverlayPng in faceCasOverlayPngs)
            {
                if (casOverlayPng is not { Length: > 0 })
                {
                    continue;
                }

                var casOverlay = await DecodeBgra8StraightAsync(casOverlayPng, cancellationToken, width, height).ConfigureAwait(false);
                if (casOverlay is not null)
                {
                    BlendStraightAlphaOver(skinPixels, casOverlay.Value.Pixels);
                }
            }
        }

        return await EncodeBgra8AsPngAsync(width, height, skinPixels, cancellationToken).ConfigureAwait(false);
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
