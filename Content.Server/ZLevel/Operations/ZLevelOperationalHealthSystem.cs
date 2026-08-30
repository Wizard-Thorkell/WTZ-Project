// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Runtime;
using Content.Server.Mapping;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server.ZLevel.Operations;

/// <summary>
/// Captures an operator-requested health report. It does no work during normal ticks.
/// </summary>
public sealed class ZLevelOperationalHealthSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly MappingSystem _mapping = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedZLevelBoundarySystem _boundaries = default!;
    [Dependency] private readonly SharedZLevelGravitySystem _gravity = default!;
    [Dependency] private readonly SharedZLevelMapSystem _maps = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedZLevelSkyExposureSystem _sky = default!;
    [Dependency] private readonly SharedZLevelSoundPortalSystem _soundPortals = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly ZLevelElevatorSystem _elevators = default!;
    [Dependency] private readonly ZLevelPvsSystem _pvs = default!;
    [Dependency] private readonly ZLevelSoundPlaybackSystem _soundPlayback = default!;
    [Dependency] private readonly ZLevelSoundRouteSystem _soundRoutes = default!;
    [Dependency] private readonly ZLevelTraversalGraphSystem _traversal = default!;

    public ZLevelOperationalHealthReport Capture()
    {
        var invalidMaps = new List<string>();
        var configuredMaps = 0;
        var mapQuery = EntityQueryEnumerator<ZLevelMapComponent>();
        while (mapQuery.MoveNext(out var mapUid, out _))
        {
            configuredMaps++;
            if (!_maps.TryValidate(mapUid, out var error))
                invalidMaps.Add($"{mapUid}: {error}");
        }

        invalidMaps.Sort(StringComparer.Ordinal);

        var metrics = _metrics.Snapshot();
        var scheduler = _pvs.SchedulerMetrics;
        var portals = _soundPortals.Snapshot();
        var routes = _soundRoutes.Snapshot();
        var playback = _soundPlayback.Snapshot();
        var traversal = _traversal.Snapshot();
        var paths = _pathfinding.SnapshotZLevelRouteMetrics();
        var steering = _steering.SnapshotZLevelMetrics();
        var autosave = _mapping.SnapshotAutosaveMetrics();
        var elevators = _elevators.Snapshot();

        var signals = new ZLevelOperationalSignals
        {
            ServerGc = GCSettings.IsServerGC,
            InGameSessions = _players.Sessions.Count(session => session.Status == SessionStatus.InGame),
            ConfiguredMaps = configuredMaps,
            InvalidMapDescriptions = invalidMaps.ToArray(),
            ActiveFlights = _zLevels.ActiveFlightCount,
            ActiveElevatorTravels = elevators.ActiveTravels,
            ActiveAutosaveSchedules = autosave.ActiveSchedules,
            AutosaveAttempts = autosave.Attempts,
            AutosaveSuccesses = autosave.Successes,
            AutosaveFailures = autosave.Failures,
            LastAutosaveAttemptUtc = autosave.LastAttemptUtc,
            LastAutosaveSuccessUtc = autosave.LastSuccessUtc,
            LastAutosaveSucceeded = autosave.LastAttemptSucceeded,
            LastAutosavePath = autosave.LastPath,
            LastAutosaveError = autosave.LastError,
            LastAutosaveValidatedEntities = autosave.LastValidatedEntities,
            LastAutosaveExcludedRoots = autosave.LastExcludedRoots,
            PvsRefreshes = metrics.PvsRefreshes,
            PvsBudgetExhaustions = metrics.PvsBudgetExhaustions,
            PvsFailOpenCandidates = metrics.PvsFailOpenCandidates,
            PvsDeferredRefreshes = scheduler.DeferredRefreshes,
            PvsSchedulerBudgetExhaustions = scheduler.BudgetExhaustions,
            TraceQueries = metrics.TraceQueries,
            TraceBudgetExhaustions = metrics.TraceBudgetExhaustions,
            TraceFrameResolutionFailures = metrics.TraceFrameResolutionFailures,
            SkyExposureQueries = metrics.SkyExposureQueries,
            SkyExposureBudgetExhaustions = metrics.SkyExposureBudgetExhaustions,
            SkyExposureBoundaryFailures = metrics.SkyExposureBoundaryFailures,
            ExplosionBudgetExhaustions =
                metrics.ExplosionAreaBudgetExhaustions + metrics.ExplosionIterationBudgetExhaustions,
            SoundRouteQueries = routes.Queries,
            SoundBudgetExhaustions =
                portals.ChunkBudgetExhaustions +
                portals.BuildBudgetExhaustions +
                portals.CandidateBudgetExhaustions +
                routes.CrossingLimitExhaustions +
                routes.PortalChunkBudgetExhaustions +
                routes.PortalBuildBudgetExhaustions +
                routes.PortalCandidateBudgetExhaustions +
                routes.EdgeBudgetExhaustions +
                routes.MediumSampleBudgetExhaustions +
                playback.RouteBudgetExhaustions +
                playback.PresentationBudgetExhaustions,
            PathQueries = paths.Queries,
            PathBudgetExhaustions =
                traversal.ConnectedBudgetExhaustions +
                paths.StateBudgetExhaustions +
                paths.LocalPathBudgetExhaustions +
                paths.TraversalEdgeBudgetExhaustions,
            PathExecutionFailures = steering.ExecutionFailures + steering.FlightLegsFailed,
            BoundaryCacheEntries = _boundaries.CachedBoundaryCount,
            BoundaryCacheCapacity = _boundaries.BoundaryCacheCapacity,
            BoundaryCacheOrderTokens = _boundaries.BoundaryCacheOrderTokenCount,
            SkyCacheEntries = _sky.CachedExposureCount,
            SkyCacheCapacity = _sky.CacheCapacity,
            SkyCacheOrderEntries = _sky.CacheOrderEntryCount,
            SoundCacheEntries = portals.CachedChunks,
            SoundCacheCapacity = portals.CacheCapacity,
            SoundCacheOrderTokens = portals.CacheOrderTokens,
            GravityCachedGrids = _gravity.CachedGridCount,
            GravityPendingRefreshGrids = _gravity.PendingRefreshGridCount,
            PvsContextCacheEntries = scheduler.VisibilityContextCacheEntries,
            PvsContextCacheMaxEntries = scheduler.VisibilityContextCacheMaxEntries,
        };

        return ZLevelOperationalHealthEvaluator.Evaluate(signals, DateTimeOffset.UtcNow);
    }
}

internal static class ZLevelOperationalHealthEvaluator
{
    public const string ContractVersion = "WTZ-OPS-HEALTH-1";

    public static ZLevelOperationalHealthReport Evaluate(
        ZLevelOperationalSignals signals,
        DateTimeOffset generatedAtUtc)
    {
        var findings = new List<ZLevelOperationalFinding>();

        if (!signals.ServerGc)
        {
            AddWarning(
                findings,
                "runtime.workstation-gc",
                "The server process is using workstation GC.",
                "Restart the production server with Server GC enabled before comparing capacity envelopes.");
        }

        if (signals.InvalidMapDescriptions.Length > 0)
        {
            AddCritical(
                findings,
                "map.invalid-state",
                $"{signals.InvalidMapDescriptions.Length} configured Z-level map(s) failed validation.",
                "Stop structural edits, preserve the current logs, and recover from the latest validated checkpoint.");
        }

        if (signals.LastAutosaveSucceeded == false)
        {
            AddCritical(
                findings,
                "autosave.last-attempt-failed",
                $"The latest autosave/checkpoint failed: {signals.LastAutosaveError ?? "unknown error"}",
                "Correct the reported map or filesystem problem and require a successful checkpoint before continuing risky changes.");
        }
        else if (signals.AutosaveFailures > 0)
        {
            AddWarning(
                findings,
                "autosave.recovered-failures",
                $"{signals.AutosaveFailures} autosave/checkpoint failure(s) occurred before the latest successful attempt.",
                "Review the earlier errors and reset counters only after the incident has been recorded.");
        }

        if (signals.PvsBudgetExhaustions > 0)
        {
            AddCritical(
                findings,
                "pvs.fail-open-budget",
                $"PVS exhausted its visibility budget {signals.PvsBudgetExhaustions} time(s), exposing {signals.PvsFailOpenCandidates} fail-open candidates.",
                "Capture a soak report, reduce candidate pressure, or raise the tested budget before restoring normal load.");
        }

        if (signals.PvsDeferredRefreshes > 0 || signals.PvsSchedulerBudgetExhaustions > 0)
        {
            AddWarning(
                findings,
                "pvs.scheduler-debt",
                $"PVS deferred {signals.PvsDeferredRefreshes} refresh(es) across {signals.PvsSchedulerBudgetExhaustions} exhausted scheduler update(s).",
                "Inspect session count and scheduler latency, then compare against the 32/64-session release envelopes.");
        }

        if (signals.TraceBudgetExhaustions > 0)
        {
            AddCritical(
                findings,
                "trace.budget-exhausted",
                $"Shared Z-level trace exhausted a hard budget {signals.TraceBudgetExhaustions} time(s).",
                "Identify the requesting channel and geometry before changing any trace limit.");
        }

        AddWarningWhenPositive(
            findings,
            signals.TraceFrameResolutionFailures,
            "trace.frame-resolution",
            "trace frame-resolution failure(s)",
            "Inspect moving-grid frame ownership and reject malformed callers.");
        AddWarningWhenPositive(
            findings,
            signals.SkyExposureBudgetExhaustions,
            "sky.budget-exhausted",
            "sky-exposure budget exhaustion(s)",
            "Inspect roof depth and sky-column invalidation before changing the boundary-check limit.");
        AddWarningWhenPositive(
            findings,
            signals.SkyExposureBoundaryFailures,
            "sky.boundary-failure",
            "sky-exposure boundary failure(s)",
            "Validate roof and boundary providers on the affected map.");
        AddWarningWhenPositive(
            findings,
            signals.ExplosionBudgetExhaustions,
            "explosion.budget-exhausted",
            "explosion topology budget exhaustion(s)",
            "Inspect the explosion fixture and configured area/iteration limits.");
        AddWarningWhenPositive(
            findings,
            signals.SoundBudgetExhaustions,
            "sound.budget-exhausted",
            "vertical sound budget exhaustion(s)",
            "Inspect portal density, route fan-out, and per-session presentation pressure.");
        AddWarningWhenPositive(
            findings,
            signals.PathBudgetExhaustions,
            "pathfinding.budget-exhausted",
            "hierarchical path budget exhaustion(s)",
            "Inspect authored traversal connectivity and route diagnostics before raising limits.");
        AddWarningWhenPositive(
            findings,
            signals.PathExecutionFailures,
            "pathfinding.execution-failure",
            "hierarchical route execution failure(s)",
            "Inspect stale topology, dynamic traversal state, elevator power, and flight capability.");

        AddCacheFindings(
            findings,
            "boundary",
            signals.BoundaryCacheEntries,
            signals.BoundaryCacheOrderTokens,
            signals.BoundaryCacheCapacity);
        AddCacheFindings(
            findings,
            "sky",
            signals.SkyCacheEntries,
            signals.SkyCacheOrderEntries,
            signals.SkyCacheCapacity);
        AddCacheFindings(
            findings,
            "sound",
            signals.SoundCacheEntries,
            signals.SoundCacheOrderTokens,
            signals.SoundCacheCapacity);

        var status = findings.Any(finding => finding.Severity == ZLevelOperationalFindingSeverity.Critical)
            ? ZLevelOperationalHealthStatus.Critical
            : findings.Count > 0
                ? ZLevelOperationalHealthStatus.Degraded
                : ZLevelOperationalHealthStatus.Healthy;

        return new ZLevelOperationalHealthReport(
            1,
            ContractVersion,
            generatedAtUtc,
            status,
            signals,
            findings.ToArray());
    }

    private static void AddWarningWhenPositive(
        List<ZLevelOperationalFinding> findings,
        long value,
        string code,
        string subject,
        string action)
    {
        if (value <= 0)
            return;

        AddWarning(findings, code, $"Observed {value} {subject}.", action);
    }

    private static void AddCacheFindings(
        List<ZLevelOperationalFinding> findings,
        string cache,
        int entries,
        int orderEntries,
        int capacity)
    {
        if (capacity <= 0 || entries > capacity)
        {
            AddCritical(
                findings,
                $"cache.{cache}-over-capacity",
                $"The {cache} cache contains {entries} entries for capacity {capacity}.",
                "Capture lifecycle diagnostics and restart only after preserving the ownership evidence.");
            return;
        }

        if ((long) orderEntries > (long) capacity * 2)
        {
            AddWarning(
                findings,
                $"cache.{cache}-order-pressure",
                $"The {cache} cache retains {orderEntries} order entries for capacity {capacity}.",
                "Exercise owner teardown and verify order compaction before the queue grows further.");
        }
    }

    private static void AddWarning(
        List<ZLevelOperationalFinding> findings,
        string code,
        string message,
        string action)
    {
        findings.Add(new ZLevelOperationalFinding(
            code,
            ZLevelOperationalFindingSeverity.Warning,
            message,
            action));
    }

    private static void AddCritical(
        List<ZLevelOperationalFinding> findings,
        string code,
        string message,
        string action)
    {
        findings.Add(new ZLevelOperationalFinding(
            code,
            ZLevelOperationalFindingSeverity.Critical,
            message,
            action));
    }
}

public enum ZLevelOperationalHealthStatus : byte
{
    Healthy,
    Degraded,
    Critical,
}

public enum ZLevelOperationalFindingSeverity : byte
{
    Warning,
    Critical,
}

public sealed record ZLevelOperationalHealthReport(
    int SchemaVersion,
    string ContractVersion,
    DateTimeOffset GeneratedAtUtc,
    ZLevelOperationalHealthStatus Status,
    ZLevelOperationalSignals Signals,
    ZLevelOperationalFinding[] Findings);

public sealed record ZLevelOperationalFinding(
    string Code,
    ZLevelOperationalFindingSeverity Severity,
    string Message,
    string Action);

public sealed record ZLevelOperationalSignals
{
    public bool ServerGc { get; init; }
    public int InGameSessions { get; init; }
    public int ConfiguredMaps { get; init; }
    public string[] InvalidMapDescriptions { get; init; } = [];
    public int ActiveFlights { get; init; }
    public int ActiveElevatorTravels { get; init; }
    public int ActiveAutosaveSchedules { get; init; }
    public long AutosaveAttempts { get; init; }
    public long AutosaveSuccesses { get; init; }
    public long AutosaveFailures { get; init; }
    public DateTimeOffset? LastAutosaveAttemptUtc { get; init; }
    public DateTimeOffset? LastAutosaveSuccessUtc { get; init; }
    public bool? LastAutosaveSucceeded { get; init; }
    public string? LastAutosavePath { get; init; }
    public string? LastAutosaveError { get; init; }
    public int LastAutosaveValidatedEntities { get; init; }
    public int LastAutosaveExcludedRoots { get; init; }
    public long PvsRefreshes { get; init; }
    public long PvsBudgetExhaustions { get; init; }
    public long PvsFailOpenCandidates { get; init; }
    public long PvsDeferredRefreshes { get; init; }
    public long PvsSchedulerBudgetExhaustions { get; init; }
    public long TraceQueries { get; init; }
    public long TraceBudgetExhaustions { get; init; }
    public long TraceFrameResolutionFailures { get; init; }
    public long SkyExposureQueries { get; init; }
    public long SkyExposureBudgetExhaustions { get; init; }
    public long SkyExposureBoundaryFailures { get; init; }
    public long ExplosionBudgetExhaustions { get; init; }
    public long SoundRouteQueries { get; init; }
    public long SoundBudgetExhaustions { get; init; }
    public long PathQueries { get; init; }
    public long PathBudgetExhaustions { get; init; }
    public long PathExecutionFailures { get; init; }
    public int BoundaryCacheEntries { get; init; }
    public int BoundaryCacheCapacity { get; init; }
    public int BoundaryCacheOrderTokens { get; init; }
    public int SkyCacheEntries { get; init; }
    public int SkyCacheCapacity { get; init; }
    public int SkyCacheOrderEntries { get; init; }
    public int SoundCacheEntries { get; init; }
    public int SoundCacheCapacity { get; init; }
    public int SoundCacheOrderTokens { get; init; }
    public int GravityCachedGrids { get; init; }
    public int GravityPendingRefreshGrids { get; init; }
    public int PvsContextCacheEntries { get; init; }
    public int PvsContextCacheMaxEntries { get; init; }
}
