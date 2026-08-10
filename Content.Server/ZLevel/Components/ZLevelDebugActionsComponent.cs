// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.ZLevel.Components;

[RegisterComponent]
public sealed partial class ZLevelDebugActionsComponent : Component
{
    [DataField]
    public EntProtoId MoveUpAction = "ActionZLevelMoveUp";

    [DataField]
    public EntProtoId MoveDownAction = "ActionZLevelMoveDown";

    [DataField]
    public EntProtoId MoveToTargetAction = "ActionZLevelMoveTarget";

    [DataField]
    public EntityUid? MoveUpActionEntity;

    [DataField]
    public EntityUid? MoveDownActionEntity;

    [DataField]
    public EntityUid? MoveToTargetActionEntity;
}
