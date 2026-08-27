// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel;

/// <summary>
/// Raised when an opt-in ballistic route reaches a closed projectile boundary.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelBallisticBoundaryHitEvent(
    EntityUid FrameUid,
    Vector2i Tile,
    int FromLocalZ,
    int ToLocalZ);
