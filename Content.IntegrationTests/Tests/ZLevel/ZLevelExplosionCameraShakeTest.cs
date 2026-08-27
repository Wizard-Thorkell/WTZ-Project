// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelExplosionCameraShakeTest : GameTest
{
    private const int FrameOrigin = 5;
    private const string ExplosionPrototype = "ZLevelCameraShakeExplosion";

    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [TestPrototypes]
    private const string Prototypes = @"
- type: explosion
  id: ZLevelCameraShakeExplosion
  damagePerIntensity:
    types:
      Structural: 0
  tileBreakChance: [0]
  tileBreakIntensity: [0]
";

    [Test]
    public async Task ShakeOnlyReachesPlayersOnExplosionTopologyFloors()
    {
        var testMap = await Pair.CreateTestMap();
        EntityUid player = default;

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession;
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Status, Is.EqualTo(SessionStatus.InGame));
            Assert.That(session.AttachedEntity, Is.Not.Null);
            player = session.AttachedEntity!.Value;

            Configure(testMap);
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetCoordinates(
                player,
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(player, 1), Is.True);

            var source = SpawnAtFloor(testMap, 0);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            QueueExplosion(source);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.ExplosionCameraShakeCandidates, Is.GreaterThanOrEqualTo(1));
                Assert.That(metrics.ExplosionCameraShakesApplied, Is.Zero);
                Assert.That(
                    metrics.ExplosionCameraShakesWorldZRejected,
                    Is.EqualTo(metrics.ExplosionCameraShakeCandidates));
            });

            OpenExplosionBoundary(testMap, Vector2i.Zero, 0);
            var secondSource = SpawnAtFloor(testMap, 0);
            SEntMan.System<SharedZLevelMetricsSystem>().ResetCounters();
            QueueExplosion(secondSource);
        });

        await RunTicksSync(20);

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.ExplosionCameraShakeCandidates, Is.GreaterThanOrEqualTo(1));
                Assert.That(metrics.ExplosionCameraShakesApplied, Is.GreaterThanOrEqualTo(1));
                Assert.That(metrics.ExplosionCameraShakesWorldZRejected, Is.Zero);
            });
        });
    }

    private void Configure(TestMapData testMap)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        var transform = SEntMan.System<SharedTransformSystem>();
        Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);

        var definitions = Server.ResolveDependency<ITileDefinitionManager>();
        var floor = (ContentTileDefinition) definitions["FloorSteel"];
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        for (var z = 0; z <= 1; z++)
        {
            for (var x = -2; x <= 2; x++)
            {
                for (var y = -2; y <= 2; y++)
                {
                    map.SetZLevelTile(
                        testMap.Grid,
                        grid,
                        new ZLevelTileIndices(x, y, z),
                        new Tile(floor.TileId));
                }
            }
        }
    }

    private EntityUid SpawnAtFloor(TestMapData testMap, int localZ)
    {
        var entity = SEntMan.SpawnEntity(
            null,
            new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, localZ), Is.True);
        return entity;
    }

    private void OpenExplosionBoundary(TestMapData testMap, Vector2i tile, int lowerLocalZ)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary(
            (provider, boundary),
            true,
            1,
            ZLevelBoundaryChannels.Explosion,
            ZLevelBoundaryChannels.None);
        transform.AnchorEntity(provider, SEntMan.GetComponent<TransformComponent>(provider));
    }

    private void QueueExplosion(EntityUid source)
    {
        SEntMan.System<ExplosionSystem>().QueueExplosion(
            source,
            ExplosionPrototype,
            totalIntensity: 12f,
            slope: 4f,
            maxTileIntensity: 6f,
            tileBreakScale: 1f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);
    }
}
