namespace Content.Shared.Mapping;

/// <summary>
/// Marks an entity subtree as runtime-only for mapping snapshots.
/// </summary>
[RegisterComponent, UnsavedComponent]
public sealed partial class MappingSnapshotTransientComponent : Component;
