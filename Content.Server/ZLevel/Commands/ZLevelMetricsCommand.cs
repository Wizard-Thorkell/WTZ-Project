// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Server.Administration;
using Content.Server.Explosion.EntitySystems;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.Administration;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Console;

namespace Content.Server.ZLevel.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ZLevelMetricsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "zlevelmetrics";
    public string Description => "Shows or resets process-local native Z-level performance counters.";
    public string Help => $"Usage: {Command} [reset]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var metricsSystem = _entityManager.System<SharedZLevelMetricsSystem>();
        if (args.Length == 1 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            metricsSystem.ResetCounters();
            _entityManager.System<SharedZLevelSoundPortalSystem>().ResetMetrics();
            _entityManager.System<ZLevelSoundRouteSystem>().ResetMetrics();
            _entityManager.System<ZLevelSoundPlaybackSystem>().ResetMetrics();
            _entityManager.System<ZLevelTraversalGraphSystem>().ResetMetrics();
            shell.WriteLine("Reset native Z-level performance counters.");
            return;
        }

        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var metrics = metricsSystem.Snapshot();
        var boundaries = _entityManager.System<SharedZLevelBoundarySystem>();
        var gravity = _entityManager.System<SharedZLevelGravitySystem>();
        var explosion = _entityManager.System<ExplosionSystem>();
        var pvs = _entityManager.System<ZLevelPvsSystem>();
        var soundPlayback = _entityManager.System<ZLevelSoundPlaybackSystem>();
        var soundPortals = _entityManager.System<SharedZLevelSoundPortalSystem>();
        var soundRoutes = _entityManager.System<ZLevelSoundRouteSystem>();
        var trace = _entityManager.System<SharedZLevelTraceSystem>();
        var traversalGraph = _entityManager.System<ZLevelTraversalGraphSystem>();
        var visibility = _entityManager.System<SharedZLevelVisibilitySystem>();

        shell.WriteLine("Native Z-level metrics for this process since the last reset:");
        shell.WriteLine(
            $"  boundary: queries={metrics.BoundaryQueries}, hits={metrics.BoundaryCacheHits}, " +
            $"misses={metrics.BoundaryCacheMisses}, hit-rate={metrics.BoundaryCacheHitPercent:0.00}%, " +
            $"cache={boundaries.CachedBoundaryCount}/{boundaries.BoundaryCacheCapacity}, " +
            $"invalidations={metrics.BoundaryInvalidations}, evictions={metrics.BoundaryEvictions}");
        shell.WriteLine(
            $"  visibility: entity={metrics.VisibilityEntityQueries}, tile={metrics.VisibilityTileQueries}, " +
            $"same-level={metrics.VisibilitySameLevel}, boundary-checks={metrics.VisibilityBoundaryChecks}, " +
            $"early-rejections={metrics.VisibilityEarlyRejections}, " +
            $"max-world-z-distance={visibility.MaxVisibleLevelDistance}");
        shell.WriteLine(
            $"  gravity: queries={metrics.GravityQueries}, hit-rate={metrics.GravityCacheHitPercent:0.00}%, " +
            $"cached-grids={gravity.CachedGridCount}, pending={gravity.PendingRefreshGridCount}, " +
            $"invalidations={metrics.GravityInvalidations}");
        shell.WriteLine(
            $"  gravity-build: count={metrics.GravityBuilds}, tiles={metrics.GravityBuildTiles}, " +
            $"sources={metrics.GravityBuildSources}, avg={metrics.GravityAverageBuildMilliseconds:0.000}ms, " +
            $"last={metrics.GravityLastBuildMilliseconds:0.000}ms, max={metrics.GravityMaxBuildMilliseconds:0.000}ms");
        shell.WriteLine(
            $"  pvs: refreshes={metrics.PvsRefreshes}, viewers={metrics.PvsViewers}, " +
            $"candidates={metrics.PvsCandidates}, visible={metrics.PvsVisible}, culled={metrics.PvsCulled}, " +
            $"checks={metrics.PvsVisibilityChecks}/{pvs.VisibilityCheckBudget}, " +
            $"budget-exhaustions={metrics.PvsBudgetExhaustions}, " +
            $"fail-open-candidates={metrics.PvsFailOpenCandidates}, " +
            $"avg={metrics.PvsAverageRefreshMilliseconds:0.000}ms, " +
            $"last={metrics.PvsLastRefreshMilliseconds:0.000}ms, max={metrics.PvsMaxRefreshMilliseconds:0.000}ms");
        shell.WriteLine(
            $"  trace: queries={metrics.TraceQueries}, completed={metrics.TraceCompleted}, " +
            $"closed={metrics.TraceClosedBoundaries}, invalid={metrics.TraceInvalidCoordinates}, " +
            $"different-maps={metrics.TraceDifferentMaps}, " +
            $"frame-failures={metrics.TraceFrameResolutionFailures}, " +
            $"budget-exhaustions={metrics.TraceBudgetExhaustions}, " +
            $"avg={metrics.TraceAverageMilliseconds:0.000}ms, " +
            $"last={metrics.TraceLastMilliseconds:0.000}ms, max={metrics.TraceMaxMilliseconds:0.000}ms");
        shell.WriteLine(
            $"  trace output: segments={metrics.TraceSegments}, tiles={metrics.TraceTileVisits}, " +
            $"hits={metrics.TraceEntityHits}, crossings={metrics.TraceBoundaryCrossings}");
        shell.WriteLine(
            $"  interaction: queries={metrics.InteractionQueries}, allowed={metrics.InteractionAllowed} " +
            $"(same={metrics.InteractionSameLevelAllowed}, vertical={metrics.InteractionVerticalAllowed}), " +
            $"rejected={metrics.InteractionRejected}, remote-origins={metrics.InteractionRemoteOriginQueries}");
        shell.WriteLine(
            $"  interaction rejects: invalid={metrics.InteractionInvalidContextRejected}, " +
            $"map={metrics.InteractionDifferentMapRejected}, range={metrics.InteractionRangeRejected}, " +
            $"level={metrics.InteractionDifferentLevelRejected}, frame={metrics.InteractionFrameRejected}, " +
            $"trace={metrics.InteractionTraceRejected}");
        shell.WriteLine(
            $"  interaction physical: queries={metrics.InteractionPhysicalQueries}, " +
            $"allowed={metrics.InteractionPhysicalAllowed}, rejected={metrics.InteractionPhysicalRejected}");
        shell.WriteLine(
            $"  ballistic: attempts={metrics.BallisticRouteAttempts}, started={metrics.BallisticRoutesStarted}, " +
            $"completed={metrics.BallisticRoutesCompleted}, rejected={metrics.BallisticRoutesRejected}");
        shell.WriteLine(
            $"  ballistic crossings: success={metrics.BallisticCrossings}, " +
            $"closed={metrics.BallisticClosedBoundaries}, " +
            $"collision-cancellations={metrics.BallisticCollisionCancellations}, " +
            $"invalid-cancellations={metrics.BallisticInvalidCancellations}, " +
            $"contact-flushes={metrics.BallisticContactFlushes}");
        shell.WriteLine(
            $"  explosion topology: builds={metrics.ExplosionTopologyBuilds}, " +
            $"grid-layers={metrics.ExplosionGridLayers}, space-layers={metrics.ExplosionSpaceLayers}, " +
            $"tiles={metrics.ExplosionTiles}, " +
            $"avg={metrics.ExplosionAverageTopologyMilliseconds:0.000}ms, " +
            $"last={metrics.ExplosionLastTopologyMilliseconds:0.000}ms, " +
            $"max={metrics.ExplosionMaxTopologyMilliseconds:0.000}ms");
        shell.WriteLine(
            $"  explosion vertical: queries={metrics.ExplosionVerticalQueries}, " +
            $"traces={metrics.ExplosionVerticalTraces}, cache-hits={metrics.ExplosionVerticalCacheHits} " +
            $"({metrics.ExplosionVerticalCacheHitPercent:0.00}%), " +
            $"open={metrics.ExplosionVerticalOpen}, closed={metrics.ExplosionVerticalClosed}, " +
            $"rejected={metrics.ExplosionVerticalRejected}");
        shell.WriteLine(
            $"  explosion budgets: area={metrics.ExplosionAreaBudgetExhaustions}, " +
            $"iterations={metrics.ExplosionIterationBudgetExhaustions}, " +
            $"limits={explosion.MaxArea} tiles/{explosion.MaxIterations} iterations");
        shell.WriteLine(
            $"  explosion camera shake: candidates={metrics.ExplosionCameraShakeCandidates}, " +
            $"applied={metrics.ExplosionCameraShakesApplied}, " +
            $"world-z-rejected={metrics.ExplosionCameraShakesWorldZRejected}");
        shell.WriteLine(
            $"  atmos overlay: updates={metrics.AtmosOverlayUpdates}, " +
            $"tiles={metrics.AtmosOverlayInvalidatedTiles}, " +
            $"upper-tiles={metrics.AtmosOverlayInvalidatedUpperTiles}, " +
            $"upper-layers={metrics.AtmosOverlayUpperLayers}, " +
            $"changed-chunks={metrics.AtmosOverlayUpdatedChunks}, " +
            $"avg={metrics.AtmosOverlayAverageMilliseconds:0.000}ms, " +
            $"last={metrics.AtmosOverlayLastMilliseconds:0.000}ms, " +
            $"max={metrics.AtmosOverlayMaxMilliseconds:0.000}ms");
        var portalMetrics = soundPortals.Snapshot();
        shell.WriteLine(
            $"  sound portals: queries={portalMetrics.PortalQueries}, " +
            $"chunks={portalMetrics.CachedChunks}/{portalMetrics.CacheCapacity}, " +
            $"open/explicit={portalMetrics.CachedOpenPortals}/{portalMetrics.CachedExplicitPortals}, " +
            $"hit-rate={portalMetrics.CacheHitPercent:0.00}%, builds={portalMetrics.Builds}, " +
            $"budget={portalMetrics.ChunkBudgetExhaustions}/" +
            $"{portalMetrics.BuildBudgetExhaustions}/{portalMetrics.CandidateBudgetExhaustions}");
        var routeMetrics = soundRoutes.Snapshot();
        shell.WriteLine(
            $"  sound routes: queries={routeMetrics.Queries}, success={routeMetrics.Successes} " +
            $"(same={routeMetrics.SameLevelSuccesses}, vertical={routeMetrics.VerticalSuccesses}), " +
            $"no-route/medium/range={routeMetrics.NoPortalRoutes}/" +
            $"{routeMetrics.MediumBlockedRoutes}/{routeMetrics.OutOfRangeRoutes}, " +
            $"edges/samples={routeMetrics.EdgesEvaluated}/{routeMetrics.MediumSamples}, " +
            $"avg/last/max={routeMetrics.AverageRouteMilliseconds:0.000}/" +
            $"{routeMetrics.LastRouteMilliseconds:0.000}/{routeMetrics.MaxRouteMilliseconds:0.000}ms");
        var playbackMetrics = soundPlayback.Snapshot();
        shell.WriteLine(
            $"  sound playback: refreshes={playbackMetrics.Refreshes}, " +
            $"candidates/routes/authorized={playbackMetrics.AudioCandidates}/" +
            $"{playbackMetrics.RouteChecks}/{playbackMetrics.AuthorizedPresentations}, " +
            $"active sessions/presentations={playbackMetrics.ActiveSessions}/" +
            $"{playbackMetrics.ActivePresentations}, snapshots={playbackMetrics.SnapshotsSent}, " +
            $"budget routes/presentations={playbackMetrics.RouteBudgetExhaustions}/" +
            $"{playbackMetrics.PresentationBudgetExhaustions}, parent-fail={playbackMetrics.ParentDepthFailures}, " +
            $"avg/last/max={playbackMetrics.AverageRefreshMilliseconds:0.000}/" +
            $"{playbackMetrics.LastRefreshMilliseconds:0.000}/{playbackMetrics.MaxRefreshMilliseconds:0.000}ms");
        var traversalMetrics = traversalGraph.Snapshot();
        shell.WriteLine(
            $"  traversal graph: nodes/locations={traversalMetrics.Nodes}/{traversalMetrics.Locations}, " +
            $"revision={traversalMetrics.TopologyRevision}/{traversalMetrics.EnvironmentRevision}, " +
            $"location hit-rate={traversalMetrics.LocationHitPercent:0.00}%, " +
            $"connected q/visits/budget={traversalMetrics.ConnectedQueries}/" +
            $"{traversalMetrics.ConnectedVisits}/{traversalMetrics.ConnectedBudgetExhaustions}");
        shell.WriteLine(
            $"  traversal edges: queries/valid/closed/unsupported/invalid=" +
            $"{traversalMetrics.EdgeQueries}/{traversalMetrics.ValidEdges}/" +
            $"{traversalMetrics.ClosedEdges}/{traversalMetrics.UnsupportedEdges}/" +
            $"{traversalMetrics.InvalidEdges}, avg/last/max=" +
            $"{traversalMetrics.AverageQueryMilliseconds:0.000}/" +
            $"{traversalMetrics.LastQueryMilliseconds:0.000}/" +
            $"{traversalMetrics.MaxQueryMilliseconds:0.000}ms");
        shell.WriteLine(
            $"  trace budgets: vertical-crossings={trace.MaxVerticalCrossings}, " +
            $"tile-visits={trace.MaxTileVisits}, entity-hits={trace.MaxEntityHits}");
    }
}
