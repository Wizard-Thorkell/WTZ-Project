// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.ZLevel.Systems;
using Content.Shared.CCVar;
using Content.Shared.ZLevel.Systems;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
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
}
