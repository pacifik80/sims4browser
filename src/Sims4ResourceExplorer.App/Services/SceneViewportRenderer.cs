using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.WinUI.SharpDX;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App.Services;

/// <summary>
/// Bundle of viewport-rendering inputs read from the hosting view-model. The
/// renderer never touches MainViewModel directly — the window constructs one
/// of these and passes it through.
/// </summary>
public sealed record SceneRenderConfig(
    SceneRenderMode RenderMode,
    string? TextureSlot,
    SceneUvChannelOverride UvChannel,
    SceneVariantOption? Variant);

/// <summary>
/// Renders a CanonicalScene into a HelixToolkit Viewport3DX. Extracted from
/// MainWindow during R2 of the multi-packet refactor so both MainWindow and
/// the upcoming SimConstructorWindow can share the exact same rendering
/// pipeline (visual parity is mandatory).
///
/// The renderer is stateless — it does not store the viewport, camera, or
/// shadow map. Hosts pass those in per call, so a single instance can drive
/// multiple windows without cross-talk.
/// </summary>
public sealed class SceneViewportRenderer
{
    public void Render(
        Viewport3DX viewport,
        PerspectiveCamera camera,
        ShadowMap3D shadowMap,
        CanonicalScene scene,
        SceneRenderConfig config,
        bool resetCamera = true)
    {
        ArgumentNullException.ThrowIfNull(viewport);
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(shadowMap);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(config);

        viewport.Items.Clear();
        var renderMode = config.RenderMode;
        scene = ApplySelectedVariantToScene(scene, config.Variant);

        switch (renderMode)
        {
            case SceneRenderMode.Wireframe:
                viewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.White });
                break;
            case SceneRenderMode.RawUv:
            case SceneRenderMode.MaterialUv:
                viewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.White });
                break;
            case SceneRenderMode.FlatTexture:
                viewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.Colors.Black });
                break;
            default:
                // Build 0310: scaled total light contribution from ~2.5× down to ~1.0× so
                // bright pre-rendered textures (e.g. EA's full-body diffuse) render at their
                // intended tone instead of clamping to white. PBR Lambertian response for a
                // forward-facing pixel is ~albedo × (Σ directional / π + ambient); previously
                // that totaled ~1.6× for any albedo, washing out peachy skin. New ambient
                // ~0.30 + 3 dirs summing to ~0.55/π ≈ 0.18 + ~0.30 ≈ ~0.78× — leaves headroom
                // for the PBR specular lobe so highlights aren't blown either.
                viewport.Items.Add(new AmbientLight3D { Color = Microsoft.UI.ColorHelper.FromArgb(255, 78, 84, 92) });
                viewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(-0.36f, -0.92f, -0.22f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 165, 160, 154)
                });
                viewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(0.78f, -0.28f, 0.42f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 130, 135, 140)
                });
                viewport.Items.Add(new DirectionalLight3D
                {
                    Direction = Vector3.Normalize(new Vector3(0.12f, 0.58f, -0.9f)),
                    Color = Microsoft.UI.ColorHelper.FromArgb(255, 88, 92, 100)
                });
                viewport.Items.Add(shadowMap);
                break;
        }

        var uvChannelOverride = ResolveUvChannelOverride(config.UvChannel);
        var selectedSlot = ResolveSelectedSlot(config.TextureSlot);
        for (var meshIndex = 0; meshIndex < scene.Meshes.Count; meshIndex++)
        {
            var mesh = scene.Meshes[meshIndex];
            var canonicalMaterial = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.Materials.Count
                ? scene.Materials[mesh.MaterialIndex]
                : null;
            var multiPassPlan = TryBuildMultiPassPlan(canonicalMaterial, renderMode);
            var deferOverlayToSeparatePass = multiPassPlan is not null;
            var material = CreateMaterial(scene, mesh.MaterialIndex, renderMode, selectedSlot, deferOverlayToSeparatePass);
            var geometry = CreateGeometry(mesh, scene, mesh.MaterialIndex, renderMode, selectedSlot, uvChannelOverride);
            if (geometry.Positions is null || geometry.Positions.Count == 0 || geometry.TriangleIndices is null || geometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            viewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = geometry,
                Material = material,
                IsTransparent = IsTransparentMaterial(scene, mesh.MaterialIndex, selectedSlot),
                CullMode = SharpDX.Direct3D11.CullMode.None,
                RenderWireframe = renderMode == SceneRenderMode.Wireframe,
                WireframeColor = Microsoft.UI.Colors.Yellow
            });

            if (multiPassPlan is not null)
            {
                AddOverlayPasses(viewport, mesh, multiPassPlan, renderMode, uvChannelOverride);
            }
        }

        if (resetCamera)
        {
            ResetSceneCamera(camera, scene);
        }
    }

    public void ResetSceneCamera(PerspectiveCamera camera, CanonicalScene? scene)
    {
        ArgumentNullException.ThrowIfNull(camera);
        if (scene is null)
        {
            return;
        }

        var center = new Vector3(
            (scene.Bounds.MinX + scene.Bounds.MaxX) * 0.5f,
            (scene.Bounds.MinY + scene.Bounds.MaxY) * 0.5f,
            (scene.Bounds.MinZ + scene.Bounds.MaxZ) * 0.5f);
        var size = Math.Max(
            Math.Max(scene.Bounds.MaxX - scene.Bounds.MinX, scene.Bounds.MaxY - scene.Bounds.MinY),
            scene.Bounds.MaxZ - scene.Bounds.MinZ);
        if (size <= 0)
        {
            size = 1f;
        }

        camera.Position = center + new Vector3(size * 1.5f, size * 0.75f, size * 1.5f);
        camera.LookDirection = center - camera.Position;
        camera.UpDirection = Vector3.UnitY;
    }

    private void AddOverlayPasses(Viewport3DX viewport, CanonicalMesh mesh, MaterialPlan plan, SceneRenderMode renderMode, int? uvChannelOverride)
    {
        for (var pass = 1; pass < plan.PassCount; pass++)
        {
            var primary = plan.LayersInPass(pass)
                .FirstOrDefault(layer =>
                    layer.Texture is not null &&
                    layer.Role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal);
            if (primary is null)
            {
                continue;
            }

            var passMaterial = BuildOverlayPassMaterial(primary);
            var passGeometry = BuildOverlayPassGeometry(mesh, primary.Texture!, renderMode, uvChannelOverride);
            if (passGeometry.Positions is null || passGeometry.Positions.Count == 0 ||
                passGeometry.TriangleIndices is null || passGeometry.TriangleIndices.Count == 0)
            {
                continue;
            }

            viewport.Items.Add(new MeshGeometryModel3D
            {
                Geometry = passGeometry,
                Material = passMaterial,
                IsTransparent = true,
                CullMode = SharpDX.Direct3D11.CullMode.None
            });
        }
    }

    internal static int? ResolveUvChannelOverride(SceneUvChannelOverride uvChannel) => uvChannel switch
    {
        SceneUvChannelOverride.Uv0 => 0,
        SceneUvChannelOverride.Uv1 => 1,
        _ => null
    };

    internal static string? ResolveSelectedSlot(string? slot) =>
        string.IsNullOrWhiteSpace(slot) || string.Equals(slot, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : slot;

    internal static MaterialPlan? TryBuildMultiPassPlan(CanonicalMaterial? material, SceneRenderMode renderMode)
    {
        if (material is null)
        {
            return null;
        }

        if (renderMode is not SceneRenderMode.LitTexture and not SceneRenderMode.FlatTexture)
        {
            return null;
        }

        var renderable = RenderableMaterialFactory.FromCanonical(material);
        var family = renderable.NormalizedFamily;
        var familyIsMultiPassCapable =
            family.StartsWith("colorMap", StringComparison.OrdinalIgnoreCase) ||
            family.Equals("DecalMap", StringComparison.OrdinalIgnoreCase);
        if (!familyIsMultiPassCapable)
        {
            return null;
        }

        var plan = MaterialApplierRegistry.Apply(renderable, material.Textures);
        if (plan.PassCount <= 1)
        {
            return null;
        }

        var hasOverlayLayer = plan.Layers.Any(layer =>
            layer.Pass > 0 &&
            layer.Texture is not null &&
            layer.Role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal);
        return hasOverlayLayer ? plan : null;
    }

    internal static PhongMaterial BuildOverlayPassMaterial(AppliedLayer layer)
    {
        var textureModel = layer.Texture is null
            ? null
            : new TextureModel(new MemoryStream(layer.Texture.PngBytes), autoCloseStream: true);
        var uvTransform = BuildUvTransform(layer.Texture);
        return new PhongMaterial
        {
            DiffuseColor = new Color4(1f, 1f, 1f, 1f),
            AmbientColor = new Color4(0f, 0f, 0f, 1f),
            EmissiveColor = new Color4(0f, 0f, 0f, 1f),
            SpecularColor = new Color4(0f, 0f, 0f, 1f),
            SpecularShininess = 0f,
            RenderDiffuseMap = textureModel is not null,
            DiffuseMap = textureModel,
            RenderDiffuseAlphaMap = false,
            UVTransform = uvTransform
        };
    }

    internal static MeshGeometry3D BuildOverlayPassGeometry(CanonicalMesh mesh, CanonicalTexture layerTexture, SceneRenderMode renderMode, int? uvChannelOverride = null)
    {
        var geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(),
            TriangleIndices = new IntCollection()
        };

        for (var index = 0; index + 2 < mesh.Positions.Count; index += 3)
        {
            geometry.Positions.Add(new Vector3(mesh.Positions[index], mesh.Positions[index + 1], mesh.Positions[index + 2]));
        }

        var vertexCount = geometry.Positions.Count;
        if (vertexCount == 0)
        {
            return geometry;
        }

        if (mesh.Normals.Count == vertexCount * 3)
        {
            geometry.Normals = new Vector3Collection();
            for (var index = 0; index + 2 < mesh.Normals.Count; index += 3)
            {
                geometry.Normals.Add(new Vector3(mesh.Normals[index], mesh.Normals[index + 1], mesh.Normals[index + 2]));
            }
        }

        var textureCoordinates = SelectTextureCoordinates(mesh, layerTexture, renderMode, uvChannelOverride);
        if (textureCoordinates.Count == vertexCount * 2)
        {
            geometry.TextureCoordinates = new Vector2Collection();
            for (var index = 0; index + 1 < textureCoordinates.Count; index += 2)
            {
                geometry.TextureCoordinates.Add(new Vector2(textureCoordinates[index], textureCoordinates[index + 1]));
            }
        }

        for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
        {
            var a = mesh.Indices[index];
            var b = mesh.Indices[index + 1];
            var c = mesh.Indices[index + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
            {
                continue;
            }

            geometry.TriangleIndices.Add(a);
            geometry.TriangleIndices.Add(b);
            geometry.TriangleIndices.Add(c);
        }

        if (geometry.Normals is null || geometry.Normals.Count != vertexCount)
        {
            geometry.Normals = BuildPreviewNormals(geometry.Positions, geometry.TriangleIndices);
        }

        return geometry;
    }

    internal static CanonicalScene ApplySelectedVariantToScene(CanonicalScene scene, SceneVariantOption? selected)
    {
        if (selected is null || scene.Materials.Count == 0)
        {
            return scene;
        }

        var rewritten = new List<CanonicalMaterial>(scene.Materials.Count);
        var changed = false;
        foreach (var material in scene.Materials)
        {
            if (material.Variants is { Count: > 0 })
            {
                var match = material.Variants.FirstOrDefault(variant => variant.StateNameHash == selected.StateNameHash);
                if (match is not null && !match.IsDefault)
                {
                    rewritten.Add(material with { Textures = match.Textures });
                    changed = true;
                    continue;
                }
            }
            rewritten.Add(material);
        }

        return changed ? scene with { Materials = rewritten } : scene;
    }

    internal static MeshGeometry3D CreateGeometry(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, int? uvChannelOverride = null)
    {
        var geometry = new MeshGeometry3D
        {
            Positions = new Vector3Collection(),
            TriangleIndices = new IntCollection()
        };

        for (var index = 0; index + 2 < mesh.Positions.Count; index += 3)
        {
            geometry.Positions.Add(new Vector3(mesh.Positions[index], mesh.Positions[index + 1], mesh.Positions[index + 2]));
        }

        var vertexCount = geometry.Positions.Count;
        if (vertexCount == 0)
        {
            return geometry;
        }

        if (mesh.Normals.Count == vertexCount * 3)
        {
            geometry.Normals = new Vector3Collection();
            for (var index = 0; index + 2 < mesh.Normals.Count; index += 3)
            {
                geometry.Normals.Add(new Vector3(mesh.Normals[index], mesh.Normals[index + 1], mesh.Normals[index + 2]));
            }
        }

        var textureCoordinates = SelectTextureCoordinates(mesh, scene, materialIndex, renderMode, selectedSlot, uvChannelOverride);
        if (textureCoordinates.Count == vertexCount * 2)
        {
            geometry.TextureCoordinates = new Vector2Collection();
            for (var index = 0; index + 1 < textureCoordinates.Count; index += 2)
            {
                geometry.TextureCoordinates.Add(new Vector2(textureCoordinates[index], textureCoordinates[index + 1]));
            }
        }

        for (var index = 0; index + 2 < mesh.Indices.Count; index += 3)
        {
            var a = mesh.Indices[index];
            var b = mesh.Indices[index + 1];
            var c = mesh.Indices[index + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
            {
                continue;
            }

            geometry.TriangleIndices.Add(a);
            geometry.TriangleIndices.Add(b);
            geometry.TriangleIndices.Add(c);
        }

        if (geometry.Normals is null || geometry.Normals.Count != vertexCount)
        {
            geometry.Normals = BuildPreviewNormals(geometry.Positions, geometry.TriangleIndices);
        }

        return geometry;
    }

    internal static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, int? uvChannelOverride = null)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryTexture = SelectPrimaryViewportTexture(material, renderMode, selectedSlot);
        return SelectTextureCoordinates(mesh, primaryTexture, renderMode, uvChannelOverride);
    }

    internal static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalTexture? primaryTexture, SceneRenderMode renderMode, int? uvChannelOverride = null)
    {
        var hasExplicitTextureUvDirective = HasExplicitTextureUvDirective(primaryTexture);
        var hasConfirmedTextureUvDirective = hasExplicitTextureUvDirective &&
                                            primaryTexture is not null &&
                                            !primaryTexture.IsApproximateUvTransform;
        var requestedChannel = uvChannelOverride is { } forcedChannel
            ? forcedChannel
            : hasConfirmedTextureUvDirective
                ? primaryTexture!.UvChannel
                : mesh.PreferredUvChannel;
        IReadOnlyList<float> coordinates;

        if (requestedChannel == 1 && mesh.Uv1s is { Count: > 0 })
        {
            coordinates = mesh.Uv1s;
        }
        else if (mesh.Uv0s is { Count: > 0 })
        {
            coordinates = mesh.Uv0s;
        }
        else
        {
            coordinates = mesh.Uvs;
        }

        if (requestedChannel == 1 && hasConfirmedTextureUvDirective)
        {
            coordinates = NormalizeUvCoordinatesIfLocalSubspace(coordinates);
        }

        if (renderMode != SceneRenderMode.MaterialUv ||
            primaryTexture is null ||
            primaryTexture.IsApproximateUvTransform ||
            (Math.Abs(primaryTexture.UvScaleU - 1f) < 0.0001f &&
             Math.Abs(primaryTexture.UvScaleV - 1f) < 0.0001f &&
             Math.Abs(primaryTexture.UvOffsetU) < 0.0001f &&
             Math.Abs(primaryTexture.UvOffsetV) < 0.0001f))
        {
            return coordinates;
        }

        var transformed = new float[coordinates.Count];
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            transformed[index] = (coordinates[index] * primaryTexture.UvScaleU) + primaryTexture.UvOffsetU;
            transformed[index + 1] = (coordinates[index + 1] * primaryTexture.UvScaleV) + primaryTexture.UvOffsetV;
        }

        return transformed;
    }

    internal static CanonicalTexture? SelectPrimaryViewportTexture(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        return BuildViewportTextureSelection(material, renderMode, selectedSlot).ViewportColorTexture;
    }

    internal static IReadOnlyList<float> NormalizeUvCoordinatesIfLocalSubspace(IReadOnlyList<float> coordinates)
    {
        if (coordinates.Count < 4)
        {
            return coordinates;
        }

        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            minU = Math.Min(minU, coordinates[index]);
            maxU = Math.Max(maxU, coordinates[index]);
            minV = Math.Min(minV, coordinates[index + 1]);
            maxV = Math.Max(maxV, coordinates[index + 1]);
        }

        if (!float.IsFinite(minU) || !float.IsFinite(maxU) || !float.IsFinite(minV) || !float.IsFinite(maxV))
        {
            return coordinates;
        }

        var rangeU = maxU - minU;
        var rangeV = maxV - minV;
        if (rangeU <= 0.0001f || rangeV <= 0.0001f)
        {
            return coordinates;
        }

        // Some TS4 UV1 atlas layouts are stored in a local subspace rather than spanning the
        // full 0..1 range. Normalize them before applying a material-driven atlas transform.
        var normalized = new float[coordinates.Count];
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            normalized[index] = (coordinates[index] - minU) / rangeU;
            normalized[index + 1] = (coordinates[index + 1] - minV) / rangeV;
        }

        return normalized;
    }

    internal static Vector3Collection BuildPreviewNormals(IList<Vector3> positions, IList<int> triangleIndices)
    {
        var normals = Enumerable.Repeat(Vector3.Zero, positions.Count).ToArray();

        for (var index = 0; index + 2 < triangleIndices.Count; index += 3)
        {
            var ia = triangleIndices[index];
            var ib = triangleIndices[index + 1];
            var ic = triangleIndices[index + 2];
            if (ia < 0 || ib < 0 || ic < 0 || ia >= positions.Count || ib >= positions.Count || ic >= positions.Count)
            {
                continue;
            }

            var a = positions[ia];
            var b = positions[ib];
            var c = positions[ic];
            var ab = b - a;
            var ac = c - a;
            var face = Vector3.Cross(ab, ac);
            if (face.LengthSquared() <= 1e-12f)
            {
                continue;
            }

            normals[ia] += face;
            normals[ib] += face;
            normals[ic] += face;
        }

        var result = new Vector3Collection();
        foreach (var normal in normals)
        {
            result.Add(normal.LengthSquared() <= 1e-12f ? Vector3.UnitY : Vector3.Normalize(normal));
        }

        return result;
    }

    internal static Material CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot) =>
        CreateMaterial(scene, materialIndex, renderMode, selectedSlot, deferOverlayToSeparatePass: false);

    internal static Material CreateMaterial(CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot, bool deferOverlayToSeparatePass)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var textureSelection = BuildViewportTextureSelection(material, renderMode, selectedSlot);
        if (deferOverlayToSeparatePass && textureSelection.LayeredColorTexture is not null)
        {
            // Overlay layer is going to be drawn as a separate sibling pass, so suppress its
            // appearances on the base pass: drop the fake-emissive promotion and prefer the
            // genuine base color texture as the viewport diffuse.
            var suppressedEmissive = textureSelection.EmissiveTexture is not null &&
                                     ReferenceEquals(textureSelection.EmissiveTexture, textureSelection.LayeredColorTexture)
                ? null
                : textureSelection.EmissiveTexture;
            var rerouted = ReferenceEquals(textureSelection.ViewportColorTexture, textureSelection.LayeredColorTexture) &&
                           textureSelection.BaseColorTexture is not null
                ? textureSelection.BaseColorTexture
                : textureSelection.ViewportColorTexture;
            textureSelection = textureSelection with
            {
                EmissiveTexture = suppressedEmissive,
                ViewportColorTexture = rerouted,
                LayeredColorTexture = null
            };
        }
        var viewportColorTexture = textureSelection.ViewportColorTexture;
        var uvTransform = BuildUvTransform(textureSelection.ViewportColorTexture);
        var viewportTintColor = BuildViewportTintColor(material);
        var approximateBaseColor = BuildApproximateBaseColor(material);
        var effectiveBaseColor = viewportTintColor ?? approximateBaseColor;
        var diffuseColor = effectiveBaseColor ?? new Color4(0.62f, 0.62f, 0.62f, 1f);
        var ambientColor = effectiveBaseColor is null
            ? new Color4(0.3f, 0.3f, 0.3f, 1f)
            : new Color4(
                Math.Clamp(effectiveBaseColor.Value.Red * 0.3f, 0f, 1f),
                Math.Clamp(effectiveBaseColor.Value.Green * 0.3f, 0f, 1f),
                Math.Clamp(effectiveBaseColor.Value.Blue * 0.3f, 0f, 1f),
                1f);

        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var renderDiffuseMap =
            !isWireframe &&
            !isFlat &&
            viewportColorTexture is not null;
        var renderEmissiveMap = (isFlat && viewportColorTexture is not null) || (!isFlat && textureSelection.EmissiveTexture is not null);
        var renderAlphaMap = textureSelection.RenderAlphaMap;
        var forceOpaqueViewportTexture = isLit && !renderAlphaMap;
        var (textureModel, usesSwatchComposite) = CreateViewportTextureModel(
            material,
            viewportColorTexture,
            textureSelection.ColorShiftMaskTexture,
            viewportTintColor,
            forceOpaqueViewportTexture);
        var emissiveTextureModel = textureSelection.EmissiveTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.EmissiveTexture.PngBytes), autoCloseStream: true);
        var alphaTextureModel = textureSelection.OpacityTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.OpacityTexture.PngBytes), autoCloseStream: true);
        var normalTextureModel = textureSelection.NormalTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.NormalTexture.PngBytes), autoCloseStream: true);
        var specularTextureModel = textureSelection.SpecularTexture is null
            ? null
            : new TextureModel(new MemoryStream(textureSelection.SpecularTexture.PngBytes), autoCloseStream: true);
        var usesOverlayAsSecondaryLayer =
            textureSelection.EmissiveTexture is not null &&
            textureSelection.LayeredColorTexture is not null &&
            ReferenceEquals(textureSelection.EmissiveTexture, textureSelection.LayeredColorTexture);
        var hasLitNormalDetail = normalTextureModel is not null;
        var hasLitSpecularDetail = specularTextureModel is not null;
        var useMatteLitShading = isLit && !hasLitNormalDetail && !hasLitSpecularDetail;
        var litAmbientColor = useMatteLitShading
            ? new Color4(0.46f, 0.46f, 0.46f, 1f)
            : new Color4(0.34f, 0.34f, 0.34f, 1f);
        var litSpecularColor = useMatteLitShading
            ? new Color4(0.02f, 0.02f, 0.02f, 1f)
            : new Color4(0.08f, 0.08f, 0.08f, 1f);
        var litSpecularShininess = useMatteLitShading ? 4f : 12f;
        // The viewport tint is metadata for skintone/swatch routing, not a blanket multiplier for
        // already-decoded textured materials. If a texture map is present, keep the lit diffuse
        // multiplier neutral unless a dedicated texture composite has already baked the tint in.
        // EXCEPTION: skin-target materials carry a neutral skin diffuse intended to be tinted by
        // the routed skintone (ViewportTintColor). Three cases qualify for PBR skin rendering:
        //  - True SimSkin / SimSkinMask shader-family materials, recognised by shader name.
        //  - ApproximateCas materials with a non-null ViewportTintColor (set by
        //    `SimSceneComposer.ApplySkintoneRouteToMaterial`). These come out of the Sim assembly
        //    path with `shader=(unknown)`, so the shader-name detector misses them.
        //  - ApproximateCas materials that have been atlas-rewritten by RewriteSkintoneRoutedMaterial.
        //    After atlas injection ViewportTintColor is cleared to prevent swatch masking, but the
        //    material still needs PBR rendering (AlbedoColor = white; atlas carries all skin colour).
        var isSimSkinFamily = material is not null &&
                              RenderableMaterialFactory.IsSimSkinFamily(material.ShaderName);
        var isApproximateCasSkintoneTarget = material is not null &&
                                              material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
                                              (material.ViewportTintColor is not null ||
                                               material.Textures.Any(static t =>
                                                   t.Semantic == CanonicalTextureSemantic.BaseColor &&
                                                   string.Equals(t.FileName, "skin_atlas.png", StringComparison.OrdinalIgnoreCase)));
        var needsViewportTintMultiply = isSimSkinFamily || isApproximateCasSkintoneTarget;
        var effectiveTexturedLitDiffuseColor =
            needsViewportTintMultiply &&
            !usesSwatchComposite &&
            viewportTintColor is { } skintone
                ? skintone
                : new Color4(1f, 1f, 1f, 1f);

        // SimSkin and skin-targeted ApproximateCas materials use HelixToolkit's built-in PBR
        // (GGX Cook-Torrance BRDF) in lit mode. PBR has physically-based specular response and
        // handles multi-directional lighting more accurately than Phong for skin atlases that were
        // designed for a PBR-lit engine. The pre-baked eye-socket / nose / lip contouring in the
        // atlas is also more naturally integrated by GGX than by Phong's sharp cos-power specular.
        if (isLit && renderDiffuseMap && (isSimSkinFamily || isApproximateCasSkintoneTarget))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,      // skin is fully dielectric
                RoughnessFactor = 0.85f,    // skin is diffuse-dominant, low specular lobe
                ReflectanceFactor = 0.04f,  // ~4% F0 is the standard value for human skin
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = true,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // SimGlass: GEOM-side glass family (eyes, contact lenses, visors).
        // Very low roughness + slightly elevated Fresnel models the glass IOR (~1.5 → F0 ≈ 4%).
        // Shadow casting is suppressed — transparent glass shouldn't project hard opaque shadows.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsSimGlassFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,
                RoughnessFactor = 0.05f,    // near-perfect specular glass surface
                ReflectanceFactor = 0.08f,  // IOR ~1.5 → F0 ≈ 4–8%
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = false,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // StandardSurface: OpenPBR-aligned family used by newer TS4 packs.
        // Generic dielectric PBR parameters; metallic/roughness varies by asset but these
        // defaults (slightly rough, non-metallic) cover most manufactured surface types.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsStandardSurfaceFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.05f,     // mostly dielectric; slight factor for variety
                RoughnessFactor = 0.60f,    // medium roughness covers most manufactured surfaces
                ReflectanceFactor = 0.04f,  // standard dielectric F0
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = true,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        // RefractionMap / SpecularEnvMap: refraction and env-mapped surfaces (pool water,
        // decorative glass, mirrors). Very smooth, slightly elevated Fresnel, no opaque shadows.
        if (isLit && renderDiffuseMap && RenderableMaterialFactory.IsRefractionFamily(material?.ShaderName))
        {
            return new PBRMaterial
            {
                AlbedoColor = effectiveTexturedLitDiffuseColor,
                EmissiveColor = new Color4(0f, 0f, 0f, 1f),
                MetallicFactor = 0.0f,
                RoughnessFactor = 0.08f,    // mostly smooth refractive or reflective surface
                ReflectanceFactor = 0.08f,  // elevated Fresnel for glass/water interfaces
                AmbientOcclusionFactor = 1.0f,
                AlbedoMap = textureModel,
                NormalMap = normalTextureModel,
                UVTransform = uvTransform,
                RenderShadowMap = false,
                EnableAutoTangent = normalTextureModel is not null,
            };
        }

        return new PhongMaterial
        {
            DiffuseColor = isWireframe
                ? new Color4(0.95f, 0.95f, 0.95f, 1f)
                : isLit
                    ? (renderDiffuseMap ? effectiveTexturedLitDiffuseColor : diffuseColor)
                    : viewportColorTexture is null
                        ? diffuseColor
                        : new Color4(0f, 0f, 0f, 1f),
            AmbientColor = isFlat
                ? viewportColorTexture is null
                    ? diffuseColor
                    : new Color4(0f, 0f, 0f, 1f)
                : isLit
                    ? (renderDiffuseMap ? litAmbientColor : ambientColor)
                    : ambientColor,
            EmissiveColor = isFlat
                ? new Color4(1f, 1f, 1f, 1f)
                : usesOverlayAsSecondaryLayer
                    ? new Color4(0.45f, 0.45f, 0.45f, 1f)
                    : new Color4(0f, 0f, 0f, 1f),
            SpecularColor = isFlat ? new Color4(0f, 0f, 0f, 1f) : litSpecularColor,
            SpecularShininess = isFlat ? 0f : litSpecularShininess,
            RenderDiffuseMap = renderDiffuseMap,
            DiffuseMap = renderDiffuseMap ? textureModel : null,
            RenderEmissiveMap = renderEmissiveMap,
            EmissiveMap = isFlat
                ? (renderEmissiveMap ? textureModel : null)
                : emissiveTextureModel,
            RenderDiffuseAlphaMap = renderAlphaMap,
            DiffuseAlphaMap = renderAlphaMap ? alphaTextureModel : null,
            RenderNormalMap = isLit && normalTextureModel is not null,
            NormalMap = isLit ? normalTextureModel : null,
            RenderSpecularColorMap = isLit && specularTextureModel is not null,
            SpecularColorMap = isLit ? specularTextureModel : null,
            EnableAutoTangent = isLit && normalTextureModel is not null,
            RenderShadowMap = isLit,
            UVTransform = uvTransform
        };
    }

    internal static (TextureModel? TextureModel, bool UsesSwatchComposite) CreateViewportTextureModel(
        CanonicalMaterial? material,
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? colorShiftMaskTexture,
        Color4? viewportTintColor,
        bool forceOpaqueAlpha)
    {
        if (material?.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
            viewportColorTexture is not null &&
            colorShiftMaskTexture is not null &&
            viewportTintColor is not null)
        {
            var compositedPngBytes = ComposeSwatchMaskedPng(
                viewportColorTexture.PngBytes,
                colorShiftMaskTexture.PngBytes,
                viewportTintColor.Value);
            if (compositedPngBytes is not null)
            {
                if (forceOpaqueAlpha)
                {
                    compositedPngBytes = ForceOpaquePngAlpha(compositedPngBytes);
                }

                return (new TextureModel(new MemoryStream(compositedPngBytes), autoCloseStream: true), true);
            }
        }

        return (CreateTextureModel(viewportColorTexture, forceOpaqueAlpha), false);
    }

    internal static Color4? BuildApproximateBaseColor(CanonicalMaterial? material)
    {
        if (material?.ApproximateBaseColor is not { } color)
        {
            return null;
        }

        return new Color4(
            Math.Clamp(color.R, 0f, 1f),
            Math.Clamp(color.G, 0f, 1f),
            Math.Clamp(color.B, 0f, 1f),
            Math.Clamp(color.A, 0f, 1f));
    }

    internal static Color4? BuildViewportTintColor(CanonicalMaterial? material)
    {
        if (material?.ViewportTintColor is not { } color)
        {
            return null;
        }

        return new Color4(
            Math.Clamp(color.R, 0f, 1f),
            Math.Clamp(color.G, 0f, 1f),
            Math.Clamp(color.B, 0f, 1f),
            Math.Clamp(color.A, 0f, 1f));
    }

    internal static TextureModel? CreateTextureModel(CanonicalTexture? texture, bool forceOpaqueAlpha = false)
    {
        if (texture is null)
        {
            return null;
        }

        var pngBytes = forceOpaqueAlpha ? ForceOpaquePngAlpha(texture.PngBytes) : texture.PngBytes;
        return new TextureModel(new MemoryStream(pngBytes), autoCloseStream: true);
    }

    internal static byte[] ForceOpaquePngAlpha(byte[] pngBytes)
    {
        using var input = new InMemoryRandomAccessStream();
        input.WriteAsync(pngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
        input.Seek(0);

        var decoder = BitmapDecoder.CreateAsync(input).AsTask().GetAwaiter().GetResult();
        var pixelData = decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult();
        var pixels = pixelData.DetachPixelData();
        for (var offset = 3; offset < pixels.Length; offset += 4)
        {
            pixels[offset] = 0xFF;
        }

        using var output = new InMemoryRandomAccessStream();
        var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            decoder.PixelWidth,
            decoder.PixelHeight,
            decoder.DpiX,
            decoder.DpiY,
            pixels);
        encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
        output.Seek(0);
        using var stream = output.AsStreamForRead();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    internal static byte[]? ComposeSwatchMaskedPng(byte[] diffusePngBytes, byte[] maskPngBytes, Color4 tintColor)
    {
        try
        {
            using var diffuseStream = new InMemoryRandomAccessStream();
            diffuseStream.WriteAsync(diffusePngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            diffuseStream.Seek(0);

            using var maskStream = new InMemoryRandomAccessStream();
            maskStream.WriteAsync(maskPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            maskStream.Seek(0);

            var diffuseDecoder = BitmapDecoder.CreateAsync(diffuseStream).AsTask().GetAwaiter().GetResult();
            var maskDecoder = BitmapDecoder.CreateAsync(maskStream).AsTask().GetAwaiter().GetResult();
            var diffusePixels = diffuseDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            var maskPixels = maskDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = diffuseDecoder.PixelWidth,
                    ScaledHeight = diffuseDecoder.PixelHeight
                },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            if (diffusePixels.Length != maskPixels.Length)
            {
                return null;
            }

            var tintR = Math.Clamp(tintColor.Red, 0f, 1f);
            var tintG = Math.Clamp(tintColor.Green, 0f, 1f);
            var tintB = Math.Clamp(tintColor.Blue, 0f, 1f);
            var tintA = Math.Clamp(tintColor.Alpha, 0f, 1f);
            var composedPixels = new byte[diffusePixels.Length];
            for (var offset = 0; offset < diffusePixels.Length; offset += 4)
            {
                var baseB = diffusePixels[offset] / 255f;
                var baseG = diffusePixels[offset + 1] / 255f;
                var baseR = diffusePixels[offset + 2] / 255f;
                var baseA = diffusePixels[offset + 3];

                var maskB = maskPixels[offset] / 255f;
                var maskG = maskPixels[offset + 1] / 255f;
                var maskR = maskPixels[offset + 2] / 255f;
                var maskA = maskPixels[offset + 3] / 255f;
                var maskFactor = Math.Clamp(Math.Max(maskA, Math.Max(maskR, Math.Max(maskG, maskB))) * tintA, 0f, 1f);

                var blendedR = baseR * ((1f - maskFactor) + (maskFactor * tintR));
                var blendedG = baseG * ((1f - maskFactor) + (maskFactor * tintG));
                var blendedB = baseB * ((1f - maskFactor) + (maskFactor * tintB));

                composedPixels[offset] = (byte)Math.Clamp((int)Math.Round(blendedB * 255f), 0, 255);
                composedPixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(blendedG * 255f), 0, 255);
                composedPixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(blendedR * 255f), 0, 255);
                composedPixels[offset + 3] = baseA;
            }

            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                diffuseDecoder.PixelWidth,
                diffuseDecoder.PixelHeight,
                diffuseDecoder.DpiX,
                diffuseDecoder.DpiY,
                composedPixels);
            encoder.FlushAsync().AsTask().GetAwaiter().GetResult();
            output.Seek(0);
            using var stream = output.AsStreamForRead();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
        catch
        {
            return null;
        }
    }

    internal static UVTransform BuildUvTransform(CanonicalTexture? texture)
    {
        if (texture is null || !HasExplicitTextureUvDirective(texture))
        {
            return new UVTransform(0f);
        }

        return new UVTransform(
            rotation: 0f,
            scalingX: texture.UvScaleU,
            scalingY: texture.UvScaleV,
            translationX: texture.UvOffsetU,
            translationY: texture.UvOffsetV);
    }

    internal static bool HasExplicitTextureUvDirective(CanonicalTexture? texture)
    {
        if (texture is null)
        {
            return false;
        }

        return texture.UvChannel != 0 ||
               Math.Abs(texture.UvScaleU - 1f) >= 0.0001f ||
               Math.Abs(texture.UvScaleV - 1f) >= 0.0001f ||
               Math.Abs(texture.UvOffsetU) >= 0.0001f ||
               Math.Abs(texture.UvOffsetV) >= 0.0001f;
    }

    internal static IReadOnlyList<CanonicalTexture> SelectViewportTextureGroup(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return [];
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return material.Textures
                .Where(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
                .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return material.Textures
            .GroupBy(BuildTextureSamplingKey, StringComparer.Ordinal)
            .OrderByDescending(group => ScoreTextureGroup(group, renderMode))
            .ThenByDescending(group => group.Count())
            .Select(group => group
                .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
                .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
                .ToArray())
            .FirstOrDefault() ?? [];
    }

    internal readonly record struct ViewportTextureSelection(
        IReadOnlyList<CanonicalTexture> TextureGroup,
        CanonicalTexture? BaseColorTexture,
        CanonicalTexture? LayeredColorTexture,
        CanonicalTexture? EmissiveTexture,
        CanonicalTexture? OpacityTexture,
        CanonicalTexture? NormalTexture,
        CanonicalTexture? SpecularTexture,
        CanonicalTexture? ColorShiftMaskTexture,
        CanonicalTexture? ViewportColorTexture,
        bool RenderAlphaMap);

    internal static ViewportTextureSelection BuildViewportTextureSelection(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var textureGroup = SelectViewportTextureGroup(material, renderMode, selectedSlot);
        var baseColorTexture = SelectBaseColorTexture(textureGroup);
        var layeredColorTexture = SelectLayeredColorTexture(material, textureGroup);
        var emissiveTexture = SelectEmissiveTexture(textureGroup, baseColorTexture, layeredColorTexture);
        var normalTexture = SelectNormalTexture(textureGroup);
        var specularTexture = SelectSpecularTexture(textureGroup);
        var colorShiftMaskTexture = SelectColorShiftMaskTexture(textureGroup);
        var selectedSlotTexture = !string.IsNullOrWhiteSpace(selectedSlot)
            ? textureGroup.FirstOrDefault(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
            : null;
        var viewportColorTexture = selectedSlotTexture ?? layeredColorTexture ?? baseColorTexture ?? emissiveTexture;
        var opacityTexture = SelectViewportOpacityTexture(material, textureGroup, baseColorTexture, viewportColorTexture, selectedSlot);
        var renderAlphaMap = ShouldRenderTransparentViewport(material, textureGroup, baseColorTexture, viewportColorTexture, selectedSlot);

        return new ViewportTextureSelection(
            textureGroup,
            baseColorTexture,
            layeredColorTexture,
            emissiveTexture,
            opacityTexture,
            normalTexture,
            specularTexture,
            colorShiftMaskTexture,
            viewportColorTexture,
            renderAlphaMap);
    }

    internal static string BuildTextureSamplingKey(CanonicalTexture texture) =>
        FormattableString.Invariant($"{texture.UvChannel}|{texture.UvScaleU:0.####}|{texture.UvScaleV:0.####}|{texture.UvOffsetU:0.####}|{texture.UvOffsetV:0.####}");

    internal static int ScoreTextureGroup(IGrouping<string, CanonicalTexture> group, SceneRenderMode renderMode) =>
        group.Sum(texture => ScorePrimaryViewportTexture(texture, renderMode)) + group.Count();

    internal static int ScorePrimaryViewportTexture(CanonicalTexture texture, SceneRenderMode renderMode)
    {
        var slot = texture.Slot;
        return texture.Semantic switch
        {
            CanonicalTextureSemantic.BaseColor => renderMode == SceneRenderMode.Wireframe ? 0 : 100,
            CanonicalTextureSemantic.Overlay => 80,
            CanonicalTextureSemantic.Emissive => renderMode == SceneRenderMode.FlatTexture ? 75 : 65,
            CanonicalTextureSemantic.Normal => renderMode == SceneRenderMode.LitTexture ? 35 : 5,
            CanonicalTextureSemantic.Specular => renderMode == SceneRenderMode.LitTexture ? 30 : 5,
            CanonicalTextureSemantic.Gloss => renderMode == SceneRenderMode.LitTexture ? 25 : 4,
            CanonicalTextureSemantic.Opacity => 20,
            _ when slot.Contains("detail", StringComparison.OrdinalIgnoreCase) => 78,
            _ when slot.Contains("decal", StringComparison.OrdinalIgnoreCase) => 76,
            _ when slot.Contains("dirt", StringComparison.OrdinalIgnoreCase) => 72,
            _ when slot.Contains("grime", StringComparison.OrdinalIgnoreCase) => 72,
            _ when slot.Contains("emiss", StringComparison.OrdinalIgnoreCase) => 64,
            _ when slot.Contains("overlay", StringComparison.OrdinalIgnoreCase) => 79,
            _ when slot.Contains("normal", StringComparison.OrdinalIgnoreCase) => renderMode == SceneRenderMode.LitTexture ? 34 : 4,
            _ when slot.Contains("spec", StringComparison.OrdinalIgnoreCase) => renderMode == SceneRenderMode.LitTexture ? 29 : 4,
            _ => 1
        };
    }

    internal static bool IsTransparentMaterial(CanonicalScene scene, int materialIndex, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        return BuildViewportTextureSelection(material, SceneRenderMode.LitTexture, selectedSlot).RenderAlphaMap;
    }

    // ----- Texture selection helpers ----------------------------------------------------

    private static bool TextureSupportsAlpha(CanonicalTexture? texture)
    {
        var bytes = texture?.PngBytes;
        if (bytes is null || bytes.Length < 33)
        {
            return false;
        }

        ReadOnlySpan<byte> pngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!bytes.AsSpan(0, pngSignature.Length).SequenceEqual(pngSignature))
        {
            return false;
        }

        // PNG IHDR chunk stores the color type at byte 25 of the file.
        // 4 = grayscale+alpha, 6 = RGBA.
        var colorType = bytes[25];
        if (colorType is 4 or 6)
        {
            return true;
        }

        // Palette PNGs may carry transparency through a tRNS chunk.
        for (var offset = 8; offset + 8 <= bytes.Length;)
        {
            var chunkLength = (bytes[offset] << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3];
            if (chunkLength < 0 || offset + 12 + chunkLength > bytes.Length)
            {
                break;
            }

            if (bytes[offset + 4] == (byte)'t' &&
                bytes[offset + 5] == (byte)'R' &&
                bytes[offset + 6] == (byte)'N' &&
                bytes[offset + 7] == (byte)'S')
            {
                return true;
            }

            offset += 12 + chunkLength;
        }

        return false;
    }

    private static CanonicalTexture? SelectBaseColorTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        static bool IsPreferredDiffuseSlot(string? slot) =>
            slot is not null &&
            (slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("basecolor", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("albedo", StringComparison.OrdinalIgnoreCase) ||
             slot.Equals("texture_0", StringComparison.OrdinalIgnoreCase));

        static bool IsNonColorSlot(string? slot) =>
            slot is not null &&
            (slot.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("rough", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("metal", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
             slot.Contains("overlay", StringComparison.OrdinalIgnoreCase));

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.BaseColor)
            ?? material.Textures.FirstOrDefault(texture => IsPreferredDiffuseSlot(texture.Slot))
            ?? material.Textures.FirstOrDefault(texture => !IsNonColorSlot(texture.Slot))
            ?? material.Textures.FirstOrDefault();
    }

    private static CanonicalTexture? SelectBaseColorTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectBaseColorTexture(scope);
    }

    private static CanonicalTexture? SelectOpacityTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        if (!string.IsNullOrWhiteSpace(material.AlphaTextureSlot))
        {
            var explicitAlpha = material.Textures.FirstOrDefault(texture =>
                texture.Slot.Equals(material.AlphaTextureSlot, StringComparison.OrdinalIgnoreCase));
            if (explicitAlpha is not null)
            {
                return explicitAlpha;
            }
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Opacity)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase))
            ?? (ShouldUseBaseColorAlphaAsOpacityFallback(material, baseColorTexture) ? baseColorTexture : null);
    }

    private static CanonicalTexture? SelectViewportOpacityTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        CanonicalTexture? viewportColorTexture,
        string? selectedSlot)
    {
        if (TextureSupportsOwnAlphaAsOpacity(viewportColorTexture, baseColorTexture, selectedSlot))
        {
            return viewportColorTexture;
        }

        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectDefaultViewportOpacityTexture(material, baseColorTexture);
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(textures, baseColorTexture, scopedAlphaSlot);
    }

    private static CanonicalTexture? SelectDefaultViewportOpacityTexture(
        CanonicalMaterial? material,
        CanonicalTexture? baseColorTexture)
    {
        if (material?.IsTransparent == true)
        {
            return SelectOpacityTexture(material, baseColorTexture);
        }

        if (material is null)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        return ShouldUseBaseColorAlphaAsOpacityFallback(material, baseColorTexture)
            ? baseColorTexture
            : null;
    }

    private static bool ShouldRenderTransparentViewport(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        CanonicalTexture? viewportColorTexture,
        string? selectedSlot)
    {
        if (TextureSupportsOwnAlphaAsOpacity(viewportColorTexture, baseColorTexture, selectedSlot))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectDefaultViewportOpacityTexture(material, baseColorTexture) is not null;
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(textures, baseColorTexture, scopedAlphaSlot) is not null;
    }

    private static CanonicalTexture? SelectOpacityTexture(IReadOnlyList<CanonicalTexture> textures, CanonicalTexture? baseColorTexture, string? explicitAlphaSlot)
    {
        if (textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        var scope = new CanonicalMaterial("Scoped", textures, AlphaTextureSlot: explicitAlphaSlot);
        return SelectOpacityTexture(scope, baseColorTexture);
    }

    private static bool TextureSupportsOwnAlphaAsOpacity(
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? baseColorTexture,
        string? selectedSlot)
    {
        if (viewportColorTexture is null || !TextureSupportsAlpha(viewportColorTexture))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return true;
        }

        return !ReferenceEquals(viewportColorTexture, baseColorTexture) &&
               viewportColorTexture.Semantic is CanonicalTextureSemantic.Overlay or CanonicalTextureSemantic.Emissive;
    }

    private static bool ShouldUseBaseColorAlphaAsOpacityFallback(CanonicalMaterial material, CanonicalTexture? baseColorTexture)
    {
        if (!TextureSupportsAlpha(baseColorTexture))
        {
            return false;
        }

        // Portable fallback-diffuse approximations are useful as color, but their alpha channel
        // is not reliable enough to drive lit preview transparency unless the material explicitly
        // named an alpha/opacity slot.
        if (material.SourceKind == CanonicalMaterialSourceKind.FallbackCandidate)
        {
            return false;
        }

        // Skintone-routed CAS materials (recognized via the structured note set by
        // SimSceneComposer.ApplySkintoneRouteToMaterial) use the diffuse texture's alpha channel
        // as a region/skin mask, NOT as portable opacity. Promoting that alpha to viewport
        // opacity makes the body render transparent wherever the mask is zero (visible as a
        // near-black viewport). Both body-shell materials (now using the base skin texture) and
        // head-shell materials (still using the CASPart diffuse) fall under this rule.
        if (material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
            material.Approximation is { } approximation &&
            approximation.Contains("Sim skintone route", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static CanonicalTexture? SelectEmissiveTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture, CanonicalTexture? layeredColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Emissive)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("emiss", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("glow", StringComparison.OrdinalIgnoreCase))
            ?? (material.LayeredTextureSlots?.Any(slot => slot.Equals("emissive", StringComparison.OrdinalIgnoreCase)) == true
                ? baseColorTexture
                : null);
    }

    private static CanonicalTexture? SelectEmissiveTexture(IReadOnlyList<CanonicalTexture> textures, CanonicalTexture? baseColorTexture, CanonicalTexture? layeredColorTexture)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectEmissiveTexture(scope, baseColorTexture, layeredColorTexture);
    }

    private static CanonicalTexture? SelectNormalTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Normal)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("normal", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("bump", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectNormalTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectNormalTexture(scope);
    }

    private static CanonicalTexture? SelectSpecularTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Specular)
            ?? material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Gloss)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("spec", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("gloss", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("rough", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("smooth", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectSpecularTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectSpecularTexture(scope);
    }

    private static CanonicalTexture? SelectColorShiftMaskTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        return textures.FirstOrDefault(static texture => texture.Slot.Equals("color_shift_mask", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectLayeredColorTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        if (material.LayeredTextureSlots is { Count: > 0 })
        {
            foreach (var preferredSlot in material.LayeredTextureSlots)
            {
                var explicitSlotMatch = material.Textures.FirstOrDefault(texture =>
                    texture.Slot.Equals(preferredSlot, StringComparison.OrdinalIgnoreCase));
                if (explicitSlotMatch is not null)
                {
                    return explicitSlotMatch;
                }
            }
        }

        return material.Textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Overlay)
            ?? material.Textures.FirstOrDefault(texture =>
                texture.Slot.Contains("overlay", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("detail", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("decal", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("dirt", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("grime", StringComparison.OrdinalIgnoreCase));
    }

    private static CanonicalTexture? SelectLayeredColorTexture(IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = new CanonicalMaterial("Scoped", textures);
        return SelectLayeredColorTexture(scope);
    }

    private static CanonicalTexture? SelectLayeredColorTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        if (material?.LayeredTextureSlots is { Count: > 0 })
        {
            foreach (var preferredSlot in material.LayeredTextureSlots)
            {
                var explicitSlotMatch = textures.FirstOrDefault(texture =>
                    texture.Slot.Equals(preferredSlot, StringComparison.OrdinalIgnoreCase));
                if (explicitSlotMatch is not null)
                {
                    return explicitSlotMatch;
                }
            }
        }

        return SelectLayeredColorTexture(textures);
    }
}
