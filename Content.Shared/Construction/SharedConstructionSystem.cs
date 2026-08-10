using System.Linq;
using Content.Shared.Construction.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using static Content.Shared.Interaction.SharedInteractionSystem;

namespace Content.Shared.Construction
{
    public abstract class SharedConstructionSystem : EntitySystem
    {
        [Dependency] private readonly IMapManager _mapManager = default!;
        [Dependency] private readonly SharedMapSystem _map = default!;
        [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
        [Dependency] protected readonly SharedTransformSystem TransformSystem = default!;
        [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

        /// <summary>
        ///     Get predicate for construction obstruction checks.
        /// </summary>
        public Ignored? GetPredicate(bool canBuildInImpassable, MapCoordinates coords, EntityUid? source = null)
        {
            if (!canBuildInImpassable)
                return null;

            if (!_mapManager.TryFindGridAt(coords, out var gridUid, out var grid))
                return null;

            var tile = _map.TileIndicesFor(gridUid, grid, coords);
            var zLevel = source is { } uid
                ? TransformSystem.WorldToLocalZLevel(gridUid, _zLevel.GetWorldZLevel(uid))
                : 0;
            var ignored = _zLevel.GetAnchoredEntitiesOnZLevel(gridUid, grid, tile, zLevel).ToHashSet();
            return e => ignored.Contains(e);
        }

        public string GetExamineName(GenericPartInfo info)
        {
            if (info.ExamineName is not null)
                return Loc.GetString(info.ExamineName.Value);

            return PrototypeManager.Index(info.DefaultPrototype).Name;
        }
    }
}
