// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Movement;
using Content.Shared.StepTrigger.Systems;
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

            Assert.That(zLevel.TryTraverseAdjacentLevel(player, 1, overrideBoundaryBlock: true), Is.True);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(1));

            Assert.That(zLevel.TryTraverseAdjacentLevel(player, -1, overrideBoundaryBlock: true), Is.True);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(0));

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), Tile.Empty);
            Assert.That(zLevel.TryTraverseAdjacentLevel(player, 1, overrideBoundaryBlock: true), Is.False);
            Assert.That(zLevel.GetZLevel(player), Is.EqualTo(0));
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
            traversalComp.OverridesBoundaryBlock = true;

            var attempt = new StepTriggerAttemptEvent { Source = traversal, Tripper = player };
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref attempt);
            Assert.That(attempt.Continue, Is.False);

            var triggered = new StepTriggeredOffEvent(traversal, player);
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref triggered);
            Assert.That(playerZ.ZLevel, Is.EqualTo(1));

            var traversalZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(traversal);
            traversalZ.ZLevel = 1;
            traversalZ.LocalZOffset = 0f;

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
