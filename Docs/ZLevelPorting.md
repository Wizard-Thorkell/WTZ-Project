# WTZ Z-Level Porting Contract

## Purpose

WTZ Project depends on a small, explicit extension series in WTZ Engine. The
porting contract makes that boundary reviewable and executable so another SS14
fork can distinguish engine prerequisites from Content-owned gameplay systems.

[`ZLevelPortingManifest.json`](ZLevelPortingManifest.json) is the
machine-readable `WTZ-PORT-1` source of truth. It records:

- the official project/engine pair;
- the RobustToolbox `v275.2.0` source base;
- the ordered 20-commit WTZ Engine extension series;
- one named capability for every engine commit;
- engine API and project-consumer probes for every capability; and
- the engine and project compile targets used by the verifier.

The contract describes required behavior and API surface. It is not a promise
that the 20 commits will cherry-pick without conflicts onto every future engine
base.

## Official Pair

| Item | Revision |
| --- | --- |
| Minimum WTZ Project contract | `bd6ce6d1e20087ebe689a9b5e782bea28dae8d10` |
| WTZ Engine source base | `3136118b5338ef2d9580178caf5c723e65eb76e7` (`v275.2.0`) |
| WTZ Engine official head | `7cbd778024e49b9d3b0f4fe259631fd8a1ffe3f2` |

The engine series changes 84 files with 4,647 insertions and 124 deletions. Most
Z-level gameplay remains in WTZ Project; the engine series exposes generic map,
rendering, input, audio, serialization, physics, and file APIs.

## Capability Series

Apply the official commits in this order when using cherry-picks. A destination
fork may implement an equivalent capability differently, in which case portable
verification uses source contracts and compilation instead of commit identity.

| Order | Capability | Phase | Commit | Responsibility |
| ---: | --- | --- | --- | --- |
| 1 | `engine-foundation` | Foundation | `b138a3fa31` | Sparse tiles, coordinates, entity Z state, placement, physics, replication, and map serialization. |
| 2 | `chunk-replication` | Foundation | `f23464319b` | Lifecycle of chunks that contain only non-zero layers. |
| 3 | `pvs-render-hooks` | Foundation | `383c5428cc` | Per-session PVS exclusions and per-sprite render policy. |
| 4 | `sparse-tile-enumeration` | Foundation | `e111a6c8dd` | Allocation-safe enumeration of authored layers. |
| 5 | `tile-index-serialization` | Foundation | `40b4b723da` | Native serializer for `ZLevelTileIndices`. |
| 6 | `moving-grid-frames` | Foundation | `4e582a810c` | Local/world Z conversion for moving grids. |
| 7 | `serialized-yaml-identifiers` | P6 | `558689ba9a` | Source-to-YAML identity correlation for in-memory duplication. |
| 8 | `physics-contact-flush` | P2 | `b768b2ac33` | Deterministic pending-contact synchronization. |
| 9 | `pointer-coordinate-layer` | P2 | `ecae4d1959` | Networked pointer layer authority. |
| 10 | `world-z-rendering` | P3 | `17f6c8f8d7` | Active-world-floor camera, grid, light, occluder, and metrics behavior. |
| 11 | `reusable-tree-queries` | P3 | `dca90bdf1f` | Caller-owned component-tree query buffers. |
| 12 | `light-add-blend` | P3 | `9d63eec795` | Native projected-light blend mode. |
| 13 | `external-shadow-atlases` | P3 | `32f197aee1` | Bounded Content-owned point-light shadow atlases. |
| 14 | `imagesharp-pixel-read` | P3 | `b6051ff8c6` | Sandboxed visual-capture pixel reads. |
| 15 | `audio-post-processing` | P4 | `3794b33b6c` | Positional stream post-processing hook. |
| 16 | `audio-recipient-filtering` | P4 | `87e2732606` | Shared authoritative audio target policy. |
| 17 | `audio-source-position` | P4 | `3aaca280f6` | Content-writable apparent source position. |
| 18 | `filtered-entity-snapshots` | P6 | `f2ae5853f6` | Read-only operation-local map snapshot filtering. |
| 19 | `invalid-reference-reporting` | P6 | `a90b854ce9` | Structured load diagnostics for broken entity references. |
| 20 | `atomic-file-writes` | P6 | `7cbd778024` | Flushed temporary writes and atomic destination replacement. |

## Verification Modes

Run the official paired checkout:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\verify_zlevel_port.ps1 `
  -Mode Paired
```

`Paired` mode requires all of the following:

- the project contains the minimum contract revision;
- the engine checkout is exactly the official revision;
- all 20 commits exist in order with their recorded subjects;
- the project gitlink, engine checkout, and official revision match;
- `.gitmodules` points to WTZ Engine;
- all 50 engine/project source probes pass; and
- both contract projects build.

For a rebased, cherry-picked, or independently implemented destination fork:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\verify_zlevel_port.ps1 `
  -Mode Portable `
  -ProjectRoot C:\path\to\destination-project `
  -EngineRoot C:\path\to\destination-engine
```

`Portable` mode records hash differences as warnings. All capability probes and
both builds remain mandatory. A renamed or redesigned API can be accepted only
by versioning the manifest and updating its Content consumer contract, not by
silently weakening the current verifier.

The verifier independently protects the `WTZ-PORT-1` version, all 20 capability
IDs, the exact total of 50 probes, the two compile targets, and the rule that the
official engine revision closes the ordered series. Reducing those sets in the
manifest alone therefore fails before compatibility can be reported.

Useful development options:

```powershell
# Fast source/revision audit while iterating.
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\verify_zlevel_port.ps1 `
  -Mode Paired -SkipBuild

# Reuse an existing restore while still compiling both sides.
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\verify_zlevel_port.ps1 `
  -Mode Paired -NoRestore

# Release/rehearsal gate with clean project and engine worktrees.
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\verify_zlevel_port.ps1 `
  -Mode Portable -Configuration Release -RequireClean

# Prove that malformed contracts fail closed and rewritten history stays portable.
powershell -NoProfile -ExecutionPolicy Bypass -File .\Tools\test_zlevel_port_verifier.ps1
```

The ignored
`artifacts/zlevel-port-compatibility/zlevel-port-compatibility.json` report
records mode, manifest hash, revisions, dirty state, official-series status,
every probe, build timing/result, warnings, and actionable failures.

The verifier self-test writes an ignored
`artifacts/zlevel-port-verifier-tests/zlevel-port-verifier-tests.json` summary.
It requires rejection of a missing capability, missing probe, broken probe,
missing protected build, and engine head outside the declared series. It also
requires `Portable` mode to accept an unresolvable official project hash only
with an explicit rewritten-history warning.

## Destination Workflow

1. Identify the destination project's RobustToolbox base and create dedicated
   engine and project branches.
2. Port the 20 engine capabilities in order. Cherry-pick when histories are
   compatible; otherwise preserve each public contract while resolving upstream
   API changes locally.
3. Port WTZ Project commits or the desired Content systems without folding the
   generic engine APIs back into Content.
4. Run `verify_zlevel_port.ps1 -Mode Portable` with builds enabled.
5. Run `run_zlevel_z0_compatibility.ps1` to protect ordinary 2D behavior.
6. Run the focused Z-level, mapping, baseline, visual, and server-soak gates
   appropriate to the destination's intended feature set.
7. Record the destination engine/project revisions and any manifest-versioned
   compatibility adaptations before opening a PR.

## Failure Interpretation

- **Revision failure in `Paired`:** the official submodule pair drifted. Restore
  the recorded gitlink/checkout or intentionally version the contract.
- **Engine probe failure:** a required generic API is absent or changed shape.
  Inspect the capability's exact file, pattern, and description in the report.
- **Project probe failure:** the destination did not port the corresponding
  consumer or moved it without updating the contract.
- **Build failure:** source markers exist but their signatures or transitive
  dependencies are incompatible. The compiler is authoritative.
- **Portable hash warning:** expected for rebases and cherry-picks with rewritten
  commit IDs; it is not permission to ignore probe, build, or runtime failures.

## Limits

Source probes detect missing or renamed contracts and compilation detects type-
level incompatibility. They do not prove runtime semantics on a foreign engine.
P8.3c owns a clean-worktree rehearsal of this process. P8.4 still owns broad
gameplay, visual, mapping, soak, and public-server release evidence.
