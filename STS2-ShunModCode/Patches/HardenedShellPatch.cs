using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 修复硬化外壳能力 — 使 ModifyHpLostBeforeOstyLate 返回原始伤害值，取消减伤效果。
/// </summary>
/// <remarks>
/// 原游戏该能力的减伤计算存在异常，此补丁将减伤前的伤害值原样返回，
/// 通过 Harmony Postfix 覆盖 __result。
/// </remarks>
[HarmonyPatch(typeof(HardenedShellPower), "ModifyHpLostBeforeOstyLate")]
public static class HardenedShellPowerPatch
{
    /// <summary>
    /// 后置拦截 — 将结果强制设为原始 amount 值，跳过减伤计算。
    /// </summary>
    /// <param name="__instance">硬化外壳能力实例</param>
    /// <param name="target">受伤目标</param>
    /// <param name="amount">原始伤害值</param>
    /// <param name="props">数值属性</param>
    /// <param name="dealer">伤害来源</param>
    /// <param name="cardSource">来源卡牌</param>
    /// <param name="__result">方法返回值（被本方法覆盖）</param>
    static void Postfix(HardenedShellPower __instance, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource, ref decimal __result)
    {
        __result = amount;
    }
}
