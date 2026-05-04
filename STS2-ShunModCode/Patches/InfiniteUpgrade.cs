using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
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

/// <summary>
/// 能量存储表 — 使用 ConditionalWeakTable 记录每张卡升级累积的能量奖励。
/// </summary>
/// <remarks>
/// ConditionalWeakTable：卡牌被 GC 回收时自动清除对应记录，避免内存泄漏。
/// </remarks>
internal static class UpgradeEnergyStore
{
    /// <summary>
    /// 卡牌 → 累积能量奖励的映射表。
    /// </summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<int>> EnergyBonus = new();

    /// <summary>
    /// 获取指定卡牌累积的能量奖励。
    /// </summary>
    /// <param name="card">目标卡牌</param>
    /// <returns>累积能量值，无记录返回 0</returns>
    public static int Get(CardModel card) =>
        EnergyBonus.TryGetValue(card, out var box) ? box.Value : 0;

    /// <summary>
    /// 为指定卡牌增加能量累积。
    /// </summary>
    /// <param name="card">目标卡牌</param>
    /// <param name="amount">增加量</param>
    public static void Add(CardModel card, int amount)
    {
        var box = EnergyBonus.GetOrCreateValue(card);
        box.Value += amount;
    }
}

/// <summary>
/// 0 费卡每次实际升级后累积 +1 能量。
/// Patch UpgradeInternal（实际升级方法）而非 Upgraded（属性 getter）。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.UpgradeInternal))]
public static class InfiniteUpgrade_GainEnergy
{
    /// <summary>
    /// 后置拦截 — 升级完成后，若卡牌费用 ≤ 0 且存在费用组件，累积 1 点能量。
    /// </summary>
    /// <param name="__instance">被升级的卡牌实例</param>
    static void Postfix(CardModel __instance)
    {
        int cost = __instance.EnergyCost?.GetResolved() ?? int.MaxValue;
        if (cost <= 0 && __instance.EnergyCost != null)
            UpgradeEnergyStore.Add(__instance, 1);
    }
}

/// <summary>
/// 打出卡牌时释放累积的能量奖励。
/// </summary>
[HarmonyPatch(typeof(CardModel), "OnPlay")]
public static class InfiniteUpgrade_OnPlay
{
    /// <summary>
    /// 前置拦截 — 打出卡牌前，释放该卡牌累积的所有能量。
    /// </summary>
    /// <param name="__instance">被打出的卡牌实例</param>
    static void Prefix(CardModel __instance)
    {
        int bonus = UpgradeEnergyStore.Get(__instance);
        if (bonus > 0)
            PlayerCmd.GainEnergy(bonus, __instance.Owner);
    }
}
