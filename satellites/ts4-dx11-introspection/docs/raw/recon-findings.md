# TS4 DX11 Recon Findings

Recon run date:
- 2026-04-21 UTC

Target scanned:
- `C:\Games\The Sims 4\Game\Bin`

## High-confidence findings

- `TS4_x64.exe` imports `D3D11CreateDevice`, `D3D11CreateDeviceAndSwapChain`, and `CreateDXGIFactory1`.
- `TS4_x64_fpb.exe` imports the same DX11 entry points.
- `TS4_Launcher_x64.exe` imports `CreateDXGIFactory1` but not direct `d3d11.dll` entry points.
- `TS4_x64.exe` and `TS4_x64_fpb.exe` both contain embedded `DXBC` blocks.

## MVP implications

- A local `d3d11.dll` proxy is a viable low-friction interception path for the two primary x64 game executables.
- `Present` can be reached through the swap chain created during `D3D11CreateDeviceAndSwapChain`.
- Shader creation can be observed directly on the returned `ID3D11Device`.
- The launcher should be treated as secondary; the main capture focus should stay on the two game executables.

## Reproduce

Run:

```powershell
powershell -ExecutionPolicy Bypass -File .\satellites\ts4-dx11-introspection\scripts\run-recon.ps1
```

Generated machine-local reports are written under `docs/reports/` and intentionally ignored by git.
