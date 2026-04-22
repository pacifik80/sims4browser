# ShaderDayNightParameters

This sheet isolates `ShaderDayNightParameters` because it is safer to treat it as a layered light-aware family than as an ordinary surface-material bucket with a few extra params.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [ShaderDayNight Runtime Cluster Candidate Floor](../live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md)
- [ShaderDayNight Runtime Context Gap](../live-proof-packets/shader-daynight-runtime-context-gap.md)
- [Projection, Reveal, And Lightmap Families](projection-reveal-lightmap.md)
- [ShaderDayNight Evidence Ledger](../shader-daynight-evidence-ledger.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
ShaderDayNightParameters
├─ Family identity as layered light-aware branch ~ 68%
├─ Reveal-helper packet ~ 73%
├─ Runtime-cluster narrowing ~ 61%
├─ Runtime context-gap closure ~ 42%
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
- [ShaderDayNight Evidence Ledger](../shader-daynight-evidence-ledger.md) now keeps external corroboration, local carry-through, and bounded synthesis separate for this family

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

Current stronger local carry-through floor:

- `tmp/precomp_sblk_inventory.json` keeps `ShaderDayNightParameters` at `occurrences = 5`
- the same packet keeps `LightsAnimLookupMap = 94` and `samplerRevealMap = 32`
- current visible-root isolation now has three anchors instead of only two:
  - `01661233:00000000:0737711577697F1C`
  - `01661233:00000000:00B6ABED04A8F593`
  - `01661233:00000000:1463BD19EE39DC8C`

Safe reading:

- the helper packet is now stronger than one-off profile-name archaeology
- the visible-root packet is still a local fixture-selection layer, not exact TS4 visible-pass proof

Safe wording:

- “current implementation preserves a layered-family approximation”

Unsafe wording:

- “the family really is `emissive + overlay` because current code maps it that way”

## Runtime clustering floor

The new runtime interface corpus improves this sheet in one specific way:

- it still does not surface literal `RevealMap`-style family names directly
- but it now exposes repeatable helper-like interface shapes that can be clustered before exact family naming is solved

Current strongest clustering hints:

- helper-like runtime names are sparse and generic:
  - `srctex = 25`
  - `dsttex = 5`
  - `maptex = 3`
- helper-like runtime variables are stronger:
  - `texscale = 26`
  - `offset = 22`
  - `scolor = 22`
  - `srctexscale = 18`
  - `texgen = 16`
- the seeded runtime family candidates most worth checking next are:
  - `F04` three-texcoord parameter-heavy pixel family
  - `F05` color plus four-texcoord pixel family

Safe reading:

- the next honest closure path is family clustering over runtime shapes, not more literal name search first
- [ShaderDayNight Runtime Cluster Candidate Floor](../live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md) now narrows that path further:
  - `F04` is the leading runtime candidate
  - `F05` is the nearest color-aware sibling comparator
  - widening back to broader helper buckets should wait until `F04` fails under context-tagged capture
- [ShaderDayNight Runtime Context Gap](../live-proof-packets/shader-daynight-runtime-context-gap.md) now also closes the current broad-capture ceiling:
  - the checked-in runtime sessions keep representative `F04` and `F05` hashes alive across all broad captures
  - those captures are not scene-tagged enough to promote `F04` into a scene-bound family reading yet
  - the next honest step is one context-tagged capture, not more re-reading of the same broad sessions

## Package carrier boundary

The package-side layer is now strong enough to constrain this family sheet too.

Current safe carrier order:

1. `MATD`
2. `MTST`
3. `Geometry` / `Model` / `ModelLOD`

Current safe reading:

- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) now closes one ambiguity that used to leak into this sheet:
  - `ShaderDayNightParameters` is not yet safe as a direct top-level `MATD` ownership claim
  - it is also not safe as an `MTST`-owned family just because object-side visible roots preserve it
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md) keeps the larger missing join explicit:
  - package-side carrier priority is now strong enough to narrow the ownership question
  - runtime helper clustering is now strong enough to narrow the runtime side
  - scene/pass context is still the missing part that blocks final ownership
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) now makes the row-level offline reading compact too:
  - `ShaderDayNightParameters` is now explicitly "carrier-constrained but not carrier-closed"
  - the next promotion trigger is also explicit instead of implied:
    - tagged capture that concentrates `F04`
    - or a stronger package-to-runtime structure match

Safe wording:

- "package-side carrier order now constrains `ShaderDayNightParameters` ownership"
- "current carrier evidence does not yet close authored-family ownership"

Unsafe wording:

- "`ShaderDayNightParameters` is a `MATD` family"
- "`ShaderDayNightParameters` is owned by `MTST`"

## Open questions

- exact visible-pass math for reveal and day/night transitions
- exact role of `LightsAnimLookupMap`
- exact relation between reveal-helper inputs and visible emissive or layered color passes

## Recommended next work

1. Keep `ShaderDayNightParameters` under the layered light-aware family packet.
2. Preserve reveal-helper and light-lookup provenance separately.
3. Use [ShaderDayNight Runtime Cluster Candidate Floor](../live-proof-packets/shader-daynight-runtime-cluster-candidate-floor.md) to start from `F04`, not a broad `F03/F04/F05` bucket.
4. Use [ShaderDayNight Runtime Context Gap](../live-proof-packets/shader-daynight-runtime-context-gap.md) to avoid overreading the current broad capture corpus.
5. Keep `F05` as the nearest color-aware comparator before widening farther.
6. Use [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) before promoting any package-side owner claim for this family.
7. Use [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) when the question is which carrier is plausible right now.
8. Only promote helper names into ordinary slot semantics if stronger TS4-facing evidence appears.
