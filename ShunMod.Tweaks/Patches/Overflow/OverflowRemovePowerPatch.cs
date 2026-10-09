using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony 约定

/// <summary>
///     超限不可被移除 — 拦截 PowerCmd.Remove（「移除增益」类效果的统一漏斗，
///     泛型重载 Remove&lt;T&gt; 内部也走单参重载，一处拦截全覆盖）。
///     防止敌方清除增益类卡牌 / 事件把超限层数剥掉。
/// </summary>
[HarmonyPatch]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal static class OverflowRemovePowerPatch
{
    private static MethodInfo? TargetMethod()
    {
        // 单参重载 Remove(PowerModel?)，用反射精确匹配，避免命中泛型重载
        return AccessTools.Method(typeof(PowerCmd), nameof(PowerCmd.Remove), new[] { typeof(PowerModel) });
    }

    private static bool Prefix(PowerModel? power)
    {
        return power is not OverflowPower; // false = 跳过原始移除逻辑
    }
}