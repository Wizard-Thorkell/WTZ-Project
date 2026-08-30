// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.ZLevel.Navigation;
using Content.Shared.NPC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    private const float ZLevelRouteCostEpsilon = 0.0001f;

    [Dependency] private readonly ZLevelTraversalGraphSystem _zLevelTraversalGraph = default!;

    /// <summary>
    /// Plans a typed route between the current positions and floors of two entities.
    /// </summary>
    public Task<ZLevelPathRouteResult> GetZLevelPath(
        EntityUid entity,
        EntityUid target,
        float range,
        CancellationToken cancelToken,
        PathFlags flags = PathFlags.None,
        ZLevelPathSearchBudget? budget = null)
    {
        var started = Stopwatch.GetTimestamp();
        if (!TryComp(entity, out TransformComponent? startTransform) ||
            !TryComp(target, out TransformComponent? endTransform))
        {
            return Task.FromResult(FinishZLevelPathRoute(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                default,
                started));
        }

        var layer = 0;
        var mask = 0;
        if (TryComp<FixturesComponent>(entity, out var fixtures))
            (layer, mask) = _physics.GetHardCollision(entity, fixtures);

        var start = new ZLevelPathEndpoint(
            startTransform.MapID,
            startTransform.Coordinates,
            GetEntityWorldZ(entity, startTransform));
        var end = new ZLevelPathEndpoint(
            endTransform.MapID,
            endTransform.Coordinates,
            GetEntityWorldZ(target, endTransform));
        return FindZLevelPath(
            start,
            end,
            range,
            layer,
            mask,
            cancelToken,
            flags,
            budget ?? CreateDefaultZLevelPathBudget(),
            started);
    }

    /// <summary>
    /// Plans a typed route for an entity between explicit authoritative
    /// endpoints while deriving collision data from the moving entity.
    /// </summary>
    public Task<ZLevelPathRouteResult> GetZLevelPath(
        EntityUid entity,
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        float range,
        CancellationToken cancelToken,
        PathFlags flags = PathFlags.None,
        ZLevelPathSearchBudget? budget = null)
    {
        var started = Stopwatch.GetTimestamp();
        if (!TryComp(entity, out TransformComponent? transform) ||
            transform.MapID != start.MapId)
        {
            return Task.FromResult(FinishZLevelPathRoute(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                default,
                started));
        }

        var layer = 0;
        var mask = 0;
        if (TryComp<FixturesComponent>(entity, out var fixtures))
            (layer, mask) = _physics.GetHardCollision(entity, fixtures);

        return FindZLevelPath(
            start,
            end,
            range,
            layer,
            mask,
            cancelToken,
            flags,
            budget ?? CreateDefaultZLevelPathBudget(),
            started);
    }

    /// <summary>
    /// Plans a typed route between explicit authoritative world-floor endpoints.
    /// </summary>
    public Task<ZLevelPathRouteResult> GetZLevelPath(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        float range,
        int layer,
        int mask,
        CancellationToken cancelToken,
        PathFlags flags = PathFlags.None,
        ZLevelPathSearchBudget? budget = null)
    {
        return FindZLevelPath(
            start,
            end,
            range,
            layer,
            mask,
            cancelToken,
            flags,
            budget ?? CreateDefaultZLevelPathBudget(),
            Stopwatch.GetTimestamp());
    }

    /// <summary>
    /// Checks an already planned route and identifies the first leg that can no
    /// longer execute. Unrelated graph revision changes do not invalidate it.
    /// </summary>
    public ZLevelPathRouteValidationResult ValidateZLevelPathRoute(ZLevelPathRoute route)
    {
        if (route.Start.MapId == MapId.Nullspace || route.Start.MapId != route.End.MapId)
        {
            return new ZLevelPathRouteValidationResult(
                ZLevelPathRouteValidationStatus.InvalidRoute,
                -1);
        }

        for (var i = 0; i < route.Legs.Length; i++)
        {
            var leg = route.Legs[i];
            switch (leg.Kind)
            {
                case ZLevelPathLegKind.Local:
                    if (leg.LocalPath.IsDefault)
                    {
                        return new ZLevelPathRouteValidationResult(
                            ZLevelPathRouteValidationStatus.InvalidRoute,
                            i);
                    }

                    foreach (var poly in leg.LocalPath)
                    {
                        if (poly.IsValid())
                            continue;

                        return new ZLevelPathRouteValidationResult(
                            ZLevelPathRouteValidationStatus.LocalNavigationChanged,
                            i);
                    }
                    break;
                case ZLevelPathLegKind.Traversal:
                    if (_zLevelTraversalGraph.TryResolveEdge(
                            leg.Traversal,
                            out var current) != ZLevelTraversalEdgeStatus.Valid ||
                        !ZLevelTraversalGraphSystem.HasEquivalentEdge(leg.Traversal, current))
                    {
                        return new ZLevelPathRouteValidationResult(
                            ZLevelPathRouteValidationStatus.TraversalChanged,
                            i);
                    }
                    break;
                default:
                    return new ZLevelPathRouteValidationResult(
                        ZLevelPathRouteValidationStatus.InvalidRoute,
                        i);
            }
        }

        return ZLevelPathRouteValidationResult.Valid;
    }

    private async Task<ZLevelPathRouteResult> FindZLevelPath(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        float range,
        int layer,
        int mask,
        CancellationToken cancelToken,
        PathFlags flags,
        ZLevelPathSearchBudget budget,
        long started)
    {
        if (!budget.IsValid ||
            !float.IsFinite(range) ||
            range < 0f ||
            start.MapId == MapId.Nullspace ||
            start.MapId != end.MapId ||
            !TrySnapshotEndpoint(start, out var stableStart) ||
            !TrySnapshotEndpoint(end, out var stableEnd))
        {
            return FinishZLevelPathRoute(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                default,
                started);
        }

        start = stableStart;
        end = stableEnd;

        if (cancelToken.IsCancellationRequested)
        {
            return FinishZLevelPathRoute(
                ZLevelPathRouteStatus.Cancelled,
                null,
                default,
                started);
        }

        var snapshot = _zLevelTraversalGraph.CreateSnapshot(start.MapId);
        if (!TryBuildConnectorGroups(snapshot, out var connectorGroups))
        {
            return FinishZLevelPathRoute(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                CreateDiagnostics(snapshot, 0, 0, 0),
                started);
        }

        var states = new List<ZLevelRouteSearchState>
        {
            new(start, 0f),
        };
        var stateLookup = new Dictionary<ZLevelPathEndpoint, int>
        {
            [start] = 0,
        };

        var statesExpanded = 0;
        var localPathsRequested = 0;
        var traversalEdgesEvaluated = 0;
        var bestTargetCost = float.PositiveInfinity;
        var bestTargetState = -1;
        ZLevelPathLeg? bestTargetLeg = null;

        while (true)
        {
            if (cancelToken.IsCancellationRequested)
            {
                return FinishSearch(
                    ZLevelPathRouteStatus.Cancelled,
                    null,
                    snapshot,
                    statesExpanded,
                    localPathsRequested,
                    traversalEdgesEvaluated,
                    started);
            }

            var currentIndex = FindNextSearchState(states, bestTargetCost);
            if (currentIndex < 0)
                break;

            if (!budget.TryTakeStateExpansion())
            {
                return FinishSearch(
                    ZLevelPathRouteStatus.StateExpansionBudgetExceeded,
                    null,
                    snapshot,
                    statesExpanded,
                    localPathsRequested,
                    traversalEdgesEvaluated,
                    started);
            }

            statesExpanded++;
            var current = states[currentIndex];
            current.Settled = true;

            if (current.Endpoint == end && current.Cost < bestTargetCost)
            {
                bestTargetCost = current.Cost;
                bestTargetState = currentIndex;
                bestTargetLeg = null;
            }

            for (var groupIndex = 0; groupIndex < connectorGroups.Count; groupIndex++)
            {
                var group = connectorGroups[groupIndex];
                if (group.Source.WorldZ != current.Endpoint.WorldZ ||
                    group.Source != current.Endpoint ||
                    !CanImproveFromGroup(
                        current,
                        group,
                        stateLookup,
                        states,
                        bestTargetCost))
                {
                    continue;
                }

                if (!TryRelaxConnectorGroup(
                        currentIndex,
                        current,
                        group,
                        null,
                        ref budget,
                        ref traversalEdgesEvaluated,
                        bestTargetCost,
                        stateLookup,
                        states,
                        out var failure))
                {
                    return FinishSearch(
                        failure,
                        null,
                        snapshot,
                        statesExpanded,
                        localPathsRequested,
                        traversalEdgesEvaluated,
                        started);
                }
            }

            var pending = BuildPendingLocalQueries(
                current,
                end,
                range,
                connectorGroups,
                stateLookup,
                states,
                bestTargetCost);
            if (!budget.TryTakeLocalPaths(pending.Count))
            {
                return FinishSearch(
                    ZLevelPathRouteStatus.LocalPathBudgetExceeded,
                    null,
                    snapshot,
                    statesExpanded,
                    localPathsRequested,
                    traversalEdgesEvaluated,
                    started);
            }

            if (pending.Count == 0)
                continue;

            localPathsRequested += pending.Count;
            var tasks = new Task<ZLevelLocalPathAttempt>[pending.Count];
            for (var i = 0; i < pending.Count; i++)
            {
                var query = pending[i];
                tasks[i] = RequestLocalZLevelPath(
                    current.Endpoint,
                    query.End,
                    query.Range,
                    layer,
                    mask,
                    cancelToken,
                    flags);
            }

            var attempts = await Task.WhenAll(tasks);
            if (cancelToken.IsCancellationRequested)
            {
                return FinishSearch(
                    ZLevelPathRouteStatus.Cancelled,
                    null,
                    snapshot,
                    statesExpanded,
                    localPathsRequested,
                    traversalEdgesEvaluated,
                    started);
            }

            var snapshotStatus = _zLevelTraversalGraph.ValidateSnapshot(snapshot);
            if (snapshotStatus != ZLevelTraversalGraphSnapshotStatus.Current)
            {
                return FinishSearch(
                    ToRouteStatus(snapshotStatus),
                    null,
                    snapshot,
                    statesExpanded,
                    localPathsRequested,
                    traversalEdgesEvaluated,
                    started);
            }

            for (var i = 0; i < pending.Count; i++)
            {
                var attempt = attempts[i];
                if (!attempt.Succeeded)
                    continue;

                var query = pending[i];
                if (query.ReachesTarget)
                {
                    var targetCost = current.Cost + attempt.Leg.Cost;
                    if (targetCost + ZLevelRouteCostEpsilon < bestTargetCost)
                    {
                        bestTargetCost = targetCost;
                        bestTargetState = currentIndex;
                        bestTargetLeg = attempt.Leg;
                    }
                }

                foreach (var groupIndex in query.ConnectorGroups)
                {
                    if (!TryRelaxConnectorGroup(
                            currentIndex,
                            current,
                            connectorGroups[groupIndex],
                            attempt.Leg,
                            ref budget,
                            ref traversalEdgesEvaluated,
                            bestTargetCost,
                            stateLookup,
                            states,
                            out var failure))
                    {
                        return FinishSearch(
                            failure,
                            null,
                            snapshot,
                            statesExpanded,
                            localPathsRequested,
                            traversalEdgesEvaluated,
                            started);
                    }
                }
            }
        }

        if (bestTargetState < 0)
        {
            return FinishSearch(
                ZLevelPathRouteStatus.NoPath,
                null,
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated,
                started);
        }

        var finalSnapshotStatus = _zLevelTraversalGraph.ValidateSnapshot(snapshot);
        if (finalSnapshotStatus != ZLevelTraversalGraphSnapshotStatus.Current)
        {
            return FinishSearch(
                ToRouteStatus(finalSnapshotStatus),
                null,
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated,
                started);
        }

        if (!TryBuildRoute(
                states,
                bestTargetState,
                bestTargetLeg,
                start,
                end,
                snapshot.Version,
                out var route))
        {
            return FinishSearch(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated,
                started);
        }

        var validation = ValidateZLevelPathRoute(route!);
        if (validation.Status == ZLevelPathRouteValidationStatus.LocalNavigationChanged)
        {
            return FinishSearch(
                ZLevelPathRouteStatus.LocalNavigationChanged,
                null,
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated,
                started);
        }

        if (!validation.IsValid)
        {
            return FinishSearch(
                ZLevelPathRouteStatus.InvalidRequest,
                null,
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated,
                started);
        }

        return FinishSearch(
            ZLevelPathRouteStatus.Success,
            route,
            snapshot,
            statesExpanded,
            localPathsRequested,
            traversalEdgesEvaluated,
            started);
    }

    private List<ZLevelPendingLocalQuery> BuildPendingLocalQueries(
        ZLevelRouteSearchState current,
        ZLevelPathEndpoint end,
        float range,
        List<ZLevelConnectorGroup> connectorGroups,
        Dictionary<ZLevelPathEndpoint, int> stateLookup,
        List<ZLevelRouteSearchState> states,
        float bestTargetCost)
    {
        var pending = new List<ZLevelPendingLocalQuery>();
        var lookup = new Dictionary<ZLevelLocalQueryKey, int>();

        if (current.Endpoint.WorldZ == end.WorldZ &&
            current.Endpoint != end &&
            current.Cost < bestTargetCost)
        {
            var key = new ZLevelLocalQueryKey(end, range);
            lookup.Add(key, pending.Count);
            pending.Add(new ZLevelPendingLocalQuery(end, range) { ReachesTarget = true });
        }

        for (var i = 0; i < connectorGroups.Count; i++)
        {
            var group = connectorGroups[i];
            if (group.Source.WorldZ != current.Endpoint.WorldZ ||
                group.Source == current.Endpoint ||
                !CanImproveFromGroup(current, group, stateLookup, states, bestTargetCost))
            {
                continue;
            }

            var key = new ZLevelLocalQueryKey(group.Source, 0f);
            if (!lookup.TryGetValue(key, out var queryIndex))
            {
                queryIndex = pending.Count;
                lookup.Add(key, queryIndex);
                pending.Add(new ZLevelPendingLocalQuery(group.Source, 0f));
            }

            pending[queryIndex].ConnectorGroups.Add(i);
        }

        return pending;
    }

    private async Task<ZLevelLocalPathAttempt> RequestLocalZLevelPath(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        float range,
        int layer,
        int mask,
        CancellationToken cancelToken,
        PathFlags flags)
    {
        var request = new AStarPathRequest(
            start.Coordinates,
            start.WorldZ,
            end.Coordinates,
            end.WorldZ,
            flags,
            range,
            layer,
            mask,
            cancelToken);
        var result = await GetPath(request);
        if (result.Result != PathResult.Path ||
            !ZLevelPathLeg.TryCreateLocal(
                start,
                end,
                result.Path.ToImmutableArray(),
                request.Cost,
                out var leg))
        {
            return default;
        }

        return new ZLevelLocalPathAttempt(true, leg);
    }

    private bool TryBuildConnectorGroups(
        ZLevelTraversalGraphSnapshot snapshot,
        out List<ZLevelConnectorGroup> groups)
    {
        groups = new List<ZLevelConnectorGroup>();
        var lookup = new Dictionary<ZLevelPathEndpoint, int>();

        foreach (var edge in snapshot.Edges)
        {
            if (edge.Source.MapId != snapshot.MapId ||
                edge.Destination.MapId != snapshot.MapId ||
                !TryGetNodeEndpoint(edge.Source, out var source) ||
                !TryGetNodeEndpoint(edge.Destination, out var destination))
            {
                return false;
            }

            if (!lookup.TryGetValue(source, out var groupIndex))
            {
                groupIndex = groups.Count;
                lookup.Add(source, groupIndex);
                groups.Add(new ZLevelConnectorGroup(source));
            }

            groups[groupIndex].Transitions.Add(new ZLevelConnectorTransition(destination, edge));
        }

        return true;
    }

    private bool TryGetNodeEndpoint(
        ZLevelTraversalNavigationNode node,
        out ZLevelPathEndpoint endpoint)
    {
        endpoint = default;
        if (!TryComp<MapGridComponent>(node.GridUid, out var grid) ||
            !TryComp(node.GridUid, out TransformComponent? transform) ||
            transform.MapID != node.MapId)
        {
            return false;
        }

        endpoint = new ZLevelPathEndpoint(
            node.MapId,
            _maps.GridTileToLocal(node.GridUid, grid, node.Tile),
            node.WorldZ);
        return true;
    }

    private bool TryRelaxConnectorGroup(
        int currentIndex,
        ZLevelRouteSearchState current,
        ZLevelConnectorGroup group,
        ZLevelPathLeg? localLeg,
        ref ZLevelPathSearchBudget budget,
        ref int traversalEdgesEvaluated,
        float bestTargetCost,
        Dictionary<ZLevelPathEndpoint, int> stateLookup,
        List<ZLevelRouteSearchState> states,
        out ZLevelPathRouteStatus failure)
    {
        failure = ZLevelPathRouteStatus.Success;
        var localCost = localLeg?.Cost ?? 0f;
        foreach (var transition in group.Transitions)
        {
            if (!budget.TryTakeTraversalEdge())
            {
                failure = ZLevelPathRouteStatus.TraversalEdgeBudgetExceeded;
                return false;
            }

            traversalEdgesEvaluated++;
            if (!ZLevelPathLeg.TryCreateTraversal(
                    group.Source,
                    transition.Destination,
                    transition.Edge,
                    out var traversalLeg))
            {
                failure = ZLevelPathRouteStatus.InvalidRequest;
                return false;
            }

            var nextCost = current.Cost + localCost + traversalLeg.Cost;
            if (!float.IsFinite(nextCost) ||
                nextCost + ZLevelRouteCostEpsilon >= bestTargetCost)
            {
                continue;
            }

            if (stateLookup.TryGetValue(transition.Destination, out var stateIndex))
            {
                var existing = states[stateIndex];
                if (nextCost + ZLevelRouteCostEpsilon >= existing.Cost)
                    continue;

                existing.Cost = nextCost;
                existing.PreviousState = currentIndex;
                existing.IncomingLocal = localLeg;
                existing.IncomingTraversal = traversalLeg;
                existing.Settled = false;
                continue;
            }

            stateLookup.Add(transition.Destination, states.Count);
            states.Add(new ZLevelRouteSearchState(
                transition.Destination,
                nextCost,
                currentIndex,
                localLeg,
                traversalLeg));
        }

        return true;
    }

    private static bool CanImproveFromGroup(
        ZLevelRouteSearchState current,
        ZLevelConnectorGroup group,
        Dictionary<ZLevelPathEndpoint, int> stateLookup,
        List<ZLevelRouteSearchState> states,
        float bestTargetCost)
    {
        foreach (var transition in group.Transitions)
        {
            var lowerBound = current.Cost + transition.Edge.Cost;
            if (lowerBound + ZLevelRouteCostEpsilon >= bestTargetCost)
                continue;

            if (!stateLookup.TryGetValue(transition.Destination, out var stateIndex) ||
                lowerBound + ZLevelRouteCostEpsilon < states[stateIndex].Cost)
            {
                return true;
            }
        }

        return false;
    }

    private static int FindNextSearchState(
        List<ZLevelRouteSearchState> states,
        float bestTargetCost)
    {
        var bestIndex = -1;
        var bestCost = bestTargetCost;
        for (var i = 0; i < states.Count; i++)
        {
            var state = states[i];
            if (state.Settled || state.Cost + ZLevelRouteCostEpsilon >= bestCost)
                continue;

            bestCost = state.Cost;
            bestIndex = i;
        }

        return bestIndex;
    }

    private static bool TryBuildRoute(
        List<ZLevelRouteSearchState> states,
        int targetState,
        ZLevelPathLeg? targetLeg,
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ZLevelTraversalGraphVersion graphVersion,
        out ZLevelPathRoute? route)
    {
        var reverse = new List<ZLevelPathLeg>();
        var stateIndex = targetState;
        while (states[stateIndex].PreviousState >= 0)
        {
            var state = states[stateIndex];
            reverse.Add(state.IncomingTraversal);
            if (state.IncomingLocal is { } local)
                reverse.Add(local);
            stateIndex = state.PreviousState;
        }

        reverse.Reverse();
        if (targetLeg is { } finalLocal)
            reverse.Add(finalLocal);

        return ZLevelPathRoute.TryCreate(
            start,
            end,
            graphVersion,
            reverse.ToImmutableArray(),
            out route);
    }

    private bool TrySnapshotEndpoint(
        ZLevelPathEndpoint endpoint,
        out ZLevelPathEndpoint snapshot)
    {
        snapshot = default;
        if (!_xformQuery.TryComp(endpoint.Coordinates.EntityId, out var transform) ||
            transform.MapID != endpoint.MapId)
        {
            return false;
        }

        var mapCoordinates = _transform.ToMapCoordinates(endpoint.Coordinates);
        var frame = _transform.GetGrid(endpoint.Coordinates) ??
                    _transform.GetMap(endpoint.Coordinates);
        if (mapCoordinates.MapId != endpoint.MapId || frame == null)
            return false;

        var coordinates = _transform.ToCoordinates(frame.Value, mapCoordinates);
        if (!coordinates.IsValid(EntityManager))
            return false;

        snapshot = new ZLevelPathEndpoint(endpoint.MapId, coordinates, endpoint.WorldZ);
        return true;
    }

    private ZLevelPathRouteResult FinishSearch(
        ZLevelPathRouteStatus status,
        ZLevelPathRoute? route,
        ZLevelTraversalGraphSnapshot snapshot,
        int statesExpanded,
        int localPathsRequested,
        int traversalEdgesEvaluated,
        long started)
    {
        return FinishZLevelPathRoute(
            status,
            route,
            CreateDiagnostics(
                snapshot,
                statesExpanded,
                localPathsRequested,
                traversalEdgesEvaluated),
            started);
    }

    private static ZLevelPathSearchDiagnostics CreateDiagnostics(
        ZLevelTraversalGraphSnapshot snapshot,
        int statesExpanded,
        int localPathsRequested,
        int traversalEdgesEvaluated)
    {
        return new ZLevelPathSearchDiagnostics(
            statesExpanded,
            localPathsRequested,
            traversalEdgesEvaluated,
            snapshot.TopologyRevision,
            snapshot.EnvironmentRevision);
    }

    private static ZLevelPathRouteStatus ToRouteStatus(
        ZLevelTraversalGraphSnapshotStatus status)
    {
        return status switch
        {
            ZLevelTraversalGraphSnapshotStatus.TopologyChanged =>
                ZLevelPathRouteStatus.TopologyChanged,
            ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged =>
                ZLevelPathRouteStatus.EnvironmentChanged,
            ZLevelTraversalGraphSnapshotStatus.TopologyAndEnvironmentChanged =>
                ZLevelPathRouteStatus.TopologyAndEnvironmentChanged,
            _ => ZLevelPathRouteStatus.InvalidRequest,
        };
    }

    private sealed class ZLevelRouteSearchState
    {
        public readonly ZLevelPathEndpoint Endpoint;
        public float Cost;
        public int PreviousState = -1;
        public ZLevelPathLeg? IncomingLocal;
        public ZLevelPathLeg IncomingTraversal;
        public bool Settled;

        public ZLevelRouteSearchState(ZLevelPathEndpoint endpoint, float cost)
        {
            Endpoint = endpoint;
            Cost = cost;
        }

        public ZLevelRouteSearchState(
            ZLevelPathEndpoint endpoint,
            float cost,
            int previousState,
            ZLevelPathLeg? incomingLocal,
            ZLevelPathLeg incomingTraversal)
        {
            Endpoint = endpoint;
            Cost = cost;
            PreviousState = previousState;
            IncomingLocal = incomingLocal;
            IncomingTraversal = incomingTraversal;
        }
    }

    private sealed class ZLevelConnectorGroup
    {
        public readonly ZLevelPathEndpoint Source;
        public readonly List<ZLevelConnectorTransition> Transitions = new();

        public ZLevelConnectorGroup(ZLevelPathEndpoint source)
        {
            Source = source;
        }
    }

    private sealed class ZLevelPendingLocalQuery
    {
        public readonly ZLevelPathEndpoint End;
        public readonly float Range;
        public readonly List<int> ConnectorGroups = new();
        public bool ReachesTarget;

        public ZLevelPendingLocalQuery(ZLevelPathEndpoint end, float range)
        {
            End = end;
            Range = range;
        }
    }

    private readonly record struct ZLevelConnectorTransition(
        ZLevelPathEndpoint Destination,
        ZLevelTraversalNavigationEdge Edge);

    private readonly record struct ZLevelLocalQueryKey(ZLevelPathEndpoint End, float Range);

    private readonly record struct ZLevelLocalPathAttempt(bool Succeeded, ZLevelPathLeg Leg);
}
