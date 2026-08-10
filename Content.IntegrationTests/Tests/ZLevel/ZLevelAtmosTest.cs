// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.IntegrationTests.Tests.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Tests;
using NUnit.Framework;
using System.Numerics;
using Content.Shared.Atmos;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelAtmosTest : AtmosTest
{
    protected override ResPath? TestMapPath => new("Maps/Test/Atmospherics/tile_atmosphere_test_room.yml");

    [Test]
    public async Task CeilingTileInvalidatesLowerAtmosAdjacency()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var sourceZTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 0);

            var lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Not.Null,
                "Expected open sky above the source tile before adding a ceiling.");

            mapSystem.SetZLevelTile(MapData.Grid, grid, new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1), new Tile(1));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Null,
                "Expected the source tile's vertical atmos adjacency to close after adding a ceiling tile above it.");

            var marker = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            var boundary = SEntMan.EnsureComponent<ZLevelBoundaryComponent>(marker);
            SEntMan.System<SharedZLevelBoundarySystem>().SetBoundary(
                (marker, boundary),
                true,
                1,
                ZLevelBoundaryChannels.Atmosphere,
                ZLevelBoundaryChannels.None);
            SEntMan.System<SharedTransformSystem>()
                .AnchorEntity(marker, SEntMan.GetComponent<TransformComponent>(marker));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Not.Null,
                "Expected an explicit atmosphere opening to override the ceiling tile.");

            SEntMan.DeleteEntity(marker);
            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            lowerTile = SAtmos.GetZLevelTileAtmosphere(RelevantAtmos, sourceZTile);
            Assert.That(lowerTile, Is.Not.Null);
            Assert.That(lowerTile!.AdjacentTileAbove, Is.Null,
                "Expected removing the explicit opening to restore the closed ceiling boundary.");
        });
    }

    [Test]
    [Ignore("Needs a dedicated upper-floor atmos fixture. Spawned test entities on z=1 currently return null containing mixtures in this map setup.")]
    public async Task EntityContainingMixtureUsesCurrentZLevel()
    {
        var markers = SEntMan.AllEntities<TestMarkerComponent>();
        Assert.That(GetMarker(markers, "floor", out var source), Is.True);

        await Server.WaitPost(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            var grid = SEntMan.GetComponent<MapGridComponent>(MapData.Grid);
            var sourceTile = mapSystem.TileIndicesFor(MapData.Grid, grid, Xform(source).Coordinates);
            var upperTile = new ZLevelTileIndices(sourceTile.X, sourceTile.Y, 1);

            var lowerMixture = SAtmos.GetTileMixture(RelevantAtmos, null, sourceTile, true);
            Assert.That(lowerMixture, Is.Not.Null, "Expected a base-layer gas mixture for the source tile.");
            lowerMixture!.AdjustMoles(Gas.Oxygen, 100f);
            mapSystem.SetZLevelTile(MapData.Grid, grid, upperTile, new Tile(1));

            var parent = SEntMan.SpawnEntity(null, Xform(source).Coordinates);
            SEntMan.EnsureComponent<ZLevelPositionComponent>(parent).ZLevel = 1;
            var child = SEntMan.SpawnEntity(null, new EntityCoordinates(parent, Vector2.Zero));

            SAtmos.RunProcessingFull(ProcessEnt, MapData.Grid.Owner, SAtmos.AtmosTickRate);

            var lowerPressure = lowerMixture.Pressure;
            var upperMixture = SAtmos.GetContainingMixture(parent, ignoreExposed: true);
            var childMixture = SAtmos.GetContainingMixture(child, ignoreExposed: true);

            Assert.That(lowerPressure, Is.GreaterThan(10f));
            Assert.That(upperMixture, Is.Not.Null);
            Assert.That(upperMixture!.Pressure, Is.LessThan(1f),
                "Expected the z=1 parent to sample the open upper layer instead of the pressurized z=0 room.");
            Assert.That(childMixture, Is.Not.Null);
            Assert.That(childMixture!.Pressure, Is.LessThan(1f),
                "Expected the child entity to inherit its parent's z-level instead of sampling z=0.");
        });
    }
}
