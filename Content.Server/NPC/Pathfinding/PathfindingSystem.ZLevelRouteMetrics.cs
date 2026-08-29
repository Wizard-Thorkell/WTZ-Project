// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using System.Threading;

namespace Content.Server.NPC.Pathfinding;

public sealed partial class PathfindingSystem
{
    private long _zLevelRouteQueries;
    private long _zLevelRouteSuccesses;
    private long _zLevelRouteNoPaths;
    private long _zLevelRouteInvalidRequests;
    private long _zLevelRouteCancellations;
    private long _zLevelRouteStateBudgetExhaustions;
    private long _zLevelRouteLocalPathBudgetExhaustions;
    private long _zLevelRouteTraversalEdgeBudgetExhaustions;
    private long _zLevelRouteTopologyChanges;
    private long _zLevelRouteEnvironmentChanges;
    private long _zLevelRouteCombinedChanges;
    private long _zLevelRouteLocalNavigationChanges;
    private long _zLevelRouteStatesExpanded;
    private long _zLevelRouteLocalPathsRequested;
    private long _zLevelRouteTraversalEdgesEvaluated;
    private long _zLevelRouteLegs;
    private long _zLevelRouteTimestampTicks;
    private long _zLevelRouteLastTimestampTicks;
    private long _zLevelRouteMaxTimestampTicks;

    public ZLevelPathRouteMetricsSnapshot SnapshotZLevelRouteMetrics()
    {
        return new ZLevelPathRouteMetricsSnapshot(
            Interlocked.Read(ref _zLevelRouteQueries),
            Interlocked.Read(ref _zLevelRouteSuccesses),
            Interlocked.Read(ref _zLevelRouteNoPaths),
            Interlocked.Read(ref _zLevelRouteInvalidRequests),
            Interlocked.Read(ref _zLevelRouteCancellations),
            Interlocked.Read(ref _zLevelRouteStateBudgetExhaustions),
            Interlocked.Read(ref _zLevelRouteLocalPathBudgetExhaustions),
            Interlocked.Read(ref _zLevelRouteTraversalEdgeBudgetExhaustions),
            Interlocked.Read(ref _zLevelRouteTopologyChanges),
            Interlocked.Read(ref _zLevelRouteEnvironmentChanges),
            Interlocked.Read(ref _zLevelRouteCombinedChanges),
            Interlocked.Read(ref _zLevelRouteLocalNavigationChanges),
            Interlocked.Read(ref _zLevelRouteStatesExpanded),
            Interlocked.Read(ref _zLevelRouteLocalPathsRequested),
            Interlocked.Read(ref _zLevelRouteTraversalEdgesEvaluated),
            Interlocked.Read(ref _zLevelRouteLegs),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelRouteTimestampTicks)),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelRouteLastTimestampTicks)),
            TimestampToMilliseconds(Interlocked.Read(ref _zLevelRouteMaxTimestampTicks)));
    }

    public void ResetZLevelRouteMetrics()
    {
        Interlocked.Exchange(ref _zLevelRouteQueries, 0);
        Interlocked.Exchange(ref _zLevelRouteSuccesses, 0);
        Interlocked.Exchange(ref _zLevelRouteNoPaths, 0);
        Interlocked.Exchange(ref _zLevelRouteInvalidRequests, 0);
        Interlocked.Exchange(ref _zLevelRouteCancellations, 0);
        Interlocked.Exchange(ref _zLevelRouteStateBudgetExhaustions, 0);
        Interlocked.Exchange(ref _zLevelRouteLocalPathBudgetExhaustions, 0);
        Interlocked.Exchange(ref _zLevelRouteTraversalEdgeBudgetExhaustions, 0);
        Interlocked.Exchange(ref _zLevelRouteTopologyChanges, 0);
        Interlocked.Exchange(ref _zLevelRouteEnvironmentChanges, 0);
        Interlocked.Exchange(ref _zLevelRouteCombinedChanges, 0);
        Interlocked.Exchange(ref _zLevelRouteLocalNavigationChanges, 0);
        Interlocked.Exchange(ref _zLevelRouteStatesExpanded, 0);
        Interlocked.Exchange(ref _zLevelRouteLocalPathsRequested, 0);
        Interlocked.Exchange(ref _zLevelRouteTraversalEdgesEvaluated, 0);
        Interlocked.Exchange(ref _zLevelRouteLegs, 0);
        Interlocked.Exchange(ref _zLevelRouteTimestampTicks, 0);
        Interlocked.Exchange(ref _zLevelRouteLastTimestampTicks, 0);
        Interlocked.Exchange(ref _zLevelRouteMaxTimestampTicks, 0);
    }

    private ZLevelPathRouteResult FinishZLevelPathRoute(
        ZLevelPathRouteStatus status,
        ZLevelPathRoute? route,
        ZLevelPathSearchDiagnostics diagnostics,
        long started)
    {
        Interlocked.Increment(ref _zLevelRouteQueries);
        switch (status)
        {
            case ZLevelPathRouteStatus.Success:
                Interlocked.Increment(ref _zLevelRouteSuccesses);
                break;
            case ZLevelPathRouteStatus.NoPath:
                Interlocked.Increment(ref _zLevelRouteNoPaths);
                break;
            case ZLevelPathRouteStatus.InvalidRequest:
                Interlocked.Increment(ref _zLevelRouteInvalidRequests);
                break;
            case ZLevelPathRouteStatus.Cancelled:
                Interlocked.Increment(ref _zLevelRouteCancellations);
                break;
            case ZLevelPathRouteStatus.StateExpansionBudgetExceeded:
                Interlocked.Increment(ref _zLevelRouteStateBudgetExhaustions);
                break;
            case ZLevelPathRouteStatus.LocalPathBudgetExceeded:
                Interlocked.Increment(ref _zLevelRouteLocalPathBudgetExhaustions);
                break;
            case ZLevelPathRouteStatus.TraversalEdgeBudgetExceeded:
                Interlocked.Increment(ref _zLevelRouteTraversalEdgeBudgetExhaustions);
                break;
            case ZLevelPathRouteStatus.TopologyChanged:
                Interlocked.Increment(ref _zLevelRouteTopologyChanges);
                break;
            case ZLevelPathRouteStatus.EnvironmentChanged:
                Interlocked.Increment(ref _zLevelRouteEnvironmentChanges);
                break;
            case ZLevelPathRouteStatus.TopologyAndEnvironmentChanged:
                Interlocked.Increment(ref _zLevelRouteCombinedChanges);
                break;
            case ZLevelPathRouteStatus.LocalNavigationChanged:
                Interlocked.Increment(ref _zLevelRouteLocalNavigationChanges);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, null);
        }

        Interlocked.Add(ref _zLevelRouteStatesExpanded, diagnostics.StatesExpanded);
        Interlocked.Add(ref _zLevelRouteLocalPathsRequested, diagnostics.LocalPathsRequested);
        Interlocked.Add(ref _zLevelRouteTraversalEdgesEvaluated, diagnostics.TraversalEdgesEvaluated);
        Interlocked.Add(ref _zLevelRouteLegs, route?.Legs.Length ?? 0);
        var elapsed = Stopwatch.GetTimestamp() - started;
        Interlocked.Add(ref _zLevelRouteTimestampTicks, elapsed);
        Interlocked.Exchange(ref _zLevelRouteLastTimestampTicks, elapsed);
        UpdateMaximum(ref _zLevelRouteMaxTimestampTicks, elapsed);
        return new ZLevelPathRouteResult(status, route, diagnostics);
    }
}

public readonly record struct ZLevelPathRouteMetricsSnapshot(
    long Queries,
    long Successes,
    long NoPaths,
    long InvalidRequests,
    long Cancellations,
    long StateBudgetExhaustions,
    long LocalPathBudgetExhaustions,
    long TraversalEdgeBudgetExhaustions,
    long TopologyChanges,
    long EnvironmentChanges,
    long CombinedChanges,
    long LocalNavigationChanges,
    long StatesExpanded,
    long LocalPathsRequested,
    long TraversalEdgesEvaluated,
    long Legs,
    double TotalMilliseconds,
    double LastMilliseconds,
    double MaxMilliseconds)
{
    public double AverageMilliseconds => Queries == 0 ? 0d : TotalMilliseconds / Queries;
}
