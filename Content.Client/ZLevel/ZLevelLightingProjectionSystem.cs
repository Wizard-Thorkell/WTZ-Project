// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.ZLevel;

/// <summary>
/// Builds deterministic, aperture-clipped lower-floor light projection plans.
/// Rendering consumes the retained batches and runs without owning visibility
/// policy or emitter discovery.
/// </summary>
public sealed class ZLevelLightingProjectionSystem : EntitySystem
{
    public const float VerticalDistancePerLevel = 0.75f;
    public const float TransmissionPerLevel = 0.72f;
    public const float NativeLightHeightSquared = 1f;

    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ZLevelLightingCacheSystem _cache = default!;

    private readonly List<ZLevelLightEmitter> _emitters = new();
    private readonly List<ZLevelLightProjectionBatch> _batches = new();
    private readonly List<ZLevelLightProjectionRun> _runs = new();

    private long _frames;
    private long _emitterInputs;
    private long _emittersProjected;
    private long _radiusRejections;
    private long _gridCandidates;
    private long _stackChunks;
    private long _stackBoundaryLayers;
    private long _visibleRuns;
    private long _visibleTiles;
    private long _buildTimestampTicks;
    private long _lastBuildTimestampTicks;
    private long _maxBuildTimestampTicks;
    private long _renderFrames;
    private long _renderBatches;
    private long _renderRuns;
    private long _renderVertices;
    private long _renderDrawCalls;
    private long _renderTimestampTicks;
    private long _lastRenderTimestampTicks;
    private long _maxRenderTimestampTicks;

    private ZLevelLightingProjectionOverlay _overlay = default!;

    public IReadOnlyList<ZLevelLightProjectionBatch> Batches => _batches;
    public IReadOnlyList<ZLevelLightProjectionRun> Runs => _runs;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new ZLevelLightingProjectionOverlay(this, EntityManager);
        _overlayManager.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        _overlayManager.RemoveOverlay(_overlay);
        base.Shutdown();
    }

    /// <summary>
    /// Rebuilds the retained projection plan for one viewport AABB.
    /// The active floor is excluded because Clyde renders it natively.
    /// </summary>
    public int BuildProjection(MapId mapId, Box2 worldBounds, int viewerWorldZ)
    {
        var started = Stopwatch.GetTimestamp();
        _batches.Clear();
        _runs.Clear();
        _emitters.Clear();

        if (mapId == MapId.Nullspace ||
            _visibility.MaxVisibleLevelDistance <= 0 ||
            !worldBounds.IsValid() ||
            worldBounds.Width <= 0f ||
            worldBounds.Height <= 0f)
        {
            RecordFrame(started);
            return 0;
        }

        var maximumWorldZ = (int) Math.Max(int.MinValue, (long) viewerWorldZ - 1);
        var minimumWorldZ = (int) Math.Max(
            int.MinValue,
            (long) viewerWorldZ - _visibility.MaxVisibleLevelDistance);
        if (minimumWorldZ > maximumWorldZ)
        {
            RecordFrame(started);
            return 0;
        }

        _cache.QueryEmitters(
            mapId,
            worldBounds,
            minimumWorldZ,
            maximumWorldZ,
            _emitters);
        SortEmitters();
        _emitterInputs += _emitters.Count;

        foreach (var emitter in _emitters)
        {
            var depth = viewerWorldZ - emitter.WorldZ;
            var verticalDistance = depth * VerticalDistancePerLevel;
            var projectedRadiusSquared = emitter.Radius * emitter.Radius -
                                         verticalDistance * verticalDistance -
                                         NativeLightHeightSquared;
            if (projectedRadiusSquared <= 0f)
            {
                _radiusRejections++;
                continue;
            }

            var projectedRadius = MathF.Sqrt(projectedRadiusSquared);
            var transmission = MathF.Pow(TransmissionPerLevel, depth);
            _gridCandidates++;
            if (!TryComp<MapGridComponent>(emitter.GridUid, out var grid) || grid.Deleted)
                continue;

            var viewerLocalZ = _transform.WorldToLocalZLevel(emitter.GridUid, viewerWorldZ);
            var targetLocalZ = _transform.WorldToLocalZLevel(emitter.GridUid, emitter.WorldZ);
            if (targetLocalZ >= viewerLocalZ)
                continue;

            var firstRun = _runs.Count;
            var firstTileCount = _visibleTiles;
            AddGridRuns(
                (emitter.GridUid, grid),
                worldBounds,
                emitter.WorldPosition,
                projectedRadius,
                targetLocalZ,
                viewerLocalZ);

            var runCount = _runs.Count - firstRun;
            if (runCount == 0)
                continue;

            _batches.Add(new ZLevelLightProjectionBatch(
                emitter,
                emitter.GridUid,
                targetLocalZ,
                viewerLocalZ,
                depth,
                projectedRadius,
                transmission,
                firstRun,
                runCount,
                checked((int) (_visibleTiles - firstTileCount))));
            _emittersProjected++;
        }

        _emitters.Clear();
        RecordFrame(started);
        return _batches.Count;
    }

    public void ResetMetrics()
    {
        _frames = 0;
        _emitterInputs = 0;
        _emittersProjected = 0;
        _radiusRejections = 0;
        _gridCandidates = 0;
        _stackChunks = 0;
        _stackBoundaryLayers = 0;
        _visibleRuns = 0;
        _visibleTiles = 0;
        _buildTimestampTicks = 0;
        _lastBuildTimestampTicks = 0;
        _maxBuildTimestampTicks = 0;
        _renderFrames = 0;
        _renderBatches = 0;
        _renderRuns = 0;
        _renderVertices = 0;
        _renderDrawCalls = 0;
        _renderTimestampTicks = 0;
        _lastRenderTimestampTicks = 0;
        _maxRenderTimestampTicks = 0;
    }

    public ZLevelLightingProjectionMetrics Snapshot()
    {
        return new ZLevelLightingProjectionMetrics(
            _frames,
            _emitterInputs,
            _emittersProjected,
            _radiusRejections,
            _gridCandidates,
            _stackChunks,
            _stackBoundaryLayers,
            _visibleRuns,
            _visibleTiles,
            _buildTimestampTicks,
            _lastBuildTimestampTicks,
            _maxBuildTimestampTicks,
            _batches.Count,
            _runs.Count,
            _renderFrames,
            _renderBatches,
            _renderRuns,
            _renderVertices,
            _renderDrawCalls,
            _renderTimestampTicks,
            _lastRenderTimestampTicks,
            _maxRenderTimestampTicks);
    }

    internal void RecordRender(
        long started,
        int batches,
        int runs,
        int vertices,
        int drawCalls)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _renderFrames++;
        _renderBatches += batches;
        _renderRuns += runs;
        _renderVertices += vertices;
        _renderDrawCalls += drawCalls;
        _renderTimestampTicks += elapsed;
        _lastRenderTimestampTicks = elapsed;
        _maxRenderTimestampTicks = Math.Max(_maxRenderTimestampTicks, elapsed);
    }

    private void AddGridRuns(
        Entity<MapGridComponent> grid,
        Box2 worldBounds,
        Vector2 emitterWorldPosition,
        float projectedRadius,
        int targetLocalZ,
        int viewerLocalZ)
    {
        var (_, _, _, inverseWorldMatrix) =
            _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
        var localBounds = inverseWorldMatrix.TransformBox(worldBounds);
        var emitterLocalPosition = Vector2.Transform(emitterWorldPosition, inverseWorldMatrix);
        var radiusVector = new Vector2(projectedRadius);
        var lightBounds = new Box2(
            emitterLocalPosition - radiusVector,
            emitterLocalPosition + radiusVector);

        if (!localBounds.Intersects(lightBounds))
            return;

        var bounds = localBounds.Intersect(lightBounds);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return;

        var tileSize = grid.Comp.TileSize;
        var minimumTile = new Vector2i(
            (int) MathF.Floor(bounds.Left / tileSize),
            (int) MathF.Floor(bounds.Bottom / tileSize));
        var maximumTile = new Vector2i(
            (int) MathF.Ceiling(bounds.Right / tileSize) - 1,
            (int) MathF.Ceiling(bounds.Top / tileSize) - 1);
        if (minimumTile.X > maximumTile.X || minimumTile.Y > maximumTile.Y)
            return;

        var minimumChunk = SharedMapSystem.GetChunkIndices(
            minimumTile,
            ZLevelApertureChunk.ChunkSize);
        var maximumChunk = SharedMapSystem.GetChunkIndices(
            maximumTile,
            ZLevelApertureChunk.ChunkSize);

        for (var chunkY = minimumChunk.Y; chunkY <= maximumChunk.Y; chunkY++)
        {
            for (var chunkX = minimumChunk.X; chunkX <= maximumChunk.X; chunkX++)
            {
                var chunkIndices = new Vector2i(chunkX, chunkY);
                if (!_cache.TryComposeApertureStack(
                        grid,
                        chunkIndices,
                        targetLocalZ,
                        viewerLocalZ,
                        out var stack))
                {
                    continue;
                }

                _stackChunks++;
                _stackBoundaryLayers += viewerLocalZ - targetLocalZ;
                if (stack.OpenCount == 0)
                    continue;

                AddChunkRuns(grid.Owner, targetLocalZ, minimumTile, maximumTile, stack);
            }
        }
    }

    private void AddChunkRuns(
        EntityUid gridUid,
        int targetLocalZ,
        Vector2i minimumTile,
        Vector2i maximumTile,
        in ZLevelApertureStack stack)
    {
        var origin = stack.ChunkIndices * ZLevelApertureChunk.ChunkSize;
        var minimumRelativeX = Math.Max(0, minimumTile.X - origin.X);
        var maximumRelativeX = Math.Min(
            ZLevelApertureChunk.ChunkSize - 1,
            maximumTile.X - origin.X);
        var minimumRelativeY = Math.Max(0, minimumTile.Y - origin.Y);
        var maximumRelativeY = Math.Min(
            ZLevelApertureChunk.ChunkSize - 1,
            maximumTile.Y - origin.Y);
        if (minimumRelativeX > maximumRelativeX || minimumRelativeY > maximumRelativeY)
            return;

        for (var relativeY = minimumRelativeY; relativeY <= maximumRelativeY; relativeY++)
        {
            var open = stack.GetRowBits(relativeY);
            var relativeX = minimumRelativeX;
            while (relativeX <= maximumRelativeX)
            {
                while (relativeX <= maximumRelativeX && (open & (1U << relativeX)) == 0)
                    relativeX++;

                if (relativeX > maximumRelativeX)
                    break;

                var runStart = relativeX;
                while (relativeX <= maximumRelativeX && (open & (1U << relativeX)) != 0)
                    relativeX++;

                var runEnd = relativeX - 1;
                _runs.Add(new ZLevelLightProjectionRun(
                    gridUid,
                    targetLocalZ,
                    origin.Y + relativeY,
                    origin.X + runStart,
                    origin.X + runEnd));
                _visibleRuns++;
                _visibleTiles += runEnd - runStart + 1;
            }
        }
    }

    private void RecordFrame(long started)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _frames++;
        _buildTimestampTicks += elapsed;
        _lastBuildTimestampTicks = elapsed;
        _maxBuildTimestampTicks = Math.Max(_maxBuildTimestampTicks, elapsed);
    }

    private void SortEmitters()
    {
        for (var i = 1; i < _emitters.Count; i++)
        {
            var emitter = _emitters[i];
            var insertionIndex = i;
            while (insertionIndex > 0 &&
                   ZLevelLightEmitterComparer.Instance.Compare(_emitters[insertionIndex - 1], emitter) > 0)
            {
                _emitters[insertionIndex] = _emitters[insertionIndex - 1];
                insertionIndex--;
            }

            _emitters[insertionIndex] = emitter;
        }
    }

    private sealed class ZLevelLightEmitterComparer : IComparer<ZLevelLightEmitter>
    {
        public static readonly ZLevelLightEmitterComparer Instance = new();

        public int Compare(ZLevelLightEmitter x, ZLevelLightEmitter y)
        {
            var z = x.WorldZ.CompareTo(y.WorldZ);
            return z != 0 ? z : x.Uid.CompareTo(y.Uid);
        }
    }
}

public readonly record struct ZLevelLightProjectionBatch(
    ZLevelLightEmitter Emitter,
    EntityUid GridUid,
    int TargetLocalZ,
    int ViewerLocalZ,
    int Depth,
    float ProjectedRadius,
    float Transmission,
    int FirstRun,
    int RunCount,
    int VisibleTileCount);

public readonly record struct ZLevelLightProjectionRun(
    EntityUid GridUid,
    int TargetLocalZ,
    int Y,
    int StartX,
    int EndX)
{
    public int TileCount => EndX - StartX + 1;

    public bool Contains(Vector2i tile)
    {
        return tile.Y == Y && tile.X >= StartX && tile.X <= EndX;
    }
}

public readonly record struct ZLevelLightingProjectionMetrics(
    long Frames,
    long EmitterInputs,
    long EmittersProjected,
    long RadiusRejections,
    long GridCandidates,
    long StackChunks,
    long StackBoundaryLayers,
    long VisibleRuns,
    long VisibleTiles,
    long BuildTimestampTicks,
    long LastBuildTimestampTicks,
    long MaxBuildTimestampTicks,
    int CurrentBatches,
    int CurrentRuns,
    long RenderFrames,
    long RenderBatches,
    long RenderRuns,
    long RenderVertices,
    long RenderDrawCalls,
    long RenderTimestampTicks,
    long LastRenderTimestampTicks,
    long MaxRenderTimestampTicks)
{
    public double BuildMilliseconds => ToMilliseconds(BuildTimestampTicks);
    public double AverageBuildMilliseconds => Frames == 0 ? 0d : BuildMilliseconds / Frames;
    public double LastBuildMilliseconds => ToMilliseconds(LastBuildTimestampTicks);
    public double MaxBuildMilliseconds => ToMilliseconds(MaxBuildTimestampTicks);
    public double RenderMilliseconds => ToMilliseconds(RenderTimestampTicks);
    public double AverageRenderMilliseconds => RenderFrames == 0 ? 0d : RenderMilliseconds / RenderFrames;
    public double LastRenderMilliseconds => ToMilliseconds(LastRenderTimestampTicks);
    public double MaxRenderMilliseconds => ToMilliseconds(MaxRenderTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
