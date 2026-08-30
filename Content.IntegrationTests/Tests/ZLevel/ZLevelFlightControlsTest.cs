// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.Movement.Systems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelFlightControlsTest : MovementTest
{
    private static readonly EntProtoId BatPrototype = "MobBat";
    private static readonly EntProtoId DragonPrototype = "MobDragon";

    [TestPrototypes]
    private const string FlightControlsPrototypes = @"
- type: entity
  id: ZLevelFlightTestStrap
  components:
  - type: Strap
";

    [Test]
    public async Task ControlsFollowMapConfigurationAndDriveFlight()
    {
        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            var controls = SEntMan.EnsureComponent<ZLevelFlightControlsComponent>(player);

            Assert.Multiple(() =>
            {
                Assert.That(controls.ToggleActionEntity, Is.Null);
                Assert.That(controls.MoveUpActionEntity, Is.Null);
                Assert.That(controls.MoveDownActionEntity, Is.Null);
            });
        });

        await Server.WaitPost(ConfigureMap);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var controls = SEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            var actions = SEntMan.System<SharedActionsSystem>();

            AssertFlightActions(controls, actions);
            actions.PerformAction(player, actions.GetAction(controls.ToggleActionEntity)!.Value);
            actions.PerformAction(player, actions.GetAction(controls.MoveUpActionEntity)!.Value);

            var flight = SEntMan.GetComponent<ZLevelFlightComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(flight.Active, Is.True);
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(1));
                Assert.That(SEntMan.GetComponent<ActionComponent>(controls.ToggleActionEntity!.Value).Toggled, Is.True);
            });
        });

        await RunSeconds(1.2f);
        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var controls = SEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(1));
                Assert.That(position.LocalZOffset, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(controls.ToggleActionEntity, Is.Not.Null);
            });
        });

        await Client.WaitAssertion(() =>
        {
            var player = ToClient(Player);
            var controls = CEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            var flight = CEntMan.GetComponent<ZLevelFlightComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(controls.ToggleActionEntity, Is.Not.Null);
                Assert.That(controls.MoveUpActionEntity, Is.Not.Null);
                Assert.That(controls.MoveDownActionEntity, Is.Not.Null);
                Assert.That(flight.Active, Is.True);
                Assert.That(flight.TargetLocalZLevel, Is.EqualTo(1));
                Assert.That(CEntMan.GetComponent<ActionComponent>(controls.ToggleActionEntity!.Value).Toggled, Is.True);
            });
        });

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            var controls = SEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            var actions = SEntMan.System<SharedActionsSystem>();
            actions.PerformAction(player, actions.GetAction(controls.MoveDownActionEntity)!.Value);
        });
        await RunSeconds(1.2f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var controls = SEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            var actions = SEntMan.System<SharedActionsSystem>();
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            Assert.That(position.ZLevel, Is.Zero);

            actions.PerformAction(player, actions.GetAction(controls.ToggleActionEntity)!.Value);
            Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.False);

            SEntMan.RemoveComponent<ZLevelMapComponent>(MapData.MapUid);
            Assert.Multiple(() =>
            {
                Assert.That(controls.ToggleActionEntity, Is.Null);
                Assert.That(controls.MoveUpActionEntity, Is.Null);
                Assert.That(controls.MoveDownActionEntity, Is.Null);
            });
        });
    }

    [Test]
    public async Task JetpackGrantsOwnedFlightAndPreservesIntrinsicActiveFlight()
    {
        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var jetpack = SEntMan.SpawnEntity("JetpackBlueFilled", SEntMan.GetCoordinates(PlayerCoords));
            var jetpackComponent = SEntMan.GetComponent<JetpackComponent>(jetpack);
            var jetpackSystem = SEntMan.System<JetpackSystem>();

            jetpackSystem.SetEnabled(jetpack, jetpackComponent, true, player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveJetpackComponent>(jetpack), Is.False,
                    "Direct activation must obey gravity policy on an ordinary grid.");
                Assert.That(SEntMan.HasComponent<JetpackUserComponent>(player), Is.False);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(player).BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            });

            ConfigureMap();
            jetpackSystem.SetEnabled(jetpack, jetpackComponent, true, player);

            var user = SEntMan.GetComponent<JetpackUserComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveJetpackComponent>(jetpack), Is.True);
                Assert.That(user.StartedZLevelFlight, Is.True);
                Assert.That(user.GrantedZLevelFlight, Is.True);
                Assert.That(user.GrantedZLevelFlightControls, Is.True);
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.True);
                Assert.That(SEntMan.HasComponent<ZLevelFlightControlsComponent>(player), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(player).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            });

            jetpackSystem.SetEnabled(jetpack, jetpackComponent, false, player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveJetpackComponent>(jetpack), Is.False);
                Assert.That(SEntMan.HasComponent<JetpackUserComponent>(player), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelFlightComponent>(player), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelFlightControlsComponent>(player), Is.False);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(player).BodyStatus, Is.EqualTo(BodyStatus.OnGround));
            });

            var intrinsicFlight = SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            SEntMan.EnsureComponent<ZLevelFlightControlsComponent>(player);
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().TryStartFlight(player, flight: intrinsicFlight),
                Is.EqualTo(ZLevelFlightResult.Success));

            jetpackSystem.SetEnabled(jetpack, jetpackComponent, true, player);
            user = SEntMan.GetComponent<JetpackUserComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(user.StartedZLevelFlight, Is.False);
                Assert.That(user.GrantedZLevelFlight, Is.False);
                Assert.That(user.GrantedZLevelFlightControls, Is.False);
            });

            jetpackSystem.SetEnabled(jetpack, jetpackComponent, false, player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.True);
                Assert.That(SEntMan.HasComponent<ZLevelFlightControlsComponent>(player), Is.True);
                Assert.That(SEntMan.GetComponent<PhysicsComponent>(player).BodyStatus, Is.EqualTo(BodyStatus.InAir));
            });

            jetpackSystem.SetEnabled(jetpack, jetpackComponent, true, player);
            Assert.That(SEntMan.HasComponent<ActiveJetpackComponent>(jetpack), Is.True);
            SEntMan.RemoveComponent<ZLevelMapComponent>(MapData.MapUid);

            var remainingControls = SEntMan.GetComponent<ZLevelFlightControlsComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveJetpackComponent>(jetpack), Is.False);
                Assert.That(SEntMan.HasComponent<JetpackUserComponent>(player), Is.False);
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.False);
                Assert.That(remainingControls.ToggleActionEntity, Is.Null);
                Assert.That(remainingControls.MoveUpActionEntity, Is.Null);
                Assert.That(remainingControls.MoveDownActionEntity, Is.Null);
            });
        });
    }

    [Test]
    public async Task IncapacitationAndForcedMovementInterruptFlightWithTypedReasons()
    {
        await Server.WaitAssertion(() =>
        {
            ConfigureMap();
            var player = ToServer(Player);
            SEntMan.EnsureComponent<ZLevelFlightComponent>(player);
            SEntMan.EnsureComponent<TestListenerComponent>(player);
            var flight = SEntMan.System<SharedZLevelSystem>();
            var listener = SEntMan.System<ZLevelFlightStopTestSystem>();

            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            var stunned = new StunnedEvent();
            SEntMan.EventBus.RaiseLocalEvent(player, ref stunned);
            Assert.That(listener.GetEvents(player).Last().Reason, Is.EqualTo(ZLevelFlightStopReason.Stunned));

            SEntMan.EnsureComponent<StunnedComponent>(player);
            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Incapacitated));
            SEntMan.RemoveComponent<StunnedComponent>(player);

            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            var thrown = new ThrownEvent(null, player);
            SEntMan.EventBus.RaiseLocalEvent(player, ref thrown);
            Assert.That(listener.GetEvents(player).Last().Reason, Is.EqualTo(ZLevelFlightStopReason.Thrown));

            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            var chair = SEntMan.SpawnEntity("ZLevelFlightTestStrap", SEntMan.GetCoordinates(PlayerCoords));
            var buckle = SEntMan.GetComponent<BuckleComponent>(player);
            var buckleSystem = SEntMan.System<SharedBuckleSystem>();
            Assert.That(buckleSystem.TryBuckle(player, player, chair, buckle), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(listener.GetEvents(player).Last().Reason, Is.EqualTo(ZLevelFlightStopReason.Buckled));
                Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Buckled));
            });
            buckleSystem.Unbuckle((player, buckle), player);

            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            var knockedDown = new KnockedDownEvent();
            SEntMan.EventBus.RaiseLocalEvent(player, ref knockedDown);
            Assert.That(listener.GetEvents(player).Last().Reason, Is.EqualTo(ZLevelFlightStopReason.KnockedDown));
            SEntMan.EnsureComponent<KnockedDownComponent>(player);
            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Incapacitated));
            SEntMan.RemoveComponent<KnockedDownComponent>(player);

            Assert.That(flight.TryStartFlight(player), Is.EqualTo(ZLevelFlightResult.Success));
            var mobState = SEntMan.GetComponent<MobStateComponent>(player);
            var changed = new MobStateChangedEvent(player, mobState, MobState.Alive, MobState.Critical);
            SEntMan.EventBus.RaiseLocalEvent(player, changed);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelFlightComponent>(player).Active, Is.False);
                Assert.That(listener.GetEvents(player).Last().Reason, Is.EqualTo(ZLevelFlightStopReason.Incapacitated));
            });
        });
    }

    [Test]
    public async Task FlyingMobContentSeparatesCapabilityFromPlayerControls()
    {
        await Server.WaitAssertion(() =>
        {
            var bat = ProtoMan.Index<EntityPrototype>(BatPrototype);
            var dragon = ProtoMan.Index<EntityPrototype>(DragonPrototype);

            Assert.Multiple(() =>
            {
                Assert.That(bat.TryGetComponent<ZLevelFlightComponent>(out _, Factory), Is.True);
                Assert.That(bat.TryGetComponent<ZLevelFlightControlsComponent>(out _, Factory), Is.False);
                Assert.That(dragon.TryGetComponent<ZLevelFlightComponent>(out _, Factory), Is.True);
                Assert.That(dragon.TryGetComponent<ZLevelFlightControlsComponent>(out _, Factory), Is.True);
            });
        });
    }

    private void ConfigureMap()
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            MapData.MapUid,
            0,
            2,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
    }

    private void AssertFlightActions(
        ZLevelFlightControlsComponent controls,
        SharedActionsSystem actions)
    {
        Assert.Multiple(() =>
        {
            Assert.That(controls.ToggleActionEntity, Is.Not.Null);
            Assert.That(controls.MoveUpActionEntity, Is.Not.Null);
            Assert.That(controls.MoveDownActionEntity, Is.Not.Null);
            Assert.That(actions.GetAction(controls.ToggleActionEntity), Is.Not.Null);
            Assert.That(actions.GetAction(controls.MoveUpActionEntity), Is.Not.Null);
            Assert.That(actions.GetAction(controls.MoveDownActionEntity), Is.Not.Null);
        });
    }
}

public sealed class ZLevelFlightStopTestSystem : TestListenerSystem<ZLevelFlightStoppedEvent>;
