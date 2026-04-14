using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview;

public sealed class BasicTextureDecodeService : ITextureDecodeService
{
    private readonly IResourceCatalogService resourceCatalogService;

    public BasicTextureDecodeService(IResourceCatalogService resourceCatalogService)
    {
        this.resourceCatalogService = resourceCatalogService;
    }

    public async Task<TextureDecodeResult> DecodeAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        try
        {
            var pngBytes = await resourceCatalogService.GetTexturePngAsync(resource.PackagePath, resource.Key, cancellationToken);
            if (pngBytes is null)
            {
                return new TextureDecodeResult(false, null, null, null, null, null, "Texture decode unsupported for this resource type.");
            }

            var (width, height) = TryReadPngSize(pngBytes);
            return new TextureDecodeResult(true, pngBytes, width, height, "PNG", null, string.Empty);
        }
        catch (Exception ex)
        {
            return new TextureDecodeResult(false, null, null, null, null, null, ex.Message);
        }
    }

    private static (int? Width, int? Height) TryReadPngSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24)
        {
            return (null, null);
        }

        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        if (!bytes[..8].SequenceEqual(signature))
        {
            return (null, null);
        }

        var width = ReadBigEndianInt(bytes.Slice(16, 4));
        var height = ReadBigEndianInt(bytes.Slice(20, 4));
        return (width, height);
    }

    private static int ReadBigEndianInt(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
}

public sealed class PlaceholderSceneBuildService : ISceneBuildService
{
    public Task<SceneBuildResult> BuildSceneAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new SceneBuildResult(
            false,
            null,
            [$"Scene reconstruction for {resource.Key.TypeName} is not implemented yet. Metadata, diagnostics, and raw export remain available."],
            SceneBuildStatus.Unsupported));
    }
}

public sealed class ResourcePreviewService : IPreviewService
{
    private readonly IResourceCatalogService resourceCatalogService;
    private readonly ITextureDecodeService textureDecodeService;
    private readonly ISceneBuildService sceneBuildService;
    private readonly IAudioDecodeService audioDecodeService;

    public ResourcePreviewService(
        IResourceCatalogService resourceCatalogService,
        ITextureDecodeService textureDecodeService,
        ISceneBuildService sceneBuildService,
        IAudioDecodeService audioDecodeService)
    {
        this.resourceCatalogService = resourceCatalogService;
        this.textureDecodeService = textureDecodeService;
        this.sceneBuildService = sceneBuildService;
        this.audioDecodeService = audioDecodeService;
    }

    public async Task<PreviewResult> CreatePreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress = null)
    {
        switch (resource.PreviewKind)
        {
            case PreviewKind.Text:
                return await CreateTextPreviewAsync(resource, cancellationToken);

            case PreviewKind.Texture:
                return await CreateTexturePreviewAsync(resource, cancellationToken);

            case PreviewKind.Scene:
                return await CreateScenePreviewAsync(resource, cancellationToken, progress);

            case PreviewKind.Audio:
                return await CreateAudioPreviewAsync(resource, cancellationToken);

            case PreviewKind.Hex:
            default:
                return await CreateHexPreviewAsync(resource, cancellationToken);
        }
    }

    private async Task<PreviewResult> CreateTextPreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var text = await resourceCatalogService.GetTextAsync(resource.PackagePath, resource.Key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(text))
        {
            return new PreviewResult(PreviewKind.Text, new TextPreviewContent(resource, text));
        }

        return await CreateHexPreviewAsync(resource, cancellationToken);
    }

    private async Task<PreviewResult> CreateTexturePreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var decoded = await textureDecodeService.DecodeAsync(resource, cancellationToken);
        if (decoded.Success && decoded.PngBytes is not null)
        {
            return new PreviewResult(
                PreviewKind.Texture,
                new TexturePreviewContent(resource, decoded.PngBytes, decoded.Width, decoded.Height, decoded.PixelFormat, decoded.MipLevels, decoded.Diagnostics));
        }

        return new PreviewResult(PreviewKind.Unsupported, new UnsupportedPreviewContent(resource, decoded.Diagnostics));
    }

    private async Task<PreviewResult> CreateScenePreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken, IProgress<PreviewBuildProgress>? progress)
    {
        progress?.Report(new PreviewBuildProgress("Preparing scene build...", 0.02));
        var result = await sceneBuildService.BuildSceneAsync(resource, cancellationToken, progress);
        progress?.Report(new PreviewBuildProgress("Scene build complete.", 1.0));
        return new PreviewResult(PreviewKind.Scene, new ScenePreviewContent(resource, result.Scene, string.Join(Environment.NewLine, result.Diagnostics), result.Status));
    }

    private async Task<PreviewResult> CreateAudioPreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var decoded = await audioDecodeService.DecodeAsync(resource, cancellationToken);
        return new PreviewResult(PreviewKind.Audio, new AudioPreviewContent(resource, decoded.WavBytes, decoded.Diagnostics, decoded.Success));
    }

    private async Task<PreviewResult> CreateHexPreviewAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken);
        return new PreviewResult(PreviewKind.Hex, new HexPreviewContent(resource, BuildHexDump(bytes)));
    }

    private static string BuildHexDump(ReadOnlySpan<byte> bytes)
    {
        var limit = Math.Min(bytes.Length, 512);
        var lines = new List<string>();

        for (var offset = 0; offset < limit; offset += 16)
        {
            var slice = bytes.Slice(offset, Math.Min(16, limit - offset));
            var hex = string.Join(" ", slice.ToArray().Select(static b => b.ToString("X2")));
            var ascii = new string(slice.ToArray().Select(static b => b is >= 32 and <= 126 ? (char)b : '.').ToArray());
            lines.Add($"{offset:X8}  {hex,-47}  {ascii}");
        }

        if (bytes.Length > limit)
        {
            lines.Add($"... truncated after {limit} bytes of {bytes.Length} total bytes.");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
