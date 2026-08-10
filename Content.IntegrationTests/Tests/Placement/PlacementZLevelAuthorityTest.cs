#nullable enable
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Robust.Server.Placement;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network.Messages;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Placement;

[TestFixture]
public sealed class PlacementZLevelAuthorityTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, DummyTicker = false };

    private const string UpperTile = "FloorSteel";
    private const string PlacedEntity = "WallSolid";

    [Test]
    public async Task EntRemoveUsesRequestedZLevel()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var placement = server.ResolveDependency<IPlacementManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var session = playerMan.Sessions.Single();
            Assert.That(session.AttachedEntity, Is.Not.Null);
            SetZLevel(entMan, session.AttachedEntity!.Value, 0);

            var sameZ = entMan.SpawnEntity(null, map.GridCoords);
            SetZLevel(entMan, sameZ, 3);

            var baseZ = entMan.SpawnEntity(null, map.GridCoords);

            placement.HandleNetMessage(new MsgPlacement
            {
                PlaceType = PlacementManagerMessage.RequestEntRemove,
                EntityUid = entMan.GetNetEntity(sameZ),
                ZLevel = 3,
                MsgChannel = session.Channel,
            });

            placement.HandleNetMessage(new MsgPlacement
            {
                PlaceType = PlacementManagerMessage.RequestEntRemove,
                EntityUid = entMan.GetNetEntity(baseZ),
                ZLevel = 3,
                MsgChannel = session.Channel,
            });

            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(sameZ), Is.True);
                Assert.That(entMan.Deleted(baseZ), Is.False);
            });

            entMan.DeleteEntity(baseZ);
        });
    }

    [Test]
    public async Task TilePlacementUsesRequestedZLevel()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var placement = server.ResolveDependency<IPlacementManager>();
        var tileDef = server.ResolveDependency<ITileDefinitionManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var session = playerMan.Sessions.Single();
            Assert.That(session.AttachedEntity, Is.Not.Null);
            SetZLevel(entMan, session.AttachedEntity!.Value, 0);

            var grid = entMan.GetComponent<MapGridComponent>(map.Grid);
            var coords = map.GridCoords.Offset(new Vector2(1f, 0f));
            var tile = mapSystem.TileIndicesFor(map.Grid, grid, coords);
            mapSystem.SetTile(map.Grid, grid, tile, Tile.Empty);

            placement.HandleNetMessage(new MsgPlacement
            {
                PlaceType = PlacementManagerMessage.RequestPlacement,
                IsTile = true,
                TileType = tileDef[UpperTile].TileId,
                NetCoordinates = entMan.GetNetCoordinates(coords),
                DirRcv = Direction.South,
                ZLevel = 2,
                MsgChannel = session.Channel,
            });

            var baseTile = mapSystem.GetTileRef(map.Grid, grid, tile).Tile;
            var upperTile = mapSystem.GetZLevelTileRef(map.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 2)).Tile;

            Assert.Multiple(() =>
            {
                Assert.That(baseTile.IsEmpty, Is.True, "Placing on z=2 should not alter the base tile.");
                Assert.That(upperTile.TypeId, Is.EqualTo(tileDef[UpperTile].TileId));
            });
        });
    }

    [Test]
    public async Task RectRemoveUsesRequestedZLevel()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var placement = server.ResolveDependency<IPlacementManager>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var session = playerMan.Sessions.Single();
            Assert.That(session.AttachedEntity, Is.Not.Null);
            SetZLevel(entMan, session.AttachedEntity!.Value, 0);

            var sameZ = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.25f, 0.25f)));
            SetZLevel(entMan, sameZ, 2);

            var baseZ = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(0.5f, 0.5f)));

            placement.HandleNetMessage(new MsgPlacement
            {
                PlaceType = PlacementManagerMessage.RequestRectRemove,
                NetCoordinates = entMan.GetNetCoordinates(map.GridCoords),
                RectSize = new Vector2(1f, 1f),
                ZLevel = 2,
                MsgChannel = session.Channel,
            });

            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(sameZ), Is.True);
                Assert.That(entMan.Deleted(baseZ), Is.False);
            });

            entMan.DeleteEntity(baseZ);
        });
    }

    [Test]
    public async Task EntityPlacementStampsRequestedZLevel()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var placement = server.ResolveDependency<IPlacementManager>();
        var lookup = entMan.System<EntityLookupSystem>();
        var map = await Pair.CreateTestMap();

        await server.WaitPost(() =>
        {
            var session = playerMan.Sessions.Single();
            Assert.That(session.AttachedEntity, Is.Not.Null);
            SetZLevel(entMan, session.AttachedEntity!.Value, 0);

            var coords = map.GridCoords.Offset(new Vector2(1f, 0f));
            placement.HandleNetMessage(new MsgPlacement
            {
                PlaceType = PlacementManagerMessage.RequestPlacement,
                IsTile = false,
                EntityTemplateName = PlacedEntity,
                NetCoordinates = entMan.GetNetCoordinates(coords),
                DirRcv = Direction.South,
                ZLevel = 2,
                MsgChannel = session.Channel,
            });

            var placed = lookup.GetEntitiesInRange(coords, 0.2f)
                .Where(uid => entMan.TryGetComponent(uid, out MetaDataComponent? meta) &&
                              meta.EntityPrototype?.ID == PlacedEntity)
                .ToArray();

            Assert.That(placed, Has.Length.EqualTo(1));
            Assert.That(entMan.TryGetComponent(placed[0], out ZLevelPositionComponent? zLevel), Is.True);
            Assert.That(zLevel!.ZLevel, Is.EqualTo(2));
        });
    }

    private static void SetZLevel(IEntityManager entMan, EntityUid uid, int zLevel)
    {
        var zLevelComp = entMan.EnsureComponent<ZLevelPositionComponent>(uid);
        zLevelComp.ZLevel = zLevel;
        zLevelComp.LocalZOffset = 0f;
        entMan.Dirty(uid, zLevelComp);
    }
}
