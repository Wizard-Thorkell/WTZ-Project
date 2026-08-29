// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Serialization;

namespace Content.Shared.ZLevel;

[Serializable, NetSerializable]
public enum ZLevelElevatorUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum ZLevelElevatorControlMode : byte
{
    Cabin,
    Landing,
}

[Serializable, NetSerializable]
public enum ZLevelElevatorState : byte
{
    Idle,
    Moving,
    Unpowered,
    Invalid,
}

[Serializable, NetSerializable]
public enum ZLevelElevatorVisuals : byte
{
    State,
}

[Serializable, NetSerializable]
public enum ZLevelElevatorVisualLayers : byte
{
    Main,
}

[Serializable, NetSerializable]
public readonly record struct ZLevelElevatorStopData(int LocalZ, string Label);

[Serializable, NetSerializable]
public sealed class ZLevelElevatorBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly ZLevelElevatorControlMode ControlMode;
    public readonly int ControlFloor;
    public readonly int? CurrentFloor;
    public readonly int? TargetFloor;
    public readonly ZLevelElevatorState State;
    public readonly TimeSpan ArrivalTime;
    public readonly TimeSpan TravelDuration;
    public readonly List<ZLevelElevatorStopData> Stops;

    public ZLevelElevatorBoundUserInterfaceState(
        ZLevelElevatorControlMode controlMode,
        int controlFloor,
        int? currentFloor,
        int? targetFloor,
        ZLevelElevatorState state,
        TimeSpan arrivalTime,
        TimeSpan travelDuration,
        List<ZLevelElevatorStopData> stops)
    {
        ControlMode = controlMode;
        ControlFloor = controlFloor;
        CurrentFloor = currentFloor;
        TargetFloor = targetFloor;
        State = state;
        ArrivalTime = arrivalTime;
        TravelDuration = travelDuration;
        Stops = stops;
    }
}

[Serializable, NetSerializable]
public sealed class ZLevelElevatorRequestFloorMessage(int targetFloor) : BoundUserInterfaceMessage
{
    public readonly int TargetFloor = targetFloor;
}
