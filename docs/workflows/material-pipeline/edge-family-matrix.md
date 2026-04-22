# Edge-Family Matrix

This document collects the narrow external-first family packets that do not fit cleanly into the ordinary shell, worn-slot, or generic surface-material story.

Use it when the question is:

- which narrow family must stay preserved as its own authority branch
- which helper vocabulary should remain provenance rather than be flattened into ordinary slots
- where current implementation is still approximating a real edge family

Related docs:

- [Knowledge map](../../knowledge-map.md)
- [Material pipeline deep dives](README.md)
- [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Shader Family Registry](shader-family-registry.md)
- [Corpus-Wide Family Priority](corpus-wide-family-priority.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Family Sheets](family-sheets/README.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Edge-Family Matrix
├─ Object-side glass and transparency seam ~ 100%
├─ Sim glass / skin-family seam ~ 100%
├─ Hotspot / morph atlas seam ~ 86%
├─ Layered reveal / day-night seam ~ 80%
├─ Generated-light seam ~ 82%
├─ Refraction seam ~ 100%
└─ Full authority ranking against MTNF/CASP/compositor ~ 60%
```

## Evidence order

Use this matrix in the following order:

1. external references and creator tooling
2. local snapshots of external tooling
3. current implementation only as boundary or failure evidence

Safe rule:

- if a narrow family has an external behavioral packet, preserve it as its own row
- do not widen it into generic `surface`, `alpha`, `overlay`, or `diffuse` logic just because current preview does not render it faithfully
- treat domain-heavy packets here as authority and provenance guidance only; once authoritative inputs are chosen, these rows still feed the same shared shader/material contract
- do not pull the separate `Build/Buy` `MTST` seam back into this edge-family matrix:
  - [Build/Buy MTST Default-State Boundary](live-proof-packets/buildbuy-mtst-default-state-boundary.md)
  - [Build/Buy MTST Portable-State Delta](live-proof-packets/buildbuy-mtst-portable-state-delta.md)

## Current matrix

| Edge family | Strongest external packet | Current safest authority reading | What must not be flattened | Current confidence |
| --- | --- | --- | --- | --- |
| `SimGlass` transparent shell companions | `TS4SimRipper` explicit enum plus separate preview/export handling; Sims-lineage `simglass` packet | preserve GEOM-side glass-family identity first, then layer `CASP`, `MTNF`, skintone, and region logic on top | do not collapse into `SimSkin` or generic alpha | medium-high |
| object-side glass and transparency | creator-facing object-glass, threshold, blended-transparency, and architectural-cutout workflows plus object-glass lineage shader packet | keep `GlassForObjectsTranslucent`, threshold/cutout helpers, `AlphaBlended`, and architectural cutout resources separate from character-side `SimGlass` and from one another until live TS4 closure says otherwise; the old decor route is now honestly stalled, and the widened window/curtain quartet is now frozen with windows on structural cutout/opening first and curtains on weaker threshold/cutout, while object glass remains unselected for that quartet | do not collapse object transparency into `SimGlass` or one universal alpha family | medium-high |
| `SimSkin` / `SimSkinMask` seam | `TS4SimRipper` sample assets and enum for `SimSkin`; creator tooling keeps masks in skintone/overlay workflows | keep `SimSkin` as the baseline GEOM-side skin family; keep body/head shell authority ahead of skintone/compositor refinement; keep `SimSkinMask` as adjacent mask or compositor semantics until a peer geometry branch is found | do not promote `SimSkinMask` into a standalone geometry-family authority node without live proof | medium-high |
| `CASHotSpotAtlas` | MorphMaker and slider tutorials tie it to `UV1`, atlas color values, `HotSpotControl`, and `SimModifier` | treat it as a CAS editing or morph atlas branch; keep the local external packet narrower than the creator packet by reading `TS4SimRipper` as downstream `SimModifier -> SMOD -> BGEO/DMap/BOND` proof, not as local atlas closure | do not reinterpret it as ordinary sampled material texture | high |
| `ShaderDayNightParameters` | family name plus reveal-map lineage packet | keep it as layered day/night or reveal-aware family with helper provenance; the local packet is now strong enough to preserve `LightsAnimLookupMap` and `samplerRevealMap` separately while using three visible-root anchors for fixture selection | do not normalize into plain `diffuse + emissive + overlay` truth claims | medium-high |
| `GenerateSpotLightmap` / `NextFloorLightMapXform` | TS4 lightmap discussion groups these names with generated-light vocabulary | keep them under generated-light or lightmap helper behavior; the local packet now favors the stronger `GenerateSpotLightmap + NextFloorLightMapXform = 14` cluster over the weaker `= 3` carry-through and keeps adjacent projective roots as controls only | do not reinterpret as ordinary surface slots or plain UV transforms | medium-high |
| `RefractionMap` | Sims-lineage refraction families and refraction helper params | keep it under projection or refraction families | do not flatten into generic surface semantics | medium |

## Proof packet summary

| Edge family | Externally proved packet | Local external snapshot packet | Current implementation boundary | Current live-proof packet | Next live-proof target |
| --- | --- | --- | --- | --- | --- |
| `SimGlass` | distinct `simglass` lineage family plus TS4 creator-tool branch | `TS4SimRipper` enum, preview grouping, export suffix, creator-facing `Simglass` transparency guidance for clothing/glasses/hair, `probe_all_buildbuy_summary_full.json` with `SimGlass = 5`, bundled `SimSkin` shell controls, one bounded negative `EP10` window-heavy packet, and one broader survey-backed `EP10` transparent-decor cluster (`fishBowl`, `displayShelf`, `shopDisplayTileable`, `lantern`, `mirror`) whose transformed roots appear in `probe_all_buildbuy.txt`; the route is now narrower because `displayShelf`, `shopDisplayTileable`, `mirror`, and `lantern` preserve repeated transformed companion bundles in candidate resolution, the current Build/Buy side is capped by an explicit evidence-limit packet so aggregate presence cannot be overread into family closure, the first valid Build/Buy promotion now requires an explicit winning-branch gate against stronger object-side transparent branches, the losing conditions plus winning-fixture recording contract are now explicit too, the positive-signal side plus allowed verdict ladder are now explicit as well, and provisional/mixed `SimGlass` outcomes are now explicitly bounded too | still approximated through broad material logic in current preview | [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md), [SimGlass Build/Buy Evidence Limit](live-proof-packets/simglass-buildbuy-evidence-limit.md), [SimGlass Build/Buy Promotion Gate](live-proof-packets/simglass-buildbuy-promotion-gate.md), [SimGlass Build/Buy Disqualifiers](live-proof-packets/simglass-buildbuy-disqualifiers.md), [SimGlass Build/Buy Winning Signals](live-proof-packets/simglass-buildbuy-winning-signals.md), [SimGlass Build/Buy Outcome Ladder](live-proof-packets/simglass-buildbuy-outcome-ladder.md), [SimGlass Build/Buy Mixed-Signal Resolution](live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md), [SimGlass Build/Buy Provisional Candidate Checklist](live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md), [SimGlass Build/Buy Winning Fixture Checklist](live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md), [SimGlass EP10 Transparent-Decor Route](live-proof-packets/simglass-ep10-transparent-decor-route.md) | reopen one row-level fixture from the transparent-decor companion-bundle cluster, record which positive `SimGlass` signals survive, force the result through the explicit verdict ladder, and document provisional mixed cases without fuzzy wording |
| object-side glass and transparency | creator-facing object-glass workflow names `GlassForObjectsTranslucent`; separate creator tutorials keep threshold/cutout transparency and `AlphaBlended` as different object-side routes; Syboulette now also keeps `Model Cutout` and `Cut Info Table` explicit for windows/doors/archways | the old transparent-decor cluster is fully stalled at the current inspection layer, the old window-heavy sweep stays frozen as a negative control, and the widened route is now explicit plus live on all four anchors: `window2X1_EP10GENsliding2Tile`, `window2X1_EP10TRADwindowBox2Tile`, `curtain1x1_EP10GENstrawTileable2Tile`, and `curtain2x1_EP10GENnorenShortTileable`; the first two windows preserve full `Model/Rig/Slot/Footprint` companion bundles and now carry the full same-instance structural companion pair `ModelCutout + CutoutInfoTable` with `flags=0x321`, including `IS_PORTAL` plus `USES_CUTOUT`, while the curtains preserve weaker `Model/Footprint` bundles plus weaker same-instance `CutoutInfoTable` and no same-instance `ModelCutout`; `norenShortTileable` survives only through `transparent=True` plus `alpha-test-or-blend` and `AlphaCutoutMaterialDecodeStrategy`, while `strawTileable2Tile` stays opaque; that is strong enough to freeze the quartet as windows -> structural cutout/opening and curtains -> weaker threshold/cutout, with object glass still unselected | current preview can still flatten object transparency families too aggressively, and multi-run `ProbeAsset` use still hits a sqlite probe-cache concurrency ceiling | [Build/Buy Transparent Object Authority Order](buildbuy-transparent-object-authority-order.md), [Build/Buy Transparent Object Classification Signals](buildbuy-transparent-object-classification-signals.md), [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md), [Build/Buy Transparent Object Candidate State Ladder](live-proof-packets/buildbuy-transparent-object-candidate-state-ladder.md), [Build/Buy Transparent-Decor Route](live-proof-packets/buildbuy-transparent-decor-route.md), [Build/Buy Transparent Object Fixture Promotion Boundary](live-proof-packets/buildbuy-transparent-object-fixture-promotion-boundary.md), [Build/Buy Transparent Object Mixed-Signal Resolution](live-proof-packets/buildbuy-transparent-object-mixed-signal-resolution.md), [Build/Buy Transparent Object Reopen Checklist](live-proof-packets/buildbuy-transparent-object-reopen-checklist.md), [Build/Buy Transparent Object Target Priority](live-proof-packets/buildbuy-transparent-object-target-priority.md), [Build/Buy Transparent Object Top-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-top-anchor-negative-reopen.md), [Build/Buy Transparent Object Lower-Anchor Negative Reopen](live-proof-packets/buildbuy-transparent-object-lower-anchor-negative-reopen.md), [Build/Buy Transparent Object Full-Route Stall](live-proof-packets/buildbuy-transparent-object-full-route-stall.md), [Build/Buy Window-Heavy Transparent Negative Control](live-proof-packets/buildbuy-window-heavy-transparent-negative-control.md), [Build/Buy Window-Curtain Widening Route](live-proof-packets/buildbuy-window-curtain-widening-route.md), [Build/Buy Window-Curtain Family Verdict Boundary](live-proof-packets/buildbuy-window-curtain-family-verdict-boundary.md), [Build/Buy Window-Curtain Strongest-Pair Material Divergence](live-proof-packets/buildbuy-window-curtain-strongest-pair-material-divergence.md), [Build/Buy Window CutoutInfoTable Companion Floor](live-proof-packets/buildbuy-window-cutoutinfotable-companion-floor.md), [Build/Buy Window ModelCutout Companion Closure](live-proof-packets/buildbuy-window-modelcutout-companion-closure.md), [Build/Buy Window Structural-Cutout Verdict Floor](live-proof-packets/buildbuy-window-structural-cutout-verdict-floor.md), [Build/Buy Curtain Route Closure](live-proof-packets/buildbuy-curtain-route-closure.md), [Build/Buy Window-Curtain Quartet Family Split](live-proof-packets/buildbuy-window-curtain-quartet-family-split.md), plus [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md) | keep the widened quartet frozen at the current inspection layer, and only reopen this row if a later fixture can beat the current winners with stronger object-glass or explicit `AlphaBlended` evidence |
| `SimSkin` / `SimSkinMask` seam | `SimSkin` has a real lineage family; mask workflows stay in overlay/skintone tooling | bundled `.simgeom` packet for `SimSkin`, `TONE.cs`/`SkinBlender.cs` as layered skintone snapshots, the completed direct character-side family floor with `SimSkin = 280983` across `401` packages and `86697` unique linked `GEOM`, the checked-in fullscan plus `414` per-package result shards that still surface `SimSkin` and `SimGlass` but no `SimSkinMask`, the new `simskin_vs_simskinmask_snapshot_2026-04-21.json` profile split (`simskin = 51` across `3` packed types; `SimSkinMask = 12` across `6` packed types) plus the current bounded fact that the wider workspace `.simgeom` list only adds a mirrored `tmp/research/TS4SimRipper` copy rather than a new non-mirrored sample lane, public `TS4SimRipper` still exposes `SimSkin` and `SimGlass` rather than a peer `SimSkinMask` export branch, and public `Sims 4: CASPFlags` keeps the nearest skin-mask category at `SkinOverlay` | current repo still lacks dedicated `SimSkinMask` authority branch and still approximates exact compositor order | [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md), [BodyType 0x44 Family Boundary](live-proof-packets/bodytype-0x44-family-boundary.md), [BodyType 0x41 Family Boundary](live-proof-packets/bodytype-0x41-family-boundary.md), [BodyType 0x6D Family Boundary](live-proof-packets/bodytype-0x6d-family-boundary.md), [BodyType 0x6F Family Boundary](live-proof-packets/bodytype-0x6f-family-boundary.md), [BodyType 0x52 Family Boundary](live-proof-packets/bodytype-0x52-family-boundary.md), [BodyType 0x80 Family Boundary](live-proof-packets/bodytype-0x80-family-boundary.md), [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md) | keep `0x44`, `0x41`, `0x6D`, `0x6F`, `0x52`, and `0x80` frozen as family-specific packets, treat the current local `SimSkinMask` search as bounded on samples plus direct census, and only widen the counterexample search when a genuinely new live or external sample appears |
| `CASHotSpotAtlas` | MorphMaker and slider tutorials tie it to `UV1`, atlas color values, `HotSpotControl`, and modifiers | `TS4SimRipper` now gives a sharper local external split: external creator evidence already proves `CASHotSpotAtlas -> color value -> HotSpotControl -> SimModifier`, while the local tooling proves the downstream `SimModifier -> SMOD -> BGEO/DMap/BOND`; local survey also keeps `SimModifier = 472` and `SimHotspotControl = 212` as live search-space counts | current archaeology only proves carry-through, not runtime sampling | [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md) | one real CAS part or morph fixture where hotspot atlas provenance survives into adjacent render metadata without being treated as a surface slot |
| `ShaderDayNightParameters` | layered day/night naming plus reveal-map lineage support | local visible Build/Buy roots now isolate `ShaderDayNightParameters` next to narrow helper vocabulary, with `occurrences = 5`, `LightsAnimLookupMap = 94`, `samplerRevealMap = 32`, and three current visible anchors (`0737711577697F1C`, `00B6ABED04A8F593`, `1463BD19EE39DC8C`) | current broad slot normalization is approximation only | [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md) | one live asset with visible day/night or reveal behavior that can be compared against preserved helper provenance more directly |
| `GenerateSpotLightmap` / `NextFloorLightMapXform` | TS4 lightmap thread groups these names under generated-light vocabulary | local carry-through packet now isolates `GenerateSpotLightmap` with `occurrences = 6`, a stronger `NextFloorLightMapXform = 14` cluster, a weaker `= 3` carry-through cluster, and nearby projective roots that stay control fixtures only | current broad UV or slot treatment is approximation only | [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md) | one live asset carrying generated-light vocabulary where helper presence can be checked without coercing it into ordinary material slots |
| `RefractionMap` | lineage refraction families and helper params already exist | local corpus isolates `RefractionMap` plus family-local `tex1` and projective helpers, `probe_all_buildbuy_summary_full.json` adds `RefractionMap = 33` at survey level, `00F643B0FDD2F1F7` is now a named row-level bridge root linked to `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad` through `instance-swap32`, and the fixture now also sits on an externally-backed `OBJD -> Model -> MLOD -> MATD/MTST` inspection seam; `0389A352F5EDFD45` now acts as the next clean projective route, while `0124E3B8AC7BEE62` stays the mixed boundary case with one `FresnelOffset` LOD and fallback diffuse; the route stack now also freezes that `0389...` still does not upgrade the branch above the named `lilyPad` floor without a stronger inspection layer | current preview still flattens toward broad material logic | [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md), [Refraction Post-LilyPad Pivot](live-proof-packets/refraction-post-lilypad-pivot.md), [Refraction Next-Route Priority](live-proof-packets/refraction-next-route-priority.md), [Refraction 0389 No Signal Upgrade](live-proof-packets/refraction-0389-no-signal-upgrade.md), [Refraction Post-0389 Handoff Boundary](live-proof-packets/refraction-post-0389-handoff-boundary.md) | keep `lilyPad` as the bounded floor/ceiling reference, inspect `0389A352F5EDFD45` as the next clean route, keep `0124...` as mixed control, and hand off to the next unfinished family track once `0389...` is honestly bounded without a stronger layer |

## Priority queue for live-proof work

`Current concrete packet rows`

- `SimSkin` body/head shell authority
- `SimSkin` versus `SimSkinMask`
- object-side glass and transparency
- `CAS/Sim` compositor-authority follow-up
- `CASHotSpotAtlas` carry-through
- `ShaderDayNightParameters`
- `GenerateSpotLightmap` / `NextFloorLightMapXform`
- `SimGlass` versus shell baseline
- `RefractionMap`

`Next unfinished row`

- no single pack-local lane should define the next row by convenience alone
- rebuild the next unfinished row through [Corpus-Wide Family Priority](corpus-wide-family-priority.md):
  - Tier A first: `SimSkin`/character foundation, object-side transparency, and `CAS/Sim` compositor-authority blockers
  - Tier B next: `CASHotSpotAtlas`, `ShaderDayNightParameters`, and generated-light helpers
  - Tier C only after that: `RefractionMap` and `SimGlass` narrow carry-over lanes
- current bounded-lane reminder:
  - the completed character-side family floor now makes `SimSkin` materially stronger than `SimGlass` for queue ordering
  - `RefractionMap` is already bounded at the current inspection layer
  - the `EP10` transparent-decor route is already stalled at the current inspection layer
  - the next transparent-object widening phase now lives in the bounded window/curtain route rather than in a generic window-heavy sweep
  - that widened route now already has four `Partial` fixtures and a frozen quartet split, so it should not be reopened by inertia
  - both remain real evidence lanes, but neither should dominate the queue without a stronger corpus-level reason

## Per-row safe rules

### 1. `SimGlass`

Safe rule:

- preserve the glass-family branch before applying broader shell or compositor logic
- preserve it as shared family semantics, not as a `BuildBuy`-, `CAS`-, or `Sim`-specific shader variant

Current implementation boundary:

- current preview may still approximate it through broad material logic
- that is implementation weakness, not evidence that the family is generic

Source sheet:

- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)
- [SimGlass Build/Buy Evidence Order](simglass-buildbuy-evidence-order.md)
- [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
- [Character Transparency Open Edge](character-transparency-open-edge.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)

Current live-proof packet:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimGlass Build/Buy Evidence Limit](live-proof-packets/simglass-buildbuy-evidence-limit.md)
- [SimGlass Build/Buy Promotion Gate](live-proof-packets/simglass-buildbuy-promotion-gate.md)
- [SimGlass Build/Buy Disqualifiers](live-proof-packets/simglass-buildbuy-disqualifiers.md)
- [SimGlass Build/Buy Winning Signals](live-proof-packets/simglass-buildbuy-winning-signals.md)
- [SimGlass Build/Buy Outcome Ladder](live-proof-packets/simglass-buildbuy-outcome-ladder.md)
- [SimGlass Build/Buy Mixed-Signal Resolution](live-proof-packets/simglass-buildbuy-mixed-signal-resolution.md)
- [SimGlass Build/Buy Provisional Candidate Checklist](live-proof-packets/simglass-buildbuy-provisional-candidate-checklist.md)
- [SimGlass Build/Buy Winning Fixture Checklist](live-proof-packets/simglass-buildbuy-winning-fixture-checklist.md)
- [SimGlass EP10 Transparent-Decor Route](live-proof-packets/simglass-ep10-transparent-decor-route.md)

Current classification boundary:

- [Build/Buy Transparent Object Classification Boundary](live-proof-packets/buildbuy-transparent-object-classification-boundary.md)

### 2. `SimSkin` versus `SimSkinMask`

Safe rule:

- preserve `SimSkin` as the baseline GEOM-side family
- keep `SimSkinMask` under mask, overlay, or skintone-adjacent semantics until a live peer branch appears
- once that authority choice is made, route both through the same shared material/shader contract instead of splitting by domain

Current implementation boundary:

- the current repo still lacks a dedicated `SimSkinMask` authority branch
- that absence is not proof that mask semantics are unimportant

Source sheet:

- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)

Current live-proof packet:

- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)

### 3. `CASHotSpotAtlas`

Safe rule:

- keep this row inside the edit or morph branch
- treat that branch as pre-shader authority or helper provenance, not as a separate renderer family for one asset domain

Current implementation boundary:

- if the atlas name survives into broader render/profile packets, record that as helper provenance only

Source sheet:

- [CASHotSpotAtlas](family-sheets/cas-hotspot-atlas.md)

Current live-proof packet:

- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)

### 4. `ShaderDayNightParameters`

Safe rule:

- preserve the family as layered and helper-heavy
- keep reveal and light-lookup names separate from ordinary base slots
- keep the distinction at the family-semantics level; do not read domain-specific fixtures here as a reason to fork the shared shader system

Current implementation boundary:

- current broad slot normalization around this family is approximation only

Source sheet:

- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [ShaderDayNight Evidence Ledger](shader-daynight-evidence-ledger.md)

Current live-proof packet:

- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)

### 5. `GenerateSpotLightmap` and `NextFloorLightMapXform`

Safe rule:

- keep this row in generated-light vocabulary
- keep `NextFloorLightMapXform` as helper provenance until exact matrix semantics are known
- helper provenance here still feeds the shared material pipeline after authority is established; it does not justify a domain-local shader branch

Current implementation boundary:

- current broad UV or slot treatment around these names is not authority

Source sheet:

- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Generated-Light Evidence Ledger](generated-light-evidence-ledger.md)

Current live-proof packet:

- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)

### 6. `RefractionMap`

Safe rule:

- preserve it as projection or refraction family
- keep family-local unresolved names inside that branch
- use concrete fixtures only to prove the family survives a given authority path, not to define asset-bound shader logic

Current implementation boundary:

- if current preview forces broad slot normalization, that is only an approximation boundary

Source sheet:

- [Projection, Reveal, And Lightmap Families](family-sheets/projection-reveal-lightmap.md)
- [Refraction Evidence Ledger](refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](refraction-bridge-fixture-boundary.md)

Current live-proof packet:

- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)
- [Refraction Companion-Material Outcome Ladder](live-proof-packets/refraction-companion-material-outcome-ladder.md)
- [Refraction Companion-Material Checklist](live-proof-packets/refraction-companion-material-checklist.md)
- [Refraction Companion MATD-vs-MTST Boundary](live-proof-packets/refraction-companion-matd-vs-mtst-boundary.md)
- [Refraction Adjacent-Helper Boundary](live-proof-packets/refraction-adjacent-helper-boundary.md)
- [Refraction LilyPad Direct MATD Floor](live-proof-packets/refraction-lilypad-direct-matd-floor.md)
- [Refraction LilyPad Projective Floor Boundary](live-proof-packets/refraction-lilypad-projective-floor-boundary.md)
- [Refraction LilyPad No Direct Family Surface](live-proof-packets/refraction-lilypad-no-direct-family-surface.md)
- [Refraction LilyPad Escalation Boundary](live-proof-packets/refraction-lilypad-escalation-boundary.md)
- [Refraction Post-LilyPad Pivot](live-proof-packets/refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](live-proof-packets/refraction-next-route-priority.md)
- [Refraction 0389 Clean-Route Baseline](live-proof-packets/refraction-0389-clean-route-baseline.md)
- [Refraction 0124 Mixed-Control Floor](live-proof-packets/refraction-0124-mixed-control-floor.md)
- [Refraction 0389 Identity Gap](live-proof-packets/refraction-0389-identity-gap.md)
- [Refraction 0389 Versus LilyPad Floor](live-proof-packets/refraction-0389-vs-lilypad-floor.md)

## Current implementation boundary

The repo can currently help in three narrow ways:

1. showing which edge-family names are already being preserved
2. showing which edge families are still being flattened together
3. showing where preview remains approximate or wrong

The repo cannot currently do in this document:

- define authoritative family truth
- define exact slot semantics for these edge families
- define exact compositor or generated-light math

## Open questions

- exact authority ranking between GEOM-side family identity, embedded `MTNF`, parsed `CASP` routing, and compositor layers
- exact visible-pass math for reveal or day-night families
- exact matrix semantics for `NextFloorLightMapXform`
- exact slot contract for `RefractionMap`
- wider live-asset proof for any peer `SimSkinMask` geometry branch

## Recommended next work

1. Use this matrix as the narrow-family authority layer instead of broadening generic fallback tables.
2. Use [P1 Live-Proof Queue](p1-live-proof-queue.md) as the working order for row-by-row candidate inspection.
3. Add live-asset proof packets row by row, starting with the unfinished Tier A rows strengthened by the completed character-side family floor.
4. Only after row-level proof improves, promote any edge-family semantics into per-slot implementation tables.
