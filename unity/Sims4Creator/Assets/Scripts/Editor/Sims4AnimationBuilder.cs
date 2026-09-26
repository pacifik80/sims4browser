// Sims4AnimationBuilder — turns the exporter's animation.json (decoded from a real game CLIP, e.g.
// ad_CAS_idle_stand_x) into a Unity AnimationClip and attaches it to the built character so the Sim
// plays the genuine game idle in Play mode.
//
// animation.json carries per-bone tracks of LOCAL-to-parent transforms in RAW TS4 space (no axis
// flip — identity convention, same space as the bind poses; see ClipAnimationExporter). So each key
// applies DIRECTLY as the bone's localRotation / localPosition / localScale.
//
// We bake a LEGACY AnimationClip (transform curves keyed by the bone's hierarchy path relative to the
// character root) and drive it with a legacy Animation component set to loop + play automatically. The
// component animates the shared skeleton's bone Transforms; the SkinnedMeshRenderers follow, so every
// body/clothing part deforms together. Blendshape morphs (driven by Sims4Character) are untouched.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sims4Creator.EditorTools
{
    public static class Sims4AnimationBuilder
    {
        // ---- animation.json DTOs (JsonUtility maps public fields by exact JSON name) ----
        [System.Serializable] private sealed class AnimManifest { public string name; public float fps; public float duration; public string rigNamespace; public bool auRigNative = true; public AnimTrack[] tracks; }
        [System.Serializable] private sealed class AnimTrack { public string bone; public string parent; public float restX, restY, restZ, restW; public float restPX, restPY, restPZ; public AnimQuatKey[] rotations; public AnimVecKey[] translations; public AnimVecKey[] scales; }
        [System.Serializable] private sealed class AnimQuatKey { public float t, x, y, z, w; }
        [System.Serializable] private sealed class AnimVecKey { public float t, x, y, z; }

        /// <summary>
        /// Builds an AnimationClip for every &lt;dir&gt;/animations/*.json (fallback: the single
        /// &lt;dir&gt;/animation.json), attaches them to a looping legacy Animation component, and adds a
        /// <c>Sims4IdleSwitcher</c> so the user can flip between idles. Returns false (no error) when
        /// there are no animation JSONs — the caller then falls back to the procedural idle.
        /// </summary>
        public static bool TryBuildAndAttachIdle(GameObject root, string dir, string assetName, Dictionary<string, Transform> byName)
        {
            // Collect clip JSONs: prefer the animations/ SET, else the single animation.json.
            var jsonPaths = new List<string>();
            var animDir = $"{dir}/animations";
            if (Directory.Exists(animDir))
                jsonPaths.AddRange(Directory.GetFiles(animDir, "*.json").OrderBy(p => p));
            else if (File.Exists($"{dir}/animation.json"))
                jsonPaths.Add($"{dir}/animation.json");
            if (jsonPaths.Count == 0) return false;

            // Remove the procedural idle (it would fight the clips by moving the same bones).
            var proc = root.GetComponent<Sims4Creator.Sims4IdleAnimator>();
            if (proc != null) Object.DestroyImmediate(proc);

            var anim = root.GetComponent<Animation>();
            if (anim == null) anim = root.AddComponent<Animation>();

            var clipNames = new List<string>();
            foreach (var jsonPath in jsonPaths)
            {
                var manifest = JsonUtility.FromJson<AnimManifest>(File.ReadAllText(jsonPath));
                if (manifest?.tracks == null || manifest.tracks.Length == 0) continue;
                if (!manifest.auRigNative)
                {
                    Debug.LogWarning($"[Sims4Creator] '{manifest.name}' is a CAS-rig clip (not auRig-native) — skipped.");
                    continue;
                }
                var clip = BuildClip(manifest, root, byName, dir);
                if (clip == null) continue;
                anim.AddClip(clip, clip.name);
                clipNames.Add(clip.name);
            }
            AssetDatabase.SaveAssets();
            if (clipNames.Count == 0) return false;

            anim.clip = anim.GetClip(clipNames[0]);
            anim.wrapMode = WrapMode.Loop;
            anim.playAutomatically = true;

            var switcher = root.GetComponent<Sims4Creator.Sims4IdleSwitcher>();
            if (switcher == null) switcher = root.AddComponent<Sims4Creator.Sims4IdleSwitcher>();
            switcher.clipNames = clipNames.ToArray();

            Debug.Log($"[Sims4Creator] Idle animations: {clipNames.Count} clip(s) [{string.Join(", ", clipNames)}]. " +
                      "Switch via the Sims4IdleSwitcher component (autoCycle, or set clipIndex).");
            return true;
        }

        // Build ONE looping legacy AnimationClip from a manifest, saved as <dir>/<clipName>.anim.
        // GAMEPLAY clips (a_*) are authored on auRig ITSELF (proven: their b__ROOT_bind__/thigh/pelvis
        // tick-0 locals equal auRig's bind), so each track's raw local rotation applies with only a
        // constant per-bone PREFIX correction:
        //     localRotation(t) = prefix * clipLocal(t),   prefix = unityBindLocal * inv(auRigBindLocal)
        // Why the prefix: our exported skeleton produces the SAME world bind pose as auRig, but it
        // collapses the root-chain rotation into the locals of ROOT_bind's children (b__Pelvis__ and
        // b__Spine0__) — for every other bone unityBindLocal == auRigBindLocal and prefix == identity.
        // Without the prefix, setting the pelvis to its auRig-relative clip value wipes the collapsed
        // rotation and the whole body lies down flat. With it, world(bone,t) == auRig world pose(t)
        // exactly (induction over the chain, anchored by the equal world bind). Other rules:
        //   • b__ROOT__/b__ROOT_bind__ are FROZEN at bind: loco clips store the root in the motion-
        //     trajectory frame; freezing = in-place playback (root translation dropped for the same reason).
        //   • CAS-rig clips (manifest.auRigNative == false) are skipped by the caller.
        // Translation/scale are dropped (bone lengths stay ours). Eyelids animate normally — direct
        // values preserve the authored blink.
        private static AnimationClip BuildClip(AnimManifest manifest, GameObject root, Dictionary<string, Transform> byName, string dir)
        {
            var clip = new AnimationClip { legacy = true, frameRate = manifest.fps > 1f ? manifest.fps : 30f };
            var trackByBone = new Dictionary<string, AnimTrack>();
            foreach (var t in manifest.tracks) if (!string.IsNullOrEmpty(t.bone)) trackByBone[t.bone] = t;

            int boundTracks = 0, prefixed = 0, composed = 0, rootMotion = 0;
            foreach (var track in manifest.tracks)
            {
                if (string.IsNullOrEmpty(track.bone) || !byName.TryGetValue(track.bone, out var boneT) || boneT == null) continue;
                if (track.bone == "b__ROOT__") continue; // trajectory root — never animated
                if (track.rotations == null || track.rotations.Length == 0) continue;
                var path = RelativePath(root.transform, boneT);
                if (path == null) continue;

                if (track.bone == "b__ROOT_bind__")
                {
                    // The skeleton export prunes ROOT_bind; the character builder recreates it as an
                    // IDENTITY placeholder and parents Pelvis/Spine0 — whose locals carry the COLLAPSED
                    // rootbind bind — under it. The clip's rootbind channel holds the authored root
                    // motion (pelvis bob + lateral sway + hip yaw; forward Z stays 0 — loco clips are
                    // in-place). Animate the placeholder with rootbind's WORLD-DELTA from its auRig
                    // bind, so the children inherit the motion without double-applying the bind:
                    //   placeholderRot(t) = clipRot(t) * inv(bindRot)            (identity at bind)
                    //   placeholderPos(t) = clipTrans(t) - placeholderRot(t) * bindPos   (zero at bind)
                    var bindRot = new Quaternion(track.restX, track.restY, track.restZ, track.restW);
                    var bn = Mathf.Sqrt(bindRot.x * bindRot.x + bindRot.y * bindRot.y + bindRot.z * bindRot.z + bindRot.w * bindRot.w);
                    if (bn < 0.5f) continue; // no rest info (old JSON) — leave frozen
                    var invBindRot = Quaternion.Inverse(bindRot.normalized);
                    var bindPos = new Vector3(track.restPX, track.restPY, track.restPZ);
                    if (Quaternion.Angle(boneT.localRotation, Quaternion.identity) > 1f || boneT.localPosition.magnitude > 0.01f)
                        Debug.LogWarning($"[Sims4Creator] ROOT_bind placeholder is not at identity ({boneT.localPosition}, {boneT.localRotation.eulerAngles}) — root motion may double-apply.");

                    var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                    var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                    for (var f = 0; f < track.rotations.Length; f++)
                    {
                        var k = track.rotations[f];
                        var dq = (new Quaternion(k.x, k.y, k.z, k.w) * invBindRot).normalized;
                        Vector3 ct;
                        if (track.translations != null && track.translations.Length > 0)
                        {
                            var kt = track.translations[Mathf.Min(f, track.translations.Length - 1)];
                            ct = new Vector3(kt.x, kt.y, kt.z);
                        }
                        else ct = bindPos;
                        var dp = ct - dq * bindPos;
                        cx.AddKey(k.t, dq.x); cy.AddKey(k.t, dq.y); cz.AddKey(k.t, dq.z); cw.AddKey(k.t, dq.w);
                        px.AddKey(k.t, dp.x); py.AddKey(k.t, dp.y); pz.AddKey(k.t, dp.z);
                    }
                    clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
                    clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
                    clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
                    clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
                    clip.SetCurve(path, typeof(Transform), "localPosition.x", px);
                    clip.SetCurve(path, typeof(Transform), "localPosition.y", py);
                    clip.SetCurve(path, typeof(Transform), "localPosition.z", pz);
                    boundTracks++; rootMotion++;
                    continue;
                }

                // MISSING-ANCESTOR chain: bones whose auRig parent was never exported to Unity
                // (b__Pelvis__/b__Spine0__ → parent b__ROOT_bind__). If such an ancestor is ANIMATED,
                // its motion (pelvis bob, sway, hip yaw — the clip's authored "root motion") would be
                // lost, so we COMPOSE it into this bone's curves: W(f) = anc.rot/pos(f) ∘ bone(f), then
                // re-anchor to the bone's actual Unity parent. Trackless missing ancestors (b__ROOT__,
                // identity in auRig) contribute nothing. If no missing ancestor is animated, the chain
                // is constant and the plain constant-prefix path below is exactly equivalent.
                var missingAnimated = new List<AnimTrack>(); // outermost first
                {
                    var pn = track.parent;
                    var guard = 0;
                    while (!string.IsNullOrEmpty(pn) && !byName.ContainsKey(pn) && guard++ < 10)
                    {
                        if (trackByBone.TryGetValue(pn, out var anc) && anc.rotations != null && anc.rotations.Length > 0)
                            missingAnimated.Insert(0, anc);
                        pn = trackByBone.TryGetValue(pn, out var up) ? up.parent : null;
                    }
                }

                if (missingAnimated.Count > 0)
                {
                    // Anchor: bake-time (bind) transform of the bone's real Unity parent, relative to the
                    // character root. W-values live in auRig/character space == unity-bind space.
                    var anchorInvRot = Quaternion.Inverse(boneT.parent.rotation) * root.transform.rotation;
                    var anchorInv = boneT.parent.worldToLocalMatrix * root.transform.localToWorldMatrix;
                    var boneRestPos = new Vector3(track.restPX, track.restPY, track.restPZ);

                    var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve(); var cw = new AnimationCurve();
                    var px = new AnimationCurve(); var py = new AnimationCurve(); var pz = new AnimationCurve();
                    var n = track.rotations.Length;
                    for (var f = 0; f < n; f++)
                    {
                        var wRot = Quaternion.identity;
                        var wPos = Vector3.zero;
                        foreach (var anc in missingAnimated)
                        {
                            var ar = anc.rotations[Mathf.Min(f, anc.rotations.Length - 1)];
                            var aq = new Quaternion(ar.x, ar.y, ar.z, ar.w);
                            Vector3 ap;
                            if (anc.translations != null && anc.translations.Length > 0)
                            {
                                var at = anc.translations[Mathf.Min(f, anc.translations.Length - 1)];
                                ap = new Vector3(at.x, at.y, at.z);
                            }
                            else ap = new Vector3(anc.restPX, anc.restPY, anc.restPZ);
                            wPos += wRot * ap;
                            wRot *= aq;
                        }
                        var k = track.rotations[f];
                        wPos += wRot * boneRestPos;
                        wRot *= new Quaternion(k.x, k.y, k.z, k.w);

                        var q = (anchorInvRot * wRot).normalized;
                        var p = anchorInv.MultiplyPoint3x4(wPos);
                        var tt = k.t;
                        cx.AddKey(tt, q.x); cy.AddKey(tt, q.y); cz.AddKey(tt, q.z); cw.AddKey(tt, q.w);
                        px.AddKey(tt, p.x); py.AddKey(tt, p.y); pz.AddKey(tt, p.z);
                    }
                    clip.SetCurve(path, typeof(Transform), "localRotation.x", cx);
                    clip.SetCurve(path, typeof(Transform), "localRotation.y", cy);
                    clip.SetCurve(path, typeof(Transform), "localRotation.z", cz);
                    clip.SetCurve(path, typeof(Transform), "localRotation.w", cw);
                    clip.SetCurve(path, typeof(Transform), "localPosition.x", px);
                    clip.SetCurve(path, typeof(Transform), "localPosition.y", py);
                    clip.SetCurve(path, typeof(Transform), "localPosition.z", pz);
                    boundTracks++; composed++;
                    continue;
                }

                // prefix = unityBindLocal * inv(auRigBindLocal); identity when rest is absent or equal.
                var prefix = Quaternion.identity;
                var rest = new Quaternion(track.restX, track.restY, track.restZ, track.restW);
                var restNorm = Mathf.Sqrt(rest.x * rest.x + rest.y * rest.y + rest.z * rest.z + rest.w * rest.w);
                if (restNorm > 0.5f)
                {
                    prefix = boneT.localRotation * Quaternion.Inverse(rest.normalized);
                    if (Quaternion.Angle(prefix, Quaternion.identity) > 0.5f) prefixed++;
                }

                var rx = new AnimationCurve(); var ry = new AnimationCurve(); var rz = new AnimationCurve(); var rw = new AnimationCurve();
                foreach (var k in track.rotations)
                {
                    var q = (prefix * new Quaternion(k.x, k.y, k.z, k.w)).normalized;
                    rx.AddKey(k.t, q.x); ry.AddKey(k.t, q.y); rz.AddKey(k.t, q.z); rw.AddKey(k.t, q.w);
                }
                clip.SetCurve(path, typeof(Transform), "localRotation.x", rx);
                clip.SetCurve(path, typeof(Transform), "localRotation.y", ry);
                clip.SetCurve(path, typeof(Transform), "localRotation.z", rz);
                clip.SetCurve(path, typeof(Transform), "localRotation.w", rw);
                boundTracks++;
            }
            if (boundTracks == 0) return null;
            Debug.Log($"[Sims4Creator] '{manifest.name}': {boundTracks} tracks, {prefixed} prefix-corrected, rootMotion={rootMotion} (1 = ROOT_bind placeholder animated: pelvis bob/sway/hip-yaw), composed={composed}.");

            clip.wrapMode = WrapMode.Loop;
            clip.EnsureQuaternionContinuity();

            var safe = new string((string.IsNullOrEmpty(manifest.name) ? "idle" : manifest.name)
                .Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
            var clipPath = $"{dir}/{safe}.anim";
            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (existing != null) AssetDatabase.DeleteAsset(clipPath);
            AssetDatabase.CreateAsset(clip, clipPath);
            return clip; // clip.name is now the asset filename (safe), used for AddClip/CrossFade
        }

        // Hierarchy path of <descendant> relative to <ancestor> (e.g. "b__ROOT_bind__/b__Pelvis__").
        // Returns "" if descendant == ancestor, or null if descendant is not under ancestor.
        private static string RelativePath(Transform ancestor, Transform descendant)
        {
            if (descendant == ancestor) return string.Empty;
            var parts = new List<string>();
            var t = descendant;
            while (t != null && t != ancestor)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            if (t != ancestor) return null; // not a descendant
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
