// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.ZLevel.Structural;

public sealed partial class ZLevelStructuralSystem
{
    private readonly HashSet<ICommonSession> _debugSessions = new();
    private readonly HashSet<EntityUid> _debugDirtyGrids = new();
    private readonly HashSet<NetEntity> _debugRemovedGrids = new();

    public bool ToggleDebugView(ICommonSession session)
    {
        var enabled = _debugSessions.Add(session);
        if (!enabled)
            _debugSessions.Remove(session);

        RaiseNetworkEvent(new ZLevelStructuralOverlayToggledEvent(enabled), session.Channel);
        if (enabled)
            SendFullDebugSnapshot(session);

        return enabled;
    }

    private void MarkDebugDirty(EntityUid gridUid)
    {
        _debugDirtyGrids.Add(gridUid);
    }

    private void MarkDebugRemoved(EntityUid gridUid)
    {
        _debugDirtyGrids.Remove(gridUid);
        if (TryGetNetEntity(gridUid, out var netGrid))
            _debugRemovedGrids.Add(netGrid.Value);
    }

    private void SendFullDebugSnapshot(ICommonSession session)
    {
        var grids = new Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>>();
        var query = EntityQueryEnumerator<ZLevelStructuralGridComponent, MapGridComponent>();
        while (query.MoveNext(out var gridUid, out var structural, out var grid))
        {
            grids[GetNetEntity(gridUid)] = BuildDebugTiles(gridUid, grid, structural);
        }

        RaiseNetworkEvent(new ZLevelStructuralOverlaySnapshotEvent(grids, true), session.Channel);
    }

    private void PushDebugSnapshots()
    {
        if (_debugSessions.Count == 0)
        {
            _debugDirtyGrids.Clear();
            _debugRemovedGrids.Clear();
            return;
        }

        if (_debugDirtyGrids.Count == 0 && _debugRemovedGrids.Count == 0)
            return;

        var grids = new Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>>();
        foreach (var netGrid in _debugRemovedGrids)
        {
            grids[netGrid] = new Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>();
        }

        _debugRemovedGrids.Clear();
        foreach (var gridUid in _debugDirtyGrids)
        {
            if (!_structuralQuery.TryComp(gridUid, out var structural) ||
                !_gridQuery.TryComp(gridUid, out var grid))
            {
                continue;
            }

            grids[GetNetEntity(gridUid)] = BuildDebugTiles(gridUid, grid, structural);
        }

        _debugDirtyGrids.Clear();
        if (grids.Count == 0)
            return;

        var ev = new ZLevelStructuralOverlaySnapshotEvent(grids, false);
        foreach (var session in _debugSessions.ToArray())
        {
            if (session.Status != SessionStatus.InGame)
                _debugSessions.Remove(session);
            else
                RaiseNetworkEvent(ev, session.Channel);
        }
    }

    private Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile> BuildDebugTiles(
        EntityUid gridUid,
        MapGridComponent grid,
        ZLevelStructuralGridComponent structural)
    {
        var result = new Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>();
        foreach (var tile in _map.GetAllNonEmptyZLevelTiles(gridUid, grid))
        {
            var indices = tile.GridIndices;
            result[indices] = new ZLevelStructuralDebugTile(
                structural.Stability.GetValueOrDefault(indices),
                structural.PendingCollapses.ContainsKey(indices));
        }

        return result;
    }

    private void ClearDebugSnapshots()
    {
        _debugDirtyGrids.Clear();
        _debugRemovedGrids.Clear();
        if (_debugSessions.Count == 0)
            return;

        var ev = new ZLevelStructuralOverlaySnapshotEvent(
            new Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>>(),
            true);
        foreach (var session in _debugSessions.ToArray())
        {
            if (session.Status != SessionStatus.InGame)
                _debugSessions.Remove(session);
            else
                RaiseNetworkEvent(ev, session.Channel);
        }
    }
}
