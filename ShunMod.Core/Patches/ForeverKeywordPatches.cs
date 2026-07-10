using System.Linq;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;
using ShunMod.Core.Core.Registry;

namespace ShunMod.Core.Patches;

/// <summary>
///     【永远】词条核心补丁。
///     效果：拥有"永远"词条的卡牌，任何时刻进入非手牌堆都会立即跳回手牌。
///     但手牌中最多只能有一张"永远"卡牌——如果手牌已有"永远"卡，多出的不再跳回。
/// </summary>
[HarmonyPatch]
public static class ForeverKeywordPatches
{
    private const string ForeverId = "forever";

    [ThreadStatic]
    private static bool _isRedirecting;

    /// <summary>检查玩家手牌中是否已有"永远"卡牌（排除自身）。</summary>
    private static bool HandHasForever(CardModel card)
    {
        var handPile = PileType.Hand.GetPile(card.Owner);
        return handPile.Cards.Any(c =>
            c != card && CustomKeywordRegistry.HasKeyword(c, ForeverId));
    }

    // ═══════════════════════════════════════════════════════════
    //  1. 拦截所有堆添加操作 → 转去手牌
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPostfix]
    static void AddInternal_Postfix(CardPile __instance, CardModel card)
    {
        // 递归保护
        if (_isRedirecting) return;
        // 没有"永远"词条 → 不处理
        if (!CustomKeywordRegistry.HasKeyword(card, ForeverId)) return;
        // 非战斗状态 → 不处理
        if (!CombatManager.Instance.IsInProgress) return;
        // 目标已经是手牌或被正常打出中 → 不处理
        if (__instance.Type == PileType.Hand || __instance.Type == PileType.Play) return;
        // 手牌已有一张"永远"卡牌 → 不跳回，防止无限堆叠
        if (HandHasForever(card)) return;

        // 从当前堆移除，加入手牌
        var handPile = PileType.Hand.GetPile(card.Owner);
        _isRedirecting = true;
        card.RemoveFromCurrentPile();
        handPile.AddInternal(card);
        _isRedirecting = false;
    }

    // ═══════════════════════════════════════════════════════════
    //  2. 卡牌打出后 → 直接返回手牌
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardModel), "GetResultPileType")]
    [HarmonyPostfix]
    static void GetResultPileType_Postfix(CardModel __instance, ref PileType __result)
    {
        if (!CustomKeywordRegistry.HasKeyword(__instance, ForeverId)) return;
        if (!CombatManager.Instance.IsInProgress) return;
        // 手牌已有一张"永远"卡牌 → 正常进弃牌堆
        if (HandHasForever(__instance)) return;

        __result = PileType.Hand;
    }
}