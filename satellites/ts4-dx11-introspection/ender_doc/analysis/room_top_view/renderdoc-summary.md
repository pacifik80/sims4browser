# RenderDoc Capture Summary: room_top_view.rdc

- Capture: `C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\room_top_view.rdc`
- Draw calls: `749`
- Actions: `802`
- Present events: `1`
- Unique VS hashes: `135` (`catalog matched: 0`)
- Unique PS hashes: `114` (`catalog matched: 0`)
- Thumbnail: `C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\analysis\room_top_view\room_top_view.thumb.png`

![Capture thumbnail](C:\Users\stani\PROJECTS\Sims4Browser\satellites\ts4-dx11-introspection\ender_doc\analysis\room_top_view\room_top_view.thumb.png)

## Pass Summary

| Pass                               | Draws | Sample EID | Sample VS    | Sample PS    | Resources                                                  |
| ---------------------------------- | ----- | ---------- | ------------ | ------------ | ---------------------------------------------------------- |
| Depth-only Pass #2                 | 188   | 1447       | fe8612ca618e | -            | -                                                          |
| Colour Pass #6 (1 Targets + Depth) | 180   | 5151       | f0fb105d8491 | 4b3456703f1b | texture0, texture1, texture2                               |
| Colour Pass #10 (1 Targets)        | 129   | 8101       | fa885f3261ac | c953833490b8 | tex[0], tex[1]                                             |
| Depth-only Pass #1                 | 99    | 284        | c8f44a0c9716 | -            | -                                                          |
| Colour Pass #3 (1 Targets + Depth) | 75    | 4102       | 1b6263e36c57 | a38eeea79371 | texture0, texture1, texture2, texture3, texture4, texture5 |
| <unmarked>                         | 25    | 43         | 8891efe9c592 | e9d9a02ee563 | texture0, texture1, texture2                               |
| Colour Pass #8 (1 Targets + Depth) | 22    | 7506       | c06f3ac0abe9 | 91ef4db7fc7c | texture0, texture1, texture2, texture3                     |
| Colour Pass #1 (1 Targets + Depth) | 14    | 66         | 3ef466b13dc8 | 157652bb368e | -                                                          |
| Depth-only Pass #3                 | 6     | 3975       | 0a7c2e45fef7 | -            | -                                                          |
| Colour Pass #4 (1 Targets)         | 6     | 4995       | d44403f45b31 | 03d2f4d7d60c | texture0, texture1, texture2, texture3                     |
| Colour Pass #7 (1 Targets + Depth) | 2     | 7446       | 977409851db2 | 2eeb32a5e6af | texture0, texture1                                         |
| Colour Pass #2 (1 Targets)         | 1     | 1371       | 02117d41d083 | b70f80399df1 | texture0                                                   |

## Representative Draws

| EID  | Pass                               | Indices | VS           | PS           | PS resources                                     | CBs                |
| ---- | ---------------------------------- | ------- | ------------ | ------------ | ------------------------------------------------ | ------------------ |
| 3839 | Depth-only Pass #2                 | 36276   | b8374a49ad2f | -            | -                                                | -                  |
| 3066 | Depth-only Pass #2                 | 30486   | b8374a49ad2f | -            | -                                                | -                  |
| 1271 | Depth-only Pass #1                 | 29622   | a066a27fcfeb | -            | -                                                | -                  |
| 2271 | Depth-only Pass #2                 | 25893   | b8374a49ad2f | -            | -                                                | -                  |
| 105  | Colour Pass #1 (1 Targets + Depth) | 22155   | 3f027b4c455e | b3465b4533d2 | -                                                | cbuffer6           |
| 419  | Depth-only Pass #1                 | 22155   | 0ba56ace5c4e | -            | -                                                | -                  |
| 5759 | Colour Pass #6 (1 Targets + Depth) | 22155   | 8126fc1cf3a7 | f088215121ea | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer2 |
| 166  | Colour Pass #1 (1 Targets + Depth) | 20346   | 3f027b4c455e | b3465b4533d2 | -                                                | cbuffer6           |
| 377  | Depth-only Pass #1                 | 20346   | 0ba56ace5c4e | -            | -                                                | -                  |
| 3975 | Depth-only Pass #3                 | 20346   | 0a7c2e45fef7 | -            | -                                                | -                  |
| 5822 | Colour Pass #6 (1 Targets + Depth) | 20346   | 8126fc1cf3a7 | 822bc7e1b38f | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer2 |
| 3851 | Depth-only Pass #2                 | 19176   | b8374a49ad2f | -            | -                                                | -                  |
| 126  | Colour Pass #1 (1 Targets + Depth) | 18924   | 3f027b4c455e | b3465b4533d2 | -                                                | cbuffer6           |
| 440  | Depth-only Pass #1                 | 18924   | 0ba56ace5c4e | -            | -                                                | -                  |
| 5744 | Colour Pass #6 (1 Targets + Depth) | 18924   | 2a764e47411d | 1dc77ba2be8a | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer2 |
| 119  | Colour Pass #1 (1 Targets + Depth) | 12864   | 3f027b4c455e | b3465b4533d2 | -                                                | cbuffer6           |
| 433  | Depth-only Pass #1                 | 12864   | 0ba56ace5c4e | -            | -                                                | -                  |
| 5733 | Colour Pass #6 (1 Targets + Depth) | 12864   | 2a764e47411d | 1dc77ba2be8a | texture0, texture1, texture2, texture3, texture4 | cbuffer0, cbuffer2 |
| 201  | Colour Pass #1 (1 Targets + Depth) | 10131   | 3f027b4c455e | b3465b4533d2 | -                                                | cbuffer6           |
| 412  | Depth-only Pass #1                 | 10131   | 0ba56ace5c4e | -            | -                                                | -                  |

## Notes

- `Sample VS/PS` are SHA-256 hashes of the replayed shader bytecode from RenderDoc reflection.
- `catalog matched` means the hash was found in `docs/raw/shader-catalog.json`.
- This report only covers the `.rdc` files currently present; if multiple frames were intended, each RenderDoc capture should normally exist as a separate `.rdc` file.