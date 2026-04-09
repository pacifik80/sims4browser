using System.Text.Json;
using Assimp;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Export;

public sealed class RawExportService : IRawExportService
{
    private readonly IResourceCatalogService resourceCatalogService;

    public RawExportService(IResourceCatalogService resourceCatalogService)
    {
        this.resourceCatalogService = resourceCatalogService;
    }

    public async Task<ExportedFileResult> ExportAsync(RawExportRequest request, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(request.OutputDirectory);
        var bytes = await resourceCatalogService.GetResourceBytesAsync(request.Resource.PackagePath, request.Resource.Key, raw: true, cancellationToken);

        var safeName = Slugify(request.Resource.Name ?? request.Resource.Key.FullTgi);
        var extension = request.Resource.PreviewKind switch
        {
            PreviewKind.Text => ".txt",
            PreviewKind.Texture => ".bin",
            PreviewKind.Audio => ".bin",
            _ => ".bin"
        };

        var outputPath = Path.Combine(request.OutputDirectory, $"{safeName}_{request.Resource.Key.TypeName}{extension}");
        await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
        return new ExportedFileResult(true, outputPath, "Raw export completed.");
    }

    private static string Slugify(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var filtered = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(filtered);
    }
}

public sealed class AssimpFbxExportService : IFbxExportService
{
    public async Task<ExportedFileResult> ExportAsync(SceneExportRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var assetFolder = Path.Combine(request.OutputDirectory, request.AssetSlug);
        var texturesFolder = Path.Combine(assetFolder, "Textures");
        Directory.CreateDirectory(assetFolder);
        Directory.CreateDirectory(texturesFolder);

        foreach (var texture in request.Textures)
        {
            var path = Path.Combine(texturesFolder, texture.FileName);
            await File.WriteAllBytesAsync(path, texture.PngBytes, cancellationToken);
        }

        var scene = BuildAssimpScene(request.Scene);
        var fbxPath = Path.Combine(assetFolder, $"{request.AssetSlug}.fbx");
        try
        {
            using var context = new AssimpContext();
            context.ExportFile(scene, fbxPath, "fbx");
        }
        catch
        {
            WriteFallbackAsciiFbx(fbxPath, request.Scene);
        }

        if (!File.Exists(fbxPath))
        {
            WriteFallbackAsciiFbx(fbxPath, request.Scene);
        }

        var manifest = new
        {
            asset = request.AssetSlug,
            sources = request.SourceResources.Select(static resource => new
            {
                resource.PackagePath,
                resource.Key.FullTgi,
                resource.Key.TypeName
            }),
            textures = request.Textures.Select(texture => Path.Combine("Textures", texture.FileName).Replace('\\', '/')).ToArray(),
            unsupportedData = request.Diagnostics.ToArray()
        };

        var metadata = new
        {
            request.Scene.Name,
            MeshCount = request.Scene.Meshes.Count,
            VertexCount = request.Scene.Meshes.Sum(static mesh => mesh.Positions.Count / 3),
            IndexCount = request.Scene.Meshes.Sum(static mesh => mesh.Indices.Count),
            MaterialCount = request.Scene.Materials.Count,
            BoneCount = request.Scene.Bones.Count,
            request.Scene.Bounds
        };

        await File.WriteAllTextAsync(Path.Combine(assetFolder, "manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(assetFolder, "metadata.json"), JsonSerializer.Serialize(metadata, JsonOptions), cancellationToken);

        return new ExportedFileResult(true, fbxPath, "FBX export completed.");
    }

    private static Scene BuildAssimpScene(CanonicalScene source)
    {
        var scene = new Scene
        {
            RootNode = new Node(source.Name)
        };

        foreach (var material in source.Materials)
        {
            scene.Materials.Add(new Material
            {
                Name = material.Name,
                ColorDiffuse = new Color4D(1f, 1f, 1f, 1f)
            });
        }

        if (scene.MaterialCount == 0)
        {
            scene.Materials.Add(new Material { Name = "DefaultMaterial" });
        }

        for (var meshIndex = 0; meshIndex < source.Meshes.Count; meshIndex++)
        {
            var sourceMesh = source.Meshes[meshIndex];
            var mesh = new Mesh(sourceMesh.Name, PrimitiveType.Triangle)
            {
                MaterialIndex = Math.Clamp(sourceMesh.MaterialIndex, 0, scene.MaterialCount - 1)
            };

            for (var i = 0; i < sourceMesh.Positions.Count; i += 3)
            {
                mesh.Vertices.Add(new Vector3D(sourceMesh.Positions[i], sourceMesh.Positions[i + 1], sourceMesh.Positions[i + 2]));
            }

            for (var i = 0; i < sourceMesh.Normals.Count; i += 3)
            {
                mesh.Normals.Add(new Vector3D(sourceMesh.Normals[i], sourceMesh.Normals[i + 1], sourceMesh.Normals[i + 2]));
            }

            for (var i = 0; i < sourceMesh.Tangents.Count; i += 3)
            {
                mesh.Tangents.Add(new Vector3D(sourceMesh.Tangents[i], sourceMesh.Tangents[i + 1], sourceMesh.Tangents[i + 2]));
            }

            for (var i = 0; i < sourceMesh.Uvs.Count; i += 2)
            {
                mesh.TextureCoordinateChannels[0].Add(new Vector3D(sourceMesh.Uvs[i], sourceMesh.Uvs[i + 1], 0));
            }

            for (var i = 0; i < sourceMesh.Indices.Count; i += 3)
            {
                var face = new Face();
                face.Indices.Add(sourceMesh.Indices[i]);
                face.Indices.Add(sourceMesh.Indices[i + 1]);
                face.Indices.Add(sourceMesh.Indices[i + 2]);
                mesh.Faces.Add(face);
            }

            foreach (var grouping in sourceMesh.SkinWeights.GroupBy(static weight => weight.BoneIndex))
            {
                var boneName = grouping.Key >= 0 && grouping.Key < source.Bones.Count
                    ? source.Bones[grouping.Key].Name
                    : $"Bone_{grouping.Key}";

                var bone = new Bone { Name = boneName };
                foreach (var weight in grouping)
                {
                    bone.VertexWeights.Add(new Assimp.VertexWeight(weight.VertexIndex, weight.Weight));
                }

                mesh.Bones.Add(bone);
            }

            scene.Meshes.Add(mesh);
            scene.RootNode.MeshIndices.Add(meshIndex);
        }

        return scene;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static void WriteFallbackAsciiFbx(string path, CanonicalScene scene)
    {
        var mesh = scene.Meshes.FirstOrDefault();
        var vertices = mesh is null
            ? string.Empty
            : string.Join(",", mesh.Positions.Select(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var polygonIndices = mesh is null
            ? string.Empty
            : string.Join(",", mesh.Indices.Select((index, i) => i % 3 == 2 ? -(index + 1) : index).Select(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        var content = $@"; FBX 7.3.0 project file
FBXHeaderExtension:  {{
	FBXHeaderVersion: 1003
	FBXVersion: 7300
}}
Objects:  {{
	Geometry: 1, ""Geometry::{scene.Name}"", ""Mesh"" {{
		Vertices: *{mesh?.Positions.Count ?? 0} {{
			a: {vertices}
		}}
		PolygonVertexIndex: *{mesh?.Indices.Count ?? 0} {{
			a: {polygonIndices}
		}}
	}}
	Model: 2, ""Model::{scene.Name}"", ""Mesh"" {{
		Version: 232
	}}
}}
Connections:  {{
	C: ""OO"",1,2
}}";

        File.WriteAllText(path, content);
    }
}
