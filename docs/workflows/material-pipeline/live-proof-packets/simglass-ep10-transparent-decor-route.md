# SimGlass EP10 Transparent-Decor Route

This packet promotes the current `SimGlass` search boundary from a loose candidate cluster into a narrower object-side route.

Question:

- does the current workspace already prove one better `Build/Buy` search route for `SimGlass` than the earlier window-heavy sweep, even though no stable reopened live fixture exists yet?

Related docs:

- [SimGlass Versus Shell Baseline](simglass-vs-shell-baseline.md)
- [Build/Buy Transparent Object Classification Boundary](buildbuy-transparent-object-classification-boundary.md)
- [Edge-Family Matrix](../edge-family-matrix.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [Live-Proof Packets](README.md)
- [SimSkin, SimGlass, And SimSkinMask](../family-sheets/simskin-simglass-simskinmask.md)
- [Object Glass And Transparency](../family-sheets/object-glass-and-transparency.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
SimGlass EP10 Transparent-Decor Route
├─ Survey-backed candidate extraction ~ 89%
├─ Object-bundle companion integrity ~ 86%
├─ Window-heavy negative control ~ 83%
├─ Stable live-fixture closure ~ 19%
└─ Restart-safe next-step narrowing ~ 91%
```

## Externally proved family baseline

What is already strong enough:

- local external [TS4SimRipper Enums.cs](../../../references/external/TS4SimRipper/src/Enums.cs) explicitly defines `SimGlass = 0x5EDA9CDE`
- local external [TS4SimRipper PreviewControl.cs](../../../references/external/TS4SimRipper/src/PreviewControl.cs) preserves a separate glass branch in preview handling
- local external [TS4SimRipper ColladaDAE.cs](../../../references/external/TS4SimRipper/src/ColladaDAE.cs) exports a dedicated `glass` suffix instead of flattening the family into generic shell handling
- creator-facing TS4 transparency guidance still ties `Simglass` more strongly to thin transparent layered content than to generic architectural glass

Safe reading:

- this packet is not re-proving that `SimGlass` exists
- it only narrows which transparent `Build/Buy` object class is currently the safest next search route
- after the new object-transparent split, that route is classification-neutral until a stable fixture says whether it belongs under `SimGlass` or object-side glass/transparency

## Current local survey floor

Current aggregate signal:

- `tmp/probe_all_buildbuy_summary_full.json` records `ProcessedEntries = 1380`
- the same summary records `SimGlass = 5`

This is still aggregate-only:

- it proves TS4-facing `Build/Buy` presence
- it does not yet name one stable reopened fixture

## What the transparent-decor cluster now proves

The current `EP10` survey already isolates a narrower object-side route than the earlier window-heavy packet.

### Cluster roots

From `tmp/probe_ep10_buildbuy_candidate_resolution_full.json`:

- `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
  - `TypeName = Model`
  - `TransformName = instance-swap32`
  - `SourceCount = 6`
- `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
  - `TypeName = Model`, `Rig`, `Slot`, `Footprint`
  - `TransformName = instance-swap32`
  - `SourceCount = 10` across that companion bundle
- `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
  - `TypeName = Model`, `Rig`, `Slot`, `Footprint`
  - `TransformName = instance-swap32`
  - `SourceCount = 8` across that companion bundle
- `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
  - `TypeName = Model`, `Footprint`
  - `TransformName = instance-swap32`
  - `SourceCount = 9`
- `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
  - `TypeName = Model`, `Rig`, `Footprint`
  - `TransformName = instance-swap32`
  - `SourceCount = 10`

The same transformed roots are also present in `tmp/probe_all_buildbuy.txt`:

- `FAE0318F3711431D`
- `E779C31F25406B73`
- `93EE8A0CF97A3861`
- `F4A27FC1857F08D4`
- `3CD0344C1824BDDD`

Safe reading:

- this is stronger than “object names looked promising”
- several cluster members already preserve a repeated object-side companion bundle around the same transformed instance
- that makes the cluster a better next route than a generic name-based sweep

## Why the window-heavy sweep is now weaker

The earlier `EP10` window-heavy packet is still useful as a negative control.

From `tmp/probe_ep10_buildbuy_identity_survey_full.json`:

- `window1X1_EP10TRADglassPanelTileable3Tile_set1..13` appears repeatedly
- `window1X1_EP10TRADglassShortTileable2Tile_set1..11` also appears repeatedly

But current safe reading is still weaker there:

- the survey does show many obvious `glass` names
- it does not currently surface one comparably clean resolved companion bundle around a transformed `Model` root
- direct reopen attempts from the earlier obvious-name packet already failed

Safe conclusion:

- obvious architectural `glass` naming is not currently the strongest route
- companion-bundle integrity beats name obviousness for the next `SimGlass` search pass
- but the cluster should now be treated as a transparent-object route first, not an implicitly `SimGlass` route

## What this packet does not prove

It does not yet prove:

- that any one of these `EP10` roots is definitely a reopened live `SimGlass` fixture
- that `Build/Buy` `SimGlass` means exactly the same thing as character-side `SimGlass`
- exact shader-slot semantics for the cluster
- exact reason current direct reopen attempts still fail

Current honest limit:

- this is a survey-backed object-side route packet, not a live-fixture closure packet

## Current implementation boundary

Current repo behavior is useful here as boundary evidence only:

- direct reopen attempts on the transformed roots still return `Build/Buy asset not found`
- that means current tooling is not yet enough to reconstruct these rows as stable fixtures
- it does not demote the route itself, because the object-side survey and candidate-resolution artifacts are already consistent

## Exact target claim for this packet

- the current workspace already proves that the `EP10` transparent-decor cluster is a better object-side `SimGlass` search route than the earlier window-heavy sweep, because it preserves repeated transformed companion bundles rather than only obvious `glass` naming

## Best next step after this packet

The next `SimGlass` packet should not widen back out to generic window names.

It should do one of these:

1. reopen one root from the transparent-decor cluster as a stable live fixture, or
2. widen to another package slice only if the whole cluster stays unreopenable, or
3. find one external creator/tool packet that ties this kind of transparent decor more directly to glass-family behavior
4. classify any reopened fixture against the object-side transparent split before keeping it under the `SimGlass` branch

Current safest target order:

1. `displayShelf`
2. `shopDisplayTileable`
3. `mirror`
4. `lantern`
5. `fishBowl`

Reason:

- `displayShelf` and `shopDisplayTileable` have the cleanest full companion bundles (`Model`, `Rig`, `Slot`, `Footprint`)
- `mirror` keeps a strong three-part bundle
- `lantern` still keeps a two-part `Model` plus `Footprint` pair
- `fishBowl` currently has only the transformed `Model` root, so it is weaker structurally even though its name remains plausible

## Honest limit

What this packet proves:

- the current `SimGlass` search route is now narrower than “look for more glass-like names”
- the `EP10` transparent-decor cluster is backed by object-side candidate-resolution evidence, not only by naming
- repeated transformed companion bundles now make `displayShelf`, `shopDisplayTileable`, `mirror`, and `lantern` better next-step targets than the earlier window-heavy slice

What remains open:

- the first stable reopened `SimGlass` live fixture
- whether any of these roots actually carry a glass-family material packet after reconstruction
- exact relation between this `Build/Buy` route and character-side `SimGlass`
