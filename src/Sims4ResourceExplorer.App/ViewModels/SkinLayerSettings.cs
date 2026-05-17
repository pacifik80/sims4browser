namespace Sims4ResourceExplorer.App.ViewModels;

/// <summary>
/// Per-skin-layer customization for the Sim Character Constructor. Each alpha is
/// applied at the corresponding blend site in <see cref="SimSkinAtlasComposer"/>;
/// 0 effectively disables the layer, 1 reproduces the default behavior. The face
/// CAS slot configs are layered on top of the synthesised SimInfo's own face-CAS
/// resolution (which is empty for our synthesised Sims today — so these are how
/// the user actually adds Lipstick / Eyeshadow / etc).
/// </summary>
public sealed record SkinLayerSettings
{
    public float BaseSkinAlpha        { get; init; } = 1f;
    public float DetailNeutralAlpha   { get; init; } = 1f;
    public float DetailOverlayAlpha   { get; init; } = 1f;
    public float Pass3HueAlpha        { get; init; } = 1f;
    public float ToneFaceOverlayAlpha { get; init; } = 1f;
    public FaceCasSlotConfig EyeColor  { get; init; } = FaceCasSlotConfig.None;
    public FaceCasSlotConfig Brows     { get; init; } = FaceCasSlotConfig.None;
    public FaceCasSlotConfig Lipstick  { get; init; } = FaceCasSlotConfig.None;
    public FaceCasSlotConfig Eyeshadow { get; init; } = FaceCasSlotConfig.None;
    public FaceCasSlotConfig Eyeliner  { get; init; } = FaceCasSlotConfig.None;
    public FaceCasSlotConfig Blush     { get; init; } = FaceCasSlotConfig.None;

    /// <summary>
    /// Deterministic fingerprint of the settings — used as part of the scene-cache
    /// key so cached scenes built with the same layer settings short-circuit and
    /// scenes built with different settings stay distinct.
    /// </summary>
    public long Fingerprint()
    {
        unchecked
        {
            long h = 17;
            h = h * 31 + System.BitConverter.SingleToInt32Bits(BaseSkinAlpha);
            h = h * 31 + System.BitConverter.SingleToInt32Bits(DetailNeutralAlpha);
            h = h * 31 + System.BitConverter.SingleToInt32Bits(DetailOverlayAlpha);
            h = h * 31 + System.BitConverter.SingleToInt32Bits(Pass3HueAlpha);
            h = h * 31 + System.BitConverter.SingleToInt32Bits(ToneFaceOverlayAlpha);
            h = h * 31 + EyeColor.Fingerprint();
            h = h * 31 + Brows.Fingerprint();
            h = h * 31 + Lipstick.Fingerprint();
            h = h * 31 + Eyeshadow.Fingerprint();
            h = h * 31 + Eyeliner.Fingerprint();
            h = h * 31 + Blush.Fingerprint();
            return h;
        }
    }

    public bool IsDefault() =>
        BaseSkinAlpha == 1f &&
        DetailNeutralAlpha == 1f &&
        DetailOverlayAlpha == 1f &&
        Pass3HueAlpha == 1f &&
        ToneFaceOverlayAlpha == 1f &&
        EyeColor.IsNone() &&
        Brows.IsNone() &&
        Lipstick.IsNone() &&
        Eyeshadow.IsNone() &&
        Eyeliner.IsNone() &&
        Blush.IsNone();
}

public sealed record FaceCasSlotConfig(ulong? CasPartInstance, float Alpha)
{
    public static FaceCasSlotConfig None { get; } = new(null, 1f);

    public bool IsNone() => CasPartInstance is null or 0ul;

    public long Fingerprint() =>
        unchecked((long)((CasPartInstance ?? 0ul) * 31) + System.BitConverter.SingleToInt32Bits(Alpha));
}

public sealed record FaceCasOption(ulong CasPartInstance, string DisplayName, string PackagePath, int BodyType)
{
    public static FaceCasOption NoneSentinel(int bodyType) =>
        new(0ul, "(None)", string.Empty, bodyType);

    public bool IsNone => CasPartInstance == 0ul;
}
