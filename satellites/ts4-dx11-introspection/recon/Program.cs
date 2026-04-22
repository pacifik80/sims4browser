using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

if (!Arguments.TryParse(args, out var options, out var error))
{
    Console.Error.WriteLine(error);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Arguments.Usage);
    return 1;
}

var scanner = new BinaryScanner();
var report = scanner.Scan(options);
Directory.CreateDirectory(options.OutputDirectory);

var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
var jsonPath = Path.Combine(options.OutputDirectory, $"recon-{stamp}.json");
var markdownPath = Path.Combine(options.OutputDirectory, $"recon-{stamp}.md");

var jsonOptions = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};

File.WriteAllText(jsonPath, JsonSerializer.Serialize(report, jsonOptions));
File.WriteAllText(markdownPath, MarkdownReportWriter.Write(report));

Console.WriteLine($"Wrote JSON report to {jsonPath}");
Console.WriteLine($"Wrote Markdown report to {markdownPath}");

return 0;

internal sealed record ReconOptions(string TargetDirectory, string OutputDirectory);

internal static class Arguments
{
    public const string Usage =
        "Usage: Ts4Dx11Recon --target <GameBinPath> [--output <OutputDirectory>]\n" +
        "Example: Ts4Dx11Recon --target \"C:\\Games\\The Sims 4\\Game\\Bin\" --output ..\\docs\\reports";

    public static bool TryParse(string[] args, out ReconOptions options, out string error)
    {
        options = new ReconOptions(string.Empty, string.Empty);
        error = string.Empty;

        string? target = null;
        string? output = null;

        for (var index = 0; index < args.Length; index++)
        {
            var current = args[index];
            switch (current)
            {
                case "--target" when index + 1 < args.Length:
                    target = args[++index];
                    break;
                case "--output" when index + 1 < args.Length:
                    output = args[++index];
                    break;
                default:
                    error = $"Unknown or incomplete argument: {current}";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            error = "Missing required --target argument.";
            return false;
        }

        if (!Directory.Exists(target))
        {
            error = $"Target directory does not exist: {target}";
            return false;
        }

        output ??= Path.Combine(AppContext.BaseDirectory, "reports");
        options = new ReconOptions(Path.GetFullPath(target), Path.GetFullPath(output));
        return true;
    }
}

internal sealed class BinaryScanner
{
    private static readonly string[] InterestingStrings =
    [
        "D3D11",
        "DXGI",
        "D3DCompiler",
        "Present",
        "CreateVertexShader",
        "CreatePixelShader",
        "CreateDXGIFactory",
        "IDXGISwapChain",
        "ID3D11Device",
    ];

    public ReconReport Scan(ReconOptions options)
    {
        var binaries = Directory.EnumerateFiles(options.TargetDirectory)
            .Where(path =>
            {
                var extension = Path.GetExtension(path);
                return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".dll", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ScanBinary)
            .ToList();

        var summary = new ReconSummary(
            binaries.Count,
            binaries.Count(binary => binary.Imports.Any(import => import.Name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase))),
            binaries.Count(binary => binary.Imports.Any(import => import.Name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase))),
            binaries.Where(binary => binary.DxbcMagicOffsets.Count > 0).Select(binary => binary.Name).ToList(),
            binaries
                .Where(binary => binary.Imports.Any(import => import.Functions.Any(fn => fn.StartsWith("CreateDXGI", StringComparison.OrdinalIgnoreCase) || fn.StartsWith("D3D11Create", StringComparison.OrdinalIgnoreCase))))
                .Select(binary => binary.Name)
                .ToList());

        return new ReconReport(
            DateTimeOffset.UtcNow,
            options.TargetDirectory,
            summary,
            binaries);
    }

    private BinaryReport ScanBinary(string path)
    {
        using var stream = File.OpenRead(path);
        using var peReader = new PEReader(stream);
        var headers = peReader.PEHeaders;
        var bytes = ReadAllBytes(path);

        var imports = ReadImports(bytes, headers);
        var stringHits = ExtractInterestingStrings(bytes);
        var dxbcMagicOffsets = FindMagicOffsets(bytes, "DXBC"u8.ToArray());

        return new BinaryReport(
            Name: Path.GetFileName(path),
            Path: path,
            Size: bytes.Length,
            Machine: headers.CoffHeader.Machine.ToString(),
            TimeDateStampUtc: DateTimeOffset.FromUnixTimeSeconds(headers.CoffHeader.TimeDateStamp),
            EntryPointRva: headers.PEHeader?.AddressOfEntryPoint,
            Imports: imports,
            DxbcMagicOffsets: dxbcMagicOffsets,
            InterestingStringHits: stringHits);
    }

    private static byte[] ReadAllBytes(string path)
    {
        using var stream = File.OpenRead(path);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static IReadOnlyList<ImportLibraryReport> ReadImports(byte[] bytes, PEHeaders headers)
    {
        var directory = headers.PEHeader?.ImportTableDirectory ?? default;
        if (directory.RelativeVirtualAddress == 0 || directory.Size == 0)
        {
            return [];
        }

        var descriptors = new List<ImportLibraryReport>();
        var descriptorOffset = RvaToOffset(headers, directory.RelativeVirtualAddress);
        if (descriptorOffset < 0)
        {
            return [];
        }

        var is64Bit = headers.PEHeader?.Magic == PEMagic.PE32Plus;
        while (descriptorOffset + 20 <= bytes.Length)
        {
            var originalFirstThunk = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset, 4));
            var timeDateStamp = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset + 4, 4));
            var forwarderChain = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset + 8, 4));
            var nameRva = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset + 12, 4));
            var firstThunk = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(descriptorOffset + 16, 4));

            if (originalFirstThunk == 0 && timeDateStamp == 0 && forwarderChain == 0 && nameRva == 0 && firstThunk == 0)
            {
                break;
            }

            var nameOffset = RvaToOffset(headers, (int)nameRva);
            var thunkRva = originalFirstThunk != 0 ? (int)originalFirstThunk : (int)firstThunk;
            var thunkOffset = RvaToOffset(headers, thunkRva);
            if (nameOffset < 0 || thunkOffset < 0)
            {
                descriptorOffset += 20;
                continue;
            }

            var functions = ReadThunkFunctions(bytes, headers, thunkOffset, is64Bit);
            descriptors.Add(new ImportLibraryReport(ReadAsciiZ(bytes, nameOffset), functions));
            descriptorOffset += 20;
        }

        return descriptors;
    }

    private static IReadOnlyList<string> ReadThunkFunctions(byte[] bytes, PEHeaders headers, int thunkOffset, bool is64Bit)
    {
        var functions = new List<string>();
        var step = is64Bit ? 8 : 4;
        var ordinalMask = is64Bit ? 0x8000000000000000UL : 0x80000000UL;

        while (thunkOffset + step <= bytes.Length)
        {
            var entry = is64Bit
                ? BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(thunkOffset, step))
                : BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(thunkOffset, step));

            if (entry == 0)
            {
                break;
            }

            if ((entry & ordinalMask) != 0)
            {
                functions.Add($"ordinal:{entry & 0xffff}");
                thunkOffset += step;
                continue;
            }

            var hintNameOffset = RvaToOffset(headers, (int)entry);
            if (hintNameOffset < 0 || hintNameOffset + 2 >= bytes.Length)
            {
                break;
            }

            functions.Add(ReadAsciiZ(bytes, hintNameOffset + 2));
            thunkOffset += step;
        }

        return functions;
    }

    private static IReadOnlyList<StringHitReport> ExtractInterestingStrings(byte[] bytes)
    {
        var hits = new List<StringHitReport>();

        foreach (var token in InterestingStrings)
        {
            var asciiOffsets = FindMagicOffsets(bytes, Encoding.ASCII.GetBytes(token));
            var utf16Offsets = FindMagicOffsets(bytes, Encoding.Unicode.GetBytes(token));
            if (asciiOffsets.Count == 0 && utf16Offsets.Count == 0)
            {
                continue;
            }

            hits.Add(new StringHitReport(token, asciiOffsets, utf16Offsets));
        }

        return hits;
    }

    private static IReadOnlyList<int> FindMagicOffsets(byte[] bytes, byte[] magic)
    {
        var offsets = new List<int>();
        for (var index = 0; index <= bytes.Length - magic.Length; index++)
        {
            if (bytes[index] != magic[0])
            {
                continue;
            }

            if (bytes.AsSpan(index, magic.Length).SequenceEqual(magic))
            {
                offsets.Add(index);
            }
        }

        return offsets;
    }

    private static string ReadAsciiZ(byte[] bytes, int offset)
    {
        var end = offset;
        while (end < bytes.Length && bytes[end] != 0)
        {
            end++;
        }

        return Encoding.ASCII.GetString(bytes, offset, end - offset);
    }

    private static int RvaToOffset(PEHeaders headers, int rva)
    {
        foreach (var section in headers.SectionHeaders)
        {
            var sectionStart = section.VirtualAddress;
            var sectionEnd = sectionStart + Math.Max(section.VirtualSize, section.SizeOfRawData);
            if (rva < sectionStart || rva >= sectionEnd)
            {
                continue;
            }

            return rva - section.VirtualAddress + section.PointerToRawData;
        }

        return -1;
    }
}

internal static class MarkdownReportWriter
{
    public static string Write(ReconReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TS4 DX11 Static Recon Report");
        builder.AppendLine();
        builder.AppendLine($"- Generated (UTC): {report.GeneratedUtc:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine($"- Target directory: `{report.TargetDirectory}`");
        builder.AppendLine($"- PE files scanned: {report.Summary.BinaryCount}");
        builder.AppendLine($"- Binaries importing `d3d11.dll`: {report.Summary.D3D11ImportCount}");
        builder.AppendLine($"- Binaries importing `dxgi.dll`: {report.Summary.DxgiImportCount}");
        builder.AppendLine();

        if (report.Summary.Dx11EntryCandidates.Count > 0)
        {
            builder.AppendLine("## DX11 Entry Candidates");
            builder.AppendLine();
            foreach (var candidate in report.Summary.Dx11EntryCandidates)
            {
                builder.AppendLine($"- `{candidate}`");
            }

            builder.AppendLine();
        }

        if (report.Summary.DxbcCarrierCandidates.Count > 0)
        {
            builder.AppendLine("## Embedded DXBC Candidates");
            builder.AppendLine();
            foreach (var candidate in report.Summary.DxbcCarrierCandidates)
            {
                builder.AppendLine($"- `{candidate}`");
            }

            builder.AppendLine();
        }

        builder.AppendLine("## Per-Binary Details");
        builder.AppendLine();
        foreach (var binary in report.Binaries)
        {
            builder.AppendLine($"### `{binary.Name}`");
            builder.AppendLine();
            builder.AppendLine($"- Size: {binary.Size:N0} bytes");
            builder.AppendLine($"- Machine: {binary.Machine}");
            builder.AppendLine($"- Timestamp (UTC): {binary.TimeDateStampUtc:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine($"- Entry point RVA: `{FormatNullableHex(binary.EntryPointRva)}`");

            var graphicsImports = binary.Imports
                .Where(import => import.Name.Equals("d3d11.dll", StringComparison.OrdinalIgnoreCase) ||
                    import.Name.Equals("dxgi.dll", StringComparison.OrdinalIgnoreCase) ||
                    import.Name.Contains("d3dcompiler", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (graphicsImports.Count == 0)
            {
                builder.AppendLine("- Graphics imports: none detected");
            }
            else
            {
                builder.AppendLine("- Graphics imports:");
                foreach (var import in graphicsImports)
                {
                    builder.AppendLine($"  - `{import.Name}` -> {string.Join(", ", import.Functions)}");
                }
            }

            builder.AppendLine($"- Embedded `DXBC` magic offsets: {FormatOffsets(binary.DxbcMagicOffsets)}");
            if (binary.InterestingStringHits.Count > 0)
            {
                builder.AppendLine("- Interesting strings:");
                foreach (var hit in binary.InterestingStringHits)
                {
                    builder.AppendLine($"  - `{hit.Token}` -> ASCII {FormatOffsets(hit.AsciiOffsets)}, UTF-16 {FormatOffsets(hit.Utf16Offsets)}");
                }
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string FormatOffsets(IReadOnlyList<int> offsets)
        => offsets.Count == 0 ? "none" : string.Join(", ", offsets.Take(8).Select(value => $"0x{value:X}"));

    private static string FormatNullableHex(int? value)
        => value is null ? "n/a" : $"0x{value.Value:X}";
}

internal sealed record ReconReport(
    DateTimeOffset GeneratedUtc,
    string TargetDirectory,
    ReconSummary Summary,
    IReadOnlyList<BinaryReport> Binaries);

internal sealed record ReconSummary(
    int BinaryCount,
    int D3D11ImportCount,
    int DxgiImportCount,
    IReadOnlyList<string> DxbcCarrierCandidates,
    IReadOnlyList<string> Dx11EntryCandidates);

internal sealed record BinaryReport(
    string Name,
    string Path,
    long Size,
    string Machine,
    DateTimeOffset TimeDateStampUtc,
    int? EntryPointRva,
    IReadOnlyList<ImportLibraryReport> Imports,
    IReadOnlyList<int> DxbcMagicOffsets,
    IReadOnlyList<StringHitReport> InterestingStringHits);

internal sealed record ImportLibraryReport(string Name, IReadOnlyList<string> Functions);

internal sealed record StringHitReport(string Token, IReadOnlyList<int> AsciiOffsets, IReadOnlyList<int> Utf16Offsets);
