using System.Collections.Generic;
using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// Owns the set of Sims (souls) on the active lot and runs their tick each frame, driven by the
    /// <see cref="GameClock"/>: needs decay for everyone, and every idle Sim consults the
    /// <see cref="UtilityBrain"/> to pick the best smart-object interaction and begins it via its
    /// <see cref="SimAgent"/>. Bodies are authored in the scene; this creates a soul per
    /// <see cref="SimBody"/> at play start and ensures each has an agent.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimulationDirector : MonoBehaviour
    {
        public GameClock clock;

        [Tooltip("The need set every Sim starts with.")]
        public List<NeedDefinition> needs = new List<NeedDefinition>();

        [Tooltip("A Sim only acts on a need once it's at least this many points below full.")]
        public float actWhenDeficitAbove = 15f;

        [Tooltip("Below this need value the Sim runs to the object instead of walking.")]
        public float hurryWhenNeedBelow = 15f;

        private readonly List<SimSoul> _souls = new List<SimSoul>();
        private readonly List<SimBody> _bodies = new List<SimBody>();
        private readonly List<SimAgent> _agents = new List<SimAgent>();
        private SmartObject[] _objects = System.Array.Empty<SmartObject>();
        private int _objectsGridVersion = -1; // LotGrid.Version at the last object scan
        private Dictionary<string, float> _decay;

        public IReadOnlyList<SimSoul> Souls => _souls;
        public IReadOnlyList<SimBody> Bodies => _bodies;
        public RelationshipBook Relationships { get; } = new RelationshipBook();

        private void Start()
        {
            if (clock == null) clock = FindFirstObjectByType<GameClock>();

            _decay = new Dictionary<string, float>();
            foreach (var nd in needs)
                if (nd != null && !_decay.ContainsKey(nd.id)) _decay[nd.id] = nd.decayPerHour;

            _bodies.Clear();
            _souls.Clear();
            _agents.Clear();
            foreach (var body in FindObjectsByType<SimBody>(FindObjectsSortMode.None))
            {
                var soul = new SimSoul
                {
                    simId = System.Guid.NewGuid().ToString("N").Substring(0, 8),
                    displayName = string.IsNullOrEmpty(body.displayName) ? body.name : body.displayName,
                    appearanceSlug = body.appearanceSlug,
                };
                foreach (var nd in needs)
                    if (nd != null) soul.needs.Add(new NeedValue(nd.id, nd.startValue));
                body.Soul = soul;

                var agent = body.GetComponent<SimAgent>();
                if (agent == null) agent = body.gameObject.AddComponent<SimAgent>();

                // Animation driven by actual movement speed (idle variety / walk / run). Configure the
                // clip set BEFORE its Start() resolves clips. Feminine set for the female look.
                var loco = body.GetComponent<SimLocomotion>();
                if (loco == null) loco = body.gameObject.AddComponent<SimLocomotion>();
                loco.ApplyDefaultSet(!string.IsNullOrEmpty(body.appearanceSlug) &&
                                     body.appearanceSlug.StartsWith("af", System.StringComparison.OrdinalIgnoreCase));

                // A collider so the player can click to possess (raycast selection).
                if (body.GetComponent<Collider>() == null)
                {
                    var cap = body.gameObject.AddComponent<CapsuleCollider>();
                    cap.height = 1.8f; cap.radius = 0.28f; cap.center = new Vector3(0f, 0.9f, 0f);
                }

                _bodies.Add(body);
                _souls.Add(soul);
                _agents.Add(agent);
            }

            _objects = FindObjectsByType<SmartObject>(FindObjectsSortMode.None);
        }

        private void Update()
        {
            if (clock == null) return;

            // Build-mode edits re-mark the nav grid (Version++). Re-scan on the same signal, so newly
            // bought furniture starts advertising to autonomy and deleted objects drop out.
            if (LotGrid.Instance != null && LotGrid.Instance.Version != _objectsGridVersion)
            {
                _objectsGridVersion = LotGrid.Instance.Version;
                _objects = FindObjectsByType<SmartObject>(FindObjectsSortMode.None);
            }

            float dh = clock.DeltaGameHours;
            if (dh <= 0f) return;

            for (int i = 0; i < _souls.Count; i++)
            {
                NeedsSystem.Decay(_souls[i], _decay, dh);

                var agent = _agents[i];
                if (agent == null || !agent.AutonomyEnabled || !agent.IsIdle) continue;

                Vector3 pos = agent.transform.position;
                bool hasObj = UtilityBrain.Pick(_souls[i], _objects, pos, actWhenDeficitAbove, _bodies[i], out var choice);
                float objScore = hasObj ? choice.score : float.NegativeInfinity;

                SimBody partner = FindSocialPartner(i, pos, out float socialScore);
                bool hasSocial = partner != null && socialScore > actWhenDeficitAbove;

                if (hasSocial && socialScore >= objScore)
                {
                    agent.Hurry = false;
                    agent.BeginSocial(partner);
                }
                else if (hasObj)
                {
                    // Desperate needs make the Sim run rather than stroll.
                    var need = _souls[i].GetNeed(choice.ad.needId);
                    agent.Hurry = need != null && need.value < hurryWhenNeedBelow;
                    agent.Begin(choice.obj, choice.ad);
                }
            }
        }

        // The best free partner for the "social" need: another Sim that is idle + autonomous (not busy,
        // not possessed). Score mirrors the object brain (deficit − distance) plus a small companionship
        // bonus, so Sims lean toward each other over the placeholder couch.
        private SimBody FindSocialPartner(int selfIndex, Vector3 fromPos, out float score)
        {
            score = float.NegativeInfinity;
            var social = _souls[selfIndex].GetNeed("social");
            if (social == null) return null;
            float deficit = 100f - social.value;
            if (deficit < actWhenDeficitAbove) return null;

            SimBody best = null;
            for (int j = 0; j < _bodies.Count; j++)
            {
                if (j == selfIndex) continue;
                var otherAgent = _agents[j];
                if (otherAgent == null || !otherAgent.IsIdle || !otherAgent.AutonomyEnabled) continue;
                float dist = Vector3.Distance(fromPos, _bodies[j].transform.position);
                float s = deficit + 10f - dist * 2f;
                if (s > score) { score = s; best = _bodies[j]; }
            }
            return best;
        }
    }
}
