# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/pathfinding`.
- Active package: `P5.3b hierarchical route search and typed composition`.
- Overall status: active.

## Mandatory Completion Gate

A package is not complete until every item below is satisfied:

- [ ] Scope check: the diff contains only the package's declared responsibility.
- [ ] Invariant review: Z 0 compatibility, world/local Z frames, moving grids,
      server authority, and boundary channels were considered where applicable.
- [ ] Automated verification: focused tests pass and broader tests scale with the
      package's blast radius.
- [ ] Performance evidence: relevant counters or benchmarks are captured before
      and after the change, or the ledger explains why they do not apply.
- [ ] Documentation: decisions, limitations, test commands, and results are
      recorded in this ledger.
- [ ] Dependency check: WTZ Engine and WTZ Project revisions are paired whenever
      an engine change is required.
- [ ] Git check: `git diff --check` passes and no unrelated working-tree changes
      are silently included.
- [ ] Mini review: findings, residual risks, and the next package are summarized
      before committing.
- [ ] Commit: the package is saved as an isolated, descriptive commit and pushed
      to its WTZ feature branch.

When a package completes, copy this checklist into its history entry with the
actual evidence. Do not mark an entire phase complete from implementation alone.

## Roadmap

| Phase | Responsibility | Status |
| --- | --- | --- |
| P0 | Baselines, stress fixtures, metrics, budgets, and observability | Complete |
| P1 | Shared geometric `ZLevelTrace` primitive and boundary crossings | Complete |
| P2 | Hitscan, projectiles, throws, explosions, effects, and interactions | Complete |
| P3 | Z-aware lighting and FOV with bounded caches and budgets | Complete |
| P4 | Vertical sound propagation through cached portals | Complete |
| P5 | Hierarchical pathfinding with vertical transition edges | In progress (P5.1-P5.3a complete; P5.3b active) |
| P6 | Safe initialized-map save/load and automated round trips | Pending |
| P7 | Roofs, grates, catwalks, shafts, elevators, weather, and flight | Pending |
| P8 | Server hardening, scale tests, Z 0 regression, and porting guide | Pending |

## Phase P4 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P4.1 | Shared vertical sound portal contract, bounded cache, and metrics | Complete |
| P4.2 | Bounded multi-portal routes, transmission, and attenuation | Complete |
| P4.3a | Positional audio post-processing hook | Complete |
| P4.3b | Bounded server authorization and per-session snapshots | Complete |
| P4.3c | Client presentation, diagnostics, and hardening | Complete |

## Phase P5 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P5.1 | Navigation inventory and indexed authored traversal-edge contract | Complete |
| P5.2 | Floor-specific local polygon navigation | Complete |
| P5.3a | Detached graph snapshots, typed routes, revisions, and budgets | Complete |
| P5.3b | Hierarchical route search and typed route composition | Active |
| P5.4 | AI traversal execution, dynamic elevators, and phase hardening | Pending |

## Phase P0 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P0.1 | Process-local metrics, debug presentation, command, and ledger | Complete |
| P0.2 | Generated 3, 6, and 10-floor stress fixtures and benchmark runner | Complete |
| P0.3 | Configurable budgets, fail-soft behavior, and baseline report | Complete |

## Completed Package: P0.1 Observability Foundation

### Scope

- Add a low-cost shared metrics collector without moving subsystem policy into it.
- Instrument boundary-cache access, visibility queries, gravity-cache builds,
  and per-session Z-level PVS refreshes.
- Show local client counters in the existing Z-level debug overlay.
- Add an admin command for server-side snapshots and counter resets.
- Add deterministic tests for cache counters and reset behavior.

### Acceptance Criteria

- Existing gameplay behavior and boundary decisions remain unchanged.
- Boundary hits and misses are distinguishable and deterministic in tests.
- Gravity and PVS timings report count, average, last, and maximum duration.
- `zlevelmetrics` reports the server process and supports `zlevelmetrics reset`.
- The existing `zlevel.debug_overlay` displays compact client-local metrics.
- The focused Z-level test matrix passes.

### Evidence

- Baseline before implementation: 45 focused integration tests and 2 unit tests
  passed on `zlevel-roadmap` at `6a0e5cc8575`.
- Implementation verification: the new deterministic metrics test passed;
  the complete focused matrix passed with 46 integration tests and 2 unit tests.
- Build verification: Shared, Server, Client, and IntegrationTests compiled with
  no new warnings attributable to this package. Existing package-vulnerability
  and upstream obsolescence warnings remain.
- Performance capture: recording paths use scalar increments without per-query
  allocations. Aggregate scale measurements are deliberately assigned to P0.2.

### Decisions

- Metrics are process-local. Client presentation does not pretend to show server
  timings; the server command is authoritative for PVS and server simulation.
- The collector stores counters and timing summaries only. Lighting, sound,
  combat, pathfinding, and their caches remain owned by specialized systems.
- Counters run on the simulation main thread to avoid atomic-operation overhead
  in high-frequency visibility paths.

### Completion Gate

- [x] Scope check: only observability, tests, and roadmap documentation changed.
- [x] Invariant review: no coordinate, boundary, authority, or gameplay policy
      was changed; moving grids and Z 0 keep their existing paths.
- [x] Automated verification: 46/46 integration and 2/2 unit tests passed.
- [x] Performance evidence: hot query recording is allocation-free by design;
      aggregate measurements are explicitly deferred to P0.2 fixtures.
- [x] Documentation: command behavior, process locality, tests, and limitations
      are recorded here.
- [x] Dependency check: no WTZ Engine change is required for P0.1.
- [x] Git check: `git diff --check` passed and the branch began from a clean
      `zlevel-roadmap` checkout.
- [x] Mini review: no gameplay regression or policy coupling was found.
- [x] Commit: saved as `Add native Z-level performance observability` on
      `zlevel/baseline-metrics`; remote verification follows the commit.

### Mini Review

- Finding: metrics remain process-local, so the client overlay intentionally
  cannot display authoritative server PVS timings.
- Residual risk: the cumulative counter cost still needs measurement under a
  dense multi-floor workload.
- Next package: generate repeatable stress fixtures and a benchmark runner that
  captures these counters without relying on a human moving around the map.

## Completed Package: P0.2 Stress Fixtures And Benchmark Runner

### Scope

- Generate equivalent 3, 6, and 10-floor sparse grids from one deterministic
  fixture description instead of maintaining large copied YAML maps.
- Exercise boundary cache, visibility, gravity rebuild, and PVS candidate load.
- Capture machine-readable snapshots before and after a fixed warm-up/run cycle.
- Keep benchmark assertions structural; timing thresholds will be reported, not
  made into flaky pass/fail conditions.

### Acceptance Criteria

- One fixture builder creates all floor counts from the same topology rules.
- Fixtures include sparse station space, openings, sealed columns, gravity
  sources, candidate entities, and a translated and rotated moving grid.
- The runner executes the real server boundary, visibility, gravity, and PVS
  paths for 3, 6, and 10 floors.
- Warm-up and measured snapshots include elapsed time, managed allocations, and
  the complete P0.1 metric snapshot in versioned JSON.
- Tests assert fixture shape and workload execution without machine-dependent
  timing limits.

### Evidence

- `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj
  --no-restore -consoleloggerparameters:ErrorsOnly` passed with zero errors;
  the reported warnings are pre-existing dependency and upstream warnings.
- `powershell -NoProfile -ExecutionPolicy Bypass -File
  Tools/run_zlevel_baseline.ps1 -NoBuild` passed all three cases and produced
  exactly three schema-versioned JSON snapshots under the ignored
  `artifacts/zlevel-baseline` directory.
- The focused integration matrix passed 49/49 tests and the focused unit matrix
  passed 2/2 tests.
- `git diff --check` passed. PowerShell reported only the repository's existing
  LF-to-CRLF checkout warning for the touched shared system.

The local Debug baseline was captured on Windows 10.0.19045 x64 with .NET
10.0.4 and 28 logical processors. These values are comparison evidence for
future changes on the same environment, not release thresholds:

| Floors | Tiles | Boundary samples/iteration | Warm-up ms | Measured ms (3 iterations) | Measured allocations | Warm boundary hit rate | Measured boundary hit rate | Warm evictions |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 1,265 | 1,280 | 9.685 | 7.006 | 6,336 B | 50.2% | 100% | 0 |
| 6 | 2,527 | 3,200 | 17.092 | 12.985 | 6,336 B | 50.2% | 100% | 0 |
| 10 | 4,209 | 5,760 | 34.880 | 52.011 | 6,336 B | 50.1% | 50.1% | 2,199 |

The 10-floor workload exceeds the current 4,096-entry boundary-cache capacity.
Its measured phase therefore continues to churn instead of becoming fully hot;
this is the first concrete capacity input for P0.3.

### Decisions

- Generate fixtures in code to keep topology equivalent across floor counts and
  avoid maintaining large copied map fixtures.
- Author fixture tiles while the map is uninitialized and temporarily suspend
  2D grid splitting. This prevents the existing splitter from observing and
  separating partially authored vertical islands; normal splitting is restored
  immediately after map initialization.
- Run the actual connected-session `ZLevelPvsSystem` refresh rather than a
  synthetic candidate-count approximation.
- Expose gravity-grid invalidation as a narrow public batch-edit and diagnostic
  API so the runner can measure deterministic cold cache construction.
- Restore the test player's original coordinates and world Z after every case,
  keeping the pooled integration server reusable.
- Keep timing and allocation values out of pass/fail assertions because CI and
  developer machines have different performance characteristics.

### Completion Gate

- [x] Scope check: the diff contains only deterministic fixtures, their runner,
      the required gravity invalidation hook, tests, and documentation.
- [x] Invariant review: the fixtures cover Z 0, non-zero floors, local/world Z
      frames, moving grids, server authority, and open/closed boundaries.
- [x] Automated verification: 49/49 focused integration and 2/2 focused unit
      tests passed after a successful IntegrationTests build.
- [x] Performance evidence: all three floor counts produced warm-up and measured
      JSON snapshots; representative local values are recorded above.
- [x] Documentation: invocation, output, environment, decisions, limitations,
      and results are recorded here and in `Docs/ZLevel.md`.
- [x] Dependency check: no WTZ Engine change is required for P0.2.
- [x] Git check: `git diff --check` passed and generated artifacts are ignored.
- [x] Mini review: findings, residual risks, and the next package are recorded
      below.
- [x] Commit: saved as `Add deterministic Z-level stress baselines` on
      `zlevel/baseline-stress-fixtures`; remote verification follows the commit.

### Mini Review

- Finding: 3- and 6-floor workloads become fully hot, while 10 floors expose
  deterministic churn at the fixed boundary-cache capacity.
- Finding: measured managed allocations remain 6,336 bytes for all floor counts;
  cold construction cost grows with authored topology as expected.
- Residual risk: this is a local Debug baseline with one connected viewer. It
  does not represent Release throughput, concurrent players, or a production
  round's full entity density.
- Next package: turn current hard-coded limits into named configuration, define
  bounded fail-soft behavior, and publish a baseline report that distinguishes
  correctness limits from tunable performance budgets.

## Completed Package: P0.3 Configurable Budgets And Baseline Report

### Scope

- Inventory current Z-level cache capacities, per-frame limits, and bounded
  scans, including the 4,096-entry boundary cache exposed by P0.2.
- Move appropriate performance policy to named configuration values with safe
  defaults and documented minimums or clamps.
- Define deterministic fail-soft behavior when each budget is exhausted and add
  observability for exhaustion events.
- Re-run the P0.2 matrix before and after the policy change and publish the
  comparison without converting local timings into brittle test thresholds.

### Acceptance Criteria

- Performance policy is named and configurable without turning gameplay
  invariants into arbitrary global limits.
- Every configured value has an effective clamp and exposes the value actually
  used by the system.
- Boundary-cache pressure never changes boundary results.
- PVS exhaustion cannot hide entities based on a partial viewer evaluation.
- Metrics identify exhaustion and fail-open behavior without per-query
  allocations.
- P0.2 snapshots include their effective budgets and remain reproducible under
  local server configuration changes.

### Evidence

- `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj
  --no-restore -consoleloggerparameters:ErrorsOnly` passed with zero errors and
  538 pre-existing warnings.
- Four focused budget cases passed: boundary capacity clamping and recomputation,
  both visibility-distance clamps, and whole-refresh PVS fail-open behavior.
- The generated baseline runner passed all 3-, 6-, and 10-floor cases and wrote
  schema-version 2 snapshots with effective budget values.
- The complete focused integration matrix passed 53/53 tests; focused unit tests
  passed 2/2.
- The before/after method, environment, values, and deferred limits are recorded
  in `Docs/ZLevelBaselineReport.md`.

The final local Debug comparison showed the intended capacity effect:

| Floors | P0.2 measured ms | P0.3 measured ms | Hot hit rate before | Hot hit rate after | Warm evictions before | Warm evictions after |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 7.006 | 7.469 | 100% | 100% | 0 | 0 |
| 6 | 12.985 | 12.654 | 100% | 100% | 0 | 0 |
| 10 | 52.011 | 20.351 | 50.1% | 100% | 2,199 | 0 |

All measured cases remained at 6,336 managed bytes. The 10-floor timing improved
60.9% after eliminating deterministic cache churn; smaller timing changes are
treated as Debug-run noise.

### Decisions

- Raise the boundary-cache default from 4,096 to 8,192 entries, clamp it to
  256 through 131,072, and replicate the server value to clients.
- Preserve correctness under cache pressure by evicting oldest entries and
  recomputing them on demand. Sequence ordering survives queue compaction.
- Keep normal visibility at four world-Z levels by default, with an effective
  range of zero through 32 replicated to clients.
- Limit PVS to 16,384 Z visibility checks per session refresh by default. On
  exhaustion, clear the complete Z culling snapshot for that refresh so normal
  engine PVS fails open instead of applying a partial exclusion set.
- Record PVS checks, exhausted refreshes, and fail-open candidates in process
  metrics and expose effective values through `zlevelmetrics` and the overlay.
- Do not impose a tile cap on synchronous gravity builds. A correct cap requires
  an incremental solver and previous-cache or double-buffer behavior.
- Do not classify structural collapse throughput or per-entity step-down depth
  as part of this shared cache and visibility package.

### Completion Gate

- [x] Scope check: the diff contains only configuration, bounded cache and PVS
      policy, metrics/presentation, focused tests, baselines, and documentation.
- [x] Invariant review: Z 0 and moving-grid coordinate behavior are unchanged;
      boundary channels share the same cache; authority remains server-side;
      replicated values keep client visibility policy aligned.
- [x] Automated verification: build, 4 budget cases, 3 baseline cases, 53/53
      focused integration tests, and 2/2 focused unit tests passed.
- [x] Performance evidence: schema-version 2 captures and the P0.2/P0.3
      comparison are recorded in `Docs/ZLevelBaselineReport.md`.
- [x] Documentation: CVar names, clamps, failure policy, operations guidance,
      limitations, commands, and results are recorded.
- [x] Dependency check: no WTZ Engine change is required for P0.3.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, generated artifacts are ignored, and the diff is package-scoped.
- [x] Mini review: findings, residual risks, and P1.1 are recorded below.
- [x] Commit: saved as `Add configurable Z-level performance budgets` on
      `zlevel/baseline-budgets`; remote verification follows the commit.

### Mini Review

- Finding: the P0.2 10-floor slowdown was boundary-cache capacity churn rather
  than growth in measured managed allocations.
- Finding: session-wide PVS fail-open gives a simple correctness guarantee when
  the visibility-check budget is exhausted.
- Residual risk: PVS candidate collection itself remains unbounded because the
  engine lookup API is not paged or resumable.
- Residual risk: cold gravity construction remains a synchronous whole-grid
  operation; P0 metrics can now identify its cost but cannot safely interrupt it.
- Next package: define the immutable `ZLevelTrace` request/result contract and
  lock down same-level 2D reference behavior before adding vertical crossings.

## Phase P1 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P1.1 | Trace request/result contract, channels, and 2D reference behavior | Complete |
| P1.2 | Ordered vertical crossings and boundary-channel integration | Complete |
| P1.3a | Current-frame normalization and explicit structural ownership | Complete |
| P1.3b1 | Reusable buffers and bounded entity-hit output | Complete |
| P1.3b2 | Trace metrics, allocation evidence, and reproducible benchmark | Complete |

## Completed Package: P1.1 ZLevelTrace Contract And Reference Behavior

### Scope

- Define request, segment, tile visit, entity hit, and boundary-crossing value
  types without coupling specialized consumer policy to the primitive.
- Represent local and world Z explicitly and require an owning map/grid frame.
- Define trace channels for projectile, explosion, visibility, interaction,
  sound, and effects queries.
- Preserve the existing engine 2D ray behavior when origin and destination are
  on the same world Z.
- Add deterministic reference tests before implementing vertical crossing logic.

### Acceptance Criteria

- Endpoints capture map XY/world Z and optional grid-local XY/local Z without
  inferring a layer from overlapping 2D geometry.
- The request contains boundary semantics and output selection but no damage,
  attenuation, penetration, or target-selection policy.
- Results expose immutable ordered segments, tile visits, entity hits, and
  boundary crossings with cumulative distances.
- Same-world-Z entity hits reuse the engine physics query and ignore colliders
  whose effective world Z differs.
- Ordinary Z 0 hit order and distance remain compatible with the engine path.
- A vertical request cannot silently execute as a same-floor ray before P1.2.

### Evidence

- `Content.Shared` and `Content.IntegrationTests` built with zero errors. The
  reported warnings are the existing dependency, generator, and upstream set.
- Four trace reference cases passed: translated/rotated frame coordinates and
  world-Z filtering, Z 0 physics parity, explicit vertical deferral, and perfect
  diagonal tile order.
- The complete focused integration matrix passed 57/57 tests, including the P0
  stress baselines and the existing mapping, atmosphere, movement, PVS, and
  boundary suites.
- Focused unit tests passed 2/2.
- Projectile and explosion channel independence is exercised through the real
  boundary resolver, including bits above the former byte range.

### Decisions

- Keep the primitive in WTZ Project `Content.Shared`; P1.1 needs no additional
  WTZ Engine API because `SharedPhysicsSystem.IntersectRay` already supplies the
  authoritative 2D entity geometry.
- Represent one endpoint as a world coordinate plus an optional captured grid
  frame and local coordinate. Grid points are created through
  `TryCreateGridPoint`; map-only points use `ZLevelTracePoint.FromMap`.
- Widen `ZLevelBoundaryChannels` from byte to unsigned 16-bit storage and add
  independent `Projectile` and `Explosion` bits rather than aliasing `Effects`.
- Use immutable result arrays while semantics are still evolving. No gameplay
  hot path is migrated until the vertical path and caller-owned P1.3 buffer are
  complete.
- Sort entity hits by distance and then entity UID, making equal-distance ties
  deterministic while preserving normal engine ordering.
- Treat exact 2D corner crossings as one diagonal tile transition, matching the
  existing pathfinding grid-cast convention.

### Completion Gate

- [x] Scope check: the diff contains only the trace contract/reference path,
      two boundary channel bits, tests, and documentation.
- [x] Invariant review: explicit local/world coordinates cover moving frame
      origins; Z 0 physics parity is tested; shared code runs on client/server;
      server authority and existing boundary behavior are unchanged.
- [x] Automated verification: build, 4/4 trace references, 57/57 focused
      integration tests, and 2/2 focused unit tests passed.
- [x] Performance evidence: no production consumer uses P1.1, so throughput
      claims do not apply. P0 baselines still pass; immutable allocation is
      documented and assigned to P1.3 before consumer migration.
- [x] Documentation: coordinate invariants, channels, ordering, options,
      limitations, and verification are recorded in `Docs/ZLevelTrace.md`.
- [x] Dependency check: no WTZ Engine change is required for P1.1.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices and the diff contains no generated benchmark artifacts.
- [x] Mini review: findings, residual risks, and P1.2 are recorded below.
- [x] Commit: saved as `Define the shared Z-level trace contract` on
      `zlevel/trace-contract`; remote verification follows the commit.

### Mini Review

- Finding: the existing physics ray can remain authoritative when its candidates
  are filtered by effective world Z; a parallel physical broadphase is not
  needed for the shared trace.
- Finding: endpoint frames and entity frames must remain independent. Tests use
  explicit world-Z stamping because anchored fixtures may reparent when no grid
  tile supports them.
- Residual risk: tile visits are emitted only when both endpoints share one grid
  frame and local Z. Cross-frame normalization remains P1.3 work.
- Residual risk: immutable arrays and the engine query allocate; P1.1 is not yet
  suitable for a migrated high-frequency consumer.
- Next package: implement ordered world-Z crossings, resolve the requested
  boundary channels at each crossing, and merge per-level segments and hits by
  cumulative distance.

## Completed Package: P1.2 Ordered Vertical Crossings And Boundary Integration

### Scope

- Traverse one continuous world XYZ line in deterministic order and split it at
  every adjacent half-level boundary.
- Resolve crossings in a translated or rotated grid frame and query the shared
  boundary resolver with the request's complete channel mask.
- Stop coherently at the first closed boundary without evaluating later tiles,
  fixtures, or crossings.
- Merge per-floor segments, tile visits, entity hits, and crossings by
  cumulative 3D distance in both upward and downward directions.
- Bound crossing and tile work with replicated server CVars and explicit
  `IterationBudgetExceeded` results.

### Acceptance Criteria

- One Z level contributes one unit to Euclidean trace distance, and crossings
  occur at half-level planes.
- Vertical crossings use explicit local and world Z values and never infer a
  floor from overlapping XY geometry.
- Every requested boundary bit must be open; the returned closed crossing is the
  final evaluated boundary.
- Same-floor fixture queries remain filtered by effective world Z, including
  segments with zero XY extent.
- Output order is deterministic for upward, downward, diagonal, and perfectly
  vertical traces.
- Crossing-budget rejection is a preflight empty result, while a tile-budget
  overflow rolls back the complete overflowing segment.

### Evidence

- `Content.Shared`, `Content.Client`, and `Content.IntegrationTests` built with
  zero errors. Their 99, 316, and 443 reported warnings are the existing
  dependency, generator, analyzer, and upstream warning set.
- The five trace reference tests passed, including Z 0 parity, rotated frames,
  vertical fixture hits, closed channels, and unresolved cross-frame requests.
- The combined trace and budget matrix passed 11/11 tests; the complete focused
  Z-level integration matrix passed 60/60 tests.
- Focused structural unit tests passed 2/2, and all 3-, 6-, and 10-floor P0
  baseline cases passed 3/3 after the implementation.
- `git diff --check` passed with only the checkout's LF-to-CRLF notices.

### Decisions

- Model discrete floor centers one world unit apart and split a monotonic line at
  each `Z + 0.5` plane. This gives every output collection one shared distance
  parameter without introducing continuous entity Z extents.
- Execute vertical traces only when both captured endpoints share a valid grid
  frame in P1.2. Return `FrameResolutionRequired` for map-only or cross-frame
  vertical requests instead of selecting an overlapping grid implicitly.
- Keep the existing engine 2D ray authoritative for segments with horizontal
  extent. Filter every candidate by effective world Z and globally sort hits by
  cumulative distance, entity UID, and segment sequence.
- Resolve perfectly vertical collision through the existing exact entity lookup
  plus point-in-fixture validation for hard fixtures matching the collision
  mask. Record those hits at the segment entry distance.
- Clamp crossings to 1 through 1,024 and tile visits to 1 through 1,000,000.
  Replicate both server values and expose their effective values in
  `zlevelmetrics` and the debug overlay.
- Preserve immutable result snapshots until P1.3 establishes stable
  caller-owned buffer and reuse semantics.

### Completion Gate

- [x] Scope check: the diff contains only vertical trace geometry, its two
      budgets and presentation, focused tests, and trace documentation.
- [x] Invariant review: Z 0 parity, local/world Z, rotated frames, upward and
      downward order, server-configured budgets, and channel masks are covered.
- [x] Automated verification: Shared, Client, and IntegrationTests builds,
      5/5 trace, 11/11 trace plus budget, 60/60 focused integration, 2/2 unit,
      and 3/3 baseline cases passed.
- [x] Performance evidence: no production system invokes the trace yet, so a
      throughput comparison would be synthetic. Existing P0 baselines pass;
      query allocations and a dedicated trace benchmark are assigned to P1.3.
- [x] Documentation: geometry, ordering, partial results, budgets, commands,
      limitations, and verification are recorded in `Docs/ZLevelTrace.md`.
- [x] Dependency check: P1.2 uses existing Robust physics and lookup APIs and
      requires no WTZ Engine revision change.
- [x] Git check: `git diff --check` passes apart from line-ending notices, and
      generated baseline artifacts remain ignored.
- [x] Mini review: findings, residual risks, and P1.3 are recorded below.
- [x] Commit: save as `Implement ordered vertical Z-level traces` on
      `zlevel/trace-vertical-crossings`; remote verification follows the commit.

### Mini Review

- Finding: one half-level split algorithm covers diagonal, vertical, upward, and
  downward requests without consumer-specific geometry.
- Finding: boundary-first truncation prevents a closed roof or deck from leaking
  entity and tile information from later floors.
- Finding: the exact point path is needed because a 2D ray has no direction for
  a perfectly vertical XYZ trace.
- Residual risk: captured endpoint world coordinates can become stale if a grid
  moves between point creation and `Trace`; P1.3 must normalize or reject this
  deterministically.
- Residual risk: immutable arrays, temporary candidate collections, and physics
  enumerables allocate. Entity hits also lack an independent output budget.
- Next package: normalize map and moving-grid frames, define stale-snapshot
  behavior, add reusable buffers and bounded hit output, and instrument a
  dedicated deterministic trace workload before migrating gameplay consumers.

## Completed Package: P1.3a Current-Frame Normalization And Explicit Ownership

### Scope

- Re-resolve every grid-relative endpoint from its local XY and local Z against
  the frame's current translation, rotation, map, and world-Z origin.
- Keep map-only endpoints world-authoritative and project them only when a caller
  explicitly supplies a structural frame.
- Use a common endpoint grid automatically, but require `BoundaryFrameUid` for
  vertical map-only, cross-grid, or overlapping-grid requests.
- Verify that shared code produces the same ordered geometry on client and
  server from replicated frame state.

### Acceptance Criteria

- Moving or changing the Z origin of a grid after point creation cannot mix
  stale world geometry with current local tiles.
- No grid is selected from 2D overlap; ambiguous vertical requests fail with
  `FrameResolutionRequired` unless ownership is explicit.
- One explicit frame can normalize map-only and cross-grid endpoints and owns all
  tile visits and boundary crossings for that request.
- Physics remains map-wide and filters entities by effective world Z rather than
  being restricted to the selected structural frame.
- Server and client agree on termination, positions, distances, tile order,
  crossing order, and resolved channel state.

### Evidence

- `Content.Shared` and `Content.IntegrationTests` built with zero errors; reported
  warnings are the existing analyzer, dependency, and upstream warning set.
- Seven trace tests passed, including current-frame movement, explicit
  cross-grid/map projection, overlap non-inference, and client/server parity.
- The complete focused integration matrix passed 62/62 tests and the focused
  structural unit matrix passed 2/2 tests.
- The supported moving-grid collider test preserves grid parentage with authored
  turf, follows the frame from world Z 5 to 7, and remains visible to the trace.
- `git diff --check` passed with only checkout line-ending notices.

### Decisions

- Treat local coordinates as authoritative for grid points. Their stored world
  coordinate is a creation-time snapshot; `Trace` refreshes it before deciding
  whether the request is horizontal or vertical.
- Treat world coordinates as authoritative for map points. Projection into a
  grid occurs only through an explicit `BoundaryFrameUid`.
- Let one common or explicit frame own structural output. Do not infer or merge
  several overlapping structures until multi-frame traversal has an explicit
  result contract.
- Apply the structural frame to tiles and boundaries only. Entity raycasts still
  query the whole map and use effective world Z as the isolation invariant.
- Add the optional request field at the end of the positional contract so
  existing callers retain source compatibility.

### Completion Gate

- [x] Scope check: the diff contains only endpoint normalization, explicit frame
      selection, its tests, and contract documentation.
- [x] Invariant review: moving XY transforms, frame-origin changes, map points,
      cross-grid and overlapping endpoints, Z 0, and client/server parity are
      covered without changing server authority.
- [x] Automated verification: build, 7/7 trace references, 62/62 focused
      integration tests, and 2/2 focused unit tests passed.
- [x] Performance evidence: normalization adds a fixed number of transform
      operations and no new result collection. Allocation measurement and the
      hot caller-owned path are the declared scope of P1.3b.
- [x] Documentation: coordinate authority, explicit ownership, multi-frame
      limits, and verification are recorded in `Docs/ZLevelTrace.md`.
- [x] Dependency check: existing Robust transform APIs are sufficient; no WTZ
      Engine revision change is required.
- [x] Git check: `git diff --check` passes apart from line-ending notices and no
      generated artifacts are present.
- [x] Mini review: findings, residual risks, and P1.3b are recorded below.
- [x] Commit: save as `Normalize Z-level traces across moving frames` on
      `zlevel/trace-frame-normalization`; remote verification follows the commit.

### Mini Review

- Finding: refreshing grid points before classifying the request also handles a
  frame-origin change that turns a formerly same-level endpoint into another
  world Z.
- Finding: explicit structural ownership makes overlapping-grid behavior
  deterministic without pretending that first-found map lookup is meaningful.
- Finding: moving-grid physics remains coherent when the fixture has real grid
  support; the earlier unsupported fixture was reparented rather than lost from
  the broadphase.
- Residual risk: one request cannot yet compose closed boundaries from several
  structures along the same world line.
- Residual risk: immutable output and physics candidate collections still
  allocate, and entity hits remain independently unbounded.
- Next package: add caller-owned buffers, a hit budget, trace counters and
  timings, and a reproducible trace-specific allocation/performance workload.

## Completed Package: P1.3b1 Reusable Buffers And Bounded Hit Output

### Scope

- Add caller-owned reusable output and scratch storage while preserving the
  immutable convenience API for cold callers.
- Bound entity-hit output with a replicated server CVar and expose the effective
  budget through the existing admin command and client debug overlay.
- Roll back every output collection to the segment bookmark when either tile or
  entity-hit work exceeds its budget.

### Acceptance Criteria

- Reusing one buffer replaces its logical contents without replacing its public
  list views or shrinking reserved capacity.
- Buffered and immutable calls produce equivalent ordered results.
- Entity-hit overflow returns `IterationBudgetExceeded` without exposing any
  part of the overflowing segment.
- Invalid inputs and preflight failures clear a previously used buffer.
- Existing callers of the immutable overload retain source and result-lifetime
  compatibility.

### Evidence

- `Content.IntegrationTests` builds with zero errors; warnings are the existing
  analyzer, dependency, and upstream warning set.
- The new buffer-reuse/equivalence and entity-hit rollback tests pass together.
- Eight trace tests, seven budget tests, the complete 64-test Z-level integration
  matrix, two structural unit tests, and three stress baselines pass.

### Decisions

- Keep the immutable overload as a cold-path convenience wrapper over the same
  buffered implementation so behavior cannot diverge.
- Make buffers caller-owned and single-invocation views: each call clears
  counts, retains capacities, and invalidates the previous logical result.
- Keep segment assembly atomic with one bookmark spanning segments, tile visits,
  entity hits, and boundary crossings.
- Treat the existing crossing, tile, and hit limits together as the aggregate
  output bound; no fourth total-count CVar is needed while every collection has
  an independent finite ceiling.
- Clamp the hit budget to at least one, matching the established trace-budget
  fail-soft policy.

### Completion Gate

- [x] Scope check: the diff is limited to trace storage, hit bounding, tests,
      observability labels, and contract/ledger documentation.
- [x] Invariant review: frame normalization, world-Z hit filtering, Z 0,
      boundary ordering, and server-owned replicated budgets are unchanged.
- [x] Automated verification: build, 8/8 trace tests, 7/7 budget tests, 64/64
      focused integration tests, 2/2 focused unit tests, and 3/3 stress
      baselines passed.
- [x] Performance evidence: this package makes WTZ-owned collections reusable
      and tests capacity retention; quantified allocations and timings are
      intentionally the next P1.3b2 package, so no unmeasured zero-allocation
      claim is made here.
- [x] Documentation: ownership, lifetime, budgets, rollback, limitations, and
      verification commands are recorded in `Docs/ZLevelTrace.md`.
- [x] Dependency check: existing Robust collection, lookup, and physics APIs are
      sufficient; no WTZ Engine revision change is required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices; the tree contains only the ten intended source, test, UI, and
      documentation files and no generated artifacts.
- [x] Mini review: findings, residual risks, and P1.3b2 are recorded below.
- [x] Commit: save as `Add reusable Z-level trace buffers` on
      `zlevel/trace-allocation-hardening`; remote verification follows the
      commit.

### Mini Review

- Finding: one buffered core now defines both hot and immutable behavior,
  eliminating duplicate trace implementations.
- Finding: segment bookmarks make failure atomic across all ordered outputs,
  including a physics ray that discovers more entities than allowed.
- Residual risk: engine-owned ray enumeration may still allocate even when WTZ
  output storage is warm.
- Residual risk: no gameplay consumer uses the buffered path yet; migration
  begins only after P1 closes.
- Next package: instrument trace counts, terminations, sizes, and timings, then
  capture repeatable allocation/performance workloads.

## Completed Package: P1.3b2 Trace Metrics And Benchmark

### Scope

- Instrument every public trace with process-local query, termination, output,
  and elapsed-core-time counters.
- Expose trace metrics through the existing server command and client debug
  overlay, including reset through `zlevelmetrics reset`.
- Add reproducible same-level, diagonal multi-floor, closed-boundary, and
  budget-exhaustion workloads for immutable and caller-buffered calls.
- Lock down equal-distance entity ordering and finite endpoints whose derived
  trace distance overflows.

### Acceptance Criteria

- Each public immutable or buffered request records exactly one trace sample.
- All six termination values and all four output collections are distinguishable
  in a snapshot and return to zero on reset.
- Trace timing does not allocate on a warmed buffered tile-only path.
- The benchmark writes versioned machine-readable metadata, budgets, structural
  results, timings, allocations, and matching metric totals.
- Timing remains comparison evidence rather than a hardware-dependent pass/fail
  threshold.

### Evidence

- `Content.IntegrationTests` builds with zero errors; the 12 reported warnings
  are the existing dependency and package-vulnerability warning set.
- Trace and budget tests pass 17/17; metrics and trace benchmark tests pass 3/3;
  the complete Z-level integration matrix passes 68/68.
- The focused structural unit matrix passes 2/2 and all three stress baselines
  pass with schema version 3 snapshots containing the new trace fields.
- The standalone benchmark runner passes 1/1 and writes schema version 1 JSON
  for all four workloads.
- Across 512 measured calls per workload, the warmed buffered path allocates
  zero managed bytes; immutable calls allocate between 1,480 and 6,896 bytes
  per request in the first capture. No timing threshold is asserted.

### Decisions

- Measure only the shared buffered core in `TraceMilliseconds`; immutable-array
  construction remains visible in the benchmark's outer elapsed and allocation
  totals instead of being charged inconsistently to one overload.
- Record metrics around one private core so immutable and buffered APIs cannot
  double-count or drift semantically.
- Reject a non-finite derived distance during preflight even when both endpoint
  components are individually finite; a coincident extreme point remains valid.
- Keep deterministic hit ordering as distance, entity UID, then segment, and
  cover equal-distance entities explicitly.
- Make zero allocation a tested invariant only for warmed tile-only workloads.
  Robust physics enumeration remains outside that claim until a collision-enabled
  gameplay consumer is profiled.

### Completion Gate

- [x] Scope check: the diff contains only trace instrumentation, benchmark and
      edge-case coverage, observability presentation, and related documentation.
- [x] Invariant review: Z 0 behavior, local/world frame normalization, moving
      grids, server-owned budgets, channel boundaries, deterministic ordering,
      and atomic rollback remain covered by the cumulative matrix.
- [x] Automated verification: build, 17/17 trace plus budget, 3/3 metrics plus
      benchmark, 68/68 Z-level integration, 2/2 unit, and 3/3 baseline tests pass.
- [x] Performance evidence: schema version 1 benchmark JSON records four warmed
      workloads and enforces zero buffered tile-only allocation without brittle
      timing assertions; results are summarized in
      `Docs/ZLevelTraceBenchmarkReport.md`.
- [x] Documentation: metric semantics, overflow handling, benchmark method,
      first results, limitations, and commands are recorded in the trace docs.
- [x] Dependency check: existing Robust timing, transforms, lookup, and physics
      APIs are sufficient; no paired WTZ Engine revision is required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and generated baseline/benchmark artifacts remain ignored.
- [x] Mini review: findings, residual risks, and P2.1 are recorded below.
- [x] Commit: save as `Instrument and benchmark Z-level traces` on
      `zlevel/trace-metrics-benchmark`; remote verification follows the commit.

### Mini Review

- Finding: the caller-buffered geometric and boundary path now has measured,
  repeatable evidence for zero WTZ-managed allocation after warm-up.
- Finding: output totals and termination counters make future consumer cost and
  failure modes observable without coupling policy to the trace primitive.
- Finding: P1 now has one tested contract for geometry, frame ownership,
  boundaries, output lifetime, budgets, deterministic ordering, and metrics.
- Residual risk: entity-hit traces can still allocate inside Robust physics and
  need consumer-specific profiling with realistic masks and candidate counts.
- Residual risk: metrics are process-local; the client overlay does not represent
  authoritative server traces, while `zlevelmetrics` does not include client work.
- Residual risk: no gameplay path consumes `ZLevelTrace` yet, so P1 completion
  establishes infrastructure rather than vertical combat behavior.
- Next package: migrate the narrowest authoritative hitscan path with Z 0 parity,
  closed-floor rejection, reusable buffers, and focused projectile-channel tests.

## Phase P2 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.1 | Authoritative hitscan migration and Z 0 parity | Complete |
| P2.2 | Physical projectiles and thrown-entity traversal | Complete |
| P2.3 | Explosions, fire, heat, and generated effects | Complete |
| P2.4 | Central direct and remote interaction validation | Complete |

P2.4 is split into independently gated subpackages:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.4a | Central spatial origin, same-floor authority, and opt-in trace primitive | Complete |
| P2.4b | Interaction metrics and authored vertical portals | Complete |
| P2.4c | Verb, UI, action, drag/drop, do-after, and remote-view request audit | Complete |
| P2.4d1 | Pointer coordinate-layer transport, frame ownership, and server authority | Complete |
| P2.4d2 | Cross-floor entity targeting, click priority, menus, and segmented obstruction | Complete |
| P2.4d3a | Explicit lower-floor coordinate opt-in, visibility, frame, and range authority | Complete |
| P2.4d3b | Coordinate aiming for guns, hitscan, projectiles, action guns, and projectile spells | Complete |
| P2.4d3c | Forged/stale request hardening and final P2 automated/manual review | Complete |

### P2.4 Contracts

- Physical use, pickup, pull, drag/drop, tool use, activation, and alt-click are
  same-world-Z by default.
- A server-owned `EyeComponent.Target` is the spatial origin for a world
  interaction, while self, held-item, equipment, and same-container operations
  remain local to the actor.
- Cross-floor use is opt-in and must complete a `ZLevelTrace` through the
  `Interaction` boundary channel; bypassing range does not bypass floor policy.
- Examine and authenticated administrative inspection remain separate targeting
  capabilities and do not weaken gameplay interaction authority.
- Low-level coordinate and physics helpers remain 2D primitives. Entity-facing
  interaction entry points own the floor policy so specialized consumers can
  state deliberate exceptions instead of inheriting one implicitly.
- Robust input transports one optional opaque coordinate layer and has no
  knowledge of Z-level policy. WTZ Content interprets that layer as world Z and
  revalidates it against server-owned target and spatial-origin state.

## Completed Package: P2.4a Central Interaction Authority

### Scope

- Add one shared entity-facing authority for physical same-floor checks,
  effective world-interaction origins, and explicit vertical interaction traces.
- Resolve a server-owned remote eye as both the world-Z and XY origin for world
  targets while keeping self, held/equipped, parent/child, and same-container
  operations local to the actor.
- Enforce the default same-world-Z policy at `UserInteraction`, hand use, tool
  use, ranged-use callbacks, low-priority after-interact callbacks, activation,
  alt-click, entity range checks, BUI message attempts, in-hand use, and pull.
- Keep pulling and held-item ownership physical, independent of remote-eye
  redirection.
- Provide an explicit `Interaction`-channel trace policy without opting any
  gameplay consumer into vertical use yet.

### Acceptance Criteria

- Calling any covered public interaction entry point directly cannot emit its
  gameplay or contact event against a target on another world Z.
- A remote eye ten tiles from its body can interact with a target at the eye,
  cannot interact with an overlapping target on the body's floor, and does not
  break activation of an item in the actor's container.
- Physical pull/use comparisons ignore remote-eye redirection.
- Explicit vertical permission rejects closed boundaries and openings for other
  channels, accepts only the `Interaction` channel, honors explicit closes and
  combined XYZ range, and works through a translated/rotated frame whose local
  layer one is world Z six.
- Existing Z 0 range, obstruction, container, and pulling behavior remains
  unchanged.

### Explicit Deferrals

- P2.4b owns counters for accepted/rejected interaction policies and the first
  deliberate consumers of `CanInteractThroughOpenBoundary`.
- P2.4c owns execution-time audits for general verbs, UI requests, target
  actions, drag/drop, do-after, relays, and other remote-view request funnels.
- P2.4d owns client-targeting polish, the final interaction regression matrix,
  manual gameplay validation, and the P2 completion review.

### Completion Gate

- [x] Scope check: the diff is limited to shared interaction authority, covered
      core call sites, pulling, focused tests, and Z-level documentation.
- [x] Invariant review: Z 0, local/world frames, a translated/rotated frame with
      origin five, remote eyes, containers, physical pulling, server authority,
      and independent boundary channels were reviewed.
- [x] Automated verification: 4/4 dedicated authority cases, 12/12 interaction,
      range, and pulling regressions, and 135/135 focused Z-level integration
      tests passed without skips; the complete solution compiled with zero
      errors.
- [x] Performance evidence: the default same-floor path performs bounded entity,
      transform, container, and integer-Z queries, allocates no result
      collections, and returns before tracing. Explicit vertical checks reuse one
      retained trace buffer. The code is event-driven and adds no per-tick work;
      policy counters and load profiling are assigned to P2.4b and P8.
- [x] Documentation: policy, public APIs, exceptions, tests, decisions,
      limitations, and the next package are recorded here and in
      `Docs/ZLevelTrace.md`.
- [x] Dependency check: `RobustToolbox` remains clean at
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea`; no WTZ Engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings and residual risks are recorded below.
- [x] Commit: package prepared as the isolated `Centralize native Z-level
      interaction authority` commit on `zlevel/interaction-authority`; remote
      verification follows the package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed with
  zero errors. Its 711 warnings are existing dependency, vulnerability,
  analyzer, and upstream obsolescence warnings.
- The dedicated cases cover direct public APIs, directed before/after callbacks,
  remote-eye XY and world-Z authority, actor-local container items, physical
  checks, and channel-specific explicit vertical traces.
- The final focused Z-level matrix passed 135/135 with no skips. The 12/12
  regression matrix covers the complete click interaction fixture, legacy 2D
  obstruction/range behavior, pulling, and the new authority fixture.
- `git diff --check` passed with only normal LF-to-CRLF checkout notices.

### Decisions

- Keep `ZLevelTrace` geometric. `SharedZLevelInteractionSystem` owns entity and
  gameplay-origin policy, while each consumer still owns access, obstruction,
  range, and effects.
- Treat only a server-owned `EyeComponent.Target` as a remote spatial origin.
  Existing Station AI range/access overrides remain authoritative after the
  floor guard; a client cannot nominate its own origin.
- Keep low-level `MapCoordinates` helpers planar. They cannot infer a target
  floor, so every entity-facing entry point must enforce vertical policy before
  calling them.
- Require explicit opt-in for cross-floor interaction. Bypassing vanilla range
  for admin or mapping convenience never bypasses the floor policy.

### Mini Review

- Finding: the previous guard covered the high-level click funnel but direct
  calls to hand use, tool use, activation, alt-click, and before/after callbacks
  could still bypass it. All covered public entry points now defend themselves.
- Finding: comparing the actor body to the target incorrectly rejected Station
  AI interactions viewed from another floor. The server-owned eye now supplies
  one coherent XY and world-Z origin.
- Finding: the first remote-eye implementation selected the correct floor but
  left ordinary raycasts at the body. A ten-tile separation test exposed this,
  and range/obstruction now start at the same effective origin.
- Finding: directed before/after events needed entity-bound test listeners;
  correcting the fixture now proves that rejected calls emit no callback rather
  than merely returning an unhandled result.
- Residual risk: general verb execution and several specialized request funnels
  can define their own access semantics. P2.4c must validate them at execution
  time without breaking authenticated admin operations or Station AI.
- Residual risk: the opt-in vertical trace primitive has no gameplay consumer or
  dedicated policy metrics yet. P2.4b owns both so accidental cross-floor use is
  visible and reviewable.
- Next package: instrument interaction-policy outcomes, remove duplicate guard
  work in nested range overloads, and migrate only deliberate vertical consumers
  backed by `Interaction`-channel tests.

## Completed Package: P2.4b Interaction Metrics And Authored Portals

### Scope

- Classify every entity-facing authority result as same-level or vertical
  allowed, or invalid-context, map, range, level, frame, or trace rejected.
- Count server-owned remote origins and physical same-floor checks separately,
  expose the snapshot through `zlevelmetrics`, and add a compact client debug
  overlay line.
- Require every explicit vertical authorization to provide a positive finite
  maximum range; remove the ambiguous public policy overload and its unbounded
  default.
- Remove duplicate authority work from nested entity-range overloads while
  preserving the guard before Station AI and other range overrides.
- Author the independent `Interaction` channel on stairs and ladders, and verify
  the existing mapping policy for openings, shafts, grates, and sealed limits.

### Acceptance Criteria

- `InteractionAllowed + InteractionRejected == InteractionQueries`, and each
  tested decision increments exactly one terminal category.
- Physical checks do not redirect through a remote eye and are not mixed into
  entity policy-query totals.
- Invalid entities, different maps, out-of-range/non-finite requests, different
  world levels, different frames, and rejected traces are distinguishable.
- Openings, shafts, stairs, and ladders authorize a one-level bounded trace;
  grates and sealed boundaries reject the same request.
- The same-level warmed authority path performs no trace and allocates zero
  managed bytes across 4,096 measured checks.
- Normal use targeting remains same-floor-only; authoring a portal does not by
  itself expose lower-floor gameplay targets.

### Explicit Deferrals

- P2.4c owns execution-time authority for verbs, BUI requests, target actions,
  drag/drop, do-after, relays, and specialized remote request funnels.
- P2.4d owns client selection through authored portals, segmented obstruction,
  click priority, manual gameplay validation, and P2 completion review.
- P8 owns high-player-count interaction-rate profiling. Counters intentionally
  measure authority API checks rather than attempting to infer unique clicks.

### Completion Gate

- [x] Scope check: the diff is limited to interaction metrics, authority API
      hardening, duplicate range work, authored portal channels, tests, and
      documentation.
- [x] Invariant review: Z 0, remote eyes, physical ownership, transformed world
      frames, different maps/grids, boundary-channel independence, finite range,
      server authority, and client targeting separation were reviewed.
- [x] Automated verification: 13/13 authority and metrics cases, 21/21 focused
      interaction/range/pulling regressions, and 144/144 focused Z-level
      integration tests passed with no skips; the complete solution compiled
      with zero errors.
- [x] Performance evidence: 4,096 warmed same-level checks allocated zero bytes,
      emitted 4,096 same-level decisions, and performed zero traces. Metrics are
      scalar main-thread increments; nested entity-range validation no longer
      records the same guard twice.
- [x] Documentation: bounded API semantics, metric interpretation, portal
      policy, current client limitation, tests, and residual risks are recorded
      here and in `Docs/ZLevel.md` and `Docs/ZLevelTrace.md`.
- [x] Dependency check: `RobustToolbox` remains clean at
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea`; no WTZ Engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings and residual risks are recorded below.
- [x] Commit: package prepared as the isolated `Instrument native Z-level
      interaction policy` commit on `zlevel/interaction-metrics`; remote
      verification follows the package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed with
  zero errors. Its 711 warnings are existing dependency, vulnerability,
  analyzer, and upstream obsolescence warnings.
- The 13/13 package matrix covers every metric category, reset behavior, remote
  and physical counts, strict finite range, seven real boundary prototypes, and
  allocation behavior.
- The final interaction matrix passed 21/21 and includes legacy planar range,
  obstruction, containers, pulling, all core interaction entry points, remote
  XY origins, and explicit portal authority.
- The complete focused Z-level integration matrix passed 144/144 with no skips.

### Decisions

- Count authority checks rather than player inputs. Public entry points remain
  independently defended, so one click may legitimately produce several checks.
- Keep rejection taxonomy in the shared authority and expose only aggregate
  snapshot fields. Consumers do not record policy decisions manually.
- Require explicit finite range before checking same-level or vertical success
  through the opt-in API, preventing accidental unlimited shaft interaction.
- Author portal capability now, but do not switch normal client use to it until
  segmented obstruction and lower-floor targeting can be validated together.
- Treat grates as visible/permeable but not directly usable through. Their
  `Interaction` channel remains closed unless a later content design explicitly
  changes that rule.

### Mini Review

- Finding: the initial structure-only inventory missed mapper boundary markers.
  The expanded audit proved openings and shafts already authored interaction,
  while grates and sealed markers intentionally did not.
- Finding: the explicit API's zero-range default meant unbounded reach. Removing
  the policy overload and requiring a positive finite range makes every future
  consumer state its reach budget.
- Finding: the first different-frame test was rejected by XY range before frame
  policy. Increasing only that fixture's range now proves the intended ordered
  classification without weakening production limits.
- Finding: the shallow entity-range overload validated authority and then called
  a public overload that validated it again. A private core preserves overrides
  and removes duplicate metrics and query work.
- Residual risk: trace rejection aggregates closed boundaries, trace budgets,
  and invalid trace completion; the adjacent trace metrics retain the detailed
  termination reason.
- Residual risk: authored interaction portals are intentionally dormant for
  normal clicks. P2.4d must add segmented obstruction and client selection in
  the same package before players can use entities through them.
- Next package: audit and enforce execution-time authority across verbs, BUI,
  target actions, drag/drop, do-after, relay entities, and Station AI request
  paths without weakening authenticated admin capabilities.

## Completed Package: P2.4c Native Interaction Request Funnels

### Scope

- Revalidate every physical verb family at execution time after the server
  reconstructs the requested verb, including the generic base `Verb` used by
  ordinary gameplay systems.
- Preserve remote execution for examine, VV, explicit forced commands, and
  generic administrative categories only when the executing entity belongs to
  an authenticated active administrator.
- Make rejected entity/world action targets terminal, enforce same-world-Z for
  entity targets independently of planar access flags, and reject missing
  entities or invalid/non-finite coordinates before rotation or effects.
- Prove the existing server guards for BUI attempts, drag/drop against both
  entities, finite-range targeted DoAfters, and interaction relays.
- Prevent Station AI's optimized BUI range override from crossing floors and
  preserve the old remote eye's world Z whenever its proxy mode is replaced.
- Repair the native targeted-DoAfter fixture so it exercises a real spatial
  context instead of relying on interaction inside `Nullspace`.

### Acceptance Criteria

- A stale or malicious verb request cannot execute any gameplay verb against an
  entity on another world Z, while the same verb families retain same-floor
  behavior and authenticated administration remains remote-capable.
- Disabling `CheckCanAccess` cannot opt an entity-targeted action into another
  floor, and every rejected or malformed target sets `ActionValidateEvent.Invalid`.
- A real client-to-server drag/drop request emits neither dragged nor target
  events unless both nominated entities share the user's world Z.
- A targeted DoAfter with a finite distance threshold rejects another floor at
  initial validation and accepts an equivalent same-floor target.
- An interaction relay acts from the relay entity's floor, not from the
  controller body, and BUI message attempts remain same-floor authoritative.
- A Station AI eye moved away from its core's floor keeps that world Z through
  both proxy mode switches and cannot use its range override on the body floor.

### Explicit Deferrals

- P2.4d owns normal client selection through authored vertical portals,
  segmented obstruction, same-XY click priority, context-menu filtering, and
  the final manual interaction matrix.
- `NetCoordinates`/`EntityCoordinates` do not encode a selected world Z beyond
  their planar parent. World-only actions therefore validate identity,
  finiteness, map, range, and obstruction, but cannot yet authorize a distinct
  lower-floor point without a P2.4d network/targeting contract.
- DoAfters whose authors explicitly set `DistanceThreshold = null` retain their
  remote or non-spatial semantics. Their initiating subsystem owns any physical
  policy; the core does not reinterpret an intentional unlimited operation.

### Completion Gate

- [x] Scope check: the diff is limited to verb/action/Station-AI authority,
      request-funnel integration coverage, the native DoAfter fixture, and
      Z-level documentation.
- [x] Invariant review: Z 0 and same-floor parity, world/local frame origin,
      remote eyes, relay origins, malformed network identities, admin
      authentication, server reconstruction, and boundary-policy separation
      were reviewed.
- [x] Automated verification: 18/18 authority/funnel cases, 24/24 native
      interaction/action/DoAfter/pulling regressions, and 151/151 focused
      Z-level integration tests passed with no skips; the complete solution
      compiled with zero errors.
- [x] Performance evidence: all new checks are request/event-driven scalar
      queries. They add no tick/frame loop or result collection; rejected verbs
      return before logging/delegates, and action validity returns before
      coordinate conversion or rotation.
- [x] Documentation: funnel coverage, administrative exceptions, coordinate
      limitations, tests, decisions, and residual risks are recorded here and
      in `Docs/ZLevel.md`.
- [x] Dependency check: `RobustToolbox` remains clean at
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea`; no WTZ Engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated working-tree changes are included.
- [x] Mini review: findings and residual risks are recorded below.
- [x] Commit: package prepared as the isolated `Harden native Z-level
      interaction funnels` commit on `zlevel/interaction-funnels`; remote
      verification follows the package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed in
  1m38s with zero errors. Its 711 warnings match the existing dependency,
  vulnerability, analyzer, and upstream obsolescence baseline.
- `ZLevelInteractionAuthorityTest` passed 18/18 with no skips. Seven funnel
  scenarios cover verb families/admin exceptions, terminal and malformed
  action requests, BUI, real network drag/drop, DoAfter, relay origin, and a
  Station AI eye on a different floor from its core.
- The native regression matrix passed 24/24 with no skips, including the
  corrected targeted-DoAfter test on a real map.
- The complete focused Z-level integration matrix passed 151/151 with no
  skips.

### Decisions

- Treat every verb except `ExamineVerb` and `VvVerb` as physical by default.
  The base `Verb` is not an administrative type: gameplay uses it for pulling,
  rotation, climbing, UI activation, spilling, and many other operations.
- Permit remote generic admin verbs only after both server-owned category
  reconstruction and active `IAdminManager` authentication. A client-supplied
  category label or a de-adminned account cannot bypass floor authority.
- Keep action target validation ordered as identity/coordinate validity,
  authority/access, optional facing, then logging/event mutation. Rejection is
  terminal and cannot continue with an unset event target.
- Keep BUI, drag/drop, DoAfter, and relay policy in their existing native
  funnels. The shared Z authority remains a small primitive instead of a new
  parallel interaction dispatcher.
- Preserve the old Station AI proxy's world Z, not the core's world Z, when
  switching mode. XY coordinates and vertical context now describe the same
  camera origin.

### Mini Review

- Finding: the first verb classification exempted base `Verb` under the
  assumption that it represented generic admin commands. Repository-wide usage
  showed extensive physical gameplay consumers, so the default was inverted
  and authenticated admin categories became the narrow exception.
- Finding: rejected action validators returned without setting `Invalid`,
  allowing the outer action funnel to continue with an unset target. Both
  entity and world paths are now terminal.
- Finding: entity action validation attempted transform lookup/rotation before
  proving a network entity existed, and unlimited world actions lacked an
  explicit finite-coordinate check. Validation now precedes those effects.
- Finding: the native DoAfter interaction test spawned targeted participants in
  `Nullspace`. The stricter authority correctly rejected that context; moving
  the fixture to a test map preserves production safety and the test's intent.
- Finding: the initial Station AI regression kept eye and core on one floor and
  could not distinguish correct eye preservation from copying the core Z. The
  final case moves the eye to a separate floor before both replacements.
- Residual risk: coordinate-only target messages cannot express a lower-floor
  selection independently of their planar parent. P2.4d must solve the client
  and network contract before enabling authored interaction portals.
- Residual risk: context-menu presentation can still list a verb produced by a
  subsystem that ignores `CanAccess`; execution is safe, but P2.4d owns the UI
  filtering and same-XY priority polish.
- Next package: define and validate the client/network floor-selection contract,
  segmented interaction obstruction, context-menu filtering, and the final P2
  manual/regression review.

## Completed Package: P2.4d1 Pointer Coordinate-Layer Authority

### Scope

- Add an optional opaque `CoordinateLayer` to Robust pointer input messages and
  preserve it through local/network conversion and pointer-handler arguments.
- Interpret the layer as world Z only in WTZ Content. Entity targets own their
  selected world Z; coordinate-only requests remain on the effective spatial
  origin unless a future owning subsystem explicitly opts in.
- Resolve viewport coordinates relative to the target grid first and the active
  viewer grid second, avoiding arbitrary planar grid selection at overlapping
  XY positions and preserving moving-frame ownership.
- Carry validated world Z through world-target action prediction and expose it
  as `WorldTargetActionEvent.TargetWorldZ`.
- Stamp manually synthesized context-menu input and preserve the layer when a
  short drag is replayed as an ordinary click.

### Contracts

- `CoordinateLayer` is an engine transport primitive, not an engine Z-level
  feature. Robust never interprets, clamps, or compares it.
- The Content contract uses world Z, including a grid's
  `ZLevelFrameComponent.Origin`; local deck indices are never sent as pointer
  authority.
- A server-owned entity target must exist on the requested layer and on the
  coordinate map. A targetless coordinate must match the effective actor,
  remote-eye, or relay origin unless the consumer deliberately opts in.
- Missing layers preserve compatibility by inferring the target's authoritative
  world Z or the effective origin's world Z.
- This package transports and validates selection only. It does not authorize
  physical cross-floor use; P2.4d2 owns visible targeting, portal policy, and
  segmented obstruction.

### Completion Gate

- [x] Scope check: the diff is limited to pointer/action layer transport,
      frame-aware coordinate ownership, server validation, focused tests,
      documentation, and the paired WTZ Engine revision.
- [x] Invariant review: Z 0 compatibility, world/local frame origins,
      overlapping grids, remote spatial origins, entity versus coordinate-only
      targets, stale prediction, and same-floor policy were reviewed.
- [x] Automated verification: 20/20 authority cases, 24/24 native regressions,
      153/153 focused Z-level integration cases, and 2/2 engine message tests
      passed with no skips; the complete project solution built with zero
      errors.
- [x] Performance evidence: the hot path adds one nullable scalar to pointer
      messages and bounded transform/Z checks per request. It adds no tick or
      frame loop, cache, retained collection, or per-target scan.
- [x] Documentation: transport ownership, validation order, test evidence,
      engine pairing, deferrals, and review findings are recorded here and in
      `Docs/ZLevel.md`.
- [x] Dependency check: WTZ Project now points at WTZ Engine commit
      `ecae4d1959ecae7b681e6e96fbc05ca4577e0d2c` on
      `zlevel/pointer-coordinate-layer`; the engine branch was pushed and its
      remote SHA was verified.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and the engine worktree is clean at the paired commit.
- [x] Mini review: findings, explicit deferrals, and the next package are
      recorded below.
- [x] Commit: package prepared as the isolated `Carry authoritative floors
      through pointer targets` commit on `zlevel/interaction-targeting`; remote
      verification follows the package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed in
  1m22s with zero errors and the established 711-warning dependency,
  vulnerability, analyzer, and upstream-obsolescence baseline.
- `ZLevelInteractionAuthorityTest` passed 20/20 with no skips. Its real network
  cases prove that a synchronized world layer preserves normal use while a
  one-tick stale client selection is independently rejected by the server.
- The unchanged native interaction/action/DoAfter/pulling matrix passed 24/24
  with no skips, proving omitted layers retain established behavior.
- The complete focused Z-level integration matrix passed 153/153 with no skips.
- `InputCmdMessageTest` passed 2/2 with no skips. Focused WTZ Engine client and
  server builds also passed with zero errors (135 and 63 baseline warnings).
- A full standalone engine solution build could not include optional
  `Robust.Client.WebView`/CefGlue projects because their local
  `project.assets.json` files have not been restored. The changed Shared,
  Client, and Server engine projects and the complete WTZ Project solution all
  compiled successfully.

### Decisions

- Keep the engine field generic so other content can attach an integer layer
  without importing WTZ geometry or policy into Robust.
- Send world Z rather than local Z. This makes a pointer selection stable across
  translated/rotated grids and unambiguous when frame origins differ.
- Prefer the selected entity's grid as the coordinate frame, then the active
  viewer's grid, and only then use the existing planar fallback. Identity and
  coordinates now describe the same structural frame.
- Treat entity identity as authoritative for its floor. Coordinate-only targets
  stay same-floor in this package so a forged integer cannot create a new
  lower-floor interaction capability.
- Preserve the field in all production message reconstruction paths, including
  context-menu synthesis and drag-to-click replay.

### Mini Review

- Finding: viewport input previously called planar `TryFindGridAt`, which could
  select an unrelated overlapping grid even after Z-aware entity targeting had
  chosen the correct entity. Target/view frame ownership now resolves first.
- Finding: the drag system rebuilt quick-click messages and silently omitted the
  new layer. The completion audit found and fixed both local and network replay
  forms before commit.
- Finding: the first moving-frame test values used local `0/1` while the fixture
  origin was world Z five. Correcting the assertions to `5/6` made frame-origin
  semantics part of the regression instead of accidentally testing Z 0.
- Finding: pointer tests that left `Use` pressed depended on runner ordering.
  Explicit release plus one synchronized tick now gives 20/20 results with no
  skips in the complete authority class.
- Residual risk: visible lower-floor entities are not yet candidates for normal
  use, same-XY priority is not finalized, and context menus can still present a
  physical verb whose execution later rejects. P2.4d2 owns all three together
  with `Interaction`-channel portal and segmented obstruction checks.
- Residual risk: an empty lower-floor tile has no entity identity from which to
  derive a layer. P2.4d3 owns explicit opt-in world actions/projectiles and the
  final P2 matrix.
- Next package: implement same-floor-first entity selection, bounded visible
  lower-floor candidates, authoritative open-boundary use, segmented native
  obstruction, and matching context-menu filtering.

## Completed Package: P2.4d2 Authored Cross-Floor Entity Use

### Scope

- Add a dedicated client targeting mode for normal use, world activation, and
  alternate use that considers same-floor and visible lower-floor entities.
- Sort interaction candidates by floor distance before native sprite draw
  order, so an overlapping lower sprite can never steal a current-floor click.
- Extend physical use and verb reach only through authored vertical portals,
  preserving ordinary same-floor interaction behavior exactly.
- Trace native interaction collision masks over every horizontal segment before
  and after a vertical crossing while retaining item and wall-mount exemptions.
- Align hover outlines and context menus with the same selection and reach
  policy, while leaving pulling and other non-opted-in commands same-floor.

### Contracts

- Cross-floor entity use is downward-only under the current viewport policy.
  The target must be visible through the `Visibility` channel and usable through
  the independent `Interaction` channel; either channel may reject it.
- Range is measured in combined planar and discrete-Z distance. Admin mapping
  range bypass may skip fixture and distance checks, but never target direction,
  visibility, frame ownership, or the authored interaction boundary.
- Same-floor calls delegate to the native `InRangeUnobstructed` path. Pulling,
  BUI requests, drag/drop, entity actions, and generic targeted DoAfters keep
  their existing same-floor policy unless their owning subsystem later opts in.
- The server resolves the effective remote eye or relay origin and repeats the
  lower-floor visibility and interaction checks. Client targeting is ergonomic
  selection, never authority.
- Vertical-validation flags are private implementation details of the pointer
  funnel. Existing public interaction APIs retain their original signatures and
  cannot opt themselves into a prevalidated cross-floor path.

### Completion Gate

- [x] Scope check: the diff is limited to entity targeting, use-specific shared
      reach, physical verbs, context menus/outlines, focused tests, and docs.
- [x] Invariant review: Z 0 parity, translated/rotated frame origins, remote
      spatial origins, target direction, independent boundary channels, range,
      every trace segment, and same-XY priority were reviewed.
- [x] Automated verification: 25/25 interaction-authority cases, 24/24 native
      interaction/action/DoAfter/pulling regressions, and 158/158 focused
      `ZLevel` integration cases passed with no skips; the complete solution
      built with zero errors.
- [x] Performance evidence: candidate ordering adds one integer comparison to
      the existing bounded click list. Vertical fixture work is request-driven,
      bounded by trace budgets, and reuses one caller-owned trace buffer; no new
      tick/frame scan or retained per-entity cache was added.
- [x] Documentation: selection order, server authority, channels, obstruction,
      bypass semantics, evidence, deferrals, and findings are recorded here and
      in `Docs/ZLevel.md`.
- [x] Dependency check: no engine change is required; WTZ Project remains paired
      with clean WTZ Engine commit `ecae4d1959ecae7b681e6e96fbc05ca4577e0d2c`.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risks, and the P2.4d3 handoff are recorded
      below.
- [x] Commit: package prepared as the isolated `Enable authored cross-floor
      entity interactions` commit on `zlevel/interaction-targeting`; remote
      verification follows the package commit.

### Evidence

- `ZLevelInteractionAuthorityTest` passes 25/25. New cases cover closed/open
  portals, a visibility-only grate, upward rejection, combined 3D range,
  source/destination segment blockers on a translated and rotated frame,
  same-floor-first ordering, and a real network pointer request.
- The unchanged native matrix passes 24/24, proving that construction actions,
  pulling, normal `DoAfter`, and action lifecycles retain their prior behavior.
- `dotnet test ... --filter "FullyQualifiedName~ZLevel"` passes 158/158. A
  narrower namespace-only filter reports 142 because 16 Z-aware regressions
  intentionally live outside `Content.IntegrationTests.Tests.ZLevel`.
- `Content.Shared` and `Content.IntegrationTests` builds pass with zero errors.
  The package removed its one newly introduced nullable warning; remaining
  warnings are the established dependency, vulnerability, analyzer, and
  upstream-obsolescence baseline.

### Decisions

- Use a separate `VisibleCrossFloorInteraction` mode instead of weakening
  examine, ranged, admin, or default same-floor targeting modes.
- Prefer floor distance before draw depth only for physical use. Other modes
  retain their established visual ordering.
- Require visibility in addition to interaction permission. This prevents a
  malicious known UID from targeting an entity above the viewer or behind a
  visibility-closed deck while preserving independent boundary channels.
- Reuse native target predicates after collecting segmented trace hits, keeping
  item pickup and wall-mounted interaction behavior consistent across floors.
- Keep empty-tile input separate. Entity identity is sufficient authority for
  this package; a coordinate-only destination needs an explicit consumer-owned
  policy in P2.4d3.

### Mini Review

- Finding: the first server pass accepted an entity above the viewer when a
  forged client knew its UID, even though the client only offered visible lower
  candidates. Server authority now enforces the same downward visibility rule.
- Finding: initial optional validation flags accidentally enlarged several
  public interaction signatures. They now live only in private core funnels;
  public APIs preserve the pre-package surface and same-floor defaults.
- Finding: the historical 153-case matrix used `FullyQualifiedName~ZLevel`, not
  the namespace-only filter used during one intermediate run. Repeating the
  historical filter produced the expected prior 153 plus five new cases: 158.
- Residual risk: automated coverage proves ordering and network authority, but
  a final manual client pass should still exercise overlapping animated sprites,
  context-menu grouping, item pickup, wall mounts, and admin mapping bypass.
- Residual risk: targetless lower-floor tiles still resolve to the active/effective
  floor. P2.4d3 owns deliberate coordinate-only actions and aiming without
  weakening consumers that must remain same-floor.
- Next package: define explicit empty-tile opt-in policy for world actions and
  projectiles, test forged/stale coordinate layers, then execute the final P2
  automated and manual regression review.

## Completed Package: P2.4d3a Visible Lower-Floor Action Coordinates

### Scope

- Add an explicit networked opt-in to world-target actions without changing the
  same-floor default of any existing action prototype.
- Resolve the nearest non-empty lower-floor tile under a targetless pointer only
  through an authored `Visibility` path, skipping sparse empty layers.
- Validate the selected world Z independently on client and server using the
  effective interaction origin, map, structural frame, downward direction, and
  combined planar/discrete-Z range.
- Preserve native same-floor action validation exactly; cross-floor coordinate
  authority is a separate branch used only by opted-in consumers.

### Contracts

- `AllowCrossLevelCoordinates` grants permission to request a coordinate layer;
  it never grants permission to cross a gameplay boundary by itself. The event
  consumer must still trace its own channel, such as `Projectile`.
- Implicit selection is downward-only and bounded by the configured visibility
  distance. A closed deck, another frame, another map, an upper layer, an
  out-of-range point, or an absent destination tile is rejected.
- The selected layer is world Z. Frame origins are applied only when resolving
  the destination tile, never serialized as a local deck index.
- No production prototype opts in during this package. Guns and projectile
  actions remain same-floor until P2.4d3b supplies their complete firing path.

### Completion Gate

- [x] Scope check: the diff is limited to world-action opt-in, coordinate
      visibility/authority helpers, client selection, one focused integration
      case, and Z-level documentation.
- [x] Invariant review: Z 0 parity, same-floor native delegation, translated and
      rotated frames, world-Z origins, remote-view origins, downward direction,
      sparse layers, range, and independent boundary channels were reviewed.
- [x] Automated verification: 26/26 interaction-authority cases, 24/24 native
      interaction/action/DoAfter/pulling regressions, and 159/159 focused
      `ZLevel` integration cases pass with no skips; the complete solution
      compiles with zero errors and its established 711-warning baseline.
- [x] Performance evidence: lower-floor discovery is click-driven, performs at
      most the configured visibility distance (hard-capped at 32) in scalar tile
      checks, and adds no tick/frame loop, retained collection, or cache.
- [x] Documentation: opt-in ownership, target rules, channel separation,
      evidence, deferrals, and review findings are recorded here and in
      `Docs/ZLevel.md`.
- [x] Dependency check: no engine change is required; WTZ Project remains paired
      with clean WTZ Engine commit
      `ecae4d1959ecae7b681e6e96fbc05ca4577e0d2c`.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risks, and the P2.4d3b handoff are recorded
      below.
- [x] Commit: package prepared as the isolated `Authorize visible lower-floor
      action coordinates` commit on `zlevel/interaction-targeting`; remote
      verification follows the package commit.

### Evidence

- `ZLevelInteractionAuthorityTest` passes 26/26. The new case proves that opt-in
  cannot bypass a closed deck, that a visibility opening exposes the correct
  frame-origin-adjusted lower world Z, and that upward, out-of-range, and sparse
  empty destinations are rejected. An unlimited-range action also rejects
  `int.MinValue` before frame arithmetic without throwing.
- The reconstructed historical native matrix passes 24/24, covering click use,
  planar obstruction/range, pulling, construction actions, action lifecycle,
  normal DoAfters, cancellation, and retractable-item actions.
- `dotnet test ... --filter "FullyQualifiedName~ZLevel"` passes 159/159 with no
  skips. The prior 158 cases remain green and the new authority case adds one.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes in
  1m21s with zero errors and the established 711 dependency, vulnerability,
  analyzer, and upstream-obsolescence warnings.

### Decisions

- Put the capability on `WorldTargetActionComponent`, where prototype authors
  must deliberately request it, instead of weakening every pointer coordinate.
- Use visibility only to select and authorize a destination surface. Projectile,
  interaction, explosion, or other consumers continue to own their independent
  boundary channel and effect rules.
- Require a real destination tile. Missing sparse storage represents empty space
  and must not become an invisible aim plane.
- Keep production consumers out of this package so their trajectory and network
  authority can be reviewed together in P2.4d3b.

### Mini Review

- Finding: `EntityQuery.TryComp` exposes a nullable `out` value under the current
  annotations. Assigning the resolved grid component after the guarded query
  removed the package's only newly introduced nullable build error.
- Finding: the historical 24-case native matrix was not written as a command in
  the ledger. Reconstructing it from its documented coverage produced the exact
  prior count and is recorded explicitly in this package's evidence.
- Finding: repository-wide search confirms the new opt-in appears only in the
  component, validation paths, and focused test; no production action silently
  changed behavior.
- Finding: integer subtraction in the prior visibility distance check could
  overflow for an extreme untrusted layer. The shared helper now widens to
  `long` and rejects the request before local-frame conversion.
- Residual risk: normal gun requests and projectile events do not yet transport
  or consume a targetless lower world Z. P2.4d3b owns those paths and must apply
  the same server-owned authority before enabling production prototypes.
- Residual risk: real-client forged and one-tick-stale coordinate requests need
  a final paired-server matrix. P2.4d3c owns that hardening and the full P2
  manual review.
- Next package: carry world Z through normal gun requests and every projectile
  consumer, then start coordinate trajectories through the `Projectile` trace.

## Completed Package: P2.4d3b Coordinate Aiming For Projectile Consumers

### Scope

- Carry the selected world Z through normal gun requests without changing the
  planar `EntityCoordinates` contract or trusting the client-selected layer.
- Let targetless hitscan and physical ammunition aim at the nearest visible,
  non-empty lower-floor surface in the same structural frame.
- Forward validated coordinate layers through action guns and projectile spells.
- Enable the explicit world-action opt-in only for Fireball and Dragon's Breath.
- Preserve ordinary same-floor gun, projectile, recoil, spread, and reflection
  behavior without creating vertical route work.

### Contracts

- The server resolves entity target layers from the entity itself. Targetless
  layers must be downward, visible, non-empty, finite, on the same map and frame,
  and are revalidated from the server-owned interaction origin.
- `Visibility` authorizes selection only. Hitscan and physical ammunition still
  trace the independent `Projectile` boundary channel before crossing a deck.
- Hitscan uses the exact coordinate destination and combined XYZ max range.
  Physical ammunition keeps Robust's 2D solver and uses a 0.1-tile facing
  displacement only when an otherwise pure-vertical route needs planar progress.
- Same-floor requests never call the vertical ballistic router. Existing callers
  that omit a coordinate layer retain their native path.

### Completion Gate

- [x] Scope check: the diff is limited to gun-layer transport, projectile
      consumers, two production opt-ins, focused tests, and documentation.
- [x] Invariant review: Z 0 parity, world/local frame origins, translated and
      rotated grids, downward visibility, independent projectile boundaries,
      XYZ range, recoil/spread, pure-vertical motion, and same-floor delegation
      were reviewed.
- [x] Automated verification: 33/33 focused hitscan/ballistic cases, 27/27 native
      interaction/action/DoAfter/pulling regressions, 1/1 native weapon case,
      and 164/164 focused `ZLevel` integration cases pass with no skips. The
      complete solution builds with zero errors and its established 711-warning
      baseline.
- [x] Performance evidence: same-floor coordinate fire records zero ballistic
      route attempts. Lower-surface discovery is click/fire-request driven and
      bounded by the configured visibility distance; cross-floor consumers add
      one existing buffered trace or bounded trajectory per shot and no retained
      per-entity cache or global tick scan.
- [x] Documentation: transport, authority, consumer behavior, content opt-ins,
      verification, limitations, and review findings are recorded here and in
      `Docs/ZLevel.md`, `Docs/ZLevelHitscan.md`, and `Docs/ZLevelProjectiles.md`.
- [x] Dependency check: no engine change is required; WTZ Project remains paired
      with clean WTZ Engine commit
      `ecae4d1959ecae7b681e6e96fbc05ca4577e0d2c`.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risks, and the P2.4d3c handoff are recorded
      below.
- [x] Commit: package prepared as the isolated `Aim projectiles at lower-floor
      coordinates` commit on `zlevel/interaction-targeting`; remote verification
      follows the package commit.

### Evidence

- `ZLevelHitscanTest` and `ZLevelBallisticTrajectoryTest` pass 33/33. New cases
  prove targetless coordinate hitscan, pure-vertical physical flight through two
  boundaries, same-floor zero-route delegation, action-gun forwarding, and the
  projectile-spell consumer.
- The pure-vertical case reaches the lower target, records exactly two ordered
  crossings and one completed route, and never gains a targeted-projectile UID.
- Fireball and Dragon's Breath are the only production world actions opted into
  lower-floor coordinates. Integration startup loads both prototypes without a
  serialization or data-definition failure.
- The expanded native matrix passes 27/27, the native wielded-gun regression
  passes 1/1, and `dotnet test ... --filter "FullyQualifiedName~ZLevel"` passes
  164/164 with no skips.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes in
  1m12s with zero errors and the established 711 dependency, vulnerability,
  analyzer, and upstream-obsolescence warnings.

### Decisions

- Extend the existing gun request instead of introducing a second firing RPC.
  The nullable field preserves wire/default behavior for old and programmatic
  callers while the server remains authoritative.
- Keep entity-target and coordinate-target ballistic overloads separate at the
  public edge, then share one validated route constructor. This avoids inventing
  a fake entity for an empty lower-floor surface.
- Use one tiny planar displacement for pure-vertical physical ammunition because
  the current physics controller measures route progress in 2D. Hitscan remains
  geometrically vertical and does not need that compatibility step.
- Opt in production content only after every consumer path had focused coverage;
  unrelated world actions remain same-floor by default.

### Mini Review

- Finding: the first pure-vertical test expected to observe local Z 1 between
  ticks. A short route can process both crossings in one tick, so the final test
  checks the authoritative two-crossing metric plus destination and impact.
- Finding: ordinary same-floor shots initially reached the coordinate overload
  and recorded a rejected route attempt. Both server branches now compare the
  projectile's authoritative world Z first, and a regression proves zero route
  work.
- Finding: `ActionGunShootEvent` is a value event; the focused test now raises it
  by value, satisfying the event analyzer and matching production dispatch.
- Residual risk: a target entity can disappear, move floors, or change frames
  between request validation and consumer execution. Forged upper/different-frame
  requests also need terminal rejection instead of harmless same-floor fallback.
- Residual risk: transient gun aim state persists across burst/continuous-fire
  lifecycle boundaries by native design. P2.4d3c must prove that stale world Z
  cannot be reused by a later request.
- Residual risk: automated coverage cannot judge cursor feel, effect placement,
  or overlapping-sprite readability. The final P2 manual matrix remains required.
- Next package: harden forged and one-tick-stale gun requests, test lifecycle
  cleanup and terminal rejection, then run the final automated/manual P2 review.

## Completed Package: P2.4d3c Forged/Stale Combat Authority And P2 Review

### Scope

- Revalidate the complete gun target immediately before ammo use and before
  every follow-up burst shot, using the server-owned entity, world Z, map,
  structural frame, visibility, and coordinate state.
- Make deleted, stale, hidden, upper-floor, different-frame, and out-of-range
  explicit targets terminal for guns and hitscan instead of falling back to a
  planar coordinate shot.
- Clear transient target state when a shot ends, stops, or becomes invalid, and
  cancel an invalid burst without consuming its remaining ammunition.
- Carry the pointer coordinate layer through the native manual-throw command,
  enable visible lower-floor ranged selection, and authorize the target before
  cooldown, stack splitting, dropping, or throwing the item.
- Audit every gun, hitscan, and manual-throw producer/consumer and close the P2
  automated and code-path review.

### Contracts

- A client-selected layer is context, never authority. An explicit entity must
  still exist and its current server world Z must equal the requested layer.
  An unresolved explicit UID remains an invalid entity request and cannot be
  reinterpreted as targetless coordinates.
- Rejected gun requests consume no ammo. Rejected manual throws leave the item
  in the hand. Both paths perform zero vertical route work.
- An explicit lower-floor entity supplies its planar aim from the current
  server transform. A forged companion coordinate cannot redirect that shot or
  throw. Targetless lower-floor coordinates retain their independent visibility
  and frame checks.
- Same-floor gunfire and throws keep native two-dimensional behavior and record
  zero ballistic route attempts. Vertical routing remains lower-only and must
  pass the independent `Projectile` boundary channel.
- Burst aim is revalidated per shot. A moved or stale target cancels the burst,
  resets counters and aim state, and preserves all unspent ammunition.

### Completion Gate

- [x] Scope check: the diff is limited to combat/throw target authority,
      lifecycle cleanup, focused network tests, and Z-level documentation.
- [x] Invariant review: Z 0 and same-floor parity, world/local frame origins,
      moving and different grids, explicit versus coordinate-only targets,
      deleted identities, upward denial, visibility, range, burst lifecycle,
      projectile boundaries, and pre-consumption rejection were reviewed.
- [x] Automated verification: 51/51 focused hitscan/ballistic cases, 7/7 real
      client/server manual-throw cases, 4/4 native weapon/throw cases, 24/24
      historical interaction/action/DoAfter/pulling regressions, and 182/182
      focused `ZLevel` integration cases pass with no skips. The complete
      solution builds with zero errors and its established 711-warning baseline.
- [x] Performance evidence: same-floor gun and throw requests record zero
      ballistic attempts; invalid requests reject before ammo/drop and also
      record zero attempts. All new validation is request or burst-shot driven,
      with no global scan, retained collection, cache, or tick/frame loop.
- [x] Documentation: authority, terminal failure, burst and throw lifecycle,
      verification, decisions, and residual visual QA are recorded here and in
      `Docs/ZLevel.md`, `Docs/ZLevelHitscan.md`, and
      `Docs/ZLevelProjectiles.md`.
- [x] Dependency check: no engine change is required; WTZ Project remains paired
      with clean WTZ Engine commit
      `ecae4d1959ecae7b681e6e96fbc05ca4577e0d2c`.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risk, and the P3.1 handoff are recorded
      below.
- [x] Commit: package prepared as the isolated `Harden Z-level combat request
      authority` commit on `zlevel/interaction-targeting`; remote verification
      follows the package commit.

### Evidence

- Real paired client/server commands prove lower entity and targetless-coordinate
  gunfire, native same-floor fire, forged upper entity/coordinate rejection,
  stale-layer rejection, different-frame rejection, and idle target cleanup.
- Burst coverage proves all three valid shots preserve the authoritative target,
  while a target that changes layer after the first shot cancels the remaining
  two and leaves their ammunition intact.
- Hitscan coverage proves deleted, hidden, upper, stale, different-frame, and
  out-of-range explicit targets emit no trace rather than using a same-floor
  decoy or planar fallback.
- Seven real pointer-input throw cases prove lower entity and coordinate routes,
  same-floor zero-route behavior, forged upper entity/coordinate rejection,
  stale-layer rejection, deleted-UID rejection, and `Down`/`Up` input hygiene.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes in
  1m25s with zero errors and the established dependency, vulnerability,
  analyzer, and upstream-obsolescence warning baseline.

### Decisions

- Validate raw gun request state in the shared firing funnel so normal client
  fire, action guns, signal guns, NPC fire, and burst continuation share one
  authority rule rather than duplicating policy in projectile consumers.
- Preserve an unresolved explicit network UID as a non-null invalid target.
  This small distinction makes rejection terminal and prevents a stale entity
  request from gaining the more permissive coordinate-only path.
- Derive cross-floor entity aim from the entity's current server transform while
  retaining native coordinate aim on the same floor. This closes forged-planar
  redirection without changing ordinary SS14 cursor shooting.
- Upgrade native hand throwing through the existing pointer-layer transport.
  It validates before any irreversible hand or stack mutation and uses the same
  bounded ballistic controller as projectile ammunition.

### Mini Review

- Finding: the final producer audit found that native manual throwing still used
  the legacy pointer callback, discarded `CoordinateLayer`, and could drop an
  item before discovering that its vertical target was invalid. The callback is
  now layer-aware and all authority runs before item mutation.
- Finding: the first network-throw test helper sent `Down` without `Up`. Reused
  integration pairs correctly retained the pressed key and exposed the fixture
  bug as a dirty-disposed skip. The helper now completes the real input cycle,
  and the seven-case matrix passes with no order dependence or skips.
- Finding: explicit hitscan targets that disappeared or failed vertical policy
  previously degraded to a same-floor ray. Explicit-target failure is now
  terminal from request validation through the final trace constructor.
- Residual risk: headless automation cannot judge cursor feel, overlapping-sprite
  readability, beam segmentation, or impact appearance. Those visual checks are
  retained as an explicit P8 public-server hardening pass; the P2 code-path,
  authority, network, native-regression, and full integration reviews are
  complete.
- Next package: establish P3.1 lighting/FOV ownership, visual baselines, metrics,
  cache keys, invalidation events, and performance thresholds before projecting
  light through lower-floor `Visibility` boundaries.

## Phase P3 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P3.1 | Lighting/FOV architecture, visual baselines, metrics, and cache contract | Complete |
| P3.2 | Chunked vertical aperture/emitter cache and targeted invalidation | Complete |
| P3.3 | Bounded lower-floor light/FOV projection and attenuation | Complete |
| P3.4a | Lighting retention, frame budgets, and whole-emitter fail-soft behavior | Complete |
| P3.4b | Independent lower-tile/FOV and mapping-preview budgets and batching | Complete |
| P3.4c | Lower-floor shadows, pixel regressions, and rendering hardening | Complete (c1-c3) |

## Completed Package: P3.1 Native Active-Floor Rendering Contract

### Scope

- Carry the entity-backed eye's world Z into Robust rendering without replacing
  the normal map position or viewport.
- Render the active sparse tile layer through Clyde's native grid mesh instead
  of using the Content overlay as a substitute for every non-zero floor.
- Filter point lights, FOV occluders, and occluder neighbor faces by world Z
  before existing frame budgets are consumed.
- Key and invalidate GPU mesh entries by grid, chunk, and local Z, including
  upper-only sparse chunks and complete GPU object cleanup.
- Expose local render counters and establish a deterministic three-color visual
  fixture plus a repeatable manual baseline procedure.

### Acceptance Criteria

- Z 0 retains the normal native tile, light, shadow, and FOV path.
- A displaced grid converts the eye's world Z to its own local layer; overlapping
  grids compare lights and occluders in world Z.
- Changing floors cannot reuse another layer's tile or edge mesh.
- Tile edits, replication, empty-layer transitions, and grid deletion invalidate
  only the relevant cached layer and neighboring edge meshes.
- A chunk containing only non-zero tiles remains spatially discoverable, shrinks
  after removals, and survives map save/load.
- Off-floor lights and occluders are rejected before native light/geometry limits.
- Content no longer redraws the active layer, but keeps lower-floor and mapping
  preview composition until P3.3 owns vertical projection.
- The canonical map contains one red, green, and blue light at the same XY on
  local Z 0, 1, and 2 and validates their floor state during map loading.

### Evidence

- WTZ Engine `Robust.Client` builds with zero errors. All 135 client integration
  tests and all 29 client unit tests pass, including entity-eye world-Z tracking
  and same/different-world-Z occluder neighbors.
- Robust shared serialization passes 4/4, including sparse upper-only grid AABB
  expansion, contraction, save, and reload. Z-level chunk replication passes
  1/1 across a real server/client pair.
- The complete Content test filter containing `ZLevel` passes 182/182 with no
  skips. The canonical-map fixture load passes and validates all three lights.
- The complete solution build passes with zero errors and the established
  711-warning dependency, vulnerability, analyzer, and upstream-obsolescence
  baseline.
- The 3-, 6-, and 10-floor server baselines pass 3/3. Measured phases retain
  6,336 allocated bytes, 100% warm boundary-cache hits, and zero evictions;
  measured times were 8.951 ms, 15.845 ms, and 26.769 ms respectively.
- Renderer-specific counters are available through `zlevelrendermetrics` and
  `zlevel.debug_overlay`. The headless harness cannot produce trustworthy GL
  pixels, so color/shadow screenshots use the versioned manual fixture and
  procedure in `Docs/ZLevelLighting.md`.

### Decisions

- Keep one normal viewport. Clyde renders exactly the active native grid layer;
  Content only composites policy-approved non-active layers.
- Store `WorldZLevel` on `IEye`, update it from the effective eye target every
  frame, and let every grid convert that value through its `ZLevelFrame` origin.
- Use `(grid, chunk, local Z)` as the GPU mesh key. A cache keyed only by chunk
  can display stale floors when the eye traverses vertically.
- Filter lights and occluders before their existing limits so hidden floors do
  not starve the active floor's quality budget.
- Recompute grid spatial bounds from the union of base and sparse Z-layer chunk
  bounds. Z-only floors must be discoverable by the native grid query.
- Move `ZLevelPositionChangedEvent` beside the engine component and use generated
  post-network-state events to refresh client occluder adjacency.
- Defer aperture indexing, lower-floor light projection, attenuation, eviction
  budgets, and fail-soft quality policy to P3.2 through P3.4.

### Completion Gate

- [x] Scope check: the engine diff is limited to eye/render/map primitives and
      their tests; the project diff is limited to Content composition, metrics,
      fixture validation, and documentation.
- [x] Invariant review: Z 0 parity, local/world frame origins, overlapping and
      moving grids, sparse Z-only chunks, network state, cache invalidation, and
      native light/occluder limits were reviewed.
- [x] Automated verification: 135 engine client integration, 29 engine client
      unit, 4 serialization, 1 replication, 182 Content Z-level, 3 baseline, and
      the full solution build pass with no failures or skips.
- [x] Performance evidence: server baselines remain allocation-stable and fully
      warm; local GPU-layer hits/misses, retained entries, draws, and Z rejections
      are now observable for the P3.2/P3.4 measurements.
- [x] Documentation: architecture, cache ownership, fixture, manual procedure,
      commands, deliberate limits, and results are recorded here and in
      `Docs/ZLevelLighting.md`.
- [x] Dependency check: WTZ Engine commit
      `17f6c8f8d763c2b6afa8d136cbb87c88934f0372` is committed, pushed, clean,
      and paired by the project submodule pointer.
- [x] Git check: engine and project `git diff --check` pass apart from checkout
      line-ending notices; generated baseline artifacts remain ignored.
- [x] Mini review: findings, residual risks, and the P3.2 handoff are recorded
      below.
- [x] Commit: engine saved as `Render active grid layers by world Z`; project
      package prepared as `Make active-floor rendering world-Z aware`.

### Mini Review

- Finding: the native Clyde grid renderer always read `MapChunk` Z 0. Upper
  floors appeared only because Content manually drew their tiles, which meant
  native tile lighting and edge rendering never followed the player.
- Finding: filtering final occluder geometry was insufficient because neighbor
  faces had already merged occluders from different floors. Adjacency now also
  requires equal world Z and refreshes after local or replicated Z changes.
- Finding: sparse chunks containing only upper-floor tiles did not contribute to
  the grid AABB, so native culling could omit them even with a correct mesh path.
- Finding: deleting cached chunks previously omitted the edge VAO/VBO/EBO. The
  layer-aware cleanup now releases both normal and edge GPU objects.
- Residual risk: automated tests validate render inputs, cache ownership, and
  lifecycle but cannot inspect real GL pixels. The canonical RGB fixture makes
  the remaining color, shadow, and transient-flash pass deterministic.
- Residual risk: visible lower floors intentionally receive no projected point
  light yet. P3.2 and P3.3 must derive bounded projection from `Visibility`
  openings without reintroducing multi-viewport rendering.
- Residual risk: the GPU layer cache has lifecycle cleanup but no independent
  capacity/eviction budget. P3.4 will choose policy from measured live counters.
- Residual risk: `MapTextOverlay` is still a general 2D map overlay without a Z
  filter. The visual fixture deliberately uses real light sprites; the broader
  overlay audit belongs to P3.4 rendering hardening.
- Next package: build a chunked cache of vertical visibility apertures and light
  emitters with revisioned, targeted invalidation and measurable cold/hot paths.

## Completed Package: P3.2 Vertical Lighting Input Cache

### Scope

- Cache `Visibility` apertures in 16 by 16 chunks keyed by grid, chunk, and
  lower grid-local Z, with four-word bitsets and monotonic revisions.
- Invalidate exact cache entries after base/sparse tile edits and explicit
  boundary changes, all entries on a reconfigured map, and all entries owned by
  a removed grid.
- Publish one map-configuration change event for local configuration, component
  lifecycle, and replicated state, and use it to invalidate the authoritative
  shared boundary cache as well as the client aperture cache.
- Reuse Robust's live point-light component tree as the emitter index, resolving
  current world positions and world Z across translated, rotated, and displaced
  grid frames.
- Add caller-owned component-tree query buffers to WTZ Engine so warmed emitter
  discovery does not allocate per query.
- Expose aperture/emitter counts, rejections, hit rate, invalidation, and timing
  through `zlevelrendermetrics`, its `reset` option, and the debug overlay.

### Acceptance Criteria

- Cold chunk construction matches `SharedZLevelBoundarySystem` for tiled and
  explicit policies, including negative chunks and negative local layers.
- A tile or provider change rebuilds only its exact chunk/lower layer; unrelated
  revisions survive, map policy changes clear every affected grid, and grid
  removal cleans only that grid.
- Emitter discovery returns live source properties on the requested world floors
  and follows moving grid transforms without a parallel movement index.
- Warming caller-owned aperture, emitter-result, and component-tree buffers
  removes allocation proportional to query count.
- Equivalent 3-, 6-, and 10-floor workloads build linearly by authored boundary
  count and retain one discoverable emitter per floor.
- Existing Z 0, mapping, atmosphere, combat, interaction, and save/load tests
  remain green.

### Evidence

- The six dedicated package cases pass 6/6. They cover cache hit/miss and
  revisions, targeted tile/policy/provider/grid invalidation, negative indices,
  explicit marker replication and anchoring, moving frames, native emitter
  properties, reusable buffers, and 3/6/10-floor scale.
- The complete Content filter containing `ZLevel` passes 188/188 with no skips.
  The full solution builds with zero errors and the established 700-warning
  dependency, analyzer, vulnerability, and upstream-obsolescence baseline.
- WTZ Engine passes 136/136 client integration tests, 29/29 client unit tests,
  1,026/1,026 shared integration tests, and 446/446 shared unit tests. Its direct
  query-buffer test validates one native light and 100 warmed allocation checks.
- The generated server baselines pass 3/3. Their measured phases retain 6,336
  allocated bytes, 100% boundary-cache hits, and zero evictions; measured times
  were 7.842 ms, 17.971 ms, and 23.103 ms for 3, 6, and 10 floors.
- The client scale fixture retained 2, 5, and 9 aperture chunks for 3, 6, and 10
  floors. Warming both input paths produced zero managed bytes in every case.
  Diagnostic cold totals were 14.263 ms, 16.169 ms, and 4.484 ms; JIT/pool state
  makes these comparison observations rather than pass thresholds.
- The paired WTZ Engine revision is
  `dca90bdf1f9e93539a03078186eb72922257054d` on
  `zlevel/lighting-aperture-cache`.

### Decisions

- Keep aperture policy in Content and source it from the shared boundary
  authority. The cache stores decisions, not a second rules engine.
- Reuse `LightTreeSystem` as the only live emitter index. Duplicating light
  movement and hierarchy bookkeeping would add invalidation races before P3.3
  has a consumer.
- Store apertures in grid-local Z because their tiles and invalidation events are
  grid-local; filter emitters by world Z because overlapping frames must agree
  in one map-space coordinate.
- Use approximate grid selection only as the component-tree broad phase, then
  apply the exact light-circle check. This removed the fixed per-query fixture
  intersection allocation without changing accepted emitters.
- Keep the cache lifecycle-bounded but not capacity-bounded in P3.2. P3.4 will
  select eviction and fail-soft policy from the visible working set measured
  after P3.3 projection exists.
- Keep emitter queries sequential on the client main thread. The system owns one
  retained tree scratch buffer and does not claim reentrant or parallel use.

### Completion Gate

- [x] Scope check: the project diff is limited to vertical lighting inputs,
      shared map-policy invalidation, metrics, tests, and documentation; the
      engine diff contains only reusable component-tree querying and its test.
- [x] Invariant review: Z 0, negative local Z, world/local frame conversion,
      translated and rotated grids, explicit boundaries, replication, mapping
      anchoring, and lifecycle cleanup are represented.
- [x] Automated verification: 6/6 package, 188/188 Content Z-level, 3/3 server
      baseline, 136/136 engine client integration, 29/29 engine client unit,
      1,026/1,026 engine shared integration, and 446/446 engine shared unit tests
      pass, followed by a zero-error full solution build.
- [x] Performance evidence: scale fixtures cover 3/6/10 floors; warmed aperture,
      emitter, and direct engine tree queries allocate no bytes proportional to
      query count, while historical server baselines remain stable.
- [x] Documentation: ownership, cache keys, invalidation, emitter discovery,
      metrics, scale behavior, limitations, and the P3.3 handoff are recorded in
      `Docs/ZLevelLighting.md` and this ledger.
- [x] Dependency check: WTZ Engine commit
      `dca90bdf1f9e93539a03078186eb72922257054d` is committed, pushed, clean, and
      paired by the project submodule pointer.
- [x] Git check: engine and project `git diff --check` pass apart from checkout
      line-ending notices; generated baseline artifacts remain ignored and no
      unrelated files are included.
- [x] Mini review: findings, residual risks, and P3.3 are recorded below.
- [x] Commit: engine saved as `Add reusable component-tree query buffers`;
      project package prepared as `Cache vertical lighting inputs`.

### Mini Review

- Finding: changing `ZLevelMap.DefaultBoundaryMode` previously left the shared
  boundary cache stale. A common lifecycle/state event now invalidates both the
  authority cache and its client lighting consumer.
- Finding: explicit boundary markers only participate while anchored. The
  integration fixture now creates a real supporting tile and asserts server and
  client anchoring, preventing an invalid test setup from imitating cache drift.
- Finding: caller-owned lists alone were insufficient because precise grid
  fixture selection allocated once per component-tree query. Propagating the
  existing approximate-query contract removed that cost while exact emitter
  filtering preserved results.
- Residual risk: P3.2 prepares inputs but deliberately produces no lower-floor
  illumination or cross-floor FOV. Visual correctness begins in P3.3.
- Residual risk: retained aperture chunks have lifecycle cleanup but no capacity
  or per-frame build budget. P3.4 owns bounded retention and predictable
  degradation after projection load is measurable.
- Residual risk: the retained emitter tree buffer makes the query API
  non-reentrant. Current render work is main-thread and sequential; a future
  parallel renderer must provide per-job buffers instead.
- Next package: consume these inputs to project bounded lower-floor light and FOV
  through ordered `Visibility` apertures with deterministic attenuation.

## Completed Package: P3.3 Bounded Lower-Floor Light/FOV Projection

### Scope

- Compose every adjacent `Visibility` aperture between a lower source floor and
  the viewer into one four-word chunk mask, stopping early when it becomes
  completely closed.
- Query only lower point lights inside the viewport and configured depth, keep
  each source tied to its own grid/frame, and produce deterministic retained
  batches plus horizontal aperture runs.
- Match native point-light radius, height, energy, color, mask rotation,
  falloff, and curve behavior while applying vertical distance and per-floor
  transmission.
- Draw projected light into Clyde's `BeforeLighting` target before active-floor
  FOV and native point lights, using a dedicated native-equivalent additive
  blend mode in WTZ Engine.
- Make lower-floor tile composition consume the same cached aperture stacks as
  projected light, one query per chunk rather than one boundary walk per tile.
- Expose planning and rendering counts/timings in `zlevelrendermetrics` and the
  debug overlay.

### Acceptance Criteria

- A lower light contributes only where every crossed visibility boundary is
  open; a closure at any intermediate floor rejects that column.
- Active-floor lights remain on the native Clyde path and are never duplicated
  by the projection overlay.
- Depth is capped by `MaxVisibleLevelDistance`; radius and transmission decrease
  deterministically with depth.
- Translated and rotated Z-level frames preserve source world position, local
  aperture coordinates, and directional mask UVs.
- Lower tiles and projected light agree on the same composed visibility bits.
- Warming planner, aperture, emitter, and geometry buffers removes allocation
  proportional to frame or query count.
- Z 0 and existing mapping, atmosphere, combat, interaction, projectile, and
  persistence behavior remain green.

### Evidence

- The six dedicated P3.3 cases pass 6/6. They cover complete multi-boundary
  stacks, closures at different depths, moving/rotated frames, depth scaling at
  3/6/10 floors, source-mask UVs, attenuation packing, shader/prototype loading,
  and warmed caller-owned buffers.
- Projection plus P3.2 cache tests pass 11/11. The complete Content filter
  containing `ZLevel` passes 194/194 with no failures or skips.
- WTZ Engine passes 136/136 client integration and 30/30 client unit tests. The
  new parser case verifies `blend_mode light_add`; existing blend modes remain
  unchanged.
- The complete solution build passes with zero errors. Its remaining warnings
  are established dependency, analyzer, vulnerability, and upstream-obsolescence
  findings whose exact count varies between incremental and clean builds.
- The 3-, 6-, and 10-floor server baselines pass 3/3 with 6,336 measured bytes,
  100% warm boundary-cache hits, and zero evictions. Measured times were 6.999,
  13.120, and 24.143 ms respectively.
- Dedicated hot loops assert at most one fixed 512-byte runtime bookkeeping
  allowance after warm-up for both planning and geometry; work remains bounded
  by viewport, light radius, authored depth, and aperture chunks.
- The paired WTZ Engine revision is
  `9d63eec79515c766a875f0d32803250298ddbcde` on
  `zlevel/lighting-projection`.

### Decisions

- Keep the active floor entirely native. Content projects only lower-floor
  contributions and Clyde applies the final active-floor FOV afterward.
- Intersect same-column aperture bits for the complete source-to-viewer stack.
  This renders the lower scene visible through authored openings; it does not
  infer physical light spill onto opaque upper tiles.
- Keep emitters on their own grids instead of testing every overlapping grid.
  Shared XY overlap is not an authored vertical connection.
- Encode per-emitter attenuation data in retained vertices. Reusing one mutable
  shader instance with changing uniforms would make queued draw commands observe
  the final emitter's values.
- Quantize falloff to 1/16 and curve factor to 1/4095 so radius, mask UV, color,
  energy, depth, and both curve controls fit the existing vertex contract
  without per-light shader instances.
- Add `light_add` rather than changing Robust's unused but existing `add`
  semantics. The new mode exactly matches native point-light blending.
- Defer cache capacity, per-frame work budgets, and predictable fail-soft
  degradation until P3.4 has these measured projection counters.

### Completion Gate

- [x] Scope check: the project diff is limited to lower-floor light/FOV
      projection, shared aperture composition, diagnostics, tests,
      documentation, and the paired engine pointer; the engine diff contains
      only one additive blend mode and its parser test.
- [x] Invariant review: Z 0 parity, world/local Z, moving and rotated frames,
      multi-floor closure, source-grid ownership, active-floor native rendering,
      mask rotation, and visibility-channel authority are represented.
- [x] Automated verification: 6/6 dedicated projection, 11/11 projection/cache,
      194/194 Content Z-level, 3/3 baseline, 136/136 engine integration, and
      30/30 engine unit tests pass, followed by a zero-error solution build.
- [x] Performance evidence: 3/6/10-floor planning is depth-bounded, warmed
      planner and geometry loops retain caller-owned buffers, server baselines
      remain stable, and live build/draw counters are exposed.
- [x] Documentation: pipeline order, equations, cache sharing, metrics, manual
      fixture procedure, limits, and verification are recorded here and in
      `Docs/ZLevelLighting.md`.
- [x] Dependency check: WTZ Engine commit
      `9d63eec79515c766a875f0d32803250298ddbcde` is committed, pushed, clean,
      and paired by the project submodule pointer.
- [x] Git check: engine and project `git diff --check` pass apart from checkout
      line-ending notices; generated baseline artifacts remain ignored and no
      unrelated files are included.
- [x] Mini review: findings, residual risks, and P3.4 are recorded below.
- [x] Commit: engine saved as `Add native light blend mode`; project package is
      prepared as `Project lower-floor Z-level lighting`.

### Mini Review

- Finding: Robust's generic `add` mode does not use the destination-preserving
  blend function used by point lights. A separate tested mode avoids changing
  existing shader behavior.
- Finding: per-batch mutable uniforms are unsafe in Clyde's queued draw path.
  Retained vertex data preserves each emitter's values through queue flush.
- Finding: lower-floor tile composition repeated a full boundary-stack query for
  every tile. Chunk masks now make tile and light visibility both cheaper and
  structurally identical.
- Finding: the native shader includes a unit light-height term. Including it in
  projected radius and attenuation prevents lower lights from gaining range.
- Residual risk: lower-floor walls do not yet cast source-specific shadows into
  projected light. Aperture clipping and active-floor FOV are correct, but this
  visual limitation belongs in P3.4 hardening.
- Residual risk: aperture retention and per-frame projection have no independent
  capacity/work budget. P3.4 must degrade predictably under dense light loads.
- Residual risk: headless tests validate shader resources, geometry, masks, and
  inputs but cannot inspect final GL pixels. The canonical map procedure remains
  mandatory for P3.4 visual regression capture.
- Next package: add measured cache/frame budgets, deterministic fail-soft
  degradation, lower-floor shadow policy, and visual regression hardening.

## Completed Package: P3.4a Bounded Lighting Retention And Projection Work

### Scope

- Bound retained vertical aperture chunks with a configurable client FIFO and
  exact recomputation after eviction.
- Add shared per-client-frame limits for native light candidates, planned
  emitters, composed aperture layers, cold aperture builds, and generated runs.
- Process discovered lower lights nearest-floor first and reject an overflowing
  source as one complete unit instead of publishing a truncated light shape.
- Let cold-cache frames make bounded forward progress without exposing partial
  stacks, and share the same allowance across automatic viewports.
- Expose capacity, evictions, used/maximum work, and cumulative exhaustion in
  `zlevelrendermetrics` and the debug overlay.

### Acceptance Criteria

- Aperture retention clamps to a finite range and never exceeds configured
  capacity after a build or runtime configuration change.
- Evicting a layer while composing a deeper stack does not alter the returned
  mask; a later query can recompute the same result exactly.
- All work controls are local archived client CVars. A server cannot raise a
  client's projection workload.
- Active-floor native light is unchanged and does not consume Content budgets.
- The nearest discovered lower-floor emitter survives a planner limit first.
- Layer, cold-build, or run exhaustion leaves no batch or run belonging to the
  incomplete emitter; earlier complete emitters remain valid.
- A cold-build limit warms retained state progressively across frames, while
  budget exhaustion is counted at most once per category per frame.

### Evidence

- The eight dedicated P3.4a cases pass 8/8. They cover lower and upper CVar
  clamps, FIFO eviction, exact depth-two composition with capacity one,
  candidate early-out, nearest-floor priority, progressive cold-cache warming,
  whole-emitter layer/run rollback, and same-frame budget sharing.
- The combined cache, projection, and budget filter passes 19/19, retaining the
  P3.2/P3.3 moving-frame, shader, attenuation, depth, and warmed-allocation
  coverage.
- The complete Content filter containing `ZLevel` passes 202/202 with no
  failures or skips. Structural unit tests pass 2/2 and generated 3-, 6-, and
  10-floor stress baselines pass 3/3.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes with
  zero errors. Reported warnings are established dependency, vulnerability,
  analyzer, and upstream-obsolescence findings.
- The warmed projection benchmark still permits only its fixed 512-byte runtime
  bookkeeping allowance. FIFO cache hits do not mutate queue order or allocate.

### Decisions

- Use insertion-order FIFO rather than LRU. It gives deterministic bounded
  retention without adding a write or queue token to every hot lookup.
- Keep aperture words already read in local stack state. Capacity can therefore
  be smaller than source depth without corrupting an in-flight composition.
- Charge work already performed even when an emitter rolls back, then stop that
  viewport's planner. Retrying later sources would make cost and quality depend
  on source shape in less predictable ways.
- Share budgets by `IGameTiming.CurFrame`. Multiple automatic viewports consume
  one allowance, while tests explicitly advance a budget frame when measuring
  repeated independent plans.
- Sort queried sources by descending world Z and entity UID. A hard candidate
  cap can prioritize only entries it discovered; native intersecting-grid tree
  selection remains outside the point-light entry budget.
- Keep lower-tile/FOV composition independent from light planning. P3.4b will
  add a separate nearest-first pool so dense light cannot starve visible tiles.

### Completion Gate

- [x] Scope check: the diff is limited to client lighting retention/budgets,
      fail-soft planning, diagnostics, focused tests, and lighting documents.
- [x] Invariant review: active-floor native ownership, Z 0, world/local frames,
      nearest-floor ordering, multi-layer stacks, exact eviction recomputation,
      same-frame sharing, and complete-emitter rollback are represented.
- [x] Automated verification: 8/8 dedicated, 19/19 lighting, 202/202 cumulative
      Z-level integration, 2/2 unit, 3/3 baseline, and zero-error full build
      pass.
- [x] Performance evidence: candidate, emitter, layer, cold-build, run, and
      retention limits are finite and observable; warmed allocation coverage
      remains green.
- [x] Documentation: controls, ordering, rollback, observability, fixtures, and
      deliberate deferrals are recorded here and in `Docs/ZLevelLighting.md`.
- [x] Dependency check: WTZ Engine remains clean at
      `9d63eec79515c766a875f0d32803250298ddbcde`; no paired engine change is
      required.
- [x] Git check: staged diff and whitespace checks pass; status contains only
      the nine declared files and no generated test or baseline artifacts.
- [x] Mini review: findings and residual risks were confirmed against the final
      staged implementation and are recorded below.
- [x] Commit: package prepared as `Bound Z-level lighting projection work` on
      `zlevel/lighting-hardening`; remote verification follows the commit.

### Mini Review

- Finding: capacity eviction during a stack query is safe because intersection
  state is value-copied before a later build can evict the source cache entry.
- Finding: whole-emitter rollback prevents a run budget from drawing striped or
  otherwise malformed light; visible-run metrics count only accepted plans.
- Finding: a small cold-build limit becomes deterministic cache warming rather
  than an all-or-nothing frame spike.
- Residual risk: lower-floor tile/FOV and mapping-preview composition are still
  unbudgeted and need an independent fail-soft policy in P3.4b.
- Residual risk: lower-floor source-specific wall shadows remain absent. P3.4c
  must implement or explicitly constrain that visual policy and capture real
  pixel baselines.
- Residual risk: the candidate cap starts after native intersecting-grid tree
  selection. P8 stress profiling must cover pathological moving-grid overlap.
- Next package: bound lower-tile/FOV and mapping-preview composition without
  allowing light work to starve scene visibility.

## Completed Package: P3.4b Bounded Tile/FOV And Mapping Composition

### Scope

- Extract lower-floor and adjacent-preview tile selection from
  `ZLevelDebugOverlay` into a retained, directly testable projection system.
- Give normal lower-tile/FOV composition and mapping preview independent
  per-client-frame chunk and tile pools; add aperture-layer and cold-build pools
  only to normal visibility composition.
- Process normal floors nearest-first, grids nearest the viewport first, and
  chunks center-out; process lower mapping preview before upper preview.
- Reject an incomplete chunk before publishing any of its tiles while retaining
  earlier complete chunks and charging work already performed.
- Batch tile geometry into one draw call per projected chunk and reuse planner,
  tile, batch, and vertex buffers after warm-up.
- Expose controls, work, exhaustion, batching, and timings through
  `zlevelrendermetrics` and the debug overlay.

### Acceptance Criteria

- Clyde's active floor remains native and consumes none of the Content tile or
  mapping budgets.
- Normal lower tiles use the same complete aperture stacks as projected light;
  mapping preview remains an authoring view and deliberately bypasses them.
- Light, normal-tile, and mapping-preview work cannot starve each other's pools.
- Under a limit, the nearest lower floor, nearest grid, center chunk, and lower
  adjacent preview receive deterministic priority.
- Layer, cold-build, chunk, or tile-visit exhaustion never exposes a partial
  chunk; completed work remains stable and counters increment at most once per
  category in one frame.
- Moving and rotated grid frames retain correct local tiles and ascending world-Z
  draw order.
- Repeated warmed planning and geometry do not allocate proportionally to frame
  count, and draw calls scale with projected chunks rather than visible tiles.

### Evidence

- The thirteen dedicated P3.4b cases pass 13/13. They cover complete aperture
  stacks, translated and rotated frames, ascending world-Z output, nearest-floor
  and nearest-grid/center-chunk priority, whole-chunk tile-visit rollback,
  progressive cold cache warming, layer exhaustion, independent preview pools,
  lower-before-upper preview, normal and preview frame sharing, CVar clamps, and
  buffer reuse.
- The combined P3.2 through P3.4b lighting/tile matrix passes 32/32 against the
  shared aperture cache. The complete Content filter containing `ZLevel` passes
  215/215 with no failures or skips; structural unit tests pass 2/2.
- Generated 3-, 6-, and 10-floor server baselines pass 3/3 with 6,336 measured
  bytes, 100% warm boundary-cache hits, and zero evictions. Measured times were
  10.951, 14.803, and 25.142 ms respectively.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes with
  zero errors and 711 established dependency, vulnerability, analyzer, and
  upstream-obsolescence warnings.
- The warmed planner and geometry loops each remain within their fixed 512-byte
  runtime-bookkeeping allowance across 100 iterations. The renderer now issues
  one draw call per non-empty projected chunk instead of one per tile.

### Decisions

- Keep selection and budget policy in `ZLevelTileProjectionSystem`; the overlay
  resolves atlas regions and submits retained batches but no longer walks map
  storage or visibility boundaries itself.
- Give normal and preview modes separate `IGameTiming.CurFrame` pools and keep
  both separate from lighting. Dense lights and authoring preview therefore
  cannot erase normal lower-floor scene visibility.
- Charge every clipped tile slot in a chunk before scanning it. This conservative
  upper bound is deterministic, makes publication transactional, and avoids a
  second unbudgeted discovery pass.
- Order planning for quality, then sort accepted batches separately for drawing.
  Nearest-first degradation and far-to-near composition are distinct concerns.
- Let mapping preview bypass apertures but retain chunk and tile limits. It must
  show adjacent authored floors through closed decks without becoming unbounded.
- Reuse Robust's approximate intersecting-grid query and retained result list.
  Native tree selection remains outside the Content chunk budget and is a P8
  stress target for pathological moving-grid overlap.

### Completion Gate

- [x] Scope check: the diff is limited to client tile/FOV and mapping planning,
      rendering batches, local CVars, diagnostics, tests, and P3 documentation.
- [x] Invariant review: active-floor ownership, Z 0, authored map ranges,
      world/local frame origins, translated and rotated grids, complete aperture
      stacks, normal/preview separation, and deterministic priority are covered.
- [x] Automated verification: 13/13 dedicated, 32/32 combined lighting/tile,
      215/215 cumulative Z-level integration, 2/2 structural unit, 3/3 baseline,
      and a zero-error full solution build pass.
- [x] Performance evidence: finite work controls and live counters bound chunks,
      layers, cold builds, and tile visits; hot planner/geometry allocation and
      server stress baselines remain green; draw calls are chunk-batched.
- [x] Documentation: controls, ordering, atomicity, batching, metrics, tests,
      limitations, and next work are recorded here and in
      `Docs/ZLevelLighting.md`.
- [x] Dependency check: WTZ Engine remains clean at
      `9d63eec79515c766a875f0d32803250298ddbcde`; no paired engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices; generated baseline artifacts remain ignored and only declared
      package files are included.
- [x] Mini review: findings, residual risks, and P3.4c are recorded below.
- [x] Commit: package prepared as `Bound Z-level tile projection work` on
      `zlevel/lighting-hardening`; remote verification follows the commit.

### Mini Review

- Finding: the former overlay issued one primitive submission for every visible
  lower tile. Retained chunk geometry reduces this to one submission per
  non-empty chunk without changing atlas variants or tint policy.
- Finding: transactional checks occur before tile insertion, so an exhausted
  chunk cannot leak a partially scanned row or aperture mask into the frame.
- Finding: normal and mapping modes can both run in one client frame without
  consuming one another's allowances; light projection remains a third pool.
- Residual risk: lower-floor walls still do not cast source-specific shadows into
  projected light. P3.4c owns that policy and its visual cost controls.
- Residual risk: headless tests validate inputs, ordering, geometry, and resource
  contracts but cannot judge final GL pixels, shadow shape, tint, or transient
  flashes. The canonical RGB fixture still requires real screenshot capture.
- Residual risk: approximate native intersecting-grid selection precedes the
  chunk budget. Extreme overlapping moving-grid counts require P8 profiling.
- Next package: implement bounded lower-floor shadow composition, capture the
  canonical visual regression set, and harden any remaining Z-unaware overlays.

## Completed Package: P3.4c1 External Point-Light Shadow Atlas Primitive

### Scope

- Add a renderer-owned polar shadow-atlas contract to WTZ Engine without
  embedding Content Z-level policy in Clyde.
- Allocate one native-format shadow row per ordered request and reuse Robust's
  existing occlusion-depth geometry and shaders for each point-light source.
- Group adjacent requests by world Z so dynamic occluder selection is rebuilt
  once per floor group rather than once per light.
- Preserve and restore the active-floor occlusion geometry and complete renderer
  state after external atlas rendering.
- Provide the same validated API in headless Clyde so Content integration and
  tests do not require a GL context.

### Acceptance Criteria

- The public request contains only world position, radius, and world Z; request
  order deterministically equals atlas row order.
- The atlas uses the native 512-sample polar format, repeat wrapping, and a
  depth-stencil target. Float-capable clients use `RG32F`; existing fallback
  clients use `RGBA8`.
- Adjacent equal-world-Z requests produce one floor-group upload while distinct
  groups remain independent.
- Invalid target dimensions, non-finite coordinates, and non-positive or
  non-finite radii fail before drawing.
- Empty and nullspace headless renders are safe and report no completed work.
- External rendering cannot leave Clyde on the requested floor or disturb the
  active-floor wall-bleed pass that follows Content overlays.

### Evidence

- `LightShadowMapTest` passes 7/7 contract cases covering row and contiguous
  group counts, exact atlas width, required capacity, invalid radii, and
  non-finite positions.
- `LightShadowMapApiTest` passes 1/1 integration case covering headless target
  creation, a safe nullspace render, and rejection of zero capacity.
- Complete engine client suites pass 37/37 unit tests and 137/137 integration
  tests after the API addition.
- `dotnet build RobustToolbox.slnx --no-restore --no-incremental` passes with
  zero errors and 189 established warnings after restoring the two previously
  absent WebView assets files.
- The staged engine diff contains exactly eight declared graphics and test
  files, and `git diff --cached --check` is clean.

### Decisions

- Expose an ordered shadow-atlas primitive, not a Z-level lighting subsystem.
  Content remains responsible for source selection, frame budgets, visibility
  apertures, attenuation, and deterministic degradation.
- Reuse `DrawOcclusionDepth` and the native polar shadow representation instead
  of maintaining CPU shadow polygons or a subtractive framebuffer pass.
- Make contiguous world-Z grouping part of the public performance contract.
  Callers that sort floor groups together avoid redundant dynamic-occluder
  uploads while preserving exact row identity.
- Snapshot occlusion query inputs every active-floor update and restore that
  geometry after the external pass. Restoring generic GL state alone is
  insufficient because native wall bleed consumes the selected geometry later
  in the same lighting pipeline.
- Keep shadow sampling and source/floor budgets out of c1. The primitive has no
  visual effect until P3.4c2 deliberately integrates it into Content.

### Completion Gate

- [x] Scope check: the diff is limited to a generic Clyde atlas API, native and
      headless implementations, validation, tests, and P3 documentation.
- [x] Invariant review: request/row order, contiguous world-Z grouping, native
      atlas format, nullspace behavior, active-floor geometry restoration, and
      full render-state restoration are covered.
- [x] Automated verification: 7/7 dedicated unit, 1/1 dedicated integration,
      37/37 complete client unit, 137/137 complete client integration, and a
      zero-error full engine solution build pass.
- [x] Performance evidence: occluder collection is performed once per adjacent
      floor group, while depth drawing remains one bounded row per request;
      Content-owned hard limits are the next package.
- [x] Documentation: the ownership boundary, format, ordering, restoration,
      tests, and deliberately pending visual integration are recorded here and
      in `Docs/ZLevelLighting.md`.
- [x] Dependency check: the paired WTZ Engine branch is
      `zlevel/lighting-shadows` at
      `32f197aee162589f73ae158c3de24154cce365eb`; the parent repository records
      that committed submodule pointer in the same package.
- [x] Git check: the eight-file staged engine diff passes
      `git diff --cached --check`; only the submodule pointer and declared docs
      are included in the parent package.
- [x] Mini review: findings, residual risks, and P3.4c2 are recorded below.
- [x] Commit: engine and parent packages are prepared for their paired remote
      branches and hash equality will be verified after each push.

### Mini Review

- Finding: the native active-floor occlusion state is semantic renderer state,
  not just GL bindings. Explicit restoration prevents external floor selection
  from changing the later native wall-bleed result.
- Finding: floor grouping amortizes dynamic occluder collection without forcing
  Content's source policy or world-Z ordering into the engine.
- Finding: strict request validation gives headless and GL implementations the
  same deterministic failure surface.
- Residual risk: the API is intentionally dormant. P3.4c2 must cap both shadow
  rows and floor-group uploads before any projected source can use it.
- Residual risk: mutable shader parameters cannot be shared across queued draws
  for different sources. P3.4c2 must use retained per-source shader instances or
  an equivalent immutable draw-data path.
- Residual risk: headless tests cannot validate polar depth values, soft-shadow
  penumbrae, tint, or transient frame artifacts. P3.4c3 owns real GL capture.
- Next package: select a deterministic bounded subset of shadow-casting projected
  lights, render grouped atlas rows, sample hard/soft variants, expose metrics,
  and degrade excess sources to the existing unshadowed projection.

## Completed Package: P3.4c2 Bounded Projected-Light Shadows

### Scope

- Select shadow rows only after an aperture-clipped projected light has been
  accepted, preserving the existing light batch regardless of shadow capacity.
- Add independent per-client-frame limits for shadow-light rows and world-Z
  occluder groups, shared by every automatic viewport in that frame.
- Render one retained power-of-two atlas per viewport through the P3.4c1 Clyde
  primitive and sample it with projected hard/soft shadow shaders.
- Keep one retained mutable shader instance per atlas row and mode so queued
  sources never observe another source's center, row, softness, or texture.
- Expose planned, rendered, fallback, current, maximum, and exhaustion counters
  through the debug overlay and `zlevelrendermetrics`.

### Acceptance Criteria

- Shadow requests preserve nearest-floor/UID batch order and equal-world-Z rows
  remain contiguous for one engine occluder upload per group.
- `castShadows: false` and the global client shadow toggle consume no row or
  floor-group work and do not count as budget fallback.
- A row or floor-group limit never removes a projected light. Excess sources use
  the unchanged unshadowed shader, with deterministic nearest-first priority.
- Row and group allowances are shared between viewport builds in one client
  frame and reset together for the next frame.
- Atlas height rounds up to a power of two, never exceeds the 1,024-row hard
  limit, grows only when required, and is released with viewport/overlay
  resources.
- Hard and soft variants retain mask rotation, vertical attenuation,
  transmission, color, energy, falloff, curve factor, and native polar shadow
  sampling.

### Evidence

- `ZLevelLightingShadowTest` passes 13/13 cases covering row and group ordering,
  atlas growth and hard-limit rejection, non-shadow casters, row fallback,
  group fallback, same-frame sharing/reset, and the global shadow toggle.
- The complete Content integration filter containing `ZLevel` passes 228/228
  with no failures or skips; structural unit tests pass 2/2.
- Generated 3-, 6-, and 10-floor server baselines pass 3/3 with 6,336 measured
  bytes, 100% warm boundary-cache hits, and zero evictions. Measured times were
  8.203, 14.388, and 28.182 ms respectively.
- The existing warmed projection allocation fixture remains green while now
  retaining and repopulating the shadow-request buffer.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes with
  zero errors and 711 established dependency, vulnerability, analyzer, and
  upstream-obsolescence warnings.

### Decisions

- Charge shadow work after complete aperture planning. Visibility/run limits
  still roll back a whole source, while shadow limits intentionally fall back
  to the already accepted unshadowed source.
- Use defaults of 64 rows and 8 floor groups per client frame, with hard maxima
  of 1,024 and 128. Both controls are local archived CVars; a server cannot
  raise client rendering cost.
- Start each viewport atlas at the smallest power of two that fits its current
  plan and retain its high-water mark. This bounds reallocations without paying
  the maximum allocation for every camera.
- Use separate hard and soft shader pools keyed by atlas row. Reusing one mutable
  shader for multiple queued sources would make all draws observe the final
  source's uniforms.
- Reuse the native shadow algorithms and packed depth format while retaining the
  projected shader's CPU-provided mask UV and attenuation data.
- Treat global shadow disablement as a quality choice, not an exhaustion. The
  projected light remains visible and no fallback or budget counter increments.

### Completion Gate

- [x] Scope check: the diff is limited to Content shadow planning, rendering,
      local CVars, diagnostics, shaders/prototypes, tests, and P3 documentation.
- [x] Invariant review: nearest-first order, contiguous groups, Z 0, authored
      world/local frames, non-shadow casters, global disablement, whole-light
      fallback, viewport sharing, and atlas lifetime are covered.
- [x] Automated verification: 13/13 dedicated, 228/228 cumulative Z-level
      integration, 2/2 structural unit, 3/3 baseline, and a zero-error clean full
      solution build pass.
- [x] Performance evidence: finite row/group limits, power-of-two capacity with
      a 1,024-row ceiling, retained per-viewport resources, retained request and
      geometry buffers, and live work/exhaustion counters are present.
- [x] Documentation: controls, ordering, grouping, shader ownership, fallback,
      metrics, tests, limitations, and P3.4c3 are recorded here and in
      `Docs/ZLevelLighting.md`.
- [x] Dependency check: WTZ Engine remains clean at
      `32f197aee162589f73ae158c3de24154cce365eb`; no additional engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices; generated baseline artifacts remain ignored and only declared
      package files are included.
- [x] Mini review: findings, residual risks, and P3.4c3 are recorded below.
- [x] Commit: package prepared as `Render bounded lower-floor light shadows` on
      `zlevel/lighting-hardening`; remote verification follows the commit.

### Mini Review

- Finding: shadow selection after complete source planning gives lighting two
  distinct fail-soft levels: expensive geometry failure rejects a source
  transactionally, while optional shadow exhaustion preserves its illumination.
- Finding: contiguous rows preserve the engine grouping optimization without a
  second sort or a Content-side occluder cache.
- Finding: per-row mutable instances make queued draw data stable while atlas,
  shader, request, run, and vertex resources all reuse their warmed capacity.
- Residual risk: headless shader/prototype tests cannot validate real polar depth
  pixels, penumbrae, tint, wall silhouettes, or transient artifacts. P3.4c3 must
  capture hard and soft GL output on the canonical RGB fixture.
- Residual risk: source-floor occluders cast these shadows; intermediate floors
  currently contribute vertical aperture clipping rather than a second lateral
  occlusion pass. The visual fixture must confirm this policy is legible.
- Residual risk: native active-floor and projected shadow pools are separately
  bounded. P8 stress profiling must measure their combined cost with overlapping
  cameras and pathological moving grids.
- Next package: run the canonical RGB fixture in a real client, capture Z 0/Z 1/
  Z 2 and mapping preview in hard and soft modes, add repeatable pixel checks,
  and harden any artifact found before the consolidated P3 gate.

## Completed Package: P3.4c3 Real-Client RGB Capture And Visual Hardening

### Scope

- Add a deterministic real-client command and PowerShell runner that capture the
  canonical RGB fixture on Z 0, Z 1, Z 2, and mapping preview in shadowless,
  hard-shadow, and soft-shadow modes.
- Analyze actual PNG pixels using fixed probes plus a grid-aligned RGB signature,
  and emit a machine-readable report whose failed checks fail the runner.
- Synchronize each synthetic camera floor with the server-attached player so PVS
  delivers the correct tiles, lights, and occluders before capture.
- Preserve lower-floor point-light and occluder render dependencies through
  bounded server PVS while ordinary hidden entities remain opening-clipped.
- Harden Content for the real sandbox and GLSL compiler paths exercised only by
  a graphical client.

### Acceptance Criteria

- One command launches an isolated local round, captures all 11 expected PNGs,
  writes `report.json`, terminates both processes, and returns non-zero for any
  setup, timeout, client, server, or pixel-check failure.
- Z 0, Z 1, and Z 2 shadowless baselines are nonblank and dominated by red,
  green, and blue respectively.
- Both hard and soft modes produce measurable occluder contrast on every floor,
  and each hard/soft signature pair differs by more than 0.0004 normalized RMS.
- Hard and soft mapping previews differ from their normal Z 1 captures, use the
  projected tile path, and retain the native active floor.
- The run observes actual external shadow-atlas work with no row/group fallback
  or exhaustion on the canonical fixture.
- Ordinary lower-floor entities remain PVS-hidden behind a closed floor while
  enabled lights and occluders needed by off-column apertures remain available.

### Evidence

- `ZLevelLightingCaptureAnalysisTest` passes 3/3 unit cases for RGB probes,
  signature difference/luminance, and translated-grid signature alignment.
- `ZLevelPvsKeepsLowerFloorLightingDependencies` passes and distinguishes one
  ordinary hidden entity from retained lower-floor light/occluder inputs.
- `ZLevelLightingShadowTest` passes 13/13; the cumulative Content integration
  filter containing `ZLevel` passes 229/229 with no failures or skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,216 measured bytes,
  100% warm boundary/gravity cache hits, zero evictions or budget exhaustion,
  and measured times of 4.202, 12.106, and 14.638 ms.
- Three consecutive real NVIDIA/OpenGL runs pass all 19/19 checks. The final run
  captures 11 frames in 14.714 seconds, reports hard/soft differences of
  0.000569, 0.000648, and 0.000603 for Z 0/Z 1/Z 2, renders 289 atlases for
  379 light rows and 379 floor groups, builds/renders mapping preview in 112/112
  frames, and renders 11,683 projected tiles without fallback or exhaustion.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passes in
  1 minute 47 seconds with zero errors and 711 established warnings. Both final
  PNG inspection and `git diff --check` pass.
- WTZ Engine's exact ImageSharp getter allowlist is committed and remotely
  verified at `b6051ff8c6d7b04638be2dbbbd0020b159906771`; the parent package points its
  submodule at that commit.

### Decisions

- Sample the authored 7 by 7 grid region in local coordinates and transform each
  point through the live grid world matrix. Whole-screen signatures were
  rejected because stars and UI dilute small projected-shadow differences and
  do not follow moving-grid framing.
- Use fixed shadow and clear probes at `(1.5, 4.5)` and `(5.5, 4.5)` plus an
  active-floor color probe at `(3.5, 2.5)`. The fixture authors symmetric
  apertures through both vertical boundaries and one blocker per floor.
- Require a non-zero 0.0004 normalized hard/soft difference. Two stricter 0.0005
  runs passed, but Z 1/Z 2 margins were narrow enough that 0.0004 better tolerates
  cross-driver raster variation without accepting identical output.
- Move the attached player authoritatively with the Debug-admin `zlevelset`
  command and wait for target-layer inventory. Moving only a client camera left
  server PVS correctly centered on Z 0 and produced black upper-floor captures.
- Treat enabled point lights and occluders as bounded render dependencies in
  PVS. Their effects may cross an aperture away from their own closed column;
  ordinary entities retain the stricter per-column visibility rule.
- Replace Content-exposed `ReadOnlySpan` members and selected `stackalloc` paths
  uncovered by the graphical sandbox with retained lists/scalars. Add only the
  exact ImageSharp pixel getter required by capture analysis to the engine
  sandbox, and rename the GLSL-reserved `packed` shader identifier.

### Completion Gate

- [x] Scope check: reviewed every Content, fixture, shader, runner, documentation,
      engine-sandbox, and submodule-pointer change as one P3.4c3 package.
- [x] Invariant review: covered Z 0, all authored floors, local/world conversion,
      moving-grid signature alignment, server authority, PVS limits, aperture
      channels, ordinary-entity privacy, and restoration after capture.
- [x] Automated verification: 3/3 analyzer, 13/13 shadow, 229/229 cumulative
      Content Z-level integration, 3/3 baseline, a zero-error clean full build,
      and a real-client 19/19 capture pass using the final threshold.
- [x] Performance evidence: retained baseline allocation/cache results and capture
      atlas, row/group, preview, duration, fallback, and exhaustion counters.
- [x] Documentation: recorded commands, architecture, fixture coordinates, PVS
      dependency policy, pixel thresholds, results, limitations, and next gate.
- [x] Dependency check: the minimal WTZ Engine sandbox allowlist change is clean,
      committed, pushed, and remotely verified before the parent pointer update.
- [x] Git check: `git diff --check` passes, both diffs were inspected, generated
      PNG/report/log artifacts remain ignored, and the engine SHA is verified.
- [x] Mini review: findings, residual risks, and consolidated P3 work are recorded.
- [x] Commit: engine is saved as `Allow safe ImageSharp pixel reads`; parent is
      prepared as `Harden Z-level lighting with real visual capture` on
      `zlevel/lighting-hardening`, with remote verification following the commit.

### Mini Review

- Finding: a real client exposed three integration assumptions that headless
  tests could not: Content sandbox restrictions, a reserved GLSL identifier,
  and server PVS remaining attached to the real player rather than a local eye.
- Finding: grid-aligned signatures turn the visual check into fixture evidence;
  they remain stable when camera framing, background stars, or grid transforms
  differ outside the authored region.
- Finding: the PVS exception is narrow and bounded. It restores all inputs needed
  for off-column projected shadows without making ordinary lower-floor entities
  visible through closed floors.
- Residual risk: captures currently cover one NVIDIA/OpenGL machine. Cross-vendor
  raster behavior and automated graphical CI remain P8 hardening work.
- Residual risk: retained lower structural inputs are bounded by XY range and
  floor distance, but dense lighting across overlapping viewers needs P8 server
  profiling.
- Residual risk: source-floor occluders cast lateral shadows while intermediate
  floors contribute aperture clipping only; this deliberate policy remains a
  future quality option rather than a P3 correctness defect.
- Next package: run the consolidated P3 completion gate across active rendering,
  caches, projection, budgets, tile composition, shadows, PVS, map persistence,
  and visual evidence before starting P4 vertical sound.

## Completed Phase Gate: P3 Z-Aware Lighting And FOV

### Scope

- Review the complete P3 delta from `03290c4a284` through `1d452ca7560` in WTZ
  Project and from `17f6c8f8d7` through `b6051ff8c6` in WTZ Engine as one
  rendering architecture rather than eight independently passing packages.
- Confirm ownership, local/world Z conversion, moving-grid behavior, cache
  invalidation, resource lifetime, server PVS inputs, persistence, deterministic
  budget degradation, observability, and real-client output agree across package
  boundaries.
- Re-run the native renderer, serialization, map, and replication suites against
  the exact paired revisions after the final P3.4c3 sandbox change.
- Record phase-level limits that remain deliberate P7/P8 work instead of silently
  extending P3 or blocking the independent P4 sound subsystem.

### Phase Acceptance Criteria

- One native viewport renders the active sparse grid layer selected by world Z;
  no per-floor map, viewport, eye, or duplicate tile-mesh pipeline is introduced.
- Visible lower tiles and projected point lights consume the same complete
  `Visibility` aperture stack, while their planning, rendering, and mapping
  preview retain independent frame budgets.
- Cache keys include grid, chunk, and local layer; tile, explicit boundary, map
  policy, and grid lifecycle changes invalidate only the relevant retained data.
- Moving and rotated `ZLevelFrame` grids resolve local layers to world Z before
  native light, occluder, tile, emitter, PVS, or capture decisions.
- Every expensive stage has a finite client/server limit and deterministic
  nearest-first behavior. Exhaustion either publishes only completed chunks/
  emitters or preserves an accepted unshadowed light; it never exposes partial
  geometry or an arbitrary blackout.
- External shadow rendering restores both generic renderer state and the active
  floor's semantic occlusion geometry before the native wall-bleed pass.
- Server PVS hides ordinary entities by column but retains bounded enabled light
  and occluder dependencies needed for off-column aperture effects.
- Sparse layer bounds, component Z, chunks, light fixtures, boundaries, and map
  configuration survive serialization, replication, and Content map loading.
- The canonical fixture proves actual RGB floors, hard/soft shadows, mapping
  preview, atlas use, budget headroom, and process cleanup through real OpenGL.

### Consolidated Evidence

- Content's complete `FullyQualifiedName~ZLevel` integration filter passes
  229/229 with no failures or skips; analyzer tests pass 3/3 and the full solution
  builds with zero errors.
- WTZ Engine's complete client suites pass 37/37 unit and 137/137 integration
  cases on `b6051ff8c6d7b04638be2dbbbd0020b159906771`.
- Focused engine persistence passes 4/4 `ZLevelSerializationTest` cases and 13/13
  `ZLevelMapTests`/`ZLevelChunkReplicationTest` cases.
- Generated 3-, 6-, and 10-floor baselines pass with 6,216 measured bytes, 100%
  warm boundary/gravity cache hits, zero evictions or budget exhaustion, and
  measured times of 4.202, 12.106, and 14.638 ms.
- Three consecutive graphical runs pass 19/19 checks. The final run captures 11
  PNGs in 14.714 seconds with nonblank RGB floors, hard/soft differences above
  0.0004, 289 atlas renders, 379 rows/groups, 11,683 projected tiles, and zero
  shadow fallback or exhaustion.
- Both repositories were clean and remotely verified at project
  `1d452ca75600d400a654a326571fa65a2e87aaac` and engine
  `b6051ff8c6d7b04638be2dbbbd0020b159906771` before this documentation-only gate.

### Architecture Review

- Finding: P3 preserves the roadmap's non-monolithic boundary. Robust owns native
  layer meshes, world-Z renderer filtering, spatial trees, GPU targets, and the
  generic shadow-atlas primitive; Content owns aperture policy, source ordering,
  attenuation, preview, budgets, diagnostics, and fixture semantics.
- Finding: sharing aperture chunks between tile/FOV and light composition avoids
  contradictory open columns without coupling their work allowances or failure
  policy.
- Finding: three degradation levels remain coherent: whole projected emitters or
  chunks roll back on incomplete geometry, optional shadows fall back to accepted
  illumination, and server PVS fails open for an entire refresh if its authority
  budget is exhausted.
- Finding: retained resources have explicit owners and lifetimes: sparse native
  meshes by grid/chunk/layer, aperture entries by Content cache, projection
  buffers by systems, and atlases/shader instances by viewport overlay cache.
- Finding: the real-client gate closes the headless-only gap without placing test
  policy in Clyde. Image analysis, server-view synchronization, and fixture
  thresholds remain Content tooling.

### Completion Gate

- [x] Scope check: reviewed all 42 project and 32 engine paths changed during P3;
      this gate changes only the resumable ledger.
- [x] Invariant review: Z 0, authored ranges, local/world frames, moving grids,
      server authority, visibility boundaries, PVS, renderer restoration, cache
      lifetime, and map persistence were checked across package boundaries.
- [x] Automated verification: 3 analyzer, 229 Content integration, 37 engine
      client unit, 137 engine client integration, 4 serialization, 13 map/
      replication, 3 baseline, and 19 real-client checks pass on paired SHAs.
- [x] Performance evidence: cache hit/allocation baselines, finite CVar clamps,
      current/maximum/exhaustion counters, graphical atlas/preview work, and
      process cleanup are recorded.
- [x] Documentation: ownership, pipeline, budgets, fixture, commands, evidence,
      deliberate limits, and P4 handoff are recorded in P3 docs and this gate.
- [x] Dependency check: WTZ Project and WTZ Engine are paired, clean, pushed, and
      remotely equal before the gate commit.
- [x] Git check: both trees are clean; generated PNGs, reports, logs, and baseline
      output remain ignored; the documentation diff passes `git diff --check`.
- [x] Mini review: findings, residual risks, and P4.1 are recorded below.
- [x] Commit: gate prepared as `Close the P3 lighting and FOV phase gate` on
      `zlevel/lighting-hardening`; remote verification follows the commit.

### Mini Review

- No blocking correctness finding remains inside P3's declared lower-floor
  lighting/FOV scope.
- Residual risk: only one NVIDIA/OpenGL driver is in the graphical matrix.
  Cross-vendor rendering and graphical CI remain P8 hardening targets.
- Residual risk: native intersecting-grid/tree broad phases run before Content
  budgets. Pathological overlapping moving grids and many simultaneous viewports
  require P8 CPU/GPU profiling.
- Residual risk: bounded PVS light/occluder retention is correct but may add
  network work in dense multi-floor scenes; P8 must measure that server cost.
- Residual risk: source-floor occluders provide lateral projected shadows while
  intermediate floors provide aperture clipping only. Additional intermediate
  lateral occlusion is an optional quality extension, not a correctness gap.
- Residual risk: normal player-facing projection intentionally reveals lower
  floors only; upward inspection remains mapping preview until a future gameplay
  policy explicitly authorizes it.
- Next package: P4.1 will define a specialized vertical-sound trace/portal
  contract and bounded cache derived from `Sound` boundary openings, without
  embedding audio attenuation or listener policy in `ZLevelTrace`.

P2.2 is split into independently gated subpackages:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.2a | Projectile/throw floor authority and lifecycle preservation | Complete |
| P2.2b | Bounded physical vertical trajectory and crossing policy | Complete |

P2.3 is split so blast topology, atmospheric consequences, and generated
presentation can each pass the completion gate independently:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.3a | Authoritative per-floor explosion topology and blast processing | Complete |
| P2.3b | Z-aware fire and atmospheric heat propagation | Complete |
| P2.3c | Generated effect placement and cross-floor presentation hardening | Complete |

P2.3c is split so persistent layered presentation and transient entity effects
remain independently reviewable:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.3c1 | Persistent decals, mapping operations, serialization, and rendering by floor | Complete |
| P2.3c2 | Generated entity/effect stamping and floor-authoritative camera shake | Complete |

## Completed Package: P2.3c2 Generated Entity And Effect Floor Authority

### Scope

- Capture source world Z before destruction, despawn, grenade content removal,
  or other lifecycle changes can erase the authoritative floor.
- Stamp destruction debris, construction outputs, stack splits, despawn
  replacements, entity effects, trigger and spawn-table results, butcher and
  refinement outputs, reform entities, AI debug spawns, and grenade payloads.
- Make `StampWorldZLevelPosition` preserve container inheritance: contained
  entities clear explicit floor state and follow their holder, while entities
  dropped onto a grid or map receive the requested world floor.
- Filter explosion camera-shake recipients by the world floors actually reached
  by authoritative grid and space topology, without changing sound behavior.
- Record camera-shake candidates, applications, and world-Z rejections in shared
  metrics and expose them through `zlevelmetrics`.

### Acceptance Criteria

- Every covered generated entity remains on source world Z across a frame whose
  local layer one maps to world Z six.
- Source floor is captured before source deletion or payload removal and applied
  before a grenade payload is thrown or fired.
- Predicted and server spawn paths, attached and map-coordinate triggers, entity
  tables, stack splits, and container fallback all preserve the same authority.
- Contained results have no stale explicit floor, follow their container after a
  floor change, and gain explicit floor state only if spawning falls back to the
  map.
- A closed explosion boundary rejects an overlapping upper-floor camera
  recipient; opening only the `Explosion` channel makes that same recipient
  eligible without moving it.
- Z 0 remains component-free where possible, moving frame origins use world/local
  conversion, and server topology remains authoritative.

### Explicit Deferrals

- P2.4 owns central direct, tool, alternate-click, pull, and remote interaction
  validation; this package does not add interaction policy to spawn systems.
- P4 owns vertical sound propagation. Explosion audio is intentionally unchanged.
- P8 owns exhaustive production-content auditing of rare raw spawn paths, live
  camera feel, and high-player-count profiling beyond the covered shared helpers
  and known generated-output consumers.

### Completion Gate

- [x] Scope check: the diff is limited to generated-entity floor authority,
      container inheritance, camera-shake filtering, metrics, tests, and this
      ledger.
- [x] Invariant review: Z 0, local/world conversion, frame origin five, moving
      frames, source deletion, container inheritance, prediction, server
      authority, and independent boundary channels were reviewed.
- [x] Automated verification: 10/10 dedicated package tests, 132/132 focused
      Z-level integration tests, 2/2 structural unit tests, and 3/3 generated
      stress baselines passed with no skips.
- [x] Performance evidence: generated placement remains event-driven; camera
      filtering reuses one retained `HashSet<int>`; the 3/6/10-floor measured
      workloads each allocate 6,336 bytes with 100% warm cache hits and zero
      evictions.
- [x] Documentation: covered producers, container behavior, topology authority,
      metrics, tests, limitations, and the P2.4 handoff are recorded here.
- [x] Dependency check: `RobustToolbox` is clean at
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea`; no WTZ Engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices; generated baseline and diagnostic artifacts remain ignored.
- [x] Mini review: findings and residual risks are recorded below.
- [x] Commit: package prepared as the isolated `Keep generated effects on their
      Z levels` commit on `zlevel/generated-effects`; remote verification follows
      the package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed with
  zero errors. Its 713 warnings are existing dependency, analyzer, vulnerability,
  and upstream obsolescence warnings.
- Dedicated coverage passes 10/10: seven generated-entity cases, one
  topology-authoritative camera-shake case, and two metric cases.
- Generated-entity tests cover destruction, timed despawn, predicted and server
  entity effects, inserted and dropped container results, stack splitting,
  direct and table triggers, and scattering/projectile grenade payloads.
- The final focused Z-level integration matrix passes 132/132 with no skips;
  structural unit tests pass 2/2 and generated stress fixtures pass 3/3.
- Final local Debug stress measurements retained the fixed allocation profile:

| Floors | Measured ms | Measured allocations | Boundary hit rate | Evictions |
| ---: | ---: | ---: | ---: | ---: |
| 3 | 8.096 | 6,336 B | 100% | 0 |
| 6 | 13.509 | 6,336 B | 100% | 0 |
| 10 | 24.611 | 6,336 B | 100% | 0 |

These timings are comparison evidence rather than release thresholds. The
fixture does not generate package effects, so it proves absence of unrelated
steady-state overhead rather than spawn throughput.

### Decisions

- Resolve and store source world Z before destructive lifecycle operations, then
  convert against the spawned entity's current frame only after its final parent
  or map attachment is known.
- Treat containment as vertical inheritance. A contained entity cannot occupy an
  independent physical floor; release paths already stamp the holder's current
  world Z when returning it to the map.
- Keep spawn authority in Content rather than intercepting generic engine spawn
  APIs. This avoids a Content dependency in WTZ Engine and leaves deliberately
  cross-floor producers free to express their own destination.
- Derive camera eligibility from the explosion's reached grid/space layer sets.
  Preserve vanilla planar range and recoil magnitude once a floor is eligible.
- Keep audio untouched until P4 can model attenuation and portal traversal as a
  dedicated sound concern.

### Mini Review

- Finding: the direct helper audit closed every `SpawnNextToOrDrop` and
  `SpawnInContainerOrDrop` consumer found in Content; the existing sharp/butcher
  path was already floor-aware and now benefits from container-safe stamping.
- Finding: the review caught ordering in shared stack splitting and moved source
  world-Z capture before count mutation.
- Finding: the first container test exposed that blindly stamping an inserted
  entity would freeze it on its old floor. Central container inheritance now
  protects both new and existing stamp callers.
- Finding: opening only an explosion boundary changes the same upper-floor player
  from rejected to accepted, proving that camera authority follows topology rather
  than a hard-coded floor comparison.
- Residual risk: camera shake is authorized per reached floor and retains vanilla
  2D range; it does not model room-level attenuation or structural vibration.
- Residual risk: raw specialized `Spawn` calls outside the audited generated
  output families may still require migration as their owning subsystem enters
  later roadmap phases.
- Next package: P2.4 will centralize server-side direct and remote interaction
  validation, beginning with a request-path inventory and explicit same-floor,
  boundary, and exception contracts.

## Completed Package: P2.3c1 Persistent Layered Decals

### Scope

- Give each decal an explicit grid-local Z level while keeping absent data and
  existing APIs compatible with Z 0.
- Carry decal layers through map serialization, component replication, mutation
  helpers, queries, tile changes, and grid splitting.
- Make fire scorching, crayons, spray painters, random decal spawners, mapping
  placement/removal, and the `adddecal` command target an explicit local floor.
- Remove decals only when their own floor tile is replaced, deconstructed, or
  removed.
- Render current-floor decals normally, lower decals only through open
  `Visibility` boundaries, and adjacent floors with the established mapping
  preview alpha convention.
- Copy decals with mapping floor-copy operations and preserve both source and
  target layers through save/load.
- Keep the legacy static map renderer deterministic by rendering Z 0 only until
  it gains an explicit floor-selection contract.

### Acceptance Criteria

- Equal XY positions can hold independent decals on multiple local floors.
- Placement fails when the requested layer has no floor even if Z 0 is solid.
- Removing or replacing a floor removes only that layer's decals.
- Runtime placement tools derive local Z from the user's world Z and the target
  grid frame; mapping tools use the explicitly selected local floor.
- Component state and map serialization preserve layers, while version-two map
  decals without a layer field load on Z 0.
- Copying a mapping floor duplicates its decals without changing the source.
- Current-floor presentation is opaque; lower floors require an open visibility
  stack; higher floors stay hidden outside adjacent-floor mapping preview.
- The package adds no per-tick server scan and preserves moving-grid frame
  semantics.

### Explicit Deferrals

- P2.3c2 owns debris, destruction outputs, trigger/spawn-table entities,
  despawn replacements, transient entity effects, grenade fragments, and
  camera-shake recipient filtering.
- P4 owns sound propagation, including upper-floor hotspot audio.
- Chemical reactions without `IZLevelTileReaction` remain safely skipped above
  Z 0; converting decal cleaning is part of later interaction/content work.
- Decal PVS remains keyed by XY chunks and can replicate decals from multiple
  floors in one visible chunk. P8 owns dense multi-floor bandwidth profiling
  and any layer-aware network partitioning justified by that evidence.
- The standalone map image renderer has no selected-floor input and therefore
  emits only Z 0 decals. A multi-floor export UI is outside this package.

### Completion Gate

- [x] Scope check: the diff is limited to persistent decal layer identity,
      producers/consumers, mapping copy, presentation, tests, and this ledger.
- [x] Invariant review: Z 0 compatibility, local/world conversion, frame origin
      5, moving grids, server-authoritative placement, and `Visibility`
      boundary checks were reviewed and covered where applicable.
- [x] Automated verification: 4/4 dedicated decal tests, 5/5 map-format tests,
      the complete 109/109 focused Z-level integration matrix, 2/2 structural unit
      tests, and the 3/3 stress runner passed.
- [x] Performance evidence: server mutation remains event-driven, measured
      stress allocations remain 6,336 bytes at 3/6/10 floors, and the unrelated
      stress fixture records no decal work; dense decal rendering/networking is
      explicitly reserved for P8 profiling.
- [x] Documentation: compatibility, ownership, mapping behavior, tests,
      limitations, and next-package boundaries are recorded here.
- [x] Dependency check: `RobustToolbox` is clean at
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea`; no WTZ Engine change is
      required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, generated baselines remain ignored, and the worktree contains
      only declared P2.3c1 files.
- [x] Mini review: findings, residual risks, and P2.3c2 ownership are recorded
      below.
- [x] Commit: package prepared as the isolated `Layer decals across Z levels`
      commit on `zlevel/generated-effects`; remote verification follows the
      package commit.

### Evidence

- `dotnet build SpaceStation14.slnx --no-restore --no-incremental` passed with
  zero errors. Its warnings are existing dependency, analyzer, and upstream
  obsolescence warnings.
- Dedicated integration coverage passes 4/4: floor-scoped validation/query and
  removal, server-to-client state replication, version-three map round-trip,
  and loading real version-two decals from `haunted.yml` as Z 0.
- The mapping/format matrix passes 5/5, including copying one source decal to a
  target floor and preserving both through save/load.
- The complete focused Z-level integration matrix passes 109/109 with no regressions;
  structural unit tests pass 2/2 and all generated stress cases pass 3/3.
- The local Debug stress snapshots retained the fixed 6,336-byte measured
  allocation profile:

| Floors | Warm-up ms | Measured ms | Measured allocations | Boundary hit rate | Evictions |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 9.487 | 7.075 | 6,336 B | 100% | 0 |
| 6 | 17.447 | 13.688 | 6,336 B | 100% | 0 |
| 10 | 34.468 | 24.822 | 6,336 B | 100% | 0 |

These timings are local comparison evidence, not release thresholds. The
fixtures contain no decals, so they establish absence of unrelated server cost
rather than GPU or dense decal-network throughput.

### Decisions

- Store local Z on each decal instead of cloning the entire existing chunk and
  PVS model per floor. This preserves old map shape, IDs, chunk dirtiness, and
  delta-state behavior while making layer identity explicit end to end.
- Bump compact decal output to version three. Version-one and version-two input
  continue through their existing paths; the missing `zLevel` field defaults
  to zero.
- Resolve world Z at the interaction/view boundary, then store grid-local Z.
  This keeps saved maps portable when grids have non-zero frame origins.
- Reuse `SharedZLevelVisibilitySystem` and its cached boundary decisions for
  lower-floor rendering instead of creating decal-specific visibility policy.
- Give mapping decal placement an explicit active-floor override so rapid floor
  changes do not depend on the network delay before the mapper entity moves.
- Snapshot source decals before clearing the target floor during mapping copy,
  then recreate them only after target tiles exist.

### Mini Review

- Finding: persistent scorch marks and authored decals now retain one floor
  from creation through query, mapping copy, replication, save/load, and draw.
- Finding: the review caught two mapping-only gaps before commit: erase requests
  still defaulted to Z 0, and floor copy omitted decals. Both now have direct
  coverage.
- Finding: version-two maps retain their prior Z 0 meaning, while new files
  serialize distinct layers without duplicating XY chunk storage.
- Residual risk: decal overlay filtering has deterministic authority/frame
  tests but no automated pixel screenshot; a live pass should inspect holes,
  mapping preview, remote eyes, and translated grids.
- Residual risk: one visible XY network chunk currently carries all of its decal
  layers. This is correct but may become bandwidth-heavy on decal-dense maps.
- Next package: P2.3c2 will stamp generated entities and transient effects with
  the source world floor, then make camera shake reject unrelated overlapping
  floors without changing P4 sound policy.

## Completed Package: P2.3b Z-Aware Fire And Atmospheric Heat

### Scope

- Reuse the existing per-floor `TileAtmosphere` simulation and `Atmosphere`
  boundary channel; do not create a second fire or gas model.
- Add explicit `(x, y, local Z)` hotspot APIs and make explosion heat target the
  atmosphere cell on the blast layer instead of suppressing upper-floor heat.
- Preserve horizontal and vertical hotspot spread through the atmosphere
  adjacency graph, including closed boundaries and independently authored
  channels.
- Store gas, hotspot, and compressed temperature overlay chunks per grid-local
  floor while preserving the established Z 0 chunk path.
- Limit custom gas-overlay replication to the world floors observed by each
  session's attached entity and view subscriptions.
- Render fire, visible gas, heat blur, and dangerous-temperature overlays from
  the viewport's authoritative world floor on translated Z-level frames.
- Add low-cost overlay-build observability and deterministic integration tests
  for authority, isolation, propagation, networking, and Z 0 parity.

### Acceptance Criteria

- An explosion on a non-zero floor can ignite and heat only that floor's gas;
  the overlapping Z 0 atmosphere and entities remain unchanged.
- Hotspot expose, query, and extinguish operations accept explicit Z-level tile
  coordinates without changing existing `Vector2i` Z 0 behavior.
- Fire and heat cross vertically only through a boundary open for
  `ZLevelBoundaryChannels.Atmosphere`; an opening for another channel is inert.
- Upper-floor hotspot events affect only entities on the matching world floor,
  including grids with non-zero frame origins.
- Overlay invalidation and chunk identity include local Z, so equal XY chunks
  on separate floors cannot overwrite one another.
- PVS-enabled sessions receive only layers represented by their current viewers;
  changing floor removes stale client layers and sends the newly viewed layer.
- All four atmosphere overlays select the current viewport world Z and retain
  their existing Z 0 visual and temperature-compression behavior.
- Work added to the atmosphere update remains proportional to invalidated tiles,
  and replication cost remains proportional to visible layers rather than every
  authored floor.

### Explicit Deferrals

- P4 owns vertical fire sound propagation. Upper hotspots remain silent until
  sound portals can attenuate and authorize them coherently.
- P2.3c owns Z-aware burnt decals, spawned debris, transient effect stamping,
  camera shake, and final presentation polish outside the gas overlay itself.
- P6 owns serialization and round-trip restoration of initialized upper-floor
  atmosphere cells and active hotspots.
- P8 owns production-scale profiling and any resumable or stricter networking
  budgets suggested by those profiles.

### Completion Gate

- [x] Scope check: the diff is limited to hotspot/heat authority, gas-overlay
      storage/network/rendering, observability, focused tests, and this ledger.
- [x] Invariant review: Z 0 compatibility, non-zero sparse layers,
      local/world frame conversion, translated frame origins, server authority,
      independent boundary channels, PVS toggles, and multiple viewers were
      considered and covered where applicable.
- [x] Automated verification: 12/12 package atmosphere tests, 14/14 explosion
      regression tests, 120/120 focused Z-level integration tests, 9/9
      explosion-prototype cases, 2/2 structural unit tests, the legacy gas
      overlay networking test, and the 3/3 stress runner passed.
- [x] Performance evidence: the deterministic two-tile/two-layer overlay
      rebuild is recorded below; unrelated stress workloads report zero overlay
      work and retain their fixed 6,336-byte measured allocation profile.
- [x] Documentation: contracts, metrics, PVS transition behavior, tests,
      limitations, and deferrals are recorded here.
- [x] Dependency check: `RobustToolbox` remains clean at its pinned revision;
      P2.3b requires no WTZ Engine commit or submodule-pointer update.
- [x] Git check: `git diff --check` passed, generated artifacts remain ignored,
      and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risks, and P2.3c ownership are recorded
      below.
- [x] Commit: package prepared as the isolated `Make fire and atmosphere
      overlays Z-aware` commit on `zlevel/fire-atmos-heat`; remote verification
      is recorded by the branch push that follows this commit.

### Evidence

- Pre-change baseline: all 7 existing `ZLevelAtmosTest` cases passed. The
  legacy gas-overlay networking case failed reproducibly because 60 live
  atmosphere ticks cooled the asserted 800 K mixture to approximately 620 K;
  this established a test-timing defect rather than a replication defect.
- The completed atmosphere matrix passed 12/12. New cases cover explicit
  hotspot expose/query/extinguish, Z 0 overload parity, upper-floor explosion
  heat, independent Projectile/Atmosphere openings, non-zero frame origins,
  full/delta component state, runtime PVS enablement, floor changes, and
  simultaneous attached/remote viewers.
- The final focused integration matrix passed 120/120. The dedicated explosion
  regression matrix passed 14/14, explosion prototype validation passed 9/9,
  focused structural unit tests passed 2/2, and the generated 3/6/10-floor
  stress runner passed 3/3.
- The legacy temperature compression/networking test passes after suspending
  simulation only for that networking-focused test. It still checks 400 K,
  rounding near 800 K, clamping at 1000 K, and dirty-threshold behavior.
- A non-incremental warning audit produced zero warnings in files touched by
  P2.3b. Existing dependency, vulnerability, and upstream warning lines remain.
- Client and server startup accept the expanded gas-overlay payloads with the
  matching serializer hash
  `2416F6408F56AAE3A82FA56D1828697E48038C2FEF4D6EB8D53EFD930F0EA765`.
- The deterministic local Debug overlay sample rebuilt 2 invalidated tiles in
  2 distinct chunks across Z 0 and one non-zero layer in 2.111 ms. This cold,
  test-host measurement is comparison evidence, not a production threshold.
- The unrelated 3/6/10-floor stress workloads recorded zero atmosphere-overlay
  updates, confirming that the new collector has no work when no overlay tile
  is invalidated. Their measured allocations remain 6,336 bytes.

### Decisions

- Reuse `TileAtmosphere`, its existing vertical adjacency, and the independent
  `Atmosphere` boundary channel. P2.3b adds coordinate authority and does not
  create a competing gas, heat, or fire simulation.
- Preserve the established Z 0 dictionaries and APIs. Non-zero local floors use
  sparse per-layer dictionaries, and every network chunk carries its local Z so
  equal XY coordinates cannot collide.
- Convert each viewer's world Z back into each intersecting grid's local frame
  before selecting chunks. Attached entities and all view subscriptions are
  unioned, allowing cameras on separate floors without sending every floor.
- Treat enabling PVS at runtime as an explicit protocol transition: clients
  clear atmosphere data by grid, then receive only currently viewed layers.
  The reset payload is proportional to grids rather than authored chunks.
- Resolve the viewport world floor through `ZLevelViewContextSystem` in fire,
  visible-gas, heat-blur, and dangerous-temperature overlays. Rendering remains
  a consumer of replicated state rather than an authority source.
- Record overlay rebuild metrics only on the simulation main thread and only
  when invalidations exist. Reused sets count distinct layers/chunks without
  adding per-tile allocations.
- Pause atmosphere simulation in the legacy compression test because that test
  owns byte encoding and replication, not thermal evolution.

### Mini Review

- Finding: hotspot state, explosion heat, event delivery, overlay identity,
  replication, and rendering now share one explicit floor from server mutation
  through client viewport selection.
- Finding: PVS cost scales with visible XY chunks multiplied by distinct viewer
  floors, while the common one-viewer case sends one floor per intersecting
  grid. Runtime enablement no longer retains stale all-floor snapshots, and
  leaving `InGame` now releases the session's pooled chunk/view caches.
- Finding: Z 0 keeps its original storage/API path and passed the complete
  focused regression and legacy temperature-compression checks.
- Residual risk: overlay layer selection has deterministic state/network/frame
  coverage but no automated pixel screenshot; final visual inspection under
  live fire, thermal goggles, remote cameras, and moving grids remains useful.
- Residual risk: production bandwidth and retained sparse-layer memory with many
  simultaneous remote viewers require P8 profiling before public-server claims.
- Residual risk: burnt decals and upper-floor fire audio remain intentionally
  suppressed. P2.3c and P4 own those policies respectively; P6 owns initialized
  atmosphere/hotspot save-load round trips.
- Next package: P2.3c will harden generated decal/debris/effect placement and
  cross-floor presentation without reopening the atmospheric authority model.

## Completed Package: P2.3a Authoritative Explosion Topology

### Scope

- Capture the authoritative world Z and structural frame before an explosive
  source can be deleted, and include both in queue-combination identity.
- Preserve the existing explosion flood's resistance, diagonal, grid/space, and
  intensity rules while indexing grid waves by local Z and space waves by world Z.
- Resolve vertical neighbors through reusable `ZLevelTrace` output and the
  independently authored `Explosion` boundary channel.
- Make airtight caches, grid edges, entity lookup, turf blocking, floor damage,
  and explosion overlay data address the floor actually reached by the wave.
- Add low-cost topology counters and focused integration coverage without moving
  damage, attenuation, tile breaking, or presentation policy into `ZLevelTrace`.

### Acceptance Criteria

- Z 0 explosions retain their established radius, resistance, tile damage, and
  queue-combination behavior.
- Overlapping entities, anchored blockers, airtight structures, and tiles on an
  unreached floor cannot affect or be affected by the explosion.
- Nearby explosions combine only when map, world Z, structural frame, prototype,
  and distance are compatible.
- An open `Explosion` boundary propagates the wave in deterministic vertical
  order with the same one-tile cost as a cardinal step; a closed boundary does
  not leak damage or topology.
- Translated, initially rotated, and world-Z-offset grids resolve local layers
  from the captured frame without trusting client-provided floor data.
- Work remains bounded by the existing explosion area/iteration limits and the
  shared trace budgets, with reusable crossing buffers and observable counters.
- Server blast visuals identify their grid-local or space-world floor so clients
  never draw reached tiles on an unrelated viewed floor.

### Explicit Deferrals

- P2.3b owns hotspot/atmospheric heat semantics and persistent fire propagation;
  P2.3a must prevent wrong-floor mutation but does not define a new fire model.
- P2.3c owns spawned debris/effect stamping and final in-game presentation
  polish beyond the authoritative explosion overlay payload.
- P4 owns audible vertical portal propagation; this package does not invent a
  temporary competing sound model.

### Evidence

- `dotnet build Content.IntegrationTests/Content.IntegrationTests.csproj
  --no-restore -consoleloggerparameters:ErrorsOnly` passed with zero errors.
  A non-incremental warning audit found no new analyzer warning after replacing
  direct entity-manager and generic transform access with the recommended
  proxies. Existing dependency, vulnerability, and upstream obsolescence
  warnings remain.
- The dedicated explosion topology matrix passed 14/14 with no skipped or
  dirty-disposed cases. It covers closed/open/unrelated boundary channels,
  two ordered crossings, Z 0 parity, different-floor queue identity, explicit
  map-coordinate Z, translated/rotated moving frames, deleted sources,
  wrong-floor airtight and turf blockers, non-zero tile damage, budget metrics,
  and floor-separated visual payloads.
- The complete focused Z-level integration matrix passed 115/115. Explosion
  prototype validation passed 9/9, focused structural unit tests passed 2/2,
  and the generated 3/6/10-floor stress runner passed 3/3 and wrote all three
  schema-versioned JSON artifacts.
- Client and server startup accepted the expanded admin-preview and explosion
  visual states with matching serializer hash
  `E4E5457AA5E12116377EEEACB700FB7983C1DF8991686FD371B8C6F8110F367B`.
- The local Debug three-floor blast fixture reached 3 grid layers, 3 space
  layers, and 87 tiles in 1.464 ms. It made 94 vertical queries, executed 76
  shared traces, and reused 18 per-explosion cached boundary results. This is
  comparison evidence on the existing Windows test host, not a release limit.
- `RobustToolbox` remained clean and its pinned revision did not change; P2.3a
  requires no WTZ Engine commit.

### Decisions

- Preserve vanilla explosion geometry: cardinal and vertical movement both cost
  two half-slope flood iterations, while diagonals retain their existing three
  half-step approximation.
- Key structural waves by `(grid, local Z)` and space waves by world Z. Convert
  only at explicit frame boundaries, and cache each normalized vertical boundary
  result for the lifetime of one topology build.
- Keep wrong-floor broadphase candidates out of the global processed set so a
  later reached floor can still process the same overlapping XY entity.
- Capture frame-local queue state before an explosive source can be deleted.
  A dead frame falls back to the captured map/world snapshot rather than
  preventing another valid grid lookup.
- Keep the coordinate overload as an explicit Z 0 compatibility API, but audit
  all server callers. Admin command/UI, smites, and bluespace lockers now supply
  entity or world-Z/frame authority.
- Carry every reached layer in the network visual payload and let the client
  select the current view's local/world layer. The explosion light is stamped at
  the epicenter world Z.
- Suppress atmosphere heating on non-zero local layers until P2.3b provides a
  real Z-aware hotspot contract; mutating the base atmosphere would be worse
  than an explicit temporary omission.

### Completion Gate

- [x] Scope check: the diff is limited to authoritative explosion topology,
      its admin callers, metrics, tests, and documentation.
- [x] Invariant review: Z 0 parity, local/world frames, translated and rotated
      grids, deleted sources, server authority, independent boundary channels,
      overlapping entities, and upper-floor tile history are covered.
- [x] Automated verification: 14/14 package, 115/115 focused integration, 9/9
      explosion prototype, 2/2 unit, and 3/3 stress cases passed.
- [x] Performance evidence: topology timing/output counters and local boundary
      cache reuse are captured above; budget exhaustion has a deterministic test.
- [x] Documentation: API authority, metrics, decisions, limitations, commands,
      and results are recorded here and in `Docs/ZLevel.md`.
- [x] Dependency check: no WTZ Engine change or submodule pointer update is
      required; the engine worktree is clean.
- [x] Git check: `git diff --check` passes; generated test artifacts remain
      ignored and no unrelated worktree changes are included.
- [x] Mini review: findings, residual risks, and P2.3b ownership are recorded
      below.
- [x] Commit: package prepared as the isolated `Make explosions Z-level
      authoritative` commit on `zlevel/explosion-topology`; remote verification
      is recorded by the branch push that follows this commit.

### Mini Review

- Finding: the blast wave now has one authoritative floor identity from queue
  through caches, topology, damage, tile mutation, and visual replication.
- Finding: a per-explosion normalized-boundary cache removed 18 of 94 trace
  executions in the representative three-floor fixture without bypassing
  `ZLevelTrace` policy.
- Finding: the audit removed the last production `MapCoordinates` explosion
  callers that silently defaulted entity-backed events to Z 0.
- Residual risk: non-zero tile updates currently use individual sparse-layer
  writes because the engine has no batch Z-tile API; very large upper-deck turf
  destruction needs profiling during P8 hardening.
- Residual risk: topology construction is still synchronous, matching vanilla.
  Extreme multi-floor blasts remain bounded but may need resumable generation if
  production profiles exceed the existing tick-time policy.
- Residual risk: upper-layer atmospheric heat is deliberately omitted, and
  sound, camera shake, generated debris, and other presentation consumers still
  need their owned vertical policies.
- Next package: P2.3b will define Z-aware hotspot/fire state and atmospheric heat
  propagation without reusing base-layer gas cells or inventing sound behavior.

## Completed Package: P2.1 Hitscan Trace Migration

### Scope

- Preserve authoritative same-floor hitscan selection while filtering physics
  candidates by world Z.
- Allow deliberate shots at visible lower-floor entities through a common
  structural frame and independently open `Projectile` boundaries.
- Keep range, target preference, damage, reflection, logging, and presentation
  in the weapon consumer instead of moving them into `ZLevelTrace`.
- Split visual effects by trace segment and stamp every client effect with its
  authoritative world Z.
- Capture collision-enabled allocation evidence and broad Z-level regressions.

### Acceptance Criteria

- Z 0 and same-floor hits retain container and
  `RequireProjectileTargetComponent` selection behavior.
- Colliders that overlap in XY but occupy another world Z cannot intercept a
  same-floor shot.
- A visible lower target on the same current grid frame can be hit only through
  open projectile boundaries and within XYZ max range.
- Hidden, above-floor, cross-frame, invalid, or over-budget requests fail
  conservatively without trusting a client floor value.
- Moving translated/rotated frames resolve current local and world Z.
- Networked muzzle, travel, and impact sprites carry the floor of their trace
  segment and are stamped on the client.

### Evidence

- `Content.IntegrationTests` builds with zero errors. Reported dependency,
  analyzer, and upstream obsolescence warnings are pre-existing; the four new
  transform-query analyzer warnings found during review were removed.
- The focused hitscan matrix passes 10/10 cases, including same-level parity,
  open and closed vertical shots, visibility authorization, above-floor denial,
  three-dimensional range, target-only obstacles, moving frames, trace budget
  exhaustion, and collision allocation capture.
- The existing weapon regression passes 1/1; the complete Z-level integration
  matrix passes 78/78; structural unit tests pass 2/2; all three generated
  3-, 6-, and 10-floor baselines pass.
- Client and server integration startup report the same serializer type hash
  after extending the hitscan visual payload with world Z.
- `git diff --check` passes apart from the repository's checkout line-ending
  notices.

The warmed collision-enabled Debug workload measured 512 requests at 447,608
managed bytes, or 874.23 bytes per request, and 5.759 ms total on the reference
machine. This is comparison evidence rather than a release threshold. P1's
buffered tile-only workload remains allocation-free; the remaining collision
allocation is inside Robust physics enumeration.

### Decisions

- Infer a cross-floor destination only from a server-resolved target entity.
  Clients do not provide world or local Z in the gun request.
- Reuse the renderer's current visibility contract: ordinary ranged targeting
  can select visible lower floors, while above-floor targeting remains disabled
  because those sprites are intentionally hidden.
- Recheck visibility on the server, then evaluate projectile passage separately
  so a visually open grate or shaft can still block weapon fire by policy.
- Use XYZ distance for cross-floor range and the post-recoil 2D direction for
  planar geometry. Same-floor shots retain their full legacy max-distance ray.
- Keep one reusable buffer in the event system. All trace output and effects are
  consumed before reflection can recursively raise another hitscan event.
- Preserve the existing reflection event contract. A reflected ray starts on
  the hit floor and remains two-dimensional until reflection can express a
  deliberate vertical destination.

### Completion Gate

- [x] Scope check: the diff contains only hitscan targeting, authoritative
      tracing, segmented presentation, focused tests, and related documentation.
- [x] Invariant review: Z 0, world/local frame conversion, moving grids, server
      authority, visibility authorization, and projectile boundaries are
      represented in implementation and tests.
- [x] Automated verification: build, 10/10 hitscan, 1/1 weapon regression,
      78/78 Z-level integration, 2/2 unit, and 3/3 baseline tests pass.
- [x] Performance evidence: the first warmed collision-enabled consumer capture
      is recorded without converting local Debug timing into a brittle limit.
- [x] Documentation: architecture, decisions, current targeting limits,
      performance, and commands are recorded in `Docs/ZLevelHitscan.md`.
- [x] Dependency check: existing Robust physics, networking, transforms, and
      sprite APIs are sufficient; no paired WTZ Engine revision is required.
- [x] Git check: `git diff --check` passes apart from checkout line-ending
      notices, generated artifacts remain ignored, and the diff is package-scoped.
- [x] Mini review: findings, residual risks, and P2.2 are recorded below.
- [x] Commit: save as `Migrate hitscan to Z-level traces` on
      `zlevel/hitscan-trace`; remote verification follows the commit.

### Mini Review

- Finding: P2 now has its first real gameplay consumer of the shared trace;
  cross-floor collision, boundary policy, metrics, and visuals use one ordered
  result without moving weapon behavior into the geometric primitive.
- Finding: same-XY colliders on other floors no longer leak into ordinary
  hitscan, even when no vertical shot is requested.
- Finding: visibility and projectile channels can intentionally disagree, and
  the server validates both before damage is emitted.
- Residual risk: the current gun request cannot encode the world Z of an empty
  clicked tile, so cross-floor shooting requires an entity target.
- Residual risk: one target entity selects the structural floor while the
  post-recoil direction selects planar geometry. Server visibility, range, and
  every traced boundary are authoritative, but stronger target-to-cursor
  binding should be considered when the input contract gains explicit view Z.
- Residual risk: upward shots are geometrically supported but deliberately not
  targetable until upper-floor FOV and rendering have a coherent player-facing
  policy.
- Residual risk: segmented visual payloads have serializer and compile coverage;
  exact muzzle/travel/impact appearance still needs an in-game manual pass.
- Next package: audit physical projectile and thrown-entity movement, define
  continuous crossing state without tracing an unbounded future trajectory,
  and preserve collision/prediction behavior on Z 0.

## Completed Package: P2.2a Projectile Floor Authority And Lifecycle

### Scope

- Stamp gun-spawned physical projectiles from a valid user or gun's world Z
  before their flight begins while preserving source-less authored floors.
- Apply the same source authority to normal throws without changing established
  horizontal throw timing, distance, landing, or gravity behavior.
- Keep impact presentation, embedding, and detaching on the projectile's
  authoritative world floor across displaced grid frames.
- Prove actual physics collision isolation between overlapping floors before
  adding any new vertical trajectory integrator.

### Acceptance Criteria

- A projectile fired on a non-zero local floor of a displaced frame starts on
  the source's effective world Z and cannot collide with an overlapping Z 0
  target.
- The same projectile collides normally when the target shares its world Z.
- Calls without a source do not erase an explicitly authored projectile or
  thrown-item floor.
- User throws acquire the user's world Z and retain all existing throw-state
  behavior.
- An embedded projectile inherits target movement between floors; detaching
  materializes the inherited world Z in the destination frame.
- Networked impact effects carry and apply the collision floor on the client.

### Evidence

- `Content.IntegrationTests` builds with zero errors and 90 existing dependency,
  analyzer, vulnerability, and upstream obsolescence warnings.
- The five lifecycle cases pass 5/5, covering sourced and source-less firing,
  real cross-floor/same-floor physics contacts, sourced and source-less throws,
  and embedding/detaching on a frame whose local zero maps to world Z 5.
- The combined projectile, weapon, embed, and item-throwing regression matrix
  passes 12/12.
- The complete focused Z-level integration matrix passes 83/83; structural unit
  tests pass 2/2; all three generated stress baselines pass.
- Client and server startup accept the extended impact event payload and report
  matching serializer type hashes.

### Decisions

- Resolve launch authority once at the existing lifecycle boundary. Do not add
  a per-tick floor copy or make projectile state depend on a later gun lookup.
- Preserve the existing `user ?? gun` launch-source contract. Stamp from that
  selected entity only when it exists; otherwise preserve the caller-authored
  floor instead of guessing from overlapping 2D geometry.
- Preserve vanilla throws as horizontal movement on one discrete floor. The
  existing Z-level solver suspends vertical gravity while an item is actively
  thrown and resumes it after landing; deliberate cross-floor throws belong to
  the explicit P2.2b trajectory contract.
- Clear an embedded projectile's explicit Z after parenting so target movement
  is inherited rather than copied once. Capture world Z before detach and stamp
  it only after the destination parent is known.
- Carry world Z in the impact network event instead of inferring a floor from
  client-side XY overlap.

### Completion Gate

- [x] Scope check: the diff contains only launch/throw authority, impact and
      embed lifecycle preservation, focused tests, and related documentation.
- [x] Invariant review: Z 0, local/world frame conversion, displaced grids,
      server launch authority, same-floor parity, and collision isolation are
      represented in implementation and tests.
- [x] Automated verification: build, 5/5 lifecycle, 12/12 affected regression,
      83/83 Z-level integration, 2/2 unit, and 3/3 baseline tests pass.
- [x] Performance evidence: no per-frame or per-physics-substep path was added;
      each stamp occurs once at an existing lifecycle transition, and all three
      stress baselines remain green. A trajectory benchmark belongs to P2.2b.
- [x] Documentation: authority, inheritance, limitations, tests, and the split
      from vertical trajectory are recorded in `Docs/ZLevelProjectiles.md`.
- [x] Dependency check: existing Robust transform, physics, parenting, and event
      APIs are sufficient; no paired WTZ Engine revision is required.
- [x] Git check: `git diff --check` passed apart from checkout line-ending
      notices; final tree review contains the ten intended source, test, and
      documentation files, while generated test and baseline artifacts remain
      ignored.
- [x] Mini review: findings, residual risks, and P2.2b are recorded below.
- [x] Commit: save as `Preserve Z-level projectile lifecycle` on
      `zlevel/projectile-traversal`; remote verification follows the commit.

### Mini Review

- Finding: physical projectiles and normal throws now have one authoritative
  floor from launch through impact, embedding, and detach.
- Finding: a real physics contact test proves that launch stamping composes with
  the engine's world-Z collision filter instead of merely checking component
  values.
- Residual risk: visible lower-floor input is already available for hitscan, but
  a physical projectile remains on the shooter's floor until P2.2b defines its
  vertical trajectory. This temporary mismatch must not be presented as working
  cross-floor ballistics.
- Residual risk: impact payload serialization is automated, while exact visual
  placement still needs a manual in-game pass.
- Residual risk: source-less callers are responsible for authoring the intended
  floor before launch; silently guessing from 2D overlap would be ambiguous.
- Next package: define an explicit, bounded trajectory component and validate
  boundary crossings at physics-substep geometry without introducing tunneling
  or changing ordinary same-floor projectile behavior.

## Completed Package: P2.2b Bounded Physical Vertical Trajectory

### Scope

- Add an opt-in, networked trajectory for physical projectiles and thrown items
  aimed at a server-resolved entity on another floor of the same grid frame.
- Keep horizontal integration and collision response in Robust physics while
  clipping each substep at ordered half-level crossings.
- Revalidate the current `Projectile` boundary and source-floor contacts before
  changing collision context at every crossing.
- Integrate normal guns, projectile spread, gun-thrown ammo, and manual hand
  throws without changing their same-floor paths.
- Add lifecycle-safe boundary impacts, adaptive thrown-item landing time,
  metrics, debug presentation, authored opening channels, and focused tests.
- Add the smallest paired engine API needed to batch newly-created contact
  processing after clipped movement.

### Acceptance Criteria

- Same-floor and Z 0 consumers retain their existing behavior and do not acquire
  trajectory state.
- A visible lower-floor target starts a bounded route only inside one valid grid
  frame; hidden, cross-frame, invalid, inactive, or over-budget requests reject.
- A fast body cannot switch floors before hard contacts from the source portion
  of its clipped substep are dispatched.
- Open crossings change floors in deterministic order; closed boundaries spend
  projectiles or stop thrown items on the source floor.
- Reflection, solver damping, range clamping, spread, translated/rotated frames,
  throw landing, and destination-floor replication preserve their native
  semantics.
- Contact discovery is batched once per substep containing crossings rather
  than once per projectile, and terminal metrics are recorded exactly once.

### Evidence

- `Content.IntegrationTests` builds with zero errors. Reported dependency,
  vulnerability, analyzer, and upstream obsolescence warnings predate P2.2b.
- The trajectory matrix passes 18/18. It covers open and closed two-floor
  routes, source-floor contact clipping, route rejection and invalidation,
  active-route retarget rejection,
  normal guns, projectile spread distance, manual and clamped throws, initially
  rotated frames translated after launch, direct thrown-item hits, reflection,
  authored channels, state replication, destination reconciliation, and a
  four-projectile contact-flush batch.
- The affected projectile, weapon, embed, and item-throwing regression matrix
  passes 12/12. The cumulative test filter containing `ZLevel` passes 101/101;
  structural unit tests pass 2/2; generated 3-, 6-, and 10-floor baselines pass
  3/3.
- WTZ Engine `Collision_Test` passes 7/7, including moved-body contact discovery
  and idempotent repeated flushes. The paired engine revision is
  `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea` on
  `zlevel/physics-contact-flush`.
- One substep containing four simultaneous crossings records four successful
  crossings and one contact flush. Launch preflight uses the already measured
  reusable trace buffer; steady updates reuse the trajectory system's crossing
  list instead of allocating a result per body.

### Decisions

- Treat vertical ballistics as an explicit route layered over Robust physics,
  not as a replacement three-dimensional physics engine. Same-floor shots and
  throws never opt in.
- Let the authoritative target entity select the destination floor while the
  post-clamp/post-recoil/post-spread displacement selects planar geometry. This
  keeps range and spread honest without trusting a client-provided Z value.
- Interpolate floor centers linearly and cross at half-level planes. For a route
  from local Z 2 to Z 0, crossings occur at one quarter and three quarters of
  the planar distance.
- Preflight with `ZLevelTrace`, then check each live boundary again at the
  current crossing tile. A deck edited while the projectile is in flight is
  therefore authoritative.
- Preserve only collinear, non-reversed solver damping after clipping. A hard
  contact, reflection, perpendicular response, or invalid body state cancels
  the route and leaves native physics in control.
- Flush contacts globally once for all crossings in the substep. A body-local
  engine API would require a larger physics ownership change and would not
  guarantee events for every moved proxy; metrics make the global cost visible.
- Preserve inertial velocity when a grid rotates during flight. Initially
  rotated frames and post-launch translation work; WTZ does not curve a shot to
  follow a suddenly rotating ship.

### Completion Gate

- [x] Scope check: the project diff is limited to bounded physical trajectories,
      their gun/throw consumers, projectile gravity and impact lifecycle,
      metrics, authored channels, tests, documentation, and the paired engine
      submodule revision.
- [x] Invariant review: Z 0 parity, local/world Z, displaced and rotated frames,
      post-launch translation, server target authority, boundary changes,
      collision ordering, reflection, and replication are represented.
- [x] Automated verification: build, 18/18 trajectory, 12/12 affected regression,
      101/101 cumulative Z-level integration, 7/7 engine contact, 2/2 unit, and
      3/3 baseline tests pass.
- [x] Performance evidence: reusable buffers remain in the hot path, four
      concurrent crossings share one global contact flush, counters expose all
      terminal causes and flushes, and the three stress baselines remain green.
- [x] Documentation: architecture, consumers, physics ordering, metrics,
      limitations, commands, and verification are recorded in
      `Docs/ZLevelProjectiles.md` and this ledger.
- [x] Dependency check: WTZ Engine revision
      `b768b2ac33d01d13dbc9ca7c0a0d092c345410ea` is committed, pushed, clean,
      and paired by the project submodule pointer.
- [x] Git check: engine and project `git diff --check` pass apart from checkout
      line-ending notices; generated TRX and baseline artifacts remain ignored,
      and final status contains only the declared package files.
- [x] Mini review: findings, residual risks, and P2.3 are recorded below.
- [x] Commit: save as `Add physical cross-level ballistic trajectories` on
      `zlevel/projectile-vertical-trajectory`; remote verification follows the
      commit.

### Mini Review

- Finding: physical projectiles and thrown items now traverse authored openings
  through real normal-input paths and preserve contacts on the correct floor.
- Finding: clipping plus the paired contact flush closes the high-speed window
  in which a projectile could otherwise leave its source collision context
  before contact events were raised.
- Finding: spread and manually clamped throws retain their true planar distance;
  the completion review caught and fixed the unit-vector spread case.
- Residual risk: an empty lower-floor tile does not identify a destination Z;
  physical traversal currently requires an authoritative target entity.
- Residual risk: upward targeting awaits coherent upper-floor FOV/input policy,
  and cross-grid routes remain intentionally unsupported.
- Residual risk: a rotating grid preserves inertial projectile velocity. If the
  body leaves its authored frame, WTZ records an invalid cancellation and native
  projectile motion continues on its current floor.
- Residual risk: the global contact flush is batched and instrumented but needs
  P8 scale profiling with many simultaneous players and moving grids.
- Residual risk: impact payload serialization and destination reconciliation are
  automated; exact visual placement still needs an in-game manual pass.
- Next package: split P2.3 into an authoritative explosion topology package
  first, followed by fire/heat propagation and generated-effect presentation,
  all as specialized consumers of `ZLevelTrace` and boundary channels.

## Completed Package: P4.1 Vertical Sound Portal Foundation

### Scope

- Define a shared vertical sound-portal record with stable grid-local identity,
  explicit/default classification, and current world projection.
- Cache `Sound` boundary decisions by grid, 16x16 chunk, and lower local Z in
  compact open/explicit bit masks, without changing audio playback behavior.
- Expose deterministic bounded queries with independent chunk, cold-build, and
  open-candidate budgets. Any failure rolls back only this call's appended data.
- Add targeted tile, provider, map-policy, and grid-lifecycle invalidation,
  bounded FIFO retention, process-local metrics, tests, and architecture docs.

### Acceptance Criteria

- Cache construction consults only the authoritative `Sound` boundary channel;
  forced Sound policy cannot alter Visibility, Projectile, or another channel.
- Queries retain deterministic layer/chunk/tile order across Z 0, negative chunk
  coordinates, authored openings, and translated or rotated Z-level frames.
- A budget failure is distinguishable from an empty successful result and never
  leaks a partial portal set into a caller-owned result list.
- Moving a frame reprojects world coordinates without rebuilding local topology;
  topology edits invalidate the smallest applicable retained region.
- Retention is finite and hot single-portal queries do not grow allocations.

### Evidence

- The focused `ZLevelSoundPortalCacheTest` matrix passes 4/4 with no skips. It
  covers default/explicit classification, channel independence, server/client
  parity, Z 0, negative chunks, deterministic ordering, all three budget
  failures and rollback, capacity-one eviction, exact invalidation, grid
  termination, moving-frame reprojection, and hot allocation.
- The complete Content `FullyQualifiedName~ZLevel` integration filter passes
  233/233 with no failures or skips. Content's structural and visual-analysis
  `ZLevel` unit filter passes 5/5.
- The 3-, 6-, and 10-floor generated stress baselines pass 3/3 with 6,336
  measured bytes each, 100% warmed boundary/gravity cache hits, and no budget
  exhaustion. Measured times were 12.171, 13.834, and 25.692 ms.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental -m:1`
  succeeds with zero errors and 700 established dependency, analyzer,
  vulnerability, and upstream-obsolescence warnings.
- The focused cache run builds one 256-boundary chunk in 0.394 ms, performs
  3,050 queries with a rounded 100% hit rate after warmup, and allocates zero
  bytes across 1,000 repeated hot queries.

### Decisions

- Keep sound topology in a specialized shared cache. `ZLevelTrace` remains the
  geometry primitive, while sound route choice, transmission, listeners, and
  playback retain their own policy and metrics.
- Never enumerate a global portal graph: default-open empty space is unbounded.
  P4.2 must derive finite search bounds from endpoints, range, and route budgets.
- Store default and explicitly forced openings separately so authored grates,
  vents, and shafts can receive later transmission policy without re-resolving
  providers. Forced close remains authoritative.
- Cache only local topology. World XY and world Z are resolved at query time so
  moving, rotated, and frame-origin-shifted grids do not churn retained chunks.
- Preserve one logical Robust `AudioComponent` per PVS emission. Later listener
  reachability and apparent direction must not duplicate playback entities.

### Completion Gate

- [x] Scope check: the diff is limited to the shared portal contract/cache,
      one CVar, focused integration coverage, and P4 documentation/ledger.
- [x] Invariant review: Z 0, negative coordinates, local/world frame conversion,
      moving grids, server/client parity, authority, and channel isolation pass.
- [x] Automated verification: 4/4 focused, 233/233 cumulative integration, 5/5
      Content unit/analyzer, 3/3 stress baseline, and a zero-error clean build.
- [x] Performance evidence: cold build duration, hot hit rate/allocation, finite
      FIFO capacity, queue compaction, and existing stress metrics are recorded.
- [x] Documentation: ownership, query semantics, coordinates, invalidation,
      metrics, commands, current audio boundary, and limitations are recorded in
      `Docs/ZLevelSound.md` and this entry.
- [x] Dependency check: no engine edit is required; WTZ Engine remains clean and
      pinned at `b6051ff8c6d7b04638be2dbbbd0020b159906771`.
- [x] Git check: generated baselines remain ignored, both repository trees were
      inspected, and `git diff --check` is required immediately before commit.
- [x] Mini review: no blocking finding remains; the residual work below is owned
      by P4.2/P4.3 rather than hidden inside the cache foundation.
- [x] Commit: prepared as the isolated `Cache bounded vertical sound portals`
      commit for the WTZ vertical-sound branch and its remote.

### Mini Review

- Finding: client and server derive identical portal topology from replicated
  Sound boundary authority, and moving-grid transforms do not invalidate it.
- Finding: independent caller-owned budgets make exhaustion explicit and retain
  previously supplied result entries; cache eviction changes cost, not behavior.
- Residual risk: no sound is audible across floors yet. Routing, attenuation,
  listener/PVS authorization, apparent direction, and diagnostics are P4.2/P4.3.
- Residual risk: acoustic-medium and vacuum behavior, cross-grid connectivity,
  and multiplayer-scale profiling remain intentionally undefined.
- Next package: P4.2 bounded multi-portal route selection and transmission,
  including crossing/range limits, deterministic tie-breaking, attenuation, and
  explicit sealed/vacuum behavior.

## Completed Package: P4.2 Bounded Vertical Sound Routes

### Scope

- Define shared endpoint, options, budget, result, status, and metrics contracts
  for one vertical acoustic route without coupling them to playback entities.
- Add a server-authoritative solver that derives finite portal bounds from
  endpoints and range, then chooses one monotonic adjacent-floor path.
- Rank paths by geometric travel and cumulative transmission loss, with stable
  equal-cost tie-breaking and current moving-frame projection.
- Sample Z-aware atmosphere pressure on the server, block vacuum and missing
  medium, and expose transmission plus decibel loss.
- Add independent route budgets/CVars, explicit failure states, rollback,
  focused tests, architecture documentation, and performance counters.

### Acceptance Criteria

- Same-floor queries preserve native Euclidean behavior and do not require an
  atmosphere medium, so Z 0 and existing audio remain compatible.
- A vertical result contains exactly one portal per adjacent boundary in travel
  order and is identical for equal topology in either direction.
- Search space and all expensive work are finite; every budget failure is
  distinguishable and never leaves a partial path in caller-owned results.
- Missing portal topology is reported before medium state. Pressurized routes
  attenuate predictably, while vacuum or missing atmosphere blocks traversal.
- Grid translation, rotation, and world-Z origin changes preserve local route
  choice while returned portal world coordinates stay current.

### Evidence

- `ZLevelSoundRouteTest` passes 4/4. It covers lower-loss ranking, equal-cost
  deterministic ties, upward/downward ordering, moving frames, same-floor
  compatibility, sealed layers, vacuum, pressure attenuation, CVar clamps,
  rollback, and all five work-budget failures.
- The combined P4.1/P4.2 `FullyQualifiedName~ZLevelSound` matrix passes 8/8
  with no failures or skips. The complete Content Z-level integration filter
  passes 237/237, and Content's structural/capture-analysis filter passes 5/5.
- The generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured
  bytes each, zero warmed boundary/gravity misses, zero PVS budget exhaustions,
  and measured times of 6.784, 13.052, and 26.142 ms.
- `dotnet build SpaceStation14.slnx --no-restore --no-incremental -m:1`
  succeeds with zero errors and 700 established dependency, analyzer,
  vulnerability, and upstream-obsolescence warnings.
- The focused route workload completes 2,026 successful queries and 28,364
  evaluated edges in 104.139 ms total. After warmup, 1,000 repeated route
  lookups allocate zero managed bytes on the test machine.

### Decisions

- Keep route policy server-side because atmosphere and listener authorization
  are authoritative. Shared code owns only immutable contracts and portal
  topology remains in the P4.1 shared cache.
- Use an ordered monotonic DAG instead of a global portal graph. One route may
  move horizontally between portals but crosses each required boundary once,
  making cycles impossible and work straightforward to budget.
- Compute effective distance as geometric distance plus `-ln(transmission)`
  loss distance. Preserve the first P4.1 portal order on equal effective cost.
- Require both endpoints to share one grid-local frame. Cross-grid routing is
  explicitly rejected until docking or another physical connection contract
  can define valid transitions between moving grids.
- Preserve native same-floor sound even in vacuum. Vertical sound defaults to
  current pressure-aware policy and retains pressure only for one lookup.
- Report missing topology before pressure state, avoiding wasted atmosphere
  samples and keeping sealed geometry distinct from vacuum.

### Completion Gate

- [x] Scope check: the diff is limited to shared route contracts/CVars, one
      server solver, focused integration tests, and P4 documentation/ledger.
- [x] Invariant review: Z 0 compatibility, local/world frames, moving and
      rotated grids, server authority, Sound-channel isolation, sealed topology,
      vacuum, and caller-owned rollback were reviewed and tested.
- [x] Automated verification: 4/4 route, 8/8 combined sound, 237/237 cumulative
      Content integration, 5/5 Content unit/analyzer, 3/3 baseline, and a
      zero-error clean solution build pass without skips.
- [x] Performance evidence: finite CVar/hard clamps, per-query budgets, route
      counters/timings, baseline metrics, and zero-byte warmed allocation are
      recorded above and in `Docs/ZLevelSound.md`.
- [x] Documentation: solver bounds, DAG ordering, formulas, medium policy,
      statuses, CVars, current audio boundary, verification, and limits are
      recorded in `Docs/ZLevelSound.md` and this entry.
- [x] Dependency check: no engine edit is required; WTZ Engine remains clean and
      pinned at `b6051ff8c6d7b04638be2dbbbd0020b159906771`.
- [x] Git check: generated baselines and test results remain ignored, both
      repository trees are inspected, and `git diff --check` is required
      immediately before commit.
- [x] Mini review: no blocking finding remains; playback integration and the
      deliberately limited policies below are assigned to P4.3 or later.
- [x] Commit: prepared as the isolated `Route vertical sound through bounded
      portals` commit for the WTZ sound-routing branch and its remote.

### Mini Review

- Finding: topology, medium, and resource exhaustion now have separate explicit
  outcomes; route failure cannot be mistaken for valid silence or leak a
  partial portal list.
- Finding: the hot solver reuses bounded scratch storage, resolves symmetric
  ties deterministically, and remains stable across moving-frame reprojection.
- Residual risk: no cross-floor audio is audible until P4.3 connects successful
  routes to listener/PVS authorization and apparent client source placement.
- Residual risk: per-floor segments are Euclidean, cross-grid routes are
  rejected, and transmission is class-level rather than material/frequency
  specific. Those limits are explicit and do not weaken route authority.
- Residual risk: the focused timings are Debug/local results; multiplayer scale,
  retained scratch high-water behavior, and long-running metrics belong to P8.
- Next package: P4.3 integrates one logical Robust emission with routed listener
  authorization, apparent direction, PVS-safe lifecycle, debug/admin metrics,
  and end-to-end sealed/vacuum/moving-grid tests.

## Completed Package: P4.3a Positional Audio Post-Processing Hook

### Scope

- Add one client-side WTZ Engine callback after native positional stream
  processing without replacing Robust audio startup, tracking, map, range, or
  occlusion behavior.
- Invoke the callback after native early-mute paths so authorized vertical audio
  can be restored by later Content policy while unauthorized audio stays muted.
- Cover the real parallel `FrameUpdate` path with a headless integration test.

### Acceptance Criteria

- With no subscriber, the original positional audio path and all return
  conditions remain behaviorally unchanged.
- One subscriber receives the initialized `AudioComponent` after default
  processing, including a stream muted because its map differs from the eye.
- The extension does not create, replace, restart, or duplicate an audio source.

### Evidence

- Focused `AudioStreamPostProcessingTest` passes 1/1 and proves callback order
  after a default early mute using the real parallel client audio update.
- The complete Robust client integration suite passes 138/138 with no skips;
  the complete Robust client unit suite passes 37/37.
- `dotnet build Robust.Client/Robust.Client.csproj --no-restore -m:1` succeeds
  with zero errors and 76 established warnings.
- The parent `Content.Client` project builds against the pinned engine commit
  with zero errors and 452 established warnings.

### Decisions

- Expose a post-processing callback rather than copying Robust's private stream
  algorithm into Content. Native behavior remains the single implementation.
- Keep the callback client-only and mutation-oriented: P4.3b owns authoritative
  per-session routes, while P4.3c will only apply received presentation data.
- Retain the existing single-subscriber rule and parallel execution contract of
  `ProcessStreamOverride`; subscribers must use thread-safe immutable state.

### Completion Gate

- [x] Scope check: one engine audio method was split without logic changes, one
      public callback was added, and one focused engine test was introduced.
- [x] Invariant review: startup, source identity, native map/range behavior,
      callback ordering, early mute, and the no-subscriber path were reviewed.
- [x] Automated verification: 1/1 focused, 138/138 client integration, 37/37
      client unit, and a zero-error Robust.Client build pass without skips.
- [x] Documentation: ownership, parallel/single-subscriber contract, current
      boundary, evidence, and residual work are recorded here and in
      `Docs/ZLevelSound.md`.
- [x] Dependency check: WTZ Engine branch `zlevel/sound-playback` is committed
      and pushed at `3794b33b6c0e9fa5bca7feeaba5edfcd11f0ddfb`.
- [x] Git check: engine and parent diffs pass `git diff --check`; generated test
      output remains ignored and the dependency pointer is the only engine link.
- [x] Mini review: no blocking finding remains; no Content system subscribes to
      the callback before server authorization and client policy are ready.
- [x] Commit: prepared as paired engine `Expose positional audio
      post-processing` and parent dependency/ledger commits.

### Mini Review

- Finding: the hook runs after default processing and can therefore adjust a
  muted stream without reimplementing native lifecycle or creating another one.
- Finding: existing audio remains byte-for-byte on the native path when there is
  no subscriber, and the full client suites retain their prior behavior.
- Residual risk: the callback executes on audio worker threads; P4.3c must swap
  immutable presentation snapshots and avoid entity-manager mutation there.
- Residual risk: cross-floor audio is still intentionally silent. Authorization,
  PVS parent-chain handling, snapshot lifecycle, direction, and attenuation are
  P4.3b/P4.3c.
- Next package: P4.3b adds bounded server authorization and per-session acoustic
  presentation snapshots integrated with Z-level PVS.

## Completed Package: P4.3b Bounded Server Sound Authorization

### Scope

- Add a server-authoritative playback coordinator that evaluates existing
  positional `AudioComponent` entities against every exact session viewer.
- Preserve native same-floor/global playback while requiring a pressure-aware,
  same-grid P4.2 route for every cross-floor presentation.
- Publish changed-only replacement snapshots containing the listener-side
  portal, geometric route distance, and transmission needed by P4.3c.
- Integrate denied cross-floor audio with PVS without changing the established
  visual fail-open policy, and centralize Robust's existing recipient filter so
  Content authorization cannot disagree with replication.

### Acceptance Criteria

- One logical emission keeps one audio entity and one playback timeline; no
  per-floor or per-listener audio entity is spawned.
- Candidate and viewer iteration is deterministic. Exact viewers, native
  included/excluded recipient filters, pressure, range, grid, and route budgets
  are all server-authoritative.
- Same-floor audio remains native even in vacuum. Cross-floor audio fails closed
  for vacuum, sealed boundaries, incompatible grids, exhausted budgets, and
  missing exact viewers.
- Successful authorization makes only the required transform parent chain
  visible. Denied audio remains culled even when visual PVS work fails open.
- Snapshot replacement, unchanged-snapshot suppression, empty clearing, and
  session disconnect cleanup cannot retain stale authorization.
- The warmed P0 stress baseline gains no allocation or cache-miss regression.

### Evidence

- Focused `ZLevelSoundPlaybackTest` passes 3/3 with real server atmosphere,
  looping positional audio, server/client replication, parent-chain PVS, route
  revocation, same-floor fallback, both aggregate budget clamps, and visual-PVS
  exhaustion.
- The combined P4.1-P4.3b `FullyQualifiedName~ZLevelSound` matrix passes 11/11
  with no failures or skips; the complete Content Z-level integration filter
  passes 240/240 and the Content unit/analyzer filter passes 5/5.
- Robust's focused recipient-filter test passes 1/1. The complete shared unit
  suite passes 447/447 and shared integration suite passes 1,026/1,026 without
  skips.
- The 3-, 6-, and 10-floor baselines pass 3/3 at the unchanged 6,336 measured
  bytes, zero warmed boundary/gravity misses, zero PVS budget exhaustions, and
  6.748, 13.118, and 20.628 ms respectively.
- A clean `SpaceStation14.slnx` build succeeds with zero errors and 700
  established warnings.

### Decisions

- Authorize the existing PVS audio entity instead of spawning relay entities.
  Playback identity, seek position, lifetime, and native same-floor behavior
  therefore remain owned by Robust.
- Send complete changed-only snapshots rather than incremental grants. This
  makes revocation and out-of-order entity arrival simple for P4.3c and bounds
  retained state to the latest refresh.
- Record the final listener-side portal, total geometric distance, and route
  transmission. The client receives presentation data, not authority to solve
  current atmosphere or topology itself.
- Keep aggregate route and presentation budgets independent of each P4.2 route
  budget. Exhaustion rejects remaining cross-floor work while retaining only
  the exact audio/viewer pairs whose routes already succeeded in that refresh.
- Preserve audio culling when visual PVS exhausts its budget. When engine PVS is
  disabled, P4.3c must still mute any cross-floor stream lacking exact
  authorization because server culling is intentionally unavailable there.
- Centralize recipient filtering in WTZ Engine as a behavior-preserving helper;
  the original replication event now calls the same API as Content authorization.

### Completion Gate

- [x] Scope check: the parent diff is limited to sound authorization, PVS
      integration, contracts/CVars, focused integration coverage, docs, and the
      paired engine dependency.
- [x] Invariant review: Z 0/same-floor compatibility, world/local frames,
      moving grids, exact viewers, server authority, pressure, parent chains,
      single-emission identity, and Sound boundary channels were reviewed.
- [x] Automated verification: 3/3 focused playback, 11/11 combined sound,
      240/240 Content integration, 5/5 Content unit/analyzer, 447/447 and
      1,026/1,026 engine shared suites, 3/3 baselines, and a zero-error clean
      solution build pass.
- [x] Performance evidence: deterministic aggregate budgets have hard clamps;
      warmed stress baselines retain 6,336 bytes and zero relevant cache misses.
- [x] Documentation: ownership, payload, budgets, lifecycle, intermediate
      client limitation, test evidence, and P4.3c obligations are recorded here
      and in `Docs/ZLevelSound.md`.
- [x] Dependency check: WTZ Engine branch `zlevel/sound-playback` is committed
      and pushed at `87e2732606b5df3f1c035d0089d053cdf333eb77`.
- [x] Git check: engine and parent diffs pass `git diff --check`; generated
      output remains ignored and no unrelated change is included.
- [x] Mini review: no server-side blocker remains; client worker-thread state,
      safety mute, direction, attenuation, and diagnostics are isolated to P4.3c.
- [x] Commit: saved as paired engine `Centralize audio recipient filtering` and
      parent `Authorize routed vertical sound per session` commits on the WTZ
      sound-playback branches.

### Mini Review

- Finding: P4.3b now answers the security question independently of client
  presentation: exactly which audio/viewer pair may cross a vertical boundary.
- Finding: the single native audio entity remains authoritative and is exposed
  only after a bounded route succeeds; successful visual fallback cannot expose
  denied cross-floor audio while Z-level PVS is active.
- Finding: replacement snapshots are stable and suppress duplicate network
  events, while vacuum, movement to the same floor, or disconnect clears stale
  grants.
- Residual risk: an authorized P4.3b stream still uses native XY presentation
  until P4.3c consumes the portal/distance/transmission snapshot.
- Residual risk: the engine callback runs on audio worker threads, and engine-PVS
  disablement removes server culling. P4.3c must atomically publish immutable
  state and fail closed without an exact authorization.
- Next package: P4.3c adds client-side apparent portal direction, route
  attenuation, safety muting, metrics/debug diagnostics, and end-to-end
  lifecycle/threading coverage.

## Completed Package: P4.3c Client Vertical Sound Presentation

### Scope

- Consume P4.3b replacement snapshots on the client and validate every grant
  against the exact current audio, viewer, grid, map, and local floors.
- Present one existing Robust positional stream at its final listener-side
  portal while applying complete-route attenuation, transmission, and portal
  occlusion.
- Safety-mute every replicated cross-floor stream without an exact valid grant,
  including when engine PVS is disabled, while leaving same-floor and global
  audio on the native path.
- Keep the parallel audio callback free of ECS and physics queries through
  atomically published, double-buffered policy snapshots.
- Publish client/server sound diagnostics and close the P4 phase gate.

### Acceptance Criteria

- No relay or duplicate audio entity is created; source identity, playback
  position, lifetime, and native startup remain owned by Robust.
- Snapshot payloads use stable grid-local portal coordinates and local Z. A
  translated or rotated moving grid reprojects direction without a new grant.
- Invalid, stale, wrong-viewer, wrong-grid, wrong-floor, non-finite, out-of-range,
  or non-positive-transmission presentations fail closed.
- Authorized gain replaces portal-only distance attenuation with complete-route
  attenuation, applies transmission, and handles every Robust attenuation mode.
- The audio worker reads immutable policy only. Occlusion, transforms, entity
  resolution, and snapshot validation execute on the client main thread.
- Revocation and movement to the listener floor remove the managed policy and
  restore Robust's native same-floor processing.

### Evidence

- `ZLevelSoundPlaybackTest` passes 5/5. The two P4.3c cases exercise actual
  server atmosphere and routing, snapshot replication, exact client policy,
  callback execution, moving-grid translation/rotation, engine-PVS-off safety
  muting, and native same-floor return.
- `ZLevelSoundPresentationTest` passes 4/4 for linear route replacement,
  transmission-only playback, centered portals under unclamped inverse
  attenuation, and invalid/fully attenuated fail-closed behavior.
- The combined P4.1-P4.3c sound matrix passes 13/13. The complete Content
  Z-level integration matrix passes 242/242 and the Content Z-level unit and
  analyzer matrix passes 9/9, all without failures or skips.
- Robust's focused callback test passes 1/1. Complete client unit/integration
  suites pass 37/37 and 138/138; shared unit/integration suites pass 447/447
  and 1,026/1,026.
- The 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes, 100%
  warmed boundary/gravity hits, zero PVS budget exhaustion, and measured times
  of 10.951, 14.803, and 25.142 ms.
- Clean WTZ Project and WTZ Engine solution builds succeed with zero errors and
  700 and 185 established warnings respectively.

### Decisions

- Evolve the network payload from world XY/world Z to grid identity, local Z,
  and portal-local XY. This removes snapshot churn and stale direction when a
  structural frame moves.
- Keep server authorization and client presentation separate. The client may
  reject a grant but can never invent one.
- Build policies before the native audio frame and atomically swap one complete
  dictionary. Robust's synchronous parallel `ProcessNow` guarantees the old
  buffer is no longer in use before it is recycled on a later frame.
- Reuse native distance modeling by applying the ratio of route-distance gain
  to portal-distance gain. The source can point toward the portal without
  pretending the entire acoustic route is only that direct distance.
- Keep explicit muted policies for unauthorized cross-floor streams. Missing
  dictionary entries mean native behavior only for streams that are not
  cross-floor in the current view.
- Expose exact policy diagnostics for deterministic tests instead of reading
  the headless client's shared `DummyAudioSource`, whose state is intentionally
  not entity-local.

### Completion Gate

- [x] Scope check: the parent diff is limited to client sound presentation,
      the local-coordinate payload, diagnostics, tests, documentation, and the
      paired one-line engine access contract.
- [x] Invariant review: Z 0/same-floor compatibility, local/world frames,
      moving and rotated grids, exact viewers, server authority, Sound-channel
      isolation, pressure, range, revocation, and PVS disablement were reviewed.
- [x] Automated verification: 4/4 unit, 5/5 playback, 13/13 sound, 242/242
      Content integration, 9/9 Content unit/analyzer, 1/1 focused engine,
      37/138 engine client, 447/1,026 engine shared, and both clean builds pass.
- [x] Performance evidence: the worker path performs only an atomic read and
      dictionary lookup; policy buffers are reused, timing counters are exposed,
      and warmed stress baselines retain 6,336 bytes and zero relevant misses.
- [x] Documentation: protocol, ownership, formulas, threading, safety policy,
      diagnostics, evidence, and deliberate limits are recorded here and in
      `Docs/ZLevelSound.md`.
- [x] Dependency check: WTZ Engine is committed and pushed at
      `3aaca280f628876939afcc10a9be920b3898902a` on
      `zlevel/sound-playback`.
- [x] Git check: both repository diffs pass `git diff --check`; generated
      artifacts remain ignored and the engine tree is clean at its pushed SHA.
- [x] Mini review: no blocking correctness, authority, lifecycle, or threading
      finding remains; scale and perceptual risks are assigned below.
- [x] Commit: paired engine `Allow content audio source positioning` and parent
      `Present routed vertical sound on clients` commits are prepared for the
      WTZ sound-playback branches.

### Mini Review

- Finding: P4 now carries one logical positional emission through bounded
  topology, pressure-aware server authorization, PVS, and deterministic client
  direction/attenuation without restarting or duplicating playback.
- Finding: authorization stays fail-closed even with engine PVS disabled, while
  same-floor/global audio preserves native Robust behavior.
- Finding: local payloads plus frame-by-frame reprojection close the stale-world
  coordinate bug for moving and rotated grids without increasing network rate.
- Residual risk: client policy construction scans replicated positional streams
  once per frame. Dense multiplayer scale, dictionary high-water retention, and
  long-running timing distribution belong to P8.
- Residual risk: headless tests prove route policy and callback execution, not a
  human stereo perception check on every audio backend. Manual round validation
  remains part of P8 hardening.
- Residual risk: cross-grid sound, room-scale wall routing, material/frequency
  coefficients, and persistent acoustics remain deliberate future policies.
- Next package: P5.1 inventories Robust navigation ownership and defines the
  stable vertical transition graph contract before changing AI path selection.

## Completed Package: P5.1 Authored Traversal Graph Contract

### Scope

- Inventory Robust Content's local path graph, path requests, HTN caller, and
  steering ownership before changing navigation behavior.
- Define an indexed directed connector graph keyed by grid, XY tile, and local
  Z, with explicit world projection, connector kind, cost, delay, support
  policy, and stale-route revisions.
- Replace the player traversal system's global connector scans and per-move BFS
  allocations with exact-floor indexed queries and bounded connected regions.
- Expose graph metrics and document the P5.2 through P5.4 architecture without
  enabling unsafe cross-floor AI routes over the existing mixed-floor graph.

### Acceptance Criteria

- Stair, ladder, shaft, and elevator semantics are representable without
  pretending every connector is a bidirectional 2D polygon portal.
- Directed edges fail closed for invalid offsets, closed traversal boundaries,
  and missing destination support when direct support is required.
- Connector identity and topology stay grid-local while source and destination
  world Z follow `ZLevelFrameComponent` changes.
- Exact-floor lookup and connected-region continuation are deterministic,
  bounded, and allocation-free after warmup within the test allowance.
- Existing delayed player stair traversal and Z 0 behavior do not regress.

### Verification Evidence

- Focused graph and step-trigger integration: 3/3 pass.
- Complete movement integration: 16/16 pass.
- Complete Content Z-level integration: 244/244 pass.
- Content Z-level unit/analyzer: 9/9 pass.
- Stress baselines: 3/3 pass for 3, 6, and 10 floors; each warmed workload
  allocates 6,336 bytes, retains 100% boundary/gravity cache hit rates, and has
  zero PVS budget exhaustion. Measured times are 7.9334 ms, 16.4693 ms, and
  31.7033 ms respectively.
- The warmed connected-region loop passes 256 repeated queries with no more
  than 256 bytes of total thread allocation.
- A clean `SpaceStation14.slnx` build succeeds with zero errors and 708
  established warnings. The package introduces no analyzer warning.

### Decisions

- Keep local polygon navigation and vertical connector topology separate. The
  former owns walkability inside one floor; the latter owns authored transitions.
- Do not attach vertical edges to the existing `PathPortal` skeleton yet. Its
  endpoints are 2D and the current breadcrumb builder combines Z 0 tiles with
  fixtures from overlapping floors, which can manufacture false routes.
- Store connector topology in local Z and derive world Z at edge resolution so
  moving frame origins do not force a topology rebuild.
- Model connectors as directed edges. Content that supports travel both ways
  must author both directions explicitly or expose a future deliberate
  bidirectional policy.
- Use sorted compact location buckets instead of `SortedSet`; the latter's
  enumerator allocates an internal stack on every compatibility lookup.
- Treat direct runtime component-field mutation as an explicit refresh contract.
  Hot reads do not rescan ECS state, and future dynamic-connector setters must
  call `RefreshTraversal` internally.
- Keep live graph buffers simulation-thread owned. Future parallel route jobs
  will receive immutable snapshots rather than query mutable collections.

### Completion Gate

- [x] Scope check: the diff is limited to traversal metadata/indexing, player
      traversal query migration, metrics, focused tests, and P5 documentation.
- [x] Invariant review: Z 0 compatibility, local/world frame conversion, moving
      frame origins, exact-floor fixture ownership, server authority, directed
      boundary channels, and destination support were reviewed.
- [x] Automated verification: 3/3 focused, 16/16 movement, 244/244 Content
      integration, 9/9 Content unit/analyzer, 3/3 baselines, and the clean full
      solution build pass.
- [x] Performance evidence: global scans and per-move graph construction are
      removed; graph metrics expose hit, visit, budget, status, and timing data;
      256 warmed connected queries meet the 256-byte total allowance.
- [x] Documentation: ownership, coordinate rules, connector validity, mutation
      and threading contracts, remaining packages, tests, and limitations are
      recorded here and in `Docs/ZLevelPathfinding.md`.
- [x] Dependency check: no WTZ Engine change is required; the engine remains at
      `3aaca280f628876939afcc10a9be920b3898902a` with a clean tree.
- [x] Git check: generated results remain ignored, the engine tree is clean, and
      the final parent diff/status checks are performed immediately before commit.
- [x] Mini review: no blocking correctness or performance finding remains; the
      current mixed-floor local polygon graph is explicitly isolated from the
      new connector graph until P5.2.
- [x] Commit: the isolated `Index authored Z-level traversal edges` parent
      commit is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: P5.1 establishes a bounded, observable, frame-correct vertical
  topology contract without exposing NPCs to the known mixed-floor navmesh.
- Finding: normal stair gameplay now reads the same connector index that future
  hierarchical planning will consume, while retaining the existing two-second
  DoAfter and adjacent-equivalent-stair behavior.
- Finding: exact location lookup is deterministic and connected lookup no longer
  scans all traversal entities or allocates an enumerator stack per request.
- Residual risk: runtime code can mutate public component fields without calling
  `RefreshTraversal`; P5.4 dynamic connector APIs must encapsulate this contract.
- Residual risk: graph methods reuse main-thread buffers and are intentionally
  not a worker-thread API; P5.3 must publish immutable route-search snapshots.
- Residual risk: direct-support-disabled edges carry policy but defer actor-
  specific landing validation to hierarchical planning and execution.
- Next package: P5.2 separates `PathPoly`, chunks, tile reads, fixture filters,
  endpoint lookup, and dirtying by floor before any vertical route composition.

## Completed Package: P5.2 Floor-Specific Local Navigation

### Scope

- Partition the existing local polygon graph by `(XY chunk, grid-local Z)` and
  carry floor identity through polygons, native portals, requests, and debug
  network payloads.
- Build sparse authored floors from their own tiles and reject broadphase fixture
  candidates that belong to an overlapping floor.
- Invalidate only affected floors and chunks after tile, collision, fixture Z,
  XY movement, grid movement, and frame-origin events.
- Add explicit world-floor query and route APIs while preserving same-floor
  behavior for existing callers.
- Expose floor-specific pathfinding diagnostics and deterministic regression
  tests without enabling vertical route composition prematurely.

### Acceptance Criteria

- Overlapping floors can have different tiles and blockers without contaminating
  each other's `PathPoly` data or route result.
- A tile or fixture change rebuilds every affected old/new location and does not
  rebuild an unrelated floor.
- Grid-local navigation survives world-frame origin changes without a topology
  rebuild, and native cross-grid portals remain same-world-floor links.
- Entity targets carry their actual floors; ambiguous coordinate-only overloads
  remain compatible by resolving to the actor or start-reference floor.
- A different-floor A* request fails closed until P5.3 can compose a typed route.
- Z 0 callers and the existing 2D pathfinding behavior remain compatible.

### Verification Evidence

- Focused P5.2 integration passes 3/3 after final review. It covers overlapping
  floor fixtures, same-floor route isolation, fixture Z and cross-chunk XY
  movement, floor-selective tile invalidation, frame origin 5, legacy Z 0, and
  deliberate different-floor rejection.
- The complete Content Z-level integration matrix passes 247/247 and the Content
  unit/analyzer matrix passes 9/9.
- The generated 3, 6, and 10-floor stress baselines pass 3/3 with 100% warmed
  boundary/gravity cache hits and zero PVS budget exhaustion. Measured times are
  11.0718 ms, 13.3543 ms, and 20.4120 ms; all three measured runs allocate
  6,336 bytes. Timing remains local comparison evidence rather than a release
  threshold.
- After 64 warmup calls, 4,096 explicit-floor `GetPoly` calls complete with no
  misses and no more than 256 bytes of total current-thread allocation.
- Reusing one fixture candidate set for all 256 tiles reduces the measured
  warmed single-chunk breadcrumb build from 59,480 to 55,448 bytes. The focused
  test enforces a 58,000-byte ceiling that rejects the former per-tile allocation
  path while retaining 2,552 bytes of local-runtime margin.
- A clean `SpaceStation14.slnx` build succeeds with zero errors and the same 708
  established warnings as P5.1. No warning points to the P5.2 files.

### Decisions

- Store local navigation topology by grid-local Z and keep public endpoints in
  world Z. Frame-origin changes therefore reproject requests without rebuilding
  local chunks.
- Continue using the mature 2D broadphase for XY candidate discovery, then apply
  an effective-floor filter before breadcrumb construction. This preserves door,
  collision-layer, diagonal-fixture, and access behavior already owned by Robust.
- Discover sparse upper-floor chunks from authored non-empty tiles while keeping
  the normal base-grid initialization path for Z 0.
- Dirty both old and new fixture locations. The package review also fixed the
  pre-existing same-grid XY move path that previously invalidated only the new
  chunk.
- Keep native `PathPortal` restricted to equal world floors. Authored stairs,
  ladders, shafts, and elevators remain in `ZLevelTraversalGraphSystem`.
- Reject cross-floor A* before polygon lookup. P5.3 will compose local legs and
  typed transition legs instead of inserting fake polygon neighbors.
- Keep metric recording allocation-free in path hot paths. The administrative
  snapshot may allocate a temporary floor set because it runs only on demand.
  Record breadcrumb build allocation alongside timing, and reuse the broadphase
  candidate set instead of allocating one collection per tile.
- Filter client pathfinding diagnostics to the viewed world floor and use floor
  division/positive modulo for negative chunk coordinates, matching the server.

### Completion Gate

- [x] Scope check: the diff is limited to floor-specific local pathfinding,
      diagnostics, focused tests, and P5 documentation.
- [x] Invariant review: Z 0 compatibility, local/world frames, sparse floors,
      moving grids, old/new XY and Z invalidation, server-owned routing, and
      same-world-floor native portals were reviewed.
- [x] Automated verification: 3/3 focused, 247/247 Content integration, 9/9
      Content unit/analyzer, 3/3 baselines, and the clean full build pass.
- [x] Performance evidence: 4,096 warmed polygon lookups meet the 256-byte
      allowance; the warmed breadcrumb build uses 55,448 bytes under its
      58,000-byte ceiling; allocation/timing, candidate rejection, query hit,
      floor cache, and pending-work counters are exposed through `zlevelmetrics`.
- [x] Documentation: topology ownership, coordinate contracts, overload limits,
      metrics, tests, performance values, and deliberate `NoPath` behavior are
      recorded here and in `Docs/ZLevelPathfinding.md`.
- [x] Dependency check: no WTZ Engine change is required; the engine remains at
      `3aaca280f628876939afcc10a9be920b3898902a` with a clean tree.
- [x] Git check: generated results remain ignored; final parent/engine status,
      remote pairing, and `git diff --check` are verified immediately before
      committing.
- [x] Mini review: no blocking correctness or performance issue remains; the
      limits assigned to P5.3/P5.4 are explicit below.
- [x] Commit: the isolated `Separate pathfinding navigation by Z level` parent
      commit is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: local pathfinding can now represent coincident walkable and blocked
  floors without false obstruction or false reachability between them.
- Finding: local topology is frame-stable and invalidation is floor-selective;
  fixture movement now covers both old/new Z and old/new XY chunks.
- Finding: diagnostics expose whether workload growth comes from sparse chunks,
  broadphase candidates, floor rejects, query misses, or pending rebuilds.
- Residual risk: plain `EntityCoordinates` cannot identify an upper target when
  its reference entity is only a grid. Those overloads intentionally choose the
  actor/start floor; cross-floor callers must use explicit world Z or an entity.
- Residual risk: NPC steering still owns a queue of plain local polygons and
  cannot execute a vertical action. Different-floor requests remain `NoPath`
  until the typed route contract is complete.
- Residual risk: live chunk and traversal graph state remains simulation-thread
  owned. P5.3 must publish immutable search input before using worker jobs.
- Residual risk: like the legacy 2D cache, a created floor chunk remains cached
  after its last authored tile is removed. `cached chunks/floors` exposes the
  high-water mark; eviction and long-round retention belong to P5.4/P8.
- Next package: P5.3 adds bounded hierarchical search over traversal connectors,
  composes local and transition legs, and rejects stale revisions
  deterministically.

## Completed Package: P5.3a Detached Graph And Typed Route Contracts

### Scope

- Publish deterministic immutable traversal snapshots that hierarchical search
  can read without touching live ECS collections.
- Cache detached snapshots by map and graph revision, evict them on map removal,
  and expose request/hit/build/allocation metrics.
- Define explicit endpoint, local-leg, traversal-leg, route, result, diagnostic,
  revision, and caller-owned budget contracts.
- Add configurable state-expansion, local-path, and traversal-edge limits without
  changing current NPC behavior before real route composition exists.

### Acceptance Criteria

- Snapshot ordering does not depend on entity enumeration order, and a captured
  edge array cannot observe later graph mutations.
- Search input can be reused without ECS access or per-request edge-copy
  allocation, while topology and environment staleness remain distinguishable.
- A typed route cannot contain disconnected endpoints, cross-map legs, invalid
  local-floor transitions, non-finite costs, or traversal revisions from a
  different snapshot.
- Budget exhaustion, cancellation, no-path, topology, and environment outcomes
  have separate result statuses for P5.3b/P5.4 consumers.

### Verification Evidence

- The focused snapshot/route integration scenario passes 1/1. It covers
  semantic edge ordering, detached storage, topology-only, environment-only and
  combined staleness, route invariants, configured budgets, and metrics.
- After 32 warmups, 256 cached snapshot requests reuse the same immutable edge
  array with no more than 256 bytes of total current-thread allocation. Cold
  two/three-edge captures stay below the 16,384-byte regression ceiling.
- The final complete Content Z-level integration matrix passes 248/248, and the
  Content unit/analyzer matrix passes 9/9.
- The final 3, 6, and 10-floor artifacts pass 3/3 with 100% warmed boundary and
  gravity cache hits, zero PVS budget exhaustion, and 6,216 measured bytes each.
  Local measured times are 4.1361, 8.2035, and 13.1701 ms.
- A full `SpaceStation14.slnx` build completes with zero errors and 237 existing
  warnings emitted by this incremental build; none points to a P5.3a file.

### Decisions

- Keep the live connector index simulation-thread-owned and give asynchronous
  route work only immutable value snapshots.
- Sort edges by source world floor, grid, local floor, tile, destination floor,
  and traversal UID so equal graph state yields equal search order.
- Cache one snapshot per map/revision because copying an immutable edge array for
  every NPC request would defeat the worker-safety design with avoidable GC work.
- Retain separate topology and environment revisions. P5.3b can therefore report
  why a pending route became stale instead of collapsing both into `NoPath`.
- Represent vertical actions as authored traversal legs and local movement as
  native polygon legs. No fake polygon neighbor or empty-space walkability is
  introduced.
- Use global graph revisions conservatively for now. Map-scoped revisions are an
  optimization only if P5.4/P8 metrics show cross-map invalidation churn.

### Completion Gate

- [x] Scope check: the diff is limited to detached traversal input, typed route
      contracts, budgets/metrics, focused tests, and pathfinding documentation.
- [x] Invariant review: Z 0, local/world floors, moving frames, map lifetime,
      directed edges, support/boundaries, server ownership, and stale revisions
      were reviewed.
- [x] Automated verification: 1/1 focused, 248/248 Content integration, 9/9
      Content unit/analyzer, 3/3 baselines, and the full solution build pass.
- [x] Performance evidence: 256 warmed requests stay within 256 bytes, cold small
      snapshots within 16,384 bytes, and cache/build timing plus allocation are
      exposed through `zlevelmetrics`.
- [x] Documentation: contracts, CVars, ordering, cache ownership, tests,
      measurements, limitations, and next work are recorded in both P5 docs.
- [x] Dependency check: no WTZ Engine change is required; the paired engine tree
      remains at `3aaca280f628876939afcc10a9be920b3898902a`.
- [x] Git check: generated baseline artifacts remain ignored; parent/engine
      status and `git diff --check` are verified immediately before commit.
- [x] Mini review: no blocking issue remains in the detached contract; residual
      planning/execution work is assigned below.
- [x] Commit: the isolated `Define hierarchical Z-level route contracts` commit
      is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: hierarchical work can now operate on deterministic, allocation-free
  warmed snapshots without racing component/index mutations.
- Finding: local and vertical route semantics are explicit and independently
  validatable; budget and stale-state failures cannot masquerade as no-path.
- Residual risk: the contracts are not yet consumed by gameplay. Cross-floor
  native A* still deliberately returns `NoPath` until P5.3b composes real legs.
- Residual risk: graph revisions are global, so activity on one map may rebuild a
  different map's snapshot. Metrics now make that cost observable.
- Residual risk: local `PathPoly` instances can become invalid after capture;
  P5.3b/P5.4 must validate the first affected leg and replan before execution.
- Next package: P5.3b runs bounded hierarchical search over these snapshots,
  awaits native same-floor A*, composes the typed route, and rejects stale work.

## Package History

| Date | Package | Commit | Verification | Result |
| --- | --- | --- | --- | --- |
| 2026-08-27 | P0.1 | `Add native Z-level performance observability` | 46 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P0.2 | `Add deterministic Z-level stress baselines` | 3 baseline cases, 49 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P0.3 | `Add configurable Z-level performance budgets` | 4 budget, 3 baseline, 53 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P1.1 | `Define the shared Z-level trace contract` | 4 trace, 57 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P1.2 | `Implement ordered vertical Z-level traces` | 5 trace, 11 trace/budget, 60 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P1.3a | `Normalize Z-level traces across moving frames` | 7 trace, 62 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P1.3b1 | `Add reusable Z-level trace buffers` | 8 trace, 7 budget, 64 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P1.3b2 | `Instrument and benchmark Z-level traces` | 17 trace/budget, 3 metrics/benchmark, 68 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.1 | `Migrate hitscan to Z-level traces` | 10 hitscan, 1 weapon, 78 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.2a | `Preserve Z-level projectile lifecycle` | 5 lifecycle, 12 regressions, 83 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.2b | `Add physical cross-level ballistic trajectories` | 18 trajectory, 12 regressions, 101 integration, 7 engine, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.3a | `Make explosions Z-level authoritative` | 14 explosion, 9 prototype, 120 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.3b | `Make fire and atmosphere overlays Z-aware` | 12 atmosphere, 14 explosion, 120 integration, 9 prototype, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.3c1 | `Layer decals across Z levels` | 4 decal, 5 map format, 109 integration, 2 unit, 3 baseline, diff check | Complete |
| 2026-08-27 | P2.3c2 | `Keep generated effects on their Z levels` | 10 package, 132 integration, 2 unit, 3 baseline, full build, diff check | Complete |
| 2026-08-27 | P2.4a | `Centralize native Z-level interaction authority` | 4 authority, 12 regression, 135 integration, full build, diff check | Complete |
| 2026-08-27 | P2.4b | `Instrument native Z-level interaction policy` | 13 package, 21 regression, 144 integration, allocation, full build, diff check | Complete |
| 2026-08-28 | P2.4c | `Harden native Z-level interaction funnels` | 18 authority/funnel, 24 native regression, 151 integration, full build, diff check | Complete |
| 2026-08-28 | P2.4d1 | `Carry authoritative floors through pointer targets` | 20 authority, 24 native regression, 153 integration, 2 engine, full build, diff check | Complete |
| 2026-08-28 | P2.4d2 | `Enable authored cross-floor entity interactions` | 25 authority, 24 native regression, 158 integration, full build, diff check | Complete |
| 2026-08-28 | P2.4d3a | `Authorize visible lower-floor action coordinates` | 26 authority, 24 native regression, 159 integration, full build, diff check | Complete |
| 2026-08-28 | P2.4d3b | `Aim projectiles at lower-floor coordinates` | 33 combat, 27 native interaction, 1 native weapon, 164 integration, full build, diff check | Complete |
| 2026-08-28 | P2.4d3c | `Harden Z-level combat request authority` | 51 combat, 7 network throw, 4 native combat, 24 native interaction, 182 integration, full build, diff check | Complete |
| 2026-08-28 | P3.1 | `Make active-floor rendering world-Z aware` | 135 engine client integration, 29 engine unit, 4 serialization, 1 replication, 182 Content Z-level, 3 baseline, full build, diff check | Complete |
| 2026-08-28 | P3.2 | `Cache vertical lighting inputs` | 6 package, 188 Content Z-level, 136/29 engine client, 1026/446 engine shared, 3 baseline, full build, allocation, diff check | Complete |
| 2026-08-28 | P3.3 | `Project lower-floor Z-level lighting` | 6 projection, 11 projection/cache, 194 Content Z-level, 136/30 engine client, 3 baseline, full build, allocation, diff check | Complete |
| 2026-08-28 | P3.4a | `Bound Z-level lighting projection work` | 8 package, 19 lighting, 202 Content Z-level, 2 unit, 3 baseline, full build, allocation, diff check | Complete |
| 2026-08-28 | P3.4b | `Bound Z-level tile projection work` | 13 package, 32 lighting/tile, 215 Content Z-level, 2 unit, 3 baseline, full build, allocation, diff check | Complete |
| 2026-08-28 | P3.4c1 | `Expose external point-light shadow atlases` / `Track external Z-level shadow atlas support` | 7 engine unit, 1 engine integration, 37 complete engine client unit, 137 complete engine client integration, full engine build, diff check | Complete |
| 2026-08-28 | P3.4c2 | `Render bounded lower-floor light shadows` | 13 package, 228 Content Z-level, 2 unit, 3 baseline, full build, allocation, diff check | Complete |
| 2026-08-28 | P3.4c3 | `Allow safe ImageSharp pixel reads` / `Harden Z-level lighting with real visual capture` | 3 analyzer, 1 PVS, 13 shadow, 229 Content Z-level, 3 baseline, full build, 3x 19 real GL checks, diff review | Complete |
| 2026-08-28 | P3 gate | `Close the P3 lighting and FOV phase gate` | 229 Content, 37/137 engine client, 4 serialization, 13 map/replication, 3 baseline, 19 real GL, architecture review | Complete |
| 2026-08-28 | P4.1 | `Cache bounded vertical sound portals` | 4 focused, 233 Content integration, 5 Content unit/analyzer, 3 baseline, allocation, full build, diff review | Complete |
| 2026-08-28 | P4.2 | `Route vertical sound through bounded portals` | 4 route, 8 sound, 237 Content integration, 5 Content unit/analyzer, 3 baseline, allocation, full build, diff review | Complete |
| 2026-08-28 | P4.3a | `Expose positional audio post-processing` / `Track positional audio post-processing support` | 1 focused engine, 138 engine client integration, 37 engine client unit, client build, diff review | Complete |
| 2026-08-29 | P4.3b | `Centralize audio recipient filtering` / `Authorize routed vertical sound per session` | 3 playback, 11 sound, 240 Content integration, 5 Content unit/analyzer, 447/1,026 engine shared, 3 baseline, clean build, allocation/diff review | Complete |
| 2026-08-29 | P4.3c | `Allow content audio source positioning` / `Present routed vertical sound on clients` | 4 unit, 5 playback, 13 sound, 242 Content integration, 9 Content unit/analyzer, 37/138 engine client, 447/1,026 engine shared, 3 baseline, clean builds, diff review | Complete |
| 2026-08-29 | P5.1 | `Index authored Z-level traversal edges` | 3 focused, 16 movement, 244 Content integration, 9 Content unit/analyzer, 3 baseline, clean build, allocation/diff review | Complete |
| 2026-08-29 | P5.2 | `Separate pathfinding navigation by Z level` | 3 focused, 247 Content integration, 9 Content unit/analyzer, 3 baseline, clean build, allocation/diff review | Complete |
| 2026-08-29 | P5.3a | `Define hierarchical Z-level route contracts` | 1 focused, 248 Content integration, 9 Content unit/analyzer, 3 baseline, full build, cache/allocation/diff review | Complete |
