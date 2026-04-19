# GenerateSpotLightmap And NextFloorLightMapXform

This sheet isolates `GenerateSpotLightmap` and `NextFloorLightMapXform` because they are no longer safe to leave inside a generic unresolved-parameter bucket.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Projection, Reveal, And Lightmap Families](projection-reveal-lightmap.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
GenerateSpotLightmap / NextFloorLightMapXform
├─ Generated-light family identity ~ 69%
├─ NextFloorLightMapXform helper reading ~ 71%
├─ Carry-through into other profile packets ~ 33%
└─ Exact matrix semantics ~ 12%
```

## Evidence order

Use this packet in the following order:

1. creator-facing or reverse-engineering discussions that group these names together
2. family naming itself as generated-light vocabulary
3. local repo archaeology only as a clue about where carry-through still happens

## Externally safest packet

Strongest evidence:

- [Sims 4 lighting in Sims 3?](https://modthesims.info/showthread.php?t=646135) groups `NextFloorLightMapXform`, `GenerateSpotLightmap`, `GenerateWindowLightmap`, `GenerateRectAreaLightmap`, and `GenerateTubeLightmap` into one TS4-specific lightmap vocabulary
- the name family itself is strongly generated-light or lightmap vocabulary rather than ordinary surface-material vocabulary

Safe reading:

- `GenerateSpotLightmap` should stay in a generated-light family
- `NextFloorLightMapXform` is safer as transform/helper provenance than as a directly sampled base slot
- this packet belongs with generated light and lightmap behavior, not with ordinary surface shading

Unsafe reading:

- do not reinterpret these names as plain diffuse/specular/overlay material slots
- do not infer exact matrix math from the current repo approximation path

## Carry-through boundary

Current local archaeology suggests these names can survive into wider profile packets.

That is useful for one reason:

- it means generated-light provenance may leak into broader material packets and should be preserved

That is not enough to prove:

- direct visible-pass sampling
- ordinary slot semantics
- exact matrix structure

## Current repo boundary

Current repo behavior is useful only as a warning boundary:

- current implementation still cannot treat this family as a closed generated-light contract
- any broad slot mapping around these names is still approximation

Safe wording:

- “current implementation still approximates generated-light helpers”

Unsafe wording:

- “`NextFloorLightMapXform` is a normal UV transform because current code treats it like one”

## Open questions

- exact matrix semantics of `NextFloorLightMapXform`
- exact visible impact of generated-light helpers in live assets
- exact boundary between generated-light helpers and any visible layered material pass

## Recommended next work

1. Keep this packet in the generated-light family, not in ordinary surface slots.
2. Preserve `NextFloorLightMapXform` as helper provenance until stronger proof appears.
3. Build live comparison fixtures around assets that clearly carry generated-light vocabulary.
