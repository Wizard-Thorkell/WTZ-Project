// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelInteractionAuthorityTest : GameTest
{
    private const int FrameOrigin = 5;
    private const int AllocationIterations = 4_096;

    [Test]
    public async Task ExplicitVerticalInteractionRequiresItsBoundaryChannel()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var lower = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var upper = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, lower), Is.True);
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);
            });

            var provider = SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Projectile);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.Interaction,
                closes: ZLevelBoundaryChannels.None);
            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 0f), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, float.NaN), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 0.9f), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.True);
            });

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.All,
                closes: ZLevelBoundaryChannels.Interaction);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.False);

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(10));
                Assert.That(snapshot.InteractionAllowed, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(8));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionVerticalAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentLevelRejected, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRangeRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionTraceRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionRemoteOriginQueries, Is.Zero);
                Assert.That(snapshot.InteractionPhysicalQueries, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.EqualTo(4));
            });
        });
    }

    [Test]
    public async Task PhysicalSameFloorCheckIgnoresRemoteEyeRedirection()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var physicalTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var remoteEye = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var remoteTarget = Spawn(testMap, new Vector2(0.5f, 0.5f), 1);
            var eye = SEntMan.EnsureComponent<EyeComponent>(user);
            SEntMan.System<SharedEyeSystem>().SetTarget(user, remoteEye, eye);

            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();
            Assert.Multiple(() =>
            {
                Assert.That(authority.AreOnSameWorldLevel(user, physicalTarget), Is.True);
                Assert.That(authority.AreOnSameWorldLevel(user, remoteTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, physicalTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, remoteTarget), Is.True);
            });

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionRemoteOriginQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentLevelRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionPhysicalQueries, Is.EqualTo(2));
                Assert.That(snapshot.InteractionPhysicalAllowed, Is.EqualTo(1));
                Assert.That(snapshot.InteractionPhysicalRejected, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task InteractionMetricsClassifyInvalidMapAndFrameRejections()
    {
        var firstMap = await Pair.CreateTestMap();
        var secondMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(firstMap);
            Configure(secondMap);
            var origin = Spawn(firstMap, new Vector2(0.5f, 0.5f), 0);
            var differentMapTarget = Spawn(secondMap, new Vector2(0.5f, 0.5f), 0);

            var mapManager = Server.ResolveDependency<IMapManager>();
            var otherGrid = mapManager.CreateGridEntity(firstMap.MapId);
            var differentFrameTarget = SEntMan.SpawnEntity(
                null,
                new EntityCoordinates(otherGrid, new Vector2(0.5f, 0.5f)));
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(differentFrameTarget, 1),
                Is.True);

            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();
            metrics.ResetCounters();

            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(EntityUid.Invalid, origin), Is.False);
                Assert.That(authority.CanDirectlyInteract(origin, differentMapTarget), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(origin, differentFrameTarget, 100f), Is.False);
            });

            var snapshot = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(3));
                Assert.That(snapshot.InteractionAllowed, Is.Zero);
                Assert.That(snapshot.InteractionRejected, Is.EqualTo(3));
                Assert.That(snapshot.InteractionInvalidContextRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionDifferentMapRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionFrameRejected, Is.EqualTo(1));
                Assert.That(snapshot.InteractionTraceRejected, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.Zero);
            });

            metrics.ResetCounters();
            var reset = metrics.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(reset.InteractionQueries, Is.Zero);
                Assert.That(reset.InteractionAllowed, Is.Zero);
                Assert.That(reset.InteractionRejected, Is.Zero);
                Assert.That(reset.InteractionInvalidContextRejected, Is.Zero);
                Assert.That(reset.InteractionDifferentMapRejected, Is.Zero);
                Assert.That(reset.InteractionFrameRejected, Is.Zero);
            });
        });
    }

    [TestCase("ZLevelStairsUp", 0, 1, true)]
    [TestCase("ZLevelStairsDown", 1, 0, true)]
    [TestCase("ZLevelLadder", 0, 1, true)]
    [TestCase("ZLevelFloorOpeningMarker", 1, 0, true)]
    [TestCase("ZLevelShaftMarker", 0, 1, true)]
    [TestCase("ZLevelGrateBoundaryMarker", 1, 0, false)]
    [TestCase("ZLevelSealedBoundaryMarker", 0, 1, false)]
    public async Task BoundaryPrototypesAuthorInteractionPolicy(
        string prototype,
        int providerLocalZ,
        int targetLocalZ,
        bool expected)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var position = new Vector2(0.5f, 0.5f);
            var provider = SEntMan.SpawnEntity(prototype, new EntityCoordinates(testMap.Grid, position));
            Assert.That(
                SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(provider, providerLocalZ),
                Is.True);

            var user = Spawn(testMap, position, providerLocalZ);
            var target = Spawn(testMap, position, targetLocalZ);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(user, target), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(user, target, 1f), Is.EqualTo(expected));
            });
        });
    }

    [Test]
    public async Task WarmSameLevelAuthorityChecksDoNotAllocate()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            Configure(testMap);
            var user = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var target = Spawn(testMap, new Vector2(0.5f, 0.5f), 0);
            var authority = SEntMan.System<SharedZLevelInteractionSystem>();
            var metrics = SEntMan.System<SharedZLevelMetricsSystem>();

            for (var i = 0; i < 32; i++)
                Assert.That(authority.CanDirectlyInteract(user, target), Is.True);

            metrics.ResetCounters();
            var allowed = true;
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < AllocationIterations; i++)
                allowed &= authority.CanDirectlyInteract(user, target);
            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var snapshot = metrics.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(allowed, Is.True);
                Assert.That(allocatedBytes, Is.Zero);
                Assert.That(snapshot.InteractionQueries, Is.EqualTo(AllocationIterations));
                Assert.That(snapshot.InteractionSameLevelAllowed, Is.EqualTo(AllocationIterations));
                Assert.That(snapshot.InteractionRejected, Is.Zero);
                Assert.That(snapshot.TraceQueries, Is.Zero);
            });
        });
    }

    private void Configure(TestMapData testMap)
    {
        SEntMan.System<SharedZLevelMapSystem>().Configure(
            testMap.MapUid,
            0,
            1,
            0,
            ZLevelDefaultBoundaryMode.TileAboveCloses);
        var transform = SEntMan.System<SharedTransformSystem>();
        Assert.That(transform.SetZLevelFrameOrigin(testMap.Grid, FrameOrigin), Is.True);
        transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -5f));
        transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(27));

        var definitions = Server.ResolveDependency<ITileDefinitionManager>();
        var floor = (ContentTileDefinition) definitions["FloorSteel"];
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        for (var z = 0; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(x, y, z), new Tile(floor.TileId));
                }
            }
        }
    }

    private EntityUid Spawn(TestMapData testMap, Vector2 position, int localZ)
    {
        var entity = SEntMan.SpawnEntity(null, new EntityCoordinates(testMap.Grid, position));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(entity, localZ), Is.True);
        return entity;
    }

    private EntityUid SetBoundary(
        TestMapData testMap,
        Vector2i tile,
        int lowerLocalZ,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes = ZLevelBoundaryChannels.None)
    {
        var map = SEntMan.System<SharedMapSystem>();
        var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
        var provider = SEntMan.SpawnEntity(null, map.GridTileToLocal(testMap.Grid, grid, tile));
        Assert.That(SEntMan.System<SharedZLevelSystem>().SetZLevelPosition(provider, lowerLocalZ), Is.True);
        var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(provider);
        SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            closes);
        SEntMan.System<SharedTransformSystem>().AnchorEntity(
            provider,
            SEntMan.GetComponent<TransformComponent>(provider));
        return provider;
    }

    private void SetBoundary(
        EntityUid provider,
        ZLevelBoundaryChannels opens,
        ZLevelBoundaryChannels closes)
    {
        var boundary = SEntMan.GetComponent<ZLevelBoundaryComponent>(provider);
        SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
            (provider, boundary),
            true,
            1,
            opens,
            closes);
    }
}
