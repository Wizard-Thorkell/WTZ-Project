// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Client.ZLevel;

/// <summary>
/// Applies Z-level visibility at draw time without mutating replicated sprites.
/// </summary>
public sealed class ZLevelSpriteVisibilitySystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedZLevelVisibilitySystem _visibility = default!;
    [Dependency] private readonly ZLevelViewContextSystem _viewContext = default!;

    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _transformQuery = GetEntityQuery<TransformComponent>();
        SubscribeLocalEvent<SpriteComponent, BeforeSpriteRenderEvent>(OnBeforeSpriteRender);
    }

    private void OnBeforeSpriteRender(Entity<SpriteComponent> entity, ref BeforeSpriteRenderEvent args)
    {
        if (_playerManager.LocalEntity is not { } player ||
            !_transformQuery.TryComp(entity.Owner, out var entityTransform) ||
            !_viewContext.TryGetViewContext(args.Eye, player, out var view) ||
            entityTransform.MapID != view.MapId)
        {
            return;
        }

        var entityZ = _transform.GetZLevel((entity.Owner, entityTransform, CompOrNull<ZLevelPositionComponent>(entity.Owner)));
        var alpha = GetRelativeAlpha(view.MapId, view.ZLevel, entityZ, entity.Owner);
        if (alpha <= 0f)
        {
            args.Cancelled = true;
            return;
        }

        args.Modulate *= Color.White.WithAlpha(alpha);
    }

    public float GetRelativeAlpha(MapId viewerMap, int viewerZ, int entityZ, EntityUid entity)
    {
        if (entityZ > viewerZ)
            return 0f;

        if (entityZ == viewerZ)
            return 1f;

        if (!_visibility.IsEntityVisibleFrom(entity, viewerMap, viewerZ))
            return 0f;

        var depth = viewerZ - entityZ;
        return MathF.Max(0.18f, 0.65f - (depth - 1) * 0.18f);
    }
}
