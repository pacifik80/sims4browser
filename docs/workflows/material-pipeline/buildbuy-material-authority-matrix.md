# Build/Buy Material Authority Matrix

This document is the detailed companion for the shared TS4 material guide when the task is specifically about `Build/Buy` material authority, object-side scene roots, `COBJ/OBJD -> MODL/MLOD -> MTST/MATD` linkage, or the boundary between base surface-material authority and parallel helper branches such as `LITE`, cutouts, and linked-object resources.

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Workflows index](../README.md)
- [Material pipeline deep dives](README.md)
- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md)
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [Package-Material Pass Filtering Contract](package-material-pass-filtering-contract.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Build/Buy object pipeline](../../references/codex-wiki/02-pipelines/01-buildbuy-object-pipeline.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Build/Buy Material Authority Matrix
├─ Base authority graph ~ 94%
├─ Swatch and variant seam ~ 84%
├─ Static object family baseline ~ 88%
├─ Stateful/material-set family boundary ~ 80%
├─ Light parallel branch ~ 74%
├─ Linked-object/helper boundary ~ 62%
├─ Edge-family fixture integration ~ 89%
└─ Full family-specific authority order ~ 66%
```

What this doc is for:

- keep the `Build/Buy` authority topic out of the main shared guide
- preserve one place where object-side external references, creator archaeology, live fixtures, and current implementation boundaries are aligned without being mixed into `CAS/Sim`
- make later `Build/Buy` family passes incremental instead of rediscovering the same base chain
- document how `Build/Buy` chooses authoritative material inputs before those inputs enter the shared shader/material pipeline

What this doc is not:

- not a claim that every `Build/Buy` family already has a closed authority table
- not a replacement for the shared cross-domain material guide
- not a definition of `BuildBuy`-specific shader semantics after canonical-material decoding begins

## Current strongest Build/Buy authority graph

```text
Object Catalog / Object Definition
               ->
         Model root (MODL)
               ->
        Model LOD packet (MLOD)
               ->
mesh-group linkage to VRTF / VBUF / IBUF / SKIN / MATD-or-MTST
                                 ->
                  Material Set / Material Definition
                                 ->
                canonical material candidates
                                 ->
                shared shader/material contract
```

Parallel branches that may coexist with, but do not replace, the base surface-material chain:

- `LITE` for light or emitter behavior
- cutout resources for architectural openings and thumbnail cutouts
- slot, footprint, rig, and result helpers for placement/interaction
- `VPXY` and related linked-object helpers where the object ecosystem actually uses them

Current safe architectural rule:

- `Build/Buy` discovery is object-first, not texture-first
- the object-side chain chooses authoritative material candidates before any shared shader decoding begins
- helper resources can modify or accompany the object, but they should not replace the `OBJD -> MODL/MLOD -> MTST/MATD` surface-material chain without stronger proof
- after authoritative inputs are chosen, `Build/Buy` still feeds the same shared shader/material contract as `CAS` and `Sim`

Current downstream filter rule:

- once `Build/Buy` package-side candidates exist, external GPU pass evidence should filter them through:
  - `SceneDomain = WorldRoom`
  - `PassClass = MaterialLike`
- `CompositorOrUi` and `DepthOnly` should currently be treated as exclusion/helper evidence, not final visible ownership
- the current browser-facing contract for that layer now lives in:
  - [Package-Material Pass Filtering Contract](package-material-pass-filtering-contract.md)

## Evidence order for this matrix

Use these sources in this order:

1. `The Sims 4 Modders Reference` for resource identity and high-level file roles
2. `Mod The Sims / SimsWiki` chunk pages for `RCOL`, `MLOD`, `LITE`, and related object-side archaeology
3. community object/material-variant guidance for the `OBJD` swatch/variant seam
4. local live-proof packets only as fixture-backed examples of the already-proved authority chain
5. current repo code only as implementation boundary

Safe rule:

- if a statement depends mainly on an object-side fixture, phrase it as fixture-backed authority evidence
- do not turn a `Build/Buy` discovery seam into a `BuildBuy`-specific shader rule

## Base authority contract

| Layer | Current strongest reading | Current confidence |
| --- | --- | --- |
| `COBJ` + `OBJD` | same-instance swatch-level object identity; `COBJ` is catalogue-facing, `OBJD` is object-definition/linkage-facing | high |
| `OBJD -> Model` | `OBJD` carries the object-side link to the model root and other direct object companions such as `Rig`, `Slot`, and `Footprint` | high |
| `MODL -> MLOD` | `MODL` is the object geometry root and leads into one or more `MLOD` packets | high |
| `MLOD` mesh-group mapping | `MLOD` is the key object-side linkage layer mapping mesh groups to `VRTF`, `VBUF`, `IBUF`, `SKIN`, and `MATD` or `MTST` | high |
| `MTST` | stateful/material-variant carrier that groups material definitions; stronger whenever the object family is swatch- or state-driven rather than one fixed material packet | medium-high |
| `MATD` | per-material definition input inside the object-side chain | high |
| `LITE` | parallel light/emitter branch; lighting behavior, not replacement for the base surface-material chain | medium-high |
| `VPXY` and similar helpers | bounded linked-object or model-link helper branch; not currently strong enough to outrank the base surface-material chain | medium-low |

## Swatch and variant seam

What is already strong enough:

- `COBJ` and `OBJD` share the same instance and together represent one object swatch or catalogue instance
- `OBJD` carries the object-facing linkage side, while `COBJ` stays catalogue-facing
- community object-override and recolor guidance is consistent that `MaterialVariant` in `OBJD` selects into object-side material entries rather than bypassing the model chain
- the current safest object-side reading is:
  - `COBJ/OBJD` choose the swatch or instance
  - `OBJD` points to the object model root
  - `MaterialVariant` selects the relevant material entry or set inside the object model chain

External corroboration:

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120)
- [EA forum thread on object definition and material variant linkage](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695)
- [Re-texturing in game objects with s4pe](https://modthesims.info/t/538657)

Current safe reading:

- swatch identity starts at `COBJ/OBJD`, not at a loose texture resource
- `MaterialVariant` is a selector inside the object-side model/material chain, not a reason to skip directly to package-local textures
- exact state and swatch behavior may still diverge by family, but the discovery root is already narrow enough to keep stable

## Family split

The base authority path is already strong enough to keep the following `Build/Buy` family groups separate at the discovery/authority layer:

| Family group | Current strongest authority reading | Current confidence |
| --- | --- | --- |
| static furniture and decor | `COBJ/OBJD -> MODL -> MLOD -> MATD or MTST`, with ordinary surface-material decoding after that | high |
| swatch-heavy or stateful objects | same base path, but `MTST` and variant selection are materially stronger and should not be collapsed into one anonymous `MATD` assumption | medium-high |
| glass-bearing transparent objects | same base object/material path, but preserve object-side glass family semantics such as `GlassForObjectsTranslucent` instead of relabeling them as character-side `SimGlass` or generic alpha fallback | medium |
| cutout or blended transparent objects | same base object/material path, but keep threshold/cutout helpers and `AlphaBlended`-style routes separate from object-glass semantics | medium |
| lights and emissive fixtures | same base surface-material path plus a parallel `LITE` branch for lighting behavior | medium-high |
| cutout-bearing architectural objects | same base surface-material path, plus cutout/model-cutout helpers for openings or thumbnails | medium |
| linked or modular object ecosystems | same base object/material path, but may also require `Object Catalog Set`, `Magalog`, `Modular Piece`, `Window Set`, or bounded `VPXY`/helper traversal for full scene reconstruction | medium-low |
| edge-family-bearing objects | same base object/material path, but shader-family semantics stay in the family and live-proof docs rather than this matrix | medium |

## Static object family baseline

What is now strong enough:

- for ordinary static decor/furniture objects, the safest material path is still object-first and model-first
- `MLOD` is the key mesh-group seam, because it maps each group to the render-bearing data and to `MATD` or `MTST`
- package-local texture fallback is only safe after explicit object linkage and material decoding fail
- creator-facing object workflows now also support a narrower split inside transparent object families:
  - object-glass routes such as `GlassForObjectsTranslucent`
  - threshold/cutout helper routes such as `AlphaMap` plus `AlphaMaskThreshold`
  - blended object routes such as `AlphaBlended`
  - architectural windows/doors/archways may also require explicit `Model Cutout` and `Cut Info Table` handling

Current safe reading:

- do not browse textures first and then guess the object family
- do not treat root/header lists or package-local image presence as object authority
- static `Build/Buy` remains the strongest current baseline for the whole repo because the object-side material chain is better externally documented than `CAS/Sim`
- do not reuse character-side `SimGlass` as the default label for object-side transparent content

Transparent-object signal companion:

- [Build/Buy Transparent Object Fallback Ladder](buildbuy-transparent-object-fallback-ladder.md) now freezes how transparent-family reading is allowed to degrade before a generic transparent boundary is used
- [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md) now freezes where transparent-family choice sits inside the object-side authority chain
- [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md) now freezes the current decision order between object-glass, threshold/cutout transparency, `AlphaBlended`, and last-choice `SimGlass`

## Stateful and material-set boundary

What is now strong enough:

- `MTST` exists as a real object-side resource and lives directly in the `RCOL` object/material ecosystem
- `MLOD` can point to `MATD` or `MTST`
- community swatch/material-variant guidance consistently places variant selection inside the object-side model/material packet, not outside it

Current safe reading:

- if the object family is stateful, swatch-heavy, or variant-driven, keep `MTST` in the authority chain
- do not flatten `MTST` state or variant selection into one anonymous texture bag
- `MATD` remains the narrower per-material definition input, but `MTST` is the stronger carrier when the object family actually uses material sets or state variants

Current open gap:

- exact family-specific ranking between `MTST`, `MATD`, and object state packets is still incomplete outside the current sampled fixtures
- the structural selector layer is now narrower than before, but exact selector semantics are still open

Companion seam doc:

- [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md)

## Light parallel branch

What is now strong enough:

- `Light` is a distinct resource family in the TS4 reference material
- `LITE` is an `RCOL` chunk with explicit light types and light-dependent data
- this makes lighting behavior a parallel object-side branch, not evidence that the base surface-material chain is unknown

Current safe reading:

- keep `LITE` separate from surface-material authority
- light-bearing objects can still use the normal `OBJD -> MODL/MLOD -> MTST/MATD` chain for their visible surface materials
- light-specific behavior should be documented as an additional companion branch rather than as a replacement material path

External corroboration:

- [The Sims 4 Modders Reference: Resource Type Index](https://thesims4moddersreference.org/reference/resource-types/)
- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- [Sims_4:0x03B4C61D](https://modthesims.info/wiki.php?title=Sims_4%3A0x03B4C61D)

## Linked-object and helper boundary

What is now strong enough:

- object-side helpers such as cutout resources, slots, footprints, and linked-object resources exist and matter for full scene reconstruction
- `VPXY` is real and still belongs in the scenegraph/linkage helper branch
- current evidence remains weaker for exact TS4 `VPXY` traversal than for `OBJD`, `MODL`, `MLOD`, `MATD`, `MTST`, or `LITE`

Current safe reading:

- keep linked-object helpers bounded as reconstruction helpers unless a family-specific packet proves they are required for the primary material path
- do not let `VPXY` displace the better-documented `OBJD -> MODL/MLOD -> MTST/MATD` authority chain
- for windows, counters, sinks, modular pieces, and similar ecosystems, assume extra helper traversal may be needed for full scene reconstruction, but not yet for the base material-authority statement

## Current fixture-backed anchors

These fixtures do not replace the authority model. They make it restart-safe.

| Fixture class | What it currently proves | Current packet |
| --- | --- | --- |
| model-rooted stateful-material boundary | one `Build/Buy` model root now shows repeated `MTST` chunks, multiple equal-scored state hashes, and an inferred default-state selection boundary even without full swatch identity | [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md) |
| texture-bearing portable-state boundary | one second model-rooted `Build/Buy` fixture now shows a `MaterialSet`-sourced material with `textures=2` and `MTST` state deltas on portable shader properties even without closed swatch identity | [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md) |
| structural selector boundary | the current `MTST` seam now also has repeated cross-fixture `stateHash -> MATD` mapping and one repeated paired `unknown0` split, which is strong enough to preserve selector structure without yet naming the runtime semantics | [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md) |
| named refraction object/material seam | one object-linked `Build/Buy` bridge root now sits cleanly on the `OBJD -> Model -> MLOD -> MATD/MTST` chain | [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md) |
| visible layered day/night roots | `ShaderDayNightParameters` already reaches real visible `Build/Buy` model roots without proving generic slot semantics | [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md) |
| generated-light helper packet | `GenerateSpotLightmap` and `NextFloorLightMapXform` already sit in a bounded lightmap-helper branch instead of the generic surface-material bucket | [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md) |
| widened transparent-object verdict floor | the window/curtain quartet now survives to real `Partial` fixtures and is strong enough to freeze the next question as family-verdict closure instead of more widening | [Build/Buy Window-Curtain Family Verdict Boundary](live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md) |
| strongest transparent-object divergence floor | the strongest direct material-entry window and curtain now diverge enough to block one quartet-wide family label and to hand the next proof specifically to window-side cutout companions | [Build/Buy Window-Curtain Strongest-Pair Material Divergence](live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md) |
| window structural cutout companion floor | the surviving windows now have explicit same-instance `CutoutInfoTable` companions with `USES_CUTOUT` and `IS_PORTAL`, which is strong enough to preserve a structural companion branch even before `ModelCutout` is closed | [Build/Buy Window CutoutInfoTable Companion Floor](live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md) |
| window structural companion closure | the surviving windows now also have same-instance `ModelCutout`, closing the full local structural pair on the exact model roots | [Build/Buy Window ModelCutout Companion Closure](live-proof-packets/buildbuy-window-modelcutout-companion-closure.md) |
| window structural verdict floor | the surviving windows are now strong enough to carry the family verdict as structural cutout/opening first, with `AlphaCutout`-side material hints secondary | [Build/Buy Window Structural-Cutout Verdict Floor](live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md) |
| curtain route closure | the surviving curtain pair does not currently close through explicit `AlphaBlended`; the strongest curtain survives only through cutout-leaning transparency and the weaker control stays opaque | [Build/Buy Curtain Route Closure](live-proof-packets/buildbuy-curtain-route-closure.md) |
| widened transparent-object family split | the widened quartet is now strong enough to stay frozen as windows -> structural cutout/opening and curtains -> weaker threshold/cutout, with object glass unselected | [Build/Buy Window-Curtain Quartet Family Split](live-proof-packets/buildbuy-window-curtain-quartet-family-split.md) |
| narrowed glass-family search route | `SimGlass` survives at survey level and now has a better object-side search boundary, even though no stable `Build/Buy` fixture is closed yet | [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md) |

Safe reading:

- these fixtures prove where the base object/material chain already reaches real family-bearing content
- they do not justify `BuildBuy`-specific shader branches
- the named refraction fixture is now narrower than a generic seam note:
  - [Refraction LilyPad Direct MATD Floor](live-proof-packets/refraction-lilypad-direct-matd-floor.md) records the current direct embedded `MATD` floor
  - [Refraction LilyPad Projective Floor Boundary](live-proof-packets/refraction-lilypad-projective-floor-boundary.md) records the current stable projective floor without overreading it as refraction-slot closure

## Current implementation boundary

Current repo behavior is useful here in three narrow ways:

1. showing where object-side linkage already survives into shared material decoding
2. showing where edge families are still flattened after the object-side chain is found
3. showing where helper branches like `LITE` or lightmap/reveal vocabulary still remain approximation-heavy

Current repo behavior is not the authority source for this doc.

## What remains open

- exact family-specific authority order for all major `Build/Buy` ecosystems, not only the base static-object path
- exact ranking between `MTST`, object state, and `MATD` across more live swatch/state fixtures
- where linked-object ecosystems really require `VPXY` or other helper traversal for practical reconstruction
- the first stable row-level `SimGlass` `Build/Buy` fixture
- wider fixture-backed coverage for windows/cutouts, modular pieces, and linked multi-part objects

## Recommended next work

1. Keep this matrix as the object-side authority baseline instead of restating the chain ad hoc in every packet.
2. Use `RefractionMap`, `ShaderDayNightParameters`, and generated-light packets as fixture-backed examples of the same base chain, not as separate object pipelines.
3. Use [Build/Buy Stateful Material-Set Seam](buildbuy-stateful-material-set-seam.md), [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md), [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md), and [Build/Buy MTST State-Selector Structure](live-proof-packets/buildbuy-mtst-state-selector-structure.md) for `MTST` / `MaterialVariant` questions instead of reopening the whole matrix.
4. Keep the stalled transparent-decor cluster frozen as exhausted first-route evidence rather than reopening it by inertia.
5. Keep the transparent-object quartet frozen at the current inspection layer instead of reopening it by inertia.
6. Treat the widened quartet as the current `Partial` fixture floor:
   - exact `ObjectDefinition` identity roots are restart-safe entry points
   - same-package `swap32` `Model` promotion is now proven locally
   - embedded `MLOD` is currently enough to reach real live fixtures across both windows and curtains
7. Preserve the strongest direct pair inside that floor:
   - `sliding2Tile` is the strongest current window-side anchor
   - `norenShortTileable` is the strongest current curtain-side anchor
   - the pair already diverges enough to block one quartet-wide family label
8. Preserve the now-closed surviving window-side structural packet:
   - `sliding2Tile` and `windowBox2Tile` each now have same-instance `ModelCutout + CutoutInfoTable`
   - both companions point back to the exact promoted model root
   - the window-side verdict is now structural cutout/opening first, with material cutout hints secondary
9. Preserve the now-closed curtain-side packet too:
   - neither `norenShortTileable` nor `strawTileable2Tile` currently closes as explicit `AlphaBlended`
   - the curtain-side verdict is now weaker threshold/cutout, not structural opening or object glass
10. Hand the next autonomous batch to the next unfinished Tier A lane unless a stronger transparent-object challenger appears.
11. Add a later transparent-object follow-up packet only when one of these family seams closes:
   - stable `SimGlass` row-level object fixture
   - stronger swatch-level or texture-bearing `MTST` state/variant fixture
   - stronger object-glass or explicit `AlphaBlended` fixture that can honestly reopen the frozen quartet split
   - narrower linked-object/`VPXY` reconstruction fixture

## Sources used for this matrix

- [The Sims 4 Modders Reference: Resource Type Index](https://thesims4moddersreference.org/reference/resource-types/)
- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/)
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34)
- [Sims_4:0x03B4C61D](https://modthesims.info/wiki.php?title=Sims_4%3A0x03B4C61D)
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120)
- [EA forum thread on object definition and material variant linkage](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695)
- [Re-texturing in game objects with s4pe](https://modthesims.info/t/538657)

## Honest limit

This matrix closes the base `Build/Buy` authority/discovery contract more clearly than before, but it does not claim that every object family is already solved.

What is now strong:

- one stable object-first material authority graph for `Build/Buy`
- one clearer split between base surface-material authority and parallel helper branches
- one restart-safe place to attach `MTST`, `RefractionMap`, day/night, generated-light, and future `SimGlass` object fixtures

What remains unresolved:

- full family-specific authority order across all `Build/Buy` ecosystems
- exact swatch-level state/variant ranking in the wider live corpus
- exact TS4-specific `VPXY` traversal behavior
- exact edge-family shader semantics, which still belong in the family and live-proof layers
