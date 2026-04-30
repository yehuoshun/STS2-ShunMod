using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 神化 (Apotheosis) 增强 — 额外升级牌组中所有卡牌
/// </summary>
[HarmonyPatch(typeof(Apotheosis), nameof(Apotheosis.OnPlay))]
public static class ApotheosisPatch
{
    static void Prefix(Apotheosis __instance)
    {
        var deckCards = PileType.Deck.GetPile(__instance.Owner).Cards
            .Where(c => c is not null && c.IsUpgradable)
            .ToList();

        foreach (var card in deckCards)
        {
            CardCmd.Upgrade(card, CardPreviewStyle.None);
        }
    }
}
