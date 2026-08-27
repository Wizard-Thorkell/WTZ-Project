// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

/// <summary>
/// Generates repeatable multi-floor workloads without maintaining copied YAML maps.
/// </summary>
internal static class ZLevelStressFixtureBuilder
{
    public const int StationSize = 24;
    public const int MovingGridSize = 8;

    private static readonly Vector2i[] StationCandidateTiles =
    [
        new(9, 10),
        new(10, 10),
        new(13, 10),
        new(14, 10),
        new(10, 13),
        new(13, 13),
        new(10, 14),
        new(13, 14),
    ];

    private static readonly Vector2i[] MovingCandidateTiles =
    [
        new(2, 2),
        new(4, 2),
        new(2, 4),
        new(4, 4),
    ];

    private static readonly Vector2i[] CardinalNeighbors =
    [
        new(1, 0),
        new(0, 1),
        new(-1, 0),
        new(0, -1),
    ];

    public static ZLevelStressFixture Build(
        IEntityManager entityManager,
        IMapManager mapManager,
        EntityUid mapUid,
        MapId mapId,
        EntityUid stationGridUid,
        int floorCount,
        Tile floorTile,
        string gravityGeneratorPrototype)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(floorCount, 2);

        var map = entityManager.System<SharedMapSystem>();
        var transform = entityManager.System<SharedTransformSystem>();
        var zLevels = entityManager.System<SharedZLevelSystem>();
        var zLevelMaps = entityManager.System<SharedZLevelMapSystem>();
        var stationGrid = entityManager.GetComponent<MapGridComponent>(stationGridUid);
        var movingGrid = mapManager.CreateGridEntity(mapId);
        var movingFrameOrigin = floorCount + 2;

        // The fixture is authored atomically. Letting 2D grid splitting observe its
        // temporary construction state would replace the UIDs before all layers exist.
        stationGrid.CanSplit = false;
        movingGrid.Comp.CanSplit = false;

        zLevelMaps.Configure(
            mapUid,
            0,
            floorCount - 1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);

        transform.SetLocalPosition(movingGrid.Owner, new Vector2(6f, -2f));
        transform.SetLocalRotation(movingGrid.Owner, Angle.FromDegrees(12));
        transform.SetZLevelFrameOrigin(movingGrid.Owner, movingFrameOrigin);

        var stationTiles = new HashSet<ZLevelTileIndices>();
        var movingTiles = new HashSet<ZLevelTileIndices>();
        PopulateGrid(
            map,
            stationGridUid,
            stationGrid,
            floorCount,
            StationSize,
            floorTile,
            IsStationTile,
            IsStationOpening,
            stationTiles);
        PopulateGrid(
            map,
            movingGrid.Owner,
            movingGrid.Comp,
            floorCount,
            MovingGridSize,
            floorTile,
            IsMovingGridTile,
            IsMovingGridOpening,
            movingTiles);

        var boundarySamples = new List<ZLevelStressBoundarySample>(
            (StationSize * StationSize + MovingGridSize * MovingGridSize) * (floorCount - 1));
        AddBoundarySamples(boundarySamples, stationGridUid, StationSize, floorCount);
        AddBoundarySamples(boundarySamples, movingGrid.Owner, MovingGridSize, floorCount);

        var gravitySamples = new List<ZLevelStressGravitySample>();
        AddGravitySamples(gravitySamples, stationGridUid, stationTiles, floorCount - 1);
        AddGravitySamples(gravitySamples, movingGrid.Owner, movingTiles, floorCount - 1);

        var candidates = new List<EntityUid>();
        SpawnCandidates(entityManager, zLevels, stationGridUid, floorCount, StationCandidateTiles, candidates);
        SpawnCandidates(entityManager, zLevels, movingGrid.Owner, floorCount, MovingCandidateTiles, candidates);

        var stationGenerator = SpawnGravityGenerator(
            entityManager,
            zLevels,
            stationGridUid,
            new Vector2i(10, 10),
            gravityGeneratorPrototype);
        var movingGenerator = SpawnGravityGenerator(
            entityManager,
            zLevels,
            movingGrid.Owner,
            new Vector2i(2, 2),
            gravityGeneratorPrototype);

        var (openBoundaries, closedBoundaries) = CountAuthoredBoundaries(stationTiles, floorCount);
        var (movingOpen, movingClosed) = CountAuthoredBoundaries(movingTiles, floorCount);
        var sealedColumns = CountSealedColumns(stationTiles, StationSize, floorCount) +
                            CountSealedColumns(movingTiles, MovingGridSize, floorCount);

        return new ZLevelStressFixture(
            mapUid,
            mapId,
            stationGridUid,
            movingGrid.Owner,
            floorCount,
            stationTiles.Count,
            movingTiles.Count,
            openBoundaries + movingOpen,
            closedBoundaries + movingClosed,
            sealedColumns,
            movingFrameOrigin,
            boundarySamples,
            gravitySamples,
            candidates,
            [stationGenerator, movingGenerator]);
    }

    private static void PopulateGrid(
        SharedMapSystem map,
        EntityUid gridUid,
        MapGridComponent grid,
        int floorCount,
        int size,
        Tile floorTile,
        Func<int, int, bool> containsTile,
        Func<int, int, int, bool> isOpening,
        HashSet<ZLevelTileIndices> authoredTiles)
    {
        for (var z = 0; z < floorCount; z++)
        {
            var layer = z;
            foreach (var tile in GetConnectedTileOrder(
                         size,
                         (x, y) => containsTile(x, y) && !isOpening(x, y, layer)))
            {
                var indices = new ZLevelTileIndices(tile.X, tile.Y, z);
                map.SetZLevelTile(gridUid, grid, indices, floorTile);
                authoredTiles.Add(indices);
            }
        }
    }

    private static IReadOnlyList<Vector2i> GetConnectedTileOrder(
        int size,
        Func<int, int, bool> containsTile)
    {
        var remaining = new HashSet<Vector2i>();
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                if (containsTile(x, y))
                    remaining.Add(new Vector2i(x, y));
            }
        }

        if (remaining.Count == 0)
            return Array.Empty<Vector2i>();

        var start = remaining.OrderBy(tile => tile.X).ThenBy(tile => tile.Y).First();
        var queue = new Queue<Vector2i>();
        var ordered = new List<Vector2i>(remaining.Count);
        remaining.Remove(start);
        queue.Enqueue(start);

        while (queue.TryDequeue(out var tile))
        {
            ordered.Add(tile);
            foreach (var offset in CardinalNeighbors)
            {
                var neighbor = tile + offset;
                if (!remaining.Remove(neighbor))
                    continue;

                queue.Enqueue(neighbor);
            }
        }

        if (remaining.Count != 0)
        {
            throw new InvalidOperationException(
                $"Stress fixture layer has {remaining.Count} disconnected tiles.");
        }

        return ordered;
    }

    private static void AddBoundarySamples(
        List<ZLevelStressBoundarySample> samples,
        EntityUid gridUid,
        int size,
        int floorCount)
    {
        for (var lowerZ = 0; lowerZ < floorCount - 1; lowerZ++)
        {
            for (var x = 0; x < size; x++)
            {
                for (var y = 0; y < size; y++)
                {
                    samples.Add(new ZLevelStressBoundarySample(gridUid, new Vector2i(x, y), lowerZ));
                }
            }
        }
    }

    private static void AddGravitySamples(
        List<ZLevelStressGravitySample> samples,
        EntityUid gridUid,
        HashSet<ZLevelTileIndices> authoredTiles,
        int queryLevel)
    {
        foreach (var tile in authoredTiles
                     .Select(indices => new Vector2i(indices.X, indices.Y))
                     .Distinct()
                     .OrderBy(indices => indices.X)
                     .ThenBy(indices => indices.Y))
        {
            samples.Add(new ZLevelStressGravitySample(gridUid, tile, queryLevel));
        }
    }

    private static void SpawnCandidates(
        IEntityManager entityManager,
        SharedZLevelSystem zLevels,
        EntityUid gridUid,
        int floorCount,
        IReadOnlyList<Vector2i> candidateTiles,
        List<EntityUid> candidates)
    {
        for (var z = 0; z < floorCount; z++)
        {
            foreach (var tile in candidateTiles)
            {
                var coordinates = new EntityCoordinates(
                    gridUid,
                    new Vector2(tile.X + 0.5f, tile.Y + 0.5f));
                var candidate = entityManager.SpawnEntity(null, coordinates);
                zLevels.SetZLevelPosition(candidate, z);
                candidates.Add(candidate);
            }
        }
    }

    private static EntityUid SpawnGravityGenerator(
        IEntityManager entityManager,
        SharedZLevelSystem zLevels,
        EntityUid gridUid,
        Vector2i tile,
        string prototype)
    {
        var coordinates = new EntityCoordinates(
            gridUid,
            new Vector2(tile.X + 0.5f, tile.Y + 0.5f));
        var generator = entityManager.SpawnEntity(prototype, coordinates);
        entityManager.GetComponent<ApcPowerReceiverComponent>(generator).NeedsPower = false;
        zLevels.SetZLevelPosition(generator, 0);
        return generator;
    }

    private static (int Open, int Closed) CountAuthoredBoundaries(
        HashSet<ZLevelTileIndices> tiles,
        int floorCount)
    {
        var columns = tiles
            .Select(indices => new Vector2i(indices.X, indices.Y))
            .Distinct()
            .ToArray();
        var open = 0;
        var closed = 0;

        foreach (var column in columns)
        {
            for (var lowerZ = 0; lowerZ < floorCount - 1; lowerZ++)
            {
                var lower = new ZLevelTileIndices(column.X, column.Y, lowerZ);
                var upper = new ZLevelTileIndices(column.X, column.Y, lowerZ + 1);
                if (!tiles.Contains(lower) && !tiles.Contains(upper))
                    continue;

                if (tiles.Contains(upper))
                    closed++;
                else
                    open++;
            }
        }

        return (open, closed);
    }

    private static int CountSealedColumns(
        HashSet<ZLevelTileIndices> tiles,
        int size,
        int floorCount)
    {
        var count = 0;
        for (var x = 0; x < size; x++)
        {
            for (var y = 0; y < size; y++)
            {
                var sealedColumn = true;
                for (var z = 0; z < floorCount; z++)
                {
                    if (tiles.Contains(new ZLevelTileIndices(x, y, z)))
                        continue;

                    sealedColumn = false;
                    break;
                }

                if (sealedColumn)
                    count++;
            }
        }

        return count;
    }

    private static bool IsStationTile(int x, int y)
    {
        var horizontalCorridor = x is >= 1 and <= 22 && y is >= 10 and <= 13;
        var verticalCorridor = y is >= 1 and <= 22 && x is >= 10 and <= 13;
        var northWestRoom = x is >= 2 and <= 8 && y is >= 2 and <= 8;
        var northEastRoom = x is >= 15 and <= 21 && y is >= 2 and <= 8;
        var southWestRoom = x is >= 2 and <= 8 && y is >= 15 and <= 21;
        var southEastRoom = x is >= 15 and <= 21 && y is >= 15 and <= 21;
        var horizontalConnector = x is >= 8 and <= 15 && (y == 5 || y == 18);
        var verticalConnector = y is >= 8 and <= 15 && (x == 5 || x == 18);

        return horizontalCorridor ||
               verticalCorridor ||
               northWestRoom ||
               northEastRoom ||
               southWestRoom ||
               southEastRoom ||
               horizontalConnector ||
               verticalConnector;
    }

    private static bool IsStationOpening(int x, int y, int z)
    {
        if (z == 0)
            return false;

        return (x == 11 && y == 11) ||
               (x == 12 && y == 12) ||
               (z % 2 == 0 && x == 10 && y == 12);
    }

    private static bool IsMovingGridTile(int x, int y)
    {
        return !((x == 0 || x == 7) && (y == 0 || y == 7));
    }

    private static bool IsMovingGridOpening(int x, int y, int z)
    {
        return z > 0 && x == 3 && y == 3;
    }
}

internal sealed record ZLevelStressFixture(
    EntityUid MapUid,
    MapId MapId,
    EntityUid StationGridUid,
    EntityUid MovingGridUid,
    int FloorCount,
    int StationTileCount,
    int MovingGridTileCount,
    int OpenBoundaryCount,
    int ClosedBoundaryCount,
    int SealedColumnCount,
    int MovingGridFrameOrigin,
    IReadOnlyList<ZLevelStressBoundarySample> BoundarySamples,
    IReadOnlyList<ZLevelStressGravitySample> GravitySamples,
    IReadOnlyList<EntityUid> CandidateEntities,
    IReadOnlyList<EntityUid> GravityGenerators);

internal readonly record struct ZLevelStressBoundarySample(
    EntityUid GridUid,
    Vector2i Tile,
    int LowerZ);

internal readonly record struct ZLevelStressGravitySample(
    EntityUid GridUid,
    Vector2i Tile,
    int QueryLevel);
