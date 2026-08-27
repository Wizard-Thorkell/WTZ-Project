// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Shared geometric trace primitive for native Z-level consumers.
/// </summary>
public sealed class SharedZLevelTraceSystem : EntitySystem
{
    public const int DefaultMaxVerticalCrossings = 64;
    public const int MaximumMaxVerticalCrossings = 1024;
    public const int DefaultMaxTileVisits = 8192;
    public const int MaximumMaxTileVisits = 1_000_000;
    public const int DefaultMaxEntityHits = 4096;
    public const int MaximumMaxEntityHits = 1_000_000;

    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FixtureSystem _fixtures = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;
    private int _maxVerticalCrossings = DefaultMaxVerticalCrossings;
    private int _maxTileVisits = DefaultMaxTileVisits;
    private int _maxEntityHits = DefaultMaxEntityHits;

    public int MaxVerticalCrossings => _maxVerticalCrossings;
    public int MaxTileVisits => _maxTileVisits;
    public int MaxEntityHits => _maxEntityHits;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTraceMaxVerticalCrossings,
            value => _maxVerticalCrossings = Math.Clamp(value, 1, MaximumMaxVerticalCrossings),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTraceMaxTileVisits,
            value => _maxTileVisits = Math.Clamp(value, 1, MaximumMaxTileVisits),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelTraceMaxEntityHits,
            value => _maxEntityHits = Math.Clamp(value, 1, MaximumMaxEntityHits),
            true);
    }

    public bool TryCreateGridPoint(
        EntityUid gridUid,
        Vector2 localPosition,
        int localZ,
        out ZLevelTracePoint point)
    {
        point = default;
        if (!_gridQuery.HasComp(gridUid) ||
            !TryComp<TransformComponent>(gridUid, out var gridTransform) ||
            gridTransform.MapID == MapId.Nullspace ||
            !IsFinite(localPosition))
        {
            return false;
        }

        var mapCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, localPosition));
        point = new ZLevelTracePoint(
            new ZLevelMapCoordinates(
                mapCoordinates.Position,
                _transform.LocalToWorldZLevel(gridUid, localZ),
                mapCoordinates.MapId),
            gridUid,
            localPosition,
            localZ);
        return true;
    }

    public ZLevelTraceResult Trace(in ZLevelTraceRequest request)
    {
        var buffer = new ZLevelTraceBuffer();
        var result = Trace(request, buffer);
        return ToImmutableResult(result, buffer);
    }

    public ZLevelTraceBufferResult Trace(in ZLevelTraceRequest request, ZLevelTraceBuffer buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        buffer.Clear();

        if (!TryNormalizePoint(request.Origin, out var normalizedOrigin) ||
            !TryNormalizePoint(request.Destination, out var normalizedDestination))
        {
            return EmptyResult(ZLevelTraceTermination.InvalidCoordinates, request.Origin);
        }

        var originWorld = normalizedOrigin.WorldCoordinates;
        var destinationWorld = normalizedDestination.WorldCoordinates;
        if (originWorld.MapId == MapId.Nullspace ||
            destinationWorld.MapId == MapId.Nullspace ||
            !IsFinite(originWorld.Position) ||
            !IsFinite(destinationWorld.Position))
        {
            return EmptyResult(ZLevelTraceTermination.InvalidCoordinates, request.Origin);
        }

        if (originWorld.MapId != destinationWorld.MapId)
            return EmptyResult(ZLevelTraceTermination.DifferentMaps, normalizedOrigin);

        var frameUid = request.BoundaryFrameUid;
        if (frameUid == null &&
            normalizedOrigin.GridUid is { } commonFrame &&
            normalizedDestination.GridUid == commonFrame)
        {
            frameUid = commonFrame;
        }

        if (frameUid is { } selectedFrame)
        {
            if (!TryProjectPointToFrame(normalizedOrigin, selectedFrame, out normalizedOrigin) ||
                !TryProjectPointToFrame(normalizedDestination, selectedFrame, out normalizedDestination))
            {
                return EmptyResult(ZLevelTraceTermination.FrameResolutionRequired, normalizedOrigin);
            }
        }

        var normalizedRequest = request with
        {
            Origin = normalizedOrigin,
            Destination = normalizedDestination,
            BoundaryFrameUid = frameUid,
        };
        originWorld = normalizedOrigin.WorldCoordinates;
        destinationWorld = normalizedDestination.WorldCoordinates;

        if (originWorld.Z == destinationWorld.Z)
            return TraceSameLevel(normalizedRequest, buffer);

        if (frameUid is not { } gridUid ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return EmptyResult(ZLevelTraceTermination.FrameResolutionRequired, normalizedOrigin);
        }

        var crossingCount = Math.Abs((long)destinationWorld.Z - originWorld.Z);
        if (crossingCount > _maxVerticalCrossings)
            return EmptyResult(ZLevelTraceTermination.IterationBudgetExceeded, normalizedOrigin);

        return TraceVerticalSameFrame(normalizedRequest, gridUid, grid, (int)crossingCount, buffer);
    }

    private bool TryNormalizePoint(ZLevelTracePoint point, out ZLevelTracePoint normalized)
    {
        normalized = point;
        if (point.GridUid is not { } gridUid)
            return true;

        if (!_gridQuery.HasComp(gridUid) ||
            !TryComp<TransformComponent>(gridUid, out var gridTransform) ||
            gridTransform.MapID == MapId.Nullspace ||
            !IsFinite(point.LocalPosition))
        {
            return false;
        }

        var mapCoordinates = _transform.ToMapCoordinates(new EntityCoordinates(gridUid, point.LocalPosition));
        normalized = CreateGridPoint(
            gridUid,
            point.LocalPosition,
            point.LocalZ,
            mapCoordinates.Position,
            _transform.LocalToWorldZLevel(gridUid, point.LocalZ),
            mapCoordinates.MapId);
        return true;
    }

    private bool TryProjectPointToFrame(
        ZLevelTracePoint point,
        EntityUid frameUid,
        out ZLevelTracePoint projected)
    {
        projected = default;
        if (!_gridQuery.HasComp(frameUid) ||
            !TryComp<TransformComponent>(frameUid, out var frameTransform) ||
            frameTransform.MapID == MapId.Nullspace ||
            frameTransform.MapID != point.WorldCoordinates.MapId)
        {
            return false;
        }

        if (point.GridUid == frameUid)
        {
            projected = point;
            return true;
        }

        var local = _transform.ToCoordinates(
            (frameUid, frameTransform),
            new MapCoordinates(point.WorldCoordinates.Position, point.WorldCoordinates.MapId));
        projected = CreateGridPoint(
            frameUid,
            local.Position,
            _transform.WorldToLocalZLevel(frameUid, point.WorldCoordinates.Z),
            point.WorldCoordinates.Position,
            point.WorldCoordinates.Z,
            point.WorldCoordinates.MapId);
        return true;
    }

    private ZLevelTraceBufferResult TraceSameLevel(
        in ZLevelTraceRequest request,
        ZLevelTraceBuffer buffer)
    {
        var length = GetTraceLength(request.Origin.WorldCoordinates, request.Destination.WorldCoordinates);
        if (!TryAppendSegment(
                buffer,
                request,
                request.Origin,
                request.Destination,
                0f,
                length))
        {
            return BuildResult(
                ZLevelTraceTermination.IterationBudgetExceeded,
                request.Origin,
                buffer);
        }

        return BuildResult(ZLevelTraceTermination.Completed, request.Destination, buffer);
    }

    private ZLevelTraceBufferResult TraceVerticalSameFrame(
        in ZLevelTraceRequest request,
        EntityUid gridUid,
        MapGridComponent grid,
        int crossingCount,
        ZLevelTraceBuffer buffer)
    {
        var origin = request.Origin;
        var destination = request.Destination;
        var originWorld = origin.WorldCoordinates;
        var destinationWorld = destination.WorldCoordinates;
        var worldDeltaZ = destinationWorld.Z - originWorld.Z;
        var step = Math.Sign(worldDeltaZ);
        var totalLength = GetTraceLength(originWorld, destinationWorld);
        var currentPoint = origin;
        var currentDistance = 0f;

        for (var i = 0; i < crossingCount; i++)
        {
            var fromWorldZ = originWorld.Z + step * i;
            var toWorldZ = fromWorldZ + step;
            var fromLocalZ = origin.LocalZ + step * i;
            var toLocalZ = fromLocalZ + step;
            var boundaryHeight = fromWorldZ + step * 0.5f;
            var interpolation = (boundaryHeight - originWorld.Z) / worldDeltaZ;
            var worldPosition = Vector2.Lerp(
                originWorld.Position,
                destinationWorld.Position,
                interpolation);
            var localPosition = Vector2.Lerp(
                origin.LocalPosition,
                destination.LocalPosition,
                interpolation);
            var crossingDistance = interpolation * totalLength;
            var pointBefore = CreateGridPoint(
                gridUid,
                localPosition,
                fromLocalZ,
                worldPosition,
                fromWorldZ,
                originWorld.MapId);

            if (!TryAppendSegment(
                    buffer,
                    request,
                    currentPoint,
                    pointBefore,
                    currentDistance,
                    crossingDistance))
            {
                return BuildResult(
                    ZLevelTraceTermination.IterationBudgetExceeded,
                    currentPoint,
                    buffer);
            }

            var tile = GetTile(localPosition, grid.TileSize);
            if (!_boundaries.TryGetBoundary(
                    gridUid,
                    grid,
                    tile,
                    fromLocalZ,
                    toLocalZ,
                    out var state))
            {
                return BuildResult(ZLevelTraceTermination.InvalidCoordinates, pointBefore, buffer);
            }

            var isOpen = state.IsOpen(request.BoundaryChannels);
            buffer.MutableBoundaryCrossings.Add(new ZLevelTraceBoundaryCrossing(
                buffer.MutableBoundaryCrossings.Count,
                gridUid,
                tile,
                fromLocalZ,
                toLocalZ,
                fromWorldZ,
                toWorldZ,
                buffer.MutableSegments.Count - 1,
                crossingDistance,
                state,
                isOpen));
            if (!isOpen)
                return BuildResult(ZLevelTraceTermination.ClosedBoundary, pointBefore, buffer);

            currentPoint = CreateGridPoint(
                gridUid,
                localPosition,
                toLocalZ,
                worldPosition,
                toWorldZ,
                originWorld.MapId);
            currentDistance = crossingDistance;
        }

        if (!TryAppendSegment(
                buffer,
                request,
                currentPoint,
                destination,
                currentDistance,
                totalLength))
        {
            return BuildResult(
                ZLevelTraceTermination.IterationBudgetExceeded,
                currentPoint,
                buffer);
        }

        return BuildResult(ZLevelTraceTermination.Completed, destination, buffer);
    }

    private bool TryAppendSegment(
        ZLevelTraceBuffer buffer,
        in ZLevelTraceRequest request,
        ZLevelTracePoint start,
        ZLevelTracePoint end,
        float startDistance,
        float endDistance)
    {
        var bookmark = buffer.Bookmark();
        var segmentSequence = buffer.MutableSegments.Count;
        if ((request.Options & ZLevelTraceOptions.IncludeTileVisits) != 0 &&
            !TryAppendSameFrameTiles(
                buffer.MutableTileVisits,
                start,
                end,
                segmentSequence,
                startDistance,
                endDistance))
        {
            buffer.Rollback(bookmark);
            return false;
        }

        var frameUid = start.GridUid == end.GridUid ? start.GridUid : null;
        buffer.MutableSegments.Add(new ZLevelTraceSegment(
            segmentSequence,
            start,
            end,
            frameUid,
            startDistance,
            endDistance));

        if ((request.Options & ZLevelTraceOptions.IncludeEntityHits) != 0 &&
            request.CollisionMask != 0 &&
            !TryAppendSameLevelEntityHits(
                buffer,
                request,
                start,
                end,
                segmentSequence,
                startDistance,
                endDistance))
        {
            buffer.Rollback(bookmark);
            return false;
        }

        return true;
    }

    private bool TryAppendSameLevelEntityHits(
        ZLevelTraceBuffer buffer,
        in ZLevelTraceRequest request,
        ZLevelTracePoint start,
        ZLevelTracePoint end,
        int segmentSequence,
        float startDistance,
        float endDistance)
    {
        var startWorld = start.WorldCoordinates;
        var delta = end.WorldCoordinates.Position - startWorld.Position;
        var twoDimensionalLength = delta.Length();
        if (twoDimensionalLength <= 0f)
        {
            if (endDistance > startDistance)
            {
                return TryAppendPointEntityHits(
                    buffer,
                    request,
                    startWorld,
                    segmentSequence,
                    startDistance);
            }

            return true;
        }

        var ray = new CollisionRay(
            startWorld.Position,
            delta / twoDimensionalLength,
            request.CollisionMask);
        var filter = new EntityFilter(this, request.IgnoredEntity, startWorld.Z);
        var physicsHits = _physics.IntersectRayWithPredicate(
            startWorld.MapId,
            ray,
            filter,
            static (entity, state) => state.System.ShouldIgnoreEntity(entity, state),
            twoDimensionalLength,
            false);
        var hits = buffer.PhysicsHits;
        hits.Clear();
        foreach (var hit in physicsHits)
        {
            hits.Add(hit);
        }

        hits.Sort(static (left, right) =>
        {
            var distance = left.Distance.CompareTo(right.Distance);
            return distance != 0 ? distance : left.HitEntity.CompareTo(right.HitEntity);
        });

        if (buffer.MutableEntityHits.Count > _maxEntityHits - hits.Count)
            return false;

        foreach (var hit in hits)
        {
            var interpolation = Math.Clamp(hit.Distance / twoDimensionalLength, 0f, 1f);
            buffer.MutableEntityHits.Add(new ZLevelTraceEntityHit(
                0,
                hit.HitEntity,
                new ZLevelMapCoordinates(hit.HitPos, startWorld.Z, startWorld.MapId),
                segmentSequence,
                Lerp(startDistance, endDistance, interpolation)));
        }

        return true;
    }

    private bool TryAppendPointEntityHits(
        ZLevelTraceBuffer buffer,
        in ZLevelTraceRequest request,
        ZLevelMapCoordinates point,
        int segmentSequence,
        float distance)
    {
        var candidates = buffer.PointCandidates;
        candidates.Clear();
        _lookup.GetEntitiesInRange(
            point.MapId,
            point.Position,
            EntityLookupSystem.LookupEpsilon,
            candidates,
            LookupFlags.Dynamic | LookupFlags.Static);
        var hits = buffer.PointHits;
        hits.Clear();
        foreach (var entity in candidates)
        {
            if (ShouldIgnoreEntity(entity, new EntityFilter(this, request.IgnoredEntity, point.Z)) ||
                !TryComp<FixturesComponent>(entity, out var fixtures))
            {
                continue;
            }

            var physicsTransform = _physics.GetPhysicsTransform(entity);
            foreach (var fixture in fixtures.Fixtures.Values)
            {
                if (!fixture.Hard ||
                    (fixture.CollisionLayer & request.CollisionMask) == 0 ||
                    !_fixtures.TestPoint(fixture.Shape, physicsTransform, point.Position))
                {
                    continue;
                }

                hits.Add(entity);
                break;
            }
        }

        hits.Sort();
        if (buffer.MutableEntityHits.Count > _maxEntityHits - hits.Count)
            return false;

        foreach (var entity in hits)
        {
            buffer.MutableEntityHits.Add(new ZLevelTraceEntityHit(
                0,
                entity,
                point,
                segmentSequence,
                distance));
        }

        return true;
    }

    private bool TryAppendSameFrameTiles(
        List<ZLevelTraceTileVisit> output,
        ZLevelTracePoint startPoint,
        ZLevelTracePoint endPoint,
        int segmentSequence,
        float startDistance,
        float endDistance)
    {
        if (startPoint.GridUid is not { } gridUid ||
            endPoint.GridUid != gridUid ||
            startPoint.LocalZ != endPoint.LocalZ ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return true;
        }

        var start = startPoint.LocalPosition;
        var end = endPoint.LocalPosition;
        var delta = end - start;
        var tileSize = grid.TileSize;
        var current = GetTile(start, tileSize);
        var endTile = GetTile(end, tileSize);
        if (!TryAddTileVisit(
                output,
                gridUid,
                current,
                startPoint.LocalZ,
                startPoint.WorldCoordinates.Z,
                segmentSequence,
                startDistance))
        {
            return false;
        }

        if (current == endTile)
            return true;

        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : tileSize / MathF.Abs(delta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : tileSize / MathF.Abs(delta.Y);
        var nextBoundaryX = (current.X + (stepX > 0 ? 1 : 0)) * tileSize;
        var nextBoundaryY = (current.Y + (stepY > 0 ? 1 : 0)) * tileSize;
        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - start.X) / delta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - start.Y) / delta.Y;
        var maxSteps = Math.Abs((long) endTile.X - current.X) +
                       Math.Abs((long) endTile.Y - current.Y) +
                       1L;

        for (long i = 0; current != endTile && i < maxSteps; i++)
        {
            float entryInterpolation;
            if (tMaxX < tMaxY)
            {
                current = new Vector2i(current.X + stepX, current.Y);
                entryInterpolation = tMaxX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                current = new Vector2i(current.X, current.Y + stepY);
                entryInterpolation = tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                current = new Vector2i(current.X + stepX, current.Y + stepY);
                entryInterpolation = tMaxX;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }

            if (!TryAddTileVisit(
                    output,
                    gridUid,
                    current,
                    startPoint.LocalZ,
                    startPoint.WorldCoordinates.Z,
                    segmentSequence,
                    Lerp(startDistance, endDistance, Math.Clamp(entryInterpolation, 0f, 1f))))
            {
                return false;
            }
        }

        return current == endTile;
    }

    private bool TryAddTileVisit(
        List<ZLevelTraceTileVisit> output,
        EntityUid gridUid,
        Vector2i tile,
        int localZ,
        int worldZ,
        int segmentSequence,
        float entryDistance)
    {
        if (output.Count >= _maxTileVisits)
            return false;

        output.Add(new ZLevelTraceTileVisit(
            output.Count,
            gridUid,
            new ZLevelTileIndices(tile.X, tile.Y, localZ),
            worldZ,
            segmentSequence,
            entryDistance));
        return true;
    }

    private bool ShouldIgnoreEntity(EntityUid entity, EntityFilter filter)
    {
        if (entity == filter.IgnoredEntity || !TryComp<TransformComponent>(entity, out var transform))
            return true;

        return _transform.GetWorldZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity))) !=
               filter.WorldZ;
    }

    private static ZLevelTracePoint CreateGridPoint(
        EntityUid gridUid,
        Vector2 localPosition,
        int localZ,
        Vector2 worldPosition,
        int worldZ,
        MapId mapId)
    {
        return new ZLevelTracePoint(
            new ZLevelMapCoordinates(worldPosition, worldZ, mapId),
            gridUid,
            localPosition,
            localZ);
    }

    private static Vector2i GetTile(Vector2 position, float tileSize)
    {
        return new Vector2i(
            (int)MathF.Floor(position.X / tileSize),
            (int)MathF.Floor(position.Y / tileSize));
    }

    private static float GetTraceLength(
        ZLevelMapCoordinates origin,
        ZLevelMapCoordinates destination)
    {
        var delta = destination.Position - origin.Position;
        var deltaZ = (double)destination.Z - origin.Z;
        return (float)Math.Sqrt(delta.LengthSquared() + deltaZ * deltaZ);
    }

    private static float Lerp(float start, float end, float amount)
    {
        return start + (end - start) * amount;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static ZLevelTraceBufferResult EmptyResult(
        ZLevelTraceTermination termination,
        ZLevelTracePoint finalPoint)
    {
        return new ZLevelTraceBufferResult(termination, finalPoint);
    }

    private static ZLevelTraceBufferResult BuildResult(
        ZLevelTraceTermination termination,
        ZLevelTracePoint finalPoint,
        ZLevelTraceBuffer buffer)
    {
        buffer.MutableEntityHits.Sort(static (left, right) =>
        {
            var distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
                return distance;

            var entity = left.Entity.CompareTo(right.Entity);
            return entity != 0 ? entity : left.SegmentSequence.CompareTo(right.SegmentSequence);
        });
        for (var i = 0; i < buffer.MutableEntityHits.Count; i++)
        {
            var hit = buffer.MutableEntityHits[i];
            buffer.MutableEntityHits[i] = hit with { Sequence = i };
        }

        return new ZLevelTraceBufferResult(termination, finalPoint);
    }

    private static ZLevelTraceResult ToImmutableResult(
        ZLevelTraceBufferResult result,
        ZLevelTraceBuffer buffer)
    {
        return new ZLevelTraceResult(
            result.Termination,
            result.FinalPoint,
            buffer.MutableSegments.ToImmutableArray(),
            buffer.MutableTileVisits.ToImmutableArray(),
            buffer.MutableEntityHits.ToImmutableArray(),
            buffer.MutableBoundaryCrossings.ToImmutableArray());
    }

    private readonly record struct EntityFilter(
        SharedZLevelTraceSystem System,
        EntityUid? IgnoredEntity,
        int WorldZ);

}
