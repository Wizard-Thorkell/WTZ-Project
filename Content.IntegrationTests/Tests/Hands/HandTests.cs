using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Gravity;
using Content.Server.Storage.EntitySystems;
using Content.Shared.Gravity;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.Hands;

[TestFixture]
public sealed class HandTests : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestPickUpThenDropInContainerTestBox
  name: box
  components:
  - type: EntityStorage
  - type: ContainerContainer
    containers:
      entity_storage: !type:Container
";


    public override PoolSettings PoolSettings => new()
    {
        Connected = true,
        DummyTicker = false
    };

    [Test]
    public async Task TestPickupDrop()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var xform = entMan.GetComponent<TransformComponent>(player);
            item = entMan.SpawnEntity("Crowbar", tSys.GetMapCoordinates(player, xform: xform));
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });

        // run ticks here is important, as errors may happen within the container system's frame update methods.
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });

        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Null);

        await server.WaitPost(() => mapSystem.DeleteMap(data.MapId));
    }

    [Test]
    public async Task TestPickUpThenDropInContainer()
    {
        var pair = Pair;
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();
        var containerSystem = server.System<SharedContainerSystem>();

        EntityUid item = default;
        EntityUid box = default;
        EntityUid player = default;
        HandsComponent hands = default!;

        // spawn the elusive box and crowbar at the coordinates
        await server.WaitPost(() => box = server.EntMan.SpawnEntity("TestPickUpThenDropInContainerTestBox", map.GridCoords));
        await server.WaitPost(() => item = server.EntMan.SpawnEntity("Crowbar", map.GridCoords));
        // place the player at the exact same coordinates and have them grab the crowbar
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            tSys.PlaceNextTo(player, item);
            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });
        await pair.RunTicksSync(5);
        Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));

        // Open then close the box to place the player, who is holding the crowbar, inside of it
        var storage = server.System<EntityStorageSystem>();
        await server.WaitPost(() =>
        {
            storage.OpenStorage(box);
            storage.CloseStorage(box);
        });
        await pair.RunTicksSync(5);
        Assert.That(containerSystem.IsEntityInContainer(player), Is.True);

        // Dropping the item while the player is inside the box should cause the item
        // to also be inside the same container the player is in now,
        // with the item not being in the player's hands
        await server.WaitPost(() =>
        {
            sys.TryDrop(player, item);
        });
        await pair.RunTicksSync(5);
        var xform = entMan.GetComponent<TransformComponent>(player);
        var itemXform = entMan.GetComponent<TransformComponent>(item);
        Assert.That(sys.GetActiveItem((player, hands)), Is.Not.EqualTo(item));
        Assert.That(containerSystem.IsInSameOrNoContainer((player, xform), (item, itemXform)));

        await server.WaitPost(() => mapSystem.DeleteMap(map.MapId));
    }

    [Test]
    public async Task TestPickupDropPreservesUpperFloorZLevelOnDisplacedFrame()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();
        var zSys = entMan.System<SharedZLevelSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        EntityCoordinates originalPlayerCoordinates = default;
        var playerHadZPosition = false;
        var originalPlayerZLevel = 0;
        var originalPlayerZOffset = 0f;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            originalPlayerCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates;
            if (entMan.TryGetComponent<ZLevelPositionComponent>(player, out var originalPlayerZ))
            {
                playerHadZPosition = true;
                originalPlayerZLevel = originalPlayerZ.ZLevel;
                originalPlayerZOffset = originalPlayerZ.LocalZOffset;
            }

            tSys.SetCoordinates(player, data.GridCoords);
            Assert.That(tSys.SetZLevelFrameOrigin(data.Grid, 5), Is.True);
            Assert.That(zSys.SetZLevelPosition(player, 1), Is.True);

            var xform = entMan.GetComponent<TransformComponent>(player);
            item = entMan.SpawnEntity("Crowbar", tSys.GetMapCoordinates(player, xform: xform));
            Assert.That(zSys.SetZLevelPosition(item, 1), Is.True);

            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });

        await pair.RunTicksSync(5);
        Assert.Multiple(() =>
        {
            Assert.That(sys.GetActiveItem((player, hands)), Is.EqualTo(item));
            Assert.That(entMan.HasComponent<ZLevelPositionComponent>(item), Is.False);
        });

        await server.WaitPost(() => sys.TryDrop(player, item));

        await pair.RunTicksSync(5);
        var droppedZ = entMan.GetComponent<ZLevelPositionComponent>(item);
        Assert.Multiple(() =>
        {
            Assert.That(sys.GetActiveItem((player, hands)), Is.Null);
            Assert.That(droppedZ.ZLevel, Is.EqualTo(1));
            Assert.That(droppedZ.LocalZOffset, Is.EqualTo(0f));
            Assert.That(tSys.GetWorldZLevel((item, entMan.GetComponent<TransformComponent>(item), droppedZ)), Is.EqualTo(6));
        });

        await server.WaitPost(() =>
        {
            tSys.SetCoordinates(player, originalPlayerCoordinates);
            if (playerHadZPosition)
                zSys.SetZLevelPosition(player, originalPlayerZLevel, originalPlayerZOffset);
            else
                zSys.ClearZLevelPosition(player);

            mapSystem.DeleteMap(data.MapId);
        });
    }

    [Test]
    public async Task TestPickupDropClearsBaseFloorZLevelComponent()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var xform = entMan.GetComponent<TransformComponent>(player);
            item = entMan.SpawnEntity("Crowbar", tSys.GetMapCoordinates(player, xform: xform));

            var itemZ = entMan.EnsureComponent<ZLevelPositionComponent>(item);
            itemZ.ZLevel = 1;
            itemZ.LocalZOffset = 0f;

            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
        });

        await pair.RunTicksSync(5);
        Assert.That(entMan.HasComponent<ZLevelPositionComponent>(item), Is.False);

        await server.WaitPost(() => sys.TryDrop(player, item));

        await pair.RunTicksSync(5);
        Assert.Multiple(() =>
        {
            Assert.That(sys.GetActiveItem((player, hands)), Is.Null);
            Assert.That(entMan.HasComponent<ZLevelPositionComponent>(item), Is.False);
        });

        await server.WaitPost(() => mapSystem.DeleteMap(data.MapId));
    }

    [Test]
    public async Task TestDropOnUnsupportedUpperFloorFallsToLowerSupport()
    {
        var pair = Pair;
        var server = pair.Server;

        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var mapSystem = server.System<SharedMapSystem>();
        var gravitySystem = server.System<GravitySystem>();
        var sys = entMan.System<SharedHandsSystem>();
        var tSys = entMan.System<TransformSystem>();

        var data = await pair.CreateTestMap();
        await pair.RunTicksSync(5);

        EntityUid item = default;
        EntityUid player = default;
        HandsComponent hands = default!;
        EntityCoordinates originalPlayerCoordinates = default;
        var playerHadZPosition = false;
        var originalPlayerZLevel = 0;
        var originalPlayerZOffset = 0f;
        var playerHadKinematics = false;
        var originalVerticalVelocity = 0f;
        var originalGrounded = false;
        await server.WaitPost(() =>
        {
            player = playerMan.Sessions.First().AttachedEntity!.Value;
            var grid = entMan.GetComponent<MapGridComponent>(data.Grid);
            var tile = data.Tile.GridIndices;
            var gravity = entMan.EnsureComponent<GravityComponent>(data.Grid);
            gravitySystem.EnableGravity(data.Grid, gravity);
            originalPlayerCoordinates = entMan.GetComponent<TransformComponent>(player).Coordinates;
            if (entMan.TryGetComponent<ZLevelPositionComponent>(player, out var originalPlayerZ))
            {
                playerHadZPosition = true;
                originalPlayerZLevel = originalPlayerZ.ZLevel;
                originalPlayerZOffset = originalPlayerZ.LocalZOffset;
            }
            if (entMan.TryGetComponent<ZLevelKinematicsComponent>(player, out var originalKinematics))
            {
                playerHadKinematics = true;
                originalVerticalVelocity = originalKinematics.VerticalVelocity;
                originalGrounded = originalKinematics.Grounded;
            }

            tSys.SetCoordinates(player, data.GridCoords);

            var playerZ = entMan.EnsureComponent<ZLevelPositionComponent>(player);
            playerZ.ZLevel = 1;
            playerZ.LocalZOffset = 0f;

            mapSystem.SetZLevelTile(data.Grid.Owner, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), Tile.Empty);
            mapSystem.SetZLevelTile(data.Grid.Owner, grid, new ZLevelTileIndices(tile.X, tile.Y, 0), new Tile(1));

            var playerXform = entMan.GetComponent<TransformComponent>(player);
            item = entMan.SpawnEntity("Crowbar", tSys.GetMapCoordinates(player, xform: playerXform));
            var itemZ = entMan.EnsureComponent<ZLevelPositionComponent>(item);
            itemZ.ZLevel = 1;
            itemZ.LocalZOffset = 0f;

            hands = entMan.GetComponent<HandsComponent>(player);
            sys.TryPickup(player, item, hands.ActiveHandId!);
            sys.TryDrop(player, item);
        });

        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            var droppedZ = entMan.GetComponent<ZLevelPositionComponent>(item);
            Assert.Multiple(() =>
            {
                Assert.That(sys.GetActiveItem((player, hands)), Is.Null);
                Assert.That(droppedZ.ZLevel, Is.EqualTo(0));
                Assert.That(droppedZ.LocalZOffset, Is.EqualTo(0f).Within(0.001f));
            });
        });

        await server.WaitPost(() =>
        {
            tSys.SetCoordinates(player, originalPlayerCoordinates);
            if (playerHadZPosition)
            {
                var playerZ = entMan.EnsureComponent<ZLevelPositionComponent>(player);
                playerZ.ZLevel = originalPlayerZLevel;
                playerZ.LocalZOffset = originalPlayerZOffset;
            }
            else
            {
                entMan.RemoveComponent<ZLevelPositionComponent>(player);
            }

            if (playerHadKinematics)
            {
                var kinematics = entMan.EnsureComponent<ZLevelKinematicsComponent>(player);
                kinematics.VerticalVelocity = originalVerticalVelocity;
                kinematics.Grounded = originalGrounded;
            }
            else
            {
                entMan.RemoveComponent<ZLevelKinematicsComponent>(player);
            }

            mapSystem.DeleteMap(data.MapId);
        });
    }
}
