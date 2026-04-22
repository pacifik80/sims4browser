# Native Component

Current implementation:
- ships as a local `d3d11.dll` proxy that forwards to the system runtime;
- hooks `D3D11CreateDevice` and `D3D11CreateDeviceAndSwapChain`;
- patches the returned `ID3D11Device`, immediate context, and swap chain vtables;
- logs `Present`, shader creation, stable shader hashes, `D3DReflect` output, and bounded per-frame state/draw events.

Key decisions:
- `d3d11.dll` proxy was chosen over a `dxgi.dll` proxy because the local TS4 binaries import `D3D11CreateDevice` and `D3D11CreateDeviceAndSwapChain` directly;
- events are emitted as JSON Lines to a named pipe, with a temp-file fallback when the companion is absent;
- `F10` starts a bounded capture window intended for later expansion.

Build the proxy with [build-native.ps1](C:/Users/stani/PROJECTS/Sims4Browser/satellites/ts4-dx11-introspection/scripts/build-native.ps1).
