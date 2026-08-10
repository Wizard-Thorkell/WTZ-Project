// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Centralizes bounded cross-floor visibility for rendering, targeting, and PVS.
/// </summary>
public sealed class SharedZLevelVisibilitySystem : EntitySystem
{
    public const int MaxVisibleLevelDistance = 4;

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public bool IsEntityVisibleFrom(
        EntityUid entity,
        MapId viewerMap,
        int viewerWorldZ,
        bool allowAbove = false)
    {
        if (!_transformQuery.TryComp(entity, out var transform) ||
            transform.MapID == MapId.Nullspace ||
            transform.MapID != viewerMap)
            return false;

        var entityLocalZ = _transform.GetZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity)));
        var entityWorldZ = _transform.GetWorldZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity)));
        if (entityWorldZ == viewerWorldZ)
            return true;

        if ((entityWorldZ > viewerWorldZ && !allowAbove) ||
            Math.Abs(entityWorldZ - viewerWorldZ) > MaxVisibleLevelDistance)
            return false;

        var mapCoordinates = _transform.GetMapCoordinates((entity, transform));
        EntityUid gridUid;
        MapGridComponent grid;
        if (transform.GridUid is { } directGrid && _gridQuery.TryComp(directGrid, out var directGridComp))
        {
            gridUid = directGrid;
            grid = directGridComp;
        }
        else if (!_mapManager.TryFindGridAt(mapCoordinates, out gridUid, out var foundGrid))
        {
            return false;
        }
        else
        {
            grid = foundGrid;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, mapCoordinates);
        return IsTileVisibleFrom(gridUid, grid, tile, viewerWorldZ, entityLocalZ, allowAbove);
    }

    public bool IsTileVisibleFrom(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int viewerWorldZ,
        int targetLocalZ,
        bool allowAbove = false)
    {
        var viewerLocalZ = _transform.WorldToLocalZLevel(gridUid, viewerWorldZ);
        var targetWorldZ = _transform.LocalToWorldZLevel(gridUid, targetLocalZ);
        if (targetWorldZ == viewerWorldZ)
            return true;

        if ((targetWorldZ > viewerWorldZ && !allowAbove) ||
            Math.Abs(targetWorldZ - viewerWorldZ) > MaxVisibleLevelDistance)
            return false;

        return _boundaries.IsStackOpen(
            gridUid,
            grid,
            tile,
            viewerLocalZ,
            targetLocalZ,
            ZLevelBoundaryChannels.Visibility);
    }
}
