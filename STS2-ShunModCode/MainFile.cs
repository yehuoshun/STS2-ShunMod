using HarmonyLib;

namespace STS2_ShunMod.STS2_ShunModCode;

/// <summary>
/// STS2-ShunMod — Native mod entry point (no BaseLib).
/// </summary>
public static class MainFile
{
    public const string ModId = "STS2_ShunMod";

    private static readonly Harmony Harmony = new(ModId);

    static MainFile()
    {
        Harmony.PatchAll();
    }
}
