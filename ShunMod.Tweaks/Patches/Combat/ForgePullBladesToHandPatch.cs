using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Logging;
namespace ShunMod.Tweaks.Combat;

/// <summary>
///     所有 Forge 行为自动将非手牌的君王之剑拉回手牌。
/// </summary>
[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge))]
public static class ForgePullBladesToHandPatch
{
    [HarmonyPostfix]
    private static async void Postfix(Task<IEnumerable<SovereignBlade>> __result, Player player)
    {
        try
        {
            await __result;

            if (player.PlayerCombatState == null) return;
            if (CombatManager.Instance?.IsOverOrEnding != false) return;

            var blades = player.PlayerCombatState.AllCards
                .OfType<SovereignBlade>()
                .Where(b => b.Pile != null && b.Pile.Type != PileType.Hand)
                .ToList();

            foreach (var blade in blades) await CardPileCmd.Add(blade, PileType.Hand);
        }
        catch (Exception ex)
        {
            Log.Error($"[锻造拉回] {ex.GetType().Name}: {ex.Message}");
        }
    }
}