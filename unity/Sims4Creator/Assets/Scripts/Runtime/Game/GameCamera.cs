using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The free gameplay camera — orbits a ground pivot, like a Sims/city-builder camera:
    ///   • **Pan**  — WASD / arrow keys, or middle-mouse drag (view-relative; faster when zoomed out).
    ///   • **Orbit**— right-mouse DRAG (≥5 px), or Q / E for yaw.
    ///   • **Zoom** — mouse wheel (not over UI).
    ///   • **Focus**— F re-centres on the possessed Sim.
    ///
    /// Runs on UNSCALED time so it works while the sim is paused. Stays LIVE in build mode — flying the
    /// lot is essential while building; only CAS (which drives the camera itself) takes it over.
    ///
    /// Input arbitration:
    ///   • Over-UI comes from <see cref="UiPointer"/> (HUD + build dock), and is LATCHED at button-down:
    ///     a drag that starts in the world stays camera-owned when it crosses the dock, and one that
    ///     starts on the dock never moves the camera.
    ///   • Right button: orbit engages only after a 5 px drag (the pre-threshold delta is applied on
    ///     engage, so nothing jumps). <see cref="RightDragOrbiting"/> lets build mode treat a short
    ///     right-CLICK as "cancel placing" without ever fighting an orbit.
    ///   • All keys are ignored while a text field (dock search) has focus.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCamera : MonoBehaviour
    {
        public Camera cam;
        public GameHudUI ui;
        public GameCasMode cas;
        public GameBuildMode build;
        public PlayerController player;

        [Header("Speeds")]
        public float panSpeed = 7f;
        public float keyRotateSpeed = 110f;
        public float dragOrbitSpeed = 3.2f;
        public float zoomSpeed = 1.6f;

        [Header("Limits")]
        public float minDistance = 2.5f;
        public float maxDistance = 24f;
        public float minPitch = 14f;
        public float maxPitch = 78f;
        public Vector2 boundsMin = new Vector2(-16f, -16f);
        public Vector2 boundsMax = new Vector2(16f, 16f);

        [Header("State")]
        public Vector3 pivot = Vector3.zero;
        public float yaw = 0f;
        public float pitch = 42f;
        public float distance = 13f;

        /// <summary>True while the right button is held AND has moved ≥5 px — i.e. a camera orbit, not a
        /// click. Stays true through the release frame so click handlers on mouse-up can test it.</summary>
        public bool RightDragOrbiting { get; private set; }

        private bool _midDownOverUi;
        private bool _rightDownOverUi;
        private Vector3 _rightDownPos;
        private Vector2 _pendingOrbit;   // pre-threshold accumulated delta, applied when the drag engages

        private void Start()
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) cam = Camera.main;
            if (ui == null) ui = FindFirstObjectByType<GameHudUI>();
            if (cas == null) cas = FindFirstObjectByType<GameCasMode>();
            if (build == null) build = FindFirstObjectByType<GameBuildMode>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            Apply();
        }

        private void LateUpdate()
        {
            // CAS owns the camera (it saves/restores the pose and orbits the Sim itself). Build does NOT.
            if (cas != null && cas.InCas) return;

            float dt = Time.unscaledDeltaTime;
            bool overUI = UiPointer.OverAnyUi();
            bool typing = UiPointer.TextInputFocused();

            // Latch over-UI at the moment each drag button goes down.
            if (Input.GetMouseButtonDown(2)) _midDownOverUi = overUI;
            if (Input.GetMouseButtonDown(1))
            {
                _rightDownOverUi = overUI;
                _rightDownPos = Input.mousePosition;
                _pendingOrbit = Vector2.zero;
                RightDragOrbiting = false;
            }

            // --- pan ---
            Vector3 move = Vector3.zero;
            if (!typing)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) move.z += 1f;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) move.z -= 1f;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) move.x -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) move.x += 1f;
            }
            if (Input.GetMouseButton(2) && !_midDownOverUi) // middle-drag = grab the world
            {
                move.x -= Input.GetAxis("Mouse X") * 1.4f;
                move.z -= Input.GetAxis("Mouse Y") * 1.4f;
            }
            if (move.sqrMagnitude > 1e-4f)
            {
                Vector3 world = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.z);
                float zoomScale = Mathf.Clamp(distance / 12f, 0.5f, 2f); // pan faster when zoomed out
                pivot += world.normalized * (panSpeed * dt * zoomScale * Mathf.Min(move.magnitude, 1.5f));
                pivot.x = Mathf.Clamp(pivot.x, boundsMin.x, boundsMax.x);
                pivot.z = Mathf.Clamp(pivot.z, boundsMin.y, boundsMax.y);
                pivot.y = 0f;
            }

            // --- orbit ---
            if (!typing)
            {
                if (Input.GetKey(KeyCode.Q)) yaw -= keyRotateSpeed * dt;
                if (Input.GetKey(KeyCode.E)) yaw += keyRotateSpeed * dt;
            }
            if (Input.GetMouseButton(1) && !_rightDownOverUi)
            {
                var delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
                if (!RightDragOrbiting)
                {
                    _pendingOrbit += delta;
                    if ((Input.mousePosition - _rightDownPos).sqrMagnitude > 25f) // 5 px: click → drag
                    {
                        RightDragOrbiting = true;
                        delta = _pendingOrbit; // apply what accumulated before the threshold — no jump
                    }
                }
                if (RightDragOrbiting)
                {
                    yaw += delta.x * dragOrbitSpeed;
                    pitch = Mathf.Clamp(pitch - delta.y * dragOrbitSpeed * 0.7f, minPitch, maxPitch);
                }
            }
            else if (!Input.GetMouseButton(1) && !Input.GetMouseButtonUp(1))
            {
                RightDragOrbiting = false; // cleared the frame AFTER release, so mouse-up handlers see it
            }

            // --- zoom ---
            if (!overUI)
            {
                float s = Input.mouseScrollDelta.y;
                if (Mathf.Abs(s) > 0.001f) distance = Mathf.Clamp(distance - s * zoomSpeed, minDistance, maxDistance);
            }

            // --- focus the possessed Sim ---
            if (!typing && Input.GetKeyDown(KeyCode.F) && player != null && player.Possessed != null)
                pivot = player.Possessed.transform.position;

            Apply();
        }

        private void Apply()
        {
            if (cam == null) return;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 pos = pivot + rot * new Vector3(0f, 0f, -distance);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((pivot - pos).normalized, Vector3.up);
        }
    }
}
