using System.Diagnostics.CodeAnalysis;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Runs;
using ShunMod.Shun.Relics;

namespace ShunMod.Shun.Patches;

// ReSharper disable UnusedType.Global — Harmony 反射调用
// ReSharper disable UnusedMember.Local — Harmony 反射调用
/// <summary>
///     NRelic 右键点击补丁 — 为 ShunMod 遗物添加右键交互。
///     NRelic 在 .tscn 中很可能设了 MouseFilter.Ignore（让事件穿透到父级 NRelicInventoryHolder），
///     所以 NRelic.GuiInput 信号发不出来。改为从 NRelicInventoryHolder.MouseReleased 信号捕获右键。
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

        // NRelic 是视觉节点，MouseFilter 被 .tscn 设为 Ignore（让事件穿透到 InventoryHolder）。
        // 不修改 Icon/Outline 的 MouseFilter，直接注册到父级 NRelicInventoryHolder 的 MouseReleased 信号。
        // NRelicInventoryHolder 的 _Ready 比 NRelic 早，信号已就绪。
        if (instance.GetParent() is not NRelicInventoryHolder holder) return;

        // 用 unique meta key 防止重复绑定
        var holderMeta = $"{MetaKey}_holder";
        if (holder.HasMeta(holderMeta)) return;

        holder.SetMeta(holderMeta, true);
        holder.Connect(NClickableControl.SignalName.MouseReleased,
            Callable.From<InputEvent>(inputEvent => OnHolderMouseReleased(holder, inputEvent)));
    }

    private static void OnHolderMouseReleased(NRelicInventoryHolder holder, InputEvent inputEvent)
    {
        // 只处理右键释放
        if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Right } mouseButton
            || !mouseButton.IsReleased())
            return;

        var viewport = holder.GetViewport();
        if (viewport.IsInputHandled())
            return;

        var relic = holder.Relic;
        if (relic.Model is not ShunModEndlessLife endlessLife)
            return;

        var combat = CombatManager.Instance;
        if (!combat.IsInProgress || combat.IsEnding)
            return;

        // 选目标中不触发右键
        if (NTargetManager.Instance?.IsInSelection == true)
            return;

        // 只响应本地玩家
        if (endlessLife.Owner == null || !LocalContext.IsMe(endlessLife.Owner))
            return;

        viewport.SetInputAsHandled();

        var player = LocalContext.GetMe(endlessLife.Owner.RunState);
        if (player == null)
            return;

        var action = new EndlessLifeRightClickAction(player, endlessLife);
        RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(action);
    }
}