using System.Diagnostics;
using System.Threading;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    private long _zLevelBreadcrumbBuilds;
    private long _zLevelFixtureCandidates;
    private long _zLevelFixtureFloorRejects;
    private long _zLevelPolyQueries;
    private long _zLevelPolyHits;
    private long _zLevelDifferentFloorRouteRejections;
    private long _zLevelBreadcrumbBuildTimestampTicks;
    private long _zLevelLastBreadcrumbBuildTimestampTicks;
    private long _zLevelMaxBreadcrumbBuildTimestampTicks;
    private long _zLevelBreadcrumbBuildAllocatedBytes;
    private long _zLevelLastBreadcrumbBuildAllocatedBytes;
    private long _zLevelMaxBreadcrumbBuildAllocatedBytes;

    public PathfindingZLevelMetricsSnapshot SnapshotZLevelMetrics()
    {
        var cachedChunks = 0;
        var pendingChunks = 0;
        var cachedFloors = new HashSet<(EntityUid GridUid, int LocalZ)>();
        var query = AllEntityQuery<GridPathfindingComponent>();

        while (query.MoveNext(out var uid, out var component))
        {
            cachedChunks += component.Chunks.Count;
            pendingChunks += component.DirtyChunks.Count;

            foreach (var key in component.Chunks.Keys)
            {
                cachedFloors.Add((uid, key.LocalZ));
            }
        }

        return new PathfindingZLevelMetricsSnapshot(
            cachedChunks,
            cachedFloors.Count,
            pendingChunks,
            Interlocked.Read(ref _zLevelBreadcrumbBuilds),
            Interlocked.Read(ref _zLevelFixtureCandidates),
            Interlocked.Read(ref _zLevelFixtureFloorRejects),
            Interlocked.Read(ref _zLevelPolyQueries),
            Interlocked.Read(ref _zLevelPolyHits),
            Interlocked.Read(ref _zLevelDifferentFloorRouteRejections),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelBreadcrumbBuildTimestampTicks)),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelLastBreadcrumbBuildTimestampTicks)),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelMaxBreadcrumbBuildTimestampTicks)),
            Interlocked.Read(ref _zLevelBreadcrumbBuildAllocatedBytes),
            Interlocked.Read(ref _zLevelLastBreadcrumbBuildAllocatedBytes),
            Interlocked.Read(ref _zLevelMaxBreadcrumbBuildAllocatedBytes));
    }

    public void ResetZLevelMetrics()
    {
        ResetZLevelRouteMetrics();
        Interlocked.Exchange(ref _zLevelBreadcrumbBuilds, 0);
        Interlocked.Exchange(ref _zLevelFixtureCandidates, 0);
        Interlocked.Exchange(ref _zLevelFixtureFloorRejects, 0);
        Interlocked.Exchange(ref _zLevelPolyQueries, 0);
        Interlocked.Exchange(ref _zLevelPolyHits, 0);
        Interlocked.Exchange(ref _zLevelDifferentFloorRouteRejections, 0);
        Interlocked.Exchange(ref _zLevelBreadcrumbBuildTimestampTicks, 0);
        Interlocked.Exchange(ref _zLevelLastBreadcrumbBuildTimestampTicks, 0);
        Interlocked.Exchange(ref _zLevelMaxBreadcrumbBuildTimestampTicks, 0);
        Interlocked.Exchange(ref _zLevelBreadcrumbBuildAllocatedBytes, 0);
        Interlocked.Exchange(ref _zLevelLastBreadcrumbBuildAllocatedBytes, 0);
        Interlocked.Exchange(ref _zLevelMaxBreadcrumbBuildAllocatedBytes, 0);
    }

    private void RecordZLevelBreadcrumbBuild(
        long fixtureCandidates,
        long fixtureFloorRejects,
        long elapsedTicks,
        long allocatedBytes)
    {
        Interlocked.Increment(ref _zLevelBreadcrumbBuilds);
        Interlocked.Add(ref _zLevelFixtureCandidates, fixtureCandidates);
        Interlocked.Add(ref _zLevelFixtureFloorRejects, fixtureFloorRejects);
        Interlocked.Add(ref _zLevelBreadcrumbBuildTimestampTicks, elapsedTicks);
        Interlocked.Exchange(ref _zLevelLastBreadcrumbBuildTimestampTicks, elapsedTicks);
        UpdateMaximum(ref _zLevelMaxBreadcrumbBuildTimestampTicks, elapsedTicks);
        Interlocked.Add(ref _zLevelBreadcrumbBuildAllocatedBytes, allocatedBytes);
        Interlocked.Exchange(ref _zLevelLastBreadcrumbBuildAllocatedBytes, allocatedBytes);
        UpdateMaximum(ref _zLevelMaxBreadcrumbBuildAllocatedBytes, allocatedBytes);
    }

    private void RecordZLevelPolyQuery(bool hit)
    {
        Interlocked.Increment(ref _zLevelPolyQueries);
        if (hit)
            Interlocked.Increment(ref _zLevelPolyHits);
    }

    private void RecordZLevelDifferentFloorRouteRejection()
    {
        Interlocked.Increment(ref _zLevelDifferentFloorRouteRejections);
    }

    private static double TimestampToMilliseconds(long timestampTicks)
    {
        return timestampTicks * 1000d / Stopwatch.Frequency;
    }

    private static void UpdateMaximum(ref long maximum, long candidate)
    {
        var current = Interlocked.Read(ref maximum);
        while (candidate > current)
        {
            var observed = Interlocked.CompareExchange(ref maximum, candidate, current);
            if (observed == current)
                return;

            current = observed;
        }
    }
}

public readonly record struct PathfindingZLevelMetricsSnapshot(
    int CachedChunks,
    int CachedFloors,
    int PendingChunks,
    long BreadcrumbBuilds,
    long FixtureCandidates,
    long FixtureFloorRejects,
    long PolyQueries,
    long PolyHits,
    long DifferentFloorRouteRejections,
    double TotalBreadcrumbBuildMilliseconds,
    double LastBreadcrumbBuildMilliseconds,
    double MaxBreadcrumbBuildMilliseconds,
    long TotalBreadcrumbBuildAllocatedBytes,
    long LastBreadcrumbBuildAllocatedBytes,
    long MaxBreadcrumbBuildAllocatedBytes)
{
    public double FixtureFloorRejectPercent => FixtureCandidates == 0
        ? 0d
        : FixtureFloorRejects * 100d / FixtureCandidates;

    public double PolyHitPercent => PolyQueries == 0
        ? 0d
        : PolyHits * 100d / PolyQueries;

    public double AverageBreadcrumbBuildMilliseconds => BreadcrumbBuilds == 0
        ? 0d
        : TotalBreadcrumbBuildMilliseconds / BreadcrumbBuilds;

    public double AverageBreadcrumbBuildAllocatedBytes => BreadcrumbBuilds == 0
        ? 0d
        : TotalBreadcrumbBuildAllocatedBytes / (double) BreadcrumbBuilds;
}
