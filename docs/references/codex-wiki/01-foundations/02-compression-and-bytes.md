# Компрессия и чтение байтов

Назначение: зафиксировать безопасные правила работы с compressed и raw resource content.

## Compression types в DBPF

Для The Sims 4 на уровне DBPF index практически важны:

- `0x0000` — uncompressed
- `0x5A42` — zlib
- `0xFFFF` — internal
- `0xFFFE` — streamable
- `0xFFE0` — deleted marker

## Что реально важно для проекта

### Uncompressed
Обычный ресурс, можно читать напрямую.

### Zlib
Самый частый тип сжатия.
Для большинства задач decompression должна происходить автоматически через библиотеку доступа к package.

### Internal
Это кастомный maxis compression format.
В Modders Reference отмечено, что он используется Maxis для string tables / text-oriented данных.

### Deleted
Это не "контент", а маркер deleted entry.
Такие записи нельзя обрабатывать как обычные ресурсы.

### Streamable
В найденных reference-материалах этот формат отмечен как unknown / not used.
Для проекта это должно считаться unsupported until proven otherwise.

## Правило для `sims4browser`

### Для обычного browsing / preview / export:
читать **decompressed** content через package library.

### Для format research / binary inspection:
иметь возможность читать **raw** content без декомпрессии.

Это особенно полезно, если:
- нужно сверять bytes против reverse-engineering notes,
- нужно отлаживать собственный parser,
- нужно понять, где package-library скрывает важную деталь.

## Что даёт LlamaLogic.Packages

`DataBasePackedFile`:
- автоматически читает index при открытии,
- автоматически декомпрессирует resource content при обычном чтении,
- даёт методы `Get`, `GetAsync`,
- даёт `GetRaw`, `GetRawAsync`,
- даёт `ForEach` и `ForEachRaw` для последовательной обработки ресурсов.

### Практический смысл
- `Get(...)` / `ForEach(...)` — нормальный путь для обычного функционала.
- `GetRaw(...)` / `ForEachRaw(...)` — путь для low-level debugging / binary inspection.

## Что НЕ стоит делать

- вручную распаковывать zlib там, где библиотека уже делает это правильно;
- трактовать `deleted` как обычный compression mode;
- смешивать "raw bytes" и "logical content" в одном API без явного naming.

## Рекомендуемая терминология в коде

- `RawContent` = байты как лежат в package
- `Content` / `DecodedContent` = логически доступный decompressed payload
- `DecodedImage`, `DecodedAudio`, `DecodedScene` = следующие semantic stages

## Источники

- DBPF format  
  https://thesims4moddersreference.org/reference/dbpf-format/
- Internal Compression of DBPF Files  
  https://thesims4moddersreference.org/reference/internal-compression-dbpf/
- LlamaLogic.Packages — `DataBasePackedFile`  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
