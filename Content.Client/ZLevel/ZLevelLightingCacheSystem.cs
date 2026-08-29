// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Maths;
using Robust.Shared.Physics;

namespace Content.Client.ZLevel;

/// <summary>
/// Caches vertical visibility apertures by grid chunk and exposes the native
/// point-light tree as the allocation-free emitter index for vertical lighting.
/// </summary>
public sealed class ZLevelLightingCacheSystem : EntitySystem
{
    public const int DefaultApertureCacheCapacity = 4_096;
    public const int MinimumApertureCacheCapacity = 1;
    public const int MaximumApertureCacheCapacity = 65_536;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly LightTreeSystem _lightTree = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<ZLevelApertureChunkKey, ZLevelApertureChunk> _apertures = new();
    private readonly Queue<ZLevelApertureCacheToken> _apertureOrder = new();
    private readonly List<ZLevelApertureCacheToken> _apertureOrderScratch = new();
    private readonly List<ZLevelApertureChunkKey> _removeScratch = new();
    private readonly List<(EntityUid Uid, LightTreeComponent Comp)> _lightTreeScratch = new();
    private static readonly DynamicTree<ComponentTreeEntry<PointLightComponent>>.QueryCallbackDelegate<EmitterQueryState>
        EmitterQueryCallback = QueryEmitter;
    private long _nextRevision;
    private int _cachedOpenTiles;
    private int _apertureCacheCapacity = DefaultApertureCacheCapacity;

    private long _apertureQueries;
    private long _apertureCacheHits;
    private long _apertureCacheMisses;
    private long _apertureBuilds;
    private long _apertureBuildTileChecks;
    private long _apertureBuildOpenTiles;
    private long _apertureInvalidations;
    private long _apertureInvalidatedChunks;
    private long _apertureBuildTimestampTicks;
    private long _apertureLastBuildTimestampTicks;
    private long _apertureMaxBuildTimestampTicks;
    private long _apertureEvictions;

    private long _emitterQueries;
    private long _emitterCandidates;
    private long _emitterAccepted;
    private long _emitterWorldZRejected;
    private long _emitterBoundsRejected;
    private long _emitterQueryTimestampTicks;
    private long _emitterLastQueryTimestampTicks;
    private long _emitterMaxQueryTimestampTicks;
    private long _emitterCandidateBudgetExhaustions;

    public int CachedApertureChunkCount => _apertures.Count;
    public int CachedOpenApertureTileCount => _cachedOpenTiles;
    public int ApertureCacheCapacity => _apertureCacheCapacity;

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingApertureCacheCapacity,
            OnApertureCacheCapacityChanged,
            true);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
        SubscribeLocalEvent<MapGridComponent, ComponentRemove>(OnGridRemoved);
    }

    public override void Shutdown()
    {
        _apertures.Clear();
        _apertureOrder.Clear();
        _apertureOrderScratch.Clear();
        _removeScratch.Clear();
        _lightTreeScratch.Clear();
        _cachedOpenTiles = 0;
        base.Shutdown();
    }

    /// <summary>
    /// Gets one local-grid chunk of visibility boundaries between
    /// <paramref name="lowerLocalZ"/> and the floor immediately above it.
    /// </summary>
    public bool TryGetApertureChunk(
        Entity<MapGridComponent> grid,
        Vector2i chunkIndices,
        int lowerLocalZ,
        out ZLevelApertureChunk aperture)
    {
        aperture = default;
        if (grid.Comp.Deleted)
            return false;

        _apertureQueries++;
        var key = new ZLevelApertureChunkKey(grid.Owner, chunkIndices, lowerLocalZ);
        if (_apertures.TryGetValue(key, out aperture))
        {
            _apertureCacheHits++;
            return true;
        }

        _apertureCacheMisses++;
        var started = Stopwatch.GetTimestamp();
        aperture = BuildApertureChunk(grid, key);
        var elapsed = Stopwatch.GetTimestamp() - started;

        _apertures.Add(key, aperture);
        _apertureOrder.Enqueue(new ZLevelApertureCacheToken(key, aperture.Revision));
        _cachedOpenTiles += aperture.OpenCount;
        _apertureBuilds++;
        _apertureBuildTileChecks += ZLevelApertureChunk.TileCount;
        _apertureBuildOpenTiles += aperture.OpenCount;
        _apertureBuildTimestampTicks += elapsed;
        _apertureLastBuildTimestampTicks = elapsed;
        _apertureMaxBuildTimestampTicks = Math.Max(_apertureMaxBuildTimestampTicks, elapsed);
        TrimApertureCache();
        if (_apertureOrder.Count > _apertureCacheCapacity * 2)
            CompactApertureOrder();
        return true;
    }

    public bool IsApertureOpen(
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int lowerLocalZ)
    {
        var chunkIndices = SharedMapSystem.GetChunkIndices(tile, ZLevelApertureChunk.ChunkSize);
        return TryGetApertureChunk(grid, chunkIndices, lowerLocalZ, out var aperture) &&
               aperture.IsOpen(tile);
    }

    /// <summary>
    /// Intersects every adjacent visibility aperture between two local floors.
    /// A set bit in the result identifies a column that stays open for the
    /// complete lower-to-upper stack.
    /// </summary>
    public bool TryComposeApertureStack(
        Entity<MapGridComponent> grid,
        Vector2i chunkIndices,
        int targetLocalZ,
        int viewerLocalZ,
        out ZLevelApertureStack stack)
    {
        var budget = ZLevelApertureQueryBudget.Unlimited;
        return TryComposeApertureStack(
                   grid,
                   chunkIndices,
                   targetLocalZ,
                   viewerLocalZ,
                   ref budget,
                   out stack) == ZLevelApertureStackQueryResult.Success;
    }

    /// <summary>
    /// Intersects an aperture stack while charging caller-owned layer and cold-build budgets.
    /// A budget failure never exposes a partial stack.
    /// </summary>
    public ZLevelApertureStackQueryResult TryComposeApertureStack(
        Entity<MapGridComponent> grid,
        Vector2i chunkIndices,
        int targetLocalZ,
        int viewerLocalZ,
        ref ZLevelApertureQueryBudget budget,
        out ZLevelApertureStack stack)
    {
        stack = default;
        if (targetLocalZ >= viewerLocalZ || grid.Comp.Deleted)
            return ZLevelApertureStackQueryResult.Invalid;

        var word0 = ulong.MaxValue;
        var word1 = ulong.MaxValue;
        var word2 = ulong.MaxValue;
        var word3 = ulong.MaxValue;

        for (var lowerZ = targetLocalZ; lowerZ < viewerLocalZ; lowerZ++)
        {
            if (budget.RemainingLayers <= 0)
                return ZLevelApertureStackQueryResult.LayerBudgetExceeded;

            var key = new ZLevelApertureChunkKey(grid.Owner, chunkIndices, lowerZ);
            var cached = _apertures.ContainsKey(key);
            if (!cached && budget.RemainingBuilds <= 0)
                return ZLevelApertureStackQueryResult.BuildBudgetExceeded;

            budget.RemainingLayers--;
            if (!cached)
                budget.RemainingBuilds--;

            if (!TryGetApertureChunk(grid, chunkIndices, lowerZ, out var aperture))
                return ZLevelApertureStackQueryResult.Invalid;

            word0 &= aperture.Word0;
            word1 &= aperture.Word1;
            word2 &= aperture.Word2;
            word3 &= aperture.Word3;

            if ((word0 | word1 | word2 | word3) == 0)
                break;
        }

        stack = new ZLevelApertureStack(
            grid.Owner,
            chunkIndices,
            targetLocalZ,
            viewerLocalZ,
            word0,
            word1,
            word2,
            word3,
            BitOperations.PopCount(word0) +
            BitOperations.PopCount(word1) +
            BitOperations.PopCount(word2) +
            BitOperations.PopCount(word3));
        return ZLevelApertureStackQueryResult.Success;
    }

    /// <summary>
    /// Appends live emitters whose light circles intersect <paramref name="worldBounds"/>
    /// and whose effective world Z is inside the inclusive requested range.
    /// </summary>
    public int QueryEmitters(
        MapId mapId,
        Box2 worldBounds,
        int minimumWorldZ,
        int maximumWorldZ,
        List<ZLevelLightEmitter> results)
    {
        return QueryEmitters(
            mapId,
            worldBounds,
            minimumWorldZ,
            maximumWorldZ,
            results,
            int.MaxValue).Accepted;
    }

    /// <summary>
    /// Queries native point lights while bounding broad-phase entry visits.
    /// </summary>
    public ZLevelLightEmitterQueryResult QueryEmitters(
        MapId mapId,
        Box2 worldBounds,
        int minimumWorldZ,
        int maximumWorldZ,
        List<ZLevelLightEmitter> results,
        int maximumCandidates)
    {
        if (minimumWorldZ > maximumWorldZ)
            throw new ArgumentOutOfRangeException(nameof(minimumWorldZ));
        if (maximumCandidates < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumCandidates));

        var started = Stopwatch.GetTimestamp();
        var initialCount = results.Count;
        var state = new EmitterQueryState(
            this,
            worldBounds,
            minimumWorldZ,
            maximumWorldZ,
            results,
            maximumCandidates);

        if (maximumCandidates == 0)
        {
            state.CandidateBudgetExceeded = true;
        }
        else
        {
            _lightTreeScratch.Clear();
            _lightTree.QueryAabb(
                ref state,
                EmitterQueryCallback,
                mapId,
                worldBounds,
                _lightTreeScratch);
            _lightTreeScratch.Clear();
        }

        var elapsed = Stopwatch.GetTimestamp() - started;
        _emitterQueries++;
        _emitterCandidates += state.Candidates;
        _emitterAccepted += state.Accepted;
        _emitterWorldZRejected += state.WorldZRejected;
        _emitterBoundsRejected += state.BoundsRejected;
        _emitterQueryTimestampTicks += elapsed;
        _emitterLastQueryTimestampTicks = elapsed;
        _emitterMaxQueryTimestampTicks = Math.Max(_emitterMaxQueryTimestampTicks, elapsed);
        if (state.CandidateBudgetExceeded)
            _emitterCandidateBudgetExhaustions++;

        return new ZLevelLightEmitterQueryResult(
            results.Count - initialCount,
            state.Candidates,
            state.CandidateBudgetExceeded);
    }

    public void InvalidateGrid(EntityUid gridUid)
    {
        RemoveWhere(static (key, grid) => key.GridUid == grid, gridUid);
    }

    public void InvalidateAll()
    {
        _apertureInvalidations++;
        _apertureInvalidatedChunks += _apertures.Count;
        _apertures.Clear();
        _apertureOrder.Clear();
        _cachedOpenTiles = 0;
    }

    public void ResetMetrics()
    {
        _apertureQueries = 0;
        _apertureCacheHits = 0;
        _apertureCacheMisses = 0;
        _apertureBuilds = 0;
        _apertureBuildTileChecks = 0;
        _apertureBuildOpenTiles = 0;
        _apertureInvalidations = 0;
        _apertureInvalidatedChunks = 0;
        _apertureBuildTimestampTicks = 0;
        _apertureLastBuildTimestampTicks = 0;
        _apertureMaxBuildTimestampTicks = 0;
        _apertureEvictions = 0;
        _emitterQueries = 0;
        _emitterCandidates = 0;
        _emitterAccepted = 0;
        _emitterWorldZRejected = 0;
        _emitterBoundsRejected = 0;
        _emitterQueryTimestampTicks = 0;
        _emitterLastQueryTimestampTicks = 0;
        _emitterMaxQueryTimestampTicks = 0;
        _emitterCandidateBudgetExhaustions = 0;
    }

    public ZLevelLightingCacheMetrics Snapshot()
    {
        return new ZLevelLightingCacheMetrics(
            _apertureQueries,
            _apertureCacheHits,
            _apertureCacheMisses,
            _apertureBuilds,
            _apertureBuildTileChecks,
            _apertureBuildOpenTiles,
            _apertureInvalidations,
            _apertureInvalidatedChunks,
            _apertureBuildTimestampTicks,
            _apertureLastBuildTimestampTicks,
            _apertureMaxBuildTimestampTicks,
            _apertureEvictions,
            _emitterQueries,
            _emitterCandidates,
            _emitterAccepted,
            _emitterWorldZRejected,
            _emitterBoundsRejected,
            _emitterQueryTimestampTicks,
            _emitterLastQueryTimestampTicks,
            _emitterMaxQueryTimestampTicks,
            _emitterCandidateBudgetExhaustions,
            _apertures.Count,
            _cachedOpenTiles,
            _apertureCacheCapacity);
    }

    private ZLevelApertureChunk BuildApertureChunk(
        Entity<MapGridComponent> grid,
        ZLevelApertureChunkKey key)
    {
        ulong word0 = 0;
        ulong word1 = 0;
        ulong word2 = 0;
        ulong word3 = 0;
        var origin = key.ChunkIndices * ZLevelApertureChunk.ChunkSize;
        var openCount = 0;

        for (var y = 0; y < ZLevelApertureChunk.ChunkSize; y++)
        {
            for (var x = 0; x < ZLevelApertureChunk.ChunkSize; x++)
            {
                var tile = origin + new Vector2i(x, y);
                if (!_boundaries.IsOpen(
                        grid.Owner,
                        grid.Comp,
                        tile,
                        key.LowerLocalZ,
                        key.LowerLocalZ + 1,
                        ZLevelBoundaryChannels.Visibility))
                {
                    continue;
                }

                var bit = x + y * ZLevelApertureChunk.ChunkSize;
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
                openCount++;
            }
        }

        return new ZLevelApertureChunk(
            key,
            ++_nextRevision,
            word0,
            word1,
            word2,
            word3,
            openCount);
    }

    private static bool QueryEmitter(
        ref EmitterQueryState state,
        in ComponentTreeEntry<PointLightComponent> entry)
    {
        if (state.Candidates >= state.MaximumCandidates)
        {
            state.CandidateBudgetExceeded = true;
            return false;
        }

        state.Candidates++;
        var system = state.System;
        var worldZ = system._transform.GetWorldZLevel((entry.Uid, entry.Transform, null));
        if (worldZ < state.MinimumWorldZ || worldZ > state.MaximumWorldZ)
        {
            state.WorldZRejected++;
            return true;
        }

        var (worldPosition, worldRotation) = system._transform.GetWorldPositionRotation(
            entry.Transform,
            system._transformQuery);
        worldPosition += worldRotation.RotateVec(entry.Component.Offset);
        if (!new Circle(worldPosition, entry.Component.Radius).Intersects(state.WorldBounds))
        {
            state.BoundsRejected++;
            return true;
        }

        state.Results.Add(new ZLevelLightEmitter(
            entry.Uid,
            entry.Transform.GridUid ?? EntityUid.Invalid,
            worldPosition,
            worldZ,
            entry.Component.Radius,
            entry.Component.Energy,
            entry.Component.Color,
            entry.Component.Softness,
            entry.Component.Falloff,
            entry.Component.CurveFactor,
            entry.Component.CastShadows,
            entry.Component.Rotation + (entry.Component.MaskAutoRotate ? worldRotation : Angle.Zero),
            entry.Component.MaskPath));
        state.Accepted++;
        return true;
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

    private void OnGridRemoved(Entity<MapGridComponent> entity, ref ComponentRemove args)
    {
        InvalidateGrid(entity.Owner);
    }

    private void InvalidateTile(EntityUid gridUid, Vector2i tile, int lowerLocalZ)
    {
        _apertureInvalidations++;
        var chunkIndices = SharedMapSystem.GetChunkIndices(tile, ZLevelApertureChunk.ChunkSize);
        var key = new ZLevelApertureChunkKey(gridUid, chunkIndices, lowerLocalZ);
        if (!_apertures.Remove(key, out var removed))
            return;

        _cachedOpenTiles -= removed.OpenCount;
        _apertureInvalidatedChunks++;

        if (_apertures.Count == 0)
            _apertureOrder.Clear();
    }

    private void RemoveWhere<TState>(Func<ZLevelApertureChunkKey, TState, bool> predicate, TState state)
    {
        _apertureInvalidations++;
        _removeScratch.Clear();
        foreach (var key in _apertures.Keys)
        {
            if (predicate(key, state))
                _removeScratch.Add(key);
        }

        foreach (var key in _removeScratch)
        {
            if (!_apertures.Remove(key, out var removed))
                continue;

            _cachedOpenTiles -= removed.OpenCount;
        }

        _apertureInvalidatedChunks += _removeScratch.Count;
        _removeScratch.Clear();

        if (_apertures.Count == 0)
            _apertureOrder.Clear();
    }

    private void OnApertureCacheCapacityChanged(int configuredCapacity)
    {
        _apertureCacheCapacity = Math.Clamp(
            configuredCapacity,
            MinimumApertureCacheCapacity,
            MaximumApertureCacheCapacity);
        TrimApertureCache();
        if (_apertureOrder.Count > _apertureCacheCapacity * 2)
            CompactApertureOrder();
    }

    private void TrimApertureCache()
    {
        while (_apertures.Count > _apertureCacheCapacity &&
               _apertureOrder.TryDequeue(out var oldest))
        {
            if (!_apertures.TryGetValue(oldest.Key, out var current) ||
                current.Revision != oldest.Revision)
            {
                continue;
            }

            _apertures.Remove(oldest.Key);
            _cachedOpenTiles -= current.OpenCount;
            _apertureEvictions++;
        }
    }

    private void CompactApertureOrder()
    {
        _apertureOrderScratch.Clear();
        foreach (var (key, aperture) in _apertures)
        {
            _apertureOrderScratch.Add(new ZLevelApertureCacheToken(key, aperture.Revision));
        }

        _apertureOrderScratch.Sort(static (left, right) => left.Revision.CompareTo(right.Revision));
        _apertureOrder.Clear();
        foreach (var token in _apertureOrderScratch)
        {
            _apertureOrder.Enqueue(token);
        }

        _apertureOrderScratch.Clear();
    }

    private struct EmitterQueryState(
        ZLevelLightingCacheSystem system,
        Box2 worldBounds,
        int minimumWorldZ,
        int maximumWorldZ,
        List<ZLevelLightEmitter> results,
        int maximumCandidates)
    {
        public readonly ZLevelLightingCacheSystem System = system;
        public readonly Box2 WorldBounds = worldBounds;
        public readonly int MinimumWorldZ = minimumWorldZ;
        public readonly int MaximumWorldZ = maximumWorldZ;
        public readonly List<ZLevelLightEmitter> Results = results;
        public readonly int MaximumCandidates = maximumCandidates;
        public int Candidates;
        public int Accepted;
        public int WorldZRejected;
        public int BoundsRejected;
        public bool CandidateBudgetExceeded;
    }
}

public enum ZLevelApertureStackQueryResult : byte
{
    Success,
    Invalid,
    LayerBudgetExceeded,
    BuildBudgetExceeded,
}

public struct ZLevelApertureQueryBudget
{
    public int RemainingLayers;
    public int RemainingBuilds;

    public static ZLevelApertureQueryBudget Unlimited => new(int.MaxValue, int.MaxValue);

    public ZLevelApertureQueryBudget(int remainingLayers, int remainingBuilds)
    {
        RemainingLayers = remainingLayers;
        RemainingBuilds = remainingBuilds;
    }
}

public readonly record struct ZLevelLightEmitterQueryResult(
    int Accepted,
    int CandidatesVisited,
    bool CandidateBudgetExceeded);

internal readonly record struct ZLevelApertureCacheToken(
    ZLevelApertureChunkKey Key,
    long Revision);

public readonly record struct ZLevelApertureChunkKey(
    EntityUid GridUid,
    Vector2i ChunkIndices,
    int LowerLocalZ);

public readonly record struct ZLevelApertureChunk(
    ZLevelApertureChunkKey Key,
    long Revision,
    ulong Word0,
    ulong Word1,
    ulong Word2,
    ulong Word3,
    int OpenCount)
{
    public const int ChunkSize = TileSystem.ChunkSize;
    public const int TileCount = ChunkSize * ChunkSize;
    public const int WordCount = TileCount / 64;

    public Vector2i Origin => Key.ChunkIndices * ChunkSize;

    public bool IsOpen(Vector2i gridTile)
    {
        if (SharedMapSystem.GetChunkIndices(gridTile, ChunkSize) != Key.ChunkIndices)
            return false;

        var relative = SharedMapSystem.GetChunkRelative(gridTile, ChunkSize);
        return IsOpenRelative(relative.X, relative.Y);
    }

    public bool IsOpenRelative(int x, int y)
    {
        if ((uint)x >= ChunkSize || (uint)y >= ChunkSize)
            return false;

        var bit = x + y * ChunkSize;
        return (GetWord(bit >> 6) & (1UL << (bit & 63))) != 0;
    }

    public ulong GetWord(int index)
    {
        return index switch
        {
            0 => Word0,
            1 => Word1,
            2 => Word2,
            3 => Word3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }
}

public readonly record struct ZLevelApertureStack(
    EntityUid GridUid,
    Vector2i ChunkIndices,
    int TargetLocalZ,
    int ViewerLocalZ,
    ulong Word0,
    ulong Word1,
    ulong Word2,
    ulong Word3,
    int OpenCount)
{
    public bool IsOpen(Vector2i gridTile)
    {
        if (SharedMapSystem.GetChunkIndices(gridTile, ZLevelApertureChunk.ChunkSize) != ChunkIndices)
            return false;

        var relative = SharedMapSystem.GetChunkRelative(gridTile, ZLevelApertureChunk.ChunkSize);
        return IsOpenRelative(relative.X, relative.Y);
    }

    public bool IsOpenRelative(int x, int y)
    {
        if ((uint)x >= ZLevelApertureChunk.ChunkSize ||
            (uint)y >= ZLevelApertureChunk.ChunkSize)
        {
            return false;
        }

        return (GetRowBits(y) & (1U << x)) != 0;
    }

    public uint GetRowBits(int y)
    {
        if ((uint)y >= ZLevelApertureChunk.ChunkSize)
            return 0;

        var bit = y * ZLevelApertureChunk.ChunkSize;
        return (uint) ((GetWord(bit >> 6) >> (bit & 63)) & 0xFFFFUL);
    }

    public ulong GetWord(int index)
    {
        return index switch
        {
            0 => Word0,
            1 => Word1,
            2 => Word2,
            3 => Word3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }
}

public readonly record struct ZLevelLightEmitter(
    EntityUid Uid,
    EntityUid GridUid,
    Vector2 WorldPosition,
    int WorldZ,
    float Radius,
    float Energy,
    Color Color,
    float Softness,
    float Falloff,
    float CurveFactor,
    bool CastShadows,
    Angle MaskRotation,
    string? MaskPath);

public readonly record struct ZLevelLightingCacheMetrics(
    long ApertureQueries,
    long ApertureCacheHits,
    long ApertureCacheMisses,
    long ApertureBuilds,
    long ApertureBuildTileChecks,
    long ApertureBuildOpenTiles,
    long ApertureInvalidations,
    long ApertureInvalidatedChunks,
    long ApertureBuildTimestampTicks,
    long ApertureLastBuildTimestampTicks,
    long ApertureMaxBuildTimestampTicks,
    long ApertureEvictions,
    long EmitterQueries,
    long EmitterCandidates,
    long EmitterAccepted,
    long EmitterWorldZRejected,
    long EmitterBoundsRejected,
    long EmitterQueryTimestampTicks,
    long EmitterLastQueryTimestampTicks,
    long EmitterMaxQueryTimestampTicks,
    long EmitterCandidateBudgetExhaustions,
    int CachedApertureChunks,
    int CachedOpenApertureTiles,
    int ApertureCacheCapacity)
{
    public double ApertureCacheHitPercent => ApertureQueries == 0
        ? 0d
        : ApertureCacheHits * 100d / ApertureQueries;

    public double ApertureBuildMilliseconds => ToMilliseconds(ApertureBuildTimestampTicks);
    public double ApertureAverageBuildMilliseconds => ApertureBuilds == 0
        ? 0d
        : ApertureBuildMilliseconds / ApertureBuilds;
    public double ApertureLastBuildMilliseconds => ToMilliseconds(ApertureLastBuildTimestampTicks);
    public double ApertureMaxBuildMilliseconds => ToMilliseconds(ApertureMaxBuildTimestampTicks);
    public double EmitterQueryMilliseconds => ToMilliseconds(EmitterQueryTimestampTicks);
    public double EmitterAverageQueryMilliseconds => EmitterQueries == 0
        ? 0d
        : EmitterQueryMilliseconds / EmitterQueries;
    public double EmitterLastQueryMilliseconds => ToMilliseconds(EmitterLastQueryTimestampTicks);
    public double EmitterMaxQueryMilliseconds => ToMilliseconds(EmitterMaxQueryTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
