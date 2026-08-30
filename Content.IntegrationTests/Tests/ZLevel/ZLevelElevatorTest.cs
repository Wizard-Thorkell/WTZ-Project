// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.Power.Components;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.Maps;
using Content.Shared.Power;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelElevatorTest : MovementTest
{
    private static readonly EntProtoId CabinPrototype = "ZLevelElevatorCabin";
    private static readonly EntProtoId StopPrototype = "ZLevelElevatorStop";
    private static readonly EntProtoId PassengerPrototype = "Crowbar";
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";
    private static readonly ProtoId<ContentTileDefinition> SteelTile = "FloorSteel";

    [Test]
    public async Task CabinAndCapturedPassengersMoveAfterAuthoritativeDelay()
    {
        EntityUid cabin = default;
        EntityUid passenger = default;
        EntityUid player = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.FromSeconds(0.2));
            cabin = fixture.Cabin;
            player = ToServer(Player);
            passenger = SEntMan.SpawnEntity(PassengerPrototype, fixture.Coordinates);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(passenger, 0), Is.True);

            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            elevators.ResetMetrics();
            Assert.That(elevators.TryRequestFloor(cabin, 2, player),
                Is.EqualTo(ZLevelElevatorRequestResult.Started));

            var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
            var power = SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin);
            Assert.Multiple(() =>
            {
                Assert.That(component.State, Is.EqualTo(ZLevelElevatorState.Moving));
                Assert.That(component.TargetLevel, Is.EqualTo(2));
                Assert.That(power.Load, Is.EqualTo(component.TravelPowerDraw));
                Assert.That(elevators.ActiveTravelCount, Is.EqualTo(1));
            });
        });

        await RunSeconds(0.2f);
        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.Zero);
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
                Assert.That(zLevels.GetZLevel(passenger), Is.Zero);
            });
        });

        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
            var metrics = elevators.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(player), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(passenger), Is.EqualTo(2));
                Assert.That(component.State, Is.EqualTo(ZLevelElevatorState.Idle));
                Assert.That(component.TargetLevel, Is.Null);
                Assert.That(SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin).Load,
                    Is.EqualTo(component.IdlePowerDraw));
                Assert.That(metrics.Started, Is.EqualTo(1));
                Assert.That(metrics.Completed, Is.EqualTo(1));
                Assert.That(metrics.PassengersCaptured, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.PassengersMoved, Is.EqualTo(metrics.PassengersCaptured));
            });
        });
    }

    [Test]
    public async Task LandingControlCallsAnEmptyCabinAndCannotSpoofAnotherFloor()
    {
        EntityUid cabin = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.FromSeconds(0.1),
                offset: new Vector2(1f, 0f));
            cabin = fixture.Cabin;
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            elevators.ResetMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(elevators.TryRequestFloor(fixture.TopStop, 1),
                    Is.EqualTo(ZLevelElevatorRequestResult.InvalidTarget));
                Assert.That(elevators.TryRequestFloor(fixture.TopStop, 2, ToServer(Player)),
                    Is.EqualTo(ZLevelElevatorRequestResult.InvalidUser));
                Assert.That(elevators.TryRequestFloor(fixture.TopStop, 2),
                    Is.EqualTo(ZLevelElevatorRequestResult.Started));
            });
        });

        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(cabin), Is.EqualTo(2));
                Assert.That(SEntMan.System<ZLevelElevatorSystem>().Snapshot().PassengersCaptured, Is.Zero);
            });
        });
    }

    [Test]
    public async Task PowerLossCancelsTravelAtSourceAndRestoresIdleLoad()
    {
        EntityUid cabin = default;
        EntityUid player = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.FromSeconds(0.5));
            cabin = fixture.Cabin;
            player = ToServer(Player);
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            elevators.ResetMetrics();
            Assert.That(elevators.TryRequestFloor(cabin, 2), Is.EqualTo(ZLevelElevatorRequestResult.Started));
        });

        await RunSeconds(0.1f);
        await Server.WaitPost(() => SetPower(cabin, false));
        await RunSeconds(1.1f);

        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.Zero);
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
                Assert.That(component.State, Is.EqualTo(ZLevelElevatorState.Idle));
                Assert.That(component.TargetLevel, Is.Null);
                Assert.That(SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin).Load,
                    Is.EqualTo(component.IdlePowerDraw));
                Assert.That(elevators.ActiveTravelCount, Is.Zero);
                Assert.That(elevators.Snapshot().Cancelled, Is.EqualTo(1));
                Assert.That(elevators.TryRequestFloor(cabin, 2),
                    Is.EqualTo(ZLevelElevatorRequestResult.Unpowered));
            });
        });
    }

    [Test]
    public async Task DuplicateStopsAndClosedShaftFailClosed()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.Zero,
                closedFloor: 1);
            var duplicate = SpawnAnchored(StopPrototype, fixture.Coordinates, 2);
            var elevators = SEntMan.System<ZLevelElevatorSystem>();

            Assert.That(elevators.TryRequestFloor(fixture.Cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.DuplicateStop));

            SEntMan.DeleteEntity(duplicate);
            Assert.That(elevators.TryRequestFloor(fixture.Cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.ClosedShaft));
            Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(fixture.Cabin), Is.Zero);
        });
    }

    [Test]
    public async Task InvalidConfigurationAndPassengerCapacityFailClosed()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.Zero);
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(fixture.Cabin);

            component.MaxTravelLevels = 0;
            Assert.That(elevators.TryRequestFloor(fixture.Cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.InvalidConfiguration));

            component.MaxTravelLevels = 16;
            component.PassengerLimit = 1;
            var passenger = SEntMan.SpawnEntity(PassengerPrototype, fixture.Coordinates);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(passenger, 0), Is.True);
            Assert.That(elevators.TryRequestFloor(fixture.Cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.OverCapacity));
            Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(fixture.Cabin), Is.Zero);
        });
    }

    [Test]
    public async Task PassengerWhoLeavesBeforeArrivalStaysOnSourceFloor()
    {
        EntityUid cabin = default;
        EntityUid player = default;
        EntityCoordinates offCabin = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.FromSeconds(0.2));
            cabin = fixture.Cabin;
            player = ToServer(Player);
            offCabin = fixture.Coordinates.Offset(Vector2.UnitX);
            Assert.That(SEntMan.System<ZLevelElevatorSystem>().TryRequestFloor(cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.Started));

            SEntMan.System<SharedTransformSystem>().SetCoordinates(player, offCabin);
        });

        await RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
            });
        });
    }

    [Test]
    public async Task PhysicalStopsExposeDeterministicSupportedNavigationEdges()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.Zero);
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var map = SEntMan.System<SharedMapSystem>();
            var tile = map.TileIndicesFor(MapData.Grid, grid, fixture.Coordinates);
            var edges = graph.CreateSnapshot(MapData.MapId).Edges
                .Where(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(edges, Has.Length.EqualTo(2));
                Assert.That(edges[0].Source.Traversal, Is.EqualTo(fixture.BottomStop));
                Assert.That(edges[0].Source.LocalZ, Is.Zero);
                Assert.That(edges[0].Destination.LocalZ, Is.EqualTo(2));
                Assert.That(edges[0].ZOffset, Is.EqualTo(2));
                Assert.That(edges[0].Cost, Is.EqualTo(12f));
                Assert.That(edges[0].RequireDirectDestinationSupport, Is.True);
                Assert.That(edges[1].Source.Traversal, Is.EqualTo(fixture.TopStop));
                Assert.That(edges[1].ZOffset, Is.EqualTo(-2));
                Assert.That(boundaries.CanBodyPass(MapData.Grid, grid, tile, 0, -1), Is.False);
                Assert.That(boundaries.CanBodyPass(MapData.Grid, grid, tile, 2, 1), Is.False);
            });

            SEntMan.GetComponent<ZLevelElevatorCabinComponent>(fixture.Cabin).NavigationCallCost = float.NaN;
            SetPower(fixture.Cabin, false);
            SetPower(fixture.Cabin, true);
            Assert.That(graph.CreateSnapshot(MapData.MapId).Edges.Any(edge =>
                edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.False,
                "Malformed route costs must fail closed instead of entering pathfinding.");
        });
    }

    [Test]
    public async Task ThreeStopNetworkUsesOnlyAdjacentEdgesAndResolvesBothMiddleDirections()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.Zero);
            var middleStop = SpawnAnchored(StopPrototype, fixture.Coordinates, 1);
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var edges = graph.CreateSnapshot(MapData.MapId).Edges
                .Where(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(edges.Select(edge => (edge.Source.LocalZ, edge.Destination.LocalZ)),
                    Is.EqualTo(new[] { (0, 1), (1, 0), (1, 2), (2, 1) }));
                Assert.That(edges.Any(edge =>
                    edge.Source.LocalZ == 0 && edge.Destination.LocalZ == 2), Is.False);
                Assert.That(edges.Count(edge => edge.Source.Traversal == middleStop), Is.EqualTo(2));
                Assert.That(edges.All(edge => edge.Cost == 8f), Is.True);
            });

            foreach (var expected in edges.Where(edge => edge.Source.Traversal == middleStop))
            {
                Assert.That(graph.TryResolveEdge(expected, out var current),
                    Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
                Assert.That(ZLevelTraversalGraphSystem.HasEquivalentEdge(expected, current), Is.True);
            }
        });
    }

    [Test]
    public async Task NavigationCallsCabinThenCarriesWaitingUserWithZeroDuration()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.Zero);
            var player = ToServer(Player);
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();

            transform.SetCoordinates(player, fixture.Coordinates.Offset(Vector2.UnitX));
            Assert.That(elevators.TryRequestFloor(fixture.Cabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.Started));
            Assert.That(zLevels.GetZLevel(fixture.Cabin), Is.EqualTo(2));

            transform.SetCoordinates(player, fixture.Coordinates);
            Assert.That(zLevels.SetZLevelPosition(player, 0), Is.True);
            var edge = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(MapData.MapId)
                .Edges
                .Single(candidate =>
                    candidate.Source.Traversal == fixture.BottomStop &&
                    candidate.Destination.LocalZ == 2);

            elevators.ResetMetrics();
            var traversal = SEntMan.System<ZLevelTraversalSystem>();
            Assert.That(traversal.TryStartTraversal(edge, player), Is.True);
            var navigation = elevators.NavigationSnapshot();
            var physical = elevators.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(fixture.Cabin), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(player), Is.EqualTo(2));
                Assert.That(traversal.IsTraversalPending(player), Is.False);
                Assert.That(navigation.Active, Is.Zero);
                Assert.That(navigation.Started, Is.EqualTo(1));
                Assert.That(navigation.Completed, Is.EqualTo(1));
                Assert.That(navigation.Cancelled, Is.Zero);
                Assert.That(physical.Started, Is.EqualTo(2));
                Assert.That(physical.Completed, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task PowerChangesInvalidateOnlyElevatorEdgeEnvironment()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(topFloor: 2, travelTimePerLevel: TimeSpan.Zero);
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var snapshot = graph.CreateSnapshot(MapData.MapId);
            Assert.That(snapshot.Edges.Count(edge =>
                edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.EqualTo(2));

            SetPower(fixture.Cabin, false);
            Assert.That(graph.ValidateSnapshot(snapshot),
                Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged));
            Assert.That(graph.CreateSnapshot(MapData.MapId).Edges.Any(edge =>
                edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.False);

            SetPower(fixture.Cabin, true);
            Assert.That(graph.CreateSnapshot(MapData.MapId).Edges.Count(edge =>
                edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task PowerLossWhileCallingCancelsNavigationAndLeavesUserAtSource()
    {
        EntityUid cabin = default;
        EntityUid player = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.FromSeconds(0.25));
            cabin = fixture.Cabin;
            player = ToServer(Player);
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var traversal = SEntMan.System<ZLevelTraversalSystem>();

            Assert.That(zLevels.SetZLevelPosition(cabin, 2), Is.True);
            var edge = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(MapData.MapId)
                .Edges
                .Single(candidate =>
                    candidate.Source.Traversal == fixture.BottomStop &&
                    candidate.Destination.LocalZ == 2);

            elevators.ResetMetrics();
            Assert.That(traversal.TryStartTraversal(edge, player), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
                Assert.That(traversal.IsTraversalPending(player, fixture.BottomStop), Is.True);
                Assert.That(elevators.IsTravelPending(cabin), Is.True);
            });
        });

        await RunSeconds(0.1f);
        await Server.WaitPost(() => SetPower(cabin, false));
        await RunSeconds(0.1f);

        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var navigation = elevators.NavigationSnapshot();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.EqualTo(2));
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
                Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(player), Is.False);
                Assert.That(elevators.IsTravelPending(cabin), Is.False);
                Assert.That(navigation.Active, Is.Zero);
                Assert.That(navigation.Cancelled, Is.EqualTo(1));
                Assert.That(elevators.Snapshot().Cancelled, Is.EqualTo(1));
                Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>()
                    .CreateSnapshot(MapData.MapId)
                    .Edges.Any(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.False);
            });
        });
    }

    [Test]
    public async Task RemovingDestinationStopCancelsRidingNavigation()
    {
        EntityUid cabin = default;
        EntityUid player = default;
        EntityUid topStop = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.FromSeconds(0.25));
            cabin = fixture.Cabin;
            player = ToServer(Player);
            topStop = fixture.TopStop;
            var edge = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(MapData.MapId)
                .Edges
                .Single(candidate =>
                    candidate.Source.Traversal == fixture.BottomStop &&
                    candidate.Destination.LocalZ == 2);
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            elevators.ResetMetrics();

            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(edge, player), Is.True);
            Assert.That(elevators.IsTravelPending(cabin), Is.True);
        });

        await RunSeconds(0.1f);
        await Server.WaitPost(() => SEntMan.DeleteEntity(topStop));
        await RunSeconds(0.1f);

        await Server.WaitAssertion(() =>
        {
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(cabin), Is.Zero);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(player), Is.Zero);
                Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(player), Is.False);
                Assert.That(elevators.IsTravelPending(cabin), Is.False);
                Assert.That(elevators.NavigationSnapshot().Cancelled, Is.EqualTo(1));
                Assert.That(elevators.Snapshot().Cancelled, Is.EqualTo(1));
                Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>()
                    .CreateSnapshot(MapData.MapId)
                    .Edges.Any(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.False);
            });
        });
    }

    [Test]
    public async Task NavigationHasOneOwnerAndCancellationDoesNotAbortTheCabinCall()
    {
        EntityUid cabin = default;
        EntityUid player = default;
        EntityUid waitingPassenger = default;
        EntityUid bottomStop = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.FromSeconds(0.2));
            cabin = fixture.Cabin;
            bottomStop = fixture.BottomStop;
            player = ToServer(Player);
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var traversal = SEntMan.System<ZLevelTraversalSystem>();

            Assert.That(zLevels.SetZLevelPosition(cabin, 2), Is.True);
            var edge = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(MapData.MapId)
                .Edges
                .Single(candidate =>
                    candidate.Source.Traversal == bottomStop &&
                    candidate.Destination.LocalZ == 2);

            elevators.ResetMetrics();
            Assert.That(traversal.TryStartTraversal(edge, player), Is.True);
            waitingPassenger = SEntMan.SpawnEntity(PassengerPrototype, fixture.Coordinates);
            Assert.That(zLevels.SetZLevelPosition(waitingPassenger, 0), Is.True);
            Assert.That(traversal.TryStartTraversal(edge, waitingPassenger), Is.False,
                "Only one route may own a physical cabin while it is busy.");
            Assert.That(traversal.TryCancelTraversal(player, bottomStop), Is.True);

            var navigation = elevators.NavigationSnapshot();
            Assert.Multiple(() =>
            {
                Assert.That(traversal.IsTraversalPending(player), Is.False);
                Assert.That(traversal.IsTraversalPending(waitingPassenger), Is.False);
                Assert.That(elevators.IsTravelPending(cabin), Is.True,
                    "Cancelling a route should release ownership without teleporting or aborting the cabin.");
                Assert.That(navigation.Active, Is.Zero);
                Assert.That(navigation.Started, Is.EqualTo(1));
                Assert.That(navigation.Cancelled, Is.EqualTo(1));
                Assert.That(navigation.Rejected, Is.EqualTo(1));
            });
        });

        await RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(cabin), Is.Zero);
                Assert.That(zLevels.GetZLevel(player), Is.Zero);
                Assert.That(zLevels.GetZLevel(waitingPassenger), Is.Zero);
                Assert.That(elevators.IsTravelPending(cabin), Is.False);
                Assert.That(elevators.NavigationSnapshot().Completed, Is.Zero);
                Assert.That(elevators.Snapshot().Completed, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task DeletingNavigationOwnerReleasesCabinWithoutASecondTrip()
    {
        EntityUid cabin = default;
        EntityUid user = default;

        await Server.WaitAssertion(() =>
        {
            var fixture = SpawnElevator(
                topFloor: 2,
                travelTimePerLevel: TimeSpan.FromSeconds(0.2));
            cabin = fixture.Cabin;
            user = SEntMan.SpawnEntity(PassengerPrototype, fixture.Coordinates);
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var elevators = SEntMan.System<ZLevelElevatorSystem>();

            Assert.That(zLevels.SetZLevelPosition(user, 0), Is.True);
            Assert.That(zLevels.SetZLevelPosition(cabin, 2), Is.True);
            var edge = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(MapData.MapId)
                .Edges
                .Single(candidate =>
                    candidate.Source.Traversal == fixture.BottomStop &&
                    candidate.Destination.LocalZ == 2);

            elevators.ResetMetrics();
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(edge, user), Is.True);
            Assert.That(elevators.IsTravelPending(cabin), Is.True);
            SEntMan.DeleteEntity(user);
        });

        await RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            var navigation = elevators.NavigationSnapshot();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.Deleted(user), Is.True);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(cabin), Is.Zero);
                Assert.That(elevators.IsTravelPending(cabin), Is.False);
                Assert.That(navigation.Active, Is.Zero);
                Assert.That(navigation.Started, Is.EqualTo(1));
                Assert.That(navigation.Completed, Is.Zero);
                Assert.That(navigation.Cancelled, Is.EqualTo(1));
                Assert.That(elevators.Snapshot().Started, Is.EqualTo(1),
                    "Deleting the route owner must not schedule a destination ride.");
                Assert.That(elevators.Snapshot().Completed, Is.EqualTo(1));
            });
        });
    }

    private ElevatorFixture SpawnElevator(
        int topFloor,
        TimeSpan travelTimePerLevel,
        Vector2? offset = null,
        int? closedFloor = null)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var format = SEntMan.System<SharedZLevelMapSystem>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        var coordinates = Transform
            .WithEntityId(SEntMan.GetCoordinates(PlayerCoords), MapData.Grid)
            .Offset(offset ?? Vector2.Zero);
        var tile = map.TileIndicesFor(MapData.Grid, grid, coordinates);
        var shaft = prototypes.Index(ShaftTile);
        var steel = prototypes.Index(SteelTile);

        grid.CanSplit = false;
        format.Configure(
            MapData.MapUid,
            0,
            topFloor,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        for (var z = 0; z <= topFloor; z++)
        {
            var tileDefinition = z == closedFloor ? steel : shaft;
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, z),
                new Tile(tileDefinition.TileId));
        }

        var bottomStop = SpawnAnchored(StopPrototype, coordinates, 0);
        var topStop = SpawnAnchored(StopPrototype, coordinates, topFloor);
        var cabin = SpawnAnchored(CabinPrototype, coordinates, 0);
        var component = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
        component.TravelTimePerLevel = travelTimePerLevel;
        SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin).NeedsPower = false;
        SetPower(cabin, true);
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(ToServer(Player), 0), Is.True);
        return new ElevatorFixture(cabin, bottomStop, topStop, coordinates);
    }

    private EntityUid SpawnAnchored(EntProtoId prototype, EntityCoordinates coordinates, int z)
    {
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var uid = SEntMan.SpawnEntity(prototype, coordinates);
        var xform = SEntMan.GetComponent<TransformComponent>(uid);
        Assert.That(zLevels.SetZLevelPosition(uid, z), Is.True);
        if (!xform.Anchored)
            transform.AnchorEntity(uid, xform);
        Assert.That(xform.Anchored, Is.True);
        return uid;
    }

    private void SetPower(EntityUid cabin, bool powered)
    {
        var power = SEntMan.GetComponent<ApcPowerReceiverComponent>(cabin);
        power.PowerDisabled = !powered;
        power.Powered = powered;
        var powerChanged = new PowerChangedEvent(powered, powered ? power.Load : 0f);
        SEntMan.EventBus.RaiseLocalEvent(cabin, ref powerChanged);
    }

    private readonly record struct ElevatorFixture(
        EntityUid Cabin,
        EntityUid BottomStop,
        EntityUid TopStop,
        EntityCoordinates Coordinates);
}
