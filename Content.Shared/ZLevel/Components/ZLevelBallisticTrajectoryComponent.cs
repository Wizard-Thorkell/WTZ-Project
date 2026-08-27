// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// An opt-in ballistic route through a single grid's native Z-level frame.
/// Horizontal motion remains owned by Robust physics.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedZLevelBallisticSystem))]
public sealed partial class ZLevelBallisticTrajectoryComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid FrameUid;

    [DataField, AutoNetworkedField]
    public Vector2 Origin;

    [DataField, AutoNetworkedField]
    public Vector2 Direction;

    [DataField, AutoNetworkedField]
    public float PlanarDistance;

    [DataField, AutoNetworkedField]
    public int SourceLocalZ;

    [DataField, AutoNetworkedField]
    public int TargetLocalZ;

    [DataField, AutoNetworkedField]
    public int NextCrossing;

    [ViewVariables]
    public bool PendingCrossing;

    [ViewVariables]
    public bool CollisionDuringStep;

    [ViewVariables]
    public bool Ending;

    [ViewVariables]
    public Vector2 NominalMapVelocity;

    [ViewVariables]
    public Vector2 StepMapVelocity;

    [ViewVariables]
    public Vector2 NominalLinearVelocity;

    [ViewVariables]
    public Vector2 StepLinearVelocity;

    [ViewVariables]
    public float StepDuration;
}
