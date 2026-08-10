// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelMovementTest : MovementTest
{
    [Test]
    public async Task ZLevelUpperFloorFallAndTileEventTest()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);
            var eastTile = new Vector2i(playerTile.X + 1, playerTile.Y);

            SEntMan.EnsureComponent<TestListenerComponent>(MapData.Grid);

            var zLevelPosition = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            zLevelPosition.ZLevel = 1;
            zLevelPosition.LocalZOffset = 0f;

            var zLevelKinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            zLevelKinematics.MaxStepDownDepth = 2;
            zLevelKinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 0), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(eastTile.X, eastTile.Y, 0), new Tile(1));

            Assert.That(zLevel.TryGetSupportTile(player, out var supportTile), Is.True);
            Assert.That(supportTile.GridIndices.Z, Is.EqualTo(1));
            Assert.That(zLevel.IsBodyActive(player), Is.False,
                "A supported body should leave the per-tick active set.");
        });

        await RunTicks(5);

        var listener = SEntMan.System<ZLevelTileChangedListenerSystem>();
        Assert.That(listener.Count(MapData.Grid), Is.EqualTo(1));

        await Move(DirectionFlag.East, 0.8f);
        await RunSeconds(0.8f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var zLevelPosition = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var zLevelKinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            var physics = SEntMan.GetComponent<PhysicsComponent>(player);

            Assert.Multiple(() =>
            {
                Assert.That(zLevelPosition.ZLevel, Is.EqualTo(0));
                Assert.That(zLevelPosition.LocalZOffset, Is.EqualTo(0f).Within(0.001f));
                Assert.That(zLevelKinematics.Grounded, Is.True);
                Assert.That(physics.BodyStatus, Is.EqualTo(BodyStatus.OnGround));
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
            });
        });
    }

    [Test]
    public async Task ZLevelTileRemovalWakesIndexedBody()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var tile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            var position = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            position.ZLevel = 1;
            position.LocalZOffset = 0f;
            var kinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            kinematics.MaxStepDownDepth = 2;
            kinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(1));

            Assert.That(boundaries.CanBodyPass(MapData.Grid, grid, tile, 1, 0), Is.False,
                "The closed boundary should be cached before support changes.");
            Assert.That(zLevel.IsBodyActive(player), Is.False);

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), Tile.Empty);

            Assert.That(boundaries.CanBodyPass(MapData.Grid, grid, tile, 1, 0), Is.True,
                "Removing the upper tile must invalidate the cached boundary.");
            Assert.That(zLevel.IsBodyActive(player), Is.True,
                "Removing support should wake the body indexed at the changed tile.");
        });

        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);

            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(0));
                Assert.That(position.LocalZOffset, Is.EqualTo(0f).Within(0.001f));
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
            });
        });
    }

    [Test]
    public async Task ZLevelTraversalApiMovesOnlyToDirectSupportedAdjacentFloors()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            var zLevelPosition = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            zLevelPosition.ZLevel = 0;
            zLevelPosition.LocalZOffset = 0f;

            var zLevelKinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            zLevelKinematics.MaxStepDownDepth = 2;
            zLevelKinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 0), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), new Tile(1));
            SEntMan.SpawnEntity("ZLevelStairsUp", map.GridTileToLocal(MapData.Grid, grid, playerTile));

            Assert.That(zLevel.TryTraverseAdjacentLevel(player, 1), Is.True);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(1));

            Assert.That(zLevel.TryTraverseAdjacentLevel(player, -1), Is.True);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(0));

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), Tile.Empty);
            Assert.That(zLevel.TryTraverseAdjacentLevel(player, 1), Is.False);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(0));
        });
    }

    [Test]
    public async Task ZLevelBoundaryChannelsResolveIndependently()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var xform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var tile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(1));

            Assert.That(boundaries.TryGetBoundary(MapData.Grid, grid, tile, 0, 1, out var defaultBoundary), Is.True);
            Assert.That(defaultBoundary.DefaultOpen, Is.False);
            Assert.That(defaultBoundary.IsOpen(ZLevelBoundaryChannels.Atmosphere), Is.False);

            var marker = SEntMan.SpawnEntity(null, playerCoords);
            var component = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            boundaries.SetBoundary(
                (marker, component),
                true,
                1,
                ZLevelBoundaryChannels.TraversalUp |
                ZLevelBoundaryChannels.Atmosphere |
                ZLevelBoundaryChannels.Visibility,
                ZLevelBoundaryChannels.Visibility);
            xform.AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            Assert.That(boundaries.TryGetBoundary(MapData.Grid, grid, tile, 0, 1, out var explicitBoundary), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(explicitBoundary.DefaultOpen, Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Body), Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.TraversalUp), Is.True);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.TraversalDown), Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Atmosphere), Is.True);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Visibility), Is.False,
                    "Forced-closed channels must win over forced-open channels.");
            });

            var chunkCount = grid.ChunkCount;
            for (var i = 0; i < SharedZLevelBoundarySystem.MaxCachedBoundaries + 32; i++)
            {
                Assert.That(boundaries.TryGetBoundary(
                    MapData.Grid,
                    grid,
                    new Vector2i(100_000 + i, 100_000),
                    0,
                    1,
                    out _), Is.True);
            }

            Assert.Multiple(() =>
            {
                Assert.That(boundaries.CachedBoundaryCount,
                    Is.LessThanOrEqualTo(SharedZLevelBoundarySystem.MaxCachedBoundaries));
                Assert.That(grid.ChunkCount, Is.EqualTo(chunkCount),
                    "Boundary queries over empty space must not allocate map chunks.");
            });
        });
    }

    [Test]
    public async Task ZLevelBodyOpeningRemovesTileSupport()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var xform = SEntMan.System<SharedTransformSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var tile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            var position = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            position.ZLevel = 1;
            position.LocalZOffset = 0f;
            var kinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            kinematics.MaxStepDownDepth = 2;
            kinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(1));

            var marker = SEntMan.SpawnEntity(null, playerCoords);
            SEntMan.EnsureComponent<ZLevelPositionComponent>(marker).ZLevel = 1;
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            boundaries.SetBoundary(
                (marker, boundary),
                true,
                -1,
                ZLevelBoundaryChannels.Body,
                ZLevelBoundaryChannels.None);
            xform.AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            Assert.That(zLevel.TryGetSupportTile(player, out var support), Is.True);
            Assert.That(support.GridIndices.Z, Is.EqualTo(0),
                "An explicit body opening must make the upper tile non-supporting.");
            Assert.That(zLevel.IsBodyActive(player), Is.True,
                "Changing support should wake only the indexed body at this tile.");
        });

        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);

            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(0));
                Assert.That(position.LocalZOffset, Is.EqualTo(0f).Within(0.001f));
                Assert.That(kinematics.Grounded, Is.True);
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
            });
        });
    }

    [Test]
    public async Task ZLevelTraversalStepTriggerRequiresSameFloor()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            var playerZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            playerZ.ZLevel = 1;
            playerZ.LocalZOffset = 0f;

            var playerKinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            playerKinematics.MaxStepDownDepth = 2;
            playerKinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 2), new Tile(1));

            var traversal = SEntMan.SpawnEntity(null, playerCoords);
            var traversalComp = SEntMan.EnsureComponent<ZLevelTraversalComponent>(traversal);
            traversalComp.ZOffset = 1;

            var attempt = new StepTriggerAttemptEvent { Source = traversal, Tripper = player };
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref attempt);
            Assert.That(attempt.Continue, Is.False);

            var triggered = new StepTriggeredOffEvent(traversal, player);
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref triggered);
            Assert.That(playerZ.ZLevel, Is.EqualTo(1));

            var traversalZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(traversal);
            traversalZ.ZLevel = 1;
            traversalZ.LocalZOffset = 0f;
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(traversal);
            boundaries.SetBoundary(
                (traversal, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Traversal,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(traversal, SEntMan.GetComponent<TransformComponent>(traversal));

            attempt = new StepTriggerAttemptEvent { Source = traversal, Tripper = player };
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref attempt);
            Assert.That(attempt.Continue, Is.True);

            triggered = new StepTriggeredOffEvent(traversal, player);
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref triggered);
            Assert.That(playerZ.ZLevel, Is.EqualTo(2));
        });
    }
}

public sealed class ZLevelTileChangedListenerSystem : TestListenerSystem<ZLevelTileChangedEvent>;
