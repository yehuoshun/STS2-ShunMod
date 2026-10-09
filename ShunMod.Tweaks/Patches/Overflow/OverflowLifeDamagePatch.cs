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
///     超限生命（出账：致命伤拆命）— 受到会打空当前生命的一击时，
///     自动消耗一条命回满血，直到能挡住或命尽。
///     Prefix 先行回血，原始 LoseHpInternal 的死亡判定（amount >= CurrentHp）
///     自然失效，无需改动结算逻辑。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowLifeDamagePatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature __instance, decimal amount)
    {
        try
        {
            if (!OverflowLifeConfig.Enabled) return;
            if (__instance.MaxHp <= 0) return;
            if (amount < __instance.CurrentHp) return; // 非致命伤，不动
            if (amount <= 0m) return;

            OverflowLifePower? power = __instance.GetPower<OverflowLifePower>();
            if (power == null || power.Amount <= 0) return;

            // 循环拆命：一击可能远超一管血（如 50 亿伤害对 9.99 亿血）
            // amount 固定，循环必然终止（命尽或挡得住）
            while (amount >= __instance.CurrentHp && power.Amount > 0)
            {
                __instance.SetCurrentHpInternal(__instance.MaxHp); // 回满
                power.SetAmount(power.Amount - 1, silent: true);
            }
        }
        catch (Exception ex)
        {
            // 拆命失败只丢命，不阻断伤害结算
            Log.Error($"[ShunMod_Tweaks/超限生命] 拆命失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}