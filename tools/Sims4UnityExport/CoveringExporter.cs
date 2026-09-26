// ---------------------------------------------------------------------------
// CoveringExporter — exports WALL and FLOOR covering textures for the Home Editor (M2).
//
// TS4 covering catalog entries (Wall 0xD5F0F921 / Floor 0xB4F762C9) are tiny records that
// reference a MaterialDefinition by BARE u64 INSTANCE (proven by hex analysis: the floor
// record carries the MATD instance inline; blank walls carry none). The MATD then references
// the tiling diffuse texture. Neither reference is stored as a full TGI, so this exporter
// works the way our OBJD/door analysis did: scan candidate u64s / texture-type ids and
// VERIFY every candidate against the index — false positives don't survive verification.
//
// Output per kind under <UnityAssetsDir>/home/coverings/{walls|floors}/:
//   <instance>.png        — the tiling covering texture (largest verified texture in the MATD)
//   <instance>_thumb.png  — the game's own BuyBuildThumbnail (raw PNG/JPG bytes when present)
// plus coverings.json (walls/floors arrays: id, label, texture, thumb) consumed by the
// HomeEditorSceneBuilder.
// ---------------------------------------------------------------------------
using System.Buffers.Binary;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Sims4ResourceExplorer.Core;

namespace Sims4UnityExport;

internal static class CoveringExporter
{
    private static readonly uint[] TextureTypes = { 0x00B2D882, 0x2BC04EDF, 0x3453CF95, 0x00B552EA };

    public static async Task RunAsync(
        string unityAssetsDir,
        int countPerKind,
        IIndexStore indexStore,
        IResourceCatalogService catalogSvc,
        CancellationToken ct)
    {
        var root = Path.Combine(unityAssetsDir, "home", "coverings");
        Directory.CreateDirectory(root);

        var catalog = new Dictionary<string, List<Dictionary<string, string>>>
        {
            ["walls"] = new(),
            ["floors"] = new(),
        };

        foreach (var (typeName, typeId, kindKey) in new[]
                 {
                     ("Wall", 0xD5F0F921u, "walls"),
                     ("Floor", 0xB4F762C9u, "floors"),
                 })
        {
            var kindFolder = Path.Combine(root, kindKey);
            Directory.CreateDirectory(kindFolder);
            Console.WriteLine($"===== {typeName} coverings (target {countPerKind}) =====");

            var exported = 0;
            int missBytes = 0, missMatd = 0, missTex = 0, missPng = 0, errors = 0, tried = 0;
            foreach (var (instanceHex, packagePath) in EnumerateCoveringCandidates(typeName))
            {
                if (exported >= countPerKind)
                {
                    break;
                }
                ct.ThrowIfCancellationRequested();
                tried++;
                try
                {
                    var instance = Convert.ToUInt64(instanceHex, 16);
                    var key = new ResourceKeyRecord(typeId, 0, instance, typeName);
                    var bytes = await catalogSvc.GetResourceBytesAsync(packagePath, key, raw: false, ct);
                    if (bytes.Length < 24)
                    {
                        missBytes++;
                        continue;
                    }

                    // 1) covering record → MaterialDefinition (u64 instance scan + index verify)
                    var matd = await FindMaterialDefinitionAsync(bytes, indexStore, ct);
                    if (matd is null)
                    {
                        missMatd++;
                        continue; // blank/base covering with no material — skip silently
                    }

                    // 2) MATD → largest verified texture (the tiling diffuse)
                    var matdBytes = await catalogSvc.GetResourceBytesAsync(matd.PackagePath, matd.Key, raw: false, ct);
                    var texture = await FindLargestTextureAsync(matdBytes, indexStore, ct);
                    if (texture is null)
                    {
                        missTex++;
                        continue;
                    }

                    var png = await catalogSvc.GetTexturePngAsync(texture.PackagePath, texture.Key, ct);
                    if (png is not { Length: > 0 })
                    {
                        missPng++;
                        continue;
                    }

                    // The pattern textures are GRAYSCALE (avg ~131): the game multiplies them by a
                    // per-covering TINT (ARGB near the record tail; thumb color ≈ tint exactly, so
                    // mid-gray is neutral ⇒ out = pattern × tint × 2). Bake it so Unity is WYSIWYG.
                    var tint = FindTintColor(bytes);
                    if (tint is { } t && !(t.r > 250 && t.g > 250 && t.b > 250))
                    {
                        png = BakeTint(png, t.r, t.g, t.b);
                    }

                    var file = $"{instanceHex}.png";
                    await File.WriteAllBytesAsync(Path.Combine(kindFolder, file), png, ct);

                    // 3) the game's own thumbnail (raw bytes are PNG/JPG; fall back to the decoder)
                    var thumbFile = await TryWriteThumbAsync(instance, kindFolder, $"{instanceHex}_thumb", indexStore, catalogSvc, ct);
                    var thumbRel = thumbFile is null ? null : $"coverings/{kindKey}/{thumbFile}";

                    catalog[kindKey].Add(new Dictionary<string, string>
                    {
                        ["id"] = instanceHex,
                        ["label"] = $"{typeName} {instanceHex[^4..]}",
                        ["texture"] = $"coverings/{kindKey}/{file}",
                        ["thumb"] = thumbRel ?? string.Empty,
                        ["tint"] = tint is { } tc ? $"#{tc.r:X2}{tc.g:X2}{tc.b:X2}" : string.Empty,
                    });
                    exported++;
                    Console.WriteLine($"  [{exported}/{countPerKind}] {instanceHex}: tex {png.Length,8} B via {texture.Key.TypeName} tint={(tint is { } tl ? $"#{tl.r:X2}{tl.g:X2}{tl.b:X2}" : "none")} {(thumbRel != null ? "+thumb" : "")}");
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors++;
                    if (errors <= 3)
                    {
                        Console.Error.WriteLine($"  {instanceHex}: {ex.GetType().Name}: {ex.Message} — skipped.");
                    }
                }
            }
            Console.WriteLine($"{typeName}: exported {exported} (tried {tried}: tinyRecord={missBytes} noMatd={missMatd} noTexture={missTex} noPng={missPng} errors={errors}).");
        }

        var jsonPath = Path.Combine(root, "coverings.json");
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }), ct);
        Console.WriteLine($"Wrote {jsonPath}.");
    }

    /// <summary>The covering's tint color: the LAST alpha-0xFF ARGB u32 in the record's final 24
    /// bytes (walls carry it before a trailing hash, floors before a zero word; some records list
    /// it twice). Gray tints are legit — no saturation filter.</summary>
    private static (byte r, byte g, byte b)? FindTintColor(byte[] record)
    {
        (byte r, byte g, byte b)? found = null;
        for (var off = Math.Max(0, record.Length - 24); off + 4 <= record.Length; off++)
        {
            if (record[off + 3] == 0xFF)
            {
                found = (record[off + 2], record[off + 1], record[off]);
            }
        }
        return found;
    }

    /// <summary>out = clamp(pattern × tint × 2 / 255): mid-gray (131) patterns reproduce the tint
    /// almost exactly, matching the game's own thumbnails.</summary>
    private static byte[] BakeTint(byte[] png, byte tr, byte tg, byte tb)
    {
        using var img = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(png);
        img.ProcessPixelRows(rows =>
        {
            for (var y = 0; y < rows.Height; y++)
            {
                var row = rows.GetRowSpan(y);
                for (var x = 0; x < row.Length; x++)
                {
                    var p = row[x];
                    p.R = (byte)Math.Min(255, p.R * tr * 2 / 255);
                    p.G = (byte)Math.Min(255, p.G * tg * 2 / 255);
                    p.B = (byte)Math.Min(255, p.B * tb * 2 / 255);
                    row[x] = p;
                }
            }
        });
        using var ms = new MemoryStream();
        img.Save(ms, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
        return ms.ToArray();
    }

    // Distinct covering instances, LARGEST records first (blank base walls are tiny), base-game
    // client packages preferred. Raw sqlite over every shard (a package's rows live in ONE shard).
    private static IEnumerable<(string instanceHex, string packagePath)> EnumerateCoveringCandidates(string typeName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<(string inst, string pkg, long size)>();
        foreach (var dbPath in HairExporter.IndexShardPaths())
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT instance_hex, package_path, COALESCE(uncompressed_size, compressed_size, 0)
                FROM resources
                WHERE type_name = $tn AND package_path LIKE '%ClientFullBuild%'
                """;
            cmd.Parameters.AddWithValue("$tn", typeName);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                rows.Add((reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? 0 : reader.GetInt64(2)));
            }
        }
        foreach (var row in rows.OrderByDescending(r => r.size))
        {
            if (seen.Add(row.inst))
            {
                yield return (row.inst, row.pkg);
            }
        }
    }

    // Scan every u64 in the record; the first candidate the index confirms as a MaterialDefinition wins.
    private static async Task<ResourceMetadata?> FindMaterialDefinitionAsync(byte[] bytes, IIndexStore indexStore, CancellationToken ct)
    {
        var candidates = new List<ulong>();
        var seen = new HashSet<ulong>();
        for (var off = 0; off + 8 <= bytes.Length; off++)
        {
            var v = BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(off, 8));
            if (v > 0xFFFFFFFFUL && seen.Add(v))
            {
                candidates.Add(v);
            }
        }
        // Covering records are tiny (≤ a few hundred bytes) but the reference can sit at ANY byte
        // offset (0x55 in the probed floor — an unaligned position past the first 48 sliding-window
        // candidates; a lower cap silently missed EVERY floor). Scan them all.
        foreach (var candidate in candidates.Take(512))
        {
            var rows = await indexStore.GetResourcesByFullInstanceAsync(candidate, ct);
            var hit = rows.FirstOrDefault(r => string.Equals(r.Key.TypeName, "MaterialDefinition", StringComparison.Ordinal));
            if (hit is not null)
            {
                return hit;
            }
        }
        return null;
    }

    // Scan MATD bytes for known texture-type ids in BOTH TGI layouts, verify against the index,
    // return the largest verified texture resource (the tiling diffuse dwarfs specular/bump).
    private static async Task<ResourceMetadata?> FindLargestTextureAsync(byte[] bytes, IIndexStore indexStore, CancellationToken ct)
    {
        var candidates = new HashSet<ulong>();
        foreach (var typeId in TextureTypes)
        {
            var pattern = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(pattern, typeId);
            for (var off = 0; off + 4 <= bytes.Length; off++)
            {
                if (!bytes.AsSpan(off, 4).SequenceEqual(pattern))
                {
                    continue;
                }
                if (off + 16 <= bytes.Length) // [type][group][instance]
                {
                    candidates.Add(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(off + 8, 8)));
                }
                if (off >= 12) // [instance][group][type]
                {
                    candidates.Add(BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(off - 12, 8)));
                }
            }
        }

        ResourceMetadata? best = null;
        long bestSize = -1;
        foreach (var candidate in candidates.Take(64))
        {
            if (candidate <= 0xFFFF)
            {
                continue;
            }
            var rows = await indexStore.GetResourcesByFullInstanceAsync(candidate, ct);
            foreach (var row in rows)
            {
                if (!TextureTypes.Contains(row.Key.Type))
                {
                    continue;
                }
                var size = row.UncompressedSize ?? row.CompressedSize ?? 0;
                if (size > bestSize)
                {
                    best = row;
                    bestSize = size;
                }
            }
        }
        return best;
    }

    /// <summary>Write an entity's game-shipped BuyBuildThumbnail (shared by covering AND object
    /// exports). Returns the written FILE NAME (jpg keeps its extension so Unity imports it), or
    /// null when the entity has no decodable thumbnail.</summary>
    internal static async Task<string?> TryWriteThumbAsync(
        ulong instance, string outFolder, string fileBaseName,
        IIndexStore indexStore, IResourceCatalogService catalogSvc, CancellationToken ct)
    {
        try
        {
            var rows = await indexStore.GetResourcesByFullInstanceAsync(instance, ct);
            var thumb = rows
                .Where(r => string.Equals(r.Key.TypeName, "BuyBuildThumbnail", StringComparison.Ordinal))
                .OrderByDescending(r => r.Key.Group) // higher groups = larger/latest variants
                .FirstOrDefault();
            if (thumb is null)
            {
                return null;
            }
            var raw = await catalogSvc.GetResourceBytesAsync(thumb.PackagePath, thumb.Key, raw: false, ct);
            string file;
            byte[]? payload;
            if (raw.Length > 8 && raw[0] == 0x89 && raw[1] == 0x50) // PNG magic
            {
                payload = raw;
                file = $"{fileBaseName}.png";
            }
            else if (raw.Length > 3 && raw[0] == 0xFF && raw[1] == 0xD8) // JPG magic
            {
                payload = raw;
                file = $"{fileBaseName}.jpg";
            }
            else
            {
                payload = await catalogSvc.GetTexturePngAsync(thumb.PackagePath, thumb.Key, ct);
                file = $"{fileBaseName}.png";
            }
            if (payload is not { Length: > 0 })
            {
                return null;
            }
            await File.WriteAllBytesAsync(Path.Combine(outFolder, file), payload, ct);
            return file;
        }
        catch
        {
            return null;
        }
    }
}
