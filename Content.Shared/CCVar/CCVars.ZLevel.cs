// DragonStation Z-Level prototype.
// Copyright (c) pedel and OpenAI Codex.

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> ZLevelDebugOverlay =
        CVarDef.Create("zlevel.debug_overlay", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
