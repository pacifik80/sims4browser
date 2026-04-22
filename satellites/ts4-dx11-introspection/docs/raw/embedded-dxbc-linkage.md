# TS4 Embedded DXBC Linkage

Last updated:
- 2026-04-21 UTC

Status:
- confirmed binary-to-runtime linkage note
- supersedes the earlier assumption that the primary x64 executables exposed only a handful of embedded `DXBC` candidates

Inputs:
- [shader-catalog.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.json)
- [embedded-dxbc-TS4_x64.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/reports/embedded-dxbc-TS4_x64.md)
- [embedded-dxbc-TS4_x64_fpb.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/reports/embedded-dxbc-TS4_x64_fpb.md)

## Confirmed Findings

For `TS4_x64.exe`:
- embedded `DXBC` containers found: `1302`
- exact runtime-catalog SHA-256 matches: `364`
- matched stage split:
  - `267` pixel shaders
  - `98` vertex shaders
- all `365` matches have catalog support `3`, meaning they recur across all three broad stable reference captures

For `TS4_x64_fpb.exe`:
- embedded `DXBC` containers found: `1302`
- exact runtime-catalog SHA-256 matches: `364`

Across the two primary x64 executables:
- common embedded `DXBC` SHA-256 set: `1298`
- `TS4_x64.exe`-only hashes: `0`
- `TS4_x64_fpb.exe`-only hashes: `0`

Interpretation:
- the two primary x64 executables expose the same embedded `DXBC` population by hash
- offsets differ between the binaries, but the actual container set is hash-identical

## What This Proves

This is now proven:
- a substantial subset of the stable runtime shader inventory is directly embedded in the main x64 executables
- runtime shader hashes can be linked back to concrete binary offsets through exact `SHA-256` equality on the full `DXBC` container

This is also proven:
- the earlier “8 embedded `DXBC` blobs” view was an undercount and should no longer be used as the working assumption

## Coverage Interpretation

Current broad stable runtime catalog:
- `1922` unique runtime shader hashes

Direct executable linkage established so far:
- `364` unique exact matches from the current broad runtime catalog to embedded `DXBC` containers in the main executables

Current direct linkage coverage:
- about `19%` of the broad stable runtime catalog by unique hash count

Unmatched side:
- `937` embedded `DXBC` containers in each executable are not currently represented in the broad stable runtime catalog

Likely interpretations for the unmatched side:
- dormant or mode-specific shaders not yet exercised in live captures
- startup/fallback/debug/feature-variant shaders not reached in the current reference sessions
- content branches used only in rarer gameplay, hardware, or option configurations

## Strongest Matched Reflection Families

Top reflection-signature clusters among the `365` exact binary↔runtime matches:
- `32`  `ps br=4 cb=0 in=5 out=1`
- `22`  `ps br=5 cb=1 in=3 out=1`
- `21`  `ps br=4 cb=0 in=3 out=1`
- `18`  `ps br=2 cb=0 in=3 out=1`
- `16`  `ps br=2 cb=0 in=5 out=1`
- `16`  `ps br=6 cb=0 in=5 out=1`
- `14`  `ps br=6 cb=0 in=3 out=1`
- `14`  `vs br=1 cb=1 in=3 out=4`
- `13`  `vs br=1 cb=1 in=2 out=4`
- `11`  `ps br=4 cb=0 in=2 out=1`

Interpretation:
- the directly linked subset is not dominated only by trivial fullscreen helpers
- it includes richer pixel families and at least some compact vertex families

## Implications For Next Analysis

Safe next steps now become more concrete:
- start from exact matched hashes rather than from abstract family signatures alone
- map matched hashes back to executable offsets and surrounding binary regions
- prioritize matched hashes from seeded families in [shader-family-registry.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-family-registry.md)
- use unmatched embedded `DXBC` containers as a “not yet exercised” search space for future live capture expansion

## Open Questions

- Which of the `364` matched hashes fall into the seeded family buckets `F01` through `F09`?
- Are the directly linked executable shaders biased toward:
  - utility/shared engine passes
  - UI/screen-space passes
  - or a mixed core subset that also includes material evaluation families?
- Are there additional runtime-used shader populations loaded from content packages or other binary/data carriers beyond the executable-embedded set?
