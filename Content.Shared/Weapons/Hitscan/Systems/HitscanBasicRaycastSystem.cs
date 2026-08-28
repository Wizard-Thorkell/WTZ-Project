using System.Numerics;
using Content.Shared.Administration.Logs;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Hitscan.Systems;

public sealed class HitscanBasicRaycastSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly ISharedAdminLogManager _log = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly SharedZLevelTraceSystem _zTrace = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _zVisibility = default!;

    [Dependency] private readonly EntityQuery<HitscanBasicVisualsComponent> _visualsQuery = default!;

    private readonly ZLevelTraceBuffer _traceBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HitscanBasicRaycastComponent, HitscanTraceEvent>(OnHitscanFired);
        _traceBuffer.EnsureCapacity(8, 0, 32, 8);
    }

    private void OnHitscanFired(Entity<HitscanBasicRaycastComponent> ent, ref HitscanTraceEvent args)
    {
        var shooter = args.Shooter ?? args.Gun;
        ZLevelTraceEntityHit? hit = null;
        var distanceTried = 0f;
        if (TryCreateTraceRequest(ent.Comp, args, shooter, out var request))
        {
            _zTrace.Trace(request, _traceBuffer);
            hit = SelectHit(shooter, args.Target, _traceBuffer.EntityHits);
            distanceTried = hit?.Distance ?? GetTraceDistance(_traceBuffer);

            // Consume buffered trace output before reflection can raise this event recursively.
            FireEffects(
                _traceBuffer.Segments,
                distanceTried,
                hit?.SegmentSequence ?? GetStopSegment(_traceBuffer),
                args.ShotDirection.ToAngle(),
                ent.Owner);
        }

        // Admin logging
        if (hit is { } selected)
        {
            _log.Add(LogType.HitScanHit,
                $"{ToPrettyString(shooter):user} hit {ToPrettyString(selected.Entity):target}"
                + $" using {ToPrettyString(args.Gun):entity}.");
        }

        var data = new HitscanRaycastFiredData
        {
            ShotDirection = args.ShotDirection,
            Gun = args.Gun,
            Shooter = args.Shooter,
            HitEntity = hit?.Entity,
        };

        var attemptEvent = new AttemptHitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref attemptEvent);

        if (attemptEvent.Cancelled)
            return;

        var hitEvent = new HitscanRaycastFiredEvent { Data = data };
        RaiseLocalEvent(ent, ref hitEvent);
    }

    private bool TryCreateTraceRequest(
        HitscanBasicRaycastComponent component,
        HitscanTraceEvent args,
        EntityUid shooter,
        out ZLevelTraceRequest request)
    {
        request = default;
        var originMap = _transform.ToMapCoordinates(args.FromCoordinates);
        if (originMap.MapId == MapId.Nullspace ||
            !TryComp(shooter, out TransformComponent? shooterTransform) ||
            shooterTransform.MapID != originMap.MapId ||
            !IsFinite(originMap.Position) ||
            !IsFinite(args.ShotDirection) ||
            !float.IsFinite(component.MaxDistance))
        {
            return false;
        }

        var maxDistance = Math.Max(0f, component.MaxDistance);
        var direction = args.ShotDirection;
        var directionLength = direction.Length();
        if (!float.IsFinite(directionLength))
            return false;

        if (directionLength > 0f)
            direction /= directionLength;

        var originWorldZ = _zLevels.GetWorldZLevel(shooter);
        var destinationPosition = originMap.Position + direction * maxDistance;
        var destinationWorldZ = originWorldZ;
        var frameUid = shooterTransform.GridUid;

        if (args.Target is { } target)
        {
            if (!TryComp(target, out TransformComponent? targetTransform) ||
                targetTransform.MapID != originMap.MapId)
            {
                return false;
            }

            var targetWorldZ = _zLevels.GetWorldZLevel(target);
            if (args.TargetWorldZ is { } selectedWorldZ && selectedWorldZ != targetWorldZ)
                return false;

            if (targetWorldZ != originWorldZ)
            {
                if (frameUid is not { } commonFrame ||
                    targetTransform.GridUid != commonFrame ||
                    !_zVisibility.IsEntityVisibleFrom(target, originMap.MapId, originWorldZ))
                {
                    return false;
                }

                var targetMap = _transform.GetMapCoordinates((target, targetTransform));
                if (!IsFinite(targetMap.Position))
                    return false;

                var planarDistance = Vector2.Distance(originMap.Position, targetMap.Position);
                var verticalDistance = (double) targetWorldZ - originWorldZ;
                var traceDistance = Math.Sqrt(
                    (double) planarDistance * planarDistance + verticalDistance * verticalDistance);
                if (!double.IsFinite(traceDistance) || traceDistance > maxDistance)
                    return false;

                destinationPosition = planarDistance == 0f || directionLength == 0f
                    ? targetMap.Position
                    : originMap.Position + direction * planarDistance;
                destinationWorldZ = targetWorldZ;
            }
        }
        else if (args.TargetWorldZ is { } targetWorldZ && targetWorldZ != originWorldZ)
        {
            if (args.TargetCoordinates is not { } targetCoordinates ||
                !targetCoordinates.IsValid(EntityManager) ||
                frameUid is not { } commonFrame)
            {
                return false;
            }

            var coordinateFrame = HasComp<MapGridComponent>(targetCoordinates.EntityId)
                ? targetCoordinates.EntityId
                : _transform.GetGrid(targetCoordinates);
            var targetMap = _transform.ToMapCoordinates(targetCoordinates);
            if (coordinateFrame != commonFrame ||
                targetMap.MapId != originMap.MapId ||
                !IsFinite(targetMap.Position) ||
                !_zVisibility.IsCoordinateVisibleFrom(
                    targetCoordinates,
                    targetWorldZ,
                    originMap.MapId,
                    originWorldZ))
            {
                return false;
            }

            var planarDistance = Vector2.Distance(originMap.Position, targetMap.Position);
            var verticalDistance = (double) targetWorldZ - originWorldZ;
            var traceDistance = Math.Sqrt(
                (double) planarDistance * planarDistance + verticalDistance * verticalDistance);
            if (!double.IsFinite(traceDistance) || traceDistance > maxDistance)
                return false;

            destinationPosition = planarDistance == 0f || directionLength == 0f
                ? targetMap.Position
                : originMap.Position + direction * planarDistance;
            destinationWorldZ = targetWorldZ;
        }

        if (!TryCreatePoint(originMap, originWorldZ, frameUid, out var origin) ||
            !TryCreatePoint(
                new MapCoordinates(destinationPosition, originMap.MapId),
                destinationWorldZ,
                frameUid,
                out var destination))
        {
            return false;
        }

        request = new ZLevelTraceRequest(
            origin,
            destination,
            ZLevelBoundaryChannels.Projectile,
            (int) component.CollisionMask,
            shooter,
            ZLevelTraceOptions.IncludeEntityHits,
            frameUid);
        return true;
    }

    private bool TryCreatePoint(
        MapCoordinates coordinates,
        int worldZ,
        EntityUid? frameUid,
        out ZLevelTracePoint point)
    {
        if (frameUid is { } gridUid && TryComp(gridUid, out TransformComponent? gridTransform))
        {
            var local = _transform.ToCoordinates((gridUid, gridTransform), coordinates);
            return _zTrace.TryCreateGridPoint(
                gridUid,
                local.Position,
                _transform.WorldToLocalZLevel(gridUid, worldZ),
                out point);
        }

        point = ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
            coordinates.Position,
            worldZ,
            coordinates.MapId));
        return true;
    }

    private ZLevelTraceEntityHit? SelectHit(
        EntityUid shooter,
        EntityUid? target,
        IReadOnlyList<ZLevelTraceEntityHit> hits)
    {
        if (hits.Count == 0)
            return null;

        if (_container.IsEntityOrParentInContainer(shooter))
            return hits[0];

        foreach (var candidate in hits)
        {
            if (candidate.Entity == target ||
                CompOrNull<RequireProjectileTargetComponent>(candidate.Entity)?.Active != true)
            {
                return candidate;
            }
        }

        return null;
    }

    private static float GetTraceDistance(ZLevelTraceBuffer buffer)
    {
        if (buffer.Segments.Count > 0)
            return buffer.Segments[^1].EndDistance;

        if (buffer.BoundaryCrossings.Count > 0)
            return buffer.BoundaryCrossings[^1].Distance;

        return 0f;
    }

    private static int GetStopSegment(ZLevelTraceBuffer buffer)
    {
        return buffer.Segments.Count - 1;
    }

    /// <summary>
    /// Create visual effects for the fired hitscan weapon.
    /// </summary>
    /// <param name="segments">Ordered trace segments to render.</param>
    /// <param name="stopDistance">Cumulative trace distance reached by the shot.</param>
    /// <param name="stopSegment">Segment containing the stop point.</param>
    /// <param name="fallbackWorldAngle">Angle used by a segment with no planar displacement.</param>
    /// <param name="hitscanUid">The hitscan entity itself.</param>
    private void FireEffects(
        IReadOnlyList<ZLevelTraceSegment> segments,
        float stopDistance,
        int stopSegment,
        Angle fallbackWorldAngle,
        EntityUid hitscanUid)
    {
        if (stopDistance <= 0f ||
            stopSegment < 0 ||
            segments.Count == 0 ||
            !_visualsQuery.TryComp(hitscanUid, out var vizComp))
        {
            return;
        }

        var sprites = new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale, int worldZ)>();
        EntityCoordinates? pvsCoordinates = null;
        for (var i = 0; i < segments.Count; i++)
        {
            var segment = segments[i];
            if (i > stopSegment)
                break;

            var distanceSpan = segment.EndDistance - segment.StartDistance;
            var amount = i < stopSegment || distanceSpan <= 0f
                ? 1f
                : Math.Clamp((stopDistance - segment.StartDistance) / distanceSpan, 0f, 1f);
            var segmentDelta = GetSegmentDelta(segment) * amount;
            var planarDistance = segmentDelta.Length();
            var fromCoordinates = GetCoordinates(segment.Start);
            pvsCoordinates ??= fromCoordinates;
            var shotAngle = GetSegmentAngle(segment, segmentDelta, fallbackWorldAngle);
            var stopsHere = i == stopSegment || i == segments.Count - 1;
            AppendEffects(
                sprites,
                fromCoordinates,
                planarDistance,
                shotAngle,
                segment.Start.WorldCoordinates.Z,
                vizComp,
                includeMuzzle: i == 0,
                includeImpact: stopsHere);

            if (stopsHere)
                break;
        }

        if (sprites.Count > 0 && pvsCoordinates is { } pvs)
        {
            RaiseNetworkEvent(new SharedGunSystem.HitscanEvent
            {
                Sprites = sprites,
            }, Filter.Pvs(pvs, entityMan: EntityManager));
        }
    }

    private void AppendEffects(
        List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale, int worldZ)> sprites,
        EntityCoordinates fromCoordinates,
        float distance,
        Angle shotAngle,
        int worldZ,
        HitscanBasicVisualsComponent visuals,
        bool includeMuzzle,
        bool includeImpact)
    {
        if (distance >= 1f)
        {
            if (includeMuzzle && visuals.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, visuals.MuzzleFlash, 1f, worldZ));
            }

            if (visuals.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(shotAngle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, shotAngle, visuals.TravelFlash, distance - 1.5f, worldZ));
            }
        }

        if (includeImpact && visuals.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(shotAngle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, shotAngle.FlipPositive(), visuals.ImpactFlash, 1f, worldZ));
        }
    }

    private EntityCoordinates GetCoordinates(ZLevelTracePoint point)
    {
        if (point.GridUid is { } gridUid)
            return new EntityCoordinates(gridUid, point.LocalPosition);

        return _transform.ToCoordinates(new MapCoordinates(
            point.WorldCoordinates.Position,
            point.WorldCoordinates.MapId));
    }

    private Angle GetSegmentAngle(
        ZLevelTraceSegment segment,
        Vector2 segmentDelta,
        Angle fallbackWorldAngle)
    {
        if (segmentDelta != Vector2.Zero)
            return segmentDelta.ToAngle();

        if (segment.FrameUid is { } frameUid && TryComp(frameUid, out TransformComponent? frameTransform))
            return fallbackWorldAngle - _transform.GetWorldRotation(frameTransform);

        return fallbackWorldAngle;
    }

    private static Vector2 GetSegmentDelta(ZLevelTraceSegment segment)
    {
        if (segment.FrameUid != null)
            return segment.End.LocalPosition - segment.Start.LocalPosition;

        return segment.End.WorldCoordinates.Position - segment.Start.WorldCoordinates.Position;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}
