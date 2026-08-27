// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Content.Server.Popups;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Verbs;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Handles first-pass in-world Z-level traversal through dedicated stairs and ladders.
/// </summary>
public sealed class ZLevelTraversalSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    private readonly Dictionary<EntityUid, PendingTraversal> _pendingTraversals = new();
    private readonly HashSet<(EntityUid User, EntityUid Traversal)> _suppressedAutoTraversals = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelTraversalComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ZLevelTraversalComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<ZLevelTraversalComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerb);
        SubscribeLocalEvent<ZLevelTraversalComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<ZLevelTraversalComponent, StepTriggeredOnEvent>(OnStepTriggeredOn);
        SubscribeLocalEvent<ZLevelTraversalComponent, StepTriggeredOffEvent>(OnStepTriggeredOff);
        SubscribeLocalEvent<ZLevelTraversalComponent, DoAfterAttemptEvent<ZLevelTraversalDoAfterEvent>>(OnTraversalDoAfterAttempt);
        SubscribeLocalEvent<ZLevelTraversalComponent, ZLevelTraversalDoAfterEvent>(OnTraversalDoAfter);
        SubscribeLocalEvent<ZLevelTraversalComponent, EntityTerminatingEvent>(OnTraversalTerminating);
        SubscribeLocalEvent<ZLevelPositionComponent, EntityTerminatingEvent>(OnTerminating);

        _transform.OnGlobalMoveEvent += OnMoverMoved;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _transform.OnGlobalMoveEvent -= OnMoverMoved;
    }

    private void OnActivate(Entity<ZLevelTraversalComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryStartTraversal(ent, args.User))
            return;

        args.Handled = true;
    }

    private void OnInteractHand(Entity<ZLevelTraversalComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (!TryStartTraversal(ent, args.User))
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
            Act = () => TryStartTraversal(ent, user)
        });
    }

    private void OnStepTriggerAttempt(Entity<ZLevelTraversalComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = CanUseTraversal(ent.Owner, args.Tripper);
    }

    private void OnStepTriggeredOn(Entity<ZLevelTraversalComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (_suppressedAutoTraversals.Contains((args.Tripper, ent.Owner)))
            return;

        TryStartTraversal(ent, args.Tripper, popupOnFailure: false);
    }

    private void OnStepTriggeredOff(Entity<ZLevelTraversalComponent> ent, ref StepTriggeredOffEvent args)
    {
        _suppressedAutoTraversals.Remove((args.Tripper, ent.Owner));
    }

    private void OnMoverMoved(ref MoveEvent args)
    {
        var user = args.Sender;
        if (!HasComp<MobStateComponent>(user) ||
            !TryGetTileChange(user, args, out var oldTile, out var newTile) ||
            oldTile == newTile)
        {
            return;
        }

        _suppressedAutoTraversals.RemoveWhere(entry =>
            entry.User == user && !CanUseTraversal(entry.Traversal, user));

        if (_pendingTraversals.ContainsKey(user) ||
            !TryGetTraversalAtUser(user, out var traversal))
        {
            return;
        }

        if (TryGetConnectedTraversalAtTile(traversal, oldTile, out _))
            return;

        TryStartTraversal(traversal, user, popupOnFailure: false);
    }

    private void OnTraversalDoAfterAttempt(
        Entity<ZLevelTraversalComponent> ent,
        ref DoAfterAttemptEvent<ZLevelTraversalDoAfterEvent> args)
    {
        if (!TryGetConnectedTraversalAtUser(ent, args.DoAfter.Args.User, out _))
            args.Cancel();
    }

    private void OnTraversalDoAfter(Entity<ZLevelTraversalComponent> ent, ref ZLevelTraversalDoAfterEvent args)
    {
        if (_pendingTraversals.TryGetValue(args.User, out var pending) &&
            pending.Traversal == ent.Owner &&
            pending.DoAfter == args.DoAfter.Id)
        {
            _pendingTraversals.Remove(args.User);
        }

        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;
        if (!TryGetConnectedTraversalAtUser(ent, args.User, out var currentTraversal) ||
            !TryUseTraversal(currentTraversal, args.User, popupOnFailure: false))
        {
            return;
        }

        SuppressDestinationAutoTraversal(args.User);
    }

    private void OnTerminating(Entity<ZLevelPositionComponent> ent, ref EntityTerminatingEvent args)
    {
        _pendingTraversals.Remove(ent.Owner);
        _suppressedAutoTraversals.RemoveWhere(entry => entry.User == ent.Owner);
    }

    private void OnTraversalTerminating(Entity<ZLevelTraversalComponent> ent, ref EntityTerminatingEvent args)
    {
        _suppressedAutoTraversals.RemoveWhere(entry => entry.Traversal == ent.Owner);

        EntityUid? userToCancel = null;
        DoAfterId? doAfterToCancel = null;
        foreach (var (user, pending) in _pendingTraversals)
        {
            if (pending.Traversal != ent.Owner)
                continue;

            userToCancel = user;
            doAfterToCancel = pending.DoAfter;
            break;
        }

        if (userToCancel == null)
            return;

        _pendingTraversals.Remove(userToCancel.Value);
        _doAfter.Cancel(doAfterToCancel);
    }

    private bool TryStartTraversal(Entity<ZLevelTraversalComponent> ent, EntityUid user, bool popupOnFailure = true)
    {
        if (!CanUseTraversal(ent.Owner, user))
        {
            if (popupOnFailure)
                _popup.PopupEntity(Loc.GetString("zlevel-traversal-failed"), ent, user);
            return false;
        }

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            user,
            ent.Comp.TraversalDelay,
            new ZLevelTraversalDoAfterEvent(),
            ent.Owner,
            target: ent.Owner)
        {
            BreakOnDamage = true,
            BreakOnMove = false,
            DistanceThreshold = null,
            RequireCanInteract = false,
            AttemptFrequency = AttemptFrequency.EveryTick,
            CancelDuplicate = false,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs, out var doAfterId))
            return false;

        _pendingTraversals[user] = new PendingTraversal(ent.Owner, doAfterId.Value);
        return true;
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

    private void SuppressDestinationAutoTraversal(EntityUid user)
    {
        var query = EntityQueryEnumerator<ZLevelTraversalComponent>();
        while (query.MoveNext(out var traversal, out _))
        {
            if (CanUseTraversal(traversal, user))
                _suppressedAutoTraversals.Add((user, traversal));
        }
    }

    private bool TryGetTraversalAtUser(EntityUid user, out Entity<ZLevelTraversalComponent> traversal)
    {
        var query = EntityQueryEnumerator<ZLevelTraversalComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_suppressedAutoTraversals.Contains((user, uid)) || !CanUseTraversal(uid, user))
                continue;

            traversal = (uid, component);
            return true;
        }

        traversal = default;
        return false;
    }

    private bool TryGetConnectedTraversalAtUser(
        Entity<ZLevelTraversalComponent> origin,
        EntityUid user,
        out Entity<ZLevelTraversalComponent> traversal)
    {
        traversal = default;
        if (!TryComp<TransformComponent>(user, out var userXform) ||
            userXform.GridUid == null ||
            !TryComp<MapGridComponent>(userXform.GridUid.Value, out var grid))
        {
            return false;
        }

        var userTile = _map.TileIndicesFor(userXform.GridUid.Value, grid, userXform.Coordinates);
        return TryGetConnectedTraversalAtTile(origin, userTile, out traversal);
    }

    private bool TryGetConnectedTraversalAtTile(
        Entity<ZLevelTraversalComponent> origin,
        Vector2i targetTile,
        out Entity<ZLevelTraversalComponent> traversal)
    {
        traversal = default;
        if (!TryComp<TransformComponent>(origin, out var originXform) ||
            originXform.GridUid == null ||
            !TryComp<MapGridComponent>(originXform.GridUid.Value, out var grid))
        {
            return false;
        }

        var originZ = _transform.GetZLevel((origin.Owner, originXform, CompOrNull<ZLevelPositionComponent>(origin)));
        var originTile = _map.TileIndicesFor(originXform.GridUid.Value, grid, originXform.Coordinates);
        var traversalsByTile = new Dictionary<Vector2i, Entity<ZLevelTraversalComponent>>();
        var query = EntityQueryEnumerator<ZLevelTraversalComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (xform.MapID != originXform.MapID ||
                xform.GridUid != originXform.GridUid ||
                component.ZOffset != origin.Comp.ZOffset ||
                component.RequireDirectDestinationSupport != origin.Comp.RequireDirectDestinationSupport ||
                _transform.GetZLevel((uid, xform, CompOrNull<ZLevelPositionComponent>(uid))) != originZ)
            {
                continue;
            }

            var tile = _map.TileIndicesFor(originXform.GridUid.Value, grid, xform.Coordinates);
            traversalsByTile.TryAdd(tile, (uid, component));
        }

        if (!traversalsByTile.ContainsKey(originTile))
            return false;

        var pending = new Queue<Vector2i>();
        var visited = new HashSet<Vector2i> { originTile };
        pending.Enqueue(originTile);

        while (pending.TryDequeue(out var tile))
        {
            if (tile == targetTile)
            {
                traversal = traversalsByTile[tile];
                return true;
            }

            TryQueueConnectedTile(tile + new Vector2i(1, 0), traversalsByTile, visited, pending);
            TryQueueConnectedTile(tile + new Vector2i(-1, 0), traversalsByTile, visited, pending);
            TryQueueConnectedTile(tile + new Vector2i(0, 1), traversalsByTile, visited, pending);
            TryQueueConnectedTile(tile + new Vector2i(0, -1), traversalsByTile, visited, pending);
        }

        return false;
    }

    private static void TryQueueConnectedTile(
        Vector2i tile,
        Dictionary<Vector2i, Entity<ZLevelTraversalComponent>> traversalsByTile,
        HashSet<Vector2i> visited,
        Queue<Vector2i> pending)
    {
        if (traversalsByTile.ContainsKey(tile) && visited.Add(tile))
            pending.Enqueue(tile);
    }

    private bool TryGetTileChange(EntityUid user, MoveEvent args, out Vector2i oldTile, out Vector2i newTile)
    {
        oldTile = default;
        newTile = default;
        if (!TryComp<TransformComponent>(user, out var userXform) ||
            userXform.GridUid == null ||
            !TryComp<MapGridComponent>(userXform.GridUid.Value, out var grid) ||
            args.OldPosition.EntityId != userXform.GridUid ||
            args.NewPosition.EntityId != userXform.GridUid)
        {
            return false;
        }

        oldTile = _map.TileIndicesFor(userXform.GridUid.Value, grid, args.OldPosition);
        newTile = _map.TileIndicesFor(userXform.GridUid.Value, grid, args.NewPosition);
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

    private readonly record struct PendingTraversal(EntityUid Traversal, DoAfterId DoAfter);
}
