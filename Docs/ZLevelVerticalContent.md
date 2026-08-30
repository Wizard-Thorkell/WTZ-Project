# Z-Level Vertical Surfaces And Sky Exposure

WTZ Project native Z-level prototype. Copyright (c) pedel and OpenAI Codex.

This document defines the P7 shared contract used to determine whether a local
floor is exposed to open sky. It intentionally separates geometric truth from
roof content, weather gameplay, rendering, atmosphere, and visibility policy.

## Boundary Contract

`ZLevelBoundaryChannels.Weather` is an independent vertical-boundary channel.
Consumers must not infer weather access from `Atmosphere` or `Visibility`:

- A solid tile on the floor above closes the boundary by the existing default
  tile-above rule.
- A non-empty tile may open a channel subset through the boundary directly
  below it with `ContentTileDefinition.zLevelOpenChannels`. The default is no
  open channels, so existing solid floors retain their behavior.
- `ZLevelFloorOpeningMarker` and `ZLevelGrateBoundaryMarker` explicitly open
  `Weather` across their authored boundary.
- Forced-closed providers retain precedence over forced-open providers.
- A roof on the highest floor closes the boundary from that floor to the
  otherwise empty space above the configured map range.

Tile-authored channels establish the baseline. Anchored `ZLevelBoundary`
providers then add forced-open channels and forced-closed channels last, so a
close always wins. `ExplicitOnly` maps continue to open every baseline channel
and require explicit closing providers for physical floor support.

## Authored Vertical Content

| Content | Open channels through floor below | Body support | Planar atmosphere | Authoring |
| --- | --- | --- | --- | --- |
| Ordinary solid tile | None | Yes | Tile-defined | Existing floor construction |
| `Lattice` / `TrainLattice` | Atmosphere, Visibility, Weather, Sound, Effects, Projectile, Explosion | Yes | Map atmosphere | Metal rods; existing catwalk recipe/RCD |
| `ZLevelGrate` | Same selective set as lattice | Yes | Interior mutable air | Fabricated `FloorTileItemZLevelGrate` stack |
| `FloorZLevelShaft` | All | No | Interior mutable air | Fabricated `FloorTileItemZLevelShaft` stack |
| `Catwalk` on a shaft | Shaft channels, with Body forced closed | Yes | Inherited from shaft | Existing rods recipe/RCD; cutters remove it |
| `ZLevelRoofMarker` | All channels forced closed above | N/A | N/A | Durable, mapper-visible top-floor cap |

The grate deliberately differs from lattice. Lattice remains directly exposed
to the map atmosphere as upstream content expects; `ZLevelGrate` owns a normal
mutable gas tile and connects rooms vertically only through the Atmosphere
boundary channel.

The shaft remains a non-empty visual tile but declares no physical support.
Adding an anchored catwalk closes only `Body` across its boundary, making a
removable bridge while sight, gas, sound, weather, effects, projectiles, and
explosions continue through it. Removing the catwalk exposes the shaft again.

`FloorElevatorShaft` is intentionally unchanged. Existing maps may use that
legacy decorative tile as ordinary flooring; silently converting it into an
opening would break Z 0 compatibility. New layered maps must use
`FloorZLevelShaft` when they require real vertical passage.

## Roof Semantics

An ordinary solid tile on floor `z + 1` is the production roof/ceiling over
floor `z`. It uses normal lattice, plating, and floor construction and therefore
inherits established deconstruction and persistence behavior.

The highest declared floor has no authorable tile above its top boundary.
`ZLevelRoofMarker` is the durable mapping representation for that special cap;
it closes every channel above the marker's floor and is hidden during normal
play like other mapper markers. A player-facing top-cap construction item and
roof presentation remain separate product work rather than an invisible item
being introduced here.

## Construction And Mapping

- The mapping tile palette exposes `ZLevelGrate` and `FloorZLevelShaft`; the
  entity palette exposes `ZLevelRoofMarker` and the existing `Catwalk`.
- Industrial lathes can fabricate the grate and shaft tile stacks. Each item
  first creates plating when needed and then replaces plating with its vertical
  surface on the user's current world Z.
- The manual catwalk recipe accepts lattice, plating, grate, and shaft. Its
  sturdy-tile condition is relaxed only within that explicit tile allow-list.
- Crowbar/cutter and RCD deconstruction retain the existing tile/entity paths,
  including same-floor item drops.
- Tile types, anchored catwalks, roof markers, provider Z positions, and channel
  behavior survive two consecutive map save/load round trips.

## Navigation Support

Local navigation now distinguishes a visually non-empty shaft from a supporting
floor. Before breadcrumb chunks build in parallel, the simulation thread fills
a fixed per-chunk support mask from tile-authored Body policy and any indexed
boundary provider. Catwalk placement/removal, provider Z movement, tile edits,
and map boundary-mode changes dirty the affected floor navigation.

The support array is allocated once per cached navigation chunk. Rebuilds reuse
it, and provider-free tiles do not perform ECS boundary queries.

## Query API

`SharedZLevelSkyExposureSystem.GetExposure()` accepts a grid and local
`ZLevelTileIndices`. It walks adjacent boundaries from the origin through the
boundary above `ZLevelMapComponent.MaximumLevel`, in ascending order.

`GetExposureAtWorldZ()` converts world Z through the grid's current
`ZLevelFrameComponent.Origin` and then uses the same local query. Moving a grid
therefore reprojects cached local geometry instead of rebuilding it.

Unconfigured legacy grids use the local range `0..0`. They retain one top
boundary query and reject origins outside local Z 0.

The result is `ZLevelSkyExposureState` with one typed termination:

- `Exposed`: every required Weather boundary is open.
- `ClosedBoundary`: `BlockingLowerZ` identifies the first closed boundary.
- `InvalidGrid`, `InvalidLevel`, or `InvalidConfiguration`: the request has no
  valid authored column.
- `BoundaryResolutionFailed`: the shared boundary system could not resolve an
  adjacent pair.
- `BoundaryBudgetExceeded`: the configured scan limit ended the query.

Only `Exposed` sets `IsExposed`. Every incomplete or invalid result fails closed.

## Weather Exposure Policy

`SharedWeatherSystem.GetWeatherExposure()` is the shared gameplay policy layered
over the geometric sky query. It returns a typed `WeatherExposureState` instead
of making callers reconstruct roof, tile, blocker, and column rules.

Unconfigured maps preserve the original planar contract exactly: only local Z 0
is valid, empty tiles remain exposed, and non-empty tiles consult
`RoofComponent`/`IsRoofComponent`, the tile definition's `Weather` flag, and
anchored `BlockWeatherComponent` entities.

Configured Z-level maps apply the following ordered policy:

1. The requested local floor must be inside the authored map range.
2. A non-empty tile on that exact floor must permit weather.
3. An anchored weather blocker must share both XY and local Z with the query.
4. The complete `Weather` boundary column above the floor must reach open sky.

Planar `RoofComponent` data is intentionally ignored on configured maps because
it cannot identify a floor and would otherwise block every Z at one XY. Authored
tiles and `ZLevelRoofMarker` provide the dimensional boundary policy instead.

`GetWeatherExposureAtWorldZ()` converts through the moving grid frame, while the
entity overload uses inherited grid and local-floor state. Valid map-space
entities without a grid are exposed; nullspace and malformed requests fail
closed. Terminations distinguish local tile rejection, planar roof, same-floor
blocker, sky blockage, and invalid coordinates/grid/level.

## Weather Presentation And Audio

The client stencil builds one retained mask plan for the viewport's active
world floor. Every intersecting grid, including a legacy map-grid, converts that
world floor through its current `ZLevelFrameComponent.Origin`; grids that do not
represent the viewed floor contribute no mask. Blocked tiles are compressed
into horizontal local-space runs grouped by grid, so contiguous interiors need
one draw call per run rather than one per tile.

Mask work is atomic. The planner preflights all visible tile checks, evaluates
the shared weather policy, and then verifies the retained-run budget. If either
budget cannot represent the complete plan, the stencil masks the entire
viewport for that frame. It never exposes an arbitrary subset of an indoor area
because iteration order happened to consume the budget first.

Ambient weather audio performs one deterministic radius-three search for the
listener update, shared by every active weather effect. It checks only the
listener's exact inherited local floor and reports typed direct, nearby,
blocked, invalid, or budget-exhausted outcomes. A nearby exposed tile retains
the upstream occlusion calculation; every incomplete outcome uses the upstream
fully occluded value. Unconfigured Z 0 maps keep their planar roof behavior.

Client-owned archived limits are intentionally independent from the shared sky
cache budgets:

| CVar | Default | Effective range | Exhaustion policy |
| --- | ---: | ---: | --- |
| `zlevel.weather_mask_max_tile_checks_per_frame` | 16,384 | 0..1,000,000 | Mask full viewport |
| `zlevel.weather_mask_max_runs_per_frame` | 8,192 | 0..1,000,000 | Mask full viewport |
| `zlevel.weather_audio_max_tile_checks_per_frame` | 64 | 0..4,096 | Fully occlude weather audio |

## Cache And Budgets

The process-local cache is keyed by grid UID and local tile/floor origin. It is
a true least-recently-used cache: hot entries move existing linked-list nodes and
do not allocate per lookup.

Each represented XY column owns a revision and cached-entry count. Tile,
non-zero tile, or boundary edits increment that column revision; stale entries
recompute lazily. Map-configuration and grid-lifecycle changes remove affected
entries. Revision metadata is removed with the final cached entry, so sparse
world edits do not create an unbounded secondary index.

Configuration:

| CVar | Default | Effective range | Exhaustion policy |
| --- | ---: | ---: | --- |
| `zlevel.sky_exposure_cache_capacity` | 4,096 | 64..65,536 | Evict LRU and recompute |
| `zlevel.sky_exposure_max_boundary_checks` | 64 | 1..4,096 | Return `BoundaryBudgetExceeded` |

Both CVars are server-owned and replicated so shared client presentation uses
the same effective policy. Empty-column reads do not allocate map chunks.

## Invalidation Sources

The cache observes:

- `TileChangedEvent` for Z 0 edits.
- `ZLevelTileChangedEvent` for sparse non-zero edits.
- `ZLevelBoundaryChangedEvent` for provider placement, removal, movement, state,
  or configuration changes.
- `ZLevelMapConfigurationChangedEvent` for local floor-range/default changes.
- Grid entity termination.
- Boundary-check budget changes, which invalidate all cached results.
- Capacity changes, which immediately evict only excess LRU entries.

## Observability

`zlevelmetrics` reports query outcomes, checks, hit rate, cache occupancy,
invalid requests, boundary failures, budget exhaustion, invalidated entries,
evictions, and effective limits. `zlevelrendermetrics` and the existing Z-level
debug overlay add client-local weather plan, grid, tile, run, render, audio,
timing, fail-closed, and effective-budget counters. Resetting render metrics
also resets the weather counters.

Stress snapshots use schema version 5 and include effective sky budgets plus
query/cache/outcome counters. The 3/6/10-floor measured workload must remain hot
after warm-up when it fits the configured cache.

## Current Consumer Boundary

P7.3a defines the shared Z-aware weather gameplay query. P7.3b makes the
production client stencil and ambient-audio consumers use that policy on the
active world floor. Robust's legacy `RoofComponent`, `IsRoofComponent`, and
planar behavior remain intact on unconfigured maps; they are not silently
treated as dimensional surfaces.

The new content is already consumed by atmosphere, visibility, sound,
projectiles, explosions, effects, falling, support, and navigation through their
existing specialized boundary channels. No arbitrary weather damage is
introduced where upstream has no weather gameplay consumer. Presentation
remains scoped to map-wide weather status effects and tile-authored weather
eligibility; future spatial planet-weather volumes need a separate contract.
Interaction and authored traversal remain closed on grates; shafts open them
only when their content explicitly requests all channels.

## Verification

- P7.1b content/construction cases: 6/6 passed, including real floor-item use on
  Z 1, channel policy, catwalk support lifecycle, navigation invalidation, and
  double save/load round trips.
- Complete pathfinding plus vertical-content matrix: 27/27 passed. Atmosphere,
  sky, mapping, and movement consumers passed 38/38.
- Complete Content Z-level/placement matrix: 290 passed with one pooled skip;
  the skipped concurrent-NPC case passed 1/1 in isolation. All 291 cases have
  passing evidence.
- Content Z-level unit/analyzer matrix: 9/9 passed.
- Stress baseline: 3/3 passed at 10.5917, 15.9291, and 22.8443 ms for 3, 6, and
  10 floors, with 6,336 measured bytes, 100% warm boundary/sky hits, and zero
  measured boundary or sky evictions at every depth.
- P7.3a weather-policy cases: 3/3 passed for legacy Z 0, exact-floor blockers,
  full sky columns, entity queries, and moving frames. One thousand hot policy
  queries allocate no more than 512 bytes in total.
- Complete Content Z-level coverage has passing evidence for all 309 cases;
  308 passed in one broad run and its one pooled skip passed in isolation.
- The P7.3a baseline passed at 11.0122, 15.4494, and 23.2621 ms for 3, 6, and 10
  floors, with 6,336 measured bytes and 100% warm boundary/sky/gravity hits.
- P7.3b weather presentation passes 5/5 focused cases and 8/8 together with the
  shared policy. The cases cover legacy planar Z 0, active world floors, moving
  frames, atomic tile/run exhaustion, exact-floor audio, and CVar clamping.
- All 314 Content Z-level integration cases have passing evidence: 313 passed in
  one broad run and its one pooled aperture-cache skip passed 1/1 in isolation.
  The combined Content unit/mapping filter passes 14/14.
- A hot loop of 128 retained mask plans allocates no more than 8,192 bytes. The
  schema-version 4 baseline passes 3/3 at 10.8001, 15.6784, and 23.4932 ms for
  3, 6, and 10 floors, with 6,336 bytes, 100% warm boundary/sky/gravity hits,
  zero measured cache misses, and no PVS budget exhaustion at every depth.
- Real OpenGL capture on an NVIDIA RTX 3070 passes 24/24 pixel checks. Covered
  Z 2 remains visually unchanged while exposed Z 3 shows rain with an RMS
  contrast gap of 0.056199; 1,003 mask plans report zero fail-closed frames or
  budget exhaustion.
- A non-incremental full-solution build completes in 2m28s with zero errors and
  the same 695 established warnings as P7.3a. Dedicated non-incremental client
  and integration scans attribute no warning to a modified production/test file.

## Flight Foundation

P7.4a adds an opt-in native flight capability inside the existing vertical
solver. Targets are grid-local floor/offset pairs, movement crosses the shared
`Body` boundary channel, active flight overrides artificial gravity, and
stopping returns control to the connected gravity plane. Closed boundaries
clamp and retarget once, while ordinary fixtures continue to collide by
discrete world floor. Runtime active/target state is replicated but excluded
from map serialization. Full API, lifecycle, collision, and consumer boundaries
are documented in `ZLevelFlight.md`.

P7.4b1 makes that capability playable. Entities with
`ZLevelFlightControlsComponent` receive toggle/up/down actions only on configured
native grids. Existing jetpacks preserve legacy space behavior and become an
owned capability source on native grids with gravity. `FlyingMobBase` supplies
the dormant intrinsic capability while dragons supply controls; future flying
NPC steering remains separate.

Critical state, stun, knockdown, throws, and buckling interrupt active flight.
Initialized mapping snapshots retain authored flight tuning but strip active
targets and action references, then reconstruct fresh actions after load. The
official mapping station provides a filled jetpack on Z 0 for manual testing.
