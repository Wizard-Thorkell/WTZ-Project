// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Client.ZLevel.Commands;

[AnyCommand]
public sealed class ZLevelLightingCaptureCommand : IConsoleCommand
{
    private static bool _startRequested;
    private static bool _requestedAutoShutdown;

    public string Command => "zlevellightingcapture";
    public string Description => "Runs the canonical real-client Z-level RGB lighting capture.";
    public string Help => $"Usage: {Command} [keep-open]";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length > 1 ||
            args.Length == 1 && !args[0].Equals("keep-open", StringComparison.OrdinalIgnoreCase))
        {
            shell.WriteError(Help);
            return;
        }

        var autoShutdown = args.Length == 0;
        if (_startRequested)
        {
            shell.WriteError("A Z-level lighting capture request is already pending.");
            return;
        }

        _requestedAutoShutdown = autoShutdown;
        _startRequested = true;
        shell.WriteLine(
            "Z-level lighting capture queued; waiting for client systems and the canonical fixture.");
    }

    internal static bool TryTakeStartRequest(out bool autoShutdown)
    {
        autoShutdown = _requestedAutoShutdown;
        if (!_startRequested)
            return false;

        _startRequested = false;
        return true;
    }
}
