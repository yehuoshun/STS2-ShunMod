using HarmonyLib;

// ReSharper disable UnusedType.Global — Harmony 反射调用
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     给玩家施加的每个 Power 标记来源：战斗中（Battle）或战斗外（Event）。
///     SkipNonCombatOriginPowers 过滤器靠这个标记来判断。
///     重连期间跳过标记（PersistTracker.IsReapplying 为 true 时不标记），
///     避免战斗开始的重连循环误把一切都标成 Event。
/// </summary>
internal static class PowerOriginPatch
{
    [HarmonyPatch(typeof(PowerCmd), nameof(PowerCmd.Apply), new[]
    {
        typeof(PlayerChoiceContext),
        typeof(PowerModel),
        typeof(Creature),
        typeof(decimal),
        typeof(Creature),
        typeof(CardModel),
        typeof(bool),
    })]
    internal static class TagOriginOnApply
    {
        public static void Postfix(PowerModel power, Creature target)
        {
            try
            {
                if (PersistTracker.IsReapplying)
                    return;

                if (!target.IsPlayer || target.Player == null)
                    return;

                var origin = CombatManager.Instance.IsInProgress
                    ? PowerOrigin.Battle
                    : PowerOrigin.Event;

                PersistTracker.TagOrigin(target.Player.NetId, power.Id, origin);
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] 标记 Power 来源失败: {ex}");
            }
        }
    }
}