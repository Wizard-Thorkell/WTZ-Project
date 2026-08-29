// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;

namespace Content.Client.ZLevel.UI;

[UsedImplicitly]
public sealed class ZLevelElevatorBoundUserInterface(EntityUid owner, Enum uiKey)
    : BoundUserInterface(owner, uiKey)
{
    private ZLevelElevatorWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ZLevelElevatorWindow>();
        _window.RequestFloor += floor => SendMessage(new ZLevelElevatorRequestFloorMessage(floor));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ZLevelElevatorBoundUserInterfaceState elevatorState)
            _window?.UpdateState(elevatorState);
    }
}
