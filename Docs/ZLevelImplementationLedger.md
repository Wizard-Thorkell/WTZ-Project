# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/server-hardening`.
- Active package: `P8.4d2 validated checkpoint and executable recovery rehearsal`.
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
| P5 | Hierarchical pathfinding with vertical transition edges | Complete |
| P6 | Safe initialized-map save/load and automated round trips | Complete |
| P7 | Roofs, grates, catwalks, shafts, elevators, weather, and flight | Complete |
| P8 | Server hardening, scale tests, Z 0 regression, and porting guide | In progress (P8.4 active) |

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
| P5.3b | Hierarchical route search and typed route composition | Complete |
| P5.4a | Static AI route execution and traversal lifecycle | Complete |
| P5.4b1 | Dynamic traversal state, cost, power, destination, and lifecycle | Complete |
| P5.4b2 | Map-scoped revisions, concurrent NPC scale, and phase hardening | Complete |

## Phase P6 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P6.1 | Initialized mapping snapshot contract and transient-state filtering | Complete |
| P6.2a | Structured reference diagnostics and detached normalization | Complete |
| P6.2b | Correlated save protocol and validation before transfer | Complete |
| P6.2c | UTF-8 temporary write, flush, and atomic destination replacement | Complete |
| P6.3a | Automated double round trips and explicit live-round boundary | Complete |
| P6.3b1 | Initialized floor create/copy/delete lifecycle | Complete |
| P6.3b2 | Validated initialized mapping autosave lifecycle | Complete |
| P6 gate | End-to-end persistence and scope review | Complete |

## Phase P7 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P7.1a | Shared vertical-surface/sky-column contract, bounded cache, and metrics | Complete |
| P7.1b | Roofs, grates, catwalks, and shafts with mapping and construction | Complete |
| P7.2a | Elevator cabins, stops, controls, power, and traversal lifecycle | Complete |
| P7.2b | Elevator mapping, save/load, pathfinding, and hardening | Complete |
| P7.3a | Shared Z-aware weather exposure policy | Complete |
| P7.3b | Bounded Z-aware weather rendering, audio, and diagnostics | Complete |
| P7.4a | Flight movement, gravity, and collision contract | Complete |
| P7.4b1 | Flight controls, capability content, interruptions, and mapping | Complete |
| P7.4b2a | Continuous flight trace and combat integration | Complete |
| P7.4b2b | Explicit flight AI navigation and execution | Complete |
| P7 gate | End-to-end vertical-content and scope review | Complete |

## Phase P8 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P8.1 | Deterministic multi-session scale/soak harness and operational metrics | Complete |
| P8.2 | Budget, cache, invalidation, and lifecycle hardening from scale evidence | Complete |
| P8.3 | Z 0 compatibility matrix and documented porting contract/tooling | Complete |
| P8.4 | Public-server release matrix, operations guide, and final roadmap gate | Active |

## Phase P8.4 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P8.4a | Batch-local PVS context reuse and 32-session Release envelope | Complete |
| P8.4b | Server GC endurance, repeated lifecycle, and retained-memory envelope | Complete |
| P8.4c | Executable `WTZ-RELEASE-1` gameplay, mapping, and persistence matrix | Complete |
| P8.4d1 | Operational health snapshot and initialized-map autosave telemetry | Complete |
| P8.4d2 | Validated checkpoint command and executable recovery rehearsal | Active |
| P8.4d3 | Operations guide, representative evidence, and final P0-P8 gate | Planned |

## Phase P8.2 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P8.2a | Per-stage latency/allocation attribution and GC correlation | Complete |
| P8.2b | Fair, staggered, and bounded PVS refresh scheduling | Complete |
| P8.2c | Gravity invalidation, topology allocation, and lifecycle hardening | Complete |

## Phase P8.3 Packages

| Package | Deliverable | Status |
| --- | --- | --- |
| P8.3a | Executable Z 0 compatibility inventory and regression matrix | Complete |
| P8.3b | Versioned engine/content porting manifest and compatibility verifier | Complete |
| P8.3c | Clean-worktree port rehearsal, guide, and P8.3 phase gate | Complete |

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
  atmosphere mixtures. Active hotspots are transient round state and require a
  separate future live-round persistence contract.
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
  atmosphere-mixture round trips, while active hotspot restoration is outside
  mapper-authored persistence.
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

## Completed Package: P5.3b Hierarchical Route Search And Typed Composition

### Scope

- Add an opt-in server API that searches detached traversal snapshots and
  composes native local A* paths with authored vertical transitions.
- Bound state expansion, local-path dispatch, and traversal-edge evaluation
  independently, with typed cancellation, no-path, and stale outcomes.
- Validate an already planned route by returning its first invalid local or
  traversal leg without rejecting unrelated graph revisions.
- Expose query outcomes, work counts, leg counts, and elapsed time through
  `zlevelmetrics` while leaving legacy cross-floor `GetPath` behavior unchanged.

### Acceptance Criteria

- A connector is usable only when native same-floor navigation can reach its
  exact source; overlapping geometry or straight-line distance is insufficient.
- Equal graph/input state produces deterministic route selection, and every
  successful route is a connected sequence of typed local/traversal legs.
- Cancellation after requests are queued terminates bounded work, each budget
  reports its own exhaustion status, and topology/environment/both staleness are
  distinguishable.
- Local polygon invalidation and exact connector removal identify the first
  affected leg, while an unrelated connector registration leaves the route valid.
- No current NPC consumer silently changes behavior before P5.4 execution exists.

### Verification Evidence

- The final focused `ZLevelPathfindingTest` matrix passes 8/8. It covers local
  routes and invalidation, equal-cost deterministic stairs, three-leg composition,
  blocked connector reachability, all budgets, queued cancellation, all graph
  staleness variants, selective validation, diagnostics, and metrics.
- The deterministic two-stair case records exactly 3 expanded states, 4 local A*
  requests, 2 evaluated traversal edges, and a 3-leg winning route. These bounded
  work counters and query timings are available through `zlevelmetrics`.
- The final complete Content Z-level integration matrix passes 253/253, and the
  Content unit/analyzer matrix passes 9/9.
- The final 3, 6, and 10-floor artifacts pass 3/3 with 100% warmed boundary and
  gravity cache hits, zero PVS budget exhaustion/fail-open candidates, and 6,336
  measured bytes each. Local times are 6.9764, 13.5241, and 23.7440 ms.
- A full incremental `SpaceStation14.slnx` build completes with zero errors and
  27 established warnings; none points to a P5.3b file.

### Decisions

- Keep hierarchical planning opt-in through `GetZLevelPath`; the native
  `GetPath` overloads remain same-floor until P5.4 has an execution consumer.
- Batch all local A* candidates for one expanded state. This preserves the
  engine's per-tick path queue and parallel processing instead of running one
  connector query per tick in serial.
- Use a deterministic bounded Dijkstra over exact connector endpoints. Local
  reachability and collision policy come from native A*, while graph snapshots
  provide only authored vertical actions.
- Store the real cancellation token on `PathRequest`; its former
  `TaskCompletionSource(object)` use treated the token as inert task state.
- Publish the A* accumulated cost and use it for route composition and
  `GetPathDistance`, replacing a legacy distance loop that never advanced its
  previous node.
- Validate pending searches conservatively against global graph revisions, but
  validate completed routes by exact legs so unrelated mutations do not force a
  replan.
- Do not impose a current-thread allocation ceiling on an async multi-tick route:
  continuations may change threads and make that measurement misleading. Explicit
  work budgets, operation counts, timings, and unchanged stress baselines are the
  package's performance evidence.

### Completion Gate

- [x] Scope check: the diff is limited to hierarchical search/composition,
      native request cancellation/cost, route validation/metrics, tests, and P5
      documentation.
- [x] Invariant review: Z 0, local/world floors, moving frames, map ownership,
      exact connector endpoints, directed edges, support/boundaries, and stale
      local navigation were reviewed.
- [x] Automated verification: 8/8 focused, 253/253 Content integration, 9/9
      Content unit/analyzer, 3/3 baselines, and the full solution build pass.
- [x] Performance evidence: exact search work is asserted, all outcome/work/time
      metrics are exposed, budgets fail explicitly, and stress baselines remain
      free of cache/PVS exhaustion.
- [x] Documentation: architecture, API ownership, cancellation, costs, budgets,
      validation, tests, measurements, limitations, and next work are recorded.
- [x] Dependency check: no WTZ Engine change is required; the paired engine tree
      remains at `3aaca280f628876939afcc10a9be920b3898902a`.
- [x] Git check: generated baseline artifacts remain ignored; parent/engine
      status and `git diff --check` are verified immediately before commit.
- [x] Mini review: no blocking planner issue remains; gameplay execution and
      dynamic connectors are assigned to P5.4 below.
- [x] Commit: the isolated `Compose hierarchical Z-level paths` commit is
      prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: WTZ now plans a real cross-floor route without making empty space
  walkable or encoding stairs as fake polygon neighbors.
- Finding: local reachability, authored transitions, work limits, cancellation,
  staleness, and post-plan validation are independently observable and testable.
- Finding: legacy callers remain behaviorally stable, which gives P5.4 a narrow
  migration surface instead of changing every NPC at once.
- Residual risk: no NPC executes typed traversal legs yet, so cross-floor AI is
  not gameplay-enabled by this package alone.
- Residual risk: native A* costs are polygon-granular; points in one broad polygon
  can tie even when their physical distances differ. Ordering is deterministic,
  and P5.4 telemetry will decide whether endpoint refinement is warranted.
- Residual risk: pending search revisions remain global and search allocates
  per-state/query collections. P5.4/P8 scale metrics will determine whether
  map-scoped revisions, pooling, or a priority queue are justified.
- Next package: P5.4 migrates a controlled AI path consumer, executes normal
  delayed traversal actions, validates/replans each leg, and models dynamic
  elevator state before closing the P5 phase gate.

## Completed Package: P5.4a Static AI Route Execution

### Scope

- Add a typed hierarchical route state machine to `NPCSteeringSystem` while
  retaining the native local `PathPoly` queue for movement inside each floor.
- Migrate `MoveToOperator` planning and runtime steering requests to
  `GetZLevelPath` only when actor and target world floors differ.
- Execute authored traversal legs through the normal server-owned delayed
  traversal action, then validate the destination and continue the next leg.
- Replan or fail deterministically after connector, local navigation, target,
  map, floor, or async endpoint changes, and expose execution metrics.
- Harden connector deletion so every pending user is cancelled.

### Acceptance Criteria

- An NPC can autonomously plan, walk to stairs, wait for the normal two-second
  `DoAfter`, change floor, continue its local path, and stop inside final range.
- HTN planning can carry a typed route into execution without changing legacy
  same-floor route behavior or encoding traversal as a fake polygon.
- Route execution validates exact graph/local legs after revisions and cancels
  an owned pending traversal before replanning or shutting down.
- Actor and target endpoints are stable across asynchronous local A* work;
  stale results cannot install after meaningful movement or map/floor changes.
- Moving a whole grid preserves frame-relative plans, while moving only the
  target triggers one observable replacement route.
- Z 0 and non-Z steering retain their existing target/range contract.

### Verification Evidence

- The final focused `ZLevelPathfindingTest` matrix passes 18/18 with no skips.
  It covers planning/composition plus HTN installation, runtime execution,
  delayed traversal, arrival braking, actor/target endpoint snapshots, stale
  rejection, graph and target-floor replans, cancellation, and moving frames.
- Connector deletion with two simultaneous users cancels both `DoAfter`s and
  neither user changes floor after the original delay.
- The complete Content `FullyQualifiedName~ZLevel` integration matrix passes
  263/263, and the Content Z-level unit/analyzer matrix passes 9/9.
- The generated 3-, 6-, and 10-floor baselines pass 3/3. Every measured phase
  allocates 6,336 bytes, has 100% boundary/gravity cache hits, and records zero
  PVS budget exhaustion or fail-open candidates. Local measured times are
  7.8504, 13.9121, and 21.6865 ms.
- A full incremental `SpaceStation14.slnx` build completes with zero errors and
  27 established dependency/obsolescence warnings; none points to this package.

### Decisions

- Keep one typed route on the steering component and load one local leg at a
  time into the existing queue. Door, obstacle, collision, and movement policy
  therefore remain owned by mature local steering.
- Use the public idempotent traversal API rather than teleporting NPCs. The
  progress bar, delay, adjacency, destination support, and boundary checks stay
  identical for NPCs and players.
- Snapshot endpoint XY into a grid/map frame before the first `await`, then
  revalidate both actor and target at installation. Frame motion remains valid;
  entity motion beyond `RepathRange` is stale.
- Validate exact route legs only when graph revisions change or a new leg is
  loaded. Unrelated global revision changes do not force a replacement route.
- Cancel route-owned traversal actions on target replacement, replan, failure,
  component shutdown, or connector deletion. Preserve cancellation retention in
  `DoAfterComponent` for replication while treating its state as inactive.
- Stop final physics velocity before reporting `InRange`; returning from that
  tick without another steering blend prevents post-arrival oscillation.
- Keep dynamic elevators out of this package. They need explicit availability,
  power, wait cost, destination, and invalidation semantics in P5.4b.

### Completion Gate

- [x] Scope check: the diff is limited to NPC route execution, traversal
      lifecycle hardening, diagnostics, focused tests, and P5 documentation.
- [x] Invariant review: Z 0, local/world floors, moving frames, map identity,
      asynchronous staleness, exact connectors, server authority, and normal
      boundary/destination policy were reviewed.
- [x] Automated verification: 18/18 focused, 263/263 Content integration, 9/9
      Content unit/analyzer, 3/3 baselines, and the full solution build pass
      without skips or errors.
- [x] Performance evidence: hierarchical execution counters are exposed;
      planner budgets remain asserted; warmed stress allocation/cache/budget
      results stay unchanged. Concurrent NPC load is assigned to P5.4b.
- [x] Documentation: execution ownership, endpoint snapshots, replan taxonomy,
      cancellation, metrics, tests, limits, and next work are recorded here and
      in `Docs/ZLevelPathfinding.md`.
- [x] Dependency check: no WTZ Engine change is required; the paired engine
      remains at `3aaca280f628876939afcc10a9be920b3898902a` with a clean tree.
- [x] Git check: generated baselines remain ignored; final parent/engine status,
      staged scope, and `git diff --check` are verified immediately before commit.
- [x] Mini review: no blocking static-route correctness issue remains; dynamic
      connector semantics and scale work are explicitly retained in P5.4b.
- [x] Commit: the isolated `Execute hierarchical Z-level NPC routes` parent
      commit is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: WTZ NPCs now consume the same typed route produced by the bounded
  hierarchical planner and use ordinary player traversal behavior at each edge.
- Finding: route publication and execution are authoritative across target,
  actor, frame, floor, graph, and local-navigation changes, with distinct
  diagnostics instead of silent cross-floor fallback.
- Finding: final braking and owned-DoAfter cancellation close two lifecycle
  failures that only appeared during full movement simulation.
- Finding: deleting one connector now cancels all simultaneous pending users,
  fixing a multiplayer issue discovered during package review.
- Residual risk: elevators are still represented only by the static connector
  contract; runtime cabin state, power, wait cost, and destination selection are
  not yet gameplay-capable.
- Residual risk: graph revisions remain global and floor chunks retain their
  high-water allocation. Concurrent NPC and long-round evidence is needed before
  choosing map-scoped revisions, pooling, or eviction.
- Residual risk: hostile/follow behaviors share `MoveToOperator` and runtime
  steering, but representative multi-floor content scenarios still belong to
  the consolidated P5.4b gate.
- Next package: P5.4b defines dynamic elevator state and route costs, executes
  changing connector availability, then closes P5 with concurrent and prolonged
  navigation tests.

## Completed Package: P5.4b1 Dynamic Traversal State

### Scope

- Add server-authoritative enabled, callable, power, wait-delay, and wait-cost
  state to authored vertical connectors.
- Resolve dynamic state into detached graph edges, route validation, exact NPC
  execution, metrics, and snapshot invalidation.
- Allow adjacent elevator destination selection while preserving normal
  boundary and direct-support authority.
- Cancel in-progress traversal when executable state changes and retain one
  timer only across contiguous tiles with equivalent live behavior.
- Harden instant traversal and entity-deletion lifecycle paths discovered by
  the focused simulation.

### Acceptance Criteria

- Disabled, unavailable, and unpowered connectors fail closed with distinct
  statuses and counters; restoring power makes the edge usable again.
- Expected waiting contributes to both route cost and the visible traversal
  delay, with finite non-negative limits enforced at the mutation API.
- A route captured before cost, delay, state, or destination changes validates
  against the exact live edge and replans instead of executing stale data.
- Power, availability, destination, or effective-delay changes cancel a normal
  pending `DoAfter`; a cost-only planning update does not interrupt physical
  traversal already underway.
- Elevator destination selection is limited to adjacent floors and still
  requires an open traversal boundary and destination support.
- Z 0 players, static stairs, wide stairs, and zero-delay connectors preserve
  their established behavior.

### Verification Evidence

- The focused `ZLevelDynamicTraversalTest` fixture passes 5/5. It covers policy
  bounds, effective cost/delay, snapshot invalidation, all availability states,
  powered recovery, destination changes, executable connected regions, three
  mid-action cancellation causes, authoritative waiting, instant traversal,
  and deletion of a Z 0 user without `ZLevelPositionComponent`.
- The combined movement/pathfinding matrix passes 36/36 with no skips. Dynamic
  route cost/delay, exact route invalidation, unavailable no-path behavior, and
  all static NPC execution regressions are included.
- The complete Content `FullyQualifiedName~ZLevel` matrix passes 269/269 and
  the Content Z-level unit/analyzer matrix passes 9/9.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes,
  100% warmed boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 6.9300, 13.1226, and 21.6093
  ms respectively.
- A full incremental `SpaceStation14.slnx` build succeeds with zero errors and
  27 established dependency, vulnerability, and obsolescence warnings; none
  points to this package.

### Decisions

- Keep `ZLevelDynamicTraversalComponent` server-only and mutate it through
  `ZLevelTraversalGraphSystem`. Explicit graph invalidation is the authority;
  non-networked components must not be passed to `Dirty`.
- Treat destination changes as topology and availability, power, wait delay,
  and wait cost as environment. P5.4b2 will scope both revision streams by map.
- Keep route equivalence stricter than physical execution equivalence. Route
  cost changes invalidate planning, while an already-running action only cares
  about live availability, destination, delay, frame, and support policy.
- Preserve the structural connected-region API for graph inspection and add an
  executable variant for active timers. This keeps static graph benchmarks
  allocation-stable while preventing disabled or mismatched wide tiles from
  extending a traversal.
- Execute zero-delay connectors directly instead of creating an instant
  `DoAfter`, whose synchronous completion could otherwise leave stale ownership.
- Subscribe cleanup to entity termination globally rather than to
  `ZLevelPositionComponent`; base-floor entities validly omit that component.
- Leave cabin movement, call panels, sprites, construction, and multi-stop
  elevator content in P7. P5.4b1 supplies the navigation/runtime contract.

### Completion Gate

- [x] Scope check: the diff is limited to dynamic traversal policy, graph and
      execution consumers, diagnostics, focused tests, and P5 documentation.
- [x] Invariant review: Z 0 component absence, local/world frames, moving grids,
      exact edge identity, server authority, boundary/support policy, and static
      connector compatibility were reviewed.
- [x] Automated verification: 5/5 focused dynamic, 36/36 movement/pathfinding,
      269/269 Content Z-level integration, 9/9 Content unit/analyzer, 3/3
      baselines, and the full solution build pass without skips or errors.
- [x] Performance evidence: warmed stress allocation remains 6,336 bytes at all
      three depths; graph counters distinguish dynamic outcomes and changes;
      the structural connected-region allocation benchmark remains green.
- [x] Documentation: policy ownership, invalidation, route versus execution
      semantics, lifecycle fixes, evidence, limitations, and next work are
      recorded here and in `Docs/ZLevelPathfinding.md`.
- [x] Dependency check: no WTZ Engine change is required; the paired engine
      remains at `3aaca280f628876939afcc10a9be920b3898902a`.
- [x] Git check: `git diff --check` passes except expected Windows line-ending
      notices; generated baseline and TRX artifacts remain ignored.
- [x] Mini review: no blocking dynamic-state correctness issue remains; map
      revision scope and concurrent/endurance evidence remain in P5.4b2.
- [x] Commit: the isolated `Model dynamic Z-level traversal state` parent
      commit is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: dynamic connectors are now first-class graph edges rather than
  static stairs with ad hoc teleports, and players and NPCs share one delayed,
  server-authoritative execution path.
- Finding: the focused tests caught two runtime-only lifecycle defects: dirtying
  non-networked components and validating `DoAfter` ownership before its ID was
  returned. Both now fail safely and have regressions.
- Finding: graph planning and physical execution deliberately use different
  equivalence strength, preventing needless interruption after a pure cost
  update while rejecting stale route starts.
- Finding: entity cleanup no longer assumes that Z 0 owns an explicit position
  component, closing a process-lifetime pending-state leak.
- Residual risk: topology and environment revisions are still global, so churn
  on one map can trigger validation work on another even though exact routes
  remain usable.
- Residual risk: concurrent hostile/follow consumers and prolonged dynamic
  toggling have not yet supplied a scale envelope for a public server.
- Residual risk: this contract selects an adjacent destination but does not yet
  provide a physical cabin, controls, power load, or mapper-facing elevator
  prototypes; those are P7 content responsibilities.
- Next package: P5.4b2 introduces map-scoped graph versions, concurrent NPC
  execution fixtures, prolonged invalidation tests, and the consolidated P5
  phase gate.

## Completed Package: P5.4b2 Map-Scoped Revisions And Phase Hardening

### Scope

- Replace process-global graph staleness decisions with independent topology and
  environment revisions for each live map while retaining aggregate counters for
  diagnostics.
- Prevent unrelated-map changes from rebuilding detached snapshots or forcing
  exact-edge validation and replanning of active NPC routes.
- Exercise prolonged dynamic state churn, hostile/follow planning, concurrent
  vertical traversal, cache retention, map removal, and phase-wide regressions.
- Close P5 without speculative pooling or eviction that the measured cache
  envelope does not justify.

### Acceptance Criteria

- A topology or environment change on map B leaves map A's graph version,
  detached snapshot, in-flight search, and active route unchanged.
- Removing a map evicts its retained graph snapshot and revision record without
  disturbing live maps.
- Follow and hostile HTN consumers install the same typed hierarchical route,
  and at least eight NPCs can plan and execute independent vertical routes.
- Repeated dynamic connector mutations keep retained snapshot storage bounded,
  preserve deterministic revisions, and restore an executable final edge.
- Existing Z 0, movement, interaction, combat, lighting, sound, and mapping
  regressions remain green.

### Verification Evidence

- The final focused matrix passes 11/11 with no skips. It includes six dynamic
  traversal cases, unrelated-map snapshot/search and active-route isolation,
  eight concurrent NPCs, and both follow and hostile `MoveToOperator` inputs.
- The 512-mutation churn fixture records exactly 512 environment revisions and
  state changes, retains one per-map snapshot slot, and keeps each rebuilt
  detached snapshot at or below 16 KiB.
- All eight concurrent NPCs plan, traverse, and arrive independently with zero
  budget exhaustion, replans, or execution failures. The native navigation
  cache remains at its measured pre-route high-water mark of 9 chunks and 2
  floors after execution.
- The combined movement/pathfinding matrix passes 40/40, the complete Content
  Z-level integration matrix passes 274/274, and the Content unit/analyzer
  matrix passes 9/9, all without skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes,
  100% warmed boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 6.9267, 13.2397, and 21.1442
  ms respectively.
- A full incremental `SpaceStation14.slnx` build succeeds with zero errors and
  27 established dependency, vulnerability, and obsolescence warnings; none
  points to this package.

### Decisions

- Make `ZLevelTraversalGraphVersion` map-scoped and authoritative for snapshot,
  search, and route validation. Keep global revisions as monotonic aggregate
  diagnostics only.
- Retain a map revision entry for the lifetime of its map so revisions never
  move backwards while live; evict both revision and snapshot state on map
  removal. Robust map IDs are not reused.
- Validate an active route's exact edges only when its own map version changes.
  This preserves safety while eliminating cross-map work from ordinary ticks.
- Keep follow and hostile planning on the established `MoveToOperator` path;
  separate Z-level planners would duplicate policy without adding capability.
- Do not add chunk pooling or more aggressive eviction in P5. The prolonged and
  concurrent fixtures show bounded high-water retention, and map teardown now
  removes graph-owned state.

### Completion Gate

- [x] Scope check: the diff is limited to graph revision ownership, its NPC and
      diagnostics consumers, focused scale/isolation tests, and P5 documentation.
- [x] Invariant review: Z 0, world/local floors, moving frames, map migration and
      removal, exact edge authority, async search, and dynamic state were reviewed.
- [x] Automated verification: 11/11 focused, 40/40 movement/pathfinding, 274/274
      Content integration, 9/9 Content unit/analyzer, 3/3 baselines, and the full
      solution build pass without skips or errors.
- [x] Performance evidence: 512 mutations retain one snapshot slot under the
      allocation ceiling; 8 concurrent routes do not grow native navigation
      caches; warmed stress allocation remains 6,336 bytes at every depth.
- [x] Documentation: map-version authority, lifecycle, scale evidence, decisions,
      limitations, and the P6 handoff are recorded here and in
      `Docs/ZLevelPathfinding.md`.
- [x] Dependency check: no WTZ Engine change is required; the paired engine
      remains at `3aaca280f628876939afcc10a9be920b3898902a`.
- [x] Git check: generated baseline artifacts remain ignored; staged scope,
      parent/engine status, and `git diff --check` are verified before commit.
- [x] Mini review: no blocking P5 correctness or measured retention issue remains;
      initialized mapping save/load is now the active P6 responsibility.
- [x] Commit: the isolated `Harden hierarchical Z-level pathfinding` parent commit
      is prepared for the pushed `zlevel/pathfinding` branch.

### Mini Review

- Finding: graph invalidation now has the same ownership boundary as route data:
  one map cannot make another map's snapshots, searches, or active routes stale.
- Finding: map movement, connector migration, frame changes, tile changes,
  dynamic state, and map deletion all invalidate or evict the intended map state.
- Finding: representative follow/hostile consumers and eight simultaneous NPCs
  execute the complete local/traversal/local lifecycle without shared-state leaks.
- Finding: the exploratory cache measurement exposed the native 8-tile chunk
  border high-water behavior; the useful invariant is stable retention before
  and after routing, which passes at 9 chunks and 2 floors.
- Residual risk: eight deterministic NPCs and 512 mutations are a package-scale
  envelope, not a substitute for P8 long-duration public-server load testing.
- Residual risk: dynamic elevators still provide navigation/runtime policy only;
  cabins, controls, construction, power load, and mapper-facing content remain P7.
- Next package: P6.1 inventories initialized map serialization, defines the safe
  mapping snapshot boundary, and excludes players, minds, sessions, and other
  transient round state before any atomic replacement workflow is added.

## Completed Package: P6.1 Initialized Mapping Snapshots

### Scope

- Add operation-local entity and component filters to WTZ Engine map
  serialization without changing ordinary save behavior.
- Create a read-only mapping snapshot of a map after map initialization while
  excluding players, active minds, explicitly marked runtime roots, their
  descendants, and runtime follower relationships.
- Validate only authored entities that the snapshot will retain, then route the
  mapper's manual save request through the detached snapshot representation.
- Prove that native Z-level tiles, format metadata, anchored infrastructure, and
  map-init lifecycle survive an in-memory load without replacing or mutating the
  source map.

### Acceptance Criteria

- A snapshot can serialize an initialized native Z-level map without the
  pre-init warning path or a serialization-time mutation of live entities.
- Players, active mind bodies, mind entities, explicit transient roots, and all
  descendants of excluded roots cannot be serialized or auto-included by a
  surviving reference.
- A filtered component inherited from an entity prototype stays absent after
  load, while an ordinary save immediately after the filtered operation remains
  unchanged and raises normal lifecycle events.
- Z-level validation ignores filtered transient roots, including their invalid
  runtime floor values, but still rejects retained infrastructure outside the
  declared map range.
- The loaded snapshot has no nullspace additions or invalid-UID diagnostics and
  preserves the map's configured floors, native tiles, anchored entity Z, and
  `MapInitialized` lifecycle.

### Verification Evidence

- Focused WTZ Engine filtering passes 1/1. It covers subtree exclusion,
  filtered-reference non-inclusion, prototype-component removal, lifecycle-event
  suppression, and a following ordinary save with no option leakage.
- The complete WTZ Engine `EntitySerialization` matrix passes 19/19.
- Focused initialized-map snapshot coverage passes 1/1, and the combined map
  format, mapping, and snapshot matrix passes 8/8.
- The complete Content Z-level integration matrix passes 275/275 and the Content
  unit/analyzer matrix passes 9/9, all without failures or skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 with 6,336 measured bytes,
  100% warmed boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 6.7769, 12.9018, and 21.7117
  ms respectively.
- A complete `SpaceStation14.slnx` build succeeds with zero errors. Its 708
  warnings are the checkout's established dependency, vulnerability, analyzer,
  and upstream obsolescence warnings; none identifies this package.

### Decisions

- Keep serializer filters in `SerializationOptions`, making them deterministic,
  operation-local policy supplied by the caller rather than global serializer
  state. Rejected references become invalid and cannot trigger auto-inclusion.
- Record filtered prototype components in `missingComponents`; omitting only
  their serialized data would otherwise restore them from the prototype on load.
- Use an explicit `SuppressMapSerializationEvents` flag whose zero/default value
  preserves legacy events. The WTZ snapshot suppresses them because current
  follower and device-network handlers can mutate live gameplay state.
- Make `MappingSnapshotTransientComponent` an unsaved marker on the root of a
  runtime-only subtree. Actor, active `MindContainer`, and `Mind` roots are
  recognized automatically, and ancestor traversal applies the same decision to
  their children and references.
- Keep validation and serialization on one inclusion policy. Tile layers remain
  map-owned and are always validated; only entity validation is filtered.
- Treat this as a detached representation, not a live-round save. Players,
  sessions, minds, active follower state, and other explicitly marked runtime
  state are intentionally outside the mapper-authored file contract.

### Completion Gate

- [x] Scope check: the diff is limited to operation-local engine filtering,
      initialized mapping snapshots, transient markers, Z-aware validation,
      mapper integration, focused tests, and P6 documentation.
- [x] Invariant review: Z 0 compatibility, native tiles, local entity Z, map
      lifecycle, anchored infrastructure, server-side filtering, and live-source
      immutability were exercised; moving-frame persistence retains its existing
      map-format path and receives broader fidelity coverage in P6.2.
- [x] Automated verification: 1/1 focused engine, 19/19 engine serialization,
      1/1 focused snapshot, 8/8 mapping, 275/275 Content integration, 9/9 Content
      unit/analyzer, 3/3 baselines, and a zero-error full build pass.
- [x] Performance evidence: snapshot creation is an explicit mapper command, not
      a tick/frame path; gameplay stress allocation remains 6,336 bytes with
      fully warm boundary/gravity caches at all three fixture depths.
- [x] Documentation: the contract, filters, invariants, tests, limitations, and
      next package are recorded here and in `Docs/ZLevelMapSaveLoad.md`.
- [x] Dependency check: WTZ Engine revision
      `f2ae5853f6ebbebe158951d90686d2d67e243537` is pushed on
      `zlevel/save-load`; the parent commit carries that exact submodule pointer.
- [x] Git check: `git diff --check` passes with only expected Windows line-ending
      notices; generated baselines remain ignored and both diffs contain only
      declared P6.1 files.
- [x] Mini review: the snapshot boundary and default-compatible engine API have
      no blocking focused or broad regression; atomic output and collection
      reference normalization remain explicitly assigned to P6.2.
- [x] Commit: the isolated engine and parent commits are prepared for the pushed
      `zlevel/save-load` branches.

### Mini Review

- Finding: snapshot creation no longer needs destructive save hooks; the test
  proves active follower state and every excluded live entity remain untouched.
- Finding: entity exclusion is subtree-aware and shared with Z validation, so
  transient child state cannot either leak into output or falsely reject a map.
- Finding: ordinary serializer calls keep events, components, referenced
  nullspace entities, and descendants, proving all new controls are local to one
  operation and default-compatible.
- Finding: map metadata, native floor layers, anchored upper-floor infrastructure,
  and post-init lifecycle survive the first detached load with no nullspace leak.
- Residual risk: `DeviceListComponent` and similar persistent collections can
  still contain deleted, filtered, or cross-map references that must be
  normalized in the detached representation without modifying live components.
- Residual risk: the client mapping writer still writes directly to the selected
  stream, initialized-map autosave remains disabled, and one load is not the
  required double round-trip structural proof.
- Residual risk: initialized-map Z-level editing operations remain intentionally
  blocked while save validation and replacement are not yet atomic.
- Next package: P6.2 validates the completed detached representation, normalizes
  internal references, and introduces temporary-file plus atomic-replace output
  before initialized mapping workflows are broadened.

## Completed Package: P6.2a Detached Snapshot Normalization

### Scope

- Add structured unresolved-entity-reference diagnostics to WTZ Engine load
  results while making the existing log-suppression option consistent.
- Load the read-only snapshot into a paused disposable map, run ordinary save
  hooks there, and delete it without exposing mutations to the live source map.
- Load a deep copy of the normalized representation again and require one map,
  no orphans/nullspace entities, no unresolved references, and valid Z state.
- Report normalized-reference and validated-entity counts to the mapping server.

### Acceptance Criteria

- Filtered, deleted, and cross-map references are observable without parsing
  logs or allowing them to auto-include an excluded entity.
- A legacy collection hook can remove invalid members on a disposable map while
  preserving valid same-map links and leaving the live collection unchanged.
- An invalid reference with no explicit normalization policy rejects the final
  snapshot and identifies its source component.
- Validation cannot consume or corrupt the `MappingDataNode` returned for later
  transfer and loading.
- Every temporary map is deleted on success, validation failure, or exception.

### Verification Evidence

- Focused WTZ Engine filter/diagnostic coverage passes 1/1, and the complete
  engine `EntitySerialization` matrix passes 19/19.
- The initialized-map snapshot fixture passes 1/1. It retains a same-map device
  link in both directions, normalizes filtered-player and cross-map targets,
  leaves the three-member live list unchanged, and rejects an unhandled
  `ActionOnInteract` reference without mutating its source component or leaking
  either disposable map load.
- The combined Z-level map-format/snapshot matrix passes 7/7, and traditional
  mapping/editor regressions pass 2/2.
- The namespace-scoped Content Z-level matrix passes 259/259. The broad filter
  passes 274 cases and reports one harness skip; the skipped lighting-cache case
  passes both in the namespace run and in a dedicated 1/1 rerun, covering all
  275 cases. Content unit/analyzer tests pass 9/9.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 7.0326, 12.9732, and 21.7031
  ms respectively.
- The complete solution builds with zero errors. Existing dependency,
  vulnerability, analyzer, and upstream-obsolescence warnings total the same
  established 708-warning baseline and remain unrelated to this package.

### Decisions

- Keep diagnostics in `LoadResult.InvalidEntityReferences` as structured
  engine data: source YAML UID, component, and serialized value. Logging remains
  an independent caller policy.
- Normalize only on a loaded disposable map. Current Content save hooks were
  written to mutate components, so invoking them on the live initialized map
  would violate the mapper snapshot contract.
- Give every validation load `MappingDataNode.Copy()`. `EntityDeserializer`
  removes component mappings while reading, making one node instance consumable
  rather than safely reusable.
- Permit unresolved references during the first detached load so explicit
  collection policies can clean them, then require zero unresolved references
  during the final load. Unknown scalar intent fails closed.
- Preserve the P6.1 entity/component filters during normalization and keep both
  temporary maps paused. The normalized result remains an authored map, not a
  live-round snapshot.

### Completion Gate

- [x] Scope check: the engine diff is limited to load diagnostics and its
      serialization test; the parent diff is limited to snapshot normalization,
      mapper reporting, focused coverage, and P6 documentation.
- [x] Invariant review: native tiles, map lifecycle, anchored upper-floor
      infrastructure, Z 0 compatibility, same-map references, filtered roots,
      and cross-map references are covered. Moving-frame format paths remain
      unchanged and pass the broader Z-level matrix.
- [x] Automated verification: 1/1 focused engine, 19/19 engine serialization,
      1/1 snapshot, 7/7 map-format/snapshot, 2/2 mapping regressions, 259/259
      namespace Z-level, all 275 broad cases covered, 9/9 unit/analyzer, 3/3
      baselines, and a zero-error solution build pass.
- [x] Performance evidence: this work remains outside tick/frame paths; the
      warmed gameplay baseline retains 6,336 bytes, fully warm caches, and zero
      budget exhaustion at all fixture depths.
- [x] Documentation: normalization stages, structured diagnostics, mutable-node
      handling, tests, limitations, and the next package are recorded here and
      in `Docs/ZLevelMapSaveLoad.md`.
- [x] Dependency check: WTZ Engine revision
      `a90b854ce9` is pushed on `zlevel/save-load`; the parent commit carries
      that exact submodule pointer.
- [x] Git check: both diffs pass `git diff --check` with only expected Windows
      line-ending notices; generated baselines remain ignored and no unrelated
      file is included.
- [x] Mini review: detached mutation, final fail-closed validation, reusable
      representation, and source immutability have focused and broad coverage.
- [x] Commit: the engine commit is pushed and the isolated parent commit is
      prepared for the pushed `zlevel/save-load` branch.

### Mini Review

- Finding: the source map now participates only in the first read-only
  serialization. All mutation-compatible cleanup occurs on a paused disposable
  map and both temporary loads are deleted in `finally` blocks.
- Finding: valid internal links survive with their reverse relationship, while
  filtered-player and cross-map list entries normalize without rewriting live
  state. Unhandled invalid references reject the file with component context.
- Finding: deep-copying before deserialization is required for correctness, not
  defensive ceremony; the focused test reproduced corruption when the same
  mapping node was validated and later loaded.
- Residual risk: the client/server messages still lack request correlation,
  timeout, and guaranteed errors, so concurrent or stale responses remain a
  protocol concern assigned to P6.2b.
- Residual risk: the selected destination is still truncated before transfer
  completion and uses ASCII encoding. UTF-8 temporary output, flush, and atomic
  replacement remain P6.2c.
- Next package: P6.2b correlates every manual save request and response, permits
  only one pending operation, validates before opening the file dialog, and
  guarantees an explicit server result for every authorized request.

## Completed Package: P6.2b Correlated Mapping Save Protocol

### Scope

- Carry a non-zero request ID through mapping save requests, map-data responses,
  and explicit error responses while retaining reliable-unordered delivery.
- Replace the client's response/stream race with one pending request tracker,
  deterministic stale-response rejection, and a 30-second timeout.
- Make every server-side session, authority, attached-map, snapshot-validation,
  and exception path return a correlated terminal result.
- Open the native destination dialog only after normalized YAML has passed all
  server validation and reached the matching client request.
- Present busy, timeout, malformed-response, server, and local-write failures to
  the mapper while returning a typed operation result to callers and tests.

### Acceptance Criteria

- A second save cannot replace, cancel, or share state with an active save.
- Stale, mismatched, and duplicate responses cannot complete the current task.
- A pending slot remains occupied after data arrival until dialog/write cleanup
  completes, and every exit releases it in `finally`.
- Unauthorized and invalid-map requests receive explicit errors with the exact
  request ID instead of waiting forever.
- Validated YAML arrives before the destination dialog is opened; a timeout and
  simultaneous late response have a single winner.

### Verification Evidence

- Three deterministic tracker tests pass 3/3. They cover monotonically assigned
  IDs, one pending operation, explicit end, duplicate completion, stale data and
  errors, mismatched cleanup, timeout winning over late data, and reuse.
- The real client/server mapping protocol test passes 1/1. It temporarily
  deadmins the host and receives `ServerRejected`, restores Host authority and
  receives an invalid-Z snapshot rejection, blocks a concurrent second request
  as `Busy`, then proves valid YAML reaches the headless file dialog as
  `Cancelled` rather than timing out.
- The combined map-format, initialized-snapshot, correlated-protocol,
  traditional mapping, and editor matrix passes 10/10 without skips.
- The complete Content Z-level integration matrix passes 276/276 and the
  relevant Content unit/analyzer matrix passes 12/12 without failures or skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 12.8096, 17.7008, and 25.2127
  ms respectively.
- A clean `SpaceStation14.slnx` build passes with zero errors and the checkout's
  established 708 dependency, vulnerability, analyzer, and obsolescence
  warnings; no warning identifies this package.

### Decisions

- Keep reliable-unordered delivery and make correlation explicit instead of
  depending on packet order. `uint` IDs skip zero after wrap and are scoped to
  the client process.
- Keep the request pending after its response completes. The operation owns the
  slot through the native dialog and write, preventing a second command from
  racing the same user workflow.
- Complete timeout through the same tracker method as server errors. The task
  completion source arbitrates timeout/data races and rejects the loser.
- Return a typed `MappingSaveResult` while preserving the existing UI command's
  ability to ignore the value. Tests can distinguish busy, rejection, timeout,
  cancellation, successful write, and local failure without inspecting UI.
- Defer error popups through `IUserInterfaceManager` so network and timeout
  continuations do not directly mutate UI controls from an unsafe context.
- Keep encoding and destination replacement unchanged in this package. Opening
  the dialog after transfer removes the protocol race; P6.2c owns filesystem
  durability and Unicode fidelity.

### Completion Gate

- [x] Scope check: the diff is limited to mapping protocol messages, client and
      server managers, one tracker, localized errors, focused tests, and P6
      documentation.
- [x] Invariant review: snapshot Z validation remains server-authoritative and
      precedes transfer; Z 0, native tiles, moving frames, and boundary behavior
      are unchanged and pass the complete Z-level matrix.
- [x] Automated verification: 3/3 tracker, 1/1 protocol, 10/10 mapping,
      276/276 Z-level integration, 12/12 unit/analyzer, 3/3 baselines, and a
      zero-error clean solution build pass without skips.
- [x] Performance evidence: save requests are mapper-triggered rather than
      tick/frame work; warmed gameplay remains at 6,336 bytes, 100% relevant
      cache hits, and zero PVS exhaustion at all stress depths.
- [x] Documentation: message flow, concurrency contract, timeout arbitration,
      tests, limits, and the next package are recorded here and in
      `Docs/ZLevelMapSaveLoad.md`.
- [x] Dependency check: this package requires no WTZ Engine change. The parent
      continues to pin pushed engine revision `a90b854ce9`.
- [x] Git check: `git diff --check` passes with only expected Windows line-ending
      notices; generated baselines remain ignored and the engine worktree is
      clean.
- [x] Mini review: authorization, invalid representation, valid data, duplicate,
      stale, timeout, cancellation, and cleanup paths are covered at the tracker
      or real-network level.
- [x] Commit: the isolated parent commit is prepared for the pushed
      `zlevel/save-load` branch.

### Mini Review

- Finding: the destination can no longer be opened or truncated while the server
  is still producing or validating YAML. The client first receives the exact
  matching terminal response.
- Finding: server failures that previously returned silently now complete the
  request explicitly, and malformed or stale responses cannot commandeer a
  newer save.
- Finding: request state is held through the whole user operation and released
  in one `finally`, replacing the two nullable fields whose timing depended on
  whether network data or the file dialog won first.
- Residual risk: after selection, the current engine dialog API still opens and
  truncates the destination directly, and Content still encodes YAML as ASCII.
  A crash or write failure can therefore damage an existing file.
- Residual risk: the server does not maintain a separate rate limit for a
  modified client, but snapshot requests require active Host authority and are
  not a public-player endpoint.
- Next package: P6.2c adds an engine-owned UTF-8 atomic write API, same-directory
  temporary files, physical flush, failure cleanup, destination preservation,
  and Content integration after the validated response.

## Completed Package: P6.2c Atomic Mapping Output

### Scope

- Add an engine-owned native-dialog API that accepts complete bytes and keeps
  the selected operating-system path out of Content.
- Write to a unique temporary file in the destination directory, physically
  flush it, and promote it with a same-volume overwrite rename only after every
  write succeeds.
- Remove partial temporary output on write, flush, or replacement failure while
  preserving the previous destination.
- Replace Content's ASCII stream writer with strict UTF-8 encoding without a
  byte-order mark and the new atomic engine operation.
- Preserve the correlated request slot through dialog, encoding, atomic write,
  cancellation, and exception cleanup.

### Acceptance Criteria

- Cancelling the native dialog creates no file and reports `Cancelled` through
  the existing typed mapping result.
- Creating a new destination and replacing an existing one produce exactly the
  supplied bytes with no temporary file left behind.
- An injected partial-write failure leaves an existing map byte-for-byte intact
  and removes the incomplete temporary file.
- Authored non-ASCII YAML survives strict UTF-8 encoding without a BOM or
  replacement characters.
- Existing `IFileDialogManager.SaveFile` consumers compile and behave unchanged;
  atomic output is an additive engine API.

### Verification Evidence

- Five focused WTZ Engine tests pass 5/5. They cover new-file creation,
  replacement, partial-write failure, destination preservation, temporary-file
  cleanup, exact Unicode bytes, and dialog cancellation.
- The complete WTZ Engine client unit suite passes 42/42 without failures or
  skips, including all 37 preexisting cases and the five new atomic cases.
- Content mapping unit tests pass 4/4: three correlated-request tracker cases
  plus strict UTF-8/no-BOM encoding.
- The real client/server mapping save protocol passes 1/1 and still reaches the
  headless dialog as `Cancelled` after receiving validated YAML.
- The combined map-format, initialized-snapshot, correlated-protocol,
  traditional mapping, and editor matrix passes 10/10 without skips.
- The first broad Content pass reported 275 passes plus one pool skip. That
  omitted lighting-cache case passed alone, and a second complete pass closed
  at 276/276 without failures or skips.
- Relevant Content unit/analyzer coverage passes 13/13 without skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 7.7195, 14.3521, and 21.9225
  ms respectively.
- A clean `SpaceStation14.slnx` build passes with zero errors and the checkout's
  established 708 dependency, vulnerability, analyzer, and obsolescence
  warnings; no warning identifies this package.

### Decisions

- Keep path ownership inside WTZ Engine. Content supplies complete bytes and
  receives only success/cancellation, so native paths do not become a new
  cross-layer API or leak into game logic.
- Keep the atomic API byte-oriented. Content explicitly selects strict UTF-8
  without a BOM, while future binary consumers can reuse the primitive without
  implicit text conversion.
- Create the GUID-named temporary file with `CreateNew`, write-only access, no
  sharing, asynchronous writes, `FlushAsync`, and a final
  `Flush(flushToDisk: true)` before replacement.
- Keep temporary and destination paths in the same directory and use overwrite
  move only after flush. This prevents cross-volume copy/delete behavior and
  relies on the filesystem's same-volume rename semantics.
- Propagate filesystem exceptions to `MappingManager`; its P6.2b catch path
  already reports `ClientError`, releases the pending slot in `finally`, and
  presents the localized reason.
- Leave the legacy stream-returning `SaveFile` API intact. Migrating unrelated
  callers would expand risk without improving initialized-map persistence.

### Completion Gate

- [x] Scope check: the engine diff contains one additive file-dialog API,
      atomic helper, and focused fixture; the parent diff contains only mapping
      integration, one encoding test, the engine pointer, and P6 documentation.
- [x] Invariant review: server-side snapshot validation still precedes transfer;
      request correlation, Z 0, native tiles, moving frames, boundaries, and all
      gameplay systems are unchanged and covered by the complete Z matrix.
- [x] Automated verification: 5/5 atomic engine, 42/42 engine client, 4/4
      mapping unit, 1/1 protocol, 10/10 mapping integration, 276/276 Content Z,
      13/13 relevant unit/analyzer, 3/3 baselines, and a zero-error clean build
      pass without unresolved skips.
- [x] Performance evidence: the operation is mapper-triggered rather than
      tick/frame work; gameplay retains 6,336 measured bytes, 100% relevant
      warm cache hits, and zero PVS exhaustion at every stress depth.
- [x] Documentation: encoding, temporary-file lifecycle, flush order,
      replacement semantics, error behavior, tests, residual risks, and P6.3
      follow-up are recorded here and in `Docs/ZLevelMapSaveLoad.md`.
- [x] Dependency check: WTZ Engine commit `7cbd778024` is published on
      `zlevel/save-load`, and the parent pins that exact revision.
- [x] Git check: both diffs pass `git diff --check` with only expected Windows
      line-ending notices; generated baselines remain ignored.
- [x] Mini review: cancellation, creation, replacement, Unicode, partial write,
      cleanup, protocol, pooled-test recovery, and cross-system regressions are
      covered with no blocking finding.
- [x] Commit: isolated engine and parent commits are prepared for their pushed
      `zlevel/save-load` branches.

### Mini Review

- Finding: a selected existing map is no longer opened or truncated. It remains
  untouched until a complete temporary copy has passed both logical and
  physical flushes.
- Finding: Content no longer loses authored non-ASCII text through ASCII
  replacement; UTF-8 policy is explicit and has exact-byte coverage.
- Finding: the existing P6.2b client error path composes cleanly with engine
  failures, so no second mapping-specific filesystem state machine is needed.
- Residual risk: a process or machine crash can orphan the dot-prefixed
  temporary file.
  The destination remains intact, but stale-temp scavenging is not implemented.
- Residual risk: the temporary file data is physically flushed, but directory
  metadata is not explicitly fsynced and unusual/network filesystems may offer
  weaker guarantees than local same-volume rename semantics.
- Residual risk: initialized-map autosave and Z-level create/copy/delete remain
  disabled until structural idempotence is proven rather than being enabled
  solely because destination writes are now atomic.
- Next package: P6.3a performs two automated snapshot/load cycles, compares maps,
  grids, authored entities, pipes, cables, Z boundaries, frames, atmosphere, and
  references, then documents and enforces the separate live-round persistence
  boundary.

## Completed Package: P6.3a Initialized Map Idempotence

### Scope

- Prove mapper-authored initialized maps remain structurally identical through
  two complete snapshot, YAML, and initialized-load cycles.
- Compare semantic state independently of unstable YAML ordering and runtime
  entity identifiers.
- Persist atmosphere mixtures on real non-zero native tiles without serializing
  simulation-only adjacency cells, hotspots, or processing caches.
- Preserve the existing Z 0 atmosphere schema alongside a separately named,
  versioned sparse upper-floor field.
- State explicitly that mapper snapshots are `FileCategory.Map` data and cannot
  be extended into live-round persistence by an option flag.

### Acceptance Criteria

- Map configuration, two grids, moving frame origin/transform, native tiles from
  Z -1 through Z 2, boundaries, decals, anchored infrastructure, and internal
  device references compare equal after both cycles.
- Z 0 and non-zero atmosphere cells preserve volume, temperature, and every gas
  species through both cycles.
- Player and explicit transient roots remain absent from mapper output.
- A source hotspot does not reappear after either load because fire lifecycle is
  runtime state rather than authored map data.
- The official three-floor map and all existing mapping workflows continue to
  load with the unchanged base atmosphere format.

### Verification Evidence

- Focused initialized snapshot plus official-map coverage passes 3/3, including
  the double round trip and its canonical semantic comparisons.
- The combined map-format, initialized-snapshot, correlated-protocol,
  traditional mapping, and editor matrix passes 11/11.
- The complete Content Z-level run reports 276 passes and one transient harness
  skip; the omitted aperture-cache lifecycle case passes immediately when run
  alone. All 277 cases have passing evidence with no unresolved failure.
- Relevant Content unit/analyzer coverage passes 13/13 without skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 7.0112, 13.3412, and 29.0055
  ms respectively.
- `SpaceStation14.slnx` builds with zero errors and 27 established incremental
  dependency, vulnerability, and obsolescence warnings; none identifies P6.3a.

### Decisions

- Use a dedicated `zLevelTiles` component field. Flattening both the legacy Z 0
  and upper-floor serializers would make their `version` and `data` keys
  collide; the official-map regression caught that incompatible shape.
- Keep the upper-floor format sparse by local Z and 4x4 chunks while sharing
  equal gas mixtures. The envelope and chunk size are versioned and validated
  before reading.
- Serialize only real atmosphere cells with mixtures. `NoGridTile` adjacency
  cells, active hotspots, excited groups, and processing queues are rebuilt or
  discarded because mapper files describe authored state, not a paused round.
- Preserve in-memory copy fidelity separately: the custom copier clones every
  tile atmosphere, including runtime cells, while file serialization applies
  the narrower persistent boundary.
- Keep `MappingSnapshotSystem` read-only over an initialized source and document
  live-round save as a separate future contract rather than a mode switch.

### Completion Gate

- [x] Scope check: the diff contains one upper-atmosphere serializer, the
      minimal component/access compatibility changes, one expanded integration
      fixture, snapshot contract remarks, and P6 documentation.
- [x] Invariant review: Z 0 and non-zero layers, local/world Z, a translated and
      rotated moving frame, explicit/open boundaries, anchoring, references,
      transient filtering, and initialized lifecycle are covered.
- [x] Automated verification: 3/3 focused/official, 11/11 mapping matrix, all
      277 broad cases with the single harness skip recovered individually,
      13/13 unit/analyzer, 3/3 baselines, and a zero-error full build pass.
- [x] Performance evidence: persistence adds no gameplay-loop work; the stress
      baseline remains allocation-stable at 6,336 measured bytes with fully warm
      relevant caches and no exhausted PVS budget.
- [x] Documentation: persistent/transient atmosphere state, double-round-trip
      semantics, live-round boundary, evidence, and P6.3b ownership are recorded
      here and in the two Z-level mapping documents.
- [x] Dependency check: P6.3a requires no engine change; the parent continues to
      pin published WTZ Engine commit `7cbd778024`.
- [x] Git check: the package diff passes `git diff --check` apart from expected
      Windows line-ending notices, and WTZ Engine remains clean.
- [x] Mini review: schema coexistence, semantic idempotence, transient filtering,
      atmosphere fidelity, and runtime-state exclusions have no blocking finding.
- [x] Commit: this package is saved as the isolated `Prove initialized Z-level
      map idempotence` commit and pushed to the parent `zlevel/save-load` branch.

### Mini Review

- Finding: mapper-authored initialized maps now have a repeatable semantic
  round-trip oracle instead of relying on one successful load or YAML text
  equality.
- Finding: persistent atmosphere is no longer silently lost above Z 0, and the
  official-map case protects the independent legacy and upper-floor schemas
  from key collisions.
- Finding: players, explicit transient roots, and source hotspot state remain
  outside both generated snapshots as intended.
- Residual risk: initialized create/copy/delete operations are still blocked;
  file idempotence does not prove mutation of a live initialized object graph is
  transactional or lifecycle-correct.
- Residual risk: active hotspots and processing caches cannot resume a live
  round. That remains intentionally outside mapper persistence.
- Next package: P6.3b tests and enables initialized floor mutation with authored
  filtering, lifecycle correctness, atmosphere handling, failure containment,
  and validated atomic autosave.

## Completed Package: P6.3b1 Initialized Floor Mutation

### Scope

- Enable authenticated create, copy, and delete floor requests on maps whose
  entity lifecycle has already reached `MapInitialized`.
- Reuse the mapper snapshot's persistent entity/component boundary instead of
  defining a second notion of authored state for floor operations.
- Preflight copied entity graphs, preserve references, anchoring, map-init
  lifecycle, and local Z offsets, then replace the selected grid's target floor.
- Replace target tiles, decals, and persistent atmosphere while discarding
  hotspots, excited groups, adjacency cells, and processing queues.
- Preserve players and explicit transient entities when their authored parent is
  replaced, and relocate runtime roots before a deleted floor disappears.
- Keep the map-wide range valid across multiple grids and handle Robust's
  automatic empty-grid deletion without accessing a deleted grid.

### Acceptance Criteria

- The real client network request path can create, copy, and delete floors on an
  initialized server map under Mapping authority.
- Copied cable, pipe, and boundary roots reach `MapInitialized`, remain anchored,
  and occupy only the requested target floor.
- Target-only tiles, decals, authored roots, atmosphere, and hotspot state are
  replaced; the source floor remains unchanged.
- Player, direct actor, and explicit transient child roots survive copy/delete;
  excluded children are detached before their authored parent is removed.
- Deleting an edge floor contracts the global range only after no other grid on
  the map still uses that local Z.
- A final tile-only floor may remove its now-empty grid, but an operation that
  would strand surviving entities or authored decals is rejected before mutation.
- A post-mutation initialized mapper snapshot validates successfully.

### Verification Evidence

- The connected initialized lifecycle fixture passes 1/1 through actual client
  network requests and covers range contraction refusal, create/delete, copy,
  two grids, and final empty-grid removal.
- The combined map-format, initialized-snapshot, save-protocol, and initialized
  mutation matrix passes 10/10 without failures or skips.
- The complete Content Z-level integration matrix passes 278/278 without
  failures or skips.
- Relevant Content unit/analyzer coverage passes 13/13 without skips.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 7.3808, 13.4672, and 20.9562
  ms respectively.
- `SpaceStation14.slnx` builds with zero errors and 24 established dependency,
  vulnerability, and obsolescence warnings; none identifies P6.3b1.

### Decisions

- Expose the existing snapshot entity/component predicates as the single
  mapper-authored boundary used by both persistence and initialized mutation.
- Deserialize copied authored roots into the initialized map with YAML IDs kept
  long enough to prove a complete root mapping before deleting target state.
- Clone only real atmosphere mixtures. Target runtime atmosphere structures are
  removed from all known sets and queues, remaining adjacency links are cleared,
  and simulation rebuilds them normally.
- Set copied target tiles before removing stale target-only coordinates so a
  non-empty replacement cannot transiently trigger Robust's empty-grid deletion.
- Treat floor editing as selected-grid mutation over a map-wide continuous range.
  Edge deletion consults all other grids; deleting an interior floor clears its
  selected-grid contents but cannot create a hole in the min/max representation.
- Refuse direct initialized range contraction. Mappers must delete edge floors,
  allowing the operation to perform entity, atmosphere, multi-grid, and runtime
  safety checks first.

### Completion Gate

- [x] Scope check: the package contains initialized floor mutation, atmosphere
      lifecycle cleanup, one connected integration fixture, and P6 documentation.
- [x] Invariant review: Z 0/non-zero floors, map-local Z, initialized lifecycle,
      multiple grids, anchoring, transient roots, actors, decals, atmosphere,
      server authority, and automatic empty-grid deletion were reviewed.
- [x] Automated verification: 1/1 connected lifecycle, 10/10 mapping matrix,
      278/278 broad Content Z-level, 13/13 unit/analyzer, 3/3 baselines, and a
      zero-error full solution build pass without skips.
- [x] Performance evidence: all new scans and serialization occur only for an
      explicit mapper command; the gameplay baseline remains at 6,336 measured
      bytes with fully warm relevant caches and no exhausted PVS budget.
- [x] Documentation: initialized mutation semantics, empty-grid policy,
      atmosphere lifecycle, multi-grid range ownership, evidence, limitations,
      and P6.3b2 ownership are recorded here and in both mapping documents.
- [x] Dependency check: P6.3b1 requires no engine change; the parent continues
      to pin published WTZ Engine commit `7cbd778024`.
- [x] Git check: the package passes `git diff --check` apart from expected
      Windows line-ending notices, and WTZ Engine remains clean.
- [x] Mini review: the connected failure discovered for a final tile-only grid
      is fixed and covered; no blocking mutation or lifecycle finding remains.
- [x] Commit: this package is saved as the isolated `Enable initialized Z-level
      floor mutations` commit and pushed to the parent `zlevel/save-load` branch.

### Mini Review

- Finding: initialized mapping no longer needs a special pre-init map solely to
  create, clone, or remove an authored floor.
- Finding: floor copy now shares snapshot filtering, so runtime players and
  transients cannot silently become authored clones.
- Finding: target atmosphere replacement is explicit and removes stale runtime
  references instead of leaving a mixture from the previous floor behind.
- Finding: multi-grid range ownership and synchronous empty-grid deletion are
  now handled deliberately rather than depending on mutation order.
- Residual risk: entity graph cloning is fully preflighted, but arbitrary
  exceptions after destructive mutation begins do not yet have a general
  in-memory rollback journal. Deterministic tile/decal/atmosphere paths and the
  final-grid dependency checks contain the known failure modes.
- Residual risk: a continuous min/max range cannot represent a deleted interior
  floor. The selected grid is emptied while that logical Z remains addressable.
- Next package: P6.3b2 routes initialized autosave through the validated detached
  snapshot, uses temporary same-directory output and atomic promotion, and tests
  cancellation/failure cleanup without turning mapper files into live-round saves.

## Completed Package: P6.3b2 Initialized Mapping Autosave

### Scope

- Share one canonical snapshot-to-YAML formatter between host-requested mapping
  saves and server-side autosave.
- Permit the existing autosave scheduler to retain initialized map roots and
  serialize them through the validated mapper-authored snapshot boundary.
- Write strict UTF-8 without a BOM to a same-directory `CreateNew` temporary,
  flush it, and atomically promote it to a never-overwritten destination.
- Preserve the pre-init map/grid legacy serializer path while refusing
  initialized grid-only output, which cannot represent complete map-owned
  Z-level state.
- Cover validation failure, partial writes, collisions, foreign temporaries,
  transient filtering, source immutability, and loading the resulting file.

### Acceptance Criteria

- An initialized map root can remain registered with the existing autosave
  timer; an initialized grid without its map root is rejected explicitly.
- Invalid authored Z-level state produces no visible destination or temporary
  file, and a corrected map can be saved on a later attempt.
- A successful autosave is strict UTF-8 without a BOM, has a unique timestamped
  filename, contains one complete map snapshot, and loads successfully.
- Players and explicit transient roots are absent from the loaded autosave while
  authored tiles, entities, range, lifecycle, and Z-level state survive.
- A partial temporary write is removed. Existing destinations and temporary
  files not created by the operation are never replaced or deleted.
- Manual host saves retain their canonical output contract, and pre-init
  autosaves retain their legacy serializer path.

### Verification Evidence

- The real initialized autosave/load fixture passes 1/1. It covers timer
  registration, initialized grid refusal, failed validation with an empty output
  directory, strict UTF-8 output, transient filtering, reload, and source-map
  immutability.
- Snapshot, save-protocol, and autosave integration pass 4/4; the complete
  mapping/save-load/mutation matrix passes 11/11 without failures or skips.
- Atomic writer coverage passes 4/4 for promotion, partial failure cleanup,
  existing destination/foreign temporary preservation, and timestamp collision
  suffixing. Relevant Content unit/analyzer coverage passes 17/17.
- The 279-case Content Z-level matrix has passing evidence for every case and no
  failures. Broad parallel runs each completed 278 cases and transiently skipped
  one old pooled fixture; both alternately omitted lighting and concurrent-NPC
  cases passed immediately when run alone on the same binary.
- Generated 3-, 6-, and 10-floor baselines pass 3/3 at 6,336 measured bytes,
  100% warm boundary/gravity cache hits, and zero PVS budget exhaustion or
  fail-open candidates. Local measured times are 6.8437, 13.9465, and 21.5024
  ms respectively.
- `SpaceStation14.slnx` builds with zero errors and 105 established dependency,
  vulnerability, content-obsolescence, and analyzer warnings; none identifies
  P6.3b2.

### Decisions

- Keep detached normalization and validation in `MappingSnapshotSystem`, then
  expose its canonical YAML text so manual save and autosave cannot drift.
- Autosaves are append-only snapshots. Timestamp collisions receive a numeric
  suffix; an existing destination is never an overwrite target.
- The server writer owns a temporary only after `CreateNew` succeeds. This
  prevents cleanup from deleting a colliding file created by another writer.
- Keep persistence synchronous with the existing mapping autosave scheduler.
  Snapshot generation is entity-manager work and cannot safely move to a worker
  thread; disk exposure remains atomic and the operation runs only when due.
- A failed scheduled save remains registered and retries at the next configured
  interval, allowing a mapper to repair invalid authored state.
- Initialized autosave means mapper-authored map persistence, not restoration of
  players, minds, sessions, chat, objectives, or other live-round state.

### Completion Gate

- [x] Scope check: this package contains canonical YAML formatting, initialized
      map autosave, atomic server output, focused tests, and P6 documentation.
- [x] Invariant review: map-root ownership, Z 0/non-zero state, lifecycle,
      transient exclusion, validation failure, UTF-8, collision, cleanup, retry,
      and legacy pre-init behavior were reviewed.
- [x] Automated verification: 1/1 initialized lifecycle, 4/4 shared persistence,
      11/11 mapping matrix, 4/4 writer, 17/17 unit/analyzer, complete 279-case
      passing evidence, 3/3 baselines, and a zero-error full build pass.
- [x] Performance evidence: autosave adds no steady-state Z-level work and the
      warmed gameplay baseline remains at 6,336 bytes with fully warm caches.
- [x] Documentation: ownership, atomic protocol, retry policy, failure cleanup,
      unsupported grid-only snapshots, and live-round boundary are recorded.
- [x] Dependency check: P6.3b2 requires no engine change and continues to use
      the published WTZ Engine pin already recorded by P6.3b1.
- [x] Git check: declared files pass diff checks; generated autosaves/baselines
      remain outside the worktree, and WTZ Engine remains clean.
- [x] Mini review: the expected initialized-grid refusal now logs as a warning;
      no blocking persistence, atomicity, lifecycle, or source-mutation finding
      remains.
- [x] Commit: this package is saved as isolated `Autosave initialized mapping
      snapshots` commit and pushed to the parent `zlevel/save-load` branch.

### Mini Review

- Finding: manual and automatic initialized saves now use exactly the same
  normalized and validated mapper-authored representation.
- Finding: a server interruption can leave at most a dot-prefixed temporary,
  never a partially promoted autosave; ordinary write failures clean it.
- Finding: scheduling remains lightweight between intervals and copies its due
  entries before mutating registration state.
- Residual risk: snapshot generation is synchronous and may pause a very large
  mapping session while due. Moving entity serialization off-thread would be
  unsafe without a separate immutable capture boundary.
- Residual risk: legacy pre-init map/grid autosave still uses Robust's direct
  serializer. Retrofitting that unrelated path with detached snapshots is not
  part of initialized Z-level persistence.
- Next package: the P6 phase gate reviews all save/load contracts together,
  repeats end-to-end persistence evidence, freezes the live-round boundary, and
  decides whether P7 can begin without another persistence implementation.

## Completed Phase Gate: P6 Initialized Map Persistence

### Scope

- Review P6.1 through P6.3b2 as one persistence pipeline instead of accepting
  isolated package results as proof that the phase composes correctly.
- Inventory every Content and engine map-save entry point and freeze the exact
  boundary between mapper-authored snapshots, legacy file maintenance, and
  future live-round persistence.
- Repeat the engine primitives, full Content persistence chain, relevant unit
  matrix, and warmed stress baselines on the published P6 revisions.
- Decide whether P7 content can rely on initialized mapping, mutation,
  validated save/autosave, and repeated load without another P6 implementation.

### Acceptance Criteria

- Manual mapping save and initialized map-root autosave share one normalized,
  validated, canonical YAML representation.
- Entity/component filtering, structured invalid-reference diagnostics,
  correlated network completion, strict UTF-8, and atomic output retain their
  default-compatible engine boundaries.
- Initialized create/copy/delete and two complete snapshot/load cycles preserve
  authored native tiles, multiple grids, frames, infrastructure, boundaries,
  decals, references, and persistent atmosphere while excluding round state.
- All direct serializer commands outside the mapper workflow are identified and
  cannot be mistaken for the validated WTZ snapshot contract.
- Focused end-to-end evidence passes on clean parent and engine worktrees, and
  no P6 residual risk blocks authored P7 content from being mapped and reloaded.

### Verification Evidence

- WTZ Engine filtering/diagnostic integration passes 1/1 and atomic file writer
  coverage passes 5/5 on revision `7cbd778024`.
- The Content map-format, initialized snapshot, correlated protocol, initialized
  mutation, and initialized autosave matrix passes 11/11 without failures or
  skips. Relevant Content unit/analyzer coverage passes 17/17.
- The immediately preceding P6.3b2 broad gate provides passing evidence for all
  279 Content Z-level integration cases and a zero-error full solution build;
  this documentation-only phase gate changes no compiled source.
- Two gate baseline runs pass 3/3. The confirming run measures 6,336 bytes at
  3, 6, and 10 floors, 100% boundary/gravity cache hits, zero PVS budget
  exhaustions/fail-open candidates, and local times of 6.7141, 13.5253, and
  29.4362 ms respectively.

### Decisions

- Close P6 without adding another serializer. The implemented pipeline already
  satisfies the mapper-authored goal, and widening it would conflate map files
  with live-round restoration.
- Treat mapping UI save and initialized map-root autosave as the supported safe
  WTZ paths. Both enter `MappingSnapshotSystem` before bytes become visible.
- Keep Robust `savemap ... true`, Content `persistencesave`, and Content
  `resave` outside this guarantee. They are force/debug, live-persistence, and
  bulk file-maintenance paths respectively; none is an alias for a validated
  initialized mapper snapshot.
- Require future P7 authored components and content to join the P6 semantic
  round-trip fixtures whenever they own persistent vertical state.
- Keep live-round restoration as a future, separately versioned format with
  explicit players, minds, sessions, simulation queues, and recovery semantics.

### Completion Gate

- [x] Scope check: this gate changes only P6/P7 planning documentation and does
      not alter a serializer, gameplay system, prototype, or engine revision.
- [x] Invariant review: Z 0/non-zero tiles, local/world frames, moving grids,
      map-root ownership, boundaries, atmosphere, initialized lifecycle,
      transients, references, and multi-grid ranges were reviewed together.
- [x] Automated verification: 1/1 engine filtering, 5/5 engine atomic writer,
      11/11 Content persistence integration, 17/17 relevant Content unit, and
      inherited passing evidence for all 279 broad cases complete without a
      blocker.
- [x] Performance evidence: two 3/3 baseline runs pass; the confirming run is
      allocation-stable at 6,336 bytes with fully warm caches and zero exhausted
      PVS budget at every fixture depth.
- [x] Documentation: supported entry points, excluded command paths,
      live-round boundary, residual risks, evidence, and P7 dependencies are
      recorded here and in `Docs/ZLevelMapSaveLoad.md`.
- [x] Dependency check: no engine change is required; the parent continues to
      pin the clean, published WTZ Engine revision `7cbd778024`.
- [x] Git check: parent and engine began clean and synchronized with their
      remotes; only the declared documentation is included in this gate.
- [x] Mini review: no blocking persistence, lifecycle, reference, encoding,
      atomicity, or scope finding remains; known limits stay explicit.
- [x] Commit: this gate is saved as isolated `Close Z-level persistence phase`
      commit and pushed to the parent `zlevel/save-load` branch.

### Mini Review

- Finding: P6 now forms one auditable pipeline from authenticated request or
  scheduled map root through detached validation to durable mapper output and
  repeated initialized reload.
- Finding: the architectural audit found no supported mapping path that bypasses
  the canonical snapshot. Legacy direct commands remain intentionally named and
  documented as separate contracts.
- Finding: P7 can add persistent roofs, shafts, elevators, weather metadata, and
  flight content against a stable initialized-map lifecycle and round-trip
  oracle.
- Residual risk: snapshot creation is synchronous and very large mapping maps
  can pause at save time; it remains off the steady-state gameplay path.
- Residual risk: arbitrary exceptions after initialized floor mutation starts
  do not have a general rollback journal, although known destructive failure
  modes are preflighted and covered.
- Residual risk: local filesystem rename semantics, stale dot-prefixed files
  after machine failure, and legacy direct serializers retain their documented
  limits.
- Next package: P7.1a inventories Robust roof/weather primitives and defines one
  Z-aware vertical-surface and sky-column query contract with bounded caching,
  invalidation, metrics, mapping semantics, and focused Z 0/moving-grid tests.

## Completed Package: P7.1a Sky-Column Contract And Cache

### Scope

- Give vertical surfaces an independent `Weather` boundary channel instead of
  borrowing atmosphere or visibility policy.
- Resolve whether a grid-local tile and floor have an open boundary chain to
  the boundary above the map's declared maximum floor.
- Bound repeated queries with a process-local LRU cache, per-column revisions,
  a maximum boundary-check budget, metrics, and administrative presentation.
- Preserve legacy Z 0 maps, moving-grid local geometry, and deterministic shared
  client/server results.
- Define the contract consumed by authored roofs and later weather gameplay
  without migrating either consumer prematurely.

### Acceptance Criteria

- A query checks every adjacent `Weather` boundary from its local origin through
  the top boundary and returns a typed termination reason.
- Invalid grids, levels, configurations, failed boundary resolution, and budget
  exhaustion never report exposed sky.
- The cache is bounded, uses real least-recently-used eviction, recomputes
  without changing results, and performs allocation-free hot lookups.
- Tile, non-zero tile, boundary, map-configuration, grid-lifecycle, and budget
  changes cannot leave a valid stale result.
- Empty-column reads do not allocate Robust map chunks.
- Z 0 maps default to one local floor; moving frame origins change world-floor
  projection without invalidating or duplicating local cached geometry.

### Verification Evidence

- A non-incremental full solution build completes in 1m49s with zero errors.
  Its 706 package-vulnerability, dependency-pruning, analyzer, and obsolescence
  warnings are the established repository warning set.
- The focused sky, budget, and metrics matrix passes 15/15. This includes top
  boundaries, tile and provider invalidation, forced-close precedence, Z 0,
  moving frames, shared determinism, clamps, fail-closed budgeting, true LRU
  eviction, chunk non-allocation, metric reset, and hot allocation coverage.
- The complete Content `FullyQualifiedName~ZLevel` filter passes 284 cases with
  one pooled skip; the skipped pre-existing aperture-cache case passes 1/1 in
  isolation. All 285 cases therefore have passing evidence and none failed.
- Content's Z-level unit/analyzer filter passes 9/9. The generated 3-, 6-, and
  10-floor baseline passes 3/3.
- The confirming Debug baseline records 6,336 measured bytes at every depth,
  100% warm boundary and sky-cache hits, zero sky misses, evictions, or budget
  exhaustions, and local times of 12.2895, 18.6039, and 37.3581 ms. Timings are
  comparison evidence, not release thresholds.

### Decisions

- `Weather` is independent from `Atmosphere`, `Visibility`, and traversal. A
  grate may admit rain and sight while retaining unrelated channel policy.
- The map's existing tile-above rule remains the default vertical surface. An
  explicit provider may open that boundary, while a roof provider on the
  highest floor may close the otherwise open top boundary.
- Cache keys contain grid UID and local `ZLevelTileIndices`. World-Z overloads
  convert through the grid's current frame origin before lookup, so translated,
  rotated, and vertically displaced moving grids reuse local geometry.
- Per-column revisions exist only while that column owns cached entries. Edit
  invalidation increments a scalar and never scans vertical geometry; stale
  entries are rebuilt lazily.
- Cache capacity defaults to 4,096 and clamps from 64 through 65,536. Boundary
  checks default to 64 and clamp from 1 through 4,096. Exhausting checks fails
  closed rather than claiming exposure from a partial column.
- Keep Robust's legacy `RoofComponent`, `IsRoofComponent`, and planar weather
  query unchanged in this package. P7.1b authors real vertical content, and P7.3
  migrates weather presentation/gameplay to this shared contract.

### Completion Gate

- [x] Scope check: only the shared sky contract, Weather channel, cache/budgets,
      metrics/presentation, marker semantics, tests, and documentation changed.
- [x] Invariant review: Z 0, sparse reads, local/world frames, moving grids,
      shared determinism, boundary precedence, and conservative failure were
      explicitly covered.
- [x] Automated verification: 15/15 focused, passing evidence for all 285 broad
      integration cases, 9/9 unit/analyzer, 3/3 baseline, and zero-error project
      builds complete.
- [x] Performance evidence: hot lookup allocation coverage and schema-version 4
      3/6/10-floor captures show bounded caches, 100% warm hits, and stable
      measured allocation.
- [x] Documentation: API semantics, effective CVar clamps, invalidation,
      observability, consumer boundary, limitations, and evidence are recorded
      here and in `Docs/ZLevelVerticalContent.md`.
- [x] Dependency check: P7.1a requires no WTZ Engine change; the clean engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: generated baselines remain ignored; declared source and docs
      pass whitespace checks, with only checkout line-ending notices.
- [x] Mini review: FIFO ordering was identified and replaced with tested LRU;
      no unresolved correctness or cache-lifecycle finding remains.
- [x] Commit: prepared as isolated `Add bounded Z-level sky exposure` commit on
      `zlevel/vertical-content`; remote verification follows the commit.

### Mini Review

- Finding: a single typed shared query now answers vertical sky exposure without
  coupling weather, rendering, roofs, or atmosphere into one monolithic system.
- Finding: the hot path performs a dictionary lookup plus a no-allocation LRU
  node move; edits invalidate only the affected XY column.
- Finding: top-floor roofs are representable because the top boundary is part of
  the query even though there is no authored floor above it.
- Residual risk: no production weather or roof consumer calls this query yet;
  the package intentionally establishes infrastructure rather than claiming
  visible weather behavior.
- Residual risk: local Debug timings include pooled test and machine noise and
  must not be treated as production throughput guarantees.
- Next package: P7.1b adds authored roofs, grates, catwalks, and shafts with
  construction/destruction, mapping prototypes, save/load coverage, and a demo
  topology built on the Weather and existing boundary channels.

## Completed Package: P7.1b Authored Vertical Surfaces

### Scope

- Let non-empty tile definitions open a channel subset through the vertical
  boundary directly below them without weakening the default solid-floor rule.
- Add production lattice, interior grate, shaft, catwalk-bridge, and top-roof
  semantics using existing specialized boundary consumers.
- Wire grate and shaft stacks into floor interaction, fabrication, mapping,
  construction, deconstruction, and double save/load round trips.
- Make local navigation distinguish visual shaft tiles from supporting floors
  and react to catwalk/provider, tile, Z-position, and boundary-mode changes.
- Preserve legacy `FloorElevatorShaft`, unconfigured Z 0 maps, and Robust's
  planar roof/weather behavior.

### Acceptance Criteria

- Existing non-empty tiles open no vertical channels unless their definition
  opts in; empty tiles and `ExplicitOnly` policy retain their prior defaults.
- Grates/lattice support bodies while admitting the selected atmosphere,
  visibility, weather, sound, effects, projectile, and explosion channels.
- A shaft is visibly non-empty but admits every boundary channel and provides no
  support; a catwalk over it restores only Body support and is removable.
- AI treats an uncovered shaft as space, a catwalk-covered shaft as floor, and
  rebuilds after content or map-policy changes without querying ECS in parallel.
- Grate and shaft floor items modify only the user's world Z, and every authored
  tile/entity/provider remains equivalent after two save/load cycles.
- A normal solid floor above acts as a constructible roof; a durable hidden
  mapper marker can seal the otherwise unrepresentable top boundary.

### Verification Evidence

- The six package-specific content and construction cases pass 6/6. They cover
  channel subsets, forced-close support, provider Z relocation, actor support,
  real grate/shaft item placement on Z 1, unchanged Z 0 tiles, navigation
  invalidation, and double map round trips.
- The complete pathfinding plus vertical-content matrix passes 27/27. The
  atmosphere, sky, mapping-format, and movement consumer matrix passes 38/38.
- The broad Content Z-level/placement filter passes 290 cases with one pooled
  concurrent-NPC skip; that exact case passes 1/1 in isolation. All 291 cases
  therefore have passing evidence and none failed.
- Content's Z-level unit/analyzer filter passes 9/9. A targeted warning rebuild
  reports no analyzer warning in the new or modified package files.
- A non-incremental full solution build completes in 1m32s with zero errors and
  the established 706 package, dependency, obsolescence, and analyzer warnings;
  no package warning remains in the diff.
- The schema-version 4 baseline passes 3/3. Measured 3/6/10-floor times are
  10.5917, 15.9291, and 22.8443 ms with 6,336 bytes at every depth, 100% warm
  boundary/sky hits, and zero measured boundary or sky evictions.

### Decisions

- `ContentTileDefinition.zLevelOpenChannels` describes the boundary immediately
  below that non-empty tile. Tile policy is the baseline; forced opens apply
  next and forced closes retain final precedence.
- `Lattice` remains map-atmosphere content for upstream compatibility.
  `ZLevelGrate` is separate so an interior grate owns mutable air and connects
  rooms vertically without venting directly to the map atmosphere.
- `FloorZLevelShaft` is separate from legacy `FloorElevatorShaft`. Existing maps
  may use the old tile decoratively, so changing its support semantics would be
  an unacceptable Z 0 regression.
- Catwalk is an anchored Body-closing provider. This works over lattice/plating
  unchanged and turns a shaft into a real removable bridge without blocking
  light, gas, sound, weather, effects, shots, or explosions.
- Pathfinding owns a fixed 256-entry support mask per cached chunk. The main
  thread prepares exact tile/provider support, then the existing breadcrumb
  geometry remains parallel. Provider-free tiles avoid ECS boundary queries.
- Ordinary upper floors are production roofs. `ZLevelRoofMarker` exists only
  for the highest boundary, where no upper-floor tile can be authored inside
  the declared map range. A player-facing top-cap item is intentionally deferred.

### Completion Gate

- [x] Scope check: the diff is limited to tile/boundary semantics, vertical
      content and recipes, navigation support, focused tests, and documentation.
- [x] Invariant review: Z 0, legacy elevator tiles, local/world Z construction,
      moving-frame reuse, shared boundary precedence, server-authoritative
      placement, and parallel-nav safety were explicitly reviewed.
- [x] Automated verification: 6/6 package cases, 27/27 path/content, 38/38
      consumers, passing evidence for all 291 broad cases, 9/9 unit/analyzer,
      3/3 baseline, and a zero-error full build complete.
- [x] Performance evidence: support storage is fixed per cached nav chunk and
      reused; 3/6/10-floor captures retain 6,336 measured bytes, hot caches, no
      measured eviction, and healthy local timings.
- [x] Documentation: channel tables, construction/mapping workflow, roof model,
      legacy split, persistence, navigation behavior, and limitations are
      recorded here and in `Docs/ZLevelVerticalContent.md`.
- [x] Dependency check: P7.1b requires no WTZ Engine change; the clean engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: generated round-trip/baseline artifacts remain ignored;
      whitespace checks pass with only repository checkout line-ending notices.
- [x] Mini review: explicit-only invalidation and literal-ID analyzer warnings
      were found during the gate and corrected; no open correctness finding
      remains in the declared package.
- [x] Commit: prepared as isolated `Add authored Z-level vertical surfaces`
      commit on `zlevel/vertical-content`; remote verification follows it.

### Mini Review

- Finding: the tile baseline removes the need for persistent invisible opening
  entities while preserving providers for dynamic structures such as catwalks.
- Finding: interior grate and lattice now have intentionally distinct
  atmosphere ownership despite sharing visual/channel behavior.
- Finding: navigation derives support from the same Body contract used by
  falling, so non-empty shaft art can no longer masquerade as walkable floor.
- Residual risk: `ZLevelRoofMarker` is mapper-only and P7.3 has not yet migrated
  weather rendering/gameplay from Robust's planar roof query.
- Residual risk: the fixed support array adds a small amount of memory per
  cached nav chunk; large public-server profiles remain part of P8.
- Residual risk: ramps, elevators, and flight each need their own movement and
  lifecycle contracts rather than being represented as special shaft cases.
- Next package: P7.2a implements powered elevator cabins, mapped stops,
  controls, deterministic travel, and dynamic traversal lifecycle.

## Completed Package: P7.2a Powered Physical Elevators

### Scope

- Add one authoritative physical cabin per same-grid, same-tile, mapper-named
  shaft network, with unique served-floor stops and cabin/landing controls.
- Validate actor floor, destination, source stop, power, travel distance,
  configuration, complete directed shaft geometry, and rider capacity before
  starting a server-timed request.
- Move the cabin and only its captured riders that remain aboard at arrival;
  cancel safely on power, cabin transform, stop topology, or geometry failure.
- Add compact BUI state/progress, APC idle/travel load, appearance states,
  bounded metrics, prototypes, locale, focused tests, and mapping documentation.
- Keep initialized-map round trips, mapping mutation tools, pathfinding/AI
  execution, doors, queues, construction, and product polish in P7.2b.

### Implementation

- A network key is `(grid UID, local shaft tile XY, trimmed shaft ID)`. The grid
  and local tile remain stable when a moving grid changes world XY or frame
  origin, while unrelated columns can reuse the same ID.
- Cabins, stops, and controls are indexed by network. Control refresh no longer
  scans every elevator on the server, avoiding quadratic map-start behavior.
- Requests fail closed for duplicate cabins/stops, missing source/target stops,
  cross-floor actors, forged landing destinations, unavailable power, busy
  cabins, closed directed traversal stacks, excessive riders, and malformed or
  out-of-range mapper values.
- Travel time is derived from local-floor distance and globally capped. The UI
  receives the authoritative timestamp only for presentation.
- Rider capture is exact-grid, exact-tile, exact-local-Z, unanchored physics
  content. Capacity exits before sorting an oversized rider set. Arrival
  rechecks that each captured entity still occupies the source cabin tile.
- The cabin authors only a `Body` close below its current floor. The surrounding
  shaft continues to use the existing independent atmosphere, visibility,
  sound, interaction, effects, projectile, explosion, and traversal channels.
- Runtime movement state and captured riders are not data fields. Mapper-owned
  configuration and the completed cabin floor remain the persistence boundary.
- `zlevelmetrics` and its reset path now include elevator registrations, active
  travel, outcomes, rejection categories, riders, and effective hard limits.

### Verification

- Focused elevator integration: 6/6 passed for authoritative delayed movement,
  cabin/player/item transport, same-floor authority, landing spoof rejection,
  power cancellation, duplicate stops, closed shafts, malformed limits,
  capacity, and riders leaving before arrival.
- Complete Content Z-level integration matrix: 297/297 passed on the final diff.
- Content Z-level unit/analyzer matrix: 9/9 passed.
- Stress baseline: 3/3 passed at 10.5840, 16.0735, and 24.4104 ms for 3, 6,
  and 10 floors. Every depth retained 6,336 measured bytes, 100% warm
  boundary/sky cache hits, and zero measured boundary/sky evictions.
- `SpaceStation14.slnx` built with zero errors and 27 established warnings. No
  analyzer warning originates from the elevator package.

### Completion Gate

- [x] Scope check: the diff is limited to physical elevator runtime/UI/content,
      observability, tests, and the roadmap documentation it changes.
- [x] Invariant review: unconfigured Z 0 maps remain opt-in and unchanged;
      local/world frames, moving grids, server authority, anchoring, sparse
      tiles, and independent boundary channels were reviewed.
- [x] Automated verification: 6 focused, 297 broad integration, 9 unit/analyzer,
      3 baseline cases, and a full solution build pass on the final diff.
- [x] Performance evidence: controls are network-indexed; rider movement and
      configuration are bounded; process-local counters are exposed; final
      3/6/10-floor timings and allocations remain healthy.
- [x] Documentation: architecture, mapper workflow, authority, limits,
      persistence boundary, current product limits, commands, and test evidence
      are recorded here and in `Docs/ZLevelElevators.md`.
- [x] Dependency check: P7.2a requires no engine change; the clean WTZ Engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: `git diff --check` passes with only checkout line-ending
      notices; generated baseline/test output remains ignored.
- [x] Mini review: coordinate parenting, power-fixture drift, process-local test
      metrics, global control scanning, and clamped malformed limits were found
      and corrected before the final matrix.
- [x] Commit: prepared as isolated `Add powered Z-level elevator cabins` commit
      on `zlevel/vertical-content`; remote hash verification follows it.

### Mini Review

- Finding: a cabin can be a physical support surface without pretending to be a
  new tile layer; its one `Body` provider composes with the shaft's other open
  channels and follows the cabin's local Z.
- Finding: capturing riders at departure and revalidating them at arrival gives
  deterministic server behavior without parenting arbitrary gameplay entities
  to a synthetic moving container.
- Finding: process-local metrics intentionally survive map recycling. Tests now
  reset only when they assert local deltas instead of depending on test order.
- Finding: strict global configuration limits are safer and easier to diagnose
  than silently clamping malformed mapping values.
- Residual risk: the engine broadphase must still enumerate every entity
  intersecting an extremely dense cabin tile before the server can enforce the
  128-rider cap; P8 scale profiling should include deliberate item piles.
- Residual risk: P7.2a does not yet prove double save/load round trips or
  initialized-floor mutation for cabins/stops, and never resumes live in-flight
  requests. Those are explicit P7.2b persistence tasks.
- Residual risk: AI cannot yet wait for, call, enter, or execute the physical
  cabin, and player calls are not queued. Doors, emergency controls,
  construction, animation, and cross-grid shafts remain product work.
- Next package: P7.2b binds cabins into dynamic traversal/pathfinding, adds
  initialized-map round trips and mapping mutations, and hardens malformed and
  lifecycle cases before elevator content is mapper-complete.

## Completed Package: P7.2b Elevator Navigation And Mapping Hardening

### Scope

- Bind the P7.2a physical cabin to the existing hierarchical traversal graph,
  path search, exact route validation, traversal execution, and NPC steering.
- Preserve one physical cabin while initialized mapping tools copy and delete
  authored floors, with explicit refusal to delete the cabin's current floor.
- Prove authored elevator configuration and graph semantics across two
  initialized save/load cycles while excluding every in-flight runtime field.
- Harden power, topology, owner lifecycle, malformed configuration, sparse
  stops, bidirectional middle stops, contention, and observability.

### Implementation

- Every valid landing contributes only its nearest served neighbor below and
  above to `ZLevelTraversalGraphSystem`. Sparse shafts work, intermediate stops
  cannot be skipped, and 64 stops imply at most 128 directed physical edges.
- Edges use world/local frame coordinates, full captured-edge resolution, direct
  landing support, directed shaft boundaries, configured call/per-level costs,
  map-scoped revisions, and strict finite/global bounds.
- The elevator system owns one navigation operation per user and cabin. It calls
  the cabin to the exact source, revalidates the waiting user, then starts the
  destination ride. Zero-time recursion, explicit cancellation, owner deletion,
  stop deletion, and power loss all release state deterministically.
- The shared traversal executor delegates physical-stop edges to this lifecycle;
  route validation now resolves the complete edge so one middle stop can safely
  expose both directions. NPC steering calls and rides the same cabin without a
  parallel elevator-specific pathfinder.
- Stops close only the `Body` boundary below their landing, preserving support
  while the cabin is absent without closing atmosphere, light, sound, projectile,
  or traversal channels.
- Power changes invalidate the owning map's environment revision. Cabin/stop
  registration and movement change topology; tile, boundary, and frame events
  recognize elevator columns without scanning unrelated maps.
- Floor copy serializes stops and other authored roots but excludes cabins on
  both source and target. Floor deletion cancels trips through removed stops and
  rejects a floor containing the physical cabin before mutating it.
- Initialized snapshots preserve shaft IDs, labels, limits, timing, power policy,
  and navigation costs. A snapshot taken during travel loads an idle cabin at
  its last completed floor with no target, timer, passengers, or route owner.
- `zlevelmetrics` now exposes physical-elevator navigation ownership, edge
  validation, starts, completions, cancellations, and rejections.

### Verification

- The complete focused elevator suite passes 14/14. It covers travel, authority,
  power, topology, malformed costs, support, sparse and three-stop graphs, both
  directions from a middle stop, call/ride recursion, contention, cancellation,
  and route-owner deletion.
- Physical NPC execution, initialized mapping mutation, and initialized
  double-round-trip persistence pass 3/3 in focused runs. The NPC starts with the
  cabin on the other floor, completes one call and one ride, and needs no replan.
- The complete Content integration filter containing `ZLevel` passes 306/306
  with no failures or skips. Content unit/analyzer coverage passes 9/9.
- The schema-version 4 stress baseline passes 3/3. Measured 3/6/10-floor times
  are 10.4115, 15.2435, and 33.3945 ms with 6,560/6,336/6,336 bytes, 100% warm
  boundary/sky/gravity cache hits, and zero measured evictions.
- A non-incremental `SpaceStation14.slnx` build completes in 2m43s with zero
  errors and 695 established warnings. A separate non-incremental integration
  build reports zero warnings in any new or modified package file.

### Decisions

- Physical elevators extend the one traversal graph instead of introducing a
  second pathfinder or converting the cabin into a static connector component.
- Route edges describe stable service topology, not transient cabin occupancy.
  This keeps the owner's captured route valid while the cabin is being called;
  server execution enforces one owner and rejects competing starts.
- Stops are the persistent landing/support object. Cabins are unique physical
  resources, so floor copy clones stops but never cabins and floor deletion must
  not silently destroy one.
- Runtime travel and navigation are intentionally absent from initialized
  snapshots. Resuming a live round remains a separate persistence product.
- Mapper-authored navigation cost/configuration is static after registration;
  power and topology remain the supported dynamic policies and own revision
  events.

### Completion Gate

- [x] Scope check: the diff is limited to elevator graph/runtime integration,
      mapping and persistence policy, observability, tests, and documentation.
- [x] Invariant review: Z 0 opt-in behavior, local/world frames, moving grids,
      independent boundary channels, direct support, server authority, and
      map-scoped invalidation were reviewed.
- [x] Automated verification: 14 elevator, 3 focused consumer/persistence, 306
      broad integration, 9 unit/analyzer, 3 baseline, targeted warning, and full
      solution build checks pass.
- [x] Performance evidence: graph output is bounded to two edges per stop and 64
      stops per network; navigation counters are exposed; the final 3/6/10-floor
      baseline retains hot caches, bounded allocations, and no evictions.
- [x] Documentation: architecture, mapping workflow, persistence boundary,
      navigation ownership, limits, metrics, and product limitations are updated
      here and in the elevator, pathfinding, and main Z-level documents.
- [x] Dependency check: P7.2b requires no WTZ Engine change; the clean engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: `git diff --check` passes with only checkout line-ending notices;
      generated baseline snapshots remain ignored.
- [x] Mini review: exact middle-stop resolution, stop-provided support, malformed
      costs, navigation metric semantics, mapping cabin preservation, owner
      deletion, and incomplete focused-test selection were found and corrected.
- [x] Commit: prepared as isolated `Integrate physical elevators with Z-level
      navigation` commit on `zlevel/vertical-content`; remote verification
      follows it.

### Mini Review

- Finding: representing nearest-neighbor physical service as ordinary typed
  traversal edges lets every existing route budget, version, and execution
  diagnostic continue to apply.
- Finding: a landing needs independent `Body` support; relying on the moving
  cabin would make an otherwise valid destination disappear from local nav.
- Finding: copying stops while preserving one cabin gives initialized mapping a
  deterministic authoring workflow and avoids duplicate-network corruption.
- Residual risk: a busy cabin remains visible in route snapshots so non-owner AI
  may briefly plan it and be rejected at execution. Fair queues and contention
  soak tests belong to P8 rather than changing the stable-edge contract here.
- Residual risk: travel is still a discrete timed transition without doors,
  interlocks, emergency controls, animation, recipes, or cross-grid shafts.
- Residual risk: initialized mapping snapshots intentionally reset an in-flight
  cabin; arbitrary live-round continuation remains outside the P6/P7 contract.
- Next package: P7.3 applies the completed sky-column contract to Z-aware weather
  presentation and gameplay, including roof changes and moving frames.

## Completed Package: P7.3a Shared Weather Exposure Policy

### Scope

- Define one shared typed policy for weather exposure at a local tile, world
  floor, or entity without coupling rendering or audio into sky geometry.
- Preserve the exact planar behavior of legacy maps while making configured
  maps dimensional and fail closed.
- Prove local tile eligibility, same-floor blockers, complete sky columns, and
  moving-frame conversion before production presentation consumes the API.

### Implementation

- `WeatherExposureState` reports exposed, invalid coordinate/grid/level, local
  tile rejection, planar roof, anchored blocker, and sky-blocked outcomes. A
  sky rejection also retains the underlying typed sky termination.
- The historical `TileRef` overload follows its original empty-tile, planar
  roof, tile-weather, and anchored-blocker order on unconfigured maps.
- Configured maps resolve the exact `ZLevelTileIndices`, reject out-of-range
  floors, check the local tile definition, filter the planar anchored lookup by
  inherited local Z, and then require a complete exposed Weather column.
- Planar roof data is deliberately ignored after a map opts into Z levels. It
  has no floor coordinate, so applying it would incorrectly roof every level at
  the same XY; authored tile boundaries and roof markers own that policy.
- World-floor queries convert through the current grid frame. Entity queries
  derive the inherited grid and local floor, treat valid unobstructed map space
  as exposed, and reject nullspace or malformed state.

### Verification

- The focused weather-policy fixture passes 3/3. It covers legacy Z 0 roofs and
  dry tiles, configured grates and roof markers, exact-floor anchored blockers,
  entity queries, invalid levels, complete columns, and moving frame origins.
- A hot loop of 1,000 configured exposure queries allocates no more than 512
  bytes in total, proving no allocation proportional to visible-tile count.
- The complete Content integration filter containing `ZLevel` passes 308 cases
  with one pooled skip; the skipped aperture-cache case passes 1/1 in isolation,
  giving passing evidence for all 309 cases. Content unit/analyzer coverage
  passes 9/9.
- The schema-version 4 stress baseline passes 3/3. Measured 3/6/10-floor times
  are 11.0122, 15.4494, and 23.2621 ms with 6,336 bytes at every depth, 100% warm
  boundary/sky/gravity cache hits, and zero measured evictions.
- A non-incremental `SpaceStation14.slnx` build completes in 2m41s with zero
  errors and the same 695 established warnings as P7.2b. No warning references
  a new or modified weather-policy file.

### Decisions

- Weather exposure is policy layered over `SharedZLevelSkyExposureSystem`, not
  another geometric cache or a new `ZLevelTrace` channel implementation.
- Local tile eligibility is checked before the column. An empty local tile keeps
  Robust's exposed-space behavior, while a non-empty dry floor rejects weather
  even under open sky.
- `BlockWeatherComponent` remains planar storage but is filtered by inherited
  local Z on configured maps. This preserves existing content without allowing
  a blocker on one floor to suppress every floor at that XY.
- P7.3a does not invent weather damage where upstream has no gameplay consumer.
  P7.3b migrates the real stencil and ambient-audio consumers under explicit
  budgets and observability.

### Completion Gate

- [x] Scope check: the diff is limited to the shared policy/result, focused
      tests, and weather/roadmap documentation.
- [x] Invariant review: legacy Z 0 ordering, local/world frames, moving grids,
      exact-floor blockers, independent Weather boundaries, and fail-closed
      invalid state were reviewed.
- [x] Automated verification: 3 focused, all 309 broad cases covered, 9
      unit/analyzer, 3 baseline, and the full non-incremental solution build pass.
- [x] Performance evidence: 1,000 hot policy calls remain within 512 bytes; the
      3/6/10-floor baseline retains bounded allocations and fully hot caches.
- [x] Documentation: policy order, legacy/configured boundary, APIs, rationale,
      limitations, tests, and next consumer package are recorded here and in the
      vertical-content and main Z-level documents.
- [x] Dependency check: P7.3a requires no WTZ Engine change; the clean engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: `git diff --check`, tree scope, and dependency state are checked
      before the isolated commit; generated baseline snapshots remain ignored.
- [x] Mini review: overload compatibility, nullable grid resolution, dimensional
      roof policy, exact blocker floors, and a missing direct allocation proof
      were reviewed and corrected.
- [x] Commit: prepared as isolated `Define shared Z-level weather exposure`
      commit on `zlevel/vertical-content`; remote verification follows it.

### Mini Review

- Finding: keeping `WeatherExposureState` in shared Content gives gameplay,
  rendering, and audio one policy while leaving the bounded sky cache reusable
  and geometrically focused.
- Finding: the old `TileRef` overload must retain its passed tile on legacy maps;
  reconstructing it unconditionally would be a subtle Z 0 compatibility change.
- Finding: same-XY anchored enumeration is safe only after filtering by inherited
  local Z, because engine snap grids remain planar.
- Residual risk: production weather still uses the legacy stencil and a planar
  radius-three audio flood fill until P7.3b migrates those consumers.
- Residual risk: map-space exposure has no tile/roof policy by definition; future
  planet weather volumes would need an explicit non-grid spatial contract.
- Next package: P7.3b builds a bounded active-world-floor weather mask, migrates
  ambient audio to entity exposure, and adds presentation metrics and visual QA.

## Completed Package: P7.3b Bounded Weather Presentation

### Scope

- Migrate the production weather stencil from planar `TileRef` queries to the
  shared typed exposure policy on the viewport's active world floor.
- Replace the allocating planar ambient-audio flood fill with one deterministic,
  bounded exact-floor query per listener update.
- Add client budgets, fail-closed behavior, diagnostics, allocation evidence,
  and real OpenGL pixel coverage without adding new weather damage gameplay.

### Implementation

- `ZLevelWeatherPresentationSystem` owns reusable grid/context/run buffers and a
  per-client-frame budget. It converts the viewed world floor through each
  intersecting grid frame, including legacy map-grids, and evaluates only valid
  represented local floors.
- Blocked tiles are compressed into horizontal local-space runs and retained in
  deterministic grid order. The stencil draws those batches with each grid's
  current transform, preserving rotated and moving-grid presentation.
- Tile-check capacity is preflighted before policy evaluation. Run capacity is
  validated before publication. Either exhaustion discards the partial plan and
  masks the complete viewport for that frame; arbitrary indoor leaks are never
  selected by iteration order.
- Ambient audio checks deterministic squared-distance offsets inside radius
  three on the listener's exact inherited local floor. Direct and nearby
  exposure are distinct typed results; blocked, invalid, and budget-exhausted
  results retain the upstream fully occluded behavior. One result is reused for
  every weather status effect in the update.
- Three archived client CVars independently bound mask tile checks, retained
  runs, and audio checks. `zlevelrendermetrics`, its reset path, and the debug
  overlay expose planning, rendering, timing, audio, budget, and fail-closed
  counters.
- The real visual-capture fixture now records covered and exposed rain on Z 2
  and Z 3. Its full-bright and tile-weather overrides are local, reversible test
  setup; production tile policy is unchanged.
- Real client startup exposed older content-sandbox violations outside weather:
  sound snapshot publication now uses allowed `Interlocked` operations, mapping
  network completions are deferred to the UI thread, and snapshot encoding
  explicitly rejects malformed UTF-16 before using BOM-free `Encoding.UTF8`.
  These narrow hardening fixes preserve the P4/P6 contracts and make the actual
  sandboxed client loadable by the P7 visual gate.

### Verification Evidence

- Weather presentation passes 5/5 focused cases and 8/8 together with P7.3a.
  Coverage includes legacy planar roofs, moving frames, active world-floor
  selection, atomic tile/run exhaustion, exact-floor audio, and CVar clamps.
- The complete Content Z-level integration matrix has passing evidence for all
  314 cases: 313 passed in one broad run and its one pooled aperture-cache skip
  passed 1/1 in isolation. The combined Content unit/mapping filter passes 14/14,
  including strict malformed-UTF-16 rejection.
- Across 128 hot mask builds, total thread allocation remains at or below 8,192
  bytes. The freshly rebuilt schema-version 4 baseline passes 3/3 at 10.8001,
  15.6784, and 23.4932 ms for 3/6/10 floors with 6,336 bytes at every depth,
  100% warm boundary/sky/gravity hits, zero measured misses, and no PVS budget
  exhaustion.
- The real OpenGL fixture on an NVIDIA RTX 3070 passes 24/24 pixel assertions.
  Covered Z 2 has RMS difference 0.000000, exposed Z 3 has 0.056199, and the
  active contrast gap is 0.056199. Its 1,003 mask plans, 49,147 tile checks,
  6,573 retained runs, and 1,003 render frames report zero fail-closed plans,
  frames, or budget exhaustion.
- A non-incremental `SpaceStation14.slnx` build completes in 2m28s with zero
  errors and the same 695 established warnings as P7.3a. Dedicated
  non-incremental client and integration scans complete with code 0 and no
  warning attributed to a modified production or test file.
- The final sandboxed visual launch repeats 24/24 after that build. Diff scope,
  whitespace, dependency state, and staged content are reviewed before the
  isolated package commit.

### Decisions

- Weather presentation consumes `SharedWeatherSystem`; it does not duplicate
  sky geometry, add a weather-specific trace, or move renderer/audio policy into
  the shared cache.
- Budgets are client-frame presentation limits. Tile/run exhaustion fails closed
  visually and audio exhaustion fully occludes, keeping degradation predictable.
- The mask retains ordinary map-grid participation from the upstream stencil.
  Explicitly excluding the map would have been a subtle legacy Z 0 regression.
- Map-space remains exposed by the shared policy. Bounded spatial planet-weather
  volumes require their own future contract rather than overloading grid roofs.

### Completion Gate

- [x] Scope check: weather presentation, its diagnostics/tests/capture, and only
      the sandbox fixes required by the real client gate are included.
- [x] Invariant review: legacy Z 0, local/world frames, moving and rotated grids,
      map-grids, exact-floor audio, independent Weather channels, and fail-closed
      exhaustion were reviewed.
- [x] Automated verification: focused, broad, isolated pooled, unit/mapping, and
      freshly compiled schema-version 4 baseline tests pass.
- [x] Performance evidence: hot allocation, 3/6/10-floor snapshots, mask/render
      counters, and GPU pixel evidence are captured above.
- [x] Documentation: architecture, CVars, behavior, limitations, and evidence are
      recorded here and in the vertical-content and overview documents.
- [x] Dependency check: P7.3b requires no WTZ Engine change; the clean engine
      remains pinned to published revision `7cbd778024`.
- [x] Git check: final non-incremental build, warning attribution, repeat visual
      launch, `git diff --check`, tree scope, clean engine state, and staged
      review pass; generated capture/baseline artifacts remain ignored.
- [x] Mini review: consumer search, map-grid compatibility, fail-closed ordering,
      sandbox constraints, residual risks, and P7.4a handoff are recorded.
- [x] Commit: prepared as isolated `Present weather on active Z levels` on
      `zlevel/vertical-content`; push and local/remote hash verification follow.

### Mini Review

- Finding: whole-plan failure is necessary for weather. Rendering a budget-sized
  prefix would leak rain indoors according to grid/tile iteration order.
- Finding: horizontal runs materially reduce draw calls while preserving exact
  tile policy and current grid transforms.
- Finding: executing the real sandboxed client caught API-policy failures that
  ordinary compilation and headless tests could not observe.
- Residual risk: a live admin experiment did not observe a dynamically cleared
  non-zero Z tile in the connected client's inventory before timeout. Persisted
  and integration replication coverage remains green, but explicit long-running
  live delta verification belongs in P8 hardening.
- Residual risk: weather is still a map-wide status effect and local tile policy;
  spatial storms and arbitrary non-grid planet volumes are not represented.
- Next package after the gate: P7.4a defines flight movement, gravity, and
  collision semantics before trace, projectile, AI, content, and mapping work.

## Completed Package: P7.4a Flight Movement, Gravity, And Collision Contract

### Scope

- Define one shared opt-in capability and typed API for controlled movement
  between native local Z floors.
- Integrate flight with the existing vertical solver, artificial gravity,
  weightlessness, `Body` boundaries, planar fixture isolation, moving frames,
  replication, lifecycle invalidation, and observability.
- Preserve map safety by serializing authored capability parameters while
  excluding active flight and target runtime state.
- Leave controls, species, jetpacks, stamina, visuals, trace/projectile policy,
  AI execution, and demo mapping to the P7.4b consumer package.

### Implementation

- `ZLevelFlightComponent` authors hover offset, vertical acceleration, and
  maximum speed. Its active flag and grid-local floor/offset target are
  networked without `DataField`, so normal saves always load the capability
  inactive.
- `SharedZLevelSystem` exposes typed start, local/world target, stop, query, and
  active-count APIs plus cancellable start, started, target-changed, stopped,
  and boundary-blocked events. Invalid configuration, current position,
  target, map, grid, body, anchor, and container states have distinct results.
- Start attempts validate before mutation and preserve inherited local height
  when explicit Z state is materialized. A cancelled attempt creates no
  position or kinematics component.
- Active movement uses bounded acceleration and stopping speed inside the
  existing continuous `LocalZOffset` integrator. Each crossing consults the
  shared `Body` channel; a closed boundary clamps and retargets to contact once,
  allowing the body to sleep without a per-tick retry loop.
- Flight forces weightlessness and permits ordinary XY weightless movement.
  Stopping clears controlled velocity, refreshes native gravity, and wakes the
  body so a connected generator plane can regain control. Managed gravity now
  also honors independent weightlessness overrides.
- Targets remain local when `ZLevelFrameComponent.Origin` moves. Fixtures keep
  the established discrete-world-floor collision contract: hovering does not
  create fractional cross-floor contacts.
- Active flight ends on capability removal, anchoring, containment, invalid
  body type, grid reparenting, malformed state, or a map-range change that
  invalidates its target.
- The no-flight hot path checks the active-flight index before touching the
  component query. Metrics, `zlevelmetrics`, and the debug overlay expose
  starts, stops, targets, updates, crossings, blocks, invalidations, and active
  count. Stress artifacts advance to schema version 5.

### Verification Evidence

- Eight connected flight tests pass. They cover hover under powered artificial
  gravity, two open crossings, moving-frame target stability and replication,
  one-shot closed-boundary contact, gravity restoration, external weightless
  precedence, typed validation and cancellation with no side effects,
  map-range invalidation, anchor/container rejection, and collision by
  discrete world floor.
- Flight plus metrics pass 10/10. The combined movement, map-format, and flight
  regression set passes 31/31. The final complete Content Z-level/placement
  matrix passes 322/322 with no failures or skips.
- The final Content unit/mapping filter passes 11/11. The freshly compiled
  schema-version 5 baseline passes 3/3 with 100% measured boundary, sky, and
  gravity cache hits, zero measured misses, zero PVS budget exhaustion, and
  zero flight starts or updates in the neutral workload.
- The final 3/6/10-floor measurements are 10.7544, 16.1705, and 25.6144 ms with
  6,560/6,336/6,336 bytes. A repeated fast-path capture measures 10.6339,
  15.6298, and 22.6518 ms with 6,336 bytes at every depth; the 224-byte
  first-case variation is initialization noise rather than steady-state flight
  cost.
- A final non-incremental `SpaceStation14.slnx` build completes in 1m28s with
  zero errors and 706 established solution warnings. Dedicated attribution
  finds no warning in any modified production or test file.
- `git diff --check` passes. The WTZ Engine worktree is clean and remains pinned
  to published revision `7cbd778024`; this package requires no engine change.

### Decisions

- Flight is a consumer mode inside the native local-Z solver, not a separate-map
  teleport system or a second physics controller.
- The target is grid-local by default. World-target callers must request the
  explicit conversion so moving ships retain ship-relative flight plans.
- `Body` remains the only movement boundary channel. Projectile, visibility,
  interaction, and other channels do not inherit flight rules implicitly.
- Active flight is runtime state. Mapping stores capability parameters only;
  controls and content may not make an authored entity load already airborne.
- Collision stays discrete by world floor. Visual interpolation and vertical
  trace consumers must not reinterpret the hover offset as a planar fixture Z.

### Completion Gate

- [x] Scope check: movement/gravity/collision capability, API, metrics, tests,
      and documentation only; P7.4b consumers remain separate.
- [x] Invariant review: Z 0 compatibility, local/world frames, inherited height,
      moving grids, finite/range validation, cancellation, open/closed `Body`
      boundaries, gravity resumption, independent weightlessness, and
      discrete-floor collision were reviewed.
- [x] Automated verification: 10 focused, 31 movement/map, 322 broad, 11
      unit/mapping, and 3 final baseline cases pass.
- [x] Performance evidence: the no-flight indexed fast path restores the
      6,336-byte steady-state baseline and reports zero neutral flight work.
- [x] Documentation: state, APIs, lifecycle, collision, save boundary,
      observability, evidence, and P7.4b ownership are recorded here and in
      `ZLevelFlight.md`, `ZLevelVerticalContent.md`, and `ZLevel.md`.
- [x] Dependency check: no WTZ Engine change; clean submodule remains pinned to
      published revision `7cbd778024`.
- [x] Git check: non-incremental build, warning attribution, generated-artifact
      ignore state, diff whitespace, tree scope, and dependency state pass.
- [x] Mini review: cancellation side effects, inherited height, malformed/out-of-
      range origins, closed-boundary sleep, neutral hot-path allocation, and
      consumer boundaries were reviewed and corrected or recorded.
- [x] Commit: prepared as isolated `Define native Z-level flight physics` on
      `zlevel/vertical-content`; push and local/remote hash verification follow.

### Mini Review

- Finding: checking the active-flight index before the component query removes
  all steady-state capability overhead from worlds with no flyers.
- Finding: start validation must precede `EnsureZLevelEntity`; otherwise a
  cancelled content policy silently mutates vertical state and velocity.
- Finding: preserving inherited local height avoids snapping a parented entity
  to local Z 0 when flight materializes its explicit position.
- Finding: retargeting to a blocked contact is necessary for bounded work. An
  unchanged unreachable target would keep the body awake forever.
- Residual risk: there is intentionally no player-facing action, species,
  jetpack, stamina, sprite, or demo map yet; ordinary players cannot activate
  this capability until P7.4b supplies content and authoritative controls.
- Residual risk: flight-aware trace/projectile behavior, AI execution, visual
  height presentation, and explicit mapping round-trip content tests remain
  P7.4b work. The runtime fields are structurally non-serializable already.
- Next package: P7.4b integrates controls/content/mapping, trace and projectile
  consumers, AI execution, and gameplay interruption policy through the typed
  API and events established here.

## Completed Package: P7.4b1 Flight Controls, Content, And Mapping

### Scope

- Add authoritative player actions for starting, stopping, ascending, and
  descending through the native flight solver without adding a second movement
  controller.
- Integrate existing jetpacks and intrinsically flying mobs while preserving
  ownership of capability components and pre-existing flight state.
- Stop active flight through typed lifecycle reasons when the user becomes
  incapacitated, stunned, knocked down, thrown, buckled, detached, or loses the
  capability.
- Exercise authored flight content and initialized-map snapshot/load behavior on
  the official three-floor mapping fixture.
- Keep trace/projectile policy and explicit AI route execution in P7.4b2.

### Implementation

- `ZLevelFlightControlsComponent` owns networked action configuration and runtime
  action references. `SharedZLevelFlightControlSystem` grants and removes those
  actions in response to capability, parent, map, and replication lifecycle.
- Player actions invoke only the typed shared flight API. They validate native
  map configuration, capability, current body state, and target bounds on the
  authoritative side before mutating flight state.
- `SharedJetpackSystem` permits activation in ordinary weightlessness and on a
  configured native Z-level gravity grid. Runtime ownership flags ensure teardown
  removes or stops only the capability, controls, and flight state it granted.
- Existing intrinsic flight remains active across jetpack activation/deactivation,
  while disabling native configuration invalidates jetpack-provided flight.
- `FlyingMobBase` supplies intrinsic capability; player-controlled dragons also
  receive controls. The official mapping station contains one filled jetpack at
  Z 0 for repeatable floor-to-floor tests.
- Runtime active state, target height, and action entity references are excluded
  from mapping snapshots. A loaded snapshot is grounded and receives fresh
  actions from normal lifecycle setup.
- Native map lookup rejects components already stopping, closing a stale-config
  lifecycle path found by the mapping test.

### Evidence

- The final focused flight/content/map matrix passes 14/14 in 1m13s.
- The broad Content integration filter covers 327 cases: 326 pass and the one
  established deliberate cache-invalidation case remains skipped; there are no
  failures.
- Content unit/mapping filters pass 18/18.
- The process-local baseline runner passes all 3/3 stress depths. Three, six, and
  ten floors complete in 10.1246, 15.3204, and 24.9917 ms respectively, each at
  6,336 bytes with 100% warm boundary/sky/gravity cache hits, zero PVS budget
  exhaustion, and zero neutral flight work.
- A non-incremental single-worker `SpaceStation14.slnx` build passes in 2m26s
  with zero errors. Its 695 warnings are established solution warnings; path
  attribution reports zero warning in any modified production or test file.
- `git diff --check` passes apart from Git's informational LF-to-CRLF notices.
  WTZ Engine is clean and remains pinned to published revision `7cbd778024`;
  this package requires no engine change.

### Decisions

- Capability and player controls are separate components. AI and passive flying
  entities do not receive action-bar entities merely because they can fly.
- Controls remain event-driven; dormant flight capability adds no per-tick scan
  to the neutral stress fixtures.
- Jetpacks keep their legacy behavior on unconfigured maps. A configured native
  grid applies its authored gravity and vertical-boundary policy instead.
- Ownership is explicit because blindly removing a shared capability on jetpack
  shutdown would break species or effects that already supplied flight.
- Reparenting between grids stops the current flight plan as required by P7.4a.
  A capable user may explicitly start a new plan in the destination grid; no
  stale local-frame target is carried across the ownership boundary.
- Empty space is not made walkable. P7.4b2 will add explicit flight-aware
  navigation and execution without weakening ordinary planar pathfinding.

### Completion Gate

- [x] Scope check: controls, capability content, jetpack ownership, typed
      interruptions, mapping fixture, tests, and documentation only.
- [x] Invariant review: Z 0 compatibility, configured/unconfigured gravity,
      local targets, reparenting, server authority, replication, bounds,
      capability loss, and active-state ownership were exercised.
- [x] Automated verification: 14 focused, 327 broad cases covered, 18
      unit/mapping, and 3 baseline cases pass without a new failure.
- [x] Performance evidence: all neutral fixtures report zero flight work and the
      same 6,336-byte steady-state allocation at every tested floor depth.
- [x] Documentation: controls, ownership, interruptions, persistence boundary,
      content, evidence, and remaining consumer work are recorded here and in
      `ZLevelFlight.md`, `ZLevelVerticalContent.md`, and `ZLevel.md`.
- [x] Dependency check: no WTZ Engine change; the clean submodule remains pinned
      to published revision `7cbd778024`.
- [x] Git check: focused and broad tests, full build, warning attribution,
      whitespace, generated-artifact ignore state, scope, and dependency state
      pass.
- [x] Mini review: lifecycle ordering, component ownership, rollback, direct
      activation, intrinsic flight, mapping normalization, and grid changes were
      reviewed and corrected or recorded.
- [x] Commit: prepared as isolated `Add native Z-level flight controls` on
      `zlevel/vertical-content`; push and local/remote hash verification follow.

### Mini Review

- Finding: Robust component startup/shutdown subscriptions are exclusive. A
  typed capability-changed event avoids duplicate lifecycle handlers while
  keeping control ownership synchronized.
- Finding: direct jetpack activation must enforce the same native-grid policy as
  the normal action path and roll back every modifier when setup fails.
- Finding: jetpack ownership flags are necessary to preserve intrinsic flyers
  and already-active flight across equipment teardown.
- Finding: snapshot tests exposed that a stopping map component could briefly be
  treated as live configuration; lifecycle-aware lookup closes that window.
- Residual risk: player-facing reason popups expose a generic typed reason rather
  than bespoke prose for every interruption.
- Residual risk: a cross-grid reparent deliberately stops flight and does not
  silently restart the jetpack plan in a different local frame.
- Residual risk: flight-aware trace/projectile height and explicit AI
  navigation/execution are not part of this package.
- Next package: P7.4b2 integrates hover-height traces and projectiles, then adds
  explicit flight navigation edges and execution without marking empty space as
  ordinary walkable area.

## Completed Package: P7.4b2a Continuous Flight Trace And Combat

### Scope

- Extend the shared trace point with bounded continuous height while preserving
  the existing floor-center API for every ordinary caller.
- Use active shooter and entity-target flight heights for authoritative hitscan
  range, crossing geometry, and bounded physical trajectory timing.
- Preserve discrete world-floor fixture collision and defer explicit flying-NPC
  path planning/execution to P7.4b2b.

### Implementation

- `ZLevelTracePoint` now carries local/world offsets and derived continuous
  heights. Offsets are finite values in `[0, 1)`; legacy constructors default to
  `0.5`.
- Vertical traces cross real integer deck planes. Segment contact points use
  offset zero above a boundary and the greatest representable offset below one
  beneath it, while cumulative distances remain exact continuous XYZ values.
- `GetFlightTraceZOffset()` exposes inherited active-flight height and returns
  the compatibility center for every inactive or malformed entity.
- Hitscan derives continuous source and active entity-target endpoints before
  validating three-dimensional range and issuing the shared `Projectile` trace.
- Ballistic routes replicate source/target offsets, stamp the projectile at the
  source height, compute each crossing from continuous endpoint geometry, and
  retain contact-side offsets on intermediate floors.
- Coordinate targets remain floor-centered. Planar fixtures continue to filter
  only by effective discrete world Z; no fractional collision layer is added.

### Evidence

- The three new asymmetric-offset trace, hitscan, and ballistic cases pass 3/3.
- Complete trace, hitscan, ballistic, and flight consumer classes pass 72/72.
- The complete Content `FullyQualifiedName~ZLevel` matrix covers 330 cases: 329
  pass, the established concurrent-path fixture remains deliberately skipped,
  and there are zero failures.
- Content structural and mapping unit tests pass 18/18.
- The repeated schema-version 5 3/6/10-floor baseline passes 3/3 at 16.9534,
  19.9935, and 23.1440 ms. Every run allocates 6,336 bytes, has 100% warm
  boundary/sky/gravity cache hits, zero PVS budget exhaustion, and zero neutral
  flight updates. A first isolated 86.1608 ms ten-floor sample did not reproduce
  and retained identical allocation/cache counters.
- The non-incremental single-worker solution build passes in 2m46s with zero
  errors and 691 established warnings. A dedicated non-incremental integration
  rebuild attributes zero warning to any modified production or test file.
- `git diff --check` passes apart from informational LF-to-CRLF notices. WTZ
  Engine is clean and remains pinned to published revision `7cbd778024`; this
  package requires no engine change.

### Decisions

- Continuous height belongs to trace endpoints, not to a new global 3D physics
  body model. Specialized consumers opt in while old callers retain exact
  center-to-center behavior.
- Integer planes are the canonical boundary geometry for half-open floor slabs.
  The old center offsets still cross at the same XY positions as before.
- An active projectile route snapshots its endpoint heights at launch. Target
  movement or later flight retargeting does not bend an in-flight shot.
- Intermediate ballistic offsets describe the contacted side of a deck; planar
  collision remains authoritative on the projectile's discrete floor.
- P7.4b2 is split into a trace/combat package and an AI package so each receives
  its own tests, performance capture, review, documentation, and commit gate.

### Completion Gate

- [x] Scope check: continuous trace/combat endpoints only; no pathfinding,
      rendering, map content, or engine change entered the package.
- [x] Invariant review: default `0.5`, finite/range validation, upward/downward
      symmetry, moving-frame projection, closed boundaries, launch snapshots,
      and discrete fixtures were reviewed.
- [x] Automated verification: 3 new, 72 consumer, 330 broad cases covered, 18
      unit/mapping, and two complete 3-case baseline runs pass.
- [x] Performance evidence: the repeated neutral baseline remains at 6,336 bytes
      with no flight work and stable 3/6/10-floor timing.
- [x] Documentation: trace geometry, hitscan, physical trajectories, flight,
      vertical-content status, general status, evidence, and remaining AI scope
      are synchronized.
- [x] Dependency check: no WTZ Engine change; clean submodule remains pinned to
      published revision `7cbd778024`.
- [x] Git check: full build, warning attribution, whitespace, generated-artifact
      ignore state, tree scope, and dependency state pass.
- [x] Mini review: contact-side representation, numeric precision, compatibility,
      target snapshots, fixture semantics, and test-grid ownership were reviewed.
- [x] Commit: prepared as isolated `Trace active Z-level flight heights` on
      `zlevel/vertical-content`; push and hash verification follow.

### Mini Review

- Finding: modeling a floor as `[Z, Z + 1)` removes the hidden half-level
  assumption while retaining identical geometry for all default-center callers.
- Finding: double-precision height arithmetic prevents large discrete Z values
  from erasing a small float offset before interpolation.
- Finding: a dynamic collidable integration target must stand over a materialized
  grid tile; otherwise Robust correctly reparents it to map space and native
  flight rejects it as `InvalidGrid`.
- Finding: source height must come from the shooter/thrower after lifecycle
  stamping, not from a newly spawned projectile's default center.
- Residual risk: projectile visuals and fixtures do not interpolate fractional
  height continuously between crossings; this is intentional discrete-floor
  behavior, but a future visual-height layer may present smoother motion.
- Residual risk: same-floor fixture collision intentionally ignores different
  hover offsets. Implementing true volumetric collision would be a separate
  engine-scale physics project.
- Next package: P7.4b2b adds explicit flight navigation edges, actor capability
  policy, and AI execution without treating every empty tile as walkable.

## Completed Package: P7.4b2b Explicit Flight AI Navigation And Execution

### Scope

- Add mapper-authored, bounded flight connections to the detached hierarchical
  graph without treating arbitrary empty tiles as navigable.
- Include those connections only for actors that can authoritatively use native
  flight and preserve actor-independent endpoint search behavior.
- Execute flight legs through physical steering and the existing vertical
  solver with explicit lifecycle ownership.
- Add mapping content, diagnostics, focused execution tests, and gate evidence.

### Implementation

- `ZLevelFlightNavigationComponent` authors an adjacent-floor corridor from a
  supported source through one `Body`-open aperture to a supported destination.
  Cardinal offsets rotate with its anchored marker; optional reverse travel
  reuses the same physical corridor.
- `ZLevelTraversalGraphSystem` indexes all affected local tile/floor keys,
  resolves live forward/reverse edges, and publishes them in a deterministic
  immutable `FlightEdges` snapshot array. Marker, tile, boundary, parent,
  anchor, map, Z, and frame changes use the existing map-scoped revisions.
- Actor path requests opt in only after side-effect-free flight validation.
  Endpoint-only and non-flying requests exclude flight edges. Search, route
  validation, and diagnostics retain a distinct typed `Flight` leg.
- NPC steering executes source, approach, crossing, and exit phases. Horizontal
  movement reaches the aperture, the native vertical solver owns the crossing,
  and steering exits onto supported destination floor without teleporting.
- Flight lifecycle ownership distinguishes a route-started flight from a
  pre-existing one. Completion, invalidation, clearing, and route replacement
  stop only owned flight; adopted flight remains active and is stabilized on an
  interrupted floor.
- `zlevelmetrics` reports markers, locations, graph edge outcomes, copied flight
  edges, planner evaluations, and steering leg starts/completions/failures.
- The official mapping station serializes one bidirectional Z 0/Z 1 corridor;
  its load test confirms one marker and two directed graph edges.

### Evidence

- The final focused fixture passes 6/6. It covers actor and endpoint capability
  gating, deterministic edge shape, support, `Body` boundary and rotation
  invalidation, physical AI completion, preservation of pre-existing flight,
  graph invalidation cleanup, and safe replacement of an owned-flight route.
- The official map load case passes 1/1 and resolves both directions. The
  final pathfinding/dynamic/flight matrix passes 38/38.
- The complete Content `FullyQualifiedName~ZLevel` matrix covers 336 cases and
  passes 336/336 without a skip or failure.
- Content structural and mapping unit tests pass 18/18.
- Two complete 3/3 schema-version 5 baseline runs pass. The captured confirming
  run measures 10.0359, 17.5405, and 27.5582 ms for 3, 6, and 10 floors. Every
  depth allocates 6,336 bytes, records 100% warm boundary/sky/gravity hits, zero
  PVS exhaustion, and zero flight updates.
- The non-incremental single-worker solution build passes in 2m45s with zero
  errors and 691 established warnings. The captured log attributes no warning
  to a modified production or test file.
- WTZ Engine remains clean and pinned to published revision `7cbd778024`; no
  engine change is required.

### Decisions

- Flight is an explicit graph capability, not a new polygon terrain type. This
  keeps empty space non-walkable and prevents ordinary NPCs from inheriting
  mobility from geometry alone.
- The marker authors a very short physical maneuver rather than an arbitrary
  free-flight volume. Local navigation owns paths to supported endpoints; the
  flight leg owns at most one cardinal approach and one cardinal exit step.
- `Body` remains authoritative at both graph resolution and physical crossing.
  A live boundary edit first invalidates the route and then the solver still
  rejects any crossing that races that invalidation.
- Capability is checked during planning and installation, then again throughout
  execution. Asynchronous results cannot grant a removed or incapacitated actor
  flight.
- Route ownership is independent from capability ownership. A route may adopt
  species or equipment flight without stopping it when the route completes.

### Completion Gate

- [x] Scope check: server graph, pathfinding, steering, one marker prototype,
      official mapping fixture, metrics, tests, and synchronized docs only.
- [x] Invariant review: Z 0 component-free actors, local/world frames, moving
      grids, adjacent floors, both directions, server authority, support,
      `Body` boundaries, and lifecycle ownership were reviewed.
- [x] Automated verification: 6 focused, 1 official-map, 38 combined path, 336
      broad, 18 unit/mapping, and two complete 3-case baselines pass.
- [x] Performance evidence: the neutral 3/6/10 baseline retains 6,336 bytes,
      fully warm caches, no PVS exhaustion, and zero flight work.
- [x] Documentation: flight, pathfinding, vertical content, project status,
      decisions, limits, mapping contract, and evidence are synchronized.
- [x] Dependency check: no WTZ Engine change; the clean submodule remains pinned
      to published revision `7cbd778024`.
- [x] Git check: whitespace, final build, warning attribution, ignored baseline
      artifacts, scope, and remote state are checked before commit.
- [x] Mini review: graph invalidation, actor gates, physical execution,
      pre-existing/owned flight, replacement, and mapper responsibility were
      reviewed.
- [x] Commit: prepared as isolated `Navigate authored Z-level flight corridors`
      on `zlevel/vertical-content`; push and hash verification follow.

### Mini Review

- Finding: retaining flight edges beside traversal edges lets one bounded search
  compare both connector kinds while ordinary actors see the exact P5 graph.
- Finding: stopping route-owned flight before replacing a validated route closes
  a lifecycle leak that happy-path completion would not reveal.
- Finding: using the native solver during the crossing preserves gravity,
  continuous height, closed-boundary contact, projectile traces, and moving-grid
  semantics without duplicating vertical physics.
- Residual risk: the one-tile internal approach/exit does not run a second local
  A* query. Mappers must keep those authored tiles physically clear; blocked or
  highly dynamic maneuvers rely on normal stuck/replan behavior.
- Residual risk: flight navigation is corridor-based, not volumetric free-flight
  planning. Arbitrary 3D pursuit would need a separately bounded navigation
  representation and is not implied by this package.
- Next package: the P7 phase gate reviews all vertical content together, runs
  final end-to-end mapping/gameplay evidence, and hands the stable surface to P8
  hardening.

## Completed Phase Gate: P7 Vertical Content

### Scope

- Review P7.1a through P7.4b2b as one authored-content pipeline rather than
  treating roofs, weather, elevators, and flight as unrelated package results.
- Re-run persistence, mapping, physical gameplay, combat, navigation, weather,
  and graphical evidence on the final paired project/engine revisions.
- Audit ownership boundaries, compatibility behavior, persistent versus runtime
  state, performance limits, and the exact product work that must move to P8.
- Harden the real-client capture when the gate itself can measure PVS or sprite
  convergence instead of the weather behavior it claims to test.

### Acceptance Criteria

- Authored vertical surfaces, roofs, weather policy, elevators, flight content,
  and their graph edges compose without weakening independent boundary channels.
- Initialized maps preserve authored surfaces and elevator state through two
  snapshot/load cycles, preserve flight configuration through its dedicated
  snapshot/load fixture, and exclude active trips, actions, passengers, and
  flight targets.
- Elevators and flight execute through server-authoritative physical lifecycle
  contracts; combat, gravity, support, and AI consume those contracts rather
  than introducing parallel Z models.
- Headless, unit, baseline, and real OpenGL evidence pass on the final diff, and
  any gate instability is explained and corrected rather than hidden by wider
  pixel tolerances.
- No unresolved P7 correctness finding blocks the start of server-scale P8
  hardening; remaining product and scale limits are explicit.

### Consolidated Evidence

- A cross-package matrix covering sky exposure, structural surfaces, vertical
  content, elevators, weather, flight, initialized snapshots/mutations, map
  format, pathfinding, hitscan, and ballistic trajectories passes 135/135 with
  no failures or skips.
- One complete Content Z-level run passes 336/336. The final post-build run
  passes 335 cases and reports the established pooled aperture-cache skip; that
  exact case then passes 1/1 in isolation. All 336 cases therefore have passing
  evidence on the final source, with no failure.
- Content structural, visual-analysis, and mapping unit coverage passes 18/18.
  Existing focused fixtures include two full initialized-map cycles for
  surfaces, atmosphere, infrastructure, and elevator topology/runtime
  filtering; a separate fixture proves authored flight capability and strips
  runtime flight state during snapshot/load.
- Two complete schema-version 5 baselines pass 3/3. The first measures
  10.2503/15.4385/22.3507 ms and the confirming run measures
  17.4163/22.1708/27.7543 ms for 3/6/10 floors. Every sample retains 6,336
  bytes, 100% warm boundary/sky/gravity cache hits, zero PVS exhaustion, and
  zero neutral flight updates.
- The first two graphical gate runs exposed a capture defect at 23/24: the
  strict covered-floor weather signature included an incompletely replicated
  structural layer and an animated connected player. The runner now requires
  the captured floor to include its baseline tile/light/occluder inventory,
  revalidates it during stabilization, hides only the local player sprite, and
  restores that sprite state at teardown. The weather threshold remains 0.003.
- Two consecutive corrected real OpenGL runs pass 24/24 in 33.9279 and 33.5015
  seconds. The confirming run measures covered-floor RMS 0.000000 versus
  exposed-floor RMS 0.055513, 1,007 mask plans, 49,343 tile checks, 6,601 runs,
  and zero fail-closed plans or budget exhaustion.
- A non-incremental single-worker full solution build completes in 2m41s with
  zero errors and the established 691 warnings. No warning points to the gate's
  modified capture system. WTZ Engine remains clean at published revision
  `7cbd778024` and requires no P7 gate change.

### Architecture Review

- Vertical geometry remains one shared truth without becoming one gameplay
  system. Tile/provider boundaries and `ZLevelTrace` provide typed crossings;
  support, sky/weather, elevators, combat, flight, and navigation retain their
  own policy, budgets, caches, and lifecycle.
- Roofs, grates, catwalks, and shafts are persistent authored content. Weather
  consumes the independent `Weather` channel and sky cache, so atmosphere,
  sight, sound, projectiles, and body support cannot be changed accidentally by
  a weather decision.
- Elevators are physical dynamic graph edges backed by one cabin and exact
  landing topology. Flight is an explicit actor capability and authored graph
  corridor backed by the shared solver. Neither makes empty space ordinary
  walkable terrain or adds a second pathfinder.
- Mapper-owned configuration is serializable while trips, riders, controls,
  active flight targets, and route ownership are transient. This composes with
  P6's canonical initialized snapshot instead of creating a P7 save format.
- Legacy unconfigured maps retain planar Z 0 weather, grid gravity, ordinary
  jetpack behavior, native tiles, and component-free entities. Native Z-level
  behavior remains map-opt-in.

### Decisions

- Close P7 without adding another vertical-content abstraction or serializer.
  The composed contracts and round trips are complete enough for hardening;
  further framework work without scale evidence would be speculative.
- Keep the capture correction in the phase gate because false graphical passes
  or failures would weaken the release oracle used again in P8. The fix changes
  no production rendering or gameplay policy and preserves the strict threshold.
- Split P8 into a measured soak harness, evidence-driven runtime hardening, Z 0
  and porting work, and a final public-server release gate. Each remains subject
  to the same mandatory package checklist.

### Completion Gate

- [x] Scope check: the only source change hardens the real-client oracle; all
      other changes record P7 closure and the bounded P8 handoff.
- [x] Invariant review: Z 0, local/world frames, moving grids, support, all
      boundary channels, server authority, persistent/transient ownership, and
      initialized lifecycle were reviewed across every P7 package.
- [x] Automated verification: 135/135 cross-package, passing evidence for all
      336 broad cases, 18/18 unit/mapping, two 3/3 baselines, two 24/24 real
      graphical runs, and a zero-error full build complete on the final source.
- [x] Performance evidence: repeat baselines preserve allocation/cache/PVS
      invariants; graphical weather work and limits are captured; server-scale
      concurrency is explicitly assigned to P8.1 rather than inferred.
- [x] Documentation: architecture, persistence, mapping, content, controls,
      evidence, capture correction, product limits, and P8 packages are
      synchronized in the ledger and focused Z-level documents.
- [x] Dependency check: WTZ Project remains paired with clean, published WTZ
      Engine `7cbd778024`; no engine source or submodule pointer changed.
- [x] Git check: generated baselines, screenshots, reports, and logs remain
      ignored; declared source/docs pass whitespace and scope review.
- [x] Mini review: findings, residual risks, and P8.1 are recorded below.
- [x] Commit: prepared as isolated `Close Z-level vertical content phase` on
      `zlevel/vertical-content`; push and remote hash verification follow.

### Mini Review

- Finding: the phase composes cleanly around shared geometry and specialized
  consumers; no monolithic vertical-content coordinator or duplicated movement
  model emerged.
- Finding: the graphical gate caught two forms of test contamination without
  revealing a production weather leak. Requiring structural convergence and
  excluding the animated player produces a strict zero-difference covered-floor
  oracle across consecutive runs.
- Residual risk: elevators still lack interpolated cabins, doors, interlocks,
  queues, emergency controls, and player construction; these are product depth,
  not correctness prerequisites for the current physical contract.
- Residual risk: flight AI is corridor-based and its one-tile approach/exit is
  mapper-cleared rather than a volumetric planner. Arbitrary 3D pursuit remains
  outside the declared P7 model.
- Residual risk: graphical evidence covers one NVIDIA/OpenGL environment, and
  the official test map is a compact fixture rather than a representative live
  station. Multi-client density, item piles, moving grids, long rounds, and
  cross-vendor graphics are P8 concerns.
- Next package: P8.1 creates deterministic multi-session and mutation soak
  workloads, records per-subsystem throughput/memory/cache/budget evidence, and
  establishes release-sized thresholds before changing runtime policy.

## Completed Package: P8.1 Deterministic Server-Scale Soak

### Scope

- Extend the generated P0 fixture with an optional, strictly bounded candidate
  density while preserving density one for every existing baseline caller.
- Add a deterministic server workload with configurable floors, in-game dummy
  sessions, candidate density, warm-up, and measured iterations.
- Exercise moving local frames, structural tile removal/restoration, boundary,
  visibility, sky, gravity, PVS, sound portal/routing/playback, and traversal
  snapshot build/reuse in one lifecycle-owned run.
- Emit a validated schema-versioned JSON report and a parameterized Release
  runner without committing generated artifacts.
- Correct pre-existing Z-level Release analyzer failures encountered while
  proving the runner, without changing their runtime behavior.

### Acceptance Criteria

- Every session owns one attached viewer and receives exactly one explicit PVS
  refresh per measured iteration; no PVS candidate fails open.
- Structural mutations always restore their tile through a local `finally`,
  drain pending gravity work, and leave both grid inventories and map
  declaration valid.
- Vertical sound and traversal use real bounded production contracts; the graph
  must rebuild after support changes and immediately reuse a current snapshot.
- Boundary, sky, sound, and graph caches stay within configured capacities; no
  PVS, sky, sound, or traversal work budget exhausts in the declared profile.
- The report records host/build context, configuration, complete subsystem
  counters, caches, allocation/heap/GC, and min/average/p50/p95/p99/max latency
  summaries without machine-dependent timing assertions.
- Two equivalent Release captures reproduce structural counters and support an
  evidence-driven P8.2 target.

### Evidence

- The schema 2 Release profile passes twice with 10 floors, 32 sessions, 960
  candidate entities, 36 traversal nodes, 8 warm-ups, and 128 measured
  iterations. Total measured time is 8,083.789 and 7,863.530 ms; main-thread
  allocation is 170,159,064 and 170,198,960 bytes; post-GC retained deltas are
  -3,586,056 and -452,544 bytes.
- Both runs record 7/2/0 Gen0/Gen1/Gen2 workload collections. Iteration
  p50/p95/p99 is 25.449/147.554/167.452 ms and
  21.572/131.913/158.816 ms. Per-session PVS p50/p95/p99/max is
  0.719/4.774/5.555/29.190 ms and 0.473/3.591/5.168/27.814 ms.
- Both reports exactly match at 4,096 PVS refreshes, 4,218,880 candidates and
  checks, 2,330,864 boundary queries, 256 gravity builds, 128 vertical sound
  successes, and 256 graph builds plus 256 cache hits. All relevant budget and
  fail-open counters are zero.
- Final cache state is boundary 8,192/8,192 with 16 evictions, sky 424/4,096,
  sound portal 17/4,096, one traversal snapshot, and zero pending gravity work.
- The checked-in runner validates schema and requested settings. Two earlier
  bounded Debug captures also passed with deterministic counters and allocation.
- The existing 3/6/10-floor baseline passes 3/3 after the density extension,
  proving default density remains one. Z-aware tile/chemical interaction passes
  5/5 in Release.
- The final complete Debug `FullyQualifiedName~ZLevel` integration matrix passes
  337/337 with no failure or skip. A preceding namespace-only Release pass covers
  321/321, and the one pooled lighting-cache case skipped in an earlier Debug
  batch passes 1/1 in isolation. Content structural, visual-analysis, and mapping
  unit coverage passes 18/18.
- `Content.IntegrationTests` builds in Release with zero errors. The build first
  exposed two pre-existing analyzer violations in our Z-level code: the admin
  verb now uses the Transform-specialized `TryComp` overload and reagent tests
  use typed `ProtoId<ReagentPrototype>` values. Their behavior is unchanged.
- The final non-incremental single-worker solution build passes in 2m44s with
  zero errors and 688 established warnings.

### Decisions

- Keep the ordinary-suite profile at 4 sessions, 240 candidates, and 8 measured
  iterations while the explicit runner defaults to the 32-session Release
  profile. Both paths execute the same implementation and assertions.
- Use structural and budget invariants as portable pass/fail policy. Record
  latency, allocation, and retained heap for equivalent-host comparisons rather
  than embedding workstation-specific thresholds in the test.
- Do not increase cache capacities in P8.2: observed boundary eviction is
  negligible and sky/sound caches have ample headroom. First decompose the
  reproducible p95 iteration tail by subsystem, then address scheduling,
  invalidation, or allocation at the measured owner.
- Keep schema 2 as the immutable P8.1 baseline. P8.2 may append attribution
  fields under a new schema while retaining every existing counter and the
  corrected pre-collection GC accounting.

### Completion Gate

- [x] Scope check: generated fixture density, server soak, runner, analyzer
      compatibility, and P8 documentation only.
- [x] Invariant review: Z 0 default callers, local/world frames, moving grids,
      server session authority, structural restoration, independent boundary
      channels, graph support, and bounded caches were reviewed.
- [x] Automated verification: two final Release soaks, 337 complete Debug and
      321 namespace-only Release integration cases, the isolated pooled cache
      case, 18 unit/mapping, 5 tile/interaction, and 3 baseline cases pass.
- [x] Performance evidence: two schema 2 reports reproduce all structural
      counters and capture latency percentiles, allocation, heap, GC, caches,
      and budget state.
- [x] Documentation: workload, runner, schema, evidence, interpretation, limits,
      and P8.2 direction are synchronized here and in the focused hardening doc.
- [x] Dependency check: WTZ Engine remains clean and pinned to published
      revision `7cbd778024`; P8.1 requires no engine change.
- [x] Git check: the final full build and Debug broad matrix pass;
      `git diff --check` reports only checkout line-ending notices; generated
      reports are ignored; the project diff is package-scoped; WTZ Engine is
      clean at published revision `7cbd778024`.
- [x] Mini review: false assumptions about round-backed dummy sessions, sound
      query range, empty traversal snapshots, failure restoration, GC accounting,
      and latency tails were found and corrected during the package.
- [x] Commit: package prepared as `Measure deterministic Z-level server scale`
      on `zlevel/server-hardening`; push and remote-hash verification are the
      immediate publication step.

### Mini Review

- Finding: 32 independent sessions and dense representative entities remain
  correct under moving frames and paired structural mutations; no safety budget
  is close to exhaustion and no state leaks after collection or teardown.
- Finding: an empty graph snapshot would have produced misleading cache-only
  evidence. Four authored logical ladder stacks now give the soak 36 real nodes
  and support-sensitive invalidation without invoking traversal movement.
- Finding: individual PVS p95 stays below 5 ms on this host, but complete
  iteration p95 exceeds 130 ms in both final runs. P8.2 must profile consumers
  inside that iteration before assigning the tail to PVS, gravity, GC, or cache
  churn.
- Residual risk: integration tests use workstation GC and synthetic viewers,
  candidate entities, mutations, and traversal stacks. A dedicated server,
  representative station, real players, item piles, networking, and hours-long
  runtime remain P8.4 release evidence.
- Next package: P8.2 adds per-owner soak timing/allocation attribution, then
  changes only the budget, cache, invalidation, scheduling, or lifecycle path
  shown to own the long tail.

## Completed Package: P8.2a Runtime Attribution

### Scope

- Extend the deterministic soak report without changing its workload or any
  production subsystem policy.
- Attribute latency and current-thread allocation to frame/viewer updates, open
  mutation, vertical consumers, sound, traversal, PVS batch, restoration,
  restored consumers, and measurement overhead.
- Correlate complete-iteration latency with Gen0/Gen1/Gen2 collection activity.
- Validate that attributed bytes sum exactly to the measured iteration bytes and
  retain every P8.1 correctness, budget, cache, and lifecycle invariant.

### Evidence

- The schema 3 short Debug profile passes 1/1 at 10 floors, 4 sessions, 240
  candidates, and 8 measured iterations. Its attribution accounts for every one
  of 10,592,208 measured bytes; the two gravity-consumer stages own 10,515,840.
- Two schema 3 Release profiles pass at 10 floors, 32 sessions, 960 candidates,
  8 warm-ups, and 128 measured iterations. Total time is 7,160.518 and 7,123.143
  ms, a 0.52 percent difference; allocation is 170,175,768 and 170,175,552 bytes.
- PVS batch p50/p95/p99 is 17.714/111.475/130.917 ms and
  15.748/110.789/132.784 ms. Shared subsystem metrics attribute 6,064.464 and
  6,039.005 ms, approximately 85 percent of total runtime, to PVS refreshes.
- Open vertical consumer p95 is 8.145 and 9.638 ms; restored consumer p95 is
  3.746 and 3.519 ms. Gravity builds total 631.818 and 626.953 ms.
- The two gravity-consumer stages allocate exactly 168,249,344 bytes in both
  runs, approximately 98.9 percent of total allocation. Complete PVS batches
  allocate only 409,600 and 411,040 bytes.
- Both runs observe collections in 7 of 128 iterations. The 121 iterations
  without a collection still have p95 above 123 ms and maximum above 165 ms, so
  collection pauses do not explain the reproducible PVS tail.
- The Debug and Release integration projects build with zero errors. All three
  schema 3 runs preserve exact P8.1 counters, zero budget/fail-open failures,
  bounded caches, restored tiles, current traversal snapshots, and zero pending
  gravity refreshes.

### Decisions

- Treat PVS scheduling and gravity topology allocation as independent owners.
  Do not hide either result behind a larger cache or a looser safety budget.
- P8.2b will stagger the existing PVS cadence fairly across ticks and bound a
  single update's session work while preserving fail-open visuals and separately
  fail-closed sound authorization.
- P8.2c will avoid repeated full-grid topology materialization for isolated tile
  mutations while retaining exact connected-component gravity behavior.
- Schema 3 is append-only over schema 2. Timing remains comparative evidence,
  while byte conservation and all functional counters remain executable policy.

### Completion Gate

- [x] Scope check: one integration harness, its runner, and synchronized P8 docs;
      no production policy or engine code changed.
- [x] Invariant review: workload geometry, Z 0 behavior, world/local frames,
      moving grids, server sessions, boundary channels, restoration, and caches
      are unchanged from P8.1.
- [x] Automated verification: Debug and Release builds pass; one short Debug and
      two complete Release schema 3 profiles pass.
- [x] Performance evidence: equivalent Release captures reproduce stage owners,
      counters, allocations, percentiles, and collection correlation.
- [x] Documentation: schema, evidence, interpretation, and next owners are
      synchronized in the ledger, server-hardening guide, runner, and main doc.
- [x] Dependency check: WTZ Engine remains clean at published revision
      `7cbd778024`; attribution requires no engine change.
- [x] Git check: generated reports remain ignored; final staged whitespace,
      scope, tree, commit, push, and remote hash are checked at publication.
- [x] Mini review: the attribution itself initially allocated 40 bytes per
      iteration through `Enum.GetValues`; a pre-measurement static stage table
      removed that observer error before evidence was accepted.
- [x] Commit: package prepared as `Attribute Z-level server workload costs` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: batching all sessions at the same 10 Hz boundary, not an individual
  pathological refresh, creates the PVS frame tail.
- Finding: gravity rebuilds are a smaller CPU owner in the 32-session profile but
  dominate allocation and become the primary cost in the 4-session profile.
- Finding: collection activity is reproducible but is not necessary for a slow
  iteration; scheduling and topology allocation need direct fixes.
- Residual risk: stage attribution still runs synthetic sessions on workstation
  GC. Dedicated-server scheduling and representative-map evidence remain P8.4.
- Next package: P8.2b introduces fair staggered PVS scheduling, scheduler metrics,
  deterministic cadence/starvation tests, and before/after batch evidence.

## Completed Package: P8.2b Bounded PVS Scheduling

### Scope

- Replace the synchronized 100 ms all-session pulse with deterministic refresh
  credit consumed on every server update.
- Preserve the 10 Hz per-session target, fair circular ordering, overdue credit,
  fail-open visual policy, and independent fail-closed sound authorization.
- Add a server-only, clamped session-count cap per update plus process-local
  scheduler counters, timing, admin output, and reset support.
- Clear culling and sound state immediately when a session leaves `InGame`.
- Drive the production scheduler from schema 4 soak frames and add pure cadence,
  backlog, fairness, long-frame, and lifecycle tests.

### Evidence

- Four pure scheduler cases pass in 36 ms. They prove the 32-session 10/11/11
  cadence over three 30 Hz frames, fair traversal of all 64 sessions under an
  eight-session cap, bounded catch-up without duplicate work, and empty-
  population credit/cursor reset.
- The schema 4 short Debug profile passes with 24 scheduler updates, 32 session
  refreshes, zero deferred work, and a maximum batch of two.
- Two schema 4 Release profiles pass at 10 floors, 32 sessions, 960 candidates,
  and 128 measured iterations. Each performs exactly 384 scheduler updates,
  4,096 session refreshes, and 4,218,880 candidate checks with maximum batch 11,
  zero deferred refreshes, and zero scheduler or visibility budget exhaustion.
- Scheduler-frame p50/p95/p99/max is 5.051/40.231/50.112/72.958 ms and
  5.008/39.649/51.430/63.031 ms. The equivalent schema 3 all-session update had
  p95 111.475 and 110.789 ms, so per-update p95 falls approximately 64 percent.
- Complete PVS-cycle p95 remains 119.096 and 115.273 ms, correctly showing that
  scheduling distributes existing CPU instead of disguising it as removed work.
  Allocation remains dominated by gravity; scheduler overhead is approximately
  0.06 percent of the 170.28 MB measured workload.
- Focused PVS/sound/budget integration coverage passes 17/17; the complete
  `FullyQualifiedName~ZLevel` matrix passes 337/337; Content Z-level/mapping unit
  coverage passes 22/22; and the 3/6/10-floor baseline passes 3/3.
- The final non-incremental single-worker solution build passes in 2m42s with
  zero errors and 688 established warnings.

### Decisions

- Budget by session count, not wall-clock timing. This makes behavior testable,
  preserves deterministic fairness, and avoids host-dependent authorization
  cadence. Per-refresh visibility checks retain their independent safety budget.
- Default the cap to 16 and clamp it from 1 through 256. At 32 sessions the
  10 Hz target naturally requests at most 11 per 30 Hz update, so no work is
  deferred. Larger populations expose debt and exhaustion rather than dropping
  sessions or processing an unbounded catch-up pulse.
- Keep `RefreshSession` as the unchanged authoritative unit. The scheduler owns
  only timing and ordering; visual and sound decisions remain in their existing
  consumers.
- Treat the remaining approximately 40 ms synthetic p95 as explicit P8.4
  tuning evidence. Operators can lower the cap at the cost of refresh age, but a
  workstation stress capture is not sufficient to change the default cadence.

### Completion Gate

- [x] Scope check: one server scheduler, PVS integration, one CVar, admin
      diagnostics, focused unit tests, soak schema/runner, and synchronized docs.
- [x] Invariant review: Z 0, world/local frames, moving grids, server authority,
      visual fail-open, sound fail-closed, session lifecycle, and fair backlog
      behavior retain their previous contracts.
- [x] Automated verification: 4 pure scheduler, 1 short Debug soak, 2 full
      Release soaks, 17 focused, 337 broad, 22 unit/mapping, and 3 baseline cases
      pass; the full solution builds cleanly.
- [x] Performance evidence: two equivalent profiles reproduce max batch 11,
      zero debt, unchanged decisions, and approximately 64 percent lower
      per-update p95.
- [x] Documentation: CVar, metrics, schema 4, evidence, tradeoff, limitation, and
      operational interpretation are synchronized.
- [x] Dependency check: WTZ Engine remains clean at published revision
      `7cbd778024`; the scheduler is content-server policy.
- [x] Git check: generated reports remain ignored; final whitespace, staged
      scope, tree, commit, push, and remote hash are checked at publication.
- [x] Mini review: the complete cycle remains expensive by design, scheduler
      array overhead is bounded, session exit clears state immediately, and the
      residual dedicated-server tuning risk is recorded rather than hidden.
- [x] Commit: package prepared as `Stagger Z-level PVS refresh work` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: the previous long tail was primarily a scheduling burst. Dividing one
  cycle over three frames delivers the largest measured gain without changing a
  visibility or audio decision.
- Finding: count budgeting is operationally legible: overload appears as credit,
  deferred work, and exhaustion while the circular cursor prevents starvation.
- Residual risk: the 32-session synthetic p95 remains slightly above a 30 Hz
  tick. P8.4 must test a dedicated server, real player positions, actual network
  serialization, and alternative cap values.
- Next package: P8.2c removes full gravity topology rematerialization and most of
  the 168.25 MB allocation caused by paired single-tile mutations.

## Completed Package: P8.2c Reusable Gravity Topology Workspaces

### Scope

- Retain one bounded high-water workspace per managed grid for live tiles,
  ordered sources, BFS assignments, queue storage, gravity columns, and recycled
  column lists.
- Maintain the live topology incrementally for ordinary base and native-layer
  empty/non-empty tile transitions while ignoring solid-to-solid replacement.
- Preserve a conservative external invalidation API that marks the live-tile
  snapshot stale, and release the entire workspace when its grid is removed.
- Reuse the pending weightlessness refresh buffer, expose reused-build metrics,
  and extend the soak report contract to schema 5.
- Prove that reused buffers discard stale assignments and retain the exact
  connected-component gravity result after topology changes.

### Evidence

- Four focused gravity solver/cache/invalidation cases pass. The new connected
  integration case proves that solid tile replacement produces one cache hit and
  zero invalidations, while removing and restoring a native Z1 endpoint produces
  exactly two builds and reuses the same workspace both times.
- The short schema 5 Debug soak passes with three floors, two sessions, and two
  measured iterations. Its gravity-consumer stages allocate only 416 bytes in
  total; the remaining small-profile allocation is owned primarily by sound.
- Two schema 5 Release profiles pass at 10 floors, 32 sessions, 960 candidates,
  8 warm-ups, and 128 measured iterations. Total time is 6,858.106 and
  7,419.711 ms; main-thread allocation is 2,068,744 and 2,064,688 bytes.
- The equivalent schema 4 scheduler profiles allocated 170,281,832 and
  170,266,272 bytes. Total measured allocation therefore falls approximately
  98.8 percent. The two gravity-consumer stages fall from 168,249,344 bytes to
  26,624 bytes, a reduction above 99.98 percent.
- Both captures report 256 gravity builds, 256 reused builds, 926,080 processed
  tiles, 54,144 cache hits, 256 misses, and 256 invalidations. Gravity build time
  is 389.790 and 406.336 ms, compared with 631.818 and 626.953 ms before reuse.
- Both captures preserve 4,096 scheduled PVS refreshes, 4,218,880 candidates and
  checks, maximum scheduler batch 11, zero deferred work, and every previous
  correctness/cache/budget invariant. No workload GC collection occurs in
  either run, compared with seven collection-bearing iterations before reuse.
- The complete Debug Z-level matrix passes 338 executed cases with one known
  pooled lighting-cache skip; that case passes 1/1 in isolation. Content
  Z-level/mapping unit coverage passes 22/22 and the generated 3/6/10-floor
  baseline passes 3/3.
- The final restored, non-incremental, single-worker solution build passes in
  2m36s with zero errors and 704 established warnings. An initial `--no-restore`
  attempt after a Release-only restore lacked optional Debug assets; allowing
  the normal restore repaired the invocation without source changes.

### Decisions

- Keep the exact deterministic multi-source BFS. Reuse its storage and maintain
  only the live-tile inventory incrementally; do not introduce a second,
  partially incremental connectivity algorithm with harder invalidation rules.
- Treat tile emptiness as the topology contract. Replacing one non-empty tile
  definition with another cannot change connected gravity and must not dirty the
  field.
- Trust ordinary tile/source events to preserve the current live inventory, but
  make the public batch-edit invalidation conservative by forcing re-enumeration
  on the next query.
- Scope retained capacity to each grid and remove it on grid teardown. Do not use
  a process-global pool that can retain a deleted station's largest topology.
- Make schema 5 append-only over schema 4 by adding reused-build evidence while
  preserving every previous scheduler and subsystem field.

### Completion Gate

- [x] Scope check: gravity solver/cache/invalidation, one metric, diagnostics,
      focused tests, soak schema, and synchronized documentation only.
- [x] Invariant review: Z 0 base tiles and native layers share the same empty-
      transition rule; source world Z, moving-grid local frames, deterministic
      source order, server authority, and exact connected components remain
      unchanged.
- [x] Automated verification: 4 focused, 1 short Debug plus 2 complete Release
      soaks, 339 broad cases covered, isolated pooled cache, 22 unit/mapping, 3
      baseline cases, and the full solution build pass.
- [x] Performance evidence: two equivalent schema 5 captures reproduce every
      structural counter and reduce total allocation by approximately 98.8
      percent without changing PVS or gravity decisions.
- [x] Documentation: lifecycle, invalidation policy, schema 5, before/after
      measurements, runner behavior, and residual limits are synchronized.
- [x] Dependency check: WTZ Engine remains clean and published at revision
      `7cbd778024`; the workspace optimization is content-shared policy.
- [x] Git check: generated reports remain ignored; `git diff --check` reports
      only checkout line-ending notices; the final tree, staged scope, commit,
      push, and remote hash are checked at publication.
- [x] Mini review: test fixtures were corrected to avoid physical grid splitting
      and to respect column-level gravity queries; stale-buffer and external-
      invalidation paths are directly covered.
- [x] Commit: package prepared as `Reuse Z-level gravity field workspaces` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: field topology rematerialization, not the BFS result itself, caused
  nearly all measured allocation. Reusing ownership-local storage removes that
  pressure without introducing an approximate gravity model.
- Finding: retaining the live set across source-only invalidations and updating
  it from empty transitions also removes roughly one third of measured gravity
  build CPU on the comparison host.
- Residual risk: collection capacities retain the largest topology seen by a
  live grid until that grid is deleted. P8.4 must inspect retained memory on a
  representative station and repeated grid lifecycle, not only per-iteration
  allocation.
- Residual risk: scheduler-frame p95 remains approximately 39 ms in the synthetic
  32-session profile. Dedicated-server cap and cadence selection remains P8.4.
- Next package: P8.3a converts the stated Z 0 compatibility promise into an
  explicit inventory and executable regression matrix before the port contract
  is frozen.

## Completed Package: P8.3a Executable Z 0 Compatibility Matrix

### Scope

- Define the versioned `WTZ-Z0-1` behavioral contract as a machine-readable
  inventory covering 15 required compatibility domains.
- Bind each of 18 promises to one unique fully-qualified integration test across
  WTZ Project and WTZ Engine.
- Add three foundational tests for passive unconfigured maps, canonical
  component-free entity positions on world Z 0, and planar Z 0 visibility.
- Add a PowerShell gate that validates the inventory, executes exact project
  groups, parses TRX results, and emits an ignored revision/hash report.
- Document that compatibility preserves legacy planar behavior and does not
  automatically migrate old stations into multi-floor maps.

### Evidence

- The three new foundational integration cases pass 3/3. They prove that an
  unconfigured map remains free of `ZLevelMapComponent`, does not allocate a
  gravity workspace, validates with base tiles, and preserves equivalent legacy
  and explicit-Z0 tile reads; stamping an entity back to world Z 0 removes its
  explicit `ZLevelPositionComponent`; and same-floor entity/coordinate
  visibility remains planar without vertical state.
- The executable compatibility gate passes all 18/18 declared contracts across
  15 domains and two projects: 17 Content integration cases and the WTZ Engine
  `ZLevelLegacy2DQueriesRemainOnBaseLayer` case. TRX discovery, exact outcomes,
  manifest SHA-256, and paired revisions are captured in the ignored schema 1
  report.
- The complete Debug `FullyQualifiedName~ZLevel` matrix executes 342 cases: 340
  pass and two fixture-conditioned pooled cases skip. Both the lighting-cache
  and concurrent-NPC cases pass 2/2 together in an isolated invocation.
- Content Z-level/mapping unit coverage passes 22/22. The generated 3/6/10-floor
  baseline passes 3/3 at 9.918, 14.697, and 21.721 ms respectively, with exactly
  6,336 allocated bytes at every depth.
- The final non-incremental single-worker solution build passes in 3m01s with
  zero errors and 704 established warnings.

### Decisions

- Define Z 0 compatibility as preservation, not conversion. Ordinary maps stay
  component-free and planar until a mapper explicitly configures native floors.
- Keep one unique test per matrix entry. A single test cannot make several
  promises appear independently covered, and renaming/removing a test causes the
  gate to fail discovery.
- Own the mandatory domain set in the runner as well as the manifest. Editing
  the source-of-truth file alone cannot silently reduce the protected surface.
- Parse TRX definitions and results after `dotnet test`; process exit success
  alone is insufficient because a stale filter can execute zero tests.
- Run project and engine contracts from the project checkout while recording
  both Git revisions. P8.3a requires no new engine source change.

### Completion Gate

- [x] Scope check: one Content fixture, one JSON inventory, one runner, and
      synchronized Z-level documentation; production behavior is unchanged.
- [x] Invariant review: component-free Z 0 representation, unconfigured-map
      passivity, legacy 2D tile APIs, server authority, moving-frame consumers,
      mapping, persistence, and all listed gameplay domains are represented.
- [x] Automated verification: 3 new, 18 executable-contract, all 342 broad cases
      covered, 2 isolated pooled cases, 22 unit/mapping, 3 baseline cases, and
      the full solution build pass.
- [x] Performance evidence: no runtime path changed; the neutral baseline
      remains at exactly 6,336 allocated bytes for 3, 6, and 10 floors.
- [x] Documentation: scope, non-migration boundary, domain table, commands,
      report schema, evidence, and next package are synchronized.
- [x] Dependency check: WTZ Engine remains clean and published at revision
      `7cbd778024`; its existing exact legacy-map test is executed by the gate.
- [x] Git check: generated TRX/JSON reports remain ignored; whitespace, staged
      scope, tree, commit, push, and remote hash are checked at publication.
- [x] Mini review: Windows PowerShell 5.1 compatibility, exact discovery, pooled
      skips, map passivity, and the distinction between preservation and
      migration were verified explicitly.
- [x] Commit: package prepared as `Make Z 0 compatibility executable` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: the implementation already had meaningful Z 0 regressions in every
  critical subsystem, but they were discoverable only through code archaeology.
  The matrix turns that latent coverage into a named release contract.
- Finding: the three missing foundation cases were architectural, not feature-
  specific. They now prove the opt-in boundary on which every legacy map relies.
- Residual risk: each domain currently selects a high-value representative
  contract rather than every possible same-floor behavior. The broad suite and
  P8.4 gameplay matrix remain necessary.
- Residual risk: this gate proves the paired checkout; it does not yet explain or
  verify which engine/content commits a foreign fork must import. P8.3b owns that
  versioned port manifest and compatibility verifier.
- Next package: P8.3b freezes the minimal engine/content port boundary, verifies
  revisions and required symbols/files, and reports actionable incompatibilities.

## Completed Package: P8.3b Versioned Engine/Content Port Contract

### Scope

- Define the machine-readable `WTZ-PORT-1` boundary between WTZ Project and WTZ
  Engine, rooted at RobustToolbox `v275.2.0` and the official paired revisions.
- Inventory the ordered 20-commit engine extension series as 20 independently
  named capabilities, with an engine API probe and project consumer for each.
- Add strict official-pair and history-independent portable verification modes,
  protected compile targets, clean-worktree enforcement, and JSON evidence.
- Add a fail-closed self-test for malformed manifests and rewritten histories.
- Publish a destination workflow, failure interpretation, and the explicit
  boundary between source/compile compatibility and runtime proof.

### Evidence

- `verify_zlevel_port.ps1 -Mode Paired -NoRestore` passes the exact WTZ pair at
  project minimum `bd6ce6d1`, engine base `3136118b`, and engine head
  `7cbd778024`. It verifies all 20 capabilities, the 20 ordered commit subjects,
  28 engine plus 22 Content probes, the submodule gitlink/URL, and both protected
  Debug builds with zero contract failures.
- The verifier self-test passes 6/6. It rejects a missing capability, missing
  probe, broken probe, missing protected build, and official head outside the
  ordered series. It accepts an unresolvable official project hash only in
  `Portable` mode and only with the expected rewritten-history warning.
- The existing executable Z 0 gate passes all 18/18 contracts across 15 domains.
  The complete Debug `FullyQualifiedName~ZLevel` matrix passes 342/342 with no
  failures or skips, and Content Z-level/mapping units pass 22/22.
- Two generated 3/6/10-floor baseline runs pass 3/3. The final run records
  10.560, 15.535, and 34.209 ms with 6,336 allocated bytes at every depth. The
  first run recorded one transient 6,560-byte 10-floor sample and returned to
  6,336 bytes in the fresh rerun; no production path changed in this package.
- The final non-incremental single-worker `SpaceStation14.slnx` build passes in
  2m30.59s with zero errors and 688 established upstream warnings.

### Decisions

- Keep the engine boundary capability-oriented. Commit identity is authoritative
  for the official pair, while a destination may satisfy the same capability
  through rebased, cherry-picked, or equivalent source in `Portable` mode.
- Require every capability to have both an engine probe and a real project
  consumer. An API marker without compiled consumption is not a portable
  contract.
- Protect `WTZ-PORT-1`, all 20 capability IDs, exactly 50 probes, both build
  targets, and the final-series head in the verifier itself. Editing only the
  manifest cannot silently weaken the gate.
- Capture native-process output and exit codes explicitly under Windows
  PowerShell 5.1. Expected Git or verifier failures remain inspectable instead
  of escaping before the JSON report or self-test can classify them.
- Treat source probes and compilation as compatibility evidence, not runtime
  equivalence. P8.3c owns a clean detached rehearsal; P8.4 owns foreign/runtime
  and public-server release evidence.

### Completion Gate

- [x] Scope check: one manifest, two PowerShell tools, one focused guide, and
      synchronized roadmap/hardening/ledger documentation; production behavior
      and the WTZ Engine checkout are unchanged.
- [x] Invariant review: Z 0 preservation, world/local frames, moving grids,
      server authority, rendering, audio, physics, serialization, save/load,
      and atomic writes are represented by named engine/consumer contracts.
- [x] Automated verification: 6 self-tests, 50 probes, 2 contract builds, 18 Z 0
      contracts, 342 broad integration cases, 22 unit/mapping cases, two
      baseline runs, and the full solution build pass.
- [x] Performance evidence: no runtime source changed; repeated neutral
      baselines preserve the 6,336-byte final reference, with the one transient
      224-byte variance recorded rather than hidden.
- [x] Documentation: official pair, 20-capability series, verification modes,
      destination workflow, reports, failures, and semantic limits are covered.
- [x] Dependency check: WTZ Engine is clean, published, and paired at
      `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: generated reports remain ignored; whitespace, staged scope,
      worktrees, commit, push, and remote hash are checked at publication.
- [x] Mini review: fail-closed mutation, rewritten-history acceptance, native
      stderr handling, manifest integrity, and runtime-proof limits were tested.
- [x] Commit: package prepared as `Define the WTZ Z-level port contract` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: the engine fork contains exactly 20 WTZ commits over `v275.2.0`, not
  an informal or partially recoverable patch set. The manifest now preserves
  their order, subjects, responsibilities, and consumers.
- Finding: a portable verifier must tolerate missing official commit objects,
  not merely different checked-out heads. The acceptance self-test exposed and
  fixed that distinction before publication.
- Finding: official-pair and portable equivalence are intentionally different
  claims. The report names which one was established instead of collapsing them
  into a generic pass.
- Residual risk: regex probes prove required source shapes, and builds prove
  signatures, but neither proves semantics after adapting to a foreign upstream.
- Residual risk: the single transient 10-floor allocation sample reinforces that
  P8.4 needs explicit performance tolerances and repeated release samples rather
  than equality against one process measurement.
- Next package: P8.3c will execute the guide from clean detached project/engine
  worktrees, verify both modes in Release, record the destination-style result,
  and close the consolidated P8.3 phase gate.

## Completed Package: P8.3c Clean Port Rehearsal And P8.3 Gate

### Scope

- Add one end-to-end runner that exercises both `WTZ-PORT-1` policies from
  independent clean project and engine checkouts with Release builds enabled.
- Prove the exact official pair from complete history, then prove a destination-
  style portable pair whose depth-one histories cannot resolve the official
  project minimum or engine base and whose heads are intentionally distinct.
- Preserve source checkout state, initialize nested engine dependencies locally,
  emit structured evidence, and remove temporary trees through an ownership-
  checked cleanup lifecycle.
- Consolidate the P8.3 executable Z 0, port-contract, broad regression,
  baseline, solution-build, dependency, publication, and documentation gates.

### Evidence

- Official rehearsal `20260830T112201Z-25440-aebc6af8` passes in 421,567.688 ms
  from clean source revision `26c9c9f21c1155c47f8e7257dd9dc4eecb06b8f9`
  paired with WTZ Engine `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
  Development-only dirty-source and skip-build switches are both disabled.
- `paired-clean-clone` proves complete history, exact revisions and gitlink,
  the official 20-capability series, 50/50 probes, and 2/2 Release builds with
  zero verifier warnings or failures.
- `portable-shallow-heads` proves depth-one project and engine histories,
  unavailable official minimum/base objects, distinct synthetic heads, 50/50
  probes, and 2/2 Release builds. Its only output warnings are the two required
  portable-history warnings.
- Both scenario trees are clean before and after verification. The source
  revisions and statuses remain unchanged, the marked temporary root is safely
  removed, and no `wtz-zpr-*` directory remains under `%TEMP%`.
- The verifier mutation suite passes 6/6 and the exact Z 0 contract passes 18/18
  across all 15 mandatory domains. The broad Debug matrix passes 341 cases and
  its one fixture-conditioned concurrent-NPC case passes 1/1 in isolation,
  covering all 342 cases without a failed assertion.
- Content Z-level/mapping unit coverage passes 22/22. The 3/6/10-floor baseline
  passes 3/3 at 14.8139, 20.4496, and 23.0671 ms with exactly 6,336 allocated
  bytes and 100 percent warm-cache hits at every depth.
- The non-incremental single-worker Debug `SpaceStation14.slnx` build passes in
  2m39.24s with zero errors and 704 established warnings. The rehearsal also
  supplies four successful protected Release builds in isolated checkouts.

### Decisions

- Use shared read-only local Git object alternates for the complete-history
  scenario. The local WTZ Engine source is a partial/promisor clone, so forcing
  a fully materialized `--no-local` clone incorrectly requires unavailable
  historical blobs; an isolated index/worktree is the property this gate needs.
- Make the portable proof structurally stronger than a different branch name:
  the official history objects must be absent, both heads must differ, and both
  expected warnings must be observed before source probes or builds can count.
- Keep reports and temporary clones outside tracked source. Every recursive
  cleanup validates an absolute `%TEMP%` path, the `wtz-zpr-` prefix, and a
  run-owned marker; `-KeepWorktrees` is the only diagnostic retention path.
- Treat `-SkipBuild` and `-AllowDirtySourceForDevelopment` as dry-run aids only.
  The runner records them and refuses to present such a run as phase evidence.
- Close P8.3 on official-pair and rewritten-history portability evidence, while
  leaving foreign-fork runtime semantics and public-server readiness to P8.4.

### Completion Gate

- [x] Scope check: one rehearsal runner and synchronized porting, roadmap,
      hardening, and ledger documentation; runtime and engine source are
      unchanged.
- [x] Invariant review: exact pairing, rewritten history, submodule gitlinks,
      nested dependencies, source immutability, clean trees, and bounded cleanup
      are explicitly asserted.
- [x] Automated verification: 2/2 isolated scenarios, 50/50 probes plus 2/2
      builds in each mode, 6 self-tests, 18 Z 0 contracts, all 342 broad cases
      covered, 22 unit/mapping cases, 3 baselines, and the full solution build
      pass.
- [x] Performance evidence: no runtime path changed; the neutral baseline stays
      at 6,336 bytes at every depth and the complete rehearsal records its
      421,567.688 ms wall time and per-build reports.
- [x] Documentation: clean rehearsal, scenario guarantees, evidence location,
      development-switch limits, cleanup behavior, and semantic boundary are
      documented.
- [x] Dependency check: project gitlink and clean WTZ Engine checkout both equal
      published revision `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: implementation revision `26c9c9f21c1155c47f8e7257dd9dc4eecb06b8f9`
      is pushed and equals the remote branch; whitespace, ignored artifacts,
      source status, temporary worktrees, and dependency pairing pass review.
- [x] Mini review: clone materialization, native-output isolation, shallow
      history, failure cleanup, source immutability, and report assertions were
      exercised before phase closure.
- [x] Commit: package closes as `Close the WTZ Z-level porting phase` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: a useful portable proof must remove the historical objects that can
  accidentally make ancestry checks pass. Depth-one file clones plus synthetic
  paired commits establish that condition without reaching external remotes.
- Finding: PowerShell pipeline output is part of function return values. Routing
  child verifier output directly to the host keeps structured JSON as the sole
  return value and prevents report parsing from becoming order-dependent.
- Finding: the official pair and portable pair now pass from disposable trees,
  not because of untracked files, build products, indexes, or object reachability
  inherited from the development checkout.
- Residual risk: a source/compile-compatible foreign fork can still change
  runtime behavior around gameplay, rendering, networking, or maps. P8.4 owns
  representative runtime and release evidence.
- Residual risk: local shared alternates isolate worktrees and indexes but depend
  on the source object store for the duration of the rehearsal. This is suitable
  for a repeatable local gate, not an archival or supply-chain independence test.
- Next package: P8.4 defines measurable public-server acceptance thresholds,
  runs prolonged/release-sized and representative gameplay/mapping evidence,
  publishes operator diagnostics and recovery steps, and closes the roadmap.

## Completed Package: P8.4a PVS Context Reuse And Release Envelope

### Scope

- Reuse entity map, grid, tile, local-Z, and world-Z resolution across sessions
  scheduled in the same PVS update without caching the final visibility answer.
- Keep direct refreshes isolated, preserve every per-viewer range/boundary/render
  decision, and expose cache hits, misses, occupancy, and high-water occupancy.
- Extend the deterministic soak contract to schema 6 and add a fail-closed
  32-session Release envelope with explicit latency, allocation, reuse, and
  scheduler-debt thresholds.
- Keep the WTZ Engine revision, cache capacities, refresh cadence, visibility
  budget, sound authorization, and gameplay policy unchanged.

### Evidence

- Four pure scheduler tests and seven focused integration cases pass. They cover
  direct-refresh cache isolation, opening mutation, lower-floor light and
  occluder dependencies, PVS fail-open budgeting, remote-view overlays, and
  fail-closed cross-floor sound authorization.
- The complete Debug `FullyQualifiedName~ZLevel` matrix passes 341 cases; its
  single fixture-conditioned aperture-cache case passes 1/1 in isolation, so
  all 342 cases are covered without a failed assertion. Z-level/mapping units
  and analyzers pass 22/22.
- The paired schema 5 references record scheduler-frame p95 at 39.2928 and
  39.1741 ms. Two schema 6 context runs record 23.4610 and 24.1598 ms p95,
  28.1156 and 29.3039 ms p99, and an exact 90.625 percent hit rate.
- The official `-RequireReleaseEnvelope` run passes at 24.7161 ms p95,
  31.2166 ms p99, 48.3365 ms maximum, and 2,132,808 allocated bytes, or 16,663
  bytes per measured iteration. It performs 4,218,880 visibility checks and
  4,096 fair session refreshes with 3,823,360 hits, 395,520 misses, 1,030
  entries, zero deferred refreshes, zero budget exhaustion, zero workload
  collections, and a -100,568-byte retained-heap delta.
- A discarded final-decision-cache experiment reached only 2.08 percent hits
  and left p95 at 38.768 ms. That implementation is not retained in source.
- The generated 3/6/10-floor baseline passes 3/3 at 15.7674, 21.5457, and
  22.9755 ms with exactly 6,336 allocated bytes and zero warm-cache misses at
  every depth.
- The full non-incremental single-worker Debug solution build passes in
  3m06.28s with zero errors and 704 established upstream warnings.

### Decisions

- Cache reusable geometry rather than a final visibility result. Viewer map and
  world Z, range policy, current boundary state, and lower-floor render
  dependency rules remain authoritative on every logical check.
- Scope reuse to one synchronous scheduler batch. Both scheduled and direct
  entry points clear the table before work, and tests prove two direct calls do
  not share contexts across potential simulation updates.
- Count one cache lookup for every logical PVS visibility check. Schema 6 asserts
  that hits plus misses exactly equal shared PVS check metrics, so diagnostics
  cannot silently omit fallback paths.
- Set the official synthetic envelope to p95 <= 30 ms, p99 <= 33.333 ms,
  maximum <= 66.667 ms, hit rate >= 85 percent, and allocation <= 24 KiB per
  iteration, with no deferred refresh or budget exhaustion. These limits have
  measured headroom while still rejecting the schema 5 regression.
- Require the report itself to identify a Release testhost. A development
  `-NoBuild` rehearsal intentionally found and rejected a stale Debug binary
  before the clean Release gate passed.

### Completion Gate

- [x] Scope check: one shared visibility context API, one server batch cache,
      diagnostics, soak schema/runner, focused assertions, and synchronized
      documentation; no unrelated gameplay or engine behavior changed.
- [x] Invariant review: Z 0, local/world frames, moving grids, per-viewer server
      authority, mutable boundaries, visual fail-open, and audio fail-closed
      behavior remain explicit and tested.
- [x] Automated verification: 11 focused, all 342 broad cases covered, 22
      unit/mapping cases, 3 baselines, one official envelope run, and the full
      solution build pass.
- [x] Performance evidence: two paired references, one rejected experiment,
      two repeated context samples, and one executable envelope sample record
      latency, allocation, cache reuse, scheduler debt, GC, and retained heap.
- [x] Documentation: schema 6, cache lifetime, thresholds, rejected approach,
      commands, evidence, tradeoffs, and interpretation limits are recorded.
- [x] Dependency check: no WTZ Engine source changed; project gitlink and clean
      engine checkout remain paired at
      `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: whitespace, ignored artifacts, source status, focused diff, and
      dependency pairing pass review before publication.
- [x] Mini review: cache semantics, direct/scheduled lifetimes, fallback paths,
      mutable boundaries, allocation tradeoff, report identity, and residual
      dedicated-server risk were reviewed before committing.
- [x] Commit: package closes as `Reuse PVS geometry across Z-level viewers` on
      `zlevel/server-hardening`.

### Mini Review

- Finding: final visibility decisions are too viewer-specific to cache well in
  this workload. Reusing only candidate geometry removes repeated transform and
  grid resolution while retaining the inexpensive authoritative decision.
- Finding: the stable 90.625 percent hit rate follows from scheduler batching,
  not a long-lived cache. Entries are cleared in `finally` before control
  returns, while metrics retain only latest and high-water occupancy counts.
- Finding: the Release identity assertion is meaningful. `dotnet test
  --configuration Release --no-build` can execute the shared Debug artifact in
  this repository layout, and the envelope correctly fails that case.
- Residual risk: the context dictionary adds at most approximately 5.4 percent
  allocation in the measured profile. It remains inside the declared envelope,
  but P8.4b must inspect long-run heap and capacity after grid teardown.
- Residual risk: these captures use workstation GC and synthetic colocated
  viewers. They establish a deterministic regression gate, not the complete
  dedicated-server or representative-station SLA.
- Next package: P8.4b runs true Server GC profiles, prolonged 32/64-session
  endurance, repeated map/grid creation and deletion, and verifies that owned
  caches and retained memory return to explicit bounds.

## Completed Package: P8.4b Server GC Lifecycle And Capacity Envelopes

### Scope

- Audit boundary, sky, gravity, sound, and traversal cache ownership across
  repeated native-map creation, initialization, warming, and deletion.
- Run the deterministic 32-session soak and a separate 64-session capacity
  profile in a testhost that reports true Server GC.
- Add executable latency, allocation, scheduler-debt, and retained-memory
  envelopes without changing gameplay policy, cache capacity, PVS cadence, or
  the WTZ Engine dependency.
- Expose ownership diagnostics needed to compare live cache entries with order
  tokens, registrations, providers, and column indexes.

### Evidence

- The lifecycle harness warms 17 ownership counters on a native three-floor
  map and returns every counter to the exact pre-cycle baseline after each of 8
  warm-up and 128 measured create/delete cycles. A second two-map test removes
  one owner while retaining the other and proves surviving boundary and sound
  entries remain exact.
- Two Server GC lifecycle captures pass. The calibration records 17.385 ms p95,
  22.512 ms p99, 24.036 ms maximum, 865,684 allocated bytes per cycle, and a
  265,144-byte retained delta. The executable envelope confirmation records
  20.168 ms p95, 22.802 ms p99, 22.849 ms maximum, 865,682 bytes per cycle,
  and a 265,360-byte retained delta. Both observe 2/2/2 Gen0/Gen1/Gen2
  collections and exact final-state equality.
- The 32-session, 128-iteration Server GC smoke passes the P8.4a Release
  envelope at 23.315 ms p95, 26.437 ms p99, 90.63 percent context hits, and
  16,897 bytes per iteration. The 1,024-iteration endurance run evaluates
  28,525,291 candidates at 19.892 ms p95, 22.907 ms p99, 59.288 ms maximum,
  88.91 percent hits, and 15,164 bytes per iteration. Neither run accumulates
  scheduler debt or exhausts a budget.
- The repeated 64-session capacity gate passes at 44.535 ms p95, 53.109 ms
  p99, 79.219 ms maximum, 95.31 percent context hits, and 28,315 bytes per
  iteration, with zero deferred or exhausted refreshes.
- The canonical Debug `FullyQualifiedName~ZLevel` matrix passes 343 cases and
  conditionally skips one aperture-cache case; that case passes 1/1 in
  isolation, so all 344 cases are covered without a failed assertion. The
  narrower namespace matrix also passes 328/328, and the package's three
  targeted lifecycle/survivor tests pass 3/3.
- Content Z-level/mapping units pass 22/22. The final Debug 3/6/10-floor
  baseline passes at 10.6475, 15.6408, and 24.2960 ms with exactly 6,336
  allocated bytes and zero boundary, sky, or gravity warm-cache misses at every
  depth.
- The non-incremental single-worker Debug solution build succeeds in 2m58.17s
  with zero errors and 704 established upstream warnings.

### Decisions

- Compact FIFO order tokens after bulk grid/map removal. Ordinary tile
  invalidation keeps the existing bounded lazy-token policy; the capacity-based
  compactor still prevents unbounded growth without adding sort work to every
  mutation.
- Reuse boundary teardown and order-compaction scratch lists. Teardown should
  release owned entries without allocating another high-water object graph.
- Treat cache entries and order tokens as separate ownership counters. A cache
  count alone could report zero while stale keys still retained grid identity.
- Require the generated report, not merely the parent shell, to identify Server
  GC and the requested build configuration. Environment variables are restored
  in `finally` after every runner invocation.
- Keep the 32-session Release envelope and 64-session capacity envelope
  separate. The latter permits p95 <= 55 ms, p99 <= 66.667 ms, maximum <= 125
  ms, hits >= 90 percent, and allocation <= 40 KiB per iteration while still
  requiring zero scheduler debt and budget exhaustion.
- Use process-wide allocation and compacting full collections for lifecycle
  retention evidence. The prolonged soak's lack of an in-window collection is
  expected from only 15.5 MB of measured allocation and is not treated as proof
  of zero retention.

### Completion Gate

- [x] Scope check: cache ownership cleanup, diagnostics, Server GC runners,
      lifecycle tests, envelopes, and synchronized documentation only; no
      unrelated gameplay, content, cadence, or engine behavior changed.
- [x] Invariant review: component-free Z 0, map-local/world Z, moving grids,
      surviving owners, boundary-channel authority, and server-only lifecycle
      behavior remain unchanged and covered.
- [x] Automated verification: 3 targeted, all 344 broad cases covered, 328
      namespace cases, 22 unit/mapping cases, 3 baselines, 2 lifecycle runs, 3
      Server GC soak profiles, and the full solution build pass.
- [x] Performance evidence: lifecycle calibration and confirmation, 1,024-run
      endurance, and repeated 64-session capacity samples record latency,
      allocation, GC mode, cache ownership, debt, budgets, and retained heap.
- [x] Documentation: ownership model, commands, report schema, thresholds,
      measured evidence, high-water semantics, and interpretation limits are
      recorded in the hardening guide, overview, and ledger.
- [x] Dependency check: no WTZ Engine source changed; project gitlink and clean
      engine checkout remain paired at
      `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: whitespace, ignored artifacts, source status, focused diff,
      script parsing, dependency pairing, and remote identity pass review before
      publication.
- [x] Mini review: cache ownership, surviving-grid behavior, queue bounds,
      forced-GC interpretation, configuration identity, environment cleanup,
      threshold headroom, and residual fixture limits were reviewed.
- [x] Commit: package closes as `Bound Z-level cache ownership across server
      lifecycles` on `zlevel/server-hardening`.

### Mini Review

- Finding: dictionary cleanup was not sufficient ownership cleanup. Boundary
  and sound queues could retain stale grid-bearing tokens after bulk removal
  even though live entry counters looked correct.
- Finding: an empty-cache lifecycle and a surviving-cache lifecycle are both
  necessary. A final `Clear()` can make the former pass while hiding accidental
  deletion or retention when another owner remains active.
- Finding: the shared project output can make `--configuration Debug --no-build`
  execute a previously built Release assembly. The final Debug build and gates
  were rerun after detecting that identity ambiguity rather than mislabeling the
  earlier functional pass.
- Residual risk: the soaks use deterministic synthetic positions and content on
  one Windows host. The envelopes are regression gates, not a portable public
  server player-count guarantee.
- Residual risk: live grids intentionally retain bounded high-water workspaces.
  This package proves release on owner teardown, not continuous shrinkage while
  an unusually large grid remains alive.
- Next package: P8.4c defines `WTZ-RELEASE-1` as a fail-closed executable matrix
  for gameplay, mapping, initialized save/load, rendering evidence, Z 0, and
  engine/project pairing.

## Completed Package: P8.4c Executable WTZ-RELEASE-1 Matrix

### Scope

- Bind a clean WTZ Project revision, exact WTZ Engine revision, and matching
  `RobustToolbox` gitlink to one versioned release contract.
- Build the complete solution in Release and execute 41 exact tests across 19
  gameplay, mapping, persistence, and presentation domains.
- Compose the existing `WTZ-Z0-1`, `WTZ-PORT-1`, and real-client visual gates
  without permitting a missing, duplicate, renamed, or additional test to pass
  silently.
- Cover immediate PVS refresh after an attached viewer changes floors and keep
  required transform ancestors in transport without inflating candidate
  metrics.
- Record machine-readable evidence and distinguish strict `Passed` runs from
  all development-only bypasses.

### Development Evidence

- Manifest validation accepts exactly 19 required domains, 41 protected tests,
  three protected composite gates, one full build target, the declared minimum
  project revision, and the exact engine/gitlink revision.
- Eight fail-closed self-tests accept the canonical contract and reject a
  missing domain, missing entry, duplicate test, unprotected project, missing
  child gate, weakened clean-source policy, and reduced visual count.
- Development run `20260830T154257Z-48560-a8427ecd` passes 38/38 integration
  and 3/3 unit tests, 18/18 Z 0 tests, 50/50 port probes, and 15 captures with
  24/24 visual checks. It correctly reports `DevelopmentPassed` because the
  package source was not yet committed.
- A separate deterministic real-client run passes exactly 15 captures and
  24/24 checks. Sandbox round setup and explicit admin acquisition remove
  unrelated fixture mutation and observer-role timing from capture identity.
- The broad Release Z-level run passes 343 tests, conditionally skips two
  long fixture-dependent cases, and fails zero of 345 total cases. The three
  neutral 3/6/10-floor baselines and 22 unit/mapping tests pass.
- The non-incremental, single-worker Debug solution build succeeds in 2m54.07s
  with zero errors and 688 established warnings.

### Strict Evidence

- The implementation is published as
  `63d1b7ac91caed1ac41211a9f2b900177b700153`; the remote branch resolves to the
  same hash before the strict run begins. WTZ Project and WTZ Engine are clean,
  and the engine checkout/gitlink remain exactly
  `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- Strict run `20260830T161628Z-2640-16179cd2` reports `Passed` after
  443,721.9441 ms. Its full Release `SpaceStation14.slnx` build succeeds with
  zero errors and 701 established warnings.
- All 19 domains execute exactly 41 declared tests and pass 41/41 with no skip,
  failure, duplicate, or undeclared TRX result. The 38 integration and 3 unit
  contracts both pass independently.
- All 3 composite gates pass: `WTZ-Z0-1` at 18/18, `WTZ-PORT-1` at 50/50, and
  `WTZ-VISUAL-1` at exactly 15 captures and 24/24 checks. The visual child
  completes in 37.19041 seconds.
- The parent report confirms clean project/engine trees and all three
  development flags false. Recomputed child hashes match the parent, and zero
  game server/client processes remain after cleanup.
- Manifest SHA-256 is
  `23e69f353123a226fe863a961a3806c335b08dd70263d3c699c90b663d932e14`;
  parent report SHA-256 is
  `c134477e9d970294179b5cd305ed4ff67df51b423609a1bb89d1ec5dd92cf5d0`.
  Child hashes are `aab235d638d45c4b0d4d0714742d9c146e3235cc86addf53285a591ced2fe2a4`
  for Z 0, `c5371905b46ffa39ebdbe89f625d4a0e8072520971077da6813e7cbc07028e00`
  for port pairing, and
  `7ec8448f2b5d934a3453fc3d085b8b8eb50cc30583cfd9bab98252f607f938a5`
  for the real-client visual record.

### Decisions

- Protect exact fully-qualified tests and reject extra TRX results. A broad
  filter remains useful regression evidence but cannot define a stable release
  contract.
- Require a full Release solution build once in the parent. The port child may
  skip its duplicate builds only because the parent verifies the exact same
  paired revisions before invoking it.
- Treat every dirty-source, skipped-build, or skipped-visual switch as a
  development run. `DevelopmentPassed` is useful while implementing the gate
  but is never release evidence.
- Refresh server PVS synchronously for the attached in-game session after a Z
  change. Mapping and snapshot actors may transiently have no session and are
  ignored rather than treated as an invalid runtime state.
- Preserve transform ancestors as PVS transport dependencies. Count only the
  original candidate intersection in metrics so `visible + culled ==
  candidates` remains exact.
- Make the visual fixture deterministic with Sandbox and explicit admin state;
  do not weaken pixel checks to accommodate unrelated round variation.

### Completion Gate

- [x] Scope check: the focused source, manifest, runners, tests, and synchronized
      documentation are confined to the executable release contract and the
      PVS behavior it exposed.
- [x] Invariant review: Z 0 compatibility, world/local Z, moving grids, server
      authority, engine PVS hierarchy, and boundary channels are represented by
      exact tests or composed child gates.
- [x] Automated verification: self-tests, manifest validation, complete
      development matrix, focused PVS/persistence cases, broad Z-level suite,
      unit/mapping filter, baselines, visual gate, and solution build pass.
- [x] Performance evidence: this package does not change scheduler cadence or
      cache policy; existing P8.4a/P8.4b envelopes remain the performance
      authority and broad metrics conservation passes after the PVS correction.
- [x] Documentation: contract, commands, reports, development evidence,
      limitations, and porting implications are recorded in the release guide,
      overview, hardening guide, porting guide, and ledger.
- [x] Dependency check: no WTZ Engine source changed; the checkout and gitlink
      remain paired at `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: publish the implementation revision, verify its remote hash,
      and run the strict gate from the resulting clean project and engine trees.
- [x] Mini review: fail-closed validation, exact test ownership, report hashes,
      source identity, PVS hierarchy, metrics conservation, fixture determinism,
      and residual package advisories were reviewed.
- [x] Commit: package closes as `Close executable WTZ Z-level release matrix`
      after strict `WTZ-RELEASE-1` reports `Passed` with 41/41 tests, 3/3
      composites, clean trees, and no development bypasses.

### Mini Review

- Finding: changing the attached viewer's floor updated its Z component before
  the next scheduled PVS cycle, leaving a short stale exclusion window. The
  floor-change event now refreshes that session immediately.
- Finding: preserving a visible child was insufficient when engine PVS excluded
  its grid or map parent. The bounded transform chain is now kept as transport
  state while metrics remain candidate-only.
- Finding: randomized Sandbox-external round variation altered fixture walls
  and shadows. The visual runner now fixes the preset and admin lifecycle rather
  than broadening expected pixels.
- Finding: an initial broad baseline exposed ancestor inflation in visibility
  metrics. Candidate/visible intersection counting restores exact conservation;
  the 3/6/10 baselines and complete broad suite pass after the correction.
- Residual risk: real-client output and timing remain host-specific, and the
  exact matrix does not exercise every upstream gameplay path or station map.
- Residual risk: established advisories in cryptography and legacy Pow3r
  dependencies remain upstream package work; this release matrix records but
  does not waive them.
- Next package: P8.4d adds operator-facing diagnostics and recovery procedures,
  exercises representative deployment and failure recovery, audits the complete
  P0-P8 evidence chain, and makes the final public-server readiness decision.

## Completed Package: P8.4d1 Operational Health And Autosave Telemetry

### Scope

- Add process-local initialized-map autosave/checkpoint counters and retain the
  latest attempt, successful path, validation report, and failure diagnostic.
- Expose an on-demand, server-authoritative operational health snapshot with a
  stable machine-readable contract and actionable findings.
- Compose existing map validation, PVS, trace, sky, explosion, sound,
  pathfinding, cache-ownership, gravity, flight, elevator, session, and Server
  GC signals without adding normal tick work.
- Integrate autosave counters into `zlevelmetrics` output and its explicit
  process-local reset.

### Evidence

- Four pure evaluator/JSON contract cases pass, including exact schema and
  finding codes for healthy, degraded, and critical reports.
- The focused health/autosave/mapping unit set passes 13/13. The complete
  comparable Z-level/mapping unit matrix passes 26/26 with no skip.
- The initialized-map autosave scenario passes 1/1 after proving an invalid
  authored floor records a critical validation/autosave failure, a corrected
  atomic snapshot records recovery, and reset clears telemetry without
  changing schedules.
- Six persistence classes pass 12/12 across initialized autosave, mutation,
  double snapshot, map format, correlated save protocol, and flight mapping.
- The broad Debug Z-level integration run passes 343, conditionally skips the
  same two fixture-dependent cases, and fails zero of 345 total cases.
- Generated 3/6/10-floor baselines pass 3/3 at 11.2849, 17.0720, and 24.9303
  ms. Every measured workload allocates 6,336 bytes, retains 100 percent warm
  boundary/sky/gravity cache hits, and records zero relevant exhaustion or
  eviction.
- A non-incremental single-worker Debug solution build succeeds in 2m39.75s
  with zero errors and 688 established warnings.
- Implementation revision `8ca03af39e91fc4a0e2f6225d6616a5c33d43cc5`
  is published on `origin/zlevel/server-hardening`, and the remote branch
  resolves to the exact same hash.

### Decisions

- `WTZ-OPS-HEALTH-1` schema 1 is a snapshot of process-local evidence, not a
  remote monitoring protocol or a substitute for the executable release gate.
- Capture and full map validation occur only when an administrator invokes
  `zlevelhealth`; normal server updates receive no new scan or allocation.
- Invalid authored maps, failed latest checkpoints, PVS fail-open pressure,
  hard trace exhaustion, and impossible bounded-cache ownership are critical.
  Recovered incidents and bounded subsystem debt remain degraded warnings with
  explicit operator actions.
- Do not invent host-specific latency thresholds in the health evaluator.
  P8.4a/P8.4b executable envelopes remain authoritative for latency, allocation,
  GC, and player-count evidence.
- `zlevelmetrics reset` clears observations only. It never changes active
  autosave schedules, deletes checkpoints, or repairs map state.

### Completion Gate

- [x] Scope check: source changes are confined to on-demand health reporting,
      autosave telemetry, command presentation, and their tests/docs.
- [x] Invariant review: map state remains server authoritative; validation uses
      world-Z-aware existing map rules; no boundary or moving-frame behavior is
      changed.
- [x] Automated verification: 13/13 focused unit, 26/26 comparable unit,
      1/1 autosave, 12/12 persistence, 343 pass plus two conditional skips
      broad, 3/3 baseline, and the full solution build pass.
- [x] Performance evidence: on-demand collection adds no tick work, while the
      unchanged warmed baseline remains at 6,336 bytes with fully hot caches.
- [x] Documentation: command, schema, status policy, reset semantics, limits,
      evidence, and residual risks are recorded in the hardening guide,
      overview, and ledger.
- [x] Dependency check: WTZ Engine source is unchanged and the checkout/gitlink
      remain paired at `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [x] Git check: whitespace and staged scope checks pass; implementation
      revision `8ca03af39e9...` is published and matches the remote; WTZ Project
      and WTZ Engine are clean at closure.
- [x] Mini review: collection cost, severity policy, map validation, reset
      scope, serialization stability, and false-positive risks were reviewed.
- [x] Commit: package implementation is saved and pushed as
      `Expose Z-level operational health`.

### Mini Review

- Finding: autosave previously exposed only a boolean schedule and ephemeral
  error return, so an operator could not distinguish no attempt, current
  failure, or successful recovery after an incident.
- Finding: PVS `VisibilityContextCacheMaxEntries` is a historical peak, not a
  configured capacity. It remains diagnostic and must not produce a false
  over-capacity finding.
- Finding: complete Z-level map validation scans authored state and is too
  expensive for a periodic tick. Keeping it command-triggered preserves the
  measured gameplay path.
- Residual risk: counters and latest diagnostics are process-local and reset on
  restart; durable incident history belongs in external logs/reports.
- Residual risk: command output can contain checkpoint paths and validation
  details, so `zlevelhealth` remains restricted to administrators with Debug
  permission.
- Next package: P8.4d2 adds an explicit validated checkpoint operation and an
  executable save/load recovery rehearsal with a versioned result contract.

## Active Package: P8.4d2 Validated Checkpoint And Recovery Rehearsal

### Scope

- Add a Server+Mapping administrator command that creates a manual checkpoint
  only from a complete initialized map root.
- Reuse initialized mapper validation, transient filtering, detached
  normalization, strict UTF-8, exclusive temporary creation, flush, and atomic
  promotion rather than introducing another serializer.
- Distinguish manual checkpoint files from scheduled autosaves while preserving
  collision-safe append-only destinations.
- Add a fail-closed executable recovery rehearsal and bind its exact test,
  source pair, steps, hashes, and structural result to `WTZ-RECOVERY-1`.

### Evidence

- The protected rehearsal passes 1/1 after refusing pre-init map and grid-only
  requests, creating a checkpoint through the actual command with autosave
  disabled, rejecting an invalid authored floor, and preserving the known-good
  bytes and sole visible destination.
- It deletes the corrupt source map, loads the first checkpoint, creates a
  second checkpoint from the recovered map, loads it again, and compares exact
  ordered map format, grid, tile, and persistent-entity fingerprints across all
  three authored states. Players and explicit transients remain excluded.
- Development runner `20260830T173056Z-33780-04cdcd0c` passes its exact TRX and
  all report checks with report SHA-256
  `5241b13ac7ca63646ab97cc3c66539ee98212e8942091d3319fdd20592c01aa5`.
  A separate negative invocation rejects dirty source before test execution.
- Focused mapping/health units pass 14/14; the comparable Z-level/mapping unit
  matrix passes 27/27. The expanded persistence matrix passes 13/13.
- The broad Debug Z-level suite passes 344, conditionally skips two pooled
  fixture cases, and fails zero of 346 total cases.
- Generated 3/6/10-floor baselines pass 3/3 at 13.6285, 19.1483, and 26.6672
  ms. Every measured workload allocates 6,336 bytes, retains 100 percent warm
  boundary/sky/gravity cache hits, and records zero relevant exhaustion or
  eviction.
- A non-incremental single-worker Debug solution build succeeds in 3m03.41s
  with zero errors and 688 established warnings.

### Decisions

- `zlevelcheckpoint <map-id> <checkpoint-name>` works independently of the
  scheduled-autosave CVar but still requires an initialized map root. It writes
  beneath the configured autosave directory in the named subdirectory.
- Checkpoints use `-CHECKPOINT.yml`; autosaves retain `-AUTO.yml`. Existing
  files are never replaced, and same-millisecond collisions receive a numeric
  suffix.
- A checkpoint is mapper-authored recovery state, not live-round persistence.
  Players, minds, sessions, explicit transients, active queues, and simulation
  caches remain intentionally excluded.
- Recovery remains an explicit operator procedure. The command neither deletes
  a damaged live map nor automatically swaps a loaded checkpoint into a round.
- `Passed` requires clean paired project/engine source and a real build.
  Dirty-source or skipped-build invocations can report only
  `DevelopmentPassed`.

### Completion Gate

- [x] Scope check: source changes are confined to the shared persistence
      wrapper, checkpoint command, one path test, one recovery scenario/contract,
      runner, and synchronized documentation.
- [x] Invariant review: complete initialized map ownership, authored Z range,
      Z 0 compatibility, transient filtering, atomic visibility, and structural
      double-round-trip identity are covered.
- [ ] Automated verification: all development matrices pass; publish the
      implementation and require one clean-source `WTZ-RECOVERY-1 Passed` run.
- [x] Performance evidence: checkpoint work is command-triggered and the
      neutral 3/6/10 baseline remains at 6,336 bytes with fully warm caches.
- [x] Documentation: command, storage, recovery sequence, report contract,
      development evidence, and limitations are recorded in the save/load,
      hardening, overview, and ledger documents.
- [x] Dependency check: WTZ Engine source is unchanged and checkout/gitlink
      remain paired at `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`.
- [ ] Git check: run final diff/status review, publish, verify the remote hash,
      and rerun the gate from clean paired source.
- [x] Mini review: serializer reuse, command authority, expected failure
      handling, append-only naming, source identity, and recovery semantics
      were reviewed.
- [ ] Commit: save and push the package as an isolated implementation revision.

### Mini Review

- Finding: invoking an expected failing console command makes the integration
  harness fail on `WriteError`. Successful checkpoints remain tested through
  the real command; the deliberate corruption is tested through its exact
  shared API without weakening operator error output.
- Finding: a successful recovery still reports `Degraded` until metrics reset,
  because the earlier refused checkpoint is retained as incident evidence.
  Absence of critical findings, not a premature `Healthy`, is the correct
  post-recovery assertion.
- Finding: the first checkpoint bytes and directory entry count must both be
  checked after refusal. Loadability alone would not prove that a known-good
  artifact was not silently replaced.
- Residual risk: checkpoints inherit filesystem rename/durability limits and do
  not provide automatic stale-temporary scavenging after process or host crash.
- Residual risk: the rehearsal uses a deterministic three-floor fixture. The
  complete initialized-map and release matrices remain responsible for complex
  references, atmosphere, elevators, moving grids, and production content.
- Next package: P8.4d3 composes health, recovery, release, Z 0, porting, visual,
  soak, baseline, and manual operations evidence into the final P0-P8 gate.

## Package History

| Date | Package | Commit | Verification | Result |
| --- | --- | --- | --- | --- |
| 2026-08-30 | P8.4d1 | `Expose Z-level operational health` | 4 evaluator/JSON, 13 focused unit, 26 unit/mapping, 1 autosave, 12 persistence, 343 pass + 2 conditional skips broad, 3 baseline, full build, semantics/performance/diff/dependency/remote review | Complete |
| 2026-08-30 | P8.4c | `Close executable WTZ Z-level release matrix` | strict WTZ-RELEASE-1 41/41 + 3/3, 8 self-tests, 343 pass + 2 conditional skips broad, 22 unit/mapping, 3 baseline, full builds, exact source/report/diff/dependency/remote review | Complete |
| 2026-08-30 | P8.4b | `Bound Z-level cache ownership across server lifecycles` | 3 targeted, all 344 broad covered, 328 namespace, 22 unit/mapping, 3 baseline, 2 lifecycle + 3 Server GC profiles, full build, ownership/performance/diff/dependency review | Complete |
| 2026-08-30 | P8.4a | `Reuse PVS geometry across Z-level viewers` | 11 focused, all 342 broad covered, 22 unit/mapping, 3 baseline, 2 repeated + 1 envelope Release soak, full build, performance/diff/dependency review | Complete |
| 2026-08-30 | P8.3c / P8.3 gate | `Close the WTZ Z-level porting phase` | 2 clean modes, 100 probes, 4 Release builds, 6 self-tests, 18 Z 0, all 342 broad covered, 22 unit/mapping, 3 baseline, full build, cleanup/diff/dependency/remote review | Complete |
| 2026-08-30 | P8.3b | `Define the WTZ Z-level port contract` | 6 self-tests, 20 capabilities, 50 probes, 2 builds, 18 Z 0, 342 broad, 22 unit/mapping, 2x3 baseline, full build, diff/dependency review | Complete |
| 2026-08-30 | P8.3a | `Make Z 0 compatibility executable` | 3 new, 18 contract, all 342 broad covered, 2 isolated pooled, 22 unit/mapping, 3 baseline, full build, diff/dependency review | Complete |
| 2026-08-30 | P8.2c | `Reuse Z-level gravity field workspaces` | 4 focused, 1 Debug + 2 Release soaks, 339 broad covered, isolated cache, 22 unit/mapping, 3 baseline, full build, allocation/diff/dependency review | Complete |
| 2026-08-30 | P8.2b | `Stagger Z-level PVS refresh work` | 4 scheduler, 1 Debug + 2 Release soaks, 17 focused, 337 broad, 22 unit/mapping, 3 baseline, full build, diff/dependency review | Complete |
| 2026-08-30 | P8.2a | `Attribute Z-level server workload costs` | Debug/Release builds, 1 short Debug soak, 2 full Release soaks, byte conservation, diff/dependency review | Complete |
| 2026-08-30 | P8.1 | `Measure deterministic Z-level server scale` | 2 Release soaks, 337 complete Debug, 321 namespace Release, isolated pooled cache, 18 unit/mapping, 5 interaction, 3 baseline, full build, diff/dependency review | Complete |
| 2026-08-30 | P7 gate | `Close Z-level vertical content phase` | 135 cross-package, all 336 broad covered, 18 unit/mapping, 2x3 baseline, 2x24 real GL, full build, architecture/diff review | Complete |
| 2026-08-30 | P7.4b2b | `Navigate authored Z-level flight corridors` | 6 focused, 1 official map, 38 path, 336 broad, 18 unit/mapping, 2x3 baseline, full build, warning/diff review | Complete |
| 2026-08-30 | P7.4b2a | `Trace active Z-level flight heights` | 3 new, 72 consumer, 330 broad cases covered, 18 unit/mapping, 2x3 baseline, full build, warning/diff review | Complete |
| 2026-08-30 | P7.4b1 | `Add native Z-level flight controls` | 14 focused, 327 broad cases covered, 18 unit/mapping, 3 baseline, full build, warning/diff review | Complete |
| 2026-08-30 | P7.4a | `Define native Z-level flight physics` | 10 focused, 31 movement/map, 322 broad, 11 unit/mapping, 3 baseline, full build, allocation/warning/diff review | Complete |
| 2026-08-29 | P7.3b | `Present weather on active Z levels` | 8 focused, all 314 broad covered, 14 unit/mapping, 3 baseline, 24 real GL, full build, allocation/warning/diff review | Complete |
| 2026-08-29 | P7.3a | `Define shared Z-level weather exposure` | 3 focused, all 309 broad covered, 9 unit, 3 baseline, full build, allocation/diff review | Complete |
| 2026-08-29 | P7.2b | `Integrate physical elevators with Z-level navigation` | 14 elevator, 3 focused consumers, 306 broad, 9 unit, 3 baseline, full build, warning/performance/diff review | Complete |
| 2026-08-29 | P7.2a | `Add powered Z-level elevator cabins` | 6 focused, 297 broad, 9 unit, 3 baseline, full build, authority/performance/diff review | Complete |
| 2026-08-29 | P7.1b | `Add authored Z-level vertical surfaces` | 6 focused, 27 path/content, 38 consumers, 291 cases covered, 9 unit, 3 baseline, full build, analyzer/diff review | Complete |
| 2026-08-29 | P7.1a | `Add bounded Z-level sky exposure` | 15 focused, 285 cases covered, 9 unit, 3 baseline, full build, allocation/LRU/diff review | Complete |
| 2026-08-29 | P6 gate | `Close Z-level persistence phase` | 1 engine filter, 5 engine atomic, 11 persistence, 17 unit, 3 baseline, architecture/diff review | Complete |
| 2026-08-29 | P6.3b2 | `Autosave initialized mapping snapshots` | 1 autosave, 4 persistence, 11 mapping, 4 writer, 17 unit, 279 cases covered, 3 baseline, full build, diff review | Complete |
| 2026-08-29 | P6.3b1 | `Enable initialized Z-level floor mutations` | 1 connected, 10 mapping, 278 broad, 13 unit, 3 baseline, full build, diff review | Complete |
| 2026-08-29 | P6.3a | `Prove initialized Z-level map idempotence` | 3 focused/official, 11 mapping, 277 broad cases covered, 13 unit, 3 baseline, full build, diff review | Complete |
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
| 2026-08-29 | P5.3b | `Compose hierarchical Z-level paths` | 8 focused, 253 Content integration, 9 Content unit/analyzer, 3 baseline, full build, budget/timing/diff review | Complete |
| 2026-08-29 | P5.4a | `Execute hierarchical Z-level NPC routes` | 18 focused, 263 Content integration, 9 Content unit/analyzer, 3 baseline, full build, lifecycle/timing/diff review | Complete |
| 2026-08-29 | P5.4b1 | `Model dynamic Z-level traversal state` | 5 dynamic, 36 movement/pathfinding, 269 Content integration, 9 Content unit/analyzer, 3 baseline, full build, lifecycle/allocation/diff review | Complete |
| 2026-08-29 | P5.4b2 | `Harden hierarchical Z-level pathfinding` | 11 focused, 40 movement/pathfinding, 274 Content integration, 9 Content unit/analyzer, 3 baseline, full build, 8-NPC/512-mutation/cache/diff review | Complete |
| 2026-08-29 | P6.1 | `Allow read-only filtered entity snapshots` / `Filter initialized mapping snapshots` | 1 engine, 19 serialization, 1 snapshot, 8 mapping, 275 Content integration, 9 unit/analyzer, 3 baseline, full build, diff review | Complete |
| 2026-08-29 | P6.2a | `Report invalid entity references during map loads` / `Normalize initialized mapping snapshots` | 1 engine, 19 serialization, 1 snapshot, 7 format/snapshot, 2 mapping, all 275 Content cases covered, 9 unit/analyzer, 3 baseline, full build, diff review | Complete |
| 2026-08-29 | P6.2b | `Correlate initialized mapping saves` | 3 tracker, 1 real protocol, 10 mapping, 276 Content integration, 12 unit/analyzer, 3 baseline, clean full build, diff review | Complete |
| 2026-08-29 | P6.2c | `Add atomic file dialog writes` / `Persist mapping snapshots atomically` | 5 atomic, 42 engine client, 4 mapping unit, 1 protocol, 10 mapping integration, 276 Content integration, 13 unit/analyzer, 3 baseline, clean full build, diff review | Complete |
