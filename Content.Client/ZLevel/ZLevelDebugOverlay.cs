// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Map;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Client.ZLevel;

/// <summary>
/// Draws sparse Z-level tiles with bounded opening-aware depth and optional diagnostics.
/// </summary>
public sealed class ZLevelDebugOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IClydeTileDefinitionManager _tileDefinitionManager = default!;

    private readonly SharedZLevelBoundarySystem _boundarySystem;
    private readonly SharedZLevelGravitySystem _gravitySystem;
    private readonly SharedZLevelMetricsSystem _metricsSystem;
    private readonly SharedZLevelTraceSystem _traceSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly SharedZLevelVisibilitySystem _visibilitySystem;
    private readonly ZLevelLightingCacheSystem _lightingCacheSystem;
    private readonly ZLevelLightingProjectionSystem _lightingProjectionSystem;
    private readonly ZLevelTileProjectionSystem _tileProjectionSystem;
    private readonly ZLevelViewContextSystem _viewContextSystem;
    private readonly EntityQuery<ZLevelPositionComponent> _zLevelQuery;
    private readonly Font _font;
    private readonly List<DrawVertexUV2D> _tileVertices = new();

    public bool ShowDebugInfo { get; set; }
    public bool MappingPreviewEnabled { get; set; }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.ScreenSpace;
    public override bool RequestScreenTexture => false;

    public ZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _boundarySystem = _entityManager.System<SharedZLevelBoundarySystem>();
        _gravitySystem = _entityManager.System<SharedZLevelGravitySystem>();
        _metricsSystem = _entityManager.System<SharedZLevelMetricsSystem>();
        _traceSystem = _entityManager.System<SharedZLevelTraceSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _visibilitySystem = _entityManager.System<SharedZLevelVisibilitySystem>();
        _lightingCacheSystem = _entityManager.System<ZLevelLightingCacheSystem>();
        _lightingProjectionSystem = _entityManager.System<ZLevelLightingProjectionSystem>();
        _tileProjectionSystem = _entityManager.System<ZLevelTileProjectionSystem>();
        _viewContextSystem = _entityManager.System<ZLevelViewContextSystem>();
        _zLevelQuery = _entityManager.GetEntityQuery<ZLevelPositionComponent>();

        var font = _resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf");
        _font = new VectorFont(font, 11);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } player ||
            args.Viewport.Eye is not { } eye ||
            !_viewContextSystem.TryGetViewContext(eye, player, out var view))
        {
            return;
        }

        switch (args.Space)
        {
            case OverlaySpace.WorldSpaceBelowEntities:
                DrawWorld(args, view.WorldZLevel, view.MapId);
                break;
            case OverlaySpace.ScreenSpace:
                if (ShowDebugInfo)
                    DrawScreen(args, view.Viewer ?? player, view.LocalZLevel, view.WorldZLevel);
                break;
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, EntityUid player, int localZ, int worldZ)
    {
        var text = localZ == worldZ
            ? $"Z-Level: {localZ}"
            : $"Z-Level: {localZ} (world {worldZ})";
        var position = new Vector2(args.ViewportBounds.Center.X - 42f, args.ViewportBounds.Top + 18f);
        args.ScreenHandle.DrawString(_font, position, text, Color.White);

        if (_zLevelQuery.TryComp(player, out var zLevel))
        {
            var detail = $"Offset: {zLevel.LocalZOffset:0.00}";
            args.ScreenHandle.DrawString(_font, position + new Vector2(0f, 14f), detail, Color.White.WithAlpha(0.8f));
        }

        var metrics = _metricsSystem.Snapshot();
        var metricsPosition = new Vector2(args.ViewportBounds.Left + 12f, args.ViewportBounds.Top + 18f);
        var metricsColor = Color.White.WithAlpha(0.82f);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition,
            $"Z metrics (local)  boundary q:{metrics.BoundaryQueries} hit:{metrics.BoundaryCacheHitPercent:0.0}% " +
            $"cache:{_boundarySystem.CachedBoundaryCount}/{_boundarySystem.BoundaryCacheCapacity}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 14f),
            $"visibility entity:{metrics.VisibilityEntityQueries} tile:{metrics.VisibilityTileQueries} " +
            $"cross:{metrics.VisibilityBoundaryChecks} reject:{metrics.VisibilityEarlyRejections} " +
            $"depth:{_visibilitySystem.MaxVisibleLevelDistance}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 28f),
            $"gravity q:{metrics.GravityQueries} hit:{metrics.GravityCacheHitPercent:0.0}% " +
            $"grids:{_gravitySystem.CachedGridCount} pending:{_gravitySystem.PendingRefreshGridCount}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 42f),
            $"gravity build count:{metrics.GravityBuilds} " +
            $"avg/max:{metrics.GravityAverageBuildMilliseconds:0.000}/{metrics.GravityMaxBuildMilliseconds:0.000}ms",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 56f),
            $"trace q:{metrics.TraceQueries} ok:{metrics.TraceCompleted} " +
            $"closed:{metrics.TraceClosedBoundaries} budget:{metrics.TraceBudgetExhaustions} " +
            $"avg/max:{metrics.TraceAverageMilliseconds:0.000}/{metrics.TraceMaxMilliseconds:0.000}ms",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 70f),
            $"trace out segments:{metrics.TraceSegments} tiles:{metrics.TraceTileVisits} " +
            $"hits:{metrics.TraceEntityHits} crossings:{metrics.TraceBoundaryCrossings}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 84f),
            $"interact q:{metrics.InteractionQueries} allow:{metrics.InteractionAllowed} " +
            $"vertical:{metrics.InteractionVerticalAllowed} reject:{metrics.InteractionRejected} " +
            $"remote:{metrics.InteractionRemoteOriginQueries}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 98f),
            $"ballistic try:{metrics.BallisticRouteAttempts} start:{metrics.BallisticRoutesStarted} " +
            $"done:{metrics.BallisticRoutesCompleted} reject:{metrics.BallisticRoutesRejected}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 112f),
            $"ballistic cross:{metrics.BallisticCrossings} closed:{metrics.BallisticClosedBoundaries} " +
            $"collide:{metrics.BallisticCollisionCancellations} invalid:{metrics.BallisticInvalidCancellations} " +
            $"flush:{metrics.BallisticContactFlushes}",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 126f),
            $"trace budget crossings:{_traceSystem.MaxVerticalCrossings} " +
            $"tiles:{_traceSystem.MaxTileVisits} hits:{_traceSystem.MaxEntityHits}",
            metricsColor);

        var render = _clyde.ZLevelRenderStats;
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 140f),
            $"render layers:{render.GridLayersDrawn} chunks:{render.GridChunksDrawn} " +
            $"cache layers:{render.CachedGridChunkLayers} hit:{render.GridChunkCacheHitPercent:0.0}% " +
            $"z-reject light:{render.LightsRejectedByZ} occ:{render.OccludersRejectedByZ}",
            metricsColor);

        var lighting = _lightingCacheSystem.Snapshot();
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 154f),
            $"vertical light aperture chunks:{lighting.CachedApertureChunks}/{lighting.ApertureCacheCapacity} " +
            $"evict:{lighting.ApertureEvictions} hit:{lighting.ApertureCacheHitPercent:0.0}% " +
            $"emit:{lighting.EmitterAccepted}/{lighting.EmitterCandidates}",
            metricsColor);

        var projection = _lightingProjectionSystem.Snapshot();
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 168f),
            $"vertical light project batch/run:{projection.CurrentBatches}/{projection.CurrentRuns} " +
            $"build avg/max:{projection.AverageBuildMilliseconds:0.000}/{projection.MaxBuildMilliseconds:0.000}ms " +
            $"draw avg/max:{projection.AverageRenderMilliseconds:0.000}/{projection.MaxRenderMilliseconds:0.000}ms",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 182f),
            $"vertical light budget c/e/l/b/r:" +
            $"{projection.CurrentEmitterCandidatesUsed}/{projection.CurrentEmittersUsed}/" +
            $"{projection.CurrentApertureLayersUsed}/{projection.CurrentApertureBuildsUsed}/" +
            $"{projection.CurrentRunsUsed} exhausted:" +
            $"{projection.CandidateBudgetExhaustions}/{projection.EmitterBudgetExhaustions}/" +
            $"{projection.ApertureLayerBudgetExhaustions}/{projection.ApertureBuildBudgetExhaustions}/" +
            $"{projection.RunBudgetExhaustions}",
            metricsColor);

        var tileProjection = _tileProjectionSystem.Snapshot();
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 196f),
            $"vertical tiles batch/tile:{tileProjection.CurrentBatches}/{tileProjection.CurrentTiles} " +
            $"chunk:{tileProjection.ChunkCandidates}/{tileProjection.ChunksProjected} " +
            $"build avg/max:{tileProjection.AverageBuildMilliseconds:0.000}/" +
            $"{tileProjection.MaxBuildMilliseconds:0.000}ms",
            metricsColor);
        args.ScreenHandle.DrawString(
            _font,
            metricsPosition + new Vector2(0f, 210f),
            $"vertical tile budget chunk/layer/build/tile:" +
            $"{tileProjection.NormalBudget.CurrentChunksUsed}/" +
            $"{tileProjection.NormalBudget.CurrentApertureLayersUsed}/" +
            $"{tileProjection.NormalBudget.CurrentApertureBuildsUsed}/" +
            $"{tileProjection.NormalBudget.CurrentTileVisitsUsed} preview:" +
            $"{tileProjection.MappingBudget.CurrentChunksUsed}/" +
            $"{tileProjection.MappingBudget.CurrentTileVisitsUsed}",
            metricsColor);
    }

    private void DrawWorld(in OverlayDrawArgs args, int playerWorldZ, MapId mapId)
    {
        var started = Stopwatch.GetTimestamp();
        var batchesDrawn = 0;
        var tilesDrawn = 0;
        var verticesDrawn = 0;
        var drawCalls = 0;
        var handle = args.WorldHandle;
        _tileProjectionSystem.BuildProjection(
            mapId,
            args.WorldAABB,
            playerWorldZ,
            MappingPreviewEnabled);

        foreach (var batch in _tileProjectionSystem.Batches)
        {
            if (!_entityManager.TryGetComponent(batch.GridUid, out MapGridComponent? grid) || grid.Deleted)
                continue;

            _tileVertices.Clear();
            for (var i = 0; i < batch.TileCount; i++)
            {
                var projectedTile = _tileProjectionSystem.Tiles[batch.FirstTile + i];
                var regionMaybe = _tileDefinitionManager.TileAtlasRegion(projectedTile.Tile);
                var region = regionMaybe == null || regionMaybe.Length <= projectedTile.Tile.Variant
                    ? _tileDefinitionManager.ErrorTileRegion
                    : regionMaybe[projectedTile.Tile.Variant];
                ZLevelTileProjectionGeometry.AppendTileVertices(
                    _tileVertices,
                    projectedTile.Indices,
                    grid.TileSize,
                    region);
            }

            if (_tileVertices.Count == 0)
                continue;

            handle.SetTransform(_transformSystem.GetWorldMatrix(batch.GridUid));
            handle.DrawPrimitives(
                DrawPrimitiveTopology.TriangleList,
                _tileDefinitionManager.TileTextureAtlas,
                CollectionsMarshal.AsSpan(_tileVertices),
                GetTileColor(playerWorldZ, batch.WorldZ, batch.MappingPreview));
            batchesDrawn++;
            tilesDrawn += batch.TileCount;
            verticesDrawn += _tileVertices.Count;
            drawCalls++;
        }

        handle.SetTransform(Matrix3x2.Identity);
        _tileProjectionSystem.RecordRender(
            started,
            MappingPreviewEnabled,
            batchesDrawn,
            tilesDrawn,
            verticesDrawn,
            drawCalls);
    }

    private static Color GetTileColor(int playerZ, int z, bool mappingPreview)
    {
        if (mappingPreview)
        {
            return z switch
            {
                _ when z == playerZ => Color.White.WithAlpha(0.78f),
                _ when z < playerZ => new Color(0.68f, 0.82f, 1f, 0.38f),
                _ => new Color(1f, 0.78f, 0.58f, 0.26f),
            };
        }

        if (z == playerZ)
            return Color.White;

        var depth = playerZ - z;
        if (depth <= 0)
            return Color.White;

        var alpha = MathF.Max(0.16f, 1f - depth * 0.2f);
        var tint = MathF.Max(0.72f, 1f - depth * 0.08f);
        return new Color(tint, tint, tint + 0.08f, alpha);
    }
}

internal static class ZLevelTileProjectionGeometry
{
    public static int AppendTileVertices(
        List<DrawVertexUV2D> vertices,
        Vector2i indices,
        float tileSize,
        Box2 region)
    {
        if (tileSize <= 0f)
            return 0;

        var quad = new Box2(
            indices.X * tileSize,
            indices.Y * tileSize,
            (indices.X + 1) * tileSize,
            (indices.Y + 1) * tileSize);
        vertices.Add(new DrawVertexUV2D(quad.BottomLeft, region.BottomLeft));
        vertices.Add(new DrawVertexUV2D(quad.BottomRight, region.BottomRight));
        vertices.Add(new DrawVertexUV2D(quad.TopRight, region.TopRight));
        vertices.Add(new DrawVertexUV2D(quad.BottomLeft, region.BottomLeft));
        vertices.Add(new DrawVertexUV2D(quad.TopRight, region.TopRight));
        vertices.Add(new DrawVertexUV2D(quad.TopLeft, region.TopLeft));
        return 6;
    }
}
