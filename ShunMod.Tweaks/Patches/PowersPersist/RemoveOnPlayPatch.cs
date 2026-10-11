using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.PowersPersist;

/// <summary>
///     当 RemovePowerCardsOnPlay 开关开启时，打出 Power 卡后将其牌组版本从运行牌组中移除。
/// </summary>
[SuppressMessage("ReSharper", "UnusedType.Global", Justification = "Harmony 反射扫描嵌套类")]
internal static class RemoveOnPlayPatch
{
    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    internal static class RemovePowerCardOnPlay
    {
        [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __instance/__result 约定")]
        [SuppressMessage("ReSharper", "RedundantAssignment", Justification = "Harmony ref __result 覆盖")]
        [SuppressMessage("ReSharper", "UnusedMember.Global", Justification = "Harmony 反射调用")]
        public static void Postfix(CardModel __instance, ref Task __result)
        {
            var original = __result;
            __result = ChainRemoveFromDeck(original, __instance);
        }

        private static async Task ChainRemoveFromDeck(Task original, CardModel card)
        {
            try
            {
                await original;
            }
            catch (Exception ex)
            {
                // 第三方 mod 在卡牌打出流程（OnPlayWrapper → Hook.AfterCardPlayed 等）抛出的异常，
                // 若不拦截会沿 task 链传播 → 回合流程等不到完成 → 整场战斗卡死。
                // 这里隔离异常只记日志，保证替换后的 __result 永远正常完成。
                Log.Error($"[PowersPersist] 卡牌打出流程异常（可能来自第三方 mod），已隔离防止战斗卡死: {ex}");
                return;
            }

            if (!PowersPersistConfig.RemovePowerCardsOnPlay)
                return;

            if (card.Type != CardType.Power)
                return;

            var deckVersion = card.DeckVersion;
            if (deckVersion?.Pile?.Type != PileType.Deck)
            {
                // 战斗中生成的卡，或已被其他机制从牌组移除。
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