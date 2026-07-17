using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;

namespace ShunMod.Tweaks.Patches.Combat;

// 显示总伤害：多段卡/X卡在描述末尾追加 "单段伤害 × 段数 = 总伤害"
// 缓存 private method GetDescriptionForPile，只反射一次
[HarmonyPatch]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class ShowTotalDamage
{
    private static readonly MethodInfo? TargetMethodCache = BuildTargetMethod();

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static MethodInfo? TargetMethod() => TargetMethodCache;

    private static MethodInfo? BuildTargetMethod()
    {
        return typeof(CardModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetDescriptionForPile" && m.GetParameters().Length == 3);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local", Justification = "Harmony 反射调用")]
    private static bool Prepare()
    {
        if (TargetMethodCache != null) return true;
        Log.Error("[总伤害] 未找到 CardModel.GetDescriptionForPile 私有方法，补丁跳过！");
        return false;
    }

    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Harmony __instance/__result 约定")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    [SuppressMessage("ReSharper", "SpellingInStringLiteral", Justification = "#ffcc00 是有效的十六进制颜色代码")]
    private static void Postfix(CardModel __instance, ref string __result)
    {
        try
        {
            var perHit = GetDamageValue(__instance);
            if (perHit <= 0) return;

            var nativeHits = GetNativeHitCount(__instance);
            var replays = __instance.GetEnchantedReplayCount();
            var totalHits = nativeHits * (replays + 1);
            if (totalHits <= 1) return;

            var perHitTruncated = (int)perHit;
            var total = perHitTruncated * totalHits;
            var perHitStr = perHitTruncated.ToString(CultureInfo.InvariantCulture);
            var totalStr = total.ToString(CultureInfo.InvariantCulture);
            var lang = SaveManager.Instance.SettingsSave.Language;
            var label = lang == "zhs"
                ? $"({perHitStr} × {totalHits} = {totalStr} 总伤害)"
                : $"({perHitStr} × {totalHits} = {totalStr} total damage)";
            __result += $"\n[color=#ffcc00]{label}[/color]";
        }
        catch (Exception ex)
        {
            Log.Error($"[总伤害] Postfix 异常: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static readonly string[] DamageKeys = ["CalculatedDamage", "Damage"];

    private static decimal GetDamageValue(CardModel card)
    {
        foreach (var key in DamageKeys)
            if (card.DynamicVars.TryGetValue(key, out var dv) && dv.PreviewValue > 0)
                return dv.PreviewValue;
        return 0;
    }

    private static readonly string[] HitCountKeys = ["Repeat", "CalculatedHits"];

    private static int GetNativeHitCount(CardModel card)
    {
        foreach (var key in HitCountKeys)
            if (card.DynamicVars.TryGetValue(key, out var dv) && dv.PreviewValue > 0)
                return (int)dv.PreviewValue;
        return 1;
    }
}