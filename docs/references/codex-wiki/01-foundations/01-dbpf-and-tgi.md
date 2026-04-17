# DBPF и TGI

Назначение: зафиксировать минимально надёжную модель контейнера `.package` для The Sims 4.

## Что такое DBPF

DBPF (`Database Package File`) — это контейнер ресурсов.  
С точки зрения формата он работает как key-value store:
- есть **header**,
- затем **resource data**,
- затем **resource index**.

Ключом ресурса выступает **resource key**, обычно в практической работе это **TGI**:
- **Type**
- **Group**
- **Instance**

## Базовая структура файла

Упрощённо:

```text
FILE
├─ HEADER
├─ RESOURCE DATA ...
└─ INDEX
```

Index содержит записи для каждого ресурса:
- type
- group
- instance upper/lower
- offset
- compressed size
- uncompressed size
- compression type

## Важные практические выводы

### 1. Package — это не "ассет"
В одном `.package` могут лежать десятки и тысячи ресурсов разных типов.

### 2. Один logical asset почти всегда = несколько ресурсов
Например:
- Build/Buy object: Object Catalog + Object Definition + Model + Model LOD + material/texture resources
- CAS part: CAS Part + Region Map + Geometry + Rig + textures

### 3. Group и instance нельзя игнорировать
Type alone недостаточен.
Многие ошибки начинаются там, где код ищет "первый ресурс нужного типа".

### 4. Index может использовать constant fields
В DBPF index header может вынести constant type/group/instance-extension в заголовок индекса.
То есть нельзя предполагать, что каждая entry всегда хранит все поля полностью и одинаково.

## Практическая модель для проекта

Для `sims4browser` package reader должен уметь надёжно извлекать из index хотя бы:

- `Type`
- `Group`
- `Instance`
- `Offset`
- `CompressedSize`
- `UncompressedSize`
- `CompressionType`
- `DeletedFlag` / deleted state, если применимо

## ResourceKey / TGI в коде

В коде стоит считать `ResourceKey` первичным идентификатором ресурса.

Полезный формат сериализации для UI/diagnostics:

```text
TTTTTTTT:GGGGGGGG:IIIIIIIIIIIIIIII
```

где:
- `Type` — 8 hex digits
- `Group` — 8 hex digits
- `Instance` — 16 hex digits

## Что НЕ стоит делать

- не выводить logical asset ID напрямую из raw TGI без доказанного правила;
- не связывать ресурсы только по близости в package;
- не считать, что один package = один object;
- не предполагать, что порядок в файле отражает логическую связанность.

## Что стоит делать

- хранить raw TGI в индексе без потерь;
- строить logical relationships отдельным слоем;
- логировать переходы между ресурсами как "доказанные" или "эвристические";
- всегда иметь raw export fallback.

## Источники

- The Sims 4 Modders Reference — DBPF format  
  https://thesims4moddersreference.org/reference/dbpf-format/
- The Sims 4 Modders Reference — Index  
  https://thesims4moddersreference.org/reference/
- LlamaLogic.Packages — `DataBasePackedFile`  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
