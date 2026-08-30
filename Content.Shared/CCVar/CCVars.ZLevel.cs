// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Maximum number of resolved vertical boundaries cached by each process.
    /// </summary>
    public static readonly CVarDef<int> ZLevelBoundaryCacheCapacity =
        CVarDef.Create(
            "zlevel.boundary_cache_capacity",
            8192,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum number of resolved Z-level sky exposure queries cached by each process.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSkyExposureCacheCapacity =
        CVarDef.Create(
            "zlevel.sky_exposure_cache_capacity",
            4096,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum adjacent vertical boundaries inspected by one sky exposure query.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSkyExposureMaxBoundaryChecks =
        CVarDef.Create(
            "zlevel.sky_exposure_max_boundary_checks",
            64,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum world-Z distance considered by normal visibility and PVS.
    /// </summary>
    public static readonly CVarDef<int> ZLevelVisibilityMaxLevelDistance =
        CVarDef.Create(
            "zlevel.visibility_max_level_distance",
            4,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum cross-floor visibility checks performed for one session PVS refresh.
    /// </summary>
    public static readonly CVarDef<int> ZLevelPvsVisibilityCheckBudget =
        CVarDef.Create(
            "zlevel.pvs_visibility_check_budget",
            16384,
            CVar.SERVERONLY);

    /// <summary>
    /// Maximum sessions whose Z-aware PVS and sound snapshots may refresh in one server update.
    /// </summary>
    public static readonly CVarDef<int> ZLevelPvsMaxSessionRefreshesPerUpdate =
        CVarDef.Create(
            "zlevel.pvs_max_session_refreshes_per_update",
            16,
            CVar.SERVERONLY);

    /// <summary>
    /// Maximum adjacent world-Z boundaries resolved by one shared trace.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTraceMaxVerticalCrossings =
        CVarDef.Create(
            "zlevel.trace_max_vertical_crossings",
            64,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum tile visits emitted by one shared trace.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTraceMaxTileVisits =
        CVarDef.Create(
            "zlevel.trace_max_tile_visits",
            8_192,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum entity hits emitted by one shared trace.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTraceMaxEntityHits =
        CVarDef.Create(
            "zlevel.trace_max_entity_hits",
            4_096,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum number of retained client aperture chunks used by vertical lighting and FOV.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingApertureCacheCapacity =
        CVarDef.Create(
            "zlevel.lighting_aperture_cache_capacity",
            4_096,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum number of retained vertical sound-portal chunks in each process.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundPortalCacheCapacity =
        CVarDef.Create(
            "zlevel.sound_portal_cache_capacity",
            4_096,
            CVar.REPLICATED | CVar.SERVER);

    /// <summary>
    /// Maximum adjacent floors crossed by one vertical sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxCrossings =
        CVarDef.Create("zlevel.sound_route_max_crossings", 8, CVar.SERVERONLY);

    /// <summary>
    /// Maximum portal chunks inspected by one vertical sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxPortalChunks =
        CVarDef.Create("zlevel.sound_route_max_portal_chunks", 64, CVar.SERVERONLY);

    /// <summary>
    /// Maximum cold portal chunks built by one vertical sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxPortalBuilds =
        CVarDef.Create("zlevel.sound_route_max_portal_builds", 16, CVar.SERVERONLY);

    /// <summary>
    /// Maximum open portal candidates inspected by one vertical sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxPortalCandidates =
        CVarDef.Create("zlevel.sound_route_max_portal_candidates", 2_048, CVar.SERVERONLY);

    /// <summary>
    /// Maximum candidate-to-candidate edges evaluated by one sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxEdges =
        CVarDef.Create("zlevel.sound_route_max_edges", 32_768, CVar.SERVERONLY);

    /// <summary>
    /// Maximum unique atmosphere cells sampled by one sound route.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundRouteMaxMediumSamples =
        CVarDef.Create("zlevel.sound_route_max_medium_samples", 4_096, CVar.SERVERONLY);

    /// <summary>
    /// Maximum cross-floor audio/listener route checks performed for one session refresh.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundPlaybackMaxRouteChecksPerRefresh =
        CVarDef.Create("zlevel.sound_playback_max_route_checks_per_refresh", 128, CVar.SERVERONLY);

    /// <summary>
    /// Maximum cross-floor audio presentations authorized for one session refresh.
    /// </summary>
    public static readonly CVarDef<int> ZLevelSoundPlaybackMaxPresentationsPerRefresh =
        CVarDef.Create("zlevel.sound_playback_max_presentations_per_refresh", 128, CVar.SERVERONLY);

    /// <summary>
    /// Maximum native point-light tree entries inspected by vertical lighting per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxEmitterCandidatesPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_emitter_candidates_per_frame",
            4_096,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum lower-floor point-light sources planned per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxEmittersPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_emitters_per_frame",
            256,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum adjacent aperture layers composed by vertical lighting per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxApertureLayersPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_aperture_layers_per_frame",
            4_096,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum cold aperture chunks built by vertical lighting per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxApertureBuildsPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_aperture_builds_per_frame",
            32,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum aperture runs generated by vertical lighting per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxRunsPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_runs_per_frame",
            8_192,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum lower-floor point-light shadow rows rendered per client frame.
    /// Sources beyond this limit retain their unshadowed projection.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxShadowLightsPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_shadow_lights_per_frame",
            64,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum lower-floor world-Z occluder groups uploaded per client frame.
    /// Sources beyond this limit retain their unshadowed projection.
    /// </summary>
    public static readonly CVarDef<int> ZLevelLightingMaxShadowFloorGroupsPerFrame =
        CVarDef.Create(
            "zlevel.lighting_max_shadow_floor_groups_per_frame",
            8,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum lower-floor tile chunks composed per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTileProjectionMaxChunksPerFrame =
        CVarDef.Create(
            "zlevel.tile_projection_max_chunks_per_frame",
            128,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum adjacent aperture layers composed for lower-floor tiles per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTileProjectionMaxApertureLayersPerFrame =
        CVarDef.Create(
            "zlevel.tile_projection_max_aperture_layers_per_frame",
            4_096,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum cold aperture chunks built for lower-floor tiles per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTileProjectionMaxApertureBuildsPerFrame =
        CVarDef.Create(
            "zlevel.tile_projection_max_aperture_builds_per_frame",
            32,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum lower-floor tile slots inspected per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelTileProjectionMaxTileVisitsPerFrame =
        CVarDef.Create(
            "zlevel.tile_projection_max_tile_visits_per_frame",
            16_384,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum adjacent mapping-preview chunks composed per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelMappingPreviewMaxChunksPerFrame =
        CVarDef.Create(
            "zlevel.mapping_preview_max_chunks_per_frame",
            128,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum adjacent mapping-preview tile slots inspected per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelMappingPreviewMaxTileVisitsPerFrame =
        CVarDef.Create(
            "zlevel.mapping_preview_max_tile_visits_per_frame",
            16_384,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum active-floor weather exposure queries performed per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelWeatherMaskMaxTileChecksPerFrame =
        CVarDef.Create(
            "zlevel.weather_mask_max_tile_checks_per_frame",
            16_384,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum horizontal weather-mask runs retained per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelWeatherMaskMaxRunsPerFrame =
        CVarDef.Create(
            "zlevel.weather_mask_max_runs_per_frame",
            8_192,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum same-floor exposure queries used to place ambient weather audio per client frame.
    /// </summary>
    public static readonly CVarDef<int> ZLevelWeatherAudioMaxTileChecksPerFrame =
        CVarDef.Create(
            "zlevel.weather_audio_max_tile_checks_per_frame",
            64,
            CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Maximum hierarchical states expanded by one Z-level path request.
    /// </summary>
    public static readonly CVarDef<int> ZLevelPathfindingMaxStateExpansions =
        CVarDef.Create("zlevel.pathfinding_max_state_expansions", 64, CVar.SERVERONLY);

    /// <summary>
    /// Maximum native same-floor paths requested by one hierarchical search.
    /// </summary>
    public static readonly CVarDef<int> ZLevelPathfindingMaxLocalPaths =
        CVarDef.Create("zlevel.pathfinding_max_local_paths", 128, CVar.SERVERONLY);

    /// <summary>
    /// Maximum authored vertical edges evaluated by one hierarchical search.
    /// </summary>
    public static readonly CVarDef<int> ZLevelPathfindingMaxTraversalEdges =
        CVarDef.Create("zlevel.pathfinding_max_traversal_edges", 512, CVar.SERVERONLY);

    public static readonly CVarDef<bool> ZLevelDebugOverlay =
        CVarDef.Create("zlevel.debug_overlay", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
