using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Sims-style CAS camera. Four fixed FRAMINGS (full height / cowboy / chest / face) computed from the
    /// character's measured height; you switch between them, and the CHARACTER rotates on a turntable
    /// (left-drag or the panel's Turn buttons) rather than the camera flying around — exactly how CAS
    /// works. Scroll fine-tunes the zoom within the current shot. The camera sits in front of the Sim
    /// (looking −Z) so the default view is the face side; the Sim faces +Z after the build.
    /// </summary>
    [ExecuteAlways] // frame the camera in the editor too, so the Main Camera preview shows the shot pre-Play
    [AddComponentMenu("Sims4 Creator/CAS Camera Rig")]
    [RequireComponent(typeof(Camera))]
    public sealed class CasCameraRig : MonoBehaviour
    {
        public static readonly string[] ShotNames = { "Full", "Cowboy", "Chest", "Face" };

        [Tooltip("The Sim root — rotated for the turntable. Auto-found if empty.")]
        public Transform character;
        [Range(0, 3)] public int shot;
        public float rotateSpeed = 6f;

        [Tooltip("Vertical FOV. ~22° ≈ 90mm-equiv — telephoto, so proportions read straight with no wide-angle " +
                 "distortion; the rig moves the camera further back to keep the framing.")]
        [Range(14f, 45f)] public float fieldOfView = 22f;
        [Tooltip("Small near clip so tight face shots don't slice into the head.")]
        public float nearClip = 0.02f;

        // Per shot: fraction of body height for the look-at pivot, vertical fit (as a fraction of height),
        // and a slight downward pitch. Tuned to read like CAS; scroll adjusts fit ±.
        private static readonly (float center, float fit, float pitch)[] Frames =
        {
            (0.52f, 1.15f, 4f), // full height — toes to crown
            (0.72f, 0.60f, 3f), // cowboy — mid-thigh up
            (0.84f, 0.40f, 1f), // chest — chest up
            (0.915f, 0.18f, 0f), // face — pivot ON THE NOSE (0.95 sat on the forehead), tighter head fit
        };

        private Camera _cam;
        private CasController _ui;
        private float _feetY;
        private float _height = 1.7f;
        private float _zoom;    // -1..1 fine zoom within a shot
        private bool _measured;

        private void OnEnable()
        {
            _cam = GetComponent<Camera>();
            _ui = FindFirstObjectByType<CasController>();
            if (character == null)
            {
                var c = FindFirstObjectByType<Sims4Character>();
                if (c != null) character = c.transform;
            }
            _measured = false;
            Measure();
            Apply();
        }

        /// <summary>Re-measure the Sim and re-frame the camera (called by the scene builder after a rebuild).</summary>
        public void Refresh()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            Measure();
            Apply();
        }

        /// <summary>Measure the Sim's world height + feet level from its renderers (call after a rebuild).</summary>
        public void Measure()
        {
            if (character == null) return;
            var any = false;
            Bounds b = default;
            foreach (var r in character.GetComponentsInChildren<Renderer>())
            {
                if (r is SkinnedMeshRenderer || r is MeshRenderer)
                {
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            if (any) { _feetY = b.min.y; _height = Mathf.Max(0.5f, b.size.y); _measured = true; }
        }

        private void LateUpdate()
        {
            if (!_measured && character != null) Measure();
            if (Application.isPlaying) // input only in Play mode; the editor just holds the framed shot
            {
                var overUI = _ui != null && _ui.PointerOverPanel;
                if (!overUI)
                {
                    if (Input.GetMouseButton(0) && character != null)
                        character.Rotate(0f, -Input.GetAxis("Mouse X") * rotateSpeed, 0f, Space.World);
                    var sc = Input.GetAxis("Mouse ScrollWheel");
                    if (Mathf.Abs(sc) > 0.0001f) _zoom = Mathf.Clamp(_zoom + sc * 4f, -1f, 1f); // scroll up = zoom in
                }
            }
            Apply();
        }

        public void SetShot(int s) { shot = Mathf.Clamp(s, 0, Frames.Length - 1); _zoom = 0f; }
        public void RotateCharacter(float deg) { if (character != null) character.Rotate(0f, deg, 0f, Space.World); }
        public void ResetRotation() { if (character != null) character.rotation = Quaternion.identity; }

        private void Apply()
        {
            if (_cam == null) return;
            // Telephoto lens + tiny near clip: straight proportions, no face-shot clipping.
            _cam.usePhysicalProperties = false;
            _cam.fieldOfView = fieldOfView;
            _cam.nearClipPlane = nearClip;

            var f = Frames[Mathf.Clamp(shot, 0, Frames.Length - 1)];
            var vfov = fieldOfView * Mathf.Deg2Rad;
            var fit = _height * f.fit * Mathf.Lerp(1.25f, 0.7f, (_zoom + 1f) * 0.5f);
            var dist = (fit * 0.5f) / Mathf.Tan(vfov * 0.5f);

            var basePos = character != null ? character.position : Vector3.zero;
            var pivot = new Vector3(basePos.x, _feetY + f.center * _height, basePos.z);
            var rot = Quaternion.Euler(f.pitch, 180f, 0f); // looks −Z, so the camera stands on the +Z (front) side
            transform.position = pivot - (rot * Vector3.forward) * dist;
            transform.rotation = rot;
        }
    }
}
