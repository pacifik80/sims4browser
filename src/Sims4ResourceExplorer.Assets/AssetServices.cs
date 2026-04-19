using System.Text;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Packages;

namespace Sims4ResourceExplorer.Assets;

internal sealed record SimArchetypeCandidate(
    ResourceMetadata Resource,
    string? Species,
    string? Age,
    string? Gender,
    int OutfitPartCount,
    int BodyModifierCount,
    int FaceModifierCount,
    bool HasSkintone);

public sealed class ExplicitAssetGraphBuilder : IAssetGraphBuilder
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore? indexStore;
    private readonly ConcurrentDictionary<string, SimBodyAssemblyCandidateFacts> bodyAssemblyCandidateFactsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<AssetSummary>>> indexedDefaultBodyRecipeCandidateCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<(string PackagePath, ulong FullInstance), Task<IReadOnlyList<ResourceMetadata>>> packageInstanceResourcesCache = new();
    private readonly ConcurrentDictionary<string, Task<IReadOnlyList<ResourceMetadata>>> resourcesByTgiCache = new(StringComparer.OrdinalIgnoreCase);

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
        summaries.AddRange(BuildSimSummaries(resources, claimedInstances));
        summaries.AddRange(BuildGeneral3DSummaries(sameInstanceLookup, claimedInstances));
        return summaries;
    }

    public async Task<AssetGraph> BuildAssetGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken)
    {
        return summary.AssetKind switch
        {
            AssetKind.BuildBuy => await BuildBuildBuyGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            AssetKind.Cas => await BuildCasGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            AssetKind.Sim => await BuildSimGraphAsync(summary, packageResources, allowPreferredTemplateRedirect: true, includeCompatibilityFallbackCandidates: false, includeCasSlotCandidates: true, cancellationToken).ConfigureAwait(false),
            AssetKind.General3D => await BuildGeneral3DGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false),
            _ => new AssetGraph(summary, [], [$"Unsupported asset kind: {summary.AssetKind}."])
        };
    }

    public async Task<AssetGraph> BuildPreviewGraphAsync(AssetSummary summary, IReadOnlyList<ResourceMetadata> packageResources, CancellationToken cancellationToken)
    {
        return summary.AssetKind switch
        {
            AssetKind.Sim => await BuildSimGraphAsync(summary, packageResources, allowPreferredTemplateRedirect: true, includeCompatibilityFallbackCandidates: false, includeCasSlotCandidates: false, cancellationToken).ConfigureAwait(false),
            _ => await BuildAssetGraphAsync(summary, packageResources, cancellationToken).ConfigureAwait(false)
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

    private IEnumerable<AssetSummary> BuildCasSummaries(
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

            var slotCategory = Ts4SeedMetadataExtractor.TryExtractCasPartSlotCategory(casPart.Description) ?? "CAS";
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
                slotCategory,
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
                CategoryNormalized: NormalizeAssetCategory(slotCategory),
                Description: casPart.Description);
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

    private static IEnumerable<AssetSummary> BuildSimSummaries(
        IReadOnlyList<ResourceMetadata> resources,
        ISet<ulong> claimedInstances)
    {
        var simGroups = resources
            .Where(static resource => resource.Key.TypeName == "SimInfo")
            .OrderBy(static resource => resource.Key.FullInstance)
            .Select(static resource => new SimArchetypeCandidate(
                resource,
                Ts4SimInfoParser.TryExtractSpeciesLabelFromSummary(resource.Description),
                Ts4SimInfoParser.TryExtractAgeLabelFromSummary(resource.Description),
                Ts4SimInfoParser.TryExtractGenderLabelFromSummary(resource.Description),
                Ts4SimInfoParser.TryExtractOutfitPartCountFromSummary(resource.Description) ?? 0,
                Ts4SimInfoParser.TryExtractBodyModifierCountFromSummary(resource.Description) ?? 0,
                Ts4SimInfoParser.TryExtractFaceModifierCountFromSummary(resource.Description) ?? 0,
                !string.IsNullOrWhiteSpace(Ts4SimInfoParser.TryExtractSkintoneInstanceFromSummary(resource.Description))))
            // GP01 full-build legacy SimInfo rows can still parse into opaque v20 "Species 0x..." summaries,
            // while matching delta rows for the same templates already provide sane human identity.
            .Where(static candidate => HasSimArchetypeIdentity(candidate) &&
                                       !Ts4SimInfoParser.IsLegacyOpaqueSpeciesSummary(candidate.Resource.Description))
            .GroupBy(
                static candidate => BuildSimArchetypeGroupingKey(candidate),
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in simGroups)
        {
            var members = group.ToArray();
            foreach (var member in members)
            {
                if (!claimedInstances.Contains(member.Resource.Key.FullInstance))
                {
                    claimedInstances.Add(member.Resource.Key.FullInstance);
                }
            }

            var representative = SelectSimArchetypeRepresentative(members);
            var species = representative.Species ?? "Sim";
            var age = representative.Age ?? "Unknown";
            var gender = representative.Gender ?? "Unknown";
            var category = species;
            var categoryNormalized = category
                .Replace(" ", string.Empty, StringComparison.Ordinal)
                .ToLowerInvariant();
            var displayName = Ts4SimInfoParser.BuildArchetypeDisplayName(species, age, gender);
            var logicalRoot = group.Key.StartsWith("sim-archetype:", StringComparison.OrdinalIgnoreCase)
                ? group.Key
                : $"{group.Key}|instance={representative.Resource.Key.FullInstance:X16}";
            var missingMetadataCount = members.Count(static member => string.IsNullOrWhiteSpace(member.Resource.Description));
            var diagnostics = new List<string>();
            if (missingMetadataCount > 0)
            {
                diagnostics.Add($"{missingMetadataCount} SimInfo row(s) in this archetype did not provide enriched metadata.");
            }

            diagnostics.Add($"Grouped {members.Length} SimInfo template row(s) into archetype {displayName}.");

            var description = BuildSimArchetypeDescription(
                species,
                age,
                gender,
                members.Length,
                representative.Resource.Description);

            yield return new AssetSummary(
                StableEntityIds.ForAsset(representative.Resource.DataSourceId, AssetKind.Sim, representative.Resource.PackagePath, representative.Resource.Key),
                representative.Resource.DataSourceId,
                representative.Resource.SourceKind,
                AssetKind.Sim,
                displayName,
                category,
                representative.Resource.PackagePath,
                representative.Resource.Key,
                ThumbnailTgi: null,
                VariantCount: members.Length,
                LinkedResourceCount: Math.Max(0, members.Length - 1),
                Diagnostics: string.Join(" ", diagnostics),
                Capabilities: new AssetCapabilitySnapshot(
                    HasSceneRoot: false,
                    HasExactGeometryCandidate: false,
                    HasMaterialReferences: false,
                    HasTextureReferences: false,
                    HasThumbnail: false,
                    HasVariants: members.Length > 1,
                    HasIdentityMetadata: true,
                    HasRigReference: false,
                    HasGeometryReference: false,
                    HasMaterialResourceCandidate: false,
                    HasTextureResourceCandidate: false,
                    IsPackageLocalGraph: true,
                    HasDiagnostics: diagnostics.Count > 0),
                PackageName: Path.GetFileName(representative.Resource.PackagePath),
                RootTypeName: "SimArchetype",
                ThumbnailTypeName: null,
                PrimaryGeometryType: null,
                IdentityType: "SimInfo",
                CategoryNormalized: categoryNormalized,
                Description: description,
                LogicalRootTgi: logicalRoot);
        }
    }

    private static string BuildSimArchetypeGroupingKey(SimArchetypeCandidate candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.Species) &&
            string.IsNullOrWhiteSpace(candidate.Age) &&
            string.IsNullOrWhiteSpace(candidate.Gender))
        {
            return $"sim-unparsed:{candidate.Resource.Key.FullInstance:X16}";
        }

        return Ts4SimInfoParser.BuildArchetypeLogicalKey(
            candidate.Species ?? "Unknown",
            candidate.Age ?? "Unknown",
            candidate.Gender ?? "Unknown");
    }

    private static bool HasSimArchetypeIdentity(SimArchetypeCandidate candidate) =>
        !string.IsNullOrWhiteSpace(candidate.Species) ||
        !string.IsNullOrWhiteSpace(candidate.Age) ||
        !string.IsNullOrWhiteSpace(candidate.Gender);

    private static SimArchetypeCandidate SelectSimArchetypeRepresentative(IReadOnlyList<SimArchetypeCandidate> members) =>
        members
            .OrderByDescending(static member => member.OutfitPartCount)
            .ThenByDescending(static member => member.BodyModifierCount + member.FaceModifierCount)
            .ThenByDescending(static member => member.HasSkintone)
            .ThenByDescending(static member => !string.IsNullOrWhiteSpace(member.Resource.Description))
            .ThenByDescending(static member => member.Resource.Description?.Length ?? 0)
            .ThenByDescending(static member => member.Resource.Name?.Length ?? 0)
            .ThenBy(static member => member.Resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static member => member.Resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .First();

    private static string BuildSimArchetypeDescription(
        string species,
        string age,
        string gender,
        int templateCount,
        string? representativeDescription)
    {
        var parts = new List<string>
        {
            "Sim archetype",
            $"species={species}",
            $"age={age}",
            $"gender={gender}",
            $"templates={templateCount}"
        };

        if (!string.IsNullOrWhiteSpace(representativeDescription))
        {
            parts.Add($"representative={representativeDescription}");
        }

        return string.Join(" | ", parts);
    }

    private async Task<AssetGraph> BuildSimGraphAsync(
        AssetSummary summary,
        IReadOnlyList<ResourceMetadata> packageResources,
        bool allowPreferredTemplateRedirect,
        bool includeCompatibilityFallbackCandidates,
        bool includeCasSlotCandidates,
        CancellationToken cancellationToken)
    {
        var graphStopwatch = Stopwatch.StartNew();
        var simInfoResource = packageResources.FirstOrDefault(resource =>
            string.Equals(resource.Key.FullTgi, summary.RootKey.FullTgi, StringComparison.OrdinalIgnoreCase) ||
            (resource.Key.TypeName == "SimInfo" && resource.Key.FullInstance == summary.RootKey.FullInstance));
        if (simInfoResource is null)
        {
            return new AssetGraph(summary, packageResources, ["Selected SimInfo root could not be loaded from the package."]);
        }

        var diagnostics = new List<string>();
        Ts4SimInfo? parsedSimInfo = null;
        SimInfoSummary? metadata = null;
        var parseElapsed = TimeSpan.Zero;
        try
        {
            var bytes = await resourceCatalogService
                .GetResourceBytesAsync(simInfoResource.PackagePath, simInfoResource.Key, raw: false, cancellationToken)
                .ConfigureAwait(false);
            parsedSimInfo = Ts4SimInfoParser.Parse(bytes);
            metadata = parsedSimInfo.ToSummary();
        }
        catch (InvalidDataException ex)
        {
            diagnostics.Add($"SimInfo metadata parse failed: {ex.Message}");
        }
        catch (EndOfStreamException ex)
        {
            diagnostics.Add($"SimInfo metadata parse failed: {ex.Message}");
        }
        catch (ArgumentOutOfRangeException ex)
        {
            diagnostics.Add($"SimInfo metadata parse failed: {ex.Message}");
        }
        finally
        {
            parseElapsed = graphStopwatch.Elapsed;
        }

        metadata ??= new SimInfoSummary(
            Version: 0,
            SpeciesLabel: summary.Category ?? "Sim",
            AgeLabel: "Unknown",
            GenderLabel: "Unknown",
            PronounCount: 0,
            OutfitCategoryCount: 0,
            OutfitEntryCount: 0,
            OutfitPartCount: 0,
            TraitCount: 0,
            FaceModifierCount: 0,
            BodyModifierCount: 0,
            GeneticFaceModifierCount: 0,
            GeneticBodyModifierCount: 0,
            SculptCount: 0,
            GeneticSculptCount: 0,
            GeneticPartCount: 0,
            GrowthPartCount: 0,
            PeltLayerCount: 0);

        if (summary.VariantCount > 1)
        {
            diagnostics.Add($"This archetype currently groups {summary.VariantCount} SimInfo template row(s) under one top-level entry.");
        }

        var templateStopwatch = Stopwatch.StartNew();
        var templateOptions = await BuildSimTemplateOptionsAsync(metadata, simInfoResource, cancellationToken).ConfigureAwait(false);
        var templateElapsed = templateStopwatch.Elapsed;
        if (allowPreferredTemplateRedirect)
        {
            var preferredTemplate = await ResolvePreferredTemplateOptionAsync(
                templateOptions,
                simInfoResource,
                cancellationToken).ConfigureAwait(false);
            if (preferredTemplate is not null &&
                preferredTemplate.ResourceId != simInfoResource.Id)
            {
                var redirectedGraph = await TryBuildSimGraphForPreferredTemplateAsync(
                    summary,
                    preferredTemplate,
                    includeCompatibilityFallbackCandidates,
                    includeCasSlotCandidates,
                    cancellationToken).ConfigureAwait(false);
                if (redirectedGraph?.SimGraph is not null)
                {
                    var redirectNote = BuildSimTemplateRedirectNote(preferredTemplate, redirectedGraph.SimGraph);
                    return redirectedGraph with
                    {
                        Diagnostics = [.. redirectedGraph.Diagnostics, redirectNote],
                        SimGraph = redirectedGraph.SimGraph with
                        {
                            Diagnostics = [.. redirectedGraph.SimGraph.Diagnostics, redirectNote]
                        }
                    };
                }
            }
        }

        var bodyFoundation = BuildSimBodyFoundation(metadata);
        var bodySources = BuildSimBodySources(parsedSimInfo);
        var bodyCandidateStopwatch = Stopwatch.StartNew();
        var bodyCandidates = await BuildSimBodyCandidatesAsync(
            simInfoResource,
            parsedSimInfo,
            simInfoResource.PackagePath,
            includeCompatibilityFallbackCandidates,
            cancellationToken).ConfigureAwait(false);
        var bodyCandidateElapsed = bodyCandidateStopwatch.Elapsed;
        var casSlotStopwatch = Stopwatch.StartNew();
        var casSlotCandidates = includeCasSlotCandidates
            ? await BuildSimCasSlotCandidatesAsync(
                parsedSimInfo,
                metadata,
                simInfoResource.PackagePath,
                cancellationToken).ConfigureAwait(false)
            : [];
        var casSlotElapsed = casSlotStopwatch.Elapsed;
        var skintoneStopwatch = Stopwatch.StartNew();
        var skintoneRender = await TryResolveSimSkintoneRenderSummaryAsync(
            metadata,
            simInfoResource.PackagePath,
            cancellationToken).ConfigureAwait(false);
        var skintoneElapsed = skintoneStopwatch.Elapsed;
        var bodyAssembly = BuildSimBodyAssembly(metadata, bodyCandidates, casSlotCandidates);
        var slotGroups = BuildSimSlotGroups(metadata);
        var morphGroups = BuildSimMorphGroups(metadata);
        if (!string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add($"Indexed CAS slot-family matching currently targets human archetypes first; {metadata.SpeciesLabel} coverage is still in progress.");
        }

        diagnostics.Add(
            $"Sim graph timings: parse {parseElapsed.TotalMilliseconds:0} ms | templates {templateElapsed.TotalMilliseconds:0} ms | body candidates {bodyCandidateElapsed.TotalMilliseconds:0} ms | CAS slots {casSlotElapsed.TotalMilliseconds:0} ms | skintone {skintoneElapsed.TotalMilliseconds:0} ms | total {graphStopwatch.Elapsed.TotalMilliseconds:0} ms.");

        var supportedSubset = "Metadata-first Sim archetypes derived from grouped SimInfo rows. This slice now prioritizes body assembly inputs first: base frame, skintone, body-layer counts, and morph-stack counts are surfaced before apparel slot editing; full named/preset character assembly is not implemented yet.";
        var simGraph = new SimAssetGraph(
            simInfoResource,
            metadata,
            skintoneRender,
            templateOptions,
            bodyFoundation,
            bodySources,
            bodyCandidates,
            bodyAssembly,
            slotGroups,
            morphGroups,
            casSlotCandidates,
            [simInfoResource],
            diagnostics,
            IsSupported: true,
            supportedSubset);
        return new AssetGraph(summary, [simInfoResource], diagnostics, SimGraph: simGraph);
    }

    private async Task<AssetGraph?> TryBuildSimGraphForPreferredTemplateAsync(
        AssetSummary summary,
        SimTemplateOptionSummary preferredTemplate,
        bool includeCompatibilityFallbackCandidates,
        bool includeCasSlotCandidates,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return null;
        }

        var preferredResource = await indexStore
            .GetResourceByTgiAsync(preferredTemplate.PackagePath, preferredTemplate.RootTgi, cancellationToken)
            .ConfigureAwait(false);
        if (preferredResource is null)
        {
            return null;
        }

        var preferredPackageResources = await indexStore
            .GetResourcesByInstanceAsync(preferredResource.PackagePath, preferredResource.Key.FullInstance, cancellationToken)
            .ConfigureAwait(false);
        var preferredSummary = summary with
        {
            PackagePath = preferredResource.PackagePath,
            PackageName = Path.GetFileName(preferredResource.PackagePath),
            RootKey = preferredResource.Key
        };

        return await BuildSimGraphAsync(
            preferredSummary,
            preferredPackageResources,
            allowPreferredTemplateRedirect: false,
            includeCompatibilityFallbackCandidates,
            includeCasSlotCandidates,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SimTemplateOptionSummary>> BuildSimTemplateOptionsAsync(
        SimInfoSummary metadata,
        ResourceMetadata representativeResource,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [CreateTemplateOption(representativeResource, isRepresentative: true)];
        }

        var archetypeKey = Ts4SimInfoParser.BuildArchetypeLogicalKey(
            metadata.SpeciesLabel,
            metadata.AgeLabel,
            metadata.GenderLabel);
        try
        {
            var indexedFacts = await indexStore
                .GetSimTemplateFactsByArchetypeAsync(archetypeKey, cancellationToken)
                .ConfigureAwait(false);
            if (indexedFacts.Count > 0)
            {
                var indexedOptions = indexedFacts
                    .GroupBy(static fact => $"{fact.PackagePath}|{fact.RootTgi}", StringComparer.OrdinalIgnoreCase)
                    .Select(static group => group.First())
                    .Select(fact => CreateTemplateOption(
                        fact,
                        fact.ResourceId == representativeResource.Id ||
                        string.Equals(fact.RootTgi, representativeResource.Key.FullTgi, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                if (!indexedOptions.Any(option => option.ResourceId == representativeResource.Id))
                {
                    indexedOptions.Add(CreateTemplateOption(representativeResource, isRepresentative: true));
                }

                return OrderAndTrimTemplateOptions(indexedOptions, representativeResource);
            }
        }
        catch (NotSupportedException)
        {
        }

        var matches = new List<ResourceMetadata>();
        try
        {
            var exactVariantMatches = await indexStore
                .GetResourcesByTgiAsync(representativeResource.Key.FullTgi, cancellationToken)
                .ConfigureAwait(false);
            matches.AddRange(
                exactVariantMatches.Where(resource =>
                    string.Equals(resource.Key.TypeName, "SimInfo", StringComparison.OrdinalIgnoreCase) &&
                    MatchesSimTemplateResource(resource, metadata)));

            const int pageSize = 1024;
            const int maxSearchWindow = 8192;
            var searchText = BuildSimTemplateSearchText(metadata);
            for (var offset = 0; offset < maxSearchWindow; offset += pageSize)
            {
                var result = await indexStore.QueryResourcesAsync(
                    new RawResourceBrowserQuery(
                        new SourceScope(),
                        searchText,
                        RawResourceDomain.All,
                        "SimInfo",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        false,
                        false,
                        false,
                        ResourceLinkFilter.Any,
                        RawResourceSort.TypeName,
                        offset,
                        pageSize),
                    cancellationToken).ConfigureAwait(false);
                matches.AddRange(
                    result.Items.Where(resource =>
                        string.Equals(resource.Key.TypeName, "SimInfo", StringComparison.OrdinalIgnoreCase) &&
                        MatchesSimTemplateResource(resource, metadata)));

                if (offset + pageSize >= result.TotalCount)
                {
                    break;
                }
            }
        }
        catch (NotSupportedException)
        {
            return [CreateTemplateOption(representativeResource, isRepresentative: true)];
        }

        if (!matches.Any(resource => resource.Id == representativeResource.Id))
        {
            matches.Add(representativeResource);
        }

        var options = matches
            .GroupBy(
                static resource => $"{resource.PackagePath}|{resource.Key.FullTgi}",
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .Select(resource => CreateTemplateOption(
                resource,
                resource.Id == representativeResource.Id))
            .ToArray();

        return OrderAndTrimTemplateOptions(options, representativeResource);
    }

    private static IReadOnlyList<SimTemplateOptionSummary> OrderAndTrimTemplateOptions(
        IEnumerable<SimTemplateOptionSummary> options,
        ResourceMetadata representativeResource)
    {
        var optionArray = options.ToArray();
        var representativeOption = optionArray.FirstOrDefault(option => option.ResourceId == representativeResource.Id);
        var exactVariantOptions = optionArray
            .Where(option => string.Equals(option.RootTgi, representativeResource.Key.FullTgi, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var exactVariantPreferred = SelectPreferredTemplatePackageVariant(exactVariantOptions);
        var preferredOption = exactVariantPreferred is not null &&
                              exactVariantPreferred.ResourceId != representativeResource.Id &&
                              IsTemplatePackageVariantPreferredOver(exactVariantPreferred, representativeOption)
            ? exactVariantPreferred
            : SimTemplateSelectionPolicy.SelectPreferredTemplate(optionArray);
        var orderedOptions = SimTemplateSelectionPolicy.OrderTemplates(optionArray).ToArray();
        var orderedExactVariantOptions = OrderTemplatePackageVariants(exactVariantOptions).ToArray();

        var finalOptions = new List<SimTemplateOptionSummary>(capacity: Math.Min(256, orderedOptions.Length));
        if (preferredOption is not null)
        {
            finalOptions.Add(preferredOption);
        }

        if (representativeOption is not null &&
            !finalOptions.Any(option => option.ResourceId == representativeOption.ResourceId))
        {
            finalOptions.Add(representativeOption);
        }

        foreach (var option in orderedExactVariantOptions)
        {
            if (finalOptions.Count >= 256)
            {
                break;
            }

            if (finalOptions.Any(existing => existing.ResourceId == option.ResourceId))
            {
                continue;
            }

            finalOptions.Add(option);
        }

        foreach (var option in orderedOptions)
        {
            if (finalOptions.Count >= 256)
            {
                break;
            }

            if (finalOptions.Any(existing => existing.ResourceId == option.ResourceId))
            {
                continue;
            }

            finalOptions.Add(option);
        }

        return finalOptions;
    }

    private static SimTemplateOptionSummary CreateTemplateOption(SimTemplateFactSummary fact, bool isRepresentative) =>
        new(
            fact.ResourceId,
            fact.DisplayName,
            fact.PackagePath,
            Path.GetFileName(fact.PackagePath),
            fact.RootTgi,
            isRepresentative,
            fact.Notes,
            fact.OutfitCategoryCount,
            fact.OutfitEntryCount,
            fact.OutfitPartCount,
            fact.BodyModifierCount,
            fact.FaceModifierCount,
            fact.SculptCount,
            fact.HasSkintone,
            fact.AuthoritativeBodyDrivingOutfitCount,
            fact.AuthoritativeBodyDrivingOutfitPartCount);

    private async Task<SimTemplateOptionSummary?> ResolvePreferredTemplateOptionAsync(
        IReadOnlyList<SimTemplateOptionSummary> templateOptions,
        ResourceMetadata representativeResource,
        CancellationToken cancellationToken)
    {
        var initialInspectionLimit = Math.Min(32, templateOptions.Count);
        var extendedInspectionLimit = templateOptions.Count;

        var summaryPreferred = SimTemplateSelectionPolicy.SelectPreferredTemplate(templateOptions);
        if (indexStore is null || templateOptions.Count == 0)
        {
            return summaryPreferred;
        }

        var indexedPreferred = SelectPreferredTemplateFromIndexedFacts(templateOptions, summaryPreferred);
        if (indexedPreferred is not null)
        {
            return indexedPreferred;
        }

        var optionsToInspect = new List<SimTemplateOptionSummary>(capacity: Math.Min(extendedInspectionLimit, templateOptions.Count));
        var inspectedResourceIds = new HashSet<Guid>();
        var inspectedCandidates = new List<SimTemplateSelectionCandidate>(capacity: Math.Min(extendedInspectionLimit, templateOptions.Count));

        void AddOption(SimTemplateOptionSummary option)
        {
            if (optionsToInspect.Any(existing => existing.ResourceId == option.ResourceId))
            {
                return;
            }

            optionsToInspect.Add(option);
        }

        if (summaryPreferred is not null)
        {
            AddOption(summaryPreferred);
        }

        foreach (var option in SimTemplateSelectionPolicy.OrderTemplates(
                     templateOptions.Where(option =>
                         string.Equals(option.RootTgi, representativeResource.Key.FullTgi, StringComparison.OrdinalIgnoreCase))))
        {
            if (optionsToInspect.Count >= initialInspectionLimit)
            {
                break;
            }

            AddOption(option);
        }

        foreach (var option in SimTemplateSelectionPolicy.OrderTemplates(
                     templateOptions.Where(static option => option.HasAuthoritativeBodyParts)))
        {
            if (optionsToInspect.Count >= initialInspectionLimit)
            {
                break;
            }

            AddOption(option);
        }

        async Task InspectQueuedOptionsAsync()
        {
            foreach (var option in optionsToInspect)
            {
                if (!inspectedResourceIds.Add(option.ResourceId))
                {
                    continue;
                }

                var candidate = await TryLoadSimTemplateSelectionCandidateAsync(option, cancellationToken).ConfigureAwait(false);
                if (candidate is not null)
                {
                    inspectedCandidates.Add(candidate);
                }
            }
        }

        await InspectQueuedOptionsAsync().ConfigureAwait(false);
        if (!inspectedCandidates.Any(static candidate => GetAuthoritativeBodyDrivingOutfitCount(candidate.Metadata) > 0))
        {
            foreach (var option in OrderTemplatesForAuthoritativeBodyDrivingInspection(templateOptions))
            {
                if (optionsToInspect.Count >= extendedInspectionLimit)
                {
                    break;
                }

                AddOption(option);
            }

            await InspectQueuedOptionsAsync().ConfigureAwait(false);
        }

        if (inspectedCandidates.Count == 0)
        {
            return summaryPreferred;
        }

        return SelectPreferredTemplateFromInspectedCandidates(inspectedCandidates, summaryPreferred);
    }

    private static SimTemplateOptionSummary? SelectPreferredTemplateFromIndexedFacts(
        IReadOnlyList<SimTemplateOptionSummary> templateOptions,
        SimTemplateOptionSummary? summaryPreferred)
    {
        var indexedOptions = templateOptions
            .Where(static option => option.HasIndexedAuthoritativeBodyDrivingFacts)
            .ToArray();
        if (indexedOptions.Length == 0)
        {
            return null;
        }

        var authoritativeIndexedOptions = indexedOptions
            .Where(static option => option.AuthoritativeBodyDrivingOutfitCount > 0)
            .OrderByDescending(static option => option.AuthoritativeBodyDrivingOutfitCount)
            .ThenByDescending(static option => option.AuthoritativeBodyDrivingOutfitPartCount)
            .ThenBy(static option => option.OutfitCategoryCount)
            .ThenBy(static option => option.OutfitEntryCount)
            .ThenBy(static option => option.OutfitPartCount)
            .ThenByDescending(static option => option.BodyModifierCount + option.SculptCount)
            .ThenByDescending(static option => option.FaceModifierCount)
            .ThenByDescending(static option => option.HasSkintone)
            .ThenBy(static option => GetSimTemplatePackagePreference(option.PackagePath))
            .ThenByDescending(static option => option.IsRepresentative)
            .ThenBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (authoritativeIndexedOptions.Length > 0)
        {
            return authoritativeIndexedOptions[0];
        }

        return indexedOptions.Length == templateOptions.Count
            ? SimTemplateSelectionPolicy.SelectPreferredTemplate(indexedOptions) ?? summaryPreferred
            : null;
    }

    private static SimTemplateOptionSummary? SelectPreferredTemplateFromInspectedCandidates(
        IReadOnlyList<SimTemplateSelectionCandidate> inspectedCandidates,
        SimTemplateOptionSummary? summaryPreferred)
    {
        ArgumentNullException.ThrowIfNull(inspectedCandidates);

        var authoritativeBodyDrivingCandidates = inspectedCandidates
            .Where(static candidate => GetAuthoritativeBodyDrivingOutfitCount(candidate.Metadata) > 0)
            .ToArray();
        if (authoritativeBodyDrivingCandidates.Length > 0)
        {
            return authoritativeBodyDrivingCandidates
                .OrderByDescending(static candidate => GetAuthoritativeBodyDrivingOutfitCount(candidate.Metadata))
                .ThenByDescending(static candidate => GetAuthoritativeBodyDrivingOutfitPartCount(candidate.Metadata))
                .ThenBy(static candidate => candidate.Metadata.OutfitCategoryCount)
                .ThenBy(static candidate => candidate.Metadata.OutfitEntryCount)
                .ThenBy(static candidate => candidate.Metadata.OutfitPartCount)
                .ThenByDescending(static candidate => candidate.Metadata.BodyModifierCount + candidate.Metadata.SculptCount)
                .ThenByDescending(static candidate => candidate.Metadata.FaceModifierCount)
                .ThenByDescending(static candidate => candidate.Metadata.SkintoneInstance != 0 || candidate.Metadata.SkintoneShift.HasValue)
                .ThenBy(static candidate => GetSimTemplatePackagePreference(candidate.Resource.PackagePath))
                .ThenByDescending(static candidate => candidate.Option.IsRepresentative)
                .ThenBy(static candidate => candidate.Option.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(static candidate => candidate.Option)
                .FirstOrDefault()
                ?? summaryPreferred;
        }

        return inspectedCandidates
            .OrderByDescending(static candidate =>
                candidate.Metadata.OutfitPartCount > 0 ||
                candidate.Metadata.OutfitEntryCount > 0 ||
                candidate.Metadata.OutfitCategoryCount > 0)
            .ThenByDescending(static candidate => GetAuthoritativeBodyDrivingOutfitCount(candidate.Metadata))
            .ThenByDescending(static candidate => GetAuthoritativeBodyDrivingOutfitPartCount(candidate.Metadata))
            .ThenBy(static candidate => candidate.Metadata.OutfitCategoryCount)
            .ThenBy(static candidate => candidate.Metadata.OutfitEntryCount)
            .ThenBy(static candidate => candidate.Metadata.OutfitPartCount)
            .ThenByDescending(static candidate => candidate.Metadata.BodyModifierCount + candidate.Metadata.SculptCount)
            .ThenByDescending(static candidate => candidate.Metadata.FaceModifierCount)
            .ThenByDescending(static candidate => candidate.Metadata.SkintoneInstance != 0 || candidate.Metadata.SkintoneShift.HasValue)
            .ThenBy(static candidate => GetSimTemplatePackagePreference(candidate.Resource.PackagePath))
            .ThenByDescending(static candidate => candidate.Option.IsRepresentative)
            .ThenBy(static candidate => candidate.Option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(static candidate => candidate.Option)
            .FirstOrDefault()
            ?? summaryPreferred;
    }

    private static IOrderedEnumerable<SimTemplateOptionSummary> OrderTemplatesForAuthoritativeBodyDrivingInspection(
        IEnumerable<SimTemplateOptionSummary> templateOptions) =>
        templateOptions
            .Where(static option => option.HasAuthoritativeBodyParts)
            .OrderBy(static option => option.OutfitCategoryCount)
            .ThenBy(static option => option.OutfitEntryCount)
            .ThenBy(static option => option.OutfitPartCount)
            .ThenByDescending(static option => option.BodyModifierCount + option.SculptCount)
            .ThenByDescending(static option => option.FaceModifierCount)
            .ThenByDescending(static option => option.HasSkintone)
            .ThenBy(static option => GetSimTemplatePackagePreference(option.PackagePath))
            .ThenByDescending(static option => option.IsRepresentative)
            .ThenBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static option => option.PackagePath, StringComparer.OrdinalIgnoreCase);

    private async Task<SimTemplateSelectionCandidate?> TryLoadSimTemplateSelectionCandidateAsync(
        SimTemplateOptionSummary option,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return null;
        }

        var resource = await indexStore
            .GetResourceByTgiAsync(option.PackagePath, option.RootTgi, cancellationToken)
            .ConfigureAwait(false);
        if (resource is null)
        {
            return null;
        }

        try
        {
            var bytes = await resourceCatalogService
                .GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken)
                .ConfigureAwait(false);
            return new SimTemplateSelectionCandidate(
                option,
                resource,
                Ts4SimInfoParser.Parse(bytes));
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static int GetSimTemplatePackagePreference(string packagePath)
    {
        var normalized = packagePath.Replace('/', '\\');
        if (normalized.Contains("\\Delta\\", StringComparison.OrdinalIgnoreCase))
        {
            return 300;
        }

        if (normalized.Contains("SimulationDeltaBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 250;
        }

        if (normalized.Contains("ClientDeltaBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 240;
        }

        if (normalized.Contains("SimulationPreload", StringComparison.OrdinalIgnoreCase))
        {
            return 120;
        }

        if (normalized.Contains("SimulationFullBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 10;
        }

        if (normalized.Contains("ClientFullBuild", StringComparison.OrdinalIgnoreCase))
        {
            return 20;
        }

        return 100;
    }

    private static string BuildSimTemplateRedirectNote(SimTemplateOptionSummary preferredTemplate, SimAssetGraph simGraph)
    {
        var packageName = preferredTemplate.PackageName ?? Path.GetFileName(preferredTemplate.PackagePath);
        var usesIndexedDefaultRecipe = simGraph.BodyCandidates.Any(static candidate => candidate.SourceKind == SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe);
        if (usesIndexedDefaultRecipe)
        {
            return $"Automatically selected SimInfo template for indexed default/naked body recipe: {preferredTemplate.DisplayName} from {packageName}.";
        }

        var hasResolvedExplicitBodyShell =
            simGraph.BodyAssembly.Mode != SimBodyAssemblyMode.None &&
            simGraph.BodySources.Any(static source => source.Label == "Body-driving outfit records" && source.Count > 0);
        if (hasResolvedExplicitBodyShell)
        {
            return $"Automatically selected body-driving SimInfo template: {preferredTemplate.DisplayName} from {packageName}.";
        }

        return $"Automatically selected SimInfo template for body-shell inspection: {preferredTemplate.DisplayName} from {packageName}.";
    }

    private static IOrderedEnumerable<SimTemplateOptionSummary> OrderTemplatePackageVariants(
        IEnumerable<SimTemplateOptionSummary> options) =>
        options
            .OrderBy(static option => GetSimTemplatePackagePreference(option.PackagePath))
            .ThenByDescending(static option => option.AuthoritativeBodyDrivingOutfitCount ?? 0)
            .ThenByDescending(static option => option.AuthoritativeBodyDrivingOutfitPartCount ?? 0)
            .ThenByDescending(static option => option.OutfitPartCount)
            .ThenByDescending(static option => option.OutfitEntryCount)
            .ThenByDescending(static option => option.OutfitCategoryCount)
            .ThenByDescending(static option => option.BodyModifierCount + option.SculptCount)
            .ThenByDescending(static option => option.FaceModifierCount)
            .ThenByDescending(static option => option.HasSkintone)
            .ThenByDescending(static option => option.IsRepresentative)
            .ThenBy(static option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static option => option.PackagePath, StringComparer.OrdinalIgnoreCase);

    private static SimTemplateOptionSummary? SelectPreferredTemplatePackageVariant(
        IEnumerable<SimTemplateOptionSummary> options) =>
        OrderTemplatePackageVariants(options).FirstOrDefault();

    private static bool IsTemplatePackageVariantPreferredOver(
        SimTemplateOptionSummary candidate,
        SimTemplateOptionSummary? baseline)
    {
        if (baseline is null)
        {
            return true;
        }

        return OrderTemplatePackageVariants([candidate, baseline]).First().ResourceId == candidate.ResourceId;
    }

    private static IReadOnlyList<SimBodyFoundationSummary> BuildSimBodyFoundation(SimInfoSummary metadata)
    {
        var groups = new List<SimBodyFoundationSummary>
        {
            new(
                "Base frame",
                1,
                $"{metadata.SpeciesLabel} | {metadata.AgeLabel} | {metadata.GenderLabel}")
        };

        if (!string.IsNullOrWhiteSpace(metadata.SkintoneInstanceHex) || metadata.SkintoneShift.HasValue)
        {
            var skintoneNotes = new List<string>();
            if (!string.IsNullOrWhiteSpace(metadata.SkintoneInstanceHex))
            {
                skintoneNotes.Add($"instance {metadata.SkintoneInstanceHex}");
            }

            if (metadata.SkintoneShift.HasValue)
            {
                skintoneNotes.Add($"shift {metadata.SkintoneShift.Value:0.###}");
            }

            groups.Add(new SimBodyFoundationSummary(
                "Skin pipeline",
                1,
                string.Join(" | ", skintoneNotes)));
        }

        var layerCount = metadata.GeneticPartCount + metadata.GrowthPartCount + metadata.PeltLayerCount;
        if (layerCount > 0)
        {
            groups.Add(new SimBodyFoundationSummary(
                "Body layers",
                layerCount,
                $"{metadata.GeneticPartCount} genetic, {metadata.GrowthPartCount} growth, {metadata.PeltLayerCount} pelt/fur"));
        }

        var bodyMorphCount = metadata.BodyModifierCount + metadata.GeneticBodyModifierCount + metadata.SculptCount + metadata.GeneticSculptCount;
        if (bodyMorphCount > 0)
        {
            groups.Add(new SimBodyFoundationSummary(
                "Body morph stack",
                bodyMorphCount,
                $"{metadata.BodyModifierCount} direct body modifier(s), {metadata.GeneticBodyModifierCount} genetic body modifier(s), {metadata.SculptCount} sculpt(s), {metadata.GeneticSculptCount} genetic sculpt(s)"));
        }

        var faceMorphCount = metadata.FaceModifierCount + metadata.GeneticFaceModifierCount;
        if (faceMorphCount > 0)
        {
            groups.Add(new SimBodyFoundationSummary(
                "Face / head morph stack",
                faceMorphCount,
                $"{metadata.FaceModifierCount} direct face modifier(s), {metadata.GeneticFaceModifierCount} genetic face modifier(s)"));
        }

        if (metadata.OutfitCategoryCount > 0 || metadata.OutfitEntryCount > 0 || metadata.OutfitPartCount > 0)
        {
            groups.Add(new SimBodyFoundationSummary(
                "Current body-part references",
                metadata.OutfitPartCount,
                $"{metadata.OutfitCategoryCount} category, {metadata.OutfitEntryCount} outfit, {metadata.OutfitPartCount} part reference(s) currently point into the body/outfit graph"));
        }

        return groups;
    }

    private static IReadOnlyList<SimBodySourceSummary> BuildSimBodySources(Ts4SimInfo? metadata)
    {
        if (metadata is null)
        {
            return [];
        }

        var groups = new List<SimBodySourceSummary>();
        var bodyDrivingOutfits = GetAuthoritativeBodyDrivingOutfits(metadata);

        groups.Add(new SimBodySourceSummary(
            "Body-driving outfit records",
            bodyDrivingOutfits.Count,
            bodyDrivingOutfits.Count > 0
                ? string.Join(", ", bodyDrivingOutfits.Take(3).Select(static outfit => $"{outfit.CategoryLabel} ({outfit.Parts.Count} parts)"))
                : "No authoritative nude/body-driving outfit record is currently present in this SimInfo template."));

        if (!string.IsNullOrWhiteSpace(metadata.ToSummary().SkintoneInstanceHex))
        {
            groups.Add(new SimBodySourceSummary(
                "Skintone reference",
                1,
                metadata.SkintoneShift.HasValue
                    ? $"{metadata.SkintoneInstance:X16} | shift {metadata.SkintoneShift.Value:0.###}"
                    : $"{metadata.SkintoneInstance:X16}"));
        }

        if (metadata.OutfitParts.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Body-part instances",
                metadata.OutfitParts.Count,
                FormatOutfitPartExamples(metadata.OutfitParts, 4)));
        }

        if (metadata.GeneticPartBodyTypes.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Genetic body-type tokens",
                metadata.GeneticPartBodyTypes.Count,
                FormatBodyTypeList(metadata.GeneticPartBodyTypes, 6)));
        }

        if (metadata.GrowthPartBodyTypes.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Growth body-type tokens",
                metadata.GrowthPartBodyTypes.Count,
                FormatBodyTypeList(metadata.GrowthPartBodyTypes, 6)));
        }

        if (metadata.PeltLayers.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Pelt layer references",
                metadata.PeltLayers.Count,
                string.Join(", ", metadata.PeltLayers.Take(3).Select(static layer => $"{layer.Instance:X16}/0x{layer.Variant:X8}"))));
        }

        if (metadata.BodyModifiers.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Direct body channels",
                metadata.BodyModifiers.Count,
                FormatModifierEntries(metadata.BodyModifiers, 4)));
        }

        if (metadata.FaceModifiers.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Direct face channels",
                metadata.FaceModifiers.Count,
                FormatModifierEntries(metadata.FaceModifiers, 4)));
        }

        if (metadata.GeneticBodyModifiers.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Genetic body channels",
                metadata.GeneticBodyModifiers.Count,
                FormatModifierEntries(metadata.GeneticBodyModifiers, 4)));
        }

        if (metadata.GeneticFaceModifiers.Count > 0)
        {
            groups.Add(new SimBodySourceSummary(
                "Genetic face channels",
                metadata.GeneticFaceModifiers.Count,
                FormatModifierEntries(metadata.GeneticFaceModifiers, 4)));
        }

        return groups;
    }

    private async Task<SimSkintoneRenderSummary?> TryResolveSimSkintoneRenderSummaryAsync(
        SimInfoSummary metadata,
        string? preferredPackagePath,
        CancellationToken cancellationToken)
    {
        if (indexStore is null ||
            string.IsNullOrWhiteSpace(metadata.SkintoneInstanceHex))
        {
            return null;
        }

        var skintoneInstance = Convert.ToUInt64(metadata.SkintoneInstanceHex, 16);
        var skintoneMatches = await indexStore
            .GetResourcesByFullInstanceAsync(skintoneInstance, cancellationToken)
            .ConfigureAwait(false);
        var skintoneResource = SelectPreferredSimResourceMatch(
            skintoneMatches.Where(resource =>
                string.Equals(resource.Key.TypeName, "Skintone", StringComparison.OrdinalIgnoreCase) &&
                resource.Key.FullInstance == skintoneInstance),
            preferredPackagePath);
        if (skintoneResource is null)
        {
            return new SimSkintoneRenderSummary(
                metadata.SkintoneInstanceHex,
                metadata.SkintoneShift,
                null,
                null,
                null,
                null,
                0,
                0,
                null,
                $"Skintone instance {metadata.SkintoneInstanceHex} is present in SimInfo metadata, but no indexed Skintone resource was resolved.");
        }

        try
        {
            var skintoneBytes = await resourceCatalogService
                .GetResourceBytesAsync(skintoneResource.PackagePath, skintoneResource.Key, raw: false, cancellationToken)
                .ConfigureAwait(false);
            var skintone = Ts4StructuredResourceMetadataExtractor.ParseSkintone(skintoneBytes);
            var baseTextureResource = await TryResolveSkintoneBaseTextureResourceAsync(
                skintone.BaseTextureInstance,
                preferredPackagePath,
                cancellationToken).ConfigureAwait(false);
            var tintColor = TryBuildSkintoneViewportTintColor(skintone);
            var noteParts = new List<string>
            {
                $"Resolved skintone resource {skintoneResource.Key.FullTgi} from {Path.GetFileName(skintoneResource.PackagePath)}.",
                $"overlays {skintone.OverlayTextures.Count:N0}",
                $"swatches {skintone.SwatchColors.Count:N0}"
            };
            if (baseTextureResource is not null)
            {
                noteParts.Add($"base texture {baseTextureResource.Key.FullTgi}");
            }
            else if (skintone.BaseTextureInstance != 0)
            {
                noteParts.Add($"base texture instance 0x{skintone.BaseTextureInstance:X16} unresolved");
            }

            if (tintColor is not null)
            {
                noteParts.Add("viewport tint prepared from skintone swatch/colorize data");
            }

            if (metadata.SkintoneShift.HasValue)
            {
                noteParts.Add($"shift {metadata.SkintoneShift.Value:0.###}");
            }

            return new SimSkintoneRenderSummary(
                metadata.SkintoneInstanceHex,
                metadata.SkintoneShift,
                skintoneResource.Key.FullTgi,
                skintoneResource.PackagePath,
                baseTextureResource?.Key.FullTgi,
                baseTextureResource?.PackagePath,
                skintone.OverlayTextures.Count,
                skintone.SwatchColors.Count,
                tintColor,
                string.Join(" | ", noteParts));
        }
        catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException)
        {
            return new SimSkintoneRenderSummary(
                metadata.SkintoneInstanceHex,
                metadata.SkintoneShift,
                skintoneResource.Key.FullTgi,
                skintoneResource.PackagePath,
                null,
                null,
                0,
                0,
                null,
                $"Skintone resource {skintoneResource.Key.FullTgi} was resolved, but parsing failed: {ex.Message}");
        }
    }

    private async Task<ResourceMetadata?> TryResolveSkintoneBaseTextureResourceAsync(
        ulong baseTextureInstance,
        string? preferredPackagePath,
        CancellationToken cancellationToken)
    {
        if (indexStore is null || baseTextureInstance == 0)
        {
            return null;
        }

        var matches = await indexStore
            .GetResourcesByFullInstanceAsync(baseTextureInstance, cancellationToken)
            .ConfigureAwait(false);
        return SelectPreferredSimResourceMatch(
            matches.Where(resource =>
                resource.Key.FullInstance == baseTextureInstance &&
                IsTextureType(resource.Key.TypeName)),
            preferredPackagePath);
    }

    private static ResourceMetadata? SelectPreferredSimResourceMatch(
        IEnumerable<ResourceMetadata> resources,
        string? preferredPackagePath) =>
        resources
            .OrderBy(resource => GetBodyAssemblyPackagePreference(resource.PackagePath, preferredPackagePath))
            .ThenBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

    private static CanonicalColor? TryBuildSkintoneViewportTintColor(Ts4Skintone skintone)
    {
        if (skintone.SwatchColors.Count > 0)
        {
            return ToCanonicalColor(skintone.SwatchColors[0]);
        }

        return skintone.Colorize == 0 ? null : ToCanonicalColor(skintone.Colorize);
    }

    private static CanonicalColor ToCanonicalColor(uint argb)
    {
        var alpha = (byte)(argb >> 24);
        var red = (byte)(argb >> 16);
        var green = (byte)(argb >> 8);
        var blue = (byte)argb;
        return new CanonicalColor(
            red / 255f,
            green / 255f,
            blue / 255f,
            alpha == 0 ? 1f : alpha / 255f);
    }

    private static string FormatOutfitPartExamples(IReadOnlyList<Ts4SimOutfitPart> parts, int maxItems) =>
        string.Join(
            ", ",
            parts.Take(maxItems).Select(static part => $"bodyType {part.BodyType}: {part.PartInstance:X16}"));

    private static string FormatBodyTypeList(IReadOnlyList<uint> bodyTypes, int maxItems) =>
        string.Join(", ", bodyTypes.Take(maxItems).Select(static bodyType => $"bodyType {bodyType}"));

    private static string FormatModifierEntries(IReadOnlyList<Ts4SimModifierEntry> entries, int maxItems) =>
        string.Join(", ", entries.Take(maxItems).Select(static entry => $"ch {entry.ChannelId}={entry.Value:0.###}"));

    private static string BuildSimTemplateSearchText(SimInfoSummary metadata)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(metadata.SpeciesLabel) &&
            !string.Equals(metadata.SpeciesLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(metadata.SpeciesLabel);
        }

        if (!string.IsNullOrWhiteSpace(metadata.AgeLabel) &&
            !string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(metadata.AgeLabel);
        }

        if (!string.IsNullOrWhiteSpace(metadata.GenderLabel) &&
            !string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(metadata.GenderLabel);
        }

        return string.Join(" ", parts);
    }

    private static bool MatchesSimTemplateResource(ResourceMetadata resource, SimInfoSummary metadata)
    {
        var species = Ts4SimInfoParser.TryExtractSpeciesLabelFromSummary(resource.Description);
        var age = Ts4SimInfoParser.TryExtractAgeLabelFromSummary(resource.Description);
        var gender = Ts4SimInfoParser.TryExtractGenderLabelFromSummary(resource.Description);
        return string.Equals(species ?? "Unknown", metadata.SpeciesLabel ?? "Unknown", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(age ?? "Unknown", metadata.AgeLabel ?? "Unknown", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(gender ?? "Unknown", metadata.GenderLabel ?? "Unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static SimTemplateOptionSummary CreateTemplateOption(ResourceMetadata resource, bool isRepresentative)
    {
        var displayName = string.IsNullOrWhiteSpace(resource.Name)
            ? $"SimInfo template [{(uint)(resource.Key.FullInstance & 0xFFFFFFFF):X8}]"
            : resource.Name!;
        var notes = string.IsNullOrWhiteSpace(resource.Description)
            ? "SimInfo template metadata"
            : resource.Description!;
        var outfitCategoryCount = Ts4SimInfoParser.TryExtractOutfitCategoryCountFromSummary(resource.Description) ?? 0;
        var outfitEntryCount = Ts4SimInfoParser.TryExtractOutfitEntryCountFromSummary(resource.Description) ?? 0;
        var outfitPartCount = Ts4SimInfoParser.TryExtractOutfitPartCountFromSummary(resource.Description) ?? 0;
        var bodyModifierCount = Ts4SimInfoParser.TryExtractBodyModifierCountFromSummary(resource.Description) ?? 0;
        var faceModifierCount = Ts4SimInfoParser.TryExtractFaceModifierCountFromSummary(resource.Description) ?? 0;
        var sculptCount = Ts4SimInfoParser.TryExtractSculptCountFromSummary(resource.Description) ?? 0;
        var hasSkintone = !string.IsNullOrWhiteSpace(Ts4SimInfoParser.TryExtractSkintoneInstanceFromSummary(resource.Description));
        return new SimTemplateOptionSummary(
            resource.Id,
            displayName,
            resource.PackagePath,
            Path.GetFileName(resource.PackagePath),
            resource.Key.FullTgi,
            isRepresentative,
            notes,
            outfitCategoryCount,
            outfitEntryCount,
            outfitPartCount,
            bodyModifierCount,
            faceModifierCount,
            sculptCount,
            hasSkintone);
    }

    private async Task<IReadOnlyList<SimBodyCandidateSummary>> BuildSimBodyCandidatesAsync(
        ResourceMetadata simInfoResource,
        Ts4SimInfo? metadata,
        string? preferredPackagePath,
        bool includeCompatibilityFallbackCandidates,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        var summariesByLabel = new Dictionary<string, SimBodyCandidateSummary>(StringComparer.OrdinalIgnoreCase);
        var seenAssetIds = new HashSet<Guid>();
        var exactCandidatesByLabel = new Dictionary<string, List<AssetSummary>>(StringComparer.OrdinalIgnoreCase);
        var indexedBodyPartFacts = await TryLoadIndexedSimTemplateBodyPartFactsAsync(simInfoResource.Id, cancellationToken).ConfigureAwait(false);
        var authoritativeBodyDrivingParts = (indexedBodyPartFacts.Any(static fact => fact.IsBodyDriving)
                ? indexedBodyPartFacts
                    .Where(static fact => fact.IsBodyDriving)
                    .Select(static fact => (BodyType: fact.BodyType, PartInstance: fact.PartInstance))
                : metadata is null
                    ? []
                    : GetAuthoritativeBodyDrivingOutfits(metadata)
                        .SelectMany(static outfit => outfit.Parts)
                        .Select(static part => (BodyType: (int)part.BodyType, PartInstance: part.PartInstance)))
            .ToArray();
        var exactResourcesByInstance = await LoadExactSimBodyPartResourceMatchesAsync(
            authoritativeBodyDrivingParts.Select(static part => part.PartInstance),
            cancellationToken).ConfigureAwait(false);
        foreach (var part in authoritativeBodyDrivingParts
                     .GroupBy(static outfitPart => (outfitPart.BodyType, outfitPart.PartInstance))
                     .Select(static group => group.First()))
        {
            var label = MapCasBodyType(part.BodyType);
            if (!IsBodyAssemblySlotCategory(label))
            {
                continue;
            }

            var resolvedExactAssets = await CreateExactSimBodyPartAssetsAsync(
                exactResourcesByInstance.TryGetValue(part.PartInstance, out var exactResources) ? exactResources : [],
                label!,
                cancellationToken).ConfigureAwait(false);
            foreach (var asset in resolvedExactAssets.Where(asset => MatchesBodyAssemblySlotCategory(asset, label!)))
            {
                if (!exactCandidatesByLabel.TryGetValue(label!, out var exactGroup))
                {
                    exactGroup = [];
                    exactCandidatesByLabel.Add(label!, exactGroup);
                }

                if (seenAssetIds.Add(asset.Id))
                {
                    exactGroup.Add(asset);
                }
            }

            if (exactCandidatesByLabel.TryGetValue(label!, out var resolvedGroup) && resolvedGroup.Count > 0)
            {
                continue;
            }

            var query = new AssetBrowserQuery(
                new SourceScope(),
                $"{part.PartInstance:X16}",
                AssetBrowserDomain.Cas,
                string.Empty,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                16);
            var result = await indexStore.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var asset in result.Items.Where(asset =>
                         asset.RootKey.FullInstance == part.PartInstance &&
                         MatchesBodyAssemblySlotCategory(asset, label!)))
            {
                if (!exactCandidatesByLabel.TryGetValue(label!, out var exactGroup))
                {
                    exactGroup = [];
                    exactCandidatesByLabel.Add(label!, exactGroup);
                }

                if (!seenAssetIds.Add(asset.Id))
                {
                    continue;
                }

                exactGroup.Add(asset);
            }
        }

        if (metadata is null)
        {
            return summariesByLabel.Values
                .OrderBy(static summary => GetBodyAssemblySlotSortOrder(summary.Label))
                .ThenBy(static summary => summary.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var compatibilityMetadata = metadata.ToSummary();
        foreach (var pair in exactCandidatesByLabel
                     .OrderBy(static pair => GetBodyAssemblySlotSortOrder(pair.Key))
                     .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            summariesByLabel[pair.Key] = await CreateBodyCandidateSummaryAsync(
                pair.Key,
                pair.Value,
                "Resolved from authoritative SimInfo outfit/body-part instances",
                SimBodyCandidateSourceKind.ExactPartLink,
                compatibilityMetadata,
                preferredPackagePath,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var slotCategory in GetIndexedDefaultBodyRecipeSlotCategories()
                     .OrderBy(GetBodyAssemblySlotSortOrder)
                     .ThenBy(static label => label, StringComparer.OrdinalIgnoreCase))
        {
            if (summariesByLabel.TryGetValue(slotCategory, out var existingSummary) &&
                existingSummary.Count > 0)
            {
                continue;
            }

            var indexedDefaultPool = await QueryIndexedDefaultBodyRecipeCandidatePoolAsync(
                compatibilityMetadata,
                slotCategory,
                cancellationToken).ConfigureAwait(false);
            if (!indexedDefaultPool.Any())
            {
                continue;
            }

            var distinctCandidates = indexedDefaultPool
                .Where(asset => MatchesBodyAssemblySlotCategory(asset, slotCategory))
                .Where(asset => seenAssetIds.Add(asset.Id))
                .ToArray();
            if (distinctCandidates.Length == 0)
            {
                continue;
            }

            var indexedDefaultSummary = await CreateBodyCandidateSummaryAsync(
                slotCategory,
                distinctCandidates,
                "Resolved from indexed default/naked body-recipe CASParts matched by species/age/gender compatibility",
                SimBodyCandidateSourceKind.IndexedDefaultBodyRecipe,
                compatibilityMetadata,
                preferredPackagePath,
                cancellationToken).ConfigureAwait(false);
            if (indexedDefaultSummary is { Count: <= 0 })
            {
                continue;
            }

            summariesByLabel[slotCategory] = indexedDefaultSummary;
        }

        var hasCompatibilitySearch =
            authoritativeBodyDrivingParts.Length > 0 &&
            string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase);
        var includeCanonicalFoundationCandidates = includeCompatibilityFallbackCandidates;

        if (hasCompatibilitySearch && includeCanonicalFoundationCandidates)
        {
            foreach (var slotCategory in GetBodyAssemblySlotCategories().Where(SimBodyAssemblyPolicy.IsShellFamilyLabel))
            {
                if (summariesByLabel.TryGetValue(slotCategory, out var existingShellSummary) &&
                    existingShellSummary.Count > 0)
                {
                    continue;
                }

                var canonicalPool = await QueryCanonicalHumanFoundationCandidatePoolAsync(
                    compatibilityMetadata,
                    slotCategory,
                    cancellationToken).ConfigureAwait(false);
                if (canonicalPool.Count == 0)
                {
                    continue;
                }

                var distinctCandidates = canonicalPool
                    .Where(asset => MatchesBodyAssemblySlotCategory(asset, slotCategory))
                    .Where(asset => seenAssetIds.Add(asset.Id))
                    .ToArray();
                if (distinctCandidates.Length == 0)
                {
                    continue;
                }

                var canonicalSummary = await CreateBodyCandidateSummaryAsync(
                    slotCategory,
                    distinctCandidates,
                    "Resolved from canonical default human foundation CASParts",
                    SimBodyCandidateSourceKind.CanonicalFoundation,
                    compatibilityMetadata,
                    preferredPackagePath,
                    cancellationToken).ConfigureAwait(false);
                if (canonicalSummary.Count == 0)
                {
                    continue;
                }

                if (!summariesByLabel.TryGetValue(slotCategory, out var existingSummary) ||
                    existingSummary.Count == 0)
                {
                    summariesByLabel[slotCategory] = canonicalSummary;
                }
            }
        }

        if (includeCompatibilityFallbackCandidates && hasCompatibilitySearch)
        {
            var fallbackSources = BuildSimBodyFallbackSources(metadata);
            foreach (var entry in fallbackSources
                         .OrderBy(static pair => GetBodyAssemblySlotSortOrder(pair.Key))
                         .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (summariesByLabel.ContainsKey(entry.Key))
                {
                    continue;
                }

                var candidatePool = await QuerySimBodyCandidatePoolAsync(
                    compatibilityMetadata,
                    entry.Key,
                    cancellationToken).ConfigureAwait(false);
                if (candidatePool.Count == 0)
                {
                    continue;
                }

                var distinctCandidates = candidatePool
                    .Where(asset => MatchesBodyAssemblySlotCategory(asset, entry.Key))
                    .Where(asset => seenAssetIds.Add(asset.Id))
                    .ToArray();
                if (distinctCandidates.Length == 0)
                {
                    continue;
                }

                var sourceNotes = string.Join(", ", entry.Value.Take(3));
                if (entry.Value.Count > 3)
                {
                    sourceNotes += $", +{entry.Value.Count - 3:N0} more";
                }

                var notePrefix = $"Fallback compatible candidates inferred from {sourceNotes}";
                summariesByLabel[entry.Key] = await CreateBodyCandidateSummaryAsync(
                    entry.Key,
                    distinctCandidates,
                    notePrefix,
                    SimBodyCandidateSourceKind.BodyTypeFallback,
                    compatibilityMetadata,
                    preferredPackagePath,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (includeCompatibilityFallbackCandidates && hasCompatibilitySearch)
        {
            foreach (var slotCategory in GetBodyAssemblySlotCategories())
            {
                if (summariesByLabel.ContainsKey(slotCategory))
                {
                    continue;
                }

                var candidatePool = await QuerySimBodyCandidatePoolAsync(
                    compatibilityMetadata,
                    slotCategory,
                    cancellationToken).ConfigureAwait(false);
                if (candidatePool.Count == 0)
                {
                    continue;
                }

                var compatibleCandidates = candidatePool
                    .Where(asset => MatchesBodyAssemblySlotCategory(asset, slotCategory))
                    .Where(asset => seenAssetIds.Add(asset.Id))
                    .ToArray();
                if (compatibleCandidates.Length == 0)
                {
                    continue;
                }

                summariesByLabel[slotCategory] = await CreateBodyCandidateSummaryAsync(
                    slotCategory,
                    compatibleCandidates,
                    "Fallback compatible body candidates inferred directly from archetype species/age/gender compatibility",
                    SimBodyCandidateSourceKind.ArchetypeCompatibilityFallback,
                    compatibilityMetadata,
                    preferredPackagePath,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return summariesByLabel.Values
            .OrderBy(static summary => GetBodyAssemblySlotSortOrder(summary.Label))
            .ThenBy(static summary => summary.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<Ts4SimOutfitPart> GetAuthoritativeBodyDrivingOutfitParts(Ts4SimInfo metadata)
    {
        return GetAuthoritativeBodyDrivingOutfits(metadata)
            .SelectMany(static outfit => outfit.Parts)
            .ToArray();
    }

    private static IReadOnlyList<Ts4SimOutfit> GetAuthoritativeBodyDrivingOutfits(Ts4SimInfo metadata)
    {
        return metadata.Outfits
            .Where(static outfit => outfit.CategoryValue == 5 && outfit.Parts.Count > 0)
            .ToArray();
    }

    private static int GetAuthoritativeBodyDrivingOutfitCount(Ts4SimInfo metadata) =>
        GetAuthoritativeBodyDrivingOutfits(metadata).Count;

    private static int GetAuthoritativeBodyDrivingOutfitPartCount(Ts4SimInfo metadata) =>
        GetAuthoritativeBodyDrivingOutfitParts(metadata).Count;

    private async Task<IReadOnlyList<SimTemplateBodyPartFact>> TryLoadIndexedSimTemplateBodyPartFactsAsync(
        Guid simInfoResourceId,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        try
        {
            return await indexStore
                .GetSimTemplateBodyPartFactsAsync(simInfoResourceId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            return [];
        }
    }

    private async Task<IReadOnlyDictionary<ulong, IReadOnlyList<ResourceMetadata>>> LoadExactSimBodyPartResourceMatchesAsync(
        IEnumerable<ulong> partInstances,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return new Dictionary<ulong, IReadOnlyList<ResourceMetadata>>();
        }

        var distinctInstances = partInstances
            .Distinct()
            .ToArray();
        if (distinctInstances.Length == 0)
        {
            return new Dictionary<ulong, IReadOnlyList<ResourceMetadata>>();
        }

        try
        {
            var resources = await indexStore
                .GetCasPartResourcesByInstancesAsync(distinctInstances, cancellationToken)
                .ConfigureAwait(false);
            return resources
                .GroupBy(static resource => resource.Key.FullInstance)
                .ToDictionary(
                    static group => group.Key,
                    static group => (IReadOnlyList<ResourceMetadata>)group.ToArray());
        }
        catch (NotSupportedException)
        {
            return new Dictionary<ulong, IReadOnlyList<ResourceMetadata>>();
        }
    }

    private async Task<IReadOnlyList<AssetSummary>> ResolveExactSimBodyPartAssetsAsync(
        string label,
        ulong partInstance,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        IReadOnlyList<ResourceMetadata> exactResources;
        try
        {
            exactResources = await indexStore
                .GetCasPartResourcesByInstancesAsync([partInstance], cancellationToken)
                .ConfigureAwait(false);
        }
        catch (NotSupportedException)
        {
            exactResources = [];
        }

        if (exactResources.Count > 0)
        {
            return await CreateExactSimBodyPartAssetsAsync(
                exactResources.Where(resource =>
                        string.Equals(resource.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase) &&
                        resource.Key.FullInstance == partInstance)
                    .ToArray(),
                label,
                cancellationToken).ConfigureAwait(false);
        }

        var result = await indexStore.QueryResourcesAsync(
            new RawResourceBrowserQuery(
                new SourceScope(),
                string.Empty,
                RawResourceDomain.All,
                "CASPart",
                string.Empty,
                string.Empty,
                partInstance.ToString("X16"),
                false,
                false,
                false,
                ResourceLinkFilter.Any,
                RawResourceSort.Tgi,
                0,
                64),
            cancellationToken).ConfigureAwait(false);

        return await CreateExactSimBodyPartAssetsAsync(
            result.Items.Where(resource =>
                    string.Equals(resource.Key.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase) &&
                    resource.Key.FullInstance == partInstance)
                .ToArray(),
            label,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<AssetSummary>> CreateExactSimBodyPartAssetsAsync(
        IReadOnlyList<ResourceMetadata> casParts,
        string authoritativeLabel,
        CancellationToken cancellationToken)
    {
        var assets = new List<AssetSummary>(casParts.Count);
        foreach (var resource in casParts)
        {
            var asset = await CreateExactCasAssetSummaryAsync(resource, authoritativeLabel, cancellationToken).ConfigureAwait(false);
            if (asset is not null)
            {
                assets.Add(asset);
            }
        }

        return assets;
    }

    private async Task<AssetSummary?> CreateExactCasAssetSummaryAsync(
        ResourceMetadata casPart,
        string authoritativeLabel,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return null;
        }

        var related = await GetPackageInstanceResourcesAsync(casPart.PackagePath, casPart.Key.FullInstance, cancellationToken).ConfigureAwait(false);
        var thumbnail = related.FirstOrDefault(static resource => resource.Key.TypeName is "CASPartThumbnail" or "BodyPartThumbnail");
        var geometry = related.FirstOrDefault(static resource => resource.Key.TypeName == "Geometry");
        var materials = related.Where(static resource => resource.Key.TypeName == "MaterialDefinition").ToArray();
        var textures = related.Where(static resource => IsTextureType(resource.Key.TypeName)).ToArray();
        var diagnostics = new List<string>();
        if (geometry is null)
        {
            diagnostics.Add("No exact-instance Geometry resource was indexed for this CAS part.");
        }

        if (materials.Length == 0)
        {
            diagnostics.Add("No exact-instance MaterialDefinition resources were indexed for this CAS part.");
        }

        if (textures.Length == 0)
        {
            diagnostics.Add("No exact-instance texture resources were indexed for this CAS part.");
        }

        return new AssetSummary(
            StableEntityIds.ForAsset(casPart.DataSourceId, AssetKind.Cas, casPart.PackagePath, casPart.Key),
            casPart.DataSourceId,
            casPart.SourceKind,
            AssetKind.Cas,
            casPart.Name ?? $"{authoritativeLabel} {casPart.Key.FullInstance:X16}",
            authoritativeLabel,
            casPart.PackagePath,
            casPart.Key,
            thumbnail?.Key.FullTgi,
            1,
            Math.Max(0, related.Count - 1),
            string.Join(" ", diagnostics),
            new AssetCapabilitySnapshot(
                HasSceneRoot: geometry is not null,
                HasExactGeometryCandidate: geometry is not null,
                HasMaterialReferences: materials.Length > 0,
                HasTextureReferences: textures.Length > 0,
                HasThumbnail: thumbnail is not null,
                HasVariants: false,
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
            CategoryNormalized: NormalizeAssetCategory(authoritativeLabel),
            Description: casPart.Description);
    }

    private async Task<IReadOnlyList<AssetSummary>> QuerySimBodyCandidatePoolAsync(
        SimInfoSummary metadata,
        string slotCategory,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        var pool = new List<AssetSummary>();
        var seenIds = new HashSet<Guid>();
        foreach (var searchText in BuildSimBodyCompatibilitySearchTexts(metadata, slotCategory))
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                continue;
            }

            var query = new AssetBrowserQuery(
                new SourceScope(),
                searchText,
                AssetBrowserDomain.Cas,
                slotCategory,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                GetBodyAssemblyCandidateQueryWindowSize(slotCategory));
            var result = await indexStore.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
            foreach (var asset in result.Items)
            {
                if (seenIds.Add(asset.Id))
                {
                    pool.Add(asset);
                }
            }
        }

        return pool;
    }

    private async Task<IReadOnlyList<AssetSummary>> QueryCanonicalHumanFoundationCandidatePoolAsync(
        SimInfoSummary metadata,
        string slotCategory,
        CancellationToken cancellationToken)
    {
        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory))
        {
            return [];
        }

        var pageSize = GetBodyAssemblyCandidateQueryWindowSize(slotCategory);
        var maxSearchWindow = GetCanonicalFoundationCandidateMaxSearchWindow(slotCategory);
        var seenIds = new HashSet<Guid>();
        var preferredCandidates = new List<AssetSummary>();
        var heuristicFallbackCandidates = new List<AssetSummary>();

        foreach (var searchText in BuildCanonicalHumanFoundationSearchTexts(metadata))
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                continue;
            }

            for (var offset = 0; offset < maxSearchWindow; offset += pageSize)
            {
                var query = new AssetBrowserQuery(
                    new SourceScope(),
                    searchText,
                    AssetBrowserDomain.Cas,
                    slotCategory,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    offset,
                    pageSize);
                var result = await indexStore!.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
                var page = result.Items
                    .Where(asset => MatchesBodyAssemblySlotCategory(asset, slotCategory))
                    .Where(asset => seenIds.Add(asset.Id))
                    .ToArray();
                if (page.Length > 0)
                {
                    var factsByAssetId = await LoadBodyAssemblyCandidateFactsAsync(page, cancellationToken).ConfigureAwait(false);
                    foreach (var asset in page)
                    {
                        factsByAssetId.TryGetValue(asset.Id, out var facts);
                        if (facts is not null && IsPreferredDefaultBodyShellCandidate(slotCategory, facts, metadata))
                        {
                            preferredCandidates.Add(asset);
                            continue;
                        }

                        if (IsHeuristicCanonicalHumanFoundationCandidate(slotCategory, asset, facts, metadata))
                        {
                            heuristicFallbackCandidates.Add(asset);
                        }
                    }

                    if (preferredCandidates.Count > 0)
                    {
                        return preferredCandidates;
                    }
                }

                if (offset + pageSize >= result.TotalCount)
                {
                    break;
                }
            }
        }

        return heuristicFallbackCandidates;
    }

    private async Task<IReadOnlyList<AssetSummary>> QueryIndexedDefaultBodyRecipeCandidatePoolAsync(
        SimInfoSummary metadata,
        string slotCategory,
        CancellationToken cancellationToken)
    {
        if (indexStore is null ||
            string.IsNullOrWhiteSpace(slotCategory) ||
            string.IsNullOrWhiteSpace(metadata.SpeciesLabel) ||
            string.IsNullOrWhiteSpace(metadata.AgeLabel) ||
            string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var cacheKey = $"{metadata.SpeciesLabel}|{metadata.AgeLabel}|{metadata.GenderLabel}|{slotCategory}";
        var cachedTask = indexedDefaultBodyRecipeCandidateCache.GetOrAdd(
            cacheKey,
            static (_, state) => state.Builder.QueryIndexedDefaultBodyRecipeCandidatePoolCoreAsync(
                state.Metadata,
                state.SlotCategory,
                CancellationToken.None),
            (Builder: this, Metadata: metadata, SlotCategory: slotCategory));

        try
        {
            return await cachedTask.WaitAsync(cancellationToken);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            indexedDefaultBodyRecipeCandidateCache.TryRemove(
                new KeyValuePair<string, Task<IReadOnlyList<AssetSummary>>>(cacheKey, cachedTask));
            throw;
        }
    }

    private async Task<IReadOnlyList<AssetSummary>> QueryIndexedDefaultBodyRecipeCandidatePoolCoreAsync(
        SimInfoSummary metadata,
        string slotCategory,
        CancellationToken cancellationToken)
    {
        return await indexStore!
            .GetIndexedDefaultBodyRecipeAssetsAsync(metadata, slotCategory, cancellationToken)
            .ConfigureAwait(false);
    }

    private static IReadOnlyList<string> BuildIndexedDefaultBodyRecipeSearchTexts(SimInfoSummary metadata, string slotCategory)
    {
        var searches = new List<string>();

        void AddSearch(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                searches.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            searches.Add(value);
        }

        var exactCompatibility = BuildSimCasCompatibilitySearchText(metadata);
        var relaxedCompatibility = BuildSimCasCompatibilitySearchText(metadata, includeGender: false);

        if (SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory))
        {
            AddSearch($"{exactCompatibility} defaultBodyType true");
            AddSearch($"{exactCompatibility} nakedLink true");
            AddSearch($"{relaxedCompatibility} defaultBodyType true");
            AddSearch($"{relaxedCompatibility} nakedLink true");
            if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
            {
                AddSearch($"{exactCompatibility} defaultBodyTypeFemale true");
                AddSearch($"{relaxedCompatibility} defaultBodyTypeFemale true");
            }
            else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
            {
                AddSearch($"{exactCompatibility} defaultBodyTypeMale true");
                AddSearch($"{relaxedCompatibility} defaultBodyTypeMale true");
            }

            foreach (var prefix in BuildExpectedBodyShellPrefixes(metadata).Concat(BuildGenericHumanFoundationPrefixes(metadata)))
            {
                AddSearch($"{prefix} Nude");
                AddSearch($"{prefix} Default");
                AddSearch($"{prefix} Bare");
            }
        }
        else
        {
            AddSearch($"{exactCompatibility} Nude");
            AddSearch($"{exactCompatibility} Default");
            AddSearch($"{exactCompatibility} Bare");
            AddSearch($"{relaxedCompatibility} Nude");
            AddSearch($"{relaxedCompatibility} Default");
            AddSearch($"{relaxedCompatibility} Bare");

            foreach (var prefix in BuildExpectedBodyLayerPrefixes(metadata, slotCategory))
            {
                AddSearch($"{prefix} Nude");
                AddSearch($"{prefix} Default");
                AddSearch(prefix);
            }
        }

        return searches;
    }

    private static IReadOnlyList<string> GetIndexedDefaultBodyRecipeSlotCategories() =>
        ["Full Body", "Body", "Top", "Bottom", "Shoes"];

    private static IReadOnlyList<string> BuildCanonicalHumanFoundationSearchTexts(SimInfoSummary metadata)
    {
        var searches = new List<string>();
        foreach (var prefix in BuildExpectedBodyShellPrefixes(metadata).Concat(BuildGenericHumanFoundationPrefixes(metadata)))
        {
            foreach (var keyword in new[] { "Nude", "Default", "\"Base Body\"", "\"Default Body\"", "Bare" })
            {
                var targeted = $"{prefix} {keyword}";
                if (!searches.Contains(targeted, StringComparer.OrdinalIgnoreCase))
                {
                    searches.Add(targeted);
                }
            }

            if (!searches.Contains(prefix, StringComparer.OrdinalIgnoreCase))
            {
                searches.Add(prefix);
            }
        }

        var exactCompatibility = BuildSimCasCompatibilitySearchText(metadata);
        if (!string.IsNullOrWhiteSpace(exactCompatibility))
        {
            foreach (var keyword in new[] { "Nude", "Default" })
            {
                var targeted = $"{exactCompatibility} {keyword}";
                if (!searches.Contains(targeted, StringComparer.OrdinalIgnoreCase))
                {
                    searches.Add(targeted);
                }
            }
        }

        return searches;
    }

    private async Task<SimBodyCandidateSummary> CreateBodyCandidateSummaryAsync(
        string label,
        IReadOnlyList<AssetSummary> assets,
        string notePrefix,
        SimBodyCandidateSourceKind sourceKind,
        SimInfoSummary metadata,
        string? preferredPackagePath,
        CancellationToken cancellationToken)
    {
        var (filteredAssets, filteringNote, factsByAssetId) = await FilterPreferredBodyAssemblyAssetsAsync(
            label,
            assets,
            sourceKind,
            metadata,
            cancellationToken).ConfigureAwait(false);
        var collapsedAssets = CollapseSimBodyCandidateAssets(
            filteredAssets,
            label,
            factsByAssetId,
            metadata,
            preferredPackagePath);
        var candidates = collapsedAssets
            .OrderByDescending(asset => GetBodyAssemblyCandidatePriority(label, asset, factsByAssetId, metadata))
            .ThenBy(asset => GetBodyAssemblyPackagePreference(asset.PackagePath, preferredPackagePath))
            .ThenBy(static asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .Select(static asset => new SimCasSlotOptionSummary(
                asset.Id,
                asset.DisplayName,
                asset.PackagePath,
                asset.PackageName,
                asset.RootKey.FullTgi))
            .ToArray();
        var noteParts = new List<string> { notePrefix };
        if (!string.IsNullOrWhiteSpace(filteringNote))
        {
            noteParts.Add(filteringNote!);
        }

        if (collapsedAssets.Count < filteredAssets.Count)
        {
            noteParts.Add(
                $"collapsed {filteredAssets.Count - collapsedAssets.Count:N0} package duplicate(s) into {collapsedAssets.Count:N0} logical option(s)");
        }

        if (candidates.Length < collapsedAssets.Count)
        {
            noteParts.Add($"showing first {candidates.Length:N0} of {collapsedAssets.Count:N0}");
        }

        return new SimBodyCandidateSummary(label, collapsedAssets.Count, string.Join(" | ", noteParts), sourceKind, candidates);
    }

    private static IReadOnlyList<AssetSummary> CollapseSimBodyCandidateAssets(
        IReadOnlyList<AssetSummary> assets,
        string label,
        IReadOnlyDictionary<Guid, SimBodyAssemblyCandidateFacts> factsByAssetId,
        SimInfoSummary metadata,
        string? preferredPackagePath)
    {
        if (assets.Count <= 1)
        {
            return assets;
        }

        return assets
            .GroupBy(BuildSimBodyCandidateCollapseKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count() == 1
                ? group.First()
                : SelectPreferredSimBodyCandidateMember(group.ToArray(), label, factsByAssetId, metadata, preferredPackagePath))
            .ToArray();
    }

    private static string BuildSimBodyCandidateCollapseKey(AssetSummary asset)
    {
        if (string.Equals(asset.RootKey.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase))
        {
            return $"caspart:{asset.RootKey.FullInstance:X16}";
        }

        return string.IsNullOrWhiteSpace(asset.CanonicalRootTgi)
            ? asset.RootKey.FullTgi
            : asset.CanonicalRootTgi;
    }

    private static AssetSummary SelectPreferredSimBodyCandidateMember(
        IReadOnlyList<AssetSummary> members,
        string label,
        IReadOnlyDictionary<Guid, SimBodyAssemblyCandidateFacts> factsByAssetId,
        SimInfoSummary metadata,
        string? preferredPackagePath) =>
        members
            .OrderByDescending(static member => member.CapabilitySnapshot.HasSceneRoot)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasExactGeometryCandidate)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasRigReference)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasMaterialResourceCandidate)
            .ThenByDescending(static member => member.CapabilitySnapshot.HasTextureResourceCandidate)
            .ThenByDescending(static member => member.HasThumbnail)
            .ThenByDescending(member => GetBodyAssemblyCandidatePriority(label, member, factsByAssetId, metadata))
            .ThenBy(member => GetBodyAssemblyPackagePreference(member.PackagePath, preferredPackagePath))
            .ThenByDescending(static member => member.LinkedResourceCount)
            .ThenBy(static member => member.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static member => member.RootKey.FullTgi, StringComparer.OrdinalIgnoreCase)
            .First();

    private async Task<(IReadOnlyList<AssetSummary> Assets, string? FilteringNote, IReadOnlyDictionary<Guid, SimBodyAssemblyCandidateFacts> FactsByAssetId)> FilterPreferredBodyAssemblyAssetsAsync(
        string label,
        IReadOnlyList<AssetSummary> assets,
        SimBodyCandidateSourceKind sourceKind,
        SimInfoSummary metadata,
        CancellationToken cancellationToken)
    {
        string? filteringNote = null;
        var factsByAssetId = await LoadBodyAssemblyCandidateFactsAsync(assets, cancellationToken).ConfigureAwait(false);

        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(label))
        {
            return (assets, sourceKind == SimBodyCandidateSourceKind.ExactPartLink ? null : null, factsByAssetId);
        }

        var isExactShellSelection = sourceKind == SimBodyCandidateSourceKind.ExactPartLink;
        var isCanonicalFoundationSelection = sourceKind == SimBodyCandidateSourceKind.CanonicalFoundation;

        var defaultShells = assets
            .Where(asset => factsByAssetId.TryGetValue(asset.Id, out var facts) && IsPreferredDefaultBodyShellCandidate(label, facts, metadata))
            .ToArray();
        if (defaultShells.Length > 0)
        {
            filteringNote = isExactShellSelection
                ? "restricted to authoritative default/nude body shells so body-first preview does not treat outfit-only shells as the base body"
                : "restricted to CASPart default/nude body shells";
            return (defaultShells, filteringNote, factsByAssetId);
        }

        if (isExactShellSelection &&
            string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            filteringNote = "withheld authoritative non-default shell candidates until a real default/nude human body shell is found";
            return ([], filteringNote, factsByAssetId);
        }

        if (!isCanonicalFoundationSelection &&
            SimBodyAssemblyPolicy.IsShellFamilyLabel(label) &&
            string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            if (assets.Count > 0)
            {
                filteringNote = isExactShellSelection
                    ? "withheld authoritative clothing-like shell candidates until a real default human body shell is found"
                    : "withheld compatibility-only shell candidates until an authoritative default human body shell is found";
            }

            return ([], filteringNote, factsByAssetId);
        }

        var baseShells = assets
            .Where(asset =>
            {
                var facts = factsByAssetId.TryGetValue(asset.Id, out var candidateFacts) ? candidateFacts : null;
                return IsLikelyBaseBodyShell(label, asset, facts, metadata);
            })
            .ToArray();
        if (baseShells.Length > 0)
        {
            var genericBaseShells = baseShells
                .Where(asset =>
                {
                var facts = factsByAssetId.TryGetValue(asset.Id, out var candidateFacts) ? candidateFacts : null;
                return !ContainsOccultOrSpecialBodyKeyword(BuildCandidateSearchText(asset, facts));
            })
            .ToArray();
            if (genericBaseShells.Length > 0 && genericBaseShells.Length < baseShells.Length)
            {
                filteringNote = isExactShellSelection
                    ? "restricted to authoritative likely nude/base human body shells"
                    : "restricted to likely nude/base human body shells";
                return (genericBaseShells, filteringNote, factsByAssetId);
            }

            filteringNote = isExactShellSelection
                ? "restricted to authoritative likely nude/base body shells"
                : "restricted to likely nude/base body shells";
            return (baseShells, filteringNote, factsByAssetId);
        }

        var nonClothing = assets
            .Where(asset =>
            {
                var facts = factsByAssetId.TryGetValue(asset.Id, out var candidateFacts) ? candidateFacts : null;
                return !LooksLikeClothingLikeBodyCandidate(asset.DisplayName ?? string.Empty, asset.Description ?? string.Empty, facts?.InternalName);
            })
            .ToArray();
        if (nonClothing.Length > 0 && nonClothing.Length < assets.Count)
        {
            filteringNote = isExactShellSelection
                ? "filtered away clothing-like authoritative shell candidates"
                : "filtered away clothing-like shell candidates";
            return (nonClothing, filteringNote, factsByAssetId);
        }

        if (nonClothing.Length == assets.Count && nonClothing.Length > 0)
        {
            return (nonClothing, null, factsByAssetId);
        }

        if (assets.Count > 0)
        {
            filteringNote = isExactShellSelection
                ? "withheld authoritative clothing-like shell candidates until a real base-body shell is found"
                : "withheld clothing-like shell candidates until a real base-body shell is found";
            return ([], filteringNote, factsByAssetId);
        }

        return (assets, null, factsByAssetId);
    }

    private static bool MatchesIndexedDefaultBodyRecipeCandidate(
        string slotCategory,
        AssetSummary asset,
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        if (!MatchesBodyAssemblySlotCategory(asset, slotCategory) ||
            !IsCasPartCompatibleWithBodyRecipeSlot(facts, metadata, slotCategory))
        {
            return false;
        }

        return slotCategory switch
        {
            "Full Body" or "Body" =>
                IsPreferredDefaultBodyShellCandidate(slotCategory, facts, metadata) ||
                IsLikelyBaseBodyShell(slotCategory, asset, facts, metadata),
            "Top" => IsLikelyIndexedDefaultBodyRecipeLayer(slotCategory, asset, facts, metadata),
            "Bottom" => IsLikelyIndexedDefaultBodyRecipeLayer(slotCategory, asset, facts, metadata),
            "Shoes" => IsLikelyIndexedDefaultBodyRecipeLayer(slotCategory, asset, facts, metadata),
            _ => false
        };
    }

    private static bool MatchesBodyAssemblySlotCategory(AssetSummary asset, string slotCategory)
    {
        if (string.IsNullOrWhiteSpace(slotCategory))
        {
            return false;
        }

        return string.Equals(asset.Category, slotCategory, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(NormalizeAssetCategory(asset.Category), NormalizeAssetCategory(slotCategory), StringComparison.Ordinal);
    }

    private static int GetBodyAssemblyCandidatePriority(
        string label,
        AssetSummary asset,
        IReadOnlyDictionary<Guid, SimBodyAssemblyCandidateFacts> factsByAssetId,
        SimInfoSummary metadata)
    {
        var score = 0;
        var displayName = asset.DisplayName ?? string.Empty;
        var description = asset.Description ?? string.Empty;
        var packageName = asset.PackageName ?? Path.GetFileName(asset.PackagePath) ?? string.Empty;
        factsByAssetId.TryGetValue(asset.Id, out var facts);
        var combined = BuildCandidateSearchText(asset, facts);

        if (label is "Full Body" or "Body")
        {
            if (facts is not null && IsPreferredDefaultBodyShellCandidate(label, facts, metadata))
            {
                score += 700;
            }

            if (IsLikelyBaseBodyShell(label, asset, facts, metadata))
            {
                score += 400;
            }
            else if (LooksLikeClothingLikeBodyCandidate(displayName, description, facts?.InternalName))
            {
                score -= 300;
            }

            if (displayName.Contains("Body", StringComparison.OrdinalIgnoreCase))
            {
                score += 80;
            }

            if (displayName.Contains("Nude", StringComparison.OrdinalIgnoreCase) ||
                displayName.Contains("Default", StringComparison.OrdinalIgnoreCase))
            {
                score += 30;
            }

            if (facts is not null && facts.BodyType == 5)
            {
                score += 40;
            }
        }

        if (combined.Contains("Human", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (packageName.Contains("ClientFullBuild0", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        if (ContainsOccultOrSpecialBodyKeyword(combined))
        {
            score -= 200;
        }

        return score;
    }

    private static int GetBodyAssemblyPackagePreference(string packagePath, string? preferredPackagePath)
    {
        if (!string.IsNullOrWhiteSpace(preferredPackagePath) &&
            string.Equals(packagePath, preferredPackagePath, StringComparison.OrdinalIgnoreCase))
        {
            return -1000;
        }

        return GetSimTemplatePackagePreference(packagePath);
    }

    private static string BuildCandidateSearchText(AssetSummary asset, SimBodyAssemblyCandidateFacts? facts)
    {
        var displayName = asset.DisplayName ?? string.Empty;
        var description = asset.Description ?? string.Empty;
        var packageName = asset.PackageName ?? Path.GetFileName(asset.PackagePath) ?? string.Empty;
        return $"{displayName} {description} {facts?.InternalName} {packageName}";
    }

    private async Task<IReadOnlyDictionary<Guid, SimBodyAssemblyCandidateFacts>> LoadBodyAssemblyCandidateFactsAsync(
        IReadOnlyList<AssetSummary> assets,
        CancellationToken cancellationToken)
    {
        var factsByAssetId = new Dictionary<Guid, SimBodyAssemblyCandidateFacts>();
        foreach (var asset in assets)
        {
            if (!string.Equals(asset.RootKey.TypeName, "CASPart", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cacheKey = $"{asset.PackagePath}|{asset.RootKey.FullTgi}";
            if (bodyAssemblyCandidateFactsCache.TryGetValue(cacheKey, out var cachedFacts))
            {
                factsByAssetId[asset.Id] = cachedFacts;
                continue;
            }

            var indexedFacts = Ts4SeedMetadataExtractor.TryExtractCasPartSummaryMetadata(
                asset.Description,
                asset.DisplayName,
                asset.Category);
            if (indexedFacts is not null &&
                indexedFacts.BodyType is int indexedBodyType &&
                CanUseIndexedBodyAssemblyCandidateFacts(asset, asset.Description))
            {
                var facts = new SimBodyAssemblyCandidateFacts(
                    indexedBodyType,
                    indexedFacts.InternalName,
                    indexedFacts.DefaultForBodyType,
                    indexedFacts.DefaultForBodyTypeFemale,
                    indexedFacts.DefaultForBodyTypeMale,
                    indexedFacts.HasNakedLink,
                    indexedFacts.RestrictOppositeGender,
                    indexedFacts.RestrictOppositeFrame,
                    indexedFacts.SortLayer ?? 0,
                    indexedFacts.SpeciesLabel,
                    indexedFacts.AgeLabel,
                    indexedFacts.GenderLabel);
                bodyAssemblyCandidateFactsCache.TryAdd(cacheKey, facts);
                factsByAssetId[asset.Id] = facts;
                continue;
            }

            try
            {
                var bytes = await resourceCatalogService
                    .GetResourceBytesAsync(asset.PackagePath, asset.RootKey, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                var casPart = Ts4CasPart.Parse(bytes);
                var facts = new SimBodyAssemblyCandidateFacts(
                    casPart.BodyType,
                    casPart.InternalName,
                    casPart.DefaultForBodyType,
                    casPart.DefaultForBodyTypeFemale,
                    casPart.DefaultForBodyTypeMale,
                    casPart.HasNakedLink,
                    casPart.RestrictOppositeGender,
                    casPart.RestrictOppositeFrame,
                    casPart.SortLayer,
                    casPart.SpeciesLabel,
                    casPart.AgeLabel,
                    casPart.GenderLabel);
                bodyAssemblyCandidateFactsCache.TryAdd(cacheKey, facts);
                factsByAssetId[asset.Id] = facts;
            }
            catch (InvalidDataException)
            {
            }
            catch (EndOfStreamException)
            {
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (KeyNotFoundException)
            {
            }
        }

        return factsByAssetId;
    }

    private static bool CanUseIndexedBodyAssemblyCandidateFacts(AssetSummary asset, string? description)
    {
        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(asset.Category))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Contains("defaultBodyType=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("defaultBodyTypeFemale=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("defaultBodyTypeMale=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("nakedLink=", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreferredDefaultBodyShellCandidate(
        string label,
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(label) ||
            facts.BodyType != 5 ||
            !IsCasPartCompatibleWithSimMetadata(facts, metadata) ||
            !MatchesAllowedBodyShellPrefix(facts, metadata))
        {
            return false;
        }

        if (facts.HasNakedLink || facts.DefaultForBodyType)
        {
            return true;
        }

        return metadata.GenderLabel switch
        {
            "Female" => facts.DefaultForBodyTypeFemale,
            "Male" => facts.DefaultForBodyTypeMale,
            _ => false
        };
    }

    private static bool IsHeuristicCanonicalHumanFoundationCandidate(
        string label,
        AssetSummary asset,
        SimBodyAssemblyCandidateFacts? facts,
        SimInfoSummary metadata)
    {
        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(label) ||
            !string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (facts is not null)
        {
            if (facts.BodyType != 5 || !IsCasPartCompatibleWithSimMetadata(facts, metadata))
            {
                return false;
            }
        }

        var displayName = asset.DisplayName ?? string.Empty;
        var description = asset.Description ?? string.Empty;
        var internalName = facts?.InternalName;
        if (!LooksLikeBaseBodyShell(displayName, description, internalName) ||
            LooksLikeClothingLikeBodyCandidate(displayName, description, internalName))
        {
            return false;
        }

        if (ContainsOccultOrSpecialBodyKeyword(BuildCandidateSearchText(asset, facts)))
        {
            return false;
        }

        var prefixText = internalName ?? displayName;
        return MatchesGenericHumanFoundationPrefix(prefixText, metadata);
    }

    private static bool IsLikelyBaseBodyShell(
        string label,
        AssetSummary asset,
        SimBodyAssemblyCandidateFacts? facts,
        SimInfoSummary metadata)
    {
        if (!SimBodyAssemblyPolicy.IsShellFamilyLabel(label))
        {
            return false;
        }

        if (facts is null)
        {
            return false;
        }

        if (facts.BodyType != 5 ||
            !IsCasPartCompatibleWithSimMetadata(facts, metadata) ||
            !MatchesAllowedBodyShellPrefix(facts, metadata))
        {
            return false;
        }

        var internalName = facts?.InternalName;
        if (LooksLikeClothingLikeBodyCandidate(asset.DisplayName ?? string.Empty, asset.Description ?? string.Empty, internalName))
        {
            return false;
        }

        if (LooksLikeBaseBodyShell(asset.DisplayName ?? string.Empty, asset.Description ?? string.Empty, internalName))
        {
            return true;
        }

        if (facts is not null && IsPreferredDefaultBodyShellCandidate(label, facts, metadata))
        {
            return true;
        }

        return false;
    }

    private static bool MatchesAllowedBodyShellPrefix(
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        if (MatchesExpectedBodyShellPrefix(facts, metadata))
        {
            return true;
        }

        return MatchesGenericHumanFoundationPrefix(facts.InternalName, metadata);
    }

    private static bool IsCasPartCompatibleWithSimMetadata(
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        if (!string.IsNullOrWhiteSpace(facts.SpeciesLabel) &&
            !MatchesCompatibleBodyRecipeSpecies(facts.SpeciesLabel, metadata))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.AgeLabel) &&
            !string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(facts.AgeLabel) &&
            !string.Equals(facts.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !facts.AgeLabel.Contains(metadata.AgeLabel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(metadata.GenderLabel) &&
            !string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(metadata.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(facts.GenderLabel) &&
            !string.Equals(facts.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(facts.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase) &&
            !facts.GenderLabel.Contains(metadata.GenderLabel, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool MatchesCompatibleBodyRecipeSpecies(string factsSpeciesLabel, SimInfoSummary metadata)
    {
        if (string.IsNullOrWhiteSpace(factsSpeciesLabel))
        {
            return true;
        }

        if (string.Equals(factsSpeciesLabel, metadata.SpeciesLabel, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(metadata.SpeciesLabel, "Little Dog", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(factsSpeciesLabel, "Dog", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCasPartCompatibleWithBodyRecipeSlot(
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata,
        string slotCategory)
    {
        if (string.Equals(slotCategory, "Shoes", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(facts.SpeciesLabel) &&
                !string.Equals(facts.SpeciesLabel, metadata.SpeciesLabel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(metadata.AgeLabel) &&
                !string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(facts.AgeLabel) &&
                !string.Equals(facts.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
                !facts.AgeLabel.Contains(metadata.AgeLabel, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        return IsCasPartCompatibleWithSimMetadata(facts, metadata);
    }

    private static bool MatchesExpectedBodyShellPrefix(
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        var expectedPrefixes = BuildExpectedBodyShellPrefixes(metadata);
        if (expectedPrefixes.Count == 0 || string.IsNullOrWhiteSpace(facts.InternalName))
        {
            return true;
        }

        return expectedPrefixes.Any(prefix =>
            facts.InternalName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> BuildExpectedBodyLayerPrefixes(SimInfoSummary metadata, string slotCategory)
    {
        if (!TryBuildBodyShellPrefix(metadata, out var prefix))
        {
            return [];
        }

        var suffix = slotCategory switch
        {
            "Full Body" => "Body",
            "Body" => "Body",
            "Top" => "Top",
            "Bottom" => "Bottom",
            "Shoes" => "Shoes",
            _ => null
        };
        if (string.IsNullOrWhiteSpace(suffix))
        {
            return [];
        }

        return [$"{prefix}{suffix}_", $"{prefix}{suffix}"];
    }

    private static bool IsLikelyIndexedDefaultBodyRecipeLayer(
        string slotCategory,
        AssetSummary asset,
        SimBodyAssemblyCandidateFacts facts,
        SimInfoSummary metadata)
    {
        var expectedBodyType = slotCategory switch
        {
            "Top" => 6,
            "Bottom" => 7,
            "Shoes" => 8,
            _ => -1
        };
        if (facts.BodyType != expectedBodyType)
        {
            return false;
        }

        var displayName = asset.DisplayName ?? string.Empty;
        var description = asset.Description ?? string.Empty;
        var internalName = facts.InternalName;
        if (LooksLikeClothingLikeBodyCandidate(displayName, description, internalName) ||
            ContainsOccultOrSpecialBodyKeyword(BuildCandidateSearchText(asset, facts)))
        {
            return false;
        }

        if (facts.HasNakedLink ||
            facts.DefaultForBodyType ||
            facts.DefaultForBodyTypeFemale ||
            facts.DefaultForBodyTypeMale)
        {
            return true;
        }

        var combined = $"{displayName} {description} {internalName}";
        if (combined.Contains("Nude", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Default", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("Bare", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return BuildExpectedBodyLayerPrefixes(metadata, slotCategory).Any(prefix =>
            !string.IsNullOrWhiteSpace(internalName) &&
            internalName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeBaseBodyShell(string displayName, string description, string? internalName = null)
    {
        var combined = $"{displayName} {description} {internalName}";
        return combined.Contains("Nude", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Default Body", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Base Body", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("Bare", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("_Nude", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("yfBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("ymBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("afBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("amBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("iuBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("puBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("cuBody_", StringComparison.OrdinalIgnoreCase) ||
               combined.Contains("chBody_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeClothingLikeBodyCandidate(string displayName, string description, string? internalName = null)
    {
        var combined = $"{displayName} {description} {internalName}";
        foreach (var keyword in new[]
                 {
                     "dress",
                     "gown",
                     "doctor",
                     "detective",
                     "bartender",
                     "barista",
                     "costume",
                     "hoodie",
                     "coat",
                     "robe",
                     "onesie",
                     "uniform",
                     "swimsuit",
                     "nightie",
                     "trench",
                     "patient",
                     "blazer",
                     "skirt",
                     "jeans",
                     "shorts",
                     "pants",
                     "leggings",
                     "shirt",
                     "sweater",
                     "blouse",
                     "bodysuit",
                     "yfTop_",
                     "ymTop_",
                     "afTop_",
                     "amTop_",
                     "yfBottom_",
                     "ymBottom_",
                     "afBottom_",
                     "amBottom_"
                 })
        {
            if (combined.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsOccultOrSpecialBodyKeyword(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var keyword in new[]
                 {
                     "alien",
                     "mermaid",
                     "vampire",
                     "werewolf",
                     "servo",
                     "robot",
                     "skeleton",
                     "ghost",
                     "plantsim",
                     "occult"
                 })
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static SimBodyAssemblySummary BuildSimBodyAssembly(
        SimInfoSummary metadata,
        IReadOnlyList<SimBodyCandidateSummary> bodyCandidates,
        IReadOnlyList<SimCasSlotCandidateSummary> casSlotCandidates)
    {
        if (bodyCandidates.Count == 0)
        {
            return new SimBodyAssemblySummary(
                SimBodyAssemblyMode.None,
                "No compatible base-body candidates are currently resolved.",
                [],
                BuildSimBodyGraphNodes(metadata, SimBodyAssemblyMode.None, [], casSlotCandidates));
        }

        var activeLabels = SimBodyAssemblyPolicy.ResolveActiveLabels(
            bodyCandidates
                .Where(static candidate => candidate.Count > 0)
                .Select(static candidate => candidate.Label));
        var mode = SimBodyAssemblyPolicy.GetMode(activeLabels);
        var layers = bodyCandidates
            .OrderBy(static candidate => GetBodyAssemblySlotSortOrder(candidate.Label))
            .ThenBy(static candidate => candidate.Label, StringComparer.OrdinalIgnoreCase)
            .Select(candidate =>
            {
                var state = SimBodyAssemblyPolicy.GetLayerState(candidate.Label, activeLabels);
                return new SimBodyAssemblyLayerSummary(
                    candidate.Label,
                    candidate.Count,
                    BuildBodyAssemblyContributionLabel(candidate.Label),
                    candidate.SourceKind,
                    state,
                    BuildBodyAssemblyLayerStateNotes(candidate, state, mode),
                    candidate.Candidates);
            })
            .ToArray();

        return new SimBodyAssemblySummary(
            mode,
            BuildBodyAssemblySummary(mode, layers),
            layers,
            BuildSimBodyGraphNodes(metadata, mode, layers, casSlotCandidates));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSimBodyFallbackSources(Ts4SimInfo metadata)
    {
        var sources = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        void AddSource(string sourceKind, uint bodyType)
        {
            var label = MapCasBodyType((int)bodyType);
            if (!IsBodyAssemblySlotCategory(label))
            {
                return;
            }

            if (!sources.TryGetValue(label!, out var entries))
            {
                entries = [];
                sources.Add(label!, entries);
            }

            entries.Add($"{sourceKind} bodyType {bodyType}");
        }

        foreach (var part in GetAuthoritativeBodyDrivingOutfitParts(metadata))
        {
            AddSource("outfit", part.BodyType);
        }

        foreach (var bodyType in metadata.GeneticPartBodyTypes)
        {
            AddSource("genetic", bodyType);
        }

        foreach (var bodyType in metadata.GrowthPartBodyTypes)
        {
            AddSource("growth", bodyType);
        }

        return sources.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsBodyAssemblySlotCategory(string? label) =>
        label is not null && label switch
        {
            "Full Body" => true,
            "Body" => true,
            "Head" => true,
            "Top" => true,
            "Bottom" => true,
            "Shoes" => true,
            _ => false
        };

    private static int GetBodyAssemblySlotSortOrder(string? label) => label switch
    {
        "Full Body" => 0,
        "Body" => 1,
        "Head" => 2,
        "Top" => 3,
        "Bottom" => 4,
        "Shoes" => 5,
        _ => 10
    };

    private static IReadOnlyList<string> GetBodyAssemblySlotCategories() =>
        ["Full Body", "Body", "Top", "Bottom", "Shoes"];

    private static string BuildBodyAssemblyContributionLabel(string label) => label switch
    {
        "Full Body" => "Whole-body shell",
        "Body" => "Primary body shell",
        "Head" => "Head shell",
        "Top" => "Upper-body layer",
        "Bottom" => "Lower-body layer",
        "Shoes" => "Footwear layer",
        _ => "Body assembly layer"
    };

    private static string BuildBodyAssemblyLayerStateNotes(
        SimBodyCandidateSummary candidate,
        SimBodyAssemblyLayerState state,
        SimBodyAssemblyMode mode)
    {
        if ((string.Equals(candidate.Label, "Top", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(candidate.Label, "Bottom", StringComparison.OrdinalIgnoreCase)) &&
            state != SimBodyAssemblyLayerState.Active)
        {
            return $"Held back from the current body-first shell path until a real torso/body assembly exists. {candidate.Notes}";
        }

        if (string.Equals(candidate.Label, "Head", StringComparison.OrdinalIgnoreCase))
        {
            return candidate.Count == 0
                ? $"No dedicated head shell is currently resolved. {candidate.Notes}"
                : $"This head-shell candidate stays separate from the torso/body shell path and can be assembled on top of the current body foundation. {candidate.Notes}";
        }

        return state switch
        {
            SimBodyAssemblyLayerState.Active when mode == SimBodyAssemblyMode.BodyUnderlayWithSplitLayers &&
                                              SimBodyAssemblyPolicy.IsShellFamilyLabel(candidate.Label) =>
                $"Active as the current body underlay beneath split torso/body layers. {candidate.Notes}",
            SimBodyAssemblyLayerState.Active => candidate.Notes,
            SimBodyAssemblyLayerState.Blocked when mode == SimBodyAssemblyMode.FullBodyShell =>
                $"Currently suppressed by the active full-body shell. {candidate.Notes}",
            SimBodyAssemblyLayerState.Blocked when mode == SimBodyAssemblyMode.BodyShell =>
                $"Currently suppressed by the active primary body shell. {candidate.Notes}",
            _ => $"Available as an alternate body layer. {candidate.Notes}"
        };
    }

    private static string BuildBodyAssemblySummary(
        SimBodyAssemblyMode mode,
        IReadOnlyList<SimBodyAssemblyLayerSummary> layers)
    {
        return mode switch
        {
            SimBodyAssemblyMode.FullBodyShell =>
                $"A full-body CAS shell is currently the highest-priority base-body layer. Clothing and footwear overlays stay out of the body-first preview while {CountBlockedLayers(layers)} other lower-priority layer(s) remain suppressed.",
            SimBodyAssemblyMode.BodyShell =>
                $"A primary body shell is active. Clothing and footwear overlays stay out of the body-first preview while split top/bottom layers remain alternates.",
            SimBodyAssemblyMode.BodyUnderlayWithSplitLayers =>
                $"The current base-body recipe combines a shell underlay with {layers.Count(static layer => layer.State == SimBodyAssemblyLayerState.Active && !SimBodyAssemblyPolicy.IsShellFamilyLabel(layer.Label)):N0} active overlay layer(s).",
            SimBodyAssemblyMode.SplitBodyLayers =>
                $"The current base-body recipe is split across {layers.Count(static layer => layer.State == SimBodyAssemblyLayerState.Active):N0} active layer(s).",
            SimBodyAssemblyMode.FallbackSingleLayer =>
                $"A single fallback body layer is active because a fuller body-shell recipe is not resolved yet.",
            _ =>
                layers.Any(layer =>
                    string.Equals(layer.Label, "Top", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(layer.Label, "Bottom", StringComparison.OrdinalIgnoreCase))
                    ? "No renderable torso/body shell is currently resolved. Clothing-like split layers are intentionally held back until a real body shell path exists."
                    : "No base-body assembly recipe is currently resolved."
        };
    }

    private static int CountBlockedLayers(IReadOnlyList<SimBodyAssemblyLayerSummary> layers) =>
        layers.Count(static layer => layer.State == SimBodyAssemblyLayerState.Blocked);

    private static IReadOnlyList<SimBodyGraphNodeSummary> BuildSimBodyGraphNodes(
        SimInfoSummary metadata,
        SimBodyAssemblyMode mode,
        IReadOnlyList<SimBodyAssemblyLayerSummary> layers,
        IReadOnlyList<SimCasSlotCandidateSummary> casSlotCandidates)
    {
        var nodes = new List<SimBodyGraphNodeSummary>
        {
            new(
                "Base frame",
                0,
                SimBodyGraphNodeState.Resolved,
                $"{metadata.SpeciesLabel} | {metadata.AgeLabel} | {metadata.GenderLabel}")
        };

        nodes.Add(
            string.IsNullOrWhiteSpace(metadata.SkintoneInstanceHex) && !metadata.SkintoneShift.HasValue
                ? new SimBodyGraphNodeSummary(
                    "Skin pipeline",
                    1,
                    SimBodyGraphNodeState.Pending,
                    "No explicit skintone reference is surfaced for this template yet.")
                : new SimBodyGraphNodeSummary(
                    "Skin pipeline",
                    1,
                    SimBodyGraphNodeState.Resolved,
                    metadata.SkintoneShift.HasValue
                        ? $"skintone {metadata.SkintoneInstanceHex ?? "(unknown)"} | shift {metadata.SkintoneShift.Value:0.###}"
                        : $"skintone {metadata.SkintoneInstanceHex}"));

        var activeGeometryLayers = layers
            .Where(static layer =>
                layer.State == SimBodyAssemblyLayerState.Active &&
                !string.Equals(layer.Label, "Shoes", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var hasAuthoritativeGeometrySelection = activeGeometryLayers.Any(static layer =>
            layer.SourceKind == SimBodyCandidateSourceKind.ExactPartLink);
        nodes.Add(
            activeGeometryLayers.Length == 0
                ? new SimBodyGraphNodeSummary(
                    "Geometry shell",
                    2,
                    SimBodyGraphNodeState.Unavailable,
                    "No active body-shell layer is currently resolved.")
                : new SimBodyGraphNodeSummary(
                    "Geometry shell",
                    2,
                    mode switch
                    {
                        SimBodyAssemblyMode.SplitBodyLayers when hasAuthoritativeGeometrySelection => SimBodyGraphNodeState.Resolved,
                        SimBodyAssemblyMode.BodyUnderlayWithSplitLayers when hasAuthoritativeGeometrySelection => SimBodyGraphNodeState.Resolved,
                        _ => SimBodyGraphNodeState.Approximate
                    },
                    mode switch
                    {
                        SimBodyAssemblyMode.FullBodyShell when hasAuthoritativeGeometrySelection =>
                            "Full-body shell selected from authoritative SimInfo outfit/body-part selections.",
                        SimBodyAssemblyMode.FullBodyShell => "Full-body shell selected from compatible CAS body candidates.",
                        SimBodyAssemblyMode.BodyShell when hasAuthoritativeGeometrySelection =>
                            "Primary body shell selected from authoritative SimInfo outfit/body-part selections.",
                        SimBodyAssemblyMode.BodyShell => "Primary body shell selected from compatible CAS body candidates.",
                        SimBodyAssemblyMode.BodyUnderlayWithSplitLayers when hasAuthoritativeGeometrySelection =>
                            $"Layered body recipe composed from {activeGeometryLayers.Length:N0} active layer(s): shell underlay plus split overlays, including authoritative SimInfo selections.",
                        SimBodyAssemblyMode.BodyUnderlayWithSplitLayers =>
                            $"Layered body recipe composed from {activeGeometryLayers.Length:N0} active layer(s): shell underlay plus split overlays.",
                        SimBodyAssemblyMode.SplitBodyLayers when hasAuthoritativeGeometrySelection =>
                            $"Split body shell composed from {activeGeometryLayers.Length:N0} active layer(s), including authoritative SimInfo selections.",
                        SimBodyAssemblyMode.SplitBodyLayers => $"Split body shell composed from {activeGeometryLayers.Length:N0} active layer(s).",
                        _ => "Fallback body layer selected from compatible CAS candidates."
                    }));

        var headCasSelections = casSlotCandidates
            .Where(static candidate =>
                string.Equals(candidate.Label, "Hair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.Label, "Accessory", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var authoritativeHeadSelections = headCasSelections
            .Where(static candidate =>
                candidate.Count > 0 &&
                candidate.SourceKind == SimCasSlotCandidateSourceKind.ExactPartLink)
            .ToArray();
        nodes.Add(
            authoritativeHeadSelections.Length > 0
                ? new SimBodyGraphNodeSummary(
                    "Head CAS selections",
                    3,
                    SimBodyGraphNodeState.Resolved,
                    $"Authoritative head-related CAS selections resolved from SimInfo: {string.Join(", ", authoritativeHeadSelections.Select(static candidate => $"{candidate.Label} ({candidate.Count})"))}.")
                : headCasSelections.Any(static candidate => candidate.Count > 0)
                    ? new SimBodyGraphNodeSummary(
                        "Head CAS selections",
                        3,
                        SimBodyGraphNodeState.Approximate,
                        $"Head-related CAS selections are currently compatibility-derived: {string.Join(", ", headCasSelections.Where(static candidate => candidate.Count > 0).Select(static candidate => $"{candidate.Label} ({candidate.Count})"))}.")
                    : new SimBodyGraphNodeSummary(
                        "Head CAS selections",
                        3,
                        SimBodyGraphNodeState.Pending,
                        "No head-related CAS selections are surfaced for this template yet."));

        var headShellLayer = layers.FirstOrDefault(layer =>
            string.Equals(layer.Label, "Head", StringComparison.OrdinalIgnoreCase) &&
            layer.CandidateCount > 0);
        nodes.Add(
            headShellLayer is not null && headShellLayer.SourceKind == SimBodyCandidateSourceKind.ExactPartLink
                ? new SimBodyGraphNodeSummary(
                    "Head shell",
                    4,
                    SimBodyGraphNodeState.Resolved,
                    "Authoritative head shell candidate resolved from SimInfo outfit/body-part selections.")
                : headShellLayer is not null
                    ? new SimBodyGraphNodeSummary(
                        "Head shell",
                        4,
                        SimBodyGraphNodeState.Approximate,
                        "A head shell candidate is resolved, but it is not yet guaranteed by authoritative SimInfo head-part selection.")
                    : authoritativeHeadSelections.Length > 0
                        ? new SimBodyGraphNodeSummary(
                            "Head shell",
                            4,
                            SimBodyGraphNodeState.Approximate,
                            "Authoritative head-related CAS selections are resolved, but no dedicated head shell candidate is surfaced yet.")
                        : new SimBodyGraphNodeSummary(
                            "Head shell",
                            4,
                            SimBodyGraphNodeState.Pending,
                            "A dedicated head layer is not assembled yet; current body-first preview focuses on the torso/body shell path."));

        var activeFootwear = layers.FirstOrDefault(layer =>
            layer.State == SimBodyAssemblyLayerState.Active &&
            string.Equals(layer.Label, "Shoes", StringComparison.OrdinalIgnoreCase));
        nodes.Add(
            activeFootwear is null
                ? new SimBodyGraphNodeSummary(
                    "Footwear overlay",
                    5,
                    SimBodyGraphNodeState.Pending,
                    "No separate footwear layer is currently active in the base-body preview.")
                : new SimBodyGraphNodeSummary(
                    "Footwear overlay",
                    5,
                    SimBodyGraphNodeState.Approximate,
                    "A separate shoes layer is active on top of the current body shell."));

        var bodyMorphCount = metadata.BodyModifierCount + metadata.GeneticBodyModifierCount + metadata.SculptCount + metadata.GeneticSculptCount;
        nodes.Add(
            bodyMorphCount == 0
                ? new SimBodyGraphNodeSummary(
                    "Body morph application",
                    6,
                    SimBodyGraphNodeState.Unavailable,
                    "This template does not currently expose body-shape channels or sculpt payloads.")
                : new SimBodyGraphNodeSummary(
                    "Body morph application",
                    6,
                    SimBodyGraphNodeState.Pending,
                    $"{bodyMorphCount:N0} body-shape/sculpt input(s) are known but not applied to the rendered body yet."));

        var faceMorphCount = metadata.FaceModifierCount + metadata.GeneticFaceModifierCount;
        nodes.Add(
            faceMorphCount == 0
                ? new SimBodyGraphNodeSummary(
                    "Face morph application",
                    7,
                    SimBodyGraphNodeState.Unavailable,
                    "This template does not currently expose face-shape channels.")
                : new SimBodyGraphNodeSummary(
                    "Face morph application",
                    7,
                    SimBodyGraphNodeState.Pending,
                    $"{faceMorphCount:N0} face-shape input(s) are known but not applied to the rendered body yet."));

        return nodes;
    }

    private static IReadOnlyList<SimSlotGroupSummary> BuildSimSlotGroups(SimInfoSummary metadata)
    {
        var groups = new List<SimSlotGroupSummary>();

        if (metadata.OutfitCategoryCount > 0 || metadata.OutfitEntryCount > 0 || metadata.OutfitPartCount > 0)
        {
            groups.Add(new SimSlotGroupSummary(
                "Outfit / body part selections",
                metadata.OutfitPartCount,
                $"{metadata.OutfitCategoryCount} category, {metadata.OutfitEntryCount} outfit, {metadata.OutfitPartCount} part reference(s)"));
        }

        if (!string.IsNullOrWhiteSpace(metadata.SkintoneInstanceHex))
        {
            groups.Add(new SimSlotGroupSummary(
                "Skintone",
                1,
                $"Instance {metadata.SkintoneInstanceHex}"));
        }

        if (metadata.GeneticPartCount > 0)
        {
            groups.Add(new SimSlotGroupSummary(
                "Genetic body part layer",
                metadata.GeneticPartCount,
                "Genetic part references that shape the archetype base body"));
        }

        if (metadata.GrowthPartCount > 0)
        {
            groups.Add(new SimSlotGroupSummary(
                "Growth / progression parts",
                metadata.GrowthPartCount,
                "Additional part references tied to growth progression"));
        }

        if (metadata.PeltLayerCount > 0)
        {
            groups.Add(new SimSlotGroupSummary(
                "Pelt / fur layers",
                metadata.PeltLayerCount,
                "Layered coat or fur overlays"));
        }

        return groups;
    }

    private static IReadOnlyList<SimMorphGroupSummary> BuildSimMorphGroups(SimInfoSummary metadata)
    {
        var groups = new List<SimMorphGroupSummary>();

        if (metadata.FaceModifierCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Face modifiers",
                metadata.FaceModifierCount,
                "Direct face-shape morph channels"));
        }

        if (metadata.BodyModifierCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Body modifiers",
                metadata.BodyModifierCount,
                "Direct body-shape morph channels"));
        }

        if (metadata.SculptCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Sculpts",
                metadata.SculptCount,
                "Explicit sculpt entries applied to the archetype"));
        }

        if (metadata.GeneticFaceModifierCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Genetic face modifiers",
                metadata.GeneticFaceModifierCount,
                "Inherited face-shape channels"));
        }

        if (metadata.GeneticBodyModifierCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Genetic body modifiers",
                metadata.GeneticBodyModifierCount,
                "Inherited body-shape channels"));
        }

        if (metadata.GeneticSculptCount > 0)
        {
            groups.Add(new SimMorphGroupSummary(
                "Genetic sculpts",
                metadata.GeneticSculptCount,
                "Inherited sculpt references"));
        }

        return groups;
    }

    private async Task<IReadOnlyList<SimCasSlotCandidateSummary>> BuildSimCasSlotCandidatesAsync(
        Ts4SimInfo? simInfo,
        SimInfoSummary metadata,
        string? preferredPackagePath,
        CancellationToken cancellationToken)
    {
        if (indexStore is null ||
            !string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var summariesByLabel = new Dictionary<string, SimCasSlotCandidateSummary>(StringComparer.OrdinalIgnoreCase);
        var seenAssetIds = new HashSet<Guid>();
        if (simInfo is not null)
        {
            var exactCandidatesByLabel = new Dictionary<string, List<AssetSummary>>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in GetAuthoritativeBodyDrivingOutfitParts(simInfo)
                         .GroupBy(static outfitPart => (outfitPart.BodyType, outfitPart.PartInstance))
                         .Select(static group => group.First()))
            {
                var label = MapCasBodyType((int)part.BodyType);
                if (!IsHumanCasSlotCategory(label))
                {
                    continue;
                }

                var resolvedExactAssets = await ResolveExactSimBodyPartAssetsAsync(
                    label!,
                    part.PartInstance,
                    cancellationToken).ConfigureAwait(false);
                foreach (var asset in resolvedExactAssets.Where(asset => MatchesHumanCasSlotCategory(asset, label!)))
                {
                    if (!exactCandidatesByLabel.TryGetValue(label!, out var group))
                    {
                        group = [];
                        exactCandidatesByLabel.Add(label!, group);
                    }

                    if (seenAssetIds.Add(asset.Id))
                    {
                        group.Add(asset);
                    }
                }

                if (exactCandidatesByLabel.TryGetValue(label!, out var resolvedGroup) && resolvedGroup.Count > 0)
                {
                    continue;
                }

                var query = new AssetBrowserQuery(
                    new SourceScope(),
                    $"{part.PartInstance:X16}",
                    AssetBrowserDomain.Cas,
                    label!,
                    string.Empty,
                    string.Empty,
                    false,
                    false,
                    AssetBrowserSort.Name,
                    0,
                    16);
                var result = await indexStore.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
                foreach (var asset in result.Items.Where(asset =>
                         asset.RootKey.FullInstance == part.PartInstance &&
                         MatchesHumanCasSlotCategory(asset, label!)))
                {
                    if (!exactCandidatesByLabel.TryGetValue(label!, out var assetGroup))
                    {
                        assetGroup = [];
                        exactCandidatesByLabel.Add(label!, assetGroup);
                    }

                    if (seenAssetIds.Add(asset.Id))
                    {
                        assetGroup.Add(asset);
                    }
                }
            }

            foreach (var pair in exactCandidatesByLabel
                         .OrderBy(static pair => GetHumanCasSlotSortOrder(pair.Key))
                         .ThenBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                summariesByLabel[pair.Key] = CreateSimCasSlotCandidateSummary(
                    pair.Key,
                    pair.Value,
                    "Resolved from authoritative SimInfo outfit/body-part instances",
                    SimCasSlotCandidateSourceKind.ExactPartLink,
                    preferredPackagePath);
            }
        }

        var searchText = BuildSimCasCompatibilitySearchText(metadata);
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return summariesByLabel.Values
                .OrderBy(static summary => GetHumanCasSlotSortOrder(summary.Label))
                .ThenBy(static summary => summary.Label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        foreach (var slotCategory in GetHumanCasSlotCategories())
        {
            if (summariesByLabel.ContainsKey(slotCategory))
            {
                continue;
            }

            var query = new AssetBrowserQuery(
                new SourceScope(),
                searchText,
                AssetBrowserDomain.Cas,
                slotCategory,
                string.Empty,
                string.Empty,
                false,
                false,
                AssetBrowserSort.Name,
                0,
                6);
            var result = await indexStore.QueryAssetsAsync(query, cancellationToken).ConfigureAwait(false);
            if (result.TotalCount == 0)
            {
                continue;
            }

            var candidates = result.Items
                .Where(asset => seenAssetIds.Add(asset.Id))
                .ToArray();
            if (candidates.Length == 0)
            {
                continue;
            }

            var noteParts = new List<string>
            {
                $"compatible with {metadata.SpeciesLabel} | {metadata.AgeLabel} | {metadata.GenderLabel}"
            };
            if (result.TotalCount > candidates.Length)
            {
                noteParts.Add($"showing first {candidates.Length:N0} of {result.TotalCount:N0}");
            }

            summariesByLabel[slotCategory] = CreateSimCasSlotCandidateSummary(
                slotCategory,
                candidates,
                string.Join(" | ", noteParts),
                SimCasSlotCandidateSourceKind.CompatibilityFallback,
                preferredPackagePath,
                totalCountOverride: result.TotalCount);
        }

        return summariesByLabel.Values
            .OrderBy(static summary => GetHumanCasSlotSortOrder(summary.Label))
            .ThenBy(static summary => summary.Label, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetHumanCasSlotCategories() =>
        ["Hair", "Full Body", "Top", "Bottom", "Shoes", "Accessory"];

    private static bool IsHumanCasSlotCategory(string? label) =>
        label is not null && GetHumanCasSlotCategories().Contains(label, StringComparer.OrdinalIgnoreCase);

    private static bool MatchesHumanCasSlotCategory(AssetSummary asset, string slotCategory)
    {
        var normalizedCategory = NormalizeAssetCategory(asset.CategoryNormalized ?? asset.Category);
        return slotCategory switch
        {
            "Hair" => string.Equals(normalizedCategory, "hair", StringComparison.OrdinalIgnoreCase),
            "Accessory" => string.Equals(normalizedCategory, "accessory", StringComparison.OrdinalIgnoreCase),
            _ => MatchesBodyAssemblySlotCategory(asset, slotCategory)
        };
    }

    private static int GetHumanCasSlotSortOrder(string? label) => label switch
    {
        "Hair" => 0,
        "Full Body" => 1,
        "Top" => 2,
        "Bottom" => 3,
        "Shoes" => 4,
        "Accessory" => 5,
        _ => 10
    };

    private static SimCasSlotCandidateSummary CreateSimCasSlotCandidateSummary(
        string label,
        IReadOnlyList<AssetSummary> assets,
        string notePrefix,
        SimCasSlotCandidateSourceKind sourceKind,
        string? preferredPackagePath,
        int? totalCountOverride = null)
    {
        var orderedAssets = assets
            .OrderBy(asset => GetBodyAssemblyPackagePreference(asset.PackagePath, preferredPackagePath))
            .ThenBy(static asset => asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var candidates = orderedAssets
            .Take(6)
            .Select(static asset => new SimCasSlotOptionSummary(
                asset.Id,
                asset.DisplayName,
                asset.PackagePath,
                asset.PackageName,
                asset.RootKey.FullTgi))
            .ToArray();
        var totalCount = totalCountOverride ?? orderedAssets.Length;
        var noteParts = new List<string> { notePrefix };
        if (totalCount > candidates.Length)
        {
            noteParts.Add($"showing first {candidates.Length:N0} of {totalCount:N0}");
        }

        return new SimCasSlotCandidateSummary(
            label,
            totalCount,
            string.Join(" | ", noteParts),
            sourceKind,
            candidates);
    }

    private static IReadOnlyList<string> BuildSimBodyCompatibilitySearchTexts(SimInfoSummary metadata, string slotCategory)
    {
        var searches = new List<string>();
        var exact = BuildSimCasCompatibilitySearchText(metadata);
        if (!string.IsNullOrWhiteSpace(exact))
        {
            searches.Add(exact);
        }

        if (SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory) &&
            string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(metadata.AgeLabel) &&
            !string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(metadata.GenderLabel) &&
            !string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(metadata.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase))
        {
            var relaxed = BuildSimCasCompatibilitySearchText(metadata, includeGender: false);
            if (!string.IsNullOrWhiteSpace(relaxed) &&
                !searches.Contains(relaxed, StringComparer.OrdinalIgnoreCase))
            {
                searches.Add(relaxed);
            }

            foreach (var keyword in new[] { "Nude", "Default", "\"Base Body\"", "\"Default Body\"", "Bare" })
            {
                var targetedExact = $"{exact} {keyword}";
                if (!searches.Contains(targetedExact, StringComparer.OrdinalIgnoreCase))
                {
                    searches.Add(targetedExact);
                }

                if (!string.IsNullOrWhiteSpace(relaxed))
                {
                    var targetedRelaxed = $"{relaxed} {keyword}";
                    if (!searches.Contains(targetedRelaxed, StringComparer.OrdinalIgnoreCase))
                    {
                        searches.Add(targetedRelaxed);
                    }
                }
            }
        }

        if (SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory))
        {
            foreach (var prefix in BuildExpectedBodyShellPrefixes(metadata))
            {
                if (!searches.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                {
                    searches.Add(prefix);
                }

                foreach (var keyword in new[] { "Nude", "Default", "\"Base Body\"", "\"Default Body\"", "Bare" })
                {
                    var targetedPrefix = $"{prefix} {keyword}";
                    if (!searches.Contains(targetedPrefix, StringComparer.OrdinalIgnoreCase))
                    {
                        searches.Add(targetedPrefix);
                    }
                }
            }

            foreach (var prefix in BuildGenericHumanFoundationPrefixes(metadata))
            {
                if (!searches.Contains(prefix, StringComparer.OrdinalIgnoreCase))
                {
                    searches.Add(prefix);
                }

                foreach (var keyword in new[] { "Nude", "Default", "\"Base Body\"", "\"Default Body\"", "Bare" })
                {
                    var targetedPrefix = $"{prefix} {keyword}";
                    if (!searches.Contains(targetedPrefix, StringComparer.OrdinalIgnoreCase))
                    {
                        searches.Add(targetedPrefix);
                    }
                }
            }
        }

        return searches;
    }

    private static int GetBodyAssemblyCandidateQueryWindowSize(string slotCategory) =>
        SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory) ? 256 : 32;

    private static int GetCanonicalFoundationCandidateMaxSearchWindow(string slotCategory) =>
        SimBodyAssemblyPolicy.IsShellFamilyLabel(slotCategory) ? 2048 : GetBodyAssemblyCandidateQueryWindowSize(slotCategory);

    private static string BuildSimCasCompatibilitySearchText(SimInfoSummary metadata, bool includeGender = true)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(metadata.SpeciesLabel))
        {
            parts.Add(metadata.SpeciesLabel);
        }

        if (!string.IsNullOrWhiteSpace(metadata.AgeLabel) &&
            !string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(metadata.AgeLabel);
        }

        if (includeGender &&
            !string.IsNullOrWhiteSpace(metadata.GenderLabel) &&
            !string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(metadata.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase))
        {
            parts.Add(metadata.GenderLabel);
        }

        return string.Join(' ', parts);
    }

    private static IReadOnlyList<string> BuildExpectedBodyShellPrefixes(SimInfoSummary metadata)
    {
        if (TryBuildBodyShellPrefix(metadata, out var prefix))
        {
            return [$"{prefix}Body_", $"{prefix}Body"];
        }

        return [];
    }

    private static bool TryBuildBodyShellPrefix(SimInfoSummary metadata, out string prefix)
    {
        prefix = string.Empty;
        if (string.IsNullOrWhiteSpace(metadata.SpeciesLabel) ||
            string.IsNullOrWhiteSpace(metadata.AgeLabel) ||
            string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builder = new StringBuilder();
        if (string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('i');
        }
        else if (string.Equals(metadata.AgeLabel, "Toddler", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ? 'p' : 'c');
        }
        else if (string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('c');
        }
        else
        {
            builder.Append(string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ? 'y' : 'a');
        }

        if (!string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append(GetNonHumanBodyShellPrefix(metadata.SpeciesLabel, metadata.AgeLabel));
        }
        else if (string.Equals(metadata.AgeLabel, "Baby", StringComparison.OrdinalIgnoreCase) ||
                 string.IsNullOrWhiteSpace(metadata.GenderLabel) ||
                 string.Equals(metadata.GenderLabel, "Unknown", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(metadata.GenderLabel, "Unisex", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            builder.Append('u');
        }
        else
        {
            builder.Append(string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase) ? 'm' : 'f');
        }

        prefix = builder.ToString();
        return prefix.Length > 0;
    }

    private static bool MatchesGenericHumanFoundationPrefix(string? text, SimInfoSummary metadata)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return BuildGenericHumanFoundationPrefixes(metadata).Any(prefix =>
            text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> BuildGenericHumanFoundationPrefixes(SimInfoSummary metadata)
    {
        if (!string.Equals(metadata.SpeciesLabel, "Human", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(metadata.AgeLabel) ||
            string.Equals(metadata.AgeLabel, "Unknown", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(metadata.AgeLabel, "Baby", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (string.Equals(metadata.AgeLabel, "Infant", StringComparison.OrdinalIgnoreCase))
        {
            return ["iuBody_", "iuBody"];
        }

        if (string.Equals(metadata.AgeLabel, "Toddler", StringComparison.OrdinalIgnoreCase))
        {
            var prefixes = new List<string> { "puBody_", "puBody" };
            if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("pfBody_");
                prefixes.Add("pfBody");
            }
            else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("pmBody_");
                prefixes.Add("pmBody");
            }

            return prefixes;
        }

        if (string.Equals(metadata.AgeLabel, "Child", StringComparison.OrdinalIgnoreCase))
        {
            var prefixes = new List<string> { "cuBody_", "cuBody" };
            if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("cfBody_");
                prefixes.Add("cfBody");
            }
            else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
            {
                prefixes.Add("cmBody_");
                prefixes.Add("cmBody");
            }

            return prefixes;
        }

        var adultPrefixes = new List<string> { "acBody_", "acBody", "ahBody_", "ahBody" };
        if (string.Equals(metadata.GenderLabel, "Female", StringComparison.OrdinalIgnoreCase))
        {
            adultPrefixes.Add("afBody_");
            adultPrefixes.Add("afBody");
        }
        else if (string.Equals(metadata.GenderLabel, "Male", StringComparison.OrdinalIgnoreCase))
        {
            adultPrefixes.Add("amBody_");
            adultPrefixes.Add("amBody");
        }

        return adultPrefixes;
    }

    private static char GetNonHumanBodyShellPrefix(string speciesLabel, string ageLabel)
    {
        if (string.Equals(ageLabel, "Child", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(speciesLabel, "Little Dog", StringComparison.OrdinalIgnoreCase))
        {
            return 'd';
        }

        return speciesLabel switch
        {
            "Dog" => 'd',
            "Cat" => 'c',
            "Little Dog" => 'l',
            "Fox" => 'f',
            "Horse" => 'h',
            _ => 'a'
        };
    }

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
            BuildBuyGraph: new BuildBuyAssetGraph(
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
            General3DGraph: new General3DAssetGraph(
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
        var regionMapResolution = await ResolveCasRegionMapSummariesAsync(casPart, root, packageResources, diagnostics, cancellationToken).ConfigureAwait(false);
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

        if (!casPart.SupportsHumanSkinnedPreviewAge)
        {
            diagnostics.Add("This CAS part is outside the supported human skinned age subset.");
        }

        if (!casPart.IsMasculineOrFeminineHumanPresentation)
        {
            diagnostics.Add("This CAS part does not expose the expected human male/female age-gender flags for the supported subset.");
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

        var isSupported = casPart.SupportsHumanSkinnedPreviewAge
            && casPart.IsMasculineOrFeminineHumanPresentation
            && geometryResources.Count > 0;

        var linkedResources = identityResources
            .Concat(geometryResources)
            .Concat(rigResources)
            .Concat(materialResources)
            .Concat(regionMapResolution.Resources)
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
            CasGraph: new CasAssetGraph(
                root,
                geometryResources.FirstOrDefault(),
                rigResources.FirstOrDefault(),
                identityResources,
                geometryResources,
                rigResources,
                materialResources,
                explicitTextures,
                regionMapResolution.Summaries,
                materialManifest,
                category,
                casPart.SwatchSummary,
                selectedLodLabel,
                isSupported,
                "Human CAS parts in the supported skinned age subset when a Geometry LOD can be resolved directly or through indexed cross-package CAS references."));
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
            CasGraph: new CasAssetGraph(
                root,
                geometryResources.FirstOrDefault(),
                rigResources.FirstOrDefault(),
                identityResources,
                geometryResources,
                rigResources,
                materialResources,
                explicitTextures,
                [],
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

    private async Task<CasRegionMapResolution> ResolveCasRegionMapSummariesAsync(
        Ts4CasPart casPart,
        ResourceMetadata root,
        IReadOnlyList<ResourceMetadata> packageResources,
        List<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var resources = new List<ResourceMetadata>();
        var summaries = new List<CasRegionMapSummary>();
        foreach (var textureCandidate in casPart.TextureReferences.Where(static candidate =>
                     string.Equals(candidate.Slot, "region_map", StringComparison.OrdinalIgnoreCase)))
        {
            var resource = await ResolveCasGraphResourceAsync(root, packageResources, textureCandidate.Key, cancellationToken).ConfigureAwait(false);
            if (resource is null)
            {
                diagnostics.Add("CASPart region_map slot did not resolve to an indexed resource.");
                continue;
            }

            if (!string.Equals(resource.Key.TypeName, "RegionMap", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add($"CASPart region_map slot resolved to {resource.Key.TypeName} instead of RegionMap.");
                continue;
            }

            try
            {
                var bytes = await resourceCatalogService
                    .GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken)
                    .ConfigureAwait(false);
                var regionMap = Ts4StructuredResourceMetadataExtractor.ParseRegionMap(bytes);
                var regions = regionMap.Entries
                    .Select(static entry => entry.RegionLabel)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static label => label, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                resources.Add(resource);
                summaries.Add(new CasRegionMapSummary(
                    textureCandidate.Slot,
                    resource.Key.FullTgi,
                    resource.PackagePath,
                    regionMap.Entries.Count,
                    regionMap.Entries.Sum(static entry => entry.LinkedKeys.Count),
                    regionMap.Entries.Any(static entry => entry.IsReplacement),
                    regions,
                    regionMap.Entries.Count > 0
                        ? $"Resolved {regionMap.Entries.Count:N0} region-map entr{(regionMap.Entries.Count == 1 ? "y" : "ies")} across {string.Join(", ", regions.Take(6))}."
                        : "Resolved RegionMap resource without any region entries."));
                if (!string.Equals(resource.PackagePath, root.PackagePath, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"CASPart region_map resolved via indexed cross-package lookup from {Path.GetFileName(resource.PackagePath)}.");
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or EndOfStreamException or ArgumentOutOfRangeException)
            {
                diagnostics.Add($"CASPart region_map {resource.Key.FullTgi} could not be parsed: {ex.Message}");
            }
        }

        return new CasRegionMapResolution(
            resources
                .GroupBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            summaries
                .GroupBy(static summary => summary.ResourceTgi, StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray());
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
            var companions = await GetPackageInstanceResourcesAsync(geometry.PackagePath, geometry.Key.FullInstance, cancellationToken).ConfigureAwait(false);
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

        var matches = await GetResourcesByTgiCachedAsync(key.FullTgi, cancellationToken).ConfigureAwait(false);
        return matches
            .OrderBy(resource => ScoreCasCrossPackageCandidate(resource, root.DataSourceId))
            .ThenBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<ResourceMetadata>> GetPackageInstanceResourcesAsync(
        string packagePath,
        ulong fullInstance,
        CancellationToken cancellationToken)
    {
        if (indexStore is null)
        {
            return [];
        }

        var cachedTask = packageInstanceResourcesCache.GetOrAdd(
            (packagePath, fullInstance),
            static (key, store) => store.GetResourcesByInstanceAsync(key.PackagePath, key.FullInstance, CancellationToken.None),
            indexStore);

        try
        {
            return await cachedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            packageInstanceResourcesCache.TryRemove(
                new KeyValuePair<(string PackagePath, ulong FullInstance), Task<IReadOnlyList<ResourceMetadata>>>((packagePath, fullInstance), cachedTask));
            throw;
        }
    }

    private async Task<IReadOnlyList<ResourceMetadata>> GetResourcesByTgiCachedAsync(
        string fullTgi,
        CancellationToken cancellationToken)
    {
        if (indexStore is null || string.IsNullOrWhiteSpace(fullTgi))
        {
            return [];
        }

        var cachedTask = resourcesByTgiCache.GetOrAdd(
            fullTgi,
            static (key, store) => store.GetResourcesByTgiAsync(key, CancellationToken.None),
            indexStore);

        try
        {
            return await cachedTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (cachedTask.IsFaulted || cachedTask.IsCanceled)
        {
            resourcesByTgiCache.TryRemove(new KeyValuePair<string, Task<IReadOnlyList<ResourceMetadata>>>(fullTgi, cachedTask));
            throw;
        }
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
        3 => "Head",
        5 => "Full Body",
        6 => "Top",
        7 => "Bottom",
        8 => "Shoes",
        12 => "Accessory",
        _ => null
    };

    private static string NormalizeAssetCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        return category.Trim().ToLowerInvariant().Replace(' ', '-');
    }

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
            var slotCategory = MapCasPartSlotCategory(casPart.BodyType) ?? $"Body Type {casPart.BodyType}";
            return new Ts4CasPartSeedMetadata(
                casPart.InternalName,
                BuildCasPartSeedDescription(casPart),
                slotCategory,
                NormalizeCasPartCategory(slotCategory),
                BuildCasPartVariants(resource, casPart),
                BuildDiscoveredCasPartFact(resource, casPart, slotCategory));
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

    public static Ts4SimInfoSeedMetadata? TryExtractSimInfoSeedMetadata(ResourceMetadata resource, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            return Ts4SimInfoParser.BuildSeedMetadata(resource, bytes);
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

    public static Ts4SimTemplateSeedMetadata? TryExtractSimTemplateSeedMetadata(ResourceMetadata resource, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(bytes);

        try
        {
            var parsed = Ts4SimInfoParser.Parse(bytes);
            var seedMetadata = Ts4SimInfoParser.BuildSeedMetadata(resource, bytes);
            var authoritativeBodyDrivingOutfits = parsed.Outfits
                .Where(static outfit => outfit.CategoryValue == 5 && outfit.Parts.Count > 0)
                .ToArray();
            var fact = new SimTemplateFactSummary(
                resource.Id,
                resource.DataSourceId,
                resource.SourceKind,
                resource.PackagePath,
                resource.Key.FullTgi,
                Ts4SimInfoParser.BuildArchetypeLogicalKey(parsed.SpeciesLabel, parsed.AgeLabel, parsed.GenderLabel),
                parsed.SpeciesLabel,
                parsed.AgeLabel,
                parsed.GenderLabel,
                seedMetadata.DisplayName,
                seedMetadata.Description,
                parsed.OutfitCategoryCount,
                parsed.OutfitEntryCount,
                parsed.OutfitPartCount,
                parsed.BodyModifierCount,
                parsed.FaceModifierCount,
                parsed.SculptCount,
                parsed.SkintoneInstance != 0,
                authoritativeBodyDrivingOutfits.Length,
                authoritativeBodyDrivingOutfits.Sum(static outfit => outfit.Parts.Count));

            var bodyParts = new List<SimTemplateBodyPartFact>();
            foreach (var (outfit, outfitIndex) in parsed.Outfits.Select(static (outfit, index) => (outfit, index)))
            {
                if (outfit.Parts.Count == 0 ||
                    !authoritativeBodyDrivingOutfits.Contains(outfit))
                {
                    continue;
                }

                foreach (var (part, partIndex) in outfit.Parts.Select(static (part, index) => (part, index)))
                {
                    bodyParts.Add(new SimTemplateBodyPartFact(
                        resource.Id,
                        resource.DataSourceId,
                        resource.SourceKind,
                        resource.PackagePath,
                        resource.Key.FullTgi,
                        (int)outfit.CategoryValue,
                        outfit.CategoryLabel,
                        outfitIndex,
                        partIndex,
                        (int)part.BodyType,
                        MapSimTemplateBodyType((int)part.BodyType),
                        part.PartInstance,
                        part.BodyType is 2 or 3 or 5 or 6 or 7 or 8 or 12));
                }
            }

            return new Ts4SimTemplateSeedMetadata(fact, bodyParts);
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
            return new Ts4CasPartSeedMetadata(Ts4CasPart.TryReadInternalName(bytes), null, null, null, [], null);
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

    public static string? TryExtractCasPartSlotCategory(string? description) =>
        TryExtractCasPartSummaryField(description, "slot");

    public static bool? TryExtractCasPartSummaryBool(string? description, string key)
    {
        var value = TryExtractCasPartSummaryField(description, key);
        return bool.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    public static int? TryExtractCasPartSummaryInt(string? description, string key)
    {
        var value = TryExtractCasPartSummaryField(description, key);
        return int.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    public static Ts4CasPartSummaryMetadata? TryExtractCasPartSummaryMetadata(
        string? description,
        string? displayName,
        string? slotCategory)
    {
        if (!LooksLikeCasPartSeedDescription(description))
        {
            return null;
        }

        var resolvedSlotCategory = TryExtractCasPartSlotCategory(description) ?? slotCategory;
        var bodyType = TryExtractCasPartSummaryInt(description, "bodyType") ?? MapCasPartSlotCategoryToBodyType(resolvedSlotCategory);
        var internalName = TryExtractCasPartSummaryField(description, "internalName");
        if (string.IsNullOrWhiteSpace(internalName))
        {
            internalName = string.IsNullOrWhiteSpace(displayName) ? null : displayName;
        }

        if (bodyType is null &&
            string.IsNullOrWhiteSpace(internalName) &&
            string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return new Ts4CasPartSummaryMetadata(
            bodyType,
            internalName,
            TryExtractCasPartSummaryBool(description, "defaultBodyType") ?? false,
            TryExtractCasPartSummaryBool(description, "defaultBodyTypeFemale") ?? false,
            TryExtractCasPartSummaryBool(description, "defaultBodyTypeMale") ?? false,
            TryExtractCasPartSummaryBool(description, "nakedLink") ?? false,
            TryExtractCasPartSummaryBool(description, "restrictOppositeGender") ?? false,
            TryExtractCasPartSummaryBool(description, "restrictOppositeFrame") ?? false,
            TryExtractCasPartSummaryInt(description, "sortLayer"),
            TryExtractCasPartSummaryField(description, "species"),
            TryExtractCasPartSummaryField(description, "age") ?? "Unknown",
            TryExtractCasPartSummaryField(description, "gender") ?? "Unknown");
    }

    private static bool LooksLikeCasPartSeedDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        return description.Contains("slot=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("bodyType=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("species=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("age=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("gender=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("defaultBodyType=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("defaultBodyTypeFemale=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("defaultBodyTypeMale=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("nakedLink=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("restrictOppositeGender=", StringComparison.OrdinalIgnoreCase) ||
               description.Contains("restrictOppositeFrame=", StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryExtractCasPartSummaryField(string? description, string key)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        foreach (var part in description.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!part.StartsWith($"{key}=", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return part[(key.Length + 1)..].Trim();
        }

        return null;
    }

    private static string? MapSimTemplateBodyType(int bodyType) => bodyType switch
    {
        2 => "Hair",
        3 => "Head",
        5 => "Full Body",
        6 => "Top",
        7 => "Bottom",
        8 => "Shoes",
        12 => "Accessory",
        _ => null
    };

    private static string BuildCasPartSeedDescription(Ts4CasPart casPart)
    {
        var parts = new List<string>
        {
            $"slot={MapCasPartSlotCategory(casPart.BodyType) ?? $"Body Type {casPart.BodyType}"}",
            $"bodyType={casPart.BodyType}"
        };

        if (!string.IsNullOrWhiteSpace(casPart.InternalName))
        {
            parts.Add($"internalName={casPart.InternalName}");
        }

        if (!string.IsNullOrWhiteSpace(casPart.SpeciesLabel))
        {
            parts.Add($"species={casPart.SpeciesLabel}");
        }

        if (!string.IsNullOrWhiteSpace(casPart.AgeLabel))
        {
            parts.Add($"age={casPart.AgeLabel}");
        }

        if (!string.IsNullOrWhiteSpace(casPart.GenderLabel))
        {
            parts.Add($"gender={casPart.GenderLabel}");
        }

        if (casPart.SwatchColors.Count > 0)
        {
            parts.Add($"swatches={casPart.SwatchColors.Count}");
        }

        if (casPart.PresetCount > 0)
        {
            parts.Add($"presets={casPart.PresetCount}");
        }

        parts.Add($"defaultBodyType={casPart.DefaultForBodyType.ToString().ToLowerInvariant()}");
        parts.Add($"defaultBodyTypeFemale={casPart.DefaultForBodyTypeFemale.ToString().ToLowerInvariant()}");
        parts.Add($"defaultBodyTypeMale={casPart.DefaultForBodyTypeMale.ToString().ToLowerInvariant()}");
        parts.Add($"nakedLink={casPart.HasNakedLink.ToString().ToLowerInvariant()}");
        parts.Add($"restrictOppositeGender={casPart.RestrictOppositeGender.ToString().ToLowerInvariant()}");
        parts.Add($"restrictOppositeFrame={casPart.RestrictOppositeFrame.ToString().ToLowerInvariant()}");

        parts.Add($"sortLayer={casPart.SortLayer}");

        return string.Join(" | ", parts);
    }

    private static string? MapCasPartSlotCategory(int bodyType) => bodyType switch
    {
        2 => "Hair",
        3 => "Head",
        5 => "Full Body",
        6 => "Top",
        7 => "Bottom",
        8 => "Shoes",
        12 => "Accessory",
        _ => null
    };

    private static int? MapCasPartSlotCategoryToBodyType(string? slotCategory) => slotCategory switch
    {
        "Hair" => 2,
        "Head" => 3,
        "Full Body" => 5,
        "Top" => 6,
        "Bottom" => 7,
        "Shoes" => 8,
        "Accessory" => 12,
        _ => TryParseBodyTypeSlotCategory(slotCategory)
    };

    private static int? TryParseBodyTypeSlotCategory(string? slotCategory)
    {
        if (string.IsNullOrWhiteSpace(slotCategory) ||
            !slotCategory.StartsWith("Body Type ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return int.TryParse(slotCategory["Body Type ".Length..], out var parsed)
            ? parsed
            : null;
    }

    private static string NormalizeCasPartCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        return category.Trim().ToLowerInvariant().Replace(' ', '-');
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

    private static DiscoveredCasPartFact BuildDiscoveredCasPartFact(ResourceMetadata resource, Ts4CasPart casPart, string slotCategory)
    {
        var categoryNormalized = NormalizeCasPartCategory(slotCategory);
        return new DiscoveredCasPartFact(
            resource.DataSourceId,
            resource.SourceKind,
            resource.PackagePath,
            resource.Key.FullTgi,
            slotCategory,
            categoryNormalized,
            casPart.BodyType,
            casPart.InternalName,
            casPart.DefaultForBodyType,
            casPart.DefaultForBodyTypeFemale,
            casPart.DefaultForBodyTypeMale,
            casPart.HasNakedLink,
            casPart.RestrictOppositeGender,
            casPart.RestrictOppositeFrame,
            casPart.SortLayer,
            casPart.SpeciesLabel,
            casPart.AgeLabel,
            casPart.GenderLabel);
    }
}

public sealed record Ts4ObjectDefinitionSeedMetadata(string? TechnicalName, string? SceneRootTgiHint);

public sealed record Ts4CasPartSeedMetadata(
    string? TechnicalName,
    string? Description,
    string? SlotCategory,
    string? CategoryNormalized,
    IReadOnlyList<DiscoveredAssetVariant> Variants,
    DiscoveredCasPartFact? Fact);

public sealed record Ts4CasPartSummaryMetadata(
    int? BodyType,
    string? InternalName,
    bool DefaultForBodyType,
    bool DefaultForBodyTypeFemale,
    bool DefaultForBodyTypeMale,
    bool HasNakedLink,
    bool RestrictOppositeGender,
    bool RestrictOppositeFrame,
    int? SortLayer,
    string? SpeciesLabel,
    string AgeLabel,
    string GenderLabel);

public sealed record Ts4SimTemplateSeedMetadata(
    SimTemplateFactSummary Fact,
    IReadOnlyList<SimTemplateBodyPartFact> BodyPartFacts);

internal sealed record SimBodyAssemblyCandidateFacts(
    int BodyType,
    string? InternalName,
    bool DefaultForBodyType,
    bool DefaultForBodyTypeFemale,
    bool DefaultForBodyTypeMale,
    bool HasNakedLink,
    bool RestrictOppositeGender,
    bool RestrictOppositeFrame,
    int SortLayer,
    string? SpeciesLabel,
    string AgeLabel,
    string GenderLabel);

internal sealed record SimTemplateSelectionCandidate(
    SimTemplateOptionSummary Option,
    ResourceMetadata Resource,
    Ts4SimInfo Metadata);

internal sealed class Ts4CasPart
{
    private const uint MinimumSupportedVersion = 26;
    private const uint InfantFlag = 0x00000080;
    private const uint ToddlerFlag = 0x00000002;
    private const uint ChildFlag = 0x00000004;
    private const uint TeenFlag = 0x00000008;
    private const uint YoungAdultFlag = 0x00000010;
    private const uint AdultFlag = 0x00000020;
    private const uint ElderFlag = 0x00000040;
    private const uint MaleFlag = 0x00001000;
    private const uint FemaleFlag = 0x00002000;

    public required int BodyType { get; init; }
    public required uint AgeGenderFlags { get; init; }
    public required uint SpeciesValue { get; init; }
    public required string? InternalName { get; init; }
    public required uint PresetCount { get; init; }
    public required byte PartFlags1 { get; init; }
    public required byte PartFlags2 { get; init; }
    public required ulong OppositeGenderPart { get; init; }
    public required ulong FallbackPart { get; init; }
    public required byte NakedKey { get; init; }
    public required int SortLayer { get; init; }
    public required IReadOnlyList<ResourceKeyRecord> TgiList { get; init; }
    public required IReadOnlyList<Ts4CasLod> Lods { get; init; }
    public required IReadOnlyList<Ts4CasTextureCandidate> TextureReferences { get; init; }
    public required IReadOnlyList<uint> SwatchColors { get; init; }
    public required string? SwatchSummary { get; init; }
    public required string? VariantThumbnailTgi { get; init; }

    public bool SupportsHumanSkinnedPreviewAge => (AgeGenderFlags & (InfantFlag | ToddlerFlag | ChildFlag | TeenFlag | YoungAdultFlag | AdultFlag | ElderFlag)) != 0;
    public bool IsMasculineOrFeminineHumanPresentation => (AgeGenderFlags & (MaleFlag | FemaleFlag)) != 0;
    public string? SpeciesLabel => FormatSpecies(SpeciesValue);
    public string AgeLabel => FormatAge(AgeGenderFlags);
    public string GenderLabel => FormatGender(AgeGenderFlags);
    public bool DefaultForBodyType => (PartFlags1 & 0x01) != 0;
    public bool DefaultForBodyTypeMale => (PartFlags2 & 0x02) != 0;
    public bool DefaultForBodyTypeFemale => (PartFlags2 & 0x04) != 0;
    public bool RestrictOppositeGender => (PartFlags1 & 0x80) != 0;
    public bool RestrictOppositeFrame => (PartFlags2 & 0x01) != 0;
    public bool HasNakedLink => TryResolveTgi(TgiList, NakedKey) is not null;

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
        var partFlags1 = reader.ReadByte();
        var partFlags2 = (byte)0;
        if (header.Version >= 39)
        {
            partFlags2 = reader.ReadByte();
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
        var speciesValue = 1u;
        if (header.Version >= 32)
        {
            speciesValue = reader.ReadUInt32();
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

        var oppositeGenderPart = 0ul;
        if (header.Version >= 38)
        {
            oppositeGenderPart = reader.ReadUInt64();
        }

        var fallbackPart = 0ul;
        if (header.Version >= 39)
        {
            fallbackPart = reader.ReadUInt64();
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

        var nakedKey = reader.ReadByte();
        _ = reader.ReadByte();
        var sortLayer = reader.ReadInt32();
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
            SpeciesValue = speciesValue,
            InternalName = NormalizeName(internalName),
            PresetCount = header.PresetCount,
            PartFlags1 = partFlags1,
            PartFlags2 = partFlags2,
            OppositeGenderPart = oppositeGenderPart,
            FallbackPart = fallbackPart,
            NakedKey = nakedKey,
            SortLayer = sortLayer,
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

    private static string? FormatSpecies(uint value) => value switch
    {
        0 => "Human",
        1 => "Human",
        2 => "Dog",
        3 => "Cat",
        4 => "Little Dog",
        5 => "Fox",
        6 => "Horse",
        _ => null
    };

    private static string FormatAge(uint flags)
    {
        var labels = new List<string>();
        AppendFlagLabel(labels, flags, 0x00000001u, "Baby");
        AppendFlagLabel(labels, flags, 0x00000080u, "Infant");
        AppendFlagLabel(labels, flags, 0x00000002u, "Toddler");
        AppendFlagLabel(labels, flags, 0x00000004u, "Child");
        AppendFlagLabel(labels, flags, 0x00000008u, "Teen");
        AppendFlagLabel(labels, flags, 0x00000010u, "Young Adult");
        AppendFlagLabel(labels, flags, 0x00000020u, "Adult");
        AppendFlagLabel(labels, flags, 0x00000040u, "Elder");
        return labels.Count == 0 ? "Unknown" : string.Join(" / ", labels);
    }

    private static string FormatGender(uint flags)
    {
        var hasMale = (flags & MaleFlag) != 0;
        var hasFemale = (flags & FemaleFlag) != 0;
        return (hasMale, hasFemale) switch
        {
            (true, true) => "Unisex",
            (true, false) => "Male",
            (false, true) => "Female",
            _ => "Unknown"
        };
    }

    private static void AppendFlagLabel(List<string> labels, uint value, uint flag, string label)
    {
        if ((value & flag) != 0)
        {
            labels.Add(label);
        }
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

internal sealed record CasRegionMapResolution(
    IReadOnlyList<ResourceMetadata> Resources,
    IReadOnlyList<CasRegionMapSummary> Summaries);

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
