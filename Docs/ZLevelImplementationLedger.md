# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/lighting-projection`.
- Active package: `P3.3 bounded lower-floor light/FOV projection and attenuation`.
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
| P3 | Z-aware lighting and FOV with bounded caches and budgets | In progress |
| P4 | Vertical sound propagation through cached portals | Pending |
| P5 | Hierarchical pathfinding with vertical transition edges | Pending |
| P6 | Safe initialized-map save/load and automated round trips | Pending |
| P7 | Roofs, grates, catwalks, shafts, elevators, weather, and flight | Pending |
| P8 | Server hardening, scale tests, Z 0 regression, and porting guide | Pending |

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
| P3.3 | Bounded lower-floor light/FOV projection and attenuation | In progress |
| P3.4 | Frame budgets, fail-soft degradation, visual regressions, and hardening | Pending |

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
