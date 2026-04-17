# Индексация и lazy enrichment

Назначение: сохранить правила, которые удерживают индексатор быстрым и предсказуемым.

## Главный принцип

Indexing ≠ parsing everything.

Индекс должен быть:
- быстрым,
- cheap,
- устойчивым к миллионам ресурсов,
- ориентированным на browsing/query,
а не на full semantic decode каждого ресурса.

## Что хранить в fast index

### Для raw resources
- source kind (Game / DLC / Mods)
- package path
- type/group/instance
- type name
- preview kind
- export-capable flag
- compressed / uncompressed known flags
- optional deferred name
- optional deferred size

### Для logical assets
- asset domain (Build/Buy / CAS)
- root candidate TGI
- package path
- cheap factual capabilities:
  - has scene root candidate
  - has geometry candidate
  - has rig candidate
  - has material candidate
  - has texture candidate
  - has thumbnail
  - has variants
- coarse category if honestly derivable

## Что НЕ делать в hot path

- `GetNameByKey` для каждого ресурса;
- `GetSize` для каждого ресурса;
- full scene reconstruction для каждого logical asset;
- eager support validation для миллионов assets;
- перерисовку main browser на каждый index batch.

## Deferred enrichment

### Name enrichment
Только когда пользователь:
- открывает details,
- открывает preview,
- делает export,
- или явно требует name.

После enrichment имя нужно кэшировать и сохранять обратно в SQLite.

### Size enrichment
Только если size действительно нужен в UI/export.

### Support/readiness validation
Не делать для всей базы сразу.
Вместо этого:
- validate selected asset,
- validate current visible window,
- cache result.

## Окно результатов важнее total corpus

Для огромных корпусов правильная стратегия:
- query -> total count
- query -> first window
- `Load More`
- bounded background readiness validation только для текущего окна

## Recommended caching layers

1. Persistent SQLite cache:
   - raw resource rows
   - logical asset summaries
   - optional enriched names/sizes
   - optional persisted support readiness

2. In-memory run cache:
   - current window readiness states
   - recently built scenes
   - recent diagnostics
   - thumbnail / texture decode cache

## Правильный компромисс

Если выбор стоит между:
- "показывать всё медленно"
- и "показывать меньше, но честно и быстро"

выбирать второе.

## Для Codex: anti-hallucination rule

Если capability field хранит только cheap factual clue, не превращай его в окончательный verdict без real validation.

Пример:
- `HasSceneRootCandidate = true` ≠ `SceneReady = true`

## Источники

- LlamaLogic `DataBasePackedFile` API and lazy name semantics  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- DBPF format  
  https://thesims4moddersreference.org/reference/dbpf-format/
