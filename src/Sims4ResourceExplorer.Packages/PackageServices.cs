using LlamaLogic.Packages;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Packages;

public sealed class FileSystemPackageScanner : IPackageScanner
{
    public Task<IReadOnlyList<string>> DiscoverPackagePathsAsync(IEnumerable<DataSourceDefinition> sources, CancellationToken cancellationToken) =>
        Task.Run<IReadOnlyList<string>>(() =>
        {
            var results = new List<string>();

            foreach (var source in sources.Where(static source => source.IsEnabled))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!Directory.Exists(source.RootPath))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(source.RootPath, "*.package", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    results.Add(file);
                }
            }

            return results;
        }, cancellationToken);
}

public sealed class LlamaResourceCatalogService : IResourceCatalogService
{
    public async Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var resources = new List<ResourceMetadata>();
        var info = new FileInfo(packagePath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        progress?.Report(new PackageScanProgress("opening package", 0, 0, 0, stopwatch.Elapsed, $"Opening {Path.GetFileName(packagePath)}"));

        await using var package = await OpenPackageAsync(packagePath, cancellationToken);
        progress?.Report(new PackageScanProgress("reading package index", 0, 0, 0, stopwatch.Elapsed));
        var keys = await package.GetKeysAsync(ResourceKeyOrder.Preserve, cancellationToken);
        progress?.Report(new PackageScanProgress("enumerating resources", keys.Count, 0, 0, stopwatch.Elapsed));

        const int reportEveryResources = 250;
        var lastReportAt = TimeSpan.Zero;

        for (var index = 0; index < keys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = keys[index];

            var resourceKey = ToCoreKey(key);
            string? name = null;
            long uncompressedSize = 0;
            long compressedSize = 0;
            var isCompressed = false;
            var itemDiagnostics = string.Empty;

            try
            {
                name = await package.GetNameByKeyAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                itemDiagnostics = $"Name lookup failed: {ex.Message}";
            }

            try
            {
                uncompressedSize = await package.GetSizeAsync(key, cancellationToken);
            }
            catch (Exception ex)
            {
                itemDiagnostics = AppendDiagnostic(itemDiagnostics, $"Size lookup failed: {ex.Message}");
            }

            compressedSize = uncompressedSize;
            isCompressed = false;

            var previewKind = ResourceTypeHints.GetPreviewKind(resourceKey.TypeName);
            resources.Add(new ResourceMetadata(
                Guid.NewGuid(),
                source.Id,
                source.Kind,
                packagePath,
                resourceKey,
                name,
                compressedSize,
                uncompressedSize,
                isCompressed,
                previewKind,
                previewKind is not PreviewKind.Unsupported,
                true,
                ResourceTypeHints.GetAssetLinkageSummary(resourceKey.TypeName),
                itemDiagnostics));

            var processed = index + 1;
            var elapsed = stopwatch.Elapsed;
            if (processed == 1 || processed == keys.Count || processed % reportEveryResources == 0 || elapsed - lastReportAt >= TimeSpan.FromSeconds(1))
            {
                lastReportAt = elapsed;
                progress?.Report(new PackageScanProgress(
                    "extracting metadata",
                    keys.Count,
                    processed,
                    0,
                    elapsed,
                    $"{Path.GetFileName(packagePath)}: extracting metadata {processed:N0} / {keys.Count:N0} resources"));
            }
        }

        progress?.Report(new PackageScanProgress("finalizing package", keys.Count, keys.Count, 0, stopwatch.Elapsed));

        return new PackageScanResult(
            source.Id,
            source.Kind,
            packagePath,
            info.Exists ? info.Length : 0,
            info.Exists ? info.LastWriteTimeUtc : DateTime.UtcNow,
            resources,
            diagnostics);
    }

    public async Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken)
    {
        await using var package = await OpenPackageAsync(packagePath, cancellationToken);
        var llamaKey = ToLlamaKey(key);

        return raw
            ? (await package.GetRawAsync(llamaKey, false, cancellationToken)).ToArray()
            : (await package.GetAsync(llamaKey, false, cancellationToken)).ToArray();
    }

    public async Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        await using var package = await OpenPackageAsync(packagePath, cancellationToken);
        var llamaKey = ToLlamaKey(key);

        try
        {
            var xml = await package.GetXmlAsync(llamaKey, false, cancellationToken);
            return xml.ToString();
        }
        catch
        {
            try
            {
                return await package.GetTextAsync(llamaKey, false, cancellationToken);
            }
            catch
            {
                return null;
            }
        }
    }

    public async Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        await using var package = await OpenPackageAsync(packagePath, cancellationToken);
        var llamaKey = ToLlamaKey(key);

        if (key.TypeName is nameof(ResourceType.PNGImage) or nameof(ResourceType.PNGImage2))
        {
            return (await package.GetAsync(llamaKey, false, cancellationToken)).ToArray();
        }

        if (key.TypeName is nameof(ResourceType.BuyBuildThumbnail) or nameof(ResourceType.BodyPartThumbnail) or nameof(ResourceType.CASPartThumbnail))
        {
            return (await package.GetTranslucentJpegAsPngAsync(llamaKey, false, cancellationToken)).ToArray();
        }

        if (ResourceTypeHints.IsDdsFamily(key.TypeName))
        {
            return (await package.GetDdsAsPngAsync(llamaKey, false, cancellationToken)).ToArray();
        }

        return null;
    }

    private static ResourceKeyRecord ToCoreKey(ResourceKey key) =>
        new((uint)key.Type, key.Group, key.FullInstance, key.Type.ToString());

    private static ResourceKey ToLlamaKey(ResourceKeyRecord key) =>
        new((ResourceType)key.Type, key.Group, key.FullInstance);

    private static string AppendDiagnostic(string existing, string message) =>
        string.IsNullOrWhiteSpace(existing) ? message : $"{existing} | {message}";

    private static async Task<DataBasePackedFile> OpenPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            return await DataBasePackedFile.FromStreamAsync(stream, cancellationToken);
        }
        catch (IOException ex)
        {
            throw new IOException(
                $"Failed to open package '{packagePath}'. If The Sims 4 is running, close it and try again. Original error: {ex.Message}",
                ex);
        }
    }
}

internal static class ResourceTypeHints
{
    public static PreviewKind GetPreviewKind(string typeName)
    {
        if (IsTextLike(typeName))
        {
            return PreviewKind.Text;
        }

        if (IsTextureLike(typeName))
        {
            return PreviewKind.Texture;
        }

        if (IsSceneLike(typeName))
        {
            return PreviewKind.Scene;
        }

        if (IsAudioLike(typeName))
        {
            return PreviewKind.Audio;
        }

        return PreviewKind.Hex;
    }

    public static bool IsDdsFamily(string typeName) =>
        typeName is nameof(ResourceType.DSTImage)
            or nameof(ResourceType.LRLEImage)
            or nameof(ResourceType.RLE2Image)
            or nameof(ResourceType.RLESImage);

    public static bool IsTextureLike(string typeName) =>
        typeName is nameof(ResourceType.PNGImage)
            or nameof(ResourceType.PNGImage2)
            or nameof(ResourceType.BuyBuildThumbnail)
            or nameof(ResourceType.BodyPartThumbnail)
            or nameof(ResourceType.CASPartThumbnail)
            || IsDdsFamily(typeName);

    public static bool IsSceneLike(string typeName) =>
        typeName is nameof(ResourceType.Geometry)
            or nameof(ResourceType.Model)
            or nameof(ResourceType.ModelLOD)
            or nameof(ResourceType.Rig);

    public static bool IsAudioLike(string typeName) =>
        typeName.Contains("Audio", StringComparison.OrdinalIgnoreCase) ||
        typeName is nameof(ResourceType.AVI);

    public static bool IsTextLike(string typeName) =>
        typeName.Contains("Tuning", StringComparison.OrdinalIgnoreCase) ||
        typeName.Contains("XML", StringComparison.OrdinalIgnoreCase) ||
        typeName.Contains("Manifest", StringComparison.OrdinalIgnoreCase) ||
        typeName.Contains("StringTable", StringComparison.OrdinalIgnoreCase) ||
        typeName.Contains("Recipe", StringComparison.OrdinalIgnoreCase);

    public static string GetAssetLinkageSummary(string typeName)
    {
        if (typeName is nameof(ResourceType.ObjectCatalog) or nameof(ResourceType.ObjectDefinition))
        {
            return "Build/Buy seed";
        }

        if (typeName is nameof(ResourceType.CASPart))
        {
            return "CAS seed";
        }

        if (IsSceneLike(typeName))
        {
            return "3D component";
        }

        if (IsTextureLike(typeName))
        {
            return "Texture/thumbnail";
        }

        if (IsAudioLike(typeName))
        {
            return "Audio-like";
        }

        return string.Empty;
    }
}
