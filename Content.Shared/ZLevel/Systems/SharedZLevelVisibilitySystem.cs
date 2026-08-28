// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Content.Shared.CCVar;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Configuration;
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
    public const int DefaultMaxVisibleLevelDistance = 4;
    public const int MaximumVisibleLevelDistance = 32;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private int _maxVisibleLevelDistance = DefaultMaxVisibleLevelDistance;

    public int MaxVisibleLevelDistance => _maxVisibleLevelDistance;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
        Subs.CVar(
            _configuration,
            CCVars.ZLevelVisibilityMaxLevelDistance,
            value => _maxVisibleLevelDistance = Math.Clamp(value, 0, MaximumVisibleLevelDistance),
            true);
    }

    public bool IsEntityVisibleFrom(
        EntityUid entity,
        MapId viewerMap,
        int viewerWorldZ,
        bool allowAbove = false)
    {
        _metrics.RecordVisibilityEntityQuery();
        if (!_transformQuery.TryComp(entity, out var transform) ||
            transform.MapID == MapId.Nullspace ||
            transform.MapID != viewerMap)
        {
            _metrics.RecordVisibilityEarlyRejection();
            return false;
        }

        var entityLocalZ = _transform.GetZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity)));
        var entityWorldZ = _transform.GetWorldZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity)));
        if (entityWorldZ == viewerWorldZ)
        {
            _metrics.RecordVisibilitySameLevel();
            return true;
        }

        if (!IsWorldLevelWithinRange(viewerWorldZ, entityWorldZ, allowAbove))
        {
            _metrics.RecordVisibilityEarlyRejection();
            return false;
        }

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
            _metrics.RecordVisibilityEarlyRejection();
            return false;
        }
        else
        {
            grid = foundGrid;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, mapCoordinates);
        return IsTileVisibleFrom(gridUid, grid, tile, viewerWorldZ, entityLocalZ, allowAbove);
    }

    /// <summary>
    /// Checks whether a coordinate identifies a non-empty tile visible from a
    /// viewer's world level on the same structural frame.
    /// </summary>
    public bool IsCoordinateVisibleFrom(
        EntityCoordinates coordinates,
        int targetWorldZ,
        MapId viewerMap,
        int viewerWorldZ,
        bool allowAbove = false)
    {
        if (!TryResolveCoordinateTile(coordinates, viewerMap, out var gridUid, out var grid, out var tile))
            return false;

        if (!IsWorldLevelWithinRange(viewerWorldZ, targetWorldZ, allowAbove))
            return false;

        var targetLocalZ = _transform.WorldToLocalZLevel(gridUid, targetWorldZ);
        if (_map.IsZLevelTileEmpty(
                gridUid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, targetLocalZ)))
        {
            return false;
        }

        return IsTileVisibleFrom(gridUid, grid, tile, viewerWorldZ, targetLocalZ, allowAbove);
    }

    /// <summary>
    /// Resolves the nearest visible lower-floor surface under a pointer.
    /// Empty sparse layers are skipped and never become implicit targets.
    /// </summary>
    public bool TryGetNearestVisibleLowerTileWorldZ(
        EntityCoordinates coordinates,
        MapId viewerMap,
        int viewerWorldZ,
        out int targetWorldZ)
    {
        targetWorldZ = default;
        if (!TryResolveCoordinateTile(coordinates, viewerMap, out var gridUid, out var grid, out var tile))
            return false;

        var viewerLocalZ = _transform.WorldToLocalZLevel(gridUid, viewerWorldZ);
        for (var distance = 1; distance <= _maxVisibleLevelDistance; distance++)
        {
            var candidateLocalZ = viewerLocalZ - distance;
            if (_map.IsZLevelTileEmpty(
                    gridUid,
                    grid,
                    new ZLevelTileIndices(tile.X, tile.Y, candidateLocalZ)) ||
                !IsTileVisibleFrom(
                    gridUid,
                    grid,
                    tile,
                    viewerWorldZ,
                    candidateLocalZ))
            {
                continue;
            }

            targetWorldZ = _transform.LocalToWorldZLevel(gridUid, candidateLocalZ);
            return true;
        }

        return false;
    }

    public bool IsTileVisibleFrom(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int viewerWorldZ,
        int targetLocalZ,
        bool allowAbove = false)
    {
        _metrics.RecordVisibilityTileQuery();
        var viewerLocalZ = _transform.WorldToLocalZLevel(gridUid, viewerWorldZ);
        var targetWorldZ = _transform.LocalToWorldZLevel(gridUid, targetLocalZ);
        if (targetWorldZ == viewerWorldZ)
        {
            _metrics.RecordVisibilitySameLevel();
            return true;
        }

        if (!IsWorldLevelWithinRange(viewerWorldZ, targetWorldZ, allowAbove))
        {
            _metrics.RecordVisibilityEarlyRejection();
            return false;
        }

        _metrics.RecordVisibilityBoundaryCheck();
        return _boundaries.IsStackOpen(
            gridUid,
            grid,
            tile,
            viewerLocalZ,
            targetLocalZ,
            ZLevelBoundaryChannels.Visibility);
    }

    private bool TryResolveCoordinateTile(
        EntityCoordinates coordinates,
        MapId viewerMap,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i tile)
    {
        gridUid = default;
        grid = default!;
        tile = default;
        if (!coordinates.IsValid(EntityManager))
            return false;

        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        if (mapCoordinates.MapId == MapId.Nullspace ||
            mapCoordinates.MapId != viewerMap)
        {
            return false;
        }

        var coordinateGrid = _gridQuery.HasComp(coordinates.EntityId)
            ? coordinates.EntityId
            : _transform.GetGrid(coordinates);
        if (coordinateGrid is not { } resolvedGrid ||
            !_gridQuery.TryComp(resolvedGrid, out var resolvedGridComp) ||
            !_transformQuery.TryComp(resolvedGrid, out var gridTransform) ||
            gridTransform.MapID != viewerMap)
        {
            return false;
        }

        gridUid = resolvedGrid;
        grid = resolvedGridComp;
        tile = _map.TileIndicesFor(gridUid, grid, mapCoordinates);
        return true;
    }

    private bool IsWorldLevelWithinRange(int viewerWorldZ, int targetWorldZ, bool allowAbove)
    {
        var difference = (long) targetWorldZ - viewerWorldZ;
        return (allowAbove || difference <= 0) &&
               Math.Abs(difference) <= _maxVisibleLevelDistance;
    }
}
