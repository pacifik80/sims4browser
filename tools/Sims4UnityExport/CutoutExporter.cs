// ---------------------------------------------------------------------------
// CutoutExporter — extracts the game's OWN wall-cut silhouettes for doors/windows (M3.6).
//
// TS4 ships a `ModelCutout` resource (0x07576A17) PER cutting model, keyed by the MODL
// instance (group 0): u32 magic (echoes the type id), u32 version=1, u32 segmentCount, then
// segmentCount × [A.xyz][B.xyz] floats — line segments in wall-plane METRES (x centered on the
// object, y up from the authored floor, z always 0). Segments chain into closed loops: one is
// always the hole's axis-aligned bounding rect, the other the exact silhouette (identical for
// plain rectangular openings; the arch window's has a real 4-segment arch). Decoded polygons
// land as cutout.json next to each item's manifest; the Unity builder feeds them to the wall
// cutter so an arched door cuts an ARCH, not its bounding box.
// ---------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Text.Json;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class CutoutExporter
{
    public static async Task RunAsync(
        string unityAssetsDir,
        IIndexStore indexStore,
        IResourceCatalogService catalogSvc,
        CancellationToken ct)
    {
        var homeRoot = Path.Combine(unityAssetsDir, "home");
        if (!Directory.Exists(homeRoot))
        {
            Console.Error.WriteLine($"No home export folder at {homeRoot}.");
            return;
        }

        int written = 0, noModel = 0, noCutout = 0, badDecode = 0;
        foreach (var folder in Directory.GetDirectories(homeRoot))
        {
            ct.ThrowIfCancellationRequested();
            var manifestPath = Path.Combine(folder, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                continue; // coverings/ and other non-item folders
            }
            var slug = Path.GetFileName(folder);

            ulong modelInstance;
            try
            {
                using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, ct));
                var tgi = doc.RootElement.GetProperty("sources").EnumerateArray()
                    .Where(s => s.TryGetProperty("TypeName", out var t) && t.GetString() == "Model")
                    .Select(s => s.GetProperty("FullTgi").GetString())
                    .FirstOrDefault();
                if (tgi is null)
                {
                    noModel++;
                    continue;
                }
                modelInstance = Convert.ToUInt64(tgi.Split(':')[^1], 16);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.Error.WriteLine($"  {slug}: manifest unreadable ({ex.GetType().Name}) — skipped.");
                noModel++;
                continue;
            }

            // Same-instance lookup; Delta packages override FullBuild, and later patches tombstone
            // stale copies to 0 bytes — the largest Delta-first row is the live one.
            var rows = await indexStore.GetResourcesByFullInstanceAsync(modelInstance, ct);
            var cutoutRes = rows
                .Where(r => string.Equals(r.Key.TypeName, "ModelCutout", StringComparison.Ordinal))
                .OrderByDescending(r => r.PackagePath.Contains("Delta", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ThenByDescending(r => r.UncompressedSize ?? r.CompressedSize ?? 0)
                .FirstOrDefault();
            if (cutoutRes is null)
            {
                noCutout++;
                continue; // plain furniture, or CC without cutout data — runtime falls back to bounds
            }

            var bytes = await catalogSvc.GetResourceBytesAsync(cutoutRes.PackagePath, cutoutRes.Key, raw: false, ct);
            var polygon = DecodeSilhouette(bytes);
            if (polygon is null || polygon.Count < 3)
            {
                badDecode++;
                Console.Error.WriteLine($"  {slug}: ModelCutout present but not decodable ({bytes.Length} B) — skipped.");
                continue;
            }

            // Point objects (not pairs): Unity's JsonUtility deserializes them straight into
            // List<Vector2> on the builder side.
            var json = JsonSerializer.Serialize(new
            {
                asset = $"home/{slug}",
                source = $"{cutoutRes.Key.FullTgi} @ {Path.GetFileName(cutoutRes.PackagePath)}",
                points = polygon.Select(p => new { p.x, p.y }).ToArray(),
            }, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(folder, "cutout.json"), json, ct);
            written++;
            var xs = polygon.Select(p => p.x).ToList();
            var ys = polygon.Select(p => p.y).ToList();
            Console.WriteLine($"  {slug}: {polygon.Count} pts, x [{xs.Min():0.###}..{xs.Max():0.###}], y [{ys.Min():0.###}..{ys.Max():0.###}]");
        }
        Console.WriteLine($"Cutouts: wrote {written} (noModelRef={noModel} noCutoutResource={noCutout} badDecode={badDecode}).");
    }

    /// <summary>Decode a ModelCutout payload and return the silhouette loop (the loop with the
    /// most vertices — ties, e.g. the double-rect case, resolve to either).</summary>
    internal static List<(float x, float y)>? DecodeSilhouette(byte[] b)
    {
        if (b.Length < 12)
        {
            return null;
        }
        var count = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8, 4));
        if (count < 3 || count > 4096 || 12 + (count * 24) > b.Length)
        {
            return null;
        }

        var segs = new List<((float x, float y) a, (float x, float y) b)>();
        for (var i = 0; i < count; i++)
        {
            var off = 12 + (i * 24);
            float F(int o) => BitConverter.ToSingle(b, off + o);
            segs.Add(((F(0), F(4)), (F(12), F(16)))); // z (offsets 8/20) is always 0 — wall plane
        }

        // Chain unordered segments into closed loops (endpoint matching, either direction).
        const float eps = 1e-3f;
        static bool Same((float x, float y) p, (float x, float y) q) =>
            Math.Abs(p.x - q.x) < eps && Math.Abs(p.y - q.y) < eps;

        var used = new bool[segs.Count];
        var loops = new List<List<(float x, float y)>>();
        for (var start = 0; start < segs.Count; start++)
        {
            if (used[start])
            {
                continue;
            }
            used[start] = true;
            var loop = new List<(float x, float y)> { segs[start].a, segs[start].b };
            var guard = 0;
            while (guard++ <= segs.Count)
            {
                var cur = loop[^1];
                if (loop.Count > 2 && Same(cur, loop[0]))
                {
                    loop.RemoveAt(loop.Count - 1); // closing point duplicates the first
                    break;
                }
                var extended = false;
                for (var i = 0; i < segs.Count && !extended; i++)
                {
                    if (used[i])
                    {
                        continue;
                    }
                    if (Same(segs[i].a, cur))
                    {
                        loop.Add(segs[i].b);
                        used[i] = true;
                        extended = true;
                    }
                    else if (Same(segs[i].b, cur))
                    {
                        loop.Add(segs[i].a);
                        used[i] = true;
                        extended = true;
                    }
                }
                if (!extended)
                {
                    break; // open chain (shouldn't happen) — keep what we have
                }
            }
            if (loop.Count >= 3)
            {
                loops.Add(loop);
            }
        }
        return loops.Count == 0 ? null : loops.OrderByDescending(l => l.Count).First();
    }
}
