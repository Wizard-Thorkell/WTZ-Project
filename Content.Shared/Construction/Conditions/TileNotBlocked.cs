using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.ZLevel.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Shared.Construction.Conditions;

[UsedImplicitly]
[DataDefinition]
public sealed partial class TileNotBlocked : IConstructionCondition
{
    [DataField("filterMobs")] private bool _filterMobs = false;
    [DataField("failIfSpace")] private bool _failIfSpace = true;
    [DataField("failIfNotSturdy")] private bool _failIfNotSturdy = true;

    public bool Condition(EntityUid user, EntityCoordinates location, Direction direction)
    {
        var entityManager = IoCManager.Resolve<IEntityManager>();
        if (!entityManager.TrySystem<TurfSystem>(out var turfSystem) ||
            !entityManager.TrySystem<SharedZLevelSystem>(out var zLevelSystem))
            return false;

        if (!turfSystem.TryGetZLevelTileRefAtWorldZ(location, zLevelSystem.GetWorldZLevel(user), out var tileRef))
        {
            return false;
        }

        if (turfSystem.IsSpace(tileRef) && _failIfSpace)
        {
            return false;
        }

        if (!turfSystem.GetContentTileDefinition(tileRef).Sturdy && _failIfNotSturdy)
        {
            return false;
        }

        return !turfSystem.IsTileBlocked(tileRef, _filterMobs ? CollisionGroup.MobMask : CollisionGroup.Impassable);
    }

    public ConstructionGuideEntry GenerateGuideEntry()
    {
        return new ConstructionGuideEntry
        {
            Localization = "construction-step-condition-tile-not-blocked",
        };
    }
}
