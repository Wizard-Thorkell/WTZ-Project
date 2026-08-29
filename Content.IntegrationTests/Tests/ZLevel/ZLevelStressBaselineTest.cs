// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.ZLevel.Systems;
using Content.Shared.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.ZLevel.Systems;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelStressBaselineTest : GameTest
{
    private const int WarmupIterations = 1;
    private const int MeasuredIterations = 3;
    private const string BaselineDirectoryEnvironmentVariable = "WTZ_ZLEVEL_BASELINE_DIR";

    [TestPrototypes]
    private const string StressPrototypes = @"
- type: entity
  id: ZLevelStressGravityGenerator
  components:
  - type: GravityGenerator
  - type: PowerCharge
    windowTitle: gravity-generator-window-title
    idlePower: 50
    chargeRate: 1000000000
    activePower: 500
  - type: ApcPowerReceiver
  - type: UserInterface
";

    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    [TestCase(3)]
    [TestCase(6)]
    [TestCase(10)]
    public async Task GeneratedFixtureProducesMachineReadableBaseline(int floorCount)
    {
        await OverrideCVar(Side.Server, CVars.NetPVS, true);
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelBoundaryCacheCapacity,
            SharedZLevelBoundarySystem.DefaultBoundaryCacheCapacity);
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelSkyExposureCacheCapacity,
            SharedZLevelSkyExposureSystem.DefaultCacheCapacity);
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelSkyExposureMaxBoundaryChecks,
            SharedZLevelSkyExposureSystem.DefaultMaxBoundaryChecks);
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelVisibilityMaxLevelDistance,
            SharedZLevelVisibilitySystem.DefaultMaxVisibleLevelDistance);
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelPvsVisibilityCheckBudget,
            ZLevelPvsSystem.DefaultVisibilityCheckBudget);
        var testMap = await Pair.CreateTestMap(initialized: false);
        ZLevelStressBaseline? baseline = null;
        ZLevelStressFixture? fixture = null;
        Vector2 movingGridStart = default;
        EntityCoordinates originalPlayerCoordinates = default;
        int originalPlayerWorldZ = default;

        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            var floor = (ContentTileDefinition) definitions["FloorSteel"];
            var session = ServerSession;
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.AttachedEntity, Is.Not.Null);
            var player = session.AttachedEntity!.Value;
            originalPlayerCoordinates = SEntMan.GetComponent<TransformComponent>(player).Coordinates;
            originalPlayerWorldZ = SEntMan.System<SharedZLevelSystem>().GetWorldZLevel(player);

            SEntMan.DeleteEntity(testMap.Grid);
            var stationGrid = mapManager.CreateGridEntity(testMap.MapId);
            fixture = ZLevelStressFixtureBuilder.Build(
                SEntMan,
                mapManager,
                testMap.MapUid,
                testMap.MapId,
                stationGrid.Owner,
                floorCount,
                new Tile(floor.TileId),
                "ZLevelStressGravityGenerator");

            var transform = SEntMan.System<SharedTransformSystem>();
            movingGridStart = transform.GetWorldPosition(fixture.MovingGridUid);
            SEntMan.System<SharedMapSystem>().InitializeMap(testMap.MapId);
            SEntMan.GetComponent<MapGridComponent>(fixture.StationGridUid).CanSplit = true;
            SEntMan.GetComponent<MapGridComponent>(fixture.MovingGridUid).CanSplit = true;
        });

        await RunTicksSync(8);

        await Server.WaitAssertion(() =>
        {
            Assert.That(fixture, Is.Not.Null);
            var stressFixture = fixture!;
            var session = ServerSession;
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.Status, Is.EqualTo(SessionStatus.InGame));
            Assert.That(session.AttachedEntity, Is.Not.Null);

            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var player = session.AttachedEntity!.Value;
            try
            {
                transform.SetCoordinates(
                    player,
                    new EntityCoordinates(stressFixture.StationGridUid, new Vector2(10.5f, 10.5f)));
                zLevels.SetZLevelPosition(player, floorCount / 2);

                AssertFixtureStructure(stressFixture);
                Assert.That(stressFixture.GravityGenerators, Has.All.Matches<EntityUid>(uid =>
                    SEntMan.GetComponent<GravityGeneratorComponent>(uid).GravityActive));

                PrepareColdRun(SEntMan, stressFixture);
                var warmup = CaptureRun(SEntMan, session, stressFixture, WarmupIterations, moveGrid: false);
                var measured = CaptureRun(SEntMan, session, stressFixture, MeasuredIterations, moveGrid: true);
                var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
                var skyExposure = SEntMan.System<SharedZLevelSkyExposureSystem>();
                var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
                var pvs = SEntMan.System<ZLevelPvsSystem>();

                AssertRunCoverage(
                    stressFixture,
                    boundaries.BoundaryCacheCapacity,
                    skyExposure.CacheCapacity,
                    warmup,
                    measured);
                Assert.That(transform.GetWorldPosition(stressFixture.MovingGridUid), Is.Not.EqualTo(movingGridStart));

                baseline = new ZLevelStressBaseline(
                    4,
                    new ZLevelStressBudgetSnapshot(
                        boundaries.BoundaryCacheCapacity,
                        skyExposure.CacheCapacity,
                        skyExposure.MaxBoundaryChecks,
                        visibility.MaxVisibleLevelDistance,
                        pvs.VisibilityCheckBudget),
                    CreateFixtureSnapshot(stressFixture),
                    new ZLevelStressWorkloadSnapshot(
                        WarmupIterations,
                        MeasuredIterations,
                        stressFixture.BoundarySamples.Count,
                        stressFixture.GravitySamples.Count,
                        stressFixture.GravitySamples.Count),
                    warmup,
                    measured);
            }
            finally
            {
                transform.SetCoordinates(player, originalPlayerCoordinates);
                zLevels.StampWorldZLevelPosition(player, originalPlayerWorldZ);
            }
        });

        Assert.That(baseline, Is.Not.Null);
        WriteBaseline(baseline!);
    }

    private static void PrepareColdRun(IEntityManager entityManager, ZLevelStressFixture fixture)
    {
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var gravity = entityManager.System<SharedZLevelGravitySystem>();
        var metrics = entityManager.System<SharedZLevelMetricsSystem>();
        var skyExposure = entityManager.System<SharedZLevelSkyExposureSystem>();

        foreach (var sample in fixture.GravitySamples
                     .GroupBy(sample => sample.GridUid)
                     .Select(group => group.First()))
        {
            gravity.TryGetGravityTarget(
                sample.GridUid,
                entityManager.GetComponent<MapGridComponent>(sample.GridUid),
                sample.Tile,
                sample.QueryLevel,
                out _);
        }

        foreach (var sample in fixture.BoundarySamples)
        {
            boundaries.InvalidateBoundary(sample.GridUid, sample.Tile, sample.LowerZ);
        }

        gravity.InvalidateGrid(fixture.StationGridUid);
        gravity.InvalidateGrid(fixture.MovingGridUid);
        skyExposure.InvalidateAll();
        metrics.ResetCounters();
    }

    private static ZLevelStressRunSnapshot CaptureRun(
        IEntityManager entityManager,
        ICommonSession session,
        ZLevelStressFixture fixture,
        int iterations,
        bool moveGrid)
    {
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var gravity = entityManager.System<SharedZLevelGravitySystem>();
        var metrics = entityManager.System<SharedZLevelMetricsSystem>();
        var pvs = entityManager.System<ZLevelPvsSystem>();
        var skyExposure = entityManager.System<SharedZLevelSkyExposureSystem>();
        var transform = entityManager.System<SharedTransformSystem>();
        var visibility = entityManager.System<SharedZLevelVisibilitySystem>();
        var grids = new Dictionary<EntityUid, MapGridComponent>
        {
            [fixture.StationGridUid] = entityManager.GetComponent<MapGridComponent>(fixture.StationGridUid),
            [fixture.MovingGridUid] = entityManager.GetComponent<MapGridComponent>(fixture.MovingGridUid),
        };

        metrics.ResetCounters();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            foreach (var sample in fixture.BoundarySamples)
            {
                var grid = grids[sample.GridUid];
                boundaries.TryGetBoundary(
                    sample.GridUid,
                    grid,
                    sample.Tile,
                    sample.LowerZ,
                    sample.LowerZ + 1,
                    out _);
                visibility.IsTileVisibleFrom(
                    sample.GridUid,
                    grid,
                    sample.Tile,
                    transform.LocalToWorldZLevel(sample.GridUid, sample.LowerZ),
                    sample.LowerZ + 1,
                    allowAbove: true);
            }

            foreach (var sample in fixture.GravitySamples)
            {
                skyExposure.GetExposure(
                    (sample.GridUid, grids[sample.GridUid]),
                    new ZLevelTileIndices(sample.Tile.X, sample.Tile.Y, 0));
                gravity.TryGetGravityTarget(
                    sample.GridUid,
                    grids[sample.GridUid],
                    sample.Tile,
                    sample.QueryLevel,
                    out _);
            }

            if (moveGrid)
            {
                var gridTransform = entityManager.GetComponent<TransformComponent>(fixture.MovingGridUid);
                transform.SetLocalPosition(
                    fixture.MovingGridUid,
                    gridTransform.LocalPosition + new Vector2(0.25f, -0.125f));
                transform.SetLocalRotation(
                    fixture.MovingGridUid,
                    gridTransform.LocalRotation + Angle.FromDegrees(1));
            }

            pvs.RefreshSession(session);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - started;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new ZLevelStressRunSnapshot(
            iterations,
            elapsedTicks * 1000d / Stopwatch.Frequency,
            allocatedBytes,
            metrics.Snapshot());
    }

    private void AssertFixtureStructure(ZLevelStressFixture fixture)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var gravity = SEntMan.System<SharedZLevelGravitySystem>();
        var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var stationGrid = SEntMan.GetComponent<MapGridComponent>(fixture.StationGridUid);
        var movingGrid = SEntMan.GetComponent<MapGridComponent>(fixture.MovingGridUid);
        var movingTransform = SEntMan.GetComponent<TransformComponent>(fixture.MovingGridUid);
        var expectedBoundarySamples =
            (ZLevelStressFixtureBuilder.StationSize * ZLevelStressFixtureBuilder.StationSize +
             ZLevelStressFixtureBuilder.MovingGridSize * ZLevelStressFixtureBuilder.MovingGridSize) *
            (fixture.FloorCount - 1);
        var expectedCandidates = fixture.FloorCount * 12;

        Assert.Multiple(() =>
        {
            Assert.That(fixture.StationTileCount, Is.GreaterThan(fixture.MovingGridTileCount));
            Assert.That(fixture.OpenBoundaryCount, Is.GreaterThan(0));
            Assert.That(fixture.ClosedBoundaryCount, Is.GreaterThan(0));
            Assert.That(fixture.SealedColumnCount, Is.GreaterThan(0));
            Assert.That(fixture.BoundarySamples, Has.Count.EqualTo(expectedBoundarySamples));
            Assert.That(fixture.CandidateEntities, Has.Count.EqualTo(expectedCandidates));
            Assert.That(fixture.GravityGenerators, Has.Count.EqualTo(2));
            Assert.That(fixture.StationGridUid, Is.Not.EqualTo(fixture.MovingGridUid));
            Assert.That(fixture.GravitySamples.Any(sample => sample.GridUid == fixture.StationGridUid), Is.True);
            Assert.That(fixture.GravitySamples.Any(sample => sample.GridUid == fixture.MovingGridUid), Is.True);
            Assert.That(gravity.IsManagedGrid(fixture.StationGridUid), Is.True);
            Assert.That(gravity.IsManagedGrid(fixture.MovingGridUid), Is.True);
            Assert.That(map.GetAllNonEmptyZLevelTiles(fixture.StationGridUid, stationGrid).Count(),
                Is.EqualTo(fixture.StationTileCount));
            Assert.That(map.GetAllNonEmptyZLevelTiles(fixture.MovingGridUid, movingGrid).Count(),
                Is.EqualTo(fixture.MovingGridTileCount));
            Assert.That(transform.GetZLevelFrameOrigin((fixture.MovingGridUid, movingTransform)),
                Is.EqualTo(fixture.MovingGridFrameOrigin));
            Assert.That(zLevelMaps.TryValidate(fixture.MapUid, out _), Is.True);
        });
    }

    private static void AssertRunCoverage(
        ZLevelStressFixture fixture,
        int boundaryCacheCapacity,
        int skyExposureCacheCapacity,
        ZLevelStressRunSnapshot warmup,
        ZLevelStressRunSnapshot measured)
    {
        var warmupMetrics = warmup.Metrics;
        var measuredMetrics = measured.Metrics;

        Assert.Multiple(() =>
        {
            Assert.That(warmup.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(warmup.AllocatedBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(warmupMetrics.BoundaryQueries, Is.GreaterThan(fixture.BoundarySamples.Count));
            Assert.That(warmupMetrics.BoundaryCacheHits, Is.GreaterThan(0));
            Assert.That(warmupMetrics.BoundaryCacheMisses, Is.GreaterThan(0));
            Assert.That(warmupMetrics.SkyExposureQueries, Is.EqualTo(fixture.GravitySamples.Count));
            Assert.That(warmupMetrics.SkyExposureCacheMisses, Is.GreaterThan(0));
            Assert.That(warmupMetrics.SkyExposureBoundaryChecks, Is.GreaterThan(0));
            Assert.That(warmupMetrics.SkyExposureBudgetExhaustions, Is.Zero);
            Assert.That(warmupMetrics.SkyExposureExposed + warmupMetrics.SkyExposureBlocked,
                Is.EqualTo(warmupMetrics.SkyExposureQueries));
            Assert.That(warmupMetrics.VisibilityTileQueries, Is.GreaterThanOrEqualTo(fixture.BoundarySamples.Count));
            Assert.That(warmupMetrics.VisibilityBoundaryChecks, Is.GreaterThan(0));
            Assert.That(warmupMetrics.GravityQueries, Is.EqualTo(fixture.GravitySamples.Count));
            Assert.That(warmupMetrics.GravityBuilds, Is.EqualTo(2));
            Assert.That(warmupMetrics.GravityBuildSources, Is.EqualTo(2));
            Assert.That(warmupMetrics.PvsRefreshes, Is.EqualTo(WarmupIterations));
            Assert.That(warmupMetrics.PvsViewers, Is.GreaterThanOrEqualTo(1));
            Assert.That(warmupMetrics.PvsCandidates, Is.GreaterThanOrEqualTo(fixture.CandidateEntities.Count));
            Assert.That(warmupMetrics.PvsVisible + warmupMetrics.PvsCulled,
                Is.EqualTo(warmupMetrics.PvsCandidates));
            Assert.That(warmupMetrics.PvsVisibilityChecks, Is.GreaterThan(0));
            Assert.That(warmupMetrics.PvsBudgetExhaustions, Is.Zero);
            Assert.That(warmupMetrics.PvsFailOpenCandidates, Is.Zero);

            Assert.That(measured.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(measured.AllocatedBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(measuredMetrics.BoundaryCacheHits, Is.GreaterThan(0));
            Assert.That(measuredMetrics.SkyExposureQueries,
                Is.EqualTo(fixture.GravitySamples.Count * MeasuredIterations));
            Assert.That(measuredMetrics.SkyExposureCacheHits,
                Is.EqualTo(measuredMetrics.SkyExposureQueries));
            Assert.That(measuredMetrics.SkyExposureCacheMisses, Is.Zero);
            Assert.That(measuredMetrics.SkyExposureBoundaryChecks, Is.Zero);
            Assert.That(measuredMetrics.SkyExposureBudgetExhaustions, Is.Zero);
            Assert.That(measuredMetrics.GravityBuilds, Is.Zero);
            Assert.That(measuredMetrics.GravityCacheMisses, Is.Zero);
            Assert.That(measuredMetrics.GravityCacheHits,
                Is.EqualTo(fixture.GravitySamples.Count * MeasuredIterations));
            Assert.That(measuredMetrics.PvsRefreshes, Is.EqualTo(MeasuredIterations));
            Assert.That(measuredMetrics.PvsVisible + measuredMetrics.PvsCulled,
                Is.EqualTo(measuredMetrics.PvsCandidates));
            Assert.That(measuredMetrics.PvsVisibilityChecks, Is.GreaterThan(0));
            Assert.That(measuredMetrics.PvsBudgetExhaustions, Is.Zero);
            Assert.That(measuredMetrics.PvsFailOpenCandidates, Is.Zero);
        });

        if (fixture.BoundarySamples.Count > boundaryCacheCapacity)
        {
            Assert.That(warmupMetrics.BoundaryEvictions, Is.GreaterThan(0),
                "The 10-floor fixture must exercise bounded-cache eviction.");
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(warmupMetrics.BoundaryEvictions, Is.Zero);
            Assert.That(measuredMetrics.BoundaryCacheMisses, Is.Zero,
                "A workload that fits the configured cache must be fully hot after warm-up.");
            Assert.That(fixture.GravitySamples.Count, Is.LessThanOrEqualTo(skyExposureCacheCapacity));
            Assert.That(warmupMetrics.SkyExposureEvictions, Is.Zero);
            Assert.That(measuredMetrics.SkyExposureEvictions, Is.Zero);
        });
    }

    private static ZLevelStressFixtureSnapshot CreateFixtureSnapshot(ZLevelStressFixture fixture)
    {
        return new ZLevelStressFixtureSnapshot(
            fixture.FloorCount,
            ZLevelStressFixtureBuilder.StationSize,
            ZLevelStressFixtureBuilder.MovingGridSize,
            fixture.StationTileCount,
            fixture.MovingGridTileCount,
            fixture.OpenBoundaryCount,
            fixture.ClosedBoundaryCount,
            fixture.SealedColumnCount,
            fixture.MovingGridFrameOrigin,
            fixture.CandidateEntities.Count,
            fixture.GravityGenerators.Count);
    }

    private static void WriteBaseline(ZLevelStressBaseline baseline)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(BaselineDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "zlevel-baselines");
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(
            outputDirectory,
            $"zlevel-baseline-{baseline.Fixture.FloorCount}-floors.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(baseline, options));
        TestContext.AddTestAttachment(path, $"WTZ Z-level {baseline.Fixture.FloorCount}-floor baseline");
        TestContext.Progress.WriteLine($"WTZ Z-level baseline: {path}");
    }
}

internal sealed record ZLevelStressBaseline(
    int SchemaVersion,
    ZLevelStressBudgetSnapshot Budgets,
    ZLevelStressFixtureSnapshot Fixture,
    ZLevelStressWorkloadSnapshot Workload,
    ZLevelStressRunSnapshot Warmup,
    ZLevelStressRunSnapshot Measured);

internal sealed record ZLevelStressBudgetSnapshot(
    int BoundaryCacheCapacity,
    int SkyExposureCacheCapacity,
    int SkyExposureMaxBoundaryChecks,
    int VisibilityMaxLevelDistance,
    int PvsVisibilityCheckBudget);

internal sealed record ZLevelStressFixtureSnapshot(
    int FloorCount,
    int StationSize,
    int MovingGridSize,
    int StationTileCount,
    int MovingGridTileCount,
    int OpenBoundaryCount,
    int ClosedBoundaryCount,
    int SealedColumnCount,
    int MovingGridFrameOrigin,
    int CandidateEntityCount,
    int GravityGeneratorCount);

internal sealed record ZLevelStressWorkloadSnapshot(
    int WarmupIterations,
    int MeasuredIterations,
    int BoundarySamplesPerIteration,
    int SkyExposureSamplesPerIteration,
    int GravitySamplesPerIteration);

internal sealed record ZLevelStressRunSnapshot(
    int Iterations,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    ZLevelMetricsSnapshot Metrics);
