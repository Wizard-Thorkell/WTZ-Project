// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using System.Linq;
using System.Threading;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking;
using Content.Shared.Maps;
using Content.Shared.ZLevel;
using Content.Shared.ZLevel.Components;
using Content.Shared.ZLevel.Systems;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.CPUJob.JobQueues.Queues;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.ZLevel.Structural;

/// <summary>
/// Computes opt-in grid stability from sparse local Z tiles and collapses unsupported turf in bounded batches.
/// </summary>
public sealed partial class ZLevelStructuralSystem : EntitySystem
{
    private const double JobTime = 0.005;
    private const int MaxCollapsesPerTick = 8;
    private const float MaxCollapseDelaySeconds = 3600f;
    private static readonly EntProtoId CollapseEffect = "ZLevelStructuralCollapseDust";
    private static readonly DamageSpecifier CollapseDamage = new()
    {
        DamageDict =
        {
            ["Blunt"] = FixedPoint2.New(1000),
            ["Structural"] = FixedPoint2.New(1000),
        },
    };

    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TileSystem _tiles = default!;
    [Dependency] private readonly SharedZLevelSystem _zLevel = default!;
    [Dependency] private readonly SharedDestructibleSystem _destructible = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitions = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly JobQueue _jobQueue = new(JobTime);
    private readonly HashSet<EntityUid> _dirtyGrids = new();
    private readonly HashSet<EntityUid> _pendingIndexScans = new();
    private readonly Dictionary<EntityUid, InFlightJob> _inFlight = new();
    private readonly List<EntityUid> _entityBuffer = new();

    private EntityQuery<ZLevelStructuralGridComponent> _structuralQuery;
    private EntityQuery<ZLevelStructuralCoreComponent> _coreQuery;
    private EntityQuery<ZLevelStructuralSupportComponent> _supportQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;
    private EntityQuery<MapGridComponent> _gridQuery;
    private EntityQuery<TransformComponent> _transformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _structuralQuery = GetEntityQuery<ZLevelStructuralGridComponent>();
        _coreQuery = GetEntityQuery<ZLevelStructuralCoreComponent>();
        _supportQuery = GetEntityQuery<ZLevelStructuralSupportComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();
        _gridQuery = GetEntityQuery<MapGridComponent>();
        _transformQuery = GetEntityQuery<TransformComponent>();

        SubscribeLocalEvent<ZLevelStructuralGridComponent, ComponentStartup>(OnGridStartup);
        SubscribeLocalEvent<ZLevelStructuralGridComponent, ComponentShutdown>(OnGridShutdown);
        SubscribeLocalEvent<ZLevelStructuralGridComponent, TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<ZLevelStructuralGridComponent, ZLevelTileChangedEvent>(OnZLevelTileChanged);

        SubscribeLocalEvent<ZLevelStructuralCoreComponent, ComponentStartup>(OnCoreStartup);
        SubscribeLocalEvent<ZLevelStructuralCoreComponent, ComponentShutdown>(OnCoreShutdown);
        SubscribeLocalEvent<ZLevelStructuralCoreComponent, AnchorStateChangedEvent>(OnCoreAnchorChanged);
        SubscribeLocalEvent<ZLevelStructuralCoreComponent, ReAnchorEvent>(OnCoreReAnchor);
        SubscribeLocalEvent<ZLevelStructuralCoreComponent, ZLevelPositionChangedEvent>(OnCoreZLevelChanged);

        SubscribeLocalEvent<ZLevelStructuralSupportComponent, ComponentStartup>(OnSupportStartup);
        SubscribeLocalEvent<ZLevelStructuralSupportComponent, ComponentShutdown>(OnSupportShutdown);
        SubscribeLocalEvent<ZLevelStructuralSupportComponent, AnchorStateChangedEvent>(OnSupportAnchorChanged);
        SubscribeLocalEvent<ZLevelStructuralSupportComponent, ReAnchorEvent>(OnSupportReAnchor);
        SubscribeLocalEvent<ZLevelStructuralSupportComponent, ZLevelPositionChangedEvent>(OnSupportZLevelChanged);

        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundCleanup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessPendingIndexScans();
        StartPendingJobs();
        _jobQueue.Process();
        CollectFinishedJobs();
        ProcessPendingCollapses();
        PushDebugSnapshots();
    }

    public override void Shutdown()
    {
        CancelInFlightJobs();
        _dirtyGrids.Clear();
        _pendingIndexScans.Clear();
        _debugDirtyGrids.Clear();
        _debugRemovedGrids.Clear();
        _debugSessions.Clear();

        base.Shutdown();
    }

    /// <summary>
    /// Invalidates a grid after external structural configuration changes.
    /// </summary>
    public void InvalidateGrid(EntityUid gridUid)
    {
        MarkDirty(gridUid);
    }

    private void MarkDirty(EntityUid gridUid)
    {
        if (!_structuralQuery.TryComp(gridUid, out var structural))
            return;

        structural.Revision++;
        _dirtyGrids.Add(gridUid);
    }

    private void OnGridStartup(Entity<ZLevelStructuralGridComponent> entity, ref ComponentStartup args)
    {
        _pendingIndexScans.Add(entity.Owner);
    }

    private void OnGridShutdown(Entity<ZLevelStructuralGridComponent> entity, ref ComponentShutdown args)
    {
        MarkDebugRemoved(entity.Owner);
        _dirtyGrids.Remove(entity.Owner);
        _pendingIndexScans.Remove(entity.Owner);
        if (_inFlight.Remove(entity.Owner, out var job))
        {
            job.Cancellation.Cancel();
            job.Cancellation.Dispose();
        }
    }

    private void OnTileChanged(Entity<ZLevelStructuralGridComponent> entity, ref TileChangedEvent args)
    {
        MarkDirty(entity.Owner);
    }

    private void OnZLevelTileChanged(Entity<ZLevelStructuralGridComponent> entity, ref ZLevelTileChangedEvent args)
    {
        MarkDirty(entity.Owner);
    }

    private void OnCoreStartup(Entity<ZLevelStructuralCoreComponent> entity, ref ComponentStartup args)
    {
        RefreshCoreIndex(entity);
    }

    private void OnCoreShutdown(Entity<ZLevelStructuralCoreComponent> entity, ref ComponentShutdown args)
    {
        RemoveCoreIndex(entity);
    }

    private void OnCoreAnchorChanged(Entity<ZLevelStructuralCoreComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshCoreIndex(entity);
    }

    private void OnCoreReAnchor(Entity<ZLevelStructuralCoreComponent> entity, ref ReAnchorEvent args)
    {
        RefreshCoreIndex(entity);
    }

    private void OnCoreZLevelChanged(Entity<ZLevelStructuralCoreComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        RefreshCoreIndex(entity);
    }

    private void OnSupportStartup(Entity<ZLevelStructuralSupportComponent> entity, ref ComponentStartup args)
    {
        RefreshSupportIndex(entity);
    }

    private void OnSupportShutdown(Entity<ZLevelStructuralSupportComponent> entity, ref ComponentShutdown args)
    {
        RemoveSupportIndex(entity);
    }

    private void OnSupportAnchorChanged(Entity<ZLevelStructuralSupportComponent> entity, ref AnchorStateChangedEvent args)
    {
        RefreshSupportIndex(entity);
    }

    private void OnSupportReAnchor(Entity<ZLevelStructuralSupportComponent> entity, ref ReAnchorEvent args)
    {
        RefreshSupportIndex(entity);
    }

    private void OnSupportZLevelChanged(Entity<ZLevelStructuralSupportComponent> entity, ref ZLevelPositionChangedEvent args)
    {
        RefreshSupportIndex(entity);
    }

    private void RefreshCoreIndex(Entity<ZLevelStructuralCoreComponent> entity)
    {
        RemoveCoreIndex(entity);
        if (!_transformQuery.TryComp(entity.Owner, out var transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid)
        {
            return;
        }

        var structural = EnsureComp<ZLevelStructuralGridComponent>(gridUid);
        structural.Cores.Add(entity.Owner);
        entity.Comp.IndexedGrid = gridUid;
        MarkDirty(gridUid);
    }

    private void RemoveCoreIndex(Entity<ZLevelStructuralCoreComponent> entity)
    {
        if (entity.Comp.IndexedGrid is not { } oldGrid)
            return;

        entity.Comp.IndexedGrid = null;
        if (_structuralQuery.TryComp(oldGrid, out var structural))
        {
            structural.Cores.Remove(entity.Owner);
            MarkDirty(oldGrid);
        }
    }

    private void RefreshSupportIndex(Entity<ZLevelStructuralSupportComponent> entity)
    {
        RemoveSupportIndex(entity);
        if (!_transformQuery.TryComp(entity.Owner, out var transform) ||
            !transform.Anchored ||
            transform.GridUid is not { } gridUid ||
            !_structuralQuery.TryComp(gridUid, out var structural))
        {
            return;
        }

        structural.Supports.Add(entity.Owner);
        entity.Comp.IndexedGrid = gridUid;
        MarkDirty(gridUid);
    }

    private void RemoveSupportIndex(Entity<ZLevelStructuralSupportComponent> entity)
    {
        if (entity.Comp.IndexedGrid is not { } oldGrid)
            return;

        entity.Comp.IndexedGrid = null;
        if (_structuralQuery.TryComp(oldGrid, out var structural))
        {
            structural.Supports.Remove(entity.Owner);
            MarkDirty(oldGrid);
        }
    }

    private void OnGridSplit(ref GridSplitEvent args)
    {
        if (!_structuralQuery.TryComp(args.Grid, out var original))
            return;

        _pendingIndexScans.Add(args.Grid);
        foreach (var newGrid in args.NewGrids)
        {
            var structural = EnsureComp<ZLevelStructuralGridComponent>(newGrid);
            structural.CollapseEnabled = original.CollapseEnabled;
            structural.CollapseDelayMin = original.CollapseDelayMin;
            structural.CollapseDelayMax = original.CollapseDelayMax;
            _pendingIndexScans.Add(newGrid);
        }
    }

    private void OnRoundCleanup(RoundRestartCleanupEvent args)
    {
        ClearDebugSnapshots();
        CancelInFlightJobs();
        _dirtyGrids.Clear();
        _pendingIndexScans.Clear();
    }

    private void CancelInFlightJobs()
    {
        foreach (var job in _inFlight.Values)
        {
            job.Cancellation.Cancel();
            job.Cancellation.Dispose();
        }

        _inFlight.Clear();
    }

    private void ProcessPendingIndexScans()
    {
        if (_pendingIndexScans.Count == 0)
            return;

        var pending = _pendingIndexScans.ToArray();
        _pendingIndexScans.Clear();
        foreach (var gridUid in pending)
        {
            if (!_structuralQuery.TryComp(gridUid, out var structural))
                continue;

            RescanGridIndex(gridUid, structural);
            MarkDirty(gridUid);
        }
    }

    private void RescanGridIndex(EntityUid gridUid, ZLevelStructuralGridComponent structural)
    {
        foreach (var coreUid in structural.Cores)
        {
            if (_coreQuery.TryComp(coreUid, out var core) && core.IndexedGrid == gridUid)
                core.IndexedGrid = null;
        }

        foreach (var supportUid in structural.Supports)
        {
            if (_supportQuery.TryComp(supportUid, out var support) && support.IndexedGrid == gridUid)
                support.IndexedGrid = null;
        }

        structural.Cores.Clear();
        structural.Supports.Clear();

        var coreQuery = EntityQueryEnumerator<ZLevelStructuralCoreComponent, TransformComponent>();
        while (coreQuery.MoveNext(out var uid, out var core, out var transform))
        {
            if (!transform.Anchored || transform.GridUid != gridUid)
                continue;

            core.IndexedGrid = gridUid;
            structural.Cores.Add(uid);
        }

        var supportQuery = EntityQueryEnumerator<ZLevelStructuralSupportComponent, TransformComponent>();
        while (supportQuery.MoveNext(out var uid, out var support, out var transform))
        {
            if (!transform.Anchored || transform.GridUid != gridUid)
                continue;

            support.IndexedGrid = gridUid;
            structural.Supports.Add(uid);
        }
    }

    private void StartPendingJobs()
    {
        if (_dirtyGrids.Count == 0)
            return;

        foreach (var gridUid in _dirtyGrids.ToArray())
        {
            if (_inFlight.ContainsKey(gridUid) ||
                !_structuralQuery.TryComp(gridUid, out var structural) ||
                !_gridQuery.TryComp(gridUid, out var grid))
            {
                continue;
            }

            _dirtyGrids.Remove(gridUid);
            var snapshot = CaptureSnapshot(gridUid, grid, structural);
            var cancellation = new CancellationTokenSource();
            var job = new ZLevelStructuralJob(
                JobTime,
                snapshot.LiveNodes,
                snapshot.Seeds,
                snapshot.Bridges,
                cancellation.Token);
            _inFlight.Add(gridUid, new InFlightJob(job, cancellation, structural.Revision));
            _jobQueue.EnqueueJob(job);
        }
    }

    private StructuralSnapshot CaptureSnapshot(
        EntityUid gridUid,
        MapGridComponent grid,
        ZLevelStructuralGridComponent structural)
    {
        var liveNodes = new HashSet<ZLevelTileIndices>();
        foreach (var tile in _map.GetAllNonEmptyZLevelTiles(gridUid, grid))
        {
            liveNodes.Add(tile.GridIndices);
        }

        var seeds = new List<ZLevelStructuralSeed>(structural.Cores.Count);
        foreach (var coreUid in structural.Cores)
        {
            if (!_coreQuery.TryComp(coreUid, out var core) ||
                !_transformQuery.TryComp(coreUid, out var transform) ||
                !transform.Anchored ||
                transform.GridUid != gridUid)
            {
                continue;
            }

            var xy = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
            var z = _transform.GetZLevel((coreUid, transform, CompOrNull<ZLevelPositionComponent>(coreUid)));
            seeds.Add(new ZLevelStructuralSeed(new ZLevelTileIndices(xy.X, xy.Y, z), Math.Max(0, core.Strength)));
        }

        var bridges = new Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>>();
        foreach (var supportUid in structural.Supports)
        {
            if (!_supportQuery.TryComp(supportUid, out var support) ||
                !_transformQuery.TryComp(supportUid, out var transform) ||
                !transform.Anchored ||
                transform.GridUid != gridUid ||
                support.TargetOffset is not (-1 or 1))
            {
                continue;
            }

            var xy = _map.TileIndicesFor(gridUid, grid, transform.Coordinates);
            var z = _transform.GetZLevel((supportUid, transform, CompOrNull<ZLevelPositionComponent>(supportUid)));
            var source = new ZLevelTileIndices(xy.X, xy.Y, z);
            var target = new ZLevelTileIndices(xy.X, xy.Y, z + support.TargetOffset);
            if (!liveNodes.Contains(source) || !liveNodes.Contains(target))
                continue;

            AddBridge(
                bridges,
                source,
                target,
                Math.Max(0, support.Strength),
                Math.Max(0, support.TransferLoss));
        }

        return new StructuralSnapshot(liveNodes, seeds, bridges);
    }

    private static void AddBridge(
        Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>> bridges,
        ZLevelTileIndices first,
        ZLevelTileIndices second,
        int strength,
        int loss)
    {
        if (!bridges.TryGetValue(first, out var firstPartners))
            bridges[first] = firstPartners = new List<ZLevelStructuralBridge>();
        firstPartners.Add(new ZLevelStructuralBridge(second, strength, loss));

        if (!bridges.TryGetValue(second, out var secondPartners))
            bridges[second] = secondPartners = new List<ZLevelStructuralBridge>();
        secondPartners.Add(new ZLevelStructuralBridge(first, strength, loss));
    }

    private void CollectFinishedJobs()
    {
        foreach (var (gridUid, inFlight) in _inFlight.ToArray())
        {
            if (inFlight.Job.Status != JobStatus.Finished)
                continue;

            _inFlight.Remove(gridUid);
            inFlight.Cancellation.Dispose();

            if (!_structuralQuery.TryComp(gridUid, out var structural) ||
                !_gridQuery.TryComp(gridUid, out var grid))
            {
                continue;
            }

            if (inFlight.Job.Exception != null)
            {
                Log.Error($"Z-level structural solve failed for {ToPrettyString(gridUid)}: {inFlight.Job.Exception}");
                _dirtyGrids.Add(gridUid);
                continue;
            }

            if (structural.Revision != inFlight.Revision)
            {
                _dirtyGrids.Add(gridUid);
                continue;
            }

            ApplyResult(gridUid, grid, structural, inFlight.Job.LiveNodes, inFlight.Job.Result ?? new());
        }
    }

    private void ApplyResult(
        EntityUid gridUid,
        MapGridComponent grid,
        ZLevelStructuralGridComponent structural,
        IReadOnlySet<ZLevelTileIndices> liveNodes,
        Dictionary<ZLevelTileIndices, int> stability)
    {
        structural.Stability.Clear();
        foreach (var (tile, value) in stability)
        {
            structural.Stability[tile] = value;
        }

        MarkDebugDirty(gridUid);

        foreach (var pending in structural.PendingCollapses.Keys.ToArray())
        {
            if (!liveNodes.Contains(pending) || stability.ContainsKey(pending) || !structural.CollapseEnabled)
            {
                structural.PendingCollapses.Remove(pending);
                continue;
            }

            var existing = structural.PendingCollapses[pending];
            structural.PendingCollapses[pending] = existing with { Revision = structural.Revision };
        }

        if (!structural.CollapseEnabled || !_map.IsInitialized(Transform(gridUid).MapUid))
            return;

        var configuredMin = SanitizeCollapseDelay(structural.CollapseDelayMin);
        var configuredMax = SanitizeCollapseDelay(structural.CollapseDelayMax);
        var delayMin = MathF.Min(configuredMin, configuredMax);
        var delayMax = MathF.Max(configuredMin, configuredMax);
        foreach (var indices in liveNodes)
        {
            if (stability.ContainsKey(indices) || structural.PendingCollapses.ContainsKey(indices))
                continue;

            var tile = _map.GetZLevelTileRef(gridUid, grid, indices);
            if (tile.Tile.IsEmpty ||
                _tileDefinitions[tile.Tile.TypeId] is not ContentTileDefinition definition ||
                definition.Indestructible ||
                definition.BaseTurf == null)
            {
                continue;
            }

            var delay = delayMax <= delayMin ? delayMin : _random.NextFloat(delayMin, delayMax);
            structural.PendingCollapses[indices] = new ZLevelPendingCollapse(
                _timing.CurTime + TimeSpan.FromSeconds(delay),
                structural.Revision);
            SpawnCollapseEffect(gridUid, grid, indices);
        }
    }

    private void ProcessPendingCollapses()
    {
        var budget = MaxCollapsesPerTick;
        var query = EntityQueryEnumerator<ZLevelStructuralGridComponent, MapGridComponent>();
        while (budget > 0 && query.MoveNext(out var gridUid, out var structural, out var grid))
        {
            if (structural.PendingCollapses.Count == 0)
                continue;

            var due = new List<ZLevelTileIndices>();
            foreach (var (tile, pending) in structural.PendingCollapses)
            {
                if (pending.At > _timing.CurTime)
                    continue;

                due.Add(tile);
                if (due.Count >= budget)
                    break;
            }

            foreach (var tile in due)
            {
                var pending = structural.PendingCollapses[tile];
                structural.PendingCollapses.Remove(tile);
                if (pending.Revision != structural.Revision)
                {
                    _dirtyGrids.Add(gridUid);
                    continue;
                }

                CollapseTile(gridUid, grid, tile);
            }

            budget -= due.Count;
        }
    }

    private void CollapseTile(EntityUid gridUid, MapGridComponent grid, ZLevelTileIndices indices)
    {
        var tile = _map.GetZLevelTileRef(gridUid, grid, indices);
        if (tile.Tile.IsEmpty ||
            _tileDefinitions[tile.Tile.TypeId] is not ContentTileDefinition definition ||
            definition.Indestructible ||
            definition.BaseTurf == null)
        {
            return;
        }

        _zLevel.GetAnchoredEntitiesOnZLevel(
            gridUid,
            grid,
            new Vector2i(indices.X, indices.Y),
            indices.Z,
            _entityBuffer);
        foreach (var uid in _entityBuffer.ToArray())
        {
            if (Deleted(uid))
                continue;

            if (_damageableQuery.TryComp(uid, out var damageable))
            {
                _damageable.TryChangeDamage(
                    (uid, damageable),
                    CollapseDamage,
                    ignoreResistances: true,
                    ignoreGlobalModifiers: true);
            }

            _destructible.DestroyEntity(uid);
        }

        SpawnCollapseEffect(gridUid, grid, indices);
        _tiles.DeconstructZLevelTile(tile);
    }

    private void SpawnCollapseEffect(EntityUid gridUid, MapGridComponent grid, ZLevelTileIndices indices)
    {
        var coordinates = _map.ToZLevelCenterCoordinates(gridUid, indices, grid);
        var effect = Spawn(CollapseEffect, coordinates.ToEntityCoordinates());
        _zLevel.SetZLevelPosition(effect, indices.Z);
    }

    private static float SanitizeCollapseDelay(float delay)
    {
        return float.IsFinite(delay)
            ? Math.Clamp(delay, 0f, MaxCollapseDelaySeconds)
            : 0f;
    }

    private sealed record InFlightJob(
        ZLevelStructuralJob Job,
        CancellationTokenSource Cancellation,
        uint Revision);

    private sealed record StructuralSnapshot(
        HashSet<ZLevelTileIndices> LiveNodes,
        List<ZLevelStructuralSeed> Seeds,
        Dictionary<ZLevelTileIndices, List<ZLevelStructuralBridge>> Bridges);
}
