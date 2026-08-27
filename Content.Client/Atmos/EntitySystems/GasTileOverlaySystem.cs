using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using JetBrains.Annotations;
using Robust.Shared.GameStates;

namespace Content.Client.Atmos.EntitySystems;

[UsedImplicitly]
public sealed class GasTileOverlaySystem : SharedGasTileOverlaySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<GasOverlayUpdateEvent>(HandleGasOverlayUpdate);
        SubscribeLocalEvent<GasTileOverlayComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(EntityUid gridUid, GasTileOverlayComponent comp, ref ComponentHandleState args)
    {
        Dictionary<Vector2i, GasOverlayChunk> modifiedChunks;
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> modifiedZLevelChunks;
        Dictionary<int, HashSet<Vector2i>> allZLevelChunks;

        switch (args.Current)
        {
            // is this a delta or full state?
            case GasTileOverlayDeltaState delta:
            {
                modifiedChunks = delta.ModifiedChunks;
                modifiedZLevelChunks = delta.ModifiedZLevelChunks;
                allZLevelChunks = delta.AllZLevelChunks;
                foreach (var index in comp.Chunks.Keys)
                {
                    if (!delta.AllChunks.Contains(index))
                        comp.Chunks.Remove(index);
                }

                break;
            }
            case GasTileOverlayState state:
            {
                modifiedChunks = state.Chunks;
                modifiedZLevelChunks = state.ZLevelChunks;
                allZLevelChunks = new(state.ZLevelChunks.Count);
                foreach (var (localZ, chunks) in state.ZLevelChunks)
                {
                    allZLevelChunks[localZ] = new(chunks.Keys);
                }

                foreach (var index in comp.Chunks.Keys)
                {
                    if (!state.Chunks.ContainsKey(index))
                        comp.Chunks.Remove(index);
                }

                break;
            }
            default:
                return;
        }

        foreach (var (index, data) in modifiedChunks)
        {
            comp.Chunks[index] = data;
        }

        ApplyZLevelState(comp, modifiedZLevelChunks, allZLevelChunks);
    }

    private static void ApplyZLevelState(
        GasTileOverlayComponent component,
        Dictionary<int, Dictionary<Vector2i, GasOverlayChunk>> modified,
        Dictionary<int, HashSet<Vector2i>> all)
    {
        foreach (var localZ in component.ZLevelChunks.Keys)
        {
            if (!all.ContainsKey(localZ))
                component.ZLevelChunks.Remove(localZ);
        }

        foreach (var (localZ, allChunks) in all)
        {
            var layer = component.GetOrNewChunks(localZ);
            foreach (var index in layer.Keys)
            {
                if (!allChunks.Contains(index))
                    layer.Remove(index);
            }

            if (!modified.TryGetValue(localZ, out var changed))
                continue;

            foreach (var (index, chunk) in changed)
            {
                layer[index] = chunk;
            }
        }
    }

    private void HandleGasOverlayUpdate(GasOverlayUpdateEvent ev)
    {
        foreach (var nent in ev.ClearedGrids)
        {
            var grid = GetEntity(nent);
            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            comp.Chunks.Clear();
            comp.ZLevelChunks.Clear();
        }

        foreach (var (nent, removedIndices) in ev.RemovedChunks)
        {
            var grid = GetEntity(nent);

            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            foreach (var index in removedIndices)
            {
                if (!comp.TryGetChunks(index.LocalZ, out var chunks))
                    continue;

                chunks.Remove(index.Indices);
                if (index.LocalZ != 0 && chunks.Count == 0)
                    comp.ZLevelChunks.Remove(index.LocalZ);
            }
        }

        foreach (var (nent, gridData) in ev.UpdatedChunks)
        {
            var grid = GetEntity(nent);

            if (!TryComp(grid, out GasTileOverlayComponent? comp))
                continue;

            foreach (var chunkData in gridData)
            {
                comp.GetOrNewChunks(chunkData.LocalZ)[chunkData.Index] = chunkData;
            }
        }
    }
}
