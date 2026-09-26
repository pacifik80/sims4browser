# Game Design Document

Status: **living draft.** Vision is stable; specifics evolve as we build. Working title: TBD.

## Vision

A city-scale **life-simulation sandbox**. A living city of autonomous NPCs, each with their own household, personality, relationships, and daily life. The player is not tied to one avatar: they can **take direct control of any NPC** and live the world from that person's perspective, then release them back to autonomy. Between and during those moments, the player can reshape the world — rebuild homes, restyle people, engineer relationships and events.

Genre touchstones: *The Sims* (autonomous needs-driven agents, build/buy, CAS) crossed with a **possession / body-hopping** control model.

## Pillars

1. **A world that lives without you.** NPCs pursue their own needs, schedules, jobs, and relationships whether or not the player is watching. The simulation is the star; the player is a participant, not the center.
2. **Be anyone.** The player can possess any NPC and act as them, then let go. Control is fluid; identity is not fixed to a single character.
3. **Author the world.** Build/rebuild homes and apartments; edit appearance, clothing, and personality; stage events and relationships. The existing CAS and building editors are these authoring tools, in-game.
4. **Believable, not just functional.** Characters look and move like real Sims (the asset pipeline already delivers this); their behavior should read as motivated, not random.

## Core loop

**Session loop:** Observe the city → pick an NPC (watch, or possess) → act (live their life / satisfy needs / socialize / work / wander) or author (build / restyle / arrange) → release → the world advances → consequences ripple → repeat.

**Moment-to-moment (autonomous NPC):** need decays → NPC evaluates available actions → chooses and performs the best one → need satisfied, mood shifts, relationships/skills update → time passes.

**Moment-to-moment (possessed NPC):** player input drives the body; needs still apply (a possessed Sim still gets hungry/tired); releasing hands control back to the NPC's own decision-making from its current state.

## Player capabilities (target)

- **Observe** the city and any household.
- **Possess / release** any NPC.
- **CAS**: edit an NPC's appearance, clothing, and personality (existing character creator).
- **Build**: construct/rebuild lots — walls, rooms, furnishings (existing building editor).
- **Interact**: socialize with other characters, trigger events.
- **Live**: work, satisfy needs, wander the city.

## Scope — first vertical slice (what proves the game)

**In:** one lot; a handful of existing characters (`am_char`/`af_char` and variants); a game clock with day/night; needs that decay; autonomous need-satisfying behavior; navigation on the lot; possess/release of one NPC; a debug HUD.

**Out (deferred past the slice):** the full city + streaming; jobs/economy; deep social/relationship systems; scripted events; in-game entry into Build/CAS. These are milestones after the loop is proven — see [roadmap.md](roadmap.md).

## Open questions

The load-bearing design questions (simulation scope, control feel, simulation depth) are tracked in [decisions.md](decisions.md) and gate the architecture. This document will be filled in as they're answered.

## Glossary

- **Sim / NPC** — an inhabitant of the world. In these docs, the persistent character; its *soul* (data) is distinct from its *body* (the rendered GameObject) — see [architecture.md](architecture.md).
- **Household** — a group of Sims sharing a home lot.
- **Lot** — a parcel with a building and its contents (residential / commercial / public).
- **Possession** — the player taking direct control of a Sim.
- **CAS** — Create-A-Sim: the appearance/clothing/personality editor.
- **Smart object** — a world object that advertises the interactions and need-satisfaction it offers (see architecture).
