# ShaderDayNight Runtime Cluster Candidate Floor

This packet turns the helper-family runtime clustering work into a narrower `ShaderDayNightParameters` follow-up.

Question:

- does the checked-in DX11 runtime corpus already narrow the next `ShaderDayNightParameters` helper-family step enough to prefer one cluster candidate over the nearest sibling?

Related docs:

- [Material Pipeline Deep Dives](../README.md)
- [Runtime Helper-Family Clustering Floor](../runtime-helper-family-clustering-floor.md)
- [Runtime Shader Interface Baseline](../runtime-shader-interface-baseline.md)
- [P1 Live-Proof Queue](../p1-live-proof-queue.md)
- [ShaderDayNightParameters](../family-sheets/shader-daynight-parameters.md)
- [ShaderDayNightParameters Visible-Pass Proof](shader-daynight-visible-pass.md)
- [Runtime cluster candidate snapshot](../../../tmp/shaderdaynight_runtime_cluster_candidates_2026-04-21.json)

## Scope status (`v0.1`)

```text
ShaderDayNight Runtime Cluster Candidate Floor
├─ Externally proved family identity ~ 68%
├─ Runtime cluster narrowing ~ 81%
├─ F04-versus-F05 boundary ~ 74%
├─ Exact family ownership ~ 22%
└─ Exact draw/pass mapping ~ 12%
```

## External identity baseline

What is already strong enough:

- the family name still signals layered day/night or reveal-aware behavior rather than an ordinary one-pass surface material
- [Sims_3:Shaders\\Params](https://modthesims.info/wiki.php?title=Sims_3%3AShaders%5CParams) still keeps `RevealMap` as dedicated helper vocabulary in the same engine lineage
- [Sims_3:Shaders](https://modthesims.info/wiki.php?title=Sims_3%3AShaders) still supports the safer reveal-helper reading instead of a plain diffuse alias

Safe reading:

- `ShaderDayNightParameters` stays a layered helper family
- runtime clustering is only narrowing the best next local route
- runtime clustering is not yet a literal family-name or package-owner proof

## Local runtime cluster floor

The checked-in helper-family snapshot now freezes one narrow comparison packet:

- `tmp/shaderdaynight_runtime_cluster_candidates_2026-04-21.json`

Seeded candidates:

- `F04 = Three-Texcoord Pixel`
- `F05 = Color Plus Four-Texcoord Pixel`

Representative `F04` hashes:

- `03ffb5addf1935b1acb49c3991e4b45773c97f4537624210e22f59db09f09bcf`
- `042d682be7ff6682204d8ef27c2ebb052a35f82e2d8f4e089357ae0147b62ee1`
- `05f6262aaca21ffe3630fa2f4fb44aa86175b4600537df79aa8f211fc8117e66`

Representative `F05` hashes:

- `061a0ce8873ead820ae520ce37b69f96216f567dc368e32fc23ee3fd5fcfaa11`
- `09f989acd7950fc679918789eeba72fac1ce69c9fb8603612edeb315d59535b1`
- `130c0fbf3466ef5656c9824d6baa1ee24418a4dae62984c18dd49b403f8b6e2b`

## Why `F04` is the stronger current candidate

Useful direct runtime signal:

- `F04` keeps the repeated `TEXCOORD0`, `TEXCOORD1`, `TEXCOORD2` pixel-shader shape
- one checked-in `F04` representative already exposes a real helper-style constant-buffer surface:
  - `fsize`
  - `offset`
  - `scolor`
  - `texscale`
- nearby `F04` members stay inside the same three-texcoord shape even when their resource vocabulary is generic

Safe reading:

- `F04` is the stronger parameter-heavy helper or intermediate-combine candidate for the next `ShaderDayNightParameters` runtime step
- this is a stronger next action than wide literal-name hunting

## Why `F05` stays the nearest comparator

Useful direct runtime signal:

- `F05` keeps a repeated color-aware input shape:
  - `COLOR0`
  - `TEXCOORD0`
  - `TEXCOORD1`
  - `TEXCOORD2`
  - `TEXCOORD3`
- current `F05` representatives expose repeated paired texture resources, but no stronger helper-variable packet than the current `F04` example

Safe reading:

- `F05` is still useful as the nearest color-aware sibling cluster
- it is not yet the leading `ShaderDayNightParameters` runtime candidate

Unsafe reading:

- do not claim that `F05` is already the visible tint or overlay pass for this family
- do not claim that `F04` is already definitively `ShaderDayNightParameters`

## What this packet changes

Before this packet, the honest helper-family move was:

- cluster `F04` and `F05`

After this packet, the honest helper-family move is narrower:

- start from `F04`
- use `F05` as the closest sibling comparator
- only widen back to `F03` or broader helper buckets if `F04` fails under context-tagged capture

That is a real narrowing step even though exact ownership is still open.

## Best next inspection step

1. Keep [ShaderDayNightParameters Visible-Pass Proof](shader-daynight-visible-pass.md) as the visible-root packet.
2. Treat `F04` as the first runtime capture target for any lighting-heavy or reveal-aware comparison scene.
3. Compare `F04` against `F05` before widening into broader helper-like clusters.

## Honest limit

This packet does not yet prove:

- exact `ShaderDayNightParameters` draw/pass ownership
- exact package-side owner for the exposed runtime resources
- exact visible day/night transition math

What it does prove:

- the runtime helper-family route is no longer one undifferentiated `F03/F04/F05` bucket for this family
- `F04` is now the strongest current runtime-cluster candidate for the next `ShaderDayNightParameters` step
