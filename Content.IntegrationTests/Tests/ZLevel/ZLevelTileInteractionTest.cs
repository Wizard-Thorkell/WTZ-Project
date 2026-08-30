// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelTileInteractionTest : InteractionTest
{
    private static readonly ProtoId<ReagentPrototype> Carpetium = "Carpetium";
    private static readonly ProtoId<ReagentPrototype> ChlorineTrifluoride = "ChlorineTrifluoride";

    [TestCase("FloorTileItemZLevelGrate", "ZLevelGrate")]
    [TestCase("FloorTileItemZLevelShaft", "FloorZLevelShaft")]
    public async Task VerticalSurfaceFloorItemsPlaceOnTheUsersZLevel(
        string itemPrototype,
        string expectedTile)
    {
        var targetCoordinates = SEntMan.GetCoordinates(TargetCoords);
        Vector2i targetIndices = default;
        Tile lowerTile = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerIndices = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            targetIndices = map.TileIndicesFor(MapData.Grid, grid, targetCoordinates);
            var plating = (ContentTileDefinition) TileMan[Plating];
            var steel = (ContentTileDefinition) TileMan[Floor];
            lowerTile = new Tile(steel.TileId);

            map.SetTile(MapData.Grid, grid, targetIndices, lowerTile);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(playerIndices.X, playerIndices.Y, 1),
                new Tile(plating.TileId));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1),
                new Tile(plating.TileId));
            Assert.That(zLevel.SetZLevelPosition(SPlayer, 1), Is.True);
        });
        await RunTicks(3);

        await PlaceInHands(itemPrototype);
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var upper = map.GetZLevelTileRef(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1));

            Assert.Multiple(() =>
            {
                Assert.That(map.GetTileRef(MapData.Grid, grid, targetIndices).Tile, Is.EqualTo(lowerTile));
                Assert.That(((ContentTileDefinition) TileMan[upper.Tile.TypeId]).ID, Is.EqualTo(expectedTile));
            });
        });
    }

    [Test]
    public async Task SupportedChemicalTileReactionsUseTheExplicitZLevel()
    {
        Vector2i targetIndices = default;
        Tile lowerTile = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            targetIndices = map.TileIndicesFor(MapData.Grid, grid, SEntMan.GetCoordinates(TargetCoords));
            var steel = (ContentTileDefinition) TileMan[Floor];
            lowerTile = new Tile(steel.TileId);
            map.SetTile(MapData.Grid, grid, targetIndices, lowerTile);

            var upper = new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1);
            map.SetZLevelTile(MapData.Grid, grid, upper, new Tile(steel.TileId));
            var upperTile = map.GetZLevelTileRef(MapData.Grid, grid, upper);

            ProtoMan.Index(ChlorineTrifluoride)
                .ReactionTile(upperTile, FixedPoint2.New(1), SEntMan, null);
            ProtoMan.Index(Carpetium)
                .ReactionTile(upperTile, FixedPoint2.New(1), SEntMan, null);
        });
        await RunTicks(3);

        var carpet = await FindEntity("Carpet");
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var upper = map.GetZLevelTileRef(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1));

            Assert.Multiple(() =>
            {
                Assert.That(map.GetTileRef(MapData.Grid, grid, targetIndices).Tile, Is.EqualTo(lowerTile));
                Assert.That(((ContentTileDefinition) TileMan[upper.Tile.TypeId]).ID, Is.EqualTo(Plating));
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(carpet), Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task SpawnAfterInteractUsesTheUsersZLevel()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerIndices = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            var targetIndices = map.TileIndicesFor(MapData.Grid, grid, SEntMan.GetCoordinates(TargetCoords));
            var plating = (ContentTileDefinition) TileMan[Plating];

            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(playerIndices.X, playerIndices.Y, 1),
                new Tile(plating.TileId));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1),
                new Tile(plating.TileId));
            Assert.That(zLevel.SetZLevelPosition(SPlayer, 1), Is.True);
        });
        await RunTicks(3);

        await PlaceInHands("InflatableWallStack");
        await Interact(null, TargetCoords);

        var wall = await FindEntity("InflatableWall");
        await Server.WaitAssertion(() =>
        {
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            Assert.That(zLevel.GetZLevel(wall), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CrowbarDeconstructsTheUsersZLevelOnly()
    {
        var targetCoordinates = SEntMan.GetCoordinates(TargetCoords);
        Vector2i targetIndices = default;
        Tile lowerTile = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerIndices = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            targetIndices = map.TileIndicesFor(MapData.Grid, grid, targetCoordinates);

            var plating = (ContentTileDefinition) TileMan[Plating];
            var steel = (ContentTileDefinition) TileMan[Floor];
            lowerTile = new Tile(steel.TileId);

            map.SetTile(MapData.Grid, grid, targetIndices, lowerTile);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(playerIndices.X, playerIndices.Y, 1),
                new Tile(plating.TileId));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1),
                new Tile(steel.TileId));

            Assert.That(zLevel.SetZLevelPosition(SPlayer, 1), Is.True);
        });
        await RunTicks(3);

        await PlaceInHands(Pry);
        await Interact(null, TargetCoords);
        var floorTile = await FindEntity("FloorTileItemSteel");

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var upper = map.GetZLevelTileRef(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1));

            Assert.Multiple(() =>
            {
                Assert.That(map.GetTileRef(MapData.Grid, grid, targetIndices).Tile, Is.EqualTo(lowerTile),
                    "Prying an upper floor must not modify the overlapping base tile.");
                Assert.That(((ContentTileDefinition) TileMan[upper.Tile.TypeId]).ID, Is.EqualTo(Plating));
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(floorTile), Is.EqualTo(1),
                    "The deconstructed floor item must remain on the pried layer.");
            });
        });
    }
}

[TestFixture]
public sealed class ZLevelAdminGhostTileInteractionTest : InteractionTest
{
    protected override string PlayerPrototype => "AdminObserver";

    [Test]
    public async Task AdminGhostCrowbarDeconstructsItsCurrentZLevelOnly()
    {
        var targetCoordinates = SEntMan.GetCoordinates(TargetCoords);
        Vector2i targetIndices = default;
        Tile lowerTile = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerIndices = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetComponent<TransformComponent>(SPlayer).Coordinates);
            targetIndices = map.TileIndicesFor(MapData.Grid, grid, targetCoordinates);

            var plating = (ContentTileDefinition) TileMan[Plating];
            var steel = (ContentTileDefinition) TileMan[Floor];
            lowerTile = new Tile(steel.TileId);

            map.SetTile(MapData.Grid, grid, targetIndices, lowerTile);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(playerIndices.X, playerIndices.Y, 1),
                new Tile(plating.TileId));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1),
                new Tile(steel.TileId));
            Assert.That(zLevel.SetZLevelPosition(SPlayer, 1), Is.True);
        });
        await RunTicks(3);

        await PlaceInHands(Pry);
        await Interact(null, TargetCoords);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var upper = map.GetZLevelTileRef(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetIndices.X, targetIndices.Y, 1));

            Assert.Multiple(() =>
            {
                Assert.That(map.GetTileRef(MapData.Grid, grid, targetIndices).Tile, Is.EqualTo(lowerTile));
                Assert.That(((ContentTileDefinition) TileMan[upper.Tile.TypeId]).ID, Is.EqualTo(Plating));
            });
        });
    }
}
