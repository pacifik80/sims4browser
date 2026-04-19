# SimSkin, SimGlass, And SimSkinMask

This sheet isolates the narrow `CAS/Sim` GEOM-side shader-family seam between baseline skin meshes, glass-like meshes, and mask-adjacent skin workflows.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](../cas-sim-material-authority-matrix.md)
- [Skintone And Overlay Compositor](../skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
SimSkin / SimGlass / SimSkinMask
├─ SimSkin identity packet ~ 81%
├─ SimGlass identity packet ~ 76%
├─ SimSkinMask negative-result packet ~ 68%
├─ Exact authority order against MTNF/CASP ~ 41%
└─ Full slot/compositor contract ~ 27%
```

## Evidence order

Use this family seam in the following order:

1. external creator tooling that explicitly names a GEOM-side shader family
2. bundled local snapshots of that external tooling
3. creator-facing workflows that show how skin masks are actually used
4. current repo behavior only as implementation boundary

## Externally proved packet

### `SimSkin`

Strongest evidence:

- local snapshot [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimSkin = 0x548394B9`
- local snapshot [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) treats `SimSkin` as a normal export texture family
- bundled `.simgeom` resources in [TS4SimRipper Resources](../../../references/external/TS4SimRipper/src/Resources) provide the current local sample packet for body/head/waist geometry
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) keeps `SimSkin` as a distinct shader family in the same engine lineage rather than a generic material alias
- [Sims_3:0x015A1849](https://modthesims.info/wiki.php?title=Sims_3%3A0x015A1849) explicitly ties morphskin-family geometry handling to the `SimSkin` shader hash in lineage documentation
- public `TS4SimRipper` repo: [GitHub](https://github.com/CmarNYC-Tools/TS4SimRipper)

Safe reading:

- `SimSkin` is a real GEOM-side skin-family shader branch
- it is the current safest baseline family for body/head shell mesh reasoning
- it should survive in provenance even when higher material layers are still only approximately decoded

Unsafe reading:

- do not treat `SimSkin` as proof that all skin rendering semantics are already solved
- do not assume `SimSkin` alone overrides `CASP`, `MTNF`, skintone, or compositor inputs

### `SimGlass`

Strongest evidence:

- local snapshot [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimGlass = 0x5EDA9CDE`
- local snapshot [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) uses a separate glass-oriented export suffix
- local snapshot [TS4SimRipper PreviewControl.cs](../../../references/external/TS4SimRipper/src/PreviewControl.cs) tracks `SimGlass` meshes separately in preview grouping
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) keeps `simglass` as a distinct family with its own refraction, overlay, and UV-related parameter packet
- public `TS4SimRipper` repo: [GitHub](https://github.com/CmarNYC-Tools/TS4SimRipper)

Safe reading:

- `SimGlass` is a real narrow transparent or glass-like mesh family
- it should not be flattened into `SimSkin`
- it should also not be flattened into a generic “alpha” fallback without preserving family identity

Unsafe reading:

- do not assume all transparent character content is `SimGlass`
- do not assume that exact slot semantics for `SimGlass` are already externally closed

## Mask-adjacent packet

### `SimSkinMask`

Strongest evidence:

- current creator-facing skin workflows keep masks inside skintone, overlay, burn-mask, or skin-detail logic rather than as a separate geometry-family branch
- [TS4 Skininator](https://modthesims.info/d/568474) exposes burn masks and skintone-adjacent mask workflows
- [Please explain Skin Overlay, Skin Mask, Normal Skin, Etc](https://modthesims.info/t/594620) reflects the same creator-facing distinction between overlays, masks, and normal skins
- recent Sims 4 Studio notes already fit the same pattern: mask-bearing skin content lives inside image or overlay workflows, not a standalone mesh-family pipeline
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) and [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) reinforce the general lineage point that mask-like or alpha-like inputs can be family-local material helpers without implying a separate mesh-family branch

Safe reading:

- `SimSkinMask` is currently safer to treat as adjacent skin-family semantics than as a proven standalone GEOM-side family
- the burden of proof is on any future claim that it is a peer geometry or export family next to `SimSkin` or `SimGlass`

Current negative result:

- the current local external-tool packet exposes named `SimSkin` and `SimGlass` branches
- it does not expose a peer `SimSkinMask` geometry or export branch

What this negative result does mean:

- current evidence is insufficient to promote `SimSkinMask` into a standalone geometry-family authority node

What this negative result does not mean:

- it does not prove `SimSkinMask` is unimportant
- it does not prove mask semantics are optional in skin rendering

## Current repo boundary

Current repo behavior is useful only as an implementation boundary:

- current repo paths already preserve some skin-family provenance and material-routing context
- current repo does not yet expose a dedicated `SimSkinMask` authority branch
- current repo still cannot be treated as proof of exact family ranking against `CASP`, embedded `MTNF`, or skintone/compositor logic

Safe wording:

- “current implementation preserves `SimSkin` and `SimGlass` as meaningful names”
- “current implementation still lacks a dedicated `SimSkinMask` branch”

Unsafe wording:

- “`SimSkinMask` is not a real family because our code does not handle it”

## Open questions

- how often live body/head shell assets outside the bundled sample packet really use `SimSkin` as their GEOM-side family
- whether wider live assets ever surface a true peer `SimSkinMask` geometry or export branch
- exact authority order between GEOM-side shader family, embedded `MTNF`, parsed `CASP` routing, and skintone/compositor layers
- exact slot and blend behavior for `SimGlass`

## Recommended next work

1. Add a wider live sample packet outside the bundled `TS4SimRipper` resources.
2. Compare the same shell family through:
   - GEOM-side shader identity
   - embedded `MTNF`, when present
   - parsed `CASP` fields
   - skintone/compositor overlays
3. Keep `SimSkinMask` in the skintone or overlay packet until a real peer geometry-family branch is found.
