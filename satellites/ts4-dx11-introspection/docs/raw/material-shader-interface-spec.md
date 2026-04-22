# TS4 Material Shader Interface Spec

Last updated:
- 2026-04-21 UTC

Status:
- first practical specification artifact derived from stable runtime captures
- intended to support package-side material parsing in the main project
- focused on shader-visible interfaces, not yet on final package field naming

## Why This Exists

The main browser project needs to read package-side material definitions correctly:
- textures
- samplers
- constant-buffer-like parameters
- geometry/input layout expectations

The runtime introspection work is useful only if it moves us toward that goal.

This document states what the current runtime work already gives us as actual specification surface.

## What We Can Specify Already

From stable `shader_created` + `D3DReflect` captures, we can already extract:
- exact shader hash
- shader stage
- bytecode size
- bound resource list:
  - resource name
  - resource type
  - bind slot
  - bind count
- constant buffer list:
  - cbuffer name
  - total size
  - variable names
  - variable types
  - offsets and sizes
- input semantic contract
- output semantic contract

That means we can already write a real shader-interface spec for many runtime-used shaders.

## What This Does For The Main Project

This directly helps the package/material reader in three ways.

### 1. It tells us what a material-facing shader expects

Example:
- shader hash:
  - `0d5a27655d43f10a7a98be014cb73741ff6c3139e45273e1ed93183b5e775287`
- stage:
  - `ps`
- bound resources:
  - `maptex` at texture slot `0`
  - `tex` at texture slot `1`
  - `sampler_maptex` at sampler slot `0`
  - `sampler_tex` at sampler slot `1`
  - `Constants` at cbuffer slot `0`
- constant buffer variables:
  - `compx : float4`
  - `compy : float4`
  - `mapScale : float4`
  - `scale : float2`

This is already a usable interface contract.

The package-side consequence:
- if a material definition cannot provide two textures, two samplers, and a parameter block compatible with those fields, it is not sufficient for this shader path as-is.

### 2. It tells us which shaders are texture-light vs parameter-heavy

Example:
- shader hash:
  - `05f6262aaca21ffe3630fa2f4fb44aa86175b4600537df79aa8f211fc8117e66`
- stage:
  - `ps`
- bound resources:
  - `tex`
  - `sampler_tex`
  - `Constants`
- constant buffer variables:
  - `fsize : float4`
  - `offset : float4`
  - `scolor : float4`
  - `texscale : float4`

The package-side consequence:
- some material paths are not “just bind a texture”.
- they require structured parameter payloads that look much closer to authored material records.

### 3. It tells us what vertex data a material family assumes

Example:
- shader hash:
  - `1813a8c47f4322852b3306bb065bba5539d6ae5d21bdc890eba05a4c800143f8`
- stage:
  - `vs`
- input semantics:
  - `POSITION0`
  - `NORMAL0`
  - `POSITION1..POSITION7`
  - `TEXCOORD0`
- output semantics:
  - `COLOR0`
  - `COLOR1`
  - `TEXCOORD0`
  - `TEXCOORD1`
  - `TEXCOORD2`
  - `TEXCOORD3`
  - `TEXCOORD5`
  - `SV_POSITION0`

The package-side consequence:
- this is not a trivial mesh path.
- any main-project material or geometry reader that ignores these extra vertex channels will inevitably mis-model part of the TS4 material pipeline.

## What This Still Does Not Give Us

This runtime work still does not prove:
- which exact package resource type owns each shader binding
- which exact `MATD`, `MTST`, `TGI`, or variant field maps to each texture/sampler/cbuffer variable
- which shader family belongs to which gameplay domain with certainty
- which parameter values are defaulted by engine code versus authored in package data

So the current state is:
- we do have interface specifications
- we do not yet have a complete package-to-shader binding specification

## Practical Rule For The Main Project

The main project should stop treating materials as “unknown blobs” and instead treat them as candidates for satisfying shader interface contracts.

Concretely:
- package-side material parsing should aim to produce:
  - named texture bindings
  - sampler bindings
  - numeric/vector parameter bindings
- then those bindings should be compared against known shader-interface families from runtime

That is the bridge:
- runtime tells us what the renderer expects
- package parsing tells us what the asset provides
- correctness comes from reconciling the two

## Immediate Engineering Consequences

The next useful outputs for the main project are not more generic maps.

They are:
- a normalized per-shader interface table
- a normalized per-family interface table
- and then a package-side matcher that asks:
  - can this material record satisfy family `F03`, `F04`, `F05`, `F07`, `F08`, or `F09`?

## Open Gaps

- We still need a generated family-level interface table instead of only representative examples.
- We still need to correlate shader resource names like `tex`, `maptex`, `sampler_tex`, `Constants`, `compx`, `mapScale`, `texscale` with package-side field names and structures.
- We still need to determine whether those names are stable enough across a family to treat as family-level contracts.

## Bottom Line

The runtime project is useful to the main project only insofar as it yields interface contracts.

It is already starting to do that.

The correct next step is:
- stop expanding narrative-only documentation,
- and start generating family-level interface specs that the package/material reader can actually target.
