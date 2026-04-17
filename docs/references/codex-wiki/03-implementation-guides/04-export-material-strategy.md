# Экспорт и material mapping

Назначение: зафиксировать realistic policy для FBX + textures export.

## Главный принцип

Не пытаться воспроизвести shader logic Maxis 1:1 во всех DCC/game-engine workflows.

Вместо этого:
- строить **portable material bundle**,
- экспортировать textures в понятных форматах,
- сохранять manifest, который объясняет wiring.

## Что должен содержать export folder

```text
/{asset_slug}/
  {asset_slug}.fbx
  /Textures/*.png
  manifest.json
  material_manifest.json
  metadata.json
```

## Минимальная portable material model

Когда выводим material bundle, пытаться извлечь следующие semantic slots:

- `BaseColor` / `Diffuse`
- `Normal`
- `Specular`
- `Gloss` / `Smoothness`
- `Opacity` / `Alpha`
- `Emissive`
- `AmbientOcclusion` — только если действительно derivable

## Правила честности

### Разрешено
- approximate mapping
- fallback mapping
- missing slots with diagnostics

### Не разрешено
- притворяться, что Maxis material semantics полностью сохранены;
- silently dropping key maps;
- писать "Unity/Blender ready" без manifest / diagnostics.

## Что писать в `material_manifest.json`

Для каждого material:
- material slot name / index
- exported texture files
- semantic meaning каждого texture
- approximations
- missing channels
- notes for Blender / Unity reconstruction

Пример:

```json
{
  "materials": [
    {
      "name": "SeatFabric",
      "maps": {
        "baseColor": "Textures/seat_diffuse.png",
        "normal": "Textures/seat_normal.png",
        "specular": "Textures/seat_specular.png"
      },
      "approximations": [
        "Original Maxis shader semantics simplified to portable slot mapping."
      ]
    }
  ]
}
```

## Правила для Build/Buy

- material slots нужно сохранять по submesh’ам;
- texture references должны переноситься даже если final shader mapping approximate;
- object без textures всё ещё можно экспортировать как mesh-only, но это должно быть отражено в manifest.

## Правила для CAS

- skinned mesh export обязан сохранять bone weights и bind pose;
- texture bundle должен идти рядом;
- если preview использует mannequin/base body, это не должно silently leak в default export selected part.

## Почему это важно

FBX сам по себе часто недостаточен для надёжного переноса materials между:
- Blender
- Unity
- другими DCC/engine tools

Поэтому `material_manifest.json` — обязательный companion artifact.

## Источники

- Build/Buy and CAS file type overview  
  https://thesims4moddersreference.org/reference/file-types/
- RCOL / mesh chunk references  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- SimRipper description for mesh + composited textures  
  https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html
