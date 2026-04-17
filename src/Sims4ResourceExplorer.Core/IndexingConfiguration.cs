namespace Sims4ResourceExplorer.Core;

public sealed record SystemMemorySnapshot(ulong TotalPhysicalBytes, ulong AvailablePhysicalBytes)
{
    public ulong SafeAvailablePhysicalBytes =>
        (ulong)Math.Floor(AvailablePhysicalBytes * 0.85d);
}

public static class IndexingMemoryBudgetCalculator
{
    private static readonly int[] SupportedPercentValues = [10, 25, 50, 75, 100];
    private const int DefaultPercent = 25;

    public static IReadOnlyList<int> SupportedPercents => SupportedPercentValues;

    public static int NormalizePercent(int value) =>
        SupportedPercentValues.Contains(value) ? value : DefaultPercent;

    public static long ResolvePackageByteCacheBudgetBytes(SystemMemorySnapshot snapshot, int requestedPercent)
    {
        var percent = NormalizePercent(requestedPercent);
        if (snapshot.TotalPhysicalBytes == 0)
        {
            return 0;
        }

        var requestedBytes = (ulong)Math.Floor(snapshot.TotalPhysicalBytes * (percent / 100d));
        var cappedBytes = Math.Min(requestedBytes, snapshot.SafeAvailablePhysicalBytes);
        return cappedBytes > long.MaxValue ? long.MaxValue : (long)cappedBytes;
    }

    public static string BuildBudgetSummary(SystemMemorySnapshot snapshot, int requestedPercent)
    {
        var percent = NormalizePercent(requestedPercent);
        var budgetBytes = ResolvePackageByteCacheBudgetBytes(snapshot, percent);
        return $"Package-read cache: up to {FormatBytes((ulong)Math.Max(0, budgetBytes))} ({percent}% target, capped by currently available RAM). Available now: {FormatBytes(snapshot.AvailablePhysicalBytes)} / {FormatBytes(snapshot.TotalPhysicalBytes)}.";
    }

    private static string FormatBytes(ulong bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;

        return bytes switch
        {
            0 => "0 B",
            < 1024UL => $"{bytes:N0} B",
            < 1024UL * 1024UL => $"{bytes / kib:0.0} KiB",
            < 1024UL * 1024UL * 1024UL => $"{bytes / mib:0.0} MiB",
            _ => $"{bytes / gib:0.00} GiB"
        };
    }
}

public static class SourcePathClassifier
{
    public static SourceKind InferKind(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            return SourceKind.Game;
        }

        var normalized = rootPath
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .TrimEnd(Path.DirectorySeparatorChar);
        var segments = normalized.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Any(static segment => string.Equals(segment, "Mods", StringComparison.OrdinalIgnoreCase)))
        {
            return SourceKind.Mods;
        }

        if (segments.Any(IsPackSegment))
        {
            return SourceKind.Dlc;
        }

        return SourceKind.Game;
    }

    public static string BuildDisplayName(string rootPath, SourceKind kind)
    {
        var normalized = rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var leaf = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(leaf))
        {
            leaf = normalized;
        }

        return $"{GetKindLabel(kind)}: {leaf}";
    }

    private static bool IsPackSegment(string segment)
    {
        if (segment.Length != 4)
        {
            return false;
        }

        if (!char.IsLetter(segment[0]) || !char.IsLetter(segment[1]))
        {
            return false;
        }

        if (!char.IsDigit(segment[2]) || !char.IsDigit(segment[3]))
        {
            return false;
        }

        var prefix = segment[..2];
        return prefix.Equals("EP", StringComparison.OrdinalIgnoreCase) ||
               prefix.Equals("GP", StringComparison.OrdinalIgnoreCase) ||
               prefix.Equals("SP", StringComparison.OrdinalIgnoreCase) ||
               prefix.Equals("FP", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetKindLabel(SourceKind kind) => kind switch
    {
        SourceKind.Game => "Game",
        SourceKind.Dlc => "DLC",
        SourceKind.Mods => "Mods",
        _ => kind.ToString()
    };
}
