// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
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
    [Dependency] private readonly LightTreeSystem _lightTree = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<ZLevelApertureChunkKey, ZLevelApertureChunk> _apertures = new();
    private readonly List<ZLevelApertureChunkKey> _removeScratch = new();
    private readonly List<(EntityUid Uid, LightTreeComponent Comp)> _lightTreeScratch = new();
    private static readonly DynamicTree<ComponentTreeEntry<PointLightComponent>>.QueryCallbackDelegate<EmitterQueryState>
        EmitterQueryCallback = QueryEmitter;
    private long _nextRevision;
    private int _cachedOpenTiles;

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

    private long _emitterQueries;
    private long _emitterCandidates;
    private long _emitterAccepted;
    private long _emitterWorldZRejected;
    private long _emitterBoundsRejected;
    private long _emitterQueryTimestampTicks;
    private long _emitterLastQueryTimestampTicks;
    private long _emitterMaxQueryTimestampTicks;

    public int CachedApertureChunkCount => _apertures.Count;
    public int CachedOpenApertureTileCount => _cachedOpenTiles;

    public override void Initialize()
    {
        base.Initialize();

        _transformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnZLevelTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
        SubscribeLocalEvent<MapGridComponent, ComponentRemove>(OnGridRemoved);
    }

    public override void Shutdown()
    {
        _apertures.Clear();
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
        _cachedOpenTiles += aperture.OpenCount;
        _apertureBuilds++;
        _apertureBuildTileChecks += ZLevelApertureChunk.TileCount;
        _apertureBuildOpenTiles += aperture.OpenCount;
        _apertureBuildTimestampTicks += elapsed;
        _apertureLastBuildTimestampTicks = elapsed;
        _apertureMaxBuildTimestampTicks = Math.Max(_apertureMaxBuildTimestampTicks, elapsed);
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
        if (minimumWorldZ > maximumWorldZ)
            throw new ArgumentOutOfRangeException(nameof(minimumWorldZ));

        var started = Stopwatch.GetTimestamp();
        var initialCount = results.Count;
        var state = new EmitterQueryState(
            this,
            worldBounds,
            minimumWorldZ,
            maximumWorldZ,
            results);

        _lightTreeScratch.Clear();
        _lightTree.QueryAabb(
            ref state,
            EmitterQueryCallback,
            mapId,
            worldBounds,
            _lightTreeScratch);
        _lightTreeScratch.Clear();

        var elapsed = Stopwatch.GetTimestamp() - started;
        _emitterQueries++;
        _emitterCandidates += state.Candidates;
        _emitterAccepted += state.Accepted;
        _emitterWorldZRejected += state.WorldZRejected;
        _emitterBoundsRejected += state.BoundsRejected;
        _emitterQueryTimestampTicks += elapsed;
        _emitterLastQueryTimestampTicks = elapsed;
        _emitterMaxQueryTimestampTicks = Math.Max(_emitterMaxQueryTimestampTicks, elapsed);
        return results.Count - initialCount;
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
        _emitterQueries = 0;
        _emitterCandidates = 0;
        _emitterAccepted = 0;
        _emitterWorldZRejected = 0;
        _emitterBoundsRejected = 0;
        _emitterQueryTimestampTicks = 0;
        _emitterLastQueryTimestampTicks = 0;
        _emitterMaxQueryTimestampTicks = 0;
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
            _emitterQueries,
            _emitterCandidates,
            _emitterAccepted,
            _emitterWorldZRejected,
            _emitterBoundsRejected,
            _emitterQueryTimestampTicks,
            _emitterLastQueryTimestampTicks,
            _emitterMaxQueryTimestampTicks,
            _apertures.Count,
            _cachedOpenTiles);
    }

    private ZLevelApertureChunk BuildApertureChunk(
        Entity<MapGridComponent> grid,
        ZLevelApertureChunkKey key)
    {
        Span<ulong> words = stackalloc ulong[ZLevelApertureChunk.WordCount];
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
                words[bit >> 6] |= 1UL << (bit & 63);
                openCount++;
            }
        }

        return new ZLevelApertureChunk(
            key,
            ++_nextRevision,
            words[0],
            words[1],
            words[2],
            words[3],
            openCount);
    }

    private static bool QueryEmitter(
        ref EmitterQueryState state,
        in ComponentTreeEntry<PointLightComponent> entry)
    {
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
    }

    private struct EmitterQueryState(
        ZLevelLightingCacheSystem system,
        Box2 worldBounds,
        int minimumWorldZ,
        int maximumWorldZ,
        List<ZLevelLightEmitter> results)
    {
        public readonly ZLevelLightingCacheSystem System = system;
        public readonly Box2 WorldBounds = worldBounds;
        public readonly int MinimumWorldZ = minimumWorldZ;
        public readonly int MaximumWorldZ = maximumWorldZ;
        public readonly List<ZLevelLightEmitter> Results = results;
        public int Candidates;
        public int Accepted;
        public int WorldZRejected;
        public int BoundsRejected;
    }
}

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

public readonly record struct ZLevelLightEmitter(
    EntityUid Uid,
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
    long EmitterQueries,
    long EmitterCandidates,
    long EmitterAccepted,
    long EmitterWorldZRejected,
    long EmitterBoundsRejected,
    long EmitterQueryTimestampTicks,
    long EmitterLastQueryTimestampTicks,
    long EmitterMaxQueryTimestampTicks,
    int CachedApertureChunks,
    int CachedOpenApertureTiles)
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
