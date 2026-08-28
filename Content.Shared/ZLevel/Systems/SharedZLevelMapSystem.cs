// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Linq;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Events;
using Robust.Shared.Player;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Owns the versioned map-level contract for native Z-level maps.
/// </summary>
public sealed class SharedZLevelMapSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZLevelMapComponent, ComponentStartup>(OnConfigurationStartup);
        SubscribeLocalEvent<ZLevelMapComponent, ComponentShutdown>(OnConfigurationShutdown);
        SubscribeLocalEvent<ZLevelMapComponent, AfterAutoHandleStateEvent>(OnConfigurationStateHandled);
        SubscribeLocalEvent<BeforeSerializationEvent>(OnBeforeSerialization);
    }

    public bool TryGetConfig(EntityUid gridUid, out Entity<ZLevelMapComponent> config)
    {
        config = default;
        if (!TryComp(gridUid, out TransformComponent? transform) ||
            transform.MapUid is not { } mapUid ||
            !TryComp<ZLevelMapComponent>(mapUid, out var component))
        {
            return false;
        }

        config = (mapUid, component);
        return true;
    }

    public void Configure(
        EntityUid mapUid,
        int minimumLevel,
        int maximumLevel,
        int defaultLevel,
        ZLevelDefaultBoundaryMode boundaryMode)
    {
        if (!HasComp<MapComponent>(mapUid))
            throw new ArgumentException("Z-level configuration can only be attached to a map entity.", nameof(mapUid));

        ValidateRange(minimumLevel, maximumLevel, defaultLevel);

        var config = EnsureComp<ZLevelMapComponent>(mapUid);
        config.FormatVersion = ZLevelMapComponent.CurrentFormatVersion;
        config.MinimumLevel = minimumLevel;
        config.MaximumLevel = maximumLevel;
        config.DefaultLevel = defaultLevel;
        config.DefaultBoundaryMode = boundaryMode;
        Dirty(mapUid, config);
        RaiseConfigurationChanged(mapUid);
    }

    private void OnConfigurationStartup(Entity<ZLevelMapComponent> entity, ref ComponentStartup args)
    {
        RaiseConfigurationChanged(entity.Owner);
    }

    private void OnConfigurationShutdown(Entity<ZLevelMapComponent> entity, ref ComponentShutdown args)
    {
        RaiseConfigurationChanged(entity.Owner);
    }

    private void OnConfigurationStateHandled(Entity<ZLevelMapComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        RaiseConfigurationChanged(entity.Owner);
    }

    private void RaiseConfigurationChanged(EntityUid mapUid)
    {
        var ev = new ZLevelMapConfigurationChangedEvent(mapUid);
        RaiseLocalEvent(mapUid, ref ev, true);
    }

    public bool TryValidate(EntityUid mapUid, out string error)
    {
        error = string.Empty;
        if (!TryComp<MapComponent>(mapUid, out var mapComponent))
        {
            error = $"Entity {mapUid} is not a map.";
            return false;
        }

        var grids = _mapManager.GetAllGrids(mapComponent.MapId).ToArray();
        var authoredLevels = grids
            .SelectMany(grid => _map.GetExistingZLevelLayers(grid.Owner, grid.Comp))
            .Distinct()
            .Order()
            .ToArray();
        var authoredEntities = EntityManager.GetAllComponents(typeof(ZLevelPositionComponent), includePaused: true)
            .Where(entry => TryComp(entry.Uid, out TransformComponent? transform) &&
                            transform.MapUid == mapUid &&
                            !HasComp<ActorComponent>(entry.Uid) &&
                            MetaData(entry.Uid).EntityPrototype?.MapSavable != false)
            .Select(entry => (entry.Uid, Level: _transform.GetZLevel((
                entry.Uid,
                Transform(entry.Uid),
                (ZLevelPositionComponent) entry.Component))))
            .ToArray();
        var hasNonZeroContent = authoredLevels.Any(level => level != 0) ||
                                authoredEntities.Any(entity => entity.Level != 0);

        if (!TryComp<ZLevelMapComponent>(mapUid, out var config))
        {
            if (!hasNonZeroContent)
                return true;

            error = "The map contains non-zero Z-level content but has no ZLevelMap format component.";
            return false;
        }

        if (config.FormatVersion != ZLevelMapComponent.CurrentFormatVersion)
        {
            error = $"Unsupported Z-level map format version {config.FormatVersion}; expected {ZLevelMapComponent.CurrentFormatVersion}.";
            return false;
        }

        try
        {
            ValidateRange(config.MinimumLevel, config.MaximumLevel, config.DefaultLevel);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            error = exception.Message;
            return false;
        }

        foreach (var authoredLevel in authoredLevels)
        {
            if (authoredLevel >= config.MinimumLevel && authoredLevel <= config.MaximumLevel)
                continue;

            error = $"Authored Z level {authoredLevel} is outside the declared range " +
                    $"[{config.MinimumLevel}, {config.MaximumLevel}].";
            return false;
        }

        foreach (var (uid, level) in authoredEntities)
        {
            if (level >= config.MinimumLevel && level <= config.MaximumLevel)
                continue;

            error = $"Entity {ToPrettyString(uid)} is on Z level {level}, outside the declared range " +
                    $"[{config.MinimumLevel}, {config.MaximumLevel}].";
            return false;
        }

        return true;
    }

    private void OnBeforeSerialization(BeforeSerializationEvent args)
    {
        foreach (var mapId in args.MapIds)
        {
            if (!_map.TryGetMap(mapId, out var mapUid) || TryValidate(mapUid.Value, out var error))
                continue;

            throw new InvalidOperationException($"Refusing to serialize invalid Z-level map {mapId}: {error}");
        }
    }

    private static void ValidateRange(int minimumLevel, int maximumLevel, int defaultLevel)
    {
        if (minimumLevel > maximumLevel)
            throw new ArgumentOutOfRangeException(nameof(minimumLevel), "Minimum Z level cannot exceed maximum Z level.");

        if (defaultLevel < minimumLevel || defaultLevel > maximumLevel)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultLevel),
                "Default Z level must be inside the declared level range.");
        }
    }
}
