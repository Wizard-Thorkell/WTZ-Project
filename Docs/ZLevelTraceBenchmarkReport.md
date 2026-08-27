# WTZ ZLevelTrace Benchmark Report

This report records the first reproducible allocation and timing baseline for
the shared `ZLevelTrace` primitive at the end of roadmap phase P1.

## Method

The integration benchmark creates one four-level grid and measures four
tile-only workloads:

- a 16-tile same-level trace;
- a 34-tile diagonal trace across three vertical boundaries;
- a trace stopped at its first closed boundary;
- an 81-tile request stopped by a 64-visit budget before output is committed.

Each mode receives 16 warm-up calls followed by 512 measured calls. The
immutable mode creates an independent snapshot per call. The buffered mode
reuses one pre-sized `ZLevelTraceBuffer`. Managed allocations are measured with
`GC.GetAllocatedBytesForCurrentThread`; elapsed time uses
`Stopwatch.GetTimestamp`.

Command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File Tools/run_zlevel_trace_benchmark.ps1 -NoBuild
```

Set `WTZ_ZLEVEL_TRACE_BENCHMARK_DIR` to choose the JSON output directory. The
test otherwise writes `zlevel-trace-benchmark.json` beneath its work directory
and attaches it to the NUnit result.

## First Capture

Captured on Windows 10.0.19045 x64, .NET 10.0.4, Debug build, 28 logical
processors, workstation GC. Timing is local evidence and never a pass/fail
threshold.

| Workload | Result | Immutable total ms | Immutable bytes/query | Buffered total ms | Buffered bytes/query |
| --- | --- | ---: | ---: | ---: | ---: |
| Same level | Completed | 3.233 | 2,344 | 2.215 | 0 |
| Diagonal multi-floor | Completed | 7.526 | 6,896 | 6.012 | 0 |
| Closed boundary | ClosedBoundary | 2.835 | 1,480 | 2.577 | 0 |
| Tile-budget exhaustion | IterationBudgetExceeded | 5.157 | 4,416 | 4.411 | 0 |

The warmed buffered tile-only path allocated zero managed bytes in every
workload. The test enforces this invariant and requires the immutable path to
allocate more, but deliberately does not assert relative or absolute timing.

## Interpretation

- Caller-owned output removes all WTZ-managed per-query allocation for the
  measured geometry and boundary paths after capacity and caches are warm.
- Immutable allocation scales with copied output. It remains appropriate for
  cold callers that need independent result lifetime.
- `TraceMilliseconds` measures the shared geometric core and excludes the
  immutable array snapshot created by the convenience overload. The benchmark's
  outer elapsed time and allocation count include the complete public call.
- Closed and budgeted traces preserve atomic output while remaining
  allocation-free in buffered mode.

## Limits

- Entity-hit workloads are excluded from the zero-allocation contract because
  the Robust physics ray can allocate internally. P2 consumers should reuse the
  WTZ buffer but must profile their collision masks separately.
- Results from different runtimes, build modes, CPUs, or effective budgets are
  not directly comparable.
- The benchmark measures a single main-thread process. Concurrent-player and
  long-round behavior belongs to P8 hardening.
