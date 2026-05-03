# uvMapping packed-data decode — gap analysis (Plan 3.1)

## Summary

The "uvMapping is stored as packed data and is not yet decoded by the generic material pipeline." note at [MaterialDecoding.cs:153](../../../src/Sims4ResourceExplorer.Preview/MaterialDecoding.cs#L153) is reached for a narrow, well-characterised slice of materials, not a general format gap.

**Population (probe sample of 28,504 MATD chunks via `--probe-uv-mapping`):**

| Bucket | Count | % of population |
|---|---:|---:|
| Material has no `uvMapping` property | 16,871 | 59.2% |
| `uvMapping` stored as `FloatVector` (handled by upstream branches) | 6,131 | 21.5% |
| `uvMapping` stored as `PackedUInt32`, rejected as resource-key marker | 4,957 | 17.4% |
| `uvMapping` stored as `PackedUInt32`, decoded as normalized-uint16 | 197 | 0.7% |
| `uvMapping` stored as `PackedUInt32`, decoded as half-float | 19 | 0.07% |
| `uvMapping` stored as `PackedUInt32`, atlas-window plausibility rejected | 0 | 0% |
| **`uvMapping` stored as `PackedUInt32`, no encoding succeeded (THE GAP)** | **329** | **1.15%** |

The gap is concentrated in two shaders:

| Shader hash | Name (from `tmp/precomp_shader_profiles.json`) | Decode-fails |
|---|---|---:|
| `0x213D6300` | `FadeWithIce` | 293 |
| `0x292D042A` | `ObjOutlineColorStateTexture` | 36 |

Both are environment / FX shaders, not core SimSkin or Build/Buy diffuse shaders. The visual impact is on objects rendered through these shader paths.

## Root cause

Per the precompiled shader profile registry, both shaders declare `uvMapping` (property hash `0x420520E9`) with `packed_type_hex = 0x01374601` and `category = uv_mapping` — which means **vec4 of float32**. The first probe pass of `Ts4MatdChunk.Parse` calls `DecodeMatdValue`; for `normalizedPropertyType == 1, propertyArity == 4` it tries `TryReadFloatComponents` first, then falls through to `TryReadUInt32Components` if the float decode fails ([BuildBuySceneBuildService.cs:3575-3594](../../../src/Sims4ResourceExplorer.Preview/BuildBuySceneBuildService.cs#L3575)).

`TryReadFloatComponents` validates **all four** components for plausibility. For these two shaders, the saved data is a vec4 where only the **first two** floats are meaningful — the trailing two contain uninitialized memory or unrelated state. Examples (from [tmp/uv-mapping-50k/decode-fail-samples.txt](../../../tmp/uv-mapping-50k/decode-fail-samples.txt)):

```
packed-words: 0x00000000 0x40000000 0xAFF8FF6C 0xD1336495
as-float32:   [0.0, 2.0, -4.8e10, ~NaN]   <-- words[0..1] are valid floats; words[2..3] are junk

packed-words: 0x3F000000 0x3F000000 0x41A00000 0x48656130
as-float32:   [0.5, 0.5, 20.0, 234884.8]  <-- words[0..2] valid; word[3] is junk
```

Of all 25 dumped samples, every single one shows the same structural pattern:
- `words[0..1]` parses as plausible float32 (typical values: `0.0`, `0.5`, `2.0`)
- `words[2..3]` parses as garbage (huge exponents, NaN, denormals, ASCII noise)

The all-or-nothing plausibility check rejects the entire vector → property is stored as `PackedUInt32` → `TryInterpretPackedUvProperty` tries half-float and normalized-uint16 (both fail because the bytes ARE float32) → fall-through to the "not yet decoded" note.

## What this is NOT

- **Not a third encoding.** The bytes are float32. We just need to accept partial vectors when only the leading components are valid.
- **Not a Sim/CAS rendering issue.** Both shaders are environment / FX. SimSkin and CAS uvMapping all decode successfully via the FloatVector or known packed paths.
- **Not a UV channel selector issue.** UV channel selection (`SelectTextureCoordinates`) is unaffected by this decode failure — it only blocks the scale/offset transform.

## Fix design (Phase 2.1)

Allow partial-vector recovery in the decoder. Two compatible approaches:

1. **In `DecodeMatdValue` (`BuildBuySceneBuildService.cs:3577`)** — if `TryReadFloatComponents` rejects an arity-4 vec because of trailing junk, retry with leading-2 plausible-floats: if positions [0..1] are plausible and [2..3] are not, return a `FloatVector` with `[float[0], float[1], 0, 0]` and a note flagging the partial decode.

2. **In `TryInterpretPackedUvProperty` (`MaterialDecoding.cs:1101`)** — add a third decode path: re-interpret packed bytes as float32, accept if leading 2 components are plausible, treat remaining as identity defaults.

Approach 1 is preferred because it avoids the "stored as PackedUInt32" misclassification at parse time and lets the rest of the pipeline see a proper FloatVector. Then the existing scalar/vector branches at MaterialDecoding.cs:113-137 will pick it up cleanly.

The semantic mapping for these shaders (`FadeWithIce`, `ObjOutlineColorStateTexture`) is most likely:
- `floats[0..1]` = `(scaleU, scaleV)` for the affected sampler
- `floats[2..3]` = unused (defaults to `(0, 0)` offset)

This needs one round of visual verification on a Build/Buy object that uses one of these shaders before declaring the fix correct.

## Reproduction

```bash
dotnet build tools/ProbeAsset/ProbeAsset.csproj
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- \
    --probe-uv-mapping "C:\GAMES\The Sims 4" 50000 tmp/uv-mapping-50k
```

Outputs:
- `tmp/uv-mapping-50k/summary.txt` — bucket histogram + per-shader decode-fail counts
- `tmp/uv-mapping-50k/decode-fail-samples.txt` — first 25 decode-fail rows with raw bytes and three encoding interpretations

## Status

3.1 closed. Ready to implement Phase 2.1 (`DecodeMatdValue` partial-vector recovery).
