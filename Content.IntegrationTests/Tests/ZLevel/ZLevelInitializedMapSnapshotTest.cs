// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Actions;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Mapping;
using Content.Server.Mind;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Follower;
using Content.Shared.Follower.Components;
using Content.Shared.Mapping;
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
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelInitializedMapSnapshotTest : GameTest
{
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
}
