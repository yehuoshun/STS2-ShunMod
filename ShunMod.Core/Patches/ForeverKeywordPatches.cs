using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using ShunMod.Core;

namespace ShunMod.Core.Patches;

/// <summary>
///     【永远】词条核心补丁。
///     效果：拥有"永远"词条的卡牌，任何时刻进入非手牌堆都会立即跳回手牌。
///     （打出、消耗、洗回抽牌堆、弃牌——全部拦截。）
/// </summary>
[HarmonyPatch]
public static class ForeverKeywordPatches
{
    private const string ForeverId = "forever";

    [ThreadStatic]
    private static bool _isRedirecting;

    // ═══════════════════════════════════════════════════════════
    //  1. 拦截所有堆添加操作 → 转去手牌
    // ═══════════════════════════════════════════════════════════
    //  必须用 Postfix：Prefix 破坏 CardPileCmd.Add 的内部状态追踪
    //  （卡被从原堆移除但未加进目标堆，后续 hook/视觉更新全乱）。
    //  Postfix 在卡牌已加入目标堆后立即转移到手牌，CardPileCmd.Add
    //  的流程不受影响。
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPostfix]
    static void AddInternal_Postfix(CardPile __instance, CardModel card)
    {
        // 递归保护
        if (_isRedirecting) return;
        // 没有"永远"词条 → 不处理
        if (!CustomKeywordRegistry.HasKeyword(card, ForeverId)) return;
        // 非战斗状态 → 不处理（卡牌组构建等）
        if (!CombatManager.Instance.IsInProgress) return;
        // 目标已经是手牌或被正常打出中 → 不处理
        if (__instance.Type == PileType.Hand || __instance.Type == PileType.Play) return;

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
    //  配合 AddInternal 拦截，确保 OnPlayWrapper 的 resultPile
    //  不会把卡牌送到弃牌/消耗堆。
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardModel), "GetResultPileType")]
    [HarmonyPostfix]
    static void GetResultPileType_Postfix(CardModel __instance, ref PileType __result)
    {
        if (!CustomKeywordRegistry.HasKeyword(__instance, ForeverId)) return;
        if (!CombatManager.Instance.IsInProgress) return;

        __result = PileType.Hand;
    }
}