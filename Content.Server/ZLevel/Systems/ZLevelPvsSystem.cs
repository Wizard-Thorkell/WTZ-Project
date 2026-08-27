// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Systems;

/// <summary>
/// Builds per-session vertical visibility exclusions before the engine serializes spatial PVS chunks.
/// </summary>
public sealed class ZLevelPvsSystem : EntitySystem
{
    private const float RefreshInterval = 0.1f;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMetricsSystem _metrics = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;

    private readonly List<ViewerContext> _viewers = new();
    private readonly HashSet<EntityUid> _viewerCandidates = new();
    private readonly HashSet<EntityUid> _candidates = new();
    private readonly HashSet<EntityUid> _visible = new();
    private readonly HashSet<EntityUid> _culled = new();

    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<MetaDataComponent> _metaQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private float _refreshAccumulator = RefreshInterval;
    private float _priorityViewSize;
    private bool _pvsEnabled;

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();
        _metaQuery = GetEntityQuery<MetaDataComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        Subs.CVar(_configuration, CVars.NetPVS, OnPvsEnabled, true);
        Subs.CVar(_configuration, CVars.NetMaxUpdateRange, _ => RefreshViewSize(), true);
        Subs.CVar(_configuration, CVars.NetPvsPriorityRange, _ => RefreshViewSize(), true);
    }

    public override void Shutdown()
    {
        foreach (var session in _players.Sessions)
        {
            _pvs.ClearSessionCulling(session);
        }

        base.Shutdown();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_pvsEnabled)
            return;

        _refreshAccumulator += frameTime;
        if (_refreshAccumulator < RefreshInterval)
            return;

        _refreshAccumulator %= RefreshInterval;
        foreach (var session in _players.Sessions)
        {
            RefreshSession(session);
        }
    }

    /// <summary>
    /// Rebuilds one session's exclusion snapshot on the main thread.
    /// </summary>
    public void RefreshSession(ICommonSession session)
    {
        if (!_pvsEnabled || session.Status != SessionStatus.InGame)
        {
            _pvs.ClearSessionCulling(session);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        CollectViewers(session);
        _candidates.Clear();
        _visible.Clear();
        _culled.Clear();
        if (_viewers.Count == 0)
        {
            _pvs.ClearSessionCulling(session);
            RecordRefresh(started);
            return;
        }

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
                if (_visibility.IsEntityVisibleFrom(candidate, viewer.MapId, viewer.ZLevel))
                    _visible.Add(candidate);
            }
        }

        _culled.UnionWith(_candidates);
        _culled.ExceptWith(_visible);
        _pvs.ReplaceSessionCulling(session, _culled);
        RecordRefresh(started);
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
        _viewers.Add(new ViewerContext(transform.MapID, worldPosition, zLevel, eye?.PvsScale ?? 1f));
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

    private void RecordRefresh(long started)
    {
        _metrics.RecordPvsRefresh(
            _viewers.Count,
            _candidates.Count,
            _visible.Count,
            _culled.Count,
            Stopwatch.GetTimestamp() - started);
    }

    private readonly record struct ViewerContext(
        MapId MapId,
        Vector2 WorldPosition,
        int ZLevel,
        float PvsScale);
}
