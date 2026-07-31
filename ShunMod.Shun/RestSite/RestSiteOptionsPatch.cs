using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace ShunMod.Shun.RestSite;

/// <summary>
///     将火堆选项按钮从单行改为多列 GridContainer 布局。
///     Prefix: 替换 _choicesContainer 为 GridContainer。
///     Postfix: 动态计算列数、间距、缩放，调整描述文字位置。
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), "UpdateRestSiteOptions")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
internal static class RestSiteOptionsPatch
{
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Prefix(NRestSiteRoom __instance)
    {
        RestSiteLayoutHelper.EnsureFlowContainer(__instance);
    }

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(NRestSiteRoom __instance)
    {
        RestSiteLayoutHelper.AdjustLayout(__instance);
    }
}