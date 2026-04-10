# CODEX_PARSING_RULES

Назначение: **короткий обязательный файл “read this first”** перед любым изменением, связанным с:
- чтением `.package` / DBPF,
- индексированием ресурсов,
- Build/Buy / CAS asset resolution,
- scene reconstruction,
- UV / material / texture mapping,
- preview / export.

Этот документ не заменяет подробную wiki. Он задаёт **жёсткие operational rules**, чтобы Codex не подменял доказанную логику эвристиками.

Связанные документы:
- [TS4 UV and Material Mapping](../03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)
- [CODEX TS4 Render Checklist](./CODEX_TS4_RENDER_CHECKLIST.md)
- [Validation checklist for Codex](./02-validation-checklist-for-codex.md)

Внешние reference sources:
- [The Sims 4 Modders Reference](https://thesims4moddersreference.org/reference/)
- [LlamaLogic.Packages docs](https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html)
- [Sims 4 RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- [Sims 4 MLOD](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34)
- [Sims 4 VRTF](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D0E723)
- [Sims 4 GEOM](https://modthesims.info/wiki.php?title=Sims_4%3AGEOM)
- [Llama-Logic/Binary-Templates](https://github.com/Llama-Logic/Binary-Templates)
- [Sims4Tools / s4pe](https://github.com/s4ptacle/Sims4Tools)
- [dbpf_reader](https://github.com/ytaa/dbpf_reader)
- [TS4 SimRipper](https://github.com/CmarNYC-Tools/TS4SimRipper)

---

## 1. Главный принцип

**Нельзя “угадывать” структуру Sims 4 файлов там, где она должна читаться из формата.**

Если связь между ресурсами, mesh data, UV channels, material maps или state variants не доказана:
- спецификацией,
- reference implementation,
- или воспроизводимой fixture-проверкой,

то она должна быть помечена как `heuristic` и не должна рекламироваться как полноценная поддержка.

---

## 2. Evidence ladder

Каждое решение должно быть помечено одним из уровней доказанности:

1. **spec-backed** — подтверждено спецификацией/format page.
2. **reference-code-backed** — подтверждено изучением рабочей реализации.
3. **fixture-backed** — подтверждено на реальных файлах и логах/тестах.
4. **heuristic** — предположение; допустимо только как fallback с честными diagnostics.

Правило:
- `heuristic` нельзя silently upgrade до “Supported”.
- `heuristic` нельзя использовать как основу архитектуры parser’а.

---

## 3. Что Codex обязан сделать до любого изменения parser’а

Перед кодом письменно ответить:

1. Какой **точный subset** поддерживается этим изменением?
2. Какой **container/resource path** разбирается?
3. Какие переходы между ресурсами доказаны и чем?
4. Какие поля читаются **из формата**, а не из догадки?
5. Что увидит пользователь при failure: `Unsupported`, `Partial`, diagnostics?

Если на любой вопрос ответ “не уверен”, сначала идёт research / logging / fixture-check, а не workaround.

---

## 4. Базовые invariants

### 4.1 Package != asset
- `.package` / DBPF — контейнер ресурсов.
- Build/Buy object, CAS part, full Sim — логические сущности поверх нескольких ресурсов.
- Нельзя смешивать container logic и asset graph.

### 4.2 Type IDs и links важнее same-instance эвристик
- Приоритет: явные ссылки / documented structure / private indexes.
- `same-instance`, package-local grouping, “похожее имя” — только fallback, и только с пометкой `heuristic`.

### 4.3 RCOL private indexes нельзя трактовать как package TGI
Для `MLOD` и родственных форматов ссылки на `Material`, `VertexFormat`, `VertexBuffer`, `IndexBuffer`, `SkinController` часто являются **private RCOL indexes**.

Если код читает их как package-wide TGI без доказательства — это почти наверняка ошибка.

### 4.4 Vertex layout всегда читается динамически
- Не хардкодить позицию UV/normal/tangent полей.
- Читать `Usage`, `UsageIndex`, `Format`, `Offset` из `VRTF` или эквивалентного vertex declaration.
- Internal mesh model должен сохранять **все UV channels**, а не только `uv0`.

### 4.5 Material path должен быть explicit
Для Build/Buy и CAS материалов недостаточно “найти texture рядом”.
Нужно пройти реальную цепочку:
- mesh/group -> material ref (`MATD`/`MTST` или эквивалент)
- material params -> texture refs
- per-map UV channel selectors
- portable material mapping

### 4.6 Heavy parsing запрещён в hot indexing path
На индексации по умолчанию:
- только cheap metadata,
- без полного scene build,
- без per-resource expensive parsing,
- enrichment — ленивый / по запросу / оконно-ограниченный.

---

## 5. Жёсткие “do not do this” правила

Codex **не должен**:

1. Строить Build/Buy graph только по `same-instance`, если формат не подтверждает этот переход.
2. Строить CAS graph только по “ресурсы лежат рядом”.
3. Хардкодить UV как `float2 uv0`.
4. Применять все texture maps к одному UV channel без чтения material selectors.
5. Считать каждый `MLOD` group обычной видимой геометрией: shadow/state groups должны быть распознаны отдельно.
6. Считать, что один найденный texture candidate уже означает корректный material resolution.
7. Маркировать asset как `Supported`, если нет fixture-backed preview/export.
8. Подменять неразобранный parser “guess-based fallback”, не выводя diagnostics.
9. Исключать сложный файл из компиляции вместо исправления поддерживаемого пути.
10. Смешивать задачи `Build/Buy`, `CAS part`, `full Sim` в одну “общую поддержку персонажей/объектов”.

---

## 6. Minimal parser workflow

Любое изменение parser’а или resolver’а делается в таком порядке:

1. **Выбрать узкий subset.**
2. **Зафиксировать golden path** в design note.
3. **Добавить минимальный structured logging** по ключевым refs/fields.
4. **Проверить на локальной fixture**.
5. **Только после этого** подключать к preview/export/index.
6. На unsupported ветках вернуть `Partial`/`Unsupported` + diagnostics.

Нельзя начинать с “давайте сразу поддержим все объекты/все CAS”.

---

## 7. Golden paths

### 7.1 Build/Buy

```text
Logical Build/Buy asset
-> Object metadata (if really resolvable)
-> Model
-> chosen MLOD / ModelLOD
-> group refs
   -> VRTF
   -> VBUF
   -> IBUF
   -> SKIN (if present)
   -> MATD or MTST
-> material params
-> texture refs
-> UV selectors
-> canonical scene
-> preview/export
```

### 7.2 CAS part

```text
Logical CAS asset
-> CAS Part
-> Geometry / GEOM
-> Rig / skeleton refs
-> material / texture refs
-> UV channels from vertex declaration
-> canonical skinned scene
-> preview/export
```

### 7.3 Full Sim

Это отдельный pipeline:
- Save/Tray/Sim data,
- CAS presets,
- Sim modifiers,
- morph/sculpt/deformer data,
- assembly logic.

Если задача не про это — не притворяться, что CAS part уже решает full Sim.

---

## 8. Minimal logging that must exist

Перед тем как “чинить” texture mapping или broken mesh, Codex должен уметь вывести минимум:

### Build/Buy / scene groups
```text
Asset={slug}
Root={type}:{group}:{instance}
ChosenLOD={...}
Group={index/nameHash}
PrimitiveType={...}
Flags={...}
VRTF={...}
VBUF={...}
IBUF={...}
SKIN={...}
MaterialRef={...}
MaterialResolvedTo={MATD|MTST|none}
VertexCount={...}
IndexCount={...}
```

### UV / material
```text
UV element: usage={...} usageIndex={...} format={...} offset={...}
Map={Diffuse|Normal|Emission|AO|Specular} -> UVChannel={...}
UV decode mode={Float2|Short2+UVScales|fallback}
TextureRef={...}
```

Если такого лога нет, parser change обычно превращается в угадывание.

---

## 9. Правила для preview/export

### 9.1 Preview
- `SceneReady` только если scene реально построена.
- Если scene не построена — diagnostics должны быть явными.
- Пустой viewport без причины недопустим.

### 9.2 Export
- Экспорт включается только для реально экспортируемого scene.
- `FBX + Textures + manifests` должны строиться из **того же canonical scene**, что и preview.
- Нельзя иметь отдельный “preview parser” и отдельный “export parser”, если этого можно избежать.

### 9.3 Status values
Использовать хотя бы такие состояния:
- `Unknown`
- `SceneReady`
- `Partial`
- `Unsupported`

И у каждого `Partial` / `Unsupported` должна быть краткая причина.

---

## 10. Что читать перед правками

### Если меняется package reader / indexing
- [LlamaLogic.Packages docs](https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html)
- [The Sims 4 Modders Reference — DBPF format](https://thesims4moddersreference.org/reference/dbpf-format/)
- [dbpf_reader](https://github.com/ytaa/dbpf_reader)

### Если меняется Build/Buy scene reconstruction
- [Sims 4 RCOL](https://modthesims.info/wiki.php?title=Sims_4%3ARCOL)
- [Sims 4 MLOD](https://modthesims.info/wiki.php?title=Sims_4%3A0x01D10F34)
- [TS4 UV and Material Mapping](../03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)
- [Llama-Logic/Binary-Templates](https://github.com/Llama-Logic/Binary-Templates)

### Если меняется CAS parser
- [Sims 4 GEOM](https://modthesims.info/wiki.php?title=Sims_4%3AGEOM)
- [The Sims 4 Modders Reference — File Types](https://thesims4moddersreference.org/reference/file-types/)
- [TS4 SimRipper](https://github.com/CmarNYC-Tools/TS4SimRipper)

---

## 11. Ultra-short PR checklist

Перед завершением любой parser/render/export задачи проверить:

- [ ] Supported subset явно указан.
- [ ] Golden path описан.
- [ ] Все resource transitions имеют evidence level.
- [ ] Нет нового major heuristic без честной пометки.
- [ ] Есть fixture-backed verification или явно описанный gap.
- [ ] Unsupported/Partial paths выдают diagnostics.
- [ ] Preview/export используют один canonical scene path.
- [ ] Документация обновлена честно, без overstating support.

---

## 12. One-sentence policy

**Сначала докажи путь чтения формата, потом строй feature; не наоборот.**
