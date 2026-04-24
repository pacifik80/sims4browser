using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using Microsoft.UI.Xaml;
using Sims4ResourceExplorer.App.ViewModels;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.App;

public sealed partial class MainWindow
{
    private static IReadOnlyList<ViewportRenderPassPlan> BuildViewportRenderPassPlans(
        CanonicalScene scene,
        SceneRenderMode renderMode,
        string? selectedSlot)
    {
        var sceneLatePassFamilies = BuildViewportSceneLatePassFamilies(scene, renderMode, selectedSlot);
        return scene.Meshes
            .Select((mesh, index) => new
            {
                Mesh = mesh,
                OriginalIndex = index,
                Material = mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.Materials.Count
                    ? scene.Materials[mesh.MaterialIndex]
                    : null
            })
            .SelectMany(entry =>
            {
                var secondaryPassVariants = GetViewportSecondaryPassVariants(entry.Material, renderMode, selectedSlot)
                    .Select(passVariant => new
                    {
                        PassVariant = passVariant,
                        SceneRelationProfile = BuildViewportLatePassSceneRelationProfile(
                            sceneLatePassFamilies,
                            entry.Material,
                            passVariant)
                    })
                    .Where(passEntry => ShouldIncludeViewportSecondaryPass(
                        scene,
                        entry.Material,
                        passEntry.PassVariant,
                        passEntry.SceneRelationProfile))
                    .ToArray();
                var primaryPassVariant = GetViewportPrimaryPassVariant(entry.Material);
                var passVariants = new List<(OverlayMaterialPassVariant PassVariant, ViewportLatePassSceneRelationProfile? SceneRelationProfile)>(secondaryPassVariants.Length + 1);
                if (ShouldIncludeViewportPrimaryPass(entry.Material, renderMode, selectedSlot, primaryPassVariant))
                {
                    passVariants.Add((primaryPassVariant, null));
                }

                passVariants.AddRange(secondaryPassVariants.Select(passEntry => (passEntry.PassVariant, (ViewportLatePassSceneRelationProfile?)passEntry.SceneRelationProfile)));
                if (passVariants.Count == 0)
                {
                    return Enumerable.Empty<ViewportRenderPassPlan>();
                }

                var resolvedRenderPassPlans = new List<ViewportRenderPassPlan>(passVariants.Count);
                foreach (var passEntry in passVariants)
                {
                    var resolvedRenderPassContract = BuildViewportResolvedRenderPassContract(
                        scene,
                        entry.Mesh.MaterialIndex,
                        entry.Material,
                        renderMode,
                        selectedSlot,
                        passEntry.PassVariant,
                        passEntry.SceneRelationProfile);
                    if (resolvedRenderPassContract is not { } contract)
                    {
                        continue;
                    }

                    resolvedRenderPassPlans.Add(new ViewportRenderPassPlan(
                        Mesh: entry.Mesh,
                        OriginalIndex: entry.OriginalIndex,
                        Material: entry.Material,
                        PassVariant: passEntry.PassVariant,
                        OrderingProfile: contract.OrderingProfile,
                        ParticipationProfile: contract.ParticipationProfile,
                        PrimaryStackOrder: contract.PrimaryStackOrder,
                        ParityProfile: contract.ParityProfile,
                        ParityStackOrder: contract.ParityStackOrder,
                        LatePassSceneRelationProfile: passEntry.SceneRelationProfile,
                        SlotCategoryOrder: contract.SlotCategoryOrder,
                        FinalOrderProfile: contract.FinalOrderProfile,
                        IsTransparent: contract.ParticipationProfile.IsTransparent,
                        RenderWireframe: contract.ParticipationProfile.RenderWireframe));
                }

                return resolvedRenderPassPlans;
            })
            .OrderBy(entry => entry.OrderingProfile.RenderStage)
            .ThenBy(entry => entry.OrderingProfile.PassBucket)
            .ThenBy(entry => entry.OrderingProfile.PassPhase)
            .ThenBy(entry => entry.PrimaryStackOrder)
            .ThenBy(entry => entry.ParityProfile.FamilyOrder)
            .ThenBy(entry => entry.ParityStackOrder)
            .ThenBy(entry => entry.ParityProfile.CompositionOrder)
            .ThenBy(entry => entry.FinalOrderProfile.SortLayerTieBreaker)
            .ThenBy(entry => entry.SlotCategoryOrder)
            .ThenBy(entry => entry.FinalOrderProfile.CompositionTieBreaker)
            .ThenBy(entry => entry.OriginalIndex)
            .ToArray();
    }

    private static ViewportResolvedRenderPassContract? BuildViewportResolvedRenderPassContract(
        CanonicalScene scene,
        int materialIndex,
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        string? selectedSlot,
        OverlayMaterialPassVariant passVariant,
        ViewportLatePassSceneRelationProfile? sceneRelationProfile)
    {
        var participationProfile = BuildViewportRenderPassParticipationProfile(
            scene,
            materialIndex,
            material,
            renderMode,
            selectedSlot,
            passVariant,
            sceneRelationProfile);
        if (!participationProfile.IncludePass)
        {
            return null;
        }

        var orderingProfile = BuildViewportRenderPassOrderingProfile(material, passVariant);
        var finalOrderProfile = BuildViewportRenderPassFinalOrderProfile(material, passVariant);
        var parityProfile = BuildViewportParityRuleProfile(material, passVariant);
        var primaryStackOrder = IsViewportPrimaryPassVariant(passVariant)
            ? BuildViewportPrimaryPassContract(material, passVariant).StackOrder
            : 0;
        return new ViewportResolvedRenderPassContract(
            OrderingProfile: orderingProfile,
            ParticipationProfile: participationProfile,
            PrimaryStackOrder: primaryStackOrder,
            ParityProfile: parityProfile,
            ParityStackOrder: sceneRelationProfile?.EffectiveStackOrder
                ?? GetViewportParityStackOrder(material, passVariant),
            SlotCategoryOrder: GetViewportCasSlotCategoryOrder(material?.CasPartSlotCategory),
            FinalOrderProfile: finalOrderProfile,
            RuleTag: string.Join("|", new[]
                {
                    participationProfile.RuleTag,
                    parityProfile.RuleTag,
                    orderingProfile.RuleTag,
                    finalOrderProfile.RuleTag
                }
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)));
    }

    private static ViewportRenderPassParticipationProfile BuildViewportRenderPassParticipationProfile(
        CanonicalScene scene,
        int materialIndex,
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        string? selectedSlot,
        OverlayMaterialPassVariant passVariant,
        ViewportLatePassSceneRelationProfile? sceneRelationProfile)
    {
        if (IsViewportPrimaryPassVariant(passVariant))
        {
            var includePass = ShouldIncludeViewportPrimaryPass(material, renderMode, selectedSlot, passVariant);
            return new ViewportRenderPassParticipationProfile(
                IncludePass: includePass,
                IsTransparent: includePass && IsViewportPrimaryPassTransparent(scene, materialIndex, passVariant, selectedSlot),
                RenderWireframe: includePass && renderMode == SceneRenderMode.Wireframe,
                RuleTag: $"{BuildViewportPrimaryPassContract(material, passVariant).ContractTag}|render-pass-participation");
        }

        var includeSecondaryPass = sceneRelationProfile is { } resolvedSceneRelationProfile &&
            ShouldIncludeViewportSecondaryPass(scene, material, passVariant, resolvedSceneRelationProfile);
        return new ViewportRenderPassParticipationProfile(
            IncludePass: includeSecondaryPass,
            IsTransparent: includeSecondaryPass && IsViewportLatePassTransparent(material, passVariant, sceneRelationProfile),
            RenderWireframe: false,
            RuleTag: sceneRelationProfile is { } resolvedSceneRelationProfileForRuleTag
                ? $"{resolvedSceneRelationProfileForRuleTag.RuleTag}|render-pass-participation"
                : $"secondary|{passVariant}|render-pass-participation");
    }

    private static ViewportRenderPassOrderingProfile BuildViewportRenderPassOrderingProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
        => new(
            RenderStage: GetViewportMaterialRenderStage(material, passVariant),
            PassBucket: GetViewportMaterialPassBucket(material),
            PassPhase: GetViewportMaterialPassPhase(material, passVariant),
            RuleTag: IsViewportPrimaryPassVariant(passVariant)
                ? $"{BuildViewportPrimaryPassContract(material, passVariant).ContractTag}|render-pass-ordering"
                : $"{BuildViewportParityRuleProfile(material, passVariant).RuleTag}|render-pass-ordering");

    private static ViewportRenderPassFinalOrderProfile BuildViewportRenderPassFinalOrderProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        var overlayLateOrderingProfile = BuildViewportOverlayLateOrderingProfile(material, passVariant);
        if (overlayLateOrderingProfile is { } resolvedOverlayLateOrderingProfile)
        {
            return new ViewportRenderPassFinalOrderProfile(
                SortLayerTieBreaker: resolvedOverlayLateOrderingProfile.SortLayerTieBreaker,
                CompositionTieBreaker: resolvedOverlayLateOrderingProfile.CompositionTieBreaker,
                RuleTag: resolvedOverlayLateOrderingProfile.RuleTag);
        }

        return new ViewportRenderPassFinalOrderProfile(
            SortLayerTieBreaker: material?.SortLayer ?? 0,
            CompositionTieBreaker: material?.CompositionMethod ?? 0,
            RuleTag: "raw-material-order");
    }

    private static OverlayMaterialPassVariant ResolveViewportPrimaryPassVariant(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant = OverlayMaterialPassVariant.Primary) =>
        passVariant switch
        {
            OverlayMaterialPassVariant.SkintoneBasePrimary or
            OverlayMaterialPassVariant.OverlayBasePrimary or
            OverlayMaterialPassVariant.HighLayerBasePrimary or
            OverlayMaterialPassVariant.HelperProjectivePrimary or
            OverlayMaterialPassVariant.HelperLayeredPrimary or
            OverlayMaterialPassVariant.HelperUtilityPrimary => passVariant,
            _ => BuildViewportPrimaryFamilyProfile(ResolveViewportPrimaryPassFamily(material))?.PassVariant ??
                OverlayMaterialPassVariant.Primary
        };

    private static OverlayMaterialPassVariant GetViewportPrimaryPassVariant(CanonicalMaterial? material) =>
        ResolveViewportPrimaryPassVariant(material);

    private static bool IsViewportPrimaryPassVariant(OverlayMaterialPassVariant passVariant) =>
        passVariant is OverlayMaterialPassVariant.Primary or
            OverlayMaterialPassVariant.SkintoneBasePrimary or
            OverlayMaterialPassVariant.OverlayBasePrimary or
            OverlayMaterialPassVariant.HighLayerBasePrimary or
            OverlayMaterialPassVariant.HelperProjectivePrimary or
            OverlayMaterialPassVariant.HelperLayeredPrimary or
            OverlayMaterialPassVariant.HelperUtilityPrimary;

    private static ViewportPrimaryPassFamily ResolveViewportPrimaryPassFamily(CanonicalMaterial? material) =>
        ResolveViewportPrimaryPassFamilyFromStage(material?.PreviewCompositorStage);

    private static ViewportPrimaryPassFamily ResolveViewportPrimaryPassFamilyFromStage(string? stage) =>
        BuildViewportPrimaryFamilyProfileFromStage(stage)?.Family ?? ViewportPrimaryPassFamily.None;

    private static ViewportPrimaryFamilyProfile? BuildViewportPrimaryFamilyProfileFromStage(string? stage) =>
        stage switch
        {
            "sim-skintone-base" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.SkintoneBase),
            "cas-overlay-base" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.OverlayBase),
            "cas-overlay-highlayer-base" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.HighLayerOverlayBase),
            "helper-projective" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.HelperProjective),
            "helper-layered" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.HelperLayered),
            "helper-utility" => BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily.HelperUtility),
            _ => null
        };

    private static ViewportPrimaryPassFamily ResolveViewportPrimaryPassFamily(OverlayMaterialPassVariant passVariant) =>
        passVariant switch
        {
            OverlayMaterialPassVariant.SkintoneBasePrimary => ViewportPrimaryPassFamily.SkintoneBase,
            OverlayMaterialPassVariant.OverlayBasePrimary => ViewportPrimaryPassFamily.OverlayBase,
            OverlayMaterialPassVariant.HighLayerBasePrimary => ViewportPrimaryPassFamily.HighLayerOverlayBase,
            OverlayMaterialPassVariant.HelperProjectivePrimary => ViewportPrimaryPassFamily.HelperProjective,
            OverlayMaterialPassVariant.HelperLayeredPrimary => ViewportPrimaryPassFamily.HelperLayered,
            OverlayMaterialPassVariant.HelperUtilityPrimary => ViewportPrimaryPassFamily.HelperUtility,
            _ => ViewportPrimaryPassFamily.None
        };

    private static ViewportPrimaryFamilyProfile? BuildViewportPrimaryFamilyProfile(ViewportPrimaryPassFamily family) =>
        family switch
        {
            ViewportPrimaryPassFamily.SkintoneBase => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.SkintoneBase,
                PassVariant: OverlayMaterialPassVariant.SkintoneBasePrimary,
                Intent: ViewportPrimaryPassIntent.SkintoneCarrier,
                StackOrder: 10,
                RenderStage: 5,
                RequiresExplicitOpacityAuthority: true,
                DisallowViewportColorAlphaFallback: true,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 0.9f,
                AllowNormalMap: true,
                AllowSpecularMap: false,
                DiffuseScale: 0.98f,
                AmbientScale: 1.02f,
                EmissiveScale: 1f,
                SpecularScale: 0.82f,
                SpecularShininessMultiplier: 0.78f,
                ContractTag: "skintone-base-primary-stage"),
            ViewportPrimaryPassFamily.OverlayBase => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.OverlayBase,
                PassVariant: OverlayMaterialPassVariant.OverlayBasePrimary,
                Intent: ViewportPrimaryPassIntent.OverlayCarrier,
                StackOrder: 20,
                RenderStage: 35,
                RequiresExplicitOpacityAuthority: true,
                DisallowViewportColorAlphaFallback: true,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 0.96f,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                DiffuseScale: 1f,
                AmbientScale: 1f,
                EmissiveScale: 1f,
                SpecularScale: 1f,
                SpecularShininessMultiplier: 1f,
                ContractTag: "overlay-base-primary-stage"),
            ViewportPrimaryPassFamily.HighLayerOverlayBase => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.HighLayerOverlayBase,
                PassVariant: OverlayMaterialPassVariant.HighLayerBasePrimary,
                Intent: ViewportPrimaryPassIntent.HighLayerCarrier,
                StackOrder: 30,
                RenderStage: 45,
                RequiresExplicitOpacityAuthority: true,
                DisallowViewportColorAlphaFallback: true,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 0.82f,
                AllowNormalMap: true,
                AllowSpecularMap: true,
                DiffuseScale: 0.96f,
                AmbientScale: 0.98f,
                EmissiveScale: 1f,
                SpecularScale: 0.74f,
                SpecularShininessMultiplier: 0.72f,
                ContractTag: "highlayer-base-primary-stage"),
            ViewportPrimaryPassFamily.HelperProjective => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.HelperProjective,
                PassVariant: OverlayMaterialPassVariant.HelperProjectivePrimary,
                Intent: ViewportPrimaryPassIntent.HelperProjectiveCarrier,
                StackOrder: 70,
                RenderStage: 70,
                RequiresExplicitOpacityAuthority: false,
                DisallowViewportColorAlphaFallback: false,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 1f,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                DiffuseScale: 1.04f,
                AmbientScale: 0.94f,
                EmissiveScale: 1f,
                SpecularScale: 0f,
                SpecularShininessMultiplier: 0f,
                ContractTag: "helper-projective-primary-stage"),
            ViewportPrimaryPassFamily.HelperLayered => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.HelperLayered,
                PassVariant: OverlayMaterialPassVariant.HelperLayeredPrimary,
                Intent: ViewportPrimaryPassIntent.HelperLayeredCarrier,
                StackOrder: 75,
                RenderStage: 75,
                RequiresExplicitOpacityAuthority: false,
                DisallowViewportColorAlphaFallback: false,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 1f,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                DiffuseScale: 1.02f,
                AmbientScale: 0.96f,
                EmissiveScale: 1f,
                SpecularScale: 0f,
                SpecularShininessMultiplier: 0f,
                ContractTag: "helper-layered-primary-stage"),
            ViewportPrimaryPassFamily.HelperUtility => new ViewportPrimaryFamilyProfile(
                Family: ViewportPrimaryPassFamily.HelperUtility,
                PassVariant: OverlayMaterialPassVariant.HelperUtilityPrimary,
                Intent: ViewportPrimaryPassIntent.HelperUtilityCarrier,
                StackOrder: 80,
                RenderStage: 80,
                RequiresExplicitOpacityAuthority: false,
                DisallowViewportColorAlphaFallback: false,
                SuppressLitEmissive: true,
                AlphaScaleMultiplier: 1f,
                AllowNormalMap: false,
                AllowSpecularMap: false,
                DiffuseScale: 1.03f,
                AmbientScale: 0.95f,
                EmissiveScale: 1f,
                SpecularScale: 0f,
                SpecularShininessMultiplier: 0f,
                ContractTag: "helper-utility-primary-stage"),
            _ => null
        };

    private static bool IsHelperPrimaryPassFamily(ViewportPrimaryPassFamily family) =>
        family is
            ViewportPrimaryPassFamily.HelperProjective or
            ViewportPrimaryPassFamily.HelperLayered or
            ViewportPrimaryPassFamily.HelperUtility;

    private static HashSet<ViewportLatePassFamily> BuildViewportSceneLatePassFamilies(
        CanonicalScene scene,
        SceneRenderMode renderMode,
        string? selectedSlot)
    {
        var families = new HashSet<ViewportLatePassFamily>();
        foreach (var material in scene.Materials)
        {
            foreach (var passVariant in GetViewportSecondaryPassVariants(material, renderMode, selectedSlot))
            {
                var family = BuildViewportLatePassFamilyContract(material, passVariant).Family;
                if (family != ViewportLatePassFamily.None)
                {
                    families.Add(family);
                }
            }
        }

        return families;
    }

    private static IReadOnlyList<OverlayMaterialPassVariant> GetViewportSecondaryPassVariants(
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        string? selectedSlot)
    {
        if (material is null ||
            !string.IsNullOrWhiteSpace(selectedSlot) ||
            renderMode is not (SceneRenderMode.LitTexture or SceneRenderMode.FlatTexture))
        {
            return [];
        }

        var overlayStageProfile = BuildViewportOverlayStageProfile(material);
        if (overlayStageProfile.IsSimSkintoneOverlayStage)
        {
            return [OverlayMaterialPassVariant.SkintoneLate];
        }

        if (!overlayStageProfile.IsCasOverlayDetailStage)
        {
            return [];
        }

        var passIntent = BuildOverlayCompositorPolicy(material).PassIntent;
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(passIntent);
        if (overlayStageProfile.IsCasOverlayDetailStage)
        {
            var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
            return compositionRuleProfile is { } ruleProfile
                ? [ruleProfile.PassVariant]
                : wornLaneProfile is { } resolvedWornLaneProfile
                ? [resolvedWornLaneProfile.PassVariant]
                : emissiveFamilyProfile is { } resolvedEmissiveFamilyProfile
                ? [resolvedEmissiveFamilyProfile.PassVariant]
                : passIntent switch
            {
                OverlayPassIntent.CosmeticDetail => [OverlayMaterialPassVariant.CosmeticDetailLate],
                OverlayPassIntent.DefaultDetail => [OverlayMaterialPassVariant.DefaultDetailLate],
                OverlayPassIntent.OrderedDetail => [OverlayMaterialPassVariant.OrderedDetailLate],
                _ => []
            };
        }

        return wornLaneProfile is { } resolvedFallbackWornLaneProfile
            ? [resolvedFallbackWornLaneProfile.PassVariant]
            : emissiveFamilyProfile is { } resolvedFallbackEmissiveFamilyProfile
            ? [resolvedFallbackEmissiveFamilyProfile.PassVariant]
            : [];
    }

    private static bool ShouldIncludeViewportSecondaryPass(
        CanonicalScene scene,
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant,
        ViewportLatePassSceneRelationProfile sceneRelationProfile)
    {
        if (sceneRelationProfile.SuppressFromPassPlanning)
        {
            return false;
        }

        var underlayProfile = sceneRelationProfile.FamilyContract.UnderlayProfile;
        if (!underlayProfile.RequiresUnderlay)
        {
            return true;
        }

        return underlayProfile.RequiredStageFamilies.Any(stage => SceneContainsViewportStage(scene, stage));
    }

    private static int GetViewportMaterialPassPhase(CanonicalMaterial? material, OverlayMaterialPassVariant passVariant) =>
        IsViewportPrimaryPassVariant(passVariant)
            ? 0
            : GetViewportParityPassPhase(
                material,
                GetViewportLatePassBlendIntent(material, passVariant),
                BuildViewportParityRuleProfile(material, passVariant));

    // Source-backed parity floor from the compositor research docs:
    // keep skintone late passes ahead of CAS overlay late passes, then order CAS overlay
    // compositor rows by CompositionMethod before SortLayer. Methods 2/3/4 are carried as
    // explicit parity-rule families because the research packet treats them as distinct runtime lanes.
    private static ViewportCompositionRuleProfile? BuildViewportCompositionRuleProfile(CanonicalMaterial? material)
    {
        if (!IsCosmeticCompositionOverlayLane(material))
        {
            return null;
        }

        var compositionDefaultsProfile = BuildViewportCompositionLaneDefaultsProfile(material?.CompositionMethod);
        return compositionDefaultsProfile is { } resolvedCompositionDefaultsProfile
            ? new ViewportCompositionRuleProfile(
                PassVariant: resolvedCompositionDefaultsProfile.PassVariant,
                Family: resolvedCompositionDefaultsProfile.Family,
                BlendIntent: resolvedCompositionDefaultsProfile.BlendIntent,
                OpacityAuthority: resolvedCompositionDefaultsProfile.OpacityAuthority,
                BlendFamily: resolvedCompositionDefaultsProfile.BlendFamily,
                StackOrder: resolvedCompositionDefaultsProfile.StackOrder,
                PassPhase: resolvedCompositionDefaultsProfile.PassPhase,
                MaterialMode: resolvedCompositionDefaultsProfile.MaterialMode,
                AlphaScale: resolvedCompositionDefaultsProfile.AlphaScale,
                EmissiveColor: resolvedCompositionDefaultsProfile.EmissiveColor,
                RequiresDedicatedOpacityInput: resolvedCompositionDefaultsProfile.RequiresDedicatedOpacityInput,
                AllowsImplicitAlphaFallback: resolvedCompositionDefaultsProfile.AllowsImplicitAlphaFallback,
                SuppressLatePassWithoutDedicatedOpacity: resolvedCompositionDefaultsProfile.SuppressLatePassWithoutDedicatedOpacity,
                CompositionOrder: resolvedCompositionDefaultsProfile.CompositionOrder,
                RuleTag: resolvedCompositionDefaultsProfile.RuleTag)
            : null;
    }

    private static ViewportCompositionLaneDefaultsProfile? BuildViewportCompositionLaneDefaultsProfile(int? compositionMethod) =>
        compositionMethod switch
        {
            2 => new ViewportCompositionLaneDefaultsProfile(
                PassVariant: OverlayMaterialPassVariant.MakeupPrimaryLate,
                Family: ViewportLatePassFamily.MakeupPrimary,
                BlendIntent: ViewportLatePassBlendIntent.MakeupOpacityPrimary,
                OpacityAuthority: ViewportParityOpacityAuthority.MakeupOpacityPrimary,
                BlendFamily: ViewportParityBlendFamily.MakeupOpacityPrimary,
                StackOrder: 120,
                PassPhase: 2,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                AlphaScale: 0.85f,
                EmissiveColor: new Color4(0.2f, 0.2f, 0.2f, 1f),
                RequiresDedicatedOpacityInput: true,
                AllowsImplicitAlphaFallback: false,
                SuppressLatePassWithoutDedicatedOpacity: true,
                CompositionOrder: 2,
                RuleTag: "composition-2-makeup-primary"),
            3 => new ViewportCompositionLaneDefaultsProfile(
                PassVariant: OverlayMaterialPassVariant.GrayscaleLate,
                Family: ViewportLatePassFamily.GrayscaleDetail,
                BlendIntent: ViewportLatePassBlendIntent.DetailGrayscale,
                OpacityAuthority: ViewportParityOpacityAuthority.GrayscaleOverlay,
                BlendFamily: ViewportParityBlendFamily.GrayscaleOverlay,
                StackOrder: 130,
                PassPhase: 2,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseGrayscale,
                AlphaScale: 1f,
                EmissiveColor: new Color4(0.16f, 0.16f, 0.16f, 1f),
                RequiresDedicatedOpacityInput: false,
                AllowsImplicitAlphaFallback: true,
                SuppressLatePassWithoutDedicatedOpacity: false,
                CompositionOrder: 3,
                RuleTag: "composition-3-grayscale"),
            4 => new ViewportCompositionLaneDefaultsProfile(
                PassVariant: OverlayMaterialPassVariant.MakeupSecondaryLate,
                Family: ViewportLatePassFamily.MakeupSecondary,
                BlendIntent: ViewportLatePassBlendIntent.MakeupOpacitySecondary,
                OpacityAuthority: ViewportParityOpacityAuthority.MakeupOpacitySecondary,
                BlendFamily: ViewportParityBlendFamily.MakeupOpacitySecondary,
                StackOrder: 140,
                PassPhase: 2,
                MaterialMode: ViewportLatePassMaterialMode.DiffuseOverlay,
                AlphaScale: 1.15f,
                EmissiveColor: new Color4(0.24f, 0.24f, 0.24f, 1f),
                RequiresDedicatedOpacityInput: true,
                AllowsImplicitAlphaFallback: false,
                SuppressLatePassWithoutDedicatedOpacity: true,
                CompositionOrder: 4,
                RuleTag: "composition-4-makeup-secondary"),
            _ => null
        };

    private static OverlayBlendFamily GetOverlayBlendFamily(ViewportCompositionRuleProfile? compositionRuleProfile) =>
        compositionRuleProfile?.BlendFamily switch
        {
            ViewportParityBlendFamily.GrayscaleOverlay => OverlayBlendFamily.GrayscaleDetail,
            ViewportParityBlendFamily.MakeupOpacityPrimary or
            ViewportParityBlendFamily.MakeupOpacitySecondary => OverlayBlendFamily.StraightAlpha,
            _ => OverlayBlendFamily.None
        };

    private static ViewportParityRuleProfile BuildViewportParityRuleProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        var parityRuleDefaultsProfile = BuildViewportParityRuleDefaultsProfile(material, passVariant);
        if (parityRuleDefaultsProfile is { } resolvedParityRuleDefaultsProfile)
        {
            return new ViewportParityRuleProfile(
                resolvedParityRuleDefaultsProfile.FamilyOrder,
                resolvedParityRuleDefaultsProfile.CompositionOrder,
                resolvedParityRuleDefaultsProfile.OpacityAuthority,
                resolvedParityRuleDefaultsProfile.BlendFamily,
                resolvedParityRuleDefaultsProfile.RuleTag);
        }

        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        if (compositionRuleProfile is { } ruleProfile &&
            passVariant == ruleProfile.PassVariant)
        {
            return new ViewportParityRuleProfile(1, ruleProfile.CompositionOrder, ruleProfile.OpacityAuthority, ruleProfile.BlendFamily, ruleProfile.RuleTag);
        }

        var overlayLateOrderingProfile = BuildViewportOverlayLateOrderingProfile(material, passVariant);
        if (overlayLateOrderingProfile is { } resolvedOverlayLateOrderingProfile)
        {
            return new ViewportParityRuleProfile(
                1,
                resolvedOverlayLateOrderingProfile.ParityOrder,
                ViewportParityOpacityAuthority.TextureOrImplicitAlpha,
                ViewportParityBlendFamily.GenericOverlay,
                $"composition-generic|{resolvedOverlayLateOrderingProfile.RuleTag}");
        }

        return new ViewportParityRuleProfile(2, 0, ViewportParityOpacityAuthority.None, ViewportParityBlendFamily.None, "generic");
    }

    private static ViewportParityRuleDefaultsProfile? BuildViewportParityRuleDefaultsProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        if (IsViewportPrimaryPassVariant(passVariant))
        {
            return new ViewportParityRuleDefaultsProfile(
                FamilyOrder: 0,
                CompositionOrder: 0,
                OpacityAuthority: ViewportParityOpacityAuthority.None,
                BlendFamily: ViewportParityBlendFamily.None,
                RuleTag: passVariant switch
                {
                    OverlayMaterialPassVariant.SkintoneBasePrimary => "skintone-base-primary",
                    OverlayMaterialPassVariant.OverlayBasePrimary => "overlay-base-primary",
                    OverlayMaterialPassVariant.HighLayerBasePrimary => "highlayer-base-primary",
                    OverlayMaterialPassVariant.HelperProjectivePrimary => "helper-projective-primary",
                    OverlayMaterialPassVariant.HelperLayeredPrimary => "helper-layered-primary",
                    OverlayMaterialPassVariant.HelperUtilityPrimary => "helper-utility-primary",
                    _ => "primary"
                });
        }

        if (passVariant == OverlayMaterialPassVariant.SkintoneLate)
        {
            return new ViewportParityRuleDefaultsProfile(
                FamilyOrder: 0,
                CompositionOrder: 0,
                OpacityAuthority: ViewportParityOpacityAuthority.SkintoneOpacity,
                BlendFamily: ViewportParityBlendFamily.SkintoneOverlay,
                RuleTag: "skintone-before-cas");
        }

        var overlayLateOrderingProfile = BuildViewportOverlayLateOrderingProfile(material, passVariant);
        if (overlayLateOrderingProfile is { } resolvedOverlayLateOrderingProfile)
        {
            return new ViewportParityRuleDefaultsProfile(
                FamilyOrder: 1,
                CompositionOrder: resolvedOverlayLateOrderingProfile.ParityOrder,
                OpacityAuthority: ViewportParityOpacityAuthority.TextureOrImplicitAlpha,
                BlendFamily: ViewportParityBlendFamily.GenericOverlay,
                RuleTag: $"composition-generic|{resolvedOverlayLateOrderingProfile.RuleTag}");
        }

        if (IsViewportGenericOverlayLatePassVariant(passVariant))
        {
            return new ViewportParityRuleDefaultsProfile(
                FamilyOrder: 2,
                CompositionOrder: 0,
                OpacityAuthority: ViewportParityOpacityAuthority.None,
                BlendFamily: ViewportParityBlendFamily.None,
                RuleTag: "generic");
        }

        return null;
    }

    private static int GetViewportParityStackOrder(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant) =>
        BuildViewportLatePassFamilyContract(material, passVariant).StackOrder;

    private static ViewportLatePassUnderlayProfile BuildViewportLatePassUnderlayProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant) =>
        BuildViewportLatePassFamilyContract(material, passVariant).UnderlayProfile;

    private static bool SceneContainsViewportStage(CanonicalScene scene, string stage) =>
        scene.Materials.Any(material => string.Equals(material.PreviewCompositorStage, stage, StringComparison.OrdinalIgnoreCase));

    private static bool IsViewportLatePassTransparent(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant,
        ViewportLatePassSceneRelationProfile? sceneRelationProfile = null) =>
        (sceneRelationProfile?.EffectiveTransparencyMode ?? BuildViewportLatePassFamilyContract(material, passVariant).TransparencyMode) switch
        {
            ViewportLatePassTransparencyMode.AlwaysTransparent => true,
            ViewportLatePassTransparencyMode.TransparentWithExplicitOpacity => HasExplicitOpacityTexture(material),
            _ => false
        };

    private static ViewportLatePassSceneRelationProfile BuildViewportLatePassSceneRelationProfile(
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies,
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        var familyContract = BuildViewportLatePassFamilyContract(material, passVariant);
        if (familyContract.Family == ViewportLatePassFamily.None)
        {
            return new ViewportLatePassSceneRelationProfile(
                FamilyContract: familyContract,
                HasSkintoneLateSibling: false,
                HasDiffuseLateSibling: false,
                HasEarlierLateSibling: false,
                EffectiveStackOrder: familyContract.StackOrder,
                EffectiveTransparencyMode: familyContract.TransparencyMode,
                AlphaScaleMultiplier: 1f,
                EmissiveScaleMultiplier: 1f,
                SuppressFromPassPlanning: false,
                RuleTag: familyContract.RelationTag);
        }

        var hasSkintoneLateSibling = sceneLatePassFamilies.Contains(ViewportLatePassFamily.Skintone);
        var hasDiffuseLateSibling = sceneLatePassFamilies.Contains(ViewportLatePassFamily.Skintone) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.GenericDetail) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.MakeupPrimary) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.GrayscaleDetail) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.MakeupSecondary);
        var hasEarlierLateSibling = sceneLatePassFamilies
            .Where(family => family != ViewportLatePassFamily.None && family != familyContract.Family)
            .Any(family => GetViewportLatePassFamilyBaseStackOrder(family) < familyContract.StackOrder);
        var coexistenceProfile = BuildViewportLatePassCoexistenceProfile(
            sceneLatePassFamilies,
            material,
            familyContract,
            hasSkintoneLateSibling,
            hasDiffuseLateSibling,
            hasEarlierLateSibling);

        return new ViewportLatePassSceneRelationProfile(
            FamilyContract: familyContract,
            HasSkintoneLateSibling: hasSkintoneLateSibling,
            HasDiffuseLateSibling: hasDiffuseLateSibling,
            HasEarlierLateSibling: hasEarlierLateSibling,
            EffectiveStackOrder: familyContract.StackOrder + coexistenceProfile.StackOrderOffset,
            EffectiveTransparencyMode: coexistenceProfile.EffectiveTransparencyMode,
            AlphaScaleMultiplier: coexistenceProfile.AlphaScaleMultiplier,
            EmissiveScaleMultiplier: coexistenceProfile.EmissiveScaleMultiplier,
            SuppressFromPassPlanning: coexistenceProfile.SuppressFromPassPlanning,
            RuleTag: coexistenceProfile.RuleTag);
    }

    private static ViewportLatePassCoexistenceProfile BuildViewportLatePassCoexistenceProfile(
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies,
        CanonicalMaterial? material,
        ViewportLatePassFamilyContract familyContract,
        bool hasSkintoneLateSibling,
        bool hasDiffuseLateSibling,
        bool hasEarlierLateSibling)
    {
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        var familyAdjustmentProfile = BuildViewportLatePassFamilySceneAdjustmentProfile(
            sceneLatePassFamilies,
            familyContract,
            wornLaneProfile,
            hasSkintoneLateSibling,
            hasDiffuseLateSibling,
            hasEarlierLateSibling);
        var pairwiseCoexistenceProfile = BuildViewportLatePassPairwiseCoexistenceProfile(
            familyContract.Family,
            sceneLatePassFamilies);

        return new ViewportLatePassCoexistenceProfile(
            StackOrderOffset: pairwiseCoexistenceProfile.StackOrderOffset,
            EffectiveTransparencyMode: familyAdjustmentProfile.EffectiveTransparencyMode,
            AlphaScaleMultiplier: familyAdjustmentProfile.AlphaScaleMultiplier * pairwiseCoexistenceProfile.AlphaScaleMultiplier,
            EmissiveScaleMultiplier: familyAdjustmentProfile.EmissiveScaleMultiplier * pairwiseCoexistenceProfile.EmissiveScaleMultiplier,
            SuppressFromPassPlanning: false,
            RuleTag: string.Join("|", new[] { familyAdjustmentProfile.RuleTag, pairwiseCoexistenceProfile.RuleTag }
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)));
    }

    private static ViewportLatePassFamilySceneAdjustmentProfile BuildViewportLatePassFamilySceneAdjustmentProfile(
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies,
        ViewportLatePassFamilyContract familyContract,
        OverlayWornLaneProfile? wornLaneProfile,
        bool hasSkintoneLateSibling,
        bool hasDiffuseLateSibling,
        bool hasEarlierLateSibling)
    {
        var effectiveTransparencyMode = familyContract.TransparencyMode;
        var alphaScaleMultiplier = 1f;
        var emissiveScaleMultiplier = 1f;

        var familyAdjustmentDefaultsProfile = BuildViewportLateFamilySceneAdjustmentDefaultsProfile(
            sceneLatePassFamilies,
            familyContract.Family,
            hasSkintoneLateSibling);
        if (familyAdjustmentDefaultsProfile is { } resolvedFamilyAdjustmentDefaultsProfile)
        {
            alphaScaleMultiplier = resolvedFamilyAdjustmentDefaultsProfile.AlphaScaleMultiplier;
            emissiveScaleMultiplier = resolvedFamilyAdjustmentDefaultsProfile.EmissiveScaleMultiplier;
        }

        switch (familyContract.Family)
        {
            case ViewportLatePassFamily.EmissiveSoft:
            case ViewportLatePassFamily.EmissiveRestricted:
                if (TryApplyViewportLateEmissiveMissingDiffuseFallback(
                    familyContract.Family,
                    wornLaneProfile,
                    hasDiffuseLateSibling,
                    hasEarlierLateSibling,
                    out var resolvedTransparencyMode,
                    out var resolvedAlphaScaleMultiplier,
                    out var resolvedEmissiveScaleMultiplier))
                {
                    effectiveTransparencyMode = resolvedTransparencyMode;
                    alphaScaleMultiplier = resolvedAlphaScaleMultiplier;
                    emissiveScaleMultiplier = resolvedEmissiveScaleMultiplier;
                }

                break;
        }

        return new ViewportLatePassFamilySceneAdjustmentProfile(
            EffectiveTransparencyMode: effectiveTransparencyMode,
            AlphaScaleMultiplier: alphaScaleMultiplier,
            EmissiveScaleMultiplier: emissiveScaleMultiplier,
            RuleTag: familyContract.RelationTag);
    }

    private static ViewportLatePassFamilySceneAdjustmentDefaultsProfile? BuildViewportLateFamilySceneAdjustmentDefaultsProfile(
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies,
        ViewportLatePassFamily family,
        bool hasSkintoneLateSibling)
    {
        var hasOrdinaryLateSibling = sceneLatePassFamilies.Contains(ViewportLatePassFamily.GenericDetail) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.MakeupPrimary) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.GrayscaleDetail) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.MakeupSecondary) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.EmissiveSoft) ||
            sceneLatePassFamilies.Contains(ViewportLatePassFamily.EmissiveRestricted);

        return family switch
        {
            ViewportLatePassFamily.Skintone when hasOrdinaryLateSibling =>
                new ViewportLatePassFamilySceneAdjustmentDefaultsProfile(0.94f, 0.9f, "scene-adjust-skintone"),
            ViewportLatePassFamily.GenericDetail when sceneLatePassFamilies.Contains(ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassFamilySceneAdjustmentDefaultsProfile(0.92f, 0.94f, "scene-adjust-generic-detail"),
            ViewportLatePassFamily.MakeupPrimary when !hasSkintoneLateSibling =>
                new ViewportLatePassFamilySceneAdjustmentDefaultsProfile(0.84f, 0.88f, "scene-adjust-makeup-primary"),
            ViewportLatePassFamily.GrayscaleDetail when sceneLatePassFamilies.Contains(ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassFamilySceneAdjustmentDefaultsProfile(0.96f, 0.92f, "scene-adjust-grayscale-detail"),
            ViewportLatePassFamily.MakeupSecondary when !hasSkintoneLateSibling =>
                new ViewportLatePassFamilySceneAdjustmentDefaultsProfile(0.8f, 0.86f, "scene-adjust-makeup-secondary"),
            _ => null
        };
    }

    private static int GetViewportLatePassFamilyBaseStackOrder(ViewportLatePassFamily family) =>
        BuildViewportLateFamilyBaseDefaultsProfile(family)?.BaseStackOrder
        ?? BuildViewportLateEmissiveFamilyProfile(family)?.BaseStackOrder
        ?? 0;

    private static ViewportLateFamilyBaseDefaultsProfile? BuildViewportLateFamilyBaseDefaultsProfile(
        ViewportLatePassFamily family) =>
        family switch
        {
            ViewportLatePassFamily.Skintone => new ViewportLateFamilyBaseDefaultsProfile(10, "late-family-skintone"),
            ViewportLatePassFamily.GenericDetail => new ViewportLateFamilyBaseDefaultsProfile(100, "late-family-generic-detail"),
            ViewportLatePassFamily.MakeupPrimary => new ViewportLateFamilyBaseDefaultsProfile(120, "late-family-makeup-primary"),
            ViewportLatePassFamily.GrayscaleDetail => new ViewportLateFamilyBaseDefaultsProfile(130, "late-family-grayscale-detail"),
            ViewportLatePassFamily.MakeupSecondary => new ViewportLateFamilyBaseDefaultsProfile(140, "late-family-makeup-secondary"),
            _ => null
        };

    private static ViewportLateEmissiveFamilyProfile? BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily family)
    {
        var defaultsProfile = BuildViewportLateEmissiveFamilyDefaultsProfile(family);
        return defaultsProfile is { } resolvedDefaultsProfile
            ? new ViewportLateEmissiveFamilyProfile(
                Family: family,
                BlendIntent: resolvedDefaultsProfile.BlendIntent,
                PassVariant: resolvedDefaultsProfile.PassVariant,
                BaseStackOrder: resolvedDefaultsProfile.BaseStackOrder,
                PassPhase: resolvedDefaultsProfile.PassPhase,
                MaterialMode: resolvedDefaultsProfile.MaterialMode,
                AlphaScale: resolvedDefaultsProfile.AlphaScale,
                EmissiveColorScale: resolvedDefaultsProfile.EmissiveColorScale,
                TransparencyMode: resolvedDefaultsProfile.TransparencyMode,
                MissingDiffuseTransparencyMode: resolvedDefaultsProfile.MissingDiffuseTransparencyMode,
                MissingDiffuseAlphaScaleMultiplier: resolvedDefaultsProfile.MissingDiffuseAlphaScaleMultiplier,
                MissingDiffuseEmissiveScaleMultiplier: resolvedDefaultsProfile.MissingDiffuseEmissiveScaleMultiplier,
                UnderlayProfile: resolvedDefaultsProfile.UnderlayProfile,
                RequiresExplicitOpacityForRenderAlpha: resolvedDefaultsProfile.RequiresExplicitOpacityForRenderAlpha,
                DisallowViewportColorAlphaSource: resolvedDefaultsProfile.DisallowViewportColorAlphaSource,
                AllowsImplicitAlphaFallback: resolvedDefaultsProfile.AllowsImplicitAlphaFallback,
                FamilyTag: resolvedDefaultsProfile.FamilyTag,
                RelationTag: resolvedDefaultsProfile.RelationTag)
            : null;
    }

    private static ViewportLateEmissiveFamilyDefaultsProfile? BuildViewportLateEmissiveFamilyDefaultsProfile(
        ViewportLatePassFamily family) =>
        family switch
        {
            ViewportLatePassFamily.EmissiveSoft => new ViewportLateEmissiveFamilyDefaultsProfile(
                BlendIntent: ViewportLatePassBlendIntent.EmissiveSoft,
                PassVariant: OverlayMaterialPassVariant.WornEmissiveLate,
                BaseStackOrder: 300,
                PassPhase: 3,
                MaterialMode: ViewportLatePassMaterialMode.EmissiveOnly,
                AlphaScale: 0.68f,
                EmissiveColorScale: 1f,
                TransparencyMode: ViewportLatePassTransparencyMode.TransparentWithExplicitOpacity,
                MissingDiffuseTransparencyMode: ViewportLatePassTransparencyMode.NotTransparent,
                MissingDiffuseAlphaScaleMultiplier: 0f,
                MissingDiffuseEmissiveScaleMultiplier: 0.78f,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(
                    true,
                    ["cas-shell-base", "surface", "surface-layered", "cas-overlay-base", "cas-overlay-highlayer-base"],
                    "requires-emissive-underlay"),
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: false,
                AllowsImplicitAlphaFallback: true,
                FamilyTag: "soft-emissive",
                RelationTag: "emissive-after-detail"),
            ViewportLatePassFamily.EmissiveRestricted => new ViewportLateEmissiveFamilyDefaultsProfile(
                BlendIntent: ViewportLatePassBlendIntent.EmissiveRestricted,
                PassVariant: OverlayMaterialPassVariant.HighLayerEmissiveLate,
                BaseStackOrder: 400,
                PassPhase: 4,
                MaterialMode: ViewportLatePassMaterialMode.EmissiveOnly,
                AlphaScale: 0.54f,
                EmissiveColorScale: 0.82f,
                TransparencyMode: ViewportLatePassTransparencyMode.TransparentWithExplicitOpacity,
                MissingDiffuseTransparencyMode: ViewportLatePassTransparencyMode.NotTransparent,
                MissingDiffuseAlphaScaleMultiplier: 0f,
                MissingDiffuseEmissiveScaleMultiplier: 0.7f,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(
                    true,
                    ["cas-shell-base", "surface", "surface-layered", "cas-overlay-base", "cas-overlay-highlayer-base"],
                    "requires-emissive-underlay"),
                RequiresExplicitOpacityForRenderAlpha: true,
                DisallowViewportColorAlphaSource: true,
                AllowsImplicitAlphaFallback: true,
                FamilyTag: "restricted-emissive",
                RelationTag: "restricted-emissive-last"),
            _ => null
        };

    private static ViewportLateEmissiveFamilyProfile? BuildViewportLateEmissiveFamilyProfile(
        ViewportLatePassBlendIntent blendIntent) =>
        blendIntent switch
        {
            ViewportLatePassBlendIntent.EmissiveSoft => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveSoft),
            ViewportLatePassBlendIntent.EmissiveRestricted => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveRestricted),
            _ => null
        };

    private static ViewportLateEmissiveFamilyProfile? BuildViewportLateEmissiveFamilyProfile(
        OverlayMaterialPassVariant passVariant) =>
        passVariant switch
        {
            OverlayMaterialPassVariant.WornEmissiveLate => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveSoft),
            OverlayMaterialPassVariant.HighLayerEmissiveLate => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveRestricted),
            _ => null
        };

    private static bool IsViewportLateEmissivePassVariant(OverlayMaterialPassVariant passVariant) =>
        BuildViewportLateEmissiveFamilyProfile(passVariant) is not null;

    private static bool IsViewportGenericOverlayLatePassVariant(OverlayMaterialPassVariant passVariant) =>
        passVariant is OverlayMaterialPassVariant.CosmeticDetailLate or
            OverlayMaterialPassVariant.DefaultDetailLate or
            OverlayMaterialPassVariant.OrderedDetailLate ||
            IsViewportLateEmissivePassVariant(passVariant);

    private static ViewportLateEmissiveFamilyProfile? BuildViewportLateEmissiveFamilyProfile(
        OverlayPassIntent passIntent) =>
        passIntent switch
        {
            OverlayPassIntent.WornLayer => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveSoft),
            OverlayPassIntent.HighLayer => BuildViewportLateEmissiveFamilyProfile(ViewportLatePassFamily.EmissiveRestricted),
            _ => null
        };

    private static bool TryApplyViewportLateEmissiveMissingDiffuseFallback(
        ViewportLatePassFamily family,
        OverlayWornLaneProfile? wornLaneProfile,
        bool hasDiffuseLateSibling,
        bool hasEarlierLateSibling,
        out ViewportLatePassTransparencyMode transparencyMode,
        out float alphaScaleMultiplier,
        out float emissiveScaleMultiplier)
    {
        transparencyMode = ViewportLatePassTransparencyMode.NotTransparent;
        alphaScaleMultiplier = 1f;
        emissiveScaleMultiplier = 1f;

        if (hasDiffuseLateSibling && hasEarlierLateSibling)
        {
            return false;
        }

        if (wornLaneProfile is { } resolvedWornLaneProfile &&
            resolvedWornLaneProfile.Family == family)
        {
            transparencyMode = resolvedWornLaneProfile.MissingDiffuseTransparencyMode;
            alphaScaleMultiplier = resolvedWornLaneProfile.MissingDiffuseAlphaScaleMultiplier;
            emissiveScaleMultiplier = resolvedWornLaneProfile.MissingDiffuseEmissiveScaleMultiplier;
            return true;
        }

        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(family);
        if (emissiveFamilyProfile is not { } resolvedEmissiveFamilyProfile)
        {
            return false;
        }

        transparencyMode = resolvedEmissiveFamilyProfile.MissingDiffuseTransparencyMode;
        alphaScaleMultiplier = resolvedEmissiveFamilyProfile.MissingDiffuseAlphaScaleMultiplier;
        emissiveScaleMultiplier = resolvedEmissiveFamilyProfile.MissingDiffuseEmissiveScaleMultiplier;
        return true;
    }

    private static IReadOnlyList<ViewportLatePassPairwiseRelation> BuildViewportLatePassPairwiseRelations(
        ViewportLatePassFamily family,
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies)
    {
        if (family == ViewportLatePassFamily.None)
        {
            return [];
        }

        var relations = new List<ViewportLatePassPairwiseRelation>();
        foreach (var siblingFamily in sceneLatePassFamilies)
        {
            if (siblingFamily is ViewportLatePassFamily.None || siblingFamily == family)
            {
                continue;
            }

            var relation = BuildViewportLatePassPairwiseRelation(family, siblingFamily);
            if (!string.IsNullOrWhiteSpace(relation.RelationTag))
            {
                relations.Add(relation);
            }
        }

        return relations;
    }

    private static ViewportLatePassPairwiseCoexistenceProfile BuildViewportLatePassPairwiseCoexistenceProfile(
        ViewportLatePassFamily family,
        IReadOnlySet<ViewportLatePassFamily> sceneLatePassFamilies)
    {
        var stackOrderOffset = 0;
        var alphaScaleMultiplier = 1f;
        var emissiveScaleMultiplier = 1f;
        var relationTags = new List<string>();
        foreach (var relation in BuildViewportLatePassPairwiseRelations(family, sceneLatePassFamilies))
        {
            stackOrderOffset += relation.StackOrderOffset;
            alphaScaleMultiplier *= relation.AlphaScaleMultiplier;
            emissiveScaleMultiplier *= relation.EmissiveScaleMultiplier;
            relationTags.Add(relation.RelationTag);
        }

        return new ViewportLatePassPairwiseCoexistenceProfile(
            StackOrderOffset: stackOrderOffset,
            AlphaScaleMultiplier: alphaScaleMultiplier,
            EmissiveScaleMultiplier: emissiveScaleMultiplier,
            RuleTag: string.Join("|", relationTags.Distinct(StringComparer.Ordinal)));
    }

    private static ViewportLatePassPairwiseRelation BuildViewportLatePassPairwiseRelation(
        ViewportLatePassFamily family,
        ViewportLatePassFamily siblingFamily)
    {
        var relationDefaultsProfile = BuildViewportLatePairwiseRelationDefaultsProfile(family, siblingFamily);
        return relationDefaultsProfile is { } resolvedRelationDefaultsProfile
            ? new ViewportLatePassPairwiseRelation(
                SiblingFamily: siblingFamily,
                StackOrderOffset: resolvedRelationDefaultsProfile.StackOrderOffset,
                AlphaScaleMultiplier: resolvedRelationDefaultsProfile.AlphaScaleMultiplier,
                EmissiveScaleMultiplier: resolvedRelationDefaultsProfile.EmissiveScaleMultiplier,
                RelationTag: resolvedRelationDefaultsProfile.RelationTag)
            : default;
    }

    private static ViewportLatePassPairwiseRelationDefaultsProfile? BuildViewportLatePairwiseRelationDefaultsProfile(
        ViewportLatePassFamily family,
        ViewportLatePassFamily siblingFamily) =>
        (family, siblingFamily) switch
        {
            (ViewportLatePassFamily.Skintone, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.96f, 0.9f, "skintone-before-generic-detail"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(2, 0.98f, 0.92f, "makeup-primary-after-skintone"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(4, 0.95f, 0.93f, "generic-detail-after-skintone"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(5, 0.97f, 0.92f, "grayscale-after-skintone"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(6, 0.94f, 0.9f, "makeup-secondary-after-skintone"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-4, 0.92f, 0.94f, "generic-before-grayscale"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.MakeupPrimary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-3, 0.94f, 0.94f, "generic-before-makeup-primary"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.MakeupSecondary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-5, 0.92f, 0.93f, "generic-before-makeup-secondary"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-3, 0.96f, 0.94f, "makeup-primary-before-grayscale"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(8, 0.96f, 0.92f, "grayscale-after-generic"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.MakeupPrimary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(4, 0.98f, 0.94f, "grayscale-after-makeup-primary"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.MakeupSecondary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.97f, 0.95f, "grayscale-before-makeup-secondary"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.MakeupSecondary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.96f, 0.94f, "makeup-primary-before-secondary"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(6, 0.96f, 0.95f, "makeup-primary-after-generic"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(6, 0.92f, 0.92f, "makeup-secondary-after-grayscale"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.MakeupPrimary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(12, 0.9f, 0.92f, "makeup-secondary-after-primary"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(10, 0.92f, 0.94f, "makeup-secondary-after-generic"),
            (ViewportLatePassFamily.Skintone, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.96f, 0.88f, "skintone-before-soft-emissive"),
            (ViewportLatePassFamily.Skintone, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-3, 0.95f, 0.86f, "skintone-before-restricted-emissive"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.94f, 0.9f, "generic-before-soft-emissive"),
            (ViewportLatePassFamily.GenericDetail, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-3, 0.93f, 0.88f, "generic-before-restricted-emissive"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-1, 0.97f, 0.9f, "makeup-primary-before-soft-emissive"),
            (ViewportLatePassFamily.MakeupPrimary, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.95f, 0.88f, "makeup-primary-before-restricted-emissive"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-1, 0.98f, 0.9f, "grayscale-before-soft-emissive"),
            (ViewportLatePassFamily.GrayscaleDetail, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.96f, 0.88f, "grayscale-before-restricted-emissive"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-1, 0.94f, 0.89f, "makeup-secondary-before-soft-emissive"),
            (ViewportLatePassFamily.MakeupSecondary, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-2, 0.92f, 0.87f, "makeup-secondary-before-restricted-emissive"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(6, 0.94f, 0.94f, "soft-emissive-after-skintone"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(4, 0.95f, 0.95f, "soft-emissive-after-generic"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.MakeupPrimary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(3, 0.95f, 0.95f, "soft-emissive-after-makeup-primary"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(2, 0.95f, 0.95f, "soft-emissive-after-grayscale"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.MakeupSecondary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(1, 0.94f, 0.95f, "soft-emissive-after-makeup-secondary"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.EmissiveSoft) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(16, 1f, 0.94f, "restricted-emissive-after-soft"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.Skintone) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(7, 0.93f, 0.94f, "restricted-emissive-after-skintone"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.GenericDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(5, 0.94f, 0.94f, "restricted-emissive-after-generic"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.MakeupPrimary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(4, 0.94f, 0.94f, "restricted-emissive-after-makeup-primary"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.GrayscaleDetail) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(3, 0.94f, 0.94f, "restricted-emissive-after-grayscale"),
            (ViewportLatePassFamily.EmissiveRestricted, ViewportLatePassFamily.MakeupSecondary) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(2, 0.93f, 0.94f, "restricted-emissive-after-makeup-secondary"),
            (ViewportLatePassFamily.EmissiveSoft, ViewportLatePassFamily.EmissiveRestricted) =>
                new ViewportLatePassPairwiseRelationDefaultsProfile(-4, 0.96f, 0.96f, "soft-emissive-before-restricted"),
            _ => null
        };

    private static int GetViewportGenericDetailStackOrder(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent) =>
        BuildViewportGenericDetailLateProfile(material, blendIntent)?.StackOrder ?? 100;

    private static ViewportGenericDetailLateProfile? BuildViewportGenericDetailLateProfile(
        CanonicalMaterial? material,
        ViewportLatePassBlendIntent blendIntent)
    {
        var sortLayerBucketProfile = BuildOverlaySortLayerBucketProfile(material);
        var genericDetailDefaultsProfile = BuildViewportGenericDetailLateDefaultsProfile(blendIntent, sortLayerBucketProfile);
        return genericDetailDefaultsProfile is { } resolvedGenericDetailDefaultsProfile
            ? new ViewportGenericDetailLateProfile(
                BlendIntent: blendIntent,
                StackOrder: resolvedGenericDetailDefaultsProfile.StackOrder,
                PassPhase: resolvedGenericDetailDefaultsProfile.PassPhase,
                AlphaScale: resolvedGenericDetailDefaultsProfile.AlphaScale,
                EmissiveColor: resolvedGenericDetailDefaultsProfile.EmissiveColor,
                RequiresExplicitOpacityForRenderAlpha: resolvedGenericDetailDefaultsProfile.RequiresExplicitOpacityForRenderAlpha,
                DisallowViewportColorAlphaSource: resolvedGenericDetailDefaultsProfile.DisallowViewportColorAlphaSource,
                RuleTag: resolvedGenericDetailDefaultsProfile.RuleTag)
            : null;
    }

    private static ViewportGenericDetailLateDefaultsProfile? BuildViewportGenericDetailLateDefaultsProfile(
        ViewportLatePassBlendIntent blendIntent,
        OverlaySortLayerBucketProfile sortLayerBucketProfile) =>
        blendIntent switch
        {
            ViewportLatePassBlendIntent.DetailMasked => new ViewportGenericDetailLateDefaultsProfile(
                StackOrder: 105,
                PassPhase: 2,
                AlphaScale: 1f,
                EmissiveColor: new Color4(0.16f, 0.16f, 0.16f, 1f),
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: false,
                RuleTag: "generic-detail-masked"),
            ViewportLatePassBlendIntent.DetailSoft => new ViewportGenericDetailLateDefaultsProfile(
                StackOrder: 105,
                PassPhase: 2,
                AlphaScale: 0.82f,
                EmissiveColor: new Color4(0.12f, 0.12f, 0.12f, 1f),
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: false,
                RuleTag: "generic-detail-soft"),
            ViewportLatePassBlendIntent.DetailDefault => new ViewportGenericDetailLateDefaultsProfile(
                StackOrder: 110,
                PassPhase: 2,
                AlphaScale: 0.78f,
                EmissiveColor: new Color4(0.1f, 0.1f, 0.1f, 1f),
                RequiresExplicitOpacityForRenderAlpha: false,
                DisallowViewportColorAlphaSource: true,
                RuleTag: "generic-detail-default"),
            ViewportLatePassBlendIntent.OrderedSoft => new ViewportGenericDetailLateDefaultsProfile(
                StackOrder: sortLayerBucketProfile.GenericDetailStackOrder,
                PassPhase: 2,
                AlphaScale: 0.74f,
                EmissiveColor: new Color4(0.09f, 0.09f, 0.09f, 1f),
                RequiresExplicitOpacityForRenderAlpha: true,
                DisallowViewportColorAlphaSource: false,
                RuleTag: $"{sortLayerBucketProfile.RuleTag}|generic-detail-ordered"),
            _ => null
        };

    private static int GetViewportGenericOverlayParityOrder(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant) =>
        BuildViewportOverlayLateOrderingProfile(material, passVariant)?.ParityOrder ?? 0;

    private static ViewportOverlayLateOrderingProfile? BuildViewportOverlayLateOrderingProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        if (!IsCasOverlayDetailStage(material) || !IsViewportGenericOverlayLatePassVariant(passVariant))
        {
            return null;
        }

        var orderingDefaultsProfile = BuildViewportOverlayLateOrderingDefaultsProfile(material, passVariant);
        return orderingDefaultsProfile is { } resolvedOrderingDefaultsProfile
            ? new ViewportOverlayLateOrderingProfile(
                ParityOrder: resolvedOrderingDefaultsProfile.ParityOrder,
                CompositionTieBreaker: resolvedOrderingDefaultsProfile.CompositionTieBreaker,
                SortLayerTieBreaker: resolvedOrderingDefaultsProfile.SortLayerTieBreaker,
                RuleTag: resolvedOrderingDefaultsProfile.RuleTag)
            : null;
    }

    private static ViewportOverlayLateOrderingDefaultsProfile? BuildViewportOverlayLateOrderingDefaultsProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        if (wornLaneProfile is { } resolvedWornLaneProfile &&
            passVariant == resolvedWornLaneProfile.PassVariant)
        {
            return new ViewportOverlayLateOrderingDefaultsProfile(
                ParityOrder: resolvedWornLaneProfile.ParityOrder,
                CompositionTieBreaker: 0,
                SortLayerTieBreaker: material?.SortLayer ?? 0,
                RuleTag: resolvedWornLaneProfile.RuleTag);
        }

        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(passVariant);
        if (emissiveFamilyProfile is { } resolvedEmissiveFamilyProfile)
        {
            return new ViewportOverlayLateOrderingDefaultsProfile(
                ParityOrder: resolvedEmissiveFamilyProfile.BaseStackOrder,
                CompositionTieBreaker: 0,
                SortLayerTieBreaker: material?.SortLayer ?? 0,
                RuleTag: resolvedEmissiveFamilyProfile.FamilyTag);
        }

        var blendIntent = GetViewportLatePassBlendIntent(material, passVariant);
        var fallbackOrderingProfile = BuildViewportLateFallbackOrderingProfile(material, blendIntent);
        return fallbackOrderingProfile is { } resolvedFallbackOrderingProfile
            ? new ViewportOverlayLateOrderingDefaultsProfile(
                ParityOrder: resolvedFallbackOrderingProfile.StackOrder,
                CompositionTieBreaker: resolvedFallbackOrderingProfile.CompositionTieBreaker,
                SortLayerTieBreaker: resolvedFallbackOrderingProfile.SortLayerTieBreaker,
                RuleTag: resolvedFallbackOrderingProfile.RuleTag)
            : null;
    }

    private static ViewportLatePassFamilyContract BuildViewportLatePassFamilyContract(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        if (passVariant == OverlayMaterialPassVariant.Primary)
        {
            return new ViewportLatePassFamilyContract(
                Family: ViewportLatePassFamily.None,
                StackOrder: 0,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(false, [], "no-underlay-required"),
                TransparencyMode: ViewportLatePassTransparencyMode.NotTransparent,
                RelationTag: "primary");
        }

        var blendIntent = GetViewportLatePassBlendIntent(material, passVariant);
        var compositionRuleProfile = BuildViewportCompositionRuleProfile(material);
        if (compositionRuleProfile is { } ruleProfile &&
            blendIntent == ruleProfile.BlendIntent)
        {
            var compositionLateFamilyDefaultsProfile = BuildViewportCompositionLateFamilyContractDefaultsProfile(ruleProfile);

            return new ViewportLatePassFamilyContract(
                Family: ruleProfile.Family,
                StackOrder: ruleProfile.StackOrder,
                UnderlayProfile: compositionLateFamilyDefaultsProfile.UnderlayProfile,
                TransparencyMode: ViewportLatePassTransparencyMode.AlwaysTransparent,
                RelationTag: compositionLateFamilyDefaultsProfile.RelationTag);
        }

        var wornLaneProfile = BuildOverlayWornLaneProfile(material);
        if (wornLaneProfile is { } resolvedWornLaneProfile &&
            blendIntent == resolvedWornLaneProfile.BlendIntent)
        {
            return new ViewportLatePassFamilyContract(
                Family: resolvedWornLaneProfile.Family,
                StackOrder: resolvedWornLaneProfile.ParityOrder,
                UnderlayProfile: new ViewportLatePassUnderlayProfile(
                    true,
                    ["cas-shell-base", "surface", "surface-layered", "cas-overlay-base", "cas-overlay-highlayer-base"],
                    "requires-emissive-underlay"),
                TransparencyMode: resolvedWornLaneProfile.TransparencyMode,
                RelationTag: resolvedWornLaneProfile.RelationTag);
        }

        var emissiveFamilyProfile = BuildViewportLateEmissiveFamilyProfile(blendIntent);
        if (emissiveFamilyProfile is { } resolvedEmissiveFamilyProfile)
        {
            return new ViewportLatePassFamilyContract(
                Family: resolvedEmissiveFamilyProfile.Family,
                StackOrder: resolvedEmissiveFamilyProfile.BaseStackOrder,
                UnderlayProfile: resolvedEmissiveFamilyProfile.UnderlayProfile,
                TransparencyMode: resolvedEmissiveFamilyProfile.TransparencyMode,
                RelationTag: resolvedEmissiveFamilyProfile.RelationTag);
        }

        var fallbackDefaultsProfile = BuildViewportLateFallbackDefaultsProfile(material, blendIntent);
        if (fallbackDefaultsProfile is { } resolvedFallbackDefaultsProfile)
        {
            return new ViewportLatePassFamilyContract(
                Family: resolvedFallbackDefaultsProfile.Family,
                StackOrder: resolvedFallbackDefaultsProfile.StackOrder,
                UnderlayProfile: resolvedFallbackDefaultsProfile.UnderlayProfile,
                TransparencyMode: resolvedFallbackDefaultsProfile.TransparencyMode,
                RelationTag: resolvedFallbackDefaultsProfile.RelationTag);
        }

        return new ViewportLatePassFamilyContract(
            Family: ViewportLatePassFamily.None,
            StackOrder: 0,
            UnderlayProfile: new ViewportLatePassUnderlayProfile(false, [], "no-underlay-required"),
            TransparencyMode: ViewportLatePassTransparencyMode.NotTransparent,
            RelationTag: "none");
    }

    private static ViewportCompositionLateFamilyContractDefaultsProfile BuildViewportCompositionLateFamilyContractDefaultsProfile(
        ViewportCompositionRuleProfile ruleProfile)
    {
        var underlayProfile = ruleProfile.Family is ViewportLatePassFamily.MakeupPrimary or ViewportLatePassFamily.MakeupSecondary
            ? new ViewportLatePassUnderlayProfile(true, ["sim-skintone-base"], "requires-skintone-makeup-underlay")
            : new ViewportLatePassUnderlayProfile(true, ["cas-shell-base", "surface", "surface-layered", "cas-overlay-base", "cas-overlay-highlayer-base"], "requires-overlay-underlay");
        var relationTag = ruleProfile.Family switch
        {
            ViewportLatePassFamily.MakeupPrimary => "makeup-primary-over-skintone",
            ViewportLatePassFamily.GrayscaleDetail => "grayscale-over-overlay",
            ViewportLatePassFamily.MakeupSecondary => "makeup-secondary-over-skintone",
            _ => ruleProfile.RuleTag
        };

        return new ViewportCompositionLateFamilyContractDefaultsProfile(
            UnderlayProfile: underlayProfile,
            RelationTag: relationTag);
    }

    private void ShowPreviewFailureDiagnostics(string message, Exception ex)
    {
        uvPreviewRenderCancellation?.Cancel();
        uvPreviewRenderCancellation = null;
        sceneViewport.Items.Clear();
        sceneViewport.Visibility = Visibility.Collapsed;
        PreviewImage.Visibility = Visibility.Collapsed;
        UvPreviewImage.Visibility = Visibility.Collapsed;
        UvPreviewImage.Source = null;

        ViewModel.CurrentScene = null;
        ViewModel.PreviewImageSource = null;
        ViewModel.PreviewSurfaceMode = PreviewSurfaceMode.Diagnostics;
        ViewModel.PreviewSurfaceTitle = "Diagnostics";
        ViewModel.SelectedPreviewDiagnosticsTabIndex = 0;
        ViewModel.PreviewText = $"{message}{Environment.NewLine}{Environment.NewLine}{ex}";
        ViewModel.StatusMessage = message;
    }

    private void ResetSceneCamera(CanonicalScene? scene)
    {
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

        sceneCamera.Position = center + new Vector3(size * 1.5f, size * 0.75f, size * 1.5f);
        sceneCamera.LookDirection = center - sceneCamera.Position;
        sceneCamera.UpDirection = Vector3.UnitY;
    }

    private static int GetViewportMaterialRenderStage(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant = OverlayMaterialPassVariant.Primary)
    {
        if (material is null)
        {
            return BuildViewportMaterialFallbackRenderStageProfile(null).RenderStage;
        }

        if (IsViewportPrimaryPassVariant(passVariant))
        {
            var primaryPassContract = BuildViewportPrimaryPassContract(material, passVariant);
            if (primaryPassContract.Family != ViewportPrimaryPassFamily.None)
            {
                return primaryPassContract.RenderStage;
            }
        }

        var latePassRenderStage = ResolveViewportLatePassRenderStage(material, passVariant);
        if (latePassRenderStage is { } resolvedLatePassRenderStage)
        {
            return resolvedLatePassRenderStage;
        }

        if (!string.IsNullOrWhiteSpace(material.PreviewCompositorStage))
        {
            return GetViewportCompositorStageOrder(material.PreviewCompositorStage);
        }

        if (IsNonVisualViewportMaterial(material))
        {
            return BuildViewportMaterialFallbackRenderStageProfile(material).RenderStage;
        }

        return BuildViewportMaterialFallbackRenderStageProfile(material).RenderStage;
    }

    private static int? ResolveViewportLatePassRenderStage(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        if (IsViewportPrimaryPassVariant(passVariant))
        {
            return null;
        }

        return BuildViewportLatePassStageProfile(material, passVariant)?.RenderStage;
    }

    private static ViewportLatePassStageProfile? BuildViewportLatePassStageProfile(
        CanonicalMaterial? material,
        OverlayMaterialPassVariant passVariant)
    {
        var family = BuildViewportLatePassFamilyContract(material, passVariant).Family;
        var overlayStageProfile = BuildViewportOverlayStageProfile(material);
        if (family == ViewportLatePassFamily.None)
        {
            return null;
        }

        if (passVariant == OverlayMaterialPassVariant.SkintoneLate ||
            family == ViewportLatePassFamily.Skintone)
        {
            return new ViewportLatePassStageProfile(
                RenderStage: GetViewportCompositorStageOrder("sim-skintone-overlay"),
                RuleTag: "late-skintone-stage");
        }

        if (family == ViewportLatePassFamily.EmissiveRestricted ||
            overlayStageProfile.IsCasOverlayHighLayerDetailStage)
        {
            return new ViewportLatePassStageProfile(
                RenderStage: GetViewportCompositorStageOrder("cas-overlay-highlayer-detail"),
                RuleTag: "late-highlayer-stage");
        }

        if (overlayStageProfile.IsCasOverlayDetailStage ||
            family is ViewportLatePassFamily.GenericDetail or
                ViewportLatePassFamily.MakeupPrimary or
                ViewportLatePassFamily.GrayscaleDetail or
                ViewportLatePassFamily.MakeupSecondary or
                ViewportLatePassFamily.EmissiveSoft)
        {
            return new ViewportLatePassStageProfile(
                RenderStage: GetViewportCompositorStageOrder("cas-overlay-detail"),
                RuleTag: "late-overlay-stage");
        }

        return new ViewportLatePassStageProfile(
            RenderStage: GetViewportCompositorStageOrder(material?.PreviewCompositorStage),
            RuleTag: "late-stage-fallback");
    }

    private static int GetViewportMaterialPassBucket(CanonicalMaterial? material)
    {
        return BuildOverlayCompositorPolicy(material).PassBucket;
    }

    private static bool ShouldIncludeViewportPrimaryPass(
        CanonicalMaterial? material,
        SceneRenderMode renderMode,
        string? selectedSlot,
        OverlayMaterialPassVariant passVariant)
    {
        if (material is null)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(selectedSlot))
        {
            return true;
        }

        if (renderMode is SceneRenderMode.RawUv or SceneRenderMode.MaterialUv or SceneRenderMode.Wireframe)
        {
            return !string.Equals(material.PreviewCompositorStage, "helper-nonvisual", StringComparison.OrdinalIgnoreCase);
        }

        var primaryPassContract = BuildViewportPrimaryPassContract(material, passVariant);
        if (primaryPassContract.Family is
            ViewportPrimaryPassFamily.HelperProjective or
            ViewportPrimaryPassFamily.HelperLayered or
            ViewportPrimaryPassFamily.HelperUtility)
        {
            return false;
        }

        return material.PreviewCompositorStage switch
        {
            "helper-nonvisual" => false,
            _ => true
        };
    }

    private static int GetViewportCompositorStageOrder(string? stage) =>
        BuildViewportCompositorStageOrderProfile(stage).RenderStage;

    private static ViewportCompositorStageOrderProfile BuildViewportCompositorStageOrderProfile(string? stage) =>
        stage switch
        {
            "sim-skintone-base" => new ViewportCompositorStageOrderProfile(5, "stage-skintone-base"),
            "sim-skintone-overlay" => new ViewportCompositorStageOrderProfile(8, "stage-skintone-overlay"),
            "cas-shell-base" => new ViewportCompositorStageOrderProfile(10, "stage-cas-shell-base"),
            "surface" => new ViewportCompositorStageOrderProfile(20, "stage-surface"),
            "surface-layered" => new ViewportCompositorStageOrderProfile(25, "stage-surface-layered"),
            "cas-overlay-base" => new ViewportCompositorStageOrderProfile(35, "stage-cas-overlay-base"),
            "cas-overlay-detail" => new ViewportCompositorStageOrderProfile(40, "stage-cas-overlay-detail"),
            "cas-overlay-highlayer-base" => new ViewportCompositorStageOrderProfile(45, "stage-cas-overlay-highlayer-base"),
            "cas-overlay-highlayer-detail" => new ViewportCompositorStageOrderProfile(48, "stage-cas-overlay-highlayer-detail"),
            "cas-overlay" => new ViewportCompositorStageOrderProfile(40, "stage-cas-overlay"),
            "helper-projective" => new ViewportCompositorStageOrderProfile(70, "stage-helper-projective"),
            "helper-layered" => new ViewportCompositorStageOrderProfile(75, "stage-helper-layered"),
            "helper-utility" => new ViewportCompositorStageOrderProfile(80, "stage-helper-utility"),
            "helper-nonvisual" => new ViewportCompositorStageOrderProfile(90, "stage-helper-nonvisual"),
            _ => new ViewportCompositorStageOrderProfile(50, "stage-default")
        };

    private static int GetViewportCasSlotCategoryOrder(string? slotCategory) =>
        BuildViewportCasSlotCategoryOrderProfile(slotCategory).SlotCategoryOrder;

    private static ViewportCasSlotCategoryOrderProfile BuildViewportCasSlotCategoryOrderProfile(string? slotCategory) =>
        slotCategory switch
        {
            "Full Body" => new ViewportCasSlotCategoryOrderProfile(0, "slot-full-body"),
            "Body" => new ViewportCasSlotCategoryOrderProfile(1, "slot-body"),
            "Head" => new ViewportCasSlotCategoryOrderProfile(2, "slot-head"),
            "Top" => new ViewportCasSlotCategoryOrderProfile(3, "slot-top"),
            "Bottom" => new ViewportCasSlotCategoryOrderProfile(4, "slot-bottom"),
            "Shoes" => new ViewportCasSlotCategoryOrderProfile(5, "slot-shoes"),
            "Hair" => new ViewportCasSlotCategoryOrderProfile(6, "slot-hair"),
            "Accessory" => new ViewportCasSlotCategoryOrderProfile(7, "slot-accessory"),
            _ => new ViewportCasSlotCategoryOrderProfile(20, "slot-default")
        };

    private static ViewportMaterialFallbackRenderStageProfile BuildViewportMaterialFallbackRenderStageProfile(
        CanonicalMaterial? material)
    {
        if (material is null)
        {
            return new ViewportMaterialFallbackRenderStageProfile(50, "material-stage-null");
        }

        if (IsNonVisualViewportMaterial(material))
        {
            return new ViewportMaterialFallbackRenderStageProfile(90, "material-stage-nonvisual");
        }

        var slotCategoryOrder = GetViewportCasSlotCategoryOrder(material.CasPartSlotCategory);
        return slotCategoryOrder switch
        {
            <= 5 => new ViewportMaterialFallbackRenderStageProfile(10, "material-stage-body-shell"),
            6 or 7 => new ViewportMaterialFallbackRenderStageProfile(20, "material-stage-head-accessory"),
            _ when (material.CompositionMethod ?? 0) != 0 || (material.SortLayer ?? 0) != 0 =>
                new ViewportMaterialFallbackRenderStageProfile(40, "material-stage-overlay-fallback"),
            _ => new ViewportMaterialFallbackRenderStageProfile(30, "material-stage-surface-fallback")
        };
    }

}
