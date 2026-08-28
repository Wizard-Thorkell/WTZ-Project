// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.Clickable;
using Content.Client.Sprite;
using Content.Shared.Input;
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
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Client.ZLevel;

public enum ZLevelTargetingMode : byte
{
    SameFloorOnly,
    VisibleCrossFloorExamine,
    VisibleCrossFloorAdmin,
    VisibleTopmostAny,
    VisibleCrossFloorRanged,
    VisibleCrossFloorInteraction,
}

/// <summary>
/// Resolves clickable entities with shared Z-level-aware visibility rules.
/// </summary>
public sealed class ZLevelTargetingSystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ClickableSystem _clickable = default!;
    [Dependency] private readonly SpriteTreeSystem _spriteTree = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ZLevelViewContextSystem _viewContext = default!;

    private readonly List<(EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom)> _candidates = new();
    private EntityQuery<ClickableComponent> _clickableQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _clickableQuery = GetEntityQuery<ClickableComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public ZLevelTargetingMode GetTargetingModeForInput(BoundKeyFunction function)
    {
        if (function == ContentKeyFunctions.ExamineEntity)
            return ZLevelTargetingMode.VisibleCrossFloorExamine;

        if (function == EngineKeyFunctions.Use ||
            function == ContentKeyFunctions.ActivateItemInWorld ||
            function == ContentKeyFunctions.AltActivateItemInWorld)
        {
            return ZLevelTargetingMode.VisibleCrossFloorInteraction;
        }

        if (function == EngineKeyFunctions.UseSecondary)
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

        var viewerContext = GetViewerContext(eye);
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

            var entityZ = _transform.GetWorldZLevel((
                entity.Uid,
                xform,
                CompOrNull<ZLevelPositionComponent>(entity.Uid)));
            var floorDistance = mode == ZLevelTargetingMode.VisibleCrossFloorInteraction
                ? Math.Abs(viewerContext.WorldZLevel - entityZ)
                : 0;
            _candidates.Add((entity.Uid, floorDistance, drawDepth, renderOrder, bottom));
        }

        if (_candidates.Count == 0)
            return Array.Empty<EntityUid>();

        _candidates.Sort(mode == ZLevelTargetingMode.VisibleCrossFloorInteraction
            ? InteractionClickableComparer.Instance
            : ClickableComparer.Instance);
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

    /// <summary>
    /// Resolves the world layer selected by a pointer target. Entity targets own
    /// their layer; coordinate-only targets stay on the active view layer.
    /// </summary>
    public int GetPointerWorldZ(IEye? eye, EntityUid? target)
    {
        if (target is { } targetUid && _transformQuery.TryComp(targetUid, out var targetTransform))
        {
            return _transform.GetWorldZLevel((
                targetUid,
                targetTransform,
                CompOrNull<ZLevelPositionComponent>(targetUid)));
        }

        return GetViewerContext(eye).WorldZLevel;
    }

    public bool TryGetNearestVisibleLowerTileWorldZ(
        EntityCoordinates coordinates,
        out int targetWorldZ)
    {
        var viewer = GetViewerContext();
        return _visibility.TryGetNearestVisibleLowerTileWorldZ(
            coordinates,
            viewer.MapId,
            viewer.WorldZLevel,
            out targetWorldZ);
    }

    /// <summary>
    /// Selects the structural frame that should own planar pointer coordinates.
    /// This avoids an arbitrary overlapping grid being selected by a 2D lookup.
    /// </summary>
    public bool TryGetPointerFrame(IEye? eye, EntityUid? target, out EntityUid frameUid)
    {
        if (target is { } targetUid &&
            _transformQuery.TryComp(targetUid, out var targetTransform) &&
            targetTransform.GridUid is { } targetGrid)
        {
            frameUid = targetGrid;
            return true;
        }

        var viewer = GetViewerContext(eye).Viewer;
        if (viewer is { } viewerUid &&
            _transformQuery.TryComp(viewerUid, out var viewerTransform) &&
            viewerTransform.GridUid is { } viewerGrid)
        {
            frameUid = viewerGrid;
            return true;
        }

        frameUid = default;
        return false;
    }

    private bool IsEntityTargetable(EntityUid entity, ZLevelViewContext viewer, ZLevelTargetingMode mode)
    {
        if (!_transformQuery.TryComp(entity, out var xform) ||
            xform.MapID != viewer.MapId)
        {
            return false;
        }

        var entityZ = _transform.GetWorldZLevel((entity, xform, CompOrNull<ZLevelPositionComponent>(entity)));
        if (entityZ == viewer.WorldZLevel)
            return true;

        return mode switch
        {
            ZLevelTargetingMode.SameFloorOnly => false,
            ZLevelTargetingMode.VisibleCrossFloorExamine => entityZ < viewer.WorldZLevel &&
                _visibility.IsEntityVisibleFrom(entity, viewer.MapId, viewer.WorldZLevel),
            ZLevelTargetingMode.VisibleCrossFloorRanged => entityZ < viewer.WorldZLevel &&
                _visibility.IsEntityVisibleFrom(entity, viewer.MapId, viewer.WorldZLevel),
            ZLevelTargetingMode.VisibleCrossFloorInteraction => entityZ < viewer.WorldZLevel &&
                _visibility.IsEntityVisibleFrom(entity, viewer.MapId, viewer.WorldZLevel),
            ZLevelTargetingMode.VisibleCrossFloorAdmin => entityZ < viewer.WorldZLevel &&
                _visibility.IsEntityVisibleFrom(entity, viewer.MapId, viewer.WorldZLevel),
            ZLevelTargetingMode.VisibleTopmostAny =>
                _visibility.IsEntityVisibleFrom(entity, viewer.MapId, viewer.WorldZLevel, allowAbove: true),
            _ => false
        };
    }

    private ZLevelViewContext GetViewerContext(IEye? eye = null)
    {
        eye ??= _eyeManager.CurrentEye;
        if (_viewContext.TryGetViewContext(eye, _playerManager.LocalEntity, out var context))
            return context;

        return new ZLevelViewContext(null, MapId.Nullspace, 0, 0);
    }

    private sealed class ClickableComparer : IComparer<(EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom)>
    {
        public static readonly ClickableComparer Instance = new();

        public int Compare(
            (EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom) x,
            (EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom) y)
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

    internal sealed class InteractionClickableComparer : IComparer<(EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom)>
    {
        public static readonly InteractionClickableComparer Instance = new();

        public int Compare(
            (EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom) x,
            (EntityUid Entity, int FloorDistance, int Depth, uint RenderOrder, float Bottom) y)
        {
            var floor = x.FloorDistance.CompareTo(y.FloorDistance);
            return floor != 0 ? floor : ClickableComparer.Instance.Compare(x, y);
        }
    }
}
