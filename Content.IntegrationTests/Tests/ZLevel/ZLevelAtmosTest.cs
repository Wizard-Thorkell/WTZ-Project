// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.IntegrationTests.Tests.Atmos;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Explosion.EntitySystems;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos.Components;
using Content.Shared.Tests;
using NUnit.Framework;
using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.NodeContainer;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelAtmosTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [TestPrototypes]
    private const string Prototypes = @"
- type: explosion
  id: ZLevelAtmosHeatTest
  damagePerIntensity:
    types:
      Heat: 0
  tileBreakChance: [0]
  tileBreakIntensity: [0]
  temperature: 1500
";

    [Test]
    public async Task CoordinateHotspotApisStayOnRequestedZLevel()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lower = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, lowerIndices, true);
            var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
            Assert.That(lower, Is.Not.Null);
            Assert.That(upper, Is.Not.Null);
            MakeCombustible(lower!);
            MakeCombustible(upper!);

            SAtmos.HotspotExpose(RelevantAtmos, upperIndices, 1500f, 100f);
            Assert.Multiple(() =>
            {
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, upperIndices), Is.True);
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, lowerIndices), Is.False);
            });

            SAtmos.HotspotExtinguish(MapData.Grid, upperIndices);
            SAtmos.HotspotExpose(RelevantAtmos, sourceTile, 1500f, 100f);
            Assert.Multiple(() =>
            {
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, upperIndices), Is.False);
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, lowerIndices), Is.True,
                    "The legacy Vector2i overload must retain its Z=0 behavior.");
            });
        });
    }

    [Test]
    public async Task ExplosionHeatUsesCapturedUpperFloorAndFrame()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var sourceMarker), Is.True);
        ZLevelTileIndices lowerIndices = default;
        ZLevelTileIndices upperIndices = default;

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(sourceMarker).Coordinates);
            lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
            upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);

            SEntMan.System<SharedZLevelMapSystem>().Configure(
                MapData.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            Assert.That(Transform.SetZLevelFrameOrigin(MapData.Grid, 5), Is.True);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lower = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, lowerIndices, true);
            var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
            Assert.That(lower, Is.Not.Null);
            Assert.That(upper, Is.Not.Null);
            MakeCombustible(lower!);
            MakeCombustible(upper!);
            SAtmos.SetAtmosphereSimulation(RelevantAtmos, false);

            var source = SEntMan.SpawnEntity(null, Xform(sourceMarker).Coordinates);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(source, 1), Is.True);
            Assert.That(Transform.GetWorldZLevel(source), Is.EqualTo(6));
            SEntMan.System<ExplosionSystem>().QueueExplosion(
                source,
                "ZLevelAtmosHeatTest",
                totalIntensity: 12f,
                slope: 4f,
                maxTileIntensity: 6f,
                tileBreakScale: 1f,
                maxTileBreak: 0,
                canCreateVacuum: false,
                addLog: false);
        });

        await RunTicks(20);

        await Server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, upperIndices), Is.True,
                    "Explosion heat must ignite the grid-local floor captured at queue time.");
                Assert.That(SAtmos.IsHotspotActive(MapData.Grid, lowerIndices), Is.False,
                    "An overlapping base-floor atmosphere tile must remain untouched.");
            });
        });
    }

    [Test]
    public async Task NonAtmosphereOpeningDoesNotConnectAtmosphereLayers()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var boundarySystem = SEntMan.System<SharedZLevelBoundarySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));

            var provider = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
            boundarySystem.SetBoundary(
                (provider, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Projectile,
                ZLevelBoundaryChannels.None);
            Transform.AnchorEntity(provider, Xform(provider));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lower = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, lowerIndices);
            Assert.That(lower, Is.Not.Null);
            Assert.That(lower!.AdjacentTileAbove, Is.Null,
                "A Projectile-only opening must not create an atmosphere or fire path.");

            boundarySystem.SetBoundary(
                (provider, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Projectile | ZLevelBoundaryChannels.Atmosphere,
                ZLevelBoundaryChannels.None);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            Assert.That(lower.AdjacentTileAbove, Is.Not.Null,
                "Adding the Atmosphere channel must connect the same vertical boundary.");
        });
    }

    [Test]
    public async Task OverlayComponentStateKeepsFloorsSeparateWithoutPvs()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);
        Vector2i sourceTile = default;
        NetEntity gridNetEntity = default;

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lower = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, lowerIndices, true);
            var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
            Assert.That(lower, Is.Not.Null);
            Assert.That(upper, Is.Not.Null);
            SetOverlayTemperature(lower!, 400f);
            SetOverlayTemperature(upper!, 800f);
            SAtmos.SetAtmosphereSimulation(RelevantAtmos, false);
            SAtmos.InvalidateVisuals((MapData.Grid.Owner, ProcessEnt.Comp2), lowerIndices);
            SAtmos.InvalidateVisuals((MapData.Grid.Owner, ProcessEnt.Comp2), upperIndices);
            gridNetEntity = SEntMan.GetNetEntity(MapData.Grid);
        });

        await RunTicks(90);

        await Client.WaitPost(() =>
        {
            var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
            Assert.Multiple(() =>
            {
                Assert.That(ReadOverlayTemperature(overlay, 0, sourceTile), Is.EqualTo(400f).Within(0.01f));
                Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(800f).Within(0.01f));
            });
        });

        await Server.WaitPost(() =>
        {
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
            Assert.That(upper, Is.Not.Null);
            SetOverlayTemperature(upper!, 600f);
            SAtmos.InvalidateVisuals((MapData.Grid.Owner, ProcessEnt.Comp2), upperIndices);
        });

        await RunTicks(90);

        await Client.WaitPost(() =>
        {
            var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
            Assert.Multiple(() =>
            {
                Assert.That(ReadOverlayTemperature(overlay, 0, sourceTile), Is.EqualTo(400f).Within(0.01f));
                Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(600f).Within(0.01f));
            });
        });
    }

    [Test]
    public async Task OverlayPvsTracksViewerWorldFloorsAndRemoteViews()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var sourceMarker), Is.True);
        var configuration = Server.ResolveDependency<IConfigurationManager>();
        EntityUid remoteViewer = default;

        try
        {
            Vector2i sourceTile = default;
            NetEntity gridNetEntity = default;
            ZLevelMetricsSnapshot overlayMetrics = default;

            await Server.WaitPost(() =>
            {
                SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

                var mapSystem = SEntMan.System<SharedMapSystem>();
                var zLevels = SEntMan.System<SharedZLevelSystem>();
                var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
                var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
                Transform.SetCoordinates(SPlayer, Xform(sourceMarker).Coordinates);
                sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(sourceMarker).Coordinates);
                var lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
                var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);

                SEntMan.System<SharedZLevelMapSystem>().Configure(
                    MapData.MapUid,
                    0,
                    1,
                    0,
                    ZLevelDefaultBoundaryMode.TileAboveCloses);
                Assert.That(Transform.SetZLevelFrameOrigin(MapData.Grid, 5), Is.True);
                mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
                SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

                var lower = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, lowerIndices, true);
                var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
                Assert.That(lower, Is.Not.Null);
                Assert.That(upper, Is.Not.Null);
                SetOverlayTemperature(lower!, 400f);
                SetOverlayTemperature(upper!, 800f);
                SAtmos.SetAtmosphereSimulation(RelevantAtmos, false);

                ProcessEnt.Comp2.InvalidTiles.Clear();
                ProcessEnt.Comp2.InvalidZLevelTiles.Clear();
                metrics.ResetCounters();
                SAtmos.InvalidateVisuals((MapData.Grid.Owner, ProcessEnt.Comp2), lowerIndices);
                SAtmos.InvalidateVisuals((MapData.Grid.Owner, ProcessEnt.Comp2), upperIndices);
                Assert.That(zLevels.SetZLevelPosition(SPlayer, 0), Is.True);
                gridNetEntity = SEntMan.GetNetEntity(MapData.Grid);
            });

            await RunTicks(90);

            await Server.WaitPost(() =>
            {
                var metrics = SEntMan.System<SharedZLevelMetricsSystem>().Snapshot();
                overlayMetrics = metrics;
                var chunkIndex = SharedGasTileOverlaySystem.GetGasChunkIndices(sourceTile);
                Assert.Multiple(() =>
                {
                    Assert.That(ProcessEnt.Comp2.Chunks.ContainsKey(chunkIndex), Is.True);
                    Assert.That(ProcessEnt.Comp2.ZLevelChunks.TryGetValue(1, out var upperChunks), Is.True);
                    Assert.That(upperChunks!.ContainsKey(chunkIndex), Is.True);
                    Assert.That(metrics.AtmosOverlayUpdates, Is.GreaterThanOrEqualTo(1));
                    Assert.That(metrics.AtmosOverlayInvalidatedTiles, Is.GreaterThanOrEqualTo(2));
                    Assert.That(metrics.AtmosOverlayInvalidatedUpperTiles, Is.GreaterThanOrEqualTo(1));
                    Assert.That(metrics.AtmosOverlayUpperLayers, Is.GreaterThanOrEqualTo(1));
                    Assert.That(metrics.AtmosOverlayUpdatedChunks, Is.GreaterThanOrEqualTo(2));
                    Assert.That(metrics.AtmosOverlayMilliseconds, Is.GreaterThanOrEqualTo(0d));
                });
            });

            TestContext.Progress.WriteLine(
                $"WTZ atmosphere overlay baseline: updates={overlayMetrics.AtmosOverlayUpdates}, " +
                $"tiles={overlayMetrics.AtmosOverlayInvalidatedTiles}, " +
                $"upper-tiles={overlayMetrics.AtmosOverlayInvalidatedUpperTiles}, " +
                $"upper-layers={overlayMetrics.AtmosOverlayUpperLayers}, " +
                $"changed-chunks={overlayMetrics.AtmosOverlayUpdatedChunks}, " +
                $"elapsed={overlayMetrics.AtmosOverlayMilliseconds:0.000}ms");

            await Client.WaitPost(() =>
            {
                var clientGrid = CEntMan.GetEntity(gridNetEntity);
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(clientGrid);
                Assert.Multiple(() =>
                {
                    Assert.That(ReadOverlayTemperature(overlay, 0, sourceTile), Is.EqualTo(400f).Within(0.01f));
                    Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(800f).Within(0.01f));
                });
            });

            await Server.WaitPost(() => configuration.SetCVar(CVars.NetPVS, true));
            await RunTicks(90);

            await Client.WaitPost(() =>
            {
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
                Assert.Multiple(() =>
                {
                    Assert.That(ReadOverlayTemperature(overlay, 0, sourceTile), Is.EqualTo(400f).Within(0.01f));
                    Assert.That(HasOverlayChunk(overlay, 1, sourceTile), Is.False,
                        "Enabling PVS must discard layers outside the player's current world Z.");
                });
            });

            await Server.WaitPost(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(SPlayer, 1), Is.True);
                Assert.That(Transform.GetWorldZLevel(SPlayer), Is.EqualTo(6));
            });
            await RunTicks(90);

            await Client.WaitPost(() =>
            {
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
                Assert.Multiple(() =>
                {
                    Assert.That(HasOverlayChunk(overlay, 0, sourceTile), Is.False);
                    Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(800f).Within(0.01f));
                });
            });

            await Server.WaitPost(() =>
            {
                remoteViewer = SEntMan.SpawnEntity(null, Xform(SPlayer).Coordinates);
                Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(remoteViewer, 0), Is.True);
                SEntMan.System<ViewSubscriberSystem>().AddViewSubscriber(remoteViewer, ServerSession);
            });
            await RunTicks(90);

            await Client.WaitPost(() =>
            {
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
                Assert.Multiple(() =>
                {
                    Assert.That(ReadOverlayTemperature(overlay, 0, sourceTile), Is.EqualTo(400f).Within(0.01f));
                    Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(800f).Within(0.01f));
                });
            });

            await Server.WaitPost(() =>
            {
                SEntMan.System<ViewSubscriberSystem>().RemoveViewSubscriber(remoteViewer, ServerSession);
                SEntMan.DeleteEntity(remoteViewer);
                remoteViewer = default;
            });
            await RunTicks(90);

            await Client.WaitPost(() =>
            {
                var overlay = CEntMan.GetComponent<GasTileOverlayComponent>(CEntMan.GetEntity(gridNetEntity));
                Assert.Multiple(() =>
                {
                    Assert.That(HasOverlayChunk(overlay, 0, sourceTile), Is.False);
                    Assert.That(ReadOverlayTemperature(overlay, 1, sourceTile), Is.EqualTo(800f).Within(0.01f));
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (remoteViewer.IsValid() && SEntMan.EntityExists(remoteViewer))
                {
                    SEntMan.System<ViewSubscriberSystem>().RemoveViewSubscriber(remoteViewer, ServerSession);
                    SEntMan.DeleteEntity(remoteViewer);
                }

                configuration.SetCVar(CVars.NetPVS, false);
            });
            await RunTicks(5);
        }
    }

    [Test]
    public async Task CeilingTileInvalidatesLowerAtmosAdjacency()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var sourceZTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);

            var lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Not.Null,
                "Expected open sky above the source tile before adding a ceiling.");

            mapSystem.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1), new Tile(1));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Null,
                "Expected the source tile's vertical atmos adjacency to close after adding a ceiling tile above it.");

            var marker = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
                (marker, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Atmosphere,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Not.Null,
                "Expected an explicit atmosphere opening to override the ceiling tile.");

            SEntMan.DeleteEntity(marker);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Null,
                "Expected removing the explicit opening to restore the closed ceiling boundary.");
        });
    }

    [Test]
    public async Task EntityContainingMixtureUsesCurrentZLevel()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            // Finish any cycle left in progress by the shared fixture so the
            // tile change below is observed by the next Revalidate stage.
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var upperTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);

            var lowerMixture = SAtmos.GetTileMixture(RelevantAtmos, null, sourceTile, true);
            Assert.That(lowerMixture, Is.Not.Null, "Expected a base-layer gas mixture for the source tile.");
            lowerMixture!.AdjustMoles(Gas.Oxygen, 100f);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperTile, new Tile(1));

            var parent = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            SEntMan.EnsureComponent<ZLevelPositionComponent>(parent).ZLevel = 1;
            var child = SEntMan.SpawnEntity(null, new EntityCoordinates(parent, Vector2.Zero));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lowerPressure = lowerMixture.Pressure;
            var directUpperTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, upperTile);
            var directUpperMixture = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperTile);
            var transformSystem = SEntMan.System<SharedTransformSystem>();
            var parentTransform = Xform(parent);
            var upperMixture = SAtmos.GetContainingMixture(parent, ignoreExposed: true);
            var childMixture = SAtmos.GetContainingMixture(child, ignoreExposed: true);

            Assert.That(lowerPressure, Is.GreaterThan(1f));
            Assert.That(directUpperTile, Is.Not.Null,
                "Expected revalidation to create an atmosphere cell for the upper tile.");
            Assert.That(directUpperMixture, Is.Not.Null,
                "Expected revalidation to create a gas mixture for the upper tile.");
            Assert.That(parentTransform.GridUid, Is.EqualTo(MapData.Grid.Owner));
            Assert.That(transformSystem.GetZLevel((parent, parentTransform, null)), Is.EqualTo(1));
            Assert.That(upperMixture, Is.Not.Null);
            Assert.That(upperMixture!.Pressure, Is.LessThan(1f),
                "Expected the z=1 parent to sample the open upper layer instead of the pressurized z=0 room.");
            Assert.That(upperMixture.Pressure, Is.LessThan(lowerPressure));
            Assert.That(childMixture, Is.Not.Null);
            Assert.That(childMixture!.Pressure, Is.LessThan(1f),
                "Expected the child entity to inherit its parent's z-level instead of sampling z=0.");
        });
    }

    [Test]
    public async Task SparseUpperTileAtmosphereRebuildsAndEnumerates()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var sparseTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 256);
            mapSystem.SetZLevelTile(MapData.Grid, grid, sparseTile, new Tile(1));

            SAtmos.InvalidateAllTiles((MapData.Grid.Owner, grid, RelevantAtmos.Comp));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mixture = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, sparseTile, true);
            Assert.That(mixture, Is.Not.Null);
            mixture!.AdjustMoles(Gas.Nitrogen, 12f);

            Assert.That(SAtmos.GetAllMixtures(MapData.Grid), Does.Contain(mixture),
                "Expected global atmosphere operations to include upper-layer mixtures.");
        });
    }

    [Test]
    public async Task AirtightPositionTracksZLevelChanges()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var transformSystem = SEntMan.System<SharedTransformSystem>();
            var zLevelSystem = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var upperTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperTile, new Tile(1));

            var airtightUid = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            zLevelSystem.SetZLevelPosition(airtightUid, 1);
            var airtight = SEntMan.EnsureComponent<AirtightComponent>(airtightUid);
            transformSystem.AnchorEntity(airtightUid, Xform(airtightUid));

            Assert.That(airtight.LastZLevel, Is.EqualTo(1));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            zLevelSystem.SetZLevelPosition(airtightUid, 0);

            Assert.Multiple(() =>
            {
                Assert.That(airtight.LastZLevel, Is.EqualTo(0));
                Assert.That(RelevantAtmos.Comp.InvalidatedZLevelCoords, Does.Contain(upperTile));
                Assert.That(RelevantAtmos.Comp.InvalidatedCoords, Does.Contain(sourceTile));
            });
        });
    }

    [Test]
    public async Task TileFireEventsStayOnTheirZLevel()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var zLevelSystem = SEntMan.System<SharedZLevelSystem>();
            var listener = SEntMan.System<ZLevelTileFireListenerSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            Assert.That(Transform.SetZLevelFrameOrigin(MapData.Grid, 5), Is.True);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lowerEntity = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            var upperEntity = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            zLevelSystem.SetZLevelPosition(upperEntity, 1);
            SEntMan.EnsureComponent<TestListenerComponent>(lowerEntity);
            SEntMan.EnsureComponent<TestListenerComponent>(upperEntity);

            Assert.Multiple(() =>
            {
                Assert.That(Transform.GetWorldZLevel(lowerEntity), Is.EqualTo(5));
                Assert.That(Transform.GetWorldZLevel(upperEntity), Is.EqualTo(6));
            });

            var upperTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, upperIndices);
            Assert.That(upperTile?.Air, Is.Not.Null);
            var upperAir = upperTile!.Air!;
            upperAir.Clear();
            upperAir.Temperature = Atmospherics.T20C;
            upperAir.AdjustMoles(Gas.Plasma, 100f);
            upperAir.AdjustMoles(Gas.Oxygen, 900f);
            SAtmos.HotspotExpose(upperTile, 1000f, 100f);

            SAtmos.RunProcessingStage(ProcessEnt, AtmosphereProcessingState.Hotspots);
            SAtmos.RunProcessingStage(ProcessEnt, AtmosphereProcessingState.Hotspots);

            Assert.Multiple(() =>
            {
                Assert.That(listener.Count(upperEntity), Is.GreaterThan(0));
                Assert.That(listener.Count(lowerEntity), Is.Zero,
                    "An upper-floor fire must not burn an entity sharing only its XY position.");
            });
        });
    }

    [Test]
    public async Task ExplicitVerticalOpeningTransfersGas()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var boundarySystem = SEntMan.System<SharedZLevelBoundarySystem>();
            var transformSystem = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var lowerIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);
            var upperIndices = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(1));
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lower = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, lowerIndices, true);
            var upper = SAtmos.GetZLevelTileMixture(RelevantAtmos, null, upperIndices, true);
            Assert.That(lower, Is.Not.Null);
            Assert.That(upper, Is.Not.Null);

            lower!.Clear();
            upper!.Clear();
            lower.Temperature = Atmospherics.T20C;
            upper.Temperature = Atmospherics.T20C;
            lower.AdjustMoles(Gas.Oxygen, 100f);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
            Assert.That(upper.GetMoles(Gas.Oxygen), Is.Zero.Within(0.001f),
                "The solid upper tile should seal the vertical boundary.");

            var marker = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            boundarySystem.SetBoundary(
                (marker, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Atmosphere,
                ZLevelBoundaryChannels.None);
            transformSystem.AnchorEntity(marker, Xform(marker));

            for (var i = 0; i < 5; i++)
                SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            Assert.Multiple(() =>
            {
                Assert.That(upper.GetMoles(Gas.Oxygen), Is.GreaterThan(0.001f));
                Assert.That(lower.GetMoles(Gas.Oxygen), Is.LessThan(100f));
            });
        });
    }

    [Test]
    public async Task PipeNetworksAndOverlapStayOnTheirZLevel()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            var zLevelSystem = SEntMan.System<SharedZLevelSystem>();
            var nodeContainerSystem = SEntMan.System<NodeContainerSystem>();
            var nodeGroupSystem = SEntMan.System<NodeGroupSystem>();
            var overlapSystem = SEntMan.System<PipeRestrictOverlapSystem>();
            var sourceCoords = Xform(source).Coordinates;

            var upper = SEntMan.SpawnEntity("GasPipeStraight", sourceCoords);
            zLevelSystem.SetZLevelPosition(upper, 1);

            var lower = SEntMan.SpawnEntity("GasPipeStraight", sourceCoords);
            var lowerNeighbor = SEntMan.SpawnEntity(
                "GasPipeStraight",
                new EntityCoordinates(sourceCoords.EntityId, sourceCoords.Position + Vector2.UnitY));

            nodeGroupSystem.ForceUpdate();

            var upperContainer = SEntMan.GetComponent<NodeContainerComponent>(upper);
            var lowerContainer = SEntMan.GetComponent<NodeContainerComponent>(lower);
            var neighborContainer = SEntMan.GetComponent<NodeContainerComponent>(lowerNeighbor);
            Assert.That(nodeContainerSystem.TryGetNode<PipeNode>(upperContainer, "pipe", out var upperNode), Is.True);
            Assert.That(nodeContainerSystem.TryGetNode<PipeNode>(lowerContainer, "pipe", out var lowerNode), Is.True);
            Assert.That(nodeContainerSystem.TryGetNode<PipeNode>(neighborContainer, "pipe", out var neighborNode), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(Xform(upper).Anchored, Is.True);
                Assert.That(Xform(lower).Anchored, Is.True,
                    "A pipe on another Z-level must not block anchoring at the same XY position.");
                Assert.That(overlapSystem.CheckOverlap(lower), Is.False);
                Assert.That(lowerNode!.NodeGroup, Is.SameAs(neighborNode!.NodeGroup),
                    "Adjacent pipes on the same Z-level should still connect.");
                Assert.That(upperNode!.NodeGroup, Is.Not.SameAs(lowerNode.NodeGroup),
                    "Pipes on different Z-levels must not share a pipe network.");
            });
        });
    }

    private static void MakeCombustible(GasMixture mixture)
    {
        mixture.Clear();
        mixture.Temperature = Atmospherics.T20C;
        mixture.AdjustMoles(Gas.Plasma, 100f);
        mixture.AdjustMoles(Gas.Oxygen, 900f);
    }

    private static void SetOverlayTemperature(GasMixture mixture, float temperature)
    {
        mixture.Clear();
        mixture.AdjustMoles(Gas.Nitrogen, 100f);
        mixture.Temperature = temperature;
    }

    private static bool HasOverlayChunk(GasTileOverlayComponent overlay, int localZ, Vector2i tile)
    {
        return overlay.TryGetChunks(localZ, out var chunks) &&
               chunks.ContainsKey(SharedGasTileOverlaySystem.GetGasChunkIndices(tile));
    }

    private static float ReadOverlayTemperature(GasTileOverlayComponent overlay, int localZ, Vector2i tile)
    {
        Assert.That(overlay.TryGetChunks(localZ, out var chunks), Is.True,
            $"Missing gas overlay layer Z={localZ}.");
        var chunkIndex = SharedGasTileOverlaySystem.GetGasChunkIndices(tile);
        Assert.That(chunks.TryGetValue(chunkIndex, out var chunk), Is.True,
            $"Missing gas overlay chunk {chunkIndex} on Z={localZ}.");
        var localX = MathHelper.Mod(tile.X, SharedGasTileOverlaySystem.ChunkSize);
        var localY = MathHelper.Mod(tile.Y, SharedGasTileOverlaySystem.ChunkSize);
        var data = chunk!.TileData[localX + localY * SharedGasTileOverlaySystem.ChunkSize];
        Assert.That(data.ByteGasTemperature.TryGetTemperature(out var temperature), Is.True);
        return temperature;
    }
}

public sealed class ZLevelTileFireListenerSystem : TestListenerSystem<TileFireEvent>
{
}
