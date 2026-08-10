// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Collections.Generic;
using Content.Shared.ZLevel.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.Map;

namespace Content.Client.ZLevel;

/// <summary>
/// Resolves the Z-level represented by entity-backed viewport eyes and remote eye targets.
/// </summary>
public sealed class ZLevelViewContextSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<IEye, EntityUid> _eyeOwners = new();
    private EntityQuery<EyeComponent> _eyeQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _eyeQuery = GetEntityQuery<EyeComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void FrameUpdate(float frameTime)
    {
        _eyeOwners.Clear();

        var query = AllEntityQuery<EyeComponent>();
        while (query.MoveNext(out var uid, out var eyeComponent))
        {
            _eyeOwners[eyeComponent.Eye] = uid;
        }
    }

    public bool TryGetViewContext(IEye eye, EntityUid? fallback, out ZLevelViewContext context)
    {
        if ((TryGetEyeOwner(eye, out var eyeOwner) || TryFindEyeOwner(eye, out eyeOwner)) &&
            _eyeQuery.TryComp(eyeOwner, out var eyeComponent))
        {
            var viewer = eyeComponent.Target ?? eyeOwner;
            if (TryResolve(viewer, eye.Position.MapId, out context))
                return true;
        }

        if (fallback is { } fallbackUid && TryResolve(fallbackUid, eye.Position.MapId, out context))
            return true;

        context = new ZLevelViewContext(null, eye.Position.MapId, 0);
        return eye.Position.MapId != MapId.Nullspace;
    }

    private bool TryGetEyeOwner(IEye eye, out EntityUid owner)
    {
        if (_eyeOwners.TryGetValue(eye, out owner) &&
            _eyeQuery.TryComp(owner, out var eyeComponent) &&
            ReferenceEquals(eyeComponent.Eye, eye))
        {
            return true;
        }

        _eyeOwners.Remove(eye);
        return false;
    }

    private bool TryFindEyeOwner(IEye eye, out EntityUid owner)
    {
        var query = AllEntityQuery<EyeComponent>();
        while (query.MoveNext(out var uid, out var eyeComponent))
        {
            if (!ReferenceEquals(eyeComponent.Eye, eye))
                continue;

            _eyeOwners[eye] = uid;
            owner = uid;
            return true;
        }

        owner = default;
        return false;
    }

    private bool TryResolve(EntityUid viewer, MapId eyeMap, out ZLevelViewContext context)
    {
        if (!_transformQuery.TryComp(viewer, out var transform) ||
            transform.MapID == MapId.Nullspace ||
            transform.MapID != eyeMap)
        {
            context = default;
            return false;
        }

        var zLevel = _transform.GetZLevel((viewer, transform, CompOrNull<ZLevelPositionComponent>(viewer)));
        context = new ZLevelViewContext(viewer, transform.MapID, zLevel);
        return true;
    }

}

public readonly record struct ZLevelViewContext(EntityUid? Viewer, MapId MapId, int ZLevel);
