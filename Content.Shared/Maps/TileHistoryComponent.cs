using System.Linq;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Maps;

[RegisterComponent, NetworkedComponent]
public sealed partial class TileHistoryComponent : Component
{
    // History of tiles for each grid chunk.
    [DataField]
    public Dictionary<Vector2i, TileHistoryChunk> ChunkHistory = new();

    // Sparse history for non-zero Z-level tiles.
    [DataField]
    public Dictionary<ZLevelTileIndices, ZLevelTileHistory> ZLevelHistory = new();

    /// <summary>
    ///     Tick at which PVS was last toggled. Ensures that all players receive a full update when toggling PVS.
    /// </summary>
    public GameTick ForceTick { get; set; }
}

[Serializable, NetSerializable]
public sealed class TileHistoryState : ComponentState
{
    public Dictionary<Vector2i, TileHistoryChunk> ChunkHistory;
    public Dictionary<ZLevelTileIndices, ZLevelTileHistory> ZLevelHistory;

    public TileHistoryState(
        Dictionary<Vector2i, TileHistoryChunk> chunkHistory,
        Dictionary<ZLevelTileIndices, ZLevelTileHistory> zLevelHistory)
    {
        ChunkHistory = chunkHistory;
        ZLevelHistory = zLevelHistory;
    }
}

[Serializable, NetSerializable]
public sealed class TileHistoryDeltaState : ComponentState, IComponentDeltaState<TileHistoryState>
{
    public Dictionary<Vector2i, TileHistoryChunk> ChunkHistory;
    public HashSet<Vector2i> AllHistoryChunks;
    public Dictionary<ZLevelTileIndices, ZLevelTileHistory> ZLevelHistory;
    public HashSet<ZLevelTileIndices> AllZLevelHistory;

    public TileHistoryDeltaState(
        Dictionary<Vector2i, TileHistoryChunk> chunkHistory,
        HashSet<Vector2i> allHistoryChunks,
        Dictionary<ZLevelTileIndices, ZLevelTileHistory> zLevelHistory,
        HashSet<ZLevelTileIndices> allZLevelHistory)
    {
        ChunkHistory = chunkHistory;
        AllHistoryChunks = allHistoryChunks;
        ZLevelHistory = zLevelHistory;
        AllZLevelHistory = allZLevelHistory;
    }

    public void ApplyToFullState(TileHistoryState state)
    {
        var toRemove = new List<Vector2i>();
        foreach (var key in state.ChunkHistory.Keys)
        {
            if (!AllHistoryChunks.Contains(key))
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
        {
            state.ChunkHistory.Remove(key);
        }

        foreach (var (indices, chunk) in ChunkHistory)
        {
            state.ChunkHistory[indices] = new TileHistoryChunk(chunk);
        }

        ApplyZLevelHistory(state.ZLevelHistory);
    }

    public void ApplyToComponent(TileHistoryComponent component)
    {
        var toRemove = new List<Vector2i>();
        foreach (var key in component.ChunkHistory.Keys)
        {
            if (!AllHistoryChunks.Contains(key))
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
        {
            component.ChunkHistory.Remove(key);
        }

        foreach (var (indices, chunk) in ChunkHistory)
        {
            component.ChunkHistory[indices] = new TileHistoryChunk(chunk);
        }

        ApplyZLevelHistory(component.ZLevelHistory);
    }

    public TileHistoryState CreateNewFullState(TileHistoryState state)
    {
        var chunks = new Dictionary<Vector2i, TileHistoryChunk>(state.ChunkHistory.Count);

        foreach (var (indices, chunk) in ChunkHistory)
        {
            chunks[indices] = new TileHistoryChunk(chunk);
        }

        foreach (var (indices, chunk) in state.ChunkHistory)
        {
            if (AllHistoryChunks.Contains(indices))
                chunks.TryAdd(indices, new TileHistoryChunk(chunk));
        }

        var zLevelHistory = CloneZLevelHistory(state.ZLevelHistory);
        foreach (var (indices, history) in ZLevelHistory)
        {
            zLevelHistory[indices] = new ZLevelTileHistory(history);
        }

        foreach (var indices in zLevelHistory.Keys.ToArray())
        {
            if (!AllZLevelHistory.Contains(indices))
                zLevelHistory.Remove(indices);
        }

        return new TileHistoryState(chunks, zLevelHistory);
    }

    private void ApplyZLevelHistory(Dictionary<ZLevelTileIndices, ZLevelTileHistory> target)
    {
        foreach (var indices in target.Keys.ToArray())
        {
            if (!AllZLevelHistory.Contains(indices))
                target.Remove(indices);
        }

        foreach (var (indices, history) in ZLevelHistory)
        {
            target[indices] = new ZLevelTileHistory(history);
        }
    }

    private static Dictionary<ZLevelTileIndices, ZLevelTileHistory> CloneZLevelHistory(
        Dictionary<ZLevelTileIndices, ZLevelTileHistory> source)
    {
        var clone = new Dictionary<ZLevelTileIndices, ZLevelTileHistory>(source.Count);
        foreach (var (indices, history) in source)
        {
            clone[indices] = new ZLevelTileHistory(history);
        }

        return clone;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class TileHistoryChunk
{
    [DataField]
    public Dictionary<Vector2i, List<ProtoId<ContentTileDefinition>>> History = new();

    [ViewVariables]
    public GameTick LastModified;

    public TileHistoryChunk()
    {
    }

    public TileHistoryChunk(TileHistoryChunk other)
    {
        History = new Dictionary<Vector2i, List<ProtoId<ContentTileDefinition>>>(other.History.Count);
        foreach (var (key, value) in other.History)
        {
            History[key] = new List<ProtoId<ContentTileDefinition>>(value);
        }
        LastModified = other.LastModified;
    }
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class ZLevelTileHistory
{
    [DataField]
    public List<ProtoId<ContentTileDefinition>> History = new();

    [ViewVariables]
    public GameTick LastModified;

    public ZLevelTileHistory()
    {
    }

    public ZLevelTileHistory(ZLevelTileHistory other)
    {
        History = new List<ProtoId<ContentTileDefinition>>(other.History);
        LastModified = other.LastModified;
    }
}
