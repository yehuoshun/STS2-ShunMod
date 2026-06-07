using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 能量保留（冰激凌逻辑）
// 回合开始时能量不清零，剩余能量累积到下一回合。
// 直接拦截 Hook.ShouldPlayerResetEnergy，始终返回 false。
// ════════════════════════════════════════════════════════

[HarmonyPatch]
public static class EnergyRetentionPatch
{
    [HarmonyTargetMethod]
    private static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(Hook), nameof(Hook.ShouldPlayerResetEnergy));
    }

    [HarmonyPrefix]
    private static bool Prefix(ref bool __result)
    {
        // 始终返回 false → 能量不清零，等同于持有冰激凌
        __result = false;
        return false; // 跳过原始方法
    }
}