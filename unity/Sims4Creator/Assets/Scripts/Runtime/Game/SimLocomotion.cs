using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Drives a Sim's animation from how fast it is actually moving: standing → a varied idle, moving →
    /// walk, moving fast → run. Speed is measured from the transform delta, so this works regardless of
    /// what moves the Sim (steering today, grid pathfinding now).
    ///
    /// FOOT SLIDING is solved by measurement rather than guesswork. While a locomotion clip plays, the
    /// PLANTED foot slides backward in character-local space at exactly the ground speed the animation
    /// depicts. So on first use of each clip we play it at rate 1 for a moment, measure that backward
    /// speed, and cache it — then play the clip at <c>groundSpeed / nativeSpeed</c> so the feet stick.
    /// The cache is static, so each clip is measured once per session, not once per Sim.
    ///
    /// Clips are resolved by NAME FRAGMENT against the legacy <c>Animation</c> component the character
    /// builder populates, so exact exported clip names don't have to be hard-coded.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimLocomotion : MonoBehaviour
    {
        [Header("Clip fragments (matched against the imported clips)")]
        public string[] idleFragments = { "idle_waiting_loop_1", "idle_waiting_loop_4" };
        public string walkFragment = "loco_default_walk";
        public string runFragment = "loco_run";

        [Header("Fallback clip speeds (m/s) if measurement fails")]
        public float walkClipSpeedFallback = 1.15f;
        public float runClipSpeedFallback = 3.2f;
        [Tooltip("Seconds of stance-foot sampling used to measure a clip's native speed.")]
        public float calibrateSeconds = 0.6f;

        [Header("Thresholds")]
        public float moveThreshold = 0.06f;  // below this the Sim counts as standing
        public float runThreshold = 1.9f;    // above this the run clip is used
        public float crossFade = 0.2f;
        public float idleSwitchSeconds = 7f;

        /// <summary>Measured native speed per clip name, shared by every Sim.</summary>
        private static readonly Dictionary<string, float> NativeSpeed = new Dictionary<string, float>();

        private Animation _anim;
        private Sims4IdleSwitcher _switcher;
        private string[] _idles = Array.Empty<string>();
        private string _walk, _run, _current;
        private Vector3 _lastPos;
        private float _speed, _idleTimer;
        private int _idleIndex = -1;

        // stance-foot calibration
        private Transform _footL, _footR, _lastStance;
        private float _lastStanceZ, _lastYaw, _calSum, _calTime;
        private string _calClip;

        public float Speed => _speed;

        /// <summary>Apply a gender-appropriate clip set. Female gets the feminine walk + female idles.</summary>
        public void ApplyDefaultSet(bool feminine)
        {
            walkFragment = feminine ? "loco_walk_feminine" : "loco_default_walk";
            idleFragments = feminine
                ? new[] { "idle_waiting_loop_1", "idle_waiting_loop_4", "idle_lookAround_female",
                          "idle_female_lookBothWays", "idle_handFidget_female", "idle_female_handfidget_smile" }
                : new[] { "idle_waiting_loop_1", "idle_waiting_loop_4" };
        }

        private void Awake()
        {
            _anim = GetComponentInChildren<Animation>();
            _switcher = GetComponentInChildren<Sims4IdleSwitcher>();
            _lastPos = transform.position;
            _lastYaw = transform.eulerAngles.y;

            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (_footL == null && t.name.IndexOf("L_Foot", StringComparison.OrdinalIgnoreCase) >= 0) _footL = t;
                else if (_footR == null && t.name.IndexOf("R_Foot", StringComparison.OrdinalIgnoreCase) >= 0) _footR = t;
                if (_footL != null && _footR != null) break;
            }
        }

        private void Start()
        {
            // We own animation now — stop the builder's switcher cycling through EVERY imported clip
            // (yoga / sleep / dance), which is why idle Sims struck odd poses.
            if (_switcher != null) { _switcher.autoCycle = false; _switcher.enabled = false; }

            _walk = Resolve(walkFragment);
            _run = Resolve(runFragment);
            _idles = ResolveAll(idleFragments);
            PlayIdle(true);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                Vector3 d = transform.position - _lastPos; d.y = 0f;
                _speed = Mathf.Lerp(_speed, d.magnitude / dt, 1f - Mathf.Exp(-12f * dt));
                _lastPos = transform.position;
            }
            if (_anim == null) return;

            if (_speed >= runThreshold && _run != null) PlayLoco(_run, runClipSpeedFallback);
            else if (_speed > moveThreshold && _walk != null) PlayLoco(_walk, walkClipSpeedFallback);
            else
            {
                _idleTimer += dt;
                if (!IsIdleClip(_current)) PlayIdle(true);
                else if (_idleTimer >= idleSwitchSeconds) PlayIdle(false);
            }
        }

        private void PlayLoco(string clip, float fallback)
        {
            if (_current != clip)
            {
                _anim.CrossFade(clip, crossFade);
                _current = clip;
                // Never measured this clip? Run a short calibration pass.
                if (!NativeSpeed.ContainsKey(clip) && _footL != null && _footR != null)
                {
                    _calClip = clip; _calSum = 0f; _calTime = 0f; _lastStance = null;
                }
            }

            var st = _anim[clip];
            if (st == null) return;
            st.wrapMode = WrapMode.Loop;

            if (_calClip == clip)
            {
                st.speed = 1f; // measure at the clip's natural rate
                SampleStance(Time.deltaTime);
                if (_calTime >= calibrateSeconds)
                {
                    float measured = _calSum / Mathf.Max(_calTime, 1e-4f);
                    NativeSpeed[clip] = (measured >= 0.4f && measured <= 5f) ? measured : fallback;
                    _calClip = null;
                }
                return;
            }

            float native = NativeSpeed.TryGetValue(clip, out var n) ? n : fallback;
            st.speed = native > 0.01f ? Mathf.Clamp(_speed / native, 0.4f, 2.2f) : 1f;
        }

        /// <summary>Accumulate how fast the planted foot travels backward in character-local space.</summary>
        private void SampleStance(float dt)
        {
            if (dt <= 0f || _footL == null || _footR == null) return;

            // Turning rotates the whole body, which pollutes a local-space measurement — skip those frames.
            float yaw = transform.eulerAngles.y;
            float dYaw = Mathf.Abs(Mathf.DeltaAngle(yaw, _lastYaw));
            _lastYaw = yaw;
            if (dYaw > 1.5f) { _lastStance = null; return; }

            Vector3 lL = transform.InverseTransformPoint(_footL.position);
            Vector3 lR = transform.InverseTransformPoint(_footR.position);
            Transform stance = lL.y <= lR.y ? _footL : _footR;   // planted foot = the lower one
            float z = (stance == _footL) ? lL.z : lR.z;

            if (stance == _lastStance)
            {
                float dz = z - _lastStanceZ;
                if (dz < 0f) { _calSum += -dz; _calTime += dt; } // backward travel only
            }
            _lastStance = stance;
            _lastStanceZ = z;
        }

        private void PlayIdle(bool immediate)
        {
            if (_idles.Length == 0) return;
            int idx = _idleIndex;
            for (int t = 0; t < 8 && (_idles.Length == 1 || idx == _idleIndex); t++)
                idx = UnityEngine.Random.Range(0, _idles.Length);
            _idleIndex = idx;

            var name = _idles[idx];
            var st = _anim[name];
            if (st != null) { st.wrapMode = WrapMode.Loop; st.speed = 1f; }
            _anim.CrossFade(name, immediate ? 0.12f : crossFade);
            _current = name;
            _idleTimer = 0f;
        }

        private bool IsIdleClip(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < _idles.Length; i++) if (_idles[i] == name) return true;
            return false;
        }

        private string[] ResolveAll(string[] fragments)
        {
            if (fragments == null) return Array.Empty<string>();
            var list = new List<string>();
            foreach (var f in fragments)
            {
                var n = Resolve(f);
                if (n != null && !list.Contains(n)) list.Add(n);
            }
            return list.ToArray();
        }

        /// <summary>Find an imported clip whose name contains <paramref name="fragment"/>.</summary>
        private string Resolve(string fragment)
        {
            if (_anim == null || string.IsNullOrEmpty(fragment)) return null;
            foreach (AnimationState st in _anim)
                if (st != null && st.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return st.name;
            return null;
        }
    }
}
