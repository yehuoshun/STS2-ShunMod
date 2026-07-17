using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;

namespace ShunMod.Tweaks.Patches.RestSite;

/// <summary>
///     休息处附魔选项 — Postfix 注入 RestSiteOption.Generate，永远多一个「附魔」选项。
/// </summary>
[HarmonyPatch(typeof(RestSiteOption), nameof(RestSiteOption.Generate))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
public static class EnchantRestSitePatch
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(Player player, ref List<RestSiteOption> __result)
    {
        __result.Add(new EnchantRestSiteOption(player));
    }
}
