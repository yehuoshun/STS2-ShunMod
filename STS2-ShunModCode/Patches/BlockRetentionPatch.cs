using System;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 格挡保留系统 — 参考 STS2Plus GlassCannonBlockRetention
//
// 回合内 ClearBlock：不清零，保留最多 15 点
// 回合结束 PrepareForNextTurn：记下格挡 → 恢复最多 15 点
// 仅对玩家生物生效，不影响怪物
//
// 注意：使用 CreatureReflection 反射访问，不依赖 Publicizer。
// ════════════════════════════════════════════════════════

internal static class BlockRetentionConst
{
    public const int MaxRetained = 15;
}

/// <summary>
/// Patch 1: 拦截 ClearBlock()，玩家生物不清零，保留最多 MaxRetained 点格挡。
/// </summary>
[HarmonyPatch]
public static class BlockRetentionClearBlockPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(CreatureReflection.CreatureType, "ClearBlock");
    }

    static bool Prefix(object __instance, ref Task __result)
    {
        if (!CreatureReflection.IsPlayer(__instance))
            return true;

        // 始终保留 min(当前, 15)，绝不归零
        int block = CreatureReflection.GetBlock(__instance);
        int capped = Math.Min(block, BlockRetentionConst.MaxRetained);
        CreatureReflection.SetBlock(__instance, capped);
        __result = Task.CompletedTask;
        return false;
    }
}

/// <summary>
/// Patch 2: 拦截 PrepareForNextTurn()，回合结束保留格挡。
/// Prefix 记下格挡值到 __state → PrepareForNextTurn 清掉 → Postfix 恢复。
/// </summary>
[HarmonyPatch]
public static class BlockRetentionPrepareForNextTurnPatch
{
    static MethodBase TargetMethod()
    {
        return AccessTools.Method(CreatureReflection.CreatureType, "PrepareForNextTurn");
    }

    static void Prefix(object __instance, ref int __state)
    {
        if (!CreatureReflection.IsPlayer(__instance))
            return;

        __state = CreatureReflection.GetBlock(__instance);
    }

    static void Postfix(object __instance, int __state)
    {
        if (__state <= 0 || !CreatureReflection.IsPlayer(__instance))
            return;

        int capped = __state > BlockRetentionConst.MaxRetained
            ? BlockRetentionConst.MaxRetained
            : __state;

        CreatureReflection.SetBlock(__instance, capped);
    }
}
