using Content.Shared.Actions;
using Content.Shared.Gravity;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Systems;

public abstract class SharedJetpackSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    [Dependency] private readonly EntityQuery<JetpackComponent> _jetpackQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JetpackComponent, GetItemActionsEvent>(OnJetpackGetAction);
        SubscribeLocalEvent<JetpackComponent, DroppedEvent>(OnJetpackDropped);
        SubscribeLocalEvent<JetpackComponent, ToggleJetpackEvent>(OnJetpackToggle);

        SubscribeLocalEvent<JetpackUserComponent, RefreshWeightlessModifiersEvent>(OnJetpackUserWeightlessMovement);
        SubscribeLocalEvent<JetpackUserComponent, CanWeightlessMoveEvent>(OnJetpackUserCanWeightless);
        SubscribeLocalEvent<JetpackUserComponent, EntParentChangedMessage>(OnJetpackUserEntParentChanged);
        SubscribeLocalEvent<JetpackComponent, EntGotInsertedIntoContainerMessage>(OnJetpackMoved);

        SubscribeLocalEvent<GravityChangedEvent>(OnJetpackUserGravityChanged);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnZLevelMapConfigurationChanged);
        SubscribeLocalEvent<JetpackComponent, MapInitEvent>(OnMapInit);
    }

    private void OnJetpackUserWeightlessMovement(Entity<JetpackUserComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        // Yes this bulldozes the values but primarily for backwards compat atm.
        args.WeightlessAcceleration = ent.Comp.WeightlessAcceleration;
        args.WeightlessModifier = ent.Comp.WeightlessModifier;
        args.WeightlessFriction = ent.Comp.WeightlessFriction;
        args.WeightlessFrictionNoInput = ent.Comp.WeightlessFrictionNoInput;
    }

    private void OnMapInit(EntityUid uid, JetpackComponent component, MapInitEvent args)
    {
        _actionContainer.EnsureAction(uid, ref component.ToggleActionEntity, component.ToggleAction);
        Dirty(uid, component);
    }

    private void OnJetpackUserGravityChanged(ref GravityChangedEvent ev)
    {
        var gridUid = ev.ChangedGridIndex;
        var query = EntityQueryEnumerator<JetpackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var user, out var transform))
        {
            if (transform.GridUid == gridUid && ev.HasGravity && !CanEnableOnGrid(gridUid) &&
                _jetpackQuery.TryGetComponent(user.Jetpack, out var jetpack))
            {
                _popup.PopupClient(Loc.GetString("jetpack-to-grid"), uid, uid);

                SetEnabled(user.Jetpack, jetpack, false, uid);
            }
        }
    }

    private void OnJetpackDropped(EntityUid uid, JetpackComponent component, DroppedEvent args)
    {
        SetEnabled(uid, component, false, args.User);
    }

    private void OnJetpackMoved(Entity<JetpackComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.Owner != ent.Comp.JetpackUser)
            SetEnabled(ent, ent.Comp, false, ent.Comp.JetpackUser);
    }

    private void OnJetpackUserCanWeightless(EntityUid uid, JetpackUserComponent component, ref CanWeightlessMoveEvent args)
    {
        args.CanMove = true;
    }

    private void OnJetpackUserEntParentChanged(EntityUid uid, JetpackUserComponent component, ref EntParentChangedMessage args)
    {
        if (TryComp<JetpackComponent>(component.Jetpack, out var jetpack) &&
            !CanEnableOnGrid(args.Transform.GridUid))
        {
            SetEnabled(component.Jetpack, jetpack, false, uid);

            _popup.PopupClient(Loc.GetString("jetpack-to-grid"), uid, uid);
        }
    }

    private bool SetupUser(EntityUid user, EntityUid jetpackUid, JetpackComponent component)
    {
        EnsureComp<JetpackUserComponent>(user, out var userComp);
        component.JetpackUser = user;

        if (TryComp<PhysicsComponent>(user, out var physics))
            _physics.SetBodyStatus(user, physics, BodyStatus.InAir);

        userComp.Jetpack = jetpackUid;
        userComp.WeightlessAcceleration = component.Acceleration;
        userComp.WeightlessModifier = component.WeightlessModifier;
        userComp.WeightlessFriction = component.Friction;
        userComp.WeightlessFrictionNoInput = component.Friction;

        if (!TrySetupZLevelFlight(user, userComp))
        {
            component.JetpackUser = null;
            RemComp<JetpackUserComponent>(user);

            if (physics != null)
                _physics.SetBodyStatus(user, physics, BodyStatus.OnGround);

            _movementSpeedModifier.RefreshWeightlessModifiers(user);
            return false;
        }

        Dirty(user, userComp);
        _movementSpeedModifier.RefreshWeightlessModifiers(user);
        return true;
    }

    private void RemoveUser(EntityUid uid, JetpackComponent component)
    {
        if (!TryComp<JetpackUserComponent>(uid, out var userComp))
            return;

        TeardownZLevelFlight(uid, userComp);
        RemComp<JetpackUserComponent>(uid);
        component.JetpackUser = null;

        if (TryComp<PhysicsComponent>(uid, out var physics))
        {
            _physics.SetBodyStatus(
                uid,
                physics,
                _zLevels.IsFlying(uid) ? BodyStatus.InAir : BodyStatus.OnGround);
        }

        _movementSpeedModifier.RefreshWeightlessModifiers(uid);
    }

    private void OnJetpackToggle(EntityUid uid, JetpackComponent component, ToggleJetpackEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp(uid, out TransformComponent? xform) && !CanEnableOnGrid(xform.GridUid))
        {
            _popup.PopupClient(Loc.GetString("jetpack-no-station"), uid, args.Performer);

            return;
        }

        SetEnabled(uid, component, !IsEnabled(uid));
    }

    private bool CanEnableOnGrid(EntityUid? gridUid)
    {
        // No and no again! Do not attempt to activate the jetpack on a grid with gravity disabled. You will not be the first or the last to try this.
        // https://discord.com/channels/310555209753690112/310555209753690112/1270067921682694234
        return gridUid == null ||
               !HasComp<GravityComponent>(gridUid) ||
               _zLevelMaps.TryGetConfig(gridUid.Value, out _);
    }

    private void OnJetpackGetAction(EntityUid uid, JetpackComponent component, GetItemActionsEvent args)
    {
        args.AddAction(ref component.ToggleActionEntity, component.ToggleAction);
    }

    private bool IsEnabled(EntityUid uid)
    {
        return HasComp<ActiveJetpackComponent>(uid);
    }

    public void SetEnabled(EntityUid uid, JetpackComponent component, bool enabled, EntityUid? user = null)
    {
        if (IsEnabled(uid) == enabled ||
            enabled && !CanEnable(uid, component))
            return;

        if (user == null)
        {
            if (!Container.TryGetContainingContainer((uid, null, null), out var container))
                return;
            user = container.Owner;
        }

        if (enabled &&
            TryComp(user.Value, out TransformComponent? userTransform) &&
            !CanEnableOnGrid(userTransform.GridUid))
        {
            return;
        }

        if (enabled)
        {
            // If the user is already using another jetpack, disable it first
            if (TryComp<JetpackUserComponent>(user, out var userComp) &&
                userComp.Jetpack != uid &&
                TryComp<JetpackComponent>(userComp.Jetpack, out var oldJetpack))
            {
                SetEnabled(userComp.Jetpack, oldJetpack, false, user);
            }

            if (!SetupUser(user.Value, uid, component))
                return;

            EnsureComp<ActiveJetpackComponent>(uid);
        }
        else
        {
            RemoveUser(user.Value, component);
            RemComp<ActiveJetpackComponent>(uid);
        }


        Appearance.SetData(uid, JetpackVisuals.Enabled, enabled);
        Dirty(uid, component);
    }

    public bool IsUserFlying(EntityUid uid)
    {
        return HasComp<JetpackUserComponent>(uid);
    }

    protected virtual bool CanEnable(EntityUid uid, JetpackComponent component)
    {
        return true;
    }

    private bool TrySetupZLevelFlight(EntityUid user, JetpackUserComponent userComp)
    {
        if (!TryComp(user, out TransformComponent? transform) ||
            transform.GridUid is not { } gridUid ||
            !_zLevelMaps.TryGetConfig(gridUid, out _))
        {
            return true;
        }

        ZLevelFlightComponent flight;
        if (TryComp<ZLevelFlightComponent>(user, out var existingFlight))
        {
            flight = existingFlight;
        }
        else
        {
            flight = EnsureComp<ZLevelFlightComponent>(user);
            userComp.GrantedZLevelFlight = true;
        }

        if (!HasComp<ZLevelFlightControlsComponent>(user))
        {
            EnsureComp<ZLevelFlightControlsComponent>(user);
            userComp.GrantedZLevelFlightControls = true;
        }

        var result = flight.Active
            ? ZLevelFlightResult.AlreadyActive
            : _zLevels.TryStartFlight(user, flight: flight);
        if (result is ZLevelFlightResult.Success or ZLevelFlightResult.AlreadyActive)
        {
            userComp.StartedZLevelFlight = result == ZLevelFlightResult.Success;
            return true;
        }

        if (userComp.GrantedZLevelFlightControls)
            RemComp<ZLevelFlightControlsComponent>(user);
        if (userComp.GrantedZLevelFlight)
            RemComp<ZLevelFlightComponent>(user);
        userComp.GrantedZLevelFlightControls = false;
        userComp.GrantedZLevelFlight = false;
        return false;
    }

    private void TeardownZLevelFlight(EntityUid user, JetpackUserComponent userComp)
    {
        if (userComp.StartedZLevelFlight)
        {
            _zLevels.TryStopFlight(
                user,
                ZLevelFlightStopReason.CapabilitySourceRemoved);
        }

        if (userComp.GrantedZLevelFlightControls)
            RemComp<ZLevelFlightControlsComponent>(user);
        if (userComp.GrantedZLevelFlight)
            RemComp<ZLevelFlightComponent>(user);
    }

    private void OnZLevelMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        var query = EntityQueryEnumerator<JetpackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var user, out var transform))
        {
            if (transform.MapUid != args.MapUid ||
                transform.GridUid is not { } gridUid ||
                CanEnableOnGrid(gridUid) ||
                !_jetpackQuery.TryGetComponent(user.Jetpack, out var jetpack))
            {
                continue;
            }

            _popup.PopupClient(Loc.GetString("jetpack-to-grid"), uid, uid);
            SetEnabled(user.Jetpack, jetpack, false, uid);
        }
    }
}

[Serializable, NetSerializable]
public enum JetpackVisuals : byte
{
    Enabled,
    Layer
}
