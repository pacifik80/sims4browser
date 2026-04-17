# Build/Buy object pipeline

Назначение: зафиксировать минимально надёжный путь от package resources к preview/export Build/Buy объекта.

## Что известно из reference-материалов

Из `File Types`:
- `Object Catalog` содержит данные о конкретном swatch Build/Buy item, такие как tags и flags.
- `Object Definition` содержит данные о конкретном swatch Build/Buy item: components, linked footprint, tuning used.
- `Model` / `Model LOD` содержат информацию о physical properties Build/Buy item.

Из RCOL/MTS:
- `.model` files содержат `MODL` chunk.
- `MODL` ссылается на `MLOD`.
- в scene/mesh pipeline встречаются `MODL`, `MLOD`, `MATD`, `MTST`, а внутри geometry path — vertex/index related blocks.

## Безопасная ментальная модель

```text
Object Catalog / Object Definition
        ↓
      Model
        ↓
    Model LOD
        ↓
 vertex/index data
        ↓
 material references
        ↓
 texture resources
```

Это безопасная **рабочая модель**, но не гарантия, что любой объект будет точно следовать этому пути в одной package и без особых случаев.

## Что считать поддерживаемым subset

Для первого честного Build/Buy slice поддерживать только объекты, у которых подтверждается:

1. есть `Model` root;
2. из него резолвится хотя бы один `Model LOD`;
3. из `Model LOD` получается triangle mesh;
4. материал/текстуры можно найти явно или package-local безопасным fallback;
5. нет skinning/animation path;
6. объект не требует stateful / multi-state логики для базового preview.

Практически это означает:
- static furniture/decor objects
- no moving parts
- no complex state machine objects
- no rigged/skinned mesh path

## Что НЕ считать доказанным

- что `same-instance` всегда означает linkage;
- что любой `Object Definition` из того же package относится к выбранному `Model`;
- что material resources всегда package-local;
- что thumbnail, catalog entry и mesh всегда находятся в одной package;
- что absence of explicit material link можно безопасно заменить случайным candidate из package.

## Рекомендуемый resolution order

### Stage 1. Найти identity layer
Попробовать привязать:
- Object Catalog
- Object Definition
- thumbnail / naming metadata

Но отсутствие identity metadata **не должно блокировать** scene build, если есть доказанный `Model` path.

### Stage 2. Найти scene root
Предпочтительно:
- `Model`

### Stage 3. Извлечь geometry
Предпочтительно:
- `Model -> Model LOD -> mesh blocks`

### Stage 4. Извлечь materials/textures
Предпочтительно:
- явные material refs
- затем явные texture refs
- только потом, если нет лучшего варианта и это специально задокументировано, очень узкий package-local fallback

### Stage 5. Build canonical scene
Заполнить:
- positions
- indices
- submeshes/material slots
- UVs
- normals
- tangents if available
- bounds

## Правильные статусы результата

### `SceneReady`
Сцена построена, есть хотя бы один пригодный mesh.

### `Partial`
Удалось получить только часть данных:
- mesh есть, но textures/materials частично отсутствуют;
- или scene есть, но details incomplete.

### `Unsupported`
Не найден доказанный path до scene root / mesh data.

## Диагностика, которую нужно выводить

- asset root TGI
- resolved model TGI
- resolved model LOD TGI
- mesh count
- material candidate count
- texture candidate count
- почему object не поддержан:
  - no model root
  - no model LOD
  - no triangle mesh
  - unsupported scene block layout
  - unresolved material linkage
  - stateful object outside supported subset

## Для Codex: anti-hallucination rules

- Если link не подтверждён — пиши `candidate`, а не `resolved`.
- Если material найден package-local fallback’ом — явно помечай это как fallback.
- Если `Object Catalog` / `Object Definition` не нашлись — scene build может всё ещё быть valid.
- Если нет meshes, не выдавать объект за previewable.

## Источники

- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- Sims 4:RCOL  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- Sims 4:0x01661233 (MODL)  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01661233
- s4pe / s4pi reference  
  https://github.com/s4ptacle/Sims4Tools
