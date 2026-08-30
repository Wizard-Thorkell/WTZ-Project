// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelHitscanTest : GameTest
{
    private const int AllocationWarmupIterations = 16;
    private const int AllocationMeasuredIterations = 512;

    [TestPrototypes]
    private const string HitscanPrototypes = @"
- type: entity
  parent: BaseStructure
  id: ZLevelHitscanObstacle
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      hitscan:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.4,-0.4,0.4,0.4""
        mask:
        - FullTileMask
        layer:
        - WallLayer
        hard: true

- type: entity
  id: ZLevelHitscanFlyer
  components:
  - type: Physics
    bodyType: Dynamic
  - type: ZLevelFlight

- type: entity
  id: ZLevelHitscanFlyingObstacle
  components:
  - type: Physics
    bodyType: Dynamic
  - type: Fixtures
    fixtures:
      hitscan:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.4,-0.4,0.4,0.4""
        mask:
        - FullTileMask
        layer:
        - WallLayer
        hard: true
  - type: ZLevelFlight
";

    public sealed class HitscanListenerSystem : TestListenerSystem<HitscanRaycastFiredEvent>;

    [Test]
    public async Task SameLevelHitscanPreservesSelectionAndFiltersOtherFloors()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid otherFloor = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(3.5f, 0.5f), 0);
            otherFloor = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(2.5f, 0.5f), 1);
            hitscan = SpawnHitscan(testMap);

            Assert.That(zLevels.GetWorldZLevel(shooter), Is.Zero);
            Assert.That(zLevels.GetWorldZLevel(target), Is.Zero);
            Assert.That(zLevels.GetWorldZLevel(otherFloor), Is.EqualTo(1));
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.UnitX);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(data.HitEntity, Is.Not.EqualTo(otherFloor));
                Assert.That(snapshot.TraceQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceEntityHits, Is.EqualTo(1));
                Assert.That(snapshot.TraceBoundaryCrossings, Is.Zero);
            });
        });
    }

    [Test]
    public async Task OpenVerticalHitscanReachesTargetFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.Zero);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(snapshot.TraceQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.EqualTo(2));
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ActiveFlightOffsetsSelectThePhysicalCrossingColumn()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 2);
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            for (var x = 0; x <= 4; x++)
                map.SetTile(testMap.Grid, grid, new Vector2i(x, 0), new Tile(1));

            var zLevels = SEntMan.System<SharedZLevelSystem>();
            shooter = Spawn(testMap, "ZLevelHitscanFlyer", new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelHitscanFlyingObstacle", new Vector2(4.25f, 0.5f), 0);
            Assert.That(zLevels.SetZLevelPosition(shooter, 2, 0.01f), Is.True);
            Assert.That(zLevels.SetZLevelPosition(target, 0, 0.99f), Is.True);
            Assert.That(zLevels.TryStartFlight(shooter, 2, 0.01f), Is.EqualTo(ZLevelFlightResult.Success));
            Assert.That(zLevels.TryStartFlight(target, 0, 0.99f), Is.EqualTo(ZLevelFlightResult.Success));

            // A center-to-center trace would cross this closed tile. The active
            // flight heights instead cross tiles 0 and 4.
            CloseBoundary(testMap, new Vector2i(1, 0), 1, ZLevelBoundaryChannels.Projectile);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.UnitX);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceClosedBoundaries, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task TargetlessCoordinateHitscanReachesSelectedLowerFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var listener = SEntMan.System<HitscanListenerSystem>();
            listener.Clear(hitscan);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var fired = new HitscanTraceEvent
            {
                FromCoordinates = SEntMan.GetComponent<TransformComponent>(shooter).Coordinates,
                ShotDirection = Vector2.Zero,
                Gun = shooter,
                Shooter = shooter,
                TargetCoordinates = SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                TargetWorldZ = 0,
            };
            SEntMan.EventBus.RaiseLocalEvent(hitscan, ref fired);

            var data = listener.GetEvents(hitscan).Single().Data;
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(snapshot.TraceQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.EqualTo(2));
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ProjectileClosedBoundaryStopsVerticalHitscan()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
            CloseBoundary(testMap, Vector2i.Zero, 0, ZLevelBoundaryChannels.Projectile);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.Zero);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.TraceQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceClosedBoundaries, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.EqualTo(1));
                Assert.That(snapshot.TraceEntityHits, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task InvisibleCrossFloorTargetFailsClosedWithoutShooterFloorTrace()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
            CloseBoundary(testMap, Vector2i.Zero, 0, ZLevelBoundaryChannels.Visibility);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.Zero);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.VisibilityEntityQueries, Is.EqualTo(1));
                Assert.That(snapshot.VisibilityBoundaryChecks, Is.EqualTo(1));
                Assert.That(snapshot.TraceQueries, Is.Zero);
                Assert.That(snapshot.TraceCompleted, Is.Zero);
                Assert.That(snapshot.TraceSegments, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.Zero);
            });
        });
    }

    [Test]
    public async Task CrossFloorTargetBeyondThreeDimensionalRangeIsNotHit()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            for (var x = 0; x <= 3; x++)
                map.SetTile(testMap.Grid, grid, new Vector2i(x, 0), new Tile(1));

            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(3.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap, 3f);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.UnitX);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.VisibilityEntityQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceQueries, Is.Zero);
                Assert.That(snapshot.TraceCompleted, Is.Zero);
                Assert.That(snapshot.TraceSegments, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.Zero);
            });
        });
    }

    [Test]
    public async Task TargetAboveShooterIsRejectedByCurrentViewportPolicy()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 1);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 1);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.Zero);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.VisibilityEntityQueries, Is.EqualTo(1));
                Assert.That(snapshot.VisibilityEarlyRejections, Is.EqualTo(1));
                Assert.That(snapshot.TraceQueries, Is.Zero);
                Assert.That(snapshot.TraceCompleted, Is.Zero);
                Assert.That(snapshot.TraceSegments, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.Zero);
            });
        });
    }

    [Test]
    public async Task DeletedExplicitTargetFailsClosedWithoutPlanarFallback()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid deletedTarget = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 0);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(2.5f, 0.5f), 0);
            deletedTarget = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(3.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
            SEntMan.DeleteEntity(deletedTarget);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, deletedTarget, Vector2.UnitX);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.TraceQueries, Is.Zero);
                Assert.That(snapshot.TraceCompleted, Is.Zero);
                Assert.That(snapshot.TraceEntityHits, Is.Zero);
            });
        });
    }

    [Test]
    public async Task SameLevelTargetSelectionStillSkipsTargetOnlyObstacles()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid targetOnly = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 0);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            targetOnly = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(2.5f, 0.5f), 0);
            SEntMan.EnsureComponent<RequireProjectileTargetComponent>(targetOnly);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(3.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.UnitX);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(data.HitEntity, Is.Not.EqualTo(targetOnly));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceEntityHits, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task DiagonalHitscanUsesCurrentMovingFrameAndWorldZ()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;
        Vector2 direction = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 2);
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            for (var x = 0; x <= 4; x++)
            {
                for (var y = 0; y <= 4; y++)
                    map.SetTile(testMap.Grid, grid, new Vector2i(x, y), new Tile(1));
            }

            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);

            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(4.5f, 4.5f), 0);
            hitscan = SpawnHitscan(testMap);
            var shooterMap = transform.GetMapCoordinates(shooter);
            var targetMap = transform.GetMapCoordinates(target);
            direction = (targetMap.Position - shooterMap.Position).Normalized();
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, direction);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetWorldZLevel(shooter), Is.EqualTo(7));
                Assert.That(zLevels.GetWorldZLevel(target), Is.EqualTo(5));
                Assert.That(data.HitEntity, Is.EqualTo(target));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.EqualTo(3));
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task VerticalCrossingBudgetFailsWithoutAHit()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxVerticalCrossings, 1);
        var testMap = await Pair.CreateTestMap();
        EntityUid shooter = default;
        EntityUid target = default;
        EntityUid hitscan = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 2);
            shooter = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(0.5f, 0.5f), 0);
            hitscan = SpawnHitscan(testMap);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var data = Fire(hitscan, shooter, target, Vector2.Zero);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(data.HitEntity, Is.Null);
                Assert.That(snapshot.TraceQueries, Is.EqualTo(1));
                Assert.That(snapshot.TraceBudgetExhaustions, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.Zero);
                Assert.That(snapshot.TraceEntityHits, Is.Zero);
            });
        });
    }

    [Test]
    public async Task CollisionEnabledTraceAllocationIsCaptured()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid target = default;
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, 0);
            target = Spawn(testMap, "ZLevelHitscanObstacle", new Vector2(3.5f, 0.5f), 0);
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(10.5f, 0.5f),
                0,
                out destination), Is.True);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var request = new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int) CollisionGroup.BulletImpassable,
                Options: ZLevelTraceOptions.IncludeEntityHits,
                BoundaryFrameUid: testMap.Grid);
            var buffer = new ZLevelTraceBuffer();
            buffer.EnsureCapacity(1, 0, 8, 0);
            for (var i = 0; i < AllocationWarmupIterations; i++)
                trace.Trace(request, buffer);

            metrics.ResetCounters();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var started = Stopwatch.GetTimestamp();
            ZLevelTraceBufferResult result = default;
            for (var i = 0; i < AllocationMeasuredIterations; i++)
                result = trace.Trace(request, buffer);
            var elapsedTicks = Stopwatch.GetTimestamp() - started;
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var snapshot = metrics.Snapshot();

            TestContext.Progress.WriteLine(
                $"WTZ hitscan collision trace: {AllocationMeasuredIterations} queries, " +
                $"{allocatedBytes} bytes ({allocatedBytes / (double) AllocationMeasuredIterations:0.00}/query), " +
                $"{elapsedTicks * 1000d / Stopwatch.Frequency:0.000} ms total.");
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(buffer.EntityHits.Select(hit => hit.Entity), Does.Contain(target));
                Assert.That(snapshot.TraceQueries, Is.EqualTo(AllocationMeasuredIterations));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(AllocationMeasuredIterations));
                Assert.That(snapshot.TraceEntityHits, Is.GreaterThanOrEqualTo(AllocationMeasuredIterations));
                Assert.That(allocatedBytes, Is.GreaterThanOrEqualTo(0));
            });
        });
    }

    private void Configure(TestMapData testMap, int maxLevel)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            maxLevel,
            0,
            ZLevelDefaultBoundaryMode.ExplicitOnly);
    }

    private EntityUid Spawn(
        TestMapData testMap,
        string? prototype,
        Vector2 position,
        int localZ)
    {
        var entity = SEntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, position));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, localZ), Is.True);
        return entity;
    }

    private EntityUid SpawnHitscan(TestMapData testMap, float maxDistance = 20f)
    {
        var hitscan = SEntMan.SpawnEntity(null, new EntityCoordinates(testMap.Grid, Vector2.Zero));
        var raycast = SEntMan.EnsureComponent<HitscanBasicRaycastComponent>(hitscan);
        raycast.MaxDistance = maxDistance;
        raycast.CollisionMask = CollisionGroup.BulletImpassable;
        SEntMan.EnsureComponent<TestListenerComponent>(hitscan);
        return hitscan;
    }

    private HitscanRaycastFiredData Fire(
        EntityUid hitscan,
        EntityUid shooter,
        EntityUid target,
        Vector2 direction)
    {
        var listener = SEntMan.System<HitscanListenerSystem>();
        listener.Clear(hitscan);
        var shooterTransform = SEntMan.GetComponent<TransformComponent>(shooter);
        var fired = new HitscanTraceEvent
        {
            FromCoordinates = shooterTransform.Coordinates,
            ShotDirection = direction,
            Gun = shooter,
            Shooter = shooter,
            Target = target,
        };
        SEntMan.EventBus.RaiseLocalEvent(hitscan, ref fired);
        return listener.GetEvents(hitscan).Single().Data;
    }

    private void CloseBoundary(
        TestMapData testMap,
        Vector2i tile,
        int localZ,
        ZLevelBoundaryChannels channels)
    {
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        map.SetZLevelTile(
            testMap.Grid,
            grid,
            new ZLevelTileIndices(tile.X, tile.Y, localZ),
            new Tile(1));
        var blocker = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(blocker, localZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(blocker);
        boundaries.SetBoundary(
            (blocker, boundary),
            true,
            1,
            ZLevelBoundaryChannels.None,
            channels);
        transform.AnchorEntity(blocker, SEntMan.GetComponent<TransformComponent>(blocker));
    }
}
