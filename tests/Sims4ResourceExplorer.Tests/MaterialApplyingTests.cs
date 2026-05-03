using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview;

namespace Sims4ResourceExplorer.Tests;

public sealed class MaterialApplyingTests
{
    private static readonly byte[] EmptyPng = Array.Empty<byte>();

    private static CanonicalTexture Texture(
        string slot,
        CanonicalTextureSemantic semantic = CanonicalTextureSemantic.Unknown,
        int uvChannel = 0,
        float scaleU = 1f,
        float scaleV = 1f,
        float offsetU = 0f,
        float offsetV = 0f,
        bool approximate = false) =>
        new(
            Slot: slot,
            FileName: $"{slot}.png",
            PngBytes: EmptyPng,
            SourceKey: null,
            SourcePackagePath: null,
            Semantic: semantic,
            SourceKind: CanonicalTextureSourceKind.ExplicitLocal,
            UvChannel: uvChannel,
            UvScaleU: scaleU,
            UvScaleV: scaleV,
            UvOffsetU: offsetU,
            UvOffsetV: offsetV,
            IsApproximateUvTransform: approximate);

    [Fact]
    public void Factory_ClassifiesBaseColorBySemantic()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "colorMap7",
            SourceKind: CanonicalMaterialSourceKind.ExplicitMatd);

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        Assert.Equal("colorMap7", renderable.ShaderFamily);
        Assert.Equal("colorMap7", renderable.NormalizedFamily);
        Assert.Single(renderable.Layers);
        Assert.Equal(RenderableLayerRole.BaseColor, renderable.Layers[0].Role);
        Assert.Equal(RenderableBlendMode.Opaque, renderable.Layers[0].Blend);
        Assert.Equal(RenderableAlphaPolicy.Opaque, renderable.Alpha.Policy);
    }

    [Fact]
    public void Factory_ClassifiesBaseColorByNameWhenSemanticIsUnknown()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[] { Texture("diffuse") },
            ShaderName: "colorMap7-painted");

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        Assert.Equal("colorMap7", renderable.NormalizedFamily);
        Assert.Equal(RenderableLayerRole.BaseColor, renderable.Layers[0].Role);
    }

    [Fact]
    public void Factory_PromotesAlphaSlotToAlphaSourceRole()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("alpha")
            },
            ShaderName: "colorMap7",
            IsTransparent: true,
            AlphaMode: "alpha-test-or-blend",
            AlphaTextureSlot: "alpha");

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        var alphaLayer = Assert.Single(renderable.WhereRole(RenderableLayerRole.AlphaSource));
        Assert.Equal("alpha", alphaLayer.Slot);
        Assert.True(alphaLayer.ContributesAlpha);
        Assert.Equal(RenderableAlphaPolicy.AlphaBlend, renderable.Alpha.Policy);
        Assert.Equal("alpha", renderable.Alpha.SourceSlot);
    }

    [Fact]
    public void Factory_TreatsLayeredOverlayAsOverlayForGenericFamilies()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay")
            },
            ShaderName: "colorMap7",
            LayeredTextureSlots: new[] { "overlay" });

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        Assert.Equal(RenderableLayerRole.Overlay, renderable.Layers[1].Role);
        Assert.Equal(RenderableBlendMode.OverlayMix, renderable.Layers[1].Blend);
    }

    [Fact]
    public void Factory_TreatsLayeredOverlayAsDecalForDecalMapFamily()
    {
        var canonical = new CanonicalMaterial(
            Name: "DecalMat",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay")
            },
            ShaderName: "DecalMap",
            LayeredTextureSlots: new[] { "overlay" });

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        Assert.Equal("DecalMap", renderable.NormalizedFamily);
        Assert.Equal(RenderableLayerRole.Decal, renderable.Layers[1].Role);
        Assert.Equal(RenderableBlendMode.DecalOver, renderable.Layers[1].Blend);
    }

    [Fact]
    public void Factory_PreservesPerTextureUvTransform()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor, uvChannel: 0, scaleU: 1f, scaleV: 1f),
                Texture("overlay", uvChannel: 1, scaleU: 0.5f, scaleV: 0.5f, offsetU: 0.25f, offsetV: 0.25f, approximate: true)
            },
            ShaderName: "colorMap7",
            LayeredTextureSlots: new[] { "overlay" });

        var renderable = RenderableMaterialFactory.FromCanonical(canonical);

        Assert.Equal(0, renderable.Layers[0].UvChannel);
        Assert.Equal(1, renderable.Layers[1].UvChannel);
        Assert.Equal(0.5f, renderable.Layers[1].UvScaleU);
        Assert.Equal(0.25f, renderable.Layers[1].UvOffsetU);
        Assert.True(renderable.Layers[1].IsApproximateUvTransform);
    }

    [Fact]
    public void DefaultApplier_PutsBaseColorOnPassZero()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "ExoticThing");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(DefaultMaterialApplier), plan.AppliedBy);
        Assert.Single(plan.Layers);
        Assert.Equal(0, plan.Layers[0].Pass);
        Assert.Equal(1, plan.PassCount);
    }

    [Fact]
    public void Plan_StacksOverlayAsAdditionalPass()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay")
            },
            ShaderName: "colorMap7",
            LayeredTextureSlots: new[] { "overlay" });

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(2, plan.PassCount);
        var basePass = Assert.Single(plan.LayersInPass(0).Where(layer => layer.Role == RenderableLayerRole.BaseColor));
        Assert.Equal("diffuse", basePass.Slot);
        var overlayPass = Assert.Single(plan.LayersInPass(1));
        Assert.Equal(RenderableLayerRole.Overlay, overlayPass.Role);
        Assert.Equal(RenderableBlendMode.OverlayMix, overlayPass.Blend);
    }

    [Fact]
    public void Plan_AlphaSourceAttachesToBasePassAsAuxiliary()
    {
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("alpha")
            },
            ShaderName: "colorMap7",
            IsTransparent: true,
            AlphaMode: "alpha-test-or-blend",
            AlphaTextureSlot: "alpha");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(1, plan.PassCount);
        var alphaLayer = Assert.Single(plan.Layers, layer => layer.Role == RenderableLayerRole.AlphaSource);
        Assert.Equal(0, alphaLayer.Pass);
        Assert.True(alphaLayer.ContributesAlpha);
    }

    [Fact]
    public void Plan_ResolvesTextureBytesBySlot()
    {
        var diffuse = Texture("diffuse", CanonicalTextureSemantic.BaseColor);
        var canonical = new CanonicalMaterial(
            Name: "MatA",
            Textures: new[] { diffuse },
            ShaderName: "colorMap7");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Same(diffuse, plan.Layers[0].Texture);
    }

    [Fact]
    public void ColorMap7Applier_AcceptsColorMapFamiliesOnly()
    {
        var applier = new ColorMap7Applier();
        var colorMap7 = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));
        var colorMap = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap"));
        var decal = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "DecalMap"));

        Assert.True(applier.CanHandle(colorMap7));
        Assert.True(applier.CanHandle(colorMap));
        Assert.False(applier.CanHandle(decal));
    }

    [Fact]
    public void DecalMapApplier_AcceptsDecalMapOnly()
    {
        var applier = new DecalMapApplier();
        var decal = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "DecalMap"));
        var colorMap7 = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));

        Assert.True(applier.CanHandle(decal));
        Assert.False(applier.CanHandle(colorMap7));
    }

    [Fact]
    public void Registry_RoutesColorMap7ThroughColorMap7Applier()
    {
        var canonical = new CanonicalMaterial(
            Name: "M",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay")
            },
            ShaderName: "colorMap7",
            LayeredTextureSlots: new[] { "overlay" });

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(ColorMap7Applier), plan.AppliedBy);
        Assert.Equal(2, plan.PassCount);
    }

    [Fact]
    public void Registry_RoutesDecalMapThroughDecalMapApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "M",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay")
            },
            ShaderName: "DecalMap",
            LayeredTextureSlots: new[] { "overlay" });

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(DecalMapApplier), plan.AppliedBy);
        Assert.Equal(2, plan.PassCount);
        var decalLayer = Assert.Single(plan.LayersInPass(1));
        Assert.Equal(RenderableLayerRole.Decal, decalLayer.Role);
        Assert.Equal(RenderableBlendMode.DecalOver, decalLayer.Blend);
    }

    [Fact]
    public void Registry_FallsBackToDefaultForUnknownFamily()
    {
        var canonical = new CanonicalMaterial(
            Name: "M",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "ExoticThing");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(DefaultMaterialApplier), plan.AppliedBy);
    }

    [Fact]
    public void SimSkinApplier_AcceptsSimSkinFamilies()
    {
        var applier = new SimSkinApplier();
        var simSkin = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SimSkin"));
        var simSkinMask = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SimSkinMask"));
        var simSkinVariant = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "simskin-variant"));
        var colorMap = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));
        var decal = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "M", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "DecalMap"));

        Assert.True(applier.CanHandle(simSkin));
        Assert.True(applier.CanHandle(simSkinMask));
        Assert.True(applier.CanHandle(simSkinVariant));
        Assert.False(applier.CanHandle(colorMap));
        Assert.False(applier.CanHandle(decal));
    }

    [Fact]
    public void Registry_RoutesSimSkinThroughSimSkinApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "Skin",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "SimSkin");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(SimSkinApplier), plan.AppliedBy);
    }

    [Fact]
    public void IsSimSkinFamily_RecognizesExpectedNames()
    {
        Assert.True(RenderableMaterialFactory.IsSimSkinFamily("SimSkin"));
        Assert.True(RenderableMaterialFactory.IsSimSkinFamily("SimSkinMask"));
        Assert.True(RenderableMaterialFactory.IsSimSkinFamily("simskin-foo"));
        Assert.False(RenderableMaterialFactory.IsSimSkinFamily("colorMap7"));
        Assert.False(RenderableMaterialFactory.IsSimSkinFamily(""));
        Assert.False(RenderableMaterialFactory.IsSimSkinFamily(null));
    }

    [Fact]
    public void DefaultApplier_HandlesEmptyMaterialWithoutThrowing()
    {
        var canonical = new CanonicalMaterial(
            Name: "Empty",
            Textures: Array.Empty<CanonicalTexture>(),
            ShaderName: "Unknown");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Empty(plan.Layers);
        Assert.Equal(0, plan.PassCount);
    }

    private static CanonicalTexture TextureWithKey(
        string slot,
        ResourceKeyRecord? key,
        string? package,
        CanonicalTextureSourceKind sourceKind,
        CanonicalTextureSemantic semantic = CanonicalTextureSemantic.Unknown) =>
        new(
            Slot: slot,
            FileName: $"{slot}.png",
            PngBytes: EmptyPng,
            SourceKey: key,
            SourcePackagePath: package,
            Semantic: semantic,
            SourceKind: sourceKind);

    [Fact]
    public void Deduplicate_DiscardsExactDuplicates()
    {
        var key = new ResourceKeyRecord(0x00B2D882, 0u, 0xF100D875C676C3B8UL, "_IMG");
        var textures = new List<CanonicalTexture>
        {
            TextureWithKey("alpha", key, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal),
            TextureWithKey("alpha", key, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal)
        };

        var diagnostics = new List<string>();
        BuildBuySceneBuildService.DeduplicateTextureSlots(textures, diagnostics);

        Assert.Single(textures);
        Assert.Equal("alpha", textures[0].Slot);
        Assert.Single(diagnostics);
        Assert.Contains("Discarded duplicate texture entry for slot alpha", diagnostics[0]);
    }

    [Fact]
    public void Deduplicate_KeepsHigherTrustResolutionOnSlotConflict()
    {
        var localKey = new ResourceKeyRecord(0x00B2D882, 0u, 0x6C7794CE8EBA0C4DUL, "_IMG");
        var indexedKey = new ResourceKeyRecord(0x00B2D882, 0u, 0xA4D80FB45AE9066BUL, "_IMG");
        var textures = new List<CanonicalTexture>
        {
            TextureWithKey("overlay", indexedKey, "ClientFullBuild6.package", CanonicalTextureSourceKind.ExplicitIndexed),
            TextureWithKey("overlay", localKey, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal)
        };

        var diagnostics = new List<string>();
        BuildBuySceneBuildService.DeduplicateTextureSlots(textures, diagnostics);

        Assert.Equal(2, textures.Count);
        Assert.Equal("overlay", textures[0].Slot);
        Assert.Equal(localKey.FullTgi, textures[0].SourceKey?.FullTgi);
        Assert.Equal("overlay_2", textures[1].Slot);
        Assert.Equal(indexedKey.FullTgi, textures[1].SourceKey?.FullTgi);
        Assert.Single(diagnostics);
        Assert.Contains("Renamed conflicting texture for slot overlay to overlay_2", diagnostics[0]);
    }

    [Fact]
    public void Deduplicate_LeavesDistinctSlotsAlone()
    {
        var diffuseKey = new ResourceKeyRecord(0x00B2D882, 0u, 0x6C7794CE8EBA0C4DUL, "_IMG");
        var alphaKey = new ResourceKeyRecord(0x00B2D882, 0u, 0xF100D875C676C3B8UL, "_IMG");
        var textures = new List<CanonicalTexture>
        {
            TextureWithKey("diffuse", diffuseKey, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal, CanonicalTextureSemantic.BaseColor),
            TextureWithKey("alpha", alphaKey, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal, CanonicalTextureSemantic.Opacity)
        };

        var diagnostics = new List<string>();
        BuildBuySceneBuildService.DeduplicateTextureSlots(textures, diagnostics);

        Assert.Equal(2, textures.Count);
        Assert.Equal(new[] { "diffuse", "alpha" }, textures.Select(t => t.Slot));
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Deduplicate_HandlesCase006StyleMaterialZero_DuplicateBaseColorAndAlpha()
    {
        var diffuseLocal = new ResourceKeyRecord(0x00B2D882, 0u, 0x6C7794CE8EBA0C4DUL, "_IMG");
        var diffuseIndexed = new ResourceKeyRecord(0x00B2D882, 0u, 0x6C7794CE8EBA0C44UL, "_IMG");
        var alphaA = new ResourceKeyRecord(0x00B2D882, 0u, 0xF100D875C676C3B8UL, "_IMG");
        var alphaB = new ResourceKeyRecord(0x00B2D882, 0u, 0xF100D875C676C3B9UL, "_IMG");
        var textures = new List<CanonicalTexture>
        {
            TextureWithKey("diffuse", diffuseLocal, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal, CanonicalTextureSemantic.BaseColor),
            TextureWithKey("diffuse", diffuseIndexed, "ClientFullBuild6.package", CanonicalTextureSourceKind.ExplicitIndexed, CanonicalTextureSemantic.BaseColor),
            TextureWithKey("alpha", alphaA, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal, CanonicalTextureSemantic.Opacity),
            TextureWithKey("alpha", alphaB, "ClientFullBuild0.package", CanonicalTextureSourceKind.ExplicitLocal, CanonicalTextureSemantic.Opacity)
        };

        BuildBuySceneBuildService.DeduplicateTextureSlots(textures);

        var slots = textures.Select(t => t.Slot).ToArray();
        Assert.Equal(4, textures.Count);
        Assert.Contains("diffuse", slots);
        Assert.Contains("diffuse_2", slots);
        Assert.Contains("alpha", slots);
        Assert.Contains("alpha_2", slots);
        var primaryDiffuse = textures.Single(t => t.Slot == "diffuse");
        Assert.Equal(CanonicalTextureSourceKind.ExplicitLocal, primaryDiffuse.SourceKind);
        Assert.Equal(diffuseLocal.FullTgi, primaryDiffuse.SourceKey?.FullTgi);
    }

    [Fact]
    public void Plan_ConsumesDedupedOverlayWithoutDoubleRendering()
    {
        // After deduplication, only the "overlay" slot drives the overlay role; the renamed
        // "overlay_2" slot stays as Unknown role and does NOT produce an extra overlay pass.
        var canonical = new CanonicalMaterial(
            Name: "DecalMat",
            Textures: new[]
            {
                Texture("diffuse", CanonicalTextureSemantic.BaseColor),
                Texture("overlay"),
                Texture("overlay_2")
            },
            ShaderName: "DecalMap",
            LayeredTextureSlots: new[] { "overlay" });

        var plan = MaterialApplierRegistry.Apply(canonical);

        var overlayPasses = plan.Layers.Count(layer =>
            layer.Role is RenderableLayerRole.Overlay or RenderableLayerRole.Decal);
        Assert.Equal(1, overlayPasses);
    }

    // ── SimGlassApplier ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SimGlassApplier_AcceptsSimGlassFamilies()
    {
        var applier = new SimGlassApplier();

        var glass = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "G", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SimGlass"));
        var glassVariant = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "G", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "simglass-eye"));
        var notGlass = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "G", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SimSkin"));

        Assert.True(applier.CanHandle(glass));
        Assert.True(applier.CanHandle(glassVariant));
        Assert.False(applier.CanHandle(notGlass));
    }

    [Fact]
    public void Registry_RoutesSimGlassThroughSimGlassApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "Eye",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "SimGlass");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(SimGlassApplier), plan.AppliedBy);
    }

    [Fact]
    public void IsSimGlassFamily_RecognizesExpectedNames()
    {
        Assert.True(RenderableMaterialFactory.IsSimGlassFamily("SimGlass"));
        Assert.True(RenderableMaterialFactory.IsSimGlassFamily("simglass-iris"));
        Assert.False(RenderableMaterialFactory.IsSimGlassFamily("SimSkin"));
        Assert.False(RenderableMaterialFactory.IsSimGlassFamily(""));
        Assert.False(RenderableMaterialFactory.IsSimGlassFamily(null));
    }

    // ── FoliageApplier ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void FoliageApplier_AcceptsFoliageAndCutoutFamilies()
    {
        var applier = new FoliageApplier();

        var foliage = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "F", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SeasonalFoliage"));
        var cutout = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "F", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "AlphaCutout"));
        var notFoliage = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "F", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));

        Assert.True(applier.CanHandle(foliage));
        Assert.True(applier.CanHandle(cutout));
        Assert.False(applier.CanHandle(notFoliage));
    }

    [Fact]
    public void Registry_RoutesFoliageThroughFoliageApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "Leaf",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "SeasonalFoliage");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(FoliageApplier), plan.AppliedBy);
    }

    [Fact]
    public void IsFoliageOrCutoutFamily_RecognizesExpectedNames()
    {
        Assert.True(RenderableMaterialFactory.IsFoliageOrCutoutFamily("SeasonalFoliage"));
        Assert.True(RenderableMaterialFactory.IsFoliageOrCutoutFamily("Foliage"));
        Assert.True(RenderableMaterialFactory.IsFoliageOrCutoutFamily("AlphaCutout"));
        Assert.True(RenderableMaterialFactory.IsFoliageOrCutoutFamily("AlphaCutout-legacy"));
        Assert.False(RenderableMaterialFactory.IsFoliageOrCutoutFamily("colorMap7"));
        Assert.False(RenderableMaterialFactory.IsFoliageOrCutoutFamily(null));
    }

    // ── StandardSurfaceApplier ─────────────────────────────────────────────────────────────────

    [Fact]
    public void StandardSurfaceApplier_AcceptsStandardSurfaceFamily()
    {
        var applier = new StandardSurfaceApplier();

        var std = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "S", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "StandardSurface"));
        var stdVariant = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "S", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "StandardSurface-v2"));
        var notStd = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "S", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));

        Assert.True(applier.CanHandle(std));
        Assert.True(applier.CanHandle(stdVariant));
        Assert.False(applier.CanHandle(notStd));
    }

    [Fact]
    public void Registry_RoutesStandardSurfaceThroughStandardSurfaceApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "Obj",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "StandardSurface");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(StandardSurfaceApplier), plan.AppliedBy);
    }

    // ── RefractionMapApplier ───────────────────────────────────────────────────────────────────

    [Fact]
    public void RefractionMapApplier_AcceptsRefractionAndEnvMapFamilies()
    {
        var applier = new RefractionMapApplier();

        var refraction = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "R", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "RefractionMap"));
        var envMap = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "R", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "SpecularEnvMap"));
        var notRefraction = RenderableMaterialFactory.FromCanonical(new CanonicalMaterial(
            "R", new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) }, ShaderName: "colorMap7"));

        Assert.True(applier.CanHandle(refraction));
        Assert.True(applier.CanHandle(envMap));
        Assert.False(applier.CanHandle(notRefraction));
    }

    [Fact]
    public void Registry_RoutesRefractionMapThroughRefractionMapApplier()
    {
        var canonical = new CanonicalMaterial(
            Name: "Water",
            Textures: new[] { Texture("diffuse", CanonicalTextureSemantic.BaseColor) },
            ShaderName: "RefractionMap");

        var plan = MaterialApplierRegistry.Apply(canonical);

        Assert.Equal(nameof(RefractionMapApplier), plan.AppliedBy);
    }

    [Fact]
    public void IsRefractionFamily_RecognizesExpectedNames()
    {
        Assert.True(RenderableMaterialFactory.IsRefractionFamily("RefractionMap"));
        Assert.True(RenderableMaterialFactory.IsRefractionFamily("SpecularEnvMap"));
        Assert.True(RenderableMaterialFactory.IsRefractionFamily("RefractionMap-pool"));
        Assert.False(RenderableMaterialFactory.IsRefractionFamily("colorMap7"));
        Assert.False(RenderableMaterialFactory.IsRefractionFamily(null));
    }
}
