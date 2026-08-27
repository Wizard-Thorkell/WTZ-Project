// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Decals;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelDecalTest : GameTest
{
    private const int FrameOrigin = 5;
    private const string DecalPrototype = "burnt1";

    [Test]
    public async Task QueriesValidationAndTileRemovalAreLayerScoped()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var decals = SEntMan.System<DecalSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            var steel = (ContentTileDefinition) definitions["FloorSteel"];
            var coordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));
            var upper = new ZLevelTileIndices(0, 0, 1);

            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);
            map.SetTile(testMap.Grid, testMap.Grid.Comp, Vector2i.Zero, new Tile(steel.TileId));
            map.SetTile(testMap.Grid, testMap.Grid.Comp, new Vector2i(1, 0), new Tile(steel.TileId));
            map.SetZLevelTile(testMap.Grid, testMap.Grid.Comp, upper, new Tile(steel.TileId));

            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out var lowerId, zLevel: 0), Is.True);
            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out var upperId, zLevel: 1), Is.True);
            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out _, zLevel: 2), Is.False,
                "A decal must not be placed on a layer without a floor tile.");

            Assert.Multiple(() =>
            {
                Assert.That(decals.GetDecalsInRange(testMap.Grid, coordinates.Position, zLevel: 0)
                    .Select(entry => entry.Index), Is.EquivalentTo(new[] { lowerId }));
                Assert.That(decals.GetDecalsInRange(testMap.Grid, coordinates.Position, zLevel: 1)
                    .Select(entry => entry.Index), Is.EquivalentTo(new[] { upperId }));
                Assert.That(decals.GetDecalsInRange(testMap.Grid, coordinates.Position, zLevel: 2), Is.Empty);
            });

            map.SetZLevelTile(testMap.Grid, testMap.Grid.Comp, upper, Tile.Empty);

            Assert.Multiple(() =>
            {
                Assert.That(decals.GetDecalsInRange(testMap.Grid, coordinates.Position, zLevel: 0)
                    .Select(entry => entry.Index), Is.EquivalentTo(new[] { lowerId }),
                    "Removing the upper floor must preserve the overlapping lower decal.");
                Assert.That(decals.GetDecalsInRange(testMap.Grid, coordinates.Position, zLevel: 1), Is.Empty,
                    "Removing a floor must remove decals only from that floor.");
            });
        });
    }

    [Test]
    public async Task ComponentStateReplicatesLayeredDecals()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var decals = SEntMan.System<DecalSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            var steel = (ContentTileDefinition) definitions["FloorSteel"];
            var coordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));

            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetTile(testMap.Grid, testMap.Grid.Comp, Vector2i.Zero, new Tile(steel.TileId));
            map.SetZLevelTile(testMap.Grid, testMap.Grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(steel.TileId));

            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out _, zLevel: 0), Is.True);
            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out _, zLevel: 1), Is.True);
        });

        await RunTicksSync(5);

        await Client.WaitAssertion(() =>
        {
            var component = CEntMan.GetComponent<DecalGridComponent>(testMap.CGridUid);
            var layers = component.ChunkCollection.ChunkCollection.Values
                .SelectMany(chunk => chunk.Decals.Values)
                .Select(decal => decal.ZLevel)
                .Order()
                .ToArray();

            Assert.That(layers, Is.EqualTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public async Task LayeredDecalsRoundTripThroughMapSerialization()
    {
        var mapManager = Server.ResolveDependency<IMapManager>();
        var map = SEntMan.System<SharedMapSystem>();
        var loader = SEntMan.System<MapLoaderSystem>();
        var format = SEntMan.System<SharedZLevelMapSystem>();
        var decals = SEntMan.System<DecalSystem>();
        var path = new ResPath("/Maps/Test/ZLevelDecal-roundtrip.yml");

        await Server.WaitAssertion(() =>
        {
            var mapUid = map.CreateMap(out var mapId, runMapInit: false);
            format.Configure(mapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            var grid = mapManager.CreateGridEntity(mapId);
            SEntMan.EnsureComponent<DecalGridComponent>(grid);
            map.SetTile(grid, grid.Comp, Vector2i.Zero, new Tile(1));
            map.SetZLevelTile(grid, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));
            var coordinates = new EntityCoordinates(grid, new Vector2(0.5f, 0.5f));

            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out _, zLevel: 0), Is.True);
            Assert.That(decals.TryAddDecal(DecalPrototype, coordinates, out _, zLevel: 1), Is.True);
            Assert.That(loader.TrySaveMap(mapId, path), Is.True);
            map.DeleteMap(mapId);
        });

        await Server.WaitIdleAsync();

        await Server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(path, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));

            var loadedGrid = loadedGrids!.Single();
            var layers = SEntMan.GetComponent<DecalGridComponent>(loadedGrid).ChunkCollection.ChunkCollection.Values
                .SelectMany(chunk => chunk.Decals.Values)
                .Select(decal => decal.ZLevel)
                .Order()
                .ToArray();

            Assert.That(layers, Is.EqualTo(new[] { 0, 1 }));
        });
    }

    [Test]
    public async Task VersionTwoMapDecalsDefaultToBaseLayer()
    {
        await Server.WaitAssertion(() =>
        {
            var loader = SEntMan.System<MapLoaderSystem>();
            Assert.That(loader.TryLoadMap(
                new ResPath("/Maps/Dungeon/haunted.yml"),
                out var loadedMap,
                out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Is.Not.Null.And.Not.Empty);

            var loadedDecals = loadedGrids!
                .Where(grid => SEntMan.HasComponent<DecalGridComponent>(grid))
                .SelectMany(grid => SEntMan.GetComponent<DecalGridComponent>(grid)
                    .ChunkCollection.ChunkCollection.Values)
                .SelectMany(chunk => chunk.Decals.Values)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(loadedDecals, Is.Not.Empty,
                    "The version-two compatibility fixture must contain real decals.");
                Assert.That(loadedDecals.Select(decal => decal.ZLevel), Is.All.Zero,
                    "Version-two decal data has no layer field and must remain on Z=0.");
            });
        });
    }
}
