# Sims4 Resource Explorer

Read-only Windows desktop browser/exporter for The Sims 4 package resources and logical assets.

## Goals

- Browse large Sims 4 game and mod libraries without loading full payloads during list browsing.
- Resolve user-facing logical assets for Build/Buy and CAS where feasible.
- Preview supported textures, text/binary data, 3D content, and supported audio resources.
- Export supported 3D assets to FBX with textures plus manifest/metadata sidecars.
- Keep the source game and mod package files strictly read-only.

## Stack

- Windows only
- WinUI 3
- C# / .NET 8
- x64 only
- SQLite persistent index/cache

## Solution layout

- `src/Sims4ResourceExplorer.App`
- `src/Sims4ResourceExplorer.Core`
- `src/Sims4ResourceExplorer.Indexing`
- `src/Sims4ResourceExplorer.Packages`
- `src/Sims4ResourceExplorer.Assets`
- `src/Sims4ResourceExplorer.Preview`
- `src/Sims4ResourceExplorer.Export`
- `src/Sims4ResourceExplorer.Audio`
- `tests/Sims4ResourceExplorer.Tests`

## Documentation

- [Architecture](docs/architecture.md)
- [Supported types](docs/supported-types.md)
- [Known limitations](docs/known-limitations.md)
- [Third-party licenses](docs/third-party-licenses.md)
- [Sample data strategy](docs/sample-data-strategy.md)
- [Workflow: Build/Buy export](docs/workflows/export-buildbuy-object.md)
- [Workflow: CAS export](docs/workflows/export-cas-asset.md)
- [Workflow: Raw resource export](docs/workflows/export-raw-resource.md)
- [Workflow: Game + Mods](docs/workflows/open-game-and-mods.md)

## Current status

This repository is being built in vertical slices. The current implementation includes:

- package scanning and persistent indexing
- a WinUI 3 desktop shell for adding Game/DLC/Mods folders manually
- raw resource browsing backed by SQLite
- heuristic logical asset summaries for Build/Buy and CAS roots
- text, hex, and image preview pipelines with graceful fallback
- raw export for every indexed resource
- a first real Build/Buy vertical slice for static model-rooted furniture/decor objects with scene preview and FBX+textures export
- a first in-app audio path for RIFF/WAV payloads

Current 3D note:

- the current supported subset is narrow and honest: static Build/Buy objects with a `Model` root, triangle-list `ModelLOD` geometry, no skinning/animation path, and package-local material/texture candidates
- unsupported Build/Buy objects remain metadata/raw-export-first and report explicit diagnostics instead of faking scene success

## Browsing model

Browsing is now mode-first instead of tab-symmetric:

- `Asset Browser` is the task-oriented path for Build/Buy and CAS asset discovery
- `Raw Resource Browser` is the diagnostic path for TGI/package/type inspection

Each mode has its own search box, scoped facets, active filter chips, result summary, and incremental result window. The app shows total matches separately from the currently loaded rows so very large libraries stay understandable. Facets are still partly heuristic where Sims 4 categories/linkage are only partially known.

## Read-only safety

The app never modifies or repacks `.package` files. Cache, logs, and exports are written only under app-controlled directories.

## Indexing note

Indexing now uses a fast metadata-first path: package discovery streams into the worker queue, unchanged packages are skipped from one bulk fingerprint preload, and the hot scan loop records TGI/type/preview metadata without per-resource name or size lookups. Resource names and precise sizes are enriched lazily when a specific resource is opened or exported, then persisted back into SQLite so they do not need to be recomputed repeatedly.
