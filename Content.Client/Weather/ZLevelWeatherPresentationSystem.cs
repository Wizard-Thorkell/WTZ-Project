// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Diagnostics;
using Content.Shared.CCVar;
using Content.Shared.Light.Components;
using Content.Shared.Weather;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using IGameTiming = Robust.Shared.Timing.IGameTiming;

namespace Content.Client.Weather;

/// <summary>
/// Builds bounded active-floor weather masks and deterministic same-floor audio
/// probes while leaving exposure policy in <see cref="SharedWeatherSystem"/>.
/// </summary>
public sealed class ZLevelWeatherPresentationSystem : EntitySystem
{
    public const int DefaultMaxMaskTileChecksPerFrame = 16_384;
    public const int DefaultMaxMaskRunsPerFrame = 8_192;
    public const int DefaultMaxAudioTileChecksPerFrame = 64;
    public const int MaximumMaskTileChecksPerFrame = 1_000_000;
    public const int MaximumMaskRunsPerFrame = 1_000_000;
    public const int MaximumAudioTileChecksPerFrame = 4_096;
    public const int AudioSearchRadius = 3;

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelMapSystem _zLevelMaps = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;

    private static readonly Vector2i[] AudioOffsets = BuildAudioOffsets();

    private List<Entity<MapGridComponent>> _grids = new();
    private readonly List<WeatherMaskGridContext> _contexts = new();
    private readonly List<ZLevelWeatherMaskBatch> _batches = new();
    private readonly List<ZLevelWeatherMaskRun> _runs = new();
    private readonly PresentationFrameBudget _budget = new();

    private int _maxMaskTileChecksPerFrame = DefaultMaxMaskTileChecksPerFrame;
    private int _maxMaskRunsPerFrame = DefaultMaxMaskRunsPerFrame;
    private int _maxAudioTileChecksPerFrame = DefaultMaxAudioTileChecksPerFrame;
    private bool _maskEntireViewport;

    private long _maskPlans;
    private long _maskGridCandidates;
    private long _maskGridLayers;
    private long _maskTileChecks;
    private long _maskBlockedTiles;
    private long _maskRuns;
    private long _maskFailClosedPlans;
    private long _maskTileBudgetExhaustions;
    private long _maskRunBudgetExhaustions;
    private long _maskBuildTimestampTicks;
    private long _maskLastBuildTimestampTicks;
    private long _maskMaxBuildTimestampTicks;
    private long _maskRenderFrames;
    private long _maskRenderBatches;
    private long _maskRenderRuns;
    private long _maskRenderDrawCalls;
    private long _maskRenderFailClosedFrames;
    private long _maskRenderTimestampTicks;
    private long _maskRenderLastTimestampTicks;
    private long _maskRenderMaxTimestampTicks;
    private long _audioQueries;
    private long _audioTileChecks;
    private long _audioDirectExposures;
    private long _audioNearbyExposures;
    private long _audioBlockedQueries;
    private long _audioInvalidQueries;
    private long _audioBudgetExhaustions;

    public IReadOnlyList<ZLevelWeatherMaskBatch> Batches => _batches;
    public IReadOnlyList<ZLevelWeatherMaskRun> Runs => _runs;
    public bool MaskEntireViewport => _maskEntireViewport;
    public int MaxMaskTileChecksPerFrame => _maxMaskTileChecksPerFrame;
    public int MaxMaskRunsPerFrame => _maxMaskRunsPerFrame;
    public int MaxAudioTileChecksPerFrame => _maxAudioTileChecksPerFrame;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(
            _configuration,
            CCVars.ZLevelWeatherMaskMaxTileChecksPerFrame,
            value => SetLimit(
                ref _maxMaskTileChecksPerFrame,
                value,
                MaximumMaskTileChecksPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelWeatherMaskMaxRunsPerFrame,
            value => SetLimit(
                ref _maxMaskRunsPerFrame,
                value,
                MaximumMaskRunsPerFrame),
            true);
        Subs.CVar(
            _configuration,
            CCVars.ZLevelWeatherAudioMaxTileChecksPerFrame,
            value => SetLimit(
                ref _maxAudioTileChecksPerFrame,
                value,
                MaximumAudioTileChecksPerFrame),
            true);
    }

    public override void Shutdown()
    {
        _grids.Clear();
        _contexts.Clear();
        _batches.Clear();
        _runs.Clear();
        base.Shutdown();
    }

    /// <summary>
    /// Builds one atomic mask plan for a viewport. If the whole plan cannot fit
    /// its frame budget, callers mask the complete viewport instead of exposing
    /// an arbitrary subset of indoor tiles to weather.
    /// </summary>
    public int BuildMask(
        SharedWeatherSystem weather,
        MapId mapId,
        Box2 worldBounds,
        int viewerWorldZ)
    {
        var started = Stopwatch.GetTimestamp();
        _maskEntireViewport = false;
        _grids.Clear();
        _contexts.Clear();
        _batches.Clear();
        _runs.Clear();

        var budget = EnsureFrameBudget();
        if (mapId == MapId.Nullspace ||
            !worldBounds.IsValid() ||
            worldBounds.Width <= 0f ||
            worldBounds.Height <= 0f)
        {
            RecordMaskBuild(started);
            return 0;
        }

        _mapManager.FindGridsIntersecting(
            mapId,
            worldBounds,
            ref _grids,
            approx: true,
            includeMap: true);
        BuildContexts(worldBounds, viewerWorldZ);
        _contexts.Sort(static (left, right) => left.GridUid.CompareTo(right.GridUid));
        _maskGridCandidates += _grids.Count;
        _maskGridLayers += _contexts.Count;

        long requiredTileChecks = 0;
        foreach (var context in _contexts)
        {
            requiredTileChecks += (long) context.Width * context.Height;
            if (requiredTileChecks > budget.RemainingMaskTileChecks)
            {
                FailMaskPlan(budget, tileBudget: true);
                RecordMaskBuild(started);
                return 0;
            }
        }

        budget.RemainingMaskTileChecks -= (int) requiredTileChecks;
        budget.CurrentMaskTileChecks += (int) requiredTileChecks;

        foreach (var context in _contexts)
        {
            var firstRun = _runs.Count;
            var blockedTiles = 0;
            for (var y = context.MinimumTile.Y; y < context.MaximumTile.Y; y++)
            {
                var runStart = int.MinValue;
                for (var x = context.MinimumTile.X; x < context.MaximumTile.X; x++)
                {
                    var exposed = weather.GetWeatherExposure(
                        (context.GridUid, context.Grid, context.Roof),
                        new ZLevelTileIndices(x, y, context.LocalZ)).IsExposed;
                    _maskTileChecks++;

                    if (!exposed)
                    {
                        blockedTiles++;
                        if (runStart == int.MinValue)
                            runStart = x;
                        continue;
                    }

                    if (runStart != int.MinValue)
                    {
                        AddRun(context, runStart, x, y);
                        runStart = int.MinValue;
                    }
                }

                if (runStart != int.MinValue)
                    AddRun(context, runStart, context.MaximumTile.X, y);
            }

            var runCount = _runs.Count - firstRun;
            if (runCount > 0)
            {
                _batches.Add(new ZLevelWeatherMaskBatch(
                    context.GridUid,
                    context.LocalZ,
                    firstRun,
                    runCount));
            }

            _maskBlockedTiles += blockedTiles;
        }

        if (_runs.Count > budget.RemainingMaskRuns)
        {
            FailMaskPlan(budget, tileBudget: false);
            RecordMaskBuild(started);
            return 0;
        }

        budget.RemainingMaskRuns -= _runs.Count;
        budget.CurrentMaskRuns += _runs.Count;
        _maskRuns += _runs.Count;
        RecordMaskBuild(started);
        return _batches.Count;
    }

    /// <summary>
    /// Finds the nearest exposed tile on the listener's exact local floor.
    /// Search order is stable by squared distance and then coordinates.
    /// </summary>
    public ZLevelWeatherAudioExposure FindAudioExposure(
        SharedWeatherSystem weather,
        EntityUid listener)
    {
        _audioQueries++;
        if (!TryComp(listener, out TransformComponent? transform) ||
            transform.MapID == MapId.Nullspace)
        {
            _audioInvalidQueries++;
            return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.Invalid);
        }

        if (transform.GridUid is not { } gridUid)
        {
            var mapSpace = weather.GetWeatherExposure(listener);
            if (mapSpace.IsExposed)
            {
                _audioDirectExposures++;
                return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.Direct);
            }

            _audioInvalidQueries++;
            return new ZLevelWeatherAudioExposure(
                ZLevelWeatherAudioTermination.Invalid,
                ExposureTermination: mapSpace.Termination);
        }

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _audioInvalidQueries++;
            return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.Invalid);
        }

        var budget = EnsureFrameBudget();
        var seed = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
        var localZ = _zLevels.GetZLevel(listener);
        var roof = CompOrNull<RoofComponent>(gridUid);

        for (var i = 0; i < AudioOffsets.Length; i++)
        {
            if (budget.RemainingAudioTileChecks <= 0)
            {
                RecordExhaustion(
                    ref budget.AudioTileExhaustedThisFrame,
                    ref _audioBudgetExhaustions);
                _audioBlockedQueries++;
                return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.BudgetExceeded);
            }

            budget.RemainingAudioTileChecks--;
            budget.CurrentAudioTileChecks++;
            _audioTileChecks++;
            var tile = seed + AudioOffsets[i];
            var exposure = weather.GetWeatherExposure(
                (gridUid, grid, roof),
                new ZLevelTileIndices(tile.X, tile.Y, localZ));
            if (exposure.IsExposed)
            {
                if (i == 0)
                {
                    _audioDirectExposures++;
                    return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.Direct);
                }

                _audioNearbyExposures++;
                return new ZLevelWeatherAudioExposure(
                    ZLevelWeatherAudioTermination.Nearby,
                    _map.GridTileToLocal(gridUid, grid, tile));
            }

            if (i == 0 && exposure.Termination is
                    WeatherExposureTermination.InvalidCoordinates or
                    WeatherExposureTermination.InvalidGrid or
                    WeatherExposureTermination.InvalidLevel)
            {
                _audioInvalidQueries++;
                return new ZLevelWeatherAudioExposure(
                    ZLevelWeatherAudioTermination.Invalid,
                    ExposureTermination: exposure.Termination);
            }
        }

        _audioBlockedQueries++;
        return new ZLevelWeatherAudioExposure(ZLevelWeatherAudioTermination.Blocked);
    }

    public void ResetMetrics()
    {
        _maskPlans = 0;
        _maskGridCandidates = 0;
        _maskGridLayers = 0;
        _maskTileChecks = 0;
        _maskBlockedTiles = 0;
        _maskRuns = 0;
        _maskFailClosedPlans = 0;
        _maskTileBudgetExhaustions = 0;
        _maskRunBudgetExhaustions = 0;
        _maskBuildTimestampTicks = 0;
        _maskLastBuildTimestampTicks = 0;
        _maskMaxBuildTimestampTicks = 0;
        _maskRenderFrames = 0;
        _maskRenderBatches = 0;
        _maskRenderRuns = 0;
        _maskRenderDrawCalls = 0;
        _maskRenderFailClosedFrames = 0;
        _maskRenderTimestampTicks = 0;
        _maskRenderLastTimestampTicks = 0;
        _maskRenderMaxTimestampTicks = 0;
        _audioQueries = 0;
        _audioTileChecks = 0;
        _audioDirectExposures = 0;
        _audioNearbyExposures = 0;
        _audioBlockedQueries = 0;
        _audioInvalidQueries = 0;
        _audioBudgetExhaustions = 0;
        _budget.ResetMetrics();
    }

    public ZLevelWeatherPresentationMetrics Snapshot()
    {
        return new ZLevelWeatherPresentationMetrics(
            _maskPlans,
            _maskGridCandidates,
            _maskGridLayers,
            _maskTileChecks,
            _maskBlockedTiles,
            _maskRuns,
            _maskFailClosedPlans,
            _maskTileBudgetExhaustions,
            _maskRunBudgetExhaustions,
            _maskBuildTimestampTicks,
            _maskLastBuildTimestampTicks,
            _maskMaxBuildTimestampTicks,
            _batches.Count,
            _runs.Count,
            _maskEntireViewport,
            _maskRenderFrames,
            _maskRenderBatches,
            _maskRenderRuns,
            _maskRenderDrawCalls,
            _maskRenderFailClosedFrames,
            _maskRenderTimestampTicks,
            _maskRenderLastTimestampTicks,
            _maskRenderMaxTimestampTicks,
            _audioQueries,
            _audioTileChecks,
            _audioDirectExposures,
            _audioNearbyExposures,
            _audioBlockedQueries,
            _audioInvalidQueries,
            _audioBudgetExhaustions,
            _budget.CurrentMaskTileChecks,
            _budget.CurrentMaskRuns,
            _budget.CurrentAudioTileChecks,
            _maxMaskTileChecksPerFrame,
            _maxMaskRunsPerFrame,
            _maxAudioTileChecksPerFrame);
    }

    internal void RecordMaskRender(
        long started,
        int batches,
        int runs,
        int drawCalls,
        bool failClosed)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _maskRenderFrames++;
        _maskRenderBatches += batches;
        _maskRenderRuns += runs;
        _maskRenderDrawCalls += drawCalls;
        if (failClosed)
            _maskRenderFailClosedFrames++;
        _maskRenderTimestampTicks += elapsed;
        _maskRenderLastTimestampTicks = elapsed;
        _maskRenderMaxTimestampTicks = Math.Max(_maskRenderMaxTimestampTicks, elapsed);
    }

    internal void BeginBudgetFrameForTesting()
    {
        _budget.Initialized = false;
        EnsureFrameBudget();
    }

    private void BuildContexts(Box2 worldBounds, int viewerWorldZ)
    {
        foreach (var grid in _grids)
        {
            if (grid.Comp.Deleted || grid.Comp.TileSize <= 0f)
                continue;

            var localZ = _transform.WorldToLocalZLevel(grid.Owner, viewerWorldZ);
            if (_zLevelMaps.TryGetConfig(grid.Owner, out var config))
            {
                if (localZ < config.Comp.MinimumLevel || localZ > config.Comp.MaximumLevel)
                    continue;
            }
            else if (localZ != 0)
            {
                continue;
            }

            var (_, _, _, inverseWorldMatrix) =
                _transform.GetWorldPositionRotationMatrixWithInv(grid.Owner);
            var localView = inverseWorldMatrix.TransformBox(worldBounds);
            var localGrid = grid.Comp.LocalAABB;
            var left = MathF.Max(localView.Left, localGrid.Left);
            var bottom = MathF.Max(localView.Bottom, localGrid.Bottom);
            var right = MathF.Min(localView.Right, localGrid.Right);
            var top = MathF.Min(localView.Top, localGrid.Top);
            if (left >= right || bottom >= top)
                continue;

            var tileSize = grid.Comp.TileSize;
            var minimum = new Vector2i(
                (int) MathF.Floor(left / tileSize),
                (int) MathF.Floor(bottom / tileSize));
            var maximum = new Vector2i(
                (int) MathF.Ceiling(right / tileSize),
                (int) MathF.Ceiling(top / tileSize));
            if (minimum.X >= maximum.X || minimum.Y >= maximum.Y)
                continue;

            _contexts.Add(new WeatherMaskGridContext(
                grid.Owner,
                grid.Comp,
                CompOrNull<RoofComponent>(grid.Owner),
                localZ,
                minimum,
                maximum));
        }
    }

    private void AddRun(in WeatherMaskGridContext context, int startX, int endX, int y)
    {
        var tileSize = context.Grid.TileSize;
        _runs.Add(new ZLevelWeatherMaskRun(new Box2(
            startX * tileSize,
            y * tileSize,
            endX * tileSize,
            (y + 1) * tileSize)));
    }

    private void FailMaskPlan(PresentationFrameBudget budget, bool tileBudget)
    {
        _batches.Clear();
        _runs.Clear();
        _maskEntireViewport = true;
        _maskFailClosedPlans++;
        if (tileBudget)
        {
            RecordExhaustion(
                ref budget.MaskTileExhaustedThisFrame,
                ref _maskTileBudgetExhaustions);
        }
        else
        {
            RecordExhaustion(
                ref budget.MaskRunExhaustedThisFrame,
                ref _maskRunBudgetExhaustions);
        }
    }

    private PresentationFrameBudget EnsureFrameBudget()
    {
        var frame = _timing.CurFrame;
        if (_budget.Initialized && _budget.Frame == frame)
            return _budget;

        _budget.Initialized = true;
        _budget.Frame = frame;
        _budget.RemainingMaskTileChecks = _maxMaskTileChecksPerFrame;
        _budget.RemainingMaskRuns = _maxMaskRunsPerFrame;
        _budget.RemainingAudioTileChecks = _maxAudioTileChecksPerFrame;
        _budget.CurrentMaskTileChecks = 0;
        _budget.CurrentMaskRuns = 0;
        _budget.CurrentAudioTileChecks = 0;
        _budget.MaskTileExhaustedThisFrame = false;
        _budget.MaskRunExhaustedThisFrame = false;
        _budget.AudioTileExhaustedThisFrame = false;
        return _budget;
    }

    private void RecordMaskBuild(long started)
    {
        var elapsed = Stopwatch.GetTimestamp() - started;
        _maskPlans++;
        _maskBuildTimestampTicks += elapsed;
        _maskLastBuildTimestampTicks = elapsed;
        _maskMaxBuildTimestampTicks = Math.Max(_maskMaxBuildTimestampTicks, elapsed);
        _grids.Clear();
        _contexts.Clear();
    }

    private void SetLimit(ref int field, int configured, int maximum)
    {
        field = Math.Clamp(configured, 0, maximum);
        _budget.Initialized = false;
    }

    private static void RecordExhaustion(ref bool frameFlag, ref long counter)
    {
        if (frameFlag)
            return;

        frameFlag = true;
        counter++;
    }

    private static Vector2i[] BuildAudioOffsets()
    {
        var offsets = new List<Vector2i>();
        for (var x = -AudioSearchRadius; x <= AudioSearchRadius; x++)
        {
            for (var y = -AudioSearchRadius; y <= AudioSearchRadius; y++)
            {
                if (x * x + y * y <= AudioSearchRadius * AudioSearchRadius)
                    offsets.Add(new Vector2i(x, y));
            }
        }

        offsets.Sort(static (left, right) =>
        {
            var leftDistance = left.X * left.X + left.Y * left.Y;
            var rightDistance = right.X * right.X + right.Y * right.Y;
            var distance = leftDistance.CompareTo(rightDistance);
            if (distance != 0)
                return distance;

            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.X.CompareTo(right.X);
        });
        return offsets.ToArray();
    }

    private sealed class PresentationFrameBudget
    {
        public bool Initialized;
        public uint Frame;
        public int RemainingMaskTileChecks;
        public int RemainingMaskRuns;
        public int RemainingAudioTileChecks;
        public int CurrentMaskTileChecks;
        public int CurrentMaskRuns;
        public int CurrentAudioTileChecks;
        public bool MaskTileExhaustedThisFrame;
        public bool MaskRunExhaustedThisFrame;
        public bool AudioTileExhaustedThisFrame;

        public void ResetMetrics()
        {
            Initialized = false;
            RemainingMaskTileChecks = 0;
            RemainingMaskRuns = 0;
            RemainingAudioTileChecks = 0;
            CurrentMaskTileChecks = 0;
            CurrentMaskRuns = 0;
            CurrentAudioTileChecks = 0;
            MaskTileExhaustedThisFrame = false;
            MaskRunExhaustedThisFrame = false;
            AudioTileExhaustedThisFrame = false;
        }
    }

    private readonly record struct WeatherMaskGridContext(
        EntityUid GridUid,
        MapGridComponent Grid,
        RoofComponent? Roof,
        int LocalZ,
        Vector2i MinimumTile,
        Vector2i MaximumTile)
    {
        public int Width => MaximumTile.X - MinimumTile.X;
        public int Height => MaximumTile.Y - MinimumTile.Y;
    }
}

public readonly record struct ZLevelWeatherMaskBatch(
    EntityUid GridUid,
    int LocalZ,
    int FirstRun,
    int RunCount);

public readonly record struct ZLevelWeatherMaskRun(Box2 LocalBounds);

public enum ZLevelWeatherAudioTermination : byte
{
    Direct,
    Nearby,
    Blocked,
    Invalid,
    BudgetExceeded,
}

public readonly record struct ZLevelWeatherAudioExposure(
    ZLevelWeatherAudioTermination Termination,
    EntityCoordinates? NearestExposedTile = null,
    WeatherExposureTermination? ExposureTermination = null)
{
    public bool IsExposed => Termination is
        ZLevelWeatherAudioTermination.Direct or ZLevelWeatherAudioTermination.Nearby;
}

public readonly record struct ZLevelWeatherPresentationMetrics(
    long MaskPlans,
    long MaskGridCandidates,
    long MaskGridLayers,
    long MaskTileChecks,
    long MaskBlockedTiles,
    long MaskRuns,
    long MaskFailClosedPlans,
    long MaskTileBudgetExhaustions,
    long MaskRunBudgetExhaustions,
    long MaskBuildTimestampTicks,
    long MaskLastBuildTimestampTicks,
    long MaskMaxBuildTimestampTicks,
    int CurrentMaskBatches,
    int CurrentMaskRuns,
    bool MaskEntireViewport,
    long MaskRenderFrames,
    long MaskRenderBatches,
    long MaskRenderRuns,
    long MaskRenderDrawCalls,
    long MaskRenderFailClosedFrames,
    long MaskRenderTimestampTicks,
    long MaskRenderLastTimestampTicks,
    long MaskRenderMaxTimestampTicks,
    long AudioQueries,
    long AudioTileChecks,
    long AudioDirectExposures,
    long AudioNearbyExposures,
    long AudioBlockedQueries,
    long AudioInvalidQueries,
    long AudioBudgetExhaustions,
    int CurrentMaskTileChecks,
    int CurrentMaskRunsUsed,
    int CurrentAudioTileChecks,
    int MaxMaskTileChecksPerFrame,
    int MaxMaskRunsPerFrame,
    int MaxAudioTileChecksPerFrame)
{
    public double MaskAverageBuildMilliseconds => MaskPlans == 0
        ? 0d
        : ToMilliseconds(MaskBuildTimestampTicks) / MaskPlans;
    public double MaskLastBuildMilliseconds => ToMilliseconds(MaskLastBuildTimestampTicks);
    public double MaskMaxBuildMilliseconds => ToMilliseconds(MaskMaxBuildTimestampTicks);
    public double MaskAverageRenderMilliseconds => MaskRenderFrames == 0
        ? 0d
        : ToMilliseconds(MaskRenderTimestampTicks) / MaskRenderFrames;
    public double MaskLastRenderMilliseconds => ToMilliseconds(MaskRenderLastTimestampTicks);
    public double MaskMaxRenderMilliseconds => ToMilliseconds(MaskRenderMaxTimestampTicks);

    private static double ToMilliseconds(long ticks)
    {
        return ticks * 1000d / Stopwatch.Frequency;
    }
}
