// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    public const int MaximumZLevelPathStateExpansions = 4_096;
    public const int MaximumZLevelLocalPaths = 4_096;
    public const int MaximumZLevelTraversalEdges = 65_536;

    [Dependency] private readonly IConfigurationManager _configuration = default!;

    private int _maxZLevelPathStateExpansions = 64;
    private int _maxZLevelLocalPaths = 128;
    private int _maxZLevelTraversalEdges = 512;

    public ZLevelPathSearchBudget CreateDefaultZLevelPathBudget()
    {
        return new ZLevelPathSearchBudget(
            _maxZLevelPathStateExpansions,
            _maxZLevelLocalPaths,
            _maxZLevelTraversalEdges);
    }

    private void InitializeZLevelRoutes()
    {
        Subs.CVar(
            _configuration,
            CCVars.ZLevelPathfindingMaxStateExpansions,
            value => _maxZLevelPathStateExpansions = Math.Clamp(
                value,
                0,
                MaximumZLevelPathStateExpansions),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelPathfindingMaxLocalPaths,
            value => _maxZLevelLocalPaths = Math.Clamp(value, 0, MaximumZLevelLocalPaths),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelPathfindingMaxTraversalEdges,
            value => _maxZLevelTraversalEdges = Math.Clamp(
                value,
                0,
                MaximumZLevelTraversalEdges),
            true);
    }
}
