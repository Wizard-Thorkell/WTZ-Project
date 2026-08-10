// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Conducts stability between the anchored tile and an adjacent vertical layer.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelStructuralSupportComponent : Component
{
    [DataField]
    public int Strength = 8;

    [DataField]
    public int TransferLoss;

    /// <summary>
    /// Vertical offset of the tile connected by this support. Must be -1 or 1.
    /// </summary>
    [DataField]
    public int TargetOffset = 1;

    [ViewVariables]
    public EntityUid? IndexedGrid;
}
