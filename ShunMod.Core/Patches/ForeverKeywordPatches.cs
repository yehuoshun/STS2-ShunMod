using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;

namespace ShunMod.Core.Patches;

/// <summary>
///     【永远】词条核心补丁。
///     效果：拥有"永远"词条的卡牌，在任何时候都会自动回到手牌。
/// </summary>
[HarmonyPatch]
public static class ForeverKeywordPatches
{
    private const string ForeverId = "forever";

    // ═══════════════════════════════════════════════════════════
    //  1. 卡牌打出后 → 直接返回手牌
    // ═══════════════════════════════════════════════════════════
    //  GetResultPileType() 决定卡牌打出后去哪个堆。
    //  对"永远"卡牌返回 Hand，让卡牌打出后直接回到手牌。
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardModel), "GetResultPileType")]
    [HarmonyPostfix]
    static void GetResultPileType_Postfix(CardModel __instance, ref PileType __result)
    {
        if (!CustomKeywordRegistry.HasKeyword(__instance, ForeverId))
            return;
        if (!CombatManager.Instance.IsInProgress)
            return;

        __result = PileType.Hand;
    }

    // ═══════════════════════════════════════════════════════════
    //  2. 回合结束时→ 从所有非手牌堆回到手牌
    // ═══════════════════════════════════════════════════════════
    //  EndPlayerTurnPhaseTwoInternal 结束后，扫描抽牌堆/弃牌堆/消耗堆，
    //  将"永远"卡牌移回手牌。
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CombatManager), "EndPlayerTurnPhaseTwoInternal")]
    [HarmonyPostfix]
    static async Task EndPlayerTurn_Postfix(CombatManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null) return;

        foreach (var player in state.Players)
        {
            // 收集所有非手牌堆中的"永远"卡牌
            var foreverCards = new List<CardModel>();

            void ScanPile(PileType pileType)
            {
                var pile = pileType.GetPile(player);
                if (pile == null) return;
                foreach (var card in pile.Cards.ToList())
                {
                    if (CustomKeywordRegistry.HasKeyword(card, ForeverId))
                        foreverCards.Add(card);
                }
            }

            ScanPile(PileType.Draw);
            ScanPile(PileType.Discard);
            ScanPile(PileType.Exhaust);

            if (foreverCards.Count == 0) continue;

            // 移回手牌
            var handPile = PileType.Hand.GetPile(player);
            foreach (var card in foreverCards)
            {
                if (card.Pile?.Type == PileType.Hand) continue;

                card.RemoveFromCurrentPile();
                handPile.AddInternal(card);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  3. 战斗开始时→ 将抽牌堆中的"永远"卡牌移入手牌
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CombatManager), "StartTurn")]
    [HarmonyPostfix]
    static async Task StartTurn_Postfix(CombatManager __instance)
    {
        var state = __instance.DebugOnlyGetState();
        if (state == null) return;

        // 只在玩家回合开始时检查
        if (state.CurrentSide != CombatSide.Player) return;

        foreach (var player in state.Players)
        {
            var drawPile = PileType.Draw.GetPile(player);
            if (drawPile == null) continue;

            var foreverCards = drawPile.Cards
                .Where(c => CustomKeywordRegistry.HasKeyword(c, ForeverId))
                .ToList();

            if (foreverCards.Count == 0) continue;

            var handPile = PileType.Hand.GetPile(player);
            foreach (var card in foreverCards)
            {
                card.RemoveFromCurrentPile();
                handPile.AddInternal(card);
            }
        }
    }
}