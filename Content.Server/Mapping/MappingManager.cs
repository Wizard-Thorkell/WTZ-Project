using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Content.Shared.Mapping;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Server.Mapping;

public sealed class MappingManager : IPostInjectInit
{
#if !FULL_RELEASE
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly ILogManager _log = default!;
    [Dependency] private readonly IServerNetManager _net = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IEntitySystemManager _systems = default!;
    [Dependency] private readonly IEntityManager _ent = default!;

    private ISawmill _sawmill = default!;
    private ZStdCompressionContext _zstd = default!;
#endif

    public void PostInject()
    {
#if !FULL_RELEASE
        _net.RegisterNetMessage<MappingSaveMapMessage>(OnMappingSaveMap);
        _net.RegisterNetMessage<MappingSaveMapErrorMessage>();
        _net.RegisterNetMessage<MappingMapDataMessage>();

        _sawmill = _log.GetSawmill("mapping");
        _zstd = new ZStdCompressionContext();
#endif
    }

    private void OnMappingSaveMap(MappingSaveMapMessage message)
    {
#if !FULL_RELEASE
        try
        {
            if (!_players.TryGetSessionByChannel(message.MsgChannel, out var session))
            {
                SendError(message, "The server could not resolve the requesting session.");
                return;
            }

            if (!_admin.IsAdmin(session, true) || !_admin.HasAdminFlag(session, AdminFlags.Host))
            {
                _sawmill.Warning($"Rejected mapping snapshot request {message.RequestId} from {session.Name}: " +
                                 "host permission is required.");
                SendError(message, "Host permission is required to save a mapping snapshot.");
                return;
            }

            if (!_ent.TryGetComponent(session.AttachedEntity, out TransformComponent? xform) ||
                xform.MapUid is not { } mapUid)
            {
                SendError(message, "The attached entity is not on a map that can be saved.");
                return;
            }

            var snapshots = _systems.GetEntitySystem<MappingSnapshotSystem>();
            if (!snapshots.TryCreateMapSnapshotText(mapUid, out var yaml, out var report, out var error))
            {
                SendError(message, error);
                return;
            }

            _sawmill.Info(
                $"Created mapping snapshot for {mapUid}; excluded {report.ExcludedRoots} transient roots " +
                $"(players={report.PlayerRoots}, minds={report.MindRoots}, explicit={report.ExplicitTransientRoots}) " +
                $"and {report.TransientComponents} transient components; normalized " +
                $"{report.NormalizedReferences} invalid references and validated " +
                $"{report.ValidatedEntities} serialized entities.");
            var msg = new MappingMapDataMessage()
            {
                Context = _zstd,
                RequestId = message.RequestId,
                Yml = yaml
            };
            _net.ServerSendMessage(msg, message.MsgChannel);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Error saving map in mapping mode:\n{e}");
            SendError(message, "The server failed to prepare the mapping snapshot.");
        }
#endif
    }

#if !FULL_RELEASE
    private void SendError(MappingSaveMapMessage request, string error)
    {
        _net.ServerSendMessage(new MappingSaveMapErrorMessage
        {
            RequestId = request.RequestId,
            Error = error,
        }, request.MsgChannel);
    }
#endif
}
