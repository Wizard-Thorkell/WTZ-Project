// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Server.Actions;
using Content.Server.Administration;
using Content.Server.Popups;
using Content.Server.ZLevel.Components;
using Content.Shared.Actions.Components;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Systems;

public sealed class ZLevelDebugActionsSystem : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelDebugActionsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<ZLevelDebugActionsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelDebugActionsComponent, ZLevelMoveUpActionEvent>(OnMoveUp);
        SubscribeLocalEvent<ZLevelDebugActionsComponent, ZLevelMoveDownActionEvent>(OnMoveDown);
        SubscribeLocalEvent<ZLevelDebugActionsComponent, ZLevelMoveToTargetActionEvent>(OnMoveToTarget);
        SubscribeLocalEvent<ZLevelPositionComponent, ComponentShutdown>(OnZLevelShutdown);
        SubscribeLocalEvent<ZLevelKinematicsComponent, ComponentShutdown>(OnZLevelShutdown);
    }

    private void OnStartup(EntityUid uid, ZLevelDebugActionsComponent component, ComponentStartup args)
    {
        AddAction(uid, ref component.MoveUpActionEntity, component.MoveUpAction);
        AddAction(uid, ref component.MoveDownActionEntity, component.MoveDownAction);
        AddAction(uid, ref component.MoveToTargetActionEntity, component.MoveToTargetAction);
    }

    private void OnShutdown(EntityUid uid, ZLevelDebugActionsComponent component, ComponentShutdown args)
    {
        RemoveAction(uid, component.MoveUpActionEntity);
        RemoveAction(uid, component.MoveDownActionEntity);
        RemoveAction(uid, component.MoveToTargetActionEntity);
    }

    private void OnZLevelShutdown<T>(EntityUid uid, T component, ComponentShutdown args) where T : IComponent
    {
        if (!HasComp<ZLevelDebugActionsComponent>(uid))
            return;

        RemCompDeferred<ZLevelDebugActionsComponent>(uid);
    }

    private void OnMoveUp(EntityUid uid, ZLevelDebugActionsComponent component, ZLevelMoveUpActionEvent args)
    {
        if (args.Handled || !HasComp<ZLevelPositionComponent>(uid) || !HasComp<ZLevelKinematicsComponent>(uid))
            return;

        if (!_zLevel.OffsetZLevel(uid, 1))
            return;

        _popup.PopupEntity(Loc.GetString("admin-popup-z-level-moved", ("z", _zLevel.GetZLevel(uid))), uid, uid);
        args.Handled = true;
    }

    private void OnMoveDown(EntityUid uid, ZLevelDebugActionsComponent component, ZLevelMoveDownActionEvent args)
    {
        if (args.Handled || !HasComp<ZLevelPositionComponent>(uid) || !HasComp<ZLevelKinematicsComponent>(uid))
            return;

        if (!_zLevel.OffsetZLevel(uid, -1))
            return;

        _popup.PopupEntity(Loc.GetString("admin-popup-z-level-moved", ("z", _zLevel.GetZLevel(uid))), uid, uid);
        args.Handled = true;
    }

    private void OnMoveToTarget(EntityUid uid, ZLevelDebugActionsComponent component, ZLevelMoveToTargetActionEvent args)
    {
        if (args.Handled ||
            !HasComp<ZLevelPositionComponent>(uid) ||
            !HasComp<ZLevelKinematicsComponent>(uid) ||
            !TryComp<ActorComponent>(uid, out var actor))
        {
            return;
        }

        _quickDialog.OpenDialog(
            actor.PlayerSession,
            Loc.GetString("zlevel-action-target-dialog-title"),
            Loc.GetString("zlevel-action-target-dialog-prompt"),
            (int targetZ) =>
            {
                if (Deleted(uid) ||
                    actor.PlayerSession.AttachedEntity != uid ||
                    !HasComp<ZLevelPositionComponent>(uid) ||
                    !HasComp<ZLevelKinematicsComponent>(uid))
                {
                    return;
                }

                if (!_zLevel.SetZLevel(uid, targetZ))
                    return;

                _popup.PopupEntity(Loc.GetString("admin-popup-z-level-moved", ("z", _zLevel.GetZLevel(uid))), uid, uid);
            });

        args.Handled = true;
    }

    private void AddAction(EntityUid uid, ref EntityUid? actionEntity, string prototype)
    {
        if (!_actions.AddAction(uid, ref actionEntity, out ActionComponent? action, prototype))
            return;

        _actions.SetTemporary((actionEntity!.Value, action), true);
    }

    private void RemoveAction(EntityUid uid, EntityUid? actionEntity)
    {
        if (actionEntity == null)
            return;

        _actions.RemoveAction(uid, actionEntity.Value);
    }
}
