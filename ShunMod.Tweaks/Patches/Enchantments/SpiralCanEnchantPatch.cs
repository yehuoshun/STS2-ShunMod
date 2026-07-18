using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.Enchantments;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result 约定

/// <summary>
///     取消涡旋（Spiral）附魔的「仅限基础打击/防御」限制，改为任意卡牌皆可附魔。
/// </summary>
[HarmonyPatch(typeof(Spiral), nameof(Spiral.CanEnchant))]
internal static class SpiralCanEnchantPatch
{
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(CardModel c, ref bool __result)
    {
        if (c != null)
            __result = true;
    }
}