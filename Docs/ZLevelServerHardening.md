# WTZ Z-Level Server Hardening

This document owns the P8 operational evidence for native Z-level servers. It
starts with the deterministic P8.1 scale harness and will accumulate the
evidence-driven runtime limits, Z 0 compatibility matrix, porting contract, and
public-server release gate from P8.2 through P8.4.

## P8.1 Workload Contract

`ZLevelServerSoakTest` builds the same generated station and moving-grid fixture
used by the P0 baseline, then adds configurable load in four independent axes:

- native floor count;
- simultaneously attached in-game sessions;
- candidate entities copied at each representative station and moving-grid
  tile;
- warm-up and measured structural mutation iterations.

Every iteration deterministically moves and rotates the secondary grid,
redistributes viewers across both local Z frames, removes and restores one safe
station floor tile, queries the boundary/visibility/sky/gravity consumers,
routes sound through a stable vertical shaft, rebuilds and reuses the traversal
snapshot, and refreshes Z-aware PVS plus sound playback for every session.

The removed tile is restored before the next iteration completes. The harness
also drains pending gravity refreshes and verifies the final tile inventory and
map declaration, so a passing performance run is also a lifecycle check.

## Running The Harness

The ordinary integration suite uses a bounded profile of 10 floors, 4 sessions,
2 candidate copies per tile, 2 warm-up iterations, and 8 measured iterations.
Run that case directly with:

```powershell
dotnet test Content.IntegrationTests/Content.IntegrationTests.csproj `
  --filter FullyQualifiedName~Content.IntegrationTests.Tests.ZLevel.ZLevelServerSoakTest
```

The checked-in runner defaults to the P8.1 release profile: Release build, 10
floors, 32 sessions, 8 candidate copies per tile, 8 warm-up iterations, and 128
measured iterations.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass `
  -File Tools/run_zlevel_server_soak.ps1
```

All load axes are explicit script parameters. `-NoBuild` reuses an existing
binary, and `-OutputDirectory` selects the report location. The runner rejects
out-of-range settings, restores prior environment variables, removes stale
output, and checks that the report contains the requested profile.

## Report Contract

The runner writes `artifacts/zlevel-server-soak/zlevel-server-soak.json`. Schema
version 4 records:

- host runtime, architecture, logical processors, GC mode, and build mode;
- every configured cache and work budget consumed by the workload;
- generated geometry, frame origin, entity density, and gravity source count;
- warm-up and measured elapsed time, thread allocation, heap before and after a
  forced collection, and GC collection counts;
- min/average/p50/p95/p99/max latency summaries for complete iterations and
  individual per-session PVS refresh calls;
- the same latency summary plus main-thread allocation for movement/viewer
  updates, open mutation, vertical consumers, sound, traversal, the complete PVS
  cycle, restoration, restored consumers, and unattributed work;
- complete-iteration latency split by whether a Gen0/Gen1/Gen2 collection was
  observed during that iteration;
- scheduler-update counts, due/scheduled/deferred work, budget exhaustion, batch
  maxima, and per-scheduler-frame latency;
- shared boundary, sky, visibility, gravity, PVS, and trace metrics;
- sound portal, route, and per-session playback metrics;
- traversal snapshot metrics and final bounded-cache lifecycle state.

Timing and heap values are evidence, not machine-independent assertions. The
test instead enforces deterministic correctness and safety invariants: all
sessions are evaluated, no configured PVS/sound budget is exhausted, no PVS
candidate fails open, cache counts remain within capacity, structural changes
produce invalidations and rebuilds, graph snapshots become current and reusable,
pending gravity work drains, and authored geometry is restored.

## P8.1 Release Evidence

The confirming profile ran twice in a Release integration server on Windows
10.0.19045 x64, .NET 10.0.4, 28 logical processors, and workstation GC. Each
run used 10 floors, 32 attached sessions, 960 representative candidate
entities, 36 traversal nodes, 8 warm-up iterations, and 128 measured structural
iterations.

| Measurement | Run 1 | Run 2 |
| --- | ---: | ---: |
| Total measured time | 8,083.789 ms | 7,863.530 ms |
| Main-thread allocation | 170,159,064 B | 170,198,960 B |
| Retained heap delta after GC | -3,586,056 B | -452,544 B |
| Workload GC collections (Gen0/Gen1/Gen2) | 7 / 2 / 0 | 7 / 2 / 0 |
| Iteration p50 / p95 / p99 | 25.449 / 147.554 / 167.452 ms | 21.572 / 131.913 / 158.816 ms |
| PVS refresh p50 / p95 / p99 | 0.719 / 4.774 / 5.555 ms | 0.473 / 3.591 / 5.168 ms |
| PVS refresh maximum | 29.190 ms | 27.814 ms |

Both runs produced exactly 4,096 PVS refreshes, 4,218,880 candidates and
visibility checks, 2,330,864 boundary queries, 256 gravity builds, 128
successful vertical sound routes, and 256 traversal snapshot builds plus 256
immediate cache hits. No PVS candidate failed open and no PVS, sky, sound
portal, sound route, sound playback, or traversal budget exhausted.

The final state remained bounded: boundary cache 8,192/8,192 with only 16
evictions across 2.33 million queries, sky cache 424/4,096, sound portal cache
17/4,096, one map-scoped traversal snapshot, and zero pending gravity refreshes.
All authored tiles were restored. Deterministic counters matched between runs;
wall time differed by 2.72 percent and allocation by 39,896 bytes.

The evidence does not justify larger caches. It does expose a reproducible
long-tail frame risk: median complete iterations fit under one 30 Hz tick, but
p95 exceeds 130 ms even though individual PVS p95 remains below 5 ms. P8.2 must
therefore instrument the iteration by subsystem, attribute allocation and
latency tails, and bound or schedule the expensive work before changing cache
capacity. The first candidates are per-session candidate discovery and culling,
full gravity-field rebuilds after paired tile mutations, and stop-the-world
Gen0/Gen1 collections. A dedicated-server GC profile remains required by P8.4;
these workstation-GC captures must not be treated as portable production SLA.

## P8.2 Attribution Evidence

Schema 3 is an append-only extension of the P8.1 report. Two equivalent Release
captures retained the 10-floor, 32-session, 960-candidate, 8-warm-up, and
128-measured-iteration profile while attributing each iteration by owner.

| Measurement | Attribution run 1 | Attribution run 2 |
| --- | ---: | ---: |
| Total measured time | 7,160.518 ms | 7,123.143 ms |
| Main-thread allocation | 170,175,768 B | 170,175,552 B |
| PVS batch p50 / p95 / p99 | 17.714 / 111.475 / 130.917 ms | 15.748 / 110.789 / 132.784 ms |
| Open vertical consumers p50 / p95 / p99 | 2.975 / 8.145 / 12.095 ms | 2.667 / 9.638 / 15.505 ms |
| Restored consumers p50 / p95 / p99 | 1.803 / 3.746 / 5.226 ms | 1.791 / 3.519 / 5.646 ms |
| Gravity build time | 631.818 ms | 626.953 ms |
| PVS refresh time | 6,064.464 ms | 6,039.005 ms |
| Iterations with / without a collection | 7 / 121 | 7 / 121 |

The deterministic counters still match exactly and total time differs by only
0.52 percent. PVS owns approximately 85 percent of measured runtime and its
32-session batch reproduces the long tail. The two gravity-consumer stages own
168,249,344 bytes in both runs, approximately 98.9 percent of measured
allocation, because every removal and restoration forces a complete field
rebuild. PVS allocates only 409,600 and 411,040 bytes in the complete runs.

Collections do not explain the PVS tail: iterations without a collection still
reach p95 above 123 ms and maximums above 165 ms. P8.2 therefore has two
independent production targets. First, stagger session refreshes across ticks so
the existing 10 Hz cadence does not intentionally batch every player into one
frame. Second, remove repeated full-grid gravity topology materialization from
single-tile invalidation without weakening connectivity correctness. Cache
capacities remain unchanged.

## P8.2 PVS Scheduling Evidence

`ZLevelPvsSystem` now turns the existing 10 Hz per-session target into fractional
refresh credit on every server update. A circular cursor consumes that credit in
fair order, while `zlevel.pvs_max_session_refreshes_per_update` caps one update
between 1 and 256 sessions and defaults to 16. Overdue credit is retained and
reported rather than silently dropped. Sessions leaving `InGame` immediately
clear visual culling and sound authorization through the player-status event.

The schema 4 soak executes the real scheduler three times at 30 Hz for each
structural iteration. With 32 sessions it deterministically schedules 10, 11,
and 11 refreshes, preserving the same 4,096 refreshes and 4,218,880 candidate
checks as the schema 3 batch.

| Measurement | Scheduler run 1 | Scheduler run 2 |
| --- | ---: | ---: |
| Total measured time | 7,386.905 ms | 7,099.772 ms |
| Main-thread allocation | 170,281,832 B | 170,266,272 B |
| Scheduler frame p50 / p95 / p99 | 5.051 / 40.231 / 50.112 ms | 5.008 / 39.649 / 51.430 ms |
| Scheduler frame maximum | 72.958 ms | 63.031 ms |
| Complete PVS cycle p95 | 119.096 ms | 115.273 ms |
| Updates / refreshes / max batch | 384 / 4,096 / 11 | 384 / 4,096 / 11 |
| Deferred / budget exhausted | 0 / 0 | 0 / 0 |

Before staggering, the equivalent schema 3 batch p95 was 111.475 and 110.789
ms in one update. Per-update p95 is now 40.231 and 39.649 ms, a repeatable
approximately 64 percent reduction without lowering cadence, changing culling,
or changing sound policy. The complete cycle intentionally retains similar cost:
the package distributes work rather than claiming to remove it.

The remaining p95 is above one 30 Hz tick on this workstation stress profile.
Operators can lower the session cap at the cost of deferred refresh age; the
admin metrics expose due, scheduled, deferred, exhaustion, maximum batch, and
latency for that decision. P8.4 must select production values from a dedicated
server and representative player distribution rather than treating 16 as a
universal SLA.

## P8 Package Gates

- **P8.1:** complete. The repeatable multi-session, dense-entity, moving-grid,
  traversal, and structural-mutation profile passes without correctness or
  budget failure and establishes the schema 2 reference above.
- **P8.2a:** complete. Schema 3 attributes the reproducible tail to batched PVS
  CPU and the allocation pressure to full gravity rebuilds.
- **P8.2b:** complete. Fair token credit and a bounded circular cursor reduce
  per-update PVS p95 by approximately 64 percent with unchanged decisions.
- **P8.2c:** active. Harden gravity invalidation and field rebuild allocation
  using the same structural workload and connectivity assertions.
- **P8.3:** execute the explicit Z 0 regression matrix and publish the minimal
  engine/content porting contract with automated checks.
- **P8.4:** run prolonged and release-sized profiles, broad gameplay/mapping
  regression, operational diagnostics, and the final public-server checklist.

Each package closes only after its source diff, focused and broad tests,
performance evidence, generated artifacts, documentation, dependency pairing,
working tree, commit, push, and remote hash have been reviewed and recorded in
`Docs/ZLevelImplementationLedger.md`.
