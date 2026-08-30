// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.Actions;
using Content.Shared.Popups;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Owns player-facing flight actions while leaving movement policy in <see cref="SharedZLevelSystem"/>.
/// </summary>
public sealed class SharedZLevelFlightControlSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedZLevelMapSystem _maps = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelFlightControlsComponent, ComponentStartup>(OnControlsStartup);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, MapInitEvent>(OnControlsMapInit);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, ComponentShutdown>(OnControlsShutdown);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, AfterAutoHandleStateEvent>(OnControlsStateHandled);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, EntParentChangedMessage>(OnControlsParentChanged);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, ZLevelFlightToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, ZLevelFlightUpActionEvent>(OnMoveUpAction);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, ZLevelFlightDownActionEvent>(OnMoveDownAction);
        SubscribeLocalEvent<ZLevelFlightControlsComponent, ZLevelFlightCapabilityChangedEvent>(OnFlightCapabilityChanged);

        SubscribeLocalEvent<ZLevelFlightComponent, ZLevelFlightStartedEvent>(OnFlightStarted);
        SubscribeLocalEvent<ZLevelFlightComponent, ZLevelFlightTargetChangedEvent>(OnFlightTargetChanged);
        SubscribeLocalEvent<ZLevelFlightComponent, ZLevelFlightStoppedEvent>(OnFlightStopped);
        SubscribeLocalEvent<ZLevelFlightComponent, ZLevelFlightBoundaryBlockedEvent>(OnFlightBoundaryBlocked);
        SubscribeLocalEvent<ZLevelMapConfigurationChangedEvent>(OnMapConfigurationChanged);
    }

    private void OnControlsStartup(Entity<ZLevelFlightControlsComponent> entity, ref ComponentStartup args)
    {
        if (MetaData(entity.Owner).EntityLifeStage >= EntityLifeStage.MapInitialized)
            SynchronizeActions(entity);
    }

    private void OnControlsMapInit(Entity<ZLevelFlightControlsComponent> entity, ref MapInitEvent args)
    {
        SynchronizeActions(entity);
    }

    private void OnControlsShutdown(Entity<ZLevelFlightControlsComponent> entity, ref ComponentShutdown args)
    {
        RemoveActions(entity, dirty: false);
    }

    private void OnControlsStateHandled(
        Entity<ZLevelFlightControlsComponent> entity,
        ref AfterAutoHandleStateEvent args)
    {
        SynchronizeActions(entity);
    }

    private void OnControlsParentChanged(
        Entity<ZLevelFlightControlsComponent> entity,
        ref EntParentChangedMessage args)
    {
        SynchronizeActions(entity);
    }

    private void OnFlightCapabilityChanged(
        Entity<ZLevelFlightControlsComponent> entity,
        ref ZLevelFlightCapabilityChangedEvent args)
    {
        if (args.Available)
            SynchronizeActions(entity);
        else
            RemoveActions(entity, dirty: !TerminatingOrDeleted(entity.Owner));
    }

    private void OnToggleAction(
        Entity<ZLevelFlightControlsComponent> entity,
        ref ZLevelFlightToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var result = _zLevels.IsFlying(entity.Owner)
            ? _zLevels.TryStopFlight(entity.Owner)
            : _zLevels.TryStartFlight(entity.Owner);
        if (result is ZLevelFlightResult.Success or ZLevelFlightResult.NoChange)
            return;

        PopupFailure(args.Performer, result);
    }

    private void OnMoveUpAction(
        Entity<ZLevelFlightControlsComponent> entity,
        ref ZLevelFlightUpActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        HandleMoveAction(entity.Owner, 1, args.Performer);
    }

    private void OnMoveDownAction(
        Entity<ZLevelFlightControlsComponent> entity,
        ref ZLevelFlightDownActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        HandleMoveAction(entity.Owner, -1, args.Performer);
    }

    private void HandleMoveAction(EntityUid uid, int direction, EntityUid performer)
    {
        if (!TryComp<ZLevelFlightComponent>(uid, out var flight))
        {
            PopupFailure(performer, ZLevelFlightResult.MissingCapability);
            return;
        }

        var origin = flight.Active ? flight.TargetLocalZLevel : _zLevels.GetZLevel(uid);
        var candidate = (long) origin + direction;
        var result = candidate is < int.MinValue or > int.MaxValue
            ? ZLevelFlightResult.InvalidTarget
            : flight.Active
                ? _zLevels.TrySetFlightTarget(uid, (int) candidate, flight.HoverOffset, flight)
                : _zLevels.TryStartFlight(uid, (int) candidate, flight.HoverOffset, flight);
        if (result is ZLevelFlightResult.Success or ZLevelFlightResult.NoChange)
            return;

        PopupFailure(performer, result);
    }

    private void OnFlightStarted(Entity<ZLevelFlightComponent> entity, ref ZLevelFlightStartedEvent args)
    {
        SetToggle(entity.Owner, true);
        Popup(entity.Owner, "zlevel-flight-started");
    }

    private void OnFlightTargetChanged(
        Entity<ZLevelFlightComponent> entity,
        ref ZLevelFlightTargetChangedEvent args)
    {
        if (!HasComp<ZLevelFlightControlsComponent>(entity.Owner))
            return;

        _popup.PopupClient(
            Loc.GetString("zlevel-flight-target-changed", ("z", args.NewLocalZLevel)),
            entity.Owner,
            entity.Owner);
    }

    private void OnFlightStopped(Entity<ZLevelFlightComponent> entity, ref ZLevelFlightStoppedEvent args)
    {
        SetToggle(entity.Owner, false);
        Popup(entity.Owner, args.Reason == ZLevelFlightStopReason.Requested
            ? "zlevel-flight-stopped"
            : "zlevel-flight-interrupted");
    }

    private void OnFlightBoundaryBlocked(
        Entity<ZLevelFlightComponent> entity,
        ref ZLevelFlightBoundaryBlockedEvent args)
    {
        Popup(entity.Owner, "zlevel-flight-boundary-blocked");
    }

    private void OnMapConfigurationChanged(ref ZLevelMapConfigurationChangedEvent args)
    {
        var query = EntityQueryEnumerator<ZLevelFlightControlsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var controls, out var transform))
        {
            if (transform.MapUid == args.MapUid)
                SynchronizeActions((uid, controls));
        }
    }

    private void SynchronizeActions(Entity<ZLevelFlightControlsComponent> entity)
    {
        if (!CanExposeActions(entity.Owner))
        {
            RemoveActions(entity, dirty: !TerminatingOrDeleted(entity.Owner));
            return;
        }

        var changed = false;
        changed |= _actions.AddAction(
            entity.Owner,
            ref entity.Comp.ToggleActionEntity,
            entity.Comp.ToggleAction);
        changed |= _actions.AddAction(
            entity.Owner,
            ref entity.Comp.MoveUpActionEntity,
            entity.Comp.MoveUpAction);
        changed |= _actions.AddAction(
            entity.Owner,
            ref entity.Comp.MoveDownActionEntity,
            entity.Comp.MoveDownAction);

        SetToggle(entity.Owner, _zLevels.IsFlying(entity.Owner), entity.Comp);
        if (changed && !TerminatingOrDeleted(entity.Owner))
            Dirty(entity.Owner, entity.Comp);
    }

    private bool CanExposeActions(EntityUid uid)
    {
        return HasComp<ZLevelFlightComponent>(uid) &&
               TryComp(uid, out TransformComponent? transform) &&
               transform.GridUid is { } gridUid &&
               _maps.TryGetConfig(gridUid, out _);
    }

    private void RemoveActions(Entity<ZLevelFlightControlsComponent> entity, bool dirty)
    {
        var changed = entity.Comp.ToggleActionEntity != null ||
                      entity.Comp.MoveUpActionEntity != null ||
                      entity.Comp.MoveDownActionEntity != null;
        _actions.RemoveAction(entity.Owner, entity.Comp.ToggleActionEntity);
        _actions.RemoveAction(entity.Owner, entity.Comp.MoveUpActionEntity);
        _actions.RemoveAction(entity.Owner, entity.Comp.MoveDownActionEntity);
        entity.Comp.ToggleActionEntity = null;
        entity.Comp.MoveUpActionEntity = null;
        entity.Comp.MoveDownActionEntity = null;

        if (changed && dirty)
            Dirty(entity.Owner, entity.Comp);
    }

    private void SetToggle(
        EntityUid uid,
        bool active,
        ZLevelFlightControlsComponent? controls = null)
    {
        if (!Resolve(uid, ref controls, false) || controls.ToggleActionEntity == null)
            return;

        _actions.SetToggled(controls.ToggleActionEntity, active);
    }

    private void Popup(EntityUid uid, string message)
    {
        if (!HasComp<ZLevelFlightControlsComponent>(uid))
            return;

        _popup.PopupClient(Loc.GetString(message), uid, uid);
    }

    private void PopupFailure(EntityUid performer, ZLevelFlightResult result)
    {
        _popup.PopupClient(
            Loc.GetString("zlevel-flight-unavailable", ("reason", result)),
            performer,
            performer);
    }
}
