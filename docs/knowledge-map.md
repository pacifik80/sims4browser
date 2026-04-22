# Knowledge Map

This file is the navigation hub for durable repo knowledge.

Use it when the question is not "how do I export X right now?" but "where does the authoritative knowledge for this topic live, and what evidence layer supports it?"

## Quick Entry Points

If the task is about shared TS4 render/material behavior:

- start with [Shared TS4 Material, Texture, And UV Pipeline](shared-ts4-material-texture-pipeline.md)
- open [Research Restart Guide](workflows/material-pipeline/research-restart-guide.md) first if the task is a resumed external-first research pass
- then jump to [Build/Buy Material Authority Matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md) if the question is object-side or Build/Buy-specific
- then jump to [Build/Buy Stateful Material-Set Seam](workflows/material-pipeline/buildbuy-stateful-material-set-seam.md) if the question is specifically about `MTST`, `MaterialVariant`, or swatch/state selection
- then jump to [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md) if the question is family-specific
- then jump to [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md) if the question is profile- or slot-specific
- then open [Documentation Status Catalog](workflows/material-pipeline/documentation-status-catalog.md) if the question is "what docs exist and how complete are they?"
- then open [Source map and trust levels](references/codex-wiki/04-research-and-sources/01-source-map.md) when you need provenance

If the task is about Sim assembly or body-shell logic:

- start with [Sim domain roadmap](sim-domain-roadmap.md)
- then open [Sim body-shell contract](sim-body-shell-contract.md)
- then jump to [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)

If the task is about package parsing or low-level TS4 file structure:

- start with [Codex wiki](references/codex-wiki/README.md)
- then open the relevant pipeline or implementation guide

If the task is about what remains unknown:

- open [Open questions](references/codex-wiki/04-research-and-sources/03-open-questions.md)
- then check [Current plan](planning/current-plan.md)

## Knowledge Layers

### 1. Normative repo guides

These are the first-entry docs that define the current project stance.

- [Shared TS4 Material, Texture, And UV Pipeline](shared-ts4-material-texture-pipeline.md)
- [Architecture](architecture.md)
- [Supported types](supported-types.md)
- [Known limitations](known-limitations.md)

### 2. Domain and family deep dives

These carry dense topic-specific knowledge that would bloat the main guides if left inline.

- [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)
- [Build/Buy Material Authority Matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md)
- [Build/Buy Stateful Material-Set Seam](workflows/material-pipeline/buildbuy-stateful-material-set-seam.md)
- [Documentation Status Catalog](workflows/material-pipeline/documentation-status-catalog.md)
- [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md)
- [Research Restart Guide](workflows/material-pipeline/research-restart-guide.md)
- [Edge-Family Matrix](workflows/material-pipeline/edge-family-matrix.md)
- [P1 Live-Proof Queue](workflows/material-pipeline/p1-live-proof-queue.md)
- [Live-Proof Packets](workflows/material-pipeline/live-proof-packets/README.md)
- [Skintone And Overlay Compositor](workflows/material-pipeline/skintone-and-overlay-compositor.md)
- [Family Sheets](workflows/material-pipeline/family-sheets/README.md)
- [Sim domain roadmap](sim-domain-roadmap.md)
- [Sim body-shell contract](sim-body-shell-contract.md)

### 3. Task workflows

These are procedure-first docs for operating the app or exports.

- [Workflows index](workflows/README.md)
- [Export Build/Buy object](workflows/export-buildbuy-object.md)
- [Export CAS asset](workflows/export-cas-asset.md)
- [Export raw resource](workflows/export-raw-resource.md)

### 4. Evidence and source layers

These preserve why the repo believes something.

- [References index](references/README.md)
- [Codex wiki](references/codex-wiki/README.md)
- [Source map and trust levels](references/codex-wiki/04-research-and-sources/01-source-map.md)
- [External snapshots](references/external/README.md)

### 5. Planning and unresolved gaps

These are not source-of-truth design docs. They track current direction and remaining holes.

- [Current plan](planning/current-plan.md)
- [Big plan](planning/big-plan.md)
- [Technical debt](planning/technical-debt.md)
- [Unknowns and non-goals](planning/unknowns-and-non-goals.md)

## Task-Oriented Routes

### I need the current material/render rule

1. [Shared TS4 Material, Texture, And UV Pipeline](shared-ts4-material-texture-pipeline.md)
2. [Research Restart Guide](workflows/material-pipeline/research-restart-guide.md)
3. [Build/Buy Material Authority Matrix](workflows/material-pipeline/buildbuy-material-authority-matrix.md)
4. [Build/Buy Stateful Material-Set Seam](workflows/material-pipeline/buildbuy-stateful-material-set-seam.md)
5. [Documentation Status Catalog](workflows/material-pipeline/documentation-status-catalog.md)
6. [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)
7. [Shader Family Registry](workflows/material-pipeline/shader-family-registry.md)
8. [Edge-Family Matrix](workflows/material-pipeline/edge-family-matrix.md)
9. [P1 Live-Proof Queue](workflows/material-pipeline/p1-live-proof-queue.md)
10. [Live-Proof Packets](workflows/material-pipeline/live-proof-packets/README.md)
11. [Skintone And Overlay Compositor](workflows/material-pipeline/skintone-and-overlay-compositor.md)
12. [Family Sheets](workflows/material-pipeline/family-sheets/README.md)
13. [Source map and trust levels](references/codex-wiki/04-research-and-sources/01-source-map.md)

### I need family-specific CAS/Sim authority details

1. [CAS/Sim Material Authority Matrix](workflows/material-pipeline/cas-sim-material-authority-matrix.md)
2. [Skintone And Overlay Compositor](workflows/material-pipeline/skintone-and-overlay-compositor.md)
3. [Edge-Family Matrix](workflows/material-pipeline/edge-family-matrix.md)
4. [Family Sheets](workflows/material-pipeline/family-sheets/README.md)
5. [Sim body-shell contract](sim-body-shell-contract.md)
6. [Full Sim and morph pipeline](references/codex-wiki/02-pipelines/03-full-sim-and-morphs.md)

### I need low-level binary/reference material

1. [Codex wiki](references/codex-wiki/README.md)
2. [External snapshots](references/external/README.md)

### I need to understand whether something is proven or still a gap

1. [Shared TS4 Material, Texture, And UV Pipeline](shared-ts4-material-texture-pipeline.md)
2. [Open questions](references/codex-wiki/04-research-and-sources/03-open-questions.md)
3. [Current plan](planning/current-plan.md)

## Growth Rules

When a topic becomes too large for its current home:

- keep the normative cross-domain rule in the parent guide
- move dense family or edge-case detail into a deep-dive companion under `docs/workflows/` or another clearly indexed topic folder
- add bidirectional links between the parent guide and the deep-dive doc
- update this map and the nearest section README so narrow tasks can still find the detail quickly
