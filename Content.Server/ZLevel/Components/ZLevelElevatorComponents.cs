// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel;

namespace Content.Server.ZLevel.Components;

/// <summary>
/// A single physical elevator cabin. Cabins and stops are grouped by shaft ID,
/// grid, and tile so identical IDs can be reused in separate shafts.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelElevatorCabinComponent : Component
{
    [DataField(required: true)]
    public string ShaftId = string.Empty;

    [DataField]
    public TimeSpan TravelTimePerLevel = TimeSpan.FromSeconds(2);

    [DataField]
    public float IdlePowerDraw = 100f;

    [DataField]
    public float TravelPowerDraw = 2_500f;

    [DataField]
    public int MaxTravelLevels = 16;

    [DataField]
    public int PassengerLimit = 32;

    /// <summary>
    /// Fixed route cost for calling and boarding this elevator.
    /// </summary>
    [DataField]
    public float NavigationCallCost = 4f;

    /// <summary>
    /// Route cost added for every crossed local Z-level.
    /// </summary>
    [DataField]
    public float NavigationCostPerLevel = 4f;

    [DataField]
    public bool RequirePower = true;

    [ViewVariables(VVAccess.ReadOnly)]
    public ZLevelElevatorState State = ZLevelElevatorState.Idle;

    [ViewVariables(VVAccess.ReadOnly)]
    public int? TargetLevel;

    [ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan ArrivalTime;
}

/// <summary>
/// Declares one mapper-authored landing in an elevator shaft.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelElevatorStopComponent : Component
{
    [DataField(required: true)]
    public string ShaftId = string.Empty;

    /// <summary>
    /// Optional display name. Empty labels fall back to the local Z number.
    /// </summary>
    [DataField]
    public string Label = string.Empty;
}

/// <summary>
/// Opens either the cabin floor selector or a landing call control.
/// The shaft ID is owned by the cabin or stop on the same entity.
/// </summary>
[RegisterComponent]
public sealed partial class ZLevelElevatorControlComponent : Component
{
    [DataField]
    public ZLevelElevatorControlMode Mode = ZLevelElevatorControlMode.Cabin;
}
