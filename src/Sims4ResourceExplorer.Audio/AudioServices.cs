using NAudio.Wave;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Audio;

public sealed class BasicAudioDecodeService : IAudioDecodeService
{
    private readonly IResourceCatalogService resourceCatalogService;

    public BasicAudioDecodeService(IResourceCatalogService resourceCatalogService)
    {
        this.resourceCatalogService = resourceCatalogService;
    }

    public async Task<AudioDecodeResult> DecodeAsync(ResourceMetadata resource, CancellationToken cancellationToken)
    {
        var bytes = await resourceCatalogService.GetResourceBytesAsync(resource.PackagePath, resource.Key, raw: false, cancellationToken);

        if (LooksLikeWave(bytes))
        {
            return new AudioDecodeResult(true, bytes, "Decoded as RIFF/WAV.");
        }

        return new AudioDecodeResult(false, null, "Audio decoding is currently limited to RIFF/WAV payloads. Raw export remains available.");
    }

    private static bool LooksLikeWave(ReadOnlySpan<byte> bytes) =>
        bytes.Length > 12 &&
        bytes[0] == (byte)'R' &&
        bytes[1] == (byte)'I' &&
        bytes[2] == (byte)'F' &&
        bytes[3] == (byte)'F' &&
        bytes[8] == (byte)'W' &&
        bytes[9] == (byte)'A' &&
        bytes[10] == (byte)'V' &&
        bytes[11] == (byte)'E';
}

public sealed class WaveOutAudioPlayer : IAudioPlayer
{
    private WaveOutEvent? output;
    private WaveStream? currentStream;

    public Task PlayAsync(byte[] wavBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopInternal();

        var memoryStream = new MemoryStream(wavBytes, writable: false);
        currentStream = new WaveFileReader(memoryStream);
        output = new WaveOutEvent();
        output.Init(currentStream);
        output.Play();
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        StopInternal();
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        StopInternal();
        return ValueTask.CompletedTask;
    }

    private void StopInternal()
    {
        output?.Stop();
        output?.Dispose();
        output = null;

        currentStream?.Dispose();
        currentStream = null;
    }
}
