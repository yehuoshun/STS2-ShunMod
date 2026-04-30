using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using System;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShunMod;

[HarmonyPatch(typeof(HardenedShellPower), "ModifyHpLostBeforeOstyLate")]
public static class HardenedShellPowerPatch
{
    static void Postfix(HardenedShellPower __instance, Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, ref decimal __result)
    {
        __result = amount;
    }
}