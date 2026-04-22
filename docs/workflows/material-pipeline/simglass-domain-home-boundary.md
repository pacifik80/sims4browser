# SimGlass Domain Home Boundary

This companion freezes one cross-domain continuity rule for `SimGlass`: the family's current semantic home is `CAS/Sim`, while `Build/Buy` remains only a bounded carry-over evidence domain.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)
- [SimGlass Build/Buy Evidence Order](simglass-buildbuy-evidence-order.md)
- [Object Glass And Transparency](family-sheets/object-glass-and-transparency.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimGlass Domain Home Boundary
├─ CAS/Sim semantic home ~ 93%
├─ Build/Buy carry-over boundary ~ 95%
├─ Cross-domain family continuity ~ 89%
└─ First live cross-domain closure ~ 34%
```

## Current safe rule

Use `SimGlass` in this order:

1. external creator tooling and creator-facing `CAS/Sim` guidance define the family floor
2. local snapshots of that external tooling may reinforce the same family floor
3. `Build/Buy` may keep a bounded carry-over branch alive
4. only reopened `Build/Buy` fixture evidence may decide whether that carry-over branch survives in a specific object-side case

Compact rule:

- `CAS/Sim` is the current semantic home for `SimGlass`
- `Build/Buy` is not a co-equal semantic root for `SimGlass`
- one shared shader/material contract still applies after authoritative inputs are chosen

## Why `CAS/Sim` is the current semantic home

Strongest current packet:

- local external [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs) explicitly names `SimGlass`
- local external [TS4SimRipper ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs) exports `SimGlass` through a distinct glass-oriented path
- local external [TS4SimRipper PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) keeps `SimGlass` meshes grouped separately in preview
- creator-facing transparency and glasses workflows for Sims content still treat glass-like character parts as a special family rather than as one generic transparent bucket
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) preserves `simglass` as a distinct engine-lineage family, which strengthens the safer reading that TS4 `SimGlass` is not just an object-side transparency alias

Safe reading:

- `SimGlass` currently enters the docs as a character-family identity first
- object-side evidence may extend that family into bounded edge cases, but does not currently redefine its home

## Why `Build/Buy` is only bounded carry-over evidence

Current safe reading:

- transparent `Build/Buy` objects must first be classified against object-side families such as `GlassForObjectsTranslucent`, threshold or cutout transparency, and `AlphaBlended`
- `Build/Buy` survey counts and route packets are enough to keep a `SimGlass` branch alive
- they are not enough to promote `Build/Buy` into a second semantic home for the family
- only reopened, classified fixture evidence may end as `SimGlass` loss, provisional `SimGlass`, or winning `SimGlass`

What this does prevent:

- treating transparent object naming as proof that `SimGlass` is an object-default family
- treating aggregate `Build/Buy` survey presence as equivalent to creator-facing `CAS/Sim` family identity
- letting object-side package patterns rewrite a character-family definition

## Shared shader contract boundary

This companion is not a reason to create separate shader systems.

Safe reading:

- `CAS/Sim` and `Build/Buy` are still different discovery and authority domains
- they are not different shader domains for `SimGlass`
- once authoritative inputs are chosen, both paths still feed the same shared shader/material contract

## Safe wording

Safe wording:

- "`SimGlass` is currently a `CAS/Sim`-rooted family with bounded `Build/Buy` carry-over evidence"
- "`Build/Buy` may surface `SimGlass`, but does not currently define the family"

Unsafe wording:

- "`SimGlass` is equally native to `Build/Buy` and `CAS/Sim`"
- "`Build/Buy` transparent routes are enough to redefine what `SimGlass` means"

## Open gap

- first fixture-grade `Build/Buy` case that survives object-side transparent branches strongly enough to count as real cross-domain `SimGlass` carry-over
