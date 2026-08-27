using Robust.Shared.Containers;
using Robust.Shared.Network;

using Content.Shared.ZLevel.Systems;

namespace Content.Shared.EntityEffects.Effects.EntitySpawning;

/// <summary>
/// Spawns a given number of entities of a given prototype in a specified container owned by this entity.
/// Acts like <see cref="SpawnEntityEntityEffectSystem"/> if it cannot spawn the prototype in the specified container.
/// Amount is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class SpawnEntityInContainerOrDropEntityEffectSystem : EntityEffectSystem<ContainerManagerComponent, SpawnEntityInContainerOrDrop>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    protected override void Effect(Entity<ContainerManagerComponent> entity, ref EntityEffectEvent<SpawnEntityInContainerOrDrop> args)
    {
        var quantity = args.Effect.Number * (int)Math.Floor(args.Scale);
        var proto = args.Effect.Entity;
        var container = args.Effect.ContainerName;
        var worldZLevel = _zLevels.GetWorldZLevel(entity);

        var xform = Transform(entity);

        if (args.Effect.Predicted)
        {
            for (var i = 0; i < quantity; i++)
            {
                var spawned = PredictedSpawnInContainerOrDrop(proto, entity, container, xform, entity.Comp);
                _zLevels.StampWorldZLevelPosition(spawned, worldZLevel);
            }
        }
        else if (_net.IsServer)
        {
            for (var i = 0; i < quantity; i++)
            {
                var spawned = SpawnInContainerOrDrop(proto, entity, container, xform, entity.Comp);
                _zLevels.StampWorldZLevelPosition(spawned, worldZLevel);
            }
        }
    }
}

/// <inheritdoc cref="BaseSpawnEntityEntityEffect{T}"/>
public sealed partial class SpawnEntityInContainerOrDrop : BaseSpawnEntityEntityEffect<SpawnEntityInContainerOrDrop>
{
    /// <summary>
    /// Name of the container we're trying to spawn into.
    /// </summary>
    [DataField(required: true)]
    public string ContainerName;
}
