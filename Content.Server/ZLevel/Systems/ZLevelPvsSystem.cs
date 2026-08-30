// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Server.GameObjects;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Builds per-session vertical visibility exclusions before the engine serializes spatial PVS chunks.
/// </summary>
public sealed class ZLevelPvsSystem : EntitySystem
{
    public const float TargetRefreshInterval = 0.1f;
    public const int DefaultVisibilityCheckBudget = 16384;
    public const int MaximumVisibilityCheckBudget = 1_000_000;
    public const int DefaultMaxSessionRefreshesPerUpdate = 16;
    public const int MaximumSessionRefreshesPerUpdate = 256;
    private const int MaximumParentDepth = 64;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ZLevelSoundPlaybackSystem _soundPlayback = default!;

    private readonly List<ZLevelPvsViewerContext> _viewers = new();
    private readonly HashSet<EntityUid> _viewerCandidates = new();
    private readonly HashSet<EntityUid> _candidates = new();
    private readonly HashSet<EntityUid> _visible = new();
    private readonly HashSet<EntityUid> _culled = new();
    private readonly HashSet<EntityUid> _soundCulled = new();
    private readonly List<ICommonSession> _scheduledSessions = new();
    private readonly Dictionary<EntityUid, ZLevelEntityVisibilityContext> _visibilityContexts = new();
    private readonly ZLevelPvsRefreshScheduler _refreshScheduler = new(TargetRefreshInterval);

    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<OccluderComponent> _occluderQuery;
    private EntityQuery<PointLightComponent> _pointLightQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private float _priorityViewSize;
    private bool _pvsEnabled;
    private int _visibilityCheckBudget = DefaultVisibilityCheckBudget;
    private int _maxSessionRefreshesPerUpdate = DefaultMaxSessionRefreshesPerUpdate;

    private long _schedulerUpdates;
    private long _schedulerActiveSessionSamples;
    private long _schedulerDueRefreshes;
    private long _schedulerRefreshes;
    private long _schedulerDeferredRefreshes;
    private long _schedulerBudgetExhaustions;
    private int _schedulerMaxActiveSessions;
    private int _schedulerMaxRefreshes;
    private int _schedulerMaxDeferredRefreshes;
    private long _schedulerTimestampTicks;
    private long _schedulerLastTimestampTicks;
    private long _schedulerMaxTimestampTicks;
    private long _visibilityContextCacheHits;
    private long _visibilityContextCacheMisses;
    private int _visibilityContextCacheEntries;
    private int _visibilityContextCacheMaxEntries;

    public int VisibilityCheckBudget => _visibilityCheckBudget;
    public int MaxSessionRefreshesPerUpdate => _maxSessionRefreshesPerUpdate;
    public ZLevelPvsSchedulerMetricsSnapshot SchedulerMetrics => new(
        _schedulerUpdates,
        _schedulerActiveSessionSamples,
        _schedulerDueRefreshes,
        _schedulerRefreshes,
        _schedulerDeferredRefreshes,
        _schedulerBudgetExhaustions,
        _schedulerMaxActiveSessions,
        _schedulerMaxRefreshes,
        _schedulerMaxDeferredRefreshes,
        TimestampTicksToMilliseconds(_schedulerTimestampTicks),
        TimestampTicksToMilliseconds(_schedulerLastTimestampTicks),
        TimestampTicksToMilliseconds(_schedulerMaxTimestampTicks),
        _visibilityContextCacheHits,
        _visibilityContextCacheMisses,
        _visibilityContextCacheEntries,
        _visibilityContextCacheMaxEntries);

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _occluderQuery = GetEntityQuery<OccluderComponent>();
        _pointLightQuery = GetEntityQuery<PointLightComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeLocalEvent<ActorComponent, ZLevelPositionChangedEvent>(OnViewerZLevelChanged);

        Subs.CVar(_configuration, CVars.NetPVS, OnPvsEnabled, true);
        Subs.CVar(_configuration, CVars.NetMaxUpdateRange, _ => RefreshViewSize(), true);
        Subs.CVar(_configuration, CVars.NetPvsPriorityRange, _ => RefreshViewSize(), true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelPvsVisibilityCheckBudget,
            value => _visibilityCheckBudget = Math.Clamp(value, 0, MaximumVisibilityCheckBudget),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelPvsMaxSessionRefreshesPerUpdate,
            value => _maxSessionRefreshesPerUpdate = Math.Clamp(
                value,
                1,
                MaximumSessionRefreshesPerUpdate),
            true);
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        foreach (var session in _players.Sessions)
        {
            _pvs.ClearSessionCulling(session);
        }

        _refreshScheduler.Reset();
        _scheduledSessions.Clear();
        _visibilityContexts.Clear();

        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        RefreshScheduledSessions(frameTime);
    }

    internal ZLevelPvsRefreshPlan RefreshScheduledSessions(
        float frameTime,
        Action<long>? refreshLatencyObserver = null)
    {
        var started = Stopwatch.GetTimestamp();
        _visibilityContexts.Clear();
        _scheduledSessions.Clear();
        foreach (var session in _players.Sessions)
        {
            if (session.Status == SessionStatus.InGame)
                _scheduledSessions.Add(session);
        }

        var plan = _refreshScheduler.Plan(
            _scheduledSessions.Count,
            frameTime,
            _maxSessionRefreshesPerUpdate);
        try
        {
            for (var offset = 0; offset < plan.ScheduledRefreshes; offset++)
            {
                var index = (plan.StartIndex + offset) % _scheduledSessions.Count;
                var refreshStarted = Stopwatch.GetTimestamp();
                RefreshSessionCore(_scheduledSessions[index]);
                refreshLatencyObserver?.Invoke(Stopwatch.GetTimestamp() - refreshStarted);
            }
        }
        finally
        {
            CompleteVisibilityContextBatch();
        }

        RecordSchedulerUpdate(
            _scheduledSessions.Count,
            plan,
            Stopwatch.GetTimestamp() - started);
        return plan;
    }

    /// <summary>
    /// Rebuilds one session's exclusion snapshot on the main thread.
    /// </summary>
    public void RefreshSession(ICommonSession session)
    {
        _visibilityContexts.Clear();
        try
        {
            RefreshSessionCore(session);
        }
        finally
        {
            CompleteVisibilityContextBatch();
        }
    }

    private void CompleteVisibilityContextBatch()
    {
        _visibilityContextCacheEntries = _visibilityContexts.Count;
        _visibilityContextCacheMaxEntries = Math.Max(
            _visibilityContextCacheMaxEntries,
            _visibilityContextCacheEntries);
        _visibilityContexts.Clear();
    }

    private void RefreshSessionCore(ICommonSession session)
    {
        if (!_pvsEnabled || session.Status != SessionStatus.InGame)
        {
            _pvs.ClearSessionCulling(session);
            if (session.Status != SessionStatus.InGame)
            {
                _soundPlayback.ClearSession(session, session.Status != SessionStatus.Disconnected);
                return;
            }
        }

        var started = Stopwatch.GetTimestamp();
        CollectViewers(session);
        _candidates.Clear();
        _visible.Clear();
        _culled.Clear();
        if (_viewers.Count == 0)
        {
            _pvs.ClearSessionCulling(session);
            _soundPlayback.ClearSession(session);
            RecordRefresh(started, 0, false);
            return;
        }

        var visibilityChecks = 0;
        var budgetExhausted = false;
        foreach (var viewer in _viewers)
        {
            _viewerCandidates.Clear();
            var range = MathF.Max(_priorityViewSize * viewer.PvsScale, 1f) / 2f +
                SharedPvsOverrideSystem.SpatialChunkSize;
            var extent = new Vector2(range, range);
            var bounds = new Box2(viewer.WorldPosition - extent, viewer.WorldPosition + extent);
            _lookup.GetEntitiesIntersecting(viewer.MapId, bounds, _viewerCandidates, LookupFlags.All);

            foreach (var candidate in _viewerCandidates)
            {
                if (!_metaQuery.TryComp(candidate, out var metadata) ||
                    metadata.NetEntity == NetEntity.Invalid)
                {
                    continue;
                }

                _candidates.Add(candidate);
                if (!_pvsEnabled)
                    continue;

                // Engine PVS treats a culled ancestor as culling its entire subtree.
                // Map and grid roots are transport dependencies, not visual candidates.
                if (_mapQuery.HasComp(candidate) || _gridQuery.HasComp(candidate))
                {
                    MarkTransformChainVisible(candidate);
                    continue;
                }

                if (budgetExhausted || _visible.Contains(candidate))
                    continue;

                if (visibilityChecks >= _visibilityCheckBudget)
                {
                    budgetExhausted = true;
                    continue;
                }

                visibilityChecks++;
                var hasContext = _visibilityContexts.TryGetValue(candidate, out var context);
                if (hasContext)
                {
                    _visibilityContextCacheHits++;
                }
                else
                {
                    _visibilityContextCacheMisses++;
                    hasContext = _visibility.TryResolveEntityVisibilityContext(candidate, out context);
                    if (hasContext)
                        _visibilityContexts.Add(candidate, context);
                }

                var isVisible = hasContext
                    ? _visibility.IsEntityVisibleFrom(context, viewer.MapId, viewer.ZLevel) ||
                      IsVerticalRenderDependencyVisible(candidate, context, viewer)
                    : _visibility.IsEntityVisibleFrom(candidate, viewer.MapId, viewer.ZLevel) ||
                      IsVerticalRenderDependencyVisible(candidate, viewer);
                if (isVisible)
                {
                    MarkTransformChainVisible(candidate);
                }
            }
        }

        _soundPlayback.RefreshSession(session, _viewers, _candidates, _visible, _soundCulled);

        if (!_pvsEnabled)
        {
            _pvs.ClearSessionCulling(session);
            return;
        }

        if (budgetExhausted)
        {
            // A partial exclusion set could hide an entity visible from a viewer
            // we did not evaluate. Fail open for visuals while preserving the
            // independently evaluated, fail-closed audio exclusions.
            _pvs.ReplaceSessionCulling(session, _soundCulled);
            RecordRefresh(started, visibilityChecks, true);
            return;
        }

        _culled.UnionWith(_candidates);
        _culled.ExceptWith(_visible);
        _culled.UnionWith(_soundCulled);
        _pvs.ReplaceSessionCulling(session, _culled);
        RecordRefresh(started, visibilityChecks, false);
    }

    private void MarkTransformChainVisible(EntityUid candidate)
    {
        var current = candidate;
        for (var depth = 0; depth < MaximumParentDepth; depth++)
        {
            _visible.Add(current);
            if (!_transformQuery.TryComp(current, out var transform))
                return;

            var parent = transform.ParentUid;
            if (!parent.IsValid())
                return;

            current = parent;
        }
    }

    /// <summary>
    /// Lower-floor lights and occluders can affect an opening away from their own tile.
    /// Keep these bounded render inputs in PVS and let client projection clip the result.
    /// </summary>
    private bool IsVerticalRenderDependencyVisible(EntityUid candidate, in ZLevelPvsViewerContext viewer)
    {
        if (!_transformQuery.TryComp(candidate, out var transform))
        {
            return false;
        }

        var candidateWorldZ = _transform.GetWorldZLevel((
            candidate,
            transform,
            CompOrNull<ZLevelPositionComponent>(candidate)));
        return IsVerticalRenderDependencyVisible(
            candidate,
            transform.MapID,
            candidateWorldZ,
            viewer);
    }

    private bool IsVerticalRenderDependencyVisible(
        EntityUid candidate,
        in ZLevelEntityVisibilityContext context,
        in ZLevelPvsViewerContext viewer)
    {
        return IsVerticalRenderDependencyVisible(candidate, context.MapId, context.WorldZ, viewer);
    }

    private bool IsVerticalRenderDependencyVisible(
        EntityUid candidate,
        MapId candidateMap,
        int candidateWorldZ,
        in ZLevelPvsViewerContext viewer)
    {
        var isEnabledLight = _pointLightQuery.TryComp(candidate, out var light) && light.Enabled;
        var isEnabledOccluder = _occluderQuery.TryComp(candidate, out var occluder) && occluder.Enabled;
        if ((!isEnabledLight && !isEnabledOccluder) || candidateMap != viewer.MapId)
            return false;

        var depth = (long) viewer.ZLevel - candidateWorldZ;
        return depth > 0 && depth <= _visibility.MaxVisibleLevelDistance;
    }

    private void CollectViewers(ICommonSession session)
    {
        _viewers.Clear();

        if (session.AttachedEntity is { } attached)
            AddViewer(attached);

        foreach (var subscription in session.ViewSubscriptions)
        {
            if (subscription != session.AttachedEntity)
                AddViewer(subscription);
        }
    }

    private void AddViewer(EntityUid viewer)
    {
        if (!_transformQuery.TryComp(viewer, out var transform) || transform.MapID == MapId.Nullspace)
            return;

        var eye = _eyeQuery.CompOrNull(viewer);
        var worldPosition = _transform.GetWorldPosition(transform) + (eye?.Offset ?? Vector2.Zero);
        var zLevel = _transform.GetWorldZLevel((viewer, transform, CompOrNull<ZLevelPositionComponent>(viewer)));
        var gridUid = transform.GridUid;
        var localPosition = gridUid is { } grid
            ? _transform.ToCoordinates(grid, new MapCoordinates(worldPosition, transform.MapID)).Position
            : worldPosition;
        var localZ = _transform.GetZLevel((viewer, transform, CompOrNull<ZLevelPositionComponent>(viewer)));
        _viewers.Add(new ZLevelPvsViewerContext(
            viewer,
            transform.MapID,
            gridUid,
            worldPosition,
            localPosition,
            localZ,
            zLevel,
            eye?.PvsScale ?? 1f,
            _viewers.Count == 0));
    }

    private void OnPvsEnabled(bool enabled)
    {
        _pvsEnabled = enabled;
        if (enabled)
            return;

        foreach (var session in _players.Sessions)
        {
            _pvs.ClearSessionCulling(session);
        }
    }

    private void RefreshViewSize()
    {
        var normal = _configuration.GetCVar(CVars.NetMaxUpdateRange);
        var priority = _configuration.GetCVar(CVars.NetPvsPriorityRange);
        _priorityViewSize = MathF.Max(8f, MathF.Max(normal, priority));
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.InGame)
            return;

        _pvs.ClearSessionCulling(args.Session);
        _soundPlayback.ClearSession(args.Session, args.NewStatus != SessionStatus.Disconnected);
    }

    private void OnViewerZLevelChanged(Entity<ActorComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        // Mapping and snapshot setup can briefly create an actor before a session is attached.
        var session = entity.Comp.PlayerSession;
        if (session == null ||
            session.Status != SessionStatus.InGame ||
            session.AttachedEntity != entity.Owner)
            return;

        RefreshSession(session);
    }

    private void RecordRefresh(long started, int visibilityChecks, bool budgetExhausted)
    {
        var visibleCandidates = 0;
        if (!budgetExhausted)
        {
            // Engine PVS also needs transform ancestors; metrics count evaluated candidates only.
            foreach (var candidate in _candidates)
            {
                if (_visible.Contains(candidate))
                    visibleCandidates++;
            }
        }

        _metrics.RecordPvsRefresh(
            _viewers.Count,
            _candidates.Count,
            budgetExhausted ? _candidates.Count : visibleCandidates,
            budgetExhausted ? 0 : _culled.Count,
            visibilityChecks,
            budgetExhausted,
            Stopwatch.GetTimestamp() - started);
    }

    private void RecordSchedulerUpdate(
        int activeSessions,
        in ZLevelPvsRefreshPlan plan,
        long elapsedTimestampTicks)
    {
        _schedulerUpdates++;
        _schedulerActiveSessionSamples += activeSessions;
        _schedulerDueRefreshes += plan.DueRefreshes;
        _schedulerRefreshes += plan.ScheduledRefreshes;
        _schedulerDeferredRefreshes += plan.DeferredRefreshes;
        if (plan.DeferredRefreshes > 0)
            _schedulerBudgetExhaustions++;
        _schedulerMaxActiveSessions = Math.Max(_schedulerMaxActiveSessions, activeSessions);
        _schedulerMaxRefreshes = Math.Max(_schedulerMaxRefreshes, plan.ScheduledRefreshes);
        _schedulerMaxDeferredRefreshes = Math.Max(
            _schedulerMaxDeferredRefreshes,
            plan.DeferredRefreshes);
        _schedulerTimestampTicks += elapsedTimestampTicks;
        _schedulerLastTimestampTicks = elapsedTimestampTicks;
        _schedulerMaxTimestampTicks = Math.Max(_schedulerMaxTimestampTicks, elapsedTimestampTicks);
    }

    public void ResetSchedulerMetrics()
    {
        _schedulerUpdates = 0;
        _schedulerActiveSessionSamples = 0;
        _schedulerDueRefreshes = 0;
        _schedulerRefreshes = 0;
        _schedulerDeferredRefreshes = 0;
        _schedulerBudgetExhaustions = 0;
        _schedulerMaxActiveSessions = 0;
        _schedulerMaxRefreshes = 0;
        _schedulerMaxDeferredRefreshes = 0;
        _schedulerTimestampTicks = 0;
        _schedulerLastTimestampTicks = 0;
        _schedulerMaxTimestampTicks = 0;
        _visibilityContextCacheHits = 0;
        _visibilityContextCacheMisses = 0;
        _visibilityContextCacheEntries = 0;
        _visibilityContextCacheMaxEntries = 0;
        _visibilityContexts.Clear();
    }

    internal void ResetSchedulerState()
    {
        _refreshScheduler.Reset();
    }

    private static double TimestampTicksToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}

public readonly record struct ZLevelPvsSchedulerMetricsSnapshot(
    long Updates,
    long ActiveSessionSamples,
    long DueRefreshes,
    long ScheduledRefreshes,
    long DeferredRefreshes,
    long BudgetExhaustions,
    int MaxActiveSessions,
    int MaxRefreshesPerUpdate,
    int MaxDeferredRefreshesPerUpdate,
    double RefreshMilliseconds,
    double LastRefreshMilliseconds,
    double MaxRefreshMilliseconds,
    long VisibilityContextCacheHits,
    long VisibilityContextCacheMisses,
    int VisibilityContextCacheEntries,
    int VisibilityContextCacheMaxEntries)
{
    public double AverageRefreshMilliseconds => Updates == 0 ? 0d : RefreshMilliseconds / Updates;
    public double VisibilityContextCacheHitPercent =>
        VisibilityContextCacheHits + VisibilityContextCacheMisses == 0
            ? 0d
            : VisibilityContextCacheHits * 100d /
              (VisibilityContextCacheHits + VisibilityContextCacheMisses);
}

public readonly record struct ZLevelPvsViewerContext(
    EntityUid Viewer,
    MapId MapId,
    EntityUid? GridUid,
    Vector2 WorldPosition,
    Vector2 LocalPosition,
    int LocalZ,
    int WorldZ,
    float PvsScale,
    bool Primary)
{
    public int ZLevel => WorldZ;
}
