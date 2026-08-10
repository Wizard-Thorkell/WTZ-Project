// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using Content.Shared.ZLevel.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.Client.ZLevel;

/// <summary>
/// Debug-first client-side Z-level visibility.
/// Hides floors above the player and progressively fades floors below.
/// </summary>
public sealed class ZLevelSpriteVisibilitySystem : EntitySystem
{
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ZLevelTargetingSystem _targeting = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;
    private EntityQuery<TransformComponent> _transformQuery;
    private readonly Dictionary<EntityUid, Color> _originalColors = new();
    private readonly HashSet<EntityUid> _touched = new();

    public override void Initialize()
    {
        base.Initialize();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        _touched.Clear();

        if (_playerManager.LocalEntity is not { } player ||
            !_transformQuery.TryComp(player, out var playerXform))
        {
            RestoreUntouched();
            return;
        }

        var mapId = playerXform.MapID;
        var bounds = _eyeManager.GetWorldViewport();
        var playerZ = _transform.GetZLevel((player, playerXform, CompOrNull<ZLevelPositionComponent>(player)));

        foreach (var uid in _lookup.GetEntitiesIntersecting(mapId, bounds))
        {
            if (!_spriteQuery.TryComp(uid, out var sprite) ||
                !_transformQuery.TryComp(uid, out var xform) ||
                xform.MapID != mapId)
            {
                continue;
            }

            var entityZ = _transform.GetZLevel((uid, xform, CompOrNull<ZLevelPositionComponent>(uid)));
            var alpha = GetRelativeAlpha(playerZ, entityZ, uid);

            if (!_originalColors.ContainsKey(uid))
                _originalColors[uid] = sprite.Color;

            var original = _originalColors[uid];
            var targetColor = original.WithAlpha(original.A * alpha);
            if (sprite.Color != targetColor)
                _sprite.SetColor((uid, sprite), targetColor);

            _touched.Add(uid);
        }

        RestoreUntouched();
    }

    private void RestoreUntouched()
    {
        var toRemove = new List<EntityUid>();
        foreach (var (uid, color) in _originalColors)
        {
            if (_touched.Contains(uid))
                continue;

            if (_spriteQuery.TryComp(uid, out var sprite) && sprite.Color != color)
                _sprite.SetColor((uid, sprite), color);

            toRemove.Add(uid);
        }

        foreach (var uid in toRemove)
        {
            _originalColors.Remove(uid);
        }
    }

    private float GetRelativeAlpha(int playerZ, int entityZ, EntityUid uid)
    {
        if (entityZ > playerZ)
            return 0f;

        if (entityZ == playerZ)
            return 1f;

        if (!_targeting.IsEntityVisibleToViewer(uid))
            return 0f;

        var depth = playerZ - entityZ;
        return MathF.Max(0.18f, 0.65f - (depth - 1) * 0.18f);
    }
}
