# Sims4 Resource Explorer

Read-only Windows desktop browser/exporter for The Sims 4 package resources and logical assets.

## Goals

- Browse large Sims 4 game and mod libraries without loading full payloads during list browsing.
- Resolve user-facing logical assets for Build/Buy, CAS, and other 3D roots where feasible.
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

- [Documentation map](docs/README.md)
- [Big plan](docs/planning/big-plan.md)
- [Current plan](docs/planning/current-plan.md)
- [Technical debt](docs/planning/technical-debt.md)
- [Unknowns and non-goals](docs/planning/unknowns-and-non-goals.md)
- [Architecture](docs/architecture.md)
- [Supported types](docs/supported-types.md)
- [Known limitations](docs/known-limitations.md)
- [Sim domain roadmap](docs/sim-domain-roadmap.md)
- [Third-party licenses](docs/third-party-licenses.md)
- [Sample data strategy](docs/sample-data-strategy.md)
- [References](docs/references/README.md)
- [Chat handoff](docs/operations/chat-handoff.md)
- [Agent guide](AGENT.md)
- [Workflow: Build/Buy export](docs/workflows/export-buildbuy-object.md)
- [Workflow: CAS export](docs/workflows/export-cas-asset.md)
- [Workflow: Raw resource export](docs/workflows/export-raw-resource.md)
- [Workflow: Game + Mods](docs/workflows/open-game-and-mods.md)

## Current status

This repository is being built in vertical slices. The current implementation includes:

- package scanning and persistent indexing
- a WinUI 3 desktop shell with an `Update Index` workflow that keeps a stored folder list, infers `Game`/`DLC`/`Mods` from the chosen paths, and lets each run choose workers plus a safe package-read cache memory target
- raw resource browsing backed by SQLite
- logical asset summaries for Build/Buy, CAS, metadata-only `Sim Archetypes`, and generalized 3D roots
- raw classification for Sim/character seeds and Sim assembly component resources so the deeper character domain remains inspectable while assembly coverage is still being built out
- text, hex, and image preview pipelines with graceful fallback
- raw export for every indexed resource
- a first real Build/Buy vertical slice for static model-rooted furniture/decor objects with scene preview and FBX+textures export
- a first real CAS vertical slice for adult/young-adult human parts, including accessories, when the `CASPart` exposes a direct skinned `Geometry` LOD either package-local or via indexed cross-package lookup
- a first metadata-heavy `Sim Archetypes` slice rooted in grouped `SimInfo` rows, with searchable species/age/gender/outfit/trait summaries and detailed per-asset metadata in the selected-asset pane; rows that still cannot be classified into an archetype stay hidden instead of surfacing as singleton fallback entries, and the selected archetype now exposes a body-first inspector: template variations under the archetype, base frame, skintone, body-layer stack, concrete body-source references, body-assembly candidate families, an explicit current body-assembly recipe, a visible body-graph stage list (`Base frame`, `Skin pipeline`, `Geometry shell`, `Head shell`, `Footwear overlay`, `Body morph application`, `Face morph application`), a resolved base-body graph for the currently selected shell/overlay/alternate layers, morph stack, plus human CAS slot-family pickers kept secondary to the body foundations. Those body candidates now prefer exact SimInfo body-part links, fall back to compatible body candidates inferred from the template's body-type tokens when needed, and finally fall back to archetype-compatible body families even when the template exposes no direct body references at all; for human shell families that fallback now widens from exact `species + age + gender` search to a broader `species + age` pass so generic unisex nude/base shells can outrank occult or clothing full-body rows, and if likely nude/base shells are found they are preferred over clothing-like full-body candidates instead of treating a dress as the body. Together they drive a first non-exportable proxy body preview so the archetype can show a real base-body stand-in before full Sim assembly exists, and malformed proxy-body candidates are skipped per-family instead of aborting the whole archetype preview on the first bad geometry payload. Proxy preview now also refuses to render overlay-only layers like `Shoes` when no renderable body shell exists, so the app no longer pretends a footwear fragment is a usable character-body preview. The same slice now materializes an explicit base-body assembly mode plus per-layer active/blocked state, keeps footwear as a separate active layer when a body shell is present, and shows both preview coverage and the actual CAS layers that made it into the current proxy preview, so the current shell/layer policy is visible in both details and the right-hand inspector instead of living only in preview heuristics
- a first `General 3D` slice for standalone package-local `Model`, `ModelLOD`, and `Geometry` roots that are not yet harmonized into Build/Buy or CAS identity graphs
- a first in-app audio path for RIFF/WAV payloads
- lightweight factual asset capability fields in the cache so the app can filter Build/Buy, CAS, and generalized 3D assets without baking support policy into the index
- the first factual raw-metadata slice for the future `Sim` domain: `CASPreset`, `RegionMap`, and `Skintone` now parse into cached human-readable summaries during lazy resource enrichment instead of remaining opaque binary blobs
- `SimInfo` roots now also receive seed-enriched display names and factual descriptions during indexing so the metadata-only `Sim Archetypes` domain is immediately searchable, with top-level rows grouped into species/age/gender archetypes instead of surfacing every `SimInfo` as its own asset; unclassified `SimInfo` rows stay out of the top-level archetype list until parser coverage improves
- `CASPart` roots now also receive seed-enriched slot/compatibility summaries during indexing, so the CAS catalog can be filtered by slot family and the selected `Sim Archetype` can surface first-pass compatible human CAS slot families from the indexed catalog

Current 3D note:

- the current supported subset is narrow and honest: static Build/Buy objects with a `Model` root, triangle-list `ModelLOD` geometry, no skinning/animation path, and package-local material/texture candidates
- the Build/Buy subset now renders in a real in-app 3D viewport when scene reconstruction succeeds, and falls back to explicit diagnostics when reconstruction fails
- CAS support is still narrow but broader than the first slice: adult/young-adult human parts, including accessories, with a direct skinned `Geometry` scene root that can now resolve package-local or indexed cross-package `Geometry`/texture candidates
- the CAS preview path now accepts both bare `GEOM` payloads and the wrapped single-chunk RCOL-style `Geometry` resources that appear in real game packages, so supported CAS assets do not fail just because the mesh payload is containerized
- the CAS/Geometry fallback preview path now skips undecodable same-instance texture candidates per-resource instead of aborting the whole scene build, so one bad texture payload degrades to `Partial` diagnostics rather than collapsing the asset back to `Unsupported`
- image preview now includes an internal `RLE2/RLES -> DXT5 DDS -> PNG` fallback path for Sims 4 texture payloads that the upstream package helper still rejects as `Unknown image format`
- `General 3D` now exposes standalone `Model`, `ModelLOD`, and `Geometry` roots as first-class logical assets so they can be searched, filtered, opened in details, and sent through the existing scene-preview path
- the first real CAS variant layer is now indexed: `CASPart` swatches and preset slots are stored as linked variant rows, `VariantCount` is no longer a stub for indexed CAS assets, and the details pane shows the indexed variant records for the selected asset
- unsupported Build/Buy objects remain metadata/raw-export-first and report explicit diagnostics instead of faking scene success
- unsupported CAS assets remain metadata/raw-export-first and report explicit diagnostics instead of faking scene success
- Build/Buy indexing now also collapses same-package identity rows that resolve to the same downstream scene/model root into one logical asset family, so repeated `set1/set2/set3`-style entries do not flood the main asset list as separate top-level rows
- Build/Buy finalization also backfills `ObjectCatalog`-only delta rows from matching `ObjectDefinition` families that share the same identity instance, using the persisted scene-root hint extracted during resource indexing, so catalog shadow entries do not survive as separate dead-end top-level assets beside the real family row

## Browsing model

Browsing is now mode-first instead of tab-symmetric:

- `Asset Browser` is the task-oriented path for Build/Buy, CAS, metadata-only `Sim Archetypes`, and generalized 3D asset discovery
- `Raw Resource Browser` is the diagnostic path for TGI/package/type inspection

Each mode has its own search box, scoped facets, active filter chips, result summary, and incremental result window. The app shows total matches separately from the currently loaded rows so very large libraries stay understandable. Facets are still partly heuristic where Sims 4 categories/linkage are only partially known. The logical asset catalog is canonicalized during indexing, so `Asset Browser` shows one logical asset row per logical family instead of surfacing separate base/delta shadow variants or catalog-only delta identities as duplicate list entries. The `General 3D` domain intentionally covers standalone scene roots that are not yet understood well enough to be promoted into richer Build/Buy or CAS identity graphs.

For logical assets, the cache now stores cheap factual fields such as scene-root presence, exact-geometry candidate presence, and material/texture reference presence. The app derives labels and filtering behavior from those fields at runtime, so changing previewability rules does not require rebuilding the cache just to change policy.

## Near-term roadmap

The current implementation is moving in this order:

1. make all discoverable 3D roots first-class browseable assets, even before they are fully harmonized into gameplay-aware identity graphs
2. keep expanding the new variant/swatch/state layer beyond the first indexed CAS slice until Build/Buy, CAS, `Sim Archetypes`, and `General 3D` families can all be navigated as structured logical assets
3. harmonize Build/Buy, CAS, `Sim Archetypes`, and `General 3D` detail/preview paths so the app exposes one logical asset with its variants and supporting structure instead of package-layer fragments
4. deepen semantic graph coverage for Build/Buy and CAS before spending more effort on export fidelity
5. move the new `Sim Archetypes` domain from metadata-only grouped `SimInfo` roots into real assembled-character graphs so rig/body/deformation data can feed both direct character browsing and deeper CAS fidelity

## Read-only safety

The app never modifies or repacks `.package` files. Cache, logs, and exports are written only under app-controlled directories.

## Indexing note

Indexing now uses a fast metadata-first full rebuild path with explicit stages. The run first defines package scope by discovering all candidate packages and total input size, then performs the actual metadata-first index build, and finally finalizes the rebuilt catalog. Folder selection now lives inside the `Update Index` dialog instead of the main toolbar: previously chosen folders stay stored between runs, the app infers `Game`/`DLC`/`Mods` from each path, and each run can choose both worker count and a package-read cache memory target. That cache target is expressed as a percentage of RAM but is capped by currently available physical memory so the preloaded package-byte cache does not blindly push the machine into swap pressure. The hot scan loop records TGI/type/preview metadata without per-resource name or size lookups, and the run builds a fresh shadow SQLite catalog from scratch instead of mutating the live catalog in place. Resource names and precise sizes are enriched lazily when a specific resource is opened or exported, then persisted back into SQLite so they do not need to be recomputed repeatedly. CASPart technical names are now extracted from the stable managed-string header instead of depending on a full semantic parse, so modern CAS packages can contribute searchable names even when later CASPart sections use newer layout variants. The same seed-enrichment pass now also persists the first real CAS variant layer into `asset_variants`, writes slot/compatibility summaries back onto `CASPart` rows, and parses `SimInfo` roots into factual display/description metadata so the first metadata-only `Sim Archetypes` domain is searchable immediately after rebuild. Those rows are now grouped into top-level species/age/gender archetypes, while richer `SimPreset` / `SimData` identity layers are still ahead. The selected asset pane now exposes indexed CAS variant rows through an explicit variant picker with thumbnail resolution, and the selected CAS swatch now feeds back into the viewport through a swatch-aware approximate CAS material path that resolves role-specific `diffuse` and `color_shift_mask` textures when the indexed material manifest exposes them. `Sim Archetypes` now also surface a body-first inspector built from `SimInfo`: base frame, skin pipeline, body layers, body morph stack, current body-part references, and body-assembly candidate families are shown before any apparel-oriented CAS slot families. Those body candidates now prioritize exact SimInfo body-part links, add compatible fallback families inferred from body-type tokens when exact links are missing, and feed a first non-exportable proxy body preview assembled from the selected body-family choices; if one chosen body candidate has a malformed GEOM payload, the proxy preview now skips it and keeps trying the remaining candidates for that family instead of collapsing the whole archetype preview. The same body-first slice now materializes an explicit base-body assembly mode and per-layer state (`Active`, `Available`, `Blocked`) so the current shell/layer policy is visible in details and in the `Body` tab instead of living only inside preview heuristics. The `Body` tab now also exposes an explicit `Body Graph` stage list, so it is visible which parts of the current base-body path are already resolved, which are still approximate, and which remain pending before full multi-layer character assembly exists. Human CAS slot-family pickers such as `Hair`, `Top`, `Bottom`, `Shoes`, `Full Body`, and `Accessory` remain available in the separate `CAS` tab, but they are now explicitly secondary to the body foundation needed for future assembled-character preview. During finalization, the asset catalog also computes one canonical logical asset row per `(data_source_id, asset_kind, root_tgi)` across the whole shard set before rebuilding asset FTS and browse indexes.

The CAS runtime graph is now a little more resilient to real Maxis payloads than the indexed summary alone suggests. When a parsed `CASPart` does not expose any LOD envelopes but its TGI table still contains direct `Geometry` references, the graph builder promotes those `Geometry` keys as scene candidates instead of treating the part as permanently metadata-only. That closes a real accessory pattern in EP packs where the direct geometry keys are present but the narrower semantic LOD path still comes back empty.

During indexing, the progress window owns the workflow and the main browse window is intentionally frozen so it does not compete with the indexer. The dialog now presents three stage-specific screens with metrics that match the current phase: scope discovery, index build, and finalization. Worker slots, persist backlog, and writer telemetry are shown only during the actual index-build stage; finalization gets its own step-oriented progress instead of reusing stale write-throughput counters. The serving catalog is now a small fixed shard set rather than a single hot SQLite file: the rebuild creates `index.sqlite` plus sibling shard files under the cache root, routes package writes into a stable shard by package path, and fans browse/search queries back out across the shard set after activation. Shadow-build ingest keeps `resources` and `assets` tables free of hot-path `PRIMARY KEY` maintenance, uses insert-only package rows during the rebuild, and builds unique browse/search indexes plus FTS once at the end. Seed-metadata enrichment is defensive: malformed resource payloads now degrade to per-resource enrichment misses instead of aborting the entire package run, and CASPart semantic parsing now follows a versioned layout model instead of the earlier legacy-only offset assumptions. Successful runs atomically activate the rebuilt shard set; canceled or failed runs leave the previously active catalog untouched. Live CPU traces can be captured with `profile-live-indexing.ps1`, and per-run indexing telemetry is written under `%LOCALAPPDATA%\\Sims4ResourceExplorer\\Telemetry\\Indexing`.
