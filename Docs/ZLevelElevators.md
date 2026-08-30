# Z-Level Elevators

WTZ Project native Z-level prototype. Copyright (c) pedel and OpenAI Codex.

This document defines the P7.2 contract for powered physical elevator cabins,
mapper-authored stops, player controls, hierarchical navigation, and initialized
mapping persistence. P7.2a introduced authoritative travel; P7.2b binds that
same cabin to the existing traversal graph without creating a second pathfinder.

## Network Model

An elevator network is identified by three values:

- the owning grid;
- the shaft tile XY coordinate;
- a mapper-authored `shaftId`.

This makes an elevator local to one physical column on one grid. The same ID can
be reused elsewhere without joining unrelated shafts. A valid network has
exactly one `ZLevelElevatorCabin` and one `ZLevelElevatorStop` on every served
local Z level. Duplicate cabins or duplicate stops fail closed.

The cabin is a real anchored entity. It owns power demand, current floor,
travel state, timing, capacity, and the cabin floor selector. Stops are anchored
landing controls. Landing controls can request only their own floor; a forged
request for another floor is rejected by the server.

Every landing closes only the `Body` boundary below itself. It therefore remains
a supported navigation destination while the cabin is on another floor without
sealing light, atmosphere, sound, projectiles, or the shaft's traversal channels.

## Mapping Workflow

1. Configure the map as a Z-level map and author the intended local floor range.
2. Place `FloorZLevelShaft` at the same XY tile on every crossed floor.
3. Place one `ZLevelElevatorStop` on that tile on every served floor.
4. Place one `ZLevelElevatorCabin` on the same tile at its initial floor.
5. Give the cabin and every stop the same non-empty `shaftId`.
6. Connect the cabin to ordinary APC power and label stops when a name is more
   useful than their local Z number.

The entire source-to-destination column must permit the requested directed
traversal channel. A solid floor, missing authored opening, invalid map range,
or forced-closed boundary rejects the trip. The cabin itself closes `Body`
through the boundary below its current floor, so riders stand on the platform
without sealing visibility, atmosphere, sound, or projectile channels.

Stops and cabins must remain anchored. Moving, unanchoring, reparenting, or
changing the Z position of a cabin cancels its active trip unless the move is
the system's own validated arrival. Stop topology is reindexed immediately.

Initialized floor copying clones authored stops but never clones a physical
cabin. A cabin already present on the destination floor is preserved. Deleting
a floor that contains the cabin is rejected with an explicit mapping error;
move or remove the cabin first. Removing a served stop cancels any trip that
depends on it and invalidates the corresponding navigation topology.

## Hierarchical Navigation

Physical elevators contribute dynamic edges to the existing
`ZLevelTraversalGraphSystem`. A valid stop exposes at most two edges: one to the
nearest served floor below and one to the nearest served floor above. Sparse
shafts are supported, but the graph never creates a direct edge that skips an
intermediate served stop. The maximum network of 64 stops therefore contributes
at most 128 directed edges.

An edge exists only while the network has one cabin, unique stops, valid bounded
configuration, power when required, open directed shaft geometry, and direct
`Body` support at both landings. Route cost is the cabin's fixed
`navigationCallCost` plus `navigationCostPerLevel` for every crossed local floor.
Non-finite, negative, excessive, unsupported, closed, or unpowered edges fail
closed before entering a route snapshot.

Topology changes include cabin/stop registration, deletion, movement, grid, or
floor changes. Power, tile, boundary, and frame changes invalidate only the
owning map's environment revision. Route validation resolves the complete
captured edge because a middle stop can offer both directions from one entity.

At execution, the server calls the cabin to the route source, verifies that the
waiting actor is still on the exact landing, and then carries that actor to the
captured adjacent destination. The operation is idempotent for its owner. One
route owns a cabin at a time; competing routes are rejected and may replan.
Cancelling or deleting the route owner releases ownership without teleporting
the cabin or aborting a call already in progress. NPC steering uses this same
path and has integration coverage for call, boarding, travel, and route finish.

## Authoritative Travel

A request is accepted only when all of these conditions hold:

- the control still belongs to a valid network;
- an interacting actor is on the control's grid and local floor;
- the cabin is idle, at a unique served source floor, and powered when required;
- the target is a unique stop within the cabin's configured distance limit;
- travel time and power values are finite and within global safety bounds;
- every crossed shaft boundary permits travel in the requested direction;
- the number of eligible riders is within the configured capacity.

Travel is server-timed. The UI receives the authoritative arrival timestamp and
duration only to present progress. It cannot complete or redirect a trip.
Power loss, invalidated stops, cabin movement, or closed geometry cancels the
trip at its source floor. A second request while moving is rejected rather than
queued.

At departure, the server captures unanchored physics entities that overlap the
cabin's exact grid tile and local floor. At arrival it moves only captured
entities that are still aboard. Someone who leaves the tile remains on the
source floor, and someone who enters after departure is not collected. Items in
containers inherit their moved holder's floor through the existing container
contract.

## Limits And Metrics

Defaults are two seconds per level, 2,500 W while travelling, 100 W while idle,
16 levels per request, and 32 directly captured riders. Global hard limits are 64
stops, 128 levels, 128 riders, 30 seconds per level, and five minutes per trip.
Shaft IDs are limited to 64 characters. Values outside these limits fail closed.

`zlevelmetrics` reports registered cabins and stops, active trips, requests,
starts, completions, cancellations, rejections, unpowered/busy rejections, and
captured/moved rider counts. It also reports active navigation ownership, edge
queries/validations, and navigation starts, completions, cancellations, and
rejections. `zlevelmetrics reset` resets these process-local counters with the
other Z-level metrics.

## Persistence Boundary

Mapper-owned configuration and the cabin's authored Z position use normal
component/map persistence. Runtime state (`Moving`, target, arrival timestamp,
and captured rider set) is intentionally transient and is not serialized. An
initialized mapping snapshot must therefore represent an idle cabin at its last
completed floor rather than resume an in-flight request.

The initialized-map contract is proven across two consecutive save/load cycles,
including custom cabin costs, stop labels, sparse floors, boundaries, and graph
edges. A snapshot deliberately taken during a pending trip loads an idle cabin
at its last completed authored floor with no target, timer, passenger set, or
navigation owner. Floor copy/delete tests exercise the same policy on an already
initialized map.

## Current Product Limits

- Movement is a timed discrete floor transition; there is no interpolated cabin
  animation between decks yet.
- Calls are not queued or scheduled across multiple route owners. Contending AI
  can replan until the cabin becomes available; a fair scheduler is future work.
- Networks do not cross grids or use different XY coordinates between stops.
- Landing doors, interlocks, emergency controls, and construction recipes are
  not yet authored.
- Saving a live round during active travel is outside the supported initialized
  mapping snapshot contract.

## P7.2 Verification

Focused integration coverage proves delayed cabin/rider movement, same-floor
authority, spoof rejection, malformed configuration, power/topology loss,
nearest-stop graph construction, exact bidirectional resolution, single-owner
lifecycle, AI execution, initialized floor mutation, and two persistence cycles.
Broader Z-level, baseline, build, and persistence evidence is recorded in
`Docs/ZLevelImplementationLedger.md` at the package gate.
