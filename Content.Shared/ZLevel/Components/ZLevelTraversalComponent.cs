// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Marks an anchored structure as a deliberate connector between adjacent Z-levels.
/// This is the first non-debug traversal surface for stairs and ladders.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelTraversalComponent : Component
{
    /// <summary>
    /// Relative floor change applied when this traversal succeeds.
    /// </summary>
    [DataField(required: true)]
    public int ZOffset;

    /// <summary>
    /// If true, the destination floor must contain direct support at the target z-level.
    /// </summary>
    [DataField]
    public bool RequireDirectDestinationSupport = true;

    /// <summary>
    /// Time spent on the traversal before changing floors.
    /// </summary>
    [DataField]
    public TimeSpan TraversalDelay = TimeSpan.FromSeconds(2);
}
