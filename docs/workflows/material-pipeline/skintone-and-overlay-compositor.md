# Skintone And Overlay Compositor

This document is the deep-dive companion for the shared TS4 material guide when the question is specifically about `Skintone`, `overlay/detail` families, `CompositionMethod`, `SortLayer`, or the boundary between region-aware skintone routing and exact in-game blend math.

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Workflows index](../README.md)
- [Material pipeline deep dives](README.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Skintone And Overlay Compositor
├─ TONE/skintone structural model ~ 82%
├─ Overlay/detail family boundary ~ 74%
├─ Region-map-aware skintone routing in current repo ~ 86%
├─ Tan/burn/underlayer evidence ~ 68%
├─ CompositionMethod / SortLayer practical role ~ 58%
└─ Exact in-game blend math ~ 38%
```

What this doc is for:

- isolate the densest skintone/compositor knowledge from the main shared guide
- separate `routing/target selection` from `exact blend math`
- keep one place where repo code, creator tooling, and reverse-engineered code all describe the same boundary

What this doc is not:

- not a claim that full in-game compositor parity is already solved
- not a replacement for the cross-domain shared pipeline guide

## Current safest structural model

The strongest current synthesis is:

```text
selected CAS or Sim material candidates
        ->
region-aware target selection
        ->
skintone base / skin-set texture
        +
body definition/details texture
        +
optional skin-set overlay / mask
        +
optional age-gated overlay images
        +
optional tan / burn variants and burn-mask behavior
        ->
post-selection CAS overlay/detail families
   (skin details, face paint, tattoos, makeup, similar CASPart layers)
        ->
final compositor output
```

What is already strong enough:

- `Skintone` is not a single flat color record; community tooling and reverse-engineered code both show skin-set textures, overlay instances, opacity, and multipliers
- `overlay/detail` families are not separate geometry roots; they are compositor-driven layers attached through selected `CASP` parts and body-type slots
- current repo code already treats skintone as a post-selection routing/apply pass, not as the thing that replaces the main material graph

What is still open:

- exact universal in-game blend order across all patches and families
- exact numeric math for every pass
- exact interaction between `CompositionMethod`, `SortLayer`, and skintone-family passes

## Structural evidence from tooling and code

### `TONE` is structurally layered

Local external code in [TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs) shows:

- multiple `SkinSets`
- `TextureInstance`
- `OverlayInstance`
- `OverlayMultiplier`
- `Opacity`
- `OverlayList`
- per-overlay age/gender flags

This is enough to treat the TS4 skintone model as layered and stateful rather than a single diffuse-like asset.

Source pointers:

- [TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs)
- [TS4 Skininator](https://modthesims.info/d/568474)
- [TS4 Skin Converter V2.3](https://modthesims.info/d/650407/ts4-skin-converter-v2-enable-cc-skintones-in-cas.html)

### `SkinBlender` shows an actual multi-pass approximation

Local external code in [SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs) explicitly combines:

- skin color/base texture
- body details
- physique overlays
- skin-set overlay or mask
- hue/saturation overlay color
- second-pass opacity
- age/gender overlay instances
- mouth overlay
- sculpt/outfit overlays

This is not authoritative EA source, but it is strong `reference-code-backed` evidence that mature community tooling converged on a genuinely multi-pass compositor model.

Especially useful:

- soft-light-like first pass over body details
- overlay-like second pass blended by opacity
- extra overlay-color pass
- age/gender overlay application at the end of the skin pass

Source pointers:

- [SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs)
- [Creating a custom skintone with TS4 Skininator](https://modthesims.info/t/568713)

## Current repo boundary

### What the repo already does

Current repo code already resolves a real `Skintone` resource and builds `SimSkintoneRenderSummary` in:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)

Current repo code already performs region-aware skintone routing in:

- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)

The current routing boundary is:

- parse selected `Skintone`
- resolve region-map-aware material targets
- only route materials that already look skintone-compatible
- apply route notes/tint metadata to merged canonical materials

### What the repo does not do yet

The repo does not yet implement:

- full exact `TONE` blend math
- `CompositionMethod`-driven compositor behavior
- `SortLayer`-driven final ordering
- an exact tan/burn/underlayer simulator

So the current repo model is best described as:

- `resolved skintone routing`
- not `full skintone compositor parity`

### Calibration: what the `details` input actually is

A previous implementation packet incorrectly assumed that the **CAS body part's diffuse texture** (e.g. `yfTop_Nude` / `ymBottom_Nude` / `ymShoes_Nude` from a body-driving Nude outfit) is the `details` input to SkinBlender's Pass 1. It is **not**. Reading [SkinBlender.cs:18-43](../../references/external/TS4SimRipper/src/SkinBlender.cs#L18-L43) and the `DisplayableSkintone(...)` flow:

```csharp
static ulong[][] detailInstance = new ulong[][] { ... };
...
Bitmap details = FetchGameTexture(detailInstance[skinIndex][0], -1, ref errorList, false);
```

The details come from a **hardcoded `detailInstance` TGI table** keyed by age × gender × physique-channel. For example the Young Adult Female row is:

```
{ 0x36C865290B1F4E79, 0x5A0156E11FEB7ED1, 0x980C64FFD5139131, 0x1A2B3CB6DF532C84, 0x95006DB72556DFEA }
```

These are purpose-built per-physique skin detail maps (neutral, heavy, fit, lean, bony), not anything resolved from the selected `CASPart` or its `MATD`. Adult/elder paths additionally apply a per-age-gender overlay row at index `skinIndex + 4`.

**Soft-light blending the CASPart body diffuse onto the base skin therefore contradicts the authoritative chain.** Symptoms observed when the wrong input was used: the composite picks up the CAS body diffuse's region-mask data (often green-encoded) and the rendered skin came out green-tinted.

The correct behaviour for a faithful preview compositor is:

1. Look up the age/gender row in the `detailInstance` table → resolve `detailInstance[ageGenderIndex][0]` (neutral) as the base detail bitmap.
2. Optionally overlay the matching row at `+4` for adult/elder.
3. Apply per-physique alpha blends from `physiqueWeights` (0..4) over the detail base — both for the per-physique detail (`detailInstance[skinIndex][1..4]`) and the per-physique overlay (`detailInstance[skinIndex+4][1..4]`).
4. Composite that resulting `details` bitmap onto the base skin texture (`tone.SkinSets[0].TextureInstance`) using SkinBlender Pass 1 + Pass 2.

The CAS body part's own diffuse texture has a different role in the in-game shader (region-mask / shape-detail / clothing texture for non-nude outfits), which is not yet documented here.

Until the proper `detailInstance` resolution is implemented, the preview should render body-shell materials with the **base skin texture alone**. That is structurally equivalent to SkinBlender with no details applied, and is the honest fallback.

### Authoritative SkinBlender chain (end-to-end reading)

This section captures what [`SkinBlender.cs`](../../references/external/TS4SimRipper/src/SkinBlender.cs#L46-L321) actually does in the `DisplayableSkintone(...)` flow, read end-to-end. It is the closest authoritative reference the project has for skin compositing, and it is what the renderer should converge on rather than guess from heuristics.

Inputs:

- `tone` — the parsed `Skintone` (`TONE` resource).
- `shift` — `SkintoneShift` from `SimInfo`.
- `skinState` — runtime tan/burn state index.
- `tanLines`, `sculptOverlay`, `outfitOverlay` — runtime overlays.
- `age`, `gender`, `physiqueWeights`, `pregnantShape`.

Step-by-step:

1. **Build a "details" texture** by compositing per-physique body-detail textures (table indexed by `age × gender`, hard-coded TGIs in `detailInstance` at the top of `SkinBlender.cs`) into a single detail layer:
    - Base detail at `physiqueWeights[0]`.
    - Per-physique overlays for heavy/fit/lean/bony, each scaled by its physique weight.
    - Adult/elder paths add a base male/female overlay first; child/baby paths skip overlays.
2. **Composite sculpt overlay** onto details, then outfit overlay on top.
3. **Build a "skin" texture** from `TONE`:
    - Start from `tone.SkinSets[0].TextureInstance` — **this is the actual base skin color/diffuse texture** (an indexed `_IMG`, not a flat color).
    - If `currentSkinSet > 0`, draw `tone.SkinSets[currentSkinSet].TextureInstance` (tan/burn state) on top, optionally masked by `tanLines`.
    - Draw `tone.SkinSets[currentSkinSet].overlayInstance` with a 0.15-alpha mask matrix.
    - If `Math.Abs(shift) > 0.001`, apply hue shift to the entire skin texture.
4. **Resize details to match skin dimensions if they differ.**
5. **Pixel-by-pixel composite (per-RGB-channel)** of details onto skin, with three selectable blend modes (radio buttons in the tool):
    - **Soft-light + overlay (`Blend1`, default)**:
      - Pass 1: `softlight(detail, color)` lightened by ×1.2.
      - Pass 2: `overlay(detail, softlit)`.
      - Mix Pass 1 vs Pass 2 by `pass2opacity = tone.Opacity / 100f`.
      - Optional Pass 3: soft-light overlay using `tone.Hue` + `tone.Saturation` (HSL → RGB), mixed by `overFactor = saturation/100`.
      - Final contrast adjust around midpoint 0.75 with contrast 1.1.
    - **HSV blend (`Blend2`)**: replace skin's V with `(skinV + (detailV - 0.40f) + shift)`; optional H replace from `tone.Hue`.
    - **HSL blend (`Blend3`)**: replace skin's L with `skinL + ((detailL - 0.40f) * 0.60f) + shift`; optional H replace.
6. **Add age/gender overlay** from `tone.GetOverlayInstance(age & gender)` (a per-age/gender chosen overlay from `tone.OverlayList`).
7. **Add fixed mouth overlay** (resource bundled in the tool) for the head pass.

Implications for our renderer:

- The "skin diffuse the player sees" comes from `tone.SkinSets[0].TextureInstance` (a `_IMG` resource), **not** from `tone.SwatchColors[0]`.
  - Our `Ts4Skintone.BaseTextureInstance` is the same value (set via `tmpInstance = br.ReadUInt64()` in TONE v6).
  - `tone.SwatchColors` in v10+ is `colorList[]` — the **CAS UI swatch palette markers**, used to render swatch chips in the skin-tone picker. They are not the actual skin diffuse.
- The **CASPart's diffuse texture** referenced by a body-shell `MATD` is a **detail/region layer** (per-physique body details, body region masks, etc.), **not** a portable diffuse color. It is the `details` input to the SkinBlender, not the `color` input.
- `tone.Hue` / `tone.Saturation` / `tone.Opacity` are blend-shape parameters of the compositor, not standalone tints. Reading any of them as ARGB always gives garbage.
- The age/gender overlay path is real and per-Sim — a flat skin tint cannot reproduce it.

### Why current preview tints are misleading

The repo's current `TryBuildSkintoneViewportTintColor` synthesizes a `ViewportTintColor` from `swatchColors[0]` (or a hardcoded fallback when no swatches exist) and the renderer multiplies that color into either a flat-shaded mesh or the CASPart's diffuse texture. Both forms are wrong vs. SkinBlender:

- `swatchColors[0]` is a UI palette anchor, often a stylised display color (light cream, dark brown, purple alien, etc.). It is not what the in-game shader uses to paint skin.
- Multiplying CASPart diffuse by any color produces `details × tint`, never `softlight(details, baseSkinTexture)`. The output is structurally not skin.
- The "green skin" + "no body" + "warm-cream-tinted body" oscillation in builds 0190–0200 was caused by this entire stack of heuristics colliding with each other across multiple layers.

The cleanest re-ground for our preview is:

1. Stop synthesizing a `ViewportTintColor` from `swatchColors[0]`; treat `Ts4Skintone.BaseTextureInstance` as the authoritative skin source.
2. For body-shell-contribution materials, use the **resolved base skin texture** (the `_IMG` at `tone.SkinSets[0].TextureInstance`) as `DiffuseMap`, not the CASPart's diffuse.
3. Optionally, blend the CASPart's diffuse on top as a soft-light or HSL details layer to recover muscle/fold detail. Until that compositor is built, show base skin alone — it is structurally correct, even if flat.
4. Head materials remain separate: the head's CASPart diffuse already includes face detail in a portable form (eyes, lips), so it can stay as the primary diffuse. The skintone's age/gender overlay is the proper place to drape additional skin tone over the face later.
5. `swatchColors[0]` and friends should be retained only as a **palette/identity hint** for diagnostics, not piped through to the renderer.

This shape is faithful to `SkinBlender.cs` while still being a reduced preview, and it clearly identifies where future packets (overlay compositor, multi-state tan/burn, age/gender overlay, exact blend math) plug in.

## Overlay/detail family boundary

The strongest current reading is:

- `skin details`
- `makeup`
- `tattoos`
- `face paint`
- similar face/body CAS layers

all belong to compositor-driven `CASPart` families, not to independent geometry-family roots.

Why this is now strong enough:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/) separates `CAS Parts` and `Skin Tones` as different appearance categories
- the same source shows outfit/appearance data carrying `part_shift` for custom opacity/hue-like behavior and `layer_id` for later layered content
- current repo docs already separated these families from shells and worn-slot identity
- current repo material decoding already normalizes many overlay-like slot names into shared semantic vocabulary

Useful compact sources:

- [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
- [Please explain Skin Overlay, Skin Mask, Normal Skin, Etc](https://modthesims.info/t/594620)
- [TS4 Skininator](https://modthesims.info/d/568474)

Current safe rule:

- keep overlay/detail families inside compositor authority
- do not promote them into separate geometry authority branches
- keep them separate from shell authority and from hair/accessory/footwear authority

## Tan, burn, underlayers, and chest overlays

This area is still partial, but it is no longer vague.

What is now well supported:

- modern skintones can carry multiple skin states such as normal, tanned, and burned
- burn masks are a distinct layer family
- skin definitions and overlays were patched over time to include new chest/breast overlay resources and underlayer concerns
- darker skins can show visible failure modes when overlay/burn behavior is wrong

Corroboration:

- [TS4 Skininator](https://modthesims.info/d/568474)
- [TS4 Skin Converter, version 1.2](https://modthesims.info/d/629700/ts4-skin-converter-version-1-2-7-8-2019-now-obsolete.html)
- [Extracted body skin textures and TGI's](https://modthesims.info/t/534912)

Current safe reading:

- tan/burn state is part of the skintone compositor branch, not a separate material family
- burn masks and underlayers can materially change visible output
- this branch should stay distinct from generic `CASPart overlay/detail` logic even though both are compositing layers

## `CompositionMethod` and `SortLayer`

What is currently strong enough:

- both are real CAS compositor-facing inputs
- they belong on the overlay/detail side of the model, not on the shell identity side
- creator-facing practice and current repo docs both treat them as ordering/compositing controls

What is still weak:

- current repo code does not yet enforce exact `CompositionMethod` math
- current repo code surfaces `SortLayer`, but does not yet drive final compositor order with it
- category-specific exceptions are still not fully tabled

This means the honest current repo boundary is:

- `CompositionMethod` and `SortLayer` are structurally authoritative metadata
- but their exact runtime effect is still only partially modeled

## Best compact evidence packet

If a later narrow task needs the smallest useful source pack, start here:

1. [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/)
2. [TS4 Skininator](https://modthesims.info/d/568474)
3. [Creating a custom skintone with TS4 Skininator](https://modthesims.info/t/568713)
4. [TS4 Skin Converter V2.3](https://modthesims.info/d/650407/ts4-skin-converter-v2-enable-cc-skintones-in-cas.html)
5. [TONE.cs](../../references/external/TS4SimRipper/src/TONE.cs)
6. [SkinBlender.cs](../../references/external/TS4SimRipper/src/SkinBlender.cs)

## What still remains open

The remaining gaps are narrow:

- exact in-game blend order for skin-set texture, body detail, overlay instance, overlay color, and age/gender overlays
- exact meaning of the skintone opacity and overlay-multiplier fields in every state
- exact universal ranking between skintone-family layers and overlay/detail `CASPart` layers
- exact `CompositionMethod` and `SortLayer` math in the live game
- exact patch- and occult-specific exceptions

## Recommended next work

The next strong follow-up packet should be one of:

- a `CompositionMethod / SortLayer` live-asset table
- a `tan/burn/underlayer` fixture pack
- a direct repo-side `TONE` parser and staged skintone compositor prototype
