// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client.ZLevel;

/// <summary>
/// Owns the lightweight debug overlay that makes Z-level testing readable in-game.
/// </summary>
public sealed class ZLevelOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ZLevelDebugOverlay _overlay = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelPositionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ZLevelPositionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelPositionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ZLevelPositionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        _overlay = new ZLevelDebugOverlay();
    }

    private void OnInit(Entity<ZLevelPositionComponent> ent, ref ComponentInit args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            AddOverlay();
    }

    private void OnShutdown(Entity<ZLevelPositionComponent> ent, ref ComponentShutdown args)
    {
        if (_playerManager.LocalEntity == ent.Owner)
            RemoveOverlay();
    }

    private void OnPlayerAttached(Entity<ZLevelPositionComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        AddOverlay();
    }

    private void OnPlayerDetached(Entity<ZLevelPositionComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        RemoveOverlay();
    }

    private void AddOverlay()
    {
        if (!_overlayManager.HasOverlay(_overlay.GetType()))
            _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        _overlayManager.RemoveOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlayManager.RemoveOverlay(_overlay);
    }
}
