# Карта источников и trust levels

Назначение: чтобы Codex не путал официальный материал, primary community reference и просто полезный tooling.

## Trust levels

### Level A — primary references для package/resource work
Использовать в первую очередь.

1. **The Sims 4 Modders Reference**
   - DBPF format
   - Internal compression
   - File Types
   - Resource Type Index
   - STBL format

   Это лучший общий reference hub для формата и high-level ролей ресурсов.

2. **LlamaLogic.Packages**
   - современный .NET API docs
   - хорошие remarks про lazy loading, names, thread safety, decompression
   - практический reference для package access layer

### Level B — format archaeology / chunk-level reverse engineering
Использовать для chunk-level parsing и scene reconstruction.

3. **Mod The Sims / SimsWiki**
   - RCOL
   - MODL
   - GEOM
   - assorted format pages

4. **Llama-Logic/Binary-Templates**
   - 010 Editor templates для изучения бинарных форматов
   - хорошо подходит для unknown/partial formats

### Level C — reference implementations / legacy tools
Использовать как source of ideas, но осторожно.

5. **s4pe / s4pi / Sims4Tools**
   - старый, но полезный reference-код
   - community standard в течение долгого времени
   - не должен автоматически считаться source of truth

6. **dbpf_reader**
   - компактный low-level reference reader
   - полезен для проверки DBPF assumptions

### Level D — problem-specific tooling
Использовать только по своей domain-задаче.

7. **TS4 SimRipper**
   - лучший reference для full-sim assembly, save parsing, morph application
   - не нужен как primary source для raw package browsing
   - очень важен для full character/export задач

### Level E — official EA material
Полезен, но по другой теме.

8. **Official EA / EA Forums modding posts**
   - в найденных материалах в основном про Python script mods и code changes for modders
   - не являются полным официальным DBPF/package spec

## Рекомендуемый приоритет чтения

### Для raw package parsing
1. Modders Reference
2. LlamaLogic.Packages docs
3. dbpf_reader
4. Binary Templates
5. s4pi/s4pe

### Для Build/Buy scene reconstruction
1. File Types / Resource Type Index
2. RCOL / MODL pages
3. Binary Templates
4. LlamaLogic / s4pi code
5. local fixtures

### Для CAS part export
1. File Types / Resource Type Index
2. GEOM page
3. Binary Templates
4. local fixtures

### Для full Sim / morph / save-game pipelines
1. File Types / Resource Type Index
2. SimRipper
3. local fixtures / save files

## Licensing notes

- `LlamaLogic` — MIT
- `Binary-Templates` — MIT
- `s4ptacle/Sims4Tools` — GPLv3
- `TS4SimRipper` — GPL-3.0
- `dbpf_reader` — zlib license

Следствие:
- permissive sources можно легче использовать в коде;
- GPL sources безопаснее использовать как reference / research source, если нет намерения переводить проект под GPL.

## Source list

- Modders Reference Index  
  https://thesims4moddersreference.org/reference/
- DBPF format  
  https://thesims4moddersreference.org/reference/dbpf-format/
- Internal compression  
  https://thesims4moddersreference.org/reference/internal-compression-dbpf/
- File Types  
  https://thesims4moddersreference.org/reference/file-types/
- Resource Type Index  
  https://thesims4moddersreference.org/reference/resource-types/
- STBL format  
  https://thesims4moddersreference.org/reference/stbl-format/
- LlamaLogic  
  https://github.com/Llama-Logic/LlamaLogic
- LlamaLogic `DataBasePackedFile` docs  
  https://llama-logic.github.io/LlamaLogic/packages/LlamaLogic.Packages.DataBasePackedFile.html
- Binary Templates  
  https://github.com/Llama-Logic/Binary-Templates
- Sims4Tools / s4pe  
  https://github.com/s4ptacle/Sims4Tools
- dbpf_reader  
  https://github.com/ytaa/dbpf_reader
- Sims 4:RCOL  
  https://modthesims.info/wiki.php?title=Sims_4%3ARCOL
- MODL  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x01661233
- GEOM  
  https://modthesims.info/wiki.php?title=Sims_4%3A0x015A1849
- TS4 SimRipper GitHub  
  https://github.com/CmarNYC-Tools/TS4SimRipper
- TS4 SimRipper MTS page  
  https://modthesims.info/d/635720/ts4-simripper-classic-rip-sims-from-savegames-v3-14-2-0-updated-4-19-2023.html
