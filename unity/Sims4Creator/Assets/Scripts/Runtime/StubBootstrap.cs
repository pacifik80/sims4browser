using UnityEngine;

namespace Sims4Creator
{
    /// <summary>
    /// Stub bootstrap. Its only job is to prove the Unity runtime assembly
    /// (Sims4Creator.Runtime) compiles and runs. Drop it on an empty GameObject
    /// and press Play — you should see the log line in the Console.
    ///
    /// This gets replaced piece by piece as we decide what to import first
    /// (the plan is: reuse the existing .NET asset-parsing libraries, convert
    /// CanonicalScene/Mesh/Material/Texture into Unity Mesh / Texture2D /
    /// SkinnedMeshRenderer / blend shapes at the import boundary).
    /// </summary>
    public sealed class StubBootstrap : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("[Sims4Creator] Runtime stub alive — Unity 6 HDRP toolchain compiled and running.");
        }
    }
}
