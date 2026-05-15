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
// 1. ConditionalWeakTable 存所有附魔引用
// 2. 同类附魔叠加 Amount
// 3. 异类附魔分别记录，EnchantInternal 后统一遍历全部 apply
//    （EnchantInternal/FinalizeUpgradeInternal 只看 card.Enchantment，
//     会覆盖之前的附魔效果，必须在之后全量重刷）
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
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), new Type[] { typeof(EnchantmentModel), typeof(CardModel), typeof(decimal) })]
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
            // 同类附魔：叠加层数
            existing.Amount += (int)amount;
        }
        else
        {
            // 异类附魔：记录新附魔，不调用 EnchantInternal（会覆盖旧附魔状态）
            enchantment.Amount = (int)amount;
            dict[typeKey] = enchantment;
        }

        // ⚠️ 修复「附魔覆盖」bug：
        // EnchantInternal 会清掉旧附魔的内部状态（buff/特效等），
        // ModifyCard 只能恢复属性修改，无法复原 EnchantInternal 的副作用。
        // 因此改为全量重建：先清除，再用第一个附魔做 EnchantInternal（主附魔），
        // 最后全部附魔统一 ModifyCard 叠加效果。
        card.ClearEnchantmentInternal();

        var primaryEnchant = dict.Values.First();
        card.EnchantInternal(primaryEnchant, primaryEnchant.Amount);
        card.FinalizeUpgradeInternal();

        foreach (var ench in dict.Values)
            ench.ModifyCard();

        __result = dict[typeKey];
        return false; // 跳过原始方法
    }
}

/// <summary>
/// Patch 3: ClearEnchantment 清掉所有附魔。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ClearEnchantment), new Type[] { typeof(CardModel) })]
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
