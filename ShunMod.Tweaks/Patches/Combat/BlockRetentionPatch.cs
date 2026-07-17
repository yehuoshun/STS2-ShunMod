using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using ShunMod.Core.Core.Helpers;

namespace ShunMod.Tweaks.Patches.Combat;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance/__result/__state 约定

// ClearBlock 跳过：玩家生物不清格挡
[HarmonyPatch]
public static class BlockRetentionClearBlockPatch
{
    private static MethodInfo? TargetMethod() =>
        AccessTools.Method(CreatureReflection.CreatureType, "ClearBlock");

    private static bool Prefix(object __instance, ref Task __result)
    {
        if (!CreatureReflection.IsPlayer(__instance)) return true;
        __result = Task.CompletedTask;
        return false;
    }
}

// PrepareForNextTurn 拦截：Prefix 记格挡 → Postfix 恢复
[HarmonyPatch]
public static class BlockRetentionPrepareForNextTurnPatch
{
    private static MethodInfo? TargetMethod() =>
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
