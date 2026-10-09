using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用

/// <summary>
///     超限生命本地化注册 — LocManager 就绪前不能注册，
///     挂 NMainMenu._Ready，向 powers 表注入「超限生命」的标题与描述。
///     键名 = ModelDb Slugify(OverflowLifePower) = OVERFLOW_LIFE_POWER。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal static class OverflowLifeLocalizationPatch
{
    private static bool _registered;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_registered) return;
        _registered = true;

        const string description =
            "生命上限顶格后继续增加、或满血时的溢出治疗，按每管血折成一条「命」。受到致命伤害时自动消耗一条命回满。跨战斗保留。";

        LocManager.Instance.GetTable("powers").MergeWith(new Dictionary<string, string>
        {
            ["OVERFLOW_LIFE_POWER.title"] = "超限生命",
            ["OVERFLOW_LIFE_POWER.description"] = description,
            ["OVERFLOW_LIFE_POWER.smartDescription"] = description
        });
    }
}