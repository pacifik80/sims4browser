# TS4 DX11 Context-Tagged Capture Analysis Workflow

This document defines the minimum honest post-capture workflow for helper-family tagged sessions.

Use it when the question is not:

- "how do we capture a tagged session?"

but:

- "after the session exists, how do we compare it cleanly enough to justify a helper-family uplift?"

Related docs:

- [TS4 DX11 Context-Tagged Capture Contract](context-tagged-capture-contract.md)
- [TS4 DX11 Context-Tagged Capture Recipes](context-tagged-capture-recipes.md)

## Why this exists

The runner presets and recipes now make tagged capture creation easy enough.

The next smaller blocker was post-capture comparison:

- which sessions should be compared
- when to use one-to-one compare versus group compare
- what minimum checks must happen before any family-level claim is promoted

## Minimum analysis path

For helper-family tagged work, the minimum honest path is:

1. collect one tagged target session
2. collect one nearby tagged control session when possible
3. verify that both sessions really have `context-tags.json`
4. compare target against control with the existing companion compare flow
5. promote a helper-family claim only if a narrowed packet shows a real contextual shift

## Standard helper compare wrapper

Use the wrapper script:

- `scripts/compare-tagged-helper-captures.ps1`

It does three bounded things:

1. resolves capture paths
2. verifies that both target and control sessions have `context-tags.json`
3. runs `compare` or `compare-groups` automatically depending on the input set

## Common commands

### One tagged target vs one control

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\compare-tagged-helper-captures.ps1 `
  -Target 20260422-210000 `
  -Control 20260422-211500 `
  -FamilyFocus generated-light
```

### Multiple tagged targets vs multiple controls

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\compare-tagged-helper-captures.ps1 `
  -Target 20260422-210000,20260422-213000 `
  -Control 20260422-211500,20260422-214500 `
  -FamilyFocus projection-reveal
```

## What counts as a useful control

A control session should be:

- nearby in capture conditions
- stable
- not just another broad wandering session
- tagged with the same helper-family focus
- different mainly by scene emphasis, not by missing metadata

Safe examples:

- same world mode, different emphasis
- same room, but without the target light-heavy fixture focus
- same nearby area, but without the reveal/projection-heavy fixture emphasis

## Minimum interpretation rule

Do not promote a helper-family ownership claim from tagged sessions unless at least one narrowed packet shows a real contextual shift, for example:

- present in target and absent in control
- materially enriched in target versus control
- repeated across target group while weak or absent across control group

## What this workflow does not do

It does not:

- prove draw/pass ownership by itself
- replace later state-level analysis
- remove the need for family-local interpretation

It does:

- standardize the first honest comparison step after tagged capture collection
- keep the helper-family lane from drifting back into ad hoc broad-session reading
- make the next post-capture uplift procedural instead of improvised
