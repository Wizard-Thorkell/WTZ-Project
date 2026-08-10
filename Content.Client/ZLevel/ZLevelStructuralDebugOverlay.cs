// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Shared.ZLevel;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map.Components;

namespace Content.Client.ZLevel;

/// <summary>
/// Draws structural stability for the currently viewed deck from opt-in server snapshots.
/// </summary>
public sealed class ZLevelStructuralDebugOverlay : Overlay
{
    private const int WhiteCap = 20;
    private const float StableFloor = 0.55f;

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private readonly ZLevelStructuralDebugSystem _debug;
    private readonly ZLevelViewContextSystem _viewContext;
    private readonly Font _font;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public ZLevelStructuralDebugOverlay()
    {
        IoCManager.InjectDependencies(this);

        _map = _entityManager.System<SharedMapSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _debug = _entityManager.System<ZLevelStructuralDebugSystem>();
        _viewContext = _entityManager.System<ZLevelViewContextSystem>();
        _font = new VectorFont(
            _resourceCache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Regular.ttf"),
            9);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_playerManager.LocalEntity is not { } player ||
            args.Viewport.Eye is not { } eye ||
            !_viewContext.TryGetViewContext(eye, player, out var view))
        {
            return;
        }

        if (args.Space == OverlaySpace.WorldSpace)
            DrawWorld(args, view.WorldZLevel);
        else if (args.Space == OverlaySpace.ScreenSpace)
            DrawText(args, view.WorldZLevel);
    }

    private void DrawWorld(in OverlayDrawArgs args, int worldZ)
    {
        var handle = args.WorldHandle;
        foreach (var (netGrid, tiles) in _debug.Grids)
        {
            if (!_entityManager.TryGetEntity(netGrid, out var gridUid) ||
                !_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid) ||
                !_entityManager.TryGetComponent<TransformComponent>(gridUid, out var transform) ||
                transform.MapID != args.MapId)
            {
                continue;
            }

            var localZ = _transform.WorldToLocalZLevel(gridUid.Value, worldZ);
            var tileSize = grid.TileSize;
            handle.SetTransform(_transform.GetWorldMatrix(gridUid.Value));
            foreach (var (indices, data) in tiles)
            {
                if (indices.Z != localZ)
                    continue;

                var bottomLeft = new Vector2(indices.X * tileSize, indices.Y * tileSize);
                var quad = Box2.FromDimensions(bottomLeft, new Vector2(tileSize, tileSize));
                handle.DrawRect(quad, GetColor(data));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    private void DrawText(in OverlayDrawArgs args, int worldZ)
    {
        if (args.ViewportControl == null)
            return;

        var handle = args.ScreenHandle;
        foreach (var (netGrid, tiles) in _debug.Grids)
        {
            if (!_entityManager.TryGetEntity(netGrid, out var gridUid) ||
                !_entityManager.TryGetComponent<MapGridComponent>(gridUid, out var grid) ||
                !_entityManager.TryGetComponent<TransformComponent>(gridUid, out var transform) ||
                transform.MapID != args.MapId)
            {
                continue;
            }

            var localZ = _transform.WorldToLocalZLevel(gridUid.Value, worldZ);
            foreach (var (indices, data) in tiles)
            {
                if (indices.Z != localZ)
                    continue;

                var world = _map.GridTileToWorldPos(gridUid.Value, grid, new Vector2i(indices.X, indices.Y));
                var screen = args.ViewportControl.WorldToScreen(world);
                var text = data.PendingCollapse ? $"{data.Stability}!" : data.Stability.ToString();
                handle.DrawString(_font, screen, text, Color.Black);
            }
        }
    }

    private static Color GetColor(ZLevelStructuralDebugTile data)
    {
        if (data.PendingCollapse)
            return Color.Red.WithAlpha(0.52f);

        if (data.Stability <= 0)
            return Color.OrangeRed.WithAlpha(0.42f);

        var normalized = Math.Clamp((data.Stability - 1f) / (WhiteCap - 1f), 0f, 1f);
        var amount = StableFloor + (1f - StableFloor) * normalized;
        return Color.InterpolateBetween(Color.Yellow, Color.White, amount).WithAlpha(0.34f);
    }
}
