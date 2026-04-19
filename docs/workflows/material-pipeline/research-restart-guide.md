# TS4 Material Research Restart Guide

Use this file when the material/shader research task is resumed in a new chat.

This is a continuation guide for the external-first TS4 material, texture, shader, and UV research track. It is not an implementation guide.

## Task goal

Build a durable knowledge base for how TS4 materials actually work by prioritizing external sources, creator tooling, and externally sourced local snapshots, then distilling those findings into bounded family and authority documents.

## Red lines

- do not treat current repo code as the source of truth for TS4 material behavior
- do not rewrite external evidence so it matches the current broken implementation
- do not promote local decoder output, corpus buckets, or probe summaries into proof
- do not collapse narrow edge families back into generic diffuse, alpha, shell, or fallback stories without external proof

## Trust ladder

Use sources in this order:

1. external TS4 creator-facing or format-facing sources
2. local snapshots of external tools checked into the repo, such as `TS4SimRipper`
3. local corpus and precompiled summaries only for candidate-target isolation
4. current repo code only as implementation boundary, failure boundary, or hypothesis source

Safe rule:

- if a statement depends mainly on current repo behavior, phrase it as a repo limitation or current boundary, not as TS4 truth
- if a statement depends mainly on where an input was discovered (`BuildBuy`, `CAS`, `Sim`), do not turn that into a domain-specific shader rule

## Working route

Resume in this order:

1. [Shared TS4 Material, Texture, And UV Pipeline](../../shared-ts4-material-texture-pipeline.md)
2. [Build/Buy Material Authority Matrix](buildbuy-material-authority-matrix.md)
3. [CAS/Sim Material Authority Matrix](cas-sim-material-authority-matrix.md)
4. [Shader Family Registry](shader-family-registry.md)
5. [Edge-Family Matrix](edge-family-matrix.md)
6. [P1 Live-Proof Queue](p1-live-proof-queue.md)
7. [Live-Proof Packets](live-proof-packets/README.md)

Use these supporting layers as needed:

- [Family Sheets](family-sheets/README.md)
- [Skintone And Overlay Compositor](skintone-and-overlay-compositor.md)
- [Source map and trust levels](../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Current state

These layers already exist and should be continued, not replaced:

- external-first `Build/Buy Material Authority Matrix`
- external-first `Shader Family Registry`
- `Family Sheets` for narrow family packets
- `Edge-Family Matrix` for row-by-row authority synthesis
- `P1 Live-Proof Queue` for candidate-target ordering
- `Live-Proof Packets` for concrete inspection packets

Current concrete live-proof packets:

- [SimGlass Versus Shell Baseline](live-proof-packets/simglass-vs-shell-baseline.md)
- [SimSkin Versus SimSkinMask](live-proof-packets/simskin-vs-simskinmask.md)
- [CASHotSpotAtlas Carry-Through](live-proof-packets/cas-hotspotatlas-carry-through.md)
- [ShaderDayNightParameters Visible-Pass Proof](live-proof-packets/shader-daynight-visible-pass.md)
- [GenerateSpotLightmap And NextFloorLightMapXform](live-proof-packets/generate-spotlightmap-nextfloorlightmapxform.md)
- [RefractionMap Live Proof](live-proof-packets/refractionmap-live-proof.md)

## Immediate next work

Resume the queue from the first unfinished concrete packet:

1. start from the now-named `RefractionMap` bridge root `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad -> 01661233:00000000:00F643B0FDD2F1F7`
2. keep treating that root as a bridge into the refraction/projective neighborhood, not as direct closure on exact `RefractionMap` slot semantics
3. use the external `OBJD/COBJ -> Model -> MLOD -> MATD/MTST` chain plus the local `OBJD` candidate `01661233:00000000:FDD2F1F700F643B0` and `instance-swap32` resolution as the durable linkage anchor
4. keep `0124E3B8AC7BEE62` only as the mixed boundary/control case for refraction, because `00F643` is now the cleaner named fixture
5. when resuming `SimGlass`, skip the already-bounded `EP10` obvious glass/window packet and search from the broader transparent-decor cluster instead:
   - `fishBowl_EP10GENmarimo -> 01661233:00000000:FAE0318F3711431D`
   - `shelfFloor2x1_EP10TEAdisplayShelf -> 01661233:00000000:E779C31F25406B73`
   - `shelfFloor2x1_EP10TEAshopDisplayTileable -> 01661233:00000000:93EE8A0CF97A3861`
   - `lightWall_EP10GENlantern -> 01661233:00000000:F4A27FC1857F08D4`
   - `mirrorWall1x1_EP10BATHsunrise -> 01661233:00000000:3CD0344C1824BDDD`
6. treat that cluster as survey-backed search anchors, not closed fixtures: those transformed roots do appear in `tmp/probe_all_buildbuy.txt`, but current direct reopen attempts still return `Build/Buy asset not found`

Safe continuation rule:

- finish the next concrete packet and then fold only the proved part back into the matrix and registry layers
- keep the architectural split explicit:
  - domain-specific discovery and authority order are allowed
  - shared shader/material semantics after canonical-material decoding are the target

## Expected packet shape

Each new live-proof packet should answer:

1. what is already externally proved
2. what local candidate target is being inspected next
3. what exact claim the packet is trying to prove or falsify
4. what current implementation mistake becomes easier to diagnose after that proof

## Mandatory update targets after each run

When a packet advances, update the smallest set that preserves restart continuity:

- the packet itself under `live-proof-packets/`
- [Live-Proof Packets](live-proof-packets/README.md)
- [P1 Live-Proof Queue](p1-live-proof-queue.md)
- [Edge-Family Matrix](edge-family-matrix.md)
- [Current plan](../../planning/current-plan.md)

Current refraction named-fixture anchor:

- `EP10\\ClientFullBuild0.package | sculptFountainSurface3x3_EP10GENlilyPad`
- `ObjectDefinition = C0DB5AE7:00000000:000000000003FC7F`
- `ObjectCatalog = 319E4F1D:00000000:000000000003FC7F`
- `OBJD model candidate = 01661233:00000000:FDD2F1F700F643B0`
- `resolved model root = 01661233:00000000:00F643B0FDD2F1F7` via `instance-swap32`

Current broader `SimGlass` candidate cluster:

- `fishBowl_EP10GENmarimo_set1..6`
  - `OBJD model candidate = 01661233:00000000:3711431DFAE0318F`
  - transformed root = `01661233:00000000:FAE0318F3711431D`
- `shelfFloor2x1_EP10TEAdisplayShelf_set1..10`
  - `OBJD model candidate = 01661233:00000000:25406B73E779C31F`
  - transformed root = `01661233:00000000:E779C31F25406B73`
- `shelfFloor2x1_EP10TEAshopDisplayTileable_set1..8`
  - `OBJD model candidate = 01661233:00000000:F97A386193EE8A0C`
  - transformed root = `01661233:00000000:93EE8A0CF97A3861`
- `lightWall_EP10GENlantern_set1..9`
  - `OBJD model candidate = 01661233:00000000:857F08D4F4A27FC1`
  - transformed root = `01661233:00000000:F4A27FC1857F08D4`
- `mirrorWall1x1_EP10BATHsunrise_set1..10`
  - `OBJD model candidate = 01661233:00000000:1824BDDD3CD0344C`
  - transformed root = `01661233:00000000:3CD0344C1824BDDD`
- all five transformed roots are present in `tmp/probe_all_buildbuy.txt`
- current direct reopen attempts still fail, so this is a narrowed route for the next packet, not a closed live-proof result

Update broader indexes only if navigation changed:

- [Material Pipeline Deep Dives](README.md)
- [Knowledge Map](../../knowledge-map.md)
- [Documentation Map](../../README.md)

## Report format contract

End each run with a compact tree-style status report.

Rules:

- put the colored square at the start of the line
- show increments only for sections that actually changed in that run
- do not show increments on untouched rows
- do not show increments on `100%` rows
- prefer the narrow subtree that actually moved over a full giant matrix dump

Expected shape:

```text
Shared TS4 Material / Texture / UV Pipeline
├─ 🟨 Shader-family registry ~ 84% (+1%)
├─ 🟨 Authority and fallback matrix ~ 81% (+1%)
│  └─ 🟥 Edge-family full matrix ~ 48% (+3%)
└─ 🟨 Open gaps ~ 53% (+1%)
   └─ 🟨 Full per-family CAS/Sim authority order ~ 68% (+2%)
```

## Multi-agent guidance

Multiple agents are allowed when they help, but keep them bounded:

- use explorers for narrow evidence lookup or codebase location questions
- keep write ownership disjoint if workers are used
- do not delegate the immediate blocking research judgment
- if agents stall, continue locally instead of blocking the whole run

## One-sentence continuity rule

External evidence defines the knowledge; local repo behavior only explains where the current implementation still fails.
