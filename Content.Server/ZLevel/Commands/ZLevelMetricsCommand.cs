// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Server.Administration;
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

        shell.WriteLine("Native Z-level metrics for this process since the last reset:");
        shell.WriteLine(
            $"  boundary: queries={metrics.BoundaryQueries}, hits={metrics.BoundaryCacheHits}, " +
            $"misses={metrics.BoundaryCacheMisses}, hit-rate={metrics.BoundaryCacheHitPercent:0.00}%, " +
            $"cache={boundaries.CachedBoundaryCount}/{SharedZLevelBoundarySystem.MaxCachedBoundaries}, " +
            $"invalidations={metrics.BoundaryInvalidations}, evictions={metrics.BoundaryEvictions}");
        shell.WriteLine(
            $"  visibility: entity={metrics.VisibilityEntityQueries}, tile={metrics.VisibilityTileQueries}, " +
            $"same-level={metrics.VisibilitySameLevel}, boundary-checks={metrics.VisibilityBoundaryChecks}, " +
            $"early-rejections={metrics.VisibilityEarlyRejections}");
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
            $"avg={metrics.PvsAverageRefreshMilliseconds:0.000}ms, " +
            $"last={metrics.PvsLastRefreshMilliseconds:0.000}ms, max={metrics.PvsMaxRefreshMilliseconds:0.000}ms");
    }
}
