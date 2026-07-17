using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Cards;

// 让所有附魔在类型相同时可叠加：
// 原生 CanEnchant 中 IsStackable 默认为 false，卡牌已有附魔时直接拒接。
// 改成：如果卡牌已有同类型附魔，允许叠加（走 CardCmd.Enchant 的 Amount 累加）。
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant), typeof(CardModel))]
public static class EnchantmentStackablePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EnchantmentModel __instance, CardModel card, ref bool __result)
    {
        // 卡牌已有同类型附魔 → 跳过原方法，直接允许叠加
        if (card.Enchantment?.GetType() == __instance.GetType())
        {
            __result = true;
            return false;
        }
        return true; // 其他情况走原方法
    }
}