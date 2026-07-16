using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.Cards;

// 让动量附魔可叠加（IsStackable → true）
// 原生 EnchantmentModel.IsStackable 默认 false，导致 CanEnchant 拒接重复附魔。
// 动量用 _extraDamage 累加状态，需要靠 Amount 叠加来让每次 OnPlay 的增量翻倍。
[HarmonyPatch]
public static class MomentumStackablePatch
{
    private static MethodBase TargetMethod()
    {
        // Momentum 不 override IsStackable，所以 getter 在 EnchantmentModel 上
        // 用 AccessTools 显式获取基类 getter，然后在 Postfix 里判断 is Momentum
        return AccessTools.PropertyGetter(typeof(EnchantmentModel), nameof(EnchantmentModel.IsStackable));
    }

    [HarmonyPostfix]
    private static void Postfix(EnchantmentModel __instance, ref bool __result)
    {
        if (__instance is Momentum)
            __result = true;
    }
}