using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Compat.Patches.Compatibility.Hextech;

// ═══════════════════════════════════════════════════════════════════════════════
// SolidTimeRune.TryGetDeckPower 修改
// 海克斯"凝固时间符文"（SolidTime Rune）：
//   原效果：打出牌组内的能力卡时，将其从牌组中移除。
//   修改后：打出任意能力卡时，存储卡牌信息，战斗开始时触发效果。
//           若该卡在牌组中存在，同时从牌组中移除。
//
// 改动：
//   1. 移除 pile.Type == 6 限制（卡不要求在牌组堆中）
//   2. DeckVersion 为 null（生成卡）时返回 combatCard 本身，让原版
//      AfterCardPlayed 走 AppendStoredCard + Flash
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedMember.Local — Prefix 由 Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance / __result 命名约定
public static class SolidTimeRunePatch
{
    private const string ModId = ModEntry.ModId;

    private static readonly Type? SolidTimeRuneType =
        AccessTools.TypeByName("HextechRunes.SolidTimeRune");

    private static readonly PropertyInfo? OwnerProperty =
        SolidTimeRuneType?.BaseType != null
            ? AccessTools.Property(SolidTimeRuneType.BaseType, "Owner")
            : null;

    private static bool _applied;

    public static void Apply(Harmony harmony)
    {
        if (_applied) return;
        if (SolidTimeRuneType == null)
        {
            Log.Warn("[SolidTimeRunePatch] HextechRunes.SolidTimeRune not found — skipping");
            return;
        }

        var target = AccessTools.Method(SolidTimeRuneType, "TryGetDeckPower");
        if (target == null)
        {
            Log.Warn("[SolidTimeRunePatch] TryGetDeckPower not found — skipping");
            return;
        }

        var prefix = AccessTools.Method(typeof(SolidTimeRunePatch), nameof(Prefix));
        harmony.Patch(target, prefix: new HarmonyMethod(prefix));
        _applied = true;
        Log.Info("[SolidTimeRunePatch] Applied");
    }

    // ReSharper disable RedundantAssignment
    private static bool Prefix(
        object __instance,
        CardModel combatCard,
        ref CardModel? deckCard,
        ref bool __result)
    {
        try
        {
            var owner = OwnerProperty?.GetValue(__instance) as Player;
            if (owner == null)
            {
                deckCard = null;
                __result = false;
                return false;
            }

            // 路径 A：DeckVersion 有值 → 移除 pile.Type == 6 限制
            deckCard = combatCard.DeckVersion;
            if (deckCard != null)
            {
                __result = deckCard.Owner == owner
                    && deckCard.Type == CardType.Power
                    && owner.Deck.Cards.Contains(deckCard);
                return false;
            }

            // 路径 B：DeckVersion 为 null（生成卡）→ 返回 combatCard 本身
            // 原版 AfterCardPlayed 会调 AppendStoredCard(combatCard) + Flash()
            // CardPileCmd.RemoveFromDeck 对不在牌组中的卡为 no-op
            if (combatCard.Type == CardType.Power)
            {
                deckCard = combatCard;
                __result = true;
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SolidTimeRunePatch] {ex.GetType().Name}: {ex.Message}");
        }

        deckCard = null;
        __result = false;
        return false;
    }
}