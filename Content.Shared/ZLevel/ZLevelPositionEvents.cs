// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Shared.ZLevel;

/// <summary>
/// Raised after an entity's effective discrete Z-level changes.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelPositionChangedEvent(int OldZLevel, int NewZLevel);
