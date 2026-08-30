// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests.ZLevel;

/// <summary>
/// Foundational compatibility contracts for ordinary planar maps and entities.
/// This suite protects Z 0 behavior; it does not migrate legacy maps into
/// multi-floor maps.
/// </summary>
public sealed class ZLevelZZeroCompatibilityTest : GameTest
{
    [Test]
    public async Task UnconfiguredBaseMapStaysComponentFreeAndVerticalSystemsRemainPassive()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var gravity = SEntMan.System<SharedZLevelGravitySystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);

            map.SetTile(testMap.Grid, grid, Vector2i.Zero, new Tile(1));
            var marker = SEntMan.SpawnEntity(
                null,
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            var markerTransform = SEntMan.GetComponent<TransformComponent>(marker);
            var cachedGridCount = gravity.CachedGridCount;

            var baseTile = map.GetTileRef(testMap.Grid, grid, Vector2i.Zero);
            var explicitBaseTile = map.GetZLevelTileRef(
                testMap.Grid,
                grid,
                new ZLevelTileIndices(0, 0, 0));

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelMapComponent>(testMap.MapUid), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(marker), Is.False);
                Assert.That(format.TryGetConfig(testMap.Grid, out _), Is.False);
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
                Assert.That(transform.GetZLevel((marker, markerTransform, null)), Is.Zero);
                Assert.That(transform.GetWorldZLevel((marker, markerTransform, null)), Is.Zero);
                Assert.That(gravity.IsManagedGrid(testMap.Grid), Is.False);
                Assert.That(
                    gravity.TryGetGravityTarget(testMap.Grid, grid, Vector2i.Zero, 0f, out _),
                    Is.False);
                Assert.That(gravity.CachedGridCount, Is.EqualTo(cachedGridCount));
                Assert.That(explicitBaseTile.Tile, Is.EqualTo(baseTile.Tile));
            });
        });
    }

    [Test]
    public async Task StampingBackToWorldZZeroRemovesExplicitPosition()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var format = SEntMan.System<SharedZLevelMapSystem>();
            var transform = SEntMan.System<SharedTransformSystem>();
            var zLevels = SEntMan.System<SharedZLevelSystem>();
            format.Configure(
                testMap.MapUid,
                -1,
                1,
                0,
                ZLevelDefaultBoundaryMode.TileAboveCloses);

            var marker = SEntMan.SpawnEntity(
                null,
                new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f)));
            Assert.That(zLevels.StampWorldZLevelPosition(marker, 1), Is.True);
            Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(marker), Is.True);
            Assert.That(transform.GetWorldZLevel(marker), Is.EqualTo(1));

            Assert.That(zLevels.StampWorldZLevelPosition(marker, 0), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(marker), Is.False);
                Assert.That(transform.GetZLevel(marker), Is.Zero);
                Assert.That(transform.GetWorldZLevel(marker), Is.Zero);
                Assert.That(format.TryValidate(testMap.MapUid, out var error), Is.True, error);
            });
        });
    }

    [Test]
    public async Task ComponentFreeZZeroEntitiesRemainPlanarVisible()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitAssertion(() =>
        {
            var map = SEntMan.System<SharedMapSystem>();
            var visibility = SEntMan.System<SharedZLevelVisibilitySystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(testMap.Grid);
            var coordinates = new EntityCoordinates(testMap.Grid, new Vector2(0.5f, 0.5f));

            map.SetTile(testMap.Grid, grid, Vector2i.Zero, new Tile(1));
            var candidate = SEntMan.SpawnEntity(null, coordinates);

            Assert.Multiple(() =>
            {
                Assert.That(visibility.IsEntityVisibleFrom(candidate, testMap.MapId, 0), Is.True);
                Assert.That(visibility.IsCoordinateVisibleFrom(coordinates, 0, testMap.MapId, 0), Is.True);
                Assert.That(SEntMan.HasComponent<ZLevelMapComponent>(testMap.MapUid), Is.False);
                Assert.That(SEntMan.HasComponent<ZLevelPositionComponent>(candidate), Is.False);
            });
        });
    }
}
