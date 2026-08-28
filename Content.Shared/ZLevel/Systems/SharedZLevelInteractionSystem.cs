// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Central spatial authority for direct and explicitly vertical interactions.
/// Gameplay interaction rules remain owned by their normal systems.
/// </summary>
public sealed class SharedZLevelInteractionSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly SharedZLevelTraceSystem _trace = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;

    private readonly ZLevelTraceBuffer _traceBuffer = new();
    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _eyeQuery = GetEntityQuery<EyeComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    /// <summary>
    /// Resolves the entity whose position governs a world interaction.
    /// Remote eyes are spatial origins, while self and container-local operations stay with the actor.
    /// </summary>
    public bool TryGetSpatialOrigin(EntityUid user, EntityUid? target, out EntityUid origin)
    {
        origin = user;
        if (!TryGetTransform(user, out _))
            return false;

        if (target is { } targetUid && IsActorLocalTarget(user, targetUid))
            return true;

        if (_eyeQuery.TryComp(user, out var eye) &&
            eye.Target is { } remote &&
            TryGetTransform(remote, out _))
        {
            origin = remote;
        }

        return true;
    }

    /// <summary>
    /// Returns whether two physical entities occupy the same map and world Z level.
    /// This deliberately ignores remote-eye redirection.
    /// </summary>
    public bool AreOnSameWorldLevel(EntityUid first, EntityUid second)
    {
        var allowed = TryGetContext(first, out var firstContext) &&
                      TryGetContext(second, out var secondContext) &&
                      firstContext.MapCoordinates.MapId == secondContext.MapCoordinates.MapId &&
                      firstContext.WorldZ == secondContext.WorldZ;
        _metrics.RecordPhysicalInteractionCheck(allowed);
        return allowed;
    }

    /// <summary>
    /// Applies the selected vertical policy from the user's effective spatial origin to a target.
    /// A positive range is measured in combined XY and discrete-Z units.
    /// </summary>
    private bool CanInteract(
        EntityUid user,
        EntityUid target,
        bool allowVertical,
        float maximumRange = 0f,
        CollisionGroup collisionMask = CollisionGroup.None,
        EntityUid? ignoredEntity = null,
        ZLevelTraceBuffer? traceBuffer = null)
    {
        traceBuffer?.Clear();
        if (!TryGetSpatialOrigin(user, target, out var origin))
        {
            return RecordDecision(ZLevelInteractionDecision.InvalidContextRejected, false);
        }

        var remoteOrigin = origin != user;
        if (!TryGetContext(origin, out var originContext) ||
            !TryGetContext(target, out var targetContext))
        {
            return RecordDecision(ZLevelInteractionDecision.InvalidContextRejected, remoteOrigin);
        }

        if (originContext.MapCoordinates.MapId != targetContext.MapCoordinates.MapId)
            return RecordDecision(ZLevelInteractionDecision.DifferentMapRejected, remoteOrigin);

        if (allowVertical && (!float.IsFinite(maximumRange) || maximumRange <= 0f))
            return RecordDecision(ZLevelInteractionDecision.RangeRejected, remoteOrigin);

        if (maximumRange > 0f && GetDistance(originContext, targetContext) > maximumRange)
            return RecordDecision(ZLevelInteractionDecision.RangeRejected, remoteOrigin);

        if (originContext.WorldZ == targetContext.WorldZ)
            return RecordDecision(ZLevelInteractionDecision.SameLevelAllowed, remoteOrigin);

        if (!allowVertical)
            return RecordDecision(ZLevelInteractionDecision.DifferentLevelRejected, remoteOrigin);

        if (originContext.GridUid is not { } frameUid ||
            targetContext.GridUid != frameUid ||
            !_transformQuery.TryComp(frameUid, out var frameTransform))
        {
            return RecordDecision(ZLevelInteractionDecision.FrameRejected, remoteOrigin);
        }

        var originLocal = _transform.ToCoordinates(
            (frameUid, frameTransform),
            originContext.MapCoordinates).Position;
        var targetLocal = _transform.ToCoordinates(
            (frameUid, frameTransform),
            targetContext.MapCoordinates).Position;
        if (!_trace.TryCreateGridPoint(frameUid, originLocal, originContext.LocalZ, out var traceOrigin) ||
            !_trace.TryCreateGridPoint(frameUid, targetLocal, targetContext.LocalZ, out var traceTarget))
        {
            return RecordDecision(ZLevelInteractionDecision.FrameRejected, remoteOrigin);
        }

        var request = new ZLevelTraceRequest(
            traceOrigin,
            traceTarget,
            ZLevelBoundaryChannels.Interaction,
            CollisionMask: (int) collisionMask,
            IgnoredEntity: ignoredEntity,
            Options: collisionMask == CollisionGroup.None
                ? ZLevelTraceOptions.None
                : ZLevelTraceOptions.IncludeEntityHits,
            BoundaryFrameUid: frameUid);
        var buffer = traceBuffer ?? _traceBuffer;
        return RecordDecision(
            _trace.Trace(request, buffer).ReachedDestination
                ? ZLevelInteractionDecision.VerticalAllowed
                : ZLevelInteractionDecision.TraceRejected,
            remoteOrigin);
    }

    public bool CanDirectlyInteract(EntityUid user, EntityUid target)
    {
        return CanInteract(user, target, false);
    }

    public bool CanInteractThroughOpenBoundary(EntityUid user, EntityUid target, float maximumRange)
    {
        return CanInteract(user, target, true, maximumRange);
    }

    /// <summary>
    /// Traces an explicitly vertical interaction and retains its segmented fixture hits.
    /// The caller owns the gameplay-specific decision about which hits are obstructions.
    /// </summary>
    public bool CanInteractThroughOpenBoundary(
        EntityUid user,
        EntityUid target,
        float maximumRange,
        CollisionGroup collisionMask,
        EntityUid? ignoredEntity,
        ZLevelTraceBuffer traceBuffer)
    {
        ArgumentNullException.ThrowIfNull(traceBuffer);
        return CanInteract(
            user,
            target,
            true,
            maximumRange,
            collisionMask,
            ignoredEntity,
            traceBuffer);
    }

    /// <summary>
    /// Returns whether a target shares the effective interaction origin's map and world level.
    /// Unlike <see cref="AreOnSameWorldLevel"/>, this follows remote-eye and relay spatial ownership.
    /// </summary>
    public bool AreOnSameEffectiveWorldLevel(EntityUid user, EntityUid target)
    {
        return TryGetSpatialOrigin(user, target, out var origin) &&
               TryGetContext(origin, out var originContext) &&
               TryGetContext(target, out var targetContext) &&
               originContext.MapCoordinates.MapId == targetContext.MapCoordinates.MapId &&
               originContext.WorldZ == targetContext.WorldZ;
    }

    public bool IsCoordinateOnEffectiveWorldLevel(
        EntityUid user,
        EntityCoordinates coordinates,
        int targetWorldZ)
    {
        if (!coordinates.IsValid(EntityManager) ||
            !TryGetSpatialOrigin(user, null, out var origin) ||
            !TryGetContext(origin, out var originContext))
        {
            return false;
        }

        var targetMap = _transform.ToMapCoordinates(coordinates);
        return IsFinite(targetMap.Position) &&
               targetMap.MapId == originContext.MapCoordinates.MapId &&
               targetWorldZ == originContext.WorldZ;
    }

    /// <summary>
    /// Validates an explicit targetless coordinate on a visible lower-floor
    /// surface. The consuming subsystem still owns its gameplay boundary channel.
    /// </summary>
    public bool CanTargetVisibleCoordinate(
        EntityUid user,
        EntityCoordinates coordinates,
        int targetWorldZ,
        float maximumRange)
    {
        if (!float.IsFinite(maximumRange) ||
            !coordinates.IsValid(EntityManager) ||
            !TryGetSpatialOrigin(user, null, out var origin) ||
            !TryGetContext(origin, out var originContext))
        {
            return false;
        }

        var targetMap = _transform.ToMapCoordinates(coordinates);
        if (!IsFinite(targetMap.Position) ||
            targetMap.MapId != originContext.MapCoordinates.MapId ||
            targetWorldZ >= originContext.WorldZ)
        {
            return false;
        }

        var coordinateFrame = HasComp<MapGridComponent>(coordinates.EntityId)
            ? coordinates.EntityId
            : _transform.GetGrid(coordinates);
        if (originContext.GridUid is not { } originFrame ||
            coordinateFrame != originFrame)
        {
            return false;
        }

        if (maximumRange > 0f)
        {
            var planar = Vector2.Distance(
                originContext.MapCoordinates.Position,
                targetMap.Position);
            var vertical = (double) targetWorldZ - originContext.WorldZ;
            var distance = Math.Sqrt((double) planar * planar + vertical * vertical);
            if (!double.IsFinite(distance) || distance > maximumRange)
                return false;
        }

        return _visibility.IsCoordinateVisibleFrom(
            coordinates,
            targetWorldZ,
            originContext.MapCoordinates.MapId,
            originContext.WorldZ);
    }

    /// <summary>
    /// Validates an entity selected for ranged targeting. Native same-floor
    /// targeting remains available, while vertical targeting is limited to a
    /// visible lower floor on the same structural frame.
    /// </summary>
    public bool CanTargetVisibleEntity(EntityUid user, EntityUid target)
    {
        if (!TryGetSpatialOrigin(user, target, out var origin) ||
            !TryGetContext(origin, out var originContext) ||
            !TryGetContext(target, out var targetContext) ||
            originContext.MapCoordinates.MapId != targetContext.MapCoordinates.MapId)
        {
            return false;
        }

        if (originContext.WorldZ == targetContext.WorldZ)
            return true;

        if (targetContext.WorldZ >= originContext.WorldZ ||
            originContext.GridUid is not { } originFrame ||
            targetContext.GridUid != originFrame)
        {
            return false;
        }

        return _visibility.IsEntityVisibleFrom(
            target,
            originContext.MapCoordinates.MapId,
            originContext.WorldZ);
    }

    /// <summary>
    /// Resolves and validates the gameplay layer attached to planar pointer coordinates.
    /// A target entity is authoritative for its layer. Coordinate-only requests remain
    /// on the effective interaction origin unless their owning subsystem explicitly opts in.
    /// </summary>
    public bool TryResolveCoordinateLayer(
        EntityUid user,
        EntityUid? target,
        EntityCoordinates coordinates,
        int? requestedWorldZ,
        bool allowCrossLevelCoordinates,
        out int worldZ)
    {
        worldZ = default;
        if (!coordinates.IsValid(EntityManager) ||
            !TryGetSpatialOrigin(user, target, out var origin) ||
            !TryGetContext(origin, out var originContext))
        {
            return false;
        }

        var mapCoordinates = _transform.ToMapCoordinates(coordinates);
        if (mapCoordinates.MapId == MapId.Nullspace ||
            mapCoordinates.MapId != originContext.MapCoordinates.MapId ||
            !IsFinite(mapCoordinates.Position))
        {
            return false;
        }

        InteractionSpatialContext? targetContext = null;
        if (target is { } targetUid)
        {
            if (!TryGetContext(targetUid, out var resolvedTarget) ||
                resolvedTarget.MapCoordinates.MapId != mapCoordinates.MapId)
            {
                return false;
            }

            targetContext = resolvedTarget;
        }

        worldZ = requestedWorldZ ?? targetContext?.WorldZ ?? originContext.WorldZ;
        if (targetContext is { } resolved && worldZ != resolved.WorldZ)
            return false;

        return allowCrossLevelCoordinates || worldZ == originContext.WorldZ;
    }

    private bool IsActorLocalTarget(EntityUid user, EntityUid target)
    {
        if (user == target)
            return true;

        if (!TryGetTransform(user, out var userTransform) ||
            !TryGetTransform(target, out var targetTransform))
        {
            return false;
        }

        if (userTransform.ParentUid == target || targetTransform.ParentUid == user)
            return true;

        var userContained = _containers.TryGetContainingContainer(user, out _);
        var targetContained = _containers.TryGetContainingContainer(target, out _);
        return (userContained || targetContained) &&
               _containers.IsInSameOrParentContainer(user, target);
    }

    private bool TryGetContext(EntityUid uid, out InteractionSpatialContext context)
    {
        context = default;
        if (!TryGetTransform(uid, out var transform))
            return false;

        context = new InteractionSpatialContext(
            _transform.GetMapCoordinates((uid, transform)),
            transform.GridUid,
            _zLevels.GetZLevel(uid),
            _zLevels.GetWorldZLevel(uid));
        return context.MapCoordinates.MapId != MapId.Nullspace;
    }

    private bool TryGetTransform(EntityUid uid, out TransformComponent transform)
    {
        transform = default!;
        if (!uid.IsValid() ||
            TerminatingOrDeleted(uid) ||
            !_transformQuery.TryComp(uid, out var resolved) ||
            resolved == null ||
            resolved.MapID == MapId.Nullspace)
        {
            return false;
        }

        transform = resolved;
        return true;
    }

    private static float GetDistance(InteractionSpatialContext origin, InteractionSpatialContext target)
    {
        var delta = target.MapCoordinates.Position - origin.MapCoordinates.Position;
        var deltaZ = (double) target.WorldZ - origin.WorldZ;
        return (float) Math.Sqrt(delta.LengthSquared() + deltaZ * deltaZ);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private bool RecordDecision(ZLevelInteractionDecision decision, bool remoteOrigin)
    {
        _metrics.RecordInteractionDecision(decision, remoteOrigin);
        return decision is ZLevelInteractionDecision.SameLevelAllowed or
            ZLevelInteractionDecision.VerticalAllowed;
    }

    private readonly record struct InteractionSpatialContext(
        MapCoordinates MapCoordinates,
        EntityUid? GridUid,
        int LocalZ,
        int WorldZ);
}
