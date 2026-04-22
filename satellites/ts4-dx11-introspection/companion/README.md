# Companion Process

Current implementation:
- hosts a named-pipe server for the native runtime;
- writes `events.jsonl`, `frames.jsonl`, `shaders.jsonl`, `draws.jsonl`, and `captures.jsonl`;
- deduplicates shader records by stable shader hash;
- provides a `summarize` command that prints unique and repeated shader hashes.

Start it with [start-companion.ps1](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/scripts/start-companion.ps1).
