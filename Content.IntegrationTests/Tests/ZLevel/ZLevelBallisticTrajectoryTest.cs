// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.Actions.Components;
using Content.Shared.CombatMode;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Input;
using Content.Shared.Magic.Events;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Input;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelBallisticTrajectoryTest : GameTest
{
    private const int FrameOrigin = 5;
    private const float NormalSpeed = 12f;

    [TestPrototypes]
    private const string BallisticPrototypes = @"
- type: entity
  parent: BaseBulletPractice
  id: ZLevelBallisticProbeProjectile
  components:
  - type: Projectile
    deleteOnCollide: false
    damage:
      types:
        Blunt: 1

- type: entity
  id: ZLevelBallisticTarget
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.4,-0.4,0.4,0.4""
        layer:
        - WallLayer
        hard: true

- type: entity
  parent: ZLevelBallisticTarget
  id: ZLevelBallisticReflector
  components:
  - type: Reflect
    reflectProb: 1
    spread: 0

- type: entity
  id: ZLevelBallisticTestGun
  components:
  - type: Gun
    fireRate: 10
    projectileSpeed: 12
    minAngle: 0
    maxAngle: 0
    angleIncrease: 0
  - type: BasicEntityAmmoProvider
    proto: ZLevelBallisticProbeProjectile
    capacity: 1
    count: 1

- type: entity
  parent: ZLevelBallisticTestGun
  id: ZLevelBallisticNetworkGun
  components:
  - type: Gun
    projectileSpeed: 0.5
  - type: CombatMode

- type: entity
  parent: ZLevelBallisticTestGun
  id: ZLevelBallisticBurstTestGun
  components:
  - type: Gun
    burstFireRate: 30
    shotsPerBurst: 3
    selectedMode: Burst
    availableModes:
    - Burst
  - type: BasicEntityAmmoProvider
    proto: ZLevelBallisticProbeProjectile
    capacity: 3
    count: 3

- type: entity
  parent: ZLevelBallisticProbeProjectile
  id: ZLevelBallisticSpreadProjectile
  components:
  - type: ProjectileSpread
    proto: ZLevelBallisticProbeProjectile
    count: 3
    spread: 20

- type: entity
  id: ZLevelBallisticSpreadTestGun
  components:
  - type: Gun
    fireRate: 10
    projectileSpeed: 12
    minAngle: 0
    maxAngle: 0
    angleIncrease: 0
  - type: BasicEntityAmmoProvider
    proto: ZLevelBallisticSpreadProjectile
    capacity: 1
    count: 1

- type: entity
  id: ZLevelBallisticThrower
  components:
  - type: Hands
    hands:
      hand_right:
        location: Right
    sortedHands:
    - hand_right

- type: entity
  parent: ZLevelBallisticThrower
  id: ZLevelBallisticShortRangeThrower
  components:
  - type: Hands
    throwRange: 2
";

    public sealed class ProjectileHitListenerSystem : TestListenerSystem<ProjectileHitEvent>;
    public sealed class BoundaryHitListenerSystem : TestListenerSystem<ZLevelBallisticBoundaryHitEvent>;
    public sealed class ThrowHitListenerSystem : TestListenerSystem<ThrowHitByEvent>;
    public sealed class AmmoShotListenerSystem : TestListenerSystem<AmmoShotEvent>;

    [Test]
    public async Task NetworkGunAcceptsVisibleLowerFloorEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity targetNet = default;
        NetEntity coordinateNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            var forgedCoordinate = Spawn(testMap, null, new Vector2(8.25f, 0.5f), 0);
            gunNet = SEntMan.GetNetEntity(gun);
            targetNet = SEntMan.GetNetEntity(target);
            coordinateNet = SEntMan.GetNetEntity(forgedCoordinate);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, coordinateNet, targetNet, FrameOrigin);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var shots = SEntMan.System<AmmoShotListenerSystem>().GetEvents(gun).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(shots, Has.Length.EqualTo(1));
                Assert.That(shots[0].FiredProjectiles, Has.Count.EqualTo(1));
                Assert.That(
                    SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(
                        shots[0].FiredProjectiles.Single()).PlanarDistance,
                    Is.EqualTo(4f).Within(0.0001f));
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.Zero);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunAcceptsVisibleLowerFloorCoordinateWithoutEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity coordinateNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 2);
            var coordinateEntity = Spawn(
                testMap,
                "ZLevelBallisticTarget",
                new Vector2(4.25f, 0.5f),
                0);
            gunNet = SEntMan.GetNetEntity(gun);
            coordinateNet = SEntMan.GetNetEntity(coordinateEntity);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, coordinateNet, null, FrameOrigin);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var shots = SEntMan.System<AmmoShotListenerSystem>().GetEvents(gun).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(shots, Has.Length.EqualTo(1));
                Assert.That(shots[0].FiredProjectiles, Has.Count.EqualTo(1));
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.Zero);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunPreservesNativeSameFloorEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity targetNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 1);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 1);
            gunNet = SEntMan.GetNetEntity(gun);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, targetNet, targetNet, FrameOrigin + 1);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var shots = SEntMan.System<AmmoShotListenerSystem>().GetEvents(gun).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(shots, Has.Length.EqualTo(1));
                Assert.That(shots[0].FiredProjectiles, Has.Count.EqualTo(1));
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.Zero);
                Assert.That(metrics.BallisticRouteAttempts, Is.Zero);
                Assert.That(metrics.BallisticRoutesStarted, Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunRejectsForgedUpperFloorEntityBeforeConsumingAmmo()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity targetNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 1);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 2);
            gunNet = SEntMan.GetNetEntity(gun);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, targetNet, targetNet, FrameOrigin + 2);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<AmmoShotListenerSystem>().Count(gun), Is.Zero);
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(1));
                Assert.That(SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts, Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunRejectsForgedUpperFloorCoordinateBeforeConsumingAmmo()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity coordinateNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 1);
            var coordinateEntity = Spawn(
                testMap,
                "ZLevelBallisticTarget",
                new Vector2(4.25f, 0.5f),
                2);
            gunNet = SEntMan.GetNetEntity(gun);
            coordinateNet = SEntMan.GetNetEntity(coordinateEntity);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, coordinateNet, null, FrameOrigin + 2);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<AmmoShotListenerSystem>().Count(gun), Is.Zero);
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(1));
                Assert.That(SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts, Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunRevalidatesStaleEntityLayerBeforeConsumingAmmo()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity targetNet = default;
        EntityUid gun = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            gunNet = SEntMan.GetNetEntity(gun);
            targetNet = SEntMan.GetNetEntity(target);
        });
        await Pair.RunTicksSync(5);

        // Keep the client on its last Z0 state while authority moves the target.
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True));
        await SendNetworkShoot(gunNet, targetNet, targetNet, FrameOrigin);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<AmmoShotListenerSystem>().Count(gun), Is.Zero);
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkGunRejectsEntityOnDifferentStructuralFrame()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        NetEntity targetNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 2);

            var otherGrid = Server.ResolveDependency<IMapManager>().CreateGridEntity(testMap.MapId);
            var map = SEntMan.System<SharedMapSystem>();
            var otherGridComp = SEntMan.GetComponent<MapGridComponent>(otherGrid);
            map.SetTile(otherGrid, otherGridComp, Vector2i.Zero, new Tile(1));
            var target = SEntMan.SpawnEntity(
                "ZLevelBallisticTarget",
                new EntityCoordinates(otherGrid, new Vector2(0.25f, 0.5f)));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 0), Is.True);

            gunNet = SEntMan.GetNetEntity(gun);
            targetNet = SEntMan.GetNetEntity(target);
        });
        await Pair.RunTicksSync(5);

        await SendNetworkShoot(gunNet, targetNet, targetNet, 0);
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<AmmoShotListenerSystem>().Count(gun), Is.Zero);
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkStopClearsIdleGunTargetState()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gunNet = default;
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            gun = SpawnNetworkGun(testMap, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            var component = SEntMan.GetComponent<GunComponent>(gun);
#pragma warning disable RA0002
            component.ShotCounter = 0;
            component.ShootCoordinates = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            component.Target = target;
            component.TargetWorldZ = FrameOrigin;
#pragma warning restore RA0002
            gunNet = SEntMan.GetNetEntity(gun);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitPost(() =>
            CEntMan.RaisePredictiveEvent(new RequestStopShootEvent { Gun = gunNet }));
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<GunComponent>(gun);
            Assert.Multiple(() =>
            {
                Assert.That(component.ShotCounter, Is.Zero);
                Assert.That(component.ShootCoordinates, Is.Null);
                Assert.That(component.Target, Is.Null);
                Assert.That(component.TargetWorldZ, Is.Null);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowAcceptsVisibleLowerFloorEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity coordinateNet = default;
        NetEntity targetNet = default;
        EntityUid thrower = default;
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            var forgedCoordinate = Spawn(testMap, null, new Vector2(8.25f, 0.5f), 0);
            coordinateNet = SEntMan.GetNetEntity(forgedCoordinate);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
            Assert.That(
                CEntMan.System<ZLevelTargetingSystem>()
                    .GetTargetingModeForInput(ContentKeyFunctions.ThrowItemInHand),
                Is.EqualTo(ZLevelTargetingMode.VisibleCrossFloorRanged)));
        await SendNetworkThrow(coordinateNet, targetNet, FrameOrigin);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowAcceptsVisibleLowerFloorCoordinateWithoutEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity coordinateNet = default;
        EntityUid thrower = default;
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 2);
            var coordinateEntity = Spawn(testMap, null, new Vector2(4.25f, 0.5f), 0);
            coordinateNet = SEntMan.GetNetEntity(coordinateEntity);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkThrow(coordinateNet, null, FrameOrigin);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowPreservesNativeSameFloorEntityTarget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        EntityUid thrower = default;
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 1);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 1);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkThrow(targetNet, targetNet, FrameOrigin + 1);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(item), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.Zero);
                Assert.That(metrics.BallisticRoutesStarted, Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowRejectsForgedUpperFloorEntityBeforeDroppingItem()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        EntityUid thrower = default;
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 1);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 2);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkThrow(targetNet, targetNet, FrameOrigin + 2);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(item), Is.False);
                Assert.That(
                    SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts,
                    Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowRejectsForgedUpperFloorCoordinateBeforeDroppingItem()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity coordinateNet = default;
        EntityUid thrower = default;
        EntityUid item = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 1);
            var coordinateEntity = Spawn(testMap, null, new Vector2(4.25f, 0.5f), 2);
            coordinateNet = SEntMan.GetNetEntity(coordinateEntity);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await SendNetworkThrow(coordinateNet, null, FrameOrigin + 2);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(item), Is.False);
                Assert.That(
                    SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts,
                    Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowRevalidatesStaleEntityLayerBeforeDroppingItem()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        EntityUid thrower = default;
        EntityUid item = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True));
        await SendNetworkThrow(targetNet, targetNet, FrameOrigin);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(item), Is.False);
                Assert.That(
                    SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts,
                    Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task NetworkThrowRejectsDeletedExplicitTargetBeforeDroppingItem()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity targetNet = default;
        EntityUid thrower = default;
        EntityUid item = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            (thrower, item) = SpawnNetworkThrower(testMap, new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            targetNet = SEntMan.GetNetEntity(target);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(target));
        await SendNetworkThrow(targetNet, targetNet, FrameOrigin);
        await Pair.RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(IsHolding(thrower, item), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(item), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(item), Is.False);
                Assert.That(
                    SEntMan.System<SharedZLevelMetricsSystem>().Snapshot().BallisticRouteAttempts,
                    Is.Zero);
            });
            Server.PlayerMan.SetAttachedEntity(ServerSession!, null);
        });
    }

    [Test]
    public async Task CrossFloorBurstPreservesAuthoritativeTargetForEveryShot()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid gun = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<DamageableComponent>(source);
            gun = Spawn(testMap, "ZLevelBallisticBurstTestGun", new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);
            SEntMan.System<SharedTransformSystem>().SetParent(gun, source);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);

            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            Assert.That(
                SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().AttemptShoot(
                    source,
                    (gun, SEntMan.GetComponent<GunComponent>(gun)),
                    SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                    target),
                Is.True);
        });

        for (var i = 0; i < 10; i++)
            await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var shots = SEntMan.System<AmmoShotListenerSystem>().GetEvents(gun).ToArray();
            var component = SEntMan.GetComponent<GunComponent>(gun);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(shots, Has.Length.EqualTo(3));
                Assert.That(shots.SelectMany(shot => shot.FiredProjectiles).Count(), Is.EqualTo(3));
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.Zero);
                Assert.That(component.BurstActivated, Is.False);
                Assert.That(component.ShootCoordinates, Is.Null);
                Assert.That(component.Target, Is.Null);
                Assert.That(component.TargetWorldZ, Is.Null);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(3));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(3));
            });
        });
    }

    [Test]
    public async Task CrossFloorBurstCancelsWhenTargetLayerBecomesStale()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid gun = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<DamageableComponent>(source);
            gun = Spawn(testMap, "ZLevelBallisticBurstTestGun", new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);
            SEntMan.System<SharedTransformSystem>().SetParent(gun, source);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);

            Assert.That(
                SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().AttemptShoot(
                    source,
                    (gun, SEntMan.GetComponent<GunComponent>(gun)),
                    SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                    target),
                Is.True);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(target, 1), Is.True);
        });

        await RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<GunComponent>(gun);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<AmmoShotListenerSystem>().Count(gun), Is.EqualTo(1));
                Assert.That(SEntMan.GetComponent<BasicEntityAmmoProviderComponent>(gun).Count, Is.EqualTo(2));
                Assert.That(component.BurstActivated, Is.False);
                Assert.That(component.BurstShotsCount, Is.Zero);
                Assert.That(component.ShootCoordinates, Is.Null);
                Assert.That(component.Target, Is.Null);
                Assert.That(component.TargetWorldZ, Is.Null);
            });
        });
    }

    [Test]
    public async Task OpenTrajectoryCrossesTwoLevelsAndHitsOnlyTargetFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;
        EntityUid wrongFloor = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            wrongFloor = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);

            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            FireProjectile(projectile, source, target, NormalSpeed);
        });

        var observedLevels = await ObserveProjectileUntilSpent(projectile);

        await Server.WaitAssertion(() =>
        {
            var hits = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(observedLevels, Does.Contain(2));
                Assert.That(observedLevels, Does.Contain(1));
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(hits, Has.Length.EqualTo(1));
                Assert.That(hits[0].Target, Is.EqualTo(target));
                Assert.That(hits[0].Target, Is.Not.EqualTo(wrongFloor));
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesCompleted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesRejected, Is.Zero);
                Assert.That(metrics.BallisticCrossings, Is.EqualTo(2));
                Assert.That(metrics.BallisticClosedBoundaries, Is.Zero);
                Assert.That(metrics.BallisticCollisionCancellations, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
                Assert.That(metrics.BallisticContactFlushes, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task ClosedProjectileBoundaryStopsOnSourceLevel()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);
            CloseBoundary(testMap, new Vector2i(1, 0), 1, ZLevelBoundaryChannels.Projectile);

            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            FireProjectile(projectile, source, target, NormalSpeed);
        });

        await ObserveProjectileUntilSpent(projectile);

        await Server.WaitAssertion(() =>
        {
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var physics = SEntMan.System<SharedPhysicsSystem>();
            var body = SEntMan.GetComponent<PhysicsComponent>(projectile);
            var boundaryHits = SEntMan.System<BoundaryHitListenerSystem>().GetEvents(projectile).ToArray();
            var projectileHits = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetZLevel(projectile), Is.EqualTo(2));
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(FrameOrigin + 2));
                Assert.That(physics.GetMapLinearVelocity(projectile, component: body).LengthSquared(), Is.LessThan(0.0001f));
                Assert.That(boundaryHits, Has.Length.EqualTo(1));
                Assert.That(boundaryHits[0].FromLocalZ, Is.EqualTo(2));
                Assert.That(boundaryHits[0].ToLocalZ, Is.EqualTo(1));
                Assert.That(projectileHits, Is.Empty);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesCompleted, Is.Zero);
                Assert.That(metrics.BallisticRoutesRejected, Is.Zero);
                Assert.That(metrics.BallisticCrossings, Is.Zero);
                Assert.That(metrics.BallisticClosedBoundaries, Is.EqualTo(1));
                Assert.That(metrics.BallisticCollisionCancellations, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
                Assert.That(metrics.BallisticContactFlushes, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ContactAtClippedCrossingIsResolvedBeforeFloorSwitch()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid sourceFloorObstacle = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.1f, 0.5f), 2);
            sourceFloorObstacle = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(1.35f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.1f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.1f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);

            FireProjectile(projectile, source, target, 120f);
        });

        await ObserveProjectileUntilSpent(projectile, 10);

        await Server.WaitAssertion(() =>
        {
            var hits = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(hits, Has.Length.EqualTo(1));
                Assert.That(hits[0].Target, Is.EqualTo(sourceFloorObstacle));
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(projectile), Is.EqualTo(2));
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
            });
        });
    }

    [Test]
    public async Task InvisibleAndSameLevelTargetsDoNotCreateVerticalRoutes()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var sameLevelTarget = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 2);
            var hiddenTarget = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(5.25f, 0.5f), 0);
            var sameLevelProjectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            var hiddenProjectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            var guns = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var ballistics = SEntMan.System<SharedZLevelBallisticSystem>();
            CloseBoundary(testMap, new Vector2i(5, 0), 1, ZLevelBoundaryChannels.Visibility);

            guns.ShootProjectile(sameLevelProjectile, Vector2.UnitX, Vector2.Zero, source, source, speed: 1f);
            guns.ShootProjectile(hiddenProjectile, Vector2.UnitX, Vector2.Zero, source, source, speed: 1f);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            Assert.Multiple(() =>
            {
                Assert.That(ballistics.TryStartTrajectory(sameLevelProjectile, sameLevelTarget, Vector2.UnitX), Is.False);
                Assert.That(ballistics.TryStartTrajectory(hiddenProjectile, hiddenTarget, Vector2.UnitX), Is.False);
                Assert.That(ballistics.TryStartTrajectory(hiddenProjectile, hiddenTarget, Vector2.Zero), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(sameLevelProjectile), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(hiddenProjectile), Is.False);
            });

            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(3));
                Assert.That(metrics.BallisticRoutesStarted, Is.Zero);
                Assert.That(metrics.BallisticRoutesRejected, Is.EqualTo(3));
                Assert.That(metrics.BallisticRoutesCompleted, Is.Zero);
                Assert.That(metrics.BallisticCrossings, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
                Assert.That(metrics.BallisticContactFlushes, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ActiveTrajectoryCannotBeRetargeted()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var firstTarget = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            var secondTarget = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(6.25f, 0.5f), 1);
            var projectile = Spawn(
                testMap,
                "ZLevelBallisticProbeProjectile",
                new Vector2(0.25f, 0.5f),
                2);
            var direction = GetMapDirection(projectile, firstTarget);
            SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().ShootProjectile(
                projectile,
                direction,
                Vector2.Zero,
                source,
                source,
                NormalSpeed);

            var ballistics = SEntMan.System<SharedZLevelBallisticSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            Assert.That(ballistics.TryStartTrajectory(projectile, firstTarget, direction), Is.True);
            var original = SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(projectile);
            var originalTargetLocalZ = original.TargetLocalZ;
            var originalPlanarDistance = original.PlanarDistance;
            var originalDirection = original.Direction;

            Assert.That(
                ballistics.TryStartTrajectory(
                    projectile,
                    secondTarget,
                    GetMapDirection(projectile, secondTarget)),
                Is.False);
            var actual = SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(projectile);
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(actual.TargetLocalZ, Is.EqualTo(originalTargetLocalZ));
                Assert.That(actual.PlanarDistance, Is.EqualTo(originalPlanarDistance));
                Assert.That(actual.Direction, Is.EqualTo(originalDirection));
                Assert.That(snapshot.BallisticRouteAttempts, Is.EqualTo(2));
                Assert.That(snapshot.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(snapshot.BallisticRoutesRejected, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task RemovingPhysicsInvalidatesTrajectoryState()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);

            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            FireProjectile(projectile, source, target, 0.1f);
            Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.True);
            SEntMan.RemoveComponent<PhysicsComponent>(projectile);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesCompleted, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.EqualTo(1));
                Assert.That(metrics.BallisticCrossings, Is.Zero);
            });
        });
    }

    [Test]
    public async Task GunTargetStartsVerticalTrajectoryThroughNormalFirePath()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var gun = Spawn(testMap, "ZLevelBallisticTestGun", new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);

            var gunSystem = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunComponent = SEntMan.GetComponent<GunComponent>(gun);
            var targetCoordinates = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            Assert.That(
                gunSystem.AttemptShoot(source, (gun, gunComponent), targetCoordinates, target),
                Is.True);
            projectile = SEntMan.System<AmmoShotListenerSystem>()
                .GetEvents(gun)
                .Single()
                .FiredProjectiles
                .Single();
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<TargetedProjectileComponent>(projectile).Target, Is.EqualTo(target));
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.True);
            });
        });

        var observedLevels = await ObserveProjectileUntilSpent(projectile);

        await Server.WaitAssertion(() =>
        {
            var hits = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(observedLevels, Does.Contain(1));
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(hits, Has.Length.EqualTo(1));
                Assert.That(hits[0].Target, Is.EqualTo(target));
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesCompleted, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task TargetlessGunCoordinateStartsPureVerticalTrajectory()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var gun = Spawn(testMap, "ZLevelBallisticTestGun", new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(0.25f, 0.5f), 0);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);

            var gunSystem = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunComponent = SEntMan.GetComponent<GunComponent>(gun);
            var targetCoordinates = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            Assert.That(
                gunSystem.AttemptShoot(
                    source,
                    (gun, gunComponent),
                    targetCoordinates,
                    targetWorldZ: FrameOrigin),
                Is.True);
            projectile = SEntMan.System<AmmoShotListenerSystem>()
                .GetEvents(gun)
                .Single()
                .FiredProjectiles
                .Single();
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);

            var trajectory = SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(projectile);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<TargetedProjectileComponent>(projectile), Is.False);
                Assert.That(trajectory.TargetLocalZ, Is.Zero);
                Assert.That(
                    trajectory.PlanarDistance,
                    Is.EqualTo(SharedGunSystem.VerticalShotPlanarDisplacement).Within(0.0001f));
            });
        });

        var observedLevels = await ObserveProjectileUntilSpent(projectile);

        await Server.WaitAssertion(() =>
        {
            var hits = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).ToArray();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(hits, Has.Length.EqualTo(1));
                Assert.That(hits[0].Target, Is.EqualTo(target));
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticCrossings, Is.EqualTo(2));
                Assert.That(metrics.BallisticRoutesCompleted, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task SameFloorGunCoordinateDoesNotAttemptVerticalTrajectory()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var gun = Spawn(testMap, "ZLevelBallisticTestGun", new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);

            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            Assert.That(
                SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().AttemptShoot(
                    source,
                    (gun, SEntMan.GetComponent<GunComponent>(gun)),
                    SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                    targetWorldZ: SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(target)),
                Is.True);

            var projectile = SEntMan.System<AmmoShotListenerSystem>()
                .GetEvents(gun)
                .Single()
                .FiredProjectiles
                .Single();
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
                Assert.That(snapshot.BallisticRouteAttempts, Is.Zero);
                Assert.That(snapshot.BallisticRoutesStarted, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ActionGunForwardsTargetlessCoordinateWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var gun = Spawn(testMap, "ZLevelBallisticTestGun", new Vector2(0.25f, 0.5f), 2);
            var targetCoordinates = new EntityCoordinates(testMap.Grid, new Vector2(4.25f, 0.5f));
            SEntMan.EnsureComponent<TestListenerComponent>(gun);
#pragma warning disable RA0002
            SEntMan.EnsureComponent<ActionGunComponent>(source).Gun = gun;
#pragma warning restore RA0002

            var fired = new ActionGunShootEvent
            {
                Performer = source,
                Target = targetCoordinates,
                TargetWorldZ = FrameOrigin,
            };
            SEntMan.EventBus.RaiseLocalEvent(source, fired);

            var projectile = SEntMan.System<AmmoShotListenerSystem>()
                .GetEvents(gun)
                .Single()
                .FiredProjectiles
                .Single();
            var trajectory = SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(projectile);
            Assert.Multiple(() =>
            {
                Assert.That(trajectory.TargetLocalZ, Is.Zero);
                Assert.That(trajectory.PlanarDistance, Is.EqualTo(4f).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task ProjectileSpellForwardsTargetlessCoordinateWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var action = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var actionComponent = SEntMan.EnsureComponent<ActionComponent>(action);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            var fired = new ProjectileSpellEvent
            {
                Performer = source,
                Action = (action, actionComponent),
                Target = new EntityCoordinates(testMap.Grid, new Vector2(4.25f, 0.5f)),
                TargetWorldZ = FrameOrigin,
                Prototype = "ZLevelBallisticProbeProjectile",
            };
            SEntMan.EventBus.RaiseEvent(EventSource.Local, fired);

            EntityUid projectile = default;
            ZLevelBallisticTrajectoryComponent? trajectory = null;
            var query = SEntMan.EntityQueryEnumerator<ZLevelBallisticTrajectoryComponent>();
            while (query.MoveNext(out var candidate, out var candidateTrajectory))
            {
                if (candidateTrajectory.FrameUid != testMap.Grid.Owner)
                    continue;

                Assert.That(trajectory, Is.Null);
                projectile = candidate;
                trajectory = candidateTrajectory;
            }

            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(fired.Handled, Is.True);
                Assert.That(projectile.IsValid(), Is.True);
                Assert.That(trajectory, Is.Not.Null);
                Assert.That(trajectory!.TargetLocalZ, Is.Zero);
                Assert.That(trajectory.PlanarDistance, Is.EqualTo(4f).Within(0.0001f));
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task SpreadProjectilesPreserveTargetDistanceInVerticalRoutes()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var gun = Spawn(testMap, "ZLevelBallisticSpreadTestGun", new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            SEntMan.EnsureComponent<TestListenerComponent>(gun);

            var gunSystem = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var gunComponent = SEntMan.GetComponent<GunComponent>(gun);
            Assert.That(
                gunSystem.AttemptShoot(
                    source,
                    (gun, gunComponent),
                    SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                    target),
                Is.True);

            var projectiles = SEntMan.System<AmmoShotListenerSystem>()
                .GetEvents(gun)
                .Single()
                .FiredProjectiles;
            Assert.That(projectiles, Has.Count.EqualTo(3));
            Assert.That(
                projectiles.Select(uid =>
                    SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(uid).PlanarDistance),
                Is.All.EqualTo(4f).Within(0.0001f));
        });
    }

    [Test]
    public async Task CursorTargetStartsVerticalTrajectoryThroughManualThrowPath()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid thrown = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var thrower = Spawn(testMap, "ZLevelBallisticThrower", new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            SEntMan.EnsureComponent<TestListenerComponent>(target);
            thrown = Spawn(testMap, "Crowbar", new Vector2(0.25f, 0.5f), 2);

            var hands = SEntMan.System<Content.Server.Hands.Systems.HandsSystem>();
            Assert.That(
                hands.TryPickupAnyHand(thrower, thrown, checkActionBlocker: false, animate: false),
                Is.True);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            var targetCoordinates = SEntMan.GetComponent<TransformComponent>(target).Coordinates;
            Assert.That(
                hands.ThrowHeldItem(thrower, targetCoordinates, target: target),
                Is.True);
            Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown), Is.True);
        });

        var observedLevels = new HashSet<int> { 2 };
        var hitTarget = false;
        for (var i = 0; i < 120; i++)
        {
            await RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                observedLevels.Add(SEntMan.System<SharedZLevelSystem>().GetZLevel(thrown));
                hitTarget = SEntMan.System<ThrowHitListenerSystem>()
                    .GetEvents(target)
                    .Any(hit => hit.Thrown == thrown);
            });
            if (hitTarget)
                break;
        }

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            var transform = SEntMan.GetComponent<TransformComponent>(thrown);
            var physics = SEntMan.GetComponent<PhysicsComponent>(thrown);
            var mapPosition = SEntMan.System<SharedTransformSystem>()
                .GetMapCoordinates((thrown, transform)).Position;
            var diagnostic =
                $"levels=[{string.Join(",", observedLevels.Order())}], " +
                $"position={mapPosition}, velocity={physics.LinearVelocity}, " +
                $"thrown={SEntMan.HasComponent<ThrownItemComponent>(thrown)}, " +
                $"trajectory={SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown)}, " +
                $"completed={metrics.BallisticRoutesCompleted}, " +
                $"closed={metrics.BallisticClosedBoundaries}, " +
                $"collision={metrics.BallisticCollisionCancellations}, " +
                $"invalid={metrics.BallisticInvalidCancellations}";
            Assert.Multiple(() =>
            {
                Assert.That(hitTarget, Is.True, diagnostic);
                Assert.That(observedLevels, Does.Contain(1));
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown), Is.False);
                Assert.That(metrics.BallisticRouteAttempts, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(1));
                Assert.That(metrics.BallisticRoutesCompleted, Is.EqualTo(1));
                Assert.That(metrics.BallisticClosedBoundaries, Is.Zero);
                Assert.That(metrics.BallisticCollisionCancellations, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ManualThrowUsesClampedDisplacementForVerticalRoute()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid thrown = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var thrower = Spawn(testMap, "ZLevelBallisticShortRangeThrower", new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(6.25f, 0.5f), 0);
            thrown = Spawn(testMap, "Crowbar", new Vector2(0.25f, 0.5f), 2);

            var hands = SEntMan.System<Content.Server.Hands.Systems.HandsSystem>();
            Assert.That(
                hands.TryPickupAnyHand(thrower, thrown, checkActionBlocker: false, animate: false),
                Is.True);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();

            Assert.That(
                hands.ThrowHeldItem(
                    thrower,
                    SEntMan.GetComponent<TransformComponent>(target).Coordinates,
                    target: target),
                Is.True);
            Assert.That(
                SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(thrown).PlanarDistance,
                Is.EqualTo(2f).Within(0.0001f));
        });

        for (var i = 0; i < 60; i++)
        {
            await RunTicksSync(1);
            var completed = false;
            await Server.WaitAssertion(() =>
            {
                completed = !SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown);
            });
            if (completed)
                break;
        }

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown), Is.False);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(thrown), Is.Zero);
                Assert.That(metrics.BallisticRoutesCompleted, Is.EqualTo(1));
                Assert.That(metrics.BallisticCrossings, Is.EqualTo(2));
                Assert.That(metrics.BallisticCollisionCancellations, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
            });
        });
    }

    [Test]
    public async Task TranslatedAfterLaunchRotatedFrameUsesLocalCrossingTiles()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));

            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            SEntMan.EnsureComponent<TestListenerComponent>(projectile);

            FireProjectile(projectile, source, target, NormalSpeed);

            // Translation after launch must preserve a frame-local route. A
            // dynamic rotation deliberately keeps Robust's inertial velocity
            // semantics and can therefore cause the projectile to miss.
            transform.SetLocalPosition(testMap.Grid, new Vector2(-4f, 9f));
        });

        var observedLevels = await ObserveProjectileUntilSpent(projectile);

        await Server.WaitAssertion(() =>
        {
            var hit = SEntMan.System<ProjectileHitListenerSystem>().GetEvents(projectile).Single();
            Assert.Multiple(() =>
            {
                Assert.That(hit.Target, Is.EqualTo(target));
                Assert.That(observedLevels, Does.Contain(1));
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(projectile), Is.EqualTo(FrameOrigin));
            });
        });
    }

    [Test]
    public async Task ThrownItemCrossesOpenLevelsAndHitsTargetFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid thrown = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var thrower = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            Spawn(testMap, "ZLevelBallisticTarget", new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            SEntMan.EnsureComponent<TestListenerComponent>(target);
            thrown = Spawn(testMap, "Crowbar", new Vector2(0.25f, 0.5f), 2);
            var physics = SEntMan.GetComponent<PhysicsComponent>(thrown);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(thrown, BodyType.Dynamic, body: physics);

            var direction = GetMapDirection(thrown, target);
            var throwing = SEntMan.System<ThrowingSystem>();
            throwing.TryThrow(
                thrown,
                direction,
                NormalSpeed,
                thrower,
                friction: 0f,
                recoil: false,
                doSpin: false);
            Assert.That(
                SEntMan.System<SharedZLevelBallisticSystem>().TryStartTrajectory(thrown, target, direction),
                Is.True);
        });

        var observedLevels = new HashSet<int> { 2 };
        var lastPosition = Vector2.Zero;
        var lastVelocity = Vector2.Zero;
        var hadThrownComponent = true;
        var hadTrajectory = true;
        for (var i = 0; i < 120; i++)
        {
            await RunTicksSync(1);
            var hit = false;
            await Server.WaitAssertion(() =>
            {
                var physics = SEntMan.GetComponent<PhysicsComponent>(thrown);
                var sharedPhysics = SEntMan.System<SharedPhysicsSystem>();
                var transform = SEntMan.System<SharedTransformSystem>();
                observedLevels.Add(SEntMan.System<SharedZLevelSystem>().GetZLevel(thrown));
                lastPosition = transform.GetMapCoordinates(thrown).Position;
                lastVelocity = sharedPhysics.GetMapLinearVelocity(thrown, component: physics);
                hadThrownComponent = SEntMan.HasComponent<ThrownItemComponent>(thrown);
                hadTrajectory = SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown);
                hit = SEntMan.System<ThrowHitListenerSystem>().Count(target) > 0;
            });
            if (hit)
                break;
        }

        await Server.WaitAssertion(() =>
        {
            var hits = SEntMan.System<ThrowHitListenerSystem>().GetEvents(target).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(observedLevels, Does.Contain(1));
                Assert.That(observedLevels, Does.Contain(0));
                Assert.That(
                    hits.Any(hit => hit.Thrown == thrown),
                    Is.True,
                    $"Last position {lastPosition}, velocity {lastVelocity}, " +
                    $"thrown component {hadThrownComponent}, trajectory {hadTrajectory}, " +
                    $"observed levels [{string.Join(", ", observedLevels.Order())}].");
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown), Is.False);
            });
        });
    }

    [Test]
    public async Task ReflectionAtClippedCrossingCancelsRouteAndPreservesReflectedVelocity()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.1f, 0.5f), 2);
            Spawn(testMap, "ZLevelBallisticReflector", new Vector2(1.35f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.1f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.1f, 0.5f), 2);

            FireProjectile(projectile, source, target, 120f);
        });

        await RunTicksSync(3);

        await Server.WaitAssertion(() =>
        {
            var physics = SEntMan.System<SharedPhysicsSystem>();
            var body = SEntMan.GetComponent<PhysicsComponent>(projectile);
            var projectileComponent = SEntMan.GetComponent<ProjectileComponent>(projectile);
            Assert.Multiple(() =>
            {
                Assert.That(projectileComponent.ProjectileSpent, Is.False);
                Assert.That(physics.GetMapLinearVelocity(projectile, component: body).X, Is.LessThan(0f));
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(projectile), Is.EqualTo(2));
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
            });
        });
    }

    [Test]
    public async Task ClosedProjectileBoundaryStopsThrownItem()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid thrown = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var thrower = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            thrown = Spawn(testMap, "Crowbar", new Vector2(0.25f, 0.5f), 2);
            var physics = SEntMan.GetComponent<PhysicsComponent>(thrown);
            SEntMan.System<SharedPhysicsSystem>().SetBodyType(thrown, BodyType.Dynamic, body: physics);
            CloseBoundary(testMap, new Vector2i(1, 0), 1, ZLevelBoundaryChannels.Projectile);

            var direction = GetMapDirection(thrown, target);
            SEntMan.System<ThrowingSystem>().TryThrow(
                thrown,
                direction,
                NormalSpeed,
                thrower,
                friction: 0f,
                recoil: false,
                doSpin: false);
            Assert.That(
                SEntMan.System<SharedZLevelBallisticSystem>().TryStartTrajectory(thrown, target, direction),
                Is.True);
        });

        for (var i = 0; i < 20; i++)
        {
            await RunTicksSync(1);
            var stopped = false;
            await Server.WaitAssertion(() =>
            {
                stopped = !SEntMan.HasComponent<ThrownItemComponent>(thrown);
            });
            if (stopped)
                break;
        }

        await Server.WaitAssertion(() =>
        {
            var body = SEntMan.GetComponent<PhysicsComponent>(thrown);
            var velocity = SEntMan.System<SharedPhysicsSystem>().GetMapLinearVelocity(thrown, component: body);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(thrown), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(thrown), Is.False);
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(thrown), Is.EqualTo(2));
                Assert.That(velocity.LengthSquared(), Is.LessThan(0.0001f));
            });
        });
    }

    [Test]
    public async Task AuthoredOpeningsExposeProjectileAndExplosionChannels()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses);
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            var floorOpening = SEntMan.SpawnEntity(
                "ZLevelFloorOpeningMarker",
                map.GridTileToLocal(testMap.Grid, grid, new Vector2i(2, 0)));
            Assert.That(zLevels.SetZLevelPosition(floorOpening, 2), Is.True);
            var openingTransform = SEntMan.GetComponent<TransformComponent>(floorOpening);
            if (!openingTransform.Anchored)
                transform.AnchorEntity(floorOpening, openingTransform);

            var stairs = SEntMan.SpawnEntity(
                "ZLevelStairsUp",
                map.GridTileToLocal(testMap.Grid, grid, new Vector2i(3, 0)));
            Assert.That(zLevels.SetZLevelPosition(stairs, 1), Is.True);
            var stairsTransform = SEntMan.GetComponent<TransformComponent>(stairs);
            if (!stairsTransform.Anchored)
                transform.AnchorEntity(stairs, stairsTransform);

            Assert.Multiple(() =>
            {
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, new Vector2i(2, 0), 2, 1,
                    ZLevelBoundaryChannels.Projectile), Is.True);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, new Vector2i(2, 0), 2, 1,
                    ZLevelBoundaryChannels.Explosion), Is.True);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, new Vector2i(3, 0), 1, 2,
                    ZLevelBoundaryChannels.Projectile), Is.True);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, new Vector2i(3, 0), 1, 2,
                    ZLevelBoundaryChannels.Explosion), Is.True);
            });
        });
    }

    [Test]
    public async Task TrajectoryStateSynchronizesToClient()
    {
        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, false));
        var testMap = await Pair.CreateTestMap();
        NetEntity projectileNet = default;
        NetEntity gridNet = default;
        ZLevelBallisticTrajectoryComponent expected = default!;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            var projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            FireProjectile(projectile, source, target, 0.1f);

            projectileNet = SEntMan.GetNetEntity(projectile);
            gridNet = SEntMan.GetNetEntity(testMap.Grid);
            expected = SEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(projectile);
        });

        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(projectileNet, out var clientProjectile), Is.True);
            Assert.That(CEntMan.TryGetEntity(gridNet, out var clientGrid), Is.True);
            var actual = CEntMan.GetComponent<ZLevelBallisticTrajectoryComponent>(clientProjectile!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(actual.FrameUid, Is.EqualTo(clientGrid!.Value));
                Assert.That(Vector2.DistanceSquared(actual.Origin, expected.Origin), Is.LessThan(0.0001f));
                Assert.That(Vector2.DistanceSquared(actual.Direction, expected.Direction), Is.LessThan(0.0001f));
                Assert.That(actual.PlanarDistance, Is.EqualTo(expected.PlanarDistance).Within(0.0001f));
                Assert.That(actual.SourceLocalZ, Is.EqualTo(2));
                Assert.That(actual.TargetLocalZ, Is.EqualTo(0));
                Assert.That(actual.NextCrossing, Is.Zero);
            });
        });
    }

    [Test]
    public async Task CompletedTrajectoryReconcilesDestinationFloorToClient()
    {
        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, false));
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        NetEntity projectileNet = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.5f), 2);
            var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(4.25f, 0.5f), 0);
            projectile = Spawn(testMap, "ZLevelBallisticProbeProjectile", new Vector2(0.25f, 0.5f), 2);
            FireProjectile(projectile, source, target, NormalSpeed);
            projectileNet = SEntMan.GetNetEntity(projectile);
        });

        await ObserveProjectileUntilSpent(projectile);
        await RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(projectile), Is.Zero);
            Assert.That(SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile), Is.False);
        });
        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(projectileNet, out var clientProjectile), Is.True);
            Assert.That(CEntMan.System<SharedZLevelSystem>().GetZLevel(clientProjectile!.Value), Is.Zero);
            Assert.That(
                CEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(clientProjectile.Value),
                Is.False);
        });
    }

    [Test]
    public async Task ConcurrentCrossingsShareOneContactFlushPerSubstep()
    {
        const int projectileCount = 4;
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            for (var i = 0; i < projectileCount; i++)
            {
                var y = -0.75f + i * 0.5f;
                var source = Spawn(testMap, null, new Vector2(0.1f, y), 2);
                var target = Spawn(testMap, "ZLevelBallisticTarget", new Vector2(8.1f, y), 0);
                var projectile = Spawn(
                    testMap,
                    "ZLevelBallisticProbeProjectile",
                    new Vector2(0.1f, y),
                    2);
                FireProjectile(projectile, source, target, 120f);
            }
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.BallisticRoutesStarted, Is.EqualTo(projectileCount));
                Assert.That(metrics.BallisticCrossings, Is.EqualTo(projectileCount));
                Assert.That(metrics.BallisticContactFlushes, Is.EqualTo(1));
                Assert.That(metrics.BallisticCollisionCancellations, Is.Zero);
                Assert.That(metrics.BallisticInvalidCancellations, Is.Zero);
            });
        });
    }

    private void Configure(
        TestMapData testMap,
        ZLevelDefaultBoundaryMode boundaryMode = ZLevelDefaultBoundaryMode.ExplicitOnly)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            2,
            0,
            boundaryMode);
        var transform = SEntMan.System<SharedTransformSystem>();
        Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);

        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        for (var z = 0; z <= 2; z++)
        {
            for (var x = -1; x <= 8; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    map.SetZLevelTile(
                        testMap.Grid,
                        grid,
                        new ZLevelTileIndices(x, y, z),
                        new Tile(1));
                }
            }
        }
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

    private EntityUid SpawnNetworkGun(TestMapData testMap, Vector2 position, int localZ)
    {
        Assert.That(ServerSession, Is.Not.Null);
        var gun = Spawn(testMap, "ZLevelBallisticNetworkGun", position, localZ);
        SEntMan.EnsureComponent<TestListenerComponent>(gun);
        var combatMode = SEntMan.GetComponent<CombatModeComponent>(gun);
        SEntMan.System<Content.Server.CombatMode.CombatModeSystem>()
            .SetInCombatMode(gun, true, combatMode);
        Server.PlayerMan.SetAttachedEntity(ServerSession!, gun);
        return gun;
    }

    private (EntityUid Thrower, EntityUid Item) SpawnNetworkThrower(
        TestMapData testMap,
        Vector2 position,
        int localZ)
    {
        Assert.That(ServerSession, Is.Not.Null);
        var thrower = Spawn(testMap, "ZLevelBallisticThrower", position, localZ);
        var item = Spawn(testMap, "Crowbar", position, localZ);
        Assert.That(
            SEntMan.System<Content.Server.Hands.Systems.HandsSystem>()
                .TryPickupAnyHand(thrower, item, checkActionBlocker: false, animate: false),
            Is.True);
        Server.PlayerMan.SetAttachedEntity(ServerSession!, thrower);
        return (thrower, item);
    }

    private bool IsHolding(EntityUid thrower, EntityUid item)
    {
        var hands = SEntMan.GetComponent<HandsComponent>(thrower);
        return SEntMan.System<Content.Server.Hands.Systems.HandsSystem>()
            .IsHolding((thrower, hands), item);
    }

    private async Task SendNetworkShoot(
        NetEntity gunNet,
        NetEntity coordinateEntityNet,
        NetEntity? targetNet,
        int? coordinateLayer)
    {
        await Client.WaitPost(() =>
        {
            var coordinateEntity = CEntMan.GetEntity(coordinateEntityNet);
            var coordinates = CEntMan.GetComponent<TransformComponent>(coordinateEntity).Coordinates;
            CEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = gunNet,
                Coordinates = CEntMan.GetNetCoordinates(coordinates),
                Target = targetNet,
                CoordinateLayer = coordinateLayer,
            });
        });
    }

    private async Task SendNetworkThrow(
        NetEntity coordinateEntityNet,
        NetEntity? targetNet,
        int? coordinateLayer)
    {
        await Client.WaitPost(() =>
        {
            var inputManager = Client.ResolveDependency<IInputManager>();
            var input = CEntMan.System<Robust.Client.GameObjects.InputSystem>();
            var coordinateEntity = CEntMan.GetEntity(coordinateEntityNet);
            var function = ContentKeyFunctions.ThrowItemInHand;
            var functionId = inputManager.NetworkBindMap.KeyFunctionID(function);

            ClientFullInputCmdMessage Message(BoundKeyState state) => new(
                CGameTiming.CurTick,
                CGameTiming.TickFraction,
                functionId)
            {
                State = state,
                Coordinates = CEntMan.GetComponent<TransformComponent>(coordinateEntity).Coordinates,
                CoordinateLayer = coordinateLayer,
                Uid = targetNet is { } target ? CEntMan.GetEntity(target) : EntityUid.Invalid,
            };

            Assert.That(
                input.HandleInputCommand(Client.Session, function, Message(BoundKeyState.Down)),
                Is.False,
                "A valid local throw command must be dispatched to the server.");
            input.HandleInputCommand(Client.Session, function, Message(BoundKeyState.Up));
        });
    }

    private void FireProjectile(EntityUid projectile, EntityUid source, EntityUid target, float speed)
    {
        var direction = GetMapDirection(projectile, target);
        SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>().ShootProjectile(
            projectile,
            direction,
            Vector2.Zero,
            source,
            source,
            speed);
        Assert.That(
            SEntMan.System<SharedZLevelBallisticSystem>().TryStartTrajectory(projectile, target, direction),
            Is.True);
    }

    private Vector2 GetMapDirection(EntityUid source, EntityUid target)
    {
        var transform = SEntMan.System<SharedTransformSystem>();
        return transform.GetMapCoordinates(target).Position - transform.GetMapCoordinates(source).Position;
    }

    private async Task<HashSet<int>> ObserveProjectileUntilSpent(EntityUid projectile, int maximumTicks = 120)
    {
        var observedLevels = new HashSet<int> { 2 };
        var spent = false;
        var hadTrajectory = true;
        var lastPosition = Vector2.Zero;
        var lastVelocity = Vector2.Zero;
        var lastMetrics = default(ZLevelMetricsSnapshot);
        for (var i = 0; i < maximumTicks; i++)
        {
            await RunTicksSync(1);
            await Server.WaitAssertion(() =>
            {
                Assert.That(SEntMan.Deleted(projectile), Is.False);
                observedLevels.Add(SEntMan.System<SharedZLevelSystem>().GetZLevel(projectile));
                spent = SEntMan.GetComponent<ProjectileComponent>(projectile).ProjectileSpent;
                hadTrajectory = SEntMan.HasComponent<ZLevelBallisticTrajectoryComponent>(projectile);
                lastPosition = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(projectile).Position;
                var body = SEntMan.GetComponent<PhysicsComponent>(projectile);
                lastVelocity = SEntMan.System<SharedPhysicsSystem>()
                    .GetMapLinearVelocity(projectile, component: body);
                lastMetrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            });
            if (spent)
                break;
        }

        Assert.That(
            spent,
            Is.True,
            $"Projectile did not become spent within {maximumTicks} ticks: " +
            $"levels=[{string.Join(",", observedLevels.Order())}], position={lastPosition}, " +
            $"velocity={lastVelocity}, trajectory={hadTrajectory}, " +
            $"completed={lastMetrics.BallisticRoutesCompleted}, " +
            $"closed={lastMetrics.BallisticClosedBoundaries}, " +
            $"collision={lastMetrics.BallisticCollisionCancellations}, " +
            $"invalid={lastMetrics.BallisticInvalidCancellations}.");
        return observedLevels;
    }

    private void CloseBoundary(
        TestMapData testMap,
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels channels)
    {
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var blocker = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(blocker, lowerLocalZ), Is.True);
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
