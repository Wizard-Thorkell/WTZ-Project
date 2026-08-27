# WTZ ZLevelTrace Contract

`SharedZLevelTraceSystem` is the shared geometric primitive for systems that
need ordered information across native Z levels. It does not own projectile
damage, explosion falloff, sound attenuation, visibility policy, interaction
range, or visual presentation. Those remain responsibilities of specialized
consumers.

## Coordinates

Every `ZLevelTracePoint` contains:

- `WorldCoordinates`: map XY, discrete world Z, and map ID.
- `GridUid`: the optional grid frame used to create the point.
- `LocalPosition`: XY in that frame, or map XY for a map-only point.
- `LocalZ`: Z relative to that frame, or world Z for a map-only point.

Use `TryCreateGridPoint` for a grid-relative endpoint. It captures the current
grid transform and `ZLevelFrameComponent` origin. Use
`ZLevelTracePoint.FromMap` when no grid frame owns the endpoint. A request never
infers world Z from XY overlap.

Endpoints on different maps are invalid. P1.1 executes only requests whose
world Z values match; a vertical request returns `VerticalTraversalRequired`
rather than silently falling through to a 2D ray.

## Channels

`ZLevelTraceRequest.BoundaryChannels` uses the existing
`ZLevelBoundaryChannels` contract. P1.1 adds independent `Projectile` and
`Explosion` bits and widens the enum to 16 bits. Consumers should request the
channel that describes their boundary semantics:

- Hitscan and physical projectiles: `Projectile`.
- Explosion propagation: `Explosion`.
- Line of sight and FOV: `Visibility`.
- Direct or remote use: `Interaction`.
- Acoustic propagation: `Sound`.
- Fire, heat, and visual propagation: `Effects`.

Traversal and body movement continue to use their existing channels. P1.2 will
evaluate the requested bits at every ordered vertical crossing through
`SharedZLevelBoundarySystem`.

## Results

`ZLevelTraceResult` is an immutable snapshot with four ordered collections:

- `Segments`: continuous world-space portions of the trace.
- `TileVisits`: grid-local cells entered by each segment.
- `EntityHits`: hard physics hits with world position and cumulative distance.
- `BoundaryCrossings`: adjacent-level crossings and their resolved state.

Sequence values are zero-based. Distances are cumulative from the request
origin. Same-level entity hits reuse `SharedPhysicsSystem.IntersectRay`, filter
by discrete world Z, and sort by distance with entity UID as the tie breaker.
This preserves ordinary Z0 behavior while preventing an overlapping collider on
another floor from winning the raycast.

Tile output is optional. P1.1 emits it when both endpoints use the same grid
frame and local Z. Exact corner crossings advance diagonally once, matching the
existing pathfinding grid-cast convention instead of reporting both side cells.

## Options

`ZLevelTraceOptions` controls optional output only:

- `IncludeTileVisits` records the grid cells crossed.
- `IncludeEntityHits` performs the shared physics raycast when the collision
  mask is nonzero.

These flags do not select damage, penetration, stopping, or target preference.
A consumer makes those decisions from the ordered result.

## Current Limits

- Vertical requests intentionally stop with `VerticalTraversalRequired` until
  P1.2 supplies ordered segments and boundary crossings.
- Same-level tile visits require one shared grid frame. Frame changes and
  overlapping moving grids are normalized in P1.3.
- Immutable arrays and the existing physics query allocate. No production hot
  path is migrated in P1.1; P1.3 adds a caller-owned buffer after vertical result
  semantics are stable.
- No gameplay system consumes this API yet. Hitscan is the first P2 migration,
  after the whole P1 trace primitive is complete.

## Verification

Focused reference tests:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --no-build --no-restore --filter "FullyQualifiedName~ZLevelTraceTest"
```

The reference matrix covers Z0 physics parity, world-Z entity filtering,
translated and rotated frames, horizontal tile order, perfect diagonals, and
rejection of vertical requests by the temporary P1.1 path.
