// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

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

public sealed class ZLevelMetricsTest : GameTest
{
    [Test]
    public async Task BoundaryAndVisibilityCountersAreDeterministicAndResettable()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var tile = new Vector2i(5, 5);

            map.SetTile(testMap.Grid, grid, tile, new Tile(1));
            boundaries.InvalidateBoundary(testMap.Grid, tile, 0);
            metrics.ResetCounters();

            Assert.That(boundaries.TryGetBoundary(testMap.Grid, grid, tile, 0, 1, out _), Is.True);
            Assert.That(boundaries.TryGetBoundary(testMap.Grid, grid, tile, 0, 1, out _), Is.True);

            var boundarySnapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(boundarySnapshot.BoundaryQueries, Is.EqualTo(2));
                Assert.That(boundarySnapshot.BoundaryCacheHits, Is.EqualTo(1));
                Assert.That(boundarySnapshot.BoundaryCacheMisses, Is.EqualTo(1));
                Assert.That(boundarySnapshot.BoundaryCacheHitPercent, Is.EqualTo(50d));
            });

            metrics.ResetCounters();
            Assert.That(visibility.IsTileVisibleFrom(testMap.Grid, grid, tile, 0, 0), Is.True);
            Assert.That(visibility.IsTileVisibleFrom(testMap.Grid, grid, tile, 0, 1, allowAbove: true), Is.True);

            var visibilitySnapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(visibilitySnapshot.VisibilityTileQueries, Is.EqualTo(2));
                Assert.That(visibilitySnapshot.VisibilitySameLevel, Is.EqualTo(1));
                Assert.That(visibilitySnapshot.VisibilityBoundaryChecks, Is.EqualTo(1));
                Assert.That(visibilitySnapshot.BoundaryQueries, Is.EqualTo(1));
                Assert.That(visibilitySnapshot.BoundaryCacheHits, Is.EqualTo(1));
            });

            metrics.RecordExplosionTopology(
                gridLayers: 3,
                spaceLayers: 1,
                tiles: 42,
                verticalQueries: 10,
                verticalCacheHits: 4,
                verticalTraces: 6,
                verticalOpen: 2,
                verticalClosed: 3,
                verticalRejected: 1,
                areaBudgetExhausted: true,
                iterationBudgetExhausted: false,
                elapsedTimestampTicks: 0);
            var explosionSnapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(explosionSnapshot.ExplosionTopologyBuilds, Is.EqualTo(1));
                Assert.That(explosionSnapshot.ExplosionGridLayers, Is.EqualTo(3));
                Assert.That(explosionSnapshot.ExplosionSpaceLayers, Is.EqualTo(1));
                Assert.That(explosionSnapshot.ExplosionTiles, Is.EqualTo(42));
                Assert.That(explosionSnapshot.ExplosionVerticalCacheHitPercent, Is.EqualTo(40d));
                Assert.That(explosionSnapshot.ExplosionVerticalOpen, Is.EqualTo(2));
                Assert.That(explosionSnapshot.ExplosionVerticalClosed, Is.EqualTo(3));
                Assert.That(explosionSnapshot.ExplosionVerticalRejected, Is.EqualTo(1));
                Assert.That(explosionSnapshot.ExplosionAreaBudgetExhaustions, Is.EqualTo(1));
                Assert.That(explosionSnapshot.ExplosionIterationBudgetExhaustions, Is.Zero);
            });

            metrics.ResetCounters();
            var resetSnapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(resetSnapshot.BoundaryQueries, Is.Zero);
                Assert.That(resetSnapshot.VisibilityTileQueries, Is.Zero);
                Assert.That(resetSnapshot.GravityQueries, Is.Zero);
                Assert.That(resetSnapshot.PvsRefreshes, Is.Zero);
                Assert.That(resetSnapshot.BallisticRouteAttempts, Is.Zero);
                Assert.That(resetSnapshot.BallisticRoutesStarted, Is.Zero);
                Assert.That(resetSnapshot.BallisticRoutesCompleted, Is.Zero);
                Assert.That(resetSnapshot.BallisticRoutesRejected, Is.Zero);
                Assert.That(resetSnapshot.BallisticCrossings, Is.Zero);
                Assert.That(resetSnapshot.BallisticClosedBoundaries, Is.Zero);
                Assert.That(resetSnapshot.BallisticCollisionCancellations, Is.Zero);
                Assert.That(resetSnapshot.BallisticInvalidCancellations, Is.Zero);
                Assert.That(resetSnapshot.BallisticContactFlushes, Is.Zero);
                Assert.That(resetSnapshot.ExplosionTopologyBuilds, Is.Zero);
                Assert.That(resetSnapshot.ExplosionGridLayers, Is.Zero);
                Assert.That(resetSnapshot.ExplosionSpaceLayers, Is.Zero);
                Assert.That(resetSnapshot.ExplosionTiles, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalQueries, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalCacheHits, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalTraces, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalOpen, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalClosed, Is.Zero);
                Assert.That(resetSnapshot.ExplosionVerticalRejected, Is.Zero);
                Assert.That(resetSnapshot.ExplosionAreaBudgetExhaustions, Is.Zero);
                Assert.That(resetSnapshot.ExplosionIterationBudgetExhaustions, Is.Zero);
                Assert.That(resetSnapshot.ExplosionTopologyMilliseconds, Is.Zero);
                Assert.That(resetSnapshot.ExplosionLastTopologyMilliseconds, Is.Zero);
                Assert.That(resetSnapshot.ExplosionMaxTopologyMilliseconds, Is.Zero);
            });
        });
    }

    [Test]
    public async Task TraceCountersCoverTerminationsOutputsAndReset()
    {
        await OverrideCVar(Side.Server, CCVars.ZLevelTraceMaxTileVisits, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var trace = SEntMan.System<SharedZLevelTraceSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            for (var x = 0; x <= 3; x++)
            {
                map.SetZLevelTile(
                    testMap.Grid,
                    grid,
                    new ZLevelTileIndices(x, 1, 0),
                    new Tile(1));
            }

            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 0),
                new Tile(1));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 1),
                new Tile(1));

            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 1.5f),
                0,
                out var origin), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(3.5f, 1.5f),
                0,
                out var destination), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                0,
                out var lower), Is.True);
            Assert.That(trace.TryCreateGridPoint(
                testMap.Grid,
                new Vector2(0.5f, 0.5f),
                1,
                out var upper), Is.True);

            metrics.ResetCounters();
            var completed = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.None));
            var budget = trace.Trace(new ZLevelTraceRequest(
                origin,
                destination,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            var closed = trace.Trace(new ZLevelTraceRequest(
                lower,
                upper,
                ZLevelBoundaryChannels.Effects,
                Options: ZLevelTraceOptions.IncludeTileVisits));
            var invalid = trace.Trace(new ZLevelTraceRequest(
                ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    new Vector2(float.NaN, 0f),
                    0,
                    testMap.MapId)),
                destination,
                ZLevelBoundaryChannels.Effects));
            var differentMaps = trace.Trace(new ZLevelTraceRequest(
                ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    Vector2.Zero,
                    0,
                    testMap.MapId)),
                ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    Vector2.One,
                    0,
                    new MapId(1_000_000))),
                ZLevelBoundaryChannels.Effects));
            var frameFailure = trace.Trace(new ZLevelTraceRequest(
                ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    Vector2.Zero,
                    0,
                    testMap.MapId)),
                ZLevelTracePoint.FromMap(new ZLevelMapCoordinates(
                    Vector2.One,
                    1,
                    testMap.MapId)),
                ZLevelBoundaryChannels.Effects));
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(completed.Termination, Is.EqualTo(ZLevelTraceTermination.Completed));
                Assert.That(budget.Termination, Is.EqualTo(ZLevelTraceTermination.IterationBudgetExceeded));
                Assert.That(closed.Termination, Is.EqualTo(ZLevelTraceTermination.ClosedBoundary));
                Assert.That(invalid.Termination, Is.EqualTo(ZLevelTraceTermination.InvalidCoordinates));
                Assert.That(differentMaps.Termination, Is.EqualTo(ZLevelTraceTermination.DifferentMaps));
                Assert.That(frameFailure.Termination, Is.EqualTo(ZLevelTraceTermination.FrameResolutionRequired));
                Assert.That(snapshot.TraceQueries, Is.EqualTo(6));
                Assert.That(snapshot.TraceCompleted, Is.EqualTo(1));
                Assert.That(snapshot.TraceClosedBoundaries, Is.EqualTo(1));
                Assert.That(snapshot.TraceInvalidCoordinates, Is.EqualTo(1));
                Assert.That(snapshot.TraceDifferentMaps, Is.EqualTo(1));
                Assert.That(snapshot.TraceFrameResolutionFailures, Is.EqualTo(1));
                Assert.That(snapshot.TraceBudgetExhaustions, Is.EqualTo(1));
                Assert.That(snapshot.TraceSegments, Is.EqualTo(2));
                Assert.That(snapshot.TraceTileVisits, Is.EqualTo(1));
                Assert.That(snapshot.TraceEntityHits, Is.Zero);
                Assert.That(snapshot.TraceBoundaryCrossings, Is.EqualTo(1));
                Assert.That(snapshot.TraceMilliseconds, Is.GreaterThanOrEqualTo(0d));
                Assert.That(snapshot.TraceLastMilliseconds, Is.GreaterThanOrEqualTo(0d));
                Assert.That(snapshot.TraceMaxMilliseconds, Is.GreaterThanOrEqualTo(snapshot.TraceLastMilliseconds));
                Assert.That(snapshot.TraceAverageMilliseconds, Is.GreaterThanOrEqualTo(0d));
            });

            metrics.ResetCounters();
            var reset = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(reset.TraceQueries, Is.Zero);
                Assert.That(reset.TraceCompleted, Is.Zero);
                Assert.That(reset.TraceClosedBoundaries, Is.Zero);
                Assert.That(reset.TraceBudgetExhaustions, Is.Zero);
                Assert.That(reset.TraceSegments, Is.Zero);
                Assert.That(reset.TraceTileVisits, Is.Zero);
                Assert.That(reset.TraceMilliseconds, Is.Zero);
                Assert.That(reset.TraceLastMilliseconds, Is.Zero);
                Assert.That(reset.TraceMaxMilliseconds, Is.Zero);
            });
        });
    }
}
