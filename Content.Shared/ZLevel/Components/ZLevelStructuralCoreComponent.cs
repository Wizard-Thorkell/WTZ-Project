// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Seeds structural stability from the tile on which this entity is anchored.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelStructuralCoreComponent : Component
{
    [DataField]
    public int Strength = 20;

    [ViewVariables]
    public EntityUid? IndexedGrid;
}
