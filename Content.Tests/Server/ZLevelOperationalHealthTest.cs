// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System;
using System.Linq;
using System.Text.Json;
using Content.Server.ZLevel.Commands;
using Content.Server.ZLevel.Operations;
using NUnit.Framework;

namespace Content.Tests.Server;

[TestFixture]
public sealed class ZLevelOperationalHealthTest
{
    [Test]
    public void HealthySignalsHaveNoFindings()
    {
        var generated = DateTimeOffset.UnixEpoch;
        var report = ZLevelOperationalHealthEvaluator.Evaluate(HealthySignals(), generated);

        Assert.Multiple(() =>
        {
            Assert.That(report.SchemaVersion, Is.EqualTo(1));
            Assert.That(report.ContractVersion, Is.EqualTo("WTZ-OPS-HEALTH-1"));
            Assert.That(report.GeneratedAtUtc, Is.EqualTo(generated));
            Assert.That(report.Status, Is.EqualTo(ZLevelOperationalHealthStatus.Healthy));
            Assert.That(report.Findings, Is.Empty);
        });
    }

    [Test]
    public void JsonOutputPreservesMachineReadableContract()
    {
        var report = ZLevelOperationalHealthEvaluator.Evaluate(
            HealthySignals(),
            DateTimeOffset.UnixEpoch);

        using var document = JsonDocument.Parse(ZLevelHealthCommand.Serialize(report));
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(root.GetProperty("contractVersion").GetString(),
                Is.EqualTo("WTZ-OPS-HEALTH-1"));
            Assert.That(root.GetProperty("status").GetString(), Is.EqualTo("Healthy"));
            Assert.That(root.GetProperty("signals").GetProperty("serverGc").GetBoolean(), Is.True);
            Assert.That(root.GetProperty("findings").GetArrayLength(), Is.Zero);
        });
    }

    [Test]
    public void RecoverableSignalsAreDegradedWithStableActions()
    {
        var signals = HealthySignals() with
        {
            ServerGc = false,
            AutosaveFailures = 1,
            LastAutosaveSucceeded = true,
            PvsDeferredRefreshes = 3,
            PvsSchedulerBudgetExhaustions = 1,
            SoundBudgetExhaustions = 2,
        };

        var report = ZLevelOperationalHealthEvaluator.Evaluate(signals, DateTimeOffset.UnixEpoch);
        var codes = report.Findings.Select(finding => finding.Code).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ZLevelOperationalHealthStatus.Degraded));
            Assert.That(codes, Is.EquivalentTo(new[]
            {
                "runtime.workstation-gc",
                "autosave.recovered-failures",
                "pvs.scheduler-debt",
                "sound.budget-exhausted",
            }));
            Assert.That(report.Findings, Has.All.Property(nameof(ZLevelOperationalFinding.Action)).Not.Empty);
        });
    }

    [Test]
    public void IntegrityAndFailOpenSignalsAreCritical()
    {
        var signals = HealthySignals() with
        {
            InvalidMapDescriptions = ["42: entity is outside the declared range"],
            AutosaveFailures = 1,
            LastAutosaveSucceeded = false,
            LastAutosaveError = "snapshot validation failed",
            PvsBudgetExhaustions = 1,
            PvsFailOpenCandidates = 12,
            TraceBudgetExhaustions = 2,
            BoundaryCacheEntries = 17,
            BoundaryCacheCapacity = 16,
        };

        var report = ZLevelOperationalHealthEvaluator.Evaluate(signals, DateTimeOffset.UnixEpoch);
        var codes = report.Findings.Select(finding => finding.Code).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(report.Status, Is.EqualTo(ZLevelOperationalHealthStatus.Critical));
            Assert.That(codes, Does.Contain("map.invalid-state"));
            Assert.That(codes, Does.Contain("autosave.last-attempt-failed"));
            Assert.That(codes, Does.Contain("pvs.fail-open-budget"));
            Assert.That(codes, Does.Contain("trace.budget-exhausted"));
            Assert.That(codes, Does.Contain("cache.boundary-over-capacity"));
            Assert.That(report.Findings.Count(finding =>
                    finding.Severity == ZLevelOperationalFindingSeverity.Critical),
                Is.EqualTo(5));
        });
    }

    private static ZLevelOperationalSignals HealthySignals()
    {
        return new ZLevelOperationalSignals
        {
            ServerGc = true,
            ConfiguredMaps = 1,
            ActiveAutosaveSchedules = 1,
            AutosaveAttempts = 1,
            AutosaveSuccesses = 1,
            LastAutosaveAttemptUtc = DateTimeOffset.UnixEpoch,
            LastAutosaveSuccessUtc = DateTimeOffset.UnixEpoch,
            LastAutosaveSucceeded = true,
            LastAutosavePath = "/Autosaves/test/checkpoint.yml",
            BoundaryCacheCapacity = 16,
            SkyCacheCapacity = 16,
            SoundCacheCapacity = 16,
        };
    }
}
