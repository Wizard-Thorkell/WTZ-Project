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

Use `TryCreateGridPoint` for a grid-relative endpoint. It captures the current
grid transform and `ZLevelFrameComponent` origin. Use
`ZLevelTracePoint.FromMap` when no grid frame owns the endpoint. A request never
infers world Z from XY overlap.

Endpoints on different maps return `DifferentMaps`. Same-world-Z endpoints use
the reference 2D path even when their frames differ. A vertical request
currently requires both endpoints to share one grid frame and to agree with its
local-to-world Z conversion. Other vertical frame combinations return
`FrameResolutionRequired` for P1.3 normalization.

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

`Completed` is the only termination that sets `ReachedDestination`. A closed
boundary returns coherent completed work up to that boundary. Invalid inputs,
different maps, unresolved frames, and preflight crossing-budget failures return
an empty result at the origin. If a tile budget is exhausted, the overflowing
segment is rolled back while previously completed segments remain coherent.

## Options And Budgets

`ZLevelTraceOptions` controls optional output only:

- `IncludeTileVisits` records grid cells crossed.
- `IncludeEntityHits` performs physics work when the collision mask is nonzero.

These flags do not select damage, penetration, stopping, or target preference.
A consumer makes those decisions from the ordered result.

Two replicated server CVars bound one query:

- `zlevel.trace_max_vertical_crossings`, default 64, clamped to 1 through 1024.
- `zlevel.trace_max_tile_visits`, default 8192, clamped to 1 through 1000000.

Effective values are visible through `zlevelmetrics` and the local Z-level debug
overlay. `IterationBudgetExceeded` is deterministic and never silently skips a
boundary or emits a truncated tile sequence.

## Current Limits

- Vertical map-only and cross-grid traces require P1.3 frame normalization.
- Grid points are captured snapshots. Moving a frame after endpoint creation is
  normalized in P1.3 rather than mixing stale world and local coordinates.
- Immutable arrays, per-query lists, the engine ray, and the vertical point
  lookup allocate. P1.3 adds caller-owned buffers and measures the hot path
  before any production consumer is migrated.
- No gameplay system consumes this API yet. Hitscan is the first P2 migration
  after the complete P1 primitive is stable.

## Verification

Focused reference tests:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ZLevelTraceTest|FullyQualifiedName~ZLevelBudgetTest"
```

The P1.2 matrix covers Z0 physics parity, world-Z filtering, translated and
rotated frames, horizontal and perfect-diagonal tile order, upward and downward
multi-floor traces, perfectly vertical fixture hits, channel-specific openings,
closed-boundary truncation, unresolved frames, and both trace budgets.
