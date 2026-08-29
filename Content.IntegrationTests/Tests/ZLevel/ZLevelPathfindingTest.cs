// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using System.Numerics;
using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.NPC.Pathfinding;
using Content.Server.ZLevel.Navigation;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelPathfindingTest : GameTest
{
    private const int AllocationWarmupIterations = 64;
    private const int AllocationMeasuredIterations = 4_096;
    private const long MaxWarmedBreadcrumbBuildAllocatedBytes = 58_000;

    [TestPrototypes]
    private const string PathfindingPrototypes = @"
- type: entity
  parent: BaseStructure
  id: ZLevelPathfindingTestBlocker
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      pathfinding:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.49,-0.49,0.49,0.49""
        layer:
        - WallLayer
        hard: true
";

    [Test]
    public async Task FloorSpecificFixturesAndRoutesRemainIsolated()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid blocker = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            blocker = SEntMan.SpawnEntity(
                "ZLevelPathfindingTestBlocker",
                Coordinates(testMap, 1.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(blocker, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var lower = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 0);
            var upper = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1);

            Assert.Multiple(() =>
            {
                Assert.That(lower, Is.Not.Null);
                Assert.That(upper, Is.Not.Null);
                Assert.That(lower!.LocalZ, Is.Zero);
                Assert.That(upper!.LocalZ, Is.EqualTo(1));
                Assert.That(lower.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Not.Zero);
                Assert.That(upper.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Zero);
            });

            var metrics = pathfinding.SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.CachedFloors, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.FixtureCandidates, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.FixtureFloorRejects, Is.GreaterThanOrEqualTo(1));
            });
        });

        Assert.That(await RequestPath(testMap, 0, 0), Is.EqualTo(PathResult.NoPath));
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.Path));

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            pathfinding.ResetZLevelMetrics();
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(blocker, 1), Is.True);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var lower = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 0);
            var upper = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1);
            var metrics = pathfinding.SnapshotZLevelMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(lower, Is.Not.Null);
                Assert.That(upper, Is.Not.Null);
                Assert.That(lower!.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Zero);
                Assert.That(upper!.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Not.Zero);
                Assert.That(metrics.BreadcrumbBuilds, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.FixtureCandidates, Is.EqualTo(2));
                Assert.That(metrics.FixtureFloorRejects, Is.EqualTo(1));
            });
        });

        Assert.That(await RequestPath(testMap, 0, 0), Is.EqualTo(PathResult.Path));
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.NoPath));

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            pathfinding.ResetZLevelMetrics();
            SEntMan.System<SharedTransformSystem>().SetCoordinates(blocker, Coordinates(testMap, 5.5f));
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var oldUpper = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1);
            var newUpper = pathfinding.GetPoly(Coordinates(testMap, 5.5f), 1);

            Assert.Multiple(() =>
            {
                Assert.That(oldUpper, Is.Not.Null);
                Assert.That(newUpper, Is.Not.Null);
                Assert.That(oldUpper!.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Zero);
                Assert.That(newUpper!.Data.CollisionLayer & (int) CollisionGroup.WallLayer, Is.Not.Zero);
                Assert.That(pathfinding.SnapshotZLevelMetrics().BreadcrumbBuilds, Is.GreaterThanOrEqualTo(2));
            });
        });
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.Path));

        await Server.WaitAssertion(() => SEntMan.System<PathfindingSystem>().ResetZLevelMetrics());
        Assert.That(await RequestPath(testMap, 0, 1), Is.EqualTo(PathResult.NoPath));
        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<PathfindingSystem>()
                    .SnapshotZLevelMetrics()
                    .DifferentFloorRouteRejections,
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task HierarchicalSameFloorRouteUsesAndValidatesNativeNavigation()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() => ConfigureCorridors(testMap));
        await Pair.RunTicksSync(40);

        var result = await RequestZLevelPath(testMap, 0.5f, 0, 5.5f, 0);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(result.Route!.Legs, Has.Length.EqualTo(1));
            Assert.That(result.Route.Legs[0].Kind, Is.EqualTo(ZLevelPathLegKind.Local));
            Assert.That(result.Route.Legs[0].LocalPath.IsDefault, Is.False);
            Assert.That(result.Diagnostics.StatesExpanded, Is.EqualTo(1));
            Assert.That(result.Diagnostics.LocalPathsRequested, Is.EqualTo(1));
            Assert.That(result.Diagnostics.TraversalEdgesEvaluated, Is.Zero);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<PathfindingSystem>().ValidateZLevelPathRoute(result.Route!),
                Is.EqualTo(ZLevelPathRouteValidationResult.Valid));

            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetTile(testMap.Grid, grid, new Vector2i(3, 0), Tile.Empty);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var validation = SEntMan.System<PathfindingSystem>()
                .ValidateZLevelPathRoute(result.Route!);
            Assert.Multiple(() =>
            {
                Assert.That(validation.Status,
                    Is.EqualTo(ZLevelPathRouteValidationStatus.LocalNavigationChanged));
                Assert.That(validation.LegIndex, Is.Zero);
            });
        });
    }

    [Test]
    public async Task HierarchicalRouteComposesLocalAndTraversalLegsDeterministically()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid stairs = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            stairs = SpawnUpStairs(testMap, 2.5f);
            SpawnUpStairs(testMap, 4.5f);
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() => SEntMan.System<PathfindingSystem>().ResetZLevelMetrics());

        var result = await RequestZLevelPath(testMap, 0.5f, 0, 5.5f, 1);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
            Assert.That(result.Route, Is.Not.Null);
            Assert.That(result.Route!.Legs.Select(leg => leg.Kind), Is.EqualTo(new[]
            {
                ZLevelPathLegKind.Local,
                ZLevelPathLegKind.Traversal,
                ZLevelPathLegKind.Local,
            }));
            Assert.That(result.Route.Legs[1].Traversal.Source.Traversal, Is.EqualTo(stairs));
            Assert.That(result.Route.TotalCost, Is.GreaterThanOrEqualTo(4f));
            Assert.That(result.Diagnostics.StatesExpanded, Is.EqualTo(3));
            Assert.That(result.Diagnostics.LocalPathsRequested, Is.EqualTo(4));
            Assert.That(result.Diagnostics.TraversalEdgesEvaluated, Is.EqualTo(2));
        });

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var metrics = pathfinding.SnapshotZLevelRouteMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(pathfinding.ValidateZLevelPathRoute(result.Route!),
                    Is.EqualTo(ZLevelPathRouteValidationResult.Valid));
                Assert.That(metrics.Queries, Is.EqualTo(1));
                Assert.That(metrics.Successes, Is.EqualTo(1));
                Assert.That(metrics.StatesExpanded, Is.EqualTo(3));
                Assert.That(metrics.LocalPathsRequested, Is.EqualTo(4));
                Assert.That(metrics.TraversalEdgesEvaluated, Is.EqualTo(2));
                Assert.That(metrics.Legs, Is.EqualTo(3));
            });

            SpawnUpStairs(testMap, 1.5f);
            Assert.That(pathfinding.ValidateZLevelPathRoute(result.Route!),
                Is.EqualTo(ZLevelPathRouteValidationResult.Valid),
                "An unrelated graph revision must not invalidate an executable route.");

            SEntMan.DeleteEntity(stairs);
            var invalid = pathfinding.ValidateZLevelPathRoute(result.Route!);
            Assert.Multiple(() =>
            {
                Assert.That(invalid.Status,
                    Is.EqualTo(ZLevelPathRouteValidationStatus.TraversalChanged));
                Assert.That(invalid.LegIndex, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task HierarchicalRouteCannotReachAConnectorThroughBlockedLocalNavigation()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            var blocker = SEntMan.SpawnEntity(
                "ZLevelPathfindingTestBlocker",
                Coordinates(testMap, 1.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(blocker, 0), Is.True);
            SpawnUpStairs(testMap, 2.5f);
        });
        await Pair.RunTicksSync(40);

        var result = await RequestZLevelPath(testMap, 0.5f, 0, 5.5f, 1);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ZLevelPathRouteStatus.NoPath));
            Assert.That(result.Route, Is.Null);
            Assert.That(result.Diagnostics.StatesExpanded, Is.EqualTo(1));
            Assert.That(result.Diagnostics.LocalPathsRequested, Is.EqualTo(1));
            Assert.That(result.Diagnostics.TraversalEdgesEvaluated, Is.Zero);
        });
    }

    [Test]
    public async Task HierarchicalRouteBudgetsAndCancellationAreExplicitlyReported()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
        });
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() => SEntMan.System<PathfindingSystem>().ResetZLevelMetrics());

        var stateBudget = await RequestZLevelPath(
            testMap,
            0.5f,
            0,
            5.5f,
            1,
            new ZLevelPathSearchBudget(0, 8, 8));
        var localBudget = await RequestZLevelPath(
            testMap,
            0.5f,
            0,
            5.5f,
            1,
            new ZLevelPathSearchBudget(8, 0, 8));
        var edgeBudget = await RequestZLevelPath(
            testMap,
            0.5f,
            0,
            5.5f,
            1,
            new ZLevelPathSearchBudget(8, 8, 0));

        Task<ZLevelPathRouteResult>? cancelledTask = null;
        using var cancellation = new CancellationTokenSource();
        await Server.WaitPost(() =>
        {
            cancelledTask = SEntMan.System<PathfindingSystem>().GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 5.5f), 1),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                cancellation.Token);
            Assert.That(cancelledTask.IsCompleted, Is.False);
            cancellation.Cancel();
        });
        await Pair.RunTicksSync(10);
        var cancelled = await cancelledTask!;

        Assert.Multiple(() =>
        {
            Assert.That(stateBudget.Status,
                Is.EqualTo(ZLevelPathRouteStatus.StateExpansionBudgetExceeded));
            Assert.That(localBudget.Status,
                Is.EqualTo(ZLevelPathRouteStatus.LocalPathBudgetExceeded));
            Assert.That(edgeBudget.Status,
                Is.EqualTo(ZLevelPathRouteStatus.TraversalEdgeBudgetExceeded));
            Assert.That(cancelled.Status, Is.EqualTo(ZLevelPathRouteStatus.Cancelled));
            Assert.That(cancelled.Diagnostics.LocalPathsRequested, Is.EqualTo(1));
        });

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<PathfindingSystem>().SnapshotZLevelRouteMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.Queries, Is.EqualTo(4));
                Assert.That(metrics.StateBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.LocalPathBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.TraversalEdgeBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.Cancellations, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task HierarchicalRouteRejectsGraphChangesWhileLocalPathsArePending()
    {
        var testMap = await Pair.CreateTestMap();
        Task<ZLevelPathRouteResult>? topologyTask = null;
        Task<ZLevelPathRouteResult>? environmentTask = null;
        Task<ZLevelPathRouteResult>? combinedTask = null;

        await Server.WaitAssertion(() => ConfigureCorridors(testMap));
        await Pair.RunTicksSync(40);
        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            pathfinding.ResetZLevelMetrics();
            topologyTask = pathfinding.GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 5.5f), 0),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);

            Assert.That(topologyTask.IsCompleted, Is.False);
            SpawnUpStairs(testMap, 2.5f);
        });
        await Pair.RunTicksSync(10);
        var topology = await topologyTask!;

        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            environmentTask = pathfinding.GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 5.5f), 0),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);

            Assert.That(environmentTask.IsCompleted, Is.False);
            Assert.That(
                SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, 5),
                Is.True);
        });
        await Pair.RunTicksSync(10);
        var environment = await environmentTask!;
        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, 0),
                Is.True);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            combinedTask = pathfinding.GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 5.5f), 0),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);

            Assert.That(combinedTask.IsCompleted, Is.False);
            SpawnUpStairs(testMap, 4.5f);
            Assert.That(
                SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, 5),
                Is.True);
        });
        await Pair.RunTicksSync(10);
        var combined = await combinedTask!;
        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, 0),
                Is.True);
        });
        await Pair.RunTicksSync(2);

        Assert.Multiple(() =>
        {
            Assert.That(topology.Status, Is.EqualTo(ZLevelPathRouteStatus.TopologyChanged));
            Assert.That(topology.Route, Is.Null);
            Assert.That(topology.Diagnostics.LocalPathsRequested, Is.EqualTo(1));
            Assert.That(environment.Status, Is.EqualTo(ZLevelPathRouteStatus.EnvironmentChanged));
            Assert.That(environment.Route, Is.Null);
            Assert.That(environment.Diagnostics.LocalPathsRequested, Is.EqualTo(2));
            Assert.That(combined.Status,
                Is.EqualTo(ZLevelPathRouteStatus.TopologyAndEnvironmentChanged));
            Assert.That(combined.Route, Is.Null);
            Assert.That(combined.Diagnostics.LocalPathsRequested, Is.EqualTo(2));
        });

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<PathfindingSystem>().SnapshotZLevelRouteMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.Queries, Is.EqualTo(3));
                Assert.That(metrics.TopologyChanges, Is.EqualTo(1));
                Assert.That(metrics.EnvironmentChanges, Is.EqualTo(1));
                Assert.That(metrics.CombinedChanges, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task UpperTileChangeRebuildsOnlyItsOwnFloor()
    {
        var testMap = await Pair.CreateTestMap();
        PathPoly? originalLower = null;

        await Server.WaitAssertion(() => ConfigureCorridors(testMap));
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            originalLower = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 0);
            Assert.That(originalLower, Is.Not.Null);
            Assert.That(pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1), Is.Not.Null);
            pathfinding.ResetZLevelMetrics();

            SEntMan.System<SharedMapSystem>().SetZLevelTile(
                testMap.Grid,
                testMap.Grid.Comp,
                new ZLevelTileIndices(1, 0, 1),
                Tile.Empty);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var currentLower = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 0);
            var currentUpper = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1);
            var metrics = pathfinding.SnapshotZLevelMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(currentLower, Is.SameAs(originalLower));
                Assert.That(
                    currentLower!.Data.Flags & PathfindingBreadcrumbFlag.Space,
                    Is.EqualTo(PathfindingBreadcrumbFlag.None));
                Assert.That(currentUpper, Is.Not.Null);
                Assert.That(
                    currentUpper!.Data.Flags & PathfindingBreadcrumbFlag.Space,
                    Is.EqualTo(PathfindingBreadcrumbFlag.Space));
                Assert.That(metrics.BreadcrumbBuilds, Is.EqualTo(1));
                Assert.That(
                    metrics.MaxBreadcrumbBuildAllocatedBytes,
                    Is.LessThanOrEqualTo(MaxWarmedBreadcrumbBuildAllocatedBytes));
            });
        });

        Assert.That(await RequestPath(testMap, 0, 0), Is.EqualTo(PathResult.Path));
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.NoPath));
    }

    [Test]
    public async Task WorldFrameSelectsLocalFloorWithoutRebuildingNavigation()
    {
        const int frameOrigin = 5;
        var testMap = await Pair.CreateTestMap();
        PathPoly? originalLower = null;
        PathPoly? originalUpper = null;

        await Server.WaitAssertion(() => ConfigureCorridors(testMap));
        await Pair.RunTicksSync(40);
        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            originalLower = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 0);
            originalUpper = pathfinding.GetPoly(Coordinates(testMap, 1.5f), 1);
            Assert.That(originalLower, Is.Not.Null);
            Assert.That(originalUpper, Is.Not.Null);

            var coordinates = Coordinates(testMap, 1.5f);
            for (var i = 0; i < AllocationWarmupIterations; i++)
            {
                pathfinding.GetPoly(coordinates, i % 2);
            }

            var misses = 0;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < AllocationMeasuredIterations; i++)
            {
                if (pathfinding.GetPoly(coordinates, i % 2) is null)
                    misses++;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.Multiple(() =>
            {
                Assert.That(misses, Is.Zero);
                Assert.That(allocated, Is.LessThanOrEqualTo(256));
            });

            pathfinding.ResetZLevelMetrics();
            Assert.That(
                SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, frameOrigin),
                Is.True);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var coordinates = Coordinates(testMap, 1.5f);

            Assert.Multiple(() =>
            {
                Assert.That(pathfinding.GetPoly(coordinates, frameOrigin), Is.SameAs(originalLower));
                Assert.That(pathfinding.GetPoly(coordinates, frameOrigin + 1), Is.SameAs(originalUpper));
                Assert.That(pathfinding.GetPoly(coordinates), Is.SameAs(originalLower));
                Assert.That(pathfinding.GetPoly(coordinates, 0), Is.Null);
                Assert.That(pathfinding.SnapshotZLevelMetrics().BreadcrumbBuilds, Is.Zero);
            });
        });

        Assert.That(await RequestPath(testMap, frameOrigin, frameOrigin), Is.EqualTo(PathResult.Path));
        Assert.That(await RequestPath(testMap, frameOrigin + 1, frameOrigin + 1), Is.EqualTo(PathResult.Path));
        Assert.That(await RequestPath(testMap, frameOrigin, frameOrigin + 1), Is.EqualTo(PathResult.NoPath));
        Assert.That(await RequestLegacyPath(testMap), Is.EqualTo(PathResult.Path));
    }

    private void ConfigureCorridors(TestMapData testMap)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var format = SEntMan.System<SharedZLevelMapSystem>();
        var floor = testMap.Tile.Tile;
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

        grid.CanSplit = false;
        format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
        for (var x = 0; x <= 6; x++)
        {
            map.SetTile(testMap.Grid, grid, new Vector2i(x, 0), floor);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, 0, 1), floor);
        }
    }

    private async Task<PathResult> RequestPath(TestMapData testMap, int startWorldZ, int endWorldZ)
    {
        Task<PathResultEvent>? task = null;
        await Server.WaitPost(() =>
        {
            task = SEntMan.System<PathfindingSystem>().GetPath(
                Coordinates(testMap, 0.5f),
                startWorldZ,
                Coordinates(testMap, 2.5f),
                endWorldZ,
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);
        });
        await Pair.RunTicksSync(10);
        return (await task!).Result;
    }

    private async Task<PathResult> RequestLegacyPath(TestMapData testMap)
    {
        Task<PathResultEvent>? task = null;
        await Server.WaitPost(() =>
        {
            task = SEntMan.System<PathfindingSystem>().GetPath(
                Coordinates(testMap, 0.5f),
                Coordinates(testMap, 2.5f),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);
        });
        await Pair.RunTicksSync(10);
        return (await task!).Result;
    }

    private async Task<ZLevelPathRouteResult> RequestZLevelPath(
        TestMapData testMap,
        float startX,
        int startWorldZ,
        float endX,
        int endWorldZ,
        ZLevelPathSearchBudget? budget = null,
        CancellationToken cancelToken = default)
    {
        Task<ZLevelPathRouteResult>? task = null;
        await Server.WaitPost(() =>
        {
            task = SEntMan.System<PathfindingSystem>().GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, startX), startWorldZ),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, endX), endWorldZ),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                cancelToken,
                budget: budget);
        });
        await Pair.RunTicksSync(10);
        return await task!;
    }

    private EntityUid SpawnUpStairs(TestMapData testMap, float x)
    {
        var stairs = SEntMan.SpawnEntity("ZLevelStairsUp", Coordinates(testMap, x));
        SEntMan.System<ZLevelTraversalGraphSystem>().RefreshTraversal(stairs);
        return stairs;
    }

    private static EntityCoordinates Coordinates(TestMapData testMap, float x)
    {
        return new EntityCoordinates(testMap.Grid, new Vector2(x, 0.5f));
    }
}
