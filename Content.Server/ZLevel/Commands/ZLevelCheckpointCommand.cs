// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Globalization;
using Content.Server.Administration;
using Content.Server.Mapping;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map;

namespace Content.Server.ZLevel.Commands;

/// <summary>
/// Creates a validated, atomic mapper checkpoint for an initialized map.
/// </summary>
[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class ZLevelCheckpointCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "zlevelcheckpoint";
    public string Description => "Creates a validated checkpoint of an initialized Z-level map.";
    public string Help => $"Usage: {Command} <map-id> <checkpoint-name>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2 ||
            !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var mapValue))
        {
            shell.WriteError(Help);
            return;
        }

        var mapId = new MapId(mapValue);
        if (!_entityManager.System<SharedMapSystem>().TryGetMap(mapId, out var mapUid))
        {
            shell.WriteError($"Map {mapId} does not exist.");
            return;
        }

        var mapping = _entityManager.System<MappingSystem>();
        if (!mapping.TryCreateCheckpointNow(
                mapUid.Value,
                args[1],
                out var path,
                out var report,
                out var error))
        {
            shell.WriteError($"Checkpoint for map {mapId} failed: {error}");
            return;
        }

        shell.WriteLine($"Validated checkpoint for map {mapId} written to {path}.");
        shell.WriteLine(
            $"  validated={report.ValidatedEntities}, excluded-roots={report.ExcludedRoots}, " +
            $"normalized-references={report.NormalizedReferences}");
    }
}
