// =====================================================================
// SwapChainPanelHost.cs — D3D11 -> WinUI 3 SwapChainPanel host.
// Vortice 3.8.3 (the maintained SharpDX successor). net8.0-windows, x64.
//
// Verified facts (research workflow whbbqaop6, against cached 3.8.3 DLLs):
//  * ISwapChainPanelNative is Vortice.DXGI.ISwapChainPanelNative
//    (GUID f92f19d2-...), ctor is (nint), SetSwapChain(IDXGISwapChain)->Result.
//  * MatrixTransform (DPI inverse-scale) is on IDXGISwapChain2 — QI for it.
//  * CreateSwapChainForComposition(IUnknown device, desc, IDXGIOutput=null)
//    returns IDXGISwapChain1.
//  * The panel is bound by QueryInterface-ing its native IUnknown for the
//    ISwapChainPanelNative IID (the one line the probe could not build-verify
//    without a WinUI head; done here with an explicit Marshal.QueryInterface).
// =====================================================================
using System;
using System.Drawing;                       // SizeF
using System.Numerics;                      // Matrix3x2
using System.Runtime.InteropServices;       // Marshal.QueryInterface
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SharpGen.Runtime;                      // Result, CheckError, Failure
using Vortice.Direct3D;                      // DriverType, FeatureLevel
using Vortice.Direct3D11;                    // ID3D11Device1/Context1, RTV
using Vortice.DXGI;                          // IDXGIFactory2, SwapChainDescription1, ISwapChainPanelNative, IDXGISwapChain1/2
using Vortice.Mathematics;                   // Viewport, Color4
using WinRT;                                 // IWinRTObject (CsWinRT)
using static Vortice.Direct3D11.D3D11;       // D3D11CreateDevice
using static Vortice.DXGI.DXGI;              // CreateDXGIFactory1

namespace Sims4ResourceExplorer.App.Rendering.Vortice;

/// <summary>
/// Owns a D3D11 device + a DXGI composition swap chain bound to a WinUI 3
/// <see cref="SwapChainPanel"/>, with DPI-correct (CompositionScale) backbuffer sizing,
/// inverse-scale transform, resize and present. No scene knowledge.
/// </summary>
public sealed class SwapChainPanelHost : IDisposable
{
    private static readonly FeatureLevel[] s_levels =
    [
        FeatureLevel.Level_11_1, FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1, FeatureLevel.Level_10_0
    ];

    private const Format ColorFormat = Format.B8G8R8A8_UNorm; // required for composition

    private readonly SwapChainPanel _panel;
    private IDXGIFactory2 _factory = null!;
    private ID3D11Device1 _device = null!;
    private ID3D11DeviceContext1 _context = null!;
    private IDXGISwapChain1 _swapChain = null!;
    private IDXGISwapChain2 _swapChain2 = null!;        // MatrixTransform (DPI) lives here
    private ISwapChainPanelNative _panelNative = null!;
    private ID3D11Texture2D _backBuffer = null!;
    private ID3D11RenderTargetView _rtv = null!;
    private bool _initialized;
    private bool _disposed;

    public SwapChainPanelHost(SwapChainPanel panel) => _panel = panel;

    public ID3D11Device1 Device => _device;
    public ID3D11DeviceContext1 Context => _context;
    public ID3D11RenderTargetView Rtv => _rtv;

    /// <summary>Backbuffer size in PHYSICAL pixels = DIP * CompositionScale.</summary>
    public SizeF PixelSize => new(
        MathF.Max(1f, (float)_panel.ActualWidth * _panel.CompositionScaleX + 0.5f),
        MathF.Max(1f, (float)_panel.ActualHeight * _panel.CompositionScaleY + 0.5f));

    /// <summary>
    /// Call from <see cref="SwapChainPanel"/>.Loaded (NOT the ctor): ActualWidth and
    /// CompositionScale are only valid after Loaded — building earlier yields a 1x1 /
    /// mis-scaled buffer that never self-corrects.
    /// </summary>
    public void Initialize()
    {
        _factory = CreateDXGIFactory1<IDXGIFactory2>();

        DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport; // needed for B8G8R8A8 composition
        Result hr = D3D11CreateDevice(
            null, DriverType.Hardware, flags, s_levels,
            out ID3D11Device tmpDev, out _, out ID3D11DeviceContext tmpCtx);
        if (hr.Failure)
        {
            D3D11CreateDevice(null, DriverType.Warp, flags, s_levels,
                out tmpDev, out _, out tmpCtx).CheckError();
        }

        _device = tmpDev.QueryInterface<ID3D11Device1>();
        _context = tmpCtx.QueryInterface<ID3D11DeviceContext1>();
        tmpCtx.Dispose();
        tmpDev.Dispose();

        CreateSwapChainAndBind();
        CreateSizeDependentResources();

        _panel.SizeChanged += OnSizeChanged;
        _panel.CompositionScaleChanged += OnCompositionScaleChanged;
        _initialized = true;
    }

    private void CreateSwapChainAndBind()
    {
        SizeF px = PixelSize;
        var desc = new SwapChainDescription1
        {
            Width = (uint)px.Width,                       // PIXELS
            Height = (uint)px.Height,                      // PIXELS
            Format = ColorFormat,
            BufferCount = 2,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,   // (1,0): flip model forbids swapchain MSAA
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipSequential,          // composition requires a flip effect
            AlphaMode = AlphaMode.Ignore,                    // opaque full-panel 3D
            Flags = SwapChainFlags.None
        };

        _swapChain = _factory.CreateSwapChainForComposition(_device, desc, null);
        _swapChain2 = _swapChain.QueryInterface<IDXGISwapChain2>();

        // Bind the swap chain to the panel: QI the panel's native IUnknown for the
        // ISwapChainPanelNative IID. (CsWinRT exposes the raw pointer via IWinRTObject.)
        Guid iid = typeof(ISwapChainPanelNative).GUID;
        nint unknown = ((IWinRTObject)_panel).NativeObject.ThisPtr;
        int qi = Marshal.QueryInterface(unknown, in iid, out nint nativePtr);
        if (qi < 0)
        {
            throw new InvalidOperationException($"QueryInterface(ISwapChainPanelNative) failed: 0x{qi:X8}");
        }
        _panelNative = new ISwapChainPanelNative(nativePtr);
        _panelNative.SetSwapChain(_swapChain).CheckError();

        ApplyInverseScale();
    }

    // DPI: inverse-scale the stretched buffer so it maps 1:1 to physical pixels.
    private void ApplyInverseScale() => _swapChain2.MatrixTransform = new Matrix3x2
    {
        M11 = 1f / _panel.CompositionScaleX,
        M22 = 1f / _panel.CompositionScaleY
    };

    private void CreateSizeDependentResources()
    {
        _backBuffer = _swapChain.GetBuffer<ID3D11Texture2D>(0);
        _rtv = _device.CreateRenderTargetView(_backBuffer);
    }

    /// <summary>Bind RTV + viewport and clear the colour buffer. Caller sets depth + draws.</summary>
    public void BeginFrame()
    {
        SizeF px = PixelSize;
        _context.OMSetRenderTargets(_rtv);
        _context.RSSetViewport(new Viewport(px.Width, px.Height));
        _context.ClearRenderTargetView(_rtv, new Color4(0.10f, 0.10f, 0.12f, 1f));
    }

    public void Present() => _swapChain.Present(1, PresentFlags.None); // vsync

    /// <summary>Resize backbuffer to the current pixel size (no-op if unchanged) + re-apply DPI.</summary>
    public void Resize()
    {
        if (!_initialized)
        {
            return;
        }
        SizeF px = PixelSize;
        var cur = _swapChain.Description1;
        bool sameSize = (uint)px.Width == cur.Width && (uint)px.Height == cur.Height;
        if (!sameSize)
        {
            _context.UnsetRenderTargets();
            _rtv.Dispose();
            _backBuffer.Dispose();
            _swapChain.ResizeBuffers(2, (uint)px.Width, (uint)px.Height, ColorFormat, SwapChainFlags.None).CheckError();
            CreateSizeDependentResources();
        }
        ApplyInverseScale();
        Resized?.Invoke();
    }

    /// <summary>Raised after a real backbuffer resize so the renderer can rebuild its depth buffer.</summary>
    public event Action? Resized;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Resize();
    private void OnCompositionScaleChanged(SwapChainPanel sender, object e) => Resize();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _panel.SizeChanged -= OnSizeChanged;
        _panel.CompositionScaleChanged -= OnCompositionScaleChanged;
        try { _panelNative?.SetSwapChain(null); } catch { /* detaching at teardown */ }
        _panelNative?.Dispose();
        _rtv?.Dispose();
        _backBuffer?.Dispose();
        _swapChain2?.Dispose();
        _swapChain?.Dispose();
        _context?.Dispose();
        _device?.Dispose();
        _factory?.Dispose();
    }
}
