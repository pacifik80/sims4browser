// Reference: TS4SimRipper (GPL-3.0)
//   docs/references/external/TS4SimRipper/src/Form1.cs:1404-1455 (GetTS4Rig + bundled rig fallback)
// Build 0289: Sim character pipeline rewrite — loads the canonical Granny rig and
// computes its bind-pose world transforms (parent-walked) for every bone.
//
// The rig stores per-bone LOCAL transforms (position + rotation + scale relative to
// parent). The world bind-pose for a bone is the parent's world × the bone's local.
// Skinning math (SimSkinner) needs these world transforms to put vertices in the right
// frame.
//
// Coordinate system: TS4 uses left-handed Y-up; HelixToolkit uses right-handed Y-up.
// We do NOT convert here — bones come out in TS4 space; the conversion happens at the
// scene-composition boundary so all internal math is consistent.

using System.Numerics;
using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

/// <summary>
/// A loaded rig with bone hierarchy, local transforms, and computed bind-pose world
/// transforms. Immutable; subsequent BOND morphs produce a new <see cref="SimRig"/>
/// rather than mutating this one.
/// </summary>
public sealed record SimRig(
    string Name,
    ulong InstanceHash,
    IReadOnlyList<SimRigBone> Bones,
    IReadOnlyDictionary<uint, int> BoneIndexByHash,
    IReadOnlyDictionary<uint, Matrix4x4> WorldBindPoseByHash);

/// <summary>One bone in a <see cref="SimRig"/>. <c>LocalPosition/Rotation/Scale</c> are
/// the bind-pose values relative to the parent.</summary>
public sealed record SimRigBone(
    string Name,
    uint NameHash,
    int ParentIndex,
    uint? ParentHash,
    Vector3 LocalPosition,
    Quaternion LocalRotation,
    Vector3 LocalScale);

/// <summary>
/// Loads <see cref="SimRig"/> instances by canonical hash. Tries the index store first,
/// then known package paths under the install root. Returns null on miss — caller is
/// responsible for reporting/diagnostics.
/// </summary>
public sealed class SimRigLoader
{
    private readonly IResourceCatalogService catalogService;
    private readonly IIndexStore indexStore;
    private readonly Dictionary<ulong, SimRig> cache = new();

    public SimRigLoader(IResourceCatalogService catalogService, IIndexStore indexStore)
    {
        ArgumentNullException.ThrowIfNull(catalogService);
        ArgumentNullException.ThrowIfNull(indexStore);
        this.catalogService = catalogService;
        this.indexStore = indexStore;
    }

    /// <summary>
    /// Loads the rig at <paramref name="canonicalHash"/>, computing bind-pose world
    /// transforms. Returns null if the rig isn't in the index AND no probe path under
    /// <paramref name="installRootHint"/> contains it.
    /// </summary>
    public async Task<SimRig?> LoadAsync(ulong canonicalHash, string rigName, string? installRootHint, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(canonicalHash, out var cached))
        {
            return cached;
        }

        Ts4RigResource? parsed = null;

        var indexResources = await indexStore.GetResourcesByFullInstanceAsync(canonicalHash, cancellationToken).ConfigureAwait(false);
        var rigResource = indexResources.FirstOrDefault(static r => r.Key.TypeName == "Rig");
        if (rigResource is not null)
        {
            try
            {
                var bytes = await catalogService.GetResourceBytesAsync(rigResource.PackagePath, rigResource.Key, raw: false, cancellationToken).ConfigureAwait(false);
                parsed = Ts4RigResource.Parse(bytes);
            }
            catch
            {
                parsed = null;
            }
        }

        if (parsed is null && installRootHint is not null)
        {
            var installRoot = TryResolveGameInstallRoot(installRootHint);
            if (installRoot is not null)
            {
                var directKey = new ResourceKeyRecord(0x8EAF13DE, 0u, canonicalHash, "Rig");
                foreach (var probePath in SimRigCatalog.GetRigProbePathsUnderInstall(installRoot).Where(File.Exists))
                {
                    try
                    {
                        var bytes = await catalogService.GetResourceBytesAsync(probePath, directKey, raw: false, cancellationToken).ConfigureAwait(false);
                        if (bytes is { Length: > 0 })
                        {
                            parsed = Ts4RigResource.Parse(bytes);
                            break;
                        }
                    }
                    catch { /* try next probe path */ }
                }
            }
        }

        if (parsed is null)
        {
            return null;
        }

        var rig = BuildSimRig(parsed, rigName, canonicalHash);
        cache[canonicalHash] = rig;
        return rig;
    }

    /// <summary>
    /// Constructs a <see cref="SimRig"/> from the parsed <see cref="Ts4RigResource"/>,
    /// computing the world bind-pose for every bone via parent-walked accumulation.
    /// Public so SimBondMorpher can rebuild a morphed rig without re-parsing.
    /// </summary>
    public static SimRig BuildSimRig(Ts4RigResource parsed, string name, ulong instanceHash)
    {
        var bones = parsed.Bones
            .Select(b => new SimRigBone(b.Name, b.NameHash, b.ParentIndex, b.ParentHash, b.Position, b.Rotation, b.Scale))
            .ToArray();

        var indexByHash = new Dictionary<uint, int>(bones.Length);
        for (var i = 0; i < bones.Length; i++)
        {
            indexByHash[bones[i].NameHash] = i;
        }

        var worldByHash = ComputeWorldBindPoses(bones);
        return new SimRig(name, instanceHash, bones, indexByHash, worldByHash);
    }

    /// <summary>
    /// Walks every bone up its parent chain (memoised) to produce world bind-pose
    /// matrices. Order in the input array doesn't matter — each bone's world transform
    /// is the parent's world × the bone's local (T·R·S).
    /// </summary>
    private static IReadOnlyDictionary<uint, Matrix4x4> ComputeWorldBindPoses(IReadOnlyList<SimRigBone> bones)
    {
        var world = new Dictionary<uint, Matrix4x4>(bones.Count);
        var byHash = bones.ToDictionary(b => b.NameHash);

        Matrix4x4 GetWorld(SimRigBone bone)
        {
            if (world.TryGetValue(bone.NameHash, out var cached))
            {
                return cached;
            }

            var local =
                Matrix4x4.CreateScale(bone.LocalScale) *
                Matrix4x4.CreateFromQuaternion(bone.LocalRotation) *
                Matrix4x4.CreateTranslation(bone.LocalPosition);

            var parentWorld = bone.ParentHash is uint ph && byHash.TryGetValue(ph, out var parent)
                ? GetWorld(parent)
                : Matrix4x4.Identity;

            var result = local * parentWorld;
            world[bone.NameHash] = result;
            return result;
        }

        foreach (var bone in bones)
        {
            GetWorld(bone);
        }
        return world;
    }

    /// <summary>
    /// Walks up from <paramref name="packagePath"/> looking for a directory with a "Data"
    /// child. Mirrors the existing helper in <c>BuildBuySceneBuildService</c>; duplicated
    /// here so SimRigLoader doesn't depend on that legacy class.
    /// </summary>
    private static string? TryResolveGameInstallRoot(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
        {
            return null;
        }

        var directory = new DirectoryInfo(Path.GetDirectoryName(packagePath)!);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Data")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }
}
