// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using IGameTiming = Robust.Shared.Timing.IGameTiming;

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

    public const int DefaultMaxEmitterCandidatesPerFrame = 4_096;
    public const int DefaultMaxEmittersPerFrame = 256;
    public const int DefaultMaxApertureLayersPerFrame = 4_096;
    public const int DefaultMaxApertureBuildsPerFrame = 32;
    public const int DefaultMaxRunsPerFrame = 8_192;
    public const int MaximumEmitterCandidatesPerFrame = 65_536;
    public const int MaximumEmittersPerFrame = 4_096;
    public const int MaximumApertureLayersPerFrame = 1_000_000;
    public const int MaximumApertureBuildsPerFrame = 4_096;
    public const int MaximumRunsPerFrame = 1_000_000;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
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

    private int _maxEmitterCandidatesPerFrame = DefaultMaxEmitterCandidatesPerFrame;
    private int _maxEmittersPerFrame = DefaultMaxEmittersPerFrame;
    private int _maxApertureLayersPerFrame = DefaultMaxApertureLayersPerFrame;
    private int _maxApertureBuildsPerFrame = DefaultMaxApertureBuildsPerFrame;
    private int _maxRunsPerFrame = DefaultMaxRunsPerFrame;
    private int _remainingEmitterCandidates;
    private int _remainingEmitters;
    private int _remainingApertureLayers;
    private int _remainingApertureBuilds;
    private int _remainingRuns;
    private int _currentEmitterCandidatesUsed;
    private int _currentEmittersUsed;
    private int _currentApertureLayersUsed;
    private int _currentApertureBuildsUsed;
    private int _currentRunsUsed;
    private uint _budgetFrame;
    private bool _budgetInitialized;
    private bool _candidateBudgetExhaustedThisFrame;
    private bool _emitterBudgetExhaustedThisFrame;
    private bool _apertureLayerBudgetExhaustedThisFrame;
    private bool _apertureBuildBudgetExhaustedThisFrame;
    private bool _runBudgetExhaustedThisFrame;
    private long _candidateBudgetExhaustions;
    private long _emitterBudgetExhaustions;
    private long _apertureLayerBudgetExhaustions;
    private long _apertureBuildBudgetExhaustions;
    private long _runBudgetExhaustions;

    private ZLevelLightingProjectionOverlay _overlay = default!;

    public IReadOnlyList<ZLevelLightProjectionBatch> Batches => _batches;
    public IReadOnlyList<ZLevelLightProjectionRun> Runs => _runs;
    public int MaxEmitterCandidatesPerFrame => _maxEmitterCandidatesPerFrame;
    public int MaxEmittersPerFrame => _maxEmittersPerFrame;
    public int MaxApertureLayersPerFrame => _maxApertureLayersPerFrame;
    public int MaxApertureBuildsPerFrame => _maxApertureBuildsPerFrame;
    public int MaxRunsPerFrame => _maxRunsPerFrame;

    public override void Initialize()
    {
        base.Initialize();
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingMaxEmitterCandidatesPerFrame,
            value => SetBudget(ref _maxEmitterCandidatesPerFrame, value, MaximumEmitterCandidatesPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingMaxEmittersPerFrame,
            value => SetBudget(ref _maxEmittersPerFrame, value, MaximumEmittersPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingMaxApertureLayersPerFrame,
            value => SetBudget(ref _maxApertureLayersPerFrame, value, MaximumApertureLayersPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingMaxApertureBuildsPerFrame,
            value => SetBudget(ref _maxApertureBuildsPerFrame, value, MaximumApertureBuildsPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelLightingMaxRunsPerFrame,
            value => SetBudget(ref _maxRunsPerFrame, value, MaximumRunsPerFrame),
            true);
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
        EnsureFrameBudget();

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

        var query = _cache.QueryEmitters(
            mapId,
            worldBounds,
            minimumWorldZ,
            maximumWorldZ,
            _emitters,
            _remainingEmitterCandidates);
        _remainingEmitterCandidates -= query.CandidatesVisited;
        _currentEmitterCandidatesUsed += query.CandidatesVisited;
        if (query.CandidateBudgetExceeded)
        {
            RecordBudgetExhaustion(
                ref _candidateBudgetExhaustedThisFrame,
                ref _candidateBudgetExhaustions);
        }

        SortEmitters();
        _emitterInputs += _emitters.Count;

        foreach (var emitter in _emitters)
        {
            if (_remainingEmitters <= 0)
            {
                RecordBudgetExhaustion(
                    ref _emitterBudgetExhaustedThisFrame,
                    ref _emitterBudgetExhaustions);
                break;
            }

            _remainingEmitters--;
            _currentEmittersUsed++;
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
            var planResult = AddGridRuns(
                (emitter.GridUid, grid),
                worldBounds,
                emitter.WorldPosition,
                projectedRadius,
                targetLocalZ,
                viewerLocalZ,
                out var visibleTiles);

            if (planResult != ZLevelProjectionPlanResult.Complete)
            {
                var addedRuns = _runs.Count - firstRun;
                if (addedRuns > 0)
                    _runs.RemoveRange(firstRun, addedRuns);
                break;
            }

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
                visibleTiles));
            _emittersProjected++;
            _visibleRuns += runCount;
            _visibleTiles += visibleTiles;
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
        _candidateBudgetExhaustions = 0;
        _emitterBudgetExhaustions = 0;
        _apertureLayerBudgetExhaustions = 0;
        _apertureBuildBudgetExhaustions = 0;
        _runBudgetExhaustions = 0;
        _budgetInitialized = false;
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
            _maxRenderTimestampTicks,
            _candidateBudgetExhaustions,
            _emitterBudgetExhaustions,
            _apertureLayerBudgetExhaustions,
            _apertureBuildBudgetExhaustions,
            _runBudgetExhaustions,
            _currentEmitterCandidatesUsed,
            _currentEmittersUsed,
            _currentApertureLayersUsed,
            _currentApertureBuildsUsed,
            _currentRunsUsed,
            _maxEmitterCandidatesPerFrame,
            _maxEmittersPerFrame,
            _maxApertureLayersPerFrame,
            _maxApertureBuildsPerFrame,
            _maxRunsPerFrame);
    }

    internal void BeginBudgetFrameForTesting()
    {
        _budgetInitialized = false;
        EnsureFrameBudget();
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

    private ZLevelProjectionPlanResult AddGridRuns(
        Entity<MapGridComponent> grid,
        Box2 worldBounds,
        Vector2 emitterWorldPosition,
        float projectedRadius,
        int targetLocalZ,
        int viewerLocalZ,
        out int visibleTiles)
    {
        visibleTiles = 0;
        var (_, _, _, inverseWorldMatrix) =
            _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
        var localBounds = inverseWorldMatrix.TransformBox(worldBounds);
        var emitterLocalPosition = Vector2.Transform(emitterWorldPosition, inverseWorldMatrix);
        var radiusVector = new Vector2(projectedRadius);
        var lightBounds = new Box2(
            emitterLocalPosition - radiusVector,
            emitterLocalPosition + radiusVector);

        if (!localBounds.Intersects(lightBounds))
            return ZLevelProjectionPlanResult.Complete;

        var bounds = localBounds.Intersect(lightBounds);
        if (bounds.Width <= 0f || bounds.Height <= 0f)
            return ZLevelProjectionPlanResult.Complete;

        var tileSize = grid.Comp.TileSize;
        var minimumTile = new Vector2i(
            (int) MathF.Floor(bounds.Left / tileSize),
            (int) MathF.Floor(bounds.Bottom / tileSize));
        var maximumTile = new Vector2i(
            (int) MathF.Ceiling(bounds.Right / tileSize) - 1,
            (int) MathF.Ceiling(bounds.Top / tileSize) - 1);
        if (minimumTile.X > maximumTile.X || minimumTile.Y > maximumTile.Y)
            return ZLevelProjectionPlanResult.Complete;

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
                var stackResult = TryComposeApertureStack(
                    grid,
                    chunkIndices,
                    targetLocalZ,
                    viewerLocalZ,
                    out var stack);
                switch (stackResult)
                {
                    case ZLevelApertureStackQueryResult.LayerBudgetExceeded:
                        return ZLevelProjectionPlanResult.ApertureLayerBudgetExceeded;
                    case ZLevelApertureStackQueryResult.BuildBudgetExceeded:
                        return ZLevelProjectionPlanResult.ApertureBuildBudgetExceeded;
                    case ZLevelApertureStackQueryResult.Invalid:
                        continue;
                }

                _stackChunks++;
                if (stack.OpenCount == 0)
                    continue;

                if (!AddChunkRuns(
                        grid.Owner,
                        targetLocalZ,
                        minimumTile,
                        maximumTile,
                        stack,
                        ref visibleTiles))
                {
                    return ZLevelProjectionPlanResult.RunBudgetExceeded;
                }
            }
        }

        return ZLevelProjectionPlanResult.Complete;
    }

    private bool AddChunkRuns(
        EntityUid gridUid,
        int targetLocalZ,
        Vector2i minimumTile,
        Vector2i maximumTile,
        in ZLevelApertureStack stack,
        ref int visibleTiles)
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
            return true;

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
                if (_remainingRuns <= 0)
                {
                    RecordBudgetExhaustion(
                        ref _runBudgetExhaustedThisFrame,
                        ref _runBudgetExhaustions);
                    return false;
                }

                _remainingRuns--;
                _currentRunsUsed++;
                _runs.Add(new ZLevelLightProjectionRun(
                    gridUid,
                    targetLocalZ,
                    origin.Y + relativeY,
                    origin.X + runStart,
                    origin.X + runEnd));
                visibleTiles += runEnd - runStart + 1;
            }
        }

        return true;
    }

    private ZLevelApertureStackQueryResult TryComposeApertureStack(
        Entity<MapGridComponent> grid,
        Vector2i chunkIndices,
        int targetLocalZ,
        int viewerLocalZ,
        out ZLevelApertureStack stack)
    {
        var budget = new ZLevelApertureQueryBudget(
            _remainingApertureLayers,
            _remainingApertureBuilds);
        var result = _cache.TryComposeApertureStack(
            grid,
            chunkIndices,
            targetLocalZ,
            viewerLocalZ,
            ref budget,
            out stack);

        var layersUsed = _remainingApertureLayers - budget.RemainingLayers;
        var buildsUsed = _remainingApertureBuilds - budget.RemainingBuilds;
        _remainingApertureLayers = budget.RemainingLayers;
        _remainingApertureBuilds = budget.RemainingBuilds;
        _currentApertureLayersUsed += layersUsed;
        _currentApertureBuildsUsed += buildsUsed;
        _stackBoundaryLayers += layersUsed;

        switch (result)
        {
            case ZLevelApertureStackQueryResult.LayerBudgetExceeded:
                RecordBudgetExhaustion(
                    ref _apertureLayerBudgetExhaustedThisFrame,
                    ref _apertureLayerBudgetExhaustions);
                break;
            case ZLevelApertureStackQueryResult.BuildBudgetExceeded:
                RecordBudgetExhaustion(
                    ref _apertureBuildBudgetExhaustedThisFrame,
                    ref _apertureBuildBudgetExhaustions);
                break;
        }

        return result;
    }

    private void SetBudget(ref int field, int configured, int maximum)
    {
        field = Math.Clamp(configured, 0, maximum);
        _budgetInitialized = false;
    }

    private void EnsureFrameBudget()
    {
        var frame = _timing.CurFrame;
        if (_budgetInitialized && _budgetFrame == frame)
            return;

        _budgetInitialized = true;
        _budgetFrame = frame;
        _remainingEmitterCandidates = _maxEmitterCandidatesPerFrame;
        _remainingEmitters = _maxEmittersPerFrame;
        _remainingApertureLayers = _maxApertureLayersPerFrame;
        _remainingApertureBuilds = _maxApertureBuildsPerFrame;
        _remainingRuns = _maxRunsPerFrame;
        _currentEmitterCandidatesUsed = 0;
        _currentEmittersUsed = 0;
        _currentApertureLayersUsed = 0;
        _currentApertureBuildsUsed = 0;
        _currentRunsUsed = 0;
        _candidateBudgetExhaustedThisFrame = false;
        _emitterBudgetExhaustedThisFrame = false;
        _apertureLayerBudgetExhaustedThisFrame = false;
        _apertureBuildBudgetExhaustedThisFrame = false;
        _runBudgetExhaustedThisFrame = false;
    }

    private static void RecordBudgetExhaustion(ref bool frameFlag, ref long counter)
    {
        if (frameFlag)
            return;

        frameFlag = true;
        counter++;
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
            // Higher lower-floor world Z is closer to the viewer and survives fail-soft limits first.
            var z = y.WorldZ.CompareTo(x.WorldZ);
            return z != 0 ? z : x.Uid.CompareTo(y.Uid);
        }
    }
}

internal enum ZLevelProjectionPlanResult : byte
{
    Complete,
    ApertureLayerBudgetExceeded,
    ApertureBuildBudgetExceeded,
    RunBudgetExceeded,
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
    long MaxRenderTimestampTicks,
    long CandidateBudgetExhaustions,
    long EmitterBudgetExhaustions,
    long ApertureLayerBudgetExhaustions,
    long ApertureBuildBudgetExhaustions,
    long RunBudgetExhaustions,
    int CurrentEmitterCandidatesUsed,
    int CurrentEmittersUsed,
    int CurrentApertureLayersUsed,
    int CurrentApertureBuildsUsed,
    int CurrentRunsUsed,
    int MaxEmitterCandidatesPerFrame,
    int MaxEmittersPerFrame,
    int MaxApertureLayersPerFrame,
    int MaxApertureBuildsPerFrame,
    int MaxRunsPerFrame)
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
