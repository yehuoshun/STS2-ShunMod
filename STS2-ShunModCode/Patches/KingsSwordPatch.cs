using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     君王之剑 — 全局效果补丁。
///     1. 禁止消化/复制
///     2. 任何卡牌打出后，若君王之剑在任意牌堆中则抽到手牌
/// </summary>
internal static class KingsSwordPatch
{
    // ══════════════════════════ 禁止消化 ══════════════════════════

    /// <summary>拦截 CardCmd.Exhaust，君王之剑不可消化</summary>
    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust), typeof(PlayerChoiceContext), typeof(CardModel),
        typeof(bool), typeof(bool))]
    [HarmonyPrefix]
    private static bool Exhaust_Prefix(PlayerChoiceContext ctx, CardModel card, bool causedByEthereal, bool skipVisuals)
    {
        if (card is ShunModKingsSword)
        {
            ShunLogger.Debug("君王之剑", "拦截消化");
            return false; // 跳过消化
        }

        return true;
    }

    // ══════════════════════════ 任何牌打出后抽回 ══════════════════════════

    /// <summary>
    ///     Patch 所有 CardModel 子类的 OnPlayCore 内部调用的 AfterCardPlayed。
    ///     直接 Patch CardModel.PlayCard 的末尾段太复杂，改为 Patch
    ///     CombatManager 的 CardPlayFinished 之后的流程。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "PlayCard")]
    [HarmonyPostfix]
    private static async void PlayCard_Postfix(CardModel __instance)
    {
        // 仅处理打出者是自己拥有者的情况
        var owner = __instance.Owner;
        if (owner == null) return;

        // 跳过君王之剑自己
        if (__instance is ShunModKingsSword) return;

        // 在持有者的所有牌堆中搜索君王之剑
        var card = FindKingsSword(owner);
        if (card == null) return;

        // 抽到手牌
        if (card.Pile != null && card.Pile.Type != PileType.Hand)
            await CardPileCmd.Add(card, PileType.Hand);
    }

    // ══════════════════════════ 禁止复制 ══════════════════════════

    /// <summary>拦截卡片复制指令</summary>
    [HarmonyPatch(typeof(CardCmd), "Duplicate")]
    [HarmonyPrefix]
    private static bool Duplicate_Prefix(CardModel card)
    {
        if (card is ShunModKingsSword)
            return false; // 不可复制

        return true;
    }

    // ══════════════════════════ 工具方法 ══════════════════════════

    private static ShunModKingsSword? FindKingsSword(PlayerModel owner)
    {
        var piles = new[] { PileType.Draw, PileType.Discard, PileType.Exhaust };
        foreach (var pileType in piles)
        {
            var pile = pileType.GetPile(owner);
            if (pile == null) continue;
            foreach (var c in pile.Cards)
                if (c is ShunModKingsSword sword)
                    return sword;
        }
        return null;
    }
}