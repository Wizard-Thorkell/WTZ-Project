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
| P6.3a | Automated double round trips and explicit live-round boundary | Complete |
| P6.3b1 | Initialized floor create/copy/delete lifecycle | Complete |
| P6.3b2 | Validated initialized mapping autosave lifecycle | Active |

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

Atmosphere on real non-zero native tiles is persistent authored state. It uses
a versioned sparse representation grouped by local Z and 4x4 chunks, preserving
volume, temperature, and all gas species while sharing equal mixtures. Runtime
adjacency cells marked `NoGridTile`, processing queues, excited groups, and
active hotspot/fire state are reconstructed or discarded instead of entering
the mapper file. The existing Z 0 atmosphere format remains unchanged; its
reader additionally accepts the empty mapping emitted when only non-zero cells
exist.

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

The P6.3a fixture then performs two complete YAML snapshot/load cycles and
compares a canonical semantic model rather than unstable YAML order or entity
identifiers. It covers map configuration, two grids, a translated and rotated
moving frame, native tiles from Z -1 through Z 2, anchored cable/pipe/APC
infrastructure, boundaries, decals, internal device references, and persistent
upper-floor atmosphere. Temperature, volume, and all nine gas species remain
identical across both cycles. Players and explicit transient roots disappear,
and an active source hotspot is absent after each load.

## Initialized Floor Mutation

P6.3b1 permits the authenticated mapping UI to create, copy, and delete floors
after the map reaches `MapInitialized`. These operations remain mapper edits,
not live-round persistence:

1. Create expands the map's continuous declared range and moves the mapper to
   the requested local Z without manufacturing empty tile chunks.
2. Copy validates the current authored map, selects roots using the same entity
   and component predicates as `MappingSnapshotSystem`, and deserializes a clone
   with YAML IDs retained for a complete-root preflight.
3. Only after that preflight does copy replace target authored roots, tiles,
   decals, and real tile atmosphere. Copied pipes, cables, and boundaries retain
   references, anchoring, local Z, and initialized lifecycle.
4. Explicit transient or player descendants are detached from an authored root
   before it is removed. Direct runtime roots are never selected as authored
   copy/delete roots.
5. Atmosphere mixtures are cloned, but hotspots, excited groups, adjacency
   cells, pressure sets, and processing queues are cleared and rebuilt by the
   running simulation.
6. Delete removes selected-grid authored state and relocates runtime roots to the
   resulting default floor before clearing tiles. A final tile-only floor may
   trigger Robust's normal empty-grid deletion.

The Z-level range belongs to the map, while a floor operation targets one grid.
Deleting an edge contracts the range only when no other grid still has tiles or
direct entities on that local Z. Deleting an interior floor clears that grid's
contents but leaves the continuous min/max range unchanged. Direct contraction
through Configure is refused on initialized maps; edge floors must pass through
Delete so these safety checks cannot be bypassed.

An operation that would empty a grid while surviving runtime/other-floor
entities or copied authored decals still depend on it is rejected before target
mutation. Target tiles shared with the source replacement are written first, so
a non-empty copy cannot transiently delete its grid.

## Current Limitations

P6.3b1 completes initialized floor create/copy/delete. It does not claim
live-round restoration or yet enable every initialized-map persistence workflow.

- Mapping autosave still rejects initialized maps. P6.3b2 owns detached snapshot
  validation, temporary output, atomic promotion, and failure cleanup for that
  separate server-side workflow.
- Copied entity graphs are preflighted before target replacement, but arbitrary
  exceptions after mutation begins do not have a general in-memory rollback
  journal. Known empty-grid and dependency failures are rejected up front.
- A continuous minimum/maximum range cannot represent a missing interior floor;
  deleting one clears the selected grid while retaining that logical Z.
- Saving sessions, chat, minds, objectives, players, or other round state is a
  separate live-round capability and is not part of mapper-authored map files.
- Active hotspots, processing queues, runtime atmosphere adjacency cells, and
  other simulation caches are intentionally outside the authored-map contract.
  A future live-round save format would need explicit restoration semantics for
  them rather than an option on `MappingSnapshotSystem`.
- A process or machine crash can leave the hidden same-directory temporary file
  behind. The previous destination remains intact, but automatic stale-temp
  scavenging and explicit directory-metadata fsync are not part of P6.2c.
- Atomic replacement relies on the destination filesystem's same-volume rename
  semantics. Network and unusual filesystems may provide weaker durability than
  local filesystems even after the temporary file's physical flush.

## Verification

P6.3b1's connected fixture passes 1/1 through real client network requests. It
covers initialized Configure contraction refusal, create/delete, copy of
anchored cable/pipe/boundary roots, replacement of tiles/decals/atmosphere,
transient and actor survival, two-grid range ownership, and final empty-grid
removal. The combined mapping/persistence matrix passes 10/10.

The complete Content Z-level integration matrix passes 278/278, and relevant
Content unit/analyzer coverage passes 13/13, with no failures or skips. The full
solution builds with zero errors and 24 established dependency, vulnerability,
and obsolescence warnings.

The generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes,
100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
fail-open candidates. Measured local times are 7.3808, 13.4672, and 20.9562 ms.
P6.3b1 adds no tick- or frame-time work; its scans and serialization run only for
explicit mapping commands.
