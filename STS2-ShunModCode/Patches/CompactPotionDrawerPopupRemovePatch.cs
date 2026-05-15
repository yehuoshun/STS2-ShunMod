using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2_ShunMod.Ui;

namespace STS2_ShunMod.Patches;

/// <summary>
/// NPotionPopup.Remove 时重新隐藏容器装饰。
/// </summary>
[HarmonyPatchCategory("UI")]
[HarmonyPatch(typeof(NPotionPopup), "Remove")]
internal static class CompactPotionDrawerPopupRemovePatch
{
    private static void Postfix()
    {
        CompactPotionDrawer.Hide();
    }
}
