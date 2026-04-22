# GenerateSpotLightmap And NextFloorLightMapXform

This sheet isolates `GenerateSpotLightmap` and `NextFloorLightMapXform` because they are no longer safe to leave inside a generic unresolved-parameter bucket.

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Shader Family Registry](../shader-family-registry.md)
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md)
- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md)
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md)
- [Generated-Light Runtime Context Gap](../live-proof-packets/generated-light-runtime-context-gap.md)
- [Projection, Reveal, And Lightmap Families](projection-reveal-lightmap.md)
- [Generated-Light Evidence Ledger](../generated-light-evidence-ledger.md)
- [Source map and trust levels](../../../references/codex-wiki/04-research-and-sources/01-source-map.md)
- [Open questions](../../../references/codex-wiki/04-research-and-sources/03-open-questions.md)

## Scope status (`v0.1`)

```text
GenerateSpotLightmap / NextFloorLightMapXform
├─ Generated-light family identity ~ 74%
├─ NextFloorLightMapXform helper reading ~ 76%
├─ Runtime-cluster narrowing ~ 53%
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
- [Generated-Light Evidence Ledger](../generated-light-evidence-ledger.md) now keeps external corroboration, local carry-through, and bounded synthesis separate for this family

Unsafe reading:

- do not reinterpret these names as plain diffuse/specular/overlay material slots
- do not infer exact matrix math from the current repo approximation path

## Carry-through boundary

Current local archaeology suggests these names can survive into wider profile packets.

Current stronger local carry-through floor:

- `tmp/precomp_sblk_inventory.json` keeps `GenerateSpotLightmap` at `occurrences = 6`
- the same inventory keeps `NextFloorLightMapXform = 14` in the stronger generated-light packet
- the same inventory still keeps a weaker `NextFloorLightMapXform = 3` carry-through in a secondary packet
- the stronger packet also carries `SeaLevel = 14`, which keeps the generated-light cluster separate from an ordinary UV-transform story

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

## Runtime clustering floor

The runtime interface corpus improves this sheet by narrowing the next move:

- the broad captures do not currently preserve literal `GenerateSpotLightmap` or `NextFloorLightMapXform` names in reflection
- that means the next useful step is not more static name-hunting first

Current runtime bridge clues:

- helper-like resource names are sparse:
  - `srctex = 25`
  - `dsttex = 5`
  - `maptex = 3`
- transform-like helper variables do survive:
  - `texscale = 26`
  - `offset = 22`
  - `srctexscale = 18`
  - `texgen = 16`
- the strongest current runtime cluster candidates sit in the parameter-heavy helper/combine shapes:
  - `F03`
  - `F04`
  - `F05`

Safe reading:

- the next real uplift here needs context-tagged lighting-heavy captures plus cluster narrowing, not one more prose-only naming pass
- [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md) now narrows that route further:
  - `F03` is the leading runtime candidate
  - the stable `maptex + tex + Constants` packet with `compx`, `compy`, `mapScale`, and `scale` is the strongest current bridge
  - `F04` should now be treated as the broader comparator, not the first generated-light target
- [Generated-Light Runtime Context Gap](../live-proof-packets/generated-light-runtime-context-gap.md) now also closes the current broad-capture ceiling for that same branch:
  - the stable `maptex + tex` packet already survives across all checked broad captures
  - the checked-in manifests still do not carry scene labels strong enough for family-context closure
  - the next honest move is one lighting-heavy context-tagged capture, not more re-reading of the same broad sessions

## Package carrier boundary

Generated-light now has a stronger offline ownership boundary too.

Current safe carrier order:

1. `MATD`
2. `MTST`
3. `Geometry` / `Model` / `ModelLOD`

Current safe reading:

- [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) now closes one recurring overread:
  - `GenerateSpotLightmap` and `NextFloorLightMapXform` are not yet safe as direct `MATD` ownership
  - `MTST` is not yet the default owner just because generated-light semantics survive object-side seams
- [Package, Runtime, And Scene Bridge Boundary](../package-runtime-scene-bridge-boundary.md) keeps the real remaining gap explicit:
  - package-side carrier order is now strong enough to constrain the question
  - the narrowed `F03` runtime packet is now strong enough to constrain the candidate runtime side
  - scene/pass context is still what blocks final ownership
- [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) now makes the row-level offline reading compact too:
  - generated-light is now explicitly "carrier-constrained but not carrier-closed"
  - the next promotion trigger is also explicit instead of implied:
    - tagged lighting-heavy capture around `F03 maptex`
    - or a stronger package-to-runtime structure match

Safe wording:

- "generated-light ownership is now package-constrained but still open"
- "`NextFloorLightMapXform` remains helper provenance, not authored slot closure"

Unsafe wording:

- "`GenerateSpotLightmap` is a direct `MATD` family"
- "`NextFloorLightMapXform` is an `MTST`-owned UV transform"

## Open questions

- exact matrix semantics of `NextFloorLightMapXform`
- exact visible impact of generated-light helpers in live assets
- exact boundary between generated-light helpers and any visible layered material pass

## Recommended next work

1. Keep this packet in the generated-light family, not in ordinary surface slots.
2. Preserve `NextFloorLightMapXform` as helper provenance until stronger proof appears.
3. Start the runtime side from [Generated-Light Runtime Cluster Candidate Floor](../live-proof-packets/generated-light-runtime-cluster-candidate-floor.md), not a broad `F03/F04/F05` bucket.
4. Use [Generated-Light Runtime Context Gap](../live-proof-packets/generated-light-runtime-context-gap.md) to avoid overreading the current broad capture corpus.
5. Use [Helper-Family Package Carrier Boundary](../helper-family-package-carrier-boundary.md) before promoting any authored carrier claim for this family.
6. Use [Helper-Family Carrier Plausibility Matrix](../helper-family-carrier-plausibility-matrix.md) when the question is which carrier is plausible versus promotable.
7. Use the runtime helper-family clustering floor before widening more naming-only follow-up.
8. Build live comparison fixtures and context-tagged captures around assets that clearly carry generated-light vocabulary.
