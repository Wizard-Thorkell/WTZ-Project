// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.ZLevel;

[Serializable, NetSerializable]
public enum ZLevelMappingOperation : byte
{
    ConfigureMap,
    SetActiveLevel,
    CreateLevel,
    CopyLevel,
    DeleteLevel,
}

[Serializable, NetSerializable]
public sealed class ZLevelMappingRequestEvent : EntityEventArgs
{
    public NetEntity Map;
    public NetEntity Grid;
    public ZLevelMappingOperation Operation;
    public int SourceLevel;
    public int TargetLevel;
    public int MinimumLevel;
    public int MaximumLevel;
    public int DefaultLevel;
    public ZLevelDefaultBoundaryMode BoundaryMode;
}

[Serializable, NetSerializable]
public sealed class ZLevelMappingResultEvent(string message, bool error = false) : EntityEventArgs
{
    public string Message = message;
    public bool Error = error;
}
