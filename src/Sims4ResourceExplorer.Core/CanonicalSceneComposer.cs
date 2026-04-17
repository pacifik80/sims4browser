namespace Sims4ResourceExplorer.Core;

public static class CanonicalSceneComposer
{
    public static ScenePreviewContent Compose(string name, IReadOnlyList<ScenePreviewContent> previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        if (previews.Count == 0)
        {
            throw new ArgumentException("At least one scene preview is required.", nameof(previews));
        }

        var primaryPreview = previews[0];
        var scenePreviews = previews
            .Where(static preview => preview.Scene is not null)
            .ToArray();
        if (scenePreviews.Length == 0)
        {
            var missingSceneDiagnostics = JoinDiagnostics(previews.Select(static preview => preview.Diagnostics));
            return new ScenePreviewContent(
                primaryPreview.Resource,
                null,
                missingSceneDiagnostics,
                SceneBuildStatus.Unsupported);
        }

        if (scenePreviews.Length == 1)
        {
            var single = scenePreviews[0];
            var singleScene = single.Scene!;
            var renamedScene = string.Equals(singleScene.Name, name, StringComparison.Ordinal)
                ? singleScene
                : singleScene with { Name = name };
            var singleDiagnostics = JoinDiagnostics(previews.Select(static preview => preview.Diagnostics));
            var singleStatus = DetermineAggregateStatus(previews);
            return new ScenePreviewContent(single.Resource, renamedScene, singleDiagnostics, singleStatus);
        }

        var materials = new List<CanonicalMaterial>();
        var bones = new List<CanonicalBone>();
        var meshes = new List<CanonicalMesh>();
        var boneIndexByName = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var preview in scenePreviews)
        {
            var scene = preview.Scene!;
            var materialOffset = materials.Count;
            materials.AddRange(scene.Materials);

            var boneIndexMap = new Dictionary<int, int>();
            for (var boneIndex = 0; boneIndex < scene.Bones.Count; boneIndex++)
            {
                var bone = scene.Bones[boneIndex];
                if (!boneIndexByName.TryGetValue(bone.Name, out var mergedBoneIndex))
                {
                    mergedBoneIndex = bones.Count;
                    bones.Add(bone);
                    boneIndexByName.Add(bone.Name, mergedBoneIndex);
                }

                boneIndexMap[boneIndex] = mergedBoneIndex;
            }

            foreach (var mesh in scene.Meshes)
            {
                var remappedWeights = mesh.SkinWeights
                    .Select(weight => weight with
                    {
                        BoneIndex = boneIndexMap.TryGetValue(weight.BoneIndex, out var mergedBoneIndex)
                            ? mergedBoneIndex
                            : weight.BoneIndex
                    })
                    .ToArray();
                meshes.Add(mesh with
                {
                    MaterialIndex = mesh.MaterialIndex + materialOffset,
                    SkinWeights = remappedWeights
                });
            }
        }

        var composedScene = new CanonicalScene(
            name,
            meshes,
            materials,
            bones,
            ComputeBounds(meshes));
        var diagnostics = JoinDiagnostics(previews.Select(static preview => preview.Diagnostics));
        var status = DetermineAggregateStatus(previews);
        return new ScenePreviewContent(primaryPreview.Resource, composedScene, diagnostics, status);
    }

    private static SceneBuildStatus DetermineAggregateStatus(IReadOnlyList<ScenePreviewContent> previews)
    {
        if (previews.Count == 0)
        {
            return SceneBuildStatus.Unsupported;
        }

        if (previews.Any(static preview => preview.Scene is not null) &&
            previews.All(static preview => preview.Status == SceneBuildStatus.SceneReady))
        {
            return SceneBuildStatus.SceneReady;
        }

        return previews.Any(static preview => preview.Scene is not null)
            ? SceneBuildStatus.Partial
            : SceneBuildStatus.Unsupported;
    }

    private static string JoinDiagnostics(IEnumerable<string> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .SelectMany(static text => text.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal));

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
}
