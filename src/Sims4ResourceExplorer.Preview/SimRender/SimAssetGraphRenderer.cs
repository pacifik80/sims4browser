using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

/// <summary>
/// Pure-function service that turns a Sim <see cref="AssetGraph"/> into a single
/// composited <see cref="CanonicalScene"/> ready for viewport display.
///
/// Mirrors the rendering-composition portion of
/// <c>MainViewModel.TryApplySimBodyProxyPreviewAsync</c> without depending on
/// any MainViewModel state, so both the legacy MainWindow render path and the
/// new SimConstructorWindow can produce a scene from the same code.
/// </summary>
public interface ISimAssetGraphRenderer
{
    Task<SimRenderResult> BuildSimSceneAsync(AssetGraph graph, CancellationToken cancellationToken);
}

/// <summary>
/// Output of <see cref="ISimAssetGraphRenderer.BuildSimSceneAsync"/>. When
/// <see cref="Scene"/> is non-null the caller can hand it to a viewport
/// renderer; <see cref="Layers"/> carries per-layer ScenePreviewContent for
/// the inspector UI, and <see cref="Diagnostics"/> records resolution +
/// morph + composition notes.
/// </summary>
public sealed record SimRenderResult(
    CanonicalScene? Scene,
    IReadOnlyList<ScenePreviewContent> Layers,
    SimAssemblyPlanSummary? AssemblyPlan,
    SimAssemblyGraphSummary? AssemblyGraph,
    IReadOnlyList<string> Diagnostics);

public sealed class SimAssetGraphRenderer : ISimAssetGraphRenderer
{
    private readonly IIndexStore indexStore;
    private readonly IAssetGraphBuilder graphBuilder;
    private readonly ISceneBuildService sceneBuildService;
    private readonly BondMorphResolver bondMorphResolver;
    private readonly DeformerMapResolver deformerMapResolver;
    private readonly BlendGeometryResolver blendGeometryResolver;
    private readonly SimRigLoader simRigLoader;

    public SimAssetGraphRenderer(
        IIndexStore indexStore,
        IAssetGraphBuilder graphBuilder,
        ISceneBuildService sceneBuildService,
        BondMorphResolver bondMorphResolver,
        DeformerMapResolver deformerMapResolver,
        BlendGeometryResolver blendGeometryResolver,
        SimRigLoader simRigLoader)
    {
        this.indexStore = indexStore;
        this.graphBuilder = graphBuilder;
        this.sceneBuildService = sceneBuildService;
        this.bondMorphResolver = bondMorphResolver;
        this.deformerMapResolver = deformerMapResolver;
        this.blendGeometryResolver = blendGeometryResolver;
        this.simRigLoader = simRigLoader;
    }

    public async Task<SimRenderResult> BuildSimSceneAsync(AssetGraph graph, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var sim = graph.SimGraph;
        if (sim is null)
        {
            diagnostics.Add("AssetGraph has no SimGraph — cannot build a Sim scene.");
            return new SimRenderResult(null, Array.Empty<ScenePreviewContent>(), null, null, diagnostics);
        }

        // Resolve one preview per active body layer (excluding Head, which composes separately).
        // Head is handled as a special case below — the BodyAssembly often marks it
        // Available rather than Active, but the head shell should still render whenever
        // the SimGraph carries a Head candidate bucket.
        var bodyLayerOutputs = new List<LayerResolution>();
        LayerResolution? headLayer = null;

        foreach (var layer in sim.BodyAssembly.Layers)
        {
            if (string.Equals(layer.Label, "Head", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (layer.State != SimBodyAssemblyLayerState.Active)
            {
                continue;
            }

            var bucket = FindBucket(sim.BodyCandidates, layer.Label);
            if (bucket is null)
            {
                diagnostics.Add($"Active body layer '{layer.Label}' has no candidate bucket in SimGraph — skipping.");
                continue;
            }

            var resolved = await ResolveLayerAsync(bucket, diagnostics, cancellationToken).ConfigureAwait(false);
            if (resolved is not null)
            {
                bodyLayerOutputs.Add(resolved.Value);
            }
        }

        var headBucket = FindBucket(sim.BodyCandidates, "Head");
        if (headBucket is not null && headBucket.Candidates.Count > 0)
        {
            var resolved = await ResolveLayerAsync(headBucket, diagnostics, cancellationToken).ConfigureAwait(false);
            if (resolved is not null)
            {
                headLayer = resolved;
            }
        }

        if (bodyLayerOutputs.Count == 0 && headLayer is null)
        {
            diagnostics.Add("No usable body or head layer was resolved from the SimGraph.");
            return new SimRenderResult(null, Array.Empty<ScenePreviewContent>(), null, null, diagnostics);
        }

        // Compose body layers into a single ScenePreviewContent when more than one is active.
        ScenePreviewContent? bodyPreview = null;
        IReadOnlyList<ResourceMetadata> bodyRigResources = Array.Empty<ResourceMetadata>();
        IReadOnlyList<CasRegionMapSummary> bodyRegionMaps = Array.Empty<CasRegionMapSummary>();
        if (bodyLayerOutputs.Count == 1)
        {
            bodyPreview = bodyLayerOutputs[0].Preview;
            bodyRigResources = bodyLayerOutputs[0].RigResources;
            bodyRegionMaps = bodyLayerOutputs[0].RegionMaps;
        }
        else if (bodyLayerOutputs.Count > 1)
        {
            bodyPreview = CanonicalSceneComposer.Compose(
                $"Synthetic Sim Body ({bodyLayerOutputs.Count} layers)",
                bodyLayerOutputs.Select(l => l.Preview).ToArray());
            bodyRigResources = bodyLayerOutputs
                .SelectMany(l => l.RigResources)
                .GroupBy(r => r.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
            bodyRegionMaps = bodyLayerOutputs
                .SelectMany(l => l.RegionMaps)
                .GroupBy(m => m.ResourceTgi, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
            diagnostics.Add($"Composed body shell from {bodyLayerOutputs.Count} layer(s): {string.Join(", ", bodyLayerOutputs.Select(l => l.LayerLabel))}.");
        }

        // Resolve and apply morphs. Failures here degrade gracefully — the un-morphed
        // scene is still useful, so we log + skip.
        var simInfoResource = sim.SimInfoResource;
        var morphContext = await ResolveMorphsAsync(simInfoResource, sim.Metadata, diagnostics, cancellationToken).ConfigureAwait(false);

        ScenePreviewContent? morphedBody = bodyPreview is null ? null : ApplyMorphs(bodyPreview, morphContext, diagnostics, "body");
        ScenePreviewContent? morphedHead = headLayer is null ? null : ApplyMorphs(headLayer.Value.Preview, morphContext, diagnostics, "head");

        // ComposeBodyAndHead requires a non-null body preview. If we only got a head, fall
        // back to using it as the body so the renderer surfaces *something*.
        var assembledBodyPreview = morphedBody ?? morphedHead;
        if (assembledBodyPreview is null)
        {
            diagnostics.Add("No body preview to assemble — both body layers and head layer failed to resolve.");
            return new SimRenderResult(null, Array.Empty<ScenePreviewContent>(), null, null, diagnostics);
        }

        ScenePreviewContent? assembledHeadPreview = morphedBody is null ? null : morphedHead;
        var headRigs = headLayer?.RigResources ?? Array.Empty<ResourceMetadata>();
        var headRegions = headLayer?.RegionMaps ?? Array.Empty<CasRegionMapSummary>();

        var assembled = SimSceneComposer.ComposeBodyAndHead(
            name: "Synthetic Sim",
            bodyPreview: assembledBodyPreview,
            bodyRigResources: bodyRigResources,
            headPreview: assembledHeadPreview,
            headRigResources: headRigs,
            simMetadata: sim.Metadata,
            morphGroups: sim.MorphGroups,
            skintoneRender: sim.SkintoneRender,
            bodyRegionMaps: bodyRegionMaps,
            headRegionMaps: headRegions);

        if (assembled.Preview.Scene is { } finalScene)
        {
            var b = finalScene.Bounds;
            diagnostics.Add(FormattableString.Invariant(
                $"Assembled scene: meshes={finalScene.Meshes.Count}, materials={finalScene.Materials.Count}, bones={finalScene.Bones.Count}, bounds=({b.MinX:0.##},{b.MinY:0.##},{b.MinZ:0.##})→({b.MaxX:0.##},{b.MaxY:0.##},{b.MaxZ:0.##})."));
        }

        var layersOut = new List<ScenePreviewContent>(bodyLayerOutputs.Count + 1);
        layersOut.AddRange(bodyLayerOutputs.Select(l => l.Preview));
        if (headLayer is not null)
        {
            layersOut.Add(headLayer.Value.Preview);
        }

        return new SimRenderResult(
            Scene: assembled.Preview.Scene,
            Layers: layersOut,
            AssemblyPlan: assembled.Plan,
            AssemblyGraph: assembled.Graph,
            Diagnostics: diagnostics);
    }

    private static SimBodyCandidateSummary? FindBucket(IReadOnlyList<SimBodyCandidateSummary> buckets, string label) =>
        buckets.FirstOrDefault(b => string.Equals(b.Label, label, StringComparison.OrdinalIgnoreCase));

    private async Task<LayerResolution?> ResolveLayerAsync(
        SimBodyCandidateSummary bucket,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var option in bucket.Candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var asset = await ResolveAssetAsync(option, cancellationToken).ConfigureAwait(false);
            if (asset is null)
            {
                diagnostics.Add($"[{bucket.Label}] could not resolve AssetSummary for option '{option.DisplayName}' (tgi={option.RootTgi ?? "<no tgi>"}, pkg={option.PackagePath ?? "<no pkg>"}).");
                continue;
            }

            var packageResources = await indexStore
                .GetResourcesByInstanceAsync(asset.PackagePath, asset.RootKey.FullInstance, cancellationToken)
                .ConfigureAwait(false);
            var casAssetGraph = await graphBuilder
                .BuildAssetGraphAsync(asset, packageResources, cancellationToken)
                .ConfigureAwait(false);
            var casGraph = casAssetGraph.CasGraph;
            if (casGraph is null || !casGraph.IsSupported || casGraph.GeometryResource is null)
            {
                diagnostics.Add($"[{bucket.Label}] candidate '{asset.DisplayName}' did not produce a supported CAS graph.");
                continue;
            }

            var sceneResult = await sceneBuildService
                .BuildSceneAsync(casGraph, cancellationToken)
                .ConfigureAwait(false);
            if (!sceneResult.Success || sceneResult.Scene is null)
            {
                diagnostics.Add($"[{bucket.Label}] scene build failed for '{asset.DisplayName}': {string.Join(" | ", sceneResult.Diagnostics)}");
                continue;
            }

            var preview = new ScenePreviewContent(
                casGraph.GeometryResource,
                sceneResult.Scene,
                string.Join(Environment.NewLine, sceneResult.Diagnostics),
                sceneResult.Status);

            return new LayerResolution(
                LayerLabel: bucket.Label,
                Asset: asset,
                Preview: preview,
                RigResources: casGraph.RigResources,
                RegionMaps: casGraph.RegionMaps);
        }

        diagnostics.Add($"[{bucket.Label}] exhausted {bucket.Candidates.Count} option(s); no usable preview produced.");
        return null;
    }

    private async Task<AssetSummary?> ResolveAssetAsync(SimCasSlotOptionSummary option, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(option.PackagePath))
        {
            var exact = await indexStore.GetPackageAssetByIdAsync(option.PackagePath, option.AssetId, cancellationToken).ConfigureAwait(false);
            if (exact is not null)
            {
                return exact;
            }
        }

        if (string.IsNullOrWhiteSpace(option.RootTgi))
        {
            return null;
        }

        var query = new AssetBrowserQuery(
            new SourceScope(),
            option.RootTgi,
            AssetBrowserDomain.Cas,
            string.Empty,
            option.PackagePath ?? string.Empty,
            string.Empty,
            false,
            false,
            AssetBrowserSort.Name,
            0,
            32);
        var result = await indexStore.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
        return result.Items.FirstOrDefault(a =>
            a.Id == option.AssetId ||
            a.RootKey.FullTgi.Equals(option.RootTgi, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<MorphContext> ResolveMorphsAsync(
        ResourceMetadata simInfoResource,
        SimInfoSummary metadata,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SimBondMorph> bondMorphs = Array.Empty<SimBondMorph>();
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimDeformerMorph> dmapMorphs = Array.Empty<Sims4ResourceExplorer.Packages.Ts4SimDeformerMorph>();
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimBlendGeometryMorph> bgeoMorphs = Array.Empty<Sims4ResourceExplorer.Packages.Ts4SimBlendGeometryMorph>();
        SimRig? rig = null;

        try
        {
            bondMorphs = await bondMorphResolver.ResolveSimBondMorphsAsync(simInfoResource, cancellationToken).ConfigureAwait(false);
            if (bondMorphs.Count > 0)
            {
                var total = bondMorphs.Sum(m => m.Adjustments.Count);
                diagnostics.Add($"BOND morph: resolved {bondMorphs.Count} morph(s) with {total} per-bone adjustment(s).");
            }
            var rigInfo = SimRigCatalog.Resolve(metadata.SpeciesLabel, metadata.AgeLabel, occultLabel: null);
            if (rigInfo is { } info)
            {
                rig = await simRigLoader.LoadAsync(info.InstanceHash, info.Name, installRootHint: null, cancellationToken).ConfigureAwait(false);
                if (rig is null)
                {
                    diagnostics.Add($"BOND morph: rig {info.Name} (0x{info.InstanceHash:X16}) failed to load — BOND will be skipped.");
                }
            }
            else
            {
                diagnostics.Add($"BOND morph: no canonical rig for ({metadata.SpeciesLabel}, {metadata.AgeLabel}) — BOND will be skipped.");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"BOND morph resolution failed: {ex.Message}");
        }

        try
        {
            dmapMorphs = await deformerMapResolver.ResolveAsync(simInfoResource, cancellationToken).ConfigureAwait(false);
            if (dmapMorphs.Count > 0)
            {
                diagnostics.Add($"DMap morph: resolved {dmapMorphs.Count} morph(s).");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"DMap morph resolution failed: {ex.Message}");
        }

        try
        {
            bgeoMorphs = await blendGeometryResolver.ResolveAsync(simInfoResource, cancellationToken).ConfigureAwait(false);
            if (bgeoMorphs.Count > 0)
            {
                diagnostics.Add($"BGEO morph: resolved {bgeoMorphs.Count} morph(s).");
            }
        }
        catch (Exception ex)
        {
            diagnostics.Add($"BGEO morph resolution failed: {ex.Message}");
        }

        return new MorphContext(bondMorphs, dmapMorphs, bgeoMorphs, rig);
    }

    private static ScenePreviewContent ApplyMorphs(
        ScenePreviewContent source,
        MorphContext morphContext,
        List<string> diagnostics,
        string layerKind)
    {
        if (source.Scene is null)
        {
            return source;
        }

        var working = source;
        if (morphContext.BondMorphs.Count > 0 && morphContext.Rig is not null)
        {
            try
            {
                var morphed = SimBondSceneMorpher.MorphScene(working.Scene!, morphContext.Rig, morphContext.BondMorphs, diagnostics);
                working = working with { Scene = morphed };
            }
            catch (Exception ex)
            {
                diagnostics.Add($"SimBondSceneMorpher failed on {layerKind}: {ex.Message}");
            }
        }
        if (morphContext.DmapMorphs.Count > 0)
        {
            try
            {
                var morphed = DeformerMapMorpher.MorphScene(working.Scene!, morphContext.DmapMorphs, diagnostics);
                working = working with { Scene = morphed };
            }
            catch (Exception ex)
            {
                diagnostics.Add($"DeformerMapMorpher failed on {layerKind}: {ex.Message}");
            }
        }
        if (morphContext.BgeoMorphs.Count > 0)
        {
            try
            {
                var morphed = BlendGeometryMorpher.MorphScene(working.Scene!, morphContext.BgeoMorphs, diagnostics);
                working = working with { Scene = morphed };
            }
            catch (Exception ex)
            {
                diagnostics.Add($"BlendGeometryMorpher failed on {layerKind}: {ex.Message}");
            }
        }
        return working;
    }

    private readonly record struct LayerResolution(
        string LayerLabel,
        AssetSummary Asset,
        ScenePreviewContent Preview,
        IReadOnlyList<ResourceMetadata> RigResources,
        IReadOnlyList<CasRegionMapSummary> RegionMaps);

    private sealed record MorphContext(
        IReadOnlyList<SimBondMorph> BondMorphs,
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimDeformerMorph> DmapMorphs,
        IReadOnlyList<Sims4ResourceExplorer.Packages.Ts4SimBlendGeometryMorph> BgeoMorphs,
        SimRig? Rig);
}
