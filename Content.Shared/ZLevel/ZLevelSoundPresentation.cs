// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.ZLevel;

/// <summary>
/// One server-authorized presentation of an existing positional audio stream for one viewer.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct ZLevelSoundPresentation(
    NetEntity Audio,
    NetEntity Viewer,
    MapId MapId,
    Vector2 ListenerPosition,
    int ListenerWorldZ,
    Vector2 PortalPosition,
    float Distance,
    float Transmission);

/// <summary>
/// Replaces all cross-floor sound presentations authorized for one session.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZLevelSoundPresentationSnapshotEvent(
    ZLevelSoundPresentation[] presentations) : EntityEventArgs
{
    public readonly ZLevelSoundPresentation[] Presentations = presentations;
}
