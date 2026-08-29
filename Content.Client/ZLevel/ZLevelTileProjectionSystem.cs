// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using IGameTiming = Robust.Shared.Timing.IGameTiming;

namespace Content.Client.ZLevel;

/// <summary>
/// Builds retained, chunk-atomic plans for normal lower-floor tiles and
/// adjacent mapping preview without sharing projection-light frame budgets.
/// </summary>
public sealed class ZLevelTileProjectionSystem : EntitySystem
{
    public const int DefaultMaxChunksPerFrame = 128;
    public const int DefaultMaxApertureLayersPerFrame = 4_096;
    public const int DefaultMaxApertureBuildsPerFrame = 32;
    public const int DefaultMaxTileVisitsPerFrame = 16_384;
    public const int DefaultMappingMaxChunksPerFrame = 128;
    public const int DefaultMappingMaxTileVisitsPerFrame = 16_384;

    public const int MaximumChunksPerFrame = 4_096;
    public const int MaximumApertureLayersPerFrame = 1_000_000;
    public const int MaximumApertureBuildsPerFrame = 4_096;
    public const int MaximumTileVisitsPerFrame = 1_000_000;
    public const int MaximumMappingChunksPerFrame = 4_096;
    public const int MaximumMappingTileVisitsPerFrame = 1_000_000;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ZLevelLightingCacheSystem _lightingCache = default!;

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<ZLevelTileProjectionGridContext> _gridContexts = new();
    private readonly List<ZLevelTileProjectionBatch> _batches = new();
    private readonly List<ZLevelTileProjectionTile> _tiles = new();
    private readonly ZLevelTileProjectionFrameBudget _normalBudget = new();
    private readonly ZLevelTileProjectionFrameBudget _mappingBudget = new();

    private int _maxChunksPerFrame = DefaultMaxChunksPerFrame;
    private int _maxApertureLayersPerFrame = DefaultMaxApertureLayersPerFrame;
    private int _maxApertureBuildsPerFrame = DefaultMaxApertureBuildsPerFrame;
    private int _maxTileVisitsPerFrame = DefaultMaxTileVisitsPerFrame;
    private int _mappingMaxChunksPerFrame = DefaultMappingMaxChunksPerFrame;
    private int _mappingMaxTileVisitsPerFrame = DefaultMappingMaxTileVisitsPerFrame;

    private long _frames;
    private long _mappingFrames;
    private long _gridCandidates;
    private long _chunkCandidates;
    private long _chunksCompleted;
    private long _chunksProjected;
    private long _apertureLayers;
    private long _apertureBuilds;
    private long _tileVisits;
    private long _tilesProjected;
    private long _buildTimestampTicks;
    private long _lastBuildTimestampTicks;
    private long _maxBuildTimestampTicks;
    private long _renderFrames;
    private long _mappingRenderFrames;
    private long _renderBatches;
    private long _renderTiles;
    private long _renderVertices;
    private long _renderDrawCalls;
    private long _renderTimestampTicks;
    private long _lastRenderTimestampTicks;
    private long _maxRenderTimestampTicks;

    public IReadOnlyList<ZLevelTileProjectionBatch> Batches => _batches;
    public IReadOnlyList<ZLevelTileProjectionTile> Tiles => _tiles;
    public int MaxChunksPerFrame => _maxChunksPerFrame;
    public int MaxApertureLayersPerFrame => _maxApertureLayersPerFrame;
    public int MaxApertureBuildsPerFrame => _maxApertureBuildsPerFrame;
    public int MaxTileVisitsPerFrame => _maxTileVisitsPerFrame;
    public int MappingMaxChunksPerFrame => _mappingMaxChunksPerFrame;
    public int MappingMaxTileVisitsPerFrame => _mappingMaxTileVisitsPerFrame;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(
            _configuration,
            CCVars.ZLevelTileProjectionMaxChunksPerFrame,
            value => SetLimit(
                ref _maxChunksPerFrame,
                value,
                MaximumChunksPerFrame,
                _normalBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTileProjectionMaxApertureLayersPerFrame,
            value => SetLimit(
                ref _maxApertureLayersPerFrame,
                value,
                MaximumApertureLayersPerFrame,
                _normalBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTileProjectionMaxApertureBuildsPerFrame,
            value => SetLimit(
                ref _maxApertureBuildsPerFrame,
                value,
                MaximumApertureBuildsPerFrame,
                _normalBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTileProjectionMaxTileVisitsPerFrame,
            value => SetLimit(
                ref _maxTileVisitsPerFrame,
                value,
                MaximumTileVisitsPerFrame,
                _normalBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelMappingPreviewMaxChunksPerFrame,
            value => SetLimit(
                ref _mappingMaxChunksPerFrame,
                value,
                MaximumMappingChunksPerFrame,
                _mappingBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelMappingPreviewMaxTileVisitsPerFrame,
            value => SetLimit(
                ref _mappingMaxTileVisitsPerFrame,
                value,
                MaximumMappingTileVisitsPerFrame,
                _mappingBudget),
            true);
    }

    public override void Shutdown()
    {
        _grids.Clear();
        _gridContexts.Clear();
        _batches.Clear();
        _tiles.Clear();
        base.Shutdown();
    }

    /// <summary>
    /// Rebuilds one viewport plan. Normal mode processes lower floors nearest
    /// first; mapping mode processes the adjacent lower floor before the upper.
    /// Retained batches are sorted into far-to-near draw order afterward.
    /// </summary>
    public int BuildProjection(
        MapId mapId,
        Box2 worldBounds,
        int viewerWorldZ,
        bool mappingPreview)
    {
        var started = Stopwatch.GetTimestamp();
        _batches.Clear();
        _tiles.Clear();
        _grids.Clear();
        _gridContexts.Clear();

        var budget = EnsureFrameBudget(mappingPreview);
        if (mapId == MapId.Nullspace ||
            !worldBounds.IsValid() ||
            worldBounds.Width <= 0f ||
            worldBounds.Height <= 0f ||
            (!mappingPreview && _visibility.MaxVisibleLevelDistance <= 0))
        {
            RecordBuild(started, mappingPreview);
            return 0;
        }

        var hasAuthoredRange = false;
        var minimumLocalZ = int.MinValue;
        var maximumLocalZ = int.MaxValue;
        if (_map.TryGetMap(mapId, out var mapUid) &&
            TryComp<ZLevelMapComponent>(mapUid.Value, out var mapConfig))
        {
            hasAuthoredRange = true;
            minimumLocalZ = mapConfig.MinimumLevel;
            maximumLocalZ = mapConfig.MaximumLevel;
        }

        _mapManager.FindGridsIntersecting(
            mapId,
            worldBounds,
            ref _grids,
            approx: true,
            includeMap: false);
        BuildGridContexts(worldBounds, viewerWorldZ);
        SortGridContexts();
        _gridCandidates += _gridContexts.Count;

        if (mappingPreview)
            BuildMappingPreview(budget, hasAuthoredRange, minimumLocalZ, maximumLocalZ);
        else
            BuildNormalProjection(budget, hasAuthoredRange, minimumLocalZ, maximumLocalZ);

        SortBatchesForDrawing();
        _grids.Clear();
        _gridContexts.Clear();
        RecordBuild(started, mappingPreview);
        return _batches.Count;
    }

    public void ResetMetrics()
    {
        _frames = 0;
        _mappingFrames = 0;
        _gridCandidates = 0;
        _chunkCandidates = 0;
        _chunksCompleted = 0;
        _chunksProjected = 0;
        _apertureLayers = 0;
        _apertureBuilds = 0;
        _tileVisits = 0;
        _tilesProjected = 0;
        _buildTimestampTicks = 0;
        _lastBuildTimestampTicks = 0;
        _maxBuildTimestampTicks = 0;
        _renderFrames = 0;
        _mappingRenderFrames = 0;
        _renderBatches = 0;
        _renderTiles = 0;
        _renderVertices = 0;
        _renderDrawCalls = 0;
        _renderTimestampTicks = 0;
        _lastRenderTimestampTicks = 0;
        _maxRenderTimestampTicks = 0;
        _normalBudget.ResetMetrics();
        _mappingBudget.ResetMetrics();
    }

    public ZLevelTileProjectionMetrics Snapshot()
    {
        return new ZLevelTileProjectionMetrics(
            _frames,
            _mappingFrames,
            _gridCandidates,
            _chunkCandidates,
            _chunksCompleted,
            _chunksProjected,
            _apertureLayers,
            _apertureBuilds,
            _tileVisits,
            _tilesProjected,
            _buildTimestampTicks,
            _lastBuildTimestampTicks,
            _maxBuildTimestampTicks,
            _batches.Count,
            _tiles.Count,
            _renderFrames,
            _mappingRenderFrames,
            _renderBatches,
            _renderTiles,
            _renderVertices,
            _renderDrawCalls,
            _renderTimestampTicks,
            _lastRenderTimestampTicks,
            _maxRenderTimestampTicks,
            CreateBudgetSnapshot(
                _normalBudget,
                _maxChunksPerFrame,
                _maxApertureLayersPerFrame,
                _maxApertureBuildsPerFrame,
                _maxTileVisitsPerFrame),
            CreateBudgetSnapshot(
                _mappingBudget,
                _mappingMaxChunksPerFrame,
                0,
                0,
                _mappingMaxTileVisitsPerFrame));
    }

    internal void BeginBudgetFrameForTesting(bool mappingPreview)
    {
        var budget = mappingPreview ? _mappingBudget : _normalBudget;
        budget.Initialized = false;
        EnsureFrameBudget(mappingPreview);
    }

    internal void RecordRender(
        long started,
        bool mappingPreview,
        int batches,
        int tiles,
        int vertices,
        int drawCalls)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _renderFrames++;
        if (mappingPreview)
            _mappingRenderFrames++;
        _renderBatches += batches;
        _renderTiles += tiles;
        _renderVertices += vertices;
        _renderDrawCalls += drawCalls;
        _renderTimestampTicks += elapsed;
        _lastRenderTimestampTicks = elapsed;
        _maxRenderTimestampTicks = Math.Max(_maxRenderTimestampTicks, elapsed);
    }

    private bool BuildNormalProjection(
        ZLevelTileProjectionFrameBudget budget,
        bool hasAuthoredRange,
        int minimumLocalZ,
        int maximumLocalZ)
    {
        for (var depth = 1; depth <= _visibility.MaxVisibleLevelDistance; depth++)
        {
            foreach (var context in _gridContexts)
            {
                var targetLocalZ = context.ViewerLocalZ - depth;
                if (hasAuthoredRange &&
                    (targetLocalZ < minimumLocalZ || targetLocalZ > maximumLocalZ))
                {
                    continue;
                }

                var targetWorldZ = _transform.LocalToWorldZLevel(context.Grid.Owner, targetLocalZ);
                if (!ProcessGridLayer(
                        context,
                        targetLocalZ,
                        targetWorldZ,
                        false,
                        true,
                        budget))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool BuildMappingPreview(
        ZLevelTileProjectionFrameBudget budget,
        bool hasAuthoredRange,
        int minimumLocalZ,
        int maximumLocalZ)
    {
        for (var offsetIndex = 0; offsetIndex < 2; offsetIndex++)
        {
            var offset = offsetIndex == 0 ? -1 : 1;
            foreach (var context in _gridContexts)
            {
                var targetLocalZ = context.ViewerLocalZ + offset;
                if (hasAuthoredRange &&
                    (targetLocalZ < minimumLocalZ || targetLocalZ > maximumLocalZ))
                {
                    continue;
                }

                var targetWorldZ = _transform.LocalToWorldZLevel(context.Grid.Owner, targetLocalZ);
                if (!ProcessGridLayer(
                        context,
                        targetLocalZ,
                        targetWorldZ,
                        true,
                        false,
                        budget))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool ProcessGridLayer(
        in ZLevelTileProjectionGridContext context,
        int targetLocalZ,
        int targetWorldZ,
        bool mappingPreview,
        bool requiresAperture,
        ZLevelTileProjectionFrameBudget budget)
    {
        var widthLong = (long) context.MaximumChunk.X - context.MinimumChunk.X + 1L;
        var heightLong = (long) context.MaximumChunk.Y - context.MinimumChunk.Y + 1L;
        if (widthLong <= 0L || heightLong <= 0L)
            return true;

        if (widthLong > int.MaxValue || heightLong > int.MaxValue)
        {
            RecordChunkExhaustion(budget);
            return false;
        }

        var width = (int) widthLong;
        var height = (int) heightLong;
        var diagonalCount = (long) width + height - 1L;
        for (long diagonal = 0; diagonal < diagonalCount; diagonal++)
        {
            var minimumYOrder = (int) Math.Max(0L, diagonal - width + 1L);
            var maximumYOrder = (int) Math.Min(height - 1L, diagonal);
            for (var yOrder = minimumYOrder; yOrder <= maximumYOrder; yOrder++)
            {
                var xOrder = (int) (diagonal - yOrder);
                var chunkIndices = new Vector2i(
                    GetCenteredCoordinate(
                        xOrder,
                        context.CenterChunk.X,
                        context.MinimumChunk.X,
                        context.MaximumChunk.X),
                    GetCenteredCoordinate(
                        yOrder,
                        context.CenterChunk.Y,
                        context.MinimumChunk.Y,
                        context.MaximumChunk.Y));

                if (!ProcessChunk(
                        context,
                        chunkIndices,
                        targetLocalZ,
                        targetWorldZ,
                        mappingPreview,
                        requiresAperture,
                        budget))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool ProcessChunk(
        in ZLevelTileProjectionGridContext context,
        Vector2i chunkIndices,
        int targetLocalZ,
        int targetWorldZ,
        bool mappingPreview,
        bool requiresAperture,
        ZLevelTileProjectionFrameBudget budget)
    {
        if (budget.RemainingChunks <= 0)
        {
            RecordChunkExhaustion(budget);
            return false;
        }

        budget.RemainingChunks--;
        budget.CurrentChunksUsed++;
        _chunkCandidates++;

        var stack = default(ZLevelApertureStack);
        if (requiresAperture)
        {
            var apertureBudget = new ZLevelApertureQueryBudget(
                budget.RemainingApertureLayers,
                budget.RemainingApertureBuilds);
            var stackResult = _lightingCache.TryComposeApertureStack(
                context.Grid,
                chunkIndices,
                targetLocalZ,
                context.ViewerLocalZ,
                ref apertureBudget,
                out stack);

            var layersUsed = budget.RemainingApertureLayers - apertureBudget.RemainingLayers;
            var buildsUsed = budget.RemainingApertureBuilds - apertureBudget.RemainingBuilds;
            budget.RemainingApertureLayers = apertureBudget.RemainingLayers;
            budget.RemainingApertureBuilds = apertureBudget.RemainingBuilds;
            budget.CurrentApertureLayersUsed += layersUsed;
            budget.CurrentApertureBuildsUsed += buildsUsed;
            _apertureLayers += layersUsed;
            _apertureBuilds += buildsUsed;

            switch (stackResult)
            {
                case ZLevelApertureStackQueryResult.LayerBudgetExceeded:
                    RecordApertureLayerExhaustion(budget);
                    return false;
                case ZLevelApertureStackQueryResult.BuildBudgetExceeded:
                    RecordApertureBuildExhaustion(budget);
                    return false;
                case ZLevelApertureStackQueryResult.Invalid:
                    _chunksCompleted++;
                    return true;
            }

            if (stack.OpenCount == 0)
            {
                _chunksCompleted++;
                return true;
            }
        }

        var origin = chunkIndices * ZLevelApertureChunk.ChunkSize;
        var chunkMinX = Math.Max(context.MinimumTile.X, origin.X);
        var chunkMaxX = Math.Min(
            context.MaximumTile.X,
            origin.X + ZLevelApertureChunk.ChunkSize - 1);
        var chunkMinY = Math.Max(context.MinimumTile.Y, origin.Y);
        var chunkMaxY = Math.Min(
            context.MaximumTile.Y,
            origin.Y + ZLevelApertureChunk.ChunkSize - 1);
        if (chunkMinX > chunkMaxX || chunkMinY > chunkMaxY)
        {
            _chunksCompleted++;
            return true;
        }

        var visits = checked((chunkMaxX - chunkMinX + 1) * (chunkMaxY - chunkMinY + 1));
        if (visits > budget.RemainingTileVisits)
        {
            RecordTileVisitExhaustion(budget);
            return false;
        }

        budget.RemainingTileVisits -= visits;
        budget.CurrentTileVisitsUsed += visits;
        _tileVisits += visits;

        var firstTile = _tiles.Count;
        var tileSize = context.Grid.Comp.TileSize;
        for (var x = chunkMinX; x <= chunkMaxX; x++)
        {
            for (var y = chunkMinY; y <= chunkMaxY; y++)
            {
                var indices = new Vector2i(x, y);
                if (requiresAperture && !stack.IsOpen(indices))
                    continue;

                var tile = _map.GetZLevelTileRef(
                    context.Grid.Owner,
                    context.Grid.Comp,
                    new ZLevelTileIndices(x, y, targetLocalZ));
                if (tile.Tile.IsEmpty)
                    continue;

                var localTile = new Box2(
                    x * tileSize,
                    y * tileSize,
                    (x + 1) * tileSize,
                    (y + 1) * tileSize);
                if (!context.GridBounds.Intersects(localTile))
                    continue;

                _tiles.Add(new ZLevelTileProjectionTile(indices, tile.Tile));
            }
        }

        var tileCount = _tiles.Count - firstTile;
        _chunksCompleted++;
        if (tileCount == 0)
            return true;

        _batches.Add(new ZLevelTileProjectionBatch(
            context.Grid.Owner,
            targetLocalZ,
            targetWorldZ,
            chunkIndices,
            mappingPreview,
            firstTile,
            tileCount));
        _chunksProjected++;
        _tilesProjected += tileCount;
        return true;
    }

    private void BuildGridContexts(Box2 worldBounds, int viewerWorldZ)
    {
        var worldCenter = worldBounds.Center;
        foreach (var grid in _grids)
        {
            if (grid.Comp.Deleted || grid.Comp.TileSize <= 0f)
                continue;

            var (_, _, worldMatrix, inverseWorldMatrix) =
                _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
            var gridBounds = inverseWorldMatrix.TransformBox(worldBounds).Enlarged(grid.Comp.TileSize * 2f);
            if (!gridBounds.IsValid())
                continue;

            var minimumTile = new Vector2i(
                (int) MathF.Floor(gridBounds.Left / grid.Comp.TileSize) - 1,
                (int) MathF.Floor(gridBounds.Bottom / grid.Comp.TileSize) - 1);
            var maximumTile = new Vector2i(
                (int) MathF.Ceiling(gridBounds.Right / grid.Comp.TileSize) + 1,
                (int) MathF.Ceiling(gridBounds.Top / grid.Comp.TileSize) + 1);
            var minimumChunk = SharedMapSystem.GetChunkIndices(
                minimumTile,
                ZLevelApertureChunk.ChunkSize);
            var maximumChunk = SharedMapSystem.GetChunkIndices(
                maximumTile,
                ZLevelApertureChunk.ChunkSize);
            var localCenter = gridBounds.Center / grid.Comp.TileSize;
            var centerTile = new Vector2i(
                (int) MathF.Floor(localCenter.X),
                (int) MathF.Floor(localCenter.Y));
            var centerChunk = SharedMapSystem.GetChunkIndices(
                centerTile,
                ZLevelApertureChunk.ChunkSize);
            centerChunk = new Vector2i(
                Math.Clamp(centerChunk.X, minimumChunk.X, maximumChunk.X),
                Math.Clamp(centerChunk.Y, minimumChunk.Y, maximumChunk.Y));

            var worldGridBounds = worldMatrix.TransformBox(grid.Comp.LocalAABB);
            var nearest = new Vector2(
                Math.Clamp(worldCenter.X, worldGridBounds.Left, worldGridBounds.Right),
                Math.Clamp(worldCenter.Y, worldGridBounds.Bottom, worldGridBounds.Top));
            var distanceSquared = Vector2.DistanceSquared(worldCenter, nearest);
            var viewerLocalZ = _transform.WorldToLocalZLevel(grid.Owner, viewerWorldZ);

            _gridContexts.Add(new ZLevelTileProjectionGridContext(
                grid,
                gridBounds,
                minimumTile,
                maximumTile,
                minimumChunk,
                maximumChunk,
                centerChunk,
                viewerLocalZ,
                distanceSquared));
        }
    }

    private void SortGridContexts()
    {
        for (var i = 1; i < _gridContexts.Count; i++)
        {
            var context = _gridContexts[i];
            var insertionIndex = i;
            while (insertionIndex > 0 &&
                   CompareGridContexts(_gridContexts[insertionIndex - 1], context) > 0)
            {
                _gridContexts[insertionIndex] = _gridContexts[insertionIndex - 1];
                insertionIndex--;
            }

            _gridContexts[insertionIndex] = context;
        }
    }

    private void SortBatchesForDrawing()
    {
        for (var i = 1; i < _batches.Count; i++)
        {
            var batch = _batches[i];
            var insertionIndex = i;
            while (insertionIndex > 0 &&
                   CompareBatches(_batches[insertionIndex - 1], batch) > 0)
            {
                _batches[insertionIndex] = _batches[insertionIndex - 1];
                insertionIndex--;
            }

            _batches[insertionIndex] = batch;
        }
    }

    private static int CompareGridContexts(
        in ZLevelTileProjectionGridContext left,
        in ZLevelTileProjectionGridContext right)
    {
        var distance = left.DistanceSquared.CompareTo(right.DistanceSquared);
        return distance != 0 ? distance : left.Grid.Owner.CompareTo(right.Grid.Owner);
    }

    private static int CompareBatches(
        in ZLevelTileProjectionBatch left,
        in ZLevelTileProjectionBatch right)
    {
        var worldZ = left.WorldZ.CompareTo(right.WorldZ);
        if (worldZ != 0)
            return worldZ;

        var grid = left.GridUid.CompareTo(right.GridUid);
        if (grid != 0)
            return grid;

        var localZ = left.LocalZ.CompareTo(right.LocalZ);
        if (localZ != 0)
            return localZ;

        var y = left.ChunkIndices.Y.CompareTo(right.ChunkIndices.Y);
        return y != 0 ? y : left.ChunkIndices.X.CompareTo(right.ChunkIndices.X);
    }

    private ZLevelTileProjectionFrameBudget EnsureFrameBudget(bool mappingPreview)
    {
        var budget = mappingPreview ? _mappingBudget : _normalBudget;
        var frame = _timing.CurFrame;
        if (budget.Initialized && budget.Frame == frame)
            return budget;

        budget.Initialized = true;
        budget.Frame = frame;
        budget.RemainingChunks = mappingPreview
            ? _mappingMaxChunksPerFrame
            : _maxChunksPerFrame;
        budget.RemainingApertureLayers = mappingPreview
            ? 0
            : _maxApertureLayersPerFrame;
        budget.RemainingApertureBuilds = mappingPreview
            ? 0
            : _maxApertureBuildsPerFrame;
        budget.RemainingTileVisits = mappingPreview
            ? _mappingMaxTileVisitsPerFrame
            : _maxTileVisitsPerFrame;
        budget.CurrentChunksUsed = 0;
        budget.CurrentApertureLayersUsed = 0;
        budget.CurrentApertureBuildsUsed = 0;
        budget.CurrentTileVisitsUsed = 0;
        budget.ChunkExhaustedThisFrame = false;
        budget.ApertureLayerExhaustedThisFrame = false;
        budget.ApertureBuildExhaustedThisFrame = false;
        budget.TileVisitExhaustedThisFrame = false;
        return budget;
    }

    private static int GetCenteredCoordinate(int order, int center, int minimum, int maximum)
    {
        if (order == 0)
            return center;

        var positiveCount = maximum - center;
        var negativeCount = center - minimum;
        var pairedCount = Math.Min(positiveCount, negativeCount);
        if (order <= pairedCount * 2)
        {
            var distance = (order + 1) / 2;
            return (order & 1) == 1
                ? center + distance
                : center - distance;
        }

        var remainder = order - pairedCount * 2 - 1;
        return positiveCount > pairedCount
            ? center + pairedCount + 1 + remainder
            : center - pairedCount - 1 - remainder;
    }

    private static void SetLimit(
        ref int field,
        int configured,
        int maximum,
        ZLevelTileProjectionFrameBudget budget)
    {
        field = Math.Clamp(configured, 0, maximum);
        budget.Initialized = false;
    }

    private static void RecordChunkExhaustion(ZLevelTileProjectionFrameBudget budget)
    {
        RecordExhaustion(ref budget.ChunkExhaustedThisFrame, ref budget.ChunkExhaustions);
    }

    private static void RecordApertureLayerExhaustion(ZLevelTileProjectionFrameBudget budget)
    {
        RecordExhaustion(
            ref budget.ApertureLayerExhaustedThisFrame,
            ref budget.ApertureLayerExhaustions);
    }

    private static void RecordApertureBuildExhaustion(ZLevelTileProjectionFrameBudget budget)
    {
        RecordExhaustion(
            ref budget.ApertureBuildExhaustedThisFrame,
            ref budget.ApertureBuildExhaustions);
    }

    private static void RecordTileVisitExhaustion(ZLevelTileProjectionFrameBudget budget)
    {
        RecordExhaustion(
            ref budget.TileVisitExhaustedThisFrame,
            ref budget.TileVisitExhaustions);
    }

    private static void RecordExhaustion(ref bool frameFlag, ref long counter)
    {
        if (frameFlag)
            return;

        frameFlag = true;
        counter++;
    }

    private void RecordBuild(long started, bool mappingPreview)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _frames++;
        if (mappingPreview)
            _mappingFrames++;
        _buildTimestampTicks += elapsed;
        _lastBuildTimestampTicks = elapsed;
        _maxBuildTimestampTicks = Math.Max(_maxBuildTimestampTicks, elapsed);
    }

    private static ZLevelTileProjectionBudgetMetrics CreateBudgetSnapshot(
        ZLevelTileProjectionFrameBudget budget,
        int maxChunks,
        int maxApertureLayers,
        int maxApertureBuilds,
        int maxTileVisits)
    {
        return new ZLevelTileProjectionBudgetMetrics(
            budget.ChunkExhaustions,
            budget.ApertureLayerExhaustions,
            budget.ApertureBuildExhaustions,
            budget.TileVisitExhaustions,
            budget.CurrentChunksUsed,
            budget.CurrentApertureLayersUsed,
            budget.CurrentApertureBuildsUsed,
            budget.CurrentTileVisitsUsed,
            maxChunks,
            maxApertureLayers,
            maxApertureBuilds,
            maxTileVisits);
    }

    private sealed class ZLevelTileProjectionFrameBudget
    {
        public bool Initialized;
        public uint Frame;
        public int RemainingChunks;
        public int RemainingApertureLayers;
        public int RemainingApertureBuilds;
        public int RemainingTileVisits;
        public int CurrentChunksUsed;
        public int CurrentApertureLayersUsed;
        public int CurrentApertureBuildsUsed;
        public int CurrentTileVisitsUsed;
        public bool ChunkExhaustedThisFrame;
        public bool ApertureLayerExhaustedThisFrame;
        public bool ApertureBuildExhaustedThisFrame;
        public bool TileVisitExhaustedThisFrame;
        public long ChunkExhaustions;
        public long ApertureLayerExhaustions;
        public long ApertureBuildExhaustions;
        public long TileVisitExhaustions;

        public void ResetMetrics()
        {
            Initialized = false;
            ChunkExhaustions = 0;
            ApertureLayerExhaustions = 0;
            ApertureBuildExhaustions = 0;
            TileVisitExhaustions = 0;
        }
    }

    private readonly record struct ZLevelTileProjectionGridContext(
        Entity<MapGridComponent> Grid,
        Box2 GridBounds,
        Vector2i MinimumTile,
        Vector2i MaximumTile,
        Vector2i MinimumChunk,
        Vector2i MaximumChunk,
        Vector2i CenterChunk,
        int ViewerLocalZ,
        float DistanceSquared);
}

public readonly record struct ZLevelTileProjectionBatch(
    EntityUid GridUid,
    int LocalZ,
    int WorldZ,
    Vector2i ChunkIndices,
    bool MappingPreview,
    int FirstTile,
    int TileCount);

public readonly record struct ZLevelTileProjectionTile(
    Vector2i Indices,
    Tile Tile);

public readonly record struct ZLevelTileProjectionBudgetMetrics(
    long ChunkExhaustions,
    long ApertureLayerExhaustions,
    long ApertureBuildExhaustions,
    long TileVisitExhaustions,
    int CurrentChunksUsed,
    int CurrentApertureLayersUsed,
    int CurrentApertureBuildsUsed,
    int CurrentTileVisitsUsed,
    int MaxChunksPerFrame,
    int MaxApertureLayersPerFrame,
    int MaxApertureBuildsPerFrame,
    int MaxTileVisitsPerFrame);

public readonly record struct ZLevelTileProjectionMetrics(
    long Frames,
    long MappingFrames,
    long GridCandidates,
    long ChunkCandidates,
    long ChunksCompleted,
    long ChunksProjected,
    long ApertureLayers,
    long ApertureBuilds,
    long TileVisits,
    long TilesProjected,
    long BuildTimestampTicks,
    long LastBuildTimestampTicks,
    long MaxBuildTimestampTicks,
    int CurrentBatches,
    int CurrentTiles,
    long RenderFrames,
    long MappingRenderFrames,
    long RenderBatches,
    long RenderTiles,
    long RenderVertices,
    long RenderDrawCalls,
    long RenderTimestampTicks,
    long LastRenderTimestampTicks,
    long MaxRenderTimestampTicks,
    ZLevelTileProjectionBudgetMetrics NormalBudget,
    ZLevelTileProjectionBudgetMetrics MappingBudget)
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
