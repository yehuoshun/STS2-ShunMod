using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace ShunMod.Tweaks.Patches.Overflow;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用

/// <summary>
///     超限本地化注册 — LocManager 就绪前不能注册（同 EnchantLocalizationPatch 约定），
///     挂 NMainMenu._Ready，向 powers 表注入「超限」的标题与描述。
///     键名 = ModelDb Slugify(OverflowPower) = OVERFLOW_POWER。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal static class OverflowLocalizationPatch
{
    private static bool _registered;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_registered) return;
        _registered = true;

        string threshold = OverflowConfig.Threshold.ToString("N0");
        string description =
            $"护盾达到 {threshold} 后，超出部分按每层 {threshold} 记账存放；护盾不足时自动拆层抵扣，与格挡同生共灭。无法被移除。";

        LocManager.Instance.GetTable("powers").MergeWith(new Dictionary<string, string>
        {
            ["OVERFLOW_POWER.title"] = "超限",
            ["OVERFLOW_POWER.description"] = description,
            ["OVERFLOW_POWER.smartDescription"] = description
        });
    }
}