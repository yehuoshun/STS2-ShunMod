using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 无限附魔系统 v2
// 卡牌同时拥有多种附魔，同类附魔叠加层数
//
// 核心策略：
// 1. Dictionary + ReferenceEqualityComparer 存所有附魔
// 2. 新附魔统一走 EnchantInternal（完整注册），不混用 ApplyInternal
// 3. ModifyCard 循环叠加所有附魔效果
// 4. FinalizeUpgradeInternal 提交全部变更
// ════════════════════════════════════════════════════════

/// <summary>Patch 1: CanEnchant 永远返回 true</summary>
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class InfiniteEnchant_CanEnchant
{
    private static void Postfix(ref bool __result) => __result = true;
}

/// <summary>Patch 2: 替换 CardCmd.Enchant，支持多附魔叠加</summary>
[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), typeof(EnchantmentModel), typeof(CardModel), typeof(decimal))]
public static class InfiniteEnchant_MultiEnchant
{
    internal static readonly Dictionary<CardModel, Dictionary<string, EnchantmentModel>> AllEnchantments =
        new(ReferenceEqualityComparer.Instance);

    internal static bool HasEnchantType(CardModel card, string typeFullName) =>
        AllEnchantments.TryGetValue(card, out var dict) && dict.ContainsKey(typeFullName);

    private static readonly PropertyInfo EnchantmentProp = typeof(CardModel).GetProperty("Enchantment",
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

    /// <summary>
    ///     确保卡片的字典已初始化。读档场景中卡片可能已有附魔（card.Enchantment != null），
    ///     此时将其纳入字典管理，避免被新附魔覆盖丢失。
    /// </summary>
    internal static Dictionary<string, EnchantmentModel> EnsureDict(CardModel card)
    {
        if (AllEnchantments.TryGetValue(card, out var dict))
            return dict;

        dict = new Dictionary<string, EnchantmentModel>();
        AllEnchantments[card] = dict;

        if (card.Enchantment != null)
        {
            var typeKey = card.Enchantment.GetType().FullName!;
            dict[typeKey] = card.Enchantment;
        }

        return dict;
    }

    /// <summary>
    ///     遍历所有附魔调用 ModifyCard，然后 FinalizeUpgradeInternal 提交。
    ///     通过反射临时切换 card.Enchantment，确保每个附魔的 ModifyCard
    ///     正确读取自身数据。
    /// </summary>
    internal static void RefreshAllEffects(CardModel card, Dictionary<string, EnchantmentModel> dict)
    {
        if (dict.Count == 0) return;

        foreach (var ench in dict.Values)
        {
            if (card.Enchantment != ench)
                EnchantmentProp.SetValue(card, ench);
            ench.ModifyCard();
        }

        EnchantmentProp.SetValue(card, dict.Values.First());
        card.FinalizeUpgradeInternal();
    }

    [HarmonyPrefix]
    private static bool Prefix(
        EnchantmentModel enchantment,
        CardModel card,
        decimal amount,
        ref EnchantmentModel __result)
    {
        enchantment.AssertMutable();

        var dict = EnsureDict(card);
        var typeKey = enchantment.GetType().FullName!;

        if (dict.TryGetValue(typeKey, out var existing))
        {
            // 同类附魔：只叠加层数，ModifyCard 会读取新 Amount
            existing.Amount += (int)amount;
        }
        else
        {
            // 异类附魔：走 EnchantInternal 完整注册
            enchantment.Amount = (int)amount;
            dict[typeKey] = enchantment;
            card.EnchantInternal(enchantment, amount);
        }

        // 全量刷新所有附魔效果
        RefreshAllEffects(card, dict);

        __result = dict[typeKey];
        return false;
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
