// ClipAnimationExporter — decodes one Sims 4 CLIP animation (e.g. ad_CAS_idle_stand_x) and writes
// an animation.json next to an exported character, carrying per-bone keyframe tracks the Unity
// builder turns into an AnimationClip.
//
// Pipeline: find the CLIP resource (by 64-bit instance hex, or by name substring) → decode it via
// the shared Ts4ClipDecoder → load the adult-human rig (auRig) to map each channel's FNV-32 target
// back to a bone NAME → group channels per bone into rotation / translation / scale tracks.
//
// COORDINATE CONVENTION: the character mesh + bind-pose matrices are exported in RAW TS4 space with
// NO axis flip (TS4 and Unity are both left-handed Y-up, so identity renders correctly — see
// Sims4CharacterBuilder which applies bindPose verbatim). The clip stores each bone's LOCAL-to-parent
// transform per frame in that SAME space, so the values below apply DIRECTLY as Unity localRotation /
// localPosition. Quaternion component order is X,Y,Z,W in both System.Numerics and Unity. If the idle
// looks twisted in Unity, that is the place to add a basis change — the data here is faithful TS4.

using System.Globalization;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sims4ResourceExplorer.Core;
using Sims4ResourceExplorer.Preview;

namespace Sims4UnityExport;

// ---------------------------------------------------------------------------
// animation.json data contract (camelCase field names are LOAD-BEARING).
// ---------------------------------------------------------------------------
internal sealed class AnimManifest
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("fps")] public float Fps { get; set; }
    [JsonPropertyName("duration")] public float Duration { get; set; }
    [JsonPropertyName("rigNamespace")] public string RigNamespace { get; set; } = string.Empty;
    // True when the clip's b__ROOT_bind__ matches auRig's bind (gameplay clips) → locals apply
    // DIRECTLY in Unity. False for CAS-rig clips (ad_CAS_*), which the Unity builder skips.
    [JsonPropertyName("auRigNative")] public bool AuRigNative { get; set; } = true;
    [JsonPropertyName("tracks")] public List<AnimTrack> Tracks { get; set; } = new();
}

internal sealed class AnimTrack
{
    [JsonPropertyName("bone")] public string Bone { get; set; } = string.Empty;
    // auRig parent bone name — lets the Unity builder re-compose ancestors that are MISSING from the
    // exported skeleton (b__ROOT_bind__ was never exported; its rotation is collapsed into the locals
    // of its children b__Pelvis__/b__Spine0__, so its animated motion must be composed into them).
    [JsonPropertyName("parent")] public string Parent { get; set; } = string.Empty;
    // auRig's BIND local rotation for this bone. Clip locals are auRig-relative; the builder applies
    // prefix = unityBindLocal * inv(rest) before each clip value (identity for most bones).
    // (0,0,0,0) = absent → the builder uses prefix = identity (direct application).
    [JsonPropertyName("restX")] public float RestX { get; set; }
    [JsonPropertyName("restY")] public float RestY { get; set; }
    [JsonPropertyName("restZ")] public float RestZ { get; set; }
    [JsonPropertyName("restW")] public float RestW { get; set; }
    // auRig's BIND local position (needed when composing a missing-ancestor chain).
    [JsonPropertyName("restPX")] public float RestPX { get; set; }
    [JsonPropertyName("restPY")] public float RestPY { get; set; }
    [JsonPropertyName("restPZ")] public float RestPZ { get; set; }
    [JsonPropertyName("rotations")] public List<AnimQuatKey> Rotations { get; set; } = new();
    [JsonPropertyName("translations")] public List<AnimVecKey> Translations { get; set; } = new();
    [JsonPropertyName("scales")] public List<AnimVecKey> Scales { get; set; } = new();
}

internal sealed class AnimQuatKey
{
    [JsonPropertyName("t")] public float T { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
    [JsonPropertyName("w")] public float W { get; set; }
}

internal sealed class AnimVecKey
{
    [JsonPropertyName("t")] public float T { get; set; }
    [JsonPropertyName("x")] public float X { get; set; }
    [JsonPropertyName("y")] public float Y { get; set; }
    [JsonPropertyName("z")] public float Z { get; set; }
}

internal static class ClipAnimationExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>A curated set of adult standing CAS idles (instance ids) exported by default so the
    /// Unity character has several idles to switch between. All rig="x" (actor rig), 202 channels.</summary>
    // GAMEPLAY clips only (a_*): these are authored on auRig itself, so their locals apply DIRECTLY
    // (proven via the probe retarget-lab: direct+frozen-root = walking at ground level, arms down).
    // ad_CAS_* clips are authored on the CAS rig (different rest convention) and are EXCLUDED — the
    // exporter stamps auRigNative=false on them and the Unity builder skips.
    //
    // LOCOMOTION comes as SINGLE-STEP clips ("LFoot"/"RFoot" = one step, ~0.3-0.4s — the game's
    // locomotion system chains them). One step alone looks cropped, so multi-instance entries are
    // CONCATENATED on the time axis into one seamless L+R gait-cycle json.
    private static readonly (string[] Instances, string Label)[] CuratedIdles =
    {
        // Locomotion (step-pairs concatenated) + a dance — for non-CAS / demo use.
        (new[] { "AF20065A537E00DD", "48619AAC4731F3F3" }, "walk"),         // a_loco_default_walk_LFoot_long + RFoot_long
        (new[] { "E3DECC1BF2F2C0D2", "278BD843F1F65FB4" }, "run"),          // a_loco_run_LFoot + RFoot
        (new[] { "042AB2FAB987012E", "D5E1EE168CEA7C18" }, "walkFeminine"), // a_loco_walk_feminine_LFoot_medium + RFoot_medium
        (new[] { "17B4C3AE317E4A27" }, "danceMachine"),                     // a_trait_danceMachine_x — full-body dance
        // STANDING IDLE variety — each carries its own blink/gaze + subtle face + body fidget. The CAS
        // panel filters to these ("idle"/"waiting"/"lookaround" in the name) and shuffles among them.
        (new[] { "B0B78CE54BB7F8DD" }, "idle_waiting"),        // a_idle_waiting_loop_1_x
        (new[] { "B0C88CE54BC66BA0" }, "idle_waiting2"),       // a_idle_waiting_loop_4_x
        (new[] { "A7A009E8E1215F2E" }, "idle_lookAround"),     // a_idle_lookAround_female_x
        (new[] { "A5482F93DA706E64" }, "idle_lookBothWays"),   // a_idle_female_lookBothWays_x
        (new[] { "6AB7674DD1D16119" }, "idle_handfidgetSmile"),// a_idle_female_handfidget_smile_x — smiles
        (new[] { "4DE3BB0E5B3B588C" }, "idle_handFidget"),     // a_idle_handFidget_female_x
    };

    /// <summary>Exports the curated idle SET into &lt;slug&gt;/animations/*.json (clearing it first), so
    /// the Unity builder can offer a switchable list. Returns the count successfully written.</summary>
    public static async Task<int> RunCuratedAsync(
        string unityAssetsDir, string slug, IIndexStore index, IResourceCatalogService catalog, CancellationToken ct)
    {
        var animDir = Path.Combine(unityAssetsDir, slug, "animations");
        if (Directory.Exists(animDir))
            foreach (var f in Directory.EnumerateFiles(animDir, "*.json")) File.Delete(f);
        Directory.CreateDirectory(animDir);

        var ok = 0;
        foreach (var (instances, label) in CuratedIdles)
        {
            Console.WriteLine($"--- exporting idle '{label}' ({string.Join("+", instances)}) ---");
            if (await RunAsync(unityAssetsDir, slug, index, catalog, instances, ct)) ok++;
        }
        Console.WriteLine($"[clip] curated idle set: {ok}/{CuratedIdles.Length} written to {animDir}");
        return ok;
    }

    /// <summary>Single-selector convenience overload (CLI `exportclip <slug> <selector>`).</summary>
    public static Task<bool> RunAsync(
        string unityAssetsDir, string slug, IIndexStore index, IResourceCatalogService catalog,
        string clipSelector, CancellationToken ct)
        => RunAsync(unityAssetsDir, slug, index, catalog, new[] { clipSelector }, ct);

    /// <summary>
    /// Decodes one or more clips (16-hex instance ids, or clip-name substrings) and writes ONE
    /// &lt;slug&gt;/animations/*.json. Multiple selectors are CONCATENATED on the time axis into a single
    /// seamless sequence — used to join per-step locomotion clips (LFoot+RFoot) into a full gait cycle.
    /// Returns false when any selector misses.
    /// </summary>
    public static async Task<bool> RunAsync(
        string unityAssetsDir,
        string slug,
        IIndexStore index,
        IResourceCatalogService catalog,
        IReadOnlyList<string> clipSelectors,
        CancellationToken ct)
    {
        // ---- 1. Rigs (shared for all segments) -------------------------------------------------
        var (hashToBone, restMap) = await BuildBoneHashMapAsync(index, catalog, ct);
        if (hashToBone.Count == 0) { Console.Error.WriteLine("[clip] could not load any rig — aborting."); return false; }
        _ = restMap; // animRig kept only for name resolution of animRig-only bones

        // ---- 2. Decode every segment and concatenate on the time axis --------------------------
        var merged = new Dictionary<string, AnimTrack>(StringComparer.Ordinal);
        float timeOffset = 0;
        float fps = 30;
        string firstName = string.Empty, rigNs = string.Empty;
        var auRigNative = true;
        int unresolvedTotal = 0, segments = 0;

        foreach (var selector in clipSelectors)
        {
            var clipBytes = await FindClipBytesAsync(index, catalog, selector, ct);
            if (clipBytes is null) { Console.Error.WriteLine($"[clip] no CLIP matching '{selector}'."); return false; }

            var clip = Ts4ClipDecoder.Decode(clipBytes);
            if (clip is null) { Console.Error.WriteLine($"[clip] decoder returned null for '{selector}'."); return false; }
            Console.WriteLine($"[clip] '{clip.Name}' rigNs='{clip.RigNamespace}' fps={clip.Fps:0.##} dur={clip.Duration:0.00}s channels={clip.Channels.Count}");

            var (trackList, _, unresolved) = BuildRetargetedTracks(clip, hashToBone);
            unresolvedTotal += unresolved;
            if (segments == 0) { firstName = clip.Name; rigNs = clip.RigNamespace; fps = clip.Fps; }

            // auRig-native check per segment: gameplay clips carry b__ROOT_bind__ ≈ auRig bind
            // (0.5,0.5,0.5,0.5); CAS clips carry the CAS-rig root (≈ -90°Y) and must be skipped in Unity.
            var rbHash = Ts4ClipDecoder.Fnv32("b__ROOT_bind__");
            var rbCh = clip.Channels.FirstOrDefault(c => c.TargetHash == rbHash && c.SubTarget == Ts4ClipSubTarget.Orientation && c.Keyframes.Count > 0);
            if (rbCh is not null && hashToBone.TryGetValue(rbHash, out var rbBone))
            {
                var k0 = rbCh.Keyframes[0];
                var q0 = new Quaternion(k0.X, k0.Y, k0.Z, k0.W);
                var qb = rbBone.Rotation;
                var dot = Math.Abs(q0.X * qb.X + q0.Y * qb.Y + q0.Z * qb.Z + q0.W * qb.W);
                var deg = 2 * Math.Acos(Math.Min(1, dot)) * 180 / Math.PI;
                if (deg >= 25)
                {
                    auRigNative = false;
                    Console.WriteLine($"[clip] WARNING: '{clip.Name}' root is {deg:0}° off auRig bind — NOT auRig-native (CAS-rig clip); Unity will skip it.");
                }
            }

            // Append this segment's keys shifted by the accumulated duration.
            foreach (var tr in trackList)
            {
                if (!merged.TryGetValue(tr.Bone, out var m))
                {
                    m = new AnimTrack
                    {
                        Bone = tr.Bone, Parent = tr.Parent,
                        RestX = tr.RestX, RestY = tr.RestY, RestZ = tr.RestZ, RestW = tr.RestW,
                        RestPX = tr.RestPX, RestPY = tr.RestPY, RestPZ = tr.RestPZ,
                    };
                    merged[tr.Bone] = m;
                }
                foreach (var k in tr.Rotations)
                    m.Rotations.Add(new AnimQuatKey { T = k.T + timeOffset, X = k.X, Y = k.Y, Z = k.Z, W = k.W });
                foreach (var k in tr.Translations)
                    m.Translations.Add(new AnimVecKey { T = k.T + timeOffset, X = k.X, Y = k.Y, Z = k.Z });
            }
            timeOffset += Math.Max(1, clip.NumTicks) * clip.TickLength; // next segment starts one tick after this one's last key
            segments++;
        }

        var manifest = new AnimManifest
        {
            Name = segments > 1 ? $"{firstName}_x{segments}cycle" : firstName,
            Fps = fps,
            Duration = timeOffset,
            RigNamespace = rigNs,
            AuRigNative = auRigNative,
            Tracks = merged.Values.OrderBy(t => t.Bone, StringComparer.Ordinal).ToList(),
        };
        var rotKeys = manifest.Tracks.Sum(t => t.Rotations.Count);

        // ---- 3. Write into <slug>/animations/<name>.json ---------------------------------------
        var animFolder = Path.Combine(unityAssetsDir, slug, "animations");
        Directory.CreateDirectory(animFolder);
        var safeName = new string((manifest.Name.Length > 0 ? manifest.Name : "clip").Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        var path = Path.Combine(animFolder, safeName + ".json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(manifest, JsonOptions), ct);

        Console.WriteLine($"[clip] wrote {path}");
        Console.WriteLine($"[clip] segments={segments} tracks={manifest.Tracks.Count} rotKeys={rotKeys} dur={manifest.Duration:0.00}s; " +
                          $"unresolved channels={unresolvedTotal} (face/CAS/twist/IK bones absent from auRig — skipped).");
        return true;
    }

    // Locate CLIP bytes by 16-hex instance id or name substring (first parseable match wins).
    private static async Task<byte[]?> FindClipBytesAsync(
        IIndexStore index, IResourceCatalogService catalog, string clipSelector, CancellationToken ct)
    {
        if (TryParseHex(clipSelector, out var instance))
        {
            var res = (await index.GetResourcesByFullInstanceAsync(instance, ct))
                .Where(r => string.Equals(r.Key.TypeName, "Clip", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var r in res)
            {
                byte[] b; try { b = await catalog.GetResourceBytesAsync(r.PackagePath, r.Key, raw: false, ct); } catch { continue; }
                if (Ts4ClipDecoder.PeekHeader(b) is not null) return b;
            }
            Console.WriteLine($"[clip] lookup by instance {instance:X16}: {res.Count} Clip resource(s), none parseable.");
            return null;
        }

        var clips = await index.GetResourcesByTypeNameAsync("Clip", ct);
        Console.WriteLine($"[clip] scanning {clips.Count} Clip resource(s) for name containing '{clipSelector}'...");
        foreach (var r in clips)
        {
            byte[] b; try { b = await catalog.GetResourceBytesAsync(r.PackagePath, r.Key, raw: false, ct); } catch { continue; }
            var hdr = Ts4ClipDecoder.PeekHeader(b);
            if (hdr is null || hdr.Value.Name.IndexOf(clipSelector, StringComparison.OrdinalIgnoreCase) < 0) continue;
            Console.WriteLine($"[clip] matched '{hdr.Value.Name}' {r.Key.FullTgi}");
            return b;
        }
        return null;
    }

    // The clip AUTHORING rig ("x" actor rig, 178 bones). Found by scanning ALL Rig resources for the
    // rest pose a CAS stand idle HOLDS (ProbeAsset --find-anim-rig): thighΔ=0.8°, rootΔ=2.1°,
    // spine0.X=0.0825 — auRig does NOT match (thigh≈identity, spine 0.117/0.162/0.16). Its per-bone
    // rest is what clip rotations are relative to; the Unity builder retargets source-rest → our bind.
    private const ulong AnimRigInstance = 0x52385BC57E4DB284UL;

    // Load BOTH rigs: auRig gives complete NAME resolution (it has every deform bone the clips target);
    // the ANIMATION rig gives the SOURCE REST local rotation for the bones it shares (43 core body
    // bones — spine/limbs/head; it lacks the twist/adjuster bones). Bones without a source rest emit
    // rest=0 and the Unity builder falls back to tick-0 rest-matching for them (chain stays consistent
    // because Wc0 is composed per bone the same way as Wc).
    private static async Task<(Dictionary<uint, Ts4RigBone> nameMap, Dictionary<uint, Ts4RigBone> restMap)> BuildBoneHashMapAsync(
        IIndexStore index, IResourceCatalogService catalog, CancellationToken ct)
    {
        async Task<Dictionary<uint, Ts4RigBone>> LoadAsync(ulong inst)
        {
            var m = new Dictionary<uint, Ts4RigBone>();
            var rigRes = (await index.GetResourcesByFullInstanceAsync(inst, ct))
                .Where(r => string.Equals(r.Key.TypeName, "Rig", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var rr in rigRes)
            {
                byte[] rb; try { rb = await catalog.GetResourceBytesAsync(rr.PackagePath, rr.Key, raw: false, ct); } catch { continue; }
                Ts4RigResource rig; try { rig = Ts4RigResource.Parse(rb); } catch { continue; }
                foreach (var bn in rig.Bones) m[bn.NameHash] = bn;
            }
            return m;
        }
        var nameMap = await LoadAsync(Ts4CanonicalRigCatalog.ComputeFnv64("auRig"));
        var restMap = await LoadAsync(AnimRigInstance);
        foreach (var kv in restMap) nameMap.TryAdd(kv.Key, kv.Value); // anim-rig-only bones still resolve
        Console.WriteLine($"[clip] rigs: auRig+animRig nameMap={nameMap.Count} bones; animRig rest for {restMap.Count}.");
        return (nameMap, restMap);
    }

    // Emit each animated bone's RAW clip local-to-parent rotation, resampled at integer ticks, plus the
    // SOURCE (animation) rig's rest local rotation per bone. We do NOT retarget here: the world-space
    // retarget (make each bone rotate in world space as the clip's bone does relative to the SOURCE rest)
    // must run in Unity, using Unity's OWN bind rotations and Unity quaternion math — doing it here in
    // System.Numerics (right-handed, row-vector) then handing quaternions to Unity (left-handed,
    // column-vector) mis-composes and tips the whole body horizontal.
    private static (List<AnimTrack> tracks, int animatedBones, int unresolved) BuildRetargetedTracks(
        Ts4ClipResource clip, Dictionary<uint, Ts4RigBone> hashToBone)
    {
        var channelByHash = new Dictionary<uint, Ts4ClipChannel>();
        int unresolved = 0;
        foreach (var ch in clip.Channels)
        {
            if (ch.SubTarget != Ts4ClipSubTarget.Orientation || ch.Keyframes.Count == 0) continue;
            if (!hashToBone.ContainsKey(ch.TargetHash)) { unresolved++; continue; }
            channelByHash[ch.TargetHash] = ch; // last wins (orientation duplicates are rare)
        }

        // Translation channels for the ROOT chain only: b__ROOT_bind__ carries the authored root
        // motion — pelvis bob + lateral sway (loco clips are IN-PLACE: forward Z stays 0; travel is
        // added by the game's locomotion system, not the clip). Other bones keep bind positions.
        var transByHash = new Dictionary<uint, Ts4ClipChannel>();
        var rootBindHash = Ts4ClipDecoder.Fnv32("b__ROOT_bind__");
        foreach (var ch in clip.Channels)
            if (ch.SubTarget == Ts4ClipSubTarget.Translation && ch.Keyframes.Count > 0 && ch.TargetHash == rootBindHash)
                transByHash[ch.TargetHash] = ch;

        var tracks = new List<AnimTrack>();
        var withRest = 0;
        var numTicks = Math.Max(1, clip.NumTicks);
        // UNION of rotation-keyed and translation-keyed bones: some clips (e.g. a_trait_danceMachine_x)
        // animate b__ROOT_bind__ with a TRANSLATION channel only (pelvis bob, no rotation channel) —
        // rotation keys are then synthesized as the constant auRig bind (delta = identity in Unity).
        var allHashes = channelByHash.Keys.Union(transByHash.Keys)
            .Where(h => hashToBone.ContainsKey(h))
            .OrderBy(h => hashToBone[h].Name, StringComparer.Ordinal)
            .ToList();
        foreach (var hash in allHashes)
        {
            var bone = hashToBone[hash];
            var hasRot = channelByHash.TryGetValue(hash, out var ch);
            var tr = new AnimTrack
            {
                Bone = bone.Name,
                Parent = bone.ParentHash is uint ph && hashToBone.TryGetValue(ph, out var pb) ? pb.Name : string.Empty,
            };
            {
                // auRig BIND local (nameMap is auRig-first; animRig-only bones don't exist in Unity anyway)
                var r = bone.Rotation;
                tr.RestX = r.X; tr.RestY = r.Y; tr.RestZ = r.Z; tr.RestW = r.W;
                tr.RestPX = bone.Position.X; tr.RestPY = bone.Position.Y; tr.RestPZ = bone.Position.Z;
                withRest++;
            }
            for (var t = 0; t < numTicks; t++)
            {
                var q = hasRot ? SampleQuat(ch!, t) : bone.Rotation; // no rot channel → constant bind
                tr.Rotations.Add(new AnimQuatKey { T = t * clip.TickLength, X = q.X, Y = q.Y, Z = q.Z, W = q.W });
            }
            if (transByHash.TryGetValue(hash, out var tch))
            {
                for (var t = 0; t < numTicks; t++)
                {
                    var v = SampleVec(tch, t);
                    tr.Translations.Add(new AnimVecKey { T = t * clip.TickLength, X = v.X, Y = v.Y, Z = v.Z });
                }
            }
            tracks.Add(tr);
        }
        Console.WriteLine($"[clip] {withRest}/{tracks.Count} tracks carry the auRig bind local; root-motion translations on {tracks.Count(t => t.Translations.Count > 0)} track(s).");
        return (tracks, tracks.Count, unresolved);
    }

    // Sample a translation channel at an integer tick (lerp between surrounding keyframes; clamp ends).
    private static Vector3 SampleVec(Ts4ClipChannel ch, int tick)
    {
        var kf = ch.Keyframes;
        Vector3 V(Ts4ClipKeyframe k) => new(k.X, k.Y, k.Z);
        if (kf.Count == 1 || tick <= kf[0].Tick) return V(kf[0]);
        if (tick >= kf[^1].Tick) return V(kf[^1]);
        for (var i = 0; i < kf.Count - 1; i++)
        {
            if (tick >= kf[i].Tick && tick <= kf[i + 1].Tick)
            {
                var span = kf[i + 1].Tick - kf[i].Tick;
                var frac = span > 0 ? (float)(tick - kf[i].Tick) / span : 0f;
                return Vector3.Lerp(V(kf[i]), V(kf[i + 1]), frac);
            }
        }
        return V(kf[^1]);
    }

    // Sample a channel's quaternion at an integer tick (slerp between surrounding keyframes; clamp ends).
    private static Quaternion SampleQuat(Ts4ClipChannel ch, int tick)
    {
        var kf = ch.Keyframes;
        Quaternion Q(Ts4ClipKeyframe k) => new(k.X, k.Y, k.Z, k.W);
        if (kf.Count == 1 || tick <= kf[0].Tick) return Q(kf[0]);
        if (tick >= kf[^1].Tick) return Q(kf[^1]);
        for (var i = 0; i < kf.Count - 1; i++)
        {
            if (tick >= kf[i].Tick && tick <= kf[i + 1].Tick)
            {
                var span = kf[i + 1].Tick - kf[i].Tick;
                var frac = span > 0 ? (float)(tick - kf[i].Tick) / span : 0f;
                return Quaternion.Normalize(Quaternion.Slerp(Q(kf[i]), Q(kf[i + 1]), frac));
            }
        }
        return Q(kf[^1]);
    }

    private static bool TryParseHex(string s, out ulong value)
    {
        var hex = s.Trim().TrimStart('#').Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);
        return ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }
}
