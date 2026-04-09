# Sample Data Strategy

## Goals

- Keep fixtures small enough for repository-friendly testing.
- Cover one representative path for each supported preview/export class.
- Document what every fixture proves.

## Fixture categories

- minimal package with a few generic resource rows for scanner/index tests
- text-like resource for text preview dispatch
- binary resource for hex preview fallback
- one supported image sample per decode family where legally distributable
- one Build/Buy-oriented sample package
- one CAS-oriented sample package
- one 3D export sample with mesh/material/texture links
- one audio sample for the first supported decode path

## Storage guidance

- Prefer tiny synthetic or user-created test assets where possible.
- If real package-derived fixtures are required, keep only the smallest lawful subset needed for regression coverage.
- Avoid checking large game assets into the repo.

## Validation mapping

- `scanner-basic` validates package enumeration and metadata extraction.
- `image-dispatch-*` validates preview dispatch and PNG export paths.
- `asset-buildbuy-basic` validates logical Build/Buy graph resolution.
- `asset-cas-basic` validates CAS graph resolution.
- `scene-export-basic` validates FBX + textures + manifest creation.
- `audio-basic` validates decode/playback/export for the first supported path.
