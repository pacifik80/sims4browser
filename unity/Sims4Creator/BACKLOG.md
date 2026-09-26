# Sims4Creator — Backlog

Parked issues, in rough priority order. Each entry has enough context to resume cold.

> **Going forward, actionable work is tracked in `tickets/` (see `tickets/index.md`).** This file is
> the legacy parked-issues list; open items migrate into tickets as they're picked up. (Clothing #1
> already migrated → BUG-003.)

## Clothing

1. **Shirt + SweaterCrewBasic render glitches (long-sleeve tops)** — Shirt shows black square patches on torso/sleeves; Sweater shows partially invisible sleeves. NOT texture/composite (Python replay of `lerp(skin, fabric, alpha)` over skin_atlas is clean, 0% pure-black). Unity-side mesh issue on the sleeve tubes — suspect bad normals or double-sided back-face z-fighting. Short-sleeve tee from the same pipeline is fine. Next step: inspect those two meshes in Unity (recompute normals / try single-sided material / check winding).
2. ~~**Garment morphs**~~ — DONE 2026-07-08: ClothingExporter now bakes the full slider catalog (BGEO+DMap via BakeMorphsAsync) into per-garment blendshapes; face/neck morphs and body sliders deform worn clothes in lockstep with the body. (Was: sliders didn't deform clothes → jaw morphs stepped at the garment's baked neck.)
3. ~~**Layering**~~ — DONE for underwear + skin layers (2026-07-09/10): bra/panties composite as texture layers under outer garments; tights/socks/makeup composite into the live skin atlas. Remaining frontier: two MESH garments stacked on one region (e.g. jacket over shirt) — still one mesh garment per region.
4. **Alt-body fitting under clothes** — EA/Eve/BetterBody are different meshes; a worn garment imposes its authored (EA) exposed-skin shape. Skin texture stays correct (shared UV). Full support = fit/morph the garment to the alt body.
5. ~~**Socks (body_type 36)**~~ — DONE 2026-07-10: socks/tights are MESH-LESS skin-texture-layer parts (that's why the scene path gave a 1-tri mesh); exported via `ExportSkinLayerAsync`, composited into the skin atlas at runtime.
6. **Sheer/lace fabrics** — genuine transparency (SimGlass) needs a separate HDRP alpha-blend material ordered by SortLayer; currently everything renders opaque.
7. **Hem seam watch** — hem-clip keeps nude ~2cm under the fabric hem; verify no z-fight/shimmer on tight garments (cropped jeans, pencil skirt). If seen: reduce overlap or push nude verts slightly inward along normals.
8. ~~**Gloves — z-fight watch**~~ — MOOT 2026-07-10: EA gloves turned out to be painted-on-skin texture layers (their GEOM is a 3-vert placeholder — same signature as socks); rerouted to the skin-layer path (slot 11), so there is no overlay mesh to z-fight. A future MESH glove (thick CC gloves) would resurrect this.
9. **Accessory translucent alpha watch** — accessories run through the same skin-under-fabric composite; an accessory diffuse with translucent alpha + non-body-atlas UVs would lerp against the WRONG skin region. Current 8 items look opaque; check any new accessory with sheer parts (veils, lace gloves).
10. **Hats** — need hat-chop hair variant switching (the variant picker currently always selects the full-crown GEOM by bounding volume).
11. **Rings, scarves, tail-of-jewelry slots** — not yet classified in ClothingExporter.Classify (rings=body_types 16-19 region, scarves vary); add on demand.
12. **PARKED: custom physics cloth skirt** (dropped 2026-07-10 — Unity Cloth quality judged "very bad" even after fitted colliders / CCD / live tuning panel). Full code preserved in `Parked/cloth-skirt/` (builder, runtime tuner, X-ray collider visualizer, plaid textures); the covers[]-keyed IsLayeredUnder fix it motivated STAYS in production. If revived, prefer the skirt-bone chain approach (EA skirt physics bones + Verlet pendulum — stable, retrofits every EA skirt) or Magica Cloth 2 (paid) over built-in Cloth.
13. **Earring swing bones** (parked by user 2026-07-10) — synthetic dangle bone + damped pendulum script; design discussed in chat, nothing built.

## Hair / CAS

8. **More hair colours for the 6 new styles** — exported 8 of 18 EA colours each (bob/pixie/ponytail/bun/sleek/wedge); re-run `exporthairmany af_char 18 <styles>` to complete palettes.
9. **Preset save/load** — save a full CharacterDefinition (morphs, skin, hair, outfit) to JSON and restore it from the CAS panel.
10. **uGUI reskin of the IMGUI CAS panel** — cosmetic; IMGUI panel is functional.

## Notes

- Clothing pipeline architecture (decided + user-accepted for this iteration): segment replacement — the garment mesh includes its exposed skin, composited over the live skin atlas per-texel (`lerp(skin, fabric.rgb, fabric.a)`); nude regions hem-clipped below the covering garment. See memory `clothing-pipeline-2026-07` for the full history and why geometry-cutting/alpha-clip approaches were abandoned.

## Exporter perf regression (2026-07-12, from the colorMap7 parser fix) — DEFERRED
- Symptom: window/door exports take ~170s each (was ~5s); furniture unaffected (~5s). Root cause: the colorMap7 parser fix (`trustDeclaredResourceKey` in TryReadTextureReference) makes MORE materials carry AUTHORITATIVE (declared type==2) texture references. In BuildBuySceneBuildService.cs BuildMaterialInfoAsync (~line 1633), an authoritative reference that misses is RE-RESOLVED on every occurrence (`!hadCachedEntry || (cachedTexture is null && isAuthoritativeReference)`) — never cached as a confirmed miss + an extra GetTextureResourcesByKeyAsync at :1655. Windows have ~10 MTST variants × 4 materials, so a few bogus authoritative keys each get full local→companion→index resolution ×40. Also slows the WPF app's window preview.
- FIX (do with review): cache authoritative MISSES so a key is resolved at most once per model build (thread a HashSet<string> alongside textureCache, or a sentinel). The re-try was only meant to UPGRADE a heuristic-null to authoritative ONCE; after an authoritative resolution the null is deterministic — don't retry. Verify a window export drops back to ~5s and textures unchanged.
- WORKAROUND for now: re-export windows/doors in PARALLEL background (each own process), ~7min for 14 items.

## Home editor — status 2026-07-12
- DONE since M3: mitered wall graph (no overlaps/posts), tint-baked coverings, exact ModelCutout door/window shapes, grounded shadows, MULTI-LEVEL (4 levels, two-faced floor/ceiling, level switcher, v4 save), 15 room-furniture items (kitchen/bath/bedroom/living), PORTALS (passable openings: doorway/arch/wide).
- NEXT candidates: **stairs** (connect the levels), **see-inside/cutaway camera** (furnish interior rooms), real glass panes, un-paint walls, per-side wallpaper, tall-wall setting, per-level grid visual for upper floors, floor tiles casting shadows (ceilings currently leak light).
- Then: **real simulation** (the user's stated end goal — a Sim living in the built room).

## Home editor (M2.5 feedback round, 2026-07-10)

14. **M3: doors/windows attach to walls + cut openings** (user: "right now windows and doors are placed as furniture") — snap to wall runs, per-segment opening mesh variants (door: lintel-only; window: sill+lintel), object oriented to wall. CutoutInfoTable available for exact shapes later.
15. **More doors + windows in catalog** (user: "too few to see how it works") — batch-export 6-8 of each; prefer base-game 1-tile doors.
16. **Multi-level support** (user request) — per-level wall/floor/object sets, level switcher, hide-above-current; upper floors need lower support in the game, free-form for v1. After M3.
17. **Wall polish**: un-paint walls (reset to default), per-SIDE wallpaper, walls blocking furniture placement, curated patterned covering set (skip solid-paint entries; current set has 3 solid walls + 6 solid floors).
19. ~~**Node-post polish**~~ — SUPERSEDED TWICE; final state 2026-07-12 (wall v3 MITER ENGINE): no posts at all — every junction is a true mitered seam (t = h/tan(Δ/2) per adjacent pair), 3+-wall nodes close around a cap polygon wearing the majority covering, and top faces tile exactly (the coplanar-shimmer class is gone). Coverings are tint-baked in the exporter (game formula pattern×tint×2).
20. **Tall-wall support**: wall height fixed at 2.8m; the 2-tile Merchant door (3.49m, EA's own cutout confirms 3.455) is builder-excluded with a warning. TS4 has 3 wall heights — making wallHeight a per-lot setting re-admits tall items.
21. **Wall drawn into a doorway** (rare): nothing stops drawing a wall (esp. a diagonal, which spawns a joint post) INTO a node inside an existing door span — the post would stand in the opening. Placement validation rejects new items over posts, but not new walls into existing openings.
22. **Real glass (SimGlass)**: window/door GLASS MESHES are skipped at export — "vertex format could not be resolved" (recurring mesh hashes 0xCD6E88DC/0xBB8C8112 across windows are the panes, not just shadow proxies). Windows currently read as open holes (user noticed "transparency"). Fix = decode that vertex format in the Assets lib + emit as HDRP transparent material (ties into CAS backlog #6 sheer/SimGlass).
23. **window_glass re-export blocked**: its AssetSummary isn't resolvable by Model TGI (index gap); MTL was hand-fixed instead. If it ever needs re-export, find its real OBJD first (it is NOT 'Mirror Mirror? Window Pane').
18. **Furniture swatch coverage**: swatch system shipped for primary-texture swaps; multi-texture items (bed) only swap the primary — full multi-material swatches need per-swatch texture SETS from the exporter.
