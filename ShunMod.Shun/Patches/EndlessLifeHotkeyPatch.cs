using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

/// <summary>
///     生生不息遗物快捷键 — 战斗中按 Q 直接触发右键效果。
/// </summary>
[HarmonyPatch(typeof(NInputManager), nameof(NInputManager._UnhandledKeyInput))]
internal static class EndlessLifeHotkeyPatch
{
    [HarmonyPostfix]
    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    private static void Postfix(NInputManager __instance, InputEvent inputEvent)
    {
        // 只响应 Q 键按下（非重复触发）
        if (inputEvent is not InputEventKey { Keycode: Key.Q, Pressed: true, Echo: false })
            return;

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

        __instance.GetViewport()?.SetInputAsHandled();

        var choiceContext = new BlockingPlayerChoiceContext();
        TaskHelper.RunSafely(endlessLife.ExecuteRightClick(choiceContext));
    }
}