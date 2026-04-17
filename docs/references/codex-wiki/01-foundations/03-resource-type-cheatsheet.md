# Cheat sheet по resource types

Ниже — не полный список, а practical subset, который важен для `sims4browser`.

## Build/Buy

| Type ID | Name | Зачем нужен |
|---|---|---|
| `319E4F1D` | Object Catalog | Каталожные данные swatch / tags / flags |
| `B91E18DB` | Object Catalog Set | Наборы каталожных элементов |
| `C0DB5AE7` | Object Definition | Компоненты объекта, footprint, tuning |
| `01661233` | Model | Model root / object mesh container |
| `01D10F34` | Model LOD | LOD-level scene/mesh data |
| `01D0E75D` | Material Definition | Material-related definitions |
| `02019972` | Material Set | Наборы материалов |
| `D382BF57` | Footprint | Геометрия размещения / placement logic |
| `2F7D0004` | PNG Image | Изображения / часть texture pipeline |
| `00B2D882` | DST Image | Texture format |

## CAS

| Type ID | Name | Зачем нужен |
|---|---|---|
| `034AEECB` | CAS Part | Основной metadata resource для CAS item |
| `3C1AF1F2` | CAS Part Thumbnail | Thumbnail |
| `EAA32ADD` | CAS Preset | Presets для CAS |
| `015A1849` | Geometry | Геометрия CAS item |
| `AC16FBEC` | Region Map | Группировка LOD’ов CAS item |
| `2BC04EDF` | LRLE Image | Часто diffuse map для CAS |
| `3453CF95` | RLE 2 Image | Некоторые CAS diffuse/shadow maps |
| `BA856C78` | RLES Image | Texture/specular-related path |
| `067CAA11` | Blend Geometry | Morph/blend geometry |
| `0354796A` | Skintone | Skintone per swatch |
| `9D1AB874` | Sculpt | Sculpt data |
| `C5F6763E` | Sim Modifier | Sim/body modifications |
| `105205BA` | Sim Preset | Full-sim style presets |
| `DB43E069` | Deformer Map | Slider/customization path |
| `8EAF13DE` | Rig | Skeleton / rig |

## Audio

| Type ID | Name |
|---|---|
| `FD04E3BE` | Audio Configuration |
| `01A527DB` | Audio Vocals |
| `376840D7` | AVI |
| `C202C770` | Music Data |

## Animation

| Type ID | Name |
|---|---|
| `02D5DF13` | Animation State Machine |
| `6B20C4F3` | Clip |
| `BC4A5044` | Clip Header |
| `8EAF13DE` | Rig |

## Misc / Full Sim

| Type ID | Name |
|---|---|
| `025ED6F4` | Sim Info |
| `B3C438F0` | Household Template |
| `545AC67A` | Sim Data |
| `0166038C` | Name Map |

## Как использовать этот cheat sheet

### Для индексации
Хранить хотя бы:
- raw `TypeId`
- `TypeName`
- coarse family / domain:
  - Build/Buy
  - CAS
  - Animation
  - Audio
  - Texture
  - Misc

### Для logical asset resolution
Использовать этот список как стартовую карту, но **не как доказательство связей**.

Type ID говорит:
- "что это за ресурс"

Type ID **не** говорит:
- "как именно он связан с другими ресурсами".

## Практические правила

- Build/Buy resolution начинается с `Object Catalog`, `Object Definition`, `Model`, `Model LOD`.
- CAS resolution начинается с `CAS Part`, `Region Map`, `Geometry`, `Rig`, image resources.
- Full Sim / morph logic нельзя собирать только из `CAS Part`.

## Источники

- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- File Types  
  https://thesims4moddersreference.org/reference/file-types/
