// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Numerics;
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
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IClydeTileDefinitionManager _tileDefinitionManager = default!;

    private readonly SharedMapSystem _mapSystem;
    private readonly SharedZLevelBoundarySystem _boundarySystem;
    private readonly SharedZLevelGravitySystem _gravitySystem;
    private readonly SharedZLevelMetricsSystem _metricsSystem;
    private readonly SharedZLevelTraceSystem _traceSystem;
    private readonly SharedTransformSystem _transformSystem;
    private readonly SharedZLevelVisibilitySystem _visibilitySystem;
    private readonly ZLevelViewContextSystem _viewContextSystem;
    private readonly EntityQuery<ZLevelPositionComponent> _zLevelQuery;
    private readonly Font _font;
    private List<Entity<MapGridComponent>> _grids = new();

    public bool ShowDebugInfo { get; set; }
    public bool MappingPreviewEnabled { get; set; }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.ScreenSpace;
    public override bool RequestScreenTexture => false;

    public ZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _mapSystem = _entityManager.System<SharedMapSystem>();
        _boundarySystem = _entityManager.System<SharedZLevelBoundarySystem>();
        _gravitySystem = _entityManager.System<SharedZLevelGravitySystem>();
        _metricsSystem = _entityManager.System<SharedZLevelMetricsSystem>();
        _traceSystem = _entityManager.System<SharedZLevelTraceSystem>();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _visibilitySystem = _entityManager.System<SharedZLevelVisibilitySystem>();
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
    }

    private void DrawWorld(in OverlayDrawArgs args, int playerWorldZ, MapId mapId)
    {
        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, args.WorldBounds, ref _grids);

        foreach (var grid in _grids)
        {
            DrawGridTiles(args, grid, playerWorldZ);
        }

        _grids.Clear();
    }

    private void DrawGridTiles(in OverlayDrawArgs args, Entity<MapGridComponent> grid, int playerWorldZ)
    {
        var handle = args.WorldHandle;
        var (_, _, matrix, invMatrix) = _transformSystem.GetWorldPositionRotationMatrixWithInv(grid.Owner);
        var gridBounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(grid.Comp.TileSize * 2);
        handle.SetTransform(matrix);

        var playerLocalZ = _transformSystem.WorldToLocalZLevel(grid.Owner, playerWorldZ);
        var lowestZ = MappingPreviewEnabled
            ? playerLocalZ - 1
            : playerLocalZ - _visibilitySystem.MaxVisibleLevelDistance;
        var highestZ = MappingPreviewEnabled ? playerLocalZ + 1 : playerLocalZ;
        for (var z = lowestZ; z <= highestZ; z++)
        {
            if (z == 0)
                continue;

            var minX = (int)Math.Floor(gridBounds.Left / grid.Comp.TileSize) - 1;
            var maxX = (int)Math.Ceiling(gridBounds.Right / grid.Comp.TileSize) + 1;
            var minY = (int)Math.Floor(gridBounds.Bottom / grid.Comp.TileSize) - 1;
            var maxY = (int)Math.Ceiling(gridBounds.Top / grid.Comp.TileSize) + 1;

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    var tile = _mapSystem.GetZLevelTileRef(grid.Owner, grid.Comp, new ZLevelTileIndices(x, y, z));
                    if (tile.Tile.IsEmpty)
                        continue;

                    if (!MappingPreviewEnabled &&
                        z < playerLocalZ &&
                        !_visibilitySystem.IsTileVisibleFrom(
                            grid.Owner,
                            grid.Comp,
                            new Vector2i(x, y),
                            playerWorldZ,
                            z))
                    {
                        continue;
                    }

                    var tileSize = grid.Comp.TileSize;
                    var localTile = new Box2(x * tileSize, y * tileSize, (x + 1) * tileSize, (y + 1) * tileSize);
                    if (!gridBounds.Intersects(localTile))
                        continue;

                    var tileWorldZ = _transformSystem.LocalToWorldZLevel(grid.Owner, z);
                    DrawTileTexture(handle,
                        localTile,
                        tile.Tile,
                        GetTileColor(playerWorldZ, tileWorldZ, MappingPreviewEnabled));
                }
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawTileTexture(DrawingHandleWorld handle, Box2 quad, Tile tile, Color color)
    {
        var regionMaybe = _tileDefinitionManager.TileAtlasRegion(tile);
        Box2 region;

        if (regionMaybe == null || regionMaybe.Length <= tile.Variant)
            region = _tileDefinitionManager.ErrorTileRegion;
        else
            region = regionMaybe[tile.Variant];

        var vertices = new[]
        {
            new DrawVertexUV2D(quad.BottomLeft, region.BottomLeft),
            new DrawVertexUV2D(quad.BottomRight, region.BottomRight),
            new DrawVertexUV2D(quad.TopRight, region.TopRight),
            new DrawVertexUV2D(quad.BottomLeft, region.BottomLeft),
            new DrawVertexUV2D(quad.TopRight, region.TopRight),
            new DrawVertexUV2D(quad.TopLeft, region.TopLeft),
        };
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, _tileDefinitionManager.TileTextureAtlas, vertices, color);
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
