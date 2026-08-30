// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Grants player-facing controls for an existing <see cref="ZLevelFlightComponent"/>.
/// Actions are runtime state and are available only while the entity is on a configured Z-level grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZLevelFlightControlSystem))]
public sealed partial class ZLevelFlightControlsComponent : Component
{
    [DataField]
    public EntProtoId ToggleAction = "ActionZLevelFlightToggle";

    [DataField]
    public EntProtoId MoveUpAction = "ActionZLevelFlightUp";

    [DataField]
    public EntProtoId MoveDownAction = "ActionZLevelFlightDown";

    [AutoNetworkedField]
    public EntityUid? ToggleActionEntity;

    [AutoNetworkedField]
    public EntityUid? MoveUpActionEntity;

    [AutoNetworkedField]
    public EntityUid? MoveDownActionEntity;
}
