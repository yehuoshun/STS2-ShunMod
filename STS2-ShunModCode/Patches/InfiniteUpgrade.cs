using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级系统 — 参考 STS2Plus 实现
// 核心思路：Patch MaxUpgradeLevel 属性而非 IsUpgradable
//           游戏内部用 CurrentUpgradeLevel < MaxUpgradeLevel 判断
//           拉到 99 后自然支持无限升级
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
/// 扫描 CardModel 及其所有子类的 MaxUpgradeLevel getter，统一拦截。
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
    /// 安全检查：仅对 MaxUpgradeLevel >= 1 的卡牌生效（跳过诅咒/状态等不可升级牌）。
    /// </remarks>
    static void Postfix(CardModel __instance, ref int __result)
    {
        // 仅对有升级路径的卡牌生效
        if (__result >= 1 && __result < UpgradeConst.MaxUpgradeCap)
        {
            __result = UpgradeConst.MaxUpgradeCap;
        }
    }
}
