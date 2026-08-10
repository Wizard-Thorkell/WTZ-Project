// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.ZLevel;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    private static readonly int[] VerticalZOffsets = [-1, 1];

    private void OnZLevelTileChanged(ref ZLevelTileChangedEvent ev)
    {
        foreach (var change in ev.Changes)
        {
            InvalidateTile((ev.Entity.Owner, CompOrNull<GridAtmosphereComponent>(ev.Entity.Owner)), change.GridIndices);
            InvalidateVerticalNeighbors(ev.Entity.Owner, change.GridIndices);
        }
    }

    private void OnZLevelBoundaryChanged(ref ZLevelBoundaryChangedEvent ev)
    {
        InvalidateTile((ev.Grid.Owner, CompOrNull<GridAtmosphereComponent>(ev.Grid.Owner)),
            new ZLevelTileIndices(ev.Tile.X, ev.Tile.Y, ev.LowerZ));
        InvalidateTile((ev.Grid.Owner, CompOrNull<GridAtmosphereComponent>(ev.Grid.Owner)),
            new ZLevelTileIndices(ev.Tile.X, ev.Tile.Y, ev.LowerZ + 1));
    }

    public void InvalidateTile(Entity<GridAtmosphereComponent?> entity, ZLevelTileIndices tile)
    {
        if (!_atmosQuery.Resolve(entity.Owner, ref entity.Comp, false))
            return;

        if (tile.Z == 0)
            entity.Comp.InvalidatedCoords.Add(new Vector2i(tile.X, tile.Y));
        else
            entity.Comp.InvalidatedZLevelCoords.Add(tile);
    }

    private void InvalidateVerticalNeighbors(EntityUid gridUid, ZLevelTileIndices tile)
    {
        var atmos = CompOrNull<GridAtmosphereComponent>(gridUid);
        if (!_atmosQuery.Resolve(gridUid, ref atmos, false))
            return;

        atmos.InvalidatedZLevelCoords.Add(new ZLevelTileIndices(tile.X, tile.Y, tile.Z - 1));
        atmos.InvalidatedZLevelCoords.Add(new ZLevelTileIndices(tile.X, tile.Y, tile.Z + 1));
    }

    private TileAtmosphere GetOrNewTile(EntityUid owner, GridAtmosphereComponent atmosphere, ZLevelTileIndices index, bool invalidateNew = true)
    {
        if (index.Z == 0)
            return GetOrNewTile(owner, atmosphere, new Vector2i(index.X, index.Y), invalidateNew);

        if (atmosphere.ZLevelTiles.TryGetValue(index, out var tile))
            return tile;

        tile = new TileAtmosphere();
        atmosphere.ZLevelTiles[index] = tile;

        if (invalidateNew)
            atmosphere.InvalidatedZLevelCoords.Add(index);

        tile.GridIndex = owner;
        tile.GridIndices = new Vector2i(index.X, index.Y);
        tile.ZLevel = index.Z;
        return tile;
    }

    private bool TryGetTileAtmosphere(GridAtmosphereComponent atmosphere, ZLevelTileIndices index, out TileAtmosphere tile)
    {
        if (index.Z == 0)
            return atmosphere.Tiles.TryGetValue(new Vector2i(index.X, index.Y), out tile!);

        return atmosphere.ZLevelTiles.TryGetValue(index, out tile!);
    }

    private bool RemoveTileAtmosphere(GridAtmosphereComponent atmosphere, TileAtmosphere tile)
    {
        return tile.ZLevel == 0
            ? atmosphere.Tiles.Remove(tile.GridIndices)
            : atmosphere.ZLevelTiles.Remove(new ZLevelTileIndices(tile.GridIndices.X, tile.GridIndices.Y, tile.ZLevel));
    }

    private bool HasBackingGridTile(Entity<MapGridComponent> grid, TileAtmosphere tile)
    {
        if (tile.ZLevel == 0)
            return _map.TryGetTile(grid, tile.GridIndices, out var gridTile) && !gridTile.IsEmpty;

        return !_mapSystem.GetZLevelTileRef(grid.Owner, grid.Comp, new ZLevelTileIndices(tile.GridIndices.X, tile.GridIndices.Y, tile.ZLevel)).Tile.IsEmpty;
    }

    private bool HasBackingGridTile(Entity<MapGridComponent> grid, ZLevelTileIndices tile)
    {
        if (tile.Z == 0)
            return _map.TryGetTile(grid, new Vector2i(tile.X, tile.Y), out var gridTile) && !gridTile.IsEmpty;

        return !_mapSystem.GetZLevelTileRef(grid.Owner, grid.Comp, tile).Tile.IsEmpty;
    }

    private IEnumerable<TileAtmosphere> EnumerateConnectedTiles(TileAtmosphere tile)
    {
        foreach (var otherTile in tile.AdjacentTiles)
        {
            if (otherTile != null)
                yield return otherTile;
        }

        if (tile.AdjacentTileAbove != null)
            yield return tile.AdjacentTileAbove;

        if (tile.AdjacentTileBelow != null)
            yield return tile.AdjacentTileBelow;
    }

    private int GetConnectedTileCount(TileAtmosphere tile)
    {
        var count = 0;
        foreach (var otherTile in tile.AdjacentTiles)
        {
            if (otherTile != null)
                count++;
        }

        if (tile.AdjacentTileAbove != null)
            count++;

        if (tile.AdjacentTileBelow != null)
            count++;

        return count;
    }
}
