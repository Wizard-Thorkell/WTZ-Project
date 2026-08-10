# ZLevel Roadmap

DragonStation Z-Level prototype. Copyright (c) pedel and OpenAI Codex.

This document is the working roadmap for turning DragonStation's experimental
ZLevel prototype into a high-quality native vertical-space feature. It should be
kept practical: every section exists to help future code changes stay coherent,
testable, and aligned with the desired final product.

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
- Vertical movement updates `PhysicsComponent.BodyStatus`.
- Collisions between entities on different Z levels are prevented.
- Tile friction and footstep lookup can use the support floor.
- Pickup/drop logic clears held Z state and restores world Z state on drop.
- Placeable surfaces stamp dropped objects to the surface floor.

Implemented traversal and debug tooling:

- `ZLevelTraversalComponent`.
- `ZLevelStairsUp`, `ZLevelStairsDown`, and `ZLevelLadder` prototypes.
- Interaction verbs for using traversal objects.
- Step-trigger traversal support.
- Stairs and ladders declare directed traversal channels on the boundary they
  connect; traversal no longer bypasses ceiling checks through a special
  boolean parameter.
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

Partially implemented mapping and placement:

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

Verified recently:

- Full `SpaceStation14.slnx` build: 0 errors.
- Robust ZLevel map suite after sparse atmosphere enumeration support: 12
  passed, 0 skipped, 0 failed.
- Content ZLevel atmosphere suite: 7 passed, 0 skipped, 0 failed.
- The former containing-mixture test is enabled and covers direct and inherited
  upper-floor positions.
- Combined Content ZLevel and hands regression: 24 passed, 0 skipped, 0 failed.
- Full `SpaceStation14.slnx` build after atmosphere integration: 0 errors.

## Reference Architecture Comparison

Crystal Edge and Monolith remain valuable reference implementations, but their
core world model differs from this prototype.

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
- DragonStation instead keeps sparse layers inside one native map/grid, one
  viewport, one spatial frame, and per-session exclusions in normal PVS. It
  avoids repeated full-map renders and global recursive map overrides while
  preserving native hierarchy, chunk lifecycle, and sparse replication.

Decision: do not port either renderer/PVS architecture wholesale. Reuse their
strong gameplay and presentation ideas through DragonStation's native sparse
model. Crystal Edge's vertical gameplay is the main reference for phase 6;
Monolith's transit and frame concepts are the main reference for the moving
ship/planet stage. Clouds, painter-style depth cues, and cutaways remain useful
visual references after core lighting behavior is floor-aware.

## Known Gaps

Major unfinished areas:

- Mapping/editor workflow is better, but still not polished enough for real
  authoring.
- Live map save/load on initialized station maps is not generally safe.
- ZLevel tile persistence and network replication support sparse Z-only chunks,
  but the normal mapper workflow still needs more validation.
- Atmos simulation and common entity-facing machinery are Z-aware, but legacy
  `TileRef` consumers such as chemistry tile reactions, explosions, station
  event targets, and admin tile commands still address only the base layer.
- Atmos monitoring-console pipe visualization is still a 2D projection and can
  visually merge different-floor networks even though their simulation groups
  are isolated.
- Upper-floor fire audio and burned decals are suppressed until sound/effects
  gain a Z-aware spatial contract.
- Lighting and FOV are not native to floors.
- Pathfinding and AI do not understand multi-floor navigation.
- Sound propagation is not Z-aware.
- Projectiles, hitscan, explosions, fire, heat, and area effects are not
  consistently Z-aware.
- Click priority and interaction semantics need more coverage.
- Wall/cutaway rendering is prototype quality.
- Special vertical structures are missing: holes, shafts, open floor tiles,
  catwalks, grates, ramps, elevators, and climbable structures beyond simple
  stairs/ladders.
- Many anchored entities and construction systems still assume one tile stack.

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
8. [Next] Define a frame model for moving ships, stations, and planets.
9. Add structural support and collapse as a late-stage consumer of the mature
   vertical model.

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
- [Done] Ensure tile placement always targets the selected/active Z.
- [Done] Ensure entity placement stamps the entity to the selected/active Z.
- [Done] Ensure tile erase targets only the selected/active Z.
- [Done] Ensure entity erase targets only same-floor entities unless explicitly in an
  admin/all-floor mode.
- [Done] Ensure rectangle erase respects active Z for both tiles and entities.
- Ensure pick mode chooses active-floor tiles/entities first.
- Add visual mapping feedback for current Z, below layers, and hidden above
  layers.
- [Partial] Add a mapper-safe workflow for copying a floor patch to another Z.
  Shared map APIs and admin/mapping commands exist and compile; polished mapping
  UI exposure still needs work.
- [Partial] Add mapper commands for clearing a Z layer or a bounded region on a
  Z layer. `zcleartiles` handles bounded regions; full-layer cleanup should
  either wrap the same API with known bounds or be added as a separate command.
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

Exit criteria:

- A mapper can build a small two-floor test room using normal tools.
- The room can be saved, loaded, and edited again.
- Cross-floor erase/pick accidents are covered by tests.

### Phase 2: Explicit Vertical Openings

Goal: replace the single "tile above means ceiling" rule with content-driven
vertical boundary semantics.

Tasks:

- [Done] Define a shared vertical boundary API.
- [Partial] Add components/prototypes for open floor, hole, shaft, grate, catwalk, ladder
  shaft, stairwell, and ramp concepts.
- [Partial] Decide which concepts are tile definitions and which are anchored
  entities. Explicit providers are anchored entities; final gameplay structures
  and tile-definition integration remain to be selected per concept.
- [Done] Add a way for traversal content to open or override the boundary between two
  adjacent floors.
- [Done] Add support for one-way or restricted traversal through separate
  upward and downward traversal channels.
- [Done] Make atmos, visibility, falling, and traversal all use the same boundary
  decision.
- [Done] Add mapper-visible markers for openings.
- [Partial] Add tests for each opening type. Channel conflicts, free-fall holes,
  stairs, and atmosphere lifecycle are covered; final ramp/catwalk gameplay
  entities still need dedicated tests.

Design notes:

- Avoid hardcoding every exception directly inside `SharedMapSystem`.
- Prefer a small boundary query/event that content systems can answer.
- Keep bounded queries. A hole can open one boundary; it should not imply an
  unbounded vertical scan.

Exit criteria:

- [Covered] Players can fall through a mapped hole.
- [Covered] Atmos can leak through an explicit opening.
- [Covered at boundary level] A grate/catwalk can allow visibility or gas based on chosen design rules while
  still supporting movement.
- [Covered] Stairs/ladders no longer need special hacks for the basic boundary rule.

### Phase 3: Interaction And Targeting Polish

Goal: make clicking, examining, using, pulling, pickup, context menus, and admin
inspection feel intentional across floors.

Tasks:

- Audit all client click resolution paths.
- Ensure same-floor interaction is the default for use/pickup/pull.
- Allow cross-floor examine only through visible openings.
- Keep admin inspection capable of deliberate cross-floor targeting.
- Improve click priority when entities overlap in XY but differ in Z.
- Add support for selecting lower-floor entities through holes without stealing
  normal same-floor clicks.
- Ensure verbs only appear for valid floor contexts.
- Ensure construction/deconstruction interactions target active floor surfaces.
- Add tests for same-XY entities on different floors.

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
- Add Z-aware checks to storage dumps, placeable surfaces, and thrown/landed
  items.
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
- `ZLevelTileIndicesSerializerTest`: 2 passed for round-trip and malformed-data
  validation.
- Content shared, server, client, and integration-test projects build with zero
  errors.

### Phase 7: Navigation And AI

Goal: make non-player actors understand floors and traversal.

Tasks:

- Represent ZLevel traversal edges in navigation data.
- Teach pathfinding about stairs, ladders, shafts, and ramps.
- Ensure AI cannot path through sealed ceilings.
- Add costs for vertical traversal.
- Add fallback behavior when no same-floor path exists.
- Validate mobs following players between floors.

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
