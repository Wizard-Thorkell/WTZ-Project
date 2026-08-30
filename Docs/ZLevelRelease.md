# WTZ Z-Level Release Contract

`WTZ-RELEASE-1` is the executable acceptance contract for the WTZ Project and
WTZ Engine Z-level pair. It binds one clean source identity to a full Release
build, an exact gameplay/mapping/persistence test matrix, and the existing Z 0,
port-pairing, and real-client visual contracts.

The contract is deliberately narrower than "all tests passed" or "ready for
every public server." It provides deterministic evidence for the declared
domains and revisions. P8.4d still owns operational diagnostics, recovery
procedures, representative deployment checks, and the final P0-P8 decision.

## Protected Source Identity

`Docs/ZLevelReleaseManifest.json` schema 1 declares:

- contract version `WTZ-RELEASE-1`;
- configuration `Release`;
- minimum WTZ Project revision
  `0139161a2ecbdbf6a2fd59b9959b57d401d3a54b`;
- exact WTZ Engine revision
  `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2`;
- the `RobustToolbox` gitlink as the engine pairing authority;
- clean WTZ Project and WTZ Engine worktrees; and
- the exact full build target `SpaceStation14.slnx`.

The runner rejects a project revision that does not descend from the declared
minimum, a different engine checkout, a mismatched gitlink, dirty source, an
unprotected project, or a manifest that weakens its hard-coded contract. Exact
engine identity is intentional: generic engine APIs and their Content
consumers must be released as a tested pair.

## Release Matrix

The manifest declares 41 exact fully-qualified tests in 19 required domains:

| Domain | Tests | Protected behavior |
| --- | ---: | --- |
| Atmosphere | 2 | Vertical gas flow and pipe isolation |
| Combat | 3 | Hitscan, physical projectile, and explosion boundaries |
| Construction | 1 | Z-aware structural deconstruction |
| Elevators | 2 | Powered traversal and persistence |
| Flight | 2 | Movement/collision and AI execution |
| Interaction | 2 | Direct/remote authority and floor isolation |
| Lighting | 2 | Vertical projection and closed-floor separation |
| Mapping lifecycle | 2 | Initialized floor creation and deletion |
| Mapping placement | 2 | Floor-targeted tile/entity placement |
| Mapping protocol | 2 | Correlated validation and client/server result handling |
| Movement and gravity | 3 | Falling, gravity convergence, and PVS floor refresh |
| Navigation and AI | 2 | Hierarchical routing and transition execution |
| Persistence autosave | 3 | Validation, atomic replacement, and failure retention |
| Persistence round trip | 3 | Double load/save structure and references |
| Persistence snapshot | 1 | Transient-state filtering |
| Sound | 2 | Portal propagation and sealed-floor isolation |
| Traversal | 2 | Authored transitions and grouped ladder behavior |
| Visibility and rendering | 3 | PVS isolation, hierarchy transport, and dependencies |
| Weather | 2 | Sky exposure and bounded presentation |

The runner invokes each declared test exactly and parses TRX output. It rejects
missing, duplicate, failed, or undeclared results, so a broad substring filter
cannot silently substitute a different test set.

Three versioned composite gates are also mandatory:

- `WTZ-Z0-1`: 18/18 exact Z 0 compatibility contracts;
- `WTZ-PORT-1`: 50/50 paired engine/project capability probes; and
- `WTZ-VISUAL-1`: 15 real-client captures with 24/24 image checks.

The parent performs the full Release solution build. The paired port child may
therefore skip its duplicate builds, but its exact source revisions, mode,
cleanliness, official-series proof, probes, report identity, and report hash
remain mandatory.

## Deterministic Visual Fixture

The visual child starts the server with the Z-level test map, Sandbox preset,
automatic round start, and `admin.deadmin_on_join=false`. The client waits for
the observer session to settle and requests `readmin` if necessary before
configuring the capture fixture. Sandbox avoids unrelated randomized round-start
mutations that can change wall and shadow pixels between runs.

The gate expects exactly 15 declared captures and 24 passing checks. Extra or
missing files fail the contract. This is a real OpenGL client check on the
current host, not proof that every GPU, renderer, map, or driver has identical
output.

## Runtime Corrections Covered

The release work exposed two PVS integration requirements that ordinary
same-floor tests did not cover:

- an attached in-game viewer now refreshes server PVS immediately when its
  world Z changes, while transient actor components without a session remain
  valid during mapping and snapshot setup; and
- visible candidates retain their transform ancestors, including grid and map
  ownership, because engine PVS culls an excluded parent subtree before a
  visible child can replicate.

The associated integration test begins with real PVS enabled, proves an upper
light and occluder are absent from a lower viewer, moves the attached player to
the upper floor, and verifies the player, render dependencies, metrics, and
attachment hierarchy refresh immediately. PVS metrics continue to count only
evaluated candidates; transport ancestors do not inflate the conservation
identity `visible + culled == candidates`.

## Commands

Validate the runner against its fail-closed mutation suite:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/test_zlevel_release_gate.ps1
```

Validate the canonical manifest without building or running tests:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_release_gate.ps1 -ValidateOnly
```

Run the strict release gate from clean, paired worktrees:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_release_gate.ps1 -NoRestore
```

Omit `-NoRestore` when dependencies must be restored. `-NoRestore` changes only
dependency acquisition and does not weaken release evidence.

The following switches are development tools and can never produce release
evidence:

- `-AllowDirtySourceForDevelopment`;
- `-SkipBuildForDevelopment`; and
- `-SkipVisualCaptureForDevelopment`.

A successful run using any of them reports `DevelopmentPassed`, never `Passed`.
The strict release record must report `Passed`, clean project and engine trees,
all 41 tests, all three composite gates, and no development bypasses.

## Reports And Failure Semantics

Every execution creates an owned directory under
`artifacts/zlevel-release/<run-id>/` and writes `zlevel-release.json`, including
the manifest SHA-256, source/gitlink revisions, dirty flags, development flags,
build records, exact test outcomes, composite report paths and hashes, duration,
summary counts, and any failure message. Child artifacts live beneath the same
run directory.

The runner writes a machine-readable report even when a build, test, child
gate, or source check fails. A strict consumer should require all of the
following rather than relying only on the process exit code:

- `schemaVersion == 1` and `contractVersion == "WTZ-RELEASE-1"`;
- `status == "Passed"` and `configuration == "Release"`;
- exact project, engine, and gitlink revisions;
- both dirty flags and all development flags equal to `false`;
- `executedTests == passedTests == declaredTests == 41`; and
- `passedCompositeGates == requiredCompositeGates == 3`.

## P8.4c Development Evidence

Development run `20260830T154257Z-48560-a8427ecd` exercised the complete matrix
against the uncommitted package source and reported `DevelopmentPassed`: 38/38
integration tests, 3/3 unit tests, 18/18 Z 0 contracts, 50/50 port probes, and
15 visual captures with 24/24 checks. A separate clean visual run also passed
15/15 captures and 24/24 checks.

The broader Release `FullyQualifiedName~ZLevel` regression run passed 343 cases,
conditionally skipped two fixture-dependent long workloads, and failed zero of
345 total cases. The initialized-map/unit filter passed 22/22, the 3/6/10-floor
baseline passed 3/3, and a non-incremental single-worker Debug solution build
completed with zero errors and 688 established warnings. The strict clean
`WTZ-RELEASE-1` run remains the package completion authority.

## Limits And Residual Risk

- The matrix is exact and representative, not exhaustive coverage of every
  upstream Content system or map.
- Visual evidence is host-specific and the gameplay matrix uses deterministic
  integration fixtures rather than a populated production station.
- The broad suite's two conditionally skipped long workloads require separate
  operational coverage where their environment is available.
- Existing package advisories include `System.Security.Cryptography.Xml 9.0.0`
  and legacy Pow3r runtime dependencies. They are upstream dependency risk and
  are not waived by this contract.
- Passing P8.4c does not close P8. P8.4d must still prove diagnostics, recovery,
  deployment procedures, representative operation, and the final roadmap gate.
