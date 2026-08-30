// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Light.Components;
using Content.Shared.Maps;
using Content.Shared.Weather;
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
public sealed class ZLevelWeatherExposureTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> GrateTile = "ZLevelGrate";
    private static readonly ProtoId<ContentTileDefinition> PlanetTile = "FloorPlanetDirt";
    private static readonly ProtoId<ContentTileDefinition> SteelTile = "FloorSteel";

    [Test]
    public async Task LegacyPolicyRetainsPlanarZZeroBehavior()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var roofs = SEntMan.System<Content.Server.Light.EntitySystems.RoofSystem>();
            var weather = SEntMan.System<Content.Server.Weather.WeatherSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);
            var steel = prototypes.Index(SteelTile);
            var tile = new Vector2i(3, 5);
            var zTile = new ZLevelTileIndices(tile.X, tile.Y, 0);

            grid.CanSplit = false;
            map.SetTile(testMap.Grid, grid, tile, new Tile(planet.TileId));
            var tileRef = map.GetTileRef(testMap.Grid, grid, tile);
            Assert.Multiple(() =>
            {
                Assert.That(weather.CanWeatherAffect((testMap.Grid, grid, null), tileRef), Is.True);
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), zTile).Termination,
                    Is.EqualTo(WeatherExposureTermination.Exposed));
            });

            var roof = SEntMan.EnsureComponent<RoofComponent>(testMap.Grid);
            roofs.SetRoof((testMap.Grid, grid, roof), tile, true);
            Assert.Multiple(() =>
            {
                Assert.That(weather.CanWeatherAffect((testMap.Grid, grid, roof), tileRef), Is.False);
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, roof), zTile).Termination,
                    Is.EqualTo(WeatherExposureTermination.PlanarRoof));
            });

            roofs.SetRoof((testMap.Grid, grid, roof), tile, false);
            map.SetTile(testMap.Grid, grid, tile, new Tile(steel.TileId));
            var dryTile = map.GetTileRef(testMap.Grid, grid, tile);
            Assert.Multiple(() =>
            {
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, roof), zTile).Termination,
                    Is.EqualTo(WeatherExposureTermination.TileDisallowsWeather));
                Assert.That(weather.CanWeatherAffect((testMap.Grid, grid, roof), dryTile), Is.False);
                Assert.That(weather.GetWeatherExposure(
                        (testMap.Grid, grid, roof),
                        new ZLevelTileIndices(tile.X, tile.Y, 1)).Termination,
                    Is.EqualTo(WeatherExposureTermination.InvalidLevel));
            });
        });
    }

    [Test]
    public async Task ConfiguredPolicyCombinesLocalTileBlockersAndSkyColumn()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var weather = SEntMan.System<Content.Server.Weather.WeatherSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);
            var grate = prototypes.Index(GrateTile);
            var steel = prototypes.Index(SteelTile);
            var tile = new Vector2i(4, 6);
            var coordinates = map.GridTileToLocal(testMap.Grid, grid, tile);
            var floorZero = new ZLevelTileIndices(tile.X, tile.Y, 0);
            var floorOne = new ZLevelTileIndices(tile.X, tile.Y, 1);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(testMap.Grid, grid, floorZero, new Tile(planet.TileId));
            map.SetZLevelTile(testMap.Grid, grid, floorOne, new Tile(grate.TileId));

            Assert.Multiple(() =>
            {
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), floorZero).IsExposed,
                    Is.True);
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), floorOne).IsExposed,
                    Is.True);
            });

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var allExposed = true;
            for (var i = 0; i < 1_000; i++)
            {
                allExposed &= weather.GetWeatherExposure(
                    (testMap.Grid, grid, null),
                    floorZero).IsExposed;
            }

            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.Multiple(() =>
            {
                Assert.That(allExposed, Is.True);
                Assert.That(allocated, Is.LessThanOrEqualTo(512),
                    "Hot weather-policy queries should not allocate per visible tile.");
            });

            var blocker = SEntMan.SpawnEntity(null, coordinates);
            Assert.That(zLevels.SetZLevelPosition(blocker, 1), Is.True);
            SEntMan.EnsureComponent<BlockWeatherComponent>(blocker);
            var blockerTransform = SEntMan.GetComponent<TransformComponent>(blocker);
            transform.AnchorEntity(blocker, blockerTransform);
            Assert.Multiple(() =>
            {
                Assert.That(blockerTransform.Anchored, Is.True);
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), floorOne).Termination,
                    Is.EqualTo(WeatherExposureTermination.AnchoredBlocker));
                Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), floorZero).IsExposed,
                    Is.True,
                    "A blocker on another local floor must not leak through the planar anchored lookup.");
            });

            var actor = SEntMan.SpawnEntity(null, coordinates);
            Assert.That(zLevels.SetZLevelPosition(actor, 1), Is.True);
            Assert.That(weather.GetWeatherExposure(actor).Termination,
                Is.EqualTo(WeatherExposureTermination.AnchoredBlocker));

            SEntMan.DeleteEntity(blocker);
            Assert.That(weather.GetWeatherExposure(actor).IsExposed, Is.True);

            var roof = SpawnAnchored("ZLevelRoofMarker", coordinates, 2);
            var blockedBySky = weather.GetWeatherExposure((testMap.Grid, grid, null), floorZero);
            Assert.Multiple(() =>
            {
                Assert.That(blockedBySky.Termination, Is.EqualTo(WeatherExposureTermination.SkyBlocked));
                Assert.That(blockedBySky.SkyTermination,
                    Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
            });

            SEntMan.DeleteEntity(roof);
            map.SetZLevelTile(testMap.Grid, grid, floorZero, new Tile(steel.TileId));
            Assert.That(weather.GetWeatherExposure((testMap.Grid, grid, null), floorZero).Termination,
                Is.EqualTo(WeatherExposureTermination.TileDisallowsWeather));

            map.SetZLevelTile(testMap.Grid, grid, floorZero, new Tile(planet.TileId));
            Assert.That(weather.GetWeatherExposure(
                    (testMap.Grid, grid, null),
                    new ZLevelTileIndices(tile.X, tile.Y, 3)).Termination,
                Is.EqualTo(WeatherExposureTermination.InvalidLevel));

            EntityUid SpawnAnchored(string prototype, EntityCoordinates at, int z)
            {
                var uid = SEntMan.SpawnEntity(prototype, at);
                var xform = SEntMan.GetComponent<TransformComponent>(uid);
                transform.Unanchor(uid, xform);
                Assert.That(zLevels.SetZLevelPosition(uid, z), Is.True);
                transform.AnchorEntity(uid, xform);
                Assert.That(xform.Anchored, Is.True);
                return uid;
            }
        });
    }

    [Test]
    public async Task WorldFloorQueriesFollowMovingFrames()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var weather = SEntMan.System<Content.Server.Weather.WeatherSystem>();
            var zLevelMaps = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var planet = prototypes.Index(PlanetTile);
            var tile = new Vector2i(2, 7);
            var localFloor = new ZLevelTileIndices(tile.X, tile.Y, 1);

            grid.CanSplit = false;
            zLevelMaps.Configure(
                testMap.MapUid,
                0,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(testMap.Grid, grid, localFloor, new Tile(planet.TileId));
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 5), Is.True);

            var exposed = weather.GetWeatherExposureAtWorldZ(
                (testMap.Grid, grid, null),
                tile,
                6);
            var oldWorldFloor = weather.GetWeatherExposureAtWorldZ(
                (testMap.Grid, grid, null),
                tile,
                1);
            Assert.Multiple(() =>
            {
                Assert.That(exposed.IsExposed, Is.True);
                Assert.That(exposed.Tile, Is.EqualTo(localFloor));
                Assert.That(oldWorldFloor.Termination, Is.EqualTo(WeatherExposureTermination.InvalidLevel));
            });

            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, 8), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(weather.GetWeatherExposureAtWorldZ(
                    (testMap.Grid, grid, null), tile, 9), Is.EqualTo(exposed));
                Assert.That(weather.GetWeatherExposureAtWorldZ(
                        (testMap.Grid, grid, null), tile, 6).Termination,
                    Is.EqualTo(WeatherExposureTermination.InvalidLevel));
            });
        });
    }
}
