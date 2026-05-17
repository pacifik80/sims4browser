using Sims4ResourceExplorer.Core;

namespace Sims4ResourceExplorer.Preview.SimRender;

/// <summary>
/// Pure-function service that turns a Sim <see cref="AssetGraph"/> into a single
/// composited <see cref="CanonicalScene"/> ready for viewport display.
///
/// Replaces the rendering-composition portion of
/// <c>MainViewModel.TryApplySimBodyProxyPreviewAsync</c> so both MainWindow and
/// SimConstructorWindow can produce a scene from the same code path. Per the
/// refactor plan (P0.4c packet R1) this service:
///
///   1. Walks <c>SimGraph.BodyCandidates</c> picking each layer's preferred option
///      (ExactPartLink first, then fallbacks).
///   2. Resolves each picked candidate to a real CASPart <c>ResourceMetadata</c>
///      via the index store, builds a CasAssetGraph through
///      <see cref="IAssetGraphBuilder"/>, and renders it via
///      <see cref="ISceneBuildService.BuildSceneAsync(CasAssetGraph, CancellationToken, IProgress{PreviewBuildProgress}?)"/>.
///   3. Resolves BOND / DMap / BGEO morphs via the three resolver services and
///      applies them to each layer's scene before composition.
///   4. Calls <see cref="SimSceneComposer.ComposeBodyAndHead"/> to combine the
///      body and head ScenePreviewContent into one composited scene with
///      skintone routing applied.
///
/// The implementation is the responsibility of refactor packet R1. The
/// interface and result record are defined here so the constructor view-model
/// (and a future MainWindow refactor in R4) can take a dependency on the
/// finished service today.
/// </summary>
public interface ISimAssetGraphRenderer
{
    Task<SimRenderResult> BuildSimSceneAsync(AssetGraph graph, CancellationToken cancellationToken);
}

/// <summary>
/// Output of <see cref="ISimAssetGraphRenderer.BuildSimSceneAsync"/>. When
/// <see cref="Scene"/> is non-null the caller can hand it to a viewport
/// renderer (R2 packet); <see cref="Layers"/> + <see cref="Diagnostics"/>
/// carry per-layer + overall context for the inspector UI.
/// </summary>
public sealed record SimRenderResult(
    CanonicalScene? Scene,
    IReadOnlyList<ScenePreviewContent> Layers,
    SimAssemblyPlanSummary? AssemblyPlan,
    SimAssemblyGraphSummary? AssemblyGraph,
    IReadOnlyList<string> Diagnostics);

public sealed class SimAssetGraphRenderer : ISimAssetGraphRenderer
{
    public Task<SimRenderResult> BuildSimSceneAsync(AssetGraph graph, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>
        {
            "SimAssetGraphRenderer is a stub; R1 implementation pending.",
            "See docs/planning/current-plan.md → 'Refactor packets — Sim scene rendering extraction' → R1."
        };
        return Task.FromResult(new SimRenderResult(
            Scene: null,
            Layers: Array.Empty<ScenePreviewContent>(),
            AssemblyPlan: null,
            AssemblyGraph: null,
            Diagnostics: diagnostics));
    }
}
