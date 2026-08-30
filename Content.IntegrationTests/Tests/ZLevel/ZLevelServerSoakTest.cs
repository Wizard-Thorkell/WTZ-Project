// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.CCVar;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
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

/// <summary>
/// Runs a configurable, deterministic multi-session workload over native Z-level
/// caches, structural invalidation, PVS, sound routing, and moving-grid frames.
/// </summary>
public sealed class ZLevelServerSoakTest : GameTest
{
    private const string OutputDirectoryEnvironmentVariable = "WTZ_ZLEVEL_SOAK_DIR";
    private const int PvsSchedulerFramesPerCycle = 3;
    private const float PvsSchedulerFrameTime = ZLevelPvsSystem.TargetRefreshInterval /
        PvsSchedulerFramesPerCycle;
    private static readonly Vector2i[] MutationTiles =
    [
        new(10, 10),
        new(13, 10),
        new(10, 13),
        new(13, 13),
    ];

    [TestPrototypes]
    private const string SoakPrototypes = @"
- type: entity
  id: ZLevelServerSoakGravityGenerator
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

    public override PoolSettings PoolSettings => new() { Connected = false, DummyTicker = true };

    [Test]
    public async Task MultiSessionStructuralWorkloadStaysBoundedAndWritesReport()
    {
        var settings = ZLevelServerSoakSettings.FromEnvironment();
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
        await OverrideCVar(
            Side.Server,
            CCVars.ZLevelPvsMaxSessionRefreshesPerUpdate,
            Math.Max(
                ZLevelPvsSystem.DefaultMaxSessionRefreshesPerUpdate,
                (settings.SessionCount + PvsSchedulerFramesPerCycle - 1) /
                PvsSchedulerFramesPerCycle));

        var dummySessions = await Server.AddDummySessions(settings.SessionCount);
        await RunTicksSync(5);
        var testMap = await Pair.CreateTestMap(initialized: false);
        var sessions = new List<ICommonSession>(settings.SessionCount);
        var viewers = new List<EntityUid>(settings.SessionCount);
        var traversals = new List<EntityUid>();
        ZLevelStressFixture? fixture = null;
        ZLevelServerSoakReport? report = null;
        Vector2 movingGridStart = default;
        Angle movingGridRotation = default;
        var fixtureFloor = Tile.Empty;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapManager = Server.ResolveDependency<IMapManager>();
                var definitions = Server.ResolveDependency<ITileDefinitionManager>();
                var floor = (ContentTileDefinition) definitions["FloorSteel"];
                fixtureFloor = new Tile(floor.TileId);
                SEntMan.DeleteEntity(testMap.Grid);
                var stationGrid = mapManager.CreateGridEntity(testMap.MapId);
                fixture = ZLevelStressFixtureBuilder.Build(
                    SEntMan,
                    mapManager,
                    testMap.MapUid,
                    testMap.MapId,
                    stationGrid.Owner,
                    settings.FloorCount,
                    fixtureFloor,
                    "ZLevelServerSoakGravityGenerator",
                    settings.CandidateCopiesPerTile);

                var stressFixture = fixture!;
                var transform = SEntMan.System<SharedTransformSystem>();
                var zLevels = SEntMan.System<SharedZLevelSystem>();
                var map = SEntMan.System<SharedMapSystem>();
                movingGridStart = transform.GetWorldPosition(stressFixture.MovingGridUid);
                movingGridRotation = SEntMan.GetComponent<TransformComponent>(stressFixture.MovingGridUid)
                    .LocalRotation;
                map.InitializeMap(testMap.MapId);
                SEntMan.GetComponent<MapGridComponent>(stressFixture.StationGridUid).CanSplit = true;
                SEntMan.GetComponent<MapGridComponent>(stressFixture.MovingGridUid).CanSplit = true;
                SpawnTraversalStacks(SEntMan, stressFixture, traversals);

                for (var i = 0; i < dummySessions.Length; i++)
                {
                    var viewer = SEntMan.SpawnEntity(
                        null,
                        new EntityCoordinates(stressFixture.StationGridUid, new Vector2(10.5f, 10.5f)));
                    Assert.That(zLevels.SetZLevelPosition(viewer, i % settings.FloorCount), Is.True);
                    Assert.That(Server.PlayerMan.SetAttachedEntity(dummySessions[i], viewer), Is.True);
                    if (dummySessions[i].Status != SessionStatus.InGame)
                        Server.PlayerMan.SetStatus(dummySessions[i], SessionStatus.InGame);
                    sessions.Add(dummySessions[i]);
                    viewers.Add(viewer);
                }
            });

            await RunTicksSync(8);

            await Server.WaitAssertion(() =>
            {
                Assert.That(fixture, Is.Not.Null);
                var stressFixture = fixture!;
                AssertFixture(settings, stressFixture, sessions, viewers, traversals);
                PositionViewers(SEntMan, stressFixture, viewers, 0);

                var warmup = CaptureRun(
                    SEntMan,
                    stressFixture,
                    sessions,
                    viewers,
                    fixtureFloor,
                    settings.WarmupIterations);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var measured = CaptureRun(
                    SEntMan,
                    stressFixture,
                    sessions,
                    viewers,
                    fixtureFloor,
                    settings.MeasuredIterations);
                AssertRun(settings, stressFixture, measured);
                AssertRestored(stressFixture);

                report = new ZLevelServerSoakReport(
                    6,
                    DateTimeOffset.UtcNow,
                    CreateHostSnapshot(),
                    settings,
                    CreateBudgetSnapshot(SEntMan),
                    CreateFixtureSnapshot(stressFixture, traversals.Count),
                    warmup,
                    measured);
            });

            Assert.That(report, Is.Not.Null);
            WriteReport(report!);
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                var transform = SEntMan.System<SharedTransformSystem>();

                for (var i = 0; i < dummySessions.Length; i++)
                {
                    Server.PlayerMan.SetAttachedEntity(dummySessions[i], null);
                    if (i < viewers.Count && SEntMan.EntityExists(viewers[i]))
                        SEntMan.DeleteEntity(viewers[i]);
                }

                if (fixture is not { } stressFixture ||
                    !SEntMan.EntityExists(stressFixture.MovingGridUid))
                {
                    return;
                }

                transform.SetLocalPosition(stressFixture.MovingGridUid, movingGridStart);
                transform.SetLocalRotation(stressFixture.MovingGridUid, movingGridRotation);
            });

            foreach (var dummy in dummySessions)
                await Server.RemoveDummySession(dummy);
        }
    }

    private void AssertFixture(
        ZLevelServerSoakSettings settings,
        ZLevelStressFixture fixture,
        IReadOnlyCollection<ICommonSession> sessions,
        IReadOnlyCollection<EntityUid> viewers,
        IReadOnlyCollection<EntityUid> traversals)
    {
        var gravity = SEntMan.System<SharedZLevelGravitySystem>();
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
        var stationGrid = SEntMan.GetComponent<MapGridComponent>(fixture.StationGridUid);
        var movingGrid = SEntMan.GetComponent<MapGridComponent>(fixture.MovingGridUid);
        var movingTransform = SEntMan.GetComponent<TransformComponent>(fixture.MovingGridUid);
        var expectedCandidates = settings.FloorCount * 12 * settings.CandidateCopiesPerTile;

        Assert.Multiple(() =>
        {
            Assert.That(sessions, Has.Count.EqualTo(settings.SessionCount));
            Assert.That(viewers, Has.Count.EqualTo(settings.SessionCount));
            Assert.That(traversals, Has.Count.EqualTo(MutationTiles.Length * (settings.FloorCount - 1)));
            Assert.That(sessions, Has.All.Matches<ICommonSession>(session =>
                session.Status == SessionStatus.InGame && session.AttachedEntity != null));
            Assert.That(fixture.CandidateCopiesPerTile, Is.EqualTo(settings.CandidateCopiesPerTile));
            Assert.That(fixture.CandidateEntities, Has.Count.EqualTo(expectedCandidates));
            Assert.That(fixture.OpenBoundaryCount, Is.GreaterThan(0));
            Assert.That(fixture.ClosedBoundaryCount, Is.GreaterThan(0));
            Assert.That(fixture.GravityGenerators, Has.Count.EqualTo(2));
            Assert.That(fixture.GravityGenerators, Has.All.Matches<EntityUid>(uid =>
                SEntMan.GetComponent<GravityGeneratorComponent>(uid).GravityActive));
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

    private static void SpawnTraversalStacks(
        IEntityManager entityManager,
        ZLevelStressFixture fixture,
        List<EntityUid> traversals)
    {
        var graph = entityManager.System<ZLevelTraversalGraphSystem>();
        var transform = entityManager.System<SharedTransformSystem>();
        var zLevels = entityManager.System<SharedZLevelSystem>();

        foreach (var tile in MutationTiles)
        {
            for (var z = 0; z < fixture.FloorCount - 1; z++)
            {
                var traversal = entityManager.SpawnEntity(
                    null,
                    new EntityCoordinates(
                        fixture.StationGridUid,
                        new Vector2(tile.X + 0.5f, tile.Y + 0.5f)));
                Assert.That(zLevels.SetZLevelPosition(traversal, z), Is.True);
                var component = entityManager.EnsureComponent<ZLevelTraversalComponent>(traversal);
                component.Kind = ZLevelTraversalKind.Ladder;
                component.ZOffset = 1;
                transform.AnchorEntity(traversal, entityManager.GetComponent<TransformComponent>(traversal));
                graph.RefreshTraversal(traversal);
                traversals.Add(traversal);
            }
        }
    }

    private static ZLevelServerSoakRunSnapshot CaptureRun(
        IEntityManager entityManager,
        ZLevelStressFixture fixture,
        IReadOnlyList<ICommonSession> sessions,
        IReadOnlyList<EntityUid> viewers,
        Tile floorTile,
        int iterations)
    {
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var graph = entityManager.System<ZLevelTraversalGraphSystem>();
        var gravity = entityManager.System<SharedZLevelGravitySystem>();
        var map = entityManager.System<SharedMapSystem>();
        var metrics = entityManager.System<SharedZLevelMetricsSystem>();
        var playback = entityManager.System<ZLevelSoundPlaybackSystem>();
        var portals = entityManager.System<SharedZLevelSoundPortalSystem>();
        var pvs = entityManager.System<ZLevelPvsSystem>();
        var routes = entityManager.System<ZLevelSoundRouteSystem>();
        var skyExposure = entityManager.System<SharedZLevelSkyExposureSystem>();
        var transform = entityManager.System<SharedTransformSystem>();
        var visibility = entityManager.System<SharedZLevelVisibilitySystem>();
        var grids = new Dictionary<EntityUid, MapGridComponent>
        {
            [fixture.StationGridUid] = entityManager.GetComponent<MapGridComponent>(fixture.StationGridUid),
            [fixture.MovingGridUid] = entityManager.GetComponent<MapGridComponent>(fixture.MovingGridUid),
        };
        var routePortals = new List<ZLevelSoundPortal>();
        var iterationLatencyTicks = new long[iterations];
        var pvsRefreshLatencyTicks = new long[iterations * sessions.Count];
        var pvsSchedulerFrameLatencyTicks = new long[iterations * PvsSchedulerFramesPerCycle];
        var stageRecorder = new ZLevelServerSoakStageRecorder(iterations);
        var pvsLatencyIndex = 0;
        var pvsSchedulerFrameLatencyIndex = 0;
        Action<long> observePvsRefreshLatency = ticks =>
            pvsRefreshLatencyTicks[pvsLatencyIndex++] = ticks;

        metrics.ResetCounters();
        portals.ResetMetrics();
        routes.ResetMetrics();
        playback.ResetMetrics();
        graph.ResetMetrics();
        pvs.ResetSchedulerMetrics();
        pvs.ResetSchedulerState();

        var collectionZeroBefore = GC.CollectionCount(0);
        var collectionOneBefore = GC.CollectionCount(1);
        var collectionTwoBefore = GC.CollectionCount(2);
        var heapBefore = GC.GetTotalMemory(false);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var generationZeroAtIterationStart = GC.CollectionCount(0);
            var generationOneAtIterationStart = GC.CollectionCount(1);
            var generationTwoAtIterationStart = GC.CollectionCount(2);
            var iterationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var iterationStarted = Stopwatch.GetTimestamp();

            var stageStarted = stageRecorder.Start();
            MoveGrid(entityManager, fixture, iteration);
            PositionViewers(entityManager, fixture, viewers, iteration);
            stageRecorder.Record(ZLevelServerSoakStage.FrameAndViewerUpdate, iteration, stageStarted);

            var mutationTile = MutationTiles[iteration % MutationTiles.Length];
            var mutationZ = 1 + iteration % (fixture.FloorCount - 1);
            var mutation = new ZLevelTileIndices(mutationTile.X, mutationTile.Y, mutationZ);

            stageStarted = stageRecorder.Start();
            map.SetZLevelTile(fixture.StationGridUid, grids[fixture.StationGridUid], mutation, Tile.Empty);
            stageRecorder.Record(ZLevelServerSoakStage.OpenMutation, iteration, stageStarted);
            try
            {
                stageStarted = stageRecorder.Start();
                QueryVerticalConsumers(
                    entityManager,
                    fixture,
                    grids,
                    boundaries,
                    gravity,
                    skyExposure,
                    transform,
                    visibility);
                stageRecorder.Record(ZLevelServerSoakStage.OpenVerticalConsumers, iteration, stageStarted);

                stageStarted = stageRecorder.Start();
                RouteAcrossStableShaft(
                    fixture,
                    grids[fixture.StationGridUid],
                    iteration,
                    routes,
                    routePortals);
                stageRecorder.Record(ZLevelServerSoakStage.SoundRoute, iteration, stageStarted);

                stageStarted = stageRecorder.Start();
                var openSnapshot = graph.CreateSnapshot(fixture.MapId);
                Assert.That(
                    graph.ValidateSnapshot(openSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
                var cachedOpenSnapshot = graph.CreateSnapshot(fixture.MapId);
                Assert.That(
                    graph.ValidateSnapshot(cachedOpenSnapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
                stageRecorder.Record(ZLevelServerSoakStage.OpenTraversalGraph, iteration, stageStarted);

                stageStarted = stageRecorder.Start();
                for (var frame = 0; frame < PvsSchedulerFramesPerCycle; frame++)
                {
                    var frameStarted = Stopwatch.GetTimestamp();
                    pvs.RefreshScheduledSessions(PvsSchedulerFrameTime, observePvsRefreshLatency);
                    pvsSchedulerFrameLatencyTicks[pvsSchedulerFrameLatencyIndex++] =
                        Stopwatch.GetTimestamp() - frameStarted;
                }
                stageRecorder.Record(ZLevelServerSoakStage.PvsRefreshCycle, iteration, stageStarted);
            }
            finally
            {
                stageStarted = stageRecorder.Start();
                try
                {
                    map.SetZLevelTile(fixture.StationGridUid, grids[fixture.StationGridUid], mutation, floorTile);
                    gravity.Update(0f);
                }
                finally
                {
                    stageRecorder.Record(ZLevelServerSoakStage.RestoreMutation, iteration, stageStarted);
                }
            }

            stageStarted = stageRecorder.Start();
            boundaries.TryGetBoundary(
                fixture.StationGridUid,
                grids[fixture.StationGridUid],
                mutationTile,
                mutationZ - 1,
                mutationZ,
                out _);
            skyExposure.GetExposure(
                (fixture.StationGridUid, grids[fixture.StationGridUid]),
                new ZLevelTileIndices(mutationTile.X, mutationTile.Y, 0));
            gravity.TryGetGravityTarget(
                fixture.StationGridUid,
                grids[fixture.StationGridUid],
                mutationTile,
                fixture.FloorCount - 1,
                out _);
            stageRecorder.Record(ZLevelServerSoakStage.RestoredConsumers, iteration, stageStarted);

            stageStarted = stageRecorder.Start();
            var restoredSnapshot = graph.CreateSnapshot(fixture.MapId);
            Assert.That(graph.ValidateSnapshot(restoredSnapshot), Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
            var cachedRestoredSnapshot = graph.CreateSnapshot(fixture.MapId);
            Assert.That(
                graph.ValidateSnapshot(cachedRestoredSnapshot),
                Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
            stageRecorder.Record(ZLevelServerSoakStage.RestoredTraversalGraph, iteration, stageStarted);

            var iterationElapsedTicks = Stopwatch.GetTimestamp() - iterationStarted;
            iterationLatencyTicks[iteration] = iterationElapsedTicks;
            var iterationAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - iterationAllocatedBefore;
            var collectionOccurred = GC.CollectionCount(0) != generationZeroAtIterationStart ||
                GC.CollectionCount(1) != generationOneAtIterationStart ||
                GC.CollectionCount(2) != generationTwoAtIterationStart;
            stageRecorder.CompleteIteration(
                iteration,
                iterationElapsedTicks,
                iterationAllocatedBytes,
                collectionOccurred);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - started;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        var heapBeforeCollection = GC.GetTotalMemory(false);
        var sharedMetrics = metrics.Snapshot();
        var portalMetrics = portals.Snapshot();
        var routeMetrics = routes.Snapshot();
        var playbackMetrics = playback.Snapshot();
        var graphMetrics = graph.Snapshot();
        var pvsSchedulerMetrics = pvs.SchedulerMetrics;
        var generationZeroCollections = GC.CollectionCount(0) - collectionZeroBefore;
        var generationOneCollections = GC.CollectionCount(1) - collectionOneBefore;
        var generationTwoCollections = GC.CollectionCount(2) - collectionTwoBefore;
        var iterationLatency = CreateLatencySnapshot(iterationLatencyTicks);
        var pvsRefreshLatency = CreateLatencySnapshot(pvsRefreshLatencyTicks);
        var pvsSchedulerFrameLatency = CreateLatencySnapshot(pvsSchedulerFrameLatencyTicks);
        var stageSnapshots = stageRecorder.CreateStageSnapshots();
        var collectionCorrelation = stageRecorder.CreateCollectionCorrelation();
        var state = CreateRuntimeState(
            boundaries,
            gravity,
            skyExposure,
            portalMetrics,
            graphMetrics);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var heapAfterCollection = GC.GetTotalMemory(false);

        return new ZLevelServerSoakRunSnapshot(
            iterations,
            sessions.Count,
            elapsedTicks * 1000d / Stopwatch.Frequency,
            allocatedBytes,
            heapBefore,
            heapBeforeCollection,
            heapAfterCollection,
            generationZeroCollections,
            generationOneCollections,
            generationTwoCollections,
            iterationLatency,
            pvsRefreshLatency,
            pvsSchedulerFrameLatency,
            stageSnapshots,
            collectionCorrelation,
            pvsSchedulerMetrics,
            sharedMetrics,
            portalMetrics,
            routeMetrics,
            playbackMetrics,
            graphMetrics,
            state);
    }

    private static ZLevelServerSoakLatencySnapshot CreateLatencySnapshot(long[] samples)
    {
        Assert.That(samples, Is.Not.Empty);
        Array.Sort(samples);
        double totalTicks = 0d;
        foreach (var sample in samples)
            totalTicks += sample;

        return new ZLevelServerSoakLatencySnapshot(
            samples.Length,
            ToMilliseconds(samples[0]),
            ToMilliseconds(totalTicks / samples.Length),
            ToMilliseconds(Percentile(samples, 0.50d)),
            ToMilliseconds(Percentile(samples, 0.95d)),
            ToMilliseconds(Percentile(samples, 0.99d)),
            ToMilliseconds(samples[^1]));
    }

    private static long Percentile(long[] sortedSamples, double percentile)
    {
        var rank = (int) Math.Ceiling(percentile * sortedSamples.Length) - 1;
        return sortedSamples[Math.Clamp(rank, 0, sortedSamples.Length - 1)];
    }

    private static double ToMilliseconds(double timestampTicks)
    {
        return timestampTicks * 1000d / Stopwatch.Frequency;
    }

    private static void QueryVerticalConsumers(
        IEntityManager entityManager,
        ZLevelStressFixture fixture,
        IReadOnlyDictionary<EntityUid, MapGridComponent> grids,
        SharedZLevelBoundarySystem boundaries,
        SharedZLevelGravitySystem gravity,
        SharedZLevelSkyExposureSystem skyExposure,
        SharedTransformSystem transform,
        SharedZLevelVisibilitySystem visibility)
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
            var grid = grids[sample.GridUid];
            skyExposure.GetExposure(
                (sample.GridUid, grid),
                new ZLevelTileIndices(sample.Tile.X, sample.Tile.Y, 0));
            gravity.TryGetGravityTarget(
                sample.GridUid,
                grid,
                sample.Tile,
                sample.QueryLevel,
                out _);
        }
    }

    private static void RouteAcrossStableShaft(
        ZLevelStressFixture fixture,
        MapGridComponent stationGrid,
        int iteration,
        ZLevelSoundRouteSystem routes,
        List<ZLevelSoundPortal> routePortals)
    {
        var targetZ = 1 + iteration % Math.Min(4, fixture.FloorCount - 1);
        var position = new Vector2(11.5f, 11.5f);
        var source = new ZLevelSoundRouteEndpoint(fixture.StationGridUid, position, 0);
        var listener = new ZLevelSoundRouteEndpoint(fixture.StationGridUid, position, targetZ);
        routePortals.Clear();
        var result = routes.FindRoute(
            (fixture.StationGridUid, stationGrid),
            source,
            listener,
            16f,
            routePortals,
            ZLevelSoundMediumMode.Ignore);
        Assert.That(
            result.Succeeded,
            Is.True,
            $"Stable shaft route failed at iteration {iteration}: {result.Status}/{result.PortalStatus}.");
        Assert.That(result.Crossings, Is.EqualTo(targetZ));
    }

    private static void MoveGrid(IEntityManager entityManager, ZLevelStressFixture fixture, int iteration)
    {
        var transform = entityManager.System<SharedTransformSystem>();
        var gridTransform = entityManager.GetComponent<TransformComponent>(fixture.MovingGridUid);
        var direction = iteration % 2 == 0 ? 1f : -0.5f;
        transform.SetLocalPosition(
            fixture.MovingGridUid,
            gridTransform.LocalPosition + new Vector2(0.125f * direction, -0.0625f * direction));
        transform.SetLocalRotation(
            fixture.MovingGridUid,
            gridTransform.LocalRotation + Angle.FromDegrees(0.5f));
    }

    private static void PositionViewers(
        IEntityManager entityManager,
        ZLevelStressFixture fixture,
        IReadOnlyList<EntityUid> viewers,
        int iteration)
    {
        var transform = entityManager.System<SharedTransformSystem>();
        var zLevels = entityManager.System<SharedZLevelSystem>();

        for (var index = 0; index < viewers.Count; index++)
        {
            var onMovingGrid = (index + iteration) % 3 == 1;
            var gridUid = onMovingGrid ? fixture.MovingGridUid : fixture.StationGridUid;
            var x = onMovingGrid ? 2.5f + index % 3 : 9.5f + index % 6;
            var y = onMovingGrid ? 2.5f + index % 2 : 10.5f + (index / 2) % 4;
            var localZ = (index * 3 + iteration) % fixture.FloorCount;
            transform.SetCoordinates(viewers[index], new EntityCoordinates(gridUid, new Vector2(x, y)));
            Assert.That(zLevels.SetZLevelPosition(viewers[index], localZ), Is.True);
        }
    }

    private static void AssertRun(
        ZLevelServerSoakSettings settings,
        ZLevelStressFixture fixture,
        ZLevelServerSoakRunSnapshot run)
    {
        var expectedRefreshes = (long) settings.SessionCount * settings.MeasuredIterations;
        var metrics = run.SharedMetrics;
        var portals = run.SoundPortals;
        var routes = run.SoundRoutes;
        var playback = run.SoundPlayback;
        var graph = run.TraversalGraph;
        var scheduler = run.PvsScheduler;
        var expectedSchedulerUpdates = (long) settings.MeasuredIterations * PvsSchedulerFramesPerCycle;
        var expectedSchedulerLimit = Math.Max(
            ZLevelPvsSystem.DefaultMaxSessionRefreshesPerUpdate,
            (settings.SessionCount + PvsSchedulerFramesPerCycle - 1) /
            PvsSchedulerFramesPerCycle);

        Assert.Multiple(() =>
        {
            Assert.That(double.IsFinite(run.ElapsedMilliseconds), Is.True);
            Assert.That(run.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(run.AllocatedBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.HeapBytesBefore, Is.GreaterThan(0));
            Assert.That(run.HeapBytesBeforeCollection, Is.GreaterThan(0));
            Assert.That(run.HeapBytesAfterCollection, Is.GreaterThan(0));
            Assert.That(run.GenerationZeroCollections, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.GenerationOneCollections, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.GenerationTwoCollections, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.IterationLatency.Samples, Is.EqualTo(settings.MeasuredIterations));
            Assert.That(run.PvsRefreshLatency.Samples, Is.EqualTo(expectedRefreshes));
            Assert.That(run.PvsSchedulerFrameLatency.Samples, Is.EqualTo(expectedSchedulerUpdates));
            Assert.That(run.Stages, Has.Count.EqualTo(Enum.GetValues<ZLevelServerSoakStage>().Length));
            Assert.That(run.Stages.Select(stage => stage.Name), Is.Unique);
            Assert.That(run.Stages.Sum(stage => stage.AllocatedBytes), Is.EqualTo(run.AllocatedBytes));
            Assert.That(
                run.CollectionCorrelation.IterationsWithCollection +
                run.CollectionCorrelation.IterationsWithoutCollection,
                Is.EqualTo(settings.MeasuredIterations));
            Assert.That(double.IsFinite(run.IterationLatency.P99Milliseconds), Is.True);
            Assert.That(double.IsFinite(run.PvsRefreshLatency.P99Milliseconds), Is.True);
            Assert.That(double.IsFinite(run.PvsSchedulerFrameLatency.P99Milliseconds), Is.True);
            Assert.That(run.IterationLatency.P50Milliseconds,
                Is.LessThanOrEqualTo(run.IterationLatency.MaxMilliseconds));
            Assert.That(run.IterationLatency.P95Milliseconds,
                Is.LessThanOrEqualTo(run.IterationLatency.MaxMilliseconds));
            Assert.That(run.IterationLatency.P99Milliseconds,
                Is.LessThanOrEqualTo(run.IterationLatency.MaxMilliseconds));
            Assert.That(run.PvsRefreshLatency.P50Milliseconds,
                Is.LessThanOrEqualTo(run.PvsRefreshLatency.MaxMilliseconds));
            Assert.That(run.PvsRefreshLatency.P95Milliseconds,
                Is.LessThanOrEqualTo(run.PvsRefreshLatency.MaxMilliseconds));
            Assert.That(run.PvsRefreshLatency.P99Milliseconds,
                Is.LessThanOrEqualTo(run.PvsRefreshLatency.MaxMilliseconds));
            Assert.That(run.PvsSchedulerFrameLatency.P50Milliseconds,
                Is.LessThanOrEqualTo(run.PvsSchedulerFrameLatency.MaxMilliseconds));
            Assert.That(run.PvsSchedulerFrameLatency.P95Milliseconds,
                Is.LessThanOrEqualTo(run.PvsSchedulerFrameLatency.MaxMilliseconds));
            Assert.That(run.PvsSchedulerFrameLatency.P99Milliseconds,
                Is.LessThanOrEqualTo(run.PvsSchedulerFrameLatency.MaxMilliseconds));

            Assert.That(metrics.BoundaryQueries, Is.GreaterThan(fixture.BoundarySamples.Count));
            Assert.That(metrics.BoundaryInvalidations, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(metrics.SkyExposureQueries, Is.GreaterThan(fixture.GravitySamples.Count));
            Assert.That(metrics.SkyExposureInvalidations, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(metrics.SkyExposureBudgetExhaustions, Is.Zero);
            Assert.That(metrics.GravityQueries, Is.GreaterThan(fixture.GravitySamples.Count));
            Assert.That(metrics.GravityInvalidations, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(metrics.GravityBuilds, Is.GreaterThan(0));
            Assert.That(metrics.GravityReusedBuilds, Is.EqualTo(metrics.GravityBuilds));
            Assert.That(metrics.PvsRefreshes, Is.EqualTo(expectedRefreshes));
            Assert.That(metrics.PvsViewers, Is.GreaterThanOrEqualTo(expectedRefreshes));
            Assert.That(metrics.PvsCandidates, Is.GreaterThan(fixture.CandidateEntities.Count));
            Assert.That(metrics.PvsVisible + metrics.PvsCulled, Is.EqualTo(metrics.PvsCandidates));
            Assert.That(metrics.PvsVisibilityChecks, Is.GreaterThan(0));
            Assert.That(metrics.PvsBudgetExhaustions, Is.Zero);
            Assert.That(metrics.PvsFailOpenCandidates, Is.Zero);

            Assert.That(scheduler.Updates, Is.EqualTo(expectedSchedulerUpdates));
            Assert.That(scheduler.ActiveSessionSamples,
                Is.EqualTo(expectedSchedulerUpdates * settings.SessionCount));
            Assert.That(scheduler.DueRefreshes, Is.EqualTo(expectedRefreshes));
            Assert.That(scheduler.ScheduledRefreshes, Is.EqualTo(expectedRefreshes));
            Assert.That(scheduler.DeferredRefreshes, Is.Zero);
            Assert.That(scheduler.BudgetExhaustions, Is.Zero);
            Assert.That(scheduler.MaxActiveSessions, Is.EqualTo(settings.SessionCount));
            Assert.That(scheduler.MaxRefreshesPerUpdate,
                Is.LessThanOrEqualTo(expectedSchedulerLimit));
            Assert.That(scheduler.MaxDeferredRefreshesPerUpdate, Is.Zero);
            Assert.That(double.IsFinite(scheduler.MaxRefreshMilliseconds), Is.True);
            Assert.That(scheduler.VisibilityContextCacheHits, Is.GreaterThan(0));
            Assert.That(scheduler.VisibilityContextCacheMisses, Is.GreaterThan(0));
            Assert.That(scheduler.VisibilityContextCacheHitPercent, Is.GreaterThan(0d));
            Assert.That(
                scheduler.VisibilityContextCacheHits + scheduler.VisibilityContextCacheMisses,
                Is.EqualTo(metrics.PvsVisibilityChecks));
            Assert.That(scheduler.VisibilityContextCacheEntries, Is.GreaterThan(0));
            Assert.That(scheduler.VisibilityContextCacheEntries,
                Is.LessThanOrEqualTo(scheduler.VisibilityContextCacheMaxEntries));
            Assert.That(scheduler.VisibilityContextCacheMaxEntries,
                Is.LessThanOrEqualTo(scheduler.VisibilityContextCacheMisses));

            Assert.That(portals.Invalidations, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(portals.Builds, Is.GreaterThan(0));
            Assert.That(portals.ChunkBudgetExhaustions, Is.Zero);
            Assert.That(portals.BuildBudgetExhaustions, Is.Zero);
            Assert.That(portals.CandidateBudgetExhaustions, Is.Zero);
            Assert.That(routes.Queries, Is.EqualTo(settings.MeasuredIterations));
            Assert.That(routes.VerticalSuccesses, Is.EqualTo(settings.MeasuredIterations));
            Assert.That(routes.CrossingLimitExhaustions, Is.Zero);
            Assert.That(routes.PortalChunkBudgetExhaustions, Is.Zero);
            Assert.That(routes.PortalBuildBudgetExhaustions, Is.Zero);
            Assert.That(routes.PortalCandidateBudgetExhaustions, Is.Zero);
            Assert.That(routes.EdgeBudgetExhaustions, Is.Zero);
            Assert.That(routes.MediumSampleBudgetExhaustions, Is.Zero);

            Assert.That(playback.Refreshes, Is.EqualTo(expectedRefreshes));
            Assert.That(playback.RouteBudgetExhaustions, Is.Zero);
            Assert.That(playback.PresentationBudgetExhaustions, Is.Zero);
            Assert.That(playback.ParentDepthFailures, Is.Zero);

            Assert.That(graph.SnapshotRequests, Is.EqualTo(settings.MeasuredIterations * 4));
            Assert.That(graph.SnapshotBuilds, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(graph.SnapshotCacheHits, Is.GreaterThanOrEqualTo(settings.MeasuredIterations * 2));
            Assert.That(graph.Nodes, Is.EqualTo(MutationTiles.Length * (settings.FloorCount - 1)));
            Assert.That(graph.ConnectedBudgetExhaustions, Is.Zero);
            Assert.That(graph.InvalidEdges, Is.Zero);
            Assert.That(graph.InvalidFlightEdges, Is.Zero);

            Assert.That(run.RuntimeState.BoundaryCacheEntries,
                Is.LessThanOrEqualTo(run.RuntimeState.BoundaryCacheCapacity));
            Assert.That(run.RuntimeState.SkyExposureCacheEntries,
                Is.LessThanOrEqualTo(run.RuntimeState.SkyExposureCacheCapacity));
            Assert.That(run.RuntimeState.SoundPortalCacheEntries,
                Is.LessThanOrEqualTo(run.RuntimeState.SoundPortalCacheCapacity));
            Assert.That(run.RuntimeState.GravityPendingRefreshGrids, Is.Zero);
            Assert.That(run.RuntimeState.TraversalCachedSnapshots, Is.LessThanOrEqualTo(1));
        });

        foreach (var stage in run.Stages)
        {
            Assert.Multiple(() =>
            {
                Assert.That(stage.Latency.Samples, Is.EqualTo(settings.MeasuredIterations));
                Assert.That(stage.AllocatedBytes, Is.GreaterThanOrEqualTo(0));
                Assert.That(double.IsFinite(stage.Latency.P99Milliseconds), Is.True);
                Assert.That(stage.Latency.P50Milliseconds,
                    Is.LessThanOrEqualTo(stage.Latency.MaxMilliseconds));
                Assert.That(stage.Latency.P95Milliseconds,
                    Is.LessThanOrEqualTo(stage.Latency.MaxMilliseconds));
                Assert.That(stage.Latency.P99Milliseconds,
                    Is.LessThanOrEqualTo(stage.Latency.MaxMilliseconds));
            });
        }

        AssertCorrelationLatency(
            run.CollectionCorrelation.WithCollectionLatency,
            run.CollectionCorrelation.IterationsWithCollection);
        AssertCorrelationLatency(
            run.CollectionCorrelation.WithoutCollectionLatency,
            run.CollectionCorrelation.IterationsWithoutCollection);
    }

    private static void AssertCorrelationLatency(
        ZLevelServerSoakLatencySnapshot? latency,
        int expectedSamples)
    {
        if (expectedSamples == 0)
        {
            Assert.That(latency, Is.Null);
            return;
        }

        Assert.That(latency, Is.Not.Null);
        Assert.That(latency!.Samples, Is.EqualTo(expectedSamples));
        Assert.That(double.IsFinite(latency.P99Milliseconds), Is.True);
    }

    private void AssertRestored(ZLevelStressFixture fixture)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var stationGrid = SEntMan.GetComponent<MapGridComponent>(fixture.StationGridUid);
        var movingGrid = SEntMan.GetComponent<MapGridComponent>(fixture.MovingGridUid);

        Assert.Multiple(() =>
        {
            Assert.That(map.GetAllNonEmptyZLevelTiles(fixture.StationGridUid, stationGrid).Count(),
                Is.EqualTo(fixture.StationTileCount));
            Assert.That(map.GetAllNonEmptyZLevelTiles(fixture.MovingGridUid, movingGrid).Count(),
                Is.EqualTo(fixture.MovingGridTileCount));
            Assert.That(SEntMan.System<SharedZLevelMapSystem>().TryValidate(fixture.MapUid, out _), Is.True);
        });
    }

    private static ZLevelServerSoakBudgetSnapshot CreateBudgetSnapshot(IEntityManager entityManager)
    {
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var playback = entityManager.System<ZLevelSoundPlaybackSystem>();
        var portals = entityManager.System<SharedZLevelSoundPortalSystem>();
        var pvs = entityManager.System<ZLevelPvsSystem>();
        var routes = entityManager.System<ZLevelSoundRouteSystem>();
        var sky = entityManager.System<SharedZLevelSkyExposureSystem>();
        var visibility = entityManager.System<SharedZLevelVisibilitySystem>();

        return new ZLevelServerSoakBudgetSnapshot(
            boundaries.BoundaryCacheCapacity,
            sky.CacheCapacity,
            sky.MaxBoundaryChecks,
            visibility.MaxVisibleLevelDistance,
            pvs.VisibilityCheckBudget,
            pvs.MaxSessionRefreshesPerUpdate,
            portals.CacheCapacity,
            routes.MaxCrossings,
            routes.MaxPortalChunks,
            routes.MaxPortalBuilds,
            routes.MaxPortalCandidates,
            routes.MaxEdges,
            routes.MaxMediumSamples,
            playback.MaxRouteChecksPerRefresh,
            playback.MaxPresentationsPerRefresh);
    }

    private static ZLevelServerSoakRuntimeState CreateRuntimeState(
        SharedZLevelBoundarySystem boundaries,
        SharedZLevelGravitySystem gravity,
        SharedZLevelSkyExposureSystem sky,
        ZLevelSoundPortalCacheMetrics portals,
        ZLevelTraversalGraphMetricsSnapshot graph)
    {
        return new ZLevelServerSoakRuntimeState(
            boundaries.CachedBoundaryCount,
            boundaries.BoundaryCacheCapacity,
            sky.CachedExposureCount,
            sky.CacheCapacity,
            gravity.CachedGridCount,
            gravity.PendingRefreshGridCount,
            portals.CachedChunks,
            portals.CacheCapacity,
            graph.CachedSnapshots,
            graph.TrackedMapRevisions);
    }

    private static ZLevelServerSoakFixtureSnapshot CreateFixtureSnapshot(
        ZLevelStressFixture fixture,
        int traversalEntityCount)
    {
        return new ZLevelServerSoakFixtureSnapshot(
            fixture.FloorCount,
            ZLevelStressFixtureBuilder.StationSize,
            ZLevelStressFixtureBuilder.MovingGridSize,
            fixture.StationTileCount,
            fixture.MovingGridTileCount,
            fixture.OpenBoundaryCount,
            fixture.ClosedBoundaryCount,
            fixture.SealedColumnCount,
            fixture.MovingGridFrameOrigin,
            fixture.CandidateCopiesPerTile,
            fixture.CandidateEntities.Count,
            fixture.GravityGenerators.Count,
            traversalEntityCount);
    }

    private static ZLevelServerSoakHostSnapshot CreateHostSnapshot()
    {
        return new ZLevelServerSoakHostSnapshot(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            Environment.ProcessorCount,
            GCSettings.IsServerGC,
            GCSettings.LatencyMode.ToString(),
#if DEBUG
            "Debug");
#else
            "Release");
#endif
    }

    private static void WriteReport(ZLevelServerSoakReport report)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "zlevel-server-soak");
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "zlevel-server-soak.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
        TestContext.AddTestAttachment(path, "WTZ Z-level deterministic server soak");
        TestContext.Progress.WriteLine($"WTZ Z-level server soak: {path}");
    }

    internal enum ZLevelServerSoakStage : byte
    {
        FrameAndViewerUpdate,
        OpenMutation,
        OpenVerticalConsumers,
        SoundRoute,
        OpenTraversalGraph,
        PvsRefreshCycle,
        RestoreMutation,
        RestoredConsumers,
        RestoredTraversalGraph,
        Unattributed,
    }

    private readonly record struct ZLevelServerSoakStageStart(long TimestampTicks, long AllocatedBytes);

    private sealed class ZLevelServerSoakStageRecorder
    {
        private static readonly ZLevelServerSoakStage[] StageValues =
            Enum.GetValues<ZLevelServerSoakStage>();

        private readonly long[][] _latencyTicks;
        private readonly long[][] _allocatedBytes;
        private readonly long[] _iterationTicks;
        private readonly bool[] _collectionOccurred;

        public ZLevelServerSoakStageRecorder(int iterations)
        {
            var stageCount = StageValues.Length;
            _latencyTicks = new long[stageCount][];
            _allocatedBytes = new long[stageCount][];
            for (var stage = 0; stage < stageCount; stage++)
            {
                _latencyTicks[stage] = new long[iterations];
                _allocatedBytes[stage] = new long[iterations];
            }

            _iterationTicks = new long[iterations];
            _collectionOccurred = new bool[iterations];
        }

        public ZLevelServerSoakStageStart Start()
        {
            return new ZLevelServerSoakStageStart(
                Stopwatch.GetTimestamp(),
                GC.GetAllocatedBytesForCurrentThread());
        }

        public void Record(
            ZLevelServerSoakStage stage,
            int iteration,
            ZLevelServerSoakStageStart started)
        {
            var index = (int) stage;
            _latencyTicks[index][iteration] = Stopwatch.GetTimestamp() - started.TimestampTicks;
            _allocatedBytes[index][iteration] =
                GC.GetAllocatedBytesForCurrentThread() - started.AllocatedBytes;
        }

        public void CompleteIteration(
            int iteration,
            long totalTicks,
            long totalAllocatedBytes,
            bool collectionOccurred)
        {
            long attributedTicks = 0;
            long attributedBytes = 0;
            foreach (var stage in StageValues)
            {
                if (stage == ZLevelServerSoakStage.Unattributed)
                    continue;

                attributedTicks += _latencyTicks[(int) stage][iteration];
                attributedBytes += _allocatedBytes[(int) stage][iteration];
            }

            if (attributedTicks > totalTicks || attributedBytes > totalAllocatedBytes)
            {
                throw new InvalidOperationException(
                    $"Stage attribution exceeded iteration {iteration} totals: " +
                    $"ticks {attributedTicks}/{totalTicks}, bytes {attributedBytes}/{totalAllocatedBytes}.");
            }

            _latencyTicks[(int) ZLevelServerSoakStage.Unattributed][iteration] =
                totalTicks - attributedTicks;
            _allocatedBytes[(int) ZLevelServerSoakStage.Unattributed][iteration] =
                totalAllocatedBytes - attributedBytes;
            _iterationTicks[iteration] = totalTicks;
            _collectionOccurred[iteration] = collectionOccurred;
        }

        public IReadOnlyList<ZLevelServerSoakStageSnapshot> CreateStageSnapshots()
        {
            var snapshots = new List<ZLevelServerSoakStageSnapshot>(_latencyTicks.Length);
            foreach (var stage in StageValues)
            {
                var index = (int) stage;
                snapshots.Add(new ZLevelServerSoakStageSnapshot(
                    stage.ToString(),
                    CreateLatencySnapshot((long[]) _latencyTicks[index].Clone()),
                    _allocatedBytes[index].Sum()));
            }

            return snapshots;
        }

        public ZLevelServerSoakCollectionCorrelationSnapshot CreateCollectionCorrelation()
        {
            var withCollection = new List<long>();
            var withoutCollection = new List<long>();
            for (var iteration = 0; iteration < _iterationTicks.Length; iteration++)
            {
                var destination = _collectionOccurred[iteration]
                    ? withCollection
                    : withoutCollection;
                destination.Add(_iterationTicks[iteration]);
            }

            return new ZLevelServerSoakCollectionCorrelationSnapshot(
                withCollection.Count,
                withoutCollection.Count,
                withCollection.Count == 0 ? null : CreateLatencySnapshot(withCollection.ToArray()),
                withoutCollection.Count == 0 ? null : CreateLatencySnapshot(withoutCollection.ToArray()));
        }
    }
}

internal sealed record ZLevelServerSoakSettings(
    int FloorCount,
    int SessionCount,
    int WarmupIterations,
    int MeasuredIterations,
    int CandidateCopiesPerTile)
{
    private const string FloorsVariable = "WTZ_ZLEVEL_SOAK_FLOORS";
    private const string SessionsVariable = "WTZ_ZLEVEL_SOAK_SESSIONS";
    private const string WarmupVariable = "WTZ_ZLEVEL_SOAK_WARMUP";
    private const string IterationsVariable = "WTZ_ZLEVEL_SOAK_ITERATIONS";
    private const string CandidateCopiesVariable = "WTZ_ZLEVEL_SOAK_CANDIDATE_COPIES";

    public static ZLevelServerSoakSettings FromEnvironment()
    {
        return new ZLevelServerSoakSettings(
            ReadBounded(FloorsVariable, 10, 3, 32),
            ReadBounded(SessionsVariable, 4, 2, 64),
            ReadBounded(WarmupVariable, 2, 1, 128),
            ReadBounded(IterationsVariable, 8, 1, 2_048),
            ReadBounded(
                CandidateCopiesVariable,
                2,
                1,
                ZLevelStressFixtureBuilder.MaximumCandidateCopiesPerTile));
    }

    private static int ReadBounded(string variable, int defaultValue, int minimum, int maximum)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new InvalidOperationException(
                $"{variable} must be an integer from {minimum} through {maximum}; received '{value}'.");
        }

        return parsed;
    }
}

internal sealed record ZLevelServerSoakReport(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    ZLevelServerSoakHostSnapshot Host,
    ZLevelServerSoakSettings Settings,
    ZLevelServerSoakBudgetSnapshot Budgets,
    ZLevelServerSoakFixtureSnapshot Fixture,
    ZLevelServerSoakRunSnapshot Warmup,
    ZLevelServerSoakRunSnapshot Measured);

internal sealed record ZLevelServerSoakHostSnapshot(
    string OperatingSystem,
    string Framework,
    string ProcessArchitecture,
    int LogicalProcessorCount,
    bool ServerGarbageCollection,
    string GarbageCollectionLatencyMode,
    string BuildConfiguration);

internal sealed record ZLevelServerSoakBudgetSnapshot(
    int BoundaryCacheCapacity,
    int SkyExposureCacheCapacity,
    int SkyExposureMaxBoundaryChecks,
    int VisibilityMaxLevelDistance,
    int PvsVisibilityCheckBudget,
    int PvsMaxSessionRefreshesPerUpdate,
    int SoundPortalCacheCapacity,
    int SoundRouteMaxCrossings,
    int SoundRouteMaxPortalChunks,
    int SoundRouteMaxPortalBuilds,
    int SoundRouteMaxPortalCandidates,
    int SoundRouteMaxEdges,
    int SoundRouteMaxMediumSamples,
    int SoundPlaybackMaxRouteChecksPerRefresh,
    int SoundPlaybackMaxPresentationsPerRefresh);

internal sealed record ZLevelServerSoakFixtureSnapshot(
    int FloorCount,
    int StationSize,
    int MovingGridSize,
    int StationTileCount,
    int MovingGridTileCount,
    int OpenBoundaryCount,
    int ClosedBoundaryCount,
    int SealedColumnCount,
    int MovingGridFrameOrigin,
    int CandidateCopiesPerTile,
    int CandidateEntityCount,
    int GravityGeneratorCount,
    int TraversalEntityCount);

internal sealed record ZLevelServerSoakRunSnapshot(
    int Iterations,
    int Sessions,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    long HeapBytesBefore,
    long HeapBytesBeforeCollection,
    long HeapBytesAfterCollection,
    int GenerationZeroCollections,
    int GenerationOneCollections,
    int GenerationTwoCollections,
    ZLevelServerSoakLatencySnapshot IterationLatency,
    ZLevelServerSoakLatencySnapshot PvsRefreshLatency,
    ZLevelServerSoakLatencySnapshot PvsSchedulerFrameLatency,
    IReadOnlyList<ZLevelServerSoakStageSnapshot> Stages,
    ZLevelServerSoakCollectionCorrelationSnapshot CollectionCorrelation,
    ZLevelPvsSchedulerMetricsSnapshot PvsScheduler,
    ZLevelMetricsSnapshot SharedMetrics,
    ZLevelSoundPortalCacheMetrics SoundPortals,
    ZLevelSoundRouteMetrics SoundRoutes,
    ZLevelSoundPlaybackMetrics SoundPlayback,
    ZLevelTraversalGraphMetricsSnapshot TraversalGraph,
    ZLevelServerSoakRuntimeState RuntimeState)
{
    public double MillisecondsPerIteration => Iterations == 0 ? 0d : ElapsedMilliseconds / Iterations;
    public double MillisecondsPerSessionRefresh => Iterations == 0 || Sessions == 0
        ? 0d
        : ElapsedMilliseconds / (Iterations * Sessions);
    public long RetainedHeapDeltaBytes => HeapBytesAfterCollection - HeapBytesBefore;
}

internal sealed record ZLevelServerSoakLatencySnapshot(
    int Samples,
    double MinMilliseconds,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds);

internal sealed record ZLevelServerSoakStageSnapshot(
    string Name,
    ZLevelServerSoakLatencySnapshot Latency,
    long AllocatedBytes);

internal sealed record ZLevelServerSoakCollectionCorrelationSnapshot(
    int IterationsWithCollection,
    int IterationsWithoutCollection,
    ZLevelServerSoakLatencySnapshot? WithCollectionLatency,
    ZLevelServerSoakLatencySnapshot? WithoutCollectionLatency);

internal sealed record ZLevelServerSoakRuntimeState(
    int BoundaryCacheEntries,
    int BoundaryCacheCapacity,
    int SkyExposureCacheEntries,
    int SkyExposureCacheCapacity,
    int GravityCachedGrids,
    int GravityPendingRefreshGrids,
    int SoundPortalCacheEntries,
    int SoundPortalCacheCapacity,
    int TraversalCachedSnapshots,
    int TraversalTrackedMapRevisions);
