using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2ShunMod.Patches.Combat;

/// <summary>
///     修复硬化外壳能力 — 使 ModifyHpLostBeforeOstyLate 返回原始伤害值，取消减伤效果。
/// </summary>
[HarmonyPatch(typeof(HardenedShellPower), "ModifyHpLostBeforeOstyLate")]
public static class HardenedShellPatch
{
    private static void Postfix(HardenedShellPower __instance, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, ref decimal __result)
    {
        __result = amount;
    }
}