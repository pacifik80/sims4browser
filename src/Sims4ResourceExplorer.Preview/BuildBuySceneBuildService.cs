using System.Buffers.Binary;
using System.Collections.Concurrent;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed partial class BuildBuySceneBuildService : ISceneBuildService
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly IIndexStore indexStore;
    private readonly ConcurrentDictionary<(string PackagePath, ulong FullInstance), Task<IReadOnlyList<ResourceMetadata>>> packageInstanceResourceCache = new();
    private readonly ConcurrentDictionary<TextureLookupKey, Task<IReadOnlyList<ResourceMetadata>>> textureResourceByKeyCache = new();
    private readonly ConcurrentDictionary<ulong, Task<IReadOnlyList<ResourceMetadata>>> textureResourceByInstanceCache = new();
    private readonly ConcurrentDictionary<(string PackagePath, string FullTgi), Task<byte[]?>> texturePngCache = new();
    private readonly ConcurrentDictionary<string, string[]> siblingClientPackageCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string[]> globalClientPackageCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly uint[] FallbackImageTypes = [0x00B2D882u, 0x2F7D0004u, 0x2BC04EDFu, 0x0988C7E1u, 0x3453CF95u, 0x1B4D2A70u];

    public BuildBuySceneBuildService(IResourceCatalogService resourceCatalogService, IIndexStore indexStore)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.indexStore = indexStore;
    }

    private Task<IReadOnlyList<ResourceMetadata>> GetPackageInstanceResourcesAsync(
        string packagePath,
        ulong fullInstance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return packageInstanceResourceCache.GetOrAdd(
            (packagePath, fullInstance),
            static (key, store) => store.GetResourcesByInstanceAsync(key.PackagePath, key.FullInstance, CancellationToken.None),
            indexStore);
    }

    private Task<IReadOnlyList<ResourceMetadata>> GetTextureResourcesByKeyAsync(
        ResourceKeyRecord key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return textureResourceByKeyCache.GetOrAdd(
            new TextureLookupKey(key.Type, key.Group, key.FullInstance, key.TypeName),
            static (lookupKey, service) => service.FindTextureResourcesByKeyCoreAsync(lookupKey, CancellationToken.None),
            this);
    }

    private Task<IReadOnlyList<ResourceMetadata>> GetTextureResourcesByInstanceAsync(
        ulong fullInstance,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return textureResourceByInstanceCache.GetOrAdd(
            fullInstance,
            static (instance, service) => service.FindTextureResourcesByInstanceCoreAsync(instance, CancellationToken.None),
            this);
    }

    private Task<byte[]?> GetTexturePngAsync(
        string packagePath,
        ResourceKeyRecord key,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return texturePngCache.GetOrAdd(
            (packagePath, key.FullTgi),
            static (cacheKey, state) => state.ResourceCatalogService.GetTexturePngAsync(
                cacheKey.PackagePath,
                state.ResourceKey,
                CancellationToken.None),
            (ResourceCatalogService: resourceCatalogService, ResourceKey: key));
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

        var embeddedFallback = await TryEmbeddedModelLodFallbackAsync(modelResource, root, diagnostics, cancellationToken).ConfigureAwait(false);
        if (embeddedFallback.Success)
        {
            return embeddedFallback;
        }

        diagnostics = embeddedFallback.Diagnostics.ToList();

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

        var packageResources = await GetPackageInstanceResourcesAsync(modelResource.PackagePath, modelResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
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

    private async Task<SceneBuildResult> TryEmbeddedModelLodFallbackAsync(
        ResourceMetadata modelResource,
        Ts4RcolResource root,
        IEnumerable<string> existingDiagnostics,
        CancellationToken cancellationToken)
    {
        var diagnostics = existingDiagnostics
            .Where(static message => !string.IsNullOrWhiteSpace(message))
            .ToList();

        var embeddedCandidates = root.Chunks
            .Where(static chunk => chunk.Tag == "MLOD")
            .OrderBy(static chunk => chunk.SelfReference.ReferenceType != Ts4ReferenceType.Public)
            .ThenBy(static chunk => chunk.SelfReference.Index)
            .ToArray();

        if (embeddedCandidates.Length == 0)
        {
            diagnostics.Add("No embedded ModelLOD chunks were available in the model root for fallback.");
            return new SceneBuildResult(false, null, diagnostics, SceneBuildStatus.Unsupported);
        }

        diagnostics.Add($"Falling back to {embeddedCandidates.Length} embedded ModelLOD chunk(s) from the model root.");
        foreach (var candidate in embeddedCandidates)
        {
            Ts4MlodChunk mlod;
            try
            {
                mlod = Ts4MlodChunk.Parse(candidate.Data.Span);
            }
            catch (Exception ex) when (ex is InvalidDataException or NotSupportedException)
            {
                diagnostics.Add($"Embedded ModelLOD chunk {candidate.Key.Type:X8}:{candidate.Key.Group:X8}:{candidate.Key.Instance:X16} could not be parsed cleanly: {ex.Message}");
                continue;
            }

            var syntheticResource = modelResource with
            {
                Key = new ResourceKeyRecord(candidate.Key.Type, candidate.Key.Group, candidate.Key.Instance, "ModelLOD"),
                PreviewKind = PreviewKind.Scene,
                Name = modelResource.Name
            };

            var candidateResult = await BuildParsedModelLodSceneAsync(
                root,
                mlod,
                syntheticResource,
                modelResource,
                cancellationToken).ConfigureAwait(false);

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

        return await BuildParsedModelLodSceneAsync(rcol, mlod, modelLodResource, logicalRootResource, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SceneBuildResult> BuildParsedModelLodSceneAsync(
        Ts4RcolResource rcol,
        Ts4MlodChunk mlod,
        ResourceMetadata modelLodResource,
        ResourceMetadata logicalRootResource,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var meshes = new List<CanonicalMesh>();
        var materials = new List<CanonicalMaterial>();
        var materialCache = new Dictionary<uint, int>();
        var materialCoverageTiers = new List<Ts4MaterialCoverageTier>();
        var materialSamplingSources = new List<string>();
        var materialFamilies = new List<string>();
        var materialStrategies = new List<string>();
        var materialVisualPayloadKinds = new List<string>();
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
                var materialInfo = await ResolveMaterialAsync(rcol.ResolveChunk(mesh.MaterialReference), rcol, modelLodResource, logicalRootResource, textureCache, cancellationToken);
                diagnostics.AddRange(materialInfo.Diagnostics);

                var materialKey = mesh.MaterialReference.Raw;
                if (!materialCache.TryGetValue(materialKey, out var materialIndex))
                {
                    materialIndex = materials.Count;
                    materialCache[materialKey] = materialIndex;
                    materialCoverageTiers.Add(materialInfo.CoverageTier);
                    materialSamplingSources.AddRange(materialInfo.SamplingSources);
                    if (!string.IsNullOrWhiteSpace(materialInfo.ShaderFamily))
                    {
                        materialFamilies.Add(materialInfo.ShaderFamily);
                    }
                    materialStrategies.Add(materialInfo.DecodeStrategy);
                    materialVisualPayloadKinds.Add(materialInfo.VisualPayloadKind);
                    materials.Add(new CanonicalMaterial(
                        materialInfo.Name,
                        materialInfo.Textures,
                        materialInfo.ShaderName,
                        materialInfo.IsTransparent,
                        materialInfo.AlphaMode,
                        materialInfo.AlphaTextureSlot,
                        materialInfo.LayeredTextureSlots,
                        materialInfo.Approximation,
                        materialInfo.SourceKind,
                        materialInfo.ApproximateBaseColor is { Length: >= 3 } color
                            ? new CanonicalColor(color[0], color[1], color[2], color.Length >= 4 ? color[3] : 1f)
                            : null));
                }

                var meshWindow = Ts4MeshWindow.From(mesh);
                var vertices = vbuf.ReadVertices(
                    vrtf,
                    mesh.StreamOffset,
                    meshWindow.VertexReadBase,
                    mesh.VertexCount,
                    materialInfo.UvScales);
                var indices = ibuf.ReadIndices(
                        mesh.StartIndex,
                        mesh.PrimitiveCount * 3,
                        meshWindow.IndexNormalizeBase,
                        mesh.VertexCount)
                    .Select(static value => (int)value)
                    .ToArray();
                var (validTriangles, invalidTriangles) = CountTriangleValidity(indices, vertices.Count);
                if (validTriangles == 0)
                {
                    diagnostics.Add(
                        $"Skipped mesh 0x{mesh.Name:X8}: index stream produced no valid triangles for {vertices.Count} vertices.");
                    continue;
                }

                if (invalidTriangles > validTriangles)
                {
                    diagnostics.Add(
                        $"Skipped mesh 0x{mesh.Name:X8}: index stream is too unstable ({validTriangles} valid triangle(s), {invalidTriangles} invalid triangle(s)).");
                    continue;
                }

                var uvSelection = SelectPreferredUvCoordinates(vertices, mesh.Name, diagnostics);
                var uv0 = vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray();
                var uv1 = vertices.SelectMany(static vertex => vertex.Uv1 ?? []).ToArray();
                if (materialInfo.Textures.Count > 0 && uvSelection.Uvs.Length == 0 && uv0.Length == 0 && uv1.Length == 0)
                {
                    diagnostics.Add(
                        $"Mesh 0x{mesh.Name:X8} resolved texture data, but this LOD exposes no UV coordinates; textured preview is unavailable on this fallback geometry.");
                }

                meshes.Add(new CanonicalMesh(
                    $"Mesh_{mesh.Name:X8}",
                    vertices.SelectMany(static vertex => vertex.Position).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Normal ?? []).ToArray(),
                    vertices.SelectMany(static vertex => vertex.Tangent ?? []).ToArray(),
                    uvSelection.Uvs,
                    uv0,
                    uv1,
                    uvSelection.Channel,
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

        if (materialCoverageTiers.Count > 0)
        {
            diagnostics.Insert(
                0,
                $"Material coverage: {string.Join(", ", materialCoverageTiers.GroupBy(static tier => tier).OrderBy(static group => group.Key).Select(static group => $"{group.Key}={group.Count()}"))}.");
        }

        if (materialSamplingSources.Count > 0)
        {
            diagnostics.Insert(
                1,
                $"Material sampling sources: {string.Join(", ", materialSamplingSources.GroupBy(static source => source, StringComparer.OrdinalIgnoreCase).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase).Select(static group => $"{group.Key}={group.Count()}"))}.");
        }

        if (materialFamilies.Count > 0)
        {
            diagnostics.Insert(
                2,
                $"Material families: {string.Join(", ", materialFamilies.GroupBy(static family => family, StringComparer.OrdinalIgnoreCase).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase).Select(static group => $"{group.Key}={group.Count()}"))}.");
        }

        if (materialStrategies.Count > 0)
        {
            diagnostics.Insert(
                3,
                $"Material decode strategies: {string.Join(", ", materialStrategies.GroupBy(static strategy => strategy, StringComparer.OrdinalIgnoreCase).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase).Select(static group => $"{group.Key}={group.Count()}"))}.");
        }

        if (materialVisualPayloadKinds.Count > 0)
        {
            diagnostics.Insert(
                4,
                $"Material visual payloads: {string.Join(", ", materialVisualPayloadKinds.GroupBy(static kind => kind, StringComparer.OrdinalIgnoreCase).OrderBy(static group => group.Key, StringComparer.OrdinalIgnoreCase).Select(static group => $"{group.Key}={group.Count()}"))}.");
        }

        diagnostics.Insert(0, $"Selected LOD root: {modelLodResource.Key.FullTgi}");
        return new SceneBuildResult(true, scene, diagnostics, DetermineSceneBuildStatus(scene));
    }

    private static Ts4MaterialInfo MergeMtstMaterialCandidates(
        Ts4MaterialInfo primary,
        IReadOnlyList<(Ts4MtstEntry Entry, Ts4MaterialInfo Material, int Score)> orderedCandidates,
        string mergeDescription = "MTST layered texture slots were merged from alternate material states when the primary state left them unresolved.")
    {
        if (orderedCandidates.Count <= 1)
        {
            return primary;
        }

        var texturesBySlot = new Dictionary<string, CanonicalTexture>(StringComparer.OrdinalIgnoreCase);
        var borrowedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in orderedCandidates)
        {
            foreach (var texture in candidate.Material.Textures)
            {
                if (texturesBySlot.TryAdd(texture.Slot, texture) &&
                    !ReferenceEquals(candidate.Material, primary))
                {
                    borrowedSlots.Add(texture.Slot);
                }
            }
        }

        if (texturesBySlot.Count == primary.Textures.Count)
        {
            return primary;
        }

        var approximation = string.IsNullOrWhiteSpace(primary.Approximation)
            ? mergeDescription
            : $"{primary.Approximation} {mergeDescription}";
        var layeredSlots = orderedCandidates
            .SelectMany(static candidate => candidate.Material.LayeredTextureSlots)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var alphaTextureSlot = !string.IsNullOrWhiteSpace(primary.AlphaTextureSlot)
            ? primary.AlphaTextureSlot
            : orderedCandidates
                .Select(static candidate => candidate.Material.AlphaTextureSlot)
                .FirstOrDefault(static slot => !string.IsNullOrWhiteSpace(slot));

        return primary with
        {
            Textures = texturesBySlot.Values.ToArray(),
            SamplingSources = orderedCandidates
                .SelectMany(static candidate => candidate.Material.SamplingSources)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            LayeredTextureSlots = layeredSlots,
            AlphaTextureSlot = alphaTextureSlot,
            Approximation = borrowedSlots.Count == 0
                ? approximation
                : $"{approximation} Borrowed slots: {string.Join(", ", borrowedSlots.OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase))}."
        };
    }

    private static int ScoreMaterialCandidate(Ts4MaterialInfo materialInfo)
    {
        var score = materialInfo.Textures.Count * 100;
        score += materialInfo.LayeredTextureSlots.Count * 10;
        score += materialInfo.CoverageTier switch
        {
            Ts4MaterialCoverageTier.StaticReady => 30,
            Ts4MaterialCoverageTier.Approximate => 20,
            _ => 0
        };
        if (materialInfo.ApproximateBaseColor is { Length: >= 3 })
        {
            score += 10;
        }

        if (!string.IsNullOrWhiteSpace(materialInfo.AlphaTextureSlot))
        {
            score += 5;
        }

        score -= materialInfo.Diagnostics.Count(static diagnostic =>
            diagnostic.Contains("Texture decode failed", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("Ignored unresolved heuristic texture binding", StringComparison.OrdinalIgnoreCase)) * 20;
        score -= materialInfo.Diagnostics.Count(static diagnostic =>
            diagnostic.Contains("not found in the indexed game data", StringComparison.OrdinalIgnoreCase)) * 30;
        score -= materialInfo.Diagnostics.Count(static diagnostic =>
            diagnostic.Contains("No explicit MATD texture references were resolved", StringComparison.OrdinalIgnoreCase)) * 5;
        return score;
    }

    private sealed record MtstStatePropertySummary(string ValueSummary, bool IsPortableVisual, uint[]? PackedUInt32Values);

    private static Dictionary<string, MtstStatePropertySummary> SummarizeMtstStateProperties(
        IReadOnlyList<(Ts4MtstEntry Entry, Ts4MatdChunk Matd, Ts4MaterialInfo Material, int Score)> candidates)
    {
        var summary = new Dictionary<string, MtstStatePropertySummary>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var materialIr = Ts4ShaderSemantics.BuildMaterialIr(candidate.Matd);
            foreach (var property in materialIr.Properties)
            {
                if (property.ValueSummary is null)
                {
                    continue;
                }

                summary[property.Name] = new MtstStatePropertySummary(
                    property.ValueSummary,
                    IsPortableVisualProperty(property),
                    property.PackedUInt32Values);
            }
        }

        return summary;
    }

    private static string? SummarizeMtstStateDifferences(
        IReadOnlyList<(uint StateNameHash, Dictionary<string, MtstStatePropertySummary> PropertySummary)> groupedStates)
    {
        if (groupedStates.Count < 2)
        {
            return null;
        }

        var varyingProperties = new List<string>();
        foreach (var propertyName in groupedStates
                     .SelectMany(static state => state.PropertySummary.Keys)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase))
        {
            var values = groupedStates
                .Select(state => state.PropertySummary.TryGetValue(propertyName, out var value) ? value.ValueSummary : "(missing)")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (values.Length > 1)
            {
                varyingProperties.Add(propertyName);
            }
        }

        if (varyingProperties.Count == 0)
        {
            return "MTST state variants do not expose any materially different portable shader properties.";
        }

        var previewNames = varyingProperties.Take(4).ToArray();
        var portablePayloadDifference = varyingProperties.Any(propertyName =>
            groupedStates.Any(state =>
                state.PropertySummary.TryGetValue(propertyName, out var property) &&
                property.IsPortableVisual));
        var prefix = portablePayloadDifference
            ? "MTST state variants change portable shader properties:"
            : "MTST state variants only change non-portable control properties:";
        var detailParts = new List<string>();
        var coveredProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (TryBuildSharedPackedStateTokenSummary(varyingProperties, groupedStates, out var tokenSummary, out var tokenProperties))
        {
            detailParts.Add(tokenSummary);
            foreach (var propertyName in tokenProperties)
            {
                coveredProperties.Add(propertyName);
            }
        }

        detailParts.AddRange(previewNames
            .Where(propertyName => !coveredProperties.Contains(propertyName))
            .Select(propertyName => BuildMtstStateValueSummary(propertyName, groupedStates))
            .Where(static detail => !string.IsNullOrWhiteSpace(detail)));
        var suffix = varyingProperties.Count > previewNames.Length ? "; ..." : string.Empty;
        return $"{prefix} {string.Join("; ", detailParts)}{suffix}.";
    }

    private static string BuildMtstStateValueSummary(
        string propertyName,
        IReadOnlyList<(uint StateNameHash, Dictionary<string, MtstStatePropertySummary> PropertySummary)> groupedStates)
    {
        var packedStates = groupedStates
            .Select(state => state.PropertySummary.TryGetValue(propertyName, out var property) ? property.PackedUInt32Values : null)
            .ToArray();
        if (TryBuildCompactPackedStateSummary(propertyName, groupedStates, packedStates, out var compactSummary))
        {
            return compactSummary;
        }

        var values = groupedStates
            .Select(state =>
            {
                var value = state.PropertySummary.TryGetValue(propertyName, out var property)
                    ? property.ValueSummary
                    : "(missing)";
                return $"0x{state.StateNameHash:X8}={value}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return $"{propertyName} [{string.Join(", ", values)}]";
    }

    private static bool TryBuildCompactPackedStateSummary(
        string propertyName,
        IReadOnlyList<(uint StateNameHash, Dictionary<string, MtstStatePropertySummary> PropertySummary)> groupedStates,
        uint[]?[] packedStates,
        out string summary)
    {
        summary = string.Empty;
        if (packedStates.Length == 0 || packedStates.Any(static values => values is null))
        {
            return false;
        }

        var first = packedStates[0]!;
        if (first.Length == 0 || packedStates.Any(values => values!.Length != first.Length))
        {
            return false;
        }

        var varyingIndices = Enumerable.Range(0, first.Length)
            .Where(index => packedStates.Select(values => values![index]).Distinct().Count() > 1)
            .ToArray();
        if (varyingIndices.Length == 0)
        {
            return false;
        }

        var values = groupedStates
            .Select((state, stateIndex) =>
            {
                var parts = varyingIndices
                    .Select(index => $"word[{index}]=0x{packedStates[stateIndex]![index]:X8}")
                    .ToArray();
                return $"0x{state.StateNameHash:X8}={string.Join(", ", parts)}";
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        summary = $"{propertyName} [{string.Join(", ", values)}]";
        return true;
    }

    private static bool TryBuildSharedPackedStateTokenSummary(
        IReadOnlyList<string> varyingProperties,
        IReadOnlyList<(uint StateNameHash, Dictionary<string, MtstStatePropertySummary> PropertySummary)> groupedStates,
        out string summary,
        out string[] coveredProperties)
    {
        summary = string.Empty;
        coveredProperties = [];

        var candidates = new List<(string PropertyName, int WordIndex, uint[] Series)>();
        foreach (var propertyName in varyingProperties)
        {
            var packedStates = groupedStates
                .Select(state => state.PropertySummary.TryGetValue(propertyName, out var property) ? property.PackedUInt32Values : null)
                .ToArray();
            if (packedStates.Length == 0 || packedStates.Any(static values => values is null))
            {
                continue;
            }

            var first = packedStates[0]!;
            if (first.Length == 0 || packedStates.Any(values => values!.Length != first.Length))
            {
                continue;
            }

            var varyingIndices = Enumerable.Range(0, first.Length)
                .Where(index => packedStates.Select(values => values![index]).Distinct().Count() > 1)
                .ToArray();
            if (varyingIndices.Length != 1)
            {
                continue;
            }

            var varyingIndex = varyingIndices[0];
            candidates.Add((
                propertyName,
                varyingIndex,
                packedStates.Select(values => values![varyingIndex]).ToArray()));
        }

        if (!TryFindBestSharedPackedStateTokenGroup(candidates, out var bestGroup))
        {
            return false;
        }

        var resolvedBestGroup = bestGroup!;
        var properties = resolvedBestGroup.Select(static candidate => candidate.PropertyName).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var firstSeries = resolvedBestGroup.First().Series;
        var tokenGroups = groupedStates
            .Select((state, index) => (state.StateNameHash, Token: firstSeries[index]))
            .GroupBy(static item => item.Token)
            .OrderBy(static group => group.Key)
            .Select(group => $"0x{group.Key:X8}->[{string.Join(", ", group.Select(static item => $"0x{item.StateNameHash:X8}"))}]")
            .ToArray();

        var roleName = InferSharedPackedStateRoleName(properties);
        summary = $"{roleName} ({FormatSharedPackedPropertyList(properties)}) [{string.Join(", ", tokenGroups)}]";
        coveredProperties = properties;
        return true;
    }

    private static bool TryFindBestSharedPackedStateTokenGroup(
        IReadOnlyList<(string PropertyName, int WordIndex, uint[] Series)> candidates,
        out IGrouping<string, (string PropertyName, int WordIndex, uint[] Series)>? bestGroup)
    {
        bestGroup = candidates
            .GroupBy(
                static candidate => string.Join("|", candidate.Series.Select(static value => value.ToString("X8", System.Globalization.CultureInfo.InvariantCulture))),
                StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() >= 2)
            .OrderByDescending(static group => group.Count())
            .FirstOrDefault();
        return bestGroup is not null;
    }

    private sealed record SharedPackedStateTokenStats(
        IReadOnlyDictionary<uint, int> GroupSizesByStateHash,
        int DistinctTokenCount);

    private static SharedPackedStateTokenStats BuildSharedPackedStateTokenStats(
        IReadOnlyList<(uint StateNameHash, Dictionary<string, MtstStatePropertySummary> PropertySummary)> groupedStates)
    {
        var candidates = new List<(string PropertyName, int WordIndex, uint[] Series)>();
        var varyingProperties = groupedStates
            .SelectMany(static state => state.PropertySummary.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var propertyName in varyingProperties)
        {
            var packedStates = groupedStates
                .Select(state => state.PropertySummary.TryGetValue(propertyName, out var property) ? property.PackedUInt32Values : null)
                .ToArray();
            if (packedStates.Length == 0 || packedStates.Any(static values => values is null))
            {
                continue;
            }

            var first = packedStates[0]!;
            if (first.Length == 0 || packedStates.Any(values => values!.Length != first.Length))
            {
                continue;
            }

            var varyingIndices = Enumerable.Range(0, first.Length)
                .Where(index => packedStates.Select(values => values![index]).Distinct().Count() > 1)
                .ToArray();
            if (varyingIndices.Length != 1)
            {
                continue;
            }

            candidates.Add((propertyName, varyingIndices[0], packedStates.Select(values => values![varyingIndices[0]]).ToArray()));
        }

        if (!TryFindBestSharedPackedStateTokenGroup(candidates, out var bestGroup))
        {
            return new SharedPackedStateTokenStats(new Dictionary<uint, int>(), 0);
        }

        var series = bestGroup!.First().Series;
        var groupedByToken = groupedStates
            .Select((state, index) => (state.StateNameHash, Token: series[index]))
            .GroupBy(static item => item.Token)
            .ToArray();
        var groupSizes = groupedByToken
            .SelectMany(group => group.Select(item => (item.StateNameHash, Size: group.Count())))
            .ToDictionary(static entry => entry.StateNameHash, static entry => entry.Size);
        return new SharedPackedStateTokenStats(groupSizes, groupedByToken.Length);
    }

    private static string InferSharedPackedStateRoleName(IReadOnlyList<string> properties)
    {
        if (properties.Any(static property => property.Contains("Aura", StringComparison.OrdinalIgnoreCase)))
        {
            return "AuraVariantToken";
        }

        if (properties.Any(static property =>
                property.Contains("Variation", StringComparison.OrdinalIgnoreCase) ||
                property.Contains("Season", StringComparison.OrdinalIgnoreCase)))
        {
            return "VariationStateToken";
        }

        if (properties.Any(static property =>
                property.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
                property.Contains("Paint", StringComparison.OrdinalIgnoreCase)))
        {
            return "DecalStateToken";
        }

        return "StateVariantToken";
    }

    private static string FormatSharedPackedPropertyList(IReadOnlyList<string> properties)
    {
        var namedProperties = properties
            .Where(static property => !property.StartsWith("Prop_", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var unnamedCount = properties.Count - namedProperties.Length;
        if (namedProperties.Length == 0)
        {
            return unnamedCount == 1 ? "1 correlated control" : $"{unnamedCount} correlated controls";
        }

        if (unnamedCount <= 0)
        {
            return string.Join(", ", namedProperties);
        }

        var suffix = unnamedCount == 1 ? "+1 correlated control" : $"+{unnamedCount} correlated controls";
        return $"{string.Join(", ", namedProperties)}, {suffix}";
    }

    private static string? BuildMtstTokenClusterNote(
        SharedPackedStateTokenStats stats,
        int stateCount,
        uint selectedStateNameHash)
    {
        if (stats.DistinctTokenCount <= 0 || stats.DistinctTokenCount >= stateCount)
        {
            return null;
        }

        var selectedClusterSize = stats.GroupSizesByStateHash.TryGetValue(selectedStateNameHash, out var size) ? size : 0;
        return selectedClusterSize > 1
            ? $"Packed control states collapse to {stats.DistinctTokenCount} distinct variant token(s); the selected default-like cluster covers {selectedClusterSize} state entries."
            : $"Packed control states collapse to {stats.DistinctTokenCount} distinct variant token(s).";
    }

    private static bool IsPortableVisualProperty(MaterialIrProperty property) =>
        IsPortableVisualPropertyName(property.Name);

    private static bool IsPortableVisualPropertyName(string propertyName) =>
        propertyName.Contains("Color", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Tint", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Diffuse", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("BaseColor", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Albedo", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Overlay", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Detail", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Decal", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Dirt", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Grime", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Emission", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Emissive", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("SelfIllum", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("Specular", StringComparison.OrdinalIgnoreCase);

    private static (int ValidTriangles, int InvalidTriangles) CountTriangleValidity(IReadOnlyList<int> indices, int vertexCount)
    {
        if (indices.Count == 0 || vertexCount <= 0)
        {
            return (0, 0);
        }

        var valid = 0;
        var invalid = 0;
        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            var a = indices[index];
            var b = indices[index + 1];
            var c = indices[index + 2];
            if (a < 0 || b < 0 || c < 0 || a >= vertexCount || b >= vertexCount || c >= vertexCount)
            {
                invalid++;
            }
            else
            {
                valid++;
            }
        }

        return (valid, invalid);
    }

    private async Task<Ts4MaterialInfo> ResolveMaterialAsync(
        Ts4RcolChunk? materialChunk,
        Ts4RcolResource rcol,
        ResourceMetadata ownerResource,
        ResourceMetadata fallbackTextureOwnerResource,
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
            return await BuildMaterialInfoAsync(matd, ownerResource, fallbackTextureOwnerResource, textureCache, CanonicalMaterialSourceKind.ExplicitMatd, cancellationToken);
        }

        if (materialChunk.Tag == "MTST")
        {
            var mtst = Ts4MtstChunk.Parse(materialChunk.Data.Span);
            var stateCandidates = new List<(Ts4MtstEntry Entry, Ts4MatdChunk Matd, Ts4MaterialInfo Material, int Score)>();
            foreach (var entry in mtst.Entries
                .Where(static entry => !entry.Reference.IsNull)
                .DistinctBy(static entry => (entry.Reference.Raw, entry.StateNameHash)))
            {
                var matdChunk = rcol.ResolveChunk(entry.Reference);
                if (matdChunk is not null && matdChunk.Tag == "MATD")
                {
                    var matd = Ts4MatdChunk.Parse(matdChunk.Data.Span);
                    var materialInfo = await BuildMaterialInfoAsync(matd, ownerResource, fallbackTextureOwnerResource, textureCache, CanonicalMaterialSourceKind.MaterialSet, cancellationToken);
                    stateCandidates.Add((entry, matd, materialInfo, ScoreMaterialCandidate(materialInfo)));
                }
            }

            if (stateCandidates.Count > 0)
            {
                var groupedStates = stateCandidates
                    .GroupBy(static candidate => candidate.Entry.StateNameHash)
                    .Select(static group =>
                    {
                        var ordered = group
                            .OrderByDescending(static candidate => candidate.Score)
                            .ThenBy(static candidate => candidate.Entry.Reference.Raw)
                            .ToList();
                        var merged = MergeMtstMaterialCandidates(
                            ordered[0].Material,
                            ordered.Select(static candidate => (candidate.Entry, candidate.Material, candidate.Score)).ToList(),
                            "MTST paired MATD entries within the selected state were merged when one entry left texture slots unresolved.");
                        return new
                        {
                            StateNameHash = group.Key,
                            Best = ordered[0],
                            Candidates = ordered,
                            Material = merged,
                            PropertySummary = SummarizeMtstStateProperties(ordered),
                            Score = ScoreMaterialCandidate(merged)
                        };
                    })
                    .ToList();
                var stateTokenStats = BuildSharedPackedStateTokenStats(
                    groupedStates.Select(static group => (group.StateNameHash, group.PropertySummary)).ToArray());
                groupedStates = groupedStates
                    .OrderByDescending(static candidate => candidate.Score)
                    .ThenByDescending(candidate => stateTokenStats.GroupSizesByStateHash.TryGetValue(candidate.StateNameHash, out var size) ? size : 0)
                    .ThenByDescending(static candidate => candidate.StateNameHash == 0)
                    .ToList();
                var selectedGroup = groupedStates[0];
                var selected = selectedGroup.Best;
                var materialInfo = selectedGroup.Material;
                if (stateCandidates.Count > 1)
                {
                    var stateNote = selected.Entry.StateNameHash == 0
                        ? "MTST exposes multiple material states; preview evaluated the available entries and kept the inferred default state."
                        : $"MTST exposes multiple material states; preview evaluated the available entries and selected state hash 0x{selected.Entry.StateNameHash:X8} as the best rendered default.";
                    var scoreNote = $"State scores: {string.Join(", ", groupedStates.Select(static group => $"0x{group.StateNameHash:X8}={group.Score}"))}.";
                    var tokenNote = BuildMtstTokenClusterNote(stateTokenStats, groupedStates.Count, selected.Entry.StateNameHash);
                    var diffNote = SummarizeMtstStateDifferences(groupedStates.Select(static group => (group.StateNameHash, group.PropertySummary)).ToArray());
                    return materialInfo with
                    {
                        Approximation = string.IsNullOrWhiteSpace(materialInfo.Approximation)
                            ? string.Join(" ", new[] { stateNote, scoreNote, tokenNote, diffNote }.Where(static value => !string.IsNullOrWhiteSpace(value)))
                            : string.Join(" ", new[] { materialInfo.Approximation, stateNote, scoreNote, tokenNote, diffNote }.Where(static value => !string.IsNullOrWhiteSpace(value)))
                    };
                }

                return materialInfo;
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
        ResourceMetadata fallbackTextureOwnerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CanonicalMaterialSourceKind sourceKind,
        CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var textures = new List<CanonicalTexture>();
        Ts4ShaderProfileRegistry.Instance.TryGetProfile(matd.ShaderNameHash, out var shaderProfile);
        var materialIr = Ts4ShaderSemantics.BuildMaterialIr(matd);
        var propertiesByHash = materialIr.Properties
            .GroupBy(static property => property.Hash)
            .ToDictionary(static group => group.Key, static group => group.First());
        var materialDecode = Ts4MaterialDecoder.Decode(materialIr, shaderProfile);
        var effectiveSamplingInstructions = materialDecode.SamplingInstructions.ToList();
        var authoritativeSlots = matd.TextureReferences
            .Where(static reference => !reference.IsHeuristic)
            .Select(reference => Ts4ShaderSemantics.ResolveTextureSlotName(reference, shaderProfile))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (shaderProfile is not null)
        {
            var matchedPropertyCount = materialIr.Properties.Count(static property => !property.Name.StartsWith("Prop_", StringComparison.Ordinal));
            diagnostics.Add(
                $"Shader profile {shaderProfile.Name} matched {matchedPropertyCount}/{materialIr.Properties.Count} MATD propertie(s).");
        }
        if (!string.IsNullOrWhiteSpace(materialDecode.ShaderFamilyName))
        {
            diagnostics.Add($"Material decode family: {materialDecode.ShaderFamilyName}.");
        }
        diagnostics.Add($"Material decode strategy: {materialDecode.StrategyName}.");
        diagnostics.Add($"Material coverage tier: {materialDecode.CoverageTier}.");
        diagnostics.AddRange(materialDecode.Notes);

        foreach (var reference in matd.TextureReferences)
        {
            var resolvedSlot = Ts4ShaderSemantics.ResolveTextureSlotName(reference, shaderProfile);
            var samplingInstruction = materialDecode.SamplingInstructions
                .FirstOrDefault(instruction => instruction.Slot.Equals(resolvedSlot, StringComparison.OrdinalIgnoreCase));
            propertiesByHash.TryGetValue(reference.PropertyHash, out var sourceProperty);
            var isAuthoritativeReference = !reference.IsHeuristic &&
                (sourceProperty?.ValueRepresentation is null or MaterialValueRepresentation.ResourceKey);
            var explicitKeyExistsInIndex = false;
            var cacheKey = $"{reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}";
            var hadCachedEntry = textureCache.TryGetValue(cacheKey, out var cachedTexture);
            if (!hadCachedEntry || (cachedTexture is null && isAuthoritativeReference))
            {
                var coreKey = new ResourceKeyRecord(
                    reference.Key.Type,
                    reference.Key.Group,
                    reference.Key.Instance,
                    GuessTypeName(reference.Key.Type));
                cachedTexture = await TryResolveTextureAsync(
                    ownerResource,
                    coreKey,
                    resolvedSlot,
                    textureCache,
                    cancellationToken).ConfigureAwait(false);
                if (cachedTexture is null)
                {
                    explicitKeyExistsInIndex = (await GetTextureResourcesByKeyAsync(coreKey, cancellationToken).ConfigureAwait(false)).Count > 0;
                }

                textureCache[cacheKey] = cachedTexture;
            }

            if (cachedTexture is null)
            {
                if (isAuthoritativeReference)
                {
                    diagnostics.Add(explicitKeyExistsInIndex
                        ? $"Texture decode failed or resource was unavailable for slot {resolvedSlot} ({reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16})."
                        : $"Texture binding for slot {resolvedSlot} points to an image-like key that was not found in the indexed game data ({reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}).");
                }
                else
                {
                    if (!authoritativeSlots.Contains(resolvedSlot) &&
                        !textures.Any(texture => texture.Slot.Equals(resolvedSlot, StringComparison.OrdinalIgnoreCase)))
                    {
                        diagnostics.Add($"Ignored unresolved heuristic texture binding for slot {resolvedSlot} ({reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}).");
                    }
                }
            }
            else
            {
                textures.Add(cachedTexture with
                {
                    Slot = resolvedSlot,
                    Semantic = ClassifyTextureSemantic(resolvedSlot),
                    UvChannel = samplingInstruction?.UvChannel ?? cachedTexture.UvChannel,
                    UvScaleU = samplingInstruction?.UvScaleU ?? cachedTexture.UvScaleU,
                    UvScaleV = samplingInstruction?.UvScaleV ?? cachedTexture.UvScaleV,
                    UvOffsetU = samplingInstruction?.UvOffsetU ?? cachedTexture.UvOffsetU,
                    UvOffsetV = samplingInstruction?.UvOffsetV ?? cachedTexture.UvOffsetV
                });
            }
        }

        IReadOnlyList<CanonicalTexture>? fallbackTextures = null;
        if (ShouldAddFallbackBaseColor(textures))
        {
            fallbackTextures = await ResolveFallbackTexturesAsync(fallbackTextureOwnerResource, textureCache, cancellationToken).ConfigureAwait(false);
            var fallbackBaseColor = fallbackTextures
                .FirstOrDefault(static texture => texture.Semantic == CanonicalTextureSemantic.BaseColor);
            if (fallbackBaseColor is not null &&
                !HasMatchingBaseColorTexture(textures, fallbackBaseColor))
            {
                textures.Add(fallbackBaseColor with
                {
                    Slot = "diffuse",
                    Semantic = CanonicalTextureSemantic.BaseColor
                });
                diagnostics.Add("No portable color texture payload was resolved; using an exact-instance texture candidate as a portable diffuse approximation.");
            }
        }

        if (textures.Count == 0)
        {
            fallbackTextures ??= await ResolveFallbackTexturesAsync(fallbackTextureOwnerResource, textureCache, cancellationToken).ConfigureAwait(false);
            var approximatedTextures = fallbackTextures;
            if (approximatedTextures.Count > 0)
            {
                textures.AddRange(approximatedTextures);
                diagnostics.Add("No explicit MATD texture references were resolved; using exact-instance texture candidates as a portable diffuse approximation.");
            }
        }

        if (!textures.Any(static texture => IsVisualColorTextureSlot(texture.Slot)) &&
            materialDecode.ShaderFamilyName.Contains("SimWingsUV", StringComparison.OrdinalIgnoreCase))
        {
            var promotedUtilityTexture = textures.FirstOrDefault(static texture => texture.Slot.Equals("specular", StringComparison.OrdinalIgnoreCase));
            if (promotedUtilityTexture is not null &&
                !textures.Any(static texture => texture.Slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase)))
            {
                textures.Add(promotedUtilityTexture with
                {
                    Slot = "diffuse",
                    Semantic = CanonicalTextureSemantic.BaseColor
                });
                diagnostics.Add("SimWingsUV exposed only a specular-like texture; preview promotes it to a diffuse approximation for a portable color preview.");
            }
        }

        if (effectiveSamplingInstructions.Count == 0 && textures.Count > 0)
        {
            foreach (var slot in textures
                .Select(static texture => texture.Slot)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase))
            {
                effectiveSamplingInstructions.Add(
                    Ts4MaterialDecoder.CreateSyntheticSamplingInstruction(
                        materialIr,
                        slot,
                        materialDecode.DiffuseUvMapping,
                        materialDecode.ShaderProfileName,
                        materialDecode.ShaderFamilyName,
                        materialDecode.CoverageTier,
                        SelectPreferredSyntheticSamplingTemplate(materialDecode.SamplingInstructions, slot)));
            }
        }

        var resolvedTextureSlots = new HashSet<string>(textures.Select(static texture => texture.Slot), StringComparer.OrdinalIgnoreCase);
        var effectiveResolvedSamplingInstructions = effectiveSamplingInstructions
            .Where(instruction => resolvedTextureSlots.Contains(instruction.Slot))
            .ToList();

        foreach (var resolvedSlot in resolvedTextureSlots.OrderBy(static slot => slot, StringComparer.OrdinalIgnoreCase))
        {
            if (effectiveResolvedSamplingInstructions.Any(instruction => instruction.Slot.Equals(resolvedSlot, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            effectiveResolvedSamplingInstructions.Add(
                Ts4MaterialDecoder.CreateSyntheticSamplingInstruction(
                    materialIr,
                    resolvedSlot,
                    materialDecode.DiffuseUvMapping,
                    materialDecode.ShaderProfileName,
                    materialDecode.ShaderFamilyName,
                    materialDecode.CoverageTier,
                    SelectPreferredSyntheticSamplingTemplate(materialDecode.SamplingInstructions, resolvedSlot)));
        }

        foreach (var texture in textures)
        {
            diagnostics.Add(
                $"Texture {DescribeTextureSemantic(texture.Semantic)} ({texture.Slot}) resolved via {DescribeTextureSourceKind(texture.SourceKind)}" +
                $"{FormatTextureSourceLocation(texture.SourcePackagePath)}.");
            if (texture.UvChannel != 0 ||
                Math.Abs(texture.UvScaleU - 1f) > 0.0001f ||
                Math.Abs(texture.UvScaleV - 1f) > 0.0001f ||
                Math.Abs(texture.UvOffsetU) > 0.0001f ||
                Math.Abs(texture.UvOffsetV) > 0.0001f)
            {
                diagnostics.Add(
                    FormattableString.Invariant(
                        $"Texture {DescribeTextureSemantic(texture.Semantic)} ({texture.Slot}) uses UV{texture.UvChannel} with transform scale=({texture.UvScaleU:0.###}, {texture.UvScaleV:0.###}) offset=({texture.UvOffsetU:0.###}, {texture.UvOffsetV:0.###})."));
            }
        }

        var distinctTextureTransforms = textures
            .Select(static texture => $"{texture.UvChannel}|{texture.UvScaleU:0.####}|{texture.UvScaleV:0.####}|{texture.UvOffsetU:0.####}|{texture.UvOffsetV:0.####}")
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctTextureTransforms > 1)
        {
            diagnostics.Add("Material exposes divergent slot UV transforms; current viewport approximates the material with a single primary texture transform.");
        }

        if (shaderProfile is not null)
        {
            var matchedProperties = materialIr.Properties
                .Where(static property => !property.Name.StartsWith("Prop_", StringComparison.Ordinal))
                .Select(static property => $"{property.Name}:{property.Category}/{property.ValueRepresentation}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase)
                .Take(16)
                .ToArray();
            if (matchedProperties.Length > 0)
            {
                diagnostics.Add($"Shader-derived MATD semantics: {string.Join(", ", matchedProperties)}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(materialDecode.AlphaSourceSlot))
        {
            diagnostics.Add($"Material alpha source slot: {materialDecode.AlphaSourceSlot}.");
        }

        if (materialDecode.LayeredColorSlots.Count > 0)
        {
            diagnostics.Add($"Material layered slots: {string.Join(", ", materialDecode.LayeredColorSlots)}.");
        }

        if (materialDecode.UtilityTextureSlots.Count > 0)
        {
            diagnostics.Add($"Material utility slots: {string.Join(", ", materialDecode.UtilityTextureSlots)}.");
        }

        var effectiveApproximateBaseColor = materialDecode.ApproximateBaseColor is { Length: >= 3 }
            ? materialDecode.ApproximateBaseColor.ToArray()
            : null;
        var hasPortableAlphaPayload =
            !string.IsNullOrWhiteSpace(materialDecode.AlphaSourceSlot) ||
            textures.Any(static texture => texture.Slot is "alpha" or "opacity" or "mask" or "cutout");
        if (effectiveApproximateBaseColor is { Length: >= 4 } && textures.Count == 0 && !hasPortableAlphaPayload)
        {
            effectiveApproximateBaseColor[3] = 1f;
        }

        if (effectiveApproximateBaseColor is { Length: >= 3 } approximateBaseColor)
        {
            diagnostics.Add(FormattableString.Invariant(
                $"Material approximate base color: rgba=({approximateBaseColor[0]:0.###}, {approximateBaseColor[1]:0.###}, {approximateBaseColor[2]:0.###}, {(approximateBaseColor.Length >= 4 ? approximateBaseColor[3] : 1f):0.###})."));
        }

        var isTransparent = matd.IsTransparent ||
            matd.TextureReferences.Any(static texture => texture.Slot is "alpha" or "opacity" or "mask" or "cutout") ||
            !string.IsNullOrWhiteSpace(materialDecode.AlphaSourceSlot) ||
            (materialDecode.SuggestsAlphaCutout && (textures.Count > 0 || hasPortableAlphaPayload));
        var usedFallbackTextureApproximation = textures.Count > 0 && matd.TextureReferences.All(static reference => reference.IsHeuristic);
        var hasPortableVisualTexturePayload = textures.Any(static texture => IsVisualColorTextureSlot(texture.Slot));
        var hasOnlyUtilityTexturePayload = textures.Count > 0 && !hasPortableVisualTexturePayload;
        var usedPortableColorApproximation = !hasPortableVisualTexturePayload && effectiveApproximateBaseColor is { Length: >= 3 };
        var lacksPortableVisualPayload = !hasPortableVisualTexturePayload && effectiveApproximateBaseColor is null;
        if (usedPortableColorApproximation && effectiveResolvedSamplingInstructions.Count == 0)
        {
            effectiveResolvedSamplingInstructions.Add(
                Ts4MaterialDecoder.CreateSyntheticSamplingInstruction(
                    materialIr,
                    "material-color",
                    materialDecode.DiffuseUvMapping,
                    materialDecode.ShaderProfileName,
                    materialDecode.ShaderFamilyName,
                    materialDecode.CoverageTier,
                    SelectPreferredSyntheticSamplingTemplate(materialDecode.SamplingInstructions, "diffuse")));
        }

        if (effectiveResolvedSamplingInstructions.Count > 0)
        {
            diagnostics.Add(
                $"Material sampling instructions: {string.Join(", ", effectiveResolvedSamplingInstructions.Select(static instruction => FormattableString.Invariant($"{instruction.Slot}->UV{instruction.UvChannel} scale=({instruction.UvScaleU:0.###},{instruction.UvScaleV:0.###}) offset=({instruction.UvOffsetU:0.###},{instruction.UvOffsetV:0.###}) [{instruction.Source}]")))}.");
        }

        var behavesLikeNonVisualMaterial = textures.Count == 0 &&
            materialDecode.ApproximateBaseColor is null &&
            (matd.Properties.Count == 0 ||
             LooksLikeNonVisualHelperMaterial(materialDecode, matd.ShaderName));
        var visualPayloadKind = hasPortableVisualTexturePayload
            ? "textured"
            : usedPortableColorApproximation
                ? "material-color"
                : behavesLikeNonVisualMaterial
                    ? "non-visual"
                    : "neutral";
        if (usedPortableColorApproximation)
        {
            diagnostics.Add(hasOnlyUtilityTexturePayload
                ? "Only utility texture payloads were resolved; preview derives visible color from an approximate material color."
                : "No portable texture payload was resolved; preview falls back to an approximate material color derived from shader properties.");
        }
        else if (behavesLikeNonVisualMaterial)
        {
            diagnostics.Add("The selected material state behaves like a non-visual helper/control material; preview keeps a neutral approximation for scene completeness.");
        }
        else if (lacksPortableVisualPayload)
        {
            diagnostics.Add("The selected material state did not expose a portable texture or color payload for preview; the viewport will fall back to a neutral material approximation.");
        }

        var approximation = usedFallbackTextureApproximation
            ? "Maxis shaders are approximated to a portable texture-set export."
            : usedPortableColorApproximation
                ? "Maxis shaders are approximated to a portable material-color preview."
                : behavesLikeNonVisualMaterial
                    ? "The selected material state behaves like a non-visual helper/control material; preview keeps a neutral approximation for scene completeness."
                : lacksPortableVisualPayload
                    ? "The selected material state exposes no portable texture or color payload; preview falls back to a neutral approximation."
                    : null;

        return new Ts4MaterialInfo(
            !string.IsNullOrWhiteSpace(matd.MaterialName) ? matd.MaterialName : $"Material_{matd.MaterialNameHash:X8}",
            matd.ShaderName,
            matd.UvScales,
            textures,
            diagnostics,
            materialDecode.ShaderFamilyName,
            materialDecode.StrategyName,
            materialDecode.CoverageTier,
            effectiveResolvedSamplingInstructions
                .Select(static instruction => instruction.Source)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static source => source, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            effectiveApproximateBaseColor,
            isTransparent,
            isTransparent ? materialDecode.AlphaModeHint ?? "alpha-test-or-blend" : "opaque",
            materialDecode.AlphaSourceSlot,
            materialDecode.LayeredColorSlots,
            visualPayloadKind,
            approximation,
            usedFallbackTextureApproximation || usedPortableColorApproximation ? CanonicalMaterialSourceKind.FallbackCandidate : sourceKind);
    }

    internal static bool LooksLikeNonVisualHelperMaterial(Ts4MaterialDecodeResult materialDecode, string shaderName)
    {
        if (LooksLikeNonVisualHelperFamily(materialDecode.ShaderFamilyName, shaderName))
        {
            return true;
        }

        if (materialDecode.LayeredColorSlots.Count == 0 &&
            (materialDecode.ShaderFamilyName.Contains("painting", StringComparison.OrdinalIgnoreCase) ||
             materialDecode.ShaderFamilyName.Contains("Decal", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (materialDecode.LayeredColorSlots.Count == 0 &&
            materialDecode.UtilityTextureSlots.Count > 0 &&
            materialDecode.ShaderFamilyName.Contains("Decal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static Ts4MaterialSamplingInstruction? SelectPreferredSyntheticSamplingTemplate(
        IReadOnlyList<Ts4MaterialSamplingInstruction> instructions,
        string slot)
    {
        if (instructions.Count == 0)
        {
            return null;
        }

        return instructions.FirstOrDefault(instruction => instruction.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)) ??
               instructions.FirstOrDefault(instruction => instruction.Slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase)) ??
               instructions[0];
    }

    internal static bool IsVisualColorTextureSlot(string slot) =>
        slot.Equals("diffuse", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("overlay", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("emissive", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("detail", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("decal", StringComparison.OrdinalIgnoreCase) ||
        slot.Equals("dirt", StringComparison.OrdinalIgnoreCase);

    internal static bool ShouldAddFallbackBaseColor(IReadOnlyList<CanonicalTexture> textures) =>
        !textures.Any(static texture => IsVisualColorTextureSlot(texture.Slot));

    internal static bool HasMatchingBaseColorTexture(IReadOnlyList<CanonicalTexture> textures, CanonicalTexture fallbackBaseColor) =>
        textures.Any(texture =>
            texture.Semantic == CanonicalTextureSemantic.BaseColor &&
            texture.SourceKey is not null &&
            texture.SourceKey.Equals(fallbackBaseColor.SourceKey));

    internal static bool LooksLikeNonVisualHelperFamily(string? shaderFamilyName, string shaderName)
    {
        static bool Matches(string? value) =>
            !string.IsNullOrWhiteSpace(value) &&
            (value.Contains("PointLight", StringComparison.OrdinalIgnoreCase) ||
             value.Contains("PeltEdit", StringComparison.OrdinalIgnoreCase));

        return Matches(shaderFamilyName) || Matches(shaderName);
    }

    private async Task<IReadOnlyList<CanonicalTexture>> ResolveFallbackTexturesAsync(
        ResourceMetadata ownerResource,
        Dictionary<string, CanonicalTexture?> textureCache,
        CancellationToken cancellationToken)
    {
        var packageResources = await GetPackageInstanceResourcesAsync(ownerResource.PackagePath, ownerResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
        var textureCandidates = packageResources
            .Where(resource => resource.Key.FullInstance == ownerResource.Key.FullInstance && IsTextureType(resource.Key.TypeName))
            .OrderBy(static resource => resource.Key.TypeName == "BuyBuildThumbnail" ? 1 : 0)
            .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal)
            .ToList();

        if (textureCandidates.Count == 0)
        {
            foreach (var companionPackagePath in EnumerateCompanionPackagePaths(ownerResource.PackagePath))
            {
                var companionResources = await GetPackageInstanceResourcesAsync(companionPackagePath, ownerResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
                foreach (var candidate in companionResources
                    .Where(resource => resource.Key.FullInstance == ownerResource.Key.FullInstance && IsTextureType(resource.Key.TypeName))
                    .OrderBy(static resource => resource.Key.TypeName == "BuyBuildThumbnail" ? 1 : 0)
                    .ThenBy(static resource => resource.Key.TypeName, StringComparer.Ordinal))
                {
                    if (textureCandidates.Any(existing => existing.Key.FullTgi.Equals(candidate.Key.FullTgi, StringComparison.OrdinalIgnoreCase) &&
                                                          existing.PackagePath.Equals(candidate.PackagePath, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    textureCandidates.Add(candidate);
                }
            }
        }

        if (textureCandidates.Count == 0)
        {
            var globalTextureCandidates = await GetTextureResourcesByInstanceAsync(ownerResource.Key.FullInstance, cancellationToken).ConfigureAwait(false);
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
                var fallbackSlot = results.Count == 0 ? "diffuse" : $"extra_{results.Count}";
                results.Add(cachedTexture with
                {
                    Slot = fallbackSlot,
                    Semantic = results.Count == 0 ? CanonicalTextureSemantic.BaseColor : CanonicalTextureSemantic.Unknown,
                    SourceKind = textureCandidates.Count == 1 && candidate.PackagePath.Equals(ownerResource.PackagePath, StringComparison.OrdinalIgnoreCase)
                        ? CanonicalTextureSourceKind.FallbackSameInstanceLocal
                        : CanonicalTextureSourceKind.FallbackSameInstanceIndexed
                });
            }
        }

        if (results.Count == 0)
        {
            foreach (var globalClientPackagePath in EnumerateGlobalClientPackagePaths(ownerResource.PackagePath))
            {
                foreach (var imageType in FallbackImageTypes)
                {
                    var key = new ResourceKeyRecord(imageType, 0u, ownerResource.Key.FullInstance, GuessTypeName(imageType));
                    var cacheKey = $"{globalClientPackagePath}|{key.FullTgi}";
                    if (!textureCache.TryGetValue(cacheKey, out var cachedTexture))
                    {
                        cachedTexture = await TryDecodeTextureAsync(
                            globalClientPackagePath,
                            key,
                            results.Count == 0 ? "diffuse" : $"extra_{results.Count}",
                            results.Count == 0 ? CanonicalTextureSemantic.BaseColor : CanonicalTextureSemantic.Unknown,
                            CanonicalTextureSourceKind.FallbackSameInstanceIndexed,
                            cancellationToken).ConfigureAwait(false);
                        textureCache[cacheKey] = cachedTexture;
                    }

                    if (cachedTexture is null ||
                        results.Any(existing => existing.SourceKey is not null && existing.SourceKey.Equals(cachedTexture.SourceKey)))
                    {
                        continue;
                    }

                    results.Add(cachedTexture with
                    {
                        Slot = results.Count == 0 ? "diffuse" : $"extra_{results.Count}",
                        Semantic = results.Count == 0 ? CanonicalTextureSemantic.BaseColor : CanonicalTextureSemantic.Unknown,
                        SourceKind = CanonicalTextureSourceKind.FallbackSameInstanceIndexed
                    });
                }
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

        var matches = await GetTextureResourcesByKeyAsync(key, cancellationToken).ConfigureAwait(false);
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

        foreach (var globalClientPackagePath in EnumerateGlobalClientPackagePaths(ownerResource.PackagePath))
        {
            var globalClientTexture = await TryDecodeTextureAsync(globalClientPackagePath, key, slot, semantic, CanonicalTextureSourceKind.ExplicitIndexed, cancellationToken).ConfigureAwait(false);
            if (globalClientTexture is not null)
            {
                return globalClientTexture;
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

    private IEnumerable<string> EnumerateGlobalClientPackagePaths(string packagePath)
    {
        var packageDirectory = Path.GetDirectoryName(packagePath);
        var siblingCandidates = string.IsNullOrWhiteSpace(packageDirectory)
            ? []
            : siblingClientPackageCache.GetOrAdd(packageDirectory, static directory =>
            {
                if (!Directory.Exists(directory))
                {
                    return [];
                }

                return Directory.EnumerateFiles(directory, "ClientFullBuild*.package", SearchOption.TopDirectoryOnly)
                    .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            });

        var installRoot = TryResolveGameInstallRoot(packagePath);
        if (string.IsNullOrWhiteSpace(installRoot))
        {
            foreach (var candidate in MergeCrossPackageTextureLookupPaths(packagePath, siblingCandidates, []))
            {
                yield return candidate;
            }

            yield break;
        }

        var globalCandidates = globalClientPackageCache.GetOrAdd(installRoot, static root =>
        {
            var dataClientPath = Path.Combine(root, "Data", "Client");
            if (!Directory.Exists(dataClientPath))
            {
                return [];
            }

            return Directory.EnumerateFiles(dataClientPath, "ClientFullBuild*.package", SearchOption.TopDirectoryOnly)
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        });

        foreach (var candidate in MergeCrossPackageTextureLookupPaths(packagePath, siblingCandidates, globalCandidates))
        {
            yield return candidate;
        }
    }

    internal static IReadOnlyList<string> MergeCrossPackageTextureLookupPaths(
        string packagePath,
        IEnumerable<string> siblingCandidates,
        IEnumerable<string> globalCandidates)
    {
        var merged = new List<string>();
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in siblingCandidates.Concat(globalCandidates))
        {
            if (string.IsNullOrWhiteSpace(candidate) ||
                candidate.Equals(packagePath, StringComparison.OrdinalIgnoreCase) ||
                !yielded.Add(candidate))
            {
                continue;
            }

            merged.Add(candidate);
        }

        return merged;
    }

    private static string? TryResolveGameInstallRoot(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(packagePath)!);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Data")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
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
            var pngBytes = await GetTexturePngAsync(packagePath, key, cancellationToken).ConfigureAwait(false);
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

    private async Task<IReadOnlyList<ResourceMetadata>> FindTextureResourcesByKeyCoreAsync(TextureLookupKey lookupKey, CancellationToken cancellationToken)
    {
        var exactTypeResults = await QueryTextureResourcesByKeyAsync(lookupKey, lookupKey.TypeName, cancellationToken).ConfigureAwait(false);
        if (exactTypeResults.Count > 0)
        {
            return exactTypeResults;
        }

        // Some indexed game datasets disagree on the friendly image type name for the same numeric
        // texture type (for example DSTImage vs PNGImage). Retry without the type-name filter and
        // trust the numeric TGI match instead of the display name.
        return await QueryTextureResourcesByKeyAsync(lookupKey, string.Empty, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ResourceMetadata>> QueryTextureResourcesByKeyAsync(
        TextureLookupKey lookupKey,
        string typeNameText,
        CancellationToken cancellationToken)
    {
        var query = new RawResourceBrowserQuery(
            new SourceScope(),
            SearchText: string.Empty,
            Domain: RawResourceDomain.Images,
            TypeNameText: typeNameText,
            PackageText: string.Empty,
            GroupHexText: lookupKey.Group.ToString("X8"),
            InstanceHexText: lookupKey.FullInstance.ToString("X16"),
            PreviewableOnly: false,
            ExportCapableOnly: false,
            CompressedKnownOnly: false,
            LinkFilter: ResourceLinkFilter.Any,
            Sort: RawResourceSort.Tgi,
            Offset: 0,
            WindowSize: 64);

        var results = await indexStore.QueryResourcesAsync(query, cancellationToken).ConfigureAwait(false);
        return results.Items
            .Where(resource => resource.Key.Type == lookupKey.Type &&
                               resource.Key.Group == lookupKey.Group &&
                               resource.Key.FullInstance == lookupKey.FullInstance &&
                               IsTextureType(resource.Key.TypeName))
            .ToArray();
    }

    private async Task<IReadOnlyList<ResourceMetadata>> FindTextureResourcesByInstanceCoreAsync(ulong instance, CancellationToken cancellationToken)
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

    private static (float[] Uvs, int Channel) SelectPreferredUvCoordinates(
        IReadOnlyList<Ts4DecodedVertex> vertices,
        uint meshName,
        ICollection<string> diagnostics)
    {
        var uv0 = vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray();
        var uv1 = vertices.SelectMany(static vertex => vertex.Uv1 ?? []).ToArray();

        if (uv0.Length == 0)
        {
            return (uv1, uv1.Length > 0 ? 1 : 0);
        }

        if (uv1.Length == 0 || uv1.Length != uv0.Length)
        {
            return (uv0, 0);
        }

        var uv0Area = EstimateUvCoverageArea(uv0);
        var uv1Area = EstimateUvCoverageArea(uv1);
        // Some Maxis materials intentionally pack a small but valid atlas island into UV0.
        // Be conservative about switching to UV1, or banner/decal meshes can drift badly.
        if (uv0Area > 0f && uv0Area < 0.002f && uv1Area > uv0Area * 8f)
        {
            diagnostics.Add($"Mesh 0x{meshName:X8} uses UV1 for preview because UV0 covers only a tiny atlas region ({uv0Area:0.####}) while UV1 covers {uv1Area:0.####}.");
            return (uv1, 1);
        }

        return (uv0, 0);
    }

    private static float EstimateUvCoverageArea(float[] uvs)
    {
        if (uvs.Length < 2)
        {
            return 0f;
        }

        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;

        for (var index = 0; index + 1 < uvs.Length; index += 2)
        {
            var u = uvs[index];
            var v = uvs[index + 1];
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }

        if (!float.IsFinite(minU) || !float.IsFinite(maxU) || !float.IsFinite(minV) || !float.IsFinite(maxV))
        {
            return 0f;
        }

        return Math.Max(0f, maxU - minU) * Math.Max(0f, maxV - minV);
    }

}

public static class PreviewDebugProbe
{
    public static IReadOnlyList<ModlEntryDebug> ParseModl(byte[] bytes)
    {
        var rcol = Ts4RcolResource.Parse(bytes);
        var modlChunk = rcol.Chunks.FirstOrDefault(static chunk => chunk.Tag == "MODL");
        if (modlChunk is null)
        {
            return Array.Empty<ModlEntryDebug>();
        }

        var modl = Ts4ModlChunk.Parse(modlChunk.Data.Span);
        return modl.LodEntries
            .Select(static entry => new ModlEntryDebug(
                entry.Reference.Raw,
                entry.Reference.ReferenceType.ToString(),
                entry.Reference.Index,
                entry.Flags,
                entry.Id,
                entry.MinZ,
                entry.MaxZ))
            .ToArray();
    }

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
                    $"Mesh name=0x{mesh.Name:X8} primitive={mesh.PrimitiveType} flags=0x{mesh.Flags:X} vbuf={mesh.VertexBufferReference.Raw:X8} ibuf={mesh.IndexBufferReference.Raw:X8} vrtf={mesh.VertexFormatReference.Raw:X8} streamOffset={mesh.StreamOffset} startVertex={mesh.StartVertex} startIndex={mesh.StartIndex} minVertexIndex={mesh.MinVertexIndex} verts={mesh.VertexCount} prims={mesh.PrimitiveCount}");

                var ibufChunk = rcol.ResolveChunk(mesh.IndexBufferReference);
                if (ibufChunk is not null)
                {
                    var ibuf = Ts4IbufChunk.Parse(ibufChunk.Data.Span);
                    foreach (var line in ibuf.InspectCandidates(mesh.StartIndex, mesh.PrimitiveCount * 3, mesh.MinVertexIndex, mesh.VertexCount))
                    {
                        lines.Add($"  IBUF {line}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lines.Add($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            lines.Add(ex.StackTrace ?? "(no stack)");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string InspectMaterialChunks(byte[] bytes)
    {
        var lines = new List<string>();
        try
        {
            var rcol = Ts4RcolResource.Parse(bytes);
            lines.Add($"RCOL parsed: public={rcol.PublicChunkCount} chunks={rcol.Chunks.Count} external={rcol.ExternalResources.Count}");
            foreach (var chunk in rcol.Chunks.Where(static chunk => chunk.Tag is "MATD" or "MTST"))
            {
                lines.Add($"Chunk {chunk.Tag} {chunk.Key.Type:X8}:{chunk.Key.Group:X8}:{chunk.Key.Instance:X16} len={chunk.Length}");
                if (chunk.Tag == "MATD")
                {
                    foreach (var line in Ts4MatdChunk.InspectProperties(chunk.Data.Span))
                    {
                        lines.Add($"  {line}");
                    }
                }
                else if (chunk.Tag == "MTST")
                {
                    var mtst = Ts4MtstChunk.Parse(chunk.Data.Span);
                    foreach (var line in Ts4MtstChunk.InspectReferences(chunk.Data.Span))
                    {
                        lines.Add($"  {line}");
                    }

                    for (var entryIndex = 0; entryIndex < mtst.Entries.Count; entryIndex++)
                    {
                        var entry = mtst.Entries[entryIndex];
                        if (entry.Reference.IsNull)
                        {
                            continue;
                        }

                        var resolvedChunk = rcol.ResolveChunk(entry.Reference);
                        if (resolvedChunk is null)
                        {
                            lines.Add($"  state[{entryIndex}] stateHash=0x{entry.StateNameHash:X8} resolvedChunk=(null)");
                            continue;
                        }

                        lines.Add($"  state[{entryIndex}] stateHash=0x{entry.StateNameHash:X8} -> {resolvedChunk.Tag} {resolvedChunk.Key.Type:X8}:{resolvedChunk.Key.Group:X8}:{resolvedChunk.Key.Instance:X16}");
                        if (resolvedChunk.Tag == "MATD")
                        {
                            foreach (var line in Ts4MatdChunk.InspectProperties(resolvedChunk.Data.Span))
                            {
                                lines.Add($"    {line}");
                            }
                        }
                    }
                }
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

public readonly record struct ModlEntryDebug(
    uint ReferenceRaw,
    string ReferenceType,
    int ReferenceIndex,
    uint Flags,
    uint Id,
    float MinZ,
    float MaxZ);

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
    string? ShaderFamily,
    string DecodeStrategy,
    Ts4MaterialCoverageTier CoverageTier,
    IReadOnlyList<string> SamplingSources,
    float[]? ApproximateBaseColor,
    bool IsTransparent,
    string AlphaMode,
    string? AlphaTextureSlot,
    IReadOnlyList<string> LayeredTextureSlots,
    string VisualPayloadKind,
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
            null,
            "UnsupportedMaterial",
            Ts4MaterialCoverageTier.RuntimeDependent,
            ["unsupported-material"],
            null,
            false,
            "opaque",
            null,
            [],
            "unsupported",
            "Material fell back to a placeholder because the source material chunk was unsupported.",
            CanonicalMaterialSourceKind.Unsupported);
}

internal readonly record struct Ts4TextureReference(string Slot, Ts4ResourceKey Key, uint PropertyHash, bool IsHeuristic = false)
{
    public Ts4TextureReference(string Slot, Ts4ResourceKey Key, uint PropertyHash)
        : this(Slot, Key, PropertyHash, false)
    {
    }
}

internal readonly record struct Ts4TextureUvMapping(
    int UvChannel = 0,
    float UvScaleU = 1f,
    float UvScaleV = 1f,
    float UvOffsetU = 0f,
    float UvOffsetV = 0f)
{
    public bool IsIdentity =>
        UvChannel == 0 &&
        Math.Abs(UvScaleU - 1f) < 0.0001f &&
        Math.Abs(UvScaleV - 1f) < 0.0001f &&
        Math.Abs(UvOffsetU) < 0.0001f &&
        Math.Abs(UvOffsetV) < 0.0001f;
}

internal sealed record Ts4MatdProperty(
    uint Hash,
    uint RawType,
    uint Type,
    uint Arity,
    int Offset,
    ShaderParameterCategory Category,
    MaterialValueRepresentation ValueRepresentation,
    string? ValueSummary,
    float[]? FloatValues,
    uint[]? PackedUInt32Values,
    Ts4ResourceKey? ResourceKeyValue);

internal sealed record Ts4MatdDecodedValue(
    MaterialValueRepresentation Representation,
    string? Summary,
    float[]? FloatValues = null,
    uint[]? PackedUInt32Values = null,
    Ts4ResourceKey? ResourceKey = null);

internal sealed record Ts4MatdChunk(
    string MaterialName,
    uint MaterialNameHash,
    uint ShaderNameHash,
    string ShaderName,
    float[] UvScales,
    IReadOnlyList<Ts4TextureReference> TextureReferences,
    bool IsTransparent,
    Ts4TextureUvMapping DiffuseUvMapping,
    IReadOnlyList<Ts4MatdProperty> Properties)
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
        var properties = new List<Ts4MatdProperty>();
        var uvScales = new[] { 1f, 1f, 1f };
        var diffuseUvMapping = new Ts4TextureUvMapping(0, 1f, 1f, 0f, 0f);
        Ts4ShaderProfileRegistry.Instance.TryGetProfile(shaderNameHash, out var shaderProfile);

        var mtrlOffset = bytes.IndexOf("MTRL"u8);
        if (mtrlOffset < 0 || mtrlOffset + 16 > bytes.Length)
        {
            return new Ts4MatdChunk(
                $"Material_{materialNameHash:X8}",
                materialNameHash,
                shaderNameHash,
                $"Shader_{shaderNameHash:X8}",
                uvScales,
                textureReferences,
                false,
                diffuseUvMapping,
                properties);
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
            var profileParameter = Ts4ShaderSemantics.ResolveParameterProfile(propertyHash, shaderProfile);
            var propertyCategory = profileParameter?.Category ?? Ts4ShaderSemantics.ClassifyMatdProperty(normalizedPropertyType, propertyArity);
            var decodedValue = DecodeMatdValue(normalizedPropertyType, propertyArity, propertyOffset, bytes);
            properties.Add(new Ts4MatdProperty(
                propertyHash,
                propertyType,
                normalizedPropertyType,
                propertyArity,
                propertyOffset,
                propertyCategory,
                decodedValue.Representation,
                decodedValue.Summary,
                decodedValue.FloatValues,
                decodedValue.PackedUInt32Values,
                decodedValue.ResourceKey));
            if (propertyOffset < 0 || propertyOffset >= bytes.Length)
            {
                continue;
            }

            if (TryReadTextureReference(propertyHash, normalizedPropertyType, propertyArity, propertyOffset, bytes, textureReferences, profileParameter, shaderProfile))
            {
                continue;
            }

            if (normalizedPropertyType == 1)
            {
                TryApplyScalarProperty(shaderNameHash, propertyHash, profileParameter, propertyOffset, bytes, uvScales, ref diffuseUvMapping);
                continue;
            }

            if (normalizedPropertyType == 4 || (normalizedPropertyType == 1 && propertyArity > 1))
            {
                TryApplyVectorProperty(shaderNameHash, propertyHash, profileParameter, propertyOffset, propertyArity, bytes, uvScales, ref diffuseUvMapping);
            }
        }

        tableOffset = mtrlOffset + 16;
        for (var i = 0; i < propertyCount && tableOffset + 16 <= bytes.Length; i++, tableOffset += 16)
        {
            var propertyHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset, 4));
            var propertyType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 4, 4));
            var normalizedPropertyType = propertyType & 0xFFFF;
            var propertyArity = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 8, 4));
            var propertyOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 12, 4)));
            var profileParameter = Ts4ShaderSemantics.ResolveParameterProfile(propertyHash, shaderProfile);
            if (propertyOffset < 0 || propertyOffset >= bytes.Length)
            {
                continue;
            }

            TryReadHeuristicTextureReference(propertyHash, normalizedPropertyType, propertyArity, propertyOffset, bytes, textureReferences, profileParameter, shaderProfile);
        }

        return new Ts4MatdChunk(
            $"Material_{materialNameHash:X8}",
            materialNameHash,
            shaderNameHash,
            $"Shader_{shaderNameHash:X8}",
            uvScales,
            textureReferences
                .GroupBy(static reference => $"{reference.Key.Type:X8}:{reference.Key.Group:X8}:{reference.Key.Instance:X16}", StringComparer.OrdinalIgnoreCase)
                .Select(static group => group.First())
                .ToArray(),
            false,
            diffuseUvMapping,
            properties);
    }

    private static Ts4MatdChunk CreateFallback(uint materialNameHash) =>
        new(
            $"Material_{materialNameHash:X8}",
            materialNameHash,
            0,
            "Unsupported",
            [1f, 1f, 1f],
            [],
            false,
            new Ts4TextureUvMapping(0, 1f, 1f, 0f, 0f),
            []);

    public static IReadOnlyList<string> InspectProperties(ReadOnlySpan<byte> bytes)
    {
        var lines = new List<string>();
        if (bytes.Length < 16 || !bytes[..4].SequenceEqual("MATD"u8))
        {
            lines.Add("Not a MATD chunk.");
            return lines;
        }

        var materialNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var shaderNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        lines.Add($"material=Material_{materialNameHash:X8} shader=Shader_{shaderNameHash:X8}");
        Ts4ShaderProfileRegistry.Instance.TryGetProfile(shaderNameHash, out var shaderProfile);
        if (shaderProfile is not null)
        {
            lines.Add($"shaderProfile={shaderProfile.Name} params={shaderProfile.Parameters.Count}");
        }

        var mtrlOffset = bytes.IndexOf("MTRL"u8);
        if (mtrlOffset < 0 || mtrlOffset + 16 > bytes.Length)
        {
            lines.Add("No MTRL property table found.");
            return lines;
        }

        var propertyCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(mtrlOffset + 12, 4)));
        lines.Add($"propertyCount={propertyCount}");
        var tableOffset = mtrlOffset + 16;
        for (var i = 0; i < propertyCount && tableOffset + 16 <= bytes.Length; i++, tableOffset += 16)
        {
            var propertyHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset, 4));
            var propertyType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 4, 4));
            var normalizedPropertyType = propertyType & 0xFFFF;
            var propertyArity = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 8, 4));
            var propertyOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(tableOffset + 12, 4)));
            var profileParameter = Ts4ShaderSemantics.ResolveParameterProfile(propertyHash, shaderProfile);
            var propertyName = profileParameter?.Name ?? $"0x{propertyHash:X8}";
            var propertyCategory = profileParameter?.Category ?? Ts4ShaderSemantics.ClassifyMatdProperty(normalizedPropertyType, propertyArity);
            var summary = $"[{i}] hash=0x{propertyHash:X8} name={propertyName} category={propertyCategory} type=0x{normalizedPropertyType:X4} arity={propertyArity} offset=0x{propertyOffset:X}";
            if (propertyOffset < 0 || propertyOffset >= bytes.Length)
            {
                lines.Add(summary + " invalid-offset");
                continue;
            }

            lines.Add(summary + DescribePropertyValue(normalizedPropertyType, propertyArity, propertyOffset, bytes));
        }

        return lines;
    }

    private static string DescribePropertyValue(uint normalizedPropertyType, uint propertyArity, int propertyOffset, ReadOnlySpan<byte> bytes)
    {
        try
        {
            var decoded = DecodeMatdValue(normalizedPropertyType, propertyArity, propertyOffset, bytes);
            return decoded.Summary is null ? string.Empty : $" {decoded.Summary}";
        }
        catch
        {
            return " value=(decode-failed)";
        }
    }

    private static Ts4MatdDecodedValue DecodeMatdValue(
        uint normalizedPropertyType,
        uint propertyArity,
        int propertyOffset,
        byte[] bytes) =>
        DecodeMatdValue(normalizedPropertyType, propertyArity, propertyOffset, bytes.AsSpan());

    private static Ts4MatdDecodedValue DecodeMatdValue(
        uint normalizedPropertyType,
        uint propertyArity,
        int propertyOffset,
        ReadOnlySpan<byte> bytes)
    {
        if (propertyOffset < 0 || propertyOffset >= bytes.Length)
        {
            return new Ts4MatdDecodedValue(MaterialValueRepresentation.None, null);
        }

        if (normalizedPropertyType == 1 && propertyArity == 1 && propertyOffset + 4 <= bytes.Length)
        {
            var scalar = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset, 4));
            if (!IsPlausibleMaterialScalar(scalar))
            {
                var raw = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(propertyOffset, 4));
                return new Ts4MatdDecodedValue(
                    MaterialValueRepresentation.PackedUInt32,
                    $"packed32=[0x{raw:X8}]",
                    PackedUInt32Values: [raw]);
            }

            return new Ts4MatdDecodedValue(
                MaterialValueRepresentation.Scalar,
                $"scalar={scalar.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)}",
                [scalar]);
        }

        if (normalizedPropertyType == 2 && propertyOffset + 16 <= bytes.Length)
        {
            var keyBytes = bytes.Slice(propertyOffset, 16);
            if (LooksLikePackedControlPayload(keyBytes))
            {
                var packedValues = new[]
                {
                    BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(0, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(4, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(8, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(12, 4))
                };
                return new Ts4MatdDecodedValue(
                    MaterialValueRepresentation.PackedUInt32,
                    $"packed32=[{string.Join(", ", packedValues.Select(static value => $"0x{value:X8}"))}]",
                    PackedUInt32Values: packedValues);
            }

            var embeddedKey = ReadMatdEmbeddedResourceKey(keyBytes);
            return new Ts4MatdDecodedValue(
                MaterialValueRepresentation.ResourceKey,
                DescribePossibleKey(keyBytes).TrimStart(),
                ResourceKey: embeddedKey);
        }

        if (normalizedPropertyType == 4 || (normalizedPropertyType == 1 && propertyArity > 1))
        {
            if (TryReadFloatComponents(propertyArity, propertyOffset, bytes, out var floatValues))
            {
                var values = floatValues
                    .Select(static value => value.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                return new Ts4MatdDecodedValue(
                    MaterialValueRepresentation.FloatVector,
                    $"vector=[{string.Join(", ", values)}]",
                    floatValues);
            }

            if (TryReadUInt32Components(propertyArity, propertyOffset, bytes, out var rawValues))
            {
                return new Ts4MatdDecodedValue(
                    MaterialValueRepresentation.PackedUInt32,
                    $"packed32=[{string.Join(", ", rawValues.Select(static value => $"0x{value:X8}"))}]",
                    PackedUInt32Values: rawValues);
            }
        }

        return new Ts4MatdDecodedValue(MaterialValueRepresentation.None, null);
    }

    private static bool LooksLikePackedControlPayload(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            return false;
        }

        var plausibleScalarCount = 0;
        var normalizedScalarCount = 0;
        for (var index = 0; index < 4; index++)
        {
            var scalar = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(index * 4, 4));
            if (!IsPlausibleMaterialScalar(scalar))
            {
                continue;
            }

            plausibleScalarCount++;
            if (Math.Abs(scalar) <= 64f)
            {
                normalizedScalarCount++;
            }
        }

        return plausibleScalarCount >= 3 && normalizedScalarCount >= 2;
    }

    private static bool IsPlausibleMaterialScalar(float scalar) =>
        float.IsFinite(scalar) &&
        !float.IsNaN(scalar) &&
        Math.Abs(scalar) <= 10000f;

    private static string DescribePossibleKey(ReadOnlySpan<byte> keyBytes)
    {
        var keyA = new Ts4ResourceKey(
            BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(0, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(4, 4)),
            BinaryPrimitives.ReadUInt64LittleEndian(keyBytes.Slice(8, 8)));
        var keyB = new Ts4ResourceKey(
            BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(8, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(keyBytes.Slice(12, 4)),
            BinaryPrimitives.ReadUInt64LittleEndian(keyBytes.Slice(0, 8)));
        var keyC = TryReadTrailingTypeResourceKey(keyBytes, out var trailingTypeKey)
            ? $" keyC={trailingTypeKey.Type:X8}:{trailingTypeKey.Group:X8}:{trailingTypeKey.Instance:X16}"
            : string.Empty;
        var keyD = TryReadShiftedTrailingTypeResourceKey(keyBytes, out var shiftedTrailingTypeKey)
            ? $" keyD={shiftedTrailingTypeKey.Type:X8}:{shiftedTrailingTypeKey.Group:X8}:{shiftedTrailingTypeKey.Instance:X16}"
            : string.Empty;
        return $" keyA={keyA.Type:X8}:{keyA.Group:X8}:{keyA.Instance:X16} keyB={keyB.Type:X8}:{keyB.Group:X8}:{keyB.Instance:X16}{keyC}{keyD}";
    }

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
        List<Ts4TextureReference> textureReferences,
        ShaderParameterProfile? profileParameter,
        ShaderBlockProfile? shaderProfile)
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
        var looksLikeEmbeddedImageKey = ReadPotentialEmbeddedResourceKeys(keyBytes)
            .Any(static key => IsImageType(key.Type) && key.Instance != 0);
        if (!IsRecognizedTexturePropertyHash(propertyHash) &&
            !Ts4ShaderSemantics.IsLikelyTextureParameter(normalizedPropertyType, propertyArity, profileParameter) &&
            !looksLikeEmbeddedImageKey)
        {
            return false;
        }

        var decodedValue = DecodeMatdValue(normalizedPropertyType, propertyArity, propertyOffset, bytes);
        var isAuthoritativeValue = normalizedPropertyType == 2 || decodedValue.Representation == MaterialValueRepresentation.ResourceKey;
        if (!isAuthoritativeValue)
        {
            if (decodedValue.Representation == MaterialValueRepresentation.FloatVector)
            {
                return false;
            }

            if (!IsRecognizedTexturePropertyHash(propertyHash) &&
                profileParameter?.Category != ShaderParameterCategory.ResourceKey &&
                !looksLikeEmbeddedImageKey)
            {
                return false;
            }
        }

        foreach (var key in ReadPotentialEmbeddedResourceKeys(keyBytes))
        {
            if (!IsImageType(key.Type) || key.Instance == 0 || !LooksLikePlausibleHeuristicImageKey(key))
            {
                continue;
            }

            var inferredReference = new Ts4TextureReference(GetTextureSlotName(propertyHash, textureReferences.Count), key, propertyHash, IsHeuristic: false);
            var resolvedSlot = Ts4ShaderSemantics.ResolveTextureSlotName(inferredReference, shaderProfile);
            textureReferences.Add(inferredReference with { Slot = resolvedSlot });
            return true;
        }

        if (TryReadInstanceOnlyImageResourceKey(keyBytes, out var instanceOnlyKey))
        {
            var inferredReference = new Ts4TextureReference(GetTextureSlotName(propertyHash, textureReferences.Count), instanceOnlyKey, propertyHash, IsHeuristic: false);
            var resolvedSlot = Ts4ShaderSemantics.ResolveTextureSlotName(inferredReference, shaderProfile);
            textureReferences.Add(inferredReference with { Slot = resolvedSlot });
            return true;
        }

        return false;
    }

    private static bool TryReadHeuristicTextureReference(
        uint propertyHash,
        uint normalizedPropertyType,
        uint propertyArity,
        int propertyOffset,
        ReadOnlySpan<byte> bytes,
        List<Ts4TextureReference> textureReferences,
        ShaderParameterProfile? profileParameter,
        ShaderBlockProfile? shaderProfile)
    {
        if (propertyOffset + 16 > bytes.Length)
        {
            return false;
        }

        if (!IsRecognizedTexturePropertyHash(propertyHash) &&
            !Ts4ShaderSemantics.IsLikelyTextureParameter(normalizedPropertyType, propertyArity, profileParameter))
        {
            return false;
        }

        if (normalizedPropertyType != 2 || propertyArity != 1)
        {
            if (!Ts4ShaderSemantics.IsLikelyTextureParameter(normalizedPropertyType, propertyArity, profileParameter))
            {
                return false;
            }
        }

        var decodedValue = DecodeMatdValue(normalizedPropertyType, propertyArity, propertyOffset, bytes);
        if (decodedValue.Representation == MaterialValueRepresentation.FloatVector)
        {
            return false;
        }

        var keyBytes = bytes.Slice(propertyOffset, 16);
        foreach (var key in ReadPotentialEmbeddedResourceKeys(keyBytes))
        {
            if (!IsImageType(key.Type) || key.Instance == 0 || !LooksLikePlausibleHeuristicImageKey(key))
            {
                continue;
            }

            if (textureReferences.Any(existing => existing.Key.Equals(key)))
            {
                continue;
            }

            var inferredReference = new Ts4TextureReference(GetHeuristicTextureSlotName(propertyHash, textureReferences.Count), key, propertyHash, IsHeuristic: true);
            var resolvedSlot = Ts4ShaderSemantics.ResolveTextureSlotName(inferredReference, shaderProfile);
            textureReferences.Add(inferredReference with { Slot = resolvedSlot });
        }

        if (TryReadInstanceOnlyImageResourceKey(keyBytes, out var instanceOnlyKey) &&
            LooksLikePlausibleHeuristicImageKey(instanceOnlyKey) &&
            !textureReferences.Any(existing => existing.Key.Equals(instanceOnlyKey)))
        {
            var inferredReference = new Ts4TextureReference(GetHeuristicTextureSlotName(propertyHash, textureReferences.Count), instanceOnlyKey, propertyHash, IsHeuristic: true);
            var resolvedSlot = Ts4ShaderSemantics.ResolveTextureSlotName(inferredReference, shaderProfile);
            textureReferences.Add(inferredReference with { Slot = resolvedSlot });
        }

        return textureReferences.Count > 0;
    }

    private static bool IsRecognizedTexturePropertyHash(uint propertyHash) => propertyHash switch
    {
        0x1B9D3AC5 => true, // diffuse/base color
        0xC53A9D1B => true, // diffuse/base color
        0x773BA60F => true, // specular
        0x9F5D1C9B => true, // normal
        0x3A9F5D1C => true, // alpha / opacity
        _ => false
    };

    private static Ts4ResourceKey[] ReadPotentialEmbeddedResourceKeys(ReadOnlySpan<byte> bytes)
    {
        var embeddedKey = ReadMatdEmbeddedResourceKey(bytes);
        var typeFirstType = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        var typeFirstGroup = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        var typeFirstInstance = BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8, 8));
        var typeFirstKey = new Ts4ResourceKey(typeFirstType, typeFirstGroup, typeFirstInstance);
        var keyB = new Ts4ResourceKey(
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4)),
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4)),
            BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(0, 8)));
        var keys = new List<Ts4ResourceKey> { embeddedKey, typeFirstKey, keyB };
        if (TryReadTrailingTypeResourceKey(bytes, out var trailingTypeKey))
        {
            keys.Add(trailingTypeKey);
        }

        if (TryReadShiftedTrailingTypeResourceKey(bytes, out var shiftedTrailingTypeKey))
        {
            keys.Add(shiftedTrailingTypeKey);
        }

        return keys
            .Distinct()
            .ToArray();
    }

    private static bool TryReadInstanceOnlyImageResourceKey(ReadOnlySpan<byte> bytes, out Ts4ResourceKey key)
    {
        key = default;
        if (bytes.Length < 16)
        {
            return false;
        }

        var controlWord0 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        var controlWord1 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        var instanceLow = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var instanceHigh = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        var instance = ((ulong)instanceHigh << 32) | instanceLow;

        if (instance == 0)
        {
            return false;
        }

        // Some MATD payloads store image references as two small control words followed by the
        // texture instance, implicitly targeting the common DST image type/group.
        if (controlWord0 > 16 || controlWord1 > 0x4000)
        {
            return false;
        }

        key = new Ts4ResourceKey(0x00B2D882u, 0u, instance);
        return true;
    }

    private static bool LooksLikePlausibleHeuristicImageKey(Ts4ResourceKey key)
    {
        var low = (uint)(key.Instance & 0xFFFFFFFF);
        var high = (uint)(key.Instance >> 32);
        if (low == 0 || high == 0)
        {
            return false;
        }

        if (low <= 16 || high <= 16)
        {
            return false;
        }

        if (LooksLikeCommonFloatConstantWord(low) || LooksLikeCommonFloatConstantWord(high))
        {
            return false;
        }

        if (LooksLikeSuspiciousFloatPayloadWord(low) && LooksLikeSuspiciousFloatPayloadWord(high))
        {
            return false;
        }

        return true;
    }

    private static bool LooksLikeCommonFloatConstantWord(uint word) => word is
        0x3F800000 or // 1.0
        0x3F000000 or // 0.5
        0xBF800000 or // -1.0
        0x3E800000 or // 0.25
        0x3DCCCCCD or // 0.1
        0x40000000 or // 2.0
        0x40400000;   // 3.0

    private static bool LooksLikeSuspiciousFloatPayloadWord(uint word)
    {
        var value = BitConverter.UInt32BitsToSingle(word);
        return float.IsFinite(value) &&
               MathF.Abs(value) > 0f &&
               MathF.Abs(value) <= 4f;
    }

    private static bool TryReadTrailingTypeResourceKey(ReadOnlySpan<byte> bytes, out Ts4ResourceKey key)
    {
        key = default;
        if (bytes.Length < 16)
        {
            return false;
        }

        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        if (!IsImageType(type))
        {
            return false;
        }

        var instanceLow = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        var instanceHigh = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var instance = ((ulong)instanceHigh << 32) | instanceLow;
        if (instance == 0)
        {
            return false;
        }

        key = new Ts4ResourceKey(type, 0u, instance);
        return true;
    }

    private static bool TryReadShiftedTrailingTypeResourceKey(ReadOnlySpan<byte> bytes, out Ts4ResourceKey key)
    {
        key = default;
        if (bytes.Length < 16)
        {
            return false;
        }

        var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(12, 4));
        if (!IsImageType(type))
        {
            return false;
        }

        var metadataOrOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        if (metadataOrOffset > 0x00010000u)
        {
            return false;
        }

        var instanceLow = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        var instanceHigh = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        var instance = ((ulong)instanceHigh << 32) | instanceLow;
        if (instance == 0)
        {
            return false;
        }

        key = new Ts4ResourceKey(type, 0u, instance);
        return true;
    }

    private static string GetTextureSlotName(uint propertyHash, int existingTextureCount) => propertyHash switch
    {
        0x1B9D3AC5 => "diffuse",
        0xC53A9D1B => "diffuse",
        0x773BA60F => "specular",
        0x9F5D1C9B => "normal",
        0x3A9F5D1C => "alpha",
        _ => existingTextureCount == 0 ? "diffuse" : $"texture_{existingTextureCount}"
    };

    private static string GetHeuristicTextureSlotName(uint propertyHash, int existingTextureCount) => propertyHash switch
    {
        0x1B9D3AC5 => "diffuse",
        0xC53A9D1B => "diffuse",
        0x3A9F5D1C => "alpha",
        0x9F5D1C9B => "normal",
        0x773BA60F => "specular",
        _ => existingTextureCount == 0 ? "diffuse" : $"texture_{existingTextureCount}"
    };

    private static void TryApplyScalarProperty(
        uint shaderNameHash,
        uint propertyHash,
        ShaderParameterProfile? profileParameter,
        int propertyOffset,
        ReadOnlySpan<byte> bytes,
        float[] uvScales,
        ref Ts4TextureUvMapping diffuseUvMapping)
    {
        _ = uvScales;
        if (propertyOffset + 4 > bytes.Length)
        {
            return;
        }

        var scalarValue = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset, 4));
        if (Ts4ShaderSemantics.TryInterpretDiffuseUvMappingScalar(profileParameter, scalarValue, diffuseUvMapping, out var genericMapping))
        {
            diffuseUvMapping = genericMapping;
            return;
        }

        _ = shaderNameHash;
        _ = propertyHash;
    }

    private static void TryApplyVectorProperty(
        uint shaderNameHash,
        uint propertyHash,
        ShaderParameterProfile? profileParameter,
        int propertyOffset,
        uint propertyArity,
        ReadOnlySpan<byte> bytes,
        float[] uvScales,
        ref Ts4TextureUvMapping diffuseUvMapping)
    {
        _ = propertyArity;
        _ = uvScales;
        if (propertyOffset + 16 > bytes.Length)
        {
            return;
        }

        if (!TryReadFloatComponents(propertyArity, propertyOffset, bytes, out var typedValues))
        {
            return;
        }

        var valueCount = typedValues.Length;
        Span<float> values = valueCount <= 8 ? stackalloc float[valueCount] : new float[valueCount];
        typedValues.CopyTo(values);

        if (Ts4ShaderSemantics.TryInterpretDiffuseUvMappingVector(profileParameter, values, diffuseUvMapping, out var genericMapping))
        {
            diffuseUvMapping = genericMapping;
            return;
        }

        _ = shaderNameHash;
        _ = propertyHash;
    }

    private static bool IsPlausibleUvScale(float value) =>
        float.IsFinite(value) &&
        Math.Abs(value) >= 0.001f &&
        Math.Abs(value) <= 1024f;

    private static bool TryReadFloatComponents(
        uint propertyArity,
        int propertyOffset,
        ReadOnlySpan<byte> bytes,
        out float[] values)
    {
        values = [];
        var count = checked((int)Math.Max(1u, propertyArity));
        if (propertyOffset < 0 || propertyOffset + (count * 4) > bytes.Length)
        {
            return false;
        }

        var decoded = new float[count];
        for (var index = 0; index < count; index++)
        {
            decoded[index] = BinaryPrimitives.ReadSingleLittleEndian(bytes.Slice(propertyOffset + (index * 4), 4));
        }

        if (!ArePlausibleFloatComponents(decoded))
        {
            return false;
        }

        values = decoded;
        return true;
    }

    private static bool TryReadUInt32Components(
        uint propertyArity,
        int propertyOffset,
        ReadOnlySpan<byte> bytes,
        out uint[] values)
    {
        values = [];
        var count = checked((int)Math.Max(1u, propertyArity));
        if (propertyOffset < 0 || propertyOffset + (count * 4) > bytes.Length)
        {
            return false;
        }

        values = new uint[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(propertyOffset + (index * 4), 4));
        }

        return true;
    }

    private static bool ArePlausibleFloatComponents(ReadOnlySpan<float> values)
    {
        var nonZeroCount = 0;
        foreach (var value in values)
        {
            if (!float.IsFinite(value) || MathF.Abs(value) > 65536f)
            {
                return false;
            }

            if (MathF.Abs(value) > 0.0000001f)
            {
                nonZeroCount++;
            }
        }

        return nonZeroCount > 0 || values.Length > 0;
    }

    private static bool IsImageType(uint type) => type is
        0x00B2D882 or
        0x2F7D0004 or
        0x2BC04EDF or
        0x0988C7E1 or
        0x3453CF95 or
        0x1B4D2A70;
}

internal readonly record struct Ts4MtstEntry(Ts4ChunkReference Reference, uint Unknown0, uint StateNameHash)
{
    public bool IsNull => Reference.IsNull;
}

internal sealed record Ts4MtstChunk(Ts4ChunkReference? DefaultMaterialReference, IReadOnlyList<Ts4MtstEntry> Entries)
{
    public static Ts4MtstChunk Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("MTST"u8))
        {
            return new(null, []);
        }

        var entryCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)));
        var entries = new List<Ts4MtstEntry>(Math.Max(0, entryCount));
        var offset = 20;
        for (var i = 0; i < entryCount && offset + 12 <= bytes.Length; i++, offset += 12)
        {
            var reference = new Ts4ChunkReference(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)));
            var unknown0 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var stateNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 8, 4));
            entries.Add(new Ts4MtstEntry(reference, unknown0, stateNameHash));
        }

        var defaultReference = entries.FirstOrDefault(static entry => !entry.Reference.IsNull && entry.StateNameHash == 0).Reference;
        return new(defaultReference.IsNull ? null : defaultReference, entries);
    }

    public static IReadOnlyList<string> InspectReferences(ReadOnlySpan<byte> bytes)
    {
        var lines = new List<string>();
        if (bytes.Length < 20 || !bytes[..4].SequenceEqual("MTST"u8))
        {
            lines.Add("Not an MTST chunk.");
            return lines;
        }

        var entryCount = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(16, 4)));
        lines.Add($"materialRefCount={entryCount}");
        var offset = 20;
        for (var i = 0; i < entryCount && offset + 12 <= bytes.Length; i++, offset += 12)
        {
            var reference = new Ts4ChunkReference(BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4)));
            var unknown0 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 4, 4));
            var stateNameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset + 8, 4));
            lines.Add($"[{i}] ref=0x{reference.Raw:X8} type={reference.ReferenceType} index={reference.Index} unknown0=0x{unknown0:X8} stateHash=0x{stateNameHash:X8}");
        }

        return lines;
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
    private static float[] SelectPreferredUvCoordinates(
        IReadOnlyList<Ts4DecodedVertex> vertices,
        uint meshName,
        ICollection<string> diagnostics)
    {
        var uv0 = vertices.SelectMany(static vertex => vertex.Uv0 ?? []).ToArray();
        var uv1 = vertices.SelectMany(static vertex => vertex.Uv1 ?? []).ToArray();

        if (uv0.Length == 0)
        {
            return uv1;
        }

        if (uv1.Length == 0 || uv1.Length != uv0.Length)
        {
            return uv0;
        }

        var uv0Area = EstimateUvCoverageArea(uv0);
        var uv1Area = EstimateUvCoverageArea(uv1);
        if (uv0Area > 0f && uv0Area < 0.01f && uv1Area > uv0Area * 4f)
        {
            diagnostics.Add($"Mesh 0x{meshName:X8} uses UV1 for preview because UV0 covers only a tiny atlas region ({uv0Area:0.####}) while UV1 covers {uv1Area:0.####}.");
            return uv1;
        }

        return uv0;
    }

    private static float EstimateUvCoverageArea(float[] uvs)
    {
        if (uvs.Length < 2)
        {
            return 0f;
        }

        var minU = float.PositiveInfinity;
        var maxU = float.NegativeInfinity;
        var minV = float.PositiveInfinity;
        var maxV = float.NegativeInfinity;

        for (var index = 0; index + 1 < uvs.Length; index += 2)
        {
            var u = uvs[index];
            var v = uvs[index + 1];
            minU = Math.Min(minU, u);
            maxU = Math.Max(maxU, u);
            minV = Math.Min(minV, v);
            maxV = Math.Max(maxV, v);
        }

        if (!float.IsFinite(minU) || !float.IsFinite(maxU) || !float.IsFinite(minV) || !float.IsFinite(maxV))
        {
            return 0f;
        }

        return Math.Max(0f, maxU - minU) * Math.Max(0f, maxV - minV);
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
            // Shadow-only Build/Buy MLOD variants commonly omit a linked VRTF chunk and pack
            // positions as a compact short4-normalized stream (8 bytes per vertex). Using a
            // 16-byte default stride silently drops every other vertex and makes the IBUF look
            // wildly invalid, so the fallback path must match the actual packed position size.
            VertexStride = 8,
            Elements =
            [
                new Ts4VertexElement(Ts4VertexUsage.Position, 0, 0x07, 0)
            ]
        };
}

internal sealed record Ts4DecodedVertex(float[] Position, float[]? Normal, float[]? Tangent, float[]? Uv0, float[]? Uv1);

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

    public IReadOnlyList<Ts4DecodedVertex> ReadVertices(
        Ts4VrtfChunk vrtf,
        uint streamOffset,
        int baseVertexIndex,
        int vertexCount,
        float[] uvScales)
    {
        var results = new List<Ts4DecodedVertex>(vertexCount);
        for (var index = 0; index < vertexCount; index++)
        {
            var absoluteVertexIndex = checked(baseVertexIndex + index);
            var vertexOffset = checked((int)streamOffset + (absoluteVertexIndex * vrtf.VertexStride));
            if (vertexOffset < 0 || vertexOffset + vrtf.VertexStride > rawData.Length)
            {
                break;
            }

            float[]? position = null;
            float[]? normal = null;
            float[]? tangent = null;
            float[]? uv0 = null;
            float[]? uv1 = null;

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
                    case Ts4VertexUsage.Uv when element.UsageIndex == 1:
                        uv1 = ReadUv(rawData, offset, element.Format, uvScales);
                        break;
                }
            }

            if (position is null)
            {
                continue;
            }

            results.Add(new Ts4DecodedVertex(position, normal, tangent, uv0, uv1));
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
        // TS4 UV format 0x06 appears in two practical encodings:
        // - full unsigned-normalized 0..65535
        // - half-range 0..32767, common on some Build/Buy atlases
        // The half-range variant otherwise collapses a full atlas into the top-left quarter.
        var rawU = ReadUInt16(data, offset);
        var rawV = ReadUInt16(data, offset + 2);
        var divisor = rawU <= short.MaxValue && rawV <= short.MaxValue
            ? (float)short.MaxValue
            : ushort.MaxValue;
        var u = rawU / divisor;
        var v = rawV / divisor;
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

    public IReadOnlyList<uint> ReadIndices(
        int startIndex,
        int count,
        int normalizeBaseIndex = 0,
        int expectedVertexCount = 0)
    {
        var directStartOffset = startIndex * indexSize;
        var canTryCompactDelta = flags == 0x1 && rawData.Length >= 4;
        var directWindowOutOfRange = directStartOffset + (count * indexSize) > rawData.Length;

        var rawResults = new List<uint>(count);
        if (!directWindowOutOfRange)
        {
            for (var offset = directStartOffset; offset + indexSize <= rawData.Length && rawResults.Count < count; offset += indexSize)
            {
                rawResults.Add(indexSize == 4
                    ? BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(offset, 4))
                    : BinaryPrimitives.ReadUInt16LittleEndian(rawData.AsSpan(offset, 2)));
            }
        }

        var results = NormalizeIndices(rawResults, normalizeBaseIndex);

        if (expectedVertexCount > 0)
        {
            var candidates = new List<IReadOnlyList<uint>>();
            if (results.Count > 0)
            {
                candidates.Add(results);
            }

            if (canTryCompactDelta &&
                TryReadCompactDeltaIndices(startIndex, count, out var compactFallback))
            {
                candidates.Add(NormalizeIndices(compactFallback, normalizeBaseIndex));
                candidates.Add(NormalizeIndices(compactFallback.Select(static value => value & 0xFFFFu).ToArray(), normalizeBaseIndex));
            }

            if (indexSize == 4 && rawResults.Count > 0)
            {
                var low16Fallback = rawResults.Select(static value => value & 0xFFFFu).ToArray();
                candidates.Add(NormalizeIndices(low16Fallback, normalizeBaseIndex));
            }

            if (candidates.Count > 0)
            {
                return SelectBestIndexCandidate(candidates, expectedVertexCount);
            }
        }

        return results;
    }

    public IReadOnlyList<string> InspectCandidates(int startIndex, int count, int normalizeBaseIndex, int expectedVertexCount)
    {
        var lines = new List<string>();
        foreach (var (name, indices) in GetIndexCandidates(startIndex, count))
        {
            var normalized = NormalizeIndices(indices, normalizeBaseIndex);
            var invalidIndices = CountInvalidIndices(normalized, expectedVertexCount);
            var (validTriangles, invalidTriangles) = CountTriangleValidity(normalized, expectedVertexCount);
            var minIndex = normalized.Count > 0 ? normalized.Min() : 0u;
            var maxIndex = normalized.Count > 0 ? normalized.Max() : 0u;
            var sample = string.Join(", ", normalized.Take(12));
            lines.Add(
                $"{name}: count={normalized.Count} base={normalizeBaseIndex} min={minIndex} max={maxIndex} validTriangles={validTriangles} invalidTriangles={invalidTriangles} invalidIndices={invalidIndices} sample=[{sample}]");
        }

        return lines;
    }

    private static List<uint> NormalizeIndices(IReadOnlyList<uint> indices, int normalizeBaseIndex)
    {
        var results = new List<uint>(indices.Count);
        if (normalizeBaseIndex <= 0)
        {
            results.AddRange(indices);
            return results;
        }

        foreach (var index in indices)
        {
            results.Add(index >= (uint)normalizeBaseIndex
                ? index - (uint)normalizeBaseIndex
                : uint.MaxValue);
        }

        return results;
    }

    private static IReadOnlyList<uint> SelectBestIndexCandidate(
        IReadOnlyList<IReadOnlyList<uint>> candidates,
        int expectedVertexCount)
    {
        var bestCandidate = candidates[0];
        var bestScore = ScoreIndexCandidate(bestCandidate, expectedVertexCount);

        for (var i = 1; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var score = ScoreIndexCandidate(candidate, expectedVertexCount);
            if (IsBetterScore(score, bestScore))
            {
                bestCandidate = candidate;
                bestScore = score;
            }
        }

        return bestCandidate;
    }

    private static IndexCandidateScore ScoreIndexCandidate(
        IReadOnlyList<uint> indices,
        int expectedVertexCount)
    {
        var invalidIndices = CountInvalidIndices(indices, expectedVertexCount);
        var (validTriangles, invalidTriangles) = CountTriangleValidity(indices, expectedVertexCount);
        return new IndexCandidateScore(validTriangles, -invalidTriangles, -invalidIndices, indices.Count);
    }

    private static bool IsBetterScore(IndexCandidateScore candidate, IndexCandidateScore currentBest)
    {
        if (candidate.ValidTriangles != currentBest.ValidTriangles)
        {
            return candidate.ValidTriangles > currentBest.ValidTriangles;
        }

        if (candidate.NegativeInvalidTriangles != currentBest.NegativeInvalidTriangles)
        {
            return candidate.NegativeInvalidTriangles > currentBest.NegativeInvalidTriangles;
        }

        if (candidate.NegativeInvalidIndices != currentBest.NegativeInvalidIndices)
        {
            return candidate.NegativeInvalidIndices > currentBest.NegativeInvalidIndices;
        }

        return candidate.IndexCount > currentBest.IndexCount;
    }

    private static bool LooksImplausible(IReadOnlyList<uint> indices, int expectedVertexCount)
    {
        if (indices.Count == 0 || expectedVertexCount <= 0)
        {
            return false;
        }

        var invalid = CountInvalidIndices(indices, expectedVertexCount);
        return invalid > indices.Count / 4;
    }

    private static int CountInvalidIndices(IReadOnlyList<uint> indices, int expectedVertexCount)
    {
        if (indices.Count == 0 || expectedVertexCount <= 0)
        {
            return 0;
        }

        var invalid = 0;
        foreach (var index in indices)
        {
            if (index >= expectedVertexCount)
            {
                invalid++;
            }
        }

        return invalid;
    }

    private static (int ValidTriangles, int InvalidTriangles) CountTriangleValidity(
        IReadOnlyList<uint> indices,
        int expectedVertexCount)
    {
        if (indices.Count == 0 || expectedVertexCount <= 0)
        {
            return (0, 0);
        }

        var valid = 0;
        var invalid = 0;
        for (var index = 0; index + 2 < indices.Count; index += 3)
        {
            var a = indices[index];
            var b = indices[index + 1];
            var c = indices[index + 2];
            if (a >= expectedVertexCount || b >= expectedVertexCount || c >= expectedVertexCount)
            {
                invalid++;
            }
            else
            {
                valid++;
            }
        }

        return (valid, invalid);
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

    private IReadOnlyList<(string Name, IReadOnlyList<uint> Indices)> GetIndexCandidates(int startIndex, int count)
    {
        var candidates = new List<(string Name, IReadOnlyList<uint> Indices)>();

        var directStartOffset = startIndex * indexSize;
        var direct = new List<uint>(count);
        for (var offset = directStartOffset; offset + indexSize <= rawData.Length && direct.Count < count; offset += indexSize)
        {
            direct.Add(indexSize == 4
                ? BinaryPrimitives.ReadUInt32LittleEndian(rawData.AsSpan(offset, 4))
                : BinaryPrimitives.ReadUInt16LittleEndian(rawData.AsSpan(offset, 2)));
        }

        candidates.Add(($"direct{indexSize * 8}", direct));

        if (flags == 0x1 && rawData.Length >= 4 &&
            TryReadCompactDeltaIndices(startIndex, count, out var compact))
        {
            candidates.Add(("compactDelta", compact));
            candidates.Add(("compactDeltaLow16", compact.Select(static value => value & 0xFFFFu).ToArray()));
        }

        if (indexSize == 4)
        {
            candidates.Add(("low16", direct.Select(static value => value & 0xFFFFu).ToArray()));
        }

        return candidates;
    }
}

internal readonly record struct IndexCandidateScore(
    int ValidTriangles,
    int NegativeInvalidTriangles,
    int NegativeInvalidIndices,
    int IndexCount);

internal readonly record struct TextureLookupKey(
    uint Type,
    uint Group,
    ulong FullInstance,
    string TypeName);

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

internal readonly record struct Ts4MeshWindow(int VertexReadBase, int IndexNormalizeBase)
{
    public static Ts4MeshWindow From(Ts4MlodMesh mesh)
    {
        var minVertexIndex = Math.Max(0, mesh.MinVertexIndex);
        var startVertex = Math.Max(0, mesh.StartVertex);
        return new Ts4MeshWindow(
            checked(startVertex + minVertexIndex),
            minVertexIndex);
    }
}

internal enum Ts4PrimitiveType : uint
{
    PointList = 0,
    LineList = 1,
    LineStrip = 2,
    TriangleList = 3
}
