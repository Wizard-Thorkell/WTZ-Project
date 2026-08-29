// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map;

namespace Content.Server.Atmos.EntitySystems;

public sealed partial class AtmosphereSystem
{
    /// <summary>
    /// Replaces the persistent atmosphere on one authored floor with clones of
    /// another floor's real tile mixtures. Runtime simulation state is rebuilt.
    /// </summary>
    public void CopyZLevelAtmosphere(EntityUid gridUid, int sourceLevel, int targetLevel)
    {
        if (sourceLevel == targetLevel || !TryComp<GridAtmosphereComponent>(gridUid, out var atmosphere))
            return;

        var source = GetLayerTiles(atmosphere, sourceLevel)
            .Where(tile => !tile.NoGridTile && tile.Air != null)
            .Select(tile => (tile.GridIndices, Mixture: tile.Air!.Clone()))
            .ToArray();

        ClearZLevelAtmosphere(gridUid, atmosphere, targetLevel);
        foreach (var (indices, mixture) in source)
        {
            var tile = new TileAtmosphere(gridUid, indices, mixture)
            {
                ZLevel = targetLevel,
            };

            if (targetLevel == 0)
                atmosphere.Tiles[indices] = tile;
            else
                atmosphere.ZLevelTiles[new ZLevelTileIndices(indices.X, indices.Y, targetLevel)] = tile;

            InvalidateTile((gridUid, atmosphere), new ZLevelTileIndices(indices.X, indices.Y, targetLevel));
            InvalidateVisuals(
                (gridUid, CompOrNull<GasTileOverlayComponent>(gridUid)),
                new ZLevelTileIndices(indices.X, indices.Y, targetLevel));
        }
    }

    /// <summary>
    /// Removes persistent and transient atmosphere cells owned by one authored
    /// floor without disturbing mixtures on the remaining floors.
    /// </summary>
    public void ClearZLevelAtmosphere(EntityUid gridUid, int level)
    {
        if (TryComp<GridAtmosphereComponent>(gridUid, out var atmosphere))
            ClearZLevelAtmosphere(gridUid, atmosphere, level);
    }

    private void ClearZLevelAtmosphere(EntityUid gridUid, GridAtmosphereComponent atmosphere, int level)
    {
        var removed = GetLayerTiles(atmosphere, level).ToHashSet();
        if (removed.Count == 0)
            return;

        var touchedGroups = new HashSet<ExcitedGroup>();
        foreach (var tile in removed)
        {
            if (tile.ExcitedGroup is { } group && !group.Disposed)
            {
                ExcitedGroupRemoveTile(group, tile);
                touchedGroups.Add(group);
            }

            tile.Excited = false;
            tile.Hotspot = default;
            atmosphere.ActiveTiles.Remove(tile);
            atmosphere.MapTiles.Remove(tile);
            atmosphere.HotspotTiles.Remove(tile);
            atmosphere.SuperconductivityTiles.Remove(tile);
            atmosphere.HighPressureDelta.Remove(tile);
        }

        foreach (var group in touchedGroups)
        {
            if (group.Tiles.Count == 0)
                ExcitedGroupDispose(atmosphere, group);
        }

        FilterQueue(atmosphere.CurrentRunTiles, tile => !removed.Contains(tile));
        FilterQueue(atmosphere.CurrentRunInvalidatedTiles, tile => !removed.Contains(tile));
        FilterQueue(atmosphere.CurrentRunExcitedGroups, group => !group.Disposed);
        atmosphere.PossiblyDisconnectedTiles.RemoveAll(removed.Contains);

        foreach (var tile in atmosphere.Tiles.Values.Concat(atmosphere.ZLevelTiles.Values))
        {
            for (var i = 0; i < tile.AdjacentTiles.Length; i++)
            {
                if (tile.AdjacentTiles[i] != null && removed.Contains(tile.AdjacentTiles[i]!))
                    tile.AdjacentTiles[i] = null;
            }

            if (tile.AdjacentTileAbove != null && removed.Contains(tile.AdjacentTileAbove))
                tile.AdjacentTileAbove = null;
            if (tile.AdjacentTileBelow != null && removed.Contains(tile.AdjacentTileBelow))
                tile.AdjacentTileBelow = null;
        }

        if (level == 0)
        {
            foreach (var tile in removed)
            {
                atmosphere.Tiles.Remove(tile.GridIndices);
                atmosphere.InvalidatedCoords.Add(tile.GridIndices);
                InvalidateVisuals((gridUid, CompOrNull<GasTileOverlayComponent>(gridUid)), tile.GridIndices);
            }
        }
        else
        {
            foreach (var tile in removed)
            {
                var indices = new ZLevelTileIndices(tile.GridIndices.X, tile.GridIndices.Y, level);
                atmosphere.ZLevelTiles.Remove(indices);
                atmosphere.InvalidatedZLevelCoords.Add(indices);
                InvalidateVisuals((gridUid, CompOrNull<GasTileOverlayComponent>(gridUid)), indices);
            }
        }
    }

    private static IEnumerable<TileAtmosphere> GetLayerTiles(GridAtmosphereComponent atmosphere, int level)
    {
        return level == 0
            ? atmosphere.Tiles.Values
            : atmosphere.ZLevelTiles
                .Where(entry => entry.Key.Z == level)
                .Select(entry => entry.Value);
    }

    private static void FilterQueue<T>(Queue<T> queue, Predicate<T> keep)
    {
        var count = queue.Count;
        for (var i = 0; i < count; i++)
        {
            var item = queue.Dequeue();
            if (keep(item))
                queue.Enqueue(item);
        }
    }
}
