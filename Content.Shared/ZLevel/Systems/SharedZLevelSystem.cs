// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Linq;
using Content.Shared.ZLevel.Components;
using Content.Shared.Friction;
using Content.Shared.Gravity;
using Content.Shared.Movement.Systems;
using Content.Shared.Throwing;
using Content.Shared.ZLevel;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// ZLevel experimental vertical support and falling resolver.
/// This keeps sparse layer semantics opt-in and leaves horizontal movement fully 2D for now.
/// </summary>
public sealed class SharedZLevelSystem : VirtualController
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<ZLevelKinematicsComponent> _kinematicsQuery;
    private EntityQuery<ZLevelPositionComponent> _positionQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ThrownItemComponent> _thrownQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly List<EntityUid> _anchoredBuffer = new();
    private readonly HashSet<EntityUid> _activeBodies = new();
    private readonly List<EntityUid> _activeBodyBuffer = new();
    private readonly HashSet<EntityUid> _refreshBodyBuffer = new();
    private readonly Dictionary<BodyTileKey, HashSet<EntityUid>> _bodiesByTile = new();
    private readonly Dictionary<EntityUid, BodyTileKey> _bodyTiles = new();

    public int ActiveBodyCount => _activeBodies.Count;
    public int IndexedBodyCount => _bodyTiles.Count;

    public override void Initialize()
    {
        UpdatesBefore.Add(typeof(SharedMoverController));
        UpdatesBefore.Add(typeof(TileFrictionController));

        base.Initialize();

        _kinematicsQuery = GetEntityQuery<ZLevelKinematicsComponent>();
        _positionQuery = GetEntityQuery<ZLevelPositionComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _thrownQuery = GetEntityQuery<ThrownItemComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ZLevelPositionComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelKinematicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelPositionComponent, ComponentRemove>(OnBodyComponentRemove);
        SubscribeLocalEvent<ZLevelKinematicsComponent, ComponentRemove>(OnBodyComponentRemove);
        SubscribeLocalEvent<PhysicsComponent, ComponentAdd>(OnPhysicsAdd);
        SubscribeLocalEvent<PhysicsComponent, ComponentRemove>(OnPhysicsRemove);
        SubscribeLocalEvent<ZLevelPositionComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<ZLevelPositionComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<ZLevelPositionComponent, MapUidChangedEvent>(OnMapChanged);
        SubscribeLocalEvent<ZLevelPositionComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<ZLevelPositionComponent, WeightlessnessChangedEvent>(OnWeightlessnessChanged);
        SubscribeLocalEvent<ThrownItemComponent, ComponentStartup>(OnThrownStartup);
        SubscribeLocalEvent<ThrownItemComponent, ComponentRemove>(OnThrownRemove);
        SubscribeLocalEvent<GravityChangedEvent>(OnGravityChanged);
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelTileChangedEvent>(OnDZTileChanged);
        SubscribeLocalEvent<ZLevelBoundaryChangedEvent>(OnBoundaryChanged);
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        _activeBodyBuffer.Clear();
        _activeBodyBuffer.AddRange(_activeBodies);

        foreach (var uid in _activeBodyBuffer)
        {
            if (!_positionQuery.TryComp(uid, out var dzPosition) ||
                !_kinematicsQuery.TryComp(uid, out var dzKinematics) ||
                !_transformQuery.TryComp(uid, out var transform) ||
                !_physicsQuery.TryComp(uid, out var physics))
            {
                RemoveBody(uid);
                continue;
            }

            ResolveVerticalState((uid, dzPosition, dzKinematics, transform, physics), frameTime);
        }

        _activeBodyBuffer.Clear();
    }

    public bool IsBodyActive(EntityUid uid)
    {
        return _activeBodies.Contains(uid);
    }

    public bool TryGetSupportTile(
        EntityUid uid,
        TransformComponent? transform,
        ZLevelPositionComponent? position,
        ZLevelKinematicsComponent? kinematics,
        out ZLevelTileRef tile)
    {
        tile = ZLevelTileRef.Zero;
        if (!_transformQuery.Resolve(uid, ref transform, false) ||
            !_positionQuery.Resolve(uid, ref position, false) ||
            !_kinematicsQuery.Resolve(uid, ref kinematics, false) ||
            transform.GridUid == null ||
            !_gridQuery.TryComp(transform.GridUid, out var grid))
        {
            return false;
        }

        var xy = _map.TileIndicesFor(transform.GridUid.Value, grid, transform.Coordinates);
        return TryGetSupportTile(
            transform.GridUid.Value,
            grid,
            xy,
            position.ZLevel,
            Math.Max(0, kinematics.MaxStepDownDepth),
            out tile);
    }

    public bool TryGetSupportTile(EntityUid uid, out ZLevelTileRef tile)
    {
        return TryGetSupportTile(uid, null, null, null, out tile);
    }

    public int GetZLevel(EntityUid uid, ZLevelPositionComponent? position = null)
    {
        return _positionQuery.Resolve(uid, ref position, false)
            ? position.ZLevel
            : 0;
    }

    public bool IsOnZLevel(EntityUid uid, int zLevel, ZLevelPositionComponent? position = null)
    {
        return GetZLevel(uid, position) == zLevel;
    }

    public void GetAnchoredEntitiesOnZLevel(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tileIndices,
        int zLevel,
        List<EntityUid> entities)
    {
        entities.Clear();
        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tileIndices);
        while (anchored.MoveNext(out var ent))
        {
            if (GetZLevel(ent.Value) != zLevel)
                continue;

            entities.Add(ent.Value);
        }
    }

    public List<EntityUid> GetAnchoredEntitiesOnZLevel(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tileIndices,
        int zLevel)
    {
        GetAnchoredEntitiesOnZLevel(gridUid, grid, tileIndices, zLevel, _anchoredBuffer);
        return _anchoredBuffer;
    }

    public bool EnsureZLevelEntity(EntityUid uid, int? zLevel = null, int maxStepDownDepth = 2)
    {
        if (!_transformQuery.TryComp(uid, out _))
            return false;

        var position = EnsureComp<ZLevelPositionComponent>(uid);
        var kinematics = EnsureComp<ZLevelKinematicsComponent>(uid);

        if (zLevel != null)
            position.ZLevel = zLevel.Value;

        position.LocalZOffset = ClampLocalOffset(position.LocalZOffset);
        kinematics.MaxStepDownDepth = Math.Max(0, maxStepDownDepth);
        kinematics.VerticalVelocity = 0f;

        Dirty(uid, position);
        Dirty(uid, kinematics);
        _boundaries.RefreshBoundary(uid);
        RefreshEntity(uid);
        return true;
    }

    public bool DisableZLevelEntity(EntityUid uid)
    {
        var removed = RemCompDeferred<ZLevelPositionComponent>(uid);
        removed |= RemCompDeferred<ZLevelKinematicsComponent>(uid);
        return removed;
    }

    public bool SetZLevel(EntityUid uid, int zLevel, float localOffset = 0f)
    {
        if (!EnsureZLevelEntity(uid, zLevel))
            return false;

        var position = Comp<ZLevelPositionComponent>(uid);
        var kinematics = Comp<ZLevelKinematicsComponent>(uid);

        position.ZLevel = zLevel;
        position.LocalZOffset = ClampLocalOffset(localOffset);
        kinematics.VerticalVelocity = 0f;

        Dirty(uid, position);
        Dirty(uid, kinematics);
        _boundaries.RefreshBoundary(uid);
        RefreshEntity(uid);
        return true;
    }

    public bool OffsetZLevel(EntityUid uid, int delta)
    {
        if (!EnsureZLevelEntity(uid))
            return false;

        var position = Comp<ZLevelPositionComponent>(uid);
        return SetZLevel(uid, position.ZLevel + delta, position.LocalZOffset);
    }

    /// <summary>
    /// Attempts to traverse the entity to an adjacent z-level through an explicit traversal object such as stairs or ladders.
    /// This validates destination support and the connector channels resolved by the shared boundary system.
    /// </summary>
    public bool TryTraverseAdjacentLevel(
        EntityUid uid,
        int zOffset,
        bool requireDirectDestinationSupport = true)
    {
        if (zOffset is 0 || !EnsureZLevelEntity(uid))
            return false;

        if (!_transformQuery.TryComp(uid, out var transform) ||
            !_positionQuery.TryComp(uid, out var position) ||
            !_kinematicsQuery.TryComp(uid, out var kinematics) ||
            transform.GridUid == null ||
            !_gridQuery.TryComp(transform.GridUid, out var grid))
        {
            return false;
        }

        var currentZ = position.ZLevel;
        var targetZ = currentZ + zOffset;
        var xy = _map.TileIndicesFor(transform.GridUid.Value, grid, transform.Coordinates);

        if (!_boundaries.CanTraverse(transform.GridUid.Value, grid, xy, currentZ, targetZ))
        {
            return false;
        }

        if (!TryGetSupportTile(transform.GridUid.Value, grid, xy, targetZ, Math.Max(0, kinematics.MaxStepDownDepth), out var support))
            return false;

        if (requireDirectDestinationSupport && support.GridIndices.Z != targetZ)
            return false;

        return SetZLevel(uid, targetZ, 0f);
    }

    public bool StampSupportPatch(EntityUid uid, int targetZ, int radius = 1)
    {
        if (radius < 0 ||
            !_transformQuery.TryComp(uid, out var transform) ||
            transform.GridUid == null ||
            !_gridQuery.TryComp(transform.GridUid, out var grid))
        {
            return false;
        }

        var sourceZ = _positionQuery.TryComp(uid, out var position) ? position.ZLevel : 0;
        var center = _map.TileIndicesFor(transform.GridUid.Value, grid, transform.Coordinates);
        var wroteTile = false;

        for (var x = center.X - radius; x <= center.X + radius; x++)
        {
            for (var y = center.Y - radius; y <= center.Y + radius; y++)
            {
                var xy = new Vector2i(x, y);
                var tile = GetStampSourceTile(transform.GridUid.Value, grid, xy, sourceZ);
                if (tile.IsEmpty)
                    continue;

                _map.SetZLevelTile(transform.GridUid.Value, grid, new ZLevelTileIndices(x, y, targetZ), tile);
                wroteTile = true;
            }
        }

        if (wroteTile)
            RefreshEntity(uid);

        return wroteTile;
    }

    private void OnStartup<T>(EntityUid uid, T component, ref ComponentStartup args) where T : IComponent
    {
        RefreshEntity(uid);
    }

    private void OnBodyComponentRemove<T>(EntityUid uid, T component, ref ComponentRemove args) where T : IComponent
    {
        RemoveBody(uid);
    }

    private void OnPhysicsRemove(Entity<PhysicsComponent> entity, ref ComponentRemove args)
    {
        if (_positionQuery.HasComp(entity.Owner))
            RemoveBody(entity.Owner);
    }

    private void OnPhysicsAdd(Entity<PhysicsComponent> entity, ref ComponentAdd args)
    {
        if (!_positionQuery.HasComp(entity.Owner) || !_kinematicsQuery.HasComp(entity.Owner))
            return;

        UpdateBodyIndex(entity.Owner);
        _activeBodies.Add(entity.Owner);
    }

    private void OnMoved(Entity<ZLevelPositionComponent> entity, ref MoveEvent args)
    {
        RefreshEntity(entity.Owner);
    }

    private void OnParentChanged(Entity<ZLevelPositionComponent> entity, ref EntParentChangedMessage args)
    {
        RefreshEntity(entity.Owner);
    }

    private void OnMapChanged(Entity<ZLevelPositionComponent> entity, ref MapUidChangedEvent args)
    {
        RefreshEntity(entity.Owner);
    }

    private void OnPreventCollide(Entity<ZLevelPositionComponent> entity, ref PreventCollideEvent args)
    {
        var ourZ = entity.Comp.ZLevel;
        var otherZ = GetZLevel(args.OtherEntity);

        if (ourZ != otherZ)
            args.Cancelled = true;
    }

    private void OnWeightlessnessChanged(Entity<ZLevelPositionComponent> entity, ref WeightlessnessChangedEvent args)
    {
        RefreshEntity(entity.Owner);
    }

    private void OnThrownStartup(Entity<ThrownItemComponent> entity, ref ComponentStartup args)
    {
        if (_positionQuery.HasComp(entity.Owner))
            RefreshEntity(entity.Owner);
    }

    private void OnThrownRemove(Entity<ThrownItemComponent> entity, ref ComponentRemove args)
    {
        if (_positionQuery.HasComp(entity.Owner))
            _activeBodies.Add(entity.Owner);
    }

    private void OnGravityChanged(ref GravityChangedEvent args)
    {
        _refreshBodyBuffer.Clear();
        foreach (var (uid, location) in _bodyTiles)
        {
            if (location.GridUid == args.ChangedGridIndex)
                _refreshBodyBuffer.Add(uid);
        }

        RefreshBufferedBodies();
    }

    private void OnDZTileChanged(ref ZLevelTileChangedEvent args)
    {
        _refreshBodyBuffer.Clear();
        foreach (var change in args.Changes)
        {
            var tile = new Vector2i(change.GridIndices.X, change.GridIndices.Y);
            _boundaries.InvalidateBoundary(args.Entity.Owner, tile, change.GridIndices.Z - 1);
            AddBodiesAt(args.Entity.Owner, tile);
        }

        RefreshBufferedBodies();
    }

    private void OnTileChanged(ref TileChangedEvent args)
    {
        _refreshBodyBuffer.Clear();
        foreach (var change in args.Changes)
        {
            _boundaries.InvalidateBoundary(args.Entity.Owner, change.GridIndices, -1);
            AddBodiesAt(args.Entity.Owner, change.GridIndices);
        }

        RefreshBufferedBodies();
    }

    private void OnBoundaryChanged(ref ZLevelBoundaryChangedEvent args)
    {
        _refreshBodyBuffer.Clear();
        AddBodiesAt(args.Grid.Owner, args.Tile);
        RefreshBufferedBodies();
    }

    private void RefreshEntity(EntityUid uid)
    {
        UpdateBodyIndex(uid);

        if (!_positionQuery.TryComp(uid, out var dzPosition) ||
            !_transformQuery.TryComp(uid, out var transform) ||
            !_physicsQuery.TryComp(uid, out var physics))
        {
            return;
        }

        if (!_kinematicsQuery.TryComp(uid, out var dzKinematics))
        {
            // Non-anchored world entities on a z-level need vertical tracking so unsupported moves can fall.
            if (transform.Anchored)
                return;

            dzKinematics = EnsureComp<ZLevelKinematicsComponent>(uid);
        }

        ResolveVerticalState((uid, dzPosition, dzKinematics, transform, physics), 0f);
    }

    private void ResolveVerticalState(
        Entity<ZLevelPositionComponent, ZLevelKinematicsComponent, TransformComponent, PhysicsComponent> entity,
        float frameTime)
    {
        var (uid, dzPosition, dzKinematics, transform, physics) = entity;

        if (transform.GridUid == null || !_gridQuery.TryComp(transform.GridUid, out var grid))
        {
            SetGrounded(uid, physics, dzKinematics, false);
            _activeBodies.Remove(uid);
            return;
        }

        var xy = _map.TileIndicesFor(transform.GridUid.Value, grid, transform.Coordinates);
        var weightless = _gravity.IsWeightless(uid);
        var activelyThrown = _thrownQuery.TryComp(uid, out var thrown) &&
            !thrown.Landed &&
            thrown.LandTime > _timing.CurTime;

        if (activelyThrown)
        {
            SetGrounded(uid, physics, dzKinematics, false);
            _activeBodies.Add(uid);
            Dirty(uid, dzKinematics);
            return;
        }

        var currentlySupported = IsStandingOnCurrentLayer(transform.GridUid.Value, grid, xy, dzPosition);

        if (!weightless && !currentlySupported)
            dzKinematics.VerticalVelocity = MathF.Max(-dzKinematics.TerminalVelocity, dzKinematics.VerticalVelocity - dzKinematics.Gravity * frameTime);

        dzPosition.LocalZOffset += dzKinematics.VerticalVelocity * frameTime;

        var traversing = true;
        while (dzPosition.LocalZOffset < 0f)
        {
            if (!_boundaries.CanBodyPass(transform.GridUid.Value, grid, xy, dzPosition.ZLevel, dzPosition.ZLevel - 1))
            {
                dzPosition.LocalZOffset = 0f;
                dzKinematics.VerticalVelocity = 0f;
                traversing = false;
                break;
            }

            dzPosition.ZLevel -= 1;
            dzPosition.LocalZOffset += 1f;
        }

        while (traversing && dzPosition.LocalZOffset >= 1f)
        {
            if (!_boundaries.CanBodyPass(transform.GridUid.Value, grid, xy, dzPosition.ZLevel, dzPosition.ZLevel + 1))
            {
                dzPosition.LocalZOffset = MathF.BitDecrement(1f);
                dzKinematics.VerticalVelocity = 0f;
                traversing = false;
                break;
            }

            dzPosition.ZLevel += 1;
            dzPosition.LocalZOffset -= 1f;
        }

        NormalizeVerticalPosition(dzPosition);
        var currentWorldHeight = dzPosition.ZLevel + dzPosition.LocalZOffset;

        if (TryGetSupportTile(uid, transform, dzPosition, dzKinematics, out var supportTile) &&
            currentWorldHeight <= supportTile.GridIndices.Z + 0.001f)
        {
            dzPosition.ZLevel = supportTile.GridIndices.Z;
            dzPosition.LocalZOffset = 0f;
            dzKinematics.VerticalVelocity = 0f;
            SetGrounded(uid, physics, dzKinematics, true);
        }
        else
        {
            SetGrounded(uid, physics, dzKinematics, false);
        }

        if (dzKinematics.Grounded ||
            weightless && MathF.Abs(dzKinematics.VerticalVelocity) < 0.001f && dzPosition.LocalZOffset < 0.001f)
        {
            _activeBodies.Remove(uid);
        }
        else
        {
            _activeBodies.Add(uid);
        }

        Dirty(uid, dzPosition);
        Dirty(uid, dzKinematics);
    }

    private void UpdateBodyIndex(EntityUid uid)
    {
        if (!_positionQuery.HasComp(uid) ||
            !_kinematicsQuery.HasComp(uid) ||
            !_physicsQuery.HasComp(uid) ||
            !_transformQuery.TryComp(uid, out var transform) ||
            transform.GridUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            RemoveBodyIndex(uid);
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var location = new BodyTileKey(gridUid, tile);
        if (_bodyTiles.TryGetValue(uid, out var oldLocation) && oldLocation == location)
            return;

        RemoveBodyIndex(uid);
        _bodyTiles[uid] = location;

        if (!_bodiesByTile.TryGetValue(location, out var bodies))
        {
            bodies = new HashSet<EntityUid>();
            _bodiesByTile.Add(location, bodies);
        }

        bodies.Add(uid);
    }

    private void RemoveBody(EntityUid uid)
    {
        _activeBodies.Remove(uid);
        RemoveBodyIndex(uid);
    }

    private void RemoveBodyIndex(EntityUid uid)
    {
        if (!_bodyTiles.Remove(uid, out var location) ||
            !_bodiesByTile.TryGetValue(location, out var bodies))
        {
            return;
        }

        bodies.Remove(uid);
        if (bodies.Count == 0)
            _bodiesByTile.Remove(location);
    }

    private void AddBodiesAt(EntityUid gridUid, Vector2i tile)
    {
        if (!_bodiesByTile.TryGetValue(new BodyTileKey(gridUid, tile), out var bodies))
            return;

        _refreshBodyBuffer.UnionWith(bodies);
    }

    private void RefreshBufferedBodies()
    {
        foreach (var uid in _refreshBodyBuffer)
        {
            RefreshEntity(uid);
        }

        _refreshBodyBuffer.Clear();
    }

    private void SetGrounded(EntityUid uid, PhysicsComponent physics, ZLevelKinematicsComponent kinematics, bool grounded)
    {
        kinematics.Grounded = grounded;
        PhysicsSystem.SetBodyStatus(uid, physics, grounded ? BodyStatus.OnGround : BodyStatus.InAir);
    }

    private static void NormalizeVerticalPosition(ZLevelPositionComponent dzPosition)
    {
        while (dzPosition.LocalZOffset < 0f)
        {
            dzPosition.ZLevel -= 1;
            dzPosition.LocalZOffset += 1f;
        }

        while (dzPosition.LocalZOffset >= 1f)
        {
            dzPosition.ZLevel += 1;
            dzPosition.LocalZOffset -= 1f;
        }
    }

    private bool IsStandingOnCurrentLayer(EntityUid gridUid, MapGridComponent grid, Vector2i xy, ZLevelPositionComponent dzPosition)
    {
        if (dzPosition.LocalZOffset > 0.001f)
            return false;

        return !_map.IsZLevelTileEmpty(gridUid, grid, new ZLevelTileIndices(xy.X, xy.Y, dzPosition.ZLevel)) &&
               !_boundaries.CanBodyPass(gridUid, grid, xy, dzPosition.ZLevel, dzPosition.ZLevel - 1);
    }

    private bool TryGetSupportTile(
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i xy,
        int startZ,
        int maxDropDepth,
        out ZLevelTileRef tile)
    {
        tile = ZLevelTileRef.Zero;
        if (maxDropDepth < 0)
            return false;

        foreach (var z in _map.GetExistingZLevelLayersAt(gridUid, grid, xy, startZ - maxDropDepth, startZ).Reverse())
        {
            var candidate = _map.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(xy.X, xy.Y, z));
            if (candidate.Tile.IsEmpty || _boundaries.CanBodyPass(gridUid, grid, xy, z, z - 1))
                continue;

            var reachable = true;
            for (var currentZ = startZ; currentZ > z; currentZ--)
            {
                if (_boundaries.CanBodyPass(gridUid, grid, xy, currentZ, currentZ - 1))
                    continue;

                reachable = false;
                break;
            }

            if (!reachable)
                return false;

            tile = candidate;
            return true;
        }

        return false;
    }

    private Tile GetStampSourceTile(EntityUid gridUid, MapGridComponent grid, Vector2i xy, int sourceZ)
    {
        var tile = _map.GetZLevelTileRef(gridUid, grid, new ZLevelTileIndices(xy.X, xy.Y, sourceZ)).Tile;
        if (!tile.IsEmpty)
            return tile;

        return _map.GetTileRef(gridUid, grid, xy).Tile;
    }

    private static float ClampLocalOffset(float localOffset)
    {
        return Math.Clamp(localOffset, 0f, MathF.BitDecrement(1f));
    }

    private readonly record struct BodyTileKey(EntityUid GridUid, Vector2i Tile);
}
