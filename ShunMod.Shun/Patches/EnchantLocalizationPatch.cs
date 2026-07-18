using System.Diagnostics.CodeAnalysis;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using ShunMod.Shun.RestSite;

namespace ShunMod.Shun.Patches;

/// <summary>
///     延迟本地化注册 — 等 LocManager 就绪后再注入自定义条目。
///     挂载到 NMainMenu._Ready，此时 LocManager.Instance 可用。
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu._Ready))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
internal static class EnchantLocalizationPatch
{
    private static bool _registered;

    [HarmonyPostfix]
    private static void Postfix()
    {
        if (_registered)
            return;
        _registered = true;

        EnchantRestSiteOption.RegisterLocalization();
    }
}