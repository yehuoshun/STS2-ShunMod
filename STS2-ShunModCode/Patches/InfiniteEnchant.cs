using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限附魔系统
// 卡牌同时拥有多种附魔，同类附魔叠加层数
//
// 原理：
// 1. 附魔效果通过 ModifyCard() 直接写入卡牌属性，不会丢失
// 2. ConditionalWeakTable 存所有附魔引用
// 3. 同类附魔叠加 Amount，异类附魔分别写入
// ════════════════════════════════════════════════════════

/// <summary>
/// Patch 1: CanEnchant 永远返回 true，移除附魔条件限制。
/// </summary>
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class InfiniteEnchant_CanEnchant
{
    static void Postfix(ref bool __result) => __result = true;
}

/// <summary>
/// Patch 2: 完全替换 CardCmd.Enchant，支持多种附魔叠加。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant))]
public static class InfiniteEnchant_MultiEnchant
{
    /// <summary>
    /// 每张卡的全部附魔：类型全名 → 附魔对象。
    /// </summary>
    internal static readonly ConditionalWeakTable<CardModel, Dictionary<string, EnchantmentModel>> AllEnchantments = new();

    [HarmonyPrefix]
    static bool Prefix(
        EnchantmentModel enchantment,
        CardModel card,
        decimal amount,
        ref EnchantmentModel __result)
    {
        enchantment.AssertMutable();

        var typeKey = enchantment.GetType().FullName!;
        var dict = AllEnchantments.GetOrCreateValue(card);

        if (dict.TryGetValue(typeKey, out var existing))
        {
            // 同类附魔：叠加层数并重新应用卡牌效果（Amount 变化后需刷新属性）
            existing.Amount += amount;
            existing.ModifyCard();
            __result = existing;
        }
        else
        {
            // 异类附魔：写入卡牌属性 + 记录引用
            card.EnchantInternal(enchantment, amount);
            enchantment.ModifyCard();
            dict[typeKey] = enchantment;
            __result = card.Enchantment;
        }

        card.FinalizeUpgradeInternal();
        return false; // 跳过原始方法
    }
}

/// <summary>
/// Patch 3: ClearEnchantment 清掉所有附魔。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ClearEnchantment))]
public static class InfiniteEnchant_ClearAll
{
    [HarmonyPrefix]
    static bool Prefix(CardModel card)
    {
        card.ClearEnchantmentInternal();
        InfiniteEnchant_MultiEnchant.AllEnchantments.Remove(card);
        return false;
    }
}
