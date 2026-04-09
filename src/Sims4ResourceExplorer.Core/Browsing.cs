namespace Sims4ResourceExplorer.Core;

public enum BrowserMode
{
    AssetBrowser,
    RawResourceBrowser
}

public enum AssetBrowserDomain
{
    BuildBuy,
    Cas
}

public enum RawResourceDomain
{
    All,
    Images,
    Audio,
    TextXml,
    ThreeDRelated,
    OtherUnknown
}

public enum AssetBrowserSort
{
    Name,
    Category,
    Package
}

public enum RawResourceSort
{
    TypeName,
    PackagePath,
    Tgi
}

public enum ResourceLinkFilter
{
    Any,
    LinkedOnly,
    UnlinkedOnly
}

public sealed record SourceScope(bool IncludeGame = true, bool IncludeDlc = true, bool IncludeMods = true)
{
    public bool HasAny => IncludeGame || IncludeDlc || IncludeMods;

    public IReadOnlyList<SourceKind> ToSourceKinds()
    {
        var results = new List<SourceKind>(3);
        if (IncludeGame)
        {
            results.Add(SourceKind.Game);
        }

        if (IncludeDlc)
        {
            results.Add(SourceKind.Dlc);
        }

        if (IncludeMods)
        {
            results.Add(SourceKind.Mods);
        }

        return results;
    }

    public string ToDisplayText()
    {
        var labels = new List<string>(3);
        if (IncludeGame)
        {
            labels.Add("Game");
        }

        if (IncludeDlc)
        {
            labels.Add("DLC");
        }

        if (IncludeMods)
        {
            labels.Add("Mods");
        }

        return labels.Count == 0 ? "No sources" : string.Join(" + ", labels);
    }
}

public sealed record AssetBrowserQuery(
    SourceScope SourceScope,
    string SearchText,
    AssetBrowserDomain Domain,
    string CategoryText,
    string PackageText,
    bool HasThumbnailOnly,
    bool VariantsOnly,
    AssetBrowserSort Sort,
    int Offset,
    int WindowSize);

public sealed record RawResourceBrowserQuery(
    SourceScope SourceScope,
    string SearchText,
    RawResourceDomain Domain,
    string TypeNameText,
    string PackageText,
    string GroupHexText,
    string InstanceHexText,
    bool PreviewableOnly,
    bool ExportCapableOnly,
    bool CompressedKnownOnly,
    ResourceLinkFilter LinkFilter,
    RawResourceSort Sort,
    int Offset,
    int WindowSize);

public sealed record WindowedQueryResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Offset,
    int WindowSize)
{
    public int LoadedCount => Offset + Items.Count;
    public bool HasMore => LoadedCount < TotalCount;
}

public sealed record FilterChip(string Key, string Label);

public sealed class AssetBrowserState
{
    public string SearchText { get; set; } = string.Empty;
    public AssetBrowserDomain Domain { get; set; } = AssetBrowserDomain.BuildBuy;
    public string CategoryText { get; set; } = string.Empty;
    public string PackageText { get; set; } = string.Empty;
    public bool HasThumbnailOnly { get; set; }
    public bool VariantsOnly { get; set; }
    public AssetBrowserSort Sort { get; set; } = AssetBrowserSort.Name;

    public AssetBrowserQuery ToQuery(SourceScope sourceScope, int offset, int windowSize) =>
        new(
            sourceScope,
            SearchText.Trim(),
            Domain,
            CategoryText.Trim(),
            PackageText.Trim(),
            HasThumbnailOnly,
            VariantsOnly,
            Sort,
            offset,
            windowSize);

    public IReadOnlyList<FilterChip> BuildFilterChips(SourceScope sourceScope)
    {
        var chips = new List<FilterChip>();
        if (!sourceScope.IncludeGame || !sourceScope.IncludeDlc || !sourceScope.IncludeMods)
        {
            chips.Add(new FilterChip("sourceScope", $"Source: {sourceScope.ToDisplayText()}"));
        }

        chips.Add(new FilterChip("domain", Domain == AssetBrowserDomain.BuildBuy ? "Domain: Build/Buy" : "Domain: CAS"));

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            chips.Add(new FilterChip("search", $"Search: {SearchText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(CategoryText))
        {
            chips.Add(new FilterChip("category", $"Category: {CategoryText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(PackageText))
        {
            chips.Add(new FilterChip("package", $"Package: {PackageText.Trim()}"));
        }

        if (HasThumbnailOnly)
        {
            chips.Add(new FilterChip("hasThumbnail", "Has thumbnail"));
        }

        if (VariantsOnly)
        {
            chips.Add(new FilterChip("variants", "Has variants"));
        }

        return chips;
    }

    public string BuildSummary(SourceScope sourceScope, int totalCount, int loadedCount) =>
        $"Assets > {(Domain == AssetBrowserDomain.BuildBuy ? "Build/Buy" : "CAS")} > {sourceScope.ToDisplayText()} · {totalCount:N0} matches · showing {loadedCount:N0} sorted by {Sort}";

    public void RemoveFilter(string key)
    {
        switch (key)
        {
            case "domain":
                Domain = AssetBrowserDomain.BuildBuy;
                break;
            case "search":
                SearchText = string.Empty;
                break;
            case "category":
                CategoryText = string.Empty;
                break;
            case "package":
                PackageText = string.Empty;
                break;
            case "hasThumbnail":
                HasThumbnailOnly = false;
                break;
            case "variants":
                VariantsOnly = false;
                break;
        }
    }

    public void ResetFilters()
    {
        SearchText = string.Empty;
        Domain = AssetBrowserDomain.BuildBuy;
        CategoryText = string.Empty;
        PackageText = string.Empty;
        HasThumbnailOnly = false;
        VariantsOnly = false;
        Sort = AssetBrowserSort.Name;
    }
}

public sealed class RawResourceBrowserState
{
    public string SearchText { get; set; } = string.Empty;
    public RawResourceDomain Domain { get; set; } = RawResourceDomain.All;
    public string TypeNameText { get; set; } = string.Empty;
    public string PackageText { get; set; } = string.Empty;
    public string GroupHexText { get; set; } = string.Empty;
    public string InstanceHexText { get; set; } = string.Empty;
    public bool PreviewableOnly { get; set; }
    public bool ExportCapableOnly { get; set; }
    public bool CompressedKnownOnly { get; set; }
    public ResourceLinkFilter LinkFilter { get; set; } = ResourceLinkFilter.Any;
    public RawResourceSort Sort { get; set; } = RawResourceSort.TypeName;

    public RawResourceBrowserQuery ToQuery(SourceScope sourceScope, int offset, int windowSize) =>
        new(
            sourceScope,
            SearchText.Trim(),
            Domain,
            TypeNameText.Trim(),
            PackageText.Trim(),
            GroupHexText.Trim(),
            InstanceHexText.Trim(),
            PreviewableOnly,
            ExportCapableOnly,
            CompressedKnownOnly,
            LinkFilter,
            Sort,
            offset,
            windowSize);

    public IReadOnlyList<FilterChip> BuildFilterChips(SourceScope sourceScope)
    {
        var chips = new List<FilterChip>();
        if (!sourceScope.IncludeGame || !sourceScope.IncludeDlc || !sourceScope.IncludeMods)
        {
            chips.Add(new FilterChip("sourceScope", $"Source: {sourceScope.ToDisplayText()}"));
        }

        if (Domain != RawResourceDomain.All)
        {
            chips.Add(new FilterChip("domain", $"Domain: {FormatDomain(Domain)}"));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            chips.Add(new FilterChip("search", $"Search: {SearchText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(TypeNameText))
        {
            chips.Add(new FilterChip("type", $"Type: {TypeNameText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(PackageText))
        {
            chips.Add(new FilterChip("package", $"Package: {PackageText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(GroupHexText))
        {
            chips.Add(new FilterChip("group", $"Group: {GroupHexText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(InstanceHexText))
        {
            chips.Add(new FilterChip("instance", $"Instance: {InstanceHexText.Trim()}"));
        }

        if (PreviewableOnly)
        {
            chips.Add(new FilterChip("previewable", "Previewable"));
        }

        if (ExportCapableOnly)
        {
            chips.Add(new FilterChip("export", "Export-capable"));
        }

        if (CompressedKnownOnly)
        {
            chips.Add(new FilterChip("compressed", "Compression known"));
        }

        if (LinkFilter != ResourceLinkFilter.Any)
        {
            chips.Add(new FilterChip("link", LinkFilter == ResourceLinkFilter.LinkedOnly ? "Linked only" : "Unlinked only"));
        }

        return chips;
    }

    public string BuildSummary(SourceScope sourceScope, int totalCount, int loadedCount) =>
        $"Raw resources > {FormatDomain(Domain)} > {sourceScope.ToDisplayText()} · {totalCount:N0} matches · showing {loadedCount:N0} sorted by {Sort}";

    public void RemoveFilter(string key)
    {
        switch (key)
        {
            case "domain":
                Domain = RawResourceDomain.All;
                break;
            case "search":
                SearchText = string.Empty;
                break;
            case "type":
                TypeNameText = string.Empty;
                break;
            case "package":
                PackageText = string.Empty;
                break;
            case "group":
                GroupHexText = string.Empty;
                break;
            case "instance":
                InstanceHexText = string.Empty;
                break;
            case "previewable":
                PreviewableOnly = false;
                break;
            case "export":
                ExportCapableOnly = false;
                break;
            case "compressed":
                CompressedKnownOnly = false;
                break;
            case "link":
                LinkFilter = ResourceLinkFilter.Any;
                break;
        }
    }

    public void ResetFilters()
    {
        SearchText = string.Empty;
        Domain = RawResourceDomain.All;
        TypeNameText = string.Empty;
        PackageText = string.Empty;
        GroupHexText = string.Empty;
        InstanceHexText = string.Empty;
        PreviewableOnly = false;
        ExportCapableOnly = false;
        CompressedKnownOnly = false;
        LinkFilter = ResourceLinkFilter.Any;
        Sort = RawResourceSort.TypeName;
    }

    private static string FormatDomain(RawResourceDomain domain) => domain switch
    {
        RawResourceDomain.All => "All",
        RawResourceDomain.Images => "Images",
        RawResourceDomain.Audio => "Audio",
        RawResourceDomain.TextXml => "Text/XML",
        RawResourceDomain.ThreeDRelated => "3D-related",
        RawResourceDomain.OtherUnknown => "Other/Unknown",
        _ => domain.ToString()
    };
}

public static class ResourceBrowserClassifier
{
    public static bool IsImage(ResourceMetadata resource) =>
        resource.PreviewKind == PreviewKind.Texture ||
        resource.Key.TypeName is "BuyBuildThumbnail" or "BodyPartThumbnail" or "CASPartThumbnail";

    public static bool IsAudio(ResourceMetadata resource) =>
        resource.PreviewKind == PreviewKind.Audio ||
        resource.Key.TypeName.Contains("Audio", StringComparison.OrdinalIgnoreCase);

    public static bool IsTextXml(ResourceMetadata resource) =>
        resource.PreviewKind == PreviewKind.Text ||
        resource.Key.TypeName.Contains("XML", StringComparison.OrdinalIgnoreCase) ||
        resource.Key.TypeName.Contains("Tuning", StringComparison.OrdinalIgnoreCase) ||
        resource.Key.TypeName.Contains("Manifest", StringComparison.OrdinalIgnoreCase) ||
        resource.Key.TypeName.Contains("StringTable", StringComparison.OrdinalIgnoreCase);

    public static bool IsThreeDRelated(ResourceMetadata resource) =>
        resource.PreviewKind == PreviewKind.Scene ||
        resource.Key.TypeName is "Geometry" or "Model" or "ModelLOD" or "Rig" or "MaterialDefinition";

    public static bool IsOtherOrUnknown(ResourceMetadata resource) =>
        !IsImage(resource) &&
        !IsAudio(resource) &&
        !IsTextXml(resource) &&
        !IsThreeDRelated(resource);
}
