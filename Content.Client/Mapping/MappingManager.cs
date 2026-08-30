using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.Mapping;
using Robust.Client.UserInterface;
using Robust.Shared.Localization;
using Robust.Shared.Network;

namespace Content.Client.Mapping;

public sealed class MappingManager : IPostInjectInit
{
    [Dependency] private readonly IFileDialogManager _file = default!;
    [Dependency] private readonly ILocalizationManager _loc = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    internal static readonly TimeSpan SaveRequestTimeout = TimeSpan.FromSeconds(30);

    private readonly MappingSaveRequestTracker _requests = new();

    public void PostInject()
    {
        _net.RegisterNetMessage<MappingSaveMapMessage>();
        _net.RegisterNetMessage<MappingSaveMapErrorMessage>(OnSaveError);
        _net.RegisterNetMessage<MappingMapDataMessage>(OnMapData);
    }

    private void OnSaveError(MappingSaveMapErrorMessage message)
    {
        var requestId = message.RequestId;
        var error = message.Error;
        _ui.DeferAction(() => _requests.TryCompleteError(requestId, error));
    }

    private void OnMapData(MappingMapDataMessage message)
    {
        var requestId = message.RequestId;
        var yml = message.Yml;
        _ui.DeferAction(() => _requests.TryCompleteData(requestId, yml));
    }

    public async Task<MappingSaveResult> SaveMap()
    {
        if (!_requests.TryBegin(out var requestId, out var responseTask))
        {
            ShowError(_loc.GetString("mapping-save-busy"));
            return MappingSaveResult.Busy;
        }

        try
        {
            _net.ClientSendMessage(new MappingSaveMapMessage { RequestId = requestId });

            var timedOut = false;
            using var timeoutCancellation = new CancellationTokenSource();
            var timeoutTask = Task.Delay(SaveRequestTimeout, timeoutCancellation.Token);
            if (await Task.WhenAny(responseTask, timeoutTask) != responseTask)
            {
                timedOut = _requests.TryCompleteError(
                    requestId,
                    _loc.GetString("mapping-save-timeout"));
            }
            else
            {
                timeoutCancellation.Cancel();
            }

            var response = await responseTask;
            if (response.Error != null)
            {
                ShowError(response.Error);
                return timedOut ? MappingSaveResult.TimedOut : MappingSaveResult.ServerRejected;
            }

            if (response.Yml == null)
            {
                ShowError(_loc.GetString("mapping-save-invalid-response"));
                return MappingSaveResult.ServerRejected;
            }

            if (!await _file.SaveFileAtomic(EncodeSnapshot(response.Yml)))
                return MappingSaveResult.Cancelled;

            return MappingSaveResult.Saved;
        }
        catch (Exception exception)
        {
            ShowError(_loc.GetString("mapping-save-client-error", ("reason", exception.Message)));
            return MappingSaveResult.ClientError;
        }
        finally
        {
            _requests.TryEnd(requestId);
        }
    }

    private void ShowError(string error)
    {
        _ui.DeferAction(() => _ui.Popup(
            error,
            _loc.GetString("mapping-save-error-title")));
    }

    internal static byte[] EncodeSnapshot(string yml)
    {
        for (var i = 0; i < yml.Length; i++)
        {
            if (!char.IsSurrogate(yml[i]))
                continue;

            if (!char.IsHighSurrogate(yml[i]) ||
                i + 1 >= yml.Length ||
                !char.IsLowSurrogate(yml[i + 1]))
            {
                throw new ArgumentException("Mapping snapshots must contain valid UTF-16.", nameof(yml));
            }

            i++;
        }

        return Encoding.UTF8.GetBytes(yml);
    }
}

public enum MappingSaveResult : byte
{
    Saved,
    Cancelled,
    Busy,
    TimedOut,
    ServerRejected,
    ClientError,
}
