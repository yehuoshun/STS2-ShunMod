using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace STS2_ShunMod.Extensions;

/// <summary>
/// DynamicVar 扩展方法 — 升级值存储。
/// </summary>
public static class DynamicVarExtensions
{
    public static readonly SpireField<DynamicVar, decimal?> DynamicVarUpgrades = new(() => null);

    public static TDynamicVar WithUpgrade<TDynamicVar>(this TDynamicVar dynamicVar, decimal upgradeValue)
        where TDynamicVar : DynamicVar
    {
        if (upgradeValue != 0) DynamicVarUpgrades[dynamicVar] = upgradeValue;
        return dynamicVar;
    }
}

/// <summary>
/// Clone 时复制升级值。
/// </summary>
[HarmonyPatch(typeof(DynamicVar), nameof(DynamicVar.Clone))]
file class CloneDynamicVarUpgrades
{
    [HarmonyPostfix]
    static void Copy(DynamicVar __result, DynamicVar __instance)
    {
        DynamicVarExtensions.DynamicVarUpgrades[__result] =
            DynamicVarExtensions.DynamicVarUpgrades[__instance];
    }
}
