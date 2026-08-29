// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Numerics;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.Power.Components;
using Content.Server.ZLevel.Components;
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
