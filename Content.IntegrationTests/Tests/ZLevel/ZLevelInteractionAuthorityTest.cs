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

            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper), Is.False);
            });

            var provider = SetBoundary(
                testMap,
                Vector2i.Zero,
                0,
                opens: ZLevelBoundaryChannels.Projectile);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper), Is.False);

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.Interaction,
                closes: ZLevelBoundaryChannels.None);
            Assert.Multiple(() =>
            {
                Assert.That(authority.CanDirectlyInteract(lower, upper), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 0.9f), Is.False);
                Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper, 1f), Is.True);
            });

            SetBoundary(
                provider,
                opens: ZLevelBoundaryChannels.All,
                closes: ZLevelBoundaryChannels.Interaction);
            Assert.That(authority.CanInteractThroughOpenBoundary(lower, upper), Is.False);
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
            Assert.Multiple(() =>
            {
                Assert.That(authority.AreOnSameWorldLevel(user, physicalTarget), Is.True);
                Assert.That(authority.AreOnSameWorldLevel(user, remoteTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, physicalTarget), Is.False);
                Assert.That(authority.CanDirectlyInteract(user, remoteTarget), Is.True);
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
