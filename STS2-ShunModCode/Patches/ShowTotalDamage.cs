using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 显示总伤害
// 多段卡 / X 卡在描述末尾追加总伤害 = 单段伤害 × 段数
// ════════════════════════════════════════════════════════

[HarmonyPatch]
public static class ShowTotalDamage_Description
{
    /// <summary>GetEnchantedReplayCount 反射调用（CI 编译兼容）</summary>
    private static readonly MethodInfo? GetReplayMethod =
        AccessTools.Method(typeof(CardModel), "GetEnchantedReplayCount");

    private static MethodBase TargetMethod()
    {
        // 私有方法 GetDescriptionForPile(PileType, DescriptionPreviewType, Creature?)
        // DescriptionPreviewType 是 private 嵌套枚举，无法直接用 typeof
        var previewType = typeof(CardModel).GetNestedType("DescriptionPreviewType",
            BindingFlags.NonPublic)!;
        return AccessTools.Method(typeof(CardModel), "GetDescriptionForPile",
            [typeof(PileType), previewType, typeof(Creature)]);
    }

    private static decimal GetDamageValue(CardModel card)
    {
        foreach (var key in new[] { "CalculatedDamage", "Damage" })
        {
            if (card.DynamicVars.TryGetValue(key, out var dv) && dv.PreviewValue > 0)
                return dv.PreviewValue;
        }
        return 0;
    }

    private static void Postfix(CardModel __instance, ref string __result)
    {
        int replays = GetReplayMethod?.Invoke(__instance, null) as int? ?? 0;
        if (replays <= 0) return;

        var perHit = GetDamageValue(__instance);
        if (perHit <= 0) return;

        int totalHits = replays + 1;
        decimal total = perHit * totalHits;

        __result += $"\n[color=#ffcc00]({perHit} × {totalHits} = {total} total damage)[/color]";
    }
}
