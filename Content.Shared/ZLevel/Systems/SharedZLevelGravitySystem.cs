// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using Content.Shared.Gravity;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Builds connected artificial-gravity fields for native Z-level maps.
/// A source affects only columns belonging to the solid tile component that contains it.
/// </summary>
public sealed class SharedZLevelGravitySystem : EntitySystem
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<ZLevelPositionComponent> _positionQuery;
    private readonly Dictionary<EntityUid, GravityFieldCache> _caches = new();
    private readonly HashSet<EntityUid> _pendingWeightlessRefresh = new();
    private readonly List<EntityUid> _pendingRefreshBuffer = new();

    public int CachedGridCount => _caches.Count;
    public int PendingRefreshGridCount => _pendingWeightlessRefresh.Count;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _positionQuery = GetEntityQuery<ZLevelPositionComponent>();

        SubscribeLocalEvent<GravityAffectedComponent, IsWeightlessEvent>(OnIsWeightless);
        SubscribeLocalEvent<GravityAffectedComponent, MoveEvent>(OnGravityAffectedMoved);
        SubscribeLocalEvent<GravityAffectedComponent, ZLevelPositionChangedEvent>(OnGravityAffectedZChanged);
        SubscribeLocalEvent<GravityGeneratorComponent, ComponentStartup>(OnSourceStartup);
        SubscribeLocalEvent<GravityGeneratorComponent, ComponentShutdown>(OnSourceShutdown);
        SubscribeLocalEvent<GravityGeneratorComponent, MoveEvent>(OnSourceMoved);
        SubscribeLocalEvent<GravityGeneratorComponent, ZLevelPositionChangedEvent>(OnSourceZChanged);
        SubscribeLocalEvent<GravityGeneratorComponent, AfterAutoHandleStateEvent>(OnSourceStateHandled);
        SubscribeLocalEvent<GravityGeneratorComponent, ZLevelGravitySourceChangedEvent>(OnSourceChanged);
        SubscribeLocalEvent<GravityChangedEvent>(OnGridGravityChanged);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<GridRemovalEvent>(OnGridRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingWeightlessRefresh.Count == 0)
            return;

        _pendingRefreshBuffer.Clear();
        _pendingRefreshBuffer.AddRange(_pendingWeightlessRefresh);
        _pendingWeightlessRefresh.Clear();
        foreach (var gridUid in _pendingRefreshBuffer)
        {
            RefreshWeightlessnessOnGrid(gridUid);
        }
    }

    public bool IsManagedGrid(EntityUid gridUid)
    {
        return _zLevelMaps.TryGetConfig(gridUid, out _);
    }

    public bool TryGetGravityTarget(EntityUid uid, out int targetLevel)
    {
        targetLevel = default;
        if (!TryComp(uid, out TransformComponent? transform) ||
            transform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var zLevel = _transform.GetZLevel((uid, transform, _positionQuery.CompOrNull(uid)));
        return TryGetGravityTarget(gridUid, grid, tile, zLevel, out targetLevel);
    }

    public bool TryGetGravityTarget(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        float zLevel,
        out int targetLevel)
    {
        _metrics.RecordGravityQuery();
        targetLevel = default;
        if (!_zLevelMaps.TryGetConfig(gridUid, out _))
            return false;

        var cache = GetCache(gridUid, grid);
        if (!cache.Columns.TryGetValue(tile, out var nodes))
            return false;

        GravityFieldNode? best = null;
        var bestVerticalDistance = float.MaxValue;
        foreach (var node in nodes)
        {
            var verticalDistance = MathF.Abs(node.Level - zLevel);
            if (verticalDistance > bestVerticalDistance ||
                verticalDistance == bestVerticalDistance && best is { } current &&
                (node.SourceDistance > current.SourceDistance ||
                 node.SourceDistance == current.SourceDistance && node.TargetLevel >= current.TargetLevel))
            {
                continue;
            }

            best = node;
            bestVerticalDistance = verticalDistance;
        }

        if (best is not { } resolved)
            return false;

        targetLevel = resolved.TargetLevel;
        return true;
    }

    private GravityFieldCache GetCache(EntityUid gridUid, MapGridComponent grid)
    {
        var reusedWorkspace = _caches.TryGetValue(gridUid, out var cached);
        if (reusedWorkspace && !cached!.Dirty)
        {
            _metrics.RecordGravityCacheAccess(true);
            return cached;
        }

        _metrics.RecordGravityCacheAccess(false);
        var started = Stopwatch.GetTimestamp();
        if (!reusedWorkspace)
        {
            cached = new GravityFieldCache();
            _caches.Add(gridUid, cached);
        }

        if (!cached!.LiveTilesCurrent)
            RefreshLiveTiles(gridUid, grid, cached);

        CollectSeeds(gridUid, grid, cached.Seeds);
        ZLevelGravitySolver.SolveSorted(
            cached.LiveTiles,
            cached.Seeds,
            cached.Assignments,
            cached.Queue);
        RebuildColumns(cached);
        cached.Dirty = false;
        cached.LiveTilesCurrent = true;
        _metrics.RecordGravityBuild(
            cached.LiveTiles.Count,
            cached.Seeds.Count,
            Stopwatch.GetTimestamp() - started,
            reusedWorkspace);
        return cached;
    }

    private void RefreshLiveTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        GravityFieldCache cache)
    {
        cache.LiveTiles.Clear();
        foreach (var tile in _map.GetAllNonEmptyZLevelTiles(gridUid, grid))
            cache.LiveTiles.Add(tile.GridIndices);
        cache.LiveTilesCurrent = true;
    }

    private void CollectSeeds(
        EntityUid gridUid,
        MapGridComponent grid,
        List<ZLevelGravitySeed> seeds)
    {
        seeds.Clear();
        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var generator, out var transform))
        {
            if (!generator.GravityActive || transform.GridUid != gridUid)
                continue;

            var xy = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
            var z = _transform.GetZLevel((uid, transform, _positionQuery.CompOrNull(uid)));
            var node = new ZLevelTileIndices(xy.X, xy.Y, z);
            seeds.Add(new ZLevelGravitySeed(node, node.Z, uid));
        }

        seeds.Sort(static (left, right) => left.Source.CompareTo(right.Source));
    }

    private static void RebuildColumns(GravityFieldCache cache)
    {
        foreach (var column in cache.Columns.Values)
            column.Clear();

        foreach (var (node, assignment) in cache.Assignments)
        {
            var xy = new Vector2i(node.X, node.Y);
            if (!cache.Columns.TryGetValue(xy, out var column))
            {
                column = cache.ColumnPool.Count == 0
                    ? new List<GravityFieldNode>()
                    : cache.ColumnPool.Pop();
                cache.Columns.Add(xy, column);
            }

            column.Add(new GravityFieldNode(node.Z, assignment.TargetLevel, assignment.Distance));
        }

        cache.EmptyColumns.Clear();
        foreach (var (xy, column) in cache.Columns)
        {
            if (column.Count == 0)
                cache.EmptyColumns.Add(xy);
        }

        foreach (var xy in cache.EmptyColumns)
        {
            var column = cache.Columns[xy];
            cache.Columns.Remove(xy);
            cache.ColumnPool.Push(column);
        }
    }

    private void OnIsWeightless(Entity<GravityAffectedComponent> entity, ref IsWeightlessEvent args)
    {
        if (args.Handled ||
            !TryComp(entity, out TransformComponent? transform) ||
            transform.GridUid is not { } gridUid ||
            !IsManagedGrid(gridUid))
        {
            return;
        }

        args.IsWeightless = !TryGetGravityTarget(entity.Owner, out _);
        args.Handled = true;
    }

    private void OnGravityAffectedMoved(Entity<GravityAffectedComponent> entity, ref MoveEvent args)
    {
        if (Transform(entity).GridUid is { } gridUid && IsManagedGrid(gridUid))
            _gravity.RefreshWeightless((entity.Owner, entity.Comp));
    }

    private void OnGravityAffectedZChanged(Entity<GravityAffectedComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        _gravity.RefreshWeightless((entity.Owner, entity.Comp));
    }

    private void OnSourceStartup(Entity<GravityGeneratorComponent> entity, ref ComponentStartup args)
    {
        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnSourceShutdown(Entity<GravityGeneratorComponent> entity, ref ComponentShutdown args)
    {
        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnSourceMoved(Entity<GravityGeneratorComponent> entity, ref MoveEvent args)
    {
        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnSourceZChanged(Entity<GravityGeneratorComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnSourceStateHandled(Entity<GravityGeneratorComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnSourceChanged(Entity<GravityGeneratorComponent> entity, ref ZLevelGravitySourceChangedEvent args)
    {
        if (args.OldGridUid is { } oldGridUid && _gridQuery.HasComp(oldGridUid))
            InvalidateGrid(oldGridUid, preserveLiveTiles: true);

        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnGridGravityChanged(ref GravityChangedEvent args)
    {
        if (_gridQuery.HasComp(args.ChangedGridIndex))
            InvalidateGrid(args.ChangedGridIndex, preserveLiveTiles: true);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        var changed = false;
        foreach (var change in args.Changes)
        {
            if (!change.EmptyChanged)
                continue;

            changed = true;
            if (!_caches.TryGetValue(args.Entity.Owner, out var cache) || !cache.LiveTilesCurrent)
                continue;

            var indices = new ZLevelTileIndices(change.GridIndices.X, change.GridIndices.Y, 0);
            if (change.NewTile.IsEmpty)
                cache.LiveTiles.Remove(indices);
            else
                cache.LiveTiles.Add(indices);
        }

        if (changed)
            InvalidateGrid(args.Entity.Owner, preserveLiveTiles: true);
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        var changed = false;
        foreach (var change in args.Changes)
        {
            if (!change.EmptyChanged)
                continue;

            changed = true;
            if (!_caches.TryGetValue(args.Entity.Owner, out var cache) || !cache.LiveTilesCurrent)
                continue;

            if (change.NewTile.IsEmpty)
                cache.LiveTiles.Remove(change.GridIndices);
            else
                cache.LiveTiles.Add(change.GridIndices);
        }

        if (changed)
            InvalidateGrid(args.Entity.Owner, preserveLiveTiles: true);
    }

    private void OnGridRemoved(GridRemovalEvent args)
    {
        if (_caches.Remove(args.EntityUid))
            _metrics.RecordGravityInvalidation();
        _pendingWeightlessRefresh.Remove(args.EntityUid);
    }

    private void InvalidateCurrentGrid(EntityUid uid)
    {
        if (TryComp(uid, out TransformComponent? transform) && transform.GridUid is { } gridUid)
            InvalidateGrid(gridUid, preserveLiveTiles: true);
    }

    /// <summary>
    /// Invalidates one grid after an external batch edit or diagnostic workload.
    /// Normal tile and source events call this automatically.
    /// </summary>
    public void InvalidateGrid(EntityUid gridUid)
    {
        InvalidateGrid(gridUid, preserveLiveTiles: false);
    }

    private void InvalidateGrid(EntityUid gridUid, bool preserveLiveTiles)
    {
        var retained = _caches.TryGetValue(gridUid, out var cache);
        if (retained)
        {
            cache!.Dirty = true;
            if (!preserveLiveTiles)
                cache.LiveTilesCurrent = false;
        }

        var managed = IsManagedGrid(gridUid);
        if (!retained && !managed)
            return;

        _metrics.RecordGravityInvalidation();
        if (managed)
            _pendingWeightlessRefresh.Add(gridUid);
    }

    private void RefreshWeightlessnessOnGrid(EntityUid gridUid)
    {
        var query = EntityQueryEnumerator<GravityAffectedComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var gravityAffected, out var transform))
        {
            if (transform.GridUid == gridUid)
                _gravity.RefreshWeightless((uid, gravityAffected));
        }
    }

    private sealed class GravityFieldCache
    {
        public readonly Dictionary<Vector2i, List<GravityFieldNode>> Columns = new();
        public readonly Stack<List<GravityFieldNode>> ColumnPool = new();
        public readonly List<Vector2i> EmptyColumns = new();
        public readonly HashSet<ZLevelTileIndices> LiveTiles = new();
        public readonly List<ZLevelGravitySeed> Seeds = new();
        public readonly Dictionary<ZLevelTileIndices, ZLevelGravityAssignment> Assignments = new();
        public readonly Queue<ZLevelTileIndices> Queue = new();
        public bool Dirty = true;
        public bool LiveTilesCurrent;
    }

    private readonly record struct GravityFieldNode(int Level, int TargetLevel, int SourceDistance);
}
