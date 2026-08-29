// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Diagnostics;
using System.Numerics;
using Content.Shared.Maps;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared.ZLevel;

/// <summary>
/// Identifies whether sound crosses through the map's default boundary policy
/// or through an explicit content-authored override.
/// </summary>
public enum ZLevelSoundPortalKind : byte
{
    DefaultOpening,
    ExplicitOpening,
}

public enum ZLevelSoundPortalQueryStatus : byte
{
    Success,
    Invalid,
    ChunkBudgetExceeded,
    BuildBudgetExceeded,
    CandidateBudgetExceeded,
}

/// <summary>
/// Caller-owned limits for one bounded sound-portal query.
/// </summary>
public struct ZLevelSoundPortalQueryBudget
{
    public int RemainingChunks;
    public int RemainingBuilds;
    public int RemainingCandidates;

    public static ZLevelSoundPortalQueryBudget Unlimited =>
        new(int.MaxValue, int.MaxValue, int.MaxValue);

    public ZLevelSoundPortalQueryBudget(
        int remainingChunks,
        int remainingBuilds,
        int remainingCandidates)
    {
        RemainingChunks = remainingChunks;
        RemainingBuilds = remainingBuilds;
        RemainingCandidates = remainingCandidates;
    }
}

public readonly record struct ZLevelSoundPortalQueryResult(
    ZLevelSoundPortalQueryStatus Status,
    int PortalsAdded,
    int ChunksVisited,
    int CandidatesVisited)
{
    public bool Succeeded => Status == ZLevelSoundPortalQueryStatus.Success;
}

/// <summary>
/// One resolved vertical sound opening. Tile and local position remain stable
/// when a grid moves; world position and world Z are resolved at query time.
/// </summary>
public readonly record struct ZLevelSoundPortal(
    EntityUid GridUid,
    Vector2i Tile,
    int LowerLocalZ,
    int UpperLocalZ,
    Vector2 LocalPosition,
    Vector2 WorldPosition,
    int LowerWorldZ,
    int UpperWorldZ,
    ZLevelSoundPortalKind Kind);

public readonly record struct ZLevelSoundPortalChunkKey(
    EntityUid GridUid,
    Vector2i ChunkIndices,
    int LowerLocalZ);

/// <summary>
/// Compact 16x16 sound-opening mask for one adjacent pair of local floors.
/// Explicit words are a subset of open words.
/// </summary>
public readonly record struct ZLevelSoundPortalChunk(
    ZLevelSoundPortalChunkKey Key,
    long Revision,
    ulong OpenWord0,
    ulong OpenWord1,
    ulong OpenWord2,
    ulong OpenWord3,
    ulong ExplicitWord0,
    ulong ExplicitWord1,
    ulong ExplicitWord2,
    ulong ExplicitWord3,
    int OpenCount,
    int ExplicitOpenCount)
{
    public const int ChunkSize = TileSystem.ChunkSize;
    public const int TileCount = ChunkSize * ChunkSize;
    public const int WordCount = TileCount / 64;

    public Vector2i Origin => Key.ChunkIndices * ChunkSize;

    public bool IsOpen(Vector2i gridTile)
    {
        if (SharedMapSystem.GetChunkIndices(gridTile, ChunkSize) != Key.ChunkIndices)
            return false;

        var relative = SharedMapSystem.GetChunkRelative(gridTile, ChunkSize);
        return IsOpenRelative(relative.X, relative.Y);
    }

    public bool IsExplicitlyOpen(Vector2i gridTile)
    {
        if (SharedMapSystem.GetChunkIndices(gridTile, ChunkSize) != Key.ChunkIndices)
            return false;

        var relative = SharedMapSystem.GetChunkRelative(gridTile, ChunkSize);
        return IsExplicitlyOpenRelative(relative.X, relative.Y);
    }

    public bool IsOpenRelative(int x, int y)
    {
        return TryGetBit(x, y, false);
    }

    public bool IsExplicitlyOpenRelative(int x, int y)
    {
        return TryGetBit(x, y, true);
    }

    public ulong GetOpenWord(int index)
    {
        return index switch
        {
            0 => OpenWord0,
            1 => OpenWord1,
            2 => OpenWord2,
            3 => OpenWord3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    public ulong GetExplicitWord(int index)
    {
        return index switch
        {
            0 => ExplicitWord0,
            1 => ExplicitWord1,
            2 => ExplicitWord2,
            3 => ExplicitWord3,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }

    private bool TryGetBit(int x, int y, bool explicitOpening)
    {
        if ((uint) x >= ChunkSize || (uint) y >= ChunkSize)
            return false;

        var bit = x + y * ChunkSize;
        var word = explicitOpening ? GetExplicitWord(bit >> 6) : GetOpenWord(bit >> 6);
        return (word & (1UL << (bit & 63))) != 0;
    }
}

public readonly record struct ZLevelSoundPortalCacheMetrics(
    long ChunkQueries,
    long CacheHits,
    long CacheMisses,
    long Builds,
    long BuildTileChecks,
    long BuildOpenPortals,
    long BuildExplicitPortals,
    long Invalidations,
    long InvalidatedChunks,
    long BuildTimestampTicks,
    long LastBuildTimestampTicks,
    long MaxBuildTimestampTicks,
    long Evictions,
    long PortalQueries,
    long QueryChunksVisited,
    long QueryCandidatesVisited,
    long QueryPortalsAdded,
    long ChunkBudgetExhaustions,
    long BuildBudgetExhaustions,
    long CandidateBudgetExhaustions,
    int CachedChunks,
    int CachedOpenPortals,
    int CachedExplicitPortals,
    int CacheOrderTokens,
    int CacheCapacity)
{
    public double CacheHitPercent => ChunkQueries == 0
        ? 0d
        : CacheHits * 100d / ChunkQueries;

    public double BuildMilliseconds => ToMilliseconds(BuildTimestampTicks);
    public double AverageBuildMilliseconds => Builds == 0
        ? 0d
        : BuildMilliseconds / Builds;
    public double LastBuildMilliseconds => ToMilliseconds(LastBuildTimestampTicks);
    public double MaxBuildMilliseconds => ToMilliseconds(MaxBuildTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
