using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core.Core.Helpers;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance 约定

/// <summary>
///     护盾超限记账（获得侧）— 护盾达到阈值后把整块超出部分转存为「超限」层数。
///     GainBlockInternal 是所有护盾获得的唯一漏斗（原版 + 全部 mod 的卡），
///     补丁这一处即可全覆盖，无需逐个适配第三方模组。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.GainBlockInternal))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class OverflowGainBlockPatch
{
    [HarmonyPostfix]
    private static void Postfix(Creature __instance)
    {
        try
        {
            if (!OverflowConfig.Enabled) return;
            if (__instance.CombatState is not { } combatState) return; // 非战斗场景不记账
            if (combatState.Players.Count > 1) return; // 多人模式无法同步补丁侧的 Power 叠加，跳过（游戏自带 clamp 兜底）
            if (!__instance.CanReceivePowers) return;
            if (__instance.Block < OverflowConfig.Threshold) return; // 未达阈值

            BankOverflow(__instance);
        }
        catch (Exception ex)
        {
            Log.Error($"[ShunMod_Tweaks/超限] 护盾记账失败: {ex.GetType().Name}: {ex.Message}");
        }
    }

    /// <summary>
    ///     把护盾中达到阈值整倍数的部分转存为层数，余数留在护盾。
    ///     数值守恒：转换前后 护盾 + 层数 × 阈值 严格相等。
    /// </summary>
    private static void BankOverflow(Creature creature)
    {
        int stacks = creature.Block / OverflowConfig.Threshold;
        if (stacks <= 0) return;

        // Block setter 不同版本可见性不同（0.107.1 public / 新版 private），
        // 统一走反射写（同 BlockRetentionPatch 惯例）
        CreatureReflection.SetBlock(creature, creature.Block % OverflowConfig.Threshold);

        OverflowPower? power = creature.GetPower<OverflowPower>();
        if (power != null)
        {
            power.SetAmount(power.Amount + stacks, silent: true);
            return;
        }

        // 有意绕过 PowerCmd.Apply：这是内部记账，不是「获得」Power，
        // 不应触发 Hook 修正 / 遗物联动 / 多人消息。silent 避免闪烁刷屏。
        OverflowPower canonical = ModelDb.Power<OverflowPower>();
        OverflowPower mutable = (OverflowPower)canonical.ToMutable();
        mutable.Applier = null;
        mutable.ApplyInternal(creature, stacks, silent: true);
    }
}