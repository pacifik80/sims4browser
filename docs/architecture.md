# Architecture

## Product shape

Sims4 Resource Explorer is a read-only Windows desktop application for browsing The Sims 4 package resources at two levels:

- logical asset browsing for Build/Buy, CAS, and generalized 3D assets
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
- providing focused fallback decode paths when upstream helpers reject still-supported Sims 4 image subformats such as `DXT5RLE2`
- shielding the rest of the app from third-party parser details

### `Sims4ResourceExplorer.Indexing`

SQLite-backed persistent index/cache and background indexing orchestration. This project is responsible for:

- full catalog rebuilds into a shadow shard set of SQLite databases
- storing resource rows and logical asset summaries
- fan-out search/filter queries across the active shard set

### `Sims4ResourceExplorer.Assets`

Logical asset graph construction and dependency resolution for Build/Buy, CAS, and generalized 3D assets. This layer produces:

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
2. `IPackageScanner` first performs a scope-discovery pass that counts candidate `.package` files and total input size before any package scan workers start.
3. `IIndexStore` opens a fresh shadow SQLite database set for the run and seeds it with the configured data sources before any package rows are written. The previously active serving catalog stays untouched until the rebuild succeeds.
4. After scope is locked, discovered packages are queued into the bounded package-worker pipeline for the actual index-build stage.
5. Package scans stay metadata-first inside each package; very large packages may parallelize cheap entry enumeration and seed-enrichment work, while multiple packages still scan concurrently.
6. The hot scan path records cheap browse metadata only: TGI/type, preview/export flags, and linkage hints. Resource names and precise sizes are deferred until a specific resource is opened or exported.
7. The rebuild opens a small fixed shard set of SQLite writer sessions, routes each package to a stable shard by package path, and writes package/resource/asset rows into shard-local shadow catalogs without mutating the active serving catalog.
8. Shadow-build ingest keeps `resources` and `assets` free of hot-path `PRIMARY KEY` maintenance. Rows are inserted into plain tables first, then unique browse indexes and FTS are built once during a dedicated `finalizing` phase after package writes complete. Finalization also canonicalizes logical assets across the whole shard set so the serving asset catalog exposes one row per logical family instead of separate base/delta shadow duplicates. For Build/Buy, that finalization step now also backfills `logical_root_tgi` onto `ObjectCatalog`-only delta rows by matching them to persisted `ObjectDefinition` scene-root hints with the same identity instance, so delta catalog shadows do not survive as separate top-level assets even when the corresponding `ObjectDefinition` family was already collapsed before SQLite write. Seed metadata parsing is defensive: malformed CASPart/Object metadata should fail closed for the single resource and not fail the whole package. CASPart technical names are extracted from the stable managed-string header, and the fuller CASPart semantic parser now follows a versioned layout model instead of relying on legacy v27/v28-era offsets. The same seed pass now also extracts the first real CAS variant layer: swatch rows and preset slots are written into `asset_variants` and linked back to logical assets through the index so `VariantCount` and details are driven by indexed facts instead of hard-coded placeholders.
9. Query-time browsing/searching fans out across the active shard set and merges sorted windows, counts, and facet values back into one logical catalog surface for the app.
10. When finalization succeeds, the rebuilt shadow shard set is atomically activated as the new serving catalog (`index.sqlite` plus sibling shard files). Canceled or failed runs discard the shadow shard set and keep the previously active catalog unchanged.
11. While indexing is active, the indexing dialog owns the workflow and the main browse window is intentionally frozen so query/filter/preview activity does not compete with the indexer. The dialog exposes three explicit UI stages: scope discovery, index build, and finalization. Worker/backlog/writer telemetry is shown only during index build; finalization has its own step-oriented progress view.

### 2. Browse raw resources

1. User chooses source scope plus a raw-resource domain such as Images, Audio, Text/XML, 3D-related, or Other/Unknown.
2. Browser fans the query out across the active catalog shards, then merges counted, sorted, windowed resource rows back into one logical result set.
3. Selecting a row loads metadata immediately from the index.
4. Preview services request the resource payload only when the details pane needs it.
5. Unsupported previews fall back to metadata + hex/raw export.

### 3. Browse logical assets

1. User chooses source scope plus the asset domain (`Build/Buy`, `CAS`, or `General 3D`).
2. Browser fans the query out across the active catalog shards, then merges counted, sorted, windowed canonical asset summaries back into one logical result set.
3. Asset graph builder resolves linked TGIs lazily for the selected asset.
4. Preview and export services consume the resolved graph and canonical scene/material models.

Current Build/Buy vertical slice:

- one supported subset is implemented end-to-end: static model-rooted Build/Buy furniture/decor objects
- the scene path resolves object identity, chosen `ModelLOD`, material candidates, and texture candidates closely enough for viewport preview and FBX+PNG bundle export
- unsupported Build/Buy assets stay explicit and diagnostic rather than silently degrading to fake scene success

Current CAS vertical slice:

- one supported subset is implemented end-to-end: adult/young-adult human parts, including accessories, when the `CASPart` exposes a direct skinned `Geometry` LOD either package-local or through indexed cross-package lookup
- the asset graph resolves the `CASPart` root, chosen LOD, direct `Geometry`, optional same-instance `Rig`, and texture candidates closely enough for viewport preview and FBX+PNG bundle export; once a `Geometry` root is resolved, the graph also pulls same-instance companions from that geometry package so cross-package CAS assets do not stay metadata-only just because the `CASPart` root lives elsewhere. If the semantic parser does not surface any LOD envelopes but the `CASPart` TGI table itself still carries direct `Geometry` keys, the graph promotes those keys as a fallback scene root path instead of dropping the asset back to metadata-only. The preview parser accepts both bare `GEOM` payloads and the wrapped single-chunk container form used by real game packages. During same-instance fallback texture resolution, unsupported image payloads are now skipped one candidate at a time and reported as diagnostics instead of aborting the whole scene build
- unsupported CAS assets stay explicit and diagnostic rather than silently degrading to fake scene success

Current Build/Buy family harmonization:

- identity rows are still discovered from `ObjectDefinition` / `ObjectCatalog` roots, but the index now carries a scene-root family hint extracted from `ObjectDefinition` references
- when multiple same-package Build/Buy identity rows resolve to the same downstream `Model` root, indexing collapses them into one logical asset row instead of surfacing each `set1/set2/set3` identity as a separate top-level result
- cross-package base/delta canonicalization now uses that logical root when available, so scene-identical Build/Buy families do not reappear as duplicate canonical rows just because they came from different identity roots
- `ObjectCatalog`-only delta identities now inherit the family logical root from persisted `ObjectDefinition` scene-root hints with the same identity instance during finalization, so catalog shadow rows no longer appear as separate unusable assets beside the real Build/Buy family even when sibling `ObjectDefinition` identities were already collapsed into one grouped asset row

Current General 3D slice:

- standalone package-local `Model`, `ModelLOD`, and `Geometry` roots that are not already claimed by Build/Buy or CAS identity graphs are indexed as first-class logical assets
- the generalized graph stays intentionally package-local and structural: root, model/model LOD candidates, geometry candidates, rig candidates, material candidates, and texture candidates
- preview reuses the existing scene path for `Model`, `ModelLOD`, and `Geometry` roots so these assets are inspectable even before they are fully harmonized into richer gameplay-aware graphs

Current variant-family slice:

- CAS now has a first real indexed variant layer built from `CASPart` payloads
- the index stores swatch rows and preset slots in `asset_variants`
- asset summaries derive `VariantCount` from those indexed rows instead of using a fixed placeholder
- the selected-asset pane reads indexed variant rows back out through `IIndexStore`
- the preview/details surface now exposes an explicit variant picker plus best-effort thumbnail resolution for indexed swatch/preset rows
- the currently selected CAS swatch now also feeds into the viewport by resolving indexed `diffuse` and `color_shift_mask` texture roles for the portable approximate CAS material path, with tint-only fallback when those role-specific textures are unavailable

## Browsing query architecture

- `AssetBrowserQuery` and `RawResourceBrowserQuery` are separate contracts.
- Query results return total match count plus a stable visible window through `WindowedQueryResult<T>`.
- Hidden browser modes are marked dirty but are not requeried until they become active.
- Active filter chips and result-summary text are derived from the mode-specific query state.
- Some facets remain heuristic because Sims 4 semantic categories, linkage, support status, and variant relationships are still incomplete in the current index.

### 4. Export 3D asset bundles

1. Build asset graph for the selected logical asset.
2. Build canonical scene/material/texture bundle.
3. Export FBX and decoded texture PNGs.
4. Write `manifest.json`, `metadata.json`, and `material_manifest.json` when material diagnostics are available.

## Storage model

SQLite stores the browse-time index. The active serving catalog is one logical catalog backed by a fixed shard set (`index.sqlite` plus sibling shard files under the cache root). Each shard carries the same schema. Current main tables per shard:

- `data_sources`
- `packages`
- `resources`
- `assets`
- `asset_variants`
- `resources_fts`
- `assets_fts`

Deferred enrichment fields such as resource names, sizes, scan tokens, cheap capability flags, and the asset `is_canonical` flag live on the `resources`/`assets` rows rather than in separate side tables. Payload blobs are not persisted in SQLite; preview/export caches stay in app-controlled filesystem and in-memory caches. Full indexing runs build a temporary shadow shard set under the cache root, ingest into `resources`/`assets` without hot-path `PRIMARY KEY` maintenance, canonicalize logical assets across shards during finalization, build unique browse indexes and FTS over that canonicalized asset set, and only then activate the rebuilt shard set as the new live catalog.

## Async and responsiveness model

- All indexing and preview operations run off the UI thread.
- UI uses cancellation-aware commands for indexing, preview generation, and export.
- Browsing uses paged database queries and on-demand preview loading.
- Long-running export/index tasks report structured progress and warnings.
- Indexing progress is intentionally throttled before it reaches the UI so status visibility does not become the next bottleneck.
- The browser surface is decoupled from live indexing progress updates; indexing uses a dedicated modal dialog and the main browse lists refresh only after the run completes or when the user explicitly refreshes.
- Deferred metadata means browse rows may initially show blank/deferred names and sizes until a resource is explicitly inspected or exported.
- Indexing also emits per-run telemetry under `%LOCALAPPDATA%\\Sims4ResourceExplorer\\Telemetry\\Indexing`, and `profile-live-indexing.ps1` can attach `dotnet-trace` to a live indexing run for CPU sampling.

## Biggest technical risks

- Sims 4 format coverage is uneven; some logical asset links may require iterative reverse engineering.
- generalized 3D roots are intentionally broader than the current semantically harmonized asset model, so future work still needs to group variants/swatches/stateful families under one logical asset surface
- WinUI 3 3D integration with HelixToolkit.WinUI.SharpDX needs careful packaging/version validation.
- Audio decode support may be partial in v1 because native-path licensing and format specifics need validation.
- FBX export fidelity depends on how fully material semantics can be mapped from Sims 4 resources.

## Deferred from v1

- automatic install-path detection
- source package mutation or repacking
- full Sim assembly from CAS parts
- full variant/swatch/state-family harmonization across Build/Buy, CAS, and generalized 3D roots
- animation/CLIP export
- guaranteed morph/blendshape support
- trimmed or NativeAOT publishing
