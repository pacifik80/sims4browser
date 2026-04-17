# CODEX TS4 Render Checklist

Назначение: короткий, жёсткий checklist для Codex перед любым изменением, связанным с:
- Build/Buy preview/export
- CAS preview/export
- UV decoding
- material resolution
- texture linkage
- scene reconstruction

Этот файл не заменяет подробные документы. Он нужен как **операционный guardrail**: пройти по шагам, зафиксировать факты, не изобретать лишние workaround’ы.

Связанные документы:
- [TS4 UV and Material Mapping](../03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)
- [Стратегия scene reconstruction](../03-implementation-guides/02-scene-reconstruction.md)
- [Build/Buy object pipeline](../02-pipelines/01-buildbuy-object-pipeline.md)
- [CAS part pipeline](../02-pipelines/02-cas-part-pipeline.md)
- [Экспорт и material mapping](../03-implementation-guides/04-export-material-strategy.md)

---

## 0. Stop rules

Перед любым кодом ответь письменно на 5 вопросов.
Если хотя бы на один ответ — «не знаю», не добавляй эвристику как будто она уже доказана.

1. Какой **точный subset** поддерживается в этом изменении?
2. Откуда берётся **scene root**?
3. Чем доказана **цепочка переходов между ресурсами**?
4. Чем доказана **цепочка material -> texture -> UV channel**?
5. Что будет показано пользователю, если этот путь не сработает: `Unsupported`, `Partial` или diagnostics?

Если ответ — «мы попробуем по same-instance / package-local grouping / похожему имени», это должно быть явно отмечено как heuristic и не должно маскироваться под полноценную поддержку.

---

## 1. Golden paths

### 1.1 Build/Buy

Используй только такой путь, пока не доказан другой:

```text
Logical Build/Buy asset
-> Object metadata (если реально разрешается)
-> Model
-> chosen MLOD / ModelLOD
-> for each group:
   -> VRTF
   -> VBUF
   -> IBUF
   -> SKIN (if present)
   -> MATD or MTST
-> material params
-> texture refs
-> UV channel selectors
-> canonical scene
-> preview/export
```

### 1.2 CAS part

```text
Logical CAS asset
-> CAS Part
-> Geometry / GEOM
-> Rig / skeleton refs
-> material / texture refs
-> UV channels from GEOM vertex format
-> canonical skinned scene
-> preview/export
```

### 1.3 Full Sim / morphs

Это **отдельная задача**. Не смешивать с CAS part.
Нужны дополнительные сущности:
- Save/Tray/Sim data
- CAS presets
- Sim modifiers
- Blend geometry / sculpt / deformer data

Если изменение не про это — не притворяться, что partial CAS part path уже решает full-Sim.

---

## 2. Build/Buy checklist

### 2.1 Resolve the correct root

Перед scene reconstruction зафиксируй:
- package path
- root TGI
- type name
- выбранный LOD
- почему именно этот LOD выбран

Минимальный лог:

```text
BuildBuy asset {slug}
Root={type}:{group}:{instance}
ChosenLOD={...}
Reason={explicit link | only candidate | heuristic fallback}
```

### 2.2 Never treat MLOD references as package-wide keys

В `MLOD` поля `Material`, `VertexFormat`, `VertexBuffer`, `IndexBuffer`, `SkinController` — это обычно **private RCOL indexes**, а не package TGI.

Проверка перед кодом:
- я читаю их как private index?
- у меня есть mapping private index -> chunk/resource внутри текущего container?

Если нет — не продолжать как будто это доказано.

### 2.3 For every group, dump the minimum required facts

Для каждого mesh group логировать минимум:
- group index / name hash if available
- primitive type
- mesh flags
- VRTF ref
- VBUF ref
- IBUF ref
- SKIN ref
- material ref and whether it resolved to MATD or MTST
- vertex count
- index count

Если этого лога нет, отладка texture/UV ошибок почти всегда превращается в угадывание.

### 2.4 Filter out non-main render groups when needed

Проверяй и отдельно отмечай:
- shadow groups
- drop shadow groups
- shadow caster groups
- state-specific geometry

Правило:
- для first-pass preview обычного объекта такие группы лучше исключать или рендерить отдельно,
- но exclusion должен быть отражён в diagnostics / metadata.

### 2.5 Validate primitive type before mesh build

Если group не triangle list / triangle-compatible:
- не строить произвольный mesh “на удачу”;
- отметить как `Unsupported` или `Partial`.

---

## 3. CAS checklist

### 3.1 Separate CAS part from full assembled Sim

Перед кодом явно зафиксируй:
- это isolated CAS part?
- это preview on mannequin/base body?
- это попытка full assembly?

Не смешивать это в одном флаге `CasSupported=true`.

### 3.2 Do not assume GEOM layout is fixed

Для GEOM:
- читать vertex format entries динамически;
- offsets считать по спецификации / format entries;
- UV channels строить по semantic/index, а не по “первый float2”.

### 3.3 Skinning must be explicit

Если это skinned CAS part, то перед export/preview должны быть явно подтверждены:
- skeleton / bone table
- bind pose or equivalent
- weights / bone indices

Если одного из этих блоков нет, не маркировать asset как fully exportable skinned mesh.

---

## 4. UV checklist

### 4.1 Never hardcode uv0 as a single float2 field

Перед чтением UV ответь:
- откуда я знаю semantic (`Usage`) этого элемента?
- откуда я знаю `UsageIndex`?
- откуда я знаю `Format`?
- откуда я знаю `Offset`?

Если ответ — «по позиции поля в struct» — это ненадёжно.

### 4.2 Preserve all UV channels

Минимальное требование к internal model:

```csharp
Dictionary<int, Vector2[]> UvSets;
```

или эквивалентная структура.

Нельзя терять `UsageIndex` уже на этапе parse.

### 4.3 Special handling for short-based UV formats

Если UV format short-based / packed:
- декодировать как short-based UV, а не как position/normal;
- применять `UVScales`, если они доступны в material/shader data;
- если `UVScales` нет, использовать documented fallback-нормализацию;
- логировать, какой режим нормализации применён.

Минимальный лог:

```text
UV decode: usageIndex=0 format=Short2 mode=UVScales[0]
```

или

```text
UV decode: usageIndex=0 format=Short2 mode=fallback_div_32767
```

### 4.4 Map-specific UV channels are mandatory

Для каждой texture map проверять её собственный UV selector, если он есть:
- `DiffuseMapUVChannel`
- `NormalMapUVChannel`
- `AmbientMapUVChannel`
- `EmissionMapUVChannel`

Нельзя делать так:

```text
all maps -> UV0
```

если материал явно хранит разные selectors.

### 4.5 Per-map binding model

Portable material model должен хранить не только texture ref, но и UV set binding:

```csharp
public sealed class PortableTextureSlot
{
    public string SlotName { get; init; } = "";
    public ResourceKey? TextureKey { get; init; }
    public int? UvChannel { get; init; }
    public Vector2? UvScale { get; init; }
    public string? Notes { get; init; }
}
```

Если твоя текущая material model не умеет хранить `UvChannel`, она уже недостаточна для корректного TS4 mapping.

---

## 5. Material checklist

### 5.1 First resolve MATD/MTST, only then textures

Правильный порядок:

```text
group -> MATD/MTST -> shader params -> texture refs -> map-specific UV channels
```

Неправильный порядок:

```text
group -> same-instance texture candidates -> best guess diffuse
```

Same-instance fallback допустим только как явно помеченный heuristic для `Partial`.

### 5.2 Minimum material subset for a reliable first renderer/exporter

Поддерживай хотя бы:
- Diffuse / BaseColor
- Normal
- Specular
- Emission
- Ambient Occlusion (если реально извлекается)
- alpha / transparency intent
- alpha mask threshold
- UVScales
- map-specific UV channels

### 5.3 Keep shader fidelity claims honest

Не обещать:
- “полное совпадение с Maxis shaders”

Можно обещать:
- “portable approximation for Blender/Unity”
- “best-effort TS4 material reconstruction”

### 5.4 Material state vs default state

Если объект использует material states:
- отметь, какой state выбран по умолчанию;
- если state logic не реализована, asset должен быть `Partial`.

---

## 6. Texture checklist

### 6.1 Texture resolution order

Используй такой порядок принятия решений:

1. Явные texture refs из resolved material path.
2. Secondary explicit refs from the same resolved material/state path.
3. Documented fallback path.
4. Same-instance/package-local heuristic — только как последний шаг и только с пометкой `heuristic`.

### 6.2 Texture resource type matters

До рендера/экспорта зафиксируй:
- resource type
- dimensions if known
- decode path
- whether alpha is present

Для image resources не притворяйся, что все они одинаковы.
TS4 использует как минимум:
- PNG Image
- DST Image
- LRLE
- RLE2
- RLES

### 6.3 Per-slot diagnostics

Если texture slot не resolved, логировать именно slot-level failure:

```text
Material slot 2: NormalMap unresolved (selector=1)
```

а не общий текст “materials incomplete”.

---

## 7. Scene build checklist

### 7.1 Canonical scene must preserve source facts

При сборке canonical scene не теряй:
- source TGI / chunk ids
- group index
- material slot id
- chosen UV channel per texture slot
- diagnostics per mesh/group/material

### 7.2 SceneReady must mean something strict

Рекомендуемое правило:
- `SceneReady` = есть хотя бы один валидный render mesh + базовая material/texture привязка или честный материал без текстур;
- `Partial` = mesh есть, но material/texture path неполный, state logic не реализована, или часть groups отфильтрована;
- `Unsupported` = mesh не собран, primitive unsupported, material path не резолвится вообще, или ключевые зависимости отсутствуют.

### 7.3 Do not validate millions of assets eagerly

Scene readiness нельзя проверять для всей базы на каждый index pass.
Делай так:
- validate visible window / selected asset;
- cache the result;
- persist if practical.

---

## 8. Diagnostics checklist

### 8.1 Minimum diagnostics block for every selected asset

Показывать пользователю и писать в logs:
- Support state
- Root resource
- Chosen LOD / geometry root
- Mesh count
- Vertex count
- Index count
- Material slot count
- Texture reference count
- Bounds
- Short reason summary
- Detailed diagnostics lines

### 8.2 Failure categories must be specific

Используй конкретные причины:
- no scene root resolved
- MLOD group primitive unsupported
- private material index unresolved
- VRTF missing
- VBUF missing
- IBUF missing
- no triangle mesh reconstructed
- material resolved but no texture refs
- map UV selector points to missing UV channel
- texture decode failed

Не использовать общий `preview failed` там, где можно дать точную причину.

---

## 9. Anti-patterns: what Codex must NOT do

Запрещённые shortcut’ы:

1. Подставлять текстуры только потому, что `instance` похож.
2. Считать, что `MLOD.Material` — это package-wide TGI.
3. Считать, что UV всегда `Float2`.
4. Вешать все texture maps на `UV0`.
5. Игнорировать `UsageIndex`.
6. Экспортировать skinned CAS как обычный static mesh без явной пометки.
7. Рендерить shadow/drop-shadow groups как обычные materials без отдельного решения.
8. Считать любой Build/Buy asset `Previewable`, если subset label совпал, но scene reconstruction реально не прошла.
9. Лечить mapping баги random flip’ами `U/V` без доказательства.
10. Молча падать обратно на эвристику без записи этого факта в diagnostics.

---

## 10. Fixture validation checklist

Перед merge изменений по UV/material/render обязательно проверить на реальных fixture-ассетах:

### 10.1 Minimum Build/Buy fixture set

Хотя бы по одному объекту:
- простой статический decor/furniture object
- объект с несколькими material slots
- объект с alpha/transparency
- объект, у которого есть unsupported/partial path

### 10.2 Minimum CAS fixture set

Хотя бы по одному asset’у:
- skinned hair or clothing item
- asset с несколькими texture maps
- asset, который должен стать `Partial` или `Unsupported`

### 10.3 For every fixture record

Сохраняй в test notes / diagnostics snapshot:
- package path
- root TGI
- scene-ready status
- expected mesh count
- expected texture slot names
- expected UV channel bindings if known
- whether export produced FBX + textures + manifests

---

## 11. Minimal code patterns Codex should use

### 11.1 Vertex declaration first, raw bytes second

```csharp
foreach (var element in vertexDeclaration.Elements)
{
    switch ((element.Usage, element.UsageIndex, element.Format))
    {
        case (VertexUsage.Position, 0, VertexFormat.Float3):
            // decode position from element.Offset
            break;
        case (VertexUsage.UV, var uvIndex, var format):
            // dispatch to dedicated UV decoder
            break;
        case (VertexUsage.Normal, 0, var normalFormat):
            // decode normal using format-specific logic
            break;
    }
}
```

### 11.2 Dedicated UV decoder

```csharp
private static Vector2 DecodeTs4Uv(
    ReadOnlySpan<byte> vertexBytes,
    VertexElement element,
    PortableMaterial? material,
    ILogger log)
{
    return element.Format switch
    {
        VertexFormat.Float2 => ReadFloat2(vertexBytes, element.Offset),
        VertexFormat.Short2 => DecodeShort2Uv(vertexBytes, element.Offset, material, log),
        VertexFormat.Short4 => DecodeShort4Uv(vertexBytes, element.Offset, material, log),
        _ => throw new NotSupportedException($"Unsupported UV format: {element.Format}")
    };
}
```

### 11.3 Per-map UV binding

```csharp
private static int ResolveUvChannel(MaterialShaderData shader, string slotName)
{
    return slotName switch
    {
        "Diffuse" => shader.DiffuseMapUvChannel ?? 0,
        "Normal" => shader.NormalMapUvChannel ?? shader.DiffuseMapUvChannel ?? 0,
        "AmbientOcclusion" => shader.AmbientMapUvChannel ?? shader.DiffuseMapUvChannel ?? 0,
        "Emission" => shader.EmissionMapUvChannel ?? shader.DiffuseMapUvChannel ?? 0,
        _ => 0,
    };
}
```

### 11.4 Explicit partial/unsupported state

```csharp
if (scene.Meshes.Count == 0)
{
    return AssetPreviewResult.Unsupported(
        "No triangle meshes could be reconstructed from the resolved MLOD groups.");
}

if (scene.Meshes.Any(m => m.Material is null))
{
    return AssetPreviewResult.Partial(
        scene,
        "Scene mesh was reconstructed, but one or more material paths are unresolved.");
}
```

---

## 12. External resources Codex should study

### 12.1 Primary reference docs

1. The Sims 4 Modders Reference
   - DBPF format
   - File types
   - Resource type index
   - Internal compression

2. Mod The Sims / SimsWiki
   - RCOL overview
   - MLOD page
   - VRTF page
   - GEOM page
   - Packed file types

### 12.2 Primary code/reference implementations

1. `LlamaLogic.Packages`
   - modern .NET package reader
   - good source for package/index/resource access patterns
   - use it as package/container reference, not as proof for higher-level asset graphs unless code really shows that

2. `Llama-Logic/Binary-Templates`
   - useful for inspecting binary structure in 010 Editor
   - especially relevant for package, geometry, clip, and related binary layouts

3. `s4pe` / `s4pi` / Sims4Tools
   - useful as historical/reference implementation for TS4 wrappers and format exploration
   - treat as reference, not automatic truth
   - mind GPL implications before copying logic/code

4. `dbpf_reader`
   - useful low-level DBPF/container reference when debugging package structure

5. `TS4 SimRipper`
   - useful reference for full-Sim assembly, morph application, and save-game driven extraction
   - especially relevant once the project goes beyond isolated CAS parts

### 12.3 What to search for in those codebases

When Codex needs concrete examples, search by these symbols/terms:
- `MLOD`
- `MODL`
- `GEOM`
- `MATD`
- `MTST`
- `VRTF`
- `VBUF`
- `IBUF`
- `SKIN`
- `DiffuseMapUVChannel`
- `NormalMapUVChannel`
- `UVScales`
- `VertexFormat`
- `UsageIndex`
- `BlendGeometry`
- `Sculpt`

Do not search only by “texture” or “mesh”; search by TS4-native format names.

---

## 13. Research log template for every rendering bug

При каждом баге по preview/export фиксируй минимум:

```text
Asset:
Package:
Root TGI:
Subset:
Chosen geometry root:
Chosen LOD:
Group count:
Per-group material refs:
Per-group UV formats:
Per-map UV channels:
Texture refs resolved:
Observed symptom:
Expected symptom:
Current hypothesis:
Evidence type: spec-backed | reference-code-backed | fixture-backed | heuristic
```

Если этого шаблона нет, не продолжать “поправки на глаз”.

---

## 14. Final go/no-go questions before merge

Перед merge ответь на 7 вопросов:

1. На каком fixture это проверено?
2. Где доказан route от logical asset до geometry root?
3. Где доказан route от mesh group до material?
4. Где доказан route от material до texture refs?
5. Где доказан выбор UV channel для каждой карты?
6. Как приложение показывает `Partial`/`Unsupported`?
7. Что останется работать, если текущий path не сработает?

Если хотя бы один пункт без ответа — не заявляй “support added”.
