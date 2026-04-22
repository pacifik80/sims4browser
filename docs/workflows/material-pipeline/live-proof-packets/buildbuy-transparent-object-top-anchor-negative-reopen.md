# Build/Buy Transparent Object Top-Anchor Negative Reopen

This packet records the first real reopen result for the two strongest transparent-object anchors.

Question:

- what exact local reopen result is now shared by `displayShelf` and `shopDisplayTileable`, and how far does that result actually move the route?

Related docs:

- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object ShopDisplayTileable Anchor](buildbuy-transparent-object-shopdisplay-anchor.md)
- [Build/Buy Transparent Object Top-Anchor Exhaustion Boundary](buildbuy-transparent-object-top-anchor-exhaustion-boundary.md)
- [Build/Buy Transparent Object Survey-Versus-Reopen Boundary](buildbuy-transparent-object-survey-vs-reopen-boundary.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent Object Reopen Checklist](buildbuy-transparent-object-reopen-checklist.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Top-Anchor Negative Reopen
├─ displayShelf reopen capture ~ 94%
├─ shopDisplayTileable reopen capture ~ 94%
├─ Shared top-anchor negative ceiling ~ 95%
└─ Stable live-fixture closure ~ 24%
```

## Current local reopen result

Current direct reopen attempts through the existing `ProbeAsset` binary now record:

- `tools/ProbeAsset/bin/Debug/net8.0/win-x64/ProbeAsset.exe "C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package" "01661233:00000000:E779C31F25406B73"`
  - `Scanning package: C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package`
  - `Build/Buy asset not found for root TGI: 01661233:00000000:E779C31F25406B73`
- `tools/ProbeAsset/bin/Debug/net8.0/win-x64/ProbeAsset.exe "C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package" "01661233:00000000:93EE8A0CF97A3861"`
  - `Scanning package: C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package`
  - `Build/Buy asset not found for root TGI: 01661233:00000000:93EE8A0CF97A3861`

Current local evidence files:

- `tmp/probe_displayshelf_reopen_attempt.txt`
- `tmp/probe_shopdisplay_reopen_attempt.txt`

## Safe reading

This packet now proves:

- the two strongest peer anchors have both been exercised through one real reopen attempt
- both top anchors currently hit the same negative reopen ceiling
- the route is no longer blocked at the purely survey-backed pre-reopen layer

This packet does not prove:

- a stable reopened transparent-object fixture
- a winning transparent-family branch
- that the whole transparent-decor route has stalled

## Why this matters

Before this packet, the strongest-anchor stack was still bounded mostly through:

- survey identity
- candidate resolution
- restart-safe ordering rules

Current evidence is now stronger than that:

- the top anchor pair is no longer only route-grade
- it now has one shared real reopen failure boundary

## Exact target claim for this packet

- `displayShelf` and `shopDisplayTileable` are now honestly exhausted at the first reopen layer without producing a fixture-grade result

## Best next step after this packet

1. Keep both top anchors as exact negative reopen references.
2. Move the transparent-object route to `mirror`.
3. Keep `lantern` and `fishBowl` behind `mirror`.
4. Do not widen to the window-heavy negative control yet.

## Honest limit

What this packet proves:

- the strongest anchor pair no longer needs another wording-only pass before the route moves downward

What remains open:

- the first stable reopened transparent-object fixture
- the first winning family classification
- whether `mirror` or a lower-ranked anchor will reopen more cleanly
