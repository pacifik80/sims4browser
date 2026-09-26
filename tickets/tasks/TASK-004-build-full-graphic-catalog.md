---
id: TASK-004
title: Build mode — Buy catalogue (bottom glass dock, virtualised thumbnail grid, swatches)
status: in-progress
area: game-ui
priority: high
created: 2026-07-21
---
**Design agreed 2026-07-21** — bottom-docked glass catalogue; **function-primary categories + room
filter**; virtualised thumbnail grid; per-item colour **swatches**; place/rotate/recolour/delete. Build
BUY (objects) first, then the BUILD structure tools (TASK-011). Start with **one exemplar per category**
(placeholder visuals), then swap in real assets (needs the object export pipeline). Full write-up in
`docs/game/systems/build-catalogue.md`.

**B2 rebuilt with REAL assets 2026-07-21 (awaiting user verify)** — after user rejected B1 ("terrible":
click-through still placed furniture, camera dead in build, placeholder cubes, glitchy selection).
Root causes found by verification, not guesswork: (1) `ScreenToPanel` needs the caller to FLIP Y first
(official docs) — both prior "fixes" tested a mirrored point, so the dock never blocked clicks;
(2) `GameCamera` hard-yielded to `InBuild`; (3) real furniture existed all along in `Assets/Sims4/home/`
(31 items, real thumbs, baked swatch diffuses). Rebuild: `UiPointer` (verified flip-Y→ScreenToPanel→
panel.Pick + TextInputFocused), `GameCatalogBaker` bakes real templates/thumbs/swatches into the scene
(reuses `HomeEditorSceneBuilder.BuildTemplate`), ghost preview + 0.5m snap + green/red footprint +
overlap validity, camera LIVE in build (RMB 5px click-vs-drag arbitration, latched drag ownership, keys
off while typing), model-based grid selection (no mid-dispatch rebind). Doc updated:
`docs/game/systems/build-catalogue.md`. **Still open:** § budget deduction; sort control; wall/surface/
ceiling placement; rail category icons (TASK-005); real thumbs for 15 items (TASK-012); real prices +
friendly swatch names (TASK-013). User must re-run **Sims4 Creator/Game/Build M0 Scene**.

**Goal** — Build mode should be a proper, always-available **graphic furniture catalog** — category
tabs + a browsable, virtualized thumbnail list — not the current 6-item placeholder palette. The user
was explicit: "current build is a stub … implement furniture/build options as a standard catalogue,
always visible as a full graphic catalog. It is the proper way to work with it."

**Acceptance** — browse furniture by category with real visual thumbnails, scroll/filter a long
(virtualized) list, and place any item; the six placeholder cubes are replaced by real catalog entries
with previews. Reads like a real build/buy browser.

**Notes** — reuse `CatalogList` (already virtualized). Needs: (a) furniture thumbnails — render small
previews of each object; (b) real furniture assets from the existing Build/Buy export pipeline instead
of coloured boxes. Related: TASK-005 (icons), the current `GameBuildMode`/`SmartObjectCatalog` are the
placeholder to replace. Deferred until after the core UI migration, but this is the intended shape.
