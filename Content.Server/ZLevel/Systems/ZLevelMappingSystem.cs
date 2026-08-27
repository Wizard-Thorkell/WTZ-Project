// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Decals;
using Content.Shared.Administration;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
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
    [Dependency] private readonly DecalSystem _decals = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
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

        if (LifeStage(mapUid) >= EntityLifeStage.MapInitialized)
        {
            Reply(session, "Z-level mapping operations require a pre-initialized mapping map.", true);
            return;
        }

        try
        {
            if (request.Operation == ZLevelMappingOperation.ConfigureMap)
            {
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

        var sourceRoots = GetLevelRoots(grid.Owner, sourceLevel);
        var targetRoots = GetLevelRoots(grid.Owner, targetLevel);
        IReadOnlyDictionary<EntityUid, int>? yamlIds = null;
        LoadResult? result = null;

        if (sourceRoots.Count > 0)
        {
            var serialization = SerializationOptions.Default with
            {
                Category = FileCategory.Unknown,
                ExpectPreInit = true,
                MissingEntityBehaviour = MissingEntityBehaviour.Error,
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
        var sourceDecals = _decals.GetDecalsIntersecting(
                grid.Owner,
                grid.Comp.LocalAABB,
                zLevel: sourceLevel)
            .Select(entry => entry.Decal)
            .ToArray();

        foreach (var tile in targetTiles)
            _map.SetZLevelTile(grid.Owner, grid.Comp, tile, Tile.Empty);

        foreach (var entity in targetRoots)
            QueueDel(entity);

        foreach (var tile in sourceTiles)
        {
            var target = new ZLevelTileIndices(tile.GridIndices.X, tile.GridIndices.Y, targetLevel);
            _map.SetZLevelTile(grid.Owner, grid.Comp, target, tile.Tile);
        }

        foreach (var decal in sourceDecals)
        {
            if (!_decals.TryAddDecal(
                    decal.WithZLevel(targetLevel),
                    new EntityCoordinates(grid.Owner, decal.Coordinates),
                    out _))
            {
                throw new InvalidOperationException($"Failed to copy decal '{decal.Id}' to Z level {targetLevel}.");
            }
        }

        IncludeLevel(mapUid, config, targetLevel);

        if (sourceRoots.Count == 0)
            return 0;

        var loadedByYamlId = new Dictionary<int, EntityUid>();
        foreach (var loaded in result!.Entities)
        {
            if (TryComp<YamlUidComponent>(loaded, out var yamlUid))
                loadedByYamlId[yamlUid.Uid] = loaded;
        }

        var levelOffset = targetLevel - sourceLevel;
        foreach (var (source, yamlId) in yamlIds!)
        {
            if (!loadedByYamlId.TryGetValue(yamlId, out var clone))
                continue;

            if (TryComp<ZLevelPositionComponent>(source, out var sourceZ))
                _zLevel.SetZLevelPosition(clone, sourceZ.ZLevel + levelOffset);

            RemCompDeferred<YamlUidComponent>(clone);
        }

        foreach (var source in sourceRoots)
        {
            if (!yamlIds!.TryGetValue(source, out var yamlId) ||
                !loadedByYamlId.TryGetValue(yamlId, out var clone) ||
                !TryComp(source, out TransformComponent? sourceTransform) ||
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

        var tiles = _map.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp)
            .Where(tile => tile.GridIndices.Z == level)
            .Select(tile => tile.GridIndices)
            .ToArray();
        foreach (var tile in tiles)
            _map.SetZLevelTile(grid.Owner, grid.Comp, tile, Tile.Empty);

        var entities = GetLevelRoots(grid.Owner, level);

        foreach (var entity in entities)
            QueueDel(entity);

        var minimum = config.MinimumLevel;
        var maximum = config.MaximumLevel;
        if (minimum != maximum && level == minimum)
            minimum++;
        else if (minimum != maximum && level == maximum)
            maximum--;

        var defaultLevel = Math.Clamp(config.DefaultLevel, minimum, maximum);
        _format.Configure(mapUid, minimum, maximum, defaultLevel, config.DefaultBoundaryMode);
        return entities.Count;
    }

    private HashSet<EntityUid> GetLevelRoots(EntityUid gridUid, int level)
    {
        var entities = new HashSet<EntityUid>();
        foreach (var (uid, component) in EntityManager.GetAllComponents(typeof(TransformComponent), includePaused: true))
        {
            var transform = (TransformComponent) component;
            if (transform.ParentUid != gridUid ||
                _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid))) != level ||
                HasComp<ActorComponent>(uid) ||
                MetaData(uid).EntityPrototype?.MapSavable == false)
            {
                continue;
            }

            entities.Add(uid);
        }

        return entities;
    }

    private void Reply(ICommonSession session, string message, bool error = false)
    {
        RaiseNetworkEvent(new ZLevelMappingResultEvent(message, error), session.Channel);
    }
}
