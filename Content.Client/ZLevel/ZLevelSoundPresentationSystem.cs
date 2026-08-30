// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Threading;
using Content.Shared.GameTicking;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using ClientAudioSystem = Robust.Client.Audio.AudioSystem;

namespace Content.Client.ZLevel;

/// <summary>
/// Converts server-authorized vertical sound routes into worker-safe client audio policies.
/// </summary>
public sealed class ZLevelSoundPresentationSystem : EntitySystem
{
    [Dependency] private readonly ClientAudioSystem _audio = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IEyeManager _eyes = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ZLevelViewContextSystem _viewContext = default!;

    private readonly Dictionary<ZLevelSoundPresentationKey, ZLevelSoundPresentation> _presentations = new();
    private readonly ZLevelSoundPolicySnapshot _firstPolicies = new();
    private readonly ZLevelSoundPolicySnapshot _secondPolicies = new();
    private ZLevelSoundPolicySnapshot _activePolicies = new();

    private EntityQuery<TransformComponent> _transformQuery;
    private Attenuation _attenuation;

    private long _snapshotsReceived;
    private long _snapshotPresentationsReceived;
    private long _invalidPresentations;
    private long _frames;
    private long _audioCandidates;
    private long _crossFloorCandidates;
    private long _authorizedPolicies;
    private long _mutedPolicies;
    private long _buildTimestampTicks;
    private long _lastBuildTimestampTicks;
    private long _maxBuildTimestampTicks;
    private long _processedStreams;
    private long _processedAuthorized;
    private long _processedMuted;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(EyeSystem));
        UpdatesBefore.Add(typeof(ClientAudioSystem));

        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeNetworkEvent<ZLevelSoundPresentationSnapshotEvent>(OnPresentationSnapshot);
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.CVar(
            _configuration,
            CVars.AudioAttenuation,
            value => _attenuation = (Attenuation) value,
            true);
        _audio.StreamProcessed += OnStreamProcessed;
    }

    public override void Shutdown()
    {
        _audio.StreamProcessed -= OnStreamProcessed;
        _presentations.Clear();
        _firstPolicies.Policies.Clear();
        _secondPolicies.Policies.Clear();
        Interlocked.Exchange(ref _activePolicies, new ZLevelSoundPolicySnapshot());
        base.Shutdown();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var started = Stopwatch.GetTimestamp();
        var active = ReadActivePolicies();
        var next = ReferenceEquals(active, _firstPolicies) ? _secondPolicies : _firstPolicies;
        next.Policies.Clear();
        next.Authorized = 0;
        next.Muted = 0;

        var candidates = 0;
        var crossFloor = 0;
        var eye = _eyes.CurrentEye;
        if (_viewContext.TryGetViewContext(eye, _players.LocalEntity, out var view) &&
            view.MapId != MapId.Nullspace)
        {
            BuildPolicies(next, eye.Position, view, ref candidates, ref crossFloor);
        }

        Interlocked.Exchange(ref _activePolicies, next);
        var elapsed = Stopwatch.GetTimestamp() - started;
        _frames++;
        _audioCandidates += candidates;
        _crossFloorCandidates += crossFloor;
        _authorizedPolicies += next.Authorized;
        _mutedPolicies += next.Muted;
        _buildTimestampTicks += elapsed;
        _lastBuildTimestampTicks = elapsed;
        _maxBuildTimestampTicks = Math.Max(_maxBuildTimestampTicks, elapsed);
    }

    private void BuildPolicies(
        ZLevelSoundPolicySnapshot policies,
        MapCoordinates listener,
        in ZLevelViewContext view,
        ref int candidates,
        ref int crossFloor)
    {
        EntityUid? viewerGrid = null;
        NetEntity? viewerNet = null;
        if (view.Viewer is { } viewer &&
            _transformQuery.TryComp(viewer, out var viewerTransform) &&
            TryGetNetEntity(viewer, out var resolvedViewerNet))
        {
            viewerGrid = viewerTransform.GridUid;
            viewerNet = resolvedViewerNet;
        }

        var query = AllEntityQuery<AudioComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var audio, out var transform))
        {
            if (audio.Global ||
                transform.MapID == MapId.Nullspace ||
                transform.MapID != view.MapId)
            {
                continue;
            }

            candidates++;
            var sourceWorldZ = _transform.GetWorldZLevel((
                uid,
                transform,
                CompOrNull<ZLevelPositionComponent>(uid)));
            if (sourceWorldZ == view.WorldZLevel)
                continue;

            crossFloor++;
            var policy = ZLevelSoundClientPolicy.Muted;
            if (TryBuildAuthorizedPolicy(
                    uid,
                    audio,
                    transform,
                    listener,
                    view,
                    viewerGrid,
                    viewerNet,
                    out var authorized))
            {
                policy = authorized;
                policies.Authorized++;
            }
            else
            {
                policies.Muted++;
            }

            policies.Policies[uid] = policy;
        }
    }

    private bool TryBuildAuthorizedPolicy(
        EntityUid audioUid,
        AudioComponent audio,
        TransformComponent audioTransform,
        MapCoordinates listener,
        in ZLevelViewContext view,
        EntityUid? viewerGrid,
        NetEntity? viewerNet,
        out ZLevelSoundClientPolicy policy)
    {
        policy = default;
        if (viewerGrid is not { } listenerGrid ||
            viewerNet is not { } listenerNet ||
            audioTransform.GridUid is not { } sourceGrid ||
            sourceGrid != listenerGrid ||
            !TryGetNetEntity(audioUid, out var audioNet) ||
            !_presentations.TryGetValue(
                new ZLevelSoundPresentationKey(audioNet.Value, listenerNet),
                out var presentation) ||
            presentation.MapId != view.MapId ||
            presentation.ListenerLocalZ != view.LocalZLevel ||
            !TryGetEntity(presentation.Grid, out var presentationGrid) ||
            presentationGrid.Value != sourceGrid)
        {
            return false;
        }

        var sourceLocalZ = _transform.GetZLevel((
            audioUid,
            audioTransform,
            CompOrNull<ZLevelPositionComponent>(audioUid)));
        if (presentation.SourceLocalZ != sourceLocalZ ||
            !IsFinite(presentation.PortalLocalPosition) ||
            !float.IsFinite(presentation.Distance) ||
            presentation.Distance < 0f ||
            presentation.Distance > audio.Params.MaxDistance ||
            !float.IsFinite(presentation.Transmission) ||
            presentation.Transmission <= 0f ||
            presentation.Transmission > 1f)
        {
            return false;
        }

        var portalPosition = _transform.ToMapCoordinates(
            new EntityCoordinates(sourceGrid, presentation.PortalLocalPosition)).Position;
        var portalDelta = portalPosition - listener.Position;
        var portalDistance = portalDelta.Length();
        if (!IsFinite(portalPosition) || !float.IsFinite(portalDistance))
            return false;

        var gainMultiplier = GetRouteGainMultiplier(
            _attenuation,
            _audio.GetAudioDistance(portalDistance),
            _audio.GetAudioDistance(MathF.Max(presentation.Distance, portalDistance)),
            audio.RolloffFactor,
            audio.ReferenceDistance,
            audio.MaxDistance,
            presentation.Transmission);
        if (!float.IsFinite(gainMultiplier) || gainMultiplier < 0f)
            return false;

        var occlusion = (audio.Flags & AudioFlags.NoOcclusion) != 0
            ? 0f
            : _audio.GetOcclusion(
                listener,
                portalDelta,
                portalDistance,
                audioTransform.ParentUid);
        if (!float.IsFinite(occlusion) || occlusion < 0f)
            return false;

        policy = new ZLevelSoundClientPolicy(
            ZLevelSoundClientPolicyMode.Authorized,
            portalPosition,
            gainMultiplier,
            occlusion);
        return true;
    }

    private void OnStreamProcessed(
        EntityUid uid,
        AudioComponent component,
        TransformComponent transform,
        MapCoordinates listener)
    {
        var policies = ReadActivePolicies();
        if (!policies.Policies.TryGetValue(uid, out var policy))
            return;

        Interlocked.Increment(ref _processedStreams);
        if (policy.Mode != ZLevelSoundClientPolicyMode.Authorized)
        {
            component.Gain = 0f;
            Interlocked.Increment(ref _processedMuted);
            return;
        }

        component.Position = policy.Position;
        component.Gain = SharedAudioSystem.VolumeToGain(component.Params.Volume) * policy.GainMultiplier;
        component.Occlusion = policy.Occlusion;
        Interlocked.Increment(ref _processedAuthorized);
    }

    private void OnPresentationSnapshot(ZLevelSoundPresentationSnapshotEvent message)
    {
        _presentations.Clear();
        foreach (var presentation in message.Presentations)
        {
            if (!IsValidPresentation(presentation) ||
                !_presentations.TryAdd(
                    new ZLevelSoundPresentationKey(presentation.Audio, presentation.Viewer),
                    presentation))
            {
                _invalidPresentations++;
            }
        }

        _snapshotsReceived++;
        _snapshotPresentationsReceived += message.Presentations.Length;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent message)
    {
        _presentations.Clear();
    }

    public ZLevelSoundClientMetrics Snapshot()
    {
        var active = ReadActivePolicies();
        return new ZLevelSoundClientMetrics(
            _snapshotsReceived,
            _snapshotPresentationsReceived,
            _invalidPresentations,
            _frames,
            _audioCandidates,
            _crossFloorCandidates,
            _authorizedPolicies,
            _mutedPolicies,
            Interlocked.Read(ref _processedStreams),
            Interlocked.Read(ref _processedAuthorized),
            Interlocked.Read(ref _processedMuted),
            _buildTimestampTicks,
            _lastBuildTimestampTicks,
            _maxBuildTimestampTicks,
            _presentations.Count,
            active.Authorized,
            active.Muted);
    }

    public bool HasCurrentPolicy(EntityUid audio)
    {
        return ReadActivePolicies().Policies.ContainsKey(audio);
    }

    public bool TryGetCurrentAuthorizedPolicy(
        EntityUid audio,
        out Vector2 position,
        out float gainMultiplier)
    {
        if (ReadActivePolicies().Policies.TryGetValue(audio, out var policy) &&
            policy.Mode == ZLevelSoundClientPolicyMode.Authorized)
        {
            position = policy.Position;
            gainMultiplier = policy.GainMultiplier;
            return true;
        }

        position = default;
        gainMultiplier = default;
        return false;
    }

    public void ResetMetrics()
    {
        _snapshotsReceived = 0;
        _snapshotPresentationsReceived = 0;
        _invalidPresentations = 0;
        _frames = 0;
        _audioCandidates = 0;
        _crossFloorCandidates = 0;
        _authorizedPolicies = 0;
        _mutedPolicies = 0;
        _buildTimestampTicks = 0;
        _lastBuildTimestampTicks = 0;
        _maxBuildTimestampTicks = 0;
        Interlocked.Exchange(ref _processedStreams, 0);
        Interlocked.Exchange(ref _processedAuthorized, 0);
        Interlocked.Exchange(ref _processedMuted, 0);
    }

    private ZLevelSoundPolicySnapshot ReadActivePolicies()
    {
        return Interlocked.CompareExchange(ref _activePolicies, null!, null!);
    }

    public static float GetRouteGainMultiplier(
        Attenuation attenuation,
        float portalDistance,
        float routeDistance,
        float rolloffFactor,
        float referenceDistance,
        float maxDistance,
        float transmission)
    {
        if (!float.IsFinite(transmission) || transmission <= 0f)
            return 0f;

        var portalGain = GetDistanceGain(
            attenuation,
            portalDistance,
            rolloffFactor,
            referenceDistance,
            maxDistance);
        var routeGain = GetDistanceGain(
            attenuation,
            routeDistance,
            rolloffFactor,
            referenceDistance,
            maxDistance);
        if (portalGain <= float.Epsilon)
            return 0f;

        return Math.Clamp(transmission * routeGain / portalGain, 0f, 1f);
    }

    private static float GetDistanceGain(
        Attenuation attenuation,
        float distance,
        float rolloffFactor,
        float referenceDistance,
        float maxDistance)
    {
        if (!float.IsFinite(distance) ||
            !float.IsFinite(rolloffFactor) ||
            !float.IsFinite(referenceDistance) ||
            !float.IsFinite(maxDistance) ||
            rolloffFactor <= 0f ||
            referenceDistance <= 0f)
        {
            return 1f;
        }

        var clamped = attenuation is Attenuation.InverseDistanceClamped or
            Attenuation.LinearDistanceClamped or Attenuation.ExponentDistanceClamped;
        if (clamped)
            distance = Math.Clamp(distance, referenceDistance, MathF.Max(referenceDistance, maxDistance));

        var gain = attenuation switch
        {
            Attenuation.NoAttenuation => 1f,
            Attenuation.InverseDistance or Attenuation.InverseDistanceClamped =>
                referenceDistance / (referenceDistance + rolloffFactor * (distance - referenceDistance)),
            Attenuation.LinearDistance or Attenuation.LinearDistanceClamped when maxDistance > referenceDistance =>
                1f - rolloffFactor * (distance - referenceDistance) / (maxDistance - referenceDistance),
            Attenuation.ExponentDistance or Attenuation.ExponentDistanceClamped =>
                MathF.Pow(MathF.Max(distance, float.Epsilon) / referenceDistance, -rolloffFactor),
            _ => 1f,
        };
        return float.IsNaN(gain) ? 0f : Math.Clamp(gain, 0f, 1f);
    }

    private static bool IsValidPresentation(in ZLevelSoundPresentation presentation)
    {
        return presentation.Audio != NetEntity.Invalid &&
               presentation.Viewer != NetEntity.Invalid &&
               presentation.Grid != NetEntity.Invalid &&
               presentation.MapId != MapId.Nullspace &&
               presentation.SourceLocalZ != presentation.ListenerLocalZ &&
               IsFinite(presentation.PortalLocalPosition) &&
               float.IsFinite(presentation.Distance) &&
               presentation.Distance >= 0f &&
               float.IsFinite(presentation.Transmission) &&
               presentation.Transmission > 0f &&
               presentation.Transmission <= 1f;
    }

    private static bool IsFinite(Vector2 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y);
    }
}

internal readonly record struct ZLevelSoundPresentationKey(NetEntity Audio, NetEntity Viewer);

internal enum ZLevelSoundClientPolicyMode : byte
{
    Muted,
    Authorized,
}

internal readonly record struct ZLevelSoundClientPolicy(
    ZLevelSoundClientPolicyMode Mode,
    Vector2 Position,
    float GainMultiplier,
    float Occlusion)
{
    public static readonly ZLevelSoundClientPolicy Muted = new(
        ZLevelSoundClientPolicyMode.Muted,
        Vector2.Zero,
        0f,
        0f);
}

internal sealed class ZLevelSoundPolicySnapshot
{
    public readonly Dictionary<EntityUid, ZLevelSoundClientPolicy> Policies = new();
    public int Authorized;
    public int Muted;
}

public readonly record struct ZLevelSoundClientMetrics(
    long SnapshotsReceived,
    long SnapshotPresentationsReceived,
    long InvalidPresentations,
    long Frames,
    long AudioCandidates,
    long CrossFloorCandidates,
    long AuthorizedPolicies,
    long MutedPolicies,
    long ProcessedStreams,
    long ProcessedAuthorized,
    long ProcessedMuted,
    long BuildTimestampTicks,
    long LastBuildTimestampTicks,
    long MaxBuildTimestampTicks,
    int ActivePresentations,
    int CurrentAuthorized,
    int CurrentMuted)
{
    public double BuildMilliseconds => ToMilliseconds(BuildTimestampTicks);
    public double AverageBuildMilliseconds => Frames == 0 ? 0d : BuildMilliseconds / Frames;
    public double LastBuildMilliseconds => ToMilliseconds(LastBuildTimestampTicks);
    public double MaxBuildMilliseconds => ToMilliseconds(MaxBuildTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
