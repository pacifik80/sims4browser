# System: M4 — In-game Build

Status: **built, awaiting Play-test.** The environment half of "author the world" (M4 part 2, alongside [in-game CAS](m4-ingame-cas.md)): reshape the lot live by placing, moving, and deleting the objects the Sims actually use.

## How it plays

- HUD → **Build Mode** (always available). The world **pauses** and a build palette appears on the left.
- **Place**: click a type (Fridge / Bed / Toilet / Shower / TV / Couch), then click the ground — drops a new object there; keep clicking to place several.
- **Move**: in Select/Move mode, click an object to select it, then **drag** it across the ground.
- **Delete**: select an object → **Delete selected** button or the **Del** key.
- **◄ Back to Game** (Esc) → resume. Newly placed objects immediately advertise their interactions, so Sims start using them.

## Design — one object definition, two creators

The buildable objects live in **`SmartObjectCatalog`** (name, size, colour, advertisements) and are instantiated by **`SmartObjectFactory`**. Both the editor scene builder (the initial lot) and the runtime build mode call the *same* factory, so a player-placed Fridge is identical to a pre-placed one — no drift. This replaced the scene builder's private inline object helper.

Because a `SmartObject` is self-describing (it advertises its interactions) and the AI re-scans objects, a freshly placed object is picked up by the utility brain with no extra wiring — build and autonomy compose for free.

## Code (`unity/Sims4Creator/Assets/Scripts/Runtime/Game/`)

| File | Role |
|---|---|
| `SmartObjectCatalog.cs` (new) | `BuildableDef` (look + advertisements) + the catalog of the six object types; shared. |
| `SmartObjectFactory.cs` (new) | `Create(def, groundPos)` — builds the box + `SmartObject` + use anchor + material, at runtime or edit time. |
| `GameBuildMode.cs` (new) | The mode: pause, palette, ground-plane raycast to place; click-select + drag to move; Delete to remove. |
| `GameDebugHud.cs` (updated) | **Build Mode** button. |
| `GameSceneBuilder.cs` (updated) | Initial lot now uses the catalog/factory; adds the `Game Build Mode` object. |

## Deliberate simplifications (tracked)

- **Objects only** — no walls/floors/rooms. The full building editor (walls/coverings/doors, earlier-stage) folds in as a later mode; this proves the place/move/delete loop with the objects that matter to gameplay.
- **No grid/rotation** — free placement on the ground, no snapping or object rotation yet.
- **Not persisted** — the edited layout lives for the session; saving the lot layout reuses the same catalog data and is a follow-up.

## How to run

Re-run **Sims4 Creator → Game → Build M0 Scene**, **Play**, → **Build Mode** on the HUD. Place a couple of extra fridges/beds, drag them around, delete one, then **Back to Game** — the Sims will start walking to whatever you placed.

## M4 status

Both halves of M4 are in: [in-game CAS](m4-ingame-cas.md) (author people) + in-game Build (author the lot). The "author the world" pillar is now playable. Remaining big milestone: **M5 — city & scale**.
