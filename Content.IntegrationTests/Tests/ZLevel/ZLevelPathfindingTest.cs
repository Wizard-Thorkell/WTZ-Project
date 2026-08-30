// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Power.Components;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks.Operators;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.Physics;
using Content.Shared.Power;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelPathfindingTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";
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
    public async Task ShaftAndCatwalkUpdateFloorSupport()
    {
        var testMap = await Pair.CreateTestMap();
        var tileDefinitions = Server.ResolveDependency<IPrototypeManager>();
        var shaft = tileDefinitions.Index(ShaftTile);
        EntityUid catwalk = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(1, 0, 1),
                new Tile(shaft.TileId));
        });
        await Pair.RunTicksSync(40);

        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.NoPath),
            "A non-empty shaft tile must not be treated as walkable floor.");

        await Server.WaitAssertion(() =>
        {
            catwalk = SEntMan.SpawnEntity("Catwalk", Coordinates(testMap, 1.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(catwalk, 1), Is.True);
        });
        await Pair.RunTicksSync(40);

        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.Path),
            "An anchored catwalk must close Body and restore support over the shaft.");

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(catwalk));
        await Pair.RunTicksSync(40);

        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.NoPath),
            "Removing the catwalk must invalidate navigation and reopen the shaft.");
    }

    [Test]
    public async Task BoundaryModeChangesRebuildExistingFloorSupport()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() => ConfigureCorridors(testMap));
        await Pair.RunTicksSync(40);
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.Path));

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
        });
        await Pair.RunTicksSync(40);
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.NoPath),
            "Explicit-only floors require an authored Body-closing provider.");

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
        });
        await Pair.RunTicksSync(40);
        Assert.That(await RequestPath(testMap, 1, 1), Is.EqualTo(PathResult.Path));
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
    public async Task DynamicTraversalPolicyInvalidatesCapturedHierarchicalRoute()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid traversal = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            traversal = SpawnUpStairs(testMap, 2.5f);
            var dynamicTraversal = SEntMan.EnsureComponent<ZLevelDynamicTraversalComponent>(traversal);
            Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>().ConfigureDynamicTraversal(
                traversal,
                true,
                true,
                false,
                TimeSpan.FromSeconds(1.5),
                6f,
                dynamicTraversal),
                Is.True);
        });
        await Pair.RunTicksSync(40);

        var result = await RequestZLevelPath(testMap, 0.5f, 0, 5.5f, 1);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
            Assert.That(result.Route, Is.Not.Null);
            var traversalLeg = result.Route!.Legs.Single(leg => leg.Kind == ZLevelPathLegKind.Traversal);
            Assert.That(traversalLeg.Traversal.Cost, Is.EqualTo(10f));
            Assert.That(traversalLeg.Traversal.TraversalDelay, Is.EqualTo(TimeSpan.FromSeconds(3.5)));
        });

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var dynamicTraversal = SEntMan.GetComponent<ZLevelDynamicTraversalComponent>(traversal);
            Assert.That(pathfinding.ValidateZLevelPathRoute(result.Route!),
                Is.EqualTo(ZLevelPathRouteValidationResult.Valid));
            Assert.That(graph.ConfigureDynamicTraversal(
                traversal,
                true,
                true,
                false,
                TimeSpan.FromSeconds(2),
                6f,
                dynamicTraversal),
                Is.True);

            var validation = pathfinding.ValidateZLevelPathRoute(result.Route!);
            Assert.Multiple(() =>
            {
                Assert.That(validation.Status, Is.EqualTo(ZLevelPathRouteValidationStatus.TraversalChanged));
                Assert.That(validation.LegIndex, Is.EqualTo(1));
            });

            Assert.That(graph.ConfigureDynamicTraversal(
                traversal,
                false,
                true,
                false,
                TimeSpan.FromSeconds(2),
                6f,
                dynamicTraversal),
                Is.True);
        });

        var unavailable = await RequestZLevelPath(testMap, 0.5f, 0, 5.5f, 1);
        Assert.Multiple(() =>
        {
            Assert.That(unavailable.Status, Is.EqualTo(ZLevelPathRouteStatus.NoPath));
            Assert.That(unavailable.Route, Is.Null);
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

    [Test]
    public async Task HierarchicalRouteSnapshotsEntityEndpointBeforeAwaitingPaths()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        Task<ZLevelPathRouteResult>? task = null;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            task = pathfinding.GetZLevelPath(
                npc,
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, new EntityCoordinates(target, Vector2.Zero), 1),
                0.2f,
                CancellationToken.None,
                pathfinding.GetFlags(npc));
            Assert.That(task.IsCompleted, Is.False);

            SEntMan.System<SharedTransformSystem>().SetCoordinates(target, Coordinates(testMap, 3.5f));
        });
        await Pair.RunTicksSync(10);
        var result = await task!;

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(result.Route!.End.Coordinates.EntityId, Is.EqualTo(testMap.Grid.Owner));
                Assert.That(
                    SEntMan.System<SharedTransformSystem>().ToMapCoordinates(result.Route.End.Coordinates).Position.X,
                    Is.EqualTo(5.5f).Within(0.001f));
            });

            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, result.Route!, steering), Is.False,
                "A route to the pre-await target position must be stale after meaningful local movement.");
        });
    }

    [Test]
    public async Task HierarchicalRouteRejectsStaleActorEndpointAfterAwait()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        Task<ZLevelPathRouteResult>? task = null;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            task = pathfinding.GetZLevelPath(
                npc,
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, new EntityCoordinates(target, Vector2.Zero), 1),
                0.2f,
                CancellationToken.None,
                pathfinding.GetFlags(npc));
            Assert.That(task.IsCompleted, Is.False);

            SEntMan.System<SharedTransformSystem>().SetCoordinates(npc, Coordinates(testMap, 5.5f));
        });
        await Pair.RunTicksSync(10);
        var result = await task!;

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(
                    SEntMan.System<SharedTransformSystem>().ToMapCoordinates(result.Route!.Start.Coordinates).Position.X,
                    Is.EqualTo(0.5f).Within(0.001f));
            });

            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, result.Route!, steering), Is.False,
                "A route must not install after its actor moved away from the captured start.");
        });
    }

    [TestCase(NPCBlackboard.FollowTarget, NPCBlackboard.PathfindKey)]
    [TestCase("TargetCoordinates", "TargetPathfind")]
    public async Task MoveToOperatorPlansFollowAndHostileHierarchicalRoutes(
        string targetKey,
        string pathfindKey)
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        MoveToOperator? moveTo = null;
        NPCBlackboard? blackboard = null;
        Task<(bool Valid, Dictionary<string, object>? Effects)>? planTask = null;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);

            moveTo = new MoveToOperator();
            moveTo.TargetKey = targetKey;
            moveTo.PathfindKey = pathfindKey;
            moveTo.Initialize(SEntMan.EntitySysManager);
            blackboard = new NPCBlackboard();
            blackboard.SetValue(NPCBlackboard.Owner, npc);
            blackboard.SetValue(moveTo.TargetKey, new EntityCoordinates(target, Vector2.Zero));
            blackboard.SetValue(moveTo.RangeKey, 0.2f);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitPost(() =>
        {
            planTask = moveTo!.Plan(blackboard!, CancellationToken.None);
            Assert.That(planTask.IsCompleted, Is.False);
        });
        await Pair.RunTicksSync(10);
        var plan = await planTask!;

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(plan.Valid, Is.True);
                Assert.That(plan.Effects, Is.Not.Null);
                Assert.That(plan.Effects, Does.ContainKey($"{pathfindKey}:ZLevel"));
            });

            foreach (var (key, value) in plan.Effects!)
            {
                blackboard!.SetValue(key, value);
            }

            moveTo!.Startup(blackboard!);
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            Assert.Multiple(() =>
            {
                Assert.That(steering.ZLevelRoute, Is.Not.Null);
                Assert.That(steering.TargetWorldZ, Is.EqualTo(1));
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.Moving));
            });

            moveTo.ConditionalShutdown(blackboard!);
        });
    }

    [Test]
    public async Task NPCSteeringExecutesAuthoredVerticalRouteWithTraversalDelay()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        ZLevelPathRouteResult routeResult = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            var coordinates = Coordinates(testMap, 2.5f);
            SpawnUpStairs(testMap, 2.5f);

            target = SEntMan.SpawnEntity(null, coordinates);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            SEntMan.EnsureComponent<GodmodeComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        Task<ZLevelPathRouteResult>? routeTask = null;
        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            routeTask = pathfinding.GetZLevelPath(
                npc,
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 2.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, new EntityCoordinates(target, Vector2.Zero), 1),
                0.2f,
                CancellationToken.None,
                pathfinding.GetFlags(npc));
        });
        await Pair.RunTicksSync(10);
        routeResult = await routeTask!;

        Assert.Multiple(() =>
        {
            Assert.That(routeResult.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
            Assert.That(routeResult.Route, Is.Not.Null);
            Assert.That(routeResult.Route!.Legs.Any(leg => leg.Kind == ZLevelPathLegKind.Traversal), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            SEntMan.System<SharedTransformSystem>().SetCoordinates(npc, Coordinates(testMap, 2.5f));
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            steering.Range = 0.2f;

            Assert.Multiple(() =>
            {
                Assert.That(steering.TargetWorldZ, Is.EqualTo(1));
                Assert.That(steeringSystem.TryInstallZLevelRoute(npc, routeResult.Route!, steering), Is.True);
            });
        });

        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.Zero);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(npc), Is.True,
                    "NPC traversal should expose the same progress bar contract as player traversal.");
                Assert.That(SEntMan.GetComponent<NPCSteeringComponent>(npc).Status,
                    Is.EqualTo(SteeringStatus.Moving));
            });
        });

        await Pair.RunSeconds(1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.Zero,
                "The authored traversal delay must not complete early.");
        });

        await Pair.RunSeconds(1.2f);
        await Pair.RunTicksSync(2);
        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var metrics = steeringSystem.SnapshotZLevelMetrics();
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var state = $"route={steering.ZLevelRoute != null}, leg={steering.ZLevelLegIndex}, " +
                        $"loaded={steering.LoadedZLevelLegIndex}, local={steering.CurrentPath.Count}, " +
                        $"failures={steering.FailedPathCount}, targetZ={steering.TargetWorldZ}, " +
                        $"lastReplan={steering.LastZLevelReplanReason}, " +
                        $"replans={metrics.Replans}, executionFailures={metrics.ExecutionFailures}";
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.EqualTo(1));
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.InRange), state);
                Assert.That(metrics.RoutesInstalled, Is.EqualTo(1));
                Assert.That(metrics.RoutesCompleted, Is.EqualTo(1));
                Assert.That(metrics.TraversalsStarted, Is.EqualTo(1));
                Assert.That(metrics.TraversalsCompleted, Is.EqualTo(1));
                Assert.That(metrics.Replans, Is.Zero);
                Assert.That(metrics.ExecutionFailures, Is.Zero);
            });
        });
    }

    [Test]
    public async Task NPCSteeringCallsAndRidesPhysicalElevator()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        EntityUid cabin = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            cabin = SpawnPhysicalElevator(
                testMap,
                2.5f,
                cabinFloor: 1,
                travelTimePerLevel: TimeSpan.FromSeconds(0.15));
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
            npc = SpawnTraversalUser(testMap, 0.5f);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            Assert.That(graph.CreateSnapshot(testMap.MapId).Edges.Count(edge =>
                edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.EqualTo(2));

            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var steering = SEntMan.System<NPCSteeringSystem>();
            elevators.ResetMetrics();
            steering.ResetZLevelMetrics();
            steering.Register(npc, new EntityCoordinates(target, Vector2.Zero)).Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });

        await Pair.RunSeconds(12f);
        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var steeringMetrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var navigation = elevators.NavigationSnapshot();
            var physical = elevators.Snapshot();
            var transform = SEntMan.GetComponent<TransformComponent>(npc);
            var distance = transform.Coordinates.TryDistance(
                SEntMan,
                new EntityCoordinates(target, Vector2.Zero),
                out var value)
                ? value
                : float.PositiveInfinity;
            var state = $"status={steering.Status},z={SEntMan.System<SharedZLevelSystem>().GetZLevel(npc)}," +
                        $"distance={distance:F2},replan={steering.LastZLevelReplanReason}," +
                        $"failure={steering.LastZLevelExecutionFailureReason}," +
                        $"nav={navigation.Started}/{navigation.Completed}/{navigation.Cancelled}/" +
                        $"{navigation.Rejected}";

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(cabin), Is.EqualTo(1), state);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(npc), Is.EqualTo(1), state);
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.InRange), state);
                Assert.That(distance, Is.LessThanOrEqualTo(steering.Range), state);
                Assert.That(navigation.Active, Is.Zero, state);
                Assert.That(navigation.Started, Is.EqualTo(1), state);
                Assert.That(navigation.Completed, Is.EqualTo(1), state);
                Assert.That(navigation.Cancelled, Is.Zero, state);
                Assert.That(navigation.Rejected, Is.Zero, state);
                Assert.That(physical.Started, Is.EqualTo(2), state);
                Assert.That(physical.Completed, Is.EqualTo(2), state);
                Assert.That(steeringMetrics.RoutesInstalled, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.RoutesCompleted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.TraversalsStarted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.TraversalsCompleted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.Replans, Is.Zero, state);
                Assert.That(steeringMetrics.ExecutionFailures, Is.Zero, state);
            });
        });
    }

    [Test]
    public async Task NPCSteeringPlansAndExecutesCrossFloorRoute()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            SEntMan.EnsureComponent<GodmodeComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.System<NPCSteeringSystem>();
            steering.ResetZLevelMetrics();
            steering.Register(npc, new EntityCoordinates(target, Vector2.Zero)).Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.That(metrics.RoutesInstalled, Is.EqualTo(1),
                "Runtime steering should request and install the typed route itself.");
        });

        var checkpoints = new List<string>();
        for (var i = 0; i < 16; i++)
        {
            await Pair.RunSeconds(0.5f);
            await Server.WaitAssertion(() =>
            {
                var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
                var transform = SEntMan.GetComponent<TransformComponent>(npc);
                var body = SEntMan.GetComponent<PhysicsComponent>(npc);
                checkpoints.Add(
                    $"{(i + 1) * 0.5f:F1}s:x={transform.LocalPosition.X:F2},z=" +
                    $"{SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel},v={body.LinearVelocity.X:F2}," +
                    $"status={steering.Status},failure={steering.LastZLevelExecutionFailureReason}," +
                    $"leg={steering.ZLevelLegIndex},local={steering.CurrentPath.Count}");
            });
        }
        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            var transform = SEntMan.GetComponent<TransformComponent>(npc);
            var distance = transform.Coordinates.TryDistance(
                SEntMan,
                new EntityCoordinates(target, Vector2.Zero),
                out var value)
                ? value
                : float.PositiveInfinity;
            var state = $"status={steering.Status}, z={SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel}, " +
                        $"position={transform.LocalPosition}, distance={distance}, route={steering.ZLevelRoute != null}, " +
                        $"leg={steering.ZLevelLegIndex}, local={steering.CurrentPath.Count}, " +
                        $"lastReplan={steering.LastZLevelReplanReason}, " +
                        $"failure={steering.LastZLevelExecutionFailureReason}, installed={metrics.RoutesInstalled}, " +
                        $"completed={metrics.RoutesCompleted}, replans={metrics.Replans}; " +
                        string.Join(" | ", checkpoints);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.EqualTo(1), state);
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.InRange), state);
                Assert.That(distance, Is.LessThanOrEqualTo(steering.Range), state);
                Assert.That(metrics.RoutesInstalled, Is.EqualTo(1), state);
                Assert.That(metrics.RoutesCompleted, Is.EqualTo(1), state);
                Assert.That(metrics.TraversalsStarted, Is.EqualTo(1), state);
                Assert.That(metrics.TraversalsCompleted, Is.EqualTo(1), state);
                Assert.That(metrics.Replans, Is.Zero, state);
                Assert.That(metrics.ExecutionFailures, Is.Zero, state);
            });
        });
    }

    [Test]
    public async Task NPCSteeringReplansWhenAuthoredTraversalIsDeleted()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        EntityUid stairs = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            stairs = SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        var route = await RequestNPCZLevelPath(testMap, npc, target);
        Assert.That(route.Succeeded, Is.True);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, route.Route!, steering), Is.True);
            Assert.That(SEntMan.System<PathfindingSystem>().ValidateZLevelPathRoute(route.Route!).IsValid, Is.True);

            SEntMan.DeleteEntity(stairs);
            Assert.That(SEntMan.System<PathfindingSystem>().ValidateZLevelPathRoute(route.Route!).Status,
                Is.EqualTo(ZLevelPathRouteValidationStatus.TraversalChanged));
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(steering.ZLevelRoute, Is.Null);
                Assert.That(steering.LastZLevelReplanReason, Is.EqualTo(NPCZLevelReplanReason.RouteInvalid));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.Zero);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(npc), Is.False);
                Assert.That(metrics.RoutesInstalled, Is.EqualTo(1));
                Assert.That(metrics.Replans, Is.EqualTo(1));
                Assert.That(metrics.TraversalsStarted, Is.Zero);
            });
        });
    }

    [Test]
    public async Task NPCSteeringReplansWhenTrackedTargetChangesFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        var route = await RequestNPCZLevelPath(testMap, npc, target);
        Assert.That(route.Succeeded, Is.True);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            var offsetTarget = new EntityCoordinates(target, new Vector2(0.25f, 0f));
            Assert.Multiple(() =>
            {
                Assert.That(steeringSystem.ResolveTargetWorldZ(npc, offsetTarget, out var tracked), Is.EqualTo(1));
                Assert.That(tracked, Is.EqualTo(target));
            });

            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, route.Route!, steering), Is.True);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 0), Is.True);
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(steering.TargetWorldZ, Is.Zero);
                Assert.That(steering.ZLevelRoute, Is.Null);
                Assert.That(steering.LastZLevelReplanReason,
                    Is.EqualTo(NPCZLevelReplanReason.TargetFloorChanged));
                Assert.That(metrics.RoutesInstalled, Is.EqualTo(1));
                Assert.That(metrics.Replans, Is.EqualTo(1));
                Assert.That(metrics.TraversalsStarted, Is.Zero);
            });
        });
    }

    [Test]
    public async Task NPCSteeringCancelsPendingTraversalWhenRouteReplans()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        EntityUid stairs = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            stairs = SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 2.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            SEntMan.EnsureComponent<GodmodeComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        var route = await RequestNPCZLevelPath(testMap, npc, target);
        Assert.That(route.Succeeded, Is.True);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, route.Route!, steering), Is.True);
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(npc, stairs), Is.True);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 0), Is.True);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(npc), Is.False);
                Assert.That(steering.ZLevelRoute, Is.Null);
                Assert.That(steering.LastZLevelReplanReason,
                    Is.EqualTo(NPCZLevelReplanReason.TargetFloorChanged));
                Assert.That(metrics.TraversalsStarted, Is.EqualTo(1));
                Assert.That(metrics.TraversalsCompleted, Is.Zero);
                Assert.That(metrics.Replans, Is.EqualTo(1));
            });
        });

        await Pair.RunSeconds(2.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(npc).ZLevel, Is.Zero,
                "A cancelled traversal must not complete after its original delay.");
        });
    }

    [Test]
    public async Task DeletingTraversalCancelsEveryPendingUser()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid stairs = default;
        EntityUid firstUser = default;
        EntityUid secondUser = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            stairs = SpawnUpStairs(testMap, 2.5f);
            firstUser = SpawnTraversalUser(testMap, 2.5f);
            secondUser = SpawnTraversalUser(testMap, 2.5f);

            var traversal = SEntMan.System<ZLevelTraversalSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(traversal.TryStartTraversal(stairs, firstUser), Is.True);
                Assert.That(traversal.TryStartTraversal(stairs, secondUser), Is.True);
                Assert.That(traversal.IsTraversalPending(firstUser, stairs), Is.True);
                Assert.That(traversal.IsTraversalPending(secondUser, stairs), Is.True);
            });

            SEntMan.DeleteEntity(stairs);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var traversal = SEntMan.System<ZLevelTraversalSystem>();
            var firstDoAfters = SEntMan.GetComponent<DoAfterComponent>(firstUser);
            var secondDoAfters = SEntMan.GetComponent<DoAfterComponent>(secondUser);
            Assert.Multiple(() =>
            {
                Assert.That(traversal.IsTraversalPending(firstUser), Is.False);
                Assert.That(traversal.IsTraversalPending(secondUser), Is.False);
                Assert.That(firstDoAfters.DoAfters.Values.All(doAfter => doAfter.Cancelled), Is.True);
                Assert.That(secondDoAfters.DoAfters.Values.All(doAfter => doAfter.Cancelled), Is.True);
            });
        });

        await Pair.RunSeconds(2.2f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(firstUser).ZLevel, Is.Zero);
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(secondUser).ZLevel, Is.Zero);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(firstUser), Is.False);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(secondUser), Is.False);
            });
        });
    }

    [Test]
    public async Task NPCSteeringDistinguishesGridMotionFromTargetMotion()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            SpawnUpStairs(testMap, 2.5f);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, 0.5f));
            SEntMan.RemoveComponent<HTNComponent>(npc);
            SEntMan.RemoveComponent<ActiveNPCComponent>(npc);
            SEntMan.EnsureComponent<GodmodeComponent>(npc);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(npc, 0), Is.True);
        });
        await Pair.RunTicksSync(40);

        var route = await RequestNPCZLevelPath(testMap, npc, target);
        Assert.That(route.Succeeded, Is.True);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, route.Route!, steering), Is.True);

            SEntMan.System<SharedTransformSystem>().SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var planned = SEntMan.System<SharedTransformSystem>()
                .ToMapCoordinates(steering.ZLevelPlannedTargetCoordinates);
            var current = SEntMan.System<SharedTransformSystem>()
                .ToMapCoordinates(new EntityCoordinates(target, Vector2.Zero));
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(steering.ZLevelRoute, Is.Not.Null,
                    "Moving the route's coordinate frame must not look like target motion.");
                Assert.That(planned, Is.EqualTo(current));
                Assert.That(steering.LastZLevelReplanReason, Is.EqualTo(NPCZLevelReplanReason.None));
                Assert.That(metrics.Replans, Is.Zero);
            });

            SEntMan.System<SharedTransformSystem>().SetCoordinates(target, Coordinates(testMap, 3.5f));
        });
        await Pair.RunTicksSync(10);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var planned = SEntMan.System<SharedTransformSystem>()
                .ToMapCoordinates(steering.ZLevelPlannedTargetCoordinates);
            var current = SEntMan.System<SharedTransformSystem>()
                .ToMapCoordinates(new EntityCoordinates(target, Vector2.Zero));
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(steering.ZLevelRoute, Is.Not.Null,
                    "The replacement route should be installed after meaningful target motion.");
                Assert.That(planned, Is.EqualTo(current));
                Assert.That(steering.LastZLevelReplanReason, Is.EqualTo(NPCZLevelReplanReason.TargetMoved));
                Assert.That(metrics.RoutesInstalled, Is.EqualTo(2));
                Assert.That(metrics.Replans, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task UnrelatedMapChangesPreserveSnapshotsAndInFlightRouteSearch()
    {
        var routeMap = await Pair.CreateTestMap();
        var noisyMap = await Pair.CreateTestMap();
        EntityUid noisyTraversal = default;
        ZLevelTraversalGraphSnapshot routeSnapshot = default;
        ZLevelTraversalGraphSnapshot noisySnapshot = default;
        Task<ZLevelPathRouteResult>? routeTask = null;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(routeMap);
            ConfigureCorridors(noisyMap);
            SpawnUpStairs(routeMap, 2.5f);
            noisyTraversal = SpawnUpStairs(noisyMap, 2.5f);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitPost(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var pathfinding = SEntMan.System<PathfindingSystem>();
            graph.ResetMetrics();
            routeSnapshot = graph.CreateSnapshot(routeMap.MapId);
            noisySnapshot = graph.CreateSnapshot(noisyMap.MapId);

            routeTask = pathfinding.GetZLevelPath(
                new ZLevelPathEndpoint(routeMap.MapId, Coordinates(routeMap, 0.5f), 0),
                new ZLevelPathEndpoint(routeMap.MapId, Coordinates(routeMap, 5.5f), 1),
                0f,
                (int) CollisionGroup.MobLayer,
                (int) CollisionGroup.MobMask,
                CancellationToken.None);
            Assert.That(routeTask.IsCompleted, Is.False);

            var dynamicTraversal = SEntMan.EnsureComponent<ZLevelDynamicTraversalComponent>(noisyTraversal);
            Assert.That(graph.ConfigureDynamicTraversal(
                noisyTraversal,
                false,
                true,
                false,
                TimeSpan.Zero,
                0f,
                dynamicTraversal),
                Is.True);
            SpawnUpStairs(noisyMap, 4.5f);

            Assert.Multiple(() =>
            {
                Assert.That(graph.GetVersion(routeMap.MapId), Is.EqualTo(routeSnapshot.Version));
                Assert.That(graph.ValidateSnapshot(routeSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
                Assert.That(graph.ValidateSnapshot(noisySnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.TopologyAndEnvironmentChanged));
                Assert.That(graph.CreateSnapshot(routeMap.MapId).Edges.Equals(routeSnapshot.Edges), Is.True,
                    "Unrelated map churn must retain the detached edge storage for this map.");
            });
        });

        await Pair.RunTicksSync(10);
        var route = await routeTask!;

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var metrics = graph.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(route.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
                Assert.That(route.Route, Is.Not.Null);
                Assert.That(route.Diagnostics.TopologyRevision, Is.EqualTo(routeSnapshot.TopologyRevision));
                Assert.That(route.Diagnostics.EnvironmentRevision, Is.EqualTo(routeSnapshot.EnvironmentRevision));
                Assert.That(graph.ValidateSnapshot(routeSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
                Assert.That(metrics.TrackedMapRevisions, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.SnapshotBuilds, Is.EqualTo(2));
                Assert.That(metrics.SnapshotCacheHits, Is.GreaterThanOrEqualTo(2));
            });

            var trackedMaps = graph.TrackedMapRevisionCount;
            var cachedSnapshots = metrics.CachedSnapshots;
            SEntMan.System<SharedMapSystem>().DeleteMap(noisyMap.MapId);
            Assert.Multiple(() =>
            {
                Assert.That(graph.TrackedMapRevisionCount, Is.EqualTo(trackedMaps - 1));
                Assert.That(graph.Snapshot().CachedSnapshots, Is.EqualTo(cachedSnapshots - 1));
                Assert.That(graph.ValidateSnapshot(routeSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
            });
        });
    }

    [Test]
    public async Task UnrelatedMapChangesDoNotRevalidateAnActiveRoute()
    {
        var routeMap = await Pair.CreateTestMap();
        var noisyMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;
        EntityUid noisyTraversal = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(routeMap);
            ConfigureCorridors(noisyMap);
            SpawnUpStairs(routeMap, 2.5f);
            noisyTraversal = SpawnUpStairs(noisyMap, 2.5f);
            var dynamicTraversal = SEntMan.EnsureComponent<ZLevelDynamicTraversalComponent>(noisyTraversal);
            Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>().ConfigureDynamicTraversal(
                noisyTraversal,
                true,
                true,
                false,
                TimeSpan.Zero,
                0f,
                dynamicTraversal),
                Is.True);

            target = SEntMan.SpawnEntity(null, Coordinates(routeMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
            npc = SpawnTraversalUser(routeMap, 0.5f);
        });
        await Pair.RunTicksSync(40);

        var route = await RequestNPCZLevelPath(routeMap, npc, target);
        Assert.That(route.Succeeded, Is.True);

        ZLevelTraversalGraphVersion routeVersion = default;
        long globalTopology = 0;
        long globalEnvironment = 0;
        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var steering = steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero));
            steering.Range = 0.2f;
            Assert.That(steeringSystem.TryInstallZLevelRoute(npc, route.Route!, steering), Is.True);

            routeVersion = graph.GetVersion(routeMap.MapId);
            globalTopology = graph.TopologyRevision;
            globalEnvironment = graph.EnvironmentRevision;
            graph.ResetMetrics();
            steeringSystem.ResetZLevelMetrics();

            var dynamicTraversal = SEntMan.GetComponent<ZLevelDynamicTraversalComponent>(noisyTraversal);
            Assert.That(graph.ConfigureDynamicTraversal(
                noisyTraversal,
                false,
                true,
                false,
                TimeSpan.Zero,
                0f,
                dynamicTraversal),
                Is.True);
            SpawnUpStairs(noisyMap, 4.5f);
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var steeringMetrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(graph.TopologyRevision, Is.GreaterThan(globalTopology));
                Assert.That(graph.EnvironmentRevision, Is.GreaterThan(globalEnvironment));
                Assert.That(graph.GetVersion(routeMap.MapId), Is.EqualTo(routeVersion));
                Assert.That(steering.ZLevelRoute, Is.SameAs(route.Route));
                Assert.That(steering.ZLevelValidatedTopologyRevision, Is.EqualTo(routeVersion.TopologyRevision));
                Assert.That(steering.ZLevelValidatedEnvironmentRevision, Is.EqualTo(routeVersion.EnvironmentRevision));
                Assert.That(steering.LastZLevelReplanReason, Is.EqualTo(NPCZLevelReplanReason.None));
                Assert.That(steeringMetrics.Replans, Is.Zero);
                Assert.That(graph.Snapshot().EdgeQueries, Is.Zero,
                    "A revision change on another map must not force exact-edge route validation.");
            });
        });
    }

    [Test]
    public async Task ConcurrentNPCsPlanAndExecuteIndependentVerticalRoutes()
    {
        const int npcCount = 8;
        var testMap = await Pair.CreateTestMap();
        var npcs = new List<EntityUid>(npcCount);
        var targets = new List<EntityUid>(npcCount);
        var cachedChunksBeforeRoutes = 0;
        var cachedFloorsBeforeRoutes = 0;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap, npcCount);
            for (var lane = 0; lane < npcCount; lane++)
            {
                SpawnUpStairs(testMap, 2.5f, lane);
                var target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f, lane));
                Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
                targets.Add(target);
                npcs.Add(SpawnTraversalUser(testMap, 0.5f, lane));
            }
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var floorMetrics = pathfinding.SnapshotZLevelMetrics();
            cachedChunksBeforeRoutes = floorMetrics.CachedChunks;
            cachedFloorsBeforeRoutes = floorMetrics.CachedFloors;
            Assert.Multiple(() =>
            {
                Assert.That(cachedFloorsBeforeRoutes, Is.EqualTo(2));
                Assert.That(floorMetrics.PendingChunks, Is.Zero);
            });
            pathfinding.ResetZLevelRouteMetrics();
            steeringSystem.ResetZLevelMetrics();
            for (var i = 0; i < npcCount; i++)
            {
                var steering = steeringSystem.Register(npcs[i], new EntityCoordinates(targets[i], Vector2.Zero));
                steering.Range = 0.2f;
                SEntMan.EnsureComponent<ActiveNPCComponent>(npcs[i]);
            }
        });

        await Pair.RunSeconds(12f);
        await Server.WaitAssertion(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            var routeMetrics = pathfinding.SnapshotZLevelRouteMetrics();
            var floorMetrics = pathfinding.SnapshotZLevelMetrics();
            var steeringMetrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            var failures = new List<string>();
            for (var i = 0; i < npcCount; i++)
            {
                var steering = SEntMan.GetComponent<NPCSteeringComponent>(npcs[i]);
                var transform = SEntMan.GetComponent<TransformComponent>(npcs[i]);
                var distance = transform.Coordinates.TryDistance(
                    SEntMan,
                    new EntityCoordinates(targets[i], Vector2.Zero),
                    out var value)
                    ? value
                    : float.PositiveInfinity;
                if (SEntMan.System<SharedZLevelSystem>().GetZLevel(npcs[i]) != 1 ||
                    steering.Status != SteeringStatus.InRange ||
                    distance > steering.Range)
                {
                    failures.Add(
                        $"npc={i},z={SEntMan.System<SharedZLevelSystem>().GetZLevel(npcs[i])}," +
                        $"status={steering.Status},distance={distance:F2},route={steering.ZLevelRoute != null}," +
                        $"replan={steering.LastZLevelReplanReason},failure={steering.LastZLevelExecutionFailureReason}");
                }
            }

            var state = string.Join(" | ", failures);
            Assert.Multiple(() =>
            {
                Assert.That(failures, Is.Empty, state);
                Assert.That(routeMetrics.Queries, Is.EqualTo(npcCount), state);
                Assert.That(routeMetrics.Successes, Is.EqualTo(npcCount), state);
                Assert.That(routeMetrics.StateBudgetExhaustions, Is.Zero, state);
                Assert.That(routeMetrics.LocalPathBudgetExhaustions, Is.Zero, state);
                Assert.That(routeMetrics.TraversalEdgeBudgetExhaustions, Is.Zero, state);
                Assert.That(steeringMetrics.RoutesInstalled, Is.EqualTo(npcCount), state);
                Assert.That(steeringMetrics.RoutesCompleted, Is.EqualTo(npcCount), state);
                Assert.That(steeringMetrics.TraversalsStarted, Is.EqualTo(npcCount), state);
                Assert.That(steeringMetrics.TraversalsCompleted, Is.EqualTo(npcCount), state);
                Assert.That(steeringMetrics.Replans, Is.Zero, state);
                Assert.That(steeringMetrics.ExecutionFailures, Is.Zero, state);
                Assert.That(floorMetrics.CachedFloors, Is.EqualTo(cachedFloorsBeforeRoutes), state);
                Assert.That(floorMetrics.CachedChunks, Is.EqualTo(cachedChunksBeforeRoutes), state);
                Assert.That(floorMetrics.PendingChunks, Is.Zero, state);
            });
        });
    }

    private void ConfigureCorridors(TestMapData testMap, int laneCount = 1)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var format = SEntMan.System<SharedZLevelMapSystem>();
        var floor = testMap.Tile.Tile;
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

        grid.CanSplit = false;
        format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
        for (var lane = 0; lane < laneCount; lane++)
        {
            var y = lane * 2;
            for (var x = 0; x <= 6; x++)
            {
                map.SetTile(testMap.Grid, grid, new Vector2i(x, y), floor);
                map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, y, 1), floor);
            }
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

    private async Task<ZLevelPathRouteResult> RequestNPCZLevelPath(
        TestMapData testMap,
        EntityUid npc,
        EntityUid target)
    {
        Task<ZLevelPathRouteResult>? task = null;
        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            task = pathfinding.GetZLevelPath(
                npc,
                new ZLevelPathEndpoint(testMap.MapId, SEntMan.GetComponent<TransformComponent>(npc).Coordinates, 0),
                new ZLevelPathEndpoint(testMap.MapId, new EntityCoordinates(target, Vector2.Zero), 1),
                0.2f,
                CancellationToken.None,
                pathfinding.GetFlags(npc));
        });
        await Pair.RunTicksSync(10);
        return await task!;
    }

    private EntityUid SpawnUpStairs(TestMapData testMap, float x, int lane = 0)
    {
        var stairs = SEntMan.SpawnEntity("ZLevelStairsUp", Coordinates(testMap, x, lane));
        SEntMan.System<ZLevelTraversalGraphSystem>().RefreshTraversal(stairs);
        return stairs;
    }

    private EntityUid SpawnPhysicalElevator(
        TestMapData testMap,
        float x,
        int cabinFloor,
        TimeSpan travelTimePerLevel)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var coordinates = Coordinates(testMap, x);
        var tile = map.TileIndicesFor(testMap.Grid, grid, coordinates);
        var shaft = prototypes.Index(ShaftTile);
        for (var z = 0; z <= 1; z++)
        {
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, z),
                new Tile(shaft.TileId));
        }

        SpawnAnchoredZLevelEntity("ZLevelElevatorStop", coordinates, 0);
        SpawnAnchoredZLevelEntity("ZLevelElevatorStop", coordinates, 1);
        var cabin = SpawnAnchoredZLevelEntity("ZLevelElevatorCabin", coordinates, cabinFloor);
        var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
        component.TravelTimePerLevel = travelTimePerLevel;
        var power = SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin);
        power.NeedsPower = false;
        power.PowerDisabled = false;
        power.Powered = true;
        var powerChanged = new PowerChangedEvent(true, power.Load);
        SEntMan.EventBus.RaiseLocalEvent(cabin, ref powerChanged);
        return cabin;
    }

    private EntityUid SpawnAnchoredZLevelEntity(
        string prototype,
        EntityCoordinates coordinates,
        int localZ)
    {
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var xform = SEntMan.GetComponent<TransformComponent>(uid);
        Assert.That(zLevels.SetZLevelPosition(uid, localZ), Is.True);
        if (!xform.Anchored)
            transform.AnchorEntity(uid, xform);
        Assert.That(xform.Anchored, Is.True);
        return uid;
    }

    private EntityUid SpawnTraversalUser(TestMapData testMap, float x, int lane = 0)
    {
        var user = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, x, lane));
        SEntMan.RemoveComponent<HTNComponent>(user);
        SEntMan.RemoveComponent<ActiveNPCComponent>(user);
        SEntMan.EnsureComponent<GodmodeComponent>(user);
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(user, 0), Is.True);
        return user;
    }

    private static EntityCoordinates Coordinates(TestMapData testMap, float x, int lane = 0)
    {
        return new EntityCoordinates(testMap.Grid, new Vector2(x, lane * 2 + 0.5f));
    }
}
