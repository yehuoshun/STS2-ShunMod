using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using STS2ShunMod.STS2_ShunModCode.Core;
namespace STS2ShunMod.STS2_ShunModCode.Patches.Combat;

// ════════════════════════════════════════════════════════
// 格挡保留系统
// ClearBlock：玩家生物直接跳过，格挡纹丝不动
// PrepareForNextTurn：回合结束前记下格挡 → 结束后原样恢复
// 仅对玩家生物生效，不影响怪物
// ════════════════════════════════════════════════════════

/// <summary>
///     Patch 1: 拦截 ClearBlock()，玩家生物不执行清格挡，直接跳过。
/// </summary>
[HarmonyPatch]
public static class BlockRetentionClearBlockPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(CreatureReflection.CreatureType, "ClearBlock");

    private static bool Prefix(object __instance, ref Task __result)
    {
        if (!CreatureReflection.IsPlayer(__instance)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>
///     Patch 2: 拦截 PrepareForNextTurn()，回合结束保留格挡。
///     Prefix 记下格挡值 → 游戏内部清理 → Postfix 原样恢复。
/// </summary>
[HarmonyPatch]
public static class BlockRetentionPrepareForNextTurnPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(CreatureReflection.CreatureType, "PrepareForNextTurn");

    private static void Prefix(object __instance, ref int __state)
    {
        if (!CreatureReflection.IsPlayer(__instance)) return;
        __state = CreatureReflection.GetBlock(__instance);
    }

    private static void Postfix(object __instance, int __state)
    {
        try
        {
            if (__state <= 0 || !CreatureReflection.IsPlayer(__instance)) return;
            CreatureReflection.SetBlock(__instance, __state);
        }
        catch (Exception ex)
        {
            Log.Error($"[格挡保留/回合结束] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
