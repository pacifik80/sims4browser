namespace Sims4ResourceExplorer.Core;

public sealed record SimAssembledSceneResult(
    ScenePreviewContent Preview,
    bool IncludesHeadShell,
    SimAssemblyPlanSummary Plan,
    SimAssemblyGraphSummary Graph);

public static class SimSceneComposer
{
    public static SimAssembledSceneResult ComposeBodyAndHead(
        string name,
        ScenePreviewContent bodyPreview,
        IReadOnlyList<ResourceMetadata>? bodyRigResources,
        ScenePreviewContent? headPreview,
        IReadOnlyList<ResourceMetadata>? headRigResources,
        SimInfoSummary? simMetadata = null,
        IReadOnlyList<SimMorphGroupSummary>? morphGroups = null,
        SimSkintoneRenderSummary? skintoneRender = null,
        IReadOnlyList<CasRegionMapSummary>? bodyRegionMaps = null,
        IReadOnlyList<CasRegionMapSummary>? headRegionMaps = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(bodyPreview);

        if (headPreview?.Scene is null)
        {
            headRigResources = [];
        }

        bodyRigResources ??= [];
        headRigResources ??= [];
        var rigCompatibility = EvaluateRigCompatibility(bodyRigResources, headRigResources);
        var execution = ExecuteAssemblyStages(
            name,
            bodyPreview,
            headPreview,
            rigCompatibility,
            simMetadata,
            morphGroups,
            skintoneRender,
            bodyRegionMaps,
            headRegionMaps);
        var graph = BuildGraph(
            execution.BodySceneResolved,
            execution.HeadSceneResolved,
            execution.Plan.BasisKind,
            execution.Plan.IncludesHeadShell,
            execution.Plan.Notes,
            execution.Payload,
            execution.PayloadAnchor,
            execution.PayloadBoneMaps,
            execution.PayloadMeshBatches,
            execution.PayloadNodes,
            execution.Application,
            execution.ApplicationPasses,
            execution.ApplicationTargets,
            execution.ApplicationPlans,
            execution.ApplicationTransforms,
            execution.ApplicationOutcomes,
            execution.Output,
            execution.Contributions,
            execution.Stages);
        return new SimAssembledSceneResult(
            execution.Preview,
            execution.Plan.IncludesHeadShell,
            execution.Plan,
            graph);
    }

    private static SimAssemblyStageExecutionResult ExecuteAssemblyStages(
        string name,
        ScenePreviewContent bodyPreview,
        ScenePreviewContent? headPreview,
        SimRigCompatibilityResult rigCompatibility,
        SimInfoSummary? simMetadata,
        IReadOnlyList<SimMorphGroupSummary>? morphGroups,
        SimSkintoneRenderSummary? skintoneRender,
        IReadOnlyList<CasRegionMapSummary>? bodyRegionMaps,
        IReadOnlyList<CasRegionMapSummary>? headRegionMaps)
    {
        var bodySceneResolved = bodyPreview.Scene is not null;
        var headSceneResolved = headPreview?.Scene is not null;
        var stages = new List<SimAssemblyStageSummary>
        {
            new(
                "Resolve body shell scene",
                0,
                bodySceneResolved ? SimAssemblyStageState.Resolved : SimAssemblyStageState.Unavailable,
                bodySceneResolved
                    ? "A renderable body shell scene is available for the current Sim assembly."
                    : "No renderable body shell scene was available for the current Sim assembly."),
            new(
                "Resolve head shell scene",
                1,
                headSceneResolved
                    ? SimAssemblyStageState.Resolved
                    : SimAssemblyStageState.Pending,
                headSceneResolved
                    ? "A renderable head shell scene is available for the current Sim assembly."
                    : "No renderable head shell scene is currently available for the current Sim assembly.")
        };

        if (!bodySceneResolved)
        {
            var diagnostics = JoinDiagnostics(
                bodyPreview.Diagnostics,
                headPreview?.Diagnostics,
                "Sim assembly requires a renderable body shell scene.");
            var preview = bodyPreview with
            {
                Diagnostics = diagnostics,
                Status = SceneBuildStatus.Unsupported
            };
            var missingBodyPlan = BuildPlan(
                SimAssemblyBasisKind.None,
                includesHeadShell: false,
                "No renderable body shell scene was available.");
            stages.Add(new SimAssemblyStageSummary("Resolve assembly basis", 2, SimAssemblyStageState.Unavailable, missingBodyPlan.Notes));
            stages.Add(new SimAssemblyStageSummary("Materialize torso/head payload seam", 3, SimAssemblyStageState.Unavailable, "No torso/head payload seam could be materialized because no body shell scene was available."));
            return new SimAssemblyStageExecutionResult(
                preview,
                missingBodyPlan,
                BuildUnavailablePayload(),
                BuildUnavailablePayloadAnchor(),
                [],
                [],
                BuildPayloadNodes(BuildUnavailablePayloadAnchor(), [], []),
                BuildUnavailableApplicationSummary(),
                BuildUnavailableApplicationPasses(),
                [],
                [],
                [],
                [],
                BuildOutputSummary(preview, missingBodyPlan.BasisKind, includesHeadShell: false),
                [],
                bodySceneResolved,
                headSceneResolved,
                stages);
        }

        if (!headSceneResolved)
        {
            var bodyOnlyPlan = BuildPlan(
                SimAssemblyBasisKind.BodyOnly,
                includesHeadShell: false,
                "Only the body shell produced a renderable scene, so the current Sim preview remains body-only.");
            var payloadAnchor = BuildPayloadAnchor(bodyPreview);
            var payloadMeshBatches = BuildAnchorOnlyMeshBatches(bodyPreview);
            var bodyOnlyPayloadData = BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene!);
            var bodyOnlyApplicationData = BuildApplicationData(bodyOnlyPayloadData, simMetadata, skintoneRender, morphGroups, bodyRegionMaps, []);
            stages.Add(new SimAssemblyStageSummary("Resolve assembly basis", 2, SimAssemblyStageState.Pending, bodyOnlyPlan.Notes));
            stages.Add(new SimAssemblyStageSummary("Materialize torso/head payload seam", 3, SimAssemblyStageState.Resolved, "An anchor-only torso/head payload seam was materialized because no head shell scene was resolved."));
            var preview = ApplyApplicationDataToPreview(
                RenameBodyPreview(
                    name,
                    bodyPreview,
                    JoinDiagnostics(bodyPreview.Diagnostics, headPreview?.Diagnostics),
                    bodyPreview.Status),
                bodyOnlyApplicationData);
            return new SimAssemblyStageExecutionResult(
                preview,
                bodyOnlyPlan,
                BuildAnchorOnlyPayload(bodyPreview),
                payloadAnchor,
                [],
                payloadMeshBatches,
                BuildPayloadNodes(payloadAnchor, [], payloadMeshBatches),
                BuildApplicationSummary(bodyOnlyApplicationData),
                BuildApplicationPasses(bodyOnlyApplicationData),
                BuildApplicationTargets(bodyOnlyApplicationData),
                BuildApplicationPlans(bodyOnlyApplicationData),
                BuildApplicationTransforms(bodyOnlyApplicationData),
                BuildApplicationOutcomes(bodyOnlyApplicationData),
                BuildOutputSummary(preview, bodyOnlyPlan.BasisKind, includesHeadShell: false, bodyOnlyPayloadData),
                BuildAnchorOnlyContributions(bodyPreview),
                bodySceneResolved,
                headSceneResolved,
                stages);
        }

        var resolvedHeadPreview = headPreview!;
        var resolvedBodyScene = bodyPreview.Scene!;
        var bodyBoneNames = resolvedBodyScene.Bones
            .Select(static bone => bone.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var headBoneNames = resolvedHeadPreview.Scene!.Bones
            .Select(static bone => bone.Name)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (rigCompatibility.IsDefinitiveMismatch)
        {
            var diagnostics = JoinDiagnostics(
                bodyPreview.Diagnostics,
                resolvedHeadPreview.Diagnostics,
                rigCompatibility.Diagnostic);
            var mismatchPlan = BuildPlan(
                SimAssemblyBasisKind.BodyOnly,
                includesHeadShell: false,
                rigCompatibility.Diagnostic);
            var payloadAnchor = BuildPayloadAnchor(bodyPreview);
            var payloadMeshBatches = BuildAnchorOnlyMeshBatches(bodyPreview);
            var bodyOnlyPayloadData = BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene!);
            var mismatchApplicationData = BuildApplicationData(bodyOnlyPayloadData, simMetadata, skintoneRender, morphGroups, bodyRegionMaps, []);
            stages.Add(new SimAssemblyStageSummary("Resolve assembly basis", 2, SimAssemblyStageState.Pending, mismatchPlan.Notes));
            stages.Add(new SimAssemblyStageSummary("Materialize torso/head payload seam", 3, SimAssemblyStageState.Resolved, "An anchor-only torso/head payload seam was materialized because the body/head rig basis does not match."));
            var preview = ApplyApplicationDataToPreview(
                RenameBodyPreview(
                    name,
                    bodyPreview,
                    diagnostics,
                    SceneBuildStatus.Partial),
                mismatchApplicationData);
            return new SimAssemblyStageExecutionResult(
                preview,
                mismatchPlan,
                BuildAnchorOnlyPayload(bodyPreview),
                payloadAnchor,
                [],
                payloadMeshBatches,
                BuildPayloadNodes(payloadAnchor, [], payloadMeshBatches),
                BuildApplicationSummary(mismatchApplicationData),
                BuildApplicationPasses(mismatchApplicationData),
                BuildApplicationTargets(mismatchApplicationData),
                BuildApplicationPlans(mismatchApplicationData),
                BuildApplicationTransforms(mismatchApplicationData),
                BuildApplicationOutcomes(mismatchApplicationData),
                BuildOutputSummary(preview, mismatchPlan.BasisKind, includesHeadShell: false, bodyOnlyPayloadData),
                BuildAnchorOnlyContributions(bodyPreview),
                bodySceneResolved,
                headSceneResolved,
                stages);
        }

        if (bodyBoneNames.Count == 0 || headBoneNames.Length == 0)
        {
            var diagnostics = JoinDiagnostics(
                bodyPreview.Diagnostics,
                resolvedHeadPreview.Diagnostics,
                "Head shell was resolved but not assembled because canonical bone coverage is missing for the body or head scene.");
            var missingCoveragePlan = BuildPlan(
                SimAssemblyBasisKind.BodyOnly,
                includesHeadShell: false,
                "Head shell was withheld because canonical bone coverage is missing for the body or head scene.");
            var payloadAnchor = BuildPayloadAnchor(bodyPreview);
            var payloadMeshBatches = BuildAnchorOnlyMeshBatches(bodyPreview);
            var bodyOnlyPayloadData = BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene!);
            var missingCoverageApplicationData = BuildApplicationData(bodyOnlyPayloadData, simMetadata, skintoneRender, morphGroups, bodyRegionMaps, []);
            stages.Add(new SimAssemblyStageSummary("Resolve assembly basis", 2, SimAssemblyStageState.Pending, missingCoveragePlan.Notes));
            stages.Add(new SimAssemblyStageSummary("Materialize torso/head payload seam", 3, SimAssemblyStageState.Resolved, "An anchor-only torso/head payload seam was materialized because canonical bone coverage is incomplete."));
            var preview = ApplyApplicationDataToPreview(
                RenameBodyPreview(
                    name,
                    bodyPreview,
                    diagnostics,
                    SceneBuildStatus.Partial),
                missingCoverageApplicationData);
            return new SimAssemblyStageExecutionResult(
                preview,
                missingCoveragePlan,
                BuildAnchorOnlyPayload(bodyPreview),
                payloadAnchor,
                [],
                payloadMeshBatches,
                BuildPayloadNodes(payloadAnchor, [], payloadMeshBatches),
                BuildApplicationSummary(missingCoverageApplicationData),
                BuildApplicationPasses(missingCoverageApplicationData),
                BuildApplicationTargets(missingCoverageApplicationData),
                BuildApplicationPlans(missingCoverageApplicationData),
                BuildApplicationTransforms(missingCoverageApplicationData),
                BuildApplicationOutcomes(missingCoverageApplicationData),
                BuildOutputSummary(preview, missingCoveragePlan.BasisKind, includesHeadShell: false, bodyOnlyPayloadData),
                BuildAnchorOnlyContributions(bodyPreview),
                bodySceneResolved,
                headSceneResolved,
                stages);
        }

        var sharedBoneCount = headBoneNames.Count(bodyBoneNames.Contains);
        if (!rigCompatibility.HasAuthoritativeRigMatch && sharedBoneCount == 0)
        {
            var diagnostics = JoinDiagnostics(
                bodyPreview.Diagnostics,
                resolvedHeadPreview.Diagnostics,
                rigCompatibility.Diagnostic,
                "Head shell was resolved but not assembled because body/head scenes do not share any canonical bone names.");
            var noOverlapPlan = BuildPlan(
                SimAssemblyBasisKind.BodyOnly,
                includesHeadShell: false,
                "Head shell was withheld because neither shared rig compatibility nor canonical bone overlap could be confirmed.");
            var payloadAnchor = BuildPayloadAnchor(bodyPreview);
            var payloadMeshBatches = BuildAnchorOnlyMeshBatches(bodyPreview);
            var bodyOnlyPayloadData = BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene!);
            var noOverlapApplicationData = BuildApplicationData(bodyOnlyPayloadData, simMetadata, skintoneRender, morphGroups, bodyRegionMaps, []);
            stages.Add(new SimAssemblyStageSummary("Resolve assembly basis", 2, SimAssemblyStageState.Pending, noOverlapPlan.Notes));
            stages.Add(new SimAssemblyStageSummary("Materialize torso/head payload seam", 3, SimAssemblyStageState.Resolved, "An anchor-only torso/head payload seam was materialized because no shared body/head basis could be confirmed."));
            var preview = ApplyApplicationDataToPreview(
                RenameBodyPreview(
                    name,
                    bodyPreview,
                    diagnostics,
                    SceneBuildStatus.Partial),
                noOverlapApplicationData);
            return new SimAssemblyStageExecutionResult(
                preview,
                noOverlapPlan,
                BuildAnchorOnlyPayload(bodyPreview),
                payloadAnchor,
                [],
                payloadMeshBatches,
                BuildPayloadNodes(payloadAnchor, [], payloadMeshBatches),
                BuildApplicationSummary(noOverlapApplicationData),
                BuildApplicationPasses(noOverlapApplicationData),
                BuildApplicationTargets(noOverlapApplicationData),
                BuildApplicationPlans(noOverlapApplicationData),
                BuildApplicationTransforms(noOverlapApplicationData),
                BuildApplicationOutcomes(noOverlapApplicationData),
                BuildOutputSummary(preview, noOverlapPlan.BasisKind, includesHeadShell: false, bodyOnlyPayloadData),
                BuildAnchorOnlyContributions(bodyPreview),
                bodySceneResolved,
                headSceneResolved,
                stages);
        }

        var acceptedSceneInputs = new[] { bodyPreview, resolvedHeadPreview };
        var composed = ComposeAssemblyOutput(name, acceptedSceneInputs);
        var assembledPlan = BuildPlan(
            rigCompatibility.BasisKind,
            includesHeadShell: true,
            rigCompatibility.HasAuthoritativeRigMatch
                ? rigCompatibility.Diagnostic
                : "Body/head assembly is currently using canonical-bone fallback because shared rig metadata is incomplete.");
        var assembledApplicationData = BuildApplicationData(composed.PayloadData, simMetadata, skintoneRender, morphGroups, bodyRegionMaps, headRegionMaps);
        stages.Add(
            new SimAssemblyStageSummary(
                "Resolve assembly basis",
                2,
                rigCompatibility.HasAuthoritativeRigMatch ? SimAssemblyStageState.Resolved : SimAssemblyStageState.Approximate,
                assembledPlan.Notes));
        var assemblyDiagnostics = JoinDiagnostics(
            composed.Preview.Diagnostics,
            rigCompatibility.Diagnostic,
            rigCompatibility.HasAuthoritativeRigMatch
                ? sharedBoneCount > 0
                    ? $"Assembled Sim body/head scene with shared rig compatibility and {sharedBoneCount:N0} shared canonical bone(s)."
                    : "Assembled Sim body/head scene from a shared rig resource even though canonical bone overlap could not be confirmed from the preview scenes."
                : $"Assembled Sim body/head scene using {sharedBoneCount:N0} shared canonical bone(s) across {bodyBoneNames.Count:N0} body bone(s) and {headBoneNames.Length:N0} head bone(s).");
        stages.Add(
            new SimAssemblyStageSummary(
                "Materialize torso/head payload seam",
                3,
                SimAssemblyStageState.Resolved,
                "A rig-centered torso/head payload seam was materialized from the accepted body/head inputs."));
        var appliedPreview = ApplyApplicationDataToPreview(
            composed.Preview with { Diagnostics = assemblyDiagnostics },
            assembledApplicationData);
        return new SimAssemblyStageExecutionResult(
            appliedPreview,
            assembledPlan,
            composed.Payload,
            composed.PayloadAnchor,
            composed.PayloadBoneMaps,
            composed.PayloadMeshBatches,
            composed.PayloadNodes,
            BuildApplicationSummary(assembledApplicationData),
            BuildApplicationPasses(assembledApplicationData),
            BuildApplicationTargets(assembledApplicationData),
            BuildApplicationPlans(assembledApplicationData),
            BuildApplicationTransforms(assembledApplicationData),
            BuildApplicationOutcomes(assembledApplicationData),
            BuildOutputSummary(appliedPreview, assembledPlan.BasisKind, includesHeadShell: true, composed.PayloadData),
            composed.Contributions,
            bodySceneResolved,
            headSceneResolved,
            stages);
    }

    private static SimRigCompatibilityResult EvaluateRigCompatibility(
        IReadOnlyList<ResourceMetadata> bodyRigResources,
        IReadOnlyList<ResourceMetadata> headRigResources)
    {
        if (bodyRigResources.Count == 0 || headRigResources.Count == 0)
        {
            return new SimRigCompatibilityResult(
                BasisKind: SimAssemblyBasisKind.CanonicalBoneFallback,
                HasAuthoritativeRigMatch: false,
                IsDefinitiveMismatch: false,
                bodyRigResources.Count == 0 && headRigResources.Count == 0
                    ? "Body/head assembly is currently using canonical-bone fallback because neither side resolved an exact rig resource."
                    : "Body/head assembly is currently using canonical-bone fallback because only one side resolved an exact rig resource.");
        }

        var sharedByTgi = bodyRigResources
            .Select(static resource => resource.Key.FullTgi)
            .Intersect(headRigResources.Select(static resource => resource.Key.FullTgi), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sharedByTgi.Length > 0)
        {
            return new SimRigCompatibilityResult(
                BasisKind: SimAssemblyBasisKind.SharedRigResource,
                HasAuthoritativeRigMatch: true,
                IsDefinitiveMismatch: false,
                $"Body/head assembly matched {sharedByTgi.Length:N0} exact rig resource(s): {string.Join(", ", sharedByTgi.Take(3))}.");
        }

        var sharedByInstance = bodyRigResources
            .Select(static resource => resource.Key.FullInstance)
            .Intersect(headRigResources.Select(static resource => resource.Key.FullInstance))
            .ToArray();
        if (sharedByInstance.Length > 0)
        {
            return new SimRigCompatibilityResult(
                BasisKind: SimAssemblyBasisKind.SharedRigInstance,
                HasAuthoritativeRigMatch: true,
                IsDefinitiveMismatch: false,
                $"Body/head assembly matched {sharedByInstance.Length:N0} rig instance id(s): {string.Join(", ", sharedByInstance.Take(3).Select(static value => value.ToString("X16")))}.");
        }

        return new SimRigCompatibilityResult(
            BasisKind: SimAssemblyBasisKind.BodyOnly,
            HasAuthoritativeRigMatch: false,
            IsDefinitiveMismatch: true,
            "Head shell was resolved but not assembled because body/head CAS graphs do not share an exact rig resource or rig instance id.");
    }

    private static SimAssemblyPlanSummary BuildPlan(
        SimAssemblyBasisKind basisKind,
        bool includesHeadShell,
        string notes) =>
        new(
            basisKind,
            includesHeadShell,
            basisKind switch
            {
                SimAssemblyBasisKind.SharedRigResource => "Shared exact rig resource",
                SimAssemblyBasisKind.SharedRigInstance => "Shared rig instance id",
                SimAssemblyBasisKind.CanonicalBoneFallback => "Canonical-bone fallback",
                SimAssemblyBasisKind.BodyOnly => "Body-only assembly",
                _ => "No assembly basis"
            },
            notes);

    private static SimAssemblyGraphSummary BuildGraph(
        bool bodySceneResolved,
        bool headSceneResolved,
        SimAssemblyBasisKind basisKind,
        bool includesHeadShell,
        string notes,
        SimAssemblyPayloadSummary payload,
        SimAssemblyAnchorSummary payloadAnchor,
        IReadOnlyList<SimAssemblyBoneMapSummary> payloadBoneMaps,
        IReadOnlyList<SimAssemblyMeshBatchSummary> payloadMeshBatches,
        IReadOnlyList<SimAssemblyPayloadNodeSummary> payloadNodes,
        SimAssemblyApplicationSummary application,
        IReadOnlyList<SimAssemblyApplicationPassSummary> applicationPasses,
        IReadOnlyList<SimAssemblyApplicationTargetSummary> applicationTargets,
        IReadOnlyList<SimAssemblyApplicationPlanSummary> applicationPlans,
        IReadOnlyList<SimAssemblyApplicationTransformSummary> applicationTransforms,
        IReadOnlyList<SimAssemblyApplicationOutcomeSummary> applicationOutcomes,
        SimAssemblyOutputSummary output,
        IReadOnlyList<SimAssemblyContributionSummary> contributions,
        IReadOnlyList<SimAssemblyStageSummary> stages)
    {
        var inputs = BuildInputs(bodySceneResolved, headSceneResolved, basisKind, includesHeadShell, notes);
        var nodes = new[]
        {
            new SimBodyGraphNodeSummary(
                "Body shell scene",
                0,
                bodySceneResolved ? SimBodyGraphNodeState.Resolved : SimBodyGraphNodeState.Unavailable,
                bodySceneResolved
                    ? "A renderable body shell scene was resolved from the selected authoritative body candidate."
                    : "No renderable body shell scene was resolved."),
            new SimBodyGraphNodeSummary(
                "Head shell scene",
                1,
                includesHeadShell
                    ? SimBodyGraphNodeState.Resolved
                    : headSceneResolved
                        ? SimBodyGraphNodeState.Approximate
                        : SimBodyGraphNodeState.Pending,
                includesHeadShell
                    ? "A renderable head shell scene was resolved and accepted into the current Sim assembly."
                    : headSceneResolved
                        ? "A head shell scene exists, but it was withheld from the current Sim assembly."
                        : "No renderable head shell scene is currently available."),
            new SimBodyGraphNodeSummary(
                "Assembly basis",
                2,
                basisKind switch
                {
                    SimAssemblyBasisKind.SharedRigResource or SimAssemblyBasisKind.SharedRigInstance => SimBodyGraphNodeState.Resolved,
                    SimAssemblyBasisKind.CanonicalBoneFallback => SimBodyGraphNodeState.Approximate,
                    SimAssemblyBasisKind.BodyOnly => SimBodyGraphNodeState.Pending,
                    _ => SimBodyGraphNodeState.Unavailable
                },
                notes),
            new SimBodyGraphNodeSummary(
                "Torso/head payload seam",
                3,
                bodySceneResolved
                    ? SimBodyGraphNodeState.Resolved
                    : SimBodyGraphNodeState.Unavailable,
                includesHeadShell
                    ? "A rig-centered torso/head payload seam was materialized from both accepted shell inputs."
                    : bodySceneResolved
                        ? "An anchor-only torso/head payload seam was materialized from the accepted body shell input."
                        : "No torso/head payload seam is available because no renderable body shell was resolved.")
        };

        return new SimAssemblyGraphSummary(
            BuildPlan(basisKind, includesHeadShell, notes),
            inputs,
            stages,
            payload,
            payloadAnchor,
            payloadBoneMaps,
            payloadMeshBatches,
            payloadNodes,
            application,
            applicationPasses,
            applicationTargets,
            applicationPlans,
            applicationTransforms,
            applicationOutcomes,
            output,
            contributions,
            nodes);
    }

    private static SimAssemblyOutputComputationResult ComposeAssemblyOutput(
        string name,
        IReadOnlyList<ScenePreviewContent> acceptedSceneInputs)
    {
        ArgumentNullException.ThrowIfNull(acceptedSceneInputs);

        var renderableInputs = acceptedSceneInputs
            .Where(static input => input.Scene is not null)
            .ToArray();
        if (renderableInputs.Length == 0)
        {
            throw new ArgumentException("At least one renderable scene input is required.", nameof(acceptedSceneInputs));
        }

        var bodyInput = renderableInputs[0];
        var bodyScene = bodyInput.Scene!;
        if (renderableInputs.Length == 1)
        {
            var renamedScene = string.Equals(bodyScene.Name, name, StringComparison.Ordinal)
                ? bodyScene
                : bodyScene with { Name = name };
            var anchorOnlyPayloadData = BuildAnchorOnlyPayloadData(bodyInput, renamedScene);
            return new SimAssemblyOutputComputationResult(
                bodyInput with
                {
                    Scene = renamedScene,
                    Diagnostics = JoinDiagnostics(
                        bodyInput.Diagnostics,
                        "Torso/head payload seam is currently anchored to the accepted body shell scene.")
                },
                BuildPayloadSummary(anchorOnlyPayloadData, "The current torso/head payload contains only the accepted body shell anchor and is ready for later rig-native augmentation."),
                BuildPayloadAnchor(anchorOnlyPayloadData),
                BuildPayloadBoneMaps(anchorOnlyPayloadData),
                BuildPayloadMeshBatches(anchorOnlyPayloadData),
                BuildPayloadNodes(anchorOnlyPayloadData),
                anchorOnlyPayloadData,
                BuildAnchorOnlyContributions(bodyInput));
        }

        var contributions = new List<SimAssemblyContributionSummary>
        {
            CreateAnchorContribution(bodyInput)
        };
        var payloadData = BuildMergedPayloadData(renderableInputs);
        contributions.AddRange(
            payloadData.BoneRemaps.Select(remap =>
                CreateMergedContribution(remap.SourceInput, remap.RebasedWeightCount, remap.AddedBoneCount)));

        var composedScene = new CanonicalScene(
            name,
            payloadData.MergedMeshes,
            payloadData.MergedMaterials,
            payloadData.MergedBones,
            ComputeBounds(payloadData.MergedMeshes));
        var diagnostics = JoinDiagnostics(
            [
                .. renderableInputs.Select(static input => input.Diagnostics),
                $"Torso/head payload seam merged {renderableInputs.Length:N0} accepted scene input(s) using the body shell scene as the skeletal anchor."
            ]);
        var status = DetermineAggregateStatus(renderableInputs);
        return new SimAssemblyOutputComputationResult(
            new ScenePreviewContent(bodyInput.Resource, composedScene, diagnostics, status),
            BuildPayloadSummary(payloadData, "The current torso/head payload is anchored to the body shell basis and tracks rebased head-shell contributions before downstream modifier consumption."),
            BuildPayloadAnchor(payloadData),
            BuildPayloadBoneMaps(payloadData),
            BuildPayloadMeshBatches(payloadData),
            BuildPayloadNodes(payloadData),
            payloadData,
            contributions);
    }

    private static SimAssemblyPayloadSummary BuildUnavailablePayload() =>
        new(
            "Current assembly payload",
            "Unavailable",
            0,
            0,
            0,
            0,
            0,
            0,
            "No rig-centered assembly payload is currently available because no renderable body shell scene was resolved.");

    private static SimAssemblyAnchorSummary BuildUnavailablePayloadAnchor() =>
        new(
            "Current payload anchor",
            "Unavailable",
            "(unavailable)",
            string.Empty,
            string.Empty,
            0,
            [],
            "No assembly anchor is currently available because no renderable body shell scene was resolved.");

    private static SimAssemblyPayloadSummary BuildAnchorOnlyPayload(ScenePreviewContent bodyPreview) =>
        bodyPreview.Scene is null
            ? BuildUnavailablePayload()
            : BuildPayloadSummary(
                BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene),
                "The current assembly payload contains only the accepted body shell anchor and is ready for later rig-native augmentation.");

    private static SimAssemblyPayloadSummary BuildPayloadSummary(
        SimAssemblyPayloadData payloadData,
        string notes) =>
        new(
            "Current assembly payload",
            payloadData.AcceptedContributionCount > 1 ? "Multi-part rig payload" : payloadData.AcceptedContributionCount == 1 ? "Anchor-only rig payload" : "Unavailable",
            payloadData.Anchor.Bones.Count,
            payloadData.AcceptedContributionCount,
            payloadData.MergedMeshes.Count,
            payloadData.TotalRebasedWeightCount,
            payloadData.TotalMappedBoneReferenceCount,
            payloadData.TotalAddedBoneCount,
            notes);

    private static SimAssemblyAnchorSummary BuildPayloadAnchor(ScenePreviewContent bodyPreview) =>
        bodyPreview.Scene is null
            ? BuildUnavailablePayloadAnchor()
            : BuildPayloadAnchor(BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene));

    private static SimAssemblyAnchorSummary BuildPayloadAnchor(SimAssemblyPayloadData payloadData) =>
        new(
            "Current payload anchor",
            "Body shell anchor",
            GetSourceLabel(payloadData.Anchor.SourceInput),
            GetSourceResourceTgi(payloadData.Anchor.SourceInput),
            GetSourcePackagePath(payloadData.Anchor.SourceInput),
            payloadData.Anchor.Bones.Count,
            payloadData.Anchor.Bones
                .Select(static bone => bone.Name)
                .Where(static name => !string.IsNullOrWhiteSpace(name))
                .Take(8)
                .ToArray(),
            "The accepted body shell scene currently defines the skeletal anchor for rig-centered Sim assembly.");

    private static IReadOnlyList<SimAssemblyMeshBatchSummary> BuildAnchorOnlyMeshBatches(ScenePreviewContent bodyPreview) =>
        bodyPreview.Scene is null
            ? []
            : BuildPayloadMeshBatches(BuildAnchorOnlyPayloadData(bodyPreview, bodyPreview.Scene));

    private static IReadOnlyList<SimAssemblyBoneMapSummary> BuildPayloadBoneMaps(SimAssemblyPayloadData payloadData) =>
        payloadData.BoneRemaps
            .Select(remap => CreateBoneMapSummary(
                remap.SourceInput,
                remap.SourceBoneCount,
                remap.ReusedBoneReferenceCount,
                remap.AddedBoneCount,
                remap.RebasedWeightCount,
                BuildBoneMapEntries(remap)))
            .ToArray();

    private static IReadOnlyList<SimAssemblyMeshBatchSummary> BuildPayloadMeshBatches(SimAssemblyPayloadData payloadData) =>
        payloadData.MeshBatches
            .Select(batch => CreateMeshBatchSummary(
                batch.Label,
                batch.SourceLabel,
                batch.SourceInput,
                batch.MeshStartIndex,
                batch.MeshCount,
                batch.MaterialStartIndex,
                batch.MaterialCount,
                batch.Notes))
            .ToArray();

    private static IReadOnlyList<SimAssemblyPayloadNodeSummary> BuildPayloadNodes(
        SimAssemblyAnchorSummary anchor,
        IReadOnlyList<SimAssemblyBoneMapSummary> boneMaps,
        IReadOnlyList<SimAssemblyMeshBatchSummary> meshBatches)
    {
        var nodes = new List<SimAssemblyPayloadNodeSummary>
        {
            new(
                anchor.Label,
                0,
                SimAssemblyPayloadNodeKind.AnchorSkeleton,
                anchor.StatusLabel,
                $"{anchor.BoneCount:N0} bone(s)",
                anchor.Notes)
        };

        var order = 1;
        nodes.AddRange(
            boneMaps.Select(boneMap => new SimAssemblyPayloadNodeSummary(
                boneMap.Label,
                order++,
                SimAssemblyPayloadNodeKind.BoneRemapTable,
                "Resolved",
                $"source {boneMap.SourceBoneCount:N0}, reused {boneMap.ReusedBoneReferenceCount:N0}, added {boneMap.AddedBoneCount:N0}, rebased weights {boneMap.RebasedWeightCount:N0}",
                boneMap.Notes)));
        nodes.AddRange(
            meshBatches.Select(meshBatch => new SimAssemblyPayloadNodeSummary(
                meshBatch.Label,
                order++,
                SimAssemblyPayloadNodeKind.MeshSet,
                "Resolved",
                $"meshes {meshBatch.MeshStartIndex:N0}..{(meshBatch.MeshStartIndex + Math.Max(0, meshBatch.MeshCount - 1)):N0}, materials {meshBatch.MaterialStartIndex:N0}..{(meshBatch.MaterialStartIndex + Math.Max(0, meshBatch.MaterialCount - 1)):N0}",
                meshBatch.Notes)));
        return nodes;
    }

    private static IReadOnlyList<SimAssemblyPayloadNodeSummary> BuildPayloadNodes(SimAssemblyPayloadData payloadData)
    {
        var anchor = BuildPayloadAnchor(payloadData);
        var boneMaps = BuildPayloadBoneMaps(payloadData);
        var meshBatches = BuildPayloadMeshBatches(payloadData);
        return BuildPayloadNodes(anchor, boneMaps, meshBatches);
    }

    private static SimAssemblyApplicationSummary BuildUnavailableApplicationSummary() =>
        new(
            "Current application passes",
            "Unavailable",
            0,
            0,
            2,
            "No modifier-aware application passes are available because no rig-centered payload was resolved.");

    private static IReadOnlyList<SimAssemblyApplicationPassSummary> BuildUnavailableApplicationPasses() =>
        [
            new(
                "Skintone application",
                0,
                SimAssemblyApplicationPassState.Unavailable,
                "Unavailable",
                0,
                "No skintone application can be prepared because no rig-centered payload was resolved."),
            new(
                "Morph application",
                1,
                SimAssemblyApplicationPassState.Unavailable,
                "Unavailable",
                0,
                "No morph application can be prepared because no rig-centered payload was resolved.")
        ];

    private static SimAssemblyApplicationData BuildApplicationData(
        SimAssemblyPayloadData payloadData,
        SimInfoSummary? simMetadata,
        SimSkintoneRenderSummary? skintoneRender,
        IReadOnlyList<SimMorphGroupSummary>? morphGroups,
        IReadOnlyList<CasRegionMapSummary>? bodyRegionMaps,
        IReadOnlyList<CasRegionMapSummary>? headRegionMaps)
    {
        var preparedPasses = new List<SimAssemblyApplicationPassData>();
        var pendingPasses = new List<SimAssemblyApplicationPassData>();
        var targets = new List<SimAssemblyApplicationTargetData>();
        var plans = new List<SimAssemblyApplicationPlanData>();
        var transforms = new List<SimAssemblyApplicationTransformData>();
        var outcomes = new List<SimAssemblyApplicationOutcomeData>();
        IReadOnlyList<SimAssemblySkintoneMaterialRouteData> skintoneRouteData = [];
        IReadOnlyList<SimAssemblyMorphTransformOperationData> morphTransformOperations = [];

        var hasSkintone = skintoneRender is not null ||
                          (simMetadata is not null &&
                           (!string.IsNullOrWhiteSpace(simMetadata.SkintoneInstanceHex) || simMetadata.SkintoneShift.HasValue));
        if (hasSkintone)
        {
            skintoneRouteData = BuildSkintoneMaterialRoutes(payloadData, simMetadata, skintoneRender, bodyRegionMaps, headRegionMaps);
            var notes = skintoneRender?.Notes
                ?? (simMetadata!.SkintoneShift.HasValue
                    ? $"Skintone metadata is available for the current payload: instance {simMetadata.SkintoneInstanceHex ?? "(unknown)"} | shift {simMetadata.SkintoneShift.Value:0.###}."
                    : $"Skintone metadata is available for the current payload: instance {simMetadata!.SkintoneInstanceHex}.");
            preparedPasses.Add(new SimAssemblyApplicationPassData(
                "Skintone application",
                Order: 0,
                InputCount: 1,
                notes));
            targets.Add(new SimAssemblyApplicationTargetData(
                "Skintone material targets",
                "Skintone application",
                skintoneRouteData.Count,
                skintoneRouteData.Count > 0
                    ? $"Prepared skintone application targets {skintoneRouteData.Count:N0} merged material(s) with region-map-aware routing support."
                    : skintoneRender is not null
                        ? "Resolved skintone inputs are available, but no region-map-bound payload materials exposed color-shift routing support."
                        : "Authoritative skintone metadata is available, but no merged payload materials are currently available for routing."));
            plans.Add(new SimAssemblyApplicationPlanData(
                "Skintone material routing",
                "Skintone application",
                Order: 0,
                skintoneRouteData.Count,
                skintoneRouteData.Count,
                skintoneRouteData.Count > 0
                    ? skintoneRender is not null
                        ? $"Prepared region-map-aware routing across {skintoneRouteData.Count:N0} merged material target(s) using resolved skintone inputs."
                        : $"Prepared routing across {skintoneRouteData.Count:N0} merged material target(s) using authoritative skintone metadata."
                    : skintoneRender is not null
                        ? "Resolved skintone inputs are available, but no region-map-bound payload materials were eligible for routing."
                        : "Authoritative skintone metadata is available, but no merged payload materials are currently available for routing."));
            transforms.Add(new SimAssemblyApplicationTransformData(
                "Skintone routing transform",
                "Skintone application",
                Order: 0,
                skintoneRouteData.Count,
                skintoneRouteData.Count,
                skintoneRouteData.Count > 0
                    ? skintoneRender is not null
                        ? $"Materialized {skintoneRouteData.Count:N0} region-map-aware skintone route record(s) against merged payload materials."
                        : $"Materialized {skintoneRouteData.Count:N0} internal skintone route record(s) against merged payload materials."
                    : skintoneRender is not null
                        ? "Resolved skintone inputs are available, but no region-map-bound material routes could be materialized."
                        : "Skintone metadata is available, but no merged payload materials were available to materialize internal routing records."));
            outcomes.Add(new SimAssemblyApplicationOutcomeData(
                "Skintone routing outcome",
                "Skintone application",
                Order: 0,
                skintoneRouteData.Count,
                skintoneRouteData.Count,
                skintoneRouteData.Count > 0
                    ? skintoneRender is not null
                        ? $"Applied region-map-aware skintone routing outcome to {skintoneRouteData.Count:N0} merged material target(s)."
                        : $"Applied internal skintone routing outcome to {skintoneRouteData.Count:N0} merged material target(s)."
                    : skintoneRender is not null
                        ? "Resolved skintone inputs are available, but no region-map-bound routing outcome could be materialized."
                        : "Skintone metadata is available, but no merged material routing outcome could be materialized."));
        }
        else
        {
            pendingPasses.Add(new SimAssemblyApplicationPassData(
                "Skintone application",
                Order: 0,
                InputCount: 0,
                "No authoritative skintone metadata is currently available for the assembled payload."));
        }

        var totalMorphInputCount = morphGroups?.Sum(static group => group.Count) ?? 0;
        if (totalMorphInputCount > 0)
        {
            var morphTransformData = BuildMorphMeshTransforms(payloadData, morphGroups!);
            morphTransformOperations = BuildMorphTransformOperations(morphTransformData, morphGroups!);
            preparedPasses.Add(new SimAssemblyApplicationPassData(
                "Morph application",
                Order: 1,
                InputCount: totalMorphInputCount,
                $"Morph metadata is available for the current payload across {morphGroups!.Count:N0} morph group(s)."));
            targets.Add(new SimAssemblyApplicationTargetData(
                "Morph mesh targets",
                "Morph application",
                payloadData.MergedMeshes.Count,
                $"Prepared morph application targets {payloadData.MergedMeshes.Count:N0} merged mesh(es) in the current payload."));
            plans.Add(new SimAssemblyApplicationPlanData(
                "Morph mesh transform planning",
                "Morph application",
                Order: 1,
                payloadData.MergedMeshes.Count,
                morphTransformData.Sum(static plan => plan.PlannedTransformCount),
                morphTransformData.Count > 0
                    ? $"Prepared transform planning across {morphTransformData.Count:N0} merged mesh target(s) using {morphGroups.Count:N0} authoritative morph group(s)."
                    : "Authoritative morph metadata is available, but no merged payload meshes are currently available for transform planning."));
            transforms.Add(new SimAssemblyApplicationTransformData(
                "Morph transform preparation",
                "Morph application",
                Order: 1,
                payloadData.MergedMeshes.Count,
                morphTransformOperations.Sum(static operation => operation.MorphChannelCount),
                morphTransformOperations.Count > 0
                    ? $"Materialized {morphTransformOperations.Count:N0} internal morph transform operation record(s) across {payloadData.MergedMeshes.Count:N0} merged mesh target(s)."
                    : "Morph metadata is available, but no merged payload meshes were available to materialize internal transform operation records."));
            outcomes.Add(new SimAssemblyApplicationOutcomeData(
                "Morph transform outcome",
                "Morph application",
                Order: 1,
                payloadData.MergedMeshes.Count,
                morphTransformOperations.Sum(static operation => operation.MorphChannelCount),
                morphTransformOperations.Count > 0
                    ? $"Applied internal morph transform outcome across {morphTransformOperations.Sum(static operation => operation.MorphChannelCount):N0} morph channel(s)."
                    : "Morph metadata is available, but no internal morph transform outcome could be materialized."));
        }
        else
        {
            pendingPasses.Add(new SimAssemblyApplicationPassData(
                "Morph application",
                Order: 1,
                InputCount: 0,
                "No authoritative morph groups are currently available for the assembled payload."));
        }

        return new SimAssemblyApplicationData(
            preparedPasses,
            pendingPasses,
            targets,
            plans,
            transforms,
            outcomes,
            BuildAppliedPayloadData(payloadData, skintoneRouteData, morphTransformOperations));
    }

    private static SimAssemblyApplicationSummary BuildApplicationSummary(SimAssemblyApplicationData applicationData) =>
        new(
            "Current application passes",
            applicationData.Outcomes.Count > 0
                ? "Materialized internal outcomes"
                : applicationData.PreparedPasses.Count > 0
                    ? "Prepared inputs available"
                    : "Pending authoritative inputs",
            applicationData.PreparedPasses.Count,
            applicationData.PendingPasses.Count,
            0,
            applicationData.Outcomes.Count > 0
                ? "Modifier-aware application data is now materialized into internal routing and transform outcomes from the current Sim payload and metadata."
                : applicationData.PreparedPasses.Count > 0
                    ? "Modifier-aware application passes are now bound to authoritative inputs from the current Sim payload and metadata."
                    : "Modifier-aware application passes are still waiting for authoritative metadata inputs.");

    private static IReadOnlyList<SimAssemblyApplicationPassSummary> BuildApplicationPasses(SimAssemblyApplicationData applicationData)
    {
        var passes = new List<SimAssemblyApplicationPassSummary>();
        passes.AddRange(
            applicationData.PreparedPasses.Select(pass => new SimAssemblyApplicationPassSummary(
                pass.Label,
                pass.Order,
                SimAssemblyApplicationPassState.Prepared,
                "Prepared",
                pass.InputCount,
                pass.Notes)));
        passes.AddRange(
            applicationData.PendingPasses.Select(pass => new SimAssemblyApplicationPassSummary(
                pass.Label,
                pass.Order,
                SimAssemblyApplicationPassState.Pending,
                "Pending",
                pass.InputCount,
                pass.Notes)));
        return passes.OrderBy(static pass => pass.Order).ToArray();
    }

    private static IReadOnlyList<SimAssemblyApplicationTargetSummary> BuildApplicationTargets(SimAssemblyApplicationData applicationData) =>
        applicationData.Targets
            .Select(target => new SimAssemblyApplicationTargetSummary(
                target.Label,
                target.PassLabel,
                "Prepared",
                target.TargetCount,
                target.Notes))
            .ToArray();

    private static IReadOnlyList<SimAssemblyApplicationPlanSummary> BuildApplicationPlans(SimAssemblyApplicationData applicationData) =>
        applicationData.Plans
            .OrderBy(static plan => plan.Order)
            .Select(plan => new SimAssemblyApplicationPlanSummary(
                plan.Label,
                plan.PassLabel,
                "Prepared",
                plan.TargetCount,
                plan.OperationCount,
                plan.Notes))
            .ToArray();

    private static IReadOnlyList<SimAssemblyApplicationTransformSummary> BuildApplicationTransforms(SimAssemblyApplicationData applicationData) =>
        applicationData.Transforms
            .OrderBy(static transform => transform.Order)
            .Select(transform => new SimAssemblyApplicationTransformSummary(
                transform.Label,
                transform.PassLabel,
                "Materialized",
                transform.TargetCount,
                transform.OperationCount,
                transform.Notes))
            .ToArray();

    private static IReadOnlyList<SimAssemblyApplicationOutcomeSummary> BuildApplicationOutcomes(SimAssemblyApplicationData applicationData) =>
        applicationData.Outcomes
            .OrderBy(static outcome => outcome.Order)
            .Select(outcome => new SimAssemblyApplicationOutcomeSummary(
                outcome.Label,
                outcome.PassLabel,
                "Applied internally",
                outcome.TargetCount,
                outcome.AppliedCount,
                outcome.Notes))
            .ToArray();

    private static string? BuildApplicationDiagnostics(SimAssemblyApplicationData applicationData)
    {
        if (applicationData.Outcomes.Count == 0)
        {
            return null;
        }

        return string.Join(
            Environment.NewLine,
            applicationData.Outcomes
                .OrderBy(static outcome => outcome.Order)
                .Select(static outcome => outcome.Notes));
    }

    private static SimAssemblyAppliedPayloadData BuildAppliedPayloadData(
        SimAssemblyPayloadData payloadData,
        IReadOnlyList<SimAssemblySkintoneMaterialRouteData> skintoneRoutes,
        IReadOnlyList<SimAssemblyMorphTransformOperationData> morphTransformOperations)
    {
        var routeLookup = skintoneRoutes.ToDictionary(static route => route.MaterialIndex);
        var effectiveMaterials = payloadData.MergedMaterials
            .Select((material, index) =>
                routeLookup.TryGetValue(index, out var route)
                    ? ApplySkintoneRouteToMaterial(material, route)
                    : material)
            .ToArray();

        var appliedMorphSets = morphTransformOperations
            .GroupBy(static operation => new { operation.MeshIndex, operation.MeshName })
            .Select(group => new SimAssemblyAppliedMorphSetData(
                group.Key.MeshIndex,
                group.Key.MeshName,
                group.Sum(static operation => operation.MorphChannelCount),
                $"Applied morph transform set prepared for mesh {group.Key.MeshIndex + 1:N0} using {group.Count():N0} morph group binding(s) and {group.Sum(static operation => operation.MorphChannelCount):N0} total channel(s)."))
            .OrderBy(static set => set.MeshIndex)
            .ToArray();

        return new SimAssemblyAppliedPayloadData(
            effectiveMaterials,
            payloadData.MergedMeshes,
            appliedMorphSets);
    }

    private static CanonicalMaterial ApplySkintoneRouteToMaterial(
        CanonicalMaterial material,
        SimAssemblySkintoneMaterialRouteData route)
    {
        var routeNote = route.SkintoneShift.HasValue
            ? $"Sim skintone route {route.SkintoneInstanceHex ?? "(unknown)"} shift {route.SkintoneShift.Value:0.###}"
            : $"Sim skintone route {route.SkintoneInstanceHex ?? "(unknown)"}";
        var regionNote = route.RegionLabels.Count > 0
            ? $"region_map {string.Join(", ", route.RegionLabels)}"
            : null;
        var sourceNote = string.IsNullOrWhiteSpace(route.SourceLabel)
            ? null
            : $"source {route.SourceLabel}";
        var approximation = string.IsNullOrWhiteSpace(material.Approximation)
            ? string.Join(" | ", new[] { routeNote, regionNote, sourceNote }.Where(static note => !string.IsNullOrWhiteSpace(note)))
            : $"{material.Approximation} | {string.Join(" | ", new[] { routeNote, regionNote, sourceNote }.Where(static note => !string.IsNullOrWhiteSpace(note)))}";
        return material with
        {
            Approximation = approximation,
            SourceKind = route.ViewportTintColor is not null
                ? CanonicalMaterialSourceKind.ApproximateCas
                : material.SourceKind,
            ViewportTintColor = route.ViewportTintColor ?? material.ViewportTintColor,
            ApproximateBaseColor = route.ViewportTintColor ?? material.ApproximateBaseColor
        };
    }

    private static ScenePreviewContent ApplyApplicationDataToPreview(
        ScenePreviewContent preview,
        SimAssemblyApplicationData applicationData)
    {
        if (preview.Scene is null)
        {
            return preview;
        }

        var appliedPayload = applicationData.AppliedPayload;
        var scene = preview.Scene with
        {
            Materials = appliedPayload.Materials,
            Meshes = appliedPayload.Meshes
        };

        return preview with
        {
            Scene = scene,
            Diagnostics = JoinDiagnostics(preview.Diagnostics, BuildApplicationDiagnostics(applicationData))
        };
    }

    private static IReadOnlyList<SimAssemblySkintoneMaterialRouteData> BuildSkintoneMaterialRoutes(
        SimAssemblyPayloadData payloadData,
        SimInfoSummary? simMetadata,
        SimSkintoneRenderSummary? skintoneRender,
        IReadOnlyList<CasRegionMapSummary>? bodyRegionMaps,
        IReadOnlyList<CasRegionMapSummary>? headRegionMaps)
    {
        if (skintoneRender is not null)
        {
            if (skintoneRender.ViewportTintColor is null)
            {
                return [];
            }

            var routes = new List<SimAssemblySkintoneMaterialRouteData>();
            foreach (var target in ResolveRegionMapAwareSkintoneTargets(payloadData, bodyRegionMaps, headRegionMaps))
            {
                var material = payloadData.MergedMaterials[target.MaterialIndex];
                routes.Add(new SimAssemblySkintoneMaterialRouteData(
                    target.MaterialIndex,
                    string.IsNullOrWhiteSpace(material.Name) ? $"Material {target.MaterialIndex + 1}" : material.Name,
                    skintoneRender.SkintoneInstanceHex,
                    skintoneRender.SkintoneShift,
                    skintoneRender.ViewportTintColor,
                    target.SourceLabel,
                    target.RegionLabels,
                    skintoneRender.SkintoneShift.HasValue
                        ? $"Route material {target.MaterialIndex + 1:N0} through resolved skintone {skintoneRender.SkintoneInstanceHex ?? "(unknown)"} with shift {skintoneRender.SkintoneShift.Value:0.###} across region_map {string.Join(", ", target.RegionLabels)}."
                        : $"Route material {target.MaterialIndex + 1:N0} through resolved skintone {skintoneRender.SkintoneInstanceHex ?? "(unknown)"} across region_map {string.Join(", ", target.RegionLabels)}."));
            }

            return routes;
        }

        if (simMetadata is null)
        {
            return [];
        }

        return payloadData.MergedMaterials
            .Select((material, index) => new SimAssemblySkintoneMaterialRouteData(
                index,
                string.IsNullOrWhiteSpace(material.Name) ? $"Material {index + 1}" : material.Name,
                simMetadata.SkintoneInstanceHex,
                simMetadata.SkintoneShift,
                null,
                string.Empty,
                [],
                simMetadata.SkintoneShift.HasValue
                    ? $"Route material {index + 1:N0} through skintone instance {simMetadata.SkintoneInstanceHex ?? "(unknown)"} with shift {simMetadata.SkintoneShift.Value:0.###}."
                    : $"Route material {index + 1:N0} through skintone instance {simMetadata.SkintoneInstanceHex ?? "(unknown)"}."))
            .ToArray();
    }

    private static IReadOnlyList<SimAssemblySkintoneMaterialTargetData> ResolveRegionMapAwareSkintoneTargets(
        SimAssemblyPayloadData payloadData,
        IReadOnlyList<CasRegionMapSummary>? bodyRegionMaps,
        IReadOnlyList<CasRegionMapSummary>? headRegionMaps)
    {
        var targets = new List<SimAssemblySkintoneMaterialTargetData>();
        foreach (var batch in payloadData.MeshBatches)
        {
            var regionMaps = batch.SourceLabel.StartsWith("Body shell", StringComparison.OrdinalIgnoreCase)
                ? bodyRegionMaps ?? []
                : batch.SourceLabel.StartsWith("Head shell", StringComparison.OrdinalIgnoreCase)
                    ? headRegionMaps ?? []
                    : [];
            if (regionMaps.Count == 0)
            {
                continue;
            }

            var regionLabels = regionMaps
                .SelectMany(static map => map.RegionLabels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(static label => label, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            for (var offset = 0; offset < batch.MaterialCount; offset++)
            {
                var materialIndex = batch.MaterialStartIndex + offset;
                if (materialIndex < 0 || materialIndex >= payloadData.MergedMaterials.Count)
                {
                    continue;
                }

                if (!MaterialSupportsRegionMapSkintoneRouting(payloadData.MergedMaterials[materialIndex]))
                {
                    continue;
                }

                targets.Add(new SimAssemblySkintoneMaterialTargetData(
                    materialIndex,
                    batch.SourceLabel,
                    regionLabels));
            }
        }

        return targets
            .GroupBy(static target => target.MaterialIndex)
            .Select(static group => group.First())
            .OrderBy(static target => target.MaterialIndex)
            .ToArray();
    }

    private static bool MaterialSupportsRegionMapSkintoneRouting(CanonicalMaterial material)
    {
        if (material.Textures.Any(static texture => texture.Slot.Equals("color_shift_mask", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (material.Textures.Any(static texture => texture.Slot.Equals("region_map", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return material.SourceKind == CanonicalMaterialSourceKind.ApproximateCas;
    }

    private static IReadOnlyList<SimAssemblyMorphMeshTransformData> BuildMorphMeshTransforms(
        SimAssemblyPayloadData payloadData,
        IReadOnlyList<SimMorphGroupSummary> morphGroups)
    {
        var totalMorphInputCount = morphGroups.Sum(static group => group.Count);
        var groupLabelSummary = string.Join(", ", morphGroups.Select(static group => group.Label));
        return payloadData.MergedMeshes
            .Select((mesh, index) => new SimAssemblyMorphMeshTransformData(
                index,
                string.IsNullOrWhiteSpace(mesh.Name) ? $"Mesh {index + 1}" : mesh.Name,
                morphGroups.Count,
                totalMorphInputCount,
                totalMorphInputCount,
                $"Plan morph transforms for mesh {index + 1:N0} across {morphGroups.Count:N0} authoritative group(s): {groupLabelSummary}.")
            )
            .ToArray();
    }

    private static IReadOnlyList<SimAssemblyMorphTransformOperationData> BuildMorphTransformOperations(
        IReadOnlyList<SimAssemblyMorphMeshTransformData> morphTransformData,
        IReadOnlyList<SimMorphGroupSummary> morphGroups) =>
        morphTransformData
            .SelectMany(
                meshPlan => morphGroups.Select(group => new SimAssemblyMorphTransformOperationData(
                    meshPlan.MeshIndex,
                    meshPlan.MeshName,
                    group.Label,
                    group.Count,
                    $"Materialized morph transform operation planning for mesh {meshPlan.MeshIndex + 1:N0} across group '{group.Label}' with {group.Count:N0} channel(s).")))
            .ToArray();

    private static SimAssemblyOutputSummary BuildOutputSummary(
        ScenePreviewContent preview,
        SimAssemblyBasisKind basisKind,
        bool includesHeadShell,
        SimAssemblyPayloadData? payloadData = null)
    {
        var scene = preview.Scene;
        var status = scene is null
            ? "Unavailable"
            : includesHeadShell
                ? "Resolved multi-part rig seam"
                : "Resolved anchor-only rig seam";
        var notes = scene is null
            ? "No rig-centered torso/head payload seam is currently available."
            : basisKind switch
            {
                SimAssemblyBasisKind.SharedRigResource => "The current torso/head payload seam is rig-centered and anchored to a shared exact rig resource.",
                SimAssemblyBasisKind.SharedRigInstance => "The current torso/head payload seam is rig-centered and anchored to a shared rig instance id.",
                SimAssemblyBasisKind.CanonicalBoneFallback => "The current torso/head payload seam is rig-centered but currently relies on canonical-bone fallback instead of a shared authoritative rig.",
                SimAssemblyBasisKind.BodyOnly => "The current torso/head payload seam is anchor-only and currently carries only the body shell contribution.",
                _ => "The current torso/head payload seam does not yet expose a stable assembly basis."
            };

        return new SimAssemblyOutputSummary(
            "Current torso/head payload seam",
            status,
            scene?.Meshes.Count ?? 0,
            scene?.Materials.Count ?? 0,
            scene?.Bones.Count ?? 0,
            payloadData?.AcceptedContributionCount ?? 0,
            payloadData is null ? [] : BuildAcceptedSeamInputs(payloadData),
            notes);
    }

    private static IReadOnlyList<SimAssemblyContributionSummary> BuildAnchorOnlyContributions(ScenePreviewContent bodyPreview) =>
        bodyPreview.Scene is null
            ? []
            : [CreateAnchorContribution(bodyPreview)];

    private static SimAssemblyContributionSummary CreateAnchorContribution(ScenePreviewContent input)
    {
        var scene = input.Scene!;
        return new SimAssemblyContributionSummary(
            "Body shell contribution",
            "Anchor",
            scene.Meshes.Count,
            scene.Materials.Count,
            scene.Bones.Count,
            0,
            0,
            "The accepted body shell scene currently defines the assembly skeleton basis.");
    }

    private static SimAssemblyBoneMapSummary CreateBoneMapSummary(
        ScenePreviewContent input,
        int sourceBoneCount,
        int reusedBoneReferenceCount,
        int addedBoneCount,
        int rebasedWeightCount,
        IReadOnlyList<SimAssemblyBoneMapEntrySummary> entries) =>
        new(
            "Head shell bone map",
            GetSourceLabel(input),
            GetSourceResourceTgi(input),
            GetSourcePackagePath(input),
            sourceBoneCount,
            reusedBoneReferenceCount,
            addedBoneCount,
            rebasedWeightCount,
            entries,
            "The accepted head shell contribution was mapped onto the body skeletal anchor before downstream torso/head modifier consumption.");

    private static SimAssemblyMeshBatchSummary CreateMeshBatchSummary(
        string label,
        string sourceLabel,
        ScenePreviewContent sourceInput,
        int meshStartIndex,
        int meshCount,
        int materialStartIndex,
        int materialCount,
        string notes) =>
        new(
            label,
            sourceLabel,
            GetSourceResourceTgi(sourceInput),
            GetSourcePackagePath(sourceInput),
            meshStartIndex,
            meshCount,
            materialStartIndex,
            materialCount,
            notes);

    private static SimAssemblyContributionSummary CreateMergedContribution(
        ScenePreviewContent input,
        int rebasedWeightCount,
        int addedBoneCount)
    {
        var scene = input.Scene!;
        return new SimAssemblyContributionSummary(
            "Head shell contribution",
            "Merged",
            scene.Meshes.Count,
            scene.Materials.Count,
            scene.Bones.Count,
            rebasedWeightCount,
            addedBoneCount,
            "The accepted head shell scene was merged onto the body skeletal anchor.");
    }

    private static IReadOnlyList<SimAssemblySeamInputSummary> BuildAcceptedSeamInputs(SimAssemblyPayloadData payloadData)
    {
        var remapsByResourceId = payloadData.BoneRemaps
            .ToDictionary(static remap => remap.SourceInput.Resource.Id);

        return payloadData.MeshBatches
            .Select(batch =>
            {
                var sourceInput = batch.SourceInput;
                var isAnchor = sourceInput.Resource.Id == payloadData.Anchor.SourceInput.Resource.Id;
                var sourceScene = sourceInput.Scene;
                remapsByResourceId.TryGetValue(sourceInput.Resource.Id, out var remap);
                return new SimAssemblySeamInputSummary(
                    isAnchor ? "Body shell seam input" : "Head shell seam input",
                    isAnchor ? "Anchor" : "Merged",
                    GetSourceLabel(sourceInput),
                    GetSourceResourceTgi(sourceInput),
                    GetSourcePackagePath(sourceInput),
                    batch.MeshStartIndex,
                    batch.MeshCount,
                    batch.MaterialStartIndex,
                    batch.MaterialCount,
                    sourceScene?.Bones.Count ?? 0,
                    remap?.RebasedWeightCount ?? 0,
                    remap?.AddedBoneCount ?? 0,
                    isAnchor
                        ? "The accepted body shell contribution defines the rig anchor and initial mesh/material ranges for the current seam."
                        : "The accepted head shell contribution is tracked as an explicit merged seam input with rebased rig references.");
            })
            .ToArray();
    }

    private static IReadOnlyList<SimAssemblyBoneMapEntrySummary> BuildBoneMapEntries(SimAssemblyBoneRemapData remap) =>
        remap.BoneIndexMap
            .OrderBy(static pair => pair.Key)
            .Select(pair => new SimAssemblyBoneMapEntrySummary(
                pair.Key,
                pair.Value,
                remap.SourceInput.Scene?.Bones[pair.Key].Name ?? $"Bone {pair.Key}"))
            .ToArray();

    private static string GetSourceLabel(ScenePreviewContent input) =>
        input.Scene?.Name ?? input.Resource.Name ?? input.Resource.Key.FullTgi;

    private static string GetSourceResourceTgi(ScenePreviewContent input) =>
        input.Resource.Key.FullTgi;

    private static string GetSourcePackagePath(ScenePreviewContent input) =>
        input.Resource.PackagePath;

    private static IReadOnlyList<VertexWeight> RebaseSkinWeights(
        IReadOnlyList<VertexWeight> weights,
        IReadOnlyDictionary<int, int> boneIndexMap,
        out int rebasedWeightCount)
    {
        rebasedWeightCount = weights.Count;
        return weights
            .Select(weight =>
            {
                var rebasedBoneIndex = boneIndexMap.TryGetValue(weight.BoneIndex, out var mergedBoneIndex)
                    ? mergedBoneIndex
                    : weight.BoneIndex;
                return weight with
                {
                    BoneIndex = rebasedBoneIndex
                };
            })
            .ToArray();
    }

    private static SimAssemblyPayloadData BuildAnchorOnlyPayloadData(
        ScenePreviewContent bodyInput,
        CanonicalScene bodyScene) =>
        new(
            new SimAssemblyAnchorData(bodyInput, bodyScene.Bones),
            bodyScene.Materials,
            bodyScene.Bones,
            bodyScene.Meshes,
            [],
            [new SimAssemblyMeshBatchData(
                "Body shell mesh batch",
                "Body shell contribution",
                bodyInput,
                0,
                bodyScene.Meshes.Count,
                0,
                bodyScene.Materials.Count,
                "The current payload contains only the accepted body shell mesh batch.")],
            AcceptedContributionCount: 1,
            TotalRebasedWeightCount: 0,
            TotalMappedBoneReferenceCount: 0,
            TotalAddedBoneCount: 0);

    private static SimAssemblyPayloadData BuildMergedPayloadData(IReadOnlyList<ScenePreviewContent> renderableInputs)
    {
        var bodyInput = renderableInputs[0];
        var bodyScene = bodyInput.Scene!;
        var anchor = new SimAssemblyAnchorData(bodyInput, bodyScene.Bones);
        var materials = new List<CanonicalMaterial>(bodyScene.Materials);
        var bones = new List<CanonicalBone>(bodyScene.Bones);
        var meshes = new List<CanonicalMesh>(bodyScene.Meshes);
        var boneIndexByName = bodyScene.Bones
            .Select((bone, index) => (bone.Name, index))
            .GroupBy(static pair => pair.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First().index, StringComparer.OrdinalIgnoreCase);
        var boneRemaps = new List<SimAssemblyBoneRemapData>();
        var meshBatches = new List<SimAssemblyMeshBatchData>
        {
            new(
                "Body shell mesh batch",
                "Body shell contribution",
                bodyInput,
                0,
                bodyScene.Meshes.Count,
                0,
                bodyScene.Materials.Count,
                "The accepted body shell batch defines the anchor-side mesh/material range for the current payload.")
        };
        var totalRebasedWeightCount = 0;
        var totalMappedBoneReferenceCount = 0;
        var totalAddedBoneCount = 0;

        foreach (var input in renderableInputs.Skip(1))
        {
            var scene = input.Scene!;
            var meshStartIndex = meshes.Count;
            var materialOffset = materials.Count;
            materials.AddRange(scene.Materials);

            var boneIndexMap = new Dictionary<int, int>();
            var addedBoneCount = 0;
            var mappedBoneReferenceCount = 0;
            for (var boneIndex = 0; boneIndex < scene.Bones.Count; boneIndex++)
            {
                var bone = scene.Bones[boneIndex];
                if (!boneIndexByName.TryGetValue(bone.Name, out var mergedBoneIndex))
                {
                    mergedBoneIndex = bones.Count;
                    bones.Add(bone);
                    boneIndexByName.Add(bone.Name, mergedBoneIndex);
                    addedBoneCount++;
                }
                else
                {
                    mappedBoneReferenceCount++;
                }

                boneIndexMap[boneIndex] = mergedBoneIndex;
            }

            var rebasedWeightCount = 0;
            foreach (var mesh in scene.Meshes)
            {
                var remappedSkinWeights = RebaseSkinWeights(mesh.SkinWeights, boneIndexMap, out var meshRebasedWeightCount);
                rebasedWeightCount += meshRebasedWeightCount;
                meshes.Add(mesh with
                {
                    MaterialIndex = mesh.MaterialIndex + materialOffset,
                    SkinWeights = remappedSkinWeights
                });
            }

            totalRebasedWeightCount += rebasedWeightCount;
            totalMappedBoneReferenceCount += mappedBoneReferenceCount;
            totalAddedBoneCount += addedBoneCount;
            boneRemaps.Add(new SimAssemblyBoneRemapData(
                input,
                scene.Bones.Count,
                boneIndexMap,
                mappedBoneReferenceCount,
                addedBoneCount,
                rebasedWeightCount));
            meshBatches.Add(new SimAssemblyMeshBatchData(
                "Head shell mesh batch",
                "Head shell contribution",
                input,
                meshStartIndex,
                scene.Meshes.Count,
                materialOffset,
                scene.Materials.Count,
                "The accepted head shell batch was appended after rebasing onto the body skeletal anchor."));
        }

        return new SimAssemblyPayloadData(
            anchor,
            materials,
            bones,
            meshes,
            boneRemaps,
            meshBatches,
            AcceptedContributionCount: renderableInputs.Count,
            TotalRebasedWeightCount: totalRebasedWeightCount,
            TotalMappedBoneReferenceCount: totalMappedBoneReferenceCount,
            TotalAddedBoneCount: totalAddedBoneCount);
    }

    private static SceneBuildStatus DetermineAggregateStatus(IReadOnlyList<ScenePreviewContent> previews)
    {
        if (previews.Count == 0)
        {
            return SceneBuildStatus.Unsupported;
        }

        if (previews.Any(static preview => preview.Scene is not null) &&
            previews.All(static preview => preview.Status == SceneBuildStatus.SceneReady))
        {
            return SceneBuildStatus.SceneReady;
        }

        return previews.Any(static preview => preview.Scene is not null)
            ? SceneBuildStatus.Partial
            : SceneBuildStatus.Unsupported;
    }

    private static Bounds3D ComputeBounds(IEnumerable<CanonicalMesh> meshes)
    {
        var positions = meshes.SelectMany(static mesh => mesh.Positions).Chunk(3).ToArray();
        if (positions.Length == 0)
        {
            return new Bounds3D(0, 0, 0, 0, 0, 0);
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var minZ = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;
        var maxZ = float.NegativeInfinity;

        foreach (var position in positions)
        {
            minX = Math.Min(minX, position[0]);
            minY = Math.Min(minY, position[1]);
            minZ = Math.Min(minZ, position[2]);
            maxX = Math.Max(maxX, position[0]);
            maxY = Math.Max(maxY, position[1]);
            maxZ = Math.Max(maxZ, position[2]);
        }

        return new Bounds3D(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private static IReadOnlyList<SimAssemblyInputSummary> BuildInputs(
        bool bodySceneResolved,
        bool headSceneResolved,
        SimAssemblyBasisKind basisKind,
        bool includesHeadShell,
        string notes) =>
        new[]
        {
            new SimAssemblyInputSummary(
                "Body shell input",
                bodySceneResolved ? "Resolved" : "Missing",
                bodySceneResolved,
                bodySceneResolved
                    ? "A renderable body shell scene is available as the primary Sim assembly input."
                    : "No renderable body shell scene is available for Sim assembly."),
            new SimAssemblyInputSummary(
                "Head shell input",
                includesHeadShell
                    ? "Accepted"
                    : headSceneResolved
                        ? "Withheld"
                        : "Missing",
                includesHeadShell,
                includesHeadShell
                    ? "A renderable head shell scene is accepted into the current Sim assembly."
                    : headSceneResolved
                        ? "A renderable head shell scene exists, but it is not accepted into the current Sim assembly."
                        : "No renderable head shell scene is available for Sim assembly."),
            new SimAssemblyInputSummary(
                "Assembly basis input",
                basisKind switch
                {
                    SimAssemblyBasisKind.SharedRigResource or SimAssemblyBasisKind.SharedRigInstance => "Authoritative",
                    SimAssemblyBasisKind.CanonicalBoneFallback => "Fallback",
                    SimAssemblyBasisKind.BodyOnly => "Body-only",
                    _ => "Unavailable"
                },
                basisKind is not SimAssemblyBasisKind.None,
                notes)
        };

    private static ScenePreviewContent RenameBodyPreview(
        string name,
        ScenePreviewContent preview,
        string diagnostics,
        SceneBuildStatus status)
    {
        var renamedScene = preview.Scene is null || string.Equals(preview.Scene.Name, name, StringComparison.Ordinal)
            ? preview.Scene
            : preview.Scene with { Name = name };
        return new ScenePreviewContent(preview.Resource, renamedScene, diagnostics, status);
    }

    private static string JoinDiagnostics(params string?[] diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .SelectMany(static text => text!.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Distinct(StringComparer.Ordinal));
}

internal sealed record SimRigCompatibilityResult(
    SimAssemblyBasisKind BasisKind,
    bool HasAuthoritativeRigMatch,
    bool IsDefinitiveMismatch,
    string Diagnostic);

internal sealed record SimAssemblyStageExecutionResult(
    ScenePreviewContent Preview,
    SimAssemblyPlanSummary Plan,
    SimAssemblyPayloadSummary Payload,
    SimAssemblyAnchorSummary PayloadAnchor,
    IReadOnlyList<SimAssemblyBoneMapSummary> PayloadBoneMaps,
    IReadOnlyList<SimAssemblyMeshBatchSummary> PayloadMeshBatches,
    IReadOnlyList<SimAssemblyPayloadNodeSummary> PayloadNodes,
    SimAssemblyApplicationSummary Application,
    IReadOnlyList<SimAssemblyApplicationPassSummary> ApplicationPasses,
    IReadOnlyList<SimAssemblyApplicationTargetSummary> ApplicationTargets,
    IReadOnlyList<SimAssemblyApplicationPlanSummary> ApplicationPlans,
    IReadOnlyList<SimAssemblyApplicationTransformSummary> ApplicationTransforms,
    IReadOnlyList<SimAssemblyApplicationOutcomeSummary> ApplicationOutcomes,
    SimAssemblyOutputSummary Output,
    IReadOnlyList<SimAssemblyContributionSummary> Contributions,
    bool BodySceneResolved,
    bool HeadSceneResolved,
    IReadOnlyList<SimAssemblyStageSummary> Stages);

internal sealed record SimAssemblyOutputComputationResult(
    ScenePreviewContent Preview,
    SimAssemblyPayloadSummary Payload,
    SimAssemblyAnchorSummary PayloadAnchor,
    IReadOnlyList<SimAssemblyBoneMapSummary> PayloadBoneMaps,
    IReadOnlyList<SimAssemblyMeshBatchSummary> PayloadMeshBatches,
    IReadOnlyList<SimAssemblyPayloadNodeSummary> PayloadNodes,
    SimAssemblyPayloadData PayloadData,
    IReadOnlyList<SimAssemblyContributionSummary> Contributions);

internal sealed record SimAssemblyAnchorData(
    ScenePreviewContent SourceInput,
    IReadOnlyList<CanonicalBone> Bones);

internal sealed record SimAssemblyBoneRemapData(
    ScenePreviewContent SourceInput,
    int SourceBoneCount,
    IReadOnlyDictionary<int, int> BoneIndexMap,
    int ReusedBoneReferenceCount,
    int AddedBoneCount,
    int RebasedWeightCount);

internal sealed record SimAssemblyMeshBatchData(
    string Label,
    string SourceLabel,
    ScenePreviewContent SourceInput,
    int MeshStartIndex,
    int MeshCount,
    int MaterialStartIndex,
    int MaterialCount,
    string Notes);

internal sealed record SimAssemblyPayloadData(
    SimAssemblyAnchorData Anchor,
    IReadOnlyList<CanonicalMaterial> MergedMaterials,
    IReadOnlyList<CanonicalBone> MergedBones,
    IReadOnlyList<CanonicalMesh> MergedMeshes,
    IReadOnlyList<SimAssemblyBoneRemapData> BoneRemaps,
    IReadOnlyList<SimAssemblyMeshBatchData> MeshBatches,
    int AcceptedContributionCount,
    int TotalRebasedWeightCount,
    int TotalMappedBoneReferenceCount,
    int TotalAddedBoneCount);

internal sealed record SimAssemblyApplicationPassData(
    string Label,
    int Order,
    int InputCount,
    string Notes);

internal sealed record SimAssemblyApplicationTargetData(
    string Label,
    string PassLabel,
    int TargetCount,
    string Notes);

internal sealed record SimAssemblyApplicationPlanData(
    string Label,
    string PassLabel,
    int Order,
    int TargetCount,
    int OperationCount,
    string Notes);

internal sealed record SimAssemblyApplicationTransformData(
    string Label,
    string PassLabel,
    int Order,
    int TargetCount,
    int OperationCount,
    string Notes);

internal sealed record SimAssemblyApplicationOutcomeData(
    string Label,
    string PassLabel,
    int Order,
    int TargetCount,
    int AppliedCount,
    string Notes);

internal sealed record SimAssemblyAppliedMorphSetData(
    int MeshIndex,
    string MeshName,
    int AppliedChannelCount,
    string Notes);

internal sealed record SimAssemblyAppliedPayloadData(
    IReadOnlyList<CanonicalMaterial> Materials,
    IReadOnlyList<CanonicalMesh> Meshes,
    IReadOnlyList<SimAssemblyAppliedMorphSetData> MorphSets);

internal sealed record SimAssemblySkintoneMaterialTargetData(
    int MaterialIndex,
    string SourceLabel,
    IReadOnlyList<string> RegionLabels);

internal sealed record SimAssemblySkintoneMaterialRouteData(
    int MaterialIndex,
    string MaterialName,
    string? SkintoneInstanceHex,
    float? SkintoneShift,
    CanonicalColor? ViewportTintColor,
    string SourceLabel,
    IReadOnlyList<string> RegionLabels,
    string Notes);

internal sealed record SimAssemblyMorphMeshTransformData(
    int MeshIndex,
    string MeshName,
    int MorphGroupCount,
    int MorphInputCount,
    int PlannedTransformCount,
    string Notes);

internal sealed record SimAssemblyMorphTransformOperationData(
    int MeshIndex,
    string MeshName,
    string MorphGroupLabel,
    int MorphChannelCount,
    string Notes);

internal sealed record SimAssemblyApplicationData(
    IReadOnlyList<SimAssemblyApplicationPassData> PreparedPasses,
    IReadOnlyList<SimAssemblyApplicationPassData> PendingPasses,
    IReadOnlyList<SimAssemblyApplicationTargetData> Targets,
    IReadOnlyList<SimAssemblyApplicationPlanData> Plans,
    IReadOnlyList<SimAssemblyApplicationTransformData> Transforms,
    IReadOnlyList<SimAssemblyApplicationOutcomeData> Outcomes,
    SimAssemblyAppliedPayloadData AppliedPayload);
