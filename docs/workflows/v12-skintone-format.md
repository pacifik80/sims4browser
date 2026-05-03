# v12 Skintone (TONE) binary format (Plan 3.3)

## Summary

The Skintone parser in [StructuredMetadataServices.cs:120-123](../../src/Sims4ResourceExplorer.Packages/StructuredMetadataServices.cs#L120) throws on any version other than 6. v12 copies of the same skintone instance exist in the user's catalog (3 of 5 copies of `0x5545` are v12) — they are silently ignored today because the v6 copies in `Client/Simulation FullBuild0` parse first. A skintone with v12-only copies would crash. This document records the v12 layout decoded from a side-by-side hex diff.

## Reproduction

```bash
dotnet run --project tools/ProbeAsset/ProbeAsset.csproj --no-build -- \
    --dump-skintone-versions "C:\GAMES\The Sims 4" 0000000000005545 tmp/skintone-versions
```

Outputs annotated hex of one v6 example (69 bytes) and one v12 example (226 bytes) of the same skintone instance.

## v6 layout (baseline, 69 bytes)

| Offset | Size | Field | Example value |
|---:|---:|---|---|
| 0x00 | 4 | `version` (UInt32) | 6 |
| 0x04 | 8 | `baseTextureInstance` (UInt64) | 0x3275143DEC141D18 |
| 0x0C | 4 | `overlayCount` (UInt32) | 1 |
| 0x10 | 4 | `overlay[0].flags` (UInt32) | 0x2004 |
| 0x14 | 8 | `overlay[0].instance` (UInt64) | 0xA4F4ECD610FF3176 |
| 0x1C | 2 | `saturation` (UInt16) | 15 |
| 0x1E | 2 | `hue` (UInt16) | 10 |
| 0x20 | 4 | `overlayOpacity` (UInt32) | 0 |
| 0x24 | 4 | `tagCount` (UInt32) | 3 |
| 0x28 | 4 × 3 | tags (UInt16 cat + UInt16 val per tag) | (0x45, 0x4B), (0x45, 0x4C), (0x66, 0x2F9) |
| 0x34 | 4 | `makeupOpacity` (Float) | 0.6802 |
| 0x38 | 1 | `swatchColorCount` (Byte) | 1 |
| 0x39 | 4 × N | `swatchColors[]` (UInt32 each) | 0xFFF9E7CF |
| 0x3D | 4 | `displayIndex` (Float) | 10.0 |
| 0x41 | 4 | `makeupOpacity2` (Float, optional) | 1.0 |

## v12 layout (decoded from skintone 0x5545, 226 bytes)

| Offset | Size | Field | Example value |
|---:|---:|---|---|
| 0x00 | 4 | `version` (UInt32) | 12 |
| 0x04 | 1 | **`subTextureCount` (Byte)** [NEW in v12] | 3 |
| 0x05 | 28 × N | **`subTextures[]`** [NEW in v12], each: | |
|  | 8 | &nbsp;&nbsp;`instance` (UInt64) | 0x3275143DEC141D18 |
|  | 8 | &nbsp;&nbsp;`reserved` (UInt64, often 0) | 0 |
|  | 4 | &nbsp;&nbsp;`weightOrR` (Float) | 1.0 |
|  | 4 | &nbsp;&nbsp;`weightOrG` (Float) | 0.6802 |
|  | 4 | &nbsp;&nbsp;`weightOrB` (Float) | 1.0 |
| 0x59 | 4 | `overlayCount` (UInt32) | 1 |
| 0x5D | 4 | `overlay[0].flags` (UInt32) | 0x2004 |
| 0x61 | 8 | `overlay[0].instance` (UInt64) | 0xA4F4ECD610FF3176 |
| 0x69 | 2 | `saturation` (UInt16) | 15 |
| 0x6B | 2 | `hue` (UInt16) | 10 |
| 0x6D | 4 | `overlayOpacity` (UInt32) | 0 |
| 0x71 | 4 | `tagCount` (UInt32) | 11 |
| 0x75 | 6 × N | **`tags[]` (6 bytes each)** [WIDENED in v12: UInt16 cat + UInt32 val per CASP.PartTag for version ≥ 7] | (0x45, 0x4B), (0x45, 0x4C), (0x6D, 0xCE9), … |
| 0xB7 | 1 | `swatchColorCount` (Byte) | 1 |
| 0xB8 | 4 × N | `swatchColors[]` (UInt32 each) | 0xFFF9E7CF |
| 0xBC | 4 | `displayIndex` (Float) | 5.0 |
| 0xC0 | 4 | **`extraHash` (UInt32)** [NEW in v12, semantics TBD] | 0x000260AD |
| 0xC4 | 4 | reserved (zero) | 0 |
| 0xC8 | 2 | unknown (UInt16) | 1 |
| 0xCA | 4 × 6 | **HSL/colorize parameters** (six Float values) [NEW in v12] | -0.05, +0.05, +0.005, -12.0, +2.3, +1.0 |

Total: 226 bytes for `subTextureCount=3, tagCount=11, swatchColorCount=1`. The trailing 6 floats are stable across v12 copies of this skintone (probe confirms identical values in all 3 v12 copies in the catalog).

## Diffs from v6

1. **`makeupOpacity` field is gone** in v12 (no equivalent slot between `tags` and `swatchColorCount`).
2. **Tags widened from 4 bytes (UInt16+UInt16) to 6 bytes (UInt16+UInt32)** — already documented in the v6 parser comment block as `version >= 7` behaviour.
3. **New leading sub-texture block**: `subTextureCount` byte + N × 28-byte entries at the start (after `version`).
4. **New trailing block** after `displayIndex`: extra UInt32 hash + UInt16 + 6 Floats (semantics TBD; likely HSL contour parameters or per-channel adjustments).
5. **`makeupOpacity2` (optional trailing Float)** is also gone.

## Open semantic questions

- **What are `subTextures` for?** Each entry has an `instance` (UInt64) plus 16 bytes of trailing data. The first entry's instance equals the `baseTextureInstance` slot used by v6 — so it may be the base, with the additional entries being per-physique or per-region variants. The (1.0, 0.6802, 1.0) floats look like blend weights (saturation=15/100=0.15? no — 0.68 doesn't match anything obvious in the v6 record).
- **What is the `extraHash` UInt32 at 0xC0?** Probably a content checksum or a related-resource pointer.
- **What are the 6 trailing floats?** Looks like HSL contouring parameters (negative + positive small values, plus a large negative + positive pair, plus 1.0). Likely shader-side HSL controls.

These open questions DON'T block parsing — we can still decode all the fields that map to v6 semantics (overlay table, saturation/hue, swatchColors, displayIndex). Phase 2.3 should:

- Implement v12 layout in `ParseSkintone`.
- Treat `subTextures[0].instance` as the equivalent of v6's `baseTextureInstance` for the rendering pipeline.
- Default `makeupOpacity` and `makeupOpacity2` to zero / null.
- Preserve the trailing 6 floats in a new domain field (named conservatively, e.g. `V12HslParameters`) so we can study them later without losing data.

## Status

3.3 closed. Layout decoded from one byte-level diff. Ready to implement Phase 2.3 (v12 parser) — the open semantic questions about sub-texture purpose and trailing floats are not blockers because the existing render pipeline only needs `baseTextureInstance`, overlay table, saturation/hue, overlayOpacity, swatchColors, displayIndex — all of which are present in v12.
