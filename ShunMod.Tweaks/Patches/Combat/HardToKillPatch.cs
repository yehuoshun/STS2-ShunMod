using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShunMod.Tweaks.Patches.Combat;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result 约定

/// <summary>
///     取消「难以杀灭」的限伤 — HardToKillPower 把外骨骼虫每次受到的伤害/生命减少封顶在 Amount（原版 9）点。
///     Postfix 直接把返回值改回 decimal.MaxValue（= 无上限）；伤害管线本身是按 Hook 取最小值
///     （Hook.cs 的 Cap 段：num4 = min(所有 listener 的 cap)），返回 MaxValue 等于这个 Power 不再参与封顶，
///     力量/易伤等加法乘法钩子不受影响。
/// </summary>
[HarmonyPatch(typeof(HardToKillPower), "ModifyDamageCap")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class HardToKillPatch
{
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __result 约定")]
    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static void Postfix(ref decimal __result)
    {
        try
        {
            __result = decimal.MaxValue;
        }
        catch (Exception ex)
        {
            Log.Error($"[难以杀灭/解除限伤] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
