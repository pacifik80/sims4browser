# Risk Log

## High risk

### Logical asset reconstruction gaps

- Impact: incomplete Build/Buy or CAS asset resolution, missing thumbnails or linked textures.
- Why risky: Sims 4 logical link chains are reverse-engineered and unevenly documented.
- Mitigation: keep raw browser first-class, store unresolved link diagnostics, design asset graph builders as composable resolvers, and ship support matrices by asset type.

### 3D export fidelity

- Impact: exported FBX may lose material semantics, rig detail, or scene completeness.
- Why risky: mapping Sims 4 material/shader semantics to generic FBX pipelines is lossy.
- Mitigation: use a canonical scene representation, export nearest practical texture set, and document omissions in `manifest.json`.

### Audio decoding coverage

- Impact: some audio resources may preview/export only as raw bytes.
- Why risky: codec/container details may need additional reverse engineering or native decoding support.
- Mitigation: separate detection from decode, preserve raw export, and clearly surface unsupported reasons.

## Medium risk

### WinUI 3 table virtualization behavior

- Impact: sluggish browsing on very large datasets.
- Mitigation: page queries, keep rows lightweight, lazy-load thumbnails, and keep a fallback custom table shell if `WinUI.TableView` becomes blocking.

### HelixToolkit WinUI integration

- Impact: preview instability, packaging issues, or device-dependent rendering problems.
- Mitigation: keep preview abstraction UI-agnostic, provide mesh-stats-only fallback, and gate export from canonical scene models rather than the viewport layer.

### Fixture scarcity

- Impact: regressions in exported assets or preview decoding go unnoticed.
- Mitigation: define a minimal but representative fixture corpus and document what each sample validates.

## Low risk

### SQLite scale for metadata-only browsing

- Impact: browse queries slow down over time.
- Mitigation: index search fields, separate package/resource tables, and avoid storing large payloads.
