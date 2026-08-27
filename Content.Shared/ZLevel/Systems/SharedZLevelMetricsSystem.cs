// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Collects process-local Z-level diagnostics without coupling subsystem behavior.
/// Counters are main-thread only and can be reset between benchmark runs.
/// </summary>
public sealed class SharedZLevelMetricsSystem : EntitySystem
{
    private long _boundaryQueries;
    private long _boundaryCacheHits;
    private long _boundaryCacheMisses;
    private long _boundaryInvalidations;
    private long _boundaryInvalidatedEntries;
    private long _boundaryEvictions;

    private long _visibilityEntityQueries;
    private long _visibilityTileQueries;
    private long _visibilitySameLevel;
    private long _visibilityEarlyRejections;
    private long _visibilityBoundaryChecks;

    private long _gravityQueries;
    private long _gravityCacheHits;
    private long _gravityCacheMisses;
    private long _gravityInvalidations;
    private long _gravityBuilds;
    private long _gravityBuildTiles;
    private long _gravityBuildSources;
    private long _gravityBuildTimestampTicks;
    private long _gravityLastBuildTimestampTicks;
    private long _gravityMaxBuildTimestampTicks;

    private long _pvsRefreshes;
    private long _pvsViewers;
    private long _pvsCandidates;
    private long _pvsVisible;
    private long _pvsCulled;
    private long _pvsVisibilityChecks;
    private long _pvsBudgetExhaustions;
    private long _pvsFailOpenCandidates;
    private long _pvsRefreshTimestampTicks;
    private long _pvsLastRefreshTimestampTicks;
    private long _pvsMaxRefreshTimestampTicks;

    private long _traceQueries;
    private long _traceCompleted;
    private long _traceClosedBoundaries;
    private long _traceInvalidCoordinates;
    private long _traceDifferentMaps;
    private long _traceFrameResolutionFailures;
    private long _traceBudgetExhaustions;
    private long _traceSegments;
    private long _traceTileVisits;
    private long _traceEntityHits;
    private long _traceBoundaryCrossings;
    private long _traceTimestampTicks;
    private long _traceLastTimestampTicks;
    private long _traceMaxTimestampTicks;

    public void RecordBoundaryQuery(bool cacheHit)
    {
        _boundaryQueries++;
        if (cacheHit)
            _boundaryCacheHits++;
        else
            _boundaryCacheMisses++;
    }

    public void RecordBoundaryInvalidation(bool removedEntry)
    {
        _boundaryInvalidations++;
        if (removedEntry)
            _boundaryInvalidatedEntries++;
    }

    public void RecordBoundaryInvalidatedEntries(int count)
    {
        _boundaryInvalidations++;
        _boundaryInvalidatedEntries += count;
    }

    public void RecordBoundaryEviction()
    {
        _boundaryEvictions++;
    }

    public void RecordVisibilityEntityQuery()
    {
        _visibilityEntityQueries++;
    }

    public void RecordVisibilityTileQuery()
    {
        _visibilityTileQueries++;
    }

    public void RecordVisibilitySameLevel()
    {
        _visibilitySameLevel++;
    }

    public void RecordVisibilityEarlyRejection()
    {
        _visibilityEarlyRejections++;
    }

    public void RecordVisibilityBoundaryCheck()
    {
        _visibilityBoundaryChecks++;
    }

    public void RecordGravityQuery()
    {
        _gravityQueries++;
    }

    public void RecordGravityCacheAccess(bool cacheHit)
    {
        if (cacheHit)
            _gravityCacheHits++;
        else
            _gravityCacheMisses++;
    }

    public void RecordGravityInvalidation()
    {
        _gravityInvalidations++;
    }

    public void RecordGravityBuild(int tileCount, int sourceCount, long elapsedTimestampTicks)
    {
        _gravityBuilds++;
        _gravityBuildTiles += tileCount;
        _gravityBuildSources += sourceCount;
        _gravityBuildTimestampTicks += elapsedTimestampTicks;
        _gravityLastBuildTimestampTicks = elapsedTimestampTicks;
        _gravityMaxBuildTimestampTicks = Math.Max(_gravityMaxBuildTimestampTicks, elapsedTimestampTicks);
    }

    public void RecordPvsRefresh(
        int viewerCount,
        int candidateCount,
        int visibleCount,
        int culledCount,
        int visibilityChecks,
        bool budgetExhausted,
        long elapsedTimestampTicks)
    {
        _pvsRefreshes++;
        _pvsViewers += viewerCount;
        _pvsCandidates += candidateCount;
        _pvsVisible += visibleCount;
        _pvsCulled += culledCount;
        _pvsVisibilityChecks += visibilityChecks;
        if (budgetExhausted)
        {
            _pvsBudgetExhaustions++;
            _pvsFailOpenCandidates += candidateCount;
        }
        _pvsRefreshTimestampTicks += elapsedTimestampTicks;
        _pvsLastRefreshTimestampTicks = elapsedTimestampTicks;
        _pvsMaxRefreshTimestampTicks = Math.Max(_pvsMaxRefreshTimestampTicks, elapsedTimestampTicks);
    }

    public void RecordTrace(
        ZLevelTraceTermination termination,
        int segmentCount,
        int tileVisitCount,
        int entityHitCount,
        int boundaryCrossingCount,
        long elapsedTimestampTicks)
    {
        _traceQueries++;
        switch (termination)
        {
            case ZLevelTraceTermination.Completed:
                _traceCompleted++;
                break;
            case ZLevelTraceTermination.ClosedBoundary:
                _traceClosedBoundaries++;
                break;
            case ZLevelTraceTermination.InvalidCoordinates:
                _traceInvalidCoordinates++;
                break;
            case ZLevelTraceTermination.DifferentMaps:
                _traceDifferentMaps++;
                break;
            case ZLevelTraceTermination.FrameResolutionRequired:
                _traceFrameResolutionFailures++;
                break;
            case ZLevelTraceTermination.IterationBudgetExceeded:
                _traceBudgetExhaustions++;
                break;
        }

        _traceSegments += segmentCount;
        _traceTileVisits += tileVisitCount;
        _traceEntityHits += entityHitCount;
        _traceBoundaryCrossings += boundaryCrossingCount;
        _traceTimestampTicks += elapsedTimestampTicks;
        _traceLastTimestampTicks = elapsedTimestampTicks;
        _traceMaxTimestampTicks = Math.Max(_traceMaxTimestampTicks, elapsedTimestampTicks);
    }

    public ZLevelMetricsSnapshot Snapshot()
    {
        return new ZLevelMetricsSnapshot(
            _boundaryQueries,
            _boundaryCacheHits,
            _boundaryCacheMisses,
            _boundaryInvalidations,
            _boundaryInvalidatedEntries,
            _boundaryEvictions,
            _visibilityEntityQueries,
            _visibilityTileQueries,
            _visibilitySameLevel,
            _visibilityEarlyRejections,
            _visibilityBoundaryChecks,
            _gravityQueries,
            _gravityCacheHits,
            _gravityCacheMisses,
            _gravityInvalidations,
            _gravityBuilds,
            _gravityBuildTiles,
            _gravityBuildSources,
            TimestampTicksToMilliseconds(_gravityBuildTimestampTicks),
            TimestampTicksToMilliseconds(_gravityLastBuildTimestampTicks),
            TimestampTicksToMilliseconds(_gravityMaxBuildTimestampTicks),
            _pvsRefreshes,
            _pvsViewers,
            _pvsCandidates,
            _pvsVisible,
            _pvsCulled,
            _pvsVisibilityChecks,
            _pvsBudgetExhaustions,
            _pvsFailOpenCandidates,
            TimestampTicksToMilliseconds(_pvsRefreshTimestampTicks),
            TimestampTicksToMilliseconds(_pvsLastRefreshTimestampTicks),
            TimestampTicksToMilliseconds(_pvsMaxRefreshTimestampTicks),
            _traceQueries,
            _traceCompleted,
            _traceClosedBoundaries,
            _traceInvalidCoordinates,
            _traceDifferentMaps,
            _traceFrameResolutionFailures,
            _traceBudgetExhaustions,
            _traceSegments,
            _traceTileVisits,
            _traceEntityHits,
            _traceBoundaryCrossings,
            TimestampTicksToMilliseconds(_traceTimestampTicks),
            TimestampTicksToMilliseconds(_traceLastTimestampTicks),
            TimestampTicksToMilliseconds(_traceMaxTimestampTicks));
    }

    public void ResetCounters()
    {
        _boundaryQueries = 0;
        _boundaryCacheHits = 0;
        _boundaryCacheMisses = 0;
        _boundaryInvalidations = 0;
        _boundaryInvalidatedEntries = 0;
        _boundaryEvictions = 0;
        _visibilityEntityQueries = 0;
        _visibilityTileQueries = 0;
        _visibilitySameLevel = 0;
        _visibilityEarlyRejections = 0;
        _visibilityBoundaryChecks = 0;
        _gravityQueries = 0;
        _gravityCacheHits = 0;
        _gravityCacheMisses = 0;
        _gravityInvalidations = 0;
        _gravityBuilds = 0;
        _gravityBuildTiles = 0;
        _gravityBuildSources = 0;
        _gravityBuildTimestampTicks = 0;
        _gravityLastBuildTimestampTicks = 0;
        _gravityMaxBuildTimestampTicks = 0;
        _pvsRefreshes = 0;
        _pvsViewers = 0;
        _pvsCandidates = 0;
        _pvsVisible = 0;
        _pvsCulled = 0;
        _pvsVisibilityChecks = 0;
        _pvsBudgetExhaustions = 0;
        _pvsFailOpenCandidates = 0;
        _pvsRefreshTimestampTicks = 0;
        _pvsLastRefreshTimestampTicks = 0;
        _pvsMaxRefreshTimestampTicks = 0;
        _traceQueries = 0;
        _traceCompleted = 0;
        _traceClosedBoundaries = 0;
        _traceInvalidCoordinates = 0;
        _traceDifferentMaps = 0;
        _traceFrameResolutionFailures = 0;
        _traceBudgetExhaustions = 0;
        _traceSegments = 0;
        _traceTileVisits = 0;
        _traceEntityHits = 0;
        _traceBoundaryCrossings = 0;
        _traceTimestampTicks = 0;
        _traceLastTimestampTicks = 0;
        _traceMaxTimestampTicks = 0;
    }

    private static double TimestampTicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}

public readonly record struct ZLevelMetricsSnapshot(
    long BoundaryQueries,
    long BoundaryCacheHits,
    long BoundaryCacheMisses,
    long BoundaryInvalidations,
    long BoundaryInvalidatedEntries,
    long BoundaryEvictions,
    long VisibilityEntityQueries,
    long VisibilityTileQueries,
    long VisibilitySameLevel,
    long VisibilityEarlyRejections,
    long VisibilityBoundaryChecks,
    long GravityQueries,
    long GravityCacheHits,
    long GravityCacheMisses,
    long GravityInvalidations,
    long GravityBuilds,
    long GravityBuildTiles,
    long GravityBuildSources,
    double GravityBuildMilliseconds,
    double GravityLastBuildMilliseconds,
    double GravityMaxBuildMilliseconds,
    long PvsRefreshes,
    long PvsViewers,
    long PvsCandidates,
    long PvsVisible,
    long PvsCulled,
    long PvsVisibilityChecks,
    long PvsBudgetExhaustions,
    long PvsFailOpenCandidates,
    double PvsRefreshMilliseconds,
    double PvsLastRefreshMilliseconds,
    double PvsMaxRefreshMilliseconds,
    long TraceQueries,
    long TraceCompleted,
    long TraceClosedBoundaries,
    long TraceInvalidCoordinates,
    long TraceDifferentMaps,
    long TraceFrameResolutionFailures,
    long TraceBudgetExhaustions,
    long TraceSegments,
    long TraceTileVisits,
    long TraceEntityHits,
    long TraceBoundaryCrossings,
    double TraceMilliseconds,
    double TraceLastMilliseconds,
    double TraceMaxMilliseconds)
{
    public double BoundaryCacheHitPercent => Percentage(BoundaryCacheHits, BoundaryQueries);
    public double GravityCacheHitPercent => Percentage(GravityCacheHits, GravityCacheHits + GravityCacheMisses);
    public double GravityAverageBuildMilliseconds => Average(GravityBuildMilliseconds, GravityBuilds);
    public double PvsAverageRefreshMilliseconds => Average(PvsRefreshMilliseconds, PvsRefreshes);
    public double TraceAverageMilliseconds => Average(TraceMilliseconds, TraceQueries);

    private static double Percentage(long value, long total)
    {
        return total == 0 ? 0d : value * 100d / total;
    }

    private static double Average(double value, long count)
    {
        return count == 0 ? 0d : value / count;
    }
}
