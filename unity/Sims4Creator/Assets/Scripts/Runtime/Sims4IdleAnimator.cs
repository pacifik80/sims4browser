using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// A lightweight PROCEDURAL idle for play mode: subtle breathing on the chest/ribcage/spine plus a
    /// slow weight-shift sway, driven by sine waves around each bone's bind-pose local rotation. No
    /// AnimationClip, no Animator, no Humanoid avatar — it just nudges the existing EA-named bones, so
    /// it works on our assembled skeleton as-is. Restores from the captured base each frame (never
    /// accumulates), and only runs while playing so edit-mode posing is untouched.
    /// </summary>
    [AddComponentMenu("Sims4 Creator/Sims4 Idle Animator")]
    [DisallowMultipleComponent]
    public sealed class Sims4IdleAnimator : MonoBehaviour
    {
        [Header("Breathing (chest / ribcage / spine)")]
        [Tooltip("Pitch amplitude in degrees.")] public float breathDegrees = 2.2f;
        [Tooltip("Breaths per minute.")] public float breathsPerMinute = 14f;

        [Header("Weight-shift sway (lower spine)")]
        [Tooltip("Roll amplitude in degrees.")] public float swayDegrees = 1.3f;
        [Tooltip("Sway cycles per second.")] public float swayHz = 0.18f;

        [Header("Head micro-motion")]
        [Tooltip("Tiny head drift in degrees.")] public float headDegrees = 1.0f;

        private readonly List<Transform> _breath = new();
        private readonly List<Quaternion> _breathBase = new();
        private Transform _sway;
        private Quaternion _swayBase;
        private Transform _head;
        private Quaternion _headBase;
        private float _seed;

        private void OnEnable()
        {
            Capture();
            // Per-instance phase so multiple characters don't breathe in lockstep. (Time-based seed is
            // fine here; this is cosmetic and not part of the resumable export pipeline.)
            _seed = (GetInstanceID() & 0x3FF) * 0.01f;
        }

        /// <summary>Re-scan the skeleton for the bones we drive and snapshot their bind-pose rotations.</summary>
        public void Capture()
        {
            _breath.Clear();
            _breathBase.Clear();
            _sway = null;
            _head = null;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                var n = t.name.ToLowerInvariant();
                if (n.Contains("ribcage") || n.Contains("spine2") || n.Contains("chest"))
                {
                    _breath.Add(t);
                    _breathBase.Add(t.localRotation);
                }
                if (_sway == null && (n.Contains("spine0") || n.Contains("spine1")))
                {
                    _sway = t;
                    _swayBase = t.localRotation;
                }
                if (_head == null && n.Contains("head") && !n.Contains("forehead"))
                {
                    _head = t;
                    _headBase = t.localRotation;
                }
            }
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            var t = Time.time + _seed;

            var breath = Mathf.Sin(t * (breathsPerMinute / 60f) * 2f * Mathf.PI) * breathDegrees;
            for (var i = 0; i < _breath.Count; i++)
            {
                if (_breath[i] != null)
                {
                    _breath[i].localRotation = _breathBase[i] * Quaternion.Euler(breath, 0f, 0f);
                }
            }

            if (_sway != null)
            {
                var sway = Mathf.Sin(t * swayHz * 2f * Mathf.PI) * swayDegrees;
                _sway.localRotation = _swayBase * Quaternion.Euler(0f, 0f, sway);
            }

            if (_head != null)
            {
                var hx = Mathf.Sin(t * 0.11f * 2f * Mathf.PI) * headDegrees;
                var hy = Mathf.Sin(t * 0.07f * 2f * Mathf.PI) * headDegrees;
                _head.localRotation = _headBase * Quaternion.Euler(hx, hy, 0f);
            }
        }
    }
}
