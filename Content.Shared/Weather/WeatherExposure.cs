// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Content.Shared.ZLevel;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Shared.Weather;

/// <summary>
/// Describes the local policy that allowed or rejected weather at one point.
/// Consumers act only on <see cref="Exposed"/> and fail closed otherwise.
/// </summary>
public enum WeatherExposureTermination : byte
{
    Exposed,
    InvalidCoordinates,
    InvalidGrid,
    InvalidLevel,
    TileDisallowsWeather,
    PlanarRoof,
    AnchoredBlocker,
    SkyBlocked,
}

/// <summary>
/// Immutable weather-policy result. A null grid/tile with an exposed result
/// represents unobstructed map space rather than a grid-local tile.
/// </summary>
public readonly record struct WeatherExposureState(
    WeatherExposureTermination Termination,
    EntityUid? GridUid = null,
    ZLevelTileIndices? Tile = null,
    ZLevelSkyExposureTermination? SkyTermination = null)
{
    public bool IsExposed => Termination == WeatherExposureTermination.Exposed;
}
