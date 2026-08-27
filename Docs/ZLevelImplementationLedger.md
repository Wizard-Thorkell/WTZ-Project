# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/projectile-traversal`.
- Active package: `P2.2b Bounded physical vertical trajectory`.
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
| P2 | Hitscan, projectiles, throws, explosions, effects, and interactions | In progress |
| P3 | Z-aware lighting and FOV with bounded caches and budgets | Pending |
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
| P2.2 | Physical projectiles and thrown-entity traversal | In progress |
| P2.3 | Explosions, fire, heat, and generated effects | Pending |
| P2.4 | Central direct and remote interaction validation | Pending |

P2.2 is split into independently gated subpackages:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.2a | Projectile/throw floor authority and lifecycle preservation | Complete |
| P2.2b | Bounded physical vertical trajectory and crossing policy | In progress |

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

## Active Package: P2.2b Bounded Physical Vertical Trajectory

### Planned Scope

- Define opt-in continuous vertical trajectory state separately from vanilla
  same-floor throw and projectile components.
- Validate every crossed half-level plane with the `Projectile` boundary channel
  at the crossing's current grid-local XY.
- Integrate with physics substeps so source-floor and destination-floor contacts
  are not assigned to the wrong portion of a fast step.
- Bound crossings and fail conservatively on invalid frames, closed decks, or
  exhausted work without tracing an unbounded future trajectory.
- Cover Z 0 parity, open/closed decks, fast diagonal crossings, moving frames,
  prediction/reconciliation, landing, reflection, and allocation evidence.

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
