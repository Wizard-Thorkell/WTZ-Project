// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Robust.Server.Player;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Authorizes existing positional audio streams for cross-floor listeners and publishes
/// per-session presentation snapshots. It never creates or duplicates an audio entity.
/// </summary>
public sealed class ZLevelSoundPlaybackSystem : EntitySystem
{
    public const int MaximumRouteChecksPerRefresh = 4_096;
    public const int MaximumPresentationsPerRefresh = 1_024;
    public const int MaximumParentDepth = 64;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ZLevelSoundRouteSystem _routes = default!;

    private readonly List<EntityUid> _audioCandidates = new();
    private readonly List<ZLevelPvsViewerContext> _listeners = new();
    private readonly List<ZLevelSoundPortal> _route = new();
    private readonly List<ZLevelSoundPresentation> _audioPresentations = new();
    private readonly List<ZLevelSoundPresentation> _presentations = new();
    private readonly List<EntityUid> _parents = new();
    private readonly Dictionary<ICommonSession, ZLevelSoundPresentation[]> _sessionSnapshots = new();

    private EntityQuery<AudioComponent> _audioQuery;
    private EntityQuery<MapComponent> _mapQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private int _maxRouteChecksPerRefresh = 128;
    private int _maxPresentationsPerRefresh = 128;

    private long _refreshes;
    private long _audioCandidatesVisited;
    private long _routeChecks;
    private long _authorizedPresentations;
    private long _routeBudgetExhaustions;
    private long _presentationBudgetExhaustions;
    private long _parentDepthFailures;
    private long _snapshotsSent;
    private long _snapshotPresentationsSent;
    private long _refreshTimestampTicks;
    private long _lastRefreshTimestampTicks;
    private long _maxRefreshTimestampTicks;

    public int MaxRouteChecksPerRefresh => _maxRouteChecksPerRefresh;
    public int MaxPresentationsPerRefresh => _maxPresentationsPerRefresh;

    public override void Initialize()
    {
        base.Initialize();

        _audioQuery = GetEntityQuery<AudioComponent>();
        _mapQuery = GetEntityQuery<MapComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundPlaybackMaxRouteChecksPerRefresh,
            value => _maxRouteChecksPerRefresh = Math.Clamp(value, 0, MaximumRouteChecksPerRefresh),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelSoundPlaybackMaxPresentationsPerRefresh,
            value => _maxPresentationsPerRefresh = Math.Clamp(value, 0, MaximumPresentationsPerRefresh),
            true);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        _sessionSnapshots.Clear();
        base.Shutdown();
    }

    public ZLevelSoundPlaybackRefreshResult RefreshSession(
        ICommonSession session,
        IReadOnlyList<ZLevelPvsViewerContext> viewers,
        HashSet<EntityUid> candidates,
        HashSet<EntityUid> visible,
        HashSet<EntityUid> culled)
    {
        var started = Stopwatch.GetTimestamp();
        _audioCandidates.Clear();
        _listeners.Clear();
        _presentations.Clear();
        culled.Clear();

        foreach (var candidate in candidates)
        {
            if (_audioQuery.HasComp(candidate))
                _audioCandidates.Add(candidate);
        }

        for (var i = 0; i < viewers.Count; i++)
            _listeners.Add(viewers[i]);

        _audioCandidates.Sort();
        _listeners.Sort(static (left, right) =>
        {
            var primary = right.Primary.CompareTo(left.Primary);
            return primary != 0 ? primary : left.Viewer.CompareTo(right.Viewer);
        });

        var routeChecks = 0;
        var routeBudgetExhausted = false;
        var presentationBudgetExhausted = false;
        var parentDepthFailures = 0;

        foreach (var audioUid in _audioCandidates)
        {
            if (!_audioQuery.TryComp(audioUid, out var audio) ||
                !_transformQuery.TryComp(audioUid, out var audioTransform) ||
                audio.Global ||
                audioTransform.MapID == MapId.Nullspace)
            {
                continue;
            }

            if (!SharedAudioSystem.IsAudioTargetAllowed(audio, session.AttachedEntity))
            {
                visible.Remove(audioUid);
                culled.Add(audioUid);
                continue;
            }

            var sourceWorldZ = _transform.GetWorldZLevel((
                audioUid,
                audioTransform,
                CompOrNull<ZLevelPositionComponent>(audioUid)));
            var sourceLocalZ = _transform.GetZLevel((
                audioUid,
                audioTransform,
                CompOrNull<ZLevelPositionComponent>(audioUid)));
            var sourceWorldPosition = _transform.GetWorldPosition(audioTransform);
            var sourceGrid = audioTransform.GridUid;
            var maxDistance = MathF.Min(audio.Params.MaxDistance, ZLevelSoundRouteSystem.MaximumRouteDistance);
            var sameFloorListener = false;
            _audioPresentations.Clear();

            foreach (var listener in _listeners)
            {
                if (listener.MapId != audioTransform.MapID)
                    continue;

                if (listener.WorldZ == sourceWorldZ)
                {
                    sameFloorListener = true;
                    continue;
                }

                if (sourceGrid is not { } gridUid ||
                    listener.GridUid != gridUid ||
                    !float.IsFinite(maxDistance) ||
                    maxDistance <= 0f ||
                    Vector2.DistanceSquared(sourceWorldPosition, listener.WorldPosition) > maxDistance * maxDistance ||
                    !_gridQuery.TryComp(gridUid, out var grid))
                {
                    continue;
                }

                if (_presentations.Count + _audioPresentations.Count >= _maxPresentationsPerRefresh)
                {
                    presentationBudgetExhausted = true;
                    continue;
                }

                if (routeChecks >= _maxRouteChecksPerRefresh)
                {
                    routeBudgetExhausted = true;
                    continue;
                }

                routeChecks++;
                var sourceLocalPosition = _transform.ToCoordinates(
                    gridUid,
                    new MapCoordinates(sourceWorldPosition, audioTransform.MapID)).Position;
                var source = new ZLevelSoundRouteEndpoint(gridUid, sourceLocalPosition, sourceLocalZ);
                var target = new ZLevelSoundRouteEndpoint(
                    gridUid,
                    listener.LocalPosition,
                    listener.LocalZ);
                _route.Clear();
                var result = _routes.FindRoute(
                    (gridUid, grid),
                    source,
                    target,
                    maxDistance,
                    _route,
                    ZLevelSoundMediumMode.RequirePressure);
                if (!result.Succeeded || _route.Count == 0)
                    continue;

                var portal = _route[^1];
                _audioPresentations.Add(new ZLevelSoundPresentation(
                    GetNetEntity(audioUid),
                    GetNetEntity(listener.Viewer),
                    GetNetEntity(gridUid),
                    listener.MapId,
                    sourceLocalZ,
                    listener.LocalZ,
                    portal.LocalPosition,
                    result.Distance,
                    result.Transmission));
            }

            var hasVerticalAuthorization = _audioPresentations.Count > 0;
            if (sameFloorListener || hasVerticalAuthorization)
            {
                if (TryMarkTransformChainVisible(audioUid, visible))
                {
                    _presentations.AddRange(_audioPresentations);
                }
                else
                {
                    parentDepthFailures++;
                    hasVerticalAuthorization = false;
                }
            }

            if (!sameFloorListener && !hasVerticalAuthorization)
            {
                visible.Remove(audioUid);
                culled.Add(audioUid);
            }
        }

        PublishSnapshot(session, _presentations);
        var elapsed = Stopwatch.GetTimestamp() - started;
        _refreshes++;
        _audioCandidatesVisited += _audioCandidates.Count;
        _routeChecks += routeChecks;
        _authorizedPresentations += _presentations.Count;
        _routeBudgetExhaustions += routeBudgetExhausted ? 1 : 0;
        _presentationBudgetExhaustions += presentationBudgetExhausted ? 1 : 0;
        _parentDepthFailures += parentDepthFailures;
        _refreshTimestampTicks += elapsed;
        _lastRefreshTimestampTicks = elapsed;
        _maxRefreshTimestampTicks = Math.Max(_maxRefreshTimestampTicks, elapsed);

        return new ZLevelSoundPlaybackRefreshResult(
            _audioCandidates.Count,
            routeChecks,
            _presentations.Count,
            routeBudgetExhausted,
            presentationBudgetExhausted,
            parentDepthFailures);
    }

    public void ClearSession(ICommonSession session, bool notify = true)
    {
        if (!_sessionSnapshots.Remove(session, out var previous) || previous.Length == 0)
            return;

        if (!notify)
            return;

        RaiseNetworkEvent(
            new ZLevelSoundPresentationSnapshotEvent(Array.Empty<ZLevelSoundPresentation>()),
            session.Channel);
        _snapshotsSent++;
    }

    public bool TryGetSessionPresentations(
        ICommonSession session,
        out IReadOnlyList<ZLevelSoundPresentation> presentations)
    {
        if (_sessionSnapshots.TryGetValue(session, out var snapshot))
        {
            presentations = snapshot;
            return true;
        }

        presentations = Array.Empty<ZLevelSoundPresentation>();
        return false;
    }

    public ZLevelSoundPlaybackMetrics Snapshot()
    {
        var activePresentations = 0;
        foreach (var snapshot in _sessionSnapshots.Values)
        {
            activePresentations += snapshot.Length;
        }

        return new ZLevelSoundPlaybackMetrics(
            _refreshes,
            _audioCandidatesVisited,
            _routeChecks,
            _authorizedPresentations,
            _routeBudgetExhaustions,
            _presentationBudgetExhaustions,
            _parentDepthFailures,
            _snapshotsSent,
            _snapshotPresentationsSent,
            _refreshTimestampTicks,
            _lastRefreshTimestampTicks,
            _maxRefreshTimestampTicks,
            _sessionSnapshots.Count,
            activePresentations);
    }

    public void ResetMetrics()
    {
        _refreshes = 0;
        _audioCandidatesVisited = 0;
        _routeChecks = 0;
        _authorizedPresentations = 0;
        _routeBudgetExhaustions = 0;
        _presentationBudgetExhaustions = 0;
        _parentDepthFailures = 0;
        _snapshotsSent = 0;
        _snapshotPresentationsSent = 0;
        _refreshTimestampTicks = 0;
        _lastRefreshTimestampTicks = 0;
        _maxRefreshTimestampTicks = 0;
    }

    private void PublishSnapshot(ICommonSession session, List<ZLevelSoundPresentation> presentations)
    {
        if (_sessionSnapshots.TryGetValue(session, out var previous) &&
            SnapshotEquals(previous, presentations))
        {
            return;
        }

        if (presentations.Count == 0)
        {
            if (!_sessionSnapshots.Remove(session))
                return;

            RaiseNetworkEvent(
                new ZLevelSoundPresentationSnapshotEvent(Array.Empty<ZLevelSoundPresentation>()),
                session.Channel);
            _snapshotsSent++;
            return;
        }

        var snapshot = presentations.ToArray();
        _sessionSnapshots[session] = snapshot;
        RaiseNetworkEvent(new ZLevelSoundPresentationSnapshotEvent(snapshot), session.Channel);
        _snapshotsSent++;
        _snapshotPresentationsSent += snapshot.Length;
    }

    private bool TryMarkTransformChainVisible(EntityUid audio, HashSet<EntityUid> visible)
    {
        _parents.Clear();
        var current = audio;
        for (var depth = 0; depth < MaximumParentDepth; depth++)
        {
            if (!_transformQuery.TryComp(current, out var transform))
                return false;

            _parents.Add(current);
            var parent = transform.ParentUid;
            if (!parent.IsValid() || _gridQuery.HasComp(parent) || _mapQuery.HasComp(parent))
            {
                foreach (var entity in _parents)
                {
                    visible.Add(entity);
                }

                return true;
            }

            current = parent;
        }

        return false;
    }

    private static bool SnapshotEquals(
        ZLevelSoundPresentation[] previous,
        List<ZLevelSoundPresentation> current)
    {
        if (previous.Length != current.Count)
            return false;

        for (var i = 0; i < previous.Length; i++)
        {
            if (previous[i] != current[i])
                return false;
        }

        return true;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _sessionSnapshots.Remove(args.Session);
    }
}

public readonly record struct ZLevelSoundPlaybackRefreshResult(
    int AudioCandidates,
    int RouteChecks,
    int Presentations,
    bool RouteBudgetExhausted,
    bool PresentationBudgetExhausted,
    int ParentDepthFailures);

public readonly record struct ZLevelSoundPlaybackMetrics(
    long Refreshes,
    long AudioCandidates,
    long RouteChecks,
    long AuthorizedPresentations,
    long RouteBudgetExhaustions,
    long PresentationBudgetExhaustions,
    long ParentDepthFailures,
    long SnapshotsSent,
    long SnapshotPresentationsSent,
    long RefreshTimestampTicks,
    long LastRefreshTimestampTicks,
    long MaxRefreshTimestampTicks,
    int ActiveSessions,
    int ActivePresentations)
{
    public double RefreshMilliseconds => ToMilliseconds(RefreshTimestampTicks);
    public double AverageRefreshMilliseconds => Refreshes == 0
        ? 0d
        : RefreshMilliseconds / Refreshes;
    public double LastRefreshMilliseconds => ToMilliseconds(LastRefreshTimestampTicks);
    public double MaxRefreshMilliseconds => ToMilliseconds(MaxRefreshTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
