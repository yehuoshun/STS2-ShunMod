using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using STS2_ShunMod.Core;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 显示总伤害
// 多段卡 / X 卡在描述末尾追加总伤害 = 单段伤害 × 段数
// 中英双语显示
//
// 源码：public string GetDescriptionForPile(PileType, Creature?)
//       内部调用 private GetDescriptionForPile(PileType, DescriptionPreviewType, Creature?)
// ════════════════════════════════════════════════════════

[HarmonyPatch(typeof(CardModel), "GetDescriptionForPile",
    [typeof(PileType), typeof(Creature)])]
public static class ShowTotalDamage_Description
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            var replays = __instance.GetEnchantedReplayCount();
            if (replays <= 0) return;

            var perHit = GetDamageValue(__instance);
            if (perHit <= 0) return;

            var totalHits = replays + 1;
            var total = perHit * totalHits;

            // 中英双语：total damage / 总伤害
            __result += $"\n[color=#ffcc00]({perHit} × {totalHits} = {total} total damage / 总伤害)[/color]";

            ShunLogger.Debug("总伤害", $"{__instance.GetType().Name}: {perHit}×{totalHits}={total}");
        }
        catch (Exception ex)
        {
            ShunLogger.Error("总伤害", ex);
        }
    }

    private static decimal GetDamageValue(CardModel card)
    {
        if (card.DynamicVars.TryGetValue("CalculatedDamage", out var dv) && dv.PreviewValue > 0)
            return dv.PreviewValue;
        if (card.DynamicVars.TryGetValue("Damage", out dv) && dv.PreviewValue > 0)
            return dv.PreviewValue;
        return 0;
    }
}