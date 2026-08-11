using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     战斗结束时快照 Player.Creature.Powers（在清除之前），
///     下一场战斗开始时重新应用快照。
///     两个过滤器开关在快照时生效，而不是重连时，所以战斗中切换开关
///     会在下一次战斗结束时生效。
/// </summary>
internal static class PersistPowersPatch
{
    [HarmonyPatch(typeof(Player), nameof(Player.AfterCombatEnd))]
    internal static class SnapshotPowersOnCombatEnd
    {
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __instance 约定")]
        [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
        public static void Prefix(Player __instance)
        {
            try
            {
                var snapshot = __instance.Creature.Powers
                .Where(power => !(PowersPersistConfig.SkipNegativePowers
                                  && power.TypeForCurrentAmount == PowerType.Debuff))
                .Where(power => !(PowersPersistConfig.SkipNonCombatOriginPowers
                                  && PersistTracker.IsEventOrigin(__instance.NetId, power.Id)))
                .Select(power => new PersistedPower(power.Id, power.Amount))
                .ToList();

                PersistTracker.SetSnapshot(__instance.NetId, snapshot);
                PersistTracker.ClearOriginsFor(__instance.NetId);
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] 快照玩家 {__instance.NetId} 的 Power 失败: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    internal static class ReapplyPowersOnCombatStart
    {
        [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
        public static void Postfix(CombatState state)
        {
            try
            {
                foreach (var player in state.Players)
                {
                    var snapshot = PersistTracker.TakeSnapshot(player.NetId);
                    if (snapshot == null || snapshot.Count == 0)
                        continue;

                    ReapplyForPlayer(player, snapshot);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] 重连 Power 失败: {ex}");
            }
        }

        private static void ReapplyForPlayer(Player player, List<PersistedPower> snapshot)
        {
            PersistTracker.IsReapplying = true;
            try
            {
                foreach (var snap in snapshot)
                {
                    if (snap.Amount == 0)
                        continue;

                    var canonical = ModelDb.GetByIdOrNull<PowerModel>(snap.Id);
                    if (canonical == null)
                    {
                        Log.Warn($"[PowersPersist] 跳过持久化 Power {snap.Id}：ModelDb 中未找到（mod 被移除？）。");
                        continue;
                    }

                    if (player.Creature.HasPower(snap.Id))
                    {
                        // 已在 Creature 上（如 Crimson Mantle 等战斗开始时自动重连的专属 Power），
                        // 不要重复叠加。
                        continue;
                    }

                    var power = canonical.ToMutable();
                    try
                    {
                        // 有意绕过 PowerCmd.Apply：从持久化重连不算"获得"Power，
                        // 不应触发 Hook.Before/AfterPowerAmountChanged、遗物效果或 History.PowerReceived。
                        power.ApplyInternal(player.Creature, snap.Amount, silent: true);

                        // 重新标记为 Battle 来源，使下次快照仍能正确过滤。
                        PersistTracker.TagOrigin(player.NetId, snap.Id, PowerOrigin.Battle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[PowersPersist] 重连 {snap.Id} 到玩家 {player.NetId} 失败: {ex}");
                    }
                }
            }
            finally
            {
                PersistTracker.IsReapplying = false;
            }
        }
    }
}