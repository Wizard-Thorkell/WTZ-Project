# Native Z-Level Flight

P7.4a defines the shared movement, gravity, and collision contract for entities
that can fly between native Z-level floors. It deliberately does not add player
actions, species, jetpack prototypes, projectile behavior, or AI steering;
those are consumers in P7.4b.

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

## API And Lifecycle

`SharedZLevelSystem` exposes typed results for:

- `TryStartFlight()`
- `TrySetFlightTarget()`
- `TrySetFlightWorldTarget()`
- `TryStopFlight()`
- `IsFlying()` and `ActiveFlightCount`

Results distinguish missing capability, inactive/already-active state,
cancellation, malformed configuration, invalid target, unconfigured map,
invalid current position, anchor/container state, body type, transform, and
grid failures. Start attempt,
started, target changed, stopped, and boundary blocked events let later content
add stamina, actions, visuals, and interruption policy without moving those
rules into the geometry solver.

Active flight stops when its entity is anchored, inserted into a container,
changed to a static or kinematic body, reparented to another grid, loses its
capability, or becomes invalid after a map-range change. The component and
active-body indexes are synchronized after replicated state changes.

## Observability

`zlevelmetrics` and the client debug overlay report active flights, starts,
stops, target changes, solver updates, successful boundary crossings, blocked
boundaries, and lifecycle invalidations. `ResetCounters()` resets every flight
counter. The stress artifact schema is version 5 after the metrics contract
change.

## P7.4b Boundary

The next package will add controllable actions and delays, species and jetpack
content, mapping/demo prototypes, flight-aware trace/projectile policy, and AI
execution. It will consume the typed API and events here; it must not bypass
`Body` boundary checks or reinterpret `LocalZOffset` as a second collision
layer.

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
