// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Actions;
using Content.Server.Atmos.EntitySystems;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Mapping;
using Content.Server.Mind;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Mapping;
using Content.Shared.Maps;
using Content.Shared.Mind.Components;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Markdown.Mapping;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelInitializedMapSnapshotTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";

    private static readonly string[] RoundTripPrototypes =
    [
        "APCBasic",
        "CableApcExtension",
        "GasPipeStraight",
        "RemoteSignaller",
        "ZLevelElevatorCabin",
        "ZLevelElevatorStop",
        "ZLevelElevatorStop",
        "ZLevelFloorOpeningMarker",
        "ZLevelSealedBoundaryMarker",
    ];

    [Test]
    public async Task InitializedSnapshotExcludesTransientRootsAndRoundTripsZLevelState()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var zLevel = entMan.System<SharedZLevelSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var snapshots = entMan.System<MappingSnapshotSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var minds = entMan.System<MindSystem>();
        var followers = entMan.System<FollowerSystem>();
        var deviceLists = entMan.System<DeviceListSystem>();

        EntityUid actor = default;
        EntityUid actorChild = default;
        EntityUid mindedBody = default;
        EntityUid mind = default;
        EntityUid explicitTransient = default;
        EntityUid runtimeFollower = default;
        EntityUid infrastructure = default;
        EntityUid persistentDevice = default;
        EntityUid crossMapDevice = default;
        MappingDataNode snapshot = default!;
        MappingSnapshotReport report = default;
        var initialMapCount = 0;

        await server.WaitAssertion(() =>
        {
            initialMapCount = entMan.Count<MapComponent>();
            var mapUid = mapSystem.CreateMap(out var mapId, runMapInit: false);
            format.Configure(mapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            var grid = mapManager.CreateGridEntity(mapId);
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));

            infrastructure = entMan.SpawnEntity("GasPipeStraight",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<DeviceListComponent>(infrastructure);
            transform.Unanchor(infrastructure);
            Assert.That(zLevel.SetZLevelPosition(infrastructure, 1), Is.True);
            transform.AnchorEntity(infrastructure, entMan.GetComponent<TransformComponent>(infrastructure));

            persistentDevice = entMan.SpawnEntity("RemoteSignaller",
                new EntityCoordinates(grid.Owner, new Vector2(1.5f, 0.5f)));

            actor = entMan.SpawnEntity("Crowbar",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<ActorComponent>(actor);
            entMan.EnsureComponent<DeviceNetworkComponent>(actor);
            Assert.That(zLevel.SetZLevelPosition(actor, 1), Is.True);
            actorChild = entMan.SpawnEntity("Wrench", new EntityCoordinates(actor, Vector2.Zero));
            entMan.EnsureComponent<ZLevelPositionComponent>(actorChild).ZLevel = 7;

            mindedBody = entMan.SpawnEntity("Screwdriver",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            var mindEntity = minds.CreateMind(null);
            mind = mindEntity.Owner;
            minds.TransferTo(mind, mindedBody, createGhost: false, mind: mindEntity.Comp);

            explicitTransient = entMan.SpawnEntity("Wirecutter",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<MappingSnapshotTransientComponent>(explicitTransient);
            entMan.EnsureComponent<ZLevelPositionComponent>(explicitTransient).ZLevel = 8;

            var crossMapUid = mapSystem.CreateMap(out var crossMapId, runMapInit: false);
            crossMapDevice = entMan.SpawnEntity("RemoteSignallerAdvanced",
                new EntityCoordinates(crossMapUid, new Vector2(2f, 2f)));

            mapSystem.InitializeMap(mapId);
            mapSystem.InitializeMap(crossMapId);
            runtimeFollower = entMan.SpawnEntity("MobMouse",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            followers.StartFollowingEntity(runtimeFollower, infrastructure);
            Assert.That(deviceLists.UpdateDeviceList(
                    infrastructure,
                    [persistentDevice, actor, crossMapDevice]),
                Is.EqualTo(DeviceListUpdateResult.UpdateOk));
            Assert.That(entMan.GetComponent<MetaDataComponent>(mapUid).EntityLifeStage,
                Is.EqualTo(EntityLifeStage.MapInitialized));
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ActorComponent>(actor), Is.True);
                Assert.That(entMan.HasComponent<MindContainerComponent>(mindedBody), Is.True);
                Assert.That(entMan.GetComponent<MindContainerComponent>(mindedBody).HasMind, Is.True);
                Assert.That(entMan.HasComponent<MappingSnapshotTransientComponent>(explicitTransient), Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(actor).EntityPrototype?.MapSavable, Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(mindedBody).EntityPrototype?.MapSavable, Is.True);
                Assert.That(entMan.GetComponent<MetaDataComponent>(explicitTransient).EntityPrototype?.MapSavable, Is.True);
                Assert.That(entMan.GetComponent<FollowerComponent>(runtimeFollower).Following,
                    Is.EqualTo(infrastructure));
                Assert.That(entMan.GetComponent<FollowedComponent>(infrastructure).Following,
                    Does.Contain(runtimeFollower));
                Assert.That(entMan.GetComponent<DeviceListComponent>(infrastructure).Devices,
                    Is.EquivalentTo(new[] { persistentDevice, actor, crossMapDevice }));
            });
            Assert.That(snapshots.TryCreateMapSnapshot(mapUid, out snapshot, out report, out var error),
                Is.True, error);

            Assert.Multiple(() =>
            {
                Assert.That(report.PlayerRoots, Is.EqualTo(2));
                Assert.That(report.MindRoots, Is.Zero);
                Assert.That(report.ExplicitTransientRoots, Is.EqualTo(1));
                Assert.That(report.ExcludedRoots, Is.EqualTo(3));
                Assert.That(report.TransientComponents, Is.EqualTo(1));
                Assert.That(report.NormalizedReferences, Is.EqualTo(2));
                Assert.That(report.ValidatedEntities, Is.GreaterThan(0));
                Assert.That(entMan.Count<MapComponent>(), Is.EqualTo(initialMapCount + 2),
                    "Successful normalization must delete both disposable map loads.");
                Assert.That(entMan.EntityExists(actor), Is.True);
                Assert.That(entMan.EntityExists(actorChild), Is.True);
                Assert.That(entMan.EntityExists(mindedBody), Is.True);
                Assert.That(entMan.EntityExists(mind), Is.True);
                Assert.That(entMan.EntityExists(explicitTransient), Is.True);
                Assert.That(entMan.GetComponent<FollowerComponent>(runtimeFollower).Following,
                    Is.EqualTo(infrastructure), "A mapping snapshot must not mutate runtime follow state.");
                Assert.That(entMan.GetComponent<FollowedComponent>(infrastructure).Following,
                    Does.Contain(runtimeFollower));
                Assert.That(entMan.GetComponent<DeviceListComponent>(infrastructure).Devices,
                    Is.EquivalentTo(new[] { persistentDevice, actor, crossMapDevice }),
                    "Detached normalization must not rewrite the live device list.");
                Assert.That(entMan.GetComponent<DeviceNetworkComponent>(persistentDevice).DeviceLists,
                    Does.Contain(infrastructure));
                Assert.That(entMan.GetComponent<DeviceNetworkComponent>(crossMapDevice).DeviceLists,
                    Does.Contain(infrastructure));
            });

            var infrastructureZ = entMan.GetComponent<ZLevelPositionComponent>(infrastructure);
            infrastructureZ.ZLevel = 2;
            Assert.That(snapshots.TryCreateMapSnapshot(mapUid, out _, out _, out var persistentError),
                Is.False);
            Assert.That(persistentError, Does.Contain("outside the declared range"));
            infrastructureZ.ZLevel = 1;

            var unhandledReference = entMan.EnsureComponent<ActionOnInteractComponent>(persistentDevice);
            unhandledReference.Actions = [];
            unhandledReference.ActionEntities = [actor];
            Assert.That(snapshots.TryCreateMapSnapshot(mapUid, out _, out _, out var referenceError),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(referenceError, Does.Contain("invalid entity reference"));
                Assert.That(referenceError, Does.Contain("ActionOnInteract"));
                Assert.That(unhandledReference.ActionEntities, Is.EquivalentTo(new[] { actor }),
                    "Rejected normalization must not rewrite the live component.");
                Assert.That(entMan.Count<MapComponent>(), Is.EqualTo(initialMapCount + 2),
                    "Rejected normalization must delete both disposable map loads.");
            });
        });

        LoadResult loaded = default!;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadGeneric(snapshot, "initialized Z-level mapping snapshot", out loaded), Is.True);
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(loaded, Is.Not.Null);
            var loadedMap = loaded.Maps.Single();
            var loadedGrid = loaded.Grids.Single();
            var loadedPrototypes = loaded.Entities
                .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
                .Where(id => id != null)
                .ToArray();
            var loadedInfrastructure = loaded.Entities.Single(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "GasPipeStraight");
            var loadedPersistentDevice = loaded.Entities.Single(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "RemoteSignaller");
            var config = entMan.GetComponent<ZLevelMapComponent>(loadedMap);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MetaDataComponent>(loadedMap).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(loaded.NullspaceEntities, Is.Empty);
                Assert.That(loaded.Orphans, Is.Empty);
                Assert.That(loaded.InvalidEntityReferences, Is.Empty);
                Assert.That(loadedPrototypes, Does.Not.Contain("Crowbar"));
                Assert.That(loadedPrototypes, Does.Not.Contain("Wrench"));
                Assert.That(loadedPrototypes, Does.Not.Contain("Screwdriver"));
                Assert.That(loadedPrototypes, Does.Not.Contain("Wirecutter"));
                Assert.That(loadedPrototypes, Does.Not.Contain("MobMouse"));
                Assert.That(loadedPrototypes, Does.Not.Contain("RemoteSignallerAdvanced"));
                Assert.That(loadedPrototypes, Does.Contain("GasPipeStraight"));
                Assert.That(loadedPrototypes, Does.Contain("RemoteSignaller"));
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.EqualTo(1));
                Assert.That(config.DefaultLevel, Is.Zero);
                Assert.That(config.DefaultBoundaryMode, Is.EqualTo(ZLevelDefaultBoundaryMode.ExplicitOnly));
                Assert.That(mapSystem.GetExistingZLevelLayers(loadedGrid.Owner, loadedGrid.Comp),
                    Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(transform.GetZLevel((
                    loadedInfrastructure,
                    entMan.GetComponent<TransformComponent>(loadedInfrastructure),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(loadedInfrastructure))), Is.EqualTo(1));
                Assert.That(entMan.GetComponent<TransformComponent>(loadedInfrastructure).Anchored, Is.True);
                Assert.That(entMan.GetComponent<DeviceListComponent>(loadedInfrastructure).Devices,
                    Is.EquivalentTo(new[] { loadedPersistentDevice }));
                Assert.That(entMan.GetComponent<DeviceNetworkComponent>(loadedPersistentDevice).DeviceLists,
                    Does.Contain(loadedInfrastructure));
                Assert.That(entMan.EntityExists(infrastructure), Is.True,
                    "Creating and loading the snapshot must not replace the live source map.");
            });
        });

        await server.WaitPost(() => loader.Delete(loaded));
    }

    [Test]
    public async Task InitializedAuthoredMapSnapshotIsStructurallyStableAcrossTwoCycles()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var zLevel = entMan.System<SharedZLevelSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var snapshots = entMan.System<MappingSnapshotSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var deviceLists = entMan.System<DeviceListSystem>();
        var atmosphere = entMan.System<AtmosphereSystem>();
        var prototypes = server.ResolveDependency<IPrototypeManager>();
        var shaftTile = prototypes.Index(ShaftTile);

        MapId sourceMapId = default;
        MappingDataNode firstSnapshot = default!;
        MappingDataNode secondSnapshot = default!;
        AuthoredMapState sourceState = default!;
        LoadResult firstLoad = default!;
        LoadResult secondLoad = default!;
        EntityUid sourceCabin = default;

        await server.WaitAssertion(() =>
        {
            var mapUid = mapSystem.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(mapUid, -1, 2, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);

            var stationGrid = mapManager.CreateGridEntity(sourceMapId);
            var movingGrid = mapManager.CreateGridEntity(sourceMapId);
            stationGrid.Comp.CanSplit = false;
            movingGrid.Comp.CanSplit = false;
            entMan.EnsureComponent<GridAtmosphereComponent>(stationGrid.Owner);

            transform.SetLocalPosition(movingGrid.Owner, new Vector2(12.25f, -4.5f));
            transform.SetLocalRotation(movingGrid.Owner, Angle.FromDegrees(90));
            Assert.That(transform.SetZLevelFrameOrigin(movingGrid.Owner, 6), Is.True);

            SetTile(stationGrid, 0, 0, -1);
            SetTile(stationGrid, 0, 0, 0, shaftTile.TileId);
            SetTile(stationGrid, 0, 0, 1, shaftTile.TileId);
            SetTile(stationGrid, 0, 0, 2, shaftTile.TileId);
            SetTile(movingGrid, 0, 0, 0);
            SetTile(movingGrid, 0, 0, 1);

            var cable = SpawnAnchored("CableApcExtension", stationGrid, new Vector2(0.5f, 0.5f), -1);
            var pipe = SpawnAnchored("GasPipeStraight", stationGrid, new Vector2(0.5f, 0.5f), 1);
            var apc = SpawnAnchored("APCBasic", stationGrid, new Vector2(0.5f, 0.5f), 2);
            var opening = SpawnAnchored("ZLevelFloorOpeningMarker", stationGrid, new Vector2(0.5f, 0.5f), 0);
            var lowerStop = SpawnAnchored("ZLevelElevatorStop", stationGrid, new Vector2(0.5f, 0.5f), 0);
            var upperStop = SpawnAnchored("ZLevelElevatorStop", stationGrid, new Vector2(0.5f, 0.5f), 2);
            sourceCabin = SpawnAnchored("ZLevelElevatorCabin", stationGrid, new Vector2(0.5f, 0.5f), 0);
            entMan.GetComponent<ZLevelElevatorStopComponent>(lowerStop).Label = "Engineering";
            entMan.GetComponent<ZLevelElevatorStopComponent>(upperStop).Label = "Bridge";
            var cabinComponent = entMan.GetComponent<ZLevelElevatorCabinComponent>(sourceCabin);
            cabinComponent.RequirePower = false;
            cabinComponent.TravelTimePerLevel = TimeSpan.FromSeconds(30);
            cabinComponent.NavigationCallCost = 7.5f;
            cabinComponent.NavigationCostPerLevel = 3.25f;
            var sealedBoundary = SpawnAnchored(
                "ZLevelSealedBoundaryMarker",
                movingGrid,
                new Vector2(0.5f, 0.5f),
                1);
            var persistentDevice = entMan.SpawnEntity(
                "RemoteSignaller",
                new EntityCoordinates(movingGrid.Owner, new Vector2(0.5f, 0.5f)));
            Assert.That(zLevel.SetZLevelPosition(persistentDevice, 0), Is.True);
            entMan.EnsureComponent<DeviceListComponent>(pipe);

            var actor = entMan.SpawnEntity(
                "Crowbar",
                new EntityCoordinates(stationGrid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<ActorComponent>(actor);
            Assert.That(zLevel.SetZLevelPosition(actor, 2), Is.True);
            entMan.SpawnEntity("Wrench", new EntityCoordinates(actor, Vector2.Zero));

            var transient = entMan.SpawnEntity(
                "Wirecutter",
                new EntityCoordinates(stationGrid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<MappingSnapshotTransientComponent>(transient);
            Assert.That(zLevel.SetZLevelPosition(transient, -1), Is.True);

            mapSystem.InitializeMap(sourceMapId);
            Assert.That(deviceLists.UpdateDeviceList(pipe, [persistentDevice]),
                Is.EqualTo(DeviceListUpdateResult.UpdateOk));

            var stationAtmosphere = entMan.GetComponent<GridAtmosphereComponent>(stationGrid.Owner);
            var processEntity = GetAtmosphereProcessEntity(entMan, stationGrid.Owner);
            atmosphere.RunProcessingFull(processEntity, mapUid, atmosphere.AtmosTickRate);
            var upperAtmosphere = atmosphere.GetZLevelTileMixture(
                (stationGrid.Owner, stationAtmosphere),
                null,
                new ZLevelTileIndices(0, 0, 1));
            Assert.That(upperAtmosphere, Is.Not.Null);
            upperAtmosphere!.Clear();
            upperAtmosphere.Temperature = 731.25f;
            upperAtmosphere.AdjustMoles(Gas.Oxygen, 900f);
            upperAtmosphere.AdjustMoles(Gas.Nitrogen, 34.75f);
            upperAtmosphere.AdjustMoles(Gas.CarbonDioxide, 3.25f);
            upperAtmosphere.AdjustMoles(Gas.Plasma, 100f);
            upperAtmosphere.AdjustMoles(Gas.Tritium, 4.5f);
            upperAtmosphere.AdjustMoles(Gas.WaterVapor, 5.75f);
            upperAtmosphere.AdjustMoles(Gas.Ammonia, 6.125f);
            upperAtmosphere.AdjustMoles(Gas.NitrousOxide, 7.875f);
            upperAtmosphere.AdjustMoles(Gas.Frezon, 8.625f);
            atmosphere.HotspotExpose(
                (stationGrid.Owner, stationAtmosphere),
                new ZLevelTileIndices(0, 0, 1),
                1500f,
                100f);
            atmosphere.SetAtmosphereSimulation((stationGrid.Owner, stationAtmosphere), false);
            Assert.That(atmosphere.IsHotspotActive(
                stationGrid.Owner,
                new ZLevelTileIndices(0, 0, 1)), Is.True);

            sourceState = CaptureAuthoredMapState(
                entMan,
                mapManager,
                mapSystem,
                transform,
                mapUid);
            Assert.Multiple(() =>
            {
                Assert.That(sourceState.Grids, Has.Length.EqualTo(2));
                Assert.That(sourceState.Tiles, Has.Length.EqualTo(6));
                Assert.That(sourceState.Entities.Select(entity => entity.Locator.Prototype),
                    Is.EquivalentTo(RoundTripPrototypes));
                Assert.That(sourceState.References, Has.Length.EqualTo(2));
                Assert.That(sourceState.Atmospheres, Has.Length.EqualTo(4));
                Assert.That(entMan.GetComponent<MetaDataComponent>(mapUid).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(format.TryValidate(mapUid, out var validationError), Is.True, validationError);
                Assert.That(cable.IsValid() && apc.IsValid() && opening.IsValid() && sealedBoundary.IsValid(), Is.True);
            });

            var elevators = entMan.System<ZLevelElevatorSystem>();
            Assert.That(elevators.TryRequestFloor(sourceCabin, 2),
                Is.EqualTo(ZLevelElevatorRequestResult.Started));
            Assert.Multiple(() =>
            {
                Assert.That(elevators.IsTravelPending(sourceCabin), Is.True);
                Assert.That(cabinComponent.State, Is.EqualTo(ZLevelElevatorState.Moving));
                Assert.That(cabinComponent.TargetLevel, Is.EqualTo(2));
            });

            Assert.That(snapshots.TryCreateMapSnapshot(
                    mapUid,
                    out firstSnapshot,
                    out var report,
                    out var error),
                Is.True,
                error);
            Assert.Multiple(() =>
            {
                Assert.That(report.PlayerRoots, Is.EqualTo(1));
                Assert.That(report.ExplicitTransientRoots, Is.EqualTo(1));
                Assert.That(report.ExcludedRoots, Is.EqualTo(2));
                Assert.That(report.NormalizedReferences, Is.Zero);
            });

            void SetTile(Entity<MapGridComponent> grid, int x, int y, int z, ushort tileId = 1)
            {
                mapSystem.SetZLevelTile(
                    grid.Owner,
                    grid.Comp,
                    new ZLevelTileIndices(x, y, z),
                    new Tile(tileId));
            }

            EntityUid SpawnAnchored(
                string prototype,
                Entity<MapGridComponent> grid,
                Vector2 position,
                int z)
            {
                var uid = entMan.SpawnEntity(prototype, new EntityCoordinates(grid.Owner, position));
                var xform = entMan.GetComponent<TransformComponent>(uid);
                transform.Unanchor(uid, xform);
                Assert.That(zLevel.SetZLevelPosition(uid, z), Is.True);
                Assert.That(transform.AnchorEntity(uid, xform), Is.True);
                return uid;
            }
        });

        await server.WaitAssertion(() =>
        {
            using var reader = CreateSnapshotReader(firstSnapshot);
            Assert.That(loader.TryLoadGeneric(
                    reader,
                    "initialized authored map snapshot cycle 1",
                    out firstLoad,
                    CreateSnapshotLoadOptions()),
                Is.True);
            AssertLoadedSnapshot(entMan, format, firstLoad);
            AssertLoadedElevatorNetwork(entMan, transform, firstLoad);

            var firstMap = firstLoad.Maps.Single().Owner;
            var firstState = CaptureAuthoredMapState(
                entMan,
                mapManager,
                mapSystem,
                transform,
                firstMap);
            AssertAuthoredMapState(sourceState, firstState, "first snapshot/load cycle");
            AssertTransientPrototypesAbsent(entMan, firstLoad);
            Assert.That(atmosphere.IsHotspotActive(
                GetStationGrid(entMan, firstLoad),
                new ZLevelTileIndices(0, 0, 1)), Is.False,
                "Active fire is transient round state and must not enter an authored map snapshot.");

            Assert.That(snapshots.TryCreateMapSnapshot(
                    firstMap,
                    out secondSnapshot,
                    out var report,
                    out var error),
                Is.True,
                error);
            Assert.Multiple(() =>
            {
                Assert.That(report.ExcludedRoots, Is.Zero);
                Assert.That(report.TransientComponents, Is.Zero);
                Assert.That(report.NormalizedReferences, Is.Zero);
            });
        });

        await server.WaitAssertion(() =>
        {
            using var reader = CreateSnapshotReader(secondSnapshot);
            Assert.That(loader.TryLoadGeneric(
                    reader,
                    "initialized authored map snapshot cycle 2",
                    out secondLoad,
                    CreateSnapshotLoadOptions()),
                Is.True);
            AssertLoadedSnapshot(entMan, format, secondLoad);
            AssertLoadedElevatorNetwork(entMan, transform, secondLoad);

            var firstState = CaptureAuthoredMapState(
                entMan,
                mapManager,
                mapSystem,
                transform,
                firstLoad.Maps.Single().Owner);
            var secondState = CaptureAuthoredMapState(
                entMan,
                mapManager,
                mapSystem,
                transform,
                secondLoad.Maps.Single().Owner);
            AssertAuthoredMapState(sourceState, secondState, "source to second snapshot/load cycle");
            AssertAuthoredMapState(firstState, secondState, "first to second snapshot/load cycle");
            AssertTransientPrototypesAbsent(entMan, secondLoad);
            Assert.That(atmosphere.IsHotspotActive(
                GetStationGrid(entMan, secondLoad),
                new ZLevelTileIndices(0, 0, 1)), Is.False,
                "A second authored-map cycle must not manufacture transient fire state.");
        });

        await server.WaitPost(() =>
        {
            loader.Delete(firstLoad);
            loader.Delete(secondLoad);
            mapSystem.DeleteMap(sourceMapId);
        });
    }

    private static MapLoadOptions CreateSnapshotLoadOptions()
    {
        var options = MapLoadOptions.Default;
        options.ExpectedCategory = FileCategory.Map;
        options.DeserializationOptions.PauseMaps = true;
        return options;
    }

    private static StringReader CreateSnapshotReader(MappingDataNode snapshot)
    {
        var document = new YamlDocument(snapshot.ToYaml());
        var stream = new YamlStream { document };
        var writer = new StringWriter();
        stream.Save(new YamlMappingFix(new Emitter(writer)), false);
        return new StringReader(writer.ToString());
    }

    private static void AssertLoadedSnapshot(
        IEntityManager entMan,
        SharedZLevelMapSystem format,
        LoadResult load)
    {
        Assert.Multiple(() =>
        {
            Assert.That(load.Category, Is.EqualTo(FileCategory.Map));
            Assert.That(load.Maps, Has.Count.EqualTo(1));
            Assert.That(load.Grids, Has.Count.EqualTo(2));
            Assert.That(load.Orphans, Is.Empty);
            Assert.That(load.NullspaceEntities, Is.Empty);
            Assert.That(load.InvalidEntityReferences, Is.Empty);
            Assert.That(entMan.GetComponent<MetaDataComponent>(load.Maps.Single().Owner).EntityLifeStage,
                Is.EqualTo(EntityLifeStage.MapInitialized));
            Assert.That(format.TryValidate(load.Maps.Single().Owner, out var error), Is.True, error);
        });
    }

    private static void AssertLoadedElevatorNetwork(
        IEntityManager entMan,
        SharedTransformSystem transform,
        LoadResult load)
    {
        var cabin = load.Entities.Single(uid =>
            entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "ZLevelElevatorCabin");
        var stops = load.Entities
            .Where(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "ZLevelElevatorStop")
            .OrderBy(uid => transform.GetZLevel((
                uid,
                entMan.GetComponent<TransformComponent>(uid),
                entMan.GetComponentOrNull<ZLevelPositionComponent>(uid))))
            .ToArray();
        var cabinComponent = entMan.GetComponent<ZLevelElevatorCabinComponent>(cabin);
        var elevators = entMan.System<ZLevelElevatorSystem>();
        var graph = entMan.System<ZLevelTraversalGraphSystem>();
        var mapId = load.Maps.Single().Comp.MapId;
        var elevatorEdges = graph.CreateSnapshot(mapId).Edges
            .Where(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(stops, Has.Length.EqualTo(2));
            Assert.That(transform.GetZLevel((
                cabin,
                entMan.GetComponent<TransformComponent>(cabin),
                entMan.GetComponentOrNull<ZLevelPositionComponent>(cabin))), Is.Zero);
            Assert.That(stops.Select(uid => transform.GetZLevel((
                    uid,
                    entMan.GetComponent<TransformComponent>(uid),
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)))),
                Is.EqualTo(new[] { 0, 2 }));
            Assert.That(stops.Select(uid => entMan.GetComponent<ZLevelElevatorStopComponent>(uid).Label),
                Is.EqualTo(new[] { "Engineering", "Bridge" }));
            Assert.That(cabinComponent.ShaftId, Is.EqualTo("main"));
            Assert.That(cabinComponent.TravelTimePerLevel, Is.EqualTo(TimeSpan.FromSeconds(30)));
            Assert.That(cabinComponent.NavigationCallCost, Is.EqualTo(7.5f));
            Assert.That(cabinComponent.NavigationCostPerLevel, Is.EqualTo(3.25f));
            Assert.That(cabinComponent.RequirePower, Is.False);
            Assert.That(cabinComponent.State, Is.EqualTo(ZLevelElevatorState.Idle));
            Assert.That(cabinComponent.TargetLevel, Is.Null);
            Assert.That(cabinComponent.ArrivalTime, Is.EqualTo(TimeSpan.Zero));
            Assert.That(elevators.IsTravelPending(cabin), Is.False);
            Assert.That(elevatorEdges, Has.Length.EqualTo(2));
            Assert.That(elevatorEdges.Select(edge => edge.ZOffset), Is.EqualTo(new[] { 2, -2 }));
            Assert.That(elevatorEdges.All(edge => edge.Cost == 14f), Is.True);
        });
    }

    private static void AssertTransientPrototypesAbsent(IEntityManager entMan, LoadResult load)
    {
        var prototypes = load.Entities
            .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
            .Where(id => id != null)
            .ToArray();
        Assert.That(prototypes,
            Has.None.EqualTo("Crowbar")
                .Or.EqualTo("Wrench")
                .Or.EqualTo("Wirecutter"));
    }

    private static EntityUid GetStationGrid(IEntityManager entMan, LoadResult load)
    {
        return load.Grids.Single(grid =>
        {
            var xform = entMan.GetComponent<TransformComponent>(grid.Owner);
            return xform.LocalPosition == Vector2.Zero &&
                   entMan.GetComponentOrNull<ZLevelFrameComponent>(grid.Owner)?.Origin is null or 0;
        }).Owner;
    }

    private static Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>
        GetAtmosphereProcessEntity(IEntityManager entMan, EntityUid gridUid)
    {
        return new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
            gridUid,
            entMan.GetComponent<GridAtmosphereComponent>(gridUid),
            entMan.GetComponent<GasTileOverlayComponent>(gridUid),
            entMan.GetComponent<MapGridComponent>(gridUid),
            entMan.GetComponent<TransformComponent>(gridUid));
    }

    private static AuthoredMapState CaptureAuthoredMapState(
        IEntityManager entMan,
        IMapManager mapManager,
        SharedMapSystem mapSystem,
        SharedTransformSystem transform,
        EntityUid mapUid)
    {
        var map = entMan.GetComponent<MapComponent>(mapUid);
        var config = entMan.GetComponent<ZLevelMapComponent>(mapUid);
        var grids = mapManager.GetAllGrids(map.MapId)
            .OrderBy(grid => Canonical(entMan.GetComponent<TransformComponent>(grid.Owner).LocalPosition.X))
            .ThenBy(grid => Canonical(entMan.GetComponent<TransformComponent>(grid.Owner).LocalPosition.Y))
            .ThenBy(grid => entMan.GetComponentOrNull<ZLevelFrameComponent>(grid.Owner)?.Origin ?? 0)
            .ToArray();
        var gridIndices = grids
            .Select((grid, index) => (grid.Owner, index))
            .ToDictionary(entry => entry.Owner, entry => entry.index);

        var gridStates = grids.Select(grid =>
        {
            var xform = entMan.GetComponent<TransformComponent>(grid.Owner);
            return new AuthoredGridState(
                Canonical(xform.LocalPosition.X),
                Canonical(xform.LocalPosition.Y),
                Canonical(xform.LocalRotation.Theta),
                entMan.GetComponentOrNull<ZLevelFrameComponent>(grid.Owner)?.Origin ?? 0,
                grid.Comp.TileSize,
                grid.Comp.CanSplit);
        }).ToArray();

        var tiles = grids
            .SelectMany(grid => mapSystem.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp)
                .Select(tile => new AuthoredTileState(
                    gridIndices[grid.Owner],
                    tile.X,
                    tile.Y,
                    tile.Z,
                    tile.Tile.TypeId,
                    tile.Tile.Flags,
                    tile.Tile.Variant,
                    tile.Tile.RotationMirroring)))
            .OrderBy(tile => tile.Grid)
            .ThenBy(tile => tile.Z)
            .ThenBy(tile => tile.X)
            .ThenBy(tile => tile.Y)
            .ToArray();

        var atmospheres = grids
            .SelectMany(grid =>
            {
                if (entMan.GetComponentOrNull<GridAtmosphereComponent>(grid.Owner) is not { } atmosphere)
                    return [];

                var baseTiles = atmosphere.Tiles
                    .Where(entry => entry.Value.Air != null && !entry.Value.NoGridTile)
                    .Select(entry => CreateAtmosphereState(
                        gridIndices[grid.Owner],
                        entry.Key.X,
                        entry.Key.Y,
                        0,
                        entry.Value.Air!));
                var upperTiles = atmosphere.ZLevelTiles
                    .Where(entry => entry.Value.Air != null && !entry.Value.NoGridTile)
                    .Select(entry => CreateAtmosphereState(
                        gridIndices[grid.Owner],
                        entry.Key.X,
                        entry.Key.Y,
                        entry.Key.Z,
                        entry.Value.Air!));
                return baseTiles.Concat(upperTiles);
            })
            .OrderBy(atmosphere => atmosphere.Grid)
            .ThenBy(atmosphere => atmosphere.Z)
            .ThenBy(atmosphere => atmosphere.X)
            .ThenBy(atmosphere => atmosphere.Y)
            .ToArray();

        var authoredEntities = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
            .Select(entry => entry.Uid)
            .Where(uid =>
            {
                var prototype = entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID;
                return prototype != null &&
                       RoundTripPrototypes.Contains(prototype) &&
                       entMan.GetComponent<TransformComponent>(uid).MapUid == mapUid;
            })
            .Select(uid =>
            {
                var metadata = entMan.GetComponent<MetaDataComponent>(uid);
                var xform = entMan.GetComponent<TransformComponent>(uid);
                Assert.That(xform.GridUid, Is.Not.Null, $"Authored entity {metadata.EntityPrototype?.ID} lost its grid.");
                var boundary = entMan.GetComponentOrNull<ZLevelBoundaryComponent>(uid);
                var elevatorCabin = entMan.GetComponentOrNull<ZLevelElevatorCabinComponent>(uid);
                var elevatorStop = entMan.GetComponentOrNull<ZLevelElevatorStopComponent>(uid);
                var locator = new AuthoredEntityLocator(
                    metadata.EntityPrototype!.ID,
                    gridIndices[xform.GridUid!.Value],
                    Canonical(xform.LocalPosition.X),
                    Canonical(xform.LocalPosition.Y),
                    transform.GetZLevel((
                        uid,
                        xform,
                        entMan.GetComponentOrNull<ZLevelPositionComponent>(uid))));
                var state = new AuthoredEntityState(
                    locator,
                    Canonical(xform.LocalRotation.Theta),
                    xform.Anchored,
                    boundary?.Enabled,
                    boundary?.BoundaryOffset,
                    boundary?.Opens,
                    boundary?.Closes,
                    elevatorCabin == null
                        ? null
                        : new AuthoredElevatorCabinState(
                            elevatorCabin.ShaftId,
                            elevatorCabin.TravelTimePerLevel,
                            elevatorCabin.IdlePowerDraw,
                            elevatorCabin.TravelPowerDraw,
                            elevatorCabin.MaxTravelLevels,
                            elevatorCabin.PassengerLimit,
                            elevatorCabin.NavigationCallCost,
                            elevatorCabin.NavigationCostPerLevel,
                            elevatorCabin.RequirePower),
                    elevatorStop == null
                        ? null
                        : new AuthoredElevatorStopState(
                            elevatorStop.ShaftId,
                            elevatorStop.Label));
                return (Uid: uid, Locator: locator, State: state);
            })
            .OrderBy(entry => entry.Locator.Prototype)
            .ThenBy(entry => entry.Locator.Grid)
            .ThenBy(entry => entry.Locator.Z)
            .ThenBy(entry => entry.Locator.X)
            .ThenBy(entry => entry.Locator.Y)
            .ToArray();
        var locators = authoredEntities.ToDictionary(entry => entry.Uid, entry => entry.Locator);
        var references = new List<AuthoredReferenceState>();
        foreach (var entity in authoredEntities)
        {
            if (entMan.GetComponentOrNull<DeviceListComponent>(entity.Uid) is { } deviceList)
            {
                foreach (var target in deviceList.Devices)
                {
                    if (locators.TryGetValue(target, out var targetLocator))
                        references.Add(new AuthoredReferenceState("DeviceList.Devices", entity.Locator, targetLocator));
                }
            }

            if (entMan.GetComponentOrNull<DeviceNetworkComponent>(entity.Uid) is { } deviceNetwork)
            {
                foreach (var target in deviceNetwork.DeviceLists)
                {
                    if (locators.TryGetValue(target, out var targetLocator))
                        references.Add(new AuthoredReferenceState("DeviceNetwork.DeviceLists", entity.Locator, targetLocator));
                }
            }
        }

        return new AuthoredMapState(
            new AuthoredMapConfiguration(
                config.FormatVersion,
                config.MinimumLevel,
                config.MaximumLevel,
                config.DefaultLevel,
                config.DefaultBoundaryMode),
            gridStates,
            tiles,
            atmospheres,
            authoredEntities.Select(entry => entry.State).ToArray(),
            references
                .OrderBy(reference => reference.Kind)
                .ThenBy(reference => reference.Source.Prototype)
                .ThenBy(reference => reference.Target.Prototype)
                .ToArray());

        AuthoredAtmosphereState CreateAtmosphereState(
            int grid,
            int x,
            int y,
            int z,
            GasMixture mixture)
        {
            return new AuthoredAtmosphereState(
                grid,
                x,
                y,
                z,
                Canonical(mixture.Temperature),
                Canonical(mixture.Volume),
                Canonical(mixture.GetMoles(Gas.Oxygen)),
                Canonical(mixture.GetMoles(Gas.Nitrogen)),
                Canonical(mixture.GetMoles(Gas.CarbonDioxide)),
                Canonical(mixture.GetMoles(Gas.Plasma)),
                Canonical(mixture.GetMoles(Gas.Tritium)),
                Canonical(mixture.GetMoles(Gas.WaterVapor)),
                Canonical(mixture.GetMoles(Gas.Ammonia)),
                Canonical(mixture.GetMoles(Gas.NitrousOxide)),
                Canonical(mixture.GetMoles(Gas.Frezon)));
        }
    }

    private static void AssertAuthoredMapState(
        AuthoredMapState expected,
        AuthoredMapState actual,
        string cycle)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.Configuration, Is.EqualTo(expected.Configuration), cycle);
            Assert.That(actual.Grids, Is.EqualTo(expected.Grids), cycle);
            Assert.That(actual.Tiles, Is.EqualTo(expected.Tiles), cycle);
            Assert.That(actual.Atmospheres, Is.EqualTo(expected.Atmospheres), cycle);
            Assert.That(actual.Entities, Is.EqualTo(expected.Entities), cycle);
            Assert.That(actual.References, Is.EqualTo(expected.References), cycle);
        });
    }

    private static double Canonical(float value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private static double Canonical(double value)
    {
        return Math.Round(value, 6, MidpointRounding.AwayFromZero);
    }

    private sealed record AuthoredMapState(
        AuthoredMapConfiguration Configuration,
        AuthoredGridState[] Grids,
        AuthoredTileState[] Tiles,
        AuthoredAtmosphereState[] Atmospheres,
        AuthoredEntityState[] Entities,
        AuthoredReferenceState[] References);

    private readonly record struct AuthoredMapConfiguration(
        int FormatVersion,
        int MinimumLevel,
        int MaximumLevel,
        int DefaultLevel,
        ZLevelDefaultBoundaryMode BoundaryMode);

    private readonly record struct AuthoredGridState(
        double X,
        double Y,
        double Rotation,
        int FrameOrigin,
        ushort TileSize,
        bool CanSplit);

    private readonly record struct AuthoredTileState(
        int Grid,
        int X,
        int Y,
        int Z,
        int TypeId,
        byte Flags,
        byte Variant,
        byte RotationMirroring);

    private readonly record struct AuthoredAtmosphereState(
        int Grid,
        int X,
        int Y,
        int Z,
        double Temperature,
        double Volume,
        double Oxygen,
        double Nitrogen,
        double CarbonDioxide,
        double Plasma,
        double Tritium,
        double WaterVapor,
        double Ammonia,
        double NitrousOxide,
        double Frezon);

    private readonly record struct AuthoredEntityLocator(
        string Prototype,
        int Grid,
        double X,
        double Y,
        int Z);

    private readonly record struct AuthoredEntityState(
        AuthoredEntityLocator Locator,
        double Rotation,
        bool Anchored,
        bool? BoundaryEnabled,
        int? BoundaryOffset,
        ZLevelBoundaryChannels? Opens,
        ZLevelBoundaryChannels? Closes,
        AuthoredElevatorCabinState? ElevatorCabin,
        AuthoredElevatorStopState? ElevatorStop);

    private readonly record struct AuthoredElevatorCabinState(
        string ShaftId,
        TimeSpan TravelTimePerLevel,
        float IdlePowerDraw,
        float TravelPowerDraw,
        int MaxTravelLevels,
        int PassengerLimit,
        float NavigationCallCost,
        float NavigationCostPerLevel,
        bool RequirePower);

    private readonly record struct AuthoredElevatorStopState(
        string ShaftId,
        string Label);

    private readonly record struct AuthoredReferenceState(
        string Kind,
        AuthoredEntityLocator Source,
        AuthoredEntityLocator Target);
}
