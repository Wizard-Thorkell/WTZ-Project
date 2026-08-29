using Robust.Shared.Serialization;

namespace Content.Shared.NPC;

[Serializable, NetSerializable]
public sealed class PathBreadcrumbsMessage : EntityEventArgs
{
    public Dictionary<NetEntity, Dictionary<PathfindingChunkKey, List<PathfindingBreadcrumb>>> Breadcrumbs = new();
}

[Serializable, NetSerializable]
public sealed class PathBreadcrumbsRefreshMessage : EntityEventArgs
{
    public NetEntity GridUid;
    public PathfindingChunkKey Key;
    public List<PathfindingBreadcrumb> Data = new();
}

[Serializable, NetSerializable]
public sealed class PathPolysMessage : EntityEventArgs
{
    public Dictionary<NetEntity, Dictionary<PathfindingChunkKey, Dictionary<Vector2i, List<DebugPathPoly>>>> Polys = new();
}
