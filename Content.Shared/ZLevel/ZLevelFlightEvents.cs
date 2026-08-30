// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Shared.ZLevel;

public enum ZLevelFlightResult : byte
{
    Success,
    NoChange,
    MissingCapability,
    AlreadyActive,
    Inactive,
    Cancelled,
    InvalidTransform,
    InvalidGrid,
    UnconfiguredMap,
    InvalidCurrentPosition,
    InvalidTarget,
    InvalidConfiguration,
    Anchored,
    Contained,
    InvalidBodyType,
}

public enum ZLevelFlightStopReason : byte
{
    Requested,
    CapabilityRemoved,
    Anchored,
    Contained,
    InvalidBodyType,
    GridChanged,
    MapConfigurationChanged,
    InvalidState,
}

[ByRefEvent]
public record struct ZLevelFlightStartAttemptEvent(int TargetLocalZLevel, float TargetLocalZOffset)
{
    public bool Cancelled;
}

[ByRefEvent]
public readonly record struct ZLevelFlightStartedEvent(int TargetLocalZLevel, float TargetLocalZOffset);

[ByRefEvent]
public readonly record struct ZLevelFlightTargetChangedEvent(
    int OldLocalZLevel,
    float OldLocalZOffset,
    int NewLocalZLevel,
    float NewLocalZOffset);

[ByRefEvent]
public readonly record struct ZLevelFlightStoppedEvent(ZLevelFlightStopReason Reason);

[ByRefEvent]
public readonly record struct ZLevelFlightBoundaryBlockedEvent(
    int LowerLocalZ,
    int UpperLocalZ,
    int Direction);
