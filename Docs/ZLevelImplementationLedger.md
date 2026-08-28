# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/interaction-metrics`.
- Active package: `P2.4c interaction request-funnel audit`.
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
| P2.2 | Physical projectiles and thrown-entity traversal | Complete |
| P2.3 | Explosions, fire, heat, and generated effects | Complete |
| P2.4 | Central direct and remote interaction validation | In progress |

P2.4 is split into independently gated subpackages:

| Package | Deliverable | Status |
| --- | --- | --- |
| P2.4a | Central spatial origin, same-floor authority, and opt-in trace primitive | Complete |
| P2.4b | Interaction metrics and authored vertical portals | Complete |
| P2.4c | Verb, UI, action, drag/drop, do-after, and remote-view request audit | In progress |
| P2.4d | Client targeting polish, regression matrix, and P2 completion review | Pending |

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
