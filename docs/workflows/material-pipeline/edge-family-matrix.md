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
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Live-Proof Packets](live-proof-packets/README.md)
- [Family Sheets](family-sheets/README.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Edge-Family Matrix
├─ Sim glass / skin-family seam ~ 84%
├─ Hotspot / morph atlas seam ~ 86%
├─ Layered reveal / day-night seam ~ 74%
├─ Generated-light seam ~ 76%
├─ Refraction seam ~ 91%
└─ Full authority ranking against MTNF/CASP/compositor ~ 57%
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

## Current matrix

| Edge family | Strongest external packet | Current safest authority reading | What must not be flattened | Current confidence |
| --- | --- | --- | --- | --- |
| `SimGlass` transparent shell companions | `TS4SimRipper` explicit enum plus separate preview/export handling; Sims-lineage `simglass` packet | preserve GEOM-side glass-family identity first, then layer `CASP`, `MTNF`, skintone, and region logic on top | do not collapse into `SimSkin` or generic alpha | medium-high |
| `SimSkin` / `SimSkinMask` seam | `TS4SimRipper` sample assets and enum for `SimSkin`; creator tooling keeps masks in skintone/overlay workflows | keep `SimSkin` as the baseline GEOM-side skin family; keep `SimSkinMask` as adjacent mask or compositor semantics until a peer geometry branch is found | do not promote `SimSkinMask` into a standalone geometry-family authority node without live proof | medium-high |
| `CASHotSpotAtlas` | MorphMaker and slider tutorials tie it to `UV1`, `HotSpotControl`, and `SimModifier` | treat it as a CAS editing or morph atlas branch | do not reinterpret it as ordinary sampled material texture | high |
| `ShaderDayNightParameters` | family name plus reveal-map lineage packet | keep it as layered day/night or reveal-aware family with helper provenance | do not normalize into plain `diffuse + emissive + overlay` truth claims | medium |
| `GenerateSpotLightmap` / `NextFloorLightMapXform` | TS4 lightmap discussion groups these names with generated-light vocabulary | keep them under generated-light or lightmap helper behavior | do not reinterpret as ordinary surface slots or plain UV transforms | medium-high |
| `RefractionMap` | Sims-lineage refraction families and refraction helper params | keep it under projection or refraction families | do not flatten into generic surface semantics | medium |

## Proof packet summary

| Edge family | Externally proved packet | Local external snapshot packet | Current implementation boundary | Current live-proof packet | Next live-proof target |
| --- | --- | --- | --- | --- | --- |
| `SimGlass` | distinct `simglass` lineage family plus TS4 creator-tool branch | `TS4SimRipper` enum, preview grouping, export suffix, creator-facing `Simglass` transparency guidance for clothing/glasses/hair, `probe_all_buildbuy_summary_full.json` with `SimGlass = 5`, bundled `SimSkin` shell controls, one bounded negative `EP10` window-heavy packet, and one broader survey-backed `EP10` transparent-decor cluster (`fishBowl`, `displayShelf`, `shopDisplayTileable`, `lantern`, `mirror`) whose transformed roots appear in `probe_all_buildbuy.txt` but do not yet reopen as stable fixtures | still approximated through broad material logic in current preview | [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md) | reopen one row-level fixture from the broader transparent-decor cluster instead of returning to window-first search |
| `SimSkin` / `SimSkinMask` seam | `SimSkin` has a real lineage family; mask workflows stay in overlay/skintone tooling | bundled `.simgeom` packet for `SimSkin`; no peer local export branch for `SimSkinMask` | current repo still lacks dedicated `SimSkinMask` authority branch | [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md) | one wider live corpus outside bundled `TS4SimRipper` samples that could either confirm or falsify a peer `SimSkinMask` geometry branch |
| `CASHotSpotAtlas` | MorphMaker and slider tutorials tie it to `UV1`, hotspots, and modifiers | `TS4SimRipper` enum plus `SIMInfo` / `SimModifier` packet confirms the morph-side resource chain | current archaeology only proves carry-through, not runtime sampling | [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md) | one real CAS part or morph fixture where hotspot atlas provenance survives into adjacent render metadata without being treated as a surface slot |
| `ShaderDayNightParameters` | layered day/night naming plus reveal-map lineage support | local visible Build/Buy roots now isolate `ShaderDayNightParameters` next to narrow helper vocabulary | current broad slot normalization is approximation only | [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md) | one live asset with visible day/night or reveal behavior that can be compared against preserved helper provenance more directly |
| `GenerateSpotLightmap` / `NextFloorLightMapXform` | TS4 lightmap thread groups these names under generated-light vocabulary | local carry-through packet now isolates stronger `GenerateSpotLightmap` and `NextFloorLightMapXform` concentration plus nearby projective roots | current broad UV or slot treatment is approximation only | [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md) | one live asset carrying generated-light vocabulary where helper presence can be checked without coercing it into ordinary material slots |
| `RefractionMap` | lineage refraction families and helper params already exist | local corpus isolates `RefractionMap` plus family-local `tex1` and projective helpers, `probe_all_buildbuy_summary_full.json` adds `RefractionMap = 33` at survey level, `00F643B0FDD2F1F7` is now a named row-level bridge root linked to `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad` through `instance-swap32`, and the fixture now also sits on an externally-backed `OBJD -> Model -> MLOD -> MATD/MTST` inspection seam; `0124E3B8AC7BEE62` remains a mixed boundary case with one `FresnelOffset` LOD and fallback diffuse | current preview still flattens toward broad material logic | [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md) | inspect the named lily-pad bridge root at the object/material companion seam, then repeat row-level extraction for `SimGlass` from a broader survey-backed packet |

## Priority queue for live-proof work

`Current concrete packet rows`

- `SimGlass` versus shell baseline
- `SimSkin` versus `SimSkinMask`
- `CASHotSpotAtlas` carry-through
- `ShaderDayNightParameters`
- `GenerateSpotLightmap` / `NextFloorLightMapXform`
- `RefractionMap`

`Next unfinished row`

- no unfinished named edge-family packet remains in the current queue; the next work is object/material inspection of the named lily-pad refraction bridge root, then broader survey-backed `SimGlass` extraction from the transparent-decor cluster rather than the earlier window-heavy packet

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

Current live-proof packet:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)

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

Current live-proof packet:

- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)

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
3. Add live-asset proof packets row by row, starting with unfinished rows that still have no concrete packet.
4. Only after row-level proof improves, promote any edge-family semantics into per-slot implementation tables.
