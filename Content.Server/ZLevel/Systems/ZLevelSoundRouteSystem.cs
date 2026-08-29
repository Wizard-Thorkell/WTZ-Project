// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Resolves one authoritative, bounded acoustic route across adjacent floors.
/// Horizontal audio remains native; this system only selects vertical portals
/// and computes the path/transmission presented to later listener policy.
/// </summary>
public sealed class ZLevelSoundRouteSystem : EntitySystem
{
    public const int MaximumCrossings = 64;
    public const int MaximumPortalChunks = 4_096;
    public const int MaximumPortalBuilds = 4_096;
    public const int MaximumPortalCandidates = 65_536;
    public const int MaximumEdges = 1_000_000;
    public const int MaximumMediumSamples = 131_072;
    public const float MaximumRouteDistance = 4_096f;

    private const float CostEpsilon = 0.0001f;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelSoundPortalSystem _portals = default!;

    private readonly List<ZLevelSoundPortal> _portalScratch = new();
    private readonly List<RouteState> _stateScratch = new();
    private readonly List<ZLevelSoundPortal> _routeReverseScratch = new();
    private readonly List<int> _layerStarts = new();
    private readonly List<int> _layerCounts = new();
    private readonly Dictionary<ZLevelTileIndices, float> _pressureScratch = new();
    private int _portalCandidatesVisitedScratch;

    private int _maxCrossings = 8;
    private int _maxPortalChunks = 64;
    private int _maxPortalBuilds = 16;
    private int _maxPortalCandidates = 2_048;
    private int _maxEdges = 32_768;
    private int _maxMediumSamples = 4_096;

    private long _queries;
    private long _successes;
    private long _sameLevelSuccesses;
    private long _verticalSuccesses;
    private long _invalidQueries;
    private long _noPortalRoutes;
    private long _mediumBlockedRoutes;
    private long _outOfRangeRoutes;
    private long _crossingLimitExhaustions;
    private long _portalChunkBudgetExhaustions;
    private long _portalBuildBudgetExhaustions;
    private long _portalCandidateBudgetExhaustions;
    private long _edgeBudgetExhaustions;
    private long _mediumSampleBudgetExhaustions;
    private long _portalCandidates;
    private long _portalsReturned;
    private long _crossings;
    private long _edgesEvaluated;
    private long _mediumSamples;
    private long _routeTimestampTicks;
    private long _lastRouteTimestampTicks;
    private long _maxRouteTimestampTicks;

    public int MaxCrossings => _maxCrossings;
    public int MaxPortalChunks => _maxPortalChunks;
    public int MaxPortalBuilds => _maxPortalBuilds;
    public int MaxPortalCandidates => _maxPortalCandidates;
    public int MaxEdges => _maxEdges;
    public int MaxMediumSamples => _maxMediumSamples;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxCrossings,
            value => _maxCrossings = Math.Clamp(value, 0, MaximumCrossings),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxPortalChunks,
            value => _maxPortalChunks = Math.Clamp(value, 0, MaximumPortalChunks),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxPortalBuilds,
            value => _maxPortalBuilds = Math.Clamp(value, 0, MaximumPortalBuilds),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxPortalCandidates,
            value => _maxPortalCandidates = Math.Clamp(value, 0, MaximumPortalCandidates),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxEdges,
            value => _maxEdges = Math.Clamp(value, 0, MaximumEdges),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundRouteMaxMediumSamples,
            value => _maxMediumSamples = Math.Clamp(value, 0, MaximumMediumSamples),
            true);
    }

    public ZLevelSoundRouteOptions CreateDefaultOptions(
        float maxDistance,
        ZLevelSoundMediumMode mediumMode = ZLevelSoundMediumMode.RequirePressure)
    {
        return ZLevelSoundRouteOptions.Default(maxDistance, _maxCrossings, mediumMode);
    }

    public ZLevelSoundRouteBudget CreateDefaultBudget()
    {
        return new ZLevelSoundRouteBudget(
            new ZLevelSoundPortalQueryBudget(
                _maxPortalChunks,
                _maxPortalBuilds,
                _maxPortalCandidates),
            _maxEdges,
            _maxMediumSamples);
    }

    public ZLevelSoundRouteResult FindRoute(
        Entity<MapGridComponent> grid,
        ZLevelSoundRouteEndpoint source,
        ZLevelSoundRouteEndpoint listener,
        float maxDistance,
        List<ZLevelSoundPortal> results,
        ZLevelSoundMediumMode mediumMode = ZLevelSoundMediumMode.RequirePressure)
    {
        var options = CreateDefaultOptions(maxDistance, mediumMode);
        var budget = CreateDefaultBudget();
        return FindRoute(grid, source, listener, options, results, ref budget);
    }

    public ZLevelSoundRouteResult FindRoute(
        Entity<MapGridComponent> grid,
        ZLevelSoundRouteEndpoint source,
        ZLevelSoundRouteEndpoint listener,
        ZLevelSoundRouteOptions options,
        List<ZLevelSoundPortal> results,
        ref ZLevelSoundRouteBudget budget)
    {
        var started = Stopwatch.GetTimestamp();
        var initialCount = results.Count;
        var edgesEvaluated = 0;
        var mediumSamples = 0;
        _portalScratch.Clear();
        _stateScratch.Clear();
        _routeReverseScratch.Clear();
        _layerStarts.Clear();
        _layerCounts.Clear();
        _pressureScratch.Clear();
        _portalCandidatesVisitedScratch = 0;

        if (grid.Comp.Deleted ||
            !TryComp(grid.Owner, out TransformComponent? gridTransform) ||
            !IsValidEndpoint(source) ||
            !IsValidEndpoint(listener) ||
            !IsValidOptions(options))
        {
            return Finish(
                ZLevelSoundRouteStatus.Invalid,
                ZLevelSoundPortalQueryStatus.Invalid,
                initialCount,
                0,
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        if (source.GridUid != grid.Owner || listener.GridUid != grid.Owner)
        {
            return Finish(
                ZLevelSoundRouteStatus.DifferentGrid,
                ZLevelSoundPortalQueryStatus.Invalid,
                initialCount,
                0,
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        var directDistance = Vector2.Distance(source.LocalPosition, listener.LocalPosition);
        if (source.LocalZ == listener.LocalZ)
        {
            var status = directDistance <= options.MaxDistance
                ? ZLevelSoundRouteStatus.Success
                : ZLevelSoundRouteStatus.OutOfRange;
            return Finish(
                status,
                ZLevelSoundPortalQueryStatus.Success,
                initialCount,
                0,
                edgesEvaluated,
                mediumSamples,
                directDistance,
                directDistance,
                status == ZLevelSoundRouteStatus.Success ? 1f : 0f,
                started,
                results);
        }

        var crossingLong = Math.Abs((long) listener.LocalZ - source.LocalZ);
        if (crossingLong > options.MaxCrossings || crossingLong > MaximumCrossings)
        {
            return Finish(
                ZLevelSoundRouteStatus.CrossingLimitExceeded,
                ZLevelSoundPortalQueryStatus.Success,
                initialCount,
                (int) Math.Min(crossingLong, int.MaxValue),
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        var crossingCount = (int) crossingLong;
        var verticalDistance = crossingCount * options.VerticalDistance;
        if (verticalDistance > options.MaxDistance)
        {
            return Finish(
                ZLevelSoundRouteStatus.OutOfRange,
                ZLevelSoundPortalQueryStatus.Success,
                initialCount,
                crossingCount,
                edgesEvaluated,
                mediumSamples,
                verticalDistance,
                verticalDistance,
                0f,
                started,
                results);
        }

        var sourceTile = LocalToTile(grid, source.LocalPosition);
        var listenerTile = LocalToTile(grid, listener.LocalPosition);
        var horizontalAllowance = options.MaxDistance - verticalDistance;
        var padding = (int) MathF.Ceiling(horizontalAllowance);
        var minimumTile = new Vector2i(
            SaturatingSubtract(Math.Min(sourceTile.X, listenerTile.X), padding),
            SaturatingSubtract(Math.Min(sourceTile.Y, listenerTile.Y), padding));
        var maximumTile = new Vector2i(
            SaturatingAdd(Math.Max(sourceTile.X, listenerTile.X), padding),
            SaturatingAdd(Math.Max(sourceTile.Y, listenerTile.Y), padding));
        var minimumLowerZ = Math.Min(source.LocalZ, listener.LocalZ);
        var maximumLowerZ = Math.Max(source.LocalZ, listener.LocalZ) - 1;

        var portalQuery = _portals.QueryPortals(
            grid,
            minimumTile,
            maximumTile,
            minimumLowerZ,
            maximumLowerZ,
            _portalScratch,
            ref budget.PortalBudget);
        _portalCandidatesVisitedScratch = portalQuery.CandidatesVisited;
        if (!portalQuery.Succeeded)
        {
            return Finish(
                MapPortalFailure(portalQuery.Status),
                portalQuery.Status,
                initialCount,
                crossingCount,
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        for (var i = 0; i < crossingCount; i++)
        {
            _layerStarts.Add(-1);
            _layerCounts.Add(0);
        }

        for (var portalIndex = 0; portalIndex < _portalScratch.Count; portalIndex++)
        {
            var layer = _portalScratch[portalIndex].LowerLocalZ - minimumLowerZ;
            if ((uint) layer >= (uint) crossingCount)
                continue;

            if (_layerStarts[layer] < 0)
                _layerStarts[layer] = portalIndex;
            _layerCounts[layer]++;
        }

        for (var layer = 0; layer < crossingCount; layer++)
        {
            if (_layerStarts[layer] >= 0 && _layerCounts[layer] > 0)
                continue;

            return Finish(
                ZLevelSoundRouteStatus.NoPortalRoute,
                portalQuery.Status,
                initialCount,
                crossingCount,
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        if (options.MediumMode == ZLevelSoundMediumMode.RequirePressure)
        {
            if (!TryGetPressure(
                    grid,
                    gridTransform,
                    new ZLevelTileIndices(sourceTile.X, sourceTile.Y, source.LocalZ),
                    ref budget,
                    ref mediumSamples,
                    out var sourcePressure) ||
                !TryGetPressure(
                    grid,
                    gridTransform,
                    new ZLevelTileIndices(listenerTile.X, listenerTile.Y, listener.LocalZ),
                    ref budget,
                    ref mediumSamples,
                    out var listenerPressure))
            {
                return Finish(
                    ZLevelSoundRouteStatus.MediumSampleBudgetExceeded,
                    portalQuery.Status,
                    initialCount,
                    crossingCount,
                    edgesEvaluated,
                    mediumSamples,
                    0f,
                    0f,
                    0f,
                    started,
                    results);
            }

            if (sourcePressure < options.MinimumPressure || listenerPressure < options.MinimumPressure)
            {
                return Finish(
                    ZLevelSoundRouteStatus.MediumBlocked,
                    portalQuery.Status,
                    initialCount,
                    crossingCount,
                    edgesEvaluated,
                    mediumSamples,
                    0f,
                    0f,
                    0f,
                    started,
                    results);
            }
        }

        var direction = Math.Sign(listener.LocalZ - source.LocalZ);
        var previousStateStart = -1;
        var previousStateCount = 0;

        for (var routeLayer = 0; routeLayer < crossingCount; routeLayer++)
        {
            var lowerZ = direction > 0
                ? source.LocalZ + routeLayer
                : source.LocalZ - routeLayer - 1;
            var layer = lowerZ - minimumLowerZ;
            var portalStart = _layerStarts[layer];
            var portalCount = _layerCounts[layer];
            if (portalStart < 0 || portalCount == 0)
            {
                return Finish(
                    ZLevelSoundRouteStatus.NoPortalRoute,
                    portalQuery.Status,
                    initialCount,
                    crossingCount,
                    edgesEvaluated,
                    mediumSamples,
                    0f,
                    0f,
                    0f,
                    started,
                    results);
            }

            var currentStateStart = _stateScratch.Count;
            var unblockedCandidates = 0;
            for (var i = 0; i < portalCount; i++)
            {
                var portalIndex = portalStart + i;
                var portal = _portalScratch[portalIndex];
                var mediumResult = TryGetPortalTransmission(
                    grid,
                    gridTransform,
                    portal,
                    options,
                    ref budget,
                    ref mediumSamples,
                    out var stepTransmission);
                if (mediumResult == MediumResult.BudgetExceeded)
                {
                    return Finish(
                        ZLevelSoundRouteStatus.MediumSampleBudgetExceeded,
                        portalQuery.Status,
                        initialCount,
                        crossingCount,
                        edgesEvaluated,
                        mediumSamples,
                        0f,
                        0f,
                        0f,
                        started,
                        results);
                }

                if (mediumResult == MediumResult.Blocked)
                    continue;

                unblockedCandidates++;
                var stepLoss = TransmissionLossDistance(stepTransmission, options.TransmissionLossDistanceScale);
                if (routeLayer == 0)
                {
                    if (!TryChargeEdge(ref budget, ref edgesEvaluated))
                    {
                        return Finish(
                            ZLevelSoundRouteStatus.EdgeBudgetExceeded,
                            portalQuery.Status,
                            initialCount,
                            crossingCount,
                            edgesEvaluated,
                            mediumSamples,
                            0f,
                            0f,
                            0f,
                            started,
                            results);
                    }

                    var distance = Vector2.Distance(source.LocalPosition, portal.LocalPosition) +
                                   options.VerticalDistance;
                    var effectiveDistance = distance + stepLoss;
                    if (stepTransmission < options.MinimumTransmission ||
                        effectiveDistance > options.MaxDistance)
                    {
                        continue;
                    }

                    _stateScratch.Add(new RouteState(
                        portalIndex,
                        -1,
                        distance,
                        effectiveDistance,
                        stepTransmission));
                    continue;
                }

                var bestPrevious = -1;
                var bestDistance = 0f;
                var bestEffectiveDistance = float.PositiveInfinity;
                var bestTransmission = 0f;
                for (var previous = 0; previous < previousStateCount; previous++)
                {
                    if (!TryChargeEdge(ref budget, ref edgesEvaluated))
                    {
                        return Finish(
                            ZLevelSoundRouteStatus.EdgeBudgetExceeded,
                            portalQuery.Status,
                            initialCount,
                            crossingCount,
                            edgesEvaluated,
                            mediumSamples,
                            0f,
                            0f,
                            0f,
                            started,
                            results);
                    }

                    var previousStateIndex = previousStateStart + previous;
                    var previousState = _stateScratch[previousStateIndex];
                    var previousPortal = _portalScratch[previousState.PortalIndex];
                    var horizontalDistance = Vector2.Distance(
                        previousPortal.LocalPosition,
                        portal.LocalPosition);
                    var distance = previousState.Distance + horizontalDistance + options.VerticalDistance;
                    var effectiveDistance = previousState.EffectiveDistance +
                                            horizontalDistance +
                                            options.VerticalDistance +
                                            stepLoss;
                    var transmission = previousState.Transmission * stepTransmission;
                    if (transmission < options.MinimumTransmission ||
                        effectiveDistance > options.MaxDistance ||
                        effectiveDistance >= bestEffectiveDistance - CostEpsilon)
                    {
                        continue;
                    }

                    bestPrevious = previousStateIndex;
                    bestDistance = distance;
                    bestEffectiveDistance = effectiveDistance;
                    bestTransmission = transmission;
                }

                if (bestPrevious >= 0)
                {
                    _stateScratch.Add(new RouteState(
                        portalIndex,
                        bestPrevious,
                        bestDistance,
                        bestEffectiveDistance,
                        bestTransmission));
                }
            }

            var currentStateCount = _stateScratch.Count - currentStateStart;
            if (currentStateCount == 0)
            {
                var status = unblockedCandidates == 0 &&
                             options.MediumMode == ZLevelSoundMediumMode.RequirePressure
                    ? ZLevelSoundRouteStatus.MediumBlocked
                    : ZLevelSoundRouteStatus.OutOfRange;
                return Finish(
                    status,
                    portalQuery.Status,
                    initialCount,
                    crossingCount,
                    edgesEvaluated,
                    mediumSamples,
                    0f,
                    0f,
                    0f,
                    started,
                    results);
            }

            previousStateStart = currentStateStart;
            previousStateCount = currentStateCount;
        }

        var finalStateIndex = -1;
        var finalDistance = 0f;
        var finalEffectiveDistance = float.PositiveInfinity;
        var finalTransmission = 0f;
        for (var i = 0; i < previousStateCount; i++)
        {
            if (!TryChargeEdge(ref budget, ref edgesEvaluated))
            {
                return Finish(
                    ZLevelSoundRouteStatus.EdgeBudgetExceeded,
                    portalQuery.Status,
                    initialCount,
                    crossingCount,
                    edgesEvaluated,
                    mediumSamples,
                    0f,
                    0f,
                    0f,
                    started,
                    results);
            }

            var stateIndex = previousStateStart + i;
            var state = _stateScratch[stateIndex];
            var portal = _portalScratch[state.PortalIndex];
            var horizontalDistance = Vector2.Distance(portal.LocalPosition, listener.LocalPosition);
            var distance = state.Distance + horizontalDistance;
            var effectiveDistance = state.EffectiveDistance + horizontalDistance;
            if (effectiveDistance > options.MaxDistance ||
                effectiveDistance >= finalEffectiveDistance - CostEpsilon)
            {
                continue;
            }

            finalStateIndex = stateIndex;
            finalDistance = distance;
            finalEffectiveDistance = effectiveDistance;
            finalTransmission = state.Transmission;
        }

        if (finalStateIndex < 0)
        {
            return Finish(
                ZLevelSoundRouteStatus.OutOfRange,
                portalQuery.Status,
                initialCount,
                crossingCount,
                edgesEvaluated,
                mediumSamples,
                0f,
                0f,
                0f,
                started,
                results);
        }

        for (var stateIndex = finalStateIndex; stateIndex >= 0;)
        {
            var state = _stateScratch[stateIndex];
            _routeReverseScratch.Add(_portalScratch[state.PortalIndex]);
            stateIndex = state.PreviousStateIndex;
        }

        for (var i = _routeReverseScratch.Count - 1; i >= 0; i--)
        {
            results.Add(_routeReverseScratch[i]);
        }

        return Finish(
            ZLevelSoundRouteStatus.Success,
            portalQuery.Status,
            initialCount,
            crossingCount,
            edgesEvaluated,
            mediumSamples,
            finalDistance,
            finalEffectiveDistance,
            finalTransmission,
            started,
            results);
    }

    public void ResetMetrics()
    {
        _queries = 0;
        _successes = 0;
        _sameLevelSuccesses = 0;
        _verticalSuccesses = 0;
        _invalidQueries = 0;
        _noPortalRoutes = 0;
        _mediumBlockedRoutes = 0;
        _outOfRangeRoutes = 0;
        _crossingLimitExhaustions = 0;
        _portalChunkBudgetExhaustions = 0;
        _portalBuildBudgetExhaustions = 0;
        _portalCandidateBudgetExhaustions = 0;
        _edgeBudgetExhaustions = 0;
        _mediumSampleBudgetExhaustions = 0;
        _portalCandidates = 0;
        _portalsReturned = 0;
        _crossings = 0;
        _edgesEvaluated = 0;
        _mediumSamples = 0;
        _routeTimestampTicks = 0;
        _lastRouteTimestampTicks = 0;
        _maxRouteTimestampTicks = 0;
    }

    public ZLevelSoundRouteMetrics Snapshot()
    {
        return new ZLevelSoundRouteMetrics(
            _queries,
            _successes,
            _sameLevelSuccesses,
            _verticalSuccesses,
            _invalidQueries,
            _noPortalRoutes,
            _mediumBlockedRoutes,
            _outOfRangeRoutes,
            _crossingLimitExhaustions,
            _portalChunkBudgetExhaustions,
            _portalBuildBudgetExhaustions,
            _portalCandidateBudgetExhaustions,
            _edgeBudgetExhaustions,
            _mediumSampleBudgetExhaustions,
            _portalCandidates,
            _portalsReturned,
            _crossings,
            _edgesEvaluated,
            _mediumSamples,
            _routeTimestampTicks,
            _lastRouteTimestampTicks,
            _maxRouteTimestampTicks);
    }

    private ZLevelSoundRouteResult Finish(
        ZLevelSoundRouteStatus status,
        ZLevelSoundPortalQueryStatus portalStatus,
        int initialCount,
        int crossings,
        int edgesEvaluated,
        int mediumSamples,
        float distance,
        float effectiveDistance,
        float transmission,
        long started,
        List<ZLevelSoundPortal> results)
    {
        var added = results.Count - initialCount;
        if (status != ZLevelSoundRouteStatus.Success && added > 0)
        {
            results.RemoveRange(initialCount, added);
            added = 0;
        }

        var elapsed = Stopwatch.GetTimestamp() - started;
        _queries++;
        _portalCandidates += _portalCandidatesVisitedScratch;
        _portalsReturned += added;
        _edgesEvaluated += edgesEvaluated;
        _mediumSamples += mediumSamples;
        _routeTimestampTicks += elapsed;
        _lastRouteTimestampTicks = elapsed;
        _maxRouteTimestampTicks = Math.Max(_maxRouteTimestampTicks, elapsed);

        switch (status)
        {
            case ZLevelSoundRouteStatus.Success:
                _successes++;
                _crossings += crossings;
                if (crossings == 0)
                    _sameLevelSuccesses++;
                else
                    _verticalSuccesses++;
                break;
            case ZLevelSoundRouteStatus.Invalid:
            case ZLevelSoundRouteStatus.DifferentGrid:
                _invalidQueries++;
                break;
            case ZLevelSoundRouteStatus.CrossingLimitExceeded:
                _crossingLimitExhaustions++;
                break;
            case ZLevelSoundRouteStatus.PortalChunkBudgetExceeded:
                _portalChunkBudgetExhaustions++;
                break;
            case ZLevelSoundRouteStatus.PortalBuildBudgetExceeded:
                _portalBuildBudgetExhaustions++;
                break;
            case ZLevelSoundRouteStatus.PortalCandidateBudgetExceeded:
                _portalCandidateBudgetExhaustions++;
                break;
            case ZLevelSoundRouteStatus.EdgeBudgetExceeded:
                _edgeBudgetExhaustions++;
                break;
            case ZLevelSoundRouteStatus.MediumSampleBudgetExceeded:
                _mediumSampleBudgetExhaustions++;
                break;
            case ZLevelSoundRouteStatus.NoPortalRoute:
                _noPortalRoutes++;
                break;
            case ZLevelSoundRouteStatus.MediumBlocked:
                _mediumBlockedRoutes++;
                break;
            case ZLevelSoundRouteStatus.OutOfRange:
                _outOfRangeRoutes++;
                break;
        }

        return new ZLevelSoundRouteResult(
            status,
            portalStatus,
            added,
            crossings,
            _portalCandidatesVisitedScratch,
            edgesEvaluated,
            mediumSamples,
            distance,
            effectiveDistance,
            transmission);
    }

    private MediumResult TryGetPortalTransmission(
        Entity<MapGridComponent> grid,
        TransformComponent gridTransform,
        ZLevelSoundPortal portal,
        ZLevelSoundRouteOptions options,
        ref ZLevelSoundRouteBudget budget,
        ref int mediumSamples,
        out float transmission)
    {
        transmission = portal.Kind == ZLevelSoundPortalKind.ExplicitOpening
            ? options.ExplicitPortalTransmission
            : options.DefaultPortalTransmission;
        if (options.MediumMode == ZLevelSoundMediumMode.Ignore)
            return MediumResult.Open;

        if (!TryGetPressure(
                grid,
                gridTransform,
                new ZLevelTileIndices(portal.Tile.X, portal.Tile.Y, portal.LowerLocalZ),
                ref budget,
                ref mediumSamples,
                out var lowerPressure) ||
            !TryGetPressure(
                grid,
                gridTransform,
                new ZLevelTileIndices(portal.Tile.X, portal.Tile.Y, portal.UpperLocalZ),
                ref budget,
                ref mediumSamples,
                out var upperPressure))
        {
            return MediumResult.BudgetExceeded;
        }

        var pressure = MathF.Min(lowerPressure, upperPressure);
        if (pressure < options.MinimumPressure)
            return MediumResult.Blocked;

        var pressureRatio = Math.Clamp(pressure / options.ReferencePressure, 0f, 1f);
        transmission *= MathF.Pow(pressureRatio, options.PressureExponent);
        return transmission >= options.MinimumTransmission
            ? MediumResult.Open
            : MediumResult.Blocked;
    }

    private bool TryGetPressure(
        Entity<MapGridComponent> grid,
        TransformComponent gridTransform,
        ZLevelTileIndices tile,
        ref ZLevelSoundRouteBudget budget,
        ref int mediumSamples,
        out float pressure)
    {
        if (_pressureScratch.TryGetValue(tile, out pressure))
            return true;

        if (budget.RemainingMediumSamples <= 0)
        {
            pressure = 0f;
            return false;
        }

        budget.RemainingMediumSamples--;
        mediumSamples++;
        pressure = _atmosphere.GetZLevelTileMixture(
            grid.Owner,
            gridTransform.MapUid,
            tile)?.Pressure ?? 0f;
        if (!float.IsFinite(pressure) || pressure < 0f)
            pressure = 0f;
        _pressureScratch.Add(tile, pressure);
        return true;
    }

    private Vector2i LocalToTile(Entity<MapGridComponent> grid, Vector2 localPosition)
    {
        return _map.LocalToTile(
            grid.Owner,
            grid.Comp,
            new EntityCoordinates(grid.Owner, localPosition));
    }

    private static bool TryChargeEdge(ref ZLevelSoundRouteBudget budget, ref int edgesEvaluated)
    {
        if (budget.RemainingEdges <= 0)
            return false;

        budget.RemainingEdges--;
        edgesEvaluated++;
        return true;
    }

    private static float TransmissionLossDistance(float transmission, float scale)
    {
        return transmission >= 1f || scale <= 0f
            ? 0f
            : -MathF.Log(MathF.Max(transmission, float.Epsilon)) * scale;
    }

    private static bool IsValidEndpoint(ZLevelSoundRouteEndpoint endpoint)
    {
        return endpoint.GridUid.IsValid() &&
               float.IsFinite(endpoint.LocalPosition.X) &&
               float.IsFinite(endpoint.LocalPosition.Y);
    }

    private static bool IsValidOptions(ZLevelSoundRouteOptions options)
    {
        return float.IsFinite(options.MaxDistance) &&
               options.MaxDistance >= 0f &&
               options.MaxDistance <= MaximumRouteDistance &&
               options.MaxCrossings >= 0 &&
               options.MaxCrossings <= MaximumCrossings &&
               IsFiniteNonNegative(options.VerticalDistance) &&
               IsTransmission(options.DefaultPortalTransmission) &&
               IsTransmission(options.ExplicitPortalTransmission) &&
               IsTransmission(options.MinimumTransmission) &&
               IsFiniteNonNegative(options.TransmissionLossDistanceScale) &&
               options.MediumMode is ZLevelSoundMediumMode.Ignore or ZLevelSoundMediumMode.RequirePressure &&
               IsFiniteNonNegative(options.MinimumPressure) &&
               float.IsFinite(options.ReferencePressure) &&
               options.ReferencePressure > 0f &&
               IsFiniteNonNegative(options.PressureExponent);
    }

    private static bool IsFiniteNonNegative(float value)
    {
        return float.IsFinite(value) && value >= 0f;
    }

    private static bool IsTransmission(float value)
    {
        return float.IsFinite(value) && value >= 0f && value <= 1f;
    }

    private static ZLevelSoundRouteStatus MapPortalFailure(ZLevelSoundPortalQueryStatus status)
    {
        return status switch
        {
            ZLevelSoundPortalQueryStatus.ChunkBudgetExceeded =>
                ZLevelSoundRouteStatus.PortalChunkBudgetExceeded,
            ZLevelSoundPortalQueryStatus.BuildBudgetExceeded =>
                ZLevelSoundRouteStatus.PortalBuildBudgetExceeded,
            ZLevelSoundPortalQueryStatus.CandidateBudgetExceeded =>
                ZLevelSoundRouteStatus.PortalCandidateBudgetExceeded,
            _ => ZLevelSoundRouteStatus.Invalid,
        };
    }

    private static int SaturatingAdd(int value, int amount)
    {
        return (int) Math.Min((long) value + amount, int.MaxValue);
    }

    private static int SaturatingSubtract(int value, int amount)
    {
        return (int) Math.Max((long) value - amount, int.MinValue);
    }

    private readonly record struct RouteState(
        int PortalIndex,
        int PreviousStateIndex,
        float Distance,
        float EffectiveDistance,
        float Transmission);

    private enum MediumResult : byte
    {
        Open,
        Blocked,
        BudgetExceeded,
    }
}
