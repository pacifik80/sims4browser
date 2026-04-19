# RefractionMap Live Proof

This packet turns `RefractionMap` into the next concrete live-proof document.

Question:

- does the current workspace already support a bounded refraction-family reading for `RefractionMap` and `tex1` without collapsing them into ordinary diffuse or generic secondary texture semantics?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Projection, Reveal, And Lightmap Families](../family-sheets/projection-reveal-lightmap.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
RefractionMap Live Proof
├─ Externally proved family identity ~ 73%
├─ Local family-local parameter isolation ~ 82%
├─ Candidate live-root isolation ~ 86%
├─ Build/Buy object/material seam ~ 79%
├─ Exact slot contract ~ 21%
└─ Implementation-diagnostic value ~ 79%
```

## Externally proved family identity

What is already strong enough:

- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) already treats refraction-oriented families and `simglass` as their own family packet rather than synonyms for ordinary surface sampling
- [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) keeps refraction-oriented helper semantics distinct from ordinary base-material slots
- [Sims_3:0xEA5118B0](https://modthesims.info/wiki.php?title=Sims_3%3A0xEA5118B0) exposes `Refraction Distortion Scale` in the same lineage, which is strong support for refraction-specific helper behavior

Overall safe reading:

- `RefractionMap` belongs under projection/refraction families
- the burden of proof is on any implementation that wants to flatten it into ordinary diffuse-like semantics
- concrete object-linked fixtures in this packet are authority/evidence routes only; they are not evidence for asset-bound refraction shaders outside the shared material contract

## Local family-local parameter isolation

Strongest current local packet:

- `tmp/precomp_sblk_inventory.json`: `name_guess = "RefractionMap"` with repeated parameter packet concentration
- the direct owning archaeology row for that packet is `hash_hex = 0xBB85A5B7` / `3146098103` in `tmp/precomp_sblk_inventory.json`
- the same owning row survives in `tmp/precomp_shader_profiles.json` under `0xBB85A5B7`
- the same local inventory isolates `tex1 = 87` under `RefractionMap`
- the same packet also carries `uvMapping = 134`, `samplerEmissionMap = 36`, and `gPosToUVDest = 36`
- the shared guide already records the same bounded reading: `tex1` is family-local inside a projective/runtime family, while `gPosToUVDest` is the broader projective helper
- the adjacent helper-side archaeology row for `samplerRefractionMap` is weaker and currently sits at `hash_hex = 0xF087D458` / `4035433560`

Safe reading:

- `tex1` is not behaving like a broad cross-family slot in the current corpus
- `tex1` is currently strongest as a family-local unresolved input inside `RefractionMap`
- the workspace now has one stable direct owning archaeology row for `RefractionMap`, separate from the weaker `samplerRefractionMap` adjacency
- the archaeology packet is still mixed and should not be promoted to truth: named params around `0xBB85A5B7` include `gPosToUVDest`, `uvMapping`, `samplerEmissionMap`, `samplerCASMedatorGridTexture`, `samplerClothWithAlphaTexture`, `samplerFloorCubeMap`, `SimWingsGhostMask`, `WaterScrollSpeedLayer1`, and `tex1`
- `gPosToUVDest` should stay separate as a broader projective helper rather than be used to flatten the whole family into a solved UV contract

## Current candidate live roots

Current honest state:

- the best current local packet is still the isolated family-local concentration in `tmp/precomp_sblk_inventory.json`
- `samplerRefractionMap` survives as a weaker adjacent name-guess packet in the same local inventory, which is enough to keep the family alive in the queue but not enough to close live behavior
- `tmp/probe_all_buildbuy_summary_full.json` now also records `"RefractionMap": 33` across the resolved Build/Buy survey, which is materially stronger than one isolated `name_guess` packet
- the nearest TS4-facing adjacent projective roots currently isolated in this workspace are:
  - `EP10\\ClientFullBuild0.package | 01661233:00000000:00F643B0FDD2F1F7` in `tmp/probe_00F6_after_projective_packed_fix.txt`
  - `EP04\\ClientFullBuild0.package | 01661233:00000000:0124E3B8AC7BEE62` in `tmp/probe_0124_projective_current.txt`
- the strongest fully textured visible comparison roots in the same edge-family neighborhood are:
  - `EP08\\ClientFullBuild0.package | 01661233:00000000:0737711577697F1C` in `tmp/probe_0737711577697F1C.txt`
  - `EP03\\ClientFullBuild0.package | 01661233:00000000:00B6ABED04A8F593` in `tmp/probe_00b6_daynight_after_family_slots.txt`
- those four roots are also confirmed as row-level coverage entries in `tmp/probe_sample_medium_coverage.txt`
- `00F643B0FDD2F1F7` is currently the cleaner adjacent projective root:
  - `Texture candidates: 1`
  - `Material families: WorldToDepthMapSpaceMatrix=1`
  - `Material decode strategy: ProjectiveMaterialDecodeStrategy`
  - static-atlas transform currently lands on `UV0 scale=(0.846,0.003) offset=(0,0.65)`
  - resolved textures currently include base `diffuse` plus `texture_5`
  - current root probe resolves both textures as explicit local package lookups from `EP10\\ClientFullBuild0.package`
  - the same root now repeats across multiple sampled coverage artifacts such as `probe_sample_ep06_ep10_coverage.txt`, `probe_sample_medium_coverage.txt`, and `probe_sample_next12_coverage.txt`
- the current narrower `EP10` Build/Buy identity pass now maps that same root back to one concrete object-definition row:
  - package/object row: `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad`
  - object-definition instance: `000000000003FC7F`
  - object-definition TGI: `C0DB5AE7:00000000:000000000003FC7F`
  - catalog TGI: `319E4F1D:00000000:000000000003FC7F`
  - embedded model-side reference candidate at `OBJD +0x006D`: `01661233:00000000:FDD2F1F700F643B0`
  - candidate-resolution transform: `instance-swap32 -> 01661233:00000000:00F643B0FDD2F1F7`
  - this proves row-level object linkage for the current first bridge root, even though it still does not prove exact `RefractionMap` slot semantics
- `0124E3B8AC7BEE62` is a weaker bridge root:
  - `Texture candidates: 0`
  - most sampled rows still read as `WorldToDepthMapSpaceMatrix=2`
  - but `LOD 00010002` falls through to `FresnelOffset=1` with `DefaultMaterialDecodeStrategy`
  - its current root probe falls back to same-instance indexed `diffuse` from `EP04\\ClientFullBuild2.package`
  - this makes it useful as a boundary case, not as a clean refraction proxy
- the visible comparison roots also currently emit saved probe textures under `tmp/probe-textures/`, but those files are comparison artifacts, not direct refraction proof

## Build/Buy object/material seam

Strongest external chain for why this fixture is valid:

- [The Sims 4 Modders Reference / Resource Type Index](https://thesims4moddersreference.org/reference/resource-types/) keeps `Object Catalog`, `Object Definition`, `Model`, `Model LOD`, `Material Definition`, and `Material Set` inside one Build Mode resource family
- [The Sims 4 Modders Reference / File Types](https://thesims4moddersreference.org/reference/file-types/) describes `Object Definition` as a swatch-level Build/Buy record with linked components, `Model/Model LOD` as the physical object data, and `Material Set` as sets of `Material Definitions`
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120) narrows the object side further:
  - `COBJ` is mostly the catalogue-facing record
  - `OBJD` links the in-world object instance to `Model`, `Rig`, `Slot`, and `Footprint`
  - `MaterialVariant` in `OBJD` points by FNV32 name into the `Type300Entries` list in `MODL/MLOD`, and that index points to the relevant material definition
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34) gives the mesh/material side of the same chain: `MLOD` maps each mesh group to `VRTF`, `VBUF`, `IBUF`, `SKIN`, and `MATD` or `MTST`
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL) keeps `MODL`, `MLOD`, `MATD`, `MTST`, `RSLT`, and `FTPT` in one scenegraph packet, which is the current safest external framing for object-material companion inspection

What the named local fixture adds:

- the `EP10` lily-pad row now fits that external chain cleanly enough to serve as a durable inspection root:
  - `COBJ/OBJD instance = 000000000003FC7F`
  - `OBJD` points to model candidate `01661233:00000000:FDD2F1F700F643B0`
  - current candidate resolution maps that to root `01661233:00000000:00F643B0FDD2F1F7` via `instance-swap32`
  - current root probe on `00F643...` already surfaces embedded `MATD` chunks and a selected `MLOD` root (`01D10F34:*:00F643B0FDD2F1F7`)

Safe reading:

- the named lily-pad object is now strong enough to use as a Build/Buy object-material inspection fixture
- this proves the object/material seam for the fixture, not the final semantics of `RefractionMap`
- the next proof step should stay at the companion-material layer and only then revisit family-local params like `tex1`
- if the seam closes, it still closes as shared refraction-family semantics reached through one authority route, not as a `BuildBuy`-specific shader rule
- do not generalize the same candidate-resolution transform into a universal rule:
  - the current broader `EP10` glass-family candidate cluster also has `OBJD -> instance-swap32` transformed roots
  - those transformed roots do appear in `tmp/probe_all_buildbuy.txt`
  - but current direct reopen attempts still do not reconstruct them as stable Build/Buy assets
- that asymmetry is why `sculptFountainSurface3x3_EP10GENlilyPad` remains the current strongest named refraction bridge fixture

Safe reading:

- `RefractionMap` now survives at survey level in the current TS4-facing workspace, not only in profile archaeology
- the workspace now has one direct named row-level bridge root for the refraction/projective neighborhood, not only unnamed adjacent TGIs
- the current survey stack still has one hard boundary: `probe_all_buildbuy.txt` is a root/header list, while family totals live in `probe_all_buildbuy_summary_full.json`; direct family-row extraction therefore still depends on narrower coverage artifacts rather than one full-log grep pass
- among the current adjacent roots, `00F643B0FDD2F1F7` is the safer first extraction target because it keeps explicit local textures and a cleaner projective-static-atlas path
- `0124E3B8AC7BEE62` should be treated as a mixed/edge comparison root because it depends on fallback diffuse resolution and already exposes one `FresnelOffset` LOD
- `00F643B0FDD2F1F7` is no longer only an unnamed extraction target; it is now a named object-linked fixture rooted at `sculptFountainSurface3x3_EP10GENlilyPad`
- this is a real gap in the current workspace, not a reason to demote the family into generic surface semantics

## What this packet is trying to prove

Exact target claim:

- the current workspace already supports a bounded refraction-family reading in which `RefractionMap` and `tex1` stay preserved as family-local/projective provenance while exact slot semantics remain open
- the fixture-level object/material seam can be documented without turning that proof into a special-case renderer branch for one asset class

Not being proved yet:

- exact TS4 sampled slot semantics for `tex1`
- exact visible-pass math for refraction-family content
- one concrete end-to-end live asset root where `RefractionMap` itself, not just the adjacent projective bridge, is surfaced directly by name
- whether the lily-pad seam resolves through direct `MATD` only or through any meaningful `MTST` variant path in the actual object packet

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- if current preview flattens `RefractionMap` into broad material logic, that is approximation
- if current slot mapping tries to normalize `tex1` into `diffuse`, `specular`, `emissive`, or `overlay`, that is still a heuristic boundary, not family truth

Diagnostic value of this packet:

- it prevents the docs from inventing false closure around `tex1`
- it now also provides one durable object-linked fixture that future implementation or inspection passes can revisit without rescanning the whole Build/Buy survey

## Best next inspection step

1. Keep the Sims-lineage refraction packet as the external family baseline.
2. Keep the current `RefractionMap + tex1 + gPosToUVDest` local concentration as the strongest family-local packet.
3. Use `tmp/probe_all_buildbuy_summary_full.json` with `"RefractionMap": 33` as the current stronger TS4-facing survey hint.
4. Use the now-named `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7` link as the first durable row-level fixture.
5. Keep `0124E3B8AC7BEE62` as a mixed boundary case because one sampled LOD already lands on `FresnelOffset` and the visible texture path currently falls back to same-instance diffuse.
6. Use the visible comparison roots `0737711577697F1C` and `00B6ABED04A8F593` to keep the edge-family neighborhood bounded.
7. Inspect the named lily-pad fixture next at the object/material companion layer using the external `OBJD -> Model -> MLOD -> MATD/MTST` chain before making any direct slot claim for `tex1`.
8. Repeat the same row-level extraction method for `SimGlass` after the lily-pad bridge packet is fully folded back into the matrix and queue.

## Honest limit

This packet does not yet prove exact TS4 refraction-slot semantics.

What it does prove:

- `RefractionMap` is already too well bounded to leave under generic unresolved texture vocabulary
- `tex1` is currently strongest as a family-local unresolved refraction input, not as a generic secondary surface slot
- the current workspace now also sees `RefractionMap` at Build/Buy survey level and has one named row-level bridge root anchored to `sculptFountainSurface3x3_EP10GENlilyPad`
- the current workspace now has a bounded adjacent-root bridge for the projective neighborhood around `RefractionMap`, which is stronger than profile archaeology alone
- `00F643B0FDD2F1F7` is currently the better first bridge root than `0124E3B8AC7BEE62`, because `0124` already shows mixed `FresnelOffset` behavior on one sampled LOD and a weaker fallback texture path
- `00F643B0FDD2F1F7` now also qualifies as the current best named bridge root because it repeats across multiple sampled coverage packets with stable projective behavior and now has an object-definition linkage through `instance-swap32`
