# Build/Buy MTST State-Selector Structure

This packet closes the next narrow `MTST` gap after the default-state and portable-state notes: not named swatch closure, but structural proof that `stateHash` selectors are stable object-side material keys rather than one-off preview noise.

Question:

- does the current workspace already prove that `MTST` state hashes behave like persistent structural selectors across multiple `Build/Buy` fixtures, even though their gameplay semantics are still unknown?

Related docs:

- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](../buildbuy-stateful-material-set-seam.md)
- [Build/Buy MTST Default-State Boundary](buildbuy-mtst-default-state-boundary.md)
- [Build/Buy MTST Portable-State Delta](buildbuy-mtst-portable-state-delta.md)
- [Material Pipeline Deep Dives](../README.md)
- [Live-Proof Packets](README.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
Build/Buy MTST State-Selector Structure
├─ Cross-fixture state-hash stability ~ 88%
├─ Repeated per-state MATD mapping ~ 91%
├─ unknown0 split boundary ~ 79%
├─ Runtime-state semantics ~ 23%
└─ Full swatch/object closure ~ 18%
```

## Externally proved authority baseline

What is already strong enough:

- [The Sims 4 Modders Reference: File Types](https://thesims4moddersreference.org/reference/file-types/) describes `Material Set` as sets of `Material Definitions`
- [Sims_4:0x01D10F34](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34) keeps `MLOD` as the mesh-group layer that points to `MATD` or `MTST`
- [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL) keeps `MODL`, `MLOD`, `MATD`, and `MTST` in one object-side scenegraph packet
- [Info | COBJ/OBJD resources](https://modthesims.info/t/551120) and the [EA material-variant thread](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695) still keep state or variant choice inside the object-side model/material chain

Safe reading:

- `MTST` is already a real object-side authority seam
- this packet only asks whether the current local fixtures prove structural selector behavior strongly enough to narrow the next work
- it does not need to decode exact gameplay meaning for each selector

## Current local fixtures

The packet relies on the two already-promoted model-root fixtures:

- `EP01\ClientDeltaBuild0.package | Build/Buy Model 002211BA8D2EE539`
  - root: `01661233:00000000:002211BA8D2EE539`
  - source artifact: `tmp/probe_002211_after.txt`
- `EP10\ClientFullBuild0.package | Build/Buy Model 05773EECEE557829`
  - root: `01661233:00000000:05773EECEE557829`
  - source artifact: `tmp/probe_0577_after_heuristic_filter.txt`

Both fixtures remain model-rooted only:

- exact `COBJ/OBJD` swatch identity is still not closed
- that is an honest limit, not a reason to discard their structural evidence

## What the current local evidence proves

### 1. `002211...` exposes a repeated five-state selector lattice

The first fixture is no longer only a default-state floor.

In `tmp/probe_002211_after.txt` the same five `stateHash` values repeat as a stable lattice:

- `0x00000000`
- `0xBA5CC973`
- `0xD7B49960`
- `0xDE5CF5D6`
- `0xF4BD1CE9`

High-signal ranges:

- `2908-3285`
- `3332-3700`
- `7474-7851`
- `7898-8266`

Inside those ranges, each hash appears twice:

- once with `unknown0=0x00000000`
- once with `unknown0=0xC3867C32`

Each paired entry still resolves to a distinct `MATD`, for example:

- `stateHash=0x00000000 -> 14EA73B2460329CC` and `05CE9D029E21A488`
- `stateHash=0xBA5CC973 -> 14072EDAA14F544E` and `5597A5F63A610782`
- `stateHash=0xF4BD1CE9 -> F49D1179D660F9B0` and `779D59285ECB890C`

Safe reading:

- this is not one flat material packet
- the state hashes repeat too cleanly to treat them as incidental probe noise
- `unknown0=0xC3867C32` is structurally meaningful because it repeats with every non-default partner, but its exact meaning is still unknown

### 2. `0577...` proves the selector model survives into a stronger portable-material case

The second fixture does not just prove portable deltas.

In `tmp/probe_0577_after_heuristic_filter.txt`:

- lines `764-856` and `1798-1890` repeat the same two-state structure
- `stateHash=0x00000000` and `stateHash=0xF4BD1CE9` each map to stable `MATD` entries
- `Material_B45072B0` stays `source=MaterialSet` and `textures=2`
- the same two selector states reappear in the material summary at lines `998` and `2032`

The portable delta remains the stronger visible seam:

- `AmbientDomeBottom` changes between `word[3]=0xEE557829` and `word[3]=0xD6060725`
- `CloudColorWRTHorizonLight1` changes between `vector=[0, 0, 0, 0]` and `vector=[-0, 0, 0, 0]`

Safe reading:

- `0xF4BD1CE9` is not unique to the default-state-floor packet
- at least one selector hash survives across fixtures and into a more portable, texture-bearing material case
- this makes the state-hash layer look like reusable selector structure, not a local accident tied to one weak fixture

### 3. The shared structure is now narrower than “find any `MTST` packet”

Across the two fixtures, the current workspace already proves:

- repeated `stateHash -> MATD` mapping
- repeated state-score summaries
- one shared hash (`0xF4BD1CE9`) across the weak and stronger fixtures
- one repeated paired-flag split in `002211...` through `unknown0=0x00000000` versus `0xC3867C32`

That is enough to promote the next safe claim:

- `MTST` state hashes should currently be treated as structural selector tokens in the object-side material chain
- they should not be flattened into one anonymous fallback material, even when their gameplay meaning is still unresolved

## What this packet does not prove

It does not yet prove:

- exact semantic meaning of `0x00000000`, `0xBA5CC973`, `0xD7B49960`, `0xDE5CF5D6`, or `0xF4BD1CE9`
- that any one of those hashes is definitely “burned”, “dirty”, “broken”, or a named swatch state
- exact meaning of `unknown0=0xC3867C32`
- full `COBJ/OBJD` swatch closure for either fixture
- that every stateful `Build/Buy` family uses the same selector lattice

Safe reading:

- the structural selector layer is now real enough to preserve
- the semantic label layer is still open

## Current implementation boundary

Current repo behavior is useful here as boundary evidence only:

- it surfaces the repeated selector structure instead of collapsing everything to one implicit `MATD`
- it preserves that `0577...` has portable state deltas and `002211...` mostly has non-portable control-property deltas
- it does not tell us what the state hashes mean in the actual game

## Exact target claim for this packet

- the current workspace already proves that `MTST` state hashes behave like persistent structural selectors across multiple `Build/Buy` model-root fixtures, even though their runtime semantics and swatch labels remain unresolved

## Best next step after this packet

The next packet should no longer re-prove repeated state-hash structure.

It should close one stronger follow-up:

1. a named `COBJ/OBJD` or `MaterialVariant` fixture where one selector state can be tied to explicit swatch/object identity, or
2. a runtime-state fixture where one selector can be tied to visible gameplay semantics, or
3. a family where the repeated `unknown0=0xC3867C32` split can be interpreted with stronger external support

## Honest limit

What this packet proves:

- the `MTST` seam now has a third bounded closure layer beyond “default state exists” and “portable state delta exists”
- selector hashes repeat structurally across multiple fixtures
- `002211...` carries a stable five-state paired lattice
- `0577...` carries a stable two-state texture-bearing portable branch that shares `0xF4BD1CE9` with the first fixture

What remains open:

- exact selector semantics
- exact meaning of the paired `unknown0` flag
- named swatch/object identity for either fixture
