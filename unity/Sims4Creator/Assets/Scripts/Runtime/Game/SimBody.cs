using UnityEngine;

namespace Sims4Creator.Game
{
    /// <summary>
    /// The presentation "body" of a Sim: the bridge between a built character GameObject and its runtime
    /// <see cref="SimSoul"/>. The authored fields (displayName, appearanceSlug) are set when the body is
    /// placed in the scene; <see cref="SimulationDirector"/> creates the soul at play start and binds it
    /// here. In M0 bodies are authored in the scene; pooled runtime spawning comes later.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SimBody : MonoBehaviour
    {
        public string displayName = "Sim";

        [Tooltip("Exported character folder this body was built from, e.g. am_char.")]
        public string appearanceSlug = "am_char";

        /// <summary>Runtime soul; null until the director binds it at play start.</summary>
        public SimSoul Soul { get; set; }

        /// <summary>The visual character on this body, if present.</summary>
        public Sims4Character Character => GetComponentInChildren<Sims4Character>();
    }
}
