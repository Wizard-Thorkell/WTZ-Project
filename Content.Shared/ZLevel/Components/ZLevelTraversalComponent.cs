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
    /// If true, the traversal may intentionally bypass the default ceiling block rule.
    /// Stairs and ladders use this to connect floors that would otherwise be sealed.
    /// </summary>
    [DataField]
    public bool OverridesBoundaryBlock = true;

    /// <summary>
    /// If true, the destination floor must contain direct support at the target z-level.
    /// </summary>
    [DataField]
    public bool RequireDirectDestinationSupport = true;
}
