# TS4 DX11 Shader Map

Last updated:
- 2026-04-21 UTC, after long mixed-mode runtime capture

Status:
- living document
- intended to accumulate stable shader-inventory findings from successful runtime captures
- intended to separate confirmed facts from hypotheses and open questions

Primary companion artifacts:
- [Shader Catalog (Markdown)](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.md)
- [Shader Catalog (JSON)](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.json)
- [Shader Family Registry](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-family-registry.md)
- [Embedded DXBC Linkage](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/embedded-dxbc-linkage.md)

## Scope

This document tracks:
- confirmed runtime render-path findings
- stable shader inventory observations
- recurring shader families inferred from reflection signatures and repeat counts
- known-safe vs known-risky hook points
- open questions for future binary/runtime analysis

It does not yet claim authoritative per-draw or per-material mapping. Current stable runtime captures do not safely expose deferred-context draw/state telemetry in TS4.

## Confirmed Runtime Path

Confirmed from live captures:
- TS4 loads the local `d3d11.dll` proxy successfully.
- The game reaches DXGI through `CreateDXGIFactory1`.
- Swap-chain creation is confirmed through `IDXGIFactory::CreateSwapChain`.
- Stable frame boundaries come from `IDXGISwapChain1::Present1`, not from classic `Present`.
- Shader creation hooks are stable for at least `VS` and `PS`.

Confirmed from stable traces:
- `CreateDeferredContext` is called.
- `FinishCommandList` can be hooked safely.
- In the observed stable runs, `FinishCommandList` did not emit useful recorded-command-list events.

## Stable Capture Capability

Currently stable:
- `frame_boundary`
- `shader_created`
- stable SHA-256 shader hashes
- reflection summaries
- session-local structured logs
- offline summary and comparison reports

Currently unstable or incomplete:
- deferred-context `Draw*` hooks
- deferred-context shader/state setter hooks
- stable `draws.jsonl`
- stable `states.jsonl`
- trustworthy per-draw shader-to-frame correlation

## Safe vs Risky Hook Points

Safe in repeated runs:
- proxy load path
- DXGI import patching
- factory swap-chain interception
- `Present1`
- shader creation hooks

Risky in repeated runs:
- deferred-context `Draw*` hooks
- deferred-context shader/state setter hooks

Observed result of risky hooks:
- TS4 startup or early runtime crash with `0xC0000005`

## Stable Session Inventory

Representative successful sessions:

| Session | Unique shaders | Shader events | Frame boundaries |
| --- | ---: | ---: | ---: |
| `20260421-210119` | 521 | 1034 | 6401 |
| `20260421-211853` | 521 | 1034 | 1738 |
| `20260421-212139` | 1807 | 2328 | 14673 |
| `20260421-212533` | 1761 | 2282 | 14326 |
| `20260421-220041` | 1876 | 2403 | 98771 |

Interpretation:
- `20260421-210119` and `20260421-211853` look like narrow baseline captures of a simpler runtime path.
- `20260421-212139` and `20260421-212533` clearly exercised a much wider shader surface.
- `20260421-220041` is the broadest successful capture so far and should currently be treated as the primary wide-coverage reference session.

## Current Catalog Baseline

Current generated catalog inputs:
- `20260421-212139`
- `20260421-212533`
- `20260421-220041`

Current generated catalog outputs:
- [shader-catalog.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.md)
- [shader-catalog.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.json)

Current catalog totals:
- `1922` unique shader hashes in the broad stable union
- `1043` pixel shaders
- `879` vertex shaders

Current support buckets across the three-capture catalog baseline:
- `1757` hashes appear in all three captures
- `8` hashes appear in exactly two captures
- `157` hashes appear in exactly one capture

Interpretation:
- the catalog baseline is broader than any single capture
- most of the stable wide surface is strongly recurrent across all three reference runs
- the single-capture tail remains meaningful enough to preserve explicitly in the catalog instead of collapsing to “common-only”

## Recurrent Shader Families

These are not semantic names yet. They are current clustering anchors derived from reflection signatures and repeat patterns.

High-repeat baseline family examples:
- repeated `ps` hash:
  - `41eb4c6251a1fc87ce73679170d92c94eeca53997ae5162cbbe93c0e7467081d`
- repeated `vs` hashes include:
  - `162c887d37290c1d5407855cfd1388ceb45b3eeb57098c899b7431e2ee7e2510`
  - `243a3cd79674b9af7d31ffac12cd276f5f7f9fd7ee0f749b6b90b83de7d9d23d`
  - `303ada98e2dd83f908eb8419fb363444fdb45c90f227d19c30cb9577e3c99bb0`
  - `36f2fddea2eb69eaeaa75dec8b488737fc834772c6cb00adb7342f538b4901a1`

Common reflection-signature families seen in broader captures:
- `ps br=0 cb=0 in=8 out=1`
- `ps br=0 cb=0 in=9 out=1`
- `ps br=0 cb=0 in=7 out=1`
- `ps br=0 cb=0 in=3 out=1`
- `vs br=0 cb=0 in=12 out=10`
- `vs br=0 cb=0 in=12 out=11`
- `vs br=0 cb=0 in=10 out=8`
- `vs br=0 cb=0 in=11 out=8`

Additional families strengthened by the long mixed-mode run:
- `ps br=0 cb=0 in=6 out=1`
- `ps br=0 cb=0 in=2 out=1`
- `ps br=0 cb=0 in=4 out=1`
- `ps br=0 cb=0 in=1 out=1`
- `ps br=4 cb=0 in=5 out=1`
- `vs br=0 cb=0 in=9 out=8`
- `vs br=0 cb=0 in=10 out=1`
- `vs br=0 cb=0 in=12 out=8`
- `vs br=0 cb=0 in=12 out=9`
- `vs br=0 cb=0 in=3 out=6`
- `vs br=0 cb=0 in=3 out=7`

These signatures are strong candidates for the first shader-family buckets in future documentation and code-assisted analysis.

## Comparison Findings

### Single-capture comparison

From:
- [compare-20260421-212139-vs-20260421-212533.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/captures/live/reports/compare-20260421-212139-vs-20260421-212533.md:1)

Findings:
- common shader hashes: `1757`
- left-only shader hashes: `50`
- right-only shader hashes: `4`

Interpretation:
- those two broader successful captures are highly similar
- most of the large shader surface is stable across both runs
- the remaining unique hashes are small enough to inspect manually later

### Long-run expansion against the previous broad baseline

From:
- [compare-20260421-212533-vs-20260421-220041.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/captures/live/reports/compare-20260421-212533-vs-20260421-220041.md:1)

Findings:
- common shader hashes: `1761`
- left-only shader hashes: `0`
- right-only shader hashes: `115`

Interpretation:
- the long mixed-mode run strictly supersets the previous `20260421-212533` wide baseline
- no previously known hashes disappeared
- the run added a meaningful but bounded set of new runtime-used shaders
- the new delta is dominated by:
  - `ps br=0 cb=0 in=7 out=1`
  - `ps br=0 cb=0 in=8 out=1`
  - `ps br=0 cb=0 in=9 out=1`
  - `ps br=0 cb=0 in=6 out=1`
  - `vs br=0 cb=0 in=9 out=8`
  - `vs br=0 cb=0 in=10 out=1`
  - `vs br=0 cb=0 in=12 out=8`
  - `vs br=0 cb=0 in=12 out=9`
  - `vs br=0 cb=0 in=3 out=6`
  - `vs br=0 cb=0 in=3 out=7`

### Group comparison

From:
- [group-compare-20260421-210119-vs-20260421-212139.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/captures/live/reports/group-compare-20260421-210119-vs-20260421-212139.md:1)

Group A:
- `20260421-210119`
- `20260421-211853`

Group B:
- `20260421-212139`
- `20260421-212533`

Findings:
- common shader hashes: `521`
- group-A-only hashes: `0`
- group-B-only hashes: `1290`

Interpretation:
- the simpler successful captures are a strict subset of the broader ones
- the broader runs activated a large additional shader population
- this is useful as a practical “wide coverage” baseline for future map expansion

### Broad-group versus long mixed-mode run

From:
- [group-compare-broad-vs-20260421-220041.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/captures/live/reports/group-compare-broad-vs-20260421-220041.md:1)

Left group:
- `20260421-212139`
- `20260421-212533`

Right group:
- `20260421-220041`

Findings:
- common shader hashes: `1765`
- broad-group-only hashes: `46`
- long-run-only hashes: `111`

Interpretation:
- the long run expands coverage substantially, but not perfectly monotonically against the union of the two prior wide sessions
- some hashes still appear only in the older broad runs
- this means “broadest single run” is not the same as “complete union coverage”
- future documentation should preserve both:
  - the best single-run reference
  - the best multi-run union

## Current Working Hypotheses

Hypothesis:
- the wide-capture-only shader population likely corresponds to additional gameplay/CAS/build/buy paths that were simply not exercised in the narrow runs

Hypothesis:
- reflection-signature families with `ps br=0 cb=0 in=[7..10] out=1` are likely broad utility/material-pass families rather than rare scene-specific one-offs

Hypothesis:
- reflection-signature families with large VS output counts such as `vs br=0 cb=0 in=12 out=10/11` are good starting points for geometry/material family grouping

Hypothesis:
- the long-run-only delta is biased toward gameplay-mode diversity rather than one isolated render subsystem, because both PS and VS families expand together and the added signatures span several nearby input/output shapes

These remain hypotheses until linked to more controlled capture contexts or code/binary clues.

## Open Questions

- Which reflection-signature families are CAS-heavy versus Live-mode-heavy versus Build/Buy-heavy?
- Which high-repeat hashes are true “core engine/common material” shaders?
- Which rare hashes are context-specific but stable across repeated visits to the same gameplay mode?
- Does TS4 use a command-list path we are not observing, or is deferred context creation mostly incidental in these runs?
- Where is the safest correlation point between stable shader creation and meaningful runtime usage if deferred draw/state hooks remain crash-prone?
- Which of the `111` long-run-only hashes from `20260421-220041` are tied to:
  - CAS-specific interactions
  - Live-mode simulation views
  - Build/Buy placement and thumbnails
  - UI-heavy overlay states

## Next Documentation Update Rules

When new successful long runs are available, update this file with:
- new representative sessions and counts
- newly stable comparison reports
- any newly recurring reflection-signature families
- any newly identified core hashes or suspected semantic clusters
- any changes to the safe/risky hook matrix
