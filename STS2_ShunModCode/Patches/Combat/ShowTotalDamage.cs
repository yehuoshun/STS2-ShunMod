using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
using STS2ShunMod.STS2_ShunModCode.Settings;

namespace STS2ShunMod.STS2_ShunModCode.Patches.Combat;

/// <summary>
///     显示总伤害 — 多段卡/X卡在卡牌描述末尾追加总伤害 = 单段伤害 × 段数。
///     中英双语显示。
/// </summary>
[HarmonyPatch]
public static class ShowTotalDamage
{
    private static MethodBase? TargetMethod()
    {
        return typeof(CardModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetDescriptionForPile" && m.GetParameters().Length == 3);
    }

    private static bool Prepare()
    {
        if (TargetMethod() == null)
        {
            Log.Error("[总伤害] 未找到 CardModel.GetDescriptionForPile 私有方法，补丁跳过！");
            return false;
        }
        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        if (!PatchManager.IsEnabled("ShowTotalDamage")) return;
        try
        {
            var perHit = GetDamageValue(__instance);
            if (perHit <= 0) return;

            var nativeHits = GetNativeHitCount(__instance);
            var replays = __instance.GetEnchantedReplayCount();
            var totalHits = nativeHits * (replays + 1);
            if (totalHits <= 1) return;

            var total = perHit * totalHits;
            var lang = SaveManager.Instance.SettingsSave.Language;
            var label = lang == "zhs"
                ? $"({perHit} × {totalHits} = {total} 总伤害)"
                : $"({perHit} × {totalHits} = {total} total damage)";
            __result += $"\n[color=#ffcc00]{label}[/color]";
        }
        catch (Exception ex)
        {
            Log.Error($"[总伤害] Postfix 异常: {ex.GetType().Name}: {ex.Message}");
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

    private static int GetNativeHitCount(CardModel card)
    {
        if (card.DynamicVars.TryGetValue("Repeat", out var dv) && dv.PreviewValue > 0)
            return (int)dv.PreviewValue;
        if (card.DynamicVars.TryGetValue("CalculatedHits", out dv) && dv.PreviewValue > 0)
            return (int)dv.PreviewValue;
        return 1;
    }
}