using System.Text.Json;

namespace Sims4ResourceExplorer.Preview;

internal sealed class Ts4ShaderProfileRegistry
{
    private static readonly Lazy<Ts4ShaderProfileRegistry> lazyInstance = new(Create);
    private readonly Dictionary<uint, ShaderBlockProfile> profiles;

    private Ts4ShaderProfileRegistry(Dictionary<uint, ShaderBlockProfile> profiles)
    {
        this.profiles = profiles;
    }

    public static Ts4ShaderProfileRegistry Instance => lazyInstance.Value;

    public bool TryGetProfile(uint shaderHash, out ShaderBlockProfile profile) =>
        profiles.TryGetValue(shaderHash, out profile!);

    private static Ts4ShaderProfileRegistry Create()
    {
        var path = TryFindProfilePath();
        if (path is null || !File.Exists(path))
        {
            TryWriteRegistryTrace($"Profile registry file was not found. BaseDirectory={AppContext.BaseDirectory}");
            return new Ts4ShaderProfileRegistry([]);
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonDocument.Parse(json);
            var profiles = new Dictionary<uint, ShaderBlockProfile>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!TryParseHex(property.Name, out var shaderHash))
                {
                    continue;
                }

                var entry = property.Value;
                var nameGuess = entry.TryGetProperty("name_guess", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString() ?? $"ShaderBlock_{shaderHash:X8}"
                    : $"ShaderBlock_{shaderHash:X8}";
                var parameters = new Dictionary<uint, ShaderParameterProfile>();
                if (entry.TryGetProperty("parm_sets", out var parmSetsElement) && parmSetsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var parmSetGroup in parmSetsElement.EnumerateArray())
                    {
                        foreach (var parmSet in parmSetGroup.EnumerateArray())
                        {
                            if (!parmSet.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Array)
                            {
                                continue;
                            }

                            foreach (var paramElement in paramsElement.EnumerateArray())
                            {
                                if (!TryReadParameter(paramElement, out var parameter))
                                {
                                    continue;
                                }

                                parameters.TryAdd(parameter.Hash, parameter);
                            }
                        }
                    }
                }

                profiles[shaderHash] = new ShaderBlockProfile(shaderHash, nameGuess, parameters.Values.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray());
            }

            TryWriteRegistryTrace($"Loaded {profiles.Count} shader profile(s) from {path}");
            return new Ts4ShaderProfileRegistry(profiles);
        }
        catch (Exception ex)
        {
            TryWriteRegistryTrace($"Failed to load shader profiles from {path}:{Environment.NewLine}{ex}");
            return new Ts4ShaderProfileRegistry([]);
        }
    }

    private static bool TryReadParameter(JsonElement element, out ShaderParameterProfile parameter)
    {
        parameter = default!;
        if (!element.TryGetProperty("hash_hex", out var hashElement) ||
            !element.TryGetProperty("name", out var nameElement) ||
            !element.TryGetProperty("packed_type_hex", out var packedTypeElement) ||
            !element.TryGetProperty("flags_hex", out var flagsElement))
        {
            return false;
        }

        if (!TryParseHex(hashElement.GetString(), out var hash) ||
            !TryParseHex(packedTypeElement.GetString(), out var packedType) ||
            !TryParseHex(flagsElement.GetString(), out var flags))
        {
            return false;
        }

        var name = nameElement.GetString() ?? $"0x{hash:X8}";
        var category = element.TryGetProperty("category", out var categoryElement)
            ? ParseCategory(categoryElement.GetString())
            : Ts4ShaderSemantics.ClassifyPackedParameter(name, packedType, flags);

        parameter = new ShaderParameterProfile(hash, name, packedType, flags, category);
        return true;
    }

    private static ShaderParameterCategory ParseCategory(string? category) => category?.ToLowerInvariant() switch
    {
        "sampler" => ShaderParameterCategory.Sampler,
        "scalar" => ShaderParameterCategory.Scalar,
        "vec2" => ShaderParameterCategory.Vector2,
        "vec3" => ShaderParameterCategory.Vector3,
        "vec4" => ShaderParameterCategory.Vector4,
        "resourcekey" => ShaderParameterCategory.ResourceKey,
        "uv_mapping" => ShaderParameterCategory.UvMapping,
        "bool_like" => ShaderParameterCategory.BoolLike,
        _ => ShaderParameterCategory.Unknown
    };

    private static bool TryParseHex(string? value, out uint result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;
        return uint.TryParse(normalized, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static string? TryFindProfilePath()
    {
        foreach (var root in EnumerateSearchRoots())
        {
            var current = new DirectoryInfo(root);
            for (var depth = 0; depth < 10 && current is not null; depth++, current = current.Parent)
            {
                var candidate = Path.Combine(current.FullName, "tmp", "precomp_shader_profiles.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void Add(HashSet<string> seenRoots, List<string> roots, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(path);
                if (seenRoots.Add(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }

        var roots = new List<string>();
        Add(seen, roots, AppContext.BaseDirectory);
        Add(seen, roots, Environment.CurrentDirectory);
        Add(seen, roots, Path.GetDirectoryName(typeof(Ts4ShaderProfileRegistry).Assembly.Location));
        return roots;
    }

    private static void TryWriteRegistryTrace(string message)
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;
            Directory.CreateDirectory(baseDirectory);
            var tracePath = Path.Combine(baseDirectory, "shader_profile_registry_trace.txt");
            File.WriteAllText(tracePath, message);
        }
        catch
        {
        }
    }
}
