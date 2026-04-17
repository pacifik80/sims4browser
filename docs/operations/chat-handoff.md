# Chat Handoff

Use this when resuming work after an interrupted session.

## Full Version

```text
Continue work on Sims4Browser in:

c:\Users\stani\PROJECTS\Sims4Browser

Read first:
1. AGENT.md
2. docs/README.md
3. docs/planning/big-plan.md
4. docs/planning/current-plan.md
5. docs/planning/technical-debt.md
6. docs/planning/unknowns-and-non-goals.md
7. docs/architecture.md
8. docs/known-limitations.md
9. docs/supported-types.md
10. docs/references/codex-wiki/README.md
11. docs/sim-domain-roadmap.md if working on CAS or Sim assembly

Working rules:
- Build an honest read-only viewer/exporter, not a heuristic approximation dressed up as support.
- Prefer authoritative data paths and factual parsers before compatibility fallbacks.
- Before inventing logic, check local docs and references first, then use live external modding/documentation sources if needed.
- Every user-facing verification build must increment <BuildNumber> in src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj.
- The same build id must appear in the title and diagnostics Build line.
- Any substantial behavior/support/workflow change must update the relevant docs in the same change set.
- Prefer targeted tests, full suite, sqlite inspection, and focused package/resource queries before launching the UI.
- tmp/ is scratch. junk/local-artifacts/ is the holding pen for likely-disposable local files.

Then summarize:
1. what the current active block is
2. where progress currently stands
3. what the next concrete step should be
```

## Short Version

```text
Continue Sims4Browser in c:\Users\stani\PROJECTS\Sims4Browser.
Read AGENT.md and docs/README.md first, then the planning docs under docs/planning/.
Prefer authoritative, documented Sims 4 data paths over heuristics.
Keep the app honest about unsupported states.
Increment BuildNumber for every user-facing verification build and report the exact build id.
Update the relevant docs whenever substantial behavior or priorities change.
Then continue the current active block.
```
