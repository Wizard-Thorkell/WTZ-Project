// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable
#pragma warning disable CS0618 // Numeric damage is used only as a deterministic test signal.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Damage.Systems;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelExplosionTopologyTest : GameTest
{
    private const int FrameOrigin = 5;
    private const string TargetPrototype = "ZLevelExplosionDamageTarget";
    private const string AirtightWallPrototype = "ZLevelExplosionAirtightWall";
    private const string DamageExplosion = "ZLevelExplosionDamage";
    private const string TileExplosion = "ZLevelExplosionTileBreaker";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelExplosionDamageTarget
  components:
  - type: Damageable
    damageContainer: StructuralInorganic
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      target:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.35,-0.35,0.35,0.35""
        layer:
        - WallLayer
        hard: true

- type: explosion
  id: ZLevelExplosionDamage
  damagePerIntensity:
    types:
      Blunt: 5
      Heat: 5
      Piercing: 5
  tileBreakChance: [0]
  tileBreakIntensity: [0]

- type: explosion
  id: ZLevelExplosionTileBreaker
  damagePerIntensity:
    types:
      Structural: 0
  tileBreakChance: [1]
  tileBreakIntensity: [0]

- type: entity
  id: ZLevelExplosionAirtightWall
  parent: WallSolid
  components:
  - type: Airtight
    resistance: 1000
";

    [Test]
    public async Task ClosedDeckDamagesOnlySourceFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sourceFloor = default;
        EntityUid overlappingLowerFloor = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            sourceFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            overlappingLowerFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            QueueExplosion(sourceFloor, DamageExplosion);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(sourceFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(overlappingLowerFloor), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task ExplosionChannelOpeningPropagatesToAdjacentFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sourceFloor = default;
        EntityUid destinationFloor = default;
        EntityUid sealedFloor = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 2, transformed: true);
            sourceFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            destinationFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            sealedFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 2);
            SetBoundary(testMap, Vector2i.Zero, 0, opens: ZLevelBoundaryChannels.Explosion);
            QueueExplosion(sourceFloor, DamageExplosion);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(sourceFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(destinationFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(sealedFloor), Is.EqualTo(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task UnrelatedOpeningDoesNotPropagateExplosion()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sourceFloor = default;
        EntityUid destinationFloor = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            sourceFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            destinationFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            SetBoundary(testMap, Vector2i.Zero, 0, opens: ZLevelBoundaryChannels.Projectile);
            QueueExplosion(sourceFloor, DamageExplosion);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.That(damage.GetTotalDamage(sourceFloor), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(damage.GetTotalDamage(destinationFloor), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task ExplicitExplosionClosureStopsPropagation()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sourceFloor = default;
        EntityUid destinationFloor = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.ExplicitOnly, 0, 1);
            sourceFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            destinationFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            SetBoundary(testMap, Vector2i.Zero, 0, closes: ZLevelBoundaryChannels.Explosion);
            QueueExplosion(sourceFloor, DamageExplosion);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.That(damage.GetTotalDamage(sourceFloor), Is.GreaterThan(FixedPoint2.Zero));
            Assert.That(damage.GetTotalDamage(destinationFloor), Is.EqualTo(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task QueuedExplosionsOnDifferentFloorsDoNotCombine()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid lower = default;
        EntityUid upper = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            lower = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            upper = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            QueueExplosion(lower, DamageExplosion);
            QueueExplosion(upper, DamageExplosion);
        });

        await RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(lower), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(upper), Is.GreaterThan(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task UpperFloorTileDamageDoesNotMutateBaseDeck()
    {
        var testMap = await Pair.CreateTestMap();
        Tile lowerBefore = default;
        Tile upperBefore = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            lowerBefore = map.GetZLevelTileRef(testMap.Grid, grid, new ZLevelTileIndices(0, 0, 0)).Tile;
            upperBefore = map.GetZLevelTileRef(testMap.Grid, grid, new ZLevelTileIndices(0, 0, 1)).Tile;
            var lowerBlocker = Spawn(testMap, "WallSolid", new Vector2(0.5f, 0.5f), 0);
            var transform = SEntMan.System<SharedTransformSystem>();
            var blockerTransform = SEntMan.GetComponent<TransformComponent>(lowerBlocker);
            if (!blockerTransform.Anchored)
                transform.AnchorEntity(lowerBlocker, blockerTransform);
            Assert.That(blockerTransform.Anchored, Is.True);
            var source = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            QueueExplosion(source, TileExplosion, maxTileBreak: 1);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var lowerAfter = map.GetZLevelTileRef(testMap.Grid, grid, new ZLevelTileIndices(0, 0, 0)).Tile;
            var upperAfter = map.GetZLevelTileRef(testMap.Grid, grid, new ZLevelTileIndices(0, 0, 1)).Tile;
            Assert.Multiple(() =>
            {
                Assert.That(lowerAfter.TypeId, Is.EqualTo(lowerBefore.TypeId));
                Assert.That(upperAfter.TypeId, Is.Not.EqualTo(upperBefore.TypeId));
            });
        });
    }

    [Test]
    public async Task BaseAndUpperSealedFloorsHaveMatchingBlastDamage()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid lowerCenter = default;
        EntityUid lowerEdge = default;
        EntityUid upperCenter = default;
        EntityUid upperEdge = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 2);
            lowerCenter = Spawn(testMap, TargetPrototype, new Vector2(-1.5f, 0.5f), 0);
            lowerEdge = Spawn(testMap, TargetPrototype, new Vector2(-0.5f, 0.5f), 0);
            upperCenter = Spawn(testMap, TargetPrototype, new Vector2(1.5f, 0.5f), 1);
            upperEdge = Spawn(testMap, TargetPrototype, new Vector2(2.5f, 0.5f), 1);
            QueueExplosion(lowerCenter, DamageExplosion);
            QueueExplosion(upperCenter, DamageExplosion);
        });

        await RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(lowerCenter), Is.EqualTo(damage.GetTotalDamage(upperCenter)));
                Assert.That(damage.GetTotalDamage(lowerEdge), Is.EqualTo(damage.GetTotalDamage(upperEdge)));
                Assert.That(damage.GetTotalDamage(lowerCenter), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(lowerEdge), Is.GreaterThan(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task OverlappingAirtightWallDoesNotBlockOtherFloorWave()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid destination = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 2);
            for (var y = -3; y <= 3; y++)
            {
                var wall = Spawn(testMap, AirtightWallPrototype, new Vector2(0.5f, y + 0.5f), 0);
                Assert.That(SEntMan.GetComponent<TransformComponent>(wall).Anchored, Is.True);
            }

            var source = Spawn(testMap, null, new Vector2(-0.5f, 0.5f), 1);
            destination = Spawn(testMap, TargetPrototype, new Vector2(1.5f, 0.5f), 1);
            QueueExplosion(source, DamageExplosion, totalIntensity: 500f, maxTileIntensity: 10f);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<DamageableSystem>().GetTotalDamage(destination),
                Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task ExplicitMapExplosionUsesRequestedWorldFloor()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid lower = default;
        EntityUid upper = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1, transformed: true);
            lower = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            upper = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            var mapCoordinates = SEntMan.System<SharedTransformSystem>().GetMapCoordinates(upper);
            SEntMan.System<ExplosionSystem>().QueueExplosion(
                mapCoordinates,
                DamageExplosion,
                12f,
                4f,
                6f,
                upper,
                maxTileBreak: 0,
                canCreateVacuum: false,
                addLog: false,
                worldZ: FrameOrigin + 1,
                frameGrid: testMap.Grid);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(lower), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(upper), Is.GreaterThan(FixedPoint2.Zero));
            });
        });
    }

    [Test]
    public async Task VisualPayloadKeepsReachedGridLayersSeparate()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 2);
            var source = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            SetBoundary(testMap, Vector2i.Zero, 0, opens: ZLevelBoundaryChannels.Explosion);
            QueueExplosion(source, DamageExplosion);
        });

        await RunTicksSync(2);

        await Server.WaitAssertion(() =>
        {
            ExplosionVisualsComponent? visuals = null;
            var query = SEntMan.EntityQueryEnumerator<ExplosionVisualsComponent>();
            while (query.MoveNext(out _, out var candidate))
            {
                if (candidate.Epicenter.MapId == testMap.MapId && candidate.ExplosionType == DamageExplosion)
                {
                    visuals = candidate;
                    break;
                }
            }

            Assert.That(visuals, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(visuals!.EpicenterWorldZ, Is.EqualTo(FrameOrigin));
                Assert.That(visuals.Tiles.TryGetValue(testMap.Grid, out var layers), Is.True);
                Assert.That(layers!.ContainsKey(0), Is.True);
                Assert.That(layers.ContainsKey(1), Is.True);
                Assert.That(layers.ContainsKey(2), Is.False);
            });
        });
    }

    [Test]
    public async Task IterationBudgetExhaustionIsObservable()
    {
        await OverrideCVar(Side.Server, CCVars.ExplosionMaxIterations, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            var source = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 0);
            QueueExplosion(source, DamageExplosion, totalIntensity: 500f, maxTileIntensity: 10f);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.ExplosionTopologyBuilds, Is.EqualTo(1));
                Assert.That(metrics.ExplosionIterationBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.ExplosionAreaBudgetExhaustions, Is.Zero);
            });
        });
    }

    [Test]
    public async Task TwoOpenBoundariesReachThirdFloorInOrder()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid sourceFloor = default;
        EntityUid middleFloor = default;
        EntityUid destinationFloor = default;
        var topologyMetrics = default(ZLevelMetricsSnapshot);

        await Server.WaitAssertion(() =>
        {
            // Z 3 is an intact ceiling so only the two authored shaft boundaries are open.
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 3);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            sourceFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 0);
            middleFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            destinationFloor = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 2);
            SetBoundary(testMap, Vector2i.Zero, 0, opens: ZLevelBoundaryChannels.Explosion);
            SetBoundary(testMap, Vector2i.Zero, 1, opens: ZLevelBoundaryChannels.Explosion);
            // Horizontal rings on every reached floor share the explosion's global intensity budget.
            QueueExplosion(sourceFloor, DamageExplosion, totalIntensity: 500f, maxTileIntensity: 10f);
        });

        await RunTicksSync(30);

        await Server.WaitAssertion(() =>
        {
            var damage = SEntMan.System<DamageableSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            topologyMetrics = metrics;
            Assert.Multiple(() =>
            {
                Assert.That(damage.GetTotalDamage(sourceFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(middleFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(damage.GetTotalDamage(destinationFloor), Is.GreaterThan(FixedPoint2.Zero));
                Assert.That(metrics.ExplosionTopologyBuilds, Is.EqualTo(1));
                Assert.That(metrics.ExplosionGridLayers, Is.EqualTo(3));
                Assert.That(metrics.ExplosionTiles, Is.GreaterThan(0));
                Assert.That(metrics.ExplosionVerticalQueries, Is.GreaterThan(metrics.ExplosionVerticalTraces));
                Assert.That(metrics.ExplosionVerticalCacheHits, Is.GreaterThanOrEqualTo(2));
                Assert.That(metrics.ExplosionVerticalOpen, Is.EqualTo(2));
                Assert.That(
                    metrics.ExplosionVerticalTraces,
                    Is.EqualTo(
                        metrics.ExplosionVerticalOpen +
                        metrics.ExplosionVerticalClosed +
                        metrics.ExplosionVerticalRejected));
                Assert.That(metrics.ExplosionAreaBudgetExhaustions, Is.Zero);
                Assert.That(metrics.ExplosionIterationBudgetExhaustions, Is.Zero);
                Assert.That(metrics.ExplosionTopologyMilliseconds, Is.GreaterThanOrEqualTo(0d));
            });
        });

        TestContext.Progress.WriteLine(
            $"WTZ explosion topology baseline: layers={topologyMetrics.ExplosionGridLayers}, " +
            $"space={topologyMetrics.ExplosionSpaceLayers}, tiles={topologyMetrics.ExplosionTiles}, " +
            $"vertical={topologyMetrics.ExplosionVerticalQueries}/" +
            $"{topologyMetrics.ExplosionVerticalTraces}, " +
            $"cache-hits={topologyMetrics.ExplosionVerticalCacheHits}, " +
            $"elapsed={topologyMetrics.ExplosionTopologyMilliseconds:0.000}ms");
    }

    [Test]
    public async Task QueuedExplosionFollowsMovingGridFrame()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1, transformed: true);
            target = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            QueueExplosion(target, DamageExplosion);

            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(-12f, 9f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(-19));
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<DamageableSystem>().GetTotalDamage(target),
                Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    [Test]
    public async Task DeletedSourceRetainsCapturedFloorAndFrame()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid target = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            var source = Spawn(testMap, null, new Vector2(0.5f, 0.5f), 1);
            target = Spawn(testMap, TargetPrototype, new Vector2(0.5f, 0.5f), 1);
            QueueExplosion(source, DamageExplosion);
            SEntMan.DeleteEntity(source);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            Assert.That(
                SEntMan.System<DamageableSystem>().GetTotalDamage(target),
                Is.GreaterThan(FixedPoint2.Zero));
        });
    }

    private void Configure(
        TestMapData testMap,
        ZLevelDefaultBoundaryMode boundaryMode,
        int minZ,
        int maxZ,
        bool transformed = false)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(testMap.MapUid, minZ, maxZ, minZ, boundaryMode);
        var transform = SEntMan.System<SharedTransformSystem>();
        Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);
        if (transformed)
        {
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));
        }

        var definitions = Server.ResolveDependency<ITileDefinitionManager>();
        var floor = (ContentTileDefinition) definitions["FloorSteel"];
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        for (var z = minZ; z <= maxZ; z++)
        {
            for (var x = -3; x <= 3; x++)
            {
                for (var y = -3; y <= 3; y++)
                {
                    map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, y, z), new Tile(floor.TileId));
                }
            }
        }
    }

    private EntityUid Spawn(TestMapData testMap, string? prototype, Vector2 position, int localZ)
    {
        var entity = SEntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, position));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, localZ), Is.True);
        return entity;
    }

    private void QueueExplosion(
        EntityUid source,
        string prototype,
        int maxTileBreak = 0,
        float totalIntensity = 12f,
        float maxTileIntensity = 6f)
    {
        SEntMan.System<ExplosionSystem>().QueueExplosion(
            source,
            prototype,
            totalIntensity: totalIntensity,
            slope: 4f,
            maxTileIntensity: maxTileIntensity,
            tileBreakScale: 1f,
            maxTileBreak: maxTileBreak,
            canCreateVacuum: false,
            addLog: false);
    }

    private void SetBoundary(
        TestMapData testMap,
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels opens = ZLevelBoundaryChannels.None,
        ZLevelBoundaryChannels closes = ZLevelBoundaryChannels.None)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary((provider, boundary), true, 1, opens, closes);
        transform.AnchorEntity(provider, SEntMan.GetComponent<TransformComponent>(provider));
    }
}

#pragma warning restore CS0618
