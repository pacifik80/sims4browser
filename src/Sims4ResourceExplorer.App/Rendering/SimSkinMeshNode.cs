using HelixToolkit.SharpDX;
using HelixToolkit.SharpDX.Model.Scene;

namespace Sims4ResourceExplorer.App.Rendering;

/// <summary>
/// A <see cref="MeshNode"/> that renders with the custom <see cref="SimSkinTechnique"/>
/// ("SimSkinComposite") technique, following HelixToolkit's own CustomShaderDemo pattern: the
/// node's <see cref="SceneNode.OnCreateRenderTechnique"/> returns the custom technique, so the
/// node renders ENTIRELY with it and <c>node.technique == material.pass.technique</c>.
///
/// This is the fix for the long black-viewport saga. Every prior attempt put a
/// <see cref="GenericMeshMaterialCore"/> whose pass came from the custom technique onto a STOCK
/// <c>MeshGeometryModel3D</c> node — but that node renders with the stock <c>RenderMesh</c>
/// technique, which (a) drove a depth prepass that black-out our color pass and (b) mismatched
/// the node's technique against the material's pass technique. Neither of HelixToolkit's two
/// working examples uses that hybrid: GenericMaterialDemo sources its pass from the node's OWN
/// technique, and CustomShaderDemo uses a custom node like this one. Add instances via
/// <c>SceneNodeGroupModel3D.AddNode(node)</c>; set <see cref="MeshNode.Material"/> directly to
/// the <see cref="MaterialCore"/> (no UI <c>Material</c> wrapper needed at the node level).
/// </summary>
internal sealed class SimSkinMeshNode : MeshNode
{
    protected override IRenderTechnique? OnCreateRenderTechnique(IEffectsManager effectsManager)
    {
        // Idempotent: the technique is normally already registered by the time the material core
        // was built, but register defensively so the node never resolves to a null technique.
        SimSkinTechnique.EnsureRegistered(effectsManager);
        return effectsManager.GetTechnique(SimSkinTechnique.TechniqueName);
    }
}
