// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Lazily resolves vertical Sound-channel openings into compact grid-local chunks.
/// Propagation, attenuation, listener selection, and playback remain consumer policy.
/// </summary>
public sealed class SharedZLevelSoundPortalSystem : EntitySystem
{
    public const int DefaultCacheCapacity = 4_096;
    public const int MinimumCacheCapacity = 1;
    public const int MaximumCacheCapacity = 65_536;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<ZLevelSoundPortalChunkKey, ZLevelSoundPortalChunk> _chunks = new();
    private readonly Queue<SoundPortalCacheToken> _cacheOrder = new();
    private readonly List<SoundPortalCacheToken> _cacheOrderScratch = new();
    private readonly List<ZLevelSoundPortalChunkKey> _removeScratch = new();
    private long _nextRevision;
    private int _cachedOpenPortals;
    private int _cachedExplicitPortals;
    private int _cacheCapacity = DefaultCacheCapacity;

    private long _chunkQueries;
    private long _cacheHits;
    private long _cacheMisses;
    private long _builds;
    private long _buildTileChecks;
    private long _buildOpenPortals;
    private long _buildExplicitPortals;
    private long _invalidations;
    private long _invalidatedChunks;
    private long _buildTimestampTicks;
    private long _lastBuildTimestampTicks;
    private long _maxBuildTimestampTicks;
    private long _evictions;
    private long _portalQueries;
    private long _queryChunksVisited;
    private long _queryCandidatesVisited;
    private long _queryPortalsAdded;
    private long _chunkBudgetExhaustions;
    private long _buildBudgetExhaustions;
    private long _candidateBudgetExhaustions;

    public int CachedChunkCount => _chunks.Count;
    public int CachedOpenPortalCount => _cachedOpenPortals;
    public int CachedExplicitPortalCount => _cachedExplicitPortals;
    public int CacheCapacity => _cacheCapacity;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundPortalCacheCapacity,
            OnCacheCapacityChanged,
            true);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
        SubscribeLocalEvent<EntityTerminatingEvent>(OnEntityTerminating);
    }

    public override void Shutdown()
    {
        _chunks.Clear();
        _cacheOrder.Clear();
        _cacheOrderScratch.Clear();
        _removeScratch.Clear();
        _cachedOpenPortals = 0;
        _cachedExplicitPortals = 0;
        base.Shutdown();
    }

    public bool TryGetPortalChunk(
        Entity<MapGridComponent> grid,
        Vector2i chunkIndices,
        int lowerLocalZ,
        out ZLevelSoundPortalChunk chunk)
    {
        chunk = default;
        if (grid.Comp.Deleted)
            return false;

        _chunkQueries++;
        var key = new ZLevelSoundPortalChunkKey(grid.Owner, chunkIndices, lowerLocalZ);
        if (_chunks.TryGetValue(key, out chunk))
        {
            _cacheHits++;
            return true;
        }

        _cacheMisses++;
        var started = Stopwatch.GetTimestamp();
        chunk = BuildPortalChunk(grid, key);
        var elapsed = Stopwatch.GetTimestamp() - started;

        _chunks.Add(key, chunk);
        _cacheOrder.Enqueue(new SoundPortalCacheToken(key, chunk.Revision));
        _cachedOpenPortals += chunk.OpenCount;
        _cachedExplicitPortals += chunk.ExplicitOpenCount;
        _builds++;
        _buildTileChecks += ZLevelSoundPortalChunk.TileCount;
        _buildOpenPortals += chunk.OpenCount;
        _buildExplicitPortals += chunk.ExplicitOpenCount;
        _buildTimestampTicks += elapsed;
        _lastBuildTimestampTicks = elapsed;
        _maxBuildTimestampTicks = Math.Max(_maxBuildTimestampTicks, elapsed);

        TrimCache();
        if (_cacheOrder.Count > _cacheCapacity * 2)
            CompactCacheOrder();
        return true;
    }

    public bool IsPortalOpen(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int lowerLocalZ)
    {
        var chunkIndices = SharedMapSystem.GetChunkIndices(tile, ZLevelSoundPortalChunk.ChunkSize);
        return TryGetPortalChunk(grid, chunkIndices, lowerLocalZ, out var chunk) &&
               chunk.IsOpen(tile);
    }

    /// <summary>
    /// Appends portals by ascending lower Z, chunk Y/X, and tile Y/X. Bounds and layers are inclusive.
    /// Budget failure rolls back every portal appended by this call.
    /// </summary>
    public ZLevelSoundPortalQueryResult QueryPortals(
        Entity<MapGridComponent> grid,
        Vector2i minimumTile,
        Vector2i maximumTile,
        int minimumLowerLocalZ,
        int maximumLowerLocalZ,
        List<ZLevelSoundPortal> results)
    {
        var budget = ZLevelSoundPortalQueryBudget.Unlimited;
        return QueryPortals(
            grid,
            minimumTile,
            maximumTile,
            minimumLowerLocalZ,
            maximumLowerLocalZ,
            results,
            ref budget);
    }

    public ZLevelSoundPortalQueryResult QueryPortals(
        Entity<MapGridComponent> grid,
        Vector2i minimumTile,
        Vector2i maximumTile,
        int minimumLowerLocalZ,
        int maximumLowerLocalZ,
        List<ZLevelSoundPortal> results,
        ref ZLevelSoundPortalQueryBudget budget)
    {
        var initialCount = results.Count;
        var chunksVisited = 0;
        var candidatesVisited = 0;

        if (grid.Comp.Deleted ||
            minimumTile.X > maximumTile.X ||
            minimumTile.Y > maximumTile.Y ||
            minimumLowerLocalZ > maximumLowerLocalZ)
        {
            return FinishQuery(
                ZLevelSoundPortalQueryStatus.Invalid,
                initialCount,
                chunksVisited,
                candidatesVisited,
                results);
        }

        var minimumChunk = SharedMapSystem.GetChunkIndices(minimumTile, ZLevelSoundPortalChunk.ChunkSize);
        var maximumChunk = SharedMapSystem.GetChunkIndices(maximumTile, ZLevelSoundPortalChunk.ChunkSize);

        for (var lowerZ = minimumLowerLocalZ; ; lowerZ++)
        {
            for (var chunkY = minimumChunk.Y; ; chunkY++)
            {
                for (var chunkX = minimumChunk.X; ; chunkX++)
                {
                    if (budget.RemainingChunks <= 0)
                    {
                        return FinishQuery(
                            ZLevelSoundPortalQueryStatus.ChunkBudgetExceeded,
                            initialCount,
                            chunksVisited,
                            candidatesVisited,
                            results);
                    }

                    var chunkIndices = new Vector2i(chunkX, chunkY);
                    var key = new ZLevelSoundPortalChunkKey(grid.Owner, chunkIndices, lowerZ);
                    var cached = _chunks.ContainsKey(key);
                    if (!cached && budget.RemainingBuilds <= 0)
                    {
                        return FinishQuery(
                            ZLevelSoundPortalQueryStatus.BuildBudgetExceeded,
                            initialCount,
                            chunksVisited,
                            candidatesVisited,
                            results);
                    }

                    budget.RemainingChunks--;
                    if (!cached)
                        budget.RemainingBuilds--;

                    if (!TryGetPortalChunk(grid, chunkIndices, lowerZ, out var chunk))
                    {
                        return FinishQuery(
                            ZLevelSoundPortalQueryStatus.Invalid,
                            initialCount,
                            chunksVisited,
                            candidatesVisited,
                            results);
                    }

                    chunksVisited++;
                    var origin = chunk.Origin;
                    var startX = Math.Max(minimumTile.X, origin.X);
                    var endX = Math.Min(maximumTile.X, origin.X + ZLevelSoundPortalChunk.ChunkSize - 1);
                    var startY = Math.Max(minimumTile.Y, origin.Y);
                    var endY = Math.Min(maximumTile.Y, origin.Y + ZLevelSoundPortalChunk.ChunkSize - 1);

                    for (var tileY = startY; tileY <= endY; tileY++)
                    {
                        for (var tileX = startX; tileX <= endX; tileX++)
                        {
                            var tile = new Vector2i(tileX, tileY);
                            if (!chunk.IsOpen(tile))
                                continue;

                            if (budget.RemainingCandidates <= 0)
                            {
                                return FinishQuery(
                                    ZLevelSoundPortalQueryStatus.CandidateBudgetExceeded,
                                    initialCount,
                                    chunksVisited,
                                    candidatesVisited,
                                    results);
                            }

                            budget.RemainingCandidates--;
                            candidatesVisited++;
                            var localPosition = _map.TileCenterToVector(grid, tile);
                            results.Add(new ZLevelSoundPortal(
                                grid.Owner,
                                tile,
                                lowerZ,
                                lowerZ + 1,
                                localPosition,
                                _map.GridTileToWorldPos(grid.Owner, grid.Comp, tile),
                                _transform.LocalToWorldZLevel(grid.Owner, lowerZ),
                                _transform.LocalToWorldZLevel(grid.Owner, lowerZ + 1),
                                chunk.IsExplicitlyOpen(tile)
                                    ? ZLevelSoundPortalKind.ExplicitOpening
                                    : ZLevelSoundPortalKind.DefaultOpening));
                        }
                    }

                    if (chunkX == maximumChunk.X)
                        break;
                }

                if (chunkY == maximumChunk.Y)
                    break;
            }

            if (lowerZ == maximumLowerLocalZ)
                break;
        }

        return FinishQuery(
            ZLevelSoundPortalQueryStatus.Success,
            initialCount,
            chunksVisited,
            candidatesVisited,
            results);
    }

    public void InvalidateGrid(EntityUid gridUid)
    {
        RemoveWhere(static (key, uid) => key.GridUid == uid, gridUid);
    }

    public void InvalidateAll()
    {
        _invalidations++;
        _invalidatedChunks += _chunks.Count;
        _chunks.Clear();
        _cacheOrder.Clear();
        _cachedOpenPortals = 0;
        _cachedExplicitPortals = 0;
    }

    public void ResetMetrics()
    {
        _chunkQueries = 0;
        _cacheHits = 0;
        _cacheMisses = 0;
        _builds = 0;
        _buildTileChecks = 0;
        _buildOpenPortals = 0;
        _buildExplicitPortals = 0;
        _invalidations = 0;
        _invalidatedChunks = 0;
        _buildTimestampTicks = 0;
        _lastBuildTimestampTicks = 0;
        _maxBuildTimestampTicks = 0;
        _evictions = 0;
        _portalQueries = 0;
        _queryChunksVisited = 0;
        _queryCandidatesVisited = 0;
        _queryPortalsAdded = 0;
        _chunkBudgetExhaustions = 0;
        _buildBudgetExhaustions = 0;
        _candidateBudgetExhaustions = 0;
    }

    public ZLevelSoundPortalCacheMetrics Snapshot()
    {
        return new ZLevelSoundPortalCacheMetrics(
            _chunkQueries,
            _cacheHits,
            _cacheMisses,
            _builds,
            _buildTileChecks,
            _buildOpenPortals,
            _buildExplicitPortals,
            _invalidations,
            _invalidatedChunks,
            _buildTimestampTicks,
            _lastBuildTimestampTicks,
            _maxBuildTimestampTicks,
            _evictions,
            _portalQueries,
            _queryChunksVisited,
            _queryCandidatesVisited,
            _queryPortalsAdded,
            _chunkBudgetExhaustions,
            _buildBudgetExhaustions,
            _candidateBudgetExhaustions,
            _chunks.Count,
            _cachedOpenPortals,
            _cachedExplicitPortals,
            _cacheOrder.Count,
            _cacheCapacity);
    }

    private ZLevelSoundPortalChunk BuildPortalChunk(
        Entity<MapGridComponent> grid,
        ZLevelSoundPortalChunkKey key)
    {
        ulong openWord0 = 0;
        ulong openWord1 = 0;
        ulong openWord2 = 0;
        ulong openWord3 = 0;
        ulong explicitWord0 = 0;
        ulong explicitWord1 = 0;
        ulong explicitWord2 = 0;
        ulong explicitWord3 = 0;
        var openCount = 0;
        var explicitCount = 0;
        var origin = key.ChunkIndices * ZLevelSoundPortalChunk.ChunkSize;

        for (var y = 0; y < ZLevelSoundPortalChunk.ChunkSize; y++)
        {
            for (var x = 0; x < ZLevelSoundPortalChunk.ChunkSize; x++)
            {
                var tile = origin + new Vector2i(x, y);
                if (!_boundaries.TryGetBoundary(
                        grid.Owner,
                        grid.Comp,
                        tile,
                        key.LowerLocalZ,
                        key.LowerLocalZ + 1,
                        out var boundary) ||
                    !boundary.IsOpen(ZLevelBoundaryChannels.Sound))
                {
                    continue;
                }

                var bit = x + y * ZLevelSoundPortalChunk.ChunkSize;
                SetBit(
                    bit,
                    ref openWord0,
                    ref openWord1,
                    ref openWord2,
                    ref openWord3);
                openCount++;

                if ((boundary.ForcedOpen & ZLevelBoundaryChannels.Sound) == 0)
                    continue;

                SetBit(
                    bit,
                    ref explicitWord0,
                    ref explicitWord1,
                    ref explicitWord2,
                    ref explicitWord3);
                explicitCount++;
            }
        }

        return new ZLevelSoundPortalChunk(
            key,
            ++_nextRevision,
            openWord0,
            openWord1,
            openWord2,
            openWord3,
            explicitWord0,
            explicitWord1,
            explicitWord2,
            explicitWord3,
            openCount,
            explicitCount);
    }

    private static void SetBit(
        int bit,
        ref ulong word0,
        ref ulong word1,
        ref ulong word2,
        ref ulong word3)
    {
        var mask = 1UL << (bit & 63);
        switch (bit >> 6)
        {
            case 0:
                word0 |= mask;
                break;
            case 1:
                word1 |= mask;
                break;
            case 2:
                word2 |= mask;
                break;
            default:
                word3 |= mask;
                break;
        }
    }

    private ZLevelSoundPortalQueryResult FinishQuery(
        ZLevelSoundPortalQueryStatus status,
        int initialCount,
        int chunksVisited,
        int candidatesVisited,
        List<ZLevelSoundPortal> results)
    {
        var added = results.Count - initialCount;
        if (status != ZLevelSoundPortalQueryStatus.Success && added > 0)
        {
            results.RemoveRange(initialCount, added);
            added = 0;
        }

        _portalQueries++;
        _queryChunksVisited += chunksVisited;
        _queryCandidatesVisited += candidatesVisited;
        _queryPortalsAdded += added;
        switch (status)
        {
            case ZLevelSoundPortalQueryStatus.ChunkBudgetExceeded:
                _chunkBudgetExhaustions++;
                break;
            case ZLevelSoundPortalQueryStatus.BuildBudgetExceeded:
                _buildBudgetExhaustions++;
                break;
            case ZLevelSoundPortalQueryStatus.CandidateBudgetExceeded:
                _candidateBudgetExhaustions++;
                break;
        }

        return new ZLevelSoundPortalQueryResult(status, added, chunksVisited, candidatesVisited);
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            InvalidateTile(args.Entity.Owner, change.GridIndices, -1);
        }
    }

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent args)
    {
        foreach (var change in args.Changes)
        {
            InvalidateTile(
                args.Entity.Owner,
                new Vector2i(change.GridIndices.X, change.GridIndices.Y),
                change.GridIndices.Z - 1);
        }
    }

    private void OnBoundaryChanged(ref ZLevelBoundaryChangedEvent args)
    {
        InvalidateTile(args.Grid.Owner, args.Tile, args.LowerZ);
    }

    private void OnMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        RemoveWhere(
            static (key, state) =>
                state.Query.TryComp(key.GridUid, out var transform) && transform.MapUid == state.MapUid,
            (Query: _transformQuery, args.MapUid));
    }

    private void OnEntityTerminating(ref EntityTerminatingEvent args)
    {
        if (_gridQuery.HasComp(args.Entity.Owner))
            InvalidateGrid(args.Entity.Owner);
    }

    private void InvalidateTile(EntityUid gridUid, Vector2i tile, int lowerLocalZ)
    {
        _invalidations++;
        var chunkIndices = SharedMapSystem.GetChunkIndices(tile, ZLevelSoundPortalChunk.ChunkSize);
        var key = new ZLevelSoundPortalChunkKey(gridUid, chunkIndices, lowerLocalZ);
        if (!_chunks.Remove(key, out var removed))
            return;

        _cachedOpenPortals -= removed.OpenCount;
        _cachedExplicitPortals -= removed.ExplicitOpenCount;
        _invalidatedChunks++;
        if (_chunks.Count == 0)
            _cacheOrder.Clear();
    }

    private void RemoveWhere<TState>(
        Func<ZLevelSoundPortalChunkKey, TState, bool> predicate,
        TState state)
    {
        _invalidations++;
        _removeScratch.Clear();
        foreach (var key in _chunks.Keys)
        {
            if (predicate(key, state))
                _removeScratch.Add(key);
        }

        foreach (var key in _removeScratch)
        {
            if (!_chunks.Remove(key, out var removed))
                continue;

            _cachedOpenPortals -= removed.OpenCount;
            _cachedExplicitPortals -= removed.ExplicitOpenCount;
        }

        _invalidatedChunks += _removeScratch.Count;
        _removeScratch.Clear();
        if (_chunks.Count == 0)
            _cacheOrder.Clear();
    }

    private void OnCacheCapacityChanged(int configuredCapacity)
    {
        _cacheCapacity = Math.Clamp(
            configuredCapacity,
            MinimumCacheCapacity,
            MaximumCacheCapacity);
        TrimCache();
        if (_cacheOrder.Count > _cacheCapacity * 2)
            CompactCacheOrder();
    }

    private void TrimCache()
    {
        while (_chunks.Count > _cacheCapacity && _cacheOrder.TryDequeue(out var oldest))
        {
            if (!_chunks.TryGetValue(oldest.Key, out var current) ||
                current.Revision != oldest.Revision)
            {
                continue;
            }

            _chunks.Remove(oldest.Key);
            _cachedOpenPortals -= current.OpenCount;
            _cachedExplicitPortals -= current.ExplicitOpenCount;
            _evictions++;
        }
    }

    private void CompactCacheOrder()
    {
        _cacheOrderScratch.Clear();
        foreach (var (key, chunk) in _chunks)
        {
            _cacheOrderScratch.Add(new SoundPortalCacheToken(key, chunk.Revision));
        }

        _cacheOrderScratch.Sort(static (left, right) => left.Revision.CompareTo(right.Revision));
        _cacheOrder.Clear();
        foreach (var token in _cacheOrderScratch)
        {
            _cacheOrder.Enqueue(token);
        }

        _cacheOrderScratch.Clear();
    }

    private readonly record struct SoundPortalCacheToken(
        ZLevelSoundPortalChunkKey Key,
        long Revision);
}
