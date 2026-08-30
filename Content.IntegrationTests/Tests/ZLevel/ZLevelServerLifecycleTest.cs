// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Content.Server.ZLevel.Navigation;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

/// <summary>
/// Repeatedly creates, warms, and removes native Z-level maps while proving
/// every map/grid-owned server cache returns to its pre-cycle state.
/// </summary>
public sealed class ZLevelServerLifecycleTest : GameTest
{
    private const string OutputDirectoryVariable = "WTZ_ZLEVEL_LIFECYCLE_DIR";
    private const int FloorCount = 3;
    private const int GridSize = 6;

    public override PoolSettings PoolSettings => new() { Connected = false, DummyTicker = true };

    [Test]
    public async Task RepeatedNativeMapLifecycleReturnsOwnedCachesToBaseline()
    {
        var settings = ZLevelServerLifecycleSettings.FromEnvironment();
        if (settings.RequireServerGarbageCollection)
        {
            Assert.That(GCSettings.IsServerGC, Is.True,
                "The lifecycle release gate must execute inside a Server GC testhost.");
        }

        Tile floor = default;
        await Server.WaitAssertion(() =>
        {
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            floor = new Tile(((ContentTileDefinition) definitions["FloorSteel"]).TileId);
        });

        ZLevelServerLifecycleStateSnapshot baseline = default;
        await Server.WaitAssertion(() => baseline = CaptureState(SEntMan));
        for (var cycle = 0; cycle < settings.WarmupCycles; cycle++)
            await RunLifecycleCycle(floor, baseline, cycle, warmup: true);

        ForceFullCollection();
        await Server.WaitAssertion(() => baseline = CaptureState(SEntMan));
        var heapBefore = GC.GetTotalMemory(forceFullCollection: false);
        var generationZeroBefore = GC.CollectionCount(0);
        var generationOneBefore = GC.CollectionCount(1);
        var generationTwoBefore = GC.CollectionCount(2);
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var started = Stopwatch.GetTimestamp();
        var cycleTicks = new long[settings.MeasuredCycles];
        var maximumWarmState = baseline;

        for (var cycle = 0; cycle < settings.MeasuredCycles; cycle++)
        {
            var result = await RunLifecycleCycle(floor, baseline, cycle, warmup: false);
            cycleTicks[cycle] = result.ElapsedTicks;
            maximumWarmState = ZLevelServerLifecycleStateSnapshot.Max(
                maximumWarmState,
                result.WarmState);
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - started;
        var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        var heapBeforeCollection = GC.GetTotalMemory(forceFullCollection: false);
        ForceFullCollection();
        var heapAfterCollection = GC.GetTotalMemory(forceFullCollection: false);
        ZLevelServerLifecycleStateSnapshot finalState = default;
        await Server.WaitAssertion(() => finalState = CaptureState(SEntMan));
        Assert.That(finalState, Is.EqualTo(baseline));

        var report = new ZLevelServerLifecycleReport(
            1,
            DateTimeOffset.UtcNow,
            CreateHostSnapshot(),
            settings,
            baseline,
            maximumWarmState,
            finalState,
            ToMilliseconds(elapsedTicks),
            allocatedBytes,
            heapBefore,
            heapBeforeCollection,
            heapAfterCollection,
            GC.CollectionCount(0) - generationZeroBefore,
            GC.CollectionCount(1) - generationOneBefore,
            GC.CollectionCount(2) - generationTwoBefore,
            CreateLatencySnapshot(cycleTicks));
        WriteReport(report);
    }

    [Test]
    public async Task RemovingOneMapCompactsOrderCachesWithoutDroppingTheSurvivor()
    {
        Tile floor = default;
        ZLevelServerLifecycleStateSnapshot initialState = default;
        await Server.WaitAssertion(() =>
        {
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            floor = new Tile(((ContentTileDefinition) definitions["FloorSteel"]).TileId);
            initialState = CaptureState(SEntMan);
        });

        MapId survivorMap = MapId.Nullspace;
        MapId removedMap = MapId.Nullspace;
        try
        {
            await Server.WaitAssertion(() => survivorMap = CreateAndWarmMap(SEntMan, floor));
            ZLevelServerLifecycleStateSnapshot survivorState = default;
            await Server.WaitAssertion(() => survivorState = CaptureState(SEntMan));

            await Server.WaitAssertion(() =>
            {
                removedMap = CreateAndWarmMap(SEntMan, floor);
                var combined = CaptureState(SEntMan);
                Assert.That(combined.BoundaryCacheEntries, Is.GreaterThan(survivorState.BoundaryCacheEntries));
                Assert.That(combined.SoundPortalChunks, Is.GreaterThan(survivorState.SoundPortalChunks));
            });

            await Server.WaitPost(() => DeleteMapIfPresent(removedMap));
            removedMap = MapId.Nullspace;
            await Server.WaitIdleAsync();
            await Server.WaitAssertion(() =>
            {
                var current = CaptureState(SEntMan);
                Assert.That(current, Is.EqualTo(survivorState));
                Assert.That(current.BoundaryOrderTokens, Is.EqualTo(current.BoundaryCacheEntries));
                Assert.That(current.SoundOrderTokens, Is.EqualTo(current.SoundPortalChunks));
            });
        }
        finally
        {
            if (removedMap != MapId.Nullspace)
                await Server.WaitPost(() => DeleteMapIfPresent(removedMap));

            if (survivorMap != MapId.Nullspace)
                await Server.WaitPost(() => DeleteMapIfPresent(survivorMap));

            await Server.WaitIdleAsync();
        }

        await Server.WaitAssertion(() => Assert.That(CaptureState(SEntMan), Is.EqualTo(initialState)));
    }

    private async Task<ZLevelServerLifecycleCycleResult> RunLifecycleCycle(
        Tile floor,
        ZLevelServerLifecycleStateSnapshot expectedBaseline,
        int cycle,
        bool warmup)
    {
        MapId mapId = MapId.Nullspace;
        var warmState = default(ZLevelServerLifecycleStateSnapshot);
        var started = Stopwatch.GetTimestamp();

        try
        {
            await Server.WaitAssertion(() =>
            {
                Assert.That(CaptureState(SEntMan), Is.EqualTo(expectedBaseline),
                    $"Lifecycle state was dirty before {(warmup ? "warm-up" : "measured")} cycle {cycle}.");
                mapId = CreateAndWarmMap(SEntMan, floor);
                warmState = CaptureState(SEntMan);
                AssertWarmState(expectedBaseline, warmState, cycle, warmup);
            });
        }
        finally
        {
            if (mapId != MapId.Nullspace)
            {
                await Server.WaitPost(() =>
                {
                    var map = SEntMan.System<SharedMapSystem>();
                    if (map.MapExists(mapId))
                        map.DeleteMap(mapId);
                });
                await Server.WaitIdleAsync();
            }
        }

        await Server.WaitAssertion(() =>
        {
            Assert.That(CaptureState(SEntMan), Is.EqualTo(expectedBaseline),
                $"Lifecycle state leaked after {(warmup ? "warm-up" : "measured")} cycle {cycle}.");
        });

        return new ZLevelServerLifecycleCycleResult(
            warmState,
            Stopwatch.GetTimestamp() - started);
    }

    private MapId CreateAndWarmMap(IEntityManager entityManager, Tile floor)
    {
        var mapManager = Server.ResolveDependency<IMapManager>();
        var map = entityManager.System<SharedMapSystem>();
        var zLevelMaps = entityManager.System<SharedZLevelMapSystem>();
        var zLevels = entityManager.System<SharedZLevelSystem>();
        var transform = entityManager.System<SharedTransformSystem>();
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var gravity = entityManager.System<SharedZLevelGravitySystem>();
        var sky = entityManager.System<SharedZLevelSkyExposureSystem>();
        var portals = entityManager.System<SharedZLevelSoundPortalSystem>();
        var graph = entityManager.System<ZLevelTraversalGraphSystem>();

        var mapUid = map.CreateMap(out var mapId, runMapInit: false);
        var grid = mapManager.CreateGridEntity(mapId);
        grid.Comp.CanSplit = false;
        zLevelMaps.Configure(
            mapUid,
            0,
            FloorCount - 1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);

        for (var z = 0; z < FloorCount; z++)
        {
            for (var x = 0; x < GridSize; x++)
            {
                for (var y = 0; y < GridSize; y++)
                    map.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(x, y, z), floor);
            }
        }

        map.InitializeMap(mapId);
        var coordinates = new EntityCoordinates(grid.Owner, new Vector2(1.5f, 1.5f));
        var connector = entityManager.SpawnEntity(null, coordinates);
        Assert.That(zLevels.SetZLevelPosition(connector, 0), Is.True);
        var boundary = entityManager.EnsureComponent<ZLevelBoundaryComponent>(connector);
        boundaries.SetBoundary(
            (connector, boundary),
            true,
            1,
            ZLevelBoundaryChannels.Sound,
            ZLevelBoundaryChannels.None);
        var traversal = entityManager.EnsureComponent<ZLevelTraversalComponent>(connector);
        traversal.Kind = ZLevelTraversalKind.Ladder;
        traversal.ZOffset = 1;
        traversal.TraversalDelay = TimeSpan.Zero;
        transform.AnchorEntity(connector, entityManager.GetComponent<TransformComponent>(connector));
        graph.RefreshTraversal(connector);

        for (var x = 0; x < GridSize; x++)
        {
            for (var y = 0; y < GridSize; y++)
            {
                var tile = new Vector2i(x, y);
                for (var lowerZ = 0; lowerZ < FloorCount - 1; lowerZ++)
                    Assert.That(boundaries.TryGetBoundary(grid.Owner, grid.Comp, tile, lowerZ, lowerZ + 1, out _), Is.True);

                sky.GetExposure((grid.Owner, grid.Comp), new ZLevelTileIndices(x, y, 0));
            }
        }

        for (var lowerZ = 0; lowerZ < FloorCount - 1; lowerZ++)
        {
            Assert.That(portals.TryGetPortalChunk(
                (grid.Owner, grid.Comp),
                Vector2i.Zero,
                lowerZ,
                out _), Is.True);
        }

        gravity.TryGetGravityTarget(grid.Owner, grid.Comp, Vector2i.Zero, FloorCount - 1, out _);
        var snapshot = graph.CreateSnapshot(mapId);
        Assert.That(graph.ValidateSnapshot(snapshot), Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
        graph.CreateSnapshot(mapId);
        return mapId;
    }

    private void DeleteMapIfPresent(MapId mapId)
    {
        var map = SEntMan.System<SharedMapSystem>();
        if (map.MapExists(mapId))
            map.DeleteMap(mapId);
    }

    private static void AssertWarmState(
        ZLevelServerLifecycleStateSnapshot baseline,
        ZLevelServerLifecycleStateSnapshot warm,
        int cycle,
        bool warmup)
    {
        var label = $"{(warmup ? "warm-up" : "measured")} cycle {cycle}";
        Assert.Multiple(() =>
        {
            Assert.That(warm.BoundaryCacheEntries, Is.GreaterThan(baseline.BoundaryCacheEntries), label);
            Assert.That(warm.BoundaryOrderTokens, Is.GreaterThan(baseline.BoundaryOrderTokens), label);
            Assert.That(warm.BoundaryRegistrations, Is.GreaterThan(baseline.BoundaryRegistrations), label);
            Assert.That(warm.BoundaryProviders, Is.GreaterThan(baseline.BoundaryProviders), label);
            Assert.That(warm.SkyExposureEntries, Is.GreaterThan(baseline.SkyExposureEntries), label);
            Assert.That(warm.SkyColumnEntries, Is.GreaterThan(baseline.SkyColumnEntries), label);
            Assert.That(warm.SkyOrderEntries, Is.GreaterThan(baseline.SkyOrderEntries), label);
            Assert.That(warm.GravityCachedGrids, Is.GreaterThan(baseline.GravityCachedGrids), label);
            Assert.That(warm.SoundPortalChunks, Is.GreaterThan(baseline.SoundPortalChunks), label);
            Assert.That(warm.SoundOpenPortals, Is.GreaterThan(baseline.SoundOpenPortals), label);
            Assert.That(warm.SoundExplicitPortals, Is.GreaterThan(baseline.SoundExplicitPortals), label);
            Assert.That(warm.SoundOrderTokens, Is.GreaterThan(baseline.SoundOrderTokens), label);
            Assert.That(warm.TraversalNodes, Is.GreaterThan(baseline.TraversalNodes), label);
            Assert.That(warm.TraversalLocations, Is.GreaterThan(baseline.TraversalLocations), label);
            Assert.That(warm.TraversalTrackedMaps, Is.GreaterThan(baseline.TraversalTrackedMaps), label);
            Assert.That(warm.TraversalSnapshots, Is.GreaterThan(baseline.TraversalSnapshots), label);
        });
    }

    private static ZLevelServerLifecycleStateSnapshot CaptureState(IEntityManager entityManager)
    {
        var boundaries = entityManager.System<SharedZLevelBoundarySystem>();
        var gravity = entityManager.System<SharedZLevelGravitySystem>();
        var sky = entityManager.System<SharedZLevelSkyExposureSystem>();
        var portal = entityManager.System<SharedZLevelSoundPortalSystem>().Snapshot();
        var graph = entityManager.System<ZLevelTraversalGraphSystem>().Snapshot();
        return new ZLevelServerLifecycleStateSnapshot(
            boundaries.CachedBoundaryCount,
            boundaries.BoundaryCacheOrderTokenCount,
            boundaries.BoundaryRegistrationCount,
            boundaries.BoundaryProviderCount,
            sky.CachedExposureCount,
            sky.CachedColumnCount,
            sky.CacheOrderEntryCount,
            gravity.CachedGridCount,
            gravity.PendingRefreshGridCount,
            portal.CachedChunks,
            portal.CachedOpenPortals,
            portal.CachedExplicitPortals,
            portal.CacheOrderTokens,
            graph.Nodes,
            graph.Locations,
            graph.TrackedMapRevisions,
            graph.CachedSnapshots);
    }

    private static void ForceFullCollection()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static ZLevelServerSoakLatencySnapshot CreateLatencySnapshot(long[] samples)
    {
        Array.Sort(samples);
        double total = 0d;
        foreach (var sample in samples)
            total += sample;

        return new ZLevelServerSoakLatencySnapshot(
            samples.Length,
            ToMilliseconds(samples[0]),
            ToMilliseconds(total / samples.Length),
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

    private static void WriteReport(ZLevelServerLifecycleReport report)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "zlevel-server-lifecycle");
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "zlevel-server-lifecycle.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
        TestContext.AddTestAttachment(path, "WTZ Z-level Server GC lifecycle report");
        TestContext.Progress.WriteLine($"WTZ Z-level server lifecycle: {path}");
    }
}

internal sealed record ZLevelServerLifecycleSettings(
    int WarmupCycles,
    int MeasuredCycles,
    bool RequireServerGarbageCollection)
{
    private const string WarmupVariable = "WTZ_ZLEVEL_LIFECYCLE_WARMUP";
    private const string CyclesVariable = "WTZ_ZLEVEL_LIFECYCLE_CYCLES";
    private const string RequireServerGcVariable = "WTZ_ZLEVEL_LIFECYCLE_REQUIRE_SERVER_GC";

    public static ZLevelServerLifecycleSettings FromEnvironment()
    {
        return new ZLevelServerLifecycleSettings(
            ReadBounded(WarmupVariable, 2, 1, 64),
            ReadBounded(CyclesVariable, 4, 1, 2_048),
            Environment.GetEnvironmentVariable(RequireServerGcVariable) == "1");
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

internal sealed record ZLevelServerLifecycleReport(
    int SchemaVersion,
    DateTimeOffset CapturedAtUtc,
    ZLevelServerSoakHostSnapshot Host,
    ZLevelServerLifecycleSettings Settings,
    ZLevelServerLifecycleStateSnapshot Baseline,
    ZLevelServerLifecycleStateSnapshot MaximumWarmState,
    ZLevelServerLifecycleStateSnapshot FinalState,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    long HeapBytesBefore,
    long HeapBytesBeforeCollection,
    long HeapBytesAfterCollection,
    int GenerationZeroCollections,
    int GenerationOneCollections,
    int GenerationTwoCollections,
    ZLevelServerSoakLatencySnapshot CycleLatency)
{
    public long RetainedHeapDeltaBytes => HeapBytesAfterCollection - HeapBytesBefore;
    public double AllocatedBytesPerCycle => Settings.MeasuredCycles == 0
        ? 0d
        : AllocatedBytes / (double) Settings.MeasuredCycles;
}

internal readonly record struct ZLevelServerLifecycleCycleResult(
    ZLevelServerLifecycleStateSnapshot WarmState,
    long ElapsedTicks);

internal readonly record struct ZLevelServerLifecycleStateSnapshot(
    int BoundaryCacheEntries,
    int BoundaryOrderTokens,
    int BoundaryRegistrations,
    int BoundaryProviders,
    int SkyExposureEntries,
    int SkyColumnEntries,
    int SkyOrderEntries,
    int GravityCachedGrids,
    int GravityPendingGrids,
    int SoundPortalChunks,
    int SoundOpenPortals,
    int SoundExplicitPortals,
    int SoundOrderTokens,
    int TraversalNodes,
    int TraversalLocations,
    int TraversalTrackedMaps,
    int TraversalSnapshots)
{
    public static ZLevelServerLifecycleStateSnapshot Max(
        in ZLevelServerLifecycleStateSnapshot left,
        in ZLevelServerLifecycleStateSnapshot right)
    {
        return new ZLevelServerLifecycleStateSnapshot(
            Math.Max(left.BoundaryCacheEntries, right.BoundaryCacheEntries),
            Math.Max(left.BoundaryOrderTokens, right.BoundaryOrderTokens),
            Math.Max(left.BoundaryRegistrations, right.BoundaryRegistrations),
            Math.Max(left.BoundaryProviders, right.BoundaryProviders),
            Math.Max(left.SkyExposureEntries, right.SkyExposureEntries),
            Math.Max(left.SkyColumnEntries, right.SkyColumnEntries),
            Math.Max(left.SkyOrderEntries, right.SkyOrderEntries),
            Math.Max(left.GravityCachedGrids, right.GravityCachedGrids),
            Math.Max(left.GravityPendingGrids, right.GravityPendingGrids),
            Math.Max(left.SoundPortalChunks, right.SoundPortalChunks),
            Math.Max(left.SoundOpenPortals, right.SoundOpenPortals),
            Math.Max(left.SoundExplicitPortals, right.SoundExplicitPortals),
            Math.Max(left.SoundOrderTokens, right.SoundOrderTokens),
            Math.Max(left.TraversalNodes, right.TraversalNodes),
            Math.Max(left.TraversalLocations, right.TraversalLocations),
            Math.Max(left.TraversalTrackedMaps, right.TraversalTrackedMaps),
            Math.Max(left.TraversalSnapshots, right.TraversalSnapshots));
    }
}
