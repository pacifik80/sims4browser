using System.Buffers.Binary;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed partial class BuildBuySceneBuildService : ISceneBuildService
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore indexStore;

    public BuildBuySceneBuildService(IResourceCatalogService resourceCatalogService, IIndexStore indexStore)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.indexStore = indexStore;
    }

    public async Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (resource.Key.TypeName is not ("Model" or "ModelLOD" or "Geometry"))
        {
            return new SceneBuildResult(
                false,
                null,
                [$"Scene reconstruction is currently supported for Model, ModelLOD, and skinned Geometry roots only. Selected type: {resource.Key.TypeName}."],
                SceneBuildStatus.Unsupported);
        }

        try
        {
            return resource.Key.TypeName switch
            {
                "Model" => await BuildModelSceneAsync(resource, cancellationToken),
                "ModelLOD" => await BuildModelLodSceneAsync(resource, resource, cancellationToken),
                "Geometry" => await BuildGeometrySceneAsync(resource, resource, cancellationToken),
                _ => new SceneBuildResult(false, null, [$"Unsupported scene root {resource.Key.TypeName}."], SceneBuildStatus.Unsupported)
            };
        }
        catch (Exception ex)
        {
            return new SceneBuildResult(false, null, [$"Scene reconstruction failed for {resource.Key.FullTgi}: {ex.Message}"], SceneBuildStatus.Unsupported);
        }
    }

    private async Task<SceneBuildResult> BuildModelSceneAsync(ResourceMetadata modelResource, CancellationToken cancellationToken)
    {
        Ts4RcolResource root;
        Ts4ModlChunk modl;
        try
        {
            var resolvedModelResource = await ResolveSceneResourceMetadataAsync(modelResource, cancellationToken).ConfigureAwait(false);
            var bytes = await resourceCatalogService.GetResourceBytesAsync(resolvedModelResource.PackagePath, resolvedModelResource.Key, raw: false, cancellationToken);
            root = Ts4RcolResource.Parse(bytes);
            var modlChunk = root.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MODL");
            if (modlChunk is null)
            {
                return new SceneBuildResult(false, null, [$"Model {resolvedModelResource.Key.FullTgi} does not contain a MODL chunk."], SceneBuildStatus.Unsupported);
            }

            modl = Ts4ModlChunk.Parse(modlChunk.Data.Span);
            modelResource = resolvedModelResource;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            return await TryIndexedModelLodFallbackAsync(
                modelResource,
                [
                    $"Model {modelResource.Key.FullTgi} could not be parsed cleanly for the current Build/Buy scene path.",
                    "This usually means the object uses a MODL/MLOD variant outside the currently supported static subset.",
                    $"Parser detail: {ex.Message}"
                ],
                cancellationToken);
        }

        var lodEntries = modl.LodEntries
            .OrderBy(static lod => lod.IsShadow)
            .ThenBy(static lod => lod.Id)
            .ToArray();

        if (lodEntries.Length == 0)
        {
            return await TryIndexedModelLodFallbackAsync(
                modelResource,
                [$"Model {modelResource.Key.FullTgi} does not expose any LOD entries."],
                cancellationToken);
        }

        var diagnostics = new List<string>
        {
            $"Available LODs: {string.Join(", ", lodEntries.Select(static lod => lod.DisplayName))}"
        };

        var nonShadowLods = lodEntries.Where(static lod => !lod.IsShadow).ToArray();
        var shadowLods = lodEntries.Where(static lod => lod.IsShadow).ToArray();

        foreach (var lod in nonShadowLods)
        {
            try
            {
                var result = await TryResolveModelLodAsync(modelResource, root, lod.Reference, cancellationToken);
                diagnostics.AddRange(result.Diagnostics);
                if (result.Resource is not null)
                {
                    var sceneResult = await BuildModelLodSceneAsync(result.Resource, modelResource, cancellationToken);
                    if (sceneResult.Success)
                    {
                        return new SceneBuildResult(true, sceneResult.Scene, diagnostics.Concat(sceneResult.Diagnostics).ToArray(), sceneResult.Status);
                    }

                    diagnostics.AddRange(sceneResult.Diagnostics);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or KeyNotFoundException)
            {
                diagnostics.Add($"LOD {lod.DisplayName} could not be reconstructed: {ex.Message}");
            }
        }

        var indexedFallback = await TryIndexedModelLodFallbackAsync(modelResource, diagnostics, cancellationToken).ConfigureAwait(false);
        if (indexedFallback.Success)
        {
            return indexedFallback;
        }

        diagnostics = indexedFallback.Diagnostics.ToList();

        foreach (var lod in shadowLods)
        {
            try
            {
                var result = await TryResolveModelLodAsync(modelResource, root, lod.Reference, cancellationToken);
                diagnostics.AddRange(result.Diagnostics);
                if (result.Resource is not null)
                {
                    var sceneResult = await BuildModelLodSceneAsync(result.Resource, modelResource, cancellationToken);
                    if (sceneResult.Success)
                    {
                        return new SceneBuildResult(true, sceneResult.Scene, diagnostics.Concat(sceneResult.Diagnostics).ToArray(), sceneResult.Status);
                    }

                    diagnostics.AddRange(sceneResult.Diagnostics);
                }
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or KeyNotFoundException)
            {
                diagnostics.Add($"LOD {lod.DisplayName} could not be reconstructed: {ex.Message}");
            }
        }

        return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
    }

    private async Task<SceneBuildResult> TryIndexedModelLodFallbackAsync(
        ResourceMetadata modelResource,
        IEnumerable<string> existingDiagnostics,
        CancellationToken cancellationToken)
    {
        var diagnostics = existingDiagnostics
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        var packageResources = await indexStore.GetPackageResourcesAsync(modelResource.PackagePath, cancellationToken).ConfigureAwait(false);
        var indexedCandidates = packageResources
            .Where(resource => resource.Key.TypeName == "ModelLOD" && resource.Key.FullInstance == modelResource.Key.FullInstance)
            .OrderBy(static resource => resource.Key.Group)
            .ThenBy(static resource => resource.Key.FullTgi, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (indexedCandidates.Length == 0)
        {
            diagnostics.Add("No exact-instance indexed ModelLOD candidates were available for fallback.");
            return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
        }

        diagnostics.Add($"Falling back to {indexedCandidates.Length} exact-instance indexed ModelLOD candidate(s).");
        foreach (var candidate in indexedCandidates)
        {
            var candidateResult = await BuildModelLodSceneAsync(candidate, modelResource, cancellationToken).ConfigureAwait(false);
            if (candidateResult.Success)
            {
                return new SceneBuildResult(true, candidateResult.Scene, diagnostics.Concat(candidateResult.Diagnostics).ToArray(), candidateResult.Status);
            }

            diagnostics.AddRange(candidateResult.Diagnostics);
        }

        return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
    }

    private async Task<SceneBuildResult> BuildModelLodSceneAsync(
        ResourceMetadata modelLodResource,
        ResourceMetadata logicalRootResource,
        CancellationToken cancellationToken)
    {
        Ts4RcolResource rcol;
        Ts4MlodChunk mlod;
        try
        {
            var resolvedModelLodResource = await ResolveSceneResourceMetadataAsync(modelLodResource, cancellationToken).ConfigureAwait(false);
            var bytes = await resourceCatalogService.GetResourceBytesAsync(resolvedModelLodResource.PackagePath, resolvedModelLodResource.Key, raw: false, cancellationToken);
            rcol = Ts4RcolResource.Parse(bytes);
            var mlodChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MLOD");
            if (mlodChunk is null)
            {
                return new SceneBuildResult(false, null, [$"ModelLOD {resolvedModelLodResource.Key.FullTgi} does not contain an MLOD chunk."], SceneBuildStatus.Unsupported);
            }

            mlod = Ts4MlodChunk.Parse(mlodChunk.Data.Span);
            modelLodResource = resolvedModelLodResource;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
        {
            return new SceneBuildResult(false, null, [$"ModelLOD {modelLodResource.Key.FullTgi} could not be parsed cleanly: {ex.Message}"], SceneBuildStatus.Unsupported);
        }

        var diagnostics = new List<string>();
        var meshes = new List<CanonicalMesh>();
        var materials = new List<CanonicalMaterial>();
        var materialCache = new Dictionary<uint, int>();
        var textureCache = new Dictionary<string, CanonicalTexture?>(StringComparer.OrdinalIgnoreCase);

        foreach (var mesh in mlod.Meshes)
        {
            if (mesh.IsSkinned)
            {
                diagnostics.Add($"Mesh 0x{mesh.Name:X8} references skin/static-blend data; rendering it in bind-pose approximation for preview.");
            }

            if (mesh.PrimitiveType != Ts4PrimitiveType.TriangleList)
            {
                diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: primitive type {mesh.PrimitiveType} is not supported in this pass.");
                continue;
            }

            try
            {
                var vertexBufferChunk = rcol.ResolveChunk(mesh.VertexBufferReference);
                var indexBufferChunk = rcol.ResolveChunk(mesh.IndexBufferReference);
                if (vertexBufferChunk is null || indexBufferChunk is null)
                {
                    diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: vertex/index buffers could not be resolved.");
                    continue;
                }

                Ts4VrtfChunk vrtf;
                var vrtfChunk = rcol.ResolveChunk(mesh.VertexFormatReference);
                if (vrtfChunk is not null)
                {
                    vrtf = Ts4VrtfChunk.Parse(vrtfChunk.Data.Span);
                }
                else if (mesh.IsShadowCaster)
                {
                    vrtf = Ts4VrtfChunk.CreateShadowDefault();
                    diagnostics.Add($"Mesh 0x{mesh.Name:X8} uses the default shadow vertex format because no VRTF chunk was linked.");
                }
                else
                {
                    diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: vertex format could not be resolved.");
                    continue;
                }

                var vbuf = Ts4VbufChunk.Parse(vertexBufferChunk.Data.Span);
                var ibuf = Ts4IbufChunk.Parse(indexBufferChunk.Data.Span);
                var materialInfo = await ResolveMaterialAsync(rcol.ResolveChunk(mesh.MaterialReference), rcol, modelLodResource, textureCache, cancellationToken);
                diagnostics.AddRange(materialInfo.Diagnostics);

                var materialKey = mesh.MaterialReference.Raw;
                if (!materialCache.TryGetValue(materialKey, out var materialIndex))
                {
                    materialIndex = materials.Count;
                    materialCache[materialKey] = materialIndex;
                    materials.Add(new CanonicalMaterial(
                        materialInfo.Name,
                        materialInfo.Textures,
                        materialInfo.ShaderName,
                        materialInfo.IsTransparent,
                        materialInfo.AlphaMode,
                        materialInfo.Approximation,
                        materialInfo.SourceKind));
                }

                var vertices = vbuf.ReadVertices(vrtf, mesh.StreamOffset, mesh.VertexCount, materialInfo.UvScales);
                var indices = ibuf.ReadIndices(mesh.StartIndex, mesh.PrimitiveCount * 3)
                    .Select(static value => (int)value)
                    .ToArray();

                meshes.Add(new CanonicalMesh(
                    $"Mesh_{mesh.Name:X8}",
                    vertices.SelectMany(static vertex => vertex.Position).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Normal ?? []).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Tangent ?? []).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray(),
                    indices,
                    materialIndex,
                    []));
            }
            catch (Exception ex) when (ex is NotSupportedException or InvalidDataException)
            {
                diagnostics.Add($"Skipped mesh 0x{mesh.Name:X8}: {ex.Message}");
            }
        }

        if (meshes.Count == 0)
        {
            diagnostics.Insert(0, $"No triangle meshes could be reconstructed from {modelLodResource.Key.FullTgi}.");
            return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
        }

        var scene = new CanonicalScene(
            logicalRootResource.Name ?? modelLodResource.Name ?? $"BuildBuy_{logicalRootResource.Key.FullInstance:X16}",
            meshes,
            materials,
            [],
            ComputeBounds(meshes));

        diagnostics.Insert(0, $"Selected LOD root: {modelLodResource.Key.FullTgi}");
        return new SceneBuildResult(true, scene, diagnostics, DetermineSceneBuildStatus(scene));
    }

    private async Task<Ts4MaterialInfo> ResolveMaterialAsync(
        Ts4RcolChunk? materialChunk,
        Ts4RcolResource rcol,
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        if (materialChunk is null)
        {
            return Ts4MaterialInfo.CreateFallback("MissingMaterial", "Material chunk is missing.");
        }

        if (materialChunk.Tag == "MATD")
        {
            var matd = Ts4MatdChunk.Parse(materialChunk.Data.Span);
            return await BuildMaterialInfoAsync(matd, ownerResource, textureCache, CanonicalMaterialSourceKind.ExplicitMatd, cancellationToken);
        }

        if (materialChunk.Tag == "MTST")
        {
            var mtst = Ts4MtstChunk.Parse(materialChunk.Data.Span);
            Ts4MaterialInfo? bestCandidate = null;
            foreach (var reference in mtst.MaterialReferences)
            {
                if (reference.IsNull)
                {
                    continue;
                }

                var matdChunk = rcol.ResolveChunk(reference);
                if (matdChunk is null || matdChunk.Tag != "MATD")
                {
                    continue;
                }

                var matd = Ts4MatdChunk.Parse(matdChunk.Data.Span);
                var candidate = await BuildMaterialInfoAsync(matd, ownerResource, textureCache, CanonicalMaterialSourceKind.MaterialSet, cancellationToken);
                if (bestCandidate is null || IsPreferredMaterialCandidate(candidate, bestCandidate))
                {
                    bestCandidate = candidate;
                }
            }

            if (bestCandidate is not null)
            {
                return bestCandidate;
            }

            if (mtst.DefaultMaterialReference is { IsNull: false } selectedReference)
            {
                var matdChunk = rcol.ResolveChunk(selectedReference);
                if (matdChunk is not null && matdChunk.Tag == "MATD")
                {
                    var matd = Ts4MatdChunk.Parse(matdChunk.Data.Span);
                    return await BuildMaterialInfoAsync(matd, ownerResource, textureCache, CanonicalMaterialSourceKind.MaterialSet, cancellationToken);
                }
            }

            return Ts4MaterialInfo.CreateFallback(
                $"MaterialSet_{materialChunk.Key.Instance:X16}",
                "MTST did not expose a usable embedded MATD.");
        }

        return Ts4MaterialInfo.CreateFallback(
            $"Material_{materialChunk.Key.Instance:X16}",
            $"Unsupported material chunk tag {materialChunk.Tag}.");
    }

    private async Task<Ts4MaterialInfo> BuildMaterialInfoAsync(
        Ts4MatdChunk matd,
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CanonicalMaterialSourceKind sourceKind,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var textures = new List<CanonicalTexture>();
        foreach (var reference in matd.TextureReferences)
        {
            var cacheKey = $"{reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}";
            if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                var coreKey = new ResourceKeyRecord(
                    reference.Key.Type,
                    reference.Key.Group,
                    reference.Key.Instance,
                    GuessTypeName(reference.Key.Type));
                cachedTexture = await TryResolveTextureAsync(
                    ownerResource,
                    coreKey,
                    reference.Slot,
                    textureCache,
                    cancellationToken).ConfigureAwait(false);

                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is null)
            {
                diagnostics.Add($"Texture decode failed or resource was unavailable for slot {reference.Slot} ({reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}).");
            }
            else
            {
                textures.Add(cachedTexture with
                {
                    Slot = reference.Slot,
                    Semantic = ClassifyTextureSemantic(reference.Slot)
                });
            }
        }

        if (textures.Count == 0)
        {
            var approximatedTextures = await ResolveFallbackTexturesAsync(ownerResource, textureCache, cancellationToken).ConfigureAwait(false);
            if (approximatedTextures.Count > 0)
            {
                textures.AddRange(approximatedTextures);
                diagnostics.Add("No explicit MATD texture references were resolved; using exact-instance texture candidates as a portable diffuse approximation.");
            }
        }

        foreach (var texture in textures)
        {
            diagnostics.Add(
                $"Texture {DescribeTextureSemantic(texture.Semantic)} ({texture.Slot}) resolved via {DescribeTextureSourceKind(texture.SourceKind)}" +
                $"{FormatTextureSourceLocation(texture.SourcePackagePath)}.");
        }

        var isTransparent = matd.IsTransparent || matd.TextureReferences.Any(static texture => texture.Slot is "alpha" or "overlay");
        var usedFallbackTextureApproximation = textures.Count > 0 && matd.TextureReferences.Count == 0;
        return new Ts4MaterialInfo(
            !string.IsNullOrWhiteSpace(matd.MaterialName) ? matd.MaterialName : $"Material_{matd.MaterialNameHash:X8}",
            matd.ShaderName,
            matd.UvScales,
            textures,
            diagnostics,
            isTransparent,
            isTransparent ? "alpha-test-or-blend" : "opaque",
            usedFallbackTextureApproximation ? "Maxis shaders are approximated to a portable texture-set export." : null,
            usedFallbackTextureApproximation ? CanonicalMaterialSourceKind.FallbackCandidate : sourceKind);
    }

    private static bool IsPreferredMaterialCandidate(Ts4MaterialInfo candidate, Ts4MaterialInfo current)
    {
        if (candidate.Textures.Count != current.Textures.Count)
        {
            return candidate.Textures.Count > current.Textures.Count;
        }

        var candidateSupported = !string.Equals(candidate.ShaderName, "Unsupported", StringComparison.OrdinalIgnoreCase);
        var currentSupported = !string.Equals(current.ShaderName, "Unsupported", StringComparison.OrdinalIgnoreCase);
        if (candidateSupported != currentSupported)
        {
            return candidateSupported;
        }

        var candidateHasApproximation = !string.IsNullOrWhiteSpace(candidate.Approximation);
        var currentHasApproximation = !string.IsNullOrWhiteSpace(current.Approximation);
        if (candidateHasApproximation != currentHasApproximation)
        {
            return !candidateHasApproximation;
        }

        return false;
    }

    private async Task<IReadOnlyList<CanonicalTexture>> ResolveFallbackTexturesAsync(
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        var packageResources = await indexStore.GetPackageResourcesAsync(ownerResource.PackagePath, cancellationToken).ConfigureAwait(false);
        var textureCandidates = packageResources
            .Where(resource => resource.Key.FullInstance == ownerResource.Key.FullInstance && IsTextureType(resource.Key.TypeName))
            .OrderBy(static resource => resource.Key.TypeName == "BuyBuildThumbnail" ? 1 : 0)
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToList();

        if (textureCandidates.Count == 0)
        {
            var globalTextureCandidates = await FindTextureResourcesByInstanceAsync(ownerResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
            foreach (var candidate in globalTextureCandidates)
            {
                if (textureCandidates.Any(existing => existing.Key.FullTgi.Equals(candidate.Key.FullTgi, StringComparison.OrdinalIgnoreCase) &&
                                                      existing.PackagePath.Equals(candidate.PackagePath, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                textureCandidates.Add(candidate);
            }
        }

        var results = new List<CanonicalTexture>(textureCandidates.Count);
        foreach (var candidate in textureCandidates)
        {
            var cacheKey = candidate.Key.FullTgi;
            if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                var pngBytes = await resourceCatalogService.GetTexturePngAsync(candidate.PackagePath, candidate.Key, cancellationToken).ConfigureAwait(false);
                cachedTexture = pngBytes is null
                    ? null
                    : new CanonicalTexture(
                        results.Count == 0 ? "diffuse" : $"extra_{results.Count}",
                        BuildTextureFileName(results.Count == 0 ? "diffuse" : $"extra_{results.Count}", new Ts4ResourceKey(candidate.Key.Type, candidate.Key.Group, candidate.Key.FullInstance)),
                        pngBytes,
                        candidate.Key,
                        candidate.PackagePath,
                        results.Count == 0 ? CanonicalTextureSemantic.BaseColor : CanonicalTextureSemantic.Unknown,
                        textureCandidates.Count == 1 && candidate.PackagePath.Equals(ownerResource.PackagePath, StringComparison.OrdinalIgnoreCase)
                            ? CanonicalTextureSourceKind.FallbackSameInstanceLocal
                            : CanonicalTextureSourceKind.FallbackSameInstanceIndexed);

                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is not null)
            {
                results.Add(cachedTexture);
            }
        }

        return results;
    }

    private async Task<CanonicalTexture?> TryResolveTextureAsync(
        ResourceMetadata ownerResource,
        ResourceKeyRecord key,
        string slot,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        var semantic = ClassifyTextureSemantic(slot);
        var localTexture = await TryDecodeTextureAsync(ownerResource.PackagePath, key, slot, semantic, CanonicalTextureSourceKind.ExplicitLocal, cancellationToken).ConfigureAwait(false);
        if (localTexture is not null)
        {
            return localTexture;
        }

        foreach (var companionPackagePath in EnumerateCompanionPackagePaths(ownerResource.PackagePath))
        {
            var companionTexture = await TryDecodeTextureAsync(companionPackagePath, key, slot, semantic, CanonicalTextureSourceKind.ExplicitCompanion, cancellationToken).ConfigureAwait(false);
            if (companionTexture is not null)
            {
                return companionTexture;
            }
        }

        var matches = await FindTextureResourcesByKeyAsync(key, cancellationToken).ConfigureAwait(false);
        foreach (var match in matches)
        {
            var cacheKey = match.Key.FullTgi;
            if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
            {
                cachedTexture = await TryDecodeTextureAsync(match.PackagePath, match.Key, slot, semantic, CanonicalTextureSourceKind.ExplicitIndexed, cancellationToken).ConfigureAwait(false);
                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is not null)
            {
                return cachedTexture with { Slot = slot, Semantic = semantic };
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCompanionPackagePaths(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            yield break;
        }

        if (packagePath.IndexOf($"{Path.DirectorySeparatorChar}Delta{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var fullPath = packagePath.Replace(
                $"{Path.DirectorySeparatorChar}Delta{Path.DirectorySeparatorChar}",
                $"{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase);
            fullPath = fullPath.Replace("ClientDeltaBuild", "ClientFullBuild", StringComparison.OrdinalIgnoreCase);
            fullPath = fullPath.Replace("SimulationDeltaBuild", "SimulationFullBuild", StringComparison.OrdinalIgnoreCase);

            if (!fullPath.Equals(packagePath, StringComparison.OrdinalIgnoreCase) && File.Exists(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private async Task<ResourceMetadata> ResolveSceneResourceMetadataAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken).ConfigureAwait(false);
        if (bytes.Length > 0)
        {
            return resource;
        }

        foreach (var companionPackagePath in EnumerateCompanionPackagePaths(resource.PackagePath))
        {
            var companionBytes = await resourceCatalogService.GetResourceBytesAsync(companionPackagePath, resource.Key, raw: false, cancellationToken).ConfigureAwait(false);
            if (companionBytes.Length > 0)
            {
                return resource with { PackagePath = companionPackagePath };
            }
        }

        return resource;
    }

    private async Task<CanonicalTexture?> TryDecodeTextureAsync(
        string packagePath,
        ResourceKeyRecord key,
        string slot,
        CanonicalTextureSemantic semantic,
        CanonicalTextureSourceKind sourceKind,
        CancellationToken cancellationToken)
    {
        try
        {
            var pngBytes = await resourceCatalogService.GetTexturePngAsync(packagePath, key, cancellationToken).ConfigureAwait(false);
            return pngBytes is null
                ? null
                : new CanonicalTexture(
                    slot,
                    BuildTextureFileName(slot, new Ts4ResourceKey(key.Type, key.Group, key.FullInstance)),
                    pngBytes,
                    key,
                    packagePath,
                    semantic,
                    sourceKind);
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<ResourceMetadata>> FindTextureResourcesByKeyAsync(ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        var query = new RawResourceBrowserQuery(
            new SourceScope(),
            SearchText: string.Empty,
            Domain: RawResourceDomain.Images,
            TypeNameText: key.TypeName,
            PackageText: string.Empty,
            GroupHexText: key.Group.ToString("X8"),
            InstanceHexText: key.FullInstance.ToString("X16"),
            PreviewableOnly: false,
            ExportCapableOnly: false,
            CompressedKnownOnly: false,
            LinkFilter: ResourceLinkFilter.Any,
            Sort: RawResourceSort.Tgi,
            Offset: 0,
            WindowSize: 64);

        var results = await indexStore.QueryResourcesAsync(query, cancellationToken).ConfigureAwait(false);
        return results.Items
            .Where(resource => resource.Key.Type == key.Type &&
                               resource.Key.Group == key.Group &&
                               resource.Key.FullInstance == key.FullInstance &&
                               IsTextureType(resource.Key.TypeName))
            .ToArray();
    }

    private async Task<IReadOnlyList<ResourceMetadata>> FindTextureResourcesByInstanceAsync(ulong instance, CancellationToken cancellationToken)
    {
        var query = new RawResourceBrowserQuery(
            new SourceScope(),
            SearchText: string.Empty,
            Domain: RawResourceDomain.Images,
            TypeNameText: string.Empty,
            PackageText: string.Empty,
            GroupHexText: string.Empty,
            InstanceHexText: instance.ToString("X16"),
            PreviewableOnly: false,
            ExportCapableOnly: false,
            CompressedKnownOnly: false,
            LinkFilter: ResourceLinkFilter.Any,
            Sort: RawResourceSort.Tgi,
            Offset: 0,
            WindowSize: 128);

        var results = await indexStore.QueryResourcesAsync(query, cancellationToken).ConfigureAwait(false);
        return results.Items
            .Where(resource => resource.Key.FullInstance == instance && IsTextureType(resource.Key.TypeName))
            .OrderBy(static resource => resource.Key.TypeName == "BuyBuildThumbnail" ? 1 : 0)
            .ThenBy(static resource => resource.PackagePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<Ts4ModelLodResolution> TryResolveModelLodAsync(
        ResourceMetadata modelResource,
        Ts4RcolResource root,
        Ts4ChunkReference reference,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var diagnostics = new List<string>();
        if (reference.IsNull)
        {
            diagnostics.Add("Encountered a null ModelLOD reference in MODL.");
            return new Ts4ModelLodResolution(null, diagnostics);
        }

        if (reference.ReferenceType is Ts4ReferenceType.Public or Ts4ReferenceType.Private)
        {
            var embeddedChunk = root.ResolveChunk(reference);
            if (embeddedChunk is null || embeddedChunk.Tag != "MLOD")
            {
                diagnostics.Add($"Embedded ModelLOD reference {reference.Raw:X8} could not be resolved to an MLOD chunk.");
                return new Ts4ModelLodResolution(null, diagnostics);
            }

            return new Ts4ModelLodResolution(
                modelResource with
                {
                    Key = new ResourceKeyRecord(embeddedChunk.Key.Type, embeddedChunk.Key.Group, embeddedChunk.Key.Instance, "ModelLOD"),
                    PreviewKind = PreviewKind.Scene,
                    Name = modelResource.Name
                },
                diagnostics);
        }

        if (reference.ReferenceType == Ts4ReferenceType.Delayed)
        {
            var delayedKey = root.ResolveExternalKey(reference);
            if (delayedKey is null)
            {
                diagnostics.Add($"Delayed ModelLOD reference {reference.Raw:X8} could not be mapped to an external resource key.");
                return new Ts4ModelLodResolution(null, diagnostics);
            }

            var directResource = await TryResolveModelLodResourceByKeyAsync(modelResource, delayedKey.Value, cancellationToken).ConfigureAwait(false);
            if (directResource is not null)
            {
                if (!string.Equals(directResource.PackagePath, modelResource.PackagePath, StringComparison.OrdinalIgnoreCase))
                {
                    diagnostics.Add($"Resolved external ModelLOD {delayedKey.Value.Type:X8}:{delayedKey.Value.Group:X8}:{delayedKey.Value.Instance:X16} from {Path.GetFileName(directResource.PackagePath)}.");
                }

                return new Ts4ModelLodResolution(directResource, diagnostics);
            }

            return new Ts4ModelLodResolution(
                new ResourceMetadata(
                    Guid.NewGuid(),
                    modelResource.DataSourceId,
                    modelResource.SourceKind,
                    modelResource.PackagePath,
                    new ResourceKeyRecord(delayedKey.Value.Type, delayedKey.Value.Group, delayedKey.Value.Instance, "ModelLOD"),
                    modelResource.Name,
                    0,
                    0,
                    false,
                    PreviewKind.Scene,
                    true,
                    true,
                    "Referenced by Model MODL LOD entry.",
                    string.Empty),
                diagnostics);
        }

        diagnostics.Add($"Unsupported MODL reference type {reference.ReferenceType}.");
        return new Ts4ModelLodResolution(null, diagnostics);
    }

    private async Task<ResourceMetadata?> TryResolveModelLodResourceByKeyAsync(
        ResourceMetadata ownerResource,
        Ts4ResourceKey key,
        CancellationToken cancellationToken)
    {
        var record = new ResourceKeyRecord(key.Type, key.Group, key.Instance, "ModelLOD");

        var localBytes = await resourceCatalogService.GetResourceBytesAsync(ownerResource.PackagePath, record, raw: false, cancellationToken).ConfigureAwait(false);
        if (localBytes.Length > 0)
        {
            return ownerResource with
            {
                Key = record,
                PreviewKind = PreviewKind.Scene,
                Name = ownerResource.Name
            };
        }

        foreach (var companionPackagePath in EnumerateCompanionPackagePaths(ownerResource.PackagePath))
        {
            var companionBytes = await resourceCatalogService.GetResourceBytesAsync(companionPackagePath, record, raw: false, cancellationToken).ConfigureAwait(false);
            if (companionBytes.Length > 0)
            {
                return new ResourceMetadata(
                    Guid.NewGuid(),
                    ownerResource.DataSourceId,
                    ownerResource.SourceKind,
                    companionPackagePath,
                    record,
                    ownerResource.Name,
                    0,
                    0,
                    false,
                    PreviewKind.Scene,
                    true,
                    true,
                    "Resolved from companion package via MODL external reference.",
                    string.Empty);
            }
        }

        var matches = await FindModelLodResourcesByKeyAsync(record, cancellationToken).ConfigureAwait(false);
        var match = matches.FirstOrDefault();
        if (match is not null)
        {
            return match with
            {
                PreviewKind = PreviewKind.Scene,
                Name = ownerResource.Name
            };
        }

        return null;
    }

    private async Task<IReadOnlyList<ResourceMetadata>> FindModelLodResourcesByKeyAsync(ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        var query = new RawResourceBrowserQuery(
            new SourceScope(),
            SearchText: string.Empty,
            Domain: RawResourceDomain.ThreeDRelated,
            TypeNameText: "ModelLOD",
            PackageText: string.Empty,
            GroupHexText: key.Group.ToString("X8"),
            InstanceHexText: key.FullInstance.ToString("X16"),
            PreviewableOnly: false,
            ExportCapableOnly: false,
            CompressedKnownOnly: false,
            LinkFilter: ResourceLinkFilter.Any,
            Sort: RawResourceSort.Tgi,
            Offset: 0,
            WindowSize: 64);

        var results = await indexStore.QueryResourcesAsync(query, cancellationToken).ConfigureAwait(false);
        return results.Items
            .Where(resource => resource.Key.Type == key.Type &&
                               resource.Key.Group == key.Group &&
                               resource.Key.FullInstance == key.FullInstance &&
                               string.Equals(resource.Key.TypeName, "ModelLOD", StringComparison.Ordinal))
            .ToArray();
    }

    private static string BuildTextureFileName(string slot, Ts4ResourceKey key) =>
        $"{slot}_{key.Type:X8}_{key.Group:X8}_{key.Instance:X16}.png";

    private static CanonicalTextureSemantic ClassifyTextureSemantic(string? slot) => slot?.ToLowerInvariant() switch
    {
        "diffuse" or "basecolor" or "albedo" => CanonicalTextureSemantic.BaseColor,
        "normal" => CanonicalTextureSemantic.Normal,
        "specular" => CanonicalTextureSemantic.Specular,
        "gloss" or "roughness" or "smoothness" => CanonicalTextureSemantic.Gloss,
        "alpha" or "opacity" or "mask" => CanonicalTextureSemantic.Opacity,
        "emissive" => CanonicalTextureSemantic.Emissive,
        "overlay" => CanonicalTextureSemantic.Overlay,
        _ => CanonicalTextureSemantic.Unknown
    };

    private static string DescribeTextureSemantic(CanonicalTextureSemantic semantic) => semantic switch
    {
        CanonicalTextureSemantic.BaseColor => "BaseColor",
        CanonicalTextureSemantic.Normal => "Normal",
        CanonicalTextureSemantic.Specular => "Specular",
        CanonicalTextureSemantic.Gloss => "Gloss",
        CanonicalTextureSemantic.Opacity => "Opacity",
        CanonicalTextureSemantic.Emissive => "Emissive",
        CanonicalTextureSemantic.Overlay => "Overlay",
        _ => "Unknown"
    };

    private static string DescribeTextureSourceKind(CanonicalTextureSourceKind sourceKind) => sourceKind switch
    {
        CanonicalTextureSourceKind.ExplicitLocal => "explicit local package lookup",
        CanonicalTextureSourceKind.ExplicitCompanion => "explicit companion full-build lookup",
        CanonicalTextureSourceKind.ExplicitIndexed => "explicit indexed cross-package lookup",
        CanonicalTextureSourceKind.FallbackSameInstanceLocal => "same-instance local fallback",
        CanonicalTextureSourceKind.FallbackSameInstanceIndexed => "same-instance indexed fallback",
        _ => "unknown lookup"
    };

    private static string FormatTextureSourceLocation(string? packagePath) =>
        string.IsNullOrWhiteSpace(packagePath) ? string.Empty : $" from {Path.GetFileName(packagePath)}";

    private static string GuessTypeName(uint type) => type switch
    {
        0x00B2D882 => "DSTImage",
        0x2F7D0004 => "PNGImage2",
        0x2BC04EDF => "DSTImage",
        0x0988C7E1 => "LRLEImage",
        0x3453CF95 => "RLE2Image",
        0x1B4D2A70 => "RLESImage",
        _ => $"0x{type:X8}"
    };

    private static bool IsTextureType(string typeName) => typeName is
        "BuyBuildThumbnail" or
        "PNGImage" or
        "PNGImage2" or
        "DSTImage" or
        "LRLEImage" or
        "RLE2Image" or
        "RLESImage";

    private static Bounds3D ComputeBounds(IEnumerable<CanonicalMesh> meshes)
    {
        var positions = meshes.SelectMany(static mesh => mesh.Positions).Chunk(3).ToArray();
        if (positions.Length == 0)
        {
            return new Bounds3D(0, 0, 0, 0, 0, 0);
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var position in positions)
        {
            minX = Math.Min(minX, position[0]);
            minY = Math.Min(minY, position[1]);
            minZ = Math.Min(minZ, position[2]);
            maxX = Math.Max(maxX, position[0]);
            maxY = Math.Max(maxY, position[1]);
            maxZ = Math.Max(maxZ, position[2]);
        }

        return new Bounds3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static SceneBuildStatus DetermineSceneBuildStatus(CanonicalScene scene)
    {
        if (scene.Meshes.Count == 0)
        {
            return SceneBuildStatus.Unsupported;
        }

        var hasApproximateMaterial = scene.Materials.Any(material =>
            !string.IsNullOrWhiteSpace(material.Approximation) ||
            material.SourceKind is CanonicalMaterialSourceKind.FallbackCandidate or CanonicalMaterialSourceKind.Unsupported);
        var hasMissingTextures = scene.Materials.Any(material => material.Textures.Count == 0);

        return hasApproximateMaterial || hasMissingTextures
            ? SceneBuildStatus.Partial
            : SceneBuildStatus.SceneReady;
    }
}

public static class PreviewDebugProbe
{
    public static string InspectModelLod(byte[] bytes)
    {
        var lines = new List<string>();
        try
        {
            var rcol = Ts4RcolResource.Parse(bytes);
            lines.Add($"RCOL parsed: public={rcol.PublicChunkCount} chunks={rcol.Chunks.Count} external={rcol.ExternalResources.Count}");
            foreach (var chunk in rcol.Chunks)
            {
                lines.Add($"Chunk {chunk.Tag} {chunk.Key.Type:X8}:{chunk.Key.Group:X8}:{chunk.Key.Instance:X16} len={chunk.Length}");
            }

            var mlodChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MLOD");
            if (mlodChunk is null)
            {
                lines.Add("No MLOD chunk found.");
                return string.Join(Environment.NewLine, lines);
            }

            lines.Add($"MLOD chunk bytes={mlodChunk.Data.Length}");
            var mlod = Ts4MlodChunk.Parse(mlodChunk.Data.Span);
            lines.Add($"MLOD parsed: meshes={mlod.Meshes.Count}");
            foreach (var mesh in mlod.Meshes)
            {
                lines.Add(
                    $"Mesh name=0x{mesh.Name:X8} primitive={mesh.PrimitiveType} flags=0x{mesh.Flags:X} vbuf={mesh.VertexBufferReference.Raw:X8} ibuf={mesh.IndexBufferReference.Raw:X8} vrtf={mesh.VertexFormatReference.Raw:X8} verts={mesh.VertexCount} prims={mesh.PrimitiveCount}");
            }
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            lines.Add(ex.StackTrace ?? "(no stack)");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

internal sealed record Ts4ModelLodResolution(ResourceMetadata? Resource, IReadOnlyList<string> Diagnostics);

internal readonly record struct Ts4ChunkReference(uint Raw)
{
    public bool IsNull => Raw == 0;
    public Ts4ReferenceType ReferenceType => IsNull ? Ts4ReferenceType.Invalid : (Ts4ReferenceType)(Raw >> 28);
    public int Index => IsNull ? -1 : (int)(Raw & 0x0FFFFFFF) - 1;
}

internal enum Ts4ReferenceType : byte
{
    Invalid = 255,
    Public = 0,
    Private = 1,
    Delayed = 3
}

internal readonly record struct Ts4ResourceKey(uint Type, uint Group, ulong Instance);

internal sealed record Ts4RcolChunk(Ts4ResourceKey Key, Ts4ChunkReference SelfReference, int Position, int Length, ReadOnlyMemory<byte> Data, string Tag);

internal sealed record Ts4MaterialInfo(
    string Name,
    string ShaderName,
    float[] UvScales,
    IReadOnlyList<CanonicalTexture> Textures,
    IReadOnlyList<string> Diagnostics,
    bool IsTransparent,
    string AlphaMode,
    string? Approximation,
    CanonicalMaterialSourceKind SourceKind)
{
    public static Ts4MaterialInfo CreateFallback(string name, string reason) =>
        new(
            name,
            "Unsupported",
            [1f / short.MaxValue, 1f / short.MaxValue, 1f / short.MaxValue],
            [],
            [reason],
            false,
            "opaque",
            "Material fell back to a placeholder because the source material chunk was unsupported.",
            CanonicalMaterialSourceKind.Unsupported);
}

internal readonly record struct Ts4TextureReference(string Slot, Ts4ResourceKey Key);

internal sealed record Ts4MatdChunk(
    string MaterialName,
    uint MaterialNameHash,
    string ShaderName,
    float[] UvScales,
    IReadOnlyList<Ts4TextureReference> TextureReferences,
    bool IsTransparent)
{
    public static Ts4MatdChunk Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16 || !bytes[..4].SequenceEqual("MATD"u8))
        {
            return CreateFallback(0);
        }

        var materialNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var shaderNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        var textureReferences = new List<Ts4TextureReference>();
        var uvScales = new[] { 1f, 1f, 1f };

        var mtrlOffset = bytes.IndexOf("MTRL"u8);
        if (mtrlOffset < 0 || mtrlOffset + 16 > bytes.Length)
        {
            return new Ts4MatdChunk(
                $"Material_{materialNameHash:X8}",
                materialNameHash,
                $"Shader_{shaderNameHash:X8}",
                uvScales,
                textureReferences,
                false);
        }

        var propertyCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(mtrlOffset + 12, 4)));
        var tableOffset = mtrlOffset + 16;
        for (var i = 0; i < propertyCount && tableOffset + 16 <= bytes.Length; i++, tableOffset += 16)
        {
            var propertyHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset, 4));
            var propertyType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 4, 4));
            var normalizedPropertyType = propertyType & 0xFFFF;
            var propertyArity = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 8, 4));
            var propertyOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 12, 4)));
            if (propertyOffset < 0 || propertyOffset >= bytes.Length)
            {
                continue;
            }

            if (TryReadTextureReference(propertyHash, normalizedPropertyType, propertyArity, propertyOffset, bytes, textureReferences))
            {
                continue;
            }

            if (normalizedPropertyType == 1)
            {
                TryApplyScalarProperty(propertyHash, propertyOffset, bytes, uvScales);
                continue;
            }

            if (normalizedPropertyType == 4)
            {
                TryApplyVectorProperty(propertyHash, propertyOffset, propertyArity, bytes, uvScales);
            }
        }

        return new Ts4MatdChunk(
            $"Material_{materialNameHash:X8}",
            materialNameHash,
            $"Shader_{shaderNameHash:X8}",
            uvScales,
            textureReferences
                .GroupBy(static reference => $"{reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}", StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            false);
    }

    private static Ts4MatdChunk CreateFallback(uint materialNameHash) =>
        new(
            $"Material_{materialNameHash:X8}",
            materialNameHash,
            "Unsupported",
            [1f, 1f, 1f],
            [],
            false);

    private static Ts4ResourceKey ReadMatdEmbeddedResourceKey(ReadOnlySpan<byte> bytes)
    {
        var instance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, 8));
        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var group = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        return new Ts4ResourceKey(type, group, instance);
    }

    private static bool TryReadTextureReference(
        uint propertyHash,
        uint normalizedPropertyType,
        uint propertyArity,
        int propertyOffset,
        ReadOnlySpan<byte> bytes,
        List<Ts4TextureReference> textureReferences)
    {
        if (propertyOffset + 16 > bytes.Length)
        {
            return false;
        }

        if (normalizedPropertyType is not (1 or 2 or 4))
        {
            return false;
        }

        if (normalizedPropertyType == 1 && propertyArity < 3)
        {
            return false;
        }

        if (normalizedPropertyType == 4 && propertyArity < 4)
        {
            return false;
        }

        var keyBytes = bytes.Slice(propertyOffset, 16);
        foreach (var key in ReadPotentialEmbeddedResourceKeys(keyBytes))
        {
            if (!IsImageType(key.Type))
            {
                continue;
            }

            textureReferences.Add(new Ts4TextureReference(GetTextureSlotName(propertyHash, textureReferences.Count), key));
            return true;
        }

        return false;
    }

    private static Ts4ResourceKey[] ReadPotentialEmbeddedResourceKeys(ReadOnlySpan<byte> bytes)
    {
        var embeddedKey = ReadMatdEmbeddedResourceKey(bytes);
        var typeFirstType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        var typeFirstGroup = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        var typeFirstInstance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8, 8));
        var typeFirstKey = new Ts4ResourceKey(typeFirstType, typeFirstGroup, typeFirstInstance);
        return [embeddedKey, typeFirstKey];
    }

    private static string GetTextureSlotName(uint propertyHash, int existingTextureCount) => propertyHash switch
    {
        0xC53A9D1B => "diffuse",
        0x773BA60F => "specular",
        0x9F5D1C9B => "normal",
        0x3A9F5D1C => "alpha",
        _ => existingTextureCount == 0 ? "diffuse" : $"texture_{existingTextureCount}"
    };

    private static void TryApplyScalarProperty(uint propertyHash, int propertyOffset, ReadOnlySpan<byte> bytes, float[] uvScales)
    {
        if (propertyOffset + 4 > bytes.Length)
        {
            return;
        }

        var value = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset, 4));
        if (!IsPlausibleUvScale(value))
        {
            return;
        }

        switch (propertyHash)
        {
            case 0x57002869:
                uvScales[0] = value;
                break;
            case 0x795EAC31:
                uvScales[1] = value;
                break;
        }
    }

    private static void TryApplyVectorProperty(uint propertyHash, int propertyOffset, uint propertyArity, ReadOnlySpan<byte> bytes, float[] uvScales)
    {
        var floatCount = checked((int)Math.Clamp(propertyArity, 2, 4));
        if (propertyOffset + (floatCount * 4) > bytes.Length)
        {
            return;
        }

        if (propertyHash is 0xC3FAAC4F or 0x04A5DAA3)
        {
            var u = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset, 4));
            var v = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset + 4, 4));
            if (IsPlausibleUvScale(u) && IsPlausibleUvScale(v))
            {
                uvScales[0] = u;
                uvScales[1] = v;
            }
        }
    }

    private static bool IsPlausibleUvScale(float value) =>
        float.IsFinite(value) &&
        Math.Abs(value) >= 0.001f &&
        Math.Abs(value) <= 1024f;

    private static bool IsImageType(uint type) => type is
        0x00B2D882 or
        0x2F7D0004 or
        0x2BC04EDF or
        0x0988C7E1 or
        0x3453CF95 or
        0x1B4D2A70;
}

internal sealed record Ts4MtstChunk(Ts4ChunkReference? DefaultMaterialReference, IReadOnlyList<Ts4ChunkReference> MaterialReferences)
{
    public static Ts4MtstChunk Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("MTST"u8))
        {
            return new(null, []);
        }

        var entryCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)));
        var references = new List<Ts4ChunkReference>(Math.Max(0, entryCount));
        var offset = 20;
        for (var i = 0; i < entryCount && offset + 12 <= bytes.Length; i++, offset += 12)
        {
            var reference = new Ts4ChunkReference(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)));
            if (!reference.IsNull)
            {
                references.Add(reference);
            }
        }

        return new(null, references);
    }
}

internal ref struct SpanReader
{
    private ReadOnlySpan<byte> remaining;
    private readonly string context;

    public SpanReader(ReadOnlySpan<byte> data, string? context = null)
    {
        remaining = data;
        this.context = string.IsNullOrWhiteSpace(context) ? "TS4 chunk" : context;
        Position = 0;
    }

    public int Position { get; private set; }
    public int RemainingLength => remaining.Length;

    public void Skip(int count)
    {
        Ensure(count);
        remaining = remaining[count..];
        Position += count;
    }

    public void ExpectTag(string tag)
    {
        var bytes = ReadBytes(tag.Length);
        var actual = System.Text.Encoding.ASCII.GetString(bytes);
        if (!string.Equals(actual, tag, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Expected tag '{tag}', found '{actual}'.");
        }

        remaining = remaining[tag.Length..];
        Position += tag.Length;
    }

    public byte ReadByte()
    {
        Ensure(1);
        var value = remaining[0];
        remaining = remaining[1..];
        Position += 1;
        return value;
    }

    public ushort ReadUInt16()
    {
        var value = BinaryPrimitives.ReadUInt16LittleEndian(ReadBytes(2));
        Position += 2;
        remaining = remaining[2..];
        return value;
    }

    public short ReadInt16()
    {
        var value = BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));
        Position += 2;
        remaining = remaining[2..];
        return value;
    }

    public uint ReadUInt32()
    {
        var value = BinaryPrimitives.ReadUInt32LittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public int ReadInt32()
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public float ReadSingle()
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(ReadBytes(4));
        Position += 4;
        remaining = remaining[4..];
        return value;
    }

    public Ts4ResourceKey ReadResourceKey()
    {
        var instance = ReadUInt64();
        var type = ReadUInt32();
        var group = ReadUInt32();
        return new Ts4ResourceKey(type, group, instance);
    }

    public Ts4ResourceKey ReadExternalResourceKey()
    {
        var instance = ReadUInt64();
        var type = ReadUInt32();
        var group = ReadUInt32();
        return new Ts4ResourceKey(type, group, instance);
    }

    public ulong ReadUInt64()
    {
        var value = BinaryPrimitives.ReadUInt64LittleEndian(ReadBytes(8));
        Position += 8;
        remaining = remaining[8..];
        return value;
    }

    public ReadOnlySpan<byte> ReadBytes(int count)
    {
        Ensure(count);
        return remaining[..count];
    }

    private void Ensure(int count)
    {
        if (count < 0 || remaining.Length < count)
        {
            throw new InvalidDataException($"Unexpected end of {context} data at offset 0x{Position:X}.");
        }
    }
}

internal enum Ts4VertexUsage : byte
{
    Position = 0,
    Normal = 1,
    Uv = 2,
    BlendIndex = 3,
    BlendWeight = 4,
    Color = 5,
    Tangent = 6
}

internal sealed record Ts4VertexElement(Ts4VertexUsage Usage, byte UsageIndex, byte Format, ushort Offset);

internal sealed class Ts4VrtfChunk
{
    public required int VertexStride { get; init; }
    public required IReadOnlyList<Ts4VertexElement> Elements { get; init; }

    public static Ts4VrtfChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data, "VRTF chunk");
        reader.ExpectTag("VRTF");
        _ = reader.ReadUInt32();
        var stride = checked((ushort)reader.ReadUInt32());
        var elementCount = reader.ReadInt32();
        _ = reader.ReadUInt32();

        var elements = new List<Ts4VertexElement>(elementCount);
        for (var index = 0; index < elementCount; index++)
        {
            var usage = (Ts4VertexUsage)reader.ReadByte();
            var usageIndex = reader.ReadByte();
            var format = reader.ReadByte();
            var offset = reader.ReadByte();
            elements.Add(new Ts4VertexElement(usage, usageIndex, format, offset));
        }

        return new Ts4VrtfChunk
        {
            VertexStride = stride,
            Elements = elements
        };
    }

    public static Ts4VrtfChunk CreateShadowDefault() =>
        new()
        {
            VertexStride = 16,
            Elements =
            [
                new Ts4VertexElement(Ts4VertexUsage.Position, 0, 0x07, 0)
            ]
        };
}

internal sealed record Ts4DecodedVertex(float[] Position, float[]? Normal, float[]? Tangent, float[]? Uv0);

internal sealed class Ts4VbufChunk
{
    private readonly byte[] rawData;
    private readonly Ts4ChunkReference swizzleInfoReference;

    private Ts4VbufChunk(byte[] rawData, Ts4ChunkReference swizzleInfoReference)
    {
        this.rawData = rawData;
        this.swizzleInfoReference = swizzleInfoReference;
    }

    public static Ts4VbufChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data, "VBUF chunk");
        reader.ExpectTag("VBUF");
        _ = reader.ReadUInt32();
        _ = reader.ReadUInt32();
        var swizzleInfoReference = new Ts4ChunkReference(reader.ReadUInt32());

        var payload = data[reader.Position..].ToArray();
        return new Ts4VbufChunk(payload, swizzleInfoReference);
    }

    public IReadOnlyList<Ts4DecodedVertex> ReadVertices(Ts4VrtfChunk vrtf, uint streamOffset, int vertexCount, float[] uvScales)
    {
        var results = new List<Ts4DecodedVertex>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            var vertexOffset = checked((int)streamOffset + (index * vrtf.VertexStride));
            if (vertexOffset < 0 || vertexOffset + vrtf.VertexStride > rawData.Length)
            {
                break;
            }

            float[]? position = null;
            float[]? normal = null;
            float[]? tangent = null;
            float[]? uv0 = null;

            foreach (var element in vrtf.Elements)
            {
                var offset = vertexOffset + element.Offset;
                if (offset < 0 || offset >= rawData.Length)
                {
                    continue;
                }

                switch (element.Usage)
                {
                    case Ts4VertexUsage.Position:
                        position = ReadVector3(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Normal:
                        normal = ReadNormalLike(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Tangent:
                        tangent = ReadNormalLike(rawData, offset, element.Format);
                        break;
                    case Ts4VertexUsage.Uv when element.UsageIndex == 0:
                        uv0 = ReadUv(rawData, offset, element.Format, uvScales);
                        break;
                }
            }

            if (position is null)
            {
                continue;
            }

            results.Add(new Ts4DecodedVertex(position, normal, tangent, uv0));
        }

        return results;
    }

    private static float[] ReadVector3(byte[] data, int offset, byte format) => format switch
    {
        0x02 => [ReadSingle(data, offset), ReadSingle(data, offset + 4), ReadSingle(data, offset + 8)],
        0x07 => DecodeShort4Normalized(data, offset),
        _ => throw new NotSupportedException($"Unsupported position vertex format 0x{format:X2}.")
    };

    private static float[] ReadNormalLike(byte[] data, int offset, byte format) => format switch
    {
        0x05 => DecodeColor4Normalized(data, offset),
        0x07 => DecodeShort4Normalized(data, offset),
        0x08 => DecodeByte4Snorm(data, offset),
        _ => throw new NotSupportedException($"Unsupported normal/tangent vertex format 0x{format:X2}.")
    };

    private static float[] ReadUv(byte[] data, int offset, byte format, float[] uvScales)
    {
        var scaleU = uvScales.Length > 0 ? uvScales[0] : 1f;
        var scaleV = uvScales.Length > 1 ? uvScales[1] : scaleU;

        var uv = format switch
        {
            0x01 => [ReadSingle(data, offset), ReadSingle(data, offset + 4)],
            0x06 => DecodeShort2Normalized(data, offset),
            0x07 => DecodeShort4Normalized(data, offset)[..2],
            _ => throw new NotSupportedException($"Unsupported UV vertex format 0x{format:X2}.")
        };

        uv[0] *= scaleU;
        uv[1] *= scaleV;
        return uv;
    }

    private static float[] DecodeColor4Normalized(byte[] data, int offset)
    {
        var x = ((data[offset + 2] / 255f) * 2f) - 1f;
        var y = ((data[offset + 1] / 255f) * 2f) - 1f;
        var z = ((data[offset] / 255f) * 2f) - 1f;
        return [x, y, z];
    }

    private static float[] DecodeShort4Normalized(byte[] data, int offset)
    {
        var x = ReadInt16(data, offset);
        var y = ReadInt16(data, offset + 2);
        var z = ReadInt16(data, offset + 4);
        var w = ReadUInt16(data, offset + 6);
        var scale = w == 0 ? (float)short.MaxValue : w;
        return [x / (float)scale, y / (float)scale, z / (float)scale];
    }

    private static float[] DecodeShort2Normalized(byte[] data, int offset)
    {
        var u = ReadInt16(data, offset) / (float)short.MaxValue;
        var v = ReadInt16(data, offset + 2) / (float)short.MaxValue;
        return [u, v];
    }

    private static float[] DecodeByte4Snorm(byte[] data, int offset)
    {
        static float Decode(byte value)
        {
            var signed = unchecked((sbyte)value);
            return Math.Clamp(signed / 127f, -1f, 1f);
        }

        return [Decode(data[offset]), Decode(data[offset + 1]), Decode(data[offset + 2])];
    }

    private static float ReadSingle(byte[] data, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset, 4));

    private static short ReadInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2));

    private static ushort ReadUInt16(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}

internal sealed class Ts4IbufChunk
{
    private readonly uint flags;
    private readonly int indexSize;
    private readonly byte[] rawData;

    private Ts4IbufChunk(uint flags, int indexSize, byte[] rawData)
    {
        this.flags = flags;
        this.indexSize = indexSize;
        this.rawData = rawData;
    }

    public static Ts4IbufChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data, "IBUF chunk");
        reader.ExpectTag("IBUF");
        _ = reader.ReadUInt32();
        var flags = reader.ReadUInt32();
        var payload = data[reader.Position..].ToArray();
        var indexSize = (flags & 0x1) != 0 ? 4 : 2;
        return new Ts4IbufChunk(flags, indexSize, payload);
    }

    public IReadOnlyList<uint> ReadIndices(int startIndex, int count)
    {
        var directStartOffset = startIndex * indexSize;
        if (directStartOffset + (count * indexSize) > rawData.Length &&
            TryReadCompactDeltaIndices(startIndex, count, out var compact))
        {
            return compact;
        }

        var results = new List<uint>(count);
        for (var offset = directStartOffset; offset + indexSize <= rawData.Length && results.Count < count; offset += indexSize)
        {
            results.Add(indexSize == 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(offset, 4))
                : BinaryPrimitives.ReadUInt16LittleEndian(rawData.AsSpan(offset, 2)));
        }

        return results;
    }

    private bool TryReadCompactDeltaIndices(int startIndex, int count, out IReadOnlyList<uint> indices)
    {
        indices = Array.Empty<uint>();

        if (flags != 0x1 || rawData.Length < 4)
        {
            return false;
        }

        var encodedCount = (rawData.Length - 4) / 2;
        if (startIndex < 0 || count < 0 || startIndex + count > encodedCount)
        {
            return false;
        }

        var decoded = new uint[encodedCount];
        var accumulator = 0;
        var decodedIndex = 0;
        for (var offset = 4; offset + 1 < rawData.Length && decodedIndex < encodedCount; offset += 2)
        {
            accumulator += BinaryPrimitives.ReadInt16LittleEndian(rawData.AsSpan(offset, 2));
            decoded[decodedIndex++] = unchecked((uint)accumulator);
        }

        indices = decoded.Skip(startIndex).Take(count).ToArray();
        return true;
    }
}

internal sealed class Ts4RcolResource
{
    public required int PublicChunkCount { get; init; }
    public required IReadOnlyList<Ts4RcolChunk> Chunks { get; init; }
    public required IReadOnlyList<Ts4ResourceKey> ExternalResources { get; init; }

    public Ts4RcolChunk? ResolveChunk(Ts4ChunkReference reference)
    {
        if (reference.IsNull)
        {
            return null;
        }

        var index = reference.ReferenceType switch
        {
            Ts4ReferenceType.Public => reference.Index,
            Ts4ReferenceType.Private => PublicChunkCount + reference.Index,
            _ => -1
        };

        return index >= 0 && index < Chunks.Count ? Chunks[index] : null;
    }

    public Ts4ResourceKey? ResolveExternalKey(Ts4ChunkReference reference)
    {
        if (reference.ReferenceType != Ts4ReferenceType.Delayed)
        {
            return null;
        }

        return reference.Index >= 0 && reference.Index < ExternalResources.Count
            ? ExternalResources[reference.Index]
            : null;
    }

    public static Ts4RcolResource Parse(ReadOnlyMemory<byte> bytes)
    {
        var reader = new SpanReader(bytes.Span, "RCOL resource");
        _ = reader.ReadUInt32();
        var publicChunkCount = reader.ReadInt32();
        _ = reader.ReadUInt32();
        var externalCount = reader.ReadInt32();
        var chunkCount = reader.ReadInt32();

        var chunkKeys = new Ts4ResourceKey[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            chunkKeys[i] = reader.ReadResourceKey();
        }

        var externalKeys = new Ts4ResourceKey[externalCount];
        for (var i = 0; i < externalCount; i++)
        {
            externalKeys[i] = reader.ReadExternalResourceKey();
        }

        var positions = new (int Position, int Length)[chunkCount];
        for (var i = 0; i < chunkCount; i++)
        {
            positions[i] = (checked((int)reader.ReadUInt32()), reader.ReadInt32());
        }

        if (chunkCount == 1)
        {
            positions[0] = (0x2C + (externalCount * 16), checked((int)bytes.Length - (0x2C + (externalCount * 16))));
        }

        var chunks = new List<Ts4RcolChunk>(chunkCount);
        for (var i = 0; i < chunkCount; i++)
        {
            var (position, length) = positions[i];
            if (position < 0 || length < 0 || position + length > bytes.Length)
            {
                continue;
            }

            var data = bytes.Slice(position, length);
            var tag = data.Length >= 4
                ? System.Text.Encoding.ASCII.GetString(data.Span[..4])
                : string.Empty;

            chunks.Add(new Ts4RcolChunk(
                chunkKeys[i],
                new Ts4ChunkReference(CreateReferenceRaw(i, publicChunkCount)),
                position,
                length,
                data,
                tag));
        }

        return new Ts4RcolResource
        {
            PublicChunkCount = publicChunkCount,
            Chunks = chunks,
            ExternalResources = externalKeys
        };
    }

    private static uint CreateReferenceRaw(int chunkIndex, int publicChunkCount)
    {
        var index = (uint)(chunkIndex + 1);
        return chunkIndex < publicChunkCount ? index : index | 0x10000000;
    }
}

internal sealed class Ts4ModlChunk
{
    public required IReadOnlyList<Ts4ModlLodEntry> LodEntries { get; init; }

    public static Ts4ModlChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data, "MODL chunk");
        reader.ExpectTag("MODL");
        var version = reader.ReadUInt32();
        var lodCount = reader.ReadInt32();
        reader.Skip(24);

        if (version >= 258 && version < 0x300)
        {
            var extraBoundsCount = reader.ReadInt32();
            reader.Skip(extraBoundsCount * 24);
            reader.Skip(8);
        }
        else if (version >= 0x300)
        {
            reader.Skip(20);
        }

        var lodEntries = new List<Ts4ModlLodEntry>(lodCount);
        for (var i = 0; i < lodCount; i++)
        {
            lodEntries.Add(new Ts4ModlLodEntry(
                new Ts4ChunkReference(reader.ReadUInt32()),
                reader.ReadUInt32(),
                reader.ReadUInt32(),
                reader.ReadSingle(),
                reader.ReadSingle()));
        }

        return new Ts4ModlChunk { LodEntries = lodEntries };
    }
}

internal readonly record struct Ts4ModlLodEntry(Ts4ChunkReference Reference, uint Flags, uint Id, float MinZ, float MaxZ)
{
    public bool IsShadow => (Id & 0x00010000) != 0;
    public string DisplayName => Id switch
    {
        0x00000000 => "HighDetail",
        0x00000001 => "MediumDetail",
        0x00000002 => "LowDetail",
        0x00010000 => "HighDetailShadow",
        0x00010001 => "MediumDetailShadow",
        0x00010002 => "LowDetailShadow",
        _ => $"LOD_{Id:X8}"
    };
}

internal sealed class Ts4MlodChunk
{
    public required IReadOnlyList<Ts4MlodMesh> Meshes { get; init; }

    public static Ts4MlodChunk Parse(ReadOnlySpan<byte> data)
    {
        var reader = new SpanReader(data, "MLOD chunk");
        reader.ExpectTag("MLOD");
        var version = reader.ReadUInt32();
        var declaredMeshCount = reader.ReadInt32();
        var meshes = new List<Ts4MlodMesh>(Math.Max(0, declaredMeshCount));

        while (meshes.Count < declaredMeshCount && reader.RemainingLength >= 4)
        {
            var meshIndex = meshes.Count;
            try
            {
                var expectedSize = reader.ReadUInt32();
                var meshStart = reader.Position;
                var name = reader.ReadUInt32();
                var materialRef = new Ts4ChunkReference(reader.ReadUInt32());
                var vrtfRef = new Ts4ChunkReference(reader.ReadUInt32());
                var vbufRef = new Ts4ChunkReference(reader.ReadUInt32());
                var ibufRef = new Ts4ChunkReference(reader.ReadUInt32());
                var primitiveAndFlags = reader.ReadUInt32();
                var primitiveType = (Ts4PrimitiveType)(primitiveAndFlags & 0xFF);
                var flags = primitiveAndFlags >> 8;
                var streamOffset = reader.ReadUInt32();
                var startVertex = reader.ReadInt32();
                var startIndex = reader.ReadInt32();
                var minVertexIndex = reader.ReadInt32();
                var vertexCount = reader.ReadInt32();
                var primitiveCount = reader.ReadInt32();
                reader.Skip(24);
                var bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);

                var skinRef = bytesRemainingInMesh >= 4
                    ? new Ts4ChunkReference(reader.ReadUInt32())
                    : default;
                bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);

                var jointCount = bytesRemainingInMesh >= 4
                    ? reader.ReadInt32()
                    : 0;
                bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);

                if (jointCount > 0)
                {
                    var jointBytes = checked(jointCount * 4);
                    if (bytesRemainingInMesh < jointBytes)
                    {
                        throw new InvalidDataException($"MLOD mesh joint list overruns its declared size. Remaining {bytesRemainingInMesh} bytes, needed {jointBytes} bytes.");
                    }

                    reader.Skip(jointBytes);
                    bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);
                }

                var scaleOffsetRef = bytesRemainingInMesh >= 4
                    ? new Ts4ChunkReference(reader.ReadUInt32())
                    : default;
                bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);

                var geometryStateCount = bytesRemainingInMesh >= 4
                    ? reader.ReadInt32()
                    : 0;
                bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);

                if (geometryStateCount > 0)
                {
                    var geometryStateBytes = checked(geometryStateCount * 20);
                    if (bytesRemainingInMesh < geometryStateBytes)
                    {
                        throw new InvalidDataException($"MLOD geometry state list overruns its declared size. Remaining {bytesRemainingInMesh} bytes, needed {geometryStateBytes} bytes.");
                    }

                    reader.Skip(geometryStateBytes);
                    bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);
                }

                if (version > 0x00000201 && bytesRemainingInMesh >= 20)
                {
                    reader.Skip(20);
                    bytesRemainingInMesh = (int)expectedSize - (reader.Position - meshStart);
                }

                if (version > 0x00000203 && bytesRemainingInMesh >= 4)
                {
                    reader.Skip(4);
                }

                var consumed = reader.Position - meshStart;
                var hasPlausibleDeclaredSize = expectedSize >= 72;
                if (hasPlausibleDeclaredSize && expectedSize < consumed)
                {
                    throw new InvalidDataException($"MLOD mesh record overruns its declared size. Declared {expectedSize} bytes, consumed {consumed} bytes.");
                }

                if (hasPlausibleDeclaredSize && consumed < expectedSize)
                {
                    reader.Skip((int)(expectedSize - consumed));
                }

                meshes.Add(new Ts4MlodMesh(
                    name,
                    materialRef,
                    vrtfRef,
                    vbufRef,
                    ibufRef,
                    skinRef,
                    primitiveType,
                    flags,
                    streamOffset,
                    startVertex,
                    startIndex,
                    minVertexIndex,
                    vertexCount,
                    primitiveCount,
                    jointCount,
                    scaleOffsetRef,
                    geometryStateCount));
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                throw new InvalidDataException(
                    $"MLOD mesh[{meshIndex}] of declared {declaredMeshCount} parse failed at absolute offset 0x{reader.Position:X}. {ex.Message}",
                    ex);
            }
        }

        return new Ts4MlodChunk { Meshes = meshes };
    }
}

internal readonly record struct Ts4MlodMesh(
    uint Name,
    Ts4ChunkReference MaterialReference,
    Ts4ChunkReference VertexFormatReference,
    Ts4ChunkReference VertexBufferReference,
    Ts4ChunkReference IndexBufferReference,
    Ts4ChunkReference SkinReference,
    Ts4PrimitiveType PrimitiveType,
    uint Flags,
    uint StreamOffset,
    int StartVertex,
    int StartIndex,
    int MinVertexIndex,
    int VertexCount,
    int PrimitiveCount,
    int JointCount,
    Ts4ChunkReference ScaleOffsetReference,
    int GeometryStateCount)
{
    public bool IsShadowCaster => (Flags & 0x10) != 0;
    public bool IsSkinned => !SkinReference.IsNull || JointCount > 0;
}

internal enum Ts4PrimitiveType : uint
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3
}
