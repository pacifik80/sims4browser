# ADR 0001: Solution architecture and boundaries

## Status

Accepted

## Context

The application must support large resource sets, a WinUI 3 desktop shell, persistent indexing, and separate concerns for parsing, preview, export, and logical asset resolution.

## Decision

Use a multi-project solution with clear boundaries:

- `App` for shell/UI composition
- `Core` for contracts and domain primitives
- `Packages` for Sims 4 package adapter logic
- `Indexing` for SQLite storage and background indexing
- `Assets` for logical asset graph resolution
- `Preview` for preview-model generation
- `Export` for export services
- `Audio` for decode/playback/export
- `Tests` for unit/integration coverage

## Consequences

Positive:

- supports vertical-slice delivery without giant shared god-assemblies
- makes third-party dependency usage explicit
- lets preview/export evolve independently from package parsing

Trade-offs:

- more project references to maintain
- requires discipline to keep contracts stable and avoid leaking parser-specific details
