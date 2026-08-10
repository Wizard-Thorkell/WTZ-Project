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
    private readonly SharedTransformSystem _transformSystem;
    private readonly SharedZLevelVisibilitySystem _visibilitySystem;
    private readonly ZLevelViewContextSystem _viewContextSystem;
    private readonly EntityQuery<ZLevelPositionComponent> _zLevelQuery;
    private readonly Font _font;
    private List<Entity<MapGridComponent>> _grids = new();

    public bool ShowDebugInfo { get; set; }

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowEntities | OverlaySpace.ScreenSpace;
    public override bool RequestScreenTexture => false;

    public ZLevelDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _mapSystem = _entityManager.System<SharedMapSystem>();
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
                DrawWorld(args, view.ZLevel, view.MapId);
                break;
            case OverlaySpace.ScreenSpace:
                if (ShowDebugInfo)
                    DrawScreen(args, view.Viewer ?? player, view.ZLevel);
                break;
        }
    }

    private void DrawScreen(in OverlayDrawArgs args, EntityUid player, int playerZ)
    {
        var text = $"Z-Level: {playerZ}";
        var position = new Vector2(args.ViewportBounds.Center.X - 42f, args.ViewportBounds.Top + 18f);
        args.ScreenHandle.DrawString(_font, position, text, Color.White);

        if (_zLevelQuery.TryComp(player, out var zLevel))
        {
            var detail = $"Offset: {zLevel.LocalZOffset:0.00}";
            args.ScreenHandle.DrawString(_font, position + new Vector2(0f, 14f), detail, Color.White.WithAlpha(0.8f));
        }
    }

    private void DrawWorld(in OverlayDrawArgs args, int playerZ, MapId mapId)
    {
        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, args.WorldBounds, ref _grids);

        foreach (var grid in _grids)
        {
            DrawGridTiles(args, grid, playerZ);
        }

        _grids.Clear();
    }

    private void DrawGridTiles(in OverlayDrawArgs args, Entity<MapGridComponent> grid, int playerZ)
    {
        var handle = args.WorldHandle;
        var (_, _, matrix, invMatrix) = _transformSystem.GetWorldPositionRotationMatrixWithInv(grid.Owner);
        var gridBounds = invMatrix.TransformBox(args.WorldBounds).Enlarged(grid.Comp.TileSize * 2);
        handle.SetTransform(matrix);

        var lowestZ = playerZ - SharedZLevelVisibilitySystem.MaxVisibleLevelDistance;
        for (var z = lowestZ; z <= playerZ; z++)
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

                    if (z < playerZ &&
                        !_visibilitySystem.IsTileVisibleFrom(
                            grid.Owner,
                            grid.Comp,
                            new Vector2i(x, y),
                            playerZ,
                            z))
                    {
                        continue;
                    }

                    var tileSize = grid.Comp.TileSize;
                    var localTile = new Box2(x * tileSize, y * tileSize, (x + 1) * tileSize, (y + 1) * tileSize);
                    if (!gridBounds.Intersects(localTile))
                        continue;

                    DrawTileTexture(handle, localTile, tile.Tile, GetTileColor(playerZ, z));
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

    private static Color GetTileColor(int playerZ, int z)
    {
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
