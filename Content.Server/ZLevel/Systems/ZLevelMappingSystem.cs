// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Mapping;
using Content.Shared.Administration;
using Content.Shared.Decals;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Applies authenticated Z-level map editing operations requested by the
/// mapping UI.
/// </summary>
public sealed class ZLevelMappingSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly MappingSnapshotSystem _snapshots = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMapSystem _format = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ZLevelMappingRequestEvent>(OnRequest);
    }

    private void OnRequest(ZLevelMappingRequestEvent request, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_admin.IsAdmin(session, true) || !_admin.HasAdminFlag(session, AdminFlags.Mapping))
            return;

        var mapUid = GetEntity(request.Map);
        if (!TryComp<MapComponent>(mapUid, out var mapComponent) ||
            session.AttachedEntity is not { } player ||
            !TryComp(player, out TransformComponent? playerTransform) ||
            playerTransform.MapUid != mapUid)
        {
            Reply(session, "The selected map is no longer available.", true);
            return;
        }

        try
        {
            if (request.Operation == ZLevelMappingOperation.ConfigureMap)
            {
                if (LifeStage(mapUid) >= EntityLifeStage.MapInitialized &&
                    TryComp<ZLevelMapComponent>(mapUid, out var currentConfig) &&
                    (request.MinimumLevel > currentConfig.MinimumLevel ||
                     request.MaximumLevel < currentConfig.MaximumLevel))
                {
                    Reply(session,
                        "Remove initialized edge floors with the Z-level delete operation before contracting the map range.",
                        true);
                    return;
                }

                _format.Configure(mapUid,
                    request.MinimumLevel,
                    request.MaximumLevel,
                    request.DefaultLevel,
                    request.BoundaryMode);
                Reply(session, $"Configured Z-level map format v{ZLevelMapComponent.CurrentFormatVersion}.");
                return;
            }

            if (request.Operation == ZLevelMappingOperation.SetActiveLevel)
            {
                if (!TryComp<ZLevelMapComponent>(mapUid, out var config) ||
                    request.TargetLevel < config.MinimumLevel ||
                    request.TargetLevel > config.MaximumLevel)
                {
                    return;
                }

                _zLevel.SetZLevelPosition(player, request.TargetLevel);
                return;
            }

            var gridUid = GetEntity(request.Grid);
            if (!TryComp<MapGridComponent>(gridUid, out var grid) ||
                !TryComp(gridUid, out TransformComponent? gridTransform) ||
                gridTransform.MapID != mapComponent.MapId)
            {
                Reply(session, "Center the mapping viewport over the grid you want to edit.", true);
                return;
            }

            if (!TryComp<ZLevelMapComponent>(mapUid, out var mapConfig))
            {
                Reply(session, "Initialize the map as a Z-level map first.", true);
                return;
            }

            switch (request.Operation)
            {
                case ZLevelMappingOperation.CreateLevel:
                    IncludeLevel(mapUid, mapConfig, request.TargetLevel);
                    _zLevel.SetZLevelPosition(player, request.TargetLevel);
                    Reply(session, $"Created empty Z level {request.TargetLevel}.");
                    break;
                case ZLevelMappingOperation.CopyLevel:
                    var copiedEntities = CopyLevel(mapUid,
                        mapComponent,
                        mapConfig,
                        (gridUid, grid),
                        request.SourceLevel,
                        request.TargetLevel);
                    _zLevel.SetZLevelPosition(player, request.TargetLevel);
                    Reply(session,
                        $"Copied Z level {request.SourceLevel} to {request.TargetLevel} with {copiedEntities} entities.");
                    break;
                case ZLevelMappingOperation.DeleteLevel:
                    var deletedEntities = DeleteLevel(mapUid, mapConfig, (gridUid, grid), request.TargetLevel);
                    _zLevel.SetZLevelPosition(player, mapConfig.DefaultLevel);
                    Reply(session, $"Removed Z level {request.TargetLevel} and {deletedEntities} entities.");
                    break;
            }
        }
        catch (Exception exception)
        {
            Log.Error($"Z-level mapping operation {request.Operation} failed: {exception}");
            Reply(session, exception.Message, true);
        }
    }

    public void IncludeLevel(EntityUid mapUid, ZLevelMapComponent config, int level)
    {
        var minimum = Math.Min(config.MinimumLevel, level);
        var maximum = Math.Max(config.MaximumLevel, level);
        _format.Configure(mapUid, minimum, maximum, config.DefaultLevel, config.DefaultBoundaryMode);
    }

    public int CopyLevel(
        EntityUid mapUid,
        MapComponent mapComponent,
        ZLevelMapComponent config,
        Entity<MapGridComponent> grid,
        int sourceLevel,
        int targetLevel)
    {
        if (sourceLevel == targetLevel)
            throw new ArgumentException("Source and target Z levels must differ.");

        if (sourceLevel < config.MinimumLevel || sourceLevel > config.MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(sourceLevel), "Source Z level is outside the map range.");

        if (!_format.TryValidate(mapUid, out var validationError, uid => IsAuthoredEntity(uid, mapUid)))
            throw new InvalidOperationException(validationError);

        var sourceRoots = GetLevelRoots(mapUid, grid.Owner, sourceLevel);
        var targetRoots = GetLevelRoots(mapUid, grid.Owner, targetLevel);
        IReadOnlyDictionary<EntityUid, int>? yamlIds = null;
        LoadResult? result = null;

        if (sourceRoots.Count > 0)
        {
            var serialization = SerializationOptions.Default with
            {
                Category = FileCategory.Unknown,
                ExpectPreInit = LifeStage(mapUid) < EntityLifeStage.MapInitialized,
                MissingEntityBehaviour = MissingEntityBehaviour.Error,
                EntityFilter = entity => IsAuthoredEntity(entity.Owner, mapUid),
                ComponentFilter = (_, component) => _snapshots.IsPersistentSnapshotComponent(component),
                SuppressMapSerializationEvents = true,
            };
            var (node, _) = _mapLoader.SerializeEntitiesRecursive(sourceRoots, out yamlIds, serialization);
            var loadOptions = MapLoadOptions.Default;
            loadOptions.MergeMap = mapComponent.MapId;
            loadOptions.DeserializationOptions.StoreYamlUids = true;
            loadOptions.DeserializationOptions.InitializeMaps = false;

            if (!_mapLoader.TryLoadGeneric(node, "Z-level floor copy", out result, loadOptions))
                throw new InvalidOperationException("Failed to deserialize the copied Z level.");
        }

        var sourceTiles = _map.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp)
            .Where(tile => tile.GridIndices.Z == sourceLevel)
            .ToArray();
        var targetTiles = _map.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp)
            .Where(tile => tile.GridIndices.Z == targetLevel)
            .Select(tile => tile.GridIndices)
            .ToArray();
        var sourceDecals = GetLevelDecals(grid, sourceLevel);
        var targetDecals = GetLevelDecals(grid, targetLevel);
        var allTiles = _map.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp).ToArray();
        var wouldEmptyGrid = sourceTiles.Length == 0 && targetTiles.Length == allTiles.Length;

        if (wouldEmptyGrid &&
            (sourceRoots.Count > 0 ||
             HasRemainingDecals(grid.Owner, targetDecals.Length) ||
             HasSurvivingGridEntities(mapUid, grid.Owner, targetRoots)))
        {
            if (result != null)
                _mapLoader.Delete(result);

            throw new InvalidOperationException(
                "The floor copy would delete the grid while entities or authored decals still depend on it.");
        }

        var cloneRoots = new List<(EntityUid Source, EntityUid Clone)>(sourceRoots.Count);
        var loadedByYamlId = new Dictionary<int, EntityUid>();
        if (sourceRoots.Count > 0)
        {
            foreach (var loaded in result!.Entities)
            {
                if (TryComp<YamlUidComponent>(loaded, out var yamlUid))
                    loadedByYamlId[yamlUid.Uid] = loaded;
            }

            foreach (var source in sourceRoots)
            {
                if (!yamlIds!.TryGetValue(source, out var yamlId) ||
                    !loadedByYamlId.TryGetValue(yamlId, out var clone) ||
                    !HasComp<TransformComponent>(source) ||
                    !HasComp<TransformComponent>(clone))
                {
                    _mapLoader.Delete(result);
                    throw new InvalidOperationException($"The copied Z level did not produce a complete clone for {ToPrettyString(source)}.");
                }

                cloneRoots.Add((source, clone));
            }
        }

        DetachExcludedDescendants(mapUid, grid.Owner, targetRoots, targetLevel);

        foreach (var (index, _) in targetDecals)
            _decals.RemoveDecal(grid.Owner, index);

        foreach (var entity in targetRoots)
            QueueDel(entity);

        foreach (var tile in sourceTiles)
        {
            var target = new ZLevelTileIndices(tile.GridIndices.X, tile.GridIndices.Y, targetLevel);
            _map.SetZLevelTile(grid.Owner, grid.Comp, target, tile.Tile);
        }

        var copiedCoordinates = sourceTiles
            .Select(tile => (tile.GridIndices.X, tile.GridIndices.Y))
            .ToHashSet();
        foreach (var tile in targetTiles)
        {
            if (!copiedCoordinates.Contains((tile.X, tile.Y)))
                _map.SetZLevelTile(grid.Owner, grid.Comp, tile, Tile.Empty);
        }

        foreach (var (_, decal) in sourceDecals)
        {
            if (!_decals.TryAddDecal(
                    decal.WithZLevel(targetLevel),
                    new EntityCoordinates(grid.Owner, decal.Coordinates),
                    out _))
            {
                throw new InvalidOperationException($"Failed to copy decal '{decal.Id}' to Z level {targetLevel}.");
            }
        }

        _atmosphere.CopyZLevelAtmosphere(grid.Owner, sourceLevel, targetLevel);
        IncludeLevel(mapUid, config, targetLevel);

        if (sourceRoots.Count == 0)
            return 0;

        var levelOffset = targetLevel - sourceLevel;
        foreach (var (source, yamlId) in yamlIds!)
        {
            if (!loadedByYamlId.TryGetValue(yamlId, out var clone))
                continue;

            if (TryComp<ZLevelPositionComponent>(source, out var sourceZ))
                _zLevel.SetZLevelPosition(clone, sourceZ.ZLevel + levelOffset);

            RemCompDeferred<YamlUidComponent>(clone);
        }

        foreach (var (source, clone) in cloneRoots)
        {
            if (!TryComp(source, out TransformComponent? sourceTransform) ||
                !TryComp(clone, out TransformComponent? cloneTransform))
            {
                continue;
            }

            _transform.SetCoordinates(clone,
                cloneTransform,
                new EntityCoordinates(grid.Owner, sourceTransform.LocalPosition),
                sourceTransform.LocalRotation);
            _zLevel.SetZLevelPosition(clone, targetLevel);

            if (sourceTransform.Anchored)
                _transform.AnchorEntity((clone, cloneTransform), grid);
        }

        return sourceRoots.Count;
    }

    public int DeleteLevel(
        EntityUid mapUid,
        ZLevelMapComponent config,
        Entity<MapGridComponent> grid,
        int level)
    {
        if (level < config.MinimumLevel || level > config.MaximumLevel)
            throw new ArgumentOutOfRangeException(nameof(level), "Z level is outside the map range.");

        if (!_format.TryValidate(mapUid, out var validationError, uid => IsAuthoredEntity(uid, mapUid)))
            throw new InvalidOperationException(validationError);

        var entities = GetLevelRoots(mapUid, grid.Owner, level);
        var usedByAnotherGrid = IsLevelUsedByAnotherGrid(mapUid, grid.Owner, level);
        var allTiles = _map.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp).ToArray();
        var tiles = allTiles
            .Where(tile => tile.GridIndices.Z == level)
            .Select(tile => tile.GridIndices)
            .ToArray();
        var decals = GetLevelDecals(grid, level);
        var wouldEmptyGrid = tiles.Length == allTiles.Length;
        if (wouldEmptyGrid &&
            (HasRemainingDecals(grid.Owner, decals.Length) ||
             HasSurvivingGridEntities(mapUid, grid.Owner, entities)))
        {
            throw new InvalidOperationException(
                "The floor deletion would delete the grid while runtime or other-floor entities still depend on it.");
        }

        DetachExcludedDescendants(mapUid, grid.Owner, entities, level);

        foreach (var (index, _) in decals)
            _decals.RemoveDecal(grid.Owner, index);

        _atmosphere.ClearZLevelAtmosphere(grid.Owner, level);

        foreach (var entity in entities)
            QueueDel(entity);

        var minimum = config.MinimumLevel;
        var maximum = config.MaximumLevel;
        if (!usedByAnotherGrid && minimum != maximum && level == minimum)
            minimum++;
        else if (!usedByAnotherGrid && minimum != maximum && level == maximum)
            maximum--;

        var defaultLevel = Math.Clamp(config.DefaultLevel, minimum, maximum);
        RelocateRuntimeLevelRoots(mapUid, grid.Owner, level, defaultLevel);

        foreach (var tile in tiles)
            _map.SetZLevelTile(grid.Owner, grid.Comp, tile, Tile.Empty);

        _format.Configure(mapUid, minimum, maximum, defaultLevel, config.DefaultBoundaryMode);
        return entities.Count;
    }

    private HashSet<EntityUid> GetLevelRoots(EntityUid mapUid, EntityUid gridUid, int level)
    {
        var entities = new HashSet<EntityUid>();
        foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
        {
            var transform = (TransformComponent) component;
            if (transform.ParentUid != gridUid ||
                _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid))) != level ||
                !IsAuthoredEntity(uid, mapUid))
            {
                continue;
            }

            entities.Add(uid);
        }

        return entities;
    }

    private bool IsAuthoredEntity(EntityUid uid, EntityUid mapUid)
    {
        return MetaData(uid).EntityPrototype?.MapSavable != false &&
               _snapshots.IsPersistentSnapshotEntity(uid, mapUid);
    }

    private bool IsLevelUsedByAnotherGrid(EntityUid mapUid, EntityUid editedGridUid, int level)
    {
        if (!TryComp<MapComponent>(mapUid, out var mapComponent))
            return false;

        foreach (var grid in _mapManager.GetAllGrids(mapComponent.MapId))
        {
            if (grid.Owner == editedGridUid)
                continue;

            if (_map.GetExistingZLevelLayers(grid.Owner, grid.Comp).Contains(level))
                return true;

            foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
            {
                var transform = (TransformComponent) component;
                if (transform.ParentUid == grid.Owner &&
                    _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid))) == level)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasSurvivingGridEntities(
        EntityUid mapUid,
        EntityUid gridUid,
        HashSet<EntityUid> removedRoots)
    {
        foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
        {
            var transform = (TransformComponent) component;
            if (transform.ParentUid == gridUid && !removedRoots.Contains(uid))
                return true;

            if (!IsAuthoredEntity(uid, mapUid) && IsDescendantOfAny(transform.ParentUid, removedRoots))
                return true;
        }

        return false;
    }

    private (uint Index, Decal Decal)[] GetLevelDecals(Entity<MapGridComponent> grid, int level)
    {
        if (!TryComp<DecalGridComponent>(grid.Owner, out var component))
            return [];

        return _decals.GetDecalsIntersecting(
                grid.Owner,
                grid.Comp.LocalAABB,
                component,
                level)
            .ToArray();
    }

    private bool HasRemainingDecals(EntityUid gridUid, int removedDecalCount)
    {
        return TryComp<DecalGridComponent>(gridUid, out var component) &&
               component.DecalIndex.Count > removedDecalCount;
    }

    private void DetachExcludedDescendants(
        EntityUid mapUid,
        EntityUid gridUid,
        HashSet<EntityUid> authoredRoots,
        int level)
    {
        if (authoredRoots.Count == 0)
            return;

        var candidates = new List<(EntityUid Uid, TransformComponent Transform)>();
        foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
        {
            var transform = (TransformComponent) component;
            if (IsAuthoredEntity(uid, mapUid) ||
                !IsDescendantOfAny(transform.ParentUid, authoredRoots) ||
                transform.ParentUid.IsValid() && !IsAuthoredEntity(transform.ParentUid, mapUid))
            {
                continue;
            }

            candidates.Add((uid, transform));
        }

        var gridRotation = _transform.GetWorldRotation(gridUid);
        var mapId = Transform(gridUid).MapID;
        foreach (var (uid, transform) in candidates)
        {
            var worldPosition = _transform.GetWorldPosition(transform);
            var worldRotation = _transform.GetWorldRotation(transform);
            _containers.TryRemoveFromContainer(uid, force: true);
            var coordinates = _transform.ToCoordinates(gridUid, new MapCoordinates(worldPosition, mapId));
            _transform.SetCoordinates(uid, transform, coordinates, worldRotation - gridRotation);
            _zLevel.SetZLevelPosition(uid, level);
        }
    }

    private void RelocateRuntimeLevelRoots(EntityUid mapUid, EntityUid gridUid, int sourceLevel, int targetLevel)
    {
        foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
        {
            var transform = (TransformComponent) component;
            if (transform.ParentUid != gridUid ||
                IsAuthoredEntity(uid, mapUid) ||
                _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid))) != sourceLevel)
            {
                continue;
            }

            _zLevel.SetZLevelPosition(uid, targetLevel);
        }
    }

    private bool IsDescendantOfAny(EntityUid uid, HashSet<EntityUid> roots)
    {
        while (uid.IsValid())
        {
            if (roots.Contains(uid))
                return true;

            if (!TryComp(uid, out TransformComponent? transform))
                return false;

            uid = transform.ParentUid;
        }

        return false;
    }

    private void Reply(ICommonSession session, string message, bool error = false)
    {
        RaiseNetworkEvent(new ZLevelMappingResultEvent(message, error), session.Channel);
    }
}
