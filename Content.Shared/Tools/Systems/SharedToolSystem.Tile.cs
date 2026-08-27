using Content.Shared.Database;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Physics;
using Content.Shared.Tools.Components;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Tools.Systems;

public abstract partial class SharedToolSystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;

    public void InitializeTile()
    {
        SubscribeLocalEvent<ToolTileCompatibleComponent, AfterInteractEvent>(OnToolTileAfterInteract);
        SubscribeLocalEvent<ToolTileCompatibleComponent, TileToolDoAfterEvent>(OnToolTileComplete);
    }

    private void OnToolTileAfterInteract(Entity<ToolTileCompatibleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || args.Target != null && !HasComp<PuddleComponent>(args.Target))
            return;

        args.Handled = UseToolOnTile((ent, ent, null), args.User, args.ClickLocation);
    }

    private void OnToolTileComplete(Entity<ToolTileCompatibleComponent> ent, ref TileToolDoAfterEvent args)
    {
        var comp = ent.Comp;
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<ToolComponent>(ent, out var tool))
            return;

        var gridUid = GetEntity(args.Grid);
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
        {
            Log.Error("Attempted use tool on a non-existent grid?");
            return;
        }

        var tileRef = _maps.GetZLevelTileRef(
            gridUid,
            grid,
            new ZLevelTileIndices(args.GridTile.X, args.GridTile.Y, args.ZLevel));
        var coords = _maps.GridTileToLocal(gridUid, grid, args.GridTile);
        if (comp.RequiresUnobstructed && _turfs.IsTileBlocked(tileRef, CollisionGroup.MobMask))
            return;

        if (!TryDeconstructWithToolQualities(tileRef, tool.Qualities))
            return;

        AdminLogger.Add(
            LogType.LatticeCut,
            LogImpact.Medium,
            $"{ToPrettyString(args.User):player} used {ToPrettyString(ent)} to edit the tile at {coords} on Z {args.ZLevel}");
        args.Handled = true;
    }

    private bool UseToolOnTile(Entity<ToolTileCompatibleComponent?, ToolComponent?> ent, EntityUid user, EntityCoordinates clickLocation)
    {
        if (!Resolve(ent, ref ent.Comp1, ref ent.Comp2, false))
            return false;

        var comp = ent.Comp1!;
        var tool = ent.Comp2!;

        if (!_mapManager.TryFindGridAt(_transformSystem.ToMapCoordinates(clickLocation), out var gridUid, out var mapGrid))
            return false;

        var userTransform = Transform(user);
        var worldZLevel = _transformSystem.GetWorldZLevel((
            user,
            userTransform,
            CompOrNull<ZLevelPositionComponent>(user)));
        var zLevel = _transformSystem.WorldToLocalZLevel(gridUid, worldZLevel);
        var gridIndices = _maps.TileIndicesFor(gridUid, mapGrid, clickLocation);
        var tileRef = _maps.GetZLevelTileRef(
            gridUid,
            mapGrid,
            new ZLevelTileIndices(gridIndices.X, gridIndices.Y, zLevel));
        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];

        if (!tool.Qualities.ContainsAny(tileDef.DeconstructTools))
            return false;

        if (string.IsNullOrWhiteSpace(tileDef.BaseTurf))
            return false;

        if (comp.RequiresUnobstructed && _turfs.IsTileBlocked(tileRef, CollisionGroup.MobMask))
            return false;

        var coordinates = _maps.GridTileToLocal(gridUid, mapGrid, gridIndices);
        SharedInteractionSystem.Ignored otherFloor = entity => !_zLevel.IsOnWorldZLevel(entity, worldZLevel);
        if (!InteractionSystem.InRangeUnobstructed(user, coordinates, predicate: otherFloor, popup: false))
            return false;

        var args = new TileToolDoAfterEvent(GetNetEntity(gridUid), gridIndices, zLevel);
        UseTool(ent, user, ent, comp.Delay, tool.Qualities, args, out _, toolComponent: tool);
        return true;
    }

    public bool TryDeconstructWithToolQualities(TileRef tileRef, PrototypeFlags<ToolQualityPrototype> withToolQualities)
    {
        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];
        if (withToolQualities.ContainsAny(tileDef.DeconstructTools))
        {
            // don't do this on the client or else the tile entity spawn mispredicts and looks horrible
            return _net.IsClient || _tiles.DeconstructTile(tileRef);
        }
        return false;
    }

    public bool TryDeconstructWithToolQualities(ZLevelTileRef tileRef, PrototypeFlags<ToolQualityPrototype> withToolQualities)
    {
        var tileDef = (ContentTileDefinition) _tileDefManager[tileRef.Tile.TypeId];
        if (withToolQualities.ContainsAny(tileDef.DeconstructTools))
        {
            // Do not deconstruct predicted tiles on the client; the resulting item spawn would mispredict.
            return _net.IsClient || _tiles.DeconstructZLevelTile(tileRef);
        }

        return false;
    }
}
