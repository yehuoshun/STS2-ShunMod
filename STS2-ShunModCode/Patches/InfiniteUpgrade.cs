using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Utils;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级系统 — 参考 STS2Plus 实现
// 核心思路：Patch MaxUpgradeLevel + IsUpgradable 双保险
//           v0.105+ 可能改了升级判断逻辑，加 IsUpgradable 兜底
// ════════════════════════════════════════════════════════

/// <summary>
/// 升级上限常量。
/// </summary>
internal static class UpgradeConst
{
    /// <summary>
    /// 无限升级上限。99 次足以覆盖所有实战场景。
    /// </summary>
    public const int MaxUpgradeCap = 99;
}

/// <summary>
/// Patch 1: 扫描 CardModel 及其所有子类的 MaxUpgradeLevel getter，统一拦截。
/// 参照 STS2Plus 的 UnlimitedGrowthMaxUpgradePatch，用 TargetMethods 而非直接注解。
/// </summary>
[HarmonyPatch]
public static class InfiniteUpgrade_MaxUpgradeLevel
{
    /// <summary>
    /// 返回 CardModel 基类及其所有非抽象子类的 MaxUpgradeLevel getter。
    /// </summary>
    static IEnumerable<MethodBase> TargetMethods()
    {
        // 基类 getter
        var baseGetter = AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.MaxUpgradeLevel));
        if (baseGetter != null)
            yield return baseGetter;

        // 所有子类中重写了 MaxUpgradeLevel 的 getter
        foreach (var type in typeof(CardModel).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(CardModel).IsAssignableFrom(type))
                continue;

            var getter = AccessTools.PropertyGetter(type, nameof(CardModel.MaxUpgradeLevel));
            if (getter != null && getter.DeclaringType == type)
                yield return getter;
        }
    }

    /// <summary>
    /// 后置拦截 — 将 MaxUpgradeLevel 拉到 99，移除升级次数限制。
    /// </summary>
    /// <remarks>
    /// 仅跳过 __result == 0 的不可升级牌（诅咒/状态等无升级路径）。
    /// v0.105+ 部分卡牌可能返回非预期值，放宽守卫条件。
    /// </remarks>
    static void Postfix(CardModel __instance, ref int __result)
    {
        if (__result > 0 && __result < UpgradeConst.MaxUpgradeCap)
        {
            __result = UpgradeConst.MaxUpgradeCap;
        }
    }
}

/// <summary>
/// Patch 2（兜底）: 直接 Patch IsUpgradable getter。
/// 当 MaxUpgradeLevel 补丁因版本 API 变化未生效时，此补丁确保自定义卡牌始终可升级。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgrade_IsUpgradable
{
    static void Postfix(CardModel __instance, ref bool __result)
    {
        // 仅对 ShunCard 子类强制可升级，避免影响原版卡牌
        if (!__result && __instance is ShunCard)
        {
            __result = true;
        }
    }
}
