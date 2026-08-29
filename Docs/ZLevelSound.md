# WTZ Vertical Sound

This document defines the vertical sound foundation introduced by roadmap
package P4.1. It deliberately stops at portal discovery and caching. Route
selection, attenuation, listener authorization, apparent direction, and audio
playback belong to P4.2 and P4.3.

## Ownership

- `SharedZLevelBoundarySystem` remains authoritative for whether the `Sound`
  channel crosses one adjacent vertical boundary.
- `SharedZLevelSoundPortalSystem` turns those decisions into bounded,
  queryable spatial data. It does not own audio entities or playback.
- Server audio remains responsible for emissions, session selection, PVS, and
  playback identity.
- Client audio remains responsible for stream processing and apparent source
  presentation.
- `ZLevelTrace` remains a shared geometric primitive. Sound routing does not
  add attenuation, listener, or mixer policy to it.

## Portal Cache

The cache key is:

```text
(grid entity, 16x16 chunk indices, lower grid-local Z)
```

Each entry describes the adjacent boundary from `lowerLocalZ` to
`lowerLocalZ + 1`. It stores two four-`ulong` masks:

- the 256 boundaries that currently permit `Sound`;
- the open boundaries whose permission came from an explicit `ForcedOpen`
  provider.

The explicit mask is always a subset of the open mask. It lets later routing
assign different transmission policy to authored vents, grates, shafts, and
ordinary structural openings without resolving every provider again.

`DefaultOpening` means that the current map boundary policy permits Sound.
`ExplicitOpening` means an authored provider forced Sound open. A forced close
still wins over both, exactly as it does for every other boundary consumer.
Opening or closing Sound never changes Visibility, Projectile, Atmosphere, or
another channel.

## Bounded Queries

`QueryPortals` accepts one grid, inclusive tile bounds, an inclusive range of
lower local layers, a caller-owned result list, and an optional caller-owned
budget. Results are ordered by ascending:

1. lower local Z;
2. chunk Y;
3. chunk X;
4. tile Y inside the chunk;
5. tile X inside the chunk.

The budget tracks chunks, cold builds, and open candidates independently. A
limit is charged only when its work is reached. If any limit is exhausted, the
query removes every result that it appended while preserving entries that were
already in the caller's list. Consumers therefore receive either a complete
bounded portal set or an explicit failure, never a partial graph mistaken for
valid silence.

There is intentionally no global portal enumeration. Empty space can be open
under the map's default boundary policy and maps are spatially unbounded; a
global graph would turn unused space into infinite work. P4.2 must derive its
search bounds from the source, listener range, authored map range, and finite
route budgets.

## Coordinates And Moving Grids

Every returned portal carries:

- stable grid-local tile and tile-center coordinates;
- lower and upper local Z;
- world XY and lower/upper world Z resolved at query time;
- default or explicit opening classification.

Moving, rotating, or changing a grid's `ZLevelFrame` origin does not invalidate
the cache because none of those operations changes local topology. A later
query reprojects the same cached portal into current world coordinates. Tile,
boundary, and map-policy edits do invalidate topology.

## Invalidation And Retention

Invalidation is targeted:

- a normal tile edit invalidates its chunk at lower local Z `-1`;
- a Z-level tile edit at local Z `z` invalidates the same chunk at `z - 1`;
- a boundary-provider change invalidates its exact chunk and lower layer;
- a map boundary-policy change invalidates cached chunks for grids on that map;
- grid termination removes every entry owned by that grid.

The FIFO cache is limited by `zlevel.sound_portal_cache_capacity`, defaults to
4,096 chunks, and is clamped from 1 through 65,536. Eviction changes only
performance: an evicted entry is rebuilt from current boundary authority when
queried again.

## Metrics

`ZLevelSoundPortalCacheMetrics` exposes:

- chunk queries, hits, misses, builds, and build time;
- fixed build tile checks, open portals, and explicit portals;
- invalidations, invalidated chunks, evictions, retained entries, queue tokens,
  and configured capacity;
- bounded portal queries, chunks visited, candidates visited, portals returned,
  and exhaustion counts for every budget kind.

The metrics are process-local. P4.3 will publish the operational subset in the
debug overlay and administrative diagnostics alongside listener and playback
metrics.

## Current Audio Boundary

P4.1 does not change audible behavior. Robust currently creates one
`AudioComponent` for a PVS sound and preserves its playback identity and time.
The existing Z-level PVS policy can still hide that entity from listeners on
other floors, and the client still spatializes ordinary audio in XY only.

P4.2 and P4.3 must preserve one logical audio emission while adding:

- a bounded multi-portal route and transmission value;
- server authorization for sessions reached through Sound portals;
- an apparent client source at the selected portal path;
- directional presentation without duplicate playback entities;
- explicit behavior for sealed areas, vacuum, multiple equal paths, and moving
  frames.

## Verification

Focused commands:

```powershell
dotnet build Content.Shared/Content.Shared.csproj --no-restore -m:1
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelSoundPortalCacheTest"
```

The P4.1 matrix passes 4/4 with no skips. It covers channel independence,
default/explicit classification, client/server parity, Z 0, negative chunks,
deterministic multi-layer order, all three budget failures and rollback,
capacity eviction, exact tile/provider/map invalidation, grid termination, and
translated/rotated frame reprojection.

The measured focused run built one 256-boundary chunk in 0.394 ms, executed
3,050 bounded queries with a rounded 100% cache-hit rate after warmup, and
allocated zero bytes across 1,000 repeated hot queries on the test machine.

## Deliberate Limits

- No sound is newly audible across floors yet.
- No route ranking, attenuation, crossing count, portal graph, listener range,
  PVS exception, apparent direction, or stream override is implemented.
- Portal discovery describes boundary permission, not whether atmosphere or
  another acoustic medium exists in the column.
- Cross-grid sound routing is undefined. Local portal identity is ready for
  moving frames, but P4.2 must define whether and how separate grids connect.
- Cache timing is a focused local measurement, not a P8 multiplayer scale
  result.
