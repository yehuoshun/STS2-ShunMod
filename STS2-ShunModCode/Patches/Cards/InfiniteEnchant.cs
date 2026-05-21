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
/// </summary>

/// Patch 1 — CanEnchant 始终返回 true
[HarmonyPatch]
public static class InfiniteEnchant_CanEnchant
{
    /// <summary>
    ///     源码：virtual bool CanEnchant(CardModel card)
    /// </summary>
    private static MethodBase? TargetMethod()
    {
        var types = new[] { typeof(CardModel) };
        var method = AccessTools.Method(typeof(EnchantmentModel), "CanEnchant", types);
        if (method != null)
        {
            ShunLogger.Info("无限附魔/CanEnchant", "匹配 CanEnchant(CardModel)");
            return method;
        }

        ShunLogger.Warn("无限附魔/CanEnchant", "未找到 CanEnchant(CardModel)，补丁跳过");
        return null;
    }

    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (!__result)
        {
            __result = true;
        }
    }
}

/// Patch 2 — Enchant 多附魔存储 + 全量重刷
/// [DISABLED] Harmony 对 static decimal 参数 Prefix 有兼容问题，PatchAll 直接炸。
/// 保留 CanEnchant 补丁（始终返回 true）暂时足够。
/*
[HarmonyPatch]
public static class InfiniteEnchant_Enchant
{
    internal static readonly Dictionary<CardModel, Dictionary<string, EnchantmentModel>> Store =
        new(ReferenceEqualityComparer.Instance);

    internal static bool HasType(CardModel card, string typeFullName) =>
        Store.TryGetValue(card, out var d) && d.ContainsKey(typeFullName);

    private static readonly PropertyInfo? Prop =
        AccessTools.Property(typeof(CardModel), "Enchantment");

    private static MethodBase? TargetMethod()
    {
        var types = new[] { typeof(EnchantmentModel), typeof(CardModel), typeof(decimal) };
        var method = AccessTools.Method(typeof(CardCmd), "Enchant", types);

        if (method != null)
        {
            ShunLogger.Info("无限附魔/Enchant", $"目标: CardCmd.Enchant({string.Join(", ", types.Select(t => t.Name))})");
            return method;
        }

        ShunLogger.Warn("无限附魔/Enchant", $"未找到 CardCmd.Enchant(EnchantmentModel, CardModel, decimal)，补丁跳过");
        return null;
    }

    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel m, CardModel card, decimal amount)
    {
        try
        {
            m.AssertMutable();

            if (!Store.TryGetValue(card, out var dict))
                Store[card] = dict = new Dictionary<string, EnchantmentModel>();

            var key = m.GetType().FullName!;

            if (dict.TryGetValue(key, out var ex))
            {
                ex.Amount += (int)amount;
                ShunLogger.Info("无限附魔/Enchant", $"叠加: card={card.GetType().Name} type={key} amount={ex.Amount}");
            }
            else
            {
                m.Amount = (int)amount;
                dict[key] = m;
                card.EnchantInternal(m, amount);
                ShunLogger.Info("无限附魔/Enchant", $"新增: card={card.GetType().Name} type={key} amount={m.Amount}");
            }

            // 全量重刷所有附魔效果
            foreach (var e in dict.Values)
            {
                if (card.Enchantment != e && Prop != null)
                    Prop.SetValue(card, e);
                e.ModifyCard();
            }

            if (Prop != null)
                Prop.SetValue(card, dict.Values.First());

            card.FinalizeUpgradeInternal();

            ShunLogger.Debug("无限附魔/状态", $"Store={Store.Count}卡, 当前={dict.Count}种");

            return false; // 跳过原版 Enchant
        }
        catch (Exception ex)
        {
            ShunLogger.Error("无限附魔/Enchant", ex);
            return true; // 炸了走原版单附魔，别崩游戏
        }
    }
}
*/