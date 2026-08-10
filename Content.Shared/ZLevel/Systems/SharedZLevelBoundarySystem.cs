// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Placement;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Resolves content-driven vertical boundaries while preserving the legacy
/// upper-tile rule as the default.
/// </summary>
public sealed class SharedZLevelBoundarySystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<EntityUid, BoundaryRegistration> _registrations = new();

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ZLevelBoundaryComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelBoundaryComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelBoundaryComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<ZLevelBoundaryComponent, AnchorStateChangedEvent>(OnAnchorChanged);
        SubscribeLocalEvent<ZLevelBoundaryComponent, AfterAutoHandleStateEvent>(OnAfterState);
        SubscribeLocalEvent<ZLevelBoundaryComponent, ZLevelBoundaryQueryEvent>(OnQuery);
        SubscribeLocalEvent<PlacementEntityEvent>(OnPlacement);
    }

    public bool TryGetBoundary(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int firstZ,
        int secondZ,
        out ZLevelBoundaryState boundary)
    {
        boundary = default;
        if (Math.Abs(firstZ - secondZ) != 1)
            return false;

        var lowerZ = Math.Min(firstZ, secondZ);
        var lower = new ZLevelTileIndices(tile.X, tile.Y, lowerZ);
        var upper = new ZLevelTileIndices(tile.X, tile.Y, lowerZ + 1);
        var defaultOpen = !_map.IsZLevelVerticalPassageBlocked(gridUid, grid, tile, lowerZ);
        var query = new ZLevelBoundaryQueryEvent((gridUid, grid), tile, lowerZ);

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
        while (anchored.MoveNext(out var entity))
        {
            RaiseLocalEvent(entity.Value, ref query);
        }

        var openChannels = defaultOpen ? ZLevelBoundaryChannels.All : ZLevelBoundaryChannels.None;
        openChannels |= query.ForcedOpen;
        openChannels &= ~query.ForcedClosed;
        boundary = new ZLevelBoundaryState(
            lower,
            upper,
            defaultOpen,
            query.ForcedOpen,
            query.ForcedClosed,
            openChannels);
        return true;
    }

    public bool IsOpen(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int firstZ,
        int secondZ,
        ZLevelBoundaryChannels channels)
    {
        return TryGetBoundary(gridUid, grid, tile, firstZ, secondZ, out var boundary) &&
               boundary.IsOpen(channels);
    }

    public bool CanBodyPass(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ)
    {
        return IsOpen(gridUid, grid, tile, fromZ, toZ, ZLevelBoundaryChannels.Body);
    }

    public bool CanTraverse(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ)
    {
        var channel = toZ > fromZ
            ? ZLevelBoundaryChannels.TraversalUp
            : ZLevelBoundaryChannels.TraversalDown;
        return IsOpen(gridUid, grid, tile, fromZ, toZ, channel);
    }

    public bool IsStackOpen(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile,
        int fromZ,
        int toZ,
        ZLevelBoundaryChannels channel)
    {
        if (fromZ == toZ)
            return true;

        var step = Math.Sign(toZ - fromZ);
        for (var z = fromZ; z != toZ; z += step)
        {
            if (!IsOpen(gridUid, grid, tile, z, z + step, channel))
                return false;
        }

        return true;
    }

    public void SetBoundary(
        Entity<ZLevelBoundaryComponent> entity,
        bool enabled,
        int boundaryOffset,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes)
    {
        if (boundaryOffset is not (-1 or 1))
            throw new ArgumentOutOfRangeException(nameof(boundaryOffset), "Boundary offset must be -1 or 1.");

        if (entity.Comp.Enabled == enabled &&
            entity.Comp.BoundaryOffset == boundaryOffset &&
            entity.Comp.Opens == opens &&
            entity.Comp.Closes == closes)
        {
            return;
        }

        entity.Comp.Enabled = enabled;
        entity.Comp.BoundaryOffset = boundaryOffset;
        entity.Comp.Opens = opens;
        entity.Comp.Closes = closes;
        Dirty(entity);
        RefreshRegistration(entity);
    }

    public void RefreshBoundary(EntityUid uid)
    {
        if (TryComp<ZLevelBoundaryComponent>(uid, out var boundary))
            RefreshRegistration((uid, boundary));
    }

    private void OnStartup(Entity<ZLevelBoundaryComponent> entity, ref ComponentStartup args)
    {
        RefreshRegistration(entity);
    }

    private void OnShutdown(Entity<ZLevelBoundaryComponent> entity, ref ComponentShutdown args)
    {
        RemoveRegistration(entity.Owner);
    }

    private void OnMoved(Entity<ZLevelBoundaryComponent> entity, ref MoveEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnAnchorChanged(Entity<ZLevelBoundaryComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnAfterState(Entity<ZLevelBoundaryComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        RefreshRegistration(entity);
    }

    private void OnPlacement(PlacementEntityEvent args)
    {
        if (args.PlacementEventAction == PlacementEventAction.Create)
            RefreshBoundary(args.EditedEntity);
    }

    private void OnQuery(Entity<ZLevelBoundaryComponent> entity, ref ZLevelBoundaryQueryEvent args)
    {
        if (!entity.Comp.Enabled ||
            entity.Comp.BoundaryOffset is not (-1 or 1) ||
            !_transformQuery.TryComp(entity.Owner, out var transform))
        {
            return;
        }

        var entityZ = _transform.GetZLevel((entity.Owner, transform, CompOrNull<ZLevelPositionComponent>(entity.Owner)));
        var lowerZ = Math.Min(entityZ, entityZ + entity.Comp.BoundaryOffset);
        if (lowerZ != args.LowerZ)
            return;

        args.ForceOpen(entity.Comp.Opens);
        args.ForceClosed(entity.Comp.Closes);
    }

    private void RefreshRegistration(Entity<ZLevelBoundaryComponent> entity)
    {
        _registrations.TryGetValue(entity.Owner, out var oldRegistration);
        var hasOldRegistration = _registrations.ContainsKey(entity.Owner);
        var hasNewRegistration = TryGetRegistration(entity, out var newRegistration);

        if (hasOldRegistration && hasNewRegistration && oldRegistration == newRegistration)
            return;

        if (hasOldRegistration)
            RaiseChanged(oldRegistration);

        if (!hasNewRegistration)
        {
            _registrations.Remove(entity.Owner);
            return;
        }

        _registrations[entity.Owner] = newRegistration;
        RaiseChanged(newRegistration);
    }

    private void RemoveRegistration(EntityUid uid)
    {
        if (!_registrations.Remove(uid, out var registration))
            return;

        RaiseChanged(registration);
    }

    private bool TryGetRegistration(Entity<ZLevelBoundaryComponent> entity, out BoundaryRegistration registration)
    {
        registration = default;
        if (entity.Comp.BoundaryOffset is not (-1 or 1) ||
            !_transformQuery.TryComp(entity.Owner, out var transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return false;
        }

        var entityZ = _transform.GetZLevel((entity.Owner, transform, CompOrNull<ZLevelPositionComponent>(entity.Owner)));
        var lowerZ = Math.Min(entityZ, entityZ + entity.Comp.BoundaryOffset);
        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        registration = new BoundaryRegistration(
            gridUid,
            tile,
            lowerZ,
            entity.Comp.Enabled,
            entity.Comp.Opens,
            entity.Comp.Closes);
        return true;
    }

    private void RaiseChanged(BoundaryRegistration registration)
    {
        if (!_gridQuery.TryComp(registration.GridUid, out var grid))
            return;

        var ev = new ZLevelBoundaryChangedEvent((registration.GridUid, grid), registration.Tile, registration.LowerZ);
        RaiseLocalEvent(registration.GridUid, ref ev, true);
    }

    private readonly record struct BoundaryRegistration(
        EntityUid GridUid,
        Vector2i Tile,
        int LowerZ,
        bool Enabled,
        ZLevelBoundaryChannels Opens,
        ZLevelBoundaryChannels Closes);
}
