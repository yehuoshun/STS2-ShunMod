using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance 约定

/// <summary>
///     超限生命（入账：治疗溢出）— 满血或治疗量超出缺口时，
///     溢出部分按「每管血（=当前 MaxHp）一条命」折算存入「超限生命」。
///     解决数值爆炸后满血回血全部浪费的问题。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.HealInternal))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowLifeHealPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature __instance, ref int __state)
    {
        // 记录治疗前的血量，postfix 计算溢出
        __state = __instance.CurrentHp;
    }

    [HarmonyPostfix]
    private static void Postfix(Creature __instance, decimal amount, int __state)
    {
        try
        {
            if (!OverflowLifeConfig.Enabled) return;
            if (__instance.MaxHp <= 0) return;
            if (amount <= 0m) return;

            // 溢出 = 治疗量 - 缺口（MaxHp - 治疗前血量）
            decimal overflow = (decimal)__state + amount - __instance.MaxHp;
            if (overflow <= 0m) return;

            // 每管血 = 当前 MaxHp，整除存命，余数丢弃
            int lives = (int)(overflow / __instance.MaxHp);
            if (lives <= 0) return;

            OverflowLifePower? power = __instance.GetPower<OverflowLifePower>();
            if (power != null)
            {
                power.SetAmount(power.Amount + lives, silent: true);
                return;
            }

            OverflowLifePower canonical = ModelDb.Power<OverflowLifePower>();
            OverflowLifePower mutable = (OverflowLifePower)canonical.ToMutable();
            mutable.Applier = null;
            mutable.ApplyInternal(__instance, lives, silent: true);
        }
        catch (Exception ex)
        {
            // 记账失败只丢命，不影响治疗本身
            Log.Error($"[ShunMod_Tweaks/超限生命] 治疗溢出存命失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}