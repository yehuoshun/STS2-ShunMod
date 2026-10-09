using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.ValueProps;
using ShunMod.Core.Core.Helpers;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance 约定

/// <summary>
///     护盾超限记账（消耗侧）— 受伤时护盾不足，自动把「超限」层数兑回护盾抵扣。
///     Prefix 先行「拆层补盾」，原始 DamageBlockInternal 结算逻辑零改动。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.DamageBlockInternal))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowDamageBlockPatch
{
    [HarmonyPrefix]
    private static void Prefix(Creature __instance, decimal amount, ValueProp props)
    {
        try
        {
            if (!OverflowConfig.Enabled) return;
            if (props.HasFlag(ValueProp.Unblockable)) return; // 不可阻挡伤害与护盾无关
            if (amount <= 0m) return;

            OverflowPower? power = __instance.GetPower<OverflowPower>();
            if (power == null || power.Amount <= 0) return;

            // 护盾不足就逐层兑回（1 层 = 阈值），直到够挡或层数耗尽
            while (__instance.Block < amount && power.Amount > 0)
            {
                // Block setter 版本差异，统一反射写（同 BlockRetentionPatch 惯例）
                CreatureReflection.SetBlock(__instance, __instance.Block + OverflowConfig.Threshold);
                power.SetAmount(power.Amount - 1, silent: true);
            }
        }
        catch (Exception ex)
        {
            // 拆层失败只影响记账展示，不阻断伤害结算（原始方法照常执行）
            Log.Error($"[ShunMod_Tweaks/超限] 拆层抵扣失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}