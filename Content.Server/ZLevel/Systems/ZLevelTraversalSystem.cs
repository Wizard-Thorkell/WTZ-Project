// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Content.Server.Popups;
using Content.Shared.Interaction;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Verbs;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Handles first-pass in-world Z-level traversal through dedicated stairs and ladders.
/// </summary>
public sealed class ZLevelTraversalSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _nextTraverse = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelTraversalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ZLevelTraversalComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ZLevelTraversalComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerb);
        SubscribeLocalEvent<ZLevelTraversalComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ZLevelTraversalComponent, StepTriggeredOffEvent>(OnStepTriggered);
        SubscribeLocalEvent<ZLevelPositionComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnActivate(Entity<ZLevelTraversalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseTraversal(ent, args.User))
            return;

        args.Handled = true;
    }

    private void OnInteractHand(Entity<ZLevelTraversalComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryUseTraversal(ent, args.User))
            return;

        args.Handled = true;
    }

    private void OnGetVerb(Entity<ZLevelTraversalComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new InteractionVerb
        {
            Text = Loc.GetString(ent.Comp.ZOffset > 0
                ? "zlevel-traversal-verb-up"
                : "zlevel-traversal-verb-down"),
            Act = () => TryUseTraversal(ent, user)
        });
    }

    private void OnStepTriggerAttempt(Entity<ZLevelTraversalComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = CanUseTraversal(ent.Owner, args.Tripper);
    }

    private void OnStepTriggered(Entity<ZLevelTraversalComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (_nextTraverse.TryGetValue(args.Tripper, out var nextTraverse) &&
            _timing.CurTime < nextTraverse)
        {
            return;
        }

        if (!TryUseTraversal(ent, args.Tripper, popupOnFailure: false, popupOnSuccess: false))
            return;

        _nextTraverse[args.Tripper] = _timing.CurTime + TimeSpan.FromSeconds(0.35f);
    }

    private void OnTerminating(Entity<ZLevelPositionComponent> ent, ref EntityTerminatingEvent args)
    {
        _nextTraverse.Remove(ent.Owner);
    }

    private bool TryUseTraversal(Entity<ZLevelTraversalComponent> ent, EntityUid user, bool popupOnFailure = true, bool popupOnSuccess = true)
    {
        if (!CanUseTraversal(ent.Owner, user) ||
            !_zLevel.TryTraverseAdjacentLevel(
                user,
                ent.Comp.ZOffset,
                ent.Comp.RequireDirectDestinationSupport))
        {
            if (popupOnFailure)
                _popup.PopupEntity(Loc.GetString("zlevel-traversal-failed"), ent, user);
            return false;
        }

        if (popupOnSuccess)
            _popup.PopupEntity(Loc.GetString("zlevel-traversal-success", ("z", _zLevel.GetZLevel(user))), user, user);

        return true;
    }

    private bool CanUseTraversal(EntityUid traversal, EntityUid user)
    {
        if (!TryComp<TransformComponent>(traversal, out var traversalXform) ||
            !TryComp<TransformComponent>(user, out var userXform) ||
            traversalXform.MapID != userXform.MapID ||
            traversalXform.GridUid == null ||
            traversalXform.GridUid != userXform.GridUid ||
            !TryComp<MapGridComponent>(traversalXform.GridUid.Value, out var grid))
        {
            return false;
        }

        var traversalZ = _transform.GetZLevel((traversal, traversalXform, CompOrNull<ZLevelPositionComponent>(traversal)));
        var userZ = _transform.GetZLevel((user, userXform, CompOrNull<ZLevelPositionComponent>(user)));
        if (traversalZ != userZ)
            return false;

        var traversalTile = _map.TileIndicesFor(traversalXform.GridUid.Value, grid, traversalXform.Coordinates);
        var userTile = _map.TileIndicesFor(traversalXform.GridUid.Value, grid, userXform.Coordinates);
        return traversalTile == userTile;
    }
}
