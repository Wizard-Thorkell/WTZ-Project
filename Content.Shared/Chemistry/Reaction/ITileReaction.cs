using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Map;

namespace Content.Shared.Chemistry.Reaction
{
    public interface ITileReaction
    {
        FixedPoint2 TileReact(TileRef tile,
            ReagentPrototype reagent,
            FixedPoint2 reactVolume,
            IEntityManager entityManager,
            List<ReagentData>? data = null);
    }

    /// <summary>
    /// A tile reaction that explicitly supports sparse Z-level tiles.
    /// Reactions without this interface are skipped outside the base layer.
    /// </summary>
    public interface IZLevelTileReaction
    {
        FixedPoint2 TileReact(ZLevelTileRef tile,
            ReagentPrototype reagent,
            FixedPoint2 reactVolume,
            IEntityManager entityManager,
            List<ReagentData>? data = null);
    }
}
