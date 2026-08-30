# Native Z-Level Flight

P7.4a defines the shared movement, gravity, and collision contract for entities
that can fly between native Z-level floors. P7.4b1 adds authoritative player
controls, intrinsic and jetpack capability sources, gameplay interruptions, and
mapping content. P7.4b2a makes traces, hitscan, and bounded physical trajectories
consume active flight height. P7.4b2b adds explicit authored AI corridors and
executes them through the same typed flight contract.

## State Model

`ZLevelFlightComponent` is an opt-in capability. Authored fields configure the
hover offset, vertical acceleration, and maximum vertical speed. Its active
flag and target are networked runtime state without `DataField`, so map saves
retain the capability but always load it inactive.

The target is a grid-local pair `(ZLevel, LocalZOffset)`. A moving grid keeps
the same local destination when `ZLevelFrameComponent.Origin` changes, while
`TrySetFlightWorldTarget()` provides the explicit world-to-local conversion for
callers that own a world-space destination.

The default offset is `0.5`, midway between adjacent boundaries. Targets must
be finite and inside the map's declared local floor range. Acceleration and
maximum speed must both be finite and positive.

## Movement And Gravity

Flight reuses `SharedZLevelSystem` and `ZLevelKinematicsComponent`. The existing
vertical solver accelerates toward a bounded stopping speed, integrates the
continuous local offset, and crosses each boundary through
`SharedZLevelBoundarySystem.CanBodyPass()`. It never teleports to a separate map
and does not create a second vertical physics loop.

An active flyer handles `IsWeightlessEvent` and `CanWeightlessMoveEvent`, so
normal XY input remains usable while the Z solver owns vertical velocity.
Thrown-item and projectile lifecycles keep precedence over controlled flight.

Stopping zeroes controlled vertical velocity, refreshes weightlessness, and
wakes the body. A connected artificial-gravity field then attracts the entity
to its generator plane. Without a field, the entity remains weightless at its
current height. Managed Z-level gravity also honors independent weightlessness
overrides such as anti-gravity equipment.

## Boundary And Collision Contract

`Body` is the sole vertical collision channel for flight. An open boundary is
crossed in order and increments the discrete local floor. A closed boundary
clamps the entity to the contact side, clears vertical velocity, and retargets
the active hover to that exact contact height. This makes the failure stable:
the body sleeps instead of retrying the same closed boundary every tick.

An entity occupies one discrete world floor at a time for ordinary planar
fixtures. `LocalZOffset` does not create fractional cross-floor fixture
collisions. Two entities on the same discrete world Z can collide even when one
is hovering; entities on different world Z floors cannot. Vertical consumers
must use a boundary-aware trace instead of relying on planar fixtures.

`GetFlightTraceZOffset()` exposes continuous height only for an active flyer.
All other entities return the compatibility center offset `0.5`. Trace and
combat consumers can therefore interpolate exact deck crossings without
changing planar collision ownership.

## API And Lifecycle

`SharedZLevelSystem` exposes typed results for:

- `TryStartFlight()`
- `TrySetFlightTarget()`
- `TrySetFlightWorldTarget()`
- `TryStopFlight()`
- `IsFlying()` and `ActiveFlightCount`

Results distinguish missing capability, inactive/already-active state,
cancellation, malformed configuration, invalid target, unconfigured map,
invalid current position, incapacity, buckle/anchor/container state, body type,
transform, and grid failures. Start attempt, capability changed, started, target
changed, stopped, and boundary blocked events keep content policy outside the
geometry solver.

Active flight stops when its entity is anchored, inserted into a container,
changed to a static or kinematic body, reparented to another grid, loses its
capability, or becomes invalid after a map-range change. The component and
active-body indexes are synchronized after replicated state changes.

It also stops with a typed reason when the flyer becomes critical or dead, is
stunned, knocked down, thrown, or buckled. A non-alive, stunned, knocked-down,
or buckled entity cannot start flight. These checks are server-side movement
policy; action availability alone is never treated as authority.

## Controls And Capability Sources

`ZLevelFlightControlsComponent` grants three ordinary action entities: toggle
flight, target one local floor up, and target one local floor down. Actions are
exposed only when both the flight capability and a live native Z-level map
configuration exist on the current grid. They are added and removed on map,
component, replication, and parent lifecycle changes.

The toggle action starts a same-floor hover or releases controlled flight. Up
and down start flight when inactive, otherwise they move the existing target
relative to its current local target. Every action enters the typed shared API;
none writes Z state directly. Toggle state and concise popups mirror start,
target, stop, interruption, and closed-boundary events.

`FlyingMobBase` now carries the inactive capability so existing flying NPCs can
become P7.4b2 AI consumers without treating empty space as walkable. Only
`BaseMobDragon` receives player controls. On ordinary maps neither prototype
shows vertical actions.

Existing filled jetpacks preserve their upstream weightless-space behavior on
unconfigured maps. On a configured native grid they may activate despite grid
gravity, grant any missing capability/controls, and start native flight. Runtime
ownership flags ensure shutdown removes only what that jetpack granted. A
jetpack does not stop or remove an intrinsic flight that was already active.
Removing the native map configuration disables an active jetpack cleanly.

## AI Navigation And Execution

`ZLevelFlightNavigationComponent` is a server-side anchored mapping marker for
one bounded corridor between adjacent floors. The marker sits on a supported
source approach. Its aperture and destination offsets rotate with the marker,
each internal horizontal step is limited to one cardinal tile, and an optional
reverse edge uses the same aperture. Navigation cost is finite and bounded.

The traversal graph publishes flight edges in a separate immutable snapshot
array. An edge exists only while both approach tiles have direct support and
the aperture passes the `Body` boundary channel. Tile, boundary, marker,
anchoring, parent, Z, map, and frame changes invalidate the map-scoped graph
revision. Runtime component edits must call `RefreshFlightNavigation()`.

Only actor-aware path requests may include these edges, and only when the actor
passes side-effect-free flight capability validation. Explicit endpoint
requests and ordinary mobs cannot infer flight. A successful route contains a
typed `Flight` leg rather than marking shaft space as a walkable polygon.

NPC steering executes four states: arrive at the supported source, activate or
adopt flight, move horizontally to the aperture, hold XY while the native
vertical solver crosses the boundary, then move to the supported destination.
It never teleports or bypasses a live `Body` check. A route stops flight only
when it activated that flight; pre-existing flight remains active and is
stabilized on interruption. Clearing, invalidating, or replacing a route
releases its owned flight before another plan is installed.

## Mapping Contract

Control action entity references and jetpack ownership flags are replicated
runtime fields without `DataField`. Active flight and the current target are
also runtime-only. Authored snapshots therefore retain capability parameters
and custom action prototype IDs but never save an airborne state or process-
local entity reference. Actions are reconstructed after the loaded map reaches
map initialization.

The official mapping station includes one filled blue jetpack on local Z 0 and
one bidirectional authored flight corridor from local Z 0 to Z 1. Its load test
materializes both directed graph edges. Snapshot coverage saves an initialized
entity while it is actively flying, inspects its YAML for runtime leakage,
reloads it, and proves that it is inactive with fresh action entities.

## Observability

`zlevelmetrics` and the client debug overlay report active flights, starts,
stops, target changes, solver updates, successful boundary crossings, blocked
boundaries, and lifecycle invalidations. `ResetCounters()` resets every flight
counter. Server metrics additionally report marker locations, graph resolution
outcomes, snapshot flight edges, path evaluations, and steering leg
started/completed/failed counts. The stress artifact schema is version 5 after
the shared metrics contract change.

## P7.4b2 Status

P7.4b2 is complete. Active source and entity-target offsets flow through
`ZLevelTrace`, hitscan range and collision, and ballistic crossing timing.
Explicit, capability-gated graph edges provide bounded AI navigation and
physical steering execution. Empty space remains non-walkable, `Body`
boundaries remain authoritative, and `LocalZOffset` is not a second collision
layer. Specialized trace, combat, and pathfinding rules remain outside the
flight movement solver.

## Verification

- The eight connected flight cases pass. They cover artificial-gravity hover,
  open crossings, moving frames and replication, stable closed-boundary
  contact, gravity restoration, independent weightlessness, typed validation
  and cancellation, container/anchor rejection, and discrete-floor collision.
- Flight plus shared metrics pass 10/10; the complete movement/map-format
  regression set passes 31/31; the final Content Z-level/placement matrix
  passes 322/322 without skips.
- The Content unit/mapping filter passes 11/11. The schema-version 5 stress
  baseline passes 3/3 with 100% warm boundary/sky/gravity hits, zero measured
  misses or PVS exhaustion, and zero flight work in the neutral workload.
- The final measured 3/6/10-floor run records 10.7544/16.1705/25.6144 ms and
  6,560/6,336/6,336 bytes. A repeated fast-path run records 6,336 bytes at all
  depths, confirming the optional capability has no steady-state allocation
  cost when no flight is active.
- A full non-incremental solution build completes in 1m28s with zero errors.
  None of the modified production or test files emit a warning.
- P7.4b1 adds five integration cases. They cover action lifecycle and client
  replication, jetpack ownership and gravity policy, typed interruption paths,
  inherited flying-mob content, and initialized snapshot/load behavior.
- The final broad matrix covers 327 cases: 326 pass and the established
  aperture-cache fixture remains intentionally skipped. Z-level flight plus the
  official map pass 14/14, and Content Z-level/mapping unit tests pass 18/18.
- Schema-version 5 baselines pass 3/3 at 10.1246/15.3204/24.9917 ms for
  3/6/10 floors. Every measured run allocates 6,336 bytes, reports 100% warm
  boundary/sky/gravity cache hits, zero cache misses or PVS exhaustion, and zero
  neutral flight work.
- P7.4b2a passes 3/3 new continuous-height cases, 72/72 complete trace/combat/
  flight consumer cases, and all 330 broad Z-level cases with 329 passes plus
  the established deliberate pathfinding skip. Content unit/mapping tests pass
  18/18.
- Its repeated 3/6/10-floor baseline records 16.9534/19.9935/23.1440 ms and
  6,336 bytes at every depth, with 100% warm boundary/sky/gravity hits, zero PVS
  exhaustion, and zero neutral flight work. A first 86.1608 ms ten-floor sample
  did not reproduce and had identical allocation/cache counters.
- The P7.4b2a non-incremental full build completes in 2m46s with zero errors and
  691 established warnings. A dedicated project rebuild attributes zero warning
  to a modified code or test file.
- P7.4b2b passes 6/6 focused graph/planning/execution/ownership cases. They
  cover capability gating, deterministic bidirectional edges, support,
  boundary and rotation invalidation, physical NPC execution, preservation of
  pre-existing flight, interruption cleanup, and safe route replacement.
- The official mapping map loads one marker and two directed flight edges. The
  final combined pathfinding/traversal matrix passes 38/38, and the complete
  Content Z-level matrix passes 336/336 without a skip.
- Two 3/3 neutral baseline runs pass. The captured run measures
  10.0359/17.5405/27.5582 ms and 6,336 bytes for 3/6/10 floors, with 100% warm
  boundary/sky/gravity hits, zero PVS exhaustion, and zero flight updates.
- The non-incremental single-worker solution build completes in 2m45s with zero
  errors and 691 established warnings. Log attribution finds no warning in a
  modified production or test file.
