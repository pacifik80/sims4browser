# CAS part pipeline

Назначение: зафиксировать безопасный минимальный путь для preview/export отдельных CAS parts.

## Что известно из reference-материалов

Из `File Types`:
- `CAS Part` содержит флаги и metadata о CAS item.
- `Geometry` содержит physical properties CAS item.
- `Region Map` содержит grouped information about a CAS item’s LODs.
- `LRLE`, `RLE 2`, `RLES` — image resources, используемые в texture pipeline CAS.
- `Blend Geometry`, `Deformer Map`, `CAS Preset`, `Sim Preset`, `Sim Modifier`, `Sculpt` относятся к presets / morph / slider path.

Из `Resource Type Index`:
- `CAS Part` = `034AEECB`
- `Geometry` = `015A1849`
- `Region Map` = `AC16FBEC`
- `Rig` = `8EAF13DE`
- `Blend Geometry` = `067CAA11`
- `Skintone` = `0354796A`

Из MTS `Body Geometry - GEOM`:
- `GEOM` — это RCOL chunk для body geometry;
- в нём есть vertex description, vertex data, faces/index-like data;
- есть ссылки на skin controller / связанные структуры.

## Безопасная рабочая модель

```text
CAS Part
   ↓
Region Map
   ↓
Geometry (GEOM)
   ↓
Rig
   ↓
textures (LRLE / RLE2 / RLES / DST / PNG where applicable)
```

## Поддерживаемый subset для первого CAS vertical slice

Реалистично поддерживать только:
- human CAS parts
- adult / young adult
- hair
- full body
- top
- bottom
- shoes

И только тогда, когда:
1. есть `CAS Part`;
2. резолвится package-local / explicit path к `Geometry`;
3. geometry skinned;
4. есть пригодный `Rig`;
5. texture candidates разрешаются явно или честным package-local fallback.

## Что НЕ надо смешивать с этим pipeline

Не включать сюда:
- full assembled Sim
- sliders / body shape editor
- saved Sim из save/tray
- occult-specific overrides
- pet pipelines

## Минимально нужные данные для canonical scene

Заполнять:
- positions
- indices
- submeshes/material slots
- UVs
- normals
- tangents if available
- skeleton / bones
- bind pose
- skin weights
- bounds

## Что считать успехом

### `SceneReady`
Есть skinned mesh + rig, достаточные для viewport и FBX export.

### `Partial`
Есть geometry, но:
- rig incomplete,
- materials/textures incomplete,
- или preview возможен только в reduced form.

### `Unsupported`
Нет надёжного path к geometry/rig, либо asset вне поддерживаемого subset.

## Preview policy

Если isolated CAS part визуально бесполезен:
- допустимо показывать его на нейтральном mannequin/base body,
- но это должно быть только preview aid,
- export по умолчанию должен оставаться scoped к выбранному part.

## Важные морфы и presets

Следующие типы не обязательны для первого CAS part export, но важны для дальнейшего развития:
- `Blend Geometry`
- `Deformer Map`
- `CAS Preset`
- `Sim Preset`
- `Sim Modifier`
- `Sculpt`
- `Skintone`

Их нужно рассматривать как **следующий слой**, а не как часть MVP для простого part export.

## Для Codex: anti-hallucination rules

- Нельзя считать любой `Geometry` из package принадлежащим выбранному `CAS Part`.
- `Region Map` — это grouping/LOD clue, но его linkage нужно подтверждать.
- Если weights/rig не найдены — не выдавать asset за полноценно exportable skinned scene.
- Full Sim нельзя строить только из `CAS Part` + `Geometry`.

## Источники

- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- Body Geometry - GEOM  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849
- TS4 SimRipper reference point for full assembled sims  
  https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html
