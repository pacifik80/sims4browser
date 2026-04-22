# SimGlass Character Transparency Order

This companion freezes the current external-first order for narrow character-transparency families so later passes do not fall back to one vague "transparent sim shader" story.

Related docs:

- [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
- [SimGlass Domain Home Boundary](simglass-domain-home-boundary.md)
- [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)
- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimGlass Character Transparency Order
├─ SimGlass priority over generic alpha ~ 94%
├─ SimAlphaBlended as separate named branch ~ 86%
├─ Generic alpha as provisional fallback only ~ 92%
└─ Exact live asset family order ~ 28%
```

## Current safe order

Use current character-transparency claims in this order:

1. `SimGlass` when external tooling and creator workflows explicitly point to the glass-like strand-split family
2. `SimAlphaBlended` when creator tooling explicitly preserves it as a named transparency-capable family
3. generic character alpha only as a provisional fallback when no named family survives strongly enough

Current safe reading:

- `SimGlass` currently has the strongest external packet
- `SimAlphaBlended` is strong enough to preserve as a separate named branch
- generic alpha remains a fallback description, not a family identity

Evidence labeling:

- what is directly source-backed versus bounded synthesis is now split explicitly in [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)

## Why `SimGlass` currently sits first

Current strongest packet:

- local external [TS4SimRipper Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs), [ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs), and [PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs) preserve `SimGlass` as an explicit code-visible family with separate handling
- creator-facing Mod The Sims guidance repeatedly uses `SimGlass` as the practical family choice for alpha hair and similar split transparency cases:
  - [Hair conversion gone wrong - Update - still need help](https://modthesims.info/t/541802)
  - [Converting a TS3 hair to TS4 using CAS Tools, Milkshape, and GIMP, plus alpha mesh tricks](https://modthesims.info/showthread.php?t=582345)

Safe reading:

- `SimGlass` is the current best-supported named family for glass-like or strand-split character transparency
- if later live assets contradict that, the contradiction must beat this packet rather than bypass it with generic wording

## Why `SimAlphaBlended` stays separate

Current strongest packet:

- [CAS Designer Toolkit](https://modthesims.info/d/694549/cas-designer-toolkit.html) explicitly records on `2026-02-09` that it "Added transparency for the SimAlphaBlended shader"
- current source-map packet already preserves `SimAlphaBlended` among visible family-level choices in creator-facing TS4 shader practice

Safe reading:

- `SimAlphaBlended` is a real transparency-capable family name in current creator tooling
- current evidence is not strong enough to demote it into generic alpha noise
- current evidence is also not strong enough to say it overrides or replaces `SimGlass`

## Why generic alpha stays last

Safe reading:

- generic character alpha is still useful as provisional descriptive language
- it should only appear after named families fail to survive the evidence order strongly enough
- it should not erase `SimGlass` or `SimAlphaBlended` provenance just because exact slot math is still open

Unsafe reading:

- do not write "transparent character content uses alpha" when the stronger safe statement is "`SimGlass`" or "`SimAlphaBlended` remains possible"
- do not collapse named families into generic alpha because current repo rendering is approximate

## Open gap

- exact live-asset authority order among `SimGlass`, `SimAlphaBlended`, `SimEyes`, and generic alpha-like character cases
