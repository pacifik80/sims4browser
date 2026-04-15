using System.Text;
using System.Buffers.Binary;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Assets;

public sealed class ExplicitAssetGraphBuilder : IAssetGraphBuilder
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore? indexStore;

    public ExplicitAssetGraphBuilder(IResourceCatalogService resourceCatalogService, IIndexStore? indexStore = null)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.indexStore = indexStore;
    }

    public IReadOnlyList<AssetSummary> BuildAssetSummaries(PackageScanResult packageScan)
    {
        var resources = packageScan.Resources;
        var sameInstanceLookup = resources
            .GroupBy(static resource => resource.Key.FullInstance)
            .ToDictionary(static group => group.Key, static group => group.ToArray());
        var claimedInstances = new HashSet<ulong>();

        var summaries = new List<AssetSummary>();
        summaries.AddRange(BuildBuildBuySummaries(sameInstanceLookup, claimedInstances));
        summaries.AddRange(BuildCasSummaries(resources, sameInstanceLookup, claimedInstances, packageScan.AssetVariants));
        summaries.AddRange(BuildGeneral3DSummaries(sameInstanceLookup, claimedInstances));
        return summaries;
    }

    public async Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken)
    {
        return summary.AssetKind switch
        {
            AssetKind.BuildBuy => await BuildBuildBuyGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            AssetKind.Cas => await BuildCasGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            AssetKind.General3D => await BuildGeneral3DGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            _ => new AssetGraph(summary, [], [$"Unsupported asset kind: {summary.AssetKind}."])
        };
    }

    private static IEnumerable<AssetSummary> BuildBuildBuySummaries(
        IReadOnlyDictionary<ulong, ResourceMetadata[]> sameInstanceLookup,
        ISet<ulong> claimedInstances)
    {
        var provisional = new List<AssetSummary>();
        foreach (var group in sameInstanceLookup
                     .OrderBy(static pair => pair.Key)
                     .Select(static pair => pair.Value))
        {
            var objectDefinition = group.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectDefinition");
            var objectCatalog = group.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectCatalog");
            if (objectDefinition is null && objectCatalog is null)
            {
                continue;
            }

            var root = objectDefinition ?? objectCatalog!;
            var model = group.FirstOrDefault(static resource => resource.Key.TypeName == "Model");
            var modelLods = group.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
            var materials = group.Where(static resource => resource.Key.TypeName == "MaterialDefinition").ToArray();
            var textures = group.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
            var thumbnail = group.FirstOrDefault(static resource => resource.Key.TypeName == "BuyBuildThumbnail")
                ?? textures.FirstOrDefault();
            var logicalRootTgi = objectDefinition?.SceneRootTgiHint
                ?? model?.Key.FullTgi
                ?? root.Key.FullTgi;

            var displayName = objectCatalog?.Name
                ?? objectDefinition?.Name
                ?? model?.Name
                ?? $"Build/Buy Object {root.Key.FullInstance:X16}";

            var diagnostics = new List<string>();
            if (model is null)
            {
                diagnostics.Add("No exact-instance Model resource was indexed for this Build/Buy identity root.");
            }

            if (modelLods.Length == 0)
            {
                diagnostics.Add("No exact-instance ModelLOD resources were indexed for this Build/Buy identity.");
            }

            if (textures.Length == 0)
            {
                diagnostics.Add("No exact-instance texture resources were indexed for this Build/Buy identity.");
            }

            claimedInstances.Add(root.Key.FullInstance);
            provisional.Add(new AssetSummary(
                StableEntityIds.ForAsset(root.DataSourceId, AssetKind.BuildBuy, root.PackagePath, root.Key),
                root.DataSourceId,
                root.SourceKind,
                AssetKind.BuildBuy,
                displayName,
                "Build/Buy",
                root.PackagePath,
                root.Key,
                thumbnail?.Key.FullTgi,
                1,
                Math.Max(0, group.Length - 1),
                string.Join(" ", diagnostics),
                new AssetCapabilitySnapshot(
                    HasSceneRoot: true,
                    HasExactGeometryCandidate: modelLods.Length > 0,
                    HasMaterialReferences: materials.Length > 0,
                    HasTextureReferences: textures.Length > 0,
                    HasThumbnail: thumbnail is not null,
                    HasVariants: false,
                    HasIdentityMetadata: true,
                    HasRigReference: false,
                    HasGeometryReference: modelLods.Length > 0 || model is not null,
                    HasMaterialResourceCandidate: materials.Length > 0,
                    HasTextureResourceCandidate: textures.Length > 0,
                    IsPackageLocalGraph: true,
                    HasDiagnostics: diagnostics.Count > 0),
                PackageName: Path.GetFileName(root.PackagePath),
                RootTypeName: root.Key.TypeName,
                ThumbnailTypeName: thumbnail?.Key.TypeName,
                PrimaryGeometryType: modelLods.Length > 0 ? "ModelLOD" : model?.Key.TypeName,
                IdentityType: objectDefinition?.Key.TypeName ?? objectCatalog?.Key.TypeName,
                CategoryNormalized: "buildbuy",
                Description: objectCatalog?.Description,
                CatalogSignal0020: objectCatalog?.CatalogSignal0020,
                CatalogSignal002C: objectCatalog?.CatalogSignal002C,
                CatalogSignal0030: objectCatalog?.CatalogSignal0030,
                CatalogSignal0034: objectCatalog?.CatalogSignal0034,
                LogicalRootTgi: logicalRootTgi));
        }

        foreach (var family in provisional
                     .GroupBy(static asset => asset.CanonicalRootTgi, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var members = family.ToArray();
            var winner = SelectCanonicalBuildBuyFamilyMember(members);
            if (members.Length == 1)
            {
                yield return winner;
                continue;
            }

            var aggregatedCapabilities = new AssetCapabilitySnapshot(
                HasSceneRoot: members.Any(static member => member.CapabilitySnapshot.HasSceneRoot),
                HasExactGeometryCandidate: members.Any(static member => member.CapabilitySnapshot.HasExactGeometryCandidate),
                HasMaterialReferences: members.Any(static member => member.CapabilitySnapshot.HasMaterialReferences),
                HasTextureReferences: members.Any(static member => member.CapabilitySnapshot.HasTextureReferences),
                HasThumbnail: members.Any(static member => member.CapabilitySnapshot.HasThumbnail),
                HasVariants: true,
                HasIdentityMetadata: members.Any(static member => member.CapabilitySnapshot.HasIdentityMetadata),
                HasRigReference: members.Any(static member => member.CapabilitySnapshot.HasRigReference),
                HasGeometryReference: members.Any(static member => member.CapabilitySnapshot.HasGeometryReference),
                HasMaterialResourceCandidate: members.Any(static member => member.CapabilitySnapshot.HasMaterialResourceCandidate),
                HasTextureResourceCandidate: members.Any(static member => member.CapabilitySnapshot.HasTextureResourceCandidate),
                IsPackageLocalGraph: members.All(static member => member.CapabilitySnapshot.IsPackageLocalGraph),
                HasDiagnostics: members.Any(static member => member.CapabilitySnapshot.HasDiagnostics));

            var diagnostics = members
                .Select(static member => member.Diagnostics)
                .Where(static diagnosticsText => !string.IsNullOrWhiteSpace(diagnosticsText))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            diagnostics.Add($"Collapsed {members.Length} Build/Buy identity entries into one logical asset family via scene root {winner.CanonicalRootTgi}.");

            yield return winner with
            {
                VariantCount = members.Length,
                LinkedResourceCount = members.Max(static member => member.LinkedResourceCount),
                Diagnostics = string.Join(" ", diagnostics),
                Capabilities = aggregatedCapabilities,
                LogicalRootTgi = winner.CanonicalRootTgi
            };
        }
    }

    private static AssetSummary SelectCanonicalBuildBuyFamilyMember(IReadOnlyList<AssetSummary> members) =>
        members
            .OrderByDescending(static member => member.CapabilitySnapshot.HasThumbnail)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasExactGeometryCandidate)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasIdentityMetadata)
            .ThenByDescending(static member => !string.IsNullOrWhiteSpace(member.Description))
            .ThenByDescending(static member => member.LinkedResourceCount)
            .ThenBy(static member => member.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
            .First();

    private static IEnumerable<AssetSummary> BuildCasSummaries(
        IReadOnlyList<ResourceMetadata> resources,
        IReadOnlyDictionary<ulong, ResourceMetadata[]> sameInstanceLookup,
        ISet<ulong> claimedInstances,
        IReadOnlyList<DiscoveredAssetVariant> discoveredVariants)
    {
        var variantCountsByRootTgi = discoveredVariants
            .Where(static variant => variant.AssetKind == AssetKind.Cas)
            .GroupBy(static variant => variant.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => Math.Max(1, group.Max(static variant => variant.VariantIndex + 1)),
                StringComparer.OrdinalIgnoreCase);

        foreach (var casPart in resources.Where(static resource => resource.Key.TypeName == "CASPart"))
        {
            sameInstanceLookup.TryGetValue(casPart.Key.FullInstance, out var related);
            related ??= [];

            var thumbnail = related.FirstOrDefault(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail")
                ?? related.FirstOrDefault(static resource => IsTextureType(resource.Key.TypeName));
            var displayName = casPart.Name ?? $"CAS Part {casPart.Key.FullInstance:X16}";
            var variantCount = variantCountsByRootTgi.TryGetValue(casPart.Key.FullTgi, out var mappedVariantCount)
                ? mappedVariantCount
                : 1;

            var diagnostics = new List<string>();
            if (thumbnail is null)
            {
                diagnostics.Add("No exact-instance CAS thumbnail was indexed for this CAS part.");
            }

            claimedInstances.Add(casPart.Key.FullInstance);
            var geometry = related.FirstOrDefault(static resource => resource.Key.TypeName == "Geometry");
            var materials = related.Where(static resource => resource.Key.TypeName == "MaterialDefinition").ToArray();
            var textures = related.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
            yield return new AssetSummary(
                StableEntityIds.ForAsset(casPart.DataSourceId, AssetKind.Cas, casPart.PackagePath, casPart.Key),
                casPart.DataSourceId,
                casPart.SourceKind,
                AssetKind.Cas,
                displayName,
                "CAS",
                casPart.PackagePath,
                casPart.Key,
                thumbnail?.Key.FullTgi,
                variantCount,
                related.Length - 1,
                string.Join(" ", diagnostics),
                new AssetCapabilitySnapshot(
                    HasSceneRoot: true,
                    HasExactGeometryCandidate: geometry is not null,
                    HasMaterialReferences: materials.Length > 0,
                    HasTextureReferences: textures.Length > 0,
                    HasThumbnail: thumbnail is not null,
                    HasVariants: variantCount > 1,
                    HasIdentityMetadata: true,
                    HasRigReference: related.Any(static resource => resource.Key.TypeName == "Rig"),
                    HasGeometryReference: geometry is not null,
                    HasMaterialResourceCandidate: materials.Length > 0,
                    HasTextureResourceCandidate: textures.Length > 0,
                    IsPackageLocalGraph: true,
                    HasDiagnostics: diagnostics.Count > 0),
                PackageName: Path.GetFileName(casPart.PackagePath),
                RootTypeName: casPart.Key.TypeName,
                ThumbnailTypeName: thumbnail?.Key.TypeName,
                PrimaryGeometryType: geometry?.Key.TypeName,
                IdentityType: casPart.Key.TypeName,
                CategoryNormalized: "cas");
        }
    }

    private static IEnumerable<AssetSummary> BuildGeneral3DSummaries(
        IReadOnlyDictionary<ulong, ResourceMetadata[]> sameInstanceLookup,
        ISet<ulong> claimedInstances)
    {
        foreach (var group in sameInstanceLookup
                     .OrderBy(static pair => pair.Key)
                     .Select(static pair => pair.Value))
        {
            var instance = group[0].Key.FullInstance;
            if (claimedInstances.Contains(instance))
            {
                continue;
            }

            var root = SelectGeneral3DRoot(group);
            if (root is null)
            {
                continue;
            }

            var modelResources = group.Where(static resource => resource.Key.TypeName == "Model").ToArray();
            var modelLods = group.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
            var geometryResources = group.Where(static resource => resource.Key.TypeName == "Geometry").ToArray();
            var rigResources = group.Where(static resource => resource.Key.TypeName == "Rig").ToArray();
            var materials = group.Where(static resource => resource.Key.TypeName == "MaterialDefinition").ToArray();
            var textures = group.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
            var thumbnail = textures.FirstOrDefault();

            var diagnostics = new List<string>();
            if (root.Key.TypeName == "ModelLOD")
            {
                diagnostics.Add("No same-instance Model root was indexed; using ModelLOD as the generalized 3D root.");
            }
            else if (root.Key.TypeName == "Geometry")
            {
                diagnostics.Add("No same-instance Model or ModelLOD root was indexed; using Geometry as the generalized 3D root.");
            }

            if (modelLods.Length == 0 && root.Key.TypeName == "Model")
            {
                diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model root.");
            }

            if (materials.Length == 0)
            {
                diagnostics.Add("No exact-instance MaterialDefinition resources were indexed for this generalized 3D root.");
            }

            if (textures.Length == 0)
            {
                diagnostics.Add("No exact-instance texture resources were indexed for this generalized 3D root.");
            }

            yield return new AssetSummary(
                StableEntityIds.ForAsset(root.DataSourceId, AssetKind.General3D, root.PackagePath, root.Key),
                root.DataSourceId,
                root.SourceKind,
                AssetKind.General3D,
                root.Name ?? $"{root.Key.TypeName} {root.Key.FullInstance:X16}",
                "General 3D",
                root.PackagePath,
                root.Key,
                thumbnail?.Key.FullTgi,
                1,
                Math.Max(0, group.Length - 1),
                string.Join(" ", diagnostics),
                new AssetCapabilitySnapshot(
                    HasSceneRoot: true,
                    HasExactGeometryCandidate: root.Key.TypeName is "Geometry" or "ModelLOD" || modelLods.Length > 0 || geometryResources.Length > 0,
                    HasMaterialReferences: materials.Length > 0,
                    HasTextureReferences: textures.Length > 0,
                    HasThumbnail: thumbnail is not null,
                    HasVariants: false,
                    HasIdentityMetadata: false,
                    HasRigReference: rigResources.Length > 0,
                    HasGeometryReference: root.Key.TypeName is "Geometry" or "ModelLOD" || modelLods.Length > 0 || geometryResources.Length > 0,
                    HasMaterialResourceCandidate: materials.Length > 0,
                    HasTextureResourceCandidate: textures.Length > 0,
                    IsPackageLocalGraph: true,
                    HasDiagnostics: diagnostics.Count > 0),
                PackageName: Path.GetFileName(root.PackagePath),
                RootTypeName: root.Key.TypeName,
                ThumbnailTypeName: thumbnail?.Key.TypeName,
                PrimaryGeometryType: root.Key.TypeName == "Model"
                    ? (modelLods.FirstOrDefault()?.Key.TypeName ?? geometryResources.FirstOrDefault()?.Key.TypeName)
                    : root.Key.TypeName,
                IdentityType: null,
                CategoryNormalized: "general3d");
        }
    }

    private static ResourceMetadata? SelectGeneral3DRoot(IReadOnlyList<ResourceMetadata> resources) =>
        resources.FirstOrDefault(static resource => resource.Key.TypeName == "Model") ??
        resources.FirstOrDefault(static resource => resource.Key.TypeName == "ModelLOD") ??
        resources.FirstOrDefault(static resource => resource.Key.TypeName == "Geometry");

    private async Task<AssetGraph> BuildBuildBuyGraphAsync(
        AssetSummary summary,
        IReadOnlyList<ResourceMetadata> packageResources,
        CancellationToken cancellationToken)
    {
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        var linked = root is null
            ? []
            : packageResources
                .Where(resource => resource.Key.FullTgi != root.Key.FullTgi && resource.Key.FullInstance == root.Key.FullInstance)
                .OrderBy(static resource => BuildBuyLinkOrder(resource.Key.TypeName))
                .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
                .ToArray();
        var effectiveRoot = root!;
        var linkedResources = linked.ToList();

        var diagnostics = new List<string>();
        if (root is null)
        {
            diagnostics.Add("The Build/Buy root resource is not available in the currently loaded package metadata.");
            return new AssetGraph(summary, linked, diagnostics);
        }

        if (root.Key.TypeName is "ObjectDefinition" or "ObjectCatalog")
        {
            var exactModel = linkedResources.FirstOrDefault(static resource => resource.Key.TypeName == "Model");
            if (exactModel is not null)
            {
                effectiveRoot = exactModel;
                diagnostics.Add($"Using exact-instance identity-linked model root: {exactModel.Key.FullTgi}.");
            }
            else
            {
                diagnostics.Add("Build/Buy asset is rooted at identity metadata; resolving model root from linked references.");
            }
        }

        var embeddedLodLabels = Array.Empty<string>();
        if (effectiveRoot.Key.TypeName == "Model")
        {
            try
            {
                var rootBytes = await resourceCatalogService
                    .GetResourceBytesAsync(effectiveRoot.PackagePath, effectiveRoot.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                embeddedLodLabels = ParseEmbeddedLodLabels(rootBytes).ToArray();
            }
            catch
            {
                // Asset graph construction should stay resilient even when the root bytes
                // use a MODL flavor we do not fully understand yet.
            }
        }

        var identityResources = (root.Key.TypeName is "ObjectCatalog" or "ObjectDefinition"
                ? new[] { root }
                : Array.Empty<ResourceMetadata>())
            .Concat(linkedResources.Where(static resource => resource.Key.TypeName is "ObjectCatalog" or "ObjectDefinition"))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var hasObjectIdentity = identityResources.Length > 0;
        var modelLods = linkedResources.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
        var textures = linkedResources.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
        var materialResources = linkedResources.Where(static resource => resource.Key.TypeName is "MaterialDefinition").ToArray();
        var objectDefinition = identityResources.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectDefinition");
        var objectCatalog = identityResources.FirstOrDefault(static resource => resource.Key.TypeName == "ObjectCatalog");
        var materialManifest = Array.Empty<MaterialManifestEntry>();

        if (!hasObjectIdentity)
        {
            diagnostics.Add("Exact-instance ObjectCatalog/ObjectDefinition metadata was not found for this model; the asset remains usable through its model-rooted identity.");
        }

        if (modelLods.Length == 0)
        {
            diagnostics.Add("No exact-instance ModelLOD resources were indexed for this model. The scene builder may still succeed if the model contains an embedded MLOD.");
        }

        if (materialResources.Length == 0)
        {
            diagnostics.Add("No exact-instance MaterialDefinition resources were indexed for this model. Scene reconstruction will rely on embedded chunks or texture approximation.");
        }

        if (objectDefinition is not null)
        {
            try
            {
                var objectDefinitionBytes = await resourceCatalogService
                    .GetResourceBytesAsync(objectDefinition.PackagePath, objectDefinition.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                var parsedObjectDefinition = Ts4ObjectDefinition.Parse(objectDefinitionBytes);
                diagnostics.Add($"ObjectDefinition internal name: {parsedObjectDefinition.InternalName}");
                var referenceSummary = parsedObjectDefinition.ReferenceCandidates
                    .Where(static candidate => candidate.RawKey.TypeName is "Model" or "Footprint" or "Rig" or "Slot")
                    .Take(6)
                    .Select(static candidate => $"{candidate.RawKey.TypeName} raw={candidate.RawKey.FullTgi} swap32={candidate.Swap32Key.FullTgi}")
                    .ToArray();
                if (referenceSummary.Length > 0)
                {
                    diagnostics.Add($"ObjectDefinition reference candidates: {string.Join("; ", referenceSummary)}");
                }

                if (indexStore is not null)
                {
                    var resolvedReferenceResources = await ResolveCrossPackageObjectDefinitionResourcesAsync(parsedObjectDefinition, cancellationToken).ConfigureAwait(false);
                    var resolvedReferenceSummary = resolvedReferenceResources
                        .Take(8)
                        .Select(static match => $"{match.Key.TypeName} -> {match.Key.FullTgi} @ {Path.GetFileName(match.PackagePath)}")
                        .ToArray();
                    if (resolvedReferenceSummary.Length > 0)
                    {
                        diagnostics.Add($"ObjectDefinition swap32-resolved references: {string.Join("; ", resolvedReferenceSummary)}");
                    }

                    var resolvedModelRoot = resolvedReferenceResources
                        .FirstOrDefault(static resource => resource.Key.TypeName == "Model");
                    if (resolvedModelRoot is not null &&
                        effectiveRoot.Key.FullTgi != resolvedModelRoot.Key.FullTgi &&
                        modelLods.Length == 0 &&
                        embeddedLodLabels.Length == 0)
                    {
                        effectiveRoot = resolvedModelRoot;
                        var resolvedLinked = await indexStore
                            .GetResourcesByInstanceAsync(resolvedModelRoot.PackagePath, resolvedModelRoot.Key.FullInstance, cancellationToken)
                            .ConfigureAwait(false);
                        linkedResources = linkedResources
                            .Concat(resolvedLinked.Where(resource => resource.Key.FullTgi != resolvedModelRoot.Key.FullTgi))
                            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                            .Select(static group => group.First())
                            .OrderBy(static resource => BuildBuyLinkOrder(resource.Key.TypeName))
                            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
                            .ToList();

                        modelLods = linkedResources.Where(static resource => resource.Key.TypeName == "ModelLOD").ToArray();
                        textures = linkedResources.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
                        materialResources = linkedResources.Where(static resource => resource.Key.TypeName is "MaterialDefinition").ToArray();
                        diagnostics.Add($"Using swap32-resolved ObjectDefinition model root: {resolvedModelRoot.Key.FullTgi} from {Path.GetFileName(resolvedModelRoot.PackagePath)}.");
                    }
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"ObjectDefinition parsing failed: {ex.Message}");
            }
        }

        if (objectCatalog is not null)
        {
            try
            {
                var objectCatalogBytes = await resourceCatalogService
                    .GetResourceBytesAsync(objectCatalog.PackagePath, objectCatalog.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                var parsedObjectCatalog = Ts4ObjectCatalog.Parse(objectCatalogBytes);
                diagnostics.Add($"ObjectCatalog word count: {parsedObjectCatalog.Words.Count}");
                if (!string.IsNullOrWhiteSpace(parsedObjectCatalog.RawCategorySignalSummary))
                {
                    diagnostics.Add($"ObjectCatalog raw category signals: {parsedObjectCatalog.RawCategorySignalSummary}");
                }
                if (parsedObjectCatalog.TailQwordCandidates.Count > 0)
                {
                    diagnostics.Add($"ObjectCatalog heuristic tail qwords: {string.Join(", ", parsedObjectCatalog.TailQwordCandidates.Select(static value => $"0x{value:X16}"))}");
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"ObjectCatalog parsing failed: {ex.Message}");
            }
        }

        if (effectiveRoot.Key.TypeName != "Model")
        {
            diagnostics.Add($"No supported Model root could be resolved for Build/Buy identity root {root.Key.FullTgi}.");
        }

        materialManifest = textures.Length == 0
            ? []
            : new[]
            {
                new MaterialManifestEntry(
                    "ApproximateMaterial",
                    null,
                    false,
                    null,
                    null,
                    [],
                    "Material/shader chunks are not fully parsed yet. Diffuse preview/export falls back to exact-instance texture candidates.",
                    textures.Select(static texture => new MaterialTextureEntry(
                        texture.Key.TypeName,
                        $"{texture.Key.TypeName}_{texture.Key.Type:X8}_{texture.Key.Group:X8}_{texture.Key.FullInstance:X16}.png",
                        texture.Key,
                        texture.PackagePath,
                        CanonicalTextureSemantic.Unknown,
                        CanonicalTextureSourceKind.FallbackSameInstanceLocal)).ToArray(),
                    CanonicalMaterialSourceKind.FallbackCandidate)
            };

        return new AssetGraph(
            summary,
            linkedResources,
            diagnostics,
            new BuildBuyAssetGraph(
                effectiveRoot,
                identityResources,
                modelLods,
                embeddedLodLabels,
                materialResources,
                textures,
                [],
                materialManifest,
                diagnostics,
                effectiveRoot.Key.TypeName == "Model",
                "Static Build/Buy furniture/decor objects with a model root, triangle-list MLOD geometry, no skinning/animation path, and package-local texture candidates."));
    }

    private static IReadOnlyList<string> ParseEmbeddedLodLabels(byte[] bytes)
    {
        var modlOffset = FindAscii(bytes, "MODL");
        if (modlOffset < 0 || modlOffset + 12 > bytes.Length)
        {
            return [];
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(modlOffset + 4, 4));
        var lodCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(modlOffset + 8, 4));
        if (lodCount <= 0 || lodCount > 64)
        {
            return [];
        }

        var cursor = modlOffset + 12;
        if (version >= 0x300)
        {
            cursor += 44;
        }
        else if (version >= 258 && version < 0x300)
        {
            if (cursor + 28 > bytes.Length)
            {
                return [];
            }

            cursor += 24;
            var extraBoundsCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(cursor, 4));
            cursor += 4;
            var extraBoundsBytes = checked(extraBoundsCount * 24);
            cursor += extraBoundsBytes + 8;
        }
        else
        {
            cursor += 24;
        }

        var labels = new List<string>(lodCount);
        for (var index = 0; index < lodCount; index++)
        {
            if (cursor + 20 > bytes.Length)
            {
                break;
            }

            var rawReference = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor, 4));
            var lodId = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(cursor + 8, 4));
            cursor += 20;

            var referenceType = rawReference == 0 ? 0xFFu : rawReference >> 28;
            if (referenceType == 0)
            {
                labels.Add(DescribeBuildBuyLod(lodId));
            }
        }

        return labels;
    }

    private static int FindAscii(byte[] bytes, string tag)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag);
        for (var index = 0; index <= bytes.Length - tagBytes.Length; index++)
        {
            var matched = true;
            for (var offset = 0; offset < tagBytes.Length; offset++)
            {
                if (bytes[index + offset] != tagBytes[offset])
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
            {
                return index;
            }
        }

        return -1;
    }

    private async Task<AssetGraph> BuildGeneral3DGraphAsync(
        AssetSummary summary,
        IReadOnlyList<ResourceMetadata> packageResources,
        CancellationToken cancellationToken)
    {
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        if (root is null)
        {
            return new AssetGraph(summary, [], ["The generalized 3D root resource is not available in the currently loaded package metadata."]);
        }

        var sameInstance = packageResources
            .Where(resource => resource.Key.FullInstance == root.Key.FullInstance && resource.Key.FullTgi != root.Key.FullTgi)
            .OrderBy(static resource => General3DLinkOrder(resource.Key.TypeName))
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        var diagnostics = new List<string>();
        var modelResources = (root.Key.TypeName == "Model" ? new[] { root } : Array.Empty<ResourceMetadata>())
            .Concat(sameInstance.Where(static resource => resource.Key.TypeName == "Model"))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var modelLods = (root.Key.TypeName == "ModelLOD" ? new[] { root } : Array.Empty<ResourceMetadata>())
            .Concat(sameInstance.Where(static resource => resource.Key.TypeName == "ModelLOD"))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var geometryResources = (root.Key.TypeName == "Geometry" ? new[] { root } : Array.Empty<ResourceMetadata>())
            .Concat(sameInstance.Where(static resource => resource.Key.TypeName == "Geometry"))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var rigResources = sameInstance
            .Where(static resource => resource.Key.TypeName == "Rig")
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var materialResources = sameInstance
            .Where(static resource => resource.Key.TypeName == "MaterialDefinition")
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var textureResources = sameInstance
            .Where(static resource => IsTextureType(resource.Key.TypeName))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

        var sceneRoot = root.Key.TypeName switch
        {
            "Model" => root,
            "ModelLOD" => root,
            "Geometry" => root,
            _ => modelResources.FirstOrDefault() ?? modelLods.FirstOrDefault() ?? geometryResources.FirstOrDefault() ?? root
        };

        IReadOnlyList<string> embeddedLodLabels = [];
        if (sceneRoot.Key.TypeName == "Model")
        {
            try
            {
                var rootBytes = await resourceCatalogService
                    .GetResourceBytesAsync(sceneRoot.PackagePath, sceneRoot.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                embeddedLodLabels = ParseEmbeddedLodLabels(rootBytes).ToArray();
            }
            catch
            {
                // Leave embedded LOD labels empty; generalized 3D discovery should stay resilient.
            }
        }

        if (root.Key.TypeName == "ModelLOD")
        {
            diagnostics.Add("This logical asset is rooted at ModelLOD because no same-instance Model resource was indexed.");
        }
        else if (root.Key.TypeName == "Geometry")
        {
            diagnostics.Add("This logical asset is rooted at Geometry because no same-instance Model or ModelLOD resource was indexed.");
        }

        if (modelResources.Length == 0)
        {
            diagnostics.Add("No exact-instance Model resource was indexed for this generalized 3D asset.");
        }

        if (modelLods.Length == 0 && embeddedLodLabels.Count == 0 && sceneRoot.Key.TypeName == "Model")
        {
            diagnostics.Add("No exact-instance ModelLOD resources were indexed for this generalized 3D model root.");
        }

        if (geometryResources.Length == 0 && sceneRoot.Key.TypeName == "Geometry")
        {
            diagnostics.Add("No additional same-instance Geometry resources were indexed beyond the selected scene root.");
        }

        if (materialResources.Length == 0)
        {
            diagnostics.Add("No exact-instance MaterialDefinition resources were indexed for this generalized 3D asset.");
        }

        if (textureResources.Length == 0)
        {
            diagnostics.Add("No exact-instance texture resources were indexed for this generalized 3D asset.");
        }

        var linkedResources = modelResources
            .Concat(modelLods)
            .Concat(geometryResources)
            .Concat(rigResources)
            .Concat(materialResources)
            .Concat(textureResources)
            .Where(resource => !string.Equals(resource.Key.FullTgi, root.Key.FullTgi, StringComparison.OrdinalIgnoreCase))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static resource => General3DLinkOrder(resource.Key.TypeName))
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        return new AssetGraph(
            summary,
            linkedResources,
            diagnostics,
            null,
            null,
            new General3DAssetGraph(
                root,
                sceneRoot,
                modelResources,
                modelLods,
                embeddedLodLabels,
                geometryResources,
                rigResources,
                materialResources,
                textureResources,
                diagnostics,
                sceneRoot.Key.TypeName is "Model" or "ModelLOD" or "Geometry",
                "Standalone package-local 3D roots discovered from Model, ModelLOD, or Geometry resources that are not currently claimed by Build/Buy or CAS identity graphs."));
    }

    private static string DescribeBuildBuyLod(uint id) => id switch
    {
        0x00000000 => "High Detail",
        0x00000001 => "Medium Detail",
        0x00000002 => "Low Detail",
        0x00010000 => "High Detail Shadow",
        0x00010001 => "Medium Detail Shadow",
        0x00010002 => "Low Detail Shadow",
        _ => $"LOD {id:X8}"
    };

    private async Task<AssetGraph> BuildCasGraphAsync(
        AssetSummary summary,
        IReadOnlyList<ResourceMetadata> packageResources,
        CancellationToken cancellationToken)
    {
        var root = packageResources.FirstOrDefault(resource => resource.Key.FullTgi == summary.RootKey.FullTgi);
        if (root is null)
        {
            return new AssetGraph(summary, [], ["The CASPart root resource is not available in the currently loaded package metadata."]);
        }

        var diagnostics = new List<string>();
        Ts4CasPart casPart;
        try
        {
            var bytes = await resourceCatalogService.GetResourceBytesAsync(root.PackagePath, root.Key, raw: false, cancellationToken).ConfigureAwait(false);
            casPart = Ts4CasPart.Parse(bytes);
        }
        catch (Exception ex)
        {
            diagnostics.Add($"CASPart parsing failed: {ex.Message}");
            return BuildFallbackCasGraph(summary, root, packageResources, diagnostics);
        }

        var sameInstance = packageResources
            .Where(resource => resource.Key.FullInstance == root.Key.FullInstance && resource.Key.FullTgi != root.Key.FullTgi)
            .OrderBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        var identityResources = new List<ResourceMetadata> { root };
        identityResources.AddRange(sameInstance.Where(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail"));

        var geometryResources = new List<ResourceMetadata>();
        var rigResources = sameInstance.Where(static resource => resource.Key.TypeName == "Rig").ToList();
        var textureReferences = await ResolveCasTextureReferencesAsync(casPart, root, packageResources, diagnostics, cancellationToken).ConfigureAwait(false);
        var materialResources = new List<ResourceMetadata>();
        var companionTextureResources = new List<ResourceMetadata>();
        var selectedLodLabel = default(string);

        foreach (var lod in casPart.Lods.OrderBy(static lod => lod.Level))
        {
            var resolvedGeometry = await ResolveCasLodResourcesAsync(root, packageResources, casPart.TgiList, lod, cancellationToken).ConfigureAwait(false);

            var directGeometry = resolvedGeometry
                .Where(static resource => string.Equals(resource!.Key.TypeName, "Geometry", StringComparison.Ordinal))
                .Cast<ResourceMetadata>()
                .ToArray();

            if (directGeometry.Length == 0)
            {
                diagnostics.Add($"LOD {lod.Level} did not expose a direct Geometry reference in the CASPart TGI list.");
                continue;
            }

            geometryResources.AddRange(directGeometry);
            selectedLodLabel = $"LOD {lod.Level}";
            break;
        }

        if (geometryResources.Count == 0)
        {
            var directGeometryFallback = await ResolveCasDirectGeometryFallbackAsync(root, packageResources, casPart.TgiList, cancellationToken).ConfigureAwait(false);
            if (directGeometryFallback.Length > 0)
            {
                geometryResources.AddRange(directGeometryFallback);
                selectedLodLabel = "Direct geometry from CASPart TGI";
                diagnostics.Add("CASPart did not expose any parsed LOD entries; using direct Geometry references from the CASPart TGI table.");
            }
        }

        geometryResources = geometryResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        if (geometryResources.Count == 0)
        {
            diagnostics.Add("No direct Geometry resource could be resolved from the CASPart LOD references. GEOM list containers remain unsupported in this pass.");
        }
        else
        {
            var companions = await ResolveCasGeometryCompanionsAsync(root, geometryResources, cancellationToken).ConfigureAwait(false);
            diagnostics.AddRange(companions.Diagnostics);
            rigResources.AddRange(companions.RigResources);
            materialResources.AddRange(companions.MaterialResources);
            companionTextureResources.AddRange(companions.TextureResources);
        }

        var category = MapCasBodyType(casPart.BodyType) ?? $"Body Type {casPart.BodyType}";

        if (!casPart.IsAdultOrYoungAdult)
        {
            diagnostics.Add("This CAS part is outside the supported adult/young-adult age subset.");
        }

        if (!casPart.IsMasculineOrFeminineHumanPresentation)
        {
            diagnostics.Add("This CAS part does not expose the expected adult human male/female age-gender flags for the supported subset.");
        }

        if (rigResources.Count == 0)
        {
            diagnostics.Add("No exact-instance Rig resource was indexed for this CAS part. Bone hierarchy names may fall back to GEOM bone hashes.");
        }

        rigResources = rigResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        materialResources = materialResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        companionTextureResources = companionTextureResources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();

        var explicitTextures = textureReferences
            .Select(static reference => reference.Resource)
            .Concat(companionTextureResources)
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        var materialManifest = BuildApproximateCasMaterialManifest(textureReferences, "CAS material/shader semantics are approximated to a portable texture bundle. Explicit CASPart texture references are used where available.");

        var isSupported = casPart.IsAdultOrYoungAdult
            && casPart.IsMasculineOrFeminineHumanPresentation
            && geometryResources.Count > 0;

        var linkedResources = identityResources
            .Concat(geometryResources)
            .Concat(rigResources)
            .Concat(materialResources)
            .Concat(explicitTextures)
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static resource => CasLinkOrder(resource.Key.TypeName))
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        return new AssetGraph(
            summary,
            linkedResources,
            diagnostics,
            null,
            new CasAssetGraph(
                root,
                geometryResources.FirstOrDefault(),
                rigResources.FirstOrDefault(),
                identityResources,
                geometryResources,
                rigResources,
                materialResources,
                explicitTextures,
                materialManifest,
                category,
                casPart.SwatchSummary,
                selectedLodLabel,
                isSupported,
                "Adult/young-adult human CAS parts when a skinned Geometry LOD can be resolved directly or through indexed cross-package CAS references."));
    }

    private AssetGraph BuildFallbackCasGraph(
        AssetSummary summary,
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        List<string> diagnostics)
    {
        diagnostics.Add("Using fallback CAS graph from exact-instance package resources. Category, swatch, and LOD semantics may be incomplete because CASPart semantic parsing did not succeed.");

        var sameInstance = packageResources
            .Where(resource => resource.Key.FullInstance == root.Key.FullInstance && resource.Key.FullTgi != root.Key.FullTgi)
            .OrderBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        var identityResources = new List<ResourceMetadata> { root };
        identityResources.AddRange(sameInstance.Where(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail"));

        var geometryResources = sameInstance
            .Where(static resource => resource.Key.TypeName == "Geometry")
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        var rigResources = sameInstance
            .Where(static resource => resource.Key.TypeName == "Rig")
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        var materialResources = sameInstance
            .Where(static resource => resource.Key.TypeName == "MaterialDefinition")
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToList();
        var explicitTextures = sameInstance
            .Where(static resource => IsTextureType(resource.Key.TypeName))
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();

        if (geometryResources.Count == 0)
        {
            diagnostics.Add("Fallback CAS graph did not find an exact-instance Geometry resource in the same package.");
        }

        if (rigResources.Count == 0)
        {
            diagnostics.Add("Fallback CAS graph did not find an exact-instance Rig resource in the same package.");
        }

        if (explicitTextures.Length == 0)
        {
            diagnostics.Add("Fallback CAS graph did not find exact-instance texture resources in the same package.");
        }

        var linkedResources = identityResources
            .Concat(geometryResources)
            .Concat(rigResources)
            .Concat(materialResources)
            .Concat(explicitTextures)
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static resource => CasLinkOrder(resource.Key.TypeName))
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();

        return new AssetGraph(
            summary,
            linkedResources,
            diagnostics,
            null,
            new CasAssetGraph(
                root,
                geometryResources.FirstOrDefault(),
                rigResources.FirstOrDefault(),
                identityResources,
                geometryResources,
                rigResources,
                materialResources,
                explicitTextures,
                BuildApproximateCasMaterialManifestFromFallbackResources(explicitTextures, "CAS material/shader semantics are approximated from exact-instance fallback resources because CASPart semantic parsing failed."),
                summary.Category,
                null,
                null,
                geometryResources.Count > 0,
                "Fallback CAS preview from exact-instance Geometry/Rig/Texture resources when CASPart semantic parsing is unavailable."));
    }

    private static IReadOnlyList<MaterialManifestEntry> BuildApproximateCasMaterialManifest(
        IReadOnlyList<Ts4CasTextureReference> explicitTextures,
        string approximationText) =>
        explicitTextures.Count == 0
            ? []
            : new[]
            {
                new MaterialManifestEntry(
                    "ApproximateCasMaterial",
                    null,
                    explicitTextures.Any(static texture => texture.Resource.Key.TypeName.Contains("PNG", StringComparison.OrdinalIgnoreCase)),
                    "portable-approximation",
                    null,
                    [],
                    approximationText,
                    explicitTextures.Select(texture => new MaterialTextureEntry(
                        texture.Slot,
                        $"{texture.Slot}_{texture.Resource.Key.Type:X8}_{texture.Resource.Key.Group:X8}_{texture.Resource.Key.FullInstance:X16}.png",
                        texture.Resource.Key,
                        texture.Resource.PackagePath,
                        texture.Semantic,
                        texture.SourceKind)).ToArray(),
                    CanonicalMaterialSourceKind.ApproximateCas)
            };

    private static IReadOnlyList<MaterialManifestEntry> BuildApproximateCasMaterialManifestFromFallbackResources(
        IReadOnlyList<ResourceMetadata> explicitTextures,
        string approximationText) =>
        explicitTextures.Count == 0
            ? []
            : new[]
            {
                new MaterialManifestEntry(
                    "ApproximateCasMaterial",
                    null,
                    explicitTextures.Any(static texture => texture.Key.TypeName.Contains("PNG", StringComparison.OrdinalIgnoreCase)),
                    "portable-approximation",
                    null,
                    [],
                    approximationText,
                    explicitTextures.Select(texture => new MaterialTextureEntry(
                        GuessCasTextureSlot(texture.Key.TypeName),
                        $"{GuessCasTextureSlot(texture.Key.TypeName)}_{texture.Key.Type:X8}_{texture.Key.Group:X8}_{texture.Key.FullInstance:X16}.png",
                        texture.Key,
                        texture.PackagePath,
                        GuessCasTextureSemantic(texture.Key.TypeName),
                        CanonicalTextureSourceKind.ExplicitLocal)).ToArray(),
                    CanonicalMaterialSourceKind.ApproximateCas)
            };

    private async Task<List<Ts4CasTextureReference>> ResolveCasTextureReferencesAsync(
        Ts4CasPart casPart,
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var results = new List<Ts4CasTextureReference>();
        foreach (var textureCandidate in casPart.TextureReferences)
        {
            var texture = await ResolveCasGraphResourceAsync(root, packageResources, textureCandidate.Key, cancellationToken).ConfigureAwait(false);
            if (texture is null)
            {
                diagnostics.Add($"CASPart texture slot '{textureCandidate.Slot}' did not resolve to an indexed resource.");
                continue;
            }

            if (IsTextureType(texture.Key.TypeName))
            {
                var sourceKind = string.Equals(texture.PackagePath, root.PackagePath, StringComparison.OrdinalIgnoreCase)
                    ? CanonicalTextureSourceKind.ExplicitLocal
                    : CanonicalTextureSourceKind.ExplicitIndexed;
                if (sourceKind == CanonicalTextureSourceKind.ExplicitIndexed)
                {
                    diagnostics.Add($"CASPart texture slot '{textureCandidate.Slot}' resolved via indexed cross-package lookup from {Path.GetFileName(texture.PackagePath)}.");
                }

                results.Add(new Ts4CasTextureReference(textureCandidate.Slot, texture, textureCandidate.Semantic, sourceKind));
            }
        }

        return results;
    }

    private async Task<ResourceMetadata[]> ResolveCasDirectGeometryFallbackAsync(
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        IReadOnlyList<ResourceKeyRecord> tgiList,
        CancellationToken cancellationToken)
    {
        var resources = new List<ResourceMetadata>();
        foreach (var key in tgiList
                     .Where(static key => string.Equals(key.TypeName, "Geometry", StringComparison.Ordinal))
                     .OrderBy(static key => key.Group)
                     .ThenBy(static key => key.FullInstance))
        {
            var resource = await ResolveCasGraphResourceAsync(root, packageResources, key, cancellationToken).ConfigureAwait(false);
            if (resource is not null)
            {
                resources.Add(resource);
            }
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private async Task<CasGeometryCompanionResources> ResolveCasGeometryCompanionsAsync(
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> geometryResources,
        CancellationToken cancellationToken)
    {
        if (indexStore is null || geometryResources.Count == 0)
        {
            return CasGeometryCompanionResources.Empty;
        }

        var diagnostics = new List<string>();
        var rigs = new List<ResourceMetadata>();
        var materials = new List<ResourceMetadata>();
        var textures = new List<ResourceMetadata>();

        foreach (var geometry in geometryResources)
        {
            var companions = await indexStore
                .GetResourcesByInstanceAsync(geometry.PackagePath, geometry.Key.FullInstance, cancellationToken)
                .ConfigureAwait(false);
            if (companions.Count == 0)
            {
                continue;
            }

            if (!string.Equals(geometry.PackagePath, root.PackagePath, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"CAS geometry {geometry.Key.FullTgi} resolved via indexed cross-package lookup from {Path.GetFileName(geometry.PackagePath)}.");
            }

            var companionRigs = companions
                .Where(static resource => resource.Key.TypeName == "Rig")
                .ToArray();
            var companionMaterials = companions
                .Where(static resource => resource.Key.TypeName == "MaterialDefinition")
                .ToArray();
            var companionTextures = companions
                .Where(static resource => IsTextureType(resource.Key.TypeName))
                .ToArray();

            if (companionRigs.Length > 0 || companionMaterials.Length > 0 || companionTextures.Length > 0)
            {
                diagnostics.Add(
                    $"Resolved CAS geometry companions from {Path.GetFileName(geometry.PackagePath)}: Rig={companionRigs.Length}, MaterialDefinition={companionMaterials.Length}, Texture={companionTextures.Length}.");
            }

            rigs.AddRange(companionRigs);
            materials.AddRange(companionMaterials);
            textures.AddRange(companionTextures);
        }

        return new CasGeometryCompanionResources(
            rigs
                .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            materials
                .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            textures
                .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            diagnostics);
    }

    private static ResourceMetadata? ResolveTgi(IReadOnlyList<ResourceMetadata> packageResources, IReadOnlyList<ResourceKeyRecord> tgiList, byte index)
    {
        if (index >= tgiList.Count)
        {
            return null;
        }

        var key = tgiList[index];
        return packageResources.FirstOrDefault(resource =>
            resource.Key.Type == key.Type &&
            resource.Key.Group == key.Group &&
            resource.Key.FullInstance == key.FullInstance);
    }

    private async Task<ResourceMetadata[]> ResolveCasLodResourcesAsync(
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        IReadOnlyList<ResourceKeyRecord> tgiList,
        Ts4CasLod lod,
        CancellationToken cancellationToken)
    {
        var resources = new List<ResourceMetadata>();
        foreach (var index in lod.KeyIndices)
        {
            if (index >= tgiList.Count)
            {
                continue;
            }

            var key = tgiList[index];
            var resource = await ResolveCasGraphResourceAsync(root, packageResources, key, cancellationToken).ConfigureAwait(false);
            if (resource is not null)
            {
                resources.Add(resource);
            }
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }

    private async Task<ResourceMetadata?> ResolveCasGraphResourceAsync(
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        ResourceKeyRecord key,
        CancellationToken cancellationToken)
    {
        var local = packageResources.FirstOrDefault(resource =>
            resource.Key.Type == key.Type &&
            resource.Key.Group == key.Group &&
            resource.Key.FullInstance == key.FullInstance);
        if (local is not null)
        {
            return local;
        }

        if (indexStore is null)
        {
            return null;
        }

        var matches = await indexStore.GetResourcesByTgiAsync(key.FullTgi, cancellationToken).ConfigureAwait(false);
        return matches
            .OrderBy(resource => ScoreCasCrossPackageCandidate(resource, root.DataSourceId))
            .ThenBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static int ScoreCasCrossPackageCandidate(ResourceMetadata resource, Guid preferredSourceId)
    {
        var score = 0;
        if (resource.DataSourceId != preferredSourceId)
        {
            score += 100;
        }

        var normalizedPath = resource.PackagePath.Replace('/', '\\');
        if (normalizedPath.Contains("\\Delta\\", StringComparison.OrdinalIgnoreCase))
        {
            score += 50;
        }

        if (normalizedPath.Contains("SimulationDeltaBuild", StringComparison.OrdinalIgnoreCase))
        {
            score += 30;
        }
        else if (normalizedPath.Contains("ClientDeltaBuild", StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }
        else if (normalizedPath.Contains("SimulationPreload", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }
        else if (normalizedPath.Contains("SimulationFullBuild", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private static string? MapCasBodyType(int bodyType) => bodyType switch
    {
        2 => "Hair",
        5 => "Full Body",
        6 => "Top",
        7 => "Bottom",
        8 => "Shoes",
        12 => "Accessory",
        _ => null
    };

    private static string GuessCasTextureSlot(string typeName) => typeName switch
    {
        "CASPartThumbnail" or "BodyPartThumbnail" => "thumbnail",
        "PNGImage" or "PNGImage2" => "diffuse",
        "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => "compressed",
        _ => "texture"
    };

    private static CanonicalTextureSemantic GuessCasTextureSemantic(string typeName) => typeName switch
    {
        "CASPartThumbnail" or "BodyPartThumbnail" => CanonicalTextureSemantic.Overlay,
        "PNGImage" or "PNGImage2" => CanonicalTextureSemantic.BaseColor,
        _ => CanonicalTextureSemantic.Unknown
    };

    private static int BuildBuyLinkOrder(string typeName) => typeName switch
    {
        "ObjectCatalog" => 0,
        "ObjectDefinition" => 1,
        "ModelLOD" => 2,
        "MaterialDefinition" => 3,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 4,
        _ => 10
    };

    private static int CasLinkOrder(string typeName) => typeName switch
    {
        "CASPart" => 0,
        "CASPartThumbnail" or "BodyPartThumbnail" => 1,
        "Geometry" => 2,
        "Rig" => 3,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 4,
        _ => 10
    };

    private static int General3DLinkOrder(string typeName) => typeName switch
    {
        "Model" => 0,
        "ModelLOD" => 1,
        "Geometry" => 2,
        "Rig" => 3,
        "MaterialDefinition" => 4,
        "BuyBuildThumbnail" or "CASPartThumbnail" or "BodyPartThumbnail" => 5,
        "PNGImage" or "PNGImage2" or "DSTImage" or "LRLEImage" or "RLE2Image" or "RLESImage" => 6,
        _ => 10
    };

    private static bool IsTextureType(string typeName) => typeName is
        "BuyBuildThumbnail" or
        "CASPartThumbnail" or
        "BodyPartThumbnail" or
        "PNGImage" or
        "PNGImage2" or
        "DSTImage" or
        "LRLEImage" or
        "RLE2Image" or
        "RLESImage";

    private async Task<string[]> ResolveCrossPackageObjectDefinitionReferencesAsync(
        Ts4ObjectDefinition objectDefinition,
        CancellationToken cancellationToken)
    {
        var resources = await ResolveCrossPackageObjectDefinitionResourcesAsync(objectDefinition, cancellationToken).ConfigureAwait(false);
        return resources
            .Select(static match => $"{match.Key.TypeName} -> {match.Key.FullTgi} @ {Path.GetFileName(match.PackagePath)}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task<ResourceMetadata[]> ResolveCrossPackageObjectDefinitionResourcesAsync(
        Ts4ObjectDefinition objectDefinition,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        var resources = new List<ResourceMetadata>();
        foreach (var candidate in objectDefinition.ReferenceCandidates
                     .Where(static candidate => candidate.Swap32Key.TypeName is "Model" or "Footprint")
                     .Take(8))
        {
            var matches = await indexStore.GetResourcesByTgiAsync(candidate.Swap32Key.FullTgi, cancellationToken).ConfigureAwait(false);
            resources.AddRange(matches);
        }

        return resources
            .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
    }
}

public static class Ts4SeedMetadataExtractor
{
    public static Ts4ObjectDefinitionSeedMetadata? TryExtractObjectDefinitionSeedMetadata(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            var parsed = Ts4ObjectDefinition.Parse(bytes);
            var sceneRootTgiHint = parsed.ReferenceCandidates
                .Where(static candidate => candidate.Swap32Key.TypeName == "Model")
                .Select(static candidate => candidate.Swap32Key.FullTgi)
                .FirstOrDefault();
            return new Ts4ObjectDefinitionSeedMetadata(parsed.InternalName, sceneRootTgiHint);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static Ts4CasPartSeedMetadata? TryExtractCasPartSeedMetadata(ResourceMetadata resource, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            var casPart = Ts4CasPart.Parse(bytes);
            return new Ts4CasPartSeedMetadata(
                casPart.InternalName,
                BuildCasPartVariants(resource, casPart));
        }
        catch (InvalidDataException)
        {
            return TryExtractCasPartHeaderOnlySeedMetadata(bytes);
        }
        catch (EndOfStreamException)
        {
            return TryExtractCasPartHeaderOnlySeedMetadata(bytes);
        }
        catch (ArgumentOutOfRangeException)
        {
            return TryExtractCasPartHeaderOnlySeedMetadata(bytes);
        }
    }

    public static string? TryExtractTechnicalName(ResourceMetadata resource, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return resource.Key.TypeName switch
            {
                "ObjectDefinition" => TryExtractObjectDefinitionSeedMetadata(bytes)?.TechnicalName,
                "CASPart" => Ts4CasPart.TryReadInternalName(bytes),
                _ => null
            };
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static uint? TryExtractObjectCatalogNameHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return Ts4ObjectCatalog.Parse(bytes).NameHash;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static uint? TryExtractObjectCatalogDescriptionHash(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return Ts4ObjectCatalog.Parse(bytes).DescriptionHash;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    public static (uint? Signal0020, uint? Signal002C, uint? Signal0030, uint? Signal0034) TryExtractObjectCatalogSignals(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            var parsed = Ts4ObjectCatalog.Parse(bytes);
            return (parsed.Word0020, parsed.Word002C, parsed.Word0030, parsed.Word0034);
        }
        catch (InvalidDataException)
        {
            return (null, null, null, null);
        }
        catch (EndOfStreamException)
        {
            return (null, null, null, null);
        }
        catch (ArgumentOutOfRangeException)
        {
            return (null, null, null, null);
        }
    }

    private static Ts4CasPartSeedMetadata? TryExtractCasPartHeaderOnlySeedMetadata(byte[] bytes)
    {
        try
        {
            return new Ts4CasPartSeedMetadata(Ts4CasPart.TryReadInternalName(bytes), []);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (EndOfStreamException)
        {
            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static IReadOnlyList<DiscoveredAssetVariant> BuildCasPartVariants(ResourceMetadata resource, Ts4CasPart casPart)
    {
        var variants = new List<DiscoveredAssetVariant>();
        for (var swatchIndex = 0; swatchIndex < casPart.SwatchColors.Count; swatchIndex++)
        {
            var swatchHex = $"#{casPart.SwatchColors[swatchIndex]:X8}";
            variants.Add(new DiscoveredAssetVariant(
                resource.DataSourceId,
                resource.SourceKind,
                AssetKind.Cas,
                resource.PackagePath,
                resource.Key,
                swatchIndex,
                "Swatch",
                $"Swatch {swatchIndex + 1} ({swatchHex})",
                swatchHex,
                casPart.VariantThumbnailTgi));
        }

        for (var presetIndex = 0; presetIndex < casPart.PresetCount; presetIndex++)
        {
            variants.Add(new DiscoveredAssetVariant(
                resource.DataSourceId,
                resource.SourceKind,
                AssetKind.Cas,
                resource.PackagePath,
                resource.Key,
                presetIndex,
                "Preset",
                $"Preset {presetIndex + 1}",
                ThumbnailTgi: casPart.VariantThumbnailTgi));
        }

        return variants;
    }
}

public sealed record Ts4ObjectDefinitionSeedMetadata(string? TechnicalName, string? SceneRootTgiHint);

public sealed record Ts4CasPartSeedMetadata(string? TechnicalName, IReadOnlyList<DiscoveredAssetVariant> Variants);

internal sealed class Ts4CasPart
{
    private const uint MinimumSupportedVersion = 26;
    private const uint YoungAdultFlag = 0x00000010;
    private const uint AdultFlag = 0x00000020;
    private const uint MaleFlag = 0x00001000;
    private const uint FemaleFlag = 0x00002000;

    public required int BodyType { get; init; }
    public required uint AgeGenderFlags { get; init; }
    public required string? InternalName { get; init; }
    public required uint PresetCount { get; init; }
    public required IReadOnlyList<ResourceKeyRecord> TgiList { get; init; }
    public required IReadOnlyList<Ts4CasLod> Lods { get; init; }
    public required IReadOnlyList<Ts4CasTextureCandidate> TextureReferences { get; init; }
    public required IReadOnlyList<uint> SwatchColors { get; init; }
    public required string? SwatchSummary { get; init; }
    public required string? VariantThumbnailTgi { get; init; }

    public bool IsAdultOrYoungAdult => (AgeGenderFlags & (YoungAdultFlag | AdultFlag)) != 0;
    public bool IsMasculineOrFeminineHumanPresentation => (AgeGenderFlags & (MaleFlag | FemaleFlag)) != 0;

    public static string? TryReadInternalName(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var header = ReadHeader(reader, stream, requireTgiOffsetInPayload: false);
        return NormalizeName(header.InternalName);
    }

    public static Ts4CasPart Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

        var header = ReadHeader(reader, stream, requireTgiOffsetInPayload: true);
        var internalName = header.InternalName;
        _ = reader.ReadSingle();
        _ = reader.ReadUInt16();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadByte();
        if (header.Version >= 39)
        {
            _ = reader.ReadByte();
        }

        _ = reader.ReadUInt64();
        if (header.Version >= 41)
        {
            _ = reader.ReadUInt64();
        }

        if (header.Version >= 36)
        {
            _ = reader.ReadUInt64();
        }
        else
        {
            _ = reader.ReadUInt32();
        }

        SkipTagMultimap(reader, stream, header.Version);

        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        if (header.Version >= 43)
        {
            _ = reader.ReadUInt32();
        }

        _ = reader.ReadByte();
        var bodyType = reader.ReadInt32();
        _ = reader.ReadInt32();
        var ageGender = reader.ReadUInt32();
        if (header.Version >= 32)
        {
            _ = reader.ReadUInt32();
        }

        if (header.Version >= 34)
        {
            _ = reader.ReadInt16();
            _ = reader.ReadByte();
            SkipBytes(stream, 9, "CASPart pack reserved bytes");
        }
        else
        {
            var unused2 = reader.ReadByte();
            if (unused2 > 0)
            {
                _ = reader.ReadByte();
            }
        }

        var swatches = ReadSwatchColors(reader, stream);
        _ = reader.ReadByte();
        var variantThumbnailKey = reader.ReadByte();
        if (header.Version >= 28)
        {
            _ = reader.ReadUInt64();
        }

        if (header.Version >= 30)
        {
            var usedMaterialCount = reader.ReadByte();
            if (usedMaterialCount > 0)
            {
                SkipBytes(stream, 12, "CASPart used material hash set");
            }
        }

        if (header.Version >= 31)
        {
            _ = reader.ReadUInt32();
        }

        if (header.Version >= 36)
        {
            _ = reader.ReadUInt32();
            _ = reader.ReadUInt32();
        }

        if (header.Version >= 38)
        {
            _ = reader.ReadUInt64();
        }

        if (header.Version >= 39)
        {
            _ = reader.ReadUInt64();
        }

        if (header.Version >= 44)
        {
            SkipBytes(stream, 44, "CASPart slider tuning");
        }

        if (header.Version >= 46)
        {
            var linkedPartCount = reader.ReadByte();
            SkipBytes(stream, linkedPartCount, "CASPart linked part list");
        }

        _ = reader.ReadByte();
        _ = reader.ReadByte();
        _ = reader.ReadInt32();
        var lodCount = reader.ReadByte();
        var lods = new List<Ts4CasLod>(lodCount);
        for (var lodIndex = 0; lodIndex < lodCount; lodIndex++)
        {
            var level = reader.ReadByte();
            _ = reader.ReadUInt32();

            var lodAssetCount = reader.ReadByte();
            SkipBytes(stream, lodAssetCount * 12L, "CASPart LOD asset list");

            var keyCount = reader.ReadByte();
            var keyIndices = new byte[keyCount];
            for (var keyIndex = 0; keyIndex < keyCount; keyIndex++)
            {
                keyIndices[keyIndex] = reader.ReadByte();
            }

            lods.Add(new Ts4CasLod(level, keyIndices));
        }

        var slotKeyCount = reader.ReadByte();
        SkipBytes(stream, slotKeyCount, "CASPart slot key list");

        var diffuseKey = reader.ReadByte();
        var shadowKey = reader.ReadByte();
        _ = reader.ReadByte();
        var regionMapKey = reader.ReadByte();
        var overrideCount = reader.ReadByte();
        SkipBytes(stream, overrideCount * 5L, "CASPart override list");
        var normalMapKey = reader.ReadByte();
        var specularMapKey = reader.ReadByte();
        if (header.Version >= 27)
        {
            _ = reader.ReadInt32();
        }

        var emissionMapKey = byte.MaxValue;
        if (header.Version >= 29)
        {
            emissionMapKey = reader.ReadByte();
        }

        if (header.Version >= 42)
        {
            _ = reader.ReadByte();
        }

        var colorShiftMaskKey = byte.MaxValue;
        if (header.Version >= 49)
        {
            colorShiftMaskKey = reader.ReadByte();
        }

        if (stream.Position > header.TgiOffset)
        {
            throw new InvalidDataException("CASPart structured body extends beyond the declared TGI table offset.");
        }

        if (stream.Position < header.TgiOffset)
        {
            SkipBytes(stream, header.TgiOffset - stream.Position, "CASPart forward-compatible tail");
        }

        var tgiList = ReadTgiList(reader, stream, header.TgiOffset);
        return new Ts4CasPart
        {
            BodyType = bodyType,
            AgeGenderFlags = ageGender,
            InternalName = NormalizeName(internalName),
            PresetCount = header.PresetCount,
            TgiList = tgiList,
            Lods = lods,
            TextureReferences = BuildTextureReferences(
                tgiList,
                diffuseKey,
                shadowKey,
                regionMapKey,
                normalMapKey,
                specularMapKey,
                emissionMapKey,
                colorShiftMaskKey),
            SwatchColors = swatches,
            SwatchSummary = BuildSwatchSummary(swatches),
            VariantThumbnailTgi = TryResolveTgi(tgiList, variantThumbnailKey)?.FullTgi
        };
    }

    private static IReadOnlyList<Ts4CasTextureCandidate> BuildTextureReferences(
        IReadOnlyList<ResourceKeyRecord> tgiList,
        byte diffuseKey,
        byte shadowKey,
        byte regionMapKey,
        byte normalMapKey,
        byte specularMapKey,
        byte emissionMapKey,
        byte colorShiftMaskKey)
    {
        var candidates = new (string Slot, byte Index, CanonicalTextureSemantic Semantic)[]
        {
            ("diffuse", diffuseKey, CanonicalTextureSemantic.BaseColor),
            ("shadow", shadowKey, CanonicalTextureSemantic.Unknown),
            ("region_map", regionMapKey, CanonicalTextureSemantic.Unknown),
            ("normal", normalMapKey, CanonicalTextureSemantic.Normal),
            ("specular", specularMapKey, CanonicalTextureSemantic.Specular),
            ("emission", emissionMapKey, CanonicalTextureSemantic.Emissive),
            ("color_shift_mask", colorShiftMaskKey, CanonicalTextureSemantic.Unknown)
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<Ts4CasTextureCandidate>(candidates.Length);
        foreach (var candidate in candidates)
        {
            if (candidate.Index >= byte.MaxValue ||
                candidate.Index >= tgiList.Count ||
                tgiList[candidate.Index].Type == 0)
            {
                continue;
            }

            var key = tgiList[candidate.Index];
            var stableKey = $"{candidate.Slot}|{key.FullTgi}";
            if (!seen.Add(stableKey))
            {
                continue;
            }

            references.Add(new Ts4CasTextureCandidate(candidate.Slot, key, candidate.Semantic));
        }

        return references;
    }

    private static ResourceKeyRecord? TryResolveTgi(IReadOnlyList<ResourceKeyRecord> tgiList, byte index) =>
        index < byte.MaxValue &&
        index < tgiList.Count &&
        tgiList[index].Type != 0
            ? tgiList[index]
            : null;

    private static Ts4CasPartHeader ReadHeader(BinaryReader reader, MemoryStream stream, bool requireTgiOffsetInPayload)
    {
        EnsureMinimumLength(stream, 12, "CASPart header");

        var version = reader.ReadUInt32();
        if (version < MinimumSupportedVersion)
        {
            throw new InvalidDataException($"CASPart version {version} is older than the supported minimum {MinimumSupportedVersion}.");
        }

        var dataSize = reader.ReadUInt32();
        var tgiOffset = checked((long)dataSize + 8);
        if (requireTgiOffsetInPayload)
        {
            SetPositionBoundsOnly(stream, tgiOffset, "CASPart TGI table");
        }
        var presetCount = reader.ReadUInt32();
        SkipPresets(reader, stream, presetCount);
        var internalName = ReadBigEndianUnicodeString(reader);
        return new Ts4CasPartHeader(version, presetCount, tgiOffset, internalName);
    }

    private static void SkipPresets(BinaryReader reader, MemoryStream stream, uint presetCount)
    {
        for (var presetIndex = 0; presetIndex < presetCount; presetIndex++)
        {
            _ = reader.ReadUInt64();
            var parameterCount = reader.ReadByte();
            for (var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
            {
                _ = reader.ReadUInt32();
                var parameterType = reader.ReadByte();
                var byteCount = parameterType switch
                {
                    1 => 4L,
                    2 => 4L,
                    3 => 16L,
                    4 => 4L,
                    _ => throw new InvalidDataException($"CASPart preset parameter type {parameterType} is unknown.")
                };
                SkipBytes(stream, byteCount, "CASPart preset parameter");
            }
        }
    }

    private static void SkipTagMultimap(BinaryReader reader, MemoryStream stream, uint version)
    {
        var tagCount = reader.ReadUInt32();
        var bytesPerTag = version >= 37 ? 6L : 4L;
        SkipBytes(stream, tagCount * bytesPerTag, "CASPart tag multimap");
    }

    private static IReadOnlyList<uint> ReadSwatchColors(BinaryReader reader, MemoryStream stream)
    {
        var count = reader.ReadByte();
        var swatches = new uint[count];
        for (var index = 0; index < count; index++)
        {
            EnsureRemainingBytes(stream, sizeof(uint), "CASPart swatch color");
            swatches[index] = reader.ReadUInt32();
        }

        return swatches;
    }

    private static IReadOnlyList<ResourceKeyRecord> ReadTgiList(BinaryReader reader, MemoryStream stream, long tgiOffset)
    {
        SetPosition(stream, tgiOffset, "CASPart TGI table");
        var tgiCount = reader.ReadByte();
        var tgiList = new List<ResourceKeyRecord>(tgiCount);
        for (var index = 0; index < tgiCount; index++)
        {
            EnsureRemainingBytes(stream, sizeof(ulong) + (sizeof(uint) * 2L), "CASPart TGI entry");
            var instance = reader.ReadUInt64();
            var group = reader.ReadUInt32();
            var type = reader.ReadUInt32();
            tgiList.Add(new ResourceKeyRecord(
                type,
                group,
                instance,
                GuessTypeName(type)));
        }

        return tgiList;
    }

    private static string? BuildSwatchSummary(IReadOnlyList<uint> swatches) =>
        swatches.Count == 0
            ? null
            : string.Join(", ", swatches.Take(4).Select(static value => $"#{value:X8}")) +
              (swatches.Count > 4 ? $" (+{swatches.Count - 4} more)" : string.Empty);

    private static string? NormalizeName(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static string GuessTypeName(uint type) => type switch
    {
        0x034AEECB => "CASPart",
        0x015A1849 => "Geometry",
        0x8EAF13DE => "Rig",
        0x3C1AF1F2 => "CASPartThumbnail",
        0x5B282D45 => "BodyPartThumbnail",
        0x00B2D882 => "PNGImage",
        0x2F7D0004 => "PNGImage2",
        0x2BC04EDF => "DSTImage",
        0x0988C7E1 => "LRLEImage",
        0x3453CF95 => "RLE2Image",
        0x1B4D2A70 => "RLESImage",
        _ => $"0x{type:X8}"
    };

    private static string ReadBigEndianUnicodeString(BinaryReader reader)
    {
        var length = Read7BitEncodedInt(reader);
        if (length < 0)
        {
            throw new InvalidDataException("CASPart string length is negative.");
        }

        var stream = reader.BaseStream;
        if (stream.Position + length > stream.Length)
        {
            throw new InvalidDataException("CASPart string extends beyond the payload.");
        }

        return Encoding.BigEndianUnicode.GetString(reader.ReadBytes(length));
    }

    private static int Read7BitEncodedInt(BinaryReader reader)
    {
        var count = 0;
        var shift = 0;
        byte currentByte;
        do
        {
            currentByte = reader.ReadByte();
            count |= (currentByte & 0x7F) << shift;
            shift += 7;
        }
        while ((currentByte & 0x80) != 0);

        return count;
    }

    private static void EnsureMinimumLength(MemoryStream stream, int minimumLength, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.Length < minimumLength)
        {
            throw new InvalidDataException($"{fieldName} is truncated.");
        }
    }

    private static void EnsureRemainingBytes(MemoryStream stream, long byteCount, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (byteCount < 0 || stream.Position + byteCount > stream.Length)
        {
            throw new InvalidDataException($"{fieldName} extends beyond the CASPart payload.");
        }
    }

    private static void SkipBytes(MemoryStream stream, long byteCount, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        EnsureRemainingBytes(stream, byteCount, fieldName);

        stream.Position += byteCount;
    }

    private static void SetPositionBoundsOnly(MemoryStream stream, long position, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (position < 0 || position > stream.Length)
        {
            throw new InvalidDataException($"{fieldName} offset extends beyond the CASPart payload.");
        }
    }

    private static void SetPosition(MemoryStream stream, long position, string fieldName)
    {
        SetPositionBoundsOnly(stream, position, fieldName);
        stream.Position = position;
    }
}

internal readonly record struct Ts4CasPartHeader(uint Version, uint PresetCount, long TgiOffset, string InternalName);

internal readonly record struct Ts4CasLod(byte Level, IReadOnlyList<byte> KeyIndices);

internal readonly record struct Ts4CasTextureReference(string Slot, ResourceMetadata Resource, CanonicalTextureSemantic Semantic, CanonicalTextureSourceKind SourceKind);

internal sealed record CasGeometryCompanionResources(
    IReadOnlyList<ResourceMetadata> RigResources,
    IReadOnlyList<ResourceMetadata> MaterialResources,
    IReadOnlyList<ResourceMetadata> TextureResources,
    IReadOnlyList<string> Diagnostics)
{
    public static CasGeometryCompanionResources Empty { get; } = new([], [], [], []);
}

internal readonly record struct Ts4CasTextureCandidate(string Slot, ResourceKeyRecord Key, CanonicalTextureSemantic Semantic);

internal sealed class Ts4ObjectDefinition
{
    public required ushort Version { get; init; }
    public required uint DeclaredSize { get; init; }
    public required string InternalName { get; init; }
    public required IReadOnlyList<uint> RemainingWords { get; init; }
    public required int RemainingByteCount { get; init; }
    public required IReadOnlyList<Ts4ObjectDefinitionReferenceCandidate> ReferenceCandidates { get; init; }

    public static Ts4ObjectDefinition Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length < 10)
        {
            throw new InvalidDataException("ObjectDefinition payload is too small to contain the confirmed header.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0, 2));
        var declaredSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(2, 4));
        var nameByteLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(6, 4));
        if (nameByteLength == 0)
        {
            throw new InvalidDataException("ObjectDefinition internal name length is zero.");
        }

        if (nameByteLength > int.MaxValue || 10 + nameByteLength > bytes.Length)
        {
            throw new InvalidDataException("ObjectDefinition internal name extends beyond the payload.");
        }

        var nameOffset = 10;
        var internalName = Encoding.ASCII.GetString(bytes, nameOffset, (int)nameByteLength);
        var remainingOffset = nameOffset + (int)nameByteLength;
        var remainingByteCount = bytes.Length - remainingOffset;
        var remainingWords = new List<uint>(remainingByteCount / 4);
        for (var offset = remainingOffset; offset + 4 <= bytes.Length; offset += 4)
        {
            remainingWords.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        }

        var referenceCandidates = ParseReferenceCandidates(bytes);

        return new Ts4ObjectDefinition
        {
            Version = version,
            DeclaredSize = declaredSize,
            InternalName = internalName,
            RemainingWords = remainingWords,
            RemainingByteCount = remainingByteCount,
            ReferenceCandidates = referenceCandidates
        };
    }

    private static IReadOnlyList<Ts4ObjectDefinitionReferenceCandidate> ParseReferenceCandidates(byte[] bytes)
    {
        var candidates = new List<Ts4ObjectDefinitionReferenceCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var offset = 0; offset + 20 <= bytes.Length; offset++)
        {
            var marker = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
            if (marker is not 4u and not 9u and not 12u)
            {
                continue;
            }

            var rawInstance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset + 4, 8));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 12, 4));
            var group = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 16, 4));
            var typeName = GuessTypeName(type);
            if (type == 0 || typeName.StartsWith("0x", StringComparison.Ordinal))
            {
                continue;
            }

            var rawKey = new ResourceKeyRecord(type, group, rawInstance, typeName);
            var swappedInstanceKey = rawKey with { FullInstance = SwapUInt32Halves(rawKey.FullInstance) };
            var candidate = new Ts4ObjectDefinitionReferenceCandidate(
                offset,
                marker,
                rawKey,
                swappedInstanceKey);
            if (!seen.Add($"{candidate.Offset}:{candidate.RawKey.FullTgi}:{candidate.Swap32Key.FullTgi}"))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        return candidates;
    }

    private static ulong SwapUInt32Halves(ulong value) =>
        ((value & 0xFFFFFFFFUL) << 32) | (value >> 32);

    private static string GuessTypeName(uint type) => type switch
    {
        0x01661233 => "Model",
        0xD382BF57 => "Footprint",
        0xD3044521 => "Slot",
        0x8EAF13DE => "Rig",
        0x319E4F1D => "ObjectCatalog",
        0xC0DB5AE7 => "ObjectDefinition",
        _ => $"0x{type:X8}"
    };
}

internal readonly record struct Ts4ObjectDefinitionReferenceCandidate(
    int Offset,
    uint Marker,
    ResourceKeyRecord RawKey,
    ResourceKeyRecord Swap32Key);

internal sealed class Ts4ObjectCatalog
{
    public required uint? NameHash { get; init; }
    public required uint? DescriptionHash { get; init; }
    public required uint? Word0020 { get; init; }
    public required uint? Word002C { get; init; }
    public required uint? Word0030 { get; init; }
    public required uint? Word0034 { get; init; }
    public required IReadOnlyList<uint> Words { get; init; }
    public required IReadOnlyList<ulong> TailQwordCandidates { get; init; }
    public required string? RawCategorySignalSummary { get; init; }
    public required string ApproximationNote { get; init; }

    public static Ts4ObjectCatalog Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length < 16)
        {
            throw new InvalidDataException("ObjectCatalog payload is too small for the current heuristic survey.");
        }

        var words = new List<uint>(bytes.Length / 4);
        for (var offset = 0; offset + 4 <= bytes.Length; offset += 4)
        {
            words.Add(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        }

        var tailQwordCandidates = new List<ulong>();
        for (var offset = Math.Max(0, bytes.Length - 32); offset + 8 <= bytes.Length; offset += 8)
        {
            tailQwordCandidates.Add(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(offset, 8)));
        }

        return new Ts4ObjectCatalog
        {
            NameHash = words.Count > 2 && words[2] != 0 ? words[2] : null,
            DescriptionHash = words.Count > 3 && words[3] != 0 ? words[3] : null,
            Word0020 = GetWord(words, 0x0020),
            Word002C = GetWord(words, 0x002C),
            Word0030 = GetWord(words, 0x0030),
            Word0034 = GetWord(words, 0x0034),
            Words = words,
            TailQwordCandidates = tailQwordCandidates,
            RawCategorySignalSummary = BuildRawCategorySignalSummary(words),
            ApproximationNote = "NameHash (+0x08) and DescriptionHash (+0x0C) are confirmed raw localization hashes. Category and tag fields remain heuristic."
        };
    }

    private static uint? GetWord(IReadOnlyList<uint> words, int byteOffset)
    {
        var index = byteOffset / 4;
        return index < words.Count && words[index] != 0
            ? words[index]
            : null;
    }

    private static string? BuildRawCategorySignalSummary(IReadOnlyList<uint> words)
    {
        var parts = new List<string>(4);
        AppendSignal(parts, "+0x0020", GetWord(words, 0x0020));
        AppendSignal(parts, "+0x002C", GetWord(words, 0x002C));
        AppendSignal(parts, "+0x0030", GetWord(words, 0x0030));
        AppendSignal(parts, "+0x0034", GetWord(words, 0x0034));
        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static void AppendSignal(List<string> parts, string label, uint? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        parts.Add($"{label}=0x{value.Value:X8}");
    }
}
