using Content.Server.Atmos.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;
using Content.Shared.CCVar;
using Content.Shared.Chunking;
using Content.Shared.GameTicking;
using Content.Shared.Rounding;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using JetBrains.Annotations;
using Microsoft.Extensions.ObjectPool;
using Robust.Server.Player;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Threading;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Runtime.CompilerServices;

// ReSharper disable once RedundantUsingDirective

namespace Content.Server.Atmos.EntitySystems
{
    [UsedImplicitly]
    public sealed class GasTileOverlaySystem : SharedGasTileOverlaySystem
    {
        [Robust.Shared.IoC.Dependency] private readonly IGameTiming _gameTiming = default!;
        [Robust.Shared.IoC.Dependency] private readonly IPlayerManager _playerManager = default!;
        [Robust.Shared.IoC.Dependency] private readonly IMapManager _mapManager = default!;
        [Robust.Shared.IoC.Dependency] private readonly IParallelManager _parMan = default!;
        [Robust.Shared.IoC.Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
        [Robust.Shared.IoC.Dependency] private readonly ChunkingSystem _chunkingSys = default!;
        [Robust.Shared.IoC.Dependency] private readonly SharedTransformSystem _transformSystem = default!;
        [Robust.Shared.IoC.Dependency] private readonly SharedZLevelMetricsSystem _zLevelMetrics = default!;

        /// <summary>
        /// Per-tick cache of sessions.
        /// </summary>
        private readonly List<ICommonSession> _sessions = new();
        private UpdatePlayerJob _updateJob;

        private readonly Dictionary<ICommonSession, Dictionary<GasOverlayGridLayer, HashSet<Vector2i>>> _lastSentChunks = new();
        private readonly Dictionary<ICommonSession, HashSet<int>> _viewedWorldZLevels = new();
        private readonly HashSet<(EntityUid Grid, int LocalZ)> _rebuiltUpperLayers = new();
        private readonly HashSet<(EntityUid Grid, int LocalZ, Vector2i Chunk)> _updatedOverlayChunks = new();

        // Oh look its more duplicated decal system code!
        private ObjectPool<HashSet<Vector2i>> _chunkIndexPool =
            new DefaultObjectPool<HashSet<Vector2i>>(
                new DefaultPooledObjectPolicy<HashSet<Vector2i>>(), 64);
        private ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> _chunkViewerPool =
            new DefaultObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>>(
                new DefaultPooledObjectPolicy<Dictionary<NetEntity, HashSet<Vector2i>>>(), 64);

        private bool _doSessionUpdate;

        /// <summary>
        ///     Overlay update interval, in seconds.
        /// </summary>
        private float _updateInterval;

        private int _thresholds;
        private EntityQuery<MapGridComponent> _gridQuery;
        private EntityQuery<GasTileOverlayComponent> _query;
        private EntityQuery<TransformComponent> _transformQuery;
        private EntityQuery<ZLevelFrameComponent> _frameQuery;
        private EntityQuery<ZLevelPositionComponent> _zLevelPositionQuery;

        public override void Initialize()
        {
            base.Initialize();

            _query = GetEntityQuery<GasTileOverlayComponent>();
            _gridQuery = GetEntityQuery<MapGridComponent>();
            _transformQuery = GetEntityQuery<TransformComponent>();
            _frameQuery = GetEntityQuery<ZLevelFrameComponent>();
            _zLevelPositionQuery = GetEntityQuery<ZLevelPositionComponent>();

            _updateJob = new UpdatePlayerJob()
            {
                EntManager = EntityManager,
                System = this,
                ChunkIndexPool = _chunkIndexPool,
                Sessions = _sessions,
                ChunkingSys = _chunkingSys,
                MapManager = _mapManager,
                ChunkViewerPool = _chunkViewerPool,
                LastSentChunks = _lastSentChunks,
                ViewedWorldZLevels = _viewedWorldZLevels,
                GridQuery = _gridQuery,
                FrameQuery = _frameQuery,
            };

            _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;

            InitializeCVars();

            SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
            SubscribeLocalEvent<GasTileOverlayComponent, ComponentStartup>(OnStartup);
        }

        private void OnStartup(EntityUid uid, GasTileOverlayComponent component, ComponentStartup args)
        {
            // This **shouldn't** be required, but just in case we ever get entity prototypes that have gas overlays, we
            // need to ensure that we send an initial full state to players.
            Dirty(uid, component);
        }

        public override void Shutdown()
        {
            base.Shutdown();
            _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
        }

        private void OnPvsToggle(bool value)
        {
            if (value == PvsEnabled)
                return;

            PvsEnabled = value;

            if (value)
            {
                ClearClientOverlayData();
                return;
            }

            foreach (var lastSent in _lastSentChunks.Values)
            {
                foreach (var set in lastSent.Values)
                {
                    set.Clear();
                    _chunkIndexPool.Return(set);
                }
                lastSent.Clear();
            }

            // PVS was turned off, ensure data gets sent to all clients.
            var query = AllEntityQuery<GasTileOverlayComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var grid, out var meta))
            {
                grid.ForceTick = _gameTiming.CurTick;
                Dirty(uid, grid, meta);
            }
        }

        private void ClearClientOverlayData()
        {
            var ev = new GasOverlayUpdateEvent();
            var query = AllEntityQuery<GasTileOverlayComponent>();
            while (query.MoveNext(out var uid, out _))
                ev.ClearedGrids.Add(GetNetEntity(uid));

            if (ev.ClearedGrids.Count == 0)
                return;

            foreach (var session in _playerManager.Sessions)
            {
                if (session.Status == SessionStatus.InGame)
                    RaiseNetworkEvent(ev, session.Channel);
            }
        }

        private void UpdateTickRate(float value) => _updateInterval = value > 0.0f ? 1 / value : float.MaxValue;
        private void UpdateThresholds(int value) => _thresholds = value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(Entity<GasTileOverlayComponent?> grid, Vector2i index)
        {
            if (_query.Resolve(grid.Owner, ref grid.Comp))
                grid.Comp.InvalidTiles.Add(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Invalidate(Entity<GasTileOverlayComponent?> grid, ZLevelTileIndices index)
        {
            if (!_query.Resolve(grid.Owner, ref grid.Comp))
                return;

            if (index.Z == 0)
                grid.Comp.InvalidTiles.Add(new Vector2i(index.X, index.Y));
            else
                grid.Comp.InvalidZLevelTiles.Add(index);
        }

        private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
        {
            if (e.NewStatus != SessionStatus.InGame)
            {
                if (_lastSentChunks.Remove(e.Session, out var sets))
                {
                    foreach (var set in sets.Values)
                    {
                        set.Clear();
                        _chunkIndexPool.Return(set);
                    }
                }

                _viewedWorldZLevels.Remove(e.Session);
                return;
            }

            if (!_lastSentChunks.ContainsKey(e.Session))
            {
                _lastSentChunks[e.Session] = new();
            }

            if (!_viewedWorldZLevels.ContainsKey(e.Session))
                _viewedWorldZLevels[e.Session] = new();
        }

        private byte GetOpacity(float moles, float molesVisible, float molesVisibleMax)
        {
            return (byte) (ContentHelpers.RoundToLevels(
                MathHelper.Clamp01((moles - molesVisible) /
                                   (molesVisibleMax - molesVisible)) * 255, byte.MaxValue,
                _thresholds) * 255 / (_thresholds - 1));
        }

        public GasOverlayData GetOverlayData(GasMixture? mixture)
        {
            ThermalByte byteTemp;
            if (mixture == null)
            {
                byteTemp = new();
                byteTemp.SetVacuum();
            }
            else
                byteTemp = new(mixture.Temperature);

            var data = new GasOverlayData(0, new byte[VisibleGasId.Length], byteTemp);

            for (var i = 0; i < VisibleGasId.Length; i++)
            {
                var id = VisibleGasId[i];
                var gas = _atmosphereSystem.GetGas(id);
                var moles = mixture?[id] ?? 0f;
                ref var opacity = ref data.Opacity[i];

                if (moles < gas.GasMolesVisible)
                {
                    continue;
                }

                opacity = (byte) (ContentHelpers.RoundToLevels(
                    MathHelper.Clamp01((moles - gas.GasMolesVisible) /
                                       (gas.GasMolesVisibleMax - gas.GasMolesVisible)) * 255, byte.MaxValue,
                    _thresholds) * 255 / (_thresholds - 1));
            }

            return data;
        }

        /// <summary>
        ///     Updates the visuals for a tile on some grid chunk. Returns true if the visuals have changed.
        /// </summary>
        private bool UpdateChunkTile(
            GridAtmosphereComponent gridAtmosphere,
            GasOverlayChunk chunk,
            ZLevelTileIndices indices)
        {
            var index = new Vector2i(indices.X, indices.Y);
            ref var oldData = ref chunk.TileData[chunk.GetDataIndex(index)];
            TileAtmosphere? tile;
            var found = indices.Z == 0
                ? gridAtmosphere.Tiles.TryGetValue(index, out tile)
                : gridAtmosphere.ZLevelTiles.TryGetValue(indices, out tile);
            if (!found || tile == null)
            {
                if (oldData.Equals(default))
                    return false;

                chunk.LastUpdate = _gameTiming.CurTick;
                oldData = default;
                return true;
            }

            var changed = false;

            ThermalByte newByteTemp = new();

            if (tile.Hotspot.Valid)
                newByteTemp.SetTemperature(tile.Hotspot.Temperature);
            else if (!tile.Space && tile.Air?.TotalMoles <= 5f)
                newByteTemp.SetVacuum();
            else if (!tile.Space && tile.Air != null)
                newByteTemp = new(tile.Air.Temperature);

            if (oldData.Equals(default))
            {
                changed = true;
                oldData = new GasOverlayData(tile.Hotspot.State, new byte[VisibleGasId.Length], newByteTemp);
            }
            else if (oldData.FireState != tile.Hotspot.State ||
                     Math.Abs(oldData.ByteGasTemperature.Value - newByteTemp.Value) > 1 || // Dirty Temperature when there is more then 1 byte difference. That should measure up to minimum 4 degreese difference, 6 degreese on average.
                     (oldData.ByteGasTemperature.Value != newByteTemp.Value && newByteTemp.Value > ThermalByte.TempResolution)) // change of special ThermalByte value
            {
                changed = true;
                oldData = new GasOverlayData(tile.Hotspot.State, oldData.Opacity, newByteTemp);
            }

            if (tile is {Air: not null, NoGridTile: false})
            {
                for (var i = 0; i < VisibleGasId.Length; i++)
                {
                    var id = VisibleGasId[i];
                    var gas = _atmosphereSystem.GetGas(id);
                    var moles = tile.Air[id];
                    ref var oldOpacity = ref oldData.Opacity[i];

                    if (moles < gas.GasMolesVisible)
                    {
                        if (oldOpacity != 0)
                        {
                            oldOpacity = 0;
                            changed = true;
                        }

                        continue;
                    }

                    var opacity = GetOpacity(moles, gas.GasMolesVisible, gas.GasMolesVisibleMax);

                    if (oldOpacity == opacity)
                        continue;

                    oldOpacity = opacity;
                    changed = true;
                }
            }
            else
            {
                for (var i = 0; i < VisibleGasId.Length; i++)
                {
                    changed |= oldData.Opacity[i] != 0;
                    oldData.Opacity[i] = 0;
                }
            }

            if (!changed)
                return false;

            chunk.LastUpdate = _gameTiming.CurTick;
            return true;
        }

        private void UpdateOverlayData()
        {
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var invalidatedTiles = 0;
            var invalidatedUpperTiles = 0;
            _rebuiltUpperLayers.Clear();
            _updatedOverlayChunks.Clear();

            // TODO parallelize?
            var query = AllEntityQuery<GasTileOverlayComponent, GridAtmosphereComponent, MetaDataComponent>();
            while (query.MoveNext(out var uid, out var overlay, out var gam, out var meta))
            {
                var changed = false;
                foreach (var index in overlay.InvalidTiles)
                {
                    invalidatedTiles++;
                    var chunkIndex = GetGasChunkIndices(index);

                    if (!overlay.Chunks.TryGetValue(chunkIndex, out var chunk))
                        overlay.Chunks[chunkIndex] = chunk = new GasOverlayChunk(chunkIndex);

                    if (UpdateChunkTile(gam, chunk, new ZLevelTileIndices(index.X, index.Y, 0)))
                    {
                        changed = true;
                        _updatedOverlayChunks.Add((uid, 0, chunkIndex));
                    }
                }

                foreach (var index in overlay.InvalidZLevelTiles)
                {
                    invalidatedTiles++;
                    invalidatedUpperTiles++;
                    var chunkIndex = GetGasChunkIndices(new Vector2i(index.X, index.Y));
                    var layer = overlay.GetOrNewChunks(index.Z);
                    _rebuiltUpperLayers.Add((uid, index.Z));

                    if (!layer.TryGetValue(chunkIndex, out var chunk))
                        layer[chunkIndex] = chunk = new GasOverlayChunk(chunkIndex, index.Z);

                    if (UpdateChunkTile(gam, chunk, index))
                    {
                        changed = true;
                        _updatedOverlayChunks.Add((uid, index.Z, chunkIndex));
                    }
                }

                if (changed)
                    Dirty(uid, overlay, meta);

                overlay.InvalidTiles.Clear();
                overlay.InvalidZLevelTiles.Clear();
            }

            if (invalidatedTiles != 0)
            {
                _zLevelMetrics.RecordAtmosOverlayUpdate(
                    invalidatedTiles,
                    invalidatedUpperTiles,
                    _rebuiltUpperLayers.Count,
                    _updatedOverlayChunks.Count,
                    System.Diagnostics.Stopwatch.GetTimestamp() - started);
            }
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);
            AccumulatedFrameTime += frameTime;

            if (_doSessionUpdate)
            {
                UpdateSessions();
                return;
            }

            if (AccumulatedFrameTime < _updateInterval)
                return;

            AccumulatedFrameTime -= _updateInterval;

            // First, update per-chunk visual data for any invalidated tiles.
            UpdateOverlayData();

            // Then, next tick we send the data to players.
            // This is to avoid doing all the work in the same tick.
            _doSessionUpdate = true;
        }

        public void UpdateSessions()
        {
            _doSessionUpdate = false;

            if (!PvsEnabled)
                return;

            // Now we'll go through each player, then through each chunk in range of that player checking if the player is still in range
            // If they are, check if they need the new data to send (i.e. if there's an overlay for the gas).
            // Afterwards we reset all the chunk data for the next time we tick.
            _sessions.Clear();

            foreach (var player in _playerManager.Sessions)
            {
                if (player.Status != SessionStatus.InGame)
                    continue;

                _sessions.Add(player);
                RefreshViewedWorldZLevels(player);
            }

            if (_sessions.Count == 0)
                return;

            _parMan.ProcessNow(_updateJob, _sessions.Count);
            _updateJob.LastSessionUpdate = _gameTiming.CurTick;
        }

        private void RefreshViewedWorldZLevels(ICommonSession session)
        {
            if (!_viewedWorldZLevels.TryGetValue(session, out var levels))
            {
                levels = new();
                _viewedWorldZLevels[session] = levels;
            }

            levels.Clear();
            if (session.AttachedEntity is { } attached)
                AddViewedWorldZLevel(attached, levels);

            foreach (var viewer in session.ViewSubscriptions)
            {
                AddViewedWorldZLevel(viewer, levels);
            }

            if (levels.Count == 0)
                levels.Add(0);
        }

        private void AddViewedWorldZLevel(EntityUid viewer, HashSet<int> levels)
        {
            if (!_transformQuery.TryComp(viewer, out var transform) || transform.MapID == MapId.Nullspace)
                return;

            levels.Add(_transformSystem.GetWorldZLevel((viewer, transform, _zLevelPositionQuery.CompOrNull(viewer))));
        }

        public void Reset(RoundRestartCleanupEvent ev)
        {
            foreach (var data in _lastSentChunks.Values)
            {
                foreach (var previous in data.Values)
                {
                    previous.Clear();
                    _chunkIndexPool.Return(previous);
                }

                data.Clear();
            }
        }

        #region Jobs

        /// <summary>
        /// Updates per player gas overlay data.
        /// </summary>
        private record struct UpdatePlayerJob : IParallelRobustJob
        {
            public int BatchSize => 2;

            public IEntityManager EntManager;
            public IMapManager MapManager;
            public ChunkingSystem ChunkingSys;
            public GasTileOverlaySystem System;
            public ObjectPool<HashSet<Vector2i>> ChunkIndexPool;
            public ObjectPool<Dictionary<NetEntity, HashSet<Vector2i>>> ChunkViewerPool;

            public GameTick LastSessionUpdate;
            public Dictionary<ICommonSession, Dictionary<GasOverlayGridLayer, HashSet<Vector2i>>> LastSentChunks;
            public Dictionary<ICommonSession, HashSet<int>> ViewedWorldZLevels;
            public List<ICommonSession> Sessions;

            public EntityQuery<MapGridComponent> GridQuery;
            public EntityQuery<ZLevelFrameComponent> FrameQuery;

            public void Execute(int index)
            {
                var playerSession = Sessions[index];
                var chunksInRange = ChunkingSys.GetChunksForSession(playerSession, ChunkSize, ChunkIndexPool, ChunkViewerPool);
                var previouslySent = LastSentChunks[playerSession];
                var viewedWorldZLevels = ViewedWorldZLevels[playerSession];

                var ev = new GasOverlayUpdateEvent();

                foreach (var (layer, oldIndices) in previouslySent)
                {
                    EntityUid gridUid = default;
                    var gridExists = false;
                    if (EntManager.TryGetEntity(layer.Grid, out var gridId) &&
                        gridId is { } resolvedGrid &&
                        GridQuery.HasComp(resolvedGrid))
                    {
                        gridUid = resolvedGrid;
                        gridExists = true;
                    }

                    Dictionary<Vector2i, GasOverlayChunk>? layerChunks = null;
                    var layerExists = gridExists &&
                        EntManager.TryGetComponent(gridUid, out GasTileOverlayComponent? overlay) &&
                        overlay.TryGetChunks(layer.LocalZ, out layerChunks);
                    var layerViewed = gridExists && IsLayerViewed(gridUid, layer.LocalZ, viewedWorldZLevels);

                    if (!chunksInRange.TryGetValue(layer.Grid, out var chunks) ||
                        !layerExists ||
                        !layerViewed)
                    {
                        previouslySent.Remove(layer);
                        if (gridExists)
                            AddRemoved(ev, layer, oldIndices);

                        oldIndices.Clear();
                        ChunkIndexPool.Return(oldIndices);

                        continue;
                    }

                    var old = ChunkIndexPool.Get();
                    DebugTools.Assert(old.Count == 0);
                    foreach (var chunk in oldIndices)
                    {
                        if (!chunks.Contains(chunk) || !layerChunks!.ContainsKey(chunk))
                            old.Add(chunk);
                    }

                    if (old.Count == 0)
                        ChunkIndexPool.Return(old);
                    else
                    {
                        AddRemoved(ev, layer, old);
                        old.Clear();
                        ChunkIndexPool.Return(old);
                    }
                }

                foreach (var (netGrid, gridChunks) in chunksInRange)
                {
                    // Not all grids have atmospheres.
                    if (!EntManager.TryGetEntity(netGrid, out var grid) || !EntManager.TryGetComponent(grid, out GasTileOverlayComponent? overlay))
                        continue;

                    var origin = FrameQuery.TryGetComponent(grid.Value, out var frame) ? frame.Origin : 0;
                    foreach (var worldZ in viewedWorldZLevels)
                    {
                        var localZ = worldZ - origin;
                        if (!overlay.TryGetChunks(localZ, out var overlayChunks))
                            continue;

                        var layer = new GasOverlayGridLayer(netGrid, localZ);
                        previouslySent.TryGetValue(layer, out var previousChunks);

                        foreach (var gIndex in gridChunks)
                        {
                            if (!overlayChunks.TryGetValue(gIndex, out var value))
                                continue;

                            if (value.LastUpdate <= LastSessionUpdate &&
                                previousChunks != null &&
                                previousChunks.Contains(gIndex))
                            {
                                continue;
                            }

                            if (!ev.UpdatedChunks.TryGetValue(netGrid, out var dataToSend))
                            {
                                dataToSend = new();
                                ev.UpdatedChunks[netGrid] = dataToSend;
                            }

                            dataToSend.Add(value);
                        }

                        var currentChunks = ChunkIndexPool.Get();
                        DebugTools.Assert(currentChunks.Count == 0);
                        foreach (var gridChunk in gridChunks)
                        {
                            if (overlayChunks.ContainsKey(gridChunk))
                                currentChunks.Add(gridChunk);
                        }

                        previouslySent[layer] = currentChunks;

                        if (previousChunks != null)
                        {
                            previousChunks.Clear();
                            ChunkIndexPool.Return(previousChunks);
                        }
                    }
                }

                foreach (var chunks in chunksInRange.Values)
                {
                    chunks.Clear();
                    ChunkIndexPool.Return(chunks);
                }

                chunksInRange.Clear();
                ChunkViewerPool.Return(chunksInRange);

                if (ev.UpdatedChunks.Count != 0 || ev.RemovedChunks.Count != 0)
                    System.RaiseNetworkEvent(ev, playerSession.Channel);
            }

            private bool IsLayerViewed(EntityUid grid, int localZ, HashSet<int> worldZLevels)
            {
                var origin = FrameQuery.TryGetComponent(grid, out var frame) ? frame.Origin : 0;
                return worldZLevels.Contains(origin + localZ);
            }

            private static void AddRemoved(
                GasOverlayUpdateEvent ev,
                GasOverlayGridLayer layer,
                IEnumerable<Vector2i> chunks)
            {
                if (!ev.RemovedChunks.TryGetValue(layer.Grid, out var removed))
                {
                    removed = new();
                    ev.RemovedChunks[layer.Grid] = removed;
                }

                foreach (var chunk in chunks)
                {
                    removed.Add(new GasOverlayChunkIndices(chunk, layer.LocalZ));
                }
            }
        }

        #endregion

        private readonly record struct GasOverlayGridLayer(NetEntity Grid, int LocalZ);

        private void InitializeCVars()
        {
            Subs.CVar(ConfMan, CCVars.NetGasOverlayTickRate, UpdateTickRate, true);
            Subs.CVar(ConfMan, CCVars.GasOverlayThresholds, UpdateThresholds, true);
            Subs.CVar(ConfMan, CVars.NetPVS, OnPvsToggle, true);
        }
    }
}
