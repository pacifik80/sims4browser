# Game Decision Log

Every significant game decision is recorded here: dated, with context, the decision, its rationale, and the alternatives considered. Entries are **superseded** by later entries, never silently rewritten. Status is one of `OPEN`, `PROPOSED`, `ACCEPTED`, or `SUPERSEDED`.

---

## Accepted

### D-001 — Simulation scope: single active lot  ·  `ACCEPTED (2026-07-14)`
**Decision:** one household/lot is fully simulated at a time; the rest of the city is abstract/statistical. Architect the soul/body split + LOD tiers so a streamed city (D-001b, future) is reachable without a rewrite.
**Rationale:** smallest CPU/streaming cost; proves the core loop fastest; the city can be a backdrop that catches up on visits.
**Alternatives:** streamed city + sim-LOD from the start — deferred (bigger up-front streaming/routing/perf cost, not needed to prove the game).

### D-002 — Player control model: point-and-click (Sims-style)  ·  `ACCEPTED (2026-07-14)`
**Decision:** possession is **directed control** — the player selects a Sim, then clicks objects/other Sims to choose from that target's advertised interactions, which **queue on the possessed Sim** using the *same interaction system* the AI uses. Camera is a free/orbit "god" camera (reuse the CAS orbit camera). No avatar/character controller.
**Rationale:** matches the classic Sims feel; makes possession a **brain swap with zero new control tech** — the AI brain (autonomous scorer) is simply replaced by a player-issued interaction queue; needs still tick; release resumes autonomy. Also removes the need for a character controller, camera-relative movement, and animation-locomotion blending in the slice.
**Alternatives:** direct third-person (WASD) — rejected for now (more control/camera/animation tech, diverges from the interaction system); hybrid — possible later.

### D-003 — Simulation depth (first build): needs + autonomy first  ·  `ACCEPTED (2026-07-14)`
**Decision:** the first playable build uses a small set of **needs** + **utility-AI/smart-objects**; traits, skills, careers, and relationships are later milestones.
**Rationale:** prove the autonomy loop on a few needs before layering systems.
**Alternatives:** full life-sim from the start — deferred (slower to a runnable slice).

### D-100 — Soul/Body split  ·  `ACCEPTED (2026-07-14)`
Sim state is plain serializable C# data (soul), separate from the presentation GameObject (body). Specifics validated during M0. See [architecture.md §1](architecture.md#1-the-one-principle-separate-the-soul-from-the-body).

### D-101 — Autonomy via utility AI + smart-object advertisements  ·  `ACCEPTED (2026-07-14)`
Objects advertise need-satisfying interactions; Sims score and pick. Alternatives (behavior trees / GOAP) rejected as more per-NPC authoring, less emergent. See [architecture.md §3](architecture.md#3-autonomy-utility-ai-over-smart-object-advertisements).

### D-102 — Possession = brain swap  ·  `ACCEPTED (2026-07-14)`
Player control replaces the AI brain with a player-issued interaction queue on the shared soul; needs keep ticking; release resumes AI. Refined by D-002 (point-and-click). See [architecture.md §4](architecture.md#4-possession--brain-swap-directed-control).

### D-103 — Plain-C# simulation + pooled GameObject presentation (no DOTS)  ·  `ACCEPTED (2026-07-14)`
Souls are plain classes; bodies are pooled GameObjects for Active-tier Sims only. Revisit only if profiling forces it. See [architecture.md §5](architecture.md#5-unity-implementation-approach).

### D-105 — Game UI framework: UI Toolkit (primary)  ·  `ACCEPTED (2026-07-20)`
**Decision:** build the game UI on **UI Toolkit** (UXML + USS + C#), replacing the IMGUI debug panels. uGUI stays available as an escape hatch only if world-space/diegetic UI needs it (floating indicators can often be 3D objects instead).
**Rationale:** our hardest UI requirement is long, filterable, thumbnail-heavy catalogs (CAS hair/clothing, furniture) — UI Toolkit's `ListView`/`TreeView` **virtualize** (only visible items instantiated), which is the make-or-break feature; USS stylesheets make the whole UI re-themeable from one file ("customizable/extendable"); its "UI reflects state" model matches our plain-data souls/catalogs; and it's engine-native with no license. uGUI is in maintenance mode while UI Toolkit gets Unity's continuous investment.
**Alternatives:** uGUI (better Animator/Timeline animation, proven world-space, deeper asset ecosystem — but hand-rolled recycling for long lists, non-linear perf degradation); full hybrid from day one (deferred — adopt only if world-space UI demands it); third-party (Nova UI, Doozy, New UI Widgets) — rejected to avoid a dependency/license for something engine-native covers.
**Practical notes:** use **FixedHeight** ListView virtualization (DynamicHeight calls `bindItem` for *every* item — 4000 items → 4000 calls vs ~35); runtime UI Toolkit needs an `EventSystem` in the scene (legacy input module, since the project uses the old Input backend).

### D-104 — Reuse existing edit tools as in-game modes  ·  `ACCEPTED (2026-07-14)`
CAS (character creator) and Build (building editor) become the game's authoring modes; appearance = existing `CharacterDefinition`. See [architecture.md §8](architecture.md#8-how-existing-work-plugs-in).

### D-106 — The 1 m tile grid is the first-class space authority  ·  `ACCEPTED (2026-07-21)`
**Decision (user):** the MAIN game grid is **1 m tiles** — furniture/build placement, per-tile **occupancy**
(the placement-validity authority), and Sim **interaction positioning** all live on it. **Navigation may
use finer subgrids to build routes** (currently 0.5 m sub-cells = tile/2), but Sims are *expected to
stand and interact occupying a full tile* — standing in front of an object (approach slot = the tile in
front of the footprint) and talking to each other (adjacent tile centres). **Ruled exceptions:** intimate
interactions (hugs, kisses) may break the tile rule; sitting/sleeping/etc. get their own furniture-slot
mechanics later. Sims still MOVE continuously along routed paths (D-106 constrains where things ARE and
where Sims STAND, not how bodies glide between).
**Rationale:** one grid shared by game furniture and the home-editor engine (walls/doors — TASK-011)
so they can never disagree; 1 m is the Sims-standard tile the exported assets were authored against;
occupancy (not collider physics) makes placement deterministic and cheap.
**Implementation:** `LotGrid` (tiles + occupancy + nav sub-cells + snap API), `GameBuildMode` validity
via `TilesFree`, approach slots at front-tile centres, social stands at adjacent tile centres, visible
1 m grid overlay in build mode. See TASK-014 and [systems/locomotion-navigation.md](systems/locomotion-navigation.md).

---

## Open

*(none right now — next decisions will be logged here as they arise, e.g. the needs set for M0, the first interaction/object list, and the save format.)*
