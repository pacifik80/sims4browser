# Junk Holding Area

This folder is the quarantine zone for local artifacts that are likely not worth keeping in the main repo layout but are not deleted yet.

## Use It For

- loose root logs
- one-off probe outputs
- temporary notes or scratch source files
- local artifacts that were useful once but are unlikely to become durable tools or docs

## Do Not Use It For

- durable documentation
- reusable probe tools
- committed tests
- reference material that should live under `docs/references/`

## Current Policy

- `junk/local-artifacts/` is for local files only and is ignored by Git.
- `tmp/` remains the active scratch workspace because existing probe flows default there.
- If something in `junk/` turns out to be useful later, promote it into `docs/`, `tools/`, or `tests/`.
