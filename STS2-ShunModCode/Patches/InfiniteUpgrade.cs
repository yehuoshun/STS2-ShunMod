using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限升级系统 — 允许卡牌无限次升级，0 费卡升级累积能量
// ════════════════════════════════════════════════════════

/// <summary>
/// 使所有卡牌的 IsUpgradable 永远返回 true，允许无限次升级。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgrade_IsUpgradable
{
    /// <summary>
    /// 后置拦截 — 将 IsUpgradable 返回值强制设为 true。
    /// </summary>
    /// <param name="__result">原始返回值</param>
    static void Postfix(ref bool __result) => __result = true;
}

/// <summary>
/// 能量存储表 — 使用 ConditionalWeakTable 记录每张卡升级累积的能量奖励。
/// </summary>
/// <remarks>
/// 选择 ConditionalWeakTable 而非 Dictionary 的原因：
/// 当 CardModel 被 GC 回收时，对应的能量记录自动清除，避免内存泄漏。
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
/// 0 费卡每次升级累积 +1 能量。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Upgraded))]
public static class InfiniteUpgrade_GainEnergy
{
    /// <summary>
    /// 前置拦截 — 若卡牌费用 ≤ 0 且存在费用组件，累积 1 点能量。
    /// </summary>
    /// <param name="__instance">被升级的卡牌实例</param>
    static void Prefix(CardModel __instance)
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
