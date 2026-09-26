// CatalogProbe — one-shot DISCOVERY helper used to populate catalog.json with real vetted
// Adult-Female options. It enumerates the EA Human skintones (TONE resources) and the EA
// EyeColor CAS parts (body_type 4) from the prebuilt index, printing their instances + names
// so the human author can pick a handful for the catalog. Investigation only; it changes no
// export. (Kept in-tree so the catalog can be re-derived if the index changes.)

using Sims4ResourceExplorer.Assets;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class CatalogProbe
{
    // body_type 35 = EyeColor (per AssetServices BodyTypeToRegion + Ts4SimInfoBuilder); 34 = Brows.
    private const int EyeColorBodyType = 35;

    public static async Task RunAsync(
        ISyntheticSimService synthetic,
        IResourceCatalogService catalog,
        int maxTones,
        int maxEyes,
        CancellationToken ct)
    {
        Console.WriteLine("============================================================");
        Console.WriteLine("catalogprobe: enumerating EA Human skintones (TONE)...");
        Console.WriteLine("============================================================");

        IReadOnlyList<SkintoneOption> tones;
        try
        {
            tones = await synthetic.EnumerateSkintonesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EnumerateSkintonesAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            tones = Array.Empty<SkintoneOption>();
        }

        Console.WriteLine($"Total skintones enumerated: {tones.Count}. Showing up to {maxTones}.");
        var shownTones = 0;
        foreach (var t in tones)
        {
            var argb = t.SwatchArgb is { } a ? $"#{a:X8}" : "(none)";
            Console.WriteLine(
                $"  TONE inst=0x{t.Instance:X16}  swatch={argb}  name='{t.DisplayName}'  pkg={Path.GetFileName(t.PackagePath)}  tgi={t.FullTgi}");
            if (++shownTones >= maxTones)
            {
                Console.WriteLine($"  ... ({tones.Count - shownTones} more not shown)");
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("============================================================");
        Console.WriteLine($"catalogprobe: enumerating EA EyeColor CAS parts (body_type {EyeColorBodyType})...");
        Console.WriteLine("============================================================");

        IReadOnlyList<ResourceMetadata> eyes;
        try
        {
            eyes = await synthetic.EnumerateCasPartsByBodyTypeAsync(EyeColorBodyType, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"EnumerateCasPartsByBodyTypeAsync FAILED ({ex.GetType().Name}: {ex.Message}).");
            eyes = Array.Empty<ResourceMetadata>();
        }

        Console.WriteLine($"Total EyeColor CAS parts enumerated: {eyes.Count}. Showing up to {maxEyes}.");
        var shownEyes = 0;
        foreach (var r in eyes)
        {
            // Best-effort internal name.
            string? name = null;
            try
            {
                var bytes = await catalog.GetResourceBytesAsync(r.PackagePath, r.Key, raw: false, ct).ConfigureAwait(false);
                name = Ts4CasPart.Parse(bytes).InternalName;
            }
            catch { /* name stays null */ }

            Console.WriteLine(
                $"  EYE inst=0x{r.Key.FullInstance:X16}  name='{name ?? r.Name ?? "(unnamed)"}'  pkg={Path.GetFileName(r.PackagePath)}  tgi={r.Key.FullTgi}");
            if (++shownEyes >= maxEyes)
            {
                Console.WriteLine($"  ... ({eyes.Count - shownEyes} more not shown)");
                break;
            }
        }

        Console.WriteLine();
        Console.WriteLine("catalogprobe DONE.");
    }
}
