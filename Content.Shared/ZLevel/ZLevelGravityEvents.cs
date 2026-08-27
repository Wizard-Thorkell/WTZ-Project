// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;

namespace Content.Shared.ZLevel;

/// <summary>
/// Raised on a gravity generator when its active state or parent changes.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelGravitySourceChangedEvent(EntityUid? OldGridUid = null);
