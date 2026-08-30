// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Text.Json;
using System.Text.Json.Serialization;
using Content.Server.Administration;
using Content.Server.ZLevel.Operations;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.ZLevel.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ZLevelHealthCommand : IConsoleCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "zlevelhealth";
    public string Description => "Evaluates native Z-level operational health and recovery signals.";
    public string Help => $"Usage: {Command} [json]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 ||
            args.Length == 1 && !args[0].Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError(Help);
            return;
        }

        var report = _entityManager.System<ZLevelOperationalHealthSystem>().Capture();
        if (args.Length == 1)
        {
            shell.WriteLine(Serialize(report));
            return;
        }

        var signals = report.Signals;
        shell.WriteLine($"WTZ Z-level operational health: {report.Status.ToString().ToUpperInvariant()}");
        shell.WriteLine(
            $"  contract={report.ContractVersion}, schema={report.SchemaVersion}, " +
            $"generated={report.GeneratedAtUtc:O}");
        shell.WriteLine(
            $"  runtime: server-gc={signals.ServerGc}, sessions={signals.InGameSessions}, " +
            $"maps={signals.ConfiguredMaps}, invalid-maps={signals.InvalidMapDescriptions.Length}, " +
            $"flights/elevators={signals.ActiveFlights}/{signals.ActiveElevatorTravels}");
        shell.WriteLine(
            $"  autosave: active={signals.ActiveAutosaveSchedules}, " +
            $"attempts/success/failure={signals.AutosaveAttempts}/" +
            $"{signals.AutosaveSuccesses}/{signals.AutosaveFailures}, " +
            $"last-success={FormatNullable(signals.LastAutosaveSucceeded)}");
        shell.WriteLine(
            $"  budgets: pvs={signals.PvsBudgetExhaustions}, " +
            $"pvs-deferred={signals.PvsDeferredRefreshes}, trace={signals.TraceBudgetExhaustions}, " +
            $"sky={signals.SkyExposureBudgetExhaustions}, explosion={signals.ExplosionBudgetExhaustions}, " +
            $"sound={signals.SoundBudgetExhaustions}, path={signals.PathBudgetExhaustions}");
        shell.WriteLine(
            $"  caches: boundary={signals.BoundaryCacheEntries}/" +
            $"{signals.BoundaryCacheCapacity} order={signals.BoundaryCacheOrderTokens}, " +
            $"sky={signals.SkyCacheEntries}/{signals.SkyCacheCapacity} " +
            $"order={signals.SkyCacheOrderEntries}, sound={signals.SoundCacheEntries}/" +
            $"{signals.SoundCacheCapacity} order={signals.SoundCacheOrderTokens}, " +
            $"gravity={signals.GravityCachedGrids} pending={signals.GravityPendingRefreshGrids}");

        if (signals.LastAutosavePath != null)
            shell.WriteLine($"  last checkpoint: {signals.LastAutosavePath}");
        if (signals.LastAutosaveError != null)
            shell.WriteLine($"  last autosave error: {OneLine(signals.LastAutosaveError)}");

        if (report.Findings.Length == 0)
        {
            shell.WriteLine("  findings: none since the last counter reset.");
            return;
        }

        shell.WriteLine($"  findings: {report.Findings.Length}");
        foreach (var finding in report.Findings)
        {
            shell.WriteLine(
                $"    [{finding.Severity.ToString().ToUpperInvariant()}] {finding.Code}: " +
                OneLine(finding.Message));
            shell.WriteLine($"      action: {OneLine(finding.Action)}");
        }
    }

    private static string FormatNullable(bool? value)
    {
        return value switch
        {
            true => "true",
            false => "false",
            null => "none",
        };
    }

    private static string OneLine(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }

    internal static string Serialize(ZLevelOperationalHealthReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }
}
