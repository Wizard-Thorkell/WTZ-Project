// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.ZLevel;

[Serializable, NetSerializable]
public readonly record struct ZLevelStructuralDebugTile(int Stability, bool PendingCollapse);

/// <summary>
/// Enables or disables structural diagnostics for one client.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZLevelStructuralOverlayToggledEvent(bool enabled) : EntityEventArgs
{
    public readonly bool Enabled = enabled;
}

/// <summary>
/// Carries sparse structural state only to clients that explicitly enabled diagnostics.
/// </summary>
[Serializable, NetSerializable]
public sealed class ZLevelStructuralOverlaySnapshotEvent(
    Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>> grids,
    bool replaceAll) : EntityEventArgs
{
    public readonly Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>> Grids = grids;
    public readonly bool ReplaceAll = replaceAll;
}
