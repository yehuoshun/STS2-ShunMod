using System.Diagnostics.CodeAnalysis;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Enchantments;

namespace ShunMod.Tweaks.Patches.Enchantments;

/// <summary>
///     解除注能（Imbued）只能附魔技能牌的限制，改为所有类型均可附魔。
/// </summary>
[HarmonyPatch(typeof(Imbued), nameof(Imbued.CanEnchantCardType))]
internal static class ImbuedAllCardTypesPatch
{
    [HarmonyPrefix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static bool Prefix(ref bool __result)
    {
        __result = true;
        return false; // 跳过原方法
    }
}