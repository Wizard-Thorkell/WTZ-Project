# WTZ Z-Level Implementation Ledger

This file is the resumable source of truth for the active Z-level implementation
goal. Update it in the same commit as every completed work package.

## Goal Status

- Goal: execute phases P0 through P8 of the WTZ native Z-level roadmap.
- Base branch: `zlevel-roadmap`.
- Active branch: `zlevel/baseline-stress-fixtures`.
- Active package: `P0.3 Configurable budgets and baseline report`.
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
| P0 | Baselines, stress fixtures, metrics, budgets, and observability | In progress |
| P1 | Shared geometric `ZLevelTrace` primitive and boundary crossings | Pending |
| P2 | Hitscan, projectiles, throws, explosions, effects, and interactions | Pending |
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
| P0.3 | Configurable budgets, fail-soft behavior, and baseline report | In progress |

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

## Active Package: P0.3 Configurable Budgets And Baseline Report

### Planned Scope

- Inventory current Z-level cache capacities, per-frame limits, and bounded
  scans, including the 4,096-entry boundary cache exposed by P0.2.
- Move appropriate performance policy to named configuration values with safe
  defaults and documented minimums or clamps.
- Define deterministic fail-soft behavior when each budget is exhausted and add
  observability for exhaustion events.
- Re-run the P0.2 matrix before and after the policy change and publish the
  comparison without converting local timings into brittle test thresholds.

## Package History

| Date | Package | Commit | Verification | Result |
| --- | --- | --- | --- | --- |
| 2026-08-27 | P0.1 | `Add native Z-level performance observability` | 46 integration, 2 unit, diff check | Complete |
| 2026-08-27 | P0.2 | `Add deterministic Z-level stress baselines` | 3 baseline cases, 49 integration, 2 unit, diff check | Complete |
