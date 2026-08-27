using System.Numerics;
using System.Linq;
using Content.Shared.Explosion;
using Content.Shared.Explosion.Components;
using Content.Shared.Explosion.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Server.Explosion.EntitySystems;

// This part of the system handled send visual / overlay data to clients.
public sealed partial class ExplosionSystem
{
    public void InitVisuals()
    {
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentGetState>(OnGetState);
    }

    private void OnGetState(EntityUid uid, ExplosionVisualsComponent component, ref ComponentGetState args)
    {
        Dictionary<NetEntity, Dictionary<int, Dictionary<int, List<Vector2i>>>> tileLists = new();
        foreach (var (grid, data) in component.Tiles)
        {
            tileLists.Add(GetNetEntity(grid), data);
        }

        args.State = new ExplosionVisualsState(
            component.Epicenter,
            component.EpicenterWorldZ,
            component.ExplosionType,
            component.Intensity,
            component.SpaceTiles,
            tileLists,
            component.SpaceMatrix,
            component.SpaceTileSize);
    }

    /// <summary>
    ///     Constructor for the shared <see cref="ExplosionEvent"/> using the server-exclusive explosion classes.
    /// </summary>
    private EntityUid CreateExplosionVisualEntity(
        MapCoordinates epicenter,
        int epicenterWorldZ,
        string prototype,
        Matrix3x2 spaceMatrix,
        Dictionary<int, ExplosionSpaceTileFlood> spaceData,
        IEnumerable<ExplosionGridTileFlood> gridData,
        List<float> iterationIntensity)
    {
        var explosionEntity = Spawn(null, MapCoordinates.Nullspace);
        var comp = AddComp<ExplosionVisualsComponent>(explosionEntity);

        foreach (var grid in gridData)
        {
            if (!comp.Tiles.TryGetValue(grid.Grid.Owner, out var layers))
            {
                layers = new();
                comp.Tiles[grid.Grid.Owner] = layers;
            }

            layers[grid.LocalZ] = grid.TileLists;
        }

        foreach (var (worldZ, data) in spaceData)
        {
            comp.SpaceTiles[worldZ] = data.TileLists;
        }

        comp.Epicenter = epicenter;
        comp.EpicenterWorldZ = epicenterWorldZ;
        comp.ExplosionType = prototype;
        comp.Intensity = iterationIntensity;
        comp.SpaceMatrix = spaceMatrix;
        comp.SpaceTileSize = spaceData.Values.FirstOrDefault()?.TileSize ?? DefaultTileSize;
        Dirty(explosionEntity, comp);

        // Light, sound & visuals may extend well beyond normal PVS range. In principle, this should probably still be
        // restricted to something like the same map, but whatever.
        _pvsSys.AddGlobalOverride(explosionEntity);

        var appearance = AddComp<AppearanceComponent>(explosionEntity);
        _appearance.SetData(explosionEntity, ExplosionAppearanceData.Progress, 1, appearance);

        return explosionEntity;
    }
}
