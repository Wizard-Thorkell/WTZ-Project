# Z-Level Vertical Surfaces And Sky Exposure

WTZ Project native Z-level prototype. Copyright (c) pedel and OpenAI Codex.

This document defines the P7 shared contract used to determine whether a local
floor is exposed to open sky. It intentionally separates geometric truth from
roof content, weather gameplay, rendering, atmosphere, and visibility policy.

## Boundary Contract

`ZLevelBoundaryChannels.Weather` is an independent vertical-boundary channel.
Consumers must not infer weather access from `Atmosphere` or `Visibility`:

- A solid tile on the floor above closes the boundary by the existing default
  tile-above rule.
- `ZLevelFloorOpeningMarker` and `ZLevelGrateBoundaryMarker` explicitly open
  `Weather` across their authored boundary.
- Forced-closed providers retain precedence over forced-open providers.
- A roof on the highest floor closes the boundary from that floor to the
  otherwise empty space above the configured map range.

The marker prototypes are mapping/debug providers. P7.1b owns production roof,
grate, catwalk, and shaft prototypes and their construction lifecycle.

## Query API

`SharedZLevelSkyExposureSystem.GetExposure()` accepts a grid and local
`ZLevelTileIndices`. It walks adjacent boundaries from the origin through the
boundary above `ZLevelMapComponent.MaximumLevel`, in ascending order.

`GetExposureAtWorldZ()` converts world Z through the grid's current
`ZLevelFrameComponent.Origin` and then uses the same local query. Moving a grid
therefore reprojects cached local geometry instead of rebuilding it.

Unconfigured legacy grids use the local range `0..0`. They retain one top
boundary query and reject origins outside local Z 0.

The result is `ZLevelSkyExposureState` with one typed termination:

- `Exposed`: every required Weather boundary is open.
- `ClosedBoundary`: `BlockingLowerZ` identifies the first closed boundary.
- `InvalidGrid`, `InvalidLevel`, or `InvalidConfiguration`: the request has no
  valid authored column.
- `BoundaryResolutionFailed`: the shared boundary system could not resolve an
  adjacent pair.
- `BoundaryBudgetExceeded`: the configured scan limit ended the query.

Only `Exposed` sets `IsExposed`. Every incomplete or invalid result fails closed.

## Cache And Budgets

The process-local cache is keyed by grid UID and local tile/floor origin. It is
a true least-recently-used cache: hot entries move existing linked-list nodes and
do not allocate per lookup.

Each represented XY column owns a revision and cached-entry count. Tile,
non-zero tile, or boundary edits increment that column revision; stale entries
recompute lazily. Map-configuration and grid-lifecycle changes remove affected
entries. Revision metadata is removed with the final cached entry, so sparse
world edits do not create an unbounded secondary index.

Configuration:

| CVar | Default | Effective range | Exhaustion policy |
| --- | ---: | ---: | --- |
| `zlevel.sky_exposure_cache_capacity` | 4,096 | 64..65,536 | Evict LRU and recompute |
| `zlevel.sky_exposure_max_boundary_checks` | 64 | 1..4,096 | Return `BoundaryBudgetExceeded` |

Both CVars are server-owned and replicated so shared client presentation uses
the same effective policy. Empty-column reads do not allocate map chunks.

## Invalidation Sources

The cache observes:

- `TileChangedEvent` for Z 0 edits.
- `ZLevelTileChangedEvent` for sparse non-zero edits.
- `ZLevelBoundaryChangedEvent` for provider placement, removal, movement, state,
  or configuration changes.
- `ZLevelMapConfigurationChangedEvent` for local floor-range/default changes.
- Grid entity termination.
- Boundary-check budget changes, which invalidate all cached results.
- Capacity changes, which immediately evict only excess LRU entries.

## Observability

`zlevelmetrics` reports query outcomes, checks, hit rate, cache occupancy,
invalid requests, boundary failures, budget exhaustion, invalidated entries,
evictions, and effective limits. The existing Z-level debug overlay shows a
compact client-local sky line.

Stress snapshots use schema version 4 and include effective sky budgets plus
query/cache/outcome counters. The 3/6/10-floor measured workload must remain hot
after warm-up when it fits the configured cache.

## Current Consumer Boundary

P7.1a does not change production weather rendering or gameplay. Robust's legacy
`RoofComponent`, `IsRoofComponent`, and planar weather query remain unchanged.
They are not silently treated as Z-aware surfaces.

P7.1b will add authored vertical surface content and its mapping/construction
behavior. P7.3 will migrate weather presentation/gameplay to this query and
define exposure policy for entities, effects, and moving grids. Atmosphere,
visibility, sound, projectiles, interaction, and traversal continue to use their
own boundary channels and specialized systems.

## Verification

- Focused sky, budget, and metrics matrix: 15/15 passed.
- Complete Content Z-level matrix: all 285 cases have passing evidence; one
  pooled aperture-cache skip passed 1/1 in isolation.
- Content Z-level unit/analyzer matrix: 9/9 passed.
- Stress baseline: 3/3 passed with 6,336 measured bytes, 100% warm sky hits, and
  zero measured sky misses, evictions, or budget exhaustions at every depth.
- Hot repeated-query coverage permits at most 512 bytes across 1,000 lookups and
  confirms no per-query allocation.
