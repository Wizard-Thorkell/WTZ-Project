// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Server.ZLevel.Components;

/// <summary>
/// Authors one bounded flight corridor between adjacent floors. The marker is
/// placed on the supported source approach tile; offsets rotate with it.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelFlightNavigationComponent : Component
{
    /// <summary>
    /// Adjacent destination floor relative to the marker's local floor.
    /// </summary>
    [DataField]
    public int ZOffset = 1;

    /// <summary>
    /// Body-open tile used to cross the vertical boundary.
    /// </summary>
    [DataField]
    public Vector2i ApertureOffset = Vector2i.Zero;

    /// <summary>
    /// Supported destination approach tile on the destination floor.
    /// </summary>
    [DataField]
    public Vector2i DestinationOffset = new(1, 0);

    /// <summary>
    /// Whether the same corridor may be flown in the opposite direction.
    /// </summary>
    [DataField]
    public bool Bidirectional = true;

    /// <summary>
    /// Abstract cost of activating and vertically crossing this corridor.
    /// Horizontal approach distances are added by the graph.
    /// </summary>
    [DataField]
    public float NavigationCost = 4f;
}
