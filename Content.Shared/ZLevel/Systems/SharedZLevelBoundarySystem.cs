// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Placement;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Resolves content-driven vertical boundaries while preserving the legacy
/// upper-tile rule as the default.
/// </summary>
public sealed class SharedZLevelBoundarySystem : EntitySystem
{
    public const int DefaultBoundaryCacheCapacity = 8192;
    public const int MinimumBoundaryCacheCapacity = 256;
    public const int MaximumBoundaryCacheCapacity = 131072;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMap = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<EntityUid, BoundaryRegistration> _registrations = new();
    private readonly Dictionary<BoundaryCacheKey, int> _providerCounts = new();
    private readonly Dictionary<BoundaryCacheKey, BoundaryCacheEntry> _boundaryCache = new();
    private readonly Queue<BoundaryCacheToken> _boundaryCacheOrder = new();
    private int _boundaryCacheCapacity = DefaultBoundaryCacheCapacity;
    private long _nextCacheSequence;

    public int CachedBoundaryCount => _boundaryCache.Count;
    public int BoundaryCacheCapacity => _boundaryCacheCapacity;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ZLevelBoundaryComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelBoundaryComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelBoundaryComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<ZLevelBoundaryComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ZLevelBoundaryComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<ZLevelBoundaryComponent, ZLevelPositionChangedEvent>(OnZLevelChanged);
        SubscribeLocalEvent<ZLevelBoundaryComponent, ZLevelBoundaryQueryEvent>(OnQuery);
        SubscribeLocalEvent<PlacementEntityEvent>(OnPlacement);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnGridTerminating);

        Subs.CVar(
            _configuration,
            CCVars.ZLevelBoundaryCacheCapacity,
            OnBoundaryCacheCapacityChanged,
            true);
    }

    public bool TryGetBoundary(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int firstZ,
        int secondZ,
        out ZLevelBoundaryState boundary)
    {
        boundary = default;
        if (Math.Abs(firstZ - secondZ) != 1)
            return false;

        var lowerZ = Math.Min(firstZ, secondZ);
        var cacheKey = new BoundaryCacheKey(gridUid, tile, lowerZ);
        if (_boundaryCache.TryGetValue(cacheKey, out var cached))
        {
            _metrics.RecordBoundaryQuery(true);
            boundary = cached.State;
            return true;
        }

        _metrics.RecordBoundaryQuery(false);

        var lower = new ZLevelTileIndices(tile.X, tile.Y, lowerZ);
        var upper = new ZLevelTileIndices(tile.X, tile.Y, lowerZ + 1);
        var upperTile = _map.GetZLevelTileRef(gridUid, grid, upper).Tile;
        var openChannels = GetDefaultOpenChannels(gridUid, upperTile);
        var defaultOpen = openChannels == ZLevelBoundaryChannels.All;
        var query = new ZLevelBoundaryQueryEvent((gridUid, grid), tile, lowerZ);

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            RaiseLocalEvent(entity.Value, ref query);
        }

        openChannels |= query.ForcedOpen;
        openChannels &= ~query.ForcedClosed;
        boundary = new ZLevelBoundaryState(
            lower,
            upper,
            defaultOpen,
            query.ForcedOpen,
            query.ForcedClosed,
            openChannels);
        CacheBoundary(cacheKey, boundary);
        return true;
    }

    public bool IsOpen(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int firstZ,
        int secondZ,
        ZLevelBoundaryChannels channels)
    {
        return TryGetBoundary(gridUid, grid, tile, firstZ, secondZ, out var boundary) &&
               boundary.IsOpen(channels);
    }

    /// <summary>
    /// Returns channels opened by map policy and the upper tile before explicit
    /// boundary providers are applied.
    /// </summary>
    public ZLevelBoundaryChannels GetDefaultOpenChannels(EntityUid gridUid, Tile upperTile)
    {
        if (_zLevelMap.TryGetConfig(gridUid, out var mapConfig) &&
            mapConfig.Comp.DefaultBoundaryMode == ZLevelDefaultBoundaryMode.ExplicitOnly)
        {
            return ZLevelBoundaryChannels.All;
        }

        if (upperTile.IsEmpty)
            return ZLevelBoundaryChannels.All;

        return ((ContentTileDefinition) _tileDefinitions[upperTile.TypeId]).ZLevelOpenChannels;
    }

    /// <summary>
    /// Returns whether an anchored content provider contributes to this exact
    /// boundary. This does not imply that any particular channel is open.
    /// </summary>
    public bool HasBoundaryProvider(EntityUid gridUid, Vector2i tile, int lowerZ)
    {
        return _providerCounts.ContainsKey(new BoundaryCacheKey(gridUid, tile, lowerZ));
    }

    public bool CanBodyPass(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ)
    {
        return IsOpen(gridUid, grid, tile, fromZ, toZ, ZLevelBoundaryChannels.Body);
    }

    public bool CanTraverse(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ)
    {
        var channel = toZ > fromZ
            ? ZLevelBoundaryChannels.TraversalUp
            : ZLevelBoundaryChannels.TraversalDown;
        return IsOpen(gridUid, grid, tile, fromZ, toZ, channel);
    }

    public bool IsStackOpen(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ,
        ZLevelBoundaryChannels channel)
    {
        if (fromZ == toZ)
            return true;

        var step = Math.Sign(toZ - fromZ);
        for (var z = fromZ; z != toZ; z += step)
        {
            if (!IsOpen(gridUid, grid, tile, z, z + step, channel))
                return false;
        }

        return true;
    }

    public void SetBoundary(
        Entity<ZLevelBoundaryComponent> entity,
        bool enabled,
        int boundaryOffset,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes)
    {
        if (boundaryOffset is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(boundaryOffset), "Boundary offset must be -1 or 1.");

        if (entity.Comp.Enabled == enabled &&
            entity.Comp.BoundaryOffset == boundaryOffset &&
            entity.Comp.Opens == opens &&
            entity.Comp.Closes == closes)
        {
            return;
        }

        entity.Comp.Enabled = enabled;
        entity.Comp.BoundaryOffset = boundaryOffset;
        entity.Comp.Opens = opens;
        entity.Comp.Closes = closes;
        Dirty(entity);
        RefreshRegistration(entity);
    }

    public void RefreshBoundary(EntityUid uid)
    {
        if (TryComp<ZLevelBoundaryComponent>(uid, out var boundary))
            RefreshRegistration((uid, boundary));
    }

    public void InvalidateBoundary(EntityUid gridUid, Vector2i tile, int lowerZ)
    {
        var removed = _boundaryCache.Remove(new BoundaryCacheKey(gridUid, tile, lowerZ));
        _metrics.RecordBoundaryInvalidation(removed);
    }

    private void OnStartup(Entity<ZLevelBoundaryComponent> entity, ref ComponentStartup args)
    {
        RefreshRegistration(entity);
    }

    private void OnShutdown(Entity<ZLevelBoundaryComponent> entity, ref ComponentShutdown args)
    {
        RemoveRegistration(entity.Owner);
    }

    private void OnMoved(Entity<ZLevelBoundaryComponent> entity, ref MoveEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnAnchorChanged(Entity<ZLevelBoundaryComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnAfterState(Entity<ZLevelBoundaryComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnZLevelChanged(Entity<ZLevelBoundaryComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnPlacement(PlacementEntityEvent args)
    {
        if (args.PlacementEventAction == PlacementEventAction.Create)
            RefreshBoundary(args.EditedEntity);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            InvalidateBoundary(args.Entity.Owner, change.GridIndices, -1);
        }
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            var tile = new Vector2i(change.GridIndices.X, change.GridIndices.Y);
            InvalidateBoundary(args.Entity.Owner, tile, change.GridIndices.Z - 1);
        }
    }

    private void OnGridTerminating(Entity<MapGridComponent> entity, ref EntityTerminatingEvent args)
    {
        var remove = new List<BoundaryCacheKey>();
        foreach (var key in _boundaryCache.Keys)
        {
            if (key.GridUid == entity.Owner)
                remove.Add(key);
        }

        foreach (var key in remove)
        {
            _boundaryCache.Remove(key);
        }

        if (remove.Count > 0)
            _metrics.RecordBoundaryInvalidatedEntries(remove.Count);

        remove.Clear();
        foreach (var key in _providerCounts.Keys)
        {
            if (key.GridUid == entity.Owner)
                remove.Add(key);
        }

        foreach (var key in remove)
        {
            _providerCounts.Remove(key);
        }
    }

    private void OnMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        var remove = new List<BoundaryCacheKey>();
        foreach (var key in _boundaryCache.Keys)
        {
            if (!_transformQuery.TryComp(key.GridUid, out var transform) || transform.MapUid != args.MapUid)
                continue;

            remove.Add(key);
        }

        foreach (var key in remove)
        {
            _boundaryCache.Remove(key);
        }

        if (remove.Count > 0)
            _metrics.RecordBoundaryInvalidatedEntries(remove.Count);
    }

    private void OnQuery(Entity<ZLevelBoundaryComponent> entity, ref ZLevelBoundaryQueryEvent args)
    {
        if (!entity.Comp.Enabled ||
            entity.Comp.BoundaryOffset is not (-1 or 1) ||
            !_transformQuery.TryComp(entity.Owner, out var transform))
        {
            return;
        }

        var entityZ = _transform.GetZLevel((entity.Owner, transform, CompOrNull<ZLevelPositionComponent>(entity.Owner)));
        var lowerZ = Math.Min(entityZ, entityZ + entity.Comp.BoundaryOffset);
        if (lowerZ != args.LowerZ)
            return;

        args.ForceOpen(entity.Comp.Opens);
        args.ForceClosed(entity.Comp.Closes);
    }

    private void RefreshRegistration(Entity<ZLevelBoundaryComponent> entity)
    {
        _registrations.TryGetValue(entity.Owner, out var oldRegistration);
        var hasOldRegistration = _registrations.ContainsKey(entity.Owner);
        var hasNewRegistration = TryGetRegistration(entity, out var newRegistration);

        if (hasOldRegistration && hasNewRegistration && oldRegistration == newRegistration)
            return;

        if (hasOldRegistration)
        {
            if (!hasNewRegistration || oldRegistration.Key != newRegistration.Key)
                RemoveProvider(oldRegistration.Key);

            RaiseChanged(oldRegistration);
        }

        if (!hasNewRegistration)
        {
            _registrations.Remove(entity.Owner);
            return;
        }

        if (!hasOldRegistration || oldRegistration.Key != newRegistration.Key)
            AddProvider(newRegistration.Key);

        _registrations[entity.Owner] = newRegistration;
        RaiseChanged(newRegistration);
    }

    private void RemoveRegistration(EntityUid uid)
    {
        if (!_registrations.Remove(uid, out var registration))
            return;

        RemoveProvider(registration.Key);
        RaiseChanged(registration);
    }

    private void AddProvider(BoundaryCacheKey key)
    {
        _providerCounts.TryGetValue(key, out var count);
        _providerCounts[key] = count + 1;
    }

    private void RemoveProvider(BoundaryCacheKey key)
    {
        if (!_providerCounts.TryGetValue(key, out var count))
            return;

        if (count <= 1)
            _providerCounts.Remove(key);
        else
            _providerCounts[key] = count - 1;
    }

    private bool TryGetRegistration(Entity<ZLevelBoundaryComponent> entity, out BoundaryRegistration registration)
    {
        registration = default;
        if (entity.Comp.BoundaryOffset is not (-1 or 1) ||
            !_transformQuery.TryComp(entity.Owner, out var transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var entityZ = _transform.GetZLevel((entity.Owner, transform, CompOrNull<ZLevelPositionComponent>(entity.Owner)));
        var lowerZ = Math.Min(entityZ, entityZ + entity.Comp.BoundaryOffset);
        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        registration = new BoundaryRegistration(
            gridUid,
            tile,
            lowerZ,
            entity.Comp.Enabled,
            entity.Comp.Opens,
            entity.Comp.Closes);
        return true;
    }

    private void RaiseChanged(BoundaryRegistration registration)
    {
        InvalidateBoundary(registration.GridUid, registration.Tile, registration.LowerZ);

        if (!_gridQuery.TryComp(registration.GridUid, out var grid))
            return;

        var ev = new ZLevelBoundaryChangedEvent((registration.GridUid, grid), registration.Tile, registration.LowerZ);
        RaiseLocalEvent(registration.GridUid, ref ev, true);
    }

    private void CacheBoundary(BoundaryCacheKey key, ZLevelBoundaryState state)
    {
        var sequence = ++_nextCacheSequence;
        _boundaryCache[key] = new BoundaryCacheEntry(state, sequence);
        _boundaryCacheOrder.Enqueue(new BoundaryCacheToken(key, sequence));

        if (_boundaryCacheOrder.Count > _boundaryCacheCapacity * 2)
        {
            CompactCacheOrder();
        }

        TrimBoundaryCache();
    }

    private void OnBoundaryCacheCapacityChanged(int configuredCapacity)
    {
        var capacity = Math.Clamp(
            configuredCapacity,
            MinimumBoundaryCacheCapacity,
            MaximumBoundaryCacheCapacity);
        if (_boundaryCacheCapacity == capacity)
            return;

        _boundaryCacheCapacity = capacity;
        TrimBoundaryCache();
        if (_boundaryCacheOrder.Count > _boundaryCacheCapacity * 2)
            CompactCacheOrder();
    }

    private void TrimBoundaryCache()
    {
        while (_boundaryCache.Count > _boundaryCacheCapacity && _boundaryCacheOrder.TryDequeue(out var oldest))
        {
            if (!_boundaryCache.TryGetValue(oldest.Key, out var current) || current.Sequence != oldest.Sequence)
                continue;

            _boundaryCache.Remove(oldest.Key);
            _metrics.RecordBoundaryEviction();
        }
    }

    private void CompactCacheOrder()
    {
        _boundaryCacheOrder.Clear();
        var ordered = new List<BoundaryCacheToken>(_boundaryCache.Count);
        foreach (var (cachedKey, entry) in _boundaryCache)
        {
            ordered.Add(new BoundaryCacheToken(cachedKey, entry.Sequence));
        }

        ordered.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        foreach (var token in ordered)
        {
            _boundaryCacheOrder.Enqueue(token);
        }
    }

    private readonly record struct BoundaryRegistration(
        EntityUid GridUid,
        Vector2i Tile,
        int LowerZ,
        bool Enabled,
        ZLevelBoundaryChannels Opens,
        ZLevelBoundaryChannels Closes)
    {
        public BoundaryCacheKey Key => new(GridUid, Tile, LowerZ);
    }

    private readonly record struct BoundaryCacheKey(EntityUid GridUid, Vector2i Tile, int LowerZ);
    private readonly record struct BoundaryCacheEntry(ZLevelBoundaryState State, long Sequence);
    private readonly record struct BoundaryCacheToken(BoundaryCacheKey Key, long Sequence);
}
