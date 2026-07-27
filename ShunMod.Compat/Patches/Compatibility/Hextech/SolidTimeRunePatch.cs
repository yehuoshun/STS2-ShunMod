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
//   修改后：打出能力卡时，将其从牌组中移除（如果是牌组中存在的卡）。
//
// AfterCardPlayed 原始逻辑：
//   if (Owner != null && Card.Owner == Owner && Card.Type == Power
//       && TryGetDeckPower(Card, out deckCard))
//   {
//       AppendStoredCard(deckCard);
//       Flash();
//       CardPileCmd.RemoveFromDeck(deckCard, false);
//   }
//
// 改动 TryGetDeckPower：
//   1. 移除 pile.Type == 6 限制（卡不要求在牌组堆中）
//   2. DeckVersion 为 null 时按 canonical ID 在牌组中查找匹配
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
        Log.Info("[SolidTimeRunePatch] Applied — TryGetDeckPower: removed pile.Type == 6 + canonical fallback");
    }

    /// <summary>
    ///     Prefix 替换原逻辑：
    ///
    ///     原版：
    ///         if (pile != null && pile.Type == 6 && deckCard.Type == 3)
    ///             return Deck.Cards.Contains(deckCard);
    ///
    ///     路径 A（DeckVersion 有值）：
    ///         if (deckCard.Type == CardType.Power && Deck.Cards.Contains(deckCard))
    ///
    ///     路径 B（DeckVersion 为 null，战斗内生成卡）：
    ///         按 card.CanonicalInstance.Id 在牌组中找同 ID 的能力卡
    /// </summary>
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

            // ── 路径 A：DeckVersion 有值 ──
            deckCard = combatCard.DeckVersion;
            if (deckCard != null
                && deckCard.Owner == owner
                && deckCard.Type == CardType.Power
                && owner.Deck.Cards.Contains(deckCard))
            {
                __result = true;
                return false;
            }

            // ── 路径 B：DeckVersion 为 null → 按 canonical ID 查牌组 ──
            deckCard = owner.Deck.Cards.FirstOrDefault(c =>
                c.CanonicalInstance.Id.Category == combatCard.CanonicalInstance.Id.Category
                && c.CanonicalInstance.Id.Entry == combatCard.CanonicalInstance.Id.Entry
                && c.Type == CardType.Power);
        }
        catch (Exception ex)
        {
            Log.Error($"[SolidTimeRunePatch] {ex.GetType().Name}: {ex.Message}");
        }

        // deckCard 非 null 表示路径 B 找到了匹配
        __result = deckCard != null;
        return false;
    }
}