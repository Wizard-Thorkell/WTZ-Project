# WTZ Z-Level Lighting And FOV

This document defines the rendering ownership and vertical projection contract
established by roadmap packages P3.1 through P3.4a. It is the baseline for
lower-tile composition budgets, shadow policy, and visual hardening in the
remaining P3.4 packages.

## Ownership

- Robust owns the eye's world Z, the active grid-layer mesh, point-light and
  occluder selection, and GPU cache lifetime.
- Content owns cross-floor visibility policy, lower-floor composition,
  attenuation, mapping preview, and boundary semantics.
- `SharedZLevelBoundarySystem` remains the authority for whether a future
  vertical projection may cross a `Visibility` boundary.
- Light, FOV, sound, combat, and pathfinding remain specialized consumers.
  None of them may turn `ZLevelTrace` into a subsystem-policy monolith.

The normal viewport is still rendered once. WTZ does not render one map or one
viewport per floor.

## Active-Layer Pipeline

1. `EyeSystem` resolves the eye target every frame and writes both its normal
   map position and `IEye.WorldZLevel`.
2. Clyde converts world Z to each intersecting grid's local frame.
3. The native grid renderer reads that local layer from sparse `MapChunk`
   storage and builds the normal tile and edge meshes.
4. Point lights and occluders are accepted only when their effective world Z
   equals the eye world Z. Filtering occurs before their existing per-frame
   limits are consumed.
5. Content sprite, decal, atmosphere, explosion, and targeting presentation use
   the same entity-backed view context.
6. `ZLevelDebugOverlay` does not redraw the active layer. It only composites
   lower visible layers or adjacent mapping-preview layers.

An entity-less or fixed eye carries an explicit world Z. Content uses that Z as
its fallback instead of silently returning to layer zero.

## Grid-Layer Cache Contract

The native mesh cache key is:

```text
(grid entity, chunk indices, grid-local Z)
```

This prevents a mesh generated for one floor from being reused after the eye
moves to another floor. Entries are created lazily for visible active layers.

Invalidation rules:

- A normal `TileChangedEvent` dirties local Z 0.
- A `ZLevelTileChangedEvent` dirties its explicit local Z.
- Empty/non-empty transitions dirty edge meshes in the changed chunk and its
  eight neighboring chunks on the same local Z.
- A cache entry is deleted when either its chunk disappears or that local layer
  becomes empty.
- Grid removal deletes the tile and edge VAO/VBO/EBO objects for every cached
  layer.

Sparse chunks that contain only non-zero layers contribute to the grid AABB.
Adding and removing boundary tiles expands and contracts that AABB, and this
state survives map save/load.

The P3.2 aperture cache and emitter index are separate from the GPU mesh cache.
They describe vertical lighting inputs and never own native tile meshes or GPU
objects.

## Vertical Aperture Cache Contract

The Content client cache key is:

```text
(grid entity, chunk indices, lower grid-local Z)
```

Each entry stores the 256 `Visibility` boundary decisions for one 16 by 16 map
chunk in four `ulong` words. A set bit means light or FOV policy may cross from
the lower local layer to the layer immediately above it. Entries are built
lazily from `SharedZLevelBoundarySystem`; they do not duplicate boundary rules.

Targeted invalidation rules:

- A normal tile edit invalidates the boundary below local Z 0 in that chunk.
- A sparse Z tile edit invalidates the boundary immediately below the edited
  local layer in that chunk.
- Adding, removing, moving, anchoring, or changing an explicit boundary provider
  invalidates its exact chunk and lower layer.
- A replicated or local `ZLevelMap` policy change invalidates entries belonging
  to every grid on that map. The same event now invalidates the shared boundary
  cache, preventing an old default policy from repopulating a fresh light cache.
- Removing a grid removes only entries owned by that grid.

Entries carry monotonically increasing revisions so tests and consumers can
distinguish a retained neighbor from a rebuilt chunk. P3.4a bounds retention
with `zlevel.lighting_aperture_cache_capacity`, which defaults to 4,096 entries
and clamps to 1 through 65,536. A FIFO queue evicts the oldest built entry.
FIFO keeps hot hits allocation-free and avoids mutating retention order on every
lookup.

Eviction affects retention only. A stack composition keeps each already-read
aperture in local words, so it remains exact even when its depth exceeds cache
capacity and a later layer evicts an earlier one. Repeating that query may
rebuild layers, but it never exposes a truncated or partially intersected mask.
Invalidated queue tokens are discarded lazily and compacted before they can
grow beyond a bounded multiple of capacity.

## Native Emitter Index Contract

WTZ reuses Robust's live `LightTreeSystem`; it does not maintain a second light
movement or hierarchy index. `ZLevelLightingCacheSystem.QueryEmitters`:

1. uses caller-retained tree and result buffers;
2. selects intersecting native component trees with an allocation-free
   approximate grid query;
3. resolves each source's effective world Z through its current hierarchy and
   `ZLevelFrame` origin;
4. applies an exact light-circle versus world-AABB check after broad-phase
   selection; and
5. returns immutable snapshots of the source properties needed by P3.3.

The index follows translated and rotated grids through the existing component
tree update lifecycle. Queries are main-thread and sequential because the
system owns one reusable tree scratch buffer. P3.4a optionally caps native
point-light entries visited by a query. Reaching the cap stops the current tree,
and callbacks for later selected trees stop without visiting another entry.
Intersecting grid-tree selection itself remains the native map broad phase.

## Lower-Floor Projection Contract

`ZLevelLightingProjectionSystem` builds one retained plan for the viewport's
world AABB and eye world Z. The active floor remains entirely native. For each
lower point light inside the configured visibility depth, the planner:

1. resolves depth in world Z and keeps the emitter tied to its own grid/frame;
2. computes a projected horizontal radius using
   `sqrt(radius^2 - (depth * 0.75)^2 - 1)`, preserving the native light shader's
   unit height term;
3. applies transmission of `0.72^depth`;
4. intersects every adjacent aperture chunk from the source local Z to the
   viewer local Z; and
5. compresses visible bits into deterministic horizontal tile runs.

A column contributes only when every crossed `Visibility` boundary is open.
The projection never infers a connection merely because another grid overlaps
the same world XY. Sources without an authored grid frame are skipped because
there is no authoritative aperture stack for them.

`ZLevelLightingProjectionOverlay` draws these runs directly into Clyde's light
target in `BeforeLighting`, after Content's enlarged light target and sun-shadow
composition. Clyde then applies the normal active-floor FOV mask and renders
active-floor point lights. The resulting order is:

```text
Content ambient/emission -> lower-floor projection -> active FOV -> native lights
```

The projection shader uses the native attenuation curve, source mask rotation,
vertical distance, transmission, falloff, and curve factor. It carries all
per-source data in retained vertices so queued draw commands never share mutable
uniform state. Falloff and curve are packed into one high-precision UV channel;
the 1/16 falloff and 1/4095 curve quantization errors are bounded and tested.

WTZ Engine provides `blend_mode light_add`, which uses the same
`source-alpha + destination` blend function as native point lights. The older
generic `add` mode is intentionally unchanged.

## P3.4a Retention And Frame Budgets

Projection work is shared by all automatic viewports in one client frame. The
first projection call initializes the frame budget; later viewports consume the
remainder instead of receiving an independent allowance. The active floor is
unaffected because it stays on Clyde's native path.

All controls are local archived client CVars. A server cannot force a client to
perform more rendering work:

| CVar | Default | Hard maximum | Charged work |
| --- | ---: | ---: | --- |
| `zlevel.lighting_max_emitter_candidates_per_frame` | 4,096 | 65,536 | Native point-light tree entries visited |
| `zlevel.lighting_max_emitters_per_frame` | 256 | 4,096 | Lower-floor sources planned |
| `zlevel.lighting_max_aperture_layers_per_frame` | 4,096 | 1,000,000 | Adjacent boundary layers composed |
| `zlevel.lighting_max_aperture_builds_per_frame` | 32 | 4,096 | Cold aperture chunks built |
| `zlevel.lighting_max_runs_per_frame` | 8,192 | 1,000,000 | Horizontal aperture runs generated |

Projection sorts discovered sources by descending world Z, then entity UID.
The nearest lower floor therefore survives an emitter/planning limit first.
The candidate broad-phase cap can only prioritize among entries it had budget
to discover; it does not scan omitted entries merely to rank them.

Fail-soft behavior is transactional per emitter. If layer, cold-build, or run
budget expires while planning a source, every run produced for that source is
removed and planning stops for that viewport. Earlier complete sources remain.
Work already performed still consumes its budget, which prevents a pathological
source from being retried repeatedly in the same frame. A low cold-build limit
can warm part of the cache without drawing a partial light; later frames finish
the stack deterministically.

Lower-floor tile/FOV composition deliberately does not consume these light
budgets. Sharing one pool would allow dense lights to leave visible lower tiles
black. P3.4b gives tile composition its own limits and nearest-first selection
while retaining the shared aperture cache.

## P3.4b Tile/FOV And Mapping Budgets

`ZLevelTileProjectionSystem` owns retained plans for normal lower-floor tiles and
adjacent mapping preview. The two modes have independent per-client-frame pools,
and neither consumes the lighting planner's allowance:

| CVar | Default | Hard maximum | Charged work |
| --- | ---: | ---: | --- |
| `zlevel.tile_projection_max_chunks_per_frame` | 128 | 4,096 | Normal lower-floor chunks considered |
| `zlevel.tile_projection_max_aperture_layers_per_frame` | 4,096 | 1,000,000 | Normal adjacent boundary layers composed |
| `zlevel.tile_projection_max_aperture_builds_per_frame` | 32 | 4,096 | Normal cold aperture chunks built |
| `zlevel.tile_projection_max_tile_visits_per_frame` | 16,384 | 1,000,000 | Normal tile slots inspected |
| `zlevel.mapping_preview_max_chunks_per_frame` | 128 | 4,096 | Adjacent preview chunks considered |
| `zlevel.mapping_preview_max_tile_visits_per_frame` | 16,384 | 1,000,000 | Adjacent preview tile slots inspected |

Normal planning visits the nearest lower floor first. On one floor it orders
intersecting grids by distance from the viewport center and UID, then visits
chunks from the viewport's center outward in deterministic diagonals. Mapping
preview processes the adjacent lower floor before the adjacent upper floor.
Completed batches are sorted by ascending world Z for far-to-near drawing after
selection, so draw order cannot alter budget priority.

Each chunk is transactional. A layer, cold-build, or tile-visit failure publishes
none of that chunk's tiles; earlier complete chunks remain. Work already spent
still consumes the frame pool. Mapping preview intentionally bypasses apertures
because it is an authoring view, but it retains the same whole-chunk tile-visit
policy. Both modes share the aperture cache with lighting without sharing work
budgets.

The overlay now builds one retained vertex batch and one draw call per projected
chunk instead of issuing one draw call per tile. Grid, context, batch, tile, and
geometry lists are caller-owned and reused after warm-up.

## Shared Aperture FOV Composition

Lower-floor tile composition and projected light consume the same composed
aperture chunks. `ZLevelTileProjectionSystem` walks 16 by 16 chunks, composes
each lower-to-viewer stack once, and rejects closed bits before reading or
drawing tiles. This replaces repeated authoritative boundary queries for every
tile and prevents the visible lower scene from disagreeing with its light mask.

The active floor's normal horizontal FOV remains owned by Clyde. Vertical
visibility only selects which lower columns can participate; it does not create
a second eye, viewport, or FOV render for every floor.

## Observability

The client command `zlevelrendermetrics` reports the latest rendered frame:

- active grid layers and chunks drawn;
- grid/chunk/layer cache hits, misses, and retained entries;
- lights rejected because their world Z differs from the eye;
- occluders rejected because their world Z differs from the eye.

It also reports cumulative input-cache counters:

- aperture hits, misses, builds, tile checks, invalidations, retained chunks,
  capacity, open bits, FIFO evictions, and build timings;
- emitter queries, candidates, accepted sources, world-Z and bounds rejections,
  candidate-budget exhaustion, and query timings.

P3.3 adds projection frames, input/projected/radius-rejected emitters, current
batches and runs, visible tiles, composed chunks and boundary layers, generated
vertices and draw calls, plus build and render timings.

P3.4a adds current used/maximum values and cumulative exhaustion counts for
candidate, emitter, aperture-layer, cold-build, and run budgets.

P3.4b adds grid/chunk candidates, completed/projected chunks, aperture and tile
work, current batches/tiles, normal and preview used/maximum values, exhaustion
counts, draw batches/vertices/calls, and build/render timings.

Run `zlevelrendermetrics reset` to reset cumulative Content vertical-rendering
counters.
Retained cache entries and native per-frame Clyde counters are not destroyed by
this command.

The same values appear when `zlevel.debug_overlay` is enabled. Counters reset at
the start of each Clyde frame; retained cache size is sampled live.

These counters are diagnostic, not synchronized server metrics. Multiple
automatic viewports contribute to the same frame snapshot.

## Reproducible Visual Fixture

`ZLevelMappingStation` contains three overlapping always-powered point lights at
`(3.5, 1.5)`:

| Local Z | Label | Color |
| ---: | --- | --- |
| 0 | `Z0 lighting baseline (red)` | red |
| 1 | `Z1 lighting baseline (green)` | green |
| 2 | `Z2 lighting baseline (blue)` | blue |

The map also contains floor-specific walls and stairs. Its integration test
asserts that all three light fixtures load and retain local Z values 0, 1, and
2.

Manual baseline procedure:

1. Run `forcemap ZLevelMappingStation`, start the round, and join as Passenger.
2. Enable `zlevel.debug_overlay` in the client console.
3. Stand near `(3.5, 1.5)` on Z 0, then traverse Z 1 and Z 2.
4. On Z 0, confirm the red source is native. On Z 1, confirm the green source is
   native and red light appears only through the stair opening near
   `(2.5, 2.5)`. On Z 2, confirm the blue source is native and green light
   appears only through the stair opening near `(4.5, 4.5)`.
5. Confirm closed floor columns do not receive the projected lower-floor color,
   and that the projected color is dimmer than its source floor.
6. Run `zlevelrendermetrics`. The projection report should show at least one
   batch, aperture run, and draw call while looking through either opening.
7. Traverse `0 -> 1 -> 2 -> 1`. The first visit to a floor may create cache
   entries; the return to Z 1 must reuse its own layer without briefly showing
   another floor.
8. Enter mapping mode and enable adjacent preview. The active floor remains the
   native Clyde layer; adjacent tiles are the tinted Content composition.

Capture screenshots at Z 0, Z 1, Z 2, and adjacent preview whenever renderer or
shader behavior changes. Headless integration tests validate state and
invalidation contracts but cannot judge light color, shadow shape, or transient
floor flashes.

## P3.2 Scale Fixture

`ZLevelLightingCacheTest.CacheWorkloadScalesWithAuthoredFloorCount` builds
equivalent one-chunk workloads with 3, 6, and 10 floors. It asserts linear cold
build work, one live emitter per authored floor, and no managed allocation in
the warmed aperture and emitter query loops. Timing is diagnostic output rather
than a machine-dependent pass threshold.

## P3.3 Projection Fixture

`ZLevelLightingProjectionTest` validates full-stack aperture intersection,
closed boundaries at different depths, moving and rotated grid frames, native
mask-space UVs, vertical attenuation, shader/prototype loading, and buffer
reuse. Its 3-, 6-, and 10-floor cases assert that accepted source depth never
exceeds `MaxVisibleLevelDistance` and that warmed planning and geometry do not
allocate proportionally to frame count.

## P3.4a Budget Fixture

`ZLevelLightingBudgetTest` validates client CVar clamps, bounded FIFO retention,
exact stack recomposition while capacity is smaller than stack depth, native
candidate early-out, nearest-floor emitter priority, progressive cold-cache
warming, whole-emitter rollback for layer and run exhaustion, and budget sharing
between projection calls in one frame. The combined `ZLevelLighting` filter
also reruns all P3.2 and P3.3 cache, movement, shader, attenuation, allocation,
and depth regressions.

## P3.4b Tile Projection Fixture

`ZLevelTileProjectionTest` validates complete aperture stacks, moving and rotated
frames, far-to-near retained order, nearest-floor, nearest-grid, and center-chunk
priority, normal layer/cold-build/tile-visit rollback, independent
mapping-preview pools, lower-before-upper preview priority, same-frame sharing,
CVar clamps, and warmed planner/geometry buffer reuse. The combined lighting and
tile filter reruns all 32 P3.2 through P3.4b cases against their shared aperture
cache.

## Current Deliberate Limits

- Only the active floor contributes native point-light shadow maps and FOV
  occluders. Lower-floor light is projected through visibility apertures, but
  lower-floor walls do not yet cast source-specific shadows in that projection.
- Projected light is intentionally clipped to the lower scene visible through
  open columns. Physical light spill onto opaque upper-floor tiles is a separate
  gameplay policy and is not inferred by this compositor.
- Upper floors remain hidden from a lower viewer unless mapping preview is
  active. Upward player-facing FOV and targeting are still separate policy work.
- Lights and occluders on different overlapping grids compare world Z, not each
  grid's local Z.
- The emitter candidate budget bounds point-light entry visits after native
  grid-tree selection. A viewport intersecting extreme numbers of moving grids
  still pays the engine's spatial tree-selection cost; P8 stress profiling will
  determine whether that native stage needs its own budget.
- Tile projection also uses Robust's approximate intersecting-grid query before
  its own chunk budget. Pathological counts of overlapping moving grids remain a
  P8 profiling target even though per-grid chunk, aperture, and tile work is now
  bounded.
