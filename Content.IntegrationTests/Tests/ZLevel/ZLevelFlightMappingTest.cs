// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.Mapping;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Markdown.Mapping;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelFlightMappingTest : GameTest
{
    [TestPrototypes]
    private const string FlightMappingPrototype = @"
- type: entity
  id: ZLevelFlightMappingFixture
  components:
  - type: Physics
    bodyType: Dynamic
  - type: ZLevelFlight
    hoverOffset: 0.25
    verticalAcceleration: 6
    maximumVerticalSpeed: 1.5
  - type: ZLevelFlightControls
";

    [Test]
    public async Task AuthoredFlightCapabilityRoundTripsWithoutRuntimeState()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var flightSystem = entMan.System<SharedZLevelSystem>();
        var snapshots = entMan.System<MappingSnapshotSystem>();
        var loader = entMan.System<MapLoaderSystem>();

        EntityUid source = default;
        EntityUid sourceMap = default;
        MapId sourceMapId = default;
        MappingDataNode snapshot = default!;
        EntityUid? sourceToggleAction = null;

        await server.WaitAssertion(() =>
        {
            sourceMap = mapSystem.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(sourceMap, 0, 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            var grid = mapManager.CreateGridEntity(sourceMapId);
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, new ZLevelTileIndices(0, 0, 1), new Tile(1));
            source = entMan.SpawnEntity(
                "ZLevelFlightMappingFixture",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));

            mapSystem.InitializeMap(sourceMapId);
            var sourceControls = entMan.GetComponent<ZLevelFlightControlsComponent>(source);
            sourceToggleAction = sourceControls.ToggleActionEntity;
            Assert.That(sourceToggleAction, Is.Not.Null);
            Assert.That(flightSystem.TryStartFlight(source, 1), Is.EqualTo(ZLevelFlightResult.Success));
            Assert.That(entMan.GetComponent<ZLevelFlightComponent>(source).Active, Is.True);

            if (!snapshots.TryCreateMapSnapshot(
                    sourceMap,
                    out var createdSnapshot,
                    out var report,
                    out var error))
            {
                Assert.Fail(error);
            }

            snapshot = createdSnapshot!;
            Assert.Multiple(() =>
            {
                Assert.That(report.ExcludedRoots, Is.Zero);
                Assert.That(report.TransientComponents, Is.Zero);
                Assert.That(report.NormalizedReferences, Is.Zero);
            });

            var yaml = snapshot.ToYaml();
            Assert.Multiple(() =>
            {
                Assert.That(yaml, Does.Not.Contain("active:"));
                Assert.That(yaml, Does.Not.Contain("targetLocalZLevel:"));
                Assert.That(yaml, Does.Not.Contain("targetLocalZOffset:"));
                Assert.That(yaml, Does.Not.Contain("toggleActionEntity:"));
                Assert.That(yaml, Does.Not.Contain("moveUpActionEntity:"));
                Assert.That(yaml, Does.Not.Contain("moveDownActionEntity:"));
            });
        });

        LoadResult loaded = default!;
        await server.WaitAssertion(() =>
        {
            if (!loader.TryLoadGeneric(snapshot, "Z-level flight mapping round-trip", out var loadResult))
                Assert.Fail("The flight mapping snapshot failed to load.");

            loaded = loadResult!;
            var loadedEntity = loaded.Entities.Single(uid =>
                entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID == "ZLevelFlightMappingFixture");
            var loadedFlight = entMan.GetComponent<ZLevelFlightComponent>(loadedEntity);
            var loadedControls = entMan.GetComponent<ZLevelFlightControlsComponent>(loadedEntity);

            Assert.Multiple(() =>
            {
                Assert.That(loadedFlight.HoverOffset, Is.EqualTo(0.25f));
                Assert.That(loadedFlight.VerticalAcceleration, Is.EqualTo(6f));
                Assert.That(loadedFlight.MaximumVerticalSpeed, Is.EqualTo(1.5f));
                Assert.That(loadedFlight.Active, Is.False);
                Assert.That(loadedFlight.TargetLocalZLevel, Is.Zero);
                Assert.That(loadedFlight.TargetLocalZOffset, Is.EqualTo(0.5f));
                Assert.That(loadedControls.ToggleActionEntity, Is.Not.Null);
                Assert.That(loadedControls.MoveUpActionEntity, Is.Not.Null);
                Assert.That(loadedControls.MoveDownActionEntity, Is.Not.Null);
                Assert.That(loadedControls.ToggleActionEntity, Is.Not.EqualTo(sourceToggleAction));
                Assert.That(entMan.GetComponent<MetaDataComponent>(loaded.Maps.Single().Owner).EntityLifeStage,
                    Is.EqualTo(EntityLifeStage.MapInitialized));
            });
        });

        await server.WaitPost(() =>
        {
            loader.Delete(loaded);
            mapSystem.DeleteMap(sourceMapId);
        });
    }
}
