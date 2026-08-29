using Robust.Shared.Serialization;

namespace Content.Shared.NPC;

/// <summary>
/// Identifies one local navigation chunk on one grid-local floor.
/// </summary>
[Serializable, NetSerializable]
public readonly record struct PathfindingChunkKey(Vector2i Origin, int LocalZ);
