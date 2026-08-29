// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Diagnostics;
using System.Numerics;
using Robust.Shared.GameObjects;

namespace Content.Shared.ZLevel;

public enum ZLevelSoundMediumMode : byte
{
    Ignore,
    RequirePressure,
}

public enum ZLevelSoundRouteStatus : byte
{
    Success,
    Invalid,
    DifferentGrid,
    CrossingLimitExceeded,
    PortalChunkBudgetExceeded,
    PortalBuildBudgetExceeded,
    PortalCandidateBudgetExceeded,
    EdgeBudgetExceeded,
    MediumSampleBudgetExceeded,
    NoPortalRoute,
    MediumBlocked,
    OutOfRange,
}

/// <summary>
/// One source or listener in a grid-local Z-level frame.
/// </summary>
public readonly record struct ZLevelSoundRouteEndpoint(
    EntityUid GridUid,
    Vector2 LocalPosition,
    int LocalZ);

/// <summary>
/// Acoustic policy for one route lookup. MaxDistance is an effective path
/// distance: geometric travel and transmission loss both consume it.
/// </summary>
public readonly record struct ZLevelSoundRouteOptions(
    float MaxDistance,
    int MaxCrossings,
    float VerticalDistance,
    float DefaultPortalTransmission,
    float ExplicitPortalTransmission,
    float MinimumTransmission,
    float TransmissionLossDistanceScale,
    ZLevelSoundMediumMode MediumMode,
    float MinimumPressure,
    float ReferencePressure,
    float PressureExponent)
{
    public const float DefaultVerticalDistance = 3f;
    public const float DefaultOpeningTransmission = 1f;
    public const float DefaultExplicitOpeningTransmission = 0.75f;
    public const float DefaultMinimumTransmission = 0.01f;
    public const float DefaultTransmissionLossDistanceScale = 4f;
    public const float DefaultMinimumPressure = 1f;
    public const float DefaultReferencePressure = 101.325f;
    public const float DefaultPressureExponent = 0.5f;

    public static ZLevelSoundRouteOptions Default(
        float maxDistance,
        int maxCrossings,
        ZLevelSoundMediumMode mediumMode = ZLevelSoundMediumMode.RequirePressure)
    {
        return new ZLevelSoundRouteOptions(
            maxDistance,
            maxCrossings,
            DefaultVerticalDistance,
            DefaultOpeningTransmission,
            DefaultExplicitOpeningTransmission,
            DefaultMinimumTransmission,
            DefaultTransmissionLossDistanceScale,
            mediumMode,
            DefaultMinimumPressure,
            DefaultReferencePressure,
            DefaultPressureExponent);
    }
}

/// <summary>
/// Caller-owned work allowance for one route lookup.
/// </summary>
public struct ZLevelSoundRouteBudget
{
    public ZLevelSoundPortalQueryBudget PortalBudget;
    public int RemainingEdges;
    public int RemainingMediumSamples;

    public static ZLevelSoundRouteBudget Unlimited =>
        new(ZLevelSoundPortalQueryBudget.Unlimited, int.MaxValue, int.MaxValue);

    public ZLevelSoundRouteBudget(
        ZLevelSoundPortalQueryBudget portalBudget,
        int remainingEdges,
        int remainingMediumSamples)
    {
        PortalBudget = portalBudget;
        RemainingEdges = remainingEdges;
        RemainingMediumSamples = remainingMediumSamples;
    }
}

public readonly record struct ZLevelSoundRouteResult(
    ZLevelSoundRouteStatus Status,
    ZLevelSoundPortalQueryStatus PortalStatus,
    int PortalsAdded,
    int Crossings,
    int PortalCandidates,
    int EdgesEvaluated,
    int MediumSamples,
    float Distance,
    float EffectiveDistance,
    float Transmission)
{
    public bool Succeeded => Status == ZLevelSoundRouteStatus.Success;

    public float TransmissionLossDecibels => Transmission <= 0f
        ? float.PositiveInfinity
        : -20f * MathF.Log10(Transmission);
}

public readonly record struct ZLevelSoundRouteMetrics(
    long Queries,
    long Successes,
    long SameLevelSuccesses,
    long VerticalSuccesses,
    long InvalidQueries,
    long NoPortalRoutes,
    long MediumBlockedRoutes,
    long OutOfRangeRoutes,
    long CrossingLimitExhaustions,
    long PortalChunkBudgetExhaustions,
    long PortalBuildBudgetExhaustions,
    long PortalCandidateBudgetExhaustions,
    long EdgeBudgetExhaustions,
    long MediumSampleBudgetExhaustions,
    long PortalCandidates,
    long PortalsReturned,
    long Crossings,
    long EdgesEvaluated,
    long MediumSamples,
    long RouteTimestampTicks,
    long LastRouteTimestampTicks,
    long MaxRouteTimestampTicks)
{
    public double RouteMilliseconds => ToMilliseconds(RouteTimestampTicks);
    public double AverageRouteMilliseconds => Queries == 0
        ? 0d
        : RouteMilliseconds / Queries;
    public double LastRouteMilliseconds => ToMilliseconds(LastRouteTimestampTicks);
    public double MaxRouteMilliseconds => ToMilliseconds(MaxRouteTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
