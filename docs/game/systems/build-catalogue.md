# Build mode — Buy catalogue

*System doc. Status: **B2 built — REAL assets + engine placement (awaiting user verify)** — 2026-07-21.
Owner ticket: `tickets/tasks/TASK-004`.*

Build mode has two halves. **Buy** places movable objects (furniture, appliances, décor); **Build**
draws the building itself (walls, floors, doors, windows — `tickets/tasks/TASK-011`). This doc covers
**Buy**, built first per the user's direction ("do buy first, build — immediate next step").

## Design (agreed 2026-07-21)

A **bottom-docked glass dock** (matching the inZOI-style HUD), full width, 3D lot visible above it.

```
┌─ build-dock (glass, bottom) ─────────────────────────────────────────────┐
│ [Buy] [Build*]  │  🔍 search   Living Kitchen Bedroom … (room chips)   Close│  header
│ ┌ rail ┐  ┌──────────── thumbnail grid (virtualized, 5-wide) ──────────┐  │
│ │Seating│ │  ▢ Sofa   ▢ Bench  ▢ Chair  ▢ Stool  ▢ Ottoman             │  │  body
│ │Surfaces│ │  §450    §180     §90      §70      §90                    │  │
│ │Beds…  │ └─────────────────────────────────────────────────────────────┘  │
│ COLOUR ◉◉◉◉◉           <status hint>            Rotate (R)   Delete (Del)  │  footer
└───────────────────────────────────────────────────────────────────────────┘
```

- **Categories are function-primary** (Seating, Surfaces, Beds, Appliances, Plumbing, Lighting,
  Electronics, Storage, Decor) — the left rail. **Room is a cross-cutting filter** (chips: the export's
  Living/Kitchen/Bathroom/Bedroom tags); click the active chip again to clear. **Search** narrows by name.
- **Swatches are the game's real baked recolours** (`Swatches/N/diffuse.png`): the strip shows the actual
  diffuse textures as chips; recolour swaps that texture on the placed object's primary materials.

## Data: REAL exported assets (no placeholders)

The catalogue is **baked at scene-build time** from the game's own Build/Buy exports
(`Assets/Sims4/home/<folder>/` + `home_catalog.json`) by `Editor/GameCatalogBaker.cs`, reusing the
home-editor's proven template builder (`HomeEditorSceneBuilder.BuildTemplate` — real OBJ mesh, HDRP
materials rebuilt from the exported textures, BoxCollider, ground-level pivot):

- **Template** — one inactive GameObject per item under `Game Catalog/Catalog Templates`; placement
  clones it (`SmartObjectFactory`). Editor-only construction, runtime-safe result: everything is
  serialized scene references (ships in a player build, zero runtime loading).
- **Thumbnail** — the game's own BuyBuildThumbnail (`thumb.jpg`), 22/37 items have one; fallback = the
  item's primary diffuse (TASK-012 exports the missing 15).
- **Swatches** — per-item `swatches.json` + baked diffuse per swatch; `primaryTexture` (first
  `_BaseColorMap` in the template) is the texture swatches replace — the home editor's ApplySwatch
  contract, ported to `SmartObject.Recolor`.
- **Function layer** — the baker maps catalog id → function category, price (hand-authored — real
  §prices aren't exported yet), and the sim-side `InteractionAdvertisement`s (bed→Sleep, fridge→Eat,
  toilet/shower→bladder/hygiene, tv/sofa→fun/social…), so player-built lots keep driving the utility AI.

## Placement engine (grid-first, D-106)

- **1 m tiles are the authority** — footprints in whole tiles from the authored bounds (same rule as the
  home editor, so future walls agree); placement snaps footprint edges onto tile lines; a **visible 1 m
  grid overlay** appears while build mode is open.
- **Validity = occupancy** — `LotGrid` keeps a per-tile owner map; placement/move/rotate are legal only
  when `TilesFree` says so (physics is consulted only for Sims, whose positions are continuous).
- **Ghost preview** — picking an item spawns a live clone of the real template (colliders disabled, the
  chosen swatch pre-applied), snapped to tiles under the cursor, above a **green/red footprint quad**.
  Invalid placement is refused, not silently allowed.
- **R rotates** 0/90/180/270 (footprint swaps on odd rotations, validated + reverted if blocked).
- **Move** — drag a selected object (tile-snapped); dropping it blocked reverts to where it came from.
- Every place/move/rotate/delete **re-registers occupancy and re-marks nav from those same tiles**, so
  Sims immediately route around edits — and use anchors are **approach slots**: the tile centred in
  front of the item, where the Sim stands occupying that full cell.

## Input contract (build mode) — camera stays LIVE

| Input | Action |
| --- | --- |
| LMB click | select object under cursor (ALWAYS wins) → else place armed item (stays armed) → else deselect |
| LMB drag on selection | move (snapped, revert-if-blocked) |
| RMB click (≤5 px) | cancel placing |
| RMB drag (>5 px) | **camera orbit** (placement stays armed) |
| MMB drag / WASD / arrows | **camera pan** · Q/E orbit · wheel **zoom** (world only, not over UI) |
| R / Del | rotate ghost-or-selection / delete selection |
| Esc | blur search → cancel placing → deselect → exit build |

Rules that make this safe (all in `UiPointer` + `GameCamera`):
- **Over-UI** = flip Y → `ScreenToPanel` → `panel.Pick` per registered UIDocument (the twice-wrong bug:
  ScreenToPanel does NOT flip Y — see memory `ui-toolkit-over-ui-hittest`).
- Drag ownership is **latched at button-down** (a drag starting in the world stays camera-owned across
  the dock; one starting on the dock never moves the camera).
- RMB click-vs-drag: 5 px threshold; `GameCamera.RightDragOrbiting` stays readable through the release
  frame so build-mode's cancel never fires after an orbit.
- **All keys are ignored while the search field has focus** (`UiPointer.TextInputFocused`).

## Implementation map

| Concern | File |
| --- | --- |
| Catalogue data (serialized real defs) | `Runtime/Game/SmartObjectCatalog.cs` — `BuildableDef`, `Swatch`, `GameCatalog` |
| Scene-build bake from exports | `Editor/GameCatalogBaker.cs` (+ `HomeEditorSceneBuilder.BuildTemplate`, made internal) |
| Virtualized thumbnail grid | `Runtime/Game/CatalogGrid.cs` (model-based selection — no rebind mid-dispatch) |
| Dock behaviour + placement engine | `Runtime/Game/GameBuildMode.cs` |
| Spawn placed object from template | `Runtime/Game/SmartObjectFactory.cs` |
| Recolour (texture-reference swap) | `Runtime/Game/SmartObject.cs` — `Recolor(int)` |
| Over-UI + text-focus service | `Runtime/Game/UiPointer.cs` |
| Camera (live in build) | `Runtime/Game/GameCamera.cs` |
| Layout / styling | `Game/UI/BuildDock.uxml`, `Game/UI/GameTheme.uss` |
| Scene wiring | `Editor/GameSceneBuilder.cs` (bake → `buildMode.catalog`, starter lot from real items) |

## Gaps / next

- 15 items lack a real thumbnail (`TASK-012` — `exportthumbs`); fallback shows the diffuse.
- Real § prices + friendly swatch names aren't in the export (`TASK-013`); prices are hand-authored.
- `PlaceOn` surfaces (wall/tabletop/ceiling placement), § budget deduction, sort control, rail category
  icons (TASK-005) — logged on TASK-004.
- **Next: BUILD structure tools** (`TASK-011`) — walls/rooms/floors/doors/windows on the home-editor
  engine, as the user directed ("build — immediate next step, don't forget it").
