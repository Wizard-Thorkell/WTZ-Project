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
output, and checks that the report contains the requested profile. Add
`-RequireReleaseEnvelope` to require the exact P8.4a 10-floor, 32-session,
8-copy Release profile with at least 8 warm-up and 128 measured iterations.
The gate also verifies the build mode recorded by the testhost, so `-NoBuild`
cannot accidentally present a stale Debug binary as Release evidence.

## Report Contract

The runner writes `artifacts/zlevel-server-soak/zlevel-server-soak.json`. Schema
version 6 records:

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
  maxima, per-scheduler-frame latency, and batch-local PVS visibility-context
  cache hits, misses, latest-batch occupancy, and high-water occupancy;
- shared boundary, sky, visibility, gravity, PVS, and trace metrics, including
  gravity builds that reused an existing per-grid workspace;
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

## P8.2 Gravity Workspace Evidence

`SharedZLevelGravitySystem` retains one workspace per managed grid instead of
allocating a new live-tile set, source array, BFS dictionary, queue, column
dictionary, and column lists for every invalidation. Ordinary tile events update
the live inventory only when emptiness changes. A solid-to-solid tile replacement
therefore keeps the current field, while removal or placement dirties it and
reuses its buffers on demand. Source-only changes preserve the live inventory.

The public `InvalidateGrid` batch-edit API remains conservative: it retains the
owned buffers but marks the tile snapshot stale so the next query re-enumerates
the grid. Grid removal drops the workspace and its high-water capacity. The
admin command and client debug overlay report gravity `count/reused`; after
warm-up, an ordinary structural workload should normally reuse every build.

Schema 5 runs the same profile and all the same production consumers as schema
4. It appends `gravityReusedBuilds` to the shared metrics snapshot.

| Measurement | Workspace run 1 | Workspace run 2 |
| --- | ---: | ---: |
| Total measured time | 6,858.106 ms | 7,419.711 ms |
| Main-thread allocation | 2,068,744 B | 2,064,688 B |
| Gravity builds / reused builds | 256 / 256 | 256 / 256 |
| Gravity build time | 389.790 ms | 406.336 ms |
| Gravity cache hits / misses | 54,144 / 256 | 54,144 / 256 |
| Workload iterations with a GC collection | 0 / 128 | 0 / 128 |

The preceding schema 4 runs allocated 170,281,832 and 170,266,272 bytes. The
workspace therefore removes approximately 98.8 percent of total measured
allocation. The two gravity-consumer stages specifically fall from 168,249,344
bytes to 26,624 bytes, above 99.98 percent, while all 256 field decisions and
every PVS, sound, traversal, cache, and budget counter remain structurally
equivalent. Gravity build CPU also falls from approximately 627-632 ms to
390-406 ms on the comparison host.

This is a high-water cache, not zero retained memory. A live grid keeps the
capacity needed by its largest observed topology until grid teardown. Operators
should compare reuse, cached-grid count, retained heap after collection, and
grid lifecycle together. P8.4b adds the repeated-map and Server GC lifecycle
evidence below; representative station behavior remains part of the release
validation rather than an inference from this synthetic topology workload.

## P8.3a Executable Z 0 Contract

WTZ preserves ordinary Space Station 14 maps as component-free planar Z 0 maps.
Opting into native multi-floor behavior remains explicit. This protects existing
2D content without adding an automatic station-migration layer or silently
inventing vertical structure.

`Docs/ZLevelZZeroCompatibility.json` is the versioned `WTZ-Z0-1` source of truth.
It binds 18 compatibility promises across 15 mandatory domains to exact tests:
17 in WTZ Project and one engine-level legacy tile API test in WTZ Engine. The
separate runner-owned domain list prevents a manifest edit from silently
dropping an entire protected area.

`Tools/run_zlevel_z0_compatibility.ps1` validates schema, IDs, domain coverage,
repository ownership, project paths, and unique fully-qualified tests. It runs
each project as one exact filter, parses the resulting TRX files, and rejects
missing, duplicate, failed, skipped, or unexpectedly selected tests. Its ignored
JSON report records the manifest hash, paired project/engine revisions, counts,
and each outcome.

The package gate passes all 18 declared contracts, three new foundational
component-free/passive-state tests, 340 of 342 broad Z-level cases with the two
fixture-conditioned cases passing in isolation, 22 unit/mapping cases, and all
three 3/6/10-floor baselines. The baseline remains at 6,336 allocated bytes for
every floor depth. A non-incremental single-worker solution build completes in
3m01s with zero errors and 704 established warnings.

## P8.3b Executable Port Contract

WTZ Project now publishes `Docs/ZLevelPortingManifest.json` as the versioned
`WTZ-PORT-1` engine/content boundary. It records the RobustToolbox `v275.2.0`
base, the ordered 20-commit WTZ Engine extension series, one capability per
commit, 28 engine probes, 22 project-consumer probes, and the two compile
targets that jointly cover the contract.

`Tools/verify_zlevel_port.ps1` has two explicit policies. `Paired` proves the
official project minimum, exact engine head, commit order and subjects,
submodule gitlink and URL, all probes, and both builds. `Portable` permits
rewritten history while keeping probes and builds authoritative. Both modes
write an ignored revision/hash/build report; `-RequireClean` is reserved for
release and rehearsal gates.

The verifier protects its capability, probe, build, head, and contract-version
sets independently from the JSON. `Tools/test_zlevel_port_verifier.ps1` proves
that five malformed contracts fail closed and that an unresolvable official
project hash is accepted by `Portable` only with an explicit warning. The final
package gate passes 6/6 verifier self-tests, 50/50 probes, 2/2 contract builds,
18/18 Z 0 contracts, 342/342 broad integration cases, 22/22 unit/mapping cases,
and two 3/6/10-floor baseline runs. The final baseline allocates 6,336 bytes at
every depth, and the full non-incremental single-worker solution build finishes
in 2m30.59s with zero errors and 688 established warnings.

## P8.3c Clean Port Rehearsal

`Tools/run_zlevel_port_rehearsal.ps1` closes the porting phase from disposable
project and engine checkouts instead of relying on the development tree. The
official clean run `20260830T112201Z-25440-aebc6af8` starts at project revision
`26c9c9f21c1155c47f8e7257dd9dc4eecb06b8f9` and engine revision
`7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`, with builds and clean-source
enforcement enabled.

The exact paired-history scenario passes 50/50 probes and 2/2 Release builds
without warnings. A second depth-one scenario creates distinct paired heads,
proves that the official project minimum and engine base are unavailable, then
passes the same 50/50 probes and 2/2 Release builds with exactly the two expected
portable-history warnings. Both scenario worktrees remain clean; source status
and revisions remain unchanged; the ownership-marked temporary root is removed.
The complete rehearsal takes 421,567.688 ms.

The consolidated phase gate also passes 6/6 verifier self-tests, 18/18 Z 0
contracts, all 342 broad integration cases with one fixture-conditioned case
confirmed in isolation, 22/22 unit/mapping cases, and the 3/6/10-floor baseline
at 6,336 allocated bytes per depth. The non-incremental single-worker Debug
solution build completes in 2m39.24s with zero errors and 704 established
warnings. P8.3 is complete; these source and compile proofs intentionally do not
replace P8.4's representative runtime and public-server evidence.

## P8.4a PVS Release Envelope

PVS candidate lookup returns the same entity to many viewers in one scheduler
update. `ZLevelPvsSystem` now resolves the candidate's map, grid, tile, local Z,
and world Z once per scheduler batch and reuses that geometry for later
sessions. The final visibility decision is deliberately not cached: every
viewer still evaluates range, boundary state, render dependencies, and normal
metrics. Scheduled batches clear the context table before collecting sessions,
and direct `RefreshSession` calls clear it before every call, so simulation
updates can never observe stale candidate geometry.

An earlier final-decision cache was measured and rejected. It reached only a
2.08 percent hit rate and left scheduler-frame p95 at 38.768 ms because viewer
positions and levels made the final key too specific. The geometry cache instead
reaches exactly 90.625 percent on the 32-session fixture.

| Measurement | P8.2c run 1 | P8.2c run 2 | Context run 1 | Context run 2 | Envelope run |
| --- | ---: | ---: | ---: | ---: | ---: |
| Scheduler-frame p95 | 39.293 ms | 39.174 ms | 23.461 ms | 24.160 ms | 24.716 ms |
| Scheduler-frame p99 | 43.208 ms | 44.408 ms | 28.116 ms | 29.304 ms | 31.217 ms |
| Total measured time | 6,858.106 ms | 7,419.711 ms | 6,353.612 ms | 6,510.473 ms | 5,901.571 ms |
| Main-thread allocation | 2,068,744 B | 2,064,688 B | 2,180,056 B | 2,126,128 B | 2,132,808 B |
| Context-cache hit rate | n/a | n/a | 90.625% | 90.625% | 90.625% |

All schema 6 runs preserve exactly 4,218,880 candidate checks and 4,096 fair
session refreshes, with 3,823,360 context hits, 395,520 misses, 1,030 maximum
entries, no deferred refresh, no budget exhaustion, no workload collection,
and no retained-heap growth. The repeated p95 reduction is approximately 38 to
40 percent against the paired P8.2c captures. Allocation increases by at most
approximately 5.4 percent, remains below 17.1 KiB per measured iteration, and
stays far below the 24 KiB envelope.

`-RequireReleaseEnvelope` fails unless scheduler-frame p95 is at most 30 ms,
p99 at most 33.333 ms, maximum at most 66.667 ms, context-cache hit rate at
least 85 percent, and allocation at most 24 KiB per measured iteration. It also
requires zero deferred refreshes and zero scheduler budget exhaustions. These are
host-specific acceptance limits for the declared deterministic profile, not a
portable promise for arbitrary hardware or station content. P8.4b owns true
Server GC, longer endurance, repeated grid lifecycle, and retained-memory
evidence.

## P8.4b Server GC Lifecycle And Capacity Envelopes

The lifecycle harness creates a fresh native three-floor map, fills a 6 by 6
column on every floor, initializes it, and warms boundary, sky, gravity, sound,
and traversal caches. It then deletes the map and requires 17 ownership
counters to return exactly to the state captured before the cycle. A second
test keeps one warmed map alive while removing another, proving that order-cache
compaction preserves the surviving map rather than relying on a global clear.

The audit found two teardown-specific retention paths. Boundary cache entries
were removed when a grid terminated, but their FIFO tokens remained until a
later capacity compaction. Sound portal grid/map invalidation had the same
behavior whenever another grid still owned chunks. Both systems now compact
their order queues after bulk removal. Boundary teardown and configuration
changes reuse scratch buffers instead of allocating temporary lists, and the
system has explicit shutdown cleanup. Admin metrics expose cache entries and
order tokens together, plus boundary registrations/providers and sky
columns/order entries, so operators can distinguish live high-water state from
orphaned ownership.

`Tools/run_zlevel_server_lifecycle.ps1` always launches the testhost with
`DOTNET_gcServer=1`, requires the report itself to confirm Server GC, compares
the complete baseline and final state, and requires a full collection. Its
Release envelope requires at least 8 warm-up and 128 measured cycles, cycle
p95 <= 30 ms, p99 <= 40 ms, maximum <= 66.667 ms, allocation <= 1 MiB per
cycle, and retained heap delta <= 2 MiB.

| Lifecycle measurement | Calibration | Envelope confirmation |
| --- | ---: | ---: |
| Cycle p50 / p95 / p99 | 14.897 / 17.385 / 22.512 ms | 15.212 / 20.168 / 22.802 ms |
| Cycle maximum | 24.036 ms | 22.849 ms |
| Allocation per cycle | 865,684 B | 865,682 B |
| Retained heap delta | 265,144 B | 265,360 B |
| Gen0 / Gen1 / Gen2 collections | 2 / 2 / 2 | 2 / 2 / 2 |

`Tools/run_zlevel_server_soak.ps1 -RequireServerGC` independently verifies the
GC mode for ordinary soaks. `-RequireCapacityEnvelope` defines a separate
64-session Release profile because doubling viewers is not expected to satisfy
the 32-session one-frame envelope. It requires 10 floors, 64 sessions, 8
warm-up iterations, at least 128 measured iterations, 8 candidate copies,
p95 <= 55 ms, p99 <= 66.667 ms, maximum <= 125 ms, context-cache hits >= 90
percent, allocation <= 40 KiB per iteration, and no scheduler debt or budget
exhaustion.

| Server GC profile | 32 x 128 | 32 x 1,024 | 64 x 128 capacity gate |
| --- | ---: | ---: | ---: |
| Scheduler-frame p95 | 23.315 ms | 19.892 ms | 44.535 ms |
| Scheduler-frame p99 | 26.437 ms | 22.907 ms | 53.109 ms |
| Scheduler-frame maximum | 42.555 ms | 59.288 ms | 79.219 ms |
| Context-cache hit rate | 90.63% | 88.91% | 95.31% |
| Allocation per iteration | 16,897 B | 15,164 B | 28,315 B |
| Deferred / exhausted refreshes | 0 / 0 | 0 / 0 | 0 / 0 |

The 1,024-iteration run evaluates 28,525,291 PVS candidates without collection
inside the measured window because total measured allocation is only 15.5 MB.
That absence is not used as a retention claim: the lifecycle envelope measures
process-wide allocation and forces compacting full collections around repeated
map ownership. Live gravity workspaces and other bounded caches intentionally
retain high-water capacity until their owning grid is removed. These limits are
regression thresholds for the declared host and synthetic fixture, not a
portable player-count promise for arbitrary maps or hardware.

## P8.4c Executable Release Matrix

`Docs/ZLevelReleaseManifest.json` and `Tools/run_zlevel_release_gate.ps1`
define `WTZ-RELEASE-1`. The fail-closed runner requires clean paired WTZ Project
and WTZ Engine worktrees, exact gitlink identity, a full `SpaceStation14.slnx`
Release build, and 41 exact tests across 19 gameplay, mapping, persistence, and
presentation domains. It parses exact TRX results and rejects omitted,
duplicate, failed, or undeclared tests.

The matrix composes `WTZ-Z0-1` at 18/18 tests, `WTZ-PORT-1` at 50/50 probes,
and `WTZ-VISUAL-1` at exactly 15 captures and 24/24 checks. Child report
identity, revisions, counts, and SHA-256 hashes are part of the parent record.
Development-only dirty/build/visual switches can report only
`DevelopmentPassed`; they cannot satisfy the strict contract.

Release integration also refreshes an attached in-game viewer immediately when
its world Z changes and keeps transform ancestors of visible PVS candidates in
transport. This is required for an upper-floor player, light, and occluder to
replicate as an attached hierarchy immediately after traversal. Metrics count
only evaluated candidates so transport parents do not corrupt visibility
conservation.

The full development matrix passes 41/41 exact tests and all three composites.
Broader evidence passes 343 cases with two explicit conditional skips and zero
failures, 22/22 unit/mapping cases, the 3/6/10-floor baseline, and a full Debug
solution build with zero errors. Strict clean run
`20260830T161628Z-2640-16179cd2` then passes on published project revision
`63d1b7ac91ca...`: full Release build, 41/41 tests, all three composites, clean
source, no development bypasses, and verified child hashes. Commands, report
schema, exact hashes, evidence, and residual risks are in
`Docs/ZLevelRelease.md`.

## P8 Package Gates

- **P8.1:** complete. The repeatable multi-session, dense-entity, moving-grid,
  traversal, and structural-mutation profile passes without correctness or
  budget failure and establishes the schema 2 reference above.
- **P8.2a:** complete. Schema 3 attributes the reproducible tail to batched PVS
  CPU and the allocation pressure to full gravity rebuilds.
- **P8.2b:** complete. Fair token credit and a bounded circular cursor reduce
  per-update PVS p95 by approximately 64 percent with unchanged decisions.
- **P8.2c:** complete. Per-grid reusable topology workspaces reduce measured
  allocation by approximately 98.8 percent with exact connectivity unchanged.
- **P8.3a:** complete. The `WTZ-Z0-1` matrix protects 15 required domains with
  18 exact project/engine tests and a fail-closed TRX-verifying runner.
- **P8.3b:** complete. `WTZ-PORT-1` protects the ordered 20-capability engine
  series with 50 source/consumer probes, two builds, and fail-closed self-tests.
- **P8.3c:** complete. Both exact-history and rewritten shallow-history pairs
  pass from disposable clean trees with four protected Release builds.
- **P8.4a:** complete. Batch-local candidate geometry reuse brings the
  32-session scheduler-frame p95 under the executable 30 ms Release envelope
  while final boundary decisions remain per viewer and authoritative.
- **P8.4b:** complete. True
  Server GC lifecycle, 32-session endurance, and 64-session capacity envelopes
  pass with exact cache ownership restored after every map cycle.
- **P8.4c:** complete. The executable `WTZ-RELEASE-1` contract passes its strict
  clean gate at 41/41 exact tests, 3/3 composites, and zero development
  bypasses on the published source pair.
- **P8.4d:** active. It owns operational diagnostics, recovery guidance,
  representative deployment checks, and the final P0-P8 public-server
  checklist.

Each package closes only after its source diff, focused and broad tests,
performance evidence, generated artifacts, documentation, dependency pairing,
working tree, commit, push, and remote hash have been reviewed and recorded in
`Docs/ZLevelImplementationLedger.md`.
