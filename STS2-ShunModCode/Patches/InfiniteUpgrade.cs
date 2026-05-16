using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级系统 — 参考 STS2Plus UnlimitedGrowth 实现
//
// Patch 1: MaxUpgradeLevel getter → 拉到 99
// Patch 2: IsUpgradable getter → ShunCard 强制可升级（兜底）
// ════════════════════════════════════════════════════════

internal static class UpgradeConst
{
    public const int MaxUpgradeCap = 99;
}

/// <summary>
///     Patch 1: 拦截所有 CardModel 子类的 MaxUpgradeLevel getter。
///     参照 STS2Plus UnlimitedGrowthMaxUpgradePatch，用 TargetMethods 动态扫描。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgradeLevel
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null)
            yield return baseGetter;

        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type)
                yield return getter;
        }
    }

    private static void Postfix(CardModel __instance, ref int __result)
    {
        if (__result > 0 && __result < UpgradeConst.MaxUpgradeCap) __result = UpgradeConst.MaxUpgradeCap;
    }
}

/// <summary>
///     Patch 2（兜底）: 直接拦截 IsUpgradable getter。
///     当 Patch 1 因版本 API 变化未命中时，确保 ShunCard 始终可升级。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgrade_IsUpgradable
{
    private static void Postfix(CardModel __instance, ref bool __result)
    {
        if (!__result && __instance is ShunCard) __result = true;
    }
}