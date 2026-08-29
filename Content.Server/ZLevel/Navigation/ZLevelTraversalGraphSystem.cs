// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Immutable;
using System.Diagnostics;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Placement;

namespace Content.Server.ZLevel.Navigation;

/// <summary>
/// Maintains the authored vertical traversal graph independently from local 2D navigation.
/// </summary>
public sealed class ZLevelTraversalGraphSystem : EntitySystem
{
    public const int ConnectedTraversalVisitBudget = 512;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;

    private readonly Dictionary<EntityUid, TraversalRegistration> _registrations = new();
    private readonly Dictionary<ZLevelTraversalNodeKey, List<EntityUid>> _byLocation = new();
    private readonly Queue<Vector2i> _connectedPending = new();
    private readonly HashSet<Vector2i> _connectedVisited = new();
    private readonly List<EntityUid> _entityBuffer = new();
    private readonly List<ZLevelTraversalNavigationEdge> _edgeBuffer = new();
    private readonly Dictionary<MapId, ZLevelTraversalGraphSnapshot> _snapshotCache = new();

    private long _topologyRevision;
    private long _environmentRevision;
    private long _refreshes;
    private long _locationQueries;
    private long _locationHits;
    private long _connectedQueries;
    private long _connectedVisits;
    private long _connectedBudgetExhaustions;
    private long _edgeQueries;
    private long _validEdges;
    private long _closedEdges;
    private long _unsupportedEdges;
    private long _invalidEdges;
    private long _snapshotRequests;
    private long _snapshotCacheHits;
    private long _snapshotBuilds;
    private long _snapshotEdges;
    private long _snapshotTimestampTicks;
    private long _lastSnapshotTimestampTicks;
    private long _maxSnapshotTimestampTicks;
    private long _snapshotAllocatedBytes;
    private long _lastSnapshotAllocatedBytes;
    private long _maxSnapshotAllocatedBytes;
    private long _queryTimestampTicks;
    private long _lastQueryTimestampTicks;
    private long _maxQueryTimestampTicks;

    public int NodeCount => _registrations.Count;
    public int LocationCount => _byLocation.Count;
    public long TopologyRevision => _topologyRevision;
    public long EnvironmentRevision => _environmentRevision;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelTraversalComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelTraversalComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelTraversalComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<ZLevelTraversalComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<ZLevelTraversalComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ZLevelTraversalComponent, ZLevelPositionChangedEvent>(OnZLevelChanged);
        SubscribeLocalEvent<PlacementEntityEvent>(OnPlacement);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
        SubscribeLocalEvent<ZLevelFrameChangedEvent>(OnFrameChanged);
        SubscribeLocalEvent<MapRemovedEvent>(OnMapRemoved);
    }

    /// <summary>
    /// Copies traversal entities at an exact floor tile in deterministic order.
    /// </summary>
    public void GetTraversalsAt(
        EntityUid gridUid,
        Vector2i tile,
        int localZ,
        List<EntityUid> results)
    {
        results.Clear();
        _locationQueries++;

        if (!_byLocation.TryGetValue(new ZLevelTraversalNodeKey(gridUid, tile, localZ), out var entities))
            return;

        _locationHits++;
        results.AddRange(entities);
    }

    /// <summary>
    /// Copies traversal entities occupying the same tile and local floor as an entity.
    /// </summary>
    public void GetTraversalsAt(EntityUid entity, List<EntityUid> results)
    {
        results.Clear();
        if (!TryGetNodeKey(entity, out var key))
        {
            _locationQueries++;
            return;
        }

        GetTraversalsAt(key.GridUid, key.Tile, key.LocalZ, results);
    }

    /// <summary>
    /// Finds a traversal with identical semantics in the origin's contiguous four-way region.
    /// </summary>
    public bool TryGetConnectedTraversal(
        EntityUid origin,
        Vector2i targetTile,
        out EntityUid traversal)
    {
        var started = Stopwatch.GetTimestamp();
        _connectedQueries++;
        traversal = default;

        if (!_registrations.TryGetValue(origin, out var registration))
        {
            RecordQueryTime(started);
            return false;
        }

        _connectedPending.Clear();
        _connectedVisited.Clear();
        _connectedVisited.Add(registration.Key.Tile);
        _connectedPending.Enqueue(registration.Key.Tile);

        while (_connectedPending.TryDequeue(out var tile))
        {
            _connectedVisits++;
            if (_connectedVisited.Count > ConnectedTraversalVisitBudget)
            {
                _connectedBudgetExhaustions++;
                RecordQueryTime(started);
                return false;
            }

            if (tile == targetTile &&
                TryGetMatchingTraversal(registration.Key with { Tile = tile }, registration.Profile, out traversal))
            {
                RecordQueryTime(started);
                return true;
            }

            TryQueueConnected(tile + new Vector2i(1, 0), registration);
            TryQueueConnected(tile + new Vector2i(-1, 0), registration);
            TryQueueConnected(tile + new Vector2i(0, 1), registration);
            TryQueueConnected(tile + new Vector2i(0, -1), registration);
        }

        RecordQueryTime(started);
        return false;
    }

    /// <summary>
    /// Resolves a directed authored connector into a currently usable navigation edge.
    /// </summary>
    public ZLevelTraversalEdgeStatus TryResolveEdge(
        EntityUid traversal,
        out ZLevelTraversalNavigationEdge edge)
    {
        var started = Stopwatch.GetTimestamp();
        _edgeQueries++;
        edge = default;

        if (!_registrations.TryGetValue(traversal, out var registration) ||
            registration.Profile.ZOffset is not (-1 or 1) ||
            !TryComp<MapGridComponent>(registration.Key.GridUid, out var grid))
        {
            _invalidEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.Invalid;
        }

        var sourceZ = registration.Key.LocalZ;
        var destinationZ = sourceZ + registration.Profile.ZOffset;
        if (!_boundaries.CanTraverse(
                registration.Key.GridUid,
                grid,
                registration.Key.Tile,
                sourceZ,
                destinationZ))
        {
            _closedEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.ClosedBoundary;
        }

        if (registration.Profile.RequireDirectDestinationSupport &&
            !HasDirectSupport(registration.Key.GridUid, grid, registration.Key.Tile, destinationZ))
        {
            _unsupportedEdges++;
            RecordQueryTime(started);
            return ZLevelTraversalEdgeStatus.MissingDestinationSupport;
        }

        var worldSourceZ = _transform.LocalToWorldZLevel(registration.Key.GridUid, sourceZ);
        var mapId = Transform(registration.Key.GridUid).MapID;
        var source = new ZLevelTraversalNavigationNode(
            traversal,
            registration.Key.GridUid,
            registration.Key.Tile,
            sourceZ,
            worldSourceZ,
            mapId,
            registration.Profile.Kind);
        var destination = source with
        {
            LocalZ = destinationZ,
            WorldZ = worldSourceZ + registration.Profile.ZOffset,
        };
        edge = new ZLevelTraversalNavigationEdge(
            source,
            destination,
            registration.Profile.ZOffset,
            Math.Max(0f, registration.Profile.NavigationCost),
            registration.Profile.TraversalDelay,
            registration.Profile.RequireDirectDestinationSupport,
            _topologyRevision,
            _environmentRevision);
        _validEdges++;
        RecordQueryTime(started);
        return ZLevelTraversalEdgeStatus.Valid;
    }

    /// <summary>
    /// Copies all currently valid authored edges on a map in deterministic order.
    /// </summary>
    public void GetEdges(MapId mapId, List<ZLevelTraversalNavigationEdge> results)
    {
        results.Clear();
        _entityBuffer.Clear();
        _entityBuffer.AddRange(_registrations.Keys);
        _entityBuffer.Sort();

        foreach (var uid in _entityBuffer)
        {
            if (!_registrations.TryGetValue(uid, out var registration) || registration.MapId != mapId)
                continue;

            if (TryResolveEdge(uid, out var edge) == ZLevelTraversalEdgeStatus.Valid)
                results.Add(edge);
        }
    }

    /// <summary>
    /// Copies the currently usable traversal graph into a deterministic snapshot.
    /// The returned data is safe to inspect without reading live ECS state.
    /// </summary>
    public ZLevelTraversalGraphSnapshot CreateSnapshot(MapId mapId)
    {
        _snapshotRequests++;
        if (_snapshotCache.TryGetValue(mapId, out var cached) &&
            cached.TopologyRevision == _topologyRevision &&
            cached.EnvironmentRevision == _environmentRevision)
        {
            _snapshotCacheHits++;
            return cached;
        }

        var started = Stopwatch.GetTimestamp();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        GetEdges(mapId, _edgeBuffer);
        _edgeBuffer.Sort(ZLevelTraversalNavigationEdgeComparer.Instance);
        var edges = _edgeBuffer.ToImmutableArray();
        var snapshot = new ZLevelTraversalGraphSnapshot(
            mapId,
            _topologyRevision,
            _environmentRevision,
            edges);
        _snapshotCache[mapId] = snapshot;

        var elapsed = Stopwatch.GetTimestamp() - started;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        _snapshotBuilds++;
        _snapshotEdges += edges.Length;
        _snapshotTimestampTicks += elapsed;
        _lastSnapshotTimestampTicks = elapsed;
        _maxSnapshotTimestampTicks = Math.Max(_maxSnapshotTimestampTicks, elapsed);
        _snapshotAllocatedBytes += allocated;
        _lastSnapshotAllocatedBytes = allocated;
        _maxSnapshotAllocatedBytes = Math.Max(_maxSnapshotAllocatedBytes, allocated);
        return snapshot;
    }

    /// <summary>
    /// Compares a detached snapshot with the live graph without inspecting its edges.
    /// </summary>
    public ZLevelTraversalGraphSnapshotStatus ValidateSnapshot(in ZLevelTraversalGraphSnapshot snapshot)
    {
        var topologyChanged = snapshot.TopologyRevision != _topologyRevision;
        var environmentChanged = snapshot.EnvironmentRevision != _environmentRevision;

        if (topologyChanged && environmentChanged)
            return ZLevelTraversalGraphSnapshotStatus.TopologyAndEnvironmentChanged;
        if (topologyChanged)
            return ZLevelTraversalGraphSnapshotStatus.TopologyChanged;
        if (environmentChanged)
            return ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged;
        return ZLevelTraversalGraphSnapshotStatus.Current;
    }

    public ZLevelTraversalGraphMetricsSnapshot Snapshot()
    {
        return new ZLevelTraversalGraphMetricsSnapshot(
            NodeCount,
            LocationCount,
            _topologyRevision,
            _environmentRevision,
            _refreshes,
            _locationQueries,
            _locationHits,
            _connectedQueries,
            _connectedVisits,
            _connectedBudgetExhaustions,
            _edgeQueries,
            _validEdges,
            _closedEdges,
            _unsupportedEdges,
            _invalidEdges,
            _snapshotCache.Count,
            _snapshotRequests,
            _snapshotCacheHits,
            _snapshotBuilds,
            _snapshotEdges,
            TimestampToMilliseconds(_snapshotTimestampTicks),
            TimestampToMilliseconds(_lastSnapshotTimestampTicks),
            TimestampToMilliseconds(_maxSnapshotTimestampTicks),
            _snapshotAllocatedBytes,
            _lastSnapshotAllocatedBytes,
            _maxSnapshotAllocatedBytes,
            TimestampToMilliseconds(_queryTimestampTicks),
            TimestampToMilliseconds(_lastQueryTimestampTicks),
            TimestampToMilliseconds(_maxQueryTimestampTicks));
    }

    public void ResetMetrics()
    {
        _refreshes = 0;
        _locationQueries = 0;
        _locationHits = 0;
        _connectedQueries = 0;
        _connectedVisits = 0;
        _connectedBudgetExhaustions = 0;
        _edgeQueries = 0;
        _validEdges = 0;
        _closedEdges = 0;
        _unsupportedEdges = 0;
        _invalidEdges = 0;
        _snapshotRequests = 0;
        _snapshotCacheHits = 0;
        _snapshotBuilds = 0;
        _snapshotEdges = 0;
        _snapshotTimestampTicks = 0;
        _lastSnapshotTimestampTicks = 0;
        _maxSnapshotTimestampTicks = 0;
        _snapshotAllocatedBytes = 0;
        _lastSnapshotAllocatedBytes = 0;
        _maxSnapshotAllocatedBytes = 0;
        _queryTimestampTicks = 0;
        _lastQueryTimestampTicks = 0;
        _maxQueryTimestampTicks = 0;
    }

    public void RefreshTraversal(EntityUid uid)
    {
        RefreshRegistration(uid);
    }

    private void OnStartup(Entity<ZLevelTraversalComponent> entity, ref ComponentStartup args)
    {
        RefreshRegistration(entity.Owner);
    }

    private void OnShutdown(Entity<ZLevelTraversalComponent> entity, ref ComponentShutdown args)
    {
        RemoveRegistration(entity.Owner);
    }

    private void OnMoved(Entity<ZLevelTraversalComponent> entity, ref MoveEvent args)
    {
        RefreshRegistration(entity.Owner);
    }

    private void OnParentChanged(Entity<ZLevelTraversalComponent> entity, ref EntParentChangedMessage args)
    {
        RefreshRegistration(entity.Owner);
    }

    private void OnAnchorChanged(Entity<ZLevelTraversalComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshRegistration(entity.Owner);
    }

    private void OnZLevelChanged(Entity<ZLevelTraversalComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        RefreshRegistration(entity.Owner);
    }

    private void OnPlacement(PlacementEntityEvent args)
    {
        if (args.PlacementEventAction == PlacementEventAction.Create &&
            HasComp<ZLevelTraversalComponent>(args.EditedEntity))
        {
            RefreshRegistration(args.EditedEntity);
        }
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            InvalidateEnvironmentAt(args.Entity.Owner, change.GridIndices, 0);
        }
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            InvalidateEnvironmentAt(
                args.Entity.Owner,
                new Vector2i(change.GridIndices.X, change.GridIndices.Y),
                change.GridIndices.Z);
        }
    }

    private void OnBoundaryChanged(ref ZLevelBoundaryChangedEvent args)
    {
        if (HasRelevantTraversal(args.Grid.Owner, args.Tile, args.LowerZ) ||
            HasRelevantTraversal(args.Grid.Owner, args.Tile, args.LowerZ + 1))
        {
            _environmentRevision++;
        }
    }

    private void OnFrameChanged(ref ZLevelFrameChangedEvent args)
    {
        foreach (var registration in _registrations.Values)
        {
            if (registration.Key.GridUid != args.GridUid)
                continue;

            _environmentRevision++;
            return;
        }
    }

    private void OnMapRemoved(MapRemovedEvent args)
    {
        _snapshotCache.Remove(args.MapId);
    }

    private void RefreshRegistration(EntityUid uid)
    {
        _refreshes++;
        var hadOld = _registrations.TryGetValue(uid, out var oldRegistration);
        var hasNew = TryGetRegistration(uid, out var newRegistration);

        if (hadOld && hasNew && oldRegistration == newRegistration)
            return;

        if (hadOld)
            RemoveFromLocation(uid, oldRegistration.Key);

        if (!hasNew)
        {
            if (hadOld)
            {
                _registrations.Remove(uid);
                _topologyRevision++;
            }

            return;
        }

        _registrations[uid] = newRegistration;
        if (!_byLocation.TryGetValue(newRegistration.Key, out var entities))
        {
            entities = new List<EntityUid>();
            _byLocation.Add(newRegistration.Key, entities);
        }

        var insertionIndex = entities.BinarySearch(uid);
        if (insertionIndex < 0)
            entities.Insert(~insertionIndex, uid);
        _topologyRevision++;
    }

    private void RemoveRegistration(EntityUid uid)
    {
        if (!_registrations.Remove(uid, out var registration))
            return;

        RemoveFromLocation(uid, registration.Key);
        _topologyRevision++;
    }

    private void RemoveFromLocation(EntityUid uid, ZLevelTraversalNodeKey key)
    {
        if (!_byLocation.TryGetValue(key, out var entities))
            return;

        entities.Remove(uid);
        if (entities.Count == 0)
            _byLocation.Remove(key);
    }

    private bool TryGetRegistration(EntityUid uid, out TraversalRegistration registration)
    {
        registration = default;
        if (!TryComp<ZLevelTraversalComponent>(uid, out var traversal) ||
            !TryComp(uid, out TransformComponent? transform) ||
            transform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            transform.MapID == MapId.Nullspace)
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var localZ = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
        var profile = new ZLevelTraversalProfile(
            traversal.Kind,
            traversal.ZOffset,
            traversal.RequireDirectDestinationSupport,
            traversal.TraversalDelay,
            traversal.NavigationCost);
        registration = new TraversalRegistration(
            new ZLevelTraversalNodeKey(gridUid, tile, localZ),
            transform.MapID,
            profile);
        return true;
    }

    private bool TryGetNodeKey(EntityUid uid, out ZLevelTraversalNodeKey key)
    {
        key = default;
        if (!TryComp(uid, out TransformComponent? transform) ||
            transform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var localZ = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
        key = new ZLevelTraversalNodeKey(gridUid, tile, localZ);
        return true;
    }

    private bool TryGetMatchingTraversal(
        ZLevelTraversalNodeKey key,
        ZLevelTraversalProfile profile,
        out EntityUid traversal)
    {
        traversal = default;
        if (!_byLocation.TryGetValue(key, out var entities))
            return false;

        foreach (var candidate in entities)
        {
            if (!_registrations.TryGetValue(candidate, out var registration) ||
                !HasEquivalentConnectedBehavior(registration.Profile, profile))
                continue;

            traversal = candidate;
            return true;
        }

        return false;
    }

    private static bool HasEquivalentConnectedBehavior(
        ZLevelTraversalProfile left,
        ZLevelTraversalProfile right)
    {
        return left.Kind == right.Kind &&
               left.ZOffset == right.ZOffset &&
               left.RequireDirectDestinationSupport == right.RequireDirectDestinationSupport &&
               left.TraversalDelay == right.TraversalDelay;
    }

    private void TryQueueConnected(
        Vector2i tile,
        TraversalRegistration origin)
    {
        if (!_connectedVisited.Contains(tile) &&
            TryGetMatchingTraversal(origin.Key with { Tile = tile }, origin.Profile, out _) &&
            _connectedVisited.Add(tile))
        {
            _connectedPending.Enqueue(tile);
        }
    }

    private bool HasDirectSupport(EntityUid gridUid, MapGridComponent grid, Vector2i tile, int localZ)
    {
        var floor = _map.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(tile.X, tile.Y, localZ));
        return !floor.Tile.IsEmpty && !_boundaries.CanBodyPass(gridUid, grid, tile, localZ, localZ - 1);
    }

    private void InvalidateEnvironmentAt(EntityUid gridUid, Vector2i tile, int localZ)
    {
        if (HasRelevantTraversal(gridUid, tile, localZ) ||
            HasRelevantTraversal(gridUid, tile, localZ - 1) ||
            HasRelevantTraversal(gridUid, tile, localZ + 1))
        {
            _environmentRevision++;
        }
    }

    private bool HasRelevantTraversal(EntityUid gridUid, Vector2i tile, int localZ)
    {
        return _byLocation.ContainsKey(new ZLevelTraversalNodeKey(gridUid, tile, localZ));
    }

    private void RecordQueryTime(long started)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _queryTimestampTicks += elapsed;
        _lastQueryTimestampTicks = elapsed;
        _maxQueryTimestampTicks = Math.Max(_maxQueryTimestampTicks, elapsed);
    }

    private static double TimestampToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }

    private readonly record struct TraversalRegistration(
        ZLevelTraversalNodeKey Key,
        MapId MapId,
        ZLevelTraversalProfile Profile);

    private sealed class ZLevelTraversalNavigationEdgeComparer : IComparer<ZLevelTraversalNavigationEdge>
    {
        public static readonly ZLevelTraversalNavigationEdgeComparer Instance = new();

        public int Compare(ZLevelTraversalNavigationEdge left, ZLevelTraversalNavigationEdge right)
        {
            var comparison = left.Source.WorldZ.CompareTo(right.Source.WorldZ);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.GridUid.CompareTo(right.Source.GridUid);
            if (comparison != 0)
                return comparison;

            comparison = left.Source.LocalZ.CompareTo(right.Source.LocalZ);
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

            return left.Source.Traversal.CompareTo(right.Source.Traversal);
        }
    }
}

public readonly record struct ZLevelTraversalNodeKey(EntityUid GridUid, Vector2i Tile, int LocalZ);

public readonly record struct ZLevelTraversalProfile(
    ZLevelTraversalKind Kind,
    int ZOffset,
    bool RequireDirectDestinationSupport,
    TimeSpan TraversalDelay,
    float NavigationCost);

public readonly record struct ZLevelTraversalNavigationNode(
    EntityUid Traversal,
    EntityUid GridUid,
    Vector2i Tile,
    int LocalZ,
    int WorldZ,
    MapId MapId,
    ZLevelTraversalKind Kind);

public readonly record struct ZLevelTraversalNavigationEdge(
    ZLevelTraversalNavigationNode Source,
    ZLevelTraversalNavigationNode Destination,
    int ZOffset,
    float Cost,
    TimeSpan TraversalDelay,
    bool RequireDirectDestinationSupport,
    long TopologyRevision,
    long EnvironmentRevision);

public enum ZLevelTraversalEdgeStatus : byte
{
    Valid,
    Invalid,
    ClosedBoundary,
    MissingDestinationSupport,
}

/// <summary>
/// A simulation-thread capture of valid traversal edges. Search workers may read
/// this value without touching live entity or component collections.
/// </summary>
public readonly record struct ZLevelTraversalGraphSnapshot(
    MapId MapId,
    long TopologyRevision,
    long EnvironmentRevision,
    ImmutableArray<ZLevelTraversalNavigationEdge> Edges)
{
    public ZLevelTraversalGraphVersion Version =>
        new(MapId, TopologyRevision, EnvironmentRevision);
}

public readonly record struct ZLevelTraversalGraphVersion(
    MapId MapId,
    long TopologyRevision,
    long EnvironmentRevision);

public enum ZLevelTraversalGraphSnapshotStatus : byte
{
    Current,
    TopologyChanged,
    EnvironmentChanged,
    TopologyAndEnvironmentChanged,
}

public readonly record struct ZLevelTraversalGraphMetricsSnapshot(
    int Nodes,
    int Locations,
    long TopologyRevision,
    long EnvironmentRevision,
    long Refreshes,
    long LocationQueries,
    long LocationHits,
    long ConnectedQueries,
    long ConnectedVisits,
    long ConnectedBudgetExhaustions,
    long EdgeQueries,
    long ValidEdges,
    long ClosedEdges,
    long UnsupportedEdges,
    long InvalidEdges,
    int CachedSnapshots,
    long SnapshotRequests,
    long SnapshotCacheHits,
    long SnapshotBuilds,
    long SnapshotEdges,
    double TotalSnapshotMilliseconds,
    double LastSnapshotMilliseconds,
    double MaxSnapshotMilliseconds,
    long TotalSnapshotAllocatedBytes,
    long LastSnapshotAllocatedBytes,
    long MaxSnapshotAllocatedBytes,
    double TotalQueryMilliseconds,
    double LastQueryMilliseconds,
    double MaxQueryMilliseconds)
{
    public double LocationHitPercent => LocationQueries == 0 ? 0d : LocationHits * 100d / LocationQueries;
    public double AverageQueryMilliseconds => ConnectedQueries + EdgeQueries == 0
        ? 0d
        : TotalQueryMilliseconds / (ConnectedQueries + EdgeQueries);
    public double AverageSnapshotMilliseconds => SnapshotBuilds == 0
        ? 0d
        : TotalSnapshotMilliseconds / SnapshotBuilds;
    public double AverageSnapshotAllocatedBytes => SnapshotBuilds == 0
        ? 0d
        : TotalSnapshotAllocatedBytes / (double) SnapshotBuilds;
    public double SnapshotHitPercent => SnapshotRequests == 0
        ? 0d
        : SnapshotCacheHits * 100d / SnapshotRequests;
}
