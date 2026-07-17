using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Relics;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

/// <summary>
///     NRelic 右键点击补丁 — 为 ShunMod 遗物添加右键交互 + Q 键快捷键支持。
///
///     在 NRelic._Ready 后连接 GuiInput 信号，检测右键点击 / 控制器取消键 / Q 键，
///     若目标遗物是 ShunModEndlessLife，触发其右键动作。
/// </summary>
[HarmonyPatch(typeof(NRelic), nameof(NRelic._Ready))]
internal static class RelicRightClickPatch
{
    private const string MetaKey = "shunmod_relic_right_click_bound";
    private const string EndlessLifeHotkey = "shunmod_endless_life";
    private static bool _hotkeyInitialized;

    /// <summary>
    ///     注册 Q 键 InputMap 动作（仅一次）。
    /// </summary>
    private static void EnsureHotkeyRegistered()
    {
        if (_hotkeyInitialized)
            return;
        _hotkeyInitialized = true;

        if (!InputMap.HasAction(EndlessLifeHotkey))
        {
            InputMap.AddAction(EndlessLifeHotkey);
            var keyEvent = new InputEventKey
            {
                Keycode = Key.Q,
                Pressed = true
            };
            InputMap.ActionAddEvent(EndlessLifeHotkey, keyEvent);
        }
    }

    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(NRelic __instance)
    {
        if (__instance.HasMeta(MetaKey))
            return;

        __instance.SetMeta(MetaKey, true);
        __instance.Connect(Control.SignalName.GuiInput, Callable.From((InputEvent inputEvent) => OnGuiInput(__instance, inputEvent)));

        // 如果是生生不息遗物，注册 Q 键热键
        if (__instance.Model is ShunModEndlessLife)
        {
            EnsureHotkeyRegistered();
            NHotkeyManager.Instance?.PushHotkeyPressedBinding(EndlessLifeHotkey, OnHotkeyPressed);
            __instance.TreeExiting += () => OnRelicRemoved();
        }
    }

    private static void OnRelicRemoved()
    {
        NHotkeyManager.Instance?.RemoveHotkeyPressedBinding(EndlessLifeHotkey, OnHotkeyPressed);
    }

    private static void OnHotkeyPressed()
    {
        // 战斗中进行
        if (!CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsEnding
            || CombatManager.Instance.PlayerActionsDisabled)
            return;

        var state = CombatManager.Instance.State;
        if (state == null)
            return;

        // 找当前玩家是否有生生不息遗物
        var endlessLife = state.Players
            .SelectMany(p => p.Relics)
            .OfType<ShunModEndlessLife>()
            .FirstOrDefault();

        if (endlessLife == null)
            return;

        var choiceContext = new BlockingPlayerChoiceContext();
        TaskHelper.RunSafely(endlessLife.ExecuteRightClick(choiceContext));
    }

    private static void OnGuiInput(NRelic relicNode, InputEvent inputEvent)
    {
        var viewport = relicNode.GetViewport();
        if (viewport.IsInputHandled())
            return;

        if (!TryGetTrigger(relicNode, inputEvent))
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