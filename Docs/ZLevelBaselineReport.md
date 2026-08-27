# WTZ Z-Level Baseline And Budget Report

This report closes roadmap package P0.3. It records the first configurable
performance policy for native Z-level systems and compares it with the P0.2
baseline captured immediately before the change.

## Effective Budgets

| CVar | Default | Effective clamp | Scope | Exhaustion behavior |
| --- | ---: | ---: | --- | --- |
| `zlevel.boundary_cache_capacity` | 8,192 entries | 256 to 131,072 | Server, replicated to clients | Evict the oldest resolved entries and recompute on the next query. Boundary results remain correct. |
| `zlevel.visibility_max_level_distance` | 4 world-Z levels | 0 to 32 | Server, replicated to clients | Reject visibility outside the configured distance. This is deterministic quality degradation, not partial traversal. |
| `zlevel.pvs_visibility_check_budget` | 16,384 checks per session refresh | 0 to 1,000,000 | Server | Clear the session's complete Z-level culling snapshot for that refresh. Normal engine PVS remains active, so entities are not incorrectly hidden. |

`zlevelmetrics` reports the effective cache capacity and visibility distance,
plus PVS visibility checks, budget exhaustions, and fail-open candidates. The
client debug overlay reports its effective replicated cache and visibility
settings.

The boundary-cache default increased from 4,096 to 8,192 entries. This is the
smallest power-of-two capacity above the deterministic 10-floor workload's
5,760 unique boundary samples.

## Method

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_baseline.ps1 -NoBuild
```

Both captures used the generated 3-, 6-, and 10-floor fixtures in a connected
Debug integration server on Windows 10.0.19045 x64, .NET 10.0.4, with 28 logical
processors. Each case performs one cold warm-up and three measured iterations.
Timing values are local comparison evidence, not portable pass/fail thresholds.

P0.3 snapshots use schema version 2 and include the effective budget values.

## Comparison

| Floors | Tiles | P0.2 measured ms | P0.3 measured ms | Delta | P0.2 hot hit rate | P0.3 hot hit rate | P0.2 warm evictions | P0.3 warm evictions |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 3 | 1,265 | 7.006 | 7.469 | +6.6% | 100% | 100% | 0 | 0 |
| 6 | 2,527 | 12.985 | 12.654 | -2.5% | 100% | 100% | 0 | 0 |
| 10 | 4,209 | 52.011 | 20.351 | -60.9% | 50.1% | 100% | 2,199 | 0 |

Measured managed allocations remained 6,336 bytes in every case before and
after P0.3. The 10-floor improvement coincides with eliminating deterministic
cache churn. The smaller timing movements are treated as normal Debug-run noise.

The measured PVS workloads performed 276, 384, and 528 Z visibility checks for
3, 6, and 10 floors respectively. None approached the 16,384-check default and
no benchmark refresh failed open.

## Deliberate Deferrals

- Gravity cache construction still solves a complete grid synchronously on a
  cold query. A tile cap would make connected regions incorrectly weightless;
  safe budgeting requires an incremental solver and a previous-cache or
  double-buffer policy.
- The PVS budget bounds Z-level visibility evaluation, but the engine spatial
  lookup still enumerates the candidate set. Bounding that phase requires an
  engine-level paged or resumable query API.
- The 100 ms Z-level PVS refresh interval remains a scheduling contract rather
  than a configurable budget. It should be revisited with concurrent-player
  server traces in P8.
- Structural collapse already processes a queue at eight collapses per tick.
  Its tuning belongs with structural content hardening rather than this shared
  visibility/cache package.
- Per-entity step-down depth is movement semantics and remains component data,
  not a server-wide performance control.

## Operational Guidance

- Raise the boundary capacity only after observing sustained eviction churn;
  each process maintains its own cache.
- Lower vertical visibility distance only as an explicit gameplay and rendering
  tradeoff, because server PVS and clients receive the same value.
- Treat any PVS budget exhaustion as a capacity signal. Fail-open behavior
  preserves correctness but increases network traffic for that refresh.
- Compare baseline JSON only when runtime, build configuration, fixture schema,
  and effective budgets match.
