// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelLightingCacheTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelLightingCacheTestLight
  components:
  - type: PointLight
    enabled: true
    radius: 4
    energy: 2
    color: '#40A0FFFF'
";

    [Test]
    public async Task AperturesCacheAndInvalidateByChunkLayerPolicyAndLifecycle()
    {
        var testMap = await Pair.CreateTestMap();
        var closedTile = new Vector2i(1, 1);
        var editedTile = new Vector2i(2, 2);
        var neighboringTile = new Vector2i(17, 1);
        var negativeTile = new Vector2i(-1, -1);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            grid.CanSplit = false;
            format.Configure(testMap.MapUid, -2, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetTile(testMap.Grid, grid, closedTile, new Tile(1));
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(closedTile.X, closedTile.Y, 1), new Tile(1));
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(neighboringTile.X, neighboringTile.Y, 1), new Tile(1));
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(negativeTile.X, negativeTile.Y, -1), new Tile(1));
        });
        await Pair.RunUntilSynced();

        long firstRevision = 0;
        long neighboringRevision = 0;
        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            var originChunk = Vector2i.Zero;
            var neighboringChunk = new Vector2i(1, 0);
            var negativeChunk = new Vector2i(-1, -1);

            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                originChunk,
                0,
                out var first), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(first.IsOpen(closedTile), Is.False);
                Assert.That(first.IsOpen(editedTile), Is.True);
                Assert.That(first.OpenCount, Is.EqualTo(ZLevelApertureChunk.TileCount - 1));
            });

            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                originChunk,
                0,
                out var hot), Is.True);
            Assert.That(hot.Revision, Is.EqualTo(first.Revision));
            firstRevision = first.Revision;

            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                neighboringChunk,
                0,
                out var neighboring), Is.True);
            Assert.That(neighboring.IsOpen(neighboringTile), Is.False);
            neighboringRevision = neighboring.Revision;

            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                negativeChunk,
                -2,
                out var negative), Is.True);
            Assert.That(negative.IsOpen(negativeTile), Is.False);

            var metrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.ApertureQueries, Is.EqualTo(4));
                Assert.That(metrics.ApertureCacheHits, Is.EqualTo(1));
                Assert.That(metrics.ApertureCacheMisses, Is.EqualTo(3));
                Assert.That(metrics.ApertureBuilds, Is.EqualTo(3));
                Assert.That(metrics.ApertureBuildTileChecks,
                    Is.EqualTo(3 * ZLevelApertureChunk.TileCount));
                Assert.That(metrics.CachedApertureChunks, Is.EqualTo(3));
            });

            QueryApertureRepeated(cache, (testMap.CGridUid, grid), editedTile, 0, 2_048);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var remainedOpen = QueryApertureRepeated(
                cache,
                (testMap.CGridUid, grid),
                editedTile,
                0,
                1_000);

            Assert.That(remainedOpen, Is.True);
            Assert.That(
                GC.GetAllocatedBytesForCurrentThread() - before,
                Is.LessThanOrEqualTo(512),
                "The integration harness may pay one fixed OSR bookkeeping allocation, but lookup count must not scale it.");
        });

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(editedTile.X, editedTile.Y, 1), new Tile(1));
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            Assert.That(cache.CachedApertureChunkCount, Is.EqualTo(2),
                "Only the edited chunk/lower-layer entry should be invalidated.");
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                new Vector2i(1, 0),
                0,
                out var neighboring), Is.True);
            Assert.That(neighboring.Revision, Is.EqualTo(neighboringRevision));
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                Vector2i.Zero,
                0,
                out var rebuilt), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(rebuilt.Revision, Is.GreaterThan(firstRevision));
                Assert.That(rebuilt.IsOpen(editedTile), Is.False);
                Assert.That(cache.Snapshot().ApertureInvalidatedChunks, Is.EqualTo(1));
            });
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                -2,
                1,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            Assert.That(cache.CachedApertureChunkCount, Is.Zero);
            Assert.That(cache.IsApertureOpen((testMap.CGridUid, grid), closedTile, 0), Is.True);
            Assert.That(cache.IsApertureOpen((testMap.CGridUid, grid), editedTile, 0), Is.True);
        });

        EntityUid marker = default;
        NetEntity markerNet = default;
        await Server.WaitAssertion(() =>
        {
            marker = SEntMan.SpawnEntity(
                "ZLevelSealedBoundaryMarker",
                new EntityCoordinates(testMap.Grid, closedTile + new Vector2(0.5f, 0.5f)));
            Assert.That(SEntMan.GetComponent<TransformComponent>(marker).Anchored, Is.True);
            markerNet = SEntMan.GetNetEntity(marker);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var boundaries = CEntMan.System<SharedZLevelBoundarySystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            Assert.That(CEntMan.TryGetEntity(markerNet, out var clientMarker), Is.True);
            Assert.That(clientMarker, Is.Not.Null);
            Assert.That(CEntMan.HasComponent<ZLevelBoundaryComponent>(clientMarker.Value), Is.True);
            Assert.That(CEntMan.GetComponent<TransformComponent>(clientMarker.Value).Anchored, Is.True);
            Assert.That(CEntMan.System<SharedZLevelSystem>().GetZLevel(clientMarker.Value), Is.Zero);
            Assert.That(boundaries.CachedBoundaryCount, Is.EqualTo(ZLevelApertureChunk.TileCount - 1));
            Assert.That(cache.CachedApertureChunkCount, Is.Zero);
            Assert.That(cache.IsApertureOpen((testMap.CGridUid, grid), closedTile, 0), Is.False);
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(marker));
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            Assert.That(cache.CachedApertureChunkCount, Is.Zero);
            Assert.That(cache.IsApertureOpen((testMap.CGridUid, grid), closedTile, 0), Is.True);
        });

        NetEntity auxiliaryGrid = default;
        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var grid = mapManager.CreateGridEntity(testMap.MapId);
            grid.Comp.CanSplit = false;
            map.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            auxiliaryGrid = SEntMan.GetNetEntity(grid.Owner);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var gridUid = CEntMan.GetEntity(auxiliaryGrid);
            var grid = CEntMan.GetComponent<MapGridComponent>(gridUid);
            Assert.That(cache.TryGetApertureChunk((gridUid, grid), Vector2i.Zero, 0, out _), Is.True);
            Assert.That(cache.CachedApertureChunkCount, Is.EqualTo(2));
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(SEntMan.GetEntity(auxiliaryGrid)));
        await Pair.RunUntilSynced();
        await Client.WaitAssertion(() => Assert.That(
            CEntMan.System<ZLevelLightingCacheSystem>().CachedApertureChunkCount,
            Is.EqualTo(1)));
    }

    [Test]
    public async Task EmitterIndexUsesWorldZTracksMovingFramesAndReusesBuffers()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity lowerLight = default;
        NetEntity upperLight = default;

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            transform.SetZLevelFrameOrigin(testMap.Grid, 5);
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -3f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(15));

            var coordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));
            var lower = SEntMan.SpawnEntity("ZLevelLightingCacheTestLight", coordinates);
            var upper = SEntMan.SpawnEntity("ZLevelLightingCacheTestLight", coordinates);
            zLevels.SetZLevelPosition(lower, 0);
            zLevels.SetZLevelPosition(upper, 1);
            lowerLight = SEntMan.GetNetEntity(lower);
            upperLight = SEntMan.GetNetEntity(upper);
        });
        await Pair.RunTicksSync(3);

        Vector2 originalPosition = default;
        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var clientLower = CEntMan.GetEntity(lowerLight);
            var clientUpper = CEntMan.GetEntity(upperLight);
            originalPosition = transform.GetWorldPosition(clientUpper);
            var bounds = Box2.CenteredAround(originalPosition, new Vector2(12f, 12f));
            var emitters = new List<ZLevelLightEmitter>(4);

            cache.ResetMetrics();
            Assert.That(cache.QueryEmitters(testMap.MapId, bounds, 6, 6, emitters), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(emitters[0].Uid, Is.EqualTo(clientUpper));
                Assert.That(emitters[0].WorldZ, Is.EqualTo(6));
                Assert.That(Vector2.Distance(emitters[0].WorldPosition, originalPosition), Is.LessThan(0.001f));
                Assert.That(emitters[0].Color, Is.EqualTo(Color.FromHex("#40A0FFFF")));
            });

            emitters.Clear();
            Assert.That(cache.QueryEmitters(testMap.MapId, bounds, 5, 6, emitters), Is.EqualTo(2));
            Assert.That(emitters.Select(emitter => emitter.Uid), Is.EquivalentTo(new[] { clientLower, clientUpper }));

            var metrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.EmitterQueries, Is.EqualTo(2));
                Assert.That(metrics.EmitterCandidates, Is.GreaterThanOrEqualTo(4));
                Assert.That(metrics.EmitterAccepted, Is.EqualTo(3));
                Assert.That(metrics.EmitterWorldZRejected, Is.GreaterThanOrEqualTo(1));
            });

            QueryEmittersRepeated(cache, testMap.MapId, bounds, 5, 6, emitters, 512);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var accepted = QueryEmittersRepeated(cache, testMap.MapId, bounds, 5, 6, emitters, 100);

            Assert.That(accepted, Is.EqualTo(200));
            Assert.That(
                GC.GetAllocatedBytesForCurrentThread() - before,
                Is.LessThanOrEqualTo(512),
                "The integration harness may pay one fixed OSR bookkeeping allocation, but query count must not scale it.");
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedTransformSystem>().SetLocalPosition(testMap.Grid, new Vector2(28f, -3f));
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var clientUpper = CEntMan.GetEntity(upperLight);
            var movedPosition = transform.GetWorldPosition(clientUpper);
            var emitters = new List<ZLevelLightEmitter>(4);

            Assert.That(cache.QueryEmitters(
                testMap.MapId,
                Box2.CenteredAround(originalPosition, new Vector2(4f, 4f)),
                5,
                6,
                emitters), Is.Zero);
            emitters.Clear();
            Assert.That(cache.QueryEmitters(
                testMap.MapId,
                Box2.CenteredAround(movedPosition, new Vector2(12f, 12f)),
                5,
                6,
                emitters), Is.EqualTo(2));
            Assert.That(movedPosition.X - originalPosition.X, Is.EqualTo(20f).Within(0.001f));
        });
    }

    [TestCase(3)]
    [TestCase(6)]
    [TestCase(10)]
    public async Task CacheWorkloadScalesWithAuthoredFloorCount(int floorCount)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            grid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, floorCount - 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);

            for (var z = 1; z < floorCount; z++)
            {
                for (var y = 0; y < ZLevelApertureChunk.ChunkSize; y++)
                {
                    for (var x = 0; x < ZLevelApertureChunk.ChunkSize; x++)
                    {
                        if ((x + y + z) % 5 == 0)
                            continue;

                        map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, y, z), new Tile(1));
                    }
                }
            }

            for (var z = 0; z < floorCount; z++)
            {
                var light = SEntMan.SpawnEntity(
                    "ZLevelLightingCacheTestLight",
                    new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
                zLevels.SetZLevelPosition(light, z);
            }
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();

            var coldStarted = Stopwatch.GetTimestamp();
            for (var lowerZ = 0; lowerZ < floorCount - 1; lowerZ++)
            {
                Assert.That(cache.TryGetApertureChunk(
                    (testMap.CGridUid, grid),
                    Vector2i.Zero,
                    lowerZ,
                    out var aperture), Is.True);
                Assert.That(aperture.OpenCount, Is.InRange(1, ZLevelApertureChunk.TileCount - 1));
            }

            var coldElapsed = Stopwatch.GetTimestamp() - coldStarted;
            var coldMetrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(coldMetrics.ApertureBuilds, Is.EqualTo(floorCount - 1));
                Assert.That(coldMetrics.ApertureBuildTileChecks,
                    Is.EqualTo((floorCount - 1) * ZLevelApertureChunk.TileCount));
                Assert.That(coldMetrics.CachedApertureChunks, Is.EqualTo(floorCount - 1));
            });

            QueryAllApertureLayers(cache, (testMap.CGridUid, grid), floorCount, 512);
            var apertureAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var apertureQueries = QueryAllApertureLayers(
                cache,
                (testMap.CGridUid, grid),
                floorCount,
                100);
            var apertureAllocated = GC.GetAllocatedBytesForCurrentThread() - apertureAllocatedBefore;

            var worldPosition = transform.GetWorldPosition(testMap.CGridUid) + new Vector2(0.5f, 0.5f);
            var bounds = Box2.CenteredAround(worldPosition, new Vector2(12f, 12f));
            var emitters = new List<ZLevelLightEmitter>(floorCount);
            Assert.That(cache.QueryEmitters(
                testMap.MapId,
                bounds,
                0,
                floorCount - 1,
                emitters), Is.EqualTo(floorCount));
            QueryEmittersRepeated(cache, testMap.MapId, bounds, 0, floorCount - 1, emitters, 512);
            var emitterAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var accepted = QueryEmittersRepeated(
                cache,
                testMap.MapId,
                bounds,
                0,
                floorCount - 1,
                emitters,
                100);
            var emitterAllocated = GC.GetAllocatedBytesForCurrentThread() - emitterAllocatedBefore;

            Assert.Multiple(() =>
            {
                Assert.That(apertureQueries, Is.EqualTo((floorCount - 1) * 100));
                Assert.That(apertureAllocated, Is.LessThanOrEqualTo(512));
                Assert.That(accepted, Is.EqualTo(floorCount * 100));
                Assert.That(emitterAllocated, Is.LessThanOrEqualTo(512));
            });

            TestContext.Progress.WriteLine(
                $"WTZ P3.2 cache scale: floors={floorCount}, chunks={floorCount - 1}, " +
                $"coldMs={coldElapsed * 1000d / Stopwatch.Frequency:0.000}, " +
                $"buildMs={coldMetrics.ApertureBuildMilliseconds:0.000}, " +
                $"hotApertureBytes={apertureAllocated}, hotEmitterBytes={emitterAllocated}");
        });
    }

    private static bool QueryApertureRepeated(
        ZLevelLightingCacheSystem cache,
        Entity<MapGridComponent> grid,
        Vector2i tile,
        int lowerLocalZ,
        int count)
    {
        var open = true;
        for (var i = 0; i < count; i++)
        {
            open &= cache.IsApertureOpen(grid, tile, lowerLocalZ);
        }

        return open;
    }

    private static int QueryEmittersRepeated(
        ZLevelLightingCacheSystem cache,
        MapId mapId,
        Box2 bounds,
        int minimumWorldZ,
        int maximumWorldZ,
        List<ZLevelLightEmitter> emitters,
        int count)
    {
        var accepted = 0;
        for (var i = 0; i < count; i++)
        {
            emitters.Clear();
            accepted += cache.QueryEmitters(mapId, bounds, minimumWorldZ, maximumWorldZ, emitters);
        }

        return accepted;
    }

    private static int QueryAllApertureLayers(
        ZLevelLightingCacheSystem cache,
        Entity<MapGridComponent> grid,
        int floorCount,
        int count)
    {
        var queries = 0;
        for (var i = 0; i < count; i++)
        {
            for (var lowerZ = 0; lowerZ < floorCount - 1; lowerZ++)
            {
                _ = cache.IsApertureOpen(grid, Vector2i.Zero, lowerZ);
                queries++;
            }
        }

        return queries;
    }
}
