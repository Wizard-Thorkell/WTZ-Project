// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Client.ZLevel;
using Content.Shared.Administration;
using Robust.Client.Graphics;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Client.ZLevel.Commands;

[AnyCommand]
public sealed class ZLevelRenderMetricsCommand : IConsoleCommand
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "zlevelrendermetrics";
    public string Description => "Shows or resets local Z-level rendering and sound-presentation metrics.";
    public string Help => $"Usage: {Command} [reset]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cache = _entityManager.System<ZLevelLightingCacheSystem>();
        var projection = _entityManager.System<ZLevelLightingProjectionSystem>();
        var sound = _entityManager.System<ZLevelSoundPresentationSystem>();
        var tileProjection = _entityManager.System<ZLevelTileProjectionSystem>();
        if (args.Length == 1 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            cache.ResetMetrics();
            projection.ResetMetrics();
            sound.ResetMetrics();
            tileProjection.ResetMetrics();
            shell.WriteLine("Reset local vertical rendering and sound counters.");
            return;
        }

        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var stats = _clyde.ZLevelRenderStats;
        shell.WriteLine(
            $"grid layers={stats.GridLayersDrawn}, chunks={stats.GridChunksDrawn}, " +
            $"cache layers={stats.CachedGridChunkLayers}, hit/miss={stats.GridChunkCacheHits}/{stats.GridChunkCacheMisses} " +
            $"({stats.GridChunkCacheHitPercent:0.0}% hit)");
        shell.WriteLine(
            $"filtered by world Z: lights={stats.LightsRejectedByZ}, occluders={stats.OccludersRejectedByZ}");

        var lighting = cache.Snapshot();
        shell.WriteLine(
            $"vertical apertures: chunks={lighting.CachedApertureChunks}/{lighting.ApertureCacheCapacity}, " +
            $"open tiles={lighting.CachedOpenApertureTiles}, evicted={lighting.ApertureEvictions}, " +
            $"hit/miss={lighting.ApertureCacheHits}/{lighting.ApertureCacheMisses} " +
            $"({lighting.ApertureCacheHitPercent:0.0}% hit), invalidated={lighting.ApertureInvalidatedChunks}");
        shell.WriteLine(
            $"vertical aperture builds: count={lighting.ApertureBuilds}, tiles={lighting.ApertureBuildTileChecks}, " +
            $"avg/last/max={lighting.ApertureAverageBuildMilliseconds:0.000}/" +
            $"{lighting.ApertureLastBuildMilliseconds:0.000}/{lighting.ApertureMaxBuildMilliseconds:0.000}ms");
        shell.WriteLine(
            $"vertical emitter index: queries={lighting.EmitterQueries}, accepted/candidates=" +
            $"{lighting.EmitterAccepted}/{lighting.EmitterCandidates}, z/bounds-rejected=" +
            $"{lighting.EmitterWorldZRejected}/{lighting.EmitterBoundsRejected}, avg/last/max=" +
            $"{lighting.EmitterAverageQueryMilliseconds:0.000}/{lighting.EmitterLastQueryMilliseconds:0.000}/" +
            $"{lighting.EmitterMaxQueryMilliseconds:0.000}ms, budget={lighting.EmitterCandidateBudgetExhaustions}");

        var projected = projection.Snapshot();
        shell.WriteLine(
            $"vertical projection: frames={projected.Frames}, input/projected/rejected=" +
            $"{projected.EmitterInputs}/{projected.EmittersProjected}/{projected.RadiusRejections}, " +
            $"current batches/runs={projected.CurrentBatches}/{projected.CurrentRuns}, " +
            $"tiles={projected.VisibleTiles}");
        shell.WriteLine(
            $"vertical projection build: chunks/layers={projected.StackChunks}/{projected.StackBoundaryLayers}, " +
            $"avg/last/max={projected.AverageBuildMilliseconds:0.000}/" +
            $"{projected.LastBuildMilliseconds:0.000}/{projected.MaxBuildMilliseconds:0.000}ms");
        shell.WriteLine(
            $"vertical projection frame budgets used/max: candidates=" +
            $"{projected.CurrentEmitterCandidatesUsed}/{projected.MaxEmitterCandidatesPerFrame}, emitters=" +
            $"{projected.CurrentEmittersUsed}/{projected.MaxEmittersPerFrame}, layers=" +
            $"{projected.CurrentApertureLayersUsed}/{projected.MaxApertureLayersPerFrame}, builds=" +
            $"{projected.CurrentApertureBuildsUsed}/{projected.MaxApertureBuildsPerFrame}, runs=" +
            $"{projected.CurrentRunsUsed}/{projected.MaxRunsPerFrame}");
        shell.WriteLine(
            $"vertical projection budget exhaustions: candidates={projected.CandidateBudgetExhaustions}, " +
            $"emitters={projected.EmitterBudgetExhaustions}, layers={projected.ApertureLayerBudgetExhaustions}, " +
            $"builds={projected.ApertureBuildBudgetExhaustions}, runs={projected.RunBudgetExhaustions}");
        shell.WriteLine(
            $"vertical projection shadows: current rows/groups/fallback=" +
            $"{projected.CurrentShadowRequests}/{projected.CurrentShadowFloorGroups}/" +
            $"{projected.CurrentShadowFallbacks}, frame used/max rows=" +
            $"{projected.CurrentShadowLightsUsed}/{projected.MaxShadowLightsPerFrame}, groups=" +
            $"{projected.CurrentShadowFloorGroupsUsed}/{projected.MaxShadowFloorGroupsPerFrame}, " +
            $"exhausted rows/groups={projected.ShadowLightBudgetExhaustions}/" +
            $"{projected.ShadowFloorGroupBudgetExhaustions}");
        shell.WriteLine(
            $"vertical projection shadow draw: atlases={projected.ShadowAtlasRenders}, " +
            $"planned/rendered rows={projected.ShadowLightsPlanned}/{projected.RenderShadowLights}, " +
            $"groups={projected.ShadowFloorGroupsPlanned}/{projected.RenderShadowFloorGroups}, " +
            $"unshadowed fallback={projected.ShadowFallbacks}");
        shell.WriteLine(
            $"vertical projection draw: frames={projected.RenderFrames}, batches/runs=" +
            $"{projected.RenderBatches}/{projected.RenderRuns}, vertices/calls=" +
            $"{projected.RenderVertices}/{projected.RenderDrawCalls}, avg/last/max=" +
            $"{projected.AverageRenderMilliseconds:0.000}/{projected.LastRenderMilliseconds:0.000}/" +
            $"{projected.MaxRenderMilliseconds:0.000}ms");

        var tiles = tileProjection.Snapshot();
        shell.WriteLine(
            $"vertical tiles: frames/preview={tiles.Frames}/{tiles.MappingFrames}, grids=" +
            $"{tiles.GridCandidates}, chunks candidate/complete/projected=" +
            $"{tiles.ChunkCandidates}/{tiles.ChunksCompleted}/{tiles.ChunksProjected}, " +
            $"visits/tiles={tiles.TileVisits}/{tiles.TilesProjected}");
        shell.WriteLine(
            $"vertical tile build: layers/builds={tiles.ApertureLayers}/{tiles.ApertureBuilds}, " +
            $"current batches/tiles={tiles.CurrentBatches}/{tiles.CurrentTiles}, avg/last/max=" +
            $"{tiles.AverageBuildMilliseconds:0.000}/{tiles.LastBuildMilliseconds:0.000}/" +
            $"{tiles.MaxBuildMilliseconds:0.000}ms");
        shell.WriteLine(
            $"vertical tile budget used/max chunk/layer/build/tile=" +
            $"{tiles.NormalBudget.CurrentChunksUsed}/{tiles.NormalBudget.MaxChunksPerFrame}," +
            $"{tiles.NormalBudget.CurrentApertureLayersUsed}/" +
            $"{tiles.NormalBudget.MaxApertureLayersPerFrame}," +
            $"{tiles.NormalBudget.CurrentApertureBuildsUsed}/" +
            $"{tiles.NormalBudget.MaxApertureBuildsPerFrame}," +
            $"{tiles.NormalBudget.CurrentTileVisitsUsed}/{tiles.NormalBudget.MaxTileVisitsPerFrame}; " +
            $"preview chunk/tile={tiles.MappingBudget.CurrentChunksUsed}/" +
            $"{tiles.MappingBudget.MaxChunksPerFrame}," +
            $"{tiles.MappingBudget.CurrentTileVisitsUsed}/{tiles.MappingBudget.MaxTileVisitsPerFrame}");
        shell.WriteLine(
            $"vertical tile budget exhaustions normal chunk/layer/build/tile=" +
            $"{tiles.NormalBudget.ChunkExhaustions}/{tiles.NormalBudget.ApertureLayerExhaustions}/" +
            $"{tiles.NormalBudget.ApertureBuildExhaustions}/{tiles.NormalBudget.TileVisitExhaustions}; " +
            $"preview chunk/tile={tiles.MappingBudget.ChunkExhaustions}/" +
            $"{tiles.MappingBudget.TileVisitExhaustions}");
        shell.WriteLine(
            $"vertical tile draw: frames/preview={tiles.RenderFrames}/{tiles.MappingRenderFrames}, " +
            $"batches/tiles={tiles.RenderBatches}/{tiles.RenderTiles}, vertices/calls=" +
            $"{tiles.RenderVertices}/{tiles.RenderDrawCalls}, avg/last/max=" +
            $"{tiles.AverageRenderMilliseconds:0.000}/{tiles.LastRenderMilliseconds:0.000}/" +
            $"{tiles.MaxRenderMilliseconds:0.000}ms");

        var soundMetrics = sound.Snapshot();
        shell.WriteLine(
            $"vertical sound snapshots: received/presentations/invalid=" +
            $"{soundMetrics.SnapshotsReceived}/{soundMetrics.SnapshotPresentationsReceived}/" +
            $"{soundMetrics.InvalidPresentations}, active={soundMetrics.ActivePresentations}");
        shell.WriteLine(
            $"vertical sound policy: frames={soundMetrics.Frames}, candidates/cross-floor=" +
            $"{soundMetrics.AudioCandidates}/{soundMetrics.CrossFloorCandidates}, " +
            $"current authorized/muted={soundMetrics.CurrentAuthorized}/{soundMetrics.CurrentMuted}, " +
            $"processed authorized/muted={soundMetrics.ProcessedAuthorized}/{soundMetrics.ProcessedMuted}, " +
            $"avg/last/max={soundMetrics.AverageBuildMilliseconds:0.000}/" +
            $"{soundMetrics.LastBuildMilliseconds:0.000}/{soundMetrics.MaxBuildMilliseconds:0.000}ms");
    }
}
