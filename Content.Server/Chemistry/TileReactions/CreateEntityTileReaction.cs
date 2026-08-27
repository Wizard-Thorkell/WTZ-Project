using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Maps;
using Content.Shared.Whitelist;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using System.Numerics;

namespace Content.Server.Chemistry.TileReactions;

[DataDefinition]
public sealed partial class CreateEntityTileReaction : ITileReaction, IZLevelTileReaction
{
    [DataField(required: true, customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Entity = default!;

    [DataField]
    public FixedPoint2 Usage = FixedPoint2.New(1);

    /// <summary>
    ///     How many of the whitelisted entity can fit on one tile?
    /// </summary>
    [DataField]
    public int MaxOnTile = 1;

    /// <summary>
    ///     The whitelist to use when determining what counts as "max entities on a tile".0
    /// </summary>
    [DataField("maxOnTileWhitelist")]
    public EntityWhitelist? Whitelist;

    [DataField]
    public float RandomOffsetMax = 0.0f;

    public FixedPoint2 TileReact(TileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (reactVolume < Usage)
            return FixedPoint2.Zero;

        if (Whitelist != null)
        {
            var lookup = entityManager.System<EntityLookupSystem>();

            int acc = 0;
            foreach (var ent in lookup.GetEntitiesInTile(tile, LookupFlags.Static))
            {
                var whitelistSystem = entityManager.System<EntityWhitelistSystem>();
                if (whitelistSystem.IsWhitelistPass(Whitelist, ent))
                    acc += 1;

                if (acc >= MaxOnTile)
                    return FixedPoint2.Zero;
            }
        }

        var random = IoCManager.Resolve<IRobustRandom>();
        var xoffs = random.NextFloat(-RandomOffsetMax, RandomOffsetMax);
        var yoffs = random.NextFloat(-RandomOffsetMax, RandomOffsetMax);

        var center = entityManager.System<TurfSystem>().GetTileCenter(tile);
        var pos = center.Offset(new Vector2(xoffs, yoffs));
        entityManager.SpawnEntity(Entity, pos);

        return Usage;
    }

    public FixedPoint2 TileReact(ZLevelTileRef tile,
        ReagentPrototype reagent,
        FixedPoint2 reactVolume,
        IEntityManager entityManager,
        List<ReagentData>? data)
    {
        if (reactVolume < Usage)
            return FixedPoint2.Zero;

        var zLevel = entityManager.System<SharedZLevelSystem>();
        if (Whitelist != null)
        {
            var lookup = entityManager.System<EntityLookupSystem>();
            var map = entityManager.System<SharedMapSystem>();
            var grid = entityManager.GetComponent<MapGridComponent>(tile.GridUid);
            var baseTile = map.GetTileRef(
                tile.GridUid,
                grid,
                new Vector2i(tile.GridIndices.X, tile.GridIndices.Y));
            var whitelistSystem = entityManager.System<EntityWhitelistSystem>();
            var count = 0;

            foreach (var ent in lookup.GetEntitiesInTile(baseTile, LookupFlags.Static))
            {
                if (zLevel.GetZLevel(ent) != tile.GridIndices.Z ||
                    !whitelistSystem.IsWhitelistPass(Whitelist, ent))
                {
                    continue;
                }

                if (++count >= MaxOnTile)
                    return FixedPoint2.Zero;
            }
        }

        var random = IoCManager.Resolve<IRobustRandom>();
        var offset = new Vector2(
            random.NextFloat(-RandomOffsetMax, RandomOffsetMax),
            random.NextFloat(-RandomOffsetMax, RandomOffsetMax));
        var center = entityManager.System<TurfSystem>().GetTileCenter(tile);
        var spawned = entityManager.SpawnEntity(Entity, center.Offset(offset));
        zLevel.SetZLevelPosition(spawned, tile.GridIndices.Z);

        return Usage;
    }
}
