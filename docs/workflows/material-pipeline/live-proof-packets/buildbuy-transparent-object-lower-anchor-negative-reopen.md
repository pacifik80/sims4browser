# Build/Buy Transparent Object Lower-Anchor Negative Reopen

This packet records the real reopen result for the remaining transparent-decor anchors after the strongest peer pair was exhausted.

Question:

- what exact local reopen result is now shared by `mirror`, `lantern`, and `fishBowl`, and how far does that move the transparent-decor route?

Related docs:

- [Build/Buy Transparent Object Post-Top-Anchor Handoff](buildbuy-transparent-object-post-top-anchor-handoff.md)
- [Build/Buy Transparent Object Route Stall Boundary](buildbuy-transparent-object-route-stall-boundary.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object Lower-Anchor Negative Reopen
├─ mirror reopen capture ~ 94%
├─ lantern reopen capture ~ 94%
├─ fishBowl reopen capture ~ 93%
└─ Full lower-anchor negative ceiling ~ 95%
```

## Current local reopen result

Current direct reopen attempts through the existing `ProbeAsset` binary now record:

- `tools/ProbeAsset/bin/Debug/net8.0/win-x64/ProbeAsset.exe "C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package" "01661233:00000000:3CD0344C1824BDDD"`
  - `Scanning package: C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package`
  - `Build/Buy asset not found for root TGI: 01661233:00000000:3CD0344C1824BDDD`
- `tools/ProbeAsset/bin/Debug/net8.0/win-x64/ProbeAsset.exe "C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package" "01661233:00000000:F4A27FC1857F08D4"`
  - `Scanning package: C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package`
  - `Build/Buy asset not found for root TGI: 01661233:00000000:F4A27FC1857F08D4`
- `tools/ProbeAsset/bin/Debug/net8.0/win-x64/ProbeAsset.exe "C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package" "01661233:00000000:FAE0318F3711431D"`
  - `Scanning package: C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package`
  - `Build/Buy asset not found for root TGI: 01661233:00000000:FAE0318F3711431D`

Current local evidence files:

- `tmp/probe_mirror_reopen_attempt.txt`
- `tmp/probe_lantern_reopen_attempt.txt`
- `tmp/probe_fishbowl_reopen_attempt.txt`

## Safe reading

This packet now proves:

- the full lower-ranked half of the transparent-decor cluster has also been exercised through real reopen attempts
- `mirror`, `lantern`, and `fishBowl` currently share the same negative reopen ceiling
- the route no longer has any untested candidate inside the current transparent-decor cluster

This packet does not prove:

- a stable transparent-object fixture
- any winning transparent-family branch
- that the next route beyond this cluster is already semantically stronger

## Exact target claim for this packet

- the lower-ranked transparent-decor anchors are now also honestly exhausted at the first reopen layer without producing a fixture-grade result

## Best next step after this packet

1. Treat the full transparent-decor cluster as exhausted at the current inspection layer.
2. Do not retry the same five roots without a new inspection layer.
3. Hand off to the documented widening path.

## Honest limit

What this packet proves:

- the lower-anchor half of the transparent-decor route no longer needs another wording-only pass

What remains open:

- which next package slice should replace the exhausted decor cluster
- whether a later inspection layer can ever reopen one of these same roots more strongly
