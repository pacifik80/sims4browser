using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

return args[0] switch
{
    "listen" => await ListenCommand.RunAsync(args.Skip(1).ToArray()),
    "summarize" => await SummarizeCommand.RunAsync(args.Skip(1).ToArray()),
    "compare" => await CompareCommand.RunAsync(args.Skip(1).ToArray()),
    "compare-groups" => await CompareGroupsCommand.RunAsync(args.Skip(1).ToArray()),
    "catalog" => await CatalogCommand.RunAsync(args.Skip(1).ToArray()),
    _ => ExitWithUsage($"Unknown command: {args[0]}"),
};

static int ExitWithUsage(string error)
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    PrintUsage();
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("Ts4Dx11Companion listen --output <captureDir> [--pipe-name ts4-dx11-introspection-v1]");
    Console.WriteLine("Ts4Dx11Companion summarize --input <captureDir> [--output <report.md>]");
    Console.WriteLine("Ts4Dx11Companion compare --left <captureDir> --right <captureDir> [--output <report.md>]");
    Console.WriteLine("Ts4Dx11Companion compare-groups --left <captureDir> [--left <captureDir> ...] --right <captureDir> [--right <captureDir> ...] [--output <report.md>]");
    Console.WriteLine("Ts4Dx11Companion catalog --input <captureDir> [--input <captureDir> ...] [--output-md <catalog.md>] [--output-json <catalog.json>]");
}

internal static class ListenCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var pipeName, out var outputDirectory, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        Directory.CreateDirectory(outputDirectory);
        var sessionId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var sessionDirectory = Path.Combine(outputDirectory, sessionId);
        Directory.CreateDirectory(sessionDirectory);

        var router = new JsonlRouter(sessionDirectory);
        Console.WriteLine($"Listening on \\\\.\\pipe\\{pipeName}");
        Console.WriteLine($"Writing session logs to {sessionDirectory}");

        while (true)
        {
            await using var pipe = new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipe.WaitForConnectionAsync();
            Console.WriteLine($"Client connected at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");

            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                router.Route(line);
            }

            Console.WriteLine("Client disconnected.");
            await router.FlushAsync();
        }
    }

    private static bool TryParse(string[] args, out string pipeName, out string outputDirectory, out string error)
    {
        pipeName = "ts4-dx11-introspection-v1";
        outputDirectory = string.Empty;
        error = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--pipe-name" when index + 1 < args.Length:
                    pipeName = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    outputDirectory = Path.GetFullPath(args[++index]);
                    break;
                default:
                    error = $"Unknown or incomplete argument: {args[index]}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            error = "Missing required --output argument.";
            return false;
        }

        return true;
    }
}

internal static class SummarizeCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var inputDirectory, out var outputPath, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var capture = await CaptureReader.LoadAsync(inputDirectory);
        if (capture.UniqueShaders.Count == 0)
        {
            Console.Error.WriteLine($"No shader records found in {inputDirectory}");
            return 1;
        }

        Console.WriteLine($"Capture: {inputDirectory}");
        Console.WriteLine($"Unique shaders: {capture.UniqueShaders.Count}");
        Console.WriteLine($"Shader events: {capture.ShaderEvents.Count}");
        Console.WriteLine($"Frame boundaries logged: {capture.FrameCount}");
        Console.WriteLine($"State definitions: {capture.StateDefinitionCount}");
        Console.WriteLine();

        Console.WriteLine("Unique shaders by stage:");
        foreach (var stageCount in capture.UniqueShaders.Values
                     .GroupBy(shader => shader.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{stageCount.Key,2}  {stageCount.Count(),4}");
        }

        Console.WriteLine();
        Console.WriteLine("Top repeated shader hashes:");
        foreach (var entry in capture.ShaderEvents
                     .GroupBy(shader => shader.Hash, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            var stage = entry.First().Stage;
            Console.WriteLine($"{stage,2}  {entry.Count(),4}  {entry.Key}");
        }

        Console.WriteLine();
        Console.WriteLine("Top reflection signatures:");
        foreach (var signatureGroup in capture.UniqueShaders.Values
                     .GroupBy(shader => shader.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(15))
        {
            Console.WriteLine($"{signatureGroup.Count(),4}  {signatureGroup.Key}");
        }

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, ReportFormatter.BuildSummaryMarkdown(inputDirectory, capture), Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"Wrote summary report: {outputPath}");
        }

        return 0;
    }

    private static bool TryParse(string[] args, out string inputDirectory, out string outputPath, out string error)
    {
        inputDirectory = string.Empty;
        outputPath = string.Empty;
        error = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--input" when index + 1 < args.Length:
                    inputDirectory = Path.GetFullPath(args[++index]);
                    break;
                case "--output" when index + 1 < args.Length:
                    outputPath = Path.GetFullPath(args[++index]);
                    break;
                default:
                    error = $"Unknown or incomplete argument: {args[index]}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(inputDirectory) || !Directory.Exists(inputDirectory))
        {
            error = $"Input directory does not exist: {inputDirectory}";
            return false;
        }

        return true;
    }
}

internal static class CompareCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var leftDirectory, out var rightDirectory, out var outputPath, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var left = await CaptureReader.LoadAsync(leftDirectory);
        var right = await CaptureReader.LoadAsync(rightDirectory);
        if (left.UniqueShaders.Count == 0 || right.UniqueShaders.Count == 0)
        {
            Console.Error.WriteLine("Both captures must contain shader records.");
            return 1;
        }

        var leftHashes = left.UniqueShaders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightHashes = right.UniqueShaders.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var common = leftHashes.Intersect(rightHashes, StringComparer.OrdinalIgnoreCase).ToArray();
        var leftOnly = leftHashes.Except(rightHashes, StringComparer.OrdinalIgnoreCase).ToArray();
        var rightOnly = rightHashes.Except(leftHashes, StringComparer.OrdinalIgnoreCase).ToArray();

        Console.WriteLine($"Left:  {leftDirectory}");
        Console.WriteLine($"Right: {rightDirectory}");
        Console.WriteLine();
        Console.WriteLine($"Common shader hashes: {common.Length}");
        Console.WriteLine($"Left-only shader hashes: {leftOnly.Length}");
        Console.WriteLine($"Right-only shader hashes: {rightOnly.Length}");
        Console.WriteLine();

        PrintStageCounts("Left-only shaders by stage", leftOnly.Select(hash => left.UniqueShaders[hash]));
        Console.WriteLine();
        PrintStageCounts("Right-only shaders by stage", rightOnly.Select(hash => right.UniqueShaders[hash]));
        Console.WriteLine();

        PrintSignatureCounts("Top left-only reflection signatures", leftOnly.Select(hash => left.UniqueShaders[hash]));
        Console.WriteLine();
        PrintSignatureCounts("Top right-only reflection signatures", rightOnly.Select(hash => right.UniqueShaders[hash]));

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(
                outputPath,
                ReportFormatter.BuildCompareMarkdown(leftDirectory, rightDirectory, left, right, common, leftOnly, rightOnly),
                Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"Wrote compare report: {outputPath}");
        }

        return 0;
    }

    private static void PrintStageCounts(string title, IEnumerable<ShaderRecord> shaders)
    {
        Console.WriteLine(title + ":");
        foreach (var stageGroup in shaders
                     .GroupBy(shader => shader.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{stageGroup.Key,2}  {stageGroup.Count(),4}");
        }
    }

    private static void PrintSignatureCounts(string title, IEnumerable<ShaderRecord> shaders)
    {
        Console.WriteLine(title + ":");
        foreach (var signatureGroup in shaders
                     .GroupBy(shader => shader.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(15))
        {
            Console.WriteLine($"{signatureGroup.Count(),4}  {signatureGroup.Key}");
        }
    }

    private static bool TryParse(string[] args, out string leftDirectory, out string rightDirectory, out string outputPath, out string error)
    {
        leftDirectory = string.Empty;
        rightDirectory = string.Empty;
        outputPath = string.Empty;
        error = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--left" when index + 1 < args.Length:
                    leftDirectory = Path.GetFullPath(args[++index]);
                    break;
                case "--right" when index + 1 < args.Length:
                    rightDirectory = Path.GetFullPath(args[++index]);
                    break;
                case "--output" when index + 1 < args.Length:
                    outputPath = Path.GetFullPath(args[++index]);
                    break;
                default:
                    error = $"Unknown or incomplete argument: {args[index]}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(leftDirectory) || !Directory.Exists(leftDirectory))
        {
            error = $"Left input directory does not exist: {leftDirectory}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(rightDirectory) || !Directory.Exists(rightDirectory))
        {
            error = $"Right input directory does not exist: {rightDirectory}";
            return false;
        }

        return true;
    }
}

internal static class CompareGroupsCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var leftDirectories, out var rightDirectories, out var outputPath, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var leftCaptures = await LoadCapturesAsync(leftDirectories);
        var rightCaptures = await LoadCapturesAsync(rightDirectories);

        var leftUnique = AggregateUniqueShaders(leftCaptures);
        var rightUnique = AggregateUniqueShaders(rightCaptures);
        if (leftUnique.Count == 0 || rightUnique.Count == 0)
        {
            Console.Error.WriteLine("Both groups must contain shader records.");
            return 1;
        }

        var leftHashes = leftUnique.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rightHashes = rightUnique.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var common = leftHashes.Intersect(rightHashes, StringComparer.OrdinalIgnoreCase).OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase).ToArray();
        var leftOnly = leftHashes.Except(rightHashes, StringComparer.OrdinalIgnoreCase).OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase).ToArray();
        var rightOnly = rightHashes.Except(leftHashes, StringComparer.OrdinalIgnoreCase).OrderBy(hash => hash, StringComparer.OrdinalIgnoreCase).ToArray();

        Console.WriteLine($"Left group captures: {leftDirectories.Count}");
        Console.WriteLine($"Right group captures: {rightDirectories.Count}");
        Console.WriteLine($"Common shader hashes: {common.Length}");
        Console.WriteLine($"Left-only shader hashes: {leftOnly.Length}");
        Console.WriteLine($"Right-only shader hashes: {rightOnly.Length}");
        Console.WriteLine();

        PrintGroupSupportTable("Top left-only shaders by left-group support", leftOnly, leftUnique);
        Console.WriteLine();
        PrintGroupSupportTable("Top right-only shaders by right-group support", rightOnly, rightUnique);
        Console.WriteLine();
        PrintStageCounts("Left-only shaders by stage", leftOnly.Select(hash => leftUnique[hash].Shader));
        Console.WriteLine();
        PrintStageCounts("Right-only shaders by stage", rightOnly.Select(hash => rightUnique[hash].Shader));

        if (!string.IsNullOrWhiteSpace(outputPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(
                outputPath,
                ReportFormatter.BuildGroupCompareMarkdown(leftDirectories, rightDirectories, leftUnique, rightUnique, common, leftOnly, rightOnly),
                Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"Wrote group compare report: {outputPath}");
        }

        return 0;
    }

    private static async Task<List<CaptureSummary>> LoadCapturesAsync(IReadOnlyList<string> directories)
    {
        var captures = new List<CaptureSummary>(directories.Count);
        foreach (var directory in directories)
        {
            captures.Add(await CaptureReader.LoadAsync(directory));
        }

        return captures;
    }

    private static Dictionary<string, GroupShaderStats> AggregateUniqueShaders(IEnumerable<CaptureSummary> captures)
    {
        var result = new Dictionary<string, GroupShaderStats>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures)
        {
            foreach (var shader in capture.UniqueShaders.Values)
            {
                if (!result.TryGetValue(shader.Hash, out var stats))
                {
                    stats = new GroupShaderStats(shader);
                    result.Add(shader.Hash, stats);
                }

                stats.CaptureCount++;
            }
        }

        return result;
    }

    private static void PrintGroupSupportTable(string title, IEnumerable<string> hashes, IReadOnlyDictionary<string, GroupShaderStats> stats)
    {
        Console.WriteLine(title + ":");
        foreach (var entry in hashes
                     .Select(hash => stats[hash])
                     .OrderByDescending(item => item.CaptureCount)
                     .ThenBy(item => item.Shader.Stage, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Shader.Hash, StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            Console.WriteLine($"{entry.CaptureCount,4}  {entry.Shader.Stage,2}  {entry.Shader.Hash}");
        }
    }

    private static void PrintStageCounts(string title, IEnumerable<ShaderRecord> shaders)
    {
        Console.WriteLine(title + ":");
        foreach (var stageGroup in shaders
                     .GroupBy(shader => shader.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{stageGroup.Key,2}  {stageGroup.Count(),4}");
        }
    }

    private static bool TryParse(string[] args, out List<string> leftDirectories, out List<string> rightDirectories, out string outputPath, out string error)
    {
        leftDirectories = [];
        rightDirectories = [];
        outputPath = string.Empty;
        error = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--left" when index + 1 < args.Length:
                    leftDirectories.Add(Path.GetFullPath(args[++index]));
                    break;
                case "--right" when index + 1 < args.Length:
                    rightDirectories.Add(Path.GetFullPath(args[++index]));
                    break;
                case "--output" when index + 1 < args.Length:
                    outputPath = Path.GetFullPath(args[++index]);
                    break;
                default:
                    error = $"Unknown or incomplete argument: {args[index]}";
                    return false;
            }
        }

        if (leftDirectories.Count == 0 || leftDirectories.Any(path => !Directory.Exists(path)))
        {
            error = "At least one valid --left capture directory is required.";
            return false;
        }

        if (rightDirectories.Count == 0 || rightDirectories.Any(path => !Directory.Exists(path)))
        {
            error = "At least one valid --right capture directory is required.";
            return false;
        }

        return true;
    }
}

internal static class CatalogCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (!TryParse(args, out var inputDirectories, out var outputMarkdownPath, out var outputJsonPath, out var error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }

        var captures = await LoadCapturesAsync(inputDirectories);
        var catalog = ShaderCatalog.Build(inputDirectories, captures);
        if (catalog.Entries.Count == 0)
        {
            Console.Error.WriteLine("The catalog inputs did not contain any shader records.");
            return 1;
        }

        Console.WriteLine($"Inputs: {catalog.InputCaptures.Count}");
        Console.WriteLine($"Unique shaders in catalog: {catalog.Entries.Count}");
        Console.WriteLine();
        Console.WriteLine("Catalog stage counts:");
        foreach (var stageCount in catalog.StageCounts.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{stageCount.Key,2}  {stageCount.Value,4}");
        }

        if (!string.IsNullOrWhiteSpace(outputMarkdownPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputMarkdownPath)!);
            await File.WriteAllTextAsync(outputMarkdownPath, ReportFormatter.BuildCatalogMarkdown(catalog), Encoding.UTF8);
            Console.WriteLine();
            Console.WriteLine($"Wrote catalog markdown: {outputMarkdownPath}");
        }

        if (!string.IsNullOrWhiteSpace(outputJsonPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputJsonPath)!);
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
            };
            await File.WriteAllTextAsync(outputJsonPath, JsonSerializer.Serialize(catalog, options), Encoding.UTF8);
            Console.WriteLine($"Wrote catalog json: {outputJsonPath}");
        }

        return 0;
    }

    private static async Task<List<CaptureSummary>> LoadCapturesAsync(IReadOnlyList<string> directories)
    {
        var captures = new List<CaptureSummary>(directories.Count);
        foreach (var directory in directories)
        {
            captures.Add(await CaptureReader.LoadAsync(directory));
        }

        return captures;
    }

    private static bool TryParse(
        string[] args,
        out List<string> inputDirectories,
        out string outputMarkdownPath,
        out string outputJsonPath,
        out string error)
    {
        inputDirectories = [];
        outputMarkdownPath = string.Empty;
        outputJsonPath = string.Empty;
        error = string.Empty;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--input" when index + 1 < args.Length:
                    inputDirectories.Add(Path.GetFullPath(args[++index]));
                    break;
                case "--output-md" when index + 1 < args.Length:
                    outputMarkdownPath = Path.GetFullPath(args[++index]);
                    break;
                case "--output-json" when index + 1 < args.Length:
                    outputJsonPath = Path.GetFullPath(args[++index]);
                    break;
                default:
                    error = $"Unknown or incomplete argument: {args[index]}";
                    return false;
            }
        }

        if (inputDirectories.Count == 0 || inputDirectories.Any(path => !Directory.Exists(path)))
        {
            error = "At least one valid --input capture directory is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputMarkdownPath) && string.IsNullOrWhiteSpace(outputJsonPath))
        {
            error = "At least one of --output-md or --output-json is required.";
            return false;
        }

        return true;
    }
}

internal static class ReportFormatter
{
    public static string BuildSummaryMarkdown(string inputDirectory, CaptureSummary capture)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Capture Summary");
        builder.AppendLine();
        builder.AppendLine($"- Capture: `{inputDirectory}`");
        builder.AppendLine($"- Unique shaders: `{capture.UniqueShaders.Count}`");
        builder.AppendLine($"- Shader events: `{capture.ShaderEvents.Count}`");
        builder.AppendLine($"- Frame boundaries: `{capture.FrameCount}`");
        builder.AppendLine($"- State definitions: `{capture.StateDefinitionCount}`");
        builder.AppendLine();
        builder.AppendLine("## Unique Shaders By Stage");
        builder.AppendLine();
        builder.AppendLine("| Stage | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var stageCount in capture.UniqueShaders.Values
                     .GroupBy(shader => shader.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| `{stageCount.Key}` | {stageCount.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Repeated Shader Hashes");
        builder.AppendLine();
        builder.AppendLine("| Stage | Count | Shader Hash |");
        builder.AppendLine("| --- | ---: | --- |");
        foreach (var entry in capture.ShaderEvents
                     .GroupBy(shader => shader.Hash, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            builder.AppendLine($"| `{entry.First().Stage}` | {entry.Count()} | `{entry.Key}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Reflection Signatures");
        builder.AppendLine();
        builder.AppendLine("| Count | Signature |");
        builder.AppendLine("| ---: | --- |");
        foreach (var signatureGroup in capture.UniqueShaders.Values
                     .GroupBy(shader => shader.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(15))
        {
            builder.AppendLine($"| {signatureGroup.Count()} | `{signatureGroup.Key}` |");
        }

        return builder.ToString();
    }

    public static string BuildCompareMarkdown(
        string leftDirectory,
        string rightDirectory,
        CaptureSummary left,
        CaptureSummary right,
        IReadOnlyCollection<string> common,
        IReadOnlyCollection<string> leftOnly,
        IReadOnlyCollection<string> rightOnly)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Capture Comparison");
        builder.AppendLine();
        builder.AppendLine($"- Left: `{leftDirectory}`");
        builder.AppendLine($"- Right: `{rightDirectory}`");
        builder.AppendLine($"- Common shader hashes: `{common.Count}`");
        builder.AppendLine($"- Left-only shader hashes: `{leftOnly.Count}`");
        builder.AppendLine($"- Right-only shader hashes: `{rightOnly.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Left-only Shaders By Stage");
        builder.AppendLine();
        AppendStageTable(builder, leftOnly.Select(hash => left.UniqueShaders[hash]));
        builder.AppendLine();
        builder.AppendLine("## Right-only Shaders By Stage");
        builder.AppendLine();
        AppendStageTable(builder, rightOnly.Select(hash => right.UniqueShaders[hash]));
        builder.AppendLine();
        builder.AppendLine("## Top Left-only Reflection Signatures");
        builder.AppendLine();
        AppendSignatureTable(builder, leftOnly.Select(hash => left.UniqueShaders[hash]));
        builder.AppendLine();
        builder.AppendLine("## Top Right-only Reflection Signatures");
        builder.AppendLine();
        AppendSignatureTable(builder, rightOnly.Select(hash => right.UniqueShaders[hash]));
        return builder.ToString();
    }

    public static string BuildGroupCompareMarkdown(
        IReadOnlyList<string> leftDirectories,
        IReadOnlyList<string> rightDirectories,
        IReadOnlyDictionary<string, GroupShaderStats> leftUnique,
        IReadOnlyDictionary<string, GroupShaderStats> rightUnique,
        IReadOnlyCollection<string> common,
        IReadOnlyCollection<string> leftOnly,
        IReadOnlyCollection<string> rightOnly)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Capture Group Comparison");
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        builder.AppendLine();
        builder.AppendLine("### Left Group");
        foreach (var left in leftDirectories)
        {
            builder.AppendLine($"- `{left}`");
        }

        builder.AppendLine();
        builder.AppendLine("### Right Group");
        foreach (var right in rightDirectories)
        {
            builder.AppendLine($"- `{right}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Totals");
        builder.AppendLine();
        builder.AppendLine($"- Common shader hashes: `{common.Count}`");
        builder.AppendLine($"- Left-only shader hashes: `{leftOnly.Count}`");
        builder.AppendLine($"- Right-only shader hashes: `{rightOnly.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Left-only Shaders By Support");
        builder.AppendLine();
        builder.AppendLine("| Left Captures | Stage | Shader Hash |");
        builder.AppendLine("| ---: | --- | --- |");
        foreach (var entry in leftOnly
                     .Select(hash => leftUnique[hash])
                     .OrderByDescending(item => item.CaptureCount)
                     .ThenBy(item => item.Shader.Stage, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Shader.Hash, StringComparer.OrdinalIgnoreCase)
                     .Take(25))
        {
            builder.AppendLine($"| {entry.CaptureCount} | `{entry.Shader.Stage}` | `{entry.Shader.Hash}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Right-only Shaders By Support");
        builder.AppendLine();
        builder.AppendLine("| Right Captures | Stage | Shader Hash |");
        builder.AppendLine("| ---: | --- | --- |");
        foreach (var entry in rightOnly
                     .Select(hash => rightUnique[hash])
                     .OrderByDescending(item => item.CaptureCount)
                     .ThenBy(item => item.Shader.Stage, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(item => item.Shader.Hash, StringComparer.OrdinalIgnoreCase)
                     .Take(25))
        {
            builder.AppendLine($"| {entry.CaptureCount} | `{entry.Shader.Stage}` | `{entry.Shader.Hash}` |");
        }

        builder.AppendLine();
        builder.AppendLine("## Left-only Shaders By Stage");
        builder.AppendLine();
        AppendStageTable(builder, leftOnly.Select(hash => leftUnique[hash].Shader));
        builder.AppendLine();
        builder.AppendLine("## Right-only Shaders By Stage");
        builder.AppendLine();
        AppendStageTable(builder, rightOnly.Select(hash => rightUnique[hash].Shader));
        builder.AppendLine();
        builder.AppendLine("## Top Left-only Reflection Signatures");
        builder.AppendLine();
        AppendSignatureTable(builder, leftOnly.Select(hash => leftUnique[hash].Shader));
        builder.AppendLine();
        builder.AppendLine("## Top Right-only Reflection Signatures");
        builder.AppendLine();
        AppendSignatureTable(builder, rightOnly.Select(hash => rightUnique[hash].Shader));
        return builder.ToString();
    }

    public static string BuildCatalogMarkdown(ShaderCatalog catalog)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Shader Catalog");
        builder.AppendLine();
        builder.AppendLine($"- Generated at: `{catalog.GeneratedAtUtc}`");
        builder.AppendLine($"- Input captures: `{catalog.InputCaptures.Count}`");
        builder.AppendLine($"- Unique shaders: `{catalog.Entries.Count}`");
        builder.AppendLine();
        builder.AppendLine("## Inputs");
        builder.AppendLine();
        foreach (var capture in catalog.InputCaptures)
        {
            builder.AppendLine($"- `{capture}`");
        }

        builder.AppendLine();
        builder.AppendLine("## Stage Counts");
        builder.AppendLine();
        builder.AppendLine("| Stage | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var stageCount in catalog.StageCounts.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| `{stageCount.Key}` | {stageCount.Value} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Capture Support Buckets");
        builder.AppendLine();
        builder.AppendLine("| Capture Support | Hash Count |");
        builder.AppendLine("| ---: | ---: |");
        foreach (var supportBucket in catalog.Entries
                     .GroupBy(entry => entry.CaptureSupport)
                     .OrderByDescending(group => group.Key))
        {
            builder.AppendLine($"| {supportBucket.Key} | {supportBucket.Count()} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Top Reflection Signatures");
        builder.AppendLine();
        builder.AppendLine("| Count | Signature |");
        builder.AppendLine("| ---: | --- |");
        foreach (var signatureGroup in catalog.Entries
                     .GroupBy(entry => entry.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(20))
        {
            builder.AppendLine($"| {signatureGroup.Count()} | `{signatureGroup.Key}` |");
        }

        foreach (var stageGroup in catalog.Entries
                     .GroupBy(entry => entry.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine();
            builder.AppendLine($"## `{stageGroup.Key}` Shaders");
            builder.AppendLine();
            builder.AppendLine("| Hash | Support | Bytecode | Reflection | Input Semantics | Output Semantics | First Seen | Last Seen |");
            builder.AppendLine("| --- | ---: | ---: | --- | --- | --- | --- | --- |");
            foreach (var entry in stageGroup
                         .OrderByDescending(item => item.CaptureSupport)
                         .ThenBy(item => item.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(item => item.Hash, StringComparer.OrdinalIgnoreCase))
            {
                builder.AppendLine(
                    $"| `{entry.Hash}` | {entry.CaptureSupport} | {entry.BytecodeSize} | `{entry.ReflectionSignature}` | `{entry.InputSemanticsSignature}` | `{entry.OutputSemanticsSignature}` | `{entry.FirstSeenCapture}` | `{entry.LastSeenCapture}` |");
            }
        }

        return builder.ToString();
    }

    private static void AppendStageTable(StringBuilder builder, IEnumerable<ShaderRecord> shaders)
    {
        builder.AppendLine("| Stage | Count |");
        builder.AppendLine("| --- | ---: |");
        foreach (var stageGroup in shaders
                     .GroupBy(shader => shader.Stage)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.AppendLine($"| `{stageGroup.Key}` | {stageGroup.Count()} |");
        }
    }

    private static void AppendSignatureTable(StringBuilder builder, IEnumerable<ShaderRecord> shaders)
    {
        builder.AppendLine("| Count | Signature |");
        builder.AppendLine("| ---: | --- |");
        foreach (var signatureGroup in shaders
                     .GroupBy(shader => shader.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(group => group.Count())
                     .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                     .Take(15))
        {
            builder.AppendLine($"| {signatureGroup.Count()} | `{signatureGroup.Key}` |");
        }
    }
}

internal sealed record ShaderRecord(
    string Hash,
    string Stage,
    int BytecodeSize,
    int BoundResourceCount,
    int ConstantBufferCount,
    int InputParameterCount,
    int OutputParameterCount,
    string TimestampUtc,
    string[] InputSemantics,
    string[] OutputSemantics)
{
    public string ReflectionSignature =>
        $"{Stage} br={BoundResourceCount} cb={ConstantBufferCount} in={InputParameterCount} out={OutputParameterCount}";

    public string InputSemanticsSignature =>
        InputSemantics.Length == 0 ? "-" : string.Join(", ", InputSemantics);

    public string OutputSemanticsSignature =>
        OutputSemantics.Length == 0 ? "-" : string.Join(", ", OutputSemantics);
}

internal sealed class GroupShaderStats(ShaderRecord shader)
{
    public ShaderRecord Shader { get; } = shader;
    public int CaptureCount { get; set; }
}

internal sealed record CaptureSummary(
    string CaptureName,
    string CapturePath,
    IReadOnlyList<ShaderRecord> ShaderEvents,
    IReadOnlyDictionary<string, ShaderRecord> UniqueShaders,
    int FrameCount,
    int StateDefinitionCount);

internal sealed record ShaderCatalogEntry(
    string Hash,
    string Stage,
    int BytecodeSize,
    string ReflectionSignature,
    int BoundResourceCount,
    int ConstantBufferCount,
    int InputParameterCount,
    int OutputParameterCount,
    string InputSemanticsSignature,
    string OutputSemanticsSignature,
    int CaptureSupport,
    string FirstSeenCapture,
    string LastSeenCapture,
    string FirstSeenTimestampUtc,
    string LastSeenTimestampUtc,
    IReadOnlyList<string> SeenInCaptures);

internal sealed record ShaderCatalog(
    string GeneratedAtUtc,
    IReadOnlyList<string> InputCaptures,
    IReadOnlyDictionary<string, int> StageCounts,
    IReadOnlyList<ShaderCatalogEntry> Entries)
{
    public static ShaderCatalog Build(IReadOnlyList<string> inputDirectories, IReadOnlyList<CaptureSummary> captures)
    {
        var entries = new Dictionary<string, CatalogShaderAggregate>(StringComparer.OrdinalIgnoreCase);
        foreach (var capture in captures.OrderBy(item => item.CaptureName, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var shader in capture.UniqueShaders.Values)
            {
                if (!entries.TryGetValue(shader.Hash, out var aggregate))
                {
                    aggregate = new CatalogShaderAggregate(shader);
                    entries.Add(shader.Hash, aggregate);
                }

                aggregate.SeenInCaptures.Add(capture.CaptureName);
                aggregate.UpdateBounds(capture.CaptureName, shader.TimestampUtc);
            }
        }

        var catalogEntries = entries.Values
            .Select(aggregate => aggregate.ToEntry())
            .OrderBy(entry => entry.Stage, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(entry => entry.CaptureSupport)
            .ThenBy(entry => entry.ReflectionSignature, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Hash, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var stageCounts = catalogEntries
            .GroupBy(entry => entry.Stage)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return new ShaderCatalog(
            DateTimeOffset.UtcNow.ToString("O"),
            inputDirectories.Select(Path.GetFullPath).ToArray(),
            stageCounts,
            catalogEntries);
    }
}

internal sealed class CatalogShaderAggregate(ShaderRecord shader)
{
    public ShaderRecord Shader { get; } = shader;
    public SortedSet<string> SeenInCaptures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string FirstSeenCapture { get; private set; } = string.Empty;
    public string LastSeenCapture { get; private set; } = string.Empty;
    public string FirstSeenTimestampUtc { get; private set; } = string.Empty;
    public string LastSeenTimestampUtc { get; private set; } = string.Empty;

    public void UpdateBounds(string captureName, string timestampUtc)
    {
        if (string.IsNullOrWhiteSpace(FirstSeenCapture) ||
            string.CompareOrdinal(captureName, FirstSeenCapture) < 0)
        {
            FirstSeenCapture = captureName;
        }

        if (string.IsNullOrWhiteSpace(LastSeenCapture) ||
            string.CompareOrdinal(captureName, LastSeenCapture) > 0)
        {
            LastSeenCapture = captureName;
        }

        if (string.IsNullOrWhiteSpace(FirstSeenTimestampUtc) ||
            string.CompareOrdinal(timestampUtc, FirstSeenTimestampUtc) < 0)
        {
            FirstSeenTimestampUtc = timestampUtc;
        }

        if (string.IsNullOrWhiteSpace(LastSeenTimestampUtc) ||
            string.CompareOrdinal(timestampUtc, LastSeenTimestampUtc) > 0)
        {
            LastSeenTimestampUtc = timestampUtc;
        }
    }

    public ShaderCatalogEntry ToEntry()
        => new(
            Shader.Hash,
            Shader.Stage,
            Shader.BytecodeSize,
            Shader.ReflectionSignature,
            Shader.BoundResourceCount,
            Shader.ConstantBufferCount,
            Shader.InputParameterCount,
            Shader.OutputParameterCount,
            Shader.InputSemanticsSignature,
            Shader.OutputSemanticsSignature,
            SeenInCaptures.Count,
            FirstSeenCapture,
            LastSeenCapture,
            FirstSeenTimestampUtc,
            LastSeenTimestampUtc,
            SeenInCaptures.ToArray());
}

internal static class CaptureReader
{
    public static async Task<CaptureSummary> LoadAsync(string inputDirectory)
    {
        var shaderEvents = await ReadShadersAsync(inputDirectory);
        var uniqueShaders = new Dictionary<string, ShaderRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var shader in shaderEvents)
        {
            uniqueShaders.TryAdd(shader.Hash, shader);
        }

        var framePath = Path.Combine(inputDirectory, "frames.jsonl");
        var statePath = Path.Combine(inputDirectory, "states.jsonl");

        return new CaptureSummary(
            Path.GetFileName(Path.TrimEndingDirectorySeparator(inputDirectory)),
            inputDirectory,
            shaderEvents,
            uniqueShaders,
            await CountLinesAsync(framePath),
            await CountLinesAsync(statePath));
    }

    private static async Task<List<ShaderRecord>> ReadShadersAsync(string inputDirectory)
    {
        var shaderPath = Path.Combine(inputDirectory, "shaders.jsonl");
        var shaders = new List<ShaderRecord>();
        if (!File.Exists(shaderPath))
        {
            return shaders;
        }

        await foreach (var json in ReadLinesAsync(shaderPath))
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var reflection = root.TryGetProperty("reflection", out var reflectionElement)
                ? reflectionElement
                : default;

            shaders.Add(new ShaderRecord(
                root.GetProperty("shader_hash").GetString() ?? "<missing>",
                root.GetProperty("stage").GetString() ?? "<missing>",
                root.TryGetProperty("bytecode_size", out var bytecodeSize) ? bytecodeSize.GetInt32() : 0,
                TryGetArrayLength(reflection, "bound_resources"),
                TryGetArrayLength(reflection, "constant_buffers"),
                TryGetArrayLength(reflection, "input_parameters"),
                TryGetArrayLength(reflection, "output_parameters"),
                root.TryGetProperty("timestamp_utc", out var timestampUtc) ? timestampUtc.GetString() ?? string.Empty : string.Empty,
                ReadSemanticNames(reflection, "input_parameters"),
                ReadSemanticNames(reflection, "output_parameters")));
        }

        return shaders;
    }

    private static string[] ReadSemanticNames(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Undefined ||
            !root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var semantics = new List<string>(property.GetArrayLength());
        foreach (var item in property.EnumerateArray())
        {
            var semanticName = item.TryGetProperty("semantic_name", out var nameElement)
                ? nameElement.GetString() ?? "<?>"
                : "<?>";
            var semanticIndex = item.TryGetProperty("semantic_index", out var indexElement)
                ? indexElement.GetInt32()
                : 0;
            semantics.Add($"{semanticName}{semanticIndex}");
        }

        return semantics.ToArray();
    }

    private static int TryGetArrayLength(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Undefined || !root.TryGetProperty(propertyName, out var property))
        {
            return 0;
        }

        return property.ValueKind == JsonValueKind.Array ? property.GetArrayLength() : 0;
    }

    private static async Task<int> CountLinesAsync(string path)
    {
        if (!File.Exists(path))
        {
            return 0;
        }

        var count = 0;
        await foreach (var _ in ReadLinesAsync(path))
        {
            count++;
        }

        return count;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync() is { } line)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }
}

internal sealed class JsonlRouter
{
    private readonly ConcurrentDictionary<string, byte> seenShaders = new(StringComparer.OrdinalIgnoreCase);
    private readonly StreamWriter eventsWriter;
    private readonly StreamWriter framesWriter;
    private readonly StreamWriter shadersWriter;
    private readonly StreamWriter capturesWriter;
    private readonly StreamWriter drawsWriter;
    private readonly StreamWriter statesWriter;

    public JsonlRouter(string outputDirectory)
    {
        eventsWriter = OpenWriter(Path.Combine(outputDirectory, "events.jsonl"));
        framesWriter = OpenWriter(Path.Combine(outputDirectory, "frames.jsonl"));
        shadersWriter = OpenWriter(Path.Combine(outputDirectory, "shaders.jsonl"));
        capturesWriter = OpenWriter(Path.Combine(outputDirectory, "captures.jsonl"));
        drawsWriter = OpenWriter(Path.Combine(outputDirectory, "draws.jsonl"));
        statesWriter = OpenWriter(Path.Combine(outputDirectory, "states.jsonl"));
    }

    public void Route(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        var eventType = root.GetProperty("event_type").GetString() ?? "unknown";

        eventsWriter.WriteLine(line);
        eventsWriter.Flush();

        switch (eventType)
        {
            case "frame_boundary":
                framesWriter.WriteLine(line);
                framesWriter.Flush();
                break;
            case "shader_created":
                var hash = root.GetProperty("shader_hash").GetString() ?? "<missing>";
                if (seenShaders.TryAdd(hash, 0))
                {
                    shadersWriter.WriteLine(line);
                }
                break;
            case "bookmark":
                capturesWriter.WriteLine(line);
                break;
            case "state_definition":
                statesWriter.WriteLine(line);
                break;
            case "draw_call":
                drawsWriter.WriteLine(line);
                break;
        }

        if (eventType == "frame_boundary")
        {
            FlushAsync().GetAwaiter().GetResult();
        }
    }

    public Task FlushAsync()
    {
        eventsWriter.Flush();
        framesWriter.Flush();
        shadersWriter.Flush();
        capturesWriter.Flush();
        drawsWriter.Flush();
        statesWriter.Flush();
        return Task.CompletedTask;
    }

    private static StreamWriter OpenWriter(string path)
        => new(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete), new UTF8Encoding(false))
        {
            AutoFlush = true,
        };
}
