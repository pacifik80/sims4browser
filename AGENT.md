# Sims4Browser Operating Guide

This file is the single source of truth for repo-level working rules.

## Read First On Resume

1. `AGENT.md`
2. `docs/planning/current-plan.md`
3. `docs/planning/big-plan.md`
4. `docs/README.md`
5. `docs/operations/multi-agent-workflow.md`
6. `docs/operations/tooling.md`
7. `docs/planning/technical-debt.md`
8. `docs/planning/unknowns-and-non-goals.md`
9. `docs/architecture.md`
10. `docs/supported-types.md`
11. `docs/known-limitations.md`
12. `docs/references/codex-wiki/README.md`
13. `docs/sim-domain-roadmap.md` when touching `CAS` or `Sim`
14. `docs/sim-body-shell-contract.md` when touching `Sim Archetype` body-shell selection, preview, or audit logic

## Single Sources

- Repo-level rules: `AGENT.md`
- Live task plan: `docs/planning/current-plan.md`
- Durable roadmap: `docs/planning/big-plan.md`
- Multi-agent operating model: `docs/operations/multi-agent-workflow.md`
- Tooling, verification ladder, and scratch-space map: `docs/operations/tooling.md`
- Documentation index: `docs/README.md`

## Project Intent

- Build a read-only, honest Sims 4 resource explorer and viewer.
- Prefer factual parsing and authoritative assembly over compatibility guesses.
- Mark unsupported or partial paths explicitly instead of faking support.
- Treat the app as a long-lived engineering project, not a pile of one-off probes.

## Mandatory Planning Rule

Before starting any meaningful action, first write or update the live plan in `docs/planning/current-plan.md`.

Every active plan must contain these sections:

1. The problem being solved.
2. The chosen approach.
3. The actions to perform, with explicit status markers showing what is already done and what is still pending.
4. Other hints needed to resume the work in a new chat if execution is interrupted.

Additional rules:

- The same plan must be updated as work progresses inside the current user request. Do not wait until the end of the session.
- Keep one clear active checklist instead of scattering progress across multiple process documents.
- When the task is broad enough for delegation, the manager still writes the plan first and then opens agent packets from that plan.

## Working Rules

- Truth over heuristics. If a path is not proven by format docs, reference code, or repeatable fixtures, say so plainly.
- Prefer authoritative links from `SimInfo`, `CASPart`, `GEOM`, `Rig`, `Skintone`, `RegionMap`, and related resources before adding fallback search logic.
- Treat the live serving index as immutable after an explicit index build. If new persisted metadata or facts are needed, add them to explicit indexing passes plus invalidation/rebuild logic instead of writing them from browse/open/export read paths.
- Treat `docs/sim-body-shell-contract.md` as the current source of truth for initial `Sim Archetype` body-shell behavior. If implementation or concept changes, update that contract in the same change set.
- Before inventing new logic, consult local docs in `docs/`, then `docs/references/codex-wiki/`, then local external snapshots under `docs/references/external/`, and only then live external sources if the local material is still insufficient.
- For game-assembly tasks, study authoritative local material first. If that is still insufficient, prefer the most authoritative external sources available. If a clean authoritative solution is still missing and a workaround seems necessary, stop and ask the user before implementing it.
- Fallbacks and workarounds are architectural exceptions here, not casual implementation details.
- The goal is an honest viewer/exporter, not a "good enough looking" reconstruction.
- Keep package files strictly read-only.

## Multi-Agent Default

For broad work, especially `Sim`, `CAS`, indexing, and cross-layer preview/export changes, use the repo's multi-agent operating mode in `docs/operations/multi-agent-workflow.md`.

- The primary thread owns manager and architecture-gate responsibilities.
- Explorers are read-only and gather facts before implementation starts.
- Workers own one packet and one explicit write set.
- Verifier work stays separate from worker implementation.
- Parallel workers are allowed only when their write sets are disjoint.
- Before a worker starts, define the packet goal, allowed files, verification target, and architectural red lines.
- If authoritative research still does not justify a clean implementation path, stop and escalate before adding a workaround or fallback.

## Verification And Release Rules

- Prefer this verification ladder: targeted tests, full test suite when justified, `tools/ProbeAsset` or focused package/resource queries, live SQLite cache inspection, and UI launch only when the task is genuinely visual.
- Do not build the app just to satisfy routine non-visual verification. Prefer tests, probes, and cache inspection unless the task genuinely needs a visual check or the user explicitly wants a runnable build.
- Only increment `<BuildNumber>` in `src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj` when producing a user-facing verification build for UI review or an explicitly requested runnable build.
- The same build id must appear in the window title and in the diagnostics `Build:` line.
- When reporting a verification build, always state the exact build id.
- When asking the user to manually launch the app, always give one exact command and explicitly say whether it rebuilds or reuses existing binaries. Do not leave launch mode implicit.
- Default manual-test launch command is `.\run.ps1`: this is the authoritative "rebuild and launch fresh" path and may stop a currently running app instance to refresh binaries.
- Use `.\run.ps1 -NoBuild` only when the user should rerun the already-built binaries on purpose. Do not recommend `-NoBuild` for a packet that depends on newly landed code unless you have already said that no rebuild is needed.
- If a packet must launch from a nonstandard output path instead of `run.ps1`, say that explicitly and explain why the standard script is not being used for that run.

## Documentation Discipline

- Any substantial change to behavior, supported subsets, workflows, limitations, or current priorities must update the relevant docs in the same change set.
- `docs/planning/current-plan.md` is the live execution plan and must stay current while work is in progress.
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

- The main architectural frontier is still honest `Sim` assembly: authoritative body/head/outfit selection, real torso/head graph, rig/body/head path, skintone/region map synthesis, and morph/deformer application.

## Session Closeout

- After meaningful progress, update the live plan before ending the session.
- Update `docs/planning/big-plan.md` if durable priorities changed.
- Update this file, `docs/operations/multi-agent-workflow.md`, and `docs/operations/tooling.md` when repo rules changed.
