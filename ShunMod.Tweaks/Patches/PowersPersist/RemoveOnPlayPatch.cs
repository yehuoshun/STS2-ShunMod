using System.Diagnostics.CodeAnalysis;

// ReSharper disable UnusedType.Global — Harmony 反射调用
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     当 RemovePowerCardsOnPlay 开关开启时，打出 Power 卡后将其牌组版本从运行牌组中移除。
///     使用 CardPileCmd.RemoveFromDeck，让运行历史记录和 BeforeCardRemoved 钩子正常触发。
///     通过链式 continuation 在异步 OnPlayWrapper 完成后执行。
/// </summary>
internal static class RemoveOnPlayPatch
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    internal static class RemovePowerCardOnPlay
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "RedundantAssignment")]
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
                // 战斗中生成的卡，或已被其他机制（如 SwipePower/Guilty）从牌组移除。
                return;
            }

            try
            {
                await CardPileCmd.RemoveFromDeck(deckVersion, showPreview: false);
            }
            catch (Exception ex)
            {
                Log.Error($"[PowersPersist] 打出后从牌组移除 {card.Id} 失败: {ex}");
            }
        }
    }
}