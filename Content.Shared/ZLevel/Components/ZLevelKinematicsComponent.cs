// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// ZLevel experimental vertical motion state.
/// XY movement remains owned by normal transform + physics systems.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ZLevelKinematicsComponent : Component
{
    [DataField, AutoNetworkedField]
    public float VerticalVelocity;

    [DataField, AutoNetworkedField]
    public bool Grounded;

    /// <summary>
    /// Maximum number of layers the resolver can search downward for support.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxStepDownDepth = 2;

    [DataField, AutoNetworkedField]
    public float Gravity = 20f;

    [DataField, AutoNetworkedField]
    public float TerminalVelocity = 30f;
}
