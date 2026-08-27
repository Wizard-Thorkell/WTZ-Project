using Content.Server.EUI;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.ZLevel.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Administration.UI;

/// <summary>
///     Admin Eui for spawning and preview-ing explosions
/// </summary>
[UsedImplicitly]
public sealed class SpawnExplosionEui : BaseEui
{
    private readonly ExplosionSystem _explosionSystem;
    private readonly IEntityManager _entityManager;
    private readonly ISawmill _sawmill;
    private readonly SharedMapSystem _map;
    private readonly SharedTransformSystem _transform;
    private readonly SharedZLevelSystem _zLevels;

    public SpawnExplosionEui()
    {
        _entityManager = IoCManager.Resolve<IEntityManager>();
        _explosionSystem = _entityManager.System<ExplosionSystem>();
        _map = _entityManager.System<SharedMapSystem>();
        _transform = _entityManager.System<SharedTransformSystem>();
        _zLevels = _entityManager.System<SharedZLevelSystem>();
        _sawmill = IoCManager.Resolve<ILogManager>().GetSawmill("explosion");
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (msg is not SpawnExplosionEuiMsg.PreviewRequest request)
            return;

        if (request.TotalIntensity <= 0 || request.IntensitySlope <= 0)
            return;

        var worldZ = request.WorldZ;
        EntityUid? frameGrid = null;
        if (Player.AttachedEntity is { } player &&
            _entityManager.TryGetComponent(player, out TransformComponent? transform) &&
            transform.MapID == request.Epicenter.MapId)
        {
            worldZ = _zLevels.GetWorldZLevel(player);
            if (transform.GridUid is { } playerGrid &&
                _entityManager.TryGetComponent(playerGrid, out MapGridComponent? grid))
            {
                var localZ = _transform.WorldToLocalZLevel(playerGrid, worldZ);
                var xy = _map.WorldToTile(playerGrid, grid, request.Epicenter.Position);
                if (!_map.GetZLevelTileRef(
                        playerGrid,
                        grid,
                        new ZLevelTileIndices(xy.X, xy.Y, localZ))
                    .Tile.IsEmpty)
                {
                    frameGrid = playerGrid;
                }
            }
        }

        var explosion = _explosionSystem.GenerateExplosionPreview(request, worldZ, frameGrid);

        if (explosion == null)
        {
            _sawmill.Error("Failed to generate explosion preview.");
            return;
        }

        SendMessage(new SpawnExplosionEuiMsg.PreviewData(explosion, request.IntensitySlope, request.TotalIntensity));
    }
}
