# Build/Buy Transparent Object Classification Boundary

This packet freezes the next semantic boundary for transparent `Build/Buy` fixture work after the object-glass split was documented externally.

Question:

- does the current workspace already know enough to stop treating the `EP10` transparent-decor cluster as implicitly `SimGlass`, even before one reopened live fixture is closed?

Related docs:

- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [SimGlass Versus Shell Baseline](simglass-vs-shell-baseline.md)
- [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md)
- [Build/Buy Material Authority Matrix](../buildbuy-material-authority-matrix.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Classification Boundary
├─ External semantic split ~ 92%
├─ Transparent-decor cluster reclassification boundary ~ 88%
├─ Safe restart wording ~ 94%
└─ Stable live-fixture classification ~ 18%
```

## Externally proved semantic floor

What is already strong enough:

- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md) now freezes the external-first split between:
  - character-side `SimGlass`
  - object-side `GlassForObjectsTranslucent`
  - threshold/cutout transparency via `AlphaMap` plus `AlphaMaskThreshold`
  - blended object transparency via `AlphaBlended`
- creator-facing object workflows explicitly name `GlassForObjectsTranslucent`, `AlphaMap`, `AlphaMaskThreshold`, and `AlphaBlended`
- creator-facing CAS workflows and local external `TS4SimRipper` snapshots still make `SimGlass` the stronger character-side family name

Safe reading:

- transparent `Build/Buy` content is no longer one semantically neutral bucket
- a transparent object fixture cannot be promoted into the `SimGlass` row by name similarity alone
- the first classification question now comes before the first family-specific live-proof claim

## What the current `EP10` cluster now means

The transparent-decor cluster from [SimGlass EP10 Transparent-Decor Route](simglass-ep10-transparent-decor-route.md) is still useful.

Current cluster:

- `fishBowl_EP10GENmarimo`
- `shelfFloor2x1_EP10TEAdisplayShelf`
- `shelfFloor2x1_EP10TEAshopDisplayTileable`
- `lightWall_EP10GENlantern`
- `mirrorWall1x1_EP10BATHsunrise`

What is still safe to say:

- it is a better transparent-object search route than the earlier window-heavy sweep
- it preserves repeated transformed companion bundles
- it remains a valid search anchor cluster for the next transparent object fixture

What is no longer safe to say without a reopened fixture:

- that the cluster is a `SimGlass` cluster
- that these objects are better evidence for character-side glass than for object-side glass/transparency
- that the next reopened root should stay under the `SimGlass` row regardless of what material family it exposes

## Exact target claim for this packet

- the current workspace already has enough external evidence to require a classification step before promoting any reopened transparent `Build/Buy` fixture into the `SimGlass` branch

## Classification rule for the next transparent-object fixture

When one of the current transparent-decor roots reopens cleanly, classify it in this order:

1. object-side `GlassForObjectsTranslucent`
2. threshold/cutout transparency via `AlphaMap` plus `AlphaMaskThreshold`
3. blended object transparency via `AlphaBlended`
4. only then, if evidence really points there, character-side `SimGlass`

Safe reading:

- this is a classification order, not a claim that `SimGlass` cannot appear in `Build/Buy`
- it only means the object-side transparent branches are now better externally proved than a generic reuse of the `SimGlass` label

## Current implementation boundary

Current repo behavior is useful only as boundary evidence:

- the `EP10` transparent-decor roots are still survey-backed candidate anchors
- current direct reopen attempts still fail with `Build/Buy asset not found`
- the current implementation therefore cannot yet classify the cluster cleanly

That does not reopen the semantic question:

- failure to reopen a fixture is not permission to merge object-glass, cutout transparency, `AlphaBlended`, and `SimGlass` back together

## Best next step after this packet

1. Keep the `EP10` transparent-decor cluster as the first transparent-object search route.
2. Reopen one root from that cluster as a stable fixture.
3. Classify the reopened fixture against the new object-transparent family split before attaching it to the `SimGlass` row.
4. Only after classification should the next live-proof packet stay under:
   - `SimGlass`, or
   - object-side glass/transparency

## Honest limit

What this packet proves:

- the semantic split is now strong enough to change how transparent `Build/Buy` fixtures must be classified
- the current transparent-decor cluster is still useful, but it is no longer safe to treat it as implicitly `SimGlass`

What remains open:

- the first stable reopened transparent `Build/Buy` fixture
- which family branch that fixture actually belongs to
- exact slot and material semantics for the winning branch
