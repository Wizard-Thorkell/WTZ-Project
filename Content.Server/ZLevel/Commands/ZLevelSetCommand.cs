// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Globalization;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;

namespace Content.Server.ZLevel.Commands;

/// <summary>
/// Moves the attached administrator between authored local Z levels for diagnostics.
/// </summary>
[AdminCommand(AdminFlags.Debug)]
public sealed class ZLevelSetCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "zlevelset";
    public string Description => "Moves your attached entity to an authored local Z level.";
    public string Help => $"Usage: {Command} <local-z>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 ||
            !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetZ))
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player?.AttachedEntity is not { } attached ||
            !_entityManager.TryGetComponent(attached, out TransformComponent? transform) ||
            transform.MapUid is not { } mapUid ||
            !_entityManager.TryGetComponent(mapUid, out ZLevelMapComponent? mapConfig))
        {
            shell.WriteError("Your attached entity is not on an authored Z-level map.");
            return;
        }

        if (targetZ < mapConfig.MinimumLevel || targetZ > mapConfig.MaximumLevel)
        {
            shell.WriteError(
                $"Z level {targetZ} is outside the authored range " +
                $"{mapConfig.MinimumLevel}..{mapConfig.MaximumLevel}.");
            return;
        }

        if (!_entityManager.System<SharedZLevelSystem>().SetZLevelPosition(attached, targetZ))
        {
            shell.WriteError("Unable to move your attached entity to the requested Z level.");
            return;
        }

        shell.WriteLine($"Moved attached entity to local Z level {targetZ}.");
    }
}
