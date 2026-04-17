# Validation checklist для Codex

Назначение: предотвратить "умные, но неподтверждённые" workaround’ы.

## Перед изменением parser / resolver / exporter Codex обязан ответить на 5 вопросов

### 1. Какой exact subset я поддерживаю?
Пример:
- static Build/Buy furniture/decor with Model root
- adult human CAS hair/top/bottom/shoes with direct package-local skinned Geometry

### 2. Чем подтверждён каждый переход?
Для каждой стрелки в pipeline:

```text
A -> B
```

нужно указать один из статусов:
- `spec-backed`
- `reference-code-backed`
- `fixture-backed`
- `heuristic`

### 3. Что является success condition?
Пример:
- scene has mesh count > 0
- CAS export has skeleton + weights
- texture bundle has at least base color map

### 4. Что является unsupported condition?
Пример:
- no model LOD
- no triangle mesh
- no rig
- no trustworthy linkage
- outside supported subset

### 5. Как это проверяется на local fixture?
Нужен хотя бы один fixture-backed smoke test.

---

## Обязательный workflow для Codex

1. Прочитать:
   - DBPF/TGI
   - relevant pipeline doc
   - reader strategy
   - source map

2. Написать короткий design note:
   - subset
   - proven links
   - risks
   - fallback behavior

3. Реализовать narrow vertical slice.

4. Добавить тесты:
   - logic tests
   - fixture integration test
   - export smoke test

5. Обновить docs:
   - supported-types
   - known-limitations

---

## Красные флаги

Если в решении есть что-то из списка ниже, оно требует review:

- "same instance" используется как универсальное правило связности;
- package-local nearest-candidate используется без diagnostics;
- asset помечается previewable без реальной scene build проверки;
- unsupported asset silently yields empty scene;
- list-level capability label выдаётся за окончательную истину;
- parser читает bytes без version guards;
- hot indexing path трогает names/sizes/content каждого ресурса;
- Codex говорит "probably", "likely" и сразу коммитит это как production logic.

---

## Минимальный quality bar

### Для index changes
- no UI regression
- no hot per-resource name/size lookups
- tests pass

### Для Build/Buy slice
- one real scene-ready object
- visible preview
- export smoke test

### Для CAS slice
- one real skinned asset
- visible preview
- export smoke test

### Для full Sim
- one real saved sim or one honest manual composer path
- morph/preset state visible
- explicit limitations

---

## Review template

При завершении шага Codex должен коротко отчитаться в таком виде:

```text
Supported subset:
Proven transitions:
Heuristics remaining:
Changed files:
Fixture coverage:
Known gaps:
```

Это сильно упрощает review и уменьшает вероятность "архитектурной пены" вместо рабочего вертикального среза.
