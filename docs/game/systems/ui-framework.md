# System: Game UI (UI Toolkit)

Status: **shell built, awaiting Play-test.** The game's UI is built on **UI Toolkit** per decision [D-105](../decisions.md). This replaces the IMGUI debug panels with real, themeable UI that can grow into the finished game menus.

## Why UI Toolkit (short version)

Our hardest UI requirement is **long, filterable, thumbnail-heavy catalogs** — CAS hair/clothing (hundreds of entries) and the furniture browser. UI Toolkit's `ListView`/`TreeView` **virtualize** (only visible items are instantiated), which is the make-or-break feature; uGUI needs a hand-rolled or bought recycling scroll view. USS stylesheets also make the whole UI re-skinnable from one file, and the "UI reflects state" model matches our plain-data souls/catalogs. Full rationale + alternatives in [D-105](../decisions.md).

## Structure

| File | Role |
|---|---|
| `Assets/Game/UI/GameTheme.uss` | **All visual style + design tokens** (colors, spacing, radius) — re-skin the game from here without touching layout or code. |
| `Assets/Game/UI/GameHud.uxml` | The shell layout: top bar (clock, speed, mode buttons) + left Sim panel. New panels are added as siblings of `root`. |
| `Assets/Game/UI/GamePanelSettings.asset` | `PanelSettings` (created by the scene builder; needs a `ThemeStyleSheet`). |
| `Runtime/Game/GameHudUI.cs` | Binds the document to the simulation: clock/speed, mode entry, Sim cards + need meters, status line. |

The scene builder creates the `Game UI` object (UIDocument + `GameHudUI`) and an **EventSystem** (runtime UI Toolkit needs one; legacy input module, since the project uses the old Input backend).

## Conventions (follow these for new UI)

1. **Structure in UXML, style in USS, behaviour in C#.** No inline styling in UXML, no hard-coded colors in C# (the need-meter colour is the one deliberate exception — it's data-driven).
2. **Style via classes + tokens.** Add a class in `GameTheme.uss`; don't set `style.*` from C# except for genuinely dynamic values (meter widths).
3. **State flows one way**: simulation → UI. The UI reads the sim each frame and reflects it; it never stores game state.
4. **Long lists use `ListView` with `FixedHeight` virtualization.** DynamicHeight calls `bindItem` for *every* item (4000 items → 4000 calls vs ~35) — a real performance trap for our catalogs.
5. **Panels self-hide** rather than being toggled from outside where practical (`GameHudUI` hides while CAS/Build modes are open).
6. **World clicks must respect the UI**: `GameHudUI.IsPointerOverUI()` gates `PlayerController`'s raycast, so clicking a button doesn't also click the world.

## Shell — inZOI-style glass clusters

Modelled on inZOI (user's reference): **floating translucent-glass clusters pinned to the edges, clear
centre.** Glassmorphism-*lite* — translucent charcoal + hairline border + rounding, **no backdrop blur**
(real blur needs a custom pass; deferred). All zones are present; ones without a system yet render as
**placeholder slots** so the whole composition is visible.

Zones (`GameHud.uxml`, anchored by `.zone--*`):
- **top-left** — location pill (lot name)
- **top-centre** — camera/view cluster (placeholder slots)
- **top-right** — logo + menu (menu = placeholder)
- **right edge** — **Sim switcher**: a round portrait per Sim (placeholder = colour + initial), click to possess; selected gets an accent ring
- **bottom-left** — clock + day/night + the **speed transport** (5 round icon buttons, active highlighted)
- **bottom-centre** — the **controlled-Sim focus card**: portrait, name, a live mood chip (derived from the worst need), current action, a need-icon strip (tinted green→red by value), and placeholder action-queue slots. Hidden until a Sim is possessed.
- **bottom-right** — money (placeholder) + **modes** (Build, Edit Look, + placeholder slots)

Portraits are placeholders (colour + initial) — live rendered headshots are a later feature. Icons come
from `IconLibrary` (placeholder for any missing name). The old `GameDebugHud` (IMGUI) is unused; the
class remains for reference.

## Gameplay camera

`GameCamera` (on the Main Camera) is the free gameplay camera — orbits a ground pivot:
- **Pan** — WASD / arrows, or middle-mouse drag (view-relative, faster when zoomed out)
- **Orbit** — right-mouse drag, or Q / E (yaw); right-drag also pitches
- **Zoom** — mouse wheel · **Focus** — F re-centres on the possessed Sim

Left-click is reserved for possession/selection, so the camera avoids it. Runs on **unscaled time** (works
while paused, consistent at any game speed), ignores input while the pointer is over a UI panel, and
**yields to CAS/Build** (which drive the camera themselves). Camera movement *inside* build mode is a
possible follow-up (currently the modal modes freeze it).

## Reusable catalog widget

`CatalogList` (`Runtime/Game/CatalogList.cs`) wraps a `ListView` as a **virtualized** icon+label list with single selection — the workhorse for every long catalog. FixedHeight virtualization; each `CatalogEntry` carries a label, an optional `Texture2D` icon (falls back to a colour `swatch`), and arbitrary `data` payload. Style lives in `.catalog*` classes in `GameTheme.uss`.

## Build mode (migrated to UI Toolkit)

`Build.uxml` + `GameBuildMode` now drive the palette through `CatalogList` (its own `UIDocument`, drawn above the HUD, shown only in build mode). World interaction (place/select/drag/delete raycast) stays in `GameBuildMode.Update`; `PointerOverPanel` stops panel clicks from also placing in the world. The IMGUI `OnGUI` is gone.

## Icons (vector)

Icons are **vector**, imported from `Assets/Game/UI/Icons/svg/` as UI Toolkit `VectorImage` — crisp at
any resolution (the user's call; no rasterization). **Unity 6 imports SVG natively** via the built-in
`VectorGraphicsModule` — do **not** add the standalone `com.unity.vectorgraphics` package (it duplicates
the built-in types → CS0433, which freezes every assembly). The starter pack is the 29-icon "Sim Game
Icon Design System" (24×24): speeds, modes, debug, settings, 4/6 needs, 4/6 emotes.

> The built-in importer **does not support `currentColor`** as a fill (it warns and imports blank). The
> project copies of the pack icons are baked to `#ffffff` (white) so they render and stay tintable via
> USS `-unity-background-image-tint-color`. New icons must use an explicit colour, not `currentColor`.

- **`IconLibrary`** (ScriptableObject) maps `name → VectorImage`; **`IconLibrary.Get(name)` returns a
  placeholder** for any missing name, so absent icons show an obvious placeholder instead of breaking
  the UI. `placeholder.svg` is a deliberately-visible grey dashed "?" box.
- **`IconLibraryBuilder`** (editor) scans the icons folder for `VectorImage` assets and (re)builds
  `IconLibrary.asset`. Menu: **Sims4 Creator → Game → Rebuild Icon Library**; the scene builder also
  calls it so a fresh scene has a current library.
- **Import requirement:** the SVGs must import as UI-Toolkit VectorImage (package present + import mode).
  Until then the library is empty and everything shows the placeholder — nothing breaks.
- Wired so far: the **need meters** show a per-need icon (hunger/energy/fun/bladder from the pack;
  hygiene/social → placeholder until provided). Speed multipliers were set to `2×/3×/5×` to match the
  speed icon art.

Missing/next icons are tracked in **TASK-005**.

## Next

1. **Furniture catalog** — Build mode should become a full graphic catalog (**TASK-004**), not the
   placeholder palette.
2. **CAS** → migrate the giant IMGUI `CasController` panel to UI Toolkit reusing `CatalogList` (**TASK-006**).
3. Later: main menu, save/load screens, tooltips, filtering/search on catalogs.
