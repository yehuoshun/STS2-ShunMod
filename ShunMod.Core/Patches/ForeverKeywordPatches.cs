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
    //  AddInternal 是所有堆操作的最底层方法。
    //  Prefix 返回 false 跳过原方法，手动加到 Hand 堆。
    // ═══════════════════════════════════════════════════════════
    [HarmonyPatch(typeof(CardPile), nameof(CardPile.AddInternal))]
    [HarmonyPrefix]
    static bool AddInternal_Prefix(CardPile __instance, CardModel card)
    {
        // 递归保护
        if (_isRedirecting) return true;
        // 没有"永远"词条 → 放行
        if (!CustomKeywordRegistry.HasKeyword(card, ForeverId)) return true;
        // 非战斗状态 → 放行（卡牌组构建等）
        if (!CombatManager.Instance.IsInProgress) return true;
        // 目标已经是手牌或被打出中 → 放行
        if (__instance.Type == PileType.Hand || __instance.Type == PileType.Play) return true;

        // 跳转到手牌
        var handPile = PileType.Hand.GetPile(card.Owner);
        _isRedirecting = true;
        handPile.AddInternal(card);
        _isRedirecting = false;
        return false; // 跳过原 AddInternal 调用
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