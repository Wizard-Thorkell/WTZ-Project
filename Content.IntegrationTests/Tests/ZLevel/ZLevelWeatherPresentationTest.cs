// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Client.Weather;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelWeatherPresentationTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> PlanetTile = "FloorPlanetDirt";

    [Test]
    public async Task LegacyMaskRetainsPlanarRoofAndZZeroPolicy()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var roofs = SEntMan.System<Content.Server.Light.EntitySystems.RoofSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);

            grid.CanSplit = false;
            map.SetTile(testMap.Grid, grid, new Vector2i(0, 0), new Tile(planet.TileId));
            map.SetTile(testMap.Grid, grid, new Vector2i(1, 0), new Tile(planet.TileId));
            var roof = SEntMan.EnsureComponent<RoofComponent>(testMap.Grid);
            roofs.SetRoof((testMap.Grid, grid, roof), new Vector2i(0, 0), true);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                new Vector2(1f, 0.5f),
                new Vector2(2f, 1f));

            Assert.That(presentation.BuildMask(weather, testMap.MapId, bounds, 0), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(ContainsBlockedPoint(presentation, new Vector2(0.5f, 0.5f)), Is.True);
                Assert.That(ContainsBlockedPoint(presentation, new Vector2(1.5f, 0.5f)), Is.False);
                Assert.That(presentation.BuildMask(weather, testMap.MapId, bounds, 1), Is.Zero);
                Assert.That(presentation.MaskEntireViewport, Is.False);
            });
        });
    }

    [Test]
    public async Task MaskUsesActiveWorldFloorAndMovingFrame()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 0),
                new Tile(planet.TileId));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(1, 0, 0),
                new Tile(planet.TileId));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 1),
                new Tile(planet.TileId));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(1, 0, 1),
                new Tile(planet.TileId));
            SpawnRoof(testMap, new Vector2i(0, 0), 1);
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                new Vector2(1f, 0.5f),
                new Vector2(2f, 1f));

            presentation.ResetMetrics();
            Assert.That(presentation.BuildMask(weather, testMap.MapId, bounds, 6), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaskEntireViewport, Is.False);
                Assert.That(presentation.Batches, Has.Count.EqualTo(1));
                Assert.That(presentation.Batches[0].LocalZ, Is.EqualTo(1));
                Assert.That(ContainsBlockedPoint(presentation, new Vector2(0.5f, 0.5f)), Is.True);
                Assert.That(ContainsBlockedPoint(presentation, new Vector2(1.5f, 0.5f)), Is.False);
            });

            const int iterations = 128;
            for (var i = 0; i < 8; i++)
                presentation.BuildMask(weather, testMap.MapId, bounds, 6);

            presentation.BeginBudgetFrameForTesting();
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < iterations; i++)
                presentation.BuildMask(weather, testMap.MapId, bounds, 6);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.That(
                allocated,
                Is.LessThanOrEqualTo(8_192),
                $"Hot weather-mask plans allocated {allocated} bytes across {iterations} builds.");
        });

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 8), Is.True);
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                new Vector2(1f, 0.5f),
                new Vector2(2f, 1f));

            Assert.That(presentation.BuildMask(weather, testMap.MapId, bounds, 9), Is.EqualTo(1));
            Assert.That(presentation.Batches[0].LocalZ, Is.EqualTo(1));
            Assert.That(presentation.BuildMask(weather, testMap.MapId, bounds, 6), Is.Zero);
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaskEntireViewport, Is.False,
                    "A grid outside the viewed world floor must not suppress map-space weather.");
                Assert.That(presentation.Batches, Is.Empty);
                Assert.That(presentation.Runs, Is.Empty);
            });
        });
    }

    [Test]
    public async Task MaskBudgetsFailClosedAtomically()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxTileChecksPerFrame, 1);
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
            for (var x = 0; x < 3; x++)
            {
                map.SetZLevelTile(
                    testMap.Grid,
                    grid,
                    new ZLevelTileIndices(x, 0, 0),
                    new Tile(planet.TileId));
                map.SetZLevelTile(
                    testMap.Grid,
                    grid,
                    new ZLevelTileIndices(x, 0, 1),
                    new Tile(planet.TileId));
            }

            SpawnRoof(testMap, new Vector2i(0, 0), 1);
            SpawnRoof(testMap, new Vector2i(2, 0), 1);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                new Vector2(1.5f, 0.5f),
                new Vector2(3f, 1f));

            presentation.ResetMetrics();
            presentation.BuildMask(weather, testMap.MapId, bounds, 1);
            var metrics = presentation.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaskEntireViewport, Is.True);
                Assert.That(presentation.Batches, Is.Empty);
                Assert.That(presentation.Runs, Is.Empty);
                Assert.That(metrics.MaskFailClosedPlans, Is.EqualTo(1));
                Assert.That(metrics.MaskTileBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.MaskRunBudgetExhaustions, Is.Zero);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxTileChecksPerFrame, 128);
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxRunsPerFrame, 1);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            var bounds = BoundsAroundLocalPoint(
                transform,
                testMap.CGridUid,
                new Vector2(1.5f, 0.5f),
                new Vector2(3f, 1f));

            presentation.ResetMetrics();
            presentation.BeginBudgetFrameForTesting();
            presentation.BuildMask(weather, testMap.MapId, bounds, 1);
            var metrics = presentation.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaskEntireViewport, Is.True);
                Assert.That(metrics.MaskTileChecks, Is.GreaterThanOrEqualTo(3));
                Assert.That(metrics.MaskTileBudgetExhaustions, Is.Zero);
                Assert.That(metrics.MaskRunBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task AudioSearchStaysOnListenerFloorAndHonorsBudget()
    {
        var testMap = await Pair.CreateTestMap();
        NetEntity actorNet = default;
        EntityUid roof = default;

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);
            var tile = new Vector2i(5, 5);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 0),
                new Tile(planet.TileId));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 1),
                new Tile(planet.TileId));
            roof = SpawnRoof(testMap, tile, 1);

            var actor = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
            Assert.That(zLevels.SetZLevelPosition(actor, 1), Is.True);
            actorNet = SEntMan.GetNetEntity(actor);
        });
        await Pair.RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(actorNet, out var actor), Is.True);
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            presentation.ResetMetrics();

            var nearby = presentation.FindAudioExposure(weather, actor!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(nearby.Termination, Is.EqualTo(ZLevelWeatherAudioTermination.Nearby));
                Assert.That(nearby.NearestExposedTile, Is.Not.Null);
                Assert.That(presentation.Snapshot().AudioNearbyExposures, Is.EqualTo(1));
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherAudioMaxTileChecksPerFrame, 1);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(actorNet, out var actor), Is.True);
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            presentation.ResetMetrics();
            presentation.BeginBudgetFrameForTesting();

            var exhausted = presentation.FindAudioExposure(weather, actor!.Value);
            Assert.Multiple(() =>
            {
                Assert.That(exhausted.Termination,
                    Is.EqualTo(ZLevelWeatherAudioTermination.BudgetExceeded));
                Assert.That(presentation.Snapshot().AudioBudgetExhaustions, Is.EqualTo(1));
            });
        });

        await Server.WaitAssertion(() => SEntMan.DeleteEntity(roof));
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(actorNet, out var actor), Is.True);
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            var weather = CEntMan.System<WeatherSystem>();
            presentation.BeginBudgetFrameForTesting();
            Assert.That(presentation.FindAudioExposure(weather, actor!.Value).Termination,
                Is.EqualTo(ZLevelWeatherAudioTermination.Direct));
        });
    }

    [Test]
    public async Task ClientWeatherPresentationLimitsAreClamped()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxTileChecksPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxRunsPerFrame, -1, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherAudioMaxTileChecksPerFrame, -1, false);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaxMaskTileChecksPerFrame, Is.Zero);
                Assert.That(presentation.MaxMaskRunsPerFrame, Is.Zero);
                Assert.That(presentation.MaxAudioTileChecksPerFrame, Is.Zero);
            });
        });

        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxTileChecksPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherMaskMaxRunsPerFrame, int.MaxValue, false);
        await OverrideCVar(Side.Client, CCVars.ZLevelWeatherAudioMaxTileChecksPerFrame, int.MaxValue, false);
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var presentation = CEntMan.System<ZLevelWeatherPresentationSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(presentation.MaxMaskTileChecksPerFrame,
                    Is.EqualTo(ZLevelWeatherPresentationSystem.MaximumMaskTileChecksPerFrame));
                Assert.That(presentation.MaxMaskRunsPerFrame,
                    Is.EqualTo(ZLevelWeatherPresentationSystem.MaximumMaskRunsPerFrame));
                Assert.That(presentation.MaxAudioTileChecksPerFrame,
                    Is.EqualTo(ZLevelWeatherPresentationSystem.MaximumAudioTileChecksPerFrame));
            });
        });
    }

    private EntityUid SpawnRoof(TestMapData testMap, Vector2i tile, int localZ)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var roof = SEntMan.SpawnEntity(
            "ZLevelRoofMarker",
            map.GridTileToLocal(testMap.Grid, grid, tile));
        var xform = SEntMan.GetComponent<TransformComponent>(roof);
        transform.Unanchor(roof, xform);
        Assert.That(zLevels.SetZLevelPosition(roof, localZ), Is.True);
        transform.AnchorEntity(roof, xform);
        Assert.That(xform.Anchored, Is.True);
        return roof;
    }

    private static Box2 BoundsAroundLocalPoint(
        SharedTransformSystem transform,
        EntityUid gridUid,
        Vector2 localPoint,
        Vector2 size)
    {
        var (_, _, worldMatrix, _) = transform.GetWorldPositionRotationMatrixWithInv(gridUid);
        var worldPoint = Vector2.Transform(localPoint, worldMatrix);
        return Box2.CenteredAround(worldPoint, size);
    }

    private static bool ContainsBlockedPoint(
        ZLevelWeatherPresentationSystem presentation,
        Vector2 point)
    {
        foreach (var run in presentation.Runs)
        {
            if (point.X >= run.LocalBounds.Left &&
                point.X < run.LocalBounds.Right &&
                point.Y >= run.LocalBounds.Bottom &&
                point.Y < run.LocalBounds.Top)
            {
                return true;
            }
        }

        return false;
    }
}
