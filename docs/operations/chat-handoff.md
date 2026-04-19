# Chat Handoff

Use this file only to restart work in a new chat. Repo-level rules stay in `AGENT.md`, and the live task state stays in `docs/planning/current-plan.md`.

## Resume Prompt

```text
Continue work on Sims4Browser in:

c:\Users\stani\PROJECTS\Sims4Browser

Read AGENT.md first.
Then read docs/planning/current-plan.md. It contains the live plan with:
1. the problem being solved
2. the chosen approach
3. the action checklist with done/pending markers
4. restart hints for the next chat

Use docs/README.md when you need the rest of the documentation map.
Use docs/operations/multi-agent-workflow.md when the task is broad enough for manager/explorer/worker/verifier split.
Follow AGENT.md for verification and release/build rules.

Then summarize:
1. the active problem
2. the chosen approach
3. what is already done
4. what is still pending
5. the next concrete step
```
