// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Grants an entity controlled vertical movement inside a native Z-level grid.
/// Runtime flight state is networked, but deliberately not map-serialized.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZLevelSystem))]
public sealed partial class ZLevelFlightComponent : Component
{
    /// <summary>
    /// Stable offset inside the target floor. One half keeps a flyer away from both boundaries.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float HoverOffset = 0.5f;

    [DataField, AutoNetworkedField]
    public float VerticalAcceleration = 8f;

    [DataField, AutoNetworkedField]
    public float MaximumVerticalSpeed = 2f;

    [AutoNetworkedField]
    public bool Active;

    [AutoNetworkedField]
    public int TargetLocalZLevel;

    [AutoNetworkedField]
    public float TargetLocalZOffset = 0.5f;

    /// <summary>
    /// Process-local frame identity used to reject reparenting while in flight.
    /// </summary>
    public EntityUid? ActiveGridUid;
}
