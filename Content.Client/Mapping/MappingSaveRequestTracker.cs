using System.Threading.Tasks;

namespace Content.Client.Mapping;

internal sealed class MappingSaveRequestTracker
{
    private readonly object _sync = new();
    private PendingRequest? _pending;
    private uint _nextRequestId;

    public bool HasPending
    {
        get
        {
            lock (_sync)
            {
                return _pending != null;
            }
        }
    }

    public bool TryBegin(out uint requestId, out Task<MappingSaveResponse> response)
    {
        lock (_sync)
        {
            if (_pending != null)
            {
                requestId = 0;
                response = Task.FromResult(default(MappingSaveResponse));
                return false;
            }

            _nextRequestId = unchecked(_nextRequestId + 1);
            if (_nextRequestId == 0)
                _nextRequestId = 1;

            var completion = new TaskCompletionSource<MappingSaveResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = new PendingRequest(_nextRequestId, completion);
            requestId = _nextRequestId;
            response = completion.Task;
            return true;
        }
    }

    public bool TryCompleteData(uint requestId, string yml)
    {
        return TryComplete(requestId, new MappingSaveResponse(yml, null));
    }

    public bool TryCompleteError(uint requestId, string error)
    {
        return TryComplete(requestId, new MappingSaveResponse(null, error));
    }

    public bool TryEnd(uint requestId)
    {
        lock (_sync)
        {
            if (_pending?.RequestId != requestId)
                return false;

            _pending = null;
            return true;
        }
    }

    private bool TryComplete(uint requestId, MappingSaveResponse response)
    {
        TaskCompletionSource<MappingSaveResponse> completion;
        lock (_sync)
        {
            if (_pending?.RequestId != requestId)
                return false;

            completion = _pending.Completion;
        }

        return completion.TrySetResult(response);
    }

    private sealed record PendingRequest(
        uint RequestId,
        TaskCompletionSource<MappingSaveResponse> Completion);
}

internal readonly record struct MappingSaveResponse(string? Yml, string? Error);
