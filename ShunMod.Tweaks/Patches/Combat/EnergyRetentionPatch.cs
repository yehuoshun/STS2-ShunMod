using HarmonyLib;
using MegaCrit.Sts2.Core.Hooks;
namespace ShunMod.Tweaks.Patches.Combat;

// ReSharper disable UnusedMember.Local — Harmony 反射调用
// 能量保留：回合开始能量不清零，始终返回 false
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldPlayerResetEnergy))]
public static class EnergyRetentionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(out bool __result)
    {
        __result = false;
        return false;
    }
}