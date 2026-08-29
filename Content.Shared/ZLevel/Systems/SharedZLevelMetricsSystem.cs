// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;

namespace Content.Shared.ZLevel.Systems;

internal enum ZLevelInteractionDecision : byte
{
    SameLevelAllowed,
    VerticalAllowed,
    InvalidContextRejected,
    DifferentMapRejected,
    RangeRejected,
    DifferentLevelRejected,
    FrameRejected,
    TraceRejected,
}

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

    private long _skyExposureQueries;
    private long _skyExposureCacheHits;
    private long _skyExposureCacheMisses;
    private long _skyExposureBoundaryChecks;
    private long _skyExposureExposed;
    private long _skyExposureBlocked;
    private long _skyExposureInvalidQueries;
    private long _skyExposureBoundaryFailures;
    private long _skyExposureBudgetExhaustions;
    private long _skyExposureInvalidations;
    private long _skyExposureInvalidatedEntries;
    private long _skyExposureEvictions;

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

    private long _interactionQueries;
    private long _interactionRemoteOriginQueries;
    private long _interactionSameLevelAllowed;
    private long _interactionVerticalAllowed;
    private long _interactionInvalidContextRejected;
    private long _interactionDifferentMapRejected;
    private long _interactionRangeRejected;
    private long _interactionDifferentLevelRejected;
    private long _interactionFrameRejected;
    private long _interactionTraceRejected;
    private long _interactionPhysicalQueries;
    private long _interactionPhysicalRejected;

    private long _ballisticRouteAttempts;
    private long _ballisticRoutesStarted;
    private long _ballisticRoutesCompleted;
    private long _ballisticCrossings;
    private long _ballisticClosedBoundaries;
    private long _ballisticCollisionCancellations;
    private long _ballisticInvalidCancellations;
    private long _ballisticContactFlushes;

    private long _explosionTopologyBuilds;
    private long _explosionGridLayers;
    private long _explosionSpaceLayers;
    private long _explosionTiles;
    private long _explosionVerticalQueries;
    private long _explosionVerticalCacheHits;
    private long _explosionVerticalTraces;
    private long _explosionVerticalOpen;
    private long _explosionVerticalClosed;
    private long _explosionVerticalRejected;
    private long _explosionAreaBudgetExhaustions;
    private long _explosionIterationBudgetExhaustions;
    private long _explosionCameraShakeCandidates;
    private long _explosionCameraShakesApplied;
    private long _explosionCameraShakesWorldZRejected;
    private long _explosionTopologyTimestampTicks;
    private long _explosionLastTopologyTimestampTicks;
    private long _explosionMaxTopologyTimestampTicks;

    private long _atmosOverlayUpdates;
    private long _atmosOverlayInvalidatedTiles;
    private long _atmosOverlayInvalidatedUpperTiles;
    private long _atmosOverlayUpperLayers;
    private long _atmosOverlayUpdatedChunks;
    private long _atmosOverlayTimestampTicks;
    private long _atmosOverlayLastTimestampTicks;
    private long _atmosOverlayMaxTimestampTicks;

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

    public void RecordSkyExposureQuery(
        ZLevelSkyExposureTermination termination,
        bool? cacheHit,
        int boundaryChecks)
    {
        _skyExposureQueries++;
        if (cacheHit == true)
            _skyExposureCacheHits++;
        else if (cacheHit == false)
            _skyExposureCacheMisses++;

        _skyExposureBoundaryChecks += boundaryChecks;
        switch (termination)
        {
            case ZLevelSkyExposureTermination.Exposed:
                _skyExposureExposed++;
                break;
            case ZLevelSkyExposureTermination.ClosedBoundary:
                _skyExposureBlocked++;
                break;
            case ZLevelSkyExposureTermination.BoundaryResolutionFailed:
                _skyExposureBoundaryFailures++;
                break;
            case ZLevelSkyExposureTermination.BoundaryBudgetExceeded:
                _skyExposureBudgetExhaustions++;
                break;
            case ZLevelSkyExposureTermination.InvalidGrid:
            case ZLevelSkyExposureTermination.InvalidLevel:
            case ZLevelSkyExposureTermination.InvalidConfiguration:
                _skyExposureInvalidQueries++;
                break;
        }
    }

    public void RecordSkyExposureInvalidation(int invalidatedEntries)
    {
        _skyExposureInvalidations++;
        _skyExposureInvalidatedEntries += invalidatedEntries;
    }

    public void RecordSkyExposureEviction()
    {
        _skyExposureEvictions++;
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

    internal void RecordInteractionDecision(ZLevelInteractionDecision decision, bool remoteOrigin)
    {
        _interactionQueries++;
        if (remoteOrigin)
            _interactionRemoteOriginQueries++;

        switch (decision)
        {
            case ZLevelInteractionDecision.SameLevelAllowed:
                _interactionSameLevelAllowed++;
                break;
            case ZLevelInteractionDecision.VerticalAllowed:
                _interactionVerticalAllowed++;
                break;
            case ZLevelInteractionDecision.InvalidContextRejected:
                _interactionInvalidContextRejected++;
                break;
            case ZLevelInteractionDecision.DifferentMapRejected:
                _interactionDifferentMapRejected++;
                break;
            case ZLevelInteractionDecision.RangeRejected:
                _interactionRangeRejected++;
                break;
            case ZLevelInteractionDecision.DifferentLevelRejected:
                _interactionDifferentLevelRejected++;
                break;
            case ZLevelInteractionDecision.FrameRejected:
                _interactionFrameRejected++;
                break;
            case ZLevelInteractionDecision.TraceRejected:
                _interactionTraceRejected++;
                break;
            default:
                _interactionInvalidContextRejected++;
                break;
        }
    }

    internal void RecordPhysicalInteractionCheck(bool allowed)
    {
        _interactionPhysicalQueries++;
        if (!allowed)
            _interactionPhysicalRejected++;
    }

    public void RecordBallisticRouteAttempt()
    {
        _ballisticRouteAttempts++;
    }

    public void RecordBallisticRouteStarted()
    {
        _ballisticRoutesStarted++;
    }

    public void RecordBallisticRouteCompleted()
    {
        _ballisticRoutesCompleted++;
    }

    public void RecordBallisticCrossing()
    {
        _ballisticCrossings++;
    }

    public void RecordBallisticClosedBoundary()
    {
        _ballisticClosedBoundaries++;
    }

    public void RecordBallisticCollisionCancellation()
    {
        _ballisticCollisionCancellations++;
    }

    public void RecordBallisticInvalidCancellation()
    {
        _ballisticInvalidCancellations++;
    }

    public void RecordBallisticContactFlush()
    {
        _ballisticContactFlushes++;
    }

    public void RecordExplosionTopology(
        int gridLayers,
        int spaceLayers,
        int tiles,
        int verticalQueries,
        int verticalCacheHits,
        int verticalTraces,
        int verticalOpen,
        int verticalClosed,
        int verticalRejected,
        bool areaBudgetExhausted,
        bool iterationBudgetExhausted,
        long elapsedTimestampTicks)
    {
        _explosionTopologyBuilds++;
        _explosionGridLayers += gridLayers;
        _explosionSpaceLayers += spaceLayers;
        _explosionTiles += tiles;
        _explosionVerticalQueries += verticalQueries;
        _explosionVerticalCacheHits += verticalCacheHits;
        _explosionVerticalTraces += verticalTraces;
        _explosionVerticalOpen += verticalOpen;
        _explosionVerticalClosed += verticalClosed;
        _explosionVerticalRejected += verticalRejected;
        if (areaBudgetExhausted)
            _explosionAreaBudgetExhaustions++;
        if (iterationBudgetExhausted)
            _explosionIterationBudgetExhaustions++;
        _explosionTopologyTimestampTicks += elapsedTimestampTicks;
        _explosionLastTopologyTimestampTicks = elapsedTimestampTicks;
        _explosionMaxTopologyTimestampTicks = Math.Max(
            _explosionMaxTopologyTimestampTicks,
            elapsedTimestampTicks);
    }

    public void RecordExplosionCameraShake(int candidates, int applied, int worldZRejected)
    {
        _explosionCameraShakeCandidates += candidates;
        _explosionCameraShakesApplied += applied;
        _explosionCameraShakesWorldZRejected += worldZRejected;
    }

    public void RecordAtmosOverlayUpdate(
        int invalidatedTiles,
        int invalidatedUpperTiles,
        int upperLayers,
        int updatedChunks,
        long elapsedTimestampTicks)
    {
        _atmosOverlayUpdates++;
        _atmosOverlayInvalidatedTiles += invalidatedTiles;
        _atmosOverlayInvalidatedUpperTiles += invalidatedUpperTiles;
        _atmosOverlayUpperLayers += upperLayers;
        _atmosOverlayUpdatedChunks += updatedChunks;
        _atmosOverlayTimestampTicks += elapsedTimestampTicks;
        _atmosOverlayLastTimestampTicks = elapsedTimestampTicks;
        _atmosOverlayMaxTimestampTicks = Math.Max(_atmosOverlayMaxTimestampTicks, elapsedTimestampTicks);
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
            _skyExposureQueries,
            _skyExposureCacheHits,
            _skyExposureCacheMisses,
            _skyExposureBoundaryChecks,
            _skyExposureExposed,
            _skyExposureBlocked,
            _skyExposureInvalidQueries,
            _skyExposureBoundaryFailures,
            _skyExposureBudgetExhaustions,
            _skyExposureInvalidations,
            _skyExposureInvalidatedEntries,
            _skyExposureEvictions,
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
            TimestampTicksToMilliseconds(_traceMaxTimestampTicks),
            _interactionQueries,
            _interactionRemoteOriginQueries,
            _interactionSameLevelAllowed,
            _interactionVerticalAllowed,
            _interactionInvalidContextRejected,
            _interactionDifferentMapRejected,
            _interactionRangeRejected,
            _interactionDifferentLevelRejected,
            _interactionFrameRejected,
            _interactionTraceRejected,
            _interactionPhysicalQueries,
            _interactionPhysicalRejected,
            _ballisticRouteAttempts,
            _ballisticRoutesStarted,
            _ballisticRoutesCompleted,
            _ballisticCrossings,
            _ballisticClosedBoundaries,
            _ballisticCollisionCancellations,
            _ballisticInvalidCancellations,
            _ballisticContactFlushes,
            _explosionTopologyBuilds,
            _explosionGridLayers,
            _explosionSpaceLayers,
            _explosionTiles,
            _explosionVerticalQueries,
            _explosionVerticalCacheHits,
            _explosionVerticalTraces,
            _explosionVerticalOpen,
            _explosionVerticalClosed,
            _explosionVerticalRejected,
            _explosionAreaBudgetExhaustions,
            _explosionIterationBudgetExhaustions,
            _explosionCameraShakeCandidates,
            _explosionCameraShakesApplied,
            _explosionCameraShakesWorldZRejected,
            TimestampTicksToMilliseconds(_explosionTopologyTimestampTicks),
            TimestampTicksToMilliseconds(_explosionLastTopologyTimestampTicks),
            TimestampTicksToMilliseconds(_explosionMaxTopologyTimestampTicks),
            _atmosOverlayUpdates,
            _atmosOverlayInvalidatedTiles,
            _atmosOverlayInvalidatedUpperTiles,
            _atmosOverlayUpperLayers,
            _atmosOverlayUpdatedChunks,
            TimestampTicksToMilliseconds(_atmosOverlayTimestampTicks),
            TimestampTicksToMilliseconds(_atmosOverlayLastTimestampTicks),
            TimestampTicksToMilliseconds(_atmosOverlayMaxTimestampTicks));
    }

    public void ResetCounters()
    {
        _boundaryQueries = 0;
        _boundaryCacheHits = 0;
        _boundaryCacheMisses = 0;
        _boundaryInvalidations = 0;
        _boundaryInvalidatedEntries = 0;
        _boundaryEvictions = 0;
        _skyExposureQueries = 0;
        _skyExposureCacheHits = 0;
        _skyExposureCacheMisses = 0;
        _skyExposureBoundaryChecks = 0;
        _skyExposureExposed = 0;
        _skyExposureBlocked = 0;
        _skyExposureInvalidQueries = 0;
        _skyExposureBoundaryFailures = 0;
        _skyExposureBudgetExhaustions = 0;
        _skyExposureInvalidations = 0;
        _skyExposureInvalidatedEntries = 0;
        _skyExposureEvictions = 0;
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
        _interactionQueries = 0;
        _interactionRemoteOriginQueries = 0;
        _interactionSameLevelAllowed = 0;
        _interactionVerticalAllowed = 0;
        _interactionInvalidContextRejected = 0;
        _interactionDifferentMapRejected = 0;
        _interactionRangeRejected = 0;
        _interactionDifferentLevelRejected = 0;
        _interactionFrameRejected = 0;
        _interactionTraceRejected = 0;
        _interactionPhysicalQueries = 0;
        _interactionPhysicalRejected = 0;
        _ballisticRouteAttempts = 0;
        _ballisticRoutesStarted = 0;
        _ballisticRoutesCompleted = 0;
        _ballisticCrossings = 0;
        _ballisticClosedBoundaries = 0;
        _ballisticCollisionCancellations = 0;
        _ballisticInvalidCancellations = 0;
        _ballisticContactFlushes = 0;
        _explosionTopologyBuilds = 0;
        _explosionGridLayers = 0;
        _explosionSpaceLayers = 0;
        _explosionTiles = 0;
        _explosionVerticalQueries = 0;
        _explosionVerticalCacheHits = 0;
        _explosionVerticalTraces = 0;
        _explosionVerticalOpen = 0;
        _explosionVerticalClosed = 0;
        _explosionVerticalRejected = 0;
        _explosionAreaBudgetExhaustions = 0;
        _explosionIterationBudgetExhaustions = 0;
        _explosionCameraShakeCandidates = 0;
        _explosionCameraShakesApplied = 0;
        _explosionCameraShakesWorldZRejected = 0;
        _explosionTopologyTimestampTicks = 0;
        _explosionLastTopologyTimestampTicks = 0;
        _explosionMaxTopologyTimestampTicks = 0;
        _atmosOverlayUpdates = 0;
        _atmosOverlayInvalidatedTiles = 0;
        _atmosOverlayInvalidatedUpperTiles = 0;
        _atmosOverlayUpperLayers = 0;
        _atmosOverlayUpdatedChunks = 0;
        _atmosOverlayTimestampTicks = 0;
        _atmosOverlayLastTimestampTicks = 0;
        _atmosOverlayMaxTimestampTicks = 0;
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
    long SkyExposureQueries,
    long SkyExposureCacheHits,
    long SkyExposureCacheMisses,
    long SkyExposureBoundaryChecks,
    long SkyExposureExposed,
    long SkyExposureBlocked,
    long SkyExposureInvalidQueries,
    long SkyExposureBoundaryFailures,
    long SkyExposureBudgetExhaustions,
    long SkyExposureInvalidations,
    long SkyExposureInvalidatedEntries,
    long SkyExposureEvictions,
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
    double TraceMaxMilliseconds,
    long InteractionQueries,
    long InteractionRemoteOriginQueries,
    long InteractionSameLevelAllowed,
    long InteractionVerticalAllowed,
    long InteractionInvalidContextRejected,
    long InteractionDifferentMapRejected,
    long InteractionRangeRejected,
    long InteractionDifferentLevelRejected,
    long InteractionFrameRejected,
    long InteractionTraceRejected,
    long InteractionPhysicalQueries,
    long InteractionPhysicalRejected,
    long BallisticRouteAttempts,
    long BallisticRoutesStarted,
    long BallisticRoutesCompleted,
    long BallisticCrossings,
    long BallisticClosedBoundaries,
    long BallisticCollisionCancellations,
    long BallisticInvalidCancellations,
    long BallisticContactFlushes,
    long ExplosionTopologyBuilds,
    long ExplosionGridLayers,
    long ExplosionSpaceLayers,
    long ExplosionTiles,
    long ExplosionVerticalQueries,
    long ExplosionVerticalCacheHits,
    long ExplosionVerticalTraces,
    long ExplosionVerticalOpen,
    long ExplosionVerticalClosed,
    long ExplosionVerticalRejected,
    long ExplosionAreaBudgetExhaustions,
    long ExplosionIterationBudgetExhaustions,
    long ExplosionCameraShakeCandidates,
    long ExplosionCameraShakesApplied,
    long ExplosionCameraShakesWorldZRejected,
    double ExplosionTopologyMilliseconds,
    double ExplosionLastTopologyMilliseconds,
    double ExplosionMaxTopologyMilliseconds,
    long AtmosOverlayUpdates,
    long AtmosOverlayInvalidatedTiles,
    long AtmosOverlayInvalidatedUpperTiles,
    long AtmosOverlayUpperLayers,
    long AtmosOverlayUpdatedChunks,
    double AtmosOverlayMilliseconds,
    double AtmosOverlayLastMilliseconds,
    double AtmosOverlayMaxMilliseconds)
{
    public double BoundaryCacheHitPercent => Percentage(BoundaryCacheHits, BoundaryQueries);
    public double SkyExposureCacheHitPercent => Percentage(
        SkyExposureCacheHits,
        SkyExposureCacheHits + SkyExposureCacheMisses);
    public double GravityCacheHitPercent => Percentage(GravityCacheHits, GravityCacheHits + GravityCacheMisses);
    public double GravityAverageBuildMilliseconds => Average(GravityBuildMilliseconds, GravityBuilds);
    public double PvsAverageRefreshMilliseconds => Average(PvsRefreshMilliseconds, PvsRefreshes);
    public double TraceAverageMilliseconds => Average(TraceMilliseconds, TraceQueries);
    public long InteractionAllowed => InteractionSameLevelAllowed + InteractionVerticalAllowed;
    public long InteractionRejected => InteractionQueries - InteractionAllowed;
    public long InteractionPhysicalAllowed => InteractionPhysicalQueries - InteractionPhysicalRejected;
    public long BallisticRoutesRejected => BallisticRouteAttempts - BallisticRoutesStarted;
    public double ExplosionVerticalCacheHitPercent => Percentage(
        ExplosionVerticalCacheHits,
        ExplosionVerticalQueries);
    public double ExplosionAverageTopologyMilliseconds => Average(
        ExplosionTopologyMilliseconds,
        ExplosionTopologyBuilds);
    public double AtmosOverlayAverageMilliseconds => Average(AtmosOverlayMilliseconds, AtmosOverlayUpdates);

    private static double Percentage(long value, long total)
    {
        return total == 0 ? 0d : value * 100d / total;
    }

    private static double Average(double value, long count)
    {
        return count == 0 ? 0d : value / count;
    }
}
