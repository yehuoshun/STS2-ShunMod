using System.Diagnostics.CodeAnalysis;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.Relics;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
/// <summary>
///     NRelic 右键点击补丁 — 为 ShunMod 遗物添加右键交互。
///     在 NRelic._Ready 后连接 GuiInput 信号，检测右键点击 / 控制器取消键，
///     若目标遗物是 ShunModEndlessLife，触发其右键动作。
/// </summary>
[HarmonyPatch(typeof(NRelic), nameof(NRelic._Ready))]
internal static class RelicRightClickPatch
{
    private const string MetaKey = "shunmod_relic_right_click_bound";

    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(NRelic instance)
    {
        if (instance.HasMeta(MetaKey))
            return;

        instance.SetMeta(MetaKey, true);

        // Icon 和 Outline 默认 MouseFilter.Stop 会吃掉鼠标事件，GuiInput 到不了 NRelic
        instance.Icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        if (instance.Outline != null)
            instance.Outline.MouseFilter = Control.MouseFilterEnum.Ignore;

        instance.Connect(Control.SignalName.GuiInput,
            Callable.From((InputEvent inputEvent) => OnGuiInput(instance, inputEvent)));
    }

    private static void OnGuiInput(NRelic relicNode, InputEvent inputEvent)
    {
        var viewport = relicNode.GetViewport();
        if (viewport.IsInputHandled())
            return;

        if (!TryGetTrigger(relicNode, inputEvent))
            return;

        if (relicNode.Model is not ShunModEndlessLife endlessLife)
            return;

        var combat = CombatManager.Instance;
        if (combat == null || !combat.IsInProgress || combat.IsEnding)
            return;

        viewport.SetInputAsHandled();

        try
        {
            var choiceContext = new BlockingPlayerChoiceContext();
            TaskHelper.RunSafely(endlessLife.ExecuteRightClick(choiceContext));
        }
        catch (Exception e)
        {
            Log.Error($"[EndlessLife] 右键执行失败: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error(
                    $"[EndlessLife]   \u2192 inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }
    }

    private static bool TryGetTrigger(Control node, InputEvent inputEvent)
    {
        return inputEvent switch
        {
            InputEventMouseButton { ButtonIndex: MouseButton.Right } mouseButton when mouseButton.IsReleased() => true,
            InputEventAction { Action: var action } actionEvent
                when action == MegaInput.cancel && actionEvent.IsPressed() && node.HasFocus() => true,
            _ => false
        };
    }
}