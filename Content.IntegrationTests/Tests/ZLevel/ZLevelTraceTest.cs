// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelTraceTest : GameTest
{
    [TestPrototypes]
    private const string TracePrototypes = @"
- type: entity
  parent: BaseStructure
  id: ZLevelTraceObstacle
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      trace:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.4,-0.4,0.4,0.4""
        mask:
        - FullTileMask
        layer:
        - WallLayer
        hard: true
";

    [Test]
    public async Task SameLevelTraceUsesMovingFrameAndFiltersOtherWorldLevels()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sameLevel = default;
        EntityUid otherLevel = default;
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;
        MapCoordinates expectedOrigin = default;
        MapCoordinates expectedDestination = default;

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(7f, -3f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(18));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);

            sameLevel = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(3.5f, 1.5f)));
            otherLevel = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 1.5f)));
            Assert.That(zLevels.StampWorldZLevelPosition(sameLevel, 5), Is.True);
            Assert.That(zLevels.StampWorldZLevelPosition(otherLevel, 6), Is.True);

            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 1.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(5.5f, 1.5f),
                0,
                out destination), Is.True);
            expectedOrigin = transform.ToMapCoordinates(
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 1.5f)));
            expectedDestination = transform.ToMapCoordinates(
                new EntityCoordinates(testMap.Grid, new Vector2(5.5f, 1.5f)));
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var physics = SEntMan.System<SharedPhysicsSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var direction = destination.WorldCoordinates.Position - origin.WorldCoordinates.Position;
            var raw = physics.IntersectRay(
                    testMap.MapId,
                    new CollisionRay(
                        origin.WorldCoordinates.Position,
                        direction.Normalized(),
                        (int) CollisionGroup.BulletImpassable),
                    direction.Length(),
                    returnOnFirstHit: false)
                .ToArray();
            var request = new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable);
            var result = trace.Trace(request);

            Assert.Multiple(() =>
            {
                Assert.That(origin.GridUid, Is.EqualTo(testMap.Grid.Owner));
                Assert.That(origin.LocalZ, Is.Zero);
                Assert.That(origin.WorldCoordinates.Z, Is.EqualTo(5));
                Assert.That(transform.GetWorldZLevel((
                    sameLevel,
                    SEntMan.GetComponent<TransformComponent>(sameLevel),
                    SEntMan.GetComponent<ZLevelPositionComponent>(sameLevel))), Is.EqualTo(5));
                Assert.That(transform.GetWorldZLevel((
                    otherLevel,
                    SEntMan.GetComponent<TransformComponent>(otherLevel),
                    SEntMan.GetComponent<ZLevelPositionComponent>(otherLevel))), Is.EqualTo(6));
                Assert.That(raw.Select(hit => hit.HitEntity), Does.Contain(sameLevel));
                Assert.That(raw.Select(hit => hit.HitEntity), Does.Contain(otherLevel));
                Assert.That(origin.WorldCoordinates.Position, Is.EqualTo(expectedOrigin.Position).Using(Vector2Comparer));
                Assert.That(destination.WorldCoordinates.Position,
                    Is.EqualTo(expectedDestination.Position).Using(Vector2Comparer));
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(result.ReachedDestination, Is.True);
                Assert.That(result.Segments, Has.Length.EqualTo(1));
                Assert.That(result.Segments[0].FrameUid, Is.EqualTo(testMap.Grid.Owner));
                Assert.That(result.Segments[0].EndDistance, Is.EqualTo(5f).Within(0.0001f));
                Assert.That(result.BoundaryCrossings, Is.Empty);
                Assert.That(result.TileVisits.Select(visit => visit.Tile), Is.EqualTo(new[]
                {
                    new ZLevelTileIndices(0, 1, 0),
                    new ZLevelTileIndices(1, 1, 0),
                    new ZLevelTileIndices(2, 1, 0),
                    new ZLevelTileIndices(3, 1, 0),
                    new ZLevelTileIndices(4, 1, 0),
                    new ZLevelTileIndices(5, 1, 0),
                }));
                Assert.That(result.TileVisits, Has.All.Matches<ZLevelTraceTileVisit>(visit =>
                    visit.GridUid == testMap.Grid.Owner && visit.WorldZ == 5));
                Assert.That(result.EntityHits.Select(hit => hit.Entity), Does.Contain(sameLevel));
                Assert.That(result.EntityHits.Select(hit => hit.Entity), Does.Not.Contain(otherLevel));
                Assert.That(result.EntityHits, Has.All.Matches<ZLevelTraceEntityHit>(hit =>
                    hit.Position.Z == 5));
            });
        });
    }

    [Test]
    public async Task ZZeroEntityHitsPreserveEngineRaycastOrderAndDistance()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid first = default;
        EntityUid second = default;

        await Server.WaitAssertion(() =>
        {
            first = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 1.5f)));
            second = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(4.5f, 1.5f)));
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var physics = SEntMan.System<SharedPhysicsSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 1.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(6.5f, 1.5f),
                0,
                out var destination), Is.True);

            var ray = new CollisionRay(
                origin.WorldCoordinates.Position,
                (destination.WorldCoordinates.Position - origin.WorldCoordinates.Position).Normalized(),
                (int) CollisionGroup.BulletImpassable);
            var raw = physics.IntersectRay(
                    testMap.MapId,
                    ray,
                    6f,
                    returnOnFirstHit: false)
                .Where(hit => hit.HitEntity == first || hit.HitEntity == second)
                .ToArray();
            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable));
            var traced = result.EntityHits
                .Where(hit => hit.Entity == first || hit.Entity == second)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(traced.Select(hit => hit.Entity), Is.EqualTo(raw.Select(hit => hit.HitEntity)));
                Assert.That(traced.Select(hit => hit.Distance),
                    Is.EqualTo(raw.Select(hit => hit.Distance)).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task VerticalTraceOrdersOpenCrossingsAndStopsAtClosedChannels()
    {
        var testMap = await Pair.CreateTestMap();
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;
        ZLevelTracePoint verticalOrigin = default;
        ZLevelTracePoint verticalDestination = default;
        EntityUid floorZeroHit = default;
        EntityUid floorOneHit = default;
        EntityUid floorTwoHit = default;
        EntityUid verticalFloorZeroHit = default;
        EntityUid verticalFloorOneHit = default;
        EntityUid verticalFloorTwoHit = default;

        await Server.WaitAssertion(() =>
        {
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var map = SEntMan.System<SharedMapSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            transform.SetLocalPosition(testMap.Grid, new Vector2(9f, -4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(23));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 10), Is.True);

            for (var z = 0; z <= 2; z++)
            {
                for (var x = 0; x <= 4; x++)
                {
                    for (var y = 0; y <= 4; y++)
                    {
                        map.SetZLevelTile(
                            testMap.Grid,
                            grid,
                            new ZLevelTileIndices(x, y, z),
                            new Tile(1));
                    }
                }
            }

            EntityUid AddProjectileOpening(Vector2i tile, int localZ)
            {
                var marker = SEntMan.SpawnEntity(
                    null,
                    map.GridTileToLocal(testMap.Grid, grid, tile));
                zLevels.SetZLevelPosition(marker, localZ);
                var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
                boundaries.SetBoundary(
                    (marker, boundary),
                    true,
                    1,
                    ZLevelBoundaryChannels.Projectile,
                    ZLevelBoundaryChannels.None);
                transform.AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));
                return marker;
            }

            AddProjectileOpening(new Vector2i(1, 1), 0);
            AddProjectileOpening(new Vector2i(3, 3), 1);
            AddProjectileOpening(new Vector2i(0, 4), 0);
            AddProjectileOpening(new Vector2i(0, 4), 1);

            floorZeroHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(1f, 1f)));
            floorOneHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 2.5f)));
            floorTwoHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(4f, 4f)));
            Assert.That(zLevels.StampWorldZLevelPosition(floorZeroHit, 10), Is.True);
            Assert.That(zLevels.StampWorldZLevelPosition(floorOneHit, 11), Is.True);
            Assert.That(zLevels.StampWorldZLevelPosition(floorTwoHit, 12), Is.True);

            verticalFloorZeroHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 4.5f)));
            verticalFloorOneHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 4.5f)));
            verticalFloorTwoHit = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 4.5f)));
            Assert.That(zLevels.StampWorldZLevelPosition(verticalFloorZeroHit, 10), Is.True);
            Assert.That(zLevels.StampWorldZLevelPosition(verticalFloorOneHit, 11), Is.True);
            Assert.That(zLevels.StampWorldZLevelPosition(verticalFloorTwoHit, 12), Is.True);

            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(4.5f, 4.5f),
                2,
                out destination), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 4.5f),
                0,
                out verticalOrigin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 4.5f),
                2,
                out verticalDestination), Is.True);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var upward = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable));
            var downward = trace.Trace(new ZLevelTraceRequest(
                destination,
                origin,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable));
            var blocked = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Explosion,
                (int) CollisionGroup.BulletImpassable));
            var verticalUpward = trace.Trace(new ZLevelTraceRequest(
                verticalOrigin,
                verticalDestination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable));
            var verticalDownward = trace.Trace(new ZLevelTraceRequest(
                verticalDestination,
                verticalOrigin,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable));

            Assert.Multiple(() =>
            {
                Assert.That(upward.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(upward.Segments, Has.Length.EqualTo(3));
                Assert.That(upward.Segments.Select(segment => segment.Sequence), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(upward.Segments.Select(segment => segment.StartDistance),
                    Is.EqualTo(new[] { 0f, 1.5f, 4.5f }).Within(0.0001f));
                Assert.That(upward.Segments.Select(segment => segment.EndDistance),
                    Is.EqualTo(new[] { 1.5f, 4.5f, 6f }).Within(0.0001f));
                Assert.That(upward.BoundaryCrossings.Select(crossing => crossing.Tile), Is.EqualTo(new[]
                {
                    new Vector2i(1, 1),
                    new Vector2i(3, 3),
                }));
                Assert.That(upward.BoundaryCrossings.Select(crossing => crossing.FromWorldZ),
                    Is.EqualTo(new[] { 10, 11 }));
                Assert.That(upward.BoundaryCrossings.Select(crossing => crossing.ToWorldZ),
                    Is.EqualTo(new[] { 11, 12 }));
                Assert.That(upward.BoundaryCrossings, Has.All.Matches<ZLevelTraceBoundaryCrossing>(crossing =>
                    crossing.IsOpen && crossing.State.IsOpen(ZLevelBoundaryChannels.Projectile)));
                Assert.That(upward.EntityHits.Select(hit => hit.Entity),
                    Is.EqualTo(new[] { floorZeroHit, floorOneHit, floorTwoHit }));
                Assert.That(upward.EntityHits.Select(hit => hit.SegmentSequence), Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(upward.EntityHits.Select(hit => hit.Distance), Is.Ordered.Ascending);
                Assert.That(upward.TileVisits.Select(visit => visit.WorldZ).Distinct(),
                    Is.EqualTo(new[] { 10, 11, 12 }));

                Assert.That(downward.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(downward.BoundaryCrossings.Select(crossing => crossing.Tile), Is.EqualTo(new[]
                {
                    new Vector2i(3, 3),
                    new Vector2i(1, 1),
                }));
                Assert.That(downward.BoundaryCrossings.Select(crossing => crossing.FromWorldZ),
                    Is.EqualTo(new[] { 12, 11 }));
                Assert.That(downward.BoundaryCrossings.Select(crossing => crossing.ToWorldZ),
                    Is.EqualTo(new[] { 11, 10 }));
                Assert.That(downward.EntityHits.Select(hit => hit.Entity),
                    Is.EqualTo(new[] { floorTwoHit, floorOneHit, floorZeroHit }));

                Assert.That(blocked.Termination, Is.EqualTo(ZLevelTraceTermination.ClosedBoundary));
                Assert.That(blocked.ReachedDestination, Is.False);
                Assert.That(blocked.Segments, Has.Length.EqualTo(1));
                Assert.That(blocked.BoundaryCrossings, Has.Length.EqualTo(1));
                Assert.That(blocked.BoundaryCrossings[0].IsOpen, Is.False);
                Assert.That(blocked.BoundaryCrossings[0].Tile, Is.EqualTo(new Vector2i(1, 1)));
                Assert.That(blocked.FinalPoint.WorldCoordinates.Z, Is.EqualTo(10));
                Assert.That(blocked.FinalPoint.LocalZ, Is.Zero);
                Assert.That(blocked.EntityHits.Select(hit => hit.Entity), Is.EqualTo(new[] { floorZeroHit }));
                Assert.That(blocked.TileVisits, Has.All.Matches<ZLevelTraceTileVisit>(visit =>
                    visit.WorldZ == 10));

                Assert.That(verticalUpward.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(verticalUpward.Segments.Select(segment => segment.StartDistance),
                    Is.EqualTo(new[] { 0f, 0.5f, 1.5f }).Within(0.0001f));
                Assert.That(verticalUpward.Segments.Select(segment => segment.EndDistance),
                    Is.EqualTo(new[] { 0.5f, 1.5f, 2f }).Within(0.0001f));
                Assert.That(verticalUpward.BoundaryCrossings.Select(crossing => crossing.Tile),
                    Is.EqualTo(new[] { new Vector2i(0, 4), new Vector2i(0, 4) }));
                Assert.That(verticalUpward.TileVisits.Select(visit => visit.WorldZ),
                    Is.EqualTo(new[] { 10, 11, 12 }));
                Assert.That(verticalUpward.EntityHits.Select(hit => hit.Entity),
                    Is.EqualTo(new[] { verticalFloorZeroHit, verticalFloorOneHit, verticalFloorTwoHit }));
                Assert.That(verticalUpward.EntityHits.Select(hit => hit.Distance),
                    Is.EqualTo(new[] { 0f, 0.5f, 1.5f }).Within(0.0001f));

                Assert.That(verticalDownward.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(verticalDownward.EntityHits.Select(hit => hit.Entity),
                    Is.EqualTo(new[] { verticalFloorTwoHit, verticalFloorOneHit, verticalFloorZeroHit }));
                Assert.That(verticalDownward.EntityHits.Select(hit => hit.Distance),
                    Is.EqualTo(new[] { 0f, 0.5f, 1.5f }).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task ContinuousOffsetsMoveCrossingsWithoutChangingLegacyCenterPoints()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
            var trace = SEntMan.System<SharedZLevelTraceSystem>();

            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.25f, 0.5f),
                2,
                0.01f,
                out var flightOrigin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(4.25f, 0.5f),
                0,
                0.99f,
                out var flightDestination), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.25f, 0.5f),
                2,
                out var legacyOrigin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(4.25f, 0.5f),
                0,
                out var legacyDestination), Is.True);

            var flight = trace.Trace(new ZLevelTraceRequest(
                flightOrigin,
                flightDestination,
                ZLevelBoundaryChannels.Projectile,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            var reverse = trace.Trace(new ZLevelTraceRequest(
                flightDestination,
                flightOrigin,
                ZLevelBoundaryChannels.Projectile,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            var legacy = trace.Trace(new ZLevelTraceRequest(
                legacyOrigin,
                legacyDestination,
                ZLevelBoundaryChannels.Projectile,
                Options: ZLevelTraceOptions.IncludeTileVisits));

            Assert.Multiple(() =>
            {
                Assert.That(flight.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(flightOrigin.WorldHeight, Is.EqualTo(2.01d).Within(0.0001d));
                Assert.That(flightDestination.WorldHeight, Is.EqualTo(0.99d).Within(0.0001d));
                Assert.That(flight.BoundaryCrossings.Select(crossing => crossing.Tile), Is.EqualTo(new[]
                {
                    new Vector2i(0, 0),
                    new Vector2i(4, 0),
                }));
                Assert.That(reverse.BoundaryCrossings.Select(crossing => crossing.Tile), Is.EqualTo(new[]
                {
                    new Vector2i(4, 0),
                    new Vector2i(0, 0),
                }));
                Assert.That(legacyOrigin.LocalZOffset, Is.EqualTo(ZLevelTracePoint.DefaultZOffset));
                Assert.That(legacyDestination.WorldZOffset, Is.EqualTo(ZLevelTracePoint.DefaultZOffset));
                Assert.That(legacy.BoundaryCrossings.Select(crossing => crossing.Tile), Is.EqualTo(new[]
                {
                    new Vector2i(1, 0),
                    new Vector2i(3, 0),
                }));
                Assert.That(flight.BoundaryCrossings.Select(crossing => crossing.Distance), Is.Ordered.Ascending);
                Assert.That(flight.Segments, Has.Length.EqualTo(3));
            });

            Assert.Multiple(() =>
            {
                Assert.That(trace.TryCreateGridPoint(
                    testMap.Grid,
                    Vector2.Zero,
                    0,
                    -0.01f,
                    out _), Is.False);
                Assert.That(trace.TryCreateGridPoint(
                    testMap.Grid,
                    Vector2.Zero,
                    0,
                    1f,
                    out _), Is.False);
                Assert.That(trace.TryCreateGridPoint(
                    testMap.Grid,
                    Vector2.Zero,
                    0,
                    float.NaN,
                    out _), Is.False);
            });
        });
    }

    [Test]
    public async Task PerfectDiagonalTileOrderIsDeterministic()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(3.5f, 3.5f),
                0,
                out var destination), Is.True);

            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(result.EntityHits, Is.Empty);
                Assert.That(result.TileVisits.Select(visit => visit.Tile), Is.EqualTo(new[]
                {
                    new ZLevelTileIndices(0, 0, 0),
                    new ZLevelTileIndices(1, 1, 0),
                    new ZLevelTileIndices(2, 2, 0),
                    new ZLevelTileIndices(3, 3, 0),
                }));
                Assert.That(result.TileVisits.Select(visit => visit.Sequence), Is.EqualTo(new[] { 0, 1, 2, 3 }));
                Assert.That(result.TileVisits.Select(visit => visit.EntryDistance), Is.Ordered.Ascending);
            });
        });
    }

    [Test]
    public async Task GridPointsResolveAgainstCurrentFrameAtTraceTime()
    {
        var testMap = await Pair.CreateTestMap();
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;
        EntityUid obstacle = default;
        MapCoordinates staleOrigin = default;
        MapCoordinates expectedOrigin = default;
        MapCoordinates expectedDestination = default;

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            for (var x = 0; x <= 5; x++)
            {
                map.SetTile(testMap.Grid, grid, new Vector2i(x, 1), new Tile(1));
            }

            transform.SetLocalPosition(testMap.Grid, new Vector2(2f, -1f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(12));
            transform.SetZLevelFrameOrigin(testMap.Grid, 5);

            obstacle = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(3.5f, 1.5f)));
            Assert.That(zLevels.StampWorldZLevelPosition(obstacle, 5), Is.True);
            Assert.That(SEntMan.GetComponent<TransformComponent>(obstacle).GridUid,
                Is.EqualTo(testMap.Grid.Owner));

            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 1.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(5.5f, 1.5f),
                0,
                out destination), Is.True);
            staleOrigin = new MapCoordinates(
                origin.WorldCoordinates.Position,
                origin.WorldCoordinates.MapId);

            transform.SetLocalPosition(testMap.Grid, new Vector2(-6f, 4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(-37));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 7), Is.True);
            expectedOrigin = transform.ToMapCoordinates(
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 1.5f)));
            expectedDestination = transform.ToMapCoordinates(
                new EntityCoordinates(testMap.Grid, new Vector2(5.5f, 1.5f)));
            Assert.That(expectedOrigin.Position, Is.Not.EqualTo(staleOrigin.Position));
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var obstacleTransform = SEntMan.GetComponent<TransformComponent>(obstacle);
            Assert.That(obstacleTransform.GridUid, Is.EqualTo(testMap.Grid.Owner));
            Assert.That(transform.GetWorldZLevel((
                obstacle,
                obstacleTransform,
                SEntMan.GetComponentOrNull<ZLevelPositionComponent>(obstacle))), Is.EqualTo(7));
            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int)CollisionGroup.BulletImpassable));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(result.Segments, Has.Length.EqualTo(1));
                Assert.That(result.Segments[0].Start.WorldCoordinates.Position,
                    Is.EqualTo(expectedOrigin.Position).Using(Vector2Comparer));
                Assert.That(result.Segments[0].End.WorldCoordinates.Position,
                    Is.EqualTo(expectedDestination.Position).Using(Vector2Comparer));
                Assert.That(result.Segments[0].Start.WorldCoordinates.Z, Is.EqualTo(7));
                Assert.That(result.Segments[0].End.WorldCoordinates.Z, Is.EqualTo(7));
                Assert.That(result.EntityHits.Select(hit => hit.Entity), Is.EqualTo(new[] { obstacle }));
                Assert.That(result.TileVisits.Select(visit => visit.Tile.Z).Distinct(), Is.EqualTo(new[] { 0 }));
            });
        });
    }

    [Test]
    public async Task VerticalTraceBetweenDifferentFramesRequiresExplicitBoundaryFrame()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var otherGrid = mapManager.CreateGridEntity(testMap.MapId);
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(otherGrid.Owner, Vector2.Zero);
            transform.SetZLevelFrameOrigin(otherGrid.Owner, 1);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                otherGrid.Owner,
                new Vector2(0.5f, 0.5f),
                0,
                out var destination), Is.True);

            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile));
            var explicitFrame = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                BoundaryFrameUid: testMap.Grid));
            var mapOnly = trace.Trace(new ZLevelTraceRequest(
                ZLevelTracePoint.FromMap(origin.WorldCoordinates),
                ZLevelTracePoint.FromMap(destination.WorldCoordinates),
                ZLevelBoundaryChannels.Projectile,
                BoundaryFrameUid: testMap.Grid));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.FrameResolutionRequired));
                Assert.That(result.Segments, Is.Empty);
                Assert.That(result.BoundaryCrossings, Is.Empty);

                Assert.That(explicitFrame.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(explicitFrame.Segments, Has.Length.EqualTo(2));
                Assert.That(explicitFrame.BoundaryCrossings, Has.Length.EqualTo(1));
                Assert.That(explicitFrame.BoundaryCrossings[0].GridUid, Is.EqualTo(testMap.Grid.Owner));
                Assert.That(explicitFrame.BoundaryCrossings[0].FromWorldZ, Is.EqualTo(0));
                Assert.That(explicitFrame.BoundaryCrossings[0].ToWorldZ, Is.EqualTo(1));

                Assert.That(mapOnly.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(mapOnly.BoundaryCrossings, Has.Length.EqualTo(1));
                Assert.That(mapOnly.BoundaryCrossings[0].GridUid, Is.EqualTo(testMap.Grid.Owner));
                Assert.That(mapOnly.Segments, Has.All.Matches<ZLevelTraceSegment>(segment =>
                    segment.FrameUid == testMap.Grid.Owner));
            });
        });
    }

    [Test]
    public async Task SharedFrameTraceIsDeterministicBetweenServerAndClient()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gridNetEntity = default;
        ZLevelTraceResult serverResult = default;

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(31));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 3), Is.True);
            gridNetEntity = SEntMan.GetNetEntity(testMap.Grid);
        });

        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(4.5f, 2.5f),
                1,
                out var destination), Is.True);
            serverResult = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Visibility,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            Assert.That(serverResult.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
        });

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(gridNetEntity, out var clientGrid), Is.True);
            var trace = CEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                clientGrid!.Value,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                clientGrid.Value,
                new Vector2(4.5f, 2.5f),
                1,
                out var destination), Is.True);
            var clientResult = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Visibility,
                Options: ZLevelTraceOptions.IncludeTileVisits));

            Assert.Multiple(() =>
            {
                Assert.That(clientResult.Termination, Is.EqualTo(serverResult.Termination));
                Assert.That(clientResult.FinalPoint.WorldCoordinates.Position,
                    Is.EqualTo(serverResult.FinalPoint.WorldCoordinates.Position).Using(Vector2Comparer));
                Assert.That(clientResult.FinalPoint.WorldCoordinates.Z,
                    Is.EqualTo(serverResult.FinalPoint.WorldCoordinates.Z));
                Assert.That(clientResult.Segments.Select(segment => segment.StartDistance),
                    Is.EqualTo(serverResult.Segments.Select(segment => segment.StartDistance)).Within(0.0001f));
                Assert.That(clientResult.Segments.Select(segment => segment.EndDistance),
                    Is.EqualTo(serverResult.Segments.Select(segment => segment.EndDistance)).Within(0.0001f));
                Assert.That(clientResult.TileVisits.Select(visit => visit.Tile),
                    Is.EqualTo(serverResult.TileVisits.Select(visit => visit.Tile)));
                Assert.That(clientResult.TileVisits.Select(visit => visit.EntryDistance),
                    Is.EqualTo(serverResult.TileVisits.Select(visit => visit.EntryDistance)).Within(0.0001f));
                Assert.That(clientResult.BoundaryCrossings.Select(crossing => crossing.Tile),
                    Is.EqualTo(serverResult.BoundaryCrossings.Select(crossing => crossing.Tile)));
                Assert.That(clientResult.BoundaryCrossings.Select(crossing => crossing.Distance),
                    Is.EqualTo(serverResult.BoundaryCrossings.Select(crossing => crossing.Distance)).Within(0.0001f));
                Assert.That(clientResult.BoundaryCrossings.Select(crossing => crossing.State.OpenChannels),
                    Is.EqualTo(serverResult.BoundaryCrossings.Select(crossing => crossing.State.OpenChannels)));
            });
        });
    }

    [Test]
    public async Task BufferedTraceReusesStorageAndMatchesImmutableResult()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(5.5f, 2.5f),
                0,
                out var destination), Is.True);
            var request = new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.IncludeTileVisits);
            var buffer = new ZLevelTraceBuffer();
            buffer.EnsureCapacity(1, 16, 0, 0);
            var segments = buffer.Segments;
            var tiles = buffer.TileVisits;

            var buffered = trace.Trace(request, buffer);
            var immutable = trace.Trace(request);
            var capacities = new
            {
                buffer.SegmentCapacity,
                buffer.TileVisitCapacity,
                buffer.EntityHitCapacity,
                buffer.BoundaryCrossingCapacity,
            };
            Assert.Multiple(() =>
            {
                Assert.That(buffered.Termination, Is.EqualTo(immutable.Termination));
                Assert.That(buffered.FinalPoint, Is.EqualTo(immutable.FinalPoint));
                Assert.That(buffer.Segments, Is.EqualTo(immutable.Segments));
                Assert.That(buffer.TileVisits, Is.EqualTo(immutable.TileVisits));
                Assert.That(buffer.EntityHits, Is.EqualTo(immutable.EntityHits));
                Assert.That(buffer.BoundaryCrossings, Is.EqualTo(immutable.BoundaryCrossings));
            });

            var reverse = trace.Trace(request with
            {
                Origin = destination,
                Destination = origin,
            }, buffer);
            Assert.Multiple(() =>
            {
                Assert.That(reverse.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(buffer.Segments, Is.SameAs(segments));
                Assert.That(buffer.TileVisits, Is.SameAs(tiles));
                Assert.That(buffer.SegmentCapacity, Is.EqualTo(capacities.SegmentCapacity));
                Assert.That(buffer.TileVisitCapacity, Is.EqualTo(capacities.TileVisitCapacity));
                Assert.That(buffer.EntityHitCapacity, Is.EqualTo(capacities.EntityHitCapacity));
                Assert.That(buffer.BoundaryCrossingCapacity, Is.EqualTo(capacities.BoundaryCrossingCapacity));
                Assert.That(buffer.Segments.Select(segment => segment.Sequence), Is.EqualTo(new[] { 0 }));
                Assert.That(buffer.TileVisits.Select(visit => visit.Sequence),
                    Is.EqualTo(Enumerable.Range(0, buffer.TileVisits.Count)));
            });

            var invalid = trace.Trace(request with
            {
                Origin = ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    new Vector2(float.NaN, 0f),
                    0,
                    testMap.MapId)),
            }, buffer);
            Assert.Multiple(() =>
            {
                Assert.That(invalid.Termination, Is.EqualTo(ZLevelTraceTermination.InvalidCoordinates));
                Assert.That(buffer.Segments, Is.Empty);
                Assert.That(buffer.TileVisits, Is.Empty);
                Assert.That(buffer.EntityHits, Is.Empty);
                Assert.That(buffer.BoundaryCrossings, Is.Empty);
                Assert.That(buffer.SegmentCapacity, Is.EqualTo(capacities.SegmentCapacity));
                Assert.That(buffer.TileVisitCapacity, Is.EqualTo(capacities.TileVisitCapacity));
            });

            buffer.Clear();
            Assert.Multiple(() =>
            {
                Assert.That(buffer.Segments, Is.Empty);
                Assert.That(buffer.TileVisits, Is.Empty);
                Assert.That(buffer.SegmentCapacity, Is.EqualTo(capacities.SegmentCapacity));
                Assert.That(buffer.TileVisitCapacity, Is.EqualTo(capacities.TileVisitCapacity));
            });
        });
    }

    [Test]
    public async Task EqualDistanceEntityHitsUseUidTieBreak()
    {
        var testMap = await Pair.CreateTestMap();
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;
        EntityUid first = default;
        EntityUid second = default;

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            first = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 0.5f)));
            second = SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 0.5f)));
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(4.5f, 0.5f),
                0,
                out destination), Is.True);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable,
                Options: ZLevelTraceOptions.IncludeEntityHits));
            var expected = new[] { first, second }.OrderBy(uid => uid).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(result.EntityHits.Select(hit => hit.Entity), Is.EqualTo(expected));
                Assert.That(result.EntityHits.Select(hit => hit.Sequence), Is.EqualTo(new[] { 0, 1 }));
                Assert.That(result.EntityHits[0].Distance,
                    Is.EqualTo(result.EntityHits[1].Distance).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task FiniteEndpointsWithOverflowingDistanceAreRejected()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var origin = ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                new Vector2(float.MaxValue, 0f),
                0,
                testMap.MapId));
            var destination = ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                new Vector2(-float.MaxValue, 0f),
                0,
                testMap.MapId));
            var request = new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.None);
            var buffer = new ZLevelTraceBuffer();
            var result = trace.Trace(request, buffer);
            var stationary = trace.Trace(request with { Destination = origin });

            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.InvalidCoordinates));
                Assert.That(buffer.Segments, Is.Empty);
                Assert.That(buffer.TileVisits, Is.Empty);
                Assert.That(stationary.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(stationary.Segments, Has.Length.EqualTo(1));
                Assert.That(stationary.Segments[0].EndDistance, Is.Zero);
            });
        });
    }

    private static IEqualityComparer<Vector2> Vector2Comparer { get; } =
        new ApproximateVector2Comparer(0.0001f);

    private sealed class ApproximateVector2Comparer(float tolerance) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 left, Vector2 right)
        {
            return Vector2.DistanceSquared(left, right) <= tolerance * tolerance;
        }

        public int GetHashCode(Vector2 value)
        {
            return 0;
        }
    }
}
