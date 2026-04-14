using LlamaLogic.Packages;
using Sims4ResourceExplorer.Core;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sims4ResourceExplorer.Packages;

public sealed class FileSystemPackageScanner : IPackageScanner
{
    public async IAsyncEnumerable<DiscoveredPackage> DiscoverPackagesAsync(IEnumerable<DataSourceDefinition> sources, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
                var info = new FileInfo(file);
                if (!info.Exists)
                {
                    continue;
                }

                yield return new DiscoveredPackage(source, file, info.Length, info.LastWriteTimeUtc);
                await Task.Yield();
            }
        }
    }
}

public sealed class LlamaResourceCatalogService : IResourceCatalogService
{
    private const int ParallelEnumerationThreshold = 20000;
    private const int EnumerationRangeSize = 4096;

    public async Task<PackageScanResult> ScanPackageAsync(DataSourceDefinition source, string packagePath, IProgress<PackageScanProgress>? progress, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var info = new FileInfo(packagePath);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        progress?.Report(new PackageScanProgress("opening package", 0, 0, 0, stopwatch.Elapsed, $"Opening {Path.GetFileName(packagePath)}"));

        await using var package = await OpenPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        progress?.Report(new PackageScanProgress("reading package index", 0, 0, 0, stopwatch.Elapsed));
        var keys = package.Keys;
        progress?.Report(new PackageScanProgress("enumerating resources", keys.Count, 0, 0, stopwatch.Elapsed));

        const int reportEveryResources = 250;
        var lastReportAt = TimeSpan.Zero;
        ResourceMetadata[] resources;

        if (ShouldParallelizeEnumeration(keys.Count))
        {
            resources = EnumerateResourcesParallel(source, packagePath, keys, progress, stopwatch, reportEveryResources, ref lastReportAt, cancellationToken);
        }
        else
        {
            resources = EnumerateResourcesSequential(source, packagePath, keys, progress, stopwatch, reportEveryResources, ref lastReportAt, cancellationToken);
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

    private static ResourceMetadata[] EnumerateResourcesSequential(
        DataSourceDefinition source,
        string packagePath,
        IReadOnlyList<ResourceKey> keys,
        IProgress<PackageScanProgress>? progress,
        Stopwatch stopwatch,
        int reportEveryResources,
        ref TimeSpan lastReportAt,
        CancellationToken cancellationToken)
    {
        var resources = new ResourceMetadata[keys.Count];
        for (var index = 0; index < keys.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resources[index] = CreateResourceMetadata(source, packagePath, keys[index]);

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

        return resources;
    }

    private static ResourceMetadata[] EnumerateResourcesParallel(
        DataSourceDefinition source,
        string packagePath,
        IReadOnlyList<ResourceKey> keys,
        IProgress<PackageScanProgress>? progress,
        Stopwatch stopwatch,
        int reportEveryResources,
        ref TimeSpan lastReportAt,
        CancellationToken cancellationToken)
    {
        var resources = new ResourceMetadata[keys.Count];
        var processedCount = 0;
        var reportSync = new object();
        var lastReported = lastReportAt;

        Parallel.ForEach(
            Partitioner.Create(0, keys.Count, EnumerationRangeSize),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = GetEnumerationParallelism(),
                CancellationToken = cancellationToken
            },
            range =>
            {
                for (var index = range.Item1; index < range.Item2; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    resources[index] = CreateResourceMetadata(source, packagePath, keys[index]);
                }

                var processed = Interlocked.Add(ref processedCount, range.Item2 - range.Item1);
                var elapsed = stopwatch.Elapsed;
                if (processed == keys.Count || processed % reportEveryResources == 0 || elapsed - lastReported >= TimeSpan.FromSeconds(1))
                {
                    lock (reportSync)
                    {
                        elapsed = stopwatch.Elapsed;
                        if (processed == keys.Count || processed % reportEveryResources == 0 || elapsed - lastReported >= TimeSpan.FromSeconds(1))
                        {
                            lastReported = elapsed;
                            progress?.Report(new PackageScanProgress(
                                "extracting metadata",
                                keys.Count,
                                processed,
                                0,
                                elapsed,
                                $"{Path.GetFileName(packagePath)}: extracting metadata {processed:N0} / {keys.Count:N0} resources"));
                        }
                    }
                }
            });

        lastReportAt = lastReported;
        return resources;
    }

    private static ResourceMetadata CreateResourceMetadata(DataSourceDefinition source, string packagePath, ResourceKey key)
    {
        var resourceKey = ToCoreKey(key);
        var previewKind = ResourceTypeHints.GetPreviewKind(resourceKey.TypeName);
        return new ResourceMetadata(
            StableEntityIds.ForResource(source.Id, packagePath, resourceKey),
            source.Id,
            source.Kind,
            packagePath,
            resourceKey,
            null,
            null,
            null,
            null,
            previewKind,
            previewKind is not PreviewKind.Unsupported,
            true,
            ResourceTypeHints.GetAssetLinkageSummary(resourceKey.TypeName),
            string.Empty);
    }

    private static bool ShouldParallelizeEnumeration(int resourceCount) =>
        resourceCount >= ParallelEnumerationThreshold &&
        GetEnumerationParallelism() > 1;

    private static int GetEnumerationParallelism() =>
        Math.Clamp(Environment.ProcessorCount, 1, 16);

    public async Task<ResourceMetadata> EnrichResourceAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        await using var package = await OpenPackageAsync(resource.PackagePath, cancellationToken).ConfigureAwait(false);
        var key = ToLlamaKey(resource.Key);

        string? name = resource.Name;
        long? uncompressedSize = resource.UncompressedSize;
        long? compressedSize = resource.CompressedSize;
        bool? isCompressed = resource.IsCompressed;
        var diagnostics = resource.Diagnostics;

        if (name is null)
        {
            try
            {
                name = await package.GetNameByKeyAsync(key, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                diagnostics = AppendDiagnostic(diagnostics, $"Name lookup failed: {ex.Message}");
            }
        }

        if (uncompressedSize is null)
        {
            try
            {
                uncompressedSize = await package.GetSizeAsync(key, cancellationToken).ConfigureAwait(false);
                compressedSize ??= uncompressedSize;
                isCompressed ??= false;
            }
            catch (Exception ex)
            {
                diagnostics = AppendDiagnostic(diagnostics, $"Size lookup failed: {ex.Message}");
            }
        }

        return resource with
        {
            Name = name,
            CompressedSize = compressedSize,
            UncompressedSize = uncompressedSize,
            IsCompressed = isCompressed,
            Diagnostics = diagnostics
        };
    }

    public async Task<byte[]> GetResourceBytesAsync(string packagePath, ResourceKeyRecord key, bool raw, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null)
    {
        progress?.Report(new ResourceReadProgress("Opening package...", 0.08));
        await using var package = await OpenPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var llamaKey = ToLlamaKey(key);
        progress?.Report(new ResourceReadProgress(raw ? "Reading raw resource bytes..." : "Reading resource bytes...", 0.35));
        byte[] bytes = raw
            ? await ReadWithDeletedFallbackAsync(force => package.GetRawAsync(llamaKey, force, cancellationToken), cancellationToken, progress).ConfigureAwait(false)
            : await ReadWithDeletedFallbackAsync(force => package.GetAsync(llamaKey, force, cancellationToken), cancellationToken, progress).ConfigureAwait(false);
        progress?.Report(new ResourceReadProgress("Resource bytes ready.", 1.0));
        return bytes;
    }

    public async Task<string?> GetTextAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken)
    {
        await using var package = await OpenPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var llamaKey = ToLlamaKey(key);

        try
        {
            var xml = await ReadWithDeletedFallbackAsync(force => package.GetXmlAsync(llamaKey, force, cancellationToken), cancellationToken).ConfigureAwait(false);
            return xml.ToString();
        }
        catch
        {
            try
            {
                return await ReadWithDeletedFallbackAsync(force => package.GetTextAsync(llamaKey, force, cancellationToken), cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }
    }

    public async Task<byte[]?> GetTexturePngAsync(string packagePath, ResourceKeyRecord key, CancellationToken cancellationToken, IProgress<ResourceReadProgress>? progress = null)
    {
        progress?.Report(new ResourceReadProgress("Opening package...", 0.08));
        await using var package = await OpenPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        var llamaKey = ToLlamaKey(key);

        if (key.TypeName is nameof(ResourceType.PNGImage) or nameof(ResourceType.PNGImage2))
        {
            progress?.Report(new ResourceReadProgress("Reading PNG image bytes...", 0.35));
            var bytes = await ReadWithDeletedFallbackAsync(force => package.GetAsync(llamaKey, force, cancellationToken), cancellationToken, progress).ConfigureAwait(false);
            progress?.Report(new ResourceReadProgress("Texture bytes ready.", 1.0));
            return bytes;
        }

        if (key.TypeName is nameof(ResourceType.BuyBuildThumbnail) or nameof(ResourceType.BodyPartThumbnail) or nameof(ResourceType.CASPartThumbnail))
        {
            progress?.Report(new ResourceReadProgress("Reading thumbnail bytes...", 0.35));
            var bytes = await ReadWithDeletedFallbackAsync(force => package.GetTranslucentJpegAsPngAsync(llamaKey, force, cancellationToken), cancellationToken, progress).ConfigureAwait(false);
            progress?.Report(new ResourceReadProgress("Texture bytes ready.", 1.0));
            return bytes;
        }

        if (ResourceTypeHints.IsDdsFamily(key.TypeName))
        {
            progress?.Report(new ResourceReadProgress("Reading DDS texture bytes...", 0.35));
            var bytes = await ReadWithDeletedFallbackAsync(force => package.GetDdsAsPngAsync(llamaKey, force, cancellationToken), cancellationToken, progress).ConfigureAwait(false);
            progress?.Report(new ResourceReadProgress("Texture bytes ready.", 1.0));
            return bytes;
        }

        progress?.Report(new ResourceReadProgress("Texture type is not previewable as PNG.", 1.0));
        return null;
    }

    private static async Task<byte[]> ReadWithDeletedFallbackAsync(
        Func<bool, Task<ReadOnlyMemory<byte>>> readAsync,
        CancellationToken cancellationToken,
        IProgress<ResourceReadProgress>? progress = null)
    {
        var memory = await ReadWithDeletedFallbackAsyncCore(readAsync, cancellationToken, progress).ConfigureAwait(false);
        return memory.ToArray();
    }

    private static async Task<T> ReadWithDeletedFallbackAsync<T>(
        Func<bool, Task<T>> readAsync,
        CancellationToken cancellationToken,
        IProgress<ResourceReadProgress>? progress = null)
    {
        return await ReadWithDeletedFallbackAsyncCore(readAsync, cancellationToken, progress).ConfigureAwait(false);
    }

    private static async Task<T> ReadWithDeletedFallbackAsyncCore<T>(
        Func<bool, Task<T>> readAsync,
        CancellationToken cancellationToken,
        IProgress<ResourceReadProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await readAsync(false).ConfigureAwait(false);
        }
        catch (Exception ex) when (LooksLikeDeletedResource(ex))
        {
            progress?.Report(new ResourceReadProgress("Retrying deleted-resource fallback...", 0.72));
            return await readAsync(true).ConfigureAwait(false);
        }
    }

    private static ResourceKeyRecord ToCoreKey(ResourceKey key) =>
        new((uint)key.Type, key.Group, key.FullInstance, key.Type.ToString());

    private static ResourceKey ToLlamaKey(ResourceKeyRecord key) =>
        new((ResourceType)key.Type, key.Group, key.FullInstance);

    private static string AppendDiagnostic(string existing, string message) =>
        string.IsNullOrWhiteSpace(existing) ? message : $"{existing} | {message}";

    private static bool LooksLikeDeletedResource(Exception ex) =>
        ex.Message.Contains("marked as deleted", StringComparison.OrdinalIgnoreCase);

    private static async Task<DataBasePackedFile> OpenPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        try
        {
            var stream = new FileStream(
                packagePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 131072,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            return await DataBasePackedFile.FromStreamAsync(stream, cancellationToken).ConfigureAwait(false);
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
