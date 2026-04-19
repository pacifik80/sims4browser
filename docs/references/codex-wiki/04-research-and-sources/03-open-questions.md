# Open questions

Эти вопросы пока нельзя считать закрытыми полностью. Но после текущего material/render pass их формулировка должна быть уже узкой и конкретной.

Если вопрос можно сузить до точного недостающего доказательства, он больше не должен звучать как "всё непонятно".

Detailed current authority notes now also live in:

- [Knowledge map](../../../knowledge-map.md)
- [Material pipeline deep dives](../../../workflows/material-pipeline/README.md)
- [CAS/Sim Material Authority Matrix](../../../workflows/material-pipeline/cas-sim-material-authority-matrix.md)

## Что уже НЕ стоит считать open question

Эти пункты раньше часто формулировались слишком широко, но теперь уже достаточно подтверждены:

1. `CASP` действительно является частью material/render pipeline, а не только identity metadata.
2. `RegionMap` действительно несёт region/layer/replacement-geometry semantics, а не только абстрактный routing hint.
3. `Skintone` действительно несёт layered compositing inputs, а не только один цвет или texture id.
4. `CompositionMethod` и `SortLayer` действительно участвуют в CAS compositing.
5. CAS shared atlas / shared UV-space — это реальное invariant behavior, а не случайная creator myth.
6. Базовый Build/Buy authority order уже можно формулировать как `Object Definition -> Model/Model LOD -> Material Set -> Material Definition`, с `Light` как параллельной веткой.

Это не означает, что всё реализовано. Это означает, что вопрос больше не в существовании этих правил, а в полном и точном покрытии.

## 1. Full shader-family registry

Что уже известно:

- repo уже имеет shared decoder и shared material IR
- repo now also has a first `v0` shader-family registry grounded in local decoder buckets and the current precompiled-profile corpus
- current decoder slot-name normalization and UV-parameter interpretation heuristics are now explicit in the docs instead of being hidden in code
- representative per-family `v0` tables now exist for core surface, color-map, foliage, layered, projective, and Sim-special examples
- the remaining `raw/unmapped` params are now split into narrower buckets such as cross-family runtime helpers, lighting/reveal helpers, projection controls, and family-local unresolved texture-like inputs
- edge-case families are now split between structurally runtime-dependent cases and narrow unresolved-family cases
- есть частично восстановленные family rules по texture slots, alpha hints и UV routing
- `BuildBuy`, `CAS` и `Sim` уже можно рассматривать через одну общую material vocabulary

Что всё ещё открыто:

- полный список shader families и profile names на реальных live assets
- authoritative slot table для каждой family
- exact scalar/vector parameter semantics по каждой family
- exact alpha/blend/compositor rules для каждой family
- for some narrow names there is still little or no public TS4-facing writeup, so closure may require live-asset fixtures rather than more forum archaeology

Что бы это закрыло:

- fixture-backed or source-backed registry table, где для каждой family есть:
  - representative assets
  - slot map
  - UV rules
  - blend rules
  - current support tier

Текущий промежуточный статус:

- `v0` registry уже достаточен, чтобы не оставлять тему shader families без структуры
- current implementation heuristics for slot naming and UV decoding are now documented
- representative `v0` family tables now expose which params are already mapped vs still `raw/unmapped`
- the remaining `raw/unmapped` space is no longer one anonymous bucket
- next semantic-work priority is now narrowed to specific family-local params instead of generic “unknown shader stuff”
- there are now narrow `P1` target sheets for `RefractionMap/tex1`, `ShaderDayNightParameters`, `NextFloorLightMapXform`, and `CASHotSpotAtlas`
- local `precomp_sblk_inventory` now adds per-profile concentration evidence, not just profile presence/absence
- the remaining narrow gaps are now split more explicitly between `surface-material`, `lightmap/projection`, and `CAS editing/morph` branches
- но он всё ещё support-oriented, а не full authoritative contract

Уточнение по самым узким текущим gaps:

- `RefractionMap/tex1` теперь уже не выглядит как missing generic slot; это family-local unresolved input inside a projective/runtime family.
- `ShaderDayNightParameters` теперь уже не выглядит как amorphous special shader; это layered diffuse/emissive/alpha family with unresolved reveal/light helpers.
- `samplerRevealMap` теперь уже имеет additional engine-lineage support as a reveal/mask-style helper via older Sims shader docs, но exact TS4 visible-pass semantics всё ещё не доказаны.
- `LightsAnimLookupMap` теперь уже выглядит уже, чем `samplerRevealMap`: пока что это narrow day/night or terrain-light lookup helper, а не broad cross-family slot.
- `NextFloorLightMapXform` теперь уже не выглядит как generic unknown texture; это narrow lightmap-transform/helper problem.
- `NextFloorLightMapXform` strongest semantic home сейчас уже выглядит как `GenerateSpotLightmap`, а `SimGhostGlassCAS` скорее как weak carry-through case.
- `CASHotSpotAtlas` теперь уже не выглядит как missing diffuse-like map; его базовая identity как `UV1`-mapped CAS hotspot atlas уже подтверждена, а open gap сузился до его carry-through в render/profile metadata.
- editing/morph branch for `CASHotSpotAtlas` is now clearer: `CASHotSpotAtlas -> HotSpotControl -> SimModifier -> DMap/BGEO/BOND -> GEOM deformation`

## 2. Authoritative CAS and Sim material linkage

Что уже известно:

- `CASP` даёт texture/material-routing fields
- `GEOM` может нести embedded `MTNF`
- `Skintone` и `RegionMap` добавляют routing/compositing inputs
- same-instance heuristics сами по себе нельзя считать универсальной истиной
- there is now a bounded shared input graph: `selected CASP -> linked GEOM -> embedded MTNF or material-definition/field-routing candidates -> RegionMap/SharedUVMapSpace/CompositionMethod/SortLayer -> Sim-only Skintone routing`
- there is now a bounded family split: body foundation shell, head shell, footwear overlay, head-related `Hair/Accessory` slots, and compositor-driven overlay/detail families
- worn-slot families are now better bounded:
  - `Hair` / `Accessory` use exact-part-link first, compatibility fallback second
  - `Shoes` stay in overlay/body-assembly logic
  - explicit companion `MaterialDefinition` resources can arrive through resolved geometry packages
  - skintone routing is currently bounded away from hair/accessory/footwear targets
- shell families are now better bounded too:
  - default/nude shell gating is real in current repo selection logic
  - body shell is the current assembly anchor
  - head shell is a mergeable sibling shell, not a body replacement
  - skintone routing is currently bounded to body/head shell targets
  - parsed `CASP` field-routing is now the current repo material floor for shell families when explicit material definitions do not materialize
  - explicit companion `MaterialDefinition` resources are now better bounded as an upgrade path, not yet a proven universal shell prerequisite
  - explicit shell `MaterialDefinition` evidence is now also known to be asymmetric: strong in generic CAS / worn-slot scene-build coverage and at the composer-level shell merge seam, but still weak in shell-specific end-to-end asset-graph fixtures
  - `MTNF` is now clearly bounded as a real geometry-side material carrier, but current repo shell fixtures rarely exercise it and the current GEOM parser still skips the embedded payload
  - the bundled local `TS4SimRipper` shell-like sample corpus gives an initial prevalence hint: `9/9` checked body/head/waist `.simgeom` samples contain `MTNF`
  - modern TS4 creator-tooling evidence now also supports `MTNF` as behaviorally relevant payload rather than dead chunk metadata: broken MTNF shader-size handling can lead to save/game issues
  - TS4 creator-facing shader practice now also supports preserving GEOM-side shader identity itself: `SimGlass`, `SimSkin`, `SimEyes`, and `SimAlphaBlended` are all used as visible family-level choices, not just internal names
  - local external `TS4SimRipper` code now also supports that separation structurally: `SimGlass` is treated as its own grouped/exported path rather than just another `SimSkin` alias
  - local precompiled shader corpus now adds a first relative-weight hint: `SimSkin`-adjacent names are common in the current snapshot, while `SimGlass` is present but narrow
  - that evidence mix is now strong enough for a bounded implementation-priority hint, but not yet for a final authority table
  - the `P1` packet is now also internally narrower: `SimSkin` is the current safe baseline skin-family seam, while `SimSkinMask` is better bounded as adjacent auxiliary skin-family semantics rather than a proven standalone `GEOM` authority root
  - local repo code also currently fits that bounded reading better than the stronger alternatives: current CAS and Sim paths materialize `ApproximateCas` floors from `CASP` texture refs, geometry companions, region maps, and skintone routing, but do not expose a dedicated `SimSkinMask` authority branch
  - local sample assets now strengthen the asymmetry further: bundled `TS4SimRipper` body/head/waist `.simgeom` files check `9/9` for `SimSkin` shader hash, while no peer asset-level `SimSkinMask` geometry branch has been found in the current repo snapshots
  - external creator tooling also currently frames skin masks as overlay/skin-detail or skintone-adjacent semantics rather than as a separate `GEOM` shader branch
  - the current negative result is now stronger and narrower: the repo/local-external code sweep performed for this pass still finds named `SimSkin` / `SimGlass` branches but no peer named `SimSkinMask` authority/export branch
  - tooling-side corroboration is also now broader: `TS4 Skininator`, `TS4 Skin Converter`, and `Sims 4 Studio` release notes all keep skin masks inside skintone, overlay, burn-mask, or image-mask workflows rather than surfacing a standalone `SimSkinMask` family
  - widening the in-repo asset sweep after that still did not surface a broader local sample family: outside the mirrored `TS4SimRipper` resources, no extra `.simgeom` packet in the current workspace exposes a standalone `SimSkinMask` branch
  - widening the mainstream tooling packet after that also stayed negative: checked `TS4CASTools` and public `TS4SimRipper` sources continue to expose `SimSkin` / `SimGlass` but not a peer named `SimSkinMask` geometry/export path

Что всё ещё открыто:

- приоритет и authority order между `CASP`, embedded `GEOM/MTNF`, `MATD/MTST`, `Skintone`, `RegionMap`
- для каких family groups explicit material definition обязателен, а где field-based routing уже является truth source
- насколько часто body/head shell families реально остаются на parsed `CASP` field-routing floor versus upgrade to explicit `MATD`
- насколько часто shell-specific end-to-end asset-graph paths reach explicit `MATD`, not just composer-level synthetic shell scenes
- где embedded `MTNF` реально меняет authority order внутри shell families, а где остаётся только candidate provenance
- где GEOM-side shader identity itself is authoritative enough to survive into the shared material contract, instead of being flattened into generic diffuse/alpha assumptions
- how the shared contract should rank GEOM-side shader-family identity against decoded `MTNF` params and higher-level `CASP` routing when they point in different directions
- how much current local corpus prevalence should influence implementation priority without being overpromoted into an in-game truth claim
- whether any wider live-asset evidence outside the current local/sample/tool corpus ever justifies promoting `SimSkinMask` from adjacent Sim-skin semantics into a standalone `GEOM` or material-authority branch
- насколько representative этот `9/9` local sample hint для реальных live shell families в более широком corpus
- насколько часто live body/head shell families вообще приходят с non-zero embedded `MTNF`
- как должен выглядеть authority order после появления real repo-side `MTNF` decoding, а не только format-level recognition
- насколько body/head shell versus worn-slot families реально используют один и тот же authority order или расходятся
- насколько все live shell families действительно подчиняются одинаковому material authority order после default/nude shell selection
- насколько часто live `hair/accessory/footwear` families действительно приходят с explicit material definitions versus mostly `CASP` field-routing
- когда fallback к manifest/package-local texture bag допустим, а когда он уже ломает архитектуру

Что бы это закрыло:

- небольшой matrix по реальным asset families:
  - body shell
  - head shell
  - hair
  - shoes
  - accessories
  - skin details / makeup
  - selected Build/Buy object families

Для каждого нужно явно показать источник истины material linkage и границу допустимого fallback.

## 3. Exact skintone compositor math

Что уже известно:

- `Skintone` несёт skin sets, overlay instances, overlay multipliers, hue/saturation, opacity and display order
- reference code already applies a layered skin synthesis model
- `CompositionMethod` and CAS overlays then stack on top of that

Что всё ещё открыто:

- exact in-game blend math for each pass
- exact relation between skin-set overlay multiplier, overlay textures, and pass-specific opacity
- whether current community reference math is visually close or mechanically exact

Что бы это закрыло:

- better comparative fixture work:
  - extract the same Sim skin inputs
  - compare game output, community-tool output, and repo output
  - document which parts are exact vs approximate

## 4. Exact `CompositionMethod` table

Что уже известно:

- `CompositionMethod` is real and compositor-relevant
- `SortLayer` is real and compositor-relevant
- community/reference code consistently treats some values differently:
  - `0` direct overlay-like
  - `1` often used as a tattoo-oriented default in creator tooling
  - `2` makeup-oriented
  - `3` grayscale-shading-oriented
  - `4` second makeup-like path

Что всё ещё открыто:

- exact authoritative meaning of every integer value across all categories and patches
- whether `1` and any rarer values have stable meanings across all categories rather than only common creator practice
- category-specific exceptions

Что бы это закрыло:

- curated table from live assets plus tool behavior plus visual fixture comparison

## 5. Per-family UV routing and transform coverage

Что уже известно:

- UV routing is per sampled map, not per mesh globally
- `CASP` contains `SharedUVMapSpace`
- `GEOM` and `VRTF` prove multi-UV reality
- `DeformerMap` using `UV1` is additional proof that extra UV channels matter in the character pipeline

Что всё ещё открыто:

- complete live-asset table of which families use which UV channel for which map
- complete coverage of slot-specific UV scale/offset behavior
- exact relation between atlas conventions and material-side UV selectors across edge-case content
- whether any remaining `NormalUVBodyType` wording should survive in repo docs at all, or be fully normalized to `SharedUVMapSpace`

Что бы это закрыло:

- fixture matrix of material family -> slot -> UV channel -> transform source

## 6. Exact role of `VPXY` in TS4 render/link graphs

Что уже известно:

- `VPXY` exists in TS4 resource listings as `Model Links`
- its broader "linking/proxy" role is plausible and consistent with older legacy documentation
- the stronger current Build/Buy authority path can already be described without relying on `VPXY`
- `VPXY` now looks better bounded as an object/scene linkage helper, not as a base material-authority node

Что всё ещё открыто:

- strong TS4-specific structural writeup
- exact relationship to `MODL`, `MLOD`, and other object graph helpers in the current game data
- whether it is required for specific render/path families in practice or only for some object ecosystems

Что бы это закрыло:

- TS4-specific code or format documentation showing real object-graph traversal through `VPXY`

## 7. Full Build/Buy family authority order

Что уже известно:

- `MODL -> MLOD` is the strongest documented object mesh path
- `MLOD` binds group -> `VRTF`/`VBUF`/`IBUF`/`SKIN`/`MATD or MTST`
- `Object Definition -> Model/Model LOD -> Material Set -> Material Definition` is now the best-supported base authority order
- `Light` is a parallel resource family for lighting behavior, not evidence that the surface-material chain itself is unknown
- a first `v0` authority/fallback matrix now exists for major Build/Buy, CAS, and Sim family groups

Что всё ещё открыто:

- exact deviations from the base authority order across all Build/Buy families
- which object categories need more than the basic static-model path
- when `VPXY` or other graph helpers materially affect practical scene reconstruction for specific object ecosystems
- complete family-specific authority/fallback coverage for all major CAS and Sim layer types

Что бы это закрыло:

- a per-family object render matrix:
  - static furniture/decor
  - cabinets/counters
  - lights
  - stateful objects

## 8. Full Sim assembly parity

Это всё ещё отдельная эпическая задача.

Что уже известно:

- current project has useful body-first proxy and graph scaffolding
- authoritative Sim assembly needs actual outfit/body-part selection, rig choice, skintone/region-map routing, and modifier/deformer application

Что всё ещё открыто:

- full save/tray-driven character assembly across species, ages, occult forms, and frame variants
- exact application order for `BGEO`, `DMAP`, `BOND`, presets, sculpts, and skin/material synthesis

Что бы это закрыло:

- a source-backed assembled-character pipeline with fixtures from real Sims

## 9. Exact texture-linkage fallback policy

Что уже известно:

- broad package-local fallback is dangerous
- provenance must be preserved
- approximate paths are acceptable only when clearly labeled

Что всё ещё открыто:

- the exact fallback boundary for each asset family when explicit refs are incomplete
- which fallback paths are safe enough to be durable architecture and which are only debugging aids

Что бы это закрыло:

- family-specific fallback policy table with explicit confidence labels

## 10. Engineering-only open questions

Эти вопросы уже не about TS4 render spec, но всё ещё real project questions:

- reliable skipped fixture tests across all runners
- keeping docs, roadmap, and supported-subset claims synchronized
- reducing large orchestration surfaces in app/assets code without losing diagnostics quality

## Working format for any remaining open question

Для каждого нового или оставшегося вопроса добавлять mini-ADR:

```text
Question:
Current evidence:
Safe temporary policy:
What would prove it:
```

Пока доказательства нет:

- не расширять supported subset молча
- не прятать вопрос за fallback’ами
- держать diagnostics честными
