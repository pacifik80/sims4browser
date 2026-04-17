# TS4 UV and Material Mapping

Назначение: собрать в одном месте практически полезные правила для надёжного чтения UV, materials и texture linkage в The Sims 4.

Этот файл нужен Codex и разработчику как anti-guessing guide.
Его задача — уменьшить число ложных эвристик вроде «подберём texture по same-instance» или «всегда есть один `uv0` и один diffuse`».

Связанные документы:
- [Build/Buy object pipeline](../02-pipelines/01-buildbuy-object-pipeline.md)
- [CAS part pipeline](../02-pipelines/02-cas-part-pipeline.md)
- [Стратегия scene reconstruction](./02-scene-reconstruction.md)
- [Экспорт и material mapping](./04-export-material-strategy.md)
- [Validation checklist для Codex](../04-research-and-sources/02-validation-checklist-for-codex.md)

---

## 1. Короткая версия: главные правила

1. **Материал берётся из mesh group, а не «по похожему instance».**  
   Для Build/Buy ключевой узел — `MLOD`: он связывает каждый group с `VRTF`, `VBUF`, `IBUF`, `SKIN` и `MATD/MTST`.

2. **UV нельзя хардкодить как `float2 uv0`.**  
   В `VRTF` у vertex element есть `Usage`, `UsageIndex`, `Format`, `Offset`. У UV могут быть packed-форматы, а не только `Float2`.

3. **У разных texture maps может быть разный UV channel.**  
   В shader/material data встречаются `DiffuseMapUVChannel`, `NormalMapUVChannel`, `AmbientMapUVChannel`, `EmissionMapUVChannel`. Нельзя автоматически вешать все карты на один и тот же UV set.

4. **Short-based UV требуют отдельного декодирования.**  
   Для UV-usage в `VRTF` short-based форматы нужно интерпретировать с учётом `MATD` `ShaderData.UVScales`; если такого параметра нет — использовать fallback-нормализацию.

5. **Нужно отделять обычные группы от shadow/state groups.**  
   В `MLOD` есть mesh flags и geometry states; в `RCOL` отдельно есть shadow-oriented `VBUF/IBUF` chunk types. Их нельзя отрисовывать как обычный textured mesh без проверки.

6. **Для preview/export нужен portable material subset, а не полная эмуляция Maxis shader runtime.**  
   Для первого рабочего renderer/exporter достаточно корректно пройти цепочку `mesh group -> material -> texture refs -> UV channels -> alpha/transparency intent`.

7. **Если material path не доказан — asset должен стать `Partial` или `Unsupported`, а не «выглядит похоже».**

---

## 2. Что именно в TS4 отвечает за UV и материалы

### 2.1 Build/Buy

Основные типы ресурсов и чанков:

```text
DBPF package
└─ Object Catalog / Object Definition
   └─ Model (0x01661233)
      └─ Model LOD / MLOD (0x01D10F34)
         ├─ VRTF (0x01D0E723)  -> vertex declaration
         ├─ VBUF (0x01D0E6FB)  -> vertex buffer bytes
         ├─ IBUF (0x01D0E70F)  -> index buffer bytes
         ├─ SKIN (0x01D0E76B)  -> skin controller, if any
         ├─ MATD (0x01D0E75D)  -> material definition
         └─ MTST (0x02019972)  -> material state set
```

Практический смысл:
- `Model` / `Model LOD` описывают object geometry и LOD-структуру.
- `MLOD` — центральный mapping-узел для mesh groups.
- `VRTF` описывает layout вершины.
- `VBUF` содержит реальные байты вершин.
- `IBUF` содержит индексы.
- `MATD` и `MTST` определяют shader parameters и texture linkage.

### 2.2 CAS

Основные типы:

```text
CAS Part (0x034AEECB)
└─ Geometry / GEOM (0x015A1849)
   ├─ inline vertex format + vertex data
   ├─ face/index data
   ├─ optional embedded material hints
   └─ TGI block list
└─ Region Map (0xAC16FBEC)
└─ Rig (0x8EAF13DE)
└─ texture resources (LRLE / RLE2 / RLES / DST / PNG)
```

Практический смысл:
- для CAS важнее `GEOM`, чем `MLOD`;
- в отличие от object `MLOD`, `GEOM` хранит vertex format прямо внутри ресурса;
- `GEOM` может вести себя не совсем как «типичный RCOL», и это нужно учитывать отдельно.

---

## 3. Build/Buy: как правильно идти от объекта к текстурам

### 3.1 Safe path

Для Build/Buy-подмножества используй путь:

```text
Logical asset
-> chosen Model / MLOD
-> each MLOD group
-> resolve VRTF/VBUF/IBUF/SKIN/MATD-or-MTST
-> parse material params
-> resolve map-specific UV channels
-> build canonical material
-> render/export
```

### 3.2 Почему `MLOD` — обязательный источник истины

`MLOD` прямо перечисляет для каждого group:
- `Material`
- `VertexFormat`
- `VertexBuffer`
- `IndexBuffer`
- `Flags`
- `BoundingBox`
- `SkinController`
- `Geometry states`

Это важнейшая вещь: **связи в object mesh не нужно угадывать по package-local grouping, if same-instance и т.п., если есть валидный `MLOD`.**

### 3.3 Private indexes, а не package-wide TGI

В `MLOD` ссылки вроде `Material`, `VertexFormat`, `VertexBuffer`, `IndexBuffer`, `SkinController` — это **private indexes внутри RCOL-контейнера**, а не глобальные package TGI.  
Частая ошибка: интерпретировать эти поля как resource keys и пытаться искать их во всём пакете.

### 3.4 Geometry states и material states

`MLOD` также хранит geometry states. Они позволяют показывать только часть group’а.  
Это означает:
- «mesh выглядит неполным» может быть не ошибкой парсинга, а stateful object behavior;
- для первого preview безопасно использовать whole-group/default-visible geometry;
- но stateful objects нужно маркировать как `Partial`, если state logic игнорируется.

`MTST` — это material state set. Он нужен не только для «burnt», но и вообще для смены material state. Если объект зависит от state switching, а ты берёшь только один material path, это уже частичный preview.

---

## 4. `VRTF`: как читать vertex layout

`VRTF` задаёт declaration вершины. У элемента есть:
- `Usage`
- `UsageIndex`
- `Format`
- `Offset`

Это означает:
- один mesh может иметь несколько UV-каналов;
- порядок полей в вершине не фиксирован;
- размер и тип данных зависят от `Format`, а не от твоих ожиданий.

### 4.1 Практические правила

1. **Сначала распарси весь `VRTF`, потом читай `VBUF`.**
2. **Никогда не предполагай fixed vertex struct.**
3. **Сохраняй все UV sets по `UsageIndex`.**
4. **Сохраняй raw declaration рядом с декодированными данными для диагностики.**

### 4.2 Что особенно важно для UV

На reference page для `VRTF` отдельно указано:
- `UsageIndex` увеличивается, когда один и тот же semantic встречается несколько раз;
- short-based форматы для UV имеют особые правила;
- для UV-usage нужно смотреть `MATD` `ShaderData` `UVScales`.

Следствие: **`uv0`, `uv1`, `uv2` надо строить по `Usage=UV` + `UsageIndex`, а не по порядку появления полей или «первое float2 = diffuse UV».**

---

## 5. UV decoding: где чаще всего ломается mapping

### 5.1 Никогда не ограничивайся `Float2`

Для UV в `VRTF` встречаются как минимум:
- `Float2`
- `Short2`
- `Short4`
- normalised short variants
- другие packed forms

Если ты для всех UV просто читаешь `float u = ReadSingle(); float v = ReadSingle();`, это работает только для части ресурсов.

### 5.2 Специальное правило для UV в short-based форматах

На reference page для `VRTF` зафиксировано:
- `0x06 Short2` и `0x07 Short4`, а также normalised short formats для UV должны интерпретироваться **не так же, как Position/Normal**;
- для UV usage нужно умножать на `MATD` `ShaderData` `UVScales`, элемент 0;
- если такого значения нет — использовать fallback `divide by 32767`.

Из этого следует алгоритм:

```text
if element.usage == UV:
    if format is Float2/Float4-compatible:
        read float uv directly
    else if format is short-based:
        decode short-based values
        if material has UVScales:
            uv = decoded * UVScales[0]
        else:
            uv = decoded / 32767
```

### 5.3 `UsageIndex` важнее «первого/второго UV»

Правильное хранение:

```text
uvSets[(usageIndex)] = decodedUV
```

Неправильное хранение:

```text
mesh.UV0 = first UV field encountered
mesh.UV1 = second UV field encountered only if lucky
```

### 5.4 GEOM и CAS

Для `GEOM` формат вершины хранится иначе, но правило то же: **нельзя предполагать фиксированную структуру вершины**.  
Reference page для `GEOM` говорит, что:
- vertex data читается по списку vertex-format entries;
- порядок элементов может быть произвольным;
- offset считается суммой предыдущих `BytesPerElement`;
- для pets встречается больше одного UV channel.

То есть CAS-ветка тоже должна строить UV channels динамически.

---

## 6. Material path: `MATD`, `MTST` и shader params

### 6.1 Что тебе реально нужно извлекать

Для надёжного preview/export minimum subset должен уметь читать:
- shader/material identity
- `DiffuseMap`
- `NormalMap`
- `SpecularMap`
- `EmissionMap`
- `AmbientOcclusionMap`
- `DiffuseMapUVChannel`
- `NormalMapUVChannel`
- `AmbientMapUVChannel`
- `EmissionMapUVChannel`
- `UVScales`
- `NormalUVScale`
- `NormalMapScale`
- `AlphaMaskThreshold`
- scalar/colour parameters вроде `shininess`, `Diffuse`, `Specular`, если они есть и нужны для portable material

### 6.2 Почему map-specific UV channel selectors критичны

Community reverse engineering shader fields показывает, что different maps могут использовать разные UV channels:
- `NormalMapUVChannel`
- `DiffuseMapUVChannel`
- `AmbientMapUVChannel`
- `EmissionMapUVChannel`

Следствие:
- нельзя применять `uv0` ко всем map types;
- для каждой карты надо отдельно искать правильный UV set.

### 6.3 `MATD` vs `MTST`

Для первой рабочей поддержки полезно разделить случаи:

1. **Есть прямой `MATD` и он разрешим**  
   Это лучший случай. Читай shader params напрямую.

2. **Есть `MTST`, а конкретный material state ещё нужно развернуть**  
   Тогда asset либо:
   - требует material-state resolution, либо
   - идёт в `Partial`, если ты поддерживаешь только default state.

3. **Есть только texture-like fallback без доказанного material path**  
   Это diagnostic fallback, а не надёжный material mapping.

### 6.4 Creator-practice vs spec-backed

В creator practice для простых объектов часто встречаются шейдеры вроде `Phong` и `PhongAlpha`. Это полезно для экспорта в Blender/Unity и для alpha-cutout preview, но **это не повод жёстко кодировать «все object materials = phong-like»**.

---

## 7. Какие texture maps и каналы стоит маппить в portable material

Для первого надёжного exporter/preview достаточно следующей canonical схемы:

```text
PortableMaterial
├─ BaseColor / Diffuse
├─ Normal
├─ Specular
├─ Emissive
├─ AmbientOcclusion
├─ AlphaMode (Opaque / Mask / Blend)
├─ AlphaCutoff
├─ UV channel per map
└─ diagnostic notes
```

### 7.1 Recommended mapping rules

- `DiffuseMap` -> `BaseColor`
- `NormalMap` -> `Normal`
- `SpecularMap` -> `Specular`
- `EmissionMap` -> `Emissive`
- `AmbientOcclusionMap` -> `AmbientOcclusion`
- `AlphaMaskThreshold > 0` -> `AlphaMode = Mask`
- явно прозрачный/alpha shader -> `AlphaMode = Blend`
- неизвестный shader -> `Opaque` + diagnostics, если нет более убедительных признаков

### 7.2 Normal map caveat

Если у материала есть `NormalMapScale` / `NormalUVScale`, это не то же самое, что diffuse UV transform. Их нужно хранить как материал-параметры, а не silently игнорировать.

---

## 8. Shadow meshes, special groups и почему preview может выглядеть «битым»

В `RCOL` отдельно перечислены shadow-related chunks:
- `VBUF 0x0229684B` — shadow mesh vertex buffer без associated `VRTF`
- `IBUF 0x0229684F` — shadow mesh index buffer

А `MLOD` mesh flags включают по крайней мере:
- `DropShadow`
- `ShadowCaster`
- `Pickable`
- и другие

### Практический вывод

Если group имеет shadow-oriented chunk types или shadow flags:
- **не рендери его как обычный textured visible mesh по умолчанию**;
- либо пропускай,
- либо выводи отдельной shadow/debug веткой.

Иначе типичный симптом такой:
- текстура выглядит «сломанной»;
- часть объекта парит в воздухе;
- geometry есть, но смысловой object preview получается абсурдным.

---

## 9. CAS: что отличается от Build/Buy

### 9.1 GEOM — не MLOD

У CAS geometry (`GEOM`):
- собственный vertex format внутри ресурса;
- собственный face/index storage;
- `EmbeddedID` и embedded material hints;
- стандартный `TGI Block List`, а не типичная scoped RCOL reference model.

### 9.2 Что важно для UV/material parsing в CAS

- vertex layout тоже нужно читать динамически;
- `DataType=3` в `GEOM` — UV;
- `DataType=6` — tangent normal;
- pets introduced meshes where more than one UV channel may exist;
- embedded material handling и morph-specific shaders нужно трактовать осторожно.

### 9.3 Что не делать

- не переносить object `MLOD` assumptions напрямую на `GEOM`;
- не предполагать, что у CAS всегда один UV set;
- не предполагать, что material linkage совпадает с Build/Buy object path.

---

## 10. Надёжный алгоритм для Build/Buy preview/export

```text
1. Resolve logical asset to concrete scene root.
2. Pick a supported MLOD / LOD.
3. For each MLOD group:
   a. Read group flags and primitive type.
   b. Skip unsupported primitive types (keep diagnostics).
   c. Skip or special-case shadow groups.
   d. Resolve VRTF/VBUF/IBUF/SKIN/MATD-or-MTST via private indexes.
   e. Parse VRTF declaration.
   f. Decode vertex attributes dynamically.
   g. Build UV sets by UsageIndex.
   h. Resolve material params and texture refs.
   i. For each texture map, choose UV set by *MapUVChannel.
   j. Build canonical submesh + portable material.
4. If at least one visible triangle mesh is reconstructed -> SceneReady or Partial.
5. Otherwise -> Unsupported with exact diagnostics.
```

### Support state suggestion

- `SceneReady` — есть хотя бы один visible triangle mesh + material path достаточно полон для preview/export.
- `Partial` — mesh есть, но material path/state/shader/texture incomplete.
- `Unsupported` — нет валидного visible mesh path.

---

## 11. Минимальная диагностика, которую нужно логировать

Для каждого scene build логируй хотя бы это:

```text
Asset root
Chosen MLOD
Chosen LOD
For each group:
  NameHash
  PrimitiveType
  MeshFlags
  BoundingBox
  VRTF private index
  VBUF private index
  IBUF private index
  MATD/MTST private index
  SKIN private index
  Geometry state count
  Visible? yes/no
  Shadow-like? yes/no
  UV sets discovered: [0,1,...]
  Maps resolved:
    DiffuseMap -> key / missing / deferred
    NormalMap -> key / missing / deferred
    SpecularMap -> key / missing / deferred
  UV channel selection:
    DiffuseMapUVChannel = ?
    NormalMapUVChannel = ?
  Result: Ready / Partial / Unsupported
```

Если Codex не пишет такой лог, он почти наверняка начнёт плодить workaround’ы вместо настоящего разбора.

---

## 12. Антипаттерны, которых нужно избегать

### Нельзя

- искать texture только по `same-instance`;
- считать, что первый UV = diffuse UV для всех карт;
- считать, что у mesh всегда один material;
- считать, что любой `VBUF` имеет `VRTF`;
- считать, что `MATD` всегда есть и всегда сразу usable;
- трактовать `MTST` как «неважно, всё равно возьмём первый diffuse`»;
- silently flip/scale UV без fixture-проверки;
- объявлять asset exportable, если scene root есть, но visible triangle mesh не собрался.

### Можно, но только как fallback с явной пометкой

- fallback texture candidate по same-instance/package-local heuristics;
- default `uv0`, если map-specific UV channel не найден и материал реально не даёт лучших данных;
- default state material из `MTST`, если state system не поддержан.

---

## 13. Примеры кода, которые стоит изучить

### 13.1 Для чтения package / DBPF

**LlamaLogic.Packages**  
Почему полезно:
- современный .NET API для DBPF;
- при открытии читает package index, но не грузит content ресурсов до запроса;
- names ленивые и кэшируются;
- есть `Keys`, `GetNameByKey`, `GetNames`, `GetAllSizes`, `ForEachRaw`.

Ссылки:
- Docs: https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- Repo: https://github.com/Llama-Logic/LlamaLogic

Что смотреть:
- как открывается package;
- как читаются raw/decompressed bytes;
- как организовать lazy enrichment без полного material parsing в index hot path.

### 13.2 Для исследования бинарных форматов

**Llama-Logic/Binary-Templates**  
Почему полезно:
- 010 Editor templates для форматов The Sims 4;
- полезно как reference для reverse engineering `MATD`, `MLOD`, `GEOM`, texture-related chunks.

Ссылки:
- Repo: https://github.com/Llama-Logic/Binary-Templates

Что смотреть:
- templates по типам ресурсов;
- как авторы трактуют поля и внутренние блоки.

### 13.3 Для старых wrappers и community behavior

**s4ptacle/Sims4Tools / s4pe / s4pi**  
Почему полезно:
- исторический reference implementation сообщества;
- есть wrappers, включая вклад в `DATA`, `RIG`, `GEOM`;
- полезно не как прямой dependency, а как source of behavior.

Ссылки:
- Repo: https://github.com/s4ptacle/Sims4Tools
- Releases: https://github.com/s4ptacle/Sims4Tools/releases

Что искать в коде:
- wrappers для `GEOM` / `RIG` / texture resources;
- parsing vertex declarations;
- texture import/export handling.

### 13.4 Для low-level DBPF

**dbpf_reader**  
Почему полезно:
- компактный low-level reader;
- хорошо помогает понять DBPF 2.1 / index 0.3 без тяжёлой обвязки.

Ссылки:
- Repo: https://github.com/ytaa/dbpf_reader

### 13.5 Для full-sim / morph application

**TS4 SimRipper**  
Почему полезно:
- показывает, как community tooling собирает Sim mesh с morphs и composited textures;
- особенно полезно, если дальше пойдёшь в full character.

Ссылки:
- Repo: https://github.com/CmarNYC-Tools/TS4SimRipper
- MTS page: https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html

---

## 14. Примеры кода: safe decoding patterns

Ниже — **авторские** примеры-паттерны, не скопированные из external source.
Их можно адаптировать в `sims4browser`.

### 14.1 Decode UV sets from `VRTF`

```csharp
public sealed record VrElement(byte Usage, byte UsageIndex, byte Format, byte Offset);

public static Dictionary<int, Vector2[]> DecodeUvSets(
    ReadOnlySpan<byte> vertexBuffer,
    int vertexCount,
    int stride,
    IReadOnlyList<VrElement> elements,
    MaterialParams? material)
{
    var uvSets = new Dictionary<int, Vector2[]>();

    foreach (var element in elements.Where(e => e.Usage == 0x02)) // UV semantic
    {
        var channel = (int)element.UsageIndex;
        var values = new Vector2[vertexCount];

        for (var i = 0; i < vertexCount; i++)
        {
            var baseOffset = i * stride + element.Offset;
            values[i] = DecodeUv(vertexBuffer.Slice(baseOffset), element.Format, material);
        }

        uvSets[channel] = values;
    }

    return uvSets;
}

private static Vector2 DecodeUv(ReadOnlySpan<byte> src, byte format, MaterialParams? material)
{
    return format switch
    {
        0x01 => new Vector2(
            BitConverter.ToSingle(src.Slice(0, 4)),
            BitConverter.ToSingle(src.Slice(4, 4))),

        0x06 => DecodeShort2Uv(src, material),
        0x07 => DecodeShort4Uv(src, material),
        0x09 => DecodeShort2NUv(src, material),
        0x0A => DecodeShort4NUv(src, material),
        0x0B => DecodeUShort2NUv(src, material),

        _ => throw new NotSupportedException($"Unsupported UV format 0x{format:X2}")
    };
}

private static Vector2 ApplyUvScale(Vector2 value, MaterialParams? material)
{
    var uvScale = material?.UvScales?.FirstOrDefault();
    return uvScale is null ? value / 32767f : value * uvScale.Value;
}
```

### 14.2 Choose UV channel per map

```csharp
public static int SelectUvChannel(MaterialParams material, TextureSemantic semantic)
{
    return semantic switch
    {
        TextureSemantic.BaseColor => material.DiffuseMapUvChannel ?? 0,
        TextureSemantic.Normal => material.NormalMapUvChannel ?? 0,
        TextureSemantic.AmbientOcclusion => material.AmbientMapUvChannel ?? 0,
        TextureSemantic.Emissive => material.EmissionMapUvChannel ?? 0,
        TextureSemantic.Specular => material.SpecularMapUvChannel ?? material.DiffuseMapUvChannel ?? 0,
        _ => 0
    };
}
```

### 14.3 Build a portable material from TS4 params

```csharp
public static PortableMaterial BuildPortableMaterial(MaterialParams m)
{
    return new PortableMaterial
    {
        Name = m.MaterialName ?? "Unnamed",
        BaseColor = m.DiffuseMap,
        BaseColorUv = m.DiffuseMapUvChannel ?? 0,
        Normal = m.NormalMap,
        NormalUv = m.NormalMapUvChannel ?? 0,
        Specular = m.SpecularMap,
        SpecularUv = m.SpecularMapUvChannel ?? m.DiffuseMapUvChannel ?? 0,
        AmbientOcclusion = m.AmbientOcclusionMap,
        AmbientOcclusionUv = m.AmbientMapUvChannel ?? 0,
        Emissive = m.EmissionMap,
        EmissiveUv = m.EmissionMapUvChannel ?? 0,
        AlphaMode = m.AlphaMaskThreshold is > 0 ? AlphaMode.Mask : AlphaMode.Opaque,
        AlphaCutoff = m.AlphaMaskThreshold,
        Diagnostics = m.Diagnostics.ToArray()
    };
}
```

### 14.4 Build/Buy group filter

```csharp
public static bool IsRenderableVisibleGroup(MlodGroup g)
{
    if (g.PrimitiveType != PrimitiveType.TriangleList)
        return false;

    if (g.MeshFlags.HasFlag(MeshFlags.DropShadow))
        return false;

    if (g.MeshFlags.HasFlag(MeshFlags.ShadowCaster) && g.VertexFormatRef is null)
        return false;

    return g.VertexCount > 0 && g.PrimitiveCount > 0;
}
```

---

## 15. Fixture-based debugging checklist

Когда texture mapping выглядит неправильно, проверь по порядку:

1. Выбран ли правильный `MLOD` / LOD?
2. Не рисуется ли shadow group вместо visible group?
3. Правильно ли разрешён `MATD` / `MTST`?
4. Есть ли `DiffuseMapUVChannel` / `NormalMapUVChannel` и используются ли они?
5. Не декодируются ли short-based UV как простые float?
6. Применяется ли `UVScales`?
7. Не перепутаны ли private indexes и package TGIs?
8. Не потерян ли `UsageIndex` для нескольких UV sets?
9. Не требуется ли geometry state / material state для полного вида объекта?
10. Не была ли выбрана fallback texture candidate вместо реального texture ref из материала?

---

## 16. Что считать доказанным и что считать эвристикой

### Spec-backed / reverse-engineering-backed

- `MLOD` связывает group с `VRTF`/`VBUF`/`IBUF`/`SKIN`/`MATD`
- `VRTF` задаёт `Usage`, `UsageIndex`, `Format`, `Offset`
- short-based UV для UV usage требуют special handling и `UVScales`
- shader data может задавать map-specific UV channels
- `GEOM` читает vertex format динамически
- у pets может быть >1 UV channel
- `MTST` связан с material states

### Heuristic / needs verification

- выбирать first/only material candidate, если `MTST` не развёрнут
- fallback texture по same-instance
- V-axis flip в конкретном renderer/export target
- трактовка неизвестного shader как phong-like
- использование `uv0` для specular, если `SpecularMapUVChannel` не найден

---

## 17. Источники

### Главные reference pages

- The Sims 4 Modders Reference — index  
  https://thesims4moddersreference.org/reference/
- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- MLOD (`0x01D10F34`)  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34
- RCOL overview  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- VRTF (`0x01D0E723`)  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01D0E723
- GEOM (`0x015A1849`)  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849

### Useful community shader/material references

- Shader data field names / reverse-engineering discussion  
  https://modthesims.info/showthread.php?p=5603975
- Material states tutorial and object state discussion  
  https://db.modthesims.info/showthread.php?t=655208
- Object transparency / shader choice discussion  
  https://modthesims.info/showthread.php?t=636311
- Creator guidance mentioning common object shaders (`Phong`, `PhongAlpha`)  
  https://modthesims.info/t/showthread.php?t=610986

### Code and implementation references

- LlamaLogic.Packages docs  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- Llama-Logic/LlamaLogic  
  https://github.com/Llama-Logic/LlamaLogic
- Llama-Logic/Binary-Templates  
  https://github.com/Llama-Logic/Binary-Templates
- s4ptacle/Sims4Tools  
  https://github.com/s4ptacle/Sims4Tools
- ytaa/dbpf_reader  
  https://github.com/ytaa/dbpf_reader
- CmarNYC-Tools/TS4SimRipper  
  https://github.com/CmarNYC-Tools/TS4SimRipper

---

## 18. Operational policy для Codex

Перед любым изменением `BuildBuySceneBuildService`, `PreviewServices`, exporter или CAS parser:

1. Зафиксируй, какой тип asset ты разбираешь: Build/Buy object, CAS part или full sim.
2. Зафиксируй, какая цепочка ресурсов доказана.
3. Выпиши, какие UV channels реально найдены.
4. Выпиши, какие material params реально найдены.
5. Не вводи workaround без пометки:
   - `spec-backed`
   - `reference-code-backed`
   - `fixture-backed`
   - `heuristic / needs verification`
6. Если не можешь доказать texture linkage — оставь asset `Partial`, а не «примерно сработало».
