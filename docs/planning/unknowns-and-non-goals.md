# Unknowns And Non-Goals

This file separates genuine unknowns from things we deliberately are not trying to solve yet.

## Known Unknowns

- Exact authoritative full-Sim assembly rules across all species, ages, occult forms, and frame variants.
- Reliable rig/body/head resolution rules for the full character pipeline, not just the current human-first slice.
- Complete skintone, region-map, and material synthesis behavior needed for a viewer that is both honest and useful.
- Correct application order and coverage for `BGEO`, `DMAP`, `BOND`, and related deformer/modifier channels.
- How far Build/Buy and CAS shader semantics can be represented faithfully in the viewport without claiming in-game renderer parity.

## Deliberate Non-Goals For Now

- Writing, repacking, or mutating `.package` files.
- Pretending unsupported paths are solved with broad heuristics or silently "best effort" output.
- Shipping per-asset hacks as if they were durable architecture.
- Chasing full renderer parity before the underlying authoritative data paths are in place.
- Cross-platform UI support. This remains a Windows desktop project.

## Deferred Until The Human-First Path Is Solid

- Broad occult coverage
- Non-human species with comparable assembly fidelity
- Full pet pipeline
- Full-character export for every unresolved character variant

## Decision Rule

If a new limitation is discovered:

- add it here if it is still unresolved or intentionally deferred
- add it to `docs/known-limitations.md` if it affects current user-visible behavior
