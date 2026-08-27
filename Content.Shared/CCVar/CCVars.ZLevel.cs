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

    public static readonly CVarDef<bool> ZLevelDebugOverlay =
        CVarDef.Create("zlevel.debug_overlay", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
