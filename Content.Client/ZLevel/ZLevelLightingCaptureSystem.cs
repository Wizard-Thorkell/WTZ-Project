// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using Content.Client.Weather;
using Content.Client.ZLevel.Commands;
using Content.Client.Viewport;
using Content.Shared.Maps;
using Content.Shared.StatusEffectNew.Components;
using Content.Shared.Weather;
using Content.Shared.ZLevel.Systems;
using Robust.Client;
using Robust.Client.Console;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.ContentPack;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client.ZLevel;

/// <summary>
/// Drives deterministic screenshots through the normal windowed Clyde renderer.
/// </summary>
public sealed class ZLevelLightingCaptureSystem : EntitySystem
{
    private static readonly ResPath OutputPath = new("/ZLevelVisualCapture");
    private static readonly Vector2 FixtureCenter = new(3.5f, 3.5f);
    private static readonly Box2 FixtureBounds = new(0f, 0f, 7f, 7f);
    private static readonly Vector2 ShadowProbe = new(1.5f, 4.5f);
    private static readonly Vector2 ClearProbe = new(5.5f, 4.5f);
    private static readonly Vector2 ActiveFloorProbe = new(3.5f, 2.5f);
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan ObserveRetryInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan ScreenshotTimeout = TimeSpan.FromSeconds(10);
    private const double MinimumShadowModeDifference = 0.0004d;

    private static readonly CaptureSpec[] CaptureSpecs =
    {
        new("baseline-z0", 0, false, false, false),
        new("baseline-z1", 1, false, false, false),
        new("baseline-z2", 2, false, false, false),
        new("hard-z0", 0, true, false, false),
        new("hard-z1", 1, true, false, false),
        new("hard-z2", 2, true, false, false),
        new("soft-z0", 0, true, true, false),
        new("soft-z1", 1, true, true, false),
        new("soft-z2", 2, true, true, false),
        new("hard-preview-z1", 1, true, false, true),
        new("soft-preview-z1", 1, true, true, true),
        new("weather-clear-z2", 2, false, false, false, WeatherCapture: true),
        new("weather-clear-z3", 3, false, false, false, WeatherCapture: true),
        new("weather-rain-z2", 2, false, false, false, WeatherCapture: true, WeatherEnabled: true),
        new("weather-rain-z3", 3, false, false, false, WeatherCapture: true, WeatherEnabled: true),
    };

    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IClientConsoleHost _console = default!;
    [Dependency] private readonly EyeSystem _eyeSystem = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IGameController _gameController = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly IClientNetManager _network = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceManager _resources = default!;
    [Dependency] private readonly IStateManager _state = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevels = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly ZLevelLightingCacheSystem _lightingCache = default!;
    [Dependency] private readonly ZLevelLightingProjectionSystem _lightingProjection = default!;
    [Dependency] private readonly ZLevelOverlaySystem _zLevelOverlay = default!;
    [Dependency] private readonly ZLevelTileProjectionSystem _tileProjection = default!;
    [Dependency] private readonly ZLevelWeatherPresentationSystem _weatherPresentation = default!;

    private readonly List<CaptureMeasurement> _measurements = new();
    private readonly List<CaptureCheck> _checks = new();
    private readonly Dictionary<string, byte[]> _signatures = new(StringComparer.Ordinal);
    private readonly Dictionary<int, FixtureLayerInventory> _baselineInventories = new();
    private ISawmill _log = default!;
    private CapturePhase _phase;
    private long _started;
    private long _lastObserveAttempt;
    private long _screenshotQueued;
    private long _serverViewRequested;
    private int _captureIndex;
    private int _stabilizationFrames;
    private int _shutdownFrames;
    private int _requestedLocalZ;
    private int _originalPlayerLocalZ;
    private MapId _fixtureMapId;
    private bool _autoShutdown;
    private bool _restored;
    private bool _fixturePrepared;
    private bool _playerViewMoved;
    private bool _weatherActive;
    private bool _originalWeatherTilePolicy;
    private long _weatherObserved;
    private ContentTileDefinition? _weatherTileDefinition;
    private EntityUid? _captureEntity;
    private EntityUid _fixtureGrid;
    private EyeComponent? _captureEye;
    private IEye? _originalEye;
    private bool _originalDrawShadows;
    private bool _originalSoftShadows;
    private bool _originalMappingPreview;
    private EntityUid? _hiddenPlayerSprite;
    private bool _originalPlayerSpriteVisible;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(EyeSystem));
        _log = Logger.GetSawmill("zlevel.capture");
        StartPendingCapture();
    }

    public bool Start(bool autoShutdown)
    {
        if (_phase != CapturePhase.Idle)
            return false;

        _autoShutdown = autoShutdown;
        _started = Stopwatch.GetTimestamp();
        _lastObserveAttempt = 0;
        _captureIndex = 0;
        _restored = false;
        _fixturePrepared = false;
        _weatherActive = false;
        _weatherObserved = 0;
        _weatherTileDefinition = null;
        _measurements.Clear();
        _checks.Clear();
        _signatures.Clear();
        _baselineInventories.Clear();

        if (_resources.UserData.Exists(OutputPath))
            _resources.UserData.Delete(OutputPath);
        _resources.UserData.CreateDir(OutputPath);

        _phase = CapturePhase.WaitingForConnection;
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        StartPendingCapture();

        if (_phase == CapturePhase.Idle)
            return;

        try
        {
            if (_phase != CapturePhase.ShutdownDelay && Elapsed(_started) > CaptureTimeout)
            {
                Fail($"Capture timed out in phase {_phase}.");
                return;
            }

            switch (_phase)
            {
                case CapturePhase.WaitingForConnection:
                    WaitForConnection();
                    break;
                case CapturePhase.WaitingForObserver:
                    WaitForObserver();
                    break;
                case CapturePhase.WaitingForFixture:
                    WaitForFixture();
                    break;
                case CapturePhase.WaitingForServerView:
                    WaitForServerView();
                    break;
                case CapturePhase.Stabilizing:
                    Stabilize();
                    break;
                case CapturePhase.WaitingForScreenshot:
                    if (Elapsed(_screenshotQueued) > ScreenshotTimeout)
                        Fail($"Screenshot {CaptureSpecs[_captureIndex].Name} timed out.");
                    break;
                case CapturePhase.ShutdownDelay:
                    if (--_shutdownFrames <= 0)
                    {
                        _phase = CapturePhase.Idle;
                        _gameController.Shutdown("Z-level lighting capture finished.");
                    }
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception.ToString());
        }
    }

    private void StartPendingCapture()
    {
        if (!ZLevelLightingCaptureCommand.TryTakeStartRequest(out var autoShutdown))
            return;

        if (!Start(autoShutdown))
            _log.Warning("Ignored a Z-level lighting capture request because a capture is already active.");
    }

    private void WaitForConnection()
    {
        if (!_network.IsConnected || _player.LocalSession is not { Status: SessionStatus.InGame })
            return;

        _phase = CapturePhase.WaitingForObserver;
    }

    private void WaitForObserver()
    {
        if (_player.LocalEntity is { } player &&
            TryComp(player, out TransformComponent? transform) &&
            transform.MapID != MapId.Nullspace &&
            HasComp<EyeComponent>(player))
        {
            _phase = CapturePhase.WaitingForFixture;
            return;
        }

        var now = Stopwatch.GetTimestamp();
        if (_lastObserveAttempt != 0 && Elapsed(_lastObserveAttempt) < ObserveRetryInterval)
            return;

        _lastObserveAttempt = now;
        _console.RemoteExecuteCommand(null, "observe");
    }

    private void WaitForFixture()
    {
        if (_state.CurrentState is not IMainViewportState ||
            !TryFindFixture(out var grid))
        {
            return;
        }

        PrepareFixture(grid);
        ApplyCaptureSpec();
    }

    private bool TryFindFixture(out EntityUid fixtureGrid)
    {
        fixtureGrid = EntityUid.Invalid;
        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? transform) ||
            transform.GridUid is not { } grid ||
            !HasComp<MapGridComponent>(grid))
        {
            return false;
        }

        fixtureGrid = grid;
        return true;
    }

    private void PrepareFixture(EntityUid grid)
    {
        _fixtureGrid = grid;
        _fixtureMapId = Transform(grid).MapID;
        LogFixtureInventory(grid);
        _originalPlayerLocalZ = _player.LocalEntity is { } player
            ? _zLevels.GetZLevel(player)
            : 0;
        _originalEye = _eyeManager.CurrentEye;
        _originalDrawShadows = _lightManager.DrawShadows;
        _originalSoftShadows = _configuration.GetCVar(CVars.LightSoftShadows);
        _originalMappingPreview = _zLevelOverlay.MappingPreviewEnabled;
        _fixturePrepared = true;

        if (_player.LocalEntity is { } playerSpriteEntity &&
            TryComp(playerSpriteEntity, out SpriteComponent? sprite))
        {
            _hiddenPlayerSprite = playerSpriteEntity;
            _originalPlayerSpriteVisible = sprite.Visible;
            _sprites.SetVisible((playerSpriteEntity, sprite), false);
        }

        _captureEntity = Spawn(null, new EntityCoordinates(grid, FixtureCenter));
        _captureEye = EnsureComp<EyeComponent>(_captureEntity.Value);
        _eyeSystem.SetDrawFov(_captureEntity.Value, false, _captureEye);
        _eyeSystem.SetDrawLight((_captureEntity.Value, _captureEye), true);
        _eyeSystem.SetZoom(_captureEntity.Value, Vector2.One, _captureEye);
        _eyeSystem.SetRotation(_captureEntity.Value, Angle.Zero, _captureEye);
        _eyeSystem.SetOffset(_captureEntity.Value, Vector2.Zero, _captureEye);
        _eyeManager.CurrentEye = _captureEye!.Eye;

        _lightingCache.ResetMetrics();
        _lightingProjection.ResetMetrics();
        _tileProjection.ResetMetrics();
        _weatherPresentation.ResetMetrics();
    }

    private void LogFixtureInventory(EntityUid gridUid)
    {
        for (var z = 0; z < 3; z++)
        {
            var inventory = GetFixtureLayerInventory(gridUid, z);
            _log.Info(
                "Fixture Z{0}: tiles={1}, lights={2} ({3} enabled), occluders={4}.",
                z,
                inventory.Tiles,
                inventory.Lights,
                inventory.EnabledLights,
                inventory.Occluders);
        }
    }

    private FixtureLayerInventory GetFixtureLayerInventory(EntityUid gridUid, int localZ)
    {
        var grid = Comp<MapGridComponent>(gridUid);
        var tiles = 0;
        foreach (var tile in _map.GetAllNonEmptyZLevelTiles(gridUid, grid))
        {
            if (tile.GridIndices.Z == localZ)
                tiles++;
        }

        var lights = 0;
        var enabledLights = 0;
        var lightQuery = AllEntityQuery<PointLightComponent, TransformComponent>();
        while (lightQuery.MoveNext(out var uid, out var light, out var transform))
        {
            if (transform.GridUid != gridUid)
                continue;

            var z = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
            if (z != localZ)
                continue;

            lights++;
            if (light.Enabled)
                enabledLights++;
        }

        var occluders = 0;
        var occluderQuery = AllEntityQuery<OccluderComponent, TransformComponent>();
        while (occluderQuery.MoveNext(out var uid, out var occluder, out var transform))
        {
            if (!occluder.Enabled || transform.GridUid != gridUid)
                continue;

            var z = _transform.GetZLevel((uid, transform, CompOrNull<ZLevelPositionComponent>(uid)));
            if (z == localZ)
                occluders++;
        }

        return new FixtureLayerInventory(tiles, lights, enabledLights, occluders);
    }

    private void ApplyCaptureSpec()
    {
        var spec = CaptureSpecs[_captureIndex];
        var entity = _captureEntity!.Value;
        var localZ = spec.LocalZ;
        _eyeSystem.SetDrawLight((entity, _captureEye!), !spec.WeatherCapture);
        if (!_zLevels.SetZLevelPosition(entity, localZ))
            throw new InvalidOperationException("Unable to place the capture eye on the fixture floor.");

        var worldZ = _transform.LocalToWorldZLevel(_fixtureGrid, localZ);
        _eyeManager.CurrentEye = _captureEye!.Eye;
        _lightManager.DrawShadows = spec.DrawShadows;
        _configuration.SetCVar(CVars.LightSoftShadows, spec.SoftShadows);
        _zLevelOverlay.SetMappingPreview(spec.MappingPreview);

        if (spec.WeatherCapture && _weatherTileDefinition == null)
        {
            var grid = Comp<MapGridComponent>(_fixtureGrid);
            var tile = _map.GetZLevelTileRef(
                _fixtureGrid,
                grid,
                new ZLevelTileIndices(3, 3, 3)).Tile;
            if (tile.IsEmpty)
                throw new InvalidOperationException("Weather capture fixture has no top-floor tile.");

            _weatherTileDefinition = (ContentTileDefinition) _tileDefinitions[tile.TypeId];
            _originalWeatherTilePolicy = _weatherTileDefinition.Weather;
            _weatherTileDefinition.Weather = true;
        }

        if (spec.WeatherEnabled && !_weatherActive)
        {
            _weatherActive = true;
            _weatherObserved = 0;
            _console.RemoteExecuteCommand(null, $"weatherset {_fixtureMapId} WeatherRain");
        }

        _requestedLocalZ = localZ;
        _serverViewRequested = Stopwatch.GetTimestamp();
        _playerViewMoved = true;
        _console.RemoteExecuteCommand(null, $"zlevelset {localZ}");
        _phase = CapturePhase.WaitingForServerView;
        _log.Info(
            "Requesting {0}: local/world Z {1}/{2}, shadows={3}, soft={4}, preview={5}.",
            spec.Name,
            localZ,
            worldZ,
            spec.DrawShadows,
            spec.SoftShadows,
            spec.MappingPreview);
    }

    private void WaitForServerView()
    {
        if (_player.LocalEntity is not { } player ||
            !TryComp(player, out TransformComponent? playerTransform) ||
            _transform.GetZLevel((player, playerTransform, CompOrNull<ZLevelPositionComponent>(player))) !=
            _requestedLocalZ)
        {
            return;
        }

        var spec = CaptureSpecs[_captureIndex];
        var inventory = GetFixtureLayerInventory(_fixtureGrid, _requestedLocalZ);
        if (inventory.Tiles == 0 ||
            (!spec.WeatherCapture && (inventory.EnabledLights == 0 || inventory.Occluders == 0)) ||
            (spec.WeatherCapture && !IncludesBaselineInventory(_requestedLocalZ, inventory)))
            return;

        if (!spec.WeatherCapture)
            _baselineInventories.TryAdd(_requestedLocalZ, inventory);

        if (spec.WeatherEnabled)
        {
            if (!HasFixtureWeather())
                return;

            if (_weatherObserved == 0)
                _weatherObserved = Stopwatch.GetTimestamp();
        }

        _stabilizationFrames = spec.MappingPreview ? 45 : spec.WeatherCapture ? 45 : 24;
        _phase = CapturePhase.Stabilizing;
        _log.Info(
            "Preparing {0} after server/PVS convergence in {1:0.000}s: " +
            "tiles={2}, lights={3} ({4} enabled), occluders={5}.",
            spec.Name,
            Elapsed(_serverViewRequested).TotalSeconds,
            inventory.Tiles,
            inventory.Lights,
            inventory.EnabledLights,
            inventory.Occluders);
    }

    private void Stabilize()
    {
        _eyeManager.CurrentEye = _captureEye!.Eye;
        var spec = CaptureSpecs[_captureIndex];
        if (spec.WeatherCapture &&
            !IncludesBaselineInventory(
                _requestedLocalZ,
                GetFixtureLayerInventory(_fixtureGrid, _requestedLocalZ)))
        {
            _phase = CapturePhase.WaitingForServerView;
            return;
        }

        if (spec.WeatherEnabled &&
            (_weatherObserved == 0 || Elapsed(_weatherObserved) < SharedWeatherSystem.StartupTime))
        {
            return;
        }

        if (--_stabilizationFrames > 0)
            return;

        if (_state.CurrentState is not IMainViewportState state ||
            state.Viewport.Viewport.ViewportSize.X <= 0 ||
            state.Viewport.Viewport.ViewportSize.Y <= 0)
        {
            _stabilizationFrames = 1;
            return;
        }

        var capturedIndex = _captureIndex;
        _screenshotQueued = Stopwatch.GetTimestamp();
        _phase = CapturePhase.WaitingForScreenshot;
        state.Viewport.Viewport.Screenshot(image => ReceiveScreenshot(capturedIndex, state, image));
    }

    private bool IncludesBaselineInventory(int localZ, FixtureLayerInventory actual)
    {
        if (!_baselineInventories.TryGetValue(localZ, out var baseline))
            return true;

        return actual.Tiles >= baseline.Tiles &&
               actual.Lights >= baseline.Lights &&
               actual.EnabledLights >= baseline.EnabledLights &&
               actual.Occluders >= baseline.Occluders;
    }

    private void ReceiveScreenshot(
        int capturedIndex,
        IMainViewportState state,
        Image<Rgba32> image)
    {
        using (image)
        {
            if (_phase != CapturePhase.WaitingForScreenshot || capturedIndex != _captureIndex)
                return;

            var spec = CaptureSpecs[capturedIndex];
            var fileName = $"{spec.Name}.png";
            using (var stream = _resources.UserData.Open(
                       OutputPath / fileName,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None))
            {
                image.SaveAsPng(stream);
            }

            var eyePosition = _captureEye!.Eye.Position.Position;
            var viewportSize = state.Viewport.Viewport.ViewportSize;
            var shadowWorld = _transform.ToMapCoordinates(
                new EntityCoordinates(_fixtureGrid, ShadowProbe)).Position;
            var clearWorld = _transform.ToMapCoordinates(
                new EntityCoordinates(_fixtureGrid, ClearProbe)).Position;
            var activeWorld = _transform.ToMapCoordinates(
                new EntityCoordinates(_fixtureGrid, ActiveFloorProbe)).Position;
            var signature = ZLevelLightingCaptureAnalysis.BuildGridRegionSignature(
                image,
                FixtureBounds,
                _transform.GetWorldMatrix(_fixtureGrid),
                eyePosition,
                viewportSize);

            _signatures.Add(spec.Name, signature);
            _measurements.Add(new CaptureMeasurement(
                spec.Name,
                fileName,
                spec.LocalZ,
                _captureEye.Eye.WorldZLevel,
                spec.DrawShadows ? spec.SoftShadows ? "soft" : "hard" : "baseline",
                spec.MappingPreview,
                spec.WeatherCapture ? spec.WeatherEnabled ? "rain" : "clear" : "none",
                image.Width,
                image.Height,
                ZLevelLightingCaptureAnalysis.SignatureLuminance(signature),
                ZLevelLightingCaptureAnalysis.SampleWorldRegion(
                    image, shadowWorld, eyePosition, viewportSize),
                ZLevelLightingCaptureAnalysis.SampleWorldRegion(
                    image, clearWorld, eyePosition, viewportSize),
                ZLevelLightingCaptureAnalysis.SampleWorldRegion(
                    image, activeWorld, eyePosition, viewportSize)));
        }

        _captureIndex++;
        if (_captureIndex < CaptureSpecs.Length)
        {
            ApplyCaptureSpec();
            return;
        }

        CompleteCapture();
    }

    private void CompleteCapture()
    {
        EvaluateChecks();
        var success = _checks.All(check => check.Passed);
        WriteReport(success, null);
        RestoreState();

        _log.Info(
            "Z-level lighting capture {0}: {1}/{2} checks passed. Output: {3}",
            success ? "passed" : "failed",
            _checks.Count(check => check.Passed),
            _checks.Count,
            OutputPath);

        if (_autoShutdown)
        {
            _shutdownFrames = 3;
            _phase = CapturePhase.ShutdownDelay;
        }
        else
        {
            _phase = CapturePhase.Idle;
        }
    }

    private void EvaluateChecks()
    {
        _checks.Clear();
        AddCheck(
            "capture-count",
            _measurements.Count == CaptureSpecs.Length,
            $"captured {_measurements.Count}/{CaptureSpecs.Length} frames");
        AddCheck(
            "nonblank-output",
            _measurements.All(frame => frame.Width > 0 && frame.Height > 0) &&
            _measurements.Where(frame => frame.Weather == "none").All(frame => frame.MeanLuminance > 1d),
            $"minimum lighting-frame mean luminance " +
            $"{_measurements.Where(frame => frame.Weather == "none").Min(frame => frame.MeanLuminance):0.000}");

        CheckDominantChannel("baseline-z0", 'R');
        CheckDominantChannel("baseline-z1", 'G');
        CheckDominantChannel("baseline-z2", 'B');

        for (var z = 0; z <= 2; z++)
        {
            CheckShadowContrast(z, soft: false);
            CheckShadowContrast(z, soft: true);
            var modeDifference = ZLevelLightingCaptureAnalysis.SignatureDifference(
                _signatures[$"hard-z{z}"],
                _signatures[$"soft-z{z}"]);
            AddCheck(
                $"hard-soft-difference-z{z}",
                modeDifference > MinimumShadowModeDifference,
                $"normalized RMS difference {modeDifference:0.000000}, " +
                $"minimum {MinimumShadowModeDifference:0.000000}");
        }

        CheckPreviewDifference("hard-z1", "hard-preview-z1");
        CheckPreviewDifference("soft-z1", "soft-preview-z1");
        CheckWeatherPresentation();

        var lighting = _lightingProjection.Snapshot();
        AddCheck(
            "projected-shadow-atlas-used",
            lighting.ShadowAtlasRenders > 0 &&
            lighting.RenderShadowLights > 0 &&
            lighting.RenderShadowFloorGroups > 0,
            $"atlases/lights/groups={lighting.ShadowAtlasRenders}/" +
            $"{lighting.RenderShadowLights}/{lighting.RenderShadowFloorGroups}");
        AddCheck(
            "fixture-within-lighting-budgets",
            lighting.ShadowFallbacks == 0 &&
            lighting.ShadowLightBudgetExhaustions == 0 &&
            lighting.ShadowFloorGroupBudgetExhaustions == 0,
            $"fallback/light/group exhaustion={lighting.ShadowFallbacks}/" +
            $"{lighting.ShadowLightBudgetExhaustions}/{lighting.ShadowFloorGroupBudgetExhaustions}");

        var tiles = _tileProjection.Snapshot();
        AddCheck(
            "mapping-preview-rendered",
            tiles.MappingFrames > 0 && tiles.MappingRenderFrames > 0 && tiles.RenderTiles > 0,
            $"mapping build/render frames={tiles.MappingFrames}/{tiles.MappingRenderFrames}, " +
            $"rendered tiles={tiles.RenderTiles}");

        var weather = _weatherPresentation.Snapshot();
        AddCheck(
            "weather-mask-rendered",
            weather.MaskRenderFrames > 0 && weather.MaskTileChecks > 0 && weather.MaskRenderRuns > 0,
            $"render frames/tile checks/runs={weather.MaskRenderFrames}/" +
            $"{weather.MaskTileChecks}/{weather.MaskRenderRuns}");
        AddCheck(
            "weather-within-presentation-budgets",
            weather.MaskFailClosedPlans == 0 &&
            weather.MaskTileBudgetExhaustions == 0 &&
            weather.MaskRunBudgetExhaustions == 0,
            $"fail-closed/tile/run exhaustion={weather.MaskFailClosedPlans}/" +
            $"{weather.MaskTileBudgetExhaustions}/{weather.MaskRunBudgetExhaustions}");
    }

    private void CheckWeatherPresentation()
    {
        var blockedDifference = ZLevelLightingCaptureAnalysis.SignatureDifference(
            _signatures["weather-clear-z2"],
            _signatures["weather-rain-z2"]);
        var exposedDifference = ZLevelLightingCaptureAnalysis.SignatureDifference(
            _signatures["weather-clear-z3"],
            _signatures["weather-rain-z3"]);

        AddCheck(
            "weather-blocked-under-upper-floor",
            blockedDifference < 0.003d,
            $"normalized RMS difference {blockedDifference:0.000000}, maximum 0.003000");
        AddCheck(
            "weather-visible-on-top-floor",
            exposedDifference > 0.006d,
            $"normalized RMS difference {exposedDifference:0.000000}, minimum 0.006000");
        AddCheck(
            "weather-active-floor-contrast",
            exposedDifference > blockedDifference + 0.005d,
            $"exposed/blocked difference={exposedDifference:0.000000}/{blockedDifference:0.000000}, " +
            "required gap 0.005000");
    }

    private bool HasFixtureWeather()
    {
        var mapUid = Transform(_fixtureGrid).MapUid;
        var query = EntityQueryEnumerator<WeatherStatusEffectComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out _, out var status))
        {
            if (status.AppliedTo == mapUid)
                return true;
        }

        return false;
    }

    private void CheckDominantChannel(string name, char channel)
    {
        var sample = Measurement(name).ActiveFloor;
        var margin = sample.DominanceMargin(channel);
        AddCheck(
            $"active-floor-{char.ToLowerInvariant(channel)}-{name[^2..]}",
            margin > 5d,
            $"RGB={sample.Red:0.0}/{sample.Green:0.0}/{sample.Blue:0.0}, margin={margin:0.000}");
    }

    private void CheckShadowContrast(int z, bool soft)
    {
        var baseline = Measurement($"baseline-z{z}");
        var mode = soft ? "soft" : "hard";
        var shadowed = Measurement($"{mode}-z{z}");
        var shadowRetention = shadowed.ShadowProbe.Luminance /
                              Math.Max(1d, baseline.ShadowProbe.Luminance);
        var clearRetention = shadowed.ClearProbe.Luminance /
                             Math.Max(1d, baseline.ClearProbe.Luminance);
        var requiredGap = soft ? 0.025d : 0.06d;
        AddCheck(
            $"{mode}-shadow-contrast-z{z}",
            shadowRetention + requiredGap < clearRetention,
            $"shadow/clear retention={shadowRetention:0.000}/{clearRetention:0.000}, " +
            $"required gap={requiredGap:0.000}");
    }

    private void CheckPreviewDifference(string normal, string preview)
    {
        var difference = ZLevelLightingCaptureAnalysis.SignatureDifference(
            _signatures[normal],
            _signatures[preview]);
        AddCheck(
            $"mapping-preview-difference-{preview[..^3]}",
            difference > 0.003d,
            $"normalized RMS difference {difference:0.000000}");
    }

    private CaptureMeasurement Measurement(string name)
    {
        return _measurements.Single(measurement => measurement.Name == name);
    }

    private void AddCheck(string name, bool passed, string details)
    {
        _checks.Add(new CaptureCheck(name, passed, details));
    }

    private void Fail(string reason)
    {
        if (_phase is CapturePhase.Idle or CapturePhase.ShutdownDelay)
            return;

        _log.Error("Z-level lighting capture failed: {0}", reason);
        _checks.Clear();
        AddCheck("capture-runner", false, reason);

        try
        {
            WriteReport(false, reason);
        }
        catch (Exception reportException)
        {
            _log.Error("Failed to write the capture failure report: {0}", reportException);
        }

        RestoreState();
        if (_autoShutdown)
        {
            _shutdownFrames = 3;
            _phase = CapturePhase.ShutdownDelay;
        }
        else
        {
            _phase = CapturePhase.Idle;
        }
    }

    private void WriteReport(bool success, string? failure)
    {
        var lighting = _lightingProjection.Snapshot();
        var tiles = _tileProjection.Snapshot();
        var weather = _weatherPresentation.Snapshot();
        var metrics = new CaptureMetrics(
            lighting.ShadowAtlasRenders,
            lighting.RenderShadowLights,
            lighting.RenderShadowFloorGroups,
            lighting.ShadowFallbacks,
            lighting.ShadowLightBudgetExhaustions,
            lighting.ShadowFloorGroupBudgetExhaustions,
            tiles.MappingFrames,
            tiles.MappingRenderFrames,
            tiles.RenderTiles,
            weather.MaskPlans,
            weather.MaskTileChecks,
            weather.MaskRuns,
            weather.MaskFailClosedPlans,
            weather.MaskTileBudgetExhaustions,
            weather.MaskRunBudgetExhaustions,
            weather.MaskRenderFrames,
            weather.MaskRenderRuns,
            weather.MaskRenderDrawCalls);

        using var stream = _resources.UserData.Open(
            OutputPath / "report.json",
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read);
        using var writer = new StreamWriter(stream);
        writer.WriteLine("{");
        writer.Write("  \"success\": ");
        WriteJsonBoolean(writer, success);
        writer.WriteLine(",");
        writer.Write("  \"failure\": ");
        WriteJsonString(writer, failure);
        writer.WriteLine(",");
        writer.Write("  \"capturedAtUtc\": ");
        WriteJsonString(writer, DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteLine(",");
        writer.Write("  \"outputPath\": ");
        WriteJsonString(writer, OutputPath.ToString());
        writer.WriteLine(",");
        writer.Write("  \"durationSeconds\": ");
        WriteJsonNumber(writer, Elapsed(_started).TotalSeconds);
        writer.WriteLine(",");
        writer.WriteLine("  \"captures\": [");
        for (var index = 0; index < _measurements.Count; index++)
        {
            WriteMeasurement(writer, _measurements[index]);
            writer.WriteLine(index + 1 < _measurements.Count ? "," : string.Empty);
        }

        writer.WriteLine("  ],");
        writer.WriteLine("  \"checks\": [");
        for (var index = 0; index < _checks.Count; index++)
        {
            var check = _checks[index];
            writer.Write("    { \"name\": ");
            WriteJsonString(writer, check.Name);
            writer.Write(", \"passed\": ");
            WriteJsonBoolean(writer, check.Passed);
            writer.Write(", \"details\": ");
            WriteJsonString(writer, check.Details);
            writer.Write(" }");
            writer.WriteLine(index + 1 < _checks.Count ? "," : string.Empty);
        }

        writer.WriteLine("  ],");
        WriteMetrics(writer, metrics);
        writer.WriteLine();
        writer.WriteLine("}");
    }

    private static void WriteMeasurement(TextWriter writer, CaptureMeasurement measurement)
    {
        writer.WriteLine("    {");
        writer.Write("      \"name\": ");
        WriteJsonString(writer, measurement.Name);
        writer.WriteLine(",");
        writer.Write("      \"file\": ");
        WriteJsonString(writer, measurement.File);
        writer.WriteLine(",");
        writer.WriteLine($"      \"localZ\": {measurement.LocalZ},");
        writer.WriteLine($"      \"worldZ\": {measurement.WorldZ},");
        writer.Write("      \"shadowMode\": ");
        WriteJsonString(writer, measurement.ShadowMode);
        writer.WriteLine(",");
        writer.Write("      \"mappingPreview\": ");
        WriteJsonBoolean(writer, measurement.MappingPreview);
        writer.WriteLine(",");
        writer.Write("      \"weather\": ");
        WriteJsonString(writer, measurement.Weather);
        writer.WriteLine(",");
        writer.WriteLine($"      \"width\": {measurement.Width},");
        writer.WriteLine($"      \"height\": {measurement.Height},");
        writer.Write("      \"meanLuminance\": ");
        WriteJsonNumber(writer, measurement.MeanLuminance);
        writer.WriteLine(",");
        WriteColorSample(writer, "shadowProbe", measurement.ShadowProbe, true);
        WriteColorSample(writer, "clearProbe", measurement.ClearProbe, true);
        WriteColorSample(writer, "activeFloor", measurement.ActiveFloor, false);
        writer.Write("    }");
    }

    private static void WriteColorSample(
        TextWriter writer,
        string name,
        ZLevelLightingCaptureColorSample sample,
        bool trailingComma)
    {
        writer.Write($"      \"{name}\": {{ \"red\": ");
        WriteJsonNumber(writer, sample.Red);
        writer.Write(", \"green\": ");
        WriteJsonNumber(writer, sample.Green);
        writer.Write(", \"blue\": ");
        WriteJsonNumber(writer, sample.Blue);
        writer.Write(", \"alpha\": ");
        WriteJsonNumber(writer, sample.Alpha);
        writer.Write(" }");
        writer.WriteLine(trailingComma ? "," : string.Empty);
    }

    private static void WriteMetrics(TextWriter writer, CaptureMetrics metrics)
    {
        writer.WriteLine("  \"metrics\": {");
        writer.WriteLine($"    \"shadowAtlasRenders\": {metrics.ShadowAtlasRenders},");
        writer.WriteLine($"    \"shadowLights\": {metrics.ShadowLights},");
        writer.WriteLine($"    \"shadowFloorGroups\": {metrics.ShadowFloorGroups},");
        writer.WriteLine($"    \"shadowFallbacks\": {metrics.ShadowFallbacks},");
        writer.WriteLine(
            $"    \"shadowLightBudgetExhaustions\": {metrics.ShadowLightBudgetExhaustions},");
        writer.WriteLine(
            $"    \"shadowFloorGroupBudgetExhaustions\": {metrics.ShadowFloorGroupBudgetExhaustions},");
        writer.WriteLine($"    \"mappingBuildFrames\": {metrics.MappingBuildFrames},");
        writer.WriteLine($"    \"mappingRenderFrames\": {metrics.MappingRenderFrames},");
        writer.WriteLine($"    \"renderedTiles\": {metrics.RenderedTiles},");
        writer.WriteLine($"    \"weatherMaskPlans\": {metrics.WeatherMaskPlans},");
        writer.WriteLine($"    \"weatherMaskTileChecks\": {metrics.WeatherMaskTileChecks},");
        writer.WriteLine($"    \"weatherMaskRuns\": {metrics.WeatherMaskRuns},");
        writer.WriteLine($"    \"weatherMaskFailClosedPlans\": {metrics.WeatherMaskFailClosedPlans},");
        writer.WriteLine(
            $"    \"weatherMaskTileBudgetExhaustions\": {metrics.WeatherMaskTileBudgetExhaustions},");
        writer.WriteLine(
            $"    \"weatherMaskRunBudgetExhaustions\": {metrics.WeatherMaskRunBudgetExhaustions},");
        writer.WriteLine($"    \"weatherMaskRenderFrames\": {metrics.WeatherMaskRenderFrames},");
        writer.WriteLine($"    \"weatherMaskRenderRuns\": {metrics.WeatherMaskRenderRuns},");
        writer.WriteLine($"    \"weatherMaskRenderDrawCalls\": {metrics.WeatherMaskRenderDrawCalls}");
        writer.Write("  }");
    }

    private static void WriteJsonBoolean(TextWriter writer, bool value)
    {
        writer.Write(value ? "true" : "false");
    }

    private static void WriteJsonNumber(TextWriter writer, double value)
    {
        writer.Write(value.ToString("0.######", CultureInfo.InvariantCulture));
    }

    private static void WriteJsonString(TextWriter writer, string? value)
    {
        if (value is null)
        {
            writer.Write("null");
            return;
        }

        writer.Write('"');
        foreach (var character in value)
        {
            switch (character)
            {
                case '"':
                    writer.Write("\\\"");
                    break;
                case '\\':
                    writer.Write("\\\\");
                    break;
                case '\b':
                    writer.Write("\\b");
                    break;
                case '\f':
                    writer.Write("\\f");
                    break;
                case '\n':
                    writer.Write("\\n");
                    break;
                case '\r':
                    writer.Write("\\r");
                    break;
                case '\t':
                    writer.Write("\\t");
                    break;
                default:
                    if (char.IsControl(character))
                    {
                        writer.Write("\\u");
                        writer.Write(((int) character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        writer.Write(character);
                    }
                    break;
            }
        }

        writer.Write('"');
    }

    private void RestoreState()
    {
        if (_restored)
            return;

        _restored = true;
        if (!_fixturePrepared)
            return;

        _zLevelOverlay.SetMappingPreview(_originalMappingPreview);
        _lightManager.DrawShadows = _originalDrawShadows;
        _configuration.SetCVar(CVars.LightSoftShadows, _originalSoftShadows);
        if (_originalEye != null)
            _eyeManager.CurrentEye = _originalEye;

        if (_playerViewMoved && _network.IsConnected)
            _console.RemoteExecuteCommand(null, $"zlevelset {_originalPlayerLocalZ}");

        if (_weatherActive && _network.IsConnected)
            _console.RemoteExecuteCommand(null, $"weatherset {_fixtureMapId} null");

        if (_weatherTileDefinition != null)
            _weatherTileDefinition.Weather = _originalWeatherTilePolicy;

        if (_hiddenPlayerSprite is { } player &&
            TryComp(player, out SpriteComponent? sprite))
        {
            _sprites.SetVisible((player, sprite), _originalPlayerSpriteVisible);
        }

        if (_captureEntity is { } captureEntity && Exists(captureEntity))
            QueueDel(captureEntity);

        _hiddenPlayerSprite = null;
        _captureEntity = null;
        _captureEye = null;
        _playerViewMoved = false;
    }

    public override void Shutdown()
    {
        RestoreState();
        base.Shutdown();
    }

    private static TimeSpan Elapsed(long started)
    {
        return TimeSpan.FromSeconds((Stopwatch.GetTimestamp() - started) / (double) Stopwatch.Frequency);
    }

    private enum CapturePhase : byte
    {
        Idle,
        WaitingForConnection,
        WaitingForObserver,
        WaitingForFixture,
        WaitingForServerView,
        Stabilizing,
        WaitingForScreenshot,
        ShutdownDelay,
    }

    private readonly record struct CaptureSpec(
        string Name,
        int LocalZ,
        bool DrawShadows,
        bool SoftShadows,
        bool MappingPreview,
        bool WeatherCapture = false,
        bool WeatherEnabled = false);

    private sealed record CaptureMeasurement(
        string Name,
        string File,
        int LocalZ,
        int WorldZ,
        string ShadowMode,
        bool MappingPreview,
        string Weather,
        int Width,
        int Height,
        double MeanLuminance,
        ZLevelLightingCaptureColorSample ShadowProbe,
        ZLevelLightingCaptureColorSample ClearProbe,
        ZLevelLightingCaptureColorSample ActiveFloor);

    private sealed record CaptureCheck(string Name, bool Passed, string Details);

    private readonly record struct FixtureLayerInventory(
        int Tiles,
        int Lights,
        int EnabledLights,
        int Occluders);

    private sealed record CaptureMetrics(
        long ShadowAtlasRenders,
        long ShadowLights,
        long ShadowFloorGroups,
        long ShadowFallbacks,
        long ShadowLightBudgetExhaustions,
        long ShadowFloorGroupBudgetExhaustions,
        long MappingBuildFrames,
        long MappingRenderFrames,
        long RenderedTiles,
        long WeatherMaskPlans,
        long WeatherMaskTileChecks,
        long WeatherMaskRuns,
        long WeatherMaskFailClosedPlans,
        long WeatherMaskTileBudgetExhaustions,
        long WeatherMaskRunBudgetExhaustions,
        long WeatherMaskRenderFrames,
        long WeatherMaskRenderRuns,
        long WeatherMaskRenderDrawCalls);

}
