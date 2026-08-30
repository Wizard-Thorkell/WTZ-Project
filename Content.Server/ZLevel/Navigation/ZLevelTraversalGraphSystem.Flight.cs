// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Server.ZLevel.Components;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.ZLevel.Navigation;

public sealed partial class ZLevelTraversalGraphSystem
{
    public const int MaximumFlightNavigationOffset = 1;
    public const float MaximumFlightNavigationCost = 1_000_000f;

    private readonly Dictionary<EntityUid, FlightNavigationRegistration> _flightNavigationRegistrations = new();
    private readonly Dictionary<ZLevelTraversalNodeKey, List<EntityUid>> _flightNavigationByLocation = new();
    private readonly List<ZLevelTraversalNodeKey> _flightNavigationKeyBuffer = new(4);
    private readonly List<EntityUid> _flightNavigationEntityBuffer = new();

    private long _flightNavigationRefreshes;
    private long _flightEdgeQueries;
    private long _validFlightEdges;
    private long _closedFlightEdges;
    private long _unsupportedFlightEdges;
    private long _invalidFlightEdges;

    public int FlightNavigationMarkerCount => _flightNavigationRegistrations.Count;
    public int FlightNavigationLocationCount => _flightNavigationByLocation.Count;

    private void InitializeFlightNavigation()
    {
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, ComponentStartup>(OnFlightNavigationStartup);
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, ComponentShutdown>(OnFlightNavigationShutdown);
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, MoveEvent>(OnFlightNavigationMoved);
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, EntParentChangedMessage>(OnFlightNavigationParentChanged);
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, AnchorStateChangedEvent>(OnFlightNavigationAnchorChanged);
        SubscribeLocalEvent<ZLevelFlightNavigationComponent, ZLevelPositionChangedEvent>(OnFlightNavigationZChanged);
    }

    public void RefreshFlightNavigation(EntityUid uid)
    {
        _flightNavigationRefreshes++;
        var hadOld = _flightNavigationRegistrations.TryGetValue(uid, out var oldRegistration);
        var hasNew = TryGetFlightNavigationRegistration(uid, out var newRegistration);

        if (hadOld && hasNew && oldRegistration == newRegistration)
            return;

        if (hadOld)
            RemoveFlightNavigationLocations(uid, oldRegistration);

        if (!hasNew)
        {
            if (hadOld)
            {
                _flightNavigationRegistrations.Remove(uid);
                InvalidateTopology(oldRegistration.MapId);
            }

            return;
        }

        _flightNavigationRegistrations[uid] = newRegistration;
        AddFlightNavigationLocations(uid, newRegistration);
        if (hadOld && oldRegistration.MapId != newRegistration.MapId)
            InvalidateTopology(oldRegistration.MapId);
        InvalidateTopology(newRegistration.MapId);
    }

    public ZLevelTraversalEdgeStatus TryResolveFlightEdge(
        in ZLevelFlightNavigationEdge expected,
        out ZLevelFlightNavigationEdge edge)
    {
        edge = default;
        var status = TryResolveFlightEdges(
            expected.Source.Marker,
            out var forward,
            out var reverse,
            out var hasReverse);
        if (status != ZLevelTraversalEdgeStatus.Valid)
            return status;

        if (HasEquivalentFlightEdge(expected, forward))
        {
            edge = forward;
            return ZLevelTraversalEdgeStatus.Valid;
        }

        if (hasReverse && HasEquivalentFlightEdge(expected, reverse))
        {
            edge = reverse;
            return ZLevelTraversalEdgeStatus.Valid;
        }

        _invalidFlightEdges++;
        return ZLevelTraversalEdgeStatus.Invalid;
    }

    public static bool HasEquivalentFlightEdge(
        ZLevelFlightNavigationEdge left,
        ZLevelFlightNavigationEdge right)
    {
        return left.Source == right.Source &&
               left.Destination == right.Destination &&
               left.ApertureTile == right.ApertureTile &&
               left.LowerLocalZ == right.LowerLocalZ &&
               left.ZOffset == right.ZOffset &&
               left.Cost.Equals(right.Cost);
    }

    private void GetFlightEdges(MapId mapId, List<ZLevelFlightNavigationEdge> results)
    {
        results.Clear();
        _flightNavigationEntityBuffer.Clear();
        _flightNavigationEntityBuffer.AddRange(_flightNavigationRegistrations.Keys);
        _flightNavigationEntityBuffer.Sort();

        foreach (var uid in _flightNavigationEntityBuffer)
        {
            if (!_flightNavigationRegistrations.TryGetValue(uid, out var registration) ||
                registration.MapId != mapId ||
                TryResolveFlightEdges(uid, out var forward, out var reverse, out var hasReverse) !=
                ZLevelTraversalEdgeStatus.Valid)
            {
                continue;
            }

            results.Add(forward);
            if (hasReverse)
                results.Add(reverse);
        }
    }

    private ZLevelTraversalEdgeStatus TryResolveFlightEdges(
        EntityUid marker,
        out ZLevelFlightNavigationEdge forward,
        out ZLevelFlightNavigationEdge reverse,
        out bool hasReverse)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        _flightEdgeQueries++;
        forward = default;
        reverse = default;
        hasReverse = false;

        if (!_flightNavigationRegistrations.TryGetValue(marker, out var registration) ||
            !IsValidFlightNavigationProfile(registration.Profile) ||
            !TryComp<MapGridComponent>(registration.Source.GridUid, out var grid))
        {
            _invalidFlightEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var sourceZ = registration.Source.LocalZ;
        var destinationZ = sourceZ + registration.Profile.ZOffset;
        var apertureTile = registration.Source.Tile + registration.Profile.ApertureOffset;
        var destinationTile = registration.Source.Tile + registration.Profile.DestinationOffset;

        if (!HasDirectSupport(registration.Source.GridUid, grid, registration.Source.Tile, sourceZ) ||
            !HasDirectSupport(registration.Source.GridUid, grid, destinationTile, destinationZ))
        {
            _unsupportedFlightEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.MissingDestinationSupport;
        }

        if (!_boundaries.CanBodyPass(
                registration.Source.GridUid,
                grid,
                apertureTile,
                sourceZ,
                destinationZ))
        {
            _closedFlightEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.ClosedBoundary;
        }

        var sourceWorldZ = _transform.LocalToWorldZLevel(registration.Source.GridUid, sourceZ);
        var source = new ZLevelFlightNavigationNode(
            marker,
            registration.Source.GridUid,
            registration.Source.Tile,
            sourceZ,
            sourceWorldZ,
            registration.MapId);
        var destination = new ZLevelFlightNavigationNode(
            marker,
            registration.Source.GridUid,
            destinationTile,
            destinationZ,
            sourceWorldZ + registration.Profile.ZOffset,
            registration.MapId);
        var horizontalCost = ManhattanLength(registration.Profile.ApertureOffset) +
                             ManhattanLength(registration.Profile.DestinationOffset -
                                             registration.Profile.ApertureOffset);
        var cost = registration.Profile.NavigationCost + horizontalCost;
        if (!float.IsFinite(cost) || cost < 0f)
        {
            _invalidFlightEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var version = GetVersion(registration.MapId);
        forward = new ZLevelFlightNavigationEdge(
            source,
            destination,
            apertureTile,
            Math.Min(sourceZ, destinationZ),
            registration.Profile.ZOffset,
            cost,
            version.TopologyRevision,
            version.EnvironmentRevision);
        hasReverse = registration.Profile.Bidirectional;
        if (hasReverse)
        {
            reverse = new ZLevelFlightNavigationEdge(
                destination,
                source,
                apertureTile,
                Math.Min(sourceZ, destinationZ),
                -registration.Profile.ZOffset,
                cost,
                version.TopologyRevision,
                version.EnvironmentRevision);
        }

        _validFlightEdges += hasReverse ? 2 : 1;
        RecordQueryTime(started);
        return ZLevelTraversalEdgeStatus.Valid;
    }

    private bool TryGetFlightNavigationRegistration(
        EntityUid uid,
        out FlightNavigationRegistration registration)
    {
        registration = default;
        if (!TryComp<ZLevelFlightNavigationComponent>(uid, out var component) ||
            !TryComp(uid, out TransformComponent? transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            transform.MapID == MapId.Nullspace)
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var localZ = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
        var rotation = transform.LocalRotation.GetCardinalDir().ToAngle();
        var profile = new ZLevelFlightNavigationProfile(
            component.ZOffset,
            component.ApertureOffset.Rotate(rotation),
            component.DestinationOffset.Rotate(rotation),
            component.Bidirectional,
            component.NavigationCost);
        registration = new FlightNavigationRegistration(
            new ZLevelTraversalNodeKey(gridUid, tile, localZ),
            transform.MapID,
            profile);
        return true;
    }

    private static bool IsValidFlightNavigationProfile(ZLevelFlightNavigationProfile profile)
    {
        return profile.ZOffset is -1 or 1 &&
               ManhattanLength(profile.ApertureOffset) <= MaximumFlightNavigationOffset &&
               ManhattanLength(profile.DestinationOffset - profile.ApertureOffset) <=
               MaximumFlightNavigationOffset &&
               float.IsFinite(profile.NavigationCost) &&
               profile.NavigationCost >= 0f &&
               profile.NavigationCost <= MaximumFlightNavigationCost;
    }

    private static int ManhattanLength(Vector2i value)
    {
        return Math.Abs(value.X) + Math.Abs(value.Y);
    }

    private void AddFlightNavigationLocations(EntityUid uid, FlightNavigationRegistration registration)
    {
        BuildFlightNavigationKeys(registration, _flightNavigationKeyBuffer);
        foreach (var key in _flightNavigationKeyBuffer)
        {
            if (!_flightNavigationByLocation.TryGetValue(key, out var markers))
            {
                markers = new List<EntityUid>();
                _flightNavigationByLocation.Add(key, markers);
            }

            var index = markers.BinarySearch(uid);
            if (index < 0)
                markers.Insert(~index, uid);
        }
    }

    private void RemoveFlightNavigationLocations(EntityUid uid, FlightNavigationRegistration registration)
    {
        BuildFlightNavigationKeys(registration, _flightNavigationKeyBuffer);
        foreach (var key in _flightNavigationKeyBuffer)
        {
            if (!_flightNavigationByLocation.TryGetValue(key, out var markers))
                continue;

            markers.Remove(uid);
            if (markers.Count == 0)
                _flightNavigationByLocation.Remove(key);
        }
    }

    private static void BuildFlightNavigationKeys(
        FlightNavigationRegistration registration,
        List<ZLevelTraversalNodeKey> keys)
    {
        keys.Clear();
        AddUniqueKey(keys, registration.Source);
        if (!IsValidFlightNavigationProfile(registration.Profile))
            return;

        var destinationZ = registration.Source.LocalZ + registration.Profile.ZOffset;
        var aperture = registration.Source.Tile + registration.Profile.ApertureOffset;
        AddUniqueKey(keys, registration.Source with { Tile = aperture });
        AddUniqueKey(keys, registration.Source with { Tile = aperture, LocalZ = destinationZ });
        AddUniqueKey(keys, registration.Source with
        {
            Tile = registration.Source.Tile + registration.Profile.DestinationOffset,
            LocalZ = destinationZ,
        });
    }

    private static void AddUniqueKey(List<ZLevelTraversalNodeKey> keys, ZLevelTraversalNodeKey key)
    {
        if (!keys.Contains(key))
            keys.Add(key);
    }

    private bool TryGetRelevantFlightNavigationMap(
        EntityUid gridUid,
        Vector2i tile,
        int localZ,
        out MapId mapId)
    {
        mapId = MapId.Nullspace;
        if (!_flightNavigationByLocation.TryGetValue(
                new ZLevelTraversalNodeKey(gridUid, tile, localZ),
                out var markers))
        {
            return false;
        }

        foreach (var marker in markers)
        {
            if (!_flightNavigationRegistrations.TryGetValue(marker, out var registration))
                continue;

            mapId = registration.MapId;
            return true;
        }

        return false;
    }

    private bool TryGetFlightNavigationMap(EntityUid gridUid, out MapId mapId)
    {
        foreach (var registration in _flightNavigationRegistrations.Values)
        {
            if (registration.Source.GridUid != gridUid)
                continue;

            mapId = registration.MapId;
            return true;
        }

        mapId = MapId.Nullspace;
        return false;
    }

    private void RemoveFlightNavigationRegistration(EntityUid uid)
    {
        if (!_flightNavigationRegistrations.Remove(uid, out var registration))
            return;

        RemoveFlightNavigationLocations(uid, registration);
        InvalidateTopology(registration.MapId);
    }

    private void OnFlightNavigationStartup(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref ComponentStartup args)
    {
        RefreshFlightNavigation(entity.Owner);
    }

    private void OnFlightNavigationShutdown(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref ComponentShutdown args)
    {
        RemoveFlightNavigationRegistration(entity.Owner);
    }

    private void OnFlightNavigationMoved(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref MoveEvent args)
    {
        RefreshFlightNavigation(entity.Owner);
    }

    private void OnFlightNavigationParentChanged(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref EntParentChangedMessage args)
    {
        RefreshFlightNavigation(entity.Owner);
    }

    private void OnFlightNavigationAnchorChanged(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref AnchorStateChangedEvent args)
    {
        RefreshFlightNavigation(entity.Owner);
    }

    private void OnFlightNavigationZChanged(
        Entity<ZLevelFlightNavigationComponent> entity,
        ref ZLevelPositionChangedEvent args)
    {
        RefreshFlightNavigation(entity.Owner);
    }

    private void ResetFlightNavigationMetrics()
    {
        _flightNavigationRefreshes = 0;
        _flightEdgeQueries = 0;
        _validFlightEdges = 0;
        _closedFlightEdges = 0;
        _unsupportedFlightEdges = 0;
        _invalidFlightEdges = 0;
    }

    private readonly record struct FlightNavigationRegistration(
        ZLevelTraversalNodeKey Source,
        MapId MapId,
        ZLevelFlightNavigationProfile Profile);

    private sealed class ZLevelFlightNavigationEdgeComparer : IComparer<ZLevelFlightNavigationEdge>
    {
        public static readonly ZLevelFlightNavigationEdgeComparer Instance = new();

        public int Compare(ZLevelFlightNavigationEdge left, ZLevelFlightNavigationEdge right)
        {
            var comparison = left.Source.WorldZ.CompareTo(right.Source.WorldZ);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.GridUid.CompareTo(right.Source.GridUid);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.Tile.X.CompareTo(right.Source.Tile.X);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.Tile.Y.CompareTo(right.Source.Tile.Y);
            if (comparison != 0)
                return comparison;

            comparison = left.Destination.WorldZ.CompareTo(right.Destination.WorldZ);
            if (comparison != 0)
                return comparison;

            return left.Source.Marker.CompareTo(right.Source.Marker);
        }
    }
}

public readonly record struct ZLevelFlightNavigationProfile(
    int ZOffset,
    Vector2i ApertureOffset,
    Vector2i DestinationOffset,
    bool Bidirectional,
    float NavigationCost);

public readonly record struct ZLevelFlightNavigationNode(
    EntityUid Marker,
    EntityUid GridUid,
    Vector2i Tile,
    int LocalZ,
    int WorldZ,
    MapId MapId);

public readonly record struct ZLevelFlightNavigationEdge(
    ZLevelFlightNavigationNode Source,
    ZLevelFlightNavigationNode Destination,
    Vector2i ApertureTile,
    int LowerLocalZ,
    int ZOffset,
    float Cost,
    long TopologyRevision,
    long EnvironmentRevision);
