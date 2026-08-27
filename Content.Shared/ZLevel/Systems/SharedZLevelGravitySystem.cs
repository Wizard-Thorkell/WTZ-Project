// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Linq;
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

        var grids = _pendingWeightlessRefresh.ToArray();
        _pendingWeightlessRefresh.Clear();
        foreach (var gridUid in grids)
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
        if (_caches.TryGetValue(gridUid, out var cached))
        {
            _metrics.RecordGravityCacheAccess(true);
            return cached;
        }

        _metrics.RecordGravityCacheAccess(false);
        var started = Stopwatch.GetTimestamp();

        var sources = GetSources(gridUid, grid);
        var liveTiles = _map.GetAllNonEmptyZLevelTiles(gridUid, grid)
            .Select(tile => tile.GridIndices)
            .ToHashSet();
        var seeds = sources
            .Select(source => new ZLevelGravitySeed(source.Node, source.Node.Z, source.Uid))
            .ToArray();
        var assignments = ZLevelGravitySolver.Solve(liveTiles, seeds);
        var columns = new Dictionary<Vector2i, List<GravityFieldNode>>();

        foreach (var (node, assignment) in assignments)
        {
            var xy = new Vector2i(node.X, node.Y);
            if (!columns.TryGetValue(xy, out var column))
            {
                column = new List<GravityFieldNode>();
                columns.Add(xy, column);
            }

            column.Add(new GravityFieldNode(node.Z, assignment.TargetLevel, assignment.Distance));
        }

        cached = new GravityFieldCache(columns);
        _caches[gridUid] = cached;
        _metrics.RecordGravityBuild(
            liveTiles.Count,
            sources.Count,
            Stopwatch.GetTimestamp() - started);
        return cached;
    }

    private List<GravitySource> GetSources(EntityUid gridUid, MapGridComponent grid)
    {
        var sources = new List<GravitySource>();
        var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var generator, out var transform))
        {
            if (!generator.GravityActive || transform.GridUid != gridUid)
                continue;

            var xy = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
            var z = _transform.GetZLevel((uid, transform, _positionQuery.CompOrNull(uid)));
            sources.Add(new GravitySource(uid, new ZLevelTileIndices(xy.X, xy.Y, z)));
        }

        sources.Sort((left, right) => left.Uid.CompareTo(right.Uid));
        return sources;
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
            InvalidateGrid(oldGridUid);

        InvalidateCurrentGrid(entity.Owner);
    }

    private void OnGridGravityChanged(ref GravityChangedEvent args)
    {
        if (_gridQuery.HasComp(args.ChangedGridIndex))
            InvalidateGrid(args.ChangedGridIndex);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        InvalidateGrid(args.Entity.Owner);
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        InvalidateGrid(args.Entity.Owner);
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
            InvalidateGrid(gridUid);
    }

    private void InvalidateGrid(EntityUid gridUid)
    {
        var removed = _caches.Remove(gridUid);
        var managed = IsManagedGrid(gridUid);
        if (!removed && !managed)
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

    private sealed record GravityFieldCache(Dictionary<Vector2i, List<GravityFieldNode>> Columns);

    private readonly record struct GravitySource(EntityUid Uid, ZLevelTileIndices Node);

    private readonly record struct GravityFieldNode(int Level, int TargetLevel, int SourceDistance);
}
