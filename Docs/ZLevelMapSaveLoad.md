# WTZ Z-Level Map Save And Load

This document defines the mapper-authored persistence boundary for native WTZ
Z-level maps. It is deliberately narrower than a live-round save: map geometry,
infrastructure, authored entities, and Z-level metadata persist; players, minds,
sessions, and transient round state do not.

## Phase Status

| Package | Capability | Status |
| --- | --- | --- |
| P6.1 | Read-only initialized-map snapshot and transient filtering | Complete |
| P6.2a | Structured reference diagnostics and detached normalization | Complete |
| P6.2b | Correlated request/response and validation before transfer | Complete |
| P6.2c | UTF-8 temporary output and atomic destination replacement | Complete |
| P6.3 | Automated double round trips and explicit live-round boundary | Active |

## Snapshot Flow

`MappingSnapshotSystem.TryCreateMapSnapshot` is the authoritative Content API
for manual mapper saves. It performs these operations without changing the live
map:

1. Confirm that the requested root is a map.
2. Validate native tile layers and every persistent entity against the declared
   `ZLevelMapComponent` range.
3. Serialize the initialized map into a raw `MappingDataNode` with map-init
   state enabled, operation-local filters, and live save hooks suppressed.
4. Load a deep copy into a paused disposable map. Unresolved references are
   collected as structured diagnostics without emitting expected error logs.
5. Serialize that disposable map with ordinary before/after save hooks enabled,
   allowing legacy collection cleanup to mutate only the disposable copy.
6. Load another deep copy and require exactly one map, no orphan/nullspace
   entities, no unresolved entity references, and valid authored Z-level state.
7. Delete both temporary loads in `finally` blocks and return the untouched,
   reusable normalized node.
8. Return a report containing transient exclusions, normalized-reference count,
   and final validated-entity count before formatting the YAML.

The source map remains live and unchanged. Loading the returned node creates a
new initialized map; it does not replace the source map.

`MappingDataNode` must be copied for every validation load. Robust's entity
deserializer consumes component mappings while reading them, so validating the
same node instance directly would corrupt the representation later transferred
to the mapper.

## Correlated Save Protocol

Every manual mapping save now uses one non-zero `uint` request identifier across
`MappingSaveMapMessage`, `MappingMapDataMessage`, and
`MappingSaveMapErrorMessage`:

1. The client reserves one pending slot and sends its next request ID. A second
   save remains local and reports that another operation is active.
2. The server resolves the session, checks active Host authority, resolves the
   attached map, creates and validates the normalized snapshot, and formats the
   YAML before returning data.
3. Session, permission, map, validation, and unexpected server failures all send
   an error carrying the original request ID instead of silently returning.
4. The client accepts only the response matching its pending ID. Stale,
   mismatched, and duplicate responses cannot complete or replace the operation.
5. A 30-second timeout completes the same pending response task. A simultaneous
   server response and timeout therefore has one deterministic winner.
6. Only validated map data opens the destination dialog. The pending slot stays
   occupied through dialog cancellation or writing and is released in `finally`.

The messages remain reliable-unordered. Ordering is no longer an implicit
correctness requirement because every terminal response is correlated.

## Atomic Destination Write

After the matching validated response arrives, Content encodes the complete
YAML as strict UTF-8 without a byte-order mark and calls the engine-owned
`IFileDialogManager.SaveFileAtomic` API. The native destination path remains
inside WTZ Engine:

1. Cancellation returns `false` without creating or opening any file.
2. A GUID-named temporary file is created in the selected destination's own
   directory with `CreateNew`, write-only access, and no sharing.
3. The complete byte buffer is written asynchronously, flushed asynchronously,
   and then physically flushed with `FileStream.Flush(flushToDisk: true)`.
4. Only after all writes and flushes succeed is the temporary path moved over
   the destination with overwrite enabled. Because both paths share a
   directory, this remains a same-volume rename rather than a copy/delete.
5. Any write, flush, or replacement exception removes the temporary file and is
   propagated to Content, where the existing typed result and localized popup
   report a client-side failure.

The legacy stream-returning `SaveFile` API remains unchanged for unrelated
callers. Mapping uses the atomic byte API exclusively, so it never opens or
truncates an existing map before the complete replacement is durable enough to
be promoted.

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

WTZ Engine also records every unresolved serialized `EntityUid`/`NetEntity` in
`LoadResult.InvalidEntityReferences`, including source YAML UID, component name,
and serialized value. `DeserializationOptions.LogInvalidEntities = false` now
suppresses both explicit-invalid and unknown-YAML-UID logs while retaining these
diagnostics for programmatic validation.

Invalid references are not silently accepted. Existing save hooks may normalize
collections such as `DeviceListComponent` on the disposable map. Any scalar or
collection reference that remains invalid after that pass rejects the snapshot
and identifies its first source component in the returned error.

WTZ Engine additionally owns atomic native-dialog output through
`IFileDialogManager.SaveFileAtomic`. The API accepts complete bytes rather than
text so encoding remains an explicit Content policy and other consumers can use
the same persistence primitive without implicit transcoding.

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

P6.2c completes safe manual mapper output. It does not claim live-round
restoration or yet enable every initialized-map editing workflow.

- Mapping autosave and Z-level create/copy/delete operations still reject
  initialized maps. They stay restricted until P6.3 proves two complete
  save/load cycles and structural idempotence.
- A single in-memory load proves the P6.1 contract but not idempotence. P6.3
  requires two save/load cycles and structural comparisons of maps, grids,
  entities, pipes, cables, boundaries, frames, and references.
- Saving sessions, chat, minds, objectives, players, or other round state is a
  separate live-round capability and is not part of mapper-authored map files.
- A process or machine crash can leave the hidden same-directory temporary file
  behind. The previous destination remains intact, but automatic stale-temp
  scavenging and explicit directory-metadata fsync are not part of P6.2c.
- Atomic replacement relies on the destination filesystem's same-volume rename
  semantics. Network and unusual filesystems may provide weaker durability than
  local filesystems even after the temporary file's physical flush.

## Verification

P6.2c closes with 5/5 focused atomic-writer tests and the complete WTZ Engine
client suite passing 42/42 without skips. Content passes 4/4 mapping unit tests,
1/1 real client/server save-protocol test, 10/10 combined
mapping/format/snapshot/protocol tests, 13/13 relevant unit/analyzer tests, and
276/276 Z-level integration tests without skips. The atomic fixture covers new
file creation, existing-file replacement, injected partial-write failure,
temporary cleanup, exact Unicode bytes, and dialog cancellation. The full
solution builds with zero errors and its established 708-warning baseline.

The generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes,
100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
fail-open candidates. Measured local times are 7.7195, 14.3521, and 21.9225 ms.
