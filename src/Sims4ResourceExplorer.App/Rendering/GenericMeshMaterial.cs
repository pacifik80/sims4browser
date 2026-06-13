using HelixToolkit.SharpDX.Model;
using HelixToolkit.WinUI.SharpDX;

namespace Sims4ResourceExplorer.App.Rendering;

/// <summary>
/// A WinUI UI <see cref="Material"/> element that wraps an arbitrary
/// <see cref="GenericMeshMaterialCore"/>. HelixToolkit has no shipped generic UI material, and
/// a <see cref="MeshGeometryModel3D"/>'s <c>Material</c> dependency property pushes its value
/// (implicitly converted to <see cref="MaterialCore"/>) into the scene node on every change —
/// AND re-pushes it at attach time via <c>AssignDefaultValuesToSceneNode</c>. So setting the
/// node's <c>Material</c> core directly (with the UI <c>Material</c> left null) gets clobbered
/// to null when the model is added to the viewport. Routing the core through this UI element
/// makes <c>model.Material = new GenericMeshMaterial(core)</c> deliver the core every sync,
/// surviving attach. (Mirrors HelixToolkit's GenericMaterialDemo, adapted to the UI wrapper.)
/// </summary>
internal sealed class GenericMeshMaterial : Material
{
    private readonly GenericMeshMaterialCore core;

    public GenericMeshMaterial(GenericMeshMaterialCore core)
        : base(core)
    {
        this.core = core;
    }

    protected override MaterialCore OnCreateCore() => core;
}
