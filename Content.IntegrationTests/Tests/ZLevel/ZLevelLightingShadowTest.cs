// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.CCVar;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelLightingShadowTest : GameTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelLightingShadowTestLight
  components:
  - type: PointLight
    enabled: true
    radius: 4
    energy: 2
    softness: 1.25
    castShadows: true

- type: entity
  id: ZLevelLightingShadowTestUnshadowedLight
  components:
  - type: PointLight
    enabled: true
    radius: 4
    energy: 2
    castShadows: false
";

    [TestCase(1, 1)]
    [TestCase(3, 4)]
    [TestCase(64, 64)]
    [TestCase(65, 128)]
    [TestCase(1_024, 1_024)]
    public void ShadowAtlasCapacityRoundsUpWithinHardLimit(int requiredRows, int expected)
    {
        Assert.That(
            ZLevelLightingProjectionOverlay.GetShadowAtlasCapacity(requiredRows),
            Is.EqualTo(expected));
    }

    [TestCase(0)]
    [TestCase(1_025)]
    public void ShadowAtlasCapacityRejectsInvalidLimits(int requiredRows)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ZLevelLightingProjectionOverlay.GetShadowAtlasCapacity(requiredRows));
    }

    [Test]
    public async Task ShadowRowsFollowNearestFloorOrderAndStayContiguous()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, 8);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, 8);
        var testMap = await CreateOpenLightingMap(
            3,
            (1, true),
            (2, true),
            (2, true),
            (0, true));

        await Client.WaitAssertion(() =>
        {
            EnableClientShadows();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var bounds = ProjectionBounds(testMap, 8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 3), Is.EqualTo(4));
            var metrics = projection.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(projection.ShadowRequests.Select(request => request.WorldZ),
                    Is.EqualTo(new[] { 2, 2, 1, 0 }));
                Assert.That(projection.Batches.Select(batch => batch.ShadowRow),
                    Is.EqualTo(new[] { 0, 1, 2, 3 }));
                Assert.That(projection.ShadowRequests.All(request => request.Radius == 4f), Is.True);
                Assert.That(metrics.CurrentShadowRequests, Is.EqualTo(4));
                Assert.That(metrics.CurrentShadowFloorGroups, Is.EqualTo(3));
                Assert.That(metrics.CurrentShadowLightsUsed, Is.EqualTo(4));
                Assert.That(metrics.CurrentShadowFloorGroupsUsed, Is.EqualTo(3));
                Assert.That(metrics.CurrentShadowFallbacks, Is.Zero);
            });

            for (var i = 0; i < projection.ShadowRequests.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(projection.ShadowRequests[i].WorldPosition,
                        Is.EqualTo(projection.Batches[i].Emitter.WorldPosition));
                    Assert.That(projection.ShadowRequests[i].Radius,
                        Is.EqualTo(projection.Batches[i].Emitter.Radius));
                });
            }
        });
    }

    [Test]
    public async Task NonShadowCasterDoesNotConsumeShadowBudget()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, 1);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, 1);
        var testMap = await CreateOpenLightingMap(2, (1, false), (0, true));

        await Client.WaitAssertion(() =>
        {
            EnableClientShadows();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, ProjectionBounds(testMap, 8f), 2),
                Is.EqualTo(2));
            var metrics = projection.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches[0].Emitter.CastShadows, Is.False);
                Assert.That(projection.Batches[0].HasShadow, Is.False);
                Assert.That(projection.Batches[1].Emitter.CastShadows, Is.True);
                Assert.That(projection.Batches[1].ShadowRow, Is.Zero);
                Assert.That(projection.ShadowRequests.Single().WorldZ, Is.Zero);
                Assert.That(metrics.CurrentShadowLightsUsed, Is.EqualTo(1));
                Assert.That(metrics.CurrentShadowFloorGroupsUsed, Is.EqualTo(1));
                Assert.That(metrics.ShadowFallbacks, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ShadowLightLimitFallsBackWithoutDroppingProjection()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, 1);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, 8);
        var testMap = await CreateOpenLightingMap(2, (1, true), (0, true));

        await Client.WaitAssertion(() =>
        {
            EnableClientShadows();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, ProjectionBounds(testMap, 8f), 2),
                Is.EqualTo(2));
            var metrics = projection.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(2));
                Assert.That(projection.Batches.All(batch => batch.RunCount > 0), Is.True);
                Assert.That(projection.Batches[0].ShadowRow, Is.Zero);
                Assert.That(projection.Batches[1].HasShadow, Is.False);
                Assert.That(projection.ShadowRequests, Has.Count.EqualTo(1));
                Assert.That(metrics.CurrentShadowFallbacks, Is.EqualTo(1));
                Assert.That(metrics.ShadowLightBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.ShadowFloorGroupBudgetExhaustions, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ShadowFloorGroupLimitFallsBackWithoutDroppingProjection()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, 8);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, 1);
        var testMap = await CreateOpenLightingMap(3, (2, true), (1, true), (0, true));

        await Client.WaitAssertion(() =>
        {
            EnableClientShadows();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, ProjectionBounds(testMap, 8f), 3),
                Is.EqualTo(3));
            var metrics = projection.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches, Has.Count.EqualTo(3));
                Assert.That(projection.Batches.Select(batch => batch.HasShadow),
                    Is.EqualTo(new[] { true, false, false }));
                Assert.That(projection.ShadowRequests.Single().WorldZ, Is.EqualTo(2));
                Assert.That(metrics.CurrentShadowFallbacks, Is.EqualTo(2));
                Assert.That(metrics.CurrentShadowLightsUsed, Is.EqualTo(1));
                Assert.That(metrics.CurrentShadowFloorGroupsUsed, Is.EqualTo(1));
                Assert.That(metrics.ShadowFloorGroupBudgetExhaustions, Is.EqualTo(1));
                Assert.That(metrics.ShadowLightBudgetExhaustions, Is.Zero);
            });
        });
    }

    [Test]
    public async Task ShadowBudgetsAreSharedWithinFrameAndResetForNextFrame()
    {
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowLightsPerFrame, 1);
        await OverrideCVar(Side.Client, CCVars.ZLevelLightingMaxShadowFloorGroupsPerFrame, 2);
        var testMap = await CreateOpenLightingMap(1, (0, true));

        await Client.WaitAssertion(() =>
        {
            EnableClientShadows();
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var bounds = ProjectionBounds(testMap, 8f);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.EqualTo(1));
            Assert.That(projection.Batches.Single().HasShadow, Is.True);
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.EqualTo(1));
            Assert.That(projection.Batches.Single().HasShadow, Is.False);
            Assert.That(projection.ShadowRequests, Is.Empty);

            var exhausted = projection.Snapshot();
            Assert.Multiple(() =>
            {
                Assert.That(exhausted.CurrentShadowLightsUsed, Is.EqualTo(1));
                Assert.That(exhausted.CurrentShadowFallbacks, Is.EqualTo(1));
                Assert.That(exhausted.ShadowLightBudgetExhaustions, Is.EqualTo(1));
            });

            projection.BeginBudgetFrameForTesting();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 1), Is.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches.Single().ShadowRow, Is.Zero);
                Assert.That(projection.ShadowRequests, Has.Count.EqualTo(1));
                Assert.That(projection.Snapshot().ShadowLightBudgetExhaustions, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task GlobalShadowToggleSkipsAtlasWorkWithoutBudgetFailure()
    {
        var testMap = await CreateOpenLightingMap(1, (0, true));

        await Client.WaitAssertion(() =>
        {
            var lightManager = Client.ResolveDependency<ILightManager>();
            var previous = lightManager.DrawShadows;
            try
            {
                lightManager.DrawShadows = false;
                var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
                projection.ResetMetrics();

                Assert.That(projection.BuildProjection(testMap.MapId, ProjectionBounds(testMap, 8f), 1),
                    Is.EqualTo(1));
                var metrics = projection.Snapshot();
                Assert.Multiple(() =>
                {
                    Assert.That(projection.Batches.Single().HasShadow, Is.False);
                    Assert.That(projection.ShadowRequests, Is.Empty);
                    Assert.That(metrics.CurrentShadowLightsUsed, Is.Zero);
                    Assert.That(metrics.CurrentShadowFloorGroupsUsed, Is.Zero);
                    Assert.That(metrics.ShadowFallbacks, Is.Zero);
                    Assert.That(metrics.ShadowLightBudgetExhaustions, Is.Zero);
                    Assert.That(metrics.ShadowFloorGroupBudgetExhaustions, Is.Zero);
                });
            }
            finally
            {
                lightManager.DrawShadows = previous;
            }
        });
    }

    private async Task<TestMapData> CreateOpenLightingMap(
        int viewerLocalZ,
        params (int LocalZ, bool CastShadows)[] sources)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            format.Configure(
                testMap.MapUid,
                0,
                viewerLocalZ,
                0,
                ZLevelDefaultBoundaryMode.ExplicitOnly);

            for (var i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                var prototype = source.CastShadows
                    ? "ZLevelLightingShadowTestLight"
                    : "ZLevelLightingShadowTestUnshadowedLight";
                var light = SEntMan.SpawnEntity(
                    prototype,
                    new EntityCoordinates(testMap.Grid, new Vector2(0.5f + i * 0.1f, 0.5f)));
                zLevels.SetZLevelPosition(light, source.LocalZ);
            }
        });
        await Pair.RunTicksSync(3);
        return testMap;
    }

    private Box2 ProjectionBounds(TestMapData testMap, float size)
    {
        var transform = CEntMan.System<SharedTransformSystem>();
        var (_, _, worldMatrix, _) = transform.GetWorldPositionRotationMatrixWithInv(testMap.CGridUid);
        var center = Vector2.Transform(new Vector2(0.65f, 0.5f), worldMatrix);
        return Box2.CenteredAround(center, new Vector2(size));
    }

    private void EnableClientShadows()
    {
        Client.ResolveDependency<ILightManager>().DrawShadows = true;
    }
}
