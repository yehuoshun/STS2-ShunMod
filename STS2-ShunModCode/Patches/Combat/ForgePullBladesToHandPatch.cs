using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_ShunMod.Patches;

/// <summary>
///     锻造后自动将所有非手牌的 SovereignBlade 拉回手牌。
///     原先"征召上前"独占此能力，现在所有 Forge 行为统一拥有。
/// </summary>
[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge))]
public static class ForgePullBladesToHandPatch
{
    /// <summary>
    ///     异步后置拦截 — 等待原 Forge 完成后，回收 SovereignBlade 到手牌。
    /// </summary>
    [HarmonyPostfix]
    private static async void Postfix(Task<IEnumerable<SovereignBlade>> __result, Player player)
    {
        try
        {
            await __result;

            if (player.PlayerCombatState == null)
                return;

            if (CombatManager.Instance?.IsOverOrEnding != false)
                return;

            var blades = player.PlayerCombatState.AllCards
                .OfType<SovereignBlade>()
                .Where(b =>
                {
                    var pile = b.Pile;
                    return pile != null && pile.Type != PileType.Hand;
                })
                .ToList();

            foreach (var blade in blades) await CardPileCmd.Add(blade, PileType.Hand);

            if (blades.Count > 0)
                Log.Info($"[锻造拉回] 回收 {blades.Count} 张 SovereignBlade 到手牌");
        }
        catch (Exception ex)
        {
            Log.Error($"[锻造拉回] {ex.GetType().Name}: {ex.Message}");
        }
    }
}