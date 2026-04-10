# Стратегия scene reconstruction

Назначение: зафиксировать общую модель canonical scene для Build/Buy и CAS.

## Canonical scene — зачем он нужен

Нужен единый внутренний scene model, чтобы:
- preview использовал те же данные, что и export;
- Build/Buy и CAS reuse’или общий viewport/export stack;
- diagnostics можно было строить на одном и том же уровне.

## Что должно быть в canonical scene

Минимальный core:

```text
Scene
├─ Nodes / hierarchy
├─ Meshes
│  ├─ Vertex buffers
│  ├─ Index buffers
│  ├─ UV channels
│  ├─ Normals
│  ├─ Tangents (optional)
│  └─ Material slot per submesh
├─ Materials
│  ├─ semantic texture references
│  └─ fallback metadata
├─ Skeleton (optional)
│  ├─ Bones
│  ├─ Bind pose
│  └─ Skin weights
└─ Diagnostics
```

## Build/Buy reconstruction

Безопасный путь:

```text
Model -> Model LOD -> mesh data -> materials -> textures
```

Успех:
- хотя бы один triangle mesh reconstructed.

Partial:
- mesh есть, но material/texture incomplete.

Unsupported:
- no model root / no LOD / no mesh data.

## CAS reconstruction

Безопасный путь:

```text
CAS metadata -> Geometry -> Rig -> textures
```

Успех:
- есть skinned mesh + rig.

Partial:
- geometry есть, но rig/materials incomplete.

Unsupported:
- no trustworthy geometry path / no rig for supported subset.

## Viewport = scene consumer, не parser

3D viewport не должен:
- сам резолвить package dependencies,
- сам декодировать bytes,
- сам угадывать materials.

Он должен получать уже собранный canonical scene.

## Export = scene consumer, не parser

FBX / glTF / other export не должны:
- сами читать package files,
- сами искать textures,
- сами повторно строить graph.

Они должны использовать уже собранный canonical scene + prepared portable material mapping.

## Диагностика должна жить рядом со scene build

Для каждого scene build сохранять:
- resolved resources
- unresolved resources
- fallbacks
- warnings
- fatal reasons

Это нужно и для UI, и для тестов, и для export manifests.

## Обязательные статусы

- `SceneReady`
- `Partial`
- `Unsupported`

Их нельзя путать с:
- "asset входит в broad supported family"
- "asset выглядит знакомо"
- "в package есть model type"

## Что считать хорошим тестом

### Unit/integration scene-build test
На local fixture проверить:
- resolved scene root
- mesh count > 0
- vertex/index count > 0
- material slots count expected-ish
- bone count > 0 for skinned CAS

### Export smoke test
Проверить:
- `.fbx` существует
- `Textures/*.png` существуют
- `manifest.json` существует
- `material_manifest.json` существует

## Источники

- Sims 4:RCOL  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- MODL chunk overview  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01661233
- GEOM overview  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849
