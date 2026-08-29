// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;

namespace Content.Shared.ZLevel;

/// <summary>
/// Caller-owned output and scratch storage for allocation-sensitive traces.
/// Contents are replaced by every buffered trace invocation.
/// </summary>
public sealed class ZLevelTraceBuffer
{
    private readonly List<ZLevelTraceSegment> _segments = new();
    private readonly List<ZLevelTraceTileVisit> _tileVisits = new();
    private readonly List<ZLevelTraceEntityHit> _entityHits = new();
    private readonly List<ZLevelTraceBoundaryCrossing> _boundaryCrossings = new();

    internal List<ZLevelTraceSegment> MutableSegments => _segments;
    internal List<ZLevelTraceTileVisit> MutableTileVisits => _tileVisits;
    internal List<ZLevelTraceEntityHit> MutableEntityHits => _entityHits;
    internal List<ZLevelTraceBoundaryCrossing> MutableBoundaryCrossings => _boundaryCrossings;
    internal readonly List<RayCastResults> PhysicsHits = new();
    internal readonly HashSet<EntityUid> PointCandidates = new();
    internal readonly List<EntityUid> PointHits = new();

    public IReadOnlyList<ZLevelTraceSegment> Segments => _segments;
    public IReadOnlyList<ZLevelTraceTileVisit> TileVisits => _tileVisits;
    public IReadOnlyList<ZLevelTraceEntityHit> EntityHits => _entityHits;
    public IReadOnlyList<ZLevelTraceBoundaryCrossing> BoundaryCrossings => _boundaryCrossings;

    public int SegmentCapacity => _segments.Capacity;
    public int TileVisitCapacity => _tileVisits.Capacity;
    public int EntityHitCapacity => _entityHits.Capacity;
    public int BoundaryCrossingCapacity => _boundaryCrossings.Capacity;

    public void EnsureCapacity(
        int segments,
        int tileVisits,
        int entityHits,
        int boundaryCrossings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(segments);
        ArgumentOutOfRangeException.ThrowIfNegative(tileVisits);
        ArgumentOutOfRangeException.ThrowIfNegative(entityHits);
        ArgumentOutOfRangeException.ThrowIfNegative(boundaryCrossings);
        _segments.EnsureCapacity(segments);
        _tileVisits.EnsureCapacity(tileVisits);
        _entityHits.EnsureCapacity(entityHits);
        _boundaryCrossings.EnsureCapacity(boundaryCrossings);
    }

    public void Clear()
    {
        _segments.Clear();
        _tileVisits.Clear();
        _entityHits.Clear();
        _boundaryCrossings.Clear();
        PhysicsHits.Clear();
        PointCandidates.Clear();
        PointHits.Clear();
    }

    internal ZLevelTraceBufferBookmark Bookmark()
    {
        return new ZLevelTraceBufferBookmark(
            _segments.Count,
            _tileVisits.Count,
            _entityHits.Count,
            _boundaryCrossings.Count);
    }

    internal void Rollback(ZLevelTraceBufferBookmark bookmark)
    {
        RemoveAfter(_segments, bookmark.SegmentCount);
        RemoveAfter(_tileVisits, bookmark.TileVisitCount);
        RemoveAfter(_entityHits, bookmark.EntityHitCount);
        RemoveAfter(_boundaryCrossings, bookmark.BoundaryCrossingCount);
    }

    private static void RemoveAfter<T>(List<T> list, int count)
    {
        if (list.Count > count)
            list.RemoveRange(count, list.Count - count);
    }
}

internal readonly record struct ZLevelTraceBufferBookmark(
    int SegmentCount,
    int TileVisitCount,
    int EntityHitCount,
    int BoundaryCrossingCount);
