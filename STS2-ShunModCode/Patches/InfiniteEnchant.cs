using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限附魔系统
// 卡牌同时拥有多种附魔，同类附魔叠加层数
//
// 原理：
// 1. Dictionary + ReferenceEqualityComparer 存所有附魔引用
//    （Godot Resource 引用计数与 ConditionalWeakTable 弱引用不兼容，
//     导致 HasEnchantType 漏检已注能卡牌，改用强引用 Dictionary）
// 2. 同类附魔叠加 Amount
// 3. 异类附魔分别记录，EnchantInternal 后统一遍历全部 apply
//    （EnchantInternal/FinalizeUpgradeInternal 只看 card.Enchantment，
//     会覆盖之前的附魔效果，必须在之后全量重刷）
// ════════════════════════════════════════════════════════

/// <summary>
///     Patch 1: CanEnchant 永远返回 true，移除附魔条件限制。
/// </summary>
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class InfiniteEnchant_CanEnchant
{
    private static void Postfix(ref bool __result)
    {
        __result = true;
    }
}

/// <summary>
///     Patch 2: 完全替换 CardCmd.Enchant，支持多种附魔叠加。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), typeof(EnchantmentModel), typeof(CardModel), typeof(decimal))]
public static class InfiniteEnchant_MultiEnchant
{
    /// <summary>
    ///     每张卡的全部附魔：类型全名 → 附魔对象。
    ///     使用引用相等比较器，确保同一 CardModel 实例匹配。
    /// </summary>
    internal static readonly Dictionary<CardModel, Dictionary<string, EnchantmentModel>> AllEnchantments =
        new(ReferenceEqualityComparer.Instance);

    /// <summary>检查卡牌是否已拥有指定类型的附魔。</summary>
    internal static bool HasEnchantType(CardModel card, string typeFullName)
    {
        return AllEnchantments.TryGetValue(card, out var dict)
               && dict.ContainsKey(typeFullName);
    }

    [HarmonyPrefix]
    private static bool Prefix(
        EnchantmentModel enchantment,
        CardModel card,
        decimal amount,
        ref EnchantmentModel __result)
    {
        enchantment.AssertMutable();

        var typeKey = enchantment.GetType().FullName!;
        if (!AllEnchantments.TryGetValue(card, out var dict))
        {
            dict = new Dictionary<string, EnchantmentModel>();
            AllEnchantments[card] = dict;
        }

        if (dict.TryGetValue(typeKey, out var existing))
        {
            // 同类附魔：叠加层数
            existing.Amount += (int)amount;
        }
        else
        {
            // 异类附魔
            enchantment.Amount = (int)amount;
            dict[typeKey] = enchantment;

            if (dict.Count == 1)
                card.EnchantInternal(enchantment, amount);
            else
                enchantment.ApplyInternal(card, amount);
        }

        // 对所有附魔调 ModifyCard：需临时切 card.Enchantment（private set，用反射写）
        var firstEnch = card.Enchantment;
        var enchantProp = typeof(CardModel).GetProperty("Enchantment",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var ench in dict.Values)
        {
            if (card.Enchantment != ench)
                enchantProp.SetValue(card, ench);
            ench.ModifyCard();
        }
        enchantProp.SetValue(card, firstEnch);

        // FinalizeUpgradeInternal 提交 DynamicVars + EnergyCost 变更
        // 必须在所有附魔 ModifyCard 之后调用，否则费用等变更不生效
        card.FinalizeUpgradeInternal();

        __result = dict[typeKey];
        return false; // 跳过原始方法
    }
}

/// <summary>
///     Patch 3: 多附魔描述 — 在卡牌描述末尾列出所有附魔名称。
/// </summary>
[HarmonyPatch]
public static class InfiniteEnchant_MultiDescription
{
    private static MethodBase TargetMethod()
    {
        var previewType = typeof(CardModel).GetNestedType("DescriptionPreviewType",
            BindingFlags.NonPublic)!;
        return AccessTools.Method(typeof(CardModel), "GetDescriptionForPile",
            [typeof(PileType), previewType, typeof(Creature)]);
    }

    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (!InfiniteEnchant_MultiEnchant.AllEnchantments.TryGetValue(__instance, out var dict))
            return;
        if (dict.Count <= 1) return;

        var primaryKey = __instance.Enchantment?.GetType().FullName;
        var names = new List<string>();
        foreach (var (typeKey, ench) in dict)
        {
            if (typeKey == primaryKey) continue;
            try
            {
                var name = ench.Title?.GetRawText() ?? ench.GetType().Name;
                names.Add(ench.Amount > 1 ? $"{name} x{ench.Amount}" : name);
            }
            catch { }
        }

        if (names.Count > 0)
            __result += $"\n[color=#aaccff]附魔: {string.Join("，", names)}[/color]";
    }
}

/// <summary>
///     Patch 4: ClearEnchantment 清掉所有附魔。
/// </summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.ClearEnchantment), typeof(CardModel))]
public static class InfiniteEnchant_ClearAll
{
    [HarmonyPrefix]
    private static bool Prefix(CardModel card)
    {
        card.ClearEnchantmentInternal();
        InfiniteEnchant_MultiEnchant.AllEnchantments.Remove(card);
        return false;
    }
}