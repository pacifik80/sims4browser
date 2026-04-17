# Стратегия package reader

Назначение: описать, как безопасно читать `.package` в `sims4browser`.

## Рекомендуемая основа

Для .NET-проекта разумная основа — `LlamaLogic.Packages`.

Почему:
- современная .NET library;
- читает package index при открытии;
- не читает весь content сразу;
- автоматически декомпрессирует;
- имена ресурсов ленивые;
- API thread-safe;
- есть bulk и iteration API.

## Что говорит `DataBasePackedFile`

Полезные свойства:
- `Count`
- `Keys`

Полезные методы:
- `Get`, `GetAsync`
- `GetRaw`, `GetRawAsync`
- `GetAllSizes`, `GetAllSizesAsync`
- `GetNameByKey`, `GetNameByKeyAsync`
- `GetNames`, `GetNamesAsync`
- `ForEach`, `ForEachAsync`
- `ForEachRaw`, `ForEachRawAsync`

## Правильная стратегия чтения

### 1. Для индексирования
Использовать cheap metadata:
- `Keys`
- `Count`
- `Type`
- `Group`
- `Instance`
- package path
- preview kind
- coarse family / domain

### 2. Имена — только lazy
`DataBasePackedFile` прямо отмечает, что resource names определяются по content и индексируются только при первом обращении.

Следствие:
- не дергать `GetNameByKeyAsync` внутри горячего per-resource цикла;
- не требовать name для каждого ресурса при index build;
- name enrichment — on-demand.

### 3. Размеры — bulk или deferred
Если нужен size:
- сначала предпочесть `GetAllSizes` / `GetAllSizesAsync`;
- если это всё равно дорого для hot path — делать deferred enrichment.

### 4. Raw vs decoded
- Normal feature path: decoded/decompressed content
- Low-level debugging path: raw content

### 5. Package processing
На уровне whole-package:
- bounded parallelism между package files;
- без агрессивного nested parallelism внутри package, пока нет доказанного выигрыша.

## Что НЕ делать

- per-resource `GetNameByKeyAsync` в index hot loop;
- per-resource `GetSizeAsync` в index hot loop;
- строить logical asset graph прямо во время чтения index без ограничений;
- тащить full content всех ресурсов просто чтобы "посмотреть что это".

## Если нужен собственный parser

Перед тем как писать parser для конкретного type/chunk:

1. проверить `File Types` / `Resource Type Index`;
2. проверить, нет ли уже support в `LlamaLogic` / `s4pi` / `SimRipper`;
3. проверить 010 binary templates;
4. проверить real fixture package;
5. только потом писать parser.

## Recommended parser policy

Каждый parser должен иметь:
- `CanRead(typeId)`
- `Read(raw/decoded bytes) -> semantic model`
- explicit diagnostics
- version checks
- graceful fallback

## Источники

- LlamaLogic `DataBasePackedFile`  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- LlamaLogic repository / acknowledgements to s4pi  
  https://github.com/Llama-Logic/LlamaLogic
- dbpf_reader  
  https://github.com/ytaa/dbpf_reader
