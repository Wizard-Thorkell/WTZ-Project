// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using Content.IntegrationTests.Tests.Movement;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Power.Components;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelFlightTest : MovementTest
{
    [TestPrototypes]
    private const string FlightPrototypes = @"
- type: entity
  id: ZLevelFlightGravityGenerator
  components:
  - type: GravityGenerator
  - type: PowerCharge
    windowTitle: gravity-generator-window-title
    idlePower: 50
    chargeRate: 1000000000
    activePower: 500
  - type: ApcPowerReceiver
  - type: UserInterface

- type: entity
  id: ZLevelFlightInvalidConfiguration
  components:
  - type: Physics
    bodyType: Dynamic
  - type: ZLevelFlight
    verticalAcceleration: 0
";

    [Test]
    public async Task ActiveFlightHoversUnderArtificialGravityAndReplicates()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            SpawnPoweredGravityGenerator();

            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.That(
                SEntMan.System<SharedZLevelSystem>().TryStartFlight(player),
                Is.EqualTo(ZLevelFlightResult.Success));
        });

        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            var physics = SEntMan.GetComponent<PhysicsComponent>(player);
            var gravity = SEntMan.GetComponent<GravityAffectedComponent>(player);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.True);
                Assert.That(flight.TargetLocalZLevel, Is.Zero);
                Assert.That(flight.TargetLocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(position.ZLevel, Is.Zero);
                Assert.That(position.LocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(kinematics.VerticalVelocity, Is.Zero.Within(0.001f));
                Assert.That(kinematics.Grounded, Is.False);
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.InAir));
                Assert.That(gravity.Weightless, Is.True);
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
                Assert.That(metrics.FlightStarts, Is.EqualTo(1));
                Assert.That(metrics.FlightUpdates, Is.GreaterThan(0));
            });
        });

        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var flight = CEntMan.GetComponent<ZLevelFlightComponent>(ToClient(Player));
            var position = CEntMan.GetComponent<ZLevelPositionComponent>(ToClient(Player));
            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.True);
                Assert.That(flight.TargetLocalZLevel, Is.Zero);
                Assert.That(flight.TargetLocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(position.LocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
            });
        });
    }

    [Test]
    public async Task FlightCrossesOpenBoundariesAndKeepsLocalTargetWhenFrameMoves()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            var system = SEntMan.System<SharedZLevelSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.That(system.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            Assert.That(system.TrySetFlightTarget(player, 2), Is.EqualTo(ZLevelFlightResult.Success));
        });

        await RunSeconds(2.2f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var system = SEntMan.System<SharedZLevelSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(player);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(2));
                Assert.That(position.LocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(2));
                Assert.That(metrics.FlightTargetChanges, Is.EqualTo(1));
                Assert.That(metrics.FlightBoundaryCrossings, Is.EqualTo(2));
            });

            Assert.That(transform.SetZLevelFrameOrigin(MapData.Grid, 4), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(2));
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(2));
                Assert.That(system.GetWorldZLevel(player), Is.EqualTo(6));
            });
        });

        await RunTicks(5);
        await Client.WaitAssertion(() =>
        {
            var player = ToClient(Player);
            var flight = CEntMan.GetComponent<ZLevelFlightComponent>(player);
            var frame = CEntMan.GetComponent<ZLevelFrameComponent>(MapData.CGridUid);
            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.True);
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(2));
                Assert.That(frame.Origin, Is.EqualTo(4));
            });
        });
    }

    [Test]
    public async Task ClosedBoundaryStopsOnceAndRetargetsToContactHeight()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var tile = map.TileIndicesFor(MapData.Grid, grid, SEntMan.GetCoordinates(PlayerCoords));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 1),
                new Tile(1));

            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            var system = SEntMan.System<SharedZLevelSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            Assert.That(system.TryStartFlight(player, 2), Is.EqualTo(ZLevelFlightResult.Success));
        });

        await RunSeconds(1.2f);

        long blocked = 0;
        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(player);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            blocked = metrics.FlightBoundaryBlocks;

            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.True);
                Assert.That(position.ZLevel, Is.Zero);
                Assert.That(position.LocalZOffset, Is.GreaterThan(0.99f));
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(position.ZLevel));
                Assert.That(flight.TargetLocalZOffset, Is.EqualTo(position.LocalZOffset).Within(0.0001f));
                Assert.That(kinematics.VerticalVelocity, Is.Zero.Within(0.001f));
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
                Assert.That(blocked, Is.EqualTo(1));
            });
        });

        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().FlightBoundaryBlocks,
                Is.EqualTo(blocked),
                "A blocked settled flyer must not retry the same closed boundary every tick.");
        });
    }

    [Test]
    public async Task StoppingFlightRestoresArtificialGravity()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            SpawnPoweredGravityGenerator();
        });
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            var system = SEntMan.System<SharedZLevelSystem>();
            Assert.That(system.TryStartFlight(player, 1), Is.EqualTo(ZLevelFlightResult.Success));
        });
        await RunSeconds(1.7f);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(1));
                Assert.That(position.LocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
            });
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().TryStopFlight(player),
                Is.EqualTo(ZLevelFlightResult.Success));
        });

        await RunSeconds(1.2f);
        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            var gravity = SEntMan.GetComponent<GravityAffectedComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.False);
                Assert.That(position.ZLevel, Is.Zero);
                Assert.That(position.LocalZOffset, Is.Zero.Within(0.001f));
                Assert.That(kinematics.Grounded, Is.True);
                Assert.That(gravity.Weightless, Is.False);
            });
        });
    }

    [Test]
    public async Task ManagedGravityRespectsExternalWeightlessOverride()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            SpawnPoweredGravityGenerator();
        });
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            var system = SEntMan.System<SharedZLevelSystem>();
            Assert.That(system.SetZLevel(player, 1), Is.True);
            SEntMan.EnsureComponent<MovementIgnoreGravityComponent>(player).Weightless = true;
            SEntMan.System<SharedGravitySystem>().RefreshWeightless(player);
        });

        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<GravityAffectedComponent>(player).Weightless, Is.True);
                Assert.That(position.ZLevel, Is.EqualTo(1));
                Assert.That(position.LocalZOffset, Is.Zero.Within(0.001f));
            });
        });
    }

    [Test]
    public async Task FlightApiRejectsInvalidStateAndMapChangesInvalidateActiveTarget()
    {
        await Server.WaitAssertion(() =>
        {
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            var entity = SEntMan.SpawnEntity(null, coordinates);
            var physics = SEntMan.EnsureComponent<PhysicsComponent>(entity);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(entity, BodyType.Dynamic, body: physics);
            var system = SEntMan.System<SharedZLevelSystem>();

            Assert.That(system.TryStartFlight(entity), Is.EqualTo(ZLevelFlightResult.MissingCapability));
            var flight = SEntMan.EnsureComponent<ZLevelFlightComponent>(entity);
            Assert.That(system.TryStartFlight(entity), Is.EqualTo(ZLevelFlightResult.UnconfiguredMap));

            ConfigureMap();
            var cancelled = SEntMan.SpawnEntity(null, coordinates);
            var cancelledPhysics = SEntMan.EnsureComponent<PhysicsComponent>(cancelled);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(
                cancelled,
                BodyType.Dynamic,
                body: cancelledPhysics);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(cancelled);
            SEntMan.EnsureComponent<TestListenerComponent>(cancelled);
            SEntMan.System<ZLevelFlightAttemptTestSystem>().CancelNext = true;
            Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(cancelled), Is.False);
            Assert.That(system.TryStartFlight(cancelled), Is.EqualTo(ZLevelFlightResult.Cancelled));
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(cancelled), Is.False,
                    "A cancelled start attempt must not materialize vertical runtime state.");
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(cancelled).Active, Is.False);
            });

            var invalid = SEntMan.SpawnEntity("ZLevelFlightInvalidConfiguration", coordinates);
            Assert.That(system.TryStartFlight(invalid), Is.EqualTo(ZLevelFlightResult.InvalidConfiguration));
            Assert.That(system.SetZLevelPosition(entity, 4), Is.True);
            Assert.That(system.TryStartFlight(entity), Is.EqualTo(ZLevelFlightResult.InvalidCurrentPosition));
            Assert.That(system.SetZLevelPosition(entity, 0), Is.True);
            Assert.That(system.TryStartFlight(entity, 99), Is.EqualTo(ZLevelFlightResult.InvalidTarget));
            Assert.That(system.TryStartFlight(entity, 2), Is.EqualTo(ZLevelFlightResult.Success));
            Assert.That(system.TryStartFlight(entity), Is.EqualTo(ZLevelFlightResult.AlreadyActive));

            SEntMan.System<SharedZLevelMapSystem>().Configure(
                MapData.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);

            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.False);
                Assert.That(system.TrySetFlightTarget(entity, 1), Is.EqualTo(ZLevelFlightResult.Inactive));
                Assert.That(system.TryStopFlight(entity), Is.EqualTo(ZLevelFlightResult.Inactive));
                Assert.That(
                    SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().FlightInvalidations,
                    Is.GreaterThanOrEqualTo(1));
            });
        });
    }

    [Test]
    public async Task HoverOffsetDoesNotChangeDiscreteCollisionFloor()
    {
        await Server.WaitPost(() =>
        {
            ConfigureMap();
            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().TryStartFlight(player),
                Is.EqualTo(ZLevelFlightResult.Success));
        });
        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var other = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(PlayerCoords));
            var playerBody = SEntMan.GetComponent<PhysicsComponent>(player);
            var otherBody = SEntMan.EnsureComponent<PhysicsComponent>(other);
            var fixture = new Fixture();
            var system = SEntMan.System<SharedZLevelSystem>();

            Assert.That(system.SetZLevelPosition(other, 0), Is.True);
            var sameFloor = new PreventCollideEvent(
                player,
                other,
                playerBody,
                otherBody,
                fixture,
                fixture);
            SEntMan.EventBus.RaiseLocalEvent(player, ref sameFloor);

            Assert.That(system.SetZLevelPosition(other, 1), Is.True);
            var differentFloor = new PreventCollideEvent(
                player,
                other,
                playerBody,
                otherBody,
                fixture,
                fixture);
            SEntMan.EventBus.RaiseLocalEvent(player, ref differentFloor);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).LocalZOffset,
                    Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(sameFloor.Cancelled, Is.False);
                Assert.That(differentFloor.Cancelled, Is.True);
            });
        });
    }

    [Test]
    public async Task ContainedAndAnchoredEntitiesCannotStartFlight()
    {
        await Server.WaitAssertion(() =>
        {
            ConfigureMap();
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            var system = SEntMan.System<SharedZLevelSystem>();
            var physicsSystem = SEntMan.System<SharedPhysicsSystem>();

            var anchored = SEntMan.SpawnEntity(null, coordinates);
            var anchoredPhysics = SEntMan.EnsureComponent<PhysicsComponent>(anchored);
            physicsSystem.SetBodyType(anchored, BodyType.Dynamic, body: anchoredPhysics);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(anchored);
            Assert.That(SEntMan.System<SharedTransformSystem>().AnchorEntity(anchored), Is.True);
            Assert.That(system.TryStartFlight(anchored), Is.EqualTo(ZLevelFlightResult.Anchored));

            var holder = SEntMan.SpawnEntity(null, coordinates);
            var contained = SEntMan.SpawnEntity(null, coordinates);
            var containedPhysics = SEntMan.EnsureComponent<PhysicsComponent>(contained);
            physicsSystem.SetBodyType(contained, BodyType.Dynamic, body: containedPhysics);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(contained);
            var container = SEntMan.System<SharedContainerSystem>()
                .EnsureContainer<Container>(holder, "zlevel-flight-test");
            Assert.That(SEntMan.System<SharedContainerSystem>().Insert(contained, container), Is.True);
            Assert.That(system.TryStartFlight(contained), Is.EqualTo(ZLevelFlightResult.Contained));
        });
    }

    private void ConfigureMap(int minimum = 0, int maximum = 3)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            MapData.MapUid,
            minimum,
            maximum,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
    }

    private EntityUid SpawnPoweredGravityGenerator()
    {
        var generator = SEntMan.SpawnEntity(
            "ZLevelFlightGravityGenerator",
            SEntMan.GetCoordinates(PlayerCoords));
        SEntMan.GetComponent<ApcPowerReceiverComponent>(generator).NeedsPower = false;
        return generator;
    }
}

public sealed class ZLevelFlightAttemptTestSystem : TestListenerSystem<ZLevelFlightStartAttemptEvent>
{
    public bool CancelNext;

    protected override void OnDirectedEvent(Entity<TestListenerComponent> ent, ref ZLevelFlightStartAttemptEvent args)
    {
        base.OnDirectedEvent(ent, ref args);
        if (!CancelNext)
            return;

        CancelNext = false;
        args.Cancelled = true;
    }
}
