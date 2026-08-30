// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Administration.Managers;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Decals;
using Content.Server.Mapping;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Mapping;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelInitializedMappingMutationTest : GameTest
{
    private static readonly ProtoId<ContentTileDefinition> ShaftTile = "FloorZLevelShaft";

    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false, Dirty = true };

    [Test]
    public async Task NetworkFloorLifecyclePreservesRuntimeStateOnInitializedMap()
    {
        var testMap = await Pair.CreateTestMap();
        var session = Pair.Player!;
        var player = session.AttachedEntity!.Value;
        var admins = Server.ResolveDependency<IAdminManager>();
        var mapManager = Server.ResolveDependency<IMapManager>();
        var network = Client.ResolveDependency<IEntityNetworkManager>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var format = SEntMan.System<SharedZLevelMapSystem>();
        var zLevel = SEntMan.System<SharedZLevelSystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var decals = SEntMan.System<DecalSystem>();
        var atmosphere = SEntMan.System<AtmosphereSystem>();
        var snapshots = SEntMan.System<MappingSnapshotSystem>();
        var prototypes = Server.ResolveDependency<IPrototypeManager>();
        var shaft = prototypes.Index(ShaftTile);

        EntityUid sourceAuthored = default;
        EntityUid sourcePipe = default;
        EntityUid sourceCable = default;
        EntityUid targetAuthored = default;
        EntityUid sourceRuntimeChild = default;
        EntityUid targetRuntimeChild = default;
        EntityUid directTargetActor = default;
        GasMixture sourceMixture = default!;
        GasMixture replacedTargetMixture = default!;
        TileAtmosphere replacedTargetAtmosphere = default!;
        NetEntity mapNet = default;
        NetEntity gridNet = default;
        EntityUid secondaryGridUid = default;
        EntityUid elevatorCabin = default;
        EntityUid sourceElevatorStop = default;

        await Server.WaitAssertion(() =>
        {
            var grid = testMap.Grid;
            format.Configure(testMap.MapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 0),
                new Tile(shaft.TileId));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(1, 0, 1), new Tile(1));

            Assert.That(decals.TryAddDecal(
                "burnt1",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)),
                out _,
                zLevel: 0), Is.True);
            Assert.That(decals.TryAddDecal(
                "burnt1",
                new EntityCoordinates(grid.Owner, new Vector2(1.5f, 0.5f)),
                out _,
                zLevel: 1), Is.True);
            Assert.That(decals.TryAddDecal(
                "burnt1",
                new EntityCoordinates(grid.Owner, new Vector2(1.6f, 0.5f)),
                out _,
                zLevel: 1), Is.True);

            sourceAuthored = SpawnAnchored("ZLevelFloorOpeningMarker", new Vector2(0.5f, 0.5f), 0);
            sourcePipe = SpawnAnchored("GasPipeStraight", new Vector2(0.5f, 0.5f), 0);
            sourceCable = SpawnAnchored("CableApcExtension", new Vector2(0.5f, 0.5f), 0);
            sourceElevatorStop = SpawnAnchored("ZLevelElevatorStop", new Vector2(0.5f, 0.5f), 0);
            elevatorCabin = SpawnAnchored("ZLevelElevatorCabin", new Vector2(0.5f, 0.5f), 0);
            var elevatorComponent = SEntMan.GetComponent<ZLevelElevatorCabinComponent>(elevatorCabin);
            elevatorComponent.RequirePower = false;
            elevatorComponent.TravelTimePerLevel = TimeSpan.FromSeconds(30);
            targetAuthored = SpawnAnchored("ZLevelSealedBoundaryMarker", new Vector2(0.5f, 0.5f), 1);

            sourceRuntimeChild = SEntMan.SpawnEntity(
                "Wrench",
                new EntityCoordinates(sourceAuthored, Vector2.Zero));
            SEntMan.EnsureComponent<MappingSnapshotTransientComponent>(sourceRuntimeChild);

            targetRuntimeChild = SEntMan.SpawnEntity(
                "Crowbar",
                new EntityCoordinates(targetAuthored, Vector2.Zero));
            SEntMan.EnsureComponent<MappingSnapshotTransientComponent>(targetRuntimeChild);

            directTargetActor = SEntMan.SpawnEntity(
                "Screwdriver",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            SEntMan.EnsureComponent<ActorComponent>(directTargetActor);
            Assert.That(zLevel.SetZLevelPosition(directTargetActor, 1), Is.True);

            SEntMan.EnsureComponent<GridAtmosphereComponent>(grid.Owner);
            SEntMan.EnsureComponent<GasTileOverlayComponent>(grid.Owner);
            var gridAtmosphere = SEntMan.GetComponent<GridAtmosphereComponent>(grid.Owner);
            var processEntity = GetAtmosphereProcessEntity(grid.Owner);
            atmosphere.RunProcessingFull(processEntity, testMap.MapUid, atmosphere.AtmosTickRate);

            sourceMixture = atmosphere.GetZLevelTileMixture(
                (grid.Owner, gridAtmosphere),
                null,
                new ZLevelTileIndices(0, 0, 0))!;
            replacedTargetMixture = atmosphere.GetZLevelTileMixture(
                (grid.Owner, gridAtmosphere),
                null,
                new ZLevelTileIndices(0, 0, 1))!;
            Assert.That(sourceMixture, Is.Not.SameAs(GasMixture.SpaceGas));
            Assert.That(replacedTargetMixture, Is.Not.SameAs(GasMixture.SpaceGas));

            sourceMixture.Clear();
            sourceMixture.Temperature = 456.75f;
            sourceMixture.AdjustMoles(Gas.Oxygen, 123.5f);
            sourceMixture.AdjustMoles(Gas.Nitrogen, 67.25f);
            replacedTargetMixture.Clear();
            replacedTargetMixture.Temperature = 812.5f;
            replacedTargetMixture.AdjustMoles(Gas.Oxygen, 100f);
            replacedTargetMixture.AdjustMoles(Gas.Plasma, 88f);

            replacedTargetAtmosphere = gridAtmosphere.ZLevelTiles[new ZLevelTileIndices(0, 0, 1)];
            atmosphere.HotspotExpose(
                (grid.Owner, gridAtmosphere),
                new ZLevelTileIndices(0, 0, 1),
                1500f,
                100f);
            Assert.That(atmosphere.IsHotspotActive(
                grid.Owner,
                new ZLevelTileIndices(0, 0, 1)), Is.True);
            atmosphere.SetAtmosphereSimulation((grid.Owner, gridAtmosphere), false);

            transform.SetCoordinates(player,
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            Assert.That(zLevel.SetZLevelPosition(player, 0), Is.True);
            admins.ReAdmin(session);
            mapNet = SEntMan.GetNetEntity(testMap.MapUid);
            gridNet = SEntMan.GetNetEntity(grid.Owner);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(testMap.MapUid).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(sourceAuthored).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(sourcePipe).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(sourceCable).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<TransformComponent>(sourceAuthored).ParentUid,
                    Is.EqualTo(grid.Owner));
                Assert.That(SEntMan.GetComponent<TransformComponent>(targetAuthored).ParentUid,
                    Is.EqualTo(grid.Owner));
                Assert.That(GetLocalZ(sourceAuthored), Is.Zero);
                Assert.That(GetLocalZ(targetAuthored), Is.EqualTo(1));
                Assert.That(snapshots.IsPersistentSnapshotEntity(sourceAuthored, testMap.MapUid), Is.True);
                Assert.That(snapshots.IsPersistentSnapshotEntity(targetAuthored, testMap.MapUid), Is.True);
                Assert.That(snapshots.IsPersistentSnapshotEntity(sourceElevatorStop, testMap.MapUid), Is.True);
                Assert.That(snapshots.IsPersistentSnapshotEntity(elevatorCabin, testMap.MapUid), Is.True);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(sourceAuthored).EntityPrototype?.MapSavable,
                    Is.Not.False);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(targetAuthored).EntityPrototype?.MapSavable,
                    Is.Not.False);
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
            });

            EntityUid SpawnAnchored(string prototype, Vector2 position, int level)
            {
                var uid = SEntMan.SpawnEntity(prototype, new EntityCoordinates(grid.Owner, position));
                var xform = SEntMan.GetComponent<TransformComponent>(uid);
                var alreadyAnchored = xform.Anchored;
                Assert.That(zLevel.SetZLevelPosition(uid, level), Is.True);
                if (!alreadyAnchored)
                    transform.AnchorEntity(uid, xform);
                return uid;
            }
        });

        await SendRequest(
            ZLevelMappingOperation.ConfigureMap,
            minimumLevel: 0,
            maximumLevel: 0,
            defaultLevel: 0);
        await Server.WaitAssertion(() =>
        {
            var config = SEntMan.GetComponent<ZLevelMapComponent>(testMap.MapUid);
            Assert.Multiple(() =>
            {
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.EqualTo(1),
                    "An initialized map range cannot contract around authored content without deleting its edge floor.");
                Assert.That(config.DefaultBoundaryMode, Is.EqualTo(ZLevelDefaultBoundaryMode.ExplicitOnly));
            });
        });

        await SendRequest(ZLevelMappingOperation.CreateLevel, targetLevel: 2);
        await Server.WaitAssertion(() =>
        {
            var config = SEntMan.GetComponent<ZLevelMapComponent>(testMap.MapUid);
            Assert.Multiple(() =>
            {
                Assert.That(config.MaximumLevel, Is.EqualTo(2));
                Assert.That(GetLocalZ(player), Is.EqualTo(2));
            });
        });

        await SendRequest(ZLevelMappingOperation.DeleteLevel, targetLevel: 2);
        await Server.WaitAssertion(() =>
        {
            var config = SEntMan.GetComponent<ZLevelMapComponent>(testMap.MapUid);
            Assert.Multiple(() =>
            {
                Assert.That(config.MaximumLevel, Is.EqualTo(1));
                Assert.That(GetLocalZ(player), Is.Zero);
                Assert.That(GetLocalZ(targetAuthored), Is.EqualTo(1));
                Assert.That(SEntMan.GetComponent<TransformComponent>(targetAuthored).ParentUid,
                    Is.EqualTo(testMap.Grid.Owner));
                Assert.That(snapshots.IsPersistentSnapshotEntity(targetAuthored, testMap.MapUid), Is.True);
            });
        });

        await SendRequest(ZLevelMappingOperation.CopyLevel, sourceLevel: 0, targetLevel: 1);
        await Server.WaitAssertion(() =>
        {
            var grid = testMap.Grid;
            var gridAtmosphere = SEntMan.GetComponent<GridAtmosphereComponent>(grid.Owner);
            var copiedMixture = atmosphere.GetZLevelTileMixture(
                (grid.Owner, gridAtmosphere),
                null,
                new ZLevelTileIndices(0, 0, 1));
            var copiedRoots = FindPrototypeRoots("ZLevelFloorOpeningMarker", 1);
            var copiedPipes = FindPrototypeRoots("GasPipeStraight", 1);
            var copiedCables = FindPrototypeRoots("CableApcExtension", 1);
            var copiedStops = FindPrototypeRoots("ZLevelElevatorStop", 1);
            var sourceCabins = FindPrototypeRoots("ZLevelElevatorCabin", 0);
            var targetCabins = FindPrototypeRoots("ZLevelElevatorCabin", 1);
            var elevatorEdges = SEntMan.System<ZLevelTraversalGraphSystem>()
                .CreateSnapshot(testMap.MapId)
                .Edges
                .Where(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(copiedRoots, Has.Length.EqualTo(1));
                Assert.That(copiedPipes, Has.Length.EqualTo(1));
                Assert.That(copiedCables, Has.Length.EqualTo(1));
                Assert.That(copiedStops, Has.Length.EqualTo(1));
                Assert.That(sourceCabins, Is.EqualTo(new[] { elevatorCabin }));
                Assert.That(targetCabins, Is.Empty,
                    "Floor copying must preserve the one physical cabin instead of cloning it.");
                Assert.That(elevatorEdges, Has.Length.EqualTo(2));
                Assert.That(elevatorEdges.Select(edge => edge.ZOffset), Is.EqualTo(new[] { 1, -1 }));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(copiedRoots[0]).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(copiedPipes[0]).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(copiedCables[0]).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(SEntMan.GetComponent<TransformComponent>(copiedRoots[0]).Anchored, Is.True);
                Assert.That(SEntMan.GetComponent<TransformComponent>(copiedPipes[0]).Anchored, Is.True);
                Assert.That(SEntMan.GetComponent<TransformComponent>(copiedCables[0]).Anchored, Is.True);
                Assert.That(SEntMan.Deleted(targetAuthored), Is.True);
                Assert.That(SEntMan.EntityExists(sourceRuntimeChild), Is.True);
                Assert.That(SEntMan.EntityExists(targetRuntimeChild), Is.True);
                Assert.That(SEntMan.EntityExists(directTargetActor), Is.True);
                Assert.That(SEntMan.GetComponent<TransformComponent>(sourceRuntimeChild).ParentUid,
                    Is.EqualTo(sourceAuthored));
                Assert.That(SEntMan.GetComponent<TransformComponent>(targetRuntimeChild).ParentUid,
                    Is.EqualTo(grid.Owner));
                Assert.That(GetLocalZ(targetRuntimeChild), Is.EqualTo(1));
                Assert.That(GetLocalZ(directTargetActor), Is.EqualTo(1));
                Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                    Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(mapSystem.GetZLevelTileRef(
                    grid.Owner,
                    grid.Comp,
                    new ZLevelTileIndices(0, 0, 1)).Tile.IsEmpty, Is.False);
                Assert.That(mapSystem.GetZLevelTileRef(
                    grid.Owner,
                    grid.Comp,
                    new ZLevelTileIndices(1, 0, 1)).Tile.IsEmpty, Is.True);
                Assert.That(decals.GetDecalsIntersecting(
                    grid.Owner,
                    grid.Comp.LocalAABB,
                    zLevel: 1), Has.Count.EqualTo(1));
                Assert.That(copiedMixture, Is.Not.Null);
                Assert.That(copiedMixture, Is.Not.SameAs(sourceMixture));
                Assert.That(copiedMixture, Is.Not.SameAs(replacedTargetMixture));
                Assert.That(copiedMixture!.Temperature, Is.EqualTo(sourceMixture.Temperature).Within(0.001f));
                Assert.That(copiedMixture.GetMoles(Gas.Oxygen),
                    Is.EqualTo(sourceMixture.GetMoles(Gas.Oxygen)).Within(0.001f));
                Assert.That(copiedMixture.GetMoles(Gas.Nitrogen),
                    Is.EqualTo(sourceMixture.GetMoles(Gas.Nitrogen)).Within(0.001f));
                Assert.That(gridAtmosphere.ZLevelTiles.Values, Does.Not.Contain(replacedTargetAtmosphere));
                Assert.That(atmosphere.IsHotspotActive(
                    grid.Owner,
                    new ZLevelTileIndices(0, 0, 1)), Is.False,
                    "Floor copying must not carry transient hotspot state into the authored clone.");
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
            });

            var elevators = SEntMan.System<ZLevelElevatorSystem>();
            Assert.That(elevators.TryRequestFloor(elevatorCabin, 1),
                Is.EqualTo(ZLevelElevatorRequestResult.Started));
            Assert.That(elevators.IsTravelPending(elevatorCabin), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.GetComponent<MapComponent>(testMap.MapUid);
            var secondaryGrid = mapManager.CreateGridEntity(map.MapId);
            secondaryGridUid = secondaryGrid.Owner;
            mapSystem.SetZLevelTile(
                secondaryGrid.Owner,
                secondaryGrid.Comp,
                new ZLevelTileIndices(0, 0, 1),
                new Tile(1));
        });

        await SendRequest(ZLevelMappingOperation.DeleteLevel, targetLevel: 1);
        await Server.WaitAssertion(() =>
        {
            var grid = testMap.Grid;
            var config = SEntMan.GetComponent<ZLevelMapComponent>(testMap.MapUid);
            var gridAtmosphere = SEntMan.GetComponent<GridAtmosphereComponent>(grid.Owner);

            Assert.Multiple(() =>
            {
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.EqualTo(1),
                    "Deleting one grid's edge floor must preserve the map range while another grid still uses it.");
                Assert.That(GetLocalZ(player), Is.Zero);
                Assert.That(GetLocalZ(targetRuntimeChild), Is.Zero);
                Assert.That(GetLocalZ(directTargetActor), Is.Zero);
                Assert.That(SEntMan.EntityExists(sourceRuntimeChild), Is.True);
                Assert.That(SEntMan.EntityExists(targetRuntimeChild), Is.True);
                Assert.That(SEntMan.EntityExists(directTargetActor), Is.True);
                Assert.That(FindPrototypeRoots("ZLevelFloorOpeningMarker", 1), Is.Empty);
                Assert.That(FindPrototypeRoots("GasPipeStraight", 1), Is.Empty);
                Assert.That(FindPrototypeRoots("CableApcExtension", 1), Is.Empty);
                Assert.That(FindPrototypeRoots("ZLevelElevatorStop", 1), Is.Empty);
                Assert.That(FindPrototypeRoots("ZLevelElevatorCabin", 0),
                    Is.EqualTo(new[] { elevatorCabin }));
                Assert.That(SEntMan.EntityExists(sourceElevatorStop), Is.True);
                Assert.That(SEntMan.System<ZLevelElevatorSystem>().IsTravelPending(elevatorCabin), Is.False);
                Assert.That(SEntMan.GetComponent<ZLevelElevatorCabinComponent>(elevatorCabin).State,
                    Is.EqualTo(ZLevelElevatorState.Idle));
                Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>()
                    .CreateSnapshot(testMap.MapId)
                    .Edges.Any(edge => edge.Source.Kind == ZLevelTraversalKind.Elevator), Is.False);
                Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                    Is.EquivalentTo(new[] { 0 }));
                var secondaryGrid = SEntMan.GetComponent<MapGridComponent>(secondaryGridUid);
                Assert.That(mapSystem.GetExistingZLevelLayers(secondaryGridUid, secondaryGrid),
                    Does.Contain(1));
                Assert.That(decals.GetDecalsIntersecting(
                    grid.Owner,
                    grid.Comp.LocalAABB,
                    zLevel: 1), Is.Empty);
                Assert.That(gridAtmosphere.ZLevelTiles.Keys, Has.None.Matches<ZLevelTileIndices>(indices => indices.Z == 1));
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
            });
        });

        await Server.WaitAssertion(() => gridNet = SEntMan.GetNetEntity(secondaryGridUid));
        await SendRequest(ZLevelMappingOperation.DeleteLevel, targetLevel: 1);
        await Server.WaitAssertion(() =>
        {
            var config = SEntMan.GetComponent<ZLevelMapComponent>(testMap.MapUid);
            var mapping = SEntMan.System<ZLevelMappingSystem>();
            var deleteException = Assert.Throws<InvalidOperationException>(() =>
                mapping.DeleteLevel(testMap.MapUid, config, testMap.Grid, 0));
            Assert.Multiple(() =>
            {
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.Zero);
                Assert.That(SEntMan.Deleted(secondaryGridUid), Is.True,
                    "Removing the final tile-only floor may remove the now-empty grid.");
                Assert.That(deleteException!.Message, Does.Contain("physical elevator cabin"));
                Assert.That(SEntMan.EntityExists(elevatorCabin), Is.True);
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
                Assert.That(snapshots.TryCreateMapSnapshot(
                    testMap.MapUid,
                    out _,
                    out var report,
                    out var snapshotError), Is.True, snapshotError);
                Assert.That(report.PlayerRoots, Is.EqualTo(1));
                Assert.That(report.ExplicitTransientRoots, Is.EqualTo(2));
            });
        });

        async Task SendRequest(
            ZLevelMappingOperation operation,
            int sourceLevel = 0,
            int targetLevel = 0,
            int minimumLevel = 0,
            int maximumLevel = 0,
            int defaultLevel = 0)
        {
            await Client.WaitPost(() => network.SendSystemNetworkMessage(new ZLevelMappingRequestEvent
            {
                Map = mapNet,
                Grid = gridNet,
                Operation = operation,
                SourceLevel = sourceLevel,
                TargetLevel = targetLevel,
                MinimumLevel = minimumLevel,
                MaximumLevel = maximumLevel,
                DefaultLevel = defaultLevel,
                BoundaryMode = ZLevelDefaultBoundaryMode.ExplicitOnly,
            }));
            await Pair.RunTicksSync(6);
        }

        int GetLocalZ(EntityUid uid)
        {
            var xform = SEntMan.GetComponent<TransformComponent>(uid);
            return transform.GetZLevel((uid, xform, SEntMan.GetComponentOrNull<ZLevelPositionComponent>(uid)));
        }

        EntityUid[] FindPrototypeRoots(string prototype, int level)
        {
            return SEntMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
                .Select(entry => (entry.Uid, Transform: (TransformComponent) entry.Component))
                .Where(entry => entry.Transform.ParentUid == testMap.Grid.Owner &&
                                SEntMan.GetComponent<MetaDataComponent>(entry.Uid).EntityPrototype?.ID == prototype &&
                                transform.GetZLevel((entry.Uid,
                                    entry.Transform,
                                    SEntMan.GetComponentOrNull<ZLevelPositionComponent>(entry.Uid))) == level)
                .Select(entry => entry.Uid)
                .ToArray();
        }

        Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>
            GetAtmosphereProcessEntity(EntityUid gridUid)
        {
            return new Entity<GridAtmosphereComponent, GasTileOverlayComponent, MapGridComponent, TransformComponent>(
                gridUid,
                SEntMan.GetComponent<GridAtmosphereComponent>(gridUid),
                SEntMan.GetComponent<GasTileOverlayComponent>(gridUid),
                SEntMan.GetComponent<MapGridComponent>(gridUid),
                SEntMan.GetComponent<TransformComponent>(gridUid));
        }
    }
}
