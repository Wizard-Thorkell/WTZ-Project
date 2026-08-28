// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevel.Components;

/// <summary>
/// Declares that a map is authored for native Z-level gameplay and records the
/// serialized contract needed to load and edit it safely.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
[Access(typeof(SharedZLevelMapSystem))]
public sealed partial class ZLevelMapComponent : Component
{
    public const int CurrentFormatVersion = 1;

    [DataField(required: true), AutoNetworkedField]
    public int FormatVersion = CurrentFormatVersion;

    [DataField, AutoNetworkedField]
    public int MinimumLevel;

    [DataField, AutoNetworkedField]
    public int MaximumLevel;

    [DataField, AutoNetworkedField]
    public int DefaultLevel;

    [DataField, AutoNetworkedField]
    public ZLevelDefaultBoundaryMode DefaultBoundaryMode = ZLevelDefaultBoundaryMode.TileAboveCloses;
}

/// <summary>
/// Defines the fallback used when no mapper-authored boundary entity overrides
/// a boundary between two adjacent levels.
/// </summary>
public enum ZLevelDefaultBoundaryMode : byte
{
    /// <summary>
    /// A non-empty tile on the upper level closes the boundary.
    /// </summary>
    TileAboveCloses,

    /// <summary>
    /// Boundaries stay open unless mapper-authored content closes them.
    /// </summary>
    ExplicitOnly,
}
