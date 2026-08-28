// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.Administration;
using Robust.Client.Graphics;
using Robust.Shared.Console;

namespace Content.Client.ZLevel.Commands;

[AnyCommand]
public sealed class ZLevelRenderMetricsCommand : IConsoleCommand
{
    [Dependency] private readonly IClyde _clyde = default!;

    public string Command => "zlevelrendermetrics";
    public string Description => "Shows local Z-level grid, lighting, and occlusion rendering metrics.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var stats = _clyde.ZLevelRenderStats;
        shell.WriteLine(
            $"grid layers={stats.GridLayersDrawn}, chunks={stats.GridChunksDrawn}, " +
            $"cache layers={stats.CachedGridChunkLayers}, hit/miss={stats.GridChunkCacheHits}/{stats.GridChunkCacheMisses} " +
            $"({stats.GridChunkCacheHitPercent:0.0}% hit)");
        shell.WriteLine(
            $"filtered by world Z: lights={stats.LightsRejectedByZ}, occluders={stats.OccludersRejectedByZ}");
    }
}
