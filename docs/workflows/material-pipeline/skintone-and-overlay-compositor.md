# Skintone And Overlay Compositor

This document is the deep-dive companion for the shared TS4 material guide when the question is specifically about `Skintone`, `overlay/detail` families, `CompositionMethod`, `SortLayer`, or the boundary between region-aware skintone routing and exact in-game blend math.

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Workflows index](../README.md)
- [Material pipeline deep dives](README.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](body-head-shell-authority-table.md)
- [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md)
- [CompositionMethod And SortLayer Boundary](compositionmethod-sortlayer-boundary.md)
- [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md)
- [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md)
- [SortLayer Census Baseline](sortlayer-census-baseline.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Skintone And Overlay Compositor
├─ TONE/skintone structural model ~ 82%
├─ Overlay/detail family boundary ~ 80%
├─ Region-map-aware skintone routing in current repo ~ 86%
├─ Tan/burn/underlayer evidence ~ 68%
├─ CompositionMethod / SortLayer practical role ~ 66%
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
- use [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md) when the question is about worn-slot identity rather than compositor math

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
- `sort_layer` now also has a direct whole-index counted layer, rather than only creator-facing and reference-code support
- the new packet [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) now also keeps low-value overlay rows separate from the closed high-byte family stack

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

- use [Overlay And Detail Family Authority Table](overlay-detail-family-authority-table.md) as the current explicit ranking baseline
- use [SortLayer Census Baseline](sortlayer-census-baseline.md) for the current direct counted `sort_layer` layer
- use [Hair, Accessory, And Shoes Authority Table](hair-accessory-shoes-authority-table.md) to keep skintone routing bounded away from worn-slot identity
- then return to [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- use [Overlay-Detail Priority After High-Byte Stack](live-proof-packets/overlay-detail-priority-after-highbyte-stack.md) as the current restart-safe Tier A precedence packet
- a `tan/burn/underlayer` fixture pack
- a direct repo-side `TONE` parser and staged skintone compositor prototype
