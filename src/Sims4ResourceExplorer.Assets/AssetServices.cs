using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Assets;

public sealed class ExplicitBuildBuyAssetGraphBuilder : IAssetGraphBuilder
{
    public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan)
    {
        var resources = packageScan.Resources;
        var sameInstanceLookup = resources
            .GroupBy(static resource => resource.Key.FullInstance)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var summaries = new List<AssetSummary>();
        foreach (var model in resources.Where(static resource => resource.Key.TypeName == "Model"))
        {
            sameInstanceLookup.TryGetValue(model.Key.FullInstance, out var related);
            related ??= [];

            var objectDefinition = related.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectDefinition");
            var objectCatalog = related.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectCatalog");
            var modelLods = related.Count(static resource => resource.Key.TypeName == "ModelLOD");
            var thumbnail = related.FirstOrDefault(static resource => resource.Key.TypeName is "BuyBuildThumbnail" or "PNGImage" or "PNGImage2");

            var displayName = objectDefinition?.Name
                ?? objectCatalog?.Name
                ?? model.Name
                ?? $"Build/Buy Model {model.Key.FullInstance:X16}";

            var diagnostics = new List<string>();
            if (objectCatalog is null && objectDefinition is null)
            {
                diagnostics.Add("Catalog metadata could not be matched by exact instance; using a model-rooted Build/Buy asset identity.");
            }

            if (modelLods == 0)
            {
                diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model.");
            }

            summaries.Add(new AssetSummary(
                Guid.NewGuid(),
                model.DataSourceId,
                model.SourceKind,
                AssetKind.BuildBuy,
                displayName,
                "Build/Buy",
                model.PackagePath,
                model.Key,
                thumbnail?.Key.FullTgi,
                1,
                related.Length - 1,
                string.Join(" ", diagnostics)));
        }

        return summaries;
    }

    public AssetGraph BuildAssetGraph(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources)
    {
        if (summary.AssetKind != AssetKind.BuildBuy)
        {
            return new AssetGraph(summary, [], ["CAS asset graph resolution is deferred in this Build/Buy-focused pass."]);
        }

        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        var linked = root is null
            ? []
            : packageResources
                .Where(resource => resource.Key.FullTgi != root.Key.FullTgi && resource.Key.FullInstance == root.Key.FullInstance)
                .OrderBy(static resource => BuildBuyLinkOrder(resource.Key.TypeName))
                .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
                .ToArray();

        var diagnostics = new List<string>();
        if (root is null)
        {
            diagnostics.Add("The model root resource is not available in the currently loaded package metadata.");
        }
        else
        {
            var hasObjectIdentity = linked.Any(static resource => resource.Key.TypeName is "ObjectCatalog" or "ObjectDefinition");
            var modelLods = linked.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();

            if (!hasObjectIdentity)
            {
                diagnostics.Add("Exact-instance ObjectCatalog/ObjectDefinition metadata was not found for this model; the asset remains usable through its model-rooted identity.");
            }

            if (modelLods.Length == 0)
            {
                diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model. The scene builder may still succeed if the model contains an embedded MLOD.");
            }

            return new AssetGraph(
                summary,
                linked,
                diagnostics,
                new BuildBuyAssetGraph(
                    root,
                    modelLods,
                    [],
                    [],
                    [],
                    diagnostics,
                    true,
                    "Model-rooted static Build/Buy subset"));
        }

        return new AssetGraph(summary, linked, diagnostics);
    }

    private static int BuildBuyLinkOrder(string typeName) => typeName switch
    {
        "ObjectCatalog" => 0,
        "ObjectDefinition" => 1,
        "ModelLOD" => 2,
        "MaterialDefinition" => 3,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 4,
        _ => 10
    };
}
