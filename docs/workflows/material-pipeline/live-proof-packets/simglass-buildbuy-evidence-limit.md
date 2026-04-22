# SimGlass Build/Buy Evidence Limit

This packet freezes the current ceiling on `Build/Buy` readings of `SimGlass`.

Question:

- what is the strongest safe statement the current workspace can make about `SimGlass` inside `Build/Buy` data without overpromoting aggregate survey presence into family truth?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [SimGlass Versus Shell Baseline](simglass-vs-shell-baseline.md)
- [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)
- [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
SimGlass Build/Buy Evidence Limit
├─ External semantic ceiling ~ 93%
├─ Aggregate-survey reading boundary ~ 94%
├─ Candidate-anchor overread prevention ~ 91%
├─ Stable fixture requirement ~ 92%
└─ Row-level family closure ~ 18%
```

## Externally proved ceiling

What is already strong enough:

- external creator-facing guidance ties `SimGlass` to thin transparent layered content such as transparent clothing parts, glasses, lashes, and hair-adjacent transparency cases
- local external `TS4SimRipper` snapshots keep `SimGlass` as a real family through enum naming, preview grouping, and export naming
- object-side transparency now has its own external-first semantic split:
  - `GlassForObjectsTranslucent`
  - threshold/cutout transparency via `AlphaMap` plus `AlphaMaskThreshold` or `AlphaThresholdMask`
  - `AlphaBlended`

Safe reading:

- `SimGlass` is real
- `SimGlass` is not the default semantic home for transparent `Build/Buy` content
- any `Build/Buy` `SimGlass` reading must now beat the stronger object-side transparent branches, not bypass them

## What the local Build/Buy packet does prove

Current local evidence:

- `tmp/probe_all_buildbuy_summary_full.json` currently records `"SimGlass": 5`
- the same summary comes from `1380` resolved Build/Buy scene entries, which makes it stronger than a one-row precompiled archaeology hit
- the broader `EP10` transparent-decor cluster still provides the best narrowed search route:
  - `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
  - `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
  - `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
  - `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
  - `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
- those transformed roots are present in `tmp/probe_all_buildbuy.txt`

Safe reading:

- the workspace has real `Build/Buy`-side `SimGlass` presence signals
- the workspace has a narrowed route for where a future row-level fixture might come from
- this is enough to keep `SimGlass` alive as a bounded Build/Buy possibility

## What the local Build/Buy packet does not prove

Current non-claims that must stay explicit:

- aggregate survey presence does not prove that the detected rows are semantically the same as character-side `SimGlass`
- aggregate survey presence does not outrank the external object-side transparent split
- transformed-root presence in `tmp/probe_all_buildbuy.txt` does not prove reopenability
- candidate resolution does not prove stable object identity
- companion-bundle integrity does not prove family classification
- none of the current `EP10` transparent-decor roots is yet a stable reopenable fixture

Current hard boundary:

- direct reopen attempts on the current transformed roots still fail with `Build/Buy asset not found`
- until one root reopens cleanly, the cluster remains a search-anchor route rather than family closure

Safe reading:

- `SimGlass = 5` is aggregate-only evidence
- the transparent-decor cluster is route evidence
- neither one is yet proof that a concrete `Build/Buy` fixture belongs under `SimGlass`

## Promotion threshold for a Build/Buy SimGlass reading

The first `Build/Buy` fixture may stay under `SimGlass` only if all of the following survive:

1. stable reopen
2. restart-safe object identity
3. material-candidate inspection strong enough to classify the fixture
4. explicit failure of stronger object-side branches to explain the fixture better
5. a remaining reading that is still narrower than generic transparent fallback

What must happen before promotion:

- classify the reopened fixture against object-side glass/transparency first
- record which stronger object-side branches were considered and why they lost
- keep `SimGlass` as the winning branch only if it still explains the fixture better than object-glass, threshold/cutout, or `AlphaBlended`
- the exact winning-branch burden now lives in:
  - [SimGlass Build/Buy Promotion Gate](simglass-buildbuy-promotion-gate.md)

## Exact claim this packet is making

Exact target claim:

- the current workspace is allowed to treat `Build/Buy SimGlass` as a bounded possibility and a search route
- the current workspace is not allowed to treat `Build/Buy SimGlass` aggregate presence as a closed family proof

Why this packet matters:

- it prevents the research track from sliding back into package-pattern overread
- it keeps the `SimGlass` row alive without letting it absorb transparent `Build/Buy` content prematurely
- it preserves the external-first semantic split while still using local surveys for fixture selection

## Honest limit

This packet does not prove a `Build/Buy SimGlass` fixture.

What it does prove:

- `Build/Buy` survey presence is real enough to justify continued search
- the evidence ceiling is now explicit
- any future `Build/Buy SimGlass` claim must be fixture-grade, not summary-grade
