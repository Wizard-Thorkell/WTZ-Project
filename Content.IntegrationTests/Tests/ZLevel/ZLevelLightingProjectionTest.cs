// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Content.Client.ZLevel;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.Graphics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.ZLevel;

public sealed class ZLevelLightingProjectionTest : GameTest
{
    private static readonly ProtoId<ShaderPrototype> ProjectionShader = "ZLevelLightProjection";
    private static readonly ProtoId<ShaderPrototype> HardShadowShader = "ZLevelLightProjectionShadowHard";
    private static readonly ProtoId<ShaderPrototype> SoftShadowShader = "ZLevelLightProjectionShadowSoft";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: ZLevelLightingProjectionTestLight
  components:
  - type: PointLight
    enabled: true
    radius: 4
    energy: 2
    color: '#40A0FFFF'
";

    [Test]
    public async Task ProjectionRequiresCompleteApertureStackAndTracksMovingFrames()
    {
        var testMap = await Pair.CreateTestMap();
        var completelyOpen = new Vector2i(0, 0);
        var lowerBoundaryClosed = new Vector2i(1, 0);
        var upperBoundaryClosed = new Vector2i(2, 0);
        NetEntity lowerLight = default;
        NetEntity middleLight = default;

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            grid.CanSplit = false;
            format.Configure(testMap.MapUid, 0, 2, 0, ZLevelDefaultBoundaryMode.TileAboveCloses);
            transform.SetZLevelFrameOrigin(testMap.Grid, 5);
            transform.SetLocalPosition(testMap.Grid, new Vector2(8f, -3f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(15));

            for (var z = 1; z <= 2; z++)
            {
                for (var y = 0; y <= 1; y++)
                {
                    for (var x = 0; x <= 3; x++)
                    {
                        map.SetZLevelTile(
                            testMap.Grid,
                            grid,
                            new ZLevelTileIndices(x, y, z),
                            new Tile(1));
                    }
                }
            }

            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(completelyOpen.X, completelyOpen.Y, 1), Tile.Empty);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(completelyOpen.X, completelyOpen.Y, 2), Tile.Empty);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(lowerBoundaryClosed.X, lowerBoundaryClosed.Y, 2), Tile.Empty);
            map.SetZLevelTile(testMap.Grid, grid, new ZLevelTileIndices(upperBoundaryClosed.X, upperBoundaryClosed.Y, 1), Tile.Empty);

            var coordinates = new EntityCoordinates(testMap.Grid, completelyOpen + new Vector2(0.5f, 0.5f));
            var lower = SEntMan.SpawnEntity("ZLevelLightingProjectionTestLight", coordinates);
            var middle = SEntMan.SpawnEntity("ZLevelLightingProjectionTestLight", coordinates);
            var active = SEntMan.SpawnEntity("ZLevelLightingProjectionTestLight", coordinates);
            zLevels.SetZLevelPosition(lower, 0);
            zLevels.SetZLevelPosition(middle, 1);
            zLevels.SetZLevelPosition(active, 2);
            lowerLight = SEntMan.GetNetEntity(lower);
            middleLight = SEntMan.GetNetEntity(middle);
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var cache = CEntMan.System<ZLevelLightingCacheSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var visibility = CEntMan.System<SharedZLevelVisibilitySystem>();
            var grid = CEntMan.GetComponent<MapGridComponent>(testMap.CGridUid);
            var lower = CEntMan.GetEntity(lowerLight);
            var middle = CEntMan.GetEntity(middleLight);
            var bounds = Box2.CenteredAround(transform.GetWorldPosition(lower), new Vector2(10f));
            var indexedEmitters = new List<ZLevelLightEmitter>(2);

            Assert.That(cache.QueryEmitters(testMap.MapId, bounds, 3, 6, indexedEmitters), Is.EqualTo(2));
            Assert.That(indexedEmitters.All(emitter => emitter.GridUid == testMap.CGridUid), Is.True);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 7), Is.EqualTo(2));
            Assert.That(projection.Batches.Select(batch => batch.Emitter.Uid),
                Is.EquivalentTo(new[] { lower, middle }));

            var lowerBatch = projection.Batches.Single(batch => batch.Emitter.Uid == lower);
            var middleBatch = projection.Batches.Single(batch => batch.Emitter.Uid == middle);
            Assert.That(cache.TryComposeApertureStack(
                (testMap.CGridUid, grid),
                Vector2i.Zero,
                lowerBatch.TargetLocalZ,
                lowerBatch.ViewerLocalZ,
                out var lowerStack),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(lowerBatch.Depth, Is.EqualTo(2));
                Assert.That(middleBatch.Depth, Is.EqualTo(1));
                Assert.That(lowerBatch.ProjectedRadius, Is.LessThan(middleBatch.ProjectedRadius));
                Assert.That(lowerBatch.Transmission, Is.LessThan(middleBatch.Transmission));

                Assert.That(ContainsTile(projection, lowerBatch, completelyOpen), Is.True);
                Assert.That(ContainsTile(projection, lowerBatch, lowerBoundaryClosed), Is.False);
                Assert.That(ContainsTile(projection, lowerBatch, upperBoundaryClosed), Is.False);

                Assert.That(ContainsTile(projection, middleBatch, completelyOpen), Is.True);
                Assert.That(ContainsTile(projection, middleBatch, lowerBoundaryClosed), Is.True);
                Assert.That(ContainsTile(projection, middleBatch, upperBoundaryClosed), Is.False);

                Assert.That(lowerStack.IsOpen(completelyOpen),
                    Is.EqualTo(visibility.IsTileVisibleFrom(
                        testMap.CGridUid,
                        grid,
                        completelyOpen,
                        7,
                        lowerBatch.TargetLocalZ)));
                Assert.That(lowerStack.IsOpen(lowerBoundaryClosed),
                    Is.EqualTo(visibility.IsTileVisibleFrom(
                        testMap.CGridUid,
                        grid,
                        lowerBoundaryClosed,
                        7,
                        lowerBatch.TargetLocalZ)));
                Assert.That(lowerStack.IsOpen(upperBoundaryClosed),
                    Is.EqualTo(visibility.IsTileVisibleFrom(
                        testMap.CGridUid,
                        grid,
                        upperBoundaryClosed,
                        7,
                        lowerBatch.TargetLocalZ)));
            });
        });

        await Server.WaitAssertion(() =>
        {
            var transform = SEntMan.System<SharedTransformSystem>();
            transform.SetLocalPosition(testMap.Grid, new Vector2(28f, 4f));
            transform.SetLocalRotation(testMap.Grid, Angle.FromDegrees(-35));
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var lower = CEntMan.GetEntity(lowerLight);
            var bounds = Box2.CenteredAround(transform.GetWorldPosition(lower), new Vector2(10f));

            Assert.That(projection.BuildProjection(testMap.MapId, bounds, 7), Is.EqualTo(2));
            foreach (var batch in projection.Batches)
            {
                Assert.That(ContainsTile(projection, batch, completelyOpen), Is.True);
                Assert.That(ContainsTile(projection, batch, upperBoundaryClosed), Is.False);
            }
        });
    }

    [Test]
    public async Task ProjectionShaderAndPrototypeLoad()
    {
        await Client.WaitAssertion(() =>
        {
            var prototypes = Client.ResolveDependency<IPrototypeManager>();

            Assert.Multiple(() =>
            {
                Assert.That(prototypes.Index(ProjectionShader).Instance(), Is.Not.Null);
                Assert.That(prototypes.Index(HardShadowShader).Instance(), Is.Not.Null);
                Assert.That(prototypes.Index(SoftShadowShader).Instance(), Is.Not.Null);
            });
        });
    }

    [TestCase(3)]
    [TestCase(6)]
    [TestCase(10)]
    public async Task ProjectionWorkIsDepthBoundedAndReusesBuffers(int floorCount)
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            format.Configure(testMap.MapUid, 0, floorCount - 1, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);

            for (var z = 0; z < floorCount; z++)
            {
                var light = SEntMan.SpawnEntity(
                    "ZLevelLightingProjectionTestLight",
                    new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
                zLevels.SetZLevelPosition(light, z);
            }
        });
        await Pair.RunTicksSync(3);

        await Client.WaitAssertion(() =>
        {
            var projection = CEntMan.System<ZLevelLightingProjectionSystem>();
            var visibility = CEntMan.System<SharedZLevelVisibilitySystem>();
            var transform = CEntMan.System<SharedTransformSystem>();
            var bounds = BoundsAroundLocalPoint(transform, testMap.CGridUid, new Vector2(0.5f, 0.5f), 8f);
            var viewerWorldZ = floorCount - 1;
            var expectedDepth = Math.Min(floorCount - 1, visibility.MaxVisibleLevelDistance);

            projection.ResetMetrics();
            Assert.That(projection.BuildProjection(testMap.MapId, bounds, viewerWorldZ), Is.EqualTo(expectedDepth));
            Assert.Multiple(() =>
            {
                Assert.That(projection.Batches.Select(batch => batch.Depth),
                    Is.EquivalentTo(Enumerable.Range(1, expectedDepth)));
                Assert.That(projection.Batches.All(batch => batch.TargetLocalZ < batch.ViewerLocalZ), Is.True);
                Assert.That(projection.Batches.All(batch => batch.VisibleTileCount > 0), Is.True);
            });

            BuildRepeated(projection, testMap.MapId, bounds, viewerWorldZ, 512);
            var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            var batches = BuildRepeated(projection, testMap.MapId, bounds, viewerWorldZ, 100);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            var metrics = projection.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(batches, Is.EqualTo(expectedDepth * 100));
                Assert.That(allocated, Is.LessThanOrEqualTo(512),
                    "Projection planning must reuse retained buffers after warm-up.");
                Assert.That(metrics.StackBoundaryLayers,
                    Is.LessThanOrEqualTo(metrics.Frames * expectedDepth * visibility.MaxVisibleLevelDistance * 4));
            });

            TestContext.Progress.WriteLine(
                $"WTZ P3.3 projection scale: floors={floorCount}, depth={expectedDepth}, " +
                $"batches={metrics.CurrentBatches}, runs={metrics.CurrentRuns}, " +
                $"avgMs={metrics.AverageBuildMilliseconds:0.000}, hotBytes={allocated}");
        });
    }

    [Test]
    public void ProjectionGeometryPreservesMaskSpaceAndAttenuationData()
    {
        var gridUid = new EntityUid(1);
        var localCenter = new Vector2(0.5f, 0.5f);
        var gridRotation = Angle.FromDegrees(37f);
        var worldMatrix = Matrix3Helpers.CreateTransform(new Vector2(7f, -3f), gridRotation);
        var worldCenter = Vector2.Transform(localCenter, worldMatrix);
        var emitter = new ZLevelLightEmitter(
            new EntityUid(2),
            gridUid,
            worldCenter,
            0,
            2f,
            2f,
            new Color(0.5f, 0.25f, 1f),
            1f,
            6.8f,
            0.37f,
            true,
            gridRotation,
            null);
        var batch = new ZLevelLightProjectionBatch(
            emitter,
            gridUid,
            0,
            2,
            2,
            MathF.Sqrt(0.75f),
            0.5f,
            0,
            1,
            1,
            -1);
        var runs = new[] { new ZLevelLightProjectionRun(gridUid, 0, 0, 0, 0) };
        var vertices = new List<DrawVertexUV2DColor>();

        var count = ZLevelLightingProjectionGeometry.AppendBatchVertices(
            vertices,
            runs,
            batch,
            1f,
            worldMatrix);

        Assert.Multiple(() =>
        {
            Assert.That(count, Is.EqualTo(6));
            Assert.That(vertices.Select(vertex => vertex.Position), Is.EqualTo(new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(0f, 1f),
            }));
            Assert.That(vertices[0].UV.X, Is.EqualTo(0.375f).Within(0.0001f));
            Assert.That(vertices[0].UV.Y, Is.EqualTo(0.625f).Within(0.0001f));
            Assert.That(vertices[2].UV.X, Is.EqualTo(0.625f).Within(0.0001f));
            Assert.That(vertices[2].UV.Y, Is.EqualTo(0.375f).Within(0.0001f));
            Assert.That(vertices.All(vertex => MathHelper.CloseTo(vertex.UV2.X, 0.8125f)), Is.True);
            Assert.That(vertices.All(vertex => MathHelper.CloseTo(vertex.Color.A, 1f)), Is.True);
        });

        var packed = vertices[0].UV2.Y;
        Assert.Multiple(() =>
        {
            Assert.That(ZLevelLightingProjectionGeometry.UnpackFalloff(packed),
                Is.EqualTo(6.8f).Within(1f / ZLevelLightingProjectionGeometry.FalloffQuantization));
            Assert.That(ZLevelLightingProjectionGeometry.UnpackCurveFactor(packed),
                Is.EqualTo(0.37f).Within(1f / ZLevelLightingProjectionGeometry.CurveQuantization));
        });

        AppendGeometryRepeated(vertices, runs, batch, worldMatrix, 512);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        AppendGeometryRepeated(vertices, runs, batch, worldMatrix, 100);
        Assert.That(
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
            Is.LessThanOrEqualTo(512),
            "Projection geometry must reuse its caller-owned vertex buffer after warm-up.");
    }

    private static Box2 BoundsAroundLocalPoint(
        SharedTransformSystem transform,
        EntityUid gridUid,
        Vector2 localPoint,
        float size)
    {
        var (_, _, worldMatrix, _) = transform.GetWorldPositionRotationMatrixWithInv(gridUid);
        var worldPoint = Vector2.Transform(localPoint, worldMatrix);
        return Box2.CenteredAround(worldPoint, new Vector2(size));
    }

    private static bool ContainsTile(
        ZLevelLightingProjectionSystem projection,
        in ZLevelLightProjectionBatch batch,
        Vector2i tile)
    {
        for (var i = 0; i < batch.RunCount; i++)
        {
            if (projection.Runs[batch.FirstRun + i].Contains(tile))
                return true;
        }

        return false;
    }

    private static int BuildRepeated(
        ZLevelLightingProjectionSystem projection,
        MapId mapId,
        Box2 bounds,
        int viewerWorldZ,
        int count)
    {
        var batches = 0;
        for (var i = 0; i < count; i++)
        {
            projection.BeginBudgetFrameForTesting();
            batches += projection.BuildProjection(mapId, bounds, viewerWorldZ);
        }

        return batches;
    }

    private static void AppendGeometryRepeated(
        List<DrawVertexUV2DColor> vertices,
        IReadOnlyList<ZLevelLightProjectionRun> runs,
        in ZLevelLightProjectionBatch batch,
        in Matrix3x2 worldMatrix,
        int count)
    {
        for (var i = 0; i < count; i++)
        {
            vertices.Clear();
            ZLevelLightingProjectionGeometry.AppendBatchVertices(
                vertices,
                runs,
                batch,
                1f,
                worldMatrix);
        }
    }
}
