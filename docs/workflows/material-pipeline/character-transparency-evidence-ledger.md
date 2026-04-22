# Character Transparency Evidence Ledger

This ledger separates four layers for the current character-transparency packet:

1. externally confirmed
2. local snapshot of external tooling
3. bounded Codex synthesis
4. still-open questions

Related docs:

- [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
- [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
- [Character Transparency Open Edge](character-transparency-open-edge.md)
- [Shader Family Registry](shader-family-registry.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Character Transparency Evidence Ledger
├─ External confirmation separation ~ 94%
├─ Local external snapshot separation ~ 96%
├─ Codex synthesis boundary ~ 92%
└─ Remaining open questions ~ 88%
```

## Ledger

### 1. Externally confirmed

These are directly supported by external creator-facing or tooling sources:

- `SimAlphaBlended` is a named transparency-capable shader family in current creator tooling.
  - Source: [CAS Designer Toolkit](https://modthesims.info/d/694549/cas-designer-toolkit.html)
  - Evidence: the changelog explicitly says that on `2026-02-09` it "Added transparency for the SimAlphaBlended shader".

- `SimGlass` is actively used by creators as the practical family choice for alpha hair or similar split transparency cases.
  - Sources:
    - [Hair conversion gone wrong - Update - still need help](https://modthesims.info/t/541802)
    - [Converting a TS3 hair to TS4 using CAS Tools, Milkshape, and GIMP, plus alpha mesh tricks](https://modthesims.info/showthread.php?t=582345)
  - Evidence: both threads explicitly direct creators to use `SimGlass` for those transparency-bearing hair cases.

### 2. Local snapshot of external tooling

These are not repo-truth claims. They are checked-in local copies of external tools:

- the checked-in `TS4SimRipper` snapshot explicitly exposes `SimGlass` as a named code-visible branch.
  - Local snapshot files:
    - [Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs)
    - [ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs)
    - [PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs)

- in the currently checked local snapshot, there is no equally explicit peer branch in hand for `SimAlphaBlended` or `SimEyes`.
  - This is a negative result about the current snapshot only.
  - It is not proof that those families do not exist elsewhere.

### 3. Bounded Codex synthesis

These points are synthesis from the evidence above. They are not direct source quotes:

- `SimGlass` currently has the strongest restart-safe packet for character transparency.
- `SimAlphaBlended` should stay preserved as a weaker named branch rather than being collapsed into generic alpha wording.
- generic character alpha should stay a provisional fallback description, not a family identity.
- `SimEyes` should stay outside the currently closed `SimGlass` / `SimAlphaBlended` order until stronger evidence appears.

Reason this synthesis is bounded:

- it combines creator-facing external sources with the local snapshot of an external tool
- it does not claim exact slot math, exact live family frequency, or a final in-game renderer order

### 4. Still open

These are not confirmed yet:

- exact live-asset order between `SimGlass`, `SimAlphaBlended`, and `SimEyes`
- exact slot-level contract for each of those families
- whether stronger external or live-asset evidence will eventually place `SimEyes` inside the same narrow transparency order or keep it separate
