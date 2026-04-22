# SimGlass Character Transparency Boundary

This companion freezes the current external-first reading for narrow character transparency families: `SimGlass` should not be flattened into generic character alpha handling, and `SimAlphaBlended` should not be silently treated as the same thing.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [SimSkin, SimGlass, And SimSkinMask](family-sheets/simskin-simglass-simskinmask.md)
- [SimGlass Domain Home Boundary](simglass-domain-home-boundary.md)
- [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimGlass Character Transparency Boundary
├─ SimGlass versus generic character alpha ~ 91%
├─ SimGlass versus SimAlphaBlended ~ 84%
├─ Creator-facing transparency split ~ 88%
└─ Exact live family slot closure ~ 31%
```

## Current safe rule

Use the current character-transparency packet in this order:

1. explicit external tooling and creator-facing workflows that name a family
2. local snapshots of that tooling
3. lineage or creator corroboration about visible behavior differences
4. current repo behavior only as implementation boundary

Compact rule:

- `SimGlass` is a named character-family branch
- `SimAlphaBlended` is also a named transparency-capable family branch
- current evidence is strong enough to keep them separate in provenance
- current evidence is not yet strong enough to publish a final slot-by-slot contract for both families

## Strongest external packet

Current strongest evidence:

- local external [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs) explicitly names `SimGlass`
- local external [TS4SimRipper ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs) and [PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) preserve a separate glass-oriented handling path
- [CAS Designer Toolkit](https://modthesims.info/d/694549/cas-designer-toolkit.html) explicitly records that on `2026-02-09` it "Added transparency for the SimAlphaBlended shader", which is strong creator-tool evidence that `SimAlphaBlended` is a distinct transparency family worth naming separately
- creator-facing hair conversion guidance on Mod The Sims repeatedly tells creators to set alpha hair parts to `SimGlass` rather than to a generic transparent mode:
  - [Hair conversion gone wrong - Update - still need help](https://modthesims.info/t/541802)
  - [Converting a TS3 hair to TS4 using CAS Tools, Milkshape, and GIMP, plus alpha mesh tricks](https://modthesims.info/showthread.php?t=582345)

Evidence labeling for this packet:

- externally confirmed versus local external snapshot versus bounded synthesis now also lives in [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)

Safe reading:

- `SimGlass` is not just a vague synonym for "transparent"
- `SimAlphaBlended` is also not just noise in a tooling dropdown
- both names currently deserve preserved family provenance until stronger evidence proves they collapse into one identical branch

## What is actually safe to infer

Safe reading:

- `SimGlass` is the current safer family for glass-like or strand-split character transparency workflows
- `SimAlphaBlended` is current evidence for a second named transparency-capable family, not proof that it replaces `SimGlass`
- creator-facing guidance currently uses `SimGlass` as the practical fix for alpha hair and similar split transparency cases
- that does not mean every transparent character mesh is automatically `SimGlass`

Unsafe reading:

- do not say "`SimGlass` and `SimAlphaBlended` are the same family"
- do not say "`SimAlphaBlended` replaces `SimGlass` because it also supports transparency"
- do not collapse either family into one generic "alpha shader" bucket before live-family closure exists

## Cross-domain implication

This companion is only about character transparency semantics.

Safe reading:

- it strengthens the `CAS/Sim` side of the `SimGlass` packet
- it does not promote `Build/Buy` into a semantic home for `SimGlass`
- it does not change the object-side rule that transparent objects should first be classified against object-glass, threshold or cutout, and `AlphaBlended`

## Open gap

- exact live-family boundary between `SimGlass`, `SimAlphaBlended`, `SimEyes`, and other transparency-capable character families in real TS4 assets
