// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel;
using Robust.Client.Graphics;
using Robust.Shared.Map;

namespace Content.Client.ZLevel;

public sealed class ZLevelStructuralDebugSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;

    public readonly Dictionary<NetEntity, Dictionary<ZLevelTileIndices, ZLevelStructuralDebugTile>> Grids = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<ZLevelStructuralOverlayToggledEvent>(OnOverlayToggled);
        SubscribeNetworkEvent<ZLevelStructuralOverlaySnapshotEvent>(OnSnapshot);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay<ZLevelStructuralDebugOverlay>();
        Grids.Clear();
    }

    private void OnOverlayToggled(ZLevelStructuralOverlayToggledEvent ev)
    {
        if (ev.Enabled)
        {
            if (!_overlayManager.HasOverlay<ZLevelStructuralDebugOverlay>())
                _overlayManager.AddOverlay(new ZLevelStructuralDebugOverlay());
            return;
        }

        _overlayManager.RemoveOverlay<ZLevelStructuralDebugOverlay>();
        Grids.Clear();
    }

    private void OnSnapshot(ZLevelStructuralOverlaySnapshotEvent ev)
    {
        if (ev.ReplaceAll)
            Grids.Clear();

        foreach (var (grid, tiles) in ev.Grids)
        {
            if (tiles.Count == 0)
                Grids.Remove(grid);
            else
                Grids[grid] = tiles;
        }
    }
}
