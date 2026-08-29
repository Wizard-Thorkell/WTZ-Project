# WTZ Vertical Sound

This document defines the vertical sound foundation delivered by roadmap
packages P4.1 through P4.3a. P4.1 owns bounded portal discovery and caching.
P4.2 adds authoritative route selection, transmission, pressure, and vacuum
policy. P4.3a adds the narrow WTZ Engine extension needed to adjust an existing
positional stream after native processing. Listener authorization, apparent
direction, diagnostics, and actual cross-floor playback remain P4.3b/P4.3c.

## Ownership

- `SharedZLevelBoundarySystem` remains authoritative for whether the `Sound`
  channel crosses one adjacent vertical boundary.
- `SharedZLevelSoundPortalSystem` turns those decisions into bounded,
  queryable spatial data. It does not own audio entities or playback.
- `ZLevelSoundRouteSystem` is server-authoritative. It selects one ordered
  portal route and reports geometric distance, effective acoustic distance,
  transmission, and an explicit result status.
- Server audio remains responsible for emissions, session selection, PVS, and
  playback identity. Client audio remains responsible for stream processing
  and apparent source presentation.
- `ZLevelTrace` remains a shared geometric primitive. Sound routing does not
  add acoustic, listener, or mixer policy to it.

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

The explicit mask is always a subset of the open mask. It lets routing assign
different transmission to authored vents, grates, shafts, and ordinary
structural openings without resolving every provider again.

`DefaultOpening` means that the current map boundary policy permits Sound.
`ExplicitOpening` means an authored provider forced Sound open. A forced close
still wins over both, exactly as it does for every other boundary consumer.
Opening or closing Sound never changes Visibility, Projectile, Atmosphere, or
another channel.

## Bounded Portal Queries

`QueryPortals` accepts one grid, inclusive tile bounds, an inclusive range of
lower local layers, a caller-owned result list, and an optional caller-owned
budget. Results are ordered by ascending lower Z, chunk Y/X, and tile Y/X.

The budget tracks chunks, cold builds, and open candidates independently. A
limit is charged only when its work is reached. If any limit is exhausted, the
query removes every result it appended while preserving entries already in the
caller's list. Consumers therefore receive either a complete bounded portal set
or an explicit failure, never a partial graph mistaken for valid silence.

There is intentionally no global portal enumeration. Empty space can be open
under the map's default boundary policy and maps are spatially unbounded; a
global graph would turn unused space into infinite work.

## Bounded Route Solver

A route endpoint contains one grid UID, grid-local XY, and grid-local Z. Both
endpoints must belong to the grid supplied to the query. P4.2 rejects
cross-grid routes explicitly rather than guessing a connection between moving
frames.

Same-floor queries return native Euclidean distance immediately. They do not
sample pressure and do not alter existing same-floor audio behavior, including
legacy audio in vacuum.

For a vertical query, the solver first reserves the unavoidable vertical cost:

```text
vertical cost = abs(listener Z - source Z) * vertical distance
horizontal allowance = max distance - vertical cost
```

The ceiling of that horizontal allowance expands the endpoint tile bounds.
Only portal chunks intersecting those finite bounds and the required adjacent
layers are queried. A missing portal layer returns `NoPortalRoute` before any
medium samples are taken.

The remaining graph is an ordered, monotonic DAG: each route crosses exactly
one portal per adjacent boundary and never revisits a floor. Dynamic
programming retains the lowest effective cost for every portal in the current
layer, then resolves the listener from the final layer. Upward results are
ordered bottom-to-top and downward results top-to-bottom.

Equal-cost alternatives retain the first portal in the deterministic P4.1
query order. The solver therefore gives identical answers for a fixed topology
without sorting or allocating a graph per emission. Moving or rotating the grid
and changing its `ZLevelFrame` origin do not change local route choice.

## Transmission And Medium

Each portal starts with a configurable default-opening or explicit-opening
transmission. In pressure-aware mode, the server samples both atmosphere cells
beside that portal and applies:

```text
pressure ratio = clamp(min(lower pressure, upper pressure) / reference pressure, 0, 1)
step transmission = portal transmission * pressure ratio ^ pressure exponent
step loss distance = -ln(step transmission) * loss distance scale
effective distance = geometric route distance + sum(step loss distance)
```

The route transmission is the product of every step transmission. The result
also exposes amplitude loss as `-20 * log10(transmission)` decibels.

Source and listener cells must meet the minimum pressure, and every traversed
portal must have usable pressure on both sides. A missing atmosphere cell,
vacuum, or transmission below the configured minimum returns `MediumBlocked`.
Pressure is cached by Z-aware tile for one route lookup, so endpoints and portal
sides that coincide are sampled once. No atmosphere result is retained across
queries; every emission sees current server state.

## Budgets And Failures

Default route work is controlled by server-only CVars:

| CVar | Default | Absolute clamp | Work |
| --- | ---: | ---: | --- |
| `zlevel.sound_route_max_crossings` | 8 | 64 | adjacent floors |
| `zlevel.sound_route_max_portal_chunks` | 64 | 4,096 | queried chunks |
| `zlevel.sound_route_max_portal_builds` | 16 | 4,096 | cold chunk builds |
| `zlevel.sound_route_max_portal_candidates` | 2,048 | 65,536 | open portals |
| `zlevel.sound_route_max_edges` | 32,768 | 1,000,000 | route edges |
| `zlevel.sound_route_max_medium_samples` | 4,096 | 131,072 | unique pressure cells |

The convenience API creates options and a caller-owned budget from these
defaults. The explicit API accepts stricter or diagnostic budgets, while hard
input clamps still reject more than 64 crossings or 4,096 units of route range.

Every exhaustion has a distinct status. Invalid input, different grids,
crossing limits, no topology, blocked medium, and out-of-range routes are also
distinguishable. Failed routes preserve pre-existing caller results and append
no partial path.

## Coordinates And Moving Grids

Every returned portal carries stable grid-local tile and tile-center
coordinates, lower/upper local Z, current world XY, current lower/upper world Z,
and default/explicit classification.

Moving, rotating, or changing a grid's `ZLevelFrame` origin does not invalidate
the cache because none changes local topology. Each query reprojects cached
portal masks into current world coordinates. Tile, boundary, and map-policy
edits do invalidate topology.

## Invalidation And Retention

Invalidation is targeted:

- a normal tile edit invalidates its chunk at lower local Z `-1`;
- a Z-level tile edit at local Z `z` invalidates the same chunk at `z - 1`;
- a boundary-provider change invalidates its exact chunk and lower layer;
- a map boundary-policy change invalidates cached chunks for grids on that map;
- grid termination removes every entry owned by that grid.

The FIFO cache is limited by `zlevel.sound_portal_cache_capacity`, defaults to
4,096 chunks, and is clamped from 1 through 65,536. Eviction changes only
performance: an entry is rebuilt from current boundary authority when queried
again.

## Metrics

`ZLevelSoundPortalCacheMetrics` exposes cache queries, hits, misses, builds,
timings, invalidations, retention, bounded-query work, and portal-budget
exhaustions.

`ZLevelSoundRouteMetrics` exposes total/same-floor/vertical successes, every
failure and budget category, portal candidates, returned portals, crossings,
edges, medium samples, and total/average/last/maximum route time. Both snapshots
are process-local. P4.3 will publish their operational subset in the debug
overlay and administrative diagnostics beside listener and playback metrics.

## Current Audio Boundary

P4.3a does not make sound newly audible across floors. Robust still creates one
`AudioComponent` for a PVS sound and preserves its playback identity and time.
The existing Z-level PVS policy can hide that entity from other floors, and the
client still spatializes ordinary audio in XY only.

WTZ Engine now exposes `AudioSystem.StreamProcessed` after its default
positional path, including native early-mute paths. The callback runs in the
parallel audio update, accepts one subscriber, and can adjust the already
initialized source without replacing startup, map checks, distance checks, or
entity tracking. With no subscriber, native behavior is unchanged.

P4.3b/P4.3c must preserve one logical emission while adding server authorization for
sessions reached by a successful route, an apparent client source at the
selected portal path, directional presentation without duplicate playback
entities, diagnostics, and lifecycle cleanup.

## Verification

Focused commands:

```powershell
dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj --no-restore -m:1
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevelSound"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --filter "FullyQualifiedName~ZLevel"
```

The P4.2 route matrix passes 4/4 and the combined P4.1/P4.2 sound matrix passes
8/8, with no failures or skips. Coverage includes lower-loss ranking,
deterministic equal-cost ties, upward/downward ordering, moving frames,
same-floor compatibility, sealed layers, vacuum, pressure attenuation, all
five work-budget failures, CVar clamps, rollback, and distinct failure states.

The complete Content Z-level integration filter passes 237/237; Content's
structural and capture-analysis filter passes 5/5. The 3-, 6-, and 10-floor
baselines pass 3/3 with 6,336 measured bytes, zero warmed boundary/gravity
misses, zero PVS budget exhaustions, and measured times of 6.784, 13.052, and
26.142 ms. A clean solution build succeeds with zero errors and 700 established
warnings.

The focused route workload executed 2,026 successful queries, evaluated 28,364
edges in 104.139 ms total, and allocated zero bytes across 1,000 repeated hot
queries on the test machine.

## Deliberate Limits

- No sound is newly audible across floors until P4.3b/P4.3c integrate listeners,
  PVS, apparent direction, stream presentation, and emission lifecycle.
- Vertical routes currently require one shared grid frame. Cross-grid sound is
  explicitly rejected until a physical docking/portal contract exists.
- Per-floor segments use Euclidean distance; P4.2 does not invent room-scale
  wall pathfinding or replace native same-floor audio behavior.
- Default and explicit openings have class-level transmission. Material,
  frequency, door state, and content-specific coefficients are future policy.
- Pressure is a current server snapshot, not a persistent acoustic simulation.
- Focused timings are local Debug measurements, not P8 multiplayer scale data.
