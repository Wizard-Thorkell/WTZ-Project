# WTZ Z-Level Operations Runbook

This runbook is the operational contract for the WTZ Project and WTZ Engine
Z-level pair. It covers release evidence, deployment, monitoring, mapper
checkpoints, recovery, incident handling, and the human pilot required before an
unrestricted public-server claim.

The automated roadmap gate is `WTZ-P0-P8-1`. A strict `Passed` result means the
P0-P8 implementation and its declared envelopes are accepted on one exact
source pair. It authorizes a controlled public pilot. It does not certify an
unattended or unlimited public server.

## Readiness Classes

| Class | Meaning |
| --- | --- |
| `DevelopmentOnly` | A dirty tree, skipped build, skipped visual capture, or skipped performance profile was used. This is not deployment evidence. |
| `ControlledPublicPilot` | The strict P0-P8 gate passed, but target-host and human multiplayer evidence remain external conditions. Use a player cap, active operators, and a rollback window. |
| `PublicCandidate` | The strict gate passed and a valid revision-bound `WTZ-PILOT-1` record was supplied. Final launch approval still belongs to the server operator. |
| Unrestricted public server | Never granted automatically. Dependency risk, backups, monitoring, target hardware, moderation, and local operations must be accepted by the operator. |

## Protected Source Pair

The final manifest requires:

- branch `zlevel/server-hardening` on remote `origin`;
- local project HEAD equal to the published remote branch;
- WTZ Engine checkout and project gitlink both equal to
  `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`;
- clean project and engine worktrees before and after the gate; and
- no WTZ client or server process left behind by the test run.

Always initialize the submodule and verify the pair before building:

```powershell
git submodule update --init --recursive
git status --short
git -C RobustToolbox status --short
git rev-parse HEAD
git ls-tree HEAD -- RobustToolbox
git -C RobustToolbox rev-parse HEAD
git ls-remote --heads origin zlevel/server-hardening
```

Do not deploy a locally modified tree under a report generated for another
revision. Reports identify source, but they are not binary signatures or a
supply-chain attestation.

## Final Acceptance Gate

Validate the immutable manifest and its twelve rejection cases first:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_final_gate.ps1 -ValidateOnly
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/test_zlevel_final_gate.ps1
```

Run the strict gate from clean, published source:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_final_gate.ps1
```

Use `-NoRestore` only when dependencies have already been restored. It does not
weaken the evidence. The following switches are development aids and force a
`DevelopmentPassed` result:

- `-AllowDirtySourceForDevelopment`;
- `-SkipBuildForDevelopment`;
- `-SkipVisualCaptureForDevelopment`; and
- `-SkipPerformanceForDevelopment`.

The parent gate composes and independently validates:

- `WTZ-RELEASE-1`: full Release build, 41 exact tests, Z 0, port pairing, and a
  real server/client OpenGL capture;
- `WTZ-RECOVERY-1`: known-good checkpoint, corruption refusal, two recovery
  loads, and exact structural identity;
- `WTZ-OPS-HEALTH-1`: four exact evaluator and JSON contract tests;
- a 128-cycle Server GC map/cache lifecycle envelope;
- a 10-floor, 32-session, 1,024-iteration Server GC endurance envelope;
- a 10-floor, 64-session, 128-iteration Server GC capacity envelope; and
- 3/6/10-floor neutral baselines with warm caches and zero relevant exhaustion
  or eviction.

Every child report and required design document is hashed into
`artifacts/zlevel-final/<run-id>/zlevel-final.json`. A consumer must require:

- `schemaVersion == 1` and `contractVersion == "WTZ-P0-P8-1"`;
- `status == "Passed"` and `readiness.roadmapStatus == "Complete"`;
- exact project, engine, gitlink, branch, and remote revisions;
- clean source before and after, with all development flags false;
- two passed composite gates, four health tests, and four performance profiles;
  and
- `failure == null`.

### Accepted Roadmap Record

Strict run `20260830T193313Z-25516-dbcddb0f` passed on published WTZ Project
`7457f8239bc9b68a1913e3a0695d5ba5a4f5c771` and WTZ Engine/gitlink
`7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`, with no development bypass. It
reports release 41/41 and 3/3 composites, recovery 1/1, health 4/4, performance
4/4, Z 0 18/18, port probes 50/50, and visual checks 24/24. Parent report
SHA-256 is
`0ccaa8cd5c74f05d3c4a602c557c9cedaf8fb65afa50664abc3eca504f550940`.

This record closes the technical P0-P8 roadmap at `ControlledPublicPilot`. It
does not satisfy the target-host or human-pilot steps below and does not change
the unrestricted-server status from `NotCertified`.

## Deployment Preflight

Before opening a pilot window:

1. Archive the final parent report and its printed SHA-256 outside the checkout.
2. Review package advisories, especially the current
   `System.Security.Cryptography.Xml 9.0.0` advisories, and record whether the
   deployment accepts, patches, or blocks them.
3. Configure server logs, crash retention, monitoring, alert delivery, and
   backup retention on the target host.
4. Confirm that the autosave directory is on a local or explicitly tested
   filesystem. Same-volume rename semantics are part of checkpoint atomicity.
5. Run a checkpoint and recovery drill on that filesystem before admitting
   players.
6. Start with a conservative player cap. The measured 32-session envelope is a
   synthetic workload on the development host, not a universal capacity claim.
7. Assign an active operator for the complete pilot and reserve a rollback
   window with no unrelated map edits.

The canonical functional map is `ZLevelMappingStation`, backed by
`/Maps/Test/ZLevel/zlevel-mapping-station.yml`. It is a laboratory fixture, not
a full production station.

## Startup Smoke

For an isolated smoke round:

1. Start the server from the accepted Release build.
2. Run `forcemap ZLevelMappingStation`.
3. Start or restart the round normally.
4. Join as Passenger, late-join once, and verify observer entry.
5. Run `zlevelhealth` and save `zlevelhealth json` output in the incident/evidence
   directory.
6. Run `zlevelmetrics` before load, after traversal, and at the end of the smoke.

The first health snapshot should contain the expected configured map and no
critical integrity finding. A report can be `Degraded` after a deliberately
refused checkpoint because process-local failure telemetry remains incident
evidence until reset. Do not erase that evidence merely to obtain `Healthy`.

## Runtime Monitoring

`zlevelhealth [json]` is the operator summary. It is intentionally on-demand so
complete map validation does not enter the tick loop.

| Status | Operator response |
| --- | --- |
| `Healthy` | Continue normal observation. Retain periodic metrics at the locally selected interval. |
| `Degraded` | Preserve the report, identify the finding code, reduce load or pause structural edits, and verify the recommended action before continuing. |
| `Critical` | Stop admitting players and stop structural edits. Preserve logs and metrics, create no new checkpoint from invalid state, and move to incident recovery or shutdown. |

Watch these `zlevelmetrics` families together:

- PVS due, scheduled, deferred, exhausted, frame latency, and scheduled
  context-cache hit rate;
- direct floor-change PVS context-cache observations, which remain visible for
  accounting but are isolated batches and are not part of the scheduler reuse
  envelope;
- boundary and sky cache entries, order entries, evictions, and invalidation;
- gravity builds, reuse, build time, and owned grid workspaces;
- traversal, elevator, flight, interaction, ballistic, explosion, and sound
  refusal/exhaustion counters;
- initialized-map autosave/checkpoint attempts, successes, failures, last path,
  last error, and validated/excluded counts; and
- live map/grid ownership versus cache ownership after deletion or round change.

The tested envelopes are acceptance ceilings, not targets:

| Profile | Required envelope |
| --- | --- |
| 32-session endurance | PVS frame p95 <= 30 ms, p99 <= 33.333 ms, max <= 66.667 ms, context-cache hits >= 85%, allocation <= 24 KiB/iteration, no deferred refresh or exhaustion |
| 64-session capacity | PVS frame p95 <= 55 ms, p99 <= 66.667 ms, max <= 125 ms, context-cache hits >= 90%, allocation <= 40 KiB/iteration, no deferred refresh or exhaustion |
| Map lifecycle | p95 <= 30 ms, p99 <= 40 ms, max <= 66.667 ms, allocation <= 1 MiB/cycle, retained heap delta <= 2 MiB, exact cache state restored |
| Neutral baseline | 3/6/10 floors, <= 8 KiB measured allocation, 100% warm boundary/sky/gravity cache hits, no relevant exhaustion or eviction |

If target-host measurements breach an envelope, lower the player cap or content
density and investigate before raising a budget. Never increase limits solely to
turn an alert green.

## Mapping And Checkpoints

Use the normal initialized mapping save path for authored map work. Before a
meaningful edit batch or deployment change, create an operator checkpoint:

```text
zlevelcheckpoint <map-id> <checkpoint-name>
```

The command requires Server and Mapping permission. It works with scheduled
autosave disabled, but only for a complete initialized map root. It refuses a
grid-only root, an uninitialized map, an invalid authored Z range, unresolved
snapshot errors, or a failed write.

Checkpoints are placed beneath the configured autosave directory in the named
subdirectory. They use a timestamped `-CHECKPOINT.yml` suffix, never replace an
existing destination, and become visible only after validation, UTF-8 write,
flush, and same-directory promotion succeed.

This is mapper-state persistence. Players, minds, sessions, explicit
transients, processing queues, and live simulation caches are excluded. A
checkpoint is not a resumable-round save.

## Recovery Procedure

For invalid authored state or a failed mapping edit:

1. Stop admissions and structural edits. Record UTC time, project/engine hashes,
   map ID, health JSON, metrics, and relevant logs.
2. Do not create another checkpoint from the damaged map. A refusal is a safety
   result, not a reason to bypass validation.
3. Identify the last known-good `-CHECKPOINT.yml` and hash it before loading.
4. Keep the damaged live map isolated until evidence is preserved. Do not delete
   it merely to silence health output.
5. Load the known-good file through the normal map lifecycle in a maintenance
   environment.
6. Run map validation, `zlevelhealth json`, and the critical gameplay checks
   before swapping traffic or restarting the round.
7. Create a fresh checkpoint from the recovered map and verify a second load.
8. Record the old/new checkpoint hashes and the final decision in the incident
   log.

`Tools/run_zlevel_recovery_rehearsal.ps1` automates this sequence for a
deterministic three-floor fixture. Production recovery still requires operator
judgment, filesystem validation, and the server's normal map/round controls.

## Incident Matrix

| Signal | Immediate action | Recovery condition |
| --- | --- | --- |
| Authored map validation fails | Freeze mapping and preserve the last good checkpoint | Validated load plus fresh checkpoint and second load |
| Last checkpoint/autosave failed | Preserve error and filesystem evidence; do not overwrite the good file | Successful validated write and no critical health finding |
| PVS deferred/exhausted | Reduce admissions/content pressure and capture soak metrics | Target-host profile returns inside the accepted envelope |
| Cache ownership survives map deletion | Stop rotation and preserve lifecycle report | Exact baseline/final ownership identity under Server GC |
| Cross-floor authority anomaly | Stop the affected interaction/combat workflow and retain reproduction | Exact release/Z 0 tests plus a focused live reproduction pass |
| Visual or client attachment failure | Stop promotion on the affected renderer/driver | Real-client visual contract and local inspection pass |
| Package advisory becomes unacceptable | Stop release promotion | Patched dependency or documented operator acceptance |

## Human Pilot Contract

Automated evidence cannot judge interaction feel, stereo perception, busy-round
readability, moderation load, or target-host networking. A public candidate
therefore needs a `WTZ-PILOT-1` JSON record supplied to the final runner through
`-PilotRecordPath`.

The record must contain:

- schema 1, contract `WTZ-PILOT-1`, and status `Passed`;
- exact project, engine, and gitlink revisions;
- operator name, target host, map, at least eight concurrent human players, and
  at least 120 minutes;
- exactly the twelve manifest check IDs, each with `passed: true`; and
- enough notes to locate logs, incidents, and checkpoint evidence.

Example shape, deliberately incomplete and not valid evidence:

```json
{
  "schemaVersion": 1,
  "contractVersion": "WTZ-PILOT-1",
  "status": "Draft",
  "source": {
    "projectRevision": "<40-character hash>",
    "engineRevision": "<40-character hash>",
    "gitlinkRevision": "<40-character hash>"
  },
  "operator": { "name": "<operator>" },
  "session": {
    "targetHost": "<host description>",
    "map": "<representative station>",
    "concurrentHumanPlayers": 0,
    "durationMinutes": 0
  },
  "checks": [
    { "id": "round-start-map-load", "passed": false, "notes": "<evidence>" }
  ]
}
```

The required check IDs cover round start, join/late-join/observer, grouped
ladders, powered elevators, gravity/fall/flight, construction, atmosphere,
combat, lighting/FOV/weather, vertical sound, mapping save/reload, and
health/checkpoint/recovery. The final gate rejects missing, extra, duplicated,
wrong-revision, undersized, or failed pilot records.

## Rollback And Evidence Retention

Rollback means restoring the previously accepted project/engine pair and its
known-good authored map, not mixing one old half with one new half. Retain:

- project, engine, gitlink, branch, and remote hashes;
- the final report, child reports, TRX files, screenshots, baseline, soak, and
  lifecycle JSON;
- health and metrics snapshots around the incident;
- checkpoint files and SHA-256 values;
- server/client logs and crash output; and
- pilot sign-off and operator decision.

Generated `artifacts/` are ignored by Git. Copy accepted evidence to durable
storage before cleaning a checkout or rotating a host.

## Known Limits

- The official fixture and exact matrices are representative, not exhaustive of
  every upstream system or community map.
- The Server GC envelopes are synthetic and host-specific; real network,
  database, moderation, and content load can lower capacity.
- The real-client visual gate covers the current host, renderer, and driver, not
  every GPU vendor or display configuration.
- Mapper checkpoints do not restore a live round.
- A host crash can leave a dot-prefixed temporary file; automatic stale-temp
  scavenging and directory-metadata fsync are not part of the current contract.
- Network or unusual filesystems may provide weaker rename durability than a
  tested local filesystem.
- Cross-grid vertical behavior is explicit; overlapping XY geometry does not
  imply a vertical connection.
- Existing dependency advisories remain operator-visible risk and are not waived
  by a passing Z-level gate.

These limits do not invalidate the P0-P8 implementation. They define the
boundary between a completed technical roadmap, a controlled public pilot, and
an unrestricted production service.
