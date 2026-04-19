# SimSkin Versus SimSkinMask

This packet turns the `SimSkin` versus `SimSkinMask` seam into a concrete `P1` inspection document.

Question:

- does the current workspace contain enough evidence to promote `SimSkinMask` into a peer GEOM-side branch next to `SimSkin`, or does the safe reading still keep it under mask/compositor-adjacent semantics?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimSkin, SimGlass, And SimSkinMask](../family-sheets/simskin-simglass-simskinmask.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimSkin Versus SimSkinMask
├─ Externally proved identity ~ 84%
├─ Local external snapshot packet ~ 81%
├─ Current negative-result packet ~ 78%
├─ Candidate counterexample isolation ~ 53%
└─ Implementation-diagnostic value ~ 66%
```

## Externally proved identity

What is already strong enough:

- local external [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimSkin = 0x548394B9`
- local external [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) keeps `SimSkin` as the baseline export family and falls back to it when a mesh shader is otherwise unmapped
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) preserves `SimSkin` as a distinct lineage family rather than a generic material alias
- [Sims_3:0x015A1849](https://modthesims.info/wiki.php?title=Sims_3%3A0x015A1849) keeps morphskin-family geometry handling tied to the `SimSkin` shader hash in the same engine lineage
- creator-facing skin-mask workflows still route through skintone, overlay, burn-mask, or image-mask tools such as [TS4 Skininator](https://modthesims.info/d/568474/ts4-skininator-updated-8-6-2018-version-1-12.html), [TS4 Skin Converter](https://modthesims.info/d/629700/ts4-skin-converter.html), and [Please explain Skin Overlay, Skin Mask, Normal Skin, Etc](https://modthesims.info/t/594620)

Safe reading:

- `SimSkin` is a real GEOM-side skin family
- `SimSkinMask` is still better grounded as adjacent mask/compositor semantics than as a proven peer geometry family
- the burden of proof remains on any future claim that `SimSkinMask` should outrank the current bounded reading

## Local external snapshot packet

Strongest local packet in this repo:

- [Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs): explicit `SimSkin = 0x548394B9`
- [ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs): explicit `SimSkin` export suffix and `SimSkin` fallback path
- [GEOM.cs](../../../references/external/TS4SimRipper/src/GEOM.cs): reads `shaderHash` directly from `.simgeom`
- [TS4SimRipper Resources](../../../references/external/TS4SimRipper/src/Resources): bundled body/head/waist `.simgeom` packet
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md): current local sample packet records `9/9` checked body/head/waist `.simgeom` samples as `SimSkin`

Representative bundled sample targets:

- [cuBodyComplete_lod0.simgeom](../../../references/external/TS4SimRipper/src/Resources/cuBodyComplete_lod0.simgeom)
- [cuHead_lod0.simgeom](../../../references/external/TS4SimRipper/src/Resources/cuHead_lod0.simgeom)
- [yfBodyComplete_lod0.simgeom](../../../references/external/TS4SimRipper/src/Resources/yfBodyComplete_lod0.simgeom)
- [ymBodyComplete_lod0.simgeom](../../../references/external/TS4SimRipper/src/Resources/ymBodyComplete_lod0.simgeom)
- [WaistFiller.simgeom](../../../references/external/TS4SimRipper/src/Resources/WaistFiller.simgeom)

Why this packet matters:

- it gives a direct positive anchor for `SimSkin`
- it still does not surface a peer bundled `SimSkinMask` geometry/export branch
- this makes the current asymmetry concrete instead of leaving it as only a vague corpus impression

## Current negative-result packet

What the current workspace already says safely:

- `tmp/precomp_sblk_inventory.json` has strong central `simskin = 51` signal and weaker but real `SimSkinMask = 12` signal
- `tmp/precomp_shader_profiles.json` repeatedly surfaces both `simskin` and `SimSkinMask` names
- the broader local sample sweep recorded in [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md) still did not surface a wider peer `.simgeom` branch for `SimSkinMask`
- the same source-map packet records a negative code/tool sweep: named `SimSkin` and `SimGlass` branches exist, but no peer named `SimSkinMask` authority/export branch has been found in the current repo plus bundled external tooling snapshot
- the same local `tmp/precomp_*` packet is still only candidate-level archaeology and cannot serve as sole proof for `SimSkin`, because name-guess packets can drift between families

What this does mean:

- current evidence is strong enough to keep `SimSkinMask` below peer GEOM-family status

What this does not mean:

- it does not prove mask semantics are unimportant
- it does not prove `SimSkinMask` can be ignored in skintone or overlay reasoning

## Current candidate live targets

These are queue targets, not proof.

### Candidate group A: bundled baseline packet

Best current baseline:

- the bundled `TS4SimRipper` body/head/waist `.simgeom` set

Why it matters:

- it gives a clean positive packet for `SimSkin`
- it is the best current local reference before searching for a counterexample

### Candidate group B: corpus-side counterexample queue

Useful local clues:

- `tmp/precomp_sblk_inventory.json`: `simskin = 51`
- `tmp/precomp_sblk_inventory.json`: `SimSkinMask = 12`
- `tmp/precomp_shader_profiles.json`: repeated `name = "SimSkinMask"` rows

Safe reading:

- mask-family vocabulary clearly survives in the local corpus
- this is enough to justify continued search
- this is still not enough to claim a peer geometry branch
- the corpus packet must stay subordinate to the stronger `TS4SimRipper` sample-geometry packet

### Candidate group C: wider live/sample search

Best current search target:

- a genuinely new live asset or external sample packet outside the mirrored `TS4SimRipper` resources

Why it is next:

- the current negative result is already strong inside the existing workspace
- another pass over the same bundled samples will not answer the remaining question

## What this packet is trying to prove

Exact target claim:

- if `SimSkinMask` is a real peer geometry family, the next proof must show one concrete live or exported asset branch that preserves it as more than parameter-level or compositor-adjacent vocabulary

Not being proved yet:

- exact authority order between `SimSkin`, embedded `MTNF`, parsed `CASP`, skintone, and compositor layers
- exact mask math
- exact slot contract for all skin-family variants

## Current implementation boundary

Current repo behavior is useful only as a diagnostic boundary:

- current repo already preserves skin-family provenance well enough to keep `SimSkin` visible
- current repo still lacks a dedicated `SimSkinMask` authority branch
- that absence is implementation boundary, not full semantic proof

Diagnostic value of this packet:

- it gives a bounded standard for future `SimSkinMask` claims
- it prevents the docs from inflating repeated corpus names into a fake peer authority node

## Best next inspection step

1. Keep the bundled `TS4SimRipper` sample set as the positive `SimSkin` baseline.
2. Search for one genuinely new live or exported sample outside the mirrored `TS4SimRipper` resources.
3. If such a sample is found, compare whether `SimSkinMask` survives as:
   - a GEOM-side family
   - an export/import branch
   - only mask/compositor-adjacent metadata

## Honest limit

This packet does not yet prove that no peer `SimSkinMask` geometry branch exists anywhere in TS4.

What it does prove:

- the current workspace strongly supports `SimSkin` as the positive baseline family
- the same workspace still does not justify promoting `SimSkinMask` beyond adjacent mask/compositor semantics
