# Sims 4 Codex Wiki

Эта wiki предназначена для Codex и для разработчика `sims4browser`.
Её цель — сократить число "угадываний" при работе с пакетами The Sims 4 и зафиксировать минимально надёжную модель чтения `.package`/DBPF, Build/Buy-ассетов, CAS-ассетов и связанных экспортных пайплайнов.

## Главные принципы

1. **Не угадывать связи между ресурсами.**  
   Любой переход между ресурсами должен опираться либо на:
   - спецификацию / reference page,
   - reference implementation,
   - либо на воспроизводимую проверку на реальных файлах.

2. **Отделять "контейнер" от "логического ассета".**
   - DBPF / package = контейнер ресурсов.
   - Build/Buy object, CAS part, full Sim = логические сущности поверх нескольких ресурсов.

3. **Сначала узкий рабочий subset, потом расширение coverage.**  
   Для Build/Buy и CAS безопаснее сначала поддерживать только те цепочки, которые реально подтверждены.

4. **Никакой ложной "поддержки".**
   Если scene reconstruction, rig, texture linkage или morph pipeline не доказаны — состояние должно быть `Unsupported` или `Partial`, а не "магически работает".

5. **Для индексации использовать cheap metadata first.**
   Имена, размеры, тяжёлые парсинги и scene validation должны быть ленивыми или оконно-ограниченными.

---

## Структура wiki

### 01. Foundations
- [DBPF и TGI](./01-foundations/01-dbpf-and-tgi.md)
- [Компрессия и чтение байтов](./01-foundations/02-compression-and-bytes.md)
- [Cheat sheet по resource types](./01-foundations/03-resource-type-cheatsheet.md)

### 02. Pipelines
- [Build/Buy object pipeline](./02-pipelines/01-buildbuy-object-pipeline.md)
- [CAS part pipeline](./02-pipelines/02-cas-part-pipeline.md)
- [Full Sim и morph pipeline](./02-pipelines/03-full-sim-and-morphs.md)

### 03. Implementation Guides
- [Стратегия package reader](./03-implementation-guides/01-reader-strategy.md)
- [Стратегия scene reconstruction](./03-implementation-guides/02-scene-reconstruction.md)
- [Индексация и lazy enrichment](./03-implementation-guides/03-indexing-and-lazy-enrichment.md)
- [Экспорт и material mapping](./03-implementation-guides/04-export-material-strategy.md)
- [TS4 UV and Material Mapping](./03-implementation-guides/05-TS4_UV_AND_MATERIAL_MAPPING.md)

### 04. Research and Sources
- [Карта источников и trust levels](./04-research-and-sources/01-source-map.md)
- [Validation checklist для Codex](./04-research-and-sources/02-validation-checklist-for-codex.md)
- [Open questions](./04-research-and-sources/03-open-questions.md)

---

## Как использовать эту wiki в Codex

Перед любой задачей, связанной с package parsing, asset resolution, 3D preview или export:

1. Прочитать:
   - `01-foundations/01-dbpf-and-tgi.md`
   - нужный pipeline-файл из `02-pipelines`
   - `03-implementation-guides/01-reader-strategy.md`
   - `04-research-and-sources/02-validation-checklist-for-codex.md`

2. В design note явно зафиксировать:
   - какой subset поддерживается,
   - какие переходы между ресурсами доказаны,
   - какие данные ленивые / deferred,
   - какие состояния считаются `SceneReady`, `Partial`, `Unsupported`.

3. Любой новый workaround отмечать как:
   - **spec-backed**
   - **reference-code-backed**
   - **fixture-backed**
   - **heuristic / needs verification**

---

## Короткая operational policy для Codex

- Не строить Build/Buy graph только по `same-instance`, если это не подтверждено.
- Не строить CAS graph только по "похоже лежит рядом в package".
- Не использовать тяжёлый per-resource parsing в hot indexing path.
- Не заявлять preview/export support без реальной fixture-проверки.
- Не смешивать Build/Buy, CAS и Full Sim как один и тот же тип задачи.

---

## Рекомендуемое место в репозитории

Эту папку удобно положить в:

```text
docs/codex-wiki/
```

и ссылаться на неё из `README.md`, `architecture.md` и из prompt’ов для Codex.
