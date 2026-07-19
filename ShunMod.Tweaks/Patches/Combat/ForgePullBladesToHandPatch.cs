using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models.Cards;

namespace ShunMod.Tweaks.Patches.Combat;

// 所有 Forge 行为自动将非手牌的君王之剑拉回手牌
[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class ForgePullBladesToHandPatch
{
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __result 约定")]
    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static async void Postfix(Task<IEnumerable<SovereignBlade>> __result, Player player)
    {
        try
        {
            await __result;

            if (player.PlayerCombatState == null) return;
            if (CombatManager.Instance.IsOverOrEnding) return;

            var bladesToPull = player.PlayerCombatState.AllCards
                .OfType<SovereignBlade>()
                .Where(b => b.Pile != null && b.Pile.Type != PileType.Hand)
                .ToList();

            foreach (var blade in bladesToPull)
                await CardPileCmd.Add(blade, PileType.Hand);
        }
        catch (Exception ex)
        {
            Log.Error($"[锻造拉回] {ex.GetType().Name}: {ex.Message}");
        }
    }
}