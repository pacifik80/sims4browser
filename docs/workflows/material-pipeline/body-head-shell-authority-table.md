# Body And Head Shell Authority Table

This document turns the current `CAS/Sim` shell evidence into the first explicit table for how body shell and head shell should be ranked in the material-selection chain.

Related docs:

- [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
- [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
- [SimSkin Body/Head Shell Authority](live-proof-packets/simskin-body-head-shell-authority.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Sim Body-Shell Contract](../../sim-body-shell-contract.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Body And Head Shell Authority Table
├─ Body shell row ~ 88%
├─ Head shell row ~ 84%
├─ Body-versus-head merge rule ~ 79%
├─ Post-shell modifier boundary ~ 76%
└─ Exact material-source ranking ~ 55%
```

## What this table is for

- make the current body/head shell reading explicit enough for implementation work
- keep body shell, head shell, and post-shell layers from collapsing into one vague `CAS` bucket
- give one place where the current docs and the current implementation boundary say the same thing

## Current authority table

| Question | Body shell | Head shell | Current safe rule |
| --- | --- | --- | --- |
| Main role | assembly anchor | mergeable sibling branch | body shell is the base; head shell may be added on top |
| Typical slot or body type | `Full Body`, `Body`, body-type `5`; body recipe may also include `Top`, `Bottom`, `Shoes` as allowed body layers | `Head`, body-type `3` | keep `Head` separate from torso/body shell even when both are part of one assembled Sim |
| Best current selection signal | `nakedLink`, `defaultBodyType`, gender-specific `defaultBodyTypeFemale` / `defaultBodyTypeMale`, plus compatibility with species/age/gender | exact head-part link from `SimInfo` when present; otherwise only approximate head candidate | body shell is allowed to come from default or nude body recipe; head shell should prefer an exact head-part selection |
| Geometry-family reading | current baseline is `SimSkin` | current baseline is also `SimSkin` unless a narrower family is proved | preserve `SimSkin` before discussing later material or compositor steps |
| Material-input chain after selection | linked `GEOM`, then embedded `MTNF` / explicit material resources / parsed `CASP` fields | same chain after head shell is accepted | body/head shell share one material chain after they are selected; they do not define separate shader systems |
| `RegionMap` role | post-selection modifier on shell-compatible targets | same role on accepted head targets | `RegionMap` refines selected shell materials; it does not choose the shell |
| Skintone role | shell-scoped refinement after shell-compatible targets are known | same, but only if head shell is accepted into the assembly | skintone comes after shell selection, not instead of it |
| Overlay/detail families | not part of the anchor itself | not part of the anchor itself | makeup, tattoos, face paint, skin details, and similar layers stay outside shell identity |
| If the layer is missing | no assembled body/head result can be trusted without a renderable body shell | current assembly stays body-only | body shell is mandatory; head shell is optional |
| If rig basis does not match | body shell stays accepted | head shell is withheld | a rig mismatch falls back to body-only assembly rather than forcing a bad merge |

## Current evidence anchors

### Direct local shell floor

The current narrow direct floor is now frozen in:

- [body_head_shell_authority_snapshot_2026-04-21.json](../../tmp/body_head_shell_authority_snapshot_2026-04-21.json)
- [sim_archetype_body_shell_audit_fresh.json](../../tmp/sim_archetype_body_shell_audit_fresh.json)

Current strongest body-shell rows:

- `Full Body` / `body_type = 5` -> `6276` rows with `naked-link = 3406`
- `Top` / `body_type = 6` -> `9287` rows with `naked-link = 3715`
- `Bottom` / `body_type = 7` -> `6191` rows with `naked-link = 2943`
- the graph-backed archetype audit still keeps:
  - `FullBodyShell = 23`
  - `SplitBodyLayers = 12`
  - `ActiveLayers: Full Body = 23, Top = 12, Bottom = 12, Shoes = 12`

Current strongest head-shell rows:

- `Head` / `body_type = 3` -> `90` rows
- `composition=0 = 88`
- `default rows = 4`
- `naked-link rows = 0`
- the template-body-part subset still preserves a distinct exact head lane:
  - `Head = 5` rows
  - all current head-bearing template rows are human `SimInfo template` assets

Safe reading:

- body shell stays broader and more body-driving on the current direct layers
- head shell stays narrower and more exact-slot-like
- that direct split is consistent with the current table:
  - body shell anchors assembly
  - head shell merges when accepted
  - post-shell modifiers remain outside shell identity

### Body shell

Current implementation boundary:

- preferred default body-shell candidates already use `HasNakedLink`, `DefaultForBodyType`, and gender-specific default flags in [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- the current body-first slot order puts `Full Body` and `Body` ahead of `Head`, `Top`, `Bottom`, and `Shoes` in the same file
- the current project contract in [Sim Body-Shell Contract](../../sim-body-shell-contract.md) keeps initial body-shell preview inside body-recipe layers only

Useful local anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [Sim Body-Shell Contract](../../sim-body-shell-contract.md)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

### Head shell

Current implementation boundary:

- current body-assembly graph already gives `Head shell` its own node and distinguishes:
  - exact head-part selection
  - approximate head candidate
  - no dedicated head shell yet
- current scene assembly keeps the body shell when head is missing
- current scene assembly also keeps the body shell when the body/head rig basis is a definitive mismatch

Useful local anchors:

- [AssetServices.cs](../../src/Sims4ResourceExplorer.Assets/AssetServices.cs)
- [SimSceneComposer.cs](../../src/Sims4ResourceExplorer.Core/SimSceneComposer.cs)
- [ExplorerTests.cs](../../tests/Sims4ResourceExplorer.Tests/ExplorerTests.cs)

## Current assembly rule

The safest current rule is:

```text
body shell selected first
        ->
head shell checked as separate input
        ->
accept head only if a renderable scene exists and the rig basis is acceptable
        ->
run skintone and other post-selection material passes on the accepted shell targets
```

What this means:

- body shell is the anchor
- head shell is real and important, but it does not outrank the body shell
- post-selection material passes should not be described as if they choose the body shell or the head shell

## Honest limit

This table is not yet the final in-game order for every case.

It does not yet prove:

- exact ranking between embedded `MTNF`, parsed `CASP` routing, and explicit material definitions
- exact exceptions for occults, animals, or patch-specific character families
- exact blend and ordering math for overlay/detail layers

## Recommended next work

1. Use this table and the new direct shell-floor snapshot to tighten the body-shell and head-shell rows inside the main `CAS/Sim` matrix.
2. Keep the next narrow follow-up on compositor metadata:
   - `CompositionMethod`
   - `SortLayer`
3. Keep the sibling `Hair` / `Accessory` / `Shoes` table paired with this one so the body/head anchor does not collapse back into worn-slot or compositor vocabulary.
