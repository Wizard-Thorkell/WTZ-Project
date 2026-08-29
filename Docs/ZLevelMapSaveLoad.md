# WTZ Z-Level Map Save And Load

This document defines the mapper-authored persistence boundary for native WTZ
Z-level maps. It is deliberately narrower than a live-round save: map geometry,
infrastructure, authored entities, and Z-level metadata persist; players, minds,
sessions, and transient round state do not.

## Phase Status

| Package | Capability | Status |
| --- | --- | --- |
| P6.1 | Read-only initialized-map snapshot and transient filtering | Complete |
| P6.2 | Representation validation, reference fidelity, and atomic output | Active |
| P6.3 | Automated double round trips and explicit live-round boundary | Pending |

## Snapshot Flow

`MappingSnapshotSystem.TryCreateMapSnapshot` is the authoritative Content API
for manual mapper saves. It performs these operations without changing the live
map:

1. Confirm that the requested root is a map.
2. Validate native tile layers and every persistent entity against the declared
   `ZLevelMapComponent` range.
3. Serialize the initialized map into a detached `MappingDataNode` with map-init
   state enabled and operation-local filters.
4. Return an exclusion report to the mapper server, which logs the number of
   player, mind, explicit transient, and transient-component removals.
5. Format and send the YAML to the authorized mapping client.

The source map remains live and unchanged. Loading the returned node creates a
new initialized map; it does not replace the source map.

## Persistent Boundary

The map root, grids, native Z-level tiles, `ZLevelMapComponent`, authored decals,
map-savable structure roots, anchored infrastructure, and their persistent
components are included by the normal Robust map serializer.

The following roots are excluded automatically:

- entities with `ActorComponent`;
- bodies with an active `MindContainerComponent`;
- entities with `MindComponent`;
- entities marked with `MappingSnapshotTransientComponent`.

Exclusion walks ancestors up to the map root. A child of an excluded player or
runtime root is therefore excluded even when another persistent component holds
a reference to that child. `FollowerComponent` and `FollowedComponent` are also
removed because following is runtime state and existing save hooks would mutate
the live relationship.

Place `MappingSnapshotTransientComponent` on the highest runtime-only root that
must be absent from mapper files. The component is itself unsaved. Do not use it
on authored structures merely to work around a bad reference; P6.2 owns
reference normalization and validation.

## Engine Contract

WTZ Engine extends `SerializationOptions` with three operation-local controls:

- `EntityFilter` can make an otherwise map-savable entity non-serializable.
  Rejected entity references become invalid and cannot auto-include the entity.
- `ComponentFilter` can omit a component. If the component came from a
  prototype, the serializer writes it to `missingComponents` so load does not
  silently restore it.
- `SuppressMapSerializationEvents` disables before/after lifecycle events only
  for a caller that performs its own validation and needs a read-only snapshot.
  Its default is `false`, so every existing serializer call preserves legacy
  behavior.

Filters can be evaluated more than once and must be deterministic. They cannot
override a prototype whose map-save behavior is disabled, and callers remain
responsible for preserving structural components such as transforms.

## Z-Level Invariants

Tile layers are map-owned and are always validated. The optional entity
predicate affects only entity-level validation, allowing excluded runtime state
to sit temporarily outside an authored range without blocking a valid mapper
snapshot. Any retained entity outside that range still fails the save.

The P6.1 integration fixture proves the following properties:

- Z 0 and Z 1 native tile layers survive load;
- map minimum, maximum, default floor, and boundary mode remain serialized;
- an anchored gas pipe remains anchored on Z 1;
- the loaded map remains `MapInitialized`;
- player roots, their children, active mind bodies, explicit transient roots,
  runtime followers, and nullspace additions are absent;
- snapshot creation does not detach a live follower or delete any source entity;
- invalid persistent Z state rejects the snapshot.

## Current Limitations

P6.1 does not claim atomic file persistence or live-round restoration.

- The mapping client still writes YAML directly to its selected stream. P6.2
  will write a temporary file, validate it, flush it, and replace the destination
  only after success.
- Persistent collections such as device lists can contain deleted, filtered, or
  cross-map entity references. P6.2 will normalize and validate these references
  in the detached representation without mutating live components.
- Mapping autosave and Z-level create/copy/delete operations still reject
  initialized maps. They stay restricted until atomic validation is available.
- A single in-memory load proves the P6.1 contract but not idempotence. P6.3
  requires two save/load cycles and structural comparisons of maps, grids,
  entities, pipes, cables, boundaries, frames, and references.
- Saving sessions, chat, minds, objectives, players, or other round state is a
  separate live-round capability and is not part of mapper-authored map files.

## Verification

P6.1 closes with 19 WTZ Engine entity-serialization tests, 8 mapping/format
tests, 275 Content Z-level integration tests, 9 Content unit/analyzer tests, and
3 generated stress baselines passing. The full solution builds with zero errors.
The measured 3-, 6-, and 10-floor baselines retain 6,336 bytes per measured run,
100% warm boundary/gravity cache hits, and zero PVS budget exhaustion.
