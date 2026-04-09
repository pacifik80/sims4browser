# ADR 0002: Dependency selection and licensing posture

## Status

Accepted

## Context

The tool needs package access, WinUI tabular browsing, SQLite persistence, optional 3D rendering/export, and audio playback while staying read-only and avoiding GPLv3 contamination unless explicitly intended.

## Decision

Primary planned dependencies:

- `LlamaLogic.Packages`
  - chosen as the primary package access library because it is purpose-built for Sims packages and aligns with the requested constraints
- `WinUI.TableView`
  - chosen for high-density list/grid browsing in WinUI 3
- `Microsoft.WindowsAppSDK`
  - required for WinUI 3 desktop shell
- `CommunityToolkit.Mvvm`
  - chosen for concise MVVM commands/observable state with permissive licensing
- `Microsoft.Data.Sqlite`
  - chosen for the persistent index/cache store
- `NAudio`
  - chosen for Windows audio playback and WAV handling
- `HelixToolkit.WinUI.SharpDX`
  - chosen for WinUI-compatible 3D preview
- `HelixToolkit.SharpDX.Assimp` or `SharpAssimp`
  - chosen as the likely FBX bridge, behind an adapter so it can be swapped if version/licensing friction appears

Reference material only:

- `Llama-Logic/Binary-Templates`
- The Sims 4 Modders Reference
- `s4pe`

## Consequences

Positive:

- package parsing and UI concerns both use libraries aligned with the problem domain
- adapters preserve flexibility where 3D/export or audio support remains risky

Risks:

- some packages may lag new .NET/Windows App SDK releases
- 3D/audio packages may require native assets and packaging validation
- exact Sims 4 format support still depends on reverse-engineered knowledge beyond the library surface
