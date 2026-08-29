// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.ZLevel.Components;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.UserInterface;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Owns physical elevator cabin travel. The hierarchy remains two-dimensional;
/// the cabin and passengers move together by changing their local Z-level.
/// </summary>
public sealed class ZLevelElevatorSystem : EntitySystem
{
    public const int MaximumShaftIdLength = 64;
    public const int MaximumStopsPerNetwork = 64;
    public const int MaximumTravelLevels = 128;
    public const int MaximumPassengers = 128;
    public static readonly TimeSpan MaximumTravelTimePerLevel = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan MaximumTravelDuration = TimeSpan.FromMinutes(5);

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerReceiverSystem _power = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    private readonly Dictionary<EntityUid, ElevatorRegistration> _cabins = new();
    private readonly Dictionary<EntityUid, ElevatorRegistration> _stops = new();
    private readonly Dictionary<EntityUid, ElevatorRegistration> _controls = new();
    private readonly Dictionary<ElevatorNetworkKey, SortedSet<EntityUid>> _cabinsByNetwork = new();
    private readonly Dictionary<ElevatorNetworkKey, SortedDictionary<int, SortedSet<EntityUid>>> _stopsByNetwork = new();
    private readonly Dictionary<ElevatorNetworkKey, SortedSet<EntityUid>> _controlsByNetwork = new();
    private readonly Dictionary<EntityUid, PendingElevatorTravel> _pending = new();
    private readonly HashSet<EntityUid> _lookupBuffer = new();
    private readonly List<EntityUid> _passengerBuffer = new();
    private readonly List<EntityUid> _pendingBuffer = new();
    private readonly HashSet<EntityUid> _completing = new();

    private long _requests;
    private long _started;
    private long _completed;
    private long _cancelled;
    private long _rejected;
    private long _unpoweredRejections;
    private long _busyRejections;
    private long _passengersCaptured;
    private long _passengersMoved;

    public int ActiveTravelCount => _pending.Count;
    public int RegisteredCabinCount => _cabins.Count;
    public int RegisteredStopCount => _stops.Count;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelElevatorCabinComponent, ComponentStartup>(OnCabinStartup);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, ComponentShutdown>(OnCabinShutdown);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, MoveEvent>(OnCabinMoved);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, EntParentChangedMessage>(OnCabinParentChanged);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, AnchorStateChangedEvent>(OnCabinAnchorChanged);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, ZLevelPositionChangedEvent>(OnCabinZChanged);
        SubscribeLocalEvent<ZLevelElevatorCabinComponent, PowerChangedEvent>(OnCabinPowerChanged);

        SubscribeLocalEvent<ZLevelElevatorStopComponent, ComponentStartup>(OnStopStartup);
        SubscribeLocalEvent<ZLevelElevatorStopComponent, ComponentShutdown>(OnStopShutdown);
        SubscribeLocalEvent<ZLevelElevatorStopComponent, MoveEvent>(OnStopMoved);
        SubscribeLocalEvent<ZLevelElevatorStopComponent, EntParentChangedMessage>(OnStopParentChanged);
        SubscribeLocalEvent<ZLevelElevatorStopComponent, AnchorStateChangedEvent>(OnStopAnchorChanged);
        SubscribeLocalEvent<ZLevelElevatorStopComponent, ZLevelPositionChangedEvent>(OnStopZChanged);

        SubscribeLocalEvent<ZLevelElevatorControlComponent, ComponentStartup>(OnControlStartup);
        SubscribeLocalEvent<ZLevelElevatorControlComponent, ComponentShutdown>(OnControlShutdown);
        SubscribeLocalEvent<ZLevelElevatorControlComponent, MoveEvent>(OnControlMoved);
        SubscribeLocalEvent<ZLevelElevatorControlComponent, EntParentChangedMessage>(OnControlParentChanged);
        SubscribeLocalEvent<ZLevelElevatorControlComponent, AnchorStateChangedEvent>(OnControlAnchorChanged);
        SubscribeLocalEvent<ZLevelElevatorControlComponent, ZLevelPositionChangedEvent>(OnControlZChanged);

        Subs.BuiEvents<ZLevelElevatorControlComponent>(ZLevelElevatorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ZLevelElevatorRequestFloorMessage>(OnRequestFloor);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        _pendingBuffer.Clear();
        foreach (var (cabin, travel) in _pending)
        {
            if (_timing.CurTime >= travel.ArrivalTime)
                _pendingBuffer.Add(cabin);
        }

        foreach (var cabin in _pendingBuffer)
            CompleteTravel(cabin);
    }

    /// <summary>
    /// Requests a mapped floor through a cabin or landing control. A null user
    /// is reserved for trusted systems and integration tests.
    /// </summary>
    public ZLevelElevatorRequestResult TryRequestFloor(
        EntityUid control,
        int targetFloor,
        EntityUid? user = null)
    {
        _requests++;

        if (!_controls.TryGetValue(control, out var controlRegistration) ||
            !TryComp<ZLevelElevatorControlComponent>(control, out var controlComponent))
        {
            return Reject(ZLevelElevatorRequestResult.InvalidControl);
        }

        if (user is { } actor && !CanUseControl(actor, controlRegistration))
            return Reject(ZLevelElevatorRequestResult.InvalidUser);

        if (!TryResolveNetwork(
                controlRegistration.Key,
                out var cabin,
                out var cabinComponent,
                out var cabinRegistration,
                out var stops,
                out var networkFailure))
        {
            return Reject(networkFailure);
        }

        if (controlComponent.Mode == ZLevelElevatorControlMode.Landing &&
            targetFloor != controlRegistration.LocalZ)
        {
            return Reject(ZLevelElevatorRequestResult.InvalidTarget);
        }

        if (controlComponent.Mode == ZLevelElevatorControlMode.Cabin &&
            controlRegistration.LocalZ != cabinRegistration.LocalZ)
        {
            return Reject(ZLevelElevatorRequestResult.InvalidControl);
        }

        if (!TryGetUniqueStop(stops, targetFloor, out _))
            return Reject(ZLevelElevatorRequestResult.InvalidTarget);

        var sourceFloor = cabinRegistration.LocalZ;
        if (!TryGetUniqueStop(stops, sourceFloor, out _))
            return Reject(ZLevelElevatorRequestResult.InvalidNetwork);

        if (targetFloor == sourceFloor)
            return ZLevelElevatorRequestResult.AlreadyThere;

        if (_pending.ContainsKey(cabin))
        {
            _busyRejections++;
            return Reject(ZLevelElevatorRequestResult.Busy);
        }

        if (!IsPowered(cabin, cabinComponent))
        {
            _unpoweredRejections++;
            return Reject(ZLevelElevatorRequestResult.Unpowered);
        }

        var distance = Math.Abs((long) targetFloor - sourceFloor);
        if (cabinComponent.MaxTravelLevels is < 1 or > MaximumTravelLevels ||
            cabinComponent.PassengerLimit is < 1 or > MaximumPassengers)
        {
            return Reject(ZLevelElevatorRequestResult.InvalidConfiguration);
        }

        var maxTravelLevels = cabinComponent.MaxTravelLevels;
        if (distance == 0 || distance > maxTravelLevels)
            return Reject(ZLevelElevatorRequestResult.TooFar);

        if (cabinComponent.TravelTimePerLevel < TimeSpan.Zero ||
            cabinComponent.TravelTimePerLevel > MaximumTravelTimePerLevel ||
            !float.IsFinite(cabinComponent.IdlePowerDraw) ||
            cabinComponent.IdlePowerDraw < 0f ||
            !float.IsFinite(cabinComponent.TravelPowerDraw) ||
            cabinComponent.TravelPowerDraw < 0f)
        {
            return Reject(ZLevelElevatorRequestResult.InvalidConfiguration);
        }

        if (!TryComp<MapGridComponent>(cabinRegistration.Key.GridUid, out var grid) ||
            !_boundaries.IsStackOpen(
                cabinRegistration.Key.GridUid,
                grid,
                cabinRegistration.Key.Tile,
                sourceFloor,
                targetFloor,
                targetFloor > sourceFloor
                    ? ZLevelBoundaryChannels.TraversalUp
                    : ZLevelBoundaryChannels.TraversalDown))
        {
            return Reject(ZLevelElevatorRequestResult.ClosedShaft);
        }

        if (!TryCapturePassengers(cabin, cabinRegistration, cabinComponent.PassengerLimit, out var passengers))
            return Reject(ZLevelElevatorRequestResult.OverCapacity);

        var durationTicks = Math.Min(
            MaximumTravelDuration.Ticks,
            cabinComponent.TravelTimePerLevel.Ticks * distance);
        var duration = TimeSpan.FromTicks(durationTicks);
        var arrival = _timing.CurTime + duration;
        var pending = new PendingElevatorTravel(
            cabinRegistration.Key,
            sourceFloor,
            targetFloor,
            arrival,
            duration,
            passengers);

        cabinComponent.State = ZLevelElevatorState.Moving;
        cabinComponent.TargetLevel = targetFloor;
        cabinComponent.ArrivalTime = arrival;
        _pending.Add(cabin, pending);
        _started++;
        _passengersCaptured += passengers.Count;
        _power.SetLoad(cabin, cabinComponent.TravelPowerDraw);
        UpdateNetwork(cabinRegistration.Key);

        if (duration <= TimeSpan.Zero)
            CompleteTravel(cabin);

        return ZLevelElevatorRequestResult.Started;
    }

    public ZLevelElevatorMetricsSnapshot Snapshot()
    {
        return new ZLevelElevatorMetricsSnapshot(
            _cabins.Count,
            _stops.Count,
            _pending.Count,
            _requests,
            _started,
            _completed,
            _cancelled,
            _rejected,
            _unpoweredRejections,
            _busyRejections,
            _passengersCaptured,
            _passengersMoved);
    }

    public void ResetMetrics()
    {
        _requests = 0;
        _started = 0;
        _completed = 0;
        _cancelled = 0;
        _rejected = 0;
        _unpoweredRejections = 0;
        _busyRejections = 0;
        _passengersCaptured = 0;
        _passengersMoved = 0;
    }

    private void OnUiOpened(Entity<ZLevelElevatorControlComponent> entity, ref BoundUIOpenedEvent args)
    {
        UpdateControlUi(entity.Owner);
    }

    private void OnRequestFloor(
        Entity<ZLevelElevatorControlComponent> entity,
        ref ZLevelElevatorRequestFloorMessage args)
    {
        var result = TryRequestFloor(entity.Owner, args.TargetFloor, args.Actor);
        if (result is not (ZLevelElevatorRequestResult.Started or ZLevelElevatorRequestResult.AlreadyThere))
        {
            _popup.PopupEntity(
                Loc.GetString(GetFailureLocId(result)),
                entity.Owner,
                args.Actor);
        }

        UpdateControlUi(entity.Owner);
    }

    private void OnCabinStartup(Entity<ZLevelElevatorCabinComponent> entity, ref ComponentStartup args)
    {
        entity.Comp.State = ZLevelElevatorState.Idle;
        entity.Comp.TargetLevel = null;
        entity.Comp.ArrivalTime = TimeSpan.Zero;
        _power.SetLoad(entity.Owner, Math.Max(0f, entity.Comp.IdlePowerDraw));
        RefreshCabin(entity.Owner, entity.Comp);
    }

    private void OnCabinShutdown(Entity<ZLevelElevatorCabinComponent> entity, ref ComponentShutdown args)
    {
        CancelTravel(entity.Owner, updateComponent: false);
        RemoveCabin(entity.Owner);
    }

    private void OnCabinMoved(Entity<ZLevelElevatorCabinComponent> entity, ref MoveEvent args)
    {
        RefreshCabinAfterExternalMove(entity);
    }

    private void OnCabinParentChanged(
        Entity<ZLevelElevatorCabinComponent> entity,
        ref EntParentChangedMessage args)
    {
        RefreshCabinAfterExternalMove(entity);
    }

    private void OnCabinAnchorChanged(
        Entity<ZLevelElevatorCabinComponent> entity,
        ref AnchorStateChangedEvent args)
    {
        RefreshCabinAfterExternalMove(entity);
    }

    private void OnCabinZChanged(
        Entity<ZLevelElevatorCabinComponent> entity,
        ref ZLevelPositionChangedEvent args)
    {
        RefreshCabinAfterExternalMove(entity);
    }

    private void OnCabinPowerChanged(
        Entity<ZLevelElevatorCabinComponent> entity,
        ref PowerChangedEvent args)
    {
        if (entity.Comp.RequirePower && !args.Powered)
            CancelTravel(entity.Owner);

        if (_cabins.TryGetValue(entity.Owner, out var registration))
            UpdateNetwork(registration.Key);
    }

    private void OnStopStartup(Entity<ZLevelElevatorStopComponent> entity, ref ComponentStartup args)
    {
        RefreshStop(entity.Owner, entity.Comp);
    }

    private void OnStopShutdown(Entity<ZLevelElevatorStopComponent> entity, ref ComponentShutdown args)
    {
        RemoveStop(entity.Owner);
    }

    private void OnStopMoved(Entity<ZLevelElevatorStopComponent> entity, ref MoveEvent args)
    {
        RefreshStop(entity.Owner, entity.Comp);
    }

    private void OnStopParentChanged(Entity<ZLevelElevatorStopComponent> entity, ref EntParentChangedMessage args)
    {
        RefreshStop(entity.Owner, entity.Comp);
    }

    private void OnStopAnchorChanged(Entity<ZLevelElevatorStopComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshStop(entity.Owner, entity.Comp);
    }

    private void OnStopZChanged(Entity<ZLevelElevatorStopComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        RefreshStop(entity.Owner, entity.Comp);
    }

    private void OnControlStartup(Entity<ZLevelElevatorControlComponent> entity, ref ComponentStartup args)
    {
        RefreshControl(entity.Owner, entity.Comp);
    }

    private void OnControlShutdown(Entity<ZLevelElevatorControlComponent> entity, ref ComponentShutdown args)
    {
        RemoveControl(entity.Owner);
    }

    private void OnControlMoved(Entity<ZLevelElevatorControlComponent> entity, ref MoveEvent args)
    {
        RefreshControl(entity.Owner, entity.Comp);
    }

    private void OnControlParentChanged(
        Entity<ZLevelElevatorControlComponent> entity,
        ref EntParentChangedMessage args)
    {
        RefreshControl(entity.Owner, entity.Comp);
    }

    private void OnControlAnchorChanged(
        Entity<ZLevelElevatorControlComponent> entity,
        ref AnchorStateChangedEvent args)
    {
        RefreshControl(entity.Owner, entity.Comp);
    }

    private void OnControlZChanged(
        Entity<ZLevelElevatorControlComponent> entity,
        ref ZLevelPositionChangedEvent args)
    {
        RefreshControl(entity.Owner, entity.Comp);
    }

    private void RefreshCabinAfterExternalMove(Entity<ZLevelElevatorCabinComponent> entity)
    {
        if (!_completing.Contains(entity.Owner))
            CancelTravel(entity.Owner);

        RefreshCabin(entity.Owner, entity.Comp);
    }

    private void RefreshCabin(EntityUid uid, ZLevelElevatorCabinComponent component)
    {
        _cabins.TryGetValue(uid, out var oldRegistration);
        var hadOld = _cabins.ContainsKey(uid);
        var hasNew = TryCreateRegistration(uid, component.ShaftId, out var newRegistration);

        if (hadOld && hasNew && oldRegistration == newRegistration)
            return;

        if (hadOld)
            RemoveCabinFromNetwork(uid, oldRegistration.Key);

        if (hasNew)
        {
            _cabins[uid] = newRegistration;
            if (!_cabinsByNetwork.TryGetValue(newRegistration.Key, out var cabins))
            {
                cabins = new SortedSet<EntityUid>();
                _cabinsByNetwork.Add(newRegistration.Key, cabins);
            }

            cabins.Add(uid);
        }
        else
        {
            _cabins.Remove(uid);
        }

        if (hadOld)
            UpdateNetwork(oldRegistration.Key);
        if (hasNew && (!hadOld || oldRegistration.Key != newRegistration.Key))
            UpdateNetwork(newRegistration.Key);
    }

    private void RemoveCabin(EntityUid uid)
    {
        if (!_cabins.Remove(uid, out var registration))
            return;

        RemoveCabinFromNetwork(uid, registration.Key);
        UpdateNetwork(registration.Key);
    }

    private void RemoveCabinFromNetwork(EntityUid uid, ElevatorNetworkKey key)
    {
        if (!_cabinsByNetwork.TryGetValue(key, out var cabins))
            return;

        cabins.Remove(uid);
        if (cabins.Count == 0)
            _cabinsByNetwork.Remove(key);
    }

    private void RefreshStop(EntityUid uid, ZLevelElevatorStopComponent component)
    {
        _stops.TryGetValue(uid, out var oldRegistration);
        var hadOld = _stops.ContainsKey(uid);
        var hasNew = TryCreateRegistration(uid, component.ShaftId, out var newRegistration);

        if (hadOld && hasNew && oldRegistration == newRegistration)
            return;

        if (hadOld)
            RemoveStopFromNetwork(uid, oldRegistration);

        if (hasNew)
        {
            _stops[uid] = newRegistration;
            if (!_stopsByNetwork.TryGetValue(newRegistration.Key, out var stops))
            {
                stops = new SortedDictionary<int, SortedSet<EntityUid>>();
                _stopsByNetwork.Add(newRegistration.Key, stops);
            }

            if (!stops.TryGetValue(newRegistration.LocalZ, out var entities))
            {
                entities = new SortedSet<EntityUid>();
                stops.Add(newRegistration.LocalZ, entities);
            }

            entities.Add(uid);
        }
        else
        {
            _stops.Remove(uid);
        }

        if (hadOld)
        {
            ValidatePendingNetwork(oldRegistration.Key);
            UpdateNetwork(oldRegistration.Key);
        }

        if (hasNew && (!hadOld || oldRegistration.Key != newRegistration.Key ||
            oldRegistration.LocalZ != newRegistration.LocalZ))
        {
            ValidatePendingNetwork(newRegistration.Key);
            UpdateNetwork(newRegistration.Key);
        }
    }

    private void RemoveStop(EntityUid uid)
    {
        if (!_stops.Remove(uid, out var registration))
            return;

        RemoveStopFromNetwork(uid, registration);
        ValidatePendingNetwork(registration.Key);
        UpdateNetwork(registration.Key);
    }

    private void RemoveStopFromNetwork(EntityUid uid, ElevatorRegistration registration)
    {
        if (!_stopsByNetwork.TryGetValue(registration.Key, out var stops) ||
            !stops.TryGetValue(registration.LocalZ, out var entities))
        {
            return;
        }

        entities.Remove(uid);
        if (entities.Count == 0)
            stops.Remove(registration.LocalZ);
        if (stops.Count == 0)
            _stopsByNetwork.Remove(registration.Key);
    }

    private void RefreshControl(EntityUid uid, ZLevelElevatorControlComponent component)
    {
        _controls.TryGetValue(uid, out var oldRegistration);
        var hadOld = _controls.ContainsKey(uid);
        var shaftId = component.Mode switch
        {
            ZLevelElevatorControlMode.Cabin when TryComp<ZLevelElevatorCabinComponent>(uid, out var cabin) =>
                cabin.ShaftId,
            ZLevelElevatorControlMode.Landing when TryComp<ZLevelElevatorStopComponent>(uid, out var stop) =>
                stop.ShaftId,
            _ => string.Empty,
        };
        var hasNew = TryCreateRegistration(uid, shaftId, out var newRegistration);

        if (hadOld && hasNew && oldRegistration == newRegistration)
            return;

        if (hadOld)
            RemoveControlFromNetwork(uid, oldRegistration.Key);

        if (hasNew)
        {
            _controls[uid] = newRegistration;
            if (!_controlsByNetwork.TryGetValue(newRegistration.Key, out var controls))
            {
                controls = new SortedSet<EntityUid>();
                _controlsByNetwork.Add(newRegistration.Key, controls);
            }

            controls.Add(uid);
        }
        else
        {
            _controls.Remove(uid);
        }

        if (hadOld)
            UpdateNetwork(oldRegistration.Key);
        if (hasNew && (!hadOld || oldRegistration.Key != newRegistration.Key))
            UpdateNetwork(newRegistration.Key);
    }

    private void RemoveControl(EntityUid uid)
    {
        if (!_controls.Remove(uid, out var registration))
            return;

        RemoveControlFromNetwork(uid, registration.Key);
        UpdateNetwork(registration.Key);
    }

    private void RemoveControlFromNetwork(EntityUid uid, ElevatorNetworkKey key)
    {
        if (!_controlsByNetwork.TryGetValue(key, out var controls))
            return;

        controls.Remove(uid);
        if (controls.Count == 0)
            _controlsByNetwork.Remove(key);
    }

    private bool TryCreateRegistration(EntityUid uid, string shaftId, out ElevatorRegistration registration)
    {
        registration = default;
        if (string.IsNullOrWhiteSpace(shaftId) ||
            shaftId.Length > MaximumShaftIdLength ||
            !TryComp(uid, out TransformComponent? transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            transform.MapID == MapId.Nullspace ||
            !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            return false;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var localZ = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
        registration = new ElevatorRegistration(
            new ElevatorNetworkKey(gridUid, tile, shaftId.Trim()),
            localZ);
        return true;
    }

    private bool TryResolveNetwork(
        ElevatorNetworkKey key,
        out EntityUid cabin,
        out ZLevelElevatorCabinComponent cabinComponent,
        out ElevatorRegistration cabinRegistration,
        out SortedDictionary<int, SortedSet<EntityUid>> stops,
        out ZLevelElevatorRequestResult failure)
    {
        cabin = default;
        cabinComponent = default!;
        cabinRegistration = default;
        stops = default!;
        failure = ZLevelElevatorRequestResult.InvalidNetwork;

        if (!_cabinsByNetwork.TryGetValue(key, out var cabins) || cabins.Count != 1)
        {
            failure = cabins is { Count: > 1 }
                ? ZLevelElevatorRequestResult.DuplicateCabin
                : ZLevelElevatorRequestResult.InvalidNetwork;
            return false;
        }

        cabin = cabins.First();
        if (!TryComp<ZLevelElevatorCabinComponent>(cabin, out var resolvedComponent) ||
            !_cabins.TryGetValue(cabin, out cabinRegistration))
            return false;
        cabinComponent = resolvedComponent;

        if (!_stopsByNetwork.TryGetValue(key, out var resolvedStops) ||
            resolvedStops.Count < 2 ||
            resolvedStops.Count > MaximumStopsPerNetwork)
        {
            return false;
        }
        stops = resolvedStops;

        foreach (var entities in stops.Values)
        {
            if (entities.Count != 1)
            {
                failure = ZLevelElevatorRequestResult.DuplicateStop;
                return false;
            }
        }

        return true;
    }

    private static bool TryGetUniqueStop(
        SortedDictionary<int, SortedSet<EntityUid>> stops,
        int floor,
        out EntityUid stop)
    {
        stop = default;
        if (!stops.TryGetValue(floor, out var entities) || entities.Count != 1)
            return false;

        stop = entities.First();
        return true;
    }

    private bool CanUseControl(EntityUid user, ElevatorRegistration control)
    {
        if (!TryComp(user, out TransformComponent? userTransform) ||
            userTransform.MapID == MapId.Nullspace ||
            userTransform.GridUid != control.Key.GridUid)
        {
            return false;
        }

        return _zLevels.GetZLevel(user) == control.LocalZ;
    }

    private bool IsPowered(EntityUid cabin, ZLevelElevatorCabinComponent component)
    {
        return !component.RequirePower ||
               TryComp<ApcPowerReceiverComponent>(cabin, out var receiver) && receiver.Powered;
    }

    private bool TryCapturePassengers(
        EntityUid cabin,
        ElevatorRegistration registration,
        int passengerLimit,
        out List<EntityUid> passengers)
    {
        _lookupBuffer.Clear();
        _passengerBuffer.Clear();
        _lookup.GetEntitiesIntersecting(
            cabin,
            _lookupBuffer,
            LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Sensors);

        if (!TryComp<MapGridComponent>(registration.Key.GridUid, out var grid))
        {
            passengers = default!;
            return false;
        }

        foreach (var uid in _lookupBuffer)
        {
            if (uid == cabin ||
                !TryComp(uid, out TransformComponent? transform) ||
                transform.Anchored ||
                transform.GridUid != registration.Key.GridUid ||
                _zLevels.GetZLevel(uid) != registration.LocalZ ||
                _map.TileIndicesFor(registration.Key.GridUid, grid, transform.Coordinates) != registration.Key.Tile ||
                !HasComp<PhysicsComponent>(uid))
            {
                continue;
            }

            _passengerBuffer.Add(uid);
            if (_passengerBuffer.Count > passengerLimit)
            {
                passengers = default!;
                return false;
            }
        }

        _passengerBuffer.Sort();
        passengers = new List<EntityUid>(_passengerBuffer);
        return true;
    }

    private void CompleteTravel(EntityUid cabin)
    {
        if (!_pending.Remove(cabin, out var travel) ||
            !TryComp<ZLevelElevatorCabinComponent>(cabin, out var component) ||
            !_cabins.TryGetValue(cabin, out var registration))
        {
            return;
        }

        if (registration.Key != travel.Key ||
            registration.LocalZ != travel.SourceFloor ||
            !IsPowered(cabin, component) ||
            !TryResolveNetwork(
                travel.Key,
                out var resolvedCabin,
                out _,
                out _,
                out var stops,
                out _) ||
            resolvedCabin != cabin ||
            !TryGetUniqueStop(stops, travel.TargetFloor, out _) ||
            !TryComp<MapGridComponent>(travel.Key.GridUid, out var grid) ||
            !_boundaries.IsStackOpen(
                travel.Key.GridUid,
                grid,
                travel.Key.Tile,
                travel.SourceFloor,
                travel.TargetFloor,
                travel.TargetFloor > travel.SourceFloor
                    ? ZLevelBoundaryChannels.TraversalUp
                    : ZLevelBoundaryChannels.TraversalDown))
        {
            FinishCancelledTravel(cabin, component, travel.Key);
            return;
        }

        _completing.Add(cabin);
        try
        {
            if (!_zLevels.SetZLevelPosition(cabin, travel.TargetFloor))
            {
                FinishCancelledTravel(cabin, component, travel.Key);
                return;
            }

            var moved = 0;
            foreach (var passenger in travel.Passengers)
            {
                if (!IsPassengerStillAboard(passenger, travel))
                    continue;

                if (_zLevels.SetZLevel(passenger, travel.TargetFloor) ||
                    _zLevels.SetZLevelPosition(passenger, travel.TargetFloor))
                {
                    moved++;
                    _popup.PopupEntity(
                        Loc.GetString("zlevel-elevator-arrived", ("z", travel.TargetFloor)),
                        passenger,
                        passenger);
                }
            }

            component.State = ZLevelElevatorState.Idle;
            component.TargetLevel = null;
            component.ArrivalTime = TimeSpan.Zero;
            _power.SetLoad(cabin, Math.Max(0f, component.IdlePowerDraw));
            _completed++;
            _passengersMoved += moved;
        }
        finally
        {
            _completing.Remove(cabin);
        }

        UpdateNetwork(travel.Key);
    }

    private bool IsPassengerStillAboard(EntityUid passenger, PendingElevatorTravel travel)
    {
        if (!Exists(passenger) ||
            !TryComp(passenger, out TransformComponent? transform) ||
            transform.Anchored ||
            transform.GridUid != travel.Key.GridUid ||
            _zLevels.GetZLevel(passenger) != travel.SourceFloor ||
            !TryComp<MapGridComponent>(travel.Key.GridUid, out var grid))
        {
            return false;
        }

        return _map.TileIndicesFor(travel.Key.GridUid, grid, transform.Coordinates) == travel.Key.Tile;
    }

    private void ValidatePendingNetwork(ElevatorNetworkKey key)
    {
        _pendingBuffer.Clear();
        foreach (var (cabin, travel) in _pending)
        {
            if (travel.Key == key &&
                (!TryResolveNetwork(key, out var resolved, out _, out _, out var stops, out _) ||
                 resolved != cabin ||
                 !TryGetUniqueStop(stops, travel.SourceFloor, out _) ||
                 !TryGetUniqueStop(stops, travel.TargetFloor, out _)))
            {
                _pendingBuffer.Add(cabin);
            }
        }

        foreach (var cabin in _pendingBuffer)
            CancelTravel(cabin);
    }

    private void CancelTravel(EntityUid cabin, bool updateComponent = true)
    {
        if (!_pending.Remove(cabin, out var travel))
            return;

        if (updateComponent && TryComp<ZLevelElevatorCabinComponent>(cabin, out var component))
        {
            component.State = ZLevelElevatorState.Idle;
            component.TargetLevel = null;
            component.ArrivalTime = TimeSpan.Zero;
            _power.SetLoad(cabin, Math.Max(0f, component.IdlePowerDraw));
        }

        _cancelled++;
        UpdateNetwork(travel.Key);
    }

    private void FinishCancelledTravel(
        EntityUid cabin,
        ZLevelElevatorCabinComponent component,
        ElevatorNetworkKey key)
    {
        component.State = ZLevelElevatorState.Idle;
        component.TargetLevel = null;
        component.ArrivalTime = TimeSpan.Zero;
        _power.SetLoad(cabin, Math.Max(0f, component.IdlePowerDraw));
        _cancelled++;
        UpdateNetwork(key);
    }

    private void UpdateNetwork(ElevatorNetworkKey key)
    {
        if (_controlsByNetwork.TryGetValue(key, out var controls))
        {
            foreach (var control in controls)
            {
                UpdateControlUi(control);
                UpdateAppearance(control, key);
            }
        }

        if (_cabinsByNetwork.TryGetValue(key, out var cabins))
        {
            foreach (var cabin in cabins)
            {
                if (!_controls.ContainsKey(cabin))
                    UpdateAppearance(cabin, key);
            }
        }
    }

    private void UpdateControlUi(EntityUid control)
    {
        if (!_controls.TryGetValue(control, out var registration) ||
            !TryComp<ZLevelElevatorControlComponent>(control, out var controlComponent))
        {
            return;
        }

        var stopData = new List<ZLevelElevatorStopData>();
        int? currentFloor = null;
        int? targetFloor = null;
        var state = ZLevelElevatorState.Invalid;
        var arrival = TimeSpan.Zero;
        var duration = TimeSpan.Zero;

        if (TryResolveNetwork(
                registration.Key,
                out var cabin,
                out var cabinComponent,
                out var cabinRegistration,
                out var stops,
                out _))
        {
            currentFloor = cabinRegistration.LocalZ;
            targetFloor = cabinComponent.TargetLevel;
            arrival = cabinComponent.ArrivalTime;
            if (_pending.TryGetValue(cabin, out var travel))
                duration = travel.Duration;

            state = !IsPowered(cabin, cabinComponent)
                ? ZLevelElevatorState.Unpowered
                : cabinComponent.State;

            foreach (var (floor, entities) in stops.Reverse())
            {
                var stop = entities.First();
                var label = TryComp<ZLevelElevatorStopComponent>(stop, out var stopComponent) &&
                            !string.IsNullOrWhiteSpace(stopComponent.Label)
                    ? stopComponent.Label
                    : Loc.GetString("zlevel-elevator-floor", ("z", floor));
                stopData.Add(new ZLevelElevatorStopData(floor, label));
            }
        }

        _ui.SetUiState(
            control,
            ZLevelElevatorUiKey.Key,
            new ZLevelElevatorBoundUserInterfaceState(
                controlComponent.Mode,
                registration.LocalZ,
                currentFloor,
                targetFloor,
                state,
                arrival,
                duration,
                stopData));
    }

    private void UpdateAppearance(EntityUid uid, ElevatorNetworkKey key)
    {
        var state = ZLevelElevatorState.Invalid;
        if (TryResolveNetwork(
                key,
                out var cabin,
                out var cabinComponent,
                out _,
                out _,
                out _))
        {
            state = !IsPowered(cabin, cabinComponent)
                ? ZLevelElevatorState.Unpowered
                : cabinComponent.State;
        }

        _appearance.SetData(uid, ZLevelElevatorVisuals.State, state);
    }

    private ZLevelElevatorRequestResult Reject(ZLevelElevatorRequestResult result)
    {
        _rejected++;
        return result;
    }

    private static string GetFailureLocId(ZLevelElevatorRequestResult result)
    {
        return result switch
        {
            ZLevelElevatorRequestResult.Busy => "zlevel-elevator-failed-busy",
            ZLevelElevatorRequestResult.Unpowered => "zlevel-elevator-failed-unpowered",
            ZLevelElevatorRequestResult.ClosedShaft => "zlevel-elevator-failed-closed-shaft",
            ZLevelElevatorRequestResult.OverCapacity => "zlevel-elevator-failed-over-capacity",
            ZLevelElevatorRequestResult.InvalidTarget => "zlevel-elevator-failed-invalid-target",
            ZLevelElevatorRequestResult.TooFar => "zlevel-elevator-failed-too-far",
            _ => "zlevel-elevator-failed-invalid",
        };
    }

    private readonly record struct ElevatorNetworkKey(EntityUid GridUid, Vector2i Tile, string ShaftId);

    private readonly record struct ElevatorRegistration(ElevatorNetworkKey Key, int LocalZ);

    private sealed record PendingElevatorTravel(
        ElevatorNetworkKey Key,
        int SourceFloor,
        int TargetFloor,
        TimeSpan ArrivalTime,
        TimeSpan Duration,
        List<EntityUid> Passengers);
}

public enum ZLevelElevatorRequestResult : byte
{
    Started,
    AlreadyThere,
    InvalidControl,
    InvalidUser,
    InvalidNetwork,
    DuplicateCabin,
    DuplicateStop,
    InvalidTarget,
    Busy,
    Unpowered,
    TooFar,
    ClosedShaft,
    OverCapacity,
    InvalidConfiguration,
}

public readonly record struct ZLevelElevatorMetricsSnapshot(
    int Cabins,
    int Stops,
    int ActiveTravels,
    long Requests,
    long Started,
    long Completed,
    long Cancelled,
    long Rejected,
    long UnpoweredRejections,
    long BusyRejections,
    long PassengersCaptured,
    long PassengersMoved);
