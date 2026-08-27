using System.Numerics;
using Content.Server.Decals;
using Content.Server.Spawners.Components;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Spawners.EntitySystems;

public sealed class RandomDecalSpawnerSystem : EntitySystem
{
    [Dependency] private readonly DecalSystem _decal = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefs = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomDecalSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, RandomDecalSpawnerComponent component, MapInitEvent args)
    {
        TrySpawn(uid);
        if (component.DeleteSpawnerAfterSpawn)
            QueueDel(uid);
    }

    public bool TrySpawn(Entity<RandomDecalSpawnerComponent?> ent)
    {
        if (!TryComp<RandomDecalSpawnerComponent>(ent, out var comp))
            return false;

        if (comp.Decals.Count == 0)
            return false;

        var tileWhitelist = new List<ITileDefinition>();
        if (comp.TileWhitelist.Count > 0)
        {
            foreach (var tileProto in comp.TileWhitelist)
            {
                if (_tileDefs.TryGetDefinition(tileProto, out var tileDef))
                    tileWhitelist.Add(tileDef);
            }
        }
        else if (comp.TileBlacklist.Count > 0)
        {
            foreach (var tileDef in _tileDefs)
            {
                if (!comp.TileBlacklist.Contains(tileDef.ID))
                    tileWhitelist.Add(tileDef);
            }
        }

        var xform = Transform(ent);
        if (!TryComp<MapGridComponent>(xform.GridUid, out var grid))
            return false;
        var localZ = _zLevel.GetZLevel(ent.Owner);

        var addedDecals = new Dictionary<string, int>();

        for (var i = 0; i < comp.MaxDecals; i++)
        {
            if (comp.Prob < 1f && _random.NextFloat() > comp.Prob)
                continue;

            // The vector added here is just to center the generated decals to the tile the spawner is on.
            var localPos = xform.Coordinates.Position + _random.NextVector2(comp.Radius) + new Vector2(-0.5f, -0.5f);
            var position = new EntityCoordinates(xform.GridUid.Value, localPos);

            var tileIndices = _map.TileIndicesFor(xform.GridUid.Value, grid, position);
            var tileRef = _map.GetZLevelTileRef(
                xform.GridUid.Value,
                grid,
                new ZLevelTileIndices(tileIndices.X, tileIndices.Y, localZ));

            if (tileWhitelist.Count > 0)
            {
                _tileDefs.TryGetDefinition(tileRef.Tile.TypeId, out var currTileDef);
                if (currTileDef is null || !tileWhitelist.Contains(currTileDef))
                    continue;
            }

            var tileRefStr = tileRef.ToString();
            if (comp.MaxDecalsPerTile is > 0)
            {
                addedDecals.TryAdd(tileRefStr, 0);
                if (addedDecals[tileRefStr] >= comp.MaxDecalsPerTile)
                    continue;
            }

            var decalProtoId = _random.Pick(comp.Decals);
            var decalProto = _prototypes.Index(decalProtoId);
            var snapPosition = comp.SnapPosition ?? decalProto.DefaultSnap;
            if (snapPosition)
            {
                position = position.WithPosition(
                    new Vector2(tileRef.GridIndices.X, tileRef.GridIndices.Y) * grid.TileSize);
            }

            var cleanable = comp.Cleanable ?? decalProto.DefaultCleanable;

            var rotation = Angle.Zero;
            if (comp.RandomRotation)
            {
                if (comp.SnapRotation)
                    rotation = new Angle((MathF.PI / 2f) * _random.Next(3));
                else
                    rotation = _random.NextAngle();
            }

            var color = comp.Color;
            if (comp.RandomColorList != null && comp.RandomColorList.Count != 0)
                color = _random.Pick(comp.RandomColorList);

            _decal.TryAddDecal(
                decalProtoId,
                position,
                out _,
                color,
                rotation,
                comp.ZIndex,
                cleanable,
                localZ
            );

            if (comp.MaxDecalsPerTile is > 0)
                addedDecals[tileRefStr]++;
        }

        return true;
    }
}
