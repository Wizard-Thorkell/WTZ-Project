// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Maps;
using Content.Shared.Tiles;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelVerticalContentTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> GrateTile = "ZLevelGrate";
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";
    private static readonly ProtoId<ContentTileDefinition> SteelTile = "FloorSteel";

    [Test]
    public async Task AuthoredSurfacesResolveChannelsSupportAndConstructionContent()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var prototypes = Server.ResolveDependency<IPrototypeManager>();
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var sky = SEntMan.System<SharedZLevelSkyExposureSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var grate = prototypes.Index(GrateTile);
            var shaft = prototypes.Index(ShaftTile);
            var tile = new Vector2i(3, 2);
            var coordinates = new EntityCoordinates(
                testMap.Grid,
                (Vector2) tile + new Vector2(0.5f, 0.5f));

            grid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, 2, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), testMap.Tile.Tile);
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 1),
                new Tile(grate.TileId));
            map.SetZLevelTile(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(tile.X, tile.Y, 2),
                new Tile(shaft.TileId));

            Assert.Multiple(() =>
            {
                Assert.That(grate.MapAtmosphere, Is.False,
                    "An interior grate connects floor atmospheres without becoming map atmosphere.");
                Assert.That(grate.Sturdy, Is.True);
                Assert.That(shaft.Sturdy, Is.False);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, tile, 0, 1,
                    ZLevelBoundaryChannels.Body), Is.False);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, tile, 0, 1,
                    ZLevelBoundaryChannels.Interaction), Is.False);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, tile, 0, 1,
                    ZLevelBoundaryChannels.Traversal), Is.False);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, tile, 0, 1,
                    ZLevelBoundaryChannels.Atmosphere |
                    ZLevelBoundaryChannels.Visibility |
                    ZLevelBoundaryChannels.Weather |
                    ZLevelBoundaryChannels.Sound |
                    ZLevelBoundaryChannels.Effects |
                    ZLevelBoundaryChannels.Projectile |
                    ZLevelBoundaryChannels.Explosion), Is.True);
                Assert.That(boundaries.IsOpen(testMap.Grid, grid, tile, 1, 2,
                    ZLevelBoundaryChannels.All), Is.True);
                Assert.That(sky.GetExposure(
                    (testMap.Grid, grid),
                    new ZLevelTileIndices(tile.X, tile.Y, 0)).IsExposed, Is.True);
            });

            var catwalk = SpawnAnchored("Catwalk", coordinates, 2);
            Assert.Multiple(() =>
            {
                Assert.That(boundaries.HasBoundaryProvider(testMap.Grid, tile, 1), Is.True);
                Assert.That(boundaries.CanBodyPass(testMap.Grid, grid, tile, 2, 1), Is.False);
            });

            var actor = SEntMan.SpawnEntity(null, coordinates);
            Assert.That(zLevels.EnsureZLevelEntity(actor, 2), Is.True);
            Assert.That(zLevels.TryGetSupportTile(actor, out var support), Is.True);
            Assert.That(support.GridIndices, Is.EqualTo(new ZLevelTileIndices(tile.X, tile.Y, 2)));

            var roof = SpawnAnchored("ZLevelRoofMarker", coordinates, 2);
            var blocked = sky.GetExposure(
                (testMap.Grid, grid),
                new ZLevelTileIndices(tile.X, tile.Y, 0));
            Assert.Multiple(() =>
            {
                Assert.That(blocked.Termination, Is.EqualTo(ZLevelSkyExposureTermination.ClosedBoundary));
                Assert.That(blocked.BlockingLowerZ, Is.EqualTo(2));
            });

            var grateItem = SEntMan.SpawnEntity("FloorTileItemZLevelGrate", coordinates);
            var shaftItem = SEntMan.SpawnEntity("FloorTileItemZLevelShaft", coordinates);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<FloorTileComponent>(grateItem).Outputs,
                    Does.Contain(GrateTile));
                Assert.That(SEntMan.GetComponent<FloorTileComponent>(shaftItem).Outputs,
                    Does.Contain(ShaftTile));
            });

            SEntMan.DeleteEntity(catwalk);
            Assert.That(boundaries.CanBodyPass(testMap.Grid, grid, tile, 2, 1), Is.True);
            Assert.That(zLevels.TryGetSupportTile(actor, out support), Is.True);
            Assert.That(support.GridIndices, Is.EqualTo(new ZLevelTileIndices(tile.X, tile.Y, 1)),
                "Removing the catwalk must expose the shaft and leave the grate below as support.");

            SEntMan.DeleteEntity(roof);
            Assert.That(sky.GetExposure(
                (testMap.Grid, grid),
                new ZLevelTileIndices(tile.X, tile.Y, 0)).IsExposed, Is.True);

            EntityUid SpawnAnchored(string prototype, EntityCoordinates at, int z)
            {
                var uid = SEntMan.SpawnEntity(prototype, at);
                var transform = SEntMan.System<SharedTransformSystem>();
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
    public async Task VerticalContentRoundTripsTwiceWithEquivalentTopology()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var loader = entMan.System<MapLoaderSystem>();
        var map = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var zLevels = entMan.System<SharedZLevelSystem>();
        var firstPath = new ResPath("/Maps/Test/ZLevelVerticalContent-roundtrip-1.yml");
        var secondPath = new ResPath("/Maps/Test/ZLevelVerticalContent-roundtrip-2.yml");
        var grate = prototypes.Index(GrateTile);
        var shaft = prototypes.Index(ShaftTile);
        var steel = prototypes.Index(SteelTile);
        var tile = new Vector2i(2, 4);

        MapId sourceMapId = default;
        await server.WaitAssertion(() =>
        {
            var mapUid = map.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(mapUid, 0, 2, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            var grid = mapManager.CreateGridEntity(sourceMapId);
            grid.Comp.CanSplit = false;
            map.SetTile(grid.Owner, grid.Comp, tile, new Tile(steel.TileId));
            map.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(grate.TileId));
            map.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(tile.X, tile.Y, 2), new Tile(shaft.TileId));

            SpawnAnchored(grid, "Catwalk", 2);
            SpawnAnchored(grid, "ZLevelRoofMarker", 2);

            AssertState(mapUid, grid);
            Assert.That(loader.TrySaveMap(sourceMapId, firstPath), Is.True);
            map.DeleteMap(sourceMapId);
        });

        await server.WaitIdleAsync();

        MapId firstLoadedMapId = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(firstPath, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));
            firstLoadedMapId = loadedMap!.Value.Comp.MapId;
            AssertState(loadedMap.Value.Owner, loadedGrids!.Single());
            Assert.That(loader.TrySaveMap(firstLoadedMapId, secondPath), Is.True);
            map.DeleteMap(firstLoadedMapId);
        });

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(secondPath, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));
            AssertState(loadedMap!.Value.Owner, loadedGrids!.Single());
        });

        void SpawnAnchored(Entity<MapGridComponent> grid, string prototype, int z)
        {
            var coordinates = new EntityCoordinates(
                grid.Owner,
                (Vector2) tile + new Vector2(0.5f, 0.5f));
            var uid = entMan.SpawnEntity(prototype, coordinates);
            var xform = entMan.GetComponent<TransformComponent>(uid);
            transform.Unanchor(uid, xform);
            Assert.That(zLevels.SetZLevelPosition(uid, z), Is.True);
            transform.AnchorEntity(uid, xform);
        }

        void AssertState(EntityUid mapUid, Entity<MapGridComponent> grid)
        {
            var boundaries = entMan.System<SharedZLevelBoundarySystem>();
            var sky = entMan.System<SharedZLevelSkyExposureSystem>();
            var entities = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
                .Where(entry => ((TransformComponent) entry.Component).GridUid == grid.Owner)
                .Select(entry => entry.Uid)
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID is
                    "Catwalk" or "ZLevelRoofMarker")
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(format.TryValidate(mapUid, out var error), Is.True, error);
                Assert.That(map.GetZLevelTileRef(grid.Owner, grid.Comp,
                    new ZLevelTileIndices(tile.X, tile.Y, 1)).Tile.TypeId, Is.EqualTo(grate.TileId));
                Assert.That(map.GetZLevelTileRef(grid.Owner, grid.Comp,
                    new ZLevelTileIndices(tile.X, tile.Y, 2)).Tile.TypeId, Is.EqualTo(shaft.TileId));
                Assert.That(entities, Has.Length.EqualTo(2));
                Assert.That(entities.All(uid => entMan.GetComponent<TransformComponent>(uid).Anchored), Is.True);
                Assert.That(entities.All(uid => transform.GetZLevel((
                    uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid))) == 2), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, tile, 0, 1,
                    ZLevelBoundaryChannels.Atmosphere), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, tile, 0, 1,
                    ZLevelBoundaryChannels.Body), Is.False);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, tile, 1, 2,
                    ZLevelBoundaryChannels.Body), Is.False,
                    "The persisted catwalk must continue to support the shaft.");
                Assert.That(sky.GetExposure(
                    grid,
                    new ZLevelTileIndices(tile.X, tile.Y, 0)).BlockingLowerZ, Is.EqualTo(2));
            });
        }
    }
}
