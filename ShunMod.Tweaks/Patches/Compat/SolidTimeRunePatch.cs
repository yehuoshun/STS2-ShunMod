using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;

namespace ShunMod.Tweaks.Patches.Compat;

// ═══════════════════════════════════════════════════════════════════════════════
// SolidTimeRune.TryGetDeckPower 修改
// 海克斯"凝固时间符文"（SolidTime Rune）：
//   原效果：打出牌组内的能力卡时，将其从牌组中移除。
//   修改后：打出能力卡时，将其从牌组中移除（如果是牌组中存在的卡）。
//
// 关键改动：移除 pile.Type == 6（卡必须在牌组堆中）的限制，
// 只检查 deckCard 是否为能力卡且存在于牌组中。
// ═══════════════════════════════════════════════════════════════════════════════

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
// ReSharper disable InconsistentNaming — Harmony __instance 命名约定
[HarmonyPatch]
public static class SolidTimeRuneTryGetDeckPowerPatch
{
    private static Type? _solidTimeRuneType;
    private static PropertyInfo? _ownerProperty;

    /// <summary>
    ///     动态查找 HextechRunes.SolidTimeRune.TryGetDeckPower，
    ///     因为 HextechRunes 是外部 mod，编译时没有引用。
    /// </summary>
    private static MethodInfo? TargetMethod()
    {
        _solidTimeRuneType = AccessTools.TypeByName("HextechRunes.SolidTimeRune");
        if (_solidTimeRuneType == null)
        {
            Log.Warn("[SolidTimeRunePatch] HextechRunes.SolidTimeRune not found — skipping");
            return null;
        }

        // Owner 定义在 HextechRelicBase 或其父类上
        _ownerProperty = AccessTools.Property(_solidTimeRuneType.BaseType, "Owner");

        return AccessTools.Method(_solidTimeRuneType, "TryGetDeckPower");
    }

    /// <summary>
    ///     Prefix 替换原逻辑：移除 pile.Type == 6 检查。
    ///     原版：
    ///         if (pile != null && pile.Type == 6 && deckCard.Type == 3)
    ///     修改后：
    ///         if (deckCard.Type == CardType.Power)
    /// </summary>
    private static bool Prefix(
        object __instance,
        CardModel combatCard,
        ref CardModel deckCard,
        ref bool __result)
    {
        try
        {
            var owner = _ownerProperty?.GetValue(__instance) as Player;
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