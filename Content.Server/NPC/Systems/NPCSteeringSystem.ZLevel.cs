// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using System.Threading;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.ZLevel;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSteeringSystem
{
    private const float ZLevelLegArrivalRange = 0.2f;
    private const float ZLevelEndpointBrakeRange = 0.75f;

    [Dependency] private readonly ZLevelTraversalSystem _zLevelTraversal = default!;
    [Dependency] private readonly ZLevelTraversalGraphSystem _zLevelTraversalGraph = default!;

    private long _zLevelRoutesInstalled;
    private long _zLevelRoutesCompleted;
    private long _zLevelTraversalsStarted;
    private long _zLevelTraversalsCompleted;
    private long _zLevelReplans;
    private long _zLevelExecutionFailures;
    private long _zLevelStaleResults;

    /// <summary>
    /// Resolves the authoritative target floor. Entity-relative targets track
    /// their entity; grid-relative coordinates intentionally stay on the
    /// actor's current floor because a 2D grid coordinate carries no local Z.
    /// </summary>
    public int ResolveTargetWorldZ(
        EntityUid actor,
        EntityCoordinates coordinates,
        out EntityUid? trackedTarget)
    {
        trackedTarget = null;
        var actorWorldZ = GetWorldZ(actor);
        var target = coordinates.EntityId;

        if (target == actor ||
            HasComp<MapGridComponent>(target) ||
            HasComp<MapComponent>(target) ||
            !_xformQuery.TryComp(target, out var targetTransform))
        {
            return actorWorldZ;
        }

        trackedTarget = target;
        return GetWorldZ(target, targetTransform);
    }

    /// <summary>
    /// Installs a validated hierarchical route and loads its first executable
    /// leg into the existing local steering queue.
    /// </summary>
    public bool TryInstallZLevelRoute(
        EntityUid uid,
        ZLevelPathRoute route,
        NPCSteeringComponent? steering = null)
    {
        if (!Resolve(uid, ref steering, false) ||
            !_xformQuery.TryComp(uid, out var xform) ||
            route.Start.MapId != xform.MapID ||
            route.Start.WorldZ != GetWorldZ(uid, xform) ||
            route.End.WorldZ != steering.TargetWorldZ ||
            !IsZLevelRouteActorCurrent(uid, route, steering, xform) ||
            !IsZLevelRouteTargetCurrent(route, steering) ||
            _pathfindingSystem.ValidateZLevelPathRoute(route) is not { IsValid: true })
        {
            return false;
        }

        var targetMap = _transform.ToMapCoordinates(route.End.Coordinates);
        var targetFrame = _transform.GetGrid(route.End.Coordinates) ??
                          _transform.GetMap(route.End.Coordinates);
        if (targetMap.MapId == MapId.Nullspace || targetFrame == null)
            return false;

        steering.ZLevelRoute = route;
        steering.ZLevelLegIndex = 0;
        steering.LoadedZLevelLegIndex = -1;
        steering.ZLevelPlannedTargetCoordinates = _transform.ToCoordinates(targetFrame.Value, targetMap);
        steering.ZLevelPendingTraversal = null;
        StampZLevelRouteValidation(steering);
        steering.LastZLevelExecutionFailureReason = NPCZLevelExecutionFailureReason.None;
        steering.Status = SteeringStatus.Moving;
        steering.CurrentPath.Clear();

        if (!TryLoadCurrentZLevelLeg(uid, steering, xform))
        {
            ClearZLevelRoute(uid, steering);
            return false;
        }

        Interlocked.Increment(ref _zLevelRoutesInstalled);
        return true;
    }

    private bool IsZLevelRouteActorCurrent(
        EntityUid uid,
        ZLevelPathRoute route,
        NPCSteeringComponent steering,
        TransformComponent xform)
    {
        var routeStart = _transform.ToMapCoordinates(route.Start.Coordinates);
        var currentStart = _transform.GetMapCoordinates(uid, xform: xform);
        return routeStart.MapId != MapId.Nullspace &&
               routeStart.MapId == currentStart.MapId &&
               (routeStart.Position - currentStart.Position).Length() <= steering.RepathRange;
    }

    private bool IsZLevelRouteTargetCurrent(
        ZLevelPathRoute route,
        NPCSteeringComponent steering)
    {
        var routeTarget = _transform.ToMapCoordinates(route.End.Coordinates);
        var currentTarget = _transform.ToMapCoordinates(steering.Coordinates);
        return routeTarget.MapId != MapId.Nullspace &&
               routeTarget.MapId == currentTarget.MapId &&
               (routeTarget.Position - currentTarget.Position).Length() <= steering.RepathRange;
    }

    public NPCZLevelSteeringMetricsSnapshot SnapshotZLevelMetrics()
    {
        return new NPCZLevelSteeringMetricsSnapshot(
            Interlocked.Read(ref _zLevelRoutesInstalled),
            Interlocked.Read(ref _zLevelRoutesCompleted),
            Interlocked.Read(ref _zLevelTraversalsStarted),
            Interlocked.Read(ref _zLevelTraversalsCompleted),
            Interlocked.Read(ref _zLevelReplans),
            Interlocked.Read(ref _zLevelExecutionFailures),
            Interlocked.Read(ref _zLevelStaleResults));
    }

    public void ResetZLevelMetrics()
    {
        Interlocked.Exchange(ref _zLevelRoutesInstalled, 0);
        Interlocked.Exchange(ref _zLevelRoutesCompleted, 0);
        Interlocked.Exchange(ref _zLevelTraversalsStarted, 0);
        Interlocked.Exchange(ref _zLevelTraversalsCompleted, 0);
        Interlocked.Exchange(ref _zLevelReplans, 0);
        Interlocked.Exchange(ref _zLevelExecutionFailures, 0);
        Interlocked.Exchange(ref _zLevelStaleResults, 0);
    }

    private int GetWorldZ(EntityUid uid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform, false))
            return 0;

        return _transform.GetWorldZLevel((uid, xform, CompOrNull<ZLevelPositionComponent>(uid)));
    }

    private bool TryRefreshTargetWorldZ(
        EntityUid uid,
        NPCSteeringComponent steering,
        out bool changed)
    {
        changed = false;
        if (steering.ZLevelTrackedTarget is not { } target)
            return true;

        if (Deleted(target) || !_xformQuery.TryComp(target, out var targetTransform))
            return false;

        var worldZ = GetWorldZ(target, targetTransform);
        if (worldZ == steering.TargetWorldZ)
            return true;

        steering.TargetWorldZ = worldZ;
        changed = true;
        return true;
    }

    private ZLevelRoutePreparation PrepareZLevelRoute(
        EntityUid uid,
        NPCSteeringComponent steering,
        TransformComponent xform)
    {
        if (!TryRefreshTargetWorldZ(uid, steering, out var targetFloorChanged))
            return ZLevelRoutePreparation.Failed;

        if (targetFloorChanged && steering.ZLevelRoute != null)
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.TargetFloorChanged);
            return ZLevelRoutePreparation.Repath;
        }

        var route = steering.ZLevelRoute;
        if (route == null)
            return ZLevelRoutePreparation.None;

        if ((steering.ZLevelValidatedTopologyRevision != _zLevelTraversalGraph.TopologyRevision ||
             steering.ZLevelValidatedEnvironmentRevision != _zLevelTraversalGraph.EnvironmentRevision) &&
            !ValidateAndStampZLevelRoute(route, steering))
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.RouteInvalid);
            return ZLevelRoutePreparation.Repath;
        }

        var targetMap = _transform.ToMapCoordinates(steering.Coordinates);
        if (targetMap.MapId != route.End.MapId || route.End.WorldZ != steering.TargetWorldZ)
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.TargetMapChanged);
            return ZLevelRoutePreparation.Repath;
        }

        var plannedCoordinates = steering.ZLevelPlannedTargetCoordinates;
        var plannedTarget = plannedCoordinates.IsValid(EntityManager)
            ? _transform.ToMapCoordinates(plannedCoordinates)
            : MapCoordinates.Nullspace;
        if (plannedTarget.MapId != targetMap.MapId ||
            (plannedTarget.Position - targetMap.Position).Length() > steering.RepathRange)
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.TargetMoved);
            return ZLevelRoutePreparation.Repath;
        }

        while (steering.ZLevelLegIndex < route.Legs.Length)
        {
            var leg = route.Legs[steering.ZLevelLegIndex];
            var currentWorldZ = GetWorldZ(uid, xform);
            if (leg.Kind == ZLevelPathLegKind.Traversal && currentWorldZ == leg.End.WorldZ)
            {
                steering.ZLevelPendingTraversal = null;
                Interlocked.Increment(ref _zLevelTraversalsCompleted);
                steering.ZLevelLegIndex++;
                steering.LoadedZLevelLegIndex = -1;
                steering.CurrentPath.Clear();

                if (!TryLoadCurrentZLevelLeg(uid, steering, xform))
                    return FailZLevelRoute(uid, steering);

                route = steering.ZLevelRoute;
                if (route == null)
                    return ZLevelRoutePreparation.None;

                continue;
            }

            if (steering.LoadedZLevelLegIndex != steering.ZLevelLegIndex &&
                !TryLoadCurrentZLevelLeg(uid, steering, xform))
            {
                ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.RouteInvalid);
                return ZLevelRoutePreparation.Repath;
            }

            if (currentWorldZ != leg.Start.WorldZ)
            {
                ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.UnexpectedFloor);
                return ZLevelRoutePreparation.Repath;
            }

            if (leg.Kind == ZLevelPathLegKind.Traversal &&
                _zLevelTraversal.IsTraversalPending(uid, leg.Traversal.Source.Traversal))
            {
                TrackStartedZLevelTraversal(steering, leg.Traversal.Source.Traversal);
                return ZLevelRoutePreparation.Hold;
            }

            return ZLevelRoutePreparation.Ready;
        }

        CompleteZLevelRoute(uid, steering);
        return ZLevelRoutePreparation.None;
    }

    private EntityCoordinates GetZLevelLegDestination(NPCSteeringComponent steering)
    {
        var route = steering.ZLevelRoute;
        if (route == null || steering.ZLevelLegIndex >= route.Legs.Length)
            return steering.Coordinates;

        var leg = route.Legs[steering.ZLevelLegIndex];
        return leg.Kind == ZLevelPathLegKind.Local
            ? leg.End.Coordinates
            : leg.Start.Coordinates;
    }

    private ZLevelRouteArrival HandleZLevelRouteArrival(
        EntityUid uid,
        NPCSteeringComponent steering,
        TransformComponent xform)
    {
        var route = steering.ZLevelRoute;
        if (route == null || steering.ZLevelLegIndex >= route.Legs.Length)
            return ZLevelRouteArrival.None;

        var leg = route.Legs[steering.ZLevelLegIndex];
        if (leg.Kind == ZLevelPathLegKind.Local)
        {
            steering.ZLevelLegIndex++;
            steering.LoadedZLevelLegIndex = -1;
            steering.CurrentPath.Clear();
            if (!TryLoadCurrentZLevelLeg(uid, steering, xform))
                return ZLevelRouteArrival.Failed;

            return steering.ZLevelRoute == null
                ? ZLevelRouteArrival.Completed
                : ZLevelRouteArrival.Advanced;
        }

        var validation = _pathfindingSystem.ValidateZLevelPathRoute(route);
        if (!validation.IsValid)
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.RouteInvalid);
            return ZLevelRouteArrival.Repath;
        }

        StampZLevelRouteValidation(steering);

        var traversal = leg.Traversal.Source.Traversal;
        var wasPending = _zLevelTraversal.IsTraversalPending(uid, traversal);
        if (!_zLevelTraversal.TryStartTraversal(leg.Traversal, uid))
        {
            ReplanZLevelRoute(uid, steering, NPCZLevelReplanReason.TraversalUnavailable);
            return ZLevelRouteArrival.Repath;
        }

        if (!wasPending || steering.ZLevelPendingTraversal != traversal)
            TrackStartedZLevelTraversal(steering, traversal);

        return ZLevelRouteArrival.Hold;
    }

    private bool TryLoadCurrentZLevelLeg(
        EntityUid uid,
        NPCSteeringComponent steering,
        TransformComponent xform)
    {
        var route = steering.ZLevelRoute;
        if (route == null)
            return true;

        if (steering.ZLevelLegIndex >= route.Legs.Length)
        {
            CompleteZLevelRoute(uid, steering);
            return true;
        }

        if (!ValidateAndStampZLevelRoute(route, steering))
            return false;

        var leg = route.Legs[steering.ZLevelLegIndex];
        if (xform.MapID != leg.Start.MapId || GetWorldZ(uid, xform) != leg.Start.WorldZ)
            return false;

        steering.CurrentPath.Clear();
        if (leg.Kind == ZLevelPathLegKind.Local)
        {
            var path = leg.LocalPath.ToList();
            var ourPosition = _transform.GetMapCoordinates(uid, xform: xform);
            var targetPosition = _transform.ToMapCoordinates(leg.End.Coordinates);
            PrunePath(uid, ourPosition, targetPosition.Position - ourPosition.Position, path);
            steering.CurrentPath = new Queue<PathPoly>(path);
        }

        steering.LoadedZLevelLegIndex = steering.ZLevelLegIndex;
        return true;
    }

    private void ClearZLevelRoute(EntityUid uid, NPCSteeringComponent steering)
    {
        if (steering.ZLevelPendingTraversal is { } traversal)
            _zLevelTraversal.TryCancelTraversal(uid, traversal);

        steering.ZLevelRoute = null;
        steering.ZLevelLegIndex = 0;
        steering.LoadedZLevelLegIndex = -1;
        steering.ZLevelPlannedTargetCoordinates = EntityCoordinates.Invalid;
        steering.ZLevelPendingTraversal = null;
        steering.ZLevelValidatedTopologyRevision = -1;
        steering.ZLevelValidatedEnvironmentRevision = -1;
        steering.LastZLevelReplanReason = NPCZLevelReplanReason.None;
        steering.CurrentPath.Clear();
    }

    private bool ValidateAndStampZLevelRoute(
        ZLevelPathRoute route,
        NPCSteeringComponent steering)
    {
        if (!_pathfindingSystem.ValidateZLevelPathRoute(route).IsValid)
            return false;

        StampZLevelRouteValidation(steering);
        return true;
    }

    private void StampZLevelRouteValidation(NPCSteeringComponent steering)
    {
        steering.ZLevelValidatedTopologyRevision = _zLevelTraversalGraph.TopologyRevision;
        steering.ZLevelValidatedEnvironmentRevision = _zLevelTraversalGraph.EnvironmentRevision;
    }

    private void ReplanZLevelRoute(
        EntityUid uid,
        NPCSteeringComponent steering,
        NPCZLevelReplanReason reason)
    {
        ClearZLevelRoute(uid, steering);
        steering.LastZLevelReplanReason = reason;
        Interlocked.Increment(ref _zLevelReplans);
    }

    private void TrackStartedZLevelTraversal(
        NPCSteeringComponent steering,
        EntityUid traversal)
    {
        if (steering.ZLevelPendingTraversal == traversal)
            return;

        steering.ZLevelPendingTraversal = traversal;
        Interlocked.Increment(ref _zLevelTraversalsStarted);
    }

    private void CompleteZLevelRoute(EntityUid uid, NPCSteeringComponent steering)
    {
        ClearZLevelRoute(uid, steering);
        steering.FailedPathCount = 0;
        Interlocked.Increment(ref _zLevelRoutesCompleted);
    }

    private ZLevelRoutePreparation FailZLevelRoute(EntityUid uid, NPCSteeringComponent steering)
    {
        SetNoPath(uid, steering, NPCZLevelExecutionFailureReason.RoutePreparationFailed);
        return ZLevelRoutePreparation.Failed;
    }

    private void SetNoPath(
        EntityUid uid,
        NPCSteeringComponent steering,
        NPCZLevelExecutionFailureReason reason)
    {
        if (steering.ZLevelRoute != null)
        {
            steering.LastZLevelExecutionFailureReason = reason;
            if (steering.Status != SteeringStatus.NoPath)
                Interlocked.Increment(ref _zLevelExecutionFailures);

            ClearZLevelRoute(uid, steering);
        }

        steering.Status = SteeringStatus.NoPath;
    }

    private bool IsAtZLevelLegDestination(
        NPCSteeringComponent steering,
        EntityCoordinates ourCoordinates,
        EntityCoordinates targetCoordinates,
        Vector2 direction)
    {
        if (steering.ZLevelRoute == null || steering.CurrentPath.Count > 0)
            return false;

        if (direction.Length() > ZLevelLegArrivalRange)
            return false;

        var leg = steering.ZLevelRoute.Legs[steering.ZLevelLegIndex];
        return targetCoordinates.Equals(leg.Kind == ZLevelPathLegKind.Local
            ? leg.End.Coordinates
            : leg.Start.Coordinates) &&
            ourCoordinates.EntityId == targetCoordinates.EntityId;
    }

    private void BrakeForZLevelEndpoint(
        EntityUid uid,
        NPCSteeringComponent steering,
        PhysicsComponent body,
        Vector2 direction)
    {
        if (steering.ZLevelRoute == null ||
            steering.CurrentPath.Count > 0 ||
            direction == Vector2.Zero ||
            direction.Length() > ZLevelEndpointBrakeRange)
        {
            return;
        }

        var normal = direction.Normalized();
        var speedTowardEndpoint = Vector2.Dot(body.LinearVelocity, normal);
        if (speedTowardEndpoint <= 0f)
            return;

        _physics.SetLinearVelocity(
            uid,
            body.LinearVelocity - normal * speedTowardEndpoint,
            body: body);
    }

    private void RecordStaleZLevelPathResult()
    {
        Interlocked.Increment(ref _zLevelStaleResults);
    }

    private enum ZLevelRoutePreparation : byte
    {
        None,
        Ready,
        Hold,
        Repath,
        Failed,
    }

    private enum ZLevelRouteArrival : byte
    {
        None,
        Advanced,
        Hold,
        Completed,
        Repath,
        Failed,
    }
}

public readonly record struct NPCZLevelSteeringMetricsSnapshot(
    long RoutesInstalled,
    long RoutesCompleted,
    long TraversalsStarted,
    long TraversalsCompleted,
    long Replans,
    long ExecutionFailures,
    long StaleResults);
