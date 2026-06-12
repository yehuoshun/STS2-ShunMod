using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Enchantments;
using STS2ShunMod.STS2_ShunModCode.Settings;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Cards;

/// <summary>
///     取消 Spiral 附魔的「仅限基础打击/防御」限制，改为任意卡牌皆可附魔。
/// </summary>
[HarmonyPatch(typeof(Spiral), nameof(Spiral.CanEnchant))]
public static class SpiralCanEnchantPatch
{
    [HarmonyPostfix]
    private static void Postfix(CardModel c, ref bool __result)
    {
        if (!PatchManager.IsEnabled("SpiralEnchant")) return;
        if (c != null) __result = true;
    }
}