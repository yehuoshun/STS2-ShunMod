using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Compat.Patches.Compatibility.Hextech;

// ═══════════════════════════════════════════════════════════════════════════════
// SolidTimeRune 修改
// 海克斯"凝固时间符文"（SolidTime Rune）：
//   原效果：打出牌组内的能力卡时，将其从牌组中移除。
//   修改后：打出任意能力卡时，存储卡牌信息，战斗开始时触发效果。
//           若该卡在牌组中存在，同时从牌组中移除。
//
// 改动：
//   TryGetDeckPower — 移除 pile.Type == 6 限制
//   AfterCardPlayed — 对 DeckVersion 为 null 的生成卡直接存储+闪光
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedMember.Local — Prefix 由 Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance / __result 命名约定
public static class SolidTimeRunePatch
{
    private const string ModId = ModEntry.ModId;

    // ── 反射缓存 ──
    private static readonly Type? SolidTimeRuneType =
        AccessTools.TypeByName("HextechRunes.SolidTimeRune");

    private static readonly PropertyInfo? OwnerProperty =
        SolidTimeRuneType?.BaseType != null
            ? AccessTools.Property(SolidTimeRuneType.BaseType, "Owner")
            : null;

    private static readonly MethodInfo? TryGetDeckPowerMethod =
        SolidTimeRuneType != null
            ? AccessTools.Method(SolidTimeRuneType, "TryGetDeckPower")
            : null;

    private static readonly MethodInfo? AppendStoredCardMethod =
        SolidTimeRuneType != null
            ? AccessTools.Method(SolidTimeRuneType, "AppendStoredCard")
            : null;

    private static readonly MethodInfo? FlashMethod = GetFlashMethod();

    private static MethodInfo? GetFlashMethod()
    {
        if (SolidTimeRuneType == null) return null;
        for (var t = SolidTimeRuneType; t != null; t = t.BaseType)
        {
            var m = AccessTools.Method(t, "Flash");
            if (m != null) return m;
        }
        return null;
    }

    private static bool _applied;

    public static void Apply(Harmony harmony)
    {
        if (_applied) return;
        if (SolidTimeRuneType == null)
        {
            Log.Warn("[SolidTimeRunePatch] HextechRunes.SolidTimeRune not found — skipping");
            return;
        }

        // Patch 1: TryGetDeckPower — 移除 pile.Type == 6
        if (TryGetDeckPowerMethod != null)
        {
            var prefix = AccessTools.Method(typeof(SolidTimeRunePatch), nameof(PrefixTryGetDeckPower));
            harmony.Patch(TryGetDeckPowerMethod, prefix: new HarmonyMethod(prefix));
            Log.Info("[SolidTimeRunePatch] TryGetDeckPower patched");
        }

        // Patch 2: AfterCardPlayed — 处理生成卡（DeckVersion 为 null）
        var afterCardPlayed = AccessTools.Method(SolidTimeRuneType, "AfterCardPlayed");
        if (afterCardPlayed != null)
        {
            var prefix = AccessTools.Method(typeof(SolidTimeRunePatch), nameof(PrefixAfterCardPlayed));
            harmony.Patch(afterCardPlayed, prefix: new HarmonyMethod(prefix));
            Log.Info("[SolidTimeRunePatch] AfterCardPlayed patched");
        }

        _applied = true;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Patch 1: TryGetDeckPower — 移除 pile.Type == 6 限制
    // ═══════════════════════════════════════════════════════════════════════════

    // ReSharper disable RedundantAssignment
    private static bool PrefixTryGetDeckPower(
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

            deckCard = combatCard.DeckVersion;
            if (deckCard != null
                && deckCard.Owner == owner
                && deckCard.Type == CardType.Power
                && owner.Deck.Cards.Contains(deckCard))
            {
                __result = true;
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[SolidTimeRunePatch/TryGetDeckPower] {ex.GetType().Name}: {ex.Message}");
        }

        deckCard = null;
        __result = false;
        return false;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Patch 2: AfterCardPlayed — 生成卡独立处理
    // ═══════════════════════════════════════════════════════════════════════════
    //
    // 原版 AfterCardPlayed 逻辑：
    //   if (Owner != null && Card.Owner == Owner && Card.Type == Power
    //       && TryGetDeckPower(Card, out deckCard))
    //   {
    //       AppendStoredCard(deckCard);
    //       Flash();
    //       CardPileCmd.RemoveFromDeck(deckCard, false);
    //   }
    //
    // 生成卡（DeckVersion == null）时 TryGetDeckPower 永远返回 false，
    // 原版什么都做不了。Prefix 在这之前拦截，自己存+闪光。
    // ═══════════════════════════════════════════════════════════════════════════

    // ReSharper disable RedundantAssignment
    private static bool PrefixAfterCardPlayed(
        object __instance,
        PlayerChoiceContext context,
        CardPlay cardPlay)
    {
        try
        {
            var card = cardPlay.Card;
            if (card == null
                || card.Type != CardType.Power
                || card.DeckVersion != null)  // 有 DeckVersion 的交原版
            {
                return true;
            }

            var owner = OwnerProperty?.GetValue(__instance) as Player;
            if (owner == null) return true;

            // 存储生成卡本身（CanonicalInstance.Id + upgrades）
            AppendStoredCardMethod?.Invoke(__instance, [card]);
            FlashMethod?.Invoke(__instance, null);

            Log.Info($"[SolidTimeRunePatch] Stored generated card: {card.Title}");
        }
        catch (Exception ex)
        {
            Log.Error($"[SolidTimeRunePatch/AfterCardPlayed] {ex.GetType().Name}: {ex.Message}");
        }

        // 继续走原版 AfterCardPlayed（TryGetDeckPower 返回 false，只会 goto end，无事发生）
        return true;
    }
}