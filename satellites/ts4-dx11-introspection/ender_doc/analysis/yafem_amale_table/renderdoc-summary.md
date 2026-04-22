# RenderDoc Capture Summary: yafem_amale_table.rdc

- Capture: `C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\yafem_amale_table.rdc`
- Draw calls: `89`
- Actions: `123`
- Present events: `1`
- Unique VS hashes: `30` (`catalog matched: 0`)
- Unique PS hashes: `23` (`catalog matched: 0`)
- Thumbnail: `C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\analysis\yafem_amale_table\yafem_amale_table.thumb.png`

![Capture thumbnail](C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\analysis\yafem_amale_table\yafem_amale_table.thumb.png)

## Pass Summary

| Pass                               | Draws | Sample EID | Sample VS    | Sample PS    | Resources                                        |
| ---------------------------------- | ----- | ---------- | ------------ | ------------ | ------------------------------------------------ |
| Colour Pass #5 (1 Targets)         | 52    | 996        | fa885f3261ac | c953833490b8 | tex[0], tex[1]                                   |
| Colour Pass #3 (1 Targets + Depth) | 15    | 522        | 152410d157b9 | c91ba73b429c | texture0, texture1, texture2, texture3, texture4 |
| Colour Pass #2 (1 Targets)         | 10    | 329        | 0a7c2e45fef7 | -            | -                                                |
| <unmarked>                         | 8     | 276        | a1cdf27bec3e | 16f3acf29252 | texture0                                         |
| Colour Pass #1 (1 Targets)         | 3     | 85         | b8374a49ad2f | -            | -                                                |
| Colour Pass #4 (1 Targets)         | 1     | 926        | 315f9c3eccc1 | 1fdc3b1b5902 | texture0                                         |

## Representative Draws

| EID | Pass                               | Indices | VS           | PS           | PS resources                                     | CBs                |
| --- | ---------------------------------- | ------- | ------------ | ------------ | ------------------------------------------------ | ------------------ |
| 532 | Colour Pass #3 (1 Targets + Depth) | 18579   | 152410d157b9 | c91ba73b429c | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer7 |
| 522 | Colour Pass #3 (1 Targets + Depth) | 18219   | 152410d157b9 | c91ba73b429c | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer7 |
| 359 | Colour Pass #2 (1 Targets)         | 10902   | 730aa2d7b0ae | -            | -                                                | -                  |
| 412 | Colour Pass #2 (1 Targets)         | 10902   | 730aa2d7b0ae | -            | -                                                | -                  |
| 602 | Colour Pass #3 (1 Targets + Depth) | 10902   | 06a508314174 | fd0b8ed626b3 | texture0, texture1, texture2, texture3, texture4 | cbuffer0           |
| 552 | Colour Pass #3 (1 Targets + Depth) | 9630    | 152410d157b9 | c91ba73b429c | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer7 |
| 329 | Colour Pass #2 (1 Targets)         | 9456    | 0a7c2e45fef7 | -            | -                                                | -                  |
| 382 | Colour Pass #2 (1 Targets)         | 9456    | 0a7c2e45fef7 | -            | -                                                | -                  |
| 617 | Colour Pass #3 (1 Targets + Depth) | 9456    | 6453fa3d3ca1 | 7e31ac926412 | texture0, texture1, texture2, texture3, texture4 | cbuffer0           |
| 345 | Colour Pass #2 (1 Targets)         | 9420    | 730aa2d7b0ae | -            | -                                                | -                  |
| 398 | Colour Pass #2 (1 Targets)         | 9420    | 730aa2d7b0ae | -            | -                                                | -                  |
| 580 | Colour Pass #3 (1 Targets + Depth) | 9420    | 06a508314174 | fd0b8ed626b3 | texture0, texture1, texture2, texture3, texture4 | cbuffer0           |
| 542 | Colour Pass #3 (1 Targets + Depth) | 6576    | 152410d157b9 | c91ba73b429c | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer7 |
| 338 | Colour Pass #2 (1 Targets)         | 3450    | 730aa2d7b0ae | -            | -                                                | -                  |
| 391 | Colour Pass #2 (1 Targets)         | 3450    | 730aa2d7b0ae | -            | -                                                | -                  |
| 569 | Colour Pass #3 (1 Targets + Depth) | 3450    | 06a508314174 | fd0b8ed626b3 | texture0, texture1, texture2, texture3, texture4 | cbuffer0           |
| 352 | Colour Pass #2 (1 Targets)         | 2982    | 730aa2d7b0ae | -            | -                                                | -                  |
| 405 | Colour Pass #2 (1 Targets)         | 2982    | 730aa2d7b0ae | -            | -                                                | -                  |
| 591 | Colour Pass #3 (1 Targets + Depth) | 2982    | 06a508314174 | fd0b8ed626b3 | texture0, texture1, texture2, texture3, texture4 | cbuffer0           |
| 678 | Colour Pass #3 (1 Targets + Depth) | 1344    | 0298513c69c5 | f9702b923dcc | texture0                                         | cbuffer0           |

## Notes

- `Sample VS/PS` are SHA-256 hashes of the replayed shader bytecode from RenderDoc reflection.
- `catalog matched` means the hash was found in `docs/raw/shader-catalog.json`.
- This report only covers the `.rdc` files currently present; if multiple frames were intended, each RenderDoc capture should normally exist as a separate `.rdc` file.