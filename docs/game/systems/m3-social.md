# System: M3 Social

Status: **✅ core verified (2026-07-14); populating the lot next.** Sims now interact with *each other*, not just objects — and remember it. Adds a relationship model and the first **two-party interaction**. Follow-up in progress: cloning the two base looks into a small crowd so the social web is meaningful.

## What it adds

- **Sim-to-Sim socializing.** A Sim walks to another Sim; the partner stops and waits; both gain the **social** need and their **relationship** grows for the duration, then both go idle.
- **Relationships.** A pairwise score (−100..+100) per Sim pair, shown on the HUD as `Adam ↔ Eve: 30 (friends)`.
- **Autonomy.** When a Sim's social need is low and another Sim is free (idle + not possessed), it prefers going to *them* over the placeholder couch.
- **Possession.** While possessing, **click another Sim** to send yours over for a chat (click an object still directs to the object; Esc still releases).

## Code (`unity/Sims4Creator/Assets/Scripts/Runtime/Game/`)

| File | Role |
|---|---|
| `RelationshipBook.cs` (new) | Pairwise −100..+100 scores, order-independent key. Plain C#, no UnityEngine dep. |
| `SimAgent.cs` (updated) | New states **GoingToSocial / Socializing / HeldSocial**. `BeginSocial(other)` walks over and reserves the partner (`HoldForSocial`); on arrival both chat — the initiator applies social to *both* souls and grows the relationship each tick; `EndSocial` releases the partner. `ReleaseHeldPartner` prevents stuck partners on interruption. |
| `SimulationDirector.cs` (updated) | Owns the `RelationshipBook`. Each idle autonomous Sim now weighs the best object interaction against the best social partner (`FindSocialPartner`: nearest free Sim, `deficit + 10 − 2·dist`) and does whichever scores higher. |
| `PlayerController.cs` (updated) | Possessing + clicking another Sim → `BeginSocial`. |
| `GameDebugHud.cs` (updated) | A "Relationships" section listing every pair + a label (strangers → acquaintances → friends → close). |

## How the two-party handshake works (self-healing, no recursion)

Only the **initiator** drives the interaction. `BeginSocial(B)` immediately puts B into `HeldSocial` (B stops and won't also initiate — this avoids a double-initiation race when both are processed in the same frame). The initiator walks up, both face each other, and for the duration it adds the social need to **both** souls and increments the relationship.

**Teardown is self-healing and never cross-recursive.** Every agent only ever changes its OWN state (`GoIdle` touches nothing on the partner). A held partner watches its initiator each frame and releases itself once the initiator is no longer engaging it (`IsEngagingWith`); an initiator drops the chat if its partner is no longer held by it (`IsHeldBy`).

> **Bug fixed (2026-07-14):** the first version had the initiator call the partner's `GoIdle` during teardown (`GoIdle → ReleaseHeldPartner → EndSocial → GoIdle`). With six Sims churning at 3× speed, two could end up mutually "held", and because state wasn't cleared before the cross-call, the two `GoIdle`s ping-ponged into a **StackOverflowException**. Lesson: cooperating state machines must self-heal by *observing* each other, not by *calling* each other's teardown.

## Deliberate M3 simplifications (tracked)

- **Six Sims via cloning.** Only `am_char`/`af_char` are exported, so the scene builder builds those two, then `Object.Instantiate`s them into a crowd of six (Adam, Ben, Carl / Eve, Fay, Gwen) — clones share a look but are independent Sims. Distinct exported appearances are a later refinement. The HUD scrolls and shows only relationships that have actually formed.
- **One social interaction** ("chat", always positive). Distinct socials (joke/argue/flirt) with trait- and mood-dependent outcomes are a later pass — the advertisement model is ready for them.
- Same M1/M2 caveats (slide instead of walk anim; no object occupancy).

## How to run

Re-run **Sims4 Creator → Game → Build M0 Scene**, press **Play** at 2×/3×: Adam and Eve should walk to each other and chat when sociable (watch `Adam ↔ Eve` climb on the HUD). Possess Adam and **click Eve** to send him over for a chat on demand.

## Next

M4 (in-game Build + CAS) or continuing to deepen social (distinct interactions, traits). See [../roadmap.md](../roadmap.md).
