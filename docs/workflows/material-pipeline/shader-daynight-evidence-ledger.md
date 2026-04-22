# ShaderDayNight Evidence Ledger

This companion keeps `ShaderDayNightParameters` restart-safe by separating what is externally corroborated from what is only local carry-through evidence or bounded synthesis.

Related docs:

- [Material Pipeline Deep Dives](README.md)
- [Shader Family Registry](shader-family-registry.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [ShaderDayNightParameters](family-sheets/shader-daynight-parameters.md)
- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)

## Scope status (`v0.1`)

```text
ShaderDayNight Evidence Ledger
├─ External corroboration labeling ~ 93%
├─ Local carry-through separation ~ 91%
├─ Bounded synthesis separation ~ 92%
└─ Exact TS4 visible-pass closure ~ 21%
```

## Externally confirmed

What the current source pack supports directly:

- the family name itself supports a layered day/night or light-aware reading rather than an ordinary static surface reading
- [Sims_3:Shaders\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) records `RevealMap` as a dedicated shader helper parameter in the same engine lineage
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) shows `RevealMap` inside a concrete shader family, which supports helper provenance rather than ordinary base-color-slot semantics

Safe externally backed reading:

- `ShaderDayNightParameters` stays a layered light-aware family
- `samplerRevealMap` stays reveal/helper provenance
- direct flattening into ordinary `diffuse`, `overlay`, or `emissive` truth claims is not externally justified

## Local package evidence

What the local workspace adds without becoming a truth layer:

- `tmp/precomp_sblk_inventory.json` repeats `ShaderDayNightParameters` with `occurrences = 5`
- the same inventory currently keeps `LightsAnimLookupMap = 94` and `samplerRevealMap = 32` inside the main `ShaderDayNightParameters` packet
- `tmp/precomp_shader_profiles.json` repeats the same helper vocabulary across local profile packets
- `tmp/probe_sample_ep06_ep10_coverage.txt` isolates one visible `Build/Buy` model root with `ShaderDayNightParameters=1`
- `tmp/probe_sample_next24_coverage_after_nonvisual_fix.txt` isolates another visible `Build/Buy` model root with `ShaderDayNightParameters=1`
- `tmp/sample_payload_batch_after_full5.txt` isolates a third visible `Build/Buy` model root with `ShaderDayNightParameters=1`

Safe local reading:

- the family survives into real local profile and visible-root packets
- `LightsAnimLookupMap` is concentrated enough to preserve as narrow helper vocabulary
- the visible-root floor is now stronger than a two-root packet
- this local packet is evidence for carry-through and fixture selection, not exact TS4 semantics

## Bounded synthesis

What this repo can now say as a bounded conclusion:

- `ShaderDayNightParameters` is stronger than an amorphous unresolved-parameter bucket
- the current safest family split is:
  - `ShaderDayNightParameters` as the layered family
  - `samplerRevealMap` as the reveal/helper input
  - `LightsAnimLookupMap` as the narrower lookup helper
- the local visible roots are strong enough to block generic slot flattening in future docs and future implementation work

This is synthesis, not a quoted external claim:

- no current source in the packet proves exact TS4 visible-pass math
- no current source in the packet proves a final per-slot contract for every helper name

## Still open

Not closed by this ledger:

- exact visible-pass math for day/night or reveal behavior
- exact semantic role of `LightsAnimLookupMap`
- exact per-slot contract for `ShaderDayNightParameters`
- one stronger TS4-facing live fixture where the helper packet can be compared against adjacent visible behavior
