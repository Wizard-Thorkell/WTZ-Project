// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Mapping;
using Content.Shared.CCVar;
using Content.Shared.Mapping;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelInitializedMappingAutosaveTest : GameTest
{
    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.AutosaveEnabled), true)]
    public async Task ValidatedInitializedMapAutosaveIsAtomicAndLoadable()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var resources = server.ResolveDependency<IResourceManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var zLevel = entMan.System<SharedZLevelSystem>();
        var autosave = entMan.System<MappingSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var name = $"zlevel-initialized-autosave-{Guid.NewGuid():N}.yml";
        var saveDirectory = new ResPath("/Autosaves") / name;

        MapId sourceMapId = default;
        EntityUid sourceMapUid = default;
        EntityUid authored = default;
        ResPath savedPath = default;
        MappingSnapshotReport savedReport = default;

        await server.WaitAssertion(() =>
        {
            resources.UserData.Delete(saveDirectory);
            sourceMapUid = mapSystem.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(sourceMapUid, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            var grid = mapManager.CreateGridEntity(sourceMapId);
            grid.Comp.CanSplit = false;
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));

            authored = entMan.SpawnEntity(
                "ZLevelFloorOpeningMarker",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            Assert.That(zLevel.SetZLevelPosition(authored, 1), Is.True);
            var authoredTransform = entMan.GetComponent<TransformComponent>(authored);
            if (!authoredTransform.Anchored)
                transform.AnchorEntity((authored, authoredTransform), grid);

            var actor = entMan.SpawnEntity(
                "Crowbar",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<ActorComponent>(actor);
            Assert.That(zLevel.SetZLevelPosition(actor, 1), Is.True);

            var transient = entMan.SpawnEntity(
                "Wirecutter",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<MappingSnapshotTransientComponent>(transient);

            mapSystem.InitializeMap(sourceMapId);
            Assert.That(entMan.GetComponent<MetaDataComponent>(sourceMapUid).EntityLifeStage,
                Is.EqualTo(EntityLifeStage.MapInitialized));

            autosave.ToggleAutosave(sourceMapUid, name);
            Assert.That(autosave.IsAutosaving(sourceMapUid), Is.True,
                "Initialized map roots should be eligible for mapper autosave.");
            autosave.ToggleAutosave(grid.Owner, name + "-grid");
            Assert.That(autosave.IsAutosaving(grid.Owner), Is.False,
                "An initialized grid cannot be persisted without its map root.");

            entMan.GetComponent<ZLevelPositionComponent>(authored).ZLevel = 2;
            Assert.That(autosave.TryAutosaveNow(
                sourceMapUid,
                name,
                out var failedPath,
                out _,
                out var validationError), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(failedPath, Is.EqualTo(default(ResPath)));
                Assert.That(validationError, Does.Contain("outside the declared range"));
                Assert.That(resources.UserData.DirectoryEntries(saveDirectory), Is.Empty,
                    "Validation failure must not expose a destination or temporary file.");
            });

            entMan.GetComponent<ZLevelPositionComponent>(authored).ZLevel = 1;
            Assert.That(autosave.TryAutosaveNow(
                sourceMapUid,
                name,
                out savedPath,
                out savedReport,
                out var saveError), Is.True, saveError);

            var files = resources.UserData.DirectoryEntries(saveDirectory).ToArray();
            var bytes = resources.UserData.ReadAllBytes(savedPath);
            var strictUtf8 = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);
            Assert.Multiple(() =>
            {
                Assert.That(savedPath.Directory, Is.EqualTo(saveDirectory));
                Assert.That(resources.UserData.Exists(savedPath), Is.True);
                Assert.That(files, Has.Length.EqualTo(1));
                Assert.That(files[0], Is.EqualTo(savedPath.Filename));
                Assert.That(files, Has.None.EndsWith(".tmp"));
                Assert.That(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble), Is.False);
                Assert.That(strictUtf8.GetString(bytes), Does.Contain("category: Map"));
                Assert.That(savedReport.PlayerRoots, Is.EqualTo(1));
                Assert.That(savedReport.ExplicitTransientRoots, Is.EqualTo(1));
                Assert.That(savedReport.ValidatedEntities, Is.GreaterThan(0));
                Assert.That(entMan.EntityExists(actor), Is.True);
                Assert.That(entMan.EntityExists(transient), Is.True);
            });

            autosave.ToggleAutosave(sourceMapUid);
            Assert.That(autosave.IsAutosaving(sourceMapUid), Is.False);
        });

        Entity<MapComponent> loadedMap = default;
        HashSet<Entity<MapGridComponent>> loadedGrids = default!;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(savedPath, out var map, out var grids), Is.True);
            loadedMap = map!.Value;
            loadedGrids = grids!;
        });

        await server.WaitAssertion(() =>
        {
            var loadedGrid = loadedGrids.Single();
            var prototypes = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
                .Select(entry => entry.Uid)
                .Where(uid => entMan.GetComponent<TransformComponent>(uid).MapUid == loadedMap.Owner)
                .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
                .Where(id => id != null)
                .ToArray();
            var config = entMan.GetComponent<ZLevelMapComponent>(loadedMap.Owner);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MetaDataComponent>(loadedMap.Owner).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
                Assert.That(config.MinimumLevel, Is.Zero);
                Assert.That(config.MaximumLevel, Is.EqualTo(1));
                Assert.That(config.DefaultLevel, Is.Zero);
                Assert.That(mapSystem.GetExistingZLevelLayers(loadedGrid.Owner, loadedGrid.Comp),
                    Is.EquivalentTo(new[] { 0, 1 }));
                Assert.That(prototypes, Does.Contain("ZLevelFloorOpeningMarker"));
                Assert.That(prototypes, Does.Not.Contain("Crowbar"));
                Assert.That(prototypes, Does.Not.Contain("Wirecutter"));
                Assert.That(format.TryValidate(loadedMap.Owner, out var error), Is.True, error);
                Assert.That(entMan.EntityExists(sourceMapUid), Is.True,
                    "Autosave and validation must leave the initialized source map untouched.");
            });
        });

        await server.WaitPost(() =>
        {
            mapSystem.DeleteMap(loadedMap.Comp.MapId);
            mapSystem.DeleteMap(sourceMapId);
            resources.UserData.Delete(saveDirectory);
        });
    }
}
