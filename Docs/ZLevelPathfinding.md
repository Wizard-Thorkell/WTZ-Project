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

## P5.3a Detached Graph And Route Contracts

`ZLevelTraversalGraphSystem.CreateSnapshot` copies currently valid authored
edges into deterministic order and returns an immutable array stamped with its
map, topology revision, and environment revision. Search code may inspect this
value without touching component queries or the graph's mutable indexes.

Snapshots are cached per map and revision. Repeated requests share the same
detached edge storage; topology or environment changes force a fresh capture,
and map removal evicts the retained entry. P5.4b2 measurements justified making
topology and environment revisions map-scoped: a connector change invalidates
only its owning map. Global revision counters remain monotonic diagnostics and
do not participate in snapshot, search, or active-route validity. `zlevelmetrics`
exposes cached snapshots, tracked map revisions, requests, hits, builds, copied
edges, time, and allocation.

The typed route contract consists of:

- `ZLevelPathEndpoint`, carrying map, exact coordinates, and world Z;
- `ZLevelPathLeg`, discriminating a native same-floor polygon path from one
  authored vertical traversal edge;
- `ZLevelPathRoute`, enforcing connected endpoints, one graph version, finite
  non-negative costs, and matching revisions for every traversal leg;
- `ZLevelPathRouteResult`, with distinct no-path, cancellation, budget, topology,
  and environment outcomes;
- `ZLevelPathSearchBudget`, with caller-owned remaining state, local-path, and
  traversal-edge work.

Default limits are controlled by
`zlevel.pathfinding_max_state_expansions` (64),
`zlevel.pathfinding_max_local_paths` (128), and
`zlevel.pathfinding_max_traversal_edges` (512), each clamped to a hard server
ceiling. P5.3a defines no gameplay fallback and does not encode vertical travel
as a fake `PathPoly`; P5.3b populates these contracts with real local A* legs.

## P5.3b Hierarchical Planning

`PathfindingSystem.GetZLevelPath` is the opt-in authoritative planner for an
explicit pair of map/world-floor endpoints or two entities. It captures one
deterministic traversal snapshot, groups directed connectors by exact source
endpoint, and runs a bounded Dijkstra search over connector destinations. Each
expanded state batches all required native same-floor A* requests with
`Task.WhenAll`; geometry alone never makes a connector reachable.

Successful output alternates immutable local `PathPoly` legs and authored
traversal legs. Vertical cost comes from each traversal profile and local cost is
the A* accumulated polygon cost, including collision and interaction modifiers.
Equal-cost choices retain snapshot/insertion order. The native cost deliberately
does not invent sub-polygon travel distance, so P5.4 execution telemetry may
justify a finer endpoint cost without changing reachability or route contracts.

State expansion, local A* dispatch, and traversal-edge evaluation consume their
own caller-owned budgets. Exhaustion returns the corresponding typed status;
invalid requests, no-path, cancellation, topology staleness, environment
staleness, and combined staleness remain distinct. `PathRequest` now retains the
actual cancellation token, and A*/BFS observe it while queued instead of treating
the token as inert `Task` state. `GetPathDistance` returns the same accumulated
A* cost used by hierarchical planning.

The planner validates the graph revision after every local batch and before
publishing a route. `ValidateZLevelPathRoute` then checks local polygon validity
and resolves each authored traversal again, returning the first invalid leg.
Unrelated graph revisions do not invalidate a route whose exact connector and
local polygons still execute. Route query outcomes, work counts, leg counts, and
timings are exposed through `zlevelmetrics` and reset with the existing
pathfinding metrics.

The existing `GetPath` overloads remain same-floor and still reject cross-floor
requests. P5.4a migrates the controlled `MoveToOperator` and runtime steering
consumers to the typed API described below.

## P5.4a Static AI Route Execution

`NPCSteeringSystem` now owns a typed hierarchical route state machine alongside
its existing local `PathPoly` queue. A local leg is loaded into that mature
queue, while a traversal leg stops at its exact authored source and calls
`ZLevelTraversalSystem.TryStartTraversal`. NPCs therefore use the same
server-authoritative two-second `DoAfter`, progress indicator, connected-stair
rules, destination checks, and boundary policy as players.

Both HTN planning through `MoveToOperator` and runtime steering can request a
cross-floor route. Entity endpoints are converted synchronously to stable grid
or map coordinates before the first asynchronous local path request. Route
installation rechecks the actor position, target position, map, world floors,
connector identity, and local polygons, so movement during an `await` cannot
publish stale work. A whole moving grid remains valid because all snapshots and
comparisons retain its coordinate frame.

Active routes remember the graph revisions under which they were last
validated. Topology or environment changes trigger exact route validation;
only a changed leg causes a replan. Target map/floor changes, meaningful local
target motion, unexpected actor floors, unavailable traversals, and invalid
local paths have distinct diagnostic reasons. Clearing or replacing a route
cancels any traversal `DoAfter` it owns, including a target-floor change while
the NPC is waiting on stairs.

Arrival handling brakes velocity before completing a local target so normal
steering cannot carry an NPC back out of range on the following tick.
`zlevelmetrics` exposes installed/completed routes, started/completed
traversals, replans, execution failures, and discarded stale path results.
Deleting a connector now cancels every pending user rather than only the first.

## P5.4b1 Dynamic Traversal State

`ZLevelDynamicTraversalComponent` adds server-authoritative enabled, callable,
power, expected-wait, and wait-cost state to any authored connector. Runtime
mutation goes through `ZLevelTraversalGraphSystem`, which rejects non-finite or
out-of-range policy, invalidates detached snapshots, and exposes disabled,
unavailable, unpowered, state-change, and destination-change counters.

The graph resolves expected waiting into both the route cost and traversal
delay. Dynamic elevator destination selection is deliberately adjacent-only;
the selected `ZOffset` still has to pass the same boundary and direct-support
checks as stairs. A destination change is topology, while availability, power,
and waiting policy are environment changes. The visual cabin, call controls,
multi-stop content, and construction workflow remain P7 content built on this
contract.

AI starts the exact edge captured by its route. A cost-only update invalidates
planning without interrupting physical travel already underway, while power,
availability, destination, or effective-delay changes cancel the normal
`DoAfter`. Contiguous wide connectors preserve one timer only across live tiles
with equivalent execution behavior. Zero-delay traversal executes directly and
cannot leave an orphaned action; deleting a base-floor user also clears pending
state even when Z 0 is represented without `ZLevelPositionComponent`.

## P5.4b2 Map-Scoped Revisions And Hardening

`ZLevelTraversalGraphSystem` owns independent topology and environment revision
pairs for every live map. Registration moves invalidate both old and new maps;
tile, boundary, frame, and dynamic-state events invalidate only the map that owns
the affected connector. Map removal evicts both its detached snapshot and its
revision record. The process-global counters remain useful aggregate telemetry,
but no runtime consumer uses them as a validity stamp.

Snapshots, in-flight searches, route installation, and active steering compare
the version of the route's map. An unrelated map can therefore change without
rebuilding retained edge arrays or forcing an exact-edge scan on every NPC tick.
A change on the route's own map still triggers authoritative exact-edge
validation and a replan whenever topology, availability, power, destination,
delay, or cost makes the captured route stale.

Scale fixtures exercise eight simultaneous NPCs, both follow and hostile HTN
inputs, and 512 consecutive dynamic connector mutations. Native navigation cache
storage remains at its pre-route high-water mark and graph snapshots remain one
bounded slot per live map. The evidence does not justify speculative pooling or
extra eviction in P5; longer public-server endurance belongs to P8.

## P5 Phase Status

P5 is complete. Authored static and dynamic vertical connectors now participate
in floor-specific local navigation, bounded hierarchical planning, exact
server-authoritative traversal, stale-route recovery, and map-scoped caching.
P7.2 now supplies physical cabin edges through this same graph: each landing
connects only to its nearest served neighbors, exact edge resolution preserves
both directions at middle stops, and AI calls then rides the authoritative
cabin. Flight remains later P7 content built on these contracts.

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

## P5.3a Verification

Focused integration coverage verifies deterministic semantic edge ordering,
snapshot immutability after later registrations, separate topology/environment
staleness, connected typed-route invariants, and positive configured budgets.
After warmup, 256 snapshot requests reuse the same immutable edge array with no
more than 256 bytes of total current-thread allocation. A cold two/three-edge
snapshot remains below a 16,384-byte ceiling.

The complete Content Z-level integration matrix passes 248 tests, the Content
unit/analyzer matrix passes 9 tests, and the generated 3, 6, and 10-floor stress
baselines pass with 100% warmed boundary/gravity cache hits, zero PVS budget
exhaustion, and 6,216 measured bytes each. The final measured baseline times for
this gate are 4.1361 ms, 8.2035 ms, and 13.1701 ms respectively; timing is retained
as local comparison evidence rather than a release threshold.

## P5.3b Verification

The focused pathfinding fixture passes 8/8. It covers same-floor typed routes,
native polygon invalidation, deterministic selection between equal-cost stairs,
`local -> traversal -> local` composition, an unreachable connector behind a
real blocker, all three search budgets, cancellation after local requests are
queued, topology/environment/combined staleness, selective traversal validation,
route diagnostics, and process metrics.

The final complete Content Z-level integration matrix passes 253/253 and the
Content unit/analyzer matrix passes 9/9. The generated 3, 6, and 10-floor stress
baselines pass 3/3 with 100% warmed boundary/gravity cache hits, zero PVS budget
exhaustion or fail-open candidates, and 6,336 measured bytes each. Local measured
times are 6.9764, 13.5241, and 23.7440 ms; timing remains comparison evidence, not
a release threshold. A full incremental `SpaceStation14.slnx` build completes
with zero errors and 27 established warnings.

## P5.4a Verification

The focused pathfinding/steering fixture passes 18/18 with no skips. It covers
HTN plan/install, autonomous runtime planning, the normal delayed traversal,
final arrival braking, target and actor endpoint snapshots, stale installation
rejection, topology and target-floor replans, pending traversal cancellation,
all-user cancellation on connector deletion, and moving-grid versus local-target
motion.

The complete Content Z-level integration matrix passes 263/263 and the Content
unit/analyzer matrix passes 9/9. The generated 3-, 6-, and 10-floor stress
baselines pass 3/3 with 100% warmed boundary/gravity cache hits, zero PVS budget
exhaustion or fail-open candidates, and 6,336 measured bytes each. Local times
are 7.8504, 13.9121, and 21.6865 ms; they remain comparison evidence rather than
release thresholds. A full incremental `SpaceStation14.slnx` build completes
with zero errors and 27 established warnings.

P5.4a deliberately enables only static authored stairs, ladders, and shafts.
Flight remains a separate P7 capability.

## P5.4b1 Verification

The focused dynamic traversal fixture passes 5/5. It covers bounded policy
configuration, effective cost/delay, detached snapshot invalidation, disabled,
unavailable, and unpowered outcomes, powered recovery, adjacent elevator
destination selection, executable wide-connector matching, cancellation during
power/availability/destination changes, authoritative waiting, zero-delay
execution, and deletion of a base-floor user. The combined movement/pathfinding
regression passes 36/36, including dynamic route cost, validation, and no-path
behavior.

The complete Content Z-level integration matrix passes 269/269 and the Content
unit/analyzer matrix passes 9/9. The generated 3-, 6-, and 10-floor stress
baselines pass 3/3 with 100% warmed boundary/gravity cache hits, zero PVS budget
exhaustion or fail-open candidates, and 6,336 measured bytes each. Local times
are 6.9300, 13.1226, and 21.6093 ms; they remain comparison evidence rather than
release thresholds.

## P5.4b2 Verification

The final focused matrix passes 11/11 and covers prolonged dynamic churn,
unrelated-map snapshot/search/route isolation, map-removal eviction, eight
concurrent NPCs, and follow/hostile planning. The 512-mutation fixture retains
one graph snapshot slot with at most 16 KiB per rebuild. All eight NPCs complete
their vertical routes without budget exhaustion, replans, or execution failures;
the measured native cache remains unchanged at 9 chunks and 2 floors.

The combined movement/pathfinding matrix passes 40/40, the complete Content
Z-level integration matrix passes 274/274, and the Content unit/analyzer matrix
passes 9/9, all without skips. Generated 3-, 6-, and 10-floor baselines pass 3/3
with 100% warmed boundary/gravity cache hits, zero PVS budget exhaustion or
fail-open candidates, and 6,336 measured bytes each. Local times are 6.9267,
13.2397, and 21.1442 ms. The full solution build completes with zero errors and
27 established warnings.
