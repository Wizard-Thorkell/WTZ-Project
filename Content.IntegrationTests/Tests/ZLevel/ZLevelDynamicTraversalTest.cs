// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

#nullable enable

using System.Linq;
using Content.IntegrationTests.Tests.Movement;
using Content.Server.Power.Components;
using Content.Server.ZLevel.Components;
using Content.Server.ZLevel.Navigation;
using Content.Server.ZLevel.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Power;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelDynamicTraversalTest : MovementTest
{
    [Test]
    public async Task DynamicPolicyControlsEdgesSnapshotsAndMetrics()
    {
        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var elevator = SpawnDynamicElevator(
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1.25),
                7.5f);
            graph.ResetMetrics();

            Assert.That(graph.TryResolveEdge(elevator, out var edge),
                Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.Multiple(() =>
            {
                Assert.That(edge.Source.Kind, Is.EqualTo(ZLevelTraversalKind.Elevator));
                Assert.That(edge.Cost, Is.EqualTo(11.5f));
                Assert.That(edge.TraversalDelay, Is.EqualTo(TimeSpan.FromSeconds(3.25)));
            });

            var snapshot = graph.CreateSnapshot(SEntMan.GetComponent<TransformComponent>(elevator).MapID);
            Assert.That(snapshot.Edges.Single(item => item.Source.Traversal == elevator), Is.EqualTo(edge));
            Assert.That(graph.TryGetConnectedExecutableTraversal(
                elevator,
                edge.Source.Tile,
                edge,
                out var connected),
                Is.True);
            Assert.That(connected, Is.EqualTo(elevator));

            var dynamicTraversal = SEntMan.GetComponent<ZLevelDynamicTraversalComponent>(elevator);
            var revision = graph.EnvironmentRevision;
            Assert.Multiple(() =>
            {
                Assert.That(graph.ConfigureDynamicTraversal(
                    elevator,
                    true,
                    true,
                    true,
                    TimeSpan.FromTicks(-1),
                    0f,
                    dynamicTraversal),
                    Is.False);
                Assert.That(graph.ConfigureDynamicTraversal(
                    elevator,
                    true,
                    true,
                    true,
                    ZLevelTraversalGraphSystem.MaximumDynamicWaitDelay + TimeSpan.FromTicks(1),
                    0f,
                    dynamicTraversal),
                    Is.False);
                Assert.That(graph.ConfigureDynamicTraversal(
                    elevator,
                    true,
                    true,
                    true,
                    TimeSpan.Zero,
                    float.NaN,
                    dynamicTraversal),
                    Is.False);
                Assert.That(graph.ConfigureDynamicTraversal(
                    elevator,
                    true,
                    true,
                    true,
                    TimeSpan.Zero,
                    ZLevelTraversalGraphSystem.MaximumDynamicWaitNavigationCost + 1f,
                    dynamicTraversal),
                    Is.False);
                Assert.That(graph.EnvironmentRevision, Is.EqualTo(revision));
            });

            Assert.That(graph.ConfigureDynamicTraversal(
                elevator,
                true,
                true,
                true,
                TimeSpan.FromSeconds(1.25),
                8f,
                dynamicTraversal),
                Is.True);
            Assert.That(graph.TryResolveEdge(elevator, out var costChanged),
                Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.Multiple(() =>
            {
                Assert.That(ZLevelTraversalGraphSystem.HasEquivalentEdge(edge, costChanged), Is.False);
                Assert.That(ZLevelTraversalGraphSystem.HasEquivalentExecutionProfile(edge, costChanged), Is.True,
                    "A route cost change must invalidate planning without interrupting physical traversal.");
                Assert.That(graph.TryGetConnectedExecutableTraversal(
                    elevator,
                    edge.Source.Tile,
                    edge,
                    out _),
                    Is.True);
            });
            Assert.That(graph.ConfigureDynamicTraversal(
                elevator,
                true,
                true,
                true,
                TimeSpan.FromSeconds(1.25),
                7.5f,
                dynamicTraversal),
                Is.True);

            Assert.That(graph.ConfigureDynamicTraversal(
                elevator,
                false,
                true,
                true,
                TimeSpan.FromSeconds(1.25),
                7.5f,
                dynamicTraversal),
                Is.True);
            Assert.That(graph.TryResolveEdge(elevator, out _), Is.EqualTo(ZLevelTraversalEdgeStatus.Disabled));
            Assert.That(graph.TryGetConnectedExecutableTraversal(
                elevator,
                edge.Source.Tile,
                edge,
                out _),
                Is.False);
            Assert.That(graph.ValidateSnapshot(snapshot), Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged));

            Assert.That(graph.ConfigureDynamicTraversal(
                elevator,
                true,
                false,
                true,
                TimeSpan.FromSeconds(1.25),
                7.5f,
                dynamicTraversal),
                Is.True);
            Assert.That(graph.TryResolveEdge(elevator, out _), Is.EqualTo(ZLevelTraversalEdgeStatus.Unavailable));

            Assert.That(graph.ConfigureDynamicTraversal(
                elevator,
                true,
                true,
                true,
                TimeSpan.FromSeconds(1.25),
                7.5f,
                dynamicTraversal),
                Is.True);
            SetPower(elevator, false);
            Assert.That(graph.TryResolveEdge(elevator, out _), Is.EqualTo(ZLevelTraversalEdgeStatus.Unpowered));

            SetPower(elevator, true);
            Assert.That(graph.TryResolveEdge(elevator, out var restored), Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.That(ZLevelTraversalGraphSystem.HasEquivalentEdge(edge, restored), Is.True);

            var metrics = graph.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(metrics.DisabledEdges, Is.GreaterThanOrEqualTo(1));
                Assert.That(metrics.UnavailableEdges, Is.EqualTo(1));
                Assert.That(metrics.UnpoweredEdges, Is.EqualTo(1));
                Assert.That(metrics.DynamicStateChanges, Is.GreaterThanOrEqualTo(5));
                Assert.That(metrics.SnapshotBuilds, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task ElevatorDestinationChangesTopologyAndResolvedEndpoint()
    {
        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var elevator = SpawnDynamicElevator(TimeSpan.FromSeconds(2), TimeSpan.Zero, 0f);
            AddLowerTraversalBoundary(elevator);
            graph.ResetMetrics();

            Assert.That(graph.TryResolveEdge(elevator, out var upward),
                Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            var snapshot = graph.CreateSnapshot(SEntMan.GetComponent<TransformComponent>(elevator).MapID);
            var topology = graph.TopologyRevision;

            Assert.That(graph.SetElevatorDestination(elevator, -1), Is.True);
            Assert.That(graph.TryResolveEdge(elevator, out var downward),
                Is.EqualTo(ZLevelTraversalEdgeStatus.Valid));
            Assert.Multiple(() =>
            {
                Assert.That(upward.Destination.LocalZ, Is.EqualTo(1));
                Assert.That(downward.Destination.LocalZ, Is.EqualTo(-1));
                Assert.That(downward.ZOffset, Is.EqualTo(-1));
                Assert.That(graph.TopologyRevision, Is.EqualTo(topology + 1));
                Assert.That(graph.ValidateSnapshot(snapshot), Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.TopologyChanged));
                Assert.That(graph.SetElevatorDestination(elevator, 0), Is.False);
                Assert.That(graph.TopologyRevision, Is.EqualTo(topology + 1));
                Assert.That(graph.Snapshot().DestinationChanges, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task DynamicChangesCancelPendingTraversalAndWaitDelayIsAuthoritative()
    {
        EntityUid elevator = default;
        EntityUid player = default;

        await Server.WaitAssertion(() =>
        {
            elevator = SpawnDynamicElevator(
                TimeSpan.FromSeconds(0.4),
                TimeSpan.FromSeconds(0.6),
                0f);
            player = ToServer(Player);
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(elevator, player), Is.True);
            Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(player), Is.True);
        });

        await RunSeconds(0.1f);
        await Server.WaitPost(() => SetPower(elevator, false));
        await RunSeconds(0.6f);
        await AssertCancelled(elevator, player, "Losing power must cancel the captured traversal.");

        await Server.WaitAssertion(() =>
        {
            SetPower(elevator, true);
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(elevator, player), Is.True);
        });
        await RunSeconds(0.1f);
        await Server.WaitPost(() => ConfigureCallable(elevator, false));
        await RunSeconds(0.6f);
        await AssertCancelled(elevator, player, "Becoming unavailable must cancel the captured traversal.");

        await Server.WaitAssertion(() =>
        {
            ConfigureCallable(elevator, true);
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(elevator, player), Is.True);
        });
        await RunSeconds(0.1f);
        await Server.WaitPost(() =>
        {
            Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>().SetElevatorDestination(elevator, -1), Is.True);
        });
        await RunSeconds(0.6f);
        await AssertCancelled(elevator, player, "Changing destination must cancel the captured traversal.");

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>().SetElevatorDestination(elevator, 1), Is.True);
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().TryStartTraversal(elevator, player), Is.True);
        });

        await RunSeconds(0.8f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(player), Is.Zero,
                "The base and dynamic wait delays must both elapse.");
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(player, elevator), Is.True);
        });

        await RunSeconds(0.3f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).ZLevel, Is.EqualTo(1));
            Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(player), Is.False);
        });
    }

    [Test]
    public async Task ZeroDelayDynamicTraversalCompletesWithoutOrphanedDoAfter()
    {
        await Server.WaitAssertion(() =>
        {
            var elevator = SpawnDynamicElevator(TimeSpan.Zero, TimeSpan.Zero, 0f);
            var player = ToServer(Player);
            var traversal = SEntMan.System<ZLevelTraversalSystem>();

            Assert.That(traversal.TryStartTraversal(elevator, player), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.GetComponent<ZLevelPositionComponent>(player).ZLevel, Is.EqualTo(1));
                Assert.That(traversal.IsTraversalPending(player), Is.False);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(player), Is.False);
            });
        });
    }

    [Test]
    public async Task DeletingBaseFloorUserClearsPendingTraversal()
    {
        await Server.WaitAssertion(() =>
        {
            var elevator = SpawnDynamicElevator(TimeSpan.FromSeconds(2), TimeSpan.Zero, 0f);
            var coordinates = SEntMan.GetComponent<TransformComponent>(elevator).Coordinates;
            var user = SEntMan.SpawnEntity("MobMouse", coordinates);
            SEntMan.RemoveComponent<ZLevelPositionComponent>(user);
            SEntMan.EnsureComponent<DoAfterComponent>(user);
            var traversal = SEntMan.System<ZLevelTraversalSystem>();

            Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(user), Is.False);
            Assert.That(traversal.TryStartTraversal(elevator, user), Is.True);
            Assert.That(traversal.IsTraversalPending(user, elevator), Is.True);

            SEntMan.DeleteEntity(user);
            Assert.That(traversal.IsTraversalPending(user), Is.False);
        });
    }

    [Test]
    public async Task DynamicTraversalChurnKeepsSnapshotStorageBounded()
    {
        const int iterations = 512;

        await Server.WaitAssertion(() =>
        {
            var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
            var elevator = SpawnDynamicElevator(TimeSpan.FromSeconds(0.25), TimeSpan.Zero, 0f);
            var dynamicTraversal = SEntMan.GetComponent<ZLevelDynamicTraversalComponent>(elevator);
            var mapId = SEntMan.GetComponent<TransformComponent>(elevator).MapID;
            var cachedBefore = graph.Snapshot().CachedSnapshots;
            graph.ResetMetrics();

            var snapshot = graph.CreateSnapshot(mapId);
            var initialVersion = snapshot.Version;
            for (var i = 0; i < iterations; i++)
            {
                var enabled = (i & 1) != 0;
                Assert.That(graph.ConfigureDynamicTraversal(
                    elevator,
                    enabled,
                    true,
                    true,
                    TimeSpan.Zero,
                    0f,
                    dynamicTraversal),
                    Is.True);
                Assert.That(graph.ValidateSnapshot(snapshot),
                    Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.EnvironmentChanged));

                snapshot = graph.CreateSnapshot(mapId);
                Assert.Multiple(() =>
                {
                    Assert.That(graph.ValidateSnapshot(snapshot),
                        Is.EqualTo(ZLevelTraversalGraphSnapshotStatus.Current));
                    Assert.That(snapshot.Edges.Length, Is.EqualTo(enabled ? 1 : 0));
                    Assert.That(graph.CreateSnapshot(mapId).Edges.Equals(snapshot.Edges), Is.True);
                });
            }

            var version = graph.GetVersion(mapId);
            var metrics = graph.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(version.TopologyRevision, Is.EqualTo(initialVersion.TopologyRevision));
                Assert.That(version.EnvironmentRevision,
                    Is.EqualTo(initialVersion.EnvironmentRevision + iterations));
                Assert.That(metrics.DynamicStateChanges, Is.EqualTo(iterations));
                Assert.That(metrics.SnapshotRequests, Is.EqualTo(1 + iterations * 2));
                Assert.That(metrics.SnapshotBuilds, Is.EqualTo(1 + iterations));
                Assert.That(metrics.SnapshotCacheHits, Is.EqualTo(iterations));
                Assert.That(metrics.CachedSnapshots, Is.InRange(cachedBefore, cachedBefore + 1));
                Assert.That(metrics.MaxSnapshotAllocatedBytes, Is.LessThanOrEqualTo(16_384));
                Assert.That(snapshot.Edges, Has.Length.EqualTo(1));
            });
        });
    }

    private EntityUid SpawnDynamicElevator(
        TimeSpan traversalDelay,
        TimeSpan waitDelay,
        float waitNavigationCost)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var zLevel = SEntMan.System<SharedZLevelSystem>();
        var graph = SEntMan.System<ZLevelTraversalGraphSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        var coordinates = SEntMan.GetCoordinates(PlayerCoords);
        var tile = map.TileIndicesFor(MapData.Grid, grid, coordinates);

        map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, 1), new Tile(1));
        var elevator = SEntMan.SpawnEntity(null, coordinates);
        Assert.That(zLevel.SetZLevelPosition(elevator, 0), Is.True);

        var traversal = SEntMan.EnsureComponent<ZLevelTraversalComponent>(elevator);
        traversal.Kind = ZLevelTraversalKind.Elevator;
        traversal.ZOffset = 1;
        traversal.TraversalDelay = traversalDelay;
        traversal.NavigationCost = 4f;

        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(elevator);
        boundaries.SetBoundary(
            (elevator, boundary),
            true,
            1,
            ZLevelBoundaryChannels.Traversal,
            ZLevelBoundaryChannels.None);
        transform.AnchorEntity(elevator, SEntMan.GetComponent<TransformComponent>(elevator));
        graph.RefreshTraversal(elevator);
        boundaries.RefreshBoundary(elevator);

        var dynamicTraversal = SEntMan.EnsureComponent<ZLevelDynamicTraversalComponent>(elevator);
        var power = SEntMan.EnsureComponent<ApcPowerReceiverComponent>(elevator);
        power.Powered = true;
        Assert.That(graph.ConfigureDynamicTraversal(
            elevator,
            true,
            true,
            true,
            waitDelay,
            waitNavigationCost,
            dynamicTraversal),
            Is.True);
        return elevator;
    }

    private void AddLowerTraversalBoundary(EntityUid elevator)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var zLevel = SEntMan.System<SharedZLevelSystem>();
        var boundaries = SEntMan.System<SharedZLevelBoundarySystem>();
        var transform = SEntMan.System<SharedTransformSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
        var coordinates = SEntMan.GetComponent<TransformComponent>(elevator).Coordinates;
        var tile = map.TileIndicesFor(MapData.Grid, grid, coordinates);
        map.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(tile.X, tile.Y, -1), new Tile(1));

        var boundaryUid = SEntMan.SpawnEntity(null, coordinates);
        Assert.That(zLevel.SetZLevelPosition(boundaryUid, 0), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(boundaryUid);
        boundaries.SetBoundary(
            (boundaryUid, boundary),
            true,
            -1,
            ZLevelBoundaryChannels.Traversal,
            ZLevelBoundaryChannels.None);
        transform.AnchorEntity(boundaryUid, SEntMan.GetComponent<TransformComponent>(boundaryUid));
        boundaries.RefreshBoundary(boundaryUid);
    }

    private void SetPower(EntityUid elevator, bool powered)
    {
        var power = SEntMan.GetComponent<ApcPowerReceiverComponent>(elevator);
        power.Powered = powered;
        var powerChanged = new PowerChangedEvent(powered, powered ? power.Load : 0f);
        SEntMan.EventBus.RaiseLocalEvent(elevator, ref powerChanged);
    }

    private void ConfigureCallable(EntityUid elevator, bool callable)
    {
        var dynamicTraversal = SEntMan.GetComponent<ZLevelDynamicTraversalComponent>(elevator);
        Assert.That(SEntMan.System<ZLevelTraversalGraphSystem>().ConfigureDynamicTraversal(
            elevator,
            true,
            callable,
            false,
            TimeSpan.FromSeconds(0.6),
            0f,
            dynamicTraversal),
            Is.True);
    }

    private async Task AssertCancelled(EntityUid elevator, EntityUid player, string message)
    {
        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.System<SharedZLevelSystem>().GetZLevel(player), Is.Zero, message);
                Assert.That(SEntMan.System<ZLevelTraversalSystem>().IsTraversalPending(player, elevator), Is.False);
                Assert.That(SEntMan.HasComponent<ActiveDoAfterComponent>(player), Is.False);
            });
        });
    }
}
