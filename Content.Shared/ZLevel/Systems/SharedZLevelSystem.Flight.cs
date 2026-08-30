// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.Buckle.Components;
using Content.Shared.Gravity;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.ZLevel.Systems;

public sealed partial class SharedZLevelSystem
{
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;

    private EntityQuery<ZLevelFlightComponent> _flightQuery;
    private readonly HashSet<EntityUid> _activeFlights = new();
    private readonly List<EntityUid> _flightBuffer = new();

    public int ActiveFlightCount => _activeFlights.Count;

    private void InitializeFlight()
    {
        _flightQuery = GetEntityQuery<ZLevelFlightComponent>();

        SubscribeLocalEvent<ZLevelFlightComponent, ComponentStartup>(OnFlightStartup);
        SubscribeLocalEvent<ZLevelFlightComponent, ComponentShutdown>(OnFlightShutdown);
        SubscribeLocalEvent<ZLevelFlightComponent, AfterAutoHandleStateEvent>(OnFlightStateHandled);
        SubscribeLocalEvent<ZLevelFlightComponent, EntParentChangedMessage>(OnFlightParentChanged);
        SubscribeLocalEvent<ZLevelFlightComponent, AnchorStateChangedEvent>(OnFlightAnchorChanged);
        SubscribeLocalEvent<ZLevelFlightComponent, PhysicsBodyTypeChangedEvent>(OnFlightBodyTypeChanged);
        SubscribeLocalEvent<ZLevelFlightComponent, EntInsertedIntoContainerMessage>(OnFlightContained);
        SubscribeLocalEvent<ZLevelFlightComponent, MobStateChangedEvent>(OnFlightMobStateChanged);
        SubscribeLocalEvent<ZLevelFlightComponent, StunnedEvent>(OnFlightStunned);
        SubscribeLocalEvent<ZLevelFlightComponent, KnockedDownEvent>(OnFlightKnockedDown);
        SubscribeLocalEvent<ZLevelFlightComponent, ThrownEvent>(OnFlightThrown);
        SubscribeLocalEvent<ZLevelFlightComponent, BuckledEvent>(OnFlightBuckled);
        SubscribeLocalEvent<ZLevelFlightComponent, IsWeightlessEvent>(OnFlightWeightless);
        SubscribeLocalEvent<ZLevelFlightComponent, CanWeightlessMoveEvent>(OnFlightCanMove);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnFlightMapConfigurationChanged);
    }

    public bool IsFlying(EntityUid uid, ZLevelFlightComponent? flight = null)
    {
        return _flightQuery.Resolve(uid, ref flight, false) && flight.Active;
    }

    /// <summary>
    /// Performs the side-effect-free portion of flight validation for route
    /// planning and installation.
    /// </summary>
    public ZLevelFlightResult GetFlightNavigationAvailability(
        EntityUid uid,
        ZLevelFlightComponent? flight = null)
    {
        if (!_flightQuery.Resolve(uid, ref flight, false))
            return ZLevelFlightResult.MissingCapability;

        var result = ValidateFlightEntity(uid, flight, out var transform, out _, out _);
        if (result != ZLevelFlightResult.Success)
            return result;

        if (flight.Active && flight.ActiveGridUid != transform.GridUid)
            return ZLevelFlightResult.InvalidGrid;

        return ZLevelFlightResult.Success;
    }

    public bool CanUseFlightNavigation(EntityUid uid, ZLevelFlightComponent? flight = null)
    {
        return GetFlightNavigationAvailability(uid, flight) == ZLevelFlightResult.Success;
    }

    /// <summary>
    /// Returns the continuous trace offset of an active flyer. Ordinary entities
    /// retain the established floor-center trace model.
    /// </summary>
    public float GetFlightTraceZOffset(EntityUid uid, ZLevelFlightComponent? flight = null)
    {
        if (!IsFlying(uid, flight) || !_transformQuery.TryComp(uid, out var transform))
            return ZLevelTracePoint.DefaultZOffset;

        var position = CompOrNull<ZLevelPositionComponent>(uid);
        var worldZ = _transform.GetWorldZLevel((uid, transform, position));
        var worldHeight = _transform.GetZLevelWorldHeight((uid, transform, position));
        var offset = worldHeight - worldZ;
        return float.IsFinite(offset) && offset >= 0f && offset < 1f
            ? offset
            : ZLevelTracePoint.DefaultZOffset;
    }

    public ZLevelFlightResult TryStartFlight(
        EntityUid uid,
        int? targetLocalZLevel = null,
        float? targetLocalZOffset = null,
        ZLevelFlightComponent? flight = null)
    {
        if (!_flightQuery.Resolve(uid, ref flight, false))
            return ZLevelFlightResult.MissingCapability;

        if (flight.Active)
            return ZLevelFlightResult.AlreadyActive;

        var validation = ValidateFlightEntity(uid, flight, out var transform, out _, out var config);
        if (validation != ZLevelFlightResult.Success)
            return validation;

        var currentLocalHeight = _transform.GetZLevelWorldHeight((
            uid,
            transform,
            _positionQuery.CompOrNull(uid))) - _transform.GetZLevelFrameOrigin((uid, transform));
        if (!float.IsFinite(currentLocalHeight))
            return ZLevelFlightResult.InvalidCurrentPosition;

        var currentZ = (int) MathF.Floor(currentLocalHeight);
        var currentOffset = ClampLocalOffset(currentLocalHeight - currentZ);
        if (!IsValidTarget(config.Comp, currentZ, currentOffset))
            return ZLevelFlightResult.InvalidCurrentPosition;

        var targetZ = targetLocalZLevel ?? currentZ;
        var targetOffset = targetLocalZOffset ?? flight.HoverOffset;
        if (!IsValidTarget(config.Comp, targetZ, targetOffset))
            return ZLevelFlightResult.InvalidTarget;

        var attempt = new ZLevelFlightStartAttemptEvent(targetZ, targetOffset);
        RaiseLocalEvent(uid, ref attempt);
        if (attempt.Cancelled)
            return ZLevelFlightResult.Cancelled;

        if (!EnsureZLevelEntity(uid, currentZ) ||
            !_positionQuery.TryComp(uid, out var position) ||
            !_kinematicsQuery.TryComp(uid, out var kinematics))
        {
            return ZLevelFlightResult.InvalidTransform;
        }

        position.LocalZOffset = currentOffset;
        Dirty(uid, position);

        EnsureComp<GravityAffectedComponent>(uid);
        flight.Active = true;
        flight.TargetLocalZLevel = targetZ;
        flight.TargetLocalZOffset = targetOffset;
        flight.ActiveGridUid = transform.GridUid;
        kinematics.VerticalVelocity = 0f;
        Dirty(uid, flight);
        Dirty(uid, kinematics);

        _activeFlights.Add(uid);
        _activeBodies.Add(uid);
        _gravity.RefreshWeightless(uid);
        _metrics.RecordFlightStarted();

        var started = new ZLevelFlightStartedEvent(targetZ, targetOffset);
        RaiseLocalEvent(uid, ref started, true);
        return ZLevelFlightResult.Success;
    }

    public ZLevelFlightResult TrySetFlightTarget(
        EntityUid uid,
        int targetLocalZLevel,
        float? targetLocalZOffset = null,
        ZLevelFlightComponent? flight = null)
    {
        if (!_flightQuery.Resolve(uid, ref flight, false))
            return ZLevelFlightResult.MissingCapability;

        if (!flight.Active)
            return ZLevelFlightResult.Inactive;

        var validation = ValidateFlightEntity(uid, flight, out var transform, out _, out var config);
        if (validation != ZLevelFlightResult.Success)
            return validation;

        if (flight.ActiveGridUid != transform.GridUid)
            return ZLevelFlightResult.InvalidGrid;

        var targetOffset = targetLocalZOffset ?? flight.HoverOffset;
        if (!IsValidTarget(config.Comp, targetLocalZLevel, targetOffset))
            return ZLevelFlightResult.InvalidTarget;

        if (flight.TargetLocalZLevel == targetLocalZLevel &&
            MathF.Abs(flight.TargetLocalZOffset - targetOffset) < 0.0001f)
        {
            return ZLevelFlightResult.NoChange;
        }

        var oldZ = flight.TargetLocalZLevel;
        var oldOffset = flight.TargetLocalZOffset;
        flight.TargetLocalZLevel = targetLocalZLevel;
        flight.TargetLocalZOffset = targetOffset;
        Dirty(uid, flight);
        _activeBodies.Add(uid);
        _metrics.RecordFlightTargetChanged();

        var changed = new ZLevelFlightTargetChangedEvent(oldZ, oldOffset, targetLocalZLevel, targetOffset);
        RaiseLocalEvent(uid, ref changed, true);
        return ZLevelFlightResult.Success;
    }

    public ZLevelFlightResult TrySetFlightWorldTarget(
        EntityUid uid,
        int targetWorldZLevel,
        float? targetLocalZOffset = null,
        ZLevelFlightComponent? flight = null)
    {
        if (!_flightQuery.Resolve(uid, ref flight, false))
            return ZLevelFlightResult.MissingCapability;

        if (!_transformQuery.TryComp(uid, out var transform) || transform.GridUid is not { } gridUid)
            return ZLevelFlightResult.InvalidGrid;

        return TrySetFlightTarget(
            uid,
            _transform.WorldToLocalZLevel(gridUid, targetWorldZLevel),
            targetLocalZOffset,
            flight);
    }

    public ZLevelFlightResult TryStopFlight(
        EntityUid uid,
        ZLevelFlightStopReason reason = ZLevelFlightStopReason.Requested,
        ZLevelFlightComponent? flight = null)
    {
        if (!_flightQuery.Resolve(uid, ref flight, false))
            return ZLevelFlightResult.MissingCapability;

        if (!flight.Active)
            return ZLevelFlightResult.Inactive;

        StopFlight(uid, flight, reason, dirty: true);
        return ZLevelFlightResult.Success;
    }

    private ZLevelFlightResult ValidateFlightEntity(
        EntityUid uid,
        ZLevelFlightComponent flight,
        out TransformComponent transform,
        out PhysicsComponent physics,
        out Entity<ZLevelMapComponent> config)
    {
        transform = default!;
        physics = default!;
        config = default;

        if (!IsValidFlightConfiguration(flight))
            return ZLevelFlightResult.InvalidConfiguration;

        if (!_transformQuery.TryComp(uid, out var resolvedTransform))
            return ZLevelFlightResult.InvalidTransform;

        transform = resolvedTransform;

        if (transform.Anchored)
            return ZLevelFlightResult.Anchored;

        if ((TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive) ||
            HasComp<StunnedComponent>(uid) ||
            HasComp<KnockedDownComponent>(uid))
        {
            return ZLevelFlightResult.Incapacitated;
        }

        if (TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled)
            return ZLevelFlightResult.Buckled;

        if (_containers.IsEntityInContainer(uid))
            return ZLevelFlightResult.Contained;

        if (!_physicsQuery.TryComp(uid, out var resolvedPhysics) ||
            resolvedPhysics.BodyType is BodyType.Static or BodyType.Kinematic)
        {
            return ZLevelFlightResult.InvalidBodyType;
        }

        physics = resolvedPhysics;

        if (transform.GridUid is not { } gridUid || !_gridQuery.HasComp(gridUid))
            return ZLevelFlightResult.InvalidGrid;

        if (!_zLevelMaps.TryGetConfig(gridUid, out config))
            return ZLevelFlightResult.UnconfiguredMap;

        return ZLevelFlightResult.Success;
    }

    private bool TryGetActiveFlight(
        EntityUid uid,
        EntityUid gridUid,
        out ZLevelFlightComponent flight)
    {
        flight = default!;
        if (!_flightQuery.TryComp(uid, out var resolvedFlight) || !resolvedFlight.Active)
            return false;

        flight = resolvedFlight;

        if (flight.ActiveGridUid == null)
            flight.ActiveGridUid = gridUid;

        if (flight.ActiveGridUid == gridUid && IsValidFlightConfiguration(flight))
            return true;

        StopFlight(uid, flight, ZLevelFlightStopReason.InvalidState, dirty: true);
        return false;
    }

    private void StopFlight(
        EntityUid uid,
        ZLevelFlightComponent flight,
        ZLevelFlightStopReason reason,
        bool dirty)
    {
        if (!flight.Active)
            return;

        flight.Active = false;
        flight.ActiveGridUid = null;
        _activeFlights.Remove(uid);

        if (dirty && !TerminatingOrDeleted(uid))
            Dirty(uid, flight);

        if (_kinematicsQuery.TryComp(uid, out var kinematics))
        {
            kinematics.VerticalVelocity = 0f;
            if (dirty && !TerminatingOrDeleted(uid))
                Dirty(uid, kinematics);

            _activeBodies.Add(uid);
        }

        if (!TerminatingOrDeleted(uid))
            _gravity.RefreshWeightless(uid);

        _metrics.RecordFlightStopped(reason != ZLevelFlightStopReason.Requested);
        var stopped = new ZLevelFlightStoppedEvent(reason);
        RaiseLocalEvent(uid, ref stopped, true);
    }

    private void BlockFlightAtBoundary(
        EntityUid uid,
        ZLevelFlightComponent flight,
        ZLevelPositionComponent position,
        int direction)
    {
        flight.TargetLocalZLevel = position.ZLevel;
        flight.TargetLocalZOffset = position.LocalZOffset;
        Dirty(uid, flight);
        _metrics.RecordFlightBoundaryBlocked();

        var lowerZ = direction > 0 ? position.ZLevel : position.ZLevel - 1;
        var blocked = new ZLevelFlightBoundaryBlockedEvent(lowerZ, lowerZ + 1, direction);
        RaiseLocalEvent(uid, ref blocked, true);
    }

    private void OnFlightStartup(Entity<ZLevelFlightComponent> entity, ref ComponentStartup args)
    {
        SynchronizeFlightState(entity);
        var changed = new ZLevelFlightCapabilityChangedEvent(true);
        RaiseLocalEvent(entity.Owner, ref changed);
    }

    private void OnFlightShutdown(Entity<ZLevelFlightComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.CapabilityRemoved, dirty: false);
        else
            _activeFlights.Remove(entity.Owner);

        var changed = new ZLevelFlightCapabilityChangedEvent(false);
        RaiseLocalEvent(entity.Owner, ref changed);
    }

    private void OnFlightStateHandled(Entity<ZLevelFlightComponent> entity, ref AfterAutoHandleStateEvent args)
    {
        SynchronizeFlightState(entity);
    }

    private void SynchronizeFlightState(Entity<ZLevelFlightComponent> entity)
    {
        if (!entity.Comp.Active)
        {
            _activeFlights.Remove(entity.Owner);
            entity.Comp.ActiveGridUid = null;
            return;
        }

        if (!_transformQuery.TryComp(entity.Owner, out var transform) || transform.GridUid == null)
            return;

        entity.Comp.ActiveGridUid = transform.GridUid;
        _activeFlights.Add(entity.Owner);
        _activeBodies.Add(entity.Owner);
        _gravity.RefreshWeightless(entity.Owner);
    }

    private void OnFlightParentChanged(Entity<ZLevelFlightComponent> entity, ref EntParentChangedMessage args)
    {
        if (entity.Comp.Active && entity.Comp.ActiveGridUid != args.Transform.GridUid)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.GridChanged, dirty: true);
    }

    private void OnFlightAnchorChanged(Entity<ZLevelFlightComponent> entity, ref AnchorStateChangedEvent args)
    {
        if (entity.Comp.Active && args.Anchored)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Anchored, dirty: true);
    }

    private void OnFlightBodyTypeChanged(Entity<ZLevelFlightComponent> entity, ref PhysicsBodyTypeChangedEvent args)
    {
        if (entity.Comp.Active && args.New is BodyType.Static or BodyType.Kinematic)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.InvalidBodyType, dirty: true);
    }

    private void OnFlightContained(Entity<ZLevelFlightComponent> entity, ref EntInsertedIntoContainerMessage args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Contained, dirty: true);
    }

    private void OnFlightMobStateChanged(Entity<ZLevelFlightComponent> entity, ref MobStateChangedEvent args)
    {
        if (entity.Comp.Active && args.NewMobState != MobState.Alive)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Incapacitated, dirty: true);
    }

    private void OnFlightStunned(Entity<ZLevelFlightComponent> entity, ref StunnedEvent args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Stunned, dirty: true);
    }

    private void OnFlightKnockedDown(Entity<ZLevelFlightComponent> entity, ref KnockedDownEvent args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.KnockedDown, dirty: true);
    }

    private void OnFlightThrown(Entity<ZLevelFlightComponent> entity, ref ThrownEvent args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Thrown, dirty: true);
    }

    private void OnFlightBuckled(Entity<ZLevelFlightComponent> entity, ref BuckledEvent args)
    {
        if (entity.Comp.Active)
            StopFlight(entity.Owner, entity.Comp, ZLevelFlightStopReason.Buckled, dirty: true);
    }

    private void OnFlightWeightless(Entity<ZLevelFlightComponent> entity, ref IsWeightlessEvent args)
    {
        if (!entity.Comp.Active)
            return;

        args.IsWeightless = true;
        args.Handled = true;
    }

    private void OnFlightCanMove(Entity<ZLevelFlightComponent> entity, ref CanWeightlessMoveEvent args)
    {
        if (entity.Comp.Active)
            args.CanMove = true;
    }

    private void OnFlightMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        _flightBuffer.Clear();
        _flightBuffer.AddRange(_activeFlights);
        foreach (var uid in _flightBuffer)
        {
            if (!_flightQuery.TryComp(uid, out var flight) ||
                !_transformQuery.TryComp(uid, out var transform) ||
                transform.MapUid != args.MapUid)
            {
                continue;
            }

            if (transform.GridUid is { } gridUid &&
                _zLevelMaps.TryGetConfig(gridUid, out var config) &&
                IsValidTarget(config.Comp, flight.TargetLocalZLevel, flight.TargetLocalZOffset))
            {
                continue;
            }

            StopFlight(uid, flight, ZLevelFlightStopReason.MapConfigurationChanged, dirty: true);
        }

        _flightBuffer.Clear();
    }

    private static bool IsValidFlightConfiguration(ZLevelFlightComponent flight)
    {
        return float.IsFinite(flight.HoverOffset) &&
               flight.HoverOffset >= 0f && flight.HoverOffset < 1f &&
               float.IsFinite(flight.VerticalAcceleration) && flight.VerticalAcceleration > 0f &&
               float.IsFinite(flight.MaximumVerticalSpeed) && flight.MaximumVerticalSpeed > 0f;
    }

    private static bool IsValidTarget(ZLevelMapComponent config, int localZ, float localOffset)
    {
        return localZ >= config.MinimumLevel &&
               localZ <= config.MaximumLevel &&
               float.IsFinite(localOffset) &&
               localOffset >= 0f && localOffset < 1f;
    }

    private static float MoveTowards(float current, float target, float maximumDelta)
    {
        if (MathF.Abs(target - current) <= maximumDelta)
            return target;

        return current + MathF.CopySign(maximumDelta, target - current);
    }
}
