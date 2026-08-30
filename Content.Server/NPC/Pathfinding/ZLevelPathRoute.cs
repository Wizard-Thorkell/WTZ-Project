// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Immutable;
using Content.Server.ZLevel.Navigation;
using Robust.Shared.Map;

namespace Content.Server.NPC.Pathfinding;

public enum ZLevelPathRouteStatus : byte
{
    Success,
    InvalidRequest,
    NoPath,
    Cancelled,
    StateExpansionBudgetExceeded,
    LocalPathBudgetExceeded,
    TraversalEdgeBudgetExceeded,
    TopologyChanged,
    EnvironmentChanged,
    TopologyAndEnvironmentChanged,
    LocalNavigationChanged,
}

public enum ZLevelPathLegKind : byte
{
    Local,
    Traversal,
    Flight,
}

public enum ZLevelPathRouteValidationStatus : byte
{
    Valid,
    InvalidRoute,
    LocalNavigationChanged,
    TraversalChanged,
}

/// <summary>
/// One exact route endpoint with an explicit map and world floor.
/// </summary>
public readonly record struct ZLevelPathEndpoint(
    MapId MapId,
    EntityCoordinates Coordinates,
    int WorldZ);

/// <summary>
/// Caller-owned allowance for one hierarchical route search.
/// </summary>
public struct ZLevelPathSearchBudget
{
    public int RemainingStateExpansions;
    public int RemainingLocalPaths;
    public int RemainingTraversalEdges;

    public static ZLevelPathSearchBudget Unlimited =>
        new(int.MaxValue, int.MaxValue, int.MaxValue);

    public ZLevelPathSearchBudget(
        int remainingStateExpansions,
        int remainingLocalPaths,
        int remainingTraversalEdges)
    {
        RemainingStateExpansions = remainingStateExpansions;
        RemainingLocalPaths = remainingLocalPaths;
        RemainingTraversalEdges = remainingTraversalEdges;
    }

    public bool IsValid =>
        RemainingStateExpansions >= 0 &&
        RemainingLocalPaths >= 0 &&
        RemainingTraversalEdges >= 0;

    internal bool TryTakeStateExpansion()
    {
        if (RemainingStateExpansions <= 0)
            return false;

        RemainingStateExpansions--;
        return true;
    }

    internal bool TryTakeLocalPaths(int count)
    {
        if (count < 0 || RemainingLocalPaths < count)
            return false;

        RemainingLocalPaths -= count;
        return true;
    }

    internal bool TryTakeTraversalEdge()
    {
        if (RemainingTraversalEdges <= 0)
            return false;

        RemainingTraversalEdges--;
        return true;
    }
}

public readonly record struct ZLevelPathSearchDiagnostics(
    int StatesExpanded,
    int LocalPathsRequested,
    int TraversalEdgesEvaluated,
    int FlightEdgesEvaluated,
    long TopologyRevision,
    long EnvironmentRevision);

/// <summary>
/// One immutable local or vertical step. Vertical transitions retain the exact
/// authored edge and local paths retain validity-checkable native polygons.
/// </summary>
public readonly struct ZLevelPathLeg
{
    public ZLevelPathLegKind Kind { get; }
    public ZLevelPathEndpoint Start { get; }
    public ZLevelPathEndpoint End { get; }
    public float Cost { get; }
    public ImmutableArray<PathPoly> LocalPath { get; }
    public ZLevelTraversalNavigationEdge Traversal { get; }
    public ZLevelFlightNavigationEdge Flight { get; }

    private ZLevelPathLeg(
        ZLevelPathLegKind kind,
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        float cost,
        ImmutableArray<PathPoly> localPath,
        ZLevelTraversalNavigationEdge traversal,
        ZLevelFlightNavigationEdge flight)
    {
        Kind = kind;
        Start = start;
        End = end;
        Cost = cost;
        LocalPath = localPath;
        Traversal = traversal;
        Flight = flight;
    }

    public static bool TryCreateLocal(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ImmutableArray<PathPoly> path,
        float cost,
        out ZLevelPathLeg leg)
    {
        leg = default;
        if (!IsFiniteCost(cost) ||
            path.IsDefault ||
            start.MapId == MapId.Nullspace ||
            start.MapId != end.MapId ||
            start.WorldZ != end.WorldZ)
        {
            return false;
        }

        leg = new ZLevelPathLeg(
            ZLevelPathLegKind.Local,
            start,
            end,
            cost,
            path,
            default,
            default);
        return true;
    }

    public static bool TryCreateTraversal(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ZLevelTraversalNavigationEdge traversal,
        out ZLevelPathLeg leg)
    {
        leg = default;
        if (!IsFiniteCost(traversal.Cost) ||
            start.MapId == MapId.Nullspace ||
            start.MapId != end.MapId ||
            start.MapId != traversal.Source.MapId ||
            end.MapId != traversal.Destination.MapId ||
            start.Coordinates.EntityId != traversal.Source.GridUid ||
            end.Coordinates.EntityId != traversal.Destination.GridUid ||
            start.WorldZ != traversal.Source.WorldZ ||
            end.WorldZ != traversal.Destination.WorldZ ||
            end.WorldZ - start.WorldZ != traversal.ZOffset)
        {
            return false;
        }

        leg = new ZLevelPathLeg(
            ZLevelPathLegKind.Traversal,
            start,
            end,
            traversal.Cost,
            ImmutableArray<PathPoly>.Empty,
            traversal,
            default);
        return true;
    }

    public static bool TryCreateFlight(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ZLevelFlightNavigationEdge flight,
        out ZLevelPathLeg leg)
    {
        leg = default;
        if (!IsFiniteCost(flight.Cost) ||
            start.MapId == MapId.Nullspace ||
            start.MapId != end.MapId ||
            start.MapId != flight.Source.MapId ||
            end.MapId != flight.Destination.MapId ||
            start.Coordinates.EntityId != flight.Source.GridUid ||
            end.Coordinates.EntityId != flight.Destination.GridUid ||
            start.WorldZ != flight.Source.WorldZ ||
            end.WorldZ != flight.Destination.WorldZ ||
            end.WorldZ - start.WorldZ != flight.ZOffset)
        {
            return false;
        }

        leg = new ZLevelPathLeg(
            ZLevelPathLegKind.Flight,
            start,
            end,
            flight.Cost,
            ImmutableArray<PathPoly>.Empty,
            default,
            flight);
        return true;
    }

    private static bool IsFiniteCost(float cost)
    {
        return float.IsFinite(cost) && cost >= 0f;
    }
}

/// <summary>
/// An alternating sequence of native same-floor paths and authored vertical
/// traversals, stamped against the graph snapshot used to plan it.
/// </summary>
public sealed class ZLevelPathRoute
{
    public ZLevelPathEndpoint Start { get; }
    public ZLevelPathEndpoint End { get; }
    public ZLevelTraversalGraphVersion GraphVersion { get; }
    public ImmutableArray<ZLevelPathLeg> Legs { get; }
    public float TotalCost { get; }

    private ZLevelPathRoute(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ZLevelTraversalGraphVersion graphVersion,
        ImmutableArray<ZLevelPathLeg> legs,
        float totalCost)
    {
        Start = start;
        End = end;
        GraphVersion = graphVersion;
        Legs = legs;
        TotalCost = totalCost;
    }

    public static bool TryCreate(
        ZLevelPathEndpoint start,
        ZLevelPathEndpoint end,
        ZLevelTraversalGraphVersion graphVersion,
        ImmutableArray<ZLevelPathLeg> legs,
        out ZLevelPathRoute? route)
    {
        route = null;
        if (legs.IsDefault ||
            start.MapId == MapId.Nullspace ||
            start.MapId != end.MapId ||
            start.MapId != graphVersion.MapId)
        {
            return false;
        }

        var expected = start;
        var totalCost = 0f;
        foreach (var leg in legs)
        {
            if (leg.Start != expected ||
                leg.Start.MapId != graphVersion.MapId ||
                !float.IsFinite(leg.Cost) ||
                leg.Cost < 0f)
            {
                return false;
            }

            if ((leg.Kind == ZLevelPathLegKind.Traversal &&
                 (leg.Traversal.TopologyRevision != graphVersion.TopologyRevision ||
                  leg.Traversal.EnvironmentRevision != graphVersion.EnvironmentRevision)) ||
                (leg.Kind == ZLevelPathLegKind.Flight &&
                 (leg.Flight.TopologyRevision != graphVersion.TopologyRevision ||
                  leg.Flight.EnvironmentRevision != graphVersion.EnvironmentRevision)))
            {
                return false;
            }

            totalCost += leg.Cost;
            if (!float.IsFinite(totalCost))
                return false;
            expected = leg.End;
        }

        if (expected != end)
            return false;

        route = new ZLevelPathRoute(start, end, graphVersion, legs, totalCost);
        return true;
    }
}

public readonly record struct ZLevelPathRouteResult(
    ZLevelPathRouteStatus Status,
    ZLevelPathRoute? Route,
    ZLevelPathSearchDiagnostics Diagnostics)
{
    public bool Succeeded => Status == ZLevelPathRouteStatus.Success && Route != null;
}

public readonly record struct ZLevelPathRouteValidationResult(
    ZLevelPathRouteValidationStatus Status,
    int LegIndex)
{
    public bool IsValid => Status == ZLevelPathRouteValidationStatus.Valid;

    public static ZLevelPathRouteValidationResult Valid =>
        new(ZLevelPathRouteValidationStatus.Valid, -1);
}
