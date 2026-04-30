using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 无限升级 — IsUpgradable 永远返回 true。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.IsUpgradable), MethodType.Getter)]
public static class InfiniteUpgrade_IsUpgradable
{
    static void Postfix(ref bool __result) => __result = true;
}

/// <summary>
/// 能量存储表 — 记录每张卡升级累积的能量奖励。
/// </summary>
internal static class UpgradeEnergyStore
{
    private static readonly ConditionalWeakTable<CardModel, StrongBox<int>> EnergyBonus = new();

    public static int Get(CardModel card) =>
        EnergyBonus.TryGetValue(card, out var box) ? box.Value : 0;

    public static void Add(CardModel card, int amount)
    {
        var box = EnergyBonus.GetOrCreateValue(card);
        box.Value += amount;
    }
}

/// <summary>
/// 0费卡升级 → 累积 +1 能量。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Upgrade))]
public static class InfiniteUpgrade_GainEnergy
{
    static void Prefix(CardModel __instance)
    {
        int cost = __instance.EnergyCost?.GetResolved() ?? int.MaxValue;
        if (cost <= 0 && __instance.EnergyCost != null)
            UpgradeEnergyStore.Add(__instance, 1);
    }
}

/// <summary>
/// 打出卡牌时释放累积的能量。
/// </summary>
[HarmonyPatch(typeof(CardModel), "OnPlay")]
public static class InfiniteUpgrade_OnPlay
{
    static void Prefix(CardModel __instance)
    {
        int bonus = UpgradeEnergyStore.Get(__instance);
        if (bonus > 0)
            PlayerCmd.GainEnergy(bonus, __instance.Owner);
    }
}
