// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.Server.Decals;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.IntegrationTests.Fixtures;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Content.Shared.Decals;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using Robust.Server.GameObjects;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelMapFormatTest : GameTest
{
    [Test]
    public async Task ChangingDefaultBoundaryModeInvalidatesBoundaryCache()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var tile = Vector2i.Zero;

            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(1));
            Assert.That(boundaries.IsOpen(
                testMap.Grid,
                grid,
                tile,
                0,
                1,
                ZLevelBoundaryChannels.Visibility), Is.False);
            Assert.That(boundaries.CachedBoundaryCount, Is.GreaterThan(0));

            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            Assert.That(boundaries.IsOpen(
                testMap.Grid,
                grid,
                tile,
                0,
                1,
                ZLevelBoundaryChannels.Visibility), Is.True);
        });
    }

    [Test]
    public async Task FloorOperationsNeverCopyOrDeleteActors()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var mapping = entMan.System<ZLevelMappingSystem>();
        var zLevel = entMan.System<SharedZLevelSystem>();
        EntityUid actor = default;

        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out var mapId, runMapInit: false);
            format.Configure(mapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            var map = entMan.GetComponent<MapComponent>(mapUid);
            var grid = mapManager.CreateGridEntity(mapId);
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));

            actor = entMan.SpawnEntity(null,
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<ActorComponent>(actor);

            Assert.That(mapping.CopyLevel(mapUid,
                map,
                entMan.GetComponent<ZLevelMapComponent>(mapUid),
                grid,
                0,
                1), Is.Zero);

            zLevel.SetZLevelPosition(actor, 1);
            Assert.That(mapping.DeleteLevel(mapUid,
                entMan.GetComponent<ZLevelMapComponent>(mapUid),
                grid,
                1), Is.Zero);
            Assert.That(format.TryValidate(mapUid, out var error), Is.True, error);
        });

        await server.WaitRunTicks(1);
        await server.WaitAssertion(() => Assert.That(entMan.EntityExists(actor), Is.True));
    }

    [Test]
    public async Task OfficialThreeFloorTestMapLoadsWithRoofAndShafts()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var loader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var boundaries = entMan.System<SharedZLevelBoundarySystem>();
        var transform = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(
                new ResPath("/Maps/Test/ZLevel/zlevel-mapping-station.yml"),
                out var loadedMap,
                out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));

            var map = loadedMap!.Value;
            var grid = loadedGrids!.Single();
            var config = entMan.GetComponent<ZLevelMapComponent>(map.Owner);
            var entities = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
                .Where(entry => ((TransformComponent) entry.Component).GridUid == grid.Owner)
                .Select(entry => entry.Uid)
                .ToArray();
            var lightFixtures = entities
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "AlwaysPoweredLightExterior" &&
                    entMan.HasComponent<PointLightComponent>(uid))
                .ToArray();
            var shadowBlockers = entities
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityName.EndsWith(
                    "lighting shadow blocker",
                    StringComparison.Ordinal))
                .OrderBy(uid => transform.GetZLevel((
                    uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid))))
                .ToArray();
            var apertureMarkers = entities
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID ==
                    "ZLevelFloorOpeningMarker")
                .ToArray();
            var flightJetpacks = entities
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "JetpackBlueFilled")
                .ToArray();
            var flightMarkers = entities
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID ==
                    "ZLevelFlightNavigationMarker")
                .ToArray();
            var flightEdges = entMan.System<ZLevelTraversalGraphSystem>().CreateSnapshot(map.Comp.MapId).FlightEdges;

            Assert.Multiple(() =>
            {
                Assert.That(format.TryValidate(map.Owner, out var error), Is.True, error);
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.EqualTo(3));
                Assert.That(config.DefaultLevel, Is.Zero);
                Assert.That(config.DefaultBoundaryMode, Is.EqualTo(ZLevelDefaultBoundaryMode.TileAboveCloses));
                Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                    Is.EquivalentTo(new[] { 0, 1, 2, 3 }));
                Assert.That(entities.Count(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "WallSolid"), Is.EqualTo(75));
                Assert.That(entities.Count(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID is
                    "ZLevelStairsUp" or "ZLevelStairsDown"), Is.EqualTo(4));
                Assert.That(apertureMarkers, Has.Length.EqualTo(4));
                Assert.That(flightJetpacks, Has.Length.EqualTo(1));
                Assert.That(flightMarkers, Has.Length.EqualTo(1));
                Assert.That(flightEdges, Has.Length.EqualTo(2));
                Assert.That(flightEdges.Select(edge => edge.Source.WorldZ), Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(transform.GetZLevel((
                    flightJetpacks[0],
                    entMan.GetComponent<TransformComponent>(flightJetpacks[0]),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(flightJetpacks[0]))), Is.Zero);
                Assert.That(lightFixtures, Has.Length.EqualTo(3));
                Assert.That(lightFixtures.Select(uid => transform.GetZLevel((
                        uid,
                        entMan.GetComponent<TransformComponent>(uid),
                        entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)))),
                    Is.EquivalentTo(new[] { 0, 1, 2 }));
                Assert.That(lightFixtures.Select(uid => entMan.GetComponent<PointLightComponent>(uid).Color),
                    Is.EquivalentTo(new[]
                    {
                        Color.FromHex("#FF4040FF"),
                        Color.FromHex("#40FF70FF"),
                        Color.FromHex("#4080FFFF"),
                    }));
                Assert.That(lightFixtures.Select(uid => entMan.GetComponent<PointLightComponent>(uid).Radius),
                    Is.All.EqualTo(5f));
                Assert.That(lightFixtures.Select(uid => entMan.GetComponent<PointLightComponent>(uid).Softness),
                    Is.All.EqualTo(0.75f));
                Assert.That(lightFixtures.Select(uid => entMan.GetComponent<PointLightComponent>(uid).CastShadows),
                    Is.All.True);
                Assert.That(shadowBlockers, Has.Length.EqualTo(3));
                Assert.That(shadowBlockers.Select(uid => transform.GetZLevel((
                        uid,
                        entMan.GetComponent<TransformComponent>(uid),
                        entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)))),
                    Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(shadowBlockers.Select(uid => entMan.GetComponent<TransformComponent>(uid).LocalPosition),
                    Is.All.EqualTo(new Vector2(2.5f, 2.5f)));
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(1, 1), 0, 1,
                    ZLevelBoundaryChannels.Atmosphere), Is.False);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(1, 5), 0, 1,
                    ZLevelBoundaryChannels.Atmosphere), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(1, 4), 0, 1,
                    ZLevelBoundaryChannels.Visibility), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(5, 4), 0, 1,
                    ZLevelBoundaryChannels.Visibility), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(1, 4), 1, 2,
                    ZLevelBoundaryChannels.Visibility), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(5, 4), 1, 2,
                    ZLevelBoundaryChannels.Visibility), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(3, 4), 0, 1,
                    ZLevelBoundaryChannels.Visibility), Is.False);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, new Vector2i(1, 1), 2, 3,
                    ZLevelBoundaryChannels.Atmosphere), Is.False);
            });
        });
    }

    [Test]
    public async Task MappingCopyDuplicatesTilesAndAnchoredEntitiesAndRoundTrips()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var loader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var mapping = entMan.System<ZLevelMappingSystem>();
        var decals = entMan.System<DecalSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var path = new ResPath("/Maps/Test/ZLevelMapFormat-copy-roundtrip.yml");

        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out var mapId, runMapInit: false);
            format.Configure(mapUid, 0, 0, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            var map = entMan.GetComponent<MapComponent>(mapUid);
            var grid = mapManager.CreateGridEntity(mapId);
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            Assert.That(decals.TryAddDecal(
                "burnt1",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)),
                out _,
                zLevel: 0), Is.True);

            var marker = entMan.SpawnEntity("ZLevelSealedBoundaryMarker",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            transform.Unanchor(marker);
            transform.AnchorEntity(marker, entMan.GetComponent<TransformComponent>(marker));

            Assert.That(mapping.CopyLevel(mapUid,
                map,
                entMan.GetComponent<ZLevelMapComponent>(mapUid),
                grid,
                0,
                1), Is.EqualTo(1));

            AssertCopiedState(grid);
            Assert.That(loader.TrySaveMap(mapId, path), Is.True);
            mapSystem.DeleteMap(mapId);
        });

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(path, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));
            AssertCopiedState(loadedGrids!.Single());
        });

        void AssertCopiedState(Entity<MapGridComponent> grid)
        {
            var markers = entMan.GetAllComponents(typeof(ZLevelBoundaryComponent), includePaused: true)
                .Select(component => component.Uid)
                .Where(uid => entMan.GetComponent<TransformComponent>(uid).GridUid == grid.Owner)
                .OrderBy(uid => transform.GetZLevel((uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid))))
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                    Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(markers, Has.Length.EqualTo(2));
                Assert.That(markers.Select(uid => transform.GetZLevel((uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)))),
                    Is.EqualTo(new[] { 0, 1 }));
                Assert.That(markers.All(uid => entMan.GetComponent<TransformComponent>(uid).Anchored), Is.True);
                Assert.That(markers.Select(uid => entMan.GetComponent<TransformComponent>(uid).LocalPosition),
                    Is.All.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(decals.GetDecalsInRange(grid.Owner, new Vector2(0.5f, 0.5f), zLevel: 0),
                    Has.Count.EqualTo(1));
                Assert.That(decals.GetDecalsInRange(grid.Owner, new Vector2(0.5f, 0.5f), zLevel: 1),
                    Has.Count.EqualTo(1));
            });
        }
    }

    [Test]
    public async Task VersionedMapRoundTripsTwiceWithLayersEntitiesAndBoundaries()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var loader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var boundaries = entMan.System<SharedZLevelBoundarySystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var firstPath = new ResPath("/Maps/Test/ZLevelMapFormat-roundtrip-1.yml");
        var secondPath = new ResPath("/Maps/Test/ZLevelMapFormat-roundtrip-2.yml");

        MapId sourceMapId = default;
        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(mapUid, -1, 2, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);

            var grid = mapManager.CreateGridEntity(sourceMapId);
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, -1), new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 2), new Tile(1));

            var marker = entMan.SpawnEntity("ZLevelSealedBoundaryMarker",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            transform.Unanchor(marker);
            entMan.EnsureComponent<ZLevelPositionComponent>(marker).ZLevel = 1;
            transform.AnchorEntity(marker, entMan.GetComponent<TransformComponent>(marker));
            SpawnInfrastructure("CableApcExtension", -1);
            SpawnInfrastructure("GasPipeStraight", 1);
            SpawnInfrastructure("APCBasic", 2);

            Assert.That(format.TryValidate(mapUid, out var error), Is.True, error);
            Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, Vector2i.Zero, 0, 1,
                ZLevelBoundaryChannels.Atmosphere), Is.True);
            Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, Vector2i.Zero, 1, 2,
                ZLevelBoundaryChannels.Atmosphere), Is.False);
            Assert.That(loader.TrySaveMap(sourceMapId, firstPath), Is.True);
            mapSystem.DeleteMap(sourceMapId);

            void SpawnInfrastructure(string prototype, int z)
            {
                var uid = entMan.SpawnEntity(prototype,
                    new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
                var xform = entMan.GetComponent<TransformComponent>(uid);
                transform.Unanchor(uid, xform);
                entMan.EnsureComponent<ZLevelPositionComponent>(uid).ZLevel = z;
                transform.AnchorEntity(uid, xform);
            }
        });

        await server.WaitIdleAsync();

        MapId firstLoadedMapId = default;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(firstPath, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));
            firstLoadedMapId = loadedMap!.Value.Comp.MapId;

            AssertMapState(loadedMap.Value.Owner, loadedGrids!.Single());
            Assert.That(loader.TrySaveMap(firstLoadedMapId, secondPath), Is.True);
            mapSystem.DeleteMap(firstLoadedMapId);
        });

        await server.WaitIdleAsync();

        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(secondPath, out var loadedMap, out var loadedGrids), Is.True);
            Assert.That(loadedMap, Is.Not.Null);
            Assert.That(loadedGrids, Has.Count.EqualTo(1));
            AssertMapState(loadedMap!.Value.Owner, loadedGrids!.Single());
        });

        void AssertMapState(EntityUid mapUid, Entity<MapGridComponent> grid)
        {
            Assert.That(format.TryValidate(mapUid, out var error), Is.True, error);
            var config = entMan.GetComponent<ZLevelMapComponent>(mapUid);
            var markers = entMan.GetAllComponents(typeof(ZLevelBoundaryComponent), includePaused: true)
                .Select(component => component.Uid)
                .Where(uid => entMan.GetComponent<TransformComponent>(uid).GridUid == grid.Owner)
                .ToArray();
            var infrastructure = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
                .Where(entry => ((TransformComponent) entry.Component).GridUid == grid.Owner)
                .Select(entry => entry.Uid)
                .Where(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID is
                    "CableApcExtension" or "GasPipeStraight" or "APCBasic")
                .ToDictionary(
                    uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID,
                    uid => uid);

            Assert.Multiple(() =>
            {
                Assert.That(config.FormatVersion, Is.EqualTo(ZLevelMapComponent.CurrentFormatVersion));
                Assert.That(config.MinimumLevel, Is.EqualTo(-1));
                Assert.That(config.MaximumLevel, Is.EqualTo(2));
                Assert.That(config.DefaultLevel, Is.Zero);
                Assert.That(config.DefaultBoundaryMode, Is.EqualTo(ZLevelDefaultBoundaryMode.ExplicitOnly));
                Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                    Is.EquivalentTo(new[] { -1, 0, 1, 2 }));
                Assert.That(markers, Has.Length.EqualTo(1));
                Assert.That(infrastructure.Keys,
                    Is.EquivalentTo(new[] { "CableApcExtension", "GasPipeStraight", "APCBasic" }));
                Assert.That(infrastructure.All(pair => entMan.GetComponent<TransformComponent>(pair.Value).Anchored),
                    Is.True);
                Assert.That(GetEntityZ(infrastructure["CableApcExtension"]), Is.EqualTo(-1));
                Assert.That(GetEntityZ(infrastructure["GasPipeStraight"]), Is.EqualTo(1));
                Assert.That(GetEntityZ(infrastructure["APCBasic"]), Is.EqualTo(2));
                Assert.That(transform.GetZLevel((markers[0], entMan.GetComponent<TransformComponent>(markers[0]),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(markers[0]))), Is.EqualTo(1));
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, Vector2i.Zero, 0, 1,
                    ZLevelBoundaryChannels.Atmosphere), Is.True);
                Assert.That(boundaries.IsOpen(grid.Owner, grid.Comp, Vector2i.Zero, 1, 2,
                    ZLevelBoundaryChannels.Atmosphere), Is.False);
            });

            int GetEntityZ(EntityUid uid)
            {
                return transform.GetZLevel((uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)));
            }
        }
    }

    [Test]
    public async Task SaveRejectsUnmarkedOrOutOfRangeZLevels()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var loader = entMan.System<MapLoaderSystem>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();

        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out var mapId, runMapInit: false);
            var grid = mapManager.CreateGridEntity(mapId);
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));

            Assert.That(format.TryValidate(mapUid, out var missingError), Is.False);
            Assert.That(missingError, Does.Contain("no ZLevelMap format component"));
            Assert.Throws<InvalidOperationException>(() => loader.SerializeEntitiesRecursive([mapUid]));

            format.Configure(mapUid, 0, 0, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            Assert.That(format.TryValidate(mapUid, out var rangeError), Is.False);
            Assert.That(rangeError, Does.Contain("outside the declared range"));
            Assert.Throws<InvalidOperationException>(() => loader.SerializeEntitiesRecursive([mapUid]));

            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), Tile.Empty);
            var marker = entMan.SpawnEntity(null, new EntityCoordinates(mapUid, Vector2.Zero));
            entMan.EnsureComponent<ZLevelPositionComponent>(marker).ZLevel = 1;
            Assert.That(format.TryValidate(mapUid, out var entityError), Is.False);
            Assert.That(entityError, Does.Contain("Entity").And.Contain("outside the declared range"));
            Assert.Throws<InvalidOperationException>(() => loader.SerializeEntitiesRecursive([mapUid]));
        });
    }
}
