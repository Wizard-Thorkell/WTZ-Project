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
    public async Task VerticalTraceBetweenDifferentFramesRequiresResolution()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var otherGrid = mapManager.CreateGridEntity(testMap.MapId);
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(otherGrid.Owner, new Vector2(3f, 0f));
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
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.FrameResolutionRequired));
                Assert.That(result.Segments, Is.Empty);
                Assert.That(result.BoundaryCrossings, Is.Empty);
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
