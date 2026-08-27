using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared.Atmos.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class GasTileOverlayComponent : Component
{
    /// <summary>
    ///     The tiles that have had their atmos data updated since last tick
    /// </summary>
    public readonly HashSet<Vector2i> InvalidTiles = new();

    /// <summary>
    ///     Non-zero grid-local tiles whose overlay data needs to be rebuilt.
    /// </summary>
    public readonly HashSet<ZLevelTileIndices> InvalidZLevelTiles = new();

    /// <summary>
    ///     Gas data stored in chunks to make PVS / bubbling easier.
    /// </summary>
    public readonly Dictionary<Vector2i, GasOverlayChunk> Chunks = new();

    /// <summary>
    ///     Gas overlay chunks for non-zero grid-local Z-levels.
    /// </summary>
    public readonly Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> ZLevelChunks = new();

    public bool TryGetChunks(int localZ, out Dictionary<Vector2i, GasOverlayChunk> chunks)
    {
        if (localZ == 0)
        {
            chunks = Chunks;
            return true;
        }

        return ZLevelChunks.TryGetValue(localZ, out chunks!);
    }

    public Dictionary<Vector2i, GasOverlayChunk> GetOrNewChunks(int localZ)
    {
        if (localZ == 0)
            return Chunks;

        if (!ZLevelChunks.TryGetValue(localZ, out var chunks))
        {
            chunks = new();
            ZLevelChunks[localZ] = chunks;
        }

        return chunks;
    }

    /// <summary>
    ///     Tick at which PVS was last toggled. Ensures that all players receive a full update when toggling PVS.
    /// </summary>
    public GameTick ForceTick { get; set; }
}

[Serializable, NetSerializable]
public sealed class GasTileOverlayState(
    Dictionary<Vector2i, GasOverlayChunk> chunks,
    Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> zLevelChunks) : ComponentState
{
    public readonly Dictionary<Vector2i, GasOverlayChunk> Chunks = chunks;
    public readonly Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> ZLevelChunks = zLevelChunks;
}

[Serializable, NetSerializable]
public sealed class GasTileOverlayDeltaState(
    Dictionary<Vector2i, GasOverlayChunk> modifiedChunks,
    HashSet<Vector2i> allChunks,
    Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> modifiedZLevelChunks,
    Dictionary<int, HashSet<Vector2i>> allZLevelChunks)
    : ComponentState, IComponentDeltaState<GasTileOverlayState>
{
    public readonly Dictionary<Vector2i, GasOverlayChunk> ModifiedChunks = modifiedChunks;
    public readonly HashSet<Vector2i> AllChunks = allChunks;
    public readonly Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> ModifiedZLevelChunks = modifiedZLevelChunks;
    public readonly Dictionary<int, HashSet<Vector2i>> AllZLevelChunks = allZLevelChunks;

    public void ApplyToFullState(GasTileOverlayState state)
    {
        foreach (var key in state.Chunks.Keys)
        {
            if (!AllChunks.Contains(key))
                state.Chunks.Remove(key);
        }

        foreach (var (chunk, data) in ModifiedChunks)
        {
            state.Chunks[chunk] = new(data);
        }

        ApplyZLevelDelta(state.ZLevelChunks, ModifiedZLevelChunks, AllZLevelChunks);
    }

    public GasTileOverlayState CreateNewFullState(GasTileOverlayState state)
    {
        var chunks = new Dictionary<Vector2i, GasOverlayChunk>(AllChunks.Count);

        foreach (var (chunk, data) in ModifiedChunks)
        {
            chunks[chunk] = new(data);
        }

        foreach (var (chunk, data) in state.Chunks)
        {
            if (AllChunks.Contains(chunk))
                chunks.TryAdd(chunk, new(data));
        }

        var zLevelChunks = CreateZLevelFullState(state.ZLevelChunks, ModifiedZLevelChunks, AllZLevelChunks);
        return new GasTileOverlayState(chunks, zLevelChunks);
    }

    private static void ApplyZLevelDelta(
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> state,
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> modified,
        Dictionary<int, HashSet<Vector2i>> all)
    {
        foreach (var localZ in state.Keys)
        {
            if (!all.ContainsKey(localZ))
                state.Remove(localZ);
        }

        foreach (var (localZ, allChunks) in all)
        {
            if (!state.TryGetValue(localZ, out var layer))
            {
                layer = new();
                state[localZ] = layer;
            }

            foreach (var index in layer.Keys)
            {
                if (!allChunks.Contains(index))
                    layer.Remove(index);
            }

            if (!modified.TryGetValue(localZ, out var modifiedLayer))
                continue;

            foreach (var (index, chunk) in modifiedLayer)
            {
                layer[index] = new(chunk);
            }
        }
    }

    private static Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> CreateZLevelFullState(
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> state,
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> modified,
        Dictionary<int, HashSet<Vector2i>> all)
    {
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> result = new(all.Count);
        foreach (var (localZ, allChunks) in all)
        {
            Dictionary<Vector2i, GasOverlayChunk> layer = new(allChunks.Count);
            if (modified.TryGetValue(localZ, out var modifiedLayer))
            {
                foreach (var (index, chunk) in modifiedLayer)
                {
                    layer[index] = new(chunk);
                }
            }

            if (state.TryGetValue(localZ, out var stateLayer))
            {
                foreach (var (index, chunk) in stateLayer)
                {
                    if (allChunks.Contains(index))
                        layer.TryAdd(index, new(chunk));
                }
            }

            result[localZ] = layer;
        }

        return result;
    }
}
