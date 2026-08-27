using Content.Server.Engineering.Components;
using Content.Server.Stack;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Stacks;
using Content.Shared.ZLevel.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Engineering.EntitySystems
{
    [UsedImplicitly]
    public sealed class SpawnAfterInteractSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly StackSystem _stackSystem = default!;
        [Dependency] private readonly TurfSystem _turfSystem = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedMapSystem _maps = default!;
        [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SpawnAfterInteractComponent, AfterInteractEvent>(HandleAfterInteract);
        }

        private async void HandleAfterInteract(EntityUid uid, SpawnAfterInteractComponent component, AfterInteractEvent args)
        {
            if (!args.CanReach && !component.IgnoreDistance)
                return;
            if (string.IsNullOrEmpty(component.Prototype))
                return;

            var gridUid = _transform.GetGrid(args.ClickLocation);
            if (!TryComp<MapGridComponent>(gridUid, out var grid))
                return;

            var worldZLevel = _zLevel.GetWorldZLevel(args.User);
            var localZLevel = _transform.WorldToLocalZLevel(gridUid.Value, worldZLevel);
            var xy = _maps.TileIndicesFor(gridUid.Value, grid, args.ClickLocation);
            var tileIndices = new ZLevelTileIndices(xy.X, xy.Y, localZLevel);

            bool IsTileClear()
            {
                var tileRef = _maps.GetZLevelTileRef(gridUid.Value, grid, tileIndices);
                return tileRef.Tile.IsEmpty == false && !_turfSystem.IsTileBlocked(tileRef, CollisionGroup.MobMask);
            }

            if (!IsTileClear())
                return;

            if (component.DoAfterTime > 0)
            {
                var doAfterArgs = new DoAfterArgs(EntityManager, args.User, component.DoAfterTime, new AwaitedDoAfterEvent(), null)
                {
                    BreakOnMove = true,
                };
                var result = await _doAfterSystem.WaitDoAfter(doAfterArgs);

                if (result != DoAfterStatus.Finished)
                    return;
            }

            if (component.Deleted || !IsTileClear())
                return;

            if (TryComp<StackComponent>(uid, out var stackComp)
                && component.RemoveOnInteract && !_stackSystem.TryUse((uid, stackComp), 1))
            {
                return;
            }

            var spawned = Spawn(component.Prototype, args.ClickLocation.SnapToGrid(grid));
            _zLevel.SetZLevelPosition(spawned, localZLevel);

            if (component.RemoveOnInteract && stackComp == null)
                TryQueueDel(uid);
        }
    }
}
