# Full Sim и morph pipeline

Назначение: объяснить, почему full character assembly — это отдельная задача, и какие ресурсы для неё нужны.

## Ключевое различие

### CAS part
Это отдельный элемент:
- hair
- top
- bottom
- shoes
- accessory
- skin detail

### Full Sim
Это уже композиция:
- base body
- outfit slots
- swatches
- skin tone
- presets
- body/face sliders
- potentially save-game state

Нельзя считать эти задачи эквивалентными.

## Какие ресурсы становятся важными

Из `File Types` / `Resource Type Index`:

- `CAS Part`
- `Geometry`
- `Region Map`
- `Rig`
- `Skintone`
- `CAS Preset`
- `Sim Preset`
- `Blend Geometry`
- `Deformer Map`
- `Sim Modifier`
- `Sim Info`
- `Household Template`
- `Sim Data`

## Что это означает practically

### Для assembled full Sim недостаточно:
- просто взять один CAS part,
- просто взять один GEOM,
- просто склеить несколько FBX вместе.

Нужно:
1. собрать slot state,
2. собрать swatch/material state,
3. применить preset/morph layer,
4. применить body/face sliders,
5. собрать composited textures,
6. учесть данные save/tray, если нужен конкретный Sim, а не абстрактная ручная сборка.

## Почему SimRipper здесь полезен

TS4 SimRipper — это reference point именно для задачи:
- читать save files,
- находить Sims,
- строить их mesh,
- применять morphs,
- сохранять mesh + composited textures.

Это очень хороший ориентир для:
- full-sim assembly,
- morph application,
- composited texture export.

Но у него GPL-3.0, поэтому использовать его нужно как reference implementation / research source, а не бездумно переносить код в MIT / permissive проект.

## Безопасная roadmap-модель

### Stage A. CAS part export
- отдельные CAS assets
- no sliders
- no presets
- no full sim

### Stage B. Manual character composer
- базовое тело
- слоты одежды
- волосы
- skin tone
- простая ручная сборка

### Stage C. Presets / morphs
- CAS preset
- Sim preset
- Blend Geometry
- Deformer Map
- Sim Modifier

### Stage D. Save/tray ingestion
- Sim Info
- save-game parsing
- конкретный сим из сохранения

## Что НЕ следует обещать Codex’у слишком рано

- "как в игре"
- full in-game CAS parity
- корректный любой slider
- корректный любой occult form
- корректный любой CC preset
- pets / child / toddler support

## Для Codex: hard rule

Если задача касается:
- whole Sim,
- morph sliders,
- presets,
- saved game sim,
- composited outfit,

то она должна считаться **отдельной epic / phase**, а не "маленьким продолжением CAS export".

## Источники

- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- TS4 SimRipper Classic description  
  https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html
- TS4 SimRipper GitHub  
  https://github.com/CmarNYC-Tools/TS4SimRipper
