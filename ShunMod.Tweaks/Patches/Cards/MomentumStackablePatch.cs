using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.Cards;

// 让动量附魔可叠加：直接 Patch CanEnchant 跳过「已有附魔且类型不同」的拦截。
// 动量已附魔的牌再次选择动量时，走 CardCmd.Enchant 的 Amount 叠加逻辑。
[HarmonyPatch(typeof(EnchantmentModel), nameof(EnchantmentModel.CanEnchant))]
public static class MomentumStackablePatch
{
    [HarmonyPostfix]
    private static void Postfix(EnchantmentModel __instance, CardModel card, ref bool __result)
    {
        // 原方法返回 false 且卡牌已有同类型附魔（动量）→ 允许叠加
        if (!__result && __instance is Momentum && card.Enchantment?.GetType() == typeof(Momentum))
            __result = true;
    }
}