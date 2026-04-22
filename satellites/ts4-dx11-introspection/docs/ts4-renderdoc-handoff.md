# TS4 RenderDoc Handoff For Main Project

Generated: `2026-04-22`

## Purpose

This document is the direct handoff from the `ts4-dx11-introspection` satellite to the main project.

Its purpose is to transfer the maximum currently-useful information extracted from external GPU frame captures, in a form that the main project can ingest and use immediately.

This document does **not** attempt to restate the full shader catalog. It focuses on the practical information gained from the two RenderDoc captures that the main project can apply now:

- scene/pass ownership
- pass-family classification
- representative draw-level shader/interface evidence
- recommended ingest contract
- package-material matching strategy
- current hard blockers

## Source Captures

The current handoff is based on these captures and generated analysis outputs:

### CAS Capture

- Capture: [yafem_amale_table.rdc](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/yafem_amale_table.rdc)
- Summary: [renderdoc-summary.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/yafem_amale_table/renderdoc-summary.md)
- Summary JSON: [renderdoc-summary.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/yafem_amale_table/renderdoc-summary.json)
- Enriched draws: [renderdoc-draws.enriched.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/yafem_amale_table/renderdoc-draws.enriched.json)

### World / Room Capture

- Capture: [room_top_view.rdc](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/room_top_view.rdc)
- Summary: [renderdoc-summary.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/room_top_view/renderdoc-summary.md)
- Summary JSON: [renderdoc-summary.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/room_top_view/renderdoc-summary.json)
- Enriched draws: [renderdoc-draws.enriched.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/ender_doc/analysis/room_top_view/renderdoc-draws.enriched.json)

## High-Confidence Facts

These statements are now grounded in external GPU capture, not only in heuristic inference:

1. TS4 frames are cleanly partitioned into multiple pass families.
2. At least some frames have explicit `Depth-only` passes before major color/material passes.
3. CAS-style frames and world/room-style frames have materially different pass structure.
4. There are clearly separable `material-like` passes and `screen/compositor-like` passes.
5. A useful amount of material-domain inference is already possible from:
   - pass name
   - draw size
   - vertex input vocabulary
   - resource vocabulary
   - constant-buffer vocabulary
6. RenderDoc-replayed shader bytecode currently does **not** hash into the same identity space as the branch's live DX11 runtime catalog. Scene/pass truth is already useful, but direct shader-hash closure is still missing.

## Why This Matters To The Main Project

The main project's current blocker is not “lack of more shader names”. The blocker is lack of closure across:

- package-side authored material data (`MATD`, `MTST`, package-side shader/profile ids)
- runtime shader families
- scene ownership
- pass/draw context

These RenderDoc captures close one major part of that gap:

- they provide **scene-pass ownership truth**
- they distinguish likely `material-like` rendering from `depth-only` or `screen/compositor` rendering
- they make it possible to filter out many false `MATD/MTST -> runtime shader` hypotheses before exact hash closure exists

## Recommended Ingest Contract

The main project should ingest only a small, stable subset of the capture-derived data.

### Capture-Level Model

Suggested record:

```csharp
public sealed record ExternalGpuCaptureEvidence(
    string CaptureName,
    string SceneDomain,
    int DrawCount,
    int ActionCount,
    IReadOnlyList<PassEvidence> Passes
);
```

Field guidance:

- `CaptureName`: filename stem, e.g. `yafem_amale_table`, `room_top_view`
- `SceneDomain`: initial manual domain label
  - `CAS`
  - `WorldRoom`
- `DrawCount`: `drawCount` from `renderdoc-summary.json`
- `ActionCount`: `actionCount` from `renderdoc-summary.json`
- `Passes`: derived from `topPasses` plus optional deeper per-draw aggregation

### Pass-Level Model

Suggested record:

```csharp
public sealed record PassEvidence(
    string CaptureName,
    string SceneDomain,
    string PassName,
    string PassClass,
    int DrawCount,
    int SampleEventId,
    string? RepresentativeVsHash,
    string? RepresentativePsHash,
    IReadOnlyList<string> ResourceVocabulary,
    IReadOnlyList<string> ConstantBufferVocabulary,
    IReadOnlyList<string> VertexInputVocabulary
);
```

`PassClass` should currently be one of:

- `DepthOnly`
- `MaterialLike`
- `CompositorOrUI`
- `Unknown`

### Draw-Level Model

Suggested record:

```csharp
public sealed record DrawEvidence(
    string CaptureName,
    string SceneDomain,
    string PassName,
    int EventId,
    int NumIndices,
    int NumInstances,
    string? VsHash,
    string? PsHash,
    IReadOnlyList<string> VertexInputs,
    IReadOnlyList<string> VsResourceNames,
    IReadOnlyList<string> PsResourceNames,
    IReadOnlyList<string> VsConstantBlocks,
    IReadOnlyList<string> PsConstantBlocks
);
```

Only the following source fields are recommended for initial ingest:

- `topMarker`
- `eventId`
- `numIndices`
- `numInstances`
- `vertexInputNames`
- `vs.hash`
- `ps.hash`
- `vs.resourceNames`
- `ps.resourceNames`
- `vs.constantBlocks`
- `ps.constantBlocks`

Do **not** make the first integration depend on all raw RenderDoc replay metadata.

## Pass Classification Rules

The main project can use the following rules immediately.

### `DepthOnly`

Classify a pass as `DepthOnly` when most draws have:

- no pixel shader evidence or `RepresentativePsHash == null`
- large indexed geometry draws
- no meaningful material resource vocabulary
- pass names containing `Depth-only`

### `MaterialLike`

Classify a pass as `MaterialLike` when most draws have:

- large indexed geometry draws
- depth present or depth-adjacent color pass structure
- stable texture vocabulary such as `texture0`, `texture1`, `texture2`, ...
- stable constant-buffer vocabulary such as `cbuffer0`, `cbuffer2`, `cbuffer7`
- non-screen vertex inputs

### `CompositorOrUI`

Classify a pass as `CompositorOrUI` when draws show:

- many small draws
- `COLOR`, `SV_Position`, or otherwise screen-space oriented inputs
- texture vocabulary like `tex[0]`, `tex[1]`, `tex`
- optional `Constants`
- pass layout consistent with late screen composition rather than object/world geometry

## Capture 1: CAS Evidence

Capture name: `yafem_amale_table`

### Summary

- Draw calls: `89`
- Actions: `123`
- Present count: `1`

### Primary Passes

| Pass | Draws | Current Class | Notes |
| --- | ---: | --- | --- |
| `Colour Pass #5 (1 Targets)` | 52 | `CompositorOrUI` | Strong screen/compositor signature; resources `tex[0]`, `tex[1]`, sometimes `Constants` |
| `Colour Pass #3 (1 Targets + Depth)` | 15 | `MaterialLike` | Strong CAS material candidate; repeated multi-texture bindings and CB usage |
| `Colour Pass #2 (1 Targets)` | 10 | `Unknown` | Geometry present, but no useful PS evidence in summary |
| `<unmarked>` | 8 | `Unknown` | Mixed or early-stage geometry |
| `Colour Pass #1 (1 Targets)` | 3 | `Unknown` | Too small for strong ownership claim |
| `Colour Pass #4 (1 Targets)` | 1 | `Unknown` | Single draw, insufficient to classify strongly |

### Strongest CAS Material-Like Evidence

Representative events:

- `522`
- `532`

Observed properties:

- Pass: `Colour Pass #3 (1 Targets + Depth)`
- VS hash: `152410d157b9e8bf895dcaa2e277c7fd0ab2a2f939e0dc85631bdebaa0aed8aa`
- PS hash: `c91ba73b429ce3c25469c84e639dcb18deb1d27f78d8f76915b58008546a1d34`
- PS resources:
  - `texture0`
  - `texture1`
  - `texture2`
  - `texture3`
  - `texture4`
- PS constant buffers:
  - `cbuffer0`
  - `cbuffer7`
- Vertex inputs:
  - `POSITION`

Interpretation:

- This is a strong candidate for “actual visible CAS character/material rendering”.
- The multi-texture vocabulary makes it much more plausible as clothing/body/hair-like material work than as UI or simple full-screen composition.

### Strongest CAS Compositor / Screen Evidence

Representative event:

- `996`

Observed properties:

- Pass: `Colour Pass #5 (1 Targets)`
- VS hash: `fa885f3261ac4ca25f9012169f54ab9a5b33a39d94b80b5297adcd213b56eeca`
- PS hash: `c953833490b8a1d4589083c9c7fae2153fb42c5a53d7eb6bcca5b1eb5734cff1`
- PS resources:
  - `tex[0]`
  - `tex[1]`
- Vertex inputs:
  - `COLOR`
  - `SV_Position`

Interpretation:

- This should **not** be treated as evidence for normal authored object/clothing material ownership.
- This is a good negative-control pattern for filtering out compositor/UI-like passes.

## Capture 2: World / Room Evidence

Capture name: `room_top_view`

### Summary

- Draw calls: `749`
- Actions: `802`
- Present count: `1`

### Primary Passes

| Pass | Draws | Current Class | Notes |
| --- | ---: | --- | --- |
| `Depth-only Pass #2` | 188 | `DepthOnly` | Strong explicit depth prepass |
| `Colour Pass #6 (1 Targets + Depth)` | 180 | `MaterialLike` | Strong world/object material pass |
| `Colour Pass #10 (1 Targets)` | 129 | `CompositorOrUI` | Strong late compositor/UI-like pattern |
| `Depth-only Pass #1` | 99 | `DepthOnly` | Strong explicit depth prepass |
| `Colour Pass #3 (1 Targets + Depth)` | 75 | `MaterialLike` | Additional world/object material pass |
| `<unmarked>` | 25 | `Unknown` | Mixed or early-stage geometry |
| `Colour Pass #8 (1 Targets + Depth)` | 22 | `MaterialLike` | Smaller but still material-like |
| `Colour Pass #1 (1 Targets + Depth)` | 14 | `MaterialLike` | Small but still geometry/material oriented |
| `Depth-only Pass #3` | 6 | `DepthOnly` | Minor depth path |

### Strongest World Material-Like Evidence

Representative events:

- `5759`
- `5822`
- `5733`
- `5744`

Observed properties:

- Main pass family: `Colour Pass #6 (1 Targets + Depth)`
- Example PS resources:
  - `texture0`
  - `texture1`
  - `texture2`
  - `texture3`
  - `texture4`
- Example PS constant buffers:
  - `cbuffer0`
  - `cbuffer2`
- Example draw sizes:
  - `22155`
  - `20346`
  - `18924`
  - `12864`

Interpretation:

- This is a strong candidate for “real room/world/object/furniture material rendering”.
- The combination of large indexed draws, depth participation, and multi-texture bindings is exactly the kind of evidence the main project needs when separating actual authored material usage from helper/compositor work.

### Strongest World Compositor / Screen Evidence

Representative pass:

- `Colour Pass #10 (1 Targets)`

Observed properties:

- resources:
  - `tex[0]`
  - `tex[1]`
- representative VS/PS pair:
  - `fa885f3261ac4ca25f9012169f54ab9a5b33a39d94b80b5297adcd213b56eeca`
  - `c953833490b8a1d4589083c9c7fae2153fb42c5a53d7eb6bcca5b1eb5734cff1`

Interpretation:

- This strongly resembles the CAS compositor-style pattern.
- This is useful because it gives the main project a repeated cross-scene “screen/compositor family” signature to exclude from normal material ownership hypotheses.

### Strongest Depth-Only Evidence

Representative passes:

- `Depth-only Pass #1`
- `Depth-only Pass #2`
- `Depth-only Pass #3`

Observed properties:

- no meaningful PS ownership
- large indexed geometry draws
- strong geometric overlap with later color/material work

Interpretation:

- These should be modeled as helper/depth-prepass families, not authored material families.
- Their presence is useful when building scene-pass closure: they mark geometry participation without proving final visible material ownership.

## Repeated Cross-Capture Evidence

The following pattern now appears in both captures:

- a compositor/screen-style pass with:
  - `tex[0]`
  - `tex[1]`
  - representative VS hash `fa885f...`
  - representative PS hash `c953833...`

Current interpretation:

- This is likely a reusable compositor/UI/helper family, not a domain-specific authored material family.
- The main project should treat this as exclusion evidence when attempting `MATD/MTST -> visible material pass` mapping.

## What The Main Project Can Do With This Immediately

### 1. Introduce Pass-Family Ownership Labels

The main project can now explicitly assign ownership labels such as:

- `CasMaterialLike`
- `WorldMaterialLike`
- `DepthOnlyHelper`
- `CompositorOrUi`
- `Unknown`

These labels should be attached to imported `PassEvidence`.

### 2. Filter Candidate Runtime Families By Scene Domain

Material/profile hypotheses should be filtered by scene domain before deeper matching.

Examples:

- `CAS` materials should be matched only against `CAS` captures and `MaterialLike` pass families.
- room/world/object materials should be matched only against `WorldRoom` captures and `MaterialLike` pass families.
- `CompositorOrUi` passes should not be treated as normal authored object/clothing materials unless there is explicit contrary evidence.

### 3. Use Resource Vocabulary As Negative And Positive Evidence

Current practical heuristic:

- `texture0..N` plus `cbuffer0/cbuffer2/cbuffer7` is positive evidence for geometry/material-style passes.
- `tex[0]`, `tex[1]`, `tex`, `Constants`, `COLOR`, `SV_Position` is strong evidence for compositor/screen-space work.

This does not prove exact material ownership, but it sharply narrows the candidate set.

### 4. Use Depth-Only Passes To Recognize Helper Paths

If a prospective runtime family is only observed in `DepthOnly` passes, do not treat it as the final visible material owner.

It is helper evidence, not visible shading closure.

## Recommended Matching Strategy

Until exact shader-identity normalization exists, the main project should match package-side materials to external GPU capture evidence by **signature**, not by direct hash.

### Package-Side Material Signature

Build a normalized signature per authored material candidate from:

- `MATD.shader`
- texture slot names
- texture slot count
- parameter names
- UV-related fields
- `MTST` state context
- asset domain

### Pass Signature

Build a normalized signature per captured pass from:

- `scene_domain`
- `pass_class`
- resource vocabulary
- constant-buffer vocabulary
- vertex-input vocabulary
- draw scale

### Candidate Match Scoring

Score a package material candidate against a pass family using:

- domain compatibility
- pass-class compatibility
- resource-vocabulary compatibility
- constant-buffer-vocabulary compatibility
- geometry/vertex-input plausibility

Recommended rule:

- do not promote a candidate to “probable ownership” if its best matching pass family is `CompositorOrUi` or `DepthOnly`

## Minimal Data The Main Project Should Ingest First

If only a minimal first integration is desired, ingest only:

- capture name
- scene domain
- pass name
- pass class
- draw count
- representative VS hash
- representative PS hash
- pass resource vocabulary
- pass constant-buffer vocabulary
- representative vertex-input vocabulary

This is sufficient to start:

- pass-family ownership classification
- scene-domain filtering
- exclusion of compositor/depth helper paths
- first-pass `MATD/MTST -> candidate pass family` mapping

## Current Hard Blocker

The main remaining blocker is now sharply defined:

- RenderDoc shader bytecode `SHA-256` values do **not** match the branch's live DX11 shader catalog hash space.

Current implications:

- external GPU capture now gives reliable scene/pass truth
- live DX11 runtime capture gives shader inventory and reflection/disassembly truth
- but the two identity spaces are not yet normalized

The missing bridge is:

- external RenderDoc replay shader identity
- live in-game DX11 shader identity
- package-side material/profile identity

This is the next technical closure problem.

## What This Document Does Not Claim

This handoff does **not** claim:

- exact `MATD` / `MTST` -> shader hash closure
- exact authored-material ownership for every pass
- that one pass equals one authored material
- that the repeated RenderDoc shader hashes are already the same hashes as the live runtime catalog

Instead, it claims something narrower and stronger:

- the main project now has reliable external evidence for pass-family ownership and scene-pass structure
- this evidence is already useful for filtering and ranking material hypotheses

## Immediate Main-Project Action

The recommended immediate action is:

1. Add import for the RenderDoc-derived pass evidence models described above.
2. Label imported passes with `PassClass`.
3. Use scene domain plus pass class as a hard filter for package-material matching.
4. Treat `texture0..N` material-like passes as positive candidates.
5. Treat `tex[0]/tex[1]` compositor-like passes as exclusion evidence for ordinary authored material ownership.
6. Keep exact shader-hash closure as a separate later bridge task, not as a prerequisite for using this evidence.
