# Sims4Browser Agent Guide

This file is the repo-level working agreement for future sessions.

## Read First On Resume

1. `docs/README.md`
2. `docs/planning/big-plan.md`
3. `docs/planning/current-plan.md`
4. `docs/planning/technical-debt.md`
5. `docs/planning/unknowns-and-non-goals.md`
6. `docs/architecture.md`
7. `docs/supported-types.md`
8. `docs/known-limitations.md`
9. `docs/references/codex-wiki/README.md`
10. `docs/sim-domain-roadmap.md` when touching `CAS` or `Sim`

## Project Intent

- Build a read-only, honest Sims 4 resource explorer and viewer.
- Prefer factual parsing and authoritative assembly over compatibility guesses.
- Mark unsupported or partial paths explicitly instead of faking support.
- Treat the app as a long-lived engineering project, not a pile of one-off probes.

## Working Rules

- Truth over heuristics. If a path is not proven by format docs, reference code, or repeatable fixtures, say so plainly.
- Prefer authoritative links from `SimInfo`, `CASPart`, `GEOM`, `Rig`, `Skintone`, `RegionMap`, and related resources before adding fallback search logic.
- Before inventing new logic, consult:
  1. local docs in `docs/`
  2. `docs/references/codex-wiki/`
  3. local external snapshots under `docs/references/external/`
  4. live modding/documentation sources on the web when the local material is not enough
- For game-assembly tasks, follow this protocol in order:
  1. study the available local documentation and repo references first
  2. if that is insufficient, search the web and prefer the most authoritative sources available
  3. if a clean authoritative solution is still not found and a fallback or workaround seems necessary, stop and ask the user explicitly before implementing it
- Fallbacks and workarounds are not neutral implementation details here; treat them as architectural exceptions that can contaminate the project if added casually.
- The goal is an honest viewer/exporter, not a "good enough looking" reconstruction.
- Keep package files strictly read-only.

## Verification Rules

- Prefer this ladder:
  1. targeted tests
  2. full test suite
  3. live SQLite cache inspection
  4. focused package/resource queries
  5. UI launch only when the task is genuinely visual
- Do not build the app just to satisfy routine non-visual verification. Prefer tests, probes, and cache inspection unless the task genuinely needs a visual check or the user explicitly wants a runnable build.
- Only increment `<BuildNumber>` in `src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj` when producing a user-facing verification build for UI review or an explicitly requested runnable app build.
- The same build id must appear in the window title and in the diagnostics `Build:` line.
- When reporting a verification build, always state the exact build id.

## Documentation Discipline

- Any substantial change to behavior, supported subsets, workflows, limitations, or current priorities must update the relevant docs in the same change set.
- `docs/planning/current-plan.md` is the first place to record what block is active now.
- `docs/planning/big-plan.md` tracks the durable roadmap.
- `docs/planning/technical-debt.md` tracks debts we know we should pay down.
- `docs/planning/unknowns-and-non-goals.md` tracks unresolved limits and deliberate non-goals.

## Repo Hygiene

- Durable probe/tooling lives under `tools/`.
- `tools/ProbeAsset` and `profile-live-indexing.ps1` are real working tools.
- The durable tooling map lives in `docs/operations/tooling.md`.
- `tmp/` is scratch space for probe outputs and local experiments, not durable documentation.
- `junk/local-artifacts/` is the holding area for local files that are likely not worth reusing but are not deleted yet.
- Useful research material belongs under `docs/references/`, not in root or temp folders.

## Current Strategic Frontier

- The main architectural frontier is still honest `Sim` assembly:
  - authoritative body/head/outfit selection
  - real torso/head graph
  - rig/body/head path
  - skintone/region map synthesis
  - morph/deformer application

## Session Closeout

- After meaningful progress, update the planning docs before ending the session.
- If the work changed project rules or restart assumptions, update this file and `docs/operations/chat-handoff.md`.
