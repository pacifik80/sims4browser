# Game Architecture

Status: **Accepted foundation (2026-07-14).** The core model below — soul/body split, sim-LOD, utility-AI + smart objects, possession, plain-C# + pooled presentation — is accepted (see [decisions.md](decisions.md) D-100–D-104, plus scope/control/depth in D-001/D-002/D-003). Specifics (the exact need set, first interactions, save format) are validated as they're built, and per-system design docs are added under this folder as each system lands.

Target engine: **Unity 6.3 HDRP**. Built on the existing headless asset-export pipeline and the CAS/building editors in `unity/Sims4Creator/`.

---

## 1. The one principle: separate the SOUL from the BODY

The single most important rule. A Sim has two halves:

- **Soul** — plain, serializable C# **data**: identity, the appearance recipe (our existing `CharacterDefinition`), needs, personality/traits, skills, relationships, inventory, job, schedule, current action/intent, mood. Owned by simulation systems. **No Unity dependency.**
- **Body** — a **presentation GameObject**: the skinned character (our `Sims4Character`), Animator/clips, NavMeshAgent, colliders. Disposable; instantiated only for Sims that must be seen or precisely animated right now.

Why this is non-negotiable:
- Off-screen NPCs simulate cheaply (no GameObject, no animation, no physics).
- The world scales to a city.
- **Possession is trivial**: swap who drives the body (AI brain ↔ player input) on the same soul.
- **Save/load is just data** (extends the existing template save/load).
- The same soul flows through live / CAS / build modes without re-instantiation.

## 2. Simulation LOD (level of detail)

Not every NPC can pathfind and animate at once. Tiered simulation, with promotion/demotion as the player/camera moves:

| Tier | Who | What runs |
|---|---|---|
| **Active** | On the player's lot / near the camera | Full: rendered body, animation, NavMesh pathing, per-tick needs + AI, real interactions |
| **Background** | Elsewhere in loaded areas | No body; a lightweight state machine advances schedule + needs in coarse ticks |
| **Dormant** | Rest of the city | Advanced statistically on day boundaries (needs drift, job, relationships); instantiated on demand |

Per **D-001 (single active lot)**, the current scope makes **Active = the one loaded lot**, and Background/Dormant a thin statistical layer for everyone else. The tiering exists now only so a streamed city (future) drops in without a rewrite — see [decisions.md](decisions.md).

## 2b. Time & speed — the simulation must be speed-invariant

A speed setting changes **how much game time passes per real second — and nothing else**. Every simulated quantity scales with it equally, so the world evolves identically at any speed: a Sim always takes the same number of *game minutes* to cross a room, whether you watch that at 1× or 8×.

`GameClock` enforces this with a single knob — it drives `Time.timeScale` from the chosen multiplier. The clock, Sim movement, and animation playback all read `Time.deltaTime`, so they stay exactly proportional (and foot-matching keeps working, because body speed and clip rate scale together). Modes that pause the world (CAS/Build) set `GameClock.ExternalPause` instead of touching `Time.timeScale`, so leaving them restores the player's chosen speed.

> **Anti-pattern (a real bug here, fixed 2026-07):** advancing some systems on game time (needs, interaction durations) while others run on real time (movement). The mix made travel cost a different number of game-hours at each speed — the simulation stopped being reproducible, and raising the speed visibly failed to make Sims move faster.

**Scaling further — event scheduling.** Ticking every agent every frame is fine for one lot, but not for a city at high speed. The standard answer is a **discrete-event scheduler**: future events are queued at game times ("arrives 09:15", "meal ends 09:45") and popped as the clock advances, so background Sims cost almost nothing and fast-forward stays cheap. That is the mechanism the Background/Dormant LOD tiers in §2 will use; the Active tier stays continuously simulated.

## 3. Autonomy: utility AI over smart-object advertisements

The proven life-sim pattern (and how the real Sims works):

- **Smart objects** in the world **advertise** the interactions they offer and the needs those satisfy — e.g. `Fridge → "Get food" → +Hunger`, `Bed → "Sleep" → +Energy`.
- A Sim's **brain** scans reachable advertisements and **scores** each by `need deficit × object quality × personality/trait modifiers × distance`, then picks the best. No per-NPC scripting; behavior emerges from data.
- **Interactions** are first-class: preconditions, a target anchor/slot, an animation, a duration, per-tick and on-complete effects, and interruptibility. A Sim runs a small **interaction queue**.

This keeps autonomy **data-driven** (designers add objects/interactions as assets, not code) and scalable (background tiers can score the same advertisements without a body).

## 4. Possession = brain swap (directed control)

A Sim's body is driven by a **controller**. Normally that's the **AI brain** (§3), which autonomously scores advertisements and picks interactions. To **possess** (per **D-002**, point-and-click), the player *becomes* that Sim's controller: select the Sim, then **click a target** (object or another Sim) to open its advertised interactions and **queue the chosen one** — the same interaction the AI could have picked. Autonomy is suspended (the Sim stops self-deciding), but **needs keep ticking** and queued interactions execute normally. **Release** re-attaches the AI brain, which resumes scoring from the current state.

Consequence: the interaction system is **shared** between AI and player — the player is just a manual advertisement-picker. No character controller, camera-relative movement, or locomotion blending is needed for the slice; a free/orbit "god" camera (reused from CAS) plus click-to-route-and-interact covers control. Any Sim is possessable because control is a swappable driver on a shared soul.

## 5. Unity implementation approach

- **Plain-C# simulation + pooled GameObject presentation.** Souls are plain classes held in registries by manager systems; bodies are pooled GameObjects instantiated only for Active-tier Sims. **Not DOTS/ECS** for now — it would fight our GameObject-based asset/render pipeline and add large complexity for a scale we can reach with LOD + pooling. (Revisit only if profiling forces it.)
- **Managers** are MonoBehaviour singletons in a persistent **Bootstrap** scene (Time, Simulation, World, Presentation, Control, UI).
- **Data as ScriptableObjects**: trait, need, interaction, career, and object-catalog definitions authored as assets — designer-editable and testable in isolation.
- **Scenes/streaming**: a persistent Bootstrap scene plus additively-loaded lot content; the city is data-driven. Streaming breadth follows the simulation-scope decision.

## 6. Systems

| System | Owns | Notes / existing work |
|---|---|---|
| **Time & Clock** | Game time, day/night, calendar, tick scheduler | Reuse `SkyTimeController` (day/night already built) |
| **Sim Model** | The soul data + registry of all Sims | Appearance = existing `CharacterDefinition` |
| **Needs & Mood** | Need decay, mood/emotion | Mood feeds the existing facial-emotion rig |
| **Autonomy (Utility AI)** | Advertisement scoring, action selection | New |
| **Interactions / Smart Objects** | Action library + per-Sim queue | New; animations from the existing clip library |
| **Navigation** | On-lot NavMesh + inter-lot routing | Unity NavMesh + waypoints |
| **World / Lots** | Lots, streaming, object placement | Build-editor output as lot data |
| **Presentation** | Pooled bodies, animation, materials | Existing `Sims4Character` + clip system |
| **Player Control & Camera** | God camera + possession + input | New |
| **Social & Relationships** | Relationship graph, social interactions | New (post-slice) |
| **Jobs & Economy** | Careers, schedules, money | New (post-slice) |
| **Events** | Scripted/emergent events | New (post-slice) |
| **Build mode** | Construct/edit lots | Existing building editor |
| **CAS mode** | Edit appearance/clothes/personality | Existing character creator |
| **Persistence** | World save/load | Extends existing template save/load |
| **Debug / Tooling** | Per-aspect inspectors (needs, AI reasoning, relationships, time) | The "separate tools per aspect" the project wants |

## 7. Assets we need

- **Sim body prefab**: skinned character + Animator + NavMeshAgent + collider (pooled).
- **Object/furniture prefabs** with a `SmartObject` component: advertisements + interaction anchors + animation refs.
- **Lot prefabs**: build-mode output.
- **Animation clip library**: locomotion + interactions (extend the existing idle/walk set).
- **ScriptableObject catalogs**: needs, traits, interactions, careers, object definitions.
- **City layout data**: lots, roads/routing graph, districts (scope-dependent).

## 8. How existing work plugs in

- **Character creator (CAS)** → a Sim's appearance is its `CharacterDefinition`; CAS is the in-game appearance editor. `Sims4Character` becomes the presentation *body* driven by a *soul*.
- **Building editor** → build mode / lot authoring.
- **Animation clips** → the interaction + locomotion library.
- **Save/load templates** → the seed of world persistence.
- **Skin/hair/clothing pipeline** → the wardrobe and look system.

## Conventions

*(Stub — grows as we write code. Proposed starting points, to be confirmed when M0 begins.)*

- **Namespaces**: game runtime under `Sims4Creator.Game.*`; keep it distinct from the asset/CAS-editor code.
- **Simulation logic in plain C#** (unit-testable, no `UnityEngine` dependency); MonoBehaviours only for managers and presentation.
- **Definitions as ScriptableObjects**, suffixed `Definition`.
- **Folder layout** under `unity/Sims4Creator/Assets/Scripts/Game/` mirroring the systems in §6.
- Docs update in the same change as the code (per [README.md](README.md)).
