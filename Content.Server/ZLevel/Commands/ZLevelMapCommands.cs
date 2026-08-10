using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;

namespace Content.Server.ZLevel.Commands;

[AdminCommand(AdminFlags.Mapping)]
public sealed class ZLevelCopyTilesCommand : ZLevelTileRegionCommand
{
    public override string Command => "zcopytiles";

    public override string Description => "Copies a rectangular tile region from one Z level to another on a grid.";

    public override string Help => "Usage: zcopytiles <gridUid> <x1> <y1> <x2> <y2> <sourceZ> <targetZ> [includeEmpty=true]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length is not (7 or 8))
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryParseRegion(shell, args, out var gridUid, out var grid, out var min, out var max))
            return;

        if (!int.TryParse(args[5], out var sourceZ))
        {
            shell.WriteError($"{args[5]} is not a valid source Z level.");
            return;
        }

        if (!int.TryParse(args[6], out var targetZ))
        {
            shell.WriteError($"{args[6]} is not a valid target Z level.");
            return;
        }

        var includeEmpty = true;
        if (args.Length == 8 && !bool.TryParse(args[7], out includeEmpty))
        {
            shell.WriteError($"{args[7]} is not a valid boolean.");
            return;
        }

        var changed = Map.CopyZLevelTileRegion(gridUid, grid, min, max, sourceZ, targetZ, includeEmpty);
        shell.WriteLine($"Copied Z tiles from {sourceZ} to {targetZ}; changed {changed} tiles.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<MapGridComponent>(args[0], EntityManager), "<gridUid>"),
            2 => CompletionResult.FromHint("<x1>"),
            3 => CompletionResult.FromHint("<y1>"),
            4 => CompletionResult.FromHint("<x2>"),
            5 => CompletionResult.FromHint("<y2>"),
            6 => CompletionResult.FromHint("<sourceZ>"),
            7 => CompletionResult.FromHint("<targetZ>"),
            8 => CompletionResult.FromHint("[includeEmpty=true]"),
            _ => CompletionResult.Empty,
        };
    }
}

[AdminCommand(AdminFlags.Mapping)]
public sealed class ZLevelClearTilesCommand : ZLevelTileRegionCommand
{
    public override string Command => "zcleartiles";

    public override string Description => "Clears a rectangular tile region on one Z level of a grid.";

    public override string Help => "Usage: zcleartiles <gridUid> <x1> <y1> <x2> <y2> <z>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 6)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!TryParseRegion(shell, args, out var gridUid, out var grid, out var min, out var max))
            return;

        if (!int.TryParse(args[5], out var z))
        {
            shell.WriteError($"{args[5]} is not a valid Z level.");
            return;
        }

        var changed = Map.ClearZLevelTileRegion(gridUid, grid, min, max, z);
        shell.WriteLine($"Cleared {changed} tiles on Z {z}.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(CompletionHelper.Components<MapGridComponent>(args[0], EntityManager), "<gridUid>"),
            2 => CompletionResult.FromHint("<x1>"),
            3 => CompletionResult.FromHint("<y1>"),
            4 => CompletionResult.FromHint("<x2>"),
            5 => CompletionResult.FromHint("<y2>"),
            6 => CompletionResult.FromHint("<z>"),
            _ => CompletionResult.Empty,
        };
    }
}

public abstract class ZLevelTileRegionCommand : LocalizedEntityCommands
{
    protected SharedMapSystem Map => EntityManager.System<SharedMapSystem>();

    protected bool TryParseRegion(
        IConsoleShell shell,
        string[] args,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i min,
        out Vector2i max)
    {
        gridUid = default;
        grid = default!;
        min = default;
        max = default;

        if (!NetEntity.TryParse(args[0], out var netGrid))
        {
            shell.WriteError($"{args[0]} is not a valid grid entity ID.");
            return false;
        }

        gridUid = EntityManager.GetEntity(netGrid);
        if (!EntityManager.TryGetComponent<MapGridComponent>(gridUid, out var gridComp))
        {
            shell.WriteError($"Entity {args[0]} is not a grid.");
            return false;
        }

        grid = gridComp;

        if (!int.TryParse(args[1], out var x1) ||
            !int.TryParse(args[2], out var y1) ||
            !int.TryParse(args[3], out var x2) ||
            !int.TryParse(args[4], out var y2))
        {
            shell.WriteError("Region coordinates must be integers.");
            return false;
        }

        min = new Vector2i(x1, y1);
        max = new Vector2i(x2, y2);
        return true;
    }
}
