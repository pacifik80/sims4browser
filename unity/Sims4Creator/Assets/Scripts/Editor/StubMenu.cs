using UnityEditor;
using UnityEngine;

namespace Sims4Creator.Editor
{
    /// <summary>
    /// Editor-side stub. Confirms the Editor assembly compiles and that custom
    /// menu integration works. This is where the Sims 4 ".package" importer and
    /// asset-browser windows will get wired in, one step at a time.
    /// Menu: "Sims4 Creator/About Stub".
    /// </summary>
    public static class StubMenu
    {
        [MenuItem("Sims4 Creator/About", priority = 201)]
        public static void About()
        {
            Debug.Log("[Sims4Creator] Editor stub alive. Next: we decide the first import slice " +
                      "(e.g. open a .package and convert one CanonicalMesh into a Unity Mesh).");
        }
    }
}
