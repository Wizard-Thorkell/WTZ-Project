// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.ZLevel.Systems;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Tests;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelSoundRouteTest : AtmosTest
{
    protected override ResPath? TestMapPath =>
        new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task ChoosesDeterministicAcousticRouteInBothDirectionsAndMovingFrames()
    {
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var route = SEntMan.System<ZLevelSoundRouteSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var center = GetFixtureCenter(map, grid);
            var defaultPortalTile = center + new Vector2i(-1, 0);
            var alternatePortalTile = center + new Vector2i(1, 0);
            var sourcePosition = map.TileCenterToVector((MapData.Grid, grid), center);
            var source = new ZLevelSoundRouteEndpoint(MapData.Grid, sourcePosition, 0);
            var listener = new ZLevelSoundRouteEndpoint(MapData.Grid, sourcePosition, 2);
            var options = RouteOptions(
                maxDistance: 5f,
                verticalDistance: 1f,
                explicitTransmission: 0.5f,
                lossScale: 4f);
            var results = new List<ZLevelSoundPortal>(4);

            Configure(0, 2);
            FillLayer(map, grid, center, 3, 0, new Tile(1));
            FillLayer(map, grid, center, 3, 1, new Tile(1));
            FillLayer(map, grid, center, 3, 2, new Tile(1));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(defaultPortalTile.X, defaultPortalTile.Y, 1),
                Tile.Empty);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(defaultPortalTile.X, defaultPortalTile.Y, 2),
                Tile.Empty);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(alternatePortalTile.X, alternatePortalTile.Y, 1),
                Tile.Empty);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(alternatePortalTile.X, alternatePortalTile.Y, 2),
                Tile.Empty);
            SetBoundary(center, 0, ZLevelBoundaryChannels.Sound);
            SetBoundary(center, 1, ZLevelBoundaryChannels.Sound);

            transform.SetLocalPosition(MapData.Grid, new Vector2(12f, -7f));
            transform.SetLocalRotation(MapData.Grid, Angle.FromDegrees(20));
            Assert.That(transform.SetZLevelFrameOrigin(MapData.Grid, 5), Is.True);
            route.ResetMetrics();

            var budget = ZLevelSoundRouteBudget.Unlimited;
            var upward = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                ref budget);
            Assert.Multiple(() =>
            {
                Assert.That(upward.Status, Is.EqualTo(ZLevelSoundRouteStatus.Success));
                Assert.That(upward.PortalsAdded, Is.EqualTo(2));
                Assert.That(upward.Crossings, Is.EqualTo(2));
                Assert.That(upward.Distance, Is.EqualTo(4f).Within(0.001f));
                Assert.That(upward.EffectiveDistance, Is.EqualTo(4f).Within(0.001f));
                Assert.That(upward.Transmission, Is.EqualTo(1f).Within(0.001f));
                Assert.That(results[0].Tile, Is.EqualTo(defaultPortalTile));
                Assert.That(results[0].LowerLocalZ, Is.Zero);
                Assert.That(results[0].LowerWorldZ, Is.EqualTo(5));
                Assert.That(results[1].Tile, Is.EqualTo(defaultPortalTile));
                Assert.That(results[1].LowerLocalZ, Is.EqualTo(1));
                Assert.That(results[1].LowerWorldZ, Is.EqualTo(6));
                Assert.That(results, Has.All.Property("Kind").EqualTo(ZLevelSoundPortalKind.DefaultOpening));
            });

            var firstWorldPosition = results[0].WorldPosition;
            transform.SetLocalPosition(MapData.Grid, new Vector2(-8f, 14f));
            transform.SetLocalRotation(MapData.Grid, Angle.FromDegrees(-35));
            Assert.That(transform.SetZLevelFrameOrigin(MapData.Grid, 8), Is.True);
            results.Clear();
            budget = ZLevelSoundRouteBudget.Unlimited;
            var downward = route.FindRoute(
                (MapData.Grid, grid),
                listener,
                source,
                options,
                results,
                ref budget);
            Assert.Multiple(() =>
            {
                Assert.That(downward.Status, Is.EqualTo(ZLevelSoundRouteStatus.Success));
                Assert.That(results.Count, Is.EqualTo(2));
                Assert.That(results[0].LowerLocalZ, Is.EqualTo(1));
                Assert.That(results[0].LowerWorldZ, Is.EqualTo(9));
                Assert.That(results[1].LowerLocalZ, Is.Zero);
                Assert.That(results[1].LowerWorldZ, Is.EqualTo(8));
                Assert.That(Vector2.Distance(firstWorldPosition, results[1].WorldPosition), Is.GreaterThan(1f));
            });

            RouteRepeated(route, (MapData.Grid, grid), source, listener, options, results, 1_024);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var accepted = RouteRepeated(
                route,
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                1_000);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var metrics = route.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(accepted, Is.EqualTo(1_000));
                Assert.That(allocated, Is.LessThanOrEqualTo(1_024));
                Assert.That(metrics.VerticalSuccesses, Is.EqualTo(metrics.Successes));
                Assert.That(metrics.EdgesEvaluated, Is.GreaterThan(metrics.Successes));
            });
            TestContext.Progress.WriteLine(
                $"WTZ P4.2 sound route: queries={metrics.Queries}, " +
                $"routeMs={metrics.RouteMilliseconds:0.000}, " +
                $"edges={metrics.EdgesEvaluated}, hotBytes={allocated}");
        });
    }

    [Test]
    public async Task RouteBudgetsFailExplicitlyWithoutPartialResults()
    {
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundRouteMaxEdges, -10);
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundRouteMaxCrossings, int.MaxValue);
        });

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var route = SEntMan.System<ZLevelSoundRouteSystem>();
            var portals = SEntMan.System<SharedZLevelSoundPortalSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var center = GetFixtureCenter(map, grid);
            var position = map.TileCenterToVector((MapData.Grid, grid), center);
            var source = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 0);
            var listener = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 1);
            var options = RouteOptions(3f, 1f, 1f, 0f);
            var results = new List<ZLevelSoundPortal> { default };

            Configure(0, 1);
            FillLayer(map, grid, center, 3, 0, new Tile(1));
            FillLayer(map, grid, center, 3, 1, new Tile(1));
            SetBoundary(center, 0, ZLevelBoundaryChannels.Sound);
            SetBoundary(center + new Vector2i(1, 0), 0, ZLevelBoundaryChannels.Sound);
            portals.InvalidateGrid(MapData.Grid);
            route.ResetMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(route.MaxEdges, Is.Zero);
                Assert.That(route.MaxCrossings, Is.EqualTo(ZLevelSoundRouteSystem.MaximumCrossings));
            });

            AssertFailure(
                route,
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                new ZLevelSoundRouteBudget(
                    new ZLevelSoundPortalQueryBudget(0, 8, 32),
                    32,
                    32),
                ZLevelSoundRouteStatus.PortalChunkBudgetExceeded);

            AssertFailure(
                route,
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                new ZLevelSoundRouteBudget(
                    new ZLevelSoundPortalQueryBudget(32, 0, 32),
                    32,
                    32),
                ZLevelSoundRouteStatus.PortalBuildBudgetExceeded);

            var candidateBudget = new ZLevelSoundRouteBudget(
                new ZLevelSoundPortalQueryBudget(32, 32, 1),
                32,
                32);
            var candidateFailure = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                ref candidateBudget);
            Assert.Multiple(() =>
            {
                Assert.That(candidateFailure.Status,
                    Is.EqualTo(ZLevelSoundRouteStatus.PortalCandidateBudgetExceeded));
                Assert.That(candidateFailure.PortalCandidates, Is.EqualTo(1));
                Assert.That(candidateFailure.PortalsAdded, Is.Zero);
                Assert.That(results.Count, Is.EqualTo(1));
            });

            var warmResults = new List<ZLevelSoundPortal>();
            var warmBudget = ZLevelSoundRouteBudget.Unlimited;
            Assert.That(route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                warmResults,
                ref warmBudget).Succeeded, Is.True);

            AssertFailure(
                route,
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                new ZLevelSoundRouteBudget(
                    new ZLevelSoundPortalQueryBudget(32, 0, 32),
                    0,
                    32),
                ZLevelSoundRouteStatus.EdgeBudgetExceeded);

            var mediumOptions = options with { MediumMode = ZLevelSoundMediumMode.RequirePressure };
            AssertFailure(
                route,
                (MapData.Grid, grid),
                source,
                listener,
                mediumOptions,
                results,
                new ZLevelSoundRouteBudget(
                    new ZLevelSoundPortalQueryBudget(32, 0, 32),
                    32,
                    0),
                ZLevelSoundRouteStatus.MediumSampleBudgetExceeded);

            var metrics = route.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.PortalChunkBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.PortalBuildBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.PortalCandidateBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.EdgeBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.MediumSampleBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task SameFloorCompatibilityAndVerticalFailureStatesRemainDistinct()
    {
        await Server.WaitAssertion(() =>
        {
            var mapManager = Server.ResolveDependency<IMapManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var route = SEntMan.System<ZLevelSoundRouteSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var center = GetFixtureCenter(map, grid);
            var position = map.TileCenterToVector((MapData.Grid, grid), center);
            var source = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 0);
            var directListener = new ZLevelSoundRouteEndpoint(
                MapData.Grid,
                position + new Vector2(2f, 0f),
                0);
            var results = new List<ZLevelSoundPortal> { default };

            var directBudget = ZLevelSoundRouteBudget.Unlimited;
            var direct = route.FindRoute(
                (MapData.Grid, grid),
                source,
                directListener,
                RouteOptions(3f, 1f, 1f, 0f) with
                {
                    MediumMode = ZLevelSoundMediumMode.RequirePressure,
                },
                results,
                ref directBudget);
            Assert.Multiple(() =>
            {
                Assert.That(direct.Status, Is.EqualTo(ZLevelSoundRouteStatus.Success));
                Assert.That(direct.PortalsAdded, Is.Zero);
                Assert.That(direct.Distance, Is.EqualTo(2f).Within(0.001f));
                Assert.That(direct.MediumSamples, Is.Zero,
                    "Same-floor audio must retain native compatibility even in vacuum.");
                Assert.That(results.Count, Is.EqualTo(1));
            });

            var invalidBudget = ZLevelSoundRouteBudget.Unlimited;
            var invalid = route.FindRoute(
                (MapData.Grid, grid),
                source,
                directListener,
                RouteOptions(3f, 1f, 1f, 0f) with
                {
                    MediumMode = (ZLevelSoundMediumMode) byte.MaxValue,
                },
                results,
                ref invalidBudget);
            Assert.Multiple(() =>
            {
                Assert.That(invalid.Status, Is.EqualTo(ZLevelSoundRouteStatus.Invalid));
                Assert.That(invalid.PortalsAdded, Is.Zero);
                Assert.That(results.Count, Is.EqualTo(1));
            });

            Configure(0, 2);
            FillLayer(map, grid, center, 3, 0, new Tile(1));
            FillLayer(map, grid, center, 3, 1, new Tile(1));
            var listener = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 1);
            var closedBudget = ZLevelSoundRouteBudget.Unlimited;
            var closed = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                RouteOptions(3f, 1f, 1f, 0f) with
                {
                    MediumMode = ZLevelSoundMediumMode.RequirePressure,
                },
                results,
                ref closedBudget);
            Assert.Multiple(() =>
            {
                Assert.That(closed.Status, Is.EqualTo(ZLevelSoundRouteStatus.NoPortalRoute));
                Assert.That(closed.MediumSamples, Is.Zero,
                    "Missing topology should fail before sampling the acoustic medium.");
                Assert.That(results.Count, Is.EqualTo(1));
            });

            var rangeBudget = ZLevelSoundRouteBudget.Unlimited;
            var outOfRange = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                RouteOptions(0.5f, 1f, 1f, 0f),
                results,
                ref rangeBudget);
            Assert.That(outOfRange.Status, Is.EqualTo(ZLevelSoundRouteStatus.OutOfRange));

            var crossingBudget = ZLevelSoundRouteBudget.Unlimited;
            var crossingLimited = route.FindRoute(
                (MapData.Grid, grid),
                source,
                new ZLevelSoundRouteEndpoint(MapData.Grid, position, 2),
                RouteOptions(10f, 1f, 1f, 0f) with { MaxCrossings = 1 },
                results,
                ref crossingBudget);
            Assert.That(crossingLimited.Status,
                Is.EqualTo(ZLevelSoundRouteStatus.CrossingLimitExceeded));

            var otherGrid = mapManager.CreateGridEntity(MapData.MapId);
            otherGrid.Comp.CanSplit = false;
            var gridBudget = ZLevelSoundRouteBudget.Unlimited;
            var differentGrid = route.FindRoute(
                (MapData.Grid, grid),
                source,
                new ZLevelSoundRouteEndpoint(otherGrid.Owner, position, 1),
                RouteOptions(10f, 1f, 1f, 0f),
                results,
                ref gridBudget);
            Assert.That(differentGrid.Status, Is.EqualTo(ZLevelSoundRouteStatus.DifferentGrid));
            Assert.That(results.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task VerticalRoutesRequireCurrentPressureAndReportTransmissionLoss()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var marker), Is.True);

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var route = SEntMan.System<ZLevelSoundRouteSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var tile = map.TileIndicesFor(MapData.Grid, grid, Xform(marker).Coordinates);
            var upperTile = new ZLevelTileIndices(tile.X, tile.Y, 1);
            var position = map.TileCenterToVector((MapData.Grid, grid), tile);
            var source = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 0);
            var listener = new ZLevelSoundRouteEndpoint(MapData.Grid, position, 1);
            var options = ZLevelSoundRouteOptions.Default(
                5f,
                4,
                ZLevelSoundMediumMode.RequirePressure);
            var results = new List<ZLevelSoundPortal>();

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            Configure(0, 1);
            FillLayer(map, grid, tile, 2, 1, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lowerMixture = SAtmos.GetZLevelTileMixture(
                RelevantAtmos,
                null,
                new ZLevelTileIndices(tile.X, tile.Y, 0),
                true);
            var upperMixture = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperTile, true);
            Assert.That(lowerMixture, Is.Not.Null);
            Assert.That(upperMixture, Is.Not.Null);
            MakeAir(lowerMixture!);
            MakeAir(upperMixture!);
            var provider = SetBoundary(
                tile,
                0,
                ZLevelBoundaryChannels.Sound | ZLevelBoundaryChannels.Atmosphere);

            var budget = ZLevelSoundRouteBudget.Unlimited;
            var pressurized = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                ref budget);
            Assert.Multiple(() =>
            {
                Assert.That(pressurized.Status, Is.EqualTo(ZLevelSoundRouteStatus.Success));
                Assert.That(pressurized.MediumSamples, Is.EqualTo(2),
                    "Endpoints and portal sides should share per-query pressure samples.");
                Assert.That(pressurized.Transmission, Is.InRange(0.70f, 0.76f));
                Assert.That(pressurized.TransmissionLossDecibels, Is.GreaterThan(2f));
                Assert.That(pressurized.EffectiveDistance, Is.GreaterThan(pressurized.Distance));
            });

            upperMixture!.Clear();
            results.Clear();
            budget = ZLevelSoundRouteBudget.Unlimited;
            var vacuum = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                ref budget);
            Assert.Multiple(() =>
            {
                Assert.That(vacuum.Status, Is.EqualTo(ZLevelSoundRouteStatus.MediumBlocked));
                Assert.That(vacuum.PortalsAdded, Is.Zero);
                Assert.That(results, Is.Empty);
            });

            var boundary = SEntMan.GetComponent<ZLevelBoundaryComponent>(provider);
            SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
                (provider, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Atmosphere,
                ZLevelBoundaryChannels.Sound);
            MakeAir(upperMixture);
            budget = ZLevelSoundRouteBudget.Unlimited;
            var sealedRoute = route.FindRoute(
                (MapData.Grid, grid),
                source,
                listener,
                options,
                results,
                ref budget);
            Assert.That(sealedRoute.Status, Is.EqualTo(ZLevelSoundRouteStatus.NoPortalRoute));
        });
    }

    private void Configure(int minimumZ, int maximumZ)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            MapData.MapUid,
            minimumZ,
            maximumZ,
            minimumZ,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        MapData.Grid.Comp.CanSplit = false;
    }

    private Vector2i GetFixtureCenter(SharedMapSystem map, MapGridComponent grid)
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var marker), Is.True);
        return map.TileIndicesFor(MapData.Grid, grid, Xform(marker).Coordinates);
    }

    private void FillLayer(
        SharedMapSystem map,
        MapGridComponent grid,
        Vector2i center,
        int radius,
        int z,
        Tile tile)
    {
        for (var y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
            {
                map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(x, y, z), tile);
            }
        }
    }

    private EntityUid SetBoundary(
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels opens)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        map.SetZLevelTile(
            MapData.Grid,
            grid,
            new ZLevelTileIndices(tile.X, tile.Y, lowerLocalZ),
            new Tile(1));
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(MapData.Grid, grid, tile));
        Assert.That(zLevels.SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            ZLevelBoundaryChannels.None);
        transform.AnchorEntity(provider, SEntMan.GetComponent<TransformComponent>(provider));
        Assert.That(SEntMan.GetComponent<TransformComponent>(provider).Anchored, Is.True);
        return provider;
    }

    private static ZLevelSoundRouteOptions RouteOptions(
        float maxDistance,
        float verticalDistance,
        float explicitTransmission,
        float lossScale)
    {
        return new ZLevelSoundRouteOptions(
            maxDistance,
            4,
            verticalDistance,
            1f,
            explicitTransmission,
            0.001f,
            lossScale,
            ZLevelSoundMediumMode.Ignore,
            1f,
            101.325f,
            0.5f);
    }

    private static void AssertFailure(
        ZLevelSoundRouteSystem route,
        Entity<MapGridComponent> grid,
        ZLevelSoundRouteEndpoint source,
        ZLevelSoundRouteEndpoint listener,
        ZLevelSoundRouteOptions options,
        List<ZLevelSoundPortal> results,
        ZLevelSoundRouteBudget budget,
        ZLevelSoundRouteStatus expected)
    {
        var result = route.FindRoute(grid, source, listener, options, results, ref budget);
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(expected));
            Assert.That(result.PortalsAdded, Is.Zero);
            Assert.That(results.Count, Is.EqualTo(1), "A failed route must preserve caller-owned entries.");
        });
    }

    private static int RouteRepeated(
        ZLevelSoundRouteSystem route,
        Entity<MapGridComponent> grid,
        ZLevelSoundRouteEndpoint source,
        ZLevelSoundRouteEndpoint listener,
        ZLevelSoundRouteOptions options,
        List<ZLevelSoundPortal> results,
        int count)
    {
        var accepted = 0;
        for (var i = 0; i < count; i++)
        {
            results.Clear();
            var budget = ZLevelSoundRouteBudget.Unlimited;
            if (route.FindRoute(grid, source, listener, options, results, ref budget).Succeeded)
                accepted++;
        }

        return accepted;
    }

    private static void MakeAir(GasMixture mixture)
    {
        mixture.Clear();
        mixture.Temperature = Atmospherics.T20C;
        mixture.AdjustMoles(Gas.Nitrogen, 100f);
    }
}
