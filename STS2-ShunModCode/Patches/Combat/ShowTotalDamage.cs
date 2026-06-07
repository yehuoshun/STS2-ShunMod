using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;

namespace STS2_ShunMod.Patches;

// ════════════════════════════════════════════════════════
// 显示总伤害
// 多段卡 / X 卡在描述末尾追加总伤害 = 单段伤害 × 段数
// 中英双语显示
//
// 段数来源（三者乘积）：
//   1) 原生 HitCount（DynamicVars.Repeat / CalculatedHits）
//   2) 重放次数（GetEnchantedReplayCount + 1）
// 当总段数 > 1 时才追加显示。
//
// 注：补丁挂在私有方法 GetDescriptionForPile(PileType, DescriptionPreviewType, Creature?)
// 而非公开方法，因为公开方法只有一行委托调用，极易被 JIT 内联导致 Postfix 不触发。
// ════════════════════════════════════════════════════════

[HarmonyPatch]
public static class ShowTotalDamage_Description
{
    /// <summary>
    ///     找到私有方法 CardModel.GetDescriptionForPile(PileType, DescriptionPreviewType, Creature?)
    /// </summary>
    private static MethodBase TargetMethod()
    {
        return typeof(CardModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetDescriptionForPile" && m.GetParameters().Length == 3);
    }

    /// <summary>
    ///     Patch 前验证目标方法是否存在。不存在则写日志，避免静默跳过。
    /// </summary>
    private static bool Prepare()
    {
        if (TargetMethod() == null)
        {
            Log.Error("[总伤害] ❌ 未找到 CardModel.GetDescriptionForPile 私有方法(PileType, DescriptionPreviewType, Creature?)，补丁跳过！游戏 API 可能已变更。");
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            var perHit = GetDamageValue(__instance);
            if (perHit <= 0) return;

            var nativeHits = GetNativeHitCount(__instance);
            var replays = __instance.GetEnchantedReplayCount();
            var totalHits = nativeHits * (replays + 1);

            // 总段数 ≤ 1 无需显示
            if (totalHits <= 1) return;

            var total = perHit * totalHits;

            // 中英双语：total damage / 总伤害
            __result += $"\n[color=#ffcc00]({perHit} × {totalHits} = {total} total damage / 总伤害)[/color]";
        }
        catch (Exception ex)
        {
            Log.Error($"[总伤害] ❌ Postfix 异常: {ex.GetType().Name}: {ex.Message}");
            if (ex.StackTrace != null)
                Log.Error($"[总伤害] {ex.StackTrace}");
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

    /// <summary>
    ///     获取卡牌原生段数。
    ///     STS2 多段卡通过 DynamicVars.Repeat 或 CalculatedHits 声明段数。
    /// </summary>
    private static int GetNativeHitCount(CardModel card)
    {
        // 方式一：DynamicVars.Repeat — 如 GunkUp / Peck
        if (card.DynamicVars.TryGetValue("Repeat", out var dv) && dv.PreviewValue > 0)
            return (int)dv.PreviewValue;

        // 方式二：CalculatedHits — 如 HelixDrill / LunarBlast / PullFromBelow / Radiate
        if (card.DynamicVars.TryGetValue("CalculatedHits", out dv) && dv.PreviewValue > 0)
            return (int)dv.PreviewValue;

        return 1;
    }
}