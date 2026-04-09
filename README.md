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
- a canonical scene export pipeline plus FBX bundle writer for synthetic/test scenes
- a first in-app audio path for RIFF/WAV payloads

Current 3D note:

- the UI and export architecture for scene reconstruction are in place
- actual Sims 4 geometry/model/rig reconstruction is not implemented yet, so 3D preview currently degrades to diagnostics rather than a reconstructed viewport scene

## Read-only safety

The app never modifies or repacks `.package` files. Cache, logs, and exports are written only under app-controlled directories.

## Indexing note

Indexing now runs through a bounded background worker pipeline with a single batched SQLite writer, throttled progress snapshots, per-package phase/counter reporting, heartbeat diagnostics, and an end-of-run summary. The app now opens a dedicated indexing dialog with stable per-worker rows, a bounded recent-activity log, and a user-selectable worker count that is remembered between runs. Package internals remain single-threaded in this pass because parser/thread-safety assumptions for intra-package parallel reads were not expanded here.
