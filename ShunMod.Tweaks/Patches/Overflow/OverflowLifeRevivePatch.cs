using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __result 约定

/// <summary>
///     超限生命（出账：濒死复活）— 死亡判定（Hook.ShouldDie）时消耗一条命回满并阻止死亡。
///     与原版复活机制（Buffer/仙灵瓶等）同层：它们先拦截，命银行兜底；
///     不再前置拦截 LoseHpInternal，避免抢掉其它复活/死亡联动机制的触发机会。
///     force 强制死亡（放弃 run 等）不走 ShouldDie，命银行不挡。
/// </summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.ShouldDie))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowLifeRevivePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result, Creature creature)
    {
        try
        {
            if (!OverflowLifeConfig.Enabled) return;
            if (!__result) return; // 已有机制阻止了死亡（Buffer/仙灵等），不消耗命

            OverflowLifePower? power = creature.GetPower<OverflowLifePower>();
            if (power == null || power.Amount <= 0) return;
            if (creature.MaxHp <= 0) return;

            // 濒死：消耗一条命回满，阻止死亡
            creature.SetCurrentHpInternal(creature.MaxHp);
            power.SetAmount(power.Amount - 1, silent: true);
            __result = false;
        }
        catch (Exception ex)
        {
            // 复活失败只丢一条命，不阻断原有死亡流程
            Log.Error($"[ShunMod_Tweaks/超限生命] 濒死复活失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}