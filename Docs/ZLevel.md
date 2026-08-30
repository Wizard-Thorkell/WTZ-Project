# ZLevel Roadmap

WTZ Project native Z-level prototype. Copyright (c) pedel and OpenAI Codex.

This document is the working roadmap for turning WTZ Project's experimental
ZLevel prototype into a high-quality native vertical-space feature. It should be
kept practical: every section exists to help future code changes stay coherent,
testable, and aligned with the desired final product.

Official repositories:

- Project and game content: https://github.com/Wizard-Thorkell/WTZ-Project
- Engine fork: https://github.com/Wizard-Thorkell/WTZ-Engine
- Active implementation ledger: [ZLevelImplementationLedger.md](ZLevelImplementationLedger.md)
- Shared trace contract: [ZLevelTrace.md](ZLevelTrace.md)
- Trace benchmark report: [ZLevelTraceBenchmarkReport.md](ZLevelTraceBenchmarkReport.md)
- Z-aware hitscan: [ZLevelHitscan.md](ZLevelHitscan.md)
- Z-aware projectile lifecycle: [ZLevelProjectiles.md](ZLevelProjectiles.md)
- Vertical surfaces and sky exposure: [ZLevelVerticalContent.md](ZLevelVerticalContent.md)
- Powered physical elevators: [ZLevelElevators.md](ZLevelElevators.md)

## Product Goal

ZLevel should make Space Station 14 feel like one continuous world with real
floors, vertical openings, and meaningful gameplay across height.

The goal is not to fake multiple disconnected maps. The goal is one map/grid
world extended with a sparse Z axis, where gameplay systems gradually become
aware of floor separation, vertical adjacency, and explicit traversal.

The target experience:

- Players can build, walk, fall, climb, see, hear, fight, and interact across
  multiple floors in ways that feel natural.
- Mappers can author layered spaces without fighting the editor.
- Existing 2D content keeps working on `z = 0`.
- Systems that are not Z-aware fail conservatively instead of leaking behavior
  across floors.
- ZLevel support can be expanded incrementally without forcing a total rewrite
  of the engine or content.

## Core Invariants

These rules are the spine of the prototype. Do not casually break them.

- Absence of non-zero layer data is the canonical representation of empty
  vertical space.
- ZLevel layer storage must stay sparse. Reads over empty vertical space must
  not allocate layers or chunks.
- ZLevel query APIs must stay bounded. Every vertical search must take an
  explicit range, depth, or known layer set.
- `TransformComponent` remains authoritative for XY.
- `ZLevelPositionComponent` is authoritative for discrete floor index and local
  vertical offset.
- `ZLevelTileIndices` and entity Z positions are local to their owning grid.
- `ZLevelMapCoordinates` and cross-grid comparisons use world Z. A grid's
  `ZLevelFrameComponent.Origin` maps local layer zero into that shared space.
- `ZLevelKinematicsComponent` stores vertical motion state.
- Legacy 2D map, movement, and tile systems continue to operate on `z = 0`
  unless a ZLevel path explicitly opts in.
- ZLevel behavior should remain opt-in during the transition.
- Missing non-zero tile layers mean empty space, not an implicit floor.
- A non-empty tile on `z + 1` currently acts as the ceiling for `z`.
- Explicit vertical content, such as stairs, ladders, shafts, grates, and holes,
  should override or refine the simple ceiling rule through well-defined APIs.

## Current State

The repository already contains a playable first-pass ZLevel prototype.

Implemented foundation:

- Sparse non-zero tile storage in RobustToolbox map chunks.
- `ZLevelTileIndices`, `ZLevelMapCoordinates`, `ZLevelEntityCoordinates`, and
  `ZLevelTileRef`.
- ZLevel tile read/write helpers on `SharedMapSystem`.
- Bounded layer queries and support searches.
- Dedicated `ZLevelTileChangedEvent`.
- Non-zero ZLevel tile replication to clients.
- Map chunk serialization support through `zTiles`.
- Z-only chunks survive full-state and delta-state replication.
- Empty non-zero writes do not allocate chunks.
- Removing the final non-zero tile uses the normal chunk lifecycle, including
  deletion history and PVS replication.
- Repeated delete/recreate/delete cycles in one delta window are coalesced into
  one final chunk state.

Implemented movement and gameplay basics:

- `ZLevelPositionComponent`.
- `ZLevelKinematicsComponent`.
- `SharedZLevelSystem`.
- Opt-in entities can stand on upper floors.
- Unsupported entities can fall to lower support layers.
- Explicit body openings can remove support from an otherwise non-empty floor
  tile without affecting unrelated boundary channels.
- `SharedZLevelSkyExposureSystem` resolves a bounded local column through the
  independent `Weather` boundary channel, including the boundary above the
  highest authored floor, with a bounded LRU cache and fail-closed budget.
- `SharedWeatherSystem` exposes one typed local/world/entity policy that combines
  exact-floor tile eligibility and blockers with the complete sky column while
  preserving planar Z 0 behavior on unconfigured maps.
- The production client weather stencil masks the active world floor through
  retained grid-local runs with atomic fail-closed frame budgets. Ambient
  weather audio searches deterministically on the listener's exact floor and
  fully occludes invalid or exhausted queries.
- Vertical movement updates `PhysicsComponent.BodyStatus`.
- Collisions between entities on different Z levels are prevented.
- Tile friction and footstep lookup can use the support floor.
- Pickup/drop logic clears held Z state and restores world Z state on drop,
  including transfers onto grids with displaced vertical frame origins.
- Placeable surfaces and storage dumps preserve world Z while converting to the
  destination grid's local frame.

Implemented connected artificial gravity:

- Native `ZLevelMap` maps no longer interpret grid-wide gravity as an infinite
  downward pull through empty Z space.
- Each active `GravityGeneratorComponent` defines an attraction plane at its
  authored local Z level.
- A sparse multi-source flood fill follows only adjacent non-empty tiles in
  `(x, y, z)`. Empty columns above or below connected station structure inherit
  its field, while disconnected asteroid or debris islands remain weightless.
- Bodies above a generator plane accelerate downward; bodies below it
  accelerate upward. Crossing the attraction plane stops vertical drift, and
  intervening floor boundaries still catch bodies in either direction.
- Field results are cached per grid and invalidated by tile, source, parent,
  power-state, and Z changes. Legacy maps keep the original grid-wide gravity
  behavior.

Implemented traversal and debug tooling:

- `ZLevelTraversalComponent`.
- Powered physical elevator cabins, mapper-authored landing controls, bounded
  authoritative travel, rider capture, APC load changes, and cancellation on
  power or topology loss. Adjacent-stop graph edges let AI call and ride the
  same cabin; initialized mapping copies stops without cloning cabins and
  double-round-trip tests preserve authored configuration without runtime trips.
- `ZLevelStairsUp`, `ZLevelStairsDown`, and `ZLevelLadder` prototypes.
- Interaction verbs for using traversal objects.
- Step-trigger traversal support.
- Stairs and ladders declare directed traversal channels on the boundary they
  connect; traversal no longer bypasses ceiling checks through a special
  boolean parameter.
- Stairs, ladders, floor openings, and shafts author the independent
  `Interaction` channel. Grates and sealed boundaries remain closed to direct
  use across floors.
- `SharedZLevelInteractionSystem` centralizes physical checks, same-world-Z
  authority, server-owned remote-eye origins, and bounded opt-in traces through
  authored interaction portals.
- Interaction outcomes and physical checks are visible in `zlevelmetrics` and
  the Z-level debug overlay.
- Pointer input now carries an optional engine-level coordinate layer. WTZ
  Content interprets it as world Z, chooses coordinates relative to the target
  grid or active viewport grid, and preserves it through context-menu and
  short-click drag replay paths. This avoids an arbitrary planar grid lookup
  when several decks or moving frames overlap in XY.
- Entity targets authoritatively own their world Z. Coordinate-only targets are
  same-floor by default, but a world action may explicitly opt into the nearest
  visible non-empty lower-floor surface under a targetless pointer. The server
  independently checks finite coordinates, map and structural-frame identity,
  downward direction, combined XY/Z range, selected world Z, and the effective
  remote-eye or relay origin before use.
- World-target actions carry the selected layer through prediction and expose
  the validated value as `WorldTargetActionEvent.TargetWorldZ`. Existing callers
  that omit the optional layer retain same-floor behavior. The action opt-in
  authorizes only destination selection; each consumer must still validate its
  own boundary channel before producing an effect.
- Normal pointer use, world activation, and alternate use always prefer a
  current-floor entity over overlapping sprites. When no current-floor target
  wins, they can deliberately select the nearest visible lower-floor entity.
- Lower-floor physical use requires both a `Visibility` path and an independently
  authored `Interaction` path, remains inside the ordinary combined XY/Z use
  range, and checks native fixture obstruction on every horizontal trace
  segment. A grate can therefore expose an entity without making it usable.
- The server repeats the lower-and-visible, frame, range, boundary, and fixture
  checks from server-owned state. Upper-floor entities remain ineligible for
  ordinary use even if a forged client knows their UID.
- Context menus include visible lower entities for examination and expose
  physical verbs only when the same use-specific authority succeeds. Pointing,
  pulling, and moving a pulled object remain same-floor operations.
- Every gameplay verb family, including the generic `Verb` used by pulling,
  rotation, UI activation, and other physical content, revalidates same-world-Z
  authority at execution time. Examine, VV, explicit forced execution, and
  authenticated administrative categories retain their remote semantics.
- Entity-targeted actions remain same-floor even when planar access checks are
  disabled. Rejected entity/world targets are terminal, and malformed entity or
  non-finite coordinate requests are rejected before rotation or execution.
- BUI requests, drag/drop, finite-range targeted DoAfters, and interaction
  relays enter guarded server-owned funnels. A relay's entity and a remote eye,
  rather than the controlling body, are the spatial origins they advertise.
- Station AI proxy replacement preserves the old eye's world Z across
  remote/physical mode switches, and its optimized BUI range override cannot
  reopen access to the body's floor from a camera on another floor.
- World-only action targets now preserve an explicit selected world Z alongside
  planar `EntityCoordinates`. Opted-in actions can select a visible lower tile,
  while closed decks, upper targets, different frames, out-of-range points, and
  sparse empty layers are rejected.
- Normal gun requests carry that selected world Z through server prediction and
  authority. Hitscan traces to a targetless lower coordinate directly; physical
  projectiles use the bounded ballistic controller, including a minimal planar
  physics step for an otherwise pure-vertical shot. Action guns and projectile
  spells forward the same contract.
- The shared firing funnel revalidates entity identity, current world Z, map,
  frame, visibility, and coordinate authority before ammo is consumed and before
  every burst follow-up. Deleted, stale, hidden, upper, different-frame, and
  out-of-range explicit targets are terminal; they cannot fall back to a planar
  coordinate shot. Invalid or stopped fire also clears transient aim state.
- Native hand throwing uses the same ranged pointer mode and coordinate-layer
  transport. The server validates an explicit entity or targetless lower-floor
  coordinate before cooldown, stack split, drop, or throw. Invalid requests keep
  the item in hand, while same-floor throws retain native behavior and perform
  no vertical route work.
- Fireball and Dragon's Breath explicitly opt into visible lower-floor
  coordinates. Other world actions remain same-floor by default, and every shot
  must still pass the independent `Projectile` boundary channel.
- Admin/debug verbs to enable/disable ZLevel mode.
- Debug hotbar actions for moving up/down or to a target Z.
- Support-floor stamping helpers.

Implemented Z-aware atmos foundation:

- ZLevel-aware tile atmos storage.
- Sparse upper/lower atmosphere cells participate in full invalidation, mixture
  enumeration, and grid lifecycle operations without scanning empty Z space.
- Vertical atmos adjacency through shared ZLevel boundary checks.
- Ceiling tiles above close vertical atmos adjacency.
- ZLevel tile changes invalidate the changed tile and vertical neighbors.
- Explicit atmosphere openings override or reinforce the tile-derived default,
  and placement/removal invalidates both sides of the boundary.
- Entity-based mixture, adjacency, space, and hotspot APIs resolve inherited Z;
  common vents, scrubbers, devices, anomalies, storage, disposal, ignition,
  smoking, cloning, and artifact consumers use those APIs.
- Airtight entities track their last Z and invalidate both their old and new
  atmosphere cells when moved between floors.
- Fire events, hotspot targets, and high-pressure movement are filtered by
  effective Z so entities sharing only XY do not affect one another.
- Pipe reachability, portable ports, overlap checks, and node-group reflooding
  keep atmos networks isolated per floor.
- Upper-floor fire deliberately suppresses legacy 2D decals and PVS audio until
  those presentation systems gain explicit vertical coordinates.

Implemented first-pass client presentation:

- `SharedZLevelVisibilitySystem` is the common bounded visibility authority for
  renderer, targeting, and server PVS. It checks at most four floors and uses
  the visibility boundary channel for every crossed boundary.
- Floors above the represented camera floor are hidden, while the current floor
  remains fully visible.
- Lower floors remain visible only through a complete stack of openings and
  fade by depth.
- Entity sprites are filtered and modulated by a draw-time engine event. The
  presentation path no longer mutates replicated `SpriteComponent.Color`.
- Sparse floor tiles use one viewport and a bounded overlay pass from the
  camera floor down to the maximum visible depth; it never scans all existing
  layers.
- View context follows the actual viewport eye, including remote `Eye.Target`
  cameras, with the local player used only as a fallback.
- Optional diagnostics are controlled by the archived client CVar
  `zlevel.debug_overlay`; normal floor presentation does not depend on it.
- Client targeting filters same-floor interactions and allows deliberate
  visible cross-floor examine/admin behavior.
- Cross-floor visibility uses the same explicit boundary resolver as movement
  and atmosphere.

Implemented first-pass Z-aware network visibility:

- The server builds per-session vertical culling snapshots at 10 Hz on the main
  thread, covering the same range and chunk margin as native PVS.
- Attached entities and all view subscriptions contribute independent camera
  contexts; visibility from any subscribed view keeps an entity relevant.

- Hidden networked entities are excluded only from normal spatial PVS. Forced,
  global, and explicit session overrides retain their native precedence.
- Culling a transform parent also culls its descendants, covering contained or
  otherwise non-spatial children without flattening entity hierarchies.
- Disabling native PVS or disconnecting a session clears its exclusion state.

Implemented authoritative Z-aware hitscan:

- Same-floor and Z 0 shots retain the established collision and target
  selection rules while filtering colliders from other world Z levels.
- Visible lower-floor entities can be targeted through open visibility paths;
  the server independently validates visibility, a shared structural frame,
  three-dimensional range, and every `Projectile` boundary crossing.
- A targetless pointer can select the nearest visible non-empty lower surface.
  The server revalidates its world Z, frame, map, range, and visibility before
  tracing, so an empty sparse layer or forged coordinate cannot become a floor.
- Explicit entity targets fail closed if their identity, current layer, map,
  frame, visibility, direction, or three-dimensional range is no longer valid.
  The trace constructor never substitutes a same-floor ray for such a failure.
- Hitscan effects are split into ordered floor segments and stamped with their
  world Z on the client.
- Upward targeting remains deferred until the viewport/FOV and input contracts
  can represent it intentionally.

Implemented Z-aware physical projectile lifecycle:

- Fired projectiles inherit the authoritative world Z of their user or gun;
  source-less projectiles preserve their explicitly authored floor.
- Thrown entities inherit a valid thrower's world Z without changing Robust's
  established horizontal throw timing and distance.
- Deliberate pointer throws can target a visible lower entity or the nearest
  visible lower-floor surface. Entity aim uses the current server transform,
  and rejection occurs before the held item is mutated.
- Cross-floor physics overlap cannot produce an impact, including on grids with
  displaced vertical frame origins.
- Impact effects carry world Z to the client, while embedded projectiles inherit
  their target's floor and preserve it when detached.
- Deliberate physical flight through deck openings is implemented for projectiles
  and thrown entities. Active flyer heights now supply continuous source and
  entity-target endpoints; lifecycle guarantees are documented in
  [ZLevelProjectiles.md](ZLevelProjectiles.md).

Implemented vertical boundary foundation:

- `SharedZLevelBoundarySystem` preserves the upper-tile rule as a compatibility
  default and applies content-driven open/closed overrides.
- Independent channels exist for body passage, upward/downward traversal,
  atmosphere, visibility, interaction, sound, and effects.
- Forced-closed channels deterministically win when providers conflict.
- Networked `ZLevelBoundaryComponent` providers can be enabled, moved, placed,
  removed, or reconfigured while emitting boundary invalidation events.
- Mapper-visible markers cover floor openings, shafts, grate-like boundaries,
  and explicit seals.

Implemented active-body and cache scaling foundation:

- `SharedZLevelSystem` maintains a per-grid, per-tile index of opt-in physics
  bodies instead of scanning every ZLevel entity when local support changes.
- Only unsupported, vertically moving, or actively thrown bodies run through
  the vertical solver every physics tick; settled bodies leave the active set.
- Tile, ZLevel tile, boundary, gravity, movement, parenting, and weightlessness
  changes wake the relevant indexed bodies.
- Boundary decisions use a bounded 4096-entry cache with explicit invalidation
  for tile data, boundary providers, and grid termination.
- Cached reads over empty space preserve sparse storage and do not allocate map
  chunks.

Implemented moving-grid frame foundation:

- Networked and serialized `ZLevelFrameComponent` gives each grid an integer
  world-Z origin while preserving sparse deck indices as grid-local data.
- Transform and map helpers explicitly convert between local tile/entity Z and
  world map Z; moving or rotating a grid between maps does not rewrite its
  decks, passengers, or chunks.
- Physics contacts, sprite filtering, targeting, shared visibility, and
  per-session PVS compare world Z, so separate grids interact only when their
  vertical frames align.
- FTL docking derives the moving grid's required origin from the paired ports:
  `target port world Z - moving port local Z`. Multi-port configurations retain
  only ports that agree on that origin.
- Direct docking refuses ports on different world layers. FTL applies the frame
  before moving the shuttle and creating dock joints.
- Changing a frame regenerates physics contacts and replicates to clients.
- Mapping and placement keep their active floor local to the selected grid,
  converting only at the map-coordinate boundary.
- Shared world-Z stamping converts construction results, cable placement,
  generic storage/entity-storage dumps, surface drops, and hand drops into the
  destination grid's local frame. Newly created floor grids inherit the actor's
  world-Z frame.

Implemented sparse structural stability and collapse:

- `ZLevelStructuralGridComponent` opts a grid into structural simulation without
  changing the behavior of legacy maps.
- The pure multi-source solver models each existing tile as a sparse local
  `(x, y, z)` node. Cores seed horizontal strength and explicit supports bridge
  adjacent decks with configurable strength and transfer loss.
- Immutable sparse snapshots are solved through a 5 ms `JobQueue`; each job
  yields every 256 nodes so large connected structures do not monopolize a
  server tick.
- Grid revisions invalidate stale job results. Structural edits made while a
  solve is running cannot apply an obsolete stability snapshot or trigger an
  obsolete collapse.
- Unsupported destructible turf receives a configurable delayed collapse.
  Restoring support cancels it, while a fresh solve that still confirms the
  failure preserves the original deadline.
- At most eight tiles collapse per tick globally. Each collapse uses the normal
  damage/destruction pipeline for anchored entities, then steps the turf down
  through its `BaseTurf` rather than deleting an entire stack at once.
- Core/support startup, shutdown, anchoring, reanchoring, local-Z changes, grid
  splits, tile changes, and round shutdown all invalidate or rebuild the live
  structural index.
- Mapper-visible core/support markers are available, and base walls provide the
  default upward support bridge on participating grids.
- `showzstability` enables an admin-only overlay. Sparse snapshots are sent only
  to opted-in sessions, and the client renders only the currently viewed deck,
  including pending-collapse warnings.

Implemented versioned mapping and placement:

- `ZLevelMapComponent` opts a map into format version 1 and declares its valid
  floor range, default floor, and default boundary policy.
- Map serialization rejects unsupported format versions, non-zero layers on
  unmarked maps, and authored layers outside the declared range.
- Legacy maps remain ordinary 2D maps. The mapper does not silently migrate or
  infer Z-level metadata for them; initializing Z-level authoring is an explicit
  action for new or deliberately updated maps.

- Placement network messages carry a ZLevel field.
- Mapping mode has an explicit active-Z spinbox.
- Client placement sends the mapping active Z, falling back to player Z outside
  mapping workflows.
- Client duplicate tile checks inspect the active Z layer.
- Client erase ignores entities outside the active floor.
- Server entity placement stamps created entities to the requested Z.
- Server tile placement writes to `SetZLevelTile` for non-zero Z.
- Server erase filters by the requested Z.
- Mapping pick mode tries to prefer the active floor.
- Shared map helpers can copy or clear bounded tile regions on a specific Z.
- Mapping/admin commands expose tile-region authoring helpers:
  `zcopytiles <gridUid> <x1> <y1> <x2> <y2> <sourceZ> <targetZ> [includeEmpty]`
  and `zcleartiles <gridUid> <x1> <y1> <x2> <y2> <z>`.
- Mapping mode can initialize a versioned map, create an empty floor, copy or
  replace a complete floor including serializable entity hierarchies, and
  delete a floor without treating the mapper's player entity as map content.
- Adjacent-floor preview shows the immediately lower and upper floor with
  distinct transparency without changing normal gameplay visibility.
- A boundary brush exposes opening, shaft, grate, and sealed marker prototypes
  directly in the mapping panel.
- `Resources/Maps/Test/ZLevel/zlevel-mapping-station.yml` is the canonical
  three-floor authoring fixture. Its fourth tile layer is a roof over the top
  playable floor. Three overlapping red, green, and blue point lights provide
  a deterministic per-floor rendering baseline described in
  `Docs/ZLevelLighting.md`.

Final verification on 2026-08-10:

- Full `SpaceStation14.slnx` build: 0 errors.
- Mapping stabilization rerun: full `SpaceStation14.slnx` build completed with
  0 errors; the existing legacy package and obsolescence warnings remain.
- Content Z-level and placement matrix: 35 passed, 0 failed.
- Versioned map-format suite: 5 passed for validation, actor safety, complete
  floor copy, infrastructure round-trip, and the official three-floor map.
- Mapping editor startup smoke test: 1 passed.
- Robust shared Z-level serialization: 4 passed.
- 55 focused tests passed across Robust shared/server integration, Content unit,
  and Content integration suites.
- Robust shared integration: 8 passed for coordinate serialization, tile-index
  serialization, cross-floor collision isolation, and displaced frame origins.
- Robust server integration: 13 passed for sparse map lifecycle, replication,
  full states, deletion, and recreation.
- Content unit: 2 structural solver tests passed.
- Content integration: 32 passed for movement, atmosphere, construction, power,
  hands, RCD, PVS, docking, and structural collapse.
- Final P2 combat authority gate: 51/51 focused hitscan/ballistic cases, 7/7
  real-network manual-throw cases, 4/4 native weapon/throw cases, 24/24 native
  interaction/action/DoAfter/pulling regressions, and 182/182 focused Z-level
  integration cases passed with no skips.
- The final P2 full solution build completed in 1m25s with zero errors and the
  established 711-warning dependency/analyzer baseline.
- The displaced-frame regressions prove that pickup/drop and RCD validation use
  world Z across grids with different local origins.
- Structural integration covers core propagation, vertical support, delayed
  collapse cancellation, support removal, local-Z reindexing, wall destruction,
  and turf collapse end to end.
- The solution still reports existing package-audit warnings in the legacy
  `Pow3r` projects for obsolete .NET Core 1.0 packages; this roadmap introduced
  no new build errors or package dependencies.

## Reference Architecture Comparison

Crystal Edge and Monolith remain valuable reference implementations, but their
core world model differs from this prototype.

GitHub marked Crystal Edge read-only and archived when this audit was performed,
so it should be treated as a strong frozen reference rather than an upstream to
follow mechanically.

- [Crystal Edge's viewport integration](https://github.com/crystallpunk-14/crystall-edge/blob/master/Content.Client/_CE/ZLevels/Core/ScalingViewport.CEZLevels.cs)
  renders a network of separate maps through repeated viewport passes and
  synthetic eyes. Its broader
  [ZLevel module](https://github.com/crystallpunk-14/crystall-edge/tree/master/Content.Shared/_CE/ZLevels)
  provides mature medieval vertical gameplay such as flight, climbing,
  throwing/falling, roofs, weather, and ladder caches.
- [Monolith's viewport port](https://github.com/Monolith-Station/Monolith/blob/main/Content.Client/_CE/ZLevels/Core/ScalingViewport.CEZLevels.cs)
  keeps the repeated-map-pass model and adds painter ordering, depth scaling,
  and cloud presentation. Its
  [PVS integration](https://github.com/Monolith-Station/Monolith/blob/main/Content.Server/_CE/ZLevels/PVS/CEPvsOverrideSystem.cs)
  globally overrides linked map entities, while its
  [transit implementation](https://github.com/Monolith-Station/Monolith/blob/main/Content.Server/_CE/ZLevels/Core/CEZLevelsSystem.Transit.cs)
  and [core components](https://github.com/Monolith-Station/Monolith/tree/main/Content.Shared/_CE/ZLevels/Core/Components)
  explore ships, planets, pilots, gravity, and transit maps.
- WTZ Project instead keeps sparse layers inside each native grid, one map
  viewport, explicit per-grid vertical origins, and per-session exclusions in
  normal PVS. It avoids repeated full-map renders, linked-map controller
  networks, and global recursive map overrides while preserving native
  hierarchy, chunk lifecycle, and sparse replication.

Structural comparison was performed against Crystal Edge commit `dcb194ee03b5`
and Monolith commit `b8d0b6d5a69a`, both fetched on 2026-08-10:

- [Crystal Edge's ZCollapse module](https://github.com/crystallpunk-14/crystall-edge/tree/master/Content.Server/_CE/ZCollapse)
  has the strongest reference implementation: opt-in stability grids, indexed
  cores/supports, a multi-source whole-column flood fill, time-sliced jobs,
  cancelable delayed collapse, an eight-tile tick budget, mapping previews, and
  a networked debug overlay. Its graph nodes must pair `(grid, x, y)` across a
  column of separate map grids.
- WTZ Project carries those proven ideas into native sparse nodes
  `(x, y, z)`. It does not need map-column discovery or cross-map coordinate
  matching, and its per-grid revision contract discards stale jobs and prevents
  stale collapse timers from firing after concurrent edits. Debug state also
  remains opt-in instead of becoming normal component replication.
- Crystal Edge is still ahead in collapse presentation: it plays collapse audio
  and throws recovered tile items onto the lower map. WTZ Project deliberately
  defers those pieces until sound/effects and falling debris have a correct
  world-Z contract.
- The inspected [Monolith repository](https://github.com/Monolith-Station/Monolith)
  contains the Crystal Edge Z-level viewport, transit, PVS, ship, and planet
  work, but no port of the ZCollapse/core/support subsystem at that commit.

Decision: do not port either renderer/PVS architecture wholesale. Reuse their
strong gameplay and presentation ideas through WTZ Project's native sparse
model. Crystal Edge's vertical gameplay is the main reference for phase 6;
Monolith's transit and frame concepts are the main reference for the moving
ship/planet stage. Clouds, painter-style depth cues, and cutaways remain useful
visual references after core lighting behavior is floor-aware.

### Final Audit Verdict

| Capability | WTZ Project | Crystal Edge | Monolith | Verdict |
| --- | --- | --- | --- | --- |
| World model | Sparse native layers per grid | Linked maps per floor | Linked maps plus transit maps | WTZ Project has the smaller, more native state model. |
| Replication and PVS | Sparse chunks and per-session normal-PVS exclusions | Linked-map overrides | Global linked-map override | WTZ Project has the clearest isolation and lifecycle contract. |
| Moving ships and planets | Explicit world/local Z frame origins and frame-aware docking | Map-network controllers | Mature transit, planet, pilot, and gravity product layer | WTZ Project has the stronger coordinate primitive; Monolith has broader product behavior. |
| Atmosphere and construction | Native sparse cells, boundaries, tools, cables, and power isolation | Broad gameplay integration on linked maps | Partial CE port | WTZ Project is ahead in engine-level integration. |
| Structural collapse | Sparse `(x, y, z)` solver, revisions, stale-job rejection, delayed collapse | Mature time-sliced solver and richer presentation | Not present at the inspected commit | WTZ Project is stronger in concurrency safety; Crystal Edge is stronger in presentation. |
| Vertical gameplay | Traversal, falling, roofs, shafts, catwalks, elevators, weather, mapping, and native flight | Mature flight, climbing, roofs, throwing, and weather | CE-derived plus space-oriented systems | WTZ has the broader native integration; Crystal Edge remains the richer climbing reference. |
| Rendering | One native viewport with floor filtering | Repeated viewport passes | Repeated passes, depth scaling, and clouds | WTZ Project is cheaper architecturally; Monolith is visually richer today. |

The project is no longer just a prototype patch. Its engine, replication,
coordinate-frame, atmosphere, construction, PVS, and structural contracts form
a coherent foundation. The next quality gains should come from closing the
remaining interaction and presentation contracts, not replacing the world
model.

## Known Gaps

Major unfinished areas:

- Mapping/editor workflow is better, but still not polished enough for real
  authoring.
- Initialized stations can produce validated, atomic mapper-authored snapshots;
  full live-round persistence of players, minds, objectives, sessions, and
  transient simulation state is deliberately unsupported.
- ZLevel tile persistence and network replication support sparse Z-only chunks,
  but the normal mapper workflow still needs more validation.
- Atmos simulation and common entity-facing machinery are Z-aware, but legacy
  `TileRef` consumers such as chemistry tile reactions, station event targets,
  and some admin tile commands still address only the base layer.
- Atmos monitoring-console pipe visualization is still a 2D projection and can
  visually merge different-floor networks even though their simulation groups
  are isolated.
- Active-floor tiles, point lights, and FOV occluders are selected natively by
  world Z. Lower-floor light and tiles now share bounded aperture projection
  with vertical attenuation, frame budgets, fail-soft degradation, lower-floor
  source shadows, and real visual capture coverage. See
  `Docs/ZLevelLighting.md`.
- Pathfinding and AI use floor-specific local navmeshes plus a hierarchical
  graph of authored stairs, ladders, and dynamic traversal. Floor support now
  also follows tile-authored shafts and catwalk boundary providers.
- Positional sound now routes through bounded same-grid vertical portals with
  pressure-aware server authorization, exact-viewer PVS, apparent portal
  direction, route attenuation, and client fail-closed safety. Cross-grid and
  room-scale material acoustics remain deliberate limits. See
  `Docs/ZLevelSound.md`.
- Hitscan, physical projectile lifecycle and flight, projectile actions, and
  authoritative explosion topology are Z-aware. Other area effects remain
  incomplete.
- Visible lower-floor entity and coordinate aiming is implemented for normal
  guns, hitscan, physical projectiles, manual throws, action guns, and projectile
  spells. Forged, deleted, stale, upper, hidden, different-frame, and range
  failures are server-authoritative and terminal. Headless P2 verification is
  complete; visual cursor, overlap, beam, and impact QA remains an explicit P8
  public-server hardening task.
- Grid lookup still begins from a 2D `MapCoordinates` selection in several
  engine APIs. Once a destination grid is known, callers now convert world Z to
  that grid's local frame correctly, but physically overlapping grids at the
  same XY need an explicit world-Z-aware grid-selection contract.
- Wall/cutaway rendering is prototype quality.
- Production lattice, interior grates, shafts, catwalk bridges, ordinary
  inter-floor roofs, mapper-authored top caps, and first-pass powered elevators
  are available. Elevator mapping, initialized save/load, pathfinding, and AI
  execution are integrated. Z-aware weather rendering/audio is integrated;
  ramps, player-built top caps, spatial weather volumes, flight-specific visual
  height, and explicit flying-NPC navigation remain pending.
- Many anchored entities and construction systems still assume one tile stack.
- FTL docking aligns grid frames, but arbitrary transit-map entry, planet
  landing, frame-authoring UI, and conflict policy for already-docked grid
  assemblies still need dedicated product rules.
- Structural stability currently rebuilds one immutable sparse snapshot for the
  whole dirty grid. The solve is time-sliced, but snapshot capture is still a
  main-thread O(existing tiles) operation; chunk-local/incremental graph updates
  remain a future optimization for very large ships.
- Structural authoring has markers and an admin overlay, but still needs polished
  mapper UI, authored beam/column families, and balance rules beyond the current
  core-strength and wall-support defaults.
- Collapse audio and tile-item debris are intentionally absent until their
  presentation and destination use explicit world-Z semantics.

## Explosion Floor Contract

- Entity-backed explosions capture map position, world Z, structural frame,
  frame-local position, and frame-local Z when queued. Deleting the source or
  translating/rotating its grid before processing does not move the blast to an
  unrelated floor or world position.
- Grid flood state is keyed by `(grid, local Z)` and space flood state by world
  Z. Airtight caches, grid edges, broadphase candidates, anchored blockers,
  turf damage, and visual payloads retain the same floor identity.
- Vertical propagation uses `ZLevelTrace` with the independent `Explosion`
  boundary channel. One vertical crossing has the same intensity cost as one
  cardinal tile; closed or invalid crossings fail conservatively.
- Prefer `QueueExplosion(EntityUid, ...)`. The `MapCoordinates` overload has no
  implicit floor data, so callers must pass `worldZ` and should pass
  `frameGrid` when structural ownership is known. Omitting both is the explicit
  compatibility path for world Z 0.
- The admin command accepts an optional final floor argument:
  `explosion intensity slope maxIntensity x y mapId prototypeId worldZ`.
  The explosion UI derives the attached administrator's world Z on the same map
  and previews only the matching local layers.
- `zlevelmetrics` reports topology builds, reached layers and tiles, vertical
  traces/cache hits/outcomes, build timings, and area/iteration budget hits.
- Non-zero local atmospheric heat, vertical sound propagation, camera-shake
  filtering, and generated debris/effect stamping are intentionally assigned to
  P2.3b, P4, and P2.3c rather than approximated inside blast topology.

## Roadmap Phases

### Current Execution Order

This is the authoritative implementation order as of 2026-08-10. The detailed
feature phases below remain the backlog and acceptance criteria for each area.

1. [Done] Preserve the prototype with branches, commits, tags, and verified Git
   bundles.
2. [Done] Stabilize chunk lifecycle and replication, including sparse Z-only
   chunks, full states, delta deletion, and real client/server PVS coverage.
3. [Done] Replace implicit ceiling behavior with explicit vertical boundaries.
4. [Done] Add active vertical bodies and bounded caches so work scales with
   relevant entities and known layers.
5. [Done] Integrate renderer and PVS behavior around visible floors and
   openings.
6. [Done] Stabilize atmosphere on top of the shared boundary model.
7. [Done] Make core construction, RCD, floor tools, anchoring, and power topology
   respect vertical layers.
8. [Done] Define a frame model for moving ships, stations, and planets, and
   integrate it with FTL docking, physics, renderer, PVS, and map coordinates.
9. [Done] Add structural support and collapse as a late-stage consumer of the
   mature vertical model.
10. [Done] Run the final cross-stage audit, full affected test matrix, and
    produce the implementation/comparison handoff.

Each completed stage should leave a focused commit, regression tests, and an
updated verification record in this document.

### Phase 0: Preserve the Prototype

Goal: make sure the current prototype is not lost and can be safely iterated.

Tasks:

- [Done] Preserve the current ZLevel work as a coherent baseline. Baseline
  commits are `84d4ccbfc8` in Space Station 14 and `b138a3fa3` in RobustToolbox.
- [Done] Create `zlevel-roadmap` branches and annotated
  `zlevel-baseline-2026-08-10` tags in both repositories.
- [Done] Create and verify incremental bundles under
  `C:\Users\pedel\source\repos\zlevel-backups`.
- Keep `AI instructions on zlevel.txt` as historical handoff context, but make
  this file the canonical roadmap.
- [Done] Add a short smoke-test command list to this document.
- Add or update code comments only where they explain non-obvious invariants.
- Avoid unrelated refactors while stabilizing the feature.

Exit criteria:

- Focused ZLevel tests pass.
- The repo builds from a clean checkout with the ZLevel changes applied.
- New work can reference this roadmap instead of rediscovering architecture.

Smoke-test commands:

- `dotnet test RobustToolbox/Robust.Shared.IntegrationTests/Robust.Shared.IntegrationTests.csproj --filter "FullyQualifiedName~ZLevelSerializationTest" --no-restore`
- `dotnet test RobustToolbox/Robust.Server.IntegrationTests/Robust.Server.IntegrationTests.csproj --filter "FullyQualifiedName~ZLevelMapTests|FullyQualifiedName~ZLevelChunkReplicationTest" --no-restore`
- `dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~ZLevel|FullyQualifiedName~PlacementZLevel" --no-restore`
- In mapping/admin console, use `zcopytiles` to copy a bounded tile patch from
  one floor to another, then `zcleartiles` to remove it from the target floor.
  Save and reload the map to confirm the `zTiles` data survives.

### Phase 1: Mapping And Authoring Workflow

Goal: make it natural to create, inspect, edit, erase, and save layered spaces.

Tasks:

- [Done] Add a clear active-Z control to mapping mode.
- [Done] Add an explicit, versioned Z-level map contract and reject invalid
  saves before writing YAML.
- [Done] Ensure tile placement always targets the selected/active Z.
- [Done] Ensure entity placement stamps the entity to the selected/active Z.
- [Done] Ensure tile erase targets only the selected/active Z.
- [Done] Ensure entity erase targets only same-floor entities unless explicitly in an
  admin/all-floor mode.
- [Done] Ensure rectangle erase respects active Z for both tiles and entities.
- Ensure pick mode chooses active-floor tiles/entities first.
- [Done] Add visual mapping feedback for the current and immediately adjacent
  layers.
- [Done] Add a mapper-safe workflow for copying a complete floor, including
  tiles and serializable entity hierarchies, to another Z.
- [Done] Add UI operations for creating and deleting complete floors; retain
  `zcopytiles` and `zcleartiles` for bounded tile-only maintenance.
- [Done] Expose explicit vertical boundary authoring as a mapper brush.
- [Done] Validate map save/load of authored non-zero layers in controlled
  fixture maps.
- [Done] Decide whether the editor's active Z should come from the player entity,
  mapping UI state, or both. Prefer explicit UI state for mappers, with player Z
  as a fallback.

Tests:

- [Covered] Tile placement on `z = 0` still uses legacy tiles.
- [Covered] Tile placement on `z = 1` writes only a ZLevel tile.
- [Covered] Entity placement on `z = 1` adds `ZLevelPositionComponent`.
- [Covered] Entity placement on `z = 0` does not leave stale Z components.
- [Covered] Entity erase cannot erase an entity on another floor.
- [Covered] Rectangle erase cannot erase entities on another floor.
- Pick mode returns the active-floor tile when stacks overlap.
- [Covered] Saved fixture maps preserve non-zero `zTiles`.
- [Covered] Loaded fixture maps restore non-zero `zTiles` without allocating
  empty layers.
- [Covered] Complete floor copy preserves anchored entities and survives a
  save/load cycle.
- [Covered] Cable, atmosphere pipe, APC, walls, stairs, and spawn markers retain
  their effective floor and anchoring through repeated save/load cycles.
- [Covered] Copying or deleting a floor never copies or deletes an attached
  actor, and actor Z state is excluded from authored-map validation.

Exit criteria:

- [Covered] A mapper can build a small two-floor test room using normal tools.
- [Covered] The room can be saved, loaded, and edited again.
- [Covered] Cross-floor erase/pick accidents are covered by tests.

Mapper workflow:

1. Create or load a mapping map. Initialized map roots support authenticated
   floor create/copy/delete, filtered authored snapshots, and validated atomic
   autosave. Initialized grid-only autosave remains unsupported because it lacks
   the complete map ownership boundary.
2. Press the Z-level initialize button once. This adds format version 1 with the
   active floor as the initial range and default floor.
3. Select a Z value, then create an empty floor or copy an existing floor into
   it. Copy replaces the target floor's tiles and map-savable entity hierarchy.
4. Place and erase tiles, entities, decals, cables, pipes, and structures using
   normal mapping tools; placement and erase are restricted to the active Z.
5. Choose the default boundary policy, then use the boundary brush for local
   openings, shafts, grates, or explicit seals. Adjacent preview can be toggled
   while aligning floors.
6. Save normally. Validation runs before YAML is written and refuses undeclared
   layers, unsupported format versions, or non-zero layers on an unmarked map.
7. Reload and continue editing. The canonical manual fixture is
   `/Maps/Test/ZLevel/zlevel-mapping-station.yml`.

Round smoke workflow:

1. Run `forcemap ZLevelMappingStation` from the server/admin console.
2. Restart or start the round normally.
3. Join as Passenger. The fixture is registered as a cut-down `TestStation`,
   with Passenger, late-join, and observer spawn points. Its mini gravity
   generator bypasses APC power only for this laboratory map so the connected
   field at `z = 0` is immediately available for manual tests.

### Phase 2: Explicit Vertical Openings

Goal: replace the single "tile above means ceiling" rule with content-driven
vertical boundary semantics.

Tasks:

- [Done] Define a shared vertical boundary API.
- [Done except ramps] Add components/prototypes for open floors, shafts, grates,
  catwalks, ladders, and stairwells. Ramps remain future content.
- [Done for current content] Solid/partial/open surfaces are tile definitions;
  catwalks and exceptional boundary overrides are anchored providers.
- [Done] Add a way for traversal content to open or override the boundary between two
  adjacent floors.
- [Done] Add support for one-way or restricted traversal through separate
  upward and downward traversal channels.
- [Done] Make atmos, visibility, falling, and traversal all use the same boundary
  decision.
- [Done] Add mapper-visible markers for openings.
- [Covered except ramps] Channel conflicts, free-fall holes, shafts, grates,
  catwalk support, stairs, atmosphere, construction, navigation, and persistence
  have dedicated coverage.

Design notes:

- Avoid hardcoding every exception directly inside `SharedMapSystem`.
- Prefer a small boundary query/event that content systems can answer.
- Keep bounded queries. A hole can open one boundary; it should not imply an
  unbounded vertical scan.

Exit criteria:

- [Covered] Players can fall through a mapped hole.
- [Covered] Atmos can leak through an explicit opening.
- [Covered] A grate/catwalk allows selected channels while supporting movement;
  a catwalk over a shaft restores Body support without sealing the other
  channels.
- [Covered] Stairs/ladders no longer need special hacks for the basic boundary rule.

### Phase 3: Interaction And Targeting Polish

Goal: make clicking, examining, using, pulling, pickup, context menus, and admin
inspection feel intentional across floors.

Tasks:

- [Done automated/code-path] Audit all client click resolution paths. Pointer layer transport,
  frame selection, context-menu synthesis, drag replay, entity-target selection,
  same-floor-first click priority, and explicit visible lower-coordinate action
  authority and gun/projectile/throw consumers are covered, including real
  client/server forged, deleted, and stale request rejection. Visual cursor and
  overlapping-sprite feel is deferred to the P8 in-game hardening matrix.
- [Done server-side] Ensure same-floor interaction is the default for
  use/pickup/pull, verbs, BUI, entity actions, drag/drop, and finite-range
  targeted DoAfters.
- [Done] Allow cross-floor examine only through visible openings.
- Keep admin inspection capable of deliberate cross-floor targeting.
- [Done] Improve click priority when entities overlap in XY but differ in Z.
- [Done] Add support for selecting lower-floor entities through holes without
  stealing normal same-floor clicks.
- [Done] Ensure verbs only appear for valid floor contexts and revalidate their
  physical authority at execution.
- [Done] Ensure construction/deconstruction interactions target active floor
  surfaces, including grate and shaft stacks on non-zero floors.
- [Done] Add tests for same-XY entities on different floors.

Exit criteria:

- The user cannot accidentally interact with a hidden entity on another floor.
- Examine through a hole is possible and predictable.
- Context menus do not list invalid cross-floor entities.

### Phase 4: Rendering And Presentation

Goal: make floors readable and attractive without relying on debug-only visuals.

Tasks:

- [Done] Replace sprite-color mutation hacks with a draw-time presentation
  path.
- [Partial] Refine lower-floor fade, occlusion, and cutaway behavior. Bounded
  opening-aware tile and sprite depth is implemented; wall-specific cutaways
  remain.
- [Done] Hide floors above while preserving useful context around openings.
- Explore wall cutaways for upper floors and vertical shafts.
- [Done] Add optional mapper/debug overlays for layer inspection.
- Ensure ZLevel presentation works with common lighting scenarios.
- [Done] Bound lower-floor presentation to four levels and fade it by depth to
  avoid visually noisy unbounded stacks.

Exit criteria:

- A two-floor room is readable at a glance.
- Looking down through an opening feels intentional.
- Hidden upper floors do not leak confusing sprites.
- Debug overlays are optional, not required for normal play.

### Phase 5: Atmos Stabilization

Goal: make ZLevel atmos reliable enough for gameplay scenarios.

Tasks:

- [Partial] Create a dedicated upper-floor atmos fixture map. The integration
  suite currently authors sparse layers over the established atmos room at
  runtime; a mapper-authored fixture remains useful for manual QA.
- [Done] Unskip and fix the containing-mixture test for entities on `z = 1`.
- [Done] Confirm child entities inherit parent Z for atmos sampling.
- [Done] Confirm common entity-facing atmos tools and devices read the correct
  floor.
- [Done] Confirm hotspots, fire targeting, pressure movement, LINDA processing,
  global mixture operations, and invalidation respect floor separation.
- [Done] Confirm ceiling/opening changes update atmos promptly.
- [Done] Define pressure behavior in shafts as normal tile adjacency through an
  atmosphere-open vertical boundary.
- [Covered] Add sparse-map checks. Enumeration and invalidation visit allocated
  chunk/layer data rather than scanning a vertical range; broader profiling on
  production-sized maps remains part of final performance QA.
- [Done] Keep atmos pipe networks and overlap restrictions isolated by Z and
  reflood node groups when their owner's effective floor changes.

Exit criteria:

- [Covered] A sealed lower room and open upper floor maintain distinct atmos.
- [Covered] Opening a shaft allows expected gas movement.
- [Covered] Entity-facing atmos tools report the floor the user is actually on.
- [Covered for the simulation core and common entity APIs] No common atmos
  processing path silently assumes `z = 0`; remaining 2D `TileRef` consumers
  are listed under Known Gaps for their owning gameplay phases.

### Phase 6: Construction And Gameplay Systems

Goal: adapt common station gameplay so ZLevel becomes useful outside debug maps.

Tasks:

- [Partial] Audit construction, RCD, tile replacement, wall building, windows,
  grilles, disposal, wires, pipes, cables, and machine anchoring. Construction,
  RCD, floor tiles, anchoring, and cable/power node topology are covered;
  disposals and remaining specialized machines still need focused audits.
- [Done for common construction paths] Ensure surface validation is
  active-Z-aware.
- [Done for floor tiles and RCD] Ensure deconstruction does not affect another
  floor.
- [Done for common construction and anchoring paths] Ensure anchored entity
  queries are filtered by Z where gameplay expects one floor.
- [Done] Keep cable and power node groups isolated per floor and reflood them
  when an entity's effective Z changes.
- [Done] Preserve independent tile replacement history for sparse upper floors,
  including network and map-data serialization of three-dimensional indices.
- [Done for generic storage, entity storage, and placeable surfaces] Preserve
  world Z while converting drops into the destination grid's local frame.
  Specialized ejectors and thrown/landed entities still need dedicated audits.
- Decide how multi-floor machines or tall entities should be represented.

Exit criteria:

- [Covered for construction graphs, floor stacks, and RCD] A mapper/player can
  build and modify upper floors with normal tools.
- [Covered for the implemented common paths] Construction actions do not leak
  across floors.
- [Covered for common anchoring blockers] Common anchored components behave as
  if each floor has its own surface unless a system explicitly opts into
  cross-floor behavior.

Verification (2026-08-10):

- `ZLevelMovementTest`: 10 passed, including inherited entity Z and independent
  upper-floor tile history.
- `TestCableNodeGroupsAreIsolatedByZLevel`: passed.
- `RCDConstructionDeconstructionTest`: passed for legacy `z = 0` behavior.
- `RCDConstructionUsesTheUsersZLevel`: passed for upper-floor wall and floor
  construction without changing the base tile.
- `RCDValidationUsesWorldZLevelAcrossGridFrames`: passed for equal world floors
  represented by different grid-local indices and rejects a later frame
  mismatch.
- `TestPickupDropPreservesUpperFloorZLevelOnDisplacedFrame`: passed for hand
  pickup/drop on a grid whose local floor one maps to world floor six.
- `TestEntityStorageEjectionPreservesWorldZLevelOnDisplacedFrame`: passed for
  materializing an inherited world floor in the destination grid's local frame.
- `ZLevelTileIndicesSerializerTest`: 2 passed for round-trip and malformed-data
  validation.
- Content shared, server, client, and integration-test projects build with zero
  errors.

### Phase 7: Navigation And AI

Goal: make non-player actors understand floors and traversal.

Tasks:

- [Done] Represent authored ZLevel traversal edges in an indexed, directed
  navigation contract with local/world frames, costs, revisions, metrics, and
  bounded connected regions.
- [Done except ramps] Teach pathfinding about stairs, ladders, shafts, catwalk
  bridges, dynamic traversal, and powered physical elevator cabins. Ramps remain
  pending.
- [Done] Ensure AI cannot path through sealed ceilings or unsupported shafts.
- [Done at connector-contract level] Add costs for vertical traversal.
- [Done] Add hierarchical fallback behavior when no same-floor path exists.
- [Done] Validate follow/hostile mobs and concurrent NPCs between floors.

Architecture and package boundaries are documented in
`Docs/ZLevelPathfinding.md`. Local polygon graphs are floor-specific, route
search is hierarchical and budgeted, and vertical-content changes invalidate
only their affected floor chunks.

Exit criteria:

- AI can path from one floor to another through mapped traversal.
- AI does not path through a closed ceiling.
- Simple hostile/follow behavior works in a two-floor test map.

### Phase 8: Lighting, FOV, Sound, And Effects

Goal: make sensory and area systems respect vertical separation.

Tasks:

- Audit lighting visibility against active floor.
- Decide how light travels through openings.
- Make FOV floor-aware.
- Make sound propagation floor-aware, with openings and shafts as connectors.
- Make explosions, fire, heat, smoke, EMP, radiation, and area effects use
  Z-aware adjacency or explicit volume rules.
- Make projectiles and hitscan respect Z unless intentionally vertical.
- Add tests or fixtures for each high-risk system.

Exit criteria:

- A flash/explosion/projectile does not affect hidden entities on another floor
  unless the rules explicitly allow it.
- Sound and light through openings are understandable and tunable.

### Phase 9: Persistence, Migration, And Tooling

Goal: make ZLevel content safe to ship, maintain, and migrate.

Tasks:

- Separate controlled map serialization issues from live runtime state issues.
- Validate `zTiles` serialization across map save/load.
- Add tooling to inspect non-zero layers in saved maps.
- Add tooling to remove empty or invalid Z layers.
- Document how to author ZLevel maps.
- Add migration notes for old maps.
- Decide how map renderer and external tooling should visualize layers.

Exit criteria:

- Controlled maps round-trip with ZLevel tiles.
- Tooling can show which layers exist.
- Mappers have a documented workflow.

## Systems Audit Checklist

When touching a system, ask:

- Does it query tiles by XY only?
- Does it query anchored entities by XY only?
- Does it use `TransformComponent.GridUid` and assume one floor?
- Does it use map coordinates where Z should matter?
- Does it create, delete, move, or clone entities that should inherit floor?
- Does it need same-floor-only behavior?
- Does it need visible-through-opening behavior?
- Does it need explicit cross-floor behavior?
- Does it need to react to `ZLevelTileChangedEvent`?
- Does it need to preserve sparse-layer invariants?

Common systems to audit:

- Placement and mapping.
- Construction and RCD.
- Interaction and verbs.
- Pickup, drop, throw, and landing.
- Storage and dumping.
- Atmos tools and gas reactions.
- Fire, hotspots, and heat.
- Explosions and area effects.
- Projectiles and melee reach.
- Lighting and FOV.
- Sound.
- AI and pathfinding.
- Power, wires, pipes, disposals, and other grid networks.
- Admin tools and debugging commands.
- Map renderer and map save/load tooling.

## Test Strategy

Prefer small, controlled fixture maps over initialized station maps.

Required test categories:

- Map storage and sparse reads.
- Map chunk serialization.
- Client replication of non-zero tiles.
- Movement support and falling.
- Boundary traversal.
- Stairs/ladders and step triggers.
- Same-XY different-Z collision prevention.
- Pickup/drop Z preservation.
- Placement and mapping authority.
- Atmos vertical adjacency and containing mixtures.
- Visibility and targeting filters.
- Save/load of fixture maps with non-zero layers.

Useful smoke commands:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj --filter "FullyQualifiedName~ZLevel|FullyQualifiedName~PlacementZLevel" --no-restore
```

Deterministic performance baselines:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_baseline.ps1
```

Use `-NoBuild` after compiling `Content.IntegrationTests`, and
`-OutputDirectory <path>` to select the snapshot directory. The runner creates
one schema-versioned JSON document for each 3-, 6-, and 10-floor generated
fixture. Each document records fixture topology, workload dimensions, elapsed
time, managed allocations, and the complete warm-up and measured metrics.

The baseline tests use structural assertions only. Compare timings and
allocations on equivalent machines and configurations; do not treat local Debug
measurements as portable release thresholds. Current captures and their review
are recorded in `Docs/ZLevelImplementationLedger.md` and
`Docs/ZLevelBaselineReport.md`.

Schema-versioned snapshots also record the effective boundary-cache,
visibility-distance, and PVS-check budgets. These can be tuned with
`zlevel.boundary_cache_capacity`, `zlevel.visibility_max_level_distance`, and
`zlevel.pvs_visibility_check_budget`; see the baseline report for clamps and
fail-soft behavior.

Run broader tests after touching shared map, serialization, placement, atmos, or
movement code.

## Implementation Guidelines

- Prefer existing repo patterns over new abstractions.
- Keep changes tightly scoped to the system being made Z-aware.
- Add Z-aware overloads rather than silently changing legacy 2D APIs when
  compatibility matters.
- Do not allocate non-zero layers during read/query paths.
- Avoid unbounded vertical scans.
- Centralize vertical boundary decisions so movement, atmos, visibility, and
  traversal do not drift apart.
- Treat `z = 0` as the compatibility layer.
- Use components/prototypes for content-specific behavior.
- Add tests for every cross-floor bug fixed.
- Keep debug tools useful, but do not let the final feature depend on debug UI.

The shared geometric request/result contract and its consumer boundaries are
documented in `Docs/ZLevelTrace.md`. Keep damage, attenuation, target selection,
and presentation in specialized systems rather than adding those policies to
`SharedZLevelTraceSystem`.

Native flight's movement, gravity, collision, lifecycle, and save-state
contracts are documented in `Docs/ZLevelFlight.md`. P7.4b1 supplies native
actions, intrinsic/jetpack content, interruption policy, and mapping coverage.
P7.4b2a supplies continuous flight-aware trace, hitscan, and physical trajectory
endpoints. P7.4b2b supplies explicit capability-gated flight corridors and
server-authoritative AI execution through the native vertical solver.

## Definition Of Done

ZLevel becomes production-quality when:

- Mappers can create and maintain layered maps using normal workflows.
- Players can naturally navigate and interact with multiple floors.
- Common gameplay systems respect floor separation.
- Vertical openings have explicit, content-driven semantics.
- Atmos, visibility, sound, lighting, effects, and AI have coherent Z rules.
- Saved maps preserve ZLevel data reliably.
- The feature has enough tests to prevent accidental regression.
- Existing 2D maps and gameplay keep working without requiring ZLevel content.

Until then, this remains a strong prototype with a clear path forward.
