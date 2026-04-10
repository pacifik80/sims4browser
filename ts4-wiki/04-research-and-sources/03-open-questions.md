# Open questions

Эти вопросы пока нельзя считать закрытыми только по текущим источникам.

## 1. Exact object graph resolution rules for all Build/Buy objects
Что хорошо понятно:
- Object Catalog / Object Definition / Model / ModelLOD существуют и важны.

Что пока не стоит считать полностью решённым:
- универсальное правило linkage для всех object families;
- cross-package linkage во всех случаях;
- stateful objects / multi-state variants.

## 2. Exact MATD / MTST semantics for portable material reconstruction
Есть практические reference points, но:
- material semantics Maxis сложнее простого PBR mapping;
- portable export пока должен оставаться approximation-first.

## 3. Full CAS graph for all asset categories
Что понятно:
- CAS Part / Region Map / Geometry / Rig — core path.

Что пока открыто:
- все category-specific exceptions,
- hair/skin detail edge cases,
- accessories with special behavior,
- occult overrides.

## 4. Full Sim assembly parity with in-game CAS
Это отдельная задача.
Нужны:
- save/tray data,
- preset resolution,
- slider / morph resolution,
- texture compositing,
- body state.

## 5. Streamable compression
В доступных reference-страницах отмечено как unknown / not used.
Пока нельзя считать поддерживаемым.

## 6. Exact texture linkage policy
Особенно для случаев, когда:
- explicit refs неполны,
- package-local fallback "работает на выборке", но не подтверждён форматом.

## 7. Reliable clean skipped fixture tests across all runners
Это уже инженерный вопрос проекта:
- в некоторых xUnit runner/runtime конфигурациях runtime skip markers могут вести себя не так, как ожидается.

---

## Как работать с open questions

Для каждого open question добавлять mini-ADR:

```text
Question:
Current evidence:
Safe temporary policy:
What would prove it:
```

Пока доказательства нет:
- не расширять supported subset молча;
- не прятать вопрос за fallback’ами;
- держать diagnostics честными.
