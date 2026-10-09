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
///     超限生命（入账：生命上限溢出）— 生命上限增长顶到游戏数值上限
///     （999,999,999）后仍继续增加时，被 clamp 吞掉的部分按
///     「每管血（=当前 MaxHp）一条命」折算存入「超限生命」，不白丢。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.SetMaxHpInternal))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowLifeMaxHpPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance, decimal amount)
    {
        try
        {
            if (!OverflowLifeConfig.Enabled) return;
            if (__instance.MaxHp <= 0) return;

            // 请求的新上限超过游戏 clamp 值，超出部分被吞 → 转命
            const int gameCap = 999_999_999;
            if (amount <= gameCap) return;
            if (__instance.MaxHp != gameCap) return; // 理论上 clamp 后必等于 cap，防御性判断

            decimal overflow = amount - gameCap;
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
            Log.Error($"[ShunMod_Tweaks/超限生命] 上限溢出存命失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}