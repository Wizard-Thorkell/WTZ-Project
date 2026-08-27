# WTZ ZLevelTrace Contract

`SharedZLevelTraceSystem` is the shared geometric primitive for systems that
need ordered information across native Z levels. It does not own projectile
damage, explosion falloff, sound attenuation, visibility policy, interaction
range, or visual presentation. Those remain responsibilities of specialized
consumers.

## Coordinates And Frames

Every `ZLevelTracePoint` contains:

- `WorldCoordinates`: map XY, discrete world Z, and map ID.
- `GridUid`: the optional grid frame used to create the point.
- `LocalPosition`: XY in that frame, or map XY for a map-only point.
- `LocalZ`: Z relative to that frame, or world Z for a map-only point.

Use `TryCreateGridPoint` for a grid-relative endpoint. Its local XY and local Z
are authoritative: `Trace` resolves them through the grid's current transform
and `ZLevelFrameComponent` origin. The world coordinates stored in the point are
the creation-time snapshot for inspection, not stale execution geometry. Use
`ZLevelTracePoint.FromMap` when no grid frame owns the endpoint; map points keep
their world coordinates authoritative.

Endpoints on different maps return `DifferentMaps`. Same-world-Z endpoints use
the reference 2D path even when their frames differ. When both endpoints share a
grid, that grid is their automatic structural frame. Otherwise a caller may set
`ZLevelTraceRequest.BoundaryFrameUid` explicitly. Both endpoints are projected
into that current frame before tile and boundary work begins.

Vertical map-only, cross-grid, and overlapping-grid requests never choose a
frame from XY overlap. Without a common or explicit structural frame they return
`FrameResolutionRequired`. An explicit frame means that one grid alone owns the
request's tiles and vertical boundaries; automatic multi-grid boundary
composition is a separate future capability.

## Vertical Geometry

Discrete world levels are modeled as planes one world unit apart. A continuous
XYZ line is split at every half-level plane, such as the boundary between Z 4
and Z 5 at Z 4.5. XY is interpolated in both world and grid-local space at that
same parameter, so translated and rotated frames resolve the correct local tile.

Each floor portion becomes one ordered `ZLevelTraceSegment`. Distances are
Euclidean XYZ distances from the request origin, with one Z level equal to one
distance unit. A diagonal trace therefore preserves one cumulative parameter
for segments, tile entries, entity hits, and crossings.

Every crossing resolves the adjacent local Z pair through
`SharedZLevelBoundarySystem`. The first closed boundary is included in the
result, but no tile or entity geometry beyond it is evaluated. `FinalPoint`
stays on the side that the trace reached.

Segments with horizontal extent use the existing engine 2D physics ray and
filter candidates by effective world Z. A perfectly vertical segment performs
an exact point-in-fixture query for hard fixtures matching the collision mask.
Those hits are recorded at the segment entry distance. A truly zero-length 2D
request retains the reference behavior and does not become an implicit overlap
query.

## Channels

`ZLevelTraceRequest.BoundaryChannels` uses the existing
`ZLevelBoundaryChannels` contract. Independent `Projectile` and `Explosion`
bits extend the original channels. Consumers should request the bits that
describe their boundary semantics:

- Hitscan and physical projectiles: `Projectile`.
- Explosion propagation: `Explosion`.
- Line of sight and FOV: `Visibility`.
- Direct or remote use: `Interaction`.
- Acoustic propagation: `Sound`.
- Fire, heat, and visual propagation: `Effects`.

All requested bits must be open at every crossing. Traversal and body movement
continue to use their existing directional and body channels.

## Ordered Results

`ZLevelTraceResult` is an immutable snapshot with four ordered collections:

- `Segments`: continuous portions on one discrete world level.
- `TileVisits`: grid-local cells entered by each segment.
- `EntityHits`: hard physics hits with world position and cumulative distance.
- `BoundaryCrossings`: adjacent-level crossings and their resolved state.

Sequence values are zero-based. Entity hits are globally sorted by cumulative
distance, entity UID, and segment sequence. Per-segment physics candidates use
the same deterministic distance and UID tie break. Exact 2D corner crossings
advance diagonally once, matching the existing pathfinding grid-cast convention.

Hot callers can instead own a `ZLevelTraceBuffer` and call
`Trace(request, buffer)`. That overload returns only a
`ZLevelTraceBufferResult` header; ordered output remains in the buffer as
read-only lists and spans. Every invocation clears and replaces the logical
contents, so consumers must finish reading before reusing the same buffer. Its
list and scratch capacities are retained across calls, and `EnsureCapacity`
allows known output sizes to be reserved before entering a hot loop. The
immutable overload remains the convenience API for cold callers and takes a
snapshot independent of later traces.

`Completed` is the only termination that sets `ReachedDestination`. A closed
boundary returns coherent completed work up to that boundary. Invalid inputs,
different maps, unresolved frames, and preflight crossing-budget failures return
an empty result at the origin. Finite endpoints whose derived distance overflows
are also invalid. If a tile budget is exhausted, the overflowing segment is
rolled back while previously completed segments remain coherent. The same
complete-segment rollback applies when the entity-hit budget is exhausted.

## Options And Budgets

`ZLevelTraceOptions` controls optional output only:

- `IncludeTileVisits` records grid cells crossed.
- `IncludeEntityHits` performs physics work when the collision mask is nonzero.

These flags do not select damage, penetration, stopping, or target preference.
A consumer makes those decisions from the ordered result.

`BoundaryFrameUid` is orthogonal to the output options. It selects the grid
whose local tiles and boundaries govern this request. It does not select an
entity broadphase: physics hits still come from the map and are filtered by
effective world Z.

Three replicated server CVars bound one query:

- `zlevel.trace_max_vertical_crossings`, default 64, clamped to 1 through 1024.
- `zlevel.trace_max_tile_visits`, default 8192, clamped to 1 through 1000000.
- `zlevel.trace_max_entity_hits`, default 4096, clamped to 1 through 1000000.

Effective values are visible through `zlevelmetrics` and the local Z-level debug
overlay. `IterationBudgetExceeded` is deterministic and never silently skips a
boundary or emits a truncated tile sequence.

Every public trace call records one process-local metrics sample. Snapshots
distinguish all six termination values, aggregate segments, tile visits, entity
hits and crossings, and report total, average, last and maximum core time.
`zlevelmetrics reset` clears trace data together with the other native Z-level
counters. Core time includes normalization, geometry, boundaries and optional
physics, but excludes immutable-array snapshot creation after the buffered core
returns.

## Current Limits

- A request can evaluate boundaries from one common or explicit grid frame.
  Automatic entry into and exit from several structural frames is not yet
  modeled.
- The immutable convenience overload allocates its snapshot by design. The
  buffered overload reuses WTZ-owned result and scratch collections, while the
  engine physics ray can still allocate internally. Warmed tile-only workloads
  are enforced as allocation-free; hit-enabled consumers must be profiled
  separately.
- Authoritative basic hitscan is the first gameplay consumer. It uses the
  caller-owned buffer, `Projectile` boundaries, server-owned target validation,
  and consumer-specific hit selection described in
  [ZLevelHitscan.md](ZLevelHitscan.md). Physical projectile and throw lifecycle
  now preserve authoritative world Z as described in
  [ZLevelProjectiles.md](ZLevelProjectiles.md); bounded vertical physical flight
  remains the active migration.

## Verification

Focused reference tests:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ZLevelTraceTest|FullyQualifiedName~ZLevelBudgetTest"
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ZLevelMetricsTest|FullyQualifiedName~ZLevelTraceBenchmarkTest"
```

The cumulative P1.3b2 matrix covers Z0 physics parity, world-Z filtering,
translated and rotated frames, movement after endpoint creation, explicit map
and cross-grid projection, overlapping-grid non-inference, matching client and
server output, horizontal and perfect-diagonal tile order, upward and downward
multi-floor traces, perfectly vertical fixture hits, channel-specific openings,
closed-boundary truncation, unresolved frames, all three trace budgets,
complete-segment hit rollback, equal-distance UID ties, overflowing finite
coordinates, reusable-buffer equivalence, metrics/reset behavior, and four
machine-readable allocation workloads. See
[ZLevelTraceBenchmarkReport.md](ZLevelTraceBenchmarkReport.md) for the captured
method, results, and comparison limits.
