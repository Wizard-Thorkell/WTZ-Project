// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Resolves which solid Z-level tiles are structurally connected to active gravity sources.
/// </summary>
public static class ZLevelGravitySolver
{
    private static readonly ZLevelTileIndices[] NeighborOffsets =
    [
        new(1, 0, 0),
        new(-1, 0, 0),
        new(0, 1, 0),
        new(0, -1, 0),
        new(0, 0, 1),
        new(0, 0, -1),
    ];

    public static Dictionary<ZLevelTileIndices, ZLevelGravityAssignment> Solve(
        IReadOnlySet<ZLevelTileIndices> liveTiles,
        IReadOnlyList<ZLevelGravitySeed> seeds)
    {
        var assignments = new Dictionary<ZLevelTileIndices, ZLevelGravityAssignment>();
        var queue = new Queue<ZLevelTileIndices>();

        foreach (var seed in seeds.OrderBy(seed => seed.Source))
        {
            if (!liveTiles.Contains(seed.Node) || assignments.ContainsKey(seed.Node))
                continue;

            assignments.Add(seed.Node, new ZLevelGravityAssignment(seed.TargetLevel, 0, seed.Source));
            queue.Enqueue(seed.Node);
        }

        while (queue.TryDequeue(out var node))
        {
            var assignment = assignments[node];
            foreach (var offset in NeighborOffsets)
            {
                var neighbor = new ZLevelTileIndices(
                    node.X + offset.X,
                    node.Y + offset.Y,
                    node.Z + offset.Z);

                if (!liveTiles.Contains(neighbor) || assignments.ContainsKey(neighbor))
                    continue;

                assignments.Add(neighbor, assignment with { Distance = assignment.Distance + 1 });
                queue.Enqueue(neighbor);
            }
        }

        return assignments;
    }
}

public readonly record struct ZLevelGravitySeed(
    ZLevelTileIndices Node,
    int TargetLevel,
    EntityUid Source);

public readonly record struct ZLevelGravityAssignment(
    int TargetLevel,
    int Distance,
    EntityUid Source);
