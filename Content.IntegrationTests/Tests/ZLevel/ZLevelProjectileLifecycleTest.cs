// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelProjectileLifecycleTest : GameTest
{
    private const int FrameOrigin = 5;

    [TestPrototypes]
    private const string ProjectilePrototypes = @"
- type: entity
  parent: BaseBulletPractice
  id: ZLevelLifecycleProjectile

- type: entity
  id: ZLevelLifecycleProjectileTarget
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
        - BulletImpassable
        hard: true
";

    [Test]
    public async Task FiredProjectileUsesAuthoritativeSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, Vector2.Zero, 2);
            var projectile = Spawn(testMap, null, Vector2.Zero, 0);
            var guns = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();

            guns.ShootProjectile(
                projectile,
                Vector2.UnitX,
                Vector2.Zero,
                source,
                source,
                speed: 1f);

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetWorldZLevel(source), Is.EqualTo(7));
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(7));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(projectile).ZLevel, Is.EqualTo(2));
                Assert.That(SEntMan.HasComponent<ProjectileComponent>(projectile), Is.True);
            });
        });
    }

    [Test]
    public async Task SourceLessProjectilePreservesAuthoredWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var projectile = Spawn(testMap, null, Vector2.Zero, 2);
            var guns = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();

            guns.ShootProjectile(
                projectile,
                Vector2.UnitX,
                Vector2.Zero,
                gunUid: null,
                speed: 1f);

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(7));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(projectile).ZLevel, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task FiredProjectileCollidesOnlyOnAuthoritativeWorldZ()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid projectile = default;
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = Spawn(testMap, null, new Vector2(0.25f, 0.25f), 2);
            target = Spawn(testMap, "ZLevelLifecycleProjectileTarget", new Vector2(0.25f, 0.25f), 0);
            projectile = Spawn(testMap, "ZLevelLifecycleProjectile", new Vector2(0.25f, 0.25f), 0);
            var guns = SEntMan.System<Content.Server.Weapons.Ranged.Systems.GunSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();

            guns.ShootProjectile(
                projectile,
                Vector2.UnitX,
                Vector2.Zero,
                source,
                source,
                speed: 0f);

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(7));
                Assert.That(zLevels.GetWorldZLevel(target), Is.EqualTo(5));
            });
        });

        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(projectile), Is.False,
                "A projectile collided with an overlapping target on another World Z.");

            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var physics = SEntMan.System<SharedPhysicsSystem>();
            Assert.That(zLevels.SetZLevelPosition(target, 2), Is.True);
            physics.RegenerateContacts(target);
            physics.RegenerateContacts(projectile);
        });

        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(projectile), Is.True,
                "A projectile did not collide after the target moved onto its authoritative World Z.");
        });
    }

    [Test]
    public async Task ThrowUsesThrowerWorldZAndSourceLessThrowPreservesAuthoredZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var thrower = Spawn(testMap, null, Vector2.Zero, 2);
            var userThrown = SpawnDynamic(testMap, "Crowbar", Vector2.Zero, 0);
            var sourceLess = SpawnDynamic(testMap, "Crowbar", new Vector2(0.25f, 0.25f), 1);
            var throwing = SEntMan.System<ThrowingSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();

            Assert.That(zLevels.GetWorldZLevel(sourceLess), Is.EqualTo(6));

            throwing.TryThrow(
                userThrown,
                Vector2.UnitX,
                baseThrowSpeed: 1f,
                user: thrower,
                recoil: false,
                doSpin: false);
            throwing.TryThrow(
                sourceLess,
                Vector2.UnitX,
                baseThrowSpeed: 1f,
                recoil: false,
                doSpin: false);

            Assert.Multiple(() =>
            {
                Assert.That(zLevels.GetWorldZLevel(userThrown), Is.EqualTo(7));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(userThrown).ZLevel, Is.EqualTo(2));
                Assert.That(zLevels.GetWorldZLevel(sourceLess), Is.EqualTo(6));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(sourceLess).ZLevel, Is.EqualTo(1));
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(userThrown), Is.True);
                Assert.That(SEntMan.HasComponent<ThrownItemComponent>(sourceLess), Is.True);
            });
        });
    }

    [Test]
    public async Task EmbeddedProjectileInheritsTargetAndDetachPreservesWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var target = Spawn(testMap, "AirlockGlass", Vector2.Zero, 1);
            var projectile = Spawn(testMap, "SurvivalKnife", Vector2.Zero, 1);
            var projectiles = SEntMan.System<Content.Server.Projectiles.ProjectileSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var embedded = SEntMan.GetComponent<EmbeddableProjectileComponent>(projectile);
            var thrown = new ThrownItemComponent();
            var hit = new ThrowDoHitEvent(projectile, target, thrown);

            SEntMan.EventBus.RaiseLocalEvent(projectile, ref hit);

            Assert.Multiple(() =>
            {
                Assert.That(embedded.EmbeddedIntoUid, Is.EqualTo(target));
                Assert.That(SEntMan.GetComponent<TransformComponent>(projectile).ParentUid, Is.EqualTo(target));
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(projectile), Is.False);
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(6));
            });

            Assert.That(zLevels.SetZLevelPosition(target, 2), Is.True);
            Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(7));

            projectiles.EmbedDetach(projectile, embedded);

            Assert.Multiple(() =>
            {
                Assert.That(embedded.EmbeddedIntoUid, Is.Null);
                Assert.That(zLevels.GetWorldZLevel(projectile), Is.EqualTo(7));
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(projectile).ZLevel, Is.EqualTo(2));
                Assert.That(
                    SEntMan.GetComponent<TransformComponent>(projectile).GridUid,
                    Is.EqualTo((EntityUid?) testMap.Grid));
            });
        });
    }

    private void Configure(TestMapData testMap)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            2,
            0,
            ZLevelDefaultBoundaryMode.ExplicitOnly);
        Assert.That(
            SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, FrameOrigin),
            Is.True);
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

    private EntityUid SpawnDynamic(
        TestMapData testMap,
        string prototype,
        Vector2 position,
        int localZ)
    {
        var entity = Spawn(testMap, prototype, position, localZ);
        var physics = SEntMan.GetComponent<PhysicsComponent>(entity);
        SEntMan.System<SharedPhysicsSystem>().SetBodyType(entity, BodyType.Dynamic, body: physics);
        return entity;
    }
}
