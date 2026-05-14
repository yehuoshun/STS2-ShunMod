using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;
using STS2_ShunMod.Ui;

namespace STS2_ShunMod.Patches;

/// <summary>
/// 注入 CompactPotionDrawer，隐藏原版药水栏。
/// </summary>
[HarmonyPatchCategory("UI")]
internal static class CompactPotionDrawerPatch
{
    /// <summary>
    /// NPotionContainer.Initialize 完成后挂载紧凑抽屉。
    /// 此时场景树、NRun.Instance、GlobalUi 均已就绪。
    /// </summary>
    [HarmonyPatch(typeof(NPotionContainer), "Initialize")]
    [HarmonyPostfix]
    private static void AfterInitialize(NPotionContainer __instance)
    {
        CompactPotionDrawer.Attach(__instance);
    }

    /// <summary>
    /// 修正焦点链：药水 holder → 抽屉按钮。
    /// </summary>
    [HarmonyPatch(typeof(NPotionContainer), "UpdateNavigation")]
    [HarmonyPostfix]
    private static void FixNavigation(NPotionContainer __instance)
    {
        var globalUi = NRun.Instance?.GlobalUi;
        if (globalUi == null) return;
        var drawer = globalUi.GetNodeOrNull<CompactPotionDrawer>("STS2ShunCompactPotionDrawer");
        var btn = drawer?.GetNodeOrNull<Button>("PotionDrawerBtn");
        if (btn == null) return;

        // 药水栏已不可见，焦点直接跳到按钮
        var holdersField = AccessTools.Field(typeof(NPotionContainer), "_holders");
        if (holdersField?.GetValue(__instance) is not System.Collections.IList holders) return;
        foreach (var h in holders)
        {
            if (h is Control c && GodotObject.IsInstanceValid(c))
                c.FocusNeighborBottom = btn.GetPath();
        }
    }
}
