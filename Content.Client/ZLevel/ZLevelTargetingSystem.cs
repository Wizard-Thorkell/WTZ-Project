// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Sprite;
using Content.Shared.Input;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.ComponentTrees;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.ZLevel;

public enum ZLevelTargetingMode : byte
{
    SameFloorOnly,
    VisibleCrossFloorExamine,
    VisibleCrossFloorAdmin,
    VisibleTopmostAny,
}

/// <summary>
/// Resolves clickable entities with shared Z-level-aware visibility rules.
/// </summary>
public sealed class ZLevelTargetingSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ClickableSystem _clickable = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<(EntityUid Entity, int Depth, uint RenderOrder, float Bottom)> _candidates = new();
    private EntityQuery<ClickableComponent> _clickableQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _clickableQuery = GetEntityQuery<ClickableComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public ZLevelTargetingMode GetTargetingModeForInput(BoundKeyFunction function)
    {
        if (function == ContentKeyFunctions.ExamineEntity)
            return ZLevelTargetingMode.VisibleCrossFloorExamine;

        if (function == ContentKeyFunctions.InspectEntity ||
            function == ContentKeyFunctions.InspectServerComponent ||
            function == ContentKeyFunctions.InspectClientComponent)
        {
            return ZLevelTargetingMode.VisibleCrossFloorAdmin;
        }

        return ZLevelTargetingMode.SameFloorOnly;
    }

    public IEnumerable<EntityUid> GetClickableEntities(
        MapCoordinates coordinates,
        IEye? eye,
        ZLevelTargetingMode mode,
        bool excludeFaded = true)
    {
        if (eye == null)
            return Array.Empty<EntityUid>();

        _candidates.Clear();

        var viewerContext = GetViewerContext();
        var entities = _spriteTree.QueryAabb(coordinates.MapId, Box2.CenteredAround(coordinates.Position, new Vector2(1, 1)));

        foreach (var entity in entities)
        {
            if (!_clickableQuery.TryComp(entity.Uid, out var clickable) ||
                !_spriteQuery.TryComp(entity.Uid, out var sprite) ||
                !_transformQuery.TryComp(entity.Uid, out var xform))
            {
                continue;
            }

            if (!_clickable.CheckClick((entity.Uid, clickable, sprite, xform, CompOrNull<FadingSpriteComponent>(entity.Uid)),
                    coordinates.Position,
                    eye,
                    excludeFaded,
                    out var drawDepth,
                    out var renderOrder,
                    out var bottom))
            {
                continue;
            }

            if (!IsEntityTargetable(entity.Uid, viewerContext, mode))
                continue;

            _candidates.Add((entity.Uid, drawDepth, renderOrder, bottom));
        }

        if (_candidates.Count == 0)
            return Array.Empty<EntityUid>();

        _candidates.Sort(ClickableComparer.Instance);
        return _candidates.Select(c => c.Entity).ToArray();
    }

    public bool IsEntityTargetable(EntityUid entity, ZLevelTargetingMode mode)
    {
        return IsEntityTargetable(entity, GetViewerContext(), mode);
    }

    public void FilterEntities(List<EntityUid> entities, ZLevelTargetingMode mode)
    {
        var viewer = GetViewerContext();
        for (var i = entities.Count - 1; i >= 0; i--)
        {
            if (!IsEntityTargetable(entities[i], viewer, mode))
                entities.RemoveSwap(i);
        }
    }

    public bool IsEntityVisibleToViewer(EntityUid entity)
    {
        return IsEntityTargetable(entity, ZLevelTargetingMode.VisibleCrossFloorExamine);
    }

    private bool IsEntityTargetable(EntityUid entity, ViewerContext viewer, ZLevelTargetingMode mode)
    {
        if (!_transformQuery.TryComp(entity, out var xform) ||
            xform.MapID != viewer.MapId)
        {
            return false;
        }

        var entityZ = _transform.GetZLevel((entity, xform, CompOrNull<ZLevelPositionComponent>(entity)));
        if (entityZ == viewer.ZLevel)
            return true;

        return mode switch
        {
            ZLevelTargetingMode.SameFloorOnly => false,
            ZLevelTargetingMode.VisibleCrossFloorExamine => entityZ < viewer.ZLevel && IsEntityVisibleAcrossOpenings(entity, viewer.ZLevel, xform),
            ZLevelTargetingMode.VisibleCrossFloorAdmin => entityZ < viewer.ZLevel && IsEntityVisibleAcrossOpenings(entity, viewer.ZLevel, xform),
            ZLevelTargetingMode.VisibleTopmostAny => IsCrossFloorVisible(entity, viewer.ZLevel, entityZ, xform),
            _ => false
        };
    }

    private bool IsCrossFloorVisible(EntityUid entity, int viewerZ, int entityZ, TransformComponent xform)
    {
        if (entityZ == viewerZ)
            return true;

        return IsEntityVisibleAcrossOpenings(entity, viewerZ, xform);
    }

    private bool IsEntityVisibleAcrossOpenings(EntityUid entity, int viewerZ, TransformComponent xform)
    {
        if (xform.MapID == MapId.Nullspace)
            return false;

        var mapCoords = _transform.GetMapCoordinates((entity, xform));
        if (!_mapManager.TryFindGridAt(mapCoords, out var gridUid, out var grid) ||
            !_gridQuery.TryComp(gridUid, out _))
        {
            return false;
        }

        var entityZ = _transform.GetZLevel((entity, xform, CompOrNull<ZLevelPositionComponent>(entity)));
        var xy = _map.TileIndicesFor(gridUid, grid, mapCoords);
        return _boundaries.IsStackOpen(
            gridUid,
            grid,
            xy,
            viewerZ,
            entityZ,
            ZLevelBoundaryChannels.Visibility);
    }

    private ViewerContext GetViewerContext()
    {
        if (_playerManager.LocalEntity is { } viewer &&
            _transformQuery.TryComp(viewer, out var xform))
        {
            return new ViewerContext(
                xform.MapID,
                _transform.GetZLevel((viewer, xform, CompOrNull<ZLevelPositionComponent>(viewer))));
        }

        return new ViewerContext(MapId.Nullspace, 0);
    }

    private readonly record struct ViewerContext(MapId MapId, int ZLevel);

    private sealed class ClickableComparer : IComparer<(EntityUid Entity, int Depth, uint RenderOrder, float Bottom)>
    {
        public static readonly ClickableComparer Instance = new();

        public int Compare(
            (EntityUid Entity, int Depth, uint RenderOrder, float Bottom) x,
            (EntityUid Entity, int Depth, uint RenderOrder, float Bottom) y)
        {
            var cmp = y.Depth.CompareTo(x.Depth);
            if (cmp != 0)
                return cmp;

            cmp = y.RenderOrder.CompareTo(x.RenderOrder);
            if (cmp != 0)
                return cmp;

            cmp = -y.Bottom.CompareTo(x.Bottom);
            if (cmp != 0)
                return cmp;

            return y.Entity.CompareTo(x.Entity);
        }
    }
}
