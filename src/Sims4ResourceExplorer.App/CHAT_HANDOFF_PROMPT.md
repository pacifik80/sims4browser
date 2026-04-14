# Sims4 Browser Handoff Prompt

Use this prompt at the start of a new chat when continuing work on this
project.

```text
You are continuing work on the Sims4 Browser project in:

c:\Users\stani\PROJECTS\Sims4Browser

Read first:
1. src/Sims4ResourceExplorer.App/AGENTS.md
2. src/Sims4ResourceExplorer.App/Sims4ResourceExplorer.App.csproj
3. src/Sims4ResourceExplorer.App/MainWindow.xaml
4. src/Sims4ResourceExplorer.App/MainWindow.xaml.cs
5. src/Sims4ResourceExplorer.App/ViewModels/MainViewModel.cs

Important project rules:
- Every new user-facing verification build must increment <BuildNumber> in
  Sims4ResourceExplorer.App.csproj.
- The same build id must appear in both the window title and the diagnostics
  `Build: ...` line.
- If code changes are meant to be verified by launching the app, always:
  - increment the build number
  - clean/build the app so `run.ps1` can be launched immediately
  - tell the user the exact build id they should see after launch
- Do not reduce or disable user-visible functionality for performance without
  discussing it first.
- Be honest about heuristics. Do not present approximations as if they were
  fully decoded shader logic.

Current preview model direction:
- `RawUv` = raw mesh UV only.
- `MaterialUv` = only confirmed material UV transform; if transform is not
  decoded, it should effectively match `RawUv` instead of inventing a nicer
  overlay.
- `FlatTexture` / `LitTexture` should support `All` or a selected texture slot.
- `MaterialUv` should be slot-specific.

Current known architecture goals:
- Preview needs to move toward explicit selection of:
  - material
  - slot
  - variant/state
- Avoid silently choosing one "best" path when diagnostics would benefit from
  explicit user control.

Current known cautions:
- UV/material debugging has been harmed by display-only heuristics before.
- If a fix is heuristic, say so clearly.
- `LitTexture` is still a viewport approximation, not the full game shader
  runtime.
- Be careful with lighting/specular/emissive choices that make matte assets
  look metallic, foggy, or washed out.

Current recent feature state:
- Build numbers have progressed through build-0048.
- Preview UI already has:
  - `RawUv`
  - `MaterialUv`
  - slot selector
  - diagnostics tabs
  - progress line under preview
- `run.ps1` was previously fixed for output-folder locking / stale processes.

How to work:
- Start by summarizing the current user request.
- Inspect the relevant code before proposing changes.
- Prefer general, architecture-consistent fixes over per-asset hacks.
- After edits, build the App project in Release x64 and report the exact new
  build id.
```

## Optional Short Version

```text
Continue Sims4 Browser in c:\Users\stani\PROJECTS\Sims4Browser.
Read AGENTS.md and the App csproj first.
Preserve the rule that every verification build increments BuildNumber and the
same build id appears in title + diagnostics.
Treat RawUv as raw mesh UV, MaterialUv as confirmed material transform only,
and do not fake shader logic with unmarked heuristics.
Prefer general fixes over per-asset hacks.
Then continue with the current task.
```
