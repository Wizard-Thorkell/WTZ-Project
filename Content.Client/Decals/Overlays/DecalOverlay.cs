using System.Numerics;
using Content.Client.ZLevel;
using Content.Shared.Decals;
using Content.Shared.ZLevel.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Map.Enumerators;
using Robust.Shared.Prototypes;

namespace Content.Client.Decals.Overlays
{
    public sealed class DecalOverlay : GridOverlay
    {
        private readonly SpriteSystem _sprites;
        private readonly IEntityManager _entManager;
        private readonly IPrototypeManager _prototypeManager;
        private readonly IPlayerManager _playerManager;
        private readonly SharedTransformSystem _transformSystem;
        private readonly SharedZLevelVisibilitySystem _visibilitySystem;
        private readonly ZLevelOverlaySystem _zLevelOverlaySystem;
        private readonly ZLevelViewContextSystem _viewContextSystem;

        private readonly Dictionary<string, (Texture Texture, bool SnapCardinals)> _cachedTextures = new(64);

        private readonly List<(uint Id, Decal Decal, float Alpha)> _decals = new();

        public DecalOverlay(
            SpriteSystem sprites,
            IEntityManager entManager,
            IPrototypeManager prototypeManager)
        {
            _sprites = sprites;
            _entManager = entManager;
            _prototypeManager = prototypeManager;
            _playerManager = IoCManager.Resolve<IPlayerManager>();
            _transformSystem = entManager.System<SharedTransformSystem>();
            _visibilitySystem = entManager.System<SharedZLevelVisibilitySystem>();
            _zLevelOverlaySystem = entManager.System<ZLevelOverlaySystem>();
            _viewContextSystem = entManager.System<ZLevelViewContextSystem>();
        }

        protected override void Draw(in OverlayDrawArgs args)
        {
            if (args.MapId == MapId.Nullspace)
                return;

            if (args.Viewport.Eye is not { } eye ||
                !_viewContextSystem.TryGetViewContext(eye, _playerManager.LocalEntity, out var view))
            {
                return;
            }

            var owner = Grid.Owner;

            if (!_entManager.TryGetComponent(owner, out DecalGridComponent? decalGrid) ||
                !_entManager.TryGetComponent(owner, out TransformComponent? xform))
            {
                return;
            }

            if (xform.MapID != args.MapId)
                return;

            // Shouldn't need to clear cached textures unless the prototypes get reloaded.
            var handle = args.WorldHandle;
            var xformSystem = _entManager.System<TransformSystem>();
            var eyeAngle = args.Viewport.Eye?.Rotation ?? Angle.Zero;

            var gridAABB = xformSystem.GetInvWorldMatrix(xform).TransformBox(args.WorldBounds.Enlarged(1f));
            var chunkEnumerator = new ChunkIndicesEnumerator(gridAABB, SharedDecalSystem.ChunkSize);
            _decals.Clear();

            while (chunkEnumerator.MoveNext(out var index))
            {
                if (!decalGrid.ChunkCollection.ChunkCollection.TryGetValue(index.Value, out var chunk))
                    continue;

                foreach (var (id, decal) in chunk.Decals)
                {
                    if (!gridAABB.Contains(decal.Coordinates))
                        continue;

                    var targetWorldZ = _transformSystem.LocalToWorldZLevel(owner, decal.ZLevel);
                    if (!TryGetLayerAlpha(owner, Grid.Comp, decal, view.WorldZLevel, targetWorldZ, out var alpha))
                        continue;

                    _decals.Add((id, decal, alpha));
                }
            }

            if (_decals.Count == 0)
                return;

            _decals.Sort((x, y) =>
            {
                var level = x.Decal.ZLevel.CompareTo(y.Decal.ZLevel);
                if (level != 0)
                    return level;

                var zComp = x.Decal.ZIndex.CompareTo(y.Decal.ZIndex);

                if (zComp != 0)
                    return zComp;

                return x.Id.CompareTo(y.Id);
            });

            var (_, worldRot, worldMatrix) = xformSystem.GetWorldPositionRotationMatrix(xform);
            handle.SetTransform(worldMatrix);

            foreach (var (_, decal, alpha) in _decals)
            {
                if (!_cachedTextures.TryGetValue(decal.Id, out var cache))
                {
                    // Nothing to cache someone messed up
                    if (!_prototypeManager.TryIndex<DecalPrototype>(decal.Id, out var decalProto))
                    {
                        continue;
                    }

                    cache = (_sprites.Frame0(decalProto.Sprite), decalProto.SnapCardinals);
                    _cachedTextures[decal.Id] = cache;
                }

                var cardinal = Angle.Zero;

                if (cache.SnapCardinals)
                {
                    var worldAngle = eyeAngle + worldRot;
                    cardinal = worldAngle.GetCardinalDir().ToAngle();
                }

                var angle = decal.Angle - cardinal;
                var color = decal.Color;
                if (alpha < 1f)
                {
                    var resolvedColor = color ?? Color.White;
                    color = resolvedColor.WithAlpha(resolvedColor.A * alpha);
                }

                if (angle.Equals(Angle.Zero))
                    handle.DrawTexture(cache.Texture, decal.Coordinates, color);
                else
                    handle.DrawTexture(cache.Texture, decal.Coordinates, angle, color);
            }

            handle.SetTransform(Matrix3x2.Identity);
        }

        private bool TryGetLayerAlpha(
            EntityUid gridUid,
            MapGridComponent grid,
            Decal decal,
            int viewerWorldZ,
            int targetWorldZ,
            out float alpha)
        {
            if (_zLevelOverlaySystem.MappingPreviewEnabled)
            {
                var delta = targetWorldZ - viewerWorldZ;
                alpha = delta switch
                {
                    0 => 1f,
                    -1 => 0.32f,
                    1 => 0.22f,
                    _ => 0f,
                };
                return alpha > 0f;
            }

            if (targetWorldZ == viewerWorldZ)
            {
                alpha = 1f;
                return true;
            }

            if (targetWorldZ > viewerWorldZ)
            {
                alpha = 0f;
                return false;
            }

            var tile = new Vector2i(
                (int) Math.Floor(decal.Coordinates.X),
                (int) Math.Floor(decal.Coordinates.Y));
            if (!_visibilitySystem.IsTileVisibleFrom(gridUid, grid, tile, viewerWorldZ, decal.ZLevel))
            {
                alpha = 0f;
                return false;
            }

            alpha = MathF.Max(0.16f, 1f - (viewerWorldZ - targetWorldZ) * 0.2f);
            return true;
        }
    }
}
