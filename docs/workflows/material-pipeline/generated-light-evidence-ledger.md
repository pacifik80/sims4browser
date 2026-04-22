# Generated-Light Evidence Ledger

This companion keeps `GenerateSpotLightmap` and `NextFloorLightMapXform` restart-safe by separating externally corroborated generated-light semantics from local carry-through evidence and bounded synthesis.

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](family-sheets/generate-spotlightmap-nextfloorlightmapxform.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
Generated-Light Evidence Ledger
├─ External corroboration labeling ~ 94%
├─ Local carry-through separation ~ 92%
├─ Bounded synthesis separation ~ 93%
└─ Exact lightmap-transform closure ~ 18%
```

## Externally confirmed

What the current source pack supports directly:

- [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135) groups `GenerateSpotLightmap`, `GenerateWindowLightmap`, `GenerateRectAreaLightmap`, `GenerateTubeLightmap`, and `NextFloorLightMapXform` into one TS4 lightmap-oriented vocabulary
- the names themselves are generated-light and transform vocabulary rather than ordinary surface-slot vocabulary

Safe externally backed reading:

- `GenerateSpotLightmap` belongs under generated-light/helper semantics
- `NextFloorLightMapXform` is safer as transform/helper provenance than as an ordinary sampled texture slot or plain canonical UV transform

## Local package evidence

What the local workspace adds without becoming a truth layer:

- `tmp/precomp_sblk_inventory.json` repeats `GenerateSpotLightmap` with `occurrences = 6`
- the same inventory concentrates `NextFloorLightMapXform = 14` in the stronger generated-light packet and preserves a weaker secondary `NextFloorLightMapXform = 3` packet
- the stronger packet also keeps `SeaLevel = 14`, which reinforces the environmental/helper reading
- `tmp/precomp_shader_profiles.json` repeats `NextFloorLightMapXform` across local profile packets
- the nearest currently isolated visible comparison roots remain the adjacent projective/light-space packet around:
  - `01661233:00000000:00F643B0FDD2F1F7`
  - `01661233:00000000:0124E3B8AC7BEE62`

Safe local reading:

- generated-light vocabulary survives in the local corpus
- one generated-light packet is now clearly stronger than the weaker secondary carry-through
- local roots currently support comparison and future fixture targeting
- the local packet is still carry-through evidence, not direct proof of exact matrix or visible-pass semantics

## Bounded synthesis

What this repo can now say as a bounded conclusion:

- generated-light helpers are stronger than a generic unresolved-UV bucket
- the current safest internal split is:
  - `GenerateSpotLightmap` as the stronger semantic home
  - `NextFloorLightMapXform` as the narrower transform/helper signal inside that family
- current broad UV or slot normalization remains implementation approximation, not family truth

This is synthesis, not a quoted external claim:

- no current source in the packet proves exact `NextFloorLightMapXform` matrix math
- no current source in the packet proves one definitive live asset with end-to-end generated-light closure

## Still open

Not closed by this ledger:

- exact matrix semantics for `NextFloorLightMapXform`
- exact visible impact of generated-light helpers in live TS4 assets
- one definitive live root where the generated-light family can be inspected end to end without leaning on adjacent projective roots
