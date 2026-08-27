// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Client.ZLevel;

/// <summary>
/// Owns layered tile presentation and its optional diagnostics.
/// </summary>
public sealed class ZLevelOverlaySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private ZLevelDebugOverlay _overlay = default!;

    public bool MappingPreviewEnabled { get; private set; }

    public override void Initialize()
    {
        _overlay = new ZLevelDebugOverlay();
        Subs.CVar(_configuration, CCVars.ZLevelDebugOverlay, OnDebugOverlayChanged, true);

        SubscribeLocalEvent<ZLevelPositionComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ZLevelPositionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZLevelPositionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ZLevelPositionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnDebugOverlayChanged(bool enabled)
    {
        _overlay.ShowDebugInfo = enabled;
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
        if (MappingPreviewEnabled)
            return;

        _overlayManager.RemoveOverlay(_overlay);
    }

    public void SetMappingPreview(bool enabled)
    {
        MappingPreviewEnabled = enabled;
        _overlay.MappingPreviewEnabled = enabled;

        if (enabled)
        {
            AddOverlay();
            return;
        }

        if (_playerManager.LocalEntity is not { } player || !HasComp<ZLevelPositionComponent>(player))
            RemoveOverlay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        MappingPreviewEnabled = false;
        _overlayManager.RemoveOverlay(_overlay);
    }
}
