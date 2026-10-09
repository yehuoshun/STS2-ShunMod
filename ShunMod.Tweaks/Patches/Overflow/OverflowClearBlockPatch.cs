using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result/__instance 约定

/// <summary>
///     护盾超限记账（清场侧）— 格挡被清空时「超限」层数同步清零，
///     铁壁类效果阻止清格挡时层数同步保留：两者永远同命运。
///     挂 ShouldClearBlock（ClearBlock 的唯一判定点），天然兼容「格挡保留」。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldClearBlock))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowClearBlockPatch
{
    [HarmonyPostfix]
    private static void Postfix(bool __result, Creature creature)
    {
        try
        {
            if (!OverflowConfig.Enabled) return;
            if (!__result) return; // 有人阻止清格挡 → 层数同步保留

            OverflowPower? power = creature.GetPower<OverflowPower>();
            if (power == null || power.Amount <= 0) return;

            // 与格挡一同清零；直接走内部移除（绕过 PowerCmd.Remove 补丁，只拦敌方清除）
            power.RemoveInternal();
        }
        catch (Exception ex)
        {
            Log.Error($"[ShunMod_Tweaks/超限] 清场失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}