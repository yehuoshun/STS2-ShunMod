using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Potions;
using STS2_ShunMod.Ui;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 在 NPotionContainer.UpdateNavigation 时通知 CompactPotionDrawer
/// 尝试绑定（药水容器可能动态创建，此时才在场景树中可用）。
/// </summary>
[HarmonyPatchCategory("UI")]
[HarmonyPatch(typeof(NPotionContainer), "UpdateNavigation")]
internal static class CompactPotionDrawerBindPatch
{
    private static void Postfix(NPotionContainer __instance)
    {
        if (!GodotObject.IsInstanceValid(__instance)) return;

        var run = NRun.Instance;
        var globalUi = run?.GlobalUi;
        if (globalUi == null || !GodotObject.IsInstanceValid(globalUi)) return;

        CompactPotionDrawer.TryInitFromContainer(globalUi, __instance);
    }
}
