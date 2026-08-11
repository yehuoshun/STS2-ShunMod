using System;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Tweaks.PowersPersist.Config;

namespace ShunMod.Tweaks.PowersPersist.Patches;

/// <summary>
///     When the RemovePowerCardsOnPlay toggle is on, remove a played power
///     card's deck version from the run deck after the play resolves. Uses the
///     canonical CardPileCmd.RemoveFromDeck so the run-history "cards removed"
///     log and any BeforeCardRemoved hooks fire normally.
///     Patches the async OnPlayWrapper by chaining a continuation onto the
///     returned Task; Harmony postfixes on async methods only run at task
///     kickoff, so __result wrapping is the standard pattern for "after the
///     async work actually completes".
/// </summary>
internal static class RemoveOnPlayPatch
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    internal static class RemovePowerCardOnPlay
    {
        // ReSharper disable once UnusedMember.Local
        public static void Postfix(CardModel __instance, ref Task __result)
        {
            var original = __result;
            __result = ChainRemoveFromDeck(original, __instance);
        }

        private static async Task ChainRemoveFromDeck(Task original, CardModel card)
        {
            await original;

            if (!PowersPersistConfig.RemovePowerCardsOnPlay)
                return;

            if (card.Type != CardType.Power)
                return;

            var deckVersion = card.DeckVersion;
            if (deckVersion == null || deckVersion.Pile == null
                || deckVersion.Pile.Type != PileType.Deck)
            {
                // Card was generated mid-combat, or has already been removed
                // from the deck by something else (e.g. SwipePower/Guilty).
                return;
            }

            try
            {
                await CardPileCmd.RemoveFromDeck(deckVersion, showPreview: false);
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] failed to remove {card.Id} from deck after play: {ex}");
            }
        }
    }
}