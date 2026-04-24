using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.WinUI.SharpDX;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App;

public sealed partial class MainWindow
{
    private static MeshGeometry3D CreateGeometry(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot)
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

        var textureCoordinates = SelectTextureCoordinates(mesh, scene, materialIndex, renderMode, selectedSlot);
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

    private static IReadOnlyList<float> SelectTextureCoordinates(CanonicalMesh mesh, CanonicalScene scene, int materialIndex, SceneRenderMode renderMode, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryPassContract = BuildViewportPrimaryPassContract(material);
        var primaryTexture = SelectViewportUvTexture(material, renderMode, selectedSlot);
        var resolvedUvBinding = BuildViewportResolvedUvBinding(material, primaryTexture, selectedSlot, primaryPassContract);
        var coordinates = SelectViewportUvCoordinates(mesh, resolvedUvBinding);

        if (renderMode != SceneRenderMode.MaterialUv ||
            primaryPassContract.Family == ViewportPrimaryPassFamily.HelperProjective ||
            !resolvedUvBinding.HasAuthoritativeTransform)
        {
            return coordinates;
        }

        var transformed = new float[coordinates.Count];
        for (var index = 0; index + 1 < coordinates.Count; index += 2)
        {
            transformed[index] = (coordinates[index] * resolvedUvBinding.UvScaleU) + resolvedUvBinding.UvOffsetU;
            transformed[index + 1] = (coordinates[index + 1] * resolvedUvBinding.UvScaleV) + resolvedUvBinding.UvOffsetV;
        }

        return transformed;
    }

    private static CanonicalTexture? SelectPrimaryViewportTexture(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var textures = SelectViewportTextures(material, renderMode, selectedSlot);
        if (textures.Count == 0)
        {
            return null;
        }

        return textures[0];
    }

    private static CanonicalTexture? SelectViewportUvTexture(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
    {
        var textures = SelectViewportTextures(material, renderMode, selectedSlot);
        if (textures.Count == 0)
        {
            return null;
        }

        var primaryPassFamily = BuildViewportPrimaryPassContract(material).Family;
        if (SelectViewportHelperInspectionTexture(material, textures, primaryPassFamily) is { } helperInspectionTexture)
        {
            return helperInspectionTexture;
        }

        return textures[0];
    }

    private static IReadOnlyList<float> SelectViewportUvCoordinates(CanonicalMesh mesh, ViewportResolvedUvBinding resolvedUvBinding)
    {
        var requestedChannel = resolvedUvBinding.HasAuthoritativeChannelBinding
            ? resolvedUvBinding.UvChannel
            : mesh.PreferredUvChannel;

        if (requestedChannel == 1 && mesh.Uv1s is { Count: > 0 })
        {
            return mesh.Uv1s;
        }

        if (mesh.Uv0s is { Count: > 0 })
        {
            return mesh.Uv0s;
        }

        return mesh.Uvs;
    }

    private static Vector3Collection BuildPreviewNormals(IList<Vector3> positions, IList<int> triangleIndices)
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

    private static PhongMaterial? CreateMaterial(
        CanonicalScene scene,
        int materialIndex,
        SceneRenderMode renderMode,
        string? selectedSlot,
        OverlayMaterialPassVariant overlayPassVariant = OverlayMaterialPassVariant.Primary,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile = null)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryPassContract = BuildViewportPrimaryPassContract(material, overlayPassVariant);
        var primaryPassFamily = primaryPassContract.Family;
        var stageInteractionProfile = BuildViewportStageInteractionProfile(material, primaryPassContract);
        var overlayPolicy = BuildOverlayCompositorPolicy(material);
        var textures = SelectViewportTextures(material, renderMode, selectedSlot);
        var surfaceTextures = SelectViewportSurfaceTextures(material, textures, selectedSlot);
        var diffuseTexture = SelectBaseColorTexture(material, surfaceTextures);
        var layeredColorTexture = SelectLayeredColorTexture(material, surfaceTextures);
        var explicitEmissiveTexture = SelectExplicitEmissiveTexture(surfaceTextures);
        var opacityTexture = SelectViewportOpacityTexture(material, surfaceTextures, diffuseTexture, selectedSlot);
        var skintoneOverlayExecutionPlan = stageInteractionProfile.UseSkintoneOverlayExecution
            ? BuildSkintoneOverlayExecutionPlan(
                material,
                diffuseTexture,
                layeredColorTexture,
                opacityTexture)
            : (SkintoneOverlayExecutionPlan?)null;
        var overlayExecutionPlan = stageInteractionProfile.UseOverlayExecution
            ? BuildOverlayExecutionPlan(
                overlayPolicy,
                material,
                surfaceTextures,
                diffuseTexture,
                layeredColorTexture,
                explicitEmissiveTexture,
                opacityTexture)
            : (OverlayExecutionPlan?)null;
        var emissiveTexture = overlayExecutionPlan?.EmissiveTexture ??
            SelectEmissiveTexture(material, surfaceTextures, diffuseTexture, layeredColorTexture);
        var normalTexture = SelectViewportNormalTexture(material, textures);
        var specularTexture = SelectViewportSpecularTexture(material, textures);
        var colorShiftMaskTexture = SelectColorShiftMaskTexture(textures);
        var selectedSlotTexture = !string.IsNullOrWhiteSpace(selectedSlot)
            ? textures.FirstOrDefault(texture => texture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
            : null;
        var helperInspectionTexture = SelectViewportHelperInspectionTexture(
            material,
            textures,
            primaryPassFamily,
            selectedSlotTexture);
        var viewportColorTexture = ResolveViewportStageViewportColorTexture(
            stageInteractionProfile,
            helperInspectionTexture,
            selectedSlotTexture,
            overlayExecutionPlan,
            skintoneOverlayExecutionPlan,
            layeredColorTexture,
            diffuseTexture,
            emissiveTexture);
        var primaryAlphaProfile = IsViewportPrimaryPassVariant(overlayPassVariant)
            ? BuildViewportPrimaryAlphaProfile(
                primaryPassContract,
                material,
                surfaceTextures,
                diffuseTexture,
                opacityTexture,
                viewportColorTexture,
                selectedSlot)
            : (ViewportPrimaryAlphaProfile?)null;
        var resolvedUvBinding = BuildViewportResolvedUvBinding(material, viewportColorTexture, selectedSlot, primaryPassContract);
        var uvTransform = primaryPassFamily == ViewportPrimaryPassFamily.HelperProjective
            ? new UVTransform(0f)
            : BuildUvTransform(resolvedUvBinding);
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
        var stageRenderRoutingProfile = BuildViewportStageRenderRoutingProfile(
            material,
            renderMode,
            overlayPassVariant,
            stageInteractionProfile,
            surfaceTextures,
            diffuseTexture,
            selectedSlot,
            viewportColorTexture,
            emissiveTexture,
            primaryAlphaProfile,
            overlayExecutionPlan,
            skintoneOverlayExecutionPlan);
        var renderDiffuseMap = stageRenderRoutingProfile.RenderDiffuseMap;
        var renderEmissiveMap = stageRenderRoutingProfile.RenderEmissiveMap;
        var renderAlphaMap = stageRenderRoutingProfile.RenderAlphaMap;
        var hasLitNormalDetail = normalTexture is not null;
        var hasLitSpecularDetail = specularTexture is not null;
        var primaryMaterialBehaviorProfile = BuildViewportPrimaryMaterialBehaviorProfile(
            primaryPassContract,
            renderMode,
            renderAlphaMap,
            stageInteractionProfile,
            hasLitNormalDetail,
            hasLitSpecularDetail);
        var (textureModel, usesSwatchComposite) = CreateViewportTextureModel(
            material,
            primaryPassContract,
            overlayExecutionPlan,
            diffuseTexture,
            layeredColorTexture,
            opacityTexture,
            viewportColorTexture,
            colorShiftMaskTexture,
            viewportTintColor,
            primaryMaterialBehaviorProfile.ForceOpaqueViewportTexture,
            allowStageComposite: string.IsNullOrWhiteSpace(selectedSlot));
        var emissiveTextureModel = stageInteractionProfile.IsHelperInspectionStage || emissiveTexture is null
            ? null
            : new TextureModel(new MemoryStream(emissiveTexture.PngBytes), autoCloseStream: true);
        var alphaTextureModel = stageInteractionProfile.IsHelperInspectionStage
            ? null
            : IsViewportPrimaryPassVariant(overlayPassVariant)
                ? CreateViewportPrimarySourceAlphaTextureModel(primaryAlphaProfile)
                : CreateViewportAlphaTextureModel(material, primaryPassContract, overlayExecutionPlan, opacityTexture, viewportColorTexture);
        var primaryAlphaSourcePngBytes = primaryAlphaProfile?.AlphaSourceTexture?.PngBytes;
        var normalTextureModel = stageInteractionProfile.IsHelperInspectionStage || normalTexture is null
            ? null
            : new TextureModel(new MemoryStream(normalTexture.PngBytes), autoCloseStream: true);
        var specularTextureModel = stageInteractionProfile.IsHelperInspectionStage || specularTexture is null
            ? null
            : new TextureModel(new MemoryStream(specularTexture.PngBytes), autoCloseStream: true);
        var usesOverlayAsSecondaryLayer =
            emissiveTexture is not null &&
            layeredColorTexture is not null &&
            ReferenceEquals(emissiveTexture, layeredColorTexture);
        // The viewport tint is metadata for skintone/swatch routing, not a blanket multiplier for
        // already-decoded textured materials. If a texture map is present, keep the lit diffuse
        // multiplier neutral unless a dedicated texture composite has already baked the tint in.
        var effectiveTexturedLitDiffuseColor = new Color4(1f, 1f, 1f, 1f);
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan = stageInteractionProfile.IsCasOverlayDetailStage
            ? BuildOverlayMaterialResponsePlan(
                overlayPolicy,
                isFlat,
                renderEmissiveMap,
                textureModel,
                emissiveTextureModel,
                primaryMaterialBehaviorProfile.DefaultLitSpecularColor,
                primaryMaterialBehaviorProfile.DefaultLitSpecularShininess)
            : null;
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan = stageInteractionProfile.IsCasOverlayDetailStage
            ? BuildOverlayMaterialApplicationPlan(
                overlayExecutionPlan,
                overlayMaterialResponsePlan,
                textureModel,
                alphaTextureModel,
                renderDiffuseMap,
                renderAlphaMap)
            : null;
        ViewportPrimaryPassExecutionPlan? primaryPassExecutionPlan = IsViewportPrimaryPassVariant(overlayPassVariant)
            ? BuildViewportPrimaryPassExecutionPlan(
                primaryPassContract,
                renderMode,
                textureModel,
                alphaTextureModel,
                primaryAlphaSourcePngBytes,
                normalTextureModel,
                specularTextureModel)
            : null;
        ViewportPrimaryPassResponsePlan? primaryPassResponsePlan = primaryPassExecutionPlan is { } resolvedPrimaryExecutionPlan
            ? BuildViewportPrimaryPassResponsePlan(
                renderMode,
                primaryPassContract,
                resolvedPrimaryExecutionPlan,
                diffuseColor,
                ambientColor,
                primaryMaterialBehaviorProfile.LitAmbientColor,
                primaryMaterialBehaviorProfile.DefaultLitSpecularColor,
                primaryMaterialBehaviorProfile.DefaultLitSpecularShininess,
                effectiveTexturedLitDiffuseColor)
            : null;
        ViewportPrimaryPassApplicationPlan? primaryPassApplicationPlan = primaryPassExecutionPlan is { } resolvedPrimaryExecutionPlanForApplication
            ? BuildViewportPrimaryPassApplicationPlan(
                renderMode,
                primaryPassContract,
                resolvedPrimaryExecutionPlanForApplication,
                primaryMaterialBehaviorProfile)
            : null;
        ViewportPrimaryPassMaterialPlan? primaryPassPlan = IsViewportPrimaryPassVariant(overlayPassVariant)
            ? BuildViewportPrimaryPassMaterialPlan(
                uvTransform,
                primaryPassContract,
                primaryPassExecutionPlan,
                primaryPassResponsePlan,
                primaryPassApplicationPlan)
            : null;
        if (!IsViewportPrimaryPassVariant(overlayPassVariant))
        {
            var latePassPlan = BuildViewportLatePassMaterialPlan(
                overlayPassVariant,
                material,
                renderMode,
                selectedSlot,
                latePassSceneRelationProfile,
                overlayExecutionPlan,
                overlayMaterialResponsePlan,
                textureModel,
                skintoneOverlayExecutionPlan);
            return latePassPlan is null
                ? null
                : BuildViewportLatePassMaterial(material, selectedSlot, latePassPlan.Value);
        }

        if (primaryPassPlan is { } resolvedPrimaryPassPlan)
        {
            return BuildViewportPrimaryPassMaterial(resolvedPrimaryPassPlan);
        }

        var fallbackMaterialPlan = BuildViewportFallbackMaterialPlan(
            renderMode,
            stageInteractionProfile,
            overlayPolicy,
            stageRenderRoutingProfile,
            viewportColorTexture,
            textureModel,
            emissiveTextureModel,
            alphaTextureModel,
            normalTextureModel,
            specularTextureModel,
            overlayMaterialResponsePlan,
            overlayMaterialApplicationPlan,
            primaryMaterialBehaviorProfile,
            usesOverlayAsSecondaryLayer,
            diffuseColor,
            ambientColor,
            effectiveTexturedLitDiffuseColor,
            uvTransform);
        return BuildViewportFallbackMaterial(fallbackMaterialPlan);
    }

    private static ViewportPrimaryPassMaterialPlan? BuildViewportPrimaryPassMaterialPlan(
        UVTransform uvTransform,
        ViewportPrimaryPassContract primaryPassContract,
        ViewportPrimaryPassExecutionPlan? primaryPassExecutionPlan,
        ViewportPrimaryPassResponsePlan? primaryPassResponsePlan,
        ViewportPrimaryPassApplicationPlan? primaryPassApplicationPlan)
    {
        if (primaryPassContract.Family == ViewportPrimaryPassFamily.None ||
            primaryPassExecutionPlan is not { } executionPlan ||
            primaryPassResponsePlan is not { } responsePlan ||
            primaryPassApplicationPlan is not { } applicationPlan)
        {
            return null;
        }

        return new ViewportPrimaryPassMaterialPlan(
            Contract: primaryPassContract,
            ExecutionPlan: executionPlan,
            ResponsePlan: responsePlan,
            ApplicationPlan: applicationPlan,
            DiffuseColor: responsePlan.DiffuseColor,
            AmbientColor: responsePlan.AmbientColor,
            EmissiveColor: responsePlan.EmissiveColor,
            SpecularColor: responsePlan.SpecularColor,
            SpecularShininess: responsePlan.SpecularShininess,
            RenderDiffuseMap: applicationPlan.RenderDiffuseMap,
            DiffuseMap: applicationPlan.DiffuseMap,
            RenderEmissiveMap: applicationPlan.RenderEmissiveMap,
            EmissiveMap: applicationPlan.EmissiveMap,
            RenderDiffuseAlphaMap: applicationPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap: applicationPlan.DiffuseAlphaMap,
            RenderNormalMap: applicationPlan.RenderNormalMap,
            NormalMap: applicationPlan.NormalMap,
            RenderSpecularColorMap: applicationPlan.RenderSpecularColorMap,
            SpecularColorMap: applicationPlan.SpecularColorMap,
            EnableAutoTangent: applicationPlan.EnableAutoTangent,
            RenderShadowMap: applicationPlan.RenderShadowMap,
            UVTransform: uvTransform);
    }

    private static ViewportPrimaryPassResponsePlan BuildViewportPrimaryPassResponsePlan(
        SceneRenderMode renderMode,
        ViewportPrimaryPassContract primaryPassContract,
        ViewportPrimaryPassExecutionPlan primaryPassExecutionPlan,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 litAmbientColor,
        Color4 defaultLitSpecularColor,
        float defaultLitSpecularShininess,
        Color4 effectiveTexturedLitDiffuseColor)
    {
        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var hasMaterialDetailMaps = primaryPassExecutionPlan.NormalMap is not null || primaryPassExecutionPlan.SpecularColorMap is not null;
        var surfaceResponseProfile = BuildViewportPrimarySurfaceResponseProfile(
            primaryPassContract,
            hasMaterialDetailMaps,
            litAmbientColor,
            defaultLitSpecularColor,
            defaultLitSpecularShininess);
        var effectiveDiffuseColor = isWireframe
            ? new Color4(0.95f, 0.95f, 0.95f, 1f)
            : isLit
                ? (primaryPassExecutionPlan.DiffuseMap is not null ? effectiveTexturedLitDiffuseColor : diffuseColor)
                : primaryPassExecutionPlan.DiffuseMap is null
                    ? diffuseColor
                    : new Color4(0f, 0f, 0f, 1f);
        var effectiveAmbientColor = isFlat
            ? primaryPassExecutionPlan.DiffuseMap is null
                ? diffuseColor
                : new Color4(0f, 0f, 0f, 1f)
            : isLit
                ? (primaryPassExecutionPlan.DiffuseMap is not null ? surfaceResponseProfile.LitAmbientColor : ambientColor)
                : ambientColor;
        var effectiveEmissiveColor = isFlat
            ? new Color4(1f, 1f, 1f, 1f)
            : primaryPassContract.SuppressLitEmissive
                ? new Color4(0f, 0f, 0f, 1f)
                : new Color4(0f, 0f, 0f, 1f);

        return new ViewportPrimaryPassResponsePlan(
            DiffuseColor: ScaleColor(effectiveDiffuseColor, primaryPassContract.DiffuseScale),
            AmbientColor: ScaleColor(effectiveAmbientColor, primaryPassContract.AmbientScale),
            EmissiveColor: ScaleColor(effectiveEmissiveColor, primaryPassContract.EmissiveScale),
            SpecularColor: ScaleColor(surfaceResponseProfile.SpecularColor, primaryPassContract.SpecularScale),
            SpecularShininess: surfaceResponseProfile.SpecularShininess * primaryPassContract.SpecularShininessMultiplier,
            RuleTag: $"{primaryPassContract.ContractTag}|{primaryPassExecutionPlan.RuleTag}|{surfaceResponseProfile.RuleTag}|primary-response");
    }

    private static ViewportPrimaryPassApplicationPlan BuildViewportPrimaryPassApplicationPlan(
        SceneRenderMode renderMode,
        ViewportPrimaryPassContract primaryPassContract,
        ViewportPrimaryPassExecutionPlan primaryPassExecutionPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile)
    {
        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var alphaTextureModel = CreateViewportPrimaryPassAlphaTextureModel(primaryPassExecutionPlan, primaryPassContract.AlphaScaleMultiplier);
        return new ViewportPrimaryPassApplicationPlan(
            RenderDiffuseMap: !isWireframe && !isFlat && primaryPassExecutionPlan.RenderDiffuseMap,
            DiffuseMap: !isWireframe && !isFlat ? primaryPassExecutionPlan.DiffuseMap : null,
            RenderEmissiveMap: isFlat && primaryPassExecutionPlan.RenderEmissiveMap,
            EmissiveMap: isFlat ? primaryPassExecutionPlan.EmissiveMap : null,
            RenderDiffuseAlphaMap: primaryPassExecutionPlan.RenderDiffuseAlphaMap && alphaTextureModel is not null,
            DiffuseAlphaMap: alphaTextureModel,
            RenderNormalMap: isLit && primaryPassContract.AllowNormalMap && primaryPassExecutionPlan.RenderNormalMap,
            NormalMap: isLit && primaryPassContract.AllowNormalMap ? primaryPassExecutionPlan.NormalMap : null,
            RenderSpecularColorMap: isLit && primaryPassContract.AllowSpecularMap && primaryPassExecutionPlan.RenderSpecularColorMap,
            SpecularColorMap: isLit && primaryPassContract.AllowSpecularMap ? primaryPassExecutionPlan.SpecularColorMap : null,
            EnableAutoTangent: isLit && primaryPassContract.AllowNormalMap && primaryPassExecutionPlan.EnableAutoTangent,
            RenderShadowMap: isLit && primaryMaterialBehaviorProfile.RenderShadowMap,
            RuleTag: $"{primaryPassExecutionPlan.RuleTag}|primary-application");
    }

    private static ViewportPrimaryMaterialBehaviorProfile BuildViewportPrimaryMaterialBehaviorProfile(
        ViewportPrimaryPassContract primaryPassContract,
        SceneRenderMode renderMode,
        bool renderAlphaMap,
        ViewportStageInteractionProfile stageInteractionProfile,
        bool hasLitNormalDetail,
        bool hasLitSpecularDetail)
    {
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var useMatteLitShading = isLit &&
            (stageInteractionProfile.IsCasOverlayHighLayerDetailStage || (!hasLitNormalDetail && !hasLitSpecularDetail));
        return new ViewportPrimaryMaterialBehaviorProfile(
            ForceOpaqueViewportTexture: isLit &&
                !renderAlphaMap &&
                !stageInteractionProfile.IsCasOverlayDetailStage &&
                !stageInteractionProfile.IsSimSkintoneOverlayStage,
            LitAmbientColor: useMatteLitShading
                ? new Color4(0.46f, 0.46f, 0.46f, 1f)
                : new Color4(0.34f, 0.34f, 0.34f, 1f),
            DefaultLitSpecularColor: useMatteLitShading
                ? new Color4(0.02f, 0.02f, 0.02f, 1f)
                : new Color4(0.08f, 0.08f, 0.08f, 1f),
            DefaultLitSpecularShininess: useMatteLitShading ? 4f : 12f,
            RenderShadowMap: !IsHelperPrimaryPassFamily(primaryPassContract.Family),
            RuleTag: useMatteLitShading
                ? "matte-primary-material-behavior"
                : "default-primary-material-behavior");
    }

    private static ViewportPrimarySurfaceResponseProfile BuildViewportPrimarySurfaceResponseProfile(
        ViewportPrimaryPassContract primaryPassContract,
        bool hasMaterialDetailMaps,
        Color4 litAmbientColor,
        Color4 defaultLitSpecularColor,
        float defaultLitSpecularShininess)
        => primaryPassContract.Family switch
        {
            ViewportPrimaryPassFamily.SkintoneBase => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: new Color4(0.44f, 0.42f, 0.42f, 1f),
                SpecularColor: defaultLitSpecularColor,
                SpecularShininess: defaultLitSpecularShininess,
                RuleTag: "skintone-base-primary-surface"),
            ViewportPrimaryPassFamily.HelperProjective => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: new Color4(0.37f, 0.4f, 0.41f, 1f),
                SpecularColor: new Color4(0f, 0f, 0f, 1f),
                SpecularShininess: 0f,
                RuleTag: "helper-projective-primary-surface"),
            ViewportPrimaryPassFamily.HelperLayered => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: new Color4(0.39f, 0.42f, 0.39f, 1f),
                SpecularColor: new Color4(0f, 0f, 0f, 1f),
                SpecularShininess: 0f,
                RuleTag: "helper-layered-primary-surface"),
            ViewportPrimaryPassFamily.HelperUtility => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: new Color4(0.43f, 0.41f, 0.37f, 1f),
                SpecularColor: new Color4(0f, 0f, 0f, 1f),
                SpecularShininess: 0f,
                RuleTag: "helper-utility-primary-surface"),
            ViewportPrimaryPassFamily.OverlayBase => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: hasMaterialDetailMaps
                    ? new Color4(0.4f, 0.4f, 0.4f, 1f)
                    : new Color4(0.41f, 0.41f, 0.41f, 1f),
                SpecularColor: hasMaterialDetailMaps
                    ? defaultLitSpecularColor
                    : new Color4(0.04f, 0.04f, 0.04f, 1f),
                SpecularShininess: hasMaterialDetailMaps
                    ? defaultLitSpecularShininess
                    : 7f,
                RuleTag: "overlay-base-primary-surface"),
            ViewportPrimaryPassFamily.HighLayerOverlayBase => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: hasMaterialDetailMaps
                    ? litAmbientColor
                    : new Color4(0.42f, 0.42f, 0.42f, 1f),
                SpecularColor: hasMaterialDetailMaps
                    ? defaultLitSpecularColor
                    : new Color4(0.03f, 0.03f, 0.03f, 1f),
                SpecularShininess: hasMaterialDetailMaps
                    ? defaultLitSpecularShininess
                    : 5f,
                RuleTag: "highlayer-base-primary-surface"),
            _ => new ViewportPrimarySurfaceResponseProfile(
                LitAmbientColor: hasMaterialDetailMaps
                    ? litAmbientColor
                    : new Color4(0.42f, 0.42f, 0.42f, 1f),
                SpecularColor: defaultLitSpecularColor,
                SpecularShininess: defaultLitSpecularShininess,
                RuleTag: "generic-primary-surface")
        };

    private static TextureModel? CreateViewportPrimaryPassAlphaTextureModel(
        ViewportPrimaryPassExecutionPlan primaryPassExecutionPlan,
        float alphaScaleMultiplier)
    {
        if (!primaryPassExecutionPlan.RenderDiffuseAlphaMap || primaryPassExecutionPlan.DiffuseAlphaMap is null)
        {
            return null;
        }

        if (Math.Abs(alphaScaleMultiplier - 1f) <= 0.001f)
        {
            return primaryPassExecutionPlan.DiffuseAlphaMap;
        }

        var sourcePngBytes = primaryPassExecutionPlan.DiffuseAlphaPngBytes;
        if (sourcePngBytes is null)
        {
            return primaryPassExecutionPlan.DiffuseAlphaMap;
        }

        var scaledAlphaPng = ScalePngAlpha(sourcePngBytes, alphaScaleMultiplier);
        return scaledAlphaPng is null
            ? primaryPassExecutionPlan.DiffuseAlphaMap
            : new TextureModel(new MemoryStream(scaledAlphaPng), autoCloseStream: true);
    }

    private static ViewportPrimaryPassExecutionPlan? BuildViewportPrimaryPassExecutionPlan(
        ViewportPrimaryPassContract primaryPassContract,
        SceneRenderMode renderMode,
        TextureModel? textureModel,
        TextureModel? alphaTextureModel,
        byte[]? alphaSourcePngBytes,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel)
    {
        if (primaryPassContract.Family == ViewportPrimaryPassFamily.None)
        {
            return null;
        }

        var executionProfile = BuildViewportPrimaryExecutionProfile(primaryPassContract);
        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        return new ViewportPrimaryPassExecutionPlan(
            Intent: primaryPassContract.Intent,
            RenderDiffuseMap: !isWireframe && !isFlat && textureModel is not null,
            DiffuseMap: !isWireframe && !isFlat ? textureModel : null,
            RenderEmissiveMap: isFlat && textureModel is not null,
            EmissiveMap: isFlat ? textureModel : null,
            RenderDiffuseAlphaMap: executionProfile.AllowAlphaMap && alphaTextureModel is not null,
            DiffuseAlphaMap: executionProfile.AllowAlphaMap ? alphaTextureModel : null,
            DiffuseAlphaPngBytes: executionProfile.AllowAlphaMap ? alphaSourcePngBytes : null,
            RenderNormalMap: executionProfile.AllowNormalMap && isLit && normalTextureModel is not null,
            NormalMap: executionProfile.AllowNormalMap && isLit ? normalTextureModel : null,
            RenderSpecularColorMap: executionProfile.AllowSpecularMap && isLit && specularTextureModel is not null,
            SpecularColorMap: executionProfile.AllowSpecularMap && isLit ? specularTextureModel : null,
            EnableAutoTangent: executionProfile.AllowAutoTangent && isLit && normalTextureModel is not null,
            RuleTag: executionProfile.RuleTag);
    }

    private static ViewportPrimaryExecutionProfile BuildViewportPrimaryExecutionProfile(
        ViewportPrimaryPassContract primaryPassContract)
        => primaryPassContract.Intent switch
        {
            ViewportPrimaryPassIntent.SkintoneCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: true,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                AllowAutoTangent: true,
                RuleTag: "skintone-base-primary-execution"),
            ViewportPrimaryPassIntent.OverlayCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: true,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                AllowAutoTangent: true,
                RuleTag: "overlay-base-primary-execution"),
            ViewportPrimaryPassIntent.HighLayerCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: true,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                AllowAutoTangent: true,
                RuleTag: "highlayer-base-primary-execution"),
            ViewportPrimaryPassIntent.HelperProjectiveCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: false,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                AllowAutoTangent: false,
                RuleTag: "helper-projective-primary-execution"),
            ViewportPrimaryPassIntent.HelperLayeredCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: false,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                AllowAutoTangent: false,
                RuleTag: "helper-layered-primary-execution"),
            ViewportPrimaryPassIntent.HelperUtilityCarrier => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: false,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                AllowAutoTangent: false,
                RuleTag: "helper-utility-primary-execution"),
            _ => new ViewportPrimaryExecutionProfile(
                AllowAlphaMap: true,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                AllowAutoTangent: true,
                RuleTag: "generic-primary-execution")
        };

    private static ViewportPrimaryPassContract BuildViewportPrimaryPassContract(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant = OverlayMaterialPassVariant.Primary)
    {
        var resolvedPassVariant = ResolveViewportPrimaryPassVariant(material, passVariant);
        var familyProfile = BuildViewportPrimaryFamilyProfile(ResolveViewportPrimaryPassFamily(resolvedPassVariant));
        return familyProfile is { } resolvedFamilyProfile
            ? new ViewportPrimaryPassContract(
                Family: resolvedFamilyProfile.Family,
                Intent: resolvedFamilyProfile.Intent,
                StackOrder: resolvedFamilyProfile.StackOrder,
                RenderStage: resolvedFamilyProfile.RenderStage,
                RequiresExplicitOpacityAuthority: resolvedFamilyProfile.RequiresExplicitOpacityAuthority,
                DisallowViewportColorAlphaFallback: resolvedFamilyProfile.DisallowViewportColorAlphaFallback,
                SuppressLitEmissive: resolvedFamilyProfile.SuppressLitEmissive,
                AlphaScaleMultiplier: resolvedFamilyProfile.AlphaScaleMultiplier,
                AllowNormalMap: resolvedFamilyProfile.AllowNormalMap,
                AllowSpecularMap: resolvedFamilyProfile.AllowSpecularMap,
                DiffuseScale: resolvedFamilyProfile.DiffuseScale,
                AmbientScale: resolvedFamilyProfile.AmbientScale,
                EmissiveScale: resolvedFamilyProfile.EmissiveScale,
                SpecularScale: resolvedFamilyProfile.SpecularScale,
                SpecularShininessMultiplier: resolvedFamilyProfile.SpecularShininessMultiplier,
                ContractTag: resolvedFamilyProfile.ContractTag)
            : new ViewportPrimaryPassContract(
                Family: ViewportPrimaryPassFamily.None,
                Intent: ViewportPrimaryPassIntent.None,
                StackOrder: 0,
                RenderStage: GetViewportCompositorStageOrder(material?.PreviewCompositorStage),
                RequiresExplicitOpacityAuthority: false,
                DisallowViewportColorAlphaFallback: false,
                SuppressLitEmissive: false,
                AlphaScaleMultiplier: 1f,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                DiffuseScale: 1f,
                AmbientScale: 1f,
                EmissiveScale: 1f,
                SpecularScale: 1f,
                SpecularShininessMultiplier: 1f,
                ContractTag: "generic-primary-stage");
    }

    private static PhongMaterial BuildViewportPrimaryPassMaterial(ViewportPrimaryPassMaterialPlan primaryPassPlan) =>
        new()
        {
            DiffuseColor = primaryPassPlan.DiffuseColor,
            AmbientColor = primaryPassPlan.AmbientColor,
            EmissiveColor = primaryPassPlan.EmissiveColor,
            SpecularColor = primaryPassPlan.SpecularColor,
            SpecularShininess = primaryPassPlan.SpecularShininess,
            RenderDiffuseMap = primaryPassPlan.RenderDiffuseMap,
            DiffuseMap = primaryPassPlan.DiffuseMap,
            RenderEmissiveMap = primaryPassPlan.RenderEmissiveMap,
            EmissiveMap = primaryPassPlan.EmissiveMap,
            RenderDiffuseAlphaMap = primaryPassPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = primaryPassPlan.DiffuseAlphaMap,
            RenderNormalMap = primaryPassPlan.RenderNormalMap,
            NormalMap = primaryPassPlan.NormalMap,
            RenderSpecularColorMap = primaryPassPlan.RenderSpecularColorMap,
            SpecularColorMap = primaryPassPlan.SpecularColorMap,
            EnableAutoTangent = primaryPassPlan.EnableAutoTangent,
            RenderShadowMap = primaryPassPlan.RenderShadowMap,
            UVTransform = primaryPassPlan.UVTransform
        };

    private static ViewportStageRenderRoutingProfile BuildViewportStageRenderRoutingProfile(
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        OverlayMaterialPassVariant overlayPassVariant,
        ViewportStageInteractionProfile stageInteractionProfile,
        IReadOnlyList<CanonicalTexture> surfaceTextures,
        CanonicalTexture? diffuseTexture,
        string? selectedSlot,
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? emissiveTexture,
        ViewportPrimaryAlphaProfile? primaryAlphaProfile,
        OverlayExecutionPlan? overlayExecutionPlan,
        SkintoneOverlayExecutionPlan? skintoneOverlayExecutionPlan)
    {
        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var renderDiffuseMap = !isWireframe && !isFlat && viewportColorTexture is not null;
        var renderEmissiveMap = stageInteractionProfile.IsHelperInspectionStage
            ? false
            : (isFlat && viewportColorTexture is not null) || (!isFlat && emissiveTexture is not null);
        var renderAlphaMap = stageInteractionProfile.IsHelperInspectionStage
            ? false
            : IsViewportPrimaryPassVariant(overlayPassVariant)
                ? primaryAlphaProfile?.RenderAlphaMap ?? false
            : stageInteractionProfile.IsCasOverlayDetailStage
                ? overlayExecutionPlan?.RenderAlphaMap ?? false
            : stageInteractionProfile.IsSimSkintoneOverlayStage
                ? skintoneOverlayExecutionPlan?.RenderAlphaMap ?? false
            : ShouldRenderTransparentViewport(material, surfaceTextures, diffuseTexture, selectedSlot);
        return new ViewportStageRenderRoutingProfile(
            RenderDiffuseMap: renderDiffuseMap,
            RenderEmissiveMap: renderEmissiveMap,
            RenderAlphaMap: renderAlphaMap,
            RuleTag: stageInteractionProfile.IsHelperInspectionStage
                ? $"{stageInteractionProfile.RuleTag}|helper-stage-routing"
                : IsViewportPrimaryPassVariant(overlayPassVariant)
                    ? $"{stageInteractionProfile.RuleTag}|primary-stage-routing"
                : stageInteractionProfile.IsCasOverlayDetailStage
                    ? $"{stageInteractionProfile.RuleTag}|overlay-stage-routing"
                : stageInteractionProfile.IsSimSkintoneOverlayStage
                    ? $"{stageInteractionProfile.RuleTag}|skintone-stage-routing"
                    : $"{stageInteractionProfile.RuleTag}|generic-stage-routing");
    }

    private static ViewportFallbackMaterialPlan BuildViewportFallbackMaterialPlan(
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform)
    {
        var fallbackMaterialPathProfile = BuildViewportFallbackMaterialPathProfile(stageInteractionProfile);
        return fallbackMaterialPathProfile.Kind switch
        {
            ViewportFallbackMaterialPathKind.HelperInspection => BuildViewportHelperInspectionFallbackMaterialPlan(
                fallbackMaterialPathProfile,
                renderMode,
                stageInteractionProfile,
                overlayPolicy,
                stageRenderRoutingProfile,
                viewportColorTexture,
                textureModel,
                emissiveTextureModel,
                alphaTextureModel,
                normalTextureModel,
                specularTextureModel,
                overlayMaterialResponsePlan,
                overlayMaterialApplicationPlan,
                primaryMaterialBehaviorProfile,
                usesOverlayAsSecondaryLayer,
                diffuseColor,
                ambientColor,
                effectiveTexturedLitDiffuseColor,
                uvTransform),
            ViewportFallbackMaterialPathKind.SkintoneOverlayStage => BuildViewportSkintoneFallbackMaterialPlan(
                fallbackMaterialPathProfile,
                renderMode,
                stageInteractionProfile,
                overlayPolicy,
                stageRenderRoutingProfile,
                viewportColorTexture,
                textureModel,
                emissiveTextureModel,
                alphaTextureModel,
                normalTextureModel,
                specularTextureModel,
                overlayMaterialResponsePlan,
                overlayMaterialApplicationPlan,
                primaryMaterialBehaviorProfile,
                usesOverlayAsSecondaryLayer,
                diffuseColor,
                ambientColor,
                effectiveTexturedLitDiffuseColor,
                uvTransform),
            ViewportFallbackMaterialPathKind.OverlayStage => BuildViewportOverlayFallbackMaterialPlan(
                fallbackMaterialPathProfile,
                renderMode,
                stageInteractionProfile,
                overlayPolicy,
                stageRenderRoutingProfile,
                viewportColorTexture,
                textureModel,
                emissiveTextureModel,
                alphaTextureModel,
                normalTextureModel,
                specularTextureModel,
                overlayMaterialResponsePlan,
                overlayMaterialApplicationPlan,
                primaryMaterialBehaviorProfile,
                usesOverlayAsSecondaryLayer,
                diffuseColor,
                ambientColor,
                effectiveTexturedLitDiffuseColor,
                uvTransform),
            _ => BuildViewportGenericFallbackMaterialPlan(
                fallbackMaterialPathProfile,
                renderMode,
                stageInteractionProfile,
                overlayPolicy,
                stageRenderRoutingProfile,
                viewportColorTexture,
                textureModel,
                emissiveTextureModel,
                alphaTextureModel,
                normalTextureModel,
                specularTextureModel,
                overlayMaterialResponsePlan,
                overlayMaterialApplicationPlan,
                primaryMaterialBehaviorProfile,
                usesOverlayAsSecondaryLayer,
                diffuseColor,
                ambientColor,
                effectiveTexturedLitDiffuseColor,
                uvTransform)
        };
    }

    private static ViewportFallbackMaterialPlan BuildViewportHelperInspectionFallbackMaterialPlan(
        ViewportFallbackMaterialPathProfile fallbackMaterialPathProfile,
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform) =>
        BuildViewportFallbackMaterialPlanCore(
            fallbackMaterialPathProfile,
            renderMode,
            stageInteractionProfile,
            overlayPolicy,
            stageRenderRoutingProfile,
            viewportColorTexture,
            textureModel,
            emissiveTextureModel,
            alphaTextureModel,
            normalTextureModel,
            specularTextureModel,
            overlayMaterialResponsePlan,
            overlayMaterialApplicationPlan,
            primaryMaterialBehaviorProfile,
            usesOverlayAsSecondaryLayer,
            diffuseColor,
            ambientColor,
            effectiveTexturedLitDiffuseColor,
            uvTransform);

    private static ViewportFallbackMaterialPlan BuildViewportSkintoneFallbackMaterialPlan(
        ViewportFallbackMaterialPathProfile fallbackMaterialPathProfile,
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform) =>
        BuildViewportFallbackMaterialPlanCore(
            fallbackMaterialPathProfile,
            renderMode,
            stageInteractionProfile,
            overlayPolicy,
            stageRenderRoutingProfile,
            viewportColorTexture,
            textureModel,
            emissiveTextureModel,
            alphaTextureModel,
            normalTextureModel,
            specularTextureModel,
            overlayMaterialResponsePlan,
            overlayMaterialApplicationPlan,
            primaryMaterialBehaviorProfile,
            usesOverlayAsSecondaryLayer,
            diffuseColor,
            ambientColor,
            effectiveTexturedLitDiffuseColor,
            uvTransform);

    private static ViewportFallbackMaterialPlan BuildViewportOverlayFallbackMaterialPlan(
        ViewportFallbackMaterialPathProfile fallbackMaterialPathProfile,
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform) =>
        BuildViewportFallbackMaterialPlanCore(
            fallbackMaterialPathProfile,
            renderMode,
            stageInteractionProfile,
            overlayPolicy,
            stageRenderRoutingProfile,
            viewportColorTexture,
            textureModel,
            emissiveTextureModel,
            alphaTextureModel,
            normalTextureModel,
            specularTextureModel,
            overlayMaterialResponsePlan,
            overlayMaterialApplicationPlan,
            primaryMaterialBehaviorProfile,
            usesOverlayAsSecondaryLayer,
            diffuseColor,
            ambientColor,
            effectiveTexturedLitDiffuseColor,
            uvTransform);

    private static ViewportFallbackMaterialPlan BuildViewportGenericFallbackMaterialPlan(
        ViewportFallbackMaterialPathProfile fallbackMaterialPathProfile,
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform) =>
        BuildViewportFallbackMaterialPlanCore(
            fallbackMaterialPathProfile,
            renderMode,
            stageInteractionProfile,
            overlayPolicy,
            stageRenderRoutingProfile,
            viewportColorTexture,
            textureModel,
            emissiveTextureModel,
            alphaTextureModel,
            normalTextureModel,
            specularTextureModel,
            overlayMaterialResponsePlan,
            overlayMaterialApplicationPlan,
            primaryMaterialBehaviorProfile,
            usesOverlayAsSecondaryLayer,
            diffuseColor,
            ambientColor,
            effectiveTexturedLitDiffuseColor,
            uvTransform);

    private static ViewportFallbackMaterialPlan BuildViewportFallbackMaterialPlanCore(
        ViewportFallbackMaterialPathProfile fallbackMaterialPathProfile,
        SceneRenderMode renderMode,
        ViewportStageInteractionProfile stageInteractionProfile,
        OverlayCompositorPolicy overlayPolicy,
        ViewportStageRenderRoutingProfile stageRenderRoutingProfile,
        CanonicalTexture? viewportColorTexture,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        TextureModel? alphaTextureModel,
        TextureModel? normalTextureModel,
        TextureModel? specularTextureModel,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        OverlayMaterialApplicationPlan? overlayMaterialApplicationPlan,
        ViewportPrimaryMaterialBehaviorProfile primaryMaterialBehaviorProfile,
        bool usesOverlayAsSecondaryLayer,
        Color4 diffuseColor,
        Color4 ambientColor,
        Color4 effectiveTexturedLitDiffuseColor,
        UVTransform uvTransform)
    {
        var isFlat = renderMode == SceneRenderMode.FlatTexture;
        var isWireframe = renderMode == SceneRenderMode.Wireframe;
        var isLit = renderMode == SceneRenderMode.LitTexture;
        var renderDiffuseMap = stageRenderRoutingProfile.RenderDiffuseMap;
        var renderEmissiveMap = stageRenderRoutingProfile.RenderEmissiveMap;
        var renderAlphaMap = stageRenderRoutingProfile.RenderAlphaMap;
        return new ViewportFallbackMaterialPlan(
            PathProfile: fallbackMaterialPathProfile,
            DiffuseColor: isWireframe
                ? new Color4(0.95f, 0.95f, 0.95f, 1f)
                : isLit
                    ? (renderDiffuseMap ? effectiveTexturedLitDiffuseColor : diffuseColor)
                    : viewportColorTexture is null
                        ? diffuseColor
                        : new Color4(0f, 0f, 0f, 1f),
            AmbientColor: isFlat
                ? viewportColorTexture is null
                    ? diffuseColor
                    : new Color4(0f, 0f, 0f, 1f)
                : isLit
                    ? (renderDiffuseMap ? primaryMaterialBehaviorProfile.LitAmbientColor : ambientColor)
                    : ambientColor,
            EmissiveColor: isFlat
                ? new Color4(1f, 1f, 1f, 1f)
                : stageInteractionProfile.IsCasOverlayDetailStage || stageInteractionProfile.IsSimSkintoneOverlayStage
                    ? overlayMaterialResponsePlan?.EmissiveColor ?? overlayPolicy.OverlayEmissiveColor
                : usesOverlayAsSecondaryLayer
                    ? new Color4(0.45f, 0.45f, 0.45f, 1f)
                    : new Color4(0f, 0f, 0f, 1f),
            SpecularColor: isFlat
                ? new Color4(0f, 0f, 0f, 1f)
                : overlayMaterialResponsePlan?.SpecularColor ?? primaryMaterialBehaviorProfile.DefaultLitSpecularColor,
            SpecularShininess: isFlat
                ? 0f
                : overlayMaterialResponsePlan?.SpecularShininess ?? primaryMaterialBehaviorProfile.DefaultLitSpecularShininess,
            RenderDiffuseMap: overlayMaterialApplicationPlan?.RenderDiffuseMap ?? renderDiffuseMap,
            DiffuseMap: overlayMaterialApplicationPlan?.DiffuseMap ?? (renderDiffuseMap ? textureModel : null),
            RenderEmissiveMap: overlayMaterialApplicationPlan?.RenderEmissiveMap ?? overlayMaterialResponsePlan?.RenderEmissiveMap ?? renderEmissiveMap,
            EmissiveMap: isFlat
                ? (renderEmissiveMap ? textureModel : null)
                : overlayMaterialApplicationPlan?.EmissiveMap ?? overlayMaterialResponsePlan?.EmissiveMap ?? emissiveTextureModel,
            RenderDiffuseAlphaMap: overlayMaterialApplicationPlan?.RenderDiffuseAlphaMap ?? renderAlphaMap,
            DiffuseAlphaMap: overlayMaterialApplicationPlan?.DiffuseAlphaMap ?? (renderAlphaMap ? alphaTextureModel : null),
            RenderNormalMap: overlayMaterialApplicationPlan?.RenderNormalMap ?? (isLit && normalTextureModel is not null),
            NormalMap: overlayMaterialApplicationPlan?.NormalMap ?? (isLit ? normalTextureModel : null),
            RenderSpecularColorMap: overlayMaterialApplicationPlan?.RenderSpecularColorMap ?? (isLit && specularTextureModel is not null),
            SpecularColorMap: overlayMaterialApplicationPlan?.SpecularColorMap ?? (isLit ? specularTextureModel : null),
            EnableAutoTangent: overlayMaterialApplicationPlan?.EnableAutoTangent ?? (isLit && normalTextureModel is not null),
            RenderShadowMap: isLit,
            UVTransform: uvTransform,
            RuleTag: $"{fallbackMaterialPathProfile.RuleTag}|{stageRenderRoutingProfile.RuleTag}|fallback-material-plan");
    }

    private static ViewportFallbackMaterialPathProfile BuildViewportFallbackMaterialPathProfile(
        ViewportStageInteractionProfile stageInteractionProfile) =>
        stageInteractionProfile.IsHelperInspectionStage
            ? new ViewportFallbackMaterialPathProfile(
                Kind: ViewportFallbackMaterialPathKind.HelperInspection,
                RuleTag: $"{stageInteractionProfile.RuleTag}|helper-fallback-path")
            : stageInteractionProfile.IsSimSkintoneOverlayStage
                ? new ViewportFallbackMaterialPathProfile(
                    Kind: ViewportFallbackMaterialPathKind.SkintoneOverlayStage,
                    RuleTag: $"{stageInteractionProfile.RuleTag}|skintone-fallback-path")
                : stageInteractionProfile.IsCasOverlayDetailStage
                    ? new ViewportFallbackMaterialPathProfile(
                        Kind: ViewportFallbackMaterialPathKind.OverlayStage,
                        RuleTag: $"{stageInteractionProfile.RuleTag}|overlay-fallback-path")
                    : new ViewportFallbackMaterialPathProfile(
                        Kind: ViewportFallbackMaterialPathKind.GenericSurface,
                        RuleTag: $"{stageInteractionProfile.RuleTag}|generic-fallback-path");

    private static PhongMaterial BuildViewportFallbackMaterial(ViewportFallbackMaterialPlan fallbackMaterialPlan) =>
        fallbackMaterialPlan.PathProfile.Kind switch
        {
            ViewportFallbackMaterialPathKind.HelperInspection => BuildViewportHelperInspectionFallbackMaterial(fallbackMaterialPlan),
            ViewportFallbackMaterialPathKind.SkintoneOverlayStage => BuildViewportSkintoneFallbackMaterial(fallbackMaterialPlan),
            ViewportFallbackMaterialPathKind.OverlayStage => BuildViewportOverlayFallbackMaterial(fallbackMaterialPlan),
            _ => BuildViewportGenericFallbackMaterial(fallbackMaterialPlan)
        };

    private static PhongMaterial BuildViewportHelperInspectionFallbackMaterial(ViewportFallbackMaterialPlan fallbackMaterialPlan) =>
        new()
        {
            DiffuseColor = fallbackMaterialPlan.DiffuseColor,
            AmbientColor = fallbackMaterialPlan.AmbientColor,
            EmissiveColor = fallbackMaterialPlan.EmissiveColor,
            SpecularColor = fallbackMaterialPlan.SpecularColor,
            SpecularShininess = fallbackMaterialPlan.SpecularShininess,
            RenderDiffuseMap = fallbackMaterialPlan.RenderDiffuseMap,
            DiffuseMap = fallbackMaterialPlan.DiffuseMap,
            RenderEmissiveMap = fallbackMaterialPlan.RenderEmissiveMap,
            EmissiveMap = fallbackMaterialPlan.EmissiveMap,
            RenderDiffuseAlphaMap = fallbackMaterialPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = fallbackMaterialPlan.DiffuseAlphaMap,
            RenderNormalMap = fallbackMaterialPlan.RenderNormalMap,
            NormalMap = fallbackMaterialPlan.NormalMap,
            RenderSpecularColorMap = fallbackMaterialPlan.RenderSpecularColorMap,
            SpecularColorMap = fallbackMaterialPlan.SpecularColorMap,
            EnableAutoTangent = fallbackMaterialPlan.EnableAutoTangent,
            RenderShadowMap = fallbackMaterialPlan.RenderShadowMap,
            UVTransform = fallbackMaterialPlan.UVTransform
        };

    private static PhongMaterial BuildViewportSkintoneFallbackMaterial(ViewportFallbackMaterialPlan fallbackMaterialPlan) =>
        new()
        {
            DiffuseColor = fallbackMaterialPlan.DiffuseColor,
            AmbientColor = fallbackMaterialPlan.AmbientColor,
            EmissiveColor = fallbackMaterialPlan.EmissiveColor,
            SpecularColor = fallbackMaterialPlan.SpecularColor,
            SpecularShininess = fallbackMaterialPlan.SpecularShininess,
            RenderDiffuseMap = fallbackMaterialPlan.RenderDiffuseMap,
            DiffuseMap = fallbackMaterialPlan.DiffuseMap,
            RenderEmissiveMap = fallbackMaterialPlan.RenderEmissiveMap,
            EmissiveMap = fallbackMaterialPlan.EmissiveMap,
            RenderDiffuseAlphaMap = fallbackMaterialPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = fallbackMaterialPlan.DiffuseAlphaMap,
            RenderNormalMap = fallbackMaterialPlan.RenderNormalMap,
            NormalMap = fallbackMaterialPlan.NormalMap,
            RenderSpecularColorMap = fallbackMaterialPlan.RenderSpecularColorMap,
            SpecularColorMap = fallbackMaterialPlan.SpecularColorMap,
            EnableAutoTangent = fallbackMaterialPlan.EnableAutoTangent,
            RenderShadowMap = fallbackMaterialPlan.RenderShadowMap,
            UVTransform = fallbackMaterialPlan.UVTransform
        };

    private static PhongMaterial BuildViewportOverlayFallbackMaterial(ViewportFallbackMaterialPlan fallbackMaterialPlan) =>
        new()
        {
            DiffuseColor = fallbackMaterialPlan.DiffuseColor,
            AmbientColor = fallbackMaterialPlan.AmbientColor,
            EmissiveColor = fallbackMaterialPlan.EmissiveColor,
            SpecularColor = fallbackMaterialPlan.SpecularColor,
            SpecularShininess = fallbackMaterialPlan.SpecularShininess,
            RenderDiffuseMap = fallbackMaterialPlan.RenderDiffuseMap,
            DiffuseMap = fallbackMaterialPlan.DiffuseMap,
            RenderEmissiveMap = fallbackMaterialPlan.RenderEmissiveMap,
            EmissiveMap = fallbackMaterialPlan.EmissiveMap,
            RenderDiffuseAlphaMap = fallbackMaterialPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = fallbackMaterialPlan.DiffuseAlphaMap,
            RenderNormalMap = fallbackMaterialPlan.RenderNormalMap,
            NormalMap = fallbackMaterialPlan.NormalMap,
            RenderSpecularColorMap = fallbackMaterialPlan.RenderSpecularColorMap,
            SpecularColorMap = fallbackMaterialPlan.SpecularColorMap,
            EnableAutoTangent = fallbackMaterialPlan.EnableAutoTangent,
            RenderShadowMap = fallbackMaterialPlan.RenderShadowMap,
            UVTransform = fallbackMaterialPlan.UVTransform
        };

    private static PhongMaterial BuildViewportGenericFallbackMaterial(ViewportFallbackMaterialPlan fallbackMaterialPlan) =>
        new()
        {
            DiffuseColor = fallbackMaterialPlan.DiffuseColor,
            AmbientColor = fallbackMaterialPlan.AmbientColor,
            EmissiveColor = fallbackMaterialPlan.EmissiveColor,
            SpecularColor = fallbackMaterialPlan.SpecularColor,
            SpecularShininess = fallbackMaterialPlan.SpecularShininess,
            RenderDiffuseMap = fallbackMaterialPlan.RenderDiffuseMap,
            DiffuseMap = fallbackMaterialPlan.DiffuseMap,
            RenderEmissiveMap = fallbackMaterialPlan.RenderEmissiveMap,
            EmissiveMap = fallbackMaterialPlan.EmissiveMap,
            RenderDiffuseAlphaMap = fallbackMaterialPlan.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = fallbackMaterialPlan.DiffuseAlphaMap,
            RenderNormalMap = fallbackMaterialPlan.RenderNormalMap,
            NormalMap = fallbackMaterialPlan.NormalMap,
            RenderSpecularColorMap = fallbackMaterialPlan.RenderSpecularColorMap,
            SpecularColorMap = fallbackMaterialPlan.SpecularColorMap,
            EnableAutoTangent = fallbackMaterialPlan.EnableAutoTangent,
            RenderShadowMap = fallbackMaterialPlan.RenderShadowMap,
            UVTransform = fallbackMaterialPlan.UVTransform
        };

    private static CanonicalTexture? SelectViewportHelperInspectionTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        ViewportPrimaryPassFamily primaryPassFamily,
        CanonicalTexture? selectedSlotTexture = null)
    {
        if (!IsHelperPrimaryPassFamily(primaryPassFamily))
        {
            return null;
        }

        return selectedSlotTexture ??
            primaryPassFamily switch
            {
                ViewportPrimaryPassFamily.HelperProjective => SelectHelperProjectiveInspectionTexture(textures),
                ViewportPrimaryPassFamily.HelperLayered => SelectHelperLayeredInspectionTexture(material, textures),
                ViewportPrimaryPassFamily.HelperUtility => SelectHelperUtilityInspectionTexture(material, textures),
                _ => null
            };
    }

    private static ViewportOverlayStageProfile BuildViewportOverlayStageProfile(CanonicalMaterial? material)
    {
        var stage = material?.PreviewCompositorStage;
        var isCasOverlayHighLayerDetailStage = string.Equals(stage, "cas-overlay-highlayer-detail", StringComparison.OrdinalIgnoreCase);
        var isCasOverlayDetailStage = isCasOverlayHighLayerDetailStage ||
            string.Equals(stage, "cas-overlay", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(stage, "cas-overlay-detail", StringComparison.OrdinalIgnoreCase);
        var isSimSkintoneOverlayStage = string.Equals(stage, "sim-skintone-overlay", StringComparison.OrdinalIgnoreCase);
        var isCasOverlayPassStage = isCasOverlayDetailStage ||
            ResolveViewportPrimaryPassFamily(material) is
                ViewportPrimaryPassFamily.OverlayBase or
                ViewportPrimaryPassFamily.HighLayerOverlayBase;
        return new ViewportOverlayStageProfile(
            IsCasOverlayDetailStage: isCasOverlayDetailStage,
            IsCasOverlayHighLayerDetailStage: isCasOverlayHighLayerDetailStage,
            IsSimSkintoneOverlayStage: isSimSkintoneOverlayStage,
            IsCasOverlayPassStage: isCasOverlayPassStage,
            RuleTag: isSimSkintoneOverlayStage
                ? "skintone-overlay-stage-profile"
                : isCasOverlayHighLayerDetailStage
                    ? "overlay-highlayer-stage-profile"
                    : isCasOverlayDetailStage
                        ? "overlay-detail-stage-profile"
                        : isCasOverlayPassStage
                            ? "overlay-pass-stage-profile"
                            : "generic-stage-profile");
    }

    private static ViewportStageInteractionProfile BuildViewportStageInteractionProfile(
        CanonicalMaterial? material,
        ViewportPrimaryPassContract primaryPassContract)
    {
        var isHelperInspectionStage = IsHelperPrimaryPassFamily(primaryPassContract.Family);
        var overlayStageProfile = BuildViewportOverlayStageProfile(material);
        var isCasOverlayDetailStage = overlayStageProfile.IsCasOverlayDetailStage;
        var isCasOverlayHighLayerDetailStage = overlayStageProfile.IsCasOverlayHighLayerDetailStage;
        var isSimSkintoneOverlayStage = overlayStageProfile.IsSimSkintoneOverlayStage;
        var useOverlayExecution = isCasOverlayDetailStage && !isHelperInspectionStage;
        var useSkintoneOverlayExecution = isSimSkintoneOverlayStage && !isHelperInspectionStage;
        return new ViewportStageInteractionProfile(
            IsHelperInspectionStage: isHelperInspectionStage,
            IsCasOverlayDetailStage: isCasOverlayDetailStage,
            IsCasOverlayHighLayerDetailStage: isCasOverlayHighLayerDetailStage,
            IsSimSkintoneOverlayStage: isSimSkintoneOverlayStage,
            UseOverlayExecution: useOverlayExecution,
            UseSkintoneOverlayExecution: useSkintoneOverlayExecution,
            UsesOverlayViewportColor: useOverlayExecution,
            UsesSkintoneViewportColor: !useOverlayExecution && useSkintoneOverlayExecution,
            RuleTag: isHelperInspectionStage
                ? $"{primaryPassContract.ContractTag}|helper-stage-interaction"
                : useOverlayExecution
                    ? "overlay-detail-stage-interaction"
                    : useSkintoneOverlayExecution
                        ? "skintone-overlay-stage-interaction"
                        : "generic-stage-interaction");
    }

    private static CanonicalTexture? ResolveViewportStageViewportColorTexture(
        ViewportStageInteractionProfile stageInteractionProfile,
        CanonicalTexture? helperInspectionTexture,
        CanonicalTexture? selectedSlotTexture,
        OverlayExecutionPlan? overlayExecutionPlan,
        SkintoneOverlayExecutionPlan? skintoneOverlayExecutionPlan,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? emissiveTexture)
    {
        if (helperInspectionTexture is not null)
        {
            return helperInspectionTexture;
        }

        if (selectedSlotTexture is not null)
        {
            return selectedSlotTexture;
        }

        if (stageInteractionProfile.UsesOverlayViewportColor)
        {
            return overlayExecutionPlan?.ViewportColorTexture;
        }

        if (stageInteractionProfile.UsesSkintoneViewportColor)
        {
            return skintoneOverlayExecutionPlan?.ViewportColorTexture ?? layeredColorTexture ?? diffuseTexture ?? emissiveTexture;
        }

        return diffuseTexture ?? layeredColorTexture ?? emissiveTexture;
    }

    private static (TextureModel? TextureModel, bool UsesSwatchComposite) CreateViewportTextureModel(
        CanonicalMaterial? material,
        ViewportPrimaryPassContract primaryPassContract,
        OverlayExecutionPlan? overlayExecutionPlan,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture,
        CanonicalTexture? colorShiftMaskTexture,
        Color4? viewportTintColor,
        bool forceOpaqueAlpha,
        bool allowStageComposite)
    {
        var primaryTextureCompositeProfile = BuildViewportPrimaryTextureCompositeProfile(
            material,
            primaryPassContract,
            viewportTintColor);
        if (allowStageComposite &&
            overlayExecutionPlan is { } executionPlan &&
            TryCreateOverlayExecutionTextureModel(executionPlan, forceOpaqueAlpha, out var overlayTextureModel, out var usesOverlayComposite))
        {
            return (overlayTextureModel, usesOverlayComposite);
        }

        if (allowStageComposite &&
            primaryTextureCompositeProfile is { } resolvedPrimaryTextureCompositeProfile &&
            resolvedPrimaryTextureCompositeProfile.Mode == ViewportPrimaryTextureCompositeMode.TintedLayered)
        {
            byte[]? compositedPngBytes = null;
            if (diffuseTexture is not null && layeredColorTexture is not null)
            {
                compositedPngBytes = ComposeTintedLayeredPng(
                    diffuseTexture.PngBytes,
                    resolvedPrimaryTextureCompositeProfile.TintColor!.Value,
                    layeredColorTexture.PngBytes,
                    opacityTexture?.PngBytes);
            }

            compositedPngBytes ??= viewportColorTexture is not null
                ? ComposeTintedPng(viewportColorTexture.PngBytes, resolvedPrimaryTextureCompositeProfile.TintColor!.Value)
                : null;
            if (compositedPngBytes is not null)
            {
                if (forceOpaqueAlpha)
                {
                    compositedPngBytes = ForceOpaquePngAlpha(compositedPngBytes);
                }

                return (new TextureModel(new MemoryStream(compositedPngBytes), autoCloseStream: true), true);
            }
        }

        if (primaryTextureCompositeProfile is { } resolvedPrimaryTextureCompositeProfileForSwatch &&
            resolvedPrimaryTextureCompositeProfileForSwatch.Mode == ViewportPrimaryTextureCompositeMode.SwatchMasked &&
            viewportColorTexture is not null &&
            colorShiftMaskTexture is not null)
        {
            var compositedPngBytes = ComposeSwatchMaskedPng(
                viewportColorTexture.PngBytes,
                colorShiftMaskTexture.PngBytes,
                resolvedPrimaryTextureCompositeProfileForSwatch.TintColor!.Value);
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

    private static ViewportPrimaryTextureCompositeProfile? BuildViewportPrimaryTextureCompositeProfile(
        CanonicalMaterial? material,
        ViewportPrimaryPassContract primaryPassContract,
        Color4? viewportTintColor)
    {
        if (primaryPassContract.Family == ViewportPrimaryPassFamily.SkintoneBase &&
            viewportTintColor is not null)
        {
            return new ViewportPrimaryTextureCompositeProfile(
                Mode: ViewportPrimaryTextureCompositeMode.TintedLayered,
                TintColor: viewportTintColor,
                RuleTag: $"{primaryPassContract.ContractTag}|skintone-primary-texture-composite");
        }

        if (material?.SourceKind == CanonicalMaterialSourceKind.ApproximateCas &&
            viewportTintColor is not null)
        {
            return new ViewportPrimaryTextureCompositeProfile(
                Mode: ViewportPrimaryTextureCompositeMode.SwatchMasked,
                TintColor: viewportTintColor,
                RuleTag: $"{primaryPassContract.ContractTag}|approximate-primary-texture-composite");
        }

        return null;
    }

    private static TextureModel? CreateViewportAlphaTextureModel(
        CanonicalMaterial? material,
        ViewportPrimaryPassContract? primaryPassContract,
        OverlayExecutionPlan? overlayExecutionPlan,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture)
    {
        if (overlayExecutionPlan is { } executionPlan)
        {
            return CreateOverlayExecutionAlphaTextureModel(executionPlan);
        }

        var overlayPolicy = BuildOverlayCompositorPolicy(material);
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        var resolvedPrimaryPassContract = primaryPassContract ?? BuildViewportPrimaryPassContract(material);

        if (resolvedPrimaryPassContract.Family == ViewportPrimaryPassFamily.HighLayerOverlayBase &&
            ResolveViewportPrimaryAlphaSourceTexture(
                resolvedPrimaryPassContract,
                material,
                opacityTexture,
                viewportColorTexture) is null)
        {
            return null;
        }

        if (IsCasOverlayDetailStage(material) &&
            !ShouldRenderOverlayPolicyTransparency(
                overlayPolicy,
                material,
                compositionRuleProfile,
                opacityTexture,
                viewportColorTexture))
        {
            return null;
        }

        var alphaSourceTexture = ResolveOverlayAlphaSourceTexture(
            overlayPolicy,
            material,
            compositionRuleProfile,
            opacityTexture,
            viewportColorTexture);
        var sourcePngBytes = alphaSourceTexture?.PngBytes;
        if (sourcePngBytes is null)
        {
            return null;
        }

        var alphaScale = overlayPolicy.OverlayOpacityScale;
        if (Math.Abs(alphaScale - 1f) <= 0.001f)
        {
            return new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true);
        }

        var scaledAlphaPng = ScalePngAlpha(sourcePngBytes, alphaScale);
        return scaledAlphaPng is null
            ? new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true)
            : new TextureModel(new MemoryStream(scaledAlphaPng), autoCloseStream: true);
    }

    private static byte[]? ComposeTintedPng(byte[] sourcePngBytes, Color4 tintColor)
    {
        try
        {
            using var sourceStream = new InMemoryRandomAccessStream();
            sourceStream.WriteAsync(sourcePngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            sourceStream.Seek(0);

            var sourceDecoder = BitmapDecoder.CreateAsync(sourceStream).AsTask().GetAwaiter().GetResult();
            var sourcePixels = sourceDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();

            var tintR = Math.Clamp(tintColor.Red, 0f, 1f);
            var tintG = Math.Clamp(tintColor.Green, 0f, 1f);
            var tintB = Math.Clamp(tintColor.Blue, 0f, 1f);
            var composedPixels = new byte[sourcePixels.Length];
            for (var offset = 0; offset < sourcePixels.Length; offset += 4)
            {
                var baseB = sourcePixels[offset] / 255f;
                var baseG = sourcePixels[offset + 1] / 255f;
                var baseR = sourcePixels[offset + 2] / 255f;

                composedPixels[offset] = (byte)Math.Clamp((int)Math.Round(baseB * tintB * 255f), 0, 255);
                composedPixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(baseG * tintG * 255f), 0, 255);
                composedPixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(baseR * tintR * 255f), 0, 255);
                composedPixels[offset + 3] = sourcePixels[offset + 3];
            }

            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                sourceDecoder.PixelWidth,
                sourceDecoder.PixelHeight,
                sourceDecoder.DpiX,
                sourceDecoder.DpiY,
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

    private static byte[]? ComposeGrayscaleOverlayPng(byte[] sourcePngBytes, byte[]? opacityPngBytes)
    {
        try
        {
            using var sourceStream = new InMemoryRandomAccessStream();
            sourceStream.WriteAsync(sourcePngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            sourceStream.Seek(0);

            var sourceDecoder = BitmapDecoder.CreateAsync(sourceStream).AsTask().GetAwaiter().GetResult();
            var sourcePixels = sourceDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();

            byte[]? opacityPixels = null;
            if (opacityPngBytes is not null)
            {
                using var opacityStream = new InMemoryRandomAccessStream();
                opacityStream.WriteAsync(opacityPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
                opacityStream.Seek(0);

                var opacityDecoder = BitmapDecoder.CreateAsync(opacityStream).AsTask().GetAwaiter().GetResult();
                opacityPixels = opacityDecoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform
                    {
                        ScaledWidth = sourceDecoder.PixelWidth,
                        ScaledHeight = sourceDecoder.PixelHeight
                    },
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            }

            var composedPixels = new byte[sourcePixels.Length];
            for (var offset = 0; offset < sourcePixels.Length; offset += 4)
            {
                var blue = sourcePixels[offset] / 255f;
                var green = sourcePixels[offset + 1] / 255f;
                var red = sourcePixels[offset + 2] / 255f;
                var luminance = Math.Clamp((0.114f * blue) + (0.587f * green) + (0.299f * red), 0f, 1f);
                var alpha = sourcePixels[offset + 3] / 255f;
                if (opacityPixels is not null)
                {
                    alpha *= ExtractOpacityMaskFactor(opacityPixels, offset);
                }

                var grayscale = (byte)Math.Clamp((int)Math.Round(luminance * 255f), 0, 255);
                composedPixels[offset] = grayscale;
                composedPixels[offset + 1] = grayscale;
                composedPixels[offset + 2] = grayscale;
                composedPixels[offset + 3] = (byte)Math.Clamp((int)Math.Round(alpha * 255f), 0, 255);
            }

            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                sourceDecoder.PixelWidth,
                sourceDecoder.PixelHeight,
                sourceDecoder.DpiX,
                sourceDecoder.DpiY,
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

    private static byte[]? ComposeTintedLayeredPng(
        byte[] basePngBytes,
        Color4 tintColor,
        byte[] overlayPngBytes,
        byte[]? opacityPngBytes)
    {
        try
        {
            using var baseStream = new InMemoryRandomAccessStream();
            baseStream.WriteAsync(basePngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            baseStream.Seek(0);

            using var overlayStream = new InMemoryRandomAccessStream();
            overlayStream.WriteAsync(overlayPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
            overlayStream.Seek(0);

            var baseDecoder = BitmapDecoder.CreateAsync(baseStream).AsTask().GetAwaiter().GetResult();
            var overlayDecoder = BitmapDecoder.CreateAsync(overlayStream).AsTask().GetAwaiter().GetResult();
            var basePixels = baseDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform(),
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            var overlayPixels = overlayDecoder.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                new BitmapTransform
                {
                    ScaledWidth = baseDecoder.PixelWidth,
                    ScaledHeight = baseDecoder.PixelHeight
                },
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();

            byte[]? opacityPixels = null;
            if (opacityPngBytes is not null)
            {
                using var opacityStream = new InMemoryRandomAccessStream();
                opacityStream.WriteAsync(opacityPngBytes.AsBuffer()).AsTask().GetAwaiter().GetResult();
                opacityStream.Seek(0);

                var opacityDecoder = BitmapDecoder.CreateAsync(opacityStream).AsTask().GetAwaiter().GetResult();
                opacityPixels = opacityDecoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform
                    {
                        ScaledWidth = baseDecoder.PixelWidth,
                        ScaledHeight = baseDecoder.PixelHeight
                    },
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage).AsTask().GetAwaiter().GetResult().DetachPixelData();
            }

            var tintR = Math.Clamp(tintColor.Red, 0f, 1f);
            var tintG = Math.Clamp(tintColor.Green, 0f, 1f);
            var tintB = Math.Clamp(tintColor.Blue, 0f, 1f);
            var composedPixels = new byte[basePixels.Length];
            for (var offset = 0; offset < basePixels.Length; offset += 4)
            {
                var baseB = basePixels[offset] / 255f * tintB;
                var baseG = basePixels[offset + 1] / 255f * tintG;
                var baseR = basePixels[offset + 2] / 255f * tintR;
                var baseA = basePixels[offset + 3] / 255f;

                var overlayB = overlayPixels[offset] / 255f;
                var overlayG = overlayPixels[offset + 1] / 255f;
                var overlayR = overlayPixels[offset + 2] / 255f;
                var overlayA = overlayPixels[offset + 3] / 255f;
                if (opacityPixels is not null)
                {
                    overlayA *= ExtractOpacityMaskFactor(opacityPixels, offset);
                }

                var outA = overlayA + (baseA * (1f - overlayA));
                var outB = (overlayB * overlayA) + (baseB * (1f - overlayA));
                var outG = (overlayG * overlayA) + (baseG * (1f - overlayA));
                var outR = (overlayR * overlayA) + (baseR * (1f - overlayA));

                composedPixels[offset] = (byte)Math.Clamp((int)Math.Round(outB * 255f), 0, 255);
                composedPixels[offset + 1] = (byte)Math.Clamp((int)Math.Round(outG * 255f), 0, 255);
                composedPixels[offset + 2] = (byte)Math.Clamp((int)Math.Round(outR * 255f), 0, 255);
                composedPixels[offset + 3] = (byte)Math.Clamp((int)Math.Round(outA * 255f), 0, 255);
            }

            using var output = new InMemoryRandomAccessStream();
            var encoder = BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, output).AsTask().GetAwaiter().GetResult();
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Straight,
                baseDecoder.PixelWidth,
                baseDecoder.PixelHeight,
                baseDecoder.DpiX,
                baseDecoder.DpiY,
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

    private static float ExtractOpacityMaskFactor(byte[] opacityPixels, int offset)
    {
        var alpha = opacityPixels[offset + 3] / 255f;
        if (alpha > 0f && alpha < 0.999f)
        {
            return alpha;
        }

        var blue = opacityPixels[offset] / 255f;
        var green = opacityPixels[offset + 1] / 255f;
        var red = opacityPixels[offset + 2] / 255f;
        return Math.Clamp((red + green + blue) / 3f, 0f, 1f);
    }

    private static byte[]? ScalePngAlpha(byte[] pngBytes, float alphaScale)
    {
        try
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
                var scaledAlpha = Math.Clamp((int)Math.Round(pixels[offset] * alphaScale), 0, 255);
                pixels[offset] = (byte)scaledAlpha;
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
        catch
        {
            return null;
        }
    }

    private static bool IsCasOverlayDetailStage(CanonicalMaterial? material) =>
        BuildViewportOverlayStageProfile(material).IsCasOverlayDetailStage;

    private static bool IsCasOverlayHighLayerDetailStage(CanonicalMaterial? material) =>
        BuildViewportOverlayStageProfile(material).IsCasOverlayHighLayerDetailStage;

    private static bool IsCasOverlayPassStage(CanonicalMaterial? material) =>
        BuildViewportOverlayStageProfile(material).IsCasOverlayPassStage;

    private static bool IsSimSkintoneOverlayStage(CanonicalMaterial? material) =>
        BuildViewportOverlayStageProfile(material).IsSimSkintoneOverlayStage;

    private static bool IsHelperInspectionStage(CanonicalMaterial? material) =>
        IsHelperPrimaryPassFamily(BuildViewportPrimaryPassContract(material).Family);

    private static bool IsComposition32WornOverlayLane(CanonicalMaterial? material) =>
        material?.CompositionMethod == 32 &&
        IsCasOverlayPassStage(material) &&
        material.CasPartSlotCategory is "Full Body" or "Top" or "Bottom" or "Shoes" or "Accessory";

    private static bool IsOverlayLateEmissivePassIntent(OverlayPassIntent passIntent) =>
        passIntent is OverlayPassIntent.WornLayer or OverlayPassIntent.HighLayer;

    private static bool IsOverlayExecutionPassIntent(OverlayPassIntent passIntent) =>
        passIntent is OverlayPassIntent.CosmeticDetail or
            OverlayPassIntent.DefaultDetail or
            OverlayPassIntent.OrderedDetail or
            OverlayPassIntent.WornLayer or
            OverlayPassIntent.HighLayer;

    private static bool IsOverlayDetailLatePassIntent(OverlayPassIntent passIntent) =>
        passIntent is OverlayPassIntent.CosmeticDetail or
            OverlayPassIntent.DefaultDetail or
            OverlayPassIntent.OrderedDetail;

    private static OverlaySortLayerThresholdsProfile BuildOverlaySortLayerThresholdsProfile() =>
        new(
            OrderedHighSortLayerFloor: 10000,
            HighLayerSortLayerFloor: 65536,
            RuleTag: "sortlayer-thresholds");

    private static bool IsHighLayerComposition32WornOverlayLane(CanonicalMaterial? material) =>
        IsComposition32WornOverlayLane(material) &&
        (material?.SortLayer ?? 0) >= BuildOverlaySortLayerThresholdsProfile().HighLayerSortLayerFloor;

    private static bool IsOrdinaryComposition32WornOverlayLane(CanonicalMaterial? material) =>
        IsComposition32WornOverlayLane(material) &&
        !IsHighLayerComposition32WornOverlayLane(material);

    private static bool IsCosmeticCompositionOverlayLane(CanonicalMaterial? material) =>
        BuildViewportOverlayStageProfile(material) is { IsCasOverlayDetailStage: true, IsCasOverlayHighLayerDetailStage: false } &&
        material?.CompositionMethod is 2 or 3 or 4 &&
        material.CasPartSlotCategory is "Head" or "Body" or "Hair";

    private static OverlaySortLayerBucket ClassifyOverlaySortLayerBucket(CanonicalMaterial? material)
    {
        if (!IsCasOverlayPassStage(material))
        {
            return OverlaySortLayerBucket.None;
        }

        if (IsHighLayerComposition32WornOverlayLane(material))
        {
            return OverlaySortLayerBucket.HighLayer;
        }

        var sortLayer = material?.SortLayer ?? 0;
        var thresholdsProfile = BuildOverlaySortLayerThresholdsProfile();
        if (sortLayer >= thresholdsProfile.OrderedHighSortLayerFloor)
        {
            return OverlaySortLayerBucket.OrderedHigh;
        }

        if (sortLayer > 0)
        {
            return OverlaySortLayerBucket.OrderedLow;
        }

        return OverlaySortLayerBucket.Default;
    }

    private static OverlaySortLayerBucketProfile BuildOverlaySortLayerBucketProfile(CanonicalMaterial? material) =>
        BuildOverlaySortLayerBucketRuntimeDefaultsProfile(ClassifyOverlaySortLayerBucket(material));

    private static OverlaySortLayerBucketProfile BuildOverlaySortLayerBucketRuntimeDefaultsProfile(OverlaySortLayerBucket bucket) =>
        bucket switch
        {
            OverlaySortLayerBucket.OrderedHigh => new OverlaySortLayerBucketProfile(
                Bucket: OverlaySortLayerBucket.OrderedHigh,
                DefaultLane: OverlayCompositorLane.OrderedHigh,
                PassBucket: 30,
                OrdinaryWornPassBucket: 30,
                GenericDetailStackOrder: 130,
                RuleTag: "sortlayer-ordered-high"),
            OverlaySortLayerBucket.OrderedLow => new OverlaySortLayerBucketProfile(
                Bucket: OverlaySortLayerBucket.OrderedLow,
                DefaultLane: OverlayCompositorLane.OrderedLow,
                PassBucket: 20,
                OrdinaryWornPassBucket: 35,
                GenericDetailStackOrder: 120,
                RuleTag: "sortlayer-ordered-low"),
            OverlaySortLayerBucket.HighLayer => new OverlaySortLayerBucketProfile(
                Bucket: OverlaySortLayerBucket.HighLayer,
                DefaultLane: OverlayCompositorLane.HighLayerWorn,
                PassBucket: 40,
                OrdinaryWornPassBucket: 40,
                GenericDetailStackOrder: 140,
                RuleTag: "sortlayer-highlayer"),
            OverlaySortLayerBucket.Default => new OverlaySortLayerBucketProfile(
                Bucket: OverlaySortLayerBucket.Default,
                DefaultLane: OverlayCompositorLane.Default,
                PassBucket: 10,
                OrdinaryWornPassBucket: 35,
                GenericDetailStackOrder: 115,
                RuleTag: "sortlayer-default"),
            _ => new OverlaySortLayerBucketProfile(
                Bucket: OverlaySortLayerBucket.None,
                DefaultLane: OverlayCompositorLane.None,
                PassBucket: 0,
                OrdinaryWornPassBucket: 0,
                GenericDetailStackOrder: 100,
                RuleTag: "sortlayer-none")
        };

    private static OverlayWornLaneProfile? BuildOverlayWornLaneProfile(CanonicalMaterial? material)
    {
        if (!IsComposition32WornOverlayLane(material))
        {
            return null;
        }

        var sortLayerBucketProfile = BuildOverlaySortLayerBucketProfile(material);
        var emissiveFamilyProfile = IsHighLayerComposition32WornOverlayLane(material)
            ? BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveRestricted)
            : BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveSoft);
        if (emissiveFamilyProfile is not { } resolvedEmissiveFamilyProfile)
        {
            return null;
        }

        return IsHighLayerComposition32WornOverlayLane(material)
            ? new OverlayWornLaneProfile(
                  Lane: OverlayCompositorLane.HighLayerWorn,
                  PassIntent: OverlayPassIntent.HighLayer,
                  PassVariant: resolvedEmissiveFamilyProfile.PassVariant,
                  Family: resolvedEmissiveFamilyProfile.Family,
                  BlendIntent: resolvedEmissiveFamilyProfile.BlendIntent,
                  BlendFamily: OverlayBlendFamily.RestrictedHighLayer,
                  PassBucket: sortLayerBucketProfile.PassBucket,
                  ParityOrder: resolvedEmissiveFamilyProfile.BaseStackOrder,
                  PassPhase: resolvedEmissiveFamilyProfile.PassPhase,
                  MaterialMode: resolvedEmissiveFamilyProfile.MaterialMode,
                  AlphaScale: resolvedEmissiveFamilyProfile.AlphaScale,
                  OverlayEmissiveColor: new Color4(0.06f, 0.06f, 0.06f, 1f),
                  OverlaySpecularColor: new Color4(0.01f, 0.01f, 0.01f, 1f),
                  OverlaySpecularShininess: 3f,
                  TransparencyMode: resolvedEmissiveFamilyProfile.TransparencyMode,
                  MissingDiffuseTransparencyMode: resolvedEmissiveFamilyProfile.MissingDiffuseTransparencyMode,
                  MissingDiffuseAlphaScaleMultiplier: resolvedEmissiveFamilyProfile.MissingDiffuseAlphaScaleMultiplier,
                  MissingDiffuseEmissiveScaleMultiplier: resolvedEmissiveFamilyProfile.MissingDiffuseEmissiveScaleMultiplier,
                  RequiresExplicitOpacityForTransparency: resolvedEmissiveFamilyProfile.RequiresExplicitOpacityForRenderAlpha,
                  DisallowViewportColorAlphaFallback: resolvedEmissiveFamilyProfile.DisallowViewportColorAlphaSource,
                  RequiresDedicatedEmissiveMap: true,
                  UseExplicitEmissiveOnly: true,
                  AllowsViewportColorEmissiveFallback: false,
                  AllowsImplicitAlphaFallback: false,
                  RuleTag: "composition-32-highlayer-worn",
                  RelationTag: resolvedEmissiveFamilyProfile.RelationTag)
              : new OverlayWornLaneProfile(
                  Lane: OverlayCompositorLane.OrdinaryWorn,
                  PassIntent: OverlayPassIntent.WornLayer,
                  PassVariant: resolvedEmissiveFamilyProfile.PassVariant,
                  Family: resolvedEmissiveFamilyProfile.Family,
                  BlendIntent: resolvedEmissiveFamilyProfile.BlendIntent,
                  BlendFamily: OverlayBlendFamily.StraightAlpha,
                  PassBucket: sortLayerBucketProfile.OrdinaryWornPassBucket,
                  ParityOrder: resolvedEmissiveFamilyProfile.BaseStackOrder,
                  PassPhase: resolvedEmissiveFamilyProfile.PassPhase,
                  MaterialMode: resolvedEmissiveFamilyProfile.MaterialMode,
                  AlphaScale: resolvedEmissiveFamilyProfile.AlphaScale,
                  OverlayEmissiveColor: new Color4(0.11f, 0.11f, 0.11f, 1f),
                  OverlaySpecularColor: new Color4(0.03f, 0.03f, 0.03f, 1f),
                  OverlaySpecularShininess: 6f,
                  TransparencyMode: resolvedEmissiveFamilyProfile.TransparencyMode,
                  MissingDiffuseTransparencyMode: resolvedEmissiveFamilyProfile.MissingDiffuseTransparencyMode,
                  MissingDiffuseAlphaScaleMultiplier: resolvedEmissiveFamilyProfile.MissingDiffuseAlphaScaleMultiplier,
                  MissingDiffuseEmissiveScaleMultiplier: resolvedEmissiveFamilyProfile.MissingDiffuseEmissiveScaleMultiplier,
                  RequiresExplicitOpacityForTransparency: resolvedEmissiveFamilyProfile.RequiresExplicitOpacityForRenderAlpha,
                  DisallowViewportColorAlphaFallback: resolvedEmissiveFamilyProfile.DisallowViewportColorAlphaSource,
                  RequiresDedicatedEmissiveMap: false,
                  UseExplicitEmissiveOnly: false,
                  AllowsViewportColorEmissiveFallback: true,
                  AllowsImplicitAlphaFallback: true,
                  RuleTag: "composition-32-ordinary-worn",
                  RelationTag: resolvedEmissiveFamilyProfile.RelationTag);
    }

    private static OverlayCompositorLane ClassifyOverlayCompositorLane(CanonicalMaterial? material)
    {
        if (material is null || !IsCasOverlayPassStage(material))
        {
            return OverlayCompositorLane.None;
        }

        if (IsCosmeticCompositionOverlayLane(material))
        {
            return OverlayCompositorLane.Cosmetic;
        }

        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        if (wornLaneProfile is { } resolvedWornLaneProfile)
        {
            return resolvedWornLaneProfile.Lane;
        }

        return BuildOverlaySortLayerBucketProfile(material).DefaultLane;
    }

    private static OverlayCompositorPolicy BuildOverlayCompositorPolicy(CanonicalMaterial? material)
    {
        var lane = ClassifyOverlayCompositorLane(material);
        var sortLayerBucketProfile = BuildOverlaySortLayerBucketProfile(material);
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        var passIntent = lane switch
        {
            OverlayCompositorLane.None => OverlayPassIntent.None,
            OverlayCompositorLane.Cosmetic => OverlayPassIntent.CosmeticDetail,
            OverlayCompositorLane.Default => OverlayPassIntent.DefaultDetail,
            OverlayCompositorLane.OrderedLow => OverlayPassIntent.OrderedDetail,
            OverlayCompositorLane.OrderedHigh => OverlayPassIntent.OrderedDetail,
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.PassIntent ?? OverlayPassIntent.None,
            _ => OverlayPassIntent.None
        };

        var blendFamily = lane switch
        {
            OverlayCompositorLane.Cosmetic when compositionRuleProfile is not null => GetOverlayBlendFamily(compositionRuleProfile),
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.BlendFamily ?? OverlayBlendFamily.None,
            OverlayCompositorLane.Cosmetic or OverlayCompositorLane.Default or OverlayCompositorLane.OrderedLow or OverlayCompositorLane.OrderedHigh => OverlayBlendFamily.StraightAlpha,
            _ => OverlayBlendFamily.None
        };

        var passBucket = lane switch
        {
            OverlayCompositorLane.None => material is null ? 50 : 0,
            OverlayCompositorLane.Cosmetic => 5,
            OverlayCompositorLane.Default or OverlayCompositorLane.OrderedLow or OverlayCompositorLane.OrderedHigh => sortLayerBucketProfile.PassBucket,
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.PassBucket ?? 0,
            _ => 0
        };

        var useGrayscaleComposite = blendFamily == OverlayBlendFamily.GrayscaleDetail;

        var overlayOpacityScale = blendFamily == OverlayBlendFamily.StraightAlpha &&
            lane == OverlayCompositorLane.Cosmetic &&
            compositionRuleProfile is not null
            ? compositionRuleProfile.Value.AlphaScale
            : 1f;

        var overlayEmissiveColor = lane switch
        {
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.OverlayEmissiveColor ?? new Color4(0.18f, 0.18f, 0.18f, 1f),
            OverlayCompositorLane.Cosmetic when compositionRuleProfile is not null => compositionRuleProfile.Value.EmissiveColor,
            _ => new Color4(0.18f, 0.18f, 0.18f, 1f)
        };

        Color4? overlaySpecularColor = lane switch
        {
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.OverlaySpecularColor,
            _ => (Color4?)null
        };

        float? overlaySpecularShininess = lane switch
        {
            OverlayCompositorLane.OrdinaryWorn or OverlayCompositorLane.HighLayerWorn => wornLaneProfile?.OverlaySpecularShininess,
            _ => (float?)null
        };

        var useExplicitEmissiveOnly = wornLaneProfile?.UseExplicitEmissiveOnly ?? false;
        var allowsViewportColorEmissiveFallback = wornLaneProfile?.AllowsViewportColorEmissiveFallback ?? lane != OverlayCompositorLane.HighLayerWorn;
        var allowsImplicitAlphaFallback = wornLaneProfile?.AllowsImplicitAlphaFallback ?? blendFamily != OverlayBlendFamily.RestrictedHighLayer;
        var requiresExplicitOpacityForTransparency = wornLaneProfile?.RequiresExplicitOpacityForTransparency ?? blendFamily == OverlayBlendFamily.RestrictedHighLayer;
        var disallowViewportColorAlphaFallback = wornLaneProfile?.DisallowViewportColorAlphaFallback ?? blendFamily == OverlayBlendFamily.RestrictedHighLayer;
        var requiresDedicatedEmissiveMap = wornLaneProfile?.RequiresDedicatedEmissiveMap ?? false;
        return new OverlayCompositorPolicy(
            Lane: lane,
            PassIntent: passIntent,
            BlendFamily: blendFamily,
            PassBucket: passBucket,
            UseGrayscaleComposite: useGrayscaleComposite,
            OverlayOpacityScale: overlayOpacityScale,
            OverlayEmissiveColor: overlayEmissiveColor,
            OverlaySpecularColor: overlaySpecularColor,
            OverlaySpecularShininess: overlaySpecularShininess,
            RequiresExplicitOpacityForTransparency: requiresExplicitOpacityForTransparency,
            DisallowViewportColorAlphaFallback: disallowViewportColorAlphaFallback,
            RequiresDedicatedEmissiveMap: requiresDedicatedEmissiveMap,
            UseExplicitEmissiveOnly: useExplicitEmissiveOnly,
            AllowsViewportColorEmissiveFallback: allowsViewportColorEmissiveFallback,
            AllowsImplicitAlphaFallback: allowsImplicitAlphaFallback);
    }

    private static CanonicalTexture? SelectOverlayEmissiveTexture(
        OverlayCompositorPolicy overlayPolicy,
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> surfaceTextures,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? explicitEmissiveTexture) =>
        overlayPolicy.UseExplicitEmissiveOnly
            ? explicitEmissiveTexture
            : SelectEmissiveTexture(material, surfaceTextures, diffuseTexture, layeredColorTexture);

    private static CanonicalTexture? SelectOverlayViewportColorTexture(
        OverlayCompositorPolicy overlayPolicy,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? emissiveTexture) =>
        overlayPolicy.AllowsViewportColorEmissiveFallback
            ? layeredColorTexture ?? diffuseTexture ?? emissiveTexture
            : layeredColorTexture ?? diffuseTexture;

    private static OverlayExecutionPlan BuildOverlayExecutionPlan(
        OverlayCompositorPolicy overlayPolicy,
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> surfaceTextures,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? explicitEmissiveTexture,
        CanonicalTexture? opacityTexture)
    {
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        var emissiveTexture = SelectOverlayEmissiveTexture(
            overlayPolicy,
            material,
            surfaceTextures,
            diffuseTexture,
            layeredColorTexture,
            explicitEmissiveTexture);
        var viewportColorTexture = SelectOverlayViewportColorTexture(
            overlayPolicy,
            layeredColorTexture,
            diffuseTexture,
            emissiveTexture);
        var renderAlphaMap = ShouldRenderOverlayPolicyTransparency(
            overlayPolicy,
            material,
            compositionRuleProfile,
            opacityTexture,
            viewportColorTexture);
        var alphaSourceTexture = ResolveOverlayAlphaSourceTexture(
            overlayPolicy,
            material,
            compositionRuleProfile,
            opacityTexture,
            viewportColorTexture);
        return new OverlayExecutionPlan(
            Policy: overlayPolicy,
            EmissiveTexture: emissiveTexture,
            ViewportColorTexture: viewportColorTexture,
            AlphaSourceTexture: alphaSourceTexture,
            RenderAlphaMap: renderAlphaMap);
    }

    private static SkintoneOverlayExecutionPlan? BuildSkintoneOverlayExecutionPlan(
        CanonicalMaterial? material,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? layeredColorTexture,
        CanonicalTexture? opacityTexture)
    {
        if (!BuildViewportOverlayStageProfile(material).IsSimSkintoneOverlayStage)
        {
            return null;
        }

        var viewportColorTexture = layeredColorTexture ?? diffuseTexture;
        if (viewportColorTexture is null)
        {
            return null;
        }

        var alphaSourceTexture = opacityTexture ??
            (TextureSupportsAlpha(viewportColorTexture) ? viewportColorTexture : null);
        return new SkintoneOverlayExecutionPlan(
            ViewportColorTexture: viewportColorTexture,
            AlphaSourceTexture: alphaSourceTexture,
            RenderAlphaMap: alphaSourceTexture is not null);
    }

    private static bool TryCreateOverlayExecutionTextureModel(
        OverlayExecutionPlan executionPlan,
        bool forceOpaqueAlpha,
        out TextureModel? textureModel,
        out bool usesComposite)
    {
        textureModel = null;
        usesComposite = false;

        var viewportColorTexture = executionPlan.ViewportColorTexture;
        if (viewportColorTexture is null)
        {
            return false;
        }

        if (executionPlan.Policy.PassIntent is OverlayPassIntent.CosmeticDetail &&
            executionPlan.Policy.BlendFamily == OverlayBlendFamily.GrayscaleDetail)
        {
            var compositedPngBytes = ComposeGrayscaleOverlayPng(
                viewportColorTexture.PngBytes,
                executionPlan.AlphaSourceTexture?.PngBytes);
            if (compositedPngBytes is null)
            {
                return false;
            }

            if (forceOpaqueAlpha)
            {
                compositedPngBytes = ForceOpaquePngAlpha(compositedPngBytes);
            }

            textureModel = new TextureModel(new MemoryStream(compositedPngBytes), autoCloseStream: true);
            usesComposite = true;
            return true;
        }

        if (!IsOverlayExecutionPassIntent(executionPlan.Policy.PassIntent))
        {
            return false;
        }

        textureModel = CreateTextureModel(viewportColorTexture, forceOpaqueAlpha);
        return true;
    }

    private static TextureModel? CreateOverlayExecutionAlphaTextureModel(OverlayExecutionPlan executionPlan)
    {
        if (!executionPlan.RenderAlphaMap)
        {
            return null;
        }

        var sourcePngBytes = executionPlan.AlphaSourceTexture?.PngBytes;
        if (sourcePngBytes is null)
        {
            return null;
        }

        var alphaScale = executionPlan.Policy.OverlayOpacityScale;
        if (Math.Abs(alphaScale - 1f) <= 0.001f)
        {
            return new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true);
        }

        var scaledAlphaPng = ScalePngAlpha(sourcePngBytes, alphaScale);
        return scaledAlphaPng is null
            ? new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true)
            : new TextureModel(new MemoryStream(scaledAlphaPng), autoCloseStream: true);
    }

    private static OverlayMaterialResponsePlan BuildOverlayMaterialResponsePlan(
        OverlayCompositorPolicy overlayPolicy,
        bool isFlat,
        bool renderEmissiveMap,
        TextureModel? textureModel,
        TextureModel? emissiveTextureModel,
        Color4 defaultSpecularColor,
        float defaultSpecularShininess)
    {
        var emissiveColor = overlayPolicy.OverlayEmissiveColor;
        var specularColor = overlayPolicy.OverlaySpecularColor ?? defaultSpecularColor;
        var specularShininess = overlayPolicy.OverlaySpecularShininess ?? defaultSpecularShininess;
        var resolvedRenderEmissiveMap = overlayPolicy.RequiresDedicatedEmissiveMap
            ? emissiveTextureModel is not null
            : renderEmissiveMap;
        var resolvedEmissiveMap = overlayPolicy.RequiresDedicatedEmissiveMap
            ? emissiveTextureModel
            : isFlat
                ? (renderEmissiveMap ? textureModel : null)
                : emissiveTextureModel;

        return new OverlayMaterialResponsePlan(
            EmissiveColor: emissiveColor,
            SpecularColor: specularColor,
            SpecularShininess: specularShininess,
            RenderEmissiveMap: resolvedRenderEmissiveMap,
            EmissiveMap: resolvedEmissiveMap);
    }

    private static OverlayMaterialApplicationPlan? BuildOverlayMaterialApplicationPlan(
        OverlayExecutionPlan? overlayExecutionPlan,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        TextureModel? textureModel,
        TextureModel? alphaTextureModel,
        bool renderDiffuseMap,
        bool renderAlphaMap)
    {
        if (overlayExecutionPlan is not { } executionPlan || overlayMaterialResponsePlan is not { } responsePlan)
        {
            return null;
        }

        var resolvedRenderDiffuseMap = renderDiffuseMap && textureModel is not null;
        var resolvedDiffuseMap = resolvedRenderDiffuseMap ? textureModel : null;
        var resolvedRenderDiffuseAlphaMap = renderAlphaMap && alphaTextureModel is not null;
        var resolvedDiffuseAlphaMap = resolvedRenderDiffuseAlphaMap ? alphaTextureModel : null;
        var resolvedRenderNormalMap = false;
        var resolvedNormalMap = (TextureModel?)null;
        var resolvedRenderSpecularColorMap = false;
        var resolvedSpecularColorMap = (TextureModel?)null;
        var resolvedEnableAutoTangent = false;

        if (!IsOverlayExecutionPassIntent(executionPlan.Policy.PassIntent))
        {
            return null;
        }

        return new OverlayMaterialApplicationPlan(
            RenderDiffuseMap: resolvedRenderDiffuseMap,
            DiffuseMap: resolvedDiffuseMap,
            RenderEmissiveMap: responsePlan.RenderEmissiveMap,
            EmissiveMap: responsePlan.EmissiveMap,
            RenderDiffuseAlphaMap: resolvedRenderDiffuseAlphaMap,
            DiffuseAlphaMap: resolvedDiffuseAlphaMap,
            RenderNormalMap: resolvedRenderNormalMap,
            NormalMap: resolvedNormalMap,
            RenderSpecularColorMap: resolvedRenderSpecularColorMap,
            SpecularColorMap: resolvedSpecularColorMap,
            EnableAutoTangent: resolvedEnableAutoTangent);
    }

    private static ViewportLatePassMaterialPlan? BuildViewportLatePassMaterialPlan(
        OverlayMaterialPassVariant passVariant,
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        string? selectedSlot,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile,
        OverlayExecutionPlan? overlayExecutionPlan,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        TextureModel? textureModel,
        SkintoneOverlayExecutionPlan? skintoneOverlayExecutionPlan)
    {
        if (renderMode is not (SceneRenderMode.LitTexture or SceneRenderMode.FlatTexture) ||
            !string.IsNullOrWhiteSpace(selectedSlot))
        {
            return null;
        }

        var sourceProfile = BuildViewportLatePassSourceProfile(
            passVariant,
            material,
            latePassSceneRelationProfile,
            overlayExecutionPlan,
            overlayMaterialResponsePlan,
            textureModel,
            skintoneOverlayExecutionPlan);
        if (sourceProfile is not { } resolvedSourceProfile)
        {
            return null;
        }

        var policy = BuildViewportLatePassExecutionPolicy(
            material,
            GetViewportLatePassBlendIntent(material, passVariant),
            BuildViewportParityRuleProfile(material, passVariant),
            resolvedSourceProfile.EmissiveColor,
            resolvedSourceProfile.RenderAlphaMap,
            resolvedSourceProfile.HasAlphaSourceTexture,
            resolvedSourceProfile.HasDedicatedOpacityInput,
            resolvedSourceProfile.UsesViewportColorAlphaSource);
        if (policy.ParityBehavior.SuppressLatePassWithoutDedicatedOpacity && !policy.RenderAlphaMap)
        {
            return null;
        }

        var materialPathProfile = BuildViewportLatePassMaterialPathProfile(
            resolvedSourceProfile.SceneRelationProfile,
            policy);
        return new ViewportLatePassMaterialPlan(
            PassVariant: passVariant,
            Policy: policy,
            PathProfile: materialPathProfile,
            SceneRelationProfile: resolvedSourceProfile.SceneRelationProfile,
            TextureMap: resolvedSourceProfile.TextureMap,
            SamplingTexture: resolvedSourceProfile.SamplingTexture,
            AlphaSourcePngBytes: resolvedSourceProfile.AlphaSourcePngBytes);
    }

    private static ViewportLatePassSourceProfile? BuildViewportLatePassSourceProfile(
        OverlayMaterialPassVariant passVariant,
        CanonicalMaterial? material,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile,
        OverlayExecutionPlan? overlayExecutionPlan,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan,
        TextureModel? textureModel,
        SkintoneOverlayExecutionPlan? skintoneOverlayExecutionPlan)
        => passVariant switch
        {
            OverlayMaterialPassVariant.WornEmissiveLate or
            OverlayMaterialPassVariant.HighLayerEmissiveLate => BuildViewportLatePassOverlayEmissiveSourceProfile(
                material,
                latePassSceneRelationProfile,
                overlayExecutionPlan,
                overlayMaterialResponsePlan),
            OverlayMaterialPassVariant.CosmeticDetailLate or
            OverlayMaterialPassVariant.DefaultDetailLate or
            OverlayMaterialPassVariant.OrderedDetailLate or
            OverlayMaterialPassVariant.MakeupPrimaryLate or
            OverlayMaterialPassVariant.GrayscaleLate or
            OverlayMaterialPassVariant.MakeupSecondaryLate => BuildViewportLatePassOverlayDetailSourceProfile(
                material,
                latePassSceneRelationProfile,
                overlayExecutionPlan,
                textureModel),
            OverlayMaterialPassVariant.SkintoneLate => BuildViewportLatePassSkintoneSourceProfile(
                material,
                latePassSceneRelationProfile,
                skintoneOverlayExecutionPlan,
                textureModel),
            _ => null
        };

    private static ViewportLatePassSourceProfile? BuildViewportLatePassOverlayEmissiveSourceProfile(
        CanonicalMaterial? material,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile,
        OverlayExecutionPlan? overlayExecutionPlan,
        OverlayMaterialResponsePlan? overlayMaterialResponsePlan)
    {
        if (overlayExecutionPlan is not { } executionPlan ||
            overlayMaterialResponsePlan is not { } responsePlan ||
            latePassSceneRelationProfile is not { } sceneRelationProfile ||
            !ShouldRenderOverlayLatePass(executionPlan, responsePlan))
        {
            return null;
        }

        var samplingTexture = executionPlan.EmissiveTexture ?? executionPlan.ViewportColorTexture;
        if (samplingTexture is null || responsePlan.EmissiveMap is null)
        {
            return null;
        }

        return new ViewportLatePassSourceProfile(
            SceneRelationProfile: sceneRelationProfile,
            TextureMap: responsePlan.EmissiveMap,
            SamplingTexture: samplingTexture,
            AlphaSourcePngBytes: executionPlan.AlphaSourceTexture?.PngBytes,
            EmissiveColor: responsePlan.EmissiveColor,
            RenderAlphaMap: executionPlan.RenderAlphaMap,
            HasAlphaSourceTexture: executionPlan.AlphaSourceTexture is not null,
            HasDedicatedOpacityInput: HasExplicitOpacityTexture(material),
            UsesViewportColorAlphaSource: ReferenceEquals(executionPlan.AlphaSourceTexture, executionPlan.ViewportColorTexture),
            RuleTag: $"{sceneRelationProfile.RuleTag}|overlay-emissive-source");
    }

    private static ViewportLatePassSourceProfile? BuildViewportLatePassOverlayDetailSourceProfile(
        CanonicalMaterial? material,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile,
        OverlayExecutionPlan? overlayExecutionPlan,
        TextureModel? textureModel)
    {
        if (overlayExecutionPlan is not { } executionPlan ||
            textureModel is null ||
            latePassSceneRelationProfile is not { } sceneRelationProfile ||
            !ShouldRenderOverlayDetailLatePass(executionPlan))
        {
            return null;
        }

        var samplingTexture = executionPlan.ViewportColorTexture;
        if (samplingTexture is null)
        {
            return null;
        }

        return new ViewportLatePassSourceProfile(
            SceneRelationProfile: sceneRelationProfile,
            TextureMap: textureModel,
            SamplingTexture: samplingTexture,
            AlphaSourcePngBytes: executionPlan.AlphaSourceTexture?.PngBytes,
            EmissiveColor: GetOverlayDetailLatePassEmissiveColor(executionPlan.Policy),
            RenderAlphaMap: executionPlan.RenderAlphaMap,
            HasAlphaSourceTexture: executionPlan.AlphaSourceTexture is not null,
            HasDedicatedOpacityInput: HasExplicitOpacityTexture(material),
            UsesViewportColorAlphaSource: ReferenceEquals(executionPlan.AlphaSourceTexture, executionPlan.ViewportColorTexture),
            RuleTag: $"{sceneRelationProfile.RuleTag}|overlay-detail-source");
    }

    private static ViewportLatePassSourceProfile? BuildViewportLatePassSkintoneSourceProfile(
        CanonicalMaterial? material,
        ViewportLatePassSceneRelationProfile? latePassSceneRelationProfile,
        SkintoneOverlayExecutionPlan? skintoneOverlayExecutionPlan,
        TextureModel? textureModel)
    {
        if (!BuildViewportOverlayStageProfile(material).IsSimSkintoneOverlayStage ||
            skintoneOverlayExecutionPlan is not { } executionPlan ||
            latePassSceneRelationProfile is not { } sceneRelationProfile ||
            textureModel is null)
        {
            return null;
        }

        if (executionPlan.ViewportColorTexture is null)
        {
            return null;
        }

        var alphaSourcePngBytes = executionPlan.AlphaSourceTexture?.PngBytes;
        return new ViewportLatePassSourceProfile(
            SceneRelationProfile: sceneRelationProfile,
            TextureMap: textureModel,
            SamplingTexture: executionPlan.ViewportColorTexture,
            AlphaSourcePngBytes: alphaSourcePngBytes,
            EmissiveColor: new Color4(0.1f, 0.1f, 0.1f, 1f),
            RenderAlphaMap: executionPlan.RenderAlphaMap,
            HasAlphaSourceTexture: alphaSourcePngBytes is not null,
            HasDedicatedOpacityInput: HasExplicitOpacityTexture(material) &&
                executionPlan.AlphaSourceTexture is not null &&
                !ReferenceEquals(executionPlan.AlphaSourceTexture, executionPlan.ViewportColorTexture),
            UsesViewportColorAlphaSource: ReferenceEquals(executionPlan.AlphaSourceTexture, executionPlan.ViewportColorTexture),
            RuleTag: $"{sceneRelationProfile.RuleTag}|skintone-source");
    }

    private static PhongMaterial BuildViewportLatePassMaterial(
        CanonicalMaterial? material,
        string? selectedSlot,
        ViewportLatePassMaterialPlan latePassPlan)
    {
        var latePassUvBinding = BuildViewportResolvedUvBinding(material, latePassPlan.SamplingTexture, selectedSlot);
        var latePassUvTransform = BuildUvTransform(latePassUvBinding);
        var stackContract = BuildViewportLatePassStackContract(latePassPlan.Policy, latePassPlan.SceneRelationProfile);
        var latePassAlphaTexture = CreateViewportLatePassAlphaTextureModel(latePassPlan, stackContract.AlphaScaleMultiplier);
        var renderAlphaMap = latePassPlan.Policy.RenderAlphaMap &&
            latePassPlan.SceneRelationProfile.EffectiveTransparencyMode != ViewportLatePassTransparencyMode.NotTransparent &&
            latePassAlphaTexture is not null;
        var effectiveDiffuseColor = renderAlphaMap
            ? stackContract.DiffuseColor
            : SetColorAlpha(stackContract.DiffuseColor, stackContract.OpacityFallbackAlpha);
        var effectiveAmbientColor = renderAlphaMap
            ? stackContract.AmbientColor
            : SetColorAlpha(stackContract.AmbientColor, stackContract.OpacityFallbackAlpha);
        var effectiveEmissiveColor = renderAlphaMap
            ? stackContract.EmissiveColor
            : ScaleColor(stackContract.EmissiveColor, stackContract.EmissiveScaleWithoutAlpha);
        return latePassPlan.PathProfile.Kind switch
        {
            ViewportLatePassMaterialPathKind.SkintoneOverlay => BuildViewportSkintoneLatePassMaterial(
                latePassPlan,
                latePassUvTransform,
                effectiveDiffuseColor,
                effectiveAmbientColor,
                effectiveEmissiveColor,
                latePassAlphaTexture,
                renderAlphaMap),
            ViewportLatePassMaterialPathKind.OrdinaryGrayscaleOverlay => BuildViewportGrayscaleLatePassMaterial(
                latePassPlan,
                latePassUvTransform,
                effectiveDiffuseColor,
                effectiveAmbientColor,
                effectiveEmissiveColor,
                latePassAlphaTexture,
                renderAlphaMap),
            ViewportLatePassMaterialPathKind.SoftEmissiveOverlay => BuildViewportSoftEmissiveLatePassMaterial(
                latePassPlan,
                latePassUvTransform,
                effectiveDiffuseColor,
                effectiveAmbientColor,
                effectiveEmissiveColor,
                latePassAlphaTexture,
                renderAlphaMap),
            ViewportLatePassMaterialPathKind.HighLayerEmissiveOverlay => BuildViewportHighLayerEmissiveLatePassMaterial(
                latePassPlan,
                latePassUvTransform,
                effectiveDiffuseColor,
                effectiveAmbientColor,
                effectiveEmissiveColor,
                latePassAlphaTexture,
                renderAlphaMap),
            _ => BuildViewportOrdinaryDiffuseLatePassMaterial(
                latePassPlan,
                latePassUvTransform,
                effectiveDiffuseColor,
                effectiveAmbientColor,
                effectiveEmissiveColor,
                latePassAlphaTexture,
                renderAlphaMap)
        };
    }

    private static ViewportLatePassMaterialPathProfile BuildViewportLatePassMaterialPathProfile(
        ViewportLatePassSceneRelationProfile sceneRelationProfile,
        ViewportLatePassExecutionPolicy policy)
        => sceneRelationProfile.FamilyContract.Family switch
        {
            ViewportLatePassFamily.Skintone => new ViewportLatePassMaterialPathProfile(
                Kind: ViewportLatePassMaterialPathKind.SkintoneOverlay,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                RuleTag: $"skintone-late-material-path|{sceneRelationProfile.RuleTag}"),
            ViewportLatePassFamily.EmissiveSoft => new ViewportLatePassMaterialPathProfile(
                Kind: ViewportLatePassMaterialPathKind.SoftEmissiveOverlay,
                MaterialMode: ViewportLatePassMaterialMode.EmissiveOnly,
                RuleTag: $"soft-emissive-late-material-path|{sceneRelationProfile.RuleTag}"),
            ViewportLatePassFamily.EmissiveRestricted => new ViewportLatePassMaterialPathProfile(
                Kind: ViewportLatePassMaterialPathKind.HighLayerEmissiveOverlay,
                MaterialMode: ViewportLatePassMaterialMode.EmissiveOnly,
                RuleTag: $"highlayer-emissive-late-material-path|{sceneRelationProfile.RuleTag}"),
            _ when policy.MaterialMode == ViewportLatePassMaterialMode.DiffuseGrayscale => new ViewportLatePassMaterialPathProfile(
                Kind: ViewportLatePassMaterialPathKind.OrdinaryGrayscaleOverlay,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseGrayscale,
                RuleTag: $"ordinary-grayscale-late-material-path|{sceneRelationProfile.RuleTag}"),
            _ => new ViewportLatePassMaterialPathProfile(
                Kind: ViewportLatePassMaterialPathKind.OrdinaryDiffuseOverlay,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                RuleTag: $"ordinary-diffuse-late-material-path|{sceneRelationProfile.RuleTag}")
        };

    private static PhongMaterial BuildViewportSkintoneLatePassMaterial(
        ViewportLatePassMaterialPlan latePassPlan,
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        TextureModel? latePassAlphaTexture,
        bool renderAlphaMap)
    {
        var materialBindingProfile = BuildViewportSkintoneLateMaterialBindingProfile(
            latePassPlan.TextureMap,
            latePassAlphaTexture,
            renderAlphaMap);
        return BuildViewportLatePassMaterialCore(
            latePassUvTransform,
            effectiveDiffuseColor,
            effectiveAmbientColor,
            effectiveEmissiveColor,
            materialBindingProfile);
    }

    private static PhongMaterial BuildViewportOrdinaryDiffuseLatePassMaterial(
        ViewportLatePassMaterialPlan latePassPlan,
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        TextureModel? latePassAlphaTexture,
        bool renderAlphaMap)
    {
        var materialBindingProfile = BuildViewportOrdinaryDiffuseLateMaterialBindingProfile(
            latePassPlan.TextureMap,
            latePassAlphaTexture,
            renderAlphaMap);
        return BuildViewportLatePassMaterialCore(
            latePassUvTransform,
            effectiveDiffuseColor,
            effectiveAmbientColor,
            effectiveEmissiveColor,
            materialBindingProfile);
    }

    private static PhongMaterial BuildViewportGrayscaleLatePassMaterial(
        ViewportLatePassMaterialPlan latePassPlan,
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        TextureModel? latePassAlphaTexture,
        bool renderAlphaMap)
    {
        var materialBindingProfile = BuildViewportGrayscaleLateMaterialBindingProfile(
            latePassPlan.TextureMap,
            latePassAlphaTexture,
            renderAlphaMap);
        return BuildViewportLatePassMaterialCore(
            latePassUvTransform,
            effectiveDiffuseColor,
            effectiveAmbientColor,
            effectiveEmissiveColor,
            materialBindingProfile);
    }

    private static PhongMaterial BuildViewportSoftEmissiveLatePassMaterial(
        ViewportLatePassMaterialPlan latePassPlan,
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        TextureModel? latePassAlphaTexture,
        bool renderAlphaMap)
    {
        var materialBindingProfile = BuildViewportSoftEmissiveLateMaterialBindingProfile(
            latePassPlan.TextureMap,
            latePassAlphaTexture,
            renderAlphaMap);
        return BuildViewportLatePassMaterialCore(
            latePassUvTransform,
            effectiveDiffuseColor,
            effectiveAmbientColor,
            effectiveEmissiveColor,
            materialBindingProfile);
    }

    private static PhongMaterial BuildViewportHighLayerEmissiveLatePassMaterial(
        ViewportLatePassMaterialPlan latePassPlan,
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        TextureModel? latePassAlphaTexture,
        bool renderAlphaMap)
    {
        var materialBindingProfile = BuildViewportHighLayerEmissiveLateMaterialBindingProfile(
            latePassPlan.TextureMap,
            latePassAlphaTexture,
            renderAlphaMap);
        return BuildViewportLatePassMaterialCore(
            latePassUvTransform,
            effectiveDiffuseColor,
            effectiveAmbientColor,
            effectiveEmissiveColor,
            materialBindingProfile);
    }

    private static PhongMaterial BuildViewportLatePassMaterialCore(
        UVTransform latePassUvTransform,
        Color4 effectiveDiffuseColor,
        Color4 effectiveAmbientColor,
        Color4 effectiveEmissiveColor,
        ViewportLatePassMaterialBindingProfile materialBindingProfile) =>
        new()
        {
            DiffuseColor = effectiveDiffuseColor,
            AmbientColor = effectiveAmbientColor,
            EmissiveColor = effectiveEmissiveColor,
            SpecularColor = new Color4(0f, 0f, 0f, 1f),
            SpecularShininess = 0f,
            RenderDiffuseMap = materialBindingProfile.RenderDiffuseMap,
            DiffuseMap = materialBindingProfile.DiffuseMap,
            RenderEmissiveMap = materialBindingProfile.RenderEmissiveMap,
            EmissiveMap = materialBindingProfile.EmissiveMap,
            RenderDiffuseAlphaMap = materialBindingProfile.RenderDiffuseAlphaMap,
            DiffuseAlphaMap = materialBindingProfile.DiffuseAlphaMap,
            RenderNormalMap = false,
            NormalMap = null,
            RenderSpecularColorMap = false,
            SpecularColorMap = null,
            EnableAutoTangent = false,
            RenderShadowMap = false,
            UVTransform = latePassUvTransform
        };

    private static ViewportLatePassMaterialBindingProfile BuildViewportSkintoneLateMaterialBindingProfile(
        TextureModel? textureMap,
        TextureModel? alphaTextureMap,
        bool renderAlphaMap)
        => new(
            RenderDiffuseMap: textureMap is not null,
            DiffuseMap: textureMap,
            RenderEmissiveMap: false,
            EmissiveMap: null,
            RenderDiffuseAlphaMap: renderAlphaMap,
            DiffuseAlphaMap: renderAlphaMap ? alphaTextureMap : null,
            RuleTag: "skintone-late-material-binding");

    private static ViewportLatePassMaterialBindingProfile BuildViewportOrdinaryDiffuseLateMaterialBindingProfile(
        TextureModel? textureMap,
        TextureModel? alphaTextureMap,
        bool renderAlphaMap)
        => new(
            RenderDiffuseMap: textureMap is not null,
            DiffuseMap: textureMap,
            RenderEmissiveMap: false,
            EmissiveMap: null,
            RenderDiffuseAlphaMap: renderAlphaMap,
            DiffuseAlphaMap: renderAlphaMap ? alphaTextureMap : null,
            RuleTag: "ordinary-diffuse-late-material-binding");

    private static ViewportLatePassMaterialBindingProfile BuildViewportGrayscaleLateMaterialBindingProfile(
        TextureModel? textureMap,
        TextureModel? alphaTextureMap,
        bool renderAlphaMap)
        => new(
            RenderDiffuseMap: textureMap is not null,
            DiffuseMap: textureMap,
            RenderEmissiveMap: false,
            EmissiveMap: null,
            RenderDiffuseAlphaMap: renderAlphaMap,
            DiffuseAlphaMap: renderAlphaMap ? alphaTextureMap : null,
            RuleTag: "ordinary-grayscale-late-material-binding");

    private static ViewportLatePassMaterialBindingProfile BuildViewportSoftEmissiveLateMaterialBindingProfile(
        TextureModel? textureMap,
        TextureModel? alphaTextureMap,
        bool renderAlphaMap)
        => new(
            RenderDiffuseMap: false,
            DiffuseMap: null,
            RenderEmissiveMap: textureMap is not null,
            EmissiveMap: textureMap,
            RenderDiffuseAlphaMap: renderAlphaMap,
            DiffuseAlphaMap: renderAlphaMap ? alphaTextureMap : null,
            RuleTag: "soft-emissive-late-material-binding");

    private static ViewportLatePassMaterialBindingProfile BuildViewportHighLayerEmissiveLateMaterialBindingProfile(
        TextureModel? textureMap,
        TextureModel? alphaTextureMap,
        bool renderAlphaMap)
        => new(
            RenderDiffuseMap: false,
            DiffuseMap: null,
            RenderEmissiveMap: textureMap is not null,
            EmissiveMap: textureMap,
            RenderDiffuseAlphaMap: renderAlphaMap,
            DiffuseAlphaMap: renderAlphaMap ? alphaTextureMap : null,
            RuleTag: "highlayer-emissive-late-material-binding");

    private static ViewportLatePassStackContract BuildViewportLatePassStackContract(
        ViewportLatePassExecutionPolicy policy,
        ViewportLatePassSceneRelationProfile sceneRelationProfile)
    {
        var stackDefaultsProfile = BuildViewportLatePassStackDefaultsProfile(
            policy.ParityBehavior.BlendFamily,
            policy.MaterialMode);
        var baseContract = new ViewportLatePassStackContract(
            MaterialMode: stackDefaultsProfile.MaterialMode,
            DiffuseColor: stackDefaultsProfile.DiffuseColor,
            AmbientColor: stackDefaultsProfile.AmbientColor,
            EmissiveColor: stackDefaultsProfile.EmissiveUsesPolicyColor
                ? policy.EmissiveColor
                : ScaleColor(policy.EmissiveColor, stackDefaultsProfile.EmissiveColorScale),
            AlphaScaleMultiplier: stackDefaultsProfile.AlphaScaleMultiplier,
            OpacityFallbackAlpha: stackDefaultsProfile.OpacityFallbackAlpha,
            EmissiveScaleWithoutAlpha: stackDefaultsProfile.EmissiveScaleWithoutAlpha,
            ContractTag: stackDefaultsProfile.ContractTag);

        var effectiveEmissiveColor = ScaleColor(baseContract.EmissiveColor, sceneRelationProfile.EmissiveScaleMultiplier);
        return baseContract with
        {
            EmissiveColor = effectiveEmissiveColor,
            AlphaScaleMultiplier = baseContract.AlphaScaleMultiplier * sceneRelationProfile.AlphaScaleMultiplier,
            OpacityFallbackAlpha = Math.Clamp(baseContract.OpacityFallbackAlpha * Math.Max(sceneRelationProfile.AlphaScaleMultiplier, 0.25f), 0f, 1f),
            EmissiveScaleWithoutAlpha = baseContract.EmissiveScaleWithoutAlpha * sceneRelationProfile.EmissiveScaleMultiplier,
            ContractTag = $"{baseContract.ContractTag}|{sceneRelationProfile.RuleTag}"
        };
    }

    private static ViewportLatePassStackDefaultsProfile BuildViewportLatePassStackDefaultsProfile(
        ViewportParityBlendFamily blendFamily,
        ViewportLatePassMaterialMode materialMode) =>
        blendFamily switch
        {
            ViewportParityBlendFamily.SkintoneOverlay => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                DiffuseColor: new Color4(0.9f, 0.9f, 0.9f, 1f),
                AmbientColor: new Color4(0.86f, 0.86f, 0.86f, 1f),
                EmissiveUsesPolicyColor: false,
                EmissiveColorScale: 0.72f,
                AlphaScaleMultiplier: 0.94f,
                OpacityFallbackAlpha: 0.52f,
                EmissiveScaleWithoutAlpha: 0.72f,
                ContractTag: "skintone-overlay-stack"),
            ViewportParityBlendFamily.MakeupOpacityPrimary => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                DiffuseColor: new Color4(0.98f, 0.98f, 0.98f, 1f),
                AmbientColor: new Color4(0.94f, 0.94f, 0.94f, 1f),
                EmissiveUsesPolicyColor: false,
                EmissiveColorScale: 0.64f,
                AlphaScaleMultiplier: 1f,
                OpacityFallbackAlpha: 0.44f,
                EmissiveScaleWithoutAlpha: 0.62f,
                ContractTag: "makeup-primary-stack"),
            ViewportParityBlendFamily.MakeupOpacitySecondary => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                DiffuseColor: new Color4(1f, 1f, 1f, 1f),
                AmbientColor: new Color4(0.96f, 0.96f, 0.96f, 1f),
                EmissiveUsesPolicyColor: false,
                EmissiveColorScale: 0.68f,
                AlphaScaleMultiplier: 1.04f,
                OpacityFallbackAlpha: 0.48f,
                EmissiveScaleWithoutAlpha: 0.64f,
                ContractTag: "makeup-secondary-stack"),
            ViewportParityBlendFamily.GrayscaleOverlay => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: ViewportLatePassMaterialMode.DiffuseGrayscale,
                DiffuseColor: new Color4(0.88f, 0.88f, 0.88f, 1f),
                AmbientColor: new Color4(0.84f, 0.84f, 0.84f, 1f),
                EmissiveUsesPolicyColor: false,
                EmissiveColorScale: 0.8f,
                AlphaScaleMultiplier: 0.98f,
                OpacityFallbackAlpha: 0.56f,
                EmissiveScaleWithoutAlpha: 0.74f,
                ContractTag: "grayscale-overlay-stack"),
            _ when materialMode == ViewportLatePassMaterialMode.EmissiveOnly => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: ViewportLatePassMaterialMode.EmissiveOnly,
                DiffuseColor: new Color4(0f, 0f, 0f, 1f),
                AmbientColor: new Color4(0f, 0f, 0f, 1f),
                EmissiveUsesPolicyColor: true,
                EmissiveColorScale: 1f,
                AlphaScaleMultiplier: 1f,
                OpacityFallbackAlpha: 1f,
                EmissiveScaleWithoutAlpha: 0.72f,
                ContractTag: "emissive-stack"),
            _ => new ViewportLatePassStackDefaultsProfile(
                MaterialMode: materialMode,
                DiffuseColor: new Color4(0.96f, 0.96f, 0.96f, 1f),
                AmbientColor: new Color4(0.92f, 0.92f, 0.92f, 1f),
                EmissiveUsesPolicyColor: false,
                EmissiveColorScale: 0.74f,
                AlphaScaleMultiplier: 1f,
                OpacityFallbackAlpha: 0.5f,
                EmissiveScaleWithoutAlpha: 0.7f,
                ContractTag: "generic-overlay-stack")
        };

    private static Color4 ScaleColor(Color4 color, float factor) =>
        new(
            Math.Clamp(color.Red * factor, 0f, 1f),
            Math.Clamp(color.Green * factor, 0f, 1f),
            Math.Clamp(color.Blue * factor, 0f, 1f),
            color.Alpha);

    private static Color4 SetColorAlpha(Color4 color, float alpha) =>
        new(color.Red, color.Green, color.Blue, Math.Clamp(alpha, 0f, 1f));

    private static bool ShouldRenderOverlayLatePass(
        OverlayExecutionPlan executionPlan,
        OverlayMaterialResponsePlan responsePlan)
    {
        if (responsePlan.EmissiveMap is null)
        {
            return false;
        }

        if (ReferenceEquals(executionPlan.EmissiveTexture, executionPlan.ViewportColorTexture))
        {
            return false;
        }

        return IsOverlayLateEmissivePassIntent(executionPlan.Policy.PassIntent);
    }

    private static ViewportLatePassBlendIntent GetViewportLatePassBlendIntent(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        if (passVariant == OverlayMaterialPassVariant.SkintoneLate)
        {
            if (!BuildViewportOverlayStageProfile(material).IsSimSkintoneOverlayStage)
            {
                return ViewportLatePassBlendIntent.None;
            }

            return HasExplicitOpacityTexture(material)
                ? ViewportLatePassBlendIntent.SkintoneMasked
                : ViewportLatePassBlendIntent.SkintoneSoft;
        }

        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        if (compositionRuleProfile is { } ruleProfile &&
            passVariant == ruleProfile.PassVariant)
        {
            return ruleProfile.BlendIntent;
        }

        var overlayPolicy = BuildOverlayCompositorPolicy(material);
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        var ordinaryDetailBlendIntent = ResolveViewportOrdinaryDetailBlendIntent(passVariant, overlayPolicy, material);
        if (ordinaryDetailBlendIntent != ViewportLatePassBlendIntent.None)
        {
            return ordinaryDetailBlendIntent;
        }

        if (wornLaneProfile is { } resolvedWornLaneProfile &&
            passVariant == resolvedWornLaneProfile.PassVariant)
        {
            return resolvedWornLaneProfile.BlendIntent;
        }

        return ViewportLatePassBlendIntent.None;
    }

    private static ViewportLatePassBlendIntent ResolveViewportOrdinaryDetailBlendIntent(
        OverlayMaterialPassVariant passVariant,
        OverlayCompositorPolicy overlayPolicy,
        CanonicalMaterial? material)
    {
        if (!BuildViewportOverlayStageProfile(material).IsCasOverlayDetailStage)
        {
            return ViewportLatePassBlendIntent.None;
        }

        return (passVariant, overlayPolicy.PassIntent, overlayPolicy.BlendFamily) switch
        {
            (OverlayMaterialPassVariant.CosmeticDetailLate, OverlayPassIntent.CosmeticDetail, OverlayBlendFamily.GrayscaleDetail) => ViewportLatePassBlendIntent.DetailMasked,
            (OverlayMaterialPassVariant.CosmeticDetailLate, OverlayPassIntent.CosmeticDetail, _) => ViewportLatePassBlendIntent.DetailSoft,
            (OverlayMaterialPassVariant.DefaultDetailLate, OverlayPassIntent.DefaultDetail, _) => ViewportLatePassBlendIntent.DetailDefault,
            (OverlayMaterialPassVariant.OrderedDetailLate, OverlayPassIntent.OrderedDetail, _) => ViewportLatePassBlendIntent.OrderedSoft,
            _ => ViewportLatePassBlendIntent.None
        };
    }

    private static int GetViewportLatePassDefaultPhase(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent) =>
        BuildViewportLateFallbackDefaultsProfile(material, blendIntent)?.PassPhase ??
        BuildViewportLateEmissiveFamilyProfile(blendIntent)?.PassPhase ??
        0;

    private static int GetViewportParityPassPhase(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile)
    {
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        if (compositionRuleProfile is { } ruleProfile &&
            parityProfile.BlendFamily == ruleProfile.BlendFamily)
        {
            return ruleProfile.PassPhase;
        }

        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        if (wornLaneProfile is { } resolvedWornLaneProfile &&
            blendIntent == resolvedWornLaneProfile.BlendIntent)
        {
            return resolvedWornLaneProfile.PassPhase;
        }

        return parityProfile.BlendFamily switch
        {
            ViewportParityBlendFamily.SkintoneOverlay => 1,
            _ => GetViewportLatePassDefaultPhase(material, blendIntent)
        };
    }

    private static ViewportParityBehaviorMatrixEntry BuildViewportParityBehaviorMatrixEntry(
        CanonicalMaterial? material,
        ViewportParityRuleProfile parityProfile,
        ViewportLatePassBlendIntent blendIntent,
        Color4 defaultEmissiveColor)
    {
        var defaultPhase = GetViewportParityPassPhase(material, blendIntent, parityProfile);
        return TryBuildViewportCompositionParityBehaviorEntry(material, parityProfile, defaultPhase) ??
            TryBuildViewportWornParityBehaviorEntry(material, blendIntent, defaultPhase, defaultEmissiveColor) ??
            TryBuildViewportEmissiveParityBehaviorEntry(blendIntent, defaultPhase, parityProfile, defaultEmissiveColor) ??
            BuildViewportFallbackParityBehaviorEntry(material, blendIntent, parityProfile, defaultPhase, defaultEmissiveColor);
    }

    private static ViewportParityBehaviorMatrixEntry? TryBuildViewportCompositionParityBehaviorEntry(
        CanonicalMaterial? material,
        ViewportParityRuleProfile parityProfile,
        int defaultPhase)
    {
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        if (compositionRuleProfile is not { } ruleProfile ||
            parityProfile.BlendFamily != ruleProfile.BlendFamily)
        {
            return null;
        }

        return new ViewportParityBehaviorMatrixEntry(
            ruleProfile.BlendFamily,
            defaultPhase,
            ruleProfile.StackOrder,
            ruleProfile.MaterialMode,
            ruleProfile.AlphaScale,
            ruleProfile.EmissiveColor,
            RequiresDedicatedOpacityInput: ruleProfile.RequiresDedicatedOpacityInput,
            RequiresExplicitOpacityForRenderAlpha: false,
            DisallowViewportColorAlphaSource: false,
            AllowsImplicitAlphaFallback: ruleProfile.AllowsImplicitAlphaFallback,
            SuppressLatePassWithoutDedicatedOpacity: ruleProfile.SuppressLatePassWithoutDedicatedOpacity,
            RuleTag: ruleProfile.RuleTag);
    }

    private static ViewportParityBehaviorMatrixEntry? TryBuildViewportWornParityBehaviorEntry(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        int defaultPhase,
        Color4 defaultEmissiveColor)
    {
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        if (wornLaneProfile is not { } resolvedWornLaneProfile ||
            blendIntent != resolvedWornLaneProfile.BlendIntent)
        {
            return null;
        }

        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(blendIntent);
        return new ViewportParityBehaviorMatrixEntry(
            ViewportParityBlendFamily.GenericOverlay,
            defaultPhase,
            resolvedWornLaneProfile.ParityOrder,
            resolvedWornLaneProfile.MaterialMode,
            resolvedWornLaneProfile.AlphaScale,
            resolvedWornLaneProfile.OverlayEmissiveColor,
            RequiresDedicatedOpacityInput: !resolvedWornLaneProfile.AllowsImplicitAlphaFallback,
            RequiresExplicitOpacityForRenderAlpha: emissiveFamilyProfile?.RequiresExplicitOpacityForRenderAlpha ?? false,
            DisallowViewportColorAlphaSource: emissiveFamilyProfile?.DisallowViewportColorAlphaSource ?? false,
            AllowsImplicitAlphaFallback: resolvedWornLaneProfile.AllowsImplicitAlphaFallback,
            SuppressLatePassWithoutDedicatedOpacity: false,
            RuleTag: resolvedWornLaneProfile.RuleTag);
    }

    private static ViewportParityBehaviorMatrixEntry? TryBuildViewportEmissiveParityBehaviorEntry(
        ViewportLatePassBlendIntent blendIntent,
        int defaultPhase,
        ViewportParityRuleProfile parityProfile,
        Color4 defaultEmissiveColor)
    {
        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(blendIntent);
        if (emissiveFamilyProfile is not { } resolvedEmissiveFamilyProfile)
        {
            return null;
        }

        return new ViewportParityBehaviorMatrixEntry(
            ViewportParityBlendFamily.GenericOverlay,
            defaultPhase,
            resolvedEmissiveFamilyProfile.BaseStackOrder,
            resolvedEmissiveFamilyProfile.MaterialMode,
            resolvedEmissiveFamilyProfile.AlphaScale,
            ScaleColor(defaultEmissiveColor, resolvedEmissiveFamilyProfile.EmissiveColorScale),
            RequiresDedicatedOpacityInput: false,
            RequiresExplicitOpacityForRenderAlpha: resolvedEmissiveFamilyProfile.RequiresExplicitOpacityForRenderAlpha,
            DisallowViewportColorAlphaSource: resolvedEmissiveFamilyProfile.DisallowViewportColorAlphaSource,
            AllowsImplicitAlphaFallback: resolvedEmissiveFamilyProfile.AllowsImplicitAlphaFallback,
            SuppressLatePassWithoutDedicatedOpacity: false,
            RuleTag: $"{resolvedEmissiveFamilyProfile.FamilyTag}|{parityProfile.RuleTag}");
    }

    private static ViewportParityBehaviorMatrixEntry BuildViewportFallbackParityBehaviorEntry(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile,
        int defaultPhase,
        Color4 defaultEmissiveColor)
    {
        var fallbackBehaviorProfile = BuildViewportFallbackParityBehaviorProfile(
            material,
            blendIntent,
            parityProfile,
            defaultEmissiveColor);
        return new ViewportParityBehaviorMatrixEntry(
            fallbackBehaviorProfile.BlendFamily,
            defaultPhase,
            fallbackBehaviorProfile.StackOrder,
            fallbackBehaviorProfile.MaterialMode,
            fallbackBehaviorProfile.AlphaScale,
            fallbackBehaviorProfile.EmissiveColor,
            fallbackBehaviorProfile.RequiresDedicatedOpacityInput,
            fallbackBehaviorProfile.RequiresExplicitOpacityForRenderAlpha,
            fallbackBehaviorProfile.DisallowViewportColorAlphaSource,
            fallbackBehaviorProfile.AllowsImplicitAlphaFallback,
            fallbackBehaviorProfile.SuppressLatePassWithoutDedicatedOpacity,
            fallbackBehaviorProfile.RuleTag);
    }

    private static ViewportFallbackParityBehaviorProfile BuildViewportFallbackParityBehaviorProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile,
        Color4 defaultEmissiveColor)
    {
        var fallbackPassVariant = GetViewportSecondaryPassVariantForParity(blendIntent, parityProfile);
        return BuildViewportLateFallbackBehaviorDefaultsProfile(
            material,
            blendIntent,
            parityProfile,
            fallbackPassVariant,
            defaultEmissiveColor);
    }

    private static ViewportLateFallbackOrderingProfile? BuildViewportLateFallbackOrderingProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent)
    {
        var fallbackDefaultsProfile = BuildViewportLateFallbackDefaultsProfile(material, blendIntent);
        return fallbackDefaultsProfile is { } resolvedFallbackDefaultsProfile
            ? new ViewportLateFallbackOrderingProfile(
                StackOrder: resolvedFallbackDefaultsProfile.StackOrder,
                SortLayerTieBreaker: resolvedFallbackDefaultsProfile.SortLayerTieBreaker,
                CompositionTieBreaker: resolvedFallbackDefaultsProfile.CompositionTieBreaker,
                RuleTag: resolvedFallbackDefaultsProfile.RuleTag)
            : null;
    }

    private static ViewportFallbackParityBehaviorProfile BuildViewportLateFallbackBehaviorDefaultsProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile,
        OverlayMaterialPassVariant passVariant,
        Color4 defaultEmissiveColor)
    {
        var fallbackDefaultsProfile = BuildViewportLateFallbackDefaultsProfile(material, blendIntent);
        if (fallbackDefaultsProfile is { } resolvedFallbackDefaultsProfile)
        {
            return new ViewportFallbackParityBehaviorProfile(
                BlendFamily: resolvedFallbackDefaultsProfile.BlendFamily,
                StackOrder: resolvedFallbackDefaultsProfile.StackOrder,
                MaterialMode: resolvedFallbackDefaultsProfile.MaterialMode,
                AlphaScale: resolvedFallbackDefaultsProfile.AlphaScale,
                EmissiveColor: resolvedFallbackDefaultsProfile.EmissiveColor,
                RequiresDedicatedOpacityInput: resolvedFallbackDefaultsProfile.RequiresDedicatedOpacityInput,
                RequiresExplicitOpacityForRenderAlpha: resolvedFallbackDefaultsProfile.RequiresExplicitOpacityForRenderAlpha,
                DisallowViewportColorAlphaSource: resolvedFallbackDefaultsProfile.DisallowViewportColorAlphaSource,
                AllowsImplicitAlphaFallback: resolvedFallbackDefaultsProfile.AllowsImplicitAlphaFallback,
                SuppressLatePassWithoutDedicatedOpacity: resolvedFallbackDefaultsProfile.SuppressLatePassWithoutDedicatedOpacity,
                RuleTag: resolvedFallbackDefaultsProfile.RuleTag);
        }

        var genericOverlayFallbackDefaultsProfile = BuildViewportGenericOverlayFallbackParityDefaultsProfile();
        var genericOverlayFallbackOrderingDefaultsProfile = BuildViewportGenericOverlayFallbackOrderingDefaultsProfile(
            material,
            passVariant,
            parityProfile);
        return new ViewportFallbackParityBehaviorProfile(
            BlendFamily: genericOverlayFallbackDefaultsProfile.BlendFamily,
            StackOrder: genericOverlayFallbackOrderingDefaultsProfile.StackOrder,
            MaterialMode: genericOverlayFallbackDefaultsProfile.MaterialMode,
            AlphaScale: genericOverlayFallbackDefaultsProfile.AlphaScale,
            EmissiveColor: defaultEmissiveColor,
            RequiresDedicatedOpacityInput: genericOverlayFallbackDefaultsProfile.RequiresDedicatedOpacityInput,
            RequiresExplicitOpacityForRenderAlpha: genericOverlayFallbackDefaultsProfile.RequiresExplicitOpacityForRenderAlpha,
            DisallowViewportColorAlphaSource: genericOverlayFallbackDefaultsProfile.DisallowViewportColorAlphaSource,
            AllowsImplicitAlphaFallback: genericOverlayFallbackDefaultsProfile.AllowsImplicitAlphaFallback,
            SuppressLatePassWithoutDedicatedOpacity: genericOverlayFallbackDefaultsProfile.SuppressLatePassWithoutDedicatedOpacity,
            RuleTag: genericOverlayFallbackDefaultsProfile.RuleTag);
    }

    private static ViewportGenericOverlayFallbackOrderingDefaultsProfile BuildViewportGenericOverlayFallbackOrderingDefaultsProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant,
        ViewportParityRuleProfile parityProfile)
    {
        var overlayLateOrderingProfile = BuildViewportOverlayLateOrderingProfile(material, passVariant);
        return new ViewportGenericOverlayFallbackOrderingDefaultsProfile(
            StackOrder: overlayLateOrderingProfile?.ParityOrder ??
                (parityProfile.CompositionOrder > 0 ? parityProfile.CompositionOrder : 100),
            RuleTag: overlayLateOrderingProfile?.RuleTag ?? "generic-overlay-fallback-order");
    }

    private static ViewportGenericOverlayFallbackParityDefaultsProfile BuildViewportGenericOverlayFallbackParityDefaultsProfile() =>
        new(
            BlendFamily: ViewportParityBlendFamily.GenericOverlay,
            MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
            AlphaScale: 1f,
            RequiresDedicatedOpacityInput: false,
            RequiresExplicitOpacityForRenderAlpha: false,
            DisallowViewportColorAlphaSource: false,
            AllowsImplicitAlphaFallback: true,
            SuppressLatePassWithoutDedicatedOpacity: false,
            RuleTag: "generic-overlay-fallback");

    private static ViewportLateFallbackDefaultsProfile? BuildViewportLateFallbackDefaultsProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent)
    {
        var skintoneFallbackDefaultsProfile = BuildViewportSkintoneLateFallbackDefaultsProfile(blendIntent);
        if (skintoneFallbackDefaultsProfile is { } resolvedSkintoneFallbackDefaultsProfile)
        {
            return new ViewportLateFallbackDefaultsProfile(
                Family: ViewportLatePassFamily.Skintone,
                BlendFamily: ViewportParityBlendFamily.SkintoneOverlay,
                StackOrder: resolvedSkintoneFallbackDefaultsProfile.StackOrder,
                PassPhase: resolvedSkintoneFallbackDefaultsProfile.PassPhase,
                MaterialMode: resolvedSkintoneFallbackDefaultsProfile.MaterialMode,
                AlphaScale: resolvedSkintoneFallbackDefaultsProfile.AlphaScale,
                EmissiveColor: resolvedSkintoneFallbackDefaultsProfile.EmissiveColor,
                RequiresDedicatedOpacityInput: resolvedSkintoneFallbackDefaultsProfile.RequiresDedicatedOpacityInput,
                RequiresExplicitOpacityForRenderAlpha: resolvedSkintoneFallbackDefaultsProfile.RequiresExplicitOpacityForRenderAlpha,
                DisallowViewportColorAlphaSource: resolvedSkintoneFallbackDefaultsProfile.DisallowViewportColorAlphaSource,
                AllowsImplicitAlphaFallback: resolvedSkintoneFallbackDefaultsProfile.AllowsImplicitAlphaFallback,
                SuppressLatePassWithoutDedicatedOpacity: resolvedSkintoneFallbackDefaultsProfile.SuppressLatePassWithoutDedicatedOpacity,
                UnderlayProfile: resolvedSkintoneFallbackDefaultsProfile.UnderlayProfile,
                TransparencyMode: resolvedSkintoneFallbackDefaultsProfile.TransparencyMode,
                SortLayerTieBreaker: resolvedSkintoneFallbackDefaultsProfile.SortLayerTieBreaker,
                CompositionTieBreaker: resolvedSkintoneFallbackDefaultsProfile.CompositionTieBreaker,
                RuleTag: resolvedSkintoneFallbackDefaultsProfile.RuleTag,
                RelationTag: resolvedSkintoneFallbackDefaultsProfile.RelationTag);
        }

        var genericDetailLateProfile = BuildViewportGenericDetailLateProfile(material, blendIntent);
        if (genericDetailLateProfile is not { } resolvedGenericDetailLateProfile)
        {
            return null;
        }

        return BuildViewportGenericDetailLateFallbackDefaultsProfile(
            material,
            blendIntent,
            resolvedGenericDetailLateProfile);
    }

    private static ViewportLateFallbackDefaultsProfile? BuildViewportSkintoneLateFallbackDefaultsProfile(
        ViewportLatePassBlendIntent blendIntent) =>
        blendIntent switch
        {
            ViewportLatePassBlendIntent.SkintoneSoft => new ViewportLateFallbackDefaultsProfile(
                Family: ViewportLatePassFamily.Skintone,
                BlendFamily: ViewportParityBlendFamily.SkintoneOverlay,
                StackOrder: 10,
                PassPhase: 1,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                AlphaScale: 0.72f,
                EmissiveColor: new Color4(0.1f, 0.1f, 0.1f, 1f),
                RequiresDedicatedOpacityInput: true,
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: false,
                AllowsImplicitAlphaFallback: false,
                SuppressLatePassWithoutDedicatedOpacity: false,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(true, ["sim-skintone-base"], "requires-skintone-underlay"),
                TransparencyMode: ViewportLatePassTransparencyMode.AlwaysTransparent,
                SortLayerTieBreaker: 0,
                CompositionTieBreaker: 0,
                RuleTag: "skintone-overlay-soft",
                RelationTag: "skintone-before-cas"),
            ViewportLatePassBlendIntent.SkintoneMasked => new ViewportLateFallbackDefaultsProfile(
                Family: ViewportLatePassFamily.Skintone,
                BlendFamily: ViewportParityBlendFamily.SkintoneOverlay,
                StackOrder: 10,
                PassPhase: 1,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                AlphaScale: 0.88f,
                EmissiveColor: new Color4(0.13f, 0.13f, 0.13f, 1f),
                RequiresDedicatedOpacityInput: true,
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: false,
                AllowsImplicitAlphaFallback: false,
                SuppressLatePassWithoutDedicatedOpacity: false,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(true, ["sim-skintone-base"], "requires-skintone-underlay"),
                TransparencyMode: ViewportLatePassTransparencyMode.AlwaysTransparent,
                SortLayerTieBreaker: 0,
                CompositionTieBreaker: 0,
                RuleTag: "skintone-overlay-masked",
                RelationTag: "skintone-before-cas"),
            _ => null
        };

    private static ViewportLateFallbackDefaultsProfile BuildViewportGenericDetailLateFallbackDefaultsProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportGenericDetailLateProfile genericDetailLateProfile) =>
        new(
            Family: ViewportLatePassFamily.GenericDetail,
            BlendFamily: ViewportParityBlendFamily.GenericOverlay,
            StackOrder: genericDetailLateProfile.StackOrder,
            PassPhase: genericDetailLateProfile.PassPhase,
            MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
            AlphaScale: genericDetailLateProfile.AlphaScale,
            EmissiveColor: genericDetailLateProfile.EmissiveColor,
            RequiresDedicatedOpacityInput: false,
            RequiresExplicitOpacityForRenderAlpha: genericDetailLateProfile.RequiresExplicitOpacityForRenderAlpha,
            DisallowViewportColorAlphaSource: genericDetailLateProfile.DisallowViewportColorAlphaSource,
            AllowsImplicitAlphaFallback: true,
            SuppressLatePassWithoutDedicatedOpacity: false,
            UnderlayProfile: new ViewportLatePassUnderlayProfile(true, ["cas-shell-base", "surface", "surface-layered", "cas-overlay-base", "cas-overlay-highlayer-base"], "requires-overlay-underlay"),
            TransparencyMode: ViewportLatePassTransparencyMode.AlwaysTransparent,
            SortLayerTieBreaker: blendIntent == ViewportLatePassBlendIntent.OrderedSoft ? material?.SortLayer ?? 0 : 0,
            CompositionTieBreaker: 0,
            RuleTag: genericDetailLateProfile.RuleTag,
            RelationTag: "generic-detail-over-underlay");

    private static OverlayMaterialPassVariant GetViewportSecondaryPassVariantForParity(
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile) =>
        blendIntent switch
        {
            ViewportLatePassBlendIntent.DetailMasked => OverlayMaterialPassVariant.CosmeticDetailLate,
            ViewportLatePassBlendIntent.DetailSoft => OverlayMaterialPassVariant.CosmeticDetailLate,
            ViewportLatePassBlendIntent.DetailDefault => OverlayMaterialPassVariant.DefaultDetailLate,
            ViewportLatePassBlendIntent.OrderedSoft => OverlayMaterialPassVariant.OrderedDetailLate,
            ViewportLatePassBlendIntent.EmissiveSoft => OverlayMaterialPassVariant.WornEmissiveLate,
            ViewportLatePassBlendIntent.EmissiveRestricted => OverlayMaterialPassVariant.HighLayerEmissiveLate,
            _ => parityProfile.BlendFamily == ViewportParityBlendFamily.GenericOverlay
                ? OverlayMaterialPassVariant.DefaultDetailLate
                : OverlayMaterialPassVariant.Primary
        };

    private static ViewportLatePassBlendIntent ResolveViewportOrdinaryDetailBlendIntent(
        OverlayCompositorPolicy overlayPolicy) =>
        overlayPolicy.PassIntent switch
        {
            OverlayPassIntent.CosmeticDetail when overlayPolicy.BlendFamily == OverlayBlendFamily.GrayscaleDetail => ViewportLatePassBlendIntent.DetailMasked,
            OverlayPassIntent.CosmeticDetail => ViewportLatePassBlendIntent.DetailSoft,
            OverlayPassIntent.DefaultDetail => ViewportLatePassBlendIntent.DetailDefault,
            OverlayPassIntent.OrderedDetail => ViewportLatePassBlendIntent.OrderedSoft,
            _ => ViewportLatePassBlendIntent.None
        };

    private static ViewportLatePassExecutionPolicy BuildViewportLatePassExecutionPolicy(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent,
        ViewportParityRuleProfile parityProfile,
        Color4 defaultEmissiveColor,
        bool renderAlphaMap,
        bool hasAlphaSource,
        bool hasExplicitOpacity,
        bool alphaSourceMatchesViewportColor)
    {
        var parityBehavior = BuildViewportParityBehaviorMatrixEntry(material, parityProfile, blendIntent, defaultEmissiveColor);
        var resolvedRenderAlphaMap = renderAlphaMap &&
            hasAlphaSource &&
            (!parityBehavior.RequiresExplicitOpacityForRenderAlpha || hasExplicitOpacity) &&
            (!parityBehavior.DisallowViewportColorAlphaSource || !alphaSourceMatchesViewportColor);

        if (parityBehavior.RequiresDedicatedOpacityInput)
        {
            resolvedRenderAlphaMap = resolvedRenderAlphaMap && (hasExplicitOpacity || !alphaSourceMatchesViewportColor);
        }
        else if (!parityBehavior.AllowsImplicitAlphaFallback && alphaSourceMatchesViewportColor)
        {
            resolvedRenderAlphaMap = false;
        }

        return new ViewportLatePassExecutionPolicy(
            BlendIntent: blendIntent,
            PassPhase: parityBehavior.PassPhase,
            ParityProfile: parityProfile,
            ParityBehavior: parityBehavior,
            MaterialMode: parityBehavior.MaterialMode,
            EmissiveColor: parityBehavior.EmissiveColor,
            RenderAlphaMap: resolvedRenderAlphaMap,
            AlphaScale: parityBehavior.AlphaScale);
    }

    private static TextureModel? CreateViewportLatePassAlphaTextureModel(
        ViewportLatePassMaterialPlan latePassPlan,
        float alphaScaleMultiplier)
    {
        if (!latePassPlan.Policy.RenderAlphaMap || latePassPlan.AlphaSourcePngBytes is null)
        {
            return null;
        }

        var sourcePngBytes = latePassPlan.AlphaSourcePngBytes;
        var effectiveAlphaScale = latePassPlan.Policy.AlphaScale * alphaScaleMultiplier;
        if (Math.Abs(effectiveAlphaScale - 1f) <= 0.001f)
        {
            return new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true);
        }

        var scaledAlphaPng = ScalePngAlpha(sourcePngBytes, effectiveAlphaScale);
        return scaledAlphaPng is null
            ? new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true)
            : new TextureModel(new MemoryStream(scaledAlphaPng), autoCloseStream: true);
    }

    private static bool ShouldRenderOverlayDetailLatePass(OverlayExecutionPlan executionPlan) =>
        executionPlan.ViewportColorTexture is not null &&
        IsOverlayDetailLatePassIntent(executionPlan.Policy.PassIntent);

    private static Color4 GetOverlayDetailLatePassEmissiveColor(OverlayCompositorPolicy overlayPolicy) =>
        BuildViewportGenericDetailLateProfile(
            null,
            ResolveViewportOrdinaryDetailBlendIntent(overlayPolicy))?.EmissiveColor ?? new Color4(0.1f, 0.1f, 0.1f, 1f);

    private static CanonicalTexture? ResolveOverlayAlphaSourceTexture(
        OverlayCompositorPolicy overlayPolicy,
        CanonicalMaterial? material,
        ViewportCompositionRuleProfile? compositionRuleProfile,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture)
    {
        if (compositionRuleProfile is { } ruleProfile)
        {
            if (ruleProfile.RequiresDedicatedOpacityInput)
            {
                return opacityTexture is not null &&
                    (HasExplicitOpacityTexture(material) || !ReferenceEquals(opacityTexture, viewportColorTexture))
                    ? opacityTexture
                    : null;
            }

            if (!ruleProfile.AllowsImplicitAlphaFallback)
            {
                return opacityTexture;
            }
        }

        if (overlayPolicy.RequiresExplicitOpacityForTransparency)
        {
            if (opacityTexture is null)
            {
                return null;
            }

            if (overlayPolicy.DisallowViewportColorAlphaFallback &&
                ReferenceEquals(opacityTexture, viewportColorTexture) &&
                !HasExplicitOpacityTexture(material))
            {
                return null;
            }

            return opacityTexture;
        }

        return opacityTexture ??
            (TextureSupportsAlpha(viewportColorTexture) ? viewportColorTexture : null);
    }

    private static bool ShouldRenderOverlayPolicyTransparency(
        OverlayCompositorPolicy overlayPolicy,
        CanonicalMaterial? material,
        ViewportCompositionRuleProfile? compositionRuleProfile,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture)
    {
        if (compositionRuleProfile is { } ruleProfile)
        {
            if (ruleProfile.RequiresDedicatedOpacityInput)
            {
                return opacityTexture is not null &&
                    (HasExplicitOpacityTexture(material) || !ReferenceEquals(opacityTexture, viewportColorTexture));
            }

            if (!ruleProfile.AllowsImplicitAlphaFallback)
            {
                return opacityTexture is not null;
            }
        }

        switch (overlayPolicy.BlendFamily)
        {
            case OverlayBlendFamily.None:
            case OverlayBlendFamily.StraightAlpha:
            case OverlayBlendFamily.GrayscaleDetail:
                if (overlayPolicy.RequiresExplicitOpacityForTransparency)
                {
                    return opacityTexture is not null &&
                        (!overlayPolicy.DisallowViewportColorAlphaFallback ||
                         HasExplicitOpacityTexture(material) ||
                         !ReferenceEquals(opacityTexture, viewportColorTexture));
                }

                return opacityTexture is not null || TextureSupportsAlpha(viewportColorTexture);
            default:
                return opacityTexture is not null || TextureSupportsAlpha(viewportColorTexture);
        }
    }

    private static bool ShouldUseGrayscaleOverlayComposite(CanonicalMaterial? material) =>
        BuildOverlayCompositorPolicy(material).UseGrayscaleComposite;

    private static float GetOverlayLaneOpacityScale(CanonicalMaterial? material)
        => IsCasOverlayDetailStage(material) ? BuildOverlayCompositorPolicy(material).OverlayOpacityScale : 1f;

    private static Color4 GetCompositionAwareOverlayEmissiveColor(CanonicalMaterial? material)
        => BuildOverlayCompositorPolicy(material).OverlayEmissiveColor;

    private static Color4? BuildApproximateBaseColor(CanonicalMaterial? material)
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

    private static Color4? BuildViewportTintColor(CanonicalMaterial? material)
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

    private static TextureModel? CreateTextureModel(CanonicalTexture? texture, bool forceOpaqueAlpha = false)
    {
        if (texture is null)
        {
            return null;
        }

        var pngBytes = forceOpaqueAlpha ? ForceOpaquePngAlpha(texture.PngBytes) : texture.PngBytes;
        return new TextureModel(new MemoryStream(pngBytes), autoCloseStream: true);
    }

    private static byte[] ForceOpaquePngAlpha(byte[] pngBytes)
    {
        using var input = new Windows.Storage.Streams.InMemoryRandomAccessStream();
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

        using var output = new Windows.Storage.Streams.InMemoryRandomAccessStream();
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

    private static byte[]? ComposeSwatchMaskedPng(byte[] diffusePngBytes, byte[] maskPngBytes, Color4 tintColor)
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

    private static ViewportResolvedUvBinding BuildViewportResolvedUvBinding(
        CanonicalMaterial? material,
        CanonicalTexture? preferredTexture,
        string? selectedSlot,
        ViewportPrimaryPassContract? primaryPassContract = null)
    {
        var sampling = SelectViewportSampling(material, preferredTexture, selectedSlot, primaryPassContract);
        if (sampling is not null && !sampling.IsApproximate)
        {
            return new ViewportResolvedUvBinding(
                UvChannel: sampling.UvChannel,
                UvScaleU: sampling.UvScaleU,
                UvScaleV: sampling.UvScaleV,
                UvOffsetU: sampling.UvOffsetU,
                UvOffsetV: sampling.UvOffsetV,
                HasAuthoritativeChannelBinding: true,
                HasAuthoritativeTransform: HasExplicitSamplingDirective(sampling),
                RuleTag: $"sampling:{sampling.Slot}|exact");
        }

        if (preferredTexture is not null && !preferredTexture.IsApproximateUvTransform)
        {
            return new ViewportResolvedUvBinding(
                UvChannel: preferredTexture.UvChannel,
                UvScaleU: preferredTexture.UvScaleU,
                UvScaleV: preferredTexture.UvScaleV,
                UvOffsetU: preferredTexture.UvOffsetU,
                UvOffsetV: preferredTexture.UvOffsetV,
                HasAuthoritativeChannelBinding: true,
                HasAuthoritativeTransform: HasExplicitTextureUvDirective(preferredTexture),
                RuleTag: $"texture:{preferredTexture.Slot}|exact");
        }

        if (sampling is not null)
        {
            return new ViewportResolvedUvBinding(
                UvChannel: sampling.UvChannel,
                UvScaleU: sampling.UvScaleU,
                UvScaleV: sampling.UvScaleV,
                UvOffsetU: sampling.UvOffsetU,
                UvOffsetV: sampling.UvOffsetV,
                HasAuthoritativeChannelBinding: false,
                HasAuthoritativeTransform: false,
                RuleTag: $"sampling:{sampling.Slot}|approx");
        }

        if (preferredTexture is not null)
        {
            return new ViewportResolvedUvBinding(
                UvChannel: preferredTexture.UvChannel,
                UvScaleU: preferredTexture.UvScaleU,
                UvScaleV: preferredTexture.UvScaleV,
                UvOffsetU: preferredTexture.UvOffsetU,
                UvOffsetV: preferredTexture.UvOffsetV,
                HasAuthoritativeChannelBinding: false,
                HasAuthoritativeTransform: false,
                RuleTag: $"texture:{preferredTexture.Slot}|approx");
        }

        return new ViewportResolvedUvBinding(
            UvChannel: 0,
            UvScaleU: 1f,
            UvScaleV: 1f,
            UvOffsetU: 0f,
            UvOffsetV: 0f,
            HasAuthoritativeChannelBinding: false,
            HasAuthoritativeTransform: false,
            RuleTag: "mesh-preferred");
    }

    private static UVTransform BuildUvTransform(ViewportResolvedUvBinding resolvedUvBinding)
    {
        if (!resolvedUvBinding.HasAuthoritativeTransform)
        {
            return new UVTransform(0f);
        }

        return new UVTransform(
            rotation: 0f,
            scalingX: resolvedUvBinding.UvScaleU,
            scalingY: resolvedUvBinding.UvScaleV,
            translationX: resolvedUvBinding.UvOffsetU,
            translationY: resolvedUvBinding.UvOffsetV);
    }

    private static bool HasExplicitTextureUvDirective(CanonicalTexture? texture)
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

    private static bool HasExplicitSamplingDirective(CanonicalMaterialSampling? sampling)
    {
        if (sampling is null)
        {
            return false;
        }

        return sampling.UvChannel != 0 ||
               Math.Abs(sampling.UvScaleU - 1f) >= 0.0001f ||
               Math.Abs(sampling.UvScaleV - 1f) >= 0.0001f ||
               Math.Abs(sampling.UvOffsetU) >= 0.0001f ||
               Math.Abs(sampling.UvOffsetV) >= 0.0001f;
    }

    private static CanonicalMaterialSampling? SelectViewportSampling(
        CanonicalMaterial? material,
        CanonicalTexture? preferredTexture,
        string? selectedSlot,
        ViewportPrimaryPassContract? primaryPassContract = null)
    {
        if (material?.Sampling is not { Count: > 0 } sampling)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            var selectedSlotSampling = sampling.FirstOrDefault(entry => entry.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase));
            if (selectedSlotSampling is not null)
            {
                return selectedSlotSampling;
            }

            // In per-slot preview mode, do not borrow diffuse/default sampling from a different slot.
            // Falling back to the texture's own UV metadata is safer than cross-slot UV pollution.
            if (preferredTexture is null ||
                preferredTexture.Slot.Equals(selectedSlot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        if (preferredTexture is not null)
        {
            var textureSlotSampling = sampling.FirstOrDefault(entry => entry.Slot.Equals(preferredTexture.Slot, StringComparison.OrdinalIgnoreCase));
            if (textureSlotSampling is not null)
            {
                return textureSlotSampling;
            }
        }

        var resolvedPrimaryPassContract = primaryPassContract ?? BuildViewportPrimaryPassContract(material);
        var primaryPassFamily = resolvedPrimaryPassContract.Family;
        var isHelperProjectiveFamily = primaryPassFamily == ViewportPrimaryPassFamily.HelperProjective;
        var isHelperLayeredFamily = primaryPassFamily == ViewportPrimaryPassFamily.HelperLayered;
        var isHelperUtilityFamily = primaryPassFamily == ViewportPrimaryPassFamily.HelperUtility;

        if (isHelperProjectiveFamily)
        {
            return null;
        }

        if (isHelperLayeredFamily)
        {
            var layeredSampling = SelectDeclaredSlotSampling(sampling, material.LayeredTextureSlots);
            if (layeredSampling is not null)
            {
                return layeredSampling;
            }

            return sampling.Count == 1 ? sampling[0] : null;
        }

        if (isHelperUtilityFamily)
        {
            var utilitySampling = SelectDeclaredSlotSampling(sampling, material.UtilityTextureSlots);
            if (utilitySampling is not null)
            {
                return utilitySampling;
            }

            return sampling.Count == 1 ? sampling[0] : null;
        }

        return sampling.FirstOrDefault(entry => entry.Slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase)) ??
               (sampling.Count == 1 ? sampling[0] : null);
    }

    private static CanonicalMaterialSampling? SelectDeclaredSlotSampling(
        IReadOnlyList<CanonicalMaterialSampling> sampling,
        IReadOnlyList<string>? preferredSlots)
    {
        if (preferredSlots is not { Count: > 0 })
        {
            return null;
        }

        foreach (var preferredSlot in preferredSlots)
        {
            var exactSampling = sampling.FirstOrDefault(entry => entry.Slot.Equals(preferredSlot, StringComparison.OrdinalIgnoreCase));
            if (exactSampling is not null)
            {
                return exactSampling;
            }
        }

        return null;
    }

    private static IReadOnlyList<CanonicalTexture> SelectViewportTextures(CanonicalMaterial? material, SceneRenderMode renderMode, string? selectedSlot)
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
            .OrderByDescending(texture => ScorePrimaryViewportTexture(texture, renderMode))
            .ThenBy(texture => texture.Slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static int ScorePrimaryViewportTexture(CanonicalTexture texture, SceneRenderMode renderMode)
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

    private static bool IsTransparentMaterial(CanonicalScene scene, int materialIndex, string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var textures = SelectViewportTextures(material, SceneRenderMode.LitTexture, selectedSlot);
        var surfaceTextures = SelectViewportSurfaceTextures(material, textures, selectedSlot);
        var diffuseTexture = SelectBaseColorTexture(material, surfaceTextures);
        return ShouldRenderTransparentViewport(material, surfaceTextures, diffuseTexture, selectedSlot);
    }

    private static bool IsViewportPrimaryPassTransparent(
        CanonicalScene scene,
        int materialIndex,
        OverlayMaterialPassVariant passVariant,
        string? selectedSlot)
    {
        var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
            ? scene.Materials[materialIndex]
            : null;
        var primaryPassContract = BuildViewportPrimaryPassContract(material, passVariant);
        if (primaryPassContract.Family == ViewportPrimaryPassFamily.None)
        {
            return IsTransparentMaterial(scene, materialIndex, selectedSlot);
        }

        if (primaryPassContract.Family is
            ViewportPrimaryPassFamily.HelperProjective or
            ViewportPrimaryPassFamily.HelperLayered or
            ViewportPrimaryPassFamily.HelperUtility)
        {
            return false;
        }

        var textures = SelectViewportTextures(material, SceneRenderMode.LitTexture, selectedSlot);
        var surfaceTextures = SelectViewportSurfaceTextures(material, textures, selectedSlot);
        var diffuseTexture = SelectBaseColorTexture(material, surfaceTextures);
        var opacityTexture = SelectViewportOpacityTexture(material, surfaceTextures, diffuseTexture, selectedSlot);
        return ShouldRenderViewportPrimaryTransparency(
            primaryPassContract,
            material,
            surfaceTextures,
            diffuseTexture,
            opacityTexture,
            selectedSlot);
    }

    private static bool ShouldRenderViewportPrimaryTransparency(
        ViewportPrimaryPassContract primaryPassContract,
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> surfaceTextures,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? opacityTexture,
        string? selectedSlot)
        => BuildViewportPrimaryAlphaProfile(
            primaryPassContract,
            material,
            surfaceTextures,
            diffuseTexture,
            opacityTexture,
            viewportColorTexture: diffuseTexture,
            selectedSlot).RenderAlphaMap;

    private static ViewportPrimaryAlphaProfile BuildViewportPrimaryAlphaProfile(
        ViewportPrimaryPassContract primaryPassContract,
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> surfaceTextures,
        CanonicalTexture? diffuseTexture,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture,
        string? selectedSlot)
    {
        if (primaryPassContract.Family == ViewportPrimaryPassFamily.None)
        {
            var renderAlphaMap = ShouldRenderTransparentViewport(material, surfaceTextures, diffuseTexture, selectedSlot);
            return new ViewportPrimaryAlphaProfile(
                RenderAlphaMap: renderAlphaMap,
                AlphaSourceTexture: renderAlphaMap
                    ? ResolveViewportPrimaryAlphaSourceTexture(
                        primaryPassContract,
                        material,
                        opacityTexture,
                        viewportColorTexture)
                    : null,
                RuleTag: "generic-primary-alpha-profile");
        }

        if (primaryPassContract.Family is
            ViewportPrimaryPassFamily.HelperProjective or
            ViewportPrimaryPassFamily.HelperLayered or
            ViewportPrimaryPassFamily.HelperUtility)
        {
            return new ViewportPrimaryAlphaProfile(
                RenderAlphaMap: false,
                AlphaSourceTexture: null,
                RuleTag: $"{primaryPassContract.ContractTag}|helper-primary-alpha-profile");
        }

        var alphaSourceTexture = ResolveViewportPrimaryAlphaSourceTexture(
            primaryPassContract,
            material,
            opacityTexture,
            viewportColorTexture);
        return new ViewportPrimaryAlphaProfile(
            RenderAlphaMap: alphaSourceTexture is not null,
            AlphaSourceTexture: alphaSourceTexture,
            RuleTag: $"{primaryPassContract.ContractTag}|primary-alpha-profile");
    }

    private static CanonicalTexture? ResolveViewportPrimaryAlphaSourceTexture(
        ViewportPrimaryPassContract primaryPassContract,
        CanonicalMaterial? material,
        CanonicalTexture? opacityTexture,
        CanonicalTexture? viewportColorTexture)
    {
        if (primaryPassContract.RequiresExplicitOpacityAuthority)
        {
            return opacityTexture is not null && HasExplicitOpacityTexture(material)
                ? opacityTexture
                : null;
        }

        if (primaryPassContract.DisallowViewportColorAlphaFallback &&
            opacityTexture is not null &&
            ReferenceEquals(opacityTexture, viewportColorTexture) &&
            !HasExplicitOpacityTexture(material))
        {
            return null;
        }

        return opacityTexture ??
            (TextureSupportsAlpha(viewportColorTexture) ? viewportColorTexture : null);
    }

    private static TextureModel? CreateViewportPrimarySourceAlphaTextureModel(
        ViewportPrimaryAlphaProfile? primaryAlphaProfile)
    {
        var sourcePngBytes = primaryAlphaProfile?.RenderAlphaMap == true
            ? primaryAlphaProfile.Value.AlphaSourceTexture?.PngBytes
            : null;
        return sourcePngBytes is null
            ? null
            : new TextureModel(new MemoryStream(sourcePngBytes), autoCloseStream: true);
    }

    private async Task<WriteableBitmap> GenerateUvPreviewAsync(CanonicalScene scene, CancellationToken cancellationToken)
    {
        var renderMode = ViewModel.SelectedSceneRenderMode;
        var selectedSlot = GetSelectedSceneTextureSlot();
        var textureEntries = scene.Materials
            .Select((material, index) => new
            {
                MaterialIndex = index,
                Texture = SelectViewportUvTexture(material, renderMode, selectedSlot)
            })
            .Where(entry => entry.Texture is not null)
            .GroupBy(
                entry => entry.Texture!.SourceKey is { } key
                    ? $"{key.Type:X8}:{key.Group:X8}:{key.FullInstance:X16}"
                    : $"{entry.Texture.FileName}|{Convert.ToHexString(SHA256.HashData(entry.Texture.PngBytes))}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Texture = group.First().Texture!,
                MaterialIndices = group.Select(entry => entry.MaterialIndex).Distinct().ToArray()
            })
            .ToArray();

        if (textureEntries.Length == 0)
        {
            return CreateSolidBitmap(512, 512, 0xFF1E1E1E);
        }

        const int panelPadding = 16;
        const int panelMaxWidth = 1024;
        const int panelMaxHeight = 1024;
        var panels = new List<(CanonicalTexture Texture, int[] MaterialIndices, uint AccentColor, uint BackgroundColor, bool IsMixedHelper, int SourceWidth, int SourceHeight, int Width, int Height, float Scale, byte[] Pixels)>();
        foreach (var entry in textureEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var decoded = await DecodePngAsync(entry.Texture.PngBytes, cancellationToken);
            var scale = MathF.Min(1f, MathF.Min(panelMaxWidth / (float)decoded.Width, panelMaxHeight / (float)decoded.Height));
            var width = Math.Max(1, (int)MathF.Round(decoded.Width * scale));
            var height = Math.Max(1, (int)MathF.Round(decoded.Height * scale));
            var (accentColor, backgroundColor, isMixedHelper) = GetUvPreviewPanelStyle(scene, entry.MaterialIndices);
            panels.Add((entry.Texture, entry.MaterialIndices, accentColor, backgroundColor, isMixedHelper, decoded.Width, decoded.Height, width, height, scale, decoded.Pixels));
        }

        var canvasWidth = panels.Max(panel => panel.Width) + (panelPadding * 2);
        var canvasHeight = panels.Sum(panel => panel.Height) + (panelPadding * (panels.Count + 1));
        var canvas = new byte[canvasWidth * canvasHeight * 4];
        FillSolid(canvas, canvasWidth, canvasHeight, 0xFF111111);

        var colors = new uint[] { 0xFFFF4FD1, 0xFFFFFF3B, 0xFFFF7F50, 0xFF7CFC00, 0xFFFF69B4, 0xFF87CEFA };
        var offsetY = panelPadding;
        foreach (var (_, materialIndices, accentColor, backgroundColor, isMixedHelper, sourceWidth, sourceHeight, width, height, _, pixels) in panels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offsetX = panelPadding;
            FillRectangle(canvas, canvasWidth, canvasHeight, offsetX - 2, offsetY - 2, width + 4, height + 4, backgroundColor);
            DrawRectangleOutline(canvas, canvasWidth, canvasHeight, offsetX - 2, offsetY - 2, width + 4, height + 4, accentColor);
            if (isMixedHelper)
            {
                DrawFilledRectangle(canvas, canvasWidth, canvasHeight, offsetX - 2, offsetY - 2, Math.Min(10, width + 4), height + 4, accentColor);
            }
            BlitScaledBgra(canvas, canvasWidth, canvasHeight, pixels, sourceWidth, sourceHeight, width, height, offsetX, offsetY);

            var meshes = scene.Meshes
                .Select((mesh, meshIndex) => new { Mesh = mesh, MeshIndex = meshIndex })
                .Where(entry => Array.IndexOf(materialIndices, entry.Mesh.MaterialIndex) >= 0)
                .ToArray();
            foreach (var meshEntry in meshes)
            {
                var coordinates = SelectTextureCoordinates(meshEntry.Mesh, scene, meshEntry.Mesh.MaterialIndex, renderMode, selectedSlot);
                var meshMaterial = meshEntry.Mesh.MaterialIndex >= 0 && meshEntry.Mesh.MaterialIndex < scene.Materials.Count
                    ? scene.Materials[meshEntry.Mesh.MaterialIndex]
                    : null;
                var color = GetUvPreviewWireframeColor(meshMaterial, meshEntry.MeshIndex, colors);
                DrawUvWireframe(canvas, canvasWidth, canvasHeight, coordinates, meshEntry.Mesh.Indices, offsetX, offsetY, width, height, color);
            }

            offsetY += height + panelPadding;
        }

        return await CreateWriteableBitmapAsync(canvasWidth, canvasHeight, canvas, cancellationToken);
    }

    private static void DrawUvWireframe(byte[] canvas, int canvasWidth, int canvasHeight, IReadOnlyList<float> coordinates, IReadOnlyList<int> indices, int offsetX, int offsetY, int panelWidth, int panelHeight, uint color)
    {
        if (coordinates.Count < 4)
        {
            return;
        }

        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            var ia = indices[index];
            var ib = indices[index + 1];
            var ic = indices[index + 2];
            if (!TryGetUvPoint(coordinates, ia, offsetX, offsetY, panelWidth, panelHeight, out var ax, out var ay) ||
                !TryGetUvPoint(coordinates, ib, offsetX, offsetY, panelWidth, panelHeight, out var bx, out var by) ||
                !TryGetUvPoint(coordinates, ic, offsetX, offsetY, panelWidth, panelHeight, out var cx, out var cy))
            {
                continue;
            }

            DrawLine(canvas, canvasWidth, canvasHeight, ax, ay, bx, by, color);
            DrawLine(canvas, canvasWidth, canvasHeight, bx, by, cx, cy, color);
            DrawLine(canvas, canvasWidth, canvasHeight, cx, cy, ax, ay, color);
        }
    }

    private static (uint AccentColor, uint BackgroundColor, bool IsMixedHelper) GetUvPreviewPanelStyle(CanonicalScene scene, IReadOnlyList<int> materialIndices)
    {
        var hasProjective = false;
        var hasLayered = false;
        var hasUtility = false;
        var hasOther = false;

        foreach (var materialIndex in materialIndices)
        {
            var material = materialIndex >= 0 && materialIndex < scene.Materials.Count
                ? scene.Materials[materialIndex]
                : null;
            switch (BuildViewportPrimaryPassContract(material).Family)
            {
                case ViewportPrimaryPassFamily.HelperProjective:
                    hasProjective = true;
                    break;
                case ViewportPrimaryPassFamily.HelperLayered:
                    hasLayered = true;
                    break;
                case ViewportPrimaryPassFamily.HelperUtility:
                    hasUtility = true;
                    break;
                default:
                    hasOther = true;
                    break;
            }
        }

        var helperFamilyCount = (hasProjective ? 1 : 0) + (hasLayered ? 1 : 0) + (hasUtility ? 1 : 0);
        if (helperFamilyCount == 0)
        {
            return (0xFF343434, 0xFF181818, false);
        }

        if (helperFamilyCount > 1 || hasOther)
        {
            return (0xFFFFB347, 0xFF2B2116, true);
        }

        if (hasProjective)
        {
            return (0xFF45C7F0, 0xFF15242B, false);
        }

        if (hasLayered)
        {
            return (0xFF9BE564, 0xFF182415, false);
        }

        return (0xFFFFC857, 0xFF2A2415, false);
    }

    private static uint GetUvPreviewWireframeColor(CanonicalMaterial? material, int meshIndex, IReadOnlyList<uint> defaultColors)
    {
        switch (BuildViewportPrimaryPassContract(material).Family)
        {
            case ViewportPrimaryPassFamily.HelperProjective:
                return 0xFF3BE0FF;
            case ViewportPrimaryPassFamily.HelperLayered:
                return 0xFFA7FF5A;
            case ViewportPrimaryPassFamily.HelperUtility:
                return 0xFFFFD166;
            default:
                return defaultColors[meshIndex % defaultColors.Count];
        }
    }

    private static bool TryGetUvPoint(IReadOnlyList<float> coordinates, int vertexIndex, int offsetX, int offsetY, int panelWidth, int panelHeight, out int x, out int y)
    {
        var uvIndex = vertexIndex * 2;
        if (uvIndex < 0 || uvIndex + 1 >= coordinates.Count)
        {
            x = 0;
            y = 0;
            return false;
        }

        var u = Math.Clamp(coordinates[uvIndex], 0f, 1f);
        var v = Math.Clamp(coordinates[uvIndex + 1], 0f, 1f);
        x = offsetX + (int)MathF.Round(u * (panelWidth - 1));
        y = offsetY + (int)MathF.Round(v * (panelHeight - 1));
        return true;
    }

    private static void DrawRectangleOutline(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, int width, int height, uint color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var maxX = x + width - 1;
        var maxY = y + height - 1;
        DrawLine(canvas, canvasWidth, canvasHeight, x, y, maxX, y, color);
        DrawLine(canvas, canvasWidth, canvasHeight, x, maxY, maxX, maxY, color);
        DrawLine(canvas, canvasWidth, canvasHeight, x, y, x, maxY, color);
        DrawLine(canvas, canvasWidth, canvasHeight, maxX, y, maxX, maxY, color);
    }

    private static void FillRectangle(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, int width, int height, uint color)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var startX = Math.Max(0, x);
        var startY = Math.Max(0, y);
        var endX = Math.Min(canvasWidth, x + width);
        var endY = Math.Min(canvasHeight, y + height);
        for (var row = startY; row < endY; row++)
        {
            for (var column = startX; column < endX; column++)
            {
                PlotPixel(canvas, canvasWidth, canvasHeight, column, row, color);
            }
        }
    }

    private static void DrawFilledRectangle(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, int width, int height, uint color) =>
        FillRectangle(canvas, canvasWidth, canvasHeight, x, y, width, height, color);

    private static void DrawLine(byte[] canvas, int canvasWidth, int canvasHeight, int x0, int y0, int x1, int y1, uint color)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;

        while (true)
        {
            PlotPixel(canvas, canvasWidth, canvasHeight, x0, y0, color);
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            var twiceError = 2 * error;
            if (twiceError >= dy)
            {
                error += dy;
                x0 += sx;
            }
            if (twiceError <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void PlotPixel(byte[] canvas, int canvasWidth, int canvasHeight, int x, int y, uint color)
    {
        if ((uint)x >= canvasWidth || (uint)y >= canvasHeight)
        {
            return;
        }

        var offset = ((y * canvasWidth) + x) * 4;
        canvas[offset] = (byte)(color & 0xFF);
        canvas[offset + 1] = (byte)((color >> 8) & 0xFF);
        canvas[offset + 2] = (byte)((color >> 16) & 0xFF);
        canvas[offset + 3] = (byte)((color >> 24) & 0xFF);
    }

    private static void FillSolid(byte[] canvas, int width, int height, uint color)
    {
        for (var index = 0; index < width * height; index++)
        {
            var offset = index * 4;
            canvas[offset] = (byte)(color & 0xFF);
            canvas[offset + 1] = (byte)((color >> 8) & 0xFF);
            canvas[offset + 2] = (byte)((color >> 16) & 0xFF);
            canvas[offset + 3] = (byte)((color >> 24) & 0xFF);
        }
    }

    private static void BlitScaledBgra(byte[] destination, int destinationWidth, int destinationHeight, byte[] source, int sourceWidth, int sourceHeight, int scaledWidth, int scaledHeight, int offsetX, int offsetY)
    {
        for (var y = 0; y < scaledHeight; y++)
        {
            for (var x = 0; x < scaledWidth; x++)
            {
                var srcX = sourceWidth == scaledWidth ? x : Math.Clamp((int)((x / (float)scaledWidth) * sourceWidth), 0, sourceWidth - 1);
                var srcY = sourceHeight == scaledHeight ? y : Math.Clamp((int)((y / (float)scaledHeight) * sourceHeight), 0, sourceHeight - 1);
                var srcOffset = ((srcY * sourceWidth) + srcX) * 4;
                var dstX = offsetX + x;
                var dstY = offsetY + y;
                if ((uint)dstX >= destinationWidth || (uint)dstY >= destinationHeight)
                {
                    continue;
                }

                var dstOffset = ((dstY * destinationWidth) + dstX) * 4;
                destination[dstOffset] = source[srcOffset];
                destination[dstOffset + 1] = source[srcOffset + 1];
                destination[dstOffset + 2] = source[srcOffset + 2];
                destination[dstOffset + 3] = source[srcOffset + 3];
            }
        }
    }

    private static async Task<(int Width, int Height, byte[] Pixels)> DecodePngAsync(byte[] pngBytes, CancellationToken cancellationToken)
    {
        using var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer()).AsTask(cancellationToken);
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(cancellationToken);
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(cancellationToken);
        return ((int)decoder.PixelWidth, (int)decoder.PixelHeight, pixelData.DetachPixelData());
    }

    private static async Task<WriteableBitmap> CreateWriteableBitmapAsync(int width, int height, byte[] pixels, CancellationToken cancellationToken)
    {
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels, cancellationToken);
        bitmap.Invalidate();
        return bitmap;
    }

    private static WriteableBitmap CreateSolidBitmap(int width, int height, uint color)
    {
        var pixels = new byte[width * height * 4];
        FillSolid(pixels, width, height, color);
        var bitmap = new WriteableBitmap(width, height);
        using var stream = bitmap.PixelBuffer.AsStream();
        stream.Write(pixels, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }

    private void MainPaneSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isDraggingMainSplitter = true;
        mainSplitterStartX = e.GetCurrentPoint(MainContentGrid).Position.X;
        mainSplitterStartResultsWidth = ResultsColumn.ActualWidth;
        mainSplitterTotalResizableWidth = ResultsColumn.ActualWidth + PreviewColumn.ActualWidth;
        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }
    }

    private void MainPaneSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingMainSplitter)
        {
            return;
        }

        var positionX = e.GetCurrentPoint(MainContentGrid).Position.X;
        var delta = positionX - mainSplitterStartX;
        var previewMinWidth = PreviewColumn.MinWidth > 0 ? PreviewColumn.MinWidth : 520;
        var resultsMinWidth = ResultsColumn.MinWidth > 0 ? ResultsColumn.MinWidth : 320;
        var newResultsWidth = Math.Clamp(mainSplitterStartResultsWidth + delta, resultsMinWidth, mainSplitterTotalResizableWidth - previewMinWidth);
        ResultsColumn.Width = new GridLength(newResultsWidth, GridUnitType.Pixel);
        PreviewColumn.Width = new GridLength(Math.Max(previewMinWidth, mainSplitterTotalResizableWidth - newResultsWidth), GridUnitType.Pixel);
    }

    private void MainPaneSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        isDraggingMainSplitter = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }
    }

    private void PreviewPaneSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        isDraggingPreviewSplitter = true;
        previewSplitterStartY = e.GetCurrentPoint(PreviewPaneGrid).Position.Y;
        previewSplitterStartHeight = PreviewViewportRow.ActualHeight;
        if (sender is UIElement element)
        {
            element.CapturePointer(e.Pointer);
        }
    }

    private void PreviewPaneSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!isDraggingPreviewSplitter)
        {
            return;
        }

        var positionY = e.GetCurrentPoint(PreviewPaneGrid).Position.Y;
        var delta = positionY - previewSplitterStartY;
        var occupiedHeight =
            PreviewPaneGrid.RowDefinitions[0].ActualHeight +
            PreviewPaneGrid.RowDefinitions[2].ActualHeight +
            PreviewPaneGrid.RowDefinitions[3].ActualHeight;
        var availableResizableHeight = Math.Max(300, PreviewPaneGrid.ActualHeight - occupiedHeight);
        var minPreviewHeight = Math.Max(PreviewViewportRow.MinHeight, 180);
        var minDetailsHeight = Math.Max(PreviewDiagnosticsRow.MinHeight, 120d);
        var newPreviewHeight = Math.Clamp(previewSplitterStartHeight + delta, minPreviewHeight, availableResizableHeight - minDetailsHeight);
        PreviewViewportRow.Height = new GridLength(newPreviewHeight, GridUnitType.Pixel);
    }

    private void PreviewPaneSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        isDraggingPreviewSplitter = false;
        if (sender is UIElement element)
        {
            element.ReleasePointerCapture(e.Pointer);
        }
    }

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

    private static CanonicalTexture? SelectBaseColorTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = CreateScopedMaterial(material, textures);
        return SelectBaseColorTexture(scope);
    }

    private static CanonicalMaterial CreateScopedMaterial(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        string? explicitAlphaSlot = null)
    {
        var scopedSlots = new HashSet<string>(
            textures.Select(texture => texture.Slot),
            StringComparer.OrdinalIgnoreCase);

        if (material is null)
        {
            return new CanonicalMaterial("Scoped", textures, AlphaTextureSlot: explicitAlphaSlot);
        }

        var scopedAlphaSlot = explicitAlphaSlot ?? material.AlphaTextureSlot;
        if (!string.IsNullOrWhiteSpace(scopedAlphaSlot) && !scopedSlots.Contains(scopedAlphaSlot))
        {
            scopedAlphaSlot = null;
        }

        var scopedLayeredSlots = material.LayeredTextureSlots?
            .Where(slot => scopedSlots.Contains(slot))
            .ToArray();
        if (scopedLayeredSlots is { Length: 0 })
        {
            scopedLayeredSlots = null;
        }

        var scopedSampling = material.Sampling?
            .Where(entry => scopedSlots.Contains(entry.Slot))
            .ToArray();
        if (scopedSampling is { Length: 0 })
        {
            scopedSampling = null;
        }

        var scopedUtilitySlots = material.UtilityTextureSlots?
            .Where(slot => scopedSlots.Contains(slot))
            .ToArray();
        if (scopedUtilitySlots is { Length: 0 })
        {
            scopedUtilitySlots = null;
        }

        return material with
        {
            Name = "Scoped",
            Textures = textures,
            AlphaTextureSlot = scopedAlphaSlot,
            LayeredTextureSlots = scopedLayeredSlots,
            Sampling = scopedSampling,
            UtilityTextureSlots = scopedUtilitySlots
        };
    }

    private static IReadOnlyList<CanonicalTexture> SelectViewportSurfaceTextures(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        string? selectedSlot)
    {
        if (textures.Count == 0 || !string.IsNullOrWhiteSpace(selectedSlot))
        {
            return textures;
        }

        var filtered = textures
            .Where(texture => !ShouldSuppressViewportSurfaceTexture(material, texture))
            .ToArray();
        return filtered.Length > 0 ? filtered : textures;
    }

    private static bool ShouldSuppressViewportSurfaceTexture(CanonicalMaterial? material, CanonicalTexture texture)
    {
        if (material is null)
        {
            return false;
        }

        if (material.UtilityTextureSlots?.Any(slot => slot.Equals(texture.Slot, StringComparison.OrdinalIgnoreCase)) == true)
        {
            return true;
        }

        if (texture.Semantic is CanonicalTextureSemantic.Normal or CanonicalTextureSemantic.Specular or CanonicalTextureSemantic.Gloss)
        {
            return true;
        }

        var slot = texture.Slot;
        if (slot.Contains("reveal", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("refraction", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("lightmap", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("lookup", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("depth", StringComparison.OrdinalIgnoreCase) ||
            slot.Contains("occlusion", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((material.ShaderFamily?.Contains("ShaderDayNightParameters", StringComparison.OrdinalIgnoreCase) ?? false) &&
            slot.Contains("lights", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((material.DecodeStrategy?.Contains("Projective", StringComparison.OrdinalIgnoreCase) ?? false) &&
            slot.Contains("project", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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
        string? selectedSlot)
    {
        if (IsNonVisualViewportMaterial(material))
        {
            return null;
        }

        var transparencyKind = DetermineViewportTransparencyKind(material, textures);
        if (transparencyKind == ViewportTransparencyKind.Opaque)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectOpacityTexture(material, baseColorTexture);
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(material, textures, baseColorTexture, scopedAlphaSlot);
    }

    private static CanonicalTexture? SelectHelperProjectiveInspectionTexture(IReadOnlyList<CanonicalTexture> textures) =>
        textures.FirstOrDefault(texture =>
            texture.Slot.Contains("project", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("refraction", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("depth", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("hotspot", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("lookup", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("projection", StringComparison.OrdinalIgnoreCase))
        ?? textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Overlay)
        ?? textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.BaseColor)
        ?? textures.FirstOrDefault();

    private static CanonicalTexture? SelectHelperLayeredInspectionTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures)
    {
        var layeredTexture = SelectLayeredColorTexture(material, textures);
        if (layeredTexture is not null)
        {
            return layeredTexture;
        }

        return textures.FirstOrDefault(texture =>
            texture.Slot.Contains("reveal", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("lightmap", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("lights", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("night", StringComparison.OrdinalIgnoreCase))
            ?? textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Overlay)
            ?? textures.FirstOrDefault();
    }

    private static CanonicalTexture? SelectHelperUtilityInspectionTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures)
    {
        if (material?.UtilityTextureSlots is { Count: > 0 })
        {
            foreach (var utilitySlot in material.UtilityTextureSlots)
            {
                var explicitSlotMatch = textures.FirstOrDefault(texture =>
                    texture.Slot.Equals(utilitySlot, StringComparison.OrdinalIgnoreCase));
                if (explicitSlotMatch is not null)
                {
                    return explicitSlotMatch;
                }
            }
        }

        return textures.FirstOrDefault(texture =>
            texture.Slot.Contains("lookup", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("occlusion", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("reveal", StringComparison.OrdinalIgnoreCase))
            ?? textures.FirstOrDefault();
    }

    private static bool HasExplicitOpacityTexture(CanonicalMaterial? material)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(material.AlphaTextureSlot) &&
            material.Textures.Any(texture => texture.Slot.Equals(material.AlphaTextureSlot, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return material.Textures.Any(texture =>
            texture.Semantic == CanonicalTextureSemantic.Opacity ||
            texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldRenderTransparentViewport(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        string? selectedSlot)
    {
        if (IsNonVisualViewportMaterial(material))
        {
            return false;
        }

        var transparencyKind = DetermineViewportTransparencyKind(material, textures);
        if (transparencyKind == ViewportTransparencyKind.Opaque)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(selectedSlot))
        {
            return SelectOpacityTexture(material, baseColorTexture) is not null;
        }

        var scopedAlphaSlot = !string.IsNullOrWhiteSpace(material?.AlphaTextureSlot) &&
                              string.Equals(material.AlphaTextureSlot, selectedSlot, StringComparison.OrdinalIgnoreCase)
            ? material.AlphaTextureSlot
            : null;
        return SelectOpacityTexture(material, textures, baseColorTexture, scopedAlphaSlot) is not null;
    }

    private static bool IsNonVisualViewportMaterial(CanonicalMaterial? material) =>
        string.Equals(material?.VisualPayloadKind, "non-visual", StringComparison.OrdinalIgnoreCase) ||
        (material?.Approximation?.Contains("non-visual helper/control material", StringComparison.OrdinalIgnoreCase) ?? false);

    private enum OverlayCompositorLane
    {
        None = 0,
        Cosmetic = 1,
        Default = 2,
        OrderedLow = 3,
        OrderedHigh = 4,
        OrdinaryWorn = 5,
        HighLayerWorn = 6
    }

    private enum OverlaySortLayerBucket
    {
        None = 0,
        Default = 1,
        OrderedLow = 2,
        OrderedHigh = 3,
        HighLayer = 4
    }

    private readonly record struct OverlaySortLayerBucketProfile(
        OverlaySortLayerBucket Bucket,
        OverlayCompositorLane DefaultLane,
        int PassBucket,
        int OrdinaryWornPassBucket,
        int GenericDetailStackOrder,
        string RuleTag);

    private readonly record struct OverlaySortLayerThresholdsProfile(
        int OrderedHighSortLayerFloor,
        int HighLayerSortLayerFloor,
        string RuleTag);

    private readonly record struct OverlayWornLaneProfile(
        OverlayCompositorLane Lane,
        OverlayPassIntent PassIntent,
        OverlayMaterialPassVariant PassVariant,
        ViewportLatePassFamily Family,
        ViewportLatePassBlendIntent BlendIntent,
        OverlayBlendFamily BlendFamily,
        int PassBucket,
        int ParityOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 OverlayEmissiveColor,
        Color4? OverlaySpecularColor,
        float? OverlaySpecularShininess,
        ViewportLatePassTransparencyMode TransparencyMode,
        ViewportLatePassTransparencyMode MissingDiffuseTransparencyMode,
        float MissingDiffuseAlphaScaleMultiplier,
        float MissingDiffuseEmissiveScaleMultiplier,
        bool RequiresExplicitOpacityForTransparency,
        bool DisallowViewportColorAlphaFallback,
        bool RequiresDedicatedEmissiveMap,
        bool UseExplicitEmissiveOnly,
        bool AllowsViewportColorEmissiveFallback,
        bool AllowsImplicitAlphaFallback,
        string RuleTag,
        string RelationTag);

    private enum OverlayPassIntent
    {
        None = 0,
        CosmeticDetail = 1,
        DefaultDetail = 2,
        OrderedDetail = 3,
        WornLayer = 4,
        HighLayer = 5
    }

    private enum OverlayBlendFamily
    {
        None = 0,
        StraightAlpha = 1,
        GrayscaleDetail = 2,
        RestrictedHighLayer = 3
    }

    private readonly record struct OverlayCompositorPolicy(
        OverlayCompositorLane Lane,
        OverlayPassIntent PassIntent,
        OverlayBlendFamily BlendFamily,
        int PassBucket,
        bool UseGrayscaleComposite,
        float OverlayOpacityScale,
        Color4 OverlayEmissiveColor,
        Color4? OverlaySpecularColor,
        float? OverlaySpecularShininess,
        bool RequiresExplicitOpacityForTransparency,
        bool DisallowViewportColorAlphaFallback,
        bool RequiresDedicatedEmissiveMap,
        bool UseExplicitEmissiveOnly,
        bool AllowsViewportColorEmissiveFallback,
        bool AllowsImplicitAlphaFallback);

    private readonly record struct OverlayExecutionPlan(
        OverlayCompositorPolicy Policy,
        CanonicalTexture? EmissiveTexture,
        CanonicalTexture? ViewportColorTexture,
        CanonicalTexture? AlphaSourceTexture,
        bool RenderAlphaMap);

    private readonly record struct SkintoneOverlayExecutionPlan(
        CanonicalTexture ViewportColorTexture,
        CanonicalTexture? AlphaSourceTexture,
        bool RenderAlphaMap);

    private readonly record struct OverlayMaterialResponsePlan(
        Color4 EmissiveColor,
        Color4 SpecularColor,
        float SpecularShininess,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap);

    private readonly record struct OverlayMaterialApplicationPlan(
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        bool RenderNormalMap,
        TextureModel? NormalMap,
        bool RenderSpecularColorMap,
        TextureModel? SpecularColorMap,
        bool EnableAutoTangent);

    private readonly record struct ViewportLatePassMaterialPlan(
        OverlayMaterialPassVariant PassVariant,
        ViewportLatePassExecutionPolicy Policy,
        ViewportLatePassMaterialPathProfile PathProfile,
        ViewportLatePassSceneRelationProfile SceneRelationProfile,
        TextureModel? TextureMap,
        CanonicalTexture SamplingTexture,
        byte[]? AlphaSourcePngBytes);

    private readonly record struct ViewportLatePassMaterialPathProfile(
        ViewportLatePassMaterialPathKind Kind,
        ViewportLatePassMaterialMode MaterialMode,
        string RuleTag);

    private readonly record struct ViewportLatePassSourceProfile(
        ViewportLatePassSceneRelationProfile SceneRelationProfile,
        TextureModel TextureMap,
        CanonicalTexture SamplingTexture,
        byte[]? AlphaSourcePngBytes,
        Color4 EmissiveColor,
        bool RenderAlphaMap,
        bool HasAlphaSourceTexture,
        bool HasDedicatedOpacityInput,
        bool UsesViewportColorAlphaSource,
        string RuleTag);

    private readonly record struct ViewportLatePassMaterialBindingProfile(
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        string RuleTag);

    private readonly record struct ViewportLatePassExecutionPolicy(
        ViewportLatePassBlendIntent BlendIntent,
        int PassPhase,
        ViewportParityRuleProfile ParityProfile,
        ViewportParityBehaviorMatrixEntry ParityBehavior,
        ViewportLatePassMaterialMode MaterialMode,
        Color4 EmissiveColor,
        bool RenderAlphaMap,
        float AlphaScale);

    private readonly record struct ViewportLatePassStackContract(
        ViewportLatePassMaterialMode MaterialMode,
        Color4 DiffuseColor,
        Color4 AmbientColor,
        Color4 EmissiveColor,
        float AlphaScaleMultiplier,
        float OpacityFallbackAlpha,
        float EmissiveScaleWithoutAlpha,
        string ContractTag);

    private readonly record struct ViewportLatePassStackDefaultsProfile(
        ViewportLatePassMaterialMode MaterialMode,
        Color4 DiffuseColor,
        Color4 AmbientColor,
        bool EmissiveUsesPolicyColor,
        float EmissiveColorScale,
        float AlphaScaleMultiplier,
        float OpacityFallbackAlpha,
        float EmissiveScaleWithoutAlpha,
        string ContractTag);

    private readonly record struct ViewportLatePassUnderlayProfile(
        bool RequiresUnderlay,
        IReadOnlyList<string> RequiredStageFamilies,
        string RuleTag);

    private readonly record struct ViewportLatePassFamilyContract(
        ViewportLatePassFamily Family,
        int StackOrder,
        ViewportLatePassUnderlayProfile UnderlayProfile,
        ViewportLatePassTransparencyMode TransparencyMode,
        string RelationTag);

    private readonly record struct ViewportLatePassSceneRelationProfile(
        ViewportLatePassFamilyContract FamilyContract,
        bool HasSkintoneLateSibling,
        bool HasDiffuseLateSibling,
        bool HasEarlierLateSibling,
        int EffectiveStackOrder,
        ViewportLatePassTransparencyMode EffectiveTransparencyMode,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        bool SuppressFromPassPlanning,
        string RuleTag);

    private readonly record struct ViewportLatePassCoexistenceProfile(
        int StackOrderOffset,
        ViewportLatePassTransparencyMode EffectiveTransparencyMode,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        bool SuppressFromPassPlanning,
        string RuleTag);

    private readonly record struct ViewportLatePassFamilySceneAdjustmentProfile(
        ViewportLatePassTransparencyMode EffectiveTransparencyMode,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        string RuleTag);

    private readonly record struct ViewportLatePassFamilySceneAdjustmentDefaultsProfile(
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        string RuleTag);

    private readonly record struct ViewportLateFamilyBaseDefaultsProfile(
        int BaseStackOrder,
        string RuleTag);

    private readonly record struct ViewportLatePassPairwiseCoexistenceProfile(
        int StackOrderOffset,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        string RuleTag);

    private readonly record struct ViewportLatePassPairwiseRelation(
        ViewportLatePassFamily SiblingFamily,
        int StackOrderOffset,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        string RelationTag);

    private readonly record struct ViewportLatePassPairwiseRelationDefaultsProfile(
        int StackOrderOffset,
        float AlphaScaleMultiplier,
        float EmissiveScaleMultiplier,
        string RelationTag);

    private readonly record struct ViewportLateEmissiveFamilyDefaultsProfile(
        ViewportLatePassBlendIntent BlendIntent,
        OverlayMaterialPassVariant PassVariant,
        int BaseStackOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        float EmissiveColorScale,
        ViewportLatePassTransparencyMode TransparencyMode,
        ViewportLatePassTransparencyMode MissingDiffuseTransparencyMode,
        float MissingDiffuseAlphaScaleMultiplier,
        float MissingDiffuseEmissiveScaleMultiplier,
        ViewportLatePassUnderlayProfile UnderlayProfile,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        string FamilyTag,
        string RelationTag);

    private readonly record struct ViewportLateEmissiveFamilyProfile(
        ViewportLatePassFamily Family,
        ViewportLatePassBlendIntent BlendIntent,
        OverlayMaterialPassVariant PassVariant,
        int BaseStackOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        float EmissiveColorScale,
        ViewportLatePassTransparencyMode TransparencyMode,
        ViewportLatePassTransparencyMode MissingDiffuseTransparencyMode,
        float MissingDiffuseAlphaScaleMultiplier,
        float MissingDiffuseEmissiveScaleMultiplier,
        ViewportLatePassUnderlayProfile UnderlayProfile,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        string FamilyTag,
        string RelationTag);

    private readonly record struct ViewportRenderPassPlan(
        CanonicalMesh Mesh,
        int OriginalIndex,
        CanonicalMaterial? Material,
        OverlayMaterialPassVariant PassVariant,
        ViewportRenderPassOrderingProfile OrderingProfile,
        ViewportRenderPassParticipationProfile ParticipationProfile,
        int PrimaryStackOrder,
        ViewportParityRuleProfile ParityProfile,
        int ParityStackOrder,
        ViewportLatePassSceneRelationProfile? LatePassSceneRelationProfile,
        int SlotCategoryOrder,
        ViewportRenderPassFinalOrderProfile FinalOrderProfile,
        bool IsTransparent,
        bool RenderWireframe);

    private readonly record struct ViewportRenderPassFinalOrderProfile(
        int SortLayerTieBreaker,
        int CompositionTieBreaker,
        string RuleTag);

    private readonly record struct ViewportRenderPassOrderingProfile(
        int RenderStage,
        int PassBucket,
        int PassPhase,
        string RuleTag);

    private readonly record struct ViewportRenderPassParticipationProfile(
        bool IncludePass,
        bool IsTransparent,
        bool RenderWireframe,
        string RuleTag);

    private readonly record struct ViewportResolvedRenderPassContract(
        ViewportRenderPassOrderingProfile OrderingProfile,
        ViewportRenderPassParticipationProfile ParticipationProfile,
        int PrimaryStackOrder,
        ViewportParityRuleProfile ParityProfile,
        int ParityStackOrder,
        int SlotCategoryOrder,
        ViewportRenderPassFinalOrderProfile FinalOrderProfile,
        string RuleTag);

    private readonly record struct ViewportResolvedUvBinding(
        int UvChannel,
        float UvScaleU,
        float UvScaleV,
        float UvOffsetU,
        float UvOffsetV,
        bool HasAuthoritativeChannelBinding,
        bool HasAuthoritativeTransform,
        string RuleTag);

    private readonly record struct ViewportLatePassStageProfile(
        int RenderStage,
        string RuleTag);

    private readonly record struct ViewportCompositorStageOrderProfile(
        int RenderStage,
        string RuleTag);

    private readonly record struct ViewportCasSlotCategoryOrderProfile(
        int SlotCategoryOrder,
        string RuleTag);

    private readonly record struct ViewportMaterialFallbackRenderStageProfile(
        int RenderStage,
        string RuleTag);

    private readonly record struct ViewportParityRuleProfile(
        int FamilyOrder,
        int CompositionOrder,
        ViewportParityOpacityAuthority OpacityAuthority,
        ViewportParityBlendFamily BlendFamily,
        string RuleTag);

    private readonly record struct ViewportParityRuleDefaultsProfile(
        int FamilyOrder,
        int CompositionOrder,
        ViewportParityOpacityAuthority OpacityAuthority,
        ViewportParityBlendFamily BlendFamily,
        string RuleTag);

    private readonly record struct ViewportCompositionRuleProfile(
        OverlayMaterialPassVariant PassVariant,
        ViewportLatePassFamily Family,
        ViewportLatePassBlendIntent BlendIntent,
        ViewportParityOpacityAuthority OpacityAuthority,
        ViewportParityBlendFamily BlendFamily,
        int StackOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresDedicatedOpacityInput,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        int CompositionOrder,
        string RuleTag);

    private readonly record struct ViewportCompositionLateFamilyContractDefaultsProfile(
        ViewportLatePassUnderlayProfile UnderlayProfile,
        string RelationTag);

    private readonly record struct ViewportGenericDetailLateDefaultsProfile(
        int StackOrder,
        int PassPhase,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        string RuleTag);

    private readonly record struct ViewportCompositionLaneDefaultsProfile(
        OverlayMaterialPassVariant PassVariant,
        ViewportLatePassFamily Family,
        ViewportLatePassBlendIntent BlendIntent,
        ViewportParityOpacityAuthority OpacityAuthority,
        ViewportParityBlendFamily BlendFamily,
        int StackOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresDedicatedOpacityInput,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        int CompositionOrder,
        string RuleTag);

    private readonly record struct ViewportParityBehaviorMatrixEntry(
        ViewportParityBlendFamily BlendFamily,
        int PassPhase,
        int StackOrder,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresDedicatedOpacityInput,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        string RuleTag);

    private readonly record struct ViewportFallbackParityBehaviorProfile(
        ViewportParityBlendFamily BlendFamily,
        int StackOrder,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresDedicatedOpacityInput,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        string RuleTag);

    private readonly record struct ViewportGenericOverlayFallbackParityDefaultsProfile(
        ViewportParityBlendFamily BlendFamily,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        bool RequiresDedicatedOpacityInput,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        string RuleTag);

    private readonly record struct ViewportGenericOverlayFallbackOrderingDefaultsProfile(
        int StackOrder,
        string RuleTag);

    private readonly record struct ViewportLateFallbackDefaultsProfile(
        ViewportLatePassFamily Family,
        ViewportParityBlendFamily BlendFamily,
        int StackOrder,
        int PassPhase,
        ViewportLatePassMaterialMode MaterialMode,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresDedicatedOpacityInput,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        bool AllowsImplicitAlphaFallback,
        bool SuppressLatePassWithoutDedicatedOpacity,
        ViewportLatePassUnderlayProfile UnderlayProfile,
        ViewportLatePassTransparencyMode TransparencyMode,
        int SortLayerTieBreaker,
        int CompositionTieBreaker,
        string RuleTag,
        string RelationTag);

    private readonly record struct ViewportLateFallbackOrderingProfile(
        int StackOrder,
        int SortLayerTieBreaker,
        int CompositionTieBreaker,
        string RuleTag);

    private readonly record struct ViewportOverlayLateOrderingDefaultsProfile(
        int ParityOrder,
        int CompositionTieBreaker,
        int SortLayerTieBreaker,
        string RuleTag);

    private readonly record struct ViewportOverlayLateOrderingProfile(
        int ParityOrder,
        int CompositionTieBreaker,
        int SortLayerTieBreaker,
        string RuleTag);

    private readonly record struct ViewportGenericDetailLateProfile(
        ViewportLatePassBlendIntent BlendIntent,
        int StackOrder,
        int PassPhase,
        float AlphaScale,
        Color4 EmissiveColor,
        bool RequiresExplicitOpacityForRenderAlpha,
        bool DisallowViewportColorAlphaSource,
        string RuleTag);

    private readonly record struct ViewportPrimaryPassContract(
        ViewportPrimaryPassFamily Family,
        ViewportPrimaryPassIntent Intent,
        int StackOrder,
        int RenderStage,
        bool RequiresExplicitOpacityAuthority,
        bool DisallowViewportColorAlphaFallback,
        bool SuppressLitEmissive,
        float AlphaScaleMultiplier,
        bool AllowNormalMap,
        bool AllowSpecularMap,
        float DiffuseScale,
        float AmbientScale,
        float EmissiveScale,
        float SpecularScale,
        float SpecularShininessMultiplier,
        string ContractTag);

    private readonly record struct ViewportPrimaryAlphaProfile(
        bool RenderAlphaMap,
        CanonicalTexture? AlphaSourceTexture,
        string RuleTag);

    private readonly record struct ViewportPrimaryFamilyProfile(
        ViewportPrimaryPassFamily Family,
        OverlayMaterialPassVariant PassVariant,
        ViewportPrimaryPassIntent Intent,
        int StackOrder,
        int RenderStage,
        bool RequiresExplicitOpacityAuthority,
        bool DisallowViewportColorAlphaFallback,
        bool SuppressLitEmissive,
        float AlphaScaleMultiplier,
        bool AllowNormalMap,
        bool AllowSpecularMap,
        float DiffuseScale,
        float AmbientScale,
        float EmissiveScale,
        float SpecularScale,
        float SpecularShininessMultiplier,
        string ContractTag);

    private readonly record struct ViewportPrimarySurfaceResponseProfile(
        Color4 LitAmbientColor,
        Color4 SpecularColor,
        float SpecularShininess,
        string RuleTag);

    private readonly record struct ViewportPrimaryTextureCompositeProfile(
        ViewportPrimaryTextureCompositeMode Mode,
        Color4? TintColor,
        string RuleTag);

    private readonly record struct ViewportPrimaryMaterialBehaviorProfile(
        bool ForceOpaqueViewportTexture,
        Color4 LitAmbientColor,
        Color4 DefaultLitSpecularColor,
        float DefaultLitSpecularShininess,
        bool RenderShadowMap,
        string RuleTag);

    private readonly record struct ViewportStageInteractionProfile(
        bool IsHelperInspectionStage,
        bool IsCasOverlayDetailStage,
        bool IsCasOverlayHighLayerDetailStage,
        bool IsSimSkintoneOverlayStage,
        bool UseOverlayExecution,
        bool UseSkintoneOverlayExecution,
        bool UsesOverlayViewportColor,
        bool UsesSkintoneViewportColor,
        string RuleTag);

    private readonly record struct ViewportStageRenderRoutingProfile(
        bool RenderDiffuseMap,
        bool RenderEmissiveMap,
        bool RenderAlphaMap,
        string RuleTag);

    private readonly record struct ViewportOverlayStageProfile(
        bool IsCasOverlayDetailStage,
        bool IsCasOverlayHighLayerDetailStage,
        bool IsSimSkintoneOverlayStage,
        bool IsCasOverlayPassStage,
        string RuleTag);

    private readonly record struct ViewportPrimaryExecutionProfile(
        bool AllowAlphaMap,
        bool AllowNormalMap,
        bool AllowSpecularMap,
        bool AllowAutoTangent,
        string RuleTag);

    private readonly record struct ViewportPrimaryPassExecutionPlan(
        ViewportPrimaryPassIntent Intent,
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        byte[]? DiffuseAlphaPngBytes,
        bool RenderNormalMap,
        TextureModel? NormalMap,
        bool RenderSpecularColorMap,
        TextureModel? SpecularColorMap,
        bool EnableAutoTangent,
        string RuleTag);

    private readonly record struct ViewportPrimaryPassMaterialPlan(
        ViewportPrimaryPassContract Contract,
        ViewportPrimaryPassExecutionPlan ExecutionPlan,
        ViewportPrimaryPassResponsePlan ResponsePlan,
        ViewportPrimaryPassApplicationPlan ApplicationPlan,
        Color4 DiffuseColor,
        Color4 AmbientColor,
        Color4 EmissiveColor,
        Color4 SpecularColor,
        float SpecularShininess,
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        bool RenderNormalMap,
        TextureModel? NormalMap,
        bool RenderSpecularColorMap,
        TextureModel? SpecularColorMap,
        bool EnableAutoTangent,
        bool RenderShadowMap,
        UVTransform UVTransform);

    private readonly record struct ViewportFallbackMaterialPlan(
        ViewportFallbackMaterialPathProfile PathProfile,
        Color4 DiffuseColor,
        Color4 AmbientColor,
        Color4 EmissiveColor,
        Color4 SpecularColor,
        float SpecularShininess,
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        bool RenderNormalMap,
        TextureModel? NormalMap,
        bool RenderSpecularColorMap,
        TextureModel? SpecularColorMap,
        bool EnableAutoTangent,
        bool RenderShadowMap,
        UVTransform UVTransform,
        string RuleTag);

    private readonly record struct ViewportFallbackMaterialPathProfile(
        ViewportFallbackMaterialPathKind Kind,
        string RuleTag);

    private readonly record struct ViewportPrimaryPassResponsePlan(
        Color4 DiffuseColor,
        Color4 AmbientColor,
        Color4 EmissiveColor,
        Color4 SpecularColor,
        float SpecularShininess,
        string RuleTag);

    private readonly record struct ViewportPrimaryPassApplicationPlan(
        bool RenderDiffuseMap,
        TextureModel? DiffuseMap,
        bool RenderEmissiveMap,
        TextureModel? EmissiveMap,
        bool RenderDiffuseAlphaMap,
        TextureModel? DiffuseAlphaMap,
        bool RenderNormalMap,
        TextureModel? NormalMap,
        bool RenderSpecularColorMap,
        TextureModel? SpecularColorMap,
        bool EnableAutoTangent,
        bool RenderShadowMap,
        string RuleTag);

    private enum ViewportPrimaryPassFamily
    {
        None = 0,
        SkintoneBase = 1,
        OverlayBase = 2,
        HighLayerOverlayBase = 3,
        HelperProjective = 4,
        HelperLayered = 5,
        HelperUtility = 6
    }

    private enum ViewportPrimaryPassIntent
    {
        None = 0,
        SkintoneCarrier = 1,
        OverlayCarrier = 2,
        HighLayerCarrier = 3,
        HelperProjectiveCarrier = 4,
        HelperLayeredCarrier = 5,
        HelperUtilityCarrier = 6
    }

    private enum ViewportPrimaryTextureCompositeMode
    {
        None = 0,
        TintedLayered = 1,
        SwatchMasked = 2
    }

    private enum OverlayMaterialPassVariant
    {
        Primary = 0,
        SkintoneBasePrimary = 1,
        OverlayBasePrimary = 2,
        HighLayerBasePrimary = 3,
        HelperProjectivePrimary = 4,
        HelperLayeredPrimary = 5,
        HelperUtilityPrimary = 6,
        WornEmissiveLate = 7,
        HighLayerEmissiveLate = 8,
        CosmeticDetailLate = 9,
        DefaultDetailLate = 10,
        OrderedDetailLate = 11,
        SkintoneLate = 12,
        MakeupPrimaryLate = 13,
        GrayscaleLate = 14,
        MakeupSecondaryLate = 15
    }

    private enum ViewportLatePassBlendIntent
    {
        None = 0,
        DetailMasked = 1,
        DetailSoft = 2,
        DetailDefault = 3,
        OrderedSoft = 4,
        EmissiveSoft = 5,
        EmissiveRestricted = 6,
        SkintoneSoft = 7,
        SkintoneMasked = 8,
        MakeupOpacityPrimary = 9,
        DetailGrayscale = 10,
        MakeupOpacitySecondary = 11
    }

    private enum ViewportLatePassFamily
    {
        None = 0,
        Skintone = 1,
        GenericDetail = 2,
        MakeupPrimary = 3,
        GrayscaleDetail = 4,
        MakeupSecondary = 5,
        EmissiveSoft = 6,
        EmissiveRestricted = 7
    }

    private enum ViewportLatePassTransparencyMode
    {
        NotTransparent = 0,
        AlwaysTransparent = 1,
        TransparentWithExplicitOpacity = 2
    }

    private enum ViewportParityOpacityAuthority
    {
        None = 0,
        TextureOrImplicitAlpha = 1,
        GrayscaleOverlay = 2,
        MakeupOpacityPrimary = 3,
        MakeupOpacitySecondary = 4,
        SkintoneOpacity = 5
    }

    private enum ViewportParityBlendFamily
    {
        None = 0,
        GenericOverlay = 1,
        SkintoneOverlay = 2,
        MakeupOpacityPrimary = 3,
        GrayscaleOverlay = 4,
        MakeupOpacitySecondary = 5
    }

    private enum ViewportLatePassMaterialMode
    {
        EmissiveOnly = 0,
        DiffuseOverlay = 1,
        DiffuseGrayscale = 2
    }

    private enum ViewportLatePassMaterialPathKind
    {
        OrdinaryDiffuseOverlay = 0,
        OrdinaryGrayscaleOverlay = 1,
        SkintoneOverlay = 2,
        SoftEmissiveOverlay = 3,
        HighLayerEmissiveOverlay = 4
    }

    private enum ViewportFallbackMaterialPathKind
    {
        GenericSurface = 0,
        OverlayStage = 1,
        SkintoneOverlayStage = 2,
        HelperInspection = 3
    }

    private enum ViewportTransparencyKind
    {
        Opaque = 0,
        ObjectGlass = 1,
        ThresholdCutout = 2,
        AlphaBlended = 3,
        SimGlass = 4,
        Generic = 5
    }

    private static ViewportTransparencyKind DetermineViewportTransparencyKind(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (material is null || IsNonVisualViewportMaterial(material))
        {
            return ViewportTransparencyKind.Opaque;
        }

        // Current evidence order is object-glass -> threshold/cutout -> AlphaBlended -> SimGlass.
        if (MaterialMetadataContains(material, "GlassForObjectsTranslucent") ||
            (MaterialMetadataContains(material, "glass") && !MaterialMetadataContains(material, "SimGlass")))
        {
            return ViewportTransparencyKind.ObjectGlass;
        }

        if (MaterialMetadataContains(material, "AlphaCutout", "cutout", "AlphaThresholdMask", "AlphaMaskThreshold") ||
            string.Equals(material.AlphaMode, "alpha-test-or-blend", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(material.AlphaTextureSlot) ||
            textures.Any(texture =>
                texture.Semantic == CanonicalTextureSemantic.Opacity ||
                texture.Slot.Contains("alpha", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("opacity", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("mask", StringComparison.OrdinalIgnoreCase) ||
                texture.Slot.Contains("cutout", StringComparison.OrdinalIgnoreCase)))
        {
            return ViewportTransparencyKind.ThresholdCutout;
        }

        if (MaterialMetadataContains(material, "AlphaBlended"))
        {
            return ViewportTransparencyKind.AlphaBlended;
        }

        if (MaterialMetadataContains(material, "SimGlass"))
        {
            return ViewportTransparencyKind.SimGlass;
        }

        return material.IsTransparent || textures.Any(TextureSupportsAlpha)
            ? ViewportTransparencyKind.Generic
            : ViewportTransparencyKind.Opaque;
    }

    private static bool MaterialMetadataContains(CanonicalMaterial material, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if ((material.ShaderFamily?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (material.ShaderName?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (material.DecodeStrategy?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (material.AlphaMode?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (material.Approximation?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }

    private static CanonicalTexture? SelectOpacityTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        string? explicitAlphaSlot)
    {
        if (textures.Count == 0)
        {
            return TextureSupportsAlpha(baseColorTexture) ? baseColorTexture : null;
        }

        var scope = CreateScopedMaterial(material, textures, explicitAlphaSlot);
        return SelectOpacityTexture(scope, baseColorTexture);
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

        return true;
    }

    private static CanonicalTexture? SelectEmissiveTexture(CanonicalMaterial? material, CanonicalTexture? baseColorTexture, CanonicalTexture? layeredColorTexture)
    {
        if (material is null || material.Textures.Count == 0)
        {
            return null;
        }

        return SelectExplicitEmissiveTexture(material.Textures)
            ?? (material.LayeredTextureSlots?.Any(slot => slot.Equals("emissive", StringComparison.OrdinalIgnoreCase)) == true
                ? baseColorTexture
                : null)
            ?? (layeredColorTexture is not null &&
                !string.Equals(material.AlphaTextureSlot, layeredColorTexture.Slot, StringComparison.OrdinalIgnoreCase)
                ? layeredColorTexture
                : null);
    }

    private static CanonicalTexture? SelectExplicitEmissiveTexture(IReadOnlyList<CanonicalTexture> textures) =>
        textures.FirstOrDefault(texture => texture.Semantic == CanonicalTextureSemantic.Emissive)
        ?? textures.FirstOrDefault(texture =>
            texture.Slot.Contains("emiss", StringComparison.OrdinalIgnoreCase) ||
            texture.Slot.Contains("glow", StringComparison.OrdinalIgnoreCase));

    private static CanonicalTexture? SelectEmissiveTexture(
        CanonicalMaterial? material,
        IReadOnlyList<CanonicalTexture> textures,
        CanonicalTexture? baseColorTexture,
        CanonicalTexture? layeredColorTexture)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = CreateScopedMaterial(material, textures);
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

    private static CanonicalTexture? SelectNormalTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = CreateScopedMaterial(material, textures);
        return SelectNormalTexture(scope);
    }

    private static CanonicalTexture? SelectViewportNormalTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (IsCasOverlayHighLayerDetailStage(material))
        {
            return null;
        }

        return SelectNormalTexture(material, textures);
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

    private static CanonicalTexture? SelectSpecularTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = CreateScopedMaterial(material, textures);
        return SelectSpecularTexture(scope);
    }

    private static CanonicalTexture? SelectViewportSpecularTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (IsCasOverlayHighLayerDetailStage(material))
        {
            return null;
        }

        return SelectSpecularTexture(material, textures);
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

    private static CanonicalTexture? SelectLayeredColorTexture(CanonicalMaterial? material, IReadOnlyList<CanonicalTexture> textures)
    {
        if (textures.Count == 0)
        {
            return null;
        }

        var scope = CreateScopedMaterial(material, textures);
        return SelectLayeredColorTexture(scope);
    }

}
