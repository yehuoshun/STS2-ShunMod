using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Ui;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 在 NGlobalUi.Initialize 时挂载 CompactPotionDrawer。
/// </summary>
[HarmonyPatchCategory("UI")]
[HarmonyPatch(typeof(NGlobalUi), "Initialize")]
internal static class CompactPotionDrawerAttachPatch
{
    private static void Postfix(NGlobalUi __instance, RunState runState)
    {
        CompactPotionDrawer.Attach(__instance, runState);
    }
}
