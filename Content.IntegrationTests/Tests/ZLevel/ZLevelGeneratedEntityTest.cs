// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Content.Shared.Stacks;
using Content.Shared.Destructible.Thresholds;
using Content.Shared.EntityEffects;
using Content.Shared.EntityEffects.Effects.EntitySpawning;
using Content.Shared.Trigger.Systems;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Spawners;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelGeneratedEntityTest : GameTest
{
    private const int FrameOrigin = 5;
    private const int LocalZ = 1;
    private const int WorldZ = FrameOrigin + LocalZ;

    private const string DestructionOutput = "ZLevelGeneratedDestructionOutput";
    private const string DespawnOutput = "ZLevelGeneratedDespawnOutput";
    private const string EffectOutput = "ZLevelGeneratedEffectOutput";
    private const string PredictedEffectOutput = "ZLevelGeneratedPredictedEffectOutput";
    private const string ContainerEffectOutput = "ZLevelGeneratedContainerEffectOutput";
    private const string ContainerDropOutput = "ZLevelGeneratedContainerDropOutput";
    private const string TriggerMapOutput = "ZLevelGeneratedTriggerMapOutput";
    private const string TriggerAttachedOutput = "ZLevelGeneratedTriggerAttachedOutput";
    private const string TriggerTableOutput = "ZLevelGeneratedTriggerTableOutput";
    private const string ScatterOutput = "ZLevelGeneratedScatterOutput";
    private const string ProjectileOutput = "ZLevelGeneratedProjectileOutput";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelGeneratedDestructionOutput

- type: entity
  id: ZLevelGeneratedDespawnOutput

- type: entity
  id: ZLevelGeneratedEffectOutput

- type: entity
  id: ZLevelGeneratedPredictedEffectOutput

- type: entity
  id: ZLevelGeneratedContainerEffectOutput

- type: entity
  id: ZLevelGeneratedContainerDropOutput

- type: entity
  id: ZLevelGeneratedTriggerMapOutput

- type: entity
  id: ZLevelGeneratedTriggerAttachedOutput

- type: entity
  id: ZLevelGeneratedTriggerTableOutput

- type: entity
  id: ZLevelGeneratedScatterOutput

- type: entity
  id: ZLevelGeneratedProjectileOutput
  parent: BulletPistol

- type: entity
  id: ZLevelGeneratedDespawnSource
  components:
  - type: SpawnOnDespawn
    prototype: ZLevelGeneratedDespawnOutput

- type: entity
  id: ZLevelGeneratedTriggerMapSource
  components:
  - type: SpawnOnTrigger
    proto: ZLevelGeneratedTriggerMapOutput
    useMapCoords: true
    predicted: false

- type: entity
  id: ZLevelGeneratedTriggerAttachedSource
  components:
  - type: SpawnOnTrigger
    proto: ZLevelGeneratedTriggerAttachedOutput
    useMapCoords: false
    predicted: true

- type: entity
  id: ZLevelGeneratedTriggerTableSource
  components:
  - type: SpawnEntityTableOnTrigger
    useMapCoords: true
    predicted: false
    table: !type:AllSelector
      children:
      - id: ZLevelGeneratedTriggerTableOutput

- type: entity
  id: ZLevelGeneratedScatterSource
  components:
  - type: ScatteringGrenade
    fillPrototype: ZLevelGeneratedScatterOutput
    capacity: 1
    triggerContents: false

- type: entity
  id: ZLevelGeneratedProjectileSource
  components:
  - type: ProjectileGrenade
    fillPrototype: ZLevelGeneratedProjectileOutput
    capacity: 1
    minVelocity: 0
    maxVelocity: 0
";

    [Test]
    public async Task DestructionDebrisInheritsSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap);
            var behavior = new SpawnEntitiesBehavior
            {
                Spawn =
                {
                    [DestructionOutput] = new MinMax { Min = 1, Max = 1 },
                },
            };

            behavior.Execute(source, SEntMan.System<DestructibleSystem>());

            AssertOutputFloor(testMap, DestructionOutput);
        });
    }

    [Test]
    public async Task TimedDespawnReplacementInheritsSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap, "ZLevelGeneratedDespawnSource");
            var despawn = new TimedDespawnEvent();
            SEntMan.EventBus.RaiseLocalEvent(source, ref despawn);

            AssertOutputFloor(testMap, DespawnOutput);
        });
    }

    [Test]
    public async Task EntityEffectsInheritSourceWorldZAcrossSpawnModes()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap);
            var effects = SEntMan.System<SharedEntityEffectsSystem>();

            effects.ApplyEffect(source, new SpawnEntity
            {
                Entity = EffectOutput,
                Predicted = false,
            });
            effects.ApplyEffect(source, new SpawnEntity
            {
                Entity = PredictedEffectOutput,
                Predicted = true,
            });

            var containers = SEntMan.System<SharedContainerSystem>();
            containers.EnsureContainer<Container>(source, "zlevel-test-container");
            effects.ApplyEffect(source, new SpawnEntityInContainerOrDrop
            {
                Entity = ContainerEffectOutput,
                ContainerName = "zlevel-test-container",
                Predicted = false,
            });
            var dropSource = SpawnAtTestFloor(testMap);
            SEntMan.EnsureComponent<ContainerManagerComponent>(dropSource);
            effects.ApplyEffect(dropSource, new SpawnEntityInContainerOrDrop
            {
                Entity = ContainerDropOutput,
                ContainerName = "missing-zlevel-test-container",
                Predicted = false,
            });

            AssertOutputFloor(testMap, EffectOutput);
            AssertOutputFloor(testMap, PredictedEffectOutput);
            var contained = AssertOutputFloor(testMap, ContainerEffectOutput);
            var dropped = AssertOutputFloor(testMap, ContainerDropOutput);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(contained), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(dropped), Is.True);
            });

            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(source, 0), Is.True);
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(contained),
                Is.EqualTo(FrameOrigin));
        });
    }

    [Test]
    public async Task PredictedStackSplitInheritsSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap, "SheetSteel");
            var split = SEntMan.System<SharedStackSystem>().GetOne((source, null));

            Assert.That(split, Is.Not.EqualTo(source));
            AssertEntityFloor(split);
        });
    }

    [Test]
    public async Task TriggerOutputsInheritSourceWorldZAcrossCoordinateModes()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var mapSource = SpawnAtTestFloor(testMap, "ZLevelGeneratedTriggerMapSource");
            var attachedSource = SpawnAtTestFloor(testMap, "ZLevelGeneratedTriggerAttachedSource");
            var tableSource = SpawnAtTestFloor(testMap, "ZLevelGeneratedTriggerTableSource");
            var triggers = SEntMan.System<TriggerSystem>();

            triggers.Trigger(mapSource, predicted: false);
            triggers.Trigger(attachedSource, predicted: true);
            triggers.Trigger(tableSource, predicted: false);

            AssertOutputFloor(testMap, TriggerMapOutput);
            AssertOutputFloor(testMap, TriggerAttachedOutput);
            AssertOutputFloor(testMap, TriggerTableOutput);
        });
    }

    [Test]
    public async Task ScatteringGrenadePayloadInheritsSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap, "ZLevelGeneratedScatterSource");
            SEntMan.System<TriggerSystem>().Trigger(source, key: "timer", predicted: false);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() => AssertOutputFloor(testMap, ScatterOutput));
    }

    [Test]
    public async Task ProjectileGrenadePayloadInheritsSourceWorldZ()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var source = SpawnAtTestFloor(testMap, "ZLevelGeneratedProjectileSource");
            SEntMan.System<TriggerSystem>().Trigger(source, key: "timer", predicted: false);

            AssertOutputFloor(testMap, ProjectileOutput);
        });
    }

    private void Configure(TestMapData testMap)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            2,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        Assert.That(
            SEntMan.System<SharedTransformSystem>().SetZLevelFrameOrigin(testMap.Grid, FrameOrigin),
            Is.True);
    }

    private EntityUid SpawnAtTestFloor(TestMapData testMap, string? prototype = null)
    {
        var entity = SEntMan.SpawnEntity(
            prototype,
            new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, LocalZ), Is.True);
        return entity;
    }

    private EntityUid AssertOutputFloor(TestMapData testMap, string prototype)
    {
        var output = FindPrototypeOnMap(testMap, prototype);
        AssertEntityFloor(output);
        return output;
    }

    private void AssertEntityFloor(EntityUid entity)
    {
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        Assert.Multiple(() =>
        {
            Assert.That(zLevels.GetZLevel(entity), Is.EqualTo(LocalZ));
            Assert.That(zLevels.GetWorldZLevel(entity), Is.EqualTo(WorldZ));
        });
    }

    private EntityUid FindPrototypeOnMap(TestMapData testMap, string prototype)
    {
        EntityUid? found = null;
        var query = SEntMan.EntityQueryEnumerator<MetaDataComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var metadata, out var transform))
        {
            if (metadata.EntityPrototype?.ID != prototype || transform.MapID != testMap.MapId)
                continue;

            Assert.That(found, Is.Null, $"Expected one {prototype} on the test map.");
            found = uid;
        }

        Assert.That(found, Is.Not.Null, $"Expected {prototype} to be spawned on the test map.");
        return found!.Value;
    }
}
