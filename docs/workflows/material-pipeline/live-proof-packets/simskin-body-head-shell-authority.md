# SimSkin Body/Head Shell Authority

This packet turns the finished `CASPart -> GEOM -> shader family` census into a bounded authority claim for `SimSkin`-anchored body/head shell selection before skintone and compositor refinement.

Question:

- does the current evidence stack justify reading `SimSkin` shell identity as the body/head assembly anchor, with skintone and overlay/compositor layers acting after shell-target selection rather than as alternate shell roots?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Live-Proof Packets](README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimSkin, SimGlass, And SimSkinMask](../family-sheets/simskin-simglass-simskinmask.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Skintone And Overlay Compositor](../skintone-and-overlay-compositor.md)
- [CASPart GEOM Shader Census Baseline](../caspart-geom-shader-census-baseline.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimSkin Body/Head Shell Authority
├─ Externally proved shell-family anchor ~ 90%
├─ Local external snapshot packet ~ 88%
├─ Direct package-derived prevalence floor ~ 95%
├─ Shell-versus-compositor authority reading ~ 76%
└─ Implementation-spec handoff ~ 69%
```

## Externally proved shell-family anchor

What is already strong enough:

- local external [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimSkin = 0x548394B9`
- local external [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) preserves `SimSkin` as the baseline export family
- bundled `.simgeom` resources under [TS4SimRipper Resources](../../../references/external/TS4SimRipper/src/Resources) give the current positive body/head/waist sample packet
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) and [Sims_3:0x015A1849](https://modthesims.info/wiki.php?title=Sims_3%3A0x015A1849) keep `SimSkin` as a distinct lineage family rather than a generic material alias
- creator-facing shell guidance such as [Modifying Sim Appearances](https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/) and [Things made based on "Naked" mesh not showing up?](https://modthesims.info/t/545850) keeps body/head shell selection separate from later layered appearance content

Safe reading:

- `SimSkin` is a real GEOM-side shell-family branch
- body shell and head shell are identity inputs selected before skintone and overlay/detail refinement
- shell-family identity should be preserved in provenance before higher-layer compositor logic is discussed

Unsafe reading:

- do not treat `SimSkin` alone as proof of the full shell material truth stack
- do not treat shell-family identity as if it already proves exact `MTNF` versus `CASP` versus `MATD` ranking
- do not treat skintone or overlay layers as if they can replace shell identity just because they visibly change final output

## Local external snapshot packet

Current strongest local snapshot stack:

- bundled `.simgeom` samples already recorded in the source-map packet as `9/9` checked body/head/waist geometries resolving to `SimSkin`
- [TONE.cs](../../../references/external/TS4SimRipper/src/TONE.cs) surfaces layered skintone structure:
  - `SkinSets`
  - `TextureInstance`
  - `OverlayInstance`
  - `OverlayMultiplier`
  - `Opacity`
  - `OverlayList`
  - `SortOrder`
- [SkinBlender.cs](../../../references/external/TS4SimRipper/src/SkinBlender.cs) shows a multi-pass approximation that combines:
  - body-detail and physique overlays
  - sculpt and outfit overlays
  - `SkinSets[0].TextureInstance`
  - optional tan texture plus skin-set mask from `overlayInstance`
  - second-pass opacity from `tone.Opacity`
  - age/gender overlay instances
  - mouth overlay at the end of the skin pass

Why this packet matters:

- it keeps shell-family geometry identity and compositor layering in the same bounded evidence packet
- it shows that skintone is not a flat tint and not a geometry-family replacement
- it still fits the safe reading that shell-target selection comes first and layered skin passes act on the selected shell-compatible materials

## Direct package-derived prevalence floor

The completed direct character-side family floor in [CASPart GEOM Shader Census Baseline](../caspart-geom-shader-census-baseline.md) now adds whole-install prevalence pressure:

- `RowsWithResolvedGeometryShader = 281271`
- `RowsWithUnknownGeometryShader = 32`
- `GeometryResolvedFromExternalPackage = 12911`
- `SimSkin = 280983` across `401` packages by `CASPart` rows
- `SimGlass = 6048` across `189` packages by `CASPart` rows
- `Phong = 33` across `5` packages by `CASPart` rows
- `SimSkin = 86697` across `147` packages by unique linked `GEOM`
- `SimGlass = 645` across `47` packages by unique linked `GEOM`
- residual geometry tail: `GeometryKeyNotIndexed = 531`

Safe reading:

- `SimSkin` is no longer only a creator-tool hint; it directly dominates the currently resolved character-side geometry floor
- `SimGlass` remains real, but much narrower
- this is strong enough to move shell/compositor authority work ahead of narrower edge-family lanes

Honest limit:

- this is still bounded by the current structured `CASPart` parser gap
- it is not yet a complete whole-raw-row `CASPart` family census

## Direct shell-identity floor

The current shell-side authority reading now also has a narrower direct slot or body-type floor:

- [body_head_shell_authority_snapshot_2026-04-21.json](../../../tmp/body_head_shell_authority_snapshot_2026-04-21.json)
- [sim_archetype_body_shell_audit_fresh.json](../../../tmp/sim_archetype_body_shell_audit_fresh.json)

Current direct `cas_part_facts` floor:

- `Head` stays narrow and exact on the current parsed layer:
  - `body_type = 3`
  - `slot_category = Head`
  - `rows = 90`
  - `composition=0 = 88`
  - `default rows = 4`
  - `naked-link rows = 0`
- the body-driving shell lane stays much broader and still carries the body recipe packet:
  - `Full Body` / `body_type = 5` -> `6276` rows with `naked-link = 3406`
  - `Top` / `body_type = 6` -> `9287` rows with `naked-link = 3715`
  - `Bottom` / `body_type = 7` -> `6191` rows with `naked-link = 2943`
  - `Shoes` / `body_type = 8` -> `2338` rows with `naked-link = 918`

Current graph-backed archetype floor:

- the fresh audit still records `46` direct `Sim archetype` assets:
  - `FullBodyShell = 23`
  - `SplitBodyLayers = 12`
  - `None = 11`
- the same graph-backed packet keeps body-shell assembly concentrated in body recipe layers:
  - `Full Body = 23`
  - `Top = 12`
  - `Bottom = 12`
  - `Shoes = 12`
- the current template-body-part subset also preserves a distinct exact head lane:
  - `Head = 5` rows
  - all `5` current head-bearing template rows are still human `SimInfo template` assets with `authoritative_body_driving_outfit_count = 1`

Safe reading:

- body shell remains the broad assembly anchor on both the parsed `CASPart` floor and the graph-backed archetype floor
- head shell remains exact but much narrower on the current direct layers
- this asymmetry strengthens the current authority reading:
  - body shell anchors assembly
  - head shell merges as a sibling branch
  - post-selection skintone or overlay layers should not be described as alternate shell roots

## Current safest authority reading

The current safest synthesis is:

```text
selected body/head CASP
        ->
linked GEOM preserving shell-family identity
        ->
embedded MTNF / explicit MATD-MTST / parsed CASP field-routing candidates
        ->
shell-scoped region-map and skintone-compatible target selection
        ->
skintone and overlay/detail compositor passes
        ->
final shared canonical material output
```

What this means right now:

- body shell is the assembly anchor
- head shell is a mergeable sibling branch, not a replacement for the body anchor
- `SimSkin` identity belongs to the shell side of the authority stack, before skintone and overlay/detail passes
- current direct floors now also show why that order is safer:
  - body-driving shell rows are broad and naked-link-heavy
  - head rows are narrower and exact-slot-like
- skintone and overlay/detail families should be read as compositor-driven refinement on selected shell targets, not as alternate geometry roots
- this packet is about authority order and provenance, not about inventing a shell-only renderer

## What this packet proves

- `SimSkin` now has enough external plus package-derived support to anchor body/head shell authority work
- shell identity and skintone/compositor layering are separate steps in the current safest model
- the next implementation-spec packet should start from body/head shell authority instead of from narrower `SimGlass` or `SimSkinMask` lanes

## What this packet does not prove

- exact universal runtime ranking between embedded `MTNF`, parsed `CASP` routing, and explicit material definitions
- exact `CompositionMethod` or `SortLayer` math
- exact patch- or occult-specific shell exceptions
- full whole-raw-row family closure for the `230713` `CASPart` rows still outside the current structured parser

## Current implementation boundary

Current repo behavior is diagnostic only:

- current character assembly already treats body shell as the assembly anchor and head shell as a merge branch
- current skintone handling is region-aware post-selection routing, not full in-game compositor parity
- current repo code still cannot be described as a full proof of exact shell material truth ordering

Useful implementation anchors:

- [AssetServices.cs](../../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [SimSceneComposer.cs](../../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

## Best next inspection step

1. Keep the body-shell versus head-shell authority table restart-safe with the new direct shell floor instead of reopening narrower family lanes first.
2. Follow it with the next compositor-order packet focused on `CompositionMethod`, `SortLayer`, or equivalent overlay/detail precedence.
3. Keep wider `SimSkinMask` counterexample search bounded as a secondary branch rather than the main queue driver.
