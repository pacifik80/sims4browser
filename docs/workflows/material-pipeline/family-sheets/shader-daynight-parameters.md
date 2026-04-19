# ShaderDayNightParameters

This sheet isolates `ShaderDayNightParameters` because it is safer to treat it as a layered light-aware family than as an ordinary surface-material bucket with a few extra params.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Projection, Reveal, And Lightmap Families](projection-reveal-lightmap.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
ShaderDayNightParameters
├─ Family identity as layered light-aware branch ~ 62%
├─ Reveal-helper packet ~ 66%
├─ Light-lookup helper packet ~ 51%
├─ Exact TS4 slot contract ~ 26%
└─ Exact visible-pass math ~ 15%
```

## Evidence order

Use this family packet in the following order:

1. family name and creator-facing lighting behavior
2. engine-lineage shader docs for reveal-helper vocabulary
3. local repo archaeology only as a clue that the family survives into real profile corpora

## Externally safest packet

Strongest evidence:

- the family name itself strongly signals layered day/night or lighting-aware behavior rather than an ordinary static surface
- [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) records `RevealMap` as a dedicated texture parameter in the same engine lineage
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) shows `RevealMap` in `Painting`, which supports the safer reading that reveal textures are family-local helpers rather than generic diffuse aliases

Safe reading:

- `ShaderDayNightParameters` should stay a layered family
- `samplerRevealMap` should stay helper provenance
- `LightsAnimLookupMap` should stay a narrow light-lookup helper until stronger TS4-facing evidence appears

Unsafe reading:

- do not normalize this family into plain `diffuse + emissive + overlay` truth claims just because current preview has to approximate it somehow
- do not treat reveal-helper names as ordinary base-color slots

## Why this is narrower than before

The safe question is no longer:

- “what random slot does this family map to?”

The safe question now is:

- “which helper inputs survive as real layered light/reveal provenance, even when current implementation cannot render them faithfully?”

That is a better research boundary.

## Current repo boundary

Current repo behavior is useful only as implementation boundary:

- current implementation still approximates this family through broad slot normalization
- that approximation is evidence that the family is not yet solved
- it is not evidence that those normalized slots are the real contract

Safe wording:

- “current implementation preserves a layered-family approximation”

Unsafe wording:

- “the family really is `emissive + overlay` because current code maps it that way”

## Open questions

- exact visible-pass math for reveal and day/night transitions
- exact role of `LightsAnimLookupMap`
- exact relation between reveal-helper inputs and visible emissive or layered color passes

## Recommended next work

1. Keep `ShaderDayNightParameters` under the layered light-aware family packet.
2. Preserve reveal-helper and light-lookup provenance separately.
3. Only promote helper names into ordinary slot semantics if stronger TS4-facing evidence appears.
