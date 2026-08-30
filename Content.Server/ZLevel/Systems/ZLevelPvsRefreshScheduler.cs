// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Distributes a target per-session refresh cadence across update frames while
/// retaining overdue credit and a fair circular cursor.
/// </summary>
internal sealed class ZLevelPvsRefreshScheduler
{
    private const double CreditEpsilon = 1e-6;

    private readonly double _targetIntervalSeconds;
    private double _refreshCredit;
    private int _cursor;

    public ZLevelPvsRefreshScheduler(float targetIntervalSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(targetIntervalSeconds, 0f);
        _targetIntervalSeconds = targetIntervalSeconds;
    }

    public ZLevelPvsRefreshPlan Plan(int sessionCount, float frameTime, int maximumRefreshes)
    {
        if (sessionCount <= 0)
        {
            Reset();
            return default;
        }

        _cursor %= sessionCount;
        maximumRefreshes = Math.Max(maximumRefreshes, 1);
        if (float.IsFinite(frameTime) && frameTime > 0f)
        {
            _refreshCredit = Math.Min(
                sessionCount,
                _refreshCredit + frameTime * sessionCount / _targetIntervalSeconds);
        }

        var due = Math.Min(
            sessionCount,
            (int) Math.Floor(_refreshCredit + CreditEpsilon));
        var scheduled = Math.Min(due, maximumRefreshes);
        var startIndex = _cursor;
        _cursor = (_cursor + scheduled) % sessionCount;
        _refreshCredit -= scheduled;

        return new ZLevelPvsRefreshPlan(
            startIndex,
            due,
            scheduled,
            due - scheduled,
            _refreshCredit);
    }

    public void Reset()
    {
        _refreshCredit = 0d;
        _cursor = 0;
    }
}

internal readonly record struct ZLevelPvsRefreshPlan(
    int StartIndex,
    int DueRefreshes,
    int ScheduledRefreshes,
    int DeferredRefreshes,
    double RemainingCredit);
