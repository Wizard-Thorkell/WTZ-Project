// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Controllers;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Advances opt-in vertical ballistic routes at bounded physics-substep
/// crossings while leaving horizontal collision response to Robust physics.
/// </summary>
public sealed class SharedZLevelBallisticSystem : VirtualController
{
    private enum RouteTermination : byte
    {
        Completed,
        ClosedBoundary,
        Collision,
        Invalid,
    }

    private const float ProgressTolerance = 0.001f;
    private const float MinimumVelocityScale = 0.05f;
    private const float VelocityToleranceSquared = 0.0001f;

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly SharedZLevelTraceSystem _trace = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<PhysicsComponent> _physicsQuery;
    private EntityQuery<ProjectileComponent> _projectileQuery;
    private EntityQuery<ThrownItemComponent> _thrownQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    private readonly ZLevelTraceBuffer _traceBuffer = new();
    private readonly List<EntityUid> _crossingBuffer = new();

    public override void Initialize()
    {
        UpdatesAfter.Add(typeof(SharedZLevelSystem));
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        _projectileQuery = GetEntityQuery<ProjectileComponent>();
        _thrownQuery = GetEntityQuery<ThrownItemComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ZLevelBallisticTrajectoryComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<ZLevelBallisticTrajectoryComponent, ComponentShutdown>(OnShutdown);
    }

    /// <summary>
    /// Starts a fixed launch-time route toward a server-resolved target floor.
    /// The planar displacement already includes range clamping, recoil, or spread.
    /// </summary>
    public bool TryStartTrajectory(EntityUid uid, EntityUid target, Vector2 mapDisplacement)
    {
        _metrics.RecordBallisticRouteAttempt();

        if (!_physicsQuery.TryComp(uid, out var physics) ||
            !_transformQuery.TryComp(uid, out var transform) ||
            !_transformQuery.TryComp(target, out var targetTransform) ||
            transform.GridUid is not { } frameUid ||
            targetTransform.GridUid != frameUid ||
            transform.MapID == MapId.Nullspace ||
            targetTransform.MapID != transform.MapID)
        {
            return false;
        }

        var sourceWorldZ = _zLevels.GetWorldZLevel(uid);
        var targetWorldZ = _zLevels.GetWorldZLevel(target);
        if (sourceWorldZ == targetWorldZ ||
            !_visibility.IsEntityVisibleFrom(target, transform.MapID, sourceWorldZ))
        {
            return false;
        }

        var sourceLocalZ = _zLevels.GetZLevel(uid);
        var targetLocalZ = _zLevels.GetZLevel(target);
        var sourceLocalZOffset = GetSourceTraceZOffset(uid, frameUid, sourceLocalZ);
        var targetLocalZOffset = _zLevels.GetFlightTraceZOffset(target);
        var sourceMap = TransformSystem.GetMapCoordinates((uid, transform));
        var targetMap = TransformSystem.GetMapCoordinates((target, targetTransform));
        if (!IsFinite(sourceMap.Position) || !IsFinite(targetMap.Position))
            return false;

        return TryStartTrajectory(
            uid,
            mapDisplacement,
            physics,
            transform,
            frameUid,
            sourceLocalZ,
            sourceLocalZOffset,
            targetLocalZ,
            targetLocalZOffset);
    }

    /// <summary>
    /// Starts a fixed launch-time route toward an explicitly selected lower-floor surface.
    /// </summary>
    public bool TryStartTrajectory(
        EntityUid uid,
        EntityCoordinates targetCoordinates,
        int targetWorldZ,
        Vector2 mapDisplacement)
    {
        _metrics.RecordBallisticRouteAttempt();

        if (!targetCoordinates.IsValid(EntityManager) ||
            !_physicsQuery.TryComp(uid, out var physics) ||
            !_transformQuery.TryComp(uid, out var transform) ||
            transform.GridUid is not { } frameUid ||
            transform.MapID == MapId.Nullspace)
        {
            return false;
        }

        var targetFrame = _gridQuery.HasComp(targetCoordinates.EntityId)
            ? targetCoordinates.EntityId
            : TransformSystem.GetGrid(targetCoordinates);
        var targetMap = TransformSystem.ToMapCoordinates(targetCoordinates);
        var sourceWorldZ = _zLevels.GetWorldZLevel(uid);
        if (targetFrame != frameUid ||
            targetMap.MapId != transform.MapID ||
            !IsFinite(targetMap.Position) ||
            sourceWorldZ == targetWorldZ ||
            !_visibility.IsCoordinateVisibleFrom(
                targetCoordinates,
                targetWorldZ,
                transform.MapID,
                sourceWorldZ))
        {
            return false;
        }

        return TryStartTrajectory(
            uid,
            mapDisplacement,
            physics,
            transform,
            frameUid,
            _zLevels.GetZLevel(uid),
            GetSourceTraceZOffset(uid, frameUid, _zLevels.GetZLevel(uid)),
            TransformSystem.WorldToLocalZLevel(frameUid, targetWorldZ),
            ZLevelTracePoint.DefaultZOffset);
    }

    private bool TryStartTrajectory(
        EntityUid uid,
        Vector2 mapDisplacement,
        PhysicsComponent physics,
        TransformComponent transform,
        EntityUid frameUid,
        int sourceLocalZ,
        float sourceLocalZOffset,
        int targetLocalZ,
        float targetLocalZOffset)
    {
        if (HasComp<ZLevelBallisticTrajectoryComponent>(uid) ||
            !_gridQuery.HasComp(frameUid) ||
            (physics.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) == 0 ||
            !IsFinite(mapDisplacement) ||
            !IsValidZOffset(sourceLocalZOffset) ||
            !IsValidZOffset(targetLocalZOffset) ||
            !IsActiveBallistic(uid))
        {
            return false;
        }

        var directionLength = mapDisplacement.Length();
        if (!float.IsFinite(directionLength) || directionLength <= 0f)
            return false;

        var crossingCount = Math.Abs((long) targetLocalZ - sourceLocalZ);
        if (crossingCount == 0 || crossingCount > _trace.MaxVerticalCrossings)
            return false;

        var sourceMap = TransformSystem.GetMapCoordinates((uid, transform));
        if (!IsFinite(sourceMap.Position))
            return false;

        var planarDistance = directionLength;
        if (!float.IsFinite(planarDistance) || planarDistance <= 0f)
            return false;

        var destinationMap = new MapCoordinates(
            sourceMap.Position + mapDisplacement,
            sourceMap.MapId);
        if (!_transformQuery.TryComp(frameUid, out var frameTransform))
            return false;

        var origin = TransformSystem.ToCoordinates((frameUid, frameTransform), sourceMap).Position;
        var destination = TransformSystem.ToCoordinates((frameUid, frameTransform), destinationMap).Position;
        var localDelta = destination - origin;
        var localDistance = localDelta.Length();
        if (!float.IsFinite(localDistance) || localDistance <= 0f || !IsFinite(localDelta))
            return false;

        if (!_trace.TryCreateGridPoint(
                frameUid,
                origin,
                sourceLocalZ,
                sourceLocalZOffset,
                out var traceOrigin) ||
            !_trace.TryCreateGridPoint(
                frameUid,
                destination,
                targetLocalZ,
                targetLocalZOffset,
                out var traceDestination))
        {
            return false;
        }

        var request = new ZLevelTraceRequest(
            traceOrigin,
            traceDestination,
            ZLevelBoundaryChannels.Projectile,
            IgnoredEntity: uid,
            Options: ZLevelTraceOptions.None,
            BoundaryFrameUid: frameUid);
        var result = _trace.Trace(request, _traceBuffer);
        if (result.Termination is not (ZLevelTraceTermination.Completed or ZLevelTraceTermination.ClosedBoundary))
            return false;

        if (!_zLevels.SetZLevelPosition(uid, sourceLocalZ, sourceLocalZOffset))
            return false;

        var trajectory = EnsureComp<ZLevelBallisticTrajectoryComponent>(uid);
        trajectory.FrameUid = frameUid;
        trajectory.Origin = origin;
        trajectory.Direction = localDelta / localDistance;
        trajectory.PlanarDistance = localDistance;
        trajectory.SourceLocalZ = sourceLocalZ;
        trajectory.SourceLocalZOffset = sourceLocalZOffset;
        trajectory.TargetLocalZ = targetLocalZ;
        trajectory.TargetLocalZOffset = targetLocalZOffset;
        trajectory.NextCrossing = 0;
        trajectory.PendingCrossing = false;
        trajectory.CollisionDuringStep = false;
        trajectory.Ending = false;
        trajectory.NominalMapVelocity = Vector2.Zero;
        trajectory.StepMapVelocity = Vector2.Zero;
        trajectory.NominalLinearVelocity = Vector2.Zero;
        trajectory.StepLinearVelocity = Vector2.Zero;
        trajectory.StepDuration = 0f;
        Dirty(uid, trajectory);
        ExtendThrownFlightTime(uid, trajectory, physics, transform);
        _metrics.RecordBallisticRouteStarted();
        return true;
    }

    public override void UpdateBeforeSolve(bool prediction, float frameTime)
    {
        base.UpdateBeforeSolve(prediction, frameTime);

        if (frameTime <= 0f)
            return;

        var query = EntityQueryEnumerator<ZLevelBallisticTrajectoryComponent>();
        while (query.MoveNext(out var uid, out var trajectory))
        {
            if (trajectory.Ending)
                continue;

            if (!_physicsQuery.TryComp(uid, out var physics) ||
                !_transformQuery.TryComp(uid, out var transform))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            if (prediction && !physics.Predict || trajectory.PendingCrossing)
                continue;

            trajectory.CollisionDuringStep = false;
            if (!TryValidateRoute(uid, trajectory, physics, transform, out var frameTransform))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            var currentMap = TransformSystem.GetMapCoordinates((uid, transform));
            var currentLocal = TransformSystem.ToCoordinates(
                (trajectory.FrameUid, frameTransform),
                currentMap).Position;
            var currentProgress = Vector2.Dot(currentLocal - trajectory.Origin, trajectory.Direction);
            var crossingProgress = GetCrossingProgress(trajectory);
            var mapVelocity = PhysicsSystem.GetMapLinearVelocity(uid, physics, transform);
            if (!IsFinite(mapVelocity))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            var predictedMap = new MapCoordinates(
                currentMap.Position + mapVelocity * frameTime,
                currentMap.MapId);
            var predictedLocal = TransformSystem.ToCoordinates(
                (trajectory.FrameUid, frameTransform),
                predictedMap).Position;
            var predictedProgress = Vector2.Dot(predictedLocal - trajectory.Origin, trajectory.Direction);
            var progressDelta = predictedProgress - currentProgress;
            if (progressDelta <= ProgressTolerance ||
                predictedProgress + ProgressTolerance < crossingProgress)
            {
                continue;
            }

            var fraction = Math.Clamp(
                (crossingProgress - currentProgress) / progressDelta,
                0f,
                1f);
            trajectory.NominalMapVelocity = mapVelocity;
            trajectory.StepMapVelocity = mapVelocity * fraction;
            trajectory.NominalLinearVelocity = physics.LinearVelocity;
            trajectory.StepDuration = frameTime;
            trajectory.PendingCrossing = true;
            SetMapVelocity(uid, physics, trajectory.StepMapVelocity, transform);
            trajectory.StepLinearVelocity = physics.LinearVelocity;
        }
    }

    public override void UpdateAfterSolve(bool prediction, float frameTime)
    {
        base.UpdateAfterSolve(prediction, frameTime);

        _crossingBuffer.Clear();
        var query = EntityQueryEnumerator<ZLevelBallisticTrajectoryComponent>();
        while (query.MoveNext(out var uid, out var trajectory))
        {
            if (trajectory.Ending)
                continue;

            if (!_physicsQuery.TryComp(uid, out var physics) ||
                !_transformQuery.TryComp(uid, out var transform))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            if (!trajectory.PendingCrossing || prediction && !physics.Predict)
                continue;

            if (trajectory.CollisionDuringStep ||
                !TryRestoreNominalVelocity(uid, trajectory, physics, transform))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Collision);
                continue;
            }

            if (!TryGetProgress(uid, trajectory, transform, out var progress))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            if (progress + ProgressTolerance < GetCrossingProgress(trajectory))
            {
                trajectory.PendingCrossing = false;
                continue;
            }

            _crossingBuffer.Add(uid);
        }

        if (_crossingBuffer.Count == 0)
            return;

        PhysicsSystem.FlushPendingContacts();
        _metrics.RecordBallisticContactFlush();

        foreach (var uid in _crossingBuffer)
        {
            if (!TryComp(uid, out ZLevelBallisticTrajectoryComponent? trajectory))
                continue;

            if (!_physicsQuery.TryComp(uid, out var physics) ||
                !_transformQuery.TryComp(uid, out var transform))
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            trajectory.PendingCrossing = false;
            if (trajectory.CollisionDuringStep)
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Collision);
                continue;
            }

            if (!TryValidateRoute(uid, trajectory, physics, transform, out _) ||
                _projectileQuery.TryComp(uid, out var projectile) && projectile.ProjectileSpent)
            {
                TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
                continue;
            }

            ResolveCrossing(uid, trajectory, physics, transform);
        }

        _crossingBuffer.Clear();
    }

    private void ResolveCrossing(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        PhysicsComponent physics,
        TransformComponent transform)
    {
        if (!_gridQuery.TryComp(trajectory.FrameUid, out var grid) ||
            !_transformQuery.TryComp(trajectory.FrameUid, out var frameTransform))
        {
            TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
            return;
        }

        var mapCoordinates = TransformSystem.GetMapCoordinates((uid, transform));
        var localCoordinates = TransformSystem.ToCoordinates(
            (trajectory.FrameUid, frameTransform),
            mapCoordinates);
        var tile = _map.TileIndicesFor(trajectory.FrameUid, grid, localCoordinates);
        var step = Math.Sign(trajectory.TargetLocalZ - trajectory.SourceLocalZ);
        var fromLocalZ = trajectory.SourceLocalZ + step * trajectory.NextCrossing;
        var toLocalZ = fromLocalZ + step;

        if (!_boundaries.IsOpen(
                trajectory.FrameUid,
                grid,
                tile,
                fromLocalZ,
                toLocalZ,
                ZLevelBoundaryChannels.Projectile))
        {
            SetMapVelocity(uid, physics, Vector2.Zero, transform);
            var hit = new ZLevelBallisticBoundaryHitEvent(
                trajectory.FrameUid,
                tile,
                fromLocalZ,
                toLocalZ);
            RaiseLocalEvent(uid, ref hit);

            if (_thrownQuery.TryComp(uid, out var thrown))
                _thrown.StopThrow(uid, thrown);

            TerminateTrajectory(uid, trajectory, RouteTermination.ClosedBoundary);
            return;
        }

        var crossingCount = Math.Abs(trajectory.TargetLocalZ - trajectory.SourceLocalZ);
        var destinationOffset = trajectory.NextCrossing + 1 >= crossingCount
            ? trajectory.TargetLocalZOffset
            : step > 0
                ? 0f
                : ZLevelTracePoint.MaximumZOffset;
        if (!_zLevels.SetZLevelPosition(uid, toLocalZ, destinationOffset))
        {
            TerminateTrajectory(uid, trajectory, RouteTermination.Invalid);
            return;
        }

        _metrics.RecordBallisticCrossing();

        ExtendThrownFlightTime(uid, trajectory, physics, transform);

        trajectory.NextCrossing++;
        trajectory.CollisionDuringStep = false;
        if (trajectory.NextCrossing >= crossingCount)
        {
            TerminateTrajectory(uid, trajectory, RouteTermination.Completed);
            return;
        }

        Dirty(uid, trajectory);
    }

    private bool TryValidateRoute(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        PhysicsComponent physics,
        TransformComponent transform,
        out TransformComponent frameTransform)
    {
        frameTransform = default!;
        if (!_transformQuery.TryComp(trajectory.FrameUid, out var resolvedFrameTransform))
            return false;

        frameTransform = resolvedFrameTransform;
        var crossingCount = Math.Abs(trajectory.TargetLocalZ - trajectory.SourceLocalZ);
        var step = Math.Sign(trajectory.TargetLocalZ - trajectory.SourceLocalZ);
        return trajectory.FrameUid.IsValid() &&
               trajectory.PlanarDistance > 0f &&
               IsValidZOffset(trajectory.SourceLocalZOffset) &&
               IsValidZOffset(trajectory.TargetLocalZOffset) &&
               IsFinite(trajectory.Origin) &&
               IsFinite(trajectory.Direction) &&
               trajectory.NextCrossing >= 0 &&
               trajectory.NextCrossing < crossingCount &&
               transform.GridUid == trajectory.FrameUid &&
               frameTransform.MapID == transform.MapID &&
               (physics.BodyType & (BodyType.Dynamic | BodyType.KinematicController)) != 0 &&
               _zLevels.GetZLevel(uid) == trajectory.SourceLocalZ + step * trajectory.NextCrossing &&
               IsActiveBallistic(uid);
    }

    private bool TryGetProgress(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        TransformComponent transform,
        out float progress)
    {
        progress = 0f;
        if (!_transformQuery.TryComp(trajectory.FrameUid, out var frameTransform) ||
            frameTransform.MapID != transform.MapID)
        {
            return false;
        }

        var mapCoordinates = TransformSystem.GetMapCoordinates((uid, transform));
        var localPosition = TransformSystem.ToCoordinates(
            (trajectory.FrameUid, frameTransform),
            mapCoordinates).Position;
        progress = Vector2.Dot(localPosition - trajectory.Origin, trajectory.Direction);
        return float.IsFinite(progress);
    }

    private bool IsActiveBallistic(EntityUid uid)
    {
        return _projectileQuery.TryComp(uid, out var projectile) && !projectile.ProjectileSpent ||
               _thrownQuery.TryComp(uid, out var thrown) && !thrown.Landed;
    }

    private void SetMapVelocity(
        EntityUid uid,
        PhysicsComponent physics,
        Vector2 desiredMapVelocity,
        TransformComponent transform)
    {
        var currentMapVelocity = PhysicsSystem.GetMapLinearVelocity(uid, physics, transform);
        PhysicsSystem.SetLinearVelocity(
            uid,
            physics.LinearVelocity + desiredMapVelocity - currentMapVelocity,
            body: physics);
    }

    private void ExtendThrownFlightTime(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        PhysicsComponent physics,
        TransformComponent transform)
    {
        if (!_thrownQuery.TryComp(uid, out var thrown) ||
            thrown.LandTime is null ||
            !_transformQuery.TryComp(trajectory.FrameUid, out var frameTransform) ||
            !TryGetProgress(uid, trajectory, transform, out var progress))
        {
            return;
        }

        var currentMap = TransformSystem.GetMapCoordinates((uid, transform));
        var mapVelocity = PhysicsSystem.GetMapLinearVelocity(uid, physics, transform);
        var projectedMap = new MapCoordinates(currentMap.Position + mapVelocity, currentMap.MapId);
        var currentLocal = TransformSystem.ToCoordinates(
            (trajectory.FrameUid, frameTransform),
            currentMap).Position;
        var projectedLocal = TransformSystem.ToCoordinates(
            (trajectory.FrameUid, frameTransform),
            projectedMap).Position;
        var progressSpeed = Vector2.Dot(projectedLocal - currentLocal, trajectory.Direction);
        if (!float.IsFinite(progressSpeed) || progressSpeed <= ProgressTolerance)
            return;

        var remainingDistance = MathF.Max(0f, trajectory.PlanarDistance - progress);
        var contactGrace = Math.Max(_timing.TickPeriod.TotalSeconds, trajectory.StepDuration);
        var minimumLandTime = _timing.CurTime + TimeSpan.FromSeconds(
            remainingDistance / progressSpeed + contactGrace);
        if (thrown.LandTime >= minimumLandTime)
            return;

        thrown.LandTime = minimumLandTime;
        Dirty(uid, thrown);
    }

    private bool TryRestoreNominalVelocity(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        PhysicsComponent physics,
        TransformComponent transform)
    {
        if (!TryGetRestoredLinearVelocity(trajectory, physics, out var restored))
            return false;

        PhysicsSystem.SetLinearVelocity(uid, restored, body: physics);
        trajectory.NominalMapVelocity = PhysicsSystem.GetMapLinearVelocity(uid, physics, transform);
        return IsFinite(trajectory.NominalMapVelocity);
    }

    private static bool TryGetRestoredLinearVelocity(
        ZLevelBallisticTrajectoryComponent trajectory,
        PhysicsComponent physics,
        out Vector2 restored)
    {
        restored = Vector2.Zero;
        var solved = physics.LinearVelocity;
        var step = trajectory.StepLinearVelocity;
        if (!IsFinite(solved) || !IsFinite(step) || !IsFinite(trajectory.NominalLinearVelocity))
            return false;

        var stepLengthSquared = step.LengthSquared();
        if (stepLengthSquared <= VelocityToleranceSquared)
        {
            if (solved.LengthSquared() > VelocityToleranceSquared)
                return false;

            restored = trajectory.NominalLinearVelocity;
            return true;
        }

        var scale = Vector2.Dot(solved, step) / stepLengthSquared;
        var residual = solved - step * scale;
        var residualTolerance = VelocityToleranceSquared * MathF.Max(1f, solved.LengthSquared());
        if (!float.IsFinite(scale) ||
            scale < MinimumVelocityScale ||
            residual.LengthSquared() > residualTolerance)
        {
            return false;
        }

        restored = trajectory.NominalLinearVelocity * scale;
        return IsFinite(restored);
    }

    private void OnStartCollide(
        Entity<ZLevelBallisticTrajectoryComponent> entity,
        ref StartCollideEvent args)
    {
        if (!entity.Comp.PendingCrossing || !args.OtherFixture.Hard)
            return;

        if (_projectileQuery.HasComp(entity.Owner))
        {
            if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
                return;
        }
        else if (!args.OurFixture.Hard)
        {
            return;
        }

        entity.Comp.CollisionDuringStep = true;
    }

    private void OnShutdown(
        Entity<ZLevelBallisticTrajectoryComponent> entity,
        ref ComponentShutdown args)
    {
        if (!entity.Comp.PendingCrossing ||
            entity.Comp.CollisionDuringStep ||
            !_physicsQuery.TryComp(entity.Owner, out var physics) ||
            !TryGetRestoredLinearVelocity(entity.Comp, physics, out var restored))
        {
            return;
        }

        PhysicsSystem.SetLinearVelocity(entity.Owner, restored, body: physics);
    }

    private void TerminateTrajectory(
        EntityUid uid,
        ZLevelBallisticTrajectoryComponent trajectory,
        RouteTermination termination)
    {
        if (trajectory.Ending)
            return;

        trajectory.Ending = true;
        trajectory.PendingCrossing = false;
        switch (termination)
        {
            case RouteTermination.Completed:
                _metrics.RecordBallisticRouteCompleted();
                break;
            case RouteTermination.ClosedBoundary:
                _metrics.RecordBallisticClosedBoundary();
                break;
            case RouteTermination.Collision:
                _metrics.RecordBallisticCollisionCancellation();
                break;
            case RouteTermination.Invalid:
                _metrics.RecordBallisticInvalidCancellation();
                break;
        }

        RemCompDeferred<ZLevelBallisticTrajectoryComponent>(uid);
    }

    private static float GetCrossingProgress(ZLevelBallisticTrajectoryComponent trajectory)
    {
        var step = Math.Sign(trajectory.TargetLocalZ - trajectory.SourceLocalZ);
        var sourceHeight = trajectory.SourceLocalZ + (double) trajectory.SourceLocalZOffset;
        var targetHeight = trajectory.TargetLocalZ + (double) trajectory.TargetLocalZOffset;
        var boundaryHeight = step > 0
            ? trajectory.SourceLocalZ + trajectory.NextCrossing + 1d
            : trajectory.SourceLocalZ - trajectory.NextCrossing;
        var interpolation = (boundaryHeight - sourceHeight) / (targetHeight - sourceHeight);
        return trajectory.PlanarDistance * (float) Math.Clamp(interpolation, 0d, 1d);
    }

    private float GetSourceTraceZOffset(EntityUid uid, EntityUid frameUid, int sourceLocalZ)
    {
        EntityUid? source = null;
        if (_projectileQuery.TryComp(uid, out var projectile))
            source = projectile.Shooter;
        else if (_thrownQuery.TryComp(uid, out var thrown))
            source = thrown.Thrower;

        if (source is { } sourceUid &&
            _transformQuery.TryComp(sourceUid, out var sourceTransform) &&
            sourceTransform.GridUid == frameUid &&
            _zLevels.GetZLevel(sourceUid) == sourceLocalZ)
        {
            return _zLevels.GetFlightTraceZOffset(sourceUid);
        }

        return _zLevels.GetFlightTraceZOffset(uid);
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static bool IsValidZOffset(float value)
    {
        return float.IsFinite(value) && value >= 0f && value < 1f;
    }
}
