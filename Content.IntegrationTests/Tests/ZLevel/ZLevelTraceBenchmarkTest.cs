// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
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

public sealed class ZLevelTraceBenchmarkTest : GameTest
{
    private const int WarmupIterations = 16;
    private const int MeasuredIterations = 512;
    private const int TileVisitBudget = 64;
    private const string BenchmarkDirectoryEnvironmentVariable = "WTZ_ZLEVEL_TRACE_BENCHMARK_DIR";

    [Test]
    public async Task TraceWorkloadsProduceMachineReadableAllocationBaseline()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxVerticalCrossings, 8);
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxTileVisits, TileVisitBudget);
        var testMap = await Pair.CreateTestMap();
        ZLevelTraceBenchmark? benchmark = null;

        await Server.WaitAssertion(() =>
        {
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var map = SEntMan.System<SharedMapSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                3,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);

            var closedTile = new Vector2i(20, 20);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(closedTile.X, closedTile.Y, 0),
                new Tile(1));
            var blocker = SEntMan.SpawnEntity(
                null,
                map.GridTileToLocal(testMap.Grid, grid, closedTile));
            zLevels.SetZLevelPosition(blocker, 0);
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(blocker);
            boundaries.SetBoundary(
                (blocker, boundary),
                true,
                1,
                ZLevelBoundaryChannels.None,
                ZLevelBoundaryChannels.Effects);
            transform.AnchorEntity(blocker, SEntMan.GetComponent<TransformComponent>(blocker));

            ZLevelTracePoint Point(Vector2 position, int localZ)
            {
                Assert.That(trace.TryCreateGridPoint(
                    testMap.Grid,
                    position,
                    localZ,
                    out var point), Is.True);
                return point;
            }

            var definitions = new[]
            {
                new TraceWorkloadDefinition(
                    "same-level",
                    new ZLevelTraceRequest(
                        Point(new Vector2(0.5f, 0.5f), 0),
                        Point(new Vector2(15.5f, 0.5f), 0),
                        ZLevelBoundaryChannels.Effects,
                        Options: ZLevelTraceOptions.IncludeTileVisits),
                    ZLevelTraceTermination.Completed,
                    1,
                    0),
                new TraceWorkloadDefinition(
                    "diagonal-multi-floor",
                    new ZLevelTraceRequest(
                        Point(new Vector2(0.5f, 5.5f), 0),
                        Point(new Vector2(15.5f, 20.5f), 3),
                        ZLevelBoundaryChannels.Effects,
                        Options: ZLevelTraceOptions.IncludeTileVisits),
                    ZLevelTraceTermination.Completed,
                    4,
                    3),
                new TraceWorkloadDefinition(
                    "closed-boundary",
                    new ZLevelTraceRequest(
                        Point(new Vector2(20.5f, 20.5f), 0),
                        Point(new Vector2(20.5f, 20.5f), 2),
                        ZLevelBoundaryChannels.Effects,
                        Options: ZLevelTraceOptions.IncludeTileVisits),
                    ZLevelTraceTermination.ClosedBoundary,
                    1,
                    1),
                new TraceWorkloadDefinition(
                    "tile-budget-exhaustion",
                    new ZLevelTraceRequest(
                        Point(new Vector2(0.5f, 30.5f), 0),
                        Point(new Vector2(80.5f, 30.5f), 0),
                        ZLevelBoundaryChannels.Effects,
                        Options: ZLevelTraceOptions.IncludeTileVisits),
                    ZLevelTraceTermination.IterationBudgetExceeded,
                    0,
                    0),
            };

            var workloads = new List<ZLevelTraceWorkloadBenchmark>(definitions.Length);
            foreach (var definition in definitions)
            {
                var immutable = CaptureImmutable(trace, metrics, definition.Request);
                var buffered = CaptureBuffered(trace, metrics, definition.Request);
                AssertRun(definition, immutable);
                AssertRun(definition, buffered);
                Assert.That(buffered.AllocatedBytes, Is.Zero,
                    $"The warmed tile-only buffer path must not allocate for {definition.Name}.");
                Assert.That(buffered.AllocatedBytes, Is.LessThan(immutable.AllocatedBytes),
                    $"The warmed buffer should allocate less for {definition.Name}.");
                workloads.Add(new ZLevelTraceWorkloadBenchmark(
                    definition.Name,
                    definition.ExpectedTermination.ToString(),
                    definition.ExpectedSegments,
                    definition.ExpectedCrossings,
                    immutable,
                    buffered));
            }

            benchmark = new ZLevelTraceBenchmark(
                1,
                Environment.Version.ToString(),
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.ProcessorCount,
                GCSettings.IsServerGC,
                Stopwatch.Frequency,
                WarmupIterations,
                MeasuredIterations,
                new ZLevelTraceBudgetSnapshot(
                    trace.MaxVerticalCrossings,
                    trace.MaxTileVisits,
                    trace.MaxEntityHits),
                workloads);
        });

        Assert.That(benchmark, Is.Not.Null);
        WriteBenchmark(benchmark!);
    }

    private static ZLevelTraceBenchmarkRun CaptureImmutable(
        SharedZLevelTraceSystem trace,
        SharedZLevelMetricsSystem metrics,
        ZLevelTraceRequest request)
    {
        for (var i = 0; i < WarmupIterations; i++)
            trace.Trace(request);

        metrics.ResetCounters();
        var last = default(ZLevelTraceResult);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < MeasuredIterations; i++)
            last = trace.Trace(request);
        var elapsedTicks = Stopwatch.GetTimestamp() - started;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return CreateRun(
            "immutable",
            elapsedTicks,
            allocatedBytes,
            last.Termination,
            last.Segments.Length,
            last.TileVisits.Length,
            last.EntityHits.Length,
            last.BoundaryCrossings.Length,
            metrics.Snapshot());
    }

    private static ZLevelTraceBenchmarkRun CaptureBuffered(
        SharedZLevelTraceSystem trace,
        SharedZLevelMetricsSystem metrics,
        ZLevelTraceRequest request)
    {
        var buffer = new ZLevelTraceBuffer();
        buffer.EnsureCapacity(8, 128, 0, 8);
        for (var i = 0; i < WarmupIterations; i++)
            trace.Trace(request, buffer);

        metrics.ResetCounters();
        var last = default(ZLevelTraceBufferResult);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var i = 0; i < MeasuredIterations; i++)
            last = trace.Trace(request, buffer);
        var elapsedTicks = Stopwatch.GetTimestamp() - started;
        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return CreateRun(
            "buffered",
            elapsedTicks,
            allocatedBytes,
            last.Termination,
            buffer.Segments.Count,
            buffer.TileVisits.Count,
            buffer.EntityHits.Count,
            buffer.BoundaryCrossings.Count,
            metrics.Snapshot());
    }

    private static ZLevelTraceBenchmarkRun CreateRun(
        string mode,
        long elapsedTicks,
        long allocatedBytes,
        ZLevelTraceTermination termination,
        int segments,
        int tileVisits,
        int entityHits,
        int boundaryCrossings,
        ZLevelMetricsSnapshot metrics)
    {
        return new ZLevelTraceBenchmarkRun(
            mode,
            MeasuredIterations,
            elapsedTicks * 1000d / Stopwatch.Frequency,
            allocatedBytes,
            termination.ToString(),
            segments,
            tileVisits,
            entityHits,
            boundaryCrossings,
            new ZLevelTraceMetricsSnapshot(
                metrics.TraceQueries,
                metrics.TraceCompleted,
                metrics.TraceClosedBoundaries,
                metrics.TraceInvalidCoordinates,
                metrics.TraceDifferentMaps,
                metrics.TraceFrameResolutionFailures,
                metrics.TraceBudgetExhaustions,
                metrics.TraceSegments,
                metrics.TraceTileVisits,
                metrics.TraceEntityHits,
                metrics.TraceBoundaryCrossings,
                metrics.TraceMilliseconds,
                metrics.TraceAverageMilliseconds,
                metrics.TraceLastMilliseconds,
                metrics.TraceMaxMilliseconds));
    }

    private static void AssertRun(
        TraceWorkloadDefinition definition,
        ZLevelTraceBenchmarkRun run)
    {
        Assert.Multiple(() =>
        {
            Assert.That(run.Iterations, Is.EqualTo(MeasuredIterations));
            Assert.That(run.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(run.AllocatedBytes, Is.GreaterThanOrEqualTo(0));
            Assert.That(run.Termination, Is.EqualTo(definition.ExpectedTermination.ToString()));
            Assert.That(run.Segments, Is.EqualTo(definition.ExpectedSegments));
            Assert.That(run.BoundaryCrossings, Is.EqualTo(definition.ExpectedCrossings));
            Assert.That(run.EntityHits, Is.Zero);
            Assert.That(run.Metrics.Queries, Is.EqualTo(MeasuredIterations));
            Assert.That(run.Metrics.Segments, Is.EqualTo((long) run.Segments * MeasuredIterations));
            Assert.That(run.Metrics.TileVisits, Is.EqualTo((long) run.TileVisits * MeasuredIterations));
            Assert.That(run.Metrics.EntityHits, Is.Zero);
            Assert.That(run.Metrics.BoundaryCrossings,
                Is.EqualTo((long) run.BoundaryCrossings * MeasuredIterations));
            Assert.That(run.Metrics.Milliseconds, Is.GreaterThanOrEqualTo(0d));
        });

        switch (definition.ExpectedTermination)
        {
            case ZLevelTraceTermination.Completed:
                Assert.That(run.Metrics.Completed, Is.EqualTo(MeasuredIterations));
                Assert.That(run.TileVisits, Is.GreaterThan(0));
                break;
            case ZLevelTraceTermination.ClosedBoundary:
                Assert.That(run.Metrics.ClosedBoundaries, Is.EqualTo(MeasuredIterations));
                break;
            case ZLevelTraceTermination.IterationBudgetExceeded:
                Assert.That(run.Metrics.BudgetExhaustions, Is.EqualTo(MeasuredIterations));
                Assert.That(run.TileVisits, Is.Zero);
                break;
        }
    }

    private static void WriteBenchmark(ZLevelTraceBenchmark benchmark)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(BenchmarkDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "zlevel-trace-benchmarks");
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "zlevel-trace-benchmark.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(benchmark, options));
        TestContext.AddTestAttachment(path, "WTZ ZLevelTrace allocation and timing benchmark");
        TestContext.Progress.WriteLine($"WTZ ZLevelTrace benchmark: {path}");
    }

    private readonly record struct TraceWorkloadDefinition(
        string Name,
        ZLevelTraceRequest Request,
        ZLevelTraceTermination ExpectedTermination,
        int ExpectedSegments,
        int ExpectedCrossings);
}

internal sealed record ZLevelTraceBenchmark(
    int SchemaVersion,
    string RuntimeVersion,
    string OperatingSystem,
    string ProcessArchitecture,
    int ProcessorCount,
    bool ServerGarbageCollection,
    long StopwatchFrequency,
    int WarmupIterations,
    int MeasuredIterations,
    ZLevelTraceBudgetSnapshot Budgets,
    IReadOnlyList<ZLevelTraceWorkloadBenchmark> Workloads);

internal sealed record ZLevelTraceBudgetSnapshot(
    int VerticalCrossings,
    int TileVisits,
    int EntityHits);

internal sealed record ZLevelTraceWorkloadBenchmark(
    string Name,
    string ExpectedTermination,
    int ExpectedSegments,
    int ExpectedBoundaryCrossings,
    ZLevelTraceBenchmarkRun Immutable,
    ZLevelTraceBenchmarkRun Buffered);

internal sealed record ZLevelTraceBenchmarkRun(
    string Mode,
    int Iterations,
    double ElapsedMilliseconds,
    long AllocatedBytes,
    string Termination,
    int Segments,
    int TileVisits,
    int EntityHits,
    int BoundaryCrossings,
    ZLevelTraceMetricsSnapshot Metrics);

internal sealed record ZLevelTraceMetricsSnapshot(
    long Queries,
    long Completed,
    long ClosedBoundaries,
    long InvalidCoordinates,
    long DifferentMaps,
    long FrameResolutionFailures,
    long BudgetExhaustions,
    long Segments,
    long TileVisits,
    long EntityHits,
    long BoundaryCrossings,
    double Milliseconds,
    double AverageMilliseconds,
    double LastMilliseconds,
    double MaxMilliseconds);
