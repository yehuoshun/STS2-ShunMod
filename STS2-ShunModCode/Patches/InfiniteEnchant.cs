using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     无限附魔系统 — 卡牌可同时拥有多种附魔，同类叠加层数。
///
///     STS2 原生只支持单附魔（card.Enchantment），本系统通过 Harmony
///     拦截 CardCmd.Enchant，用 Dictionary 管理多附魔，并在每次操作后
///     遍历全部附魔依次调 ModifyCard + FinalizeUpgradeInternal 叠加效果。
///
///     注意：EnchantInternal 会覆盖 card.Enchantment 并可能清理卡牌内部
///     附魔数据，所以每次都需要"全量重刷"所有附魔的 ModifyCard。
/// </summary>

[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class InfiniteEnchant_CanEnchant
{
    private static void Postfix(ref bool __result)
    {
        if (!__result)
        {
            __result = true;
            ShunLogger.Info("无限附魔/CanEnchant", "强制返回 true");
        }
    }
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Enchant), typeof(EnchantmentModel), typeof(CardModel), typeof(decimal))]
public static class InfiniteEnchant_Enchant
{
    internal static readonly Dictionary<CardModel, Dictionary<string, EnchantmentModel>> Store =
        new(ReferenceEqualityComparer.Instance);

    internal static bool HasType(CardModel card, string typeFullName) =>
        Store.TryGetValue(card, out var d) && d.ContainsKey(typeFullName);

    private static readonly PropertyInfo Prop = typeof(CardModel).GetProperty(
        "Enchantment", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;

    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel m, CardModel card, decimal amount)
    {
        m.AssertMutable();

        if (!Store.TryGetValue(card, out var dict))
            Store[card] = dict = new Dictionary<string, EnchantmentModel>();

        var key = m.GetType().FullName!;

        if (dict.TryGetValue(key, out var ex))
        {
            ex.Amount += (int)amount;
        }
        else
        {
            m.Amount = (int)amount;
            dict[key] = m;
            card.EnchantInternal(m, amount);
        }

        // 全量重刷所有附魔效果
        foreach (var e in dict.Values)
        {
            ShunLogger.Info("无限附魔/Enchant", $"card={card.GetType().Name} type={key} amount={e.Amount}");
            if (card.Enchantment != e) Prop.SetValue(card, e);
            e.ModifyCard();
        }
        Prop.SetValue(card, dict.Values.First());
        card.FinalizeUpgradeInternal();

        return false;
    }
}
