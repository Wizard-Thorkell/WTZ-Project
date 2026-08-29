# WTZ Z-Level Pathfinding

This document records navigation ownership, coordinate contracts, and the
incremental P5 implementation plan. Vertical navigation is a specialized
consumer of authored Z-level topology; it is not part of `ZLevelTrace`.

## Baseline Inventory

Robust Content currently owns NPC navigation in these layers:

- `PathfindingSystem` builds and updates a polygon graph per map grid.
- `GridPathfindingComponent` stores 16 by 16 XY chunks and portal metadata.
- `AStarPathRequest` and `BFSPathRequest` time-slice searches over `PathPoly`
  neighbors.
- `MoveToOperator` requests a path during HTN planning.
- `NPCSteeringSystem` follows `PathPoly` queues, handles doors and obstacles,
  and requests replacement paths when a route becomes invalid.

The native `PathPortal` API joins two polygon nodes, but it is not a complete
Z-level solution. Its coordinates are 2D, it assumes a bidirectional edge, and
both endpoint polygons are looked up in one XY graph.

Before P5, the local graph has two Z-level correctness problems:

1. `BuildBreadcrumbs` reads only the legacy base tile at Z 0.
2. Static fixtures from every overlapping floor are collected into the same
   breadcrumb data because graph nodes do not carry a floor.

Adding only stair edges to that graph would therefore produce false routes.
The local graph must first become floor-specific while preserving its existing
door, access, climbing, smashing, diagonal-fixture, and chunk behavior.

## P5.1 Traversal Graph Contract

`ZLevelTraversalGraphSystem` owns the authored connector index. It keys every
connector by `(grid, tile XY, local Z)` and keeps topology separate from local
2D navigation.

Each directed connector has:

- a source grid, tile, local Z, and derived world Z;
- a destination at the same grid/tile plus `ZOffset`;
- a content kind (`Stairs`, `Ladder`, `Shaft`, or `Elevator`);
- a traversal delay and abstract navigation cost;
- a direct-destination-support policy;
- topology and environment revisions for stale-route detection.

An authored edge is currently valid only when:

- `ZOffset` is exactly `-1` or `+1`;
- the corresponding `TraversalUp` or `TraversalDown` boundary channel is open;
- direct support exists on the destination floor when required.

Connectors are directed. Bidirectional stairs require one usable connector in
each direction, whether represented by paired content or a later content type
that deliberately exposes both edges.

The index updates from component startup/shutdown, movement, reparenting,
anchoring, Z changes, mapping placement, tile changes, boundary changes, and
frame-origin changes. Tile and boundary changes advance only the environment
revision when a connector at that column can be affected. Moving a grid changes
world projection without rebuilding local topology.

Runtime code that mutates connector policy fields directly must call
`RefreshTraversal` after the mutation. Hot graph reads deliberately do not
rescan component state; authored prototype state and lifecycle events keep the
index current without adding ECS work or allocations to every query. A later
mutation API may encapsulate this refresh when dynamic connectors need it.

Connected stair regions use a deterministic four-way search over the index
with a 512-node hard limit. Stairs and ladders, opposite directions, different
support policies, or different traversal delays cannot merge into one region.
The player traversal system uses this index instead of globally enumerating all
traversal components for step entry, continuation, and destination suppression.

`zlevelmetrics` exposes node/location counts, revisions, exact-location hit
rate, connected-region visits and budget exhaustion, edge validity outcomes,
and query timing.

Graph queries run on the simulation thread and reuse internal work buffers.
Future parallel path jobs must consume an immutable edge snapshot rather than
calling the live graph concurrently or retaining its internal collections.

## Coordinate Rules

Local graph storage uses grid-local Z because authored tiles and anchored
entities move with their grid frame. Public route endpoints use
`ZLevelMapCoordinates`, whose Z is a shared world layer.

Conversions must follow these rules:

- Resolve an entity target with `SharedTransformSystem.GetZLevelMapCoordinates`.
- Convert world Z to a grid layer with `WorldToLocalZLevel` only after choosing
  the owning grid.
- Never infer an upper-floor target from plain `EntityCoordinates` when its
  reference entity no longer carries that target's floor.
- Reproject local connector positions when a moving or rotating grid changes;
  do not bake mutable world XY into cached topology.

Z 0 remains the component-free compatibility layer. A legacy 2D request with no
explicit floor resolves to the requester's world floor, not unconditionally to
world Z 0.

## Hierarchical Route Shape

The completed route contract will be a sequence of specialized legs:

1. a local 2D path to a connector source;
2. an explicit vertical transition referencing the connector entity and its
   captured revisions;
3. a local 2D path from the destination floor to the next connector or target.

The upper search chooses connector edges and local reachability; it does not
copy local polygons into `ZLevelTrace` and does not pretend empty space is
walkable. Flight will use separate navigation capabilities in P7.

## P5.2 Floor-Specific Local Navigation

The local polygon graph is now partitioned by `PathfindingChunkKey`, which
combines the existing XY chunk origin with a grid-local Z. `PathPoly`, debug
payloads, portal endpoints, dirty queues, and chunk lookup all carry that floor
identity. Sparse upper floors are discovered from authored Z-level tiles while
Z 0 retains the normal grid initialization path.

Breadcrumb construction reads `GetZLevelTileRef` for the selected local floor.
The existing 2D broadphase still supplies spatial candidates, but fixtures are
accepted only when their effective local Z matches the chunk. Tile edits dirty
only their own floor. Fixture collision changes, XY movement, grid changes, and
Z movement dirty the affected old and new chunks/floors independently.

Public path and polygon APIs accept explicit world floors. They convert to local
Z only after resolving the owning grid, so a `ZLevelFrameComponent` origin can
move without rebuilding local topology. Entity-to-entity requests resolve both
actual world floors. Legacy coordinate-only requests remain same-floor and use
the actor or start-reference floor; callers that need a distinct target floor
must use the explicit overload or an entity target.

Native `PathPortal` links remain same-world-floor links between grids. Their
endpoints retain local floors and are re-evaluated after frame changes. They are
not used to represent authored vertical traversal.

P5.2 deliberately rejects an A* request whose endpoints have different world
floors. Vertical composition belongs to P5.3 and will return typed local and
transition legs; returning `NoPath` now is safer than connecting overlapping 2D
polygons or treating empty space as walkable.

`zlevelmetrics` reports cached chunks/floors, pending invalidations, breadcrumb
build time and allocation, fixture candidate rejection by floor, polygon hit
rate, and cross-floor requests rejected pending hierarchical planning. Hot-path
counters are atomic; the diagnostic snapshot allocates its floor summary only
when requested. Created floor chunks currently retain the legacy cache lifetime
even after their last authored tile is removed; P5.4/P8 will use these counters
to evaluate bounded eviction under long rounds.

## Remaining P5 Packages

### P5.3 Hierarchical Planning

- Search the traversal graph with configurable edge and expansion budgets.
- Compose local path legs between the start, connectors, and destination.
- Reject stale topology/environment revisions and replan affected legs only.
- Return a typed route rather than encoding vertical actions as fake polygons.

### P5.4 AI Execution And Dynamic Connectors

- Teach steering to stop on a connector, perform its normal delayed traversal,
  verify the destination, and continue the next local leg.
- Replan when a connector, support tile, boundary, target floor, or grid frame
  invalidates the active route.
- Add dynamic elevator edges with state, power, wait cost, and destination
  selection rather than treating elevators as static stairs.
- Validate hostile/follow behavior through multiple floors and close the P5
  phase gate with scale, budget, and long-running tests.

## P5.1 Verification

Focused integration coverage verifies:

- exact tile/floor indexing and deterministic lookup;
- contiguous equivalent stairs and non-equivalent ladder separation;
- topology revision after movement and unregister on deletion;
- open, unsupported, and boundary-closed directed edge outcomes;
- local-to-world floor conversion after changing a grid frame;
- the existing two-second player step-trigger traversal regression.

The warmed connected-region benchmark performs 256 repeated queries with at
most 256 bytes of total thread allocation. Location buckets use deterministically
ordered compact lists so lookup does not allocate an enumerator stack.

## P5.2 Verification

Focused integration coverage verifies:

- independent tiles, fixtures, polygon data, and routes on overlapping floors;
- invalidation of both old and new floors after fixture Z movement;
- invalidation of old and new chunks after fixture XY movement;
- upper-floor tile changes without rebuilding the matching lower-floor chunk;
- world/local floor conversion after a grid frame changes from world Z 0 to 5;
- explicit rejection of different-floor requests and legacy Z 0 compatibility.

After warmup, 4,096 explicit-floor `GetPoly` calls allocate no more than 256
bytes in total. The complete Content Z-level integration matrix passes 247
tests, the Content unit/analyzer matrix passes 9 tests, and the generated 3, 6,
and 10-floor stress baselines pass without cache or PVS budget exhaustion. A
clean full solution build completes with zero errors and the established 708
warnings. Reusing one fixture candidate set across a chunk lowers the measured
warmed breadcrumb build from 59,480 to 55,448 bytes; the focused fixture
enforces a 58,000-byte ceiling so the old per-tile allocation path cannot return
silently.
