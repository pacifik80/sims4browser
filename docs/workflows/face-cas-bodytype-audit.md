# Face CAS body-type pipeline audit (Plan 3.4)

## TL;DR

The original gap-report assumption that face makeup uses `bodyType=15-20` is **wrong**. Real face makeup uses `bodyType=29-35`. All of these are **flat texture overlays** (0 LODs, 1-2 texture references) — they fit the existing skin atlas compositor pipeline. The current code looks at the wrong body types AND iterates the wrong outfit set, so face makeup is never composited today.

## Reproduction

```bash
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- \
    --probe-face-cas-bodytypes
```

Outputs `tmp/face-cas-bodytypes/audit.txt` with per-body-type composition and a name-pattern distribution that maps category names to actual body-type values.

## Real body-type → makeup category mapping (from name-pattern distribution)

| Body type | Real category | Sample CASPart name | Population |
|---:|---|---|---:|
| 29 | **Lipstick** | `yuMakeupLipstick_DollLips_Black` | 519 |
| 30 | **Eyeshadow** | `yuMakeupEyeshadow_ScalesLine_Violet` | 488 |
| 31 | **Eyeliner** (also 3 Mascara) | `yfMakeupEyeliner_ThinAlien_BlueLt` | 161 |
| 32 | **Blush** | `yuMakeupBlush_CheekStripe_Violet` | 88 |
| 34 | **Brow** (a.k.a. Eyebrows / 'Brow' patterns dominant) | `cmMakeupEyebrows_ArchedThick_Black` | 936 |
| 35 | **EyeColor** | `ahMakeupEyeColor_Blue` | 74 |

bt=15..20 are largely **accessory slots** (wrist watches, nose rings, ear rings) with their own geometry — NOT face makeup. The original "bt=15-20 = Eyeliner..." line in the plan was a misunderstanding.

## Composition: all real-makeup body types are texture-only

| BT | LODs | TextureRefs | TgiList types | Verdict |
|---:|---:|---:|---|---|
| 29 (Lipstick) | 0 | 2 | 1 × `RLE2Image`, 1 × `0xBA856C78` (RLES specular) | atlas-compatible |
| 30 (Eyeshadow) | 0 | 1 | 1 × `RLE2Image` | atlas-compatible |
| 31 (Eyeliner) | 0 | 1 | 1 × `RLE2Image` | atlas-compatible |
| 32 (Blush) | 0 | 1 | 1 × `RLE2Image` | atlas-compatible |
| 34 (Brow) | 0 | 1 | 1 × `RLE2Image` | atlas-compatible |
| 35 (EyeColor) | 0 | 2 | 1 × `RLE2Image`, 1 × `0xBA856C78` (RLES specular) | atlas-compatible |

`0xBA856C78` is the RLES (CAS specular) resource type per [docs/shared-ts4-material-texture-pipeline.md:375](../shared-ts4-material-texture-pipeline.md#L375). The specular companion can be ignored for the diffuse-overlay compositing path.

## Why the existing pipeline misses face makeup

Two layered bugs in [AssetServices.cs:1585-1619](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs#L1585):

1. **`IsFaceOverlayBodyType` is too narrow.** Currently:
   ```csharp
   private static bool IsFaceOverlayBodyType(int bodyType) => bodyType is 14;
   ```
   This matches only bt=14, which (per probe) is dominated by accessories not Brows. The real Brows live at bt=34.

2. **Iterates only `GetAuthoritativeBodyDrivingOutfits`.** Probe of test SimInfo `369CA7F9DE882B52` shows face makeup parts are present (bt=29 instance `0x3CCE4`, bt=30 `0x3CA0A`, bt=31 `0x316CB`, bt=32 `0x3C964`, bt=34 `0x8756`, bt=35 `0x2AE9`) but all have `bodyDriving=0`. The current iteration path skips non-body-driving parts entirely, so it would never see face makeup even with a corrected `IsFaceOverlayBodyType`.

## Fix design (Phase 2.4)

Two paired edits in `AssetServices.cs`:

```csharp
private static bool IsFaceOverlayBodyType(int bodyType) =>
    bodyType is 14 or 29 or 30 or 31 or 32 or 34 or 35;

private static string LabelForFaceOverlay(int bodyType) => bodyType switch
{
    4 => "EyeColor",
    14 => "Brows",
    29 => "Lipstick",
    30 => "Eyeshadow",
    31 => "Eyeliner",
    32 => "Blush",
    34 => "Brow",
    35 => "EyeColor",
    _ => $"BodyType{bodyType}"
};
```

And widen the iteration source from `GetAuthoritativeBodyDrivingOutfits` to **all outfit parts** (face makeup is non-body-driving by design):

```csharp
var faceParts = parsedSimInfo.Outfits
    .SelectMany(static outfit => outfit.Parts)
    .Where(static p => IsFaceOverlayBodyType((int)p.BodyType))
    .GroupBy(static p => (p.BodyType, p.PartInstance))
    .Select(static g => g.First())
    .OrderBy(static p => p.BodyType)
    .ToArray();
```

After this, the existing `TryFetchFaceCasOverlayPngAsync` and the skin atlas compositor's face-CAS-overlay drawing pass will pick up Lipstick/Eyeshadow/Eyeliner/Blush/Brow/EyeColor without any further changes — the compositor already iterates `summary.FaceCasOverlayTextures` and blends each as a flat overlay.

## Caveat: bt=4 and bt=14 in code

The current code maps `4 => "EyeColor"` and `14 => "Brows"`. The probe shows that bt=4 and bt=14 are heavily reused for non-face geometry (teeth, accessories). Because the current iteration is restricted to `body-driving outfits`, this rarely hits the wrong slot in practice — but the mapping should be re-validated once non-body-driving iteration is enabled. If bt=14 produces unexpected wrist-watch overlays after the widening, restrict bt=14 to body-driving parts only.

## Status

3.4 closed. Real makeup body types (29-35) identified, all confirmed as flat texture overlays. Fix is a small two-line widening in `AssetServices.cs`. Phase 2.4 is straightforward.
