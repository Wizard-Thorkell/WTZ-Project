// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;
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

            metrics.ResetCounters();
            var resetSnapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(resetSnapshot.BoundaryQueries, Is.Zero);
                Assert.That(resetSnapshot.VisibilityTileQueries, Is.Zero);
                Assert.That(resetSnapshot.GravityQueries, Is.Zero);
                Assert.That(resetSnapshot.PvsRefreshes, Is.Zero);
            });
        });
    }
}
