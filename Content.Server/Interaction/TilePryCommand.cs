using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Maps;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.Interaction;

[AdminCommand(AdminFlags.Debug)]
public sealed class TilePryCommand : LocalizedEntityCommands
{
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    private readonly string _platingId = "Plating";

    public override string Command => "tilepry";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player?.AttachedEntity is not { } attached)
        {
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteLine(Help);
            return;
        }

        if (!int.TryParse(args[0], out var radius))
        {
            shell.WriteError(Loc.GetString($"cmd-tilepry-arg-must-be-number", ("arg", args[0])));
            return;
        }

        if (radius < 0)
        {
            shell.WriteError(Loc.GetString($"cmd-tilepry-radius-must-be-positive"));
            return;
        }

        var xform = EntityManager.GetComponent<TransformComponent>(attached);

        var playerGrid = xform.GridUid;

        if (!EntityManager.TryGetComponent<MapGridComponent>(playerGrid, out var mapGrid))
            return;

        var playerPosition = xform.Coordinates;
        var playerZLevel = _zLevel.GetZLevel(attached);

        for (var i = -radius; i <= radius; i++)
        {
            for (var j = -radius; j <= radius; j++)
            {
                var indices = _mapSystem.TileIndicesFor(
                    playerGrid.Value,
                    mapGrid,
                    playerPosition.Offset(new System.Numerics.Vector2(i, j)));
                var tile = _mapSystem.GetZLevelTileRef(
                    playerGrid.Value,
                    mapGrid,
                    new ZLevelTileIndices(indices.X, indices.Y, playerZLevel));
                if (tile.Tile.IsEmpty)
                    continue;

                var tileDef = (ContentTileDefinition)_tileDefinitionManager[tile.Tile.TypeId];

                if (!tileDef.CanCrowbar)
                    continue;

                var plating = _tileDefinitionManager[_platingId];
                _mapSystem.SetZLevelTile(playerGrid.Value, mapGrid, tile.GridIndices, new Tile(plating.TileId));
            }
        }
    }
}
