using System;
using System.Collections.Generic;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Tweaks.PowersPersist.Config;
using ShunMod.Tweaks.PowersPersist.State;

namespace ShunMod.Tweaks.PowersPersist.Patches;

/// <summary>
///     Snapshot Player.Creature.Powers right before the end-of-combat clear
///     (Player.AfterCombatEnd -> Creature.RemoveAllPowersInternalExcept), and
///     re-apply the snapshot for each player at the start of the next combat
///     (CombatManager.SetUpCombat Postfix).
///     Both filter toggles are applied at snapshot time, not reapply time, so
///     that toggling the filter mid-run takes effect on the next end-of-combat.
/// </summary>
internal static class PersistPowersPatch
{
    [HarmonyPatch(typeof(Player), nameof(Player.AfterCombatEnd))]
    internal static class SnapshotPowersOnCombatEnd
    {
        // ReSharper disable once UnusedMember.Local
        public static void Prefix(Player __instance)
        {
            try
            {
                var snapshot = new List<PersistedPower>();
                foreach (var power in __instance.Creature.Powers)
                {
                    if (PowersPersistConfig.SkipNegativePowers
                        && power.TypeForCurrentAmount == PowerType.Debuff)
                        continue;

                    if (PowersPersistConfig.SkipNonCombatOriginPowers
                        && PersistTracker.IsEventOrigin(__instance.NetId, power.Id))
                        continue;

                    snapshot.Add(new PersistedPower(power.Id, power.Amount));
                }

                PersistTracker.SetSnapshot(__instance.NetId, snapshot);
                PersistTracker.ClearOriginsFor(__instance.NetId);
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] failed to snapshot powers for player {__instance.NetId}: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(CombatManager), nameof(CombatManager.SetUpCombat))]
    internal static class ReapplyPowersOnCombatStart
    {
        // ReSharper disable once UnusedMember.Local
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
                Log.Error($"[PowersPersist] failed to reapply powers: {ex}");
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
                        Log.Warn($"[PowersPersist] dropped persisted power {snap.Id}; not found in ModelDb (mod removed?).");
                        continue;
                    }

                    if (player.Creature.HasPower(snap.Id))
                    {
                        // Already on the creature (canonical player powers like
                        // Crimson Mantle that re-apply themselves at combat start).
                        // Don't double-stack.
                        continue;
                    }

                    var power = canonical.ToMutable();
                    try
                    {
                        // Bypasses PowerCmd.Apply on purpose: re-applying from
                        // persistence is not "gaining" the power, so we don't
                        // want to retrigger Hook.Before/AfterPowerAmountChanged,
                        // on-apply relic effects, or History.PowerReceived.
                        power.ApplyInternal(player.Creature, snap.Amount, silent: true);

                        // Re-tag as Battle origin so the next snapshot still
                        // counts this power as combat-originated under the
                        // SkipNonCombatOriginPowers filter.
                        PersistTracker.TagOrigin(player.NetId, snap.Id, PowerOrigin.Battle);
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[PowersPersist] failed to reapply {snap.Id} on player {player.NetId}: {ex}");
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