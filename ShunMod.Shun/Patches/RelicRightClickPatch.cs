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
///     Icon/Outline 默认 MouseFilter.Stop 会吃掉鼠标事件，GuiInput 到不了 NRelic，
///     所以设为 Ignore 让事件穿透到 NRelic，再连接 GuiInput 信号检测右键。
/// </summary>
[HarmonyPatch(typeof(NRelic), nameof(NRelic._Ready))]
internal static class RelicRightClickPatch
{
    private const string MetaKey = "shunmod_relic_right_click_bound";

    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(NRelic __instance)
    {
        if (__instance.HasMeta(MetaKey))
            return;

        __instance.SetMeta(MetaKey, true);

        // Icon 和 Outline 默认 MouseFilter.Stop 会吃掉鼠标事件，GuiInput 到不了 NRelic
        __instance.Icon.MouseFilter = Control.MouseFilterEnum.Ignore;
        __instance.Outline.MouseFilter = Control.MouseFilterEnum.Ignore;

        __instance.Connect(Control.SignalName.GuiInput,
            Callable.From((InputEvent inputEvent) => OnGuiInput(__instance, inputEvent)));
    }

    private static void OnGuiInput(NRelic relicNode, InputEvent inputEvent)
    {
        if (!TryGetTrigger(relicNode, inputEvent, out _))
            return;

        var viewport = relicNode.GetViewport();
        if (viewport.IsInputHandled())
            return;

        // 只处理我们自己的遗物
        if (relicNode.Model is not ShunModEndlessLife endlessLife)
            return;

        if (endlessLife.Owner == null)
            return;

        // 战斗中进行
        if (!CombatManager.Instance.IsInProgress
            || CombatManager.Instance.IsEnding)
            return;

        viewport.SetInputAsHandled();

        // 使用 BlockingPlayerChoiceContext（不涉及游戏动作同步，纯 UI 选择）
        // 这是 6f61c2fb 验证过的执行方式，不走 GameAction 队列避免排队被拒
        try
        {
            var choiceContext = new BlockingPlayerChoiceContext();
            TaskHelper.RunSafely(endlessLife.ExecuteRightClick(choiceContext));
        }
        catch (Exception e)
        {
            Log.Error($"[EndlessLife] 右键执行失败: {e.GetType().Name}: {e.Message}");
            if (e.InnerException != null)
                Log.Error($"[EndlessLife]   -> inner: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
        }
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