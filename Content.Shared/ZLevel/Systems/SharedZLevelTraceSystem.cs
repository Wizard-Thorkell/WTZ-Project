// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Shared geometric trace primitive for native Z-level consumers.
/// </summary>
public sealed class SharedZLevelTraceSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();
        _gridQuery = GetEntityQuery<MapGridComponent>();
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

    /// <summary>
    /// Executes the stable same-world-Z reference path. Ordered vertical
    /// crossings are added by P1.2 without changing this request/result shape.
    /// </summary>
    public ZLevelTraceResult Trace(in ZLevelTraceRequest request)
    {
        var origin = request.Origin.WorldCoordinates;
        var destination = request.Destination.WorldCoordinates;
        if (origin.MapId == MapId.Nullspace ||
            destination.MapId == MapId.Nullspace ||
            !IsFinite(origin.Position) ||
            !IsFinite(destination.Position))
        {
            return EmptyResult(ZLevelTraceTermination.InvalidCoordinates, request.Origin);
        }

        if (origin.MapId != destination.MapId)
            return EmptyResult(ZLevelTraceTermination.DifferentMaps, request.Origin);

        if (origin.Z != destination.Z)
            return EmptyResult(ZLevelTraceTermination.VerticalTraversalRequired, request.Origin);

        var delta = destination.Position - origin.Position;
        var length = delta.Length();
        var frameUid = request.Origin.GridUid == request.Destination.GridUid
            ? request.Origin.GridUid
            : null;
        var segments = ImmutableArray.Create(new ZLevelTraceSegment(
            0,
            request.Origin,
            request.Destination,
            frameUid,
            0f,
            length));
        var tiles = (request.Options & ZLevelTraceOptions.IncludeTileVisits) != 0
            ? TraceSameFrameTiles(request, length)
            : ImmutableArray<ZLevelTraceTileVisit>.Empty;
        var entities = (request.Options & ZLevelTraceOptions.IncludeEntityHits) != 0 &&
                       request.CollisionMask != 0 &&
                       length > 0f
            ? TraceSameLevelEntities(request, delta / length, length)
            : ImmutableArray<ZLevelTraceEntityHit>.Empty;

        return new ZLevelTraceResult(
            ZLevelTraceTermination.Completed,
            request.Destination,
            segments,
            tiles,
            entities,
            ImmutableArray<ZLevelTraceBoundaryCrossing>.Empty);
    }

    private ImmutableArray<ZLevelTraceEntityHit> TraceSameLevelEntities(
        in ZLevelTraceRequest request,
        Vector2 direction,
        float length)
    {
        var origin = request.Origin.WorldCoordinates;
        var ray = new CollisionRay(origin.Position, direction, request.CollisionMask);
        var filter = new EntityFilter(this, request.IgnoredEntity, origin.Z);
        var physicsHits = _physics.IntersectRayWithPredicate(
            origin.MapId,
            ray,
            filter,
            static (entity, state) => state.System.ShouldIgnoreEntity(entity, state),
            length,
            false);
        var hits = new List<RayCastResults>();
        foreach (var hit in physicsHits)
        {
            hits.Add(hit);
        }

        hits.Sort(static (left, right) =>
        {
            var distance = left.Distance.CompareTo(right.Distance);
            return distance != 0 ? distance : left.HitEntity.CompareTo(right.HitEntity);
        });

        var result = ImmutableArray.CreateBuilder<ZLevelTraceEntityHit>(hits.Count);
        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            result.Add(new ZLevelTraceEntityHit(
                i,
                hit.HitEntity,
                new ZLevelMapCoordinates(hit.HitPos, origin.Z, origin.MapId),
                0,
                hit.Distance));
        }

        return result.MoveToImmutable();
    }

    private ImmutableArray<ZLevelTraceTileVisit> TraceSameFrameTiles(
        in ZLevelTraceRequest request,
        float worldLength)
    {
        if (request.Origin.GridUid is not { } gridUid ||
            request.Destination.GridUid != gridUid ||
            request.Origin.LocalZ != request.Destination.LocalZ ||
            !_gridQuery.TryComp(gridUid, out var grid))
        {
            return ImmutableArray<ZLevelTraceTileVisit>.Empty;
        }

        var start = request.Origin.LocalPosition;
        var end = request.Destination.LocalPosition;
        var delta = end - start;
        var tileSize = grid.TileSize;
        var current = GetTile(start, tileSize);
        var endTile = GetTile(end, tileSize);
        var visits = ImmutableArray.CreateBuilder<ZLevelTraceTileVisit>();
        AddTileVisit(visits, gridUid, current, request.Origin.LocalZ, request.Origin.WorldCoordinates.Z, 0f);

        if (current == endTile)
            return visits.ToImmutable();

        var stepX = Math.Sign(delta.X);
        var stepY = Math.Sign(delta.Y);
        var tDeltaX = stepX == 0 ? float.PositiveInfinity : tileSize / MathF.Abs(delta.X);
        var tDeltaY = stepY == 0 ? float.PositiveInfinity : tileSize / MathF.Abs(delta.Y);
        var nextBoundaryX = (current.X + (stepX > 0 ? 1 : 0)) * tileSize;
        var nextBoundaryY = (current.Y + (stepY > 0 ? 1 : 0)) * tileSize;
        var tMaxX = stepX == 0 ? float.PositiveInfinity : (nextBoundaryX - start.X) / delta.X;
        var tMaxY = stepY == 0 ? float.PositiveInfinity : (nextBoundaryY - start.Y) / delta.Y;
        var maxSteps = Math.Abs(endTile.X - current.X) + Math.Abs(endTile.Y - current.Y) + 1;

        for (var i = 0; current != endTile && i < maxSteps; i++)
        {
            float entryT;
            if (tMaxX < tMaxY)
            {
                current = new Vector2i(current.X + stepX, current.Y);
                entryT = tMaxX;
                tMaxX += tDeltaX;
            }
            else if (tMaxY < tMaxX)
            {
                current = new Vector2i(current.X, current.Y + stepY);
                entryT = tMaxY;
                tMaxY += tDeltaY;
            }
            else
            {
                current = new Vector2i(current.X + stepX, current.Y + stepY);
                entryT = tMaxX;
                tMaxX += tDeltaX;
                tMaxY += tDeltaY;
            }

            AddTileVisit(
                visits,
                gridUid,
                current,
                request.Origin.LocalZ,
                request.Origin.WorldCoordinates.Z,
                Math.Clamp(entryT, 0f, 1f) * worldLength);
        }

        return visits.ToImmutable();
    }

    private bool ShouldIgnoreEntity(EntityUid entity, EntityFilter filter)
    {
        if (entity == filter.IgnoredEntity || !TryComp<TransformComponent>(entity, out var transform))
            return true;

        return _transform.GetWorldZLevel((entity, transform, CompOrNull<ZLevelPositionComponent>(entity))) !=
               filter.WorldZ;
    }

    private static void AddTileVisit(
        ImmutableArray<ZLevelTraceTileVisit>.Builder visits,
        EntityUid gridUid,
        Vector2i tile,
        int localZ,
        int worldZ,
        float entryDistance)
    {
        visits.Add(new ZLevelTraceTileVisit(
            visits.Count,
            gridUid,
            new ZLevelTileIndices(tile.X, tile.Y, localZ),
            worldZ,
            0,
            entryDistance));
    }

    private static Vector2i GetTile(Vector2 position, float tileSize)
    {
        return new Vector2i(
            (int)MathF.Floor(position.X / tileSize),
            (int)MathF.Floor(position.Y / tileSize));
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }

    private static ZLevelTraceResult EmptyResult(
        ZLevelTraceTermination termination,
        ZLevelTracePoint finalPoint)
    {
        return new ZLevelTraceResult(
            termination,
            finalPoint,
            ImmutableArray<ZLevelTraceSegment>.Empty,
            ImmutableArray<ZLevelTraceTileVisit>.Empty,
            ImmutableArray<ZLevelTraceEntityHit>.Empty,
            ImmutableArray<ZLevelTraceBoundaryCrossing>.Empty);
    }

    private readonly record struct EntityFilter(
        SharedZLevelTraceSystem System,
        EntityUid? IgnoredEntity,
        int WorldZ);
}
