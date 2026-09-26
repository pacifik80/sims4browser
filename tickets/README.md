# Tickets

The single home for **actionable work** — "what needs doing." (Design & architecture live in
`docs/`; what Claude has learned lives in agent memory. Neither of those is a to-do list.)

## Layout
- `bugs/` — things that are broken · `bugs/archive/` — resolved
- `tasks/` — things to build or improve · `tasks/archive/` — done
- `index.md` — a **generated** overview of open items. Do not hand-edit it; it is rebuilt from the
  ticket files, whose frontmatter is the single source of truth.

## IDs
`BUG-NNN` / `TASK-NNN`, zero-padded, monotonic, never reused. The ID stays with the ticket forever —
when resolved, the file moves to `archive/` but keeps its name. Reference tickets from commits and
code, e.g. `fixes BUG-014`.

## Format — YAML frontmatter + a short body

Bug:
```
---
id: BUG-001
title: <one line>
status: open        # open | in-progress | blocked | done
area: home-editor   # home-editor | clothing | cas | sky | export | ...
severity: minor     # minor | major | critical
created: 2026-07-13
---
**Context** — where/when it happens.
**Repro** — the trigger or steps.
**Expected** — what should happen instead.
**Notes** — findings, links, related tickets.
```

Task:
```
---
id: TASK-001
title: <one line>
status: open
area: home-editor
priority: high      # low | medium | high
created: 2026-07-13
---
**Goal** — what we want.
**Acceptance** — how we'll know it's done.
**Notes** — approach, links, related tickets / requirements.
```

## Lifecycle
`open → in-progress → done`. On **done**: set `status: done`, add a one-line **Resolution**, move the
file into `archive/`, and rebuild `index.md`.

## Rebuilding the index
The index is a mechanical scan of the ticket frontmatter (Claude regenerates it whenever tickets
change). Keep status in the ticket, never only in the index — that's what stops the index from drifting.
