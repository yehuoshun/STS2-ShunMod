using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.Cards;

// 让所有附魔在类型相同时可叠加：
// 原生 CanEnchant 中 `IsStackable` 默认为 false，卡牌已有附魔时直接拒接。
// 改成：如果卡牌已有同类型附魔，允许叠加（走 CardCmd.Enchant 的 Amount 累加）。
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class EnchantmentStackablePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnchantmentModel __instance, CardModel card, ref bool __result)
    {
        // 原方法返回 false 且卡牌已有同类型附魔 → 允许叠加
        if (!__result && card.Enchantment?.GetType() == __instance.GetType())
            __result = true;
    }
}