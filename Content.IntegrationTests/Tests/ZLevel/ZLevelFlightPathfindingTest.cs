// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using System.Numerics;
using System.Threading;
using Content.IntegrationTests.Fixtures;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server.ZLevel.Navigation;
using Content.Shared.Damage.Components;
using Content.Shared.Maps;
using Content.Shared.NPC;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelFlightPathfindingTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";

    [Test]
    public async Task FlightNavigationIsExplicitCapabilityGatedAndInvalidatable()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid marker = default;
        EntityUid flyer = default;
        EntityUid walker = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            marker = ConfigureFlightCorridor(testMap, 2);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            walker = SpawnUser(testMap, 0.5f);
            flyer = SpawnUser(testMap, 0.5f);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(flyer);
        });
        await Pair.RunTicksSync(40);

        var untyped = await RequestUntypedPath(testMap);
        var walking = await RequestActorPath(testMap, walker, target);
        var flying = await RequestActorPath(testMap, flyer, target);

        Assert.Multiple(() =>
        {
            Assert.That(untyped.Status, Is.EqualTo(ZLevelPathRouteStatus.NoPath),
                "Explicit endpoint searches must not assume an actor can fly.");
            Assert.That(walking.Status, Is.EqualTo(ZLevelPathRouteStatus.NoPath),
                "A normal mob must not consume flight-only graph edges.");
            Assert.That(flying.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
            Assert.That(flying.Route, Is.Not.Null);
            Assert.That(flying.Route!.Legs.Select(leg => leg.Kind), Is.EqualTo(new[]
            {
                ZLevelPathLegKind.Local,
                ZLevelPathLegKind.Flight,
                ZLevelPathLegKind.Local,
            }));
            Assert.That(flying.Diagnostics.TraversalEdgesEvaluated, Is.Zero);
            Assert.That(flying.Diagnostics.FlightEdgesEvaluated, Is.GreaterThanOrEqualTo(1));
        });

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var snapshot = graph.CreateSnapshot(testMap.MapId);
            var flightLeg = flying.Route!.Legs.Single(leg => leg.Kind == ZLevelPathLegKind.Flight);
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Edges, Is.Empty);
                Assert.That(snapshot.FlightEdges, Has.Length.EqualTo(2));
                Assert.That(flightLeg.Flight.Source.Marker, Is.EqualTo(marker));
                Assert.That(flightLeg.Flight.Source.Tile, Is.EqualTo(new Vector2i(2, 0)));
                Assert.That(flightLeg.Flight.ApertureTile, Is.EqualTo(new Vector2i(2, 0)));
                Assert.That(flightLeg.Flight.Destination.Tile, Is.EqualTo(new Vector2i(3, 0)));
                Assert.That(SEntMan.System<PathfindingSystem>().ValidateZLevelPathRoute(flying.Route!),
                    Is.EqualTo(ZLevelPathRouteValidationResult.Valid));
            });

            SEntMan.DeleteEntity(marker);
            var validation = SEntMan.System<PathfindingSystem>().ValidateZLevelPathRoute(flying.Route!);
            Assert.Multiple(() =>
            {
                Assert.That(validation.Status, Is.EqualTo(ZLevelPathRouteValidationStatus.TraversalChanged));
                Assert.That(validation.LegIndex, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task FlightNavigationTracksBoundarySupportAndMarkerRotation()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid marker = default;
        ZLevelTraversalGraphSnapshot openSnapshot = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            marker = ConfigureFlightCorridor(testMap, 2);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            openSnapshot = graph.CreateSnapshot(testMap.MapId);
            Assert.That(openSnapshot.FlightEdges, Has.Length.EqualTo(2));

            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(2, 0, 1),
                testMap.Tile.Tile);
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(graph.ValidateSnapshot(openSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged));
                Assert.That(graph.CreateSnapshot(testMap.MapId).FlightEdges, Is.Empty,
                    "A closed Body boundary must remove the corridor.");
            });

            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var shaft = Server.ResolveDependency<IPrototypeManager>().Index(ShaftTile);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(2, 0, 1),
                new Tile(shaft.TileId));
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            Assert.That(graph.CreateSnapshot(testMap.MapId).FlightEdges, Has.Length.EqualTo(2));
            SEntMan.System<SharedTransformSystem>().SetLocalRotation(marker, Angle.FromDegrees(90));
        });
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            Assert.That(graph.CreateSnapshot(testMap.MapId).FlightEdges, Is.Empty,
                "Rotating the authored exit away from supported floor must invalidate both directions.");
            Assert.That(graph.Snapshot().UnsupportedFlightEdges, Is.GreaterThanOrEqualTo(1));
        });
    }

    [Test]
    public async Task NPCSteeringPlansAndExecutesAuthoredFlightCorridor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            ConfigureFlightCorridor(testMap, 2);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SpawnUser(testMap, 0.5f);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(npc);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero)).Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });

        await Pair.RunSeconds(12f);
        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var steeringMetrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            var transform = SEntMan.GetComponent<TransformComponent>(npc);
            var distance = transform.Coordinates.TryDistance(
                SEntMan,
                new EntityCoordinates(target, Vector2.Zero),
                out var value)
                ? value
                : float.PositiveInfinity;
            var state = $"status={steering.Status},z={SEntMan.System<SharedZLevelSystem>().GetZLevel(npc)}," +
                        $"distance={distance:F2},route={steering.ZLevelRoute != null}," +
                        $"leg={steering.ZLevelLegIndex},stage={steering.ZLevelFlightStage}," +
                        $"owned={steering.ZLevelRouteOwnsFlight},replan={steering.LastZLevelReplanReason}," +
                        $"failure={steering.LastZLevelExecutionFailureReason}";

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(npc), Is.EqualTo(1), state);
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.InRange), state);
                Assert.That(distance, Is.LessThanOrEqualTo(steering.Range), state);
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(npc).Active, Is.False, state);
                Assert.That(steering.ZLevelFlightStage, Is.EqualTo(NPCZLevelFlightStage.None), state);
                Assert.That(steeringMetrics.RoutesInstalled, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.RoutesCompleted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.FlightLegsStarted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.FlightLegsCompleted, Is.EqualTo(1), state);
                Assert.That(steeringMetrics.FlightLegsFailed, Is.Zero, state);
                Assert.That(steeringMetrics.TraversalsStarted, Is.Zero, state);
                Assert.That(steeringMetrics.Replans, Is.Zero, state);
                Assert.That(steeringMetrics.ExecutionFailures, Is.Zero, state);
            });
        });
    }

    [Test]
    public async Task NPCSteeringPreservesPreexistingFlightAtDestination()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            ConfigureFlightCorridor(testMap, 2);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SpawnUser(testMap, 2.5f);
            var flight = SEntMan.EnsureComponent<ZLevelFlightComponent>(npc);
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().TryStartFlight(npc, 0, flight.HoverOffset, flight),
                Is.EqualTo(ZLevelFlightResult.Success));
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero)).Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });

        await Pair.RunSeconds(10f);
        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();
            var state = $"status={steering.Status},z={SEntMan.System<SharedZLevelSystem>().GetZLevel(npc)}," +
                        $"stage={steering.ZLevelFlightStage},owned={steering.ZLevelRouteOwnsFlight}," +
                        $"active={flight.Active},target={flight.TargetLocalZLevel}";

            Assert.Multiple(() =>
            {
                Assert.That(steering.Status, Is.EqualTo(SteeringStatus.InRange), state);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(npc), Is.EqualTo(1), state);
                Assert.That(flight.Active, Is.True, state);
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(1), state);
                Assert.That(steering.ZLevelFlightStage, Is.EqualTo(NPCZLevelFlightStage.None), state);
                Assert.That(steering.ZLevelRouteOwnsFlight, Is.False, state);
                Assert.That(metrics.FlightLegsStarted, Is.EqualTo(1), state);
                Assert.That(metrics.FlightLegsCompleted, Is.EqualTo(1), state);
                Assert.That(metrics.FlightLegsFailed, Is.Zero, state);
            });
        });
    }

    [Test]
    public async Task RouteInvalidationReleasesFlightItActivated()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid marker = default;
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            marker = ConfigureFlightCorridor(testMap, 0);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SpawnUser(testMap, 0.5f);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(npc);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            steeringSystem.ResetZLevelMetrics();
            steeringSystem.Register(npc, new EntityCoordinates(target, Vector2.Zero)).Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });

        await PoolManager.WaitUntil(Server, () =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(npc);
            return flight.Active && steering.ZLevelFlightStage != NPCZLevelFlightStage.None;
        }, maxTicks: 120);

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(marker));
        await PoolManager.WaitUntil(Server, () =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(npc);
            return steering.ZLevelRoute == null && !flight.Active;
        }, maxTicks: 120);

        await Server.WaitAssertion(() =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(npc);
            var metrics = SEntMan.System<NPCSteeringSystem>().SnapshotZLevelMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.False);
                Assert.That(steering.ZLevelFlightStage, Is.EqualTo(NPCZLevelFlightStage.None));
                Assert.That(steering.ZLevelRouteOwnsFlight, Is.False);
                Assert.That(steering.LastZLevelReplanReason, Is.EqualTo(NPCZLevelReplanReason.RouteInvalid));
                Assert.That(metrics.FlightLegsStarted, Is.EqualTo(1));
                Assert.That(metrics.FlightLegsCompleted, Is.Zero);
                Assert.That(metrics.FlightLegsFailed, Is.Zero,
                    "Graph invalidation is a route failure, not a flight-command failure.");
                Assert.That(metrics.Replans, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ReplacingRouteReleasesFlightOwnedByPreviousRoute()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid npc = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            ConfigureCorridors(testMap);
            ConfigureFlightCorridor(testMap, 0);
            target = SEntMan.SpawnEntity(null, Coordinates(testMap, 5.5f));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);

            npc = SpawnUser(testMap, 0.5f);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(npc);
        });
        await Pair.RunTicksSync(40);

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<NPCSteeringSystem>()
                .Register(npc, new EntityCoordinates(target, Vector2.Zero))
                .Range = 0.2f;
            SEntMan.EnsureComponent<ActiveNPCComponent>(npc);
        });

        await PoolManager.WaitUntil(Server, () =>
        {
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            return steering.ZLevelFlightStage == NPCZLevelFlightStage.Approach;
        }, maxTicks: 120);

        await Server.WaitAssertion(() => SEntMan.RemoveComponent<ActiveNPCComponent>(npc));
        var replacement = await RequestActorPath(testMap, npc, target);
        Assert.That(replacement.Status, Is.EqualTo(ZLevelPathRouteStatus.Success));
        Assert.That(replacement.Route, Is.Not.Null);

        await Server.WaitAssertion(() =>
        {
            var steeringSystem = SEntMan.System<NPCSteeringSystem>();
            var steering = SEntMan.GetComponent<NPCSteeringComponent>(npc);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(npc);
            var oldRoute = steering.ZLevelRoute;

            Assert.Multiple(() =>
            {
                Assert.That(oldRoute, Is.Not.Null);
                Assert.That(flight.Active, Is.True);
                Assert.That(steering.ZLevelRouteOwnsFlight, Is.True);
                Assert.That(steeringSystem.TryInstallZLevelRoute(npc, replacement.Route!, steering), Is.True);
                Assert.That(steering.ZLevelRoute, Is.SameAs(replacement.Route));
                Assert.That(steering.ZLevelRoute, Is.Not.SameAs(oldRoute));
                Assert.That(flight.Active, Is.False);
                Assert.That(steering.ZLevelFlightStage, Is.EqualTo(NPCZLevelFlightStage.None));
                Assert.That(steering.ZLevelRouteOwnsFlight, Is.False);
            });
        });
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

    private EntityUid ConfigureFlightCorridor(TestMapData testMap, int apertureX)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var shaft = prototypes.Index(ShaftTile);
        map.SetZLevelTile(
            testMap.Grid,
            grid,
            new ZLevelTileIndices(apertureX, 0, 1),
            new Tile(shaft.TileId));

        return SpawnAnchoredZLevelEntity(
            "ZLevelFlightNavigationMarker",
            Coordinates(testMap, apertureX + 0.5f),
            0);
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

    private EntityUid SpawnUser(TestMapData testMap, float x)
    {
        var user = SEntMan.SpawnEntity("MobMouse", Coordinates(testMap, x));
        SEntMan.RemoveComponent<HTNComponent>(user);
        SEntMan.RemoveComponent<ActiveNPCComponent>(user);
        SEntMan.EnsureComponent<GodmodeComponent>(user);
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(user, 0), Is.True);
        return user;
    }

    private async Task<ZLevelPathRouteResult> RequestUntypedPath(TestMapData testMap)
    {
        Task<ZLevelPathRouteResult>? task = null;
        await Server.WaitPost(() =>
        {
            task = SEntMan.System<PathfindingSystem>().GetZLevelPath(
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 0.5f), 0),
                new ZLevelPathEndpoint(testMap.MapId, Coordinates(testMap, 5.5f), 1),
                0.2f,
                0,
                0,
                CancellationToken.None);
        });
        await Pair.RunTicksSync(10);
        return await task!;
    }

    private async Task<ZLevelPathRouteResult> RequestActorPath(
        TestMapData testMap,
        EntityUid actor,
        EntityUid target)
    {
        Task<ZLevelPathRouteResult>? task = null;
        await Server.WaitPost(() =>
        {
            var pathfinding = SEntMan.System<PathfindingSystem>();
            task = pathfinding.GetZLevelPath(
                actor,
                new ZLevelPathEndpoint(
                    testMap.MapId,
                    SEntMan.GetComponent<TransformComponent>(actor).Coordinates,
                    SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(actor)),
                new ZLevelPathEndpoint(
                    testMap.MapId,
                    new EntityCoordinates(target, Vector2.Zero),
                    SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(target)),
                0.2f,
                CancellationToken.None,
                pathfinding.GetFlags(actor));
        });
        await Pair.RunTicksSync(10);
        return await task!;
    }

    private static EntityCoordinates Coordinates(TestMapData testMap, float x)
    {
        return new EntityCoordinates(testMap.Grid, new Vector2(x, 0.5f));
    }
}
