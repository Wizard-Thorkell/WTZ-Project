// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelSkyExposureTest : GameTest
{
    [Test]
    public async Task WeatherBoundariesInvalidateColumnsAndFailClosed()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var tile = new Vector2i(4, 7);
            var origin = new ZLevelTileIndices(tile.X, tile.Y, 0);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(testMap.Grid, grid, origin, new Tile(1));
            sky.InvalidateAll();
            metrics.ResetCounters();

            var exposed = sky.GetExposure((testMap.Grid, grid), origin);
            var cached = sky.GetExposure((testMap.Grid, grid), origin);
            var firstMetrics = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(exposed.Termination, Is.EqualTo(ZLevelSkyExposureTermination.Exposed));
                Assert.That(exposed.BoundaryChecks, Is.EqualTo(3),
                    "The boundary above the declared maximum floor is part of the sky column.");
                Assert.That(cached, Is.EqualTo(exposed));
                Assert.That(firstMetrics.SkyExposureQueries, Is.EqualTo(2));
                Assert.That(firstMetrics.SkyExposureCacheMisses, Is.EqualTo(1));
                Assert.That(firstMetrics.SkyExposureCacheHits, Is.EqualTo(1));
                Assert.That(firstMetrics.SkyExposureBoundaryChecks, Is.EqualTo(3));
                Assert.That(firstMetrics.SkyExposureExposed, Is.EqualTo(2));
                Assert.That(sky.CachedExposureCount, Is.EqualTo(1));
            });

            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 2),
                new Tile(1));
            var blockedByTile = sky.GetExposure((testMap.Grid, grid), origin);
            Assert.Multiple(() =>
            {
                Assert.That(blockedByTile.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
                Assert.That(blockedByTile.BlockingLowerZ, Is.EqualTo(1));
                Assert.That(blockedByTile.BoundaryChecks, Is.EqualTo(2));
                Assert.That(metrics.Snapshot().SkyExposureInvalidatedEntries, Is.GreaterThanOrEqualTo(1));
            });

            var opening = SetBoundary(testMap, tile, 1, opens: ZLevelBoundaryChannels.Weather);
            var opened = sky.GetExposure((testMap.Grid, grid), origin);
            Assert.That(opened.Termination, Is.EqualTo(ZLevelSkyExposureTermination.Exposed));

            var openingComp = SEntMan.GetComponent<ZLevelBoundaryComponent>(opening);
            boundaries.SetBoundary(
                (opening, openingComp),
                true,
                1,
                ZLevelBoundaryChannels.Weather,
                ZLevelBoundaryChannels.Weather);
            var forcedClosed = sky.GetExposure((testMap.Grid, grid), origin);
            Assert.Multiple(() =>
            {
                Assert.That(forcedClosed.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
                Assert.That(forcedClosed.BlockingLowerZ, Is.EqualTo(1),
                    "Forced closed must retain precedence over an open contribution.");
            });

            SEntMan.DeleteEntity(opening);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 2),
                Tile.Empty);
            Assert.That(sky.GetExposure((testMap.Grid, grid), origin).IsExposed, Is.True);

            var roof = SetBoundary(testMap, tile, 2, closes: ZLevelBoundaryChannels.Weather);
            var blockedByRoof = sky.GetExposure((testMap.Grid, grid), origin);
            Assert.Multiple(() =>
            {
                Assert.That(blockedByRoof.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
                Assert.That(blockedByRoof.BlockingLowerZ, Is.EqualTo(2),
                    "A roof can close the boundary above the highest authored floor.");
                Assert.That(blockedByRoof.BoundaryChecks, Is.EqualTo(3));
            });

            SEntMan.DeleteEntity(roof);
            var warm = sky.GetExposure((testMap.Grid, grid), origin);
            Assert.That(warm.IsExposed, Is.True);
            metrics.ResetCounters();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var allExposed = true;
            for (var i = 0; i < 1_000; i++)
            {
                allExposed &= sky.GetExposure((testMap.Grid, grid), origin).IsExposed;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var hotMetrics = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(allExposed, Is.True);
                Assert.That(hotMetrics.SkyExposureQueries, Is.EqualTo(1_000));
                Assert.That(hotMetrics.SkyExposureCacheHits, Is.EqualTo(1_000));
                Assert.That(hotMetrics.SkyExposureCacheMisses, Is.Zero);
                Assert.That(hotMetrics.SkyExposureBoundaryChecks, Is.Zero);
                Assert.That(allocated, Is.LessThanOrEqualTo(512),
                    "Hot sky exposure queries should not allocate per lookup.");
            });
        });
    }

    [Test]
    public async Task LegacyZZeroAndMovingFrameUseLocalColumnGeometry()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var tile = new Vector2i(2, 3);
            map.SetTile(testMap.Grid, grid, tile, new Tile(1));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);

            var baseResult = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 5);
            var invalidUpper = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 6);
            Assert.Multiple(() =>
            {
                Assert.That(baseResult.IsExposed, Is.True,
                    "An unconfigured Z 0 map retains one open top boundary.");
                Assert.That(baseResult.Origin.Z, Is.Zero);
                Assert.That(invalidUpper.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.InvalidLevel));
            });

            var roof = SetBoundary(testMap, tile, 0, closes: ZLevelBoundaryChannels.Weather);
            var blockedAtFive = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 5);
            Assert.That(blockedAtFive.Termination,
                Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));

            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 8), Is.True);
            var blockedAtEight = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 8);
            var oldWorldFloor = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 5);
            Assert.Multiple(() =>
            {
                Assert.That(blockedAtEight, Is.EqualTo(blockedAtFive),
                    "Moving the frame reuses local geometry at its new world floor.");
                Assert.That(oldWorldFloor.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.InvalidLevel));
            });

            SEntMan.DeleteEntity(roof);
        });
    }

    [Test]
    public async Task SharedExposureIsDeterministicBetweenServerAndClient()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity gridNetEntity = default;
        ZLevelSkyExposureState serverResult = default;

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var tile = new Vector2i(6, 1);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 0),
                new Tile(1));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 2),
                new Tile(1));
            transform.SetLocalPosition(testMap.Grid, new Vector2(9f, -4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 3), Is.True);

            serverResult = sky.GetExposureAtWorldZ((testMap.Grid, grid), tile, 3);
            gridNetEntity = SEntMan.GetNetEntity(testMap.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(serverResult.Termination,
                    Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
                Assert.That(serverResult.BlockingLowerZ, Is.EqualTo(1));
            });
        });

        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(gridNetEntity, out var clientGridUid), Is.True);
            var clientGrid = CEntMan.GetComponent<MapGridComponent>(clientGridUid!.Value);
            var sky = CEntMan.System<SharedZLevelSkyExposureSystem>();
            var clientResult = sky.GetExposureAtWorldZ(
                (clientGridUid.Value, clientGrid),
                new Vector2i(6, 1),
                3);
            Assert.That(clientResult, Is.EqualTo(serverResult));
        });
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
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary((provider, boundary), true, 1, opens, closes);
        transform.AnchorEntity(provider, SEntMan.GetComponent<TransformComponent>(provider));
        Assert.That(SEntMan.GetComponent<TransformComponent>(provider).Anchored, Is.True);
        return provider;
    }
}
