// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel;

[Flags]
public enum ZLevelBoundaryChannels : ushort
{
    None = 0,
    Body = 1 << 0,
    TraversalUp = 1 << 1,
    TraversalDown = 1 << 2,
    Atmosphere = 1 << 3,
    Visibility = 1 << 4,
    Interaction = 1 << 5,
    Sound = 1 << 6,
    Effects = 1 << 7,
    Projectile = 1 << 8,
    Explosion = 1 << 9,
    Weather = 1 << 10,
    Traversal = TraversalUp | TraversalDown,
    All = ushort.MaxValue,
}

/// <summary>
/// The resolved state of one vertical boundary between adjacent tiles.
/// </summary>
public readonly record struct ZLevelBoundaryState(
    ZLevelTileIndices Lower,
    ZLevelTileIndices Upper,
    bool DefaultOpen,
    ZLevelBoundaryChannels ForcedOpen,
    ZLevelBoundaryChannels ForcedClosed,
    ZLevelBoundaryChannels OpenChannels)
{
    public bool IsOpen(ZLevelBoundaryChannels channels)
    {
        return channels != ZLevelBoundaryChannels.None &&
               (OpenChannels & channels) == channels;
    }
}

/// <summary>
/// Raised on anchored entities at a boundary tile so content can contribute
/// channel-specific overrides. Forced-closed channels take precedence.
/// </summary>
[ByRefEvent]
public struct ZLevelBoundaryQueryEvent(
    Entity<MapGridComponent> grid,
    Vector2i tile,
    int lowerZ)
{
    public readonly Entity<MapGridComponent> Grid = grid;
    public readonly Vector2i Tile = tile;
    public readonly int LowerZ = lowerZ;

    public ZLevelBoundaryChannels ForcedOpen { get; private set; }
    public ZLevelBoundaryChannels ForcedClosed { get; private set; }

    public void ForceOpen(ZLevelBoundaryChannels channels)
    {
        ForcedOpen |= channels;
    }

    public void ForceClosed(ZLevelBoundaryChannels channels)
    {
        ForcedClosed |= channels;
    }
}

/// <summary>
/// Raised on a grid when an explicit boundary provider is added, removed,
/// moved, or reconfigured.
/// </summary>
[ByRefEvent]
public readonly record struct ZLevelBoundaryChangedEvent(
    Entity<MapGridComponent> Grid,
    Vector2i Tile,
    int LowerZ);
