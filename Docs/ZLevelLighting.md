# WTZ Z-Level Lighting And FOV

This document defines the rendering ownership established by roadmap package
P3.1. It is the baseline for the aperture cache in P3.2, vertical light
projection in P3.3, and frame budgets in P3.4.

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

P3.2 may add aperture and emitter indexes, but those indexes must be separate
from the GPU mesh cache and use targeted revision invalidation.

## Observability

The client command `zlevelrendermetrics` reports the latest rendered frame:

- active grid layers and chunks drawn;
- grid/chunk/layer cache hits, misses, and retained entries;
- lights rejected because their world Z differs from the eye;
- occluders rejected because their world Z differs from the eye.

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
4. On each floor, confirm that only the matching red, green, or blue light
   affects the active floor and that walls from other floors cast no shadows.
5. Run `zlevelrendermetrics`. With all fixtures in the query area, two lights
   should be rejected by world Z and at least one grid layer should be drawn.
6. Traverse `0 -> 1 -> 2 -> 1`. The first visit to a floor may create cache
   entries; the return to Z 1 must reuse its own layer without briefly showing
   another floor.
7. Enter mapping mode and enable adjacent preview. The active floor remains the
   native Clyde layer; adjacent tiles are the tinted Content composition.

Capture screenshots at Z 0, Z 1, Z 2, and adjacent preview whenever renderer or
shader behavior changes. Headless integration tests validate state and
invalidation contracts but cannot judge light color, shadow shape, or transient
floor flashes.

## P3.1 Deliberate Limits

- Only the active floor contributes native point light and FOV occlusion.
- Lower floors can remain visible through Content composition, but their light
  is not projected upward yet.
- Vertical attenuation, aperture/emitter caches, work budgets, and fail-soft
  projection are assigned to P3.2 through P3.4.
- Lights and occluders on different overlapping grids compare world Z, not each
  grid's local Z.
- The cache is bounded by authored sparse content lifetime, but P3.1 does not
  impose a separate eviction budget. P3.4 will use measured stress behavior to
  select a predictable policy.
