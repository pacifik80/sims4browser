# Claude Chat (in-Unity)

A docked Editor window that chats with Claude on **your logged-in Claude Code account**, not a
metered API key. It drives the real `claude` CLI you're already logged into.

Open it: **Sims4 Creator ▸ Claude Chat**.

## Billing — read this
Typing in Claude Code interactively (terminal / Rider) draws on your normal subscription usage.
This window runs Claude **headless** (`claude -p`), and as of mid-2026 headless/"Agent SDK" usage
is metered against a **separate monthly programmatic credit pool** (≈$20 Pro / $100 Max-5x /
$200 Max-20x), with optional pay-as-you-go overflow. The `total_cost_usd` in each `done` line is
that consumption; the toolbar shows the per-chat running total. Confirm with `claude auth status`
and your Anthropic usage dashboard. If you want chat that uses your *interactive* allowance, use
Claude Code in Rider instead.

## Look & feel — "Forge" (UI Toolkit)
The panel is built with **UI Toolkit** (USS-styled `VisualElement` tree), not IMGUI — real
rounded widgets, hover/focus states, transitions, dark + light skins. It's an IDE-style chat:
- **Collapsible tool-call blocks** (`⚙ N tool calls` foldout) keep agentic turns tidy.
- **Streaming** token-by-token (`--include-partial-messages`; toggle "stream tokens").
- **Markdown** — headings, lists, task checkboxes (☑/☐), tables (flex grid), blockquotes, rules,
  links, inline code, and **monospace fenced code blocks** (Consolas) with a per-block Copy.
- **Avatars + timestamps**, per-message **Copy**, **Retry** last.
- **History** — toolbar button lists past sessions for this folder; pick one to reopen + `--resume`.
- **Budget bar** — per-chat + per-month spend (auto monthly reset); set your monthly credit for a
  % bar; **Usage ↗** opens the dashboard.
- **Smart auto-scroll** — pauses when you scroll up; **↓ latest** pill to resume.
- **Attachments & vision** — 📎 attach files, drag-drop files/Project assets, or **⛰ Scene / ▶ Game**
  capture the viewport to PNG (HDRP `RenderPipeline.SubmitRenderRequest`, native resolution). The
  paths are appended to your prompt and Claude reads them as actual **vision** (so it can look at a
  render). Attachments are staged into a **`.sims4-claude-chat/`** folder inside the working dir so
  Claude can read them under any permission mode — add that folder to `.gitignore`.
- **Inline asset previews** — image/model/asset paths Claude mentions (or `[[image:…]]`/`[[model:…]]`/
  `[[ping:…]]` tags) render as preview cards with a **Ping** button; taught to Claude via `--append-system-prompt`.
- **Enter** sends · **Shift+Enter** newline. **Model** + **Effort** in the toolbar/settings.

## Performance on long chats
UI Toolkit is retained-mode, so unchanged messages aren't re-laid-out every frame (the old IMGUI
re-parse-every-frame cost is gone). Markdown is parsed once per message, the autoscroll is
coalesced to one pending scroll, and the transcript is capped (~600 elements, oldest trimmed).

## Files
- `Sims4ClaudeChatWindow.cs` — the EditorWindow (UI Toolkit `CreateGUI`): toolbar, budget bar,
  transcript, messages, collapsible tools, streaming, settings, history, autoscroll.
- `ClaudeMarkdownRenderer.cs` — builds a `VisualElement` tree from markdown (blocks + inline).
- `Sims4ClaudeChat.uss` — the Forge stylesheet (palette tokens, components, hover/focus, dark+light).
  Loaded at runtime via `AssetDatabase.FindAssets("Sims4ClaudeChat t:StyleSheet")`.
- `ClaudeCliDriver.cs` — spawns `claude` headless, one process per turn, feeds the prompt over
  stdin, reads stdout as NDJSON on background threads, surfaces events on a thread-safe queue.
- `ClaudeJson.cs` — tiny dependency-free JSON parser for the stream events.

## How it stays on your subscription (and ToS-compliant)
- It **invokes the actual `claude` binary** (never reads/reuses your OAuth token — token-scraping
  is the thing Anthropic bans and enforces).
- Before spawning, it **clears `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, and the cloud-provider
  flags** from the child environment so usage can't silently fall back to metered API/cloud billing.
  (`CLAUDE_CODE_OAUTH_TOKEN` is left intact — it's subscription-funded.) Toggle: "force subscription".

## Settings
- **claude path** — auto-detected via `where claude`; or Browse/Detect. `.cmd` shims are run via `cmd.exe`.
- **working dir** — defaults to the git repo root, so Claude can edit both the exporter (`tools/`)
  and the Unity scripts. Narrow it if you want.
- **permissions** —
  - `bypassPermissions` (default): full access, no prompts. Fine on your own repo under git.
  - `acceptEdits`: auto-approves edits, but **aborts the turn** if Claude hits a command it can't
    auto-approve (headless can't prompt) — so it's less reliable for an agent that runs commands.
  - `plan`: read-only.
  - `default`: blocks anything needing approval (limited).
- **extra args** — appended verbatim, e.g. `--model claude-opus-4-8`.

## Known caveat to smoke-test
Multi-turn context uses `--resume <session_id>` with the prompt piped on **stdin**. That exact
combination isn't explicitly documented. If follow-up turns lose context, the fix is to pass the
prompt as a command-line argument instead of stdin when `--resume` is present (in
`ClaudeCliDriver.BuildCommand` / the stdin-writer in `TryStartTurn`).

## Not yet (possible later)
- Real diff rendering for edits (tool calls show as one-line summaries in the foldout).
- Clickable links (currently accent-colored text, not navigable).
- A persistent "thinking…" indicator across multi-step turns (currently single-shot per turn;
  the toolbar "● working…" status covers ongoing activity).
- "Send selection to Claude" context action.
