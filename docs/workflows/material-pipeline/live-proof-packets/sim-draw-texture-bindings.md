# Live proof: Sim draw-call texture bindings (RenderDoc, yafem_amale_table.rdc)

Date: 2026-06-12. Source capture: `satellites/ts4-dx11-introspection/ender_doc/yafem_amale_table.rdc`
(291 MB, live scene: YA female + adult male at a table). Mined with RenderDoc 1.43
(`qrenderdoc --python`, script preserved at `c:\tmp\s4probe\mine_rdc.py`; extraction JSON at
`c:\tmp\s4probe\rdc-mine\draw-bindings.json`; texture PNGs in the same folder).

Evidence level: **live-game GPU capture** — the highest trust layer in the source map.

## The sim pixel-shader binds 9 SRV slots

Observed identically (modulo per-part slot 4) across the sim body/part draws
(eventIds 569, 580, 602, 617; skinned meshes 3.4k–10.9k indices with
`POSITION/NORMAL/TEXCOORD0/TEXCOORD1/BLENDINDICES/BLENDWEIGHT/TANGENT/COLOR1/POSITION1/NORMAL1`
vertex layout — i.e. tangents for normal mapping and GPU morph targets are part of the
vertex interface):

| Slot | Resource (run) | Size / format | Identified role (visual inspection) |
|---|---|---|---|
| 0 | 87754 (runtime) | 1024×2048 B8G8R8A8 | **Skin DETAIL composite — grayscale.** Fully shaded anatomy (muscles, face, ears) in monochrome; eye whites at the atlas eye spots. |
| 1 | 87756 (runtime) | 1024×2048 B8G8R8A8 | **Skin COLOR composite.** Soft per-tone color (the v12 LRLE tone-texture content, composited per Sim). No anatomy detail. |
| 2 | 87761 (runtime) | 1024×2048 BC3 | **Outfit diffuse composite.** Hair + brows/lashes + shirt + bottoms + shoes, all in body-UV space, alpha-masked; layered over skin in-shader. |
| 3 | 87758 (runtime) | 256×1 B8G8R8A8 | **Tone/shift ramp.** Near-identity grayscale ramp with green-biased midtones (x=128 → R118 G140 B114). The skintone-shift / tone-curve mechanism. |
| 4 | 84074 / 84048 (per part) | 1024×1024 BC3 | **Per-part normal map** (DXT5nm-style; garment seams/wrinkles visible). Absent on some draws (e.g. eid 617). |
| 5 | 87763 (runtime) | 512×1024 BC3 | **Specular / material-mask composite** (channel-packed; hair strands + garment regions). |
| 6 | 23038 (global) | 128×128 BC1 **array** | Global detail/noise tile array. |
| 7 | 23028 (global) | 64×128 BC3 | Small global LUT. |
| 8 | 187 (global) | 1×1 B8G8R8A8 | Dummy/white. |

A second draw family (eids 522–552, up to 18.5k indices) binds the 2048×6144 shadow map +
small R8G8 maps + a shared BC1 1024² atlas — the prop/table family, distinct from the sim
shader.

## Architectural conclusions

1. **The game does NOT pre-bake one diffuse.** Skin detail (grayscale), skin color, outfit
   diffuse, specular masks, and the tone ramp are bound as SEPARATE composited textures
   and combined per-pixel in the shader. The runtime compositor produces per-sim texture
   sets (high resource ids created late in the stream).
2. **Skin color = grayscale detail modulated by a color layer (+ ramp).** This vindicates
   the SkinBlender-chain structure (detail × tone-color) as the right offline
   approximation: pre-baking `combine(detail_gray, tone_color)` into one diffuse is the
   CPU equivalent of slots 0+1.
3. **Brows/lashes/eyes live in the OUTFIT composite**, not the skin composite — equipping
   EyeColor/Brow parts and compositing their diffuses (build 0313) mirrors the real
   pipeline.
4. **Normal maps are per-PART 1024×1024 BC3** (CASP `normalMapKey`), with TANGENT in the
   vertex layout. Our viewer must resolve and bind part normal maps (and verify whether
   the nude body parts carry one).
5. **Specular is a real composited input** (512×1024) — our pipeline currently ignores
   RLES/specular textures entirely.
6. **Exact combine math is extractable**: the pixel shader disassembly for eid 602 and
   the constant buffers (light rig values for calibration) are in the same capture —
   queued as the next mining pass.

## Follow-ups

- [ ] Disassemble the sim pixel shader (eid 602) → exact detail×color×ramp math, clothing
      blend, specular model.
- [ ] Dump sim draw constant buffers → in-game light rig values for viewport calibration (A8).
- [ ] Probe nude-part CASPs for normalMapKey/specularMapKey presence; wire part normal +
      specular maps into the viewer materials.
- [ ] Identify the second sim's texture cluster in the same capture (yafem) for a female
      reference set.
