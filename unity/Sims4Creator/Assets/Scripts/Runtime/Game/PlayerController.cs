using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Point-and-click possession (decision D-002). Click a Sim to take control — its autonomy suspends,
    /// so the AI stops choosing for it. While possessing, click a smart object to queue that object's
    /// interaction on the possessed Sim (the same interaction the AI would run). Esc or the HUD's Release
    /// button hands control back to the brain. Needs keep ticking the whole time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        public Camera cam;
        public SimulationDirector director;
        [Tooltip("Used to ignore world clicks that land on a UI panel.")]
        public GameHudUI ui;

        public SimBody Possessed { get; private set; }
        public string LastActionMessage { get; private set; } = "";

        private GameObject _marker;
        private float _lastClickTime = -10f;
        private bool _doubleClick;
        private const float DoubleClickSeconds = 0.35f;

        private void Start()
        {
            if (cam == null) cam = Camera.main;
            if (director == null) director = FindFirstObjectByType<SimulationDirector>();
            _marker = BuildMarker();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Release();
            }
            else if (Input.GetMouseButtonDown(0) && cam != null && !(ui != null && ui.IsPointerOverUI()))
            {
                // Double-click = "hurry": the directed action is performed at a run.
                float now = Time.unscaledTime;
                _doubleClick = (now - _lastClickTime) < DoubleClickSeconds;
                _lastClickTime = now;

                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 200f))
                {
                    var body = hit.collider.GetComponentInParent<SimBody>();
                    if (body != null)
                    {
                        // Possessing + clicking ANOTHER Sim = go socialize with them; clicking nobody
                        // (or re-clicking the same Sim) possesses. Switch possession via Esc then click.
                        if (Possessed != null && body != Possessed) DirectSocial(body);
                        else Possess(body);
                    }
                    else
                    {
                        var obj = hit.collider.GetComponentInParent<SmartObject>();
                        if (obj != null && Possessed != null) DirectTo(obj);
                        else if (Possessed != null) DirectMove(hit.point); // click the ground to walk there
                    }
                }
            }

            UpdateMarker();
        }

        public void Possess(SimBody body)
        {
            if (body == null || body == Possessed) return;
            if (Possessed != null) SetAutonomy(Possessed, true);
            Possessed = body;
            SetAutonomy(body, false);
            LastActionMessage = $"Possessed {Label(body)}";
        }

        public void Release()
        {
            if (Possessed == null) return;
            SetAutonomy(Possessed, true);
            LastActionMessage = $"Released {Label(Possessed)}";
            Possessed = null;
        }

        private void DirectTo(SmartObject obj)
        {
            if (Possessed == null) return;
            var agent = Possessed.GetComponent<SimAgent>();
            if (agent != null && obj.advertises.Count > 0)
            {
                var ad = obj.advertises[0]; // first offering; a context menu picks among many later
                agent.Hurry = _doubleClick;
                agent.Begin(obj, ad);
                LastActionMessage = $"{Label(Possessed)} {(_doubleClick ? "⇒ hurries to" : "→")} {obj.displayName}: {ad.label}";
            }
        }

        private void DirectMove(Vector3 point)
        {
            if (Possessed == null) return;
            var agent = Possessed.GetComponent<SimAgent>();
            if (agent == null) return;
            agent.Hurry = _doubleClick;   // set BEFORE GoTo so the status reads right
            agent.GoTo(point);
            LastActionMessage = $"{Label(Possessed)} {(_doubleClick ? "⇒ runs to a spot" : "→ walks to a spot")}";
        }

        private void DirectSocial(SimBody other)
        {
            if (Possessed == null || other == null) return;
            var agent = Possessed.GetComponent<SimAgent>();
            if (agent != null)
            {
                agent.Hurry = _doubleClick;
                agent.BeginSocial(other);
                LastActionMessage = $"{Label(Possessed)} {(_doubleClick ? "⇒ hurries to" : "→")} chat with {Label(other)}";
            }
        }

        private static void SetAutonomy(SimBody body, bool on)
        {
            var a = body.GetComponent<SimAgent>();
            if (a != null) a.AutonomyEnabled = on;
        }

        private static string Label(SimBody b) => b.Soul != null ? b.Soul.displayName : b.displayName;

        private void UpdateMarker()
        {
            if (_marker == null) return;
            bool show = Possessed != null;
            if (_marker.activeSelf != show) _marker.SetActive(show);
            if (show) _marker.transform.position = Possessed.transform.position + Vector3.up * 2.05f;
        }

        private static GameObject BuildMarker()
        {
            var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m.name = "Possession Marker";
            var col = m.GetComponent<Collider>();
            if (col != null) Destroy(col); // must not block selection raycasts
            m.transform.localScale = Vector3.one * 0.22f;

            var r = m.GetComponent<Renderer>();
            if (r != null)
            {
                var mat = new Material(r.sharedMaterial);
                var c = new Color(0.30f, 1f, 0.55f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
                if (mat.HasProperty("_EmissiveColor")) { mat.SetColor("_EmissiveColor", c * 3f); mat.EnableKeyword("_EMISSION"); }
                r.sharedMaterial = mat;
            }
            m.SetActive(false);
            return m;
        }
    }
}
