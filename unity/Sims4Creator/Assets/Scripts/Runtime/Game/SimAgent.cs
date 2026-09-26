using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Executes what a Sim does on its body: walk to a smart object and use it, or walk to another Sim
    /// and socialize. M1 uses straight-line locomotion (the lot is open); NavMesh arrives when lots gain
    /// walls. The brain (or, when possessed, the player) decides *what* via <see cref="Begin"/> /
    /// <see cref="BeginSocial"/>; this component carries it out.
    ///
    /// Social is a two-party interaction. The initiator walks over and "holds" the partner (it stops and
    /// waits). Teardown is <b>self-healing</b>, never cross-recursive: an agent only ever changes its OWN
    /// state, and a held partner releases itself once it notices the initiator is no longer engaging it.
    /// (An earlier version had the initiator call the partner's GoIdle, which could ping-pong into a
    /// stack overflow when two Sims ended up mutually held — this design makes that impossible.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimAgent : MonoBehaviour
    {
        [Tooltip("Metres per second while walking.")]
        public float walkSpeed = 1.2f;
        [Tooltip("Metres per second while hurrying (double-click, or an urgent need).")]
        public float runSpeed = 3.0f;
        [Tooltip("Set for this action to make the Sim run; cleared when the action finishes.")]
        public bool Hurry;

        public float arriveDistance = 0.4f;
        [Tooltip("How close to a path waypoint counts as reaching it.")]
        public float waypointTolerance = 0.18f;

        [Header("Avoidance")]
        [Tooltip("Moving Sims softly push away from other Sims within this distance.")]
        public float separationRadius = 0.6f;
        public float separationStrength = 0.8f;
        [Tooltip("Abandon a route after this many real seconds so a Sim can never stall forever.")]
        public float maxTravelSeconds = 30f;
        [Tooltip("Degrees/sec turn rate toward the target.")]
        public float turnSpeed = 540f;

        [Tooltip("When false the Sim is possessed: the AI won't auto-pick; only the player directs it.")]
        public bool AutonomyEnabled = true;

        [Header("Social")]
        public string socialNeedId = "social";
        public float socialDurationHours = 1f;
        public float socialPerHour = 45f;
        public float relationshipPerHour = 25f;

        private enum State { Idle, Going, Performing, GoingToSocial, Socializing, HeldSocial, MovingTo }
        private State _state = State.Idle;

        // object interaction
        private SmartObject _target;
        private InteractionAdvertisement _ad;
        private float _performRemainingHours;

        // routing — grid path following, with a straight line as the fallback when there's no LotGrid
        private readonly System.Collections.Generic.List<Vector3> _path = new System.Collections.Generic.List<Vector3>();
        private int _pathIndex;
        private int _gridVersion = -1;
        private Vector3 _goal;
        private float _travelSeconds;
        private SmartObject _reserved;
        private Vector3 _moveGoal;

        // social interaction
        private SimBody _socialOther;       // partner (when I'm the initiator)
        private SimAgent _socialOtherAgent;
        private SimBody _socialActor;        // initiator (when I'm the held partner)

        private SimBody _body;
        private GameClock _clock;
        private SimulationDirector _director;

        public bool IsIdle => _state == State.Idle;
        public string StatusLabel { get; private set; } = "idle";

        // ---- debug introspection (GridDebugOverlay) ----
        public System.Collections.Generic.IReadOnlyList<Vector3> DebugPath => _path;
        public int DebugPathIndex => _pathIndex;
        public Vector3 DebugGoal => _goal;
        /// <summary>True while this agent is travelling somewhere (has a route it is consuming).</summary>
        public bool DebugMoving => _state == State.Going || _state == State.MovingTo || _state == State.GoingToSocial;

        /// <summary>True while I am walking toward or chatting with <paramref name="b"/> as the initiator.</summary>
        public bool IsEngagingWith(SimBody b)
            => (_state == State.GoingToSocial || _state == State.Socializing) && _socialOther == b;

        /// <summary>True while I am a held partner of initiator <paramref name="actor"/>.</summary>
        public bool IsHeldBy(SimBody actor) => _state == State.HeldSocial && _socialActor == actor;

        private void Awake() => _body = GetComponent<SimBody>();

        private void Start()
        {
            _clock = FindFirstObjectByType<GameClock>();
            _director = FindFirstObjectByType<SimulationDirector>();
        }

        /// <summary>Walk to <paramref name="obj"/> and perform <paramref name="ad"/>.</summary>
        public void Begin(SmartObject obj, InteractionAdvertisement ad)
        {
            if (obj == null || ad == null) return;
            ReleaseReservation();
            if (!obj.TryReserve(_body)) { GoIdle(); return; } // another Sim claimed it first
            _reserved = obj;
            _target = obj;
            _ad = ad;
            _socialOther = null; _socialOtherAgent = null; _socialActor = null;
            _state = State.Going;
            _travelSeconds = 0f;
            RequestPath(obj.AnchorPosition);
            StatusLabel = "→ " + obj.displayName;
        }

        /// <summary>Just go stand at a world point — no interaction (player click-to-move).</summary>
        public void GoTo(Vector3 point)
        {
            // Sims stand IN cells (D-106): walking somewhere means walking to that tile's centre.
            var grid = LotGrid.Instance;
            if (grid != null) point = grid.TileCenter(grid.TileX(point.x), grid.TileZ(point.z));
            ReleaseReservation();
            _target = null; _ad = null;
            _socialOther = null; _socialOtherAgent = null; _socialActor = null;
            _moveGoal = point;
            _state = State.MovingTo;
            _travelSeconds = 0f;
            RequestPath(point);
            StatusLabel = Hurry ? "running there" : "walking there";
        }

        /// <summary>Walk to <paramref name="other"/> and socialize (two-party).</summary>
        public void BeginSocial(SimBody other)
        {
            if (other == null || _body == null || other == _body) return;
            var oa = other.GetComponent<SimAgent>();
            if (oa == null) return;
            ReleaseReservation(); // switching to a person frees whatever object I'd claimed
            _target = null; _ad = null; _socialActor = null;
            _socialOther = other;
            _socialOtherAgent = oa;
            _state = State.GoingToSocial;
            _travelSeconds = 0f;
            RequestPath(other.transform.position);
            StatusLabel = "→ " + Name(other);
            oa.HoldForSocial(_body); // reserve them so they wait and don't also initiate
        }

        /// <summary>Called on the partner by the initiator: stop and wait to be socialized with.</summary>
        public void HoldForSocial(SimBody actor)
        {
            // Becoming a held partner cancels any initiator role I had; my old partner will self-release.
            _target = null; _ad = null;
            _socialOther = null; _socialOtherAgent = null;
            _socialActor = actor;
            _state = State.HeldSocial;
            StatusLabel = "chatting";
        }

        private void Update()
        {
            if (_body == null || _body.Soul == null) return;
            float dh = _clock != null ? _clock.DeltaGameHours : 0f;

            switch (_state)
            {
                case State.Going:
                {
                    if (_target == null) { GoIdle(); break; }
                    _travelSeconds += Time.deltaTime;
                    if (_travelSeconds > maxTravelSeconds) { GoIdle(); break; } // unreachable → re-decide
                    if (StepAlongPath(_target.AnchorPosition, arriveDistance))
                    {
                        _state = State.Performing;
                        _performRemainingHours = _ad.durationHours;
                        StatusLabel = _ad.label;
                    }
                    break;
                }

                case State.MovingTo:
                {
                    _travelSeconds += Time.deltaTime;
                    if (_travelSeconds > maxTravelSeconds) { GoIdle(); break; }
                    if (StepAlongPath(_moveGoal, arriveDistance)) GoIdle();
                    break;
                }

                case State.Performing:
                {
                    if (_target != null) FaceToward(_target.transform.position); // use the object, face it
                    if (dh > 0f && _ad != null)
                    {
                        var need = _body.Soul.GetNeed(_ad.needId);
                        if (need != null) need.value = Mathf.Min(100f, need.value + _ad.satisfyPerHour * dh);
                        _performRemainingHours -= dh;
                    }
                    if (_performRemainingHours <= 0f) GoIdle();
                    break;
                }

                case State.GoingToSocial:
                {
                    // Bail if the partner got grabbed by someone else (or vanished) while I walked over.
                    if (_socialOther == null || _socialOtherAgent == null || !_socialOtherAgent.IsHeldBy(_body))
                    { GoIdle(); break; }

                    _travelSeconds += Time.deltaTime;
                    if (_travelSeconds > maxTravelSeconds) { GoIdle(); break; }
                    if (StepAlongPath(SocialStandPoint(), arriveDistance))
                    {
                        _state = State.Socializing;
                        _performRemainingHours = socialDurationHours;
                        StatusLabel = "chatting w/ " + Name(_socialOther);
                    }
                    break;
                }

                case State.Socializing:
                {
                    if (_socialOther == null || _socialOtherAgent == null || !_socialOtherAgent.IsHeldBy(_body))
                    { GoIdle(); break; }

                    FaceToward(_socialOther.transform.position);
                    if (dh > 0f)
                    {
                        BoostSocial(_body, dh);
                        BoostSocial(_socialOther, dh);
                        if (_director != null && _socialOther.Soul != null)
                            _director.Relationships.Add(_body.Soul.simId, _socialOther.Soul.simId, relationshipPerHour * dh);
                        _performRemainingHours -= dh;
                    }
                    if (_performRemainingHours <= 0f) GoIdle();
                    break;
                }

                case State.HeldSocial:
                {
                    // Self-release once my initiator is no longer walking to me or chatting with me.
                    var actorAgent = _socialActor != null ? _socialActor.GetComponent<SimAgent>() : null;
                    if (actorAgent == null || !actorAgent.IsEngagingWith(_body)) { GoIdle(); break; }
                    FaceToward(_socialActor.transform.position);
                    break;
                }
            }
        }

        /// <summary>Plan a route to <paramref name="goal"/>; falls back to a straight line with no grid.</summary>
        private void RequestPath(Vector3 goal)
        {
            _goal = goal;
            _pathIndex = 0;
            _path.Clear();
            var grid = LotGrid.Instance;
            if (grid != null && grid.TryPath(transform.position, goal, _path))
                _gridVersion = grid.Version;
            else
            {
                _path.Add(goal); // straight line
                _gridVersion = grid != null ? grid.Version : -1;
            }
        }

        /// <summary>
        /// Advance one frame along the planned route toward <paramref name="goal"/>; true on arrival.
        /// Re-plans when the lot changes (a build-mode edit bumps the grid version) or the goal moves.
        /// </summary>
        private bool StepAlongPath(Vector3 goal, float arriveDist)
        {
            var grid = LotGrid.Instance;
            if (_path.Count == 0) RequestPath(goal);
            else if (grid != null && grid.Version != _gridVersion) RequestPath(goal);
            else if ((goal - _goal).sqrMagnitude > 0.25f) RequestPath(goal);

            Vector3 flatGoal = goal; flatGoal.y = transform.position.y;
            if ((flatGoal - transform.position).sqrMagnitude <= arriveDist * arriveDist) return true;

            // At high game speeds one frame covers a lot of ground, so a fixed tolerance would be
            // overshot and the Sim would orbit its waypoint. Scale it with the actual step length.
            float step = (Hurry ? runSpeed : walkSpeed) * Time.deltaTime;
            float tolerance = Mathf.Max(waypointTolerance, step * 1.25f);

            while (_pathIndex < _path.Count)
            {
                Vector3 wp = _path[_pathIndex]; wp.y = transform.position.y;
                Vector3 d = wp - transform.position;
                float dist = d.magnitude;
                if (dist <= tolerance) { _pathIndex++; continue; }
                MoveToward(d, dist);
                return false;
            }

            // Route consumed — close the final stretch straight at the goal.
            Vector3 fd = flatGoal - transform.position;
            float fdist = fd.magnitude;
            if (fdist <= arriveDist) return true;
            MoveToward(fd, fdist);
            return false;
        }

        private void MoveToward(Vector3 delta, float d)
        {
            float speed = Hurry ? runSpeed : walkSpeed;
            Vector3 dir = delta / Mathf.Max(d, 1e-4f);

            // Soft avoidance: nudge away from nearby Sims so they slide past instead of overlapping.
            // Deliberately a steer, not a collision — hard blocking between Sims causes deadlocks.
            Vector3 move = dir + Separation() * separationStrength;
            if (move.sqrMagnitude > 1e-6f) move.Normalize(); else move = dir;

            transform.position += move * (speed * Time.deltaTime);

            var flat = new Vector3(move.x, 0f, move.z); // face where we actually travel
            if (flat.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
        }

        /// <summary>Push-away vector from other Sims within <see cref="separationRadius"/>.</summary>
        private Vector3 Separation()
        {
            if (_director == null || _body == null) return Vector3.zero;
            Vector3 push = Vector3.zero;
            var bodies = _director.Bodies;
            for (int i = 0; i < bodies.Count; i++)
            {
                var other = bodies[i];
                if (other == null || other == _body) continue;
                Vector3 d = transform.position - other.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist >= separationRadius || dist < 1e-4f) continue;
                push += (d / dist) * (1f - dist / separationRadius); // stronger the closer they are
            }
            return push;
        }

        /// <summary>
        /// Where I stand to talk (D-106): Sims interact from full TILES — the free tile beside the
        /// partner that is nearest to me, at its centre. Falls back to the partner's position when no
        /// grid or no free neighbour exists. (Intimate interactions — hugs, kisses — will later be
        /// allowed to break the tile rule; sitting/sleeping get furniture-slot mechanics instead.)
        /// </summary>
        private Vector3 SocialStandPoint()
        {
            Vector3 pp = _socialOther.transform.position;
            var grid = LotGrid.Instance;
            if (grid == null) return pp;
            int ptx = grid.TileX(pp.x), ptz = grid.TileZ(pp.z);
            Vector3 best = pp;
            float bestD = float.MaxValue;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int tx = ptx + dx, tz = ptz + dz;
                    if (!grid.TilesFree(tx, tz, 1, 1)) continue; // occupied or off-lot
                    var c = grid.TileCenter(tx, tz);
                    float d = (c - transform.position).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = c; }
                }
            return best;
        }

        private void ReleaseReservation()
        {
            if (_reserved != null) { _reserved.Release(_body); _reserved = null; }
        }

        private void FaceToward(Vector3 worldPos)
        {
            var flat = worldPos - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(flat), turnSpeed * Time.deltaTime);
        }

        private void BoostSocial(SimBody b, float dh)
        {
            var n = b != null && b.Soul != null ? b.Soul.GetNeed(socialNeedId) : null;
            if (n != null) n.value = Mathf.Min(100f, n.value + socialPerHour * dh);
        }

        private void GoIdle()
        {
            // Only ever touches MY own state — partners self-heal by observing my state next frame.
            _state = State.Idle;
            _target = null;
            _ad = null;
            _socialOther = null;
            _socialOtherAgent = null;
            _socialActor = null;
            _path.Clear();
            _pathIndex = 0;
            _travelSeconds = 0f;
            ReleaseReservation();
            Hurry = false; // urgency applies to one action only
            StatusLabel = "idle";
        }

        private static string Name(SimBody b) => b != null && b.Soul != null ? b.Soul.displayName : (b != null ? b.name : "?");
    }
}
