using Content.Shared.Construction;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared.Tools.Systems;

public sealed class ToolRefinablSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ToolRefinableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ToolRefinableComponent, WelderRefineDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(EntityUid uid, ToolRefinableComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = _toolSystem.UseTool(
            args.Used,
            args.User,
            uid,
            component.RefineTime,
            component.QualityNeeded,
            new WelderRefineDoAfterEvent(),
            fuel: component.RefineFuel);
    }

    private void OnDoAfter(EntityUid uid, ToolRefinableComponent component, WelderRefineDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        var xform = Transform(uid);
        var spawns = EntitySpawnCollection.GetSpawns(component.RefineResult, _random);
        var worldZLevel = _zLevels.GetWorldZLevel(uid);
        foreach (var spawn in spawns)
        {
            var spawned = SpawnNextToOrDrop(spawn, uid, xform);
            _zLevels.StampWorldZLevelPosition(spawned, worldZLevel);
        }

        Del(uid);
    }
}
