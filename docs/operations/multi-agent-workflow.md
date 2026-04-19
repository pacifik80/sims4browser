# Multi-Agent Workflow

Use this operating mode when the active block is too broad for one agent to hold in working memory without mixing research, architecture, implementation, and verification.

## Default Roles

### Manager

The primary thread owns manager responsibilities.

- keeps `docs/planning/current-plan.md` aligned with the active block using the mandatory `Problem`, `Approach`, `Actions`, and `Restart hints` shape
- chooses the next packet boundary and decides when a packet is done
- defines the allowed write set, verification target, and architectural red lines before implementation starts
- accepts or rejects worker output based on evidence, not momentum

The manager also acts as the architecture gatekeeper for this repo:

- no casual workaround or fallback expansion
- no silent policy drift from honest/authoritative paths toward "looks okay"
- no broad refactors unless the active packet explicitly calls for them

### Explorer

Explorers are read-only.

- inspect local docs, references, code, tests, and tooling first
- use the web only for questions that are unstable, missing locally, or require authoritative external confirmation
- return narrow findings, exact file targets, open questions, and verification hooks
- do not edit files or blend into implementation

Use explorers in parallel when the questions are independent.

### Worker

Workers implement one narrow packet at a time.

- own one explicit write set
- do not edit outside that write set unless the manager expands it first
- keep changes aligned with the packet goal instead of opportunistically fixing everything nearby
- update docs in the same change set when the packet changes behavior, support level, workflow, or current priorities

Parallel workers are allowed only when their write sets are disjoint.

### Verifier

The verifier is separate from the worker.

- reruns the agreed targeted checks
- expands to the full suite when the packet is broad enough to justify it
- uses `tools/ProbeAsset`, focused package/resource queries, or SQLite inspection when tests alone cannot prove the claim
- runs the UI only when the packet is genuinely visual or the user explicitly wants it
- reports whether the packet addressed the stated problem and whether it introduced policy drift or fallback creep

The verifier should not quietly "finish the fix." If verification exposes a defect, that becomes a new packet.

### Probe / Data Forensics

Use this role when the repo's synthetic tests are green but the live game data still contradicts the current model.

- works against the real SQLite index, package bytes, `tools/ProbeAsset`, and focused one-off probes
- confirms whether a bug comes from missing data, wrong source selection, wrong parsing, or wrong assembly/routing
- treats "the game definitely has this data" as a falsifiable probe question, not as a place for fallback heuristics
- returns concrete live-data evidence that the manager can turn into a new implementation or verification packet

This role is still read-mostly. If it needs a durable tool change, the manager should open a separate worker packet for that tooling change instead of letting the probe role drift into implementation.

## Packet Rules

Every packet should fit on one screen and answer these questions up front:

1. What exact behavior is changing?
2. Which files may be edited?
3. How will the result be verified?
4. What architectural shortcuts are forbidden?

Prefer packets that end in one of these outcomes:

- a merged code change with verification
- a documented research conclusion
- a rejected approach with the reason recorded

Avoid packets like "continue Sim assembly" or "clean up the architecture." They are too large to coordinate well.

## Repo-Specific Guardrails

For this repository, the multi-agent split exists to protect the project from three recurring failure modes:

- authoritative Sims 4 assembly research getting mixed with speculative implementation
- large orchestrator files absorbing yet more responsibility because nobody defined a seam first
- support statements drifting ahead of what preview/export paths actually prove

Treat fallback/workaround additions as architectural exceptions. If authoritative research is still insufficient and a workaround seems necessary, escalate before implementation rather than normalizing it inside a worker packet.

## Default Operating Cadence

1. Manager writes or updates the current packet in `docs/planning/current-plan.md`, including the problem, chosen approach, live action checklist, and restart hints.
2. One or more explorers answer the packet's open questions.
3. Manager narrows the packet again if the explorer output is still too broad.
4. One worker implements the packet inside the allowed write set.
5. Add a probe/data-forensics pass when synthetic verification and live-package behavior disagree.
6. One verifier checks the claim independently.
7. Manager updates planning docs and either closes the packet or opens the next one.

## Packet Template

Use this shape when opening a packet:

- Goal:
- Owner:
- Allowed write set:
- Verification:
- Red lines:
- Deliverable:

## Current Default

For broad `Sim`, `CAS`, or indexing frontier work, multi-agent mode is now the default operating style for this repo.
