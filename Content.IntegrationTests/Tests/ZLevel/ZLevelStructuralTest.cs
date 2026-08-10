// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server.ZLevel.Structural;
using Content.Shared.Maps;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelStructuralTest : GameTest
{
    [Test]
    public async Task SupportBridgeCancelsPendingCollapseAndRemovalCollapsesDeck()
    {
        var map = await Pair.CreateTestMap();
        var mapManager = Server.ResolveDependency<IMapManager>();
        var definitions = Server.ResolveDependency<ITileDefinitionManager>();
        var mapSystem = SEntMan.System<SharedMapSystem>();
        var structuralSystem = SEntMan.System<ZLevelStructuralSystem>();
        var zLevelSystem = SEntMan.System<SharedZLevelSystem>();
        var steel = (ContentTileDefinition) definitions["FloorSteel"];

        EntityUid gridUid = default;
        EntityUid supportUid = default;
        EntityUid upperWallUid = default;
        var upper = new ZLevelTileIndices(1, 0, 1);

        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(map.Grid);
            var grid = mapManager.CreateGridEntity(map.MapId);
            gridUid = grid.Owner;
            mapSystem.SetTile(grid.Owner, grid.Comp, new Vector2i(0, 0), new Tile(steel.TileId));
            mapSystem.SetTile(grid.Owner, grid.Comp, new Vector2i(1, 0), new Tile(steel.TileId));
            mapSystem.SetZLevelTile(grid.Owner, grid.Comp, upper, new Tile(steel.TileId));

            var structural = SEntMan.EnsureComponent<ZLevelStructuralGridComponent>(gridUid);
            structural.CollapseEnabled = false;

            SEntMan.SpawnEntity(
                "ZLevelStructuralCoreMarker",
                new EntityCoordinates(gridUid, new Vector2(0.5f, 0.5f)));
            supportUid = SEntMan.SpawnEntity(
                "ZLevelStructuralSupportMarker",
                new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var structural = SEntMan.GetComponent<ZLevelStructuralGridComponent>(gridUid);
            Assert.That(structural.Stability[upper], Is.EqualTo(8));
            Assert.That(structural.PendingCollapses, Is.Empty);
        });

        await Server.WaitAssertion(() =>
        {
            SEntMan.DeleteEntity(supportUid);
            structuralSystem.InvalidateGrid(gridUid);
        });
        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var structural = SEntMan.GetComponent<ZLevelStructuralGridComponent>(gridUid);
            Assert.That(structural.Stability.ContainsKey(upper), Is.False);
            Assert.That(mapSystem.GetZLevelTileRef(gridUid, SEntMan.GetComponent<MapGridComponent>(gridUid), upper).Tile.TypeId,
                Is.EqualTo(steel.TileId));

            structural.CollapseEnabled = true;
            structural.CollapseDelayMin = 30f;
            structural.CollapseDelayMax = 30f;
            structuralSystem.InvalidateGrid(gridUid);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var structural = SEntMan.GetComponent<ZLevelStructuralGridComponent>(gridUid);
            Assert.That(structural.PendingCollapses.ContainsKey(upper), Is.True);

            supportUid = SEntMan.SpawnEntity(
                "ZLevelStructuralSupportMarker",
                new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));
            structuralSystem.InvalidateGrid(gridUid);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
        {
            var structural = SEntMan.GetComponent<ZLevelStructuralGridComponent>(gridUid);
            Assert.That(structural.Stability[upper], Is.EqualTo(8));
            Assert.That(structural.PendingCollapses.ContainsKey(upper), Is.False);
            Assert.That(mapSystem.GetZLevelTileRef(gridUid, SEntMan.GetComponent<MapGridComponent>(gridUid), upper).Tile.TypeId,
                Is.EqualTo(steel.TileId));

            SEntMan.DeleteEntity(supportUid);
            upperWallUid = SEntMan.SpawnEntity(
                "WallSolid",
                new EntityCoordinates(gridUid, new Vector2(1.5f, 0.5f)));
            zLevelSystem.SetZLevelPosition(upperWallUid, upper.Z);
            structural.CollapseDelayMin = 0f;
            structural.CollapseDelayMax = 0f;
            structuralSystem.InvalidateGrid(gridUid);
        });

        await RunTicksSync(8);
        await Server.WaitAssertion(() =>
        {
            var tile = mapSystem.GetZLevelTileRef(
                gridUid,
                SEntMan.GetComponent<MapGridComponent>(gridUid),
                upper);
            Assert.That(tile.Tile.TypeId, Is.Not.EqualTo(steel.TileId));
            Assert.That(SEntMan.Deleted(upperWallUid), Is.True);
        });
    }
}
