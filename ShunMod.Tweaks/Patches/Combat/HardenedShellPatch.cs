using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Powers;

namespace ShunMod.Tweaks.Patches.Combat;

// 修复硬化外壳：直接返回原始伤害值，取消减伤
[HarmonyPatch(typeof(HardenedShellPower), "ModifyHpLostBeforeOstyLate")]
public static class HardenedShellPatch
{
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __result 约定")]
    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    [SuppressMessage("ReSharper", "Spelling", Justification = "Osty 是游戏内部命名")]
    private static void Postfix(decimal amount, out decimal __result)
    {
        __result = amount;
    }
}