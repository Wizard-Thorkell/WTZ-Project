using Content.Server.Spawners.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnOnDespawnSystem : EntitySystem
{
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnOnDespawnComponent, TimedDespawnEvent>(OnDespawn);
    }

    private void OnDespawn(EntityUid uid, SpawnOnDespawnComponent comp, ref TimedDespawnEvent args)
    {
        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var worldZLevel = _zLevels.GetWorldZLevel(uid);
        var spawned = Spawn(comp.Prototype, xform.Coordinates);
        _zLevels.StampWorldZLevelPosition(spawned, worldZLevel);
    }

    public void SetPrototype(Entity<SpawnOnDespawnComponent> entity, EntProtoId prototype)
    {
        entity.Comp.Prototype = prototype;
    }
}
