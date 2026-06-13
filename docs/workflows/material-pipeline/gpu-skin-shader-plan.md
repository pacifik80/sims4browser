# GPU Skin Compositing — Implementation Plan (design workflow wf_e100c2ec, 2026-06-14)

> Verdict: HelixToolkit.WinUI.SharpDX 3.1.2 fully supports custom pixel shaders as a
> first-class, documented extension point (EffectsManager.AddTechnique + GenericMeshMaterialCore,
> SetTexture/SetProperty by reflected name, runtime HLSL compile via bundled SharpDX.D3DCompiler).
> No rewrite needed. Full research (5 findings) in the workflow transcript.

The findings are accurate against the live code. Note that `Render` receives the `viewport` (which holds `EffectsManager`) but not the `effectsManager` directly вЂ” that's a wiring detail for the plan. I have everything needed.

# GPU Skin Compositing вЂ” Build-Ready Implementation Plan

## 1. Chosen approach

**APPROACH A вЂ” custom surface technique that replaces the skin PBR pixel shader.** Register a custom `TechniqueDescription` on the `DefaultEffectsManager`, reusing the stock mesh VS + input layout, swapping only the pixel shader; bind it via a `GenericMeshMaterialCore` that exposes per-tone layer SRVs + a `SkinParams` constant buffer the sliders write into.

**Why A, not B (decisive):**
- Finding 1 proves A is a first-class, documented extension point in this exact package: `EffectsManager.AddTechnique(TechniqueDescription)` + `GenericMeshMaterialCore(shaderPass, cbName)` with `SetProperty(name,вЂ¦)` / `SetTexture(name, stream)` mapping by reflected name. All types live in the core `HelixToolkit.SharpDX.dll` that the WinUI package re-references вЂ” no WinUI blockers, no XML-duplication gap.
- Finding 3 shows B's "keep stock PBRMaterial, just feed it a GPU texture" premise **does not hold**: `PBRMaterial.AlbedoMap` only accepts a `TextureModel`, and `TextureModel`/`TextureInfo` carry **CPU data only** вЂ” there is no overload to bind an app-owned `ShaderResourceView`. To make the stock material sample an RT you must either subclass `PBRMaterialVariable.OnBindMaterialTextures` (same internal-API surface as A) **or** do a GPUв†’CPUв†’GPU readback per slider (defeats "instant"). So B is *more* plumbing (RT lifecycle, resize, pass-ordering) for *equal-or-worse* risk.
- A solves compositing **and** sampling in one place (the shader the material already runs): no extra render target, no readback, no pass-ordering hazard. Slider change = write one constant + `InvalidateRender()`.

**Fallback (only if A's reflection-binding of a custom cbuffer proves unworkable at runtime):** B in its option-3c form вЂ” GPU composite into an RT, `CopyResource`+`Map` back to `byte[]`, feed a normal `TextureModel`. This keeps the stock `PBRMaterial` untouched and is strictly a drop-in for the current CPU bake (one GPU pass replaces the CPU compose). Acceptable for discrete slider commits, not live drag. We do **not** start here; we keep it documented as the escape hatch.

**Runtime vs precompiled HLSL:** compile HLSL в†’ bytecode **at runtime** via the bundled `SharpDX.D3DCompiler.ShaderBytecode.Compile(...)` (hard dependency per Finding 1). No `fxc`/build-step needed; embed the `.hlsl` as a resource, compile on first use, feed `byte[]` + `new ShaderReflector()` into `ShaderDescription`. This avoids a native shader-build toolchain in the WinUI project.

---

## 2. Architecture

### 2.1 Textures uploaded ONCE per skintone (bound as named SRVs on the custom material)
Sourced from `SimSkintoneRenderSummary` (Domain.cs:907-943), bound by HLSL variable name via `GenericMeshMaterialCore.SetTexture(name, stream)`. Use free registers (Finding 2: `t6вЂ“t19` free in mesh path; Finding 1: avoid reserved `t0вЂ“t5`, `t20/21`, `t30+`).

| HLSL var (register) | Source field | Notes |
|---|---|---|
| `texSkinColor` (t6) | `BaseTexturePngBytes` *(or `TryExtractFullBodyDiffuse`)* | C, UV0, **UNORM** |
| `texDetailNeutral` (t7) | `DetailNeutralPngBytes` | D0, UV0, UNORM |
| `texDetailOverlay` (t8) | `DetailOverlayPngBytes` | UV0 |
| `texPhysiqueDetail0..3` (t9вЂ“t12) | `PhysiqueDetailPngBytes[0..3]` | heavy/fit/lean/bony |
| `texPhysiqueOverlay0..3` (t13вЂ“t16) | `PhysiqueOverlayPngBytes[0..3]` | |
| `texFaceOverlay` (t17) | `FaceOverlayPngBytes` | UV0 |
| `texFaceCas0..N` (t18вЂ¦) | `FaceCasOverlayPngBytes[]` | equipped CAS overlays |

All 8 physique rows uploaded unconditionally (weights gate them in-shader), matching `BuildAsync`'s "fetched once regardless of weight" contract. **Bind these as UNORM (non-sRGB) views** (Finding 5 gotcha a): the math runs in gamma/byte space exactly as the CPU compositor does. The custom sampler is a plain linear-clamp `samplerSurface` (reuse stock s0) вЂ” no sRGB decode.

Normal map: **derived in-shader** from the detail height D (Finding 5 В§3.5), so `DeriveNormalMapPngAsync` is no longer on the per-slider path. No normal SRV uploaded for nude parts.

### 2.2 Constant-buffer layout `cbSkinParams : register(b10)`
Brand-new buffer at a free register (Finding 1: b10 free; b0вЂ“b6 reserved). The PS also declares `cbTransforms:b0` and `cbLights:b3` with the stock struct layouts (Finding 2) so it gets view/proj/eyePos and lights bound for free вЂ” **but** the captured skin rig (2 dir lights + rim) is self-contained in `cbSkinParams`, so we drive lighting from our own constants and only use `cbTransforms.vEyePos`/matrices from the stock buffers. Fields (Finding 5 В§2), all written via `SetProperty(name, value)`:

```
float4 physiqueWeights;        // [heavy,fit,lean,bony]
float  detailNeutralAlpha, detailOverlayAlpha, pass2Opacity, skintoneShift;
float  faceOverlayAlpha; int faceCasCount;
float4 faceCasAlphas[ (N+3)/4 ];
float  rampMix;                // FIXED 0.5
// captured light rig:
float3 lightDir0, lightDir1, lightColor0, lightColor1, rimColorGround, rimColorSky;
float  normalStrength; float2 specIntensity, rimExponent, ambientBand;
float3 sunSpecDir; float sunSpecPower;
float3 tintTarget, fresnelTintRGB; float tintAmount, tintStrength;
float  specGlossScale, specGlossBias;
float2 detailTexSize;          // (1/1024, 1/2048) for В§3.5 gradient
```

### 2.3 Where the material/technique is created
- **Technique registration:** one-time, in a new `SimSkinTechnique` static registrar called when the `DefaultEffectsManager` is constructed (`SimConstructorWindow.xaml.cs:23`). It calls `effectsManager.AddTechnique(...)` once (guard with `HasTechnique`). The renderer needs access to the manager вЂ” today `Render` gets it via `viewport.EffectsManager`, so the registrar can run off that.
- **Material creation:** at the existing seam `SceneViewportRenderer.CreateMaterial` **lines 681-697**. Replace `return new PBRMaterial{вЂ¦}` with construction of a `Material` whose `Core` is a `GenericMeshMaterialCore(pass, "cbSkinParams")`, where `pass = effectsManager.GetTechnique("SimSkinComposite").GetPass(DefaultPassNames.Default)`. Keep the **route gate at 660-668 verbatim** (the `skin_atlas.png` filename identifier is load-bearing вЂ” it routes both head and body shells down this path).

### 2.4 Head + body shells
Both skin shells carry the same texture set + constants today (binder unifies under `skin_atlas.png`). `Render` calls `CreateMaterial` per mesh (one `Material` per `MeshGeometryModel3D`), so each skin mesh gets its **own** material instance, but all share the **same SRV set + same `cbSkinParams` values** (upload the layer streams once into a cache keyed by skintone; reuse for every skin-routed mesh). Non-skin meshes (SimGlass eyes, StandardSurface, Phong fallback) are untouched вЂ” only the 681-697 branch changes.

### 2.5 Slider в†’ constant в†’ redraw flow
- **Scalar sliders** (`BaseSkinAlpha`вЂ¦`BlushAlpha`, the 6 CAS alphas, `SkintoneShift`, 4 physique weights вЂ” SimConstructorViewModel.cs:312-329): no longer call `RebuildSkinLayers()`. Instead call a new `UpdateSkinConstants()` that writes the **live material core's** properties (`material.Core.SetProperty("pass2Opacity", k)` etc.) and requests a redraw. Because `GenericMaterialVariable` re-uploads the cbuffer from the dict each draw, the new value is picked up with no PNG decode, no `Items.Clear()`, no scene rebuild.
- **Redraw trigger:** `sceneViewport.InvalidateRender()` (HelixToolkit raises invalidate; the same GPU SRVs are sampled вЂ” next frame reflects the new constant).
- **Texture-changing inputs** (skintone swatch change; face-CAS **picker** change that swaps *which* overlay is bound) still re-upload SRVs through the existing path вЂ” only *scalar* sliders become constant-only.
- **Cache key:** drop `LayersFingerprint` from `SceneCacheKey` for the scalar set so slider changes don't perturb scene identity / trigger a full `Render`.
- **Window-level bypass:** `ViewModel_PropertyChanged` / `UpdateViewport` (xaml.cs:82-117) must NOT run full `Render()` (which does `Items.Clear()` + full decode) on constant-only changes. Add a distinct VM signal (e.g. `SkinConstantsChanged` event) the window handles by calling `sceneViewport.InvalidateRender()` directly, keeping the existing `CurrentScene`-change path for geometry/texture-set changes.

---

## 3. Step-by-step implementation checklist (dependency order)

### Step 1 вЂ” HLSL pixel shader `psSimSkinComposite.hlsl` (new)
**Create:** `src/Sims4ResourceExplorer.App/Shaders/psSimSkinComposite.hlsl` (embedded resource).
- Declare `PSInput` **identical** to Finding 2 В§1 (same order/semantics) so it's byte-compatible with stock `vsMeshDefault`.
- Declare `cbTransforms:b0`, `cbLights:b3` with stock layouts (for eyePos/matrices); declare `cbSkinParams:b10` per В§2.2.
- Declare SRVs `texSkinColor:t6 вЂ¦ texFaceCasN`, sampler `samplerSurface:s0`.
- Body: transcribe Finding 5 В§3.1 (build D, hole-guard `D=0.5` when coverage <0.03), В§3.2 (HSV-Value shift), В§3.3 (identity-ramp form `base=C*(1+D)/2`, double `overlayBlend`, `lerp(O1,O2,pass2Opacity)`, outfit + face/CAS alpha-over), В§3.5 (texture-space central-difference normal from D, gutter guard 0.04), В§3.6 lighting (2-light + rim + AO band; tint stage with `tintAmount/tintStrength` initially **0** to disable until proven).
- **Milestone-1 variant first** (see В§5): a trivial body that just `return float4(texSkinColor.Sample(samplerSurface, input.t).rgb, 1)`.
- **Verify:** `SharpDX.D3DCompiler.ShaderBytecode.Compile(src,"main","ps_5_0")` returns no errors (`result.Message` empty); assert `result.Bytecode` non-null in a unit test.

### Step 2 вЂ” Technique registrar `SimSkinTechnique.cs` (new)
**Create:** `src/Sims4ResourceExplorer.App/Services/SimSkinTechnique.cs`.
- `static void EnsureRegistered(IEffectsManager mgr)`: if `!mgr.HasTechnique("SimSkinComposite")`, compile the embedded HLSL, build `TechniqueDescription` reusing `DefaultInputLayout.VSInput` + `DefaultVSShaderDescriptions.VSMeshDefault`, one `ShaderPassDescription(DefaultPassNames.Default)` with `[VSMeshDefault, new ShaderDescription("PSSimSkin", ShaderStage.Pixel, new ShaderReflector(), psBytecode)]`, then `mgr.AddTechnique(...)`.
- **Verify:** after calling, `mgr.HasTechnique("SimSkinComposite")` is true and `GetTechnique(...).GetPass(DefaultPassNames.Default)` is non-null (log in a debug run).

### Step 3 вЂ” Material factory `SimSkinCompositeMaterialFactory.cs` (new)
**Create:** builds a `Material` from a `GenericMeshMaterialCore(pass, "cbSkinParams")`.
- `SetTexture("texSkinColor", stream)` вЂ¦ for every layer; `SetSampler("samplerSurface", linearClampDesc)`.
- `SetProperty(...)` for every `cbSkinParams` field from the summary scalars + the captured rig constants (rig values hardcoded from Finding 5 В§2).
- Cache the per-skintone layer `Stream`/SRV set keyed by skintone id; reuse across head+body.
- Expose the created `MaterialCore` back to the VM (via the returned scene/material registry) so sliders can mutate it.
- **Verify:** `core.TextureNames`/`PropertieNames` introspection lists every name we set (proves reflection matched the HLSL).

### Step 4 вЂ” Wire the seam `SceneViewportRenderer.CreateMaterial` (modify 681-697)
- Add `IEffectsManager` access into the create path. `Render` already has `viewport`; call `SimSkinTechnique.EnsureRegistered(viewport.EffectsManager)` once at the top of `Render`, and thread the manager (or the resolved skin `ShaderPass`) into `CreateMaterial`.
- Replace `return new PBRMaterial{вЂ¦}` with `return SimSkinCompositeMaterialFactory.Create(pass, layerStreams, scalars, rig)`. Keep route gate 660-668 and the `skin_atlas.png`/`IsSimSkinFamily` predicate verbatim.
- **Verify:** build + run; skin mesh renders through the custom technique (Milestone-1 PS shows flat base color), non-skin meshes unchanged.

### Step 5 вЂ” Binder change `SimSkintoneMaterialBinder.RewriteOne` (modify :46-145)
- Stop collapsing layers into one `skin_atlas.png` BaseColor. Attach the **layer PNG set + scalar weights** to the routed `CanonicalMaterial` (preserve the `"Sim skintone route"` tag and the `skin_atlas.png` identifier so the renderer gate still fires). The `BuildAsync` / `DeriveNormalMapPngAsync` calls become **unused on this path**.
- **Verify:** routed material now carries N distinct layer textures (assert `material.Textures.Count` matches expected) and still trips `isApproximateCasSkintoneTarget`.

### Step 6 вЂ” VM constant-only path `SimConstructorViewModel` (modify :312-329, :738-750)
- Scalar slider setters call new `UpdateSkinConstants()` в†’ mutate the live material core's `SetProperty(...)` + raise `SkinConstantsChanged`. **Not** `RebuildForSkintoneChange`.
- Drop `LayersFingerprint` from the scalar `SceneCacheKey` contribution (:76).
- Skintone swatch + face-CAS **picker** changes keep the re-upload path.
- **Verify:** dragging a scalar slider fires `SkinConstantsChanged` (not `CurrentScene` change); no `BuildAsync` call in the profiler.

### Step 7 вЂ” Window redraw bypass `SimConstructorWindow.xaml.cs` (modify :82-117)
- Subscribe to `SkinConstantsChanged`; handler calls `sceneViewport.InvalidateRender()` only (no `Render`/`Items.Clear()`). Keep `CurrentScene`/`SelectedRenderMode` в†’ full `UpdateViewport()`.
- **Verify:** slider drag updates the viewport with no flicker/reframe; frame time drops to a redraw (no decode spike).

### Retired vs kept
- **Retired from the per-slider hot path:** `SimSkinAtlasComposer.BuildAsync` / `BuildAtlasFromPreRenderedBaseAsync` recompose, `DeriveNormalMapPngAsync`, `RebindWithAtlas`. They no longer run on scalar slider changes.
- **Kept:** the entire `SimSkinAtlasComposer` + `DeriveNormalMapPngAsync` for **FBX/texture export** (export needs a baked atlas PNG) and as the **CPU oracle** to diff against GPU output during validation. Also kept as the **B fallback** compositor. Do not delete; just remove it from the live slider trigger.

---

## 4. Risks & unknowns (only compile+run validates these)

1. **Reflection binding of a brand-new `cbSkinParams:b10`** вЂ” Finding 1 confirms `GenericMaterialCore` maps `SetProperty` by reflected field name, and the ctor takes the material cbuffer name. Unknown: whether a *non-stock* cbuffer name (`"cbSkinParams"`) at a free register is fully discovered by the toolkit's reflector for a `GenericMeshMaterialCore`, or whether it only wires the buffer it was constructed with. **Mitigation:** Milestone 1 binds exactly one scalar to prove write-through before adding 30 fields. If b10 isn't reflected, fall back to reusing the stock `cbMesh:b1` field names for our scalars (Finding 1 explicitly supports passing the stock material cbuffer name).
2. **Gamma/color-space** (Finding 5 gotcha a) вЂ” if HelixToolkit forces sRGB views on our SRVs, the albedo chain washes out. **Mitigation:** explicitly request UNORM `TextureModel`s / verify the bound view format at runtime; compare a single pixel against the CPU oracle.
3. **`SetProperty` is PS-stage only** (Finding 1 В§WinUI gotcha) вЂ” fine, our buffer is PS-only.
4. **Per-draw cbuffer re-upload latency / dirty-flag** вЂ” unknown whether mutating `core.SetProperty` after first draw re-uploads automatically or needs an invalidate. **Mitigation:** Step 6/7 always pair the mutation with `InvalidateRender()`.
5. **Tangent frame for the derived normal** вЂ” `EnableAutoTangent` was on the stock path; the custom PS uses `input.t1/t2`. If the geometry lacks tangents, normals read flat. **Mitigation:** keep the texture-space (not screen-space) gradient and verify tangents exist in the GEOM; gutter guard prevents seam embossing.
6. **Shadow/`cbLights` interaction** вЂ” we drive lighting from our own rig, but the viewport still has `ShadowMap3D` + directional lights (xaml.cs:24, renderer :90-100). Unknown interplay between our self-lit PS and the shadow pass. **Mitigation:** Milestone-2 lighting first matches the captured rig with shadows off, then re-enable.

---

## 5. Milestone 1 вЂ” proof-of-plumbing (do this first)

**Goal:** prove a custom GPU technique renders the skin mesh at all, end-to-end, before any equation.

**Minimal change set:**
1. `psSimSkinComposite.hlsl` with the stock `PSInput`, `cbSkinParams:b10` containing a **single** `float4 debugTint;`, body = `return texSkinColor.Sample(samplerSurface, input.t) * debugTint;` (just C Г— a constant).
2. `SimSkinTechnique.EnsureRegistered` вЂ” compile + `AddTechnique`.
3. `SimSkinCompositeMaterialFactory.Create` вЂ” bind only `texSkinColor` (the existing base/atlas PNG stream) + `SetProperty("debugTint", new Vector4(1,1,1,1))`.
4. Seam swap at `SceneViewportRenderer.cs:681-697` to return this material.

**Pass criteria:**
- App runs; the sim's skin shells render (textured, not magenta/black) through `"SimSkinComposite"`, non-skin meshes unchanged.
- Set `debugTint` to `(1,0,0,1)` from a temporary control в†’ skin turns red **without** any PNG re-decode or `Items.Clear()` (proves: technique registered, SRV bound by name, cbuffer write-through + `InvalidateRender` redraw вЂ” the entire instant-slider mechanism).

Once green, **Milestone 2** = add В§3.1вЂ“3.3 albedo + В§3.5 derived normal (compare to CPU oracle pixel-for-pixel), **Milestone 3** = В§3.6 captured lighting rig, **Milestone 4** = migrate the real sliders (Steps 5вЂ“7). If Milestone 1's cbuffer write-through fails, switch the debug field into the stock `cbMesh:b1` per Risk 1 before proceeding.

---

**Key file anchors for the implementer:** seam = `SceneViewportRenderer.cs:660-697` (route gate + PBR branch, both verified present); manager = `SimConstructorWindow.xaml.cs:23` (`DefaultEffectsManager`), reachable in `Render` via `viewport.EffectsManager`; binder = `SimSkintoneMaterialBinder.cs:46-145`; equation source of truth (oracle + export) = `SimSkinAtlasComposer.cs` (`BuildAsync` :196-216, `HsvValueShiftInPlace` :647, `DeriveNormalMapPngAsync` :572-637); shader math spec = Finding 5 В§3; captured rig constants = Finding 5 В§2 / `c:\tmp\s4probe\rdc-mine\eid602_ps_cbuffers.txt`.