# WTZ Z 0 Compatibility Contract

## Scope

WTZ preserves ordinary Space Station 14 maps as planar Z 0 maps. A map does not
become a native multi-floor map until it is explicitly configured with a
`ZLevelMapComponent`. Component-free entities on those maps continue to use the
engine's native two-dimensional representation.

This is a behavioral compatibility contract, not an automatic migration layer.
Old stations can keep running as Z 0 maps; maps authored for WTZ must opt into and
validate their own vertical structure.

## Executable Matrix

[`ZLevelZZeroCompatibility.json`](ZLevelZZeroCompatibility.json) is the
machine-readable source of truth. Every entry binds one compatibility promise to
one exact integration test in either WTZ Project or WTZ Engine. The runner owns a
separate mandatory domain list, so removing a domain from the manifest cannot make
the gate pass with less coverage.

| Domain | Protected behavior |
| --- | --- |
| Core | Unconfigured maps stay passive and component-free. |
| Entity position | Returning to world Z 0 removes explicit vertical state. |
| Engine map | Legacy tile APIs continue to address the base layer. |
| Mapping | Tile and entity placement retain authoritative layer selection. |
| Serialization | Pre-Z-level data defaults safely to Z 0. |
| Construction | Native RCD construction/deconstruction remains functional. |
| Atmosphere | Coordinate operations remain isolated to their layer. |
| Interaction | Networked same-floor targeting retains its layer. |
| Combat | Hitscan, guns, and throws preserve native Z 0 targeting. |
| Navigation | Native planar path requests remain available. |
| Sound | Same-floor routing remains the compatibility fast path. |
| Visibility | Planar same-floor visibility does not require vertical state. |
| Weather | Legacy exposure policy remains planar on Z 0. |
| Rendering | Legacy roof presentation remains planar on Z 0. |
| Gravity | WTZ does not invent a downward pull without a gravity source. |

## Running The Gate

From the project root:

```powershell
.\Tools\run_zlevel_z0_compatibility.ps1 -Configuration Debug
```

After the relevant projects have already been built:

```powershell
.\Tools\run_zlevel_z0_compatibility.ps1 -Configuration Debug -NoBuild
```

The runner validates the schema, protected domains, repository ownership, project
paths, unique IDs, and exact fully-qualified test names. It then executes tests in
project groups, parses each TRX file, and fails if a declared test was not
discovered, ran more than once, did not pass, or if the filter selected an
undeclared test.

The ignored report at
`artifacts/zlevel-z0-compatibility/zlevel-z0-compatibility.json` records the
manifest hash, project and engine revisions, test totals, and individual outcomes.
