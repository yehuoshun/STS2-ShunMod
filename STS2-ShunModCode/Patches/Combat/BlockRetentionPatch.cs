using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Combat;

// ════════════════════════════════════════════════════════
// 格挡保留系统
// ClearBlock：玩家生物直接跳过，格挡纹丝不动
// PrepareForNextTurn：回合结束前记下格挡 → 结束后原样恢复
// 仅对玩家生物生效，不影响怪物
// ════════════════════════════════════════════════════════

/// <summary>
///     Creature 反射工具 — 访问 Creature 类型内部属性（Block / IsPlayer）。
///     内联自原 Core/CreatureReflection.cs。
/// </summary>
internal static class CreatureReflection
{
    public static readonly Type? CreatureType =
        AccessTools.TypeByName("MegaCrit.Sts2.Core.Entities.Creatures.Creature");

    public static readonly PropertyInfo? BlockProperty =
        AccessTools.Property(CreatureType, "Block");

    public static readonly PropertyInfo? IsPlayerProperty =
        AccessTools.Property(CreatureType, "IsPlayer");

    public static int GetBlock(object creature) => BlockProperty?.GetValue(creature) as int? ?? 0;

    public static void SetBlock(object creature, int value) => BlockProperty?.SetValue(creature, value);

    public static bool IsPlayer(object? creature) =>
        creature != null && IsPlayerProperty?.GetValue(creature) is true;
}

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