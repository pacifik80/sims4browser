# Карта источников и trust levels

Назначение: чтобы Codex не путал официальный материал, primary community reference и просто полезный tooling.

## Trust levels

### Level A — primary references для package/resource work
Использовать в первую очередь.

1. **The Sims 4 Modders Reference**
   - DBPF format
   - Internal compression
   - File Types
   - Resource Type Index
   - STBL format

   Это лучший общий reference hub для формата и high-level ролей ресурсов.

   Особенно полезно для текущей общей material/texture задачи:
   - Resource Type Index
   - File Types

2. **LlamaLogic.Packages**
   - современный .NET API docs
   - хорошие remarks про lazy loading, names, thread safety, decompression
   - практический reference для package access layer

### Level B — format archaeology / chunk-level reverse engineering
Использовать для chunk-level parsing и scene reconstruction.

3. **Mod The Sims / SimsWiki**
   - RCOL
   - MODL
   - GEOM
   - assorted format pages

4. **Llama-Logic/Binary-Templates**
   - 010 Editor templates для изучения бинарных форматов
   - хорошо подходит для unknown/partial formats

### Level C — reference implementations / legacy tools
Использовать как source of ideas, но осторожно.

5. **s4pe / s4pi / Sims4Tools**
   - старый, но полезный reference-код
   - community standard в течение долгого времени
   - не должен автоматически считаться source of truth

6. **dbpf_reader**
   - компактный low-level reference reader
   - полезен для проверки DBPF assumptions

### Level D — problem-specific tooling
Использовать только по своей domain-задаче.

7. **TS4 SimRipper**
   - лучший reference для full-sim assembly, save parsing, morph application
   - не нужен как primary source для raw package browsing
   - очень важен для full character/export задач

### Level D.5 — community creator knowledge
Использовать как practical guidance, но не как binary spec.

8. **Sims 4 Studio forum / release notes**
   - полезно для CAS atlas practice, `uv_1`, `ColorShiftMask`, creator-visible overlap failures
   - хорошо показывает реальные invariants и failure modes
   - не должен подменять format spec

9. **Creator forum posts on Mod The Sims**
   - полезны как bridge между file formats и observed in-game behavior
   - лучше использовать вместе с spec/reference pages, а не отдельно

### Level E — official EA material
Полезен, но по другой теме.

10. **Official EA / EA Forums modding posts**
   - в найденных материалах в основном про Python script mods и code changes for modders
   - не являются полным официальным DBPF/package spec

## Material / render pipeline source pack

Companion docs created from this source pack:

- [Knowledge map](../../../knowledge-map.md)
- [Workflows index](../../../workflows/README.md)
- [Material pipeline deep dives](../../../workflows/material-pipeline/README.md)
- [Shared TS4 Material, Texture, And UV Pipeline](../../../shared-ts4-material-texture-pipeline.md)
- [Corpus-Wide Family Priority](../../../workflows/material-pipeline/corpus-wide-family-priority.md)
- [Corpus-Wide Family Census Baseline](../../../workflows/material-pipeline/corpus-wide-family-census-baseline.md)
- [MATD Shader Census Baseline](../../../workflows/material-pipeline/matd-shader-census-baseline.md)
- [Sim Archetype Material Carrier Census](../../../workflows/material-pipeline/sim-archetype-material-carrier-census.md)
- [CAS Carrier Census Baseline](../../../workflows/material-pipeline/cas-carrier-census-baseline.md)
- [CASPart Linkage Census Baseline](../../../workflows/material-pipeline/caspart-linkage-census-baseline.md)
- [CASPart GEOM Shader Census Baseline](../../../workflows/material-pipeline/caspart-geom-shader-census-baseline.md)
- [CASPart Parser Boundary](../../../workflows/material-pipeline/caspart-parser-boundary.md)
- [CASPart GEOM Resolution Boundary](../../../workflows/material-pipeline/caspart-geom-resolution-boundary.md)
- [Build/Buy Material Authority Matrix](../../../workflows/material-pipeline/buildbuy-material-authority-matrix.md)
- [Build/Buy Transparent Object Fallback Ladder](../../../workflows/material-pipeline/buildbuy-transparent-object-fallback-ladder.md)
- [Build/Buy Transparent Object Classification Signals](../../../workflows/material-pipeline/buildbuy-transparent-object-classification-signals.md)
- [Build/Buy Stateful Material-Set Seam](../../../workflows/material-pipeline/buildbuy-stateful-material-set-seam.md)
- [Documentation Status Catalog](../../../workflows/material-pipeline/documentation-status-catalog.md)
- [CAS/Sim Material Authority Matrix](../../../workflows/material-pipeline/cas-sim-material-authority-matrix.md)
- [Body And Head Shell Authority Table](../../../workflows/material-pipeline/body-head-shell-authority-table.md)
- [BodyType Translation Boundary](../../../workflows/material-pipeline/bodytype-translation-boundary.md)
- [CompositionMethod And SortLayer Boundary](../../../workflows/material-pipeline/compositionmethod-sortlayer-boundary.md)
- [CompositionMethod Census Baseline](../../../workflows/material-pipeline/compositionmethod-census-baseline.md)
- [Overlay And Detail Family Authority Table](../../../workflows/material-pipeline/overlay-detail-family-authority-table.md)
- [SortLayer Census Baseline](../../../workflows/material-pipeline/sortlayer-census-baseline.md)
- [SimGlass Build/Buy Evidence Order](../../../workflows/material-pipeline/simglass-buildbuy-evidence-order.md)
- [SimGlass Domain Home Boundary](../../../workflows/material-pipeline/simglass-domain-home-boundary.md)
- [SimGlass Character Transparency Boundary](../../../workflows/material-pipeline/simglass-character-transparency-boundary.md)
- [SimGlass Character Transparency Order](../../../workflows/material-pipeline/simglass-character-transparency-order.md)
- [Character Transparency Open Edge](../../../workflows/material-pipeline/character-transparency-open-edge.md)
- [Character Transparency Evidence Ledger](../../../workflows/material-pipeline/character-transparency-evidence-ledger.md)
- [Object Transparency Evidence Ledger](../../../workflows/material-pipeline/object-transparency-evidence-ledger.md)
- [ShaderDayNight Evidence Ledger](../../../workflows/material-pipeline/shader-daynight-evidence-ledger.md)
- [Generated-Light Evidence Ledger](../../../workflows/material-pipeline/generated-light-evidence-ledger.md)
- [Projection, Reveal, And Generated-Light Boundary](../../../workflows/material-pipeline/projection-reveal-generated-light-boundary.md)
- [Refraction Evidence Ledger](../../../workflows/material-pipeline/refraction-evidence-ledger.md)
- [Refraction Bridge Fixture Boundary](../../../workflows/material-pipeline/refraction-bridge-fixture-boundary.md)
- [Refraction Post-LilyPad Pivot](../../../workflows/material-pipeline/live-proof-packets/refraction-post-lilypad-pivot.md)
- [Refraction Next-Route Priority](../../../workflows/material-pipeline/live-proof-packets/refraction-next-route-priority.md)
- [Refraction 0389 Clean-Route Baseline](../../../workflows/material-pipeline/live-proof-packets/refraction-0389-clean-route-baseline.md)
- [Refraction 0124 Mixed-Control Floor](../../../workflows/material-pipeline/live-proof-packets/refraction-0124-mixed-control-floor.md)
- [Refraction 0389 Identity Gap](../../../workflows/material-pipeline/live-proof-packets/refraction-0389-identity-gap.md)
- [Refraction 0389 Versus LilyPad Floor](../../../workflows/material-pipeline/live-proof-packets/refraction-0389-vs-lilypad-floor.md)
- [Refraction Companion MATD-vs-MTST Boundary](../../../workflows/material-pipeline/live-proof-packets/refraction-companion-matd-vs-mtst-boundary.md)
- [Refraction Adjacent-Helper Boundary](../../../workflows/material-pipeline/live-proof-packets/refraction-adjacent-helper-boundary.md)
- [Shader Family Registry](../../../workflows/material-pipeline/shader-family-registry.md)
- [Skintone And Overlay Compositor](../../../workflows/material-pipeline/skintone-and-overlay-compositor.md)
- [SimSkin Body/Head Shell Authority](../../../workflows/material-pipeline/live-proof-packets/simskin-body-head-shell-authority.md)
- [Object Glass And Transparency](../../../workflows/material-pipeline/family-sheets/object-glass-and-transparency.md)

Для общей темы `BuildBuy/CAS/Sim` render pipeline полезно заранее разделять источники не только по trust level, но и по тому, какой кусок пайплайна они реально подтверждают.

### A.5. Whole-install index and direct package-derived census layers

Use for whole-corpus prevalence and ranking baselines, not for semantics:

- live whole-install shard set under `tmp/profile-index-cache/cache/`
- `tmp/profile_index_fullscan_2026-04-20.log`
- `tmp/matd_shader_census_fullscan.json`
- `tmp/sim_material_carrier_census.json`
- `tmp/cas_carrier_census_fullscan.json`
- `tmp/caspart_linkage_census_fullscan.json`
- `tmp/caspart_geom_shader_census_fullscan.json`
- `tmp/compositionmethod_census_fullscan.json`
- `tmp/compositionmethod_cache_backfill.json`

What they confirm well:

- real whole-install package/resource/asset totals
- real object-side `MaterialDefinition` prevalence
- direct graph-backed `Sim archetype` carrier prevalence
- direct whole-`CAS` slot/fact prevalence
- direct package-derived `CASPart -> GEOM/texture/region_map` linkage prevalence for the currently parsable structured subset
- direct package-derived `CASPart -> linked GEOM -> shader family` prevalence for the currently parsable subset with broad cross-package geometry resolution
- direct whole-install `CompositionMethod` prevalence and `CompositionMethod + SortLayer` pair prevalence for the currently parsable subset
- repopulated shard-backed `composition_method` facts for ordinary SQLite queries over `cas_part_facts`
- direct object-side `MATD` shader-profile counts such as:
  - `FadeWithIce = 27434`
  - `g_ssao_ps_apply_params = 480`
  - `ObjOutlineColorStateTexture = 157`
- direct character-side `GEOM` family counts such as:
  - `SimSkin = 280983` by `CASPart` rows
  - `SimGlass = 6048` by `CASPart` rows
  - `GeometryResolvedFromExternalPackage = 12911`
- direct compositor-side counts such as:
  - `composition=0 = 243517`
  - `composition=32 = 44619`
  - `composition=32 | sort=65536 = 44598`

What this layer also needs explicitly:

- known GEOM-side hash-name overrides from the external `TS4SimRipper` enum packet, because local precompiled profile guesses can drift across domains

What they do not confirm by themselves:

- external shader-family semantics
- whole `CAS/Sim` family prevalence
- direct `GEOM`/linked material prevalence for `CAS` asset rows
- full whole-`CAS` linkage prevalence across all raw `CASPart` rows
- full whole-`CAS` family prevalence across the raw rows still outside the structured parser boundary
- whole-game family ranking for `SimSkin`, `SimGlass`, `CASHotSpotAtlas`, `RefractionMap`, `ShaderDayNightParameters`

### A. Resource identity and high-level roles

Использовать для ответа на вопрос "какой ресурс за что отвечает":

- `The Sims 4 Modders Reference / Resource Type Index`
- `The Sims 4 Modders Reference / File Types`

Что они хорошо подтверждают:

- type ids
- high-level роли `CAS Part`, `Skintone`, `Region Map`, `Geometry`, `Model`, `Model LOD`, `Material Definition`, `Material Set`
- разделение `Build Mode` и `Create-A-Sim` как разных discovery domains
- `Object Definition` как swatch-level Build/Buy linkage record
- `Light` как отдельную resource family, а не просто material slot

Чего они не дают полностью:

- полный shader-family registry
- полный порядок runtime compositing
- точную authority order между `CASP`, `GEOM/MTNF`, `MATD`, `MTST`, `Skintone`, `RegionMap`

### B. Chunk-level mesh and material linkage

Использовать для ответа на вопрос "как именно mesh group связан с material/vertex data":

- `MTS / Sims_4:RCOL`
- `MTS / Sims_4:0x01D10F34` (`MLOD`)
- `MTS / Sims_4:0x015A1849` (`GEOM`)
- `MTS / Sims_4:0xC0DB5AE7` (`Object Definition`)
- `MTS / Info | COBJ/OBJD resources`
- `MTS / Sims_4:0x03B4C61D` (`LITE`)
- `EA forum thread` on `Object Definition` vs object definition cross-references

Что они хорошо подтверждают:

- `MLOD -> VRTF/VBUF/IBUF/SKIN/MATD or MTST`
- `GEOM` как body geometry container
- embedded `MTNF`
- multi-UV and vertex-layout reality
- object-side seam details for Build/Buy fixtures:
  - `OBJD` carries direct `Model`, `Rig`, `Slot`, and `Footprint` references
  - `MaterialVariant` in `OBJD` points by FNV32 name into `MODL/MLOD` material entries and from there to the relevant material definition
- базовый Build/Buy authority order: `Object Definition -> Model/Model LOD -> Material Set -> Material Definition`
- `LITE` как отдельную light/emitter ветку рядом с surface-material chain
- enough external object-side authority to keep a dedicated `Build/Buy` companion doc stable:
  - `COBJ/OBJD` as swatch-level identity
  - `MODL/MLOD` as the base mesh/material chain
  - `MTST/MATD` as the current material authority seam
  - `LITE` and `VPXY` as bounded parallel/helper branches rather than replacements for that seam

Чего они не дают полностью:

- все современные TS4 shader families
- все CAS and Sim compositing rules
- полный TS4-specific `VPXY` writeup

### C. CAS field-level routing and modern character material inputs

Использовать для ответа на вопрос "какие реальные material-relevant поля есть у `CASP` и `Skintone`":

- local `Binary-Templates`
- local `TS4SimRipper`
- `Sims 4 Studio` release notes and creator guidance

Что они хорошо подтверждают:

- `CASP` diffuse/shadow/region/normal/specular/emission/mask fields
- `SharedUVMapSpace` как code-backed field name
- `ColorShiftMask`
- separate `CASP` texture-space fields such as `textureSpace` / `UniqueTextureSpace`
- `Skintone` base + overlay + opacity/colorize fields
- creator-visible shared CAS atlas behavior
- body-type slot semantics: one active part per body-type slot, with `Full Body` versus `Upper/Lower Body` incompatibility documented in creator-facing references
- `SimModifier` / `DeformerMap` / `HotSpotControl` resource families and their relevance to the full CAS editing/morph branch
- code-backed DMap application to `GEOM` with `UV1`
- a first bounded `CAS/Sim` material-input graph:
  - selected `CASP` + linked `GEOM`
  - embedded `MTNF` when present
  - `MATD/MTST` material-definition path when present
  - `CASP` field-routing path
  - `RegionMap` / `SharedUVMapSpace` / `CompositionMethod` / `SortLayer`
  - Sim-only `Skintone` routing/compositing layer
- a first bounded `CAS/Sim` family split:
  - torso/body shell families
  - separate head shell family
  - footwear overlay family
  - separate hair/accessory slot families
  - compositor-driven overlay/detail families
- stronger worn-slot authority boundaries in current repo code:
  - `Hair` and `Accessory` use exact-part-link slot resolution before compatibility fallback
  - `Shoes` remain overlay/body-assembly content, not shell identity
  - resolved `GEOM` can bring cross-package companion `Rig` / `MaterialDefinition` / texture resources
  - scene build prefers explicit material-definition decoding before manifest approximation when those resources exist
- stronger shell-family authority boundaries in current repo code:
  - shell filtering is applied only to shell labels
  - default/nude shell preference uses `nakedLink` / `defaultBodyType` facts rather than only name heuristics
  - exact human shell candidates can be withheld until a real default/nude shell is found
  - head shell stays a separate contribution on top of the body shell anchor
  - skintone targeting is currently scoped to body/head shell batches
  - parsed `CASP` texture refs and `region_map` are eagerly resolved before scene build, so shell families already have a field-routed material floor even when no explicit `MaterialDefinition` is present
  - explicit companion `MaterialDefinition` resources can still upgrade shell materials when geometry companions expose them
  - explicit shell `MaterialDefinition` evidence is currently asymmetric: strong for generic CAS and worn-slot scene-build fixtures, partial for shell-specific end-to-end graph/scene fixtures, but already proven at the composer-level shell merge seam
- current shell fixtures still frequently materialize as shell-scoped `ApproximateCas`, so explicit `MATD` is a supported upgrade path, not yet a proven universal shell prerequisite
- `MTNF` is strongly confirmed as a real embedded `GEOM` material carrier by external references, but current repo GEOM parsing still skips the embedded payload and current shell fixtures barely exercise it
  - the bundled local `TS4SimRipper` body/head/waist sample corpus gives a first prevalence hint: `9/9` sampled shell-like `.simgeom` resources in that snapshot contain `MTNF`
  - that first local sample hint is now also split into a small matrix: `Body 4/4`, `Head 4/4`, `Waist 1/1`
  - modern TS4 creator-tooling evidence also supports `MTNF` as behaviorally relevant payload: incorrect MTNF shader-size handling is documented as causing save/game issues for `GEOM`s
  - TS4 creator-facing shader practice also supports preserving `GEOM`-side shader identity in the authority model: `SimGlass`, `SimSkin`, `SimEyes`, and `SimAlphaBlended` are all treated as practical mesh/shader choices with visible behavior differences
  - creator-facing transparency guidance now also narrows the character side of that split further: current evidence keeps `SimGlass` and `SimAlphaBlended` as separate transparency-capable family names rather than one generic alpha bucket
  - current family ordering is now narrower too: `SimGlass` has the stronger external packet, `SimAlphaBlended` remains a separate named branch, and generic character alpha stays only as provisional fallback wording
  - that ordering is still intentionally open at one neighboring edge: the current local external snapshot is much stronger for `SimGlass` than for `SimAlphaBlended` or `SimEyes`, so `SimEyes` remains unresolved here rather than silently entering the same closed order
  - local external `TS4SimRipper` code strengthens that further: `SimGlass` meshes are tracked/exported as a separate glass path, while unknown mesh shaders fall back to `SimSkin`
  - cross-domain reading is now bounded more tightly too: current external evidence keeps `CAS/Sim` as the semantic home for `SimGlass`, while `Build/Buy` is only allowed as a carry-over evidence domain until a fixture-grade object-side case survives reopen
  - local precompiled shader corpus adds a first relative-weight hint: `simskin` / `SimSkinMask` look core in the current snapshot, while `SimGlass` looks real but narrow
  - this is now strong enough for a first implementation-priority split in the docs: `SimSkin`/`SimSkinMask` as core-family work first, `SimGlass` as edge-but-real, and `SimEyes`/`SimAlphaBlended` as preserved special provenance
  - this priority split is still explicitly inference-backed for planning, not a claim that local corpus prevalence equals full in-game frequency
  - the current guide now also narrows the `P1` packet internally: `SimSkin` is the safe baseline `GEOM`-side skin family seam, while `SimSkinMask` is better treated as an adjacent auxiliary skin-family signal until stronger live-asset proof shows a standalone authority branch
  - that narrower split is supported by a mixed evidence packet: repeated parameter-level `SimSkinMask` entries in the local precompiled snapshot, `SimSkinMask` co-presence with skin-adjacent params in the local inventory snapshot, and the absence of an equally explicit external code branch comparable to `SimGlass`
  - local repo code now also supports the same bounded reading indirectly: current CAS graphs still resolve explicit `CASP` texture refs and geometry companions into `ApproximateCasMaterial`, skintone routing stays region-map-aware `ApproximateCas` application data, and viewport code treats `mask` as generic slot vocabulary rather than a dedicated `SimSkinMask` authority root
  - bundled local sample geometries now add the first direct sample-asset anchor for `SimSkin`: the `TS4SimRipper` body/head/waist `.simgeom` resources currently check `9/9` for shader hash `0x548394B9`
  - that same local sample packet currently still does not provide a peer asset-level `SimSkinMask` branch, which strengthens the reading that `SimSkinMask` is not yet proven as a standalone geometry family
  - external creator/tooling guidance around skin masks also points in the same direction: current TS4 mask usage is better described as overlay/skin-detail or skintone-adjacent semantics than as a separate `GEOM` shader branch
  - the current local negative finding is now tighter too: an exact code sweep across the current repo render/composition paths plus bundled external tool code checked in-repo still does not surface a named `SimSkinMask` authority/export branch, while `SimSkin` and `SimGlass` are explicitly named
  - creator tooling now corroborates that reading from the other side as well: `TS4 Skininator`, `TS4 Skin Converter`, and recent `Sims 4 Studio` notes all keep mask-bearing skin content inside skintone/overlay/image workflows rather than surfacing a peer `SimSkinMask` geometry family
  - a wider workspace sweep still does not surface a broader local live/sample corpus for `SimSkinMask`: outside the mirrored `TS4SimRipper` resources, no extra `.simgeom` packet in the current repo snapshot exposes a peer branch
- the broader mainstream toolchain packet checked for this pass still points the same way: `TS4CASTools`, `TS4SimRipper`, `Skininator`, and `Sims 4 Studio` expose `SimSkin`, `SimGlass`, `ColorShiftMask`, overlays, and burn-mask semantics, but not a peer named `SimSkinMask` geometry/export/import branch
- a new external lead for packed `CASP` body-type values:
  - local external code reads separate `textureSpace` and `bodyType` fields
  - community documentation lists `AdditionalTextureSpace` with the same vocabulary as `BodyType`
  - creator-facing analysis now also talks about “Outfit Type” as `BodyType + AdditionalTextureSpce`
  - this is strong enough to treat `AdditionalTextureSpace` as the leading external candidate for the large high-byte `BodyType` families, but not strong enough to call the exact encoding solved
- that dense `CAS/Sim` authority packet is now also split out into its own workflow doc so further family-by-family passes can stay local instead of bloating the main cross-domain guide
- the shader-family packet is now also split out more explicitly:
  - `MaterialDecoding.cs` now defines the current preview-facing strategy buckets
  - `ShaderSemantics.cs` now defines the current slot fallback rules for families like `DecalMap`, `ShaderDayNightParameters`, `WriteDepthMask`, and `WorldToDepthMapSpaceMatrix`
  - local sample coverage dumps now add representative live Build/Buy roots for `SeasonalFoliage`, `colorMap7`, `WriteDepthMask`, `ShaderDayNightParameters`, `WorldToDepthMapSpaceMatrix`, `SpecularEnvMap`, `DecalMap`, `painting`, `SimWingsUV`, and `samplerCASPeltEditTexture`
  - bundled local `TS4SimRipper` `.simgeom` resources keep the first direct `SimSkin` sample packet even where full per-family material dumps are still missing
- the skintone/compositor packet is now also split out more explicitly:
  - `TONE` carries skin sets, overlay instances, overlay multipliers, hue/saturation, opacity, and per-overlay age/gender flags
  - `SkinBlender` implements a clear multi-pass approximation using base skin, body details, skin-set overlay/mask, overlay color, second-pass opacity, and age/gender overlays
  - current repo code already resolves real `Skintone` resources and applies region-map-aware skintone routing to selected canonical materials, but does not yet implement exact compositor math
  - overlay/detail families are now better bounded as compositor-driven `CASPart` layers, not geometry-family roots

Чего они не дают полностью:

- не всегда formal binary spec
- часть сведений остаётся tool-centric или community-centric
- полная in-game shader stack для Sim skin всё ещё собрана не до конца
- exact packed encoding rule for the large high-byte `BodyType` values seen in the current whole-`CAS` counts

### D. Community behavior proofs

Использовать для ответа на вопрос "что реально ломается в живом creator workflow, если rule нарушена":

- `Sims 4 Studio` forum threads about CAS UV map placement
- release notes about `ColorShiftMask`, skin specularity, and newer CAS texture support
- creator troubleshooting threads on `Mod The Sims`
- `CAS Designer Toolkit` release notes:
  - https://modthesims.info/d/694549
- TS4 creator-facing transparency guidance:
  - [Transparency in clothing tutorial](https://maxismatchccworld.tumblr.com/post/645249485712326656/transparency-in-clothing-tutorial)
  - [Semi-Square Eyeglasses](https://kijiko-catfood.com/semi-square-eyeglasses/)
  - [Lashes and hair cc clashing](https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/lashes-and-hair-cc-clashing-pics-included-please-help-/12047424)
  - [DaraSims glass-object tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2980-urok-po-sozdaniyu-steklyannyh-obektov-pri-pomoschi-programmy-sims-4-studio.html)
  - [DaraSims object transparency without `AlphaBlended`](https://darasims.com/stati/tutorial/tutor_sims4/3196-dobavlenie-obektam-prozrachnosti-gde-net-parametra-alphablended-v-sims-4-studio.html)
  - [DaraSims transparent-curtain tutorial](https://darasims.com/stati/tutorial/tutor_sims4/2984-sozdanie-prozrachnyh-shtor-v-sims-4.html)
  - [Object Material Settings Cheat Sheet](https://staberindesims.wordpress.com/2021/06/05/object-material-settings-cheat-sheet/)

Что они хорошо подтверждают:

- общая CAS UV atlas space
- bleed and overlap failure modes
- значимость `uv_1`
- значимость `ColorShiftMask` и похожих полей как реальной vocabulary, а не repo-local invention
- практический смысл `CompositionMethod` и `SortLayer`
- то, что более высокий `SortLayer` рисуется поверх меньшего
- то, что `SharedUVMapSpace` влияет на normal/atlas behavior при смене body-part category
- practical `CASHotSpotAtlas -> HotSpotControl -> SimModifier` behavior in creator-facing morph workflows
- creator-facing body-type semantics for `Hair`, `Head`, `Full Body`, `Top`, `Bottom`, `Shoes`, and accessory-like slots
- creator-facing separation between:
  - character-side `SimGlass`
  - object-side glass families such as `GlassForObjectsTranslucent`
  - threshold/cutout transparency via `AlphaMap` plus `AlphaMaskThreshold`
  - blended object transparency via `AlphaBlended`
- enough creator-facing signal detail to build a first decision order for reopened transparent `Build/Buy` fixtures:
  - explicit `GlassForObjectsTranslucent` or glass-family params
  - threshold/cutout signals such as `AlphaMap` plus `AlphaMaskThreshold`
  - explicit `AlphaBlended`
  - only then fallback consideration of `SimGlass`
- enough creator-facing signal detail to build a first fallback ladder too:
  - prefer named object-side branches first
  - keep `SimGlass` as last-choice named branch for `Build/Buy`
- that object-side packet is now also split more explicitly by evidence layer:
  - external creator and lineage confirmation for the object-side branches
  - local package evidence only for candidate selection and route narrowing
  - bounded synthesis for the current decision order
  - allow generic transparent provisional reading only after named branches are documented as insufficient
- practical `CASP -> GEOM -> RegionMapKey` linkage expectations in mesh-edit workflows
- creator-facing expectation that CAS body types are true slot identities rather than interchangeable material families
- creator-facing expectation that default/nude CASP flags materially affect whether a part behaves like underwear/default body content
- creator-facing expectation that `CASP` remains the part-level root when additional `GEOM` resources are attached through the `CASP` TGI list
- creator-facing evidence that importing/exporting `MTNF` can materially affect CAS mesh behavior even outside formal format docs
- TS4 creator-tooling changelogs now also support that malformed `MTNF` shader payloads are not benign metadata errors; they can affect saved/game-visible `GEOM` behavior
- creator-facing evidence that family-specific GEOM shader choices like `SimGlass` and `SimEyes` change visible TS4 behavior and therefore should not be flattened away in documentation or IR design
- creator-facing evidence that `SimGlass` is not just "anything glass-like":
  - creators explicitly switch to `Simglass` for transparent clothing parts
  - semi-transparent eyeglass frames are described as requiring `SimGlass`
  - current support/forum guidance describes alpha hair and lashes as using the same "glass" shader family as glasses and other transparent CAS items
- this makes `SimGlass` safer to search from transparent layered content than from broad architectural window vocabulary alone
- `Build/Buy` carry-over for `SimGlass` is now weighted explicitly too:
  - external creator-facing `SimGlass` packet sets the family floor
  - local external `TS4SimRipper` packet preserves the branch operationally
  - object-side transparent split remains the stronger competing explanation layer
  - aggregate survey and narrowed route packets only keep the branch alive and rank reopens
  - only reopened fixture evidence may decide branch loss, provisional `SimGlass`, or winning `SimGlass`
- local external code-backed evidence that some GEOM shader families are already treated as separate export/render branches, not just named flags
- local corpus-backed evidence that shader-family prevalence is uneven and should influence prioritization: `SimSkin`-adjacent families appear much more heavily than `SimGlass` in the current precompiled snapshot

Чего они не дают полностью:

- точный engine-side runtime implementation
- строгую spec authority

### E. Local decoder-backed corpus evidence

Использовать для ответа на вопрос "что именно уже видит и поддерживает текущий repo decoder":

- local `tmp/precomp_shader_profiles.json`
- local [ShaderProfileRegistry.cs](../../../../src/Sims4ResourceExplorer.Preview/ShaderProfileRegistry.cs)
- local [MaterialDecoding.cs](../../../../src/Sims4ResourceExplorer.Preview/MaterialDecoding.cs)
- local [MaterialCoverageMetrics.cs](../../../../src/Sims4ResourceExplorer.Preview/MaterialCoverageMetrics.cs)

Что они хорошо подтверждают:

- фактический local shader-profile corpus
- normalized family buckets, которые уже использует repo decoder
- текущие strategy buckets: `StandardSurface`, `ColorMap`, `AlphaCutout`, `SeasonalFoliage`, `Projective`, `SpecularEnvMap`, `StairRailings`, `Unknown/default`
- текущие support tiers `StaticReady`, `Approximate`, `RuntimeDependent`
- какие UV/compositor cases текущий preview already treats as static-ready vs approximation vs runtime-dependent
- текущие slot-name normalization и UV-interpretation heuristics, которые уже живут в decoder code
- current decoder family-specific fallback behavior, including `ShaderDayNightParameters` mapping `texture_1 -> emissive`, `texture_* -> overlay`, `SourceTexture -> diffuse`, and `routingMap -> alpha`
- representative per-family slot/parameter tables for the current implementation baseline
- first taxonomy split between family-local unresolved params and broad cross-family runtime/helper params
- first edge-case family matrix and semantic priority queue for unresolved family-local params
- first narrow `P1 target sheets` for `RefractionMap/tex1`, `ShaderDayNightParameters`, `NextFloorLightMapXform`, and `CASHotSpotAtlas`
- stronger per-profile concentration evidence from local `precomp_sblk_inventory.json`, not only simple presence/absence in `precomp_shader_profiles.json`
- first branch-oriented narrowing of the unresolved space:
  - `samplerRevealMap` as a cross-family visible-pass reveal/helper input
  - `LightsAnimLookupMap` as a much narrower day/night or terrain-light lookup helper
  - `NextFloorLightMapXform` as a lightmap-transform/helper signal with `GenerateSpotLightmap` as the stronger semantic home
  - `RefractionMap/tex1` as a projective/refraction-family-local unresolved input rather than a generic surface slot

Чего они не дают полностью:

- authoritative EA semantics
- полный per-family slot/parameter contract
- гарантию, что current support tier == in-game parity

### F. Narrow external corroboration for lightmap-adjacent names

Использовать только как supporting evidence, не как full shader spec:

- `Mod The Sims` thread on TS4 lighting/lightmaps:
  - [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135)

Что это хорошо подтверждает:

- `NextFloorLightMapXform` и `GenerateSpotLightmap` действительно живут в одном lightmap-oriented vocabulary
- TS4 lightmap surface is materially richer than a single baked-light slot
- lightmap-generation and lightmap-transform names не стоит насильно трактовать как ordinary material slots
- `Generated-Light Evidence Ledger` can now treat this thread as the external anchor while keeping local carry-through clearly separate from exact semantics

Чего это не даёт полностью:

- exact shader math
- exact matrix semantics for `NextFloorLightMapXform`
- per-family authoritative slot contracts

### G. Narrow external corroboration for `CASHotSpotAtlas`

Использовать как strong role evidence for hotspot/morph editing, not as a general surface-material spec:

- `Mod The Sims` TS4 MorphMaker / DMap tutorial:
  - [Making a CAS slider with TS4MorphMaker using a Deformer Map](https://modthesims.info/t/613057)
- `Mod The Sims` pointed-ear slider thread:
  - [Pointed Ears as CAS Sliders](https://db.modthesims.info/showthread.php?t=596028)

Что это хорошо подтверждает:

- `CASHotSpotAtlas` — реальный EA atlas resource
- atlas is mapped to `UV1` of Sim meshes
- atlas participates in CAS edit hotspot / slider / morph routing rather than behaving like a normal surface-material slot
- atlas colors feed `HotSpotControl`, which then selects `SimModifier` resources linked to `DMap` / `BGEO` / `BOND` style morph mechanisms

Чего это не даёт полностью:

- почему `CASHotSpotAtlas` carry-through still appears in some non-obvious rendering-profile corpora
- exact runtime/render-path behavior when this atlas metadata survives outside explicit CAS editing workflows

### H. Engine-lineage corroboration for reveal/refraction naming

Использовать только как lineage/inference support, not as an authoritative TS4 spec:

- `Mod The Sims` Sims 3 shader parameter index:
  - [Sims_3:Shaders\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams)
- `Mod The Sims` Sims 3 shader family index:
  - [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders)

Что это хорошо подтверждает:

- `RevealMap` historically exists as a dedicated shader texture param in the same engine lineage rather than as a synonym for `DiffuseMap`
- `RevealMap` is shown in a concrete shader family (`Painting`), which supports the safer interpretation of `samplerRevealMap` as reveal/mask/helper provenance instead of a normal canonical surface slot
- refraction-oriented families in the same lineage (`simglass`, water families) already use dedicated refraction semantics such as `index_of_refraction` and `RefractionDistortionScale`
- this makes it safer to keep `RefractionMap` and `tex1` in the projection/refraction branch until stronger TS4-specific proof appears
- `Refraction Evidence Ledger` can now treat those lineage references as the external anchor while keeping survey-level presence and named bridge-root evidence clearly separate from slot closure
- `Refraction Bridge Fixture Boundary` now freezes the distinction between a valid named inspection bridge and exact refraction-slot closure
- `Refraction Post-LilyPad Pivot` now freezes that the named `lilyPad` fixture is a bounded floor/ceiling reference, not the only remaining refraction route
- `Refraction Next-Route Priority` now fixes the local post-`lilyPad` order so a restart does not jump back to the same fixture or promote the noisier `0124...` mixed route too early
- `Refraction 0389 Clean-Route Baseline` now records the first honest coverage-backed packet for the next clean projective/refraction route
- `Refraction 0124 Mixed-Control Floor` now freezes the narrower reading of `0124...` as a mixed/control route rather than a promoted next clean target
- `Refraction 0389 Identity Gap` now records that `0389...` is still coverage-backed only and not yet a named object/material fixture like `lilyPad`
- `Refraction 0389 Versus LilyPad Floor` now freezes the honest comparison: `0389...` currently matches the `lilyPad` floor shape, but not the stronger named seam and negative-ceiling packet
- `Refraction Companion MATD-vs-MTST Boundary` now freezes one more layer: the named fixture may reach `MATD`, meaningful `MTST`, both, or neither, but none of those should be assumed before inspection
- `Refraction Adjacent-Helper Boundary` now keeps projective-helper survival separate from direct refraction-family closure
- `ShaderDayNight Evidence Ledger` can now treat the lineage `RevealMap` packet as the external anchor while keeping local visible-root isolation clearly separate from exact TS4 visible-pass semantics
- `Projection, Reveal, And Generated-Light Boundary` now uses those same anchors to keep refraction, reveal/day-night, and generated-light rows separate under one umbrella

Чего это не даёт полностью:

- exact TS4 shader bytecode contracts
- exact visible-pass math for `samplerRevealMap`
- exact semantic identity of `tex1` in `RefractionMap`

### I. Narrow corroboration for `VPXY`

Использовать как bounded scenegraph/linkage evidence, not as a fully authoritative TS4 traversal spec:

- `Mod The Sims` TS4 RCOL page:
  - [Sims_4:RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- `Mod The Sims` older engine-lineage VPXY page:
  - [Sims_3:0x736884F1](https://modthesims.info/wiki.php?title=Sims_3%3A0x736884F1)

Что это хорошо подтверждает:

- `VPXY` definitely exists in the TS4 scenegraph chunk ecosystem and is still categorized as `Model Links`
- lineage material for the same type id strongly supports a linkage/proxy role that can point at `GEOM`, `MODL`, `MLOD`, `LITE`, `RSLT`, and `FTPT`
- this is enough to keep `VPXY` in the object/scene linkage branch and out of the base surface-material authority chain unless stronger TS4-specific proof appears

Чего это не даёт полностью:

- exact TS4 `VPXY` structure
- exact TS4 traversal order
- proof that current repo scene reconstruction should actively depend on `VPXY` outside specific linked-object ecosystems

### J. Full-corpus local package census

Использовать для ответа на вопрос "каков реальный размер и layout installed TS4 corpus", а не для вывода shader semantics:

- fresh full filesystem profile scan:
  - `tools/ProbeAsset/bin/Release/net8.0/win-x64/ProbeAsset.exe --profile-index "C:\GAMES\The Sims 4" 6000 16 largest`
- log:
  - `tmp/profile_index_fullscan_2026-04-20.log`
- shard cache:
  - `tmp/profile-index-cache/cache/index.sqlite`
  - `tmp/profile-index-cache/cache/index.shard01.sqlite`
  - `tmp/profile-index-cache/cache/index.shard02.sqlite`
  - `tmp/profile-index-cache/cache/index.shard03.sqlite`

Что это хорошо подтверждает:

- реальный full-install corpus floor:
  - `4965` filesystem package files selected
  - `4963` indexed package rows currently persisted
  - `4789589` indexed resources
  - `743150` indexed assets
  - `603` asset-bearing package paths
- доменное распределение indexed assets:
  - `Cas = 530507`
  - `BuildBuy = 142941`
  - `General3D = 68158`
  - `Sim = 1544`
- реальный layout corpus slices:
  - огромный хвост `Strings_*`, `ClipHeader`, `magalog`, `thumbnails`, `SimulationPreload`, `Delta`
  - только небольшое подмножество package paths реально несёт indexed assets
- почему whole-game priority нельзя выводить из одного pack-local route

Чего это не даёт полностью:

- direct whole-game family counts for rows like `SimGlass`, `RefractionMap`, `ShaderDayNightParameters`
- shader/material semantics
- perfect integrity yet:
  - `EP18\ClientFullBuild0.package`
  - `EP18\SimulationFullBuild0.package`
  are currently missing from the persisted shard tables and must stay marked as an explicit census gap

## Material-pipeline contradictions to keep explicit

Эти противоречия нельзя замалчивать в документации; их лучше держать как явные gaps:

1. `Material Definition` в современных справочниках описывается как wall/floor surface data, но более старые `RCOL/MLOD`-страницы явно помещают `MATD` в object mesh pipeline.
2. `CAS` shared UV space хорошо подтверждён community guidance и поддержан `CASP` fields, но точный enforcement model внутри runtime всё ещё не описан одной authoritative spec.
3. `Sim` skin pipeline подтверждён через `Skintone`, `RegionMap`, `CASP`, creator tooling и reference implementations, но полного официального end-to-end shader/compositor description всё ещё нет.
4. `VPXY` уверенно существует в TS4 ecosystem как `Model Links`, но его точная роль в current object render/link graph пока документирована заметно слабее, чем `Object Definition`, `MLOD` или `GEOM`.
5. Для `0xAC16FBEC` есть naming mismatch между источниками: часть code-backed материалов называет его `GEOMListResource`, а local binary templates и `TS4SimRipper` фактически описывают region/layer/replacement geometry map. Для render-domain работы безопаснее считать доказанным именно region-map behavior, а alias naming держать как reference mismatch.
6. `NormalUVBodyType` как имя поля сейчас слабее, чем `SharedUVMapSpace`: практический смысл совпадает, но code-backed upstream naming пока подтверждает именно `SharedUVMapSpace`.
7. local decoder buckets уже достаточно сильны для `support-oriented` shader registry, но этого всё ещё недостаточно для полного authoritative game-faithful shader contract.
8. для exact names вроде `tex1`, `samplerRevealMap`, `LightsAnimLookupMap`, `NextFloorLightMapXform`, `CASHotSpotAtlas` public TS4 writeups крайне редки; поэтому безопасная опора сейчас — это local code/corpus evidence плюс узкие community corroboration points, а местами ещё и clearly-labeled engine-lineage inference, а не выдуманная уверенность.
9. `CASHotSpotAtlas` уже нельзя считать completely unknown helper: его identity как `UV1`-mapped CAS hotspot atlas теперь подтверждена отдельно от render-profile archaeology.

## Рекомендуемый приоритет чтения

### Для raw package parsing
1. Modders Reference
2. LlamaLogic.Packages docs
3. dbpf_reader
4. Binary Templates
5. s4pi/s4pe

### Для Build/Buy scene reconstruction
1. File Types / Resource Type Index
2. RCOL / MODL pages
3. Binary Templates
4. LlamaLogic / s4pi code
5. local fixtures

### Для CAS part export
1. File Types / Resource Type Index
2. GEOM page
3. Binary Templates
4. local fixtures

### Для full Sim / morph / save-game pipelines
1. File Types / Resource Type Index
2. SimRipper
3. local fixtures / save files

## Licensing notes

- `LlamaLogic` — MIT
- `Binary-Templates` — MIT
- `s4ptacle/Sims4Tools` — GPLv3
- `TS4SimRipper` — GPL-3.0
- `dbpf_reader` — zlib license

Следствие:
- permissive sources можно легче использовать в коде;
- GPL sources безопаснее использовать как reference / research source, если нет намерения переводить проект под GPL.

## Source list

- Modders Reference Index  
  https://thesims4moddersreference.org/reference/
- DBPF format  
  https://thesims4moddersreference.org/reference/dbpf-format/
- Internal compression  
  https://thesims4moddersreference.org/reference/internal-compression-dbpf/
- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- STBL format  
  https://thesims4moddersreference.org/reference/stbl-format/
- LlamaLogic  
  https://github.com/Llama-Logic/LlamaLogic
- LlamaLogic `DataBasePackedFile` docs  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- Binary Templates  
  https://github.com/Llama-Logic/Binary-Templates
- Sims4Tools / s4pe  
  https://github.com/s4ptacle/Sims4Tools
- dbpf_reader  
  https://github.com/ytaa/dbpf_reader
- Sims 4:RCOL  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- MODL  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01661233
- GEOM  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849
- Object Definition  
  https://modthesims.info/wiki.php?title=Sims_4%3A0xC0DB5AE7
- LITE  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x03B4C61D
- Sims 4 Packed File Types  
  https://modthesims.info/wiki.php?title=Sims_4%3APackedFileTypes
- MLOD  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34
- VRTF  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01D0E723
- EA forum material-variant thread  
  https://forums.ea.com/discussions/the-sims-4-mods-and-custom-content-en/simgurumodsquad-looking-for-cross-reference-between-catalogobject-or-object-defi/1213694/replies/1213695
- TS4 SimRipper GitHub  
  https://github.com/CmarNYC-Tools/TS4SimRipper
- TS4 SimRipper MTS page  
  https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html
- Sims 4 Studio: Mesh glitches and gets color from other CC  
  https://sims4studio.com/thread/18038/solved-mesh-glitches-gets-color
- Sims 4 Studio: Texture Bake  
  https://sims4studio.com/thread/27008/texture-bake
- Sims 4 Studio 3.2.4.7 release notes  
  https://sims4studio.com/thread/29786/sims-studio-windows-star-open
- Manually editing the BodyPart thread  
  https://modthesims.info/showthread.php?t=542283
- pyxiidis: skins and makeup info  
  https://pyxiidis.tumblr.com/post/123291105281/skins-makeup-info-post
- Maxis Match CC World: Composition Method 0  
  https://maxismatchccworld.tumblr.com/post/622238734033797120/composition-method-0
- softerhaze sortLayer override note  
  https://www.tumblr.com/softerhaze/712246258728894464/growing-together-scar-freckle-and-mole
- Sims 4 Studio: bake texture problem with Blender when creating cc  
  https://sims4studio.com/thread/26090/bake-texture-problem-blender-creating
- Modding tutorial: Modifying Sim Appearances  
  https://thesims4moddersreference.org/tutorials/modifying-sim-appearances/
- Mod The Sims: Several CASP questions  
  https://modthesims.info/t/589486
- Mod The Sims: Adding new GEOMs to a CAS part with s4pe & S4CASTools  
  https://modthesims.info/t/536671
- DeepWiki / Sims4Tools CAS resources overview  
  https://deepwiki.com/s4ptacle/Sims4Tools/4.1-cas-%28create-a-sim%29-resources
