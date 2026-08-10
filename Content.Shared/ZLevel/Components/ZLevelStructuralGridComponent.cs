// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Map;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Opts a grid into sparse structural stability and stores its last authoritative result.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelStructuralGridComponent : Component
{
    [DataField]
    public bool CollapseEnabled = true;

    [DataField]
    public float CollapseDelayMin = 3f;

    [DataField]
    public float CollapseDelayMax = 10f;

    [ViewVariables]
    public readonly Dictionary<ZLevelTileIndices, int> Stability = new();

    [ViewVariables]
    public readonly Dictionary<ZLevelTileIndices, ZLevelPendingCollapse> PendingCollapses = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Cores = new();

    [ViewVariables]
    public readonly HashSet<EntityUid> Supports = new();

    [ViewVariables]
    public uint Revision;
}

public readonly record struct ZLevelPendingCollapse(TimeSpan At, uint Revision);
