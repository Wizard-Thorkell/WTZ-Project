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
    public string Description => "Shows or resets local Z-level grid, lighting, and occlusion rendering metrics.";
    public string Help => $"Usage: {Command} [reset]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var cache = _entityManager.System<ZLevelLightingCacheSystem>();
        if (args.Length == 1 && args[0].Equals("reset", StringComparison.OrdinalIgnoreCase))
        {
            cache.ResetMetrics();
            shell.WriteLine("Reset local vertical-lighting counters.");
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
            $"vertical apertures: chunks={lighting.CachedApertureChunks}, open tiles={lighting.CachedOpenApertureTiles}, " +
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
            $"{lighting.EmitterMaxQueryMilliseconds:0.000}ms");
    }
}
