// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Immutable;
using System.Numerics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel;

/// <summary>
/// Selects optional trace output without defining consumer-specific hit policy.
/// </summary>
[Flags]
public enum ZLevelTraceOptions : byte
{
    None = 0,
    IncludeTileVisits = 1 << 0,
    IncludeEntityHits = 1 << 1,
    Default = IncludeTileVisits | IncludeEntityHits,
}

public enum ZLevelTraceTermination : byte
{
    Completed,
    InvalidCoordinates,
    DifferentMaps,
    FrameResolutionRequired,
    ClosedBoundary,
    IterationBudgetExceeded,
}

/// <summary>
/// A trace endpoint captured in both world space and an optional grid frame.
/// </summary>
public readonly record struct ZLevelTracePoint
{
    public ZLevelMapCoordinates WorldCoordinates { get; }
    public EntityUid? GridUid { get; }
    public Vector2 LocalPosition { get; }
    public int LocalZ { get; }

    internal ZLevelTracePoint(
        ZLevelMapCoordinates worldCoordinates,
        EntityUid? gridUid,
        Vector2 localPosition,
        int localZ)
    {
        WorldCoordinates = worldCoordinates;
        GridUid = gridUid;
        LocalPosition = localPosition;
        LocalZ = localZ;
    }

    public static ZLevelTracePoint FromMap(ZLevelMapCoordinates coordinates)
    {
        return new ZLevelTracePoint(coordinates, null, coordinates.Position, coordinates.Z);
    }
}

/// <summary>
/// Shared geometric input. Specialized systems retain ownership of damage,
/// attenuation, penetration, and target-selection rules.
/// </summary>
public readonly record struct ZLevelTraceRequest(
    ZLevelTracePoint Origin,
    ZLevelTracePoint Destination,
    ZLevelBoundaryChannels BoundaryChannels,
    int CollisionMask = 0,
    EntityUid? IgnoredEntity = null,
    ZLevelTraceOptions Options = ZLevelTraceOptions.Default,
    EntityUid? BoundaryFrameUid = null);

public readonly record struct ZLevelTraceSegment(
    int Sequence,
    ZLevelTracePoint Start,
    ZLevelTracePoint End,
    EntityUid? FrameUid,
    float StartDistance,
    float EndDistance);

public readonly record struct ZLevelTraceTileVisit(
    int Sequence,
    EntityUid GridUid,
    ZLevelTileIndices Tile,
    int WorldZ,
    int SegmentSequence,
    float EntryDistance);

public readonly record struct ZLevelTraceEntityHit(
    int Sequence,
    EntityUid Entity,
    ZLevelMapCoordinates Position,
    int SegmentSequence,
    float Distance);

public readonly record struct ZLevelTraceBoundaryCrossing(
    int Sequence,
    EntityUid GridUid,
    Vector2i Tile,
    int FromLocalZ,
    int ToLocalZ,
    int FromWorldZ,
    int ToWorldZ,
    int SegmentSequence,
    float Distance,
    ZLevelBoundaryState State,
    bool IsOpen);

/// <summary>
/// Immutable trace snapshot. P1.3 will add a caller-owned buffer overload for
/// hot consumers after the complete vertical contract is stable.
/// </summary>
public readonly record struct ZLevelTraceResult(
    ZLevelTraceTermination Termination,
    ZLevelTracePoint FinalPoint,
    ImmutableArray<ZLevelTraceSegment> Segments,
    ImmutableArray<ZLevelTraceTileVisit> TileVisits,
    ImmutableArray<ZLevelTraceEntityHit> EntityHits,
    ImmutableArray<ZLevelTraceBoundaryCrossing> BoundaryCrossings)
{
    public bool ReachedDestination => Termination == ZLevelTraceTermination.Completed;
}
