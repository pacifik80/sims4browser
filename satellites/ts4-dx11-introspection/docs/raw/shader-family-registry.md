# TS4 DX11 Shader Family Registry

Last updated:
- 2026-04-21 UTC

Status:
- initial seeded registry
- intended to bridge stable runtime shader inventory with code/binary analysis
- intended to hold named family buckets, evidence, representative hashes, and open questions

Inputs used for the current seed pass:
- [shader-catalog.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.md)
- [shader-catalog.json](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/shader-catalog.json)
- [recon-findings.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/recon-findings.md)
- [recon-20260421-141531.md](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/docs/reports/recon-20260421-141531.md)

## Working Rules

Family labels here are intentionally pragmatic:
- they are stable enough to guide further work;
- they are not yet authoritative semantic names from TS4 itself;
- each family explicitly separates confirmed facts from interpretation.

Confidence levels:
- `high`: shape is strongly recurrent and interpretation is narrow
- `medium`: shape is strongly recurrent but interpretation is still broad
- `low`: current label is mainly a navigation aid

## Binary Linkage Baseline

Confirmed from static recon:
- `TS4_x64.exe` imports `D3D11CreateDevice`, `D3D11CreateDeviceAndSwapChain`, and `CreateDXGIFactory1`.
- `TS4_x64_fpb.exe` imports the same entry points.
- both primary x64 executables contain a large embedded `DXBC` population and should be treated as first-class shader carriers.

Confirmed from runtime catalog:
- the current broad stable union contains `1922` unique runtime shader hashes.
- `1757` of those hashes recur across all three wide stable captures.

Confirmed from direct binary extraction:
- `TS4_x64.exe` contains `1302` embedded `DXBC` containers.
- `TS4_x64_fpb.exe` contains `1302` embedded `DXBC` containers.
- `365` exact `SHA-256` matches exist between the current broad runtime catalog and the embedded `DXBC` population in each executable.
- the two executable variants have the same embedded `DXBC` hash set.

Interpretation:
- a substantial subset of the runtime shader surface is directly embedded in the main executables.
- the executables still do not explain the entire runtime shader catalog by themselves, so additional data/runtime sources remain likely.

Open question:
- which seeded registry families are most strongly represented in the `365` exact binary↔runtime matches.

## Seed Families

### `F01` Screen-Space Bare Pixel

Confidence:
- `medium`

Confirmed signals:
- stage: `ps`
- input semantics: `SV_POSITION0`
- output semantics: `SV_Target0`
- family size: `23`
- capture support:
  - `23` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `ps br=0 cb=0 in=1 out=1`

Representative hashes:
- `01ff7cc0d7817cf1d779171f8d0fc801332e38c59ec0965de32aef32cb54c606`
- `0c8fbd2a3738fc341411bb364a3bf6e81c2830ddb5dd7c7d9457acbf0521aa2b`
- `10d3c56e85878c81daf7e915e010fcc5a5279c447a7acab2ee68aec085eb9df7`
- `248017326e9ab0c0ca6bf97c9e605ce663dea6906f018185923645a7c0d4ecae`
- `2d40f8c1be78710e6ca6ec39be10efa1cf9b5b348a2af76be4998510c06654e9`

Current interpretation:
- very likely screen-space/fixed-geometry pixel pass family
- plausible candidates include simple fullscreen or rect-based passes, clears, UI quads, or utility blits

What is not yet proven:
- whether these are UI-only, postprocess-only, or a shared generic quad family

### `F02` Screen-Space UV Pixel

Confidence:
- `medium`

Confirmed signals:
- stage: `ps`
- input semantics: `SV_POSITION0, TEXCOORD0`
- output semantics: `SV_Target0`
- family size: `24`
- capture support:
  - `24` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `ps br=0 cb=0 in=2 out=1`

Representative hashes:
- `088998f86aa9e81b881c6f73ad0e34c8daeb734254ff6e73111b791f45e87736`
- `0e6436764ac0925399d1361cff1499eb6ed2f4e33c12c58730b73d190c06504f`
- `16f3acf292525e958f05fb1591f821cbc4fd275de4a26bccd15e9dc61e36371f`
- `2108eadcaf9621c3291d31328f1f12a284b52786d30bf42c7707718bb6b1b18c`
- `38a5f4b17ddb7c7e532403582aa68a096d7ce94a7315668b7afc28149570ec15`

Current interpretation:
- likely another stable screen-space family, but one that carries UVs explicitly
- strongest candidates are textured fullscreen/rect passes, presentation helpers, or UV-based UI/postprocess work

What is not yet proven:
- whether this family is closer to UI composition, thumbnail/blit work, or general postprocess

### `F03` Single-Texcoord Pixel

Confidence:
- `high`

Confirmed signals:
- stage: `ps`
- input semantics: `TEXCOORD0`
- output semantics: `SV_Target0`
- family size: `42`
- capture support:
  - `42` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `ps br=0 cb=0 in=1 out=1`
- family also includes richer members such as:
  - `ps br=2 cb=0 in=1 out=1`
  - `ps br=5 cb=1 in=1 out=1`
  - `ps br=7 cb=1 in=1 out=1`

Representative hashes:
- `01c922311b480050c4aef67bb176416474274a0cdf0d32b9ee4526f6e35b8938`
- `0244871e004f783d18f8fa9445cdfe09aeab036eb3fe8976a02ea3319c9ff139`
- `0a70b4dcb769772916605e6fb8d9d3858bb60573f91d6899b0b655619e4d4110`
- `0c4bfd1e3ff98ec979dc9ce877698f011399a99f2a89a3cf97907b7842c0a1bc`
- `0d5a27655d43f10a7a98be014cb73741ff6c3139e45273e1ed93183b5e775287`

Current interpretation:
- likely the largest simple textured pixel-pass family in the current registry
- broad enough to include both minimal texture reads and richer sampled/material variants

What is not yet proven:
- whether this family should later split into:
  - basic blit/sample
  - alpha/mask sample
  - parameterized material utility passes

### `F04` Three-Texcoord Pixel

Confidence:
- `high`

Confirmed signals:
- stage: `ps`
- input semantics: `TEXCOORD0, TEXCOORD1, TEXCOORD2`
- output semantics: `SV_Target0`
- family size: `59`
- capture support:
  - `59` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature in the family: `ps br=5 cb=1 in=3 out=1`
- the family also contains multiple lighter variants with `br=2..4` and `cb=0..1`

Representative hashes:
- `03ffb5addf1935b1acb49c3991e4b45773c97f4537624210e22f59db09f09bcf`
- `042d682be7ff6682204d8ef27c2ebb052a35f82e2d8f4e089357ae0147b62ee1`
- `05f6262aaca21ffe3630fa2f4fb44aa86175b4600537df79aa8f211fc8117e66`
- `0698d451e81099dd5d519d43d660b786f19679ab84e6d2468fb6eba00a45274d`
- `0bf67ae37ddb7e7517cec7375a5704c7b06fc0be000d52399d874692ae143362`

Current interpretation:
- strong candidate for a broad material/intermediate-combine family rather than a tiny utility niche
- enough bound-resource and constant-buffer variation to suspect several subfamilies inside one semantic shape

What is not yet proven:
- which members are scene material passes versus UI/build-buy/CAS compositing helpers

### `F05` Color Plus Four-Texcoord Pixel

Confidence:
- `medium`

Confirmed signals:
- stage: `ps`
- input semantics: `COLOR0, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3`
- output semantics: `SV_Target0`
- family size: `48`
- capture support:
  - `48` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `ps br=4 cb=0 in=5 out=1`

Representative hashes:
- `061a0ce8873ead820ae520ce37b69f96216f567dc368e32fc23ee3fd5fcfaa11`
- `06cda843a3ee38b15a7bd39a6c01042849e035ccdcc7ba117532d99226ced8b8`
- `09f989acd7950fc679918789eeba72fac1ce69c9fb8603612edeb315d59535b1`
- `1272c1eecbee4f61724e5d75a7dc4352964ecba8585573a55a9b5ce283c4b0f5`
- `130c0fbf3466ef5656c9824d6baa1ee24418a4dae62984c18dd49b403f8b6e2b`

Current interpretation:
- likely a richer color-aware material/compositor family
- plausible candidates include tinted/translucent overlay-like work or multi-input material evaluation

What is not yet proven:
- whether this family skews toward CAS overlays, object tinting, or general shared material evaluation

### `F06` Simple Position Vertex

Confidence:
- `medium`

Confirmed signals:
- stage: `vs`
- input semantics: `POSITION0`
- output semantics: `SV_POSITION0`
- family size: `14`
- capture support:
  - `14` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `vs br=0 cb=0 in=1 out=1`

Representative hashes:
- `388069f0a0a0688ae742da9f5de0ccd6f8de1fcbd4b4e65d2f5b387715b9a68f`
- `40d51d48ba9f9e219ee2dbb558d4f71be8486bee6d1715eccba3fde75846643c`
- `56734c88d873b193afe717192b19f6e74cadc8737e7375d40ecee9e5d02b0faa`
- `5f212acae32abb42dfb006044d3fc605dea3a1894a22643bfdd3560f47f9eb2b`
- `6b6225962a109a54c0d08a3e1a556bbba6bcbd533da9de00883dfa8d508436ab`

Current interpretation:
- likely shared simple geometry/quad/line helper family

What is not yet proven:
- whether these are mostly fullscreen/UI feeders or also used by tiny utility scene geometry

### `F07` Mesh Material Vertex 12→10

Confidence:
- `high`

Confirmed signals:
- stage: `vs`
- input semantics:
  - `POSITION0, NORMAL0, POSITION1, POSITION2, POSITION3, POSITION4, POSITION5, POSITION6, POSITION7, TEXCOORD0, COLOR0, TANGENT0`
- output semantics:
  - `COLOR0, COLOR1, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3, TEXCOORD5, TEXCOORD6, TEXCOORD7, SV_POSITION0`
- family size: `18`
- capture support:
  - `18` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `vs br=0 cb=0 in=12 out=10`

Representative hashes:
- `118a4df97e634ed6ec1ba3352a7c183d8b4233517c876b69ae4e58c69f83adb9`
- `18cd2fb5911f77761111a366f72bdf63a769fadb0779b88bd4ce3ce82a520187`
- `335598f392c62eec6ed998113e9c33b5bdb9b6c572aa04a02e3e6c5120913a86`
- `3ae9a92ff410cc46a3d0b1b5e89ab6dac43712e921748935a2ace986f07eca19`
- `44cd6b34b9880b3d0b7155d3ffa4610e9ed0a462139f94485db5289d087ca6d7`

Current interpretation:
- strongest current candidate for a core skinned/material-heavy vertex family
- the repeated input layout strongly suggests complex mesh evaluation rather than screen-space work

What is not yet proven:
- whether this bucket is sim-body-heavy, object-heavy, or shared across both

### `F08` Mesh Material Vertex 12→11

Confidence:
- `high`

Confirmed signals:
- same 12-element input layout as `F07`
- expanded 11-output semantic set
- family size: `15`
- capture support:
  - `15` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `vs br=0 cb=0 in=12 out=11`

Representative hashes:
- `06f8e59d554984499621f637339af17bb998ee2661584229b3e1aba783e147f7`
- `21d4dfa3343f0ea1fdad6ceb205ce97d8113e187c9a0ebbdb45c836540de88f9`
- `47c1e9039e05b29876e9aaebe29d5d96bbeb435fedbe7176f5d9eb45c5a2c41b`
- `5348a39fc2c2e6a6af0908a80f2dacd5108274c9811f67298fe77fb98a117c4d`
- `659ab4aa3c23745cf27059c305b32d27ffee9a63b49b65650154ba5b34fe265a`

Current interpretation:
- closely adjacent to `F07`
- likely a sibling mesh/material family that carries one extra interpolant payload

What is not yet proven:
- whether the extra output corresponds to lighting, mask, tangent-space helper, or mode-specific carry data

### `F09` Mesh Material Vertex 10→8

Confidence:
- `high`

Confirmed signals:
- stage: `vs`
- input semantics:
  - `POSITION0, NORMAL0, POSITION1, POSITION2, POSITION3, POSITION4, POSITION5, POSITION6, POSITION7, TEXCOORD0`
- output semantics:
  - `COLOR0, COLOR1, TEXCOORD0, TEXCOORD1, TEXCOORD2, TEXCOORD3, TEXCOORD5, SV_POSITION0`
- family size: `12`
- capture support:
  - `12` hashes appear in all three broad reference captures
  - `0` hashes are single-capture only
- dominant reflection signature: `vs br=0 cb=0 in=10 out=8`

Representative hashes:
- `1813a8c47f4322852b3306bb065bba5539d6ae5d21bdc890eba05a4c800143f8`
- `1ce2d170fb680b28b3e8752e07be81cb1164c65fd08976d643b417c63843a9a7`
- `3891f075978746544b401c29995b7988bac0ab0641df0e827a537283683922e0`
- `4bc3fd89bff0ca1d5537a74332ccad5060a237428e80a8d5b3a565a4ed63753a`
- `61c81453292e6fdb034521bddbd995eec9efed99d9c790e5015e4504cbdf9643`

Current interpretation:
- likely a lighter sibling to the 12-input mesh families
- candidate for geometry that does not need the full color+tangent payload

What is not yet proven:
- whether this is a separate asset class or only a lower-feature branch of the same material pipeline

## Current Registry Takeaways

Strong current split:
- screen-space/helper families are highly recurrent and compact:
  - `F01`
  - `F02`
  - `F06`
- richer pixel material/compositor families are also highly recurrent:
  - `F03`
  - `F04`
  - `F05`
- complex mesh/material vertex families are strongly recurrent and deserve first-class tracking:
  - `F07`
  - `F08`
  - `F09`

Important catalog signal:
- single-capture-only hashes are limited but non-trivial:
  - `95` pixel shaders
  - `62` vertex shaders

Interpretation:
- the core render backbone is already visible and stable
- the remaining single-capture tail is small enough to inspect as “mode-specific expansion” rather than treating the whole catalog as undifferentiated

## Open Questions

- Which of `F03` / `F04` / `F05` are shared material families versus mode-specific compositor families?
- Are `F07` / `F08` / `F09` primarily sim/CAS geometry, object/build-buy geometry, or mixed shared infrastructure?
- Which single-capture-only hashes cluster naturally under the seeded families, and which demand new family IDs?
- Are the `8` embedded `DXBC` blobs in `TS4_x64.exe` and `TS4_x64_fpb.exe` members of `F01`/`F02`/`F06`, or are they unrelated bootstrap helpers?

## Next Update Rules

When this registry is updated, prefer:
- adding evidence under existing family IDs before inventing new family IDs
- promoting a hypothesis only when supported by:
  - stable runtime recurrence
  - semantic/reflection clustering
  - and at least one code or binary clue
- keeping “confirmed” and “interpreted” statements clearly separated
