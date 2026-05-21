using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

/// <summary>
///     君王之剑（SovereignBlade）增强补丁。
///     1. 唯一 — 不可复制
///     2. 不可消耗
///     3. 任何牌打出后，若君王之剑在任意牌堆中则抽到手牌
/// </summary>
internal static class SovereignBladePatch
{
    // ══════════════════════════ 禁止消耗 ══════════════════════════

    [HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust), typeof(PlayerChoiceContext), typeof(CardModel),
        typeof(bool), typeof(bool))]
    [HarmonyPrefix]
    private static bool Exhaust_Prefix(CardModel card)
    {
        if (card is SovereignBlade)
            return false; // 不可消耗
        return true;
    }

    // ══════════════════════════ 禁止复制 ══════════════════════════

    [HarmonyPatch(typeof(CardCmd), "Duplicate")]
    [HarmonyPrefix]
    private static bool Duplicate_Prefix(CardModel card)
    {
        if (card is SovereignBlade)
            return false; // 不可复制
        return true;
    }

    // ══════════════════════════ 任何牌打出后抽回 ══════════════════════════

    /// <summary>
    ///     Patch CardModel.PlayCard — 任何卡牌打出后，搜索所有牌堆中的 SovereignBlade 并抽回。
    /// </summary>
    [HarmonyPatch(typeof(CardModel), "PlayCard")]
    [HarmonyPostfix]
    private static async void PlayCard_Postfix(CardModel __instance)
    {
        var owner = __instance.Owner;
        if (owner == null) return;
        if (__instance is SovereignBlade) return; // 自己打出不管

        // 在所有牌堆中找君王之剑
        SovereignBlade? blade = null;
        foreach (var pileType in new[] { PileType.Draw, PileType.Discard, PileType.Hand })
        {
            var pile = pileType.GetPile(owner);
            if (pile == null) continue;
            foreach (var c in pile.Cards)
            {
                if (c is SovereignBlade sb)
                {
                    blade = sb;
                    break;
                }
            }
            if (blade != null) break;
        }

        if (blade == null || blade.Pile?.Type == PileType.Hand) return;

        ShunLogger.Debug("君王之剑", $"从 {blade.Pile?.Type} 抽回手牌");
        await CardPileCmd.Add(blade, PileType.Hand);
    }
}