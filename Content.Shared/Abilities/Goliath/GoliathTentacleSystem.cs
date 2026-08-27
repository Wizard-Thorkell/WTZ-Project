using Content.Shared.Directions;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Content.Shared.ZLevel.Systems;

namespace Content.Shared.Abilities.Goliath;

public sealed class GoliathTentacleSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<GoliathSummonTentacleAction>(OnSummonAction);
    }

    private void OnSummonAction(GoliathSummonTentacleAction args)
    {
        if (args.Handled)
            return;

        // TODO: animation

        _popup.PopupPredicted(Loc.GetString("tentacle-ability-use-popup", ("entity", args.Performer)), args.Performer, args.Performer, type: PopupType.SmallCaution);
        _stun.TryAddStunDuration(args.Performer, TimeSpan.FromSeconds(0.8f));

        var coords = args.Target;
        List<EntityCoordinates> spawnPos = new();
        spawnPos.Add(coords);

        var dirs = new List<Direction>();
        dirs.AddRange(args.OffsetDirections);

        for (var i = 0; i < 3; i++)
        {
            var dir = _random.PickAndTake(dirs);
            spawnPos.Add(coords.Offset(dir));
        }

        if (_transform.GetGrid(coords) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return;
        var worldZLevel = _zLevel.GetWorldZLevel(args.Performer);

        foreach (var pos in spawnPos)
        {
            if (!_turf.TryGetZLevelTileRefAtWorldZ(pos, worldZLevel, out var tileRef) ||
                _turf.IsSpace(tileRef) ||
                _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
            {
                continue;
            }

            if (_net.IsServer)
            {
                var tentacle = Spawn(args.EntityId, pos);
                _zLevel.SetZLevelPosition(tentacle, tileRef.GridIndices.Z);
            }
        }

        args.Handled = true;
    }
}
