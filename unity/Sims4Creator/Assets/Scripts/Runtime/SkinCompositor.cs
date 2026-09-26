using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Recomposes a Sims skin albedo into a <see cref="RenderTexture"/> from a base-tone color
    /// texture, any number of active grayscale detail layers (overlay blend, alpha = coverage) and
    /// an eye overlay (source-over), using the <c>Hidden/Sims4/SkinComposite</c> shader via
    /// <see cref="Graphics.Blit(Texture, RenderTexture, Material, int)"/>.
    ///
    /// The compose math is fixed by the project contract and lives in the shader; this class only
    /// drives the Blit. The shader processes up to 4 detail layers per pass, so when more than 4
    /// details are active we ping-pong a second RenderTexture and feed the previous result back in
    /// as the base (each extra pass folds in up to 4 more details). The eye overlay is applied only
    /// on the final pass.
    ///
    /// IMPORTANT (color space): the base/eye textures import as sRGB color (Unity linearizes them on
    /// sample); the detail textures import LINEAR so the shader sees their RAW gamma-authored bytes.
    /// The DETAIL step (grayscale overlay relief AND "over" colored washes, per-layer _DetailMode)
    /// runs in GAMMA space — the shader converts the base linear→gamma, blends against the raw
    /// detail bytes, and converts back (fixed 2026-07-10: running overlay in linear space
    /// desaturated warm skin into gray-green "dirt"; this also matches the exporter's CPU preview
    /// bake, which operates on raw bytes). The eye source-over stays in linear. The working
    /// RenderTexture is sRGB, so Graphics.Blit sRGB-encodes on store and HDRP re-linearizes on
    /// sample — a correct round-trip for a color map; with zero active details the output is
    /// bit-identical to a plain base passthrough.
    /// </summary>
    public sealed class SkinCompositor : System.IDisposable
    {
        private const string ShaderName = "Hidden/Sims4/SkinComposite";
        private const int MaxDetailsPerPass = 4;

        private static readonly int BaseTexId = Shader.PropertyToID("_BaseTex");
        private static readonly int EyeTexId = Shader.PropertyToID("_EyeTex");
        private static readonly int DetailCountId = Shader.PropertyToID("_DetailCount");
        private static readonly int HasEyeId = Shader.PropertyToID("_HasEye");
        private static readonly int[] DetailIds =
        {
            Shader.PropertyToID("_Detail0"),
            Shader.PropertyToID("_Detail1"),
            Shader.PropertyToID("_Detail2"),
            Shader.PropertyToID("_Detail3"),
        };
        private static readonly int[] DetailModeIds =
        {
            Shader.PropertyToID("_DetailMode0"),
            Shader.PropertyToID("_DetailMode1"),
            Shader.PropertyToID("_DetailMode2"),
            Shader.PropertyToID("_DetailMode3"),
        };

        private Material _material;
        private RenderTexture _rtA;
        private RenderTexture _rtB;
        private int _width;
        private int _height;

        /// <summary>The most recently composed albedo. Stable across recomposes of the same size.</summary>
        public RenderTexture Result { get; private set; }

        private Material Material
        {
            get
            {
                if (_material == null)
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader == null)
                    {
                        Debug.LogError($"[Sims4Creator] Skin composite shader '{ShaderName}' not found.");
                        return null;
                    }

                    _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                }

                return _material;
            }
        }

        /// <summary>
        /// Compose <paramref name="baseTex"/> + the given active <paramref name="details"/> + optional
        /// <paramref name="eyeTex"/> into the managed RenderTexture and return it. The RT is sized to
        /// the base texture (falls back to 1024 when unknown). Null entries in <paramref name="details"/>
        /// are skipped. <paramref name="sourceOver"/> (parallel to details; null = all overlay) marks
        /// layers that composite as COLORED source-over washes (e.g. PsBoss) instead of grayscale
        /// overlay relief. Returns null if the base texture or shader is missing.
        /// </summary>
        public RenderTexture Compose(Texture baseTex, IReadOnlyList<Texture> details, IReadOnlyList<bool> sourceOver, Texture eyeTex)
        {
            var material = Material;
            if (baseTex == null || material == null)
            {
                return null;
            }

            var active = new List<Texture>();
            var modes = new List<float>();
            if (details != null)
            {
                for (var i = 0; i < details.Count; i++)
                {
                    if (details[i] != null)
                    {
                        active.Add(details[i]);
                        modes.Add(sourceOver != null && i < sourceOver.Count && sourceOver[i] ? 1f : 0f);
                    }
                }
            }

            var w = baseTex.width > 0 ? baseTex.width : 1024;
            var h = baseTex.height > 0 ? baseTex.height : 1024;
            EnsureTargets(w, h);

            // Number of Blit passes: at least one (base + first up-to-4 details), plus one per extra
            // group of 4 details. The eye overlay rides on the final pass only.
            var passCount = Mathf.Max(1, Mathf.CeilToInt(active.Count / (float)MaxDetailsPerPass));

            Texture currentBase = baseTex;
            RenderTexture dst = _rtA;
            RenderTexture other = _rtB;

            for (var pass = 0; pass < passCount; pass++)
            {
                var isLast = pass == passCount - 1;

                material.SetTexture(BaseTexId, currentBase);

                // Bind this pass's slice of details (up to 4); pad the rest with black.
                var start = pass * MaxDetailsPerPass;
                var countThisPass = Mathf.Min(MaxDetailsPerPass, active.Count - start);
                for (var k = 0; k < MaxDetailsPerPass; k++)
                {
                    material.SetTexture(DetailIds[k], k < countThisPass ? active[start + k] : Texture2D.blackTexture);
                    material.SetFloat(DetailModeIds[k], k < countThisPass ? modes[start + k] : 0f);
                }

                material.SetFloat(DetailCountId, countThisPass);

                if (isLast && eyeTex != null)
                {
                    material.SetTexture(EyeTexId, eyeTex);
                    material.SetFloat(HasEyeId, 1f);
                }
                else
                {
                    material.SetTexture(EyeTexId, Texture2D.blackTexture);
                    material.SetFloat(HasEyeId, 0f);
                }

                Graphics.Blit(currentBase, dst, material, 0);

                // Ping-pong: next pass reads what we just wrote.
                currentBase = dst;
                (dst, other) = (other, dst);
            }

            // After the loop, the last write went to 'other' (we swapped after it). 'currentBase' holds it.
            Result = (RenderTexture)currentBase;
            return Result;
        }

        private void EnsureTargets(int w, int h)
        {
            if (_rtA != null && (_width != w || _height != h))
            {
                ReleaseTargets();
            }

            if (_rtA == null)
            {
                _width = w;
                _height = h;
                _rtA = CreateRt(w, h, "Sims4SkinRT_A");
                _rtB = CreateRt(w, h, "Sims4SkinRT_B");
            }
        }

        private static RenderTexture CreateRt(int w, int h, string name)
        {
            // sRGB RT: the shader composes in LINEAR space; Blit sRGB-encodes on store so HDRP reads
            // the result as a normal sRGB _BaseColorMap (re-linearizes on sample). See class doc for
            // the contract caveat (gamma- vs linear-space overlay).
            var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
            {
                sRGB = true,
                useMipMap = false,
                autoGenerateMips = false,
            };
            var rt = new RenderTexture(desc)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        private void ReleaseTargets()
        {
            if (_rtA != null)
            {
                _rtA.Release();
                Object.DestroyImmediate(_rtA);
                _rtA = null;
            }

            if (_rtB != null)
            {
                _rtB.Release();
                Object.DestroyImmediate(_rtB);
                _rtB = null;
            }

            Result = null;
        }

        public void Dispose()
        {
            ReleaseTargets();
            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }
        }
    }
}
