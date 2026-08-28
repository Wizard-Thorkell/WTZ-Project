// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

namespace Content.Shared.ZLevel;

/// <summary>
/// Raised when map-level Z configuration starts, stops, or changes locally or
/// through replicated component state.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelMapConfigurationChangedEvent(EntityUid MapUid);
