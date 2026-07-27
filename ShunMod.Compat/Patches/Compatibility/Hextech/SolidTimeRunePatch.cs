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
// 改动：
//   1. 移除 pile.Type == 6（卡必须在牌组堆中）限制
//   2. DeckVersion 为 null 时按卡牌 CanconicalInstance.Id 在牌组中查找匹配
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
        Log.Info("[SolidTimeRunePatch] Applied — removed pile.Type == 6 restriction");
    }

    /// <summary>
    ///     Prefix 替换原逻辑：
    ///     1. 移除 pile.Type == 6 检查
    ///     2. DeckVersion 为 null 时按 card canonical ID 在牌组中查找匹配
    ///
    ///     原版：
    ///         if (pile != null && pile.Type == 6 && deckCard.Type == 3)
    ///     修改后：
    ///         if (deckCard.Type == CardType.Power)  // 优先判 DeckVersion
    ///         或按 canonical ID 在牌组中匹配            // DeckVersion 为 null 时补底
    /// </summary>
    // ReSharper disable RedundantAssignment — __result 全部路径覆盖，Rider 认为最后一条多余
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

            // ── 路径 A：直接通过 DeckVersion 匹配 ──
            deckCard = combatCard.DeckVersion;
            if (deckCard != null
                && deckCard.Owner == owner
                && deckCard.Type == CardType.Power
                && owner.Deck.Cards.Contains(deckCard))
            {
                __result = true;
                return false;
            }

            // ── 路径 B：DeckVersion 为 null 或不在牌组中 → 按 canonical ID 查找 ──
            var canonicalId = combatCard.CanonicalInstance.Id;

            // 在牌组中找同 ID 的能力卡（不限制 pile type）
            deckCard = owner.Deck.Cards.FirstOrDefault(c =>
                c.CanonicalInstance.Id.Category == canonicalId.Category
                && c.CanonicalInstance.Id.Entry == canonicalId.Entry
                && c.Type == CardType.Power);

            if (deckCard != null)
            {
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