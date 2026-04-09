# Architecture

## Product shape

Sims4 Resource Explorer is a read-only Windows desktop application for browsing The Sims 4 package resources at two levels:

- logical asset browsing for Build/Buy and CAS assets
- raw TGI/resource browsing for every discovered package resource

The architecture favors thin UI layers, lazy resource loading, persistent indexing, and graceful degradation when semantic parsing or preview/export support is incomplete.

## Design principles

- Read-only by default. Package access code never mutates source files.
- Index first, decode on demand. Browsing/searching runs against SQLite, not against package payloads.
- Canonical models between parsing and rendering/export. Scene export is not coupled to any one parser or UI toolkit.
- Graceful fallback. Unsupported content still exposes metadata, diagnostics, and raw export.
- Vertical slices. Each feature lands with its storage, service, UI, tests, and documentation touch points.

## Project boundaries

### `Sims4ResourceExplorer.Core`

Shared primitives, domain models, interfaces, diagnostics, and application settings.

### `Sims4ResourceExplorer.Packages`

Thin adapter layer over `LlamaLogic.Packages` and package-resource metadata extraction. This project is responsible for:

- enumerating package entries
- reading resource payload streams on demand
- exposing TGI-based metadata records
- shielding the rest of the app from third-party parser details

### `Sims4ResourceExplorer.Indexing`

SQLite-backed persistent index/cache and background indexing orchestration. This project is responsible for:

- package fingerprinting
- incremental rescans
- storing resource rows and logical asset summaries
- search/filter queries

### `Sims4ResourceExplorer.Assets`

Logical asset graph construction and dependency resolution for Build/Buy and CAS assets. This layer produces:

- asset summaries for the browser
- linked resource graphs
- diagnostics explaining unresolved links

### `Sims4ResourceExplorer.Preview`

Preview dispatch for images, text, binary, and scene previews. This layer builds preview models and diagnostics, not UI controls.

### `Sims4ResourceExplorer.Export`

Raw export and asset-centric export pipelines, including scene export manifests and metadata sidecars.

### `Sims4ResourceExplorer.Audio`

Audio detection, decode abstractions, playback adapters, and WAV export support.

### `Sims4ResourceExplorer.App`

WinUI 3 shell, MVVM presentation layer, dependency injection composition, background task coordination, and platform-specific services.

The browse shell is mode-first:

- a first-class `BrowserMode` switches between `Asset Browser` and `Raw Resource Browser`
- each mode owns separate query/view state instead of sharing one generic filter bag
- the left rail defines source scope and mode-specific facets before large result sets are shown

### `Sims4ResourceExplorer.Tests`

Unit and integration tests using a small documented fixture corpus.

## Primary workflows

### 1. Index packages

1. User selects one or more root folders for Game, DLC, and optional Mods.
2. `IPackageScanner` streams candidate `.package` files as they are discovered instead of materializing the full list first.
3. `IIndexStore` bulk-loads stored package fingerprints once, and discovery compares file path, file size, and last write time in memory to skip unchanged packages without one-query-per-package overhead.
4. Changed packages are queued immediately into a bounded package-worker pipeline, so scanning can start before discovery completes.
5. Package scans stay single-threaded within each package in the current build, but multiple packages can scan concurrently.
6. The hot scan path records cheap browse metadata only: TGI/type, preview/export flags, and linkage hints. Resource names and precise sizes are deferred until a specific resource is opened or exported.
7. A single SQLite writer session keeps one connection open for the whole indexing run and persists package results inside per-package transactions.
8. UI consumes throttled progress snapshots with stable worker-slot detail, worker/backlog counts, heartbeat diagnostics, and an end-of-run summary while remaining responsive.

### 2. Browse raw resources

1. User chooses source scope plus a raw-resource domain such as Images, Audio, Text/XML, 3D-related, or Other/Unknown.
2. Browser queries SQLite for counted, sorted, windowed resource rows.
2. Selecting a row loads metadata immediately from the index.
3. Preview services request the resource payload only when the details pane needs it.
4. Unsupported previews fall back to metadata + hex/raw export.

### 3. Browse logical assets

1. User chooses source scope plus the asset domain (`Build/Buy` or `CAS`).
2. Browser queries SQLite for counted, sorted, windowed asset summaries.
2. Asset graph builder resolves linked TGIs lazily for the selected asset.
3. Preview and export services consume the resolved graph and canonical scene/material models.

Current Build/Buy vertical slice:

- one supported subset is implemented end-to-end: static model-rooted Build/Buy furniture/decor objects
- the scene path resolves object identity, chosen `ModelLOD`, material candidates, and texture candidates closely enough for viewport preview and FBX+PNG bundle export
- unsupported Build/Buy assets stay explicit and diagnostic rather than silently degrading to fake scene success

Current CAS vertical slice:

- one supported subset is implemented end-to-end: adult/young-adult human hair, full body, top, bottom, and shoes parts when the `CASPart` exposes a direct package-local skinned `Geometry` LOD
- the asset graph resolves the `CASPart` root, chosen LOD, package-local `Geometry`, optional exact-instance `Rig`, and package-local texture candidates closely enough for viewport preview and FBX+PNG bundle export
- unsupported CAS assets stay explicit and diagnostic rather than silently degrading to fake scene success

## Browsing query architecture

- `AssetBrowserQuery` and `RawResourceBrowserQuery` are separate contracts.
- Query results return total match count plus a stable visible window through `WindowedQueryResult<T>`.
- Hidden browser modes are marked dirty but are not requeried until they become active.
- Active filter chips and result-summary text are derived from the mode-specific query state.
- Some facets remain heuristic because Sims 4 semantic categories, linkage, and support status are still incomplete in the current index.

### 4. Export 3D asset bundles

1. Build asset graph for the selected logical asset or raw scene-capable resource.
2. Build canonical scene/material/texture bundle.
3. Export FBX and decoded texture PNGs.
4. Write `manifest.json` and `metadata.json`.

## Storage model

SQLite stores the browse-time index. Suggested main tables:

- `data_sources`
- `packages`
- `resources`
- `resource_links`
- `logical_assets`
- `asset_resources`
- `thumbnails`
- `index_runs`
- `diagnostics`

Payload blobs are not persisted in SQLite in v1 except for optional small thumbnails/cache artifacts.

## Async and responsiveness model

- All indexing and preview operations run off the UI thread.
- UI uses cancellation-aware commands for indexing, preview generation, and export.
- Browsing uses paged database queries and lazy thumbnails.
- Long-running export/index tasks report structured progress and warnings.
- Indexing progress is intentionally throttled before it reaches the UI so status visibility does not become the next bottleneck.
- The browser surface is decoupled from live indexing progress updates; indexing uses a dedicated modal dialog and the main browse lists refresh only after the run completes or when the user explicitly refreshes.
- Deferred metadata means browse rows may initially show blank/deferred names and sizes until a resource is explicitly inspected.

## Biggest technical risks

- Sims 4 format coverage is uneven; some logical asset links may require iterative reverse engineering.
- WinUI 3 3D integration with HelixToolkit.WinUI.SharpDX needs careful packaging/version validation.
- Audio decode support may be partial in v1 because native-path licensing and format specifics need validation.
- FBX export fidelity depends on how fully material semantics can be mapped from Sims 4 resources.

## Deferred from v1

- automatic install-path detection
- source package mutation or repacking
- full Sim assembly from CAS parts
- animation/CLIP export
- guaranteed morph/blendshape support
- trimmed or NativeAOT publishing
