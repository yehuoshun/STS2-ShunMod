using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Saves;
namespace STS2ShunMod.STS2_ShunModCode.Patches.Combat;

/// <summary>
///     显示总伤害 — 多段卡/X卡在卡牌描述末尾追加总伤害 = 单段伤害 × 段数。
///     中英双语显示。
/// </summary>
[HarmonyPatch]
public static class ShowTotalDamage
{
    // ═══════════════════════════════════════════════════════════
    //  目标方法缓存
    // ═══════════════════════════════════════════════════════════
    //
    //  缓存设计原因：
    //  1. CardModel.GetDescriptionForPile 是游戏内建私有方法，
    //     程序集加载后不会变化。Harmony 的 TargetMethod() 在
    //     Prepare 阶段会被调用，每次调用都全量反射扫描 CardModel
    //     的所有方法（GetMethods + FirstOrDefault）是纯浪费。
    //  2. C# static readonly 字段由 CLR 在类型加载时保证只初始化一次，
    //     隐式线程安全，不需要额外同步。
    //  3. 如果游戏移除了这个方法（BuildTargetMethod 返回 null），
    //     TargetMethodCache 就是 null，Prepare 检测跳过补丁，
    //     行为与原版本完全一致。
    //
    // ═══════════════════════════════════════════════════════════
    private static readonly MethodBase? TargetMethodCache = BuildTargetMethod();

    private static MethodBase? TargetMethod() => TargetMethodCache;

    private static MethodBase? BuildTargetMethod()
    {
        return typeof(CardModel).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "GetDescriptionForPile" && m.GetParameters().Length == 3);
    }

    private static bool Prepare()
    {
        if (TargetMethodCache == null)
        {
            Log.Error("[总伤害] 未找到 CardModel.GetDescriptionForPile 私有方法，补丁跳过！");
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
            if (totalHits <= 1) return;

            var perHitTruncated = (int)perHit;
            var total = perHitTruncated * totalHits;
            var perHitStr = perHitTruncated.ToString();
            var totalStr = total.ToString();
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