// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelTileProjectionTest : GameTest
{
    private static readonly Vector2i CenterTile = new(8, 8);

    [Test]
    public async Task NormalPlanUsesCompleteAperturesAndTracksMovingFrames()
    {
        var testMap = await Pair.CreateTestMap();
        var depthTwoOpen = CenterTile;
        var depthOneOpen = CenterTile + new Vector2i(1, 0);
        var depthTwoClosed = CenterTile + new Vector2i(2, 0);
        var activeOnly = CenterTile + new Vector2i(3, 0);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            grid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, 2, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            transform.SetZLevelFrameOrigin(testMap.Grid, 5);
            transform.SetLocalPosition(testMap.Grid, new Vector2(7f, -4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(23f));

            SetTile(map, testMap, grid, depthTwoOpen, 0);
            SetTile(map, testMap, grid, depthOneOpen, 1);
            SetTile(map, testMap, grid, depthTwoClosed, 0);
            SetTile(map, testMap, grid, depthTwoClosed, 1);
            SetTile(map, testMap, grid, activeOnly, 2);
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                CenterTile + new Vector2(1.5f, 0.5f),
                8f);

            cache.InvalidateGrid(testMap.CGridUid);
            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 7, false), Is.GreaterThan(0));
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, depthTwoOpen), Is.True);
                Assert.That(ContainsTile(projection, 1, depthOneOpen), Is.True);
                Assert.That(ContainsTile(projection, 0, depthTwoClosed), Is.False);
                Assert.That(ContainsTile(projection, 1, depthTwoClosed), Is.True);
                Assert.That(ContainsTile(projection, 2, activeOnly), Is.False);
                Assert.That(projection.Batches.Select(batch => batch.WorldZ), Is.Ordered.Ascending);
                Assert.That(projection.Batches.All(batch => batch.WorldZ < 7), Is.True);
            });
        });

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(27f, 3f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(-31f));
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                CenterTile + new Vector2(1.5f, 0.5f),
                8f);

            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 7, false), Is.GreaterThan(0));
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, depthTwoOpen), Is.True);
                Assert.That(ContainsTile(projection, 0, depthTwoClosed), Is.False);
                Assert.That(ContainsTile(projection, 1, depthTwoClosed), Is.True);
            });
        });
    }

    [Test]
    public async Task ChunkBudgetKeepsNearestFloorFirst()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 1);
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0),
            (CenterTile, 1));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2, false), Is.EqualTo(1));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Batches[0].LocalZ, Is.EqualTo(1));
                Assert.That(ContainsTile(projection, 1, CenterTile), Is.True);
                Assert.That(ContainsTile(projection, 0, CenterTile), Is.False);
                Assert.That(metrics.NormalBudget.CurrentChunksUsed, Is.EqualTo(1));
                Assert.That(metrics.NormalBudget.ChunkExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ChunkBudgetProcessesViewportCenterBeforeEdges()
    {
        var centerChunkTile = new Vector2i(24, 8);
        var edgeChunkTile = new Vector2i(8, 8);
        var testMap = await CreateTileMap(
            1,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (centerChunkTile, 0),
            (edgeChunkTile, 0));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                centerChunkTile + new Vector2(0.5f),
                34f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.GreaterThan(1));
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, centerChunkTile), Is.True);
                Assert.That(ContainsTile(projection, 0, edgeChunkTile), Is.True);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 1);
        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                centerChunkTile + new Vector2(0.5f),
                34f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, centerChunkTile), Is.True);
                Assert.That(ContainsTile(projection, 0, edgeChunkTile), Is.False);
                Assert.That(projection.Batches[0].ChunkIndices, Is.EqualTo(new Vector2i(1, 0)));
                Assert.That(projection.Snapshot().NormalBudget.ChunkExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ChunkBudgetProcessesNearestGridFirst()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity farGridNet = default;

        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var nearGrid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            nearGrid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            SetTile(map, testMap, nearGrid, CenterTile, 0);

            var farGrid = mapManager.CreateGridEntity(testMap.MapId);
            farGrid.Comp.CanSplit = false;
            transform.SetLocalPosition(farGrid.Owner, new Vector2(12f, 8f));
            map.SetZLevelTile(
                farGrid.Owner,
                farGrid.Comp,
                new ZLevelTileIndices(0, 0, 0),
                new Tile(1));
            farGridNet = SEntMan.GetNetEntity(farGrid.Owner);
        });
        await Pair.RunUntilSynced();

        var bounds = Box2.CenteredAround(new Vector2(8.5f), new Vector2(12f));
        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var farGrid = CEntMan.GetEntity(farGridNet);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches.Any(batch => batch.GridUid == testMap.CGridUid), Is.True);
                Assert.That(projection.Batches.Any(batch => batch.GridUid == farGrid), Is.True);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 1);
        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var farGrid = CEntMan.GetEntity(farGridNet);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(1));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Batches[0].GridUid, Is.EqualTo(testMap.CGridUid));
                Assert.That(projection.Batches.Any(batch => batch.GridUid == farGrid), Is.False);
                Assert.That(metrics.GridCandidates, Is.EqualTo(2));
                Assert.That(metrics.NormalBudget.ChunkExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task TileVisitBudgetRejectsAWholeChunk()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 1);
        var testMap = await CreateTileMap(
            1,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0));
        var requiredVisits = 0;

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(1));
            requiredVisits = projection.Snapshot().NormalBudget.CurrentTileVisitsUsed;
            Assert.That(requiredVisits, Is.GreaterThan(1));
        });

        await OverrideCVar(
            Side.Client,
            CCVars.ZLevelTileProjectionMaxTileVisitsPerFrame,
            requiredVisits - 1);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.Zero);
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Tiles, Is.Empty);
                Assert.That(metrics.NormalBudget.CurrentTileVisitsUsed, Is.Zero);
                Assert.That(metrics.NormalBudget.TileVisitExhaustions, Is.EqualTo(1));
                Assert.That(metrics.TilesProjected, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ColdApertureBudgetWarmsWithoutPublishingPartialChunks()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureBuildsPerFrame, 1);
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();
            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2, false), Is.Zero);
            var cold = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Tiles, Is.Empty);
                Assert.That(cold.NormalBudget.ApertureBuildExhaustions, Is.EqualTo(1));
                Assert.That(cold.NormalBudget.CurrentApertureBuildsUsed, Is.EqualTo(1));
                Assert.That(cache.CachedApertureChunkCount, Is.EqualTo(1));
            });

            projection.BeginBudgetFrameForTesting(false);
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2, false), Is.EqualTo(1));
            var warm = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, CenterTile), Is.True);
                Assert.That(warm.NormalBudget.CurrentApertureLayersUsed, Is.EqualTo(3));
                Assert.That(warm.NormalBudget.CurrentApertureBuildsUsed, Is.EqualTo(1));
                Assert.That(warm.NormalBudget.ApertureBuildExhaustions, Is.EqualTo(1));
                Assert.That(cache.CachedApertureChunkCount, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task ApertureLayerBudgetKeepsCompletedNearFloorChunks()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureLayersPerFrame, 1);
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0),
            (CenterTile, 1));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            cache.InvalidateGrid(testMap.CGridUid);
            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2, false), Is.EqualTo(1));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 1, CenterTile), Is.True);
                Assert.That(ContainsTile(projection, 0, CenterTile), Is.False);
                Assert.That(metrics.NormalBudget.CurrentApertureLayersUsed, Is.EqualTo(1));
                Assert.That(metrics.NormalBudget.ApertureLayerExhaustions, Is.EqualTo(1));
                Assert.That(metrics.NormalBudget.CurrentTileVisitsUsed, Is.GreaterThan(0));
            });
        });
    }

    [Test]
    public async Task MappingPreviewUsesAnIndependentPoolAndBypassesApertures()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 0, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureLayersPerFrame, 0, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureBuildsPerFrame, 0, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxTileVisitsPerFrame, 0);
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.TileAboveCloses,
            (CenterTile, 0),
            (CenterTile, 2));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();
            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.Zero);
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.EqualTo(2));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(ContainsTile(projection, 0, CenterTile), Is.True);
                Assert.That(ContainsTile(projection, 2, CenterTile), Is.True);
                Assert.That(projection.Batches.Select(batch => batch.WorldZ), Is.Ordered.Ascending);
                Assert.That(metrics.NormalBudget.ChunkExhaustions, Is.EqualTo(1));
                Assert.That(metrics.MappingBudget.CurrentChunksUsed, Is.EqualTo(2));
                Assert.That(metrics.MappingBudget.ApertureLayerExhaustions, Is.Zero);
                Assert.That(metrics.MappingBudget.ApertureBuildExhaustions, Is.Zero);
                Assert.That(cache.Snapshot().ApertureQueries, Is.Zero);
            });
        });
    }

    [Test]
    public async Task MappingChunkBudgetKeepsTheLowerAdjacentLayerFirst()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxChunksPerFrame, 1);
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0),
            (CenterTile, 2));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.EqualTo(1));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Batches[0].LocalZ, Is.EqualTo(0));
                Assert.That(ContainsTile(projection, 0, CenterTile), Is.True);
                Assert.That(ContainsTile(projection, 2, CenterTile), Is.False);
                Assert.That(metrics.MappingBudget.CurrentChunksUsed, Is.EqualTo(1));
                Assert.That(metrics.MappingBudget.ChunkExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task MappingTileVisitBudgetRejectsAWholeChunk()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxChunksPerFrame, 1);
        var testMap = await CreateTileMap(
            1,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0));
        var requiredVisits = 0;

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.EqualTo(1));
            requiredVisits = projection.Snapshot().MappingBudget.CurrentTileVisitsUsed;
            Assert.That(requiredVisits, Is.GreaterThan(1));
        });

        await OverrideCVar(
            Side.Client,
            CCVars.ZLevelMappingPreviewMaxTileVisitsPerFrame,
            requiredVisits - 1);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.Zero);
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Tiles, Is.Empty);
                Assert.That(metrics.MappingBudget.CurrentTileVisitsUsed, Is.Zero);
                Assert.That(metrics.MappingBudget.TileVisitExhaustions, Is.EqualTo(1));
                Assert.That(metrics.TilesProjected, Is.Zero);
            });
        });
    }

    [Test]
    public async Task BudgetsAreSharedWithinEachModeAndResetForTheNextFrame()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, 1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxChunksPerFrame, 1);
        var testMap = await CreateTileMap(
            1,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(1));
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.Zero);
            Assert.That(projection.Snapshot().NormalBudget.ChunkExhaustions, Is.EqualTo(1));

            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.EqualTo(1));
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.Zero);
            Assert.That(projection.Snapshot().MappingBudget.ChunkExhaustions, Is.EqualTo(1));

            projection.BeginBudgetFrameForTesting(false);
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, false), Is.EqualTo(1));
            projection.BeginBudgetFrameForTesting(true);
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1, true), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Snapshot().NormalBudget.ChunkExhaustions, Is.EqualTo(1));
                Assert.That(projection.Snapshot().MappingBudget.ChunkExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ClientTileProjectionLimitsAreClamped()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureLayersPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureBuildsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxTileVisitsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxChunksPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxTileVisitsPerFrame, -1);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(projection.MaxChunksPerFrame, Is.Zero);
                Assert.That(projection.MaxApertureLayersPerFrame, Is.Zero);
                Assert.That(projection.MaxApertureBuildsPerFrame, Is.Zero);
                Assert.That(projection.MaxTileVisitsPerFrame, Is.Zero);
                Assert.That(projection.MappingMaxChunksPerFrame, Is.Zero);
                Assert.That(projection.MappingMaxTileVisitsPerFrame, Is.Zero);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxChunksPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureLayersPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxApertureBuildsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelTileProjectionMaxTileVisitsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxChunksPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelMappingPreviewMaxTileVisitsPerFrame, int.MaxValue);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(projection.MaxChunksPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumChunksPerFrame));
                Assert.That(projection.MaxApertureLayersPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumApertureLayersPerFrame));
                Assert.That(projection.MaxApertureBuildsPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumApertureBuildsPerFrame));
                Assert.That(projection.MaxTileVisitsPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumTileVisitsPerFrame));
                Assert.That(projection.MappingMaxChunksPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumMappingChunksPerFrame));
                Assert.That(projection.MappingMaxTileVisitsPerFrame,
                    Is.EqualTo(ZLevelTileProjectionSystem.MaximumMappingTileVisitsPerFrame));
            });
        });
    }

    [Test]
    public async Task PlanningAndBatchedGeometryReuseBuffersAfterWarmup()
    {
        var testMap = await CreateTileMap(
            2,
            ZLevelDefaultBoundaryMode.ExplicitOnly,
            (CenterTile, 0),
            (CenterTile, 1));

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelTileProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, CenterTile + new Vector2(0.5f), 0.8f);

            BuildRepeated(projection, testMap.MapId, bounds, 2, 512);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var batches = BuildRepeated(projection, testMap.MapId, bounds, 2, 100);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.Multiple(() =>
            {
                Assert.That(batches, Is.EqualTo(200));
                Assert.That(allocated, Is.LessThanOrEqualTo(512),
                    "Tile planning must reuse grid, context, batch, and tile buffers after warm-up.");
            });

            var vertices = new List<DrawVertexUV2D>();
            var region = new Box2(0.25f, 0.5f, 0.75f, 1f);
            AppendGeometryRepeated(vertices, region, 512);
            var geometryAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            AppendGeometryRepeated(vertices, region, 100);
            var geometryAllocated = GC.GetAllocatedBytesForCurrentThread() - geometryAllocatedBefore;

            Assert.Multiple(() =>
            {
                Assert.That(vertices, Has.Count.EqualTo(12));
                Assert.That(vertices[0].Position, Is.EqualTo(new Vector2(16f, 16f)));
                Assert.That(vertices[0].UV, Is.EqualTo(region.BottomLeft));
                Assert.That(vertices[5].UV, Is.EqualTo(region.TopLeft));
                Assert.That(geometryAllocated, Is.LessThanOrEqualTo(512),
                    "Chunk geometry must reuse its caller-owned vertex buffer after warm-up.");
            });
        });
    }

    private async Task<TestMapData> CreateTileMap(
        int maximumLocalZ,
        ZLevelDefaultBoundaryMode boundaryMode,
        params (Vector2i Indices, int LocalZ)[] tiles)
    {
        var testMap = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            grid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, maximumLocalZ, 0, boundaryMode);

            foreach (var (indices, localZ) in tiles)
            {
                SetTile(map, testMap, grid, indices, localZ);
            }
        });
        await Pair.RunTicksSync(3);
        return testMap;
    }

    private static void SetTile(
        SharedMapSystem map,
        TestMapData testMap,
        MapGridComponent grid,
        Vector2i indices,
        int localZ)
    {
        map.SetZLevelTile(
            testMap.Grid,
            grid,
            new ZLevelTileIndices(indices.X, indices.Y, localZ),
            new Tile(1));
    }

    private static bool ContainsTile(
        ZLevelTileProjectionSystem projection,
        int localZ,
        Vector2i indices)
    {
        foreach (var batch in projection.Batches)
        {
            if (batch.LocalZ != localZ)
                continue;

            for (var i = 0; i < batch.TileCount; i++)
            {
                if (projection.Tiles[batch.FirstTile + i].Indices == indices)
                    return true;
            }
        }

        return false;
    }

    private static Box2 BoundsAroundLocalPoint(
        SharedTransformSystem transform,
        EntityUid gridUid,
        Vector2 localPoint,
        float size)
    {
        var (_, _, worldMatrix, _) = transform.GetWorldPositionRotationMatrixWithInv(gridUid);
        var worldPoint = Vector2.Transform(localPoint, worldMatrix);
        return Box2.CenteredAround(worldPoint, new Vector2(size));
    }

    private static int BuildRepeated(
        ZLevelTileProjectionSystem projection,
        MapId mapId,
        Box2 bounds,
        int viewerWorldZ,
        int count)
    {
        var batches = 0;
        for (var i = 0; i < count; i++)
        {
            projection.BeginBudgetFrameForTesting(false);
            batches += projection.BuildProjection(mapId, bounds, viewerWorldZ, false);
        }

        return batches;
    }

    private static void AppendGeometryRepeated(
        List<DrawVertexUV2D> vertices,
        Box2 region,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            vertices.Clear();
            ZLevelTileProjectionGeometry.AppendTileVertices(vertices, CenterTile, 2f, region);
            ZLevelTileProjectionGeometry.AppendTileVertices(
                vertices,
                CenterTile + new Vector2i(1, 0),
                2f,
                region);
        }
    }
}
