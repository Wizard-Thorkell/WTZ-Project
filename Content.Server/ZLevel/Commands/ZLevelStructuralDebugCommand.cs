// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Server.Administration;
using Content.Server.ZLevel.Structural;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.ZLevel.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class ZLevelStructuralDebugCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "showzstability";
    public string Description => "Toggles native Z-level structural stability diagnostics.";
    public string Help => $"Usage: {Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } session)
        {
            shell.WriteError("This command must be run by a player.");
            return;
        }

        var enabled = _entityManager.System<ZLevelStructuralSystem>().ToggleDebugView(session);
        shell.WriteLine(enabled
            ? "Enabled Z-level structural stability diagnostics."
            : "Disabled Z-level structural stability diagnostics.");
    }
}
