// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.ZLevel.Systems;

public sealed partial class ZLevelElevatorSystem
{
    public const float MaximumNavigationCost = 1_000_000f;

    private readonly Dictionary<EntityUid, PendingElevatorNavigation> _navigationByUser = new();
    private readonly Dictionary<EntityUid, EntityUid> _navigationUserByCabin = new();
    private readonly List<EntityUid> _navigationStopBuffer = new();

    private long _navigationEdgeQueries;
    private long _validNavigationEdges;
    private long _navigationStarted;
    private long _navigationCompleted;
    private long _navigationCancelled;
    private long _navigationRejected;

    public int ActiveNavigationCount => _navigationByUser.Count;

    private void InitializeNavigation()
    {
        SubscribeLocalEvent<EntityTerminatingEvent>(OnNavigationEntityTerminating);
    }

    /// <summary>
    /// Adds at most two deterministic edges per landing: the nearest served
    /// floor below and the nearest served floor above.
    /// </summary>
    public void AppendNavigationEdges(
        MapId mapId,
        ZLevelTraversalGraphVersion version,
        List<ZLevelTraversalNavigationEdge> results)
    {
        if (mapId == MapId.Nullspace || version.MapId != mapId)
            return;

        _navigationStopBuffer.Clear();
        foreach (var (stop, registration) in _stops)
        {
            if (registration.MapId == mapId)
                _navigationStopBuffer.Add(stop);
        }

        _navigationStopBuffer.Sort();
        foreach (var stop in _navigationStopBuffer)
        {
            if (TryBuildNavigationEdge(stop, -1, version, out var down) ==
                ZLevelTraversalEdgeStatus.Valid)
            {
                results.Add(down);
            }

            if (TryBuildNavigationEdge(stop, 1, version, out var up) ==
                ZLevelTraversalEdgeStatus.Valid)
            {
                results.Add(up);
            }
        }
    }

    /// <summary>
    /// Rebuilds the live physical-elevator edge selected by a captured edge.
    /// The complete expected edge is needed because one stop can offer both
    /// an upward and a downward transition.
    /// </summary>
    public ZLevelTraversalEdgeStatus TryResolveNavigationEdge(
        in ZLevelTraversalNavigationEdge expected,
        ZLevelTraversalGraphVersion version,
        out ZLevelTraversalNavigationEdge current)
    {
        _navigationEdgeQueries++;
        current = default;
        if (expected.Source.Kind != ZLevelTraversalKind.Elevator ||
            expected.ZOffset == 0 ||
            expected.Source.MapId != version.MapId)
        {
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var status = TryBuildNavigationEdge(
            expected.Source.Traversal,
            Math.Sign(expected.ZOffset),
            version,
            out current);
        if (status == ZLevelTraversalEdgeStatus.Valid)
            _validNavigationEdges++;
        return status;
    }

    public bool IsPhysicalNavigationStop(EntityUid uid)
    {
        return _stops.ContainsKey(uid);
    }

    public bool IsNavigationTraversalPending(EntityUid user, EntityUid? stop = null)
    {
        return _navigationByUser.TryGetValue(user, out var pending) &&
               (stop == null || pending.Edge.Source.Traversal == stop);
    }

    /// <summary>
    /// Calls the physical cabin to the route source and then carries the user
    /// to the edge destination. Repeated execution of the same edge is
    /// idempotent while the cabin is in flight.
    /// </summary>
    public bool TryStartNavigationTraversal(
        in ZLevelTraversalNavigationEdge edge,
        EntityUid user)
    {
        if (_navigationByUser.TryGetValue(user, out var existing))
        {
            return ZLevelTraversalGraphSystem.HasEquivalentEdge(existing.Edge, edge);
        }

        var version = new ZLevelTraversalGraphVersion(
            edge.Source.MapId,
            edge.TopologyRevision,
            edge.EnvironmentRevision);
        if (TryResolveNavigationEdge(edge, version, out var current) !=
                ZLevelTraversalEdgeStatus.Valid ||
            !ZLevelTraversalGraphSystem.HasEquivalentEdge(edge, current) ||
            !_stops.TryGetValue(edge.Source.Traversal, out var sourceRegistration) ||
            !IsUserAtNavigationSource(user, sourceRegistration, edge.Source) ||
            !TryResolveNetwork(
                sourceRegistration.Key,
                out var cabin,
                out _,
                out var cabinRegistration,
                out _,
                out _) ||
            _pending.ContainsKey(cabin) ||
            _navigationUserByCabin.ContainsKey(cabin))
        {
            _navigationRejected++;
            return false;
        }

        var stage = cabinRegistration.LocalZ == edge.Source.LocalZ
            ? ElevatorNavigationStage.Riding
            : ElevatorNavigationStage.Calling;
        var pending = new PendingElevatorNavigation(cabin, edge, stage);
        _navigationByUser.Add(user, pending);
        _navigationUserByCabin.Add(cabin, user);

        var requestedFloor = stage == ElevatorNavigationStage.Riding
            ? edge.Destination.LocalZ
            : edge.Source.LocalZ;
        var result = TryRequestFloor(cabin, requestedFloor);
        if (result is ZLevelElevatorRequestResult.Started or ZLevelElevatorRequestResult.AlreadyThere)
        {
            _navigationStarted++;
            return true;
        }

        RemoveNavigation(user, cabin);
        _navigationRejected++;
        return false;
    }

    public bool TryCancelNavigationTraversal(EntityUid user, EntityUid? stop = null)
    {
        if (!_navigationByUser.TryGetValue(user, out var pending) ||
            stop != null && pending.Edge.Source.Traversal != stop)
        {
            return false;
        }

        RemoveNavigation(user, pending.Cabin);
        _navigationCancelled++;
        return true;
    }

    /// <summary>
    /// Lets graph invalidation cheaply recognize authored elevator columns.
    /// </summary>
    public bool TryGetNavigationMapAt(
        EntityUid gridUid,
        Vector2i tile,
        out MapId mapId)
    {
        foreach (var registration in _stops.Values)
        {
            if (registration.Key.GridUid != gridUid || registration.Key.Tile != tile)
                continue;

            mapId = registration.MapId;
            return true;
        }

        mapId = MapId.Nullspace;
        return false;
    }

    public bool HasNavigationGrid(EntityUid gridUid, out MapId mapId)
    {
        foreach (var registration in _stops.Values)
        {
            if (registration.Key.GridUid != gridUid)
                continue;

            mapId = registration.MapId;
            return true;
        }

        mapId = MapId.Nullspace;
        return false;
    }

    public ZLevelElevatorNavigationMetricsSnapshot NavigationSnapshot()
    {
        return new ZLevelElevatorNavigationMetricsSnapshot(
            _navigationByUser.Count,
            _navigationEdgeQueries,
            _validNavigationEdges,
            _navigationStarted,
            _navigationCompleted,
            _navigationCancelled,
            _navigationRejected);
    }

    private void ResetNavigationMetrics()
    {
        _navigationEdgeQueries = 0;
        _validNavigationEdges = 0;
        _navigationStarted = 0;
        _navigationCompleted = 0;
        _navigationCancelled = 0;
        _navigationRejected = 0;
    }

    private ZLevelTraversalEdgeStatus TryBuildNavigationEdge(
        EntityUid sourceStop,
        int direction,
        ZLevelTraversalGraphVersion version,
        out ZLevelTraversalNavigationEdge edge)
    {
        edge = default;
        if (direction is not (-1 or 1) ||
            !_stops.TryGetValue(sourceStop, out var sourceRegistration) ||
            sourceRegistration.MapId != version.MapId ||
            !TryResolveNetwork(
                sourceRegistration.Key,
                out var cabin,
                out var cabinComponent,
                out var cabinRegistration,
                out var stops,
                out _))
        {
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        if (!TryGetUniqueStop(stops, sourceRegistration.LocalZ, out var uniqueSource) ||
            uniqueSource != sourceStop ||
            !TryGetUniqueStop(stops, cabinRegistration.LocalZ, out _) ||
            !_controls.TryGetValue(cabin, out var cabinControl) ||
            cabinControl != cabinRegistration ||
            !TryComp<ZLevelElevatorControlComponent>(cabin, out var controlComponent) ||
            controlComponent.Mode != ZLevelElevatorControlMode.Cabin ||
            !HasValidNavigationConfiguration(cabinComponent, stops))
        {
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        if (!IsPowered(cabin, cabinComponent))
            return ZLevelTraversalEdgeStatus.Unpowered;

        if (!TryFindAdjacentFloor(stops, sourceRegistration.LocalZ, direction, out var targetFloor) ||
            !TryGetUniqueStop(stops, targetFloor, out _) ||
            !TryComp<MapGridComponent>(sourceRegistration.Key.GridUid, out var grid))
        {
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var offsetLong = (long) targetFloor - sourceRegistration.LocalZ;
        var distance = Math.Abs(offsetLong);
        if (distance == 0 || distance > cabinComponent.MaxTravelLevels || distance > int.MaxValue)
            return ZLevelTraversalEdgeStatus.Invalid;

        if (!_boundaries.IsStackOpen(
                sourceRegistration.Key.GridUid,
                grid,
                sourceRegistration.Key.Tile,
                sourceRegistration.LocalZ,
                targetFloor,
                direction > 0
                    ? ZLevelBoundaryChannels.TraversalUp
                    : ZLevelBoundaryChannels.TraversalDown))
        {
            return ZLevelTraversalEdgeStatus.ClosedBoundary;
        }

        if (!HasDirectNavigationSupport(sourceRegistration, grid, sourceRegistration.LocalZ) ||
            !HasDirectNavigationSupport(sourceRegistration, grid, targetFloor))
        {
            return ZLevelTraversalEdgeStatus.MissingDestinationSupport;
        }

        var navigationCost = cabinComponent.NavigationCallCost +
                             cabinComponent.NavigationCostPerLevel * distance;
        if (!float.IsFinite(navigationCost) ||
            navigationCost < 0f ||
            navigationCost > MaximumNavigationCost)
        {
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var sourceWorldZ = _transform.LocalToWorldZLevel(
            sourceRegistration.Key.GridUid,
            sourceRegistration.LocalZ);
        var targetWorldZ = _transform.LocalToWorldZLevel(
            sourceRegistration.Key.GridUid,
            targetFloor);
        var source = new ZLevelTraversalNavigationNode(
            sourceStop,
            sourceRegistration.Key.GridUid,
            sourceRegistration.Key.Tile,
            sourceRegistration.LocalZ,
            sourceWorldZ,
            sourceRegistration.MapId,
            ZLevelTraversalKind.Elevator);
        var destination = source with
        {
            LocalZ = targetFloor,
            WorldZ = targetWorldZ,
        };
        var durationTicks = Math.Min(
            MaximumTravelDuration.Ticks,
            cabinComponent.TravelTimePerLevel.Ticks * distance);
        edge = new ZLevelTraversalNavigationEdge(
            source,
            destination,
            (int) offsetLong,
            navigationCost,
            TimeSpan.FromTicks(durationTicks),
            true,
            version.TopologyRevision,
            version.EnvironmentRevision);
        return ZLevelTraversalEdgeStatus.Valid;
    }

    private static bool HasValidNavigationConfiguration(
        ZLevelElevatorCabinComponent component,
        SortedDictionary<int, SortedSet<EntityUid>> stops)
    {
        if (component.MaxTravelLevels is < 1 or > MaximumTravelLevels ||
            component.PassengerLimit is < 1 or > MaximumPassengers ||
            component.TravelTimePerLevel < TimeSpan.Zero ||
            component.TravelTimePerLevel > MaximumTravelTimePerLevel ||
            !float.IsFinite(component.NavigationCallCost) ||
            component.NavigationCallCost < 0f ||
            component.NavigationCallCost > MaximumNavigationCost ||
            !float.IsFinite(component.NavigationCostPerLevel) ||
            component.NavigationCostPerLevel < 0f ||
            component.NavigationCostPerLevel > MaximumNavigationCost)
        {
            return false;
        }

        var span = (long) stops.Last().Key - stops.First().Key;
        return span > 0 && span <= component.MaxTravelLevels;
    }

    private static bool TryFindAdjacentFloor(
        SortedDictionary<int, SortedSet<EntityUid>> stops,
        int sourceFloor,
        int direction,
        out int targetFloor)
    {
        targetFloor = default;
        var found = false;
        foreach (var floor in stops.Keys)
        {
            if (direction > 0)
            {
                if (floor <= sourceFloor)
                    continue;

                targetFloor = floor;
                return true;
            }

            if (floor >= sourceFloor)
                break;

            targetFloor = floor;
            found = true;
        }

        return found;
    }

    private bool HasDirectNavigationSupport(
        ElevatorRegistration registration,
        MapGridComponent grid,
        int localZ)
    {
        var floor = _map.GetZLevelTileRef(
            registration.Key.GridUid,
            grid,
            new ZLevelTileIndices(registration.Key.Tile.X, registration.Key.Tile.Y, localZ));
        return !floor.Tile.IsEmpty &&
               !_boundaries.CanBodyPass(
                   registration.Key.GridUid,
                   grid,
                   registration.Key.Tile,
                   localZ,
                   localZ - 1);
    }

    private bool IsUserAtNavigationSource(
        EntityUid user,
        ElevatorRegistration registration,
        ZLevelTraversalNavigationNode source)
    {
        if (!TryComp(user, out TransformComponent? transform) ||
            transform.MapID != source.MapId ||
            transform.GridUid != source.GridUid ||
            registration.Key.GridUid != source.GridUid ||
            registration.Key.Tile != source.Tile ||
            registration.LocalZ != source.LocalZ ||
            _zLevels.GetZLevel(user) != source.LocalZ ||
            !TryComp<MapGridComponent>(source.GridUid, out var grid))
        {
            return false;
        }

        return _map.TileIndicesFor(source.GridUid, grid, transform.Coordinates) == source.Tile;
    }

    private void ContinueNavigationAfterArrival(EntityUid cabin, int arrivedFloor)
    {
        if (!_navigationUserByCabin.TryGetValue(cabin, out var user) ||
            !_navigationByUser.TryGetValue(user, out var pending) ||
            pending.Cabin != cabin)
        {
            return;
        }

        if (pending.Stage == ElevatorNavigationStage.Riding)
        {
            RemoveNavigation(user, cabin);
            if (arrivedFloor == pending.Edge.Destination.LocalZ)
                _navigationCompleted++;
            else
                _navigationCancelled++;
            return;
        }

        var version = new ZLevelTraversalGraphVersion(
            pending.Edge.Source.MapId,
            pending.Edge.TopologyRevision,
            pending.Edge.EnvironmentRevision);
        if (arrivedFloor != pending.Edge.Source.LocalZ ||
            TryResolveNavigationEdge(pending.Edge, version, out var current) !=
                ZLevelTraversalEdgeStatus.Valid ||
            !ZLevelTraversalGraphSystem.HasEquivalentEdge(pending.Edge, current) ||
            !_stops.TryGetValue(pending.Edge.Source.Traversal, out var sourceRegistration) ||
            !IsUserAtNavigationSource(user, sourceRegistration, pending.Edge.Source))
        {
            RemoveNavigation(user, cabin);
            _navigationCancelled++;
            return;
        }

        _navigationByUser[user] = pending with { Stage = ElevatorNavigationStage.Riding };
        var result = TryRequestFloor(cabin, pending.Edge.Destination.LocalZ);
        if (result is ZLevelElevatorRequestResult.Started or ZLevelElevatorRequestResult.AlreadyThere)
            return;

        RemoveNavigation(user, cabin);
        _navigationRejected++;
    }

    private void CancelNavigationForCabin(EntityUid cabin)
    {
        if (!_navigationUserByCabin.TryGetValue(cabin, out var user))
            return;

        RemoveNavigation(user, cabin);
        _navigationCancelled++;
    }

    private void RemoveNavigation(EntityUid user, EntityUid cabin)
    {
        _navigationByUser.Remove(user);
        if (_navigationUserByCabin.TryGetValue(cabin, out var owner) && owner == user)
            _navigationUserByCabin.Remove(cabin);
    }

    private void NotifyNavigationTopologyChanged(MapId mapId)
    {
        RaiseNavigationChanged(mapId, topologyChanged: true);
    }

    private void NotifyNavigationEnvironmentChanged(MapId mapId)
    {
        RaiseNavigationChanged(mapId, topologyChanged: false);
    }

    private void RaiseNavigationChanged(MapId mapId, bool topologyChanged)
    {
        if (mapId == MapId.Nullspace)
            return;

        var ev = new ZLevelElevatorNavigationChangedEvent(mapId, topologyChanged);
        RaiseLocalEvent(ev);
    }

    private void OnNavigationEntityTerminating(ref EntityTerminatingEvent args)
    {
        TryCancelNavigationTraversal(args.Entity.Owner);
    }

    private sealed record PendingElevatorNavigation(
        EntityUid Cabin,
        ZLevelTraversalNavigationEdge Edge,
        ElevatorNavigationStage Stage);

    private enum ElevatorNavigationStage : byte
    {
        Calling,
        Riding,
    }
}

public sealed class ZLevelElevatorNavigationChangedEvent : EntityEventArgs
{
    public MapId MapId { get; }
    public bool TopologyChanged { get; }

    public ZLevelElevatorNavigationChangedEvent(MapId mapId, bool topologyChanged)
    {
        MapId = mapId;
        TopologyChanged = topologyChanged;
    }
}

public readonly record struct ZLevelElevatorNavigationMetricsSnapshot(
    int Active,
    long EdgeQueries,
    long ValidEdges,
    long Started,
    long Completed,
    long Cancelled,
    long Rejected);
