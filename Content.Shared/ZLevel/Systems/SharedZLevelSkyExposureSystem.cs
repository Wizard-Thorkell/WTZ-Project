// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Resolves whether a grid-local tile column reaches open sky through Weather
/// boundaries. Weather rendering and gameplay remain consumer policy.
/// </summary>
public sealed class SharedZLevelSkyExposureSystem : EntitySystem
{
    public const int DefaultCacheCapacity = 4_096;
    public const int MinimumCacheCapacity = 64;
    public const int MaximumCacheCapacity = 65_536;
    public const int DefaultMaxBoundaryChecks = 64;
    public const int MinimumMaxBoundaryChecks = 1;
    public const int MaximumMaxBoundaryChecks = 4_096;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<SkyExposureCacheKey, SkyExposureCacheEntry> _cache = new();
    private readonly Dictionary<SkyColumnKey, SkyColumnState> _columns = new();
    private readonly LinkedList<SkyExposureCacheKey> _cacheOrder = new();
    private readonly List<SkyExposureCacheKey> _removeScratch = new();
    private readonly HashSet<SkyColumnKey> _changedColumns = new();
    private int _cacheCapacity = DefaultCacheCapacity;
    private int _maxBoundaryChecks = DefaultMaxBoundaryChecks;

    public int CachedExposureCount => _cache.Count;
    public int CacheCapacity => _cacheCapacity;
    public int MaxBoundaryChecks => _maxBoundaryChecks;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSkyExposureCacheCapacity,
            OnCacheCapacityChanged,
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSkyExposureMaxBoundaryChecks,
            OnMaxBoundaryChecksChanged,
            true);
    }

    public override void Shutdown()
    {
        ClearCache(recordInvalidation: false);
        _removeScratch.Clear();
        _changedColumns.Clear();
        base.Shutdown();
    }

    /// <summary>
    /// Resolves exposure from one local floor through the top boundary above
    /// the map's declared maximum floor.
    /// </summary>
    public ZLevelSkyExposureState GetExposure(
        Entity<MapGridComponent> grid,
        ZLevelTileIndices origin)
    {
        if (grid.Comp.Deleted)
        {
            return FinishUncached(new ZLevelSkyExposureState(
                origin,
                ZLevelSkyExposureTermination.InvalidGrid,
                0));
        }

        var minimumZ = 0;
        var maximumZ = 0;
        if (_zLevelMaps.TryGetConfig(grid.Owner, out var config))
        {
            minimumZ = config.Comp.MinimumLevel;
            maximumZ = config.Comp.MaximumLevel;
            if (minimumZ > maximumZ || maximumZ == int.MaxValue)
            {
                return FinishUncached(new ZLevelSkyExposureState(
                    origin,
                    ZLevelSkyExposureTermination.InvalidConfiguration,
                    0));
            }
        }

        if (origin.Z < minimumZ || origin.Z > maximumZ)
        {
            return FinishUncached(new ZLevelSkyExposureState(
                origin,
                ZLevelSkyExposureTermination.InvalidLevel,
                0));
        }

        var key = new SkyExposureCacheKey(grid.Owner, origin);
        var columnKey = new SkyColumnKey(grid.Owner, new Vector2i(origin.X, origin.Y));
        var revision = _columns.TryGetValue(columnKey, out var column) ? column.Revision : 0;
        if (_cache.TryGetValue(key, out var cached) && cached.ColumnRevision == revision)
        {
            TouchCacheEntry(cached);
            _metrics.RecordSkyExposureQuery(cached.State.Termination, cacheHit: true, boundaryChecks: 0);
            return cached.State;
        }

        var state = BuildExposure(grid, origin, maximumZ);
        CacheState(key, columnKey, revision, state);
        _metrics.RecordSkyExposureQuery(state.Termination, cacheHit: false, state.BoundaryChecks);
        return state;
    }

    /// <summary>
    /// Resolves a world floor against the selected grid's current local frame.
    /// Cached geometry remains grid-local when a moving frame changes origin.
    /// </summary>
    public ZLevelSkyExposureState GetExposureAtWorldZ(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int worldZ)
    {
        var localZ = _transform.WorldToLocalZLevel(grid.Owner, worldZ);
        return GetExposure(grid, new ZLevelTileIndices(tile.X, tile.Y, localZ));
    }

    public bool IsExposed(
        Entity<MapGridComponent> grid,
        ZLevelTileIndices origin)
    {
        return GetExposure(grid, origin).IsExposed;
    }

    public bool IsExposedAtWorldZ(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int worldZ)
    {
        return GetExposureAtWorldZ(grid, tile, worldZ).IsExposed;
    }

    /// <summary>
    /// Invalidates every cached starting floor in one local tile column.
    /// Existing entries are lazily recomputed without scanning vertical geometry.
    /// </summary>
    public void InvalidateColumn(EntityUid gridUid, Vector2i tile)
    {
        var key = new SkyColumnKey(gridUid, tile);
        var invalidated = 0;
        if (_columns.TryGetValue(key, out var column))
        {
            column.Revision++;
            invalidated = column.EntryCount;
        }

        _metrics.RecordSkyExposureInvalidation(invalidated);
    }

    public void InvalidateAll()
    {
        ClearCache(recordInvalidation: true);
    }

    private ZLevelSkyExposureState BuildExposure(
        Entity<MapGridComponent> grid,
        ZLevelTileIndices origin,
        int maximumZ)
    {
        var tile = new Vector2i(origin.X, origin.Y);
        var lowerZ = origin.Z;
        var checks = 0;
        while (true)
        {
            if (checks >= _maxBoundaryChecks)
            {
                return new ZLevelSkyExposureState(
                    origin,
                    ZLevelSkyExposureTermination.BoundaryBudgetExceeded,
                    checks);
            }

            checks++;
            if (!_boundaries.TryGetBoundary(
                    grid.Owner,
                    grid.Comp,
                    tile,
                    lowerZ,
                    lowerZ + 1,
                    out var boundary))
            {
                return new ZLevelSkyExposureState(
                    origin,
                    ZLevelSkyExposureTermination.BoundaryResolutionFailed,
                    checks);
            }

            if (!boundary.IsOpen(ZLevelBoundaryChannels.Weather))
            {
                return new ZLevelSkyExposureState(
                    origin,
                    ZLevelSkyExposureTermination.ClosedBoundary,
                    checks,
                    lowerZ);
            }

            if (lowerZ == maximumZ)
                break;

            lowerZ++;
        }

        return new ZLevelSkyExposureState(
            origin,
            ZLevelSkyExposureTermination.Exposed,
            checks);
    }

    private ZLevelSkyExposureState FinishUncached(ZLevelSkyExposureState state)
    {
        _metrics.RecordSkyExposureQuery(state.Termination, cacheHit: null, boundaryChecks: 0);
        return state;
    }

    private void CacheState(
        SkyExposureCacheKey key,
        SkyColumnKey columnKey,
        long revision,
        ZLevelSkyExposureState state)
    {
        if (_cache.TryGetValue(key, out var existing))
        {
            existing.State = state;
            existing.ColumnRevision = revision;
            TouchCacheEntry(existing);
        }
        else
        {
            if (!_columns.TryGetValue(columnKey, out var column))
            {
                column = new SkyColumnState();
                _columns.Add(columnKey, column);
            }

            column.EntryCount++;
            revision = column.Revision;
            var node = _cacheOrder.AddLast(key);
            _cache.Add(key, new SkyExposureCacheEntry(state, revision, node));
        }

        TrimCache();
    }

    private void TouchCacheEntry(SkyExposureCacheEntry entry)
    {
        _cacheOrder.Remove(entry.Node);
        _cacheOrder.AddLast(entry.Node);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        _changedColumns.Clear();
        foreach (var change in args.Changes)
        {
            _changedColumns.Add(new SkyColumnKey(args.Entity.Owner, change.GridIndices));
        }

        InvalidateChangedColumns();
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        _changedColumns.Clear();
        foreach (var change in args.Changes)
        {
            _changedColumns.Add(new SkyColumnKey(
                args.Entity.Owner,
                new Vector2i(change.GridIndices.X, change.GridIndices.Y)));
        }

        InvalidateChangedColumns();
    }

    private void OnBoundaryChanged(ref ZLevelBoundaryChangedEvent args)
    {
        InvalidateColumn(args.Grid.Owner, args.Tile);
    }

    private void OnMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        _removeScratch.Clear();
        foreach (var key in _cache.Keys)
        {
            if (!_transformQuery.TryComp(key.GridUid, out var transform) || transform.MapUid != args.MapUid)
                continue;

            _removeScratch.Add(key);
        }

        var removed = RemoveCachedEntries(_removeScratch);
        _metrics.RecordSkyExposureInvalidation(removed);
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (!_gridQuery.HasComp(args.Entity.Owner))
            return;

        _removeScratch.Clear();
        foreach (var key in _cache.Keys)
        {
            if (key.GridUid == args.Entity.Owner)
                _removeScratch.Add(key);
        }

        var removed = RemoveCachedEntries(_removeScratch);
        _metrics.RecordSkyExposureInvalidation(removed);
    }

    private void InvalidateChangedColumns()
    {
        foreach (var column in _changedColumns)
        {
            InvalidateColumn(column.GridUid, column.Tile);
        }

        _changedColumns.Clear();
    }

    private int RemoveCachedEntries(List<SkyExposureCacheKey> keys)
    {
        var removed = 0;
        foreach (var key in keys)
        {
            if (RemoveCacheEntry(key))
                removed++;
        }

        keys.Clear();
        return removed;
    }

    private bool RemoveCacheEntry(SkyExposureCacheKey key)
    {
        if (!_cache.Remove(key, out var entry))
            return false;

        _cacheOrder.Remove(entry.Node);

        var columnKey = new SkyColumnKey(
            key.GridUid,
            new Vector2i(key.Origin.X, key.Origin.Y));
        if (_columns.TryGetValue(columnKey, out var column))
        {
            column.EntryCount--;
            if (column.EntryCount == 0)
                _columns.Remove(columnKey);
        }

        return true;
    }

    private void OnCacheCapacityChanged(int configuredCapacity)
    {
        var capacity = Math.Clamp(configuredCapacity, MinimumCacheCapacity, MaximumCacheCapacity);
        if (_cacheCapacity == capacity)
            return;

        _cacheCapacity = capacity;
        TrimCache();
    }

    private void OnMaxBoundaryChecksChanged(int configuredChecks)
    {
        var checks = Math.Clamp(
            configuredChecks,
            MinimumMaxBoundaryChecks,
            MaximumMaxBoundaryChecks);
        if (_maxBoundaryChecks == checks)
            return;

        _maxBoundaryChecks = checks;
        ClearCache(recordInvalidation: true);
    }

    private void TrimCache()
    {
        while (_cache.Count > _cacheCapacity && _cacheOrder.First is { } oldest)
        {
            if (RemoveCacheEntry(oldest.Value))
                _metrics.RecordSkyExposureEviction();
        }
    }

    private void ClearCache(bool recordInvalidation)
    {
        var removed = _cache.Count;
        _cache.Clear();
        _columns.Clear();
        _cacheOrder.Clear();
        if (recordInvalidation)
            _metrics.RecordSkyExposureInvalidation(removed);
    }

    private readonly record struct SkyExposureCacheKey(EntityUid GridUid, ZLevelTileIndices Origin);
    private readonly record struct SkyColumnKey(EntityUid GridUid, Vector2i Tile);
    private sealed class SkyExposureCacheEntry(
        ZLevelSkyExposureState state,
        long columnRevision,
        LinkedListNode<SkyExposureCacheKey> node)
    {
        public ZLevelSkyExposureState State = state;
        public long ColumnRevision = columnRevision;
        public readonly LinkedListNode<SkyExposureCacheKey> Node = node;
    }

    private sealed class SkyColumnState
    {
        public long Revision;
        public int EntryCount;
    }
}
