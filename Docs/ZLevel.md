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

Implemented first-pass atmos:

- ZLevel-aware tile atmos storage.
- Vertical atmos adjacency through shared ZLevel boundary checks.
- Ceiling tiles above close vertical atmos adjacency.
- ZLevel tile changes invalidate the changed tile and vertical neighbors.
- Basic tests cover ceiling invalidation.
- Explicit atmosphere openings override or reinforce the tile-derived default,
  and placement/removal invalidates both sides of the boundary.

Implemented first-pass client presentation:

- Floors above the player are hidden.
- Current floor is fully visible.
- Lower floors can remain visible through openings.
- Lower-floor sprites fade by depth.
- Client targeting filters same-floor interactions and allows deliberate
  visible cross-floor examine/admin behavior.
- Cross-floor visibility uses the same explicit boundary resolver as movement
  and atmosphere.

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
- Focused Robust lifecycle/PVS run: 12 passed, 0 skipped, 0 failed.
- Broader Robust chunk, map, serialization, and physics runs: 17 passed,
  0 skipped, 0 failed.
- Focused Content ZLevel run after explicit boundaries: 12 passed, 1 skipped,
  0 failed.
- The skipped test is an atmos containing-mixture test that needs a dedicated
  upper-floor fixture.

## Known Gaps

Major unfinished areas:

- Mapping/editor workflow is better, but still not polished enough for real
  authoring.
- Live map save/load on initialized station maps is not generally safe.
- ZLevel tile persistence and network replication support sparse Z-only chunks,
  but the normal mapper workflow still needs more validation.
- Atmos is Z-aware adjacency on top of mostly 2D machinery, not full volumetric
  atmos.
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
4. [Next] Add active vertical bodies and bounded caches so work scales with relevant
   entities and known layers.
5. Integrate renderer and PVS behavior around visible floors and openings.
6. Stabilize atmosphere on top of the shared boundary model.
7. Expand vertical gameplay, construction, interaction, effects, and AI.
8. Define a frame model for moving ships, stations, and planets.
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

- Replace sprite-color mutation hacks with a more robust presentation path if
  needed.
- Refine lower-floor fade, occlusion, and cutaway behavior.
- Hide floors above while preserving useful context around openings.
- Explore wall cutaways for upper floors and vertical shafts.
- Add optional mapper/debug overlays for layer inspection.
- Ensure ZLevel presentation works with common lighting scenarios.
- Avoid making lower floors visually noisy during normal play.

Exit criteria:

- A two-floor room is readable at a glance.
- Looking down through an opening feels intentional.
- Hidden upper floors do not leak confusing sprites.
- Debug overlays are optional, not required for normal play.

### Phase 5: Atmos Stabilization

Goal: make ZLevel atmos reliable enough for gameplay scenarios.

Tasks:

- Create a dedicated upper-floor atmos fixture map.
- Unskip and fix the containing-mixture test for entities on `z = 1`.
- Confirm child entities inherit parent Z for atmos sampling.
- Confirm gas analyzers and atmos tools read the correct floor.
- Confirm hotspots, fire, superconduction, LINDA processing, and invalidation
  all respect vertical adjacency.
- Confirm ceiling/opening changes update atmos promptly.
- Decide how pressure behaves in shafts and open multi-floor volumes.
- Add performance checks for tall but sparse maps.

Exit criteria:

- A sealed lower room and open upper floor maintain distinct atmos.
- Opening a shaft allows expected gas movement.
- Atmos tools report the floor the user is actually on.
- No common atmos processing path silently assumes `z = 0`.

### Phase 6: Construction And Gameplay Systems

Goal: adapt common station gameplay so ZLevel becomes useful outside debug maps.

Tasks:

- Audit construction, RCD, tile replacement, wall building, windows, grilles,
  disposal, wires, pipes, cables, and machine anchoring.
- Ensure surface validation is active-Z-aware.
- Ensure deconstruction does not affect another floor.
- Ensure anchored entity queries are filtered by Z where gameplay expects one
  floor.
- Add Z-aware checks to storage dumps, placeable surfaces, and thrown/landed
  items.
- Decide how multi-floor machines or tall entities should be represented.

Exit criteria:

- A mapper/player can build and modify upper floors with normal tools.
- Construction actions do not leak across floors.
- Common anchored components behave as if each floor has its own surface unless
  a system explicitly opts into cross-floor behavior.

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
