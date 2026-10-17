using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace ShunMod.Tweaks.Patches.Combat;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用

/// <summary>
///     「难以杀灭」文案改写 — 补丁已取消该 Power 的限伤，原版描述（"不会超过 9 点"）会误导。
///     沿用仓库约定：LocManager 未就绪不能注册，挂 NMainMenu._Ready，向 powers 表覆盖
///     HARD_TO_KILL_POWER 的 description / smartDescription（title 保持原版不变）。
///     订阅 LocaleChangeCallback：切语言会重建 _tables（SetLanguageInternal 先换表再回调），
///     不重注入的话覆盖会被冲掉。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal static class HardToKillLocalizationPatch
{
    private static bool _registered;
    private static bool _subscribed;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_registered) return;
        _registered = true;

        Apply();

        if (_subscribed) return;
        _subscribed = true;
        LocManager.Instance.SubscribeToLocaleChange(Apply);
    }

    private static void Apply()
    {
        try
        {
            bool zh = LocManager.Instance.Language == "zhs";

            string description = zh
                ? "[gold]外骨骼虫[/gold]不再有单次受伤上限：受到的所有伤害与生命减少效果全额结算。"
                : "Damage dealt to and HP lost by [gold]Exoskeleton[/gold] are no longer capped.";

            string smartDescription = zh
                ? "[gold]{OwnerName}[/gold]不再有单次受伤上限：受到的所有伤害与生命减少效果全额结算。"
                : "Damage dealt to and HP lost by [gold]{OwnerName}[/gold] are no longer capped.";

            LocManager.Instance.GetTable("powers").MergeWith(new Dictionary<string, string>
            {
                ["HARD_TO_KILL_POWER.description"] = description,
                ["HARD_TO_KILL_POWER.smartDescription"] = smartDescription
            });
        }
        catch (Exception ex)
        {
            Log.Error($"[难以杀灭/文案覆盖] {ex.GetType().Name}: {ex.Message}");
        }
    }
}
