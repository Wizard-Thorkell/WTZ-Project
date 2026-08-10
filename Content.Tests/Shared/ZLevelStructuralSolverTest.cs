// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Shared.Map;

namespace Content.Tests.Shared;

[TestFixture]
public sealed class ZLevelStructuralSolverTest
{
    [Test]
    public void StabilityAttenuatesAndRequiresASeed()
    {
        var nodes = new HashSet<ZLevelTileIndices>
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(2, 0, 0),
            new(10, 0, 0),
        };
        var seeds = new List<ZLevelStructuralSeed>
        {
            new(new ZLevelTileIndices(0, 0, 0), 2),
        };

        var result = ZLevelStructuralSolver.Solve(
            nodes,
            seeds,
            new Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>>());

        Assert.Multiple(() =>
        {
            Assert.That(result[new ZLevelTileIndices(0, 0, 0)], Is.EqualTo(2));
            Assert.That(result[new ZLevelTileIndices(1, 0, 0)], Is.EqualTo(1));
            Assert.That(result.ContainsKey(new ZLevelTileIndices(2, 0, 0)), Is.False);
            Assert.That(result.ContainsKey(new ZLevelTileIndices(10, 0, 0)), Is.False);
        });
    }

    [Test]
    public void VerticalSupportIsCappedAndBidirectional()
    {
        var lower = new ZLevelTileIndices(0, 0, 0);
        var upper = new ZLevelTileIndices(0, 0, 1);
        var upperNeighbor = new ZLevelTileIndices(1, 0, 1);
        var nodes = new HashSet<ZLevelTileIndices> { lower, upper, upperNeighbor };
        var bridges = new Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>>
        {
            [lower] = [new ZLevelStructuralBridge(upper, 4, 1)],
            [upper] = [new ZLevelStructuralBridge(lower, 4, 1)],
        };

        var fromBelow = ZLevelStructuralSolver.Solve(
            nodes,
            [new ZLevelStructuralSeed(lower, 10)],
            bridges);
        var fromAbove = ZLevelStructuralSolver.Solve(
            nodes,
            [new ZLevelStructuralSeed(upper, 3)],
            bridges);

        Assert.Multiple(() =>
        {
            Assert.That(fromBelow[upper], Is.EqualTo(4));
            Assert.That(fromBelow[upperNeighbor], Is.EqualTo(3));
            Assert.That(fromAbove[lower], Is.EqualTo(2));
        });
    }
}
