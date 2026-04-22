# Build/Buy Transparent Object ShopDisplayTileable Anchor

This packet records the second strongest current survey-backed transparent-object anchor inside the `EP10` transparent-decor cluster.

Question:

- what exact local identity and companion-bundle evidence already makes `shopDisplayTileable` the strongest peer anchor after `displayShelf`?

Related docs:

- [Build/Buy Transparent-Decor Route](buildbuy-transparent-decor-route.md)
- [Build/Buy Transparent Object DisplayShelf Anchor](buildbuy-transparent-object-displayshelf-anchor.md)
- [Build/Buy Transparent Object Target Priority](buildbuy-transparent-object-target-priority.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)

## Scope status (`v0.1`)

```text
Build/Buy Transparent Object ShopDisplayTileable Anchor
├─ Survey-backed identity capture ~ 95%
├─ Companion-bundle anchor strength ~ 96%
├─ Restart-safe second-target wording ~ 96%
└─ Stable live-fixture closure ~ 21%
```

## Current local identity

Current `tmp/probe_ep10_buildbuy_identity_survey_full.json` is already strong enough to record:

- `ObjectDefinitionTgi = C0DB5AE7:00000000:000000000003E7CB`
- `ObjectCatalogTgi = 319E4F1D:00000000:000000000003E7CB`
- `ObjectDefinitionInternalName = shelfFloor2x1_EP10TEAshopDisplayTileable_set1`

Current `tmp/probe_ep10_buildbuy_candidate_resolution_full.json` is already strong enough to record:

- `Model -> 01661233:00000000:93EE8A0CF97A3861` via `instance-swap32`, `SourceCount = 8`
- `Rig -> 8EAF13DE:00000000:93EE8A0CF97A3861` via `instance-swap32`, `SourceCount = 8`
- `Slot -> D3044521:00000000:93EE8A0CF97A3861` via `instance-swap32`, `SourceCount = 8`
- `Footprint -> D382BF57:00000000:93EE8A0CF97A3861` via `instance-swap32`, `SourceCount = 8`

Current `tmp/probe_all_buildbuy.txt` is already strong enough to record:

- `C:\GAMES\The Sims 4\EP10\ClientFullBuild0.package|01661233:00000000:93EE8A0CF97A3861`

## Safe reading

This anchor currently proves:

- `shopDisplayTileable` is a real peer anchor, not just a fallback after `displayShelf`
- the current second target also preserves the full companion bundle:
  - `Model`
  - `Rig`
  - `Slot`
  - `Footprint`

This anchor does not yet prove:

- a stable reopened `Build/Buy` asset
- a winning transparent-family branch
- that it should outrank the current first anchor

## Why this matters

Without this packet, `shopDisplayTileable` could still read as:

- only the next name in the route list

The current evidence is stronger:

- it is a full-companion, survey-backed peer anchor that can take over immediately if `displayShelf` fails to reopen cleanly

## Best next use of this packet

Keep `shopDisplayTileable` as the immediate second reopen target.

If `displayShelf` stalls at the pre-reopen ceiling, the route should move here next before widening to `mirror`, `lantern`, or `fishBowl`.
