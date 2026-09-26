# Sims4Creator — Unity HDRP stub

A **scaffolding stub** for the Unity-side character-creator/game. No migration and
no asset code yet — just enough to open, compile, and prove the toolchain. We then
decide what to implement **one slice at a time**.

- **Engine:** Unity **6000.3.8f1** (Unity 6.3), **HDRP** (`com.unity.render-pipelines.high-definition`).
- **Why Unity:** the rendering pain (custom D3D11/Helix skin pipeline) goes away — Unity
  gives us SSS skin, blend shapes, and modular skinned meshes for free. The hard part
  (parsing Sims 4 formats) is already done in the existing .NET libraries and is **reused**, not rewritten.

## Open it

1. Unity Hub ▸ **Add** ▸ select this folder (`unity/Sims4Creator`).
2. Open with **6000.3.8f1**. First import resolves HDRP (may take a few minutes).
3. **HDRP setup (one time):** `Window ▸ Rendering ▸ HDRP Wizard` ▸ **Fix All** —
   this creates and assigns the HDRP pipeline asset + graphics settings.
   (Hand-authoring those YAML assets is fragile, so we let the Wizard do it.)
4. Sanity check: `File ▸ New Scene ▸ Basic (HDRP)`, save as `Assets/Scenes/Main.unity`,
   add an empty GameObject with the **StubBootstrap** component, press Play — the
   Console should show `[Sims4Creator] Runtime stub alive`. Also try the
   **Sims4 Creator ▸ About Stub** menu to confirm the Editor assembly.

> If Package Manager flags the HDRP version, that's the single line to bump in
> `Packages/manifest.json` — the editor normally auto-resolves it to its bundled version.

## Layout

```
Assets/
  Scripts/
    Runtime/   Sims4Creator.Runtime.asmdef  + StubBootstrap.cs   (runtime code)
    Editor/    Sims4Creator.Editor.asmdef   + StubMenu.cs        (importer/UI lives here)
  Sims4/       import target for converted Sims 4 content (empty)
  Scenes/      your scenes (empty)
Packages/      manifest.json (HDRP + modules)
ProjectSettings/ ProjectVersion.txt
```

## What's next (decided 1-by-1)

The existing repo already produces engine-agnostic records — `CanonicalScene`,
`CanonicalMesh`, `CanonicalMaterial`, `CanonicalTexture` (Core layer, zero render deps).
The Unity importer's job is to convert those into `Mesh` / `Texture2D` / `Material` /
`SkinnedMeshRenderer` + blend shapes. We'll pick the first concrete slice together —
likely "load one `.package` and render a single static mesh" before touching skin/rig/clothing.
```
