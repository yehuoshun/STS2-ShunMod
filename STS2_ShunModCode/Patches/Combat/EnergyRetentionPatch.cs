using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
using STS2ShunMod.STS2_ShunModCode.Settings;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Combat;

/// <summary>
///     能量保留（冰激凌逻辑）
///     回合开始时能量不清零，剩余能量累积到下一回合。
///     直接拦截 Hook.ShouldPlayerResetEnergy，始终返回 false。
/// </summary>
[HarmonyPatch]
public static class EnergyRetentionPatch
{
    [HarmonyTargetMethod]
    private static System.Reflection.MethodBase TargetMethod() =>
        AccessTools.Method(typeof(Hook), nameof(Hook.ShouldPlayerResetEnergy));

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        if (!PatchManager.IsEnabled("EnergyRetention")) return true;
        __result = false;
        return false;
    }
}