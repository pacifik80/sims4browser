// =====================================================================
// SimHeadPocRenderer.cs — ONE lit, textured Sim head with a live slider.
// Vortice 3.8.3. Pulls geometry/texture from the existing CanonicalScene
// types; DPI/RTV owned by SwapChainPanelHost. This is the proof that a
// custom HLSL pixel shader + live constant renders cleanly on raw D3D11
// (the thing that fought us for a dozen builds on HelixToolkit).
// =====================================================================
using System;
using System.Numerics;
using System.Runtime.InteropServices;
using SharpGen.Runtime;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Sims4ResourceExplorer.Core;
using MapFlags = Vortice.Direct3D11.MapFlags;

namespace Sims4ResourceExplorer.App.Rendering.Vortice;

[StructLayout(LayoutKind.Sequential)]
internal struct FrameCb            // 3 matrices (192) + 4 vec4 (64) = 256 bytes, 16-aligned
{
    public Matrix4x4 World;
    public Matrix4x4 View;
    public Matrix4x4 Projection;
    public Vector4 LightDir0;      // xyz = TOWARD-light (un-negated), w unused
    public Vector4 LightDir1;
    public Vector4 LightColor0;    // x = dir0 grey 0.502, y = dir1 grey 0.239
    public Vector4 Ambient;        // x = 0.451
}

[StructLayout(LayoutKind.Sequential)]
internal struct TintCb { public Vector4 DebugTint; } // the ONE live slider feed

public sealed class SimHeadPocRenderer : IDisposable
{
    private readonly SwapChainPanelHost _host;
    private ID3D11Buffer _vb = null!, _ib = null!, _frameCb = null!, _tintCb = null!;
    private ID3D11InputLayout _layout = null!;
    private ID3D11VertexShader _vs = null!;
    private ID3D11PixelShader _ps = null!;
    private ID3D11Texture2D _tex = null!;
    private ID3D11ShaderResourceView _srv = null!;
    private ID3D11SamplerState _samp = null!;
    private ID3D11RasterizerState _raster = null!;
    private ID3D11DepthStencilView _dsv = null!;
    private ID3D11Texture2D _depth = null!;
    private int _indexCount;
    private Vector3 _center;
    private float _radius = 1f;
    private bool _ready;

    /// <summary>Live slider value (0..1). Bound from the Slider in the constructor window.</summary>
    public float DebugTint { get; set; } = 1f;

    public SimHeadPocRenderer(SwapChainPanelHost host)
    {
        _host = host;
        _host.Resized += OnResized;
    }

    /// <summary>Builds all GPU resources from a scene + decoded BGRA atlas. Call once after host.Initialize().</summary>
    public void Load(CanonicalScene scene, byte[] bgra, int texW, int texH)
    {
        var pick = SimHeadData.PickHeadMeshAndSkin(scene)
                   ?? throw new InvalidOperationException("No skin BaseColor atlas mesh in scene.");
        var buffers = SimHeadData.BuildInterleaved(pick.Mesh);
        _indexCount = buffers.Indices.Length;
        _center = buffers.Center;
        _radius = buffers.Radius;
        var dev = _host.Device;

        _vb = dev.CreateBuffer(buffers.Interleaved, BindFlags.VertexBuffer, ResourceUsage.Immutable);
        _ib = dev.CreateBuffer(buffers.Indices, BindFlags.IndexBuffer, ResourceUsage.Immutable);

        var texDesc = new Texture2DDescription
        {
            Width = (uint)texW,
            Height = (uint)texH,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.B8G8R8A8_UNorm,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Immutable,
            BindFlags = BindFlags.ShaderResource,
            CPUAccessFlags = CpuAccessFlags.None,
            MiscFlags = ResourceOptionFlags.None
        };
        unsafe
        {
            fixed (byte* p = bgra)
            {
                var sub = new SubresourceData((nint)p, (uint)(texW * 4));
                _tex = dev.CreateTexture2D(texDesc, new[] { sub });
            }
        }
        _srv = dev.CreateShaderResourceView(_tex);
        _samp = dev.CreateSamplerState(new SamplerDescription
        {
            Filter = Filter.MinMagMipLinear,
            AddressU = TextureAddressMode.Wrap,
            AddressV = TextureAddressMode.Wrap,
            AddressW = TextureAddressMode.Wrap,
            ComparisonFunc = ComparisonFunction.Never,
            MinLOD = 0,
            MaxLOD = float.MaxValue
        });

        Result vsHr = Compiler.Compile(Hlsl, "VSMain", "SimHeadPoc.hlsl", "vs_5_0", out Blob vsCode, out Blob vsErr);
        if (vsHr.Failure)
        {
            throw new InvalidOperationException($"VS compile failed: {vsErr?.AsString() ?? vsHr.Description}");
        }
        Result psHr = Compiler.Compile(Hlsl, "PSMain", "SimHeadPoc.hlsl", "ps_5_0", out Blob psCode, out Blob psErr);
        if (psHr.Failure)
        {
            throw new InvalidOperationException($"PS compile failed: {psErr?.AsString() ?? psHr.Description}");
        }
        _vs = dev.CreateVertexShader(vsCode.AsSpan());
        _ps = dev.CreatePixelShader(psCode.AsSpan());
        _layout = dev.CreateInputLayout(new[]
        {
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 12, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 24, 0),
        }, vsCode.AsSpan());
        vsCode.Dispose();
        vsErr?.Dispose();
        psCode.Dispose();
        psErr?.Dispose();

        _frameCb = dev.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<FrameCb>(),
            BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));
        _tintCb = dev.CreateBuffer(new BufferDescription((uint)Marshal.SizeOf<TintCb>(),
            BindFlags.ConstantBuffer, ResourceUsage.Dynamic, CpuAccessFlags.Write));

        _raster = dev.CreateRasterizerState(new RasterizerDescription
        {
            FillMode = FillMode.Solid,
            CullMode = CullMode.None,       // double-sided, matching the old Helix path
            FrontCounterClockwise = false,
            DepthClipEnable = true
        });

        CreateDepth();
        _ready = true;
    }

    private void CreateDepth()
    {
        var px = _host.PixelSize;
        _depth = _host.Device.CreateTexture2D(new Texture2DDescription
        {
            Width = (uint)px.Width,
            Height = (uint)px.Height,
            MipLevels = 1,
            ArraySize = 1,
            Format = Format.D32_Float,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            BindFlags = BindFlags.DepthStencil
        });
        _dsv = _host.Device.CreateDepthStencilView(_depth);
    }

    public void Render()
    {
        if (!_ready)
        {
            return;
        }
        var ctx = _host.Context;
        var px = _host.PixelSize;

        _host.BeginFrame();                                   // RTV + viewport + colour clear
        ctx.OMSetRenderTargets(_host.Rtv, _dsv);
        ctx.ClearDepthStencilView(_dsv, DepthStencilClearFlags.Depth, 1f, 0);

        // Frame the mesh from its bounding sphere (the head sits high in world space).
        float aspect = px.Width / px.Height;
        var eye = _center + new Vector3(0f, 0f, -_radius * 2.6f);
        var view = Matrix4x4.CreateLookAtLeftHanded(eye, _center, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(MathF.PI / 4f, aspect, _radius * 0.02f, _radius * 12f);

        var frame = new FrameCb
        {
            World = Matrix4x4.Transpose(Matrix4x4.Identity),
            View = Matrix4x4.Transpose(view),
            Projection = Matrix4x4.Transpose(proj),
            // TOWARD-light vectors (captured rig). Source: SceneViewportRenderer light setup.
            LightDir0 = new Vector4(Vector3.Normalize(new Vector3(0.417f, 0.460f, 0.784f)), 0f),
            LightDir1 = new Vector4(Vector3.Normalize(new Vector3(-0.916f, 0.100f, 0.389f)), 0f),
            LightColor0 = new Vector4(0.502f, 0.239f, 0f, 0f),
            Ambient = new Vector4(0.451f, 0f, 0f, 0f)
        };
        UpdateCb(ctx, _frameCb, frame);
        UpdateCb(ctx, _tintCb, new TintCb { DebugTint = new Vector4(DebugTint, DebugTint, DebugTint, 1f) });

        ctx.IASetInputLayout(_layout);
        ctx.IASetVertexBuffer(0, _vb, (uint)(8 * sizeof(float)), 0u);
        ctx.IASetIndexBuffer(_ib, Format.R32_UInt, 0);
        ctx.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        ctx.RSSetState(_raster);
        ctx.VSSetShader(_vs);
        ctx.PSSetShader(_ps);
        ctx.VSSetConstantBuffer(0, _frameCb);
        ctx.PSSetConstantBuffer(0, _frameCb);
        ctx.PSSetConstantBuffer(1, _tintCb);
        ctx.PSSetShaderResource(0, _srv);
        ctx.PSSetSampler(0, _samp);
        ctx.DrawIndexed((uint)_indexCount, 0, 0);

        _host.Present();
    }

    private void OnResized()
    {
        _dsv?.Dispose();
        _depth?.Dispose();
        if (_ready)
        {
            CreateDepth();
        }
    }

    private static void UpdateCb<T>(ID3D11DeviceContext ctx, ID3D11Buffer cb, T data) where T : struct
    {
        MappedSubresource map = ctx.Map(cb, MapMode.WriteDiscard, MapFlags.None);
        Marshal.StructureToPtr(data, map.DataPointer, false);
        ctx.Unmap(cb);
    }

    public void Dispose()
    {
        _host.Resized -= OnResized;
        _dsv?.Dispose();
        _depth?.Dispose();
        _raster?.Dispose();
        _samp?.Dispose();
        _srv?.Dispose();
        _tex?.Dispose();
        _tintCb?.Dispose();
        _frameCb?.Dispose();
        _layout?.Dispose();
        _vs?.Dispose();
        _ps?.Dispose();
        _ib?.Dispose();
        _vb?.Dispose();
    }

    private const string Hlsl = @"
cbuffer Frame : register(b0)
{
    float4x4 World;
    float4x4 View;
    float4x4 Projection;
    float4   LightDir0;
    float4   LightDir1;
    float4   LightColor0;
    float4   Ambient;
};

cbuffer Tint : register(b1)
{
    float4 DebugTint;
};

Texture2D    BaseMap  : register(t0);
SamplerState BaseSamp : register(s0);

struct VSIn  { float3 pos : POSITION; float3 nrm : NORMAL; float2 uv : TEXCOORD; };
struct VSOut { float4 pos : SV_Position; float3 nrm : NORMAL; float2 uv : TEXCOORD; };

VSOut VSMain(VSIn i)
{
    VSOut o;
    float4 wp = mul(float4(i.pos, 1.0), World);
    o.pos = mul(mul(wp, View), Projection);
    o.nrm = normalize(mul(i.nrm, (float3x3)World));
    o.uv  = i.uv;
    return o;
}

float4 PSMain(VSOut i) : SV_Target
{
    float3 albedo = BaseMap.Sample(BaseSamp, i.uv).rgb;
    float3 n = normalize(i.nrm);
    float nl0 = saturate(dot(n, LightDir0.xyz));
    float nl1 = saturate(dot(n, LightDir1.xyz));
    float lighting = Ambient.x + LightColor0.x * nl0 + LightColor0.y * nl1;
    float3 color = albedo * lighting * DebugTint.rgb;
    return float4(color, 1.0);
}
";
}
