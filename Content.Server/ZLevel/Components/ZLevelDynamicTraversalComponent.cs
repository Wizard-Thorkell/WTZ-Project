// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Server.ZLevel.Components;

/// <summary>
/// Adds runtime availability, power, and waiting policy to an authored
/// traversal. Mutate this component through <c>ZLevelTraversalGraphSystem</c>
/// so cached routes observe every state change.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelDynamicTraversalComponent : Component
{
    /// <summary>
    /// Administrative or mechanical master switch.
    /// </summary>
    [DataField]
    public bool Enabled = true;

    /// <summary>
    /// Whether the connector can currently accept a traversal request.
    /// </summary>
    [DataField]
    public bool Callable = true;

    /// <summary>
    /// Whether an attached APC power receiver must report power.
    /// </summary>
    [DataField]
    public bool RequirePower = true;

    /// <summary>
    /// Time needed to call or position the connector before normal travel.
    /// </summary>
    [DataField]
    public TimeSpan WaitDelay = TimeSpan.Zero;

    /// <summary>
    /// Abstract pathfinding cost for expected waiting, independent of travel.
    /// </summary>
    [DataField]
    public float WaitNavigationCost;
}
