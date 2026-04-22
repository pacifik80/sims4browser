# ShaderDayNight Runtime Context Gap

This packet records the current ceiling of the checked-in DX11 runtime captures for `ShaderDayNightParameters`.

Question:

- do the current checked-in broad captures already separate the leading `F04` candidate from the nearest `F05` sibling by scene or context strongly enough to promote a context-bound family reading?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [ShaderDayNightParameters](../family-sheets/shader-daynight-parameters.md)
- [ShaderDayNight Runtime Cluster Candidate Floor](shader-daynight-runtime-cluster-candidate-floor.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)
- [shaderdaynight runtime context gap snapshot](../../../tmp/shaderdaynight_runtime_context_gap_snapshot_2026-04-22.json)

## Scope status (`v0.1`)

```text
ShaderDayNight Runtime Context Gap
├─ Externally proved family identity ~ 68%
├─ Broad-capture persistence floor ~ 84%
├─ F04-versus-F05 parity ceiling ~ 81%
├─ Context-tagged capture availability ~ 31%
└─ Exact scene-bound family ownership ~ 12%
```

## What this packet is for

The previous runtime-cluster packet already narrowed the helper-family route from:

- broad `F03/F04/F05`

to:

- `F04` first
- `F05` second

This packet answers the next narrower question:

- whether the current checked-in captures already contain enough scene/context separation to go farther without another tagged capture run

## Local snapshot of external tooling

Current bounded snapshot:

- `tmp/shaderdaynight_runtime_context_gap_snapshot_2026-04-22.json`

Broad comparison captures checked here:

- `20260421-212139`
- `20260421-212533`
- `20260421-220041`

Useful checked-in comparison layer:

- [compare-20260421-212533-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/compare-20260421-212533-vs-20260421-220041.md)
- [group-compare-broad-vs-20260421-220041.md](../../../satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md)

## What the current broad captures do prove

Representative `F04` hashes persist across all three checked broad captures with the same simple support counts:

- `03ffb5addf1935b1acb49c3991e4b45773c97f4537624210e22f59db09f09bcf = 3 / 3 / 3`
- `042d682be7ff6682204d8ef27c2ebb052a35f82e2d8f4e089357ae0147b62ee1 = 3 / 3 / 3`
- `05f6262aaca21ffe3630fa2f4fb44aa86175b4600537df79aa8f211fc8117e66 = 1 / 1 / 1`

Representative `F05` hashes do the same:

- `061a0ce8873ead820ae520ce37b69f96216f567dc368e32fc23ee3fd5fcfaa11 = 3 / 3 / 3`
- `09f989acd7950fc679918789eeba72fac1ce69c9fb8603612edeb315d59535b1 = 3 / 3 / 3`
- `130c0fbf3466ef5656c9824d6baa1ee24418a4dae62984c18dd49b403f8b6e2b = 3 / 3 / 3`

Safe reading:

- both candidate clusters are real recurring members of the current broad runtime surface
- the current broad sessions preserve candidate stability
- the current broad sessions do not yet create a capture-level split between those candidate families

## What the current broad captures do not prove

Current manifests still expose runtime/session metadata only:

- session id
- timestamps
- binary paths
- frame count

They do not currently carry:

- scene labels
- capture-purpose labels
- `Build/Buy` versus `CAS` tags
- lighting-heavy versus reveal-heavy labels

Safe reading:

- the checked-in capture corpus is broad and useful
- it is not yet context-tagged enough to bind `F04` to one `ShaderDayNightParameters` scene class ahead of `F05`

## Why this matters

Without this packet, the next move still looks like:

- inspect more of the same broad captures

With this packet, the next move is narrower and more honest:

- stop expecting broad untagged sessions to close scene-specific helper-family ownership
- keep the current cluster narrowing
- require one context-tagged capture step before stronger promotion

## Best next inspection step

1. Keep [ShaderDayNightParameters Visible-Pass Proof](shader-daynight-visible-pass.md) as the visible-root floor.
2. Keep [ShaderDayNight Runtime Cluster Candidate Floor](shader-daynight-runtime-cluster-candidate-floor.md) as the `F04`-first runtime narrowing.
3. Run one lighting-heavy or reveal-aware context-tagged capture and check `F04` before `F05`.

## Honest limit

This packet does not yet prove:

- which scene or pass owns `F04`
- which scene or pass owns `F05`
- exact `ShaderDayNightParameters` pass order or math

What it does prove:

- the checked-in broad runtime corpus already has a real ceiling
- that ceiling is not lack of runtime data in general
- that ceiling is lack of scene/context tagging needed to separate the `F04`-first route from the `F05` sibling route
