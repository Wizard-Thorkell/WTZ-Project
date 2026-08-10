// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Threading;
using System.Threading.Tasks;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Map;

namespace Content.Server.ZLevel.Structural;

/// <summary>
/// Time-sliced structural solve over an immutable main-thread snapshot.
/// </summary>
public sealed class ZLevelStructuralJob(
    double maxTime,
    HashSet<ZLevelTileIndices> liveNodes,
    List<ZLevelStructuralSeed> seeds,
    Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>> bridges,
    CancellationToken cancellation = default)
    : Job<Dictionary<ZLevelTileIndices, int>>(maxTime, cancellation)
{
    private const int BatchSize = 256;

    public IReadOnlySet<ZLevelTileIndices> LiveNodes => liveNodes;

    protected override async Task<Dictionary<ZLevelTileIndices, int>?> Process()
    {
        var stability = new Dictionary<ZLevelTileIndices, int>();
        var queue = new Queue<(ZLevelTileIndices Node, int Value)>();

        foreach (var seed in seeds)
        {
            ZLevelStructuralSolver.Seed(stability, queue, liveNodes, seed.Node, seed.Strength);
        }

        while (ZLevelStructuralSolver.Process(queue, stability, liveNodes, bridges, BatchSize) > 0)
        {
            await SuspendIfOutOfTime();
        }

        return stability;
    }
}
