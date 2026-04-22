# Character Transparency Open Edge

This companion freezes the current unresolved edge inside character-transparency family work: `SimGlass` has a stronger packet than the neighboring names, while `SimAlphaBlended` and especially `SimEyes` are not yet proved strongly enough to close the full family order.

Related docs:

- [SimGlass Character Transparency Boundary](simglass-character-transparency-boundary.md)
- [SimGlass Character Transparency Order](simglass-character-transparency-order.md)
- [Character Transparency Evidence Ledger](character-transparency-evidence-ledger.md)
- [Shader Family Registry](shader-family-registry.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Character Transparency Open Edge
├─ Strong SimGlass packet boundary ~ 95%
├─ SimAlphaBlended bounded-but-weaker packet ~ 83%
├─ SimEyes unresolved edge boundary ~ 79%
└─ Full character-transparency family closure ~ 24%
```

## Current safe reading

Current safe order remains:

1. `SimGlass` as the strongest named packet
2. `SimAlphaBlended` as a weaker but still preserved named branch
3. `SimEyes` as an unresolved neighboring edge, not a closed member of the same order yet
4. generic alpha wording only as provisional fallback

## Why this edge remains open

Current local external snapshot packet is asymmetric:

- local external `TS4SimRipper` snapshots in this repo expose an explicit `SimGlass` branch in [Enums.cs](../../references/external/TS4SimRipper/src/Enums.cs), [ColladaDAE.cs](../../references/external/TS4SimRipper/src/ColladaDAE.cs), and [PreviewControl.cs](../../references/external/TS4SimRipper/src/PreviewControl.cs)
- the same checked local external snapshot does not currently surface a peer `SimAlphaBlended` or `SimEyes` branch in the same explicit code-visible way
- creator-facing toolkit evidence still keeps `SimAlphaBlended` alive as a named family
- current packet in hand does not yet give `SimEyes` an equivalently strong creator-tool or local external branch for this exact transparency-order question

Safe reading:

- `SimGlass` currently has the strongest restart-safe packet
- `SimAlphaBlended` should remain preserved because it is still a named creator-tool family
- `SimEyes` should remain an unresolved edge family here until stronger evidence is assembled

Unsafe reading:

- do not promote `SimEyes` into the same closed order just because it is another character-special family name
- do not demote `SimAlphaBlended` to generic alpha noise only because the local external snapshot is weaker than the `SimGlass` packet

## What this changes operationally

Safe reading:

- current docs may use `SimGlass` confidently where the stronger packet survives
- current docs may keep `SimAlphaBlended` as a named secondary possibility
- current docs should treat `SimEyes` as an open neighboring family rather than as evidence for the same transparency rule

## Open gap

- stronger external or local external snapshot proving where `SimEyes` sits relative to `SimGlass`, `SimAlphaBlended`, and generic character alpha in real TS4 family order
