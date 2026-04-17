# Big Plan

This file tracks the durable roadmap for the whole project.

## End Goal

Build a read-only Sims 4 explorer that can move from raw package browsing to honest logical asset viewing, preview, diagnostics, and export without pretending unsupported paths are solved.

## Workstreams

### 1. Repository Hygiene And Operating System

Status: `In progress`

- [x] Establish a stable documentation home under `docs/`
- [x] Add durable planning documents for roadmap, active block, debt, and unknowns
- [x] Consolidate repo-level agent instructions and handoff guidance
- [ ] Finish classifying old probe outputs and one-off artifacts
- [ ] Promote any still-useful scratch probes into named tools or documented workflows

### 2. Metadata-First Package And Indexing Platform

Status: `Usable, ongoing hardening`

- [x] Package scanning and persistent SQLite indexing
- [x] Shard-aware rebuild/activation flow
- [x] Lazy metadata enrichment
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

### 6. Sim Domain And Character Assembly

Status: `Main frontier`

- [x] Metadata-heavy `Sim Archetypes` rooted in grouped `SimInfo`
- [x] Body-first inspector, candidate families, and graph diagnostics
- [x] First authoritative body-part selection groundwork
- [ ] Real authoritative torso/head/body graph
- [ ] Rig selection and assembled character path
- [ ] Skintone, region-map, and material synthesis
- [ ] Morph and deformer application
- [ ] Honest full-character export

### 7. Canonical Scene And Export Fidelity

Status: `In progress`

- [x] Canonical scene direction exists in architecture
- [x] Export already works for the supported Build/Buy slice
- [ ] Reuse one canonical scene model more consistently across preview/export paths
- [ ] Improve shader/material mapping without overselling fidelity

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

## Success Criteria

- A resumed session can recover project intent from the repo itself.
- Durable docs clearly separate roadmap, current work, debt, and unknowns.
- Reference material is easy to find and no longer scattered across root and temp folders.
- The app keeps moving toward authoritative viewers instead of prettier heuristics.
