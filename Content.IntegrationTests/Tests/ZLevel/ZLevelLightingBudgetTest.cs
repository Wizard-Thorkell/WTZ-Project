// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelLightingBudgetTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelLightingBudgetTestLight
  components:
  - type: PointLight
    enabled: true
    radius: 4
    energy: 2
    color: '#40A0FFFF'
";

    [Test]
    public async Task ClientLightingLimitsAreClamped()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingApertureCacheCapacity, 0, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmitterCandidatesPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmittersPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureLayersPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureBuildsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxRunsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, -1, false);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(cache.ApertureCacheCapacity,
                    Is.EqualTo(ZLevelLightingCacheSystem.MinimumApertureCacheCapacity));
                Assert.That(projection.MaxEmitterCandidatesPerFrame, Is.Zero);
                Assert.That(projection.MaxEmittersPerFrame, Is.Zero);
                Assert.That(projection.MaxApertureLayersPerFrame, Is.Zero);
                Assert.That(projection.MaxApertureBuildsPerFrame, Is.Zero);
                Assert.That(projection.MaxRunsPerFrame, Is.Zero);
                Assert.That(projection.MaxShadowLightsPerFrame, Is.Zero);
                Assert.That(projection.MaxShadowFloorGroupsPerFrame, Is.Zero);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelLightingApertureCacheCapacity, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmitterCandidatesPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmittersPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureLayersPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureBuildsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxRunsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, int.MaxValue, false);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(cache.ApertureCacheCapacity,
                    Is.EqualTo(ZLevelLightingCacheSystem.MaximumApertureCacheCapacity));
                Assert.That(projection.MaxEmitterCandidatesPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumEmitterCandidatesPerFrame));
                Assert.That(projection.MaxEmittersPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumEmittersPerFrame));
                Assert.That(projection.MaxApertureLayersPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumApertureLayersPerFrame));
                Assert.That(projection.MaxApertureBuildsPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumApertureBuildsPerFrame));
                Assert.That(projection.MaxRunsPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumRunsPerFrame));
                Assert.That(projection.MaxShadowLightsPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumShadowLightsPerFrame));
                Assert.That(projection.MaxShadowFloorGroupsPerFrame,
                    Is.EqualTo(ZLevelLightingProjectionSystem.MaximumShadowFloorGroupsPerFrame));
            });
        });
    }

    [Test]
    public async Task ApertureCacheEvictsOldestEntryAndRecomputesItExactly()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingApertureCacheCapacity, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            SEntMan.System<SharedZLevelMapSystem>().Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            var firstChunk = Vector2i.Zero;
            var secondChunk = new Vector2i(1, 0);

            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                firstChunk,
                0,
                out var first), Is.True);
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                secondChunk,
                0,
                out _), Is.True);
            Assert.That(cache.TryGetApertureChunk(
                (testMap.CGridUid, grid),
                firstChunk,
                0,
                out var rebuilt), Is.True);

            var metrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(rebuilt.Revision, Is.GreaterThan(first.Revision));
                Assert.That(rebuilt.Word0, Is.EqualTo(first.Word0));
                Assert.That(rebuilt.Word1, Is.EqualTo(first.Word1));
                Assert.That(rebuilt.Word2, Is.EqualTo(first.Word2));
                Assert.That(rebuilt.Word3, Is.EqualTo(first.Word3));
                Assert.That(rebuilt.OpenCount, Is.EqualTo(first.OpenCount));
                Assert.That(metrics.ApertureBuilds, Is.EqualTo(3));
                Assert.That(metrics.ApertureEvictions, Is.EqualTo(2));
                Assert.That(metrics.CachedApertureChunks, Is.EqualTo(1));
                Assert.That(metrics.CachedOpenApertureTiles, Is.EqualTo(rebuilt.OpenCount));
            });

            Assert.That(cache.TryComposeApertureStack(
                (testMap.CGridUid, grid),
                firstChunk,
                0,
                2,
                out var firstStack), Is.True);
            Assert.That(cache.TryComposeApertureStack(
                (testMap.CGridUid, grid),
                firstChunk,
                0,
                2,
                out var recomposedStack), Is.True);

            var stackMetrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(firstStack.OpenCount, Is.EqualTo(ZLevelApertureChunk.TileCount));
                Assert.That(recomposedStack.Word0, Is.EqualTo(firstStack.Word0));
                Assert.That(recomposedStack.Word1, Is.EqualTo(firstStack.Word1));
                Assert.That(recomposedStack.Word2, Is.EqualTo(firstStack.Word2));
                Assert.That(recomposedStack.Word3, Is.EqualTo(firstStack.Word3));
                Assert.That(recomposedStack.OpenCount, Is.EqualTo(firstStack.OpenCount));
                Assert.That(stackMetrics.ApertureBuilds, Is.EqualTo(6));
                Assert.That(stackMetrics.ApertureEvictions, Is.EqualTo(5));
                Assert.That(stackMetrics.CachedApertureChunks, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task EmitterCandidateBudgetStopsBroadPhaseVisits()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);

            for (var i = 0; i < 3; i++)
            {
                var light = SEntMan.SpawnEntity(
                    "ZLevelLightingBudgetTestLight",
                    new EntityCoordinates(testMap.Grid, new Vector2(0.5f + i * 0.1f, 0.5f)));
                zLevels.SetZLevelPosition(light, 0);
            }
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 8f);
            var emitters = new List<ZLevelLightEmitter>(3);

            cache.ResetMetrics();
            var result = cache.QueryEmitters(testMap.MapId, bounds, 0, 0, emitters, 1);
            var metrics = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(result.Accepted, Is.EqualTo(1));
                Assert.That(result.CandidatesVisited, Is.EqualTo(1));
                Assert.That(result.CandidateBudgetExceeded, Is.True);
                Assert.That(emitters, Has.Count.EqualTo(1));
                Assert.That(metrics.EmitterCandidates, Is.EqualTo(1));
                Assert.That(metrics.EmitterCandidateBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task EmitterBudgetKeepsNearestCompleteLowerFloorFirst()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmittersPerFrame, 1);
        var testMap = await CreateOpenLightingMap(2, 0, 1);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 4f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2), Is.EqualTo(1));
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Batches[0].Depth, Is.EqualTo(1));
                Assert.That(projection.Batches[0].RunCount, Is.GreaterThan(0));
                Assert.That(metrics.CurrentEmittersUsed, Is.EqualTo(1));
                Assert.That(metrics.EmitterBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ColdBuildBudgetWarmsCacheWithoutPublishingPartialLight()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureBuildsPerFrame, 1);
        var testMap = await CreateOpenLightingMap(2, 0);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 0.8f);

            cache.InvalidateGrid(testMap.CGridUid);
            cache.ResetMetrics();
            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2), Is.Zero);
            var coldProjection = projection.Snapshot();
            var coldCache = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Runs, Is.Empty);
                Assert.That(coldProjection.ApertureBuildBudgetExhaustions, Is.EqualTo(1));
                Assert.That(coldProjection.CurrentApertureBuildsUsed, Is.EqualTo(1));
                Assert.That(coldCache.CachedApertureChunks, Is.EqualTo(1));
            });

            projection.BeginBudgetFrameForTesting();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2), Is.EqualTo(1));
            var warmProjection = projection.Snapshot();
            var warmCache = cache.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Batches[0].Depth, Is.EqualTo(2));
                Assert.That(projection.Batches[0].RunCount, Is.EqualTo(1));
                Assert.That(warmProjection.ApertureBuildBudgetExhaustions, Is.EqualTo(1));
                Assert.That(warmProjection.CurrentApertureLayersUsed, Is.EqualTo(2));
                Assert.That(warmProjection.CurrentApertureBuildsUsed, Is.EqualTo(1));
                Assert.That(warmCache.CachedApertureChunks, Is.EqualTo(2));
            });
        });
    }

    [Test]
    public async Task LayerBudgetRejectsTheWholeEmitterPlan()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxApertureLayersPerFrame, 1);
        var testMap = await CreateOpenLightingMap(2, 0);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 2), Is.Zero);
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Runs, Is.Empty);
                Assert.That(metrics.CurrentApertureLayersUsed, Is.EqualTo(1));
                Assert.That(metrics.ApertureLayerBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.VisibleRuns, Is.Zero);
                Assert.That(metrics.VisibleTiles, Is.Zero);
            });
        });
    }

    [Test]
    public async Task RunBudgetRollsBackTheWholeEmitterPlan()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxRunsPerFrame, 1);
        var testMap = await CreateOpenLightingMap(1, 0);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 4f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.Zero);
            var metrics = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Is.Empty);
                Assert.That(projection.Runs, Is.Empty);
                Assert.That(metrics.CurrentRunsUsed, Is.EqualTo(1));
                Assert.That(metrics.RunBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.VisibleRuns, Is.Zero);
                Assert.That(metrics.VisibleTiles, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ProjectionBudgetsAreSharedWithinAFrameAndResetForTheNext()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxEmittersPerFrame, 1);
        var testMap = await CreateOpenLightingMap(1, 0);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f), 0.8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.EqualTo(1));
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.Zero);
            Assert.That(projection.Snapshot().EmitterBudgetExhaustions, Is.EqualTo(1));

            projection.BeginBudgetFrameForTesting();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(1));
                Assert.That(projection.Snapshot().EmitterBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    private async Task<TestMapData> CreateOpenLightingMap(int viewerLocalZ, params int[] sourceLocalZs)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            format.Configure(
                testMap.MapUid,
                0,
                viewerLocalZ,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);

            foreach (var localZ in sourceLocalZs)
            {
                var light = SEntMan.SpawnEntity(
                    "ZLevelLightingBudgetTestLight",
                    new EntityCoordinates(testMap.Grid, new Vector2(0.5f)));
                zLevels.SetZLevelPosition(light, localZ);
            }
        });
        await Pair.RunTicksSync(3);
        return testMap;
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
}
