using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Sims4ResourceExplorer.App.Rendering.Vortice;

/// <summary>
/// Decodes PNG bytes to tightly-packed BGRA8 via <see cref="BitmapDecoder"/> (no System.Drawing),
/// ready to feed a D3D11 <c>Texture2D</c> with <c>Format.B8G8R8A8_UNorm</c> and rowPitch = w*4.
/// </summary>
internal static class TextureLoader
{
    public static async Task<(byte[] Bgra, int Width, int Height)> DecodeBgraAsync(byte[] pngBytes)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(pngBytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Straight,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage);

        byte[] bgra = pixelData.DetachPixelData();
        return (bgra, (int)decoder.PixelWidth, (int)decoder.PixelHeight);
    }
}
