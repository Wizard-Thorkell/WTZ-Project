// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Map;

namespace Content.Shared.ZLevel;

/// <summary>
/// Describes why a vertical column query did or did not reach open sky.
/// Consumers fail closed unless the result is explicitly <see cref="Exposed"/>.
/// </summary>
public enum ZLevelSkyExposureTermination : byte
{
    Exposed,
    ClosedBoundary,
    InvalidGrid,
    InvalidLevel,
    InvalidConfiguration,
    BoundaryResolutionFailed,
    BoundaryBudgetExceeded,
}

/// <summary>
/// Immutable result for one grid-local tile column. Boundary checks include the
/// top boundary between the map's maximum authored floor and open sky.
/// </summary>
public readonly record struct ZLevelSkyExposureState(
    ZLevelTileIndices Origin,
    ZLevelSkyExposureTermination Termination,
    int BoundaryChecks,
    int? BlockingLowerZ = null)
{
    public bool IsExposed => Termination == ZLevelSkyExposureTermination.Exposed;
}
