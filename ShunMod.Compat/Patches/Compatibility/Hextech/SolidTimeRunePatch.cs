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
// 关键改动：移除 pile.Type == 6（卡必须在牌组堆中）的限制，
// 只检查 deckCard 是否为能力卡且存在于牌组中。
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
    ///     Prefix 替换原逻辑：移除 pile.Type == 6 检查。
    ///     原版：
    ///         if (pile != null && pile.Type == 6 && deckCard.Type == 3)
    ///     修改后：
    ///         if (deckCard.Type == CardType.Power)
    /// </summary>
    // ReSharper disable RedundantAssignment — __result 全部路径覆盖，Rider 认为最后一条多余
    private static bool Prefix(
        object __instance,
        CardModel combatCard,
        ref CardModel deckCard,
        ref bool __result)
    {
        try
        {
            var owner = OwnerProperty?.GetValue(__instance) as Player;
            if (owner == null)
            {
                __result = false;
                return false;
            }

            deckCard = combatCard.DeckVersion;

            // 修改点：不再要求卡必须在"牌组堆"（pile.Type == 6）中
            // 只要 deckCard 是能力卡且牌组中确实存在，就算合法
            if (deckCard != null
                && deckCard.Owner == owner
                && deckCard.Type == CardType.Power)
            {
                __result = owner.Deck.Cards.Contains(deckCard);
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