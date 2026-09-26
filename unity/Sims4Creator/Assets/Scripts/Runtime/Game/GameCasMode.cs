using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// In-game CAS (M4): open the Create-A-Sim editor on a selected Sim without leaving the game. Pauses
    /// the world, turns the Sim to face the camera, gives an orbit/zoom camera (there is no CasCameraRig
    /// in the game scene, so this supplies the controls), and spins up the existing <see cref="CasController"/>
    /// bound to that Sim's body. Exit restores everything and resumes gameplay.
    ///
    /// CasController binds its target in Awake (once per instance), so we create a FRESH controller each
    /// session: make its GameObject inactive, set the character, then activate it so Awake binds correctly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCasMode : MonoBehaviour
    {
        public GameDebugHud hud;
        public PlayerController player;
        public GameClock clock;
        public Camera cam;

        [Tooltip("Right-drag to orbit, scroll to zoom while in CAS.")]
        public float orbitSensitivity = 3f;
        public float zoomSensitivity = 0.4f;

        public bool InCas { get; private set; }

        private CasController _cas;
        private SimBody _editedSim;
        private Vector3 _savedCamPos;
        private Quaternion _savedCamRot;
        private Quaternion _savedSimRot;

        // orbit state
        private Vector3 _target;
        private float _yaw, _pitch, _dist;

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            if (hud == null) hud = FindFirstObjectByType<GameDebugHud>();
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (clock == null) clock = FindFirstObjectByType<GameClock>();
        }

        public void EnterCas(SimBody sim)
        {
            if (InCas || sim == null) return;
            var ch = sim.Character;
            if (ch == null) return;
            _editedSim = sim;

            // Turn the Sim to face the camera (front view), remembering its rotation to restore on exit.
            _savedSimRot = sim.transform.rotation;
            sim.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up); // face −Z

            // Orbit camera in front of the Sim; remember the game camera to restore on exit.
            if (cam != null)
            {
                _savedCamPos = cam.transform.position;
                _savedCamRot = cam.transform.rotation;
            }
            _target = sim.transform.position + Vector3.up * 1.0f;
            _yaw = 0f;      // 0 = directly in front (the −Z side the Sim now faces)
            _pitch = 6f;
            _dist = 2.3f;
            UpdateCamera();

            // Fresh CasController bound to this Sim (create inactive → set target → activate → Awake binds).
            var go = new GameObject("CAS (in-game)");
            go.SetActive(false);
            _cas = go.AddComponent<CasController>();
            _cas.character = ch;
            _cas.idle = sim.GetComponentInChildren<Sims4IdleSwitcher>();
            _cas.rig = null;
            _cas.visible = true;
            go.SetActive(true);

            if (hud != null) hud.enabled = false;
            if (player != null) player.enabled = false;
            // Pause via the clock so exiting restores the player's chosen speed, not a hard 1×.
            if (clock != null) clock.ExternalPause = true; else Time.timeScale = 0f;
            InCas = true;
        }

        public void ExitCas()
        {
            if (!InCas) return;
            if (clock != null) clock.ExternalPause = false; else Time.timeScale = 1f;

            if (_cas != null) Destroy(_cas.gameObject);
            _cas = null;

            if (_editedSim != null)
            {
                _editedSim.transform.rotation = _savedSimRot;
                // CasController.Awake disabled the Sim's idle; restore it so it lives again after editing.
                var sw = _editedSim.GetComponentInChildren<Sims4IdleSwitcher>();
                if (sw != null) { sw.enabled = true; sw.autoCycle = true; }
                else
                {
                    var proc = _editedSim.GetComponentInChildren<Sims4IdleAnimator>();
                    if (proc != null) proc.enabled = true;
                }
            }
            _editedSim = null;

            if (cam != null) { cam.transform.position = _savedCamPos; cam.transform.rotation = _savedCamRot; }
            if (hud != null) hud.enabled = true;
            if (player != null) player.enabled = true;
            InCas = false;
        }

        private void Update()
        {
            if (!InCas) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { ExitCas(); return; }

            bool changed = false;
            if (Input.GetMouseButton(1)) // right-drag to orbit (left button is the CAS panel)
            {
                _yaw += Input.GetAxis("Mouse X") * orbitSensitivity;
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * orbitSensitivity, -25f, 70f);
                changed = true;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                _dist = Mathf.Clamp(_dist - scroll * zoomSensitivity, 0.45f, 6f);
                changed = true;
            }
            if (changed) UpdateCamera();
        }

        private void UpdateCamera()
        {
            if (cam == null) return;
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 pos = _target + rot * new Vector3(0f, 0f, -_dist);
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation((_target - pos).normalized, Vector3.up);
        }

        private void OnGUI()
        {
            if (!InCas) return;
            const float w = 300f, h = 30f;
            var r = new Rect((Screen.width - w) * 0.5f, 8f, w, h);
            if (GUI.Button(r, "◄  Back to Game  (Esc)   ·   right-drag/scroll: camera")) ExitCas();
        }
    }
}
