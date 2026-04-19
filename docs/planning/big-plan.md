# Big Plan

This file tracks the durable roadmap for the whole project.

## End Goal

Build a read-only Sims 4 explorer that can move from raw package browsing to honest logical asset viewing, preview, diagnostics, and export without pretending unsupported paths are solved.

## Workstreams

### 1. Repository Hygiene And Operating System

Status: `In progress`

- [x] Establish a stable documentation home under `docs/`
- [x] Add durable planning documents for roadmap, active block, debt, and unknowns
- [x] Consolidate repo-level working rules into `AGENT.md`
- [x] Establish a durable multi-agent workflow with packet ownership, verifier separation, and handoff rules
- [x] Make `docs/planning/current-plan.md` the mandatory live plan with `Problem`, `Approach`, `Actions`, and `Restart hints`
- [x] Reduce duplicated process guidance so `docs/README.md` and `docs/operations/chat-handoff.md` act as entry points instead of second sources of truth
- [ ] Finish classifying old probe outputs and one-off artifacts
- [ ] Promote any still-useful scratch probes into named tools or documented workflows

### 2. Metadata-First Package And Indexing Platform

Status: `Usable, ongoing hardening`

- [x] Package scanning and persistent SQLite indexing
- [x] Shard-aware rebuild/activation flow
- [x] First persisted metadata/fact enrichment passes
- [ ] Remove runtime-persisted lazy enrichment from serving index read paths
- [ ] Move any runtime-required derived metadata into explicit indexing/finalization passes with version invalidation
- [x] Progress and diagnostics for indexing
- [ ] Keep reducing hot-path parsing cost and operational rough edges

### 3. Raw Resource Browser

Status: `Baseline done`

- [x] Raw resource browsing and export
- [x] Search/filter on indexed metadata
- [ ] Continue filling factual enrichment gaps for important raw types

### 4. Build/Buy Honest Viewer

Status: `Partially done`

- [x] First real static Build/Buy scene slice
- [x] In-app preview and export for the supported subset
- [x] Honest unsupported diagnostics for the rest
- [ ] Deepen identity resolution and material fidelity without asset-specific hacks

### 5. CAS Honest Viewer

Status: `Partially done`

- [x] First real human-first `CASPart` slice
- [x] Variant/swatch indexing
- [x] Real `GEOM`-backed preview path for the supported subset
- [ ] Keep expanding exact scene/material coverage from authoritative data paths
- [ ] Converge `BuildBuy`, `CAS`, and `Sim` onto one shared material/texture/UV-routing pipeline instead of separate domain-specific mapping rules

### 6. Sim Domain And Character Assembly

Status: `Main frontier`

- [x] Metadata-heavy `Sim Archetypes` rooted in grouped `SimInfo`
- [x] Body-first inspector, candidate families, and graph diagnostics
- [x] First authoritative body-part selection groundwork
- [ ] Make runtime character assembly primarily an index-driven graph walk from archetype/template root to authoritative linked resources
- [ ] Real authoritative torso/head/body graph
- [ ] Rig selection and assembled character path
- [ ] Skintone, region-map, and material synthesis
- [ ] Reuse the shared canonical material/texture/UV-routing pipeline so `Sim` does not grow its own divergent texture-mapping logic
- [ ] Morph and deformer application
- [ ] Honest full-character export

### 7. Canonical Scene And Export Fidelity

Status: `In progress`

- [x] Canonical scene direction exists in architecture
- [x] Export already works for the supported Build/Buy slice
- [ ] Reuse one canonical scene model more consistently across preview/export paths
- [ ] Improve shader/material mapping without overselling fidelity
- [ ] Keep shader/material/UV decode rules centralized so improvements learned in one asset domain land in the others automatically

### 8. Research Discipline And Fixture Coverage

Status: `In progress`

- [x] Curated internal wiki exists
- [x] External local reference snapshots are preserved
- [x] Tests cover many vertical-slice regressions
- [ ] Keep converting ad hoc discovery into durable docs, fixtures, and tests

## Priority Order Right Now

1. Finish repo cleanup and make the operating docs reliable.
2. Resume honest `Sim` assembly from authoritative selections.
3. Keep paying down architectural sprawl as new `Sim` layers land.

The multi-agent operating model now overlays those priorities rather than blocking them: new frontier work should run through packetized manager/explorer/worker/verifier loops instead of reverting to one-agent broad sweeps.

## Success Criteria

- A resumed session can recover project intent from the repo itself.
- Repo-level working rules are readable from one place.
- Durable docs clearly separate roadmap, current work, debt, and unknowns.
- Reference material is easy to find and no longer scattered across root and temp folders.
- The app keeps moving toward authoritative viewers instead of prettier heuristics.
