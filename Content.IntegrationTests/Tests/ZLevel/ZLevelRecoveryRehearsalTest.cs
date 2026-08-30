// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Mapping;
using Content.Server.ZLevel.Operations;
using Content.Shared.CCVar;
using Content.Shared.Mapping;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.ContentPack;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.ZLevel;

[TestFixture]
public sealed class ZLevelRecoveryRehearsalTest : GameTest
{
    private const string ContractVersion = "WTZ-RECOVERY-1";
    private const string ExpectedTest =
        "Content.IntegrationTests.Tests.ZLevel.ZLevelRecoveryRehearsalTest." +
        nameof(ValidatedCheckpointRejectsCorruptionAndRecoversTwice);
    private const string OutputDirectoryEnvironmentVariable = "WTZ_ZLEVEL_RECOVERY_DIR";

    [Test]
    [EnsureCVar(Side.Server, typeof(CCVars), nameof(CCVars.AutosaveEnabled), false)]
    public async Task ValidatedCheckpointRejectsCorruptionAndRecoversTwice()
    {
        var server = Pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var mapManager = server.ResolveDependency<IMapManager>();
        var resources = server.ResolveDependency<IResourceManager>();
        var mapSystem = entMan.System<SharedMapSystem>();
        var transform = entMan.System<SharedTransformSystem>();
        var format = entMan.System<SharedZLevelMapSystem>();
        var zLevel = entMan.System<SharedZLevelSystem>();
        var mapping = entMan.System<MappingSystem>();
        var snapshots = entMan.System<MappingSnapshotSystem>();
        var operations = entMan.System<ZLevelOperationalHealthSystem>();
        var loader = entMan.System<MapLoaderSystem>();
        var initialName = $"zlevel-recovery-initial-{Guid.NewGuid():N}";
        var recoveredName = $"zlevel-recovery-restored-{Guid.NewGuid():N}";
        var initialDirectory = new ResPath("/Autosaves") / initialName;
        var recoveredDirectory = new ResPath("/Autosaves") / recoveredName;

        MapId sourceMapId = default;
        EntityUid sourceMapUid = default;
        EntityUid opening = default;
        RecoveryMapFingerprint sourceFingerprint = default!;
        ResPath initialPath = default;
        ResPath recoveredPath = default;
        byte[] initialBytes = default!;
        MappingAutosaveMetricsSnapshot initialMetrics = default;
        MappingAutosaveMetricsSnapshot failedMetrics = default;
        MappingAutosaveMetricsSnapshot recoveredMetrics = default;

        await server.WaitAssertion(() =>
        {
            resources.UserData.Delete(initialDirectory);
            resources.UserData.Delete(recoveredDirectory);
            mapping.ResetAutosaveMetrics();

            sourceMapUid = mapSystem.CreateMap(out sourceMapId, runMapInit: false);
            format.Configure(sourceMapUid, 0, 2, 0, ZLevelDefaultBoundaryMode.ExplicitOnly);
            var grid = mapManager.CreateGridEntity(sourceMapId);
            grid.Comp.CanSplit = false;
            mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
            mapSystem.SetZLevelTile(
                grid.Owner,
                grid.Comp,
                new ZLevelTileIndices(0, 0, 1),
                new Tile(1));
            mapSystem.SetZLevelTile(
                grid.Owner,
                grid.Comp,
                new ZLevelTileIndices(1, 0, 2),
                new Tile(1));

            opening = entMan.SpawnEntity(
                "ZLevelFloorOpeningMarker",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            Assert.That(zLevel.SetZLevelPosition(opening, 1), Is.True);
            var openingTransform = entMan.GetComponent<TransformComponent>(opening);
            if (!openingTransform.Anchored)
                Assert.That(transform.AnchorEntity((opening, openingTransform), grid), Is.True);

            var actor = entMan.SpawnEntity(
                "Crowbar",
                new EntityCoordinates(grid.Owner, new Vector2(1.5f, 0.5f)));
            entMan.EnsureComponent<ActorComponent>(actor);
            Assert.That(zLevel.SetZLevelPosition(actor, 2), Is.True);

            var transient = entMan.SpawnEntity(
                "Wirecutter",
                new EntityCoordinates(grid.Owner, new Vector2(0.5f, 0.5f)));
            entMan.EnsureComponent<MappingSnapshotTransientComponent>(transient);

            Assert.That(mapping.TryCreateCheckpointNow(
                    sourceMapUid,
                    initialName,
                    out var uninitializedPath,
                    out _,
                    out var uninitializedError),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(uninitializedPath, Is.EqualTo(default(ResPath)));
                Assert.That(uninitializedError, Does.Contain("MapInitialized"));
                Assert.That(resources.UserData.Exists(initialDirectory), Is.False);
            });

            mapSystem.InitializeMap(sourceMapId);
            Assert.That(mapping.TryCreateCheckpointNow(
                    grid.Owner,
                    initialName,
                    out var gridPath,
                    out _,
                    out var gridError),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(gridPath, Is.EqualTo(default(ResPath)));
                Assert.That(gridError, Does.Contain("complete initialized map root"));
                Assert.That(resources.UserData.Exists(initialDirectory), Is.False);
            });

            mapping.ResetAutosaveMetrics();
            Assert.That(format.TryValidate(sourceMapUid, out var error), Is.True, error);
            sourceFingerprint = CaptureFingerprint(
                entMan,
                mapManager,
                mapSystem,
                transform,
                snapshots,
                sourceMapUid);
        });

        await server.ExecuteCommand($"zlevelcheckpoint {sourceMapId} {initialName}");
        await server.WaitAssertion(() =>
        {
            initialMetrics = mapping.SnapshotAutosaveMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(initialMetrics.Attempts, Is.EqualTo(1));
                Assert.That(initialMetrics.Successes, Is.EqualTo(1));
                Assert.That(initialMetrics.Failures, Is.Zero);
                Assert.That(initialMetrics.LastAttemptSucceeded, Is.True);
                Assert.That(initialMetrics.LastPath, Is.Not.Null);
                Assert.That(initialMetrics.LastExcludedRoots, Is.EqualTo(2));
                Assert.That(initialMetrics.LastValidatedEntities, Is.GreaterThan(0));
            });

            initialPath = new ResPath(initialMetrics.LastPath!);
            initialBytes = resources.UserData.ReadAllBytes(initialPath);
            Assert.Multiple(() =>
            {
                Assert.That(initialPath.Directory, Is.EqualTo(initialDirectory));
                Assert.That(initialPath.Filename, Does.Contain("-CHECKPOINT"));
                Assert.That(resources.UserData.DirectoryEntries(initialDirectory), Has.Exactly(1).Items);
                Assert.That(resources.UserData.DirectoryEntries(initialDirectory), Has.None.EndsWith(".tmp"));
            });

            entMan.GetComponent<ZLevelPositionComponent>(opening).ZLevel = 3;
            Assert.That(format.TryValidate(sourceMapUid, out var error), Is.False);
            Assert.That(error, Does.Contain("outside the declared range"));
        });

        await server.WaitAssertion(() =>
        {
            Assert.That(mapping.TryCreateCheckpointNow(
                    sourceMapUid,
                    initialName,
                    out var rejectedPath,
                    out _,
                    out var rejectedError),
                Is.False);
            failedMetrics = mapping.SnapshotAutosaveMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(rejectedPath, Is.EqualTo(default(ResPath)));
                Assert.That(rejectedError, Does.Contain("outside the declared range"));
                Assert.That(failedMetrics.Attempts, Is.EqualTo(2));
                Assert.That(failedMetrics.Successes, Is.EqualTo(1));
                Assert.That(failedMetrics.Failures, Is.EqualTo(1));
                Assert.That(failedMetrics.LastAttemptSucceeded, Is.False);
                Assert.That(failedMetrics.LastError, Does.Contain("outside the declared range"));
                Assert.That(resources.UserData.ReadAllBytes(initialPath), Is.EqualTo(initialBytes),
                    "A rejected checkpoint must preserve the last known-good bytes.");
                Assert.That(resources.UserData.DirectoryEntries(initialDirectory), Has.Exactly(1).Items);
                Assert.That(resources.UserData.DirectoryEntries(initialDirectory), Has.None.EndsWith(".tmp"));
            });

            var failedHealth = operations.Capture();
            Assert.That(failedHealth.Status, Is.EqualTo(ZLevelOperationalHealthStatus.Critical));
            Assert.That(failedHealth.Findings.Select(finding => finding.Code),
                Does.Contain("autosave.last-attempt-failed"));

            mapSystem.DeleteMap(sourceMapId);
            Assert.That(entMan.EntityExists(sourceMapUid), Is.False);
        });

        Entity<MapComponent> firstRecoveredMap = default;
        HashSet<Entity<MapGridComponent>> firstRecoveredGrids = default!;
        RecoveryMapFingerprint firstRecoveredFingerprint = default!;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(initialPath, out var map, out var grids), Is.True);
            firstRecoveredMap = map!.Value;
            firstRecoveredGrids = grids!;
            firstRecoveredFingerprint = CaptureFingerprint(
                entMan,
                mapManager,
                mapSystem,
                transform,
                snapshots,
                firstRecoveredMap.Owner);

            AssertFingerprint(sourceFingerprint, firstRecoveredFingerprint, "first recovery load");
            AssertRecoveredMap(entMan, mapSystem, format, firstRecoveredMap, firstRecoveredGrids);
        });

        await server.ExecuteCommand(
            $"zlevelcheckpoint {firstRecoveredMap.Comp.MapId} {recoveredName}");
        await server.WaitAssertion(() =>
        {
            recoveredMetrics = mapping.SnapshotAutosaveMetrics();
            Assert.Multiple(() =>
            {
                Assert.That(recoveredMetrics.Attempts, Is.EqualTo(3));
                Assert.That(recoveredMetrics.Successes, Is.EqualTo(2));
                Assert.That(recoveredMetrics.Failures, Is.EqualTo(1));
                Assert.That(recoveredMetrics.LastAttemptSucceeded, Is.True);
                Assert.That(recoveredMetrics.LastPath, Is.Not.Null);
                Assert.That(recoveredMetrics.LastExcludedRoots, Is.Zero);
                Assert.That(recoveredMetrics.LastValidatedEntities,
                    Is.EqualTo(initialMetrics.LastValidatedEntities));
            });

            recoveredPath = new ResPath(recoveredMetrics.LastPath!);
            Assert.Multiple(() =>
            {
                Assert.That(recoveredPath, Is.Not.EqualTo(initialPath));
                Assert.That(recoveredPath.Directory, Is.EqualTo(recoveredDirectory));
                Assert.That(recoveredPath.Filename, Does.Contain("-CHECKPOINT"));
                Assert.That(resources.UserData.DirectoryEntries(recoveredDirectory), Has.Exactly(1).Items);
                Assert.That(resources.UserData.DirectoryEntries(recoveredDirectory), Has.None.EndsWith(".tmp"));
            });
        });

        Entity<MapComponent> secondRecoveredMap = default;
        HashSet<Entity<MapGridComponent>> secondRecoveredGrids = default!;
        RecoveryMapFingerprint secondRecoveredFingerprint = default!;
        ZLevelOperationalHealthReport finalHealth = default!;
        await server.WaitAssertion(() =>
        {
            Assert.That(loader.TryLoadMap(recoveredPath, out var map, out var grids), Is.True);
            secondRecoveredMap = map!.Value;
            secondRecoveredGrids = grids!;
            secondRecoveredFingerprint = CaptureFingerprint(
                entMan,
                mapManager,
                mapSystem,
                transform,
                snapshots,
                secondRecoveredMap.Owner);

            AssertFingerprint(sourceFingerprint, secondRecoveredFingerprint, "second recovery load");
            AssertFingerprint(firstRecoveredFingerprint, secondRecoveredFingerprint, "recovered round trip");
            AssertRecoveredMap(entMan, mapSystem, format, secondRecoveredMap, secondRecoveredGrids);

            finalHealth = operations.Capture();
            Assert.That(finalHealth.Findings.Any(finding =>
                finding.Severity == ZLevelOperationalFindingSeverity.Critical), Is.False);
        });

        var report = new ZLevelRecoveryRehearsalReport(
            1,
            ContractVersion,
            GetEnvironment("WTZ_ZLEVEL_RECOVERY_STATUS", "DevelopmentPassed"),
            DateTimeOffset.UtcNow,
            ExpectedTest,
            new ZLevelRecoverySourceSnapshot(
                GetEnvironment("WTZ_ZLEVEL_RECOVERY_PROJECT_REVISION", "unbound"),
                GetEnvironment("WTZ_ZLEVEL_RECOVERY_ENGINE_REVISION", "unbound"),
                GetEnvironment("WTZ_ZLEVEL_RECOVERY_GITLINK_REVISION", "unbound"),
                GetEnvironmentBool("WTZ_ZLEVEL_RECOVERY_PROJECT_CLEAN"),
                GetEnvironmentBool("WTZ_ZLEVEL_RECOVERY_ENGINE_CLEAN")),
            new ZLevelRecoveryScenarioSnapshot(
                3,
                firstRecoveredGrids.Count,
                initialMetrics.LastValidatedEntities,
                initialMetrics.LastExcludedRoots,
                recoveredMetrics.LastValidatedEntities,
                recoveredMetrics.LastExcludedRoots,
                failedMetrics.LastError ?? string.Empty,
                initialPath.ToString(),
                recoveredPath.ToString(),
                Sha256(initialBytes),
                Sha256(resources.UserData.ReadAllBytes(recoveredPath)),
                recoveredMetrics.Attempts,
                recoveredMetrics.Successes,
                recoveredMetrics.Failures,
                finalHealth.Status.ToString()),
            new ZLevelRecoveryStepSnapshot(
                InitialCheckpointCreated: true,
                InvalidCheckpointRejected: true,
                KnownGoodBytesPreserved: true,
                CorruptSourceRemoved: true,
                InitialCheckpointLoaded: true,
                RecoveredCheckpointCreated: true,
                RecoveredCheckpointLoaded: true,
                StructuralStateMatched: true,
                NoCriticalHealthFinding: true,
                TemporaryFilesRemaining: 0));
        WriteReport(report);

        await server.WaitPost(() =>
        {
            mapSystem.DeleteMap(firstRecoveredMap.Comp.MapId);
            mapSystem.DeleteMap(secondRecoveredMap.Comp.MapId);
            resources.UserData.Delete(initialDirectory);
            resources.UserData.Delete(recoveredDirectory);
            mapping.ResetAutosaveMetrics();
        });
    }

    private static RecoveryMapFingerprint CaptureFingerprint(
        IEntityManager entMan,
        IMapManager mapManager,
        SharedMapSystem mapSystem,
        SharedTransformSystem transform,
        MappingSnapshotSystem snapshots,
        EntityUid mapUid)
    {
        var map = entMan.GetComponent<MapComponent>(mapUid);
        var config = entMan.GetComponent<ZLevelMapComponent>(mapUid);
        var grids = mapManager.GetAllGrids(map.MapId)
            .OrderBy(grid => grid.Owner.Id)
            .ToArray();
        var gridIndices = grids
            .Select((grid, index) => (grid.Owner, index))
            .ToDictionary(entry => entry.Owner, entry => entry.index);
        var gridSignatures = grids
            .Select(grid =>
            {
                var xform = entMan.GetComponent<TransformComponent>(grid.Owner);
                var origin = entMan.GetComponentOrNull<ZLevelFrameComponent>(grid.Owner)?.Origin ?? 0;
                return string.Join(
                    "|",
                    Canonical(xform.LocalPosition.X),
                    Canonical(xform.LocalPosition.Y),
                    Canonical(xform.LocalRotation.Theta),
                    origin,
                    grid.Comp.TileSize,
                    grid.Comp.CanSplit);
            })
            .ToArray();
        var tiles = grids
            .SelectMany(grid => mapSystem.GetAllNonEmptyZLevelTiles(grid.Owner, grid.Comp)
                .Select(tile => string.Join(
                    "|",
                    gridIndices[grid.Owner],
                    tile.X,
                    tile.Y,
                    tile.Z,
                    tile.Tile.TypeId,
                    tile.Tile.Flags,
                    tile.Tile.Variant,
                    tile.Tile.RotationMirroring)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var entities = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
            .Select(entry => entry.Uid)
            .Where(uid =>
            {
                var xform = entMan.GetComponent<TransformComponent>(uid);
                return xform.MapUid == mapUid &&
                       xform.GridUid != null &&
                       entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID != null &&
                       snapshots.IsPersistentSnapshotEntity(uid, mapUid);
            })
            .Select(uid =>
            {
                var metadata = entMan.GetComponent<MetaDataComponent>(uid);
                var xform = entMan.GetComponent<TransformComponent>(uid);
                var level = transform.GetZLevel((
                    uid,
                    xform,
                    entMan.GetComponentOrNull<ZLevelPositionComponent>(uid)));
                return string.Join(
                    "|",
                    metadata.EntityPrototype!.ID,
                    gridIndices[xform.GridUid!.Value],
                    Canonical(xform.LocalPosition.X),
                    Canonical(xform.LocalPosition.Y),
                    Canonical(xform.LocalRotation.Theta),
                    level,
                    xform.Anchored);
            })
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new RecoveryMapFingerprint(
            config.FormatVersion,
            config.MinimumLevel,
            config.MaximumLevel,
            config.DefaultLevel,
            config.DefaultBoundaryMode,
            gridSignatures,
            tiles,
            entities);
    }

    private static void AssertFingerprint(
        RecoveryMapFingerprint expected,
        RecoveryMapFingerprint actual,
        string context)
    {
        Assert.Multiple(() =>
        {
            Assert.That(actual.FormatVersion, Is.EqualTo(expected.FormatVersion), context);
            Assert.That(actual.MinimumLevel, Is.EqualTo(expected.MinimumLevel), context);
            Assert.That(actual.MaximumLevel, Is.EqualTo(expected.MaximumLevel), context);
            Assert.That(actual.DefaultLevel, Is.EqualTo(expected.DefaultLevel), context);
            Assert.That(actual.DefaultBoundaryMode, Is.EqualTo(expected.DefaultBoundaryMode), context);
            Assert.That(actual.Grids, Is.EqualTo(expected.Grids), context);
            Assert.That(actual.Tiles, Is.EqualTo(expected.Tiles), context);
            Assert.That(actual.Entities, Is.EqualTo(expected.Entities), context);
        });
    }

    private static void AssertRecoveredMap(
        IEntityManager entMan,
        SharedMapSystem mapSystem,
        SharedZLevelMapSystem format,
        Entity<MapComponent> map,
        HashSet<Entity<MapGridComponent>> grids)
    {
        var prototypes = entMan.GetAllComponents(typeof(TransformComponent), includePaused: true)
            .Select(entry => entry.Uid)
            .Where(uid => entMan.GetComponent<TransformComponent>(uid).MapUid == map.Owner)
            .Select(uid => entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype?.ID)
            .Where(id => id != null)
            .ToArray();
        var grid = grids.Single();

        Assert.Multiple(() =>
        {
            Assert.That(entMan.GetComponent<MetaDataComponent>(map.Owner).EntityLifeStage,
                Is.EqualTo(EntityLifeStage.MapInitialized));
            Assert.That(format.TryValidate(map.Owner, out var error), Is.True, error);
            Assert.That(mapSystem.GetExistingZLevelLayers(grid.Owner, grid.Comp),
                Is.EquivalentTo(new[] { 0, 1, 2 }));
            Assert.That(prototypes, Does.Contain("ZLevelFloorOpeningMarker"));
            Assert.That(prototypes, Does.Not.Contain("Crowbar"));
            Assert.That(prototypes, Does.Not.Contain("Wirecutter"));
        });
    }

    private static string Canonical(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string Sha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static string GetEnvironment(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool GetEnvironmentBool(string name)
    {
        return bool.TryParse(Environment.GetEnvironmentVariable(name), out var value) && value;
    }

    private static void WriteReport(ZLevelRecoveryRehearsalReport report)
    {
        var outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Path.Combine(
                TestContext.CurrentContext.WorkDirectory,
                "zlevel-recovery");
        }

        outputDirectory = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, "zlevel-recovery.json");
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(report, options));
        TestContext.AddTestAttachment(path, "WTZ Z-level validated checkpoint recovery rehearsal");
        TestContext.Progress.WriteLine($"WTZ Z-level recovery report: {path}");
    }

    private sealed record RecoveryMapFingerprint(
        int FormatVersion,
        int MinimumLevel,
        int MaximumLevel,
        int DefaultLevel,
        ZLevelDefaultBoundaryMode DefaultBoundaryMode,
        string[] Grids,
        string[] Tiles,
        string[] Entities);
}

internal sealed record ZLevelRecoveryRehearsalReport(
    int SchemaVersion,
    string ContractVersion,
    string Status,
    DateTimeOffset GeneratedAtUtc,
    string FullyQualifiedTest,
    ZLevelRecoverySourceSnapshot Source,
    ZLevelRecoveryScenarioSnapshot Scenario,
    ZLevelRecoveryStepSnapshot Steps);

internal sealed record ZLevelRecoverySourceSnapshot(
    string ProjectRevision,
    string EngineRevision,
    string GitlinkRevision,
    bool ProjectClean,
    bool EngineClean);

internal sealed record ZLevelRecoveryScenarioSnapshot(
    int FloorCount,
    int GridCount,
    int InitialValidatedEntities,
    int InitialExcludedRoots,
    int RecoveredValidatedEntities,
    int RecoveredExcludedRoots,
    string RejectedCheckpointError,
    string InitialCheckpoint,
    string RecoveredCheckpoint,
    string InitialCheckpointSha256,
    string RecoveredCheckpointSha256,
    long Attempts,
    long Successes,
    long Failures,
    string FinalHealthStatus);

internal sealed record ZLevelRecoveryStepSnapshot(
    bool InitialCheckpointCreated,
    bool InvalidCheckpointRejected,
    bool KnownGoodBytesPreserved,
    bool CorruptSourceRemoved,
    bool InitialCheckpointLoaded,
    bool RecoveredCheckpointCreated,
    bool RecoveredCheckpointLoaded,
    bool StructuralStateMatched,
    bool NoCriticalHealthFinding,
    int TemporaryFilesRemaining);
