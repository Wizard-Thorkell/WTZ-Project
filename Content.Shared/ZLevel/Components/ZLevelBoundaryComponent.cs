// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Contributes explicit behavior to the vertical boundary adjacent to an
/// anchored entity. Positive offsets address the boundary above the entity;
/// negative offsets address the boundary below it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZLevelBoundarySystem))]
public sealed partial class ZLevelBoundaryComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    /// <summary>
    /// Must be either -1 or 1.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BoundaryOffset = 1;

    [DataField, AutoNetworkedField]
    public ZLevelBoundaryChannels Opens = ZLevelBoundaryChannels.None;

    [DataField, AutoNetworkedField]
    public ZLevelBoundaryChannels Closes = ZLevelBoundaryChannels.None;
}
