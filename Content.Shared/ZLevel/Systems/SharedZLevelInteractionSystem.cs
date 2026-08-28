// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Selects how an entity-to-entity interaction may cross world Z levels.
/// </summary>
public enum ZLevelInteractionPolicy : byte
{
    /// <summary>
    /// The effective interaction origin and target must occupy the same world Z level.
    /// </summary>
    SameWorldLevel,

    /// <summary>
    /// Different world Z levels require a completed trace through open Interaction boundaries.
    /// </summary>
    OpenBoundaryTrace,
}

/// <summary>
/// Central spatial authority for direct and explicitly vertical interactions.
/// Gameplay interaction rules remain owned by their normal systems.
/// </summary>
public sealed class SharedZLevelInteractionSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly SharedZLevelTraceSystem _trace = default!;

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
        return TryGetContext(first, out var firstContext) &&
               TryGetContext(second, out var secondContext) &&
               firstContext.MapCoordinates.MapId == secondContext.MapCoordinates.MapId &&
               firstContext.WorldZ == secondContext.WorldZ;
    }

    /// <summary>
    /// Applies the selected vertical policy from the user's effective spatial origin to a target.
    /// A positive range is measured in combined XY and discrete-Z units.
    /// </summary>
    public bool CanInteract(
        EntityUid user,
        EntityUid target,
        ZLevelInteractionPolicy policy = ZLevelInteractionPolicy.SameWorldLevel,
        float maximumRange = 0f)
    {
        if (!TryGetSpatialOrigin(user, target, out var origin) ||
            !TryGetContext(origin, out var originContext) ||
            !TryGetContext(target, out var targetContext) ||
            originContext.MapCoordinates.MapId != targetContext.MapCoordinates.MapId)
        {
            return false;
        }

        if (maximumRange > 0f && GetDistance(originContext, targetContext) > maximumRange)
            return false;

        if (originContext.WorldZ == targetContext.WorldZ)
            return true;

        if (policy != ZLevelInteractionPolicy.OpenBoundaryTrace ||
            originContext.GridUid is not { } frameUid ||
            targetContext.GridUid != frameUid ||
            !_transformQuery.TryComp(frameUid, out var frameTransform))
        {
            return false;
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
            return false;
        }

        var request = new ZLevelTraceRequest(
            traceOrigin,
            traceTarget,
            ZLevelBoundaryChannels.Interaction,
            Options: ZLevelTraceOptions.None,
            BoundaryFrameUid: frameUid);
        return _trace.Trace(request, _traceBuffer).ReachedDestination;
    }

    public bool CanDirectlyInteract(EntityUid user, EntityUid target)
    {
        return CanInteract(user, target);
    }

    public bool CanInteractThroughOpenBoundary(EntityUid user, EntityUid target, float maximumRange = 0f)
    {
        return CanInteract(user, target, ZLevelInteractionPolicy.OpenBoundaryTrace, maximumRange);
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

    private readonly record struct InteractionSpatialContext(
        MapCoordinates MapCoordinates,
        EntityUid? GridUid,
        int LocalZ,
        int WorldZ);
}
