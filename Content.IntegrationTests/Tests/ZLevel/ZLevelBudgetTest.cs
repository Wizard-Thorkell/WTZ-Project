// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.ZLevel.Systems;
using Content.Shared.CCVar;
using Content.Shared.Physics;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelBudgetTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task BoundaryCacheCapacityIsClampedAndFailsSoftByRecomputation()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelBoundaryCacheCapacity, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var firstTile = new Vector2i(800000, 800000);
            var chunkCount = grid.ChunkCount;

            Assert.That(
                boundaries.BoundaryCacheCapacity,
                Is.EqualTo(SharedZLevelBoundarySystem.MinimumBoundaryCacheCapacity));

            boundaries.InvalidateBoundary(testMap.Grid, firstTile, 0);
            metrics.ResetCounters();
            Assert.That(boundaries.TryGetBoundary(testMap.Grid, grid, firstTile, 0, 1, out var first), Is.True);

            for (var i = 0; i < boundaries.BoundaryCacheCapacity + 32; i++)
            {
                Assert.That(boundaries.TryGetBoundary(
                    testMap.Grid,
                    grid,
                    new Vector2i(firstTile.X + i + 1, firstTile.Y),
                    0,
                    1,
                    out _), Is.True);
            }

            Assert.That(boundaries.TryGetBoundary(testMap.Grid, grid, firstTile, 0, 1, out var recomputed), Is.True);
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(recomputed, Is.EqualTo(first));
                Assert.That(boundaries.CachedBoundaryCount, Is.LessThanOrEqualTo(boundaries.BoundaryCacheCapacity));
                Assert.That(snapshot.BoundaryCacheMisses,
                    Is.EqualTo(boundaries.BoundaryCacheCapacity + 34));
                Assert.That(snapshot.BoundaryEvictions, Is.GreaterThan(0));
                Assert.That(grid.ChunkCount, Is.EqualTo(chunkCount),
                    "Cache misses over empty space must not allocate map chunks.");
            });
        });
    }

    [Test]
    public async Task SkyExposureCacheCapacityIsClampedAndFailsSoftByRecomputation()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelSkyExposureCacheCapacity, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var first = new ZLevelTileIndices(900_000, 900_000, 0);
            var chunkCount = grid.ChunkCount;

            Assert.That(sky.CacheCapacity,
                Is.EqualTo(SharedZLevelSkyExposureSystem.MinimumCacheCapacity));

            sky.InvalidateAll();
            metrics.ResetCounters();
            var initial = sky.GetExposure((testMap.Grid, grid), first);
            Assert.That(initial.IsExposed, Is.True);

            for (var i = 0; i < sky.CacheCapacity + 32; i++)
            {
                var result = sky.GetExposure(
                    (testMap.Grid, grid),
                    new ZLevelTileIndices(first.X + i + 1, first.Y, 0));
                Assert.That(result.IsExposed, Is.True);
            }

            var recomputed = sky.GetExposure((testMap.Grid, grid), first);
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(recomputed, Is.EqualTo(initial));
                Assert.That(sky.CachedExposureCount, Is.LessThanOrEqualTo(sky.CacheCapacity));
                Assert.That(snapshot.SkyExposureCacheMisses, Is.EqualTo(sky.CacheCapacity + 34));
                Assert.That(snapshot.SkyExposureEvictions, Is.GreaterThan(0));
                Assert.That(grid.ChunkCount, Is.EqualTo(chunkCount),
                    "Sky cache misses over empty columns must not allocate map chunks.");
            });
        });
    }

    [Test]
    public async Task SkyExposureCacheEvictsLeastRecentlyUsedColumn()
    {
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelSkyExposureCacheCapacity,
            SharedZLevelSkyExposureSystem.MinimumCacheCapacity);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var first = new ZLevelTileIndices(1_000_000, 1_000_000, 0);
            var second = new ZLevelTileIndices(first.X + 1, first.Y, 0);

            sky.InvalidateAll();
            for (var i = 0; i < sky.CacheCapacity; i++)
            {
                sky.GetExposure(
                    (testMap.Grid, grid),
                    new ZLevelTileIndices(first.X + i, first.Y, 0));
            }

            metrics.ResetCounters();
            sky.GetExposure((testMap.Grid, grid), first);
            sky.GetExposure(
                (testMap.Grid, grid),
                new ZLevelTileIndices(first.X + sky.CacheCapacity, first.Y, 0));
            sky.GetExposure((testMap.Grid, grid), first);
            sky.GetExposure((testMap.Grid, grid), second);
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SkyExposureCacheHits, Is.EqualTo(2),
                    "Touching the oldest entry must protect it from the next eviction.");
                Assert.That(snapshot.SkyExposureCacheMisses, Is.EqualTo(2));
                Assert.That(snapshot.SkyExposureEvictions, Is.EqualTo(2));
                Assert.That(sky.CachedExposureCount, Is.EqualTo(sky.CacheCapacity));
            });
        });
    }

    [Test]
    public async Task SkyExposureBoundaryBudgetIsClampedAndFailsClosed()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelSkyExposureMaxBoundaryChecks, 0);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);

            Assert.That(sky.MaxBoundaryChecks,
                Is.EqualTo(SharedZLevelSkyExposureSystem.MinimumMaxBoundaryChecks));
            sky.InvalidateAll();
            metrics.ResetCounters();
            var result = sky.GetExposure(
                (testMap.Grid, grid),
                new ZLevelTileIndices(0, 0, 0));
            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.BoundaryBudgetExceeded));
                Assert.That(result.IsExposed, Is.False);
                Assert.That(result.BoundaryChecks, Is.EqualTo(1));
                Assert.That(snapshot.SkyExposureBudgetExhaustions, Is.EqualTo(1));
                Assert.That(snapshot.SkyExposureBoundaryChecks, Is.EqualTo(1));
            });
        });
    }

    [TestCase(-1, 0)]
    [TestCase(1000, SharedZLevelVisibilitySystem.MaximumVisibleLevelDistance)]
    public async Task VisibilityDistanceIsClamped(int configured, int expected)
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelVisibilityMaxLevelDistance, configured);

        await Server.WaitAssertion(() =>
        {
            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
            Assert.That(visibility.MaxVisibleLevelDistance, Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task PvsBudgetExhaustionFailsOpenForTheWholeRefresh()
    {
        await OverrideCVar(Side.Server, CVars.NetPVS, true);
        await OverrideCVar(Side.Server, CCVars.ZLevelPvsVisibilityCheckBudget, 0);

        await Server.WaitAssertion(() =>
        {
            var session = ServerSession;
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Status, Is.EqualTo(SessionStatus.InGame));
            Assert.That(session.AttachedEntity, Is.Not.Null);

            var player = session.AttachedEntity!.Value;
            var playerTransform = SEntMan.GetComponent<TransformComponent>(player);
            var candidate = SEntMan.SpawnEntity(
                null,
                playerTransform.Coordinates.Offset(new Vector2(0.25f, 0.25f)));
            try
            {
                var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
                var pvs = SEntMan.System<ZLevelPvsSystem>();
                Assert.That(pvs.VisibilityCheckBudget, Is.Zero);

                metrics.ResetCounters();
                pvs.RefreshSession(session);
                var snapshot = metrics.Snapshot();
                Assert.Multiple(() =>
                {
                    Assert.That(snapshot.PvsRefreshes, Is.EqualTo(1));
                    Assert.That(snapshot.PvsCandidates, Is.GreaterThan(0));
                    Assert.That(snapshot.PvsVisibilityChecks, Is.Zero);
                    Assert.That(snapshot.PvsBudgetExhaustions, Is.EqualTo(1));
                    Assert.That(snapshot.PvsFailOpenCandidates, Is.EqualTo(snapshot.PvsCandidates));
                    Assert.That(snapshot.PvsVisible, Is.EqualTo(snapshot.PvsCandidates));
                    Assert.That(snapshot.PvsCulled, Is.Zero);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(candidate);
            }
        });
    }

    [Test]
    public async Task TraceCrossingBudgetIsClampedAndRejectsOversizedVerticalWork()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxVerticalCrossings, 0);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.MaxVerticalCrossings, Is.EqualTo(1));
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                2,
                out var destination), Is.True);

            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.IterationBudgetExceeded));
                Assert.That(result.Segments, Is.Empty);
                Assert.That(result.TileVisits, Is.Empty);
                Assert.That(result.BoundaryCrossings, Is.Empty);
            });
        });
    }

    [Test]
    public async Task TraceTileBudgetRollsBackTheOverflowingSegment()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxTileVisits, 2);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.MaxTileVisits, Is.EqualTo(2));
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(3.5f, 0.5f),
                0,
                out var destination), Is.True);

            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.IterationBudgetExceeded));
                Assert.That(result.Segments, Is.Empty);
                Assert.That(result.TileVisits, Is.Empty,
                    "An overflowing segment must not expose a truncated tile sequence.");
                Assert.That(result.EntityHits, Is.Empty);
            });
        });
    }

    [Test]
    public async Task TraceEntityHitBudgetRollsBackTheOverflowingSegment()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxEntityHits, 0);
        var testMap = await Pair.CreateTestMap();
        ZLevelTracePoint origin = default;
        ZLevelTracePoint destination = default;

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            Assert.That(trace.MaxEntityHits, Is.EqualTo(1));
            SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(1.5f, 0.5f)));
            SEntMan.SpawnEntity(
                "ZLevelTraceObstacle",
                new EntityCoordinates(testMap.Grid, new Vector2(2.5f, 0.5f)));
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(3.5f, 0.5f),
                0,
                out destination), Is.True);
        });

        await RunTicksSync(1);

        await Server.WaitAssertion(() =>
        {
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var result = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Projectile,
                (int)CollisionGroup.BulletImpassable));
            Assert.Multiple(() =>
            {
                Assert.That(result.Termination, Is.EqualTo(ZLevelTraceTermination.IterationBudgetExceeded));
                Assert.That(result.Segments, Is.Empty);
                Assert.That(result.TileVisits, Is.Empty);
                Assert.That(result.EntityHits, Is.Empty,
                    "An overflowing hit set must roll back the complete segment.");
                Assert.That(result.BoundaryCrossings, Is.Empty);
            });
        });
    }
}
