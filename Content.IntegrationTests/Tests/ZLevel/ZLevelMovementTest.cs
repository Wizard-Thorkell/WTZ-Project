// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.Power.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gravity;
using Content.Shared.Maps;
using Content.Shared.StepTrigger.Components;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelMovementTest : MovementTest
{
    [TestPrototypes]
    private const string GravityPrototypes = @"
- type: entity
  id: ZLevelGravityGeneratorDummy
  components:
  - type: GravityGenerator
  - type: PowerCharge
    windowTitle: gravity-generator-window-title
    idlePower: 50
    chargeRate: 1000000000
    activePower: 500
  - type: ApcPowerReceiver
  - type: UserInterface
";

    [Test]
    public async Task ZLevelSystemResolvesInheritedPosition()
    {
        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            var parent = SEntMan.SpawnEntity(null, coordinates);
            var child = SEntMan.SpawnEntity(null, coordinates);

            Assert.That(zLevel.SetZLevelPosition(parent, 3), Is.True);
            transform.SetParent(child, parent);

            Assert.That(zLevel.GetZLevel(child), Is.EqualTo(3));
            Assert.That(zLevel.IsOnZLevel(child, 3), Is.True);
        });
    }

    [Test]
    public async Task ZLevelTileHistoryIsIndependentFromBaseLayer()
    {
        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var tiles = SEntMan.System<TileSystem>();
            var definitions = Server.ResolveDependency<ITileDefinitionManager>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            var xy = map.TileIndicesFor(MapData.Grid, grid, coordinates);
            var upperIndices = new ZLevelTileIndices(xy.X, xy.Y, 1);
            var plating = (ContentTileDefinition) definitions["Plating"];
            var steel = (ContentTileDefinition) definitions["FloorSteel"];

            map.SetTile(MapData.Grid, grid, xy, new Tile(plating.TileId));
            map.SetZLevelTile(MapData.Grid, grid, upperIndices, new Tile(plating.TileId));

            var upper = map.GetZLevelTileRef(MapData.Grid, grid, upperIndices);
            Assert.That(tiles.ReplaceZLevelTile(upper, steel, MapData.Grid, grid), Is.True);

            upper = map.GetZLevelTileRef(MapData.Grid, grid, upperIndices);
            Assert.That(tiles.ReplaceZLevelTile(upper, plating, MapData.Grid, grid), Is.True);

            var history = SEntMan.GetComponent<TileHistoryComponent>(MapData.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(history.ZLevelHistory[upperIndices].History, Has.Count.EqualTo(1));
                Assert.That(history.ChunkHistory, Is.Empty);
            });

            upper = map.GetZLevelTileRef(MapData.Grid, grid, upperIndices);
            Assert.That(tiles.DeconstructZLevelTile(upper, spawnItem: false), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(map.GetZLevelTileRef(MapData.Grid, grid, upperIndices).Tile.TypeId, Is.EqualTo(steel.TileId));
                Assert.That(map.GetTileRef(MapData.Grid, grid, xy).Tile.TypeId, Is.EqualTo(plating.TileId));
                Assert.That(history.ZLevelHistory, Is.Empty);
            });
        });
    }

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
    public async Task NativeZLevelMapWithoutGravitySourceDoesNotFall()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var zLevelMap = SEntMan.System<SharedZLevelMapSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            zLevelMap.Configure(
                MapData.MapUid,
                -2,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), Tile.Empty);
            Assert.That(zLevel.SetZLevel(player, 1), Is.True);
        });

        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(1));
                Assert.That(position.LocalZOffset, Is.EqualTo(0f).Within(0.001f));
                Assert.That(kinematics.VerticalVelocity, Is.EqualTo(0f).Within(0.001f));
                Assert.That(SEntMan.System<SharedZLevelSystem>().IsBodyActive(player), Is.False);
            });
        });
    }

    [Test]
    public async Task ConnectedGravityGeneratorPullsTowardItsLevelOnly()
    {
        EntityUid generator = default;
        Vector2i playerTile = default;
        Vector2i disconnectedTile = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevelMap = SEntMan.System<SharedZLevelMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);
            disconnectedTile = new Vector2i(playerTile.X + 20, playerTile.Y);

            zLevelMap.Configure(
                MapData.MapUid,
                -2,
                2,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);
            map.SetTile(MapData.Grid, grid, disconnectedTile, new Tile(1));

            generator = SEntMan.SpawnEntity("ZLevelGravityGeneratorDummy", playerCoords);
            SEntMan.GetComponent<ApcPowerReceiverComponent>(generator).NeedsPower = false;
        });

        await RunTicks(5);

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var gravity = SEntMan.System<SharedZLevelGravitySystem>();
            var zLevel = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var generatorComponent = SEntMan.GetComponent<GravityGeneratorComponent>(generator);

            Assert.Multiple(() =>
            {
                Assert.That(generatorComponent.GravityActive, Is.True);
                Assert.That(gravity.TryGetGravityTarget(MapData.Grid, grid, playerTile, 2, out var connectedTarget), Is.True);
                Assert.That(connectedTarget, Is.EqualTo(0));
                Assert.That(gravity.TryGetGravityTarget(MapData.Grid, grid, disconnectedTile, 2, out _), Is.False,
                    "A disconnected asteroid island must remain outside the station gravity field.");
            });

            var player = ToServer(Player);
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), Tile.Empty);
            Assert.That(zLevel.SetZLevel(player, 1), Is.True);
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
            });
        });

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevel(player, -1), Is.True);
        });

        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            var position = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            var kinematics = SEntMan.GetComponent<ZLevelKinematicsComponent>(player);
            Assert.Multiple(() =>
            {
                Assert.That(position.ZLevel, Is.EqualTo(-1));
                Assert.That(position.LocalZOffset, Is.GreaterThan(0.99f),
                    "A body below the generator plane should be pulled against the boundary above it.");
                Assert.That(kinematics.Grounded, Is.True);
            });
        });
    }

    [Test]
    public void GravitySolverDoesNotCrossEmptySpace()
    {
        var station = new HashSet<ZLevelTileIndices>
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(1, 0, 1),
            new(2, 0, 1),
        };
        var asteroid = new ZLevelTileIndices(10, 0, 0);
        station.Add(asteroid);

        var assignments = ZLevelGravitySolver.Solve(
            station,
            [new ZLevelGravitySeed(new ZLevelTileIndices(0, 0, 0), 0, EntityUid.FirstUid)]);

        Assert.Multiple(() =>
        {
            Assert.That(assignments, Does.ContainKey(new ZLevelTileIndices(2, 0, 1)));
            Assert.That(assignments, Does.Not.ContainKey(asteroid));
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
            var stairs = SEntMan.SpawnEntity("ZLevelStairsUp", map.GridTileToLocal(MapData.Grid, grid, playerTile));
            Assert.That(SEntMan.GetComponent<StepTriggerComponent>(stairs).StepOn, Is.True,
                "Z-level traversal prototypes must emit distinct step-on and step-off events.");

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
                ZLevelBoundaryChannels.Visibility |
                ZLevelBoundaryChannels.Projectile,
                ZLevelBoundaryChannels.Visibility |
                ZLevelBoundaryChannels.Explosion);
            xform.AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            Assert.That(boundaries.TryGetBoundary(MapData.Grid, grid, tile, 0, 1, out var explicitBoundary), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(explicitBoundary.DefaultOpen, Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Body), Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.TraversalUp), Is.True);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.TraversalDown), Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Atmosphere), Is.True);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Projectile), Is.True);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Explosion), Is.False);
                Assert.That(explicitBoundary.IsOpen(ZLevelBoundaryChannels.Visibility), Is.False,
                    "Forced-closed channels must win over forced-open channels.");
            });

            var chunkCount = grid.ChunkCount;
            for (var i = 0; i < boundaries.BoundaryCacheCapacity + 32; i++)
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
                    Is.LessThanOrEqualTo(boundaries.BoundaryCacheCapacity));
                Assert.That(grid.ChunkCount, Is.EqualTo(chunkCount),
                    "Boundary queries over empty space must not allocate map chunks.");
            });
        });
    }

    [Test]
    public async Task ZLevelVisibilityIsOpeningAwareAndBounded()
    {
        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
            var xform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var tile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), new Tile(1));
            Assert.That(visibility.IsTileVisibleFrom(MapData.Grid, grid, tile, 0, -1), Is.False);

            var marker = SEntMan.SpawnEntity(null, playerCoords);
            SEntMan.EnsureComponent<ZLevelPositionComponent>(marker).ZLevel = 0;
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            boundaries.SetBoundary(
                (marker, boundary),
                true,
                -1,
                ZLevelBoundaryChannels.Visibility,
                ZLevelBoundaryChannels.None);
            xform.AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            Assert.Multiple(() =>
            {
                Assert.That(visibility.IsTileVisibleFrom(MapData.Grid, grid, tile, 0, -1), Is.True);
                Assert.That(visibility.IsTileVisibleFrom(
                    MapData.Grid,
                    grid,
                    tile,
                    0,
                    -visibility.MaxVisibleLevelDistance), Is.True);
                Assert.That(visibility.IsTileVisibleFrom(
                    MapData.Grid,
                    grid,
                    tile,
                    0,
                    -visibility.MaxVisibleLevelDistance - 1), Is.False);
                Assert.That(visibility.IsTileVisibleFrom(MapData.Grid, grid, tile, 0, 1), Is.False,
                    "Normal gameplay visibility must not reveal floors above the viewer.");
            });
        });
    }

    [Test]
    public async Task ZLevelPvsTracksVisibilityOpenings()
    {
        NetEntity target = default;
        EntityUid? clientTarget = default;

        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, true));
        await Server.WaitPost(() =>
        {
            target = SEntMan.GetNetEntity(SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(TargetCoords)));
        });
        await RunTicks(10);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(target, out clientTarget), Is.True);
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(clientTarget!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False);
        });

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var xform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var targetCoords = SEntMan.GetCoordinates(TargetCoords);
            var targetTile = map.TileIndicesFor(MapData.Grid, grid, targetCoords);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);

            Assert.That(xform.SetZLevelFrameOrigin(MapData.Grid, 5), Is.True);
            SEntMan.EnsureComponent<ZLevelPositionComponent>(SPlayer).ZLevel = 1;
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(targetTile.X, targetTile.Y, 1), new Tile(1));

            var targetUid = SEntMan.GetEntity(target);
            Assert.That(SEntMan.System<SharedZLevelVisibilitySystem>()
                .IsEntityVisibleFrom(targetUid, MapId, 6), Is.False);
            Assert.That(SEntMan.System<EntityLookupSystem>()
                .GetEntitiesInRange(Transform.GetMapCoordinates(targetUid), 1f), Does.Contain(targetUid));
            SEntMan.System<ZLevelPvsSystem>().RefreshSession(ServerSession);
        });
        await RunSeconds(0.5f);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(clientTarget!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.True,
                "A closed floor must remove lower-floor entities from normal spatial PVS.");
        });

        EntityUid opening = default;
        await Server.WaitPost(() =>
        {
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var xform = SEntMan.System<SharedTransformSystem>();
            opening = SEntMan.SpawnEntity(null, SEntMan.GetCoordinates(TargetCoords));
            SEntMan.EnsureComponent<ZLevelPositionComponent>(opening).ZLevel = 1;
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(opening);
            boundaries.SetBoundary(
                (opening, boundary),
                true,
                -1,
                ZLevelBoundaryChannels.Visibility,
                ZLevelBoundaryChannels.None);
            xform.AnchorEntity(opening, SEntMan.GetComponent<TransformComponent>(opening));
        });
        await RunSeconds(0.5f);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(clientTarget!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.False,
                "Opening the boundary must restore lower-floor entities to PVS.");
        });

        await Server.WaitPost(() => SEntMan.DeleteEntity(opening));
        await RunSeconds(0.5f);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.GetComponent<MetaDataComponent>(clientTarget!.Value).Flags.HasFlag(MetaDataFlags.Detached), Is.True,
                "Closing the boundary again must evict lower-floor entities.");
        });
    }

    [Test]
    public async Task ZLevelPvsKeepsLowerFloorLightingDependencies()
    {
        NetEntity plainTarget = default;
        NetEntity lightTarget = default;
        NetEntity occluderTarget = default;
        EntityUid? clientPlain = default;
        EntityUid? clientLight = default;
        EntityUid? clientOccluder = default;

        await Server.WaitPost(() => Server.CfgMan.SetCVar(CVars.NetPVS, true));
        await Server.WaitPost(() =>
        {
            var coordinates = SEntMan.GetCoordinates(TargetCoords);
            var plain = SEntMan.SpawnEntity(null, coordinates);
            var light = SEntMan.SpawnEntity(null, coordinates);
            var occluder = SEntMan.SpawnEntity(null, coordinates);
            SEntMan.EnsureComponent<PointLightComponent>(light);
            SEntMan.EnsureComponent<OccluderComponent>(occluder);
            plainTarget = SEntMan.GetNetEntity(plain);
            lightTarget = SEntMan.GetNetEntity(light);
            occluderTarget = SEntMan.GetNetEntity(occluder);
        });
        await RunTicks(10);

        await Client.WaitAssertion(() =>
        {
            Assert.That(CEntMan.TryGetEntity(plainTarget, out clientPlain), Is.True);
            Assert.That(CEntMan.TryGetEntity(lightTarget, out clientLight), Is.True);
            Assert.That(CEntMan.TryGetEntity(occluderTarget, out clientOccluder), Is.True);
        });

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerTile = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetCoordinates(PlayerCoords));
            var targetTile = map.TileIndicesFor(
                MapData.Grid,
                grid,
                SEntMan.GetCoordinates(TargetCoords));

            Assert.That(zLevels.SetZLevelPosition(SPlayer, 1), Is.True);
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(playerTile.X, playerTile.Y, 1),
                new Tile(1));
            map.SetZLevelTile(
                MapData.Grid,
                grid,
                new ZLevelTileIndices(targetTile.X, targetTile.Y, 1),
                new Tile(1));
            SEntMan.System<ZLevelPvsSystem>().RefreshSession(ServerSession);
        });
        await RunSeconds(0.5f);

        await Client.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(IsDetached(clientPlain), Is.True,
                    "Ordinary lower-floor entities must remain hidden behind a closed floor.");
                Assert.That(IsDetached(clientLight), Is.False,
                    "Lower-floor lights remain PVS inputs for aperture projection.");
                Assert.That(IsDetached(clientOccluder), Is.False,
                    "Lower-floor occluders remain PVS inputs for projected shadows.");
            });
        });

        bool IsDetached(EntityUid? uid)
        {
            return CEntMan.GetComponent<MetaDataComponent>(uid!.Value).Flags
                .HasFlag(MetaDataFlags.Detached);
        }
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
    public async Task ZLevelTraversalGraphIndexesFloorsAndConnectedProfiles()
    {
        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var originCoordinates = SEntMan.GetCoordinates(PlayerCoords);
            var originTile = map.TileIndicesFor(MapData.Grid, grid, originCoordinates);
            var adjacentTile = originTile + new Vector2i(1, 0);
            var ladderTile = originTile + new Vector2i(2, 0);
            var adjacentCoordinates = map.GridTileToLocal(MapData.Grid, grid, adjacentTile);
            var ladderCoordinates = map.GridTileToLocal(MapData.Grid, grid, ladderTile);

            var origin = SEntMan.SpawnEntity(null, originCoordinates);
            var originTraversal = SEntMan.EnsureComponent<ZLevelTraversalComponent>(origin);
            originTraversal.ZOffset = 1;
            graph.RefreshTraversal(origin);

            var adjacent = SEntMan.SpawnEntity(null, adjacentCoordinates);
            var adjacentTraversal = SEntMan.EnsureComponent<ZLevelTraversalComponent>(adjacent);
            adjacentTraversal.ZOffset = 1;
            graph.RefreshTraversal(adjacent);

            var ladder = SEntMan.SpawnEntity(null, ladderCoordinates);
            var ladderTraversal = SEntMan.EnsureComponent<ZLevelTraversalComponent>(ladder);
            ladderTraversal.ZOffset = 1;
            ladderTraversal.Kind = ZLevelTraversalKind.Ladder;
            graph.RefreshTraversal(ladder);

            var upper = SEntMan.SpawnEntity(null, originCoordinates);
            var upperTraversal = SEntMan.EnsureComponent<ZLevelTraversalComponent>(upper);
            upperTraversal.ZOffset = 1;
            var upperPosition = SEntMan.EnsureComponent<ZLevelPositionComponent>(upper);
            upperPosition.ZLevel = 1;
            graph.RefreshTraversal(upper);

            var results = new List<EntityUid>();
            graph.GetTraversalsAt(MapData.Grid, originTile, 0, results);
            Assert.That(results, Is.EquivalentTo(new[] { origin }));
            graph.GetTraversalsAt(MapData.Grid, originTile, 1, results);
            Assert.That(results, Is.EquivalentTo(new[] { upper }));

            Assert.Multiple(() =>
            {
                Assert.That(graph.TryGetConnectedTraversal(origin, adjacentTile, out var connected), Is.True);
                Assert.That(connected, Is.EqualTo(adjacent));
                Assert.That(graph.TryGetConnectedTraversal(origin, ladderTile, out _), Is.False,
                    "Different traversal kinds must not share a continuation timer or navigation region.");
                Assert.That(graph.TryGetConnectedTraversal(origin, originTile, out connected), Is.True);
                Assert.That(connected, Is.EqualTo(origin));
            });

            for (var i = 0; i < 32; i++)
                graph.TryGetConnectedTraversal(origin, adjacentTile, out _);

            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var allConnected = true;
            for (var i = 0; i < 256; i++)
                allConnected &= graph.TryGetConnectedTraversal(origin, adjacentTile, out _);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.Multiple(() =>
            {
                Assert.That(allConnected, Is.True);
                Assert.That(allocated, Is.LessThanOrEqualTo(256),
                    "Warmed connected-region queries should reuse traversal buffers.");
            });

            var revision = graph.TopologyRevision;
            transform.SetCoordinates(adjacent, ladderCoordinates);
            graph.GetTraversalsAt(MapData.Grid, adjacentTile, 0, results);
            Assert.Multiple(() =>
            {
                Assert.That(results, Is.Empty);
                Assert.That(graph.TopologyRevision, Is.GreaterThan(revision));
            });

            SEntMan.DeleteEntity(origin);
            Assert.That(graph.NodeCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ZLevelTraversalGraphResolvesAndInvalidatesDirectedEdges()
    {
        await Server.WaitAssertion(() =>
        {
            var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var map = SEntMan.System<SharedMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var coordinates = SEntMan.GetCoordinates(PlayerCoords);
            var tile = map.TileIndicesFor(MapData.Grid, grid, coordinates);
            var upper = new ZLevelTileIndices(tile.X, tile.Y, 1);
            map.SetZLevelTile(MapData.Grid, grid, upper, new Tile(1));

            var stairs = SEntMan.SpawnEntity(
                "ZLevelStairsUp",
                map.GridTileToLocal(MapData.Grid, grid, tile));
            graph.RefreshTraversal(stairs);

            Assert.That(graph.TryResolveEdge(stairs, out var edge), Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.Multiple(() =>
            {
                Assert.That(edge.Source.LocalZ, Is.Zero);
                Assert.That(edge.Source.WorldZ, Is.Zero);
                Assert.That(edge.Destination.LocalZ, Is.EqualTo(1));
                Assert.That(edge.Destination.WorldZ, Is.EqualTo(1));
                Assert.That(edge.ZOffset, Is.EqualTo(1));
                Assert.That(edge.Cost, Is.EqualTo(4f));
            });

            var environmentRevision = graph.EnvironmentRevision;
            map.SetZLevelTile(MapData.Grid, grid, upper, Tile.Empty);
            Assert.Multiple(() =>
            {
                Assert.That(graph.EnvironmentRevision, Is.GreaterThan(environmentRevision));
                Assert.That(graph.TryResolveEdge(stairs, out _),
                    Is.EqualTo(ZLevelTraversalEdgeStatus.MissingDestinationSupport));
            });

            map.SetZLevelTile(MapData.Grid, grid, upper, new Tile(1));
            var boundary = SEntMan.GetComponent<ZLevelBoundaryComponent>(stairs);
            boundaries.SetBoundary(
                (stairs, boundary),
                true,
                1,
                ZLevelBoundaryChannels.None,
                ZLevelBoundaryChannels.TraversalUp);
            Assert.That(graph.TryResolveEdge(stairs, out _),
                Is.EqualTo(ZLevelTraversalEdgeStatus.ClosedBoundary));

            boundaries.SetBoundary(
                (stairs, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Traversal,
                ZLevelBoundaryChannels.None);
            transform.SetZLevelFrameOrigin(MapData.Grid, 6);
            Assert.That(graph.TryResolveEdge(stairs, out edge), Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.Multiple(() =>
            {
                Assert.That(edge.Source.WorldZ, Is.EqualTo(6));
                Assert.That(edge.Destination.WorldZ, Is.EqualTo(7));
            });
        });
    }

    [Test]
    public async Task ZLevelTraversalStepTriggerRequiresSameFloor()
    {
        EntityUid traversal = default;
        EntityUid destinationTraversal = default;
        EntityCoordinates stairsCoordinates = default;
        EntityCoordinates adjacentStairsCoordinates = default;
        EntityCoordinates offStairsCoordinates = default;

        await Server.WaitPost(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var player = ToServer(Player);
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var playerCoords = SEntMan.GetCoordinates(PlayerCoords);
            var playerTile = map.TileIndicesFor(MapData.Grid, grid, playerCoords);
            var adjacentTile = new Vector2i(playerTile.X + 1, playerTile.Y);
            var offStairsTile = new Vector2i(playerTile.X - 1, playerTile.Y);
            stairsCoordinates = map.GridTileToLocal(MapData.Grid, grid, playerTile);
            adjacentStairsCoordinates = map.GridTileToLocal(MapData.Grid, grid, adjacentTile);
            offStairsCoordinates = map.GridTileToLocal(MapData.Grid, grid, offStairsTile);

            var playerZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(player);
            playerZ.ZLevel = 1;
            playerZ.LocalZOffset = 0f;

            var playerKinematics = SEntMan.EnsureComponent<ZLevelKinematicsComponent>(player);
            playerKinematics.MaxStepDownDepth = 2;
            playerKinematics.VerticalVelocity = 0f;

            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 1), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(playerTile.X, playerTile.Y, 2), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(adjacentTile.X, adjacentTile.Y, 1), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(adjacentTile.X, adjacentTile.Y, 2), new Tile(1));
            map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(offStairsTile.X, offStairsTile.Y, 1), new Tile(1));

            traversal = SEntMan.SpawnEntity(null, playerCoords);
            var traversalComp = SEntMan.EnsureComponent<ZLevelTraversalComponent>(traversal);
            traversalComp.ZOffset = 1;

            var attempt = new StepTriggerAttemptEvent { Source = traversal, Tripper = player };
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref attempt);
            Assert.That(attempt.Continue, Is.False);

            var triggered = new StepTriggeredOnEvent(traversal, player);
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

            var adjacentTraversal = SEntMan.SpawnEntity(null, adjacentStairsCoordinates);
            var adjacentTraversalComp = SEntMan.EnsureComponent<ZLevelTraversalComponent>(adjacentTraversal);
            adjacentTraversalComp.ZOffset = 1;
            var adjacentTraversalZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(adjacentTraversal);
            adjacentTraversalZ.ZLevel = 1;
            var adjacentBoundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(adjacentTraversal);
            boundaries.SetBoundary(
                (adjacentTraversal, adjacentBoundary),
                true,
                1,
                ZLevelBoundaryChannels.Traversal,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(adjacentTraversal, SEntMan.GetComponent<TransformComponent>(adjacentTraversal));

            var oppositeTraversal = SEntMan.SpawnEntity(null, offStairsCoordinates);
            var oppositeTraversalComp = SEntMan.EnsureComponent<ZLevelTraversalComponent>(oppositeTraversal);
            oppositeTraversalComp.ZOffset = -1;
            var oppositeTraversalZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(oppositeTraversal);
            oppositeTraversalZ.ZLevel = 1;
            var oppositeBoundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(oppositeTraversal);
            boundaries.SetBoundary(
                (oppositeTraversal, oppositeBoundary),
                true,
                -1,
                ZLevelBoundaryChannels.Traversal,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(oppositeTraversal, SEntMan.GetComponent<TransformComponent>(oppositeTraversal));

            destinationTraversal = SEntMan.SpawnEntity(null, adjacentStairsCoordinates);
            var destinationTraversalComp = SEntMan.EnsureComponent<ZLevelTraversalComponent>(destinationTraversal);
            destinationTraversalComp.ZOffset = -1;
            var destinationTraversalZ = SEntMan.EnsureComponent<ZLevelPositionComponent>(destinationTraversal);
            destinationTraversalZ.ZLevel = 2;
            var destinationBoundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(destinationTraversal);
            boundaries.SetBoundary(
                (destinationTraversal, destinationBoundary),
                true,
                -1,
                ZLevelBoundaryChannels.Traversal,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(destinationTraversal, SEntMan.GetComponent<TransformComponent>(destinationTraversal));

            attempt = new StepTriggerAttemptEvent { Source = traversal, Tripper = player };
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref attempt);
            Assert.That(attempt.Continue, Is.True);

            triggered = new StepTriggeredOnEvent(traversal, player);
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref triggered);
            Assert.Multiple(() =>
            {
                Assert.That(playerZ.ZLevel, Is.EqualTo(1), "Traversal must wait for its DoAfter.");
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(player), Is.True,
                    "Traversal should use the standard visible DoAfter progress bar.");
            });
        });

        await RunSeconds(0.5f);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(player, offStairsCoordinates);
            var triggeredOff = new StepTriggeredOffEvent(traversal, player);
            SEntMan.EventBus.RaiseLocalEvent(traversal, ref triggeredOff);
        });

        await RunSeconds(2f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).ZLevel, Is.EqualTo(1),
                "Moving from up stairs onto adjacent down stairs must cancel the pending traversal.");
        });

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(player, stairsCoordinates);
            Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(player), Is.True,
                "Entering a traversal tile through movement must start the DoAfter automatically.");
        });

        await RunSeconds(1f);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            SEntMan.System<SharedTransformSystem>().SetCoordinates(player, adjacentStairsCoordinates);
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).ZLevel, Is.EqualTo(1));
        });

        await RunSeconds(1.1f);

        await Server.WaitPost(() =>
        {
            var player = ToServer(Player);
            var playerZ = SEntMan.GetComponent<ZLevelPositionComponent>(player);
            Assert.That(playerZ.ZLevel, Is.EqualTo(2),
                "Moving onto a directly adjacent equivalent staircase must preserve the original timer.");

            var triggeredOn = new StepTriggeredOnEvent(destinationTraversal, player);
            SEntMan.EventBus.RaiseLocalEvent(destinationTraversal, ref triggeredOn);
        });

        await RunSeconds(2.1f);

        await Server.WaitAssertion(() =>
        {
            var player = ToServer(Player);
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).ZLevel, Is.EqualTo(2),
                "Arriving on the paired stairs must not immediately send the player back down.");
        });
    }
}

public sealed class ZLevelTileChangedListenerSystem : TestListenerSystem<ZLevelTileChangedEvent>;
