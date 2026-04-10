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
    int WindowSize,
    AssetCapabilityFilter? CapabilityFilter = null,
    string RootTypeText = "",
    string IdentityTypeText = "",
    string PrimaryGeometryTypeText = "",
    string ThumbnailTypeText = "");

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
    public string RootTypeText { get; set; } = string.Empty;
    public string IdentityTypeText { get; set; } = string.Empty;
    public string PrimaryGeometryTypeText { get; set; } = string.Empty;
    public string ThumbnailTypeText { get; set; } = string.Empty;
    public string PackageText { get; set; } = string.Empty;
    public bool HasThumbnailOnly { get; set; }
    public bool VariantsOnly { get; set; }
    public bool RequireSceneRoot { get; set; }
    public bool RequireExactGeometryCandidate { get; set; }
    public bool RequireMaterialReferences { get; set; }
    public bool RequireTextureReferences { get; set; }
    public bool RequireIdentityMetadata { get; set; }
    public bool RequireRigReference { get; set; }
    public bool RequireGeometryReference { get; set; }
    public bool RequireMaterialResourceCandidate { get; set; }
    public bool RequireTextureResourceCandidate { get; set; }
    public bool RequirePackageLocalGraph { get; set; }
    public bool RequireDiagnostics { get; set; }
    public AssetBrowserSort Sort { get; set; } = AssetBrowserSort.Name;

    public AssetBrowserQuery ToQuery(SourceScope sourceScope, int offset, int windowSize) =>
        new(
            sourceScope,
            SearchText.Trim(),
            Domain,
            NormalizeFacetValue(CategoryText),
            PackageText.Trim(),
            HasThumbnailOnly,
            VariantsOnly,
            Sort,
            offset,
            windowSize,
            new AssetCapabilityFilter(
                RequireSceneRoot,
                RequireExactGeometryCandidate,
                RequireMaterialReferences,
                RequireTextureReferences,
                RequireIdentityMetadata,
                RequireRigReference,
                RequireGeometryReference,
                RequireMaterialResourceCandidate,
                RequireTextureResourceCandidate,
                RequirePackageLocalGraph,
                RequireDiagnostics),
            NormalizeFacetValue(RootTypeText),
            NormalizeFacetValue(IdentityTypeText),
            NormalizeFacetValue(PrimaryGeometryTypeText),
            NormalizeFacetValue(ThumbnailTypeText));

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

        if (!string.IsNullOrWhiteSpace(RootTypeText))
        {
            chips.Add(new FilterChip("rootType", $"Root type: {RootTypeText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(IdentityTypeText))
        {
            chips.Add(new FilterChip("identityType", $"Identity type: {IdentityTypeText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(PrimaryGeometryTypeText))
        {
            chips.Add(new FilterChip("geometryType", $"Geometry type: {PrimaryGeometryTypeText.Trim()}"));
        }

        if (!string.IsNullOrWhiteSpace(ThumbnailTypeText))
        {
            chips.Add(new FilterChip("thumbnailType", $"Thumbnail type: {ThumbnailTypeText.Trim()}"));
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

        if (RequireSceneRoot)
        {
            chips.Add(new FilterChip("sceneRoot", "Has scene root"));
        }

        if (RequireExactGeometryCandidate)
        {
            chips.Add(new FilterChip("exactGeometry", "Has exact geometry"));
        }

        if (RequireMaterialReferences)
        {
            chips.Add(new FilterChip("materialRefs", "Has material refs"));
        }

        if (RequireTextureReferences)
        {
            chips.Add(new FilterChip("textureRefs", "Has texture refs"));
        }

        if (RequireIdentityMetadata)
        {
            chips.Add(new FilterChip("identityMetadata", "Has identity metadata"));
        }

        if (RequireRigReference)
        {
            chips.Add(new FilterChip("rigReference", "Has rig reference"));
        }

        if (RequireGeometryReference)
        {
            chips.Add(new FilterChip("geometryReference", "Has geometry reference"));
        }

        if (RequireMaterialResourceCandidate)
        {
            chips.Add(new FilterChip("materialCandidate", "Has material candidate"));
        }

        if (RequireTextureResourceCandidate)
        {
            chips.Add(new FilterChip("textureCandidate", "Has texture candidate"));
        }

        if (RequirePackageLocalGraph)
        {
            chips.Add(new FilterChip("packageLocal", "Package-local graph"));
        }

        if (RequireDiagnostics)
        {
            chips.Add(new FilterChip("diagnostics", "Has diagnostics"));
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
            case "rootType":
                RootTypeText = string.Empty;
                break;
            case "identityType":
                IdentityTypeText = string.Empty;
                break;
            case "geometryType":
                PrimaryGeometryTypeText = string.Empty;
                break;
            case "thumbnailType":
                ThumbnailTypeText = string.Empty;
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
            case "sceneRoot":
                RequireSceneRoot = false;
                break;
            case "exactGeometry":
                RequireExactGeometryCandidate = false;
                break;
            case "materialRefs":
                RequireMaterialReferences = false;
                break;
            case "textureRefs":
                RequireTextureReferences = false;
                break;
            case "identityMetadata":
                RequireIdentityMetadata = false;
                break;
            case "rigReference":
                RequireRigReference = false;
                break;
            case "geometryReference":
                RequireGeometryReference = false;
                break;
            case "materialCandidate":
                RequireMaterialResourceCandidate = false;
                break;
            case "textureCandidate":
                RequireTextureResourceCandidate = false;
                break;
            case "packageLocal":
                RequirePackageLocalGraph = false;
                break;
            case "diagnostics":
                RequireDiagnostics = false;
                break;
        }
    }

    public void ResetFilters()
    {
        SearchText = string.Empty;
        Domain = AssetBrowserDomain.BuildBuy;
        CategoryText = string.Empty;
        RootTypeText = string.Empty;
        IdentityTypeText = string.Empty;
        PrimaryGeometryTypeText = string.Empty;
        ThumbnailTypeText = string.Empty;
        PackageText = string.Empty;
        HasThumbnailOnly = false;
        VariantsOnly = false;
        RequireSceneRoot = false;
        RequireExactGeometryCandidate = false;
        RequireMaterialReferences = false;
        RequireTextureReferences = false;
        RequireIdentityMetadata = false;
        RequireRigReference = false;
        RequireGeometryReference = false;
        RequireMaterialResourceCandidate = false;
        RequireTextureResourceCandidate = false;
        RequirePackageLocalGraph = false;
        RequireDiagnostics = false;
        Sort = AssetBrowserSort.Name;
    }

    private static string NormalizeFacetValue(string value) =>
        string.Equals(value.Trim(), "All", StringComparison.OrdinalIgnoreCase) ? string.Empty : value.Trim();
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
