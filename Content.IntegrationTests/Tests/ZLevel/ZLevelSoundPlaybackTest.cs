// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using System.Numerics;
using Content.IntegrationTests.Tests.Atmos;
using Content.Server.ZLevel.Systems;
using Content.Shared.Atmos;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Content.Shared.Tests;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared;
using Robust.Shared.Audio;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using ServerAudioSystem = Robust.Server.Audio.AudioSystem;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelSoundPlaybackTest : AtmosTest
{
    protected override ResPath? TestMapPath =>
        new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task PressurizedRoutePublishesStableSnapshotAndVacuumFailsClosed()
    {
        await Server.WaitAssertion(() =>
        {
            var fixture = CreatePlaybackFixture();
            var playback = SEntMan.System<ZLevelSoundPlaybackSystem>();
            var candidates = new HashSet<EntityUid> { fixture.Audio };
            var visible = new HashSet<EntityUid> { fixture.Audio };
            var culled = new HashSet<EntityUid>();
            var viewers = new[] { fixture.Viewer };
            playback.ResetMetrics();

            var authorized = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            Assert.Multiple(() =>
            {
                Assert.That(authorized.AudioCandidates, Is.EqualTo(1));
                Assert.That(authorized.RouteChecks, Is.EqualTo(1));
                Assert.That(authorized.Presentations, Is.EqualTo(1));
                Assert.That(authorized.RouteBudgetExhausted, Is.False);
                Assert.That(authorized.PresentationBudgetExhausted, Is.False);
                Assert.That(visible, Does.Contain(fixture.Audio));
                Assert.That(visible, Does.Contain(fixture.Source),
                    "The transform parent must remain available to network the audio coordinates.");
                Assert.That(culled, Is.Empty);
            });

            Assert.That(playback.TryGetSessionPresentations(ServerSession, out var presentations), Is.True);
            Assert.That(presentations, Has.Count.EqualTo(1));
            var presentation = presentations[0];
            Assert.Multiple(() =>
            {
                Assert.That(presentation.Audio, Is.EqualTo(SEntMan.GetNetEntity(fixture.Audio)));
                Assert.That(presentation.Viewer, Is.EqualTo(Player));
                Assert.That(presentation.MapId, Is.EqualTo(MapData.MapId));
                Assert.That(presentation.ListenerWorldZ, Is.EqualTo(1));
                Assert.That(presentation.ListenerPosition, Is.EqualTo(fixture.Viewer.WorldPosition));
                Assert.That(Vector2.Distance(presentation.PortalPosition, fixture.PortalWorldPosition),
                    Is.LessThan(0.001f));
                Assert.That(presentation.Distance, Is.GreaterThan(0f));
                Assert.That(presentation.Distance, Is.LessThanOrEqualTo(8f));
                Assert.That(presentation.Transmission, Is.InRange(float.Epsilon, 1f));
            });

            visible.Clear();
            visible.Add(fixture.Audio);
            var unchanged = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            Assert.That(unchanged.Presentations, Is.EqualTo(1));
            Assert.That(playback.Snapshot().SnapshotsSent, Is.EqualTo(1),
                "An unchanged presentation snapshot must not generate network traffic.");

            fixture.UpperMixture.Clear();
            visible.Clear();
            visible.Add(fixture.Audio);
            var blocked = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            var metrics = playback.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(blocked.RouteChecks, Is.EqualTo(1));
                Assert.That(blocked.Presentations, Is.Zero);
                Assert.That(visible, Does.Not.Contain(fixture.Audio));
                Assert.That(culled, Does.Contain(fixture.Audio));
                Assert.That(playback.TryGetSessionPresentations(ServerSession, out _), Is.False);
                Assert.That(metrics.SnapshotsSent, Is.EqualTo(2),
                    "The second network update is the explicit empty replacement snapshot.");
                Assert.That(metrics.SnapshotPresentationsSent, Is.EqualTo(1));
            });

            Assert.That(
                SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(fixture.Source, 1),
                Is.True);
            visible.Add(fixture.Audio);
            var sameFloor = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            Assert.Multiple(() =>
            {
                Assert.That(sameFloor.RouteChecks, Is.Zero);
                Assert.That(sameFloor.Presentations, Is.Zero);
                Assert.That(visible, Does.Contain(fixture.Audio));
                Assert.That(culled, Is.Empty,
                    "Same-floor audio retains native behavior even when its atmosphere is vacuum.");
            });
        });
    }

    [Test]
    public async Task PlaybackBudgetsClampAndDenyWhenNoWorkIsAvailable()
    {
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxRouteChecksPerRefresh, -1);
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxPresentationsPerRefresh, int.MaxValue);
        });

        await Server.WaitAssertion(() =>
        {
            var fixture = CreatePlaybackFixture();
            var playback = SEntMan.System<ZLevelSoundPlaybackSystem>();
            var candidates = new HashSet<EntityUid> { fixture.Audio };
            var visible = new HashSet<EntityUid> { fixture.Audio };
            var culled = new HashSet<EntityUid>();
            var viewers = new[] { fixture.Viewer };
            playback.ResetMetrics();

            Assert.Multiple(() =>
            {
                Assert.That(playback.MaxRouteChecksPerRefresh, Is.Zero);
                Assert.That(playback.MaxPresentationsPerRefresh,
                    Is.EqualTo(ZLevelSoundPlaybackSystem.MaximumPresentationsPerRefresh));
            });

            var routeLimited = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            Assert.Multiple(() =>
            {
                Assert.That(routeLimited.RouteChecks, Is.Zero);
                Assert.That(routeLimited.Presentations, Is.Zero);
                Assert.That(routeLimited.RouteBudgetExhausted, Is.True);
                Assert.That(culled, Does.Contain(fixture.Audio));
            });

            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxRouteChecksPerRefresh, int.MaxValue);
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxPresentationsPerRefresh, -1);
            visible.Add(fixture.Audio);
            var presentationLimited = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            Assert.Multiple(() =>
            {
                Assert.That(playback.MaxRouteChecksPerRefresh,
                    Is.EqualTo(ZLevelSoundPlaybackSystem.MaximumRouteChecksPerRefresh));
                Assert.That(playback.MaxPresentationsPerRefresh, Is.Zero);
                Assert.That(presentationLimited.RouteChecks, Is.Zero);
                Assert.That(presentationLimited.Presentations, Is.Zero);
                Assert.That(presentationLimited.PresentationBudgetExhausted, Is.True);
                Assert.That(culled, Does.Contain(fixture.Audio));
            });

            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxRouteChecksPerRefresh, 8);
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxPresentationsPerRefresh, 8);
            visible.Add(fixture.Audio);
            var authorized = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            visible.Add(fixture.Audio);
            var repeated = playback.RefreshSession(
                ServerSession,
                viewers,
                candidates,
                visible,
                culled);
            var metrics = playback.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(authorized.Presentations, Is.EqualTo(1));
                Assert.That(repeated.Presentations, Is.EqualTo(1));
                Assert.That(culled, Is.Empty);
                Assert.That(metrics.RouteBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.PresentationBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.SnapshotsSent, Is.EqualTo(1));
                Assert.That(metrics.ActivePresentations, Is.EqualTo(1));
            });

            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxRouteChecksPerRefresh, 128);
            Server.CfgMan.SetCVar(CCVars.ZLevelSoundPlaybackMaxPresentationsPerRefresh, 128);
        });
    }

    [Test]
    public async Task VisualBudgetFailOpenStillCullsUnauthorizedAudio()
    {
        NetEntity sourceNet = default;
        NetEntity audioNet = default;

        await Server.WaitAssertion(() =>
        {
            Server.CfgMan.SetCVar(CVars.NetPVS, true);
            Server.CfgMan.SetCVar(CCVars.ZLevelPvsVisibilityCheckBudget, 0);
            var fixture = CreatePlaybackFixture();
            fixture.UpperMixture.Clear();
            sourceNet = SEntMan.GetNetEntity(fixture.Source);
            audioNet = SEntMan.GetNetEntity(fixture.Audio);

            var pvs = SEntMan.System<ZLevelPvsSystem>();
            var playback = SEntMan.System<ZLevelSoundPlaybackSystem>();
            playback.ResetMetrics();
            pvs.RefreshSession(ServerSession);

            Assert.Multiple(() =>
            {
                Assert.That(pvs.VisibilityCheckBudget, Is.Zero);
                Assert.That(playback.TryGetSessionPresentations(ServerSession, out _), Is.False);
                Assert.That(playback.Snapshot().AuthorizedPresentations, Is.Zero);
            });
        });

        await Pair.RunTicksSync(5);
        Assert.Multiple(() =>
        {
            Assert.That(CEntMan.TryGetEntity(sourceNet, out _), Is.True,
                "Visual entities must preserve the PVS fail-open behavior.");
            Assert.That(CEntMan.TryGetEntity(audioNet, out _), Is.False,
                "Denied cross-floor audio must remain explicitly culled.");
        });

        await Server.WaitPost(() =>
            Server.CfgMan.SetCVar(
                CCVars.ZLevelPvsVisibilityCheckBudget,
                ZLevelPvsSystem.DefaultVisibilityCheckBudget));
    }

    private PlaybackFixture CreatePlaybackFixture()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var marker), Is.True);

        var map = SEntMan.System<SharedMapSystem>();
        var zLevels = SEntMan.System<SharedZLevelSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        var tile = map.TileIndicesFor(MapData.Grid, grid, Xform(marker).Coordinates);
        var coordinates = map.GridTileToLocal(MapData.Grid, grid, tile);

        SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            MapData.MapUid,
            0,
            1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        MapData.Grid.Comp.CanSplit = false;
        FillLayer(map, grid, tile, 2, 1, new Tile(1));
        SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

        var lowerMixture = SAtmos.GetZLevelTileMixture(
            RelevantAtmos,
            null,
            new ZLevelTileIndices(tile.X, tile.Y, 0),
            true);
        var upperMixture = SAtmos.GetZLevelTileMixture(
            RelevantAtmos,
            null,
            new ZLevelTileIndices(tile.X, tile.Y, 1),
            true);
        Assert.That(lowerMixture, Is.Not.Null);
        Assert.That(upperMixture, Is.Not.Null);
        MakeAir(lowerMixture!);
        MakeAir(upperMixture!);
        SetBoundary(tile, 0, ZLevelBoundaryChannels.Sound | ZLevelBoundaryChannels.Atmosphere);

        Transform.SetCoordinates(SPlayer, coordinates);
        Assert.That(zLevels.SetZLevelPosition(SPlayer, 1), Is.True);
        var source = SEntMan.SpawnEntity(null, coordinates);
        Assert.That(zLevels.SetZLevelPosition(source, 0), Is.True);

        var audio = SEntMan.System<ServerAudioSystem>().PlayPvs(
            new SoundPathSpecifier("/Audio/Weapons/click.ogg"),
            source,
            AudioParams.Default.WithMaxDistance(8f).WithLoop(true));
        Assert.That(audio, Is.Not.Null);

        var playerTransform = Xform(SPlayer);
        var worldPosition = Transform.GetWorldPosition(playerTransform);
        var viewer = new ZLevelPvsViewerContext(
            SPlayer,
            MapData.MapId,
            MapData.Grid,
            worldPosition,
            coordinates.Position,
            1,
            1,
            1f,
            true);
        var portalWorldPosition = Transform.ToMapCoordinates(coordinates).Position;
        return new PlaybackFixture(
            source,
            audio!.Value.Entity,
            upperMixture,
            viewer,
            portalWorldPosition);
    }

    private void FillLayer(
        SharedMapSystem map,
        MapGridComponent grid,
        Vector2i center,
        int radius,
        int z,
        Tile tile)
    {
        for (var y = center.Y - radius; y <= center.Y + radius; y++)
        {
            for (var x = center.X - radius; x <= center.X + radius; x++)
                map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(x, y, z), tile);
        }
    }

    private void SetBoundary(
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels opens)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        map.SetZLevelTile(
            MapData.Grid,
            grid,
            new ZLevelTileIndices(tile.X, tile.Y, lowerLocalZ),
            new Tile(1));
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(MapData.Grid, grid, tile));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        boundaries.SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            ZLevelBoundaryChannels.None);
        Transform.AnchorEntity(provider, Xform(provider));
    }

    private static void MakeAir(GasMixture mixture)
    {
        mixture.Clear();
        mixture.Temperature = Atmospherics.T20C;
        mixture.AdjustMoles(Gas.Nitrogen, 100f);
    }

    private readonly record struct PlaybackFixture(
        EntityUid Source,
        EntityUid Audio,
        GasMixture UpperMixture,
        ZLevelPvsViewerContext Viewer,
        Vector2 PortalWorldPosition);
}
