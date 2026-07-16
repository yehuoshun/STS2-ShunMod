using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Relics;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

/// <summary>
///     NRelic 右键点击补丁 — 为 ShunMod 遗物添加右键交互支持。
///
///     在 NRelic._Ready 后连接 GuiInput 信号，检测右键点击 / 控制器取消键，
///     若目标遗物是 ShunModEndlessLife，触发其右键动作。
/// </summary>
[HarmonyPatch(typeof(NRelic), nameof(NRelic._Ready))]
internal static class RelicRightClickPatch
{
    private const string MetaKey = "shunmod_relic_right_click_bound";

    [HarmonyPostfix]
    private static void Postfix(NRelic __instance)
    {
        if (__instance.HasMeta(MetaKey))
            return;

        __instance.SetMeta(MetaKey, true);
        __instance.Connect(Control.SignalName.GuiInput, Callable.From((InputEvent inputEvent) => OnGuiInput(__instance, inputEvent)));
    }

    private static void OnGuiInput(NRelic relicNode, InputEvent inputEvent)
    {
        var viewport = relicNode.GetViewport();
        if (viewport.IsInputHandled())
            return;

        if (!TryGetTrigger(relicNode, inputEvent, out _))
            return;

        // 只处理我们自己的遗物
        if (relicNode.Model is not ShunModEndlessLife endlessLife)
            return;

        // 战斗中进行
        if (!CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsEnding
            || CombatManager.Instance.PlayerActionsDisabled)
            return;

        viewport.SetInputAsHandled();

        // 使用 BlockingPlayerChoiceContext（不涉及游戏动作同步，纯 UI 选择）
        var choiceContext = new BlockingPlayerChoiceContext();
        TaskHelper.RunSafely(endlessLife.ExecuteRightClick(choiceContext));
    }

    private static bool TryGetTrigger(Control node, InputEvent inputEvent, out bool isController)
    {
        switch (inputEvent)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Right } mouseButton when mouseButton.IsReleased():
                isController = false;
                return true;
            case InputEventAction { Action: var action } actionEvent
                when action == MegaInput.cancel && actionEvent.IsPressed() && node.HasFocus():
                isController = true;
                return true;
            default:
                isController = default;
                return false;
        }
    }
}