// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.ZLevel;

[Serializable, NetSerializable]
public sealed partial class ZLevelTraversalDoAfterEvent : SimpleDoAfterEvent
{
}
