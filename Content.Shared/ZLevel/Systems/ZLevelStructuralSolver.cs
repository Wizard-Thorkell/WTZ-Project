// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel.Systems;

/// <summary>
/// Pure multi-source structural flood fill over sparse grid-local Z-level tiles.
/// </summary>
public static class ZLevelStructuralSolver
{
    private static readonly Vector2i[] CardinalOffsets =
    [
        new(1, 0),
        new(-1, 0),
        new(0, 1),
        new(0, -1),
    ];

    public static Dictionary<ZLevelTileIndices, int> Solve(
        IReadOnlySet<ZLevelTileIndices> liveNodes,
        IReadOnlyList<ZLevelStructuralSeed> seeds,
        IReadOnlyDictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>> bridges)
    {
        var stability = new Dictionary<ZLevelTileIndices, int>();
        var queue = new Queue<(ZLevelTileIndices Node, int Value)>();

        foreach (var seed in seeds)
        {
            Seed(stability, queue, liveNodes, seed.Node, seed.Strength);
        }

        Process(queue, stability, liveNodes, bridges);
        return stability;
    }

    /// <summary>
    /// Processes up to <paramref name="budget"/> queued nodes, or the entire queue when omitted.
    /// </summary>
    public static int Process(
        Queue<(ZLevelTileIndices Node, int Value)> queue,
        Dictionary<ZLevelTileIndices, int> stability,
        IReadOnlySet<ZLevelTileIndices> liveNodes,
        IReadOnlyDictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>> bridges,
        int? budget = null)
    {
        var processed = 0;
        while ((!budget.HasValue || processed < budget.Value) && queue.TryDequeue(out var entry))
        {
            processed++;
            if (stability.GetValueOrDefault(entry.Node) != entry.Value)
                continue;

            foreach (var offset in CardinalOffsets)
            {
                var neighbor = new ZLevelTileIndices(
                    entry.Node.X + offset.X,
                    entry.Node.Y + offset.Y,
                    entry.Node.Z);
                Seed(stability, queue, liveNodes, neighbor, entry.Value - 1);
            }

            if (bridges.TryGetValue(entry.Node, out var partners))
            {
                foreach (var bridge in partners)
                {
                    var transferred = Math.Min(entry.Value - Math.Max(0, bridge.Loss), bridge.Strength);
                    Seed(stability, queue, liveNodes, bridge.Node, transferred);
                }
            }

        }

        return queue.Count;
    }

    public static void Seed(
        Dictionary<ZLevelTileIndices, int> stability,
        Queue<(ZLevelTileIndices Node, int Value)> queue,
        IReadOnlySet<ZLevelTileIndices> liveNodes,
        ZLevelTileIndices node,
        int value)
    {
        if (value <= 0 || !liveNodes.Contains(node) || value <= stability.GetValueOrDefault(node))
            return;

        stability[node] = value;
        queue.Enqueue((node, value));
    }
}

public readonly record struct ZLevelStructuralSeed(ZLevelTileIndices Node, int Strength);

public readonly record struct ZLevelStructuralBridge(ZLevelTileIndices Node, int Strength, int Loss);
