using Robust.Shared.Map;

namespace Content.Server.NPC.Pathfinding;

/// <summary>
/// Connects 2 disparate locations.
/// </summary>
/// <remarks>
/// For example, 2 docking airlocks connecting 2 graphs, or an actual portal on the same graph.
/// </remarks>
public struct PathPortal
{
    // Assume for now it's 2-way and code 1-ways later.
    public readonly int Handle;
    public readonly EntityCoordinates CoordinatesA;
    public readonly EntityCoordinates CoordinatesB;
    public readonly int LocalZA;
    public readonly int LocalZB;

    // TODO: Whenever the chunk rebuilds need to add a neighbor.
    public PathPortal(
        int handle,
        EntityCoordinates coordsA,
        int localZA,
        EntityCoordinates coordsB,
        int localZB)
    {
        Handle = handle;
        CoordinatesA = coordsA;
        LocalZA = localZA;
        CoordinatesB = coordsB;
        LocalZB = localZB;
    }

    public override int GetHashCode()
    {
        return Handle;
    }
}
