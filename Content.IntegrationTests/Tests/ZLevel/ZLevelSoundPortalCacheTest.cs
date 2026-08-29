// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelSoundPortalCacheTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [Test]
    public async Task SoundPolicyClassifiesAndInvalidatesOnlyTheAffectedChunkLayer()
    {
        var testMap = await Pair.CreateTestMap();
        var explicitTile = new Vector2i(0, 0);
        var defaultTile = new Vector2i(1, 0);
        var soundClosedTile = new Vector2i(2, 0);
        EntityUid explicitProvider = default;

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 2);
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(explicitTile.X, explicitTile.Y, 1),
                new Tile(1));
            explicitProvider = SetBoundary(
                testMap,
                explicitTile,
                0,
                opens: ZLevelBoundaryChannels.Sound);
            SetBoundary(
                testMap,
                soundClosedTile,
                0,
                closes: ZLevelBoundaryChannels.Sound);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var portals = CEntMan.System<SharedZLevelSoundPortalSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            var results = new List<ZLevelSoundPortal>(4);
            var query = portals.QueryPortals(
                (testMap.CGridUid, grid),
                explicitTile,
                soundClosedTile,
                0,
                0,
                results);
            Assert.Multiple(() =>
            {
                Assert.That(query.Status, Is.EqualTo(ZLevelSoundPortalQueryStatus.Success));
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results[0].Tile, Is.EqualTo(explicitTile));
                Assert.That(results[0].Kind, Is.EqualTo(ZLevelSoundPortalKind.ExplicitOpening));
                Assert.That(results[1].Tile, Is.EqualTo(defaultTile));
                Assert.That(results[1].Kind, Is.EqualTo(ZLevelSoundPortalKind.DefaultOpening));
            });
        });

        await Server.WaitAssertion(() =>
        {
            var portals = SEntMan.System<SharedZLevelSoundPortalSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var results = new List<ZLevelSoundPortal>(4);

            portals.InvalidateGrid(testMap.Grid);
            portals.ResetMetrics();
            var query = portals.QueryPortals(
                (testMap.Grid, grid),
                explicitTile,
                soundClosedTile,
                0,
                0,
                results);

            Assert.Multiple(() =>
            {
                Assert.That(query.Status, Is.EqualTo(ZLevelSoundPortalQueryStatus.Success));
                Assert.That(query.PortalsAdded, Is.EqualTo(2));
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results[0].Tile, Is.EqualTo(explicitTile));
                Assert.That(results[0].Kind, Is.EqualTo(ZLevelSoundPortalKind.ExplicitOpening));
                Assert.That(results[0].LowerLocalZ, Is.Zero);
                Assert.That(results[0].LowerWorldZ, Is.Zero, "Z 0 must retain legacy frame semantics.");
                Assert.That(results[1].Tile, Is.EqualTo(defaultTile));
                Assert.That(results[1].Kind, Is.EqualTo(ZLevelSoundPortalKind.DefaultOpening));
                Assert.That(boundaries.IsOpen(
                    testMap.Grid,
                    grid,
                    explicitTile,
                    0,
                    1,
                    ZLevelBoundaryChannels.Visibility), Is.False,
                    "Opening Sound must not implicitly open Visibility.");
                Assert.That(boundaries.IsOpen(
                    testMap.Grid,
                    grid,
                    soundClosedTile,
                    0,
                    1,
                    ZLevelBoundaryChannels.Visibility), Is.True,
                    "Closing Sound must not implicitly close Visibility.");
            });

            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                Vector2i.Zero,
                0,
                out var origin), Is.True);
            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                new Vector2i(1, 0),
                0,
                out var neighboring), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(origin.OpenCount, Is.EqualTo(ZLevelSoundPortalChunk.TileCount - 1));
                Assert.That(origin.ExplicitOpenCount, Is.EqualTo(1));
                Assert.That(origin.IsExplicitlyOpen(explicitTile), Is.True);
                Assert.That(origin.IsOpen(soundClosedTile), Is.False);
                Assert.That(portals.Snapshot().BuildTileChecks,
                    Is.EqualTo(2 * ZLevelSoundPortalChunk.TileCount));
            });

            var firstRevision = origin.Revision;
            var neighboringRevision = neighboring.Revision;
            var map = SEntMan.System<SharedMapSystem>();
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(defaultTile.X, defaultTile.Y, 1),
                new Tile(1));

            Assert.That(portals.CachedChunkCount, Is.EqualTo(1),
                "A tile edit must retain unrelated cached chunks.");
            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                new Vector2i(1, 0),
                0,
                out var retained), Is.True);
            Assert.That(retained.Revision, Is.EqualTo(neighboringRevision));
            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                Vector2i.Zero,
                0,
                out var rebuilt), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(rebuilt.Revision, Is.GreaterThan(firstRevision));
                Assert.That(rebuilt.IsOpen(defaultTile), Is.False);
                Assert.That(portals.Snapshot().InvalidatedChunks, Is.EqualTo(1));
            });

            var provider = SEntMan.GetComponent<ZLevelBoundaryComponent>(explicitProvider);
            boundaries.SetBoundary(
                (explicitProvider, provider),
                true,
                1,
                ZLevelBoundaryChannels.Projectile,
                ZLevelBoundaryChannels.None);
            Assert.That(portals.CachedChunkCount, Is.EqualTo(1),
                "Changing one provider must invalidate its exact chunk/layer only.");

            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
            Assert.That(portals.CachedChunkCount, Is.Zero,
                "Changing map boundary policy must invalidate every chunk on that map.");
        });
    }

    [Test]
    public async Task QueriesAreDeterministicBudgetedAndFailWithoutPartialResults()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelSoundPortalCacheCapacity, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.ExplicitOnly, 0, 2);
            var portals = SEntMan.System<SharedZLevelSoundPortalSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var results = new List<ZLevelSoundPortal>(8);

            portals.InvalidateGrid(testMap.Grid);
            portals.ResetMetrics();
            var success = portals.QueryPortals(
                (testMap.Grid, grid),
                new Vector2i(-1, 0),
                new Vector2i(1, 0),
                0,
                1,
                results);
            Assert.Multiple(() =>
            {
                Assert.That(success.Status, Is.EqualTo(ZLevelSoundPortalQueryStatus.Success));
                Assert.That(success.PortalsAdded, Is.EqualTo(6));
                Assert.That(success.ChunksVisited, Is.EqualTo(4));
                Assert.That(portals.CacheCapacity, Is.EqualTo(SharedZLevelSoundPortalSystem.MinimumCacheCapacity));
                Assert.That(portals.CachedChunkCount, Is.EqualTo(1));
                Assert.That(portals.Snapshot().Evictions, Is.EqualTo(3));
            });

            var expectedTiles = new[]
            {
                new Vector2i(-1, 0),
                new Vector2i(0, 0),
                new Vector2i(1, 0),
                new Vector2i(-1, 0),
                new Vector2i(0, 0),
                new Vector2i(1, 0),
            };
            var expectedLayers = new[] { 0, 0, 0, 1, 1, 1 };
            for (var i = 0; i < results.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(results[i].Tile, Is.EqualTo(expectedTiles[i]));
                    Assert.That(results[i].LowerLocalZ, Is.EqualTo(expectedLayers[i]));
                });
            }

            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                Vector2i.Zero,
                0,
                out _), Is.True);
            results.Clear();
            results.Add(default);
            var candidateBudget = new ZLevelSoundPortalQueryBudget(1, 0, 2);
            var candidateFailure = portals.QueryPortals(
                (testMap.Grid, grid),
                Vector2i.Zero,
                new Vector2i(2, 0),
                0,
                0,
                results,
                ref candidateBudget);
            Assert.Multiple(() =>
            {
                Assert.That(candidateFailure.Status,
                    Is.EqualTo(ZLevelSoundPortalQueryStatus.CandidateBudgetExceeded));
                Assert.That(candidateFailure.PortalsAdded, Is.Zero);
                Assert.That(candidateFailure.CandidatesVisited, Is.EqualTo(2));
                Assert.That(results.Count, Is.EqualTo(1), "The caller's previous results must be preserved.");
                Assert.That(candidateBudget.RemainingCandidates, Is.Zero);
            });

            var buildBudget = new ZLevelSoundPortalQueryBudget(1, 0, 100);
            var buildFailure = portals.QueryPortals(
                (testMap.Grid, grid),
                new Vector2i(16, 0),
                new Vector2i(16, 0),
                0,
                0,
                results,
                ref buildBudget);
            Assert.Multiple(() =>
            {
                Assert.That(buildFailure.Status,
                    Is.EqualTo(ZLevelSoundPortalQueryStatus.BuildBudgetExceeded));
                Assert.That(buildFailure.PortalsAdded, Is.Zero);
                Assert.That(results.Count, Is.EqualTo(1));
                Assert.That(buildBudget.RemainingChunks, Is.EqualTo(1));
            });

            var chunkBudget = new ZLevelSoundPortalQueryBudget(1, 1, 100);
            var chunkFailure = portals.QueryPortals(
                (testMap.Grid, grid),
                Vector2i.Zero,
                new Vector2i(16, 0),
                0,
                0,
                results,
                ref chunkBudget);
            Assert.Multiple(() =>
            {
                Assert.That(chunkFailure.Status,
                    Is.EqualTo(ZLevelSoundPortalQueryStatus.ChunkBudgetExceeded));
                Assert.That(chunkFailure.PortalsAdded, Is.Zero);
                Assert.That(chunkFailure.CandidatesVisited, Is.EqualTo(16));
                Assert.That(results.Count, Is.EqualTo(1));
            });

            var metrics = portals.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.ChunkBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.BuildBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.CandidateBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.QueryPortalsAdded, Is.EqualTo(6));
                Assert.That(metrics.CacheOrderTokens,
                    Is.LessThanOrEqualTo(portals.CacheCapacity * 2));
            });
        });
    }

    [Test]
    public async Task MovingFrameReprojectsCachedLocalPortalsWithoutRebuild()
    {
        var testMap = await Pair.CreateTestMap();
        var tile = new Vector2i(3, -2);

        await Server.WaitAssertion(() =>
        {
            Configure(testMap, ZLevelDefaultBoundaryMode.TileAboveCloses, 0, 1);
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -3f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(15));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);

            var portals = SEntMan.System<SharedZLevelSoundPortalSystem>();
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var results = new List<ZLevelSoundPortal>(1);
            portals.InvalidateGrid(testMap.Grid);
            portals.ResetMetrics();

            var firstQuery = portals.QueryPortals(
                (testMap.Grid, grid),
                tile,
                tile,
                0,
                0,
                results);
            Assert.That(firstQuery.Succeeded, Is.True);
            Assert.That(results.Count, Is.EqualTo(1));
            var first = results[0];
            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                SharedMapSystem.GetChunkIndices(tile, ZLevelSoundPortalChunk.ChunkSize),
                0,
                out var cached), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(first.LocalPosition, Is.EqualTo(map.TileCenterToVector((testMap.Grid, grid), tile)));
                Assert.That(first.WorldPosition,
                    Is.EqualTo(map.GridTileToWorldPos(testMap.Grid, grid, tile)));
                Assert.That(first.LowerWorldZ, Is.EqualTo(5));
                Assert.That(first.UpperWorldZ, Is.EqualTo(6));
            });

            transform.SetLocalPosition(testMap.Grid, new Vector2(28f, 4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(-35));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 8), Is.True);
            results.Clear();
            var secondQuery = portals.QueryPortals(
                (testMap.Grid, grid),
                tile,
                tile,
                0,
                0,
                results);
            Assert.That(secondQuery.Succeeded, Is.True);
            Assert.That(portals.TryGetPortalChunk(
                (testMap.Grid, grid),
                cached.Key.ChunkIndices,
                0,
                out var retained), Is.True);

            var moved = results[0];
            QueryPortalRepeated(portals, (testMap.Grid, grid), tile, results, 2_048);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var accepted = QueryPortalRepeated(portals, (testMap.Grid, grid), tile, results, 1_000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var metrics = portals.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(retained.Revision, Is.EqualTo(cached.Revision));
                Assert.That(moved.LocalPosition, Is.EqualTo(first.LocalPosition));
                Assert.That(Vector2.Distance(moved.WorldPosition, first.WorldPosition), Is.GreaterThan(1f));
                Assert.That(moved.WorldPosition,
                    Is.EqualTo(map.GridTileToWorldPos(testMap.Grid, grid, tile)));
                Assert.That(moved.LowerWorldZ, Is.EqualTo(8));
                Assert.That(moved.UpperWorldZ, Is.EqualTo(9));
                Assert.That(metrics.Builds, Is.EqualTo(1));
                Assert.That(accepted, Is.EqualTo(1_000));
                Assert.That(allocated, Is.LessThanOrEqualTo(512),
                    "Hot bounded queries must reuse caller buffers without allocation growth.");
            });
            TestContext.Progress.WriteLine(
                $"WTZ P4.1 sound portal cache: builds={metrics.Builds}, " +
                $"buildMs={metrics.BuildMilliseconds:0.000}, " +
                $"queries={metrics.PortalQueries}, hit={metrics.CacheHitPercent:0.0}%, " +
                $"hotBytes={allocated}");
        });
    }

    [Test]
    public async Task GridTerminationDropsItsPortalChunks()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var auxiliary = mapManager.CreateGridEntity(testMap.MapId);
            auxiliary.Comp.CanSplit = false;
            var portals = SEntMan.System<SharedZLevelSoundPortalSystem>();
            portals.InvalidateAll();
            Assert.That(portals.TryGetPortalChunk(
                auxiliary,
                Vector2i.Zero,
                0,
                out _), Is.True);
            Assert.That(portals.CachedChunkCount, Is.EqualTo(1));

            SEntMan.DeleteEntity(auxiliary.Owner);
            Assert.That(portals.CachedChunkCount, Is.Zero);
        });
    }

    private void Configure(
        TestMapData testMap,
        ZLevelDefaultBoundaryMode boundaryMode,
        int minZ,
        int maxZ)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(testMap.MapUid, minZ, maxZ, minZ, boundaryMode);
        SEntMan.GetComponent<MapGridComponent>(testMap.Grid).CanSplit = false;
    }

    private EntityUid SetBoundary(
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
        map.SetZLevelTile(
            testMap.Grid,
            grid,
            new ZLevelTileIndices(tile.X, tile.Y, lowerLocalZ),
            new Tile(1));
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary((provider, boundary), true, 1, opens, closes);
        transform.AnchorEntity(provider, SEntMan.GetComponent<TransformComponent>(provider));
        Assert.That(SEntMan.GetComponent<TransformComponent>(provider).Anchored, Is.True);
        return provider;
    }

    private static int QueryPortalRepeated(
        SharedZLevelSoundPortalSystem portals,
        Entity<MapGridComponent> grid,
        Vector2i tile,
        List<ZLevelSoundPortal> results,
        int count)
    {
        var accepted = 0;
        for (var i = 0; i < count; i++)
        {
            results.Clear();
            accepted += portals.QueryPortals(grid, tile, tile, 0, 0, results).PortalsAdded;
        }

        return accepted;
    }
}
